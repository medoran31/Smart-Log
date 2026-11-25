# Progress

## 2026-11-24 — Démarrage

- Découverte du workspace, extraction des prompts depuis `Prompt 0.docx`.
- Confirmation que les prompts 0, 1 et 2 sont réalisés et compilent.
- Plan créé pour le prompt 3 : services, tests, interfaces, vérification.
- Prochaine action : étendre `IAvenantService` et créer `IMarcheService`.

## 2026-11-24 — Services et tests

- `IAvenantService` étendu (liste, simulation sans écriture, création brouillon) ; `ValiderAsync`/`AnnulerAsync` recalculent désormais depuis `MontantInitial + somme des avenants validés`, ce qui garantit un retour exact aux valeurs initiales et recalcule la date prévisionnelle.
- `IMarcheService`/`MarcheService` créés : BPU (ajout/modification/suppression), total calculé dans le service, import PDF/Word/Excel avec stockage local et journalisation.
- Services enregistrés dans `App.xaml.cs`. Build vert.
- Projet `SMARTLOG.Tests` créé (xUnit, SQLite en mémoire avec migration Initial) ; 7 tests verts, dont validation→annulation (cas simple et multi-avenants) et simulation sans écriture.
- Deux bugs corrigés pendant les tests : `DefaultIfEmpty().MaxAsync()` non traduit par EF Core SQLite et statut non persisté avant le recalcul.

## 2026-11-24 — Interfaces

- `VueMarches`/`MarchesViewModel` : pièces importées (double-clic pour ouvrir), BPU éditable, total fourni par le service.
- `VueAvenants`/`AvenantsViewModel` : liste à gauche, formulaire et simulation temps réel à droite, boutons branchés sur `IAvenantService`.
- Navigation mise à jour (`MainViewModel`, `MainWindow.xaml`, DI) ; build vert.

## 2026-11-24 — Vérification finale

- Build complet : 0 erreur, 0 avertissement.
- Tests : 7/7 réussis.
- Application WPF lancée sans exception (processus toujours actif après 6 s).
- Règles du Prompt 0 respectées : calculs uniquement dans les services (simulation incluse), écritures journalisées, libellés et messages en français.
- Prompt 3 terminé ; prêt pour le Prompt 4 (GED et moteur de modèles).
