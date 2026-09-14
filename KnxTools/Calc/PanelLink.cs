using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Model;

namespace KnxTools.Calc
{
    /// Связь = группа AutoCAD: ровно один SDK_АВТОМАТ_УНИВ + блоки с атрибутом ЩИТ_ПИТАЮЩЕГО_АВТОМАТА.
    public class PanelLink
    {
        public ObjectId GroupId;
        public string GroupName = "";
        public AvtomatUnivBlock Breaker;
        public List<BlockEntity> Devices = new List<BlockEntity>();
    }

    /// Всё, что нужно шагам: модель, связи по щитам, принадлежность блока щиту.
    public class CalcContext
    {
        public DrawingModel Model;
        public Dictionary<ObjectId, BlockEntity> BlockById = new Dictionary<ObjectId, BlockEntity>();
        public Dictionary<ObjectId, PanelScope> ScopeOfBlock = new Dictionary<ObjectId, PanelScope>();
        public Dictionary<PanelScope, List<PanelLink>> Links = new Dictionary<PanelScope, List<PanelLink>>();
        public KnxTools.Calc.Diag.DiagLog Diag = new KnxTools.Calc.Diag.DiagLog();

        public List<PanelLink> LinksOf(PanelScope s) =>
            Links.TryGetValue(s, out var l) ? l : new List<PanelLink>();

        public PanelLink LinkOf(PanelScope s, BlockEntity breaker) =>
            LinksOf(s).FirstOrDefault(l => l.Breaker.Id == breaker.Id);

        public PanelScope ScopeOf(BlockEntity b) =>
            b != null && ScopeOfBlock.TryGetValue(b.Id, out var s) ? s : null;
    }

    public static class LinkReader
    {
        const string W = "связи";

        public static CalcContext Build(Database db, Transaction tr, DrawingModel model, Report report)
        {
            var ctx = new CalcContext { Model = model };

            foreach (var s in model.Scopes)
            {
                ctx.Links[s] = new List<PanelLink>();
                foreach (var b in CalcUtil.AllBlocks(s))
                {
                    ctx.BlockById[b.Id] = b;
                    ctx.ScopeOfBlock[b.Id] = s;
                }
            }
            foreach (var b in model.OutsideBlocks) ctx.BlockById[b.Id] = b;

            var breakerGroup = new Dictionary<ObjectId, string>();
            var gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);

            foreach (DBDictionaryEntry e in gd)
            {
                var g = tr.GetObject(e.Value, OpenMode.ForRead) as Group;
                if (g == null) continue;

                var breakers = new List<AvtomatUnivBlock>();
                var devices = new List<BlockEntity>();
                foreach (ObjectId id in g.GetAllEntityIds())
                {
                    if (id.IsErased || !ctx.BlockById.TryGetValue(id, out var b)) continue;   // не наш блок — игнор
                    if (b is AvtomatUnivBlock av) { breakers.Add(av); continue; }
                    if (b is ShemaShitBlock) continue;                                         // схема щита нагрузкой не бывает
                    if (CalcUtil.Has(b, Names.NATR_SHIT_PIT_AVT, tr)) devices.Add(b);
                }

                if (breakers.Count == 0) continue;   // группа не про питание
                if (breakers.Count > 1)
                {
                    report.Error(W, $"группа «{g.Name}»: автоматов {breakers.Count}, допустим один — группа пропущена",
                        string.Join(",", breakers.Select(x => x.Handle)));
                    continue;
                }

                var brk = breakers[0];
                if (breakerGroup.TryGetValue(brk.Id, out var other))
                {
                    report.Error(W, $"автомат входит в две группы: «{other}» и «{g.Name}» — вторая пропущена", brk.Handle);
                    continue;
                }
                breakerGroup[brk.Id] = g.Name;

                var scope = ctx.ScopeOf(brk);
                if (scope == null)
                {
                    report.Warn(W, $"группа «{g.Name}»: автомат вне рамок щита — связь не учтена", brk.Handle);
                    continue;
                }

                foreach (var d in devices)
                    if (ctx.ScopeOf(d) != scope)
                        report.Warn(W, $"группа «{g.Name}»: устройство {d.BlockName} вне рамки щита автомата", d.Handle);
                if (devices.Count == 0)
                    report.Warn(W, $"группа «{g.Name}»: у автомата нет устройств с атрибутом {Names.NATR_SHIT_PIT_AVT}", brk.Handle);

                ctx.Links[scope].Add(new PanelLink { GroupId = g.ObjectId, GroupName = g.Name, Breaker = brk, Devices = devices });
            }

            report.Info(W, "связей (групп с автоматом): " + ctx.Links.Sum(k => k.Value.Count));
            return ctx;
        }
    }
}
