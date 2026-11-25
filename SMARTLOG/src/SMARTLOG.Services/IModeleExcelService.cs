namespace Smartlog.Services;

/// <summary>Une colonne d'un modèle Excel à remplir.</summary>
public sealed record ColonneExcel(string Intitule, string Description, double Largeur = 22);

/// <summary>Un modèle de saisie livré avec l'application.</summary>
public sealed record ModeleExcel(
    string NomFichier,
    string Titre,
    string Categorie,
    IReadOnlyList<ColonneExcel> Colonnes,
    IReadOnlyList<string> Instructions);

public sealed record ModeleExcelFichier(string NomFichier, string Chemin, bool Cree);

/// <summary>
/// Fournit les modèles Excel prêts à remplir, classés par catégorie
/// métier. Les fichiers sont déposés dans un dossier utilisateur puis peuvent
/// être importés comme pièces dans la GED via « Importer des pièces ».
/// </summary>
public interface IModeleExcelService
{
    string DossierModeles { get; }

    IReadOnlyList<ModeleExcel> ListerModeles();

    /// <summary>
    /// Crée les classeurs manquants. Les fichiers déjà présents ne sont jamais
    /// écrasés, afin de préserver les saisies de l'utilisateur.
    /// </summary>
    Task<IReadOnlyList<ModeleExcelFichier>> CreerModelesAsync(CancellationToken ct = default);
}
