using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Service dédié aux situations de travaux. C'est le seul endroit de
/// l'application qui calcule et écrit les montants dérivés d'une situation
/// (cumul, retenue de garantie, avance, pénalités).
/// </summary>
public interface ISituationService
{
    /// <summary>
    /// Calcule les montants dérivés d'une situation à partir du montant
    /// actualisé du projet et d'un taux d'avancement. Opération journalisée
    /// et idempotente.
    /// </summary>
    Task InitialiserAsync(
        Guid situationId,
        decimal tauxAvancement,
        decimal tauxRetenueGarantie,
        Guid utilisateurId,
        decimal tauxAvance = 0.10m,
        CancellationToken ct = default);

    /// <summary>
    /// Crée (ou réutilise) la situation d'une période et regroupe les
    /// attachements validés de cette période. Le cumul inclut tous les
    /// attachements validés jusqu'à la fin de la période.
    /// </summary>
    Task<SituationResume> CreerOuRecalculerAsync(
        Guid projetId,
        string periode,
        TypeSituation type,
        decimal tauxRetenueGarantie,
        decimal tauxAvance,
        decimal penalites,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>Recalcule une situation existante depuis les attachements validés.</summary>
    Task<SituationResume> RecalculerAsync(
        Guid situationId,
        decimal tauxRetenueGarantie,
        decimal tauxAvance,
        decimal penalites,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<SituationResume> ChangerStatutAsync(
        Guid situationId,
        StatutSituation statut,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<SituationResume?> ObtenirAsync(Guid situationId, CancellationToken ct = default);

    /// <summary>Situations et points d'évolution engagé / payé / restant dû.</summary>
    Task<SuiviFinancier> ObtenirSuiviAsync(Guid projetId, CancellationToken ct = default);
}

public sealed record SituationResume(
    Guid Id,
    Guid ProjetId,
    string Periode,
    string TypeLibelle,
    string Statut,
    StatutSituation StatutValeur,
    decimal MontantPeriode,
    decimal MontantCumule,
    decimal RetenueGarantie,
    decimal Avance,
    decimal Penalites,
    decimal NetAPayer);

public sealed record PointEvolution(
    string Periode,
    decimal MontantEngage,
    decimal MontantPaye,
    decimal RestantDu);

public sealed record SuiviFinancier(
    Guid ProjetId,
    decimal MontantContractuel,
    IReadOnlyList<SituationResume> Situations,
    IReadOnlyList<PointEvolution> Evolution,
    decimal TotalEngage,
    decimal TotalPaye,
    decimal TotalRestantDu)
{
    /// <summary>Avancement financier : engagé / montant actualisé du projet.</summary>
    public decimal TauxAvancement => MontantContractuel <= 0m
        ? 0m
        : Math.Round(TotalEngage / MontantContractuel, 4);
}
