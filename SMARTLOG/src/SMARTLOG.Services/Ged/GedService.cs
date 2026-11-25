using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services.Ged;

public class GedService : IGedService
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "PDF",
        [".doc"] = "Word",
        [".docx"] = "Word",
        [".xls"] = "Excel",
        [".xlsx"] = "Excel",
        [".csv"] = "Excel",
        [".png"] = "Image",
        [".jpg"] = "Image",
        [".jpeg"] = "Image",
        [".bmp"] = "Image",
        [".tif"] = "Image",
        [".tiff"] = "Image"
    };

    private static readonly HashSet<string> ExtensionsOcrables = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff" };

    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IOcrService _ocr;

    public GedService(SmartlogDbContext db, IJournalService journal, IOcrService ocr)
    {
        _db = db;
        _journal = journal;
        _ocr = ocr;
    }

    public async Task<DocumentGedResume> ImporterAsync(
        DemandeImportDocument demande,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(demande.CheminSource) || !File.Exists(demande.CheminSource))
            throw new FileNotFoundException("Le fichier à importer est introuvable.", demande.CheminSource);

        var projet = await _db.Projets
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == demande.ProjetId, ct)
            ?? throw new InvalidOperationException($"Projet {demande.ProjetId} introuvable.");

        var categorie = Categories.TryGetValue(Path.GetExtension(demande.CheminSource), out var trouve)
            ? trouve
            : "Autre";
        var sousDossier = string.IsNullOrWhiteSpace(demande.SousDossier)
            ? SousDossierParDefaut(demande.EntiteLieeType)
            : NettoyerNom(demande.SousDossier);
        var dossier = Path.Combine(RacineDocuments(), NettoyerNom(projet.Code), sousDossier);
        Directory.CreateDirectory(dossier);

        var nomOriginal = NettoyerNom(Path.GetFileName(demande.CheminSource));
        var documentsExistants = await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProjetId == projet.Id)
            .Select(d => d.CheminFichier)
            .ToListAsync(ct);

        var version = documentsExistants
            .Where(c => string.Equals(Path.GetDirectoryName(c), dossier, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(ExtraireNomFichier(c), nomOriginal, StringComparison.OrdinalIgnoreCase))
            .Select(c => ExtraireVersion(c))
            .DefaultIfEmpty(0)
            .Max() + 1;

        var cheminDestinataire = Path.Combine(dossier, $"{version}_{nomOriginal}");
        File.Copy(demande.CheminSource, cheminDestinataire, true);

        // L'OCR est optionnel : toute panne ou absence de Tesseract laisse le
        // document stocké avec ContenuOcr=null, sans faire échouer l'import.
        string? contenuOcr = null;
        var statutOcr = "OCR non applicable à ce format";
        if (ExtensionsOcrables.Contains(Path.GetExtension(cheminDestinataire)))
        {
            try
            {
                var estPdf = string.Equals(
                    Path.GetExtension(cheminDestinataire), ".pdf", StringComparison.OrdinalIgnoreCase);
                contenuOcr = estPdf
                    ? await _ocr.ExtraireTextePdfAsync(cheminDestinataire, ct)
                    : await _ocr.ExtraireTexteAsync(cheminDestinataire, ct);

                statutOcr = string.IsNullOrWhiteSpace(contenuOcr)
                    ? "Tesseract indisponible : document stocké sans OCR"
                    : "OCR réussi";
            }
            catch (Exception)
            {
                statutOcr = "OCR en échec : document stocké sans OCR";
                contenuOcr = null;
            }
        }

        var document = new DocumentGed
        {
            ProjetId = projet.Id,
            TypeDocument = categorie,
            CheminFichier = cheminDestinataire,
            Version = version,
            ContenuOcr = string.IsNullOrWhiteSpace(contenuOcr) ? null : contenuOcr.Trim(),
            EntiteLieeType = demande.EntiteLieeType,
            EntiteLieeId = demande.EntiteLieeId
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(DocumentGed), document.Id, "Creation", null,
            new
            {
                DocumentId = document.Id,
                ProjetId = projet.Id,
                ProjetCode = projet.Code,
                document.TypeDocument,
                NomFichier = nomOriginal,
                document.Version,
                document.CheminFichier,
                StatutOcr = statutOcr,
                EntiteLieeType = document.EntiteLieeType,
                EntiteLieeId = document.EntiteLieeId
            },
            ct);

        return VersResume(document, projet, statutOcr);
    }

    public async Task<IReadOnlyList<DocumentGedResume>> ListerAsync(
        Guid? projetId = null,
        CancellationToken ct = default)
    {
        var documents = await RequeteDocuments(projetId)
            .OrderBy(d => d.Projet!.Code)
            .ThenBy(d => d.CheminFichier)
            .ToListAsync(ct);

        return documents.Select(d => VersResume(d, d.Projet!)).ToList();
    }

    public async Task<IReadOnlyList<DocumentGedResume>> RechercherAsync(
        Guid? projetId,
        string recherche,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(recherche))
            return await ListerAsync(projetId, ct);

        var termes = Normaliser(recherche)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var documents = await RequeteDocuments(projetId).ToListAsync(ct);

        return documents
            .Where(d =>
            {
                var contenuIndexe = Normaliser(string.Join(' ',
                    d.ContenuOcr,
                    ExtraireNomFichier(d.CheminFichier),
                    d.TypeDocument,
                    d.EntiteLieeType,
                    d.Projet!.Code,
                    d.Projet.Intitule));
                return termes.All(terme => contenuIndexe.Contains(terme, StringComparison.Ordinal));
            })
            .Select(d => VersResume(d, d.Projet!))
            .ToList();
    }

    public async Task<DocumentGedResume?> ObtenirAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await RequeteDocuments()
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        return document is null ? null : VersResume(document, document.Projet!);
    }

    private IQueryable<DocumentGed> RequeteDocuments(Guid? projetId = null)
    {
        var requete = _db.Documents
            .AsNoTracking()
            .Include(d => d.Projet)
            .AsQueryable();

        return projetId is null ? requete : requete.Where(d => d.ProjetId == projetId.Value);
    }

    private static string RacineDocuments() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Smartlog", "Documents");

    private static string SousDossierParDefaut(string? entiteLieeType) => entiteLieeType switch
    {
        nameof(Marche) => "Marchés",
        nameof(Avenant) => "Avenants",
        nameof(Reserve) => "Réserves",
        nameof(Situation) => "Situations",
        nameof(Courrier) => "Courrier",
        _ => "Divers"
    };

    private static string NettoyerNom(string nom)
    {
        var nettoye = nom;
        foreach (var caractere in Path.GetInvalidFileNameChars())
            nettoye = nettoye.Replace(caractere, '_');
        return string.IsNullOrWhiteSpace(nettoye) ? "sans-nom" : nettoye.Trim();
    }

    private static string ExtraireNomFichier(string chemin)
    {
        var nom = Path.GetFileName(chemin);
        var indice = nom.IndexOf('_');
        return indice >= 0 && indice + 1 < nom.Length ? nom[(indice + 1)..] : nom;
    }

    private static int ExtraireVersion(string chemin)
    {
        var nom = Path.GetFileName(chemin);
        var indice = nom.IndexOf('_');
        return indice > 0 && int.TryParse(nom[..indice], out var version) ? version : 1;
    }

    private static string ExtraireDossier(string chemin)
    {
        var marqueur = RacineDocuments() + Path.DirectorySeparatorChar;
        var position = chemin.IndexOf(marqueur, StringComparison.OrdinalIgnoreCase);
        if (position < 0)
            return Path.GetFileName(Path.GetDirectoryName(chemin)) ?? "Divers";

        var parties = chemin[(position + marqueur.Length)..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
        return parties.Length > 1 ? parties[1] : "Racine";
    }

    private static string Normaliser(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte))
            return string.Empty;

        var formeDécomposée = texte.Normalize(NormalizationForm.FormD);
        var résultat = new StringBuilder(formeDécomposée.Length);
        foreach (var caractère in formeDécomposée)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractère) != UnicodeCategory.NonSpacingMark)
                résultat.Append(caractère);
        }
        return résultat.ToString().ToLowerInvariant();
    }

    private static DocumentGedResume VersResume(DocumentGed document, Projet projet, string? statutOcr = null)
    {
        var informationFichier = new FileInfo(document.CheminFichier);
        var categorieOcrable = ExtensionsOcrables.Contains(Path.GetExtension(document.CheminFichier));
        var statut = statutOcr ?? (document.ContenuOcr is null
            ? (categorieOcrable ? "Tesseract indisponible : document stocké sans OCR" : "OCR non applicable à ce format")
            : "OCR réussi");

        return new DocumentGedResume(
            document.Id,
            document.ProjetId,
            projet.Code,
            projet.Intitule,
            ExtraireNomFichier(document.CheminFichier),
            document.TypeDocument,
            document.Version,
            document.CheminFichier,
            ExtraireDossier(document.CheminFichier),
            informationFichier.Exists ? informationFichier.Length : 0,
            informationFichier.Exists ? informationFichier.LastWriteTime : DateTime.MinValue,
            document.ContenuOcr,
            statut,
            document.EntiteLieeType,
            document.EntiteLieeId);
    }
}
