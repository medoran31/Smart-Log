# Task Plan : Prompt 7 — Réserves, courrier et alertes

## Goal

Livrer `IReserveService` (cycle de vie complet + PV de levée généré par le
moteur de modèles du Prompt 4), `ICourrierService` (registre entrant/sortant
avec délai de réponse), `IAlerteService` (moteur de règles analysant échéances,
tâches en retard, réserves non levées et courriers sans réponse), les écrans
correspondants et le panneau de notifications de la cloche de `MainWindow`.

## Next Step

Aucune : Prompt 7 terminé et vérifié.

## Phases

### Phase 1 : Cadrage
- [x] Entités `Reserve`, `Courrier`, `Alerte` et énumérations de statuts/priorités déjà présentes
- [x] Moteur `IModeleDocumentService.GenererAsync` disponible (jetons + GED + journal)
- [x] Cloche de `MainWindow` présente mais sans comportement ; menus « Réserves » et « Courrier » en placeholder
- [x] `Courrier` sans date de courrier/réponse → migration EF nécessaire
- **Status:** complete

### Phase 2 : Services
- [x] Migration `CourrierDates` (DateCourrier, DateReponse) + extension `ModeleDocumentDemande` pour lier le document généré à la réserve
- [x] `IReserveService`/`ReserveService` : numérotation, cycle Nouvelle → EnCours → EnVerification → Levee → Cloturee, échéances calculées, PV de levée via le moteur de modèles, journalisation
- [x] `ICourrierService`/`CourrierService` : registre entrant/sortant, délai de réponse, réponse/sans suite, échéance calculée, journalisation
- [x] `IAlerteService`/`AlerteService` : 4 règles, idempotence, centre de notifications (libellés calculés), marquage lu, journalisation
- [x] Données de démonstration (courriers + premier passage du moteur d'alertes) et libellés du journal
- [x] `dotnet build` après chaque étape (0 avertissement, 0 erreur)
- **Status:** complete

### Phase 3 : Écrans et notifications
- [x] `ReservesViewModel`/`VueReserves` : cycle de vie, échéances, PV généré
- [x] `CourriersViewModel`/`VueCourriers` : registre et réponses
- [x] `AlertesViewModel`/`VueAlertes` : centre complet, analyse et marquage lu
- [x] Panneau cloche dans `MainWindow` (badge, dernières alertes, navigation, tout marquer comme lu), menu « Centre d'alertes », DI et DataTemplates
- [x] Propagation de la sélection de projet aux trois nouveaux modules
- [x] `dotnet build` (0 avertissement, 0 erreur)
- **Status:** complete

### Phase 4 : Tests et vérification
- [x] Tests : cycle de vie et PV lié, délai de réponse, 4 règles d'alerte, idempotence, marquage lu
- [x] `dotnet test` vert : 33/33
- [x] Lancement WPF + navigation UI Automation (Réserves, Courrier, Centre d'alertes, cloche) : tous les contrôles présents
- **Status:** complete
