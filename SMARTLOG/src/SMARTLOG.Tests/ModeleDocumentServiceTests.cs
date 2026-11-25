using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Services;

namespace Smartlog.Tests;

public class ModeleDocumentServiceTests : SqliteTestBase
{
    [Fact]
    public async Task GenererWord_RemplaceLesJetonsEtArchiveLeDocumentDansLaGed()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var modele = "Projet : {{Projet.Intitule}} ({{Projet.Code}})\n" +
                     "Marché : {{Marche.Reference}}\n" +
                     "Montant : {{Projet.MontantActualise}}\n" +
                     "Observations : {{Observations}}\n" +
                     "Inconnu : {{ChampInconnu}}";

        var genere = await Modeles.GenererAsync(
            new ModeleDocumentDemande(
                projet.Id, modele, "Courrier de suivi", FormatDocument.Word, UtilisateurTestId,
                new Dictionary<string, string> { ["Observations"] = "Travaux conformes" }));

        Assert.Contains("Projet de test (TEST-001)", genere.ContenuGenere);
        Assert.Contains("MARCHE-TEST-001", genere.ContenuGenere);
        var montantAttendu = 1_250_000m.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " DA";
        Assert.Contains(montantAttendu, genere.ContenuGenere);
        Assert.Contains("Travaux conformes", genere.ContenuGenere);
        Assert.Contains("{{ChampInconnu}}", genere.ContenuGenere);
        Assert.Contains("ChampInconnu", genere.JetonsNonResolus);
        Assert.Equal(".docx", Path.GetExtension(genere.CheminFichier));
        Assert.True(File.Exists(genere.CheminFichier));

        try
        {
            using var archive = ZipFile.OpenRead(genere.CheminFichier);
            var entree = archive.GetEntry("word/document.xml");
            Assert.NotNull(entree);
            using var lecteur = new StreamReader(entree!.Open());
            Assert.Contains("Projet de test (TEST-001)", await lecteur.ReadToEndAsync());

            var document = await Db.Documents.AsNoTracking()
                .SingleAsync(d => d.CheminFichier == genere.CheminFichier);
            Assert.Equal("Word", document.TypeDocument);
            Assert.Equal(nameof(Projet), document.EntiteLieeType);

            var journal = await Journal.ListerAsync(nameof(DocumentGed), document.Id);
            Assert.Contains(journal, e => e.ActionLibelle == "GenerationModele");
        }
        finally
        {
            File.Delete(genere.CheminFichier);
        }
    }

    [Fact]
    public async Task GenererPdf_ProduitUnPdfLisibleEtLeJournalise()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();

        var genere = await Modeles.GenererAsync(
            new ModeleDocumentDemande(
                projet.Id,
                "Rapport — {{Projet.Code}} — {{Projet.Statut}}",
                "Rapport mensuel",
                FormatDocument.Pdf,
                UtilisateurTestId));

        Assert.Equal(FormatDocument.Pdf, genere.Format);
        Assert.Equal(".pdf", Path.GetExtension(genere.CheminFichier));
        Assert.Empty(genere.JetonsNonResolus);
        Assert.Contains("TEST-001", genere.ContenuGenere);

        try
        {
            var octets = await File.ReadAllBytesAsync(genere.CheminFichier);
            var entete = System.Text.Encoding.Latin1.GetString(octets, 0, 8);
            Assert.Equal("%PDF-1.4", entete);
            Assert.Contains("%%EOF", System.Text.Encoding.Latin1.GetString(octets));

            var document = await Db.Documents.AsNoTracking()
                .SingleAsync(d => d.CheminFichier == genere.CheminFichier);
            Assert.Equal("PDF", document.TypeDocument);
        }
        finally
        {
            File.Delete(genere.CheminFichier);
        }
    }

    [Fact]
    public async Task GenererDepuisFichier_LitLeModeleEtRemplaceLesJetons()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var cheminModele = Path.Combine(Path.GetTempPath(), $"smartlog-modele-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(cheminModele, "Code projet : {{Projet.Code}}");

        try
        {
            var genere = await Modeles.GenererDepuisFichierAsync(
                projet.Id, cheminModele, "Fiche projet", FormatDocument.Word, UtilisateurTestId);

            Assert.Equal("Code projet : TEST-001", genere.ContenuGenere);
            Assert.True(File.Exists(genere.CheminFichier));
            File.Delete(genere.CheminFichier);
        }
        finally
        {
            File.Delete(cheminModele);
        }
    }

    [Fact]
    public void ListerJetonsDisponibles_ContientLesJetonsProjetEtMarche()
    {
        var jetons = Modeles.ListerJetonsDisponibles();

        Assert.Contains(jetons, j => j.Cle == "Projet.Intitule");
        Assert.Contains(jetons, j => j.Cle == "Projet.MontantActualise");
        Assert.Contains(jetons, j => j.Cle == "Marche.Reference");
    }
}
