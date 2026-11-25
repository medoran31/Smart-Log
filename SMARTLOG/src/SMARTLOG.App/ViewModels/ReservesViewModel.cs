using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Suivi des réserves : saisie, cycle de vie et accès au PV de levée.
/// Échéances, ancienneté et libellés sont calculés par IReserveService.
/// </summary>
public class ReservesViewModel : ViewModelBase
{
    private readonly IReserveService _reserves;
    private readonly IProjetService _projets;
    private readonly IRecalculCroiseService _recalcul;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<ReserveResume> Reserves { get; } = new();
    public ObservableCollection<IntervenantResume> Entreprises { get; } = new();
    public ObservableCollection<OptionPriorite> Priorites { get; } = new();

    public ICommand CreerCommand { get; }
    public ICommand EnregistrerCommand { get; }
    public ICommand ChangerStatutCommand { get; }
    public ICommand OuvrirPvCommand { get; }

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

    private ReserveResume? _reserveSelectionnee;
    public ReserveResume? ReserveSelectionnee
    {
        get => _reserveSelectionnee;
        set
        {
            if (!SetField(ref _reserveSelectionnee, value))
                return;

            if (value is not null)
            {
                Description = value.Description;
                DateConstatationTexte = value.DateConstatation.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                DelaiLeveeTexte = value.DelaiLeveeJours.ToString(CultureInfo.InvariantCulture);
                PrioriteSelectionnee = Priorites.FirstOrDefault(p => p.Valeur == value.Priorite);
                EntrepriseSelectionnee = value.EntrepriseResponsableId.HasValue
                    ? Entreprises.FirstOrDefault(e => e.Id == value.EntrepriseResponsableId.Value)
                    : Entreprises.FirstOrDefault();
            }

            ActualiserCommandes();
        }
    }

    private IntervenantResume? _entrepriseSelectionnee;
    public IntervenantResume? EntrepriseSelectionnee
    {
        get => _entrepriseSelectionnee;
        set => SetField(ref _entrepriseSelectionnee, value);
    }

    private OptionPriorite? _prioriteSelectionnee;
    public OptionPriorite? PrioriteSelectionnee
    {
        get => _prioriteSelectionnee;
        set => SetField(ref _prioriteSelectionnee, value);
    }

    private string _description = string.Empty;
    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    private string _dateConstatationTexte = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DateConstatationTexte
    {
        get => _dateConstatationTexte;
        set => SetField(ref _dateConstatationTexte, value);
    }

    private string _delaiLeveeTexte = "15";
    public string DelaiLeveeTexte
    {
        get => _delaiLeveeTexte;
        set => SetField(ref _delaiLeveeTexte, value);
    }

    private string _message = "Sélectionnez un projet pour afficher ses réserves.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private string _prochaineEtape = "—";
    public string ProchaineEtape
    {
        get => _prochaineEtape;
        private set => SetField(ref _prochaineEtape, value);
    }

    private bool _pvDisponible;
    public bool PvDisponible
    {
        get => _pvDisponible;
        private set => SetField(ref _pvDisponible, value);
    }

    public ReservesViewModel(
        IReserveService reserves,
        IProjetService projets,
        IRecalculCroiseService recalcul)
    {
        _reserves = reserves;
        _projets = projets;
        _recalcul = recalcul;

        CreerCommand = new RelayCommand(_ => Creer(), _ => ProjetSelectionne is not null);
        EnregistrerCommand = new RelayCommand(_ => Enregistrer(), _ => ReserveSelectionnee is not null);
        ChangerStatutCommand = new RelayCommand(
            _ => ChangerStatut(),
            _ => ReserveSelectionnee is not null && ReserveSelectionnee.Statut != StatutReserve.Cloturee);
        OuvrirPvCommand = new RelayCommand(
            _ => OuvrirPv(), _ => PvDisponible && ReserveSelectionnee?.CheminPvLevee is not null);

        foreach (var priorite in Enum.GetValues<PrioriteReserve>())
            Priorites.Add(new OptionPriorite(priorite, Libeller(priorite)));
        PrioriteSelectionnee = Priorites.First(p => p.Valeur == PrioriteReserve.Moyenne);

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
        Reserves.Clear();
        Entreprises.Clear();
        ReserveSelectionnee = null;
        PvDisponible = false;
        ProchaineEtape = "—";

        if (ProjetSelectionne is null)
            return;

        foreach (var entreprise in _reserves.ListerEntreprisesAsync(ProjetSelectionne.Id).GetAwaiter().GetResult())
            Entreprises.Add(entreprise);
        if (Entreprises.Count > 0)
            EntrepriseSelectionnee = Entreprises[0];

        foreach (var reserve in _reserves.ListerAsync(ProjetSelectionne.Id).GetAwaiter().GetResult())
            Reserves.Add(reserve);

        if (Reserves.Count > 0)
            ReserveSelectionnee = Reserves[0];

        Message = $"{Reserves.Count} réserve(s) — " +
                  $"{Reserves.Count(r => r.EstEnRetard)} en retard de levée.";
        ActualiserCommandes();
    }

    private void Creer()
    {
        if (ProjetSelectionne is null || !EssayerLireSaisie(out var date, out var delai))
            return;

        try
        {
            var reserve = _reserves.CreerAsync(
                ProjetSelectionne.Id,
                Description,
                PrioriteSelectionnee?.Valeur ?? PrioriteReserve.Moyenne,
                EntrepriseSelectionnee?.Id,
                date,
                delai,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ReserveSelectionnee = Reserves.FirstOrDefault(r => r.Id == reserve.Id);
            Message = reserve.EstEnRetard
                ? $"Réserve n°{reserve.Numero} créée : échéance dépassée de {-reserve.JoursRestants} jour(s)."
                : $"Réserve n°{reserve.Numero} créée : échéance dans {reserve.JoursRestants} jour(s).";
        }
        catch (Exception ex)
        {
            Message = $"Création impossible : {ex.Message}";
        }
    }

    private void Enregistrer()
    {
        if (ReserveSelectionnee is null || !EssayerLireSaisie(out var date, out var delai))
            return;

        try
        {
            var reserve = _reserves.ModifierAsync(
                ReserveSelectionnee.Id,
                Description,
                PrioriteSelectionnee?.Valeur ?? ReserveSelectionnee.Priorite,
                EntrepriseSelectionnee?.Id,
                date,
                delai,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ReserveSelectionnee = Reserves.FirstOrDefault(r => r.Id == reserve.Id);
            Message = $"Réserve n°{reserve.Numero} enregistrée.";
        }
        catch (Exception ex)
        {
            Message = $"Modification impossible : {ex.Message}";
        }
    }

    private void ChangerStatut()
    {
        if (ReserveSelectionnee is null)
            return;

        var ordre = new[]
        {
            StatutReserve.Nouvelle, StatutReserve.EnCours, StatutReserve.EnVerification,
            StatutReserve.Levee, StatutReserve.Cloturee
        };
        var index = Array.IndexOf(ordre, ReserveSelectionnee.Statut);
        if (index < 0 || index + 1 >= ordre.Length)
            return;

        try
        {
            var reserve = _reserves.ChangerStatutAsync(
                ReserveSelectionnee.Id,
                ordre[index + 1],
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            _recalcul.ActualiserAlertesAsync(
                ProjetSelectionne?.Id,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ReserveSelectionnee = Reserves.FirstOrDefault(r => r.Id == reserve.Id);
            Message = reserve.Statut == StatutReserve.Levee
                ? $"Réserve n°{reserve.Numero} levée — PV généré dans la GED."
                : $"Réserve n°{reserve.Numero} : statut « {reserve.StatutLibelle} ».";
        }
        catch (Exception ex)
        {
            Message = $"Changement de statut impossible : {ex.Message}";
        }
    }

    private void OuvrirPv()
    {
        var chemin = ReserveSelectionnee?.CheminPvLevee;
        if (chemin is null || !File.Exists(chemin))
        {
            Message = "Le PV de levée est introuvable sur le disque.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(chemin) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Message = $"Ouverture du PV impossible : {ex.Message}";
        }
    }

    private bool EssayerLireSaisie(out DateOnly date, out int delai)
    {
        var dateOk = DateOnly.TryParseExact(
            DateConstatationTexte, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date);
        var delaiOk = int.TryParse(DelaiLeveeTexte, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out delai) && delai > 0;
        if (!dateOk || !delaiOk)
            Message = "Vérifiez la date (AAAA-MM-JJ) et le délai de levée (entier positif).";
        return dateOk && delaiOk;
    }

    private void ActualiserCommandes()
    {
        ((RelayCommand)CreerCommand).Actualiser();
        ((RelayCommand)EnregistrerCommand).Actualiser();
        ((RelayCommand)ChangerStatutCommand).Actualiser();
        ((RelayCommand)OuvrirPvCommand).Actualiser();

        if (ReserveSelectionnee is null)
        {
            ProchaineEtape = "—";
            PvDisponible = false;
            return;
        }

        var ordre = new[]
        {
            StatutReserve.Nouvelle, StatutReserve.EnCours, StatutReserve.EnVerification,
            StatutReserve.Levee, StatutReserve.Cloturee
        };
        var index = Array.IndexOf(ordre, ReserveSelectionnee.Statut);
        ProchaineEtape = index >= 0 && index + 1 < ordre.Length
            ? Libeller(ordre[index + 1])
            : "Terminée";
        PvDisponible = !string.IsNullOrWhiteSpace(ReserveSelectionnee.CheminPvLevee);
    }

    private static string Libeller(PrioriteReserve priorite) => priorite switch
    {
        PrioriteReserve.Basse => "Basse",
        PrioriteReserve.Moyenne => "Moyenne",
        PrioriteReserve.Haute => "Haute",
        _ => priorite.ToString()
    };

    private static string Libeller(StatutReserve statut) => statut switch
    {
        StatutReserve.Nouvelle => "Nouvelle",
        StatutReserve.EnCours => "En cours",
        StatutReserve.EnVerification => "En vérification",
        StatutReserve.Levee => "Levée",
        StatutReserve.Cloturee => "Clôturée",
        _ => statut.ToString()
    };
}

public sealed record OptionPriorite(PrioriteReserve Valeur, string Libelle);
