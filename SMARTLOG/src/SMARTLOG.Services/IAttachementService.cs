using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Saisie des quantités exécutées. Le montant de chaque ligne est calculé ici
/// et nulle part ailleurs : MontantCalcule = QuantiteExecutee × PrixUnitaireHT.
/// Le reliquat contractuel et l'indicateur de dépassement sont également
/// fournis par ce service (jamais par l'interface).
/// </summary>
public interface IAttachementService
{
    Task<AttachementDetail> CreerAsync(
        Guid projetId, DateOnly date, Guid utilisateurId, CancellationToken ct = default);

    Task<LigneAttachementResume> AjouterLigneAsync(
        Guid attachementId,
        Guid prixUnitaireId,
        decimal quantiteExecutee,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>
    /// Calcul une ligne sans écriture : utilisé par l'aperçu temps réel du
    /// formulaire (montant et reliquat restent calculés par le service).
    /// </summary>
    Task<LigneAttachementResume> SimulerLigneAsync(
        Guid projetId,
        Guid prixUnitaireId,
        decimal quantiteExecutee,
        Guid? ligneIdAExclure = null,
        CancellationToken ct = default);

    Task<LigneAttachementResume> ModifierQuantiteAsync(
        Guid ligneId,
        decimal quantiteExecutee,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task SupprimerLigneAsync(
        Guid ligneId,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>
    /// Recalcule le MontantCalcule stocké de toutes les lignes qui utilisent un
    /// prix unitaire après modification du BPU. Sans cette opération, la valeur
    /// persistée diverge du prix appliqué aux situations financières.
    /// </summary>
    Task<int> RecalculerLignesAsync(
        Guid prixUnitaireId, Guid utilisateurId, CancellationToken ct = default);

    Task<AttachementDetail> ChangerStatutAsync(
        Guid attachementId,
        StatutAttachement statut,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<AttachementDetail?> ObtenirAsync(Guid attachementId, CancellationToken ct = default);

    Task<IReadOnlyList<AttachementResume>> ListerAsync(Guid projetId, CancellationToken ct = default);

    /// <summary>Toutes les lignes du projet, avec reliquat calculé à date.</summary>
    Task<IReadOnlyList<LigneAttachementResume>> ListerLignesAsync(
        Guid projetId, CancellationToken ct = default);
}

public sealed record LigneAttachementResume(
    Guid Id,
    Guid AttachementId,
    Guid PrixUnitaireId,
    string ReferencePrix,
    string Designation,
    string Unite,
    decimal PrixUnitaireHT,
    decimal QuantiteContractuelle,
    decimal QuantiteExecutee,
    decimal QuantiteDejaExecuteeAilleurs,
    decimal ReliquatAvantLigne,
    decimal ReliquatApresLigne,
    decimal MontantCalcule,
    bool DepasseReliquat,
    string StatutAttachement);

public sealed record AttachementResume(
    Guid Id,
    Guid ProjetId,
    DateOnly Date,
    string Statut,
    StatutAttachement StatutValeur,
    int NombreLignes,
    decimal MontantTotal);

public sealed record AttachementDetail(
    Guid Id,
    Guid ProjetId,
    DateOnly Date,
    string Statut,
    StatutAttachement StatutValeur,
    int NombreLignes,
    decimal MontantTotal,
    IReadOnlyList<LigneAttachementResume> Lignes);
