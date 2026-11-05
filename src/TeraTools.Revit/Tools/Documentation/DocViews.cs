using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Geometry;
using TeraTools.Core.Layout;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Documentation;

internal static class DocUtil
{
    public static string Name(string template, IDictionary<string, object?> values) => NameTemplate.Apply(template, values.ToDictionary(k => k.Key, k => k.Value));

    public static void ApplyTemplateOrScale(Document doc, View v, string? template, int scale)
    {
        if (!string.IsNullOrWhiteSpace(template) && Lookup.View(doc, template) is { IsTemplate: true } t)
        {
            v.ViewTemplateId = t.Id;
            return;
        }
        if (scale > 0)
        {
            try { v.Scale = scale; } catch { /* controlado por modelo */ }
        }
    }

    /// <summary>Ajusta o recorte de uma vista de planta a uma caixa (coordenadas do modelo).</summary>
    public static void CropPlan(View v, double minX, double minY, double maxX, double maxY)
    {
        try
        {
            var cb = v.CropBox;
            var inv = cb.Transform.Inverse;
            var pts = new[] { new XYZ(minX, minY, 0), new XYZ(maxX, minY, 0), new XYZ(maxX, maxY, 0), new XYZ(minX, maxY, 0) }.Select(p => inv.OfPoint(p)).ToList();
            cb.Min = new XYZ(pts.Min(p => p.X), pts.Min(p => p.Y), cb.Min.Z);
            cb.Max = new XYZ(pts.Max(p => p.X), pts.Max(p => p.Y), cb.Max.Z);
            v.CropBox = cb;
            v.CropBoxActive = true;
        }
        catch (Exception ex)
        {
            Log.Warn("Recorte de vista: " + ex.Message);
        }
    }

    public static List<Level> PickLevels(ToolContext ctx)
    {
        var levels = Pick.Many(Lookup.Collect<Level>(ctx.Doc).OrderBy(l => l.Elevation), l => l.Name, ctx.Tool.Name,
            Loc.T("Para quais níveis?", "For which levels?"), ctx.Owner, l => RevitGeo.ViewLevel(ctx.ActiveView)?.Id == l.Id);
        if (levels.Count == 0) ctx.Cancel();
        return levels;
    }

    public static (double MinX, double MinY, double MaxX, double MaxY)? RoomsExtent(Document doc, Level level, double margin)
    {
        var boxes = RevitGeo.Rooms(doc).Where(r => r.LevelId == level.Id && r.Area > 0).Select(r => r.get_BoundingBox(null)).Where(b => b != null).ToList();
        if (boxes.Count == 0) return null;
        return (boxes.Min(b => b!.Min.X) - margin, boxes.Min(b => b!.Min.Y) - margin, boxes.Max(b => b!.Max.X) + margin, boxes.Max(b => b!.Max.Y) + margin);
    }
}

public sealed class PlanSetOptions
{
    [Opt("Conjuntos de plantas (Nome | Modelo de vista)", "Plan sets (Name | View template)", Kind = OptKind.MultiLine,
        Tip = "Um por linha. Inclua (forro) no nome para criar planta de forro. O modelo de vista é opcional.|One per line. Include (forro) in the name for a ceiling plan. The template is optional.")]
    public List<string> Sets { get; set; } = new()
    {
        "Layout |", "Demolir e construir |", "Pontos elétricos e iluminação |", "Paginação de piso |", "Forro (forro) |", "Acabamentos |",
    };

    [Opt("Modelo do nome", "Name template", Tip = "Marcadores: {Nivel} {Conjunto}.|Tokens: {Nivel} {Conjunto}.")]
    public string NameTemplate { get; set; } = "{Conjunto} - {Nivel}";

    [Opt("Escala (1:n) quando não houver modelo", "Scale (1:n) when there is no template", Min = 1, Max = 5000)]
    public int Scale { get; set; } = 50;

    [Opt("Recortar pela extensão dos ambientes", "Crop to the rooms' extent")]
    public bool Crop { get; set; } = true;

    [Opt("Margem do recorte", "Crop margin", Kind = OptKind.Length, Min = 0, VisibleIf = "Crop")]
    public double Margin { get; set; } = U.M(1.5);
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.plans", Modules.Documentation, "Conjunto de plantas", "Plan set", Order = 10, Large = true, Group = "Vistas|Views",
    DescPt = "Cria de uma vez as plantas do projeto de interiores por nível (layout, demolir/construir, elétrica, paginação, forro, acabamentos), com modelo de vista e recorte.",
    DescEn = "Creates all interior design plans per level at once (layout, demolition, electrical, tiling, ceiling, finishes), with view template and crop.",
    Keywords = "conjunto plantas criar vistas layout demolir construir eletrica forro plan set views")]
public sealed class PlanSetCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var levels = DocUtil.PickLevels(ctx);
        var o = ctx.Ask<PlanSetOptions>();
        var floorType = Lookup.ViewFamilyType(doc, ViewFamily.FloorPlan) ?? throw new ToolException(Loc.T("Tipo de planta não encontrado.", "Floor plan type not found."));
        var ceilType = Lookup.ViewFamilyType(doc, ViewFamily.CeilingPlan);
        var sets = o.Sets.Select(s => s.Split('|')).Where(p => p[0].Trim().Length > 0).Select(p => (Name: p[0].Trim(), Template: p.Length > 1 ? p[1].Trim() : "")).ToList();
        ctx.CheckCount(levels.Count * sets.Count, "vistas", "views");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var level in levels)
            {
                var extent = o.Crop ? DocUtil.RoomsExtent(doc, level, o.Margin) : null;
                foreach (var (name, template) in sets)
                {
                    var isCeiling = name.Contains("(forro)", StringComparison.OrdinalIgnoreCase) || name.Contains("(ceiling)", StringComparison.OrdinalIgnoreCase);
                    var vft = isCeiling ? ceilType : floorType;
                    if (vft == null) continue;
                    var clean = name.Replace("(forro)", "", StringComparison.OrdinalIgnoreCase).Replace("(ceiling)", "", StringComparison.OrdinalIgnoreCase).Trim();
                    var v = ViewPlan.Create(doc, vft.Id, level.Id);
                    v.Name = Lookup.UniqueViewName(doc, DocUtil.Name(o.NameTemplate, new Dictionary<string, object?> { ["Nivel"] = level.Name, ["Conjunto"] = clean }));
                    DocUtil.ApplyTemplateOrScale(doc, v, template, o.Scale);
                    if (extent is { } e) DocUtil.CropPlan(v, e.MinX, e.MinY, e.MaxX, e.MaxY);
                    ctx.Report.Created();
                    ctx.Report.AddResult(v.Id.Value);
                }
            }
        });
    }
}

public sealed class RoomSectionOptions
{
    [Opt("Corte longitudinal", "Longitudinal section")]
    public bool Longitudinal { get; set; } = true;

    [Opt("Corte transversal", "Cross section")]
    public bool Transversal { get; set; } = true;

    [Opt("Margem", "Margin", Kind = OptKind.Length, Min = 0)]
    public double Margin { get; set; } = U.Mm(500);

    [Opt("Escala (1:n)", "Scale (1:n)", Min = 1, Max = 5000)]
    public int Scale { get; set; } = 50;

    [Opt("Modelo de vista", "View template", Kind = OptKind.ViewTemplate, Filter = "Section")]
    public string Template { get; set; } = "";

    [Opt("Modelo do nome", "Name template", Tip = "Marcadores: {Numero} {Nome} {Corte}.|Tokens: {Numero} {Nome} {Corte}.")]
    public string NameTemplate { get; set; } = "{Numero} {Nome} - Corte {Corte}";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.sections", Modules.Documentation, "Cortes por ambiente", "Sections per room", Order = 11, Group = "Vistas|Views",
    DescPt = "Cria cortes longitudinal e transversal passando pelo centro de cada ambiente, alinhados às paredes e enquadrados.",
    DescEn = "Creates longitudinal and cross sections through the center of each room, aligned to its walls and framed.",
    Keywords = "cortes ambiente secao longitudinal transversal sections room")]
public sealed class RoomSectionsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var o = ctx.Ask<RoomSectionOptions>();
        var vft = Lookup.ViewFamilyType(doc, ViewFamily.Section) ?? throw new ToolException(Loc.T("Tipo de corte não encontrado.", "Section type not found."));
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms)
            {
                var reg = RevitGeo.RoomRegion(room);
                if (reg == null || room.Level == null) continue;
                var a = GridLayout.DominantDirection(reg.Outer);
                var c = reg.Outer.Centroid;
                var z0 = room.Level.Elevation;
                var h = Math.Max(room.UnboundedHeight, U.M(2.5));
                var cuts = new List<(string Label, Vec2 Dir)>();
                if (o.Longitudinal) cuts.Add(("A", Vec2.FromAngle(a)));
                if (o.Transversal) cuts.Add(("B", Vec2.FromAngle(a + Math.PI / 2)));
                foreach (var (label, d) in cuts)
                {
                    var perp = d.Perp;
                    var (u0, u1) = InteriorsUtil.Extent(reg.Outer, d);
                    var (w0, w1) = InteriorsUtil.Extent(reg.Outer, perp);
                    var cu = c.Dot(d);
                    var cw = c.Dot(perp);
                    var t = Transform.Identity;
                    t.Origin = new XYZ(c.X, c.Y, z0);
                    t.BasisX = new XYZ(d.X, d.Y, 0);
                    t.BasisY = XYZ.BasisZ;
                    t.BasisZ = t.BasisX.CrossProduct(t.BasisY);
                    var box = new BoundingBoxXYZ
                    {
                        Transform = t,
                        Min = new XYZ(u0 - cu - o.Margin, -U.Mm(300), -(Math.Max(w1 - cw, cw - w0) + o.Margin)),
                        Max = new XYZ(u1 - cu + o.Margin, h + o.Margin, 0),
                    };
                    try
                    {
                        var v = ViewSection.CreateSection(doc, vft.Id, box);
                        v.Name = Lookup.UniqueViewName(doc, DocUtil.Name(o.NameTemplate, new Dictionary<string, object?> { ["Numero"] = room.Number, ["Nome"] = room.Name, ["Corte"] = label }));
                        DocUtil.ApplyTemplateOrScale(doc, v, o.Template, o.Scale);
                        ctx.Report.Created();
                        ctx.Report.AddResult(v.Id.Value);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.Warn(RoomUtil.Label(room) + ": " + Shapes.FailText(ex), room.Id.Value);
                    }
                }
            }
        });
    }
}

public enum AxoScope
{
    [Text("Um 3D por ambiente selecionado", "One 3D per selected room")] Rooms,
    [Text("Um 3D por nível (cortado na altura indicada)", "One 3D per level (cut at the given height)")] Levels,
}

public enum AxoDirection
{
    [Text("Sudoeste", "Southwest")] SW,
    [Text("Sudeste", "Southeast")] SE,
    [Text("Noroeste", "Northwest")] NW,
    [Text("Nordeste", "Northeast")] NE,
}

public sealed class AxoOptions
{
    [Opt("Criar", "Create")]
    public AxoScope Scope { get; set; } = AxoScope.Rooms;

    [Opt("Direção de observação", "View direction")]
    public AxoDirection Direction { get; set; } = AxoDirection.SW;

    [Opt("Altura de corte (sobre o nível)", "Cut height (above level)", Kind = OptKind.Length, Min = 0.1)]
    public double CutHeight { get; set; } = U.M(2.4);

    [Opt("Margem", "Margin", Kind = OptKind.Length, Min = 0)]
    public double Margin { get; set; } = U.Mm(300);

    [Opt("Modelo de vista", "View template", Kind = OptKind.ViewTemplate, Filter = "ThreeD")]
    public string Template { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.axo", Modules.Documentation, "Axonométricas (3D) cortadas", "Cut axonometric (3D) views", Order = 12, Group = "Vistas|Views",
    DescPt = "Cria vistas 3D isométricas por ambiente ou por nível, com caixa de corte na altura escolhida — ideais para apresentação de interiores.",
    DescEn = "Creates isometric 3D views per room or level, with a section box at the chosen height — ideal for interior presentations.",
    Keywords = "axonometrica isometrica 3d cortada caixa de corte apresentacao axonometric isometric section box")]
public sealed class AxoCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<AxoOptions>();
        var vft = Lookup.ViewFamilyType(doc, ViewFamily.ThreeDimensional) ?? throw new ToolException(Loc.T("Tipo de vista 3D não encontrado.", "3D view type not found."));
        var targets = new List<(string Name, BoundingBoxXYZ Box)>();
        if (o.Scope == AxoScope.Rooms)
        {
            foreach (var r in ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room rr && rr.Area > 0).OfType<Room>())
            {
                var bb = r.get_BoundingBox(null);
                if (bb == null || r.Level == null) continue;
                targets.Add(($"{RoomUtil.Label(r)} - 3D", new BoundingBoxXYZ
                {
                    Min = new XYZ(bb.Min.X - o.Margin, bb.Min.Y - o.Margin, r.Level.Elevation - U.Mm(200)),
                    Max = new XYZ(bb.Max.X + o.Margin, bb.Max.Y + o.Margin, r.Level.Elevation + o.CutHeight),
                }));
            }
        }
        else
        {
            foreach (var level in DocUtil.PickLevels(ctx))
            {
                var e = DocUtil.RoomsExtent(doc, level, o.Margin);
                if (e == null)
                {
                    ctx.Report.Info(Loc.F("{0}: sem ambientes para definir a extensão.", "{0}: no rooms to define the extent.", level.Name));
                    continue;
                }
                targets.Add(($"{level.Name} - 3D", new BoundingBoxXYZ
                {
                    Min = new XYZ(e.Value.MinX, e.Value.MinY, level.Elevation - U.Mm(200)),
                    Max = new XYZ(e.Value.MaxX, e.Value.MaxY, level.Elevation + o.CutHeight),
                }));
            }
        }
        var (sx, sy) = o.Direction switch { AxoDirection.SE => (1, -1), AxoDirection.NW => (-1, 1), AxoDirection.NE => (1, 1), _ => (-1, -1) };
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var (name, box) in targets)
            {
                var v = View3D.CreateIsometric(doc, vft.Id);
                v.Name = Lookup.UniqueViewName(doc, name);
                v.SetSectionBox(box);
                var center = (box.Min + box.Max) / 2;
                var eye = center + new XYZ(sx, sy, 1).Normalize() * 100;
                var forward = (center - eye).Normalize();
                var up = (XYZ.BasisZ - forward * XYZ.BasisZ.DotProduct(forward)).Normalize();
                v.SetOrientation(new ViewOrientation3D(eye, up, forward));
                if (Lookup.View(doc, o.Template) is { IsTemplate: true } t) v.ViewTemplateId = t.Id;
                else
                {
                    try { v.DisplayStyle = DisplayStyle.ShadingWithEdges; v.DetailLevel = ViewDetailLevel.Fine; } catch { /* modelo */ }
                }
                ctx.Report.Created();
                ctx.Report.AddResult(v.Id.Value);
            }
        });
    }
}

