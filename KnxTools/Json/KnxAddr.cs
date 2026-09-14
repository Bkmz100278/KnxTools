using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace KnxTools.Json
{
    public static class KnxAddr
    {
        static readonly char[] PaSeps = { '.', ',', ':', ' ' };
        static readonly char[] GaSeps = { '/', '\\', '-', ' ' };
        static readonly Regex DptRx = new Regex(@"^\d{1,2}\.\d{3}$", RegexOptions.Compiled);

        /// «1.1.11» → PA. Пусто → null без ошибки. Не по формату / вне диапазона → null + error.
        public static PaDto ParsePa(string raw, out string error)
        {
            error = null;
            var s = (raw ?? "").Trim();
            if (s.Length == 0) return null;
            var n = Ints(s.Split(PaSeps, StringSplitOptions.RemoveEmptyEntries));
            if (n == null || n.Length != 3) { error = $"PA «{raw}»: ожидается область.линия.устройство"; return null; }
            if (n[0] < 0 || n[0] > 15) error = $"PA «{raw}»: область вне 0–15";
            else if (n[1] < 0 || n[1] > 15) error = $"PA «{raw}»: линия вне 0–15";
            else if (n[2] < 0 || n[2] > 255) error = $"PA «{raw}»: устройство вне 0–255";
            return error == null ? new PaDto { Area = n[0], Line = n[1], Device = n[2] } : null;
        }

        /// «1/1/1» → ГА (Name и Dpt не заполнены).
        public static GroupAddressDto ParseGa(string raw, out string error)
        {
            error = null;
            var s = (raw ?? "").Trim();
            if (s.Length == 0) return null;
            var n = Ints(s.Split(GaSeps, StringSplitOptions.RemoveEmptyEntries));
            if (n == null || n.Length != 3) { error = $"ГА «{raw}»: ожидается три уровня главная/средняя/подгруппа"; return null; }
            if (n[0] < 0 || n[0] > 31) error = $"ГА «{raw}»: главная группа вне 0–31";
            else if (n[1] < 0 || n[1] > 7) error = $"ГА «{raw}»: средняя группа вне 0–7";
            else if (n[2] < 0 || n[2] > 255) error = $"ГА «{raw}»: подгруппа вне 0–255";
            else if (n[0] == 0 && n[1] == 0 && n[2] == 0) error = "ГА 0/0/0 недопустима";
            return error == null ? new GroupAddressDto { Main = n[0], Middle = n[1], Sub = n[2] } : null;
        }

        /// «1/1/1; 1/1/2; 1/1/3» + «1.008; 1.007; 5.001» → три ГА со своими DPT.
        /// Один DPT на несколько ГА — применяется ко всем. Иное несовпадение количества — ошибка.
        public static List<GroupAddressDto> ParseGaDpt(string gaRaw, string dptRaw, List<string> errors)
        {
            var res = new List<GroupAddressDto>();
            var gas = SplitList(gaRaw);
            var dpts = SplitList(dptRaw).Select(NormDpt).ToList();
            if (gas.Count == 0) return res;
            if (dpts.Count > 1 && dpts.Count != gas.Count)
                errors.Add($"ГА {gas.Count} шт., DPT {dpts.Count} шт. — количество должно совпадать (или один DPT на все)");

            for (int k = 0; k < gas.Count; k++)
            {
                var ga = ParseGa(gas[k], out var err);
                if (ga == null) { if (err != null) errors.Add(err); continue; }
                string dpt = dpts.Count == 1 ? dpts[0] : (k < dpts.Count ? dpts[k] : null);
                if (dpt != null && !DptRx.IsMatch(dpt)) errors.Add($"DPT «{dpt}» не по формату N.NNN");
                ga.Dpt = dpt;
                res.Add(ga);
            }
            return res;
        }

        /// «1;2;3;4», «1, 2 3» → [1,2,3,4].
        public static List<int> ParseIntList(string raw, List<string> errors)
        {
            var res = new List<int>();
            foreach (var p in (raw ?? "").Split(';', ',', ' '))
            {
                if (p.Trim().Length == 0) continue;
                if (int.TryParse(p.Trim(), out var v)) res.Add(v); else errors.Add($"«{p}» — не число");
            }
            return res;
        }

        /// «DPT 1.001», «1,001» → «1.001».
        static string NormDpt(string raw)
        {
            var s = (raw ?? "").Trim().Replace(',', '.');
            return Regex.Replace(s, @"^(?i)dpt\s*-?\s*", "");
        }

        static List<string> SplitList(string raw) =>
            (raw ?? "").Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

        static int[] Ints(string[] parts)
        {
            var r = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++) if (!int.TryParse(parts[i].Trim(), out r[i])) return null;
            return r;
        }
    }
}
