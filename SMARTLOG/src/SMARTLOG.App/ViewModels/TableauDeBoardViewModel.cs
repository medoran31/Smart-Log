using System.Collections.ObjectModel;
using System.Windows.Input;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// ViewModel du tableau de bord : indicateurs, alertes, échéances, avancement
/// financier et chemin critique du projet actif. Toutes les valeurs proviennent
/// des services ; aucune donnée n'est calculée dans la vue.
/// </summary>
public class TableauDeBoardViewModel : ViewModelBase
{
    private readonly ProjetsViewModel _projetsViewModel;
    private readonly IAlerteService _alerteService;
    private readonly IPlanificationService _planificationService;
    private readonly ISituationService _situationService;

    private bool _aProjetOuvert;
    private bool _aDesAlertes;
    private bool _aDesEcheances;
    private string _nomProjet = string.Empty;
    private string _sousTitreProjet = string.Empty;
    private decimal _tauxAvancement;
    private decimal _montantInitial;
    private decimal _montantActualise;
    private decimal _totalPaye;
    private decimal _restantDu;
    private int _dureeProjetJours;
    private int _nombreTaches;
    private int _nombreTachesCritiques;

    public ObservableCollection<KpiCard> KpiCards { get; } = new();
    public ObservableCollection<AlerteCarte> AlertesRecentes { get; } = new();
    public ObservableCollection<ProchaineEcheance> ProchainesEcheances { get; } = new();
    public ObservableCollection<TacheCarte> TachesCritiques { get; } = new();

    public ICommand RafraichirCommand { get; }

    public TableauDeBoardViewModel(
        ProjetsViewModel projetsViewModel,
        IAlerteService alerteService,
        IPlanificationService planificationService,
        ISituationService situationService)
    {
        _projetsViewModel = projetsViewModel;
        _alerteService = alerteService;
        _planificationService = planificationService;
        _situationService = situationService;

        RafraichirCommand = new RelayCommand(_ => ChargerPourProjetActif());
    }

    public bool AProjetOuvert
    {
        get => _aProjetOuvert;
        private set
        {
            if (SetField(ref _aProjetOuvert, value))
                Notifier(nameof(AInverseProjetOuvert));
        }
    }

    public bool AInverseProjetOuvert => !AProjetOuvert;

    public bool ADesAlertes
    {
        get => _aDesAlertes;
        private set => SetField(ref _aDesAlertes, value);
    }

    public bool ADesEcheances
    {
        get => _aDesEcheances;
        private set => SetField(ref _aDesEcheances, value);
    }

    public string NomProjet
    {
        get => _nomProjet;
        private set => SetField(ref _nomProjet, value);
    }

    public string SousTitreProjet
    {
        get => _sousTitreProjet;
        private set => SetField(ref _sousTitreProjet, value);
    }

    public decimal TauxAvancement
    {
        get => _tauxAvancement;
        private set => SetField(ref _tauxAvancement, value);
    }

    public decimal MontantInitial
    {
        get => _montantInitial;
        private set => SetField(ref _montantInitial, value);
    }

    public decimal MontantActualise
    {
        get => _montantActualise;
        private set => SetField(ref _montantActualise, value);
    }

    public decimal TotalPaye
    {
        get => _totalPaye;
        private set => SetField(ref _totalPaye, value);
    }

    public decimal RestantDu
    {
        get => _restantDu;
        private set => SetField(ref _restantDu, value);
    }

    public int DureeProjetJours
    {
        get => _dureeProjetJours;
        private set => SetField(ref _dureeProjetJours, value);
    }

    public int NombreTaches
    {
        get => _nombreTaches;
        private set
        {
            if (SetField(ref _nombreTaches, value))
                Notifier(nameof(ResumeTachesCritiques));
        }
    }

    public int NombreTachesCritiques
    {
        get => _nombreTachesCritiques;
        private set
        {
            if (SetField(ref _nombreTachesCritiques, value))
                Notifier(nameof(ResumeTachesCritiques));
        }
    }

    public string ResumeTachesCritiques => $"{NombreTachesCritiques} sur {NombreTaches}";

    /// <summary>Recharge le tableau de bord pour le projet sélectionné, ou le premier projet disponible.</summary>
    public void ChargerPourProjetActif()
    {
        var projet = _projetsViewModel.ProjetSelectionne
                     ?? _projetsViewModel.Projets.FirstOrDefault();

        Vider();
        AProjetOuvert = projet is not null;
        if (projet is null)
            return;

        NomProjet = projet.Intitule;
        SousTitreProjet = $"{projet.Code} · {projet.Statut}";

        var planning = _planificationService.ObtenirAsync(projet.Id).GetAwaiter().GetResult();
        var suivi = _situationService.ObtenirSuiviAsync(projet.Id).GetAwaiter().GetResult();
        var centre = _alerteService.ObtenirCentreAsync(projet.Id, true).GetAwaiter().GetResult();

        DureeProjetJours = planning.DureeProjetJours;
        NombreTaches = planning.NombreTaches;
        NombreTachesCritiques = planning.NombreTachesCritiques;

        MontantInitial = projet.MontantInitial;
        MontantActualise = projet.MontantActualise;
        TotalPaye = suivi.TotalPaye;
        RestantDu = suivi.TotalRestantDu;
        TauxAvancement = Math.Round(suivi.TauxAvancement * 100m, 1);

        ChargerKpi(projet, centre.Alertes.Count);
        ChargerAlertes(centre.Alertes);
        ChargerEcheances(planning);
        ChargerTachesCritiques(planning);
    }

    private void Vider()
    {
        KpiCards.Clear();
        AlertesRecentes.Clear();
        ProchainesEcheances.Clear();
        TachesCritiques.Clear();
        ADesAlertes = false;
        ADesEcheances = false;
        NomProjet = string.Empty;
        SousTitreProjet = string.Empty;
        TauxAvancement = 0m;
        MontantInitial = 0m;
        MontantActualise = 0m;
        TotalPaye = 0m;
        RestantDu = 0m;
        DureeProjetJours = 0;
        NombreTaches = 0;
        NombreTachesCritiques = 0;
    }

    private void ChargerKpi(ProjetResume projet, int nombreAlertes)
    {
        KpiCards.Add(new KpiCard("\uE8C7", $"{projet.MontantActualise:N0} DA", "Montant actualisé", "#2E7D6B"));
        KpiCards.Add(new KpiCard("\uE9D2", $"{TauxAvancement:N1} %", "Avancement financier", "#2E7D6B"));
        KpiCards.Add(new KpiCard("\uE787", $"{projet.DelaiActualiseJours} j", "Délai actualisé", "#1B2733"));
        KpiCards.Add(new KpiCard("\uE9D5", $"{NombreTachesCritiques}/{NombreTaches}", "Tâches critiques", "#D97706"));
        KpiCards.Add(new KpiCard(
            "\uE7BA",
            $"{nombreAlertes}",
            "Alertes en cours",
            nombreAlertes > 0 ? "#C0392B" : "#2E7D6B"));
    }

    private void ChargerAlertes(IReadOnlyList<AlerteResume> alertes)
    {
        foreach (var alerte in alertes.Take(5))
        {
            var echeance = alerte.EstEnRetard
                ? $"En retard de {-alerte.JoursRestants} j"
                : $"Échéance : {alerte.DateEcheance:dd/MM/yyyy}";

            AlertesRecentes.Add(new AlerteCarte(
                IconeSource(alerte.SourceType),
                alerte.Titre,
                alerte.Message,
                alerte.PrioriteLibelle,
                CouleurPriorite(alerte.Priorite),
                echeance));
        }

        ADesAlertes = alertes.Count > 0;
    }

    private void ChargerEcheances(PlanningDetail planning)
    {
        foreach (var idTache in planning.TachesCritiques.Take(5))
        {
            var tache = planning.Taches.FirstOrDefault(t => t.Id == idTache);
            if (tache is null)
                continue;

            ProchainesEcheances.Add(new ProchaineEcheance(
                "\uE787",
                tache.Libelle,
                $"Avancement : {tache.AvancementPct:N0} % · {tache.DureeJours} j",
                "Chemin critique",
                $"Fin prévue : {tache.DateFinPrevue:dd/MM/yyyy}"));
        }

        ADesEcheances = ProchainesEcheances.Count > 0;
    }

    private void ChargerTachesCritiques(PlanningDetail planning)
    {
        foreach (var idTache in planning.TachesCritiques.Take(6))
        {
            var tache = planning.Taches.FirstOrDefault(t => t.Id == idTache);
            if (tache is null)
                continue;

            TachesCritiques.Add(new TacheCarte(
                tache.Libelle,
                $"{tache.DateDebutPrevue:dd/MM/yyyy} → {tache.DateFinPrevue:dd/MM/yyyy} · {tache.AvancementPct:N0} %"));
        }
    }

    private static string IconeSource(string sourceType) => sourceType switch
    {
        "Reserve" => "\uE7BA",
        "Tache" => "\uE9D5",
        "Courrier" => "\uE715",
        "Projet" => "\uE8FC",
        _ => "\uE945"
    };

    private static string CouleurPriorite(PrioriteAlerte priorite) => priorite switch
    {
        PrioriteAlerte.Haute => "#C0392B",
        PrioriteAlerte.Moyenne => "#D97706",
        _ => "#2E7D6B"
    };
}

/// <summary>Représente une carte KPI affichée sur le tableau de bord.</summary>
public sealed class KpiCard
{
    public string Icone { get; }
    public string Valeur { get; }
    public string Libelle { get; }
    public string Couleur { get; }

    public KpiCard(string icone, string valeur, string libelle, string couleur)
    {
        Icone = icone;
        Valeur = valeur;
        Libelle = libelle;
        Couleur = couleur;
    }
}

/// <summary>Alerte formatée pour une carte du tableau de bord.</summary>
public sealed class AlerteCarte
{
    public string Icone { get; }
    public string Titre { get; }
    public string Description { get; }
    public string PrioriteTexte { get; }
    public string PrioriteCouleur { get; }
    public string EcheanceTexte { get; }

    public AlerteCarte(
        string icone,
        string titre,
        string description,
        string prioriteTexte,
        string prioriteCouleur,
        string echeanceTexte)
    {
        Icone = icone;
        Titre = titre;
        Description = description;
        PrioriteTexte = prioriteTexte;
        PrioriteCouleur = prioriteCouleur;
        EcheanceTexte = echeanceTexte;
    }
}

/// <summary>Représente une prochaine échéance dans le tableau de bord.</summary>
public sealed class ProchaineEcheance
{
    public string TypeIcone { get; }
    public string Libelle { get; }
    public string Description { get; }
    public string ContexteProjet { get; }
    public string DateTexte { get; }

    public ProchaineEcheance(string icone, string libelle, string description, string contexteProjet, string dateTexte)
    {
        TypeIcone = icone;
        Libelle = libelle;
        Description = description;
        ContexteProjet = contexteProjet;
        DateTexte = dateTexte;
    }
}

/// <summary>Tâche critique affichée dans le panneau planning.</summary>
public sealed class TacheCarte
{
    public string Libelle { get; }
    public string Periode { get; }

    public TacheCarte(string libelle, string periode)
    {
        Libelle = libelle;
        Periode = periode;
    }
}
