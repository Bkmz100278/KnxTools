using KnxTools.Model;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE04 нагрузка без канала, PE09 нагрузка без помещения/мощности, PN01 свободные (нарисованные, но пустые) каналы.
    public static class Chk4_Channels
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            foreach (var d in ctx.Devices)
            {
                foreach (var o in d.Outputs)
                {
                    string ga = Val(o, Names.NATR_GRUPPOVOY_ADRES);
                    if (None(ga)) continue;                                                  // намеренно не используется
                    string name = Val(o, Names.NATR_NAZVANIE_PRIEMNIKA);
                    bool hasLoad = Filled(name) || Filled(Val(o, Names.NATR_MOSHNOST)) || Filled(Val(o, Names.NATR_NOMER_KOMNATY));
                    if (Empty(ga))
                    {
                        if (hasLoad) log.Add(CheckCatalog.PE04, d.Scope, o, $"{Ch(o)}: нагрузка «{name}» без {Names.NATR_GRUPPOVOY_ADRES}");
                        else log.Add(CheckCatalog.PN01, d.Scope, o, $"{Ch(o)}: нарисован, но не задействован (ГА пуст)");
                        continue;
                    }
                    if (Empty(Val(o, Names.NATR_NOMER_KOMNATY)))
                        log.Add(CheckCatalog.PE09, d.Scope, o, $"{Ch(o)}, ГА {ga}, «{name}»: {Names.NATR_NOMER_KOMNATY} пуст");
                    string p = Val(o, Names.NATR_MOSHNOST);
                    if (Empty(p)) log.Add(CheckCatalog.PE09, d.Scope, o, $"{Ch(o)}, ГА {ga}, «{name}»: {Names.NATR_MOSHNOST} пуста");
                    else if (Filled(p) && (!Num(p, out double pv) || pv < 0)) log.Add(CheckCatalog.PE09, d.Scope, o, $"{Ch(o)}: {Names.NATR_MOSHNOST} «{p}» не число");
                }

                foreach (var g in d.Dali)
                {
                    string ga = Val(g, Names.NATR_GRUPPOVOY_ADRES);
                    if (None(ga)) continue;
                    string name = Val(g, Names.NATR_NAZVANIE_PRIEMNIKA);
                    if (Empty(ga)) { log.Add(CheckCatalog.PE04, d.Scope, g, $"группа DALI «{name}»: {Names.NATR_GRUPPOVOY_ADRES} пуст"); continue; }
                    if (Empty(Val(g, Names.NATR_NOMER_KOMNATY)))
                        log.Add(CheckCatalog.PE09, d.Scope, g, $"группа DALI «{name}», ГА {ga}: {Names.NATR_NOMER_KOMNATY} пуст");
                }
            }

            foreach (var b in ctx.LooseChannels)
            {
                if (b is AdresaDaliBlock) continue;                                          // это PE07
                var scope = ctx.ScopeOf(b);
                log.Add(CheckCatalog.PE04, scope, b, $"{Ch(b)} вне {Names.NB_KONTEYNER_KNX} — не принадлежит устройству" + (scope == null ? " и лежит вне рамок щитов" : ""));
            }
        }
    }
}