using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Model;

namespace KnxTools.Check
{
    /// Устройство KNX = контейнер + его дети, разложенные по ролям. Тип — по составу, артикулы не читаются.
    public sealed class KnxDevice
    {
        public PanelScope Scope;
        public KonteynerKnxBlock Container;
        public ObKnxBlock Ob;         // графика + спецификация: отсюда берём имя для подписей
        public BlokKnxBlock Blok;     // адрес и питание
        public readonly List<LiniyaKnxVyhodBlock> Outputs = new List<LiniyaKnxVyhodBlock>();
        public readonly List<LiniyaKnxVhodBlock> Inputs = new List<LiniyaKnxVhodBlock>();
        public readonly List<AdresaDaliBlock> Dali = new List<AdresaDaliBlock>();

        public string Name = "";      // ТИП или ТИП_МАРКА_СП из SDK_ОБ_KNX, обрезано до CHECK_LABEL_MAX
        public string AddrText = "";
        public PhysAddr Addr;         // null — не задан / НЕТ / не читается

        public bool HasChannels => Outputs.Count + Inputs.Count + Dali.Count > 0;
        public bool IsDaliGateway => Dali.Count > 0;
       
        

        public string Kind
        {
            get
            {
                if (Dali.Count > 0) return "шлюз DALI";
                if (Outputs.Count > 0) return Inputs.Count > 0 ? "модуль выходов/входов" : "модуль выходов";
                if (Inputs.Count > 0) return "модуль входов";
                return "без каналов";
            }
        }

        public string Label
        {
            get
            {
                string t = Name.Length > 0 ? Name : Kind;
                if (Addr != null) t += " " + Addr.Key;
                return t + " <" + Container.Handle + ">";
            }
        }

        public HashSet<string> Handles
        {
            get
            {
                var h = new HashSet<string> { Container.Handle };
                foreach (var c in Container.Children) h.Add(c.Handle);
                return h;
            }
        }

        public int UsedOutputs => Outputs.Count(o => CheckUtil.Filled(CheckUtil.Val(o, Names.NATR_GRUPPOVOY_ADRES)));
        public int UsedInputs => Inputs.Count(i => CheckUtil.Filled(CheckUtil.Val(i, Names.NATR_GA_UPRAVLENIYA)));
    }

    /// Группа AutoCAD EL_KNX*: автомат(ы) + потребители (блоки с ЩИТ_ПИТАЮЩЕГО_АВТОМАТА).
    /// SDK_СХЕМА_ЩИТ в группе допустим, но не учитывается. Остальное игнорируется (Ignored).
    public sealed class PowerLink
    {
        public string GroupName = "";
        public readonly List<AvtomatUnivBlock> Breakers = new List<AvtomatUnivBlock>();
        public readonly List<BlockEntity> Devices = new List<BlockEntity>();
        public int Ignored;
        public bool IsValid => Breakers.Count == 1;
        public AvtomatUnivBlock Breaker => IsValid ? Breakers[0] : null;
    }

    /// Всё, что нужно проверкам, собранное один раз. Только чтение.
    public sealed class CheckContext
    {
        public DrawingModel Model { get; private set; }
        public readonly List<KnxDevice> Devices = new List<KnxDevice>();
        public readonly List<PowerLink> Links = new List<PowerLink>();
        public readonly List<AvtomatUnivBlock> Breakers = new List<AvtomatUnivBlock>();
        public readonly List<LiniyaKnxVyhodBlock> Outputs = new List<LiniyaKnxVyhodBlock>();
        public readonly List<LiniyaKnxVhodBlock> Inputs = new List<LiniyaKnxVhodBlock>();
        public readonly List<AdresaDaliBlock> Dali = new List<AdresaDaliBlock>();
        public readonly List<BlockEntity> LooseChannels = new List<BlockEntity>();
        public readonly List<BlockEntity> PowerConsumers = new List<BlockEntity>();    // без автоматов, контейнеров и SDK_СХЕМА_ЩИТ
        public readonly HashSet<string> ListenerKeys = new HashSet<string>();
        public readonly HashSet<string> SenderKeys = new HashSet<string>();
        public int GroupsTotal, GroupsOurs;

        private readonly Dictionary<ObjectId, BlockEntity> byId = new Dictionary<ObjectId, BlockEntity>();
        private readonly Dictionary<ObjectId, PanelScope> scopeOf = new Dictionary<ObjectId, PanelScope>();
        private readonly Dictionary<ObjectId, KnxDevice> deviceOf = new Dictionary<ObjectId, KnxDevice>();
        private readonly Dictionary<ObjectId, List<PowerLink>> linksOf = new Dictionary<ObjectId, List<PowerLink>>();
        private static readonly List<PowerLink> NoLinks = new List<PowerLink>();

        public PanelScope ScopeOf(BlockEntity b) => b != null && scopeOf.TryGetValue(b.Id, out var s) ? s : null;
        public KnxDevice DeviceOf(BlockEntity b) => b != null && deviceOf.TryGetValue(b.Id, out var d) ? d : null;
        public List<PowerLink> LinksOf(BlockEntity b) => b != null && linksOf.TryGetValue(b.Id, out var l) ? l : NoLinks;
        public IEnumerable<BlockEntity> AllBlocks => byId.Values;

        public static CheckContext Build(Database db, Transaction tr, DrawingModel model)
        {
            var ctx = new CheckContext { Model = model };

            foreach (var s in model.Scopes)
            {
                foreach (var b in s.AllBlocks()) ctx.Register(b, s);
                ctx.Breakers.AddRange(s.Panel.Avtomats);
                foreach (var c in s.Containers) ctx.Devices.Add(ctx.MakeDevice(c, s));
                foreach (var b in s.LooseBlocks) if (IsChannel(b)) ctx.LooseChannels.Add(b);
            }
            foreach (var b in model.OutsideBlocks)
            {
                ctx.Register(b, null);
                if (IsChannel(b)) ctx.LooseChannels.Add(b);
                if (b is AvtomatUnivBlock a) ctx.Breakers.Add(a);
            }

            foreach (var b in ctx.byId.Values)
                if (IsPowerConsumer(b)) ctx.PowerConsumers.Add(b);

            foreach (var o in ctx.Outputs) AddKeys(ctx.ListenerKeys, CheckUtil.Val(o, Names.NATR_GRUPPOVOY_ADRES));
            foreach (var d in ctx.Dali) AddKeys(ctx.ListenerKeys, CheckUtil.Val(d, Names.NATR_GRUPPOVOY_ADRES));
            foreach (var i in ctx.Inputs) AddKeys(ctx.SenderKeys, CheckUtil.Val(i, Names.NATR_GA_UPRAVLENIYA));

            ctx.ReadGroups(db, tr);
            return ctx;
        }

        private void Register(BlockEntity b, PanelScope s)
        {
            if (b == null || byId.ContainsKey(b.Id)) return;
            byId[b.Id] = b;
            if (s != null) scopeOf[b.Id] = s;
        }

        private static bool IsChannel(BlockEntity b)
            => b is LiniyaKnxVyhodBlock || b is LiniyaKnxVhodBlock || b is AdresaDaliBlock;

        /// Потребитель 230 В для проверки питания: любой наш блок с ЩИТ_ПИТАЮЩЕГО_АВТОМАТА,
        /// кроме автомата, контейнера и SDK_СХЕМА_ЩИТ (питание щита от щита — дело SDK_CALC_PANEL).
        private static bool IsPowerConsumer(BlockEntity b)
            => !(b is AvtomatUnivBlock) && !(b is KonteynerKnxBlock) && !(b is ShemaShitBlock) && b.Has(Names.NATR_SHIT_PIT_AVT);

        /// Блоки, которые в группе EL_KNX* допустимы, но ни на что не влияют и в Ignored не считаются.
        private static bool IsSilentInGroup(BlockEntity b) => b is ShemaShitBlock;

        private static void AddKeys(HashSet<string> set, string gaList)
        {
            if (!CheckUtil.Filled(gaList)) return;
            foreach (var part in CheckUtil.SplitList(gaList))
            {
                string k = CheckUtil.GaKey(part);
                if (k != null) set.Add(k);
            }
        }

        private KnxDevice MakeDevice(KonteynerKnxBlock c, PanelScope s)
        {
            var d = new KnxDevice { Scope = s, Container = c };
            deviceOf[c.Id] = d;
            foreach (var ch in c.Children)
            {
                deviceOf[ch.Id] = d;
                if (d.Ob == null && ch is ObKnxBlock ob) d.Ob = ob;
                else if (d.Blok == null && ch is BlokKnxBlock bk) d.Blok = bk;
                else if (ch is LiniyaKnxVyhodBlock o) { d.Outputs.Add(o); Outputs.Add(o); }
                else if (ch is LiniyaKnxVhodBlock i) { d.Inputs.Add(i); Inputs.Add(i); }
                else if (ch is AdresaDaliBlock da) { d.Dali.Add(da); Dali.Add(da); }
            }

            if (d.Ob != null)
            {
                string n = CheckUtil.Val(d.Ob, Names.NATR_TIP);
                if (!CheckUtil.Filled(n)) n = CheckUtil.Val(d.Ob, Names.NATR_TIP_MARKA_SP);
                if (CheckUtil.Filled(n))
                    d.Name = n.Length > Names.CHECK_LABEL_MAX ? n.Substring(0, Names.CHECK_LABEL_MAX).TrimEnd() + "…" : n;
            }

            if (d.Blok != null)
            {
                d.AddrText = CheckUtil.Val(d.Blok, Names.NATR_FIZICHESKIY_ADRES);
                if (CheckUtil.Filled(d.AddrText) && CheckUtil.ParsePhys(d.AddrText, out var a, out _)) d.Addr = a;
            }
            return d;
        }

        /// Группы AutoCAD с префиксом EL_KNX. Членство собирается с двух сторон:
        /// из списка группы и из реакторов каждого нашего блока — так не теряются члены, которых список группы не отдаёт.
        /// SDK_СХЕМА_ЩИТ в группе пропускается молча.
        private void ReadGroups(Database db, Transaction tr)
        {
            var byGroup = new Dictionary<ObjectId, PowerLink>();

            var gd = (DBDictionary)tr.GetObject(db.GroupDictionaryId, OpenMode.ForRead);
            foreach (DBDictionaryEntry e in gd)
            {
                Group g;
                try { g = tr.GetObject(e.Value, OpenMode.ForRead) as Group; } catch { continue; }
                if (g == null) continue;
                GroupsTotal++;
                if (!IsOurs(g)) continue;
                GroupsOurs++;

                var link = new PowerLink { GroupName = g.Name };
                byGroup[g.ObjectId] = link;
                Links.Add(link);

                ObjectId[] ids;
                try { ids = g.GetAllEntityIds(); } catch { ids = new ObjectId[0]; }
                foreach (var id in ids)
                {
                    if (id.IsNull || id.IsErased || !byId.TryGetValue(id, out var b)) { link.Ignored++; continue; }
                    if (IsSilentInGroup(b)) continue;
                    if (!Join(link, b)) link.Ignored++;
                }
            }

            // обратный ход: от объекта к его группам
            foreach (var b in byId.Values.ToList())
            {
                if (!(b is AvtomatUnivBlock) && !IsPowerConsumer(b)) continue;
                Entity ent;
                try { ent = tr.GetObject(b.Id, OpenMode.ForRead) as Entity; } catch { continue; }
                var reactors = ent?.GetPersistentReactorIds();
                if (reactors == null) continue;
                foreach (ObjectId rid in reactors)
                {
                    Group g;
                    try { g = tr.GetObject(rid, OpenMode.ForRead) as Group; } catch { continue; }
                    if (g == null || !IsOurs(g)) continue;
                    if (!byGroup.TryGetValue(g.ObjectId, out var link))
                    {
                        link = new PowerLink { GroupName = g.Name };
                        byGroup[g.ObjectId] = link; Links.Add(link); GroupsOurs++;
                    }
                    Join(link, b);
                }
            }
        }

        private static bool IsOurs(Group g)
            => g.Name.StartsWith(Names.NGP_EL_KNX, StringComparison.OrdinalIgnoreCase);

        /// Блок → связь (без дубликатов) и в обратный индекс. false — блок для связи не нужен.
        private bool Join(PowerLink link, BlockEntity b)
        {
            if (b is AvtomatUnivBlock a) { if (!link.Breakers.Contains(a)) link.Breakers.Add(a); }
            else if (IsPowerConsumer(b)) { if (!link.Devices.Contains(b)) link.Devices.Add(b); }
            else return false;

            if (!linksOf.TryGetValue(b.Id, out var list)) linksOf[b.Id] = list = new List<PowerLink>();
            if (!list.Contains(link)) list.Add(link);
            return true;
        }
    }
}
