using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE07 — адреса балластов, номера групп, принадлежность контейнеру. Шлюз DALI = контейнер, в котором есть SDK_АДРЕСА_DALI.
    /// Один балласт в нескольких группах одного шлюза — допустимо (DALI разрешает до 16 групп). Ошибка только при повторе внутри одного списка.
    public static class Chk6_Dali
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            foreach (var d in ctx.Devices)
            {
                if (!d.IsDaliGateway) continue;
                foreach (var g in d.Dali)
                {
                    string who = $"группа DALI «{Val(g, Names.NATR_NAZVANIE_PRIEMNIKA)}»";

                    string grp = Val(g, Names.NATR_NOMER_GRUPPY_DALI);
                    if (Filled(grp) && (!Int(grp, out int gn) || gn < 0 || gn > Names.CHECK_DALI_GROUP_MAX))
                        log.Add(CheckCatalog.PE07, d.Scope, g, $"{who}: {Names.NATR_NOMER_GRUPPY_DALI} «{grp}» не число 0–{Names.CHECK_DALI_GROUP_MAX}");

                    string addrs = Val(g, Names.NATR_ADRESA_DALI);
                    if (None(addrs)) continue;                                               // группа-заготовка без балластов
                    if (Empty(addrs)) { log.Add(CheckCatalog.PE07, d.Scope, g, $"{who}: {Names.NATR_ADRESA_DALI} пусты — состав группы неизвестен"); continue; }

                    var list = new List<int>();
                    if (!ParseDali(addrs, list, out string err)) { log.Add(CheckCatalog.PE07, d.Scope, g, $"{who}: {Names.NATR_ADRESA_DALI} «{addrs}»: {err}"); continue; }

                    foreach (int a in list)
                        if (a < 0 || a > Names.CHECK_DALI_ADDR_MAX)
                            log.Add(CheckCatalog.PE07, d.Scope, g, $"{who}: адрес балласта {a} вне 0–{Names.CHECK_DALI_ADDR_MAX}");

                    var dup = list.GroupBy(a => a).Where(x => x.Count() > 1).Select(x => x.Key).OrderBy(x => x).ToList();
                    if (dup.Count > 0)
                        log.Add(CheckCatalog.PE07, d.Scope, g, $"{who}: адрес повторяется внутри списка «{addrs}»: {string.Join(", ", dup)}");
                }
            }

            foreach (var b in ctx.LooseChannels)
                if (b is AdresaDaliBlock)
                {
                    var scope = ctx.ScopeOf(b);
                    log.Add(CheckCatalog.PE07, scope, b, $"группа DALI «{Val(b, Names.NATR_NAZVANIE_PRIEMNIKA)}» вне контейнера шлюза" + (scope == null ? " и вне рамок щитов" : ""));
                }
        }
    }
}