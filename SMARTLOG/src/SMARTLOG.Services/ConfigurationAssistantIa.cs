using System.Text.Json;

namespace Smartlog.Services;

/// <summary>
/// Lit la configuration de l'assistant IA depuis
/// <c>%LOCALAPPDATA%\Smartlog\assistant-ia.json</c>. Les variables
/// d'environnement <c>SMARTLOG_CLAUDE_*</c> prennent le pas sur le fichier.
/// Aucune clé n'est écrite dans le code, ni journalisée, ni retournée par
/// <see cref="Resumer"/>.
/// </summary>
public class ConfigurationAssistantIa : IAssistantIaConfiguration
{
    public const string VariableCleApi = "SMARTLOG_CLAUDE_API_KEY";
    public const string VariableModele = "SMARTLOG_CLAUDE_MODELE";
    public const string VariableVersionApi = "SMARTLOG_CLAUDE_VERSION_API";
    public const string VariableEspaceTravail = "SMARTLOG_CLAUDE_WORKSPACE_ID";

    private const string ModeleParDefaut = "claude-opus-5-5";
    private const string VersionParDefaut = "2026-10-21";

    private readonly JsonElement _racine;

    public string CheminFichier { get; }

    public ConfigurationAssistantIa()
    {
        var dossier = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartlog");
        CheminFichier = Path.Combine(dossier, "assistant-ia.json");
        _racine = LireFichier(CheminFichier);
    }

    public string? CleApi =>
        Lire(VariableCleApi, "cleApi", "CleApi", "apiKey", "ApiKey");

    public string Modele =>
        Lire(VariableModele, "modele", "Modele", "model", "Model") ?? ModeleParDefaut;

    public string VersionApi =>
        Lire(VariableVersionApi, "versionApi", "VersionApi", "anthropicVersion") ?? VersionParDefaut;

    public string? IdentifiantEspaceTravail =>
        Lire(VariableEspaceTravail, "identifiantEspaceTravail", "IdentifiantEspaceTravail", "workspaceId", "WorkspaceId");

    public bool EstConfigure => !string.IsNullOrWhiteSpace(CleApi);

    /// <summary>Description sans secret, destinée à l'interface.</summary>
    public string Resumer() =>
        EstConfigure
            ? $"Claude configuré ({Modele}) — clé lue dans {(Environment.GetEnvironmentVariable(VariableCleApi) is not null ? "l'environnement" : CheminFichier)}."
            : $"Claude non configuré. Ajoutez « CleApi » dans {CheminFichier} " +
              $"ou définissez la variable {VariableCleApi}.";

    private string? Lire(string variable, params string[] proprietes)
    {
        var depuisEnvironnement = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrWhiteSpace(depuisEnvironnement))
            return depuisEnvironnement.Trim();

        foreach (var propriete in proprietes)
        {
            if (_racine.ValueKind != JsonValueKind.Object ||
                !_racine.TryGetProperty(propriete, out var valeur))
                continue;

            var texte = valeur.ValueKind == JsonValueKind.String ? valeur.GetString() : null;
            if (!string.IsNullOrWhiteSpace(texte))
                return texte.Trim();
        }

        return null;
    }

    private static JsonElement LireFichier(string chemin)
    {
        if (!File.Exists(chemin))
            return default;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(chemin));
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return default;
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }
}
