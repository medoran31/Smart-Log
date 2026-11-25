using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Registre des courriers entrants/sortants. Les échéances de réponse
/// affichées sont calculées par ICourrierService.
/// </summary>
public class CourriersViewModel : ViewModelBase
{
    private readonly ICourrierService _courriers;
    private readonly IProjetService _projets;
    private readonly IRecalculCroiseService _recalcul;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<CourrierResume> Courriers { get; } = new();
    public ObservableCollection<OptionSens> Sens { get; } = new();

    public ICommand CreerCommand { get; }
    public ICommand EnregistrerReponseCommand { get; }
    public ICommand MarquerSansSuiteCommand { get; }

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

    private CourrierResume? _courrierSelectionne;
    public CourrierResume? CourrierSelectionne
    {
        get => _courrierSelectionne;
        set
        {
            if (!SetField(ref _courrierSelectionne, value))
                return;

            if (value is not null)
            {
                Expediteur = value.Expediteur;
                Destinataire = value.Destinataire;
                Objet = value.Objet;
                DateCourrierTexte = value.DateCourrier.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                DelaiReponseTexte = value.DelaiReponseJours?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                SensSelectionne = Sens.FirstOrDefault(s => s.Valeur == value.Sens);
            }

            ActualiserCommandes();
        }
    }

    private OptionSens? _sensSelectionne;
    public OptionSens? SensSelectionne
    {
        get => _sensSelectionne;
        set => SetField(ref _sensSelectionne, value);
    }

    private string _expediteur = string.Empty;
    public string Expediteur
    {
        get => _expediteur;
        set => SetField(ref _expediteur, value);
    }

    private string _destinataire = string.Empty;
    public string Destinataire
    {
        get => _destinataire;
        set => SetField(ref _destinataire, value);
    }

    private string _objet = string.Empty;
    public string Objet
    {
        get => _objet;
        set => SetField(ref _objet, value);
    }

    private string _dateCourrierTexte = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DateCourrierTexte
    {
        get => _dateCourrierTexte;
        set => SetField(ref _dateCourrierTexte, value);
    }

    private string _delaiReponseTexte = "15";
    public string DelaiReponseTexte
    {
        get => _delaiReponseTexte;
        set => SetField(ref _delaiReponseTexte, value);
    }

    private string _dateReponseTexte = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DateReponseTexte
    {
        get => _dateReponseTexte;
        set => SetField(ref _dateReponseTexte, value);
    }

    private string _message = "Sélectionnez un projet pour afficher son registre courrier.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public CourriersViewModel(
        ICourrierService courriers,
        IProjetService projets,
        IRecalculCroiseService recalcul)
    {
        _courriers = courriers;
        _projets = projets;
        _recalcul = recalcul;

        CreerCommand = new RelayCommand(_ => Creer(), _ => ProjetSelectionne is not null);
        EnregistrerReponseCommand = new RelayCommand(
            _ => EnregistrerReponse(),
            _ => CourrierSelectionne is not null && CourrierSelectionne.Statut == StatutCourrier.EnAttente);
        MarquerSansSuiteCommand = new RelayCommand(
            _ => MarquerSansSuite(),
            _ => CourrierSelectionne is not null && CourrierSelectionne.Statut == StatutCourrier.EnAttente);

        Sens.Add(new OptionSens(SensCourrier.Entrant, "Entrant"));
        Sens.Add(new OptionSens(SensCourrier.Sortant, "Sortant"));
        SensSelectionne = Sens[0];

        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);
        if (Projets.Count > 0)
            ProjetSelectionne = Projets[0];
    }

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
        Courriers.Clear();
        CourrierSelectionne = null;

        if (ProjetSelectionne is not null)
            foreach (var courrier in _courriers.ListerAsync(ProjetSelectionne.Id).GetAwaiter().GetResult())
                Courriers.Add(courrier);

        if (Courriers.Count > 0)
            CourrierSelectionne = Courriers[0];

        Message = $"{Courriers.Count} courrier(s) — " +
                  $"{Courriers.Count(c => c.Statut == StatutCourrier.EnAttente)} en attente, " +
                  $"{Courriers.Count(c => c.EstEnRetard)} hors délai.";
        ActualiserCommandes();
    }

    private void Creer()
    {
        if (ProjetSelectionne is null || !EssayerLireDateCourrier(out var date, out var delai))
            return;

        try
        {
            var courrier = _courriers.CreerAsync(
                ProjetSelectionne.Id,
                SensSelectionne?.Valeur ?? SensCourrier.Entrant,
                Expediteur,
                Destinataire,
                Objet,
                date,
                delai,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            CourrierSelectionne = Courriers.FirstOrDefault(c => c.Id == courrier.Id);
            Message = courrier.EstEnRetard
                ? $"Courrier enregistré : réponse en retard de {-courrier.JoursRestants} jour(s)."
                : "Courrier enregistré dans le registre.";
        }
        catch (Exception ex)
        {
            Message = $"Création impossible : {ex.Message}";
        }
    }

    private void EnregistrerReponse()
    {
        if (CourrierSelectionne is null ||
            !DateOnly.TryParseExact(DateReponseTexte, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dateReponse))
        {
            Message = "La date de réponse doit être saisie au format AAAA-MM-JJ.";
            return;
        }

        try
        {
            var courrier = _courriers.EnregistrerReponseAsync(
                CourrierSelectionne.Id, dateReponse,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            _recalcul.ActualiserAlertesAsync(
                ProjetSelectionne?.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            CourrierSelectionne = Courriers.FirstOrDefault(c => c.Id == courrier.Id);
            Message = $"Réponse du {courrier.DateReponse:d} enregistrée pour « {courrier.Objet} ».";
        }
        catch (Exception ex)
        {
            Message = $"Enregistrement impossible : {ex.Message}";
        }
    }

    private void MarquerSansSuite()
    {
        if (CourrierSelectionne is null)
            return;

        try
        {
            _courriers.MarquerSansSuiteAsync(
                CourrierSelectionne.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            _recalcul.ActualiserAlertesAsync(
                ProjetSelectionne?.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            Message = "Courrier classé sans suite.";
        }
        catch (Exception ex)
        {
            Message = $"Classement impossible : {ex.Message}";
        }
    }

    private bool EssayerLireDateCourrier(out DateOnly date, out int? delai)
    {
        var dateOk = DateOnly.TryParseExact(
            DateCourrierTexte, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
        delai = null;
        var delaiOk = true;
        if (!string.IsNullOrWhiteSpace(DelaiReponseTexte))
        {
            if (int.TryParse(DelaiReponseTexte, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var valeur) && valeur >= 0)
                delai = valeur;
            else
                delaiOk = false;
        }
        if (!dateOk || !delaiOk)
            Message = "Vérifiez la date (AAAA-MM-JJ) et le délai de réponse (entier positif).";
        return dateOk && delaiOk;
    }

    private void ActualiserCommandes()
    {
        ((RelayCommand)CreerCommand).Actualiser();
        ((RelayCommand)EnregistrerReponseCommand).Actualiser();
        ((RelayCommand)MarquerSansSuiteCommand).Actualiser();
    }
}

public sealed record OptionSens(SensCourrier Valeur, string Libelle);
