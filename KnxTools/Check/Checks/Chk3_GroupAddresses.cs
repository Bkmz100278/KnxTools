using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE03 формат / DPT / имя (для каждого ГА списка), PE05 один ГА — разные DPT, PN04 ГА без отправителя.
    /// ГРУППОВОЙ_АДРЕС и DPT — списки через «;», сопоставляются по позиции. Один DPT на все ГА допускается.
    public static class Chk3_GroupAddresses
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            var listeners = new Dictionary<string, List<BlockEntity>>();
            var dptByGa = new Dictionary<string, Dictionary<string, BlockEntity>>();   // ГА → DPT → первый блок

            foreach (var b in ctx.Outputs.Cast<BlockEntity>().Concat(ctx.Dali))
            {
                string gaRaw = Val(b, Names.NATR_GRUPPOVOY_ADRES);
                if (!Filled(gaRaw)) continue;                                            // пусто/НЕТ — дело PE04/PN01
                var scope = ctx.ScopeOf(b);
                var gas = SplitList(gaRaw);

                if (Empty(Val(b, Names.NATR_NAZVANIE_PRIEMNIKA)))
                    log.Add(CheckCatalog.PE03, scope, b, $"ГА «{gaRaw}»: {Names.NATR_NAZVANIE_PRIEMNIKA} (имя ГА) пусто");

                // DPT: список параллельно ГА
                string dptRaw = Val(b, Names.NATR_DPT);
                string[] dpts;
                if (!Filled(dptRaw))
                {
                    dpts = new string[0];
                    log.Add(CheckCatalog.PE03, scope, b, $"ГА «{gaRaw}»: {Names.NATR_DPT} не задан" + (gas.Length > 1 ? $" (ожидается {gas.Length} значений через «;»)" : ""));
                }
                else
                {
                    dpts = SplitList(dptRaw);
                    if (dpts.Length != gas.Length && dpts.Length != 1)
                        log.Add(CheckCatalog.PE03, scope, b,
                            $"ГА {gas.Length} шт. «{gaRaw}», DPT {dpts.Length} шт. «{dptRaw}» — число не совпадает; сопоставлены по порядку, лишние/недостающие пропущены");
                }

                var seenInBlock = new HashSet<string>();
                for (int i = 0; i < gas.Length; i++)
                {
                    string pos = gas.Length > 1 ? $" (№{i + 1} из {gas.Length})" : "";
                    if (!ParseGa(gas[i], out var g, out string err))
                    {
                        log.Add(CheckCatalog.PE03, scope, b, $"{Names.NATR_GRUPPOVOY_ADRES} «{gas[i]}»{pos}: {err}");
                        continue;
                    }
                    if (!seenInBlock.Add(g.Key))
                    {
                        log.Add(CheckCatalog.PE03, scope, b, $"ГА {g.Key}{pos} повторяется внутри одного блока");
                        continue;
                    }

                    if (!listeners.TryGetValue(g.Key, out var l)) listeners[g.Key] = l = new List<BlockEntity>();
                    l.Add(b);

                    string dpt = dpts.Length == 1 ? dpts[0] : (i < dpts.Length ? dpts[i] : null);
                    if (dpt != null && Filled(dpt))
                    {
                        if (!dptByGa.TryGetValue(g.Key, out var dd)) dptByGa[g.Key] = dd = new Dictionary<string, BlockEntity>();
                        string k = DptKey(dpt);
                        if (!dd.ContainsKey(k)) dd[k] = b;
                    }
                }
            }

            // входы: ГА_УПРАВЛЕНИЯ и статусный ГА — тоже списки, проверяем только формат
            foreach (var i in ctx.Inputs)
                foreach (var tag in new[] { Names.NATR_GA_UPRAVLENIYA, Names.NATR_GRUPPOVOY_ADRES })
                {
                    string raw = Val(i, tag);
                    if (!Filled(raw)) continue;
                    var parts = SplitList(raw);
                    for (int k = 0; k < parts.Length; k++)
                        if (!ParseGa(parts[k], out _, out string err))
                            log.Add(CheckCatalog.PE03, ctx.ScopeOf(i), i, $"{tag} «{parts[k]}»" + (parts.Length > 1 ? $" (№{k + 1} из {parts.Length})" : "") + $": {err}");
                }

            // PE05: один ГА — разные DPT (между блоками или внутри списка одного блока)
            foreach (var kv in dptByGa.Where(kv => kv.Value.Count > 1))
            {
                string list = string.Join(", ", kv.Value.Select(x => $"{x.Key} <{x.Value.Handle}>"));
                foreach (var b in kv.Value.Values.Distinct())
                    log.Add(CheckCatalog.PE05, ctx.ScopeOf(b), b, $"ГА {kv.Key}: DPT {list}");
            }

            // PN04: ГА без отправителя
            foreach (var kv in listeners.Where(kv => !ctx.SenderKeys.Contains(kv.Key)))
            {
                var b = kv.Value[0];
                log.Add(CheckCatalog.PN04, ctx.ScopeOf(b), b, $"на ГА {kv.Key} не пишет ни один вход ({Names.NATR_GA_UPRAVLENIYA}); получателей {kv.Value.Count}");
            }
        }
    }
}
