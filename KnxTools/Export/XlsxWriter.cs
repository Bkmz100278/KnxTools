using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Text;

namespace KnxTools.Export
{
    /// Простой xlsx: несколько листов, первая строка жирным, ширины колонок. Нужна ссылка WindowsBase.
    public class XlsxWriter
    {
        public class Sheet
        {
            public string Name;
            public List<object[]> Rows = new List<object[]>();
            public double[] Widths;
            public int HeaderRows = 1;
        }

        private readonly List<Sheet> _sheets = new List<Sheet>();

        private const string NsMain = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private const string NsRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private const string RelDoc = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument";
        private const string RelSheet = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet";
        private const string RelStyles = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles";
        private const string CtWorkbook = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
        private const string CtSheet = "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml";
        private const string CtStyles = "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml";

        public Sheet AddSheet(string name)
        {
            foreach (var ch in new[] { '[', ']', ':', '*', '?', '/', '\\' }) name = name.Replace(ch, '_');
            if (name.Length > 31) name = name.Substring(0, 31);
            var s = new Sheet { Name = name };
            _sheets.Add(s);
            return s;
        }

        public void Save(string path)
        {
            if (_sheets.Count == 0) AddSheet("Лист1");
            using (var pkg = Package.Open(path, FileMode.Create, FileAccess.ReadWrite))
            {
                var wbUri = PackUriHelper.CreatePartUri(new Uri("/xl/workbook.xml", UriKind.Relative));
                var wb = pkg.CreatePart(wbUri, CtWorkbook, CompressionOption.Normal);
                pkg.CreateRelationship(wbUri, TargetMode.Internal, RelDoc);

                var stUri = PackUriHelper.CreatePartUri(new Uri("/xl/styles.xml", UriKind.Relative));
                var st = pkg.CreatePart(stUri, CtStyles, CompressionOption.Normal);
                wb.CreateRelationship(PackUriHelper.GetRelativeUri(wbUri, stUri), TargetMode.Internal, RelStyles);
                Write(st, StylesXml);

                var sbWb = new StringBuilder();
                sbWb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
                sbWb.Append($"<workbook xmlns=\"{NsMain}\" xmlns:r=\"{NsRel}\"><sheets>");
                for (int i = 0; i < _sheets.Count; i++)
                {
                    var shUri = PackUriHelper.CreatePartUri(new Uri($"/xl/worksheets/sheet{i + 1}.xml", UriKind.Relative));
                    var sh = pkg.CreatePart(shUri, CtSheet, CompressionOption.Normal);
                    var rel = wb.CreateRelationship(PackUriHelper.GetRelativeUri(wbUri, shUri), TargetMode.Internal, RelSheet);
                    Write(sh, SheetXml(_sheets[i]));
                    sbWb.Append($"<sheet name=\"{Esc(_sheets[i].Name)}\" sheetId=\"{i + 1}\" r:id=\"{rel.Id}\"/>");
                }
                sbWb.Append("</sheets></workbook>");
                Write(wb, sbWb.ToString());
            }
        }

        private static string SheetXml(Sheet s)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append($"<worksheet xmlns=\"{NsMain}\">");
            if (s.Widths != null && s.Widths.Length > 0)
            {
                sb.Append("<cols>");
                for (int i = 0; i < s.Widths.Length; i++)
                    sb.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{s.Widths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
                sb.Append("</cols>");
            }
            sb.Append("<sheetData>");
            for (int r = 0; r < s.Rows.Count; r++)
            {
                var row = s.Rows[r];
                if (row == null) continue;
                sb.Append($"<row r=\"{r + 1}\">");
                string style = r < s.HeaderRows ? " s=\"1\"" : "";
                for (int c = 0; c < row.Length; c++)
                {
                    var v = row[c];
                    if (v == null) continue;
                    string cellRef = ColName(c) + (r + 1);
                    if (v is int || v is long || v is double || v is float || v is decimal)
                        sb.Append($"<c r=\"{cellRef}\"{style}><v>{Convert.ToString(v, CultureInfo.InvariantCulture)}</v></c>");
                    else
                        sb.Append($"<c r=\"{cellRef}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(v.ToString())}</t></is></c>");
                }
                sb.Append("</row>");
            }
            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private const string StylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"" + NsMain + "\">" +
            "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
            "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
            "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        private static void Write(PackagePart part, string xml)
        {
            using (var st = part.GetStream(FileMode.Create, FileAccess.Write))
            using (var w = new StreamWriter(st, new UTF8Encoding(false)))
                w.Write(xml);
        }

        private static string ColName(int idx)
        {
            var s = "";
            idx++;
            while (idx > 0) { int m = (idx - 1) % 26; s = (char)('A' + m) + s; idx = (idx - 1) / 26; }
            return s;
        }

        private static string Esc(string s) =>
            s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}