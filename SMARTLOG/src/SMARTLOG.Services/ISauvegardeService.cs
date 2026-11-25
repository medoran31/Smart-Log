namespace Smartlog.Services;

/// <summary>
/// Sauvegarde et restauration du poste de travail : base SQLite et documents
/// de la GED. La clé Claude n'est volontairement pas incluse dans une copie.
/// </summary>
public interface ISauvegardeService
{
    string CheminBase { get; }
    string DossierSauvegardes { get; }

    Task<ResultatSauvegarde> CreerAsync(
        string? description = null, CancellationToken ct = default);

    Task<IReadOnlyList<SauvegardeResume>> ListerAsync(CancellationToken ct = default);

    /// <summary>
    /// Prépare une restauration : l'intégrité de la copie est vérifiée, puis
    /// l'application est programmée pour le prochain lancement (aucune base
    /// ouverte ne peut être remplacée pendant l'exécution).
    /// </summary>
    Task<ResultatRestauration> PreparerRestaurationAsync(
        string cheminSauvegarde, CancellationToken ct = default);

    Task<bool> SupprimerAsync(string cheminSauvegarde, CancellationToken ct = default);
}

public sealed record SauvegardeResume(
    string Nom,
    string Chemin,
    DateTime DateCreation,
    long TailleOctets,
    bool ContientDocuments,
    string Description);

public sealed record ResultatSauvegarde(
    bool Succes, string Message, SauvegardeResume? Sauvegarde = null);

public sealed record ResultatRestauration(
    bool Succes, string Message, string? CheminSauvegardeSecurite = null);
