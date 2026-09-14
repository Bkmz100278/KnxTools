using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 8. Нумерация каналов KNX.
    /// В каждом SDK_КОНТЕЙНЕР_KNX блоки SDK_ЛИНИЯ_KNX_ВЫХОД и SDK_ЛИНИЯ_KNX_ВХОД
    /// получают НОМЕР_КАНАЛА = 1, 2, 3… слева направо (при равном X — сверху вниз).
    /// Выходы и входы нумеруются раздельно. Принадлежность к контейнеру берётся
    /// из модели (KonteynerKnxBlock.Children) — та же, что в KNXJSON.
    ///   нет атрибута НОМЕР_КАНАЛА → E02;  линия вне контейнера → N03.
    public static class Step8_Channels
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 8, " + panel;
            int totalOut = 0, totalIn = 0;

            foreach (var cont in s.Containers)
            {
                var outs = cont.Children.OfType<LiniyaKnxVyhodBlock>().Cast<BlockEntity>().ToList();
                var ins = cont.Children.OfType<LiniyaKnxVhodBlock>().Cast<BlockEntity>().ToList();
                if (outs.Count == 0 && ins.Count == 0) continue;

                string who = ContainerLabel(cont, tr);
                totalOut += Number(outs, "выход", who, panel, tr, r, W, diag);
                totalIn += Number(ins, "вход", who, panel, tr, r, W, diag);

                r.Info(W, $"{who}: выходов {outs.Count}, входов {ins.Count} — пронумерованы слева направо", cont.Handle);
            }

            foreach (var b in s.LooseBlocks.Where(b => b is LiniyaKnxVyhodBlock || b is LiniyaKnxVhodBlock))
                diag.Add(DiagCatalog.N03, panel, b.Handle, $"{b.BlockName} вне контейнера {Names.NB_KONTEYNER_KNX} — номер канала не присвоен");

            if (totalOut + totalIn == 0)
                r.Info(W, "в рамке нет каналов KNX для нумерации", s.Handle);
        }

        /// Сортирует блоки по X (слева направо; при совпадении X — сверху вниз) и пишет 1..n в НОМЕР_КАНАЛА.
        private static int Number(List<BlockEntity> blocks, string kind, string who, string panel,
                                  Transaction tr, Report r, string W, DiagLog diag)
        {
            if (blocks.Count == 0) return 0;

            var ordered = blocks
                .OrderBy(b => CalcUtil.Pos(b, tr).X)
                .ThenByDescending(b => CalcUtil.Pos(b, tr).Y)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var b = ordered[i];
                string newNo = (i + 1).ToString();
                string oldNo = CalcUtil.Get(b, Names.NATR_NOMER_KANALA, tr).Trim();

                if (!diag.Set(b, Names.NATR_NOMER_KANALA, newNo, tr, panel)) continue;

                if (oldNo.Length > 0 && oldNo != newNo)
                    r.Info(W, $"{who}, {kind}: {Names.NATR_NOMER_KANALA} «{oldNo}» → «{newNo}» (по расположению)", b.Handle);
            }
            return ordered.Count;
        }

        /// «контейнер 1.1.11» если внутри есть SDK_БЛОК_KNX с адресом, иначе «контейнер <handle>».
        private static string ContainerLabel(KonteynerKnxBlock cont, Transaction tr)
        {
            var d = cont.Children.OfType<BlokKnxBlock>().FirstOrDefault();
            string pa = d == null ? "" : CalcUtil.Get(d, Names.NATR_FIZICHESKIY_ADRES, tr).Trim();
            return pa.Length > 0 ? "контейнер " + pa : "контейнер <" + cont.Handle + ">";
        }
    }
}
