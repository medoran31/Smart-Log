namespace Smartlog.Services.Ged;

/// <summary>
/// Adaptateur OCR. L'implémentation s'appuie sur Tesseract ; lorsqu'il est
/// absent, les méthodes renvoient null et l'import continue sans OCR.
/// </summary>
public interface IOcrService
{
    bool EstDisponible { get; }

    /// <summary>OCR d'une image (PNG, JPEG, BMP, TIFF...). Null si indisponible.</summary>
    Task<string?> ExtraireTexteAsync(string cheminImage, CancellationToken ct = default);

    /// <summary>OCR d'un PDF-scanné (rastérisation puis OCR). Null si indisponible.</summary>
    Task<string?> ExtraireTextePdfAsync(string cheminPdf, CancellationToken ct = default);
}
