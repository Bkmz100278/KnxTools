using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE08 — вход без описания действия (и PN01 для нарисованного пустого входа).
    public static class Chk7_Inputs
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            foreach (var i in ctx.Inputs)
            {
                var scope = ctx.ScopeOf(i);
                string gaU = Val(i, Names.NATR_GA_UPRAVLENIYA);
                string dev = Val(i, Names.NATR_USTROYSTVO_UPRAVLENIYA);
                if (None(gaU)) continue;                                                     // вход намеренно не используется

                if (Empty(gaU))
                {
                    if (Filled(dev)) log.Add(CheckCatalog.PE08, scope, i, $"{Ch(i)}: «{dev}» подключено, но {Names.NATR_GA_UPRAVLENIYA} пуст — неизвестно, чем управляет");
                    else log.Add(CheckCatalog.PN01, scope, i, $"{Ch(i)}: нарисован, но не задействован ({Names.NATR_GA_UPRAVLENIYA} пуст)");
                    continue;
                }
                if (Empty(dev))
                    log.Add(CheckCatalog.PE08, scope, i, $"{Ch(i)}, ГА {gaU}: {Names.NATR_USTROYSTVO_UPRAVLENIYA} пусто — неизвестно, что подключено ко входу");

                foreach (var part in SplitList(gaU))
                {
                    if (!ParseGa(part, out var g, out _)) continue;                          // формат — дело Chk3
                    if (!ctx.ListenerKeys.Contains(g.Key))
                        log.Add(CheckCatalog.PE08, scope, i, $"{Ch(i)}: {Names.NATR_GA_UPRAVLENIYA} {g.Key} не слушает ни один выход / группа DALI — что происходит при нажатии, не определить");
                }
            }
        }
    }
}