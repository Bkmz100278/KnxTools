using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE11 — в одном контейнере не повторяются номера выходов (между собой), входов (между собой) и групп DALI;
    /// у задействованного канала номер обязан быть указан.
    public static class Chk8_Numbering
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            foreach (var d in ctx.Devices)
            {
                CheckSet(d, log, d.Outputs.Cast<BlockEntity>(), Names.NATR_NOMER_KANALA, "выход", Names.NATR_GRUPPOVOY_ADRES);
                CheckSet(d, log, d.Inputs.Cast<BlockEntity>(), Names.NATR_NOMER_KANALA, "вход", Names.NATR_GA_UPRAVLENIYA);
                CheckSet(d, log, d.Dali.Cast<BlockEntity>(), Names.NATR_NOMER_GRUPPY_DALI, "группа DALI", Names.NATR_GRUPPOVOY_ADRES);
            }
        }

        static void CheckSet(KnxDevice d, CheckLog log, IEnumerable<BlockEntity> blocks, string tag, string what, string usedTag)
        {
            var byNum = new Dictionary<string, List<BlockEntity>>();
            foreach (var b in blocks)
            {
                string n = Val(b, tag);
                if (Empty(n))
                {
                    string used = Val(b, usedTag);
                    if (Filled(used))
                        log.Add(CheckCatalog.PE11, d.Scope, b, $"{what} <{b.Handle}>: {tag} пуст, а канал задействован (ГА {used})");
                    continue;
                }
                if (None(n)) continue;
                string key = NumKey(n);
                if (!byNum.TryGetValue(key, out var l)) byNum[key] = l = new List<BlockEntity>();
                l.Add(b);
            }

            foreach (var kv in byNum.Where(kv => kv.Value.Count > 1))
                foreach (var b in kv.Value)
                    log.Add(CheckCatalog.PE11, d.Scope, b,
                        $"{what} «{kv.Key}» повторяется в контейнере {d.Label}: " +
                        string.Join(", ", kv.Value.Select(x => "<" + x.Handle + ">")));
        }

        /// «03» и «3» — один номер; нечисловой номер сравнивается как текст без учёта регистра.
        static string NumKey(string s) => Int(s, out int v) ? v.ToString() : s.Trim().ToUpperInvariant();
    }
}
