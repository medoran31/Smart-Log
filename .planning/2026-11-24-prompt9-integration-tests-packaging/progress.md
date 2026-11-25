# Progress Log — Prompt 9

## Session: 2026-11-24

### Phase 1 : Audit

- **Status:** complete
- Actions :
  - Inventaire des services, interfaces, VMs et vues.
  - Cartographie des valeurs dérivées et des écritures multiples.
  - Audit des tests existants (41 tests), packaging, sauvegarde et docs.
- Résultat : voir `findings.md`.

### Phase 2 : Cohérence et doublons

- **Status:** complete
- Actions :
  - Ajout de `IAvenantService.RecalculerValeursDeriveesAsync` et de
    `IAttachementService.RecalculerLignesAsync`.
  - Création de `IRecalculCroiseService` / `RecalculCroiseService` avec
    préservation des taux historiques.
  - Injection dans `ProjetService`, `MarcheService` et les VMs concernées ;
    enregistrement DI dans `App.xaml.cs`.
  - Rechargement des modules dépendants dans `MainViewModel`.
  - Ajout des libellés de journal manquants.
- Fichiers : `src/SMARTLOG.Services/*`, `src/SMARTLOG.App/App.xaml.cs`,
  `src/SMARTLOG.App/ViewModels/*`, `src/SMARTLOG.App/Views/MainWindow.xaml`.

### Phase 3 : Couverture de tests

- **Status:** complete
- Actions :
  - Ajout de `RecalculCroiseServiceTests` (5 tests).
  - Ajout de `CouvertureServicesTests` (8 tests).
  - Ajout de `ClientClaudeHttpTests` (3 tests, handler HTTP factice).
  - Ajustement de `SqliteTestBase` pour les nouvelles dépendances.
- Résultat : 62 tests réussis, 0 échec.

### Phase 4 : Packaging

- **Status:** complete
- Actions :
  - Profil `Properties/PublishProfiles/win-x64-selfcontained.pubxml`.
  - Version/AssemblyName/RuntimeIdentifier dans `SMARTLOG.App.csproj`.
  - Scripts `scripts/publish.ps1`, `install.ps1`, `uninstall.ps1`.
  - `dotnet publish` exécuté avec succès.

### Phase 5 : Sauvegarde / restauration

- **Status:** complete
- Actions :
  - `ISauvegardeService.cs` et `SauvegardeService.cs` (VACUUM INTO, GED,
    intégrité, restauration différée au démarrage avec copie de sécurité).
  - Application de la restauration avant migrations dans `App.xaml.cs`.
  - `SauvegardeViewModel.cs` + `VueSauvegarde.xaml(.cs)` branchés sur
    « Paramètres » (DI, `MainViewModel`, `MainWindow.xaml`).
  - `SauvegardeServiceTests.cs` (5 tests).

### Phase 6 : Guide utilisateur

- **Status:** complete
- Actions : rédaction de `docs/guide-utilisateur.md` (15 menus, installation,
  sauvegarde/restauration, configuration Claude, limites), refonte du README.

### Phase 7 : Vérification finale

- **Status:** complete
- Actions :
  - Build solution : 0 avertissement, 0 erreur.
  - Tests : 62 réussis, 0 échec.
  - `dotnet publish` régénéré (EXE autonome daté du correctif).
  - PowerShell : 0 erreur de syntaxe dans `publish.ps1`, `install.ps1`,
    `uninstall.ps1`.
  - UIAutomation (`verify_prompt9.ps1`) : fenêtre « SMARTLOG », 15 éléments de
    menu ; « Paramètres » (titre, créer/restaurer/supprimer, emplacements,
    liste, notice de démarrage, clé Claude) ; projets, planning (CPM), centre
    d'alertes (Analyser), finances (graphique). Tous les contrôles : True.
  - Diagnostic et correction d'un interblocage au démarrage causé par
    `SauvegardeService` (voir `findings.md`).

## Test Results

| Test | Input | Expected | Actual | Status |
|------|-------|----------|--------|--------|
| `dotnet build SMARTLOG.sln --nologo -v q` | solution complète | 0/0 | 0 avertissement, 0 erreur | PASS |
| `dotnet test SMARTLOG.sln --nologo -v q` | 62 tests | 0 échec | 62 réussis, 0 échec | PASS |
| `dotnet publish ... -p:PublishProfile=win-x64-selfcontained` | profil release | EXE autonome | `SMARTLOG.App.exe` + `includedFrameworks` | PASS |
| UIAutomation menu « Paramètres » | lancement de l'app WPF | vue sauvegarde affichée | 8/8 contrôles True | PASS |
| UIAutomation modules | navigation Projets/Planning/Alertes/Finances | vues et boutons présents | tous True | PASS |
| Scripts PowerShell | analyse syntaxique | 0 erreur | 3/3 à 0 erreur | PASS |
| `dotnet publish` | profil `win-x64-selfcontained` | EXE autonome | EXE présent et régénéré | PASS |

## Error Log

| Timestamp | Error | Attempt | Resolution |
|-----------|-------|---------|------------|
| 2026-11-24 | `CS0815` : `void` assigné dans `RecalculCroiseService` | 1 | `ActualiserAlertesAsync` retourne `Task<CentreNotifications>` |
| 2026-11-24 | `CS7036` : nouvelles dépendances `ProjetService`/`MarcheService` dans `SqliteTestBase` | 1 | Ordre d'instanciation corrigé, `Recalcul` exposé |
| 2026-11-24 | Test taux avance : cumul à 0 | 1 | Attachement validé avant création de situation (seuls les attachements validés alimentent le cumul) |
| 2026-11-24 | Test journal recalcul croisé : libellé absent | 1 | Journal lu sur l'entité `Situation` (et non `Projet`) |
| 2026-11-24 | `ObjectDisposedException` sur le corps HTTP | 1 | Corps capturé dans le handler factice avant la fin de la requête |
| 2026-11-24 | Copie corrompue : `SqliteException 26` au lieu d'un message métier | 1 | `VerifierIntegriteAsync` convertit l'exception en statut `invalide (...)` |
| 2026-11-24 | Verrou Windows sur la base de test | 1 | `Pooling=False` dans le service et les helpers de test |
| 2026-11-24 | Journal recalcul planning introuvable sur `Tache` | 1 | Journal vérifié sur `Projet` (entité propriétaire du recalcul) |
| 2026-11-24 | Application démarrant sans fenêtre (UIAutomation `WINDOW_NOT_FOUND`) | 1 | Interblocage identifié : `ListerAsync().GetResult()` dans le constructeur de `SauvegardeViewModel` + `File.ReadAllTextAsync` sans `ConfigureAwait(false)`. Corrigé par `ConfigureAwait(false)` sur tous les awaits de `SauvegardeService`. |
| 2026-11-24 | Contrôles UIAutomation intermittents (accents, délais de rendu) | 2 | Sondage avec timeout dans `Find-Text`, libellés ASCII et attente de 2,5 s par changement de menu |

## 5-Question Reboot Check

| Question | Answer |
|----------|--------|
| Where am I? | Phase 7 (vérification finale) |
| Where am I going? | Clôture du plan Prompt 9 |
| What's the goal? | Prompt 9 complet et vérifié |
| What have I learned? | Voir `findings.md` |
| What have I done? | Voir le détail des phases ci-dessus |
