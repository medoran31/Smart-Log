namespace Smartlog.Services;

/// <summary>
/// Orchestre l'insertion du jeu de démonstration du premier lancement,
/// la traçabilité de chaque création et l'application des règles métier
/// de calcul des valeurs dérivées.
/// </summary>
public interface IDonneesDemonstrationService
{
    Task InitialiserAsync(CancellationToken ct = default);
}
