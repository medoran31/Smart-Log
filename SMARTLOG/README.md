# SMARTLOG — suivi de chantier (WPF / .NET 8)

Application Windows mono-poste de suivi de chantier : projets, marchés,
avenants, attachements et situations, finances, planning CPM, réserves, GED,
courriers, alertes, assistant IA et sauvegarde/restauration.

## Architecture

```
SMARTLOG.sln
src/
  SMARTLOG.Domain/     → entités et énumérations métier, sans dépendance externe
  SMARTLOG.Data/        → EF Core + SQLite, DbContext et migrations
  SMARTLOG.Services/    → toutes les règles de calcul et les écritures métier
  SMARTLOG.App/         → application WPF (MVVM : UserControl + ViewModel)
  SMARTLOG.Tests/       → tests unitaires et d'intégration xUnit
docs/guide-utilisateur.md → guide complet en français
scripts/               → publication et installation Windows
```

Règle structurante : **aucun calcul dans l'interface**. Chaque valeur dérivée
appartient à un seul service et toute écriture passe par `IJournalService`.
La chaîne de recalcul croisé est orchestrée par `IRecalculCroiseService` :
avenant → finances → planning → alertes.

## Modules

Le menu latéral comporte 15 entrées :

`Tableau de bord`, `Mes projets`, `Fiche projet`, `Marché et contrats`,
`Avenants`, `Attachements et situations`, `Suivi technique`, `Réserves`,
`Planning`, `Finances`, `Documents`, `Courrier`, `Centre d'alertes`,
`Assistant IA`, `Paramètres`.

« Tableau de bord » et « Suivi technique » sont encore des placeholders ; les
13 autres modules disposent de leur `UserControl` et de leur `ViewModel`.
« Paramètres » héberge la sauvegarde/restauration de la base et des documents.

## Construction et tests

Avec le SDK .NET 8 installé, depuis ce dossier :

```powershell
dotnet build SMARTLOG.sln --nologo
dotnet test SMARTLOG.sln --nologo
```

Les tests couvrent les services de calcul (avenants, attachements, situations,
CPM, finances, réserves, courriers, GED, projets), les recalculs croisés, le
client HTTP Claude (avec handler factice), la sauvegarde/restauration SQLite
et les parcours métier.

## Installation Windows

La publication est autonome (`win-x64`, `--self-contained`) : le runtime .NET
n'est pas requis sur le poste cible.

```powershell
.\scripts\publish.ps1
.\scripts\install.ps1          # ajoutez -DesktopShortcut pour le bureau
```

Résultat : `src\SMARTLOG.App\bin\Release\net8.0-windows\win-x64\publish\SMARTLOG.App.exe`.
Installation par défaut dans `%LOCALAPPDATA%\Programs\SmartLog`, avec raccourci
dans le menu Démarrer. Désinstallation : `.\scripts\uninstall.ps1`
(`-RemoveUserData` supprime aussi base, documents et configuration).

La base est créée automatiquement au premier lancement dans
`%LocalAppData%\Smartlog\smartlog.db`, avec migrations EF Core et données de
démonstration.

## Assistant IA — configuration de la clé Claude

La clé API n'est **jamais** écrite dans le code ni journalisée. Créez le fichier
local `%LocalAppData%\Smartlog\assistant-ia.json` :

```json
{
  "CleApi": "sk-ant-...",
  "Modele": "claude-opus-5-5",
  "VersionApi": "2026-10-21",
  "IdentifiantEspaceTravail": "wrkspc_..."
}
```

`Modele`, `VersionApi` et `IdentifiantEspaceTravail` sont facultatifs. Les
variables d'environnement `SMARTLOG_CLAUDE_API_KEY`,
`SMARTLOG_CLAUDE_MODELE`, `SMARTLOG_CLAUDE_VERSION_API` et
`SMARTLOG_CLAUDE_WORKSPACE_ID` prennent le pas sur le fichier.

Le panneau « Assistant IA » assemble la provenance ([DOC], [CALCUL],
[SUGGESTION]) et n'applique aucune suggestion sans clic explicite sur
« Valider et appliquer ».

Voir le [guide utilisateur](docs/guide-utilisateur.md) pour l'usage de chaque
module, la sauvegarde/restauration et les limites connues.
