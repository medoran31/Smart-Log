using Microsoft.Data.Sqlite;
using Smartlog.Services;

namespace Smartlog.Tests;

/// <summary>
/// Vérifie la copie cohérente de la base, l'intégrité avant restauration et
/// l'application différée au démarrage (jamais sur une base ouverte).
/// </summary>
public class SauvegardeServiceTests : IDisposable
{
    private readonly string _dossier;
    private readonly string _cheminBase;
    private readonly SauvegardeService _service;

    public SauvegardeServiceTests()
    {
        _dossier = Path.Combine(Path.GetTempPath(), "smartlog-sauvegarde-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dossier);
        _cheminBase = Path.Combine(_dossier, "smartlog.db");
        _service = new SauvegardeService(_cheminBase);
        CreerBase("Valeur initiale");
    }

    [Fact]
    public async Task CreerAsync_CopieLaBaseEtLesDocuments_etJournaliseLaDescription()
    {
        var dossierDocuments = Path.Combine(_dossier, "Documents", "2026");
        Directory.CreateDirectory(dossierDocuments);
        await File.WriteAllTextAsync(Path.Combine(dossierDocuments, "plan.txt"), "Plan de test");

        var resultat = await _service.CreerAsync("Avant travaux");

        Assert.True(resultat.Succes, resultat.Message);
        Assert.NotNull(resultat.Sauvegarde);
        Assert.True(resultat.Sauvegarde.ContientDocuments);
        Assert.True(File.Exists(Path.Combine(resultat.Sauvegarde.Chemin, "smartlog.db")));
        Assert.True(File.Exists(
            Path.Combine(resultat.Sauvegarde.Chemin, "Documents", "2026", "plan.txt")));

        var sauvegardes = await _service.ListerAsync();
        var resume = Assert.Single(sauvegardes);
        Assert.Equal("Avant travaux", resume.Description);
        Assert.True(resume.TailleOctets > 0);
        Assert.Equal("ok", await IntegriteAsync(
            Path.Combine(resume.Chemin, "smartlog.db")));
    }

    [Fact]
    public async Task PreparerRestaurationAsync_VerifieLaCopiePuisRestaureAuDemarrageEnConservantLancienneBase()
    {
        var sauvegarde = await _service.CreerAsync("Point de repère");
        Assert.True(sauvegarde.Succes);

        // La base courante diverge volontairement de la sauvegarde.
        CreerBase("Valeur modifiée");
        Assert.Equal("Valeur modifiée", LireValeur(_cheminBase));

        var preparation = await _service.PreparerRestaurationAsync(sauvegarde.Sauvegarde!.Chemin);
        Assert.True(preparation.Succes, preparation.Message);
        Assert.Equal(sauvegarde.Sauvegarde.Chemin, (await File.ReadAllTextAsync(_service.CheminMarqueur)).Trim());

        var message = SauvegardeService.AppliquerRestaurationEnAttente(_cheminBase);

        Assert.Contains("restaurée au démarrage", message!);
        Assert.Equal("Valeur initiale", LireValeur(_cheminBase));
        Assert.False(File.Exists(_service.CheminMarqueur));
        var ancienne = Assert.Single(Directory.GetFiles(_dossier, "smartlog.db.avant-restauration-*"));
        Assert.Equal("Valeur modifiée", LireValeur(ancienne));
    }

    [Fact]
    public async Task PreparerRestaurationAsync_CopieCorrompue_RefuseSansEcrireLeMarqueur()
    {
        var sauvegarde = await _service.CreerAsync();
        Assert.True(sauvegarde.Succes);
        await File.WriteAllTextAsync(
            Path.Combine(sauvegarde.Sauvegarde!.Chemin, "smartlog.db"),
            "ceci n'est pas une base SQLite");

        var preparation = await _service.PreparerRestaurationAsync(sauvegarde.Sauvegarde.Chemin);

        Assert.False(preparation.Succes);
        Assert.Contains("corrompue", preparation.Message);
        Assert.False(File.Exists(_service.CheminMarqueur));
    }

    [Fact]
    public async Task SupprimerAsync_RefuseUnDossierHorsDuDossierDeSauvegardes()
    {
        Assert.False(await _service.SupprimerAsync(_dossier));

        var sauvegarde = await _service.CreerAsync();
        Assert.True(sauvegarde.Succes);
        Assert.False(await _service.SupprimerAsync(_service.DossierSauvegardes));
        Assert.True(await _service.SupprimerAsync(sauvegarde.Sauvegarde!.Chemin));
        Assert.Empty(await _service.ListerAsync());
    }

    [Fact]
    public void AppliquerRestaurationEnAttente_SansMarqueur_NeModifieRien()
    {
        Assert.Null(SauvegardeService.AppliquerRestaurationEnAttente(_cheminBase));
        Assert.Equal("Valeur initiale", LireValeur(_cheminBase));
    }

    private void CreerBase(string valeur)
    {
        if (File.Exists(_cheminBase))
            File.Delete(_cheminBase);

        using var connexion = new SqliteConnection($"Data Source={_cheminBase};Pooling=False");
        connexion.Open();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "CREATE TABLE IF NOT EXISTS Test (Valeur TEXT); " +
                               "DELETE FROM Test; INSERT INTO Test (Valeur) VALUES ($valeur);";
        commande.Parameters.AddWithValue("$valeur", valeur);
        commande.ExecuteNonQuery();
    }

    private static string LireValeur(string chemin)
    {
        using var connexion = new SqliteConnection($"Data Source={chemin};Mode=ReadOnly;Pooling=False");
        connexion.Open();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "SELECT Valeur FROM Test LIMIT 1;";
        return (string)commande.ExecuteScalar()!;
    }

    private static async Task<string> IntegriteAsync(string chemin)
    {
        using var connexion = new SqliteConnection($"Data Source={chemin};Mode=ReadOnly;Pooling=False");
        await connexion.OpenAsync();
        using var commande = connexion.CreateCommand();
        commande.CommandText = "PRAGMA integrity_check;";
        return (await commande.ExecuteScalarAsync())!.ToString()!;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dossier, true);
        }
        catch (IOException)
        {
            // Nettoyage best effort sur Windows (verrous antivirus).
        }
    }
}
