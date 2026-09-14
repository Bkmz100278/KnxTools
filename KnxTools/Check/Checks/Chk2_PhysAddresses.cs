using System.Collections.Generic;
using System.Linq;
using static KnxTools.Check.CheckUtil;

namespace KnxTools.Check.Checks
{
    /// PE01 дубликаты, PE02 нет/неверен адрес, PN02 линия (число устройств, блоки питания).
    /// Устройство с каналами обязано иметь адрес О.Л.У. Устройство без каналов: НЕТ, О.Л (= блок питания линии) или О.Л.У.
    public static class Chk2_PhysAddresses
    {
        public static void Run(CheckContext ctx, CheckLog log)
        {
            var byAddr = new Dictionary<string, List<KnxDevice>>();
            var devicesOnLine = new Dictionary<string, List<KnxDevice>>();

            foreach (var d in ctx.Devices)
            {
                if (d.Blok == null) { log.Add(CheckCatalog.PE02, d, $"в контейнере нет {Names.NB_BLOK_KNX} — физический адрес не задан"); continue; }
                string txt = d.AddrText;
                if (Empty(txt)) { log.Add(CheckCatalog.PE02, d, $"{Names.NATR_FIZICHESKIY_ADRES} пуст"); continue; }
                if (None(txt))
                {
                    if (d.HasChannels) log.Add(CheckCatalog.PE02, d, $"{Names.NATR_FIZICHESKIY_ADRES} = «{txt}», но в контейнере есть каналы ({d.Kind}) — шинный адрес обязателен");
                    continue;
                }
                if (!ParsePhys(txt, out var a, out string err)) { log.Add(CheckCatalog.PE02, d, $"{Names.NATR_FIZICHESKIY_ADRES} «{txt}»: {err}"); continue; }

                if (a.HasDevice) { Get(byAddr, a.Key).Add(d); Get(devicesOnLine, a.LineKey).Add(d); }
                else if (d.HasChannels) log.Add(CheckCatalog.PE02, d, $"{Names.NATR_FIZICHESKIY_ADRES} «{txt}»: номер устройства не указан, а в контейнере есть каналы ({d.Kind}) — нужен адрес вида {txt}.N");
                // устройство без каналов с адресом «О.Л» — допустимо, молча
            }

            foreach (var kv in byAddr.Where(kv => kv.Value.Count > 1))
                foreach (var d in kv.Value)
                    log.Add(CheckCatalog.PE01, d, $"адрес {kv.Key} также у: " +
                        string.Join(", ", kv.Value.Where(x => x != d).Select(x => x.Label)));

            foreach (var kv in devicesOnLine.Where(kv => kv.Value.Count > Names.CHECK_DEVICES_PER_LINE_MAX))
                log.Add(CheckCatalog.PN02, PanelLabel(kv.Value[0].Scope), "линия " + kv.Key, "",
                    $"адресовано устройств: {kv.Value.Count}, допустимо {Names.CHECK_DEVICES_PER_LINE_MAX}");
        }

        private static List<KnxDevice> Get(Dictionary<string, List<KnxDevice>> d, string k)
        {
            if (!d.TryGetValue(k, out var l)) d[k] = l = new List<KnxDevice>();
            return l;
        }
    }
}
