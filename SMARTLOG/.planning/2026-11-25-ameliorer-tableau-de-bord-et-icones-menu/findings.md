# Findings — Tableau de bord & icônes du menu

## Demande utilisateur (français)
« Améliorer l'interface (tableau de bord) qui est vierge et changer les icônes sur le volet à gauche qui sont très sombres. »

## État découvert

### 1. Le tableau de bord est un placeholder
- `MainWindow.xaml` (l. 160-163) affiche un `TextBlock` gris « Module à brancher : « Tableau de bord » et « Suivi technique »… » piloté par `AfficherPlaceholder`.
- `MainViewModel.MettreAJourVueCourante()` n'a **aucun** cas pour « Tableau de bord » → `VueCourante = null`, `AfficherPlaceholder = true`.
- `VueTableauDeBoard.xaml` existe mais :
  - aucun code-behind `VueTableauDeBoard.xaml.cs` (`x:Class` non implémenté) ;
  - référence des convertisseurs `Smartlog.App.Converters.BoolToVisibilityConverter` / `InverseBoolToVisibilityConverter` inexistants ;
  - référence un style `KpiCardStyle` inexistant ;
  - ligne 69 : `&` brut dans `Text="⚡ Alertes & Réserves"` → **erreur XAML MC3000**, la solution ne compile pas ;
  - bindings sans propriétés correspondantes : `TauxAvancement`, `MontantInitial`, `MontantActualise`, `TotalPaye`, `RestantDu`, `DureeProjetJours`, `NombreTachesCritiques`, `NombreTaches`.
- `TableauDeBoardViewModel` :
  - n'est pas enregistré dans `App.xaml.cs` (DI), ni injecté dans `MainViewModel` ;
  - hérite de `INotifyPropertyChanged` alors que `MainViewModel.VueCourante` exige `ViewModelBase` ;
  - appelle `_projetService.ObtenirProjetActif()` qui **n'existe pas** dans `IProjetService` (3 occurrences) ;
  - `AProjetOuvert` n'est jamais valorisé ;
  - drapeaux d'état vide inversés (`ADesAlertesOuReserves = centre.Alertes.Count == 0`, `ADesEcheances = true` dans le cas « aucune échéance ») ;
  - le service `_reserveService` est injecté mais jamais utilisé.
- `IProjetService` expose uniquement `ListerAsync`, `ObtenirAsync`, `CreerAsync`, `ModifierAsync`, `ChangerStatutAsync` et les records `ProjetResume` / `ProjetDetail` (pas de `DureeProjetJours`, `TauxAvancementPct` sur le détail).

### 2. Icônes du volet latéral
- `MainViewModel.ElementsMenu` utilise 15 **emoji** (`📊 🏗️ 📁 📄 ✏️ 💰 🧱 ⚠️ 📅 📈 🗂️ ✉️ 🔔 🤖 ⚙️`).
- `MainWindow.xaml` l. 101 : `<TextBlock Text="{Binding Icone}" FontSize="16" />` sans `Foreground` explicite → les glyphes emoji (souvent noirs/gris foncés : 🗂️, ✏️, 🧱, ⚙️) se confondent avec le fond sombre `#1B2733`.
- Aucun style `ListBoxItem` : l'élément sélectionné n'a pas de surlignage lisible.
- Documentation Microsoft consultée : police `Segoe MDL2 Assets` (monochrome, colorisable) avec codes PUA vérifiés (ex. `E80F` Home, `E8B7` Folder, `E8A5` Document, `E787` Calendar, `E8C7` PaymentCard, `E822` Construction, `E7BA` Warning, `E715` Mail, `E91C` ActionCenter, `E99A` Robot, `E713` Setting, `E9D2` AreaChart, `E70F` Edit, `E838` FolderOpen, `E8F1` Library, `E945` LightningBolt).

### 3. Contraintes / règles du projet
- WPF .NET 8, MVVM strict (README) : « aucun calcul dans l'interface », chaque valeur dérivée vient d'un service.
- `dotnet build SMARTLOG.sln` échoue aujourd'hui sur l'entité `&` non échappée dans le XAML du tableau de bord.
- Convention de nommage français dans le code (`Libelle`, `Charger`, `Notifier`, etc.).

## Décisions à prendre
- Remplacer les emoji du menu par des glyphes MDL2 + `Foreground` clair, avec style d'élément sélectionné.
- Brancher le tableau de bord (DI + code-behind + cas menu) et réécrire sa vue en XAML valide avec ressources locales.
- Revoir le ViewModel pour le rendre compilable et afficher des données réelles (projet actif à déterminer depuis les services existants).
