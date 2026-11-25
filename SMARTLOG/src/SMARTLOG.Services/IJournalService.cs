namespace Smartlog.Services;

/// <summary>
/// Point d'entrée unique de la traçabilité (§6 et §12 du cahier des charges :
/// "historique des opérations... qui a modifié quoi et à quelle date").
/// Tout service métier qui modifie une entité doit passer par ici.
/// </summary>
public interface IJournalService
{
    Task EnregistrerAsync(
        Guid utilisateurId,
        string entiteType,
        Guid entiteId,
        string action,
        object? valeurAvant,
        object? valeurApres,
        CancellationToken ct = default);

    /// <summary>
    /// Lit l'historique d'une entité, du plus récent au plus ancien.
    /// Utilisé notamment par l'onglet « Historique » de la fiche projet.
    /// </summary>
    Task<IReadOnlyList<EntreeJournal>> ListerAsync(
        string entiteType,
        Guid entiteId,
        CancellationToken ct = default);
}

public sealed record EntreeJournal(
    Guid Id,
    DateTime DateAction,
    string ActionLibelle,
    string? ValeurAvantJson,
    string? ValeurApresJson,
    string Utilisateur);
