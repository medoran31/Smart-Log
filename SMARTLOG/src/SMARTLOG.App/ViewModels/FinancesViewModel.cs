using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Domain.Enums;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Vue du suivi financier. Les séries du graphique et tous les indicateurs
/// sont calculés par ISituationService ; la vue ne fait que les projeter dans
/// un repère WPF natif (aucun calcul métier ici).
/// </summary>
public class FinancesViewModel : ViewModelBase
{
    public const double LargeurGraphe = 700;
    public const double HauteurGraphe = 270;
    private const double MargeGauche = 50;
    private const double MargeDroite = 20;
    private const double MargeHaut = 20;
    private const double MargeBas = 40;

    private readonly IProjetService _projets;
    private readonly ISituationService _situations;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<SituationResume> Situations { get; } = new();
    public ObservableCollection<PointCourbe> PointsEngages { get; } = new();
    public ObservableCollection<PointCourbe> PointsPayes { get; } = new();
    public ObservableCollection<PointCourbe> PointsRestant { get; } = new();
    public ObservableCollection<LigneCourbe> SegmentsEngages { get; } = new();
    public ObservableCollection<LigneCourbe> SegmentsPayes { get; } = new();
    public ObservableCollection<LigneCourbe> SegmentsRestant { get; } = new();
    public ObservableCollection<LigneGrille> GrilleHorizontale { get; } = new();
    public ObservableCollection<LibelleAxe> LibellesHorizontaux { get; } = new();
    public ObservableCollection<LibelleAxe> LibellesVerticaux { get; } = new();

    public ICommand ActualiserCommand { get; }
    public ICommand MarquerEmiseCommand { get; }
    public ICommand MarquerPayeeCommand { get; }

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

    private SituationResume? _situationSelectionnee;
    public SituationResume? SituationSelectionnee
    {
        get => _situationSelectionnee;
        set
        {
            if (SetField(ref _situationSelectionnee, value))
            {
                ((RelayCommand)MarquerEmiseCommand).Actualiser();
                ((RelayCommand)MarquerPayeeCommand).Actualiser();
            }
        }
    }

    private decimal _montantContractuel;
    public decimal MontantContractuel
    {
        get => _montantContractuel;
        private set => SetField(ref _montantContractuel, value);
    }

    private decimal _totalEngage;
    public decimal TotalEngage
    {
        get => _totalEngage;
        private set => SetField(ref _totalEngage, value);
    }

    private decimal _totalPaye;
    public decimal TotalPaye
    {
        get => _totalPaye;
        private set => SetField(ref _totalPaye, value);
    }

    private decimal _totalRestantDu;
    public decimal TotalRestantDu
    {
        get => _totalRestantDu;
        private set => SetField(ref _totalRestantDu, value);
    }

    private decimal _tauxAvancement;
    public decimal TauxAvancement
    {
        get => _tauxAvancement;
        private set => SetField(ref _tauxAvancement, value);
    }

    private string _maximumAxeLibelle = string.Empty;
    public string MaximumAxeLibelle
    {
        get => _maximumAxeLibelle;
        private set => SetField(ref _maximumAxeLibelle, value);
    }

    private string _message = "Sélectionnez un projet pour afficher son suivi financier.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public FinancesViewModel(IProjetService projets, ISituationService situations)
    {
        _projets = projets;
        _situations = situations;

        ActualiserCommand = new RelayCommand(_ => Charger(), _ => ProjetSelectionne is not null);
        MarquerEmiseCommand = new RelayCommand(
            _ => ChangerStatut(StatutSituation.Emise), _ => SituationSelectionnee is not null);
        MarquerPayeeCommand = new RelayCommand(
            _ => ChangerStatut(StatutSituation.Payee), _ => SituationSelectionnee is not null);

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

    /// <summary>Recharge le suivi et reconstruit les séries du graphique.</summary>
    public void Charger()
    {
        PointsEngages.Clear();
        PointsPayes.Clear();
        PointsRestant.Clear();
        SegmentsEngages.Clear();
        SegmentsPayes.Clear();
        SegmentsRestant.Clear();
        GrilleHorizontale.Clear();
        LibellesHorizontaux.Clear();
        LibellesVerticaux.Clear();
        Situations.Clear();

        if (ProjetSelectionne is null)
            return;

        var suivi = _situations.ObtenirSuiviAsync(ProjetSelectionne.Id).GetAwaiter().GetResult();

        MontantContractuel = suivi.MontantContractuel;
        TotalEngage = suivi.TotalEngage;
        TotalPaye = suivi.TotalPaye;
        TotalRestantDu = suivi.TotalRestantDu;
        TauxAvancement = suivi.TauxAvancement;

        foreach (var situation in suivi.Situations)
            Situations.Add(situation);

        ConstruireGraphique(suivi);
        Message = suivi.Evolution.Count == 0
            ? "Aucune situation : créez un attachement validé puis générez une situation dans « Attachements et situations »."
            : $"{suivi.Evolution.Count} situation(s) — dernier point {suivi.Evolution[^1].Periode}.";

        ((RelayCommand)ActualiserCommand).Actualiser();
    }

    private void ConstruireGraphique(SuiviFinancier suivi)
    {
        var maximum = DeterminerMaximum(suivi.Evolution);
        MaximumAxeLibelle = $"{maximum:N0} DA";

        var largeurUtile = LargeurGraphe - MargeGauche - MargeDroite;
        var hauteurUtile = HauteurGraphe - MargeHaut - MargeBas;

        // Grille horizontale : 5 paliers de 0 à maximum.
        for (var i = 0; i <= 4; i++)
        {
            var y = MargeHaut + hauteurUtile * i / 4.0;
            GrilleHorizontale.Add(new LigneGrille(MargeGauche, y, LargeurGraphe - MargeDroite, y));
            var valeur = maximum * (4 - i) / 4m;
            LibellesVerticaux.Add(new LibelleAxe(4, y - 8, $"{valeur:N0}"));
        }

        var points = suivi.Evolution;
        for (var i = 0; i < points.Count; i++)
        {
            var x = points.Count == 1
                ? MargeGauche + largeurUtile / 2.0
                : MargeGauche + largeurUtile * i / (points.Count - 1);

            PointsEngages.Add(new PointCourbe(x, Y(points[i].MontantEngage, maximum, hauteurUtile)));
            PointsPayes.Add(new PointCourbe(x, Y(points[i].MontantPaye, maximum, hauteurUtile)));
            PointsRestant.Add(new PointCourbe(x, Y(points[i].RestantDu, maximum, hauteurUtile)));
            LibellesHorizontaux.Add(new LibelleAxe(x - 20, MargeHaut + hauteurUtile + 10, points[i].Periode));
            GrilleHorizontale.Add(new LigneGrille(x, MargeHaut, x, MargeHaut + hauteurUtile));
        }

        AjouterSegments(PointsEngages, SegmentsEngages);
        AjouterSegments(PointsPayes, SegmentsPayes);
        AjouterSegments(PointsRestant, SegmentsRestant);
    }

    private static void AjouterSegments(
        IReadOnlyList<PointCourbe> points, ICollection<LigneCourbe> segments)
    {
        for (var i = 1; i < points.Count; i++)
            segments.Add(new LigneCourbe(points[i - 1].X, points[i - 1].Y, points[i].X, points[i].Y));
    }

    private static double Y(decimal valeur, decimal maximum, double hauteurUtile)
    {
        var rapport = maximum <= 0m ? 0d : (double)(valeur / maximum);
        rapport = Math.Min(1d, Math.Max(0d, rapport));
        return MargeHaut + hauteurUtile * (1 - rapport);
    }

    private static decimal DeterminerMaximum(IReadOnlyList<PointEvolution> points)
    {
        var plusGrand = points.Count == 0
            ? 0m
            : points.Max(p => Math.Max(p.MontantEngage, Math.Max(p.MontantPaye, p.RestantDu)));

        if (plusGrand <= 0m)
            return 100m;

        var pas = 1m;
        while (plusGrand / pas > 5m)
            pas *= 10m;
        return Math.Ceiling(plusGrand / pas) * pas;
    }

    private void ChangerStatut(StatutSituation statut)
    {
        if (SituationSelectionnee is null)
            return;

        try
        {
            var modifiee = _situations.ChangerStatutAsync(
                SituationSelectionnee.Id,
                statut,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            SituationSelectionnee = Situations.FirstOrDefault(s => s.Id == modifiee.Id);
            Message = $"Situation {modifiee.Periode} : statut « {modifiee.Statut} », net à payer {modifiee.NetAPayer:N2} DA.";
        }
        catch (Exception ex)
        {
            Message = $"Changement de statut impossible : {ex.Message}";
        }
    }
}

/// <summary>Point d'une série, en coordonnées du Canvas du graphique.</summary>
public sealed record PointCourbe(double X, double Y);

public sealed record LigneGrille(double X1, double Y1, double X2, double Y2);

public sealed record LigneCourbe(double X1, double Y1, double X2, double Y2);

public sealed record LibelleAxe(double X, double Y, string Texte);
