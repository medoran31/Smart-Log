using System.Globalization;
using System.Text.RegularExpressions;
using Smartlog.Domain.Entities;
using Smartlog.Services.Ged;

namespace Smartlog.Services;

/// <summary>
/// Moteur transversal de modèles documentaires. Les valeurs {{Projet.*}} et
/// {{Marche.*}} sont lues depuis les services métier (source unique des
/// valeurs dérivées) ; les jetons inconnus sont laissés tels quels et
/// signalés dans <see cref="DocumentGenere.JetonsNonResolus"/>.
/// </summary>
public class ModeleDocumentService : IModeleDocumentService
{
    private static readonly Regex JetonRegex = new(
        @"\{\{\s*([A-Za-z0-9_.]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    private readonly IJournalService _journal;
    private readonly IGedService _ged;
    private readonly IProjetService _projets;
    private readonly IMarcheService _marches;

    public ModeleDocumentService(
        IJournalService journal,
        IGedService ged,
        IProjetService projets,
        IMarcheService marches)
    {
        _journal = journal;
        _ged = ged;
        _projets = projets;
        _marches = marches;
    }

    public IReadOnlyList<JetonModele> ListerJetonsDisponibles() => new[]
    {
        new JetonModele("Projet.Code", "Code du projet"),
        new JetonModele("Projet.Intitule", "Intitulé du projet"),
        new JetonModele("Projet.Localisation", "Localisation du chantier"),
        new JetonModele("Projet.NatureTravaux", "Nature des travaux"),
        new JetonModele("Projet.MaitreOuvrage", "Maître d'ouvrage"),
        new JetonModele("Projet.MaitreOeuvre", "Maître d'œuvre"),
        new JetonModele("Projet.Entreprise", "Entreprise attributaire"),
        new JetonModele("Projet.MontantInitial", "Montant initial du projet"),
        new JetonModele("Projet.MontantActualise", "Montant actualisé (avenants validés)"),
        new JetonModele("Projet.DelaiContractuelJours", "Délai contractuel (jours)"),
        new JetonModele("Projet.DelaiActualiseJours", "Délai actualisé (jours)"),
        new JetonModele("Projet.DateNotification", "Date de notification"),
        new JetonModele("Projet.DateCommencement", "Date de commencement"),
        new JetonModele("Projet.DatePrevisionnelleAchevement", "Date prévisionnelle d'achèvement"),
        new JetonModele("Projet.DateReelleAchevement", "Date réelle d'achèvement"),
        new JetonModele("Projet.Statut", "Statut du projet"),
        new JetonModele("Marche.Reference", "Référence du marché"),
        new JetonModele("Marche.DateSignature", "Date de signature du marché"),
        new JetonModele("Marche.TotalBordereau", "Total du bordereau de prix unitaires"),
        new JetonModele("DateGeneration", "Date de génération du document"),
        new JetonModele("Titre", "Titre du document")
    };

    public async Task<DocumentGenere> GenererAsync(
        ModeleDocumentDemande demande,
        CancellationToken ct = default)
    {
        if (demande is null)
            throw new ArgumentNullException(nameof(demande));
        if (string.IsNullOrWhiteSpace(demande.ContenuModele))
            throw new ArgumentException("Le contenu du modèle est vide.", nameof(demande));
        if (string.IsNullOrWhiteSpace(demande.Titre))
            throw new ArgumentException("Le titre du document est vide.", nameof(demande));

        var projet = await _projets.ObtenirAsync(demande.ProjetId, ct)
            ?? throw new InvalidOperationException($"Projet {demande.ProjetId} introuvable.");
        var marche = await _marches.ObtenirAsync(demande.ProjetId, ct);

        var valeurs = await ConstruireValeursAsync(projet, marche, demande, ct);
        var contenuGenere = JetonRegex.Replace(
            demande.ContenuModele,
            correspondance => valeurs.TryGetValue(correspondance.Groups[1].Value, out var valeur)
                ? valeur
                : correspondance.Value);

        var nonResolus = JetonRegex.Matches(contenuGenere)
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(v => v, StringComparer.Ordinal)
            .ToList();

        var octets = demande.Format == FormatDocument.Word
            ? DocxWriter.Generer(demande.Titre, contenuGenere)
            : PdfWriter.Generer(demande.Titre, contenuGenere);
        var extension = demande.Format == FormatDocument.Word ? ".docx" : ".pdf";

        var titreNettoye = NettoyerNom(demande.Titre);
        var dossierTemporaire = Path.Combine(
            Path.GetTempPath(), "smartlog-modele-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossierTemporaire);
        var cheminTemporaire = Path.Combine(dossierTemporaire, titreNettoye + extension);

        try
        {
            await File.WriteAllBytesAsync(cheminTemporaire, octets, ct);

            var document = await _ged.ImporterAsync(
                new DemandeImportDocument(
                    demande.ProjetId,
                    cheminTemporaire,
                    "Modèles",
                    demande.EntiteLieeType ?? nameof(Projet),
                    demande.EntiteLieeId ?? demande.ProjetId),
                demande.UtilisateurId,
                ct);

            await _journal.EnregistrerAsync(
                demande.UtilisateurId,
                nameof(DocumentGed),
                document.Id,
                "GenerationModele",
                null,
                new
                {
                    DocumentId = document.Id,
                    ProjetId = demande.ProjetId,
                    ProjetCode = projet.Code,
                    demande.Titre,
                    Format = demande.Format.ToString(),
                    JetonsNonResolus = string.Join(", ", nonResolus)
                },
                ct);

            return new DocumentGenere(
                document.CheminFichier,
                document.NomFichier,
                demande.Format,
                contenuGenere,
                nonResolus);
        }
        finally
        {
            try { Directory.Delete(dossierTemporaire, true); } catch (Exception) { /* nettoyage tolérant */ }
        }
    }

    public async Task<DocumentGenere> GenererDepuisFichierAsync(
        Guid projetId,
        string cheminModele,
        string titre,
        FormatDocument format,
        Guid utilisateurId,
        IReadOnlyDictionary<string, string>? valeursSupplementaires = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(cheminModele) || !File.Exists(cheminModele))
            throw new FileNotFoundException("Le fichier modèle est introuvable.", cheminModele);

        var contenu = await File.ReadAllTextAsync(cheminModele, System.Text.Encoding.UTF8, ct);
        return await GenererAsync(
            new ModeleDocumentDemande(
                projetId, contenu, titre, format, utilisateurId, valeursSupplementaires),
            ct);
    }

    private async Task<Dictionary<string, string>> ConstruireValeursAsync(
        ProjetDetail projet,
        MarcheDetail? marche,
        ModeleDocumentDemande demande,
        CancellationToken ct)
    {
        var valeurs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Projet.Code"] = projet.Code,
            ["Projet.Intitule"] = projet.Intitule,
            ["Projet.Localisation"] = projet.Localisation ?? "—",
            ["Projet.NatureTravaux"] = projet.NatureTravaux ?? "—",
            ["Projet.MaitreOuvrage"] = projet.MaitreOuvrage ?? "—",
            ["Projet.MaitreOeuvre"] = projet.MaitreOeuvre ?? "—",
            ["Projet.Entreprise"] = projet.Entreprise ?? "—",
            ["Projet.MontantInitial"] = FormaterMontant(projet.MontantInitial, "Projet.MontantInitial"),
            ["Projet.MontantActualise"] = FormaterMontant(projet.MontantActualise, "Projet.MontantActualise"),
            ["Projet.DelaiContractuelJours"] = projet.DelaiContractuelJours.ToString(Fr),
            ["Projet.DelaiActualiseJours"] = projet.DelaiActualiseJours.ToString(Fr),
            ["Projet.DateNotification"] = FormaterDate(projet.DateNotification),
            ["Projet.DateCommencement"] = FormaterDate(projet.DateCommencement),
            ["Projet.DatePrevisionnelleAchevement"] = FormaterDate(projet.DatePrevisionnelleAchevement),
            ["Projet.DateReelleAchevement"] = FormaterDate(projet.DateReelleAchevement),
            ["Projet.Statut"] = projet.StatutLibelle,
            ["DateGeneration"] = DateOnly.FromDateTime(DateTime.Now).ToString("d", Fr),
            ["Titre"] = demande.Titre
        };

        if (marche is not null)
        {
            valeurs["Marche.Reference"] = marche.ReferenceMarche;
            valeurs["Marche.DateSignature"] = FormaterDate(marche.DateSignature);
            var total = await _marches.CalculerTotalBordereauAsync(marche.Id, ct);
            valeurs["Marche.TotalBordereau"] = FormaterMontant(total, "Marche.TotalBordereau");
        }

        if (demande.ValeursSupplementaires is not null)
        {
            foreach (var (cle, valeur) in demande.ValeursSupplementaires)
            {
                valeurs[cle] = valeur ?? string.Empty;
                // Un jeton court {{Observation}} est aussi accepté pour
                // {{Resume.Observation}} ou {{Courrier.Observation}}.
                var partieCourte = cle.Contains('.') ? cle[(cle.LastIndexOf('.') + 1)..] : cle;
                valeurs.TryAdd(partieCourte, valeur ?? string.Empty);
            }
        }

        return valeurs;
    }

    private static string FormaterMontant(decimal montant, string cle) =>
        cle.Contains("Montant", StringComparison.OrdinalIgnoreCase) || cle.Contains("Total", StringComparison.OrdinalIgnoreCase)
            ? $"{montant.ToString("N2", Fr)} DA"
            : montant.ToString("N2", Fr);

    private static string FormaterDate(DateOnly? date) =>
        date?.ToString("d", Fr) ?? "Non renseignée";

    private static string NettoyerNom(string nom)
    {
        var nettoye = nom;
        foreach (var caractere in Path.GetInvalidFileNameChars())
            nettoye = nettoye.Replace(caractere, '_');
        return string.IsNullOrWhiteSpace(nettoye) ? "document" : nettoye.Trim();
    }
}
