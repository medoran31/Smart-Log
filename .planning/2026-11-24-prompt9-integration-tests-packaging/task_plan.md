# Task Plan : Prompt 9 — Intégration, tests et packaging

## Goal

1. Éliminer les doublons de données et vérifier la cohérence des recalculs
   croisés (avenant → finances → planning).
2. Compléter la couverture de tests unitaires sur tous les services de calcul.
3. Configurer une publication Windows installable (`dotnet publish
   --self-contained` retenu par défaut).
4. Vérifier le système de sauvegarde/restauration du fichier SQLite.
5. Rédiger un guide utilisateur en français couvrant chaque module.

## Next Step

Aucune : toutes les phases sont terminées et vérifiées.

## Phases

### Phase 1 : Audit
- [x] Cartographie des doublons et des valeurs dérivées écrites à plusieurs endroits
- [x] Graphe des recalculs croisés et staleness UI
- [x] Inventaire des tests par service de calcul
- [x] État du packaging, des sauvegardes SQLite et de la documentation existante
- **Status:** complete

### Phase 2 : Cohérence et doublons
- [x] `IAvenantService.RecalculerValeursDeriveesAsync` : point d'écriture unique du montant/délai/date prévisionnelle
- [x] `IAttachementService.RecalculerLignesAsync` appelé par `MarcheService.ModifierPrixUnitaireAsync`
- [x] `IRecalculCroiseService` : orchestration avenant → situations → planning → alertes, avec préservation des taux historiques
- [x] Injection et câblage UI (`App.xaml.cs`, `AvenantsViewModel`, `AttachementsViewModel`, `PlanningViewModel`, `ReservesViewModel`, `CourriersViewModel`, `MainViewModel`)
- [x] `SituationService.InitialiserAsync` : `tauxAvance` configurable, appel démonstration corrigé
- [x] Libellés de journal ajoutés
- [x] `dotnet build` 0 avertissement / 0 erreur
- **Status:** complete

### Phase 3 : Couverture de tests
- [x] Tests complémentaires sur les services de calcul (avenant, situation, attachement, planning, réserve, courrier, GED, projet, client Claude HTTP)
- [x] 5 tests d'intégration des recalculs croisés
- [x] `dotnet test` vert : 62 réussis / 0 échec
- **Status:** complete

### Phase 4 : Packaging
- [x] Profil `win-x64-selfcontained.pubxml` (`--self-contained`, `RuntimeIdentifier win-x64`)
- [x] Scripts `publish.ps1`, `install.ps1`, `uninstall.ps1`
- [x] Publication réelle vérifiée : `SMARTLOG.App.exe` + runtime inclus (`includedFrameworks`)
- **Status:** complete

### Phase 5 : Sauvegarde / restauration SQLite
- [x] Audit : aucun mécanisme existant
- [x] `ISauvegardeService` / `SauvegardeService` : `VACUUM INTO`, GED, métadonnées, `PRAGMA integrity_check`
- [x] Restauration programmée appliquée au démarrage avant migrations, avec copie de sécurité
- [x] UI « Paramètres » : `SauvegardeViewModel` + `VueSauvegarde`
- [x] 5 tests de sauvegarde/restauration
- **Status:** complete

### Phase 6 : Guide utilisateur
- [x] `docs/guide-utilisateur.md` : installation, 15 menus, actions, sauvegarde/restauration, configuration Claude, limites
- [x] README mis à jour (15 modules, build/tests, publication, lien vers le guide)
- **Status:** complete

### Phase 7 : Vérification finale
- [x] Build 0 avertissement / 0 erreur
- [x] 62 tests réussis / 0 échec
- [x] Publication autonome régénérée après le correctif de verrouillage
- [x] UIAutomation : 15 menus, Paramètres (8 contrôles), Projets, Planning, Alertes, Finances
- [x] Correction d'un interblocage au démarrage (`ConfigureAwait(false)` dans `SauvegardeService`)
- [x] Validation syntaxique des 3 scripts PowerShell
- **Status:** complete
