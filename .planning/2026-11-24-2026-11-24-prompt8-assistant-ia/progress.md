# Progress

## 2026-11-24 — Prompt 8 terminé

- Cadrage terminé : API Messages (`output_config.format`), en-têtes d'authentification
  et de workspace, structured outputs, relevé des signatures des services et du
  placeholder « Assistant IA ».
- Configuration locale `ConfigurationAssistantIa` (%LOCALAPPDATA%\Smartlog\assistant-ia.json
  + variables `SMARTLOG_CLAUDE_*`) et client HTTP injectable `ClientClaude`.
  La clé n'est ni codée en dur, ni journalisée, ni exposée dans les exceptions.
- `AssistantIaService` : assemblage du contexte projet (données + calculs + extraits OCR),
  invitation système imposant `[DOC]` / `[CALCUL]` / `[SUGGESTION]`, réponse JSON
  structurée, suggestions inactives jusqu'à validation. Application des trois actions
  (réserve, courrier, tâche) via les services dédiés, avec journalisation.
- Interface : `AssistantIaViewModel` + `VueAssistantIa` (conversation, zones de
  provenance séparées, suggestions avec « Valider et appliquer » / « Ignorer »),
  DI, menu, DataTemplate, propagation de projet et rafraîchissement croisé.
- Documentation : section « Assistant IA » ajoutée au README pour la configuration
  de la clé.
- Tests : 8 nouveaux tests (provenance, absence d'écriture avant clic, trois types
  de suggestion, ignorage, marqueurs, clé absente). `dotnet test` : 41/41.
- Vérification UI Automation : navigation vers « Assistant IA » et présence des
  zones de conversation, de provenance, de suggestions et des deux boutons confirmées.
- État final : build 0 avertissement / 0 erreur, tests 41/41, UI vérifiée.
  Prochaine étape : Prompt 9.
