using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;

namespace KnxTools.Check
{
    public sealed class CheckItem
    {
        public CheckCode Code;
        public string Panel;    // щит (имя или «рамка <handle>»)
        public string Obj;      // человекочитаемый объект: «SDK_ЛИНИЯ_KNX_ВЫХОД «3»», «ZIOMB8V4 1.1.5»
        public string Handle;   // handle объекта чертежа, может быть пустым
        public string Details;

        public override string ToString()
            => $"{Panel} | {Obj} | {Details}" + (Handle.Length == 0 ? "" : $" | <{Handle}>");
    }

    /// Накопитель срабатываний за один запуск. Одинаковые (код + handle + текст) не дублируются.
    public sealed class CheckLog
    {
        public readonly List<CheckItem> Items = new List<CheckItem>();

        public void Add(CheckCode code, string panel, string obj, string handle, string details)
        {
            panel = panel ?? ""; obj = obj ?? ""; handle = handle ?? ""; details = details ?? "";
            if (Items.Any(i => i.Code == code && i.Handle == handle && i.Details == details)) return;
            Items.Add(new CheckItem { Code = code, Panel = panel, Obj = obj, Handle = handle, Details = details });
        }

        public void Add(CheckCode code, PanelScope scope, BlockEntity b, string details)
            => Add(code, scope == null ? "вне рамок" : CheckUtil.PanelLabel(scope),
                   b == null ? "" : CheckUtil.Label(b), b == null ? "" : b.Handle, details);

        public void Add(CheckCode code, KnxDevice d, string details)
            => Add(code, d.Scope == null ? "вне рамок" : CheckUtil.PanelLabel(d.Scope), d.Label, d.Container.Handle, details);

        public bool IsEmpty => Items.Count == 0;
        public int Count(CheckLevel level) => Items.Count(i => i.Code.Level == level);
        public int CountOf(CheckCode code) => Items.Count(i => i.Code == code);
        public int CountForHandles(HashSet<string> handles) => Items.Count(i => handles.Contains(i.Handle));

        public IEnumerable<CheckItem> Sorted =>
            Items.OrderBy(i => i.Code.Level).ThenBy(i => i.Code.Id).ThenBy(i => i.Panel).ThenBy(i => i.Obj).ThenBy(i => i.Handle);
    }
}