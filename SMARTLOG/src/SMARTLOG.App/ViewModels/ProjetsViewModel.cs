using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>Option de la liste déroulante de statuts du formulaire de création.</summary>
public sealed record OptionStatutProjet(StatutProjet Valeur, string Libelle);

/// <summary>
/// ViewModel du module « Mes projets ». Liste filtrable et triable ; aucun
/// calcul n'est effectué ici, seules les valeurs issues des services sont
/// affichées. La sélection d'une ligne notifie la navigation vers la fiche.
/// Le formulaire « Nouveau projet » délègue l'écriture à IProjetService.
/// </summary>
public class ProjetsViewModel : ViewModelBase
{
    private readonly IProjetService _projetService;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ICollectionView ProjetsView { get; }

    public IReadOnlyList<OptionStatutProjet> Statuts { get; } = new[]
    {
        new OptionStatutProjet(StatutProjet.EnCours, "En cours"),
        new OptionStatutProjet(StatutProjet.Termine, "Terminé"),
        new OptionStatutProjet(StatutProjet.Suspendu, "Suspendu")
    };

    public event EventHandler<ProjetResume?>? ProjetSelectionneChanged;

    private string _filtre = string.Empty;
    public string Filtre
    {
        get => _filtre;
        set
        {
            if (SetField(ref _filtre, value))
                ProjetsView.Refresh();
        }
    }

    private ProjetResume? _projetSelectionne;
    public ProjetResume? ProjetSelectionne
    {
        get => _projetSelectionne;
        set
        {
            if (SetField(ref _projetSelectionne, value))
                ProjetSelectionneChanged?.Invoke(this, value);
        }
    }

    // --- Formulaire de création ------------------------------------------------

    private bool _formulaireVisible;
    public bool FormulaireVisible
    {
        get => _formulaireVisible;
        private set => SetField(ref _formulaireVisible, value);
    }

    private string _nouveauCode = string.Empty;
    public string NouveauCode
    {
        get => _nouveauCode;
        set => SetField(ref _nouveauCode, value);
    }

    private string _nouvelIntitule = string.Empty;
    public string NouvelIntitule
    {
        get => _nouvelIntitule;
        set => SetField(ref _nouvelIntitule, value);
    }

    private string _nouvelleLocalisation = string.Empty;
    public string NouvelleLocalisation
    {
        get => _nouvelleLocalisation;
        set => SetField(ref _nouvelleLocalisation, value);
    }

    private string _nouvelleNatureTravaux = string.Empty;
    public string NouvelleNatureTravaux
    {
        get => _nouvelleNatureTravaux;
        set => SetField(ref _nouvelleNatureTravaux, value);
    }

    private string _nouveauMontantInitial = "0";
    public string NouveauMontantInitial
    {
        get => _nouveauMontantInitial;
        set => SetField(ref _nouveauMontantInitial, value);
    }

    private string _nouveauDelaiContractuelJours = "0";
    public string NouveauDelaiContractuelJours
    {
        get => _nouveauDelaiContractuelJours;
        set => SetField(ref _nouveauDelaiContractuelJours, value);
    }

    private DateTime? _nouvelleDateNotification;
    public DateTime? NouvelleDateNotification
    {
        get => _nouvelleDateNotification;
        set => SetField(ref _nouvelleDateNotification, value);
    }

    private DateTime? _nouvelleDateCommencement;
    public DateTime? NouvelleDateCommencement
    {
        get => _nouvelleDateCommencement;
        set => SetField(ref _nouvelleDateCommencement, value);
    }

    private StatutProjet _nouveauStatut = StatutProjet.EnCours;
    public StatutProjet NouveauStatut
    {
        get => _nouveauStatut;
        set => SetField(ref _nouveauStatut, value);
    }

    private string _messageFormulaire = string.Empty;
    public string MessageFormulaire
    {
        get => _messageFormulaire;
        private set => SetField(ref _messageFormulaire, value);
    }

    public ICommand AfficherFormulaireCommand { get; }
    public ICommand AnnulerFormulaireCommand { get; }
    public ICommand CreerProjetCommand { get; }

    public ProjetsViewModel(IProjetService projetService)
    {
        _projetService = projetService;
        ProjetsView = CollectionViewSource.GetDefaultView(Projets);
        ProjetsView.Filter = CorrespondAuFiltre;

        AfficherFormulaireCommand = new RelayCommand(_ => AfficherFormulaire());
        AnnulerFormulaireCommand = new RelayCommand(_ => AnnulerFormulaire());
        CreerProjetCommand = new RelayCommand(_ => CreerProjet());

        Recharger();
    }

    public void Recharger()
    {
        var idSelectionne = ProjetSelectionne?.Id;

        Projets.Clear();
        foreach (var projet in _projetService.ListerAsync().GetAwaiter().GetResult())
        {
            Projets.Add(projet);
        }

        ProjetsView.Refresh();

        if (idSelectionne is not null)
        {
            var retrouve = Projets.FirstOrDefault(p => p.Id == idSelectionne.Value);
            SetField(ref _projetSelectionne, retrouve);
        }
    }

    private bool CorrespondAuFiltre(object element)
    {
        if (element is not ProjetResume projet || string.IsNullOrWhiteSpace(Filtre))
            return true;

        var texte = $"{projet.Code} {projet.Intitule} {projet.Localisation} {projet.Statut}";
        return texte.Contains(Filtre.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private void AfficherFormulaire()
    {
        ViderFormulaire();
        FormulaireVisible = true;
        MessageFormulaire = "Les champs marqués d'un * sont obligatoires.";
    }

    private void AnnulerFormulaire()
    {
        FormulaireVisible = false;
        ViderFormulaire();
    }

    private void ViderFormulaire()
    {
        NouveauCode = string.Empty;
        NouvelIntitule = string.Empty;
        NouvelleLocalisation = string.Empty;
        NouvelleNatureTravaux = string.Empty;
        NouveauMontantInitial = "0";
        NouveauDelaiContractuelJours = "0";
        NouvelleDateNotification = null;
        NouvelleDateCommencement = null;
        NouveauStatut = StatutProjet.EnCours;
        MessageFormulaire = string.Empty;
    }

    private void CreerProjet()
    {
        if (string.IsNullOrWhiteSpace(NouveauCode) || string.IsNullOrWhiteSpace(NouvelIntitule))
        {
            MessageFormulaire = "Le code et l'intitulé du projet sont obligatoires.";
            return;
        }

        if (!decimal.TryParse(NouveauMontantInitial, NumberStyles.Number, CultureInfo.CurrentCulture, out var montant)
            || montant < 0)
        {
            MessageFormulaire = "Le montant initial doit être un nombre positif.";
            return;
        }

        if (!int.TryParse(NouveauDelaiContractuelJours, NumberStyles.Integer, CultureInfo.InvariantCulture, out var delai)
            || delai <= 0)
        {
            MessageFormulaire = "Le délai contractuel doit être un nombre entier de jours supérieur à 0.";
            return;
        }

        try
        {
            var cree = _projetService.CreerAsync(
                new ProjetCreation(
                    NouveauCode.Trim(),
                    NouvelIntitule.Trim(),
                    string.IsNullOrWhiteSpace(NouvelleLocalisation) ? null : NouvelleLocalisation.Trim(),
                    string.IsNullOrWhiteSpace(NouvelleNatureTravaux) ? null : NouvelleNatureTravaux.Trim(),
                    montant,
                    NouvelleDateNotification is DateTime notification ? DateOnly.FromDateTime(notification) : null,
                    NouvelleDateCommencement is DateTime commencement ? DateOnly.FromDateTime(commencement) : null,
                    delai,
                    NouveauStatut),
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

            AnnulerFormulaire();
            Recharger();

            var nouveau = Projets.FirstOrDefault(p => p.Id == cree.Id);
            if (nouveau is not null)
                ProjetSelectionne = nouveau;
        }
        catch (Exception ex)
        {
            MessageFormulaire = $"Création impossible : {ex.Message}";
        }
    }
}
