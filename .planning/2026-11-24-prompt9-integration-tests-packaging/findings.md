# Findings — Prompt 9

## Périmètre confirmé

La phrase finale initialement tronquée a été précisée par l'utilisateur :
« vérifie le système de sauvegarde/restauration du fichier SQLite, et rédige un
guide utilisateur en français couvrant chaque module ».

## Hypothèse conservée

Packaging : `dotnet publish --self-contained` + script d'installation, aucun
certificat MSIX disponible (choix proposé, sans réponse de l'utilisateur).

## Audit initial

- Aucun système de sauvegarde/restauration n'existait : base SQLite et
  documents GED étaient sans copie ni contrôle d'intégrité.
- Aucun profil de publication ni script d'installation n'existait.
- 15 entrées de menu ; « Tableau de bord » et « Suivi technique » restent des
  placeholders. Aucune authentification n'est implémentée.
- Doublons de calcul identifiés : BPU (`MarcheService` / `GedService`) et
  valeurs dérivées du projet ; plusieurs VMs ne rechargeaient pas les modules
  dépendants après mutation.
- Couverture initiale : 41 tests dans 8 classes. Lacunes : lecture des
  avenants, suppression de ligne, obtention de situation, recalcul planning,
  modification/liste des réserves, lecture des courriers, GED, projets, client
  Claude HTTP.

## Décisions de conception retenues

- Un seul service propriétaire écrit chaque valeur dérivée.
  `IAvenantService.RecalculerValeursDeriveesAsync` est le point d'entrée
  unique pour montant, délai et date prévisionnelle.
- `RecalculCroiseService` orchestre avenant → finances → planning → alertes,
  sans refaire de calcul : il appelle les services propriétaires.
- Les taux historiques des situations sont préservés via les rapports
  `RetenueGarantie/MontantCumule` et `Avance/MontantCumule`, pas via des
  valeurs par défaut.
- `MarcheService.ModifierPrixUnitaireAsync` recalcule les lignes stockées via
  `IAttachementService.RecalculerLignesAsync`.
- `ProjetService.ModifierAsync` ne déclenche le recalcul complet que si
  `DateCommencement` change.
- `ActualiserAlertesAsync` est utilisé après les mutations de planning,
  réserves et courriers ; `AnalyserAsync` n'est pas appelé depuis
  `MettreAJourNotifications()` (qui journaliserait à chaque ouverture de la
  cloche).
- Sauvegarde : `VACUUM INTO` pour une copie cohérente à chaud, contrôle
  `PRAGMA integrity_check` avant restauration, restauration appliquée au
  démarrage avant les migrations avec conservation de l'ancienne base.
  La clé Claude est exclue des sauvegardes.
- Publication autonome `win-x64` vérifiée par la présence de
  `includedFrameworks` dans `SMARTLOG.App.runtimeconfig.json`.
- **Interblocage au démarrage découvert en vérification UI** : `SauvegardeViewModel`
  appelle `ListerAsync().GetResult()` dans son constructeur (motif déjà utilisé
  par tous les VMs). `SauvegardeService.VersResume` utilise `File.ReadAllTextAsync`,
  dont la continuation était republiée sur le contexte WPF alors que le thread UI
  était bloqué : l'application démarrait sans jamais afficher de fenêtre.
  Correction : `ConfigureAwait(false)` sur tous les `await` de `SauvegardeService`
  (le service est une bibliothèque et ne doit jamais capturer le contexte UI).

## Résultats

- Build : 0 avertissement, 0 erreur.
- Tests : 62 réussis, 0 échec, 0 ignoré.
- Publication : `src/SMARTLOG.App/bin/Release/net8.0-windows/win-x64/publish/SMARTLOG.App.exe`.
- Documentation : `docs/guide-utilisateur.md` et README mis à jour.

## Limites connues documentées

- Deux placeholders (Tableau de bord, Suivi technique).
- Pas d'authentification : usage mono-poste mono-utilisateur.
- GED locale, aucun serveur de documents.
