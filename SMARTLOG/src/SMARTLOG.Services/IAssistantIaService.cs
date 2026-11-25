namespace Smartlog.Services;

/// <summary>
/// Assistant contextualisé au projet ouvert. Les faits sont assemblés par
/// SMARTLOG (documents et calculs), la réponse et les suggestions sont
/// produites par Claude. Aucune suggestion n'est appliquée par
/// <see cref="PoserQuestionAsync"/> : l'application passe obligatoirement par
/// <see cref="AppliquerSuggestionAsync"/>, appelée après validation explicite.
/// </summary>
public interface IAssistantIaService
{
    /// <summary>Vrai si une clé API est disponible dans la configuration locale.</summary>
    bool EstConfigure { get; }

    /// <summary>Description de la configuration, sans jamais exposer la clé.</summary>
    string ResumeConfiguration { get; }

    Task<ReponseAssistant> PoserQuestionAsync(
        Guid? projetId,
        string question,
        IReadOnlyList<MessageConversation> historique,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>
    /// Crée l'entité proposée dans le projet indiqué. Unique point d'écriture
    /// de l'assistant : appelé uniquement par le bouton « Valider et appliquer ».
    /// </summary>
    Task<ResultatApplicationSuggestion> AppliquerSuggestionAsync(
        SuggestionAssistant suggestion,
        Guid projetId,
        Guid utilisateurId,
        CancellationToken ct = default);

    /// <summary>Trace l'action explicite « Ignorer » sans modifier de donnée métier.</summary>
    Task<ResultatApplicationSuggestion> IgnorerSuggestionAsync(
        SuggestionAssistant suggestion,
        Guid projetId,
        Guid utilisateurId,
        CancellationToken ct = default);
}
