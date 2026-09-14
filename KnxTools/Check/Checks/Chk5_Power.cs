using System.Linq;
using KnxTools.Model;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE06 цепь питания (группы EL_KNX), PE10 кабели, PN03 автомат против мощности.
    public static class Chk5_Power
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            // 1. состав групп
            foreach (var l in ctx.Links)
            {
                var any = l.Breakers.Count > 0 ? (BlockEntity)l.Breakers[0] : l.Devices.FirstOrDefault();
                string panel = PanelLabel(ctx.ScopeOf(any));
                if (l.Breakers.Count == 0)
                    log.Add(CheckCatalog.PE06, panel, "группа " + l.GroupName, "", $"в группе нет {Names.NB_AVTOMAT_UNIV} (потребителей {l.Devices.Count}) — питание не определено");
                else if (l.Breakers.Count > 1)
                    log.Add(CheckCatalog.PE06, panel, "группа " + l.GroupName, "", $"в группе {l.Breakers.Count} автомата: {string.Join(", ", l.Breakers.Select(Q))} — допустим один");
            }

            // 2. автомат в нескольких группах
            foreach (var a in ctx.Breakers)
            {
                var ls = ctx.LinksOf(a);
                if (ls.Count > 1) log.Add(CheckCatalog.PE06, ctx.ScopeOf(a), a, $"{Q(a)} состоит в {ls.Count} группах: {string.Join(", ", ls.Select(x => x.GroupName))}");
            }

            // 3. потребители без питания
            foreach (var b in ctx.PowerConsumers)
            {
                if (None(Val(b, Names.NATR_SHIT_PIT_AVT))) continue;                          // 230 В не нужно — осознанно
                if ((b is LiniyaKnxVyhodBlock || b is AdresaDaliBlock) && !Filled(Val(b, Names.NATR_GRUPPOVOY_ADRES))) continue;   // канал не задействован
                var ls = ctx.LinksOf(b);
                if (ls.Count == 0)
                    log.Add(CheckCatalog.PE06, ctx.ScopeOf(b), b, $"не входит ни в одну группу {Names.NGP_EL_KNX}* — автомат, кабель и щит питания не определены");
                else if (ls.Count > 1)
                    log.Add(CheckCatalog.PE06, ctx.ScopeOf(b), b, $"входит в {ls.Count} группы ({string.Join(", ", ls.Select(x => x.GroupName))}) — питание неоднозначно");
            }

            // 4. кабели
            foreach (var a in ctx.Breakers)
                if (!IsReserve(a) && Empty(Val(a, Names.NATR_PROVOD)))
                    log.Add(CheckCatalog.PE10, ctx.ScopeOf(a), a, $"{Q(a)}: {Names.NATR_PROVOD} пуст — марка/сечение отходящего кабеля не заданы");
            foreach (var o in ctx.Outputs)
                if (Filled(Val(o, Names.NATR_GRUPPOVOY_ADRES)) && Empty(Val(o, Names.NATR_PROVOD)))
                    log.Add(CheckCatalog.PE10, ctx.ScopeOf(o), o, $"{Ch(o)}, ГА {Val(o, Names.NATR_GRUPPOVOY_ADRES)}: {Names.NATR_PROVOD} пуст — кабель до нагрузки не задан");
            foreach (var i in ctx.Inputs)
                if (Filled(Val(i, Names.NATR_GA_UPRAVLENIYA)) && Empty(Val(i, Names.NATR_PROVOD)))
                    log.Add(CheckCatalog.PE10, ctx.ScopeOf(i), i, $"{Ch(i)}: {Names.NATR_PROVOD} пуст — кабель до устройства управления не задан");

            // 5. автомат против мощности (по данным SDK_CALC_PANEL)
            foreach (var a in ctx.Breakers)
            {
                if (IsReserve(a)) continue;
                if (!Num(Val(a, Names.NATR_MOSHNOST), out double P) || P <= 0) continue;
                if (!Num(Val(a, Names.NATR_USTAVKA), out double ust) || ust <= 0) continue;
                double cos = Cos(a); bool tp = ThreePhase(a); double I = Current(P, cos, tp);
                string ph = tp ? "3ф" : "1ф";
                if (I > ust)
                    log.Add(CheckCatalog.PN03, ctx.ScopeOf(a), a, $"{Q(a)}: I={F(I)} А > уставка {F(ust, "0.#")} А (P={F(P, "0.###")} кВт, cosφ={F(cos, "0.00")}, {ph})");
                else if (I >= Names.CHECK_OVERSIZE_MIN_I && ust > Names.CHECK_OVERSIZE_FACTOR * I)
                    log.Add(CheckCatalog.PN03, ctx.ScopeOf(a), a, $"{Q(a)}: уставка {F(ust, "0.#")} А более чем в {F(Names.CHECK_OVERSIZE_FACTOR, "0.#")} раза выше расчётного тока {F(I)} А ({ph})");
            }
        }
    }
}
