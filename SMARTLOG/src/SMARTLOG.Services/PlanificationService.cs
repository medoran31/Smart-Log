using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services;

/// <summary>
/// Seul point de calcul et d'écriture du planning. Le moteur CPM est pur
/// (CpmCalculateur) ; ce service charge les données, applique le résultat et
/// journalise chaque opération via IJournalService.
/// </summary>
public class PlanificationService : IPlanificationService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;

    public PlanificationService(SmartlogDbContext db, IJournalService journal)
    {
        _db = db;
        _journal = journal;
    }

    public async Task<TacheResume> CreerTacheAsync(
        Guid projetId, Guid? parentTacheId, string libelle,
        DateOnly dateDebut, DateOnly dateFin, Guid utilisateurId,
        CancellationToken ct = default)
    {
        VerifierLibelle(libelle);
        VerifierDates(dateDebut, dateFin);

        if (!await _db.Projets.AnyAsync(p => p.Id == projetId, ct))
            throw new InvalidOperationException($"Projet {projetId} introuvable.");

        if (parentTacheId.HasValue)
        {
            var parent = await _db.Taches.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == parentTacheId.Value, ct)
                ?? throw new InvalidOperationException($"Tâche parente {parentTacheId} introuvable.");
            if (parent.ProjetId != projetId)
                throw new ArgumentException("La tâche parente appartient à un autre projet.", nameof(parentTacheId));
        }

        var tache = new Tache
        {
            ProjetId = projetId,
            ParentTacheId = parentTacheId,
            Libelle = libelle.Trim(),
            DateDebutPrevue = dateDebut,
            DateFinPrevue = dateFin
        };
        _db.Taches.Add(tache);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Tache), tache.Id, "Creation", null,
            new { tache.Id, tache.ProjetId, tache.ParentTacheId, tache.Libelle, tache.DateDebutPrevue, tache.DateFinPrevue },
            ct);

        var planning = await RecalculerAsync(projetId, utilisateurId, ct);
        return planning.Taches.Single(t => t.Id == tache.Id);
    }

    public async Task<TacheResume> ModifierTacheAsync(
        Guid tacheId, string libelle, DateOnly dateDebut, DateOnly dateFin,
        Guid utilisateurId, CancellationToken ct = default)
    {
        VerifierLibelle(libelle);
        VerifierDates(dateDebut, dateFin);

        var tache = await _db.Taches.FirstOrDefaultAsync(t => t.Id == tacheId, ct)
            ?? throw new InvalidOperationException($"Tâche {tacheId} introuvable.");

        var avant = new { tache.Libelle, tache.DateDebutPrevue, tache.DateFinPrevue };
        tache.Libelle = libelle.Trim();
        tache.DateDebutPrevue = dateDebut;
        tache.DateFinPrevue = dateFin;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Tache), tache.Id, "ModificationPlanning", avant,
            new { tache.Libelle, tache.DateDebutPrevue, tache.DateFinPrevue }, ct);

        // La nouvelle date est une contrainte de l'opération : elle est
        // propagée aux successeurs par le passage avant du CPM.
        var planning = await RecalculerInterneAsync(
            tache.ProjetId, tache.Id, dateDebut.DayNumber, utilisateurId, ct);
        return planning.Taches.Single(t => t.Id == tache.Id);
    }

    public async Task SupprimerTacheAsync(
        Guid tacheId, Guid utilisateurId, CancellationToken ct = default)
    {
        var tache = await _db.Taches.FirstOrDefaultAsync(t => t.Id == tacheId, ct)
            ?? throw new InvalidOperationException($"Tâche {tacheId} introuvable.");

        if (await _db.Taches.AnyAsync(t => t.ParentTacheId == tacheId, ct))
            throw new InvalidOperationException("Impossible de supprimer une tâche qui a des sous-tâches.");
        if (await _db.LiaisonsTache.AnyAsync(
                l => l.TachePredecesseurId == tacheId || l.TacheSuccesseurId == tacheId, ct))
            throw new InvalidOperationException("Impossible de supprimer une tâche liée : supprimez d'abord ses liaisons.");

        var avant = new { tache.Id, tache.ProjetId, tache.Libelle, tache.DateDebutPrevue, tache.DateFinPrevue };
        var projetId = tache.ProjetId;
        _db.Taches.Remove(tache);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Tache), tacheId, "SuppressionTache", avant, null, ct);
        await RecalculerAsync(projetId, utilisateurId, ct);
    }

    public async Task<LiaisonResume> AjouterLiaisonAsync(
        Guid predecesseurId, Guid successeurId, TypeLiaison typeLiaison,
        Guid utilisateurId, CancellationToken ct = default)
    {
        if (predecesseurId == successeurId)
            throw new ArgumentException("Une tâche ne peut pas être liée à elle-même.", nameof(successeurId));
        if (!Enum.IsDefined(typeLiaison))
            throw new ArgumentOutOfRangeException(nameof(typeLiaison), "Type de liaison inconnu.");

        var taches = await _db.Taches.AsNoTracking()
            .Where(t => t.Id == predecesseurId || t.Id == successeurId)
            .ToListAsync(ct);
        var predecesseur = taches.FirstOrDefault(t => t.Id == predecesseurId)
            ?? throw new InvalidOperationException($"Tâche prédécesseur {predecesseurId} introuvable.");
        var successeur = taches.FirstOrDefault(t => t.Id == successeurId)
            ?? throw new InvalidOperationException($"Tâche successeur {successeurId} introuvable.");
        if (predecesseur.ProjetId != successeur.ProjetId)
            throw new ArgumentException("Les deux tâches appartiennent à des projets différents.");

        var doublon = await _db.LiaisonsTache.AnyAsync(l =>
            (l.TachePredecesseurId == predecesseurId && l.TacheSuccesseurId == successeurId) ||
            (l.TachePredecesseurId == successeurId && l.TacheSuccesseurId == predecesseurId), ct);
        if (doublon)
            throw new InvalidOperationException("Ces deux tâches sont déjà liées.");

        var liaison = new LiaisonTache
        {
            TachePredecesseurId = predecesseurId,
            TacheSuccesseurId = successeurId,
            TypeLiaison = typeLiaison
        };
        _db.LiaisonsTache.Add(liaison);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(LiaisonTache), liaison.Id, "AjoutLiaison", null,
            new
            {
                liaison.Id,
                liaison.TachePredecesseurId,
                liaison.TacheSuccesseurId,
                liaison.TypeLiaison
            },
            ct);

        var planning = await RecalculerAsync(predecesseur.ProjetId, utilisateurId, ct);
        return planning.Liaisons.Single(l => l.Id == liaison.Id);
    }

    public async Task SupprimerLiaisonAsync(
        Guid liaisonId, Guid utilisateurId, CancellationToken ct = default)
    {
        var liaison = await _db.LiaisonsTache
            .FirstOrDefaultAsync(l => l.Id == liaisonId, ct)
            ?? throw new InvalidOperationException($"Liaison {liaisonId} introuvable.");

        var avant = new
        {
            liaison.Id,
            liaison.TachePredecesseurId,
            liaison.TacheSuccesseurId,
            liaison.TypeLiaison
        };
        var projetId = (await _db.Taches.AsNoTracking()
            .Where(t => t.Id == liaison.TachePredecesseurId)
            .Select(t => t.ProjetId)
            .SingleAsync(ct));

        _db.LiaisonsTache.Remove(liaison);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(LiaisonTache), liaisonId, "SuppressionLiaison", avant, null, ct);
        await RecalculerAsync(projetId, utilisateurId, ct);
    }

    public Task<PlanningDetail> RecalculerAsync(
        Guid projetId, Guid utilisateurId, CancellationToken ct = default) =>
        RecalculerInterneAsync(projetId, null, null, utilisateurId, ct);

    public async Task<PlanningDetail> ObtenirAsync(Guid projetId, CancellationToken ct = default)
    {
        var (taches, liaisons) = await ChargerDonneesAsync(projetId, ct);
        return VersDetail(projetId, taches, liaisons, null);
    }

    public async Task<PlanningDetail> CalculerAsync(Guid projetId, CancellationToken ct = default)
    {
        var (taches, liaisons) = await ChargerDonneesAsync(projetId, ct);
        var resultat = CpmCalculateur.Calculer(ConstruireCpm(taches, liaisons), new Dictionary<Guid, int>());
        return VersDetail(projetId, taches, liaisons, resultat);
    }

    private async Task<PlanningDetail> RecalculerInterneAsync(
        Guid projetId,
        Guid? tacheContrainte,
        int? debutImpose,
        Guid utilisateurId,
        CancellationToken ct)
    {
        var (taches, liaisons) = await ChargerDonneesAsync(projetId, ct);
        var imposees = new Dictionary<Guid, int>();
        if (debutImpose.HasValue && tacheContrainte.HasValue)
            imposees[tacheContrainte.Value] = debutImpose.Value;

        var resultat = CpmCalculateur.Calculer(ConstruireCpm(taches, liaisons), imposees);

        var avant = taches.ToDictionary(t => t.Id, t => new
        {
            t.DateDebutPrevue,
            t.DateFinPrevue,
            t.EstCheminCritique
        });

        foreach (var tache in taches)
        {
            tache.DateDebutPrevue = DateOnly.FromDayNumber(resultat.Debuts[tache.Id]);
            tache.DateFinPrevue = DateOnly.FromDayNumber(resultat.Fins[tache.Id]);
            tache.EstCheminCritique = resultat.Critiques.Contains(tache.Id);
        }
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projetId, "RecalculPlanning",
            avant,
            new
            {
                resultat.DebutProjet,
                resultat.FinProjet,
                TachesCritiques = resultat.Critiques.Count
            },
            ct);

        return VersDetail(projetId, taches, liaisons, resultat);
    }

    private async Task<(List<Tache> Taches, List<LiaisonTache> Liaisons)> ChargerDonneesAsync(
        Guid projetId, CancellationToken ct)
    {
        var taches = await _db.Taches
            .Where(t => t.ProjetId == projetId)
            .ToListAsync(ct);
        var ids = taches.Select(t => t.Id).ToList();
        var liaisons = await _db.LiaisonsTache
            .Where(l => ids.Contains(l.TachePredecesseurId) && ids.Contains(l.TacheSuccesseurId))
            .ToListAsync(ct);
        return (taches, liaisons);
    }

    private static List<TacheCpm> ConstruireCpm(
        IReadOnlyList<Tache> taches, IReadOnlyList<LiaisonTache> liaisons)
    {
        var predecesseurs = taches.ToDictionary(t => t.Id, _ => new List<LienCpm>());
        var successeurs = taches.ToDictionary(t => t.Id, _ => new List<LienCpm>());

        foreach (var liaison in liaisons)
        {
            if (!predecesseurs.ContainsKey(liaison.TachePredecesseurId) ||
                !predecesseurs.ContainsKey(liaison.TacheSuccesseurId))
                continue;

            var lien = new LienCpm(
                liaison.TachePredecesseurId, liaison.TacheSuccesseurId, liaison.TypeLiaison);
            predecesseurs[liaison.TacheSuccesseurId].Add(lien);
            successeurs[liaison.TachePredecesseurId].Add(lien);
        }

        return taches.Select(t => new TacheCpm(
            t.Id,
            t.DateFinPrevue.DayNumber - t.DateDebutPrevue.DayNumber + 1,
            t.DateDebutPrevue.DayNumber,
            predecesseurs[t.Id],
            successeurs[t.Id])).ToList();
    }

    private static PlanningDetail VersDetail(
        Guid projetId,
        IReadOnlyList<Tache> taches,
        IReadOnlyList<LiaisonTache> liaisons,
        ResultatCpm? resultat)
    {
        var ordre = OrdreWbs(taches, out var niveaux);
        var resumes = ordre.Select(t => new TacheResume(
            t.Id,
            t.ProjetId,
            t.ParentTacheId,
            t.Libelle,
            niveaux[t.Id],
            t.DateFinPrevue.DayNumber - t.DateDebutPrevue.DayNumber + 1,
            t.DateDebutPrevue,
            t.DateFinPrevue,
            t.DateDebutReelle,
            t.DateFinReelle,
            t.AvancementPct,
            resultat?.Critiques.Contains(t.Id) ?? t.EstCheminCritique,
            resultat?.Marges.GetValueOrDefault(t.Id) ?? 0,
            liaisons.Count(l => l.TachePredecesseurId == t.Id))).ToList();

        var libelles = taches.ToDictionary(t => t.Id, t => t.Libelle);
        var liaisonsResume = liaisons.Select(l => new LiaisonResume(
            l.Id,
            l.TachePredecesseurId,
            l.TacheSuccesseurId,
            l.TypeLiaison,
            LibellerType(l.TypeLiaison),
            libelles.GetValueOrDefault(l.TachePredecesseurId, "?"),
            libelles.GetValueOrDefault(l.TacheSuccesseurId, "?")))
            .OrderBy(l => l.PredecesseurLibelle).ThenBy(l => l.SuccesseurLibelle).ToList();

        var critiques = resumes.Where(t => t.EstCheminCritique).Select(t => t.Id).ToList();

        return new PlanningDetail(
            projetId,
            resumes,
            liaisonsResume,
            critiques,
            resultat is not null ? DateOnly.FromDayNumber(resultat.DebutProjet)
                : resumes.Count > 0 ? resumes.Min(t => t.DateDebutPrevue) : DateOnly.MinValue,
            resultat is not null ? DateOnly.FromDayNumber(resultat.FinProjet)
                : resumes.Count > 0 ? resumes.Max(t => t.DateFinPrevue) : DateOnly.MinValue,
            resultat is not null ? resultat.FinProjet - resultat.DebutProjet + 1
                : resumes.Count > 0
                    ? resumes.Max(t => t.DateFinPrevue).DayNumber - resumes.Min(t => t.DateDebutPrevue).DayNumber + 1
                    : 0);
    }

    /// <summary>Aplatit la WBS en pré-ordre (parent avant ses enfants).</summary>
    private static List<Tache> OrdreWbs(IReadOnlyList<Tache> taches, out Dictionary<Guid, int> niveaux)
    {
        var niveauxLocal = new Dictionary<Guid, int>();
        var resultat = new List<Tache>();
        var visites = new HashSet<Guid>();
        var parId = taches.ToDictionary(t => t.Id);
        var racine = Guid.Empty; // sentinelle pour les tâches sans parent
        var enfants = taches
            .GroupBy(t => t.ParentTacheId ?? racine)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.DateDebutPrevue).ThenBy(t => t.Libelle).ToList());

        void Parcourir(Guid parentId, int niveau)
        {
            if (!enfants.TryGetValue(parentId, out var liste))
                return;
            foreach (var tache in liste)
            {
                if (!visites.Add(tache.Id))
                    continue;
                niveauxLocal[tache.Id] = niveau;
                resultat.Add(tache);
                Parcourir(tache.Id, niveau + 1);
            }
        }

        Parcourir(racine, 0);

        // Tâches dont le parent a été supprimé : remontées à la racine.
        foreach (var tache in taches.Where(t =>
                     t.ParentTacheId.HasValue && !parId.ContainsKey(t.ParentTacheId.Value)))
        {
            if (!visites.Add(tache.Id))
                continue;
            niveauxLocal[tache.Id] = 0;
            resultat.Add(tache);
            Parcourir(tache.Id, 1);
        }

        niveaux = niveauxLocal;
        return resultat;
    }

    private static string LibellerType(TypeLiaison type) => type switch
    {
        TypeLiaison.FinDebut => "Fin → Début",
        TypeLiaison.DebutDebut => "Début → Début",
        TypeLiaison.FinFin => "Fin → Fin",
        TypeLiaison.DebutFin => "Début → Fin",
        _ => type.ToString()
    };

    private static void VerifierLibelle(string libelle)
    {
        if (string.IsNullOrWhiteSpace(libelle))
            throw new ArgumentException("Le libellé de la tâche est obligatoire.", nameof(libelle));
    }

    private static void VerifierDates(DateOnly debut, DateOnly fin)
    {
        if (fin < debut)
            throw new ArgumentException("La date de fin ne peut pas précéder la date de début.", nameof(fin));
    }
}
