using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static Autodesk.AutoCAD.LayerManager.LayerFilter;

namespace KnxTools.Calc.Diag
{
    /// Выгрузка результатов проверок: диалог сохранения → книга Excel (через установленный Excel, COM без Interop-сборок).
    /// Лист «Результат» — что сработало; лист «Справочник проверок» — все проверки и сколько раз каждая сработала.
    /// Если Excel не установлен — рядом пишется CSV с тем же содержимым.
    public static class DiagExcel
    {
        static readonly string[] ResHead = { "№", "Код", "Уровень", "Шаг", "Проверка", "Щит", "Объект (Handle)", "Подробности", "Что сделать" };
        static readonly string[] CatHead = { "Код", "Уровень", "Шаг", "Проверка", "Что обнаружено", "Что сделать", "Найдено в этом расчёте" };

        /// Возвращает путь созданного файла или null. note — пояснение для командной строки (может быть null).
        public static string Export(DiagLog diag, string title, string dwgPath, out string note)
        {
            note = null;
            string path = AskPath(title, dwgPath);
            if (path == null) { note = "Выгрузка отменена."; return null; }

            string info = $"{title} — проверки расчёта щита.  Чертёж: {dwgPath}.  Дата: {DateTime.Now:dd.MM.yyyy HH:mm}";
            object[,] res = ResultTable(diag);
            object[,] cat = CatalogTable(diag);

            try
            {
                WriteExcel(path, info, res, cat);
                return path;
            }
            catch (Exception ex)
            {
                string csv = Path.ChangeExtension(path, ".csv");
                try { WriteCsv(csv, info, res, cat); }
                catch (Exception ex2) { note = "Не удалось создать ни Excel, ни CSV: " + ex.Message + " / " + ex2.Message; return null; }
                note = "Excel недоступен (" + ex.Message + ") — записан CSV.";
                return csv;
            }
        }

        // ------------------------------------------------------------------ таблицы
        static object[,] ResultTable(DiagLog diag)
        {
            var items = diag.Sorted.ToList();
            var t = new object[items.Count + 1, ResHead.Length];
            for (int c = 0; c < ResHead.Length; c++) t[0, c] = ResHead[c];
            for (int i = 0; i < items.Count; i++)
            {
                var x = items[i]; var k = x.Code; int r = i + 1;
                t[r, 0] = r; t[r, 1] = k.Id; t[r, 2] = k.LevelText; t[r, 3] = k.Steps; t[r, 4] = k.Title;
                t[r, 5] = x.Panel; t[r, 6] = x.Handle; t[r, 7] = x.Details; t[r, 8] = k.Action;
            }
            return t;
        }

        static object[,] CatalogTable(DiagLog diag)
        {
            var all = DiagCatalog.All;
            var t = new object[all.Length + 1, CatHead.Length];
            for (int c = 0; c < CatHead.Length; c++) t[0, c] = CatHead[c];
            for (int i = 0; i < all.Length; i++)
            {
                var k = all[i]; int r = i + 1;
                t[r, 0] = k.Id; t[r, 1] = k.LevelText; t[r, 2] = k.Steps; t[r, 3] = k.Title;
                t[r, 4] = k.What; t[r, 5] = k.Action; t[r, 6] = diag.CountOf(k);
            }
            return t;
        }

        // ------------------------------------------------------------------ диалог
        // ------------------------------------------------------------------ диалог (WPF, без Windows.Forms)
        static string AskPath(string title, string dwgPath)
        {
            string dir = "", baseName = "";
            try { dir = Path.GetDirectoryName(dwgPath) ?? ""; baseName = Path.GetFileNameWithoutExtension(dwgPath); } catch { }
            if (dir.Length == 0 || !Directory.Exists(dir)) dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(baseName)) baseName = "Чертеж";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = title + " — сохранить отчёт проверок",
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = dir,
                FileName = $"{baseName}_CALC_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        // ------------------------------------------------------------------ Excel через COM
        static void WriteExcel(string path, string info, object[,] res, object[,] cat)
        {
            Type t = Type.GetTypeFromProgID("Excel.Application");
            if (t == null) throw new InvalidOperationException("Excel не установлен");

            dynamic xl = Activator.CreateInstance(t);
            dynamic wb = null;
            try
            {
                xl.Visible = false;
                xl.DisplayAlerts = false;
                wb = xl.Workbooks.Add();

                dynamic ws1 = wb.Worksheets[1];
                ws1.Name = "Результат";
                if (wb.Worksheets.Count < 2) wb.Worksheets.Add(Type.Missing, ws1);
                dynamic ws2 = wb.Worksheets[2];
                ws2.Name = "Справочник проверок";

                Fill(ws1, info, res, new[] { 7 }, new[] { 8, 9 });
                Fill(ws2, "Все проверки команды SDK_CALC_PANEL. Колонка «Найдено» — сколько раз проверка сработала в этом расчёте.",
                     cat, null, new[] { 5, 6 });

                ws1.Activate();
                if (File.Exists(path)) File.Delete(path);
                wb.SaveAs(path, 51);   // 51 = xlOpenXMLWorkbook (.xlsx)
            }
            finally
            {
                try { if (wb != null) wb.Close(false); } catch { }
                try { xl.Quit(); } catch { }
                try { Marshal.FinalReleaseComObject((object)xl); } catch { }
            }
        }

        /// info — в A1; таблица с заголовком — с 3-й строки. textCols — колонки «как текст» (Handle вида 2E1 иначе станет числом).
        static void Fill(dynamic ws, string info, object[,] table, int[] textCols, int[] wideCols)
        {
            int rows = table.GetLength(0), cols = table.GetLength(1);

            ws.Cells[1, 1].Value2 = info;
            ws.Cells[1, 1].Font.Bold = true;

            if (textCols != null) foreach (int c in textCols) ws.Columns[c].NumberFormat = "@";

            dynamic rng = ws.Range[ws.Cells[3, 1], ws.Cells[2 + rows, cols]];
            rng.Value2 = table;
            rng.VerticalAlignment = -4160;               // xlTop

            dynamic head = ws.Range[ws.Cells[3, 1], ws.Cells[3, cols]];
            head.Font.Bold = true;
            head.Interior.Color = 0xEFEFEF;

            ws.Columns.AutoFit();
            if (wideCols != null)
                foreach (int c in wideCols)
                {
                    ws.Columns[c].ColumnWidth = 60;
                    ws.Columns[c].WrapText = true;
                }
        }

        // ------------------------------------------------------------------ CSV (запасной вариант)
        static void WriteCsv(string path, string info, object[,] res, object[,] cat)
        {
            var sb = new StringBuilder();
            sb.AppendLine(info);
            sb.AppendLine();
            Append(sb, res);
            sb.AppendLine();
            sb.AppendLine("СПРАВОЧНИК ПРОВЕРОК");
            Append(sb, cat);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static void Append(StringBuilder sb, object[,] t)
        {
            int rows = t.GetLength(0), cols = t.GetLength(1);
            for (int r = 0; r < rows; r++)
            {
                var cells = new string[cols];
                for (int c = 0; c < cols; c++)
                {
                    string s = t[r, c] == null ? "" : t[r, c].ToString();
                    cells[c] = "\"" + s.Replace("\"", "\"\"") + "\"";
                }
                sb.AppendLine(string.Join(";", cells));
            }
        }
    }
}
