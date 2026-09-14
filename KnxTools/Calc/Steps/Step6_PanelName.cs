using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 6. НАЗВАНИЕ_ПО_СХЕМЕ у SDK_ОБ_ЩИТ — главное; раздаётся во все блоки рамки с НАЗВАНИЕ_ЩИТА_ПО_СХЕМЕ
    /// (в том числе SDK_СХЕМА_ЩИТ). Если у SDK_ОБ_ЩИТ пусто — берётся первое непустое из блоков и пишется в него.
    ///   имя щита не определено нигде → E04.
    public static class Step6_PanelName
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 6, " + panel;
            var shit = s.Panel.ShitBlocks.FirstOrDefault();
            var targets = CalcUtil.AllBlocks(s).Where(b => CalcUtil.Has(b, Names.NATR_NAZVANIE_SHITA_PO_SHEME, tr)).ToList();

            string name = shit != null ? CalcUtil.Get(shit, Names.NATR_NAZVANIE_PO_SHEME, tr) : "";

            if (name.Length == 0)
            {
                foreach (var t in targets)
                {
                    string n = CalcUtil.Get(t, Names.NATR_NAZVANIE_SHITA_PO_SHEME, tr);
                    if (n.Length == 0) continue;
                    name = n;
                    if (shit != null)
                    {
                        CalcUtil.Set(shit, Names.NATR_NAZVANIE_PO_SHEME, name, tr);
                        r.Info(W, $"{Names.NB_OB_SHIT}: {Names.NATR_NAZVANIE_PO_SHEME} было пусто, взято «{name}» из {t.BlockName} <{t.Handle}>", shit.Handle);
                    }
                    break;
                }
            }

            if (name.Length == 0)
            {
                diag.Add(DiagCatalog.E04, panel, s.Handle,
                    $"имя щита не определено: {Names.NB_OB_SHIT} " + (shit == null ? "отсутствует" : $"с пустым {Names.NATR_NAZVANIE_PO_SHEME}") +
                    $", ни в одном блоке нет {Names.NATR_NAZVANIE_SHITA_PO_SHEME}");
                return;
            }

            int changed = 0;
            foreach (var t in targets)
            {
                if (CalcUtil.Get(t, Names.NATR_NAZVANIE_SHITA_PO_SHEME, tr) == name) continue;
                CalcUtil.Set(t, Names.NATR_NAZVANIE_SHITA_PO_SHEME, name, tr);   // атрибут есть — отобрано по Has
                changed++;
                r.Info(W, $"{t.BlockName}: {Names.NATR_NAZVANIE_SHITA_PO_SHEME} → «{name}»", t.Handle);
            }
            r.Info(W, $"щит «{name}»: блоков с именем щита {targets.Count}, исправлено {changed}", s.Handle);
        }
    }
}