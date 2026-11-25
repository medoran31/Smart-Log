using System.Text.Json.Serialization;

namespace Smartlog.Services;

public enum RoleMessageAssistant
{
    Utilisateur,
    Assistant
}

public enum TypeSuggestionAssistant
{
    Aucune,
    CreerReserve,
    CreerCourrier,
    CreerTache
}

/// <summary>Message de la conversation, mémorisé par la vue et renvoyé en contexte.</summary>
public sealed record MessageConversation(
    RoleMessageAssistant Role,
    string Contenu,
    DateTime Horodatage);

/// <summary>Information dont l'origine est un document du projet (jamais inventée).</summary>
public sealed record ExtraitDocument(string Source, string Contenu);

/// <summary>Valeur calculée par un service SMARTLOG (jamais inventée par l'IA).</summary>
public sealed record CalculSysteme(string Libelle, string Valeur, string Explication);

/// <summary>
/// Suggestation produite par l'IA. Aucune action n'est effectuée tant que
/// l'utilisateur n'a pas cliqué sur « Valider et appliquer ».
/// </summary>
public sealed record SuggestionAssistant(
    string Id,
    string Titre,
    string Description,
    TypeSuggestionAssistant TypeAction,
    string? DescriptionReserve,
    string? PrioriteReserve,
    int? DelaiLeveeJours,
    string? SensCourrier,
    string? ExpediteurCourrier,
    string? DestinataireCourrier,
    string? ObjetCourrier,
    int? DelaiReponseJours,
    string? LibelleTache,
    string? DateDebutTache,
    string? DateFinTache)
{
    public bool Appliquee { get; init; }
    public bool Ignoree { get; init; }
    public string? Resultat { get; init; }
    public bool EstTraitee => Appliquee || Ignoree;

    public SuggestionAssistant MarquerAppliquee(string resultat) => this with
    {
        Appliquee = true,
        Ignoree = false,
        Resultat = resultat
    };

    public SuggestionAssistant MarquerIgnoree() => this with
    {
        Appliquee = false,
        Ignoree = true,
        Resultat = "Suggestion ignorée."
    };
}

/// <summary>Réponse affichée, avec provenance séparée par construction.</summary>
public sealed record ReponseAssistant(
    string Texte,
    string ContexteProjet,
    IReadOnlyList<ExtraitDocument> Extraits,
    IReadOnlyList<CalculSysteme> Calculs,
    IReadOnlyList<SuggestionAssistant> Suggestions);

public sealed record ResultatApplicationSuggestion(
    bool Succes,
    string Message,
    Guid? EntiteCreeeId);

/// <summary>Format JSON attendu du modèle (structured outputs de l'API Claude).</summary>
public sealed class ReponseClaudeBrute
{
    [JsonPropertyName("reponse")]
    public string Reponse { get; set; } = string.Empty;

    [JsonPropertyName("suggestions")]
    public List<SuggestionClaudeBrute> Suggestions { get; set; } = new();
}

public sealed class SuggestionClaudeBrute
{
    [JsonPropertyName("titre")]
    public string Titre { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("type_action")]
    public string TypeAction { get; set; } = "aucune";

    [JsonPropertyName("description_reserve")]
    public string DescriptionReserve { get; set; } = string.Empty;

    [JsonPropertyName("priorite_reserve")]
    public string PrioriteReserve { get; set; } = "moyenne";

    // "number" est le type JSON pris en charge par les structured outputs.
    [JsonPropertyName("delai_levee_jours")]
    public double DelaiLeveeJours { get; set; }

    [JsonPropertyName("sens_courrier")]
    public string SensCourrier { get; set; } = "entrant";

    [JsonPropertyName("expediteur_courrier")]
    public string ExpediteurCourrier { get; set; } = string.Empty;

    [JsonPropertyName("destinataire_courrier")]
    public string DestinataireCourrier { get; set; } = string.Empty;

    [JsonPropertyName("objet_courrier")]
    public string ObjetCourrier { get; set; } = string.Empty;

    [JsonPropertyName("delai_reponse_jours")]
    public double DelaiReponseJours { get; set; }

    [JsonPropertyName("libelle_tache")]
    public string LibelleTache { get; set; } = string.Empty;

    [JsonPropertyName("date_debut_tache")]
    public string DateDebutTache { get; set; } = string.Empty;

    [JsonPropertyName("date_fin_tache")]
    public string DateFinTache { get; set; } = string.Empty;
}
