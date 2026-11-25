using System.Net;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Le client HTTP est testé avec un handler factice : aucune clé réelle, aucun
/// appel réseau. On vérifie le contrat de requête et le traitement des réponses.
/// </summary>
public class ClientClaudeHttpTests
{
    private static ClaudeRequete Requete => new(
        "claude-opus-5-5", "2026-10-21", "workspace-test",
        "Réponds uniquement en JSON.",
        new[] { new ClaudeMessage("user", "Quelles réserves sont en retard ?") },
        "{\"type\":\"object\",\"properties\":{\"reponse\":{\"type\":\"string\"}}}");

    [Fact]
    public async Task EnvoyerAsync_ReponseValide_RenvoieLeTexteEtRespecteLeContratHttp()
    {
        var handler = new HandlerFactice(HttpStatusCode.OK,
            "{\"content\":[{\"type\":\"text\",\"text\":\"{\\\"reponse\\\":\\\"OK\\\"}\"}]}");
        using var http = new HttpClient(handler);
        var client = new ClientClaude(new ConfigurationFactice(), http);

        var texte = await client.EnvoyerAsync(Requete);

        Assert.Equal("{\"reponse\":\"OK\"}", texte);
        Assert.Equal(HttpMethod.Post, handler.Requete!.Method);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.Requete.RequestUri!.ToString());
        Assert.Equal("Bearer cle-de-test", handler.Requete.Headers.Authorization!.ToString());
        Assert.Equal("2026-10-21", handler.Requete.Headers.GetValues("anthropic-version").Single());
        Assert.Equal(
            "workspace-test", handler.Requete.Headers.GetValues("anthropic-workspace-id").Single());
        Assert.Equal("application/json", handler.Requete.Content!.Headers.ContentType!.MediaType);
        var corps = handler.Corps;
        Assert.Contains("output_config", corps);
        Assert.Contains("json_schema", corps);
        Assert.Contains("Quelles r", corps);
        Assert.DoesNotContain("cle-de-test", corps);
    }

    [Fact]
    public async Task EnvoyerAsync_RefusHttp_ExposeLeStatutEtLeMessageSansLaClef()
    {
        var handler = new HandlerFactice(HttpStatusCode.BadRequest,
            "{\"error\":{\"type\":\"invalid_request_error\",\"message\":\"modele inconnu\"}}");
        using var http = new HttpClient(handler);
        var client = new ClientClaude(new ConfigurationFactice(), http);

        var erreur = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.EnvoyerAsync(Requete));

        Assert.Contains("400", erreur.Message);
        Assert.Contains("modele inconnu", erreur.Message);
        Assert.DoesNotContain("cle-de-test", erreur.Message);
    }

    [Fact]
    public async Task EnvoyerAsync_ReponseSansBlocTexte_EchoueClairement()
    {
        var handler = new HandlerFactice(HttpStatusCode.OK, "{\"content\":[]}");
        using var http = new HttpClient(handler);
        var client = new ClientClaude(new ConfigurationFactice(), http);

        var erreur = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.EnvoyerAsync(Requete));

        Assert.Contains("aucun bloc de texte", erreur.Message);
    }

    private sealed class ConfigurationFactice : IAssistantIaConfiguration
    {
        public string? CleApi => "cle-de-test";
        public string Modele => "claude-opus-5-5";
        public string VersionApi => "2026-10-21";
        public string? IdentifiantEspaceTravail => "workspace-test";
        public string CheminFichier => "assistant-ia.json";
        public bool EstConfigure => true;
        public string Resumer() => "Configuration factice";
    }

    private sealed class HandlerFactice : HttpMessageHandler
    {
        private readonly HttpStatusCode _statut;
        private readonly string _contenu;

        public HttpRequestMessage? Requete { get; private set; }
        public string Corps { get; private set; } = string.Empty;

        public HandlerFactice(HttpStatusCode statut, string contenu)
        {
            _statut = statut;
            _contenu = contenu;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requete = request;
            Corps = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statut)
            {
                Content = new StringContent(_contenu)
            };
        }
    }
}
