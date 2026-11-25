using Smartlog.Domain.Enums;

namespace Smartlog.Services;

/// <summary>
/// Registre des courriers entrants et sortants. Les échéances de réponse
/// sont calculées par le service et jamais saisies dans l'interface.
/// </summary>
public interface ICourrierService
{
    Task<CourrierResume> CreerAsync(
        Guid projetId,
        SensCourrier sens,
        string expediteur,
        string destinataire,
        string objet,
        DateOnly dateCourrier,
        int? delaiReponseJours,
        Guid utilisateurId,
        CancellationToken ct = default);

    Task<CourrierResume> EnregistrerReponseAsync(
        Guid courrierId, DateOnly dateReponse, Guid utilisateurId,
        CancellationToken ct = default);

    Task<CourrierResume> MarquerSansSuiteAsync(
        Guid courrierId, Guid utilisateurId, CancellationToken ct = default);

    Task<CourrierResume?> ObtenirAsync(Guid courrierId, CancellationToken ct = default);

    Task<IReadOnlyList<CourrierResume>> ListerAsync(
        Guid? projetId = null,
        SensCourrier? sens = null,
        bool enAttenteSeulement = false,
        CancellationToken ct = default);
}

public sealed record CourrierResume(
    Guid Id,
    Guid ProjetId,
    SensCourrier Sens,
    string SensLibelle,
    string Expediteur,
    string Destinataire,
    string Objet,
    DateOnly DateCourrier,
    int? DelaiReponseJours,
    DateOnly? DateEcheanceReponse,
    int? JoursRestants,
    bool EstEnRetard,
    StatutCourrier Statut,
    string StatutLibelle,
    DateOnly? DateReponse);
