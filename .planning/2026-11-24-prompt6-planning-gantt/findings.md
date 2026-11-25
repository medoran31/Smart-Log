# Findings — Prompt 6

- `Tache` : Id, ProjetId, ParentTacheId (auto-référence, `DeleteBehavior.Restrict`), Libelle, DateDebutPrevue, DateFinPrevue, DateDebutReelle?, DateFinReelle?, AvancementPct, EstCheminCritique (« calculé par le moteur de planification, jamais saisi »).
- `LiaisonTache` : PredecesseurId, SuccesseurId, TypeLiaison { FinDebut, DebutDebut, FinFin, DebutFin }. Aucune contrainte d'unicité en base → anti-doublon à vérifier dans le service.
- Durée = `DateFinPrevue.DayNumber - DateDebutPrevue.DayNumber + 1` (jours calendaires inclusifs).
- CPM : passage avant ES = max(contraintes des prédécesseurs), passage arrière LF = min(contraintes des successeurs), marge = LS - ES, critique si marge = 0. Tri topologique (Kahn) → cycle détecté explicitement.
- Équations de contraintes (dates en DayNumber, Ds = durée successeur, Dp = durée prédécesseur) :
  - ESs ≥ EFp + 1 (FD), ≥ ESp (DD), ≥ EFp − Ds + 1 (FF), ≥ ESp − Ds + 1 (DF)
  - LFp ≤ LSs − 1 (FD), ≤ LSs + Dp − 1 (DD), ≤ LFs (FF), ≤ LFs + Dp − 1 (DF)
- Les tâches sans prédécesseur conservent leur `DateDebutPrevue` comme ancre. Pour un changement de date sur une tâche déjà liée, la nouvelle date est passée comme contrainte imposée **à l'opération**, puis le CPM recalcule toute la cascade.
- Aucun package Gantt WPF disponible : rendu natif `Canvas` + `ItemsControl` (barres positionnées par le ViewModel à partir des dates calculées par le service).
- Règles Prompt 0 : calculs uniquement dans `SMARTLOG.Services`, écritures via `IJournalService`, interface + implémentation dans `SMARTLOG.Services`, `UserControl` + ViewModel dans `SMARTLOG.App`, français, build à chaque étape.
