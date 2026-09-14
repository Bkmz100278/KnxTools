using System.IO;
using System.Linq;
using System.Text;
using KnxTools.Model;

namespace KnxTools.Export
{
    public static class ModelExporter
    {
        public static void Export(DrawingModel m, string xlsxPath)
        {
            var x = new XlsxWriter();

            // ---------- Лист 1: сводка ----------
            var sum = x.AddSheet("Сводка");
            sum.Widths = new[] { 44.0, 16.0 };
            sum.Rows.Add(new object[] { "Показатель", "Значение" });
            sum.Rows.Add(new object[] { "Рамок (полилиний " + Names.NL_KONT_PANEL + ")", m.Scopes.Count });
            sum.Rows.Add(new object[] { "Наших блоков всего", m.TotalBlocks });
            sum.Rows.Add(new object[] { "Блоков " + Names.NB_OB_SHIT, m.Scopes.Sum(s => s.Panel.ShitBlocks.Count) });
            sum.Rows.Add(new object[] { "Автоматов", m.Scopes.Sum(s => s.Panel.Avtomats.Count) });
            sum.Rows.Add(new object[] { "Блоков " + Names.NB_SHEMA_SHIT, m.Scopes.Sum(s => s.Panel.ShemaBlocks.Count) });
            sum.Rows.Add(new object[] { "Щитового оборудования (" + Names.NBP_OB_SHITOVOE + "*)", m.Scopes.Sum(s => s.Panel.Equipment.Count) });
            sum.Rows.Add(new object[] { "Контейнеров KNX", m.Scopes.Sum(s => s.Containers.Count) });
            sum.Rows.Add(new object[] { "Блоков в контейнерах", m.Scopes.Sum(s => s.Containers.Sum(c => c.Children.Count)) });
            sum.Rows.Add(new object[] { "Блоков в рамке, но вне контейнеров", m.Scopes.Sum(s => s.LooseBlocks.Count) });
            sum.Rows.Add(new object[] { "Блоков вне рамок", m.OutsideBlocks.Count });
            sum.Rows.Add(new object[] { "Ошибок", m.Report.Errors });
            sum.Rows.Add(new object[] { "Предупреждений", m.Report.Warnings });

            // ---------- Лист 2: топология ----------
            var topo = x.AddSheet("Топология");
            topo.Widths = new[] { 8.0, 36.0, 26.0, 10.0, 16.0, 32.0, 40.0, 10.0 };
            topo.Rows.Add(new object[] { "Уровень", "Тип", "Ключ", "Handle", "Слой", "Поле", "Значение", "Найдено" });

            foreach (var s in m.Scopes)
            {
                Row(topo, 0, "РАМКА", s.Area?.ToString(), s.Handle, Names.NL_KONT_PANEL, "тип", s.PolyType);

                Row(topo, 1, "ЩИТ", s.Panel.Name, null, null, "автоматов", s.Panel.Avtomats.Count.ToString());
                foreach (var b in s.Panel.ShitBlocks) WriteBlock(topo, 2, b);
                foreach (var b in s.Panel.ShemaBlocks) WriteBlock(topo, 2, b);
                foreach (var b in s.Panel.Equipment) WriteBlock(topo, 2, b);
                foreach (var b in s.Panel.Avtomats) WriteBlock(topo, 2, b);

                foreach (var c in s.Containers)
                {
                    Row(topo, 1, "КОНТЕЙНЕР KNX", c.Area?.ToString(), c.Handle, c.Layer, "область", c.AreaSource);
                    foreach (var b in c.Children) WriteBlock(topo, 2, b);
                }

                if (s.LooseBlocks.Count > 0)
                {
                    Row(topo, 1, "В РАМКЕ, ВНЕ КОНТЕЙНЕРОВ", null, null, null);
                    foreach (var b in s.LooseBlocks) WriteBlock(topo, 2, b);
                }
            }

            if (m.OutsideBlocks.Count > 0)
            {
                Row(topo, 0, "ВНЕ ВСЕХ РАМОК", null, null, null);
                foreach (var b in m.OutsideBlocks) WriteBlock(topo, 1, b);
            }

            // ---------- Лист 3: плоский список блоков ----------
            var flat = x.AddSheet("Блоки");
            flat.Widths = new[] { 30.0, 26.0, 10.0, 16.0, 24.0, 20.0 };
            flat.Rows.Add(new object[] { "Блок", "Ключ", "Handle", "Слой", "Родитель", "Точка вставки" });
            foreach (var s in m.Scopes)
            {
                foreach (var b in s.Panel.ShitBlocks.Cast<BlockEntity>()
                         .Concat(s.Panel.ShemaBlocks).Concat(s.Panel.Equipment).Concat(s.Panel.Avtomats)
                         .Concat(s.Containers).Concat(s.Containers.SelectMany(c => c.Children).Where(ch => !(ch is AvtomatUnivBlock)))
                         .Concat(s.LooseBlocks))
                    flat.Rows.Add(new object[] { b.BlockName, b.DisplayKey, b.Handle, b.Layer, ParentName(b, s), $"{b.Position.X:F1}; {b.Position.Y:F1}" });
            }
            foreach (var b in m.OutsideBlocks)
                flat.Rows.Add(new object[] { b.BlockName, b.DisplayKey, b.Handle, b.Layer, "вне рамок", $"{b.Position.X:F1}; {b.Position.Y:F1}" });

            // ---------- Лист 4: ошибки ----------
            var err = x.AddSheet("Ошибки");
            err.Widths = new[] { 6.0, 12.0, 10.0, 28.0, 100.0 };
            err.Rows.Add(new object[] { "№", "Уровень", "Handle", "Где", "Сообщение" });
            int n = 1;
            foreach (var i in m.Report.Issues.OrderByDescending(i => i.Severity))
                err.Rows.Add(new object[] { n++, i.Severity.ToString(), i.Handle, i.Where, i.Message });

            x.Save(xlsxPath);

            var txt = Path.ChangeExtension(xlsxPath, null) + "_report.txt";
            File.WriteAllText(txt, m.Report.ToString(), Encoding.UTF8);
        }

        // ---------- помощники ----------
        private static string ParentName(BlockEntity b, PanelScope s)
        {
            switch (b.Parent)
            {
                case KonteynerKnxBlock c: return "контейнер <" + c.Handle + ">";
                case ElectricPanel p: return "щит " + p.Name;
                case PanelScope sc: return "рамка <" + sc.Handle + ">";
                default: return "";
            }
        }

        private static void Row(XlsxWriter.Sheet sh, int level, string type, string key, string handle, string layer,
                                string field = null, string value = null, string found = null)
        {
            sh.Rows.Add(new object[] { level, new string(' ', level * 3) + type, key, handle, layer, field, value, found });
        }

        private static void WriteBlock(XlsxWriter.Sheet sh, int level, BlockEntity b)
        {
            Row(sh, level, "Блок " + b.BlockName, b.DisplayKey, b.Handle, b.Layer, "точка вставки", $"{b.Position.X:F1}; {b.Position.Y:F1}");
            foreach (var a in b.Attrs.Values)
                Row(sh, level + 1, "атрибут", null, null, null, a.Name, a.Value, a.IsFound ? "да" : "НЕТ");
            foreach (var kv in b.ExtraAttrs)
                Row(sh, level + 1, "атрибут вне описания", null, null, null, kv.Key, kv.Value, "лишний");
        }
    }
}
