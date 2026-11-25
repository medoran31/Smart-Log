using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

public sealed record OptionAvenant(TypeAvenant Valeur, string Libelle);

/// <summary>
/// ViewModel de l'écran « Avenants ». La simulation est demandée à
/// IAvenantService à chaque saisie : aucun calcul n'est fait dans l'UI et
/// aucun brouillon n'est écrit tant que l'utilisateur n'a pas cliqué sur
/// « Enregistrer le brouillon » ou « Valider ».
/// </summary>
public class AvenantsViewModel : ViewModelBase
{
    private readonly IAvenantService _avenantService;
    private readonly IMarcheService _marcheService;
    private readonly IRecalculCroiseService _recalcul;

    private Guid _projetId;
    private Guid _marcheId;

    public IReadOnlyList<OptionAvenant> OptionsType { get; } = new[]
    {
        new OptionAvenant(TypeAvenant.Delai, "Délai"),
        new OptionAvenant(TypeAvenant.Financier, "Financier"),
        new OptionAvenant(TypeAvenant.Mixte, "Mixte")
    };

    public ObservableCollection<AvenantResume> Avenants { get; } = new();

    public ICommand EnregistrerBrouillonCommand { get; }
    public ICommand ValiderCommand { get; }
    public ICommand AnnulerAvenantCommand { get; }
    public ICommand NouvelAvenantCommand { get; }

    /// <summary>Notifie le parent que les valeurs dérivées du projet ont changé.</summary>
    public event EventHandler<Guid>? ProjetModifie;

    private string _intituleProjet = string.Empty;
    public string IntituleProjet
    {
        get => _intituleProjet;
        private set => SetField(ref _intituleProjet, value);
    }

    private string _referenceMarche = string.Empty;
    public string ReferenceMarche
    {
        get => _referenceMarche;
        private set => SetField(ref _referenceMarche, value);
    }

    private string _message = "Sélectionnez un projet dans « Mes projets » pour afficher ses avenants.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private bool _estCharge;
    public bool EstCharge
    {
        get => _estCharge;
        private set
        {
            if (SetField(ref _estCharge, value))
            {
                ActualiserCommandes();
                Notifier(nameof(EstFormulaireEditable));
            }
        }
    }

    /// <summary>Un avenant validé ou annulé est verrouillé : seul un brouillon est éditable.</summary>
    public bool EstFormulaireEditable =>
        EstCharge && (AvenantSelectionne is null || EstBrouillon);

    private AvenantResume? _avenantSelectionne;
    public AvenantResume? AvenantSelectionne
    {
        get => _avenantSelectionne;
        set
        {
            if (SetField(ref _avenantSelectionne, value))
            {
                if (value is not null)
                {
                    Numero = value.Numero;
                    TypeSelectionne = OptionsType.First(o => o.Valeur == value.Type);
                    MontantDeltaTexte = value.MontantDelta.ToString(CultureInfo.CurrentCulture);
                    DelaiDeltaTexte = value.DelaiDeltaJours.ToString(CultureInfo.CurrentCulture);
                }

                EstBrouillon = value?.Statut == StatutAvenant.Brouillon;
                EstValide = value?.Statut == StatutAvenant.Valide;
                ActualiserCommandes();
                Notifier(nameof(EstFormulaireEditable));
                Simuler();
            }
        }
    }

    private bool _estBrouillon;
    public bool EstBrouillon
    {
        get => _estBrouillon;
        private set => SetField(ref _estBrouillon, value);
    }

    private bool _estValide;
    public bool EstValide
    {
        get => _estValide;
        private set => SetField(ref _estValide, value);
    }

    private int _numero;
    public int Numero
    {
        get => _numero;
        private set => SetField(ref _numero, value);
    }

    private OptionAvenant? _typeSelectionne;
    public OptionAvenant? TypeSelectionne
    {
        get => _typeSelectionne;
        set
        {
            if (SetField(ref _typeSelectionne, value))
                Simuler();
        }
    }

    private string _montantDeltaTexte = "0";
    public string MontantDeltaTexte
    {
        get => _montantDeltaTexte;
        set
        {
            if (SetField(ref _montantDeltaTexte, value))
                Simuler();
        }
    }

    private string _delaiDeltaTexte = "0";
    public string DelaiDeltaTexte
    {
        get => _delaiDeltaTexte;
        set
        {
            if (SetField(ref _delaiDeltaTexte, value))
                Simuler();
        }
    }

    private decimal _montantActuel;
    public decimal MontantActuel
    {
        get => _montantActuel;
        private set => SetField(ref _montantActuel, value);
    }

    private decimal _nouveauMontant;
    public decimal NouveauMontant
    {
        get => _nouveauMontant;
        private set => SetField(ref _nouveauMontant, value);
    }

    private int _delaiActuelJours;
    public int DelaiActuelJours
    {
        get => _delaiActuelJours;
        private set => SetField(ref _delaiActuelJours, value);
    }

    private int _nouveauDelaiJours;
    public int NouveauDelaiJours
    {
        get => _nouveauDelaiJours;
        private set => SetField(ref _nouveauDelaiJours, value);
    }

    private string _dateFinActuelle = "—";
    public string DateFinActuelle
    {
        get => _dateFinActuelle;
        private set => SetField(ref _dateFinActuelle, value);
    }

    private string _nouvelleDateFin = "—";
    public string NouvelleDateFin
    {
        get => _nouvelleDateFin;
        private set => SetField(ref _nouvelleDateFin, value);
    }

    public AvenantsViewModel(
        IAvenantService avenantService,
        IMarcheService marcheService,
        IRecalculCroiseService recalcul)
    {
        _avenantService = avenantService;
        _marcheService = marcheService;
        _recalcul = recalcul;

        EnregistrerBrouillonCommand = new RelayCommand(_ => EnregistrerBrouillon(), _ => EstCharge);
        ValiderCommand = new RelayCommand(_ => Valider(), _ => EstCharge && EstBrouillon && AvenantSelectionne is not null);
        AnnulerAvenantCommand = new RelayCommand(_ => AnnulerAvenant(), _ => EstCharge && EstValide);
        NouvelAvenantCommand = new RelayCommand(_ => NouvelAvenant(), _ => EstCharge);
    }

    public void Charger(Guid projetId)
    {
        _projetId = projetId;
        _marcheId = Guid.Empty;
        Avenants.Clear();
        AvenantSelectionne = null;
        EstCharge = false;

        var marche = _marcheService.ObtenirAsync(projetId).GetAwaiter().GetResult();
        if (marche is null)
        {
            IntituleProjet = string.Empty;
            ReferenceMarche = string.Empty;
            Message = "Aucun marché n'est associé à ce projet : aucun avenant ne peut être créé.";
            ReinitialiserSimulation();
            return;
        }

        _marcheId = marche.Id;
        EstCharge = true;
        IntituleProjet = marche.ProjetIntitule;
        ReferenceMarche = marche.ReferenceMarche;

        foreach (var avenant in _avenantService.ListerAsync(marche.Id).GetAwaiter().GetResult())
            Avenants.Add(avenant);

        NouvelAvenant();
        Message = $"{Avenants.Count} avenant(s) sur le marché {marche.ReferenceMarche}.";
    }

    private void NouvelAvenant()
    {
        AvenantSelectionne = null;
        EstBrouillon = false;
        EstValide = false;
        Numero = 0;
        TypeSelectionne = OptionsType[1];
        MontantDeltaTexte = "0";
        DelaiDeltaTexte = "0";
        ActualiserCommandes();
        Simuler();
    }

    private void EnregistrerBrouillon()
    {
        try
        {
            var cree = _avenantService.CreerAsync(
                _marcheId,
                new AvenantCreation(
                    Numero,
                    TypeSelectionne?.Valeur ?? TypeAvenant.Financier,
                    LireMontantDelta(),
                    LireDelaiDelta()),
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();

            Charger(_projetId);
            AvenantSelectionne = Avenants.FirstOrDefault(a => a.Id == cree.Id);
            Message = $"Avenant n°{cree.Numero} enregistré en brouillon. Aucune valeur du projet n'a été modifiée.";
        }
        catch (Exception ex)
        {
            Message = $"Enregistrement impossible : {ex.Message}";
        }
    }

    private void Valider()
    {
        try
        {
            var utilisateur = DonneesDemonstration.UtilisateurDemonstrationId;
            var projet = _avenantService.ValiderAsync(
                AvenantSelectionne!.Id, utilisateur).GetAwaiter().GetResult();

            // La chaîne avenant → finances → planning → alertes est rafraîchie
            // ici : le projet a déjà été recalculé par IAvenantService.
            _recalcul.RecalculerDepuisProjetAsync(projet.Id, utilisateur).GetAwaiter().GetResult();

            var numero = AvenantSelectionne.Numero;
            Charger(_projetId);
            Message = $"Avenant n°{numero} validé. Montant actualisé : {projet.MontantActualise:N2} DA, délai : {projet.DelaiActualiseJours} jours.";
            ProjetModifie?.Invoke(this, _projetId);
        }
        catch (Exception ex)
        {
            Message = $"Validation impossible : {ex.Message}";
        }
    }

    private void AnnulerAvenant()
    {
        if (AvenantSelectionne is null)
            return;

        try
        {
            var utilisateur = DonneesDemonstration.UtilisateurDemonstrationId;
            var projet = _avenantService.AnnulerAsync(
                AvenantSelectionne.Id, utilisateur).GetAwaiter().GetResult();

            _recalcul.RecalculerDepuisProjetAsync(projet.Id, utilisateur).GetAwaiter().GetResult();

            var numero = AvenantSelectionne.Numero;
            Charger(_projetId);
            Message = $"Avenant n°{numero} annulé. Montant actualisé : {projet.MontantActualise:N2} DA, délai : {projet.DelaiActualiseJours} jours.";
            ProjetModifie?.Invoke(this, _projetId);
        }
        catch (Exception ex)
        {
            Message = $"Annulation impossible : {ex.Message}";
        }
    }

    private void Simuler()
    {
        if (!EstCharge || _marcheId == Guid.Empty)
        {
            ReinitialiserSimulation();
            return;
        }

        try
        {
            var simulation = _avenantService.SimulerAsync(
                _marcheId,
                LireMontantDelta(),
                LireDelaiDelta()).GetAwaiter().GetResult();

            MontantActuel = simulation.MontantActuel;
            NouveauMontant = simulation.NouveauMontant;
            DelaiActuelJours = simulation.DelaiActuelJours;
            NouveauDelaiJours = simulation.NouveauDelaiJours;
            DateFinActuelle = simulation.DateFinActuelle?.ToString("d") ?? "—";
            NouvelleDateFin = simulation.NouvelleDateFin?.ToString("d") ?? "—";
        }
        catch (Exception ex)
        {
            Message = $"Simulation impossible : {ex.Message}";
        }
    }

    private void ReinitialiserSimulation()
    {
        MontantActuel = 0m;
        NouveauMontant = 0m;
        DelaiActuelJours = 0;
        NouveauDelaiJours = 0;
        DateFinActuelle = "—";
        NouvelleDateFin = "—";
    }

    private decimal LireMontantDelta() =>
        decimal.TryParse(MontantDeltaTexte, NumberStyles.Number, CultureInfo.CurrentCulture, out var valeur)
            ? valeur
            : 0m;

    private int LireDelaiDelta() =>
        int.TryParse(DelaiDeltaTexte, NumberStyles.Integer, CultureInfo.CurrentCulture, out var valeur)
            ? valeur
            : 0;

    private void ActualiserCommandes()
    {
        ((RelayCommand)EnregistrerBrouillonCommand).Actualiser();
        ((RelayCommand)ValiderCommand).Actualiser();
        ((RelayCommand)AnnulerAvenantCommand).Actualiser();
        ((RelayCommand)NouvelAvenantCommand).Actualiser();
    }
}
