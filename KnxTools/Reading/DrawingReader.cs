using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using KnxTools.Geometry;
using KnxTools.Model;

namespace KnxTools.Reading
{
    public static class DrawingReader
    {
        public static DrawingModel Read(Database db, Report report)
        {
            var model = new DrawingModel(report);
            var blocks = new List<BlockEntity>();
            var scopes = new List<PanelScope>();
            var plStats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);      // слой → сколько полилиний
            var unknownSdk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);   // блоки SDK_*, которых мы не знаем

            var brClass = RXObject.GetClass(typeof(BlockReference));
            var plClass = RXObject.GetClass(typeof(Polyline));
            var pl2Class = RXObject.GetClass(typeof(Polyline2d));
            var pl3Class = RXObject.GetClass(typeof(Polyline3d));

            // ---------- Проход 1: собираем всё из Model Space ----------
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in ms)
                {
                    try
                    {
                        if (id.ObjectClass.IsDerivedFrom(brClass))
                        {
                            var br = (BlockReference)tr.GetObject(id, OpenMode.ForRead);
                            var name = EffectiveName(br, tr);
                            var ent = BlockFactory.Create(name);
                            if (ent == null)
                            {
                                if (name.StartsWith("SDK_", StringComparison.OrdinalIgnoreCase))
                                    unknownSdk[name] = unknownSdk.TryGetValue(name, out var u) ? u + 1 : 1;
                                continue;
                            }
                            ent.Read(br, name, tr, report);
                            blocks.Add(ent);
                        }
                        else if (id.ObjectClass.IsDerivedFrom(plClass) || id.ObjectClass.IsDerivedFrom(pl2Class) || id.ObjectClass.IsDerivedFrom(pl3Class))
                        {
                            var ent = (Entity)tr.GetObject(id, OpenMode.ForRead);
                            string layer = (ent.Layer ?? "").Trim();
                            plStats[layer] = plStats.TryGetValue(layer, out var cnt) ? cnt + 1 : 1;

                            if (!string.Equals(layer, Names.NL_KONT_PANEL, StringComparison.OrdinalIgnoreCase)) continue;

                            string handle = id.Handle.ToString();
                            var area = Polygon2d.FromEntity(ent, tr, Matrix3d.Identity);
                            if (area == null || area.Points.Count < 3) { report.Error("рамка", "меньше 3 вершин, пропущена", handle); continue; }
                            if (!area.IsClosed) report.Info("рамка", "полилиния не замкнута флагом, замкнута условно", handle);
                            scopes.Add(new PanelScope { PolylineId = id, Handle = handle, Area = area, PolyType = ent.GetType().Name });
                        }
                        else
                        {
                            var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                            if (ent != null && string.Equals((ent.Layer ?? "").Trim(), Names.NL_KONT_PANEL, StringComparison.OrdinalIgnoreCase))
                                report.Warn("слой рамок", $"объект {ent.GetType().Name} не полилиния и не учтён как рамка", id.Handle.ToString());
                        }
                    }
                    catch (System.Exception ex)
                    {
                        report.Error("чтение", "объект пропущен: " + ex.Message, id.Handle.ToString());
                    }
                }
                tr.Commit();
            }

            model.TotalBlocks = blocks.Count;
            report.Info("чертёж", $"найдено: рамок {scopes.Count}, наших блоков {blocks.Count}");
            if (unknownSdk.Count > 0)
                report.Info("чертёж", "блоки SDK_*, не описанные в Names: " +
                    string.Join(", ", unknownSdk.Select(k => $"{k.Key} ({k.Value})")));

            if (scopes.Count == 0)
            {
                var sb = new StringBuilder();
                sb.Append($"нет ни одной полилинии в слое {Names.NL_KONT_PANEL}. ");
                if (plStats.Count == 0)
                    sb.Append("В Model Space полилиний нет вообще — рамка нарисована отрезками (LINE) или лежит внутри блока.");
                else
                {
                    sb.Append("Полилинии есть в других слоях: ");
                    sb.Append(string.Join(", ", plStats.OrderByDescending(k => k.Value).Select(k => $"«{k.Key}» — {k.Value} шт.")));
                    sb.Append(". Переложите рамку на нужный слой или проверьте имя слоя (пробелы, регистр).");
                }
                report.Error("чертёж", sb.ToString());
            }

            // ---------- Проход 2: раскладываем блоки по рамкам ----------
            model.Scopes.AddRange(scopes);
            var perScope = scopes.ToDictionary(s => s, s => new List<BlockEntity>());

            foreach (var b in blocks)
            {
                var hits = scopes.Where(s => s.Area.Contains(b.Position)).ToList();
                if (hits.Count == 0) { model.OutsideBlocks.Add(b); report.Warn($"блок {b.BlockName}", "вне всех рамок", b.Handle); continue; }
                if (hits.Count > 1) report.Warn($"блок {b.BlockName}", "попадает в несколько рамок, взята первая", b.Handle);
                perScope[hits[0]].Add(b);
            }

            // ---------- Проход 3: внутри рамки — щит, контейнеры ----------
            foreach (var s in scopes) AssembleScope(s, perScope[s], report);

            return model;
        }

        private static void AssembleScope(PanelScope s, List<BlockEntity> blocks, Report report)
        {
            foreach (var c in blocks.OfType<KonteynerKnxBlock>()) { c.Parent = s; s.Containers.Add(c); }

            foreach (var b in blocks)
            {
                switch (b)
                {
                    case KonteynerKnxBlock _: break;

                    case ObShitBlock sh: sh.Parent = s.Panel; s.Panel.ShitBlocks.Add(sh); break;
                    case ShemaShitBlock sc: sc.Parent = s.Panel; s.Panel.ShemaBlocks.Add(sc); break;
                    case ObShitovoeBlock eq: eq.Parent = s.Panel; s.Panel.Equipment.Add(eq); break;

                    case AvtomatUnivBlock av:
                        av.Parent = s.Panel; s.Panel.Avtomats.Add(av);       // 5.2: автомат всегда в щите
                        AttachToContainer(s, av, report, warnIfNone: false); // и, если попал в контейнер — ещё и туда
                        break;

                    default:
                        if (BlockFactory.IsContainerChild(b))
                        {
                            if (!AttachToContainer(s, b, report, warnIfNone: true)) { b.Parent = s; s.LooseBlocks.Add(b); }
                        }
                        else { b.Parent = s; s.LooseBlocks.Add(b); }
                        break;
                }
            }

            if (s.Panel.ShitBlocks.Count == 0) report.Warn("рамка", $"нет блока {Names.NB_OB_SHIT}", s.Handle);
            if (s.Panel.ShitBlocks.Count > 1) report.Warn("рамка", $"блоков {Names.NB_OB_SHIT}: {s.Panel.ShitBlocks.Count}", s.Handle);
            if (s.Panel.Avtomats.Count == 0) report.Warn("рамка", $"нет ни одного {Names.NB_AVTOMAT_UNIV}", s.Handle);
            if (s.Panel.ShemaBlocks.Count == 0) report.Info("рамка", $"нет блока {Names.NB_SHEMA_SHIT}", s.Handle);
            if (s.Panel.ShemaBlocks.Count > 1) report.Warn("рамка", $"блоков {Names.NB_SHEMA_SHIT}: {s.Panel.ShemaBlocks.Count}", s.Handle);

            foreach (var c in s.Containers)
                if (c.Children.Count == 0) report.Warn("контейнер", "пустой контейнер (нет блоков внутри)", c.Handle);
        }

        private static bool AttachToContainer(PanelScope s, BlockEntity b, Report report, bool warnIfNone)
        {
            var hits = s.Containers.Where(c => c.Contains(b.Position)).ToList();
            if (hits.Count == 0)
            {
                if (warnIfNone) report.Warn($"блок {b.BlockName}", "в рамке, но не попал ни в один контейнер", b.Handle);
                return false;
            }
            if (hits.Count > 1) report.Warn($"блок {b.BlockName}", "попадает в несколько контейнеров, взят первый", b.Handle);
            hits[0].Children.Add(b);
            if (!(b is AvtomatUnivBlock)) b.Parent = hits[0];
            return true;
        }

        /// Имя блока с учётом динамических (иначе получите «*U12»).
        private static string EffectiveName(BlockReference br, Transaction tr)
        {
            var btrId = br.IsDynamicBlock ? br.DynamicBlockTableRecord : br.BlockTableRecord;
            return ((BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead)).Name ?? "";
        }
    }
}
