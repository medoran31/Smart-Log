using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Services.Ged;

namespace Smartlog.Tests;

public class GedServiceTests : SqliteTestBase
{
    [Fact]
    public async Task ImporterSansOcr_StockeLeDocumentParProjetEtLeJournalise()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();
        var cheminSource = CreerFichierTemporaire(".pdf", "%PDF-1.4 test");

        try
        {
            var document = await Ged.ImporterAsync(
                new DemandeImportDocument(
                    projet.Id, cheminSource, EntiteLieeType: nameof(Marche), EntiteLieeId: marche.Id),
                UtilisateurTestId);

            Assert.Equal("PDF", document.Categorie);
            Assert.Equal(1, document.Version);
            Assert.Null(document.ContenuOcr);
            Assert.Contains("sans OCR", document.StatutOcr);
            Assert.Contains("TEST-001", document.CheminFichier);
            Assert.Contains("Marchés", document.CheminFichier);
            Assert.True(File.Exists(document.CheminFichier));
            Assert.Equal(nameof(Marche), document.EntiteLieeType);
            Assert.Equal(marche.Id, document.EntiteLieeId);

            var persiste = await Db.Documents.AsNoTracking().SingleAsync(d => d.Id == document.Id);
            Assert.Equal(projet.Id, persiste.ProjetId);

            var journal = await Journal.ListerAsync(nameof(DocumentGed), document.Id);
            Assert.Single(journal);
            Assert.Equal("Création", journal[0].ActionLibelle);

            File.Delete(document.CheminFichier);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    [Fact]
    public async Task ImporterDeuxFoisLeMemeFichier_IncrementeLaVersion()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        var cheminSource = CreerFichierTemporaire(".pdf", "%PDF-1.4 test");

        try
        {
            var premier = await Ged.ImporterAsync(
                new DemandeImportDocument(projet.Id, cheminSource), UtilisateurTestId);
            var deuxieme = await Ged.ImporterAsync(
                new DemandeImportDocument(projet.Id, cheminSource), UtilisateurTestId);

            Assert.Equal(1, premier.Version);
            Assert.Equal(2, deuxieme.Version);
            Assert.Equal(2, await Db.Documents.CountAsync());
            Assert.Equal(Path.GetFileName(cheminSource), premier.NomFichier);
            Assert.Equal(Path.GetFileName(cheminSource), deuxieme.NomFichier);
            Assert.Equal($"1_{Path.GetFileName(cheminSource)}", Path.GetFileName(premier.CheminFichier));
            Assert.Equal($"2_{Path.GetFileName(cheminSource)}", Path.GetFileName(deuxieme.CheminFichier));

            File.Delete(premier.CheminFichier);
            File.Delete(deuxieme.CheminFichier);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    [Fact]
    public async Task OcrDisponible_IndexeLeTexteEtLaRechercheIgnoreLesAccents()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Ocr.TextePdf = "Réhabilitation du gymnase municipal à Lyon";
        var cheminSource = CreerFichierTemporaire(".pdf", "%PDF-1.4 test");

        try
        {
            var document = await Ged.ImporterAsync(
                new DemandeImportDocument(projet.Id, cheminSource), UtilisateurTestId);

            Assert.Equal("OCR réussi", document.StatutOcr);
            Assert.Contains("gymnase", document.ContenuOcr, StringComparison.OrdinalIgnoreCase);

            var parTexte = await Ged.RechercherAsync(projet.Id, "rehabilitation gymnase");
            Assert.Single(parTexte);
            Assert.Equal(document.Id, parTexte[0].Id);

            var parMetadonnee = await Ged.RechercherAsync(projet.Id, "TEST-001 PDF");
            Assert.Single(parMetadonnee);

            var sansResultat = await Ged.RechercherAsync(projet.Id, "mot-absent");
            Assert.Empty(sansResultat);

            File.Delete(document.CheminFichier);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    [Fact]
    public async Task EchecDuTesseract_ImporteLeDocumentSansFaireEchouerLImport()
    {
        var (projet, _) = await CreerProjetEtMarcheAsync();
        Ocr.LeverException = true;
        var cheminSource = CreerFichierTemporaire(".png", "image factice");

        try
        {
            var document = await Ged.ImporterAsync(
                new DemandeImportDocument(projet.Id, cheminSource), UtilisateurTestId);

            Assert.Equal("Image", document.Categorie);
            Assert.Null(document.ContenuOcr);
            Assert.Contains("en échec", document.StatutOcr);
            Assert.True(File.Exists(document.CheminFichier));

            File.Delete(document.CheminFichier);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    private static string CreerFichierTemporaire(string extension, string contenu)
    {
        var chemin = Path.Combine(Path.GetTempPath(), $"smartlog-ged-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(chemin, contenu);
        return chemin;
    }
}
