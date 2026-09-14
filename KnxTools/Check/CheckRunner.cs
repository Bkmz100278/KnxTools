using System;
using System.IO;
using System.Linq;
using KnxTools.Check.Checks;
using KnxTools.Model;
using KnxTools.Reading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Check
{
    /// Результат одного прогона проверок — то, что окно показывает и выгружает в Excel.
    public class CheckResult
    {
        public CheckLog Log = new CheckLog();
        public CheckContext Ctx;      // null — до проверок не дошло (PF01/PF02)
        public string DwgName;        // имя файла
        public string DwgPath;        // полный путь — для имени Excel-файла
        public string Stats = "";     // «щитов 3, устройств KNX 41, …»
        public string Error;          // null — прогон завершён
        public DateTime When;
    }

    /// Чтение чертежа → контекст → Chk2…Chk8. Ничего не печатает и не меняет — всё возвращает в CheckResult.
    /// Вызывается окном CheckWindow при открытии и по «Обновить».
    public static class CheckRunner
    {
        public const string CMD = "SDK_CHECK_PROJECT";

        public static CheckResult Collect()
        {
            var res = new CheckResult { When = DateTime.Now };
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) { res.Error = "Нет активного чертежа."; return res; }

            res.DwgPath = doc.Name;
            res.DwgName = Path.GetFileName(doc.Name);
            var db = doc.Database;
            var log = res.Log;
            var report = new Report();

            // ---- чтение чертежа ----
            DrawingModel model;
            try { model = DrawingReader.Read(db, report); }
            catch (Exception ex)
            {
                log.Add(CheckCatalog.PF02, "", "чтение чертежа", "", $"{ex.GetType().Name}: {ex.Message}");
                return res;
            }

            if (model.Scopes.Count == 0)
            {
                log.Add(CheckCatalog.PF01, "", "", "", $"в слое {Names.NL_KONT_PANEL} нет ни одной рамки щита");
                return res;
            }
            if (model.Scopes.All(s => s.Containers.Count == 0))
            {
                log.Add(CheckCatalog.PF01, "", "", "", $"ни в одной рамке нет {Names.NB_KONTEYNER_KNX} — проверять нечего");
                return res;
            }

            // ---- контекст: устройства + группы AutoCAD (транзакция только здесь; LockDocument обязателен — зовём из немодального окна) ----
            CheckContext ctx;
            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    ctx = CheckContext.Build(db, tr, model);
                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                log.Add(CheckCatalog.PF02, "", "сбор контекста", "", $"{ex.GetType().Name}: {ex.Message}");
                return res;
            }
            res.Ctx = ctx;
            res.Stats = $"щитов {model.Scopes.Count}, устройств KNX {ctx.Devices.Count}, автоматов {ctx.Breakers.Count}, " +
                        $"групп {Names.NGP_EL_KNX}* — {ctx.GroupsOurs} (всего групп в чертеже {ctx.GroupsTotal})";

            // ---- проверки: сбой одной группы не останавливает остальные ----
            Safe(log, "физические адреса", () => Chk2_PhysAddresses.Run(ctx, log));
            Safe(log, "групповые адреса", () => Chk3_GroupAddresses.Run(ctx, log));
            Safe(log, "каналы", () => Chk4_Channels.Run(ctx, log));
            Safe(log, "питание и кабели", () => Chk5_Power.Run(ctx, log));
            Safe(log, "DALI", () => Chk6_Dali.Run(ctx, log));
            Safe(log, "входы", () => Chk7_Inputs.Run(ctx, log));
            Safe(log, "нумерация каналов", () => Chk8_Numbering.Run(ctx, log));

            return res;
        }

        /// SDK_CHECK_HELP: справочник проверок и правила флага «НЕТ» — в командную строку.
        public static void PrintCatalog()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            doc.Editor.WriteMessage("{0}", "\n" + CheckCatalog.AsText());
        }

        static void Safe(CheckLog log, string what, Action run)
        {
            try { run(); }
            catch (Exception ex)
            {
                log.Add(CheckCatalog.PF02, "", "проверка: " + what, "", $"{ex.GetType().Name}: {ex.Message} — группа проверок пропущена");
            }
        }
    }
}