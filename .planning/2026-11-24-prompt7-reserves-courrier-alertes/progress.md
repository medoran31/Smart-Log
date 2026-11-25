# Progress

## 2026-11-24 — Prompt 7 terminé

- Cadrage des entités, du moteur de modèles du Prompt 4 et de la cloche de `MainWindow`.
- Décisions : migration EF pour les dates de courrier, jetons `{{Reserve.*}}` via `ValeursSupplementaires`, extension de la demande de génération pour lier le PV à la réserve, alertes idempotentes par (projet, type source, id source) avec horizon de 30 jours.
- Migration `CourrierDates` générée (le projet d'application ne référence pas `Microsoft.EntityFrameworkCore.Design` : `dotnet ef` est exécuté avec `SMARTLOG.Data` comme projet de démarrage).
- Services implémentés : `ReserveService` (cycle strict + PV lié + journal), `CourrierService` (registre + délai de réponse), `AlerteService` (4 règles, déduplication, centre de notifications, marquage lu). Moteur de modèles étendu pour le lien GED ; libellés du journal complétés ; graines de démonstration enrichies (courriers + analyse initiale).
- Écrans implémentés : `VueReserves`, `VueCourriers`, `VueAlertes` avec ViewModels, DI, menus, DataTemplates et propagation de la sélection de projet. Cloche fonctionnelle avec badge, pop-up, navigation et marquage lu.
- Tests ajoutés : cycle de vie et PV lié, délai de réponse, quatre règles d'alerte, idempotence, marquage lu. `dotnet test` : 33/33.
- Vérification WPF/UI Automation : navigation vers les trois nouveaux modules et ouverture de la cloche confirmées.
- Corrections apportées en cours de route : `using System.IO` manquant, types anonymes avec membre qualifié interdits, warning CS8629 sur le délai de courrier, avertissement xUnit2031, validation de la date de levée avant mutation de l'entité.
- État final : build 0 avertissement / 0 erreur, tests 33/33, UI vérifiée. Prochaine étape : Prompt 8.
