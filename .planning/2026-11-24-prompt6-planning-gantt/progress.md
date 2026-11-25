# Progress

## 2026-11-24 — Démarrage Prompt 6

- Cadrage des entités `Tache`/`LiaisonTache`, du DbContext et de la migration : aucune migration nécessaire.
- Décision CPM : jours calendaires inclusifs, 4 types de liaisons, ancrage des tâches racines sur leur date saisie, contrainte imposée à l'opération pour un changement de date sur une tâche liée.
- Plan créé ; prochaine action : service de planification et moteur CPM.

## 2026-11-24 — Service et moteur CPM

- `CpmCalculateur` (interne à `SMARTLOG.Services`) : tri topologique de Kahn (détection de cycles), passage avant (ES/EF), passage arrière (LS/LF), marge totale et chemin critique. Gère Fin→Début, Début→Début, Fin→Fin et Début→Fin avec des durées en jours calendaires inclusifs.
- `IPlanificationService`/`PlanificationService` : création/modification/suppression de tâches WBS, ajout/suppression de liaisons (anti-doublon, anti-auto-liaison, projets cohérents), `RecalculerAsync` (persistance des dates et du flag critique), `ObtenirAsync` (lecture) et `CalculerAsync` (aperçu CPM sans écriture). Une modification de durée ou de date impose la nouvelle date à l'opération puis propage en cascade via le passage avant.
- Journalisation : `ModificationPlanning`, `SuppressionTache`, `AjoutLiaison`, `SuppressionLiaison`, `RecalculPlanning`.
- Build : 0 erreur/0 avertissement.

## 2026-11-24 — Vue Gantt et vérification

- `PlanningViewModel` : WBS aplatie en pré-ordre avec niveaux, arborescence et barres partageant le même `NoeudTache` (sélection synchronisée dans les deux sens), échelle mensuelle, KPI durée/nombre critique, formulaires de tâches et de liaisons. Le positionnement des barres est purement graphique ; les dates et marges viennent du service.
- `VuePlanning.xaml` : arborescence à gauche, diagramme natif `Canvas`/`ItemsControl` à droite, défilement horizontal, chemin critique rouge et tâches normales vertes, infobulles, légende. Aucune dépendance Gantt externe.
- `LiaisonResume` enrichi des libellés de tâches pour l'affichage de la grille des liaisons.
- Câblage : DI (`IPlanificationService`, `PlanningViewModel`), menu « Planning », `DataTemplate`, propagation de la sélection de projet.
- Build : 0 erreur/0 avertissement. Tests : 30/30 verts. Navigation UI Automation jusqu'à la vue Gantt : chargée sans exception."})

