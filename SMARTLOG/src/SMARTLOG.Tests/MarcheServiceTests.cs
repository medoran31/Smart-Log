using Microsoft.EntityFrameworkCore;
using Smartlog.Domain.Entities;
using Smartlog.Services;

namespace Smartlog.Tests;

public class MarcheServiceTests : SqliteTestBase
{
    [Fact]
    public async Task Bordereau_CalculeMontantContractuelEtTotal_JournaliseChaqueEcriture()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();

        var prix = await Marches.AjouterPrixUnitaireAsync(
            marche.Id,
            new PrixUnitaireCreation("PU-01", "Béton armé B30", "m³", 185m, 450m),
            UtilisateurTestId);

        Assert.Equal(83_250m, prix.MontantContractuel);
        Assert.Equal(83_250m, await Marches.CalculerTotalBordereauAsync(marche.Id, CancellationToken.None));

        var modifie = await Marches.ModifierPrixUnitaireAsync(
            new PrixUnitaireModification(prix.Id, "PU-01", "Béton armé B30", "m³", 190m, 450m),
            UtilisateurTestId);
        Assert.Equal(85_500m, modifie.MontantContractuel);

        var detail = await Marches.ObtenirAsync(projet.Id);
        Assert.NotNull(detail);
        Assert.Single(detail.PrixUnitaires);
        Assert.Equal("MARCHE-TEST-001", detail.ReferenceMarche);

        var historique = await Journal.ListerAsync(nameof(PrixUnitaire), prix.Id);
        Assert.Equal(2, historique.Count);
        Assert.Equal("Création", historique[1].ActionLibelle);
        Assert.Equal("Modification", historique[0].ActionLibelle);

        await Marches.SupprimerPrixUnitaireAsync(prix.Id, UtilisateurTestId);
        Assert.Equal(0, await Db.PrixUnitaires.CountAsync());
        Assert.Equal(
            "Suppression",
            (await Journal.ListerAsync(nameof(PrixUnitaire), prix.Id))[0].ActionLibelle);
    }

    [Fact]
    public async Task ImporterUnePieceDuMarche_StockeLeDocumentEtLeJournalise()
    {
        var (projet, marche) = await CreerProjetEtMarcheAsync();

        var cheminSource = Path.Combine(Path.GetTempPath(), $"smartlog-test-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(cheminSource, "%PDF-1.4 document de test");

        try
        {
            var document = await Marches.ImporterDocumentAsync(
                marche.Id, cheminSource, UtilisateurTestId, CancellationToken.None);

            Assert.Equal("PDF", document.Categorie);
            Assert.Equal(Path.GetFileName(cheminSource), document.NomFichier);
            Assert.True(File.Exists(document.CheminFichier));

            var persiste = await Db.Documents.AsNoTracking().SingleAsync(d => d.Id == document.Id);
            Assert.Equal(nameof(Marche), persiste.EntiteLieeType);
            Assert.Equal(marche.Id, persiste.EntiteLieeId);
            Assert.Equal(projet.Id, persiste.ProjetId);

            var detail = await Marches.ObtenirAsync(projet.Id);
            Assert.NotNull(detail);
            Assert.Single(detail.Documents);

            var journal = await Journal.ListerAsync(nameof(DocumentGed), document.Id);
            Assert.Single(journal);
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }

    [Fact]
    public async Task ImporterUnFormatNonAutorise_EchoueSansEcrire()
    {
        var (_, marche) = await CreerProjetEtMarcheAsync();

        var cheminSource = Path.Combine(Path.GetTempPath(), $"smartlog-test-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(cheminSource, "format interdit");

        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                Marches.ImporterDocumentAsync(marche.Id, cheminSource, UtilisateurTestId));
            Assert.Equal(0, await Db.Documents.CountAsync());
        }
        finally
        {
            File.Delete(cheminSource);
        }
    }
}
