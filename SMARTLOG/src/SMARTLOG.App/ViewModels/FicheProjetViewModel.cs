using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

public sealed record OptionStatut(StatutProjet Valeur, string Libelle);

/// <summary>
/// ViewModel de la fiche projet. Les champs contractuels calculés
/// (montant actualisé, délai actualisé, fin prévisionnelle) ne sont jamais
/// modifiables ici : ils sont affichés en lecture seule dans la vue.
/// </summary>
public class FicheProjetViewModel : ViewModelBase
{
    private readonly IProjetService _projetService;
    private readonly IJournalService _journalService;

    public IReadOnlyList<OptionStatut> OptionsStatut { get; } = new[]
    {
        new OptionStatut(StatutProjet.EnCours, "En cours"),
        new OptionStatut(StatutProjet.Termine, "Terminé"),
        new OptionStatut(StatutProjet.Suspendu, "Suspendu")
    };

    public ObservableCollection<EntreeJournal> Historique { get; } = new();

    public event EventHandler? ProjetModifie;
    public event EventHandler? RetourALaListeDemande;
    public event EventHandler<Guid>? ProjetSupprime;

    private ProjetDetail? _projet;
    public ProjetDetail? Projet
    {
        get => _projet;
        private set
        {
            if (SetField(ref _projet, value))
                Notifier(nameof(EstCharge), nameof(EstVide));
        }
    }

    public bool EstCharge => Projet is not null;
    public bool EstVide => !EstCharge;

    private string _intitule = string.Empty;
    public string Intitule
    {
        get => _intitule;
        set => SetField(ref _intitule, value);
    }

    private string? _localisation;
    public string? Localisation
    {
        get => _localisation;
        set => SetField(ref _localisation, value);
    }

    private string? _natureTravaux;
    public string? NatureTravaux
    {
        get => _natureTravaux;
        set => SetField(ref _natureTravaux, value);
    }

    private DateTime? _dateNotification;
    public DateTime? DateNotification
    {
        get => _dateNotification;
        set => SetField(ref _dateNotification, value);
    }

    private DateTime? _dateCommencement;
    public DateTime? DateCommencement
    {
        get => _dateCommencement;
        set => SetField(ref _dateCommencement, value);
    }

    private OptionStatut? _statutSelectionne;
    public OptionStatut? StatutSelectionne
    {
        get => _statutSelectionne;
        set => SetField(ref _statutSelectionne, value);
    }

    public ICommand EnregistrerCommand { get; }
    public ICommand ChangerStatutCommand { get; }
    public ICommand SupprimerCommand { get; }
    public ICommand RetourALaListeCommand { get; }

    public FicheProjetViewModel(IProjetService projetService, IJournalService journalService)
    {
        _projetService = projetService;
        _journalService = journalService;

        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => EstCharge);
        ChangerStatutCommand = new RelayCommand(_ => ChangerStatut(), _ => EstCharge && StatutSelectionne is not null);
        SupprimerCommand = new RelayCommand(_ => Supprimer(), _ => EstCharge);
        RetourALaListeCommand = new RelayCommand(_ => RetourALaListeDemande?.Invoke(this, EventArgs.Empty));
    }

    public void Charger(Guid projetId)
    {
        var detail = _projetService.ObtenirAsync(projetId).GetAwaiter().GetResult();
        Projet = detail;

        Historique.Clear();
        if (detail is null)
            return;

        Intitule = detail.Intitule;
        Localisation = detail.Localisation;
        NatureTravaux = detail.NatureTravaux;
        DateNotification = VersDateTime(detail.DateNotification);
        DateCommencement = VersDateTime(detail.DateCommencement);
        StatutSelectionne = OptionsStatut.First(o => o.Valeur == detail.Statut);

        foreach (var entree in _journalService
                     .ListerAsync(nameof(Projet), projetId)
                     .GetAwaiter()
                     .GetResult())
        {
            Historique.Add(entree);
        }

        ((RelayCommand)EnregistrerCommand).Actualiser();
        ((RelayCommand)ChangerStatutCommand).Actualiser();
        ((RelayCommand)SupprimerCommand).Actualiser();
    }

    /// <summary>Vide la fiche lorsque le projet affiché n'existe plus.</summary>
    public void Vider()
    {
        Projet = null;
        Historique.Clear();
        Intitule = string.Empty;
        Localisation = null;
        NatureTravaux = null;
        DateNotification = null;
        DateCommencement = null;
        StatutSelectionne = null;

        ((RelayCommand)EnregistrerCommand).Actualiser();
        ((RelayCommand)ChangerStatutCommand).Actualiser();
        ((RelayCommand)SupprimerCommand).Actualiser();
    }

    private void Enregistrer()
    {
        if (Projet is null)
            return;

        var modifie = _projetService.ModifierAsync(
            new ProjetModification(
                Projet.Id,
                Intitule,
                Localisation,
                NatureTravaux,
                VersDateOnly(DateNotification),
                VersDateOnly(DateCommencement)),
            DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

        Charger(modifie.Id);
        ProjetModifie?.Invoke(this, EventArgs.Empty);
    }

    private void ChangerStatut()
    {
        if (Projet is null || StatutSelectionne is null)
            return;

        var modifie = _projetService.ChangerStatutAsync(
            Projet.Id,
            StatutSelectionne.Valeur,
            DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

        Charger(modifie.Id);
        ProjetModifie?.Invoke(this, EventArgs.Empty);
    }

    private void Supprimer()
    {
        if (Projet is null)
            return;

        var confirmation = MessageBox.Show(
            $"Supprimer définitivement le projet « {Projet.Code} — {Projet.Intitule} » ?\n\n" +
            "Toutes ses données liées (marché, avenants, finances, planning, " +
            "réserves, documents, courriers, alertes) et ses fichiers archivés " +
            "seront supprimés.\nCette action est irréversible.",
            "Confirmation de suppression",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (confirmation != MessageBoxResult.Yes)
            return;

        var projetId = Projet.Id;
        _projetService.SupprimerAsync(
            projetId, DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

        ProjetSupprime?.Invoke(this, projetId);
    }

    private static DateTime? VersDateTime(DateOnly? date) =>
        date?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? VersDateOnly(DateTime? date) =>
        date is null ? null : DateOnly.FromDateTime(date.Value);
}
