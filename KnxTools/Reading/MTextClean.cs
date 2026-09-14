using System;
using System.Text;
using System.Text.RegularExpressions;

namespace KnxTools.Reading
{
    /// Очистка MTEXT-кодов и нормализация строк для сравнения ключей.
    public static class MTextClean
    {
        private const string EscBs = "\u0001", EscOb = "\u0002", EscCb = "\u0003";

        private static readonly Regex RxUnicode = new Regex(@"\\U\+([0-9A-Fa-f]{4})", RegexOptions.Compiled);
        private static readonly Regex RxStack = new Regex(@"\\S([^;]*?)[\^#/]([^;]*);", RegexOptions.Compiled);
        private static readonly Regex RxCodeSemi = new Regex(@"\
\[ACFHQTWfcp][^;]*;", RegexOptions.Compiled); // \fArial|b0;  \H1.2x;  \C1;  \A1;  \pxi-3;
        private static readonly Regex RxToggle = new Regex(@"\
\[LlOoKkX]", RegexOptions.Compiled);          // подчёркивание и т.п. — без «;»
        private static readonly Regex RxSpaces = new Regex(@"\s+", RegexOptions.Compiled);

        /// Убирает форматирование MTEXT, оставляя видимый текст. Переносы строк -> '\n'.
        public static string Strip(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace(@"\\", EscBs).Replace(@"\{", EscOb).Replace(@"\}", EscCb);
            s = RxUnicode.Replace(s, m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            s = RxStack.Replace(s, "$1/$2");
            s = RxCodeSemi.Replace(s, "");
            s = RxToggle.Replace(s, "");
            s = s.Replace(@"\P", "\n").Replace(@"\~", " ");
            s = s.Replace("{", "").Replace("}", "");
            s = s.Replace(EscBs, "\\").Replace(EscOb, "{").Replace(EscCb, "}");
            s = s.Replace("%%d", "°").Replace("%%D", "°").Replace("%%p", "±").Replace("%%P", "±")
                 .Replace("%%c", "Ø").Replace("%%C", "Ø").Replace("%%u", "").Replace("%%U", "")
                 .Replace("%%o", "").Replace("%%O", "").Replace("%%%", "%");
            return s.Trim();
        }

        /// Ключ для сравнения: без форматирования, в одну строку, верхний регистр,
        /// без хвостовой пунктуации, кириллические «двойники» приведены к латинице.
        public static string Key(string s)
        {
            s = Strip(s).Replace('\n', ' ');
            s = RxSpaces.Replace(s, " ").Trim().ToUpperInvariant().TrimEnd(':', '.', ',', ';', ' ');
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                int i = Cyr.IndexOf(ch);
                sb.Append(i >= 0 ? Lat[i] : ch);
            }
            return sb.ToString();
        }

        private const string Cyr = "АВЕКМНОРСТУХ";
        private const string Lat = "ABEKMHOPCTYX";
    }
}
