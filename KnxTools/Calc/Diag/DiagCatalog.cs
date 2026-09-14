namespace KnxTools.Calc.Diag
{
    /// Уровень результата проверки.
    public enum DiagLevel
    {
        Fatal = 0,   // расчёт НЕ выполнен (для чертежа или для щита)
        Error = 1,   // выполнен, но результат недостоверен — исправить и пересчитать
        Notice = 2    // выполнен, обратите внимание
    }

    public sealed class DiagCode
    {
        public readonly string Id;        // F01 / E03 / N02
        public readonly DiagLevel Level;
        public readonly int Step;         // основной шаг: 0 — чтение чертежа / CalcRunner, 1..8 — шаг SDK_CALC_PANEL
        public readonly string Steps;     // где встречается, текстом: «шаги 3, 4»
        public readonly string Title;     // короткое имя
        public readonly string What;      // что обнаружено
        public readonly string Action;    // что сделать

        public DiagCode(string id, DiagLevel level, int step, string steps, string title, string what, string action)
        { Id = id; Level = level; Step = step; Steps = steps; Title = title; What = what; Action = action; }

        public string LevelText
        {
            get
            {
                switch (Level)
                {
                    case DiagLevel.Fatal: return "ФАТАЛЬНО — не выполнено";
                    case DiagLevel.Error: return "ОШИБКА — выполнено с ошибками";
                    default: return "ВНИМАНИЕ — выполнено";
                }
            }
        }

        public string StepText => Steps;

        public override string ToString() => Id + " " + Title;
    }

    /// ============================================================================
    ///                 СПРАВОЧНИК ПРОВЕРОК КОМАНДЫ SDK_CALC_PANEL
    ///  Добавить проверку = добавить DiagCode здесь и вписать в All. Больше ничего.
    ///  F — расчёт не выполнен; E — выполнен с ошибками; N — выполнен, обратите внимание.
    /// ============================================================================
    public static class DiagCatalog
    {
        // ---------- F: расчёт не выполнен ----------
        public static readonly DiagCode F01 = new DiagCode("F01", DiagLevel.Fatal, 0, "чтение",
            "Нет рамок щитов",
            "В слое SDK_KontPanel не найдено ни одной полилинии. Расчёт не запускался.",
            "Обвести щит замкнутой полилинией в слое SDK_KontPanel. Проверить имя слоя: пробелы, регистр.");

        public static readonly DiagCode F02 = new DiagCode("F02", DiagLevel.Fatal, 0, "чтение",
            "Щит пуст",
            "Внутри рамки нет ни одного блока SDK_АВТОМАТ_УНИВ. Все шаги для этого щита пропущены.",
            "Проверить, что точки вставки автоматов лежат внутри рамки и блоки называются SDK_АВТОМАТ_УНИВ.");

        public static readonly DiagCode F03 = new DiagCode("F03", DiagLevel.Fatal, 0, "любой",
            "Сбой шага",
            "Шаг прерван внутренней ошибкой. Шаг для этого щита не выполнен; остальные шаги и щиты продолжены.",
            "Текст из колонки «Подробности» — разработчику. Обход: найти объект по Handle и проверить его атрибуты.");

        // ---------- E: выполнено с ошибками ----------
        public static readonly DiagCode E01 = new DiagCode("E01", DiagLevel.Error, 3, "шаги 3, 4",
            "Нечисловое значение",
            "В атрибуте МОЩНОСТЬ / КОСИНУС / УСТАВКА / ТОК стоит текст, который не читается как число. В расчёт принят 0.",
            "Исправить значение: только цифры, разделитель точка или запятая, без единиц измерения.");

        public static readonly DiagCode E02 = new DiagCode("E02", DiagLevel.Error, 2, "шаги 2, 3, 4, 7, 8",
            "Атрибут отсутствует в блоке",
            "Во вставке блока нет атрибута, в который расчёт должен записать результат. Значение не записано.",
            "Добавить атрибут в определение блока и выполнить ATTSYNC, либо перевставить блок из актуальной библиотеки.");

        public static readonly DiagCode E03 = new DiagCode("E03", DiagLevel.Error, 3, "шаг 3",
            "Перегрузка автомата",
            "Расчётный ток группы больше уставки автомата.",
            "Увеличить уставку, разделить нагрузку на две группы или уточнить мощность и cosφ.");

        public static readonly DiagCode E04 = new DiagCode("E04", DiagLevel.Error, 4, "шаги 4, 6, 7",
            "Неполные данные щита",
            "У автомата пуст НОМЕР_АВТ, УСТАВКА или другой атрибут, который нужен для итога щита или передаётся в приёмники. Расчёт выполнен с пустым значением.",
            "Заполнить атрибут автомата (или выполнить шаги 2–3, которые его заполняют) и пересчитать.");

        public static readonly DiagCode E05 = new DiagCode("E05", DiagLevel.Error, 5, "шаг 5",
            "Дубликат группы",
            "В щите два и более автоматов с одинаковым значением ГРУППА. Привязка приёмников к автомату неоднозначна — взят первый.",
            "Дать каждой группе уникальное имя в ГРУППА автомата и в приёмниках.");

        // ---------- N: выполнено, обратите внимание ----------
        public static readonly DiagCode N01 = new DiagCode("N01", DiagLevel.Notice, 1, "шаг 1",
            "cosφ по умолчанию",
            "КОСИНУС пуст — принято значение по умолчанию для типа нагрузки.",
            "Проверить, что значение по умолчанию подходит; при необходимости заполнить атрибут.");

        public static readonly DiagCode N02 = new DiagCode("N02", DiagLevel.Notice, 3, "шаги 3, 4",
            "Автомат без нагрузки",
            "МОЩНОСТЬ пуста или 0 — автомат учтён как резерв, в сумму щита не входит.",
            "Если это не резерв — заполнить МОЩНОСТЬ.");

        public static readonly DiagCode N03 = new DiagCode("N03", DiagLevel.Notice, 5, "шаги 5, 8",
            "Блок без привязки",
            "Блок лежит в рамке щита, но не попал ни в контейнер, ни в группу автомата. В расчёте и нумерации не участвует.",
            "Переместить блок внутрь контейнера SDK_КОНТЕЙНЕР_KNX или указать ГРУППА, совпадающую с автоматом щита.");

        public static readonly DiagCode[] All = { F01, F02, F03, E01, E02, E03, E04, E05, N01, N02, N03 };

        /// Текст справочника для командной строки (SDK_CALC_HELP).
        public static string AsText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("===== Проверки команды SDK_CALC_PANEL =====");
            foreach (var c in All)
            {
                sb.AppendLine($"{c.Id}  [{c.LevelText}]  {c.Steps}  —  {c.Title}");
                sb.AppendLine($"      что: {c.What}");
                sb.AppendLine($"      делать: {c.Action}");
            }
            return sb.ToString();
        }

    }
}
