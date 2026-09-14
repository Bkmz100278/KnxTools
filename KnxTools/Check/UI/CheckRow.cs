using System;

namespace KnxTools.Check.Ui
{
    /// Одна строка окна. Плоская копия CheckItem — окно не держит ссылок на модель чертежа.
    public sealed class CheckRow
    {
        public int N { get; set; }
        public CheckLevel Level { get; set; }
        public string LevelText { get; set; }
        public string Code { get; set; }
        public string Title { get; set; }     // название проверки — в подсказке строки
        public string Panel { get; set; }
        public string Where { get; set; }     // объект словами
        public string Details { get; set; }
        public string Action { get; set; }
        public string Handle { get; set; }

        /// Ключ, по которому после «Обновить» находим ту же запись и возвращаем на неё выделение.
        public string Key => Code + "|" + Handle + "|" + Where + "|" + Details;

        // ===== ЗАГЛУШКА: единственное место, где окно знает поля CheckItem / CheckCode. =====
        public static CheckRow From(int n, CheckItem it)
        {
            var r = new CheckRow
            {
                N = n,
                Level = it.Code.Level,
                Code = it.Code.Id,
                Title = it.Code.Title,
                Action = it.Code.Action,
                Panel = it.Panel ?? "",
                Where = it.Obj ?? "",
                Details = it.Details ?? "",
                Handle = it.Handle ?? ""
            };
            r.LevelText = LevelName(r.Level);
            return r;
        }
        // ======================================================================================

        public static string LevelName(CheckLevel l)
        {
            switch (l)
            {
                case CheckLevel.Fatal: return "Фатально";
                case CheckLevel.Error: return "Ошибка";
                default: return "Замечание";
            }
        }

        public bool Matches(string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var s = search.Trim();
            return Has(Code, s) || Has(Panel, s) || Has(Where, s) || Has(Details, s) || Has(Action, s) || Has(Title, s) || Has(Handle, s);
        }

        static bool Has(string text, string s) =>
            !string.IsNullOrEmpty(text) && text.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
