using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Smartlog.Services;

/// <summary>
/// Copie cohérente de la base SQLite avec VACUUM INTO (aucune connexion à
/// fermer, y compris en WAL), des documents GED et des métadonnées locales.
/// La restauration est appliquée au démarrage, avant toute ouverture EF.
/// </summary>
public class SauvegardeService : ISauvegardeService
{
    private const string NomFichierBase = "smartlog.db";
    private const string NomMarqueur = "restauration-en-attente.txt";
    private const string NomDossierDocuments = "Documents";

    public string CheminBase { get; }
    public string DossierSauvegardes { get; }
    public string CheminMarqueur => Path.Combine(
        Path.GetDirectoryName(CheminBase)!, NomMarqueur);

    public SauvegardeService(string cheminBase, string? dossierSauvegardes = null)
    {
        if (string.IsNullOrWhiteSpace(cheminBase))
            throw new ArgumentException("Le chemin de la base est obligatoire.", nameof(cheminBase));

        CheminBase = Path.GetFullPath(cheminBase);
        DossierSauvegardes = Path.GetFullPath(
            dossierSauvegardes ?? Path.Combine(Path.GetDirectoryName(CheminBase)!, "Sauvegardes"));
    }

    public async Task<ResultatSauvegarde> CreerAsync(
        string? description = null, CancellationToken ct = default)
    {
        if (!File.Exists(CheminBase))
            return new ResultatSauvegarde(false, "La base SMARTLOG est introuvable.");

        try
        {
            Directory.CreateDirectory(DossierSauvegardes);
            var nom = "Sauvegarde_" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var cheminDossier = CheminLibre(Path.Combine(DossierSauvegardes, nom));
            Directory.CreateDirectory(cheminDossier);

            var cheminCopie = Path.Combine(cheminDossier, NomFichierBase);
            using (var connexion = new SqliteConnection($"Data Source={CheminBase};Pooling=False"))
            {
                await connexion.OpenAsync(ct).ConfigureAwait(false);
                using var commande = connexion.CreateCommand();
                commande.CommandText = $"VACUUM INTO '{Echapper(cheminCopie)}';";
                await commande.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            var contientDocuments = CopierDocuments(cheminDossier);
            await File.WriteAllTextAsync(
                Path.Combine(cheminDossier, "sauvegarde.json"),
                JsonSerializer.Serialize(new
                {
                    DateCreation = DateTime.Now,
                    Description = description ?? "Sauvegarde manuelle",
                    Version = 1
                }),
                ct).ConfigureAwait(false);

            var resume = await VersResume(cheminDossier, ct).ConfigureAwait(false);
            return new ResultatSauvegarde(
                true,
                contientDocuments
                    ? "Sauvegarde de la base et des documents terminée."
                    : "Sauvegarde de la base terminée (aucun document GED à copier).",
                resume);
        }
        catch (Exception ex)
        {
            return new ResultatSauvegarde(false, $"Sauvegarde impossible : {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<SauvegardeResume>> ListerAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(DossierSauvegardes))
            return Array.Empty<SauvegardeResume>();

        var resultats = new List<SauvegardeResume>();
        foreach (var dossier in Directory.EnumerateDirectories(DossierSauvegardes, "Sauvegarde_*"))
        {
            if (!File.Exists(Path.Combine(dossier, NomFichierBase)))
                continue;
            resultats.Add(await VersResume(dossier, ct).ConfigureAwait(false));
        }

        return resultats.OrderByDescending(s => s.DateCreation).ToList();
    }

    public async Task<ResultatRestauration> PreparerRestaurationAsync(
        string cheminSauvegarde, CancellationToken ct = default)
    {
        try
        {
            var dossier = Path.GetFullPath(cheminSauvegarde);
            var cheminCopie = Path.Combine(dossier, NomFichierBase);
            if (!File.Exists(cheminCopie))
                return new ResultatRestauration(false, "Ce dossier ne contient pas de base SMARTLOG.");

            var integrite = await VerifierIntegriteAsync(cheminCopie, ct).ConfigureAwait(false);
            if (!string.Equals(integrite, "ok", StringComparison.OrdinalIgnoreCase))
                return new ResultatRestauration(
                    false, $"La copie est corrompue (integrity_check : {integrite}).");

            await File.WriteAllTextAsync(CheminMarqueur, dossier, ct).ConfigureAwait(false);
            return new ResultatRestauration(
                true,
                "Restauration vérifiée. Fermez SMARTLOG puis relancez-le pour l'appliquer.");
        }
        catch (Exception ex)
        {
            return new ResultatRestauration(false, $"Restauration impossible : {ex.Message}");
        }
    }

    public Task<bool> SupprimerAsync(string cheminSauvegarde, CancellationToken ct = default)
    {
        try
        {
            var dossier = Path.GetFullPath(cheminSauvegarde);
            var racine = DossierSauvegardes.TrimEnd(Path.DirectorySeparatorChar) +
                         Path.DirectorySeparatorChar;
            if (!dossier.StartsWith(racine, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(dossier.TrimEnd(Path.DirectorySeparatorChar),
                    DossierSauvegardes.TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(false);

            Directory.Delete(dossier, true);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Applique une restauration programmée. À appeler avant la création du
    /// DbContext et des migrations, au tout début de l'exécution.
    /// </summary>
    public static string? AppliquerRestaurationEnAttente(string cheminBase)
    {
        var dossierBase = Path.GetDirectoryName(Path.GetFullPath(cheminBase))!;
        var cheminMarqueur = Path.Combine(dossierBase, NomMarqueur);
        if (!File.Exists(cheminMarqueur))
            return null;

        try
        {
            var dossierSauvegarde = File.ReadAllText(cheminMarqueur).Trim();
            var cheminCopie = Path.Combine(dossierSauvegarde, NomFichierBase);
            if (!File.Exists(cheminCopie))
                throw new InvalidOperationException("La sauvegarde programmée est introuvable.");

            var horodatage = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string? cheminSecurite = null;
            if (File.Exists(cheminBase))
            {
                cheminSecurite = $"{cheminBase}.avant-restauration-{horodatage}";
                File.Copy(cheminBase, cheminSecurite, true);
            }

            File.Copy(cheminCopie, cheminBase, true);
            SupprimerFichiersJournaux(cheminBase);

            var dossierDocuments = Path.Combine(dossierBase, NomDossierDocuments);
            var documentsSauvegardes = Path.Combine(dossierSauvegarde, NomDossierDocuments);
            if (Directory.Exists(documentsSauvegardes))
            {
                if (Directory.Exists(dossierDocuments))
                    Directory.Move(dossierDocuments, $"{dossierDocuments}.avant-restauration-{horodatage}");
                CopierRecursif(documentsSauvegardes, dossierDocuments);
            }

            File.Delete(cheminMarqueur);
            return cheminSecurite is null
                ? "Sauvegarde restaurée au démarrage."
                : $"Sauvegarde restaurée au démarrage. L'ancienne base a été conservée : {cheminSecurite}";
        }
        catch
        {
            // Le marqueur est conservé : l'utilisateur pourra retenter au
            // prochain lancement sans perdre la base courante.
            return "La restauration programmée a échoué ; la base courante n'a pas été remplacée.";
        }
    }

    private async Task<SauvegardeResume> VersResume(string dossier, CancellationToken ct)
    {
        var cheminDb = Path.Combine(dossier, NomFichierBase);
        var taille = new FileInfo(cheminDb).Length;
        var description = "Sauvegarde manuelle";
        var cheminMetadata = Path.Combine(dossier, "sauvegarde.json");
        if (File.Exists(cheminMetadata))
        {
            try
            {
                using var document = JsonDocument.Parse(
                    await File.ReadAllTextAsync(cheminMetadata, ct).ConfigureAwait(false));
                if (document.RootElement.TryGetProperty("Description", out var valeur))
                    description = valeur.GetString() ?? description;
            }
            catch (JsonException)
            {
                // Métadonnées endommagées : la copie de la base reste valide.
            }
        }

        return new SauvegardeResume(
            Path.GetFileName(dossier),
            dossier,
            File.GetCreationTime(cheminDb),
            taille,
            Directory.Exists(Path.Combine(dossier, NomDossierDocuments)),
            description);
    }

    private static async Task<string> VerifierIntegriteAsync(
        string cheminBase, CancellationToken ct)
    {
        try
        {
            using var connexion = new SqliteConnection(
                $"Data Source={cheminBase};Mode=ReadOnly;Pooling=False");
            await connexion.OpenAsync(ct).ConfigureAwait(false);
            using var commande = connexion.CreateCommand();
            commande.CommandText = "PRAGMA integrity_check;";
            return (await commande.ExecuteScalarAsync(ct).ConfigureAwait(false))?.ToString() ?? "resultat vide";
        }
        catch (SqliteException ex)
        {
            // Fichier absent, illisible ou qui n'est pas une base SQLite.
            return $"invalide ({ex.Message})";
        }
    }

    private static void SupprimerFichiersJournaux(string cheminBase)
    {
        foreach (var suffixe in new[] { "-journal", "-wal", "-shm" })
        {
            var fichier = cheminBase + suffixe;
            if (File.Exists(fichier))
                File.Delete(fichier);
        }
    }

    private static string CheminLibre(string chemin)
    {
        if (!Directory.Exists(chemin))
            return chemin;

        for (var i = 2; i < 1000; i++)
        {
            var candidat = $"{chemin}-{i}";
            if (!Directory.Exists(candidat))
                return candidat;
        }

        throw new IOException("Impossible de créer un dossier de sauvegarde unique.");
    }

    private bool CopierDocuments(string cheminDossier)
    {
        var source = Path.Combine(Path.GetDirectoryName(CheminBase)!, NomDossierDocuments);
        if (!Directory.Exists(source))
            return false;

        CopierRecursif(source, Path.Combine(cheminDossier, NomDossierDocuments));
        return true;
    }

    private static void CopierRecursif(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var fichier in Directory.EnumerateFiles(source))
            File.Copy(fichier, Path.Combine(destination, Path.GetFileName(fichier)), true);

        foreach (var sousDossier in Directory.EnumerateDirectories(source))
            CopierRecursif(sousDossier, Path.Combine(destination, Path.GetFileName(sousDossier)));
    }

    private static string Echapper(string chemin) => chemin.Replace("'", "''");
}
