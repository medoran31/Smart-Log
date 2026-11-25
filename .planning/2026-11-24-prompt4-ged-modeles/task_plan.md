# Task Plan : Prompt 4 — GED et moteur de modèles documentaires

## Goal

Livrer `SMARTLOG.Services.Ged` (import local organisé par projet, OCR Tesseract
avec repli silencieux, recherche plein texte sur OCR + métadonnées), un moteur
transversal `IModeleDocumentService` (génération Word/PDF depuis un modèle à
jetons `{{Projet.Intitule}}`) et l'écran GED (arborescence, grille,
prévisualisation), en respectant les règles du Prompt 0.

## Next Step

Prompt 4 terminé. Prochaine étape : Prompt 5 (à confirmer avec l'utilisateur).

## Phases

### Phase 1 : Cadrage
- [x] Vérifier l'état du code (prompts 0-3 livrés) et les dépendances disponibles
- [x] Constat : Tesseract/pdftoppm absents, aucun package OCR/OpenXML/PDF en cache → adaptateur processus externe + génération DOCX/PDF sans dépendance lourde
- **Status:** complete

### Phase 2 : Service GED + OCR
- [x] `Smartlog.Services.Ged` : `IGedService`/`GedService` (import par projet, versions, journal, recherche)
- [x] `IOcrService`/`TesseractOcrService` (images + PDF via pdftoppm, repli sans OCR)
- [x] Enregistrer les services dans `App.xaml.cs`
- [x] `dotnet build` (0 erreur)
- **Status:** complete

### Phase 3 : Moteur de modèles
- [x] `IModeleDocumentService`/`ModeleDocumentService` : jetons `{{Projet.*}}`, `{{Marche.*}}` et valeurs supplémentaires, jetons non résolus signalés
- [x] Écriture DOCX (zip Open XML minimal) et PDF (PDF 1.4 natif), sans dépendance externe
- [x] Journalisation des documents générés et persistance dans la GED du projet
- [x] `dotnet build` (0 erreur)
- **Status:** complete

### Phase 4 : Écran GED
- [x] `GedViewModel` : arborescence projets/dossiers, grille filtrable, prévisualisation, import multiple, génération depuis modèle
- [x] `VueGed` (TreeView + DataGrid + aperçu texte/image)
- [x] Câblage menu « Documents », DataTemplate et DI
- [x] `dotnet build` (0 erreur)
- **Status:** complete

### Phase 5 : Tests et vérification
- [x] Tests : import sans OCR, OCR disponible, panne OCR tolérée, versions, recherche OCR/métadonnées accent-insensible, génération Word/PDF, jetons non résolus, modèle fichier
- [x] `dotnet test` vert (15/15)
- [x] Lancement WPF vérifié (processus actif après 7 s) et build 0 erreur/0 avertissement
- [x] Contrôle des règles du Prompt 0 (calculs centralisés, journalisation, français)
- **Status:** complete
