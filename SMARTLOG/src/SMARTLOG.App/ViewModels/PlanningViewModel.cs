using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Smartlog.Data;
using Smartlog.Domain.Entities;
using Smartlog.Services;

namespace Smartlog.App.ViewModels;

/// <summary>
/// Vue Gantt. Toutes les dates, marges et flags de chemin critique sont
/// calculés par IPlanificationService ; le ViewModel ne fait que positionner
/// les barres dans un repère Canvas natif.
/// </summary>
public class PlanningViewModel : ViewModelBase
{
    private const int PixelsParJour = 12;
    private const int LargeurMinimum = 720;

    private readonly IPlanificationService _planification;
    private readonly IProjetService _projets;
    private readonly IRecalculCroiseService _recalcul;

    public ObservableCollection<ProjetResume> Projets { get; } = new();
    public ObservableCollection<NoeudTache> Racines { get; } = new();
    public ObservableCollection<NoeudTache> ToutesTaches { get; } = new();
    public ObservableCollection<NoeudTache> ParentsPossibles { get; } = new();
    public ObservableCollection<LiaisonResume> Liaisons { get; } = new();
    public ObservableCollection<OptionLiaison> TypesLiaison { get; } = new();
    public ObservableCollection<LibelleAxe> Echelle { get; } = new();
    public ObservableCollection<LigneGrille> Grille { get; } = new();

    public ICommand CreerTacheCommand { get; }
    public ICommand EnregistrerTacheCommand { get; }
    public ICommand SupprimerTacheCommand { get; }
    public ICommand SelectionnerTacheCommand { get; }
    public ICommand AjouterLiaisonCommand { get; }
    public ICommand SupprimerLiaisonCommand { get; }
    public ICommand RecalculerCommand { get; }

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

    private NoeudTache? _tacheSelectionnee;
    public NoeudTache? TacheSelectionnee
    {
        get => _tacheSelectionnee;
        set
        {
            if (!SetField(ref _tacheSelectionnee, value))
                return;

            foreach (var noeud in ToutesTaches)
                noeud.EstSelectionnee = ReferenceEquals(noeud, value);

            if (value is not null)
            {
                LibelleTache = value.Libelle;
                DateDebutTexte = value.Tache.DateDebutPrevue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                DateFinTexte = value.Tache.DateFinPrevue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                ParentSelectionne = value.Tache.ParentTacheId.HasValue
                    ? ToutesTaches.FirstOrDefault(n => n.Id == value.Tache.ParentTacheId.Value)
                    : ParentsPossibles.FirstOrDefault();
            }

            ActualiserCommandes();
        }
    }

    private NoeudTache? _parentSelectionne;
    public NoeudTache? ParentSelectionne
    {
        get => _parentSelectionne;
        set => SetField(ref _parentSelectionne, value);
    }

    private NoeudTache? _predecesseurSelectionne;
    public NoeudTache? PredecesseurSelectionne
    {
        get => _predecesseurSelectionne;
        set
        {
            if (SetField(ref _predecesseurSelectionne, value))
                ((RelayCommand)AjouterLiaisonCommand).Actualiser();
        }
    }

    private NoeudTache? _successeurSelectionne;
    public NoeudTache? SuccesseurSelectionne
    {
        get => _successeurSelectionne;
        set
        {
            if (SetField(ref _successeurSelectionne, value))
                ((RelayCommand)AjouterLiaisonCommand).Actualiser();
        }
    }

    private OptionLiaison? _typeLiaisonSelectionne;
    public OptionLiaison? TypeLiaisonSelectionne
    {
        get => _typeLiaisonSelectionne;
        set => SetField(ref _typeLiaisonSelectionne, value);
    }

    private LiaisonResume? _liaisonSelectionnee;
    public LiaisonResume? LiaisonSelectionnee
    {
        get => _liaisonSelectionnee;
        set
        {
            if (SetField(ref _liaisonSelectionnee, value))
                ((RelayCommand)SupprimerLiaisonCommand).Actualiser();
        }
    }

    private string _libelleTache = string.Empty;
    public string LibelleTache
    {
        get => _libelleTache;
        set => SetField(ref _libelleTache, value);
    }

    private string _dateDebutTexte = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DateDebutTexte
    {
        get => _dateDebutTexte;
        set => SetField(ref _dateDebutTexte, value);
    }

    private string _dateFinTexte = DateOnly.FromDateTime(DateTime.Now).AddDays(2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public string DateFinTexte
    {
        get => _dateFinTexte;
        set => SetField(ref _dateFinTexte, value);
    }

    private string _message = "Sélectionnez un projet pour afficher son planning.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private int _nombreTachesCritiques;
    public int NombreTachesCritiques
    {
        get => _nombreTachesCritiques;
        private set => SetField(ref _nombreTachesCritiques, value);
    }

    private int _dureeProjetJours;
    public int DureeProjetJours
    {
        get => _dureeProjetJours;
        private set => SetField(ref _dureeProjetJours, value);
    }

    private DateOnly _dateDebutProjet;
    public DateOnly DateDebutProjet
    {
        get => _dateDebutProjet;
        private set => SetField(ref _dateDebutProjet, value);
    }

    private DateOnly _dateFinProjet;
    public DateOnly DateFinProjet
    {
        get => _dateFinProjet;
        private set => SetField(ref _dateFinProjet, value);
    }

    private double _largeurGraphe = LargeurMinimum;
    public double LargeurGraphe
    {
        get => _largeurGraphe;
        private set => SetField(ref _largeurGraphe, value);
    }

    public PlanningViewModel(
        IPlanificationService planification,
        IProjetService projets,
        IRecalculCroiseService recalcul)
    {
        _planification = planification;
        _projets = projets;
        _recalcul = recalcul;

        CreerTacheCommand = new RelayCommand(_ => CreerTache(), _ => ProjetSelectionne is not null);
        EnregistrerTacheCommand = new RelayCommand(_ => EnregistrerTache(), _ => TacheSelectionnee is not null);
        SupprimerTacheCommand = new RelayCommand(_ => SupprimerTache(), _ => TacheSelectionnee is not null);
        SelectionnerTacheCommand = new RelayCommand(noeud =>
        {
            if (noeud is NoeudTache tache && tache.Id != Guid.Empty)
                TacheSelectionnee = tache;
        });
        AjouterLiaisonCommand = new RelayCommand(
            _ => AjouterLiaison(),
            _ => PredecesseurSelectionne is not null && SuccesseurSelectionne is not null &&
                 !ReferenceEquals(PredecesseurSelectionne, SuccesseurSelectionne));
        SupprimerLiaisonCommand = new RelayCommand(_ => SupprimerLiaison(), _ => LiaisonSelectionnee is not null);
        RecalculerCommand = new RelayCommand(_ => Recalculer(), _ => ProjetSelectionne is not null);

        foreach (var type in Enum.GetValues<TypeLiaison>())
            TypesLiaison.Add(new OptionLiaison(type, LibellerType(type)));
        TypeLiaisonSelectionne = TypesLiaison.First();

        foreach (var projet in _projets.ListerAsync().GetAwaiter().GetResult())
            Projets.Add(projet);

        if (Projets.Count > 0)
            ProjetSelectionne = Projets[0];
    }

    /// <summary>Sélectionne un projet depuis la fiche projet.</summary>
    public void SelectionnerProjet(Guid projetId)
    {
        var projet = Projets.FirstOrDefault(p => p.Id == projetId);
        if (projet is not null)
            ProjetSelectionne = projet;
    }

    /// <summary>Recharge le planning et reconstruit l'arborescence et l'échelle.</summary>
    public void Charger()
    {
        Racines.Clear();
        ToutesTaches.Clear();
        ParentsPossibles.Clear();
        Liaisons.Clear();
        Echelle.Clear();
        Grille.Clear();
        TacheSelectionnee = null;
        LiaisonSelectionnee = null;

        ParentsPossibles.Add(new NoeudTache(Guid.Empty, "— Racine —", 0));

        if (ProjetSelectionne is null)
            return;

        var planning = _planification.ObtenirAsync(ProjetSelectionne.Id).GetAwaiter().GetResult();
        var noeuds = new Dictionary<Guid, NoeudTache>();

        foreach (var tache in planning.Taches)
            noeuds[tache.Id] = new NoeudTache(tache);

        foreach (var tache in planning.Taches)
        {
            var noeud = noeuds[tache.Id];
            ToutesTaches.Add(noeud);
            ParentsPossibles.Add(noeud);
            if (tache.ParentTacheId.HasValue && noeuds.TryGetValue(tache.ParentTacheId.Value, out var parent))
                parent.SousTaches.Add(noeud);
            else
                Racines.Add(noeud);
        }

        foreach (var liaison in planning.Liaisons)
            Liaisons.Add(liaison);

        DateDebutProjet = planning.DateDebutProjet;
        DateFinProjet = planning.DateFinProjet;
        DureeProjetJours = planning.DureeProjetJours;
        NombreTachesCritiques = planning.NombreTachesCritiques;
        LargeurGraphe = Math.Max(LargeurMinimum, (DureeProjetJours + 3) * PixelsParJour + 16);

        if (planning.Taches.Count > 0 && DateDebutProjet != DateOnly.MinValue)
        {
            var debut = DateDebutProjet.DayNumber;
            foreach (var noeud in noeuds.Values)
                noeud.Positionner(debut, PixelsParJour);

            var mois = new DateOnly(DateDebutProjet.Year, DateDebutProjet.Month, 1);
            while (mois <= DateFinProjet)
            {
                var x = (mois.DayNumber - debut) * PixelsParJour + 8;
                if (x >= 0)
                {
                    Echelle.Add(new LibelleAxe(x, 0, mois.ToString("MM/yyyy", CultureInfo.InvariantCulture)));
                    Grille.Add(new LigneGrille(x, 0, x, 30));
                }
                mois = mois.AddMonths(1);
            }
        }

        if (ToutesTaches.Count > 0)
            TacheSelectionnee = ToutesTaches[0];

        Message = $"{planning.NombreTaches} tâche(s), {planning.NombreLiaisons} liaison(s), " +
                  $"{NombreTachesCritiques} tâche(s) critique(s) sur {DureeProjetJours} jour(s).";
        ActualiserCommandes();
    }

    /// <summary>Les échéances du planning alimentent le centre d'alertes.</summary>
    private void ActualiserAlertes()
    {
        _recalcul.ActualiserAlertesAsync(
            ProjetSelectionne?.Id,
            DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
    }

    private void CreerTache()
    {
        if (ProjetSelectionne is null || !EssayerLireDates(out var debut, out var fin))
            return;

        try
        {
            var parentId = ParentSelectionne is not null && ParentSelectionne.Id != Guid.Empty
                ? ParentSelectionne.Id
                : (Guid?)null;
            var cree = _planification.CreerTacheAsync(
                ProjetSelectionne.Id, parentId, LibelleTache, debut, fin,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            TacheSelectionnee = ToutesTaches.FirstOrDefault(n => n.Id == cree.Id);
            Message = cree.EstCheminCritique
                ? $"Tâche « {cree.Libelle} » créée : elle appartient au chemin critique."
                : $"Tâche « {cree.Libelle} » créée (marge {cree.MargeTotale} jour(s)).";
        }
        catch (Exception ex)
        {
            Message = $"Création impossible : {ex.Message}";
        }
    }

    private void EnregistrerTache()
    {
        if (TacheSelectionnee is null || !EssayerLireDates(out var debut, out var fin))
            return;

        try
        {
            var modifiee = _planification.ModifierTacheAsync(
                TacheSelectionnee.Id, LibelleTache, debut, fin,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            TacheSelectionnee = ToutesTaches.FirstOrDefault(n => n.Id == modifiee.Id);
            Message = $"Tâche « {modifiee.Libelle} » enregistrée — planning recalculé en cascade " +
                      $"(fin du projet : {modifiee.DateFinPrevue:d}).";
        }
        catch (Exception ex)
        {
            Message = $"Modification impossible : {ex.Message}";
        }
    }

    private void SupprimerTache()
    {
        if (TacheSelectionnee is null || TacheSelectionnee.Id == Guid.Empty)
            return;

        try
        {
            _planification.SupprimerTacheAsync(
                TacheSelectionnee.Id, DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            Message = "Tâche supprimée et planning recalculé.";
        }
        catch (Exception ex)
        {
            Message = $"Suppression impossible : {ex.Message}";
        }
    }

    private void AjouterLiaison()
    {
        if (PredecesseurSelectionne is null || SuccesseurSelectionne is null || TypeLiaisonSelectionne is null)
            return;

        try
        {
            _planification.AjouterLiaisonAsync(
                PredecesseurSelectionne.Id,
                SuccesseurSelectionne.Id,
                TypeLiaisonSelectionne.Valeur,
                DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            Message = $"Liaison {TypeLiaisonSelectionne.Libelle} ajoutée : chemin critique recalculé.";
        }
        catch (Exception ex)
        {
            Message = $"Ajout de liaison impossible : {ex.Message}";
        }
    }

    private void SupprimerLiaison()
    {
        if (LiaisonSelectionnee is null)
            return;

        try
        {
            _planification.SupprimerLiaisonAsync(
                LiaisonSelectionnee.Id, DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            Message = "Liaison supprimée et planning recalculé.";
        }
        catch (Exception ex)
        {
            Message = $"Suppression impossible : {ex.Message}";
        }
    }

    private void Recalculer()
    {
        if (ProjetSelectionne is null)
            return;

        try
        {
            var planning = _planification.RecalculerAsync(
                ProjetSelectionne.Id, DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
            Charger();
            ActualiserAlertes();
            Message = $"CPM recalculé : {planning.NombreTachesCritiques} tâche(s) critique(s), " +
                      $"{planning.DureeProjetJours} jour(s), fin prévue {planning.DateFinProjet:d}.";
        }
        catch (Exception ex)
        {
            Message = $"Recalcul impossible : {ex.Message}";
        }
    }

    private bool EssayerLireDates(out DateOnly debut, out DateOnly fin)
    {
        var format = DateOnly.TryParseExact(DateDebutTexte, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out debut);
        var formatFin = DateOnly.TryParseExact(DateFinTexte, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out fin);
        if (!format || !formatFin)
            Message = "Les dates doivent être saisies au format AAAA-MM-JJ.";
        return format && formatFin;
    }

    private void ActualiserCommandes()
    {
        ((RelayCommand)CreerTacheCommand).Actualiser();
        ((RelayCommand)EnregistrerTacheCommand).Actualiser();
        ((RelayCommand)SupprimerTacheCommand).Actualiser();
        ((RelayCommand)AjouterLiaisonCommand).Actualiser();
        ((RelayCommand)SupprimerLiaisonCommand).Actualiser();
        ((RelayCommand)RecalculerCommand).Actualiser();
    }

    private static string LibellerType(TypeLiaison type) => type switch
    {
        TypeLiaison.FinDebut => "Fin → Début",
        TypeLiaison.DebutDebut => "Début → Début",
        TypeLiaison.FinFin => "Fin → Fin",
        TypeLiaison.DebutFin => "Début → Fin",
        _ => type.ToString()
    };
}

/// <summary>Noeud d'arborescence partagé entre l'arbre et les lignes du Gantt.</summary>
public class NoeudTache : ViewModelBase
{
    public Guid Id { get; }
    public string Libelle { get; }
    public int Niveau { get; }
    public TacheResume Tache { get; }

    public ObservableCollection<NoeudTache> SousTaches { get; } = new();

    private bool _estSelectionnee;
    public bool EstSelectionnee
    {
        get => _estSelectionnee;
        set => SetField(ref _estSelectionnee, value);
    }

    private double _barreGauche;
    public double BarreGauche
    {
        get => _barreGauche;
        private set => SetField(ref _barreGauche, value);
    }

    private double _barreLargeur;
    public double BarreLargeur
    {
        get => _barreLargeur;
        private set => SetField(ref _barreLargeur, value);
    }

    private Brush _couleurBarre = Brushes.SteelBlue;
    public Brush CouleurBarre
    {
        get => _couleurBarre;
        private set => SetField(ref _couleurBarre, value);
    }

    private string _indentation = string.Empty;
    public string Indentation
    {
        get => _indentation;
        private set => SetField(ref _indentation, value);
    }

    private Brush _couleurTexte = Brushes.Black;
    public Brush CouleurTexte
    {
        get => _couleurTexte;
        private set => SetField(ref _couleurTexte, value);
    }

    private FontWeight _poidsTexte = FontWeights.Normal;
    public FontWeight PoidsTexte
    {
        get => _poidsTexte;
        private set => SetField(ref _poidsTexte, value);
    }

    public NoeudTache(Guid id, string libelle, int niveau)
    {
        Id = id;
        Libelle = libelle;
        Niveau = niveau;
        Tache = new TacheResume(
            id, Guid.Empty, null, libelle, niveau, 0,
            DateOnly.MinValue, DateOnly.MinValue, null, null, 0m, false, 0, 0);
        Indentation = string.Empty;
    }

    public NoeudTache(TacheResume tache)
    {
        Tache = tache;
        Id = tache.Id;
        Libelle = tache.Libelle;
        Niveau = tache.Niveau;
        Indentation = Niveau == 0 ? string.Empty : new string(' ', Niveau * 2) + "└─ ";
        CouleurBarre = tache.EstCheminCritique
            ? new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B))
            : new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x6B));
        CouleurTexte = tache.EstCheminCritique
            ? new SolidColorBrush(Color.FromRgb(0xC0, 0x39, 0x2B))
            : new SolidColorBrush(Color.FromRgb(0x1B, 0x27, 0x33));
        PoidsTexte = tache.EstCheminCritique ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public void Positionner(int debutProjet, int pixelsParJour)
    {
        var debut = Tache.DateDebutPrevue.DayNumber;
        var fin = Tache.DateFinPrevue.DayNumber;
        if (fin < debut)
            fin = debut;
        BarreGauche = (debut - debutProjet) * pixelsParJour + 8;
        BarreLargeur = Math.Max(10, (fin - debut + 1) * pixelsParJour);
    }
}

public sealed record OptionLiaison(TypeLiaison Valeur, string Libelle);
