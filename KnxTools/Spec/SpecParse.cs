using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace KnxTools.Spec
{
    public sealed class CableInfo
    {
        public string Display = "";   // как написано в первом встреченном блоке
        public string Mark = "";      // для сортировки, нижний регистр
        public int Cores;             // число жил (2x2x0.8 → 4)
        public double Section;        // сечение последней размерности
        public bool Parsed;
    }

    public sealed class PipeInfo
    {
        public string Display = "";
        public string Mark = "";
        public double Diameter;
        public bool Parsed;
    }

    public static class SpecParse
    {
        static readonly Regex Ws = new Regex(@"\s+");
        static readonly Regex CableRx = new Regex(@"^(?<mark>.*?)\s*(?<sizes>\d+(?:\.\d+)?(?:\s*x\s*\d+(?:\.\d+)?)+)(?<tail>.*)$", RegexOptions.IgnoreCase);
        static readonly Regex PipeRx = new Regex(@"^(?<mark>.*?)\s*(?<d>\d+(?:\.\d+)?)(?<tail>.*)$");
        static readonly Regex FirstNum = new Regex(@"\d+(?:[.,]\d+)?");

        /// Ключ сравнения: без пробелов, один регистр, х→x, ×/*→x, запятая→точка.
        public static string Key(string s) => Unify(Squash(s));

        /// Без пробелов, нижний регистр — для сравнения наименований оборудования.
        public static string Squash(string s) => s == null ? "" : Ws.Replace(s, "").ToLowerInvariant();

        /// Пробелы схлопнуты, края обрезаны — для вывода.
        public static string Clean(string s) => s == null ? "" : Ws.Replace(s.Trim(), " ");

        /// Первое число в строке (уставка «C16», «16 А», «10»). false — числа нет.
        public static bool FirstNumber(string s, out double v)
        {
            v = 0;
            if (string.IsNullOrEmpty(s)) return false;
            var m = FirstNum.Match(s);
            return m.Success && double.TryParse(m.Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        public static CableInfo Cable(string raw)
        {
            var c = new CableInfo { Display = Clean(raw) };
            string work = Unify(c.Display);               // посимвольно той же длины, что Display
            var m = CableRx.Match(work);
            if (!m.Success) { c.Mark = work.ToLowerInvariant(); return c; }

            var nums = m.Groups["sizes"].Value.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            int cores = 1; double last = 0; bool ok = true;
            for (int i = 0; i < nums.Length; i++)
            {
                if (!double.TryParse(nums[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) { ok = false; break; }
                if (i == nums.Length - 1) last = v; else cores *= (int)Math.Round(v);
            }
            if (!ok) { c.Mark = work.ToLowerInvariant(); return c; }

            c.Mark = (m.Groups["mark"].Value + " " + m.Groups["tail"].Value).Trim().ToLowerInvariant();
            c.Cores = nums.Length == 1 ? 1 : cores;
            c.Section = last;
            c.Parsed = true;
            return c;
        }

        public static PipeInfo Pipe(string raw)
        {
            var p = new PipeInfo { Display = Clean(raw) };
            string work = Unify(p.Display);
            var m = PipeRx.Match(work);
            if (!m.Success || !double.TryParse(m.Groups["d"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            { p.Mark = work.ToLowerInvariant(); return p; }
            p.Mark = (m.Groups["mark"].Value + " " + m.Groups["tail"].Value).Trim().ToLowerInvariant();
            p.Diameter = d;
            p.Parsed = true;
            return p;
        }

        /// х/Х (кириллица), ×, * → x; запятая → точка. Длина строки не меняется.
        static string Unify(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
            {
                char ch = a[i];
                if (ch == 'х' || ch == 'Х' || ch == '×' || ch == '*') a[i] = 'x';
                else if (ch == ',') a[i] = '.';
            }
            return new string(a);
        }
    }
}