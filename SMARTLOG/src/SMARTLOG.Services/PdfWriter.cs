using System.Text;

namespace Smartlog.Services;

/// <summary>
/// Écrit un PDF 1.4 simple (polices standards Helvetica, plusieurs pages),
/// sans dépendance externe. Le contenu est encodé en Latin-1, ce qui couvre
/// les caractères français courants.
/// </summary>
internal static class PdfWriter
{
    private const int LargeurLigne = 92;
    private const int LignesParPage = 52;

    public static byte[] Generer(string titre, string contenu)
    {
        var lignes = DecouperEnLignes($"{titre}\n\n{contenu}");
        var pages = DecouperEnPages(lignes);
        var pdf = new StringBuilder("%PDF-1.4\n");
        var positions = new Dictionary<int, int>();

        AjouterObjet(pdf, positions, 1, "<< /Type /Catalog /Pages 2 0 R >>");
        var referencesPages = string.Join(" ", pages.Select((_, i) => $"{4 + i * 2} 0 R"));
        AjouterObjet(pdf, positions, 2,
            $"<< /Type /Pages /Kids [{referencesPages}] /Count {pages.Count} >>");
        AjouterObjet(pdf, positions, 3,
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

        for (var i = 0; i < pages.Count; i++)
        {
            var numeroPage = 4 + i * 2;
            var numeroContenu = numeroPage + 1;
            AjouterObjet(pdf, positions, numeroPage,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] " +
                $"/Resources << /Font << /F1 3 0 R >> >> /Contents {numeroContenu} 0 R >>");

            var flux = ConstruireFlux(pages[i]);
            AjouterObjet(pdf, positions, numeroContenu,
                $"<< /Length {Encoding.Latin1.GetByteCount(flux)} >>\nstream\n{flux}endstream");
        }

        var nombreObjets = 3 + pages.Count * 2;
        var positionXref = pdf.Length;
        pdf.Append("xref\n0 ").Append(nombreObjets + 1).Append('\n')
            .Append("0000000000 65535 f \n");
        for (var numero = 1; numero <= nombreObjets; numero++)
            pdf.Append(positions[numero].ToString("D10", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" 00000 n \n");

        pdf.Append("trailer\n<< /Size ").Append(nombreObjets + 1)
            .Append(" /Root 1 0 R >>\nstartxref\n").Append(positionXref).Append("\n%%EOF\n");

        return Encoding.Latin1.GetBytes(pdf.ToString());
    }

    private static void AjouterObjet(
        StringBuilder pdf,
        Dictionary<int, int> positions,
        int numero,
        string corps)
    {
        positions[numero] = pdf.Length;
        pdf.Append(numero).Append(" 0 obj\n").Append(corps).Append("\nendobj\n");
    }

    private static string ConstruireFlux(IReadOnlyList<string> lignes)
    {
        var flux = new StringBuilder("BT\n/F1 11 Tf\n50 792 Td\n14 TL\n");
        foreach (var ligne in lignes)
            flux.Append(Echapper(ligne)).Append(" Tj\nT*\n");
        flux.Append("ET\n");
        return flux.ToString();
    }

    private static string Echapper(string texte)
    {
        var résultat = new StringBuilder("(");
        foreach (var caractère in texte)
        {
            if (caractère is '\\' or '(' or ')')
            {
                résultat.Append('\\').Append(caractère);
                continue;
            }

            // WinAnsiEncoding : €=0x80, ‘’=0x91/0x92, “”=0x93/0x94,
            // –—=0x96/0x97, «»=0xAB/0xBB.
            var code = caractère switch
            {
                '€' => 128,
                '\'' or '’' => 146,
                '"' or '“' or '”' => 147,
                '–' => 150,
                '—' => 151,
                '«' => 171,
                '»' => 187,
                _ => caractère
            };

            résultat.Append(code is >= 32 and <= 255 ? (char)code : '?');
        }
        return résultat.Append(')').ToString();
    }

    private static List<string> DecouperEnLignes(string texte)
    {
        var résultat = new List<string>();
        foreach (var ligne in texte.Replace("\r\n", "\n").Split('\n'))
        {
            if (ligne.Length <= LargeurLigne)
            {
                résultat.Add(ligne);
                continue;
            }

            var morceaux = ligne.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var courante = new StringBuilder();
            foreach (var mot in morceaux)
            {
                if (courante.Length > 0 && courante.Length + mot.Length + 1 > LargeurLigne)
                {
                    résultat.Add(courante.ToString());
                    courante.Clear();
                }
                if (courante.Length > 0)
                    courante.Append(' ');
                courante.Append(mot);
            }
            if (courante.Length > 0)
                résultat.Add(courante.ToString());
        }
        return résultat;
    }

    private static List<List<string>> DecouperEnPages(IReadOnlyList<string> lignes)
    {
        var pages = new List<List<string>>();
        for (var index = 0; index < lignes.Count; index += LignesParPage)
            pages.Add(lignes.Skip(index).Take(LignesParPage).ToList());

        return pages.Count == 0 ? new List<List<string>> { new() { string.Empty } } : pages;
    }
}
