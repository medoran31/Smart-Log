using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// ViewModel du module « Marché et contrats ». Affiche les pièces importées
/// et le bordereau de prix unitaires. Aucun montant n'est calculé ici : le
/// total du bordereau et le montant de chaque ligne viennent du service.
/// </summary>
public class MarchesViewModel : ViewModelBase
{
    private readonly IMarcheService _marcheService;

    private Guid _projetId;
    private Guid _marcheId;

    public ObservableCollection<PrixUnitaireResume> PrixUnitaires { get; } = new();
    public ObservableCollection<DocumentMarche> Documents { get; } = new();

    public ICommand AjouterPrixCommand { get; }
    public ICommand ModifierPrixCommand { get; }
    public ICommand SupprimerPrixCommand { get; }
    public ICommand OuvrirDocumentCommand { get; }

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

    private string _dateSignature = "—";
    public string DateSignature
    {
        get => _dateSignature;
        private set => SetField(ref _dateSignature, value);
    }

    private decimal _montantActualise;
    public decimal MontantActualise
    {
        get => _montantActualise;
        private set => SetField(ref _montantActualise, value);
    }

    private decimal _totalBordereau;
    public decimal TotalBordereau
    {
        get => _totalBordereau;
        private set => SetField(ref _totalBordereau, value);
    }

    private string _message = "Sélectionnez un projet dans « Mes projets » pour afficher son marché.";
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
                ((RelayCommand)AjouterPrixCommand).Actualiser();
                ((RelayCommand)ModifierPrixCommand).Actualiser();
                ((RelayCommand)SupprimerPrixCommand).Actualiser();
            }
        }
    }

    private PrixUnitaireResume? _prixSelectionne;
    public PrixUnitaireResume? PrixSelectionne
    {
        get => _prixSelectionne;
        set
        {
            if (SetField(ref _prixSelectionne, value) && value is not null)
            {
                Reference = value.Reference;
                Designation = value.Designation;
                Unite = value.Unite;
                PrixUnitaireHTTexte = value.PrixUnitaireHT.ToString(CultureInfo.CurrentCulture);
                QuantiteContractuelleTexte = value.QuantiteContractuelle.ToString(CultureInfo.CurrentCulture);
            }

            ((RelayCommand)ModifierPrixCommand).Actualiser();
            ((RelayCommand)SupprimerPrixCommand).Actualiser();
        }
    }

    private string _reference = string.Empty;
    public string Reference
    {
        get => _reference;
        set => SetField(ref _reference, value);
    }

    private string _designation = string.Empty;
    public string Designation
    {
        get => _designation;
        set => SetField(ref _designation, value);
    }

    private string _unite = string.Empty;
    public string Unite
    {
        get => _unite;
        set => SetField(ref _unite, value);
    }

    private string _prixUnitaireHTTexte = string.Empty;
    public string PrixUnitaireHTTexte
    {
        get => _prixUnitaireHTTexte;
        set => SetField(ref _prixUnitaireHTTexte, value);
    }

    private string _quantiteContractuelleTexte = string.Empty;
    public string QuantiteContractuelleTexte
    {
        get => _quantiteContractuelleTexte;
        set => SetField(ref _quantiteContractuelleTexte, value);
    }

    public MarchesViewModel(IMarcheService marcheService)
    {
        _marcheService = marcheService;

        AjouterPrixCommand = new RelayCommand(_ => AjouterPrix(), _ => EstCharge);
        ModifierPrixCommand = new RelayCommand(_ => ModifierPrix(), _ => EstCharge && PrixSelectionne is not null);
        SupprimerPrixCommand = new RelayCommand(_ => SupprimerPrix(), _ => EstCharge && PrixSelectionne is not null);
        OuvrirDocumentCommand = new RelayCommand(
            parametre => OuvrirDocument(parametre as DocumentMarche), _ => true);
    }

    public void Charger(Guid projetId)
    {
        _projetId = projetId;
        _marcheId = Guid.Empty;
        PrixUnitaires.Clear();
        Documents.Clear();
        PrixSelectionne = null;
        ViderFormulaire();

        var detail = _marcheService.ObtenirAsync(projetId).GetAwaiter().GetResult();
        if (detail is null)
        {
            EstCharge = false;
            IntituleProjet = string.Empty;
            ReferenceMarche = string.Empty;
            DateSignature = "—";
            MontantActualise = 0m;
            TotalBordereau = 0m;
            Message = "Aucun marché n'est associé à ce projet.";
            return;
        }

        _marcheId = detail.Id;
        EstCharge = true;
        IntituleProjet = detail.ProjetIntitule;
        ReferenceMarche = detail.ReferenceMarche;
        DateSignature = detail.DateSignature?.ToString("d") ?? "Non renseignée";
        MontantActualise = detail.MontantActualise;

        foreach (var prix in detail.PrixUnitaires)
            PrixUnitaires.Add(prix);
        foreach (var document in detail.Documents)
            Documents.Add(document);

        TotalBordereau = _marcheService
            .CalculerTotalBordereauAsync(detail.Id)
            .GetAwaiter()
            .GetResult();
        Message = $"Marché {detail.ReferenceMarche} — {detail.PrixUnitaires.Count} prix unitaire(s), {detail.Documents.Count} pièce(s).";
    }

    /// <summary>Appelé par la vue après sélection d'un fichier PDF/Word/Excel.</summary>
    public void ImporterDocument(string cheminSource)
    {
        if (!EstCharge)
        {
            Message = "Sélectionnez d'abord un projet disposant d'un marché.";
            return;
        }

        try
        {
            var document = _marcheService.ImporterDocumentAsync(
                _marcheId,
                cheminSource,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger(_projetId);
            Message = $"Pièce « {document.NomFichier} » importée (version {document.Version}).";
        }
        catch (Exception ex)
        {
            Message = $"Import impossible : {ex.Message}";
        }
    }

    private void AjouterPrix()
    {
        try
        {
            var donnees = LireFormulaire();
            var ajoute = _marcheService.AjouterPrixUnitaireAsync(
                _marcheId, donnees, DonneesDemonstration.UtilisateurDemonstrationId)
                .GetAwaiter().GetResult();
            Charger(_projetId);
            PrixSelectionne = PrixUnitaires.FirstOrDefault(p => p.Id == ajoute.Id);
            Message = $"Prix {ajoute.Reference} ajouté au bordereau.";
        }
        catch (Exception ex)
        {
            Message = $"Ajout impossible : {ex.Message}";
        }
    }

    private void ModifierPrix()
    {
        if (PrixSelectionne is null)
            return;

        try
        {
            var donnees = LireFormulaire();
            var modifie = _marcheService.ModifierPrixUnitaireAsync(
                new PrixUnitaireModification(
                    PrixSelectionne.Id,
                    donnees.Reference,
                    donnees.Designation,
                    donnees.Unite,
                    donnees.PrixUnitaireHT,
                    donnees.QuantiteContractuelle),
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger(_projetId);
            PrixSelectionne = PrixUnitaires.FirstOrDefault(p => p.Id == modifie.Id);
            Message = $"Prix {modifie.Reference} modifié.";
        }
        catch (Exception ex)
        {
            Message = $"Modification impossible : {ex.Message}";
        }
    }

    private void SupprimerPrix()
    {
        if (PrixSelectionne is null)
            return;

        try
        {
            var reference = PrixSelectionne.Reference;
            _marcheService.SupprimerPrixUnitaireAsync(
                PrixSelectionne.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger(_projetId);
            Message = $"Prix {reference} supprimé du bordereau.";
        }
        catch (Exception ex)
        {
            Message = $"Suppression impossible : {ex.Message}";
        }
    }

    private void OuvrirDocument(DocumentMarche? document)
    {
        if (document is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(document.CheminFichier) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Message = $"Ouverture impossible : {ex.Message}";
        }
    }

    private PrixUnitaireCreation LireFormulaire()
    {
        if (!decimal.TryParse(PrixUnitaireHTTexte, NumberStyles.Number,
                CultureInfo.CurrentCulture, out var prixUnitaireHT) ||
            !decimal.TryParse(QuantiteContractuelleTexte, NumberStyles.Number,
                CultureInfo.CurrentCulture, out var quantiteContractuelle))
        {
            throw new ArgumentException(
                "Saisissez un prix et une quantité valides (nombres décimaux).");
        }

        return new PrixUnitaireCreation(
            Reference, Designation, Unite, prixUnitaireHT, quantiteContractuelle);
    }

    private void ViderFormulaire()
    {
        Reference = string.Empty;
        Designation = string.Empty;
        Unite = string.Empty;
        PrixUnitaireHTTexte = string.Empty;
        QuantiteContractuelleTexte = string.Empty;
    }
}
