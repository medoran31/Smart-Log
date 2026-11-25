using Microsoft.EntityFrameworkCore;
using Smartlog.Data;
using Smartlog.Domain.Entities;

namespace Smartlog.Services;

public class MarcheService : IMarcheService
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "PDF",
        [".doc"] = "Word",
        [".docx"] = "Word",
        [".xls"] = "Excel",
        [".xlsx"] = "Excel"
    };

    private readonly SmartlogDbContext _db;
    private readonly IJournalService _journal;
    private readonly IAttachementService _attachements;

    public MarcheService(
        SmartlogDbContext db,
        IJournalService journal,
        IAttachementService attachements)
    {
        _db = db;
        _journal = journal;
        _attachements = attachements;
    }

    public async Task<MarcheDetail?> ObtenirAsync(Guid projetId, CancellationToken ct = default)
    {
        var marche = await _db.Marches
            .AsNoTracking()
            .Include(m => m.Projet)
            .Include(m => m.PrixUnitaires)
            .FirstOrDefaultAsync(m => m.ProjetId == projetId, ct);

        if (marche is null)
            return null;

        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => d.EntiteLieeType == nameof(Marche) && d.EntiteLieeId == marche.Id)
            .OrderBy(d => d.Version)
            .ThenBy(d => d.CheminFichier)
            .ToListAsync(ct);

        return new MarcheDetail(
            marche.Id,
            marche.ProjetId,
            marche.Projet?.Intitule ?? string.Empty,
            marche.ReferenceMarche,
            marche.DateSignature,
            marche.Projet?.MontantActualise ?? 0m,
            marche.PrixUnitaires.Select(VersResume).OrderBy(p => p.Reference).ToList(),
            documents.Select(VersDocument).ToList());
    }

    public async Task<decimal> CalculerTotalBordereauAsync(Guid marcheId, CancellationToken ct = default)
    {
        var prix = await _db.PrixUnitaires
            .AsNoTracking()
            .Where(p => p.MarcheId == marcheId)
            .Select(p => new { p.PrixUnitaireHT, p.QuantiteContractuelle })
            .ToListAsync(ct);

        return Math.Round(prix.Sum(p => p.PrixUnitaireHT * p.QuantiteContractuelle), 2);
    }

    public async Task<PrixUnitaireResume> AjouterPrixUnitaireAsync(
        Guid marcheId,
        PrixUnitaireCreation donnees,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var marche = await _db.Marches
            .FirstOrDefaultAsync(m => m.Id == marcheId, ct)
            ?? throw new InvalidOperationException($"Marché {marcheId} introuvable.");

        VerifierPrix(donnees.Reference, donnees.Designation, donnees.PrixUnitaireHT, donnees.QuantiteContractuelle);

        if (await _db.PrixUnitaires.AnyAsync(
                p => p.MarcheId == marcheId && p.Reference == donnees.Reference.Trim(), ct))
        {
            throw new InvalidOperationException($"La référence {donnees.Reference} existe déjà dans ce bordereau.");
        }

        var prix = new PrixUnitaire
        {
            MarcheId = marche.Id,
            Reference = donnees.Reference.Trim(),
            Designation = donnees.Designation.Trim(),
            Unite = donnees.Unite.Trim(),
            PrixUnitaireHT = Math.Round(donnees.PrixUnitaireHT, 2),
            QuantiteContractuelle = Math.Round(donnees.QuantiteContractuelle, 3)
        };

        _db.PrixUnitaires.Add(prix);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(PrixUnitaire), prix.Id, "Creation", null,
            new
            {
                prix.Id,
                prix.MarcheId,
                prix.Reference,
                prix.Designation,
                prix.Unite,
                prix.PrixUnitaireHT,
                prix.QuantiteContractuelle
            },
            ct);

        return VersResume(prix);
    }

    public async Task<PrixUnitaireResume> ModifierPrixUnitaireAsync(
        PrixUnitaireModification donnees,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var prix = await _db.PrixUnitaires
            .FirstOrDefaultAsync(p => p.Id == donnees.Id, ct)
            ?? throw new InvalidOperationException($"Prix unitaire {donnees.Id} introuvable.");

        VerifierPrix(donnees.Reference, donnees.Designation, donnees.PrixUnitaireHT, donnees.QuantiteContractuelle);

        var doublon = await _db.PrixUnitaires.AnyAsync(
            p => p.MarcheId == prix.MarcheId && p.Id != prix.Id && p.Reference == donnees.Reference.Trim(), ct);
        if (doublon)
            throw new InvalidOperationException($"La référence {donnees.Reference} existe déjà dans ce bordereau.");

        var avant = new
        {
            prix.Reference,
            prix.Designation,
            prix.Unite,
            prix.PrixUnitaireHT,
            prix.QuantiteContractuelle
        };

        prix.Reference = donnees.Reference.Trim();
        prix.Designation = donnees.Designation.Trim();
        prix.Unite = donnees.Unite.Trim();
        prix.PrixUnitaireHT = Math.Round(donnees.PrixUnitaireHT, 2);
        prix.QuantiteContractuelle = Math.Round(donnees.QuantiteContractuelle, 3);

        await _db.SaveChangesAsync(ct);

        // Le montant stocké de chaque ligne dépend du BPU : sans ce recalcul,
        // les situations financières utiliseraient une valeur périmée.
        await _attachements.RecalculerLignesAsync(prix.Id, utilisateurId, ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(PrixUnitaire), prix.Id, "Modification", avant,
            new
            {
                prix.Reference,
                prix.Designation,
                prix.Unite,
                prix.PrixUnitaireHT,
                prix.QuantiteContractuelle
            },
            ct);

        return VersResume(prix);
    }

    public async Task SupprimerPrixUnitaireAsync(
        Guid prixUnitaireId,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        var prix = await _db.PrixUnitaires
            .FirstOrDefaultAsync(p => p.Id == prixUnitaireId, ct)
            ?? throw new InvalidOperationException($"Prix unitaire {prixUnitaireId} introuvable.");

        if (await _db.LignesAttachement.AnyAsync(l => l.PrixUnitaireId == prixUnitaireId, ct))
            throw new InvalidOperationException(
                "Ce prix unitaire est utilisé par des attachements et ne peut pas être supprimé.");

        var avant = new
        {
            prix.Id,
            prix.Reference,
            prix.Designation,
            prix.PrixUnitaireHT,
            prix.QuantiteContractuelle
        };

        _db.PrixUnitaires.Remove(prix);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(PrixUnitaire), prix.Id, "Suppression", avant, null, ct);
    }

    public async Task<DocumentMarche> ImporterDocumentAsync(
        Guid marcheId,
        string cheminSource,
        Guid utilisateurId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cheminSource) || !File.Exists(cheminSource))
            throw new FileNotFoundException("Le fichier à importer est introuvable.", cheminSource);

        var extension = Path.GetExtension(cheminSource);
        if (!Categories.TryGetValue(extension, out var categorie))
            throw new ArgumentException(
                "Seuls les fichiers PDF, Word (.doc, .docx) et Excel (.xls, .xlsx) peuvent être importés.",
                nameof(cheminSource));

        var marche = await _db.Marches
            .AsNoTracking()
            .Include(m => m.Projet)
            .FirstOrDefaultAsync(m => m.Id == marcheId, ct)
            ?? throw new InvalidOperationException($"Marché {marcheId} introuvable.");

        var nomOriginal = NettoyerNomFichier(Path.GetFileName(cheminSource));
        var versions = await _db.Documents
            .AsNoTracking()
            .Where(d => d.EntiteLieeType == nameof(Marche) && d.EntiteLieeId == marcheId)
            .Select(d => d.Version)
            .ToListAsync(ct);
        var version = versions.DefaultIfEmpty(0).Max() + 1;

        var dossier = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Smartlog", "Documents",
            NettoyerNomFichier(marche.Projet?.Code ?? "projet"),
            NettoyerNomFichier(marche.ReferenceMarche));
        Directory.CreateDirectory(dossier);

        var nomStocke = $"{version}_{nomOriginal}";
        var cheminDestinataire = Path.Combine(dossier, nomStocke);
        File.Copy(cheminSource, cheminDestinataire, true);

        var document = new DocumentGed
        {
            ProjetId = marche.ProjetId,
            TypeDocument = categorie,
            CheminFichier = cheminDestinataire,
            Version = version,
            EntiteLieeType = nameof(Marche),
            EntiteLieeId = marche.Id
        };

        _db.Documents.Add(document);
        await _db.SaveChangesAsync(ct);

        await _journal.EnregistrerAsync(
            utilisateurId, nameof(DocumentGed), document.Id, "Creation", null,
            new
            {
                DocumentId = document.Id,
                MarcheId = marche.Id,
                document.TypeDocument,
                NomFichier = nomOriginal,
                document.Version,
                document.CheminFichier
            },
            ct);

        return VersDocument(document);
    }

    private static void VerifierPrix(
        string reference, string designation, decimal prixUnitaireHT, decimal quantiteContractuelle)
    {
        if (string.IsNullOrWhiteSpace(reference))
            throw new ArgumentException("La référence du prix est obligatoire.", nameof(reference));
        if (string.IsNullOrWhiteSpace(designation))
            throw new ArgumentException("La désignation du prix est obligatoire.", nameof(designation));
        if (prixUnitaireHT < 0)
            throw new ArgumentOutOfRangeException(nameof(prixUnitaireHT), "Le prix ne peut pas être négatif.");
        if (quantiteContractuelle < 0)
            throw new ArgumentOutOfRangeException(nameof(quantiteContractuelle), "La quantité ne peut pas être négative.");
    }

    private static string NettoyerNomFichier(string nom)
    {
        var nettoye = nom;
        foreach (var caractere in Path.GetInvalidFileNameChars())
            nettoye = nettoye.Replace(caractere, '_');
        return string.IsNullOrWhiteSpace(nettoye) ? "sans-nom" : nettoye;
    }

    private static PrixUnitaireResume VersResume(PrixUnitaire p) => new(
        p.Id,
        p.Reference,
        p.Designation,
        p.Unite,
        p.PrixUnitaireHT,
        p.QuantiteContractuelle,
        Math.Round(p.PrixUnitaireHT * p.QuantiteContractuelle, 2));

    private static DocumentMarche VersDocument(DocumentGed d) => new(
        d.Id,
        d.TypeDocument,
        ExtraireNomFichier(d.CheminFichier),
        d.CheminFichier,
        d.Version);

    private static string ExtraireNomFichier(string chemin)
    {
        var nom = Path.GetFileName(chemin);
        var indice = nom.IndexOf('_');
        return indice >= 0 && indice + 1 < nom.Length ? nom[(indice + 1)..] : nom;
    }
}
