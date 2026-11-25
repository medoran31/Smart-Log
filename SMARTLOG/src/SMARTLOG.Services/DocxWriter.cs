using System.IO.Compression;
using System.Security;
using System.Text;

namespace Smartlog.Services;

/// <summary>
/// Écrit un fichier .docx minimal (Open XML) sans dépendance externe :
/// [Content_Types].xml, relations du paquet et word/document.xml.
/// </summary>
internal static class DocxWriter
{
    private const string Types = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
          <Default Extension="xml" ContentType="application/xml" />
          <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" />
        </Types>
        """;

    private const string Relations = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml" />
        </Relationships>
        """;

    public static byte[] Generer(string titre, string contenu)
    {
        var paragraphes = new StringBuilder();
        paragraphes.Append("<w:p><w:r><w:rPr><w:b/></w:rPr><w:t xml:space=\"preserve\">")
            .Append(Echapper(titre))
            .Append("</w:t></w:r></w:p>");

        foreach (var ligne in contenu.Replace("\r\n", "\n").Split('\n'))
        {
            paragraphes.Append("<w:p><w:r><w:t xml:space=\"preserve\">")
                .Append(Echapper(ligne))
                .Append("</w:t></w:r></w:p>");
        }

        var documentXml = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                          "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                          "<w:body>" + paragraphes +
                          "<w:sectPr><w:pgSz w:w=\"11906\" w:h=\"16838\"/>" +
                          "<w:pgMar w:top=\"1134\" w:right=\"1134\" w:bottom=\"1134\" w:left=\"1134\"/></w:sectPr>" +
                          "</w:body></w:document>";

        using var flux = new MemoryStream();
        using (var archive = new ZipArchive(flux, ZipArchiveMode.Create, true))
        {
            Ecrire(archive, "[Content_Types].xml", Types);
            Ecrire(archive, "_rels/.rels", Relations);
            Ecrire(archive, "word/document.xml", documentXml);
        }
        return flux.ToArray();
    }

    private static void Ecrire(ZipArchive archive, string nom, string contenu)
    {
        var entree = archive.CreateEntry(nom, CompressionLevel.Optimal);
        using var flux = entree.Open();
        using var ecrivain = new StreamWriter(flux, new UTF8Encoding(false));
        ecrivain.Write(contenu);
    }

    private static string Echapper(string texte) =>
        SecurityElement.Escape(texte ?? string.Empty) ?? string.Empty;
}
