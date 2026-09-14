using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 4. Итоги щита → SDK_СХЕМА_ЩИТ:
    ///   УСТАНОВЛЕННАЯ_МОЩНОСТЬ (Py) = Σ МОЩНОСТЬ всех автоматов рамки,
    ///   КОСИНУС — средневзвешенный по автоматам,
    ///   КОЭФ_СПРОСА (Kc) — из блока; не число / ≤0 / >1 → CALC_KC_DEFAULT (записывается обратно),
    ///   РАСЧЕТНАЯ_МОЩНОСТЬ (Pp) = Py · Kc,
    ///   ТОК (Ip) = Pp·1000 / (U·cosφ) — щит принимается однофазным, U = CALC_U_PHASE.
    ///   нет SDK_СХЕМА_ЩИТ                 → E04;
    ///   МОЩНОСТЬ автомата / Кс не число    → E01;
    ///   нет атрибута для записи           → E02;
    ///   ни у одного автомата нет мощности → N02.
    public static class Step4_Panel
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 4, " + panel;

            if (s.Panel.ShemaBlocks.Count == 0)
            {
                diag.Add(DiagCatalog.E04, panel, s.Handle, $"нет блока {Names.NB_SHEMA_SHIT} — итоги щита не записаны");
                return;
            }

            // сумма мощностей и средневзвешенный косинус по всем автоматам с мощностью
            double py = 0, sumS = 0; int n = 0;
            foreach (var av in s.Panel.Avtomats)
            {
                string txt = CalcUtil.Get(av, Names.NATR_MOSHNOST, tr);
                if (txt.Length == 0) continue;
                if (!CalcUtil.Num(txt, out double p))
                {
                    string q = CalcUtil.Get(av, Names.NATR_NOMER_AVT, tr);
                    if (q.Length == 0) q = av.BlockName;
                    diag.Add(DiagCatalog.E01, panel, av.Handle, $"{q}: {Names.NATR_MOSHNOST} «{txt}» не число");   // тот же текст, что в шаге 3 → не дублируется
                    continue;
                }
                if (p <= 0) continue;
                double cos = CalcUtil.Cos(av, tr);
                py += p; sumS += p / cos; n++;
            }
            if (n == 0)
                diag.Add(DiagCatalog.N02, panel, s.Handle, $"ни у одного автомата нет {Names.NATR_MOSHNOST} — итоги щита нулевые");

            double cosAvg = sumS > 0 ? py / sumS : 1.0;

            foreach (var sh in s.Panel.ShemaBlocks)
            {
                double kc = Names.CALC_KC_DEFAULT;
                string kcStr = CalcUtil.Get(sh, Names.NATR_KOEF_SPROSA, tr);
                if (kcStr.Length == 0)
                    r.Info(W, $"{Names.NATR_KOEF_SPROSA} пуст — принят {CalcUtil.F(kc, "0.00")}", sh.Handle);
                else if (!CalcUtil.Num(kcStr, out double k))
                    diag.Add(DiagCatalog.E01, panel, sh.Handle, $"{Names.NATR_KOEF_SPROSA} «{kcStr}» не число — принят {CalcUtil.F(kc, "0.00")}");
                else if (k <= 0 || k > 1)
                    diag.Add(DiagCatalog.E01, panel, sh.Handle, $"{Names.NATR_KOEF_SPROSA} = {kcStr} вне (0; 1] — принят {CalcUtil.F(kc, "0.00")}");
                else
                    kc = k;

                double pp = py * kc;
                double ip = pp * 1000.0 / (Names.CALC_U_PHASE * cosAvg);

                string sPy = CalcUtil.F(py, "0.###");
                string sKc = CalcUtil.F(kc, "0.00");
                string sPp = CalcUtil.F(pp, "0.###");
                string sCos = CalcUtil.F(cosAvg, "0.00");
                string sIp = CalcUtil.F(ip, "0.0");

                diag.Set(sh, Names.NATR_USTANOVLENNAYA_MOSHNOST, sPy, tr, panel);
                diag.Set(sh, Names.NATR_KOEF_SPROSA, sKc, tr, panel);
                diag.Set(sh, Names.NATR_RASCHETNAYA_MOSHNOST, sPp, tr, panel);
                diag.Set(sh, Names.NATR_KOSINUS, sCos, tr, panel);
                diag.Set(sh, Names.NATR_TOK, sIp, tr, panel);

                r.Info(W, $"Py={sPy} Kc={sKc} Pp={sPp} cosφ={sCos} Ip={sIp} А (1ф), автоматов в сумме {n}", sh.Handle);
            }

            if (s.Panel.ShemaBlocks.Count > 1)
                r.Warn(W, $"блоков {Names.NB_SHEMA_SHIT}: {s.Panel.ShemaBlocks.Count} — записано во все", s.Handle);
        }
    }
}