using System;
using System.Collections.Generic;
using System.Linq;
using KnxTools.Model;

namespace KnxTools.Json
{
    public static class JsonBuilder
    {
        const string W = "JSON";
        // Роли атрибутов входа. Если у вас наоборот — поменять местами здесь.
        const string ATTR_INPUT_TARGET_GA = Names.NATR_GA_UPRAVLENIYA;   // куда вход пишет (ГА актуатора), обязателен
        const string ATTR_INPUT_STATUS_GA = Names.NATR_GRUPPOVOY_ADRES;  // ГА статуса для индикатора клавиши; «НЕТ» — индикации нет

        public static KnxExport Build(DrawingModel m, string drawingName)
        {
            var r = m.Report;
            var x = new KnxExport { Drawing = drawingName ?? "", Generated = DateTime.Now.ToString("s") };
            var book = new Dictionary<string, GroupAddressDto>();            // text → запись книги ГА
            var gaOwner = new Dictionary<string, string>();                  // text → handle первого объявившего (для отчёта)
            var statusGas = new HashSet<string>();                           // ГА, объявленные как статусные (их отправитель — актуатор)
            var controlled = new HashSet<string>();                          // ГА, в которые пишет хотя бы один вход
            var paOwners = new Dictionary<string, string>();                 // PA → handle (дубли)
            var toResolve = new List<Tuple<List<GaRefDto>, string>>();       // ГА входов: DPT проставим из книги в конце

            foreach (var s in m.Scopes)
            {
                string panel = string.IsNullOrWhiteSpace(s.Panel.Name) ? "ЩИТ<" + s.Handle + ">" : s.Panel.Name.Trim();

                foreach (var b in s.LooseBlocks.Where(b => b is BlokKnxBlock || b is LiniyaKnxVyhodBlock || b is LiniyaKnxVhodBlock || b is AdresaDaliBlock))
                    r.Error(W, $"блок {b.BlockName} в рамке, но вне контейнера — в JSON не попал", b.Handle);

                foreach (var cont in s.Containers)
                {
                    var sp = cont.Children.OfType<ObKnxBlock>().FirstOrDefault();
                    var d = cont.Children.OfType<BlokKnxBlock>().FirstOrDefault();
                    if (sp == null) r.Error(W, $"контейнер без {Names.NB_OB_KNX} — нет производителя/артикула", cont.Handle);
                    if (d == null) r.Error(W, $"контейнер без {Names.NB_BLOK_KNX} — устройство без физического адреса", cont.Handle);
                    if (cont.Children.OfType<BlokKnxBlock>().Count() > 1) r.Error(W, $"в контейнере несколько {Names.NB_BLOK_KNX} — одно устройство на контейнер", cont.Handle);

                    // ---- PA: число | «НЕТ» (пассивное устройство) | пусто | мусор
                    PaDto pa = null;
                    bool paNone = false, paBad = false;
                    if (d != null)
                    {
                        string paRaw = d.Get(Names.NATR_FIZICHESKIY_ADRES);
                        if (Names.IsNone(paRaw)) paNone = true;
                        else
                        {
                            pa = KnxAddr.ParsePa(paRaw, out var err);
                            if (err != null) { paBad = true; r.Error(W, err, d.Handle); }
                        }
                    }
                    string devId = pa != null ? pa.Text : "NOPA<" + cont.Handle + ">";
                    if (pa != null)
                    {
                        if (paOwners.TryGetValue(pa.Text, out var other)) r.Error(W, $"дублирующийся PA {pa.Text}: <{other}> и <{d.Handle}>", d.Handle);
                        else paOwners[pa.Text] = d.Handle;
                    }

                    var dev = new DeviceDto
                    {
                        Pa = pa,
                        Manufacturer = sp != null ? sp.Get(Names.NATR_IZGOTOVITEL_SP) : "",
                        Article = sp != null ? Or(sp.Get(Names.NATR_KOD_SP), sp.Get(Names.NATR_TIP_MARKA_SP)) : "",
                        Name = sp != null ? sp.Get(Names.NATR_NAIMENOVANIE) : "",
                        DeviceType = sp != null ? sp.Get(Names.NATR_TIP) : "",
                        Panel = panel,
                        Position = d != null ? d.Get(Names.NATR_POZICIYA_V_SHITE) : "",
                        Handle = cont.Handle
                    };
                    if (sp != null && dev.Article.Length == 0)
                    {
                        dev.Article = Names.NV_UNKNOWN;
                        r.Warn(W, $"{devId}: пустые КОД_СП и ТИП_МАРКА_СП — артикул «{Names.NV_UNKNOWN}»", sp.Handle);
                    }
                    string devName = dev.Name.Trim().Length > 0 ? dev.Name.Trim() : devId;

                    // ---- выходы
                    foreach (var o in cont.Children.OfType<LiniyaKnxVyhodBlock>().OrderBy(o => IntOrNull(o.Get(Names.NATR_NOMER_KANALA)) ?? int.MaxValue))
                    {
                        int? no = IntOrNull(o.Get(Names.NATR_NOMER_KANALA));
                        string who = $"{devId} выход «{o.Get(Names.NATR_NOMER_KANALA)}»";
                        if (no == null) r.Error(W, $"{who}: НОМЕР_КАНАЛА не число", o.Handle);

                        var errs = new List<string>();
                        var ga = KnxAddr.ParseGaDpt(o.Get(Names.NATR_GRUPPOVOY_ADRES), o.Get(Names.NATR_DPT), errs);
                        foreach (var e in errs) r.Error(W, $"{who}: {e}", o.Handle);
                        if (ga.Count == 0) r.Error(W, $"{who}: без группового адреса", o.Handle);

                        string load = o.Get(Names.NATR_NAZVANIE_PRIEMNIKA), room = o.Get(Names.NATR_NOMER_KOMNATY);
                        if (load.Trim().Length == 0) r.Error(W, $"{who}: нагрузка не указана", o.Handle);
                        Register(book, gaOwner, ga, (load + " " + room).Trim(), who, o.Handle, r);

                        dev.Outputs.Add(new OutputDto { No = no, LoadType = o.Get(Names.NATR_TIP_NAGRUZKI), Load = load, Room = room, Ga = Refs(ga) });
                    }
                    foreach (var d1 in dev.Outputs.GroupBy(o => o.No).Where(g1 => g1.Count() > 1))
                        r.Error(W, $"{devId}: номер выхода {d1.Key} повторяется {d1.Count()} раз", cont.Handle);

                    // ---- входы
                    int n = 0;
                    foreach (var i in cont.Children.OfType<LiniyaKnxVhodBlock>())
                    {
                        n++;
                        int? no = IntOrNull(i.Get(Names.NATR_NOMER_KANALA));
                        if (no == null) { no = n; r.Warn(W, $"{devId} вход: нет НОМЕР_КАНАЛА, присвоен {n} по порядку", i.Handle); }
                        string who = $"{devId} вход {no}";
                        string ctlDev = i.Get(Names.NATR_USTROYSTVO_UPRAVLENIYA);

                        var errs = new List<string>();
                        var ctl = KnxAddr.ParseGaDpt(i.Get(ATTR_INPUT_TARGET_GA), "", errs);       // DPT — из книги, позже
                        if (ctl.Count == 0) r.Error(W, $"{who}: не указано, каким ГА управляет ({ATTR_INPUT_TARGET_GA})", i.Handle);

                        // статус: «НЕТ» — индикации нет, тишина; пусто — подсказка; иначе разбираем
                        string stRaw = i.Get(ATTR_INPUT_STATUS_GA);
                        var st = new List<GroupAddressDto>();
                        if (Names.IsNone(stRaw)) { /* осознанно без статуса */ }
                        else if (stRaw.Trim().Length == 0)
                            r.Info(W, $"{who}: {ATTR_INPUT_STATUS_GA} пуст — если индикации у клавиши нет, напишите «{Names.NV_NONE}»", i.Handle);
                        else
                            st = KnxAddr.ParseGaDpt(stRaw, i.Get(Names.NATR_DPT), errs);
                        foreach (var e in errs) r.Error(W, $"{who}: {e}", i.Handle);

                        Register(book, gaOwner, st, ("статус " + ctlDev).Trim(), who, i.Handle, r);
                        foreach (var g1 in st) statusGas.Add(g1.Text);

                        var ctlRefs = Refs(ctl);
                        toResolve.Add(Tuple.Create(ctlRefs, i.Handle));
                        foreach (var g1 in ctl) controlled.Add(g1.Text);

                        dev.Inputs.Add(new InputDto
                        {
                            No = no,
                            ControlDevice = ctlDev,
                            Room = i.Get(Names.NATR_NOMER_KOMNATY),
                            GaControls = ctlRefs,
                            GaStatus = Refs(st)
                        });
                    }
                    foreach (var d1 in dev.Inputs.GroupBy(o => o.No).Where(g1 => g1.Count() > 1))
                        r.Error(W, $"{devId}: номер входа {d1.Key} повторяется {d1.Count()} раз", cont.Handle);

                    // ---- DALI-группы
                    int seq = 0;
                    var grpSeen = new HashSet<int>();
                    var ecgSeen = new Dictionary<int, int>();                // адрес ЭПРА → номер группы
                    foreach (var b in cont.Children.OfType<AdresaDaliBlock>())
                    {
                        seq++;
                        int? grp = IntOrNull(b.Get(Names.NATR_NOMER_GRUPPY_DALI));
                        if (grp == null) { grp = seq; r.Warn(W, $"{devId} DALI: нет {Names.NATR_NOMER_GRUPPY_DALI}, присвоен {seq} по порядку", b.Handle); }
                        string who = $"{devId} DALI-группа {grp}";
                        if (grp < 1 || grp > 16) r.Error(W, $"{who}: номер группы вне 1–16", b.Handle);
                        if (!grpSeen.Add(grp.Value)) r.Error(W, $"{who}: номер группы повторяется в шлюзе", b.Handle);

                        var errs = new List<string>();
                        var ballasts = KnxAddr.ParseIntList(b.Get(Names.NATR_ADRESA_DALI), errs);
                        var ga = KnxAddr.ParseGaDpt(b.Get(Names.NATR_GRUPPOVOY_ADRES), b.Get(Names.NATR_DPT), errs);
                        foreach (var e in errs) r.Error(W, $"{who}: {e}", b.Handle);
                        if (ga.Count == 0) r.Error(W, $"{who}: без группового адреса", b.Handle);
                        if (ballasts.Count == 0) r.Error(W, $"{who}: пустой {Names.NATR_ADRESA_DALI}", b.Handle);
                        foreach (var a in ballasts)
                        {
                            if (a < 1 || a > 64) r.Error(W, $"{who}: адрес ЭПРА {a} вне 1–64", b.Handle);
                            else if (ecgSeen.TryGetValue(a, out var g0)) r.Error(W, $"{who}: ЭПРА {a} уже в группе {g0} этого шлюза", b.Handle);
                            else ecgSeen[a] = grp.Value;
                        }

                        string load = b.Get(Names.NATR_NAZVANIE_PRIEMNIKA), room = b.Get(Names.NATR_NOMER_KOMNATY);
                        if (load.Trim().Length == 0) r.Error(W, $"{who}: нагрузка не указана", b.Handle);
                        Register(book, gaOwner, ga, (load + " " + room).Trim(), who, b.Handle, r);

                        dev.Dali.Add(new DaliDto { Group = grp.Value, Ballasts = ballasts, Load = load, Room = room, Ga = Refs(ga) });
                    }

                    // ---- единственное решение про физический адрес
                    bool hasChannels = dev.Outputs.Count + dev.Inputs.Count + dev.Dali.Count > 0;
                    if (dev.Pa == null)
                    {
                        if (paBad) { }                                                     // ошибка формата уже записана
                        else if (paNone && !hasChannels)
                        {
                            r.Info(W, $"{devName}: {Names.NATR_FIZICHESKIY_ADRES} = «{Names.NV_NONE}» — пассивное устройство, в ETS не выгружается", cont.Handle);
                            continue;
                        }
                        else if (paNone)
                            r.Error(W, $"{devName}: {Names.NATR_FIZICHESKIY_ADRES} = «{Names.NV_NONE}», но у устройства есть каналы — адрес обязателен", cont.Handle);
                        else if (!hasChannels)
                        {
                            r.Warn(W, $"{devName}: {Names.NATR_FIZICHESKIY_ADRES} пуст и каналов нет — если это пассивное устройство, напишите «{Names.NV_NONE}»; в ETS не выгружается", cont.Handle);
                            continue;
                        }
                        else
                            r.Error(W, $"{devName}: устройство с каналами без физического адреса", cont.Handle);
                    }

                    x.Devices.Add(dev);
                }
            }

            // DPT входов — из книги; ГА, которого нет ни у одного выхода / DALI-группы — ошибка
            foreach (var t in toResolve)
                foreach (var ga in t.Item1)
                {
                    if (book.TryGetValue(ga.Text, out var known)) ga.Dpt = known.Dpt;
                    else r.Error(W, $"вход управляет ГА {ga.Text}, которого нет ни у одного выхода / DALI-группы", t.Item2);
                }

            // ГА без отправителя: его слушает актуатор или DALI-группа, но со схемы в него никто не пишет
            foreach (var ga in book.Values)
                if (!controlled.Contains(ga.Text) && !statusGas.Contains(ga.Text))
                    r.Warn(W, $"ГА {ga.Text} «{ga.Name}»: ни один вход им не управляет (сцена / логика / визуализация?)", gaOwner[ga.Text]);

            x.GroupAddresses = book.Values.OrderBy(a => a.Main).ThenBy(a => a.Middle).ThenBy(a => a.Sub).ToList();
            x.Errors = r.Issues.Where(i => i.Severity != Severity.Info)
                .Select(i => new ErrorDto { Severity = i.Severity.ToString(), Handle = i.Handle, Message = i.Message })
                .ToList();
            return x;
        }

        /// Книга ГА. Проверки: «ГА без DPT», «один ГА — один DPT».
        static void Register(Dictionary<string, GroupAddressDto> book, Dictionary<string, string> owner,
                             List<GroupAddressDto> gas, string name, string who, string handle, Report r)
        {
            foreach (var ga in gas)
            {
                if (ga.Dpt == null) r.Error(W, $"{who}: ГА {ga.Text} без DPT", handle);
                if (!book.TryGetValue(ga.Text, out var known))
                {
                    ga.Name = (name + " — " + DptSuffix(ga.Dpt)).Trim(' ', '—');
                    book[ga.Text] = ga;
                    owner[ga.Text] = handle;
                    continue;
                }
                if (known.Dpt == null && ga.Dpt != null) known.Dpt = ga.Dpt;
                else if (known.Dpt != null && ga.Dpt != null && known.Dpt != ga.Dpt)
                    r.Error(W, $"{who}: ГА {ga.Text} уже с DPT {known.Dpt}, здесь {ga.Dpt}", handle);
            }
        }

        /// Человекочитаемый хвост имени ГА по его DPT — чтобы 1/3/1, 1/3/2, 1/3/3 не назывались одинаково.
        static string DptSuffix(string dpt)
        {
            switch (dpt)
            {
                case "1.001": return "вкл/выкл";
                case "1.008": return "движение";
                case "1.007": return "стоп/шаг";
                case "3.007": return "диммирование";
                case "5.001": return "значение %";
                case "1.011": return "статус";
                default: return dpt ?? "";
            }
        }

        static List<GaRefDto> Refs(List<GroupAddressDto> gas) => gas.Select(g => new GaRefDto { Text = g.Text, Dpt = g.Dpt }).ToList();
        static string Or(string a, string b) => string.IsNullOrWhiteSpace(a) ? (b ?? "").Trim() : a.Trim();
        static int? IntOrNull(string s) => int.TryParse((s ?? "").Trim(), out var v) ? v : (int?)null;
    }
}