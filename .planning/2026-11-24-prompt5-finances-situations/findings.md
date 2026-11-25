# Findings — Prompt 5

- Entités existantes : `Attachement` (Date, Statut: Brouillon/Contradictoire/Valide, Lignes), `LigneAttachement` (PrixUnitaireId, QuantiteExecutee, MontantCalcule), `Situation` (Periode « AAAA-MM », Type, MontantCumule, RetenueGarantie, Avance, Penalites, Statut Brouillon/Emise/Payee).
- `LigneAttachement.MontantCalcule` est documenté comme toujours dérivé de `QuantiteExecutee * PrixUnitaire.PrixUnitaireHT` : aucun service ne l'écrit encore.
- `SituationService.InitialiserAsync` existe (taux d'avancement + taux de retenue, avance fixe 10 %) et est appelé par le seeder avec 0,35 puis 0,60 : `MontantCumule` est donc un cumul à date, pas un montant de période.
- Pas de clé étrangère Situation↔Attachement : le regroupement se fait par période calendaire (`Attachement.Date` → « AAAA-MM »), sans migration.
- Reliquat contractuel = `PrixUnitaire.QuantiteContractuelle - somme des quantités exécutées sur toutes les lignes du projet`. Le dépassement est une alerte visuelle, pas une exception (demande explicite).
- Le menu latéral contient déjà « Attachements et situations » et « Finances » (placeholders).
- Aucun package de graphiques WPF en cache : le graphique sera dessiné en XAML natif (axes + `Polyline` + points + légende), sans dépendance externe.
- Règles Prompt 0 : calculs uniquement dans les services, écritures via `IJournalService`, interface et implémentation dans `SMARTLOG.Services`, UserControl + ViewModel dans `SMARTLOG.App`, libellés français, build à chaque étape.
