using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

public class ProjetService : IProjetService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IAvenantService _avenants;
    private readonly IRecalculCroiseService _recalcul;

    public ProjetService(
        SmartlogDbContext db,
        IJournalService journal,
        IAvenantService avenants,
        IRecalculCroiseService recalcul)
    {
        _db = db;
        _journal = journal;
        _avenants = avenants;
        _recalcul = recalcul;
    }

    public async Task<IReadOnlyList<ProjetResume>> ListerAsync(CancellationToken ct = default)
    {
        var projets = await _db.Projets
            .AsNoTracking()
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

        return projets.Select(p => new ProjetResume(
            p.Id,
            p.Code,
            p.Intitule,
            p.Localisation,
            p.MontantInitial,
            p.MontantActualise,
            p.DelaiContractuelJours,
            p.DelaiActualiseJours,
            LibellerStatut(p.Statut))).ToList();
    }

    public async Task<ProjetDetail?> ObtenirAsync(Guid projetId, CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .AsNoTracking()
            .Include(p => p.MaitreOuvrage)
            .Include(p => p.MaitreOeuvre)
            .Include(p => p.Entreprise)
            .FirstOrDefaultAsync(p => p.Id == projetId, ct);

        return projet is null ? null : VersDetail(projet);
    }

    public async Task<ProjetDetail> CreerAsync(
        ProjetCreation donnees,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(donnees.Code))
            throw new ArgumentException("Le code du projet est obligatoire.", nameof(donnees));
        if (string.IsNullOrWhiteSpace(donnees.Intitule))
            throw new ArgumentException("L'intitulé du projet est obligatoire.", nameof(donnees));
        if (await _db.Projets.AnyAsync(p => p.Code == donnees.Code, ct))
            throw new InvalidOperationException($"Le code {donnees.Code} existe déjà.");

        var projet = new Projet
        {
            Code = donnees.Code.Trim(),
            Intitule = donnees.Intitule.Trim(),
            Localisation = donnees.Localisation,
            NatureTravaux = donnees.NatureTravaux,
            MontantInitial = donnees.MontantInitial,
            DateNotification = donnees.DateNotification,
            DateCommencement = donnees.DateCommencement,
            DelaiContractuelJours = donnees.DelaiContractuelJours,
            Statut = donnees.Statut
            // MontantActualise et DelaiActualiseJours restent à 0 : ils sont
            // initialisés juste après par IAvenantService, seul service habilité.
        };

        _db.Projets.Add(projet);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projet.Id, "Creation", null,
            new
            {
                projet.Id,
                projet.Code,
                projet.Intitule,
                projet.MontantInitial,
                projet.DelaiContractuelJours,
                projet.Statut
            },
            ct);

        await _avenants.InitialiserProjetAsync(projet.Id, utilisateurId, ct);

        return VersDetail(projet);
    }

    public async Task<ProjetDetail> ModifierAsync(
        ProjetModification donnees,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(donnees.Intitule))
            throw new ArgumentException("L'intitulé du projet est obligatoire.", nameof(donnees));

        var projet = await _db.Projets
            .FirstOrDefaultAsync(p => p.Id == donnees.Id, ct)
            ?? throw new InvalidOperationException($"Projet {donnees.Id} introuvable.");

        var avant = new
        {
            projet.Intitule,
            projet.Localisation,
            projet.NatureTravaux,
            projet.DateNotification,
            projet.DateCommencement
        };

        projet.Intitule = donnees.Intitule.Trim();
        projet.Localisation = donnees.Localisation;
        projet.NatureTravaux = donnees.NatureTravaux;
        projet.DateNotification = donnees.DateNotification;
        projet.DateCommencement = donnees.DateCommencement;

        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projet.Id, "Modification", avant,
            new
            {
                projet.Intitule,
                projet.Localisation,
                projet.NatureTravaux,
                projet.DateNotification,
                projet.DateCommencement
            },
            ct);

        // La date prévisionnelle et la cascade CPM dépendent de la date de
        // commencement : sans recalcul, le planning resterait périmé.
        if (avant.DateCommencement != projet.DateCommencement)
            await _recalcul.RecalculerCompletAsync(projet.Id, utilisateurId, ct: ct);

        return VersDetail(projet);
    }

    public async Task<ProjetDetail> ChangerStatutAsync(
        Guid projetId,
        StatutProjet statut,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .FirstOrDefaultAsync(p => p.Id == projetId, ct)
            ?? throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var avant = new { projet.Statut };
        projet.Statut = statut;

        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projet.Id, "ChangementStatut", avant,
            new { projet.Statut },
            ct);

        return VersDetail(projet);
    }

    public async Task SupprimerAsync(
        Guid projetId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .FirstOrDefaultAsync(p => p.Id == projetId, ct)
            ?? throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var avant = new
        {
            projet.Id,
            projet.Code,
            projet.Intitule,
            projet.MontantInitial,
            projet.MontantActualise,
            projet.DelaiContractuelJours,
            projet.Statut
        };

        var documents = await _db.Documents
            .Where(d => d.ProjetId == projetId)
            .ToListAsync(ct);
        var attachementIds = await _db.Attachements
            .Where(a => a.ProjetId == projetId)
            .Select(a => a.Id)
            .ToListAsync(ct);
        var marcheId = await _db.Marches
            .Where(m => m.ProjetId == projetId)
            .Select(m => m.Id)
            .FirstOrDefaultAsync(ct);
        var prixIds = marcheId == Guid.Empty
            ? new List<Guid>()
            : await _db.PrixUnitaires
                .Where(p => p.MarcheId == marcheId)
                .Select(p => p.Id)
                .ToListAsync(ct);
        var taches = await _db.Taches
            .Where(t => t.ProjetId == projetId)
            .ToListAsync(ct);
        var tacheIds = taches.Select(t => t.Id).ToList();

        // Toutes les suppressions sont atomiques : une contrainte violation
        // en cours de route ne laisse jamais un projet à moitié effacé.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        // 1. Lignes d'attachement : elles dépendent à la fois de l'attachement
        //    et du prix unitaire (DeleteBehavior.Restrict sur les deux).
        var lignes = await _db.LignesAttachement
            .Where(l => attachementIds.Contains(l.AttachementId)
                     || prixIds.Contains(l.PrixUnitaireId))
            .ToListAsync(ct);
        _db.LignesAttachement.RemoveRange(lignes);
        await _db.SaveChangesAsync(ct);

        // 2. Avenants et prix unitaires avant le marché. Les avenants sont
        //    retirés avant les documents car ils peuvent référencer un
        //    justificatif GED.
        var avenants = marcheId == Guid.Empty
            ? new List<Avenant>()
            : await _db.Avenants.Where(a => a.MarcheId == marcheId).ToListAsync(ct);
        var prixUnitaires = marcheId == Guid.Empty
            ? new List<PrixUnitaire>()
            : await _db.PrixUnitaires.Where(p => p.MarcheId == marcheId).ToListAsync(ct);
        _db.Avenants.RemoveRange(avenants);
        _db.PrixUnitaires.RemoveRange(prixUnitaires);
        await _db.SaveChangesAsync(ct);

        // 3. Planning : les liaisons et les parents sont des auto-références
        //    en Restrict, donc on les détache avant de supprimer les tâches.
        var liaisons = await _db.LiaisonsTache
            .Where(l => tacheIds.Contains(l.TachePredecesseurId)
                     || tacheIds.Contains(l.TacheSuccesseurId))
            .ToListAsync(ct);
        var tachesAyantUnParent = await _db.Taches
            .Where(t => t.ParentTacheId.HasValue && tacheIds.Contains(t.ParentTacheId.Value))
            .ToListAsync(ct);
        foreach (var tache in tachesAyantUnParent)
            tache.ParentTacheId = null;
        await _db.SaveChangesAsync(ct);
        _db.LiaisonsTache.RemoveRange(liaisons);
        await _db.SaveChangesAsync(ct);
        _db.Taches.RemoveRange(taches);
        await _db.SaveChangesAsync(ct);

        // 4. Situations et attachements, puis les documents GED.
        var situations = await _db.Situations
            .Where(s => s.ProjetId == projetId)
            .ToListAsync(ct);
        var attachements = await _db.Attachements
            .Where(a => a.ProjetId == projetId)
            .ToListAsync(ct);
        _db.Situations.RemoveRange(situations);
        _db.Attachements.RemoveRange(attachements);
        await _db.SaveChangesAsync(ct);

        _db.Documents.RemoveRange(documents);
        await _db.SaveChangesAsync(ct);

        // 5. Données directement rattachées au projet, puis le projet lui-même.
        var reserves = await _db.Reserves.Where(r => r.ProjetId == projetId).ToListAsync(ct);
        var courriers = await _db.Courriers.Where(c => c.ProjetId == projetId).ToListAsync(ct);
        var alertes = await _db.Alertes.Where(a => a.ProjetId == projetId).ToListAsync(ct);
        var marche = marcheId == Guid.Empty
            ? null
            : await _db.Marches.FirstOrDefaultAsync(m => m.Id == marcheId, ct);
        _db.Reserves.RemoveRange(reserves);
        _db.Courriers.RemoveRange(courriers);
        _db.Alertes.RemoveRange(alertes);
        if (marche is not null)
            _db.Marches.Remove(marche);
        await _db.SaveChangesAsync(ct);

        _db.Projets.Remove(projet);
        await _db.SaveChangesAsync(ct);

        // Le journal n'a pas de clé étrangère vers Projet : la trace survit
        // à la suppression et reste consultable dans les sauvegardes.
        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projetId, "SuppressionProjet", avant, null, ct);
        await transaction.CommitAsync(ct);

        SupprimerFichiersGed(projet.Code, documents);
    }

    /// <summary>
    /// Retire les fichiers physiques indexés par la GED pour ce projet. Les
    /// chemins sont vérifiés : aucun fichier hors du dossier du projet n'est
    /// touché, et les erreurs d'E/S (fichier ouvert...) ne bloquent pas la
    /// suppression en base, déjà engagée.
    /// </summary>
    private static void SupprimerFichiersGed(string codeProjet, IEnumerable<DocumentGed> documents)
    {
        var racine = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartlog", "Documents", NettoyerNom(codeProjet)));

        foreach (var document in documents)
        {
            if (string.IsNullOrWhiteSpace(document.CheminFichier))
                continue;

            try
            {
                var chemin = Path.GetFullPath(document.CheminFichier);
                if (!chemin.StartsWith(racine + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (File.Exists(chemin))
                    File.Delete(chemin);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        try
        {
            if (!Directory.Exists(racine))
                return;

            foreach (var dossier in Directory
                         .GetDirectories(racine, "*", SearchOption.AllDirectories)
                         .OrderByDescending(d => d.Length))
            {
                if (!Directory.EnumerateFileSystemEntries(dossier).Any())
                    Directory.Delete(dossier);
            }

            if (!Directory.EnumerateFileSystemEntries(racine).Any())
                Directory.Delete(racine);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string NettoyerNom(string nom)
    {
        var nettoye = nom;
        foreach (var caractere in Path.GetInvalidFileNameChars())
            nettoye = nettoye.Replace(caractere, '_');
        return string.IsNullOrWhiteSpace(nettoye) ? "sans-nom" : nettoye.Trim();
    }

    private static ProjetDetail VersDetail(Projet p) => new(
        p.Id,
        p.Code,
        p.Intitule,
        p.Localisation,
        p.NatureTravaux,
        p.MaitreOuvrage?.RaisonSociale,
        p.MaitreOeuvre?.RaisonSociale,
        p.Entreprise?.RaisonSociale,
        p.MontantInitial,
        p.MontantActualise,
        p.DateNotification,
        p.DateCommencement,
        p.DelaiContractuelJours,
        p.DelaiActualiseJours,
        p.DatePrevisionnelleAchevement,
        p.DateReelleAchevement,
        p.Statut,
        LibellerStatut(p.Statut));

    private static string LibellerStatut(StatutProjet statut) => statut switch
    {
        StatutProjet.EnCours => "En cours",
        StatutProjet.Termine => "Terminé",
        StatutProjet.Suspendu => "Suspendu",
        _ => statut.ToString()
    };
}
