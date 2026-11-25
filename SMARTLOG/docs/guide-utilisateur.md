# Guide utilisateur SMARTLOG

SMARTLOG est un poste de travail Windows de suivi de chantier : projets, marchés,
avenants, attachements, situations financières, planning, réserves, documents,
courriers, alertes et assistant IA. Toutes les valeurs calculées sont produites
par les services de `SMARTLOG.Services` et tracées dans un journal d'actions.

## 1. Installer et lancer SMARTLOG

### Installation sur un poste Windows 64 bits

Aucun runtime .NET n'est nécessaire : la publication est **autonome**
(`self-contained`, `win-x64`).

1. Publier l'application depuis la solution :

   ```powershell
   cd SMARTLOG
   .\scripts\publish.ps1
   ```

   Le résultat est dans `src\SMARTLOG.App\bin\Release\net8.0-windows\win-x64\publish`.
2. Installer :

   ```powershell
   .\scripts\install.ps1
   ```

   Les fichiers sont copiés dans `%LOCALAPPDATA%\Programs\SmartLog` et un
   raccourci « SMARTLOG » est créé dans le menu Démarrer. Ajoutez
   `-DesktopShortcut` pour obtenir aussi un raccourci sur le bureau. Aucun
   droit administrateur n'est requis.
3. Lancer « SMARTLOG » depuis le menu Démarrer, ou directement
   `SMARTLOG.App.exe` depuis le dossier de publication.

Pour désinstaller : `.\scripts\uninstall.ps1`. Les données utilisateur sont
conservées par défaut ; ajoutez `-RemoveUserData` pour les supprimer
**définitivement** (base, documents, sauvegardes et configuration Claude).

### Données locales

| Contenu | Emplacement |
| --- | --- |
| Base SQLite | `%LOCALAPPDATA%\Smartlog\smartlog.db` |
| Documents de la GED | `%LOCALAPPDATA%\Smartlog\Documents` |
| Sauvegardes | `%LOCALAPPDATA%\Smartlog\Sauvegardes` |
| Configuration Claude | `%LOCALAPPDATA%\Smartlog\assistant-ia.json` |

Au premier lancement, les migrations EF Core sont appliquées automatiquement
et un jeu de données de démonstration est créé. Les lancements suivants
conservent vos données.

## 2. Prise en main : la fenêtre principale

- **Menu latéral** : 15 entrées ; un clic affiche le module correspondant.
- **Barre supérieure** : recherche, thème clair/sombre et cloche de
  notifications. La cloche ouvre les dernières alertes non lues ; chaque
  notification peut être marquée comme lue ou ouverte sur sa source.
- **Sélection d'un projet** : choisir une ligne dans « Mes projets » charge
  automatiquement sa fiche, son marché, ses avenants, ses attachements, son
  planning, ses réserves, ses courriers, ses documents et l'assistant IA sur
  ce projet.

L'entrée « Suivi technique » affiche encore un écran de placeholder. Le
« Tableau de bord » affiche désormais les indicateurs du projet courant
(KPI, alertes, échéances, finances et planning).

## 3. Les modules pas à pas

### Mes projets

Liste filtrable (code, intitulé, localisation, statut) avec montant initial,
**montant actualisé calculé**, délais contractuel et actualisé. La ligne
sélectionnée est le projet courant pour tous les autres modules.

Pour ajouter un projet, cliquez sur **« + Nouveau projet »** puis renseignez le
formulaire : code (obligatoire et unique), intitulé (obligatoire), localisation,
nature des travaux, montant initial, délai contractuel en jours, dates de
notification et de commencement, et statut. Le bouton **« Créer le projet »**
appelle `IProjetService.CreerAsync` : la création est journalisée, les valeurs
dérivées sont initialisées, la liste est rechargée et le nouveau projet devient
le projet courant. **« Annuler »** masque le formulaire sans rien écrire.

### Fiche projet

Trois onglets :

- **Identification** : intitulé, localisation, nature des travaux
  modifiables ; code, intervenants et montants (initial et actualisé) en
  lecture seule.
- **Dates clés** : date de notification et date de commencement modifiables,
  fin prévisionnelle calculée en lecture seule. Le bouton « Changer le statut »
  applique le statut choisi. « Enregistrer les modifications » journalise la
  mutation. Si la date de commencement change, le système déclenche
  automatiquement le recalcul croisé complet.
- **Historique** : toutes les actions journalisées sur le projet, avec les
  valeurs avant/après et l'utilisateur.

Le bouton **« Supprimer le projet »** (en haut de la fiche) ouvre une
confirmation. S'il est accepté, `IProjetService.SupprimerAsync` efface dans une
seule transaction le projet et **toutes ses données liées** : marché, avenants,
bordereau de prix, attachements et situations, tâches et liaisons du planning,
réserves, documents GED, courriers et alertes. Les fichiers archivés dans
`%LOCALAPPDATA%\Smartlog\Documents\<CODE>\` sont aussi retirés du disque, et
la suppression reste tracée dans le journal. **Cette opération est
irréversible** : pour conserver l'historique, préférez le statut **Terminé**
(« Changer le statut »). Si aucun marché n'est encore associé, vous pouvez
créer une sauvegarde depuis « Paramètres » avant de supprimer.

### Marché et contrats

Consultation du marché du projet, de ses pièces, du bordereau de prix
unitaires et de son total calculé. Les boutons « Ajouter », « Modifier » et
« Supprimer » gèrent les prix du bordereau. Modifier un prix recalcule
immédiatement le montant de toutes les lignes d'attachement qui l'utilisent
(recalcul journalisé) afin que le cumul des situations reste exact.

### Avenants

- « Nouvel avenant » prépare un brouillon ; « Enregistrer le brouillon » le
  conserve sans effet sur le projet.
- Le bloc de **simulation** affiche les montants et délais avant/après sans
  aucune écriture.
- « Valider l'avenant » est le **seul** endroit qui modifie le montant
  actualisé, le délai actualisé et la date prévisionnelle du projet.
- « Annuler l'avenant » recalcule le projet en excluant cet avenant ; l'action
  reste tracée dans l'historique.

Après validation ou annulation, les finances, le planning, les alertes et
l'assistant IA sont rechargés automatiquement.

### Attachements et situations

- « Nouvel attachement » crée un attachement à une date donnée.
- « Ajouter », « Modifier » et « Supprimer » gèrent les lignes : le montant
  d'une ligne est toujours `quantité exécutée × prix unitaire HT`, calculé par
  le service. Le dépassement de reliquat est signalé en rouge mais n'empêche
  pas la saisie ; il doit être régularisé par un avenant.
- « Valider l'attachement » rend ses lignes éligibles aux situations
  financières.
- « Créer / recalculer la situation » génère la situation de la période
  saisie (`AAAA-MM`) : le cumul inclut tous les attachements validés jusqu'à
  la fin de cette période. La retenue de garantie (5 %) et l'avance (10 %) sont
  calculées par le service.

Toute modification de ligne déclenche le recalcul croisé afin d'éviter un
suivi financier périmé.

### Suivi technique

Entrée réservée aux indicateurs techniques ; l'écran dédié n'est pas encore
disponible.

### Réserves

- « Nouvelle réserve » : description, priorité, entreprise responsable, date
  de constatation et délai de levée. L'échéance et l'ancienneté sont calculées.
- Cycle de vie strict : **Nouvelle → En cours → En vérification → Levée →
  Clôturée** ; un saut d'étape est refusé.
- À la levée, un PV est généré et archivé dans la GED ; « Ouvrir le PV de levée
  » l'ouvre avec l'application associée.

### Planning

- « Nouvelle tâche », « Enregistrer » et « Supprimer » gèrent l'arborescence
  WBS. Les dates prévues et le chemin critique sont calculés, jamais saisis.
- « Lier » et « Supprimer le lien » gèrent les liaisons (Fin–Début, Début–
  Début, Fin–Fin, Début–Fin). Un lien qui créerait un cycle est refusé.
- « Recalculer le CPM » recalcule dates, marges et chemin critique. Une tâche
  en retard alimente le centre d'alertes.

### Finances

- Indicateurs : montant contractuel, engagé, payé et restant dû.
- Graphique d'évolution engagé/payé dans le temps.
- Liste des situations : « Marquer émise » et « Marquer payée » font évoluer
  le statut sans modifier les montants calculés.
- « Actualiser » relit les données après une modification intervenue dans un
  autre module.

### Documents

- « Importer des pièces » : PDF, images et documents bureautiques du projet.
  L'OCR est tenté ; en cas d'échec, le document est quand même archivé et
  signalé « sans OCR ».
- La recherche ignore la casse et les accents et porte sur le texte OCR et
  les métadonnées.
- « Ouvrir le fichier » ouvre la pièce sélectionnée.
- « Générer et archiver dans la GED » crée un document (Word ou PDF) à partir
  d'un modèle et des jetons disponibles (`{{Projet.Intitule}}`,
  `{{Marche.Reference}}`, `{{Observations}}`…).

#### Modèles Excel à remplir

Le panneau « Modèles Excel à remplir » propose **14 classeurs `.xlsx`** :
fiche projet, contrat/marché, avenant, attachement, situation, suivi financier,
réserve, procès-verbal, planning, courrier, alertes, bordereau de prix
unitaires, document GED générique et rapport d'activité.

1. Cliquez sur **« Créer les modèles »** : les classeurs manquants sont écrits
   dans `%LOCALAPPDATA%\Smartlog\Modeles`. Les fichiers déjà présents ne sont
   jamais écrasés, afin de préserver vos saisies.
2. Cliquez sur **« Ouvrir le dossier »**, ouvrez le classeur voulu dans Excel
   et remplissez les cellules **jaunes** de la feuille *Saisie*. La feuille
   *Aide* rappelle les colonnes, les instructions et les jetons disponibles.
   Les montants sont exprimés en **DA**.
3. Dans SMARTLOG, sélectionnez le projet dans l'arborescence de **Documents**,
   puis **« Importer des pièces »** et choisissez le classeur rempli. Il est
   archivé et versionné comme n'importe quelle autre pièce.

Ces modèles sont des supports de saisie et d'archivage : ils ne créent pas
eux-mêmes les données métier, qui restent saisies dans les modules dédiés.

### Courrier

- « Nouveau courrier » : sens (entrant/sortant), expéditeur, destinataire,
  objet, date et délai de réponse facultatif. L'échéance est calculée.
- « Enregistrer la réponse » et « Sans suite » font évoluer le statut.
  Une réponse en retard alimente le centre d'alertes.

### Centre d'alertes

- « Analyser » relance les quatre règles (échéance projet, tâche en retard,
  réserve non levée, courrier sans réponse) sur l'horizon choisi. Une seconde
  analyse ne duplique pas les alertes existantes.
- « Ouvrir la source » navigue vers le module concerné.
- « Marquer comme lue » / « Tout marquer comme lu » mettent à jour le compteur
  de la cloche.

### Assistant IA

- Sélectionnez un projet et configurez votre clé (voir section 4).
- La provenance est assemblée par SMARTLOG et explicitement séparée :
  `[DOC]` (extraits OCR de la GED), `[CALCUL]` (valeurs calculées par les
  services) et `[SUGGESTION]` (pistes proposées par Claude).
- « Envoyer à Claude » interroge l'API ; « Effacer » vide la conversation.
- **Aucune action n'est effectuée automatiquement** : une suggestion n'est
  créée qu'après un clic sur « Valider et appliquer ». « Ignorer » la_trace
  simplement comme ignorée.

### Paramètres (sauvegarde et restauration)

- « Créer une sauvegarde » copie de façon cohérente la base SQLite (via
  `VACUUM INTO`) et les documents de la GED dans un dossier horodaté de
  `%LOCALAPPDATA%\Smartlog\Sauvegardes`. Une description facultative permet de
  repérer le point de repère. La clé Claude n'est **jamais** incluse.
- La liste affiche la date, la taille, la description et la présence des
  documents. « Actualiser » relit la liste ; « Supprimer » retire une copie.
- « Restaurer » vérifie d'abord `PRAGMA integrity_check`. Si la copie est
  valide, la restauration est **programmée** : fermez SMARTLOG puis relancez-le
  pour l'appliquer. L'ancienne base est conservée sous le nom
  `smartlog.db.avant-restauration-…`. Si la copie est corrompue, la
  restauration est refusée sans toucher à la base courante.

## 4. Configurer l'assistant IA

Créez `%LOCALAPPDATA%\Smartlog\assistant-ia.json` :

```json
{
  "CleApi": "sk-ant-...",
  "Modele": "claude-opus-5-5",
  "VersionApi": "2026-10-21",
  "IdentifiantEspaceTravail": "wrkspc_..."
}
```

Seul `CleApi` est obligatoire. Les variables d'environnement
`SMARTLOG_CLAUDE_API_KEY`, `SMARTLOG_CLAUDE_MODELE`,
`SMARTLOG_CLAUDE_VERSION_API` et `SMARTLOG_CLAUDE_WORKSPACE_ID` prennent le pas
sur le fichier. La clé n'est ni journalisée ni incluse dans une sauvegarde.

## 5. Règles de cohérence à connaître

- Un seul service propriétaire écrit chaque valeur dérivée. Les vues ne
  calculent aucune donnée métier.
- La chaîne de recalcul est **avenant → finances → planning → alertes**. Elle
  est déclenchée automatiquement lors des mutations contractuelles et peut être
  relancée via les boutons d'actualisation des modules concernés.
- Les taux historiques d'une situation (retenue et avance) sont préservés lors
  d'un recalcul croisé : vos saisies ne sont pas écrasées par des valeurs
  par défaut.
- Chaque écriture métier passe par le journal : l'onglet « Historique » de la
  fiche projet permet de vérifier qui a fait quoi et quand.

## 6. Limites connues

- « Suivi technique » est un placeholder.
- Aucune authentification n'est encore implémentée : le poste de travail
  suppose un usage mono-poste et mono-utilisateur.
- La GED stocke les fichiers localement ; aucun serveur de documents n'est
  configuré.

## 7. Vérifier l'installation (développeur)

Depuis le dossier `SMARTLOG` :

```powershell
dotnet build SMARTLOG.sln --nologo -v q
dotnet test SMARTLOG.sln --nologo -v q
.\scripts\publish.ps1
```

La suite de tests couvre les services de calcul, les recalculs croisés, la
sauvegarde/restauration, le client HTTP Claude et les parcours métier des
modules.
