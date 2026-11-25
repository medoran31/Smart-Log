using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Saisie des quantités exécutées. Tous les montants affichés (ligne, aperçu,
/// reliquat, total d'attachement) proviennent de l'IAttachementService : la
/// vue ne calcule rien.
/// </summary>
public class AttachementsViewModel : ViewModelBase
{
    private readonly IAttachementService _attachements;
    private readonly IMarcheService _marches;
    private readonly IProjetService _projets;
    private readonly ISituationService _situations;
    private readonly IRecalculCroiseService _recalcul;

    private bool _chargement;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<AttachementResume> Attachements { get; } = new();
    public ObservableCollection<LigneAttachementResume> Lignes { get; } = new();
    public ObservableCollection<PrixUnitaireResume> PrixUnitaires { get; } = new();
    public ObservableCollection<SituationResume> Situations { get; } = new();

    public ICommand NouvelAttachementCommand { get; }
    public ICommand AjouterLigneCommand { get; }
    public ICommand ModifierLigneCommand { get; }
    public ICommand SupprimerLigneCommand { get; }
    public ICommand ValiderAttachementCommand { get; }
    public ICommand CreerSituationCommand { get; }

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

    private AttachementResume? _attachementSelectionne;
    public AttachementResume? AttachementSelectionne
    {
        get => _attachementSelectionne;
        set
        {
            if (!SetField(ref _attachementSelectionne, value))
                return;

            _chargement = true;
            Lignes.Clear();
            if (value is not null)
                foreach (var ligne in _toutesLignes.Where(l => l.AttachementId == value.Id))
                    Lignes.Add(ligne);
            _chargement = false;

            ActualiserCommandes();
            Simuler();
        }
    }

    private LigneAttachementResume? _ligneSelectionnee;
    public LigneAttachementResume? LigneSelectionnee
    {
        get => _ligneSelectionnee;
        set
        {
            if (!SetField(ref _ligneSelectionnee, value) || value is null)
                return;

            _chargement = true;
            PrixSelectionne = PrixUnitaires.FirstOrDefault(p => p.Id == value.PrixUnitaireId);
            QuantiteTexte = value.QuantiteExecutee.ToString(CultureInfo.CurrentCulture);
            _chargement = false;
            Simuler(value.Id);
            ActualiserCommandes();
        }
    }

    private PrixUnitaireResume? _prixSelectionne;
    public PrixUnitaireResume? PrixSelectionne
    {
        get => _prixSelectionne;
        set
        {
            if (SetField(ref _prixSelectionne, value) && !_chargement)
                Simuler();
        }
    }

    private string _quantiteTexte = "0";
    public string QuantiteTexte
    {
        get => _quantiteTexte;
        set
        {
            if (SetField(ref _quantiteTexte, value) && !_chargement)
                Simuler();
        }
    }

    private DateOnly _dateAttachement = DateOnly.FromDateTime(DateTime.Now);
    public DateOnly DateAttachement
    {
        get => _dateAttachement;
        set => SetField(ref _dateAttachement, value);
    }

    private string _periode = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    public string Periode
    {
        get => _periode;
        set => SetField(ref _periode, value);
    }

    private string _penalitesTexte = "0";
    public string PenalitesTexte
    {
        get => _penalitesTexte;
        set => SetField(ref _penalitesTexte, value);
    }

    private decimal _montantApercu;
    public decimal MontantApercu
    {
        get => _montantApercu;
        private set => SetField(ref _montantApercu, value);
    }

    private decimal _reliquatApercu;
    public decimal ReliquatApercu
    {
        get => _reliquatApercu;
        private set => SetField(ref _reliquatApercu, value);
    }

    private bool _depassementApercu;
    public bool DepassementApercu
    {
        get => _depassementApercu;
        private set => SetField(ref _depassementApercu, value);
    }

    private string _depassementTexte = string.Empty;
    public string DepassementTexte
    {
        get => _depassementTexte;
        private set => SetField(ref _depassementTexte, value);
    }

    private string _message = "Sélectionnez un projet pour saisir les quantités exécutées.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private IReadOnlyList<LigneAttachementResume> _toutesLignes = Array.Empty<LigneAttachementResume>();

    public AttachementsViewModel(
        IAttachementService attachements,
        IMarcheService marches,
        IProjetService projets,
        ISituationService situations,
        IRecalculCroiseService recalcul)
    {
        _attachements = attachements;
        _marches = marches;
        _projets = projets;
        _situations = situations;
        _recalcul = recalcul;

        NouvelAttachementCommand = new RelayCommand(_ => NouvelAttachement(), _ => ProjetSelectionne is not null);
        AjouterLigneCommand = new RelayCommand(
            _ => AjouterLigne(), _ => ProjetSelectionne is not null && AttachementSelectionne is not null && PrixSelectionne is not null);
        ModifierLigneCommand = new RelayCommand(
            _ => ModifierLigne(), _ => LigneSelectionnee is not null && PrixSelectionne is not null);
        SupprimerLigneCommand = new RelayCommand(_ => SupprimerLigne(), _ => LigneSelectionnee is not null);
        ValiderAttachementCommand = new RelayCommand(
            _ => ValiderAttachement(), _ => AttachementSelectionne is not null);
        CreerSituationCommand = new RelayCommand(_ => CreerSituation(), _ => ProjetSelectionne is not null);

        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);

        if (Projets.Count > 0)
            ProjetSelectionne = Projets[0];
    }

    /// <summary>Sélectionne un projet depuis la fiche projet (menu « Mes projets »).</summary>
    public void SelectionnerProjet(Guid projetId)
    {
        var projet = Projets.FirstOrDefault(p => p.Id == projetId);
        if (projet is not null)
            ProjetSelectionne = projet;
    }

    /// <summary>Recharge toutes les données du projet sélectionné.</summary>
    public void Charger()
    {
        Attachements.Clear();
        Lignes.Clear();
        PrixUnitaires.Clear();
        Situations.Clear();
        _toutesLignes = Array.Empty<LigneAttachementResume>();
        AttachementSelectionne = null;
        LigneSelectionnee = null;

        if (ProjetSelectionne is null)
            return;

        var projetId = ProjetSelectionne.Id;
        foreach (var attachement in _attachements.ListerAsync(projetId).GetAwaiter().GetResult())
            Attachements.Add(attachement);

        _toutesLignes = _attachements.ListerLignesAsync(projetId).GetAwaiter().GetResult();
        foreach (var ligne in _toutesLignes)
            Lignes.Add(ligne);

        var marche = _marches.ObtenirAsync(projetId).GetAwaiter().GetResult();
        if (marche is not null)
            foreach (var prix in marche.PrixUnitaires)
                PrixUnitaires.Add(prix);

        foreach (var situation in _situations.ObtenirSuiviAsync(projetId).GetAwaiter().GetResult().Situations)
            Situations.Add(situation);

        if (Attachements.Count > 0)
            AttachementSelectionne = Attachements[0];

        Message = $"{Attachements.Count} attachement(s), {_toutesLignes.Count} ligne(s), {Situations.Count} situation(s).";
        ActualiserCommandes();
        Simuler();
    }

    private void NouvelAttachement()
    {
        if (ProjetSelectionne is null)
            return;

        try
        {
            var cree = _attachements.CreerAsync(
                ProjetSelectionne.Id, DateAttachement,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            AttachementSelectionne = Attachements.FirstOrDefault(a => a.Id == cree.Id);
            Message = $"Attachement du {cree.Date:d} créé en brouillon.";
        }
        catch (Exception ex)
        {
            Message = $"Création impossible : {ex.Message}";
        }
    }

    private void AjouterLigne()
    {
        if (ProjetSelectionne is null || AttachementSelectionne is null || PrixSelectionne is null)
            return;

        try
        {
            var cree = _attachements.AjouterLigneAsync(
                AttachementSelectionne.Id,
                PrixSelectionne.Id,
                LireQuantite(),
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            RecalculerDependances();
            LigneSelectionnee = Lignes.FirstOrDefault(l => l.Id == cree.Id);
            Message = cree.DepasseReliquat
                ? $"⚠ Ligne ajoutée mais la quantité dépasse le reliquat de {Math.Abs(cree.ReliquatApresLigne):N2} {cree.Unite}."
                : $"Ligne {cree.ReferencePrix} ajoutée — montant calculé : {cree.MontantCalcule:N2} DA.";
        }
        catch (Exception ex)
        {
            Message = $"Ajout impossible : {ex.Message}";
        }
    }

    private void ModifierLigne()
    {
        if (LigneSelectionnee is null)
            return;

        try
        {
            var modifiee = _attachements.ModifierQuantiteAsync(
                LigneSelectionnee.Id,
                LireQuantite(),
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            RecalculerDependances();
            LigneSelectionnee = Lignes.FirstOrDefault(l => l.Id == modifiee.Id);
            Message = modifiee.DepasseReliquat
                ? $"⚠ Quantité modifiée mais dépassement du reliquat de {Math.Abs(modifiee.ReliquatApresLigne):N2} {modifiee.Unite}."
                : $"Quantité modifiée — montant calculé : {modifiee.MontantCalcule:N2} DA.";
        }
        catch (Exception ex)
        {
            Message = $"Modification impossible : {ex.Message}";
        }
    }

    private void SupprimerLigne()
    {
        if (LigneSelectionnee is null)
            return;

        try
        {
            _attachements.SupprimerLigneAsync(
                LigneSelectionnee.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            RecalculerDependances();
            Message = "Ligne supprimée de l'attachement.";
        }
        catch (Exception ex)
        {
            Message = $"Suppression impossible : {ex.Message}";
        }
    }

    private void ValiderAttachement()
    {
        if (AttachementSelectionne is null)
            return;

        try
        {
            var valide = _attachements.ChangerStatutAsync(
                AttachementSelectionne.Id,
                StatutAttachement.Valide,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            RecalculerDependances();
            AttachementSelectionne = Attachements.FirstOrDefault(a => a.Id == valide.Id);
            Message = $"Attachement validé — montant : {valide.MontantTotal:N2} DA. Il alimente désormais les situations.";
        }
        catch (Exception ex)
        {
            Message = $"Validation impossible : {ex.Message}";
        }
    }

    /// <summary>
    /// Une ligne modifiée change le cumul de toutes les situations dont la
    /// période inclut l'attachement : les recalculer évite un suivi financier
    /// périmé, puis rafraîchir les alertes et le planning dépendant.
    /// </summary>
    private void RecalculerDependances()
    {
        if (ProjetSelectionne is null)
            return;

        _recalcul.RecalculerDepuisProjetAsync(
            ProjetSelectionne.Id,
            DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
    }

    private void CreerSituation()
    {
        if (ProjetSelectionne is null)
            return;

        try
        {
            var penalites = decimal.TryParse(PenalitesTexte, NumberStyles.Number,
                CultureInfo.CurrentCulture, out var valeur) ? valeur : 0m;
            var situation = _situations.CreerOuRecalculerAsync(
                ProjetSelectionne.Id,
                Periode,
                TypeSituation.Provisoire,
                0.05m,
                0.10m,
                penalites,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            Message = $"Situation {situation.Periode} : cumul {situation.MontantCumule:N2} DA, " +
                      $"retenue {situation.RetenueGarantie:N2} DA, net à payer {situation.NetAPayer:N2} DA.";
        }
        catch (Exception ex)
        {
            Message = $"Création de situation impossible : {ex.Message}";
        }
    }

    private void Simuler(Guid? ligneIdAExclure = null)
    {
        if (ProjetSelectionne is null || PrixSelectionne is null)
        {
            MontantApercu = 0m;
            ReliquatApercu = 0m;
            DepassementApercu = false;
            DepassementTexte = string.Empty;
            return;
        }

        try
        {
            var simulation = _attachements.SimulerLigneAsync(
                ProjetSelectionne.Id,
                PrixSelectionne.Id,
                LireQuantite(),
                ligneIdAExclure).GetAwaiter().GetResult();
            MontantApercu = simulation.MontantCalcule;
            ReliquatApercu = simulation.ReliquatApresLigne;
            DepassementApercu = simulation.DepasseReliquat;
            DepassementTexte = simulation.DepasseReliquat
                ? $"⚠ Dépassement du reliquat contractuel de {Math.Abs(simulation.ReliquatApresLigne):N2} {simulation.Unite}."
                : string.Empty;
        }
        catch (Exception ex)
        {
            Message = $"Aperçu indisponible : {ex.Message}";
        }
    }

    private decimal LireQuantite() =>
        decimal.TryParse(QuantiteTexte, NumberStyles.Number, CultureInfo.CurrentCulture, out var valeur)
            ? valeur
            : 0m;

    private void ActualiserCommandes()
    {
        ((RelayCommand)NouvelAttachementCommand).Actualiser();
        ((RelayCommand)AjouterLigneCommand).Actualiser();
        ((RelayCommand)ModifierLigneCommand).Actualiser();
        ((RelayCommand)SupprimerLigneCommand).Actualiser();
        ((RelayCommand)ValiderAttachementCommand).Actualiser();
        ((RelayCommand)CreerSituationCommand).Actualiser();
    }
}
