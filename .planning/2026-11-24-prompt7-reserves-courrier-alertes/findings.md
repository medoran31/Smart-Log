# Findings — Prompt 7

## Données et calculs

- `Reserve` : Numéro, Description, Priorité {Basse, Moyenne, Haute}, EntrepriseResponsable, DateConstatation, DelaiLeveeJours, Statut {Nouvelle, EnCours, EnVerification, Levee, Cloturee}, DateLevee. Suffisant pour le cycle de vie.
- `Courrier` : Sens {Entrant, Sortant}, Expéditeur, Destinataire, Objet, DelaiReponseJours?, Statut {EnAttente, Repondu, SansSuite}. **Aucune date** → ajout de `DateCourrier` (émission/réception) et `DateReponse` via la migration EF `CourrierDates`.
- `Alerte` : EntiteSourceType/EntiteSourceId, DateEcheance, Priorité {Basse, Moyenne, Haute}, EstLue. Aucun libellé stocké : le centre calcule le libellé à partir de l'entité source (valeur dérivée uniquement dans `SMARTLOG.Services`).
- Échéances calculées uniquement dans les services :
  - Réserve : `DateConstatation + DelaiLeveeJours`.
  - Courrier : `DateCourrier + DelaiReponseJours` (si renseigné).
  - Projet : `DatePrevisionnelleAchevement` (déjà dérivé des avenants validés).
- Numérotation des réserves : `Max(Numero) + 1` par projet, calculée dans `ReserveService`.

## Moteur de règles

- `AlerteService.AnalyserAsync` : horizon par défaut 30 jours, une alerte par (projet, type source, id source), idempotente (mise à jour échéance/priorité, `EstLue` remis à false uniquement si l'alerte change). Suppression des alertes dont la source ne satisfait plus la règle.
- Règles : échéance contractuelle proche/dépassée et projet non terminé ; tâche `DateFinPrevue < aujourd'hui`, sans `DateFinReelle` et avancement < 100 % ; réserve non levée dont l'échéance est proche/dépassée ; courrier `EnAttente` dont l'échéance est proche/dépassée.
- Priorité haute si l'échéance est à 7 jours ou moins ou dépassée ; réserve de priorité haute toujours haute.
- Création, recalcul, suppression, marquage et analyse sont journalisés via `IJournalService`.

## PV de levée

- `IModeleDocumentService.GenererAsync` avec jetons `{{Reserve.*}}` passés en `ValeursSupplementaires`.
- `ModeleDocumentDemande` étendu avec `EntiteLieeType`/`EntiteLieeId` : `IGedService.ImporterAsync` lie le PV à la réserve (`EntiteLieeType = nameof(Reserve)`) au lieu du projet par défaut.
- Le modèle Word est intégré à `ReserveService`, généré à la transition vers `Levee`, puis journalisé (`GenerationPvLevee`).

## Interface

- `VueReserves` : cycle de vie en un clic (« Passer en … »), échéances, responsable, ouverture du PV.
- `VueCourriers` : saisie entrant/sortant, délai de réponse, enregistrement de la réponse, classement sans suite, surlignage des hors délai.
- `VueAlertes` : compteurs, analyse paramétrable, marquage lu/tout lu, navigation vers la source.
- `MainWindow` : cloche avec badge du nombre de non-lues, pop-up des 5 dernières, ouverture du centre, tout marquer comme lu, navigation directe vers la source.
- Règles Prompt 0 conservées : calculs dans `SMARTLOG.Services`, écritures via `IJournalService`, interface + implémentation, `UserControl` + ViewModel, français, build à chaque étape.

## Vérification

- Build : 0 avertissement, 0 erreur. Tests : 33/33.
- UI Automation (PowerShell) : ouverture de la fenêtre, navigation Réserves / Courrier / Centre d'alertes et clic sur la cloche — tous les contrôles attendus présents.
- Piège rencontré : les scripts PowerShell 5.1 lisent les `.ps1` sans BOM en ANSI ; les recherches UI Automation avec accents doivent être construites via `[char]0xE9` (ou cibler `AutomationId` pour la cloche).
- Piège rencontré : un build échoue si l'application WPF est encore ouverte (DLL verrouillée) — fermer l'app avant de reconstruire.
