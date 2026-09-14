using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace KnxTools.Model
{
    /// Базовая пара Имя–Значение с признаком «удалось ли прочитать». От неё наследуется атрибут блока
    /// (и в будущем — любая другая «ячейка данных»).
    public abstract class NamedValue
    {
        public string Name { get; }
        public string Value { get; protected set; } = "";
        public bool IsFound { get; protected set; }
        public object Owner { get; }                  // хозяин — вставка блока
        public abstract string Source { get; }        // откуда взято — для отчёта

        protected NamedValue(string name, object owner) { Name = name; Owner = owner; }

        public void SetValue(string value) { Value = value ?? ""; IsFound = true; }
        public void SetMissing() { Value = ""; IsFound = false; }
        public override string ToString() => $"{Name} = \"{Value}\"" + (IsFound ? "" : " (не найдено)");
    }

    /// 2.1 — атрибут вставки блока (не определения).
    public class BlockAttr : NamedValue
    {
        public BlockEntity Block => (BlockEntity)Owner;
        public ObjectId AttrId { get; private set; } = ObjectId.Null;   // пригодится для записи обратно

        public override string Source => $"атрибут {Name} блока {Block.BlockName} <{Block.Handle}>";

        public BlockAttr(string name, BlockEntity owner) : base(name, owner) { }

        /// Способ чтения: из словаря фактических атрибутов вставки (тег → id + текст).
        public void ReadFrom(IReadOnlyDictionary<string, KeyValuePair<ObjectId, string>> actual)
        {
            if (actual != null && actual.TryGetValue(Name, out var pair))
            {
                AttrId = pair.Key;
                SetValue(pair.Value);
            }
            else SetMissing();
        }

        /// Записать значение в чертёж и в память. false — атрибута во вставке нет, писать некуда.
        public bool Write(string value, Transaction tr, Report report, string where)
        {
            value = value ?? "";
            if (AttrId.IsNull)
            {
                report.Warn(where, $"атрибут {Name} отсутствует во вставке блока {Block.BlockName} — значение «{value}» не записано", Block.Handle);
                return false;
            }
            var ar = (AttributeReference)tr.GetObject(AttrId, OpenMode.ForWrite);
            if (ar.TextString != value) ar.TextString = value;
            SetValue(value);
            return true;
        }
    }
}
