using System.IO.Compression;
using System.Security;
using System.Text;

namespace Smartlog.Services;

/// <summary>
/// Écrit un classeur .xlsx minimal (Open XML) sans dépendance externe :
/// deux feuilles (Saisie et Aide), styles intégrés, cellules en texte inline.
/// Aucune bibliothèque Excel n'est nécessaire pour produire ou lire ces
/// fichiers dans l'application.
/// </summary>
internal static class XlsxWriter
{
    private const string FeuilleNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private const string Styles = """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
          <fonts count="4">
            <font><sz val="11"/><color rgb="FF1A1A1A"/><name val="Calibri"/></font>
            <font><b/><sz val="11"/><color rgb="FFFFFFFF"/><name val="Calibri"/></font>
            <font><b/><sz val="14"/><color rgb="FF1F4E5F"/><name val="Calibri"/></font>
            <font><i/><sz val="10"/><color rgb="FF666666"/><name val="Calibri"/></font>
          </fonts>
          <fills count="5">
            <fill><patternFill patternType="none"/></fill>
            <fill><patternFill patternType="gray125"/></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FF1F4E5F"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFE2F0D9"/><bgColor indexed="64"/></patternFill></fill>
            <fill><patternFill patternType="solid"><fgColor rgb="FFFFF2CC"/><bgColor indexed="64"/></patternFill></fill>
          </fills>
          <borders count="2">
            <border><left/><right/><top/><bottom/><diagonal/></border>
            <border>
              <left style="thin"><color rgb="FFBFBFBF"/></left>
              <right style="thin"><color rgb="FFBFBFBF"/></right>
              <top style="thin"><color rgb="FFBFBFBF"/></top>
              <bottom style="thin"><color rgb="FFBFBFBF"/></bottom>
              <diagonal/>
            </border>
          </borders>
          <cellStyleXfs count="1">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0"/>
          </cellStyleXfs>
          <cellXfs count="6">
            <xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/>
            <xf numFmtId="0" fontId="1" fillId="2" borderId="1" xfId="0" applyFont="1" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment horizontal="center" vertical="center" wrapText="1"/>
            </xf>
            <xf numFmtId="0" fontId="2" fillId="3" borderId="0" xfId="0" applyFont="1" applyFill="1"/>
            <xf numFmtId="0" fontId="0" fillId="3" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment vertical="center" wrapText="1"/>
            </xf>
            <xf numFmtId="0" fontId="0" fillId="4" borderId="1" xfId="0" applyFill="1" applyBorder="1" applyAlignment="1">
              <alignment vertical="center" wrapText="1"/>
            </xf>
            <xf numFmtId="0" fontId="3" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1">
              <alignment vertical="top" wrapText="1"/>
            </xf>
          </cellXfs>
          <cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles>
        </styleSheet>
        """;

    public static byte[] Generer(ModeleExcel modele, IReadOnlyList<JetonModele> jetons)
    {
        var types = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
            </Types>
            """;

        var relations = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """;

        var classeur = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                      xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets>
                <sheet name="Saisie" sheetId="1" r:id="rId1"/>
                <sheet name="Aide" sheetId="2" r:id="rId2"/>
              </sheets>
            </workbook>
            """;

        var relationsClasseur = """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
              <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """;

        using var flux = new MemoryStream();
        using (var archive = new ZipArchive(flux, ZipArchiveMode.Create, true))
        {
            Ecrire(archive, "[Content_Types].xml", types);
            Ecrire(archive, "_rels/.rels", relations);
            Ecrire(archive, "xl/workbook.xml", classeur);
            Ecrire(archive, "xl/_rels/workbook.xml.rels", relationsClasseur);
            Ecrire(archive, "xl/styles.xml", Styles);
            Ecrire(archive, "xl/worksheets/sheet1.xml", FeuilleSaisie(modele));
            Ecrire(archive, "xl/worksheets/sheet2.xml", FeuilleAide(modele, jetons));
        }

        return flux.ToArray();
    }

    private static string FeuilleSaisie(ModeleExcel modele)
    {
        var colonnes = modele.Colonnes;
        var derniereColonne = Lettre(colonnes.Count);
        const int premiereLigneSaisie = 5;
        const int nombreLignesSaisie = 25;
        var derniereLigne = premiereLigneSaisie + nombreLignesSaisie - 1;

        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
            .Append("<worksheet xmlns=\"").Append(FeuilleNamespace).Append("\">")
            .Append("<sheetViews><sheetView workbookViewId=\"0\">")
            .Append("<pane ySplit=\"4\" topLeftCell=\"A5\" activePane=\"bottomLeft\" state=\"frozen\"/>")
            .Append("</sheetView></sheetViews>")
            .Append("<sheetFormatPr defaultRowHeight=\"16\"/>")
            .Append("<cols>");
        for (var i = 0; i < colonnes.Count; i++)
            xml.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1)
                .Append("\" width=\"").Append(colonnes[i].Largeur.ToString("0.##"))
                .Append("\" customWidth=\"1\"/>");
        xml.Append("</cols><sheetData>");

        Ligne(xml, 1, (2, modele.Titre));
        Ligne(xml, 2, (5, $"Catégorie : {modele.Categorie} — montants exprimés en DA (dinar algérien)"));
        Ligne(xml, 3, (5, "Remplissez les cellules jaunes, puis importez ce classeur dans le projet via Documents ▸ Importer des pièces."));
        Ligne(xml, 4, colonnes.Select(c => (1, c.Intitule)).ToArray());

        for (var numero = premiereLigneSaisie; numero <= derniereLigne; numero++)
        {
            xml.Append("<row r=\"").Append(numero).Append("\">");
            for (var colonne = 1; colonne <= colonnes.Count; colonne++)
                xml.Append("<c r=\"").Append(Lettre(colonne)).Append(numero).Append("\" s=\"4\"/>");
            xml.Append("</row>");
        }

        xml.Append("</sheetData>")
            .Append("<autoFilter ref=\"A4:").Append(derniereColonne).Append(derniereLigne).Append("\"/>")
            .Append("<mergeCells count=\"3\">")
            .Append("<mergeCell ref=\"A1:").Append(derniereColonne).Append("1\"/>")
            .Append("<mergeCell ref=\"A2:").Append(derniereColonne).Append("2\"/>")
            .Append("<mergeCell ref=\"A3:").Append(derniereColonne).Append("3\"/>")
            .Append("</mergeCells>")
            .Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>")
            .Append("</worksheet>");
        return xml.ToString();
    }

    private static string FeuilleAide(ModeleExcel modele, IReadOnlyList<JetonModele> jetons)
    {
        var xml = new StringBuilder();
        xml.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
            .Append("<worksheet xmlns=\"").Append(FeuilleNamespace).Append("\">")
            .Append("<sheetViews><sheetView workbookViewId=\"0\"/></sheetViews>")
            .Append("<sheetFormatPr defaultRowHeight=\"16\"/>")
            .Append("<cols><col min=\"1\" max=\"1\" width=\"42\" customWidth=\"1\"/>")
            .Append("<col min=\"2\" max=\"2\" width=\"86\" customWidth=\"1\"/></cols>")
            .Append("<sheetData>");

        Ligne(xml, 1, (2, $"Aide — {modele.Titre}"));
        Ligne(xml, 2, (5, "Ce classeur est un modèle de saisie SMARTLOG. Il peut être archivé tel quel dans la GED du projet."));

        var ligne = 4;
        Ligne(xml, ligne++, (2, "Instructions"));
        foreach (var instruction in modele.Instructions)
            Ligne(xml, ligne++, (5, instruction));

        ligne++;
        Ligne(xml, ligne++, (2, "Colonnes de la feuille Saisie"));
        Ligne(xml, ligne++, (1, "Colonne"), (1, "Description"));
        foreach (var colonne in modele.Colonnes)
            Ligne(xml, ligne++, (3, colonne.Intitule), (5, colonne.Description));

        ligne++;
        Ligne(xml, ligne++, (2, "Jetons disponibles pour les modèles Word / PDF"));
        Ligne(xml, ligne++, (1, "Jeton"), (1, "Description"));
        foreach (var jeton in jetons)
            Ligne(xml, ligne++, (3, "{{" + jeton.Cle + "}}"), (5, jeton.Description));

        ligne++;
        Ligne(xml, ligne++, (2, "Légende"));
        Ligne(xml, ligne++, (5, "Jaune : cellule à remplir. Vert : information. Gras blanc : en-tête."));
        Ligne(xml, ligne++, (5, "Les montants sont saisis en DA. Les valeurs calculées (montant actualisé, délai actualisé, fin prévisionnelle) sont produites par SMARTLOG, jamais par ce classeur."));

        xml.Append("</sheetData>")
            .Append("<pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/>")
            .Append("</worksheet>");
        return xml.ToString();
    }

    private static void Ligne(StringBuilder xml, int numero, params (int Style, string Texte)[] cellules)
    {
        xml.Append("<row r=\"").Append(numero).Append("\">");
        for (var i = 0; i < cellules.Length; i++)
        {
            xml.Append("<c r=\"").Append(Lettre(i + 1)).Append(numero).Append("\" s=\"")
                .Append(cellules[i].Style).Append("\" t=\"inlineStr\"><is><t xml:space=\"preserve\">")
                .Append(Echapper(cellules[i].Texte)).Append("</t></is></c>");
        }
        xml.Append("</row>");
    }

    private static string Lettre(int index)
    {
        var reste = index - 1;
        var resultat = new StringBuilder();
        while (reste >= 0)
        {
            resultat.Insert(0, (char)('A' + reste % 26));
            reste = reste / 26 - 1;
        }
        return resultat.ToString();
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
