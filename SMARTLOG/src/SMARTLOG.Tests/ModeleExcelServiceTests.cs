using System.IO.Compression;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Vérifie que les modèles Excel livrés sont bien des classeurs Open XML
/// exploitables et que les saisies existantes ne sont jamais écrasées.
/// </summary>
public class ModeleExcelServiceTests : IDisposable
{
    private readonly string _dossier = Path.Combine(
        Path.GetTempPath(), "smartlog-modeles-" + Guid.NewGuid().ToString("N"));

    private readonly ModeleExcelService _service;

    public ModeleExcelServiceTests()
    {
        _service = new ModeleExcelService(new ModeleDocumentFactice(), _dossier);
    }

    [Fact]
    public void ListerModeles_CouvreToutesLesCategoriesMetier()
    {
        var modeles = _service.ListerModeles();

        Assert.Equal(14, modeles.Count);
        Assert.Equal(14, modeles.Select(m => m.NomFichier).Distinct().Count());
        Assert.Contains(modeles, m => m.NomFichier.Contains("Contrat"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Avenant"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Attachement"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Situation"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Reserve"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Finances"));
        Assert.Contains(modeles, m => m.NomFichier.Contains("Proces_Verbal"));
        Assert.All(modeles, m =>
        {
            Assert.NotEmpty(m.Titre);
            Assert.NotEmpty(m.Categorie);
            Assert.NotEmpty(m.Colonnes);
            Assert.NotEmpty(m.Instructions);
        });
    }

    [Fact]
    public async Task CreerModelesAsync_ProduitDesClasseursXlsxValides()
    {
        var fichiers = await _service.CreerModelesAsync();

        Assert.Equal(14, fichiers.Count);
        Assert.All(fichiers, f =>
        {
            Assert.True(f.Cree);
            Assert.True(File.Exists(f.Chemin));
            Assert.EndsWith(".xlsx", f.NomFichier);
        });

        foreach (var fichier in fichiers)
        {
            using var archive = ZipFile.OpenRead(fichier.Chemin);
            Assert.NotNull(archive.GetEntry("[Content_Types].xml"));
            Assert.NotNull(archive.GetEntry("_rels/.rels"));
            Assert.NotNull(archive.GetEntry("xl/workbook.xml"));
            Assert.NotNull(archive.GetEntry("xl/_rels/workbook.xml.rels"));
            Assert.NotNull(archive.GetEntry("xl/styles.xml"));

            var saisie = archive.GetEntry("xl/worksheets/sheet1.xml");
            var aide = archive.GetEntry("xl/worksheets/sheet2.xml");
            Assert.NotNull(saisie);
            Assert.NotNull(aide);

            using var lecteur = new StreamReader(saisie!.Open());
            var contenu = await lecteur.ReadToEndAsync();
            Assert.Contains("inlineStr", contenu);
            Assert.Contains("s=\"4\"", contenu); // cellules jaunes à remplir
        }
    }

    [Fact]
    public async Task CreerModelesAsync_NeEcrasePasUnModeleDejaRempli()
    {
        var premiers = await _service.CreerModelesAsync();
        var chemin = premiers[0].Chemin;
        await File.WriteAllTextAsync(chemin, "saisie utilisateur");

        var seconds = await _service.CreerModelesAsync();

        Assert.All(seconds, f => Assert.False(f.Cree));
        Assert.Equal("saisie utilisateur", await File.ReadAllTextAsync(chemin));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dossier))
            Directory.Delete(_dossier, true);
        GC.SuppressFinalize(this);
    }

    private sealed class ModeleDocumentFactice : IModeleDocumentService
    {
        public IReadOnlyList<JetonModele> ListerJetonsDisponibles() => new[]
        {
            new JetonModele("Projet.Code", "Code du projet"),
            new JetonModele("Projet.Intitule", "Intitulé du projet")
        };

        public Task<DocumentGenere> GenererAsync(
            ModeleDocumentDemande demande, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<DocumentGenere> GenererDepuisFichierAsync(
            Guid projetId,
            string cheminModele,
            string titre,
            FormatDocument format,
            Guid utilisateurId,
            IReadOnlyDictionary<string, string>? valeursSupplementaires = null,
            CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
