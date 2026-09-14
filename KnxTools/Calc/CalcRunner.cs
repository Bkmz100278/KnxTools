using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using KnxTools.Calc.Diag;
using KnxTools.Calc.Steps;
using KnxTools.Model;
using KnxTools.Reading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Calc
{
    /// Точка входа: выбор щита → одно чтение (DrawingReader) → связи → шаги → журнал → проверки → Excel.
    /// Report — подробный рабочий журнал (что и куда записано). DiagLog — только срабатывания проверок из DiagCatalog.
    public static class CalcRunner
    {
        public static void Run(string title, params int[] steps)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor;
            var db = doc.Database;
            var report = new Report();
            var diag = new DiagLog();

            if (!PickPanel(ed, out ObjectId onlyPoly)) return;   // Esc

            DrawingModel model;
            try { model = DrawingReader.Read(db, report); }
            catch (System.Exception ex) { Out(ed, $"\n{title}: ошибка чтения чертежа: {ex.Message}\n"); return; }

            if (model.Scopes.Count == 0)
            {
                diag.Add(DiagCatalog.F01, "", "", $"слой {Names.NL_KONT_PANEL}: полилиний нет");
                PrintDiag(ed, title, diag);
                return;
            }

            var selected = onlyPoly.IsNull ? model.Scopes.ToList() : model.Scopes.Where(s => s.PolylineId == onlyPoly).ToList();
            if (selected.Count == 0) { Out(ed, $"\n{title}: указанная полилиния не распознана как рамка щита.\n"); return; }

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ctx = LinkReader.Build(db, tr, model, report);

                    foreach (var s in Order(selected, ctx))
                    {
                        string panel = CalcUtil.Label(s, tr);
                        report.Info("щит", $"===== {panel}: автоматов {s.Panel.Avtomats.Count}, контейнеров {s.Containers.Count}, " +
                                           $"схем {s.Panel.ShemaBlocks.Count}, связей {ctx.LinksOf(s).Count}", s.Handle);

                        if (s.Panel.Avtomats.Count == 0)
                        {
                            diag.Add(DiagCatalog.F02, panel, s.Handle, "в рамке нет ни одного SDK_АВТОМАТ_УНИВ — шаги для щита пропущены");
                            continue;
                        }

                        foreach (int n in steps)
                        {
                            try { RunStep(n, s, ctx, tr, report, diag); }
                            catch (System.Exception ex)
                            {
                                diag.Add(DiagCatalog.F03, panel, s.Handle, $"шаг {n}: {ex.GetType().Name}: {ex.Message}");
                                report.Error("сбой", $"шаг {n}, {panel}: {ex.Message} — шаг пропущен", s.Handle);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                report.Error("сбой", ex.Message + " — изменения отменены");
                diag.Add(DiagCatalog.F03, "", "", "команда: " + ex.Message + " — все изменения отменены");
            }

            Print(ed, title, report);
            PrintDiag(ed, title, diag);

            if (diag.IsEmpty) return;
            if (!AskExcel(ed)) return;

            string path = DiagExcel.Export(diag, title, doc.Name, out string note);
            if (note != null) Out(ed, note + "\n");
            if (path != null)
            {
                Out(ed, "Файл создан: " + path + "\n");
                try { System.Diagnostics.Process.Start(path); } catch { }
            }
        }

        static void RunStep(int n, PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog d)
        {
            switch (n)
            {
                case 1: Step1_Cos.Run(s, ctx, tr, r, d); break;
                case 2: Step2_Numbering.Run(s, ctx, tr, r, d); break;
                case 3: Step3_Loads.Run(s, ctx, tr, r, d); break;
                case 4: Step4_Panel.Run(s, ctx, tr, r, d); break;
                case 5: Step5_Channels.Run(s, ctx, tr, r, d); break;
                case 6: Step6_PanelName.Run(s, ctx, tr, r, d); break;
                case 7: Step7_Propagate.Run(s, ctx, tr, r, d); break;
                case 8: Step8_Channels.Run(s, ctx, tr, r, d); break;
            }
        }

        /// Питаемые щиты раньше питающих: если автомат щита A группирован с SDK_СХЕМА_ЩИТ щита B — B считается до A.
        static List<PanelScope> Order(List<PanelScope> scopes, CalcContext ctx)
        {
            var deps = scopes.ToDictionary(s => s, s => new HashSet<PanelScope>());
            foreach (var a in scopes)
                foreach (var l in ctx.LinksOf(a))
                    foreach (var d in l.Devices.OfType<ShemaShitBlock>())
                    {
                        var b = ctx.ScopeOf(d);
                        if (b != null && b != a && deps.ContainsKey(b)) deps[a].Add(b);
                    }

            var result = new List<PanelScope>();
            var left = new List<PanelScope>(scopes);
            while (left.Count > 0)
            {
                var ready = left.Where(s => deps[s].All(result.Contains)).ToList();
                if (ready.Count == 0) ready.Add(left[0]);   // цикл питания — берём как есть
                foreach (var s in ready) { result.Add(s); left.Remove(s); }
            }
            return result;
        }

        /// true — продолжать (onlyPoly.IsNull = все щиты); false — Esc.
        static bool PickPanel(Editor ed, out ObjectId onlyPoly)
        {
            onlyPoly = ObjectId.Null;
            while (true)
            {
                var opt = new PromptEntityOptions($"\nУкажите полилинию щита (слой {Names.NL_KONT_PANEL}) или Enter — все щиты: ") { AllowNone = true };
                var res = ed.GetEntity(opt);
                if (res.Status == PromptStatus.None) return true;
                if (res.Status != PromptStatus.OK) return false;

                using (var tr = ed.Document.Database.TransactionManager.StartOpenCloseTransaction())
                {
                    var ent = tr.GetObject(res.ObjectId, OpenMode.ForRead) as Entity;
                    bool isPoly = ent is Polyline || ent is Polyline2d || ent is Polyline3d;
                    if (isPoly && CalcUtil.Is(ent.Layer, Names.NL_KONT_PANEL)) { onlyPoly = res.ObjectId; return true; }
                }
                Out(ed, $"\nЭто не полилиния в слое {Names.NL_KONT_PANEL}. Повторите.");
            }
        }

        /// Да/Нет, Enter = Да.
        static bool AskExcel(Editor ed)
        {
            var opt = new PromptKeywordOptions("\nВывести список проверок в Excel?");
            opt.Keywords.Add("Yes", "Да", "Да");
            opt.Keywords.Add("No", "Нет", "Нет");
            opt.Keywords.Default = "Yes";
            opt.AllowNone = true;
            var res = ed.GetKeywords(opt);
            if (res.Status == PromptStatus.None) return true;
            return res.Status == PromptStatus.OK && res.StringResult == "Yes";
        }

        // ------------------------------------------------------------------ вывод
        static void Out(Editor ed, string text) => ed.WriteMessage("{0}", text);

        /// Рабочий журнал: что записано, что пропущено.
        static void Print(Editor ed, string title, Report report)
        {
            Out(ed, $"\n===== {title}: журнал — ошибок {report.Errors}, предупреждений {report.Warnings} =====\n");
            foreach (var i in report.Issues)
            {
                bool calc = i.Where.StartsWith("шаг") || i.Where == "щит" || i.Where == "связи" || i.Where == "сбой";
                if (i.Severity == Severity.Info && !calc) continue;   // заметки чтения чертежа — в KNXREAD
                Out(ed, i + "\n");
            }
        }

        /// Итог проверок по каталогу: сначала сводка по уровням, затем по кодам.
        static void PrintDiag(Editor ed, string title, DiagLog diag)
        {
            Out(ed, $"\n===== {title}: ПРОВЕРКИ — фатально {diag.Count(DiagLevel.Fatal)}, " +
                    $"ошибок {diag.Count(DiagLevel.Error)}, замечаний {diag.Count(DiagLevel.Notice)} =====\n");
            if (diag.IsEmpty) { Out(ed, "Все проверки пройдены.\n"); return; }

            foreach (var g in diag.Sorted.GroupBy(i => i.Code))
            {
                Out(ed, $"{g.Key.Id} {g.Key.Title} — {g.Key.LevelText} — {g.Count()} шт.\n");
                foreach (var i in g)
                    Out(ed, $"     {i.Panel} | {i.Details}" + (i.Handle.Length == 0 ? "" : $" | <{i.Handle}>") + "\n");
            }
            Out(ed, "Справочник всех проверок: SDK_CALC_HELP\n");
        }
    }
}
