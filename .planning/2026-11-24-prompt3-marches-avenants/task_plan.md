# Task Plan : Prompt 3 — Marchés, contrats et avenants

## Goal

Livrer les écrans « Marché et contrats » et « Avenants » (import de pièces,
bordereau de prix unitaires, simulation avant validation, branchement sur
IAvenantService) avec des tests unitaires prouvant qu'une validation suivie
d'une annulation restaure exactement le montant et le délai initiaux du projet.

## Next Step

Étendre IAvenantService (simulation, création, liste, recalcul robuste) et
créer IMarcheService (bordereau + import de pièces), puis compiler.

## Current Phase

Phase 4 terminée → Phase 5 : Vérification finale

## Phases

### Phase 1 : Découverte et cadrage
- [x] Lire Prompt 0, README et l'intégralité du code existant
- [x] Confirmer l'état : prompting 0/1/2 livrés, prompt 3 à réaliser
- [x] Vérifier SDK .NET et disponibilité NuGet locale (xUnit, EF SQLite)
- [x] Compiler la solution de départ (0 erreur)
- **Status:** complete

### Phase 2 : Services métier (marché + avenants)
- [x] Étendre IAvenantService : simulation sans écriture, création brouillon, liste, recalcul depuis les avenants validés
- [x] Corriger AnnulerAsync pour recalculer aussi la date prévisionnelle
- [x] Créer IMarcheService/MarcheService : BPU (ajout/modification/suppression) et import de pièces PDF/Word/Excel journalisés
- [x] Enregistrer les services dans App.xaml.cs
- [x] `dotnet build` après l'étape (0 erreur)
- **Status:** complete

### Phase 3 : Projet de tests unitaires
- [x] Créer SMARTLOG.Tests (xUnit + SQLite en mémoire, migration Initial appliquée)
- [x] Tests : valider puis annuler un avenant restaure montant/délai exacts (cas simple et cas multi-avenants)
- [x] Tests : simulation ne modifie pas le projet ; journalisation des écritures
- [x] Tests : bordereau, import de pièce et refus des formats non autorisés
- [x] `dotnet test` vert (7/7)
- **Status:** complete

### Phase 4 : Interfaces WPF
- [x] VueMarches + MarchesViewModel (pièces, BPU, import)
- [x] VueAvenants + AvenantsViewModel (liste, formulaire, simulation temps réel, Valider/Annuler)
- [x] Câblage dans MainViewModel et DataTemplates de MainWindow
- [x] `dotnet build` après l'étape (0 erreur)
- **Status:** complete

### Phase 5 : Vérification finale
- [x] Build complet sans erreur ni avertissement
- [x] Tests unitaires verts (7/7)
- [x] Contrôle des règles du Prompt 0 (calculs centralisés, journalisation, français)
- [x] Lancement de l'application WPF vérifié (processus actif après 6 s)
- **Status:** complete
