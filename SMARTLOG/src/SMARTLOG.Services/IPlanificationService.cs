using Smartlog.Domain.Entities;

namespace Smartlog.Services;

/// <summary>
/// Unique point de calcul et d'écriture du planning : arborescence WBS,
/// liaisons, CPM et cascade des dates prévisionnelles. Les dates calculées et
/// le flag de chemin critique ne sont jamais saisis ailleurs.
/// </summary>
public interface IPlanificationService
{
    Task<TacheResume> CreerTacheAsync(
        Guid projetId, Guid? parentTacheId, string libelle,
        DateOnly dateDebut, DateOnly dateFin, Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>Modifie une tâche puis recalcule toute la cascade (durée ou date).</summary>
    Task<TacheResume> ModifierTacheAsync(
        Guid tacheId, string libelle, DateOnly dateDebut, DateOnly dateFin,
        Guid utilisateurId, CancellationToken ct = default);

    Task SupprimerTacheAsync(Guid tacheId, Guid utilisateurId, CancellationToken ct = default);

    Task<LiaisonResume> AjouterLiaisonAsync(
        Guid predecesseurId, Guid successeurId, TypeLiaison typeLiaison,
        Guid utilisateurId, CancellationToken ct = default);

    Task SupprimerLiaisonAsync(Guid liaisonId, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>Recalcule et persiste dates prévues et chemin critique du projet.</summary>
    Task<PlanningDetail> RecalculerAsync(
        Guid projetId, Guid utilisateurId, CancellationToken ct = default);

    /// <summary>Lecture du planning persistant (sans recalcul).</summary>
    Task<PlanningDetail> ObtenirAsync(Guid projetId, CancellationToken ct = default);

    /// <summary>Calcul CPM sans écriture : aperçu et tests.</summary>
    Task<PlanningDetail> CalculerAsync(Guid projetId, CancellationToken ct = default);
}

public sealed record TacheResume(
    Guid Id,
    Guid ProjetId,
    Guid? ParentTacheId,
    string Libelle,
    int Niveau,
    int DureeJours,
    DateOnly DateDebutPrevue,
    DateOnly DateFinPrevue,
    DateOnly? DateDebutReelle,
    DateOnly? DateFinReelle,
    decimal AvancementPct,
    bool EstCheminCritique,
    int MargeTotale,
    int NombreSuccesseurs);

public sealed record LiaisonResume(
    Guid Id,
    Guid PredecesseurId,
    Guid SuccesseurId,
    TypeLiaison TypeLiaison,
    string TypeLibelle,
    string PredecesseurLibelle,
    string SuccesseurLibelle);

public sealed record PlanningDetail(
    Guid ProjetId,
    IReadOnlyList<TacheResume> Taches,
    IReadOnlyList<LiaisonResume> Liaisons,
    IReadOnlyList<Guid> TachesCritiques,
    DateOnly DateDebutProjet,
    DateOnly DateFinProjet,
    int DureeProjetJours)
{
    public int NombreTaches => Taches.Count;
    public int NombreLiaisons => Liaisons.Count;
    public int NombreTachesCritiques => TachesCritiques.Count;
}
