using System.Collections.Generic;
using KnxTools.Model;

namespace KnxTools.Spec
{
    /// ЗАГЛУШКИ. Всё, что спецификация знает о модели чертежа и о Names, — здесь.
    /// Если у BlockEntity / PanelScope / Names что-то названо иначе — правится только этот файл.
    internal static class SpecAccess
    {
        // --- имена блоков и атрибутов (из Names.cs)
        public static readonly string NB_OB_SHIT = Names.NB_OB_SHIT;            // SDK_ОБ_ЩИТ
        public static readonly string NB_OB_KNX = Names.NB_OB_KNX;             // SDK_ОБ_KNX
        public static readonly string A_NAME = Names.NATR_NAIMENOVANIE_SP;  // НАИМЕНОВАНИЕ_СП
        public static readonly string A_MARK = Names.NATR_TIP_MARKA_SP;     // ТИП_МАРКА_СП
        public static readonly string A_CODE = Names.NATR_KOD_SP;           // КОД_СП
        public static readonly string A_MAKER = Names.NATR_IZGOTOVITEL_SP;   // ИЗГОТОВИТЕЛЬ_СП
        public static readonly string A_PANEL_NAME = Names.NATR_NAZVANIE_PO_SHEME;// НАЗВАНИЕ_ПО_СХЕМЕ
        public static readonly string A_USTAVKA = Names.NATR_USTAVKA;          // УСТАВКА
        public static readonly string A_PROVOD = Names.NATR_PROVOD;           // ПРОВОД
        public static readonly string A_TRUBA = Names.NATR_TRUBA;            // ТРУБА
        public static readonly string A_DLINA = Names.NATR_DLINA;            // ДЛИНА

        // --- модель
        /// Эффективное имя блока (для динамических — имя определения, не *U12).
        public static string BlockName(BlockEntity b) => b.BlockName;
        public static string Handle(BlockEntity b) => b.Handle;
        public static string PanelHandle(PanelScope s) => s.Handle;
        /// Все блоки в рамке щита, ВКЛЮЧАЯ детей контейнеров SDK_КОНТЕЙНЕР_KNX (иначе не будет оборудования KNX).
        public static IEnumerable<BlockEntity> Blocks(PanelScope s) => s.AllBlocks();

        // --- чтение атрибутов (те же помощники, что в проверках)
        public static string Val(BlockEntity b, string tag) => KnxTools.Check.CheckUtil.Val(b, tag);
        public static bool Filled(string v) => KnxTools.Check.CheckUtil.Filled(v);   // не пусто и не флаг «НЕТ»
        public static bool None(string v) => KnxTools.Check.CheckUtil.None(v);       // НЕТ / - / — / N/A
        public static bool Num(string v, out double d) => KnxTools.Check.CheckUtil.Num(v, out d);
    }
}