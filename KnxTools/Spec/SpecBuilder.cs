using System;
using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;
using static KnxTools.Spec.SpecAccess;

namespace KnxTools.Spec
{
    /// Собирает строки спецификации из модели чертежа. Только чтение.
    public static class SpecBuilder
    {
        sealed class Item
        {
            public string Name, Mark, Code, Maker;
            public double Qty;
            public double Ustavka = double.MaxValue;   // только для АВТ_ГР; MaxValue — не распознана, в конец
            public bool Psu;                           // «блок питания» в НАИМЕНОВАНИЕ_СП
            public string Key => SpecParse.Squash(Name + Mark + Code);
        }

        sealed class Panel
        {
            public string Title, Handle;
            public bool HasObShit;
            public readonly List<Item> Head = new List<Item>(), Vykl = new List<Item>(), Avt = new List<Item>(),
                                       Other = new List<Item>(), Knx = new List<Item>();
            public int Count => Head.Count + Vykl.Count + Avt.Count + Other.Count + Knx.Count;
        }

        sealed class Line
        {
            public string Display; public double Length; public int Blocks;
            public string Mark; public int Cores; public double Size; public bool Parsed;
        }

        static readonly StringComparer Alpha = StringComparer.CurrentCultureIgnoreCase;

        /// Длина в спецификацию идёт целыми метрами, половина — вверх.
        static double Meters(double len) => Math.Round(len, MidpointRounding.AwayFromZero);

        public static SpecResult Build(DrawingModel model)
        {
            var res = new SpecResult();
            res.Rows.Add(SpecRow.Header());

            // ---------------- I. Щитовое оборудование
            var panels = model.Scopes.Select(s => ReadPanel(s, res)).Where(p => p != null)
                                     .OrderBy(p => p.Title, Alpha).ToList();
            res.Panels = panels.Count;
            res.Rows.Add(SpecRow.Section(Names.SPEC_SECTION_1));
            int pi = 0;
            foreach (var p in panels) EmitPanel(res, p, ++pi);

            // ---------------- II. Кабели
            var cables = Collect(model, res, A_PROVOD, raw =>
            {
                var c = SpecParse.Cable(raw);
                return new Line { Display = c.Display, Mark = c.Mark, Cores = c.Cores, Size = c.Section, Parsed = c.Parsed };
            })
                .OrderBy(l => l.Mark, Alpha).ThenBy(l => l.Cores).ThenBy(l => l.Size).ThenBy(l => l.Display, Alpha).ToList();
            res.Cables = cables.Count;
            res.Rows.Add(SpecRow.Empty());
            res.Rows.Add(SpecRow.Section(Names.SPEC_SECTION_2));
            int k = 0;
            foreach (var l in cables)
                res.Rows.Add(SpecRow.Item("2." + (++k), l.Display, "", "", "", Names.SPEC_UNIT_M, Meters(l.Length),
                    l.Parsed ? "" : "марка/жилы/сечение не распознаны"));

            // ---------------- III. Трубы
            var pipes = Collect(model, res, A_TRUBA, raw =>
            {
                var t = SpecParse.Pipe(raw);
                return new Line { Display = t.Display, Mark = t.Mark, Size = t.Diameter, Parsed = t.Parsed };
            })
                .OrderBy(l => l.Mark, Alpha).ThenBy(l => l.Size).ThenBy(l => l.Display, Alpha).ToList();
            res.Pipes = pipes.Count;
            res.Rows.Add(SpecRow.Empty());
            res.Rows.Add(SpecRow.Section(Names.SPEC_SECTION_3));
            k = 0;
            foreach (var l in pipes)
                res.Rows.Add(SpecRow.Item("3." + (++k), l.Display, "", "", "", Names.SPEC_UNIT_M, Meters(l.Length),
                    l.Parsed ? "" : "марка/номинал не распознаны"));

            int outside = model.OutsideBlocks.Count(b => Filled(Val(b, A_NAME)) || (Filled(Val(b, A_PROVOD)) && Filled(Val(b, A_DLINA))));
            if (outside > 0) res.Notes.Add($"вне рамок щитов: {outside} блоков с {A_NAME} или {A_PROVOD}+{A_DLINA} — в спецификацию не входят");
            return res;
        }

        // ------------------------------------------------------------------ щит
        static Panel ReadPanel(PanelScope s, SpecResult res)
        {
            var p = new Panel { Handle = PanelHandle(s) };
            string panelName = null;

            foreach (var b in Blocks(s))
            {
                string bn = (BlockName(b) ?? "").ToUpperInvariant();
                if (bn == NB_OB_SHIT.ToUpperInvariant() && panelName == null)
                {
                    p.HasObShit = true;
                    panelName = SpecParse.Clean(Val(b, A_PANEL_NAME));
                }

                string name = Val(b, A_NAME);
                if (!Filled(name)) continue;
                var it = new Item
                {
                    Name = SpecParse.Clean(name),
                    Mark = SpecParse.Clean(Val(b, A_MARK)),
                    Code = SpecParse.Clean(Val(b, A_CODE)),
                    Maker = SpecParse.Clean(Val(b, A_MAKER)),
                    Qty = 1
                };

                if (bn == NB_OB_SHIT.ToUpperInvariant()) p.Head.Add(it);
                else if (bn.Contains(Names.SPEC_KEY_VYKL_NAGR.ToUpperInvariant())) p.Vykl.Add(it);
                else if (bn.Contains(Names.SPEC_KEY_AVT_GR.ToUpperInvariant()))
                {
                    if (SpecParse.FirstNumber(Val(b, A_USTAVKA), out double u)) it.Ustavka = u;
                    else res.Notes.Add($"{bn} <{Handle(b)}>: {A_USTAVKA} «{Val(b, A_USTAVKA)}» не число — поставлен в конец автоматов");
                    p.Avt.Add(it);
                }
                else if (bn.Contains(NB_OB_KNX.ToUpperInvariant()))
                {
                    it.Psu = SpecParse.Squash(it.Name).Contains(SpecParse.Squash(Names.SPEC_KEY_PSU));
                    p.Knx.Add(it);
                }
                else p.Other.Add(it);
            }

            if (p.Count == 0)
            {
                res.Notes.Add($"рамка <{p.Handle}>: ни одного блока с заполненным {A_NAME} — щит пропущен");
                return null;
            }
            if (!p.HasObShit) res.Notes.Add($"рамка <{p.Handle}>: нет блока {NB_OB_SHIT} — щит выведен без названия");
            p.Title = Names.SPEC_PANEL_PREFIX + (string.IsNullOrEmpty(panelName) ? "<" + p.Handle + ">" : panelName);
            return p;
        }

        static void EmitPanel(SpecResult res, Panel p, int index)
        {
            string prefix = "1." + index;
            int k = 0;
            Func<Item, SpecRow> row = it => SpecRow.Item(prefix + "." + (++k), it.Name, it.Mark, it.Code, it.Maker, Names.SPEC_UNIT_PCS, it.Qty);

            res.Rows.Add(SpecRow.Panel(prefix, p.Title));
            foreach (var it in Merge(p.Head)) { res.Rows.Add(row(it)); res.Items++; }

            var power = Merge(p.Vykl).OrderBy(i => i.Name, Alpha)
                .Concat(Merge(p.Avt).OrderBy(i => i.Ustavka).ThenBy(i => i.Name, Alpha))
                .Concat(Merge(p.Other).OrderBy(i => i.Name, Alpha)).ToList();
            if (power.Count > 0)
            {
                res.Rows.Add(SpecRow.Empty());
                res.Rows.Add(SpecRow.Sub(Names.SPEC_SUB_POWER));
                foreach (var it in power) { res.Rows.Add(row(it)); res.Items++; }
            }

            var knx = Merge(p.Knx).OrderBy(i => i.Psu ? 0 : 1).ThenBy(i => i.Name, Alpha).ToList();
            if (knx.Count > 0)
            {
                res.Rows.Add(SpecRow.Empty());
                res.Rows.Add(SpecRow.Sub(Names.SPEC_SUB_KNX));
                foreach (var it in knx) { res.Rows.Add(row(it)); res.Items++; }
            }
            res.Rows.Add(SpecRow.Empty());
        }

        /// Равные (НАИМЕНОВАНИЕ_СП + ТИП_МАРКА_СП + КОД_СП без пробелов, один регистр) схлопываются, количество суммируется.
        static List<Item> Merge(IEnumerable<Item> items)
        {
            var map = new Dictionary<string, Item>();
            var order = new List<Item>();
            foreach (var it in items)
            {
                if (map.TryGetValue(it.Key, out var m))
                {
                    m.Qty += it.Qty;
                    m.Ustavka = Math.Min(m.Ustavka, it.Ustavka);
                    m.Psu |= it.Psu;
                    if (m.Maker.Length == 0) m.Maker = it.Maker;
                }
                else { map[it.Key] = it; order.Add(it); }
            }
            return order;
        }

        // ------------------------------------------------------------------ кабели и трубы
        /// Все блоки во всех рамках с заполненными <tag> и ДЛИНА; одинаковые по ключу складываются.
        static List<Line> Collect(DrawingModel model, SpecResult res, string tag, Func<string, Line> parse)
        {
            var map = new Dictionary<string, Line>();
            foreach (var s in model.Scopes)
                foreach (var b in Blocks(s))
                {
                    string raw = Val(b, tag);
                    if (!Filled(raw)) continue;                       // пусто или «НЕТ» — кабеля/трубы нет по смыслу
                    string len = Val(b, A_DLINA);
                    if (!Filled(len)) continue;                       // без длины в спецификацию не входит
                    if (!Num(len, out double L) || L < 0)
                    {
                        res.Notes.Add($"{BlockName(b)} <{Handle(b)}>: {A_DLINA} «{len}» не число — {tag} «{SpecParse.Clean(raw)}» не учтён");
                        continue;
                    }
                    string key = SpecParse.Key(raw);
                    if (!map.TryGetValue(key, out var line)) map[key] = line = parse(raw);
                    line.Length += L;
                    line.Blocks++;
                }
            return map.Values.ToList();
        }
    }
}