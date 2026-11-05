using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Geometry;

public enum ResultCategory
{
    [Text("Modelo genérico", "Generic model")] GenericModel,
    [Text("Massa", "Mass")] Mass,
    [Text("Mobiliário", "Furniture")] Furniture,
    [Text("Marcenaria", "Casework")] Casework,
    [Text("Pisos", "Floors")] Floors,
    [Text("Paredes", "Walls")] Walls,
    [Text("Forros", "Ceilings")] Ceilings,
}

internal static class Cat
{
    public static BuiltInCategory Of(ResultCategory c) => c switch
    {
        ResultCategory.Mass => BuiltInCategory.OST_Mass,
        ResultCategory.Furniture => BuiltInCategory.OST_Furniture,
        ResultCategory.Casework => BuiltInCategory.OST_Casework,
        ResultCategory.Floors => BuiltInCategory.OST_Floors,
        ResultCategory.Walls => BuiltInCategory.OST_Walls,
        ResultCategory.Ceilings => BuiltInCategory.OST_Ceilings,
        _ => BuiltInCategory.OST_GenericModel,
    };
}

public enum OpeningShape
{
    [Text("Retangular", "Rectangular")] Rectangle,
    [Text("Circular", "Circular")] Circle,
}

public enum ConvertTarget
{
    [Text("Paredes", "Walls")] Walls,
    [Text("Pisos (contornos fechados)", "Floors (closed loops)")] Floors,
    [Text("Forros (contornos fechados)", "Ceilings (closed loops)")] Ceilings,
    [Text("Separadores de ambiente", "Room separators")] RoomSeparators,
    [Text("Linhas de modelo", "Model lines")] ModelLines,
}

public sealed class ConvertOptions
{
    [Opt("Converter em", "Convert to")]
    public ConvertTarget Target { get; set; } = ConvertTarget.Walls;

    [Opt("Camada do CAD contém", "CAD layer contains", Tip = "Filtra as linhas de um DWG importado/vinculado pelo nome da camada. Vazio = todas.|Filters lines of an imported/linked DWG by layer name. Empty = all.")]
    public string Layer { get; set; } = "";

    [Opt("Tipo de parede", "Wall type", Kind = OptKind.WallType, VisibleIf = "Target=Walls")]
    public string WallType { get; set; } = "";

    [Opt("Altura da parede", "Wall height", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Target=Walls")]
    public double WallHeight { get; set; } = U.Mm(2800);

    [Opt("Tipo de piso", "Floor type", Kind = OptKind.FloorType, VisibleIf = "Target=Floors")]
    public string FloorType { get; set; } = "";

    [Opt("Tipo de forro", "Ceiling type", Kind = OptKind.CeilingType, VisibleIf = "Target=Ceilings")]
    public string CeilingType { get; set; } = "";

    [Opt("Altura do forro", "Ceiling height", Kind = OptKind.Length, VisibleIf = "Target=Ceilings")]
    public double CeilingHeight { get; set; } = U.Mm(2600);

    [Opt("Nível", "Level", Kind = OptKind.Level, Tip = "Vazio = nível da vista ativa.|Empty = active view level.")]
    public string Level { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.convert", Modules.Modeling, "Converter linhas/CAD em elementos", "Convert lines/CAD to elements", Order = 60, Large = true,
    DescPt = "Transforma linhas de modelo/detalhe ou de um DWG em paredes, pisos, forros, separadores de ambiente ou linhas de modelo.",
    DescEn = "Turns model/detail lines or DWG lines into walls, floors, ceilings, room separators or model lines.",
    HelpPt = "Selecione linhas ou um CAD importado/vinculado. Filtre pela camada do DWG (ex.: \"PAREDE\"). Para pisos e forros, as linhas são encadeadas em contornos fechados automaticamente (folga de até 3 mm).",
    HelpEn = "Select lines or an imported/linked CAD. Filter by DWG layer. For floors and ceilings lines are chained into closed loops automatically.",
    Keywords = "converter cad dwg linhas paredes pisos convert walls floors import")]
public sealed class ConvertCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione linhas ou um CAD importado", "Select lines or an imported CAD", e => e is CurveElement || e is ImportInstance);
        var o = ctx.Ask<ConvertOptions>();
        var curves = els.SelectMany(Shapes.CurvesOf)
            .Where(c => string.IsNullOrWhiteSpace(o.Layer) || c.Layer.Contains(o.Layer, StringComparison.OrdinalIgnoreCase))
            .Select(c => c.Curve).ToList();
        if (curves.Count == 0) ToolContext.Fail("Nenhuma linha encontrada (verifique o filtro de camada).", "No lines found (check the layer filter).");
        var level = Lookup.Level(ctx.Doc, o.Level) ?? RevitGeo.ViewLevel(ctx.ActiveView) ?? throw new ToolException(Loc.T("Nenhum nível no projeto.", "No level in the project."));
        var z = level.Elevation;
        Curve Flatten(Curve c)
        {
            var t = Transform.CreateTranslation(new XYZ(0, 0, z - c.GetEndPoint(0).Z));
            var moved = c.CreateTransformed(t);
            if (Math.Abs(moved.GetEndPoint(1).Z - z) > 1e-6 && moved is Line)
                moved = Line.CreateBound(new XYZ(moved.GetEndPoint(0).X, moved.GetEndPoint(0).Y, z), new XYZ(moved.GetEndPoint(1).X, moved.GetEndPoint(1).Y, z));
            return moved;
        }
        var flat = curves.Select(Flatten).Where(c => c.Length > Tolerance.ShortCurve).ToList();
        ctx.CheckCount(flat.Count, "elementos", "elements");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            switch (o.Target)
            {
                case ConvertTarget.Walls:
                {
                    var wt = Lookup.Named<WallType>(ctx.Doc, o.WallType)?.Id ?? ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.WallType);
                    foreach (var c in flat)
                    {
                        try
                        {
                            var w = Wall.Create(ctx.Doc, c, wt, level.Id, o.WallHeight, 0, false, false);
                            ctx.Report.Created();
                            ctx.Report.AddResult(w.Id.Value);
                        }
                        catch (Exception ex)
                        {
                            ctx.ItemFailed("Linhas não convertidas em parede", "Lines not converted to walls", ex);
                        }
                    }
                    break;
                }
                case ConvertTarget.Floors:
                case ConvertTarget.Ceilings:
                {
                    var loops = Shapes.ChainLoops(flat, 0.01).Select(Shapes.ToCurveLoop).Where(l => l != null).Select(l => l!).ToList();
                    if (loops.Count == 0) throw new ToolException(Loc.T("As linhas não formam contornos fechados.", "The lines do not form closed loops."));
                    foreach (var group in GroupWithHoles(loops))
                    {
                        try
                        {
                            Element created;
                            if (o.Target == ConvertTarget.Floors)
                            {
                                var ft = Lookup.Named<FloorType>(ctx.Doc, o.FloorType)?.Id ?? ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.FloorType);
                                created = Floor.Create(ctx.Doc, group, ft, level.Id);
                            }
                            else
                            {
                                var ct = Lookup.Named<CeilingType>(ctx.Doc, o.CeilingType)?.Id ?? ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.CeilingType);
                                var ceiling = Ceiling.Create(ctx.Doc, group, ct, level.Id);
                                RevitGeo.Set(ceiling, BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM, o.CeilingHeight);
                                created = ceiling;
                            }
                            ctx.Report.Created();
                            ctx.Report.AddResult(created.Id.Value);
                        }
                        catch (Exception ex)
                        {
                            ctx.ItemFailed("Contornos não convertidos", "Outlines not converted", ex);
                        }
                    }
                    ctx.RequireCreated(ctx.Report.GetCount(Loc.T("Criados", "Created")), o.Target == ConvertTarget.Floors ? "pisos" : "forros", o.Target == ConvertTarget.Floors ? "floors" : "ceilings");
                    break;
                }
                case ConvertTarget.RoomSeparators:
                {
                    var arr = new CurveArray();
                    foreach (var c in flat) arr.Append(c);
                    var sp = SketchPlane.Create(ctx.Doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z)));
                    if (!RevitGeo.IsPlanLike(ctx.ActiveView))
                        throw new ToolException(Loc.T("Abra uma planta para criar separadores de ambiente.", "Open a plan view to create room separators."));
                    var res = ctx.Doc.Create.NewRoomBoundaryLines(sp, arr, ctx.ActiveView);
                    ctx.Report.Created(res.Size);
                    break;
                }
                default:
                {
                    var sp = SketchPlane.Create(ctx.Doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z)));
                    foreach (var c in flat)
                    {
                        var mc = ctx.Doc.Create.NewModelCurve(c, sp);
                        ctx.Report.AddResult(mc.Id.Value);
                    }
                    ctx.Report.Created(flat.Count);
                    break;
                }
            }
        });
    }

    /// <summary>
    /// Agrupa contornos em regiões: um contorno dentro de outro vira furo (e uma ilha dentro do furo vira
    /// uma nova região). Antes, cada contorno virava um elemento separado e os furos ficavam sobrepostos.
    /// </summary>
    private static List<List<CurveLoop>> GroupWithHoles(List<CurveLoop> loops)
    {
        var items = loops.Select(l => (Loop: l, Poly: RevitGeo.LoopToPolygon(l))).Where(x => !x.Poly.IsEmpty)
            .OrderByDescending(x => x.Poly.Area).ToList();
        var groups = new List<(Polygon2 Outer, List<CurveLoop> Loops)>();
        for (var i = 0; i < items.Count; i++)
        {
            var probe = items[i].Poly[0];
            // quantos contornos maiores o contêm: par = contorno externo; ímpar = furo do menor que o contém
            var containers = items.Take(i).Where(x => x.Poly.Contains(probe)).ToList();
            if (containers.Count % 2 == 0)
            {
                groups.Add((items[i].Poly, new List<CurveLoop> { items[i].Loop }));
                continue;
            }
            var parent = containers.OrderBy(x => x.Poly.Area).First();
            var g = groups.FirstOrDefault(x => ReferenceEquals(x.Outer, parent.Poly));
            if (g.Loops != null) g.Loops.Add(items[i].Loop);
            else groups.Add((items[i].Poly, new List<CurveLoop> { items[i].Loop }));
        }
        return groups.Select(g => g.Loops).ToList();
    }
}
