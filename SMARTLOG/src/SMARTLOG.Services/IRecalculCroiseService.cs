namespace Smartlog.Services;

/// <summary>
/// Orchestre les recalculs qui traversent plusieurs modules. Chaque valeur
/// dérivée continue d'être calculée et écrite par son service propriétaire
/// (avenant, situation, planning, alertes) : ce service ne duplique aucun
/// calcul, il garantit que la chaîne complète est rafraîchie dans le bon ordre.
/// </summary>
public interface IRecalculCroiseService
{
    /// <summary>
    /// Recalcule les dépendances d'un projet dont les valeurs dérivées sont
    /// déjà à jour : situations financières, CPM du planning puis alertes.
    /// </summary>
    Task RecalculerDepuisProjetAsync(
        Guid projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default);

    /// <summary>
    /// Recalcule d'abord les valeurs dérivées du projet (montant, délai, date
    /// prévisionnelle), puis toutes leurs dépendances. Utilisé après une
    /// modification contractuelle du projet.
    /// </summary>
    Task RecalculerCompletAsync(
        Guid projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default);

    /// <summary>Rafraîchit le moteur d'alertes pour un projet ou pour tous.</summary>
    Task<CentreNotifications> ActualiserAlertesAsync(
        Guid? projetId,
        Guid utilisateurId,
        int horizonJours = 30,
        CancellationToken ct = default);
}
