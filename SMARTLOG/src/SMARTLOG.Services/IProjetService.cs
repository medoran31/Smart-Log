using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Service dédié aux projets : lecture, création, modification,
/// changement de statut et suppression. Chaque écriture est journalisée via
/// IJournalService.
/// Les valeurs dérivées ne sont jamais écrites ici : la création délègue
/// leur initialisation à IAvenantService, seul point de calcul du montant
/// et du délai actualisés.
/// </summary>
public interface IProjetService
{
    Task<IReadOnlyList<ProjetResume>> ListerAsync(CancellationToken ct = default);

    Task<ProjetDetail?> ObtenirAsync(Guid projetId, CancellationToken ct = default);

    Task<ProjetDetail> CreerAsync(ProjetCreation donnees, Guid utilisateurId, CancellationToken ct = default);

    Task<ProjetDetail> ModifierAsync(ProjetModification donnees, Guid utilisateurId, CancellationToken ct = default);

    Task<ProjetDetail> ChangerStatutAsync(
        Guid projetId, StatutProjet statut, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>
    /// Supprime définitivement un projet et toutes ses données liées dans une
    /// seule transaction (marché, avenants, prix, attachements, situations,
    /// planning, réserves, documents, courriers et alertes). Les fichiers GED
    /// archivés pour ce projet sont également retirés du disque. L'action
    /// reste tracée dans le journal avec l'état du projet avant suppression.
    /// </summary>
    Task SupprimerAsync(Guid projetId, Guid utilisateurId, CancellationToken ct = default);
}

public sealed record ProjetResume(
    Guid Id,
    string Code,
    string Intitule,
    string? Localisation,
    decimal MontantInitial,
    decimal MontantActualise,
    int DelaiContractuelJours,
    int DelaiActualiseJours,
    string Statut);

public sealed record ProjetDetail(
    Guid Id,
    string Code,
    string Intitule,
    string? Localisation,
    string? NatureTravaux,
    string? MaitreOuvrage,
    string? MaitreOeuvre,
    string? Entreprise,
    decimal MontantInitial,
    decimal MontantActualise,
    DateOnly? DateNotification,
    DateOnly? DateCommencement,
    int DelaiContractuelJours,
    int DelaiActualiseJours,
    DateOnly? DatePrevisionnelleAchevement,
    DateOnly? DateReelleAchevement,
    StatutProjet Statut,
    string StatutLibelle);

public sealed record ProjetCreation(
    string Code,
    string Intitule,
    string? Localisation,
    string? NatureTravaux,
    decimal MontantInitial,
    DateOnly? DateNotification,
    DateOnly? DateCommencement,
    int DelaiContractuelJours,
    StatutProjet Statut);

public sealed record ProjetModification(
    Guid Id,
    string Intitule,
    string? Localisation,
    string? NatureTravaux,
    DateOnly? DateNotification,
    DateOnly? DateCommencement);
