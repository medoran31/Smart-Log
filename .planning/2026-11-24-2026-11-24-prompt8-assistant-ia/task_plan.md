# Task Plan : Prompt 8 — Assistant IA

## Goal

Implémenter le panneau « Assistant IA » : conversation contextualisée au projet
ouvert, provenance explicite (extrait de document / calculé par le système /
suggéré par l'IA), suggestions appliquées uniquement après clic sur
« Valider et appliquer », service `IAssistantIaService` appelant l'API Claude
avec une clé lue dans la configuration locale (jamais en dur).

## Next Step

Aucune. Prompt 8 terminé ; prochaine étape : Prompt 9.

## Phases

### Phase 1 : Cadrage
- [x] API Claude actuelle vérifiée (Messages API, `output_config.format`, en-têtes)
- [x] Points d'intégration et signatures des services existants relevés
- [x] Stratégie de configuration locale définie et documentée
- **Status:** complete

### Phase 2 : Service Assistant IA
- [x] Configuration locale + client HTTP injectable (`ClientClaude`)
- [x] `IAssistantIaService`/`AssistantIaService` : contexte projet, provenance séparée, suggestions structurées, journalisation
- [x] `dotnet build` (0 avertissement / 0 erreur)
- **Status:** complete

### Phase 3 : Interface
- [x] `AssistantIaViewModel`/`VueAssistantIa` : conversation, zones de provenance, suggestions avec boutons explicites
- [x] DI, menu, DataTemplate, propagation du projet ouvert, rafraîchissement après application
- [x] `dotnet build` (0 avertissement / 0 erreur)
- **Status:** complete

### Phase 4 : Tests et vérification
- [x] Tests : contexte/provenance, aucune écriture avant clic, application des trois types de suggestion, ignorer, clé absente
- [x] `dotnet test` : 41/41
- [x] Lancement WPF + UI Automation du panneau
- **Status:** complete
