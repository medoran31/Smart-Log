using System.Globalization;
using System.Text;
using System.Text.Json;
using Smartlog.Domain.Enums;
using Smartlog.Services.Ged;

namespace Smartlog.Services;

/// <summary>
/// Assemble le contexte du projet à partir des services métier, interroge
/// Claude, puis sépare explicitement les extraits de documents, les valeurs
/// calculées par SMARTLOG et les suggestions de l'IA.
/// </summary>
public sealed class AssistantIaService : IAssistantIaService
{
    private const int NombreMaxExtraits = 8;
    private const int LongueurExtrait = 700;

    private static readonly CultureInfo CultureFr = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IClientClaude _client;
    private readonly IAssistantIaConfiguration _configuration;
    private readonly IProjetService _projets;
    private readonly IGedService _ged;
    private readonly ISituationService _situations;
    private readonly IPlanificationService _planification;
    private readonly IAlerteService _alertes;
    private readonly IReserveService _reserves;
    private readonly ICourrierService _courriers;
    private readonly IJournalService _journal;

    public AssistantIaService(
        IClientClaude client,
        IAssistantIaConfiguration configuration,
        IProjetService projets,
        IGedService ged,
        ISituationService situations,
        IPlanificationService planification,
        IAlerteService alertes,
        IReserveService reserves,
        ICourrierService courriers,
        IJournalService journal)
    {
        _client = client;
        _configuration = configuration;
        _projets = projets;
        _ged = ged;
        _situations = situations;
        _planification = planification;
        _alertes = alertes;
        _reserves = reserves;
        _courriers = courriers;
        _journal = journal;
    }

    public bool EstConfigure => _configuration.EstConfigure;

    public string ResumeConfiguration => _configuration.Resumer();

    public async Task<ReponseAssistant> PoserQuestionAsync(
        Guid? projetId,
        string question,
        IReadOnlyList<MessageConversation> historique,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new ArgumentException("La question ne peut pas être vide.", nameof(question));

        var contexte = await ConstruireContexteAsync(projetId, ct);
        var invite = ConstruireInviteSysteme(contexte.Texte);

        var messages = new List<ClaudeMessage>();
        foreach (var message in historique.TakeLast(10))
        {
            messages.Add(new ClaudeMessage(
                message.Role == RoleMessageAssistant.Assistant ? "assistant" : "user",
                message.Contenu));
        }
        messages.Add(new ClaudeMessage("user", question.Trim()));

        var corpsBrut = await _client.EnvoyerAsync(
            new ClaudeRequete(
                _configuration.Modele,
                _configuration.VersionApi,
                _configuration.IdentifiantEspaceTravail,
                invite,
                messages,
                SchemaReponse),
            ct);

        var reponseClaude = JsonSerializer.Deserialize<ReponseClaudeBrute>(
            ExtraireJson(corpsBrut), OptionsJson) ?? new ReponseClaudeBrute();

        var suggestions = reponseClaude.Suggestions
            .Where(s => !string.IsNullOrWhiteSpace(s.Titre))
            .Select(s => new SuggestionAssistant(
                Guid.NewGuid().ToString("N"),
                s.Titre.Trim(),
                string.IsNullOrWhiteSpace(s.Description) ? s.Titre.Trim() : s.Description.Trim(),
                MapperTypeAction(s.TypeAction),
                Propre(s.DescriptionReserve),
                Propre(s.PrioriteReserve),
                s.DelaiLeveeJours > 0 ? (int)Math.Round(s.DelaiLeveeJours) : null,
                Propre(s.SensCourrier),
                Propre(s.ExpediteurCourrier),
                Propre(s.DestinataireCourrier),
                Propre(s.ObjetCourrier),
                s.DelaiReponseJours > 0 ? (int)Math.Round(s.DelaiReponseJours) : null,
                Propre(s.LibelleTache),
                Propre(s.DateDebutTache),
                Propre(s.DateFinTache)))
            .Where(s => s.TypeAction != TypeSuggestionAssistant.Aucune)
            .ToList();

        return new ReponseAssistant(
            AppliquerMarqueurs(reponseClaude.Reponse),
            contexte.Texte,
            contexte.Extraits,
            contexte.Calculs,
            suggestions);
    }

    public async Task<ResultatApplicationSuggestion> AppliquerSuggestionAsync(
        SuggestionAssistant suggestion,
        Guid projetId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (suggestion.EstTraitee)
            return new ResultatApplicationSuggestion(false, "Cette suggestion a déjà été traitée.", null);
        if (suggestion.TypeAction == TypeSuggestionAssistant.Aucune)
            return new ResultatApplicationSuggestion(false, "Aucune action applicable à cette suggestion.", null);

        try
        {
            var entiteId = suggestion.TypeAction switch
            {
                TypeSuggestionAssistant.CreerReserve => await CreerReserveAsync(
                    suggestion, projetId, utilisateurId, ct),
                TypeSuggestionAssistant.CreerCourrier => await CreerCourrierAsync(
                    suggestion, projetId, utilisateurId, ct),
                TypeSuggestionAssistant.CreerTache => await CreerTacheAsync(
                    suggestion, projetId, utilisateurId, ct),
                _ => null
            };

            if (entiteId is null)
            {
                return new ResultatApplicationSuggestion(
                    false,
                    "La suggestion ne contient pas les informations minimales pour créer l'entité.",
                    null);
            }

            await _journal.EnregistrerAsync(
                utilisateurId,
                "AssistantIa",
                entiteId.Value,
                "ApplicationSuggestion",
                null,
                new { suggestion.Titre, TypeAction = suggestion.TypeAction.ToString() },
                ct);

            return new ResultatApplicationSuggestion(
                true,
                $"« {suggestion.Titre} » appliqué : l'entité a été créée et journalisée.",
                entiteId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ResultatApplicationSuggestion(false, $"Application impossible : {ex.Message}", null);
        }
    }

    public async Task<ResultatApplicationSuggestion> IgnorerSuggestionAsync(
        SuggestionAssistant suggestion,
        Guid projetId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        try
        {
            var entiteId = Guid.TryParse(suggestion.Id, out var id) ? id : Guid.Empty;
            await _journal.EnregistrerAsync(
                utilisateurId,
                "AssistantIa",
                entiteId,
                "SuggestionIgnoree",
                null,
                new { suggestion.Titre, ProjetId = projetId },
                ct);

            return new ResultatApplicationSuggestion(true, "Suggestion ignorée.", null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ResultatApplicationSuggestion(false, $"Enregistrement impossible : {ex.Message}", null);
        }
    }

    private async Task<ContexteAssistant> ConstruireContexteAsync(
        Guid? projetId, CancellationToken ct)
    {
        var extraits = new List<ExtraitDocument>();
        var calculs = new List<CalculSysteme>();
        var descriptionProjet = new List<string>();

        if (projetId is null)
        {
            descriptionProjet.Add("Aucun projet n'est ouvert dans SMARTLOG.");
        }
        else
        {
            var projet = await _projets.ObtenirAsync(projetId.Value, ct);
            if (projet is null)
            {
                descriptionProjet.Add($"Projet {projetId} introuvable.");
            }
            else
            {
                descriptionProjet.Add($"Code : {projet.Code}");
                descriptionProjet.Add($"Intitulé : {projet.Intitule}");
                descriptionProjet.Add($"Localisation : {projet.Localisation ?? "non renseignée"}");
                descriptionProjet.Add($"Nature des travaux : {projet.NatureTravaux ?? "non renseignée"}");
                descriptionProjet.Add($"Maître d'ouvrage : {projet.MaitreOuvrage ?? "non renseigné"}");
                descriptionProjet.Add($"Maître d'œuvre : {projet.MaitreOeuvre ?? "non renseigné"}");
                descriptionProjet.Add($"Entreprise : {projet.Entreprise ?? "non renseignée"}");
                descriptionProjet.Add($"Montant initial : {FormaterMontant(projet.MontantInitial)}");
                descriptionProjet.Add($"Délai contractuel : {projet.DelaiContractuelJours} jours");
                descriptionProjet.Add($"Notification : {FormaterDate(projet.DateNotification)}");
                descriptionProjet.Add($"Commencement : {FormaterDate(projet.DateCommencement)}");
                descriptionProjet.Add($"Achèvement prévisionnel : {FormaterDate(projet.DatePrevisionnelleAchevement)}");

                calculs.Add(new CalculSysteme(
                    "Montant actualisé",
                    FormaterMontant(projet.MontantActualise),
                    "Valeur dérivée calculée par IAvenantService."));
                calculs.Add(new CalculSysteme(
                    "Délai actualisé",
                    $"{projet.DelaiActualiseJours} jours",
                    "Valeur dérivée calculée par IAvenantService."));
            }

            foreach (var document in (await _ged.ListerAsync(projetId, ct))
                         .Where(d => !string.IsNullOrWhiteSpace(d.ContenuOcr))
                         .OrderByDescending(d => d.DateModification)
                         .Take(NombreMaxExtraits))
            {
                extraits.Add(new ExtraitDocument(
                    $"{document.NomFichier} (v{document.Version}, {document.Categorie})",
                    Tronquer(document.ContenuOcr!, LongueurExtrait)));
            }

            SuiviFinancier? suivi = null;
            string? erreurSuivi = null;
            try
            {
                suivi = await _situations.ObtenirSuiviAsync(projetId.Value, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                erreurSuivi = ex.Message;
            }

            if (suivi is not null)
            {
                calculs.Add(new CalculSysteme(
                    "Taux d'avancement",
                    $"{suivi.TauxAvancement.ToString("N1", CultureFr)} %",
                    "Calculé par ISituationService à partir des situations."));
                calculs.Add(new CalculSysteme(
                    "Total payé",
                    FormaterMontant(suivi.TotalPaye),
                    "Cumul calculé par ISituationService."));
                calculs.Add(new CalculSysteme(
                    "Restant dû",
                    FormaterMontant(suivi.TotalRestantDu),
                    "Différence calculée par ISituationService."));
            }
            else
            {
                calculs.Add(new CalculSysteme(
                    "Suivi financier",
                    "indisponible",
                    $"Calcul non disponible : {erreurSuivi}"));
            }
            calculs.Add(await EssayerCalculAsync(
                "Planning",
                async () =>
                {
                    var planning = await _planification.ObtenirAsync(projetId.Value, ct);
                    return new CalculSysteme(
                        "Planning",
                        $"{planning.DureeProjetJours} jours, {planning.NombreTachesCritiques} tâche(s) critique(s) " +
                        $"sur {planning.NombreTaches}",
                        "Durée et chemin critique calculés par IPlanificationService (CPM).");
                }));
            calculs.Add(await EssayerCalculAsync(
                "Alertes actives",
                async () =>
                {
                    var centre = await _alertes.ObtenirCentreAsync(projetId.Value, ct: ct);
                    return new CalculSysteme(
                        "Alertes actives",
                        $"{centre.Alertes.Count} (dont {centre.NombreEnRetard} en retard)",
                        "Calculé par IAlerteService.");
                }));
            calculs.Add(await EssayerCalculAsync(
                "Réserves ouvertes",
                async () =>
                {
                    var reserves = await _reserves.ListerAsync(projetId.Value, false, ct);
                    return new CalculSysteme(
                        "Réserves ouvertes",
                        $"{reserves.Count} (dont {reserves.Count(r => r.EstEnRetard)} en retard)",
                        "Échéances calculées par IReserveService.");
                }));
            calculs.Add(await EssayerCalculAsync(
                "Courriers en attente",
                async () =>
                {
                    var courriers = await _courriers.ListerAsync(projetId.Value, null, true, ct);
                    return new CalculSysteme(
                        "Courriers en attente",
                        $"{courriers.Count} (dont {courriers.Count(c => c.EstEnRetard)} hors délai)",
                        "Délais de réponse calculés par ICourrierService.");
                }));
        }

        var texte = new StringBuilder();
        texte.AppendLine("=== PROJET OUVERT ===");
        texte.AppendLine(descriptionProjet.Count > 0
            ? string.Join(Environment.NewLine, descriptionProjet)
            : "Aucun projet ouvert.");
        texte.AppendLine();
        texte.AppendLine("=== EXTRAITS DE DOCUMENTS (texte OCR de la GED) ===");
        texte.AppendLine(extraits.Count > 0
            ? string.Join(Environment.NewLine, extraits.Select(e => $"[DOC] {e.Source} : {e.Contenu}"))
            : "Aucun document exploitable pour ce projet.");
        texte.AppendLine();
        texte.AppendLine("=== VALEURS CALCULÉES PAR SMARTLOG ===");
        texte.AppendLine(string.Join(Environment.NewLine,
            calculs.Select(c => $"[CALCUL] {c.Libelle} : {c.Valeur} ({c.Explication})")));

        return new ContexteAssistant(texte.ToString(), extraits, calculs);
    }

    private static async Task<CalculSysteme> EssayerCalculAsync(
        string libelle, Func<Task<CalculSysteme>> fabrique)
    {
        try
        {
            return await fabrique();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new CalculSysteme(libelle, "indisponible", $"Calcul non disponible : {ex.Message}");
        }
    }

    private static string ConstruireInviteSysteme(string contexte)
    {
        return """
               Tu es l'assistant de SMARTLOG, un outil de suivi de chantier. Réponds en français, de manière concise et factuelle.

               RÈGLES ABSOLUES DE PROVENANCE :
               1. N'affirme aucun fait chiffré ou daté qui ne figure pas dans le contexte ci-dessous.
               2. Dans le champ « reponse », fais commencer chaque ligne d'information par exactement l'un des préfixes suivants :
                  [DOC] pour un fait issu d'un document du projet ;
                  [CALCUL] pour une valeur calculée par SMARTLOG ;
                  [SUGGESTION] pour une proposition que tu formules toi-même.
               3. Ne qualifie jamais un [DOC] ou un [CALCUL] comme une suggestion, et inversement.
               4. Si une information manque, dis-le explicitement au lieu de l'imaginer.
               5. Tu ne modifies aucune donnée : le champ « suggestions » ne fait que proposer des actions. L'utilisateur décide ensuite en cliquant sur « Valider et appliquer » ou « Ignorer ».

               ACTIONS POSSIBLES DANS LE CHAMP « suggestions » (type_action) :
               - creer_reserve : renseigne description_reserve, priorite_reserve (basse|moyenne|haute) et delai_levee_jours.
               - creer_courrier : renseigne sens_courrier (entrant|sortant), expediteur_courrier, destinataire_courrier, objet_courrier et delai_reponse_jours.
               - creer_tache : renseigne libelle_tache, date_debut_tache et date_fin_tache au format AAAA-MM-JJ.
               - aucune : aucune action à proposer.
               La date de création est toujours la date du jour, déterminée par SMARTLOG ; ne l'invente pas.

               CONTEXTE FOURNI PAR SMARTLOG (faits vérifiés) :
               """ + Environment.NewLine + contexte;
    }

    private static string ExtraireJson(string corpsBrut)
    {
        var texte = corpsBrut.Trim();
        var debut = texte.IndexOf('{');
        var fin = texte.LastIndexOf('}');
        if (debut < 0 || fin <= debut)
            throw new InvalidOperationException("Claude n'a pas renvoyé de JSON exploitable.");

        var json = texte[debut..(fin + 1)];
        try
        {
            using var document = JsonDocument.Parse(json);
            return json;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("La réponse Claude n'est pas un JSON valide.", ex);
        }
    }

    private static string AppliquerMarqueurs(string texte)
    {
        var lignes = (texte ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToList();

        if (lignes.Count == 0)
            return "Aucune réponse fournie par Claude.";

        var avecMarqueur = lignes.Any(l =>
            l.StartsWith("[DOC]", StringComparison.Ordinal) ||
            l.StartsWith("[CALCUL]", StringComparison.Ordinal) ||
            l.StartsWith("[SUGGESTION]", StringComparison.Ordinal));

        return avecMarqueur
            ? string.Join(Environment.NewLine, lignes)
            : string.Join(Environment.NewLine, lignes.Select(l => $"[SUGGESTION] {l}"));
    }

    private async Task<Guid?> CreerReserveAsync(
        SuggestionAssistant suggestion, Guid projetId, Guid utilisateurId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suggestion.DescriptionReserve))
            return null;

        var priorite = Normaliser(suggestion.PrioriteReserve ?? string.Empty) switch
        {
            "haute" => PrioriteReserve.Haute,
            "basse" => PrioriteReserve.Basse,
            _ => PrioriteReserve.Moyenne
        };
        var delai = suggestion.DelaiLeveeJours is > 0 ? suggestion.DelaiLeveeJours.Value : 7;

        var reserve = await _reserves.CreerAsync(
            projetId,
            suggestion.DescriptionReserve,
            priorite,
            null,
            DateOnly.FromDateTime(DateTime.Today),
            delai,
            utilisateurId,
            ct);
        return reserve.Id;
    }

    private async Task<Guid?> CreerCourrierAsync(
        SuggestionAssistant suggestion, Guid projetId, Guid utilisateurId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suggestion.ObjetCourrier))
            return null;

        var sens = Normaliser(suggestion.SensCourrier ?? string.Empty).StartsWith("sort", StringComparison.Ordinal)
            ? SensCourrier.Sortant
            : SensCourrier.Entrant;

        var courrier = await _courriers.CreerAsync(
            projetId,
            sens,
            suggestion.ExpediteurCourrier ?? string.Empty,
            suggestion.DestinataireCourrier ?? string.Empty,
            suggestion.ObjetCourrier,
            DateOnly.FromDateTime(DateTime.Today),
            suggestion.DelaiReponseJours is > 0 ? suggestion.DelaiReponseJours.Value : null,
            utilisateurId,
            ct);
        return courrier.Id;
    }

    private async Task<Guid?> CreerTacheAsync(
        SuggestionAssistant suggestion, Guid projetId, Guid utilisateurId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(suggestion.LibelleTache))
            return null;

        var debut = ParseDate(suggestion.DateDebutTache) ?? DateOnly.FromDateTime(DateTime.Today);
        var fin = ParseDate(suggestion.DateFinTache) ?? debut.AddDays(1);
        if (fin < debut)
            fin = debut;

        var tache = await _planification.CreerTacheAsync(
            projetId, null, suggestion.LibelleTache, debut, fin, utilisateurId, ct);
        return tache.Id;
    }

    private static DateOnly? ParseDate(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return null;

        var nettoye = texte.Trim();
        if (DateOnly.TryParse(nettoye, CultureFr, DateTimeStyles.None, out var date))
            return date;
        if (DateOnly.TryParse(nettoye, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return date;
        return null;
    }

    private static TypeSuggestionAssistant MapperTypeAction(string texte) =>
        Normaliser(texte) switch
        {
            "creerreserve" or "reserve" => TypeSuggestionAssistant.CreerReserve,
            "creercourrier" or "courrier" => TypeSuggestionAssistant.CreerCourrier,
            "creertache" or "tache" => TypeSuggestionAssistant.CreerTache,
            _ => TypeSuggestionAssistant.Aucune
        };

    private static string Normaliser(string texte)
    {
        var formeD = texte.Normalize(NormalizationForm.FormD);
        var nettoye = new StringBuilder(formeD.Length);
        foreach (var caractere in formeD)
        {
            var categorie = CharUnicodeInfo.GetUnicodeCategory(caractere);
            if (categorie == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(caractere))
                nettoye.Append(char.ToLowerInvariant(caractere));
        }
        return nettoye.ToString();
    }

    private static string? Propre(string? texte) =>
        string.IsNullOrWhiteSpace(texte) ? null : texte.Trim();

    private static string FormaterMontant(decimal montant) =>
        $"{montant.ToString("N2", CultureFr)} DA";

    private static string FormaterDate(DateOnly? date) =>
        date?.ToString("dd/MM/yyyy", CultureFr) ?? "non renseignée";

    private static string Tronquer(string texte, int longueur) =>
        texte.Length <= longueur ? texte : texte[..longueur] + " […]";

    private sealed record ContexteAssistant(
        string Texte,
        IReadOnlyList<ExtraitDocument> Extraits,
        IReadOnlyList<CalculSysteme> Calculs);

    private const string SchemaReponse = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["reponse", "suggestions"],
          "properties": {
            "reponse": {
              "type": "string",
              "description": "Réponse en français avec préfixes [DOC], [CALCUL] ou [SUGGESTION] en début de ligne."
            },
            "suggestions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": [
                  "titre", "description", "type_action",
                  "description_reserve", "priorite_reserve", "delai_levee_jours",
                  "sens_courrier", "expediteur_courrier", "destinataire_courrier",
                  "objet_courrier", "delai_reponse_jours",
                  "libelle_tache", "date_debut_tache", "date_fin_tache"
                ],
                "properties": {
                  "titre": { "type": "string" },
                  "description": { "type": "string" },
                  "type_action": { "type": "string" },
                  "description_reserve": { "type": "string" },
                  "priorite_reserve": { "type": "string" },
                  "delai_levee_jours": { "type": "number" },
                  "sens_courrier": { "type": "string" },
                  "expediteur_courrier": { "type": "string" },
                  "destinataire_courrier": { "type": "string" },
                  "objet_courrier": { "type": "string" },
                  "delai_reponse_jours": { "type": "number" },
                  "libelle_tache": { "type": "string" },
                  "date_debut_tache": { "type": "string" },
                  "date_fin_tache": { "type": "string" }
                }
              }
            }
          }
        }
        """;
}
