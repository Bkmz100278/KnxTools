using System;
using System.Collections.Generic;
using System.Globalization;
using KnxTools.Model;
using System.Linq;

namespace KnxTools.Check
{
    public sealed class PhysAddr
    {
        public int Area, Line, Device;
        public bool HasDevice;
        public string LineKey => Area + "." + Line;
        public string Key => HasDevice ? Area + "." + Line + "." + Device : LineKey;
    }

    public sealed class GroupAddr
    {
        public int Main, Middle, Sub;
        public string Key => Main + "/" + Middle + "/" + Sub;
    }

    internal static class CheckUtil
    {
        // ---------- значения атрибутов ----------
        public static string Val(BlockEntity b, string tag) => b == null ? "" : (b.GetAny(tag) ?? "").Trim();
        public static bool Empty(string s) => string.IsNullOrWhiteSpace(s);
        public static bool None(string s) => Names.IsNone(s);
        public static bool Filled(string s) => !Empty(s) && !None(s);
        public static bool HasWord(string s, string word) => !Empty(s) && s.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0;

        /// «2,5 кВт» → 2.5; «abc» → false.
        public static bool Num(string s, out double v)
        {
            v = 0;
            if (Empty(s)) return false;
            string t = s.Trim().Replace(',', '.');
            int end = 0;
            while (end < t.Length && (char.IsDigit(t[end]) || t[end] == '.' || (end == 0 && t[end] == '-'))) end++;
            if (end == 0) return false;
            return double.TryParse(t.Substring(0, end), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        public static bool Int(string s, out int v) => int.TryParse((s ?? "").Trim(), out v);

        // ---------- адреса ----------
        public static bool ParsePhys(string s, out PhysAddr a, out string err)
        {
            a = null; err = null;
            var p = (s ?? "").Trim().Split('.');
            if (p.Length < 2 || p.Length > 3) { err = "ожидается вид О.Л.У, например 1.1.5"; return false; }
            if (!Int(p[0], out int ar) || ar < 0 || ar > Names.CHECK_KNX_AREA_MAX) { err = $"область «{p[0]}» не число 0–{Names.CHECK_KNX_AREA_MAX}"; return false; }
            if (!Int(p[1], out int ln) || ln < 0 || ln > Names.CHECK_KNX_LINE_MAX) { err = $"линия «{p[1]}» не число 0–{Names.CHECK_KNX_LINE_MAX}"; return false; }
            var r = new PhysAddr { Area = ar, Line = ln };
            if (p.Length == 3 && !None(p[2]))
            {
                if (!Int(p[2], out int dv) || dv < 1 || dv > Names.CHECK_KNX_DEVICE_MAX) { err = $"устройство «{p[2]}» не число 1–{Names.CHECK_KNX_DEVICE_MAX}"; return false; }
                r.Device = dv; r.HasDevice = true;
            }
            a = r; return true;
        }

        public static bool ParseGa(string s, out GroupAddr g, out string err)
        {
            g = null; err = null;
            var p = (s ?? "").Trim().Split('/');
            if (p.Length != 3) { err = "ожидается трёхуровневый вид Г/С/П, например 1/2/3"; return false; }
            if (!Int(p[0], out int m) || m < 0 || m > Names.CHECK_GA_MAIN_MAX) { err = $"главная группа «{p[0]}» не число 0–{Names.CHECK_GA_MAIN_MAX}"; return false; }
            if (!Int(p[1], out int mid) || mid < 0 || mid > Names.CHECK_GA_MIDDLE_MAX) { err = $"средняя группа «{p[1]}» не число 0–{Names.CHECK_GA_MIDDLE_MAX}"; return false; }
            if (!Int(p[2], out int sub) || sub < 0 || sub > Names.CHECK_GA_SUB_MAX) { err = $"подгруппа «{p[2]}» не число 0–{Names.CHECK_GA_SUB_MAX}"; return false; }
            g = new GroupAddr { Main = m, Middle = mid, Sub = sub }; return true;
        }

        ///// Ключ ГА для сравнения: «1/2/3» или null, если не читается.
        //public static string GaKey(string s) => ParseGa(s, out var g, out _) ? g.Key : null;


        // ---------- списки адресов: «1/3/1; 1/3/2; 1/3/3» ----------
        static readonly char[] ListSep = { ';', '\n', '\r' };

        /// Элементы списка без пустых и пробелов.
        public static string[] SplitList(string s)
            => (s ?? "").Split(ListSep, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

        /// Все корректные ключи ГА из списка (некорректные молча пропускаются — о них скажет Chk3).
        public static IEnumerable<string> GaKeys(string s)
        {
            foreach (var part in SplitList(s))
                if (ParseGa(part, out var g, out _)) yield return g.Key;
        }

        /// Ключ одиночного ГА: «1/2/3» или null, если не читается.
        public static string GaKey(string s) => ParseGa(s, out var g, out _) ? g.Key : null;

        /// DPT для сравнения: «DPT 1.001» и «1.001» — одно и то же.
        public static string DptKey(string s) => (s ?? "").ToUpperInvariant().Replace("DPT", "").Replace(" ", "").Trim();





        /// «0,1,2», «0-3», «0;1 2» → список адресов балластов.
        public static bool ParseDali(string s, List<int> res, out string err)
        {
            err = null;
            foreach (var tok in (s ?? "").Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string t = tok.Trim();
                int dash = t.IndexOf('-'); if (dash < 0) dash = t.IndexOf('–');
                if (dash > 0)
                {
                    if (!Int(t.Substring(0, dash), out int a) || !Int(t.Substring(dash + 1), out int b) || b < a || b - a > Names.CHECK_DALI_ADDR_MAX)
                    { err = $"диапазон «{t}» не читается"; return false; }
                    for (int i = a; i <= b; i++) res.Add(i);
                }
                else if (Int(t, out int v)) res.Add(v);
                else { err = $"«{t}» не число"; return false; }
            }
            if (res.Count == 0) { err = "адресов нет"; return false; }
            return true;
        }

        /// DPT для сравнения: «DPT 1.001» и «1.001» — одно и то же.
        //public static string DptKey(string s) => (s ?? "").ToUpperInvariant().Replace("DPT", "").Replace(" ", "").Trim();

        // ---------- электрика ----------
        public static bool IsReserve(AvtomatUnivBlock a)
            => HasWord(Val(a, Names.NATR_GRUPPA), Names.NV_REZERV) || HasWord(Val(a, Names.NATR_NAZVANIE_PRIEMNIKA), Names.NV_REZERV);

        public static bool ThreePhase(AvtomatUnivBlock a) => Val(a, Names.NATR_FAZNOST).Contains("3");

        public static double Cos(BlockEntity b)
            => Num(Val(b, Names.NATR_KOSINUS), out double c) && c > 0 && c <= 1 ? c : Names.COS_DEFAULT;

        public static double Current(double pKw, double cos, bool threePhase)
            => threePhase ? pKw * 1000.0 / (Math.Sqrt(3) * Names.CALC_U_LINE * cos)
                          : pKw * 1000.0 / (Names.CALC_U_PHASE * cos);

        public static string F(double v, string fmt = "0.0#")
        {
            string s = v.ToString(fmt, CultureInfo.InvariantCulture);
            return Names.CALC_DECIMAL_COMMA ? s.Replace('.', ',') : s;
        }

        // ---------- подписи ----------
        public static string PanelLabel(PanelScope s)
        {
            if (s == null) return "вне рамок";
            string n = s.Panel.Name;
            return Empty(n) ? "рамка <" + s.Handle + ">" : n;
        }

        public static string Label(BlockEntity b)
        {
            if (b == null) return "";
            string key = (b.DisplayKey ?? "").Trim();
            return key.Length > 0 && key != b.Handle ? $"{b.BlockName} «{key}»" : b.BlockName;
        }

        /// «Q3» или «SDK_АВТОМАТ_УНИВ <handle>».
        public static string Q(AvtomatUnivBlock a)
        {
            string n = Val(a, Names.NATR_NOMER_AVT);
            return Filled(n) ? n : Names.NB_AVTOMAT_UNIV + " <" + a.Handle + ">";
        }

        /// «выход 3» / «вход 2» / «выход <handle>».
        public static string Ch(BlockEntity b)
        {
            string kind = b is LiniyaKnxVhodBlock ? "вход" : "выход";
            string n = Val(b, Names.NATR_NOMER_KANALA);
            return kind + " " + (Filled(n) ? n : "<" + b.Handle + ">");
        }
    }
}
