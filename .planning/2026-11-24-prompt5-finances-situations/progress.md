# Progress

## 2026-11-24 — Démarrage Prompt 5

- Cadrage des entités, des enums et du service de situations existant.
- Décision : regroupement des attachements par période calendaire (aucune migration), `MontantCumule` conservé comme cumul à date, dépassement de reliquat signalé par un indicateur et non par une exception.
- Plan créé ; prochaine action : service des attachements.

## 2026-11-24 — Services attachements et situations

- `IAttachementService`/`AttachementService` : création d'attachements, ajout/modification/suppression de lignes, `MontantCalcule = QuantiteExecutee × PrixUnitaireHT` calculé dans le service, reliquat avant/après ligne et indicateur `DepasseReliquat` (alerte, pas exception), simulation en lecture seule pour l'aperçu temps réel. Toutes les écritures journalisées.
- Correction SQLite : `Sum` sur `decimal` non traduit par le provider → total des quantités calculé en mémoire depuis les lignes matérialisées.
- `ISituationService` étendu : `CreerOuRecalculerAsync` (regroupement des attachements validés par période `AAAA-MM`), cumul à date, retenue de garantie, avance, pénalités, net à payer, `ChangerStatutAsync` et `ObtenirSuiviAsync` (engagé/payé/restant dû + taux d'avancement). `InitialiserAsync` conservé pour le seeder (idempotent).

## 2026-11-24 — Écrans et vérification

- `VueAttachements`/`AttachementsViewModel` : projet, formulaire de saisie avec aperçu calculé, lignes surlignées en rouge en cas de dépassement, validation d'attachement, création/recalcul de situation.
- `VueFinances`/`FinancesViewModel` : indicateurs, graphique d'évolution natif WPF (axes, grille, 3 séries, légende), grille des situations, passage en statut Émise/Payée. Câblage menu, DataTemplates et DI.
- Build : 0 erreur/0 avertissement. Tests : 23/23 verts. Application WPF lancée sans exception.
