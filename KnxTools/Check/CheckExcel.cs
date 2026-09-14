using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
//using System.Windows.Forms;
using static Autodesk.AutoCAD.LayerManager.LayerFilter;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check
{
    /// Диалог сохранения → книга Excel через установленный Excel (COM, без Interop-сборок).
    /// Листы: «Результат» — срабатывания; «Устройства» — сводка по каждому контейнеру; «Справочник проверок» — все проверки, счётчик, флаги НЕТ.
    /// Нет Excel — рядом пишется CSV с тем же содержимым.
    public static class CheckExcel
    {
        static readonly string[] ResHead = { "№", "Код", "Уровень", "Проверка", "Щит", "Объект", "Handle", "Подробности", "Что сделать", "Как пропустить" };   // 10
        static readonly string[] DevHead = { "Щит", "Устройство", "Тип (по составу)", "Наименование (SDK_ОБ_KNX)", "Физ. адрес", "Линия", "Выходы нарис./задейств.", "Входы нарис./задейств.", "Групп DALI", "Питание 230 В", "Замечаний", "Handle контейнера" };
        static readonly string[] CatHead = { "Код", "Уровень", "Проверка", "Источник", "Что обнаружено", "Что сделать", "Как пропустить", "Найдено" };   // 8
        

        public static string Export(CheckLog log, CheckContext ctx, string dwgPath, out string note)
        {
            note = null;
            string path = AskPath(dwgPath);
            if (path == null) { note = "Выгрузка отменена."; return null; }

            string info = $"SDK_CHECK_PROJECT — проверка проекта.  Чертёж: {dwgPath}.  Дата: {DateTime.Now:dd.MM.yyyy HH:mm}.  " +
                          $"Фатально {log.Count(CheckLevel.Fatal)}, ошибок {log.Count(CheckLevel.Error)}, замечаний {log.Count(CheckLevel.Notice)}.";
            object[,] res = ResultTable(log);
            object[,] dev = DeviceTable(log, ctx);
            object[,] cat = CatalogTable(log);

            try { WriteExcel(path, info, res, dev, cat); return path; }
            catch (System.Exception ex)
            {
                string csv = Path.ChangeExtension(path, ".csv");
                try { WriteCsv(csv, info, res, dev, cat); }
                catch (Exception ex2) { note = "Не удалось создать ни Excel, ни CSV: " + ex.Message + " / " + ex2.Message; return null; }
                note = "Excel недоступен (" + ex.Message + ") — записан CSV.";
                return csv;
            }
        }

        // ------------------------------------------------------------------ таблицы
        static object[,] ResultTable(CheckLog log)
        {
            var items = log.Sorted.ToList();
            var t = new object[items.Count + 1, ResHead.Length];
            for (int c = 0; c < ResHead.Length; c++) t[0, c] = ResHead[c];
            for (int i = 0; i < items.Count; i++)
            {
                var x = items[i]; var k = x.Code; int r = i + 1;
                t[r, 0] = r; t[r, 1] = k.Id; t[r, 2] = k.LevelText; t[r, 3] = k.Title; t[r, 4] = x.Panel;
                t[r, 5] = x.Obj; t[r, 6] = x.Handle; t[r, 7] = x.Details; t[r, 8] = k.Action; t[r, 9] = k.Skip;
            }
            return t;
        }

        static object[,] DeviceTable(CheckLog log, CheckContext ctx)
        {
            var devs = ctx == null ? new System.Collections.Generic.List<KnxDevice>() : ctx.Devices;
            var t = new object[devs.Count + 1, DevHead.Length];
            for (int c = 0; c < DevHead.Length; c++) t[0, c] = DevHead[c];
            for (int i = 0; i < devs.Count; i++)
            {
                var d = devs[i]; int r = i + 1;
                t[r, 0] = PanelLabel(d.Scope);
                t[r, 1] = d.Label;
                t[r, 2] = d.Kind;
                t[r, 3] = d.Ob == null ? "" : Val(d.Ob, Names.NATR_TIP_MARKA_SP);
                t[r, 4] = d.AddrText;
                t[r, 5] = d.Addr != null ? d.Addr.LineKey : "";
                t[r, 6] = $"{d.Outputs.Count} / {d.UsedOutputs}";
                t[r, 7] = $"{d.Inputs.Count} / {d.UsedInputs}";
                t[r, 8] = d.Dali.Count;
                t[r, 9] = PowerText(d, ctx);
                t[r, 10] = log.CountForHandles(d.Handles);
                t[r, 11] = d.Container.Handle;
            }
            return t;
        }

        static string KindText(string kind)
        {
            switch (kind)
            {
                case Names.DK_RELAY: return "релейный модуль";
                case Names.DK_SHUTTER: return "шторный модуль";
                case Names.DK_DALI: return "шлюз DALI";
                case Names.DK_INPUT: return "модуль входов";
                case Names.DK_PSU: return "блок питания";
                default: return "";
            }
        }

        static string PowerText(KnxDevice d, CheckContext ctx)
        {
            if (d.Blok == null) return "— (нет " + Names.NB_BLOK_KNX + ")";
            if (None(Val(d.Blok, Names.NATR_SHIT_PIT_AVT))) return "НЕТ (от шины)";
            var ls = ctx.LinksOf(d.Blok);
            if (ls.Count == 0) return "нет группы " + Names.NGP_EL_KNX;
            return string.Join("; ", ls.Select(l => l.GroupName + " / " + (l.Breaker != null ? Q(l.Breaker) : $"автоматов {l.Breakers.Count}")));
        }

        static object[,] CatalogTable(CheckLog log)
        {
            var all = CheckCatalog.All;
            var t = new object[all.Length + 1 + CheckCatalog.NoneRules.Length + 2, CatHead.Length];
            for (int c = 0; c < CatHead.Length; c++) t[0, c] = CatHead[c];
            for (int i = 0; i < all.Length; i++)
            {
                var k = all[i]; int r = i + 1;
                t[r, 0] = k.Id; t[r, 1] = k.LevelText; t[r, 2] = k.Title; t[r, 3] = k.Source;
                t[r, 4] = k.What; t[r, 5] = k.Action; t[r, 6] = k.Skip; t[r, 7] = log.CountOf(k);
            }
            int row = all.Length + 2;
            t[row, 0] = "Флаг «НЕТ»"; t[row, 1] = "правила";
            for (int i = 0; i < CheckCatalog.NoneRules.Length; i++) t[row + 1 + i, 4] = CheckCatalog.NoneRules[i];
            return t;
        }

        // ------------------------------------------------------------------ диалог
        static string AskPath(string dwgPath)
        {
            string dir = "", baseName = "";
            try
            {
                dir = Path.GetDirectoryName(dwgPath) ?? "";
                baseName = Path.GetFileNameWithoutExtension(dwgPath);
            }
            catch (System.Exception) { }

            if (dir.Length == 0 || !Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(baseName))
                baseName = "Чертеж";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "SDK_CHECK_PROJECT — сохранить отчёт проверки проекта",
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = dir,
                FileName = $"{baseName}_CHECK_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }

        // ------------------------------------------------------------------ Excel через COM
        static void WriteExcel(string path, string info, object[,] res, object[,] dev, object[,] cat)
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
                while (wb.Worksheets.Count < 3) wb.Worksheets.Add(Type.Missing, wb.Worksheets[wb.Worksheets.Count]);

                dynamic ws1 = wb.Worksheets[1]; ws1.Name = "Результат";
                dynamic ws2 = wb.Worksheets[2]; ws2.Name = "Устройства";
                dynamic ws3 = wb.Worksheets[3]; ws3.Name = "Справочник проверок";

                Fill(ws1, info, res, new[] { 7 }, new[] { 8, 9, 10 });
                Fill(ws2, "Устройства KNX по контейнерам. Колонка «Замечаний» — сколько строк листа «Результат» относятся к контейнеру и его блокам.", dev, new[] { 5, 12 }, new[] { 10 });
                Fill(ws3, "Все проверки SDK_CHECK_PROJECT. «Найдено» — сколько раз сработала в этом запуске. Ниже — правила флага «НЕТ».", cat, null, new[] { 5, 6, 7 });

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

        /// info — в A1; таблица с заголовком — с 3-й строки.
        /// textCols — колонки «как текст» (Handle вида 2E1 и адрес 1.1.11 иначе станут числом/датой).
        /// wideCols — широкие колонки с переносом строк.
        static void Fill(dynamic ws, string info, object[,] table, int[] textCols, int[] wideCols)
        {
            int rows = table.GetLength(0), cols = table.GetLength(1);

            ws.Cells[1, 1].Value2 = info;
            ws.Cells[1, 1].Font.Bold = true;

            if (textCols != null)
                foreach (int c in textCols) ws.Columns[c].NumberFormat = "@";

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

            // закрепить заголовок, чтобы при прокрутке было видно, что в колонках
            ws.Activate();
            ws.Application.ActiveWindow.SplitRow = 3;
            ws.Application.ActiveWindow.SplitColumn = 0;
            ws.Application.ActiveWindow.FreezePanes = true;
        }

        // ------------------------------------------------------------------ CSV (запасной вариант, если Excel нет)
        static void WriteCsv(string path, string info, object[,] res, object[,] dev, object[,] cat)
        {
            var sb = new StringBuilder();
            sb.AppendLine(info);
            sb.AppendLine();
            sb.AppendLine("РЕЗУЛЬТАТ");
            Append(sb, res);
            sb.AppendLine();
            sb.AppendLine("УСТРОЙСТВА");
            Append(sb, dev);
            sb.AppendLine();
            sb.AppendLine("СПРАВОЧНИК ПРОВЕРОК");
            Append(sb, cat);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));   // BOM — чтобы Excel открыл кириллицу
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
