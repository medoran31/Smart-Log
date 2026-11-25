using System.Collections.ObjectModel;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

public record ElementMenu(string Libelle, string Glyphe);

/// <summary>
/// Pilote le menu latéral et la zone centrale : chaque élément du menu
/// affiche sa vue, et la sélection d'un projet dans « Mes projets » ouvre
/// automatiquement sa fiche. La cloche affiche les dernières alertes du
/// centre de notifications et permet d'y naviguer.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private readonly IAlerteService _alertes;
    private readonly TableauDeBoardViewModel _tableauDeBoardViewModel;
    private readonly ProjetsViewModel _projetsViewModel;
    private readonly FicheProjetViewModel _ficheProjetViewModel;
    private readonly MarchesViewModel _marchesViewModel;
    private readonly AvenantsViewModel _avenantsViewModel;
    private readonly GedViewModel _gedViewModel;
    private readonly AttachementsViewModel _attachementsViewModel;
    private readonly FinancesViewModel _financesViewModel;
    private readonly PlanningViewModel _planningViewModel;
    private readonly ReservesViewModel _reservesViewModel;
    private readonly CourriersViewModel _courriersViewModel;
    private readonly AlertesViewModel _alertesViewModel;
    private readonly AssistantIaViewModel _assistantIaViewModel;
    private readonly SauvegardeViewModel _sauvegardeViewModel;

    // Glyphes Segoe MDL2 Assets : monochromes, contrastés sur le fond sombre du menu.
    public ObservableCollection<ElementMenu> ElementsMenu { get; } = new()
    {
        new("Tableau de bord", "\uE9D2"),   // AreaChart
        new("Mes projets", "\uE822"),       // Construction
        new("Fiche projet", "\uE8FC"),      // GoToStart
        new("Marché et contrats", "\uE8A5"), // Document
        new("Avenants", "\uE70F"),          // Edit
        new("Attachements et situations", "\uE8C7"), // PaymentCard
        new("Suivi technique", "\uE9D5"),   // CheckList
        new("Réserves", "\uE7BA"),          // Warning
        new("Planning", "\uE787"),          // Calendar
        new("Finances", "\uE9D2"),          // AreaChart
        new("Documents", "\uE8F1"),         // Library
        new("Courrier", "\uE715"),          // Mail
        new("Centre d'alertes", "\uE91C"),  // ActionCenter
        new("Assistant IA", "\uE99A"),      // Robot
        new("Paramètres", "\uE713"),        // Setting
    };

    public ObservableCollection<AlerteResume> DernieresNotifications { get; } = new();

    public ICommand BasculerNotificationsCommand { get; }
    public ICommand OuvrirCentreAlertesCommand { get; }
    public ICommand MarquerNotificationLueCommand { get; }
    public ICommand MarquerToutesNotificationsLuesCommand { get; }
    public ICommand OuvrirNotificationCommand { get; }

    private ElementMenu? _elementSelectionne;
    public ElementMenu? ElementSelectionne
    {
        get => _elementSelectionne;
        set
        {
            if (SetField(ref _elementSelectionne, value))
                MettreAJourVueCourante();
        }
    }

    private ViewModelBase? _vueCourante;
    public ViewModelBase? VueCourante
    {
        get => _vueCourante;
        private set => SetField(ref _vueCourante, value);
    }

    private bool _afficherPlaceholder = true;
    public bool AfficherPlaceholder
    {
        get => _afficherPlaceholder;
        private set => SetField(ref _afficherPlaceholder, value);
    }

    private int _nombreNotificationsNonLues;
    public int NombreNotificationsNonLues
    {
        get => _nombreNotificationsNonLues;
        private set => SetField(ref _nombreNotificationsNonLues, value);
    }

    public bool AAlertesNonLues => NombreNotificationsNonLues > 0;

    private bool _panneauNotificationsOuvert;
    public bool PanneauNotificationsOuvert
    {
        get => _panneauNotificationsOuvert;
        set
        {
            if (SetField(ref _panneauNotificationsOuvert, value) && value)
                MettreAJourNotifications();
        }
    }

    public MainViewModel(
        IAlerteService alertes,
        TableauDeBoardViewModel tableauDeBoardViewModel,
        ProjetsViewModel projetsViewModel,
        FicheProjetViewModel ficheProjetViewModel,
        MarchesViewModel marchesViewModel,
        AvenantsViewModel avenantsViewModel,
        GedViewModel gedViewModel,
        AttachementsViewModel attachementsViewModel,
        FinancesViewModel financesViewModel,
        PlanningViewModel planningViewModel,
        ReservesViewModel reservesViewModel,
        CourriersViewModel courriersViewModel,
        AlertesViewModel alertesViewModel,
        AssistantIaViewModel assistantIaViewModel,
        SauvegardeViewModel sauvegardeViewModel)
    {
        _alertes = alertes;
        _tableauDeBoardViewModel = tableauDeBoardViewModel;
        _projetsViewModel = projetsViewModel;
        _ficheProjetViewModel = ficheProjetViewModel;
        _marchesViewModel = marchesViewModel;
        _avenantsViewModel = avenantsViewModel;
        _gedViewModel = gedViewModel;
        _attachementsViewModel = attachementsViewModel;
        _financesViewModel = financesViewModel;
        _planningViewModel = planningViewModel;
        _reservesViewModel = reservesViewModel;
        _courriersViewModel = courriersViewModel;
        _alertesViewModel = alertesViewModel;
        _assistantIaViewModel = assistantIaViewModel;
        _sauvegardeViewModel = sauvegardeViewModel;

        _assistantIaViewModel.SuggestionAppliquee += SurSuggestionAppliquee;
        _projetsViewModel.ProjetSelectionneChanged += SurProjetSelectionne;
        _ficheProjetViewModel.ProjetModifie += SurProjetModifie;
        _ficheProjetViewModel.RetourALaListeDemande += SurRetourALaListe;
        _ficheProjetViewModel.ProjetSupprime += SurProjetSupprime;
        _avenantsViewModel.ProjetModifie += SurAvenantModifie;
        _alertesViewModel.NavigationDemandee += SurNavigationAlerte;

        BasculerNotificationsCommand = new RelayCommand(
            _ => PanneauNotificationsOuvert = !PanneauNotificationsOuvert);
        OuvrirCentreAlertesCommand = new RelayCommand(_ => OuvrirCentreAlertes());
        MarquerNotificationLueCommand = new RelayCommand(parametre => MarquerNotificationLue(parametre as AlerteResume));
        MarquerToutesNotificationsLuesCommand = new RelayCommand(
            _ => MarquerToutesNotificationsLues(), _ => NombreNotificationsNonLues > 0);
        OuvrirNotificationCommand = new RelayCommand(parametre => OuvrirNotification(parametre as AlerteResume));

        ElementSelectionne = ElementsMenu.First();
        MettreAJourNotifications();
    }

    private void MettreAJourVueCourante()
    {
        switch (ElementSelectionne?.Libelle)
        {
            case "Tableau de bord":
                _tableauDeBoardViewModel.ChargerPourProjetActif();
                VueCourante = _tableauDeBoardViewModel;
                AfficherPlaceholder = false;
                break;
            case "Mes projets":
                VueCourante = _projetsViewModel;
                AfficherPlaceholder = false;
                break;
            case "Fiche projet":
                VueCourante = _ficheProjetViewModel;
                AfficherPlaceholder = false;
                break;
            case "Marché et contrats":
                VueCourante = _marchesViewModel;
                AfficherPlaceholder = false;
                break;
            case "Avenants":
                VueCourante = _avenantsViewModel;
                AfficherPlaceholder = false;
                break;
            case "Attachements et situations":
                _attachementsViewModel.Charger();
                VueCourante = _attachementsViewModel;
                AfficherPlaceholder = false;
                break;
            case "Finances":
                _financesViewModel.Charger();
                VueCourante = _financesViewModel;
                AfficherPlaceholder = false;
                break;
            case "Planning":
                _planningViewModel.Charger();
                VueCourante = _planningViewModel;
                AfficherPlaceholder = false;
                break;
            case "Réserves":
                _reservesViewModel.Charger();
                VueCourante = _reservesViewModel;
                AfficherPlaceholder = false;
                break;
            case "Courrier":
                _courriersViewModel.Charger();
                VueCourante = _courriersViewModel;
                AfficherPlaceholder = false;
                break;
            case "Centre d'alertes":
                _alertesViewModel.RafraichirDepuisNotifications();
                VueCourante = _alertesViewModel;
                AfficherPlaceholder = false;
                break;
            case "Documents":
                _gedViewModel.Charger();
                VueCourante = _gedViewModel;
                AfficherPlaceholder = false;
                break;
            case "Assistant IA":
                _assistantIaViewModel.Recharger();
                VueCourante = _assistantIaViewModel;
                AfficherPlaceholder = false;
                break;
            case "Paramètres":
                _sauvegardeViewModel.Charger();
                VueCourante = _sauvegardeViewModel;
                AfficherPlaceholder = false;
                break;
            default:
                VueCourante = null;
                AfficherPlaceholder = true;
                break;
        }
    }

    private void SurProjetSelectionne(object? expediteur, ProjetResume? projet)
    {
        if (projet is null)
            return;

        _ficheProjetViewModel.Charger(projet.Id);
        _marchesViewModel.Charger(projet.Id);
        _avenantsViewModel.Charger(projet.Id);
        _gedViewModel.SelectionnerProjet(projet.Id);
        _attachementsViewModel.SelectionnerProjet(projet.Id);
        _financesViewModel.SelectionnerProjet(projet.Id);
        _planningViewModel.SelectionnerProjet(projet.Id);
        _reservesViewModel.SelectionnerProjet(projet.Id);
        _courriersViewModel.SelectionnerProjet(projet.Id);
        _alertesViewModel.SelectionnerProjet(projet.Id);
        _assistantIaViewModel.SelectionnerProjet(projet.Id);
        _tableauDeBoardViewModel.ChargerPourProjetActif();

        var menuFiche = ElementsMenu.First(e => e.Libelle == "Fiche projet");
        if (!ReferenceEquals(ElementSelectionne, menuFiche))
            ElementSelectionne = menuFiche;

        VueCourante = _ficheProjetViewModel;
        AfficherPlaceholder = false;
    }

    private void SurProjetModifie(object? expediteur, EventArgs evenement)
    {
        _projetsViewModel.Recharger();
    }

    private void SurRetourALaListe(object? expediteur, EventArgs evenement)
    {
        ElementSelectionne = ElementsMenu.First(e => e.Libelle == "Mes projets");
    }

    /// <summary>
    /// Après une suppression, aucun module ne doit continuer à afficher le
    /// projet disparu : la liste est rechargée, la fiche vidée et tous les
    /// modules dépendants sont réinitialisés avant le retour à la liste.
    /// </summary>
    private void SurProjetSupprime(object? expediteur, Guid projetId)
    {
        _projetsViewModel.Recharger();
        _ficheProjetViewModel.Vider();

        _marchesViewModel.Charger(Guid.Empty);
        _avenantsViewModel.Charger(Guid.Empty);
        _gedViewModel.Charger();
        _gedViewModel.SelectionnerProjet(Guid.Empty);
        _attachementsViewModel.Charger();
        _financesViewModel.Charger();
        _planningViewModel.Charger();
        _reservesViewModel.Charger();
        _courriersViewModel.Charger();
        _alertesViewModel.Charger();
        _assistantIaViewModel.Recharger();
        _tableauDeBoardViewModel.ChargerPourProjetActif();

        ElementSelectionne = ElementsMenu.First(e => e.Libelle == "Mes projets");
        MettreAJourNotifications();
    }

    private void SurAvenantModifie(object? expediteur, Guid projetId)
    {
        _projetsViewModel.Recharger();
        _marchesViewModel.Charger(projetId);
        _financesViewModel.Charger();
        _financesViewModel.SelectionnerProjet(projetId);
        _planningViewModel.Charger();
        _planningViewModel.SelectionnerProjet(projetId);
        _alertesViewModel.Charger();
        _alertesViewModel.SelectionnerProjet(projetId);
        _assistantIaViewModel.Recharger();
        if (_ficheProjetViewModel.Projet?.Id == projetId)
            _ficheProjetViewModel.Charger(projetId);
        _tableauDeBoardViewModel.ChargerPourProjetActif();
        MettreAJourNotifications();
    }

    private void SurNavigationAlerte(
        object? expediteur, (string MenuCible, Guid ProjetId, Guid SourceId) navigation)
    {
        NaviguerVers(navigation.MenuCible, navigation.ProjetId);
    }

    /// <summary>Recharge les modules touchés après une suggestion validée par l'utilisateur.</summary>
    private void SurSuggestionAppliquee(
        object? expediteur, (string TypeAction, Guid ProjetId) suggestion)
    {
        switch (suggestion.TypeAction)
        {
            case "CreerReserve":
                _reservesViewModel.Charger();
                break;
            case "CreerCourrier":
                _courriersViewModel.Charger();
                break;
            case "CreerTache":
                _planningViewModel.Charger();
                break;
        }

        _projetsViewModel.Recharger();
        _tableauDeBoardViewModel.ChargerPourProjetActif();
        MettreAJourNotifications();
    }

    private void NaviguerVers(string menuCible, Guid projetId)
    {
        switch (menuCible)
        {
            case "Réserves":
                _reservesViewModel.SelectionnerProjet(projetId);
                break;
            case "Courrier":
                _courriersViewModel.SelectionnerProjet(projetId);
                break;
            case "Planning":
                _planningViewModel.SelectionnerProjet(projetId);
                break;
            case "Fiche projet":
                _ficheProjetViewModel.Charger(projetId);
                break;
        }

        var menu = ElementsMenu.FirstOrDefault(e => e.Libelle == menuCible);
        if (menu is not null)
            ElementSelectionne = menu;
    }

    private void MettreAJourNotifications()
    {
        var centre = _alertes.ObtenirCentreAsync().GetAwaiter().GetResult();
        DernieresNotifications.Clear();
        foreach (var alerte in centre.DernieresNonLues)
            DernieresNotifications.Add(alerte);

        NombreNotificationsNonLues = centre.NombreNonLues;
        Notifier(nameof(AAlertesNonLues));
        ((RelayCommand)MarquerToutesNotificationsLuesCommand).Actualiser();
    }

    private void OuvrirCentreAlertes()
    {
        PanneauNotificationsOuvert = false;
        ElementSelectionne = ElementsMenu.First(e => e.Libelle == "Centre d'alertes");
    }

    private void MarquerNotificationLue(AlerteResume? alerte)
    {
        if (alerte is null)
            return;

        try
        {
            _alertes.MarquerLueAsync(
                alerte.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            MettreAJourNotifications();
            _alertesViewModel.RafraichirDepuisNotifications();
        }
        catch
        {
            // La notification reste affichée si le marquage échoue.
        }
    }

    private void MarquerToutesNotificationsLues()
    {
        try
        {
            _alertes.MarquerToutesLuesAsync(
                null,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            MettreAJourNotifications();
            _alertesViewModel.RafraichirDepuisNotifications();
        }
        catch
        {
            // Les notifications restent affichées si le marquage échoue.
        }
    }

    private void OuvrirNotification(AlerteResume? alerte)
    {
        if (alerte is null)
            return;

        MarquerNotificationLue(alerte);
        PanneauNotificationsOuvert = false;
        NaviguerVers(alerte.MenuCible, alerte.ProjetId);
    }
}
