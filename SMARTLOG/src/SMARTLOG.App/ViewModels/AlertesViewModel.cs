using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Centre d'alertes : affiche les alertes générées par le moteur de règles,
/// permet de lancer une analyse et de marquer les notifications comme lues.
/// Les libellés, priorités et échéances sont calculés par IAlerteService.
/// </summary>
public class AlertesViewModel : ViewModelBase
{
    private readonly IAlerteService _alertes;
    private readonly IProjetService _projets;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<AlerteResume> Alertes { get; } = new();

    public ICommand ActualiserCommand { get; }
    public ICommand AnalyserCommand { get; }
    public ICommand MarquerLueCommand { get; }
    public ICommand MarquerToutesLuesCommand { get; }
    public ICommand OuvrirSourceCommand { get; }

    /// <summary>Demande de navigation vers le module à l'origine d'une alerte.</summary>
    public event EventHandler<(string MenuCible, Guid ProjetId, Guid SourceId)>? NavigationDemandee;

    private ProjetResume? _projetSelectionne;
    public ProjetResume? ProjetSelectionne
    {
        get => _projetSelectionne;
        set
        {
            if (SetField(ref _projetSelectionne, value))
                Charger();
        }
    }

    private string _horizonTexte = "30";
    public string HorizonTexte
    {
        get => _horizonTexte;
        set => SetField(ref _horizonTexte, value);
    }

    private string _message = "Sélectionnez un projet pour afficher ses alertes.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private int _nombreNonLues;
    public int NombreNonLues
    {
        get => _nombreNonLues;
        private set => SetField(ref _nombreNonLues, value);
    }

    private int _nombreEnRetard;
    public int NombreEnRetard
    {
        get => _nombreEnRetard;
        private set => SetField(ref _nombreEnRetard, value);
    }

    private AlerteResume? _alerteSelectionnee;
    public AlerteResume? AlerteSelectionnee
    {
        get => _alerteSelectionnee;
        set
        {
            if (SetField(ref _alerteSelectionnee, value))
                ActualiserCommandes();
        }
    }

    public AlertesViewModel(IAlerteService alertes, IProjetService projets)
    {
        _alertes = alertes;
        _projets = projets;

        ActualiserCommand = new RelayCommand(_ => Charger());
        AnalyserCommand = new RelayCommand(_ => Analyser());
        MarquerLueCommand = new RelayCommand(
            _ => MarquerLue(),
            _ => AlerteSelectionnee is not null && !AlerteSelectionnee.EstLue);
        MarquerToutesLuesCommand = new RelayCommand(
            _ => MarquerToutesLues(), _ => NombreNonLues > 0);
        OuvrirSourceCommand = new RelayCommand(
            _ => OuvrirSource(), _ => AlerteSelectionnee is not null);

        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);
        Charger();
    }

    /// <summary>Aligne le centre sur le projet couramment sélectionné dans l'application.</summary>
    public void SelectionnerProjet(Guid projetId)
    {
        var projet = Projets.FirstOrDefault(p => p.Id == projetId);
        if (projet is not null && !ReferenceEquals(projet, ProjetSelectionne))
            ProjetSelectionne = projet;
        else
            Charger();
    }

    public void Charger()
    {
        var centre = _alertes.ObtenirCentreAsync(ProjetSelectionne?.Id).GetAwaiter().GetResult();
        Remplir(centre);
    }

    public void Analyser()
    {
        if (!int.TryParse(HorizonTexte, NumberStyles.Integer, CultureInfo.InvariantCulture, out var horizon))
        {
            Message = "L'horizon d'analyse doit être un nombre entier de jours.";
            return;
        }

        try
        {
            var centre = _alertes.AnalyserAsync(
                ProjetSelectionne?.Id,
                DonneesDemonstration.UtilisateurDemonstrationId,
                horizon).GetAwaiter().GetResult();
            Remplir(centre);
            Message = $"Analyse terminée : {centre.Alertes.Count} alerte(s), " +
                      $"{centre.NombreNonLues} non lue(s), {centre.NombreEnRetard} en retard.";
        }
        catch (Exception ex)
        {
            Message = $"Analyse impossible : {ex.Message}";
        }
    }

    public void RafraichirDepuisNotifications()
    {
        Charger();
    }

    private void MarquerLue()
    {
        if (AlerteSelectionnee is null)
            return;

        try
        {
            _alertes.MarquerLueAsync(
                AlerteSelectionnee.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            Message = "Alerte marquée comme lue.";
        }
        catch (Exception ex)
        {
            Message = $"Marquage impossible : {ex.Message}";
        }
    }

    private void MarquerToutesLues()
    {
        try
        {
            _alertes.MarquerToutesLuesAsync(
                ProjetSelectionne?.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            Message = "Toutes les alertes ont été marquées comme lues.";
        }
        catch (Exception ex)
        {
            Message = $"Marquage impossible : {ex.Message}";
        }
    }

    private void OuvrirSource()
    {
        if (AlerteSelectionnee is null)
            return;

        NavigationDemandee?.Invoke(
            this,
            (AlerteSelectionnee.MenuCible, AlerteSelectionnee.ProjetId, AlerteSelectionnee.SourceId));
    }

    private void Remplir(CentreNotifications centre)
    {
        Alertes.Clear();
        foreach (var alerte in centre.Alertes)
            Alertes.Add(alerte);

        AlerteSelectionnee = Alertes.FirstOrDefault();
        NombreNonLues = centre.NombreNonLues;
        NombreEnRetard = centre.NombreEnRetard;
        if (!string.IsNullOrWhiteSpace(Message) &&
            !Message.StartsWith("Analyse", StringComparison.Ordinal) &&
            !Message.StartsWith("Alerte", StringComparison.Ordinal) &&
            !Message.StartsWith("Toutes les alertes", StringComparison.Ordinal) &&
            !Message.StartsWith("Marquage", StringComparison.Ordinal) &&
            !Message.StartsWith("Vérifiez", StringComparison.Ordinal))
        {
            Message = $"{centre.Alertes.Count} alerte(s) — {centre.NombreNonLues} non lue(s), " +
                      $"{centre.NombreEnRetard} en retard.";
        }
        ActualiserCommandes();
    }

    private void ActualiserCommandes()
    {
        ((RelayCommand)ActualiserCommand).Actualiser();
        ((RelayCommand)AnalyserCommand).Actualiser();
        ((RelayCommand)MarquerLueCommand).Actualiser();
        ((RelayCommand)MarquerToutesLuesCommand).Actualiser();
        ((RelayCommand)OuvrirSourceCommand).Actualiser();
    }
}
