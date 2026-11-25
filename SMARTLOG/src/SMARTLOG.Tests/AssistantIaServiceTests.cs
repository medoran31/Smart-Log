using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Domain.Enums;
using Smartlog.Services;
using Xunit;

namespace Smartlog.Tests;

public class AssistantIaServiceTests : SqliteTestBase
{
    private const string SuggestionReserve = """
        {"reponse":"[SUGGESTION] Je propose de créer une réserve.",
         "suggestions":[{
           "titre":"Réserve suggérée", "description":"Contrôle à prévoir",
           "type_action":"creer_reserve", "description_reserve":"Vérifier l'étanchéité",
           "priorite_reserve":"haute", "delai_levee_jours":12,
           "sens_courrier":"", "expediteur_courrier":"", "destinataire_courrier":"",
           "objet_courrier":"", "delai_reponse_jours":0,
           "libelle_tache":"", "date_debut_tache":"", "date_fin_tache":""
         }]}
        """;

    private const string SuggestionCourrier = """
        {"reponse":"[SUGGESTION] Je propose un courrier.",
         "suggestions":[{
           "titre":"Courrier suggéré", "description":"Relancer le planning",
           "type_action":"creer_courrier", "description_reserve":"",
           "priorite_reserve":"", "delai_levee_jours":0,
           "sens_courrier":"entrant", "expediteur_courrier":"Maître d'ouvrage",
           "destinataire_courrier":"Entreprise", "objet_courrier":"Relance planning",
           "delai_reponse_jours":10,
           "libelle_tache":"", "date_debut_tache":"", "date_fin_tache":""
         }]}
        """;

    private const string SuggestionTache = """
        {"reponse":"[SUGGESTION] Je propose une tâche.",
         "suggestions":[{
           "titre":"Tâche suggérée", "description":"Préparer la levée",
           "type_action":"creer_tache", "description_reserve":"",
           "priorite_reserve":"", "delai_levee_jours":0,
           "sens_courrier":"", "expediteur_courrier":"", "destinataire_courrier":"",
           "objet_courrier":"", "delai_reponse_jours":0,
           "libelle_tache":"Préparer la levée des réserves",
           "date_debut_tache":"2026-12-01", "date_fin_tache":"2026-12-05"
         }]}
        """;

    [Fact]
    public async Task PoserQuestion_ProjetAvecDocument_TransmetProvenanceSansEcrire()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Db.Documents.Add(new DocumentGed
        {
            ProjetId = projet.Id,
            TypeDocument = "Marchés",
            CheminFichier = @"C:\documents\contrat.pdf",
            ContenuOcr = "Le délai contractuel est de 300 jours."
        });
        await Db.SaveChangesAsync();

        var journalAvant = await Db.JournalActions.CountAsync();
        var reservesAvant = await Db.Reserves.CountAsync();

        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id,
            "Que dit le contrat ?",
            Array.Empty<MessageConversation>(),
            UtilisateurTestId);

        Assert.Equal(1, Claude.NombreAppels);
        Assert.Single(reponse.Extraits);
        Assert.Contains("contrat.pdf", reponse.Extraits[0].Source);
        Assert.Contains("300 jours", reponse.ContexteProjet);
        Assert.Contains("Montant actualisé", reponse.ContexteProjet);
        Assert.Contains("[DOC]", reponse.Texte);
        Assert.Contains("[CALCUL]", reponse.Texte);
        Assert.Contains("[SUGGESTION]", reponse.Texte);
        Assert.Contains("[DOC]", Claude.DerniereRequete!.InviteSysteme);
        Assert.Contains("[CALCUL]", Claude.DerniereRequete.InviteSysteme);
        Assert.Contains("[SUGGESTION]", Claude.DerniereRequete.InviteSysteme);
        Assert.Empty(reponse.Suggestions);
        Assert.Equal(journalAvant, await Db.JournalActions.CountAsync());
        Assert.Equal(reservesAvant, await Db.Reserves.CountAsync());
    }

    [Fact]
    public async Task PoserQuestion_AvecSuggestion_NeCreeRienAvantValidation()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = SuggestionReserve;

        var journalAvant = await Db.JournalActions.CountAsync();
        var reservesAvant = await Db.Reserves.CountAsync();

        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Que faire ?", Array.Empty<MessageConversation>(), UtilisateurTestId);

        var suggestion = Assert.Single(reponse.Suggestions);
        Assert.Equal(TypeSuggestionAssistant.CreerReserve, suggestion.TypeAction);
        Assert.Equal("Vérifier l'étanchéité", suggestion.DescriptionReserve);
        Assert.Equal(12, suggestion.DelaiLeveeJours);
        Assert.False(suggestion.EstTraitee);
        Assert.Equal(journalAvant, await Db.JournalActions.CountAsync());
        Assert.Equal(reservesAvant, await Db.Reserves.CountAsync());
    }

    [Fact]
    public async Task AppliquerSuggestion_Reserve_CreeEtJournalise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = SuggestionReserve;
        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Que faire ?", Array.Empty<MessageConversation>(), UtilisateurTestId);
        var suggestion = reponse.Suggestions[0];
        var avant = await Db.Reserves.CountAsync();

        var resultat = await AssistantIa.AppliquerSuggestionAsync(
            suggestion, projet.Id, UtilisateurTestId);

        Assert.True(resultat.Succes);
        Assert.NotNull(resultat.EntiteCreeeId);
        Assert.Equal(avant + 1, await Db.Reserves.CountAsync());
        Assert.True(await Db.JournalActions.AnyAsync(j => j.Action == "ApplicationSuggestion"));

        var reserve = await Db.Reserves.SingleAsync(r => r.Id == resultat.EntiteCreeeId);
        Assert.Equal(projet.Id, reserve.ProjetId);
        Assert.Equal("Vérifier l'étanchéité", reserve.Description);
    }

    [Fact]
    public async Task AppliquerSuggestion_Courrier_CreeEtJournalise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = SuggestionCourrier;
        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Que faire ?", Array.Empty<MessageConversation>(), UtilisateurTestId);
        var suggestion = reponse.Suggestions[0];

        var resultat = await AssistantIa.AppliquerSuggestionAsync(
            suggestion, projet.Id, UtilisateurTestId);

        Assert.True(resultat.Succes);
        var courrier = await Db.Courriers.SingleAsync(c => c.Id == resultat.EntiteCreeeId);
        Assert.Equal(SensCourrier.Entrant, courrier.Sens);
        Assert.Equal("Relance planning", courrier.Objet);
        Assert.Equal(10, courrier.DelaiReponseJours);
    }

    [Fact]
    public async Task AppliquerSuggestion_Tache_CreeEtJournalise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = SuggestionTache;
        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Que faire ?", Array.Empty<MessageConversation>(), UtilisateurTestId);
        var suggestion = reponse.Suggestions[0];

        var resultat = await AssistantIa.AppliquerSuggestionAsync(
            suggestion, projet.Id, UtilisateurTestId);

        Assert.True(resultat.Succes);
        var tache = await Db.Taches.SingleAsync(t => t.Id == resultat.EntiteCreeeId);
        Assert.Equal("Préparer la levée des réserves", tache.Libelle);
        Assert.Equal(new DateOnly(2026, 12, 1), tache.DateDebutPrevue);
        Assert.Equal(new DateOnly(2026, 12, 5), tache.DateFinPrevue);
    }

    [Fact]
    public async Task IgnorerSuggestion_TraceSansCreerDEntite()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = SuggestionReserve;
        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Que faire ?", Array.Empty<MessageConversation>(), UtilisateurTestId);
        var suggestion = reponse.Suggestions[0];
        var avant = await Db.Reserves.CountAsync();

        var resultat = await AssistantIa.IgnorerSuggestionAsync(
            suggestion, projet.Id, UtilisateurTestId);

        Assert.True(resultat.Succes);
        Assert.Null(resultat.EntiteCreeeId);
        Assert.Equal(avant, await Db.Reserves.CountAsync());
        Assert.True(await Db.JournalActions.AnyAsync(j => j.Action == "SuggestionIgnoree"));
    }

    [Fact]
    public async Task PoserQuestion_ReponseSansMarqueur_QualifieLaSuggestion()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Claude.ReponseJson = """{"reponse":"Réponse sans marqueur.","suggestions":[]}""";

        var reponse = await AssistantIa.PoserQuestionAsync(
            projet.Id, "Bonjour", Array.Empty<MessageConversation>(), UtilisateurTestId);

        Assert.StartsWith("[SUGGESTION]", reponse.Texte);
    }

    [Fact]
    public async Task ClientClaude_SansClee_RefuseSansAppelHttp()
    {
        var configuration = new ConfigurationAssistantFactice { CleApi = null };
        var handler = new HttpMessageHandlerFactice();
        using var http = new HttpClient(handler);
        var client = new ClientClaude(configuration, http);
        var requete = new ClaudeRequete(
            "claude-opus-5-5", "2026-10-21", null, "systeme",
            new[] { new ClaudeMessage("user", "bonjour") }, "{}");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.EnvoyerAsync(requete));

        Assert.Equal(0, handler.NombreAppels);
    }

    private sealed class HttpMessageHandlerFactice : HttpMessageHandler
    {
        public int NombreAppels { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            NombreAppels++;
            throw new InvalidOperationException("Aucun appel HTTP n'était attendu.");
        }
    }
}
