using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Implémente la règle centrale du §6 du cahier des charges :
/// "Lorsqu'un avenant est ajouté, recalculer le montant actualisé et
/// le délai contractuel." Le montant/délai du Projet n'est JAMAIS
/// modifié ailleurs que dans ce service.
/// </summary>
public class AvenantService : IAvenantService
{
    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;

    public AvenantService(SmartlogDbContext db, IJournalService journal)
    {
        _db = db;
        _journal = journal;
    }

    public async Task InitialiserProjetAsync(Guid projetId, Guid utilisateurId, CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .FirstOrDefaultAsync(p => p.Id == projetId, ct)
            ?? throw new InvalidOperationException($"Projet {projetId} introuvable.");

        // Idempotent : on n'écrase jamais des valeurs déjà calculées (par
        // exemple après la validation d'un avenant).
        if (projet.MontantActualise != 0 || projet.DelaiActualiseJours != 0)
            return;

        var avant = new
        {
            projet.MontantActualise,
            projet.DelaiActualiseJours,
            projet.DatePrevisionnelleAchevement
        };

        await RecalculerProjetAsync(projet, "InitialisationValeursDerivees", avant, utilisateurId, ct);
    }

    public async Task RecalculerValeursDeriveesAsync(
        Guid projetId, Guid utilisateurId, CancellationToken ct = default)
    {
        var projet = await _db.Projets
            .FirstOrDefaultAsync(p => p.Id == projetId, ct)
            ?? throw new InvalidOperationException($"Projet {projetId} introuvable.");

        var avant = new
        {
            projet.MontantActualise,
            projet.DelaiActualiseJours,
            projet.DatePrevisionnelleAchevement
        };

        await RecalculerProjetAsync(projet, "RecalculValeursDerivees", avant, utilisateurId, ct);
    }

    public async Task<IReadOnlyList<AvenantResume>> ListerAsync(Guid marcheId, CancellationToken ct = default)
    {
        var avenants = await _db.Avenants
            .AsNoTracking()
            .Where(a => a.MarcheId == marcheId)
            .OrderBy(a => a.Numero)
            .ToListAsync(ct);

        return avenants.Select(VersResume).ToList();
    }

    public async Task<SimulationAvenant> SimulerAsync(
        Guid marcheId,
        decimal montantDelta,
        int delaiDeltaJours,
        CancellationToken ct = default)
    {
        var projet = await ObtenirProjetAsync(marcheId, ct);

        var nouveauDelai = projet.DelaiActualiseJours + delaiDeltaJours;
        return new SimulationAvenant(
            projet.MontantActualise,
            Math.Round(projet.MontantActualise + montantDelta, 2),
            projet.DelaiActualiseJours,
            nouveauDelai,
            CalculerDateFin(projet.DateCommencement, projet.DelaiActualiseJours),
            CalculerDateFin(projet.DateCommencement, nouveauDelai));
    }

    public async Task<AvenantResume> CreerAsync(
        Guid marcheId,
        AvenantCreation donnees,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var marche = await _db.Marches
            .FirstOrDefaultAsync(m => m.Id == marcheId, ct)
            ?? throw new InvalidOperationException($"Marché {marcheId} introuvable.");

        int dernierNumero;
        if (donnees.Numero > 0)
        {
            dernierNumero = donnees.Numero - 1;
        }
        else
        {
            var numeros = await _db.Avenants
                .AsNoTracking()
                .Where(a => a.MarcheId == marcheId)
                .Select(a => a.Numero)
                .ToListAsync(ct);
            dernierNumero = numeros.Count == 0 ? 0 : numeros.Max();
        }

        var numero = dernierNumero + 1;

        if (await _db.Avenants.AnyAsync(a => a.MarcheId == marcheId && a.Numero == numero, ct))
            throw new InvalidOperationException($"L'avenant n°{numero} existe déjà pour ce marché.");

        var avenant = new Avenant
        {
            MarcheId = marche.Id,
            Numero = numero,
            Type = donnees.Type,
            MontantDelta = Math.Round(donnees.MontantDelta, 2),
            DelaiDeltaJours = donnees.DelaiDeltaJours,
            Statut = StatutAvenant.Brouillon
        };

        _db.Avenants.Add(avenant);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Avenant), avenant.Id, "Creation", null,
            new
            {
                avenant.Id,
                avenant.MarcheId,
                avenant.Numero,
                avenant.Type,
                avenant.MontantDelta,
                avenant.DelaiDeltaJours,
                avenant.Statut
            },
            ct);

        return VersResume(avenant);
    }

    public async Task<Projet> ValiderAsync(Guid avenantId, Guid utilisateurId, CancellationToken ct = default)
    {
        var avenant = await _db.Avenants
            .Include(a => a.Marche)
            .ThenInclude(m => m!.Projet)
            .FirstOrDefaultAsync(a => a.Id == avenantId, ct)
            ?? throw new InvalidOperationException($"Avenant {avenantId} introuvable.");

        if (avenant.Statut != StatutAvenant.Brouillon)
            throw new InvalidOperationException("Seul un avenant en brouillon peut être validé.");

        var projet = avenant.Marche!.Projet!;
        var avant = new { projet.MontantActualise, projet.DelaiActualiseJours };

        avenant.Statut = StatutAvenant.Valide;
        avenant.DateValidation = DateOnly.FromDateTime(DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        await RecalculerProjetAsync(projet, "RecalculAvenant", avant, utilisateurId, ct);
        return projet;
    }

    public async Task<Projet> AnnulerAsync(Guid avenantId, Guid utilisateurId, CancellationToken ct = default)
    {
        var avenant = await _db.Avenants
            .Include(a => a.Marche)
            .ThenInclude(m => m!.Projet)
            .FirstOrDefaultAsync(a => a.Id == avenantId, ct)
            ?? throw new InvalidOperationException($"Avenant {avenantId} introuvable.");

        if (avenant.Statut != StatutAvenant.Valide)
            throw new InvalidOperationException("Seul un avenant validé peut être annulé.");

        var projet = avenant.Marche!.Projet!;
        var avant = new { projet.MontantActualise, projet.DelaiActualiseJours };

        avenant.Statut = StatutAvenant.Annule;
        await _db.SaveChangesAsync(ct);

        await RecalculerProjetAsync(projet, "AnnulationAvenant", avant, utilisateurId, ct);
        return projet;
    }

    /// <summary>
    /// Unique point d'écriture des valeurs dérivées du projet. Le montant et
    /// le délai sont reconstruits depuis les valeurs contractuelles et la
    /// totalité des avenants validés : valider puis annuler un avenant ramène
    /// donc exactement aux valeurs initiales, même en présence d'autres
    /// avenants validés.
    /// </summary>
    private async Task RecalculerProjetAsync(
        Projet projet,
        string action,
        object? valeurAvant,
        Guid utilisateurId,
        CancellationToken ct)
    {
        var deltas = await _db.Avenants
            .AsNoTracking()
            .Where(a => a.Marche!.ProjetId == projet.Id && a.Statut == StatutAvenant.Valide)
            .Select(a => new { a.MontantDelta, a.DelaiDeltaJours })
            .ToListAsync(ct);

        projet.MontantActualise = Math.Round(
            projet.MontantInitial + deltas.Sum(d => d.MontantDelta), 2);
        projet.DelaiActualiseJours =
            projet.DelaiContractuelJours + deltas.Sum(d => d.DelaiDeltaJours);
        projet.DatePrevisionnelleAchevement =
            CalculerDateFin(projet.DateCommencement, projet.DelaiActualiseJours);

        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projet.Id, action, valeurAvant,
            new
            {
                projet.MontantActualise,
                projet.DelaiActualiseJours,
                projet.DatePrevisionnelleAchevement
            },
            ct);
    }

    private async Task<Projet> ObtenirProjetAsync(Guid marcheId, CancellationToken ct)
    {
        var marche = await _db.Marches
            .AsNoTracking()
            .Include(m => m.Projet)
            .FirstOrDefaultAsync(m => m.Id == marcheId, ct)
            ?? throw new InvalidOperationException($"Marché {marcheId} introuvable.");

        return marche.Projet
            ?? throw new InvalidOperationException($"Le marché {marcheId} n'a pas de projet associé.");
    }

    private static DateOnly? CalculerDateFin(DateOnly? debut, int delaiJours) =>
        debut?.AddDays(delaiJours);

    private static AvenantResume VersResume(Avenant a) => new(
        a.Id,
        a.MarcheId,
        a.Numero,
        a.Type,
        LibellerType(a.Type),
        a.MontantDelta,
        a.DelaiDeltaJours,
        a.Statut,
        LibellerStatut(a.Statut),
        a.DateValidation);

    public static string LibellerType(TypeAvenant type) => type switch
    {
        TypeAvenant.Delai => "Délai",
        TypeAvenant.Financier => "Financier",
        TypeAvenant.Mixte => "Mixte",
        _ => type.ToString()
    };

    public static string LibellerStatut(StatutAvenant statut) => statut switch
    {
        StatutAvenant.Brouillon => "Brouillon",
        StatutAvenant.Valide => "Validé",
        StatutAvenant.Annule => "Annulé",
        _ => statut.ToString()
    };
}
