using System;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Model;
using KnxTools.Reading;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace KnxTools.Check
{
    /// SDK_CHECK_GROUPS: состав каждой группы EL_KNX* глазами проверки + для каждого SDK_АДРЕСА_DALI —
    /// в каких группах он состоит по реакторам самого объекта. Ничего не пишет.
    public static class CheckDebug
    {
        public static void Run()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            var ed = doc.Editor; var db = doc.Database;
            var model = DrawingReader.Read(db, new Report());

            using (var tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var ctx = CheckContext.Build(db, tr, model);
                var byId = ctx.AllBlocks.ToDictionary(b => b.Id);
                ed.WriteMessage($"\nГрупп в чертеже {ctx.GroupsTotal}, с префиксом {Names.NGP_EL_KNX}: {ctx.GroupsOurs}\n");

                var gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
                foreach (DBDictionaryEntry e in gd)
                {
                    var g = tr.GetObject(e.Value, OpenMode.ForRead) as Group;
                    if (g == null || !g.Name.StartsWith(Names.NGP_EL_KNX, StringComparison.OrdinalIgnoreCase)) continue;
                    ed.WriteMessage($"\n--- {g.Name}\n");
                    foreach (var id in g.GetAllEntityIds())
                    {
                        string name = "", note;
                        if (byId.TryGetValue(id, out var b))
                        {
                            name = b.BlockName;
                            note = b is AvtomatUnivBlock ? "автомат"
                                   : b is ShemaShitBlock ? "SDK_СХЕМА_ЩИТ → вне проверки питания"
                                   : b.Has(Names.NATR_SHIT_PIT_AVT) ? "потребитель"
                                   : "в модели, но без " + Names.NATR_SHIT_PIT_AVT + " → ИГНОР";
                        }
                        else
                        {
                            note = "НЕТ В МОДЕЛИ → ИГНОР";
                            if (tr.GetObject(id, OpenMode.ForRead) is BlockReference br)
                                name = br.IsDynamicBlock ? ((BlockTableRecord)tr.GetObject(br.DynamicBlockTableRecord, OpenMode.ForRead)).Name : br.Name;
                        }
                        ed.WriteMessage($"   <{id.Handle}> {id.ObjectClass.DxfName} {name} — {note}\n");
                    }
                }

                ed.WriteMessage($"\n--- {Names.NB_ADRESA_DALI} в модели, группы по реакторам объекта:\n");
                foreach (var d in ctx.Dali.Cast<BlockEntity>().Concat(ctx.LooseChannels.Where(b => b is AdresaDaliBlock)))
                {
                    var ent = tr.GetObject(d.Id, OpenMode.ForRead) as Entity;
                    var groups = (ent?.GetPersistentReactorIds() ?? new ObjectIdCollection()).Cast<ObjectId>()
                        .Select(rid => tr.GetObject(rid, OpenMode.ForRead) as Group).Where(x => x != null).Select(x => x.Name).ToList();
                    ed.WriteMessage($"   <{d.Handle}> «{d.GetAny(Names.NATR_NAZVANIE_PRIEMNIKA)}» Has({Names.NATR_SHIT_PIT_AVT})={d.Has(Names.NATR_SHIT_PIT_AVT)} " +
                                    $"группы: {(groups.Count == 0 ? "НЕТ" : string.Join(", ", groups))}\n");
                }
            }
        }
    }
}