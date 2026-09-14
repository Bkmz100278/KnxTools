using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using KnxTools.Model;

namespace KnxTools.Calc
{
    /// Атрибуты (живое чтение/запись из вставки блока), числа, признаки, обход блоков щита.
    public static class CalcUtil
    {
        public static bool Is(string a, string b) =>
            string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

        static bool Contains(string s, string word) =>
            (s ?? "").ToLowerInvariant().Contains((word ?? "").ToLowerInvariant());

        // ---------------- атрибуты ----------------
        static AttributeReference FindAttr(BlockEntity b, string tag, Transaction tr)
        {
            if (b == null || b.Id.IsNull || b.Id.IsErased) return null;
            var br = tr.GetObject(b.Id, OpenMode.ForRead) as BlockReference;
            if (br == null) return null;
            foreach (ObjectId id in br.AttributeCollection)
            {
                if (id.IsErased) continue;
                var ar = tr.GetObject(id, OpenMode.ForRead) as AttributeReference;
                if (ar != null && Is(ar.Tag, tag)) return ar;
            }
            return null;
        }

        public static bool Has(BlockEntity b, string tag, Transaction tr) => FindAttr(b, tag, tr) != null;

        public static string Get(BlockEntity b, string tag, Transaction tr)
        {
            var ar = FindAttr(b, tag, tr);
            return ar == null ? "" : (ar.TextString ?? "").Trim();
        }

        /// false — атрибута в блоке нет (не ошибка, просто пропуск).
        public static bool Set(BlockEntity b, string tag, string value, Transaction tr)
        {
            var ar = FindAttr(b, tag, tr);
            if (ar == null) return false;
            value = value ?? "";
            if (ar.TextString != value)
            {
                ar.UpgradeOpen();
                ar.TextString = value;
            }
            return true;
        }

        /// Set с предупреждением в отчёт, если атрибута нет.
        public static void SetOrWarn(BlockEntity b, string tag, string value, Transaction tr, Report report, string where)
        {
            if (!Set(b, tag, value, tr))
                report.Warn(where, $"{b.BlockName}: нет атрибута {tag}", b.Handle);
        }

        // ---------------- признаки ----------------
        public static bool IsReserve(BlockEntity b, Transaction tr) =>
            Contains(Get(b, Names.NATR_NAZVANIE_PRIEMNIKA, tr), Names.NV_REZERV);

        //public static bool IsVvod(BlockEntity b, Transaction tr) =>
        //    Contains(Get(b, Names.NATR_TIP, tr), Names.NV_VVOD) ||
        //    Contains(Get(b, Names.NATR_GRUPPA, tr), Names.NV_VVOD) ||
        //    Contains(Get(b, Names.NATR_NAZVANIE_PRIEMNIKA, tr), Names.NV_VVOD);

        /// ФАЗНОСТЬ содержит «3» → трёхфазный.
        public static bool ThreePhase(string faznost) => (faznost ?? "").Contains("3");

        // ---------------- числа ----------------
        static readonly Regex NumRx = new Regex(@"-?\d+(?:[.,]\d+)?", RegexOptions.Compiled);

        /// «2,5 кВт» → 2.5
        public static bool Num(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            var m = NumRx.Match(s);
            return m.Success && double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        /// Косинус корректен, если это число в (0; 1].
        public static bool ValidCos(string s, out double cos) => Num(s, out cos) && cos > 0 && cos <= 1;

        /// Косинус блока; некорректный → 1.
        public static double Cos(BlockEntity b, Transaction tr) =>
            ValidCos(Get(b, Names.NATR_KOSINUS, tr), out double c) ? c : 1.0;

        public static string F(double v, string format)
        {
            string s = v.ToString(format, CultureInfo.InvariantCulture);
            return Names.CALC_DECIMAL_COMMA ? s.Replace('.', ',') : s;
        }

        /// Ток, А: P в кВт.
        public static double Current(double pKw, double cos, bool threePhase) =>
            threePhase
                ? pKw * 1000.0 / (Math.Sqrt(3) * Names.CALC_U_LINE * cos)
                : pKw * 1000.0 / (Names.CALC_U_PHASE * cos);

        // ---------------- щит ----------------
        /// Все блоки рамки без повторов.
        public static List<BlockEntity> AllBlocks(PanelScope s)
        {
            var list = new List<BlockEntity>();
            var seen = new HashSet<ObjectId>();
            void Add(BlockEntity b) { if (b != null && seen.Add(b.Id)) list.Add(b); }

            foreach (var b in s.Panel.ShitBlocks) Add(b);
            foreach (var b in s.Panel.ShemaBlocks) Add(b);
            foreach (var b in s.Panel.Equipment) Add(b);
            foreach (var b in s.Panel.Avtomats) Add(b);
            foreach (var c in s.Containers) { Add(c); foreach (var ch in c.Children) Add(ch); }
            foreach (var b in s.LooseBlocks) Add(b);
            return list;
        }

        /// Живое имя щита: НАЗВАНИЕ_ПО_СХЕМЕ у SDK_ОБ_ЩИТ, иначе первое НАЗВАНИЕ_ЩИТА_ПО_СХЕМЕ в рамке.
        public static string PanelName(PanelScope s, Transaction tr)
        {
            var shit = s.Panel.ShitBlocks.FirstOrDefault();
            string n = shit != null ? Get(shit, Names.NATR_NAZVANIE_PO_SHEME, tr) : "";
            if (n.Length > 0) return n;
            foreach (var b in AllBlocks(s))
            {
                n = Get(b, Names.NATR_NAZVANIE_SHITA_PO_SHEME, tr);
                if (n.Length > 0) return n;
            }
            return "";
        }

        public static string Label(PanelScope s, Transaction tr)
        {
            string n = PanelName(s, tr);
            return n.Length > 0 ? n : "полилиния <" + s.Handle + ">";
        }

        /// Точка вставки блока (для сортировки по расположению).
        public static Point3d Pos(BlockEntity e, Transaction tr)
        {
            var br = tr.GetObject(e.Id, OpenMode.ForRead) as BlockReference;
            return br != null ? br.Position : Point3d.Origin;
        }

    }
}