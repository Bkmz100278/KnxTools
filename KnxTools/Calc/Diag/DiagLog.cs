using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Model;

namespace KnxTools.Calc.Diag
{
    /// Одно срабатывание проверки.
    public sealed class DiagItem
    {
        public DiagCode Code;
        public string Panel;     // имя щита или «полилиния <handle>»
        public string Handle;    // объект чертежа (может быть пустым)
        public string Details;   // конкретика: какой атрибут, какие числа

        public override string ToString()
        {
            return $"[{Code.Id}] {Code.Title} | {Panel} | {Details}" + (Handle.Length == 0 ? "" : $" | <{Handle}>");
        }
    }

    /// Накопитель срабатываний за один запуск команды. Одинаковые (код + объект + текст) не дублируются.
    public sealed class DiagLog
    {
        public readonly List<DiagItem> Items = new List<DiagItem>();

        public void Add(DiagCode code, string panel, object handle, string details)
        {
            string h = handle == null ? "" : handle.ToString();
            panel = panel ?? ""; details = details ?? "";
            if (Items.Any(i => i.Code == code && i.Handle == h && i.Details == details)) return;
            Items.Add(new DiagItem { Code = code, Panel = panel, Handle = h, Details = details });
        }

        public bool IsEmpty => Items.Count == 0;
        public int Count(DiagLevel level) => Items.Count(i => i.Code.Level == level);
        public int CountOf(DiagCode code) => Items.Count(i => i.Code == code);

        public IEnumerable<DiagItem> Sorted =>
            Items.OrderBy(i => i.Code.Level).ThenBy(i => i.Code.Id).ThenBy(i => i.Panel).ThenBy(i => i.Handle);

        /// Записать атрибут блока. Нет атрибута — E02, значение не записано.
        public bool Set(BlockEntity b, string attr, string value, Transaction tr, string panel)
        {
            if (CalcUtil.Set(b, attr, value, tr)) return true;
            Add(DiagCatalog.E02, panel, b.Handle, $"{b.BlockName}: нет атрибута {attr}, значение «{value}» не записано");
            return false;
        }
    }
}