using Autodesk.Revit.DB;
using TeraTools.Core.Documentation;
using TeraTools.Core.Geometry;
using TeraTools.Core.Units;

namespace TeraTools.Revit.Infrastructure;

/// <summary>Coloca vistas numa prancha sem sobreposição (usa o tamanho real de cada viewport).</summary>
public static class SheetPlacer
{
    /// <summary>Área útil da prancha (pelos limites do carimbo), com margem e reserva à direita para o carimbo vertical.</summary>
    public static Rect2 UsableArea(ViewSheet sheet, double margin, double rightReserve)
    {
        var o = sheet.Outline;
        var w = o.Max.U - o.Min.U;
        var h = o.Max.V - o.Min.V;
        if (w < U.Mm(50) || h < U.Mm(50)) return new Rect2(margin, margin, U.Mm(420) - margin - rightReserve, U.Mm(297) - margin);
        return new Rect2(o.Min.U + margin, o.Min.V + margin, o.Max.U - margin - rightReserve, o.Max.V - margin);
    }

    /// <summary>Cria os viewports e organiza em prateleiras. Devolve as vistas que não couberam (removidas da prancha).</summary>
    public static List<View> Place(Document doc, ViewSheet sheet, IEnumerable<View> views, double gap, Rect2? area = null)
    {
        var vps = new List<Viewport>();
        var notPlaced = new List<View>();
        foreach (var v in views)
        {
            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, v.Id))
            {
                notPlaced.Add(v);
                continue;
            }
            vps.Add(Viewport.Create(doc, sheet.Id, v.Id, XYZ.Zero));
        }
        if (vps.Count == 0) return notPlaced;
        doc.Regenerate();
        var boxes = vps.Select((vp, i) =>
        {
            var ol = vp.GetBoxOutline();
            return new ViewBox { Key = i.ToString(), Width = ol.MaximumPoint.X - ol.MinimumPoint.X, Height = ol.MaximumPoint.Y - ol.MinimumPoint.Y };
        }).ToList();
        var a = area ?? UsableArea(sheet, U.Mm(15), U.Mm(0));
        foreach (var p in SheetPacker.Pack(boxes, a, gap))
        {
            var vp = vps[int.Parse(p.View.Key)];
            if (p.Sheet == 0)
            {
                vp.SetBoxCenter(new XYZ(p.Center.X, p.Center.Y, 0));
            }
            else
            {
                notPlaced.Add((View)doc.GetElement(vp.ViewId));
                doc.Delete(vp.Id);
            }
        }
        return notPlaced;
    }

    public static ElementId TitleBlockId(Document doc, string? key)
    {
        var s = Lookup.Symbol(doc, key, "OST_TitleBlocks") ?? Lookup.Symbols(doc, "OST_TitleBlocks").FirstOrDefault();
        return s?.Id ?? ElementId.InvalidElementId;
    }
}
