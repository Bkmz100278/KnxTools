using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace KnxTools.Spec
{
    /// Пишет .xlsx напрямую (OpenXML в zip). Excel на машине не нужен. Все форматы — в Styles().
    public static class SpecExcel
    {
        const string NS = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string NSR = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        // индексы в cellXfs (см. Styles)
        const int S_TEXT_C = 1;   // текст, центр/центр, перенос
        const int S_TEXT_L = 2;   // текст, влево/центр, перенос
        const int S_SECTION = 3;  // раздел: жирный чёрный, влево
        const int S_SUB = 4;      // подраздел: обычный чёрный, влево
        const int S_PANEL = 5;    // щит: жирный, влево
        const int S_QTY = 6;      // количество: целое число, центр
        const int S_HEAD = 7;     // шапка: жирный, центр
        const int S_BOLD_C = 8;   // жирный, центр (позиция щита / раздела)

        public static void Write(string path, IList<SpecRow> rows)
        {
            if (File.Exists(path)) File.Delete(path);
            using (var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                Put(zip, "[Content_Types].xml", ContentTypes());
                Put(zip, "_rels/.rels", RootRels());
                Put(zip, "xl/workbook.xml", Workbook());
                Put(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
                Put(zip, "xl/styles.xml", Styles());
                Put(zip, "xl/worksheets/sheet1.xml", Sheet(rows));
            }
        }

        static void Put(ZipArchive zip, string name, string xml)
        {
            var e = zip.CreateEntry(name, CompressionLevel.Optimal);
            using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(xml);
        }

        // ------------------------------------------------------------------ лист
        static string Sheet(IList<SpecRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"").Append(NS).Append("\" xmlns:r=\"").Append(NSR).Append("\">");
            sb.Append("<sheetPr><pageSetUpPr fitToPage=\"1\"/></sheetPr>");
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            sb.Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
            sb.Append("<cols>");
            var w = Names.SPEC_COL_WIDTHS;
            for (int i = 0; i < 8; i++)
                sb.Append("<col min=\"").Append(i + 1).Append("\" max=\"").Append(i + 1)
                  .Append("\" width=\"").Append(w[i].ToString(CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
            sb.Append("</cols><sheetData>");
            for (int r = 0; r < rows.Count; r++) AppendRow(sb, r + 1, rows[r]);
            sb.Append("</sheetData>");
            sb.Append("<pageMargins left=\"0.5\" right=\"0.4\" top=\"0.6\" bottom=\"0.6\" header=\"0.3\" footer=\"0.3\"/>");
            sb.Append("<pageSetup paperSize=\"9\" orientation=\"landscape\" fitToWidth=\"1\" fitToHeight=\"0\"/>");
            sb.Append("</worksheet>");
            return sb.ToString();
        }

        static void AppendRow(StringBuilder sb, int r, SpecRow row)
        {
            int pos, name, other;
            switch (row.Kind)
            {
                case SpecRowKind.Header: pos = S_HEAD; name = S_HEAD; other = S_HEAD; break;
                case SpecRowKind.Section: pos = S_BOLD_C; name = S_SECTION; other = S_TEXT_C; break;
                case SpecRowKind.SubHeader: pos = S_TEXT_C; name = S_SUB; other = S_TEXT_C; break;
                case SpecRowKind.Panel: pos = S_BOLD_C; name = S_PANEL; other = S_TEXT_C; break;
                default: pos = S_TEXT_C; name = S_TEXT_L; other = S_TEXT_C; break;
            }
            sb.Append("<row r=\"").Append(r).Append("\">");
            Str(sb, r, 0, row.Pos, pos);
            Str(sb, r, 1, row.Name, name);
            Str(sb, r, 2, row.Mark, other);
            Str(sb, r, 3, row.Code, other);
            Str(sb, r, 4, row.Maker, other);
            Str(sb, r, 5, row.Unit, other);
            if (row.Qty.HasValue) Numb(sb, r, 6, row.Qty.Value, S_QTY);
            else Str(sb, r, 6, row.QtyText, other);
            Str(sb, r, 7, row.Note, other);
            sb.Append("</row>");
        }

        static string Ref(int r, int c) => ((char)('A' + c)).ToString() + r;

        static void Str(StringBuilder sb, int r, int c, string text, int style)
        {
            sb.Append("<c r=\"").Append(Ref(r, c)).Append("\" s=\"").Append(style).Append('"');
            if (string.IsNullOrEmpty(text)) { sb.Append("/>"); return; }
            sb.Append(" t=\"inlineStr\"><is><t xml:space=\"preserve\">").Append(Esc(text)).Append("</t></is></c>");
        }

        /// Количество пишется целым числом: округление до целого, половина — вверх.
        static void Numb(StringBuilder sb, int r, int c, double v, int style)
        {
            double whole = Math.Round(v, MidpointRounding.AwayFromZero);
            sb.Append("<c r=\"").Append(Ref(r, c)).Append("\" s=\"").Append(style).Append("\"><v>")
              .Append(whole.ToString("0", CultureInfo.InvariantCulture)).Append("</v></c>");
        }

        static string Esc(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    default:
                        if (ch < 0x20 && ch != '\t' && ch != '\n' && ch != '\r') break;   // управляющие символы Excel не примет
                        sb.Append(ch); break;
                }
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------------ служебные части пакета
        static string ContentTypes() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        static string RootRels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        static string Workbook() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"" + NS + "\" xmlns:r=\"" + NSR + "\">" +
            "<sheets><sheet name=\"Спецификация\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        static string WorkbookRels() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        /// fonts: 0 обычный чёрный, 1 жирный чёрный. numFmt 49 = текст «@», 1 = встроенный целый «0».
        static string Styles() =>
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"" + NS + "\">" +
            "<fonts count=\"2\">" +
            "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
            "</fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"2\"><border><left/><right/><top/><bottom/><diagonal/></border>" +
            "<border><left style=\"thin\"><color auto=\"1\"/></left><right style=\"thin\"><color auto=\"1\"/></right>" +
            "<top style=\"thin\"><color auto=\"1\"/></top><bottom style=\"thin\"><color auto=\"1\"/></bottom><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"9\">" +
            "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +   // 0 по умолчанию
            Xf(49, 0, "center") +                                                          // 1 текст, центр
            Xf(49, 0, "left") +                                                            // 2 текст, влево
            Xf(49, 1, "left") +                                                            // 3 раздел — жирный чёрный
            Xf(49, 0, "left") +                                                            // 4 подраздел — обычный чёрный
            Xf(49, 1, "left") +                                                            // 5 щит — жирный
            Xf(1, 0, "center") +                                                           // 6 количество — целое «0»
            Xf(49, 1, "center") +                                                          // 7 шапка — жирный, центр
            Xf(49, 1, "center") +                                                          // 8 жирный, центр
            "</cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        static string Xf(int numFmt, int font, string h) =>
            "<xf numFmtId=\"" + numFmt + "\" fontId=\"" + font + "\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyNumberFormat=\"1\" applyFont=\"1\" applyBorder=\"1\" applyAlignment=\"1\">" +
            "<alignment horizontal=\"" + h + "\" vertical=\"center\" wrapText=\"1\"/></xf>";
    }
}