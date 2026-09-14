using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 1. У всех блоков рамки с атрибутом КОСИНУС: пусто / не число / вне (0; 1] → значение по умолчанию
    /// (то, что вернёт CalcUtil.Cos). Автоматы-резервы не трогаются.
    ///   заменён косинус → N01.
    public static class Step1_Cos
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 1, " + panel;
            int fixedCount = 0, total = 0;

            foreach (var b in CalcUtil.AllBlocks(s))
            {
                if (!CalcUtil.Has(b, Names.NATR_KOSINUS, tr)) continue;
                if (s.Panel.Avtomats.Any(a => a.Id == b.Id && CalcUtil.IsReserve(a, tr))) continue;
                total++;

                string txt = CalcUtil.Get(b, Names.NATR_KOSINUS, tr);
                if (CalcUtil.Num(txt, out double c) && c > 0 && c <= 1) continue;

                string val = CalcUtil.F(CalcUtil.Cos(b, tr), "0.00");
                CalcUtil.Set(b, Names.NATR_KOSINUS, val, tr);      // атрибут точно есть — проверено Has
                fixedCount++;
                diag.Add(DiagCatalog.N01, panel, b.Handle,
                    $"{b.BlockName}: {Names.NATR_KOSINUS} «{(txt.Length == 0 ? "пусто" : txt)}» → {val}");
            }

            r.Info(W, $"блоков с {Names.NATR_KOSINUS}: {total}, заменено на значение по умолчанию: {fixedCount}", s.Handle);
        }
    }
}