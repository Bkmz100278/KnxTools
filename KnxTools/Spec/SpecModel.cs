using System.Collections.Generic;

namespace KnxTools.Spec
{
    public enum SpecRowKind { Header, Section, Panel, SubHeader, Item, Empty }

    /// Одна строка листа. Qty — число (в Excel будет числом), QtyText — только для шапки.
    public sealed class SpecRow
    {
        public SpecRowKind Kind;
        public string Pos = "", Name = "", Mark = "", Code = "", Maker = "", Unit = "", Note = "", QtyText = "";
        public double? Qty;

        public static SpecRow Header()
        {
            var h = Names.SPEC_HEADER;
            return new SpecRow { Kind = SpecRowKind.Header, Pos = h[0], Name = h[1], Mark = h[2], Code = h[3], Maker = h[4], Unit = h[5], QtyText = h[6], Note = h[7] };
        }
        public static SpecRow Empty() => new SpecRow { Kind = SpecRowKind.Empty };
        public static SpecRow Section(string text) => new SpecRow { Kind = SpecRowKind.Section, Name = text };
        public static SpecRow Sub(string text) => new SpecRow { Kind = SpecRowKind.SubHeader, Name = text };
        public static SpecRow Panel(string pos, string title) => new SpecRow { Kind = SpecRowKind.Panel, Pos = pos, Name = title };
        public static SpecRow Item(string pos, string name, string mark, string code, string maker, string unit, double qty, string note = "")
            => new SpecRow { Kind = SpecRowKind.Item, Pos = pos, Name = name, Mark = mark, Code = code, Maker = maker, Unit = unit, Qty = qty, Note = note };
    }

    public sealed class SpecResult
    {
        public readonly List<SpecRow> Rows = new List<SpecRow>();
        public readonly List<string> Notes = new List<string>();   // что пропущено и почему — в командную строку
        public int Panels, Items, Cables, Pipes;
        public int Total => Items + Cables + Pipes;
    }
}