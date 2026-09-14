using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 7. От автомата ко всем устройствам его группы:
    ///   ЩИТ_ПИТАЮЩЕГО_АВТОМАТА = имя щита, НОМЕР_ПИТАЮЩЕГО_АВТОМАТА, УСТАВКА_ПИТАЮЩЕГО_АВТОМАТА;
    ///   SDK_БЛОК_KNX дополнительно — ПРОВОД;  SDK_АДРЕСА_DALI дополнительно — ГРУППА.
    ///   имя щита пусто / у автомата пуст НОМЕР_АВТ или УСТАВКА → E04;
    ///   у устройства нет ЩИТ_/НОМЕР_ПИТАЮЩЕГО_АВТОМАТА          → E02 (остальные атрибуты — необязательные).
    public static class Step7_Propagate
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 7, " + panel;
            string panelName = CalcUtil.PanelName(s, tr);
            var links = ctx.LinksOf(s);

            if (links.Count == 0) { r.Info(W, "связей (групп с автоматом) нет", s.Handle); return; }

            if (panelName.Length == 0)
            {
                int devs = 0; foreach (var l in links) devs += l.Devices.Count;
                diag.Add(DiagCatalog.E04, panel, s.Handle, $"имя щита пусто — {Names.NATR_SHIT_PIT_AVT} записан пустым в {devs} устройств");
            }

            foreach (var link in links)
            {
                var av = link.Breaker;
                string nomer = CalcUtil.Get(av, Names.NATR_NOMER_AVT, tr);
                string ust = CalcUtil.Get(av, Names.NATR_USTAVKA, tr);
                string provod = CalcUtil.Get(av, Names.NATR_PROVOD, tr);
                string gruppa = CalcUtil.Get(av, Names.NATR_GRUPPA, tr);

                if (nomer.Length == 0)
                    diag.Add(DiagCatalog.E04, panel, av.Handle, $"группа «{link.GroupName}»: у автомата пуст {Names.NATR_NOMER_AVT} — в {link.Devices.Count} устройств передан пустой номер");
                if (ust.Length == 0)
                    diag.Add(DiagCatalog.E04, panel, av.Handle, $"группа «{link.GroupName}»: у автомата пуста {Names.NATR_USTAVKA} — в {link.Devices.Count} устройств передана пустая уставка");

                foreach (var d in link.Devices)
                {
                    diag.Set(d, Names.NATR_SHIT_PIT_AVT, panelName, tr, panel);
                    diag.Set(d, Names.NATR_NOMER_PIT_AVT, nomer, tr, panel);
                    CalcUtil.Set(d, Names.NATR_USTAVKA_PIT_AVT, ust, tr);            // необязательный
                    if (d is BlokKnxBlock) CalcUtil.Set(d, Names.NATR_PROVOD, provod, tr);
                    if (d is AdresaDaliBlock) CalcUtil.Set(d, Names.NATR_GRUPPA, gruppa, tr);
                }
                r.Info(W, $"группа «{link.GroupName}»: {nomer} / {ust} / {panelName} → устройств {link.Devices.Count}", av.Handle);
            }
        }
    }
}