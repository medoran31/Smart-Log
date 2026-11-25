using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Seul point de calcul et d'écriture du suivi des réserves. Le cycle de vie
/// est strict : Nouvelle → EnCours → EnVerification → Levee → Cloturee.
/// La levée génère un procès-verbal via le moteur de modèles du Prompt 4.
/// </summary>
public interface IReserveService
{
    Task<ReserveResume> CreerAsync(
        Guid projetId,
        string description,
        PrioriteReserve priorite,
        Guid? entrepriseResponsableId,
        DateOnly dateConstatation,
        int delaiLeveeJours,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<ReserveResume> ModifierAsync(
        Guid reserveId,
        string description,
        PrioriteReserve priorite,
        Guid? entrepriseResponsableId,
        DateOnly dateConstatation,
        int delaiLeveeJours,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<ReserveResume> ChangerStatutAsync(
        Guid reserveId, StatutReserve nouveauStatut, Guid utilisateurId,
        CancellationToken ct = default);

    Task<ReserveResume?> ObtenirAsync(Guid reserveId, CancellationToken ct = default);

    /// <summary>Entreprises du projet pouvant être désignées responsables.</summary>
    Task<IReadOnlyList<IntervenantResume>> ListerEntreprisesAsync(
        Guid projetId, CancellationToken ct = default);

    /// <summary>Liste les réserves, triées par projet puis par numéro.</summary>
    Task<IReadOnlyList<ReserveResume>> ListerAsync(
        Guid? projetId = null, bool inclureCloturees = true, CancellationToken ct = default);
}

public sealed record IntervenantResume(Guid Id, string RaisonSociale);

/// <summary>Échéances, libellés et ancienneté calculés par le service, jamais saisis.</summary>
public sealed record ReserveResume(
    Guid Id,
    Guid ProjetId,
    int Numero,
    string Libelle,
    string Description,
    PrioriteReserve Priorite,
    string PrioriteLibelle,
    Guid? EntrepriseResponsableId,
    string EntrepriseResponsableLibelle,
    DateOnly DateConstatation,
    int DelaiLeveeJours,
    DateOnly DateEcheanceLevee,
    int JoursRestants,
    bool EstEnRetard,
    StatutReserve Statut,
    string StatutLibelle,
    DateOnly? DateLevee,
    string? CheminPvLevee);
