using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 3. Для автомата со связью: МОЩНОСТЬ = Σ МОЩНОСТЬ устройств группы, КОСИНУС — средневзвешенный.
    /// Затем ТОК = P/(U·cosφ) (1ф или 3ф по ФАЗНОСТЬ) у всех автоматов с мощностью; проверка против УСТАВКА.
    /// Резервы пропускаются.
    ///   МОЩНОСТЬ / УСТАВКА не число         → E01;
    ///   нет атрибута для записи             → E02;
    ///   I > УСТАВКА                          → E03;
    ///   МОЩНОСТЬ пуста/0, ΣP группы < минимума → N02.
    public static class Step3_Loads
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 3, " + panel;

            foreach (var av in s.Panel.Avtomats)
            {
                string q = Q(av, tr);
                if (CalcUtil.IsReserve(av, tr)) { r.Info(W, q + ": резерв — пропущен", av.Handle); continue; }

                // ---- 3а: нагрузка по связи ----
                var link = ctx.LinkOf(s, av);
                if (link != null)
                {
                    double sumP = 0, sumS = 0; int n = 0;
                    foreach (var d in link.Devices)
                    {
                        string txt = CalcUtil.Get(d, Names.NATR_MOSHNOST, tr);
                        if (txt.Length == 0) continue;                       // устройство без мощности (вход, датчик) — норма
                        if (!CalcUtil.Num(txt, out double p))
                        {
                            diag.Add(DiagCatalog.E01, panel, d.Handle, $"{d.BlockName}: {Names.NATR_MOSHNOST} «{txt}» не число — принято 0");
                            continue;
                        }
                        if (p <= 0) continue;
                        double cos = CalcUtil.Cos(d, tr);
                        sumP += p; sumS += p / cos; n++;
                    }
                    double cosW = sumS > 0 ? sumP / sumS : 1.0;

                    if (sumP < Names.CALC_MIN_POWER_KW)
                    {
                        diag.Add(DiagCatalog.N02, panel, av.Handle,
                            $"{q}: группа «{link.GroupName}» ΣP={CalcUtil.F(sumP, "0.###")} кВт < {Names.CALC_MIN_POWER_KW} — принято {Names.CALC_MIN_POWER_KW}");
                        sumP = Names.CALC_MIN_POWER_KW;
                    }

                    diag.Set(av, Names.NATR_MOSHNOST, CalcUtil.F(sumP, "0.###"), tr, panel);
                    diag.Set(av, Names.NATR_KOSINUS, CalcUtil.F(cosW, "0.00"), tr, panel);
                    r.Info(W, $"{q}: группа «{link.GroupName}», устройств с мощностью {n}, P={CalcUtil.F(sumP, "0.###")} cosφ={CalcUtil.F(cosW, "0.00")}", av.Handle);
                }

                // ---- 3б: ток ----
                string pTxt = CalcUtil.Get(av, Names.NATR_MOSHNOST, tr);
                if (pTxt.Length == 0)
                {
                    diag.Add(DiagCatalog.N02, panel, av.Handle, $"{q}: {Names.NATR_MOSHNOST} пуста — ток не считается");
                    continue;
                }
                if (!CalcUtil.Num(pTxt, out double P))
                {
                    diag.Add(DiagCatalog.E01, panel, av.Handle, $"{q}: {Names.NATR_MOSHNOST} «{pTxt}» не число");
                    continue;
                }
                if (P <= 0)
                {
                    diag.Add(DiagCatalog.N02, panel, av.Handle, $"{q}: {Names.NATR_MOSHNOST} = 0 — ток не считается");
                    continue;
                }

                double c = CalcUtil.Cos(av, tr);
                bool tp = CalcUtil.ThreePhase(CalcUtil.Get(av, Names.NATR_FAZNOST, tr));
                double I = CalcUtil.Current(P, c, tp);
                diag.Set(av, Names.NATR_TOK, CalcUtil.F(I, "0.0"), tr, panel);

                // ---- 3в: уставка ----
                string uTxt = CalcUtil.Get(av, Names.NATR_USTAVKA, tr);
                bool hasUst = uTxt.Length > 0;
                double ust = 0;
                if (hasUst && !CalcUtil.Num(uTxt, out ust))
                    diag.Add(DiagCatalog.E01, panel, av.Handle, $"{q}: {Names.NATR_USTAVKA} «{uTxt}» не число — перегрузка не проверена");
                else if (hasUst && ust > 0 && I > ust)
                    diag.Add(DiagCatalog.E03, panel, av.Handle,
                        $"{q}: I={CalcUtil.F(I, "0.0")} А > уставка {CalcUtil.F(ust, "0.#")} А ({(tp ? "3ф" : "1ф")}, P={CalcUtil.F(P, "0.###")} кВт, cosφ={CalcUtil.F(c, "0.00")})");

                r.Info(W, $"{q}: I={CalcUtil.F(I, "0.0")} А ({(tp ? "3ф" : "1ф")})", av.Handle);
            }
        }

        /// «Q3» или имя блока, если номера ещё нет.
        static string Q(BlockEntity av, Transaction tr)
        {
            string n = CalcUtil.Get(av, Names.NATR_NOMER_AVT, tr);
            return n.Length > 0 ? n : av.BlockName;
        }
    }
}
