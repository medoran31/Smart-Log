namespace Smartlog.Services.Ged;

/// <summary>
/// Gestion électronique des documents : stockage local par projet, OCR
/// tolérant aux pannes, et recherche plein texte simple sur le contenu OCR
/// et les métadonnées. Toutes les écritures passent par IJournalService.
/// </summary>
public interface IGedService
{
    /// <summary>Importe un fichier dans le dossier du projet et tente l'OCR.</summary>
    Task<DocumentGedResume> ImporterAsync(
        DemandeImportDocument demande,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<IReadOnlyList<DocumentGedResume>> ListerAsync(
        Guid? projetId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Recherche tous les termes (séparés par des espaces) dans le contenu
    /// OCR et les métadonnées ; la comparaison ignore la casse et les accents.
    /// </summary>
    Task<IReadOnlyList<DocumentGedResume>> RechercherAsync(
        Guid? projetId,
        string recherche,
        CancellationToken ct = default);

    Task<DocumentGedResume?> ObtenirAsync(Guid documentId, CancellationToken ct = default);
}

public sealed record DemandeImportDocument(
    Guid ProjetId,
    string CheminSource,
    string? SousDossier = null,
    string? EntiteLieeType = null,
    Guid? EntiteLieeId = null);

public sealed record DocumentGedResume(
    Guid Id,
    Guid ProjetId,
    string ProjetCode,
    string ProjetIntitule,
    string NomFichier,
    string Categorie,
    int Version,
    string CheminFichier,
    string Dossier,
    long TailleOctets,
    DateTime DateModification,
    string? ContenuOcr,
    string StatutOcr,
    string? EntiteLieeType,
    Guid? EntiteLieeId)
{
    /// <summary>Extrait court pour l'aperçu de la grille.</summary>
    public string Apercu =>
        string.IsNullOrWhiteSpace(ContenuOcr)
            ? "Aucun texte extrait (document non OCRisé)."
            : ContenuOcr.Length <= 240 ? ContenuOcr : ContenuOcr[..240] + "…";

    /// <summary>Taille affichée dans la grille.</summary>
    public string TailleLisible => TailleOctets switch
    {
        >= 1_048_576 => $"{TailleOctets / 1_048_576d:F2} Mo",
        >= 1024 => $"{TailleOctets / 1024d:F1} Ko",
        _ => $"{TailleOctets} o"
    };
}
