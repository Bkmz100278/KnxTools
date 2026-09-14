using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using KnxTools.Geometry;

namespace KnxTools.Model
{
    /// Базовая сущность «вставка блока». Читает атрибуты по списку ожидаемых тегов наследника.
    public abstract class BlockEntity
    {
        public ObjectId Id { get; private set; }
        public string Handle { get; private set; } = "";
        public string BlockName { get; private set; } = "";
        public string Layer { get; private set; } = "";
        public Point3d Position { get; private set; }     // точка вставки — по ней определяем принадлежность
        public object Parent { get; set; }                 // контейнер / щит / рамка — ставится при сборке

        public abstract string[] ExpectedTags { get; }
        public virtual string DisplayKey => Handle;        // чем подписывать в отчёте

        public Dictionary<string, BlockAttr> Attrs { get; } = new Dictionary<string, BlockAttr>(StringComparer.OrdinalIgnoreCase);
        /// Атрибуты, которые есть в блоке, но не описаны в ТЗ — в отчёт, чтобы поправить константы.
        public Dictionary<string, string> ExtraAttrs { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string Get(string tag) => Attrs.TryGetValue(tag, out var a) ? a.Value : "";

        public Dictionary<string, BlockAttr> AllAttrs { get; } = new Dictionary<string, BlockAttr>(StringComparer.OrdinalIgnoreCase);

        public bool Has(string tag) => AllAttrs.ContainsKey(tag);
        public string GetAny(string tag) => AllAttrs.TryGetValue(tag, out var a) ? a.Value : "";

        public bool Set(string tag, string value, Transaction tr, Report report, string where)
        {
            if (!AllAttrs.TryGetValue(tag, out var a))
            {
                report.Warn(where, $"в блоке {BlockName} нет атрибута {tag} — «{value}» не записано", Handle);
                return false;
            }
            return a.Write(value, tr, report, where);
        }

        public void Read(BlockReference br, string effectiveName, Transaction tr, Report report)
        {
            Id = br.ObjectId;
            Handle = br.Handle.ToString();
            BlockName = effectiveName;
            Layer = br.Layer;
            Position = br.Position;

            // 1. Все фактические атрибуты вставки
            var actual = new Dictionary<string, KeyValuePair<ObjectId, string>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (ObjectId attId in br.AttributeCollection)
                {
                    var ar = tr.GetObject(attId, OpenMode.ForRead) as AttributeReference;
                    if (ar == null) continue;
                    string tag = (ar.Tag ?? "").Trim();
                    if (actual.ContainsKey(tag))
                        report.Warn($"блок {BlockName}", $"тег {tag} встречается дважды, взято первое значение", Handle);
                    else
                        actual[tag] = new KeyValuePair<ObjectId, string>(attId, ar.TextString);
                }
            }
            catch (System.Exception ex)
            {
                report.Error($"блок {BlockName}", "не удалось прочитать атрибуты: " + ex.Message, Handle);
            }

            // 2. Ожидаемые → сущности BlockAttr
            foreach (var tag in ExpectedTags)
            {
                if (Attrs.ContainsKey(tag)) continue;                 // дубль в списке ТЗ (НОМЕР_КОМНАТЫ) — игнорируем
                var a = new BlockAttr(tag, this);
                a.ReadFrom(actual);
                Attrs[tag] = a;
                if (!a.IsFound) report.Warn($"блок {BlockName}", $"атрибут {tag} отсутствует во вставке", Handle);
            }

            foreach (var kv in actual)
            {
                var a = new BlockAttr(kv.Key, this);
                a.ReadFrom(actual);
                AllAttrs[kv.Key] = a;
            }

            // 3. Лишние
            foreach (var kv in actual)
                if (!Attrs.ContainsKey(kv.Key)) ExtraAttrs[kv.Key] = kv.Value.Value;
            //if (ExtraAttrs.Count > 0)
            //    report.Info($"блок {BlockName}", "атрибуты вне описания: " + string.Join(", ", ExtraAttrs.Keys), Handle);

            ReadGeometry(br, tr, report);
        }

        protected virtual void ReadGeometry(BlockReference br, Transaction tr, Report report) { }
    }

    // ---------------- 3.1.1 SDK_ОБ_ЩИТ ----------------
    public class ObShitBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_NAIMENOVANIE, Names.NATR_NAZVANIE_PO_SHEME, Names.NATR_TIP,
            Names.NATR_NAIMENOVANIE_SP, Names.NATR_TIP_MARKA_SP, Names.NATR_KOD_SP,
            Names.NATR_IZGOTOVITEL_SP, Names.NATR_MASSA_SP, Names.NATR_ED_IZM_SP
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_NAZVANIE_PO_SHEME);
    }

    // ---------------- 3.1.2 SDK_АВТОМАТ_УНИВ ----------------
    public class AvtomatUnivBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_NOMER_AVT, Names.NATR_USTAVKA, Names.NATR_DLINA, Names.NATR_PROVOD, Names.NATR_TRUBA,
            Names.NATR_GRUPPA, Names.NATR_MOSHNOST, Names.NATR_KOSINUS, Names.NATR_TOK,
            Names.NATR_NOMER_KOMNATY, Names.NATR_NAZVANIE_PRIEMNIKA
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_NOMER_AVT);
    }

    // ---------------- 3.1.3 SDK_ОБ_KNX ----------------
    public class ObKnxBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_TIP, Names.NATR_NAIMENOVANIE, Names.NATR_NAIMENOVANIE_SP, Names.NATR_TIP_MARKA_SP,
            Names.NATR_KOD_SP, Names.NATR_IZGOTOVITEL_SP, Names.NATR_MASSA_SP, Names.NATR_ED_IZM_SP
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_NAIMENOVANIE);
    }

    // ---------------- 3.1.4 SDK_БЛОК_KNX ----------------
    public class BlokKnxBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_POZICIYA_V_SHITE, Names.NATR_FIZICHESKIY_ADRES, Names.NATR_SHIT_PIT_AVT,
            Names.NATR_NOMER_PIT_AVT, Names.NATR_USTAVKA_PIT_AVT, Names.NATR_PROVOD
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_FIZICHESKIY_ADRES);
    }

    // ---------------- 3.1.5 SDK_ЛИНИЯ_KNX_ВХОД ----------------
    public class LiniyaKnxVhodBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_NOMER_KANALA, Names.NATR_USTROYSTVO_UPRAVLENIYA, Names.NATR_GRUPPOVOY_ADRES, Names.NATR_GA_UPRAVLENIYA,
            Names.NATR_DLINA, Names.NATR_PROVOD, Names.NATR_TRUBA
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_GRUPPOVOY_ADRES);
    }

    // ---------------- 3.1.6 SDK_ЛИНИЯ_KNX_ВЫХОД ----------------
    public class LiniyaKnxVyhodBlock : BlockEntity
    {
        public static readonly string[] Tags =
{
            Names.NATR_NOMER_KANALA, Names.NATR_GRUPPOVOY_ADRES, Names.NATR_TIP_NAGRUZKI, Names.NATR_DPT,
            Names.NATR_NOMER_KOMNATY, Names.NATR_GRUPPA, Names.NATR_NAZVANIE_PRIEMNIKA,
            // питание нагрузки канала — от автомата, отличного от питания самого модуля
            Names.NATR_SHIT_PIT_AVT, Names.NATR_NOMER_PIT_AVT,
            // отходящий кабель канал → нагрузка (задание: «марка и сечение» для кабельных линий)
            Names.NATR_PROVOD, Names.NATR_DLINA,
            // нагрузка (задание: «мощность, количество»)
            Names.NATR_MOSHNOST
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_NOMER_KANALA);
    }

    // ---------------- 3.1.7 SDK_АДРЕСА_DALI (новый) ----------------
    public class AdresaDaliBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_GRUPPOVOY_ADRES, Names.NATR_ADRESA_DALI, Names.NATR_NAZVANIE_PRIEMNIKA,
            Names.NATR_NOMER_KOMNATY, Names.NATR_SHIT_PIT_AVT, Names.NATR_GRUPPA,
            Names.NATR_DPT, Names.NATR_NOMER_GRUPPY_DALI
        };

        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_GRUPPOVOY_ADRES);
    }

    // ---------------- 3.1.8 SDK_СХЕМА_ЩИТ (новый) ----------------
    public class ShemaShitBlock : BlockEntity
    {
        public static readonly string[] Tags =
        {
            Names.NATR_USTANOVLENNAYA_MOSHNOST, Names.NATR_KOEF_SPROSA, Names.NATR_RASCHETNAYA_MOSHNOST,
            Names.NATR_KOSINUS, Names.NATR_TOK, Names.NATR_GRUPPA, Names.NATR_SHIT_PIT_AVT, Names.NATR_PROVOD
        };
        public override string[] ExpectedTags => Tags;
        public override string DisplayKey => Get(Names.NATR_GRUPPA);
    }

    // ---------------- 3.1.9 SDK_ОБ_ЩИТОВОЕ* (по префиксу) ----------------
    public class ObShitovoeBlock : BlockEntity
    {
        public override string[] ExpectedTags => Names.SpecTags;
        public override string DisplayKey => Get(Names.NATR_NAIMENOVANIE_SP);
    }

    // ---------------- 5.1 SDK_КОНТЕЙНЕР_KNX ----------------
    public class KonteynerKnxBlock : BlockEntity
    {
        public override string[] ExpectedTags => new string[0];

        public Polygon2d Area { get; private set; }
        public string AreaSource { get; private set; } = "";
        public List<BlockEntity> Children { get; } = new List<BlockEntity>();

        public bool Contains(Point3d p) => Area != null && Area.Contains(p);

        /// Область = полилиния внутри определения динамического блока (в текущем растянутом состоянии),
        /// перенесённая в мировые координаты. Если полилинии нет — габариты вставки.
        protected override void ReadGeometry(BlockReference br, Transaction tr, Report report)
        {
            try
            {
                var btr = (BlockTableRecord)tr.GetObject(br.BlockTableRecord, OpenMode.ForRead);
                Polyline best = null;
                foreach (ObjectId id in btr)
                {
                    var pl = tr.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (pl == null) continue;
                    if (best == null || (pl.Closed && !best.Closed)) best = pl;
                }
                if (best != null)
                {
                    Area = Polygon2d.FromPolyline(best, br.BlockTransform);
                    AreaSource = "полилиния блока";
                    if (!best.Closed) report.Info("контейнер", "полилиния контейнера не замкнута, замкнута условно", Handle);
                }
                else
                {
                    Area = Polygon2d.FromExtents(br.GeometricExtents);
                    AreaSource = "габариты вставки";
                    report.Warn("контейнер", "в блоке нет полилинии, область взята по габаритам", Handle);
                }
            }
            catch (System.Exception ex)
            {
                try { Area = Polygon2d.FromExtents(br.GeometricExtents); AreaSource = "габариты (после ошибки)"; }
                catch { Area = null; }
                report.Error("контейнер", "ошибка определения области: " + ex.Message, Handle);
            }
            if (Area == null) report.Error("контейнер", "область не определена — дети не будут привязаны", Handle);
        }
    }

    /// Имя блока → сущность. Сначала точные имена, потом префиксы. Неизвестные → null (блок не наш).
    public static class BlockFactory
    {
        public static BlockEntity Create(string blockName)
        {
            if (Eq(blockName, Names.NB_OB_SHIT)) return new ObShitBlock();
            if (Eq(blockName, Names.NB_AVTOMAT_UNIV)) return new AvtomatUnivBlock();
            if (Eq(blockName, Names.NB_OB_KNX)) return new ObKnxBlock();
            if (Eq(blockName, Names.NB_BLOK_KNX)) return new BlokKnxBlock();
            if (Eq(blockName, Names.NB_LINIYA_KNX_VHOD)) return new LiniyaKnxVhodBlock();
            if (Eq(blockName, Names.NB_LINIYA_KNX_VYHOD)) return new LiniyaKnxVyhodBlock();
            if (Eq(blockName, Names.NB_KONTEYNER_KNX)) return new KonteynerKnxBlock();
            if (Eq(blockName, Names.NB_ADRESA_DALI)) return new AdresaDaliBlock();
            if (Eq(blockName, Names.NB_SHEMA_SHIT)) return new ShemaShitBlock();
            if (Starts(blockName, Names.NBP_OB_SHITOVOE)) return new ObShitovoeBlock();
            return null;
        }

        public static bool IsContainerChild(BlockEntity b)
        {
            foreach (var n in Names.ContainerChildBlocks) if (Eq(b.BlockName, n)) return true;
            return false;
        }

        private static bool Eq(string a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
        private static bool Starts(string a, string prefix) => (a ?? "").Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
