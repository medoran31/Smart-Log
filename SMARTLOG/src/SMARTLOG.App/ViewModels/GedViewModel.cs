using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Smartlog.Data;
using Smartlog.Services;
using Smartlog.Services.Ged;

namespace Smartlog.App.ViewModels;

public sealed record OptionFormat(FormatDocument Valeur, string Libelle);

/// <summary>Noeud générique de l'arborescence GED (projet ou dossier logique).</summary>
public abstract class NoeudArborescence
{
    protected NoeudArborescence(string libelle, string icone)
    {
        Libelle = libelle;
        Icone = icone;
    }

    public string Libelle { get; }
    public string Icone { get; }
}

public sealed class NoeudProjet : NoeudArborescence
{
    public NoeudProjet(Guid projetId, string code, string intitule)
        : base($"{code} — {intitule}", "🏗️")
    {
        ProjetId = projetId;
    }

    public Guid ProjetId { get; }
    public ObservableCollection<NoeudDossier> Dossiers { get; } = new();
}

public sealed class NoeudDossier : NoeudArborescence
{
    public NoeudDossier(Guid projetId, string nom)
        : base(nom, "📂")
    {
        ProjetId = projetId;
        NomDossier = nom;
    }

    public Guid ProjetId { get; }
    public string NomDossier { get; }
}

/// <summary>
/// Écran GED : arborescence projets/dossiers à gauche, grille des documents au
/// centre, prévisualisation et génération de modèles à droite. Toutes les
/// données viennent des services ; le ViewModel ne calcule aucun montant.
/// </summary>
public class GedViewModel : ViewModelBase
{
    private readonly IGedService _ged;
    private readonly IProjetService _projets;
    private readonly IModeleDocumentService _modeles;
    private readonly IModeleExcelService _modelesExcel;

    private Guid? _projetFiltre;
    private string? _dossierFiltre;

    public ObservableCollection<NoeudProjet> Arborescence { get; } = new();
    public ObservableCollection<DocumentGedResume> Documents { get; } = new();
    public IReadOnlyList<JetonModele> JetonsDisponibles { get; }

    public IReadOnlyList<OptionFormat> OptionsFormat { get; } = new[]
    {
        new OptionFormat(FormatDocument.Word, "Word (.docx)"),
        new OptionFormat(FormatDocument.Pdf, "PDF (.pdf)")
    };

    public ICommand ImporterCommand { get; }
    public ICommand OuvrirCommand { get; }
    public ICommand InsererJetonCommand { get; }
    public ICommand GenererCommand { get; }
    public ICommand ActualiserCommand { get; }
    public ICommand CreerModelesExcelCommand { get; }
    public ICommand OuvrirDossierModelesCommand { get; }

    private NoeudArborescence? _selectionArborescence;
    public NoeudArborescence? SelectionArborescence
    {
        get => _selectionArborescence;
        set
        {
            if (!SetField(ref _selectionArborescence, value))
                return;

            _projetFiltre = value switch
            {
                NoeudProjet projet => projet.ProjetId,
                NoeudDossier dossier => dossier.ProjetId,
                _ => null
            };
            _dossierFiltre = (value as NoeudDossier)?.NomDossier;
            Notifier(nameof(ProjetSelectionne));
            RechargerDocuments();
        }
    }

    private string _message = "Sélectionnez un projet dans l'arborescence pour afficher ses documents.";
    public string Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    private string _messageModeles = string.Empty;
    public string MessageModeles
    {
        get => _messageModeles;
        private set => SetField(ref _messageModeles, value);
    }

    private string _recherche = string.Empty;
    public string Recherche
    {
        get => _recherche;
        set
        {
            if (SetField(ref _recherche, value))
                RechargerDocuments();
        }
    }

    private DocumentGedResume? _documentSelectionne;
    public DocumentGedResume? DocumentSelectionne
    {
        get => _documentSelectionne;
        set
        {
            if (SetField(ref _documentSelectionne, value))
            {
                Notifier(
                    nameof(ApercuNomFichier),
                    nameof(ApercuMetadonnees),
                    nameof(ApercuTexte),
                    nameof(CheminApercuImage),
                    nameof(EstImageApercu),
                    nameof(ApercuDisponible));
            }
        }
    }

    public bool ProjetSelectionne => _projetFiltre.HasValue;
    public bool ApercuDisponible => DocumentSelectionne is not null;

    public bool EstImageApercu => !string.IsNullOrEmpty(CheminApercuImage);

    public string ApercuNomFichier => DocumentSelectionne?.NomFichier ?? "Aucun document sélectionné";

    public string ApercuMetadonnees => DocumentSelectionne is null
        ? string.Empty
        : $"Projet {DocumentSelectionne.ProjetCode} — {DocumentSelectionne.ProjetIntitule}\n" +
          $"Catégorie : {DocumentSelectionne.Categorie} — version {DocumentSelectionne.Version}\n" +
          $"Dossier : {DocumentSelectionne.Dossier}\n" +
          $"Taille : {DocumentSelectionne.TailleLisible} — modifié le {DocumentSelectionne.DateModification:g}\n" +
          $"OCR : {DocumentSelectionne.StatutOcr}";

    public string ApercuTexte => DocumentSelectionne?.ContenuOcr ??
        "Aucun texte extrait. Importez une image ou un PDF scanné avec Tesseract installé pour alimenter l'index plein texte.";

    public string CheminApercuImage => DocumentSelectionne is not null &&
        new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff" }
            .Contains(Path.GetExtension(DocumentSelectionne.CheminFichier), StringComparer.OrdinalIgnoreCase)
        ? DocumentSelectionne.CheminFichier
        : string.Empty;

    private string _titreDocument = "Courrier de suivi";
    public string TitreDocument
    {
        get => _titreDocument;
        set => SetField(ref _titreDocument, value);
    }

    private FormatDocument _formatDocument = FormatDocument.Word;
    public FormatDocument FormatDocument
    {
        get => _formatDocument;
        set => SetField(ref _formatDocument, value);
    }

    private string _contenuModele =
        "Objet : {{Titre}}\n\nProjet : {{Projet.Intitule}} ({{Projet.Code}})\n" +
        "Maître d'ouvrage : {{Projet.MaitreOuvrage}}\n" +
        "Marché : {{Marche.Reference}} signé le {{Marche.DateSignature}}\n" +
        "Montant actualisé : {{Projet.MontantActualise}}\n" +
        "Délai actualisé : {{Projet.DelaiActualiseJours}} jours\n" +
        "Fin prévisionnelle : {{Projet.DatePrevisionnelleAchevement}}\n\n" +
        "Observations : {{Observations}}";
    public string ContenuModele
    {
        get => _contenuModele;
        set => SetField(ref _contenuModele, value);
    }

    private JetonModele? _jetonSelectionne;
    public JetonModele? JetonSelectionne
    {
        get => _jetonSelectionne;
        set => SetField(ref _jetonSelectionne, value);
    }

    private string _observations = "Document généré automatiquement depuis le modèle SMARTLOG.";
    public string Observations
    {
        get => _observations;
        set => SetField(ref _observations, value);
    }

    public GedViewModel(
        IGedService ged,
        IProjetService projets,
        IModeleDocumentService modeles,
        IModeleExcelService modelesExcel)
    {
        _ged = ged;
        _projets = projets;
        _modeles = modeles;
        _modelesExcel = modelesExcel;
        JetonsDisponibles = modeles.ListerJetonsDisponibles();
        MessageModeles = $"{modelesExcel.ListerModeles().Count} modèles Excel disponibles dans {modelesExcel.DossierModeles}.";

        ImporterCommand = new RelayCommand(_ => ImporterCommandExecute(), _ => ProjetSelectionne);
        OuvrirCommand = new RelayCommand(
            _ => Ouvrir(DocumentSelectionne), _ => DocumentSelectionne is not null);
        InsererJetonCommand = new RelayCommand(
            _ => InsererJeton(JetonSelectionne), _ => JetonSelectionne is not null);
        GenererCommand = new RelayCommand(_ => Generer(), _ => ProjetSelectionne);
        ActualiserCommand = new RelayCommand(_ => Charger());
        CreerModelesExcelCommand = new RelayCommand(async _ => await CreerModelesExcelAsync());
        OuvrirDossierModelesCommand = new RelayCommand(_ => OuvrirDossierModeles());

        Charger();
    }

    /// <summary>Recharge l'arborescence et la grille depuis les services.</summary>
    public void Charger()
    {
        var projetIdSelectionne = _projetFiltre;
        var dossierSelectionne = _dossierFiltre;

        Arborescence.Clear();
        var resumeProjets = _projets.ListerAsync().GetAwaiter().GetResult();
        var documents = _ged.ListerAsync().GetAwaiter().GetResult();

        foreach (var projet in resumeProjets)
        {
            var noeud = new NoeudProjet(projet.Id, projet.Code, projet.Intitule);
            foreach (var dossier in documents
                         .Where(d => d.ProjetId == projet.Id)
                         .Select(d => d.Dossier)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                noeud.Dossiers.Add(new NoeudDossier(projet.Id, dossier));
            Arborescence.Add(noeud);
        }

        _projetFiltre = projetIdSelectionne;
        _dossierFiltre = dossierSelectionne;
        RechargerDocuments();
        Message = $"{documents.Count} document(s) indexé(s) sur {resumeProjets.Count} projet(s).";
    }

    /// <summary>Sélectionne un projet (appelé depuis la navigation principale).</summary>
    public void SelectionnerProjet(Guid projetId)
    {
        Charger();
        var noeud = Arborescence.FirstOrDefault(p => p.ProjetId == projetId);
        if (noeud is not null)
            SelectionArborescence = noeud;
    }

    private void RechargerDocuments()
    {
        var documents = _ged.RechercherAsync(_projetFiltre, Recherche).GetAwaiter().GetResult();

        Documents.Clear();
        foreach (var document in documents.Where(d => _dossierFiltre is null ||
                                                      string.Equals(d.Dossier, _dossierFiltre, StringComparison.OrdinalIgnoreCase)))
            Documents.Add(document);

        if (DocumentSelectionne is not null && !Documents.Any(d => d.Id == DocumentSelectionne.Id))
            DocumentSelectionne = null;

        ((RelayCommand)ImporterCommand).Actualiser();
        ((RelayCommand)GenererCommand).Actualiser();
        ((RelayCommand)OuvrirCommand).Actualiser();
    }

    /// <summary>Importe plusieurs fichiers dans le dossier du projet sélectionné.</summary>
    public void ImporterFichiers(IEnumerable<string> chemins)
    {
        if (!_projetFiltre.HasValue)
        {
            Message = "Sélectionnez d'abord un projet dans l'arborescence.";
            return;
        }

        var nombre = 0;
        foreach (var chemin in chemins)
        {
            try
            {
                var document = _ged.ImporterAsync(
                    new DemandeImportDocument(_projetFiltre.Value, chemin),
                    DonneesDemonstration.UtilisateurDemonstrationId).GetAwaiter().GetResult();
                nombre++;
                Message = $"« {document.NomFichier} » importé — {document.StatutOcr}.";
            }
            catch (Exception ex)
            {
                Message = $"Import de « {Path.GetFileName(chemin)} » impossible : {ex.Message}";
            }
        }

        if (nombre > 0)
            Charger();
    }

    private void ImporterCommandExecute()
    {
        // La sélection de fichiers est faite par la vue (OpenFileDialog) puis
        // transmise à ImporterFichiers ; la commande sert à signaler l'action.
        Message = "Choisissez un ou plusieurs fichiers dans la boîte de dialogue.";
    }

    private async Task CreerModelesExcelAsync()
    {
        try
        {
            MessageModeles = "Création des modèles en cours…";
            var fichiers = await _modelesExcel.CreerModelesAsync();
            var crees = fichiers.Count(f => f.Cree);
            MessageModeles = $"{crees} modèle(s) créé(s), {fichiers.Count - crees} déjà présent(s) dans {_modelesExcel.DossierModeles}.";
        }
        catch (Exception ex)
        {
            MessageModeles = $"Création des modèles impossible : {ex.Message}";
        }
    }

    private void OuvrirDossierModeles()
    {
        try
        {
            Directory.CreateDirectory(_modelesExcel.DossierModeles);
            Process.Start(new ProcessStartInfo(_modelesExcel.DossierModeles) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageModeles = $"Ouverture du dossier impossible : {ex.Message}";
        }
    }

    private static void Ouvrir(DocumentGedResume? document)
    {
        if (document is null)
            return;

        try
        {
            Process.Start(new ProcessStartInfo(document.CheminFichier) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // L'ouverture externe est best-effort (type de fichier non associé).
        }
    }

    private void InsererJeton(JetonModele? jeton)
    {
        if (jeton is null)
            return;

        ContenuModele += $"{{{{{jeton.Cle}}}}}";
    }

    private void Generer()
    {
        if (!_projetFiltre.HasValue)
        {
            Message = "Sélectionnez d'abord un projet dans l'arborescence.";
            return;
        }

        try
        {
            var valeurs = new Dictionary<string, string>
            {
                ["Observations"] = Observations
            };

            var genere = _modeles.GenererAsync(
                new ModeleDocumentDemande(
                    _projetFiltre.Value,
                    ContenuModele,
                    TitreDocument,
                    FormatDocument,
                    DonneesDemonstration.UtilisateurDemonstrationId,
                    valeurs)).GetAwaiter().GetResult();

            Charger();
            Message = genere.JetonsNonResolus.Count == 0
                ? $"Document « {genere.NomFichier} » généré et archivé dans la GED."
                : $"Document généré ; jetons non résolus : {string.Join(", ", genere.JetonsNonResolus)}.";
        }
        catch (Exception ex)
        {
            Message = $"Génération impossible : {ex.Message}";
        }
    }
}
