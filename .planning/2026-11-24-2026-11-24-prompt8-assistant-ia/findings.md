# Findings — Prompt 8

## API Claude (documentation consultée le 2026-11-24)

- `POST https://api.anthropic.com/v1/messages`.
- En-têtes : `content-type: application/json`, `anthropic-version: 2026-10-21`,
  `Authorization: Bearer <clé>` (`x-api-key` reste supporté en fallback),
  `anthropic-workspace-id` uniquement si la clé n'est pas rattachée à un espace.
- Corps : `model`, `max_tokens`, `messages` (rôles `user`/`assistant`), `system`.
- Structured outputs stables : `output_config.format = { "type": "json_schema", "schema": {...} }` ;
  la réponse JSON conforme est dans le bloc de texte. Types JSON pris en charge :
  `string`, `number`, `boolean`, `array`, `object`, `enum` — les champs numériques
  sont donc modélisés en `number` puis convertis en `int` côté SMARTLOG.
- Modèle par défaut : `claude-opus-5-5`, configurable localement.
- Erreurs HTTP : 400/401/429 etc. avec corps `{ "error": { "message": ... } }` ;
  le message est remonté sans jamais inclure la clé.

## Décisions de conception retenues

- La provenance est contrôlée par SMARTLOG, pas par le modèle : les extraits
  OCR et les calculs sont assemblés par `AssistantIaService` à partir des
  services métier, puis affichés dans des zones distinctes. Claude fournit la
  réponse et les suggestions, jamais l'origine des données.
- Le système prompt impose les préfixes `[DOC]`, `[CALCUL]`, `[SUGGESTION]` en
  début de ligne ; si le modèle ne les utilise pas, le service qualifie
  automatiquement sa réponse comme `[SUGGESTION]`.
- `IClientClaude` injectable permet de tester sans réseau ni clé.
- Configuration locale : `%LOCALAPPDATA%\Smartlog\assistant-ia.json` avec
  `CleApi`, `Modele`, `VersionApi`, `IdentifiantEspaceTravail` ; variables
  `SMARTLOG_CLAUDE_*` en surcharge. Aucun secret dans le code, le journal ou
  les exceptions. Mode d'emploi ajouté au `README.md`.
- `PoserQuestionAsync` est strictement en lecture : aucune écriture métier ni
  journalisation. Seules `AppliquerSuggestionAsync` (création de réserve,
  courrier ou tâche + journalisation) et `IgnorerSuggestionAsync` (trace)
  écrivent, et uniquement après le clic explicite de l'utilisateur.
- Après application, `AssistantIaViewModel.SuggestionAppliquee` notifie
  `MainViewModel` qui recharge les modules touchés, la liste des projets et
  les notifications.
- Contexte transmis : fiche projet, montant/délai actualisés, avancement,
  cumuls financiers, CPM, alertes, réserves ouvertes, courriers en attente et
  jusqu'à 8 extraits OCR (700 caractères chacun).
