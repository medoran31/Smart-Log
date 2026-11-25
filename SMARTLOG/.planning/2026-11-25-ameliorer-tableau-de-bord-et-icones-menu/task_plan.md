# Task Plan: Améliorer le tableau de bord et les icônes du menu SMARTLOG

## Goal

Le module « Tableau de bord » affiche un écran complet et lisible avec les indicateurs du projet actif, et les icônes du volet latéral sont des glyphes clairs, bien contrastés sur le fond sombre.

## Next Step

Livrer le résumé des changements à l'utilisateur (toutes les phases techniques sont terminées).

## Current Phase

Phase 3 — Implémentation

## Phases

### Phase 1: Requirements & Discovery

- [x] Comprendre la demande (tableau de bord vierge, icônes sombres)
- [x] Inspecter `MainWindow.xaml`, `MainViewModel`, `VueTableauDeBoard.xaml`, services
- [x] Reproduire l'échec de build (`&` non échappé en XAML)
- [x] Documenter les findings
- **Status:** complete

### Phase 2: Planning & Structure

- [x] Choisir l'approche : glyphes Segoe MDL2 au lieu d'emoji, câblage MVVM complet
- [x] Vérifier les codes Unicode des glyphes auprès de Microsoft Learn
- [x] Arrêter la liste des propriétés du ViewModel et les ressources XAML nécessaires
- **Status:** complete

### Phase 3: Implementation

- [x] Ajouter le convertisseur inverse et la ressource dans `App.xaml`
- [x] Réécrire `TableauDeBoardViewModel.cs` (hérite `ViewModelBase`, données réelles, états vides corrigés)
- [x] Ajouter `VueTableauDeBoard.xaml.cs` et réécrire la vue XAML (entités échappées, styles, glyphes)
- [x] Enregistrer/injecter le ViewModel et ajouter le cas « Tableau de bord » dans `MainViewModel`
- [x] Remplacer les emoji du menu par des glyphes MDL2 et styliser la sélection
- **Status:** complete

### Phase 4: Testing & Verification

- [x] `dotnet build SMARTLOG.sln` sans erreur (0 avertissement, 0 erreur)
- [x] `dotnet test SMARTLOG.sln` : 62 tests réussis
- [x] Lancement réel de l'application : aucune exception XAML/binding (stderr vide)
- [x] Correction du binding TwoWay sur `TauxAvancement` (ProgressBar) détecté au lancement
- **Status:** complete

### Phase 5: Delivery

- [x] Relire les fichiers modifiés
- [x] Résumer les changements à l'utilisateur
- **Status:** complete

## Key Questions

1. Comment déterminer le « projet actif » ? → `ProjetsViewModel.ProjetSelectionne`, avec repli sur le premier projet de la liste.
2. Quelle police pour les icônes ? → `Segoe MDL2 Assets`, monochrome et colorisable (contrairement aux emoji).
3. Faut-il créer de nouveaux calculs métier ? → Non : réutilisation de `ISituationService.ObtenirSuiviAsync` et `IPlanificationService.ObtenirAsync`.

## Decisions Made

| Decision | Rationale |
|----------|-----------|
| Remplacer les emoji du menu par des glyphes MDL2 avec `Foreground` clair | Les emoji sont foncés/noirs sur le fond `#1B2733` et leur couleur n'est pas contrôlable en WPF. |
| Brancher le tableau de bord existant plutôt que créer une nouvelle vue | `VueTableauDeBoard.xaml` et son ViewModel existent déjà ; ils étaient simplement inachevés. |
| Faire hériter `TableauDeBoardViewModel` de `ViewModelBase` | `MainViewModel.VueCourante` exige ce type ; evite deux implémentations de `INotifyPropertyChanged`. |
| Ajouter un style `ListBoxItem` avec surlignage de sélection | Le menu sélectionné était invisible sur fond sombre. |
| Corriger les drapeaux « aucun… » inversés et les bindings orphelins | Sinon l'état vide s'affiche à contresens et les valeurs financières restent vides. |

## Errors Encountered

| Error | Attempt | Resolution |
|-------|---------|------------|
| `MC3000 : XML 'An error occurred while parsing EntityName'` (VueTableauDeBoard.xaml l. 69, `&` brut) | 1 | Réécriture du XAML avec `&amp;` et suppression des références de ressources inexistantes. |
| `IProjetService.ObtenirProjetActif()` inexistant (3 appels) | 1 | Remplacement par `ProjetsViewModel.ProjetSelectionne` + repli sur `Projets.FirstOrDefault()`. |
| `InvalidOperationException` au lancement : liaison TwoWay sur `TauxAvancement` (lecture seule) | 1 | `Mode=OneWay` ajouté au binding de `ProgressBar.Value`. |

## Notes

- Palette conservée : fond `#F4F6F8`, menu `#1B2733`, accent `#2E7D6B`, texte clair `#FFFFFF`.
- Codes MDL2 utilisés (vérifiés) : E80F, E8B7, E8FC, E8A5, E70F, E8C7, E822, E7BA, E787, E9D2, E8F1, E715, E91C, E99A, E713, E9D5, E945.
