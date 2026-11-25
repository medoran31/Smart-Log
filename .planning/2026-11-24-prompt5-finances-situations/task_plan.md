# Task Plan : Prompt 5 — Attachements, situations et suivi financier

## Goal

Livrer `IAttachementService` (quantités exécutées, montant calculé, reliquat
contractuel et alerte de dépassement), étendre `ISituationService` (regroupement
par période, cumul, retenue de garantie, avance, pénalités), ajouter les écrans
« Attachements et situations » et « Finances » avec un graphique d'évolution
engagé / payé / restant dû, et des tests unitaires sur cumul et retenue.

## Next Step

Prompt 5 terminé. Prochaine étape : Prompt 6 (à confirmer avec l'utilisateur).

## Phases

### Phase 1 : Cadrage
- [x] Lire les entités `Attachement`, `LigneAttachement`, `Situation`, les enums et le service de situations existant
- [x] Constat : `MontantCalcule` existe déjà mais aucun service ne l'écrit ; aucune relation Situations↔Attachements → regroupement par `Periode` « AAAA-MM » vs `Attachement.Date`
- [x] Constat : `MontantCumule` est déjà un cumul à date dans le seeder (35 % puis 60 %) → conserver cette sémantique
- [x] Aucune migration nécessaire (entités et champs existants)
- **Status:** complete

### Phase 2 : Service des attachements
- [x] `IAttachementService`/`AttachementService` : création, ajout/modification/suppression de lignes, `MontantCalcule = QuantiteExecutee * PrixUnitaireHT`, reliquat et indicateur de dépassement, changement de statut
- [x] Journalisation de toutes les écritures via `IJournalService` (nouveaux libellés traduits)
- [x] Enregistrement DI + `dotnet build` (0 erreur)
- **Status:** complete

### Phase 3 : Service des situations
- [x] Étendre `ISituationService` : création/recalcul par période depuis les attachements validés, cumul à date, retenue, avance, pénalités, net à payer, changement de statut, lecture du suivi financier
- [x] Conserver `InitialiserAsync` existant (seed) et centraliser les calculs dans le service
- [x] `dotnet build` (0 erreur)
- **Status:** complete

### Phase 4 : Interfaces
- [x] `VueAttachements`/`AttachementsViewModel` : saisie des quantités, aperçu calculé, lignes en rouge si dépassement du reliquat, validation et création de situation
- [x] `VueFinances`/`FinancesViewModel` : graphique d'évolution (engagé / payé / restant dû) dessiné en WPF natif, grille des situations et indicateurs
- [x] Câblage menu, DataTemplates et DI
- [x] `dotnet build` (0 erreur/0 avertissement)
- **Status:** complete

### Phase 5 : Tests et vérification
- [x] Tests : calcul par ligne, modification journalisée, dépassement de reliquat (+ aperçu sans écriture), cumul sur deux périodes, retenue de garantie/idempotence, retenue + avance + pénalités + net, attachement non validé exclu, suivi engagé/payé/restant dû
- [x] `dotnet test` vert (23/23)
- [x] Lancement WPF vérifié (processus actif après 8 s) et build 0 erreur/0 avertissement
- [x] Contrôle des règles du Prompt 0 (calculs centralisés dans les services, écritures journalisées, français)
- **Status:** complete
