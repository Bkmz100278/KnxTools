using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KnxTools
{
    /// Единственное место с именами из чертежа. Переименовали в AutoCAD — правите одну строку здесь.
    public static class Names
    {
        // ---------- Слои ----------
        public const string NL_SCHEM_EL = "SDK_SchemEL";
        public const string NL_SCHEM_KNX = "SDK_SchemKNX";
        public const string NL_KONT_PANEL = "SDK_KontPanel";

        // ---------- Атрибуты ----------
        public const string NATR_NAIMENOVANIE = "НАИМЕНОВАНИЕ";
        public const string NATR_NAZVANIE_PO_SHEME = "НАЗВАНИЕ_ПО_СХЕМЕ";
        public const string NATR_TIP = "ТИП";
        public const string NATR_NAIMENOVANIE_SP = "НАИМЕНОВАНИЕ_СП";
        public const string NATR_TIP_MARKA_SP = "ТИП_МАРКА_СП";
        public const string NATR_KOD_SP = "КОД_СП";
        public const string NATR_IZGOTOVITEL_SP = "ИЗГОТОВИТЕЛЬ_СП";
        public const string NATR_MASSA_SP = "МАССА_СП";
        public const string NATR_ED_IZM_SP = "ЕДИНИЦА_ИЗМЕРЕНИЯ_СП";
        public const string NATR_FAZNOST = "ФАЗНОСТЬ";
        public const string NATR_USTAVKA = "УСТАВКА";
        public const string NATR_NOMER_AVT = "НОМЕР_АВТ";
        public const string NATR_DIFTOK = "ДИФТОК";
        public const string NATR_DLINA = "ДЛИНА";
        public const string NATR_PROVOD = "ПРОВОД";
        public const string NATR_TRUBA = "ТРУБА";
        public const string NATR_FAZA = "ФАЗА";
        public const string NATR_GRUPPA = "ГРУППА";
        public const string NATR_MOSHNOST = "МОЩНОСТЬ";
        public const string NATR_KOSINUS = "КОСИНУС";
        public const string NATR_TOK = "ТОК";
        public const string NATR_POTERI = "ПОТЕРИ";
        public const string NATR_NOMER_KOMNATY = "НОМЕР_КОМНАТЫ";
        public const string NATR_NAZVANIE_PRIEMNIKA = "НАЗВАНИЕ_ПРИЕМНИКА";
        public const string NATR_USTROYSTVO_UPRAVLENIYA = "УСТРОЙСТВО_УПРАВЛЕНИЯ";
        public const string NATR_GRUPPOVOY_ADRES = "ГРУППОВОЙ_АДРЕС";
        public const string NATR_GA_UPRAVLENIYA = "ГА_УПРАВЛЕНИЯ";
        public const string NATR_FIZICHESKIY_ADRES = "ФИЗИЧЕСКИЙ_АДРЕС";
        public const string NATR_POZICIYA_V_SHITE = "ПОЗИЦИЯ_В_ЩИТЕ";
        public const string NATR_SHIT_PIT_AVT = "ЩИТ_ПИТАЮЩЕГО_АВТОМАТА";
        public const string NATR_NOMER_PIT_AVT = "НОМЕР_ПИТАЮЩЕГО_АВТОМАТА";
        public const string NATR_USTAVKA_PIT_AVT = "УСТАВКА_ПИТАЮЩЕГО_АВТОМАТА";
        public const string NATR_NOMER_KANALA = "НОМЕР_КАНАЛА";
        public const string NATR_TIP_NAGRUZKI = "ТИП_НАГРУЗКИ";
        public const string NATR_DPT = "DPT";
        // новые
        public const string NATR_KOEF_SPROSA = "КОЭФ_СПРОСА";
        public const string NATR_USTANOVLENNAYA_MOSHNOST = "УСТАНОВЛЕННАЯ_МОЩНОСТЬ";
        public const string NATR_RASCHETNAYA_MOSHNOST = "РАСЧЕТНАЯ_МОЩНОСТЬ";
        public const string NATR_NAZVANIE_SHITA_PO_SHEME = "НАЗВАНИЕ_ЩИТА_ПО_СХЕМЕ";
        public const string NATR_ADRESA_DALI = "АДРЕСА_DALI";


        // признак вводного автомата (ищется в ТИП / ГРУППА / НАЗВАНИЕ_ПРИЕМНИКА без учёта регистра)
        // public const string NV_VVOD = "ВВОД";


        /// //////////////////////////////////////////////////////////


        // ---------- Новые атрибуты ----------     
        public const string NATR_NOMER_GRUPPY_DALI = "НОМЕР_ГРУППЫ_DALI";    // новый, добавить в блок SDK_АДРЕСА_DALI
        public const string NATR_KOLICHESTVO = "КОЛИЧЕСТВО";           // новый, в SDK_ЛИНИЯ_KNX_ВЫХОД (задание: «мощность, количество»)


        // ---------- Блоки (точное имя) ----------
        public const string NB_OB_SHIT = "SDK_ОБ_ЩИТ";
        public const string NB_AVTOMAT_UNIV = "SDK_АВТОМАТ_УНИВ";
        public const string NB_OB_KNX = "SDK_ОБ_KNX";
        public const string NB_BLOK_KNX = "SDK_БЛОК_KNX";
        public const string NB_LINIYA_KNX_VHOD = "SDK_ЛИНИЯ_KNX_ВХОД";
        public const string NB_LINIYA_KNX_VYHOD = "SDK_ЛИНИЯ_KNX_ВЫХОД";
        public const string NB_KONTEYNER_KNX = "SDK_КОНТЕЙНЕР_KNX";
        public const string NB_ADRESA_DALI = "SDK_АДРЕСА_DALI";     // новый
        public const string NB_SHEMA_SHIT = "SDK_СХЕМА_ЩИТ";       // новый

        // ---------- Блоки по префиксу имени ----------
        public const string NBP_OB_SHITOVOE = "SDK_ОБ_ЩИТОВОЕ";      // все блоки, имя которых начинается так

        // ---------- Префиксы групп AutoCAD (на будущее) ----------
        public const string NGP_EL_KNX = "EL_KNX";
        public const string NGP_PLAN_SCH = "PLAN_SCH";

        // ---------- Наборы ----------
        /// Блоки, которые складываем в SDK_КОНТЕЙНЕР_KNX по попаданию точки вставки в его область (5.1).
        public static readonly string[] ContainerChildBlocks =
        {
            NB_AVTOMAT_UNIV, NB_OB_KNX, NB_BLOK_KNX, NB_LINIYA_KNX_VHOD, NB_LINIYA_KNX_VYHOD, NB_ADRESA_DALI
        };

        /// Спецификационные атрибуты (для блоков SDK_ОБ_ЩИТОВОЕ*).
        public static readonly string[] SpecTags =
        {
            NATR_NAIMENOVANIE_SP, NATR_TIP_MARKA_SP, NATR_KOD_SP, NATR_IZGOTOVITEL_SP, NATR_MASSA_SP, NATR_ED_IZM_SP
        };

        // ---------- Служебные значения ----------
        public const string NV_UNKNOWN = "НЕИЗВЕСТН";
        /// Заглушка «поля нет по смыслу»: ФИЗИЧЕСКИЙ_АДРЕС блока питания, ГРУППОВОЙ_АДРЕС (статус) клавиши без индикации.
        public const string NV_NONE = "НЕТ";
        public static readonly string[] NoneValues = { NV_NONE, "-", "—", "–", "N/A", "Н/Д" };

        public static bool IsNone(string v)
        {
            var s = (v ?? "").Trim();
            if (s.Length == 0) return false;
            foreach (var n in NoneValues) if (string.Equals(s, n, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }


        // ---------- Типы каналов в JSON ----------
        public const string CT_LIGHT = "light", CT_SHUTTER = "shutter", CT_DALI = "dali",
                            CT_SOCKET = "socket", CT_OTHER = "other", CT_INPUT = "input";

        /// Слово в ТИП_НАГРУЗКИ (верхний регистр, поиск подстроки) → тип канала.
        public static readonly KeyValuePair<string, string>[] LoadTypeWords =
        {
            new KeyValuePair<string, string>("DALI",   CT_DALI),
            new KeyValuePair<string, string>("СВЕТ",   CT_LIGHT),
            new KeyValuePair<string, string>("ОСВЕЩ",  CT_LIGHT),
            new KeyValuePair<string, string>("ШТОР",   CT_SHUTTER),
            new KeyValuePair<string, string>("ЖАЛЮЗ",  CT_SHUTTER),
            new KeyValuePair<string, string>("РОЛЬСТ", CT_SHUTTER),
            new KeyValuePair<string, string>("РОЗЕТ",  CT_SOCKET),
        };


        // параметры расчёта
        public const double CALC_U_PHASE = 220.0;
        public const double CALC_U_LINE = 380.0;
        public const double CALC_KC_DEFAULT = 1.0;      // если КОЭФ_СПРОСА на щите пуст
        public const double COS_DEFAULT = 0.95;         // если КОСИНУС пуст и тип нагрузки не распознан
        public static readonly (string key, double cos)[] COS_BY_TYPE =
        {
                ("led", 0.95), ("свет", 0.95), ("освещ", 0.95), ("dali", 0.95),
                ("розет", 0.90), ("штор", 0.70), ("привод", 0.70), ("двиг", 0.75), ("тэн", 1.00), ("нагрев", 1.00),
        };
        public const bool CALC_RENUMBER_ALL = false;  // false — нумеруются только пустые НОМЕР_АВТ
        public const double CALC_ROW_TOLERANCE = 5.0;   // автоматы с |ΔY| < допуска считаются одной строкой
        public const string GRUPPA_FORMAT = "{0}-{1}";  // ГРУППА по умолчанию: <щит>-<номер автомата>


        // --- Расчёт щита ---------- //
        public const string NV_REZERV = "резерв";   // признак автомата-резерва в НАЗВАНИЕ_ПРИЕМНИКА       
        public const string NP_NOMER_AVT = "Q";     // префикс номера автомата: Q1, Q2 …        
        public const double CALC_MIN_POWER_KW = 0.1;  // минимум мощности автомата со связью       
        public const bool CALC_DECIMAL_COMMA = true;   // писать 0,85, а не 0.85
        public const bool CALC_CHANNELS_PER_TYPE = true; // входы и выходы нумеруются в контейнере отдельно


        // ---------- Проверка проекта: SDK_CHECK_PROJECT ----------
        /// Типы устройств KNX из задания. Артикул ищется подстрокой без учёта регистра
        /// в ТИП / ТИП_МАРКА_СП / НАИМЕНОВАНИЕ / НАИМЕНОВАНИЕ_СП / КОД_СП блока SDK_ОБ_KNX внутри контейнера.
        /// kind: relay — релейные выходы; shutter — шторные; dali — шлюз (outputs = макс. групп DALI); input — входы; psu — блок питания.
        public const string DK_RELAY = "relay", DK_SHUTTER = "shutter", DK_DALI = "dali", DK_INPUT = "input", DK_PSU = "psu";
        public static readonly (string article, string kind, int outputs, int inputs)[] KNX_DEVICE_TYPES =
        {
            ("ZIOMB8V4",   DK_RELAY,    8,  0),
            ("ZIOMBSH4V3", DK_SHUTTER,  4,  0),
            ("ZDID64V3",   DK_DALI,    16,  0),
            ("ZIORQ12",    DK_INPUT,    0, 12),
            ("ZPSU640",    DK_PSU,      0,  0),
        };

        // ---------- Проверка проекта: SDK_CHECK_PROJECT ----------
        // Тип устройства определяется по составу контейнера: есть SDK_АДРЕСА_DALI — шлюз DALI; есть ВЫХОД — модуль выходов;
        // есть ВХОД — модуль входов; каналов нет — устройство без каналов (блок питания и т.п.). Артикулы не разбираются.
        public const int CHECK_KNX_AREA_MAX = 15, CHECK_KNX_LINE_MAX = 15, CHECK_KNX_DEVICE_MAX = 255;
        public const int CHECK_GA_MAIN_MAX = 31, CHECK_GA_MIDDLE_MAX = 7, CHECK_GA_SUB_MAX = 255;
        public const int CHECK_DEVICES_PER_LINE_MAX = 64;
        public const int CHECK_DALI_ADDR_MAX = 63, CHECK_DALI_GROUP_MAX = 15;
        public const double CHECK_OVERSIZE_FACTOR = 2.0;   // уставка > 2·I — «автомат завышен»
        public const double CHECK_OVERSIZE_MIN_I = 3.0;    // …но только если I ≥ 3 А
        public const int CHECK_LABEL_MAX = 40;             // длина имени устройства в подписях

        ///////////////////////////////////////////////////////////////////////////////////////////////////////

        // ---------- Спецификация: SDK_SPEC ----------
        public const string SPEC_KEY_VYKL_NAGR = "ВЫКЛ_НАГР";     // подстрока в имени блока: выключатель нагрузки
        public const string SPEC_KEY_AVT_GR = "АВТ_ГР";        // подстрока в имени блока: групповой автомат
        public const string SPEC_KEY_PSU = "блок питания";  // подстрока в НАИМЕНОВАНИЕ_СП (регистр/пробелы не важны): первым в разделе KNX
        public const string SPEC_SECTION_1 = "I Щитовое оборудование";
        public const string SPEC_SECTION_2 = "II Кабельная продукция";
        public const string SPEC_SECTION_3 = "III Трубы";
        public const string SPEC_SUB_POWER = "Модульное силовое электрооборудование";
        public const string SPEC_SUB_KNX = "Оборудование KNX";
        public const string SPEC_PANEL_PREFIX = "Щит ";
        public const string SPEC_UNIT_PCS = "шт.";
        public const string SPEC_UNIT_M = "м";
        public const string SPEC_FILE_SUFFIX = "_Спецификация";
        public static readonly string[] SPEC_HEADER = { "Поз", "Наименование", "Марка", "Код", "Завод", "Единиц", "Количество", "Примечание" };
        public static readonly double[] SPEC_COL_WIDTHS = { 9, 62, 22, 14, 20, 8, 12, 22 };   // ширина колонок Excel, символов

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }
}
