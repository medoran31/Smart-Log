using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services;

/// <summary>
/// Cohérence des recalculs croisés : avenant → finances → planning → alertes.
/// L'ordre est important : les situations dépendent du montant actualisé, le
/// planning de la date prévisionnelle, les alertes des deux.
/// </summary>
public class RecalculCroiseService : IRecalculCroiseService
{
    private readonly SmartlogDbContext _db;
    private readonly IAvenantService _avenants;
    private readonly ISituationService _situations;
    private readonly IPlanificationService _planification;
    private readonly IAlerteService _alertes;
    private readonly IJournalService _journal;

    public RecalculCroiseService(
        SmartlogDbContext db,
        IAvenantService avenants,
        ISituationService situations,
        IPlanificationService planification,
        IAlerteService alertes,
        IJournalService journal)
    {
        _db = db;
        _avenants = avenants;
        _situations = situations;
        _planification = planification;
        _alertes = alertes;
        _journal = journal;
    }

    public async Task RecalculerCompletAsync(
        Guid projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default)
    {
        await _avenants.RecalculerValeursDeriveesAsync(projetId, utilisateurId, ct);
        await RecalculerDepuisProjetAsync(projetId, utilisateurId, horizonJours, ct);
    }

    public async Task RecalculerDepuisProjetAsync(
        Guid projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default)
    {
        var situations = await _db.Situations
            .AsNoTracking()
            .Where(s => s.ProjetId == projetId)
            .OrderBy(s => s.Periode)
            .ToListAsync(ct);

        foreach (var situation in situations)
        {
            // Les taux saisis par l'utilisateur ne sont pas stockés sur la
            // situation : on préserve les rapports déjà appliqués plutôt que
            // d'imposer des valeurs par défaut differentes de son historique.
            var tauxRetenue = Taux(situation.RetenueGarantie, situation.MontantCumule);
            var tauxAvance = Taux(situation.Avance, situation.MontantCumule);
            await _situations.RecalculerAsync(
                situation.Id, tauxRetenue, tauxAvance, situation.Penalites,
                utilisateurId, ct);
        }

        var planning = await _planification.RecalculerAsync(projetId, utilisateurId, ct);
        var alertes = await ActualiserAlertesAsync(projetId, utilisateurId, horizonJours, ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(Projet), projetId, "RecalculCroise", null,
            new
            {
                NombreSituationsRecalculees = situations.Count,
                planning.NombreTaches,
                planning.NombreTachesCritiques,
                alertes.NombreNonLues,
                alertes.NombreEnRetard
            },
            ct);
    }

    public Task<CentreNotifications> ActualiserAlertesAsync(
        Guid? projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default) =>
        _alertes.AnalyserAsync(projetId, utilisateurId, horizonJours, ct);

    private static decimal Taux(decimal montant, decimal cumule) =>
        cumule <= 0m ? 0m : Math.Min(1m, Math.Max(0m, Math.Round(montant / cumule, 4)));
}
