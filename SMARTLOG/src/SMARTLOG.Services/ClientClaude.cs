using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Smartlog.Services;

/// <summary>
/// Client HTTP de l'API Messages de Claude. La clé est lue dans la
/// configuration locale à chaque appel : elle n'est ni stockée dans un champ
/// sérialisé, ni journalisée, ni incluse dans les exceptions.
/// </summary>
public sealed class ClientClaude : IClientClaude
{
    private const string UrlApi = "https://api.anthropic.com/v1/messages";

    private readonly IAssistantIaConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public ClientClaude(IAssistantIaConfiguration configuration, HttpClient? httpClient = null)
    {
        _configuration = configuration;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<string> EnvoyerAsync(ClaudeRequete requete, CancellationToken ct = default)
    {
        var cle = _configuration.CleApi;
        if (string.IsNullOrWhiteSpace(cle))
        {
            throw new InvalidOperationException(
                "Claude n'est pas configuré. Définissez SMARTLOG_CLAUDE_API_KEY " +
                $"ou « CleApi » dans {_configuration.CheminFichier}.");
        }

        using var contenu = new StringContent(ConstruireCorps(requete), Encoding.UTF8, "application/json");
        using var message = new HttpRequestMessage(HttpMethod.Post, UrlApi) { Content = contenu };
        message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cle}");
        message.Headers.TryAddWithoutValidation("anthropic-version", requete.VersionApi);
        if (!string.IsNullOrWhiteSpace(requete.IdentifiantEspaceTravail))
        {
            message.Headers.TryAddWithoutValidation(
                "anthropic-workspace-id", requete.IdentifiantEspaceTravail.Trim());
        }

        using var reponse = await _httpClient.SendAsync(message, ct);
        var texteReponse = await reponse.Content.ReadAsStringAsync(ct);

        if (!reponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Claude a refusé la requête ({(int)reponse.StatusCode} {reponse.ReasonPhrase}) : " +
                ExtraireErreur(texteReponse));
        }

        return ExtraireTexte(texteReponse);
    }

    private static string ConstruireCorps(ClaudeRequete requete)
    {
        var messages = new JsonArray();
        foreach (var message in requete.Messages)
        {
            messages.Add(new JsonObject
            {
                ["role"] = message.Role,
                ["content"] = message.Contenu
            });
        }

        var corps = new JsonObject
        {
            ["model"] = requete.Modele,
            ["max_tokens"] = requete.MaxTokens,
            ["system"] = requete.InviteSysteme,
            ["messages"] = messages,
            ["output_config"] = new JsonObject
            {
                ["format"] = new JsonObject
                {
                    ["type"] = "json_schema",
                    ["schema"] = JsonNode.Parse(requete.SchemaJson)
                }
            }
        };

        // Le workspace est transmis uniquement en en-tête : l'API Messages
        // n'attend pas ce champ dans le corps de la requête.
        return corps.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static string ExtraireTexte(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("content", out var blocs) &&
                blocs.ValueKind == JsonValueKind.Array)
            {
                foreach (var bloc in blocs.EnumerateArray())
                {
                    if (bloc.ValueKind == JsonValueKind.Object &&
                        bloc.TryGetProperty("type", out var type) &&
                        type.ValueKind == JsonValueKind.String &&
                        type.GetString() == "text" &&
                        bloc.TryGetProperty("text", out var texte) &&
                        texte.ValueKind == JsonValueKind.String)
                    {
                        return texte.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Traité ci-dessous comme une réponse inexploitable.
        }

        throw new InvalidOperationException("Réponse Claude inexploitable : aucun bloc de texte.");
    }

    private static string ExtraireErreur(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var erreur) &&
                erreur.ValueKind == JsonValueKind.Object &&
                erreur.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? json;
            }
        }
        catch (JsonException)
        {
            // Le corps brut est renvoyé tronqué.
        }

        var extrait = json.Length > 300 ? json[..300] : json;
        return string.Format(CultureInfo.InvariantCulture, "corps de réponse : {0}", extrait);
    }
}
