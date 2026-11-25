using System.Diagnostics;

namespace Smartlog.Services.Ged;

/// <summary>
/// OCR basé sur l'exécutable Tesseract (et pdftoppm pour rastériser les PDF).
/// Aucun package NuGet natif n'est imposé : si les exécutables ne sont pas
/// installés, les méthodes renvoient null et l'import de documents continue.
/// </summary>
public class TesseractOcrService : IOcrService
{
    private static readonly string[] ExtensionsImages =
        { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".gif" };

    private static readonly string[] CheminsTesseractConnus =
    {
        @"C:\Program Files\Tesseract-OCR\tesseract.exe",
        @"C:\Program Files (x86)\Tesseract-OCR\tesseract.exe"
    };

    private static readonly string[] CheminsPdftoppmConnus =
    {
        @"C:\Program Files\poppler\bin\pdftoppm.exe",
        @"C:\Program Files (x86)\poppler\bin\pdftoppm.exe"
    };

    private string? _cheminTesseract;
    private bool _tesseractRecherche;
    private string? _cheminPdftoppm;
    private bool _pdftoppmRecherche;

    public bool EstDisponible => ObtenirTesseract() is not null;

    public async Task<string?> ExtraireTexteAsync(string cheminImage, CancellationToken ct = default)
    {
        var executable = ObtenirTesseract();
        if (executable is null || !ExtensionsImages.Contains(Path.GetExtension(cheminImage), StringComparer.OrdinalIgnoreCase))
            return null;

        // Tente le français, puis l'anglais si le modèle de langue n'est pas installé.
        var texte = await ExecuterAsync(executable, new[] { cheminImage, "stdout", "-l", "fra+eng" }, ct);
        return texte ?? await ExecuterAsync(executable, new[] { cheminImage, "stdout", "-l", "eng" }, ct);
    }

    public async Task<string?> ExtraireTextePdfAsync(string cheminPdf, CancellationToken ct = default)
    {
        var tesseract = ObtenirTesseract();
        var pdftoppm = ObtenirPdftoppm();
        if (tesseract is null || pdftoppm is null ||
            !string.Equals(Path.GetExtension(cheminPdf), ".pdf", StringComparison.OrdinalIgnoreCase))
            return null;

        var dossierTemporaire = Path.Combine(
            Path.GetTempPath(), "smartlog-ocr-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossierTemporaire);

        try
        {
            var prefix = Path.Combine(dossierTemporaire, "page");
            await ExecuterAsync(
                pdftoppm,
                new[] { "-f", "1", "-l", "1", "-png", "-r", "150", cheminPdf, prefix },
                ct);

            var image = Directory.GetFiles(dossierTemporaire, "*.png")
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (image is null)
                return null;

            var texte = await ExecuterAsync(tesseract, new[] { image, "stdout", "-l", "fra+eng" }, ct);
            return texte ?? await ExecuterAsync(tesseract, new[] { image, "stdout", "-l", "eng" }, ct);
        }
        catch (Exception)
        {
            // L'OCR est best-effort : un échec ne doit jamais bloquer l'import.
            return null;
        }
        finally
        {
            try { Directory.Delete(dossierTemporaire, true); } catch (Exception) { /* nettoyage tolérant */ }
        }
    }

    private string? ObtenirTesseract()
    {
        if (!_tesseractRecherche)
        {
            _cheminTesseract = TrouverExecutable("tesseract", CheminsTesseractConnus);
            _tesseractRecherche = true;
        }
        return _cheminTesseract;
    }

    private string? ObtenirPdftoppm()
    {
        if (!_pdftoppmRecherche)
        {
            _cheminPdftoppm = TrouverExecutable("pdftoppm", CheminsPdftoppmConnus);
            _pdftoppmRecherche = true;
        }
        return _cheminPdftoppm;
    }

    private static string? TrouverExecutable(string nom, IEnumerable<string> cheminsConnus)
    {
        var noms = OperatingSystem.IsWindows()
            ? new[] { nom + ".exe", nom }
            : new[] { nom };

        var dossiers = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(d => !string.IsNullOrWhiteSpace(d));

        foreach (var dossier in dossiers)
        {
            foreach (var nomCandidat in noms)
            {
                var candidat = Path.Combine(dossier.Trim('"'), nomCandidat);
                if (File.Exists(candidat))
                    return candidat;
            }
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidatsConnus = cheminsConnus.Concat(new[]
        {
            Path.Combine(localAppData, "Programs", "Tesseract-OCR", "tesseract.exe"),
            Path.Combine(localAppData, "Programs", "poppler", "bin", "pdftoppm.exe")
        });

        return candidatsConnus.FirstOrDefault(File.Exists);
    }

    private static async Task<string?> ExecuterAsync(
        string executable,
        IReadOnlyList<string> arguments,
        CancellationToken ct)
    {
        try
        {
            var demarrage = new ProcessStartInfo(executable)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                demarrage.ArgumentList.Add(argument);

            using var processus = Process.Start(demarrage);
            if (processus is null)
                return null;

            using var delais = CancellationTokenSource.CreateLinkedTokenSource(ct);
            delais.CancelAfter(TimeSpan.FromSeconds(45));

            var tacheSortie = processus.StandardOutput.ReadToEndAsync();
            var tacheErreur = processus.StandardError.ReadToEndAsync();
            await processus.WaitForExitAsync(delais.Token);

            if (processus.ExitCode != 0)
                return null;

            var sortie = await tacheSortie;
            _ = await tacheErreur;
            return string.IsNullOrWhiteSpace(sortie) ? null : sortie.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
