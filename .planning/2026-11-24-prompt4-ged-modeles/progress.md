# Progress

## 2026-11-24 — Démarrage Prompt 4

- Cadrage de l'état du code et des outils disponibles.
- Décision d'implémenter l'OCR via un adaptateur Tesseract externe avec repli silencieux, et la génération Word/PDF sans package NuGet (DOCX zip minimal + PDF natif).
- Plan créé ; prochaine action : services `Smartlog.Services.Ged`.

## 2026-11-24 — Services GED et OCR

- `Smartlog.Services.Ged` créé : `IGedService`/`GedService` (stockage `%LocalAppData%\Smartlog\Documents\<CodeProjet>\<sous-dossier>`, versions, métadonnées dérivées du système de fichiers, recherche plein texte accent-insensible sur OCR + métadonnées), `IOcrService`/`TesseractOcrService` (Tesseract + pdftoppm, repli silencieux).
- Toutes les écritures passent par `IJournalService` ; aucun calcul métier dans la GED.

## 2026-11-24 — Moteur de modèles

- `IModeleDocumentService`/`ModeleDocumentService` : jetons `{{Projet.*}}`, `{{Marche.*}}`, `{{DateGeneration}}`, `{{Titre}}` et valeurs supplémentaires (`{{Observations}}`), jetons non résolus signalés.
- Génération DOCX (zip Open XML minimal) et PDF 1.4 natif via `DocxWriter`/`PdfWriter` (aucun package NuGet lourd). Documents générés archivés dans la GED et journalisés (`GenerationModele`).

## 2026-11-24 — Écran et vérification

- `VueGed`/`GedViewModel` : arborescence projets/dossiers, grille filtrable, prévisualisation (image + texte OCR + métadonnées), import multiple, génération depuis modèle. Menu « Documents » câblé.
- Correction WPF : `TreeView.SelectedItem` est read-only → sélection propagée via `SelectedItemChanged`.
- Build : 0 erreur/0 avertissement. Tests : 15/15 verts. Application WPF lancée sans exception.
