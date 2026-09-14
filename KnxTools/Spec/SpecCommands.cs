using System;
using System.IO;
using Autodesk.AutoCAD.Runtime;
using KnxTools.Model;
using KnxTools.Reading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Spec
{
    public class SpecCommands
    {
        [CommandMethod("SDK_SPEC")]
        public void Spec() => SpecRunner.Run();
    }

    /// Чтение чертежа → сборка строк → сводка в командную строку → окно «Сохранить как» → .xlsx → открыть.
    public static class SpecRunner
    {
        public static void Run()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;

            DrawingModel model;
            try { model = DrawingReader.Read(doc.Database, new Report()); }
            catch (System.Exception ex) { ed.WriteMessage("\nСпецификация: чертёж не прочитан — " + ex.GetType().Name + ": " + ex.Message + "\n"); return; }

            if (model.Scopes.Count == 0)
            { ed.WriteMessage($"\nСпецификация: в слое {Names.NL_KONT_PANEL} нет рамок щитов — считать нечего.\n"); return; }

            SpecResult res;
            try { res = SpecBuilder.Build(model); }
            catch (System.Exception ex) { ed.WriteMessage("\nСпецификация: сбой сборки — " + ex.GetType().Name + ": " + ex.Message + "\n"); return; }

            ed.WriteMessage($"\n===== Спецификация: щитов {res.Panels}, позиций оборудования {res.Items}, кабелей {res.Cables}, труб {res.Pipes} =====\n");
            foreach (var n in res.Notes) ed.WriteMessage("  " + n + "\n");

            if (res.Total == 0)
            {
                ed.WriteMessage($"Внутри рамок нет ни одного блока с заполненным {SpecAccess.A_NAME} или {SpecAccess.A_PROVOD}/{SpecAccess.A_TRUBA} + {SpecAccess.A_DLINA} — файл не создаётся.\n");
                return;
            }

            string path = AskPath(doc.Name);
            if (path == null) { ed.WriteMessage("Отменено.\n"); return; }

            try { SpecExcel.Write(path, res.Rows); }
            catch (System.Exception ex) { ed.WriteMessage("\nФайл не записан: " + ex.Message + " (возможно, открыт в Excel).\n"); return; }

            ed.WriteMessage("Файл создан: " + path + "\n");
            try { System.Diagnostics.Process.Start(path); } catch { }
        }

        // ------------------------------------------------------------------ диалог сохранения (WPF, без Windows.Forms)
        static string AskPath(string dwgName)
        {
            string dir = "", name = "";
            try
            {
                dir = Path.GetDirectoryName(dwgName) ?? "";
                name = Path.GetFileNameWithoutExtension(dwgName);
            }
            catch (System.Exception) { }

            if (dir.Length == 0 || !Directory.Exists(dir))
                dir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (string.IsNullOrEmpty(name))
                name = "Спецификация";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Спецификация — сохранить как",
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = dir,
                FileName = name + Names.SPEC_FILE_SUFFIX + ".xlsx"
            };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }
}