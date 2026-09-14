using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Calc.Diag;
using KnxTools.Model;

namespace KnxTools.Calc.Steps
{
    /// Шаг 5. Внутри контейнера НОМЕР_КАНАЛА у линий ВХОД/ВЫХОД — целые 1..n без пропусков и повторов.
    /// Только проверка, ничего не пишет.
    ///   пусто / не целое / < 1 / повтор / пропуск / больше числа линий → E05;
    ///   линия KNX вне контейнера → N03.
    public static class Step5_Channels
    {
        public static void Run(PanelScope s, CalcContext ctx, Transaction tr, Report r, DiagLog diag)
        {
            string panel = CalcUtil.Label(s, tr);
            string W = "шаг 5, " + panel;
            var covered = new HashSet<ObjectId>();

            foreach (var c in s.Containers)
            {
                var ins = c.Children.OfType<LiniyaKnxVhodBlock>().Cast<BlockEntity>().ToList();
                var outs = c.Children.OfType<LiniyaKnxVyhodBlock>().Cast<BlockEntity>().ToList();
                foreach (var k in ins) covered.Add(k.Id);
                foreach (var k in outs) covered.Add(k.Id);

                string where = "контейнер <" + c.Handle + ">";
                if (Names.CALC_CHANNELS_PER_TYPE)
                {
                    Check(ins, where + " / входы", c.Handle, panel, tr, r, W, diag);
                    Check(outs, where + " / выходы", c.Handle, panel, tr, r, W, diag);
                }
                else Check(ins.Concat(outs).ToList(), where, c.Handle, panel, tr, r, W, diag);
            }

            foreach (var b in s.LooseBlocks)
                if ((b is LiniyaKnxVhodBlock || b is LiniyaKnxVyhodBlock) && !covered.Contains(b.Id))
                    diag.Add(DiagCatalog.N03, panel, b.Handle, $"{b.BlockName} вне контейнера {Names.NB_KONTEYNER_KNX} — номер канала не проверен");
        }

        static void Check(List<BlockEntity> lines, string where, object contHandle, string panel,
                          Transaction tr, Report r, string W, DiagLog diag)
        {
            if (lines.Count == 0) return;
            int before = diag.Items.Count;
            var nums = new List<int>();

            foreach (var l in lines)
            {
                string txt = CalcUtil.Get(l, Names.NATR_NOMER_KANALA, tr);
                if (txt.Length == 0) { diag.Add(DiagCatalog.E05, panel, l.Handle, $"{where}: {Names.NATR_NOMER_KANALA} не заполнен"); continue; }
                if (!int.TryParse(txt, out int n)) { diag.Add(DiagCatalog.E05, panel, l.Handle, $"{where}: {Names.NATR_NOMER_KANALA} «{txt}» не целое число"); continue; }
                if (n < 1) { diag.Add(DiagCatalog.E05, panel, l.Handle, $"{where}: {Names.NATR_NOMER_KANALA} {n} меньше 1"); continue; }
                nums.Add(n);
            }

            foreach (var g in nums.GroupBy(x => x).Where(g => g.Count() > 1))
                diag.Add(DiagCatalog.E05, panel, contHandle, $"{where}: номер канала {g.Key} повторяется {g.Count()} раза");

            var set = new HashSet<int>(nums);
            for (int k = 1; k <= lines.Count; k++)
                if (!set.Contains(k)) diag.Add(DiagCatalog.E05, panel, contHandle, $"{where}: пропущен номер канала {k}");
            foreach (int n in set)
                if (n > lines.Count) diag.Add(DiagCatalog.E05, panel, contHandle, $"{where}: номер канала {n} больше числа линий ({lines.Count})");

            if (diag.Items.Count == before) r.Info(W, $"{where}: каналы 1..{lines.Count} в порядке");
        }
    }
}
