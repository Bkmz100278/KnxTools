using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 2. Все SDK_АВТОМАТ_УНИВ рамки нумеруются слева направо (при равном X — сверху вниз): Q1, Q2, …
    ///   у автомата нет НОМЕР_АВТ → E02 (номер за ним резервируется, чтобы нумерация не «поехала»).
    public static class Step2_Numbering
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 2, " + panel;

            var ordered = s.Panel.Avtomats
                .OrderBy(b => b.Position.X)
                .ThenByDescending(b => b.Position.Y)
                .ToList();

            int n = 0, changed = 0;
            foreach (var b in ordered)
            {
                n++;
                string num = Names.NP_NOMER_AVT + n;
                string old = CalcUtil.Get(b, Names.NATR_NOMER_AVT, tr);

                if (!diag.Set(b, Names.NATR_NOMER_AVT, num, tr, panel)) continue;

                if (old != num)
                {
                    changed++;
                    r.Info(W, $"автомат «{(old.Length == 0 ? "пусто" : old)}» → {num}", b.Handle);
                }
            }

            r.Info(W, $"пронумеровано автоматов: {n}, изменено номеров: {changed}", s.Handle);
        }
    }
}