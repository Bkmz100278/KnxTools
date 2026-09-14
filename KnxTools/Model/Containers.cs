using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using KnxTools.Geometry;

namespace KnxTools.Model
{
    /// 5.2 — электрический щит: обозначение, все автоматы рамки, блок(и) расчётной схемы, щитовое оборудование.
    public class ElectricPanel
    {
        public List<ObShitBlock> ShitBlocks { get; } = new List<ObShitBlock>();
        public List<AvtomatUnivBlock> Avtomats { get; } = new List<AvtomatUnivBlock>();
        public List<ShemaShitBlock> ShemaBlocks { get; } = new List<ShemaShitBlock>();    // сюда потом запишем расчёты
        public List<ObShitovoeBlock> Equipment { get; } = new List<ObShitovoeBlock>();    // SDK_ОБ_ЩИТОВОЕ*

        public string Name =>
            ShitBlocks.Select(b => b.DisplayKey).FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";

        public bool HasContent => ShitBlocks.Count + Avtomats.Count + ShemaBlocks.Count + Equipment.Count > 0;
    }

    /// 6 — рамка-полилиния в слое SDK_KontPanel и всё, что в неё попало.
    public class PanelScope
    {
        public ObjectId PolylineId { get; set; }
        public string Handle { get; set; } = "";
        public string PolyType { get; set; } = "";
        public Polygon2d Area { get; set; }
        public ElectricPanel Panel { get; } = new ElectricPanel();
        public List<KonteynerKnxBlock> Containers { get; } = new List<KonteynerKnxBlock>();
        public List<BlockEntity> LooseBlocks { get; } = new List<BlockEntity>();   // KNX-блоки в рамке, но вне контейнеров

        public bool HasContent => Panel.HasContent || Containers.Count > 0 || LooseBlocks.Count > 0;

        public IEnumerable<BlockEntity> AllBlocks()
        {
            foreach (var b in Panel.ShitBlocks) yield return b;
            foreach (var b in Panel.ShemaBlocks) yield return b;
            foreach (var b in Panel.Equipment) yield return b;
            foreach (var b in Panel.Avtomats) yield return b;
            foreach (var c in Containers)
            {
                yield return c;
                foreach (var b in c.Children) if (!(b is AvtomatUnivBlock)) yield return b;   // автомат уже отдан выше
            }
            foreach (var b in LooseBlocks) yield return b;
        }

    }

    /// 7 — весь чертёж.
    public class DrawingModel
    {
        public Report Report { get; }
        public List<PanelScope> Scopes { get; } = new List<PanelScope>();
        public List<BlockEntity> OutsideBlocks { get; } = new List<BlockEntity>();   // наши блоки вне всех рамок
        public int TotalBlocks { get; set; }

        public bool HasData => Scopes.Any(s => s.HasContent);

        public DrawingModel(Report report) { Report = report; }
    }
}