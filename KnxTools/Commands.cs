using System;
using System.Diagnostics;
using System.IO;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using KnxTools.Export;
using KnxTools.Model;
using KnxTools.Reading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using KnxTools.Json;
using Autodesk.AutoCAD.Geometry;
using KnxTools.Calc.Diag;
using System.Globalization;

namespace KnxTools
{
    public class Commands
    {
        [CommandMethod("KNXHELLO")]
        public void Hello()
        {
            AcApp.DocumentManager.MdiActiveDocument?.Editor.WriteMessage("\nKnxTools загружен.\n");
        }

        /// Считать базу с чертежа и выгрузить в Excel.
        [CommandMethod("KNXREAD")]
        public void ReadToExcel()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var report = new Report();
            DrawingModel model;

            // 1. Чтение
            try
            {
                model = DrawingReader.Read(doc.Database, report);
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nKNXREAD: критическая ошибка чтения: {0}\n{1}\n", ex.Message, ex.StackTrace);
                return;
            }

            ed.WriteMessage("\nKNXREAD: рамок {0}, блоков {1}; ошибок {2}, предупреждений {3}\n",
                model.Scopes.Count, model.TotalBlocks, report.Errors, report.Warnings);

            // 2. Нечего выгружать
            if (!model.HasData)
            {
                string why = model.Scopes.Count == 0
                    ? $"Не найдено ни одной полилинии в слое {Names.NL_KONT_PANEL}."
                    : "Рамки найдены, но внутри них нет ни одного блока SDK_*.";
                if (model.OutsideBlocks.Count > 0)
                    why += $"\nПри этом {model.OutsideBlocks.Count} наших блоков лежат вне рамок.";
                foreach (var i in report.Issues) ed.WriteMessage(i + "\n");
                AcApp.ShowAlertDialog("KNXREAD: выгрузка не выполнена.\n\n" + why + "\n\nПодробности — в командной строке.");
                return;
            }

            // 3. Диалог сохранения
            string dwgDir = "";
            try { dwgDir = Path.GetDirectoryName(doc.Name); } catch { }
            string baseName = "";
            try { baseName = Path.GetFileNameWithoutExtension(doc.Name); } catch { }
            if (string.IsNullOrEmpty(baseName)) baseName = "Drawing";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить выгрузку KNX",
                Filter = "Книга Excel (*.xlsx)|*.xlsx",
                DefaultExt = ".xlsx",
                FileName = $"{baseName}_KNX_{DateTime.Now:yyyyMMdd_HHmm}.xlsx",
                InitialDirectory = !string.IsNullOrEmpty(dwgDir) && Directory.Exists(dwgDir)
                    ? dwgDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) { ed.WriteMessage("\nKNXREAD: отменено пользователем.\n"); return; }

            // 4. Экспорт
            try
            {
                ModelExporter.Export(model, dlg.FileName);
            }
            catch (IOException ex)
            {
                ed.WriteMessage("\nKNXREAD: не удалось записать файл (открыт в Excel?): {0}\n", ex.Message);
                AcApp.ShowAlertDialog("Файл не записан. Закройте его в Excel и повторите.\n" + ex.Message);
                return;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\nKNXREAD: ошибка экспорта: {0}\n{1}\n", ex.Message, ex.StackTrace);
                return;
            }

            ed.WriteMessage("\nKNXREAD: записано {0}\nОтчёт: {1}\n", dlg.FileName, Path.ChangeExtension(dlg.FileName, null) + "_report.txt");
            int shown = 0;
            foreach (var i in report.Issues)
            {
                if (i.Severity == Severity.Info) continue;
                ed.WriteMessage(i + "\n");
                if (++shown >= 30) { ed.WriteMessage("... остальное — в листе «Ошибки».\n"); break; }
            }

            try { Process.Start(dlg.FileName); } catch { /* Excel не установлен — файл лежит на диске */ }
        }

        /// Диагностика: кликнуть по объекту — увидеть класс, слой, имя блока, атрибуты.
        [CommandMethod("KNXWHAT")]
        public void What()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var res = ed.GetEntity("\nУкажите объект: ");
            if (res.Status != PromptStatus.OK) return;

            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var ent = (Entity)tr.GetObject(res.ObjectId, OpenMode.ForRead);
                ed.WriteMessage("\nКласс .NET: {0}\nКласс DXF: {1}\nСлой: «{2}»\nHandle: {3}\n",
                    ent.GetType().Name, res.ObjectId.ObjectClass.DxfName, ent.Layer, ent.Handle);
                var owner = tr.GetObject(ent.OwnerId, OpenMode.ForRead) as BlockTableRecord;
                if (owner != null) ed.WriteMessage("Лежит в пространстве/блоке: {0}\n", owner.Name);

                if (ent is BlockReference br)
                {
                    var btrId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
                    var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);
                    ed.WriteMessage("Имя блока: «{0}»  (динамический: {1})\nТочка вставки: {2:F1}; {3:F1}\nАтрибуты:\n",
                        btr.Name, br.IsDynamicBlock, br.Position.X, br.Position.Y);
                    foreach (ObjectId aid in br.AttributeCollection)
                    {
                        var ar = tr.GetObject(aid, OpenMode.ForRead) as AttributeReference;
                        if (ar != null) ed.WriteMessage("   {0} = «{1}»\n", ar.Tag, ar.TextString);
                    }
                    var e = BlockFactory.Create(btr.Name);
                    ed.WriteMessage("Распознан как: {0}\n", e == null ? "— (не наш блок)" : e.GetType().Name);
                }
                tr.Commit();
            }
        }


        [CommandMethod("KNXJSON")]
        public void ExportJson()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var report = new Report();
            DrawingModel model;

            try { model = DrawingReader.Read(doc.Database, report); }
            catch (System.Exception ex) { ed.WriteMessage("\nKNXJSON: ошибка чтения: {0}\n{1}\n", ex.Message, ex.StackTrace); return; }

            if (!model.HasData)
            {
                ed.WriteMessage("\nKNXJSON: нет данных — рамок {0}, блоков вне рамок {1}. Выгрузка не выполнена.\n",
                    model.Scopes.Count, model.OutsideBlocks.Count);
                foreach (var i in report.Issues) ed.WriteMessage(i + "\n");
                return;
            }

            KnxExport export;
            try { export = JsonBuilder.Build(model, Path.GetFileName(doc.Name)); }
            catch (System.Exception ex) { ed.WriteMessage("\nKNXJSON: ошибка сборки модели: {0}\n{1}\n", ex.Message, ex.StackTrace); return; }

            string dwgDir = ""; try { dwgDir = Path.GetDirectoryName(doc.Name); } catch { }
            string baseName = ""; try { baseName = Path.GetFileNameWithoutExtension(doc.Name); } catch { }
            if (string.IsNullOrEmpty(baseName)) baseName = "Drawing";

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Сохранить выгрузку KNX (JSON)",
                Filter = "JSON (*.json)|*.json",
                DefaultExt = ".json",
                FileName = $"{baseName}_KNX_{DateTime.Now:yyyyMMdd_HHmm}.json",
                InitialDirectory = Directory.Exists(dwgDir) ? dwgDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() != true) { ed.WriteMessage("\nKNXJSON: отменено.\n"); return; }

            string xlsx = Path.ChangeExtension(dlg.FileName, ".xlsx");
            try
            {
                File.WriteAllText(dlg.FileName, JsonWriter.Serialize(export), new System.Text.UTF8Encoding(false));
                ModelExporter.Export(model, xlsx);      // тихий отчёт: Топология + Ошибки (включая проверки JSON)
            }
            catch (IOException ex) { ed.WriteMessage("\nKNXJSON: файл не записан (открыт в другой программе?): {0}\n", ex.Message); return; }
            catch (System.Exception ex) { ed.WriteMessage("\nKNXJSON: ошибка записи: {0}\n{1}\n", ex.Message, ex.StackTrace); return; }

            ed.WriteMessage("\nKNXJSON: {0}\n  JSON:  {1}\n  Отчёт: {2}\n  Ошибок {3}, предупреждений {4}\n  Сериализатор: {5}\n",
                report.Errors > 0 ? "ЗАВЕРШЕНО С ОШИБКАМИ (validation.status = FAILED)" : "OK",
                dlg.FileName, xlsx, report.Errors, report.Warnings, JsonWriter.LibraryInfo());
        }


        //[CommandMethod("SDK_CALC_FIND")]
        //public void CalcFind()
        //{
        //    var doc = AcApp.DocumentManager.MdiActiveDocument;
        //    var ed = doc.Editor; var db = doc.Database;

        //    var pr = ed.GetString(new PromptStringOptions("\nHandle объекта из отчёта: ") { AllowSpaces = false });
        //    if (pr.Status != PromptStatus.OK) return;
        //    string s = pr.StringResult.Trim().Trim('[', ']');

        //    long h;
        //    if (!long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out h)) { ed.WriteMessage("\n«" + s + "» — это не handle."); return; }
        //    ObjectId id;
        //    if (!db.TryGetObjectId(new Handle(h), out id) || id.IsErased) { ed.WriteMessage("\nОбъект " + s + " не найден: удалён или отчёт от другого чертежа."); return; }

        //    Extents3d ext;
        //    using (var tr = db.TransactionManager.StartTransaction())
        //    {
        //        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
        //        if (ent == null) { ed.WriteMessage("\nОбъект " + s + " не графический."); return; }
        //        try { ext = ent.GeometricExtents; } catch { ed.WriteMessage("\nУ объекта " + s + " нет геометрии."); return; }
        //        var br = ent as BlockReference;
        //        ed.WriteMessage("\n" + (br != null ? DiagRef.Of(br, tr).Text : ent.GetType().Name + "  [" + s + "]"));
        //        tr.Commit();
        //    }

        //    double w = ext.MaxPoint.X - ext.MinPoint.X, hh = ext.MaxPoint.Y - ext.MinPoint.Y;
        //    double pad = Math.Max(Math.Max(w, hh) * 2.0, 20.0);          // объект ~ треть экрана, соседи видны
        //    using (var v = ed.GetCurrentView())
        //    {
        //        v.CenterPoint = new Point2d((ext.MinPoint.X + ext.MaxPoint.X) / 2, (ext.MinPoint.Y + ext.MaxPoint.Y) / 2);
        //        v.Width = w + pad; v.Height = hh + pad;
        //        ed.SetCurrentView(v);
        //    }
        //    ed.SetImpliedSelection(new[] { id });                          // объект остаётся выделенным — видны ручки
        //}

    }
}