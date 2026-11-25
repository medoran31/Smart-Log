using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Moteur de règles du centre de notifications. L'analyse est idempotente :
/// une même source (projet, tâche, réserve ou courrier) produit au plus une
/// alerte, mise à jour lors des analyses suivantes.
/// </summary>
public interface IAlerteService
{
    /// <summary>Analyze les échéances et écrit les alertes dans le centre.</summary>
    Task<CentreNotifications> AnalyserAsync(
        Guid? projetId = null,
        Guid utilisateurId = default,
        int horizonJours = 30,
        CancellationToken ct = default);

    Task<CentreNotifications> ObtenirCentreAsync(
        Guid? projetId = null, bool inclureLues = true, CancellationToken ct = default);

    Task MarquerLueAsync(Guid alerteId, Guid utilisateurId, CancellationToken ct = default);

    Task MarquerToutesLuesAsync(
        Guid? projetId, Guid utilisateurId, CancellationToken ct = default);
}

/// <summary>Une alerte usable directement par le centre de notifications.</summary>
public sealed record AlerteResume(
    Guid Id,
    Guid ProjetId,
    string ProjetCode,
    string ProjetIntitule,
    string SourceType,
    string SourceTypeLibelle,
    Guid SourceId,
    string Titre,
    string Message,
    string MenuCible,
    DateOnly DateEcheance,
    int JoursRestants,
    bool EstEnRetard,
    PrioriteAlerte Priorite,
    string PrioriteLibelle,
    bool EstLue);

public sealed record CentreNotifications(
    IReadOnlyList<AlerteResume> Alertes,
    int NombreNonLues,
    int NombreEnRetard)
{
    public IReadOnlyList<AlerteResume> DernieresNonLues => Alertes
        .Where(a => !a.EstLue)
        .OrderByDescending(a => a.Priorite)
        .ThenBy(a => a.DateEcheance)
        .Take(5)
        .ToList();
}
