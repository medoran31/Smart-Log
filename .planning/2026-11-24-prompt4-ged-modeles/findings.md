# Findings — Prompt 4

- Code livré jusqu'au Prompt 3 : migration Initial, données de démo, projets/fiche projet, marchés/BPU et avenants avec simulation + tests (7 tests verts).
- Entité `DocumentGed` existante : `Id`, `ProjetId`, `TypeDocument`, `CheminFichier`, `Version`, `ContenuOcr`, `EntiteLieeType`, `EntiteLieeId`. Pas de colonne nom de fichier/date/taille → ces métadonnées seront dérivées du chemin et du système de fichiers (aucune migration supplémentaire nécessaire).
- `MarcheService` importe déjà des pièces dans `%LocalAppData%\Smartlog\Documents\<CodeProjet>\<ReferenceMarche>` ; le service GED utilisera la même racine avec un sous-dossier logique par type d'entité.
- Environnement : `tesseract`, `pdftoppm` et `magick` absents du PATH ; aucun package NuGet OCR/OpenXML/PDF en cache local.
- Stratégie OCR : `IOcrService` avec implémentation `TesseractOcrService` appelant l'exécutable Tesseract (PATH ou chemins Windows connus) et `pdftoppm` pour rasteriser les PDF ; si l'un ou l'autre manque, `ExtraireTexteAsync` renvoie `null` et l'import réussit sans OCR.
- Stratégie génération : DOCX = archive zip avec `[Content_Types].xml`, `_rels/.rels`, `word/document.xml` ; PDF = PDF 1.4 natif (Helvetica/WinAnsiEncoding, multipage). Aucune dépendance externe.
- Menu latéral existant contient déjà l'entrée « Documents » (encore au placeholder) : c'est elle qui affichera la GED.
- Règles Prompt 0 à respecter : calculs uniquement dans les services, écritures via `IJournalService`, UI/code en français, interface + implémentation côté Services, UserControl + ViewModel côté App, build à chaque étape.
