namespace Smartlog.Services;

/// <summary>Requête envoyée à l'API Messages de Claude, sans secret transporté ici.</summary>
public sealed record ClaudeRequete(
    string Modele,
    string VersionApi,
    string? IdentifiantEspaceTravail,
    string InviteSysteme,
    IReadOnlyList<ClaudeMessage> Messages,
    string SchemaJson,
    int MaxTokens = 2048);

public sealed record ClaudeMessage(string Role, string Contenu);

/// <summary>Abstraction HTTP de Claude, injectable pour les tests.</summary>
public interface IClientClaude
{
    Task<string> EnvoyerAsync(ClaudeRequete requete, CancellationToken ct = default);
}
