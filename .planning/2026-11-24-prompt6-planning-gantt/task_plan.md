# Task Plan : Prompt 6 — Planning et Gantt

## Goal

Livrer `IPlanificationService` (WBS, liaisons, CPM standard 4 types, recalcul
en cascade), une vue Gantt WPF native (arborescence synchronisée, chemin
critique surligné) et des tests unitaires dont un scénario CPM à 4 tâches liées.

## Next Step

Prompt 6 terminé. Prochaine étape : Prompt 7 (à confirmer avec l'utilisateur).

## Phases

### Phase 1 : Cadrage
- [x] Entités `Tache` (ParentTacheId, dates prévues/réelles, AvancementPct, EstCheminCritique) et `LiaisonTache` (FinDebut, DebutDebut, FinFin, DebutFin) déjà présentes
- [x] `DbSet` et migration Initial déjà en place → aucune migration
- [x] Aucun service de planification ; menu « Planning » encore placeholder
- **Status:** complete

### Phase 2 : Service et moteur CPM
- [x] `IPlanificationService`/`PlanificationService` : CRUD tâches WBS, CRUD liaisons (anti-doublon, anti-auto-liaison), détection de cycles
- [x] Moteur CPM dans `SMARTLOG.Services` : tri topologique, passages avant/arrière, 4 types de liaisons, marge totale, `EstCheminCritique`
- [x] Recalcul en cascade sur changement de durée ou de date (contrainte imposée à l'opération)
- [x] Journalisation de toutes les écritures + `dotnet build` (0 erreur)
- **Status:** complete

### Phase 3 : Vue Gantt
- [x] `PlanningViewModel` : WBS aplatie en pré-ordre, sélection partagée arbre ↔ barres, positionnement Canvas, formulaires tâches et liaisons
- [x] `VuePlanning.xaml` : arborescence à gauche, diagramme Canvas/ItemsControl à droite, chemin critique rouge, échelle mensuelle, légende
- [x] Câblage menu, DataTemplate, DI + `dotnet build` (0 erreur/0 avertissement)
- **Status:** complete

### Phase 4 : Tests et vérification
- [x] Tests : CPM 4 tâches liées (marges et chemin critique), cascade sur durée, cascade sur date avec contrainte imposée, cycle rejeté, 4 types de liaisons, WBS, suppression protégée
- [x] `dotnet test` vert (30/30)
- [x] Lancement WPF + navigation UI Automation jusqu'à la vue Gantt (chargée sans exception)
- [x] Contrôle des règles du Prompt 0 (calculs centralisés dans `SMARTLOG.Services`, écritures journalisées, français)
- **Status:** complete
