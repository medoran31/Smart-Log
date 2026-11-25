namespace Smartlog.Services;

/// <summary>
/// Service dédié au marché : bordereau de prix unitaires et pièces
/// associées (PDF, Word, Excel). Toutes les écritures sont journalisées via
/// IJournalService ; les montants du bordereau sont calculés ici et nulle
/// part ailleurs.
/// </summary>
public interface IMarcheService
{
    Task<MarcheDetail?> ObtenirAsync(Guid projetId, CancellationToken ct = default);

    Task<decimal> CalculerTotalBordereauAsync(Guid marcheId, CancellationToken ct = default);

    Task<PrixUnitaireResume> AjouterPrixUnitaireAsync(
        Guid marcheId,
        PrixUnitaireCreation donnees,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<PrixUnitaireResume> ModifierPrixUnitaireAsync(
        PrixUnitaireModification donnees,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task SupprimerPrixUnitaireAsync(
        Guid prixUnitaireId,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>Importe une pièce du marché et l'enregistre dans le journal.</summary>
    Task<DocumentMarche> ImporterDocumentAsync(
        Guid marcheId,
        string cheminSource,
        Guid utilisateurId,
        CancellationToken ct = default);
}

public sealed record MarcheDetail(
    Guid Id,
    Guid ProjetId,
    string ProjetIntitule,
    string ReferenceMarche,
    DateOnly? DateSignature,
    decimal MontantActualise,
    IReadOnlyList<PrixUnitaireResume> PrixUnitaires,
    IReadOnlyList<DocumentMarche> Documents);

public sealed record PrixUnitaireResume(
    Guid Id,
    string Reference,
    string Designation,
    string Unite,
    decimal PrixUnitaireHT,
    decimal QuantiteContractuelle,
    decimal MontantContractuel);

public sealed record PrixUnitaireCreation(
    string Reference,
    string Designation,
    string Unite,
    decimal PrixUnitaireHT,
    decimal QuantiteContractuelle);

public sealed record PrixUnitaireModification(
    Guid Id,
    string Reference,
    string Designation,
    string Unite,
    decimal PrixUnitaireHT,
    decimal QuantiteContractuelle);

public sealed record DocumentMarche(
    Guid Id,
    string Categorie,
    string NomFichier,
    string CheminFichier,
    int Version);
