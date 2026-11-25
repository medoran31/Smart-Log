using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services.Ged;

namespace Smartlog.Services;

/// <summary>
/// Service métier des réserves : numérotation, cycle de vie, échéances
/// dérivées et génération du PV de levée. Toutes les écritures sont
/// journalisées ; les calculs ne sont jamais effectués dans l'interface.
/// </summary>
public class ReserveService : IReserveService
{
    private static readonly StatutReserve[] Cycle =
    {
        StatutReserve.Nouvelle,
        StatutReserve.EnCours,
        StatutReserve.EnVerification,
        StatutReserve.Levee,
        StatutReserve.Cloturee
    };

    private const string ModelePvLevee =
        "PROCÈS-VERBAL DE LEVÉE DE RÉSERVE\n\n" +
        "Projet : {{Projet.Code}} — {{Projet.Intitule}}\n" +
        "Maître d'ouvrage : {{Projet.MaitreOuvrage}}\n" +
        "Maître d'œuvre : {{Projet.MaitreOeuvre}}\n" +
        "Entreprise : {{Projet.Entreprise}}\n\n" +
        "Réserve n° {{Reserve.Numero}}\n" +
        "Description : {{Reserve.Description}}\n" +
        "Priorité : {{Reserve.Priorite}}\n" +
        "Date de constatation : {{Reserve.DateConstatation}}\n" +
        "Délai de levée : {{Reserve.DelaiLeveeJours}} jour(s)\n" +
        "Date d'échéance : {{Reserve.DateEcheanceLevee}}\n" +
        "Date de levée : {{Reserve.DateLevee}}\n" +
        "Observation : {{Reserve.Observation}}\n\n" +
        "Le présent procès-verbal constate la levée de la réserve après " +
        "vérification de la réalisation des travaux correctifs. " +
        "Document généré le {{DateGeneration}}.";

    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IModeleDocumentService _modeles;
    private readonly IGedService _ged;

    public ReserveService(
        SmartlogDbContext db,
        IJournalService journal,
        IModeleDocumentService modeles,
        IGedService ged)
    {
        _db = db;
        _journal = journal;
        _modeles = modeles;
        _ged = ged;
    }

    public async Task<ReserveResume> CreerAsync(
        Guid projetId,
        string description,
        PrioriteReserve priorite,
        Guid? entrepriseResponsableId,
        DateOnly dateConstatation,
        int delaiLeveeJours,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        ValiderSaisie(description, dateConstatation, delaiLeveeJours);

        if (!await _db.Projets.AnyAsync(p => p.Id == projetId, ct))
            throw new InvalidOperationException($"Projet {projetId} introuvable.");
        if (entrepriseResponsableId.HasValue &&
            !await _db.Intervenants.AnyAsync(i => i.Id == entrepriseResponsableId.Value, ct))
            throw new InvalidOperationException("L'entreprise responsable est introuvable.");

        var numero = await _db.Reserves
            .Where(r => r.ProjetId == projetId)
            .Select(r => (int?)r.Numero)
            .MaxAsync(ct) ?? 0;

        var reserve = new Reserve
        {
            ProjetId = projetId,
            Numero = numero + 1,
            Description = description.Trim(),
            Priorite = priorite,
            EntrepriseResponsableId = entrepriseResponsableId,
            DateConstatation = dateConstatation,
            DelaiLeveeJours = delaiLeveeJours,
            Statut = StatutReserve.Nouvelle
        };
        _db.Reserves.Add(reserve);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Reserve), reserve.Id, "Creation", null,
            new
            {
                reserve.Id,
                reserve.ProjetId,
                reserve.Numero,
                reserve.Description,
                reserve.Priorite,
                reserve.DateConstatation,
                reserve.DelaiLeveeJours
            },
            ct);

        return await ConstruireResumeAsync(reserve, ct);
    }

    public async Task<ReserveResume> ModifierAsync(
        Guid reserveId,
        string description,
        PrioriteReserve priorite,
        Guid? entrepriseResponsableId,
        DateOnly dateConstatation,
        int delaiLeveeJours,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        ValiderSaisie(description, dateConstatation, delaiLeveeJours);

        var reserve = await _db.Reserves.FirstOrDefaultAsync(r => r.Id == reserveId, ct)
            ?? throw new InvalidOperationException($"Réserve {reserveId} introuvable.");
        if (reserve.Statut is StatutReserve.Levee or StatutReserve.Cloturee)
            throw new InvalidOperationException("Une réserve levée ou clôturée ne peut plus être modifiée.");

        var avant = new
        {
            reserve.Description,
            reserve.Priorite,
            reserve.EntrepriseResponsableId,
            reserve.DateConstatation,
            reserve.DelaiLeveeJours
        };
        reserve.Description = description.Trim();
        reserve.Priorite = priorite;
        reserve.EntrepriseResponsableId = entrepriseResponsableId;
        reserve.DateConstatation = dateConstatation;
        reserve.DelaiLeveeJours = delaiLeveeJours;
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Reserve), reserve.Id, "Modification", avant,
            new
            {
                reserve.Description,
                reserve.Priorite,
                reserve.EntrepriseResponsableId,
                reserve.DateConstatation,
                reserve.DelaiLeveeJours
            },
            ct);

        return await ConstruireResumeAsync(reserve, ct);
    }

    public async Task<ReserveResume> ChangerStatutAsync(
        Guid reserveId, StatutReserve nouveauStatut, Guid utilisateurId,
        CancellationToken ct = default)
    {
        var reserve = await _db.Reserves.FirstOrDefaultAsync(r => r.Id == reserveId, ct)
            ?? throw new InvalidOperationException($"Réserve {reserveId} introuvable.");
        if (!Enum.IsDefined(nouveauStatut))
            throw new ArgumentOutOfRangeException(nameof(nouveauStatut), "Statut de réserve inconnu.");
        if (nouveauStatut == reserve.Statut)
            return await ConstruireResumeAsync(reserve, ct);

        var positionActuelle = Array.IndexOf(Cycle, reserve.Statut);
        var positionCible = Array.IndexOf(Cycle, nouveauStatut);
        if (positionCible != positionActuelle + 1)
            throw new InvalidOperationException(
                $"Transition interdite : {LibellerStatut(reserve.Statut)} → {LibellerStatut(nouveauStatut)}. " +
                "Le cycle est Nouvelle → En cours → En vérification → Levée → Clôturée.");

        if (nouveauStatut == StatutReserve.Levee && reserve.DateLevee.HasValue)
            throw new InvalidOperationException("Cette réserve porte déjà une date de levée.");

        var statutAvant = reserve.Statut;
        var dateLeveeAvant = reserve.DateLevee;
        reserve.Statut = nouveauStatut;
        if (nouveauStatut == StatutReserve.Levee)
            reserve.DateLevee = DateOnly.FromDateTime(DateTime.Now);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Reserve), reserve.Id, "ChangementStatut",
            new { Statut = statutAvant, DateLevee = dateLeveeAvant },
            new { Statut = reserve.Statut, reserve.DateLevee },
            ct);

        if (nouveauStatut == StatutReserve.Levee)
            await GenererPvLeveeAsync(reserve, utilisateurId, ct);

        return await ConstruireResumeAsync(reserve, ct);
    }

    public async Task<ReserveResume?> ObtenirAsync(Guid reserveId, CancellationToken ct = default)
    {
        var reserve = await _db.Reserves.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reserveId, ct);
        return reserve is null ? null : await ConstruireResumeAsync(reserve, ct);
    }

    public async Task<IReadOnlyList<IntervenantResume>> ListerEntreprisesAsync(
        Guid projetId, CancellationToken ct = default)
    {
        var entrepriseId = await _db.Projets.AsNoTracking()
            .Where(p => p.Id == projetId)
            .Select(p => p.EntrepriseId)
            .FirstOrDefaultAsync(ct);
        if (!entrepriseId.HasValue)
            return Array.Empty<IntervenantResume>();

        return await _db.Intervenants.AsNoTracking()
            .Where(i => i.Id == entrepriseId.Value || i.Type == TypeIntervenant.SousTraitant)
            .OrderBy(i => i.RaisonSociale)
            .Select(i => new IntervenantResume(i.Id, i.RaisonSociale))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ReserveResume>> ListerAsync(
        Guid? projetId = null, bool inclureCloturees = true, CancellationToken ct = default)
    {
        var requete = _db.Reserves.AsNoTracking().AsQueryable();
        if (projetId.HasValue)
            requete = requete.Where(r => r.ProjetId == projetId.Value);
        if (!inclureCloturees)
            requete = requete.Where(r => r.Statut != StatutReserve.Cloturee);

        var reserves = await requete
            .OrderBy(r => r.ProjetId)
            .ThenBy(r => r.Numero)
            .ToListAsync(ct);

        var resultats = new List<ReserveResume>(reserves.Count);
        foreach (var reserve in reserves)
            resultats.Add(await ConstruireResumeAsync(reserve, ct));
        return resultats;
    }

    private async Task GenererPvLeveeAsync(
        Reserve reserve, Guid utilisateurId, CancellationToken ct)
    {
        var valeurs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Reserve.Numero"] = reserve.Numero.ToString(),
            ["Reserve.Description"] = reserve.Description,
            ["Reserve.Priorite"] = LibellerPriorite(reserve.Priorite),
            ["Reserve.DateConstatation"] = reserve.DateConstatation.ToString("d", CultureInfoFr),
            ["Reserve.DelaiLeveeJours"] = reserve.DelaiLeveeJours.ToString(),
            ["Reserve.DateEcheanceLevee"] = reserve.DateConstatation
                .AddDays(reserve.DelaiLeveeJours).ToString("d", CultureInfoFr),
            ["Reserve.DateLevee"] = reserve.DateLevee?.ToString("d", CultureInfoFr) ?? "—",
            ["Reserve.Observation"] = "Réserve levée après vérification des travaux correctifs."
        };

        var document = await _modeles.GenererAsync(
            new ModeleDocumentDemande(
                reserve.ProjetId,
                ModelePvLevee,
                $"PV de levée - Réserve n°{reserve.Numero}",
                FormatDocument.Word,
                utilisateurId,
                valeurs,
                nameof(Reserve),
                reserve.Id),
            ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Reserve), reserve.Id, "GenerationPvLevee",
            null,
            new
            {
                reserve.Id,
                reserve.Numero,
                document.NomFichier,
                document.CheminFichier,
                JetonsNonResolus = string.Join(", ", document.JetonsNonResolus)
            },
            ct);
    }

    private static readonly System.Globalization.CultureInfo CultureInfoFr =
        System.Globalization.CultureInfo.GetCultureInfo("fr-FR");

    private async Task<ReserveResume> ConstruireResumeAsync(
        Reserve reserve, CancellationToken ct)
    {
        var entreprise = reserve.EntrepriseResponsableId.HasValue
            ? await _db.Intervenants.AsNoTracking()
                .Where(i => i.Id == reserve.EntrepriseResponsableId.Value)
                .Select(i => i.RaisonSociale)
                .FirstOrDefaultAsync(ct)
            : null;

        var cheminPv = await _ged.ListerAsync(reserve.ProjetId, ct);
        var pv = cheminPv.FirstOrDefault(d =>
            d.EntiteLieeType == nameof(Reserve) && d.EntiteLieeId == reserve.Id &&
            d.NomFichier.StartsWith("PV de levée", StringComparison.OrdinalIgnoreCase));

        var echeance = reserve.DateConstatation.AddDays(reserve.DelaiLeveeJours);
        var restants = echeance.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        var estEnRetard = restants < 0 && reserve.Statut is not (StatutReserve.Levee or StatutReserve.Cloturee);

        return new ReserveResume(
            reserve.Id,
            reserve.ProjetId,
            reserve.Numero,
            $"Réserve n°{reserve.Numero}",
            reserve.Description,
            reserve.Priorite,
            LibellerPriorite(reserve.Priorite),
            reserve.EntrepriseResponsableId,
            entreprise ?? "Non renseignée",
            reserve.DateConstatation,
            reserve.DelaiLeveeJours,
            echeance,
            restants,
            estEnRetard,
            reserve.Statut,
            LibellerStatut(reserve.Statut),
            reserve.DateLevee,
            pv?.CheminFichier);
    }

    private static void ValiderSaisie(
        string description, DateOnly dateConstatation, int delaiLeveeJours)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La description de la réserve est obligatoire.", nameof(description));
        if (delaiLeveeJours <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(delaiLeveeJours), "Le délai de levée doit être positif.");
        if (dateConstatation == DateOnly.MinValue)
            throw new ArgumentException("La date de constatation est obligatoire.", nameof(dateConstatation));
    }

    private static string LibellerPriorite(PrioriteReserve priorite) => priorite switch
    {
        PrioriteReserve.Basse => "Basse",
        PrioriteReserve.Moyenne => "Moyenne",
        PrioriteReserve.Haute => "Haute",
        _ => priorite.ToString()
    };

    private static string LibellerStatut(StatutReserve statut) => statut switch
    {
        StatutReserve.Nouvelle => "Nouvelle",
        StatutReserve.EnCours => "En cours",
        StatutReserve.EnVerification => "En vérification",
        StatutReserve.Levee => "Levée",
        StatutReserve.Cloturee => "Clôturée",
        _ => statut.ToString()
    };
}
