using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Structure;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public enum BarOutput
{
    [Text("Forma livre (tubos e nós, leve)", "Free form (tubes and nodes, light)")] FreeForm,
    [Text("Família paramétrica TT_Bloco (barras quadradas)", "TT_Bloco parametric family (square bars)")] Parametric,
    [Text("Elementos estruturais (vigas/contraventos do projeto)", "Structural elements (project beams/braces)")] Structural,
}

public sealed class SpaceFrameOptions
{
    [Opt("Tipo de malha", "Grid type", Group = "Malha|Grid")]
    public SpaceFrameType Type { get; set; } = SpaceFrameType.SquareOnSquareOffset;

    [Opt("Módulo X", "Module X", Group = "Malha|Grid", Kind = OptKind.Length, Min = 0.05)]
    public double ModuleX { get; set; } = U.M(1.5);

    [Opt("Módulo Y", "Module Y", Group = "Malha|Grid", Kind = OptKind.Length, Min = 0.05, VisibleIf = "Type!=TriangularOffset")]
    public double ModuleY { get; set; } = U.M(1.5);

    [Opt("Altura entre camadas", "Depth between layers", Group = "Malha|Grid", Kind = OptKind.Length, Min = 0.05)]
    public double Depth { get; set; } = U.M(1.0);

    [Opt("Camadas", "Layers", Group = "Malha|Grid", Min = 2, Max = 6)]
    public int Layers { get; set; } = 2;

    [Opt("Diagonais", "Diagonals", Group = "Malha|Grid")]
    public DiagonalPattern Diagonals { get; set; } = DiagonalPattern.All;

    [Opt("Rotação da malha", "Grid rotation", Group = "Malha|Grid", Kind = OptKind.Angle)]
    public double Rotation { get; set; }

    [Opt("Deslocamento X", "Offset X", Group = "Malha|Grid", Kind = OptKind.Length)]
    public double OffsetX { get; set; }

    [Opt("Deslocamento Y", "Offset Y", Group = "Malha|Grid", Kind = OptKind.Length)]
    public double OffsetY { get; set; }

    [Opt("Cota do banzo superior (acima do nível/hospedeiro)", "Top chord elevation (above level/host)", Group = "Posição|Position", Kind = OptKind.Length)]
    public double TopOffset { get; set; } = U.M(4);

    [Opt("Seguir uma superfície curva", "Follow a curved surface", Group = "Posição|Position", Tip = "Depois de OK, escolha a face (ex.: cobertura em massa). A malha é projetada verticalmente sobre ela.|After OK, pick the face. The grid is projected vertically onto it.")]
    public bool FollowSurface { get; set; }

    [Opt("Saída das barras", "Bar output", Group = "Elementos|Elements")]
    public BarOutput BarOutput { get; set; } = BarOutput.FreeForm;

    [Opt("Tipo de viga/perfil", "Beam/profile type", Group = "Elementos|Elements", Kind = OptKind.FamilyType, Filter = "OST_StructuralFraming", VisibleIf = "BarOutput=Structural")]
    public string BeamType { get; set; } = "";

    [Opt("Diâmetro do tubo", "Tube diameter", Group = "Elementos|Elements", Kind = OptKind.Length, Min = 0.001, VisibleIf = "BarOutput!=Structural")]
    public double TubeDiameter { get; set; } = U.Mm(60);

    [Opt("Criar nós", "Create nodes", Group = "Elementos|Elements")]
    public bool CreateNodes { get; set; } = true;

    [Opt("Diâmetro do nó", "Node diameter", Group = "Elementos|Elements", Kind = OptKind.Length, Min = 0.001, VisibleIf = "CreateNodes")]
    public double NodeDiameter { get; set; } = U.Mm(120);

    [Opt("Material das barras", "Bar material", Group = "Elementos|Elements", Kind = OptKind.Material)]
    public string BarMaterial { get; set; } = "";

    [Opt("Material dos nós", "Node material", Group = "Elementos|Elements", Kind = OptKind.Material, VisibleIf = "CreateNodes")]
    public string NodeMaterial { get; set; } = "";

    [Opt("Tolerância para agrupar comprimentos", "Length grouping tolerance", Group = "Quantitativos|Quantities", Kind = OptKind.Length, Min = 0.0001)]
    public double LengthTolerance { get; set; } = U.Mm(1);

    [Opt("Criar vista 3D de detalhamento", "Create 3D detail view", Group = "Quantitativos|Quantities")]
    public bool CreateView { get; set; } = true;

    public SpaceFrameSettings ToCore() => new()
    {
        Type = Type,
        ModuleX = ModuleX,
        ModuleY = ModuleY,
        Depth = Depth,
        Layers = Layers,
        Diagonals = Diagonals,
        RotationDeg = Rotation,
        OffsetX = OffsetX,
        OffsetY = OffsetY,
        TubeDiameter = TubeDiameter,
        NodeDiameter = NodeDiameter,
        LengthTolerance = LengthTolerance,
    };
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.spaceframe", Modules.Generators, "Quadro espacial", "Space frame", Order = 40, Large = true,
    DescPt = "Gera malhas espaciais de duas ou mais camadas (quadradas ou triangulares) sobre pisos, coberturas, ambientes ou superfícies curvas, com quantitativos de barras e nós.",
    DescEn = "Generates two-or-more-layer space frames (square or triangular) over floors, roofs, rooms or curved surfaces, with bar and node quantities.",
    HelpPt = "A planta da estrutura vem do elemento escolhido (piso, cobertura, ambiente) ou de um retângulo. As barras podem ser geradas como forma livre (tubos), blocos paramétricos ou como vigas/contraventos estruturais do tipo escolhido — estes são elementos BIM nativos.\n\nO relatório traz a lista de barras agrupadas por função e comprimento e a contagem de nós por quantidade de barras conectadas.\n\nLimitação: é um gerador geométrico; dimensionamento estrutural deve ser feito em software de cálculo.",
    HelpEn = "The plan comes from the picked element (floor, roof, room) or a rectangle. Bars can be free-form tubes, parametric blocks or structural beams/braces of the chosen type.\n\nLimitation: this is a geometric generator; structural design must be done in analysis software.",
    Keywords = "quadro espacial trelica malha estrutura space frame truss grid tubos nos")]
public sealed class SpaceFrameCommand : ToolCommand
{
    public sealed class Context
    {
        public List<double[]> Footprint { get; set; } = new();
        public double BaseZ { get; set; }
        public string? FaceReference { get; set; }
    }

    public override void Run(ToolContext ctx)
    {
        var i = Msg.Choose(Loc.T("Qual a planta do quadro espacial?", "What is the space frame plan?"), ctx.Tool.Name,
            Loc.T("Contorno de piso, cobertura ou ambiente", "Floor, roof or room outline"), Loc.T("Retângulo por dois pontos", "Rectangle by two points"));
        if (i < 0) ctx.Cancel();
        var c = new Context();
        if (i == 0)
        {
            var e = ctx.PickElement("Escolha o piso, a cobertura ou o ambiente", "Pick the floor, roof or room", x => x is Floor or RoofBase or Room);
            Region2? reg = e is Room room ? RevitGeo.RoomRegion(room) : RevitGeo.HostRegion(e);
            if (reg == null)
            {
                var b = RevitGeo.Box(e)!;
                reg = new Region2(Rect2.FromPoints(new[] { b.Min.ToVec2(), b.Max.ToVec2() }).ToPolygon());
            }
            c.Footprint = reg.Outer.Select(p => new[] { p.X, p.Y }).ToList();
            c.BaseZ = e is Room rr ? rr.Level?.Elevation ?? 0 : RevitGeo.Box(e)?.Max.Z ?? 0;
        }
        else
        {
            var a = ctx.PickPoint("Primeiro canto", "First corner");
            var b = ctx.PickPoint("Canto oposto", "Opposite corner");
            c.Footprint = Rect2.FromPoints(new[] { a.ToVec2(), b.ToVec2() }).ToPolygon().Select(p => new[] { p.X, p.Y }).ToList();
            c.BaseZ = (Lookup.LevelBelow(ctx.Doc, a.Z)?.Elevation) ?? a.Z;
        }
        var o = ctx.Ask(ctx.LoadOptions<SpaceFrameOptions>(), preview: x => Preview(ctx, x, c), okText: Loc.T("Gerar", "Generate"));
        if (o.FollowSurface)
        {
            var r = ctx.PickFace("Escolha a superfície a seguir", "Pick the surface to follow");
            c.FaceReference = r.ConvertToStableRepresentation(ctx.Doc);
        }
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static Region2 Footprint(Context c) => new(new Polygon2(c.Footprint.Select(p => new Vec2(p[0], p[1]))));

    private static SpaceFrameResult Compute(ToolContext ctx, SpaceFrameOptions o, Context c)
    {
        Func<Vec2, (Vec3, Vec3)>? surface = null;
        var top = c.BaseZ + o.TopOffset;
        if (c.FaceReference != null)
        {
            var r = Reference.ParseFromStableRepresentation(ctx.Doc, c.FaceReference);
            var el = ctx.Doc.GetElement(r);
            var face = el?.GetGeometryObjectFromReference(r) as Face;
            var t = el == null ? Transform.Identity : GenHost.TransformOf(el);
            var inv = t.Inverse;
            if (face != null)
            {
                surface = p =>
                {
                    var a = inv.OfPoint(new XYZ(p.X, p.Y, top + 1000));
                    var b = inv.OfPoint(new XYZ(p.X, p.Y, top - 1000));
                    if (face.Intersect(Line.CreateBound(a, b), out var res) == SetComparisonResult.Overlap && res != null && res.Size > 0)
                    {
                        var ir = res.get_Item(0);
                        var n = t.OfVector(face.ComputeNormal(ir.UVPoint)).Normalize();
                        if (n.Z < 0) n = n.Negate();
                        return (t.OfPoint(ir.XYZPoint).ToVec3(), n.ToVec3());
                    }
                    return (new Vec3(p.X, p.Y, top), Vec3.UnitZ);
                };
            }
        }
        var res2 = SpaceFrameGenerator.Generate(o.ToCore(), Footprint(c), surface, ctx.Settings.ElementHardLimit);
        if (surface == null)
        {
            foreach (var n in res2.Nodes) n.Position = n.Position + new Vec3(0, 0, top);
            foreach (var b in res2.Bars)
            {
                b.Start = b.Start + new Vec3(0, 0, top);
                b.End = b.End + new Vec3(0, 0, top);
            }
        }
        return res2;
    }

    private static PreviewModel Preview(ToolContext ctx, SpaceFrameOptions o, Context c)
    {
        var r = Compute(ctx, o, c);
        var pm = new PreviewModel();
        pm.Outline(Footprint(c).Outer, "#5F6B7A", 2);
        foreach (var b in r.Bars.Take(15000))
        {
            var color = b.Role switch
            {
                BarRole.TopChord => "#B5652B",
                BarRole.BottomChord => "#3B7DD8",
                BarRole.Vertical => "#2F7D4F",
                _ => "#9AA3AE",
            };
            pm.Line(b.Start.XY, b.End.XY, color, b.Role == BarRole.Diagonal ? 0.6 : 1.2);
        }
        var sc = pm.S3;
        var fp = Footprint(c).Outer;
        sc.Face(new Frame3(new Vec3(0, 0, c.BaseZ), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), fp, "#D9D4CC", 0.25);
        var tube = Math.Max(o.TubeDiameter, U.Mm(10));
        foreach (var b in r.Bars.Take(8000))
        {
            var color = b.Role switch
            {
                BarRole.TopChord => "#B5652B",
                BarRole.BottomChord => "#3B7DD8",
                BarRole.Vertical => "#2F7D4F",
                _ => "#9AA3AE",
            };
            sc.Cylinder(b.Start, b.End, tube / 2, color, 6);
        }
        if (o.CreateNodes)
            foreach (var n in r.Nodes.Take(4000)) sc.Sphere(n.Position, Math.Max(o.NodeDiameter, tube) / 2, "#5F6B7A", 8);
        pm.Info = Loc.F("{0} nós · {1} barras · {2} de barras", "{0} nodes · {1} bars · {2} of bars", r.Nodes.Count, r.Bars.Count, U.FormatM(r.TotalBarLength));
        return pm;
    }

    internal static void Generate(ToolContext ctx, SpaceFrameOptions o, Context c, string systemId)
    {
        var doc = ctx.Doc;
        var r = Compute(ctx, o, c);
        ctx.CheckCount(r.Bars.Count + (o.CreateNodes ? r.Nodes.Count : 0), "elementos", "elements");
        FamilySymbol? beam = null;
        if (o.BarOutput == BarOutput.Structural)
        {
            beam = Lookup.Symbol(doc, o.BeamType, "OST_StructuralFraming") ?? throw new ToolException(Loc.T("Escolha o tipo de viga/perfil.", "Choose the beam/profile type."));
            if (!beam.IsActive) Tx.Run(doc, "Téra Tools: ativar perfil", () => beam.Activate());
        }
        var sink = new BoxSink(ctx, o.BarOutput == BarOutput.Parametric ? GenOutput.Parametric : GenOutput.FreeForm, BoxKind.Block, systemId) { Name = "TT Quadro espacial" };
        if (o.BarOutput == BarOutput.Parametric) sink.Prepare();
        var created = new List<ElementId>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var barOpts = Shapes.Options(doc, o.BarMaterial);
            var nodeOpts = Shapes.Options(doc, o.NodeMaterial);
            var level = Lookup.LevelBelow(doc, r.Nodes.Count > 0 ? r.Nodes.Min(n => n.Position.Z) : c.BaseZ);
            var byRole = new Dictionary<BarRole, List<GeometryObject>>();
            var idx = 0;
            foreach (var b in r.Bars)
            {
                var a = b.Start.ToXYZ();
                var e = b.End.ToXYZ();
                if (a.DistanceTo(e) < Tolerance.ShortCurve) continue;
                var dir = (e - a).Normalize();
                switch (o.BarOutput)
                {
                    case BarOutput.Structural:
                        try
                        {
                            var st = Math.Abs(dir.Z) > 0.1 ? StructuralType.Brace : StructuralType.Beam;
                            var fi = doc.Create.NewFamilyInstance(Line.CreateBound(a, e), beam!, level!, st);
                            GenSystem.Mark(fi, systemId, "gen.spaceframe", idx, SpaceFrameResult.RoleName(b.Role));
                            created.Add(fi.Id);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug("Barra estrutural: " + ex.Message);
                        }
                        break;
                    case BarOutput.Parametric:
                        var perp = Math.Abs(dir.Z) < 0.99 ? dir.CrossProduct(XYZ.BasisZ).Normalize() : dir.CrossProduct(XYZ.BasisX).Normalize();
                        var box = OrientedBox.FromCenter(((a + e) / 2).ToVec3(), dir.ToVec3(), perp.ToVec3(), a.DistanceTo(e), o.TubeDiameter, o.TubeDiameter);
                        sink.Add(box, string.IsNullOrWhiteSpace(o.BarMaterial) ? null : o.BarMaterial, idx, el => RevitGeo.Set(el, SpaceFrameResult.RoleName(b.Role), SharedParams.Grupo));
                        break;
                    default:
                        try
                        {
                            var plane = Plane.CreateByNormalAndOrigin(dir, a);
                            var rad = o.TubeDiameter / 2;
                            var loop = CurveLoop.Create(new List<Curve> { Arc.Create(plane, rad, 0, Math.PI), Arc.Create(plane, rad, Math.PI, 2 * Math.PI) });
                            var solid = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, dir, a.DistanceTo(e), barOpts);
                            if (!byRole.TryGetValue(b.Role, out var l)) byRole[b.Role] = l = new List<GeometryObject>();
                            l.Add(solid);
                        }
                        catch (Exception ex)
                        {
                            Log.Debug("Tubo: " + ex.Message);
                        }
                        break;
                }
                idx++;
            }
            sink.Finish();
            created.AddRange(sink.Created);
            foreach (var (role, geo) in byRole)
            {
                foreach (var chunk in geo.Chunk(3000))
                {
                    var ds = Shapes.Create(doc, chunk.ToList(), BuiltInCategory.OST_GenericModel, "TT Quadro espacial — " + SpaceFrameResult.RoleName(role));
                    GenSystem.Mark(ds, systemId, "gen.spaceframe", idx++, SpaceFrameResult.RoleName(role));
                    created.Add(ds.Id);
                }
            }
            if (o.CreateNodes && r.Nodes.Count > 0)
            {
                var spheres = new List<GeometryObject>();
                var rad = o.NodeDiameter / 2;
                foreach (var n in r.Nodes)
                {
                    try
                    {
                        var cpt = n.Position.ToXYZ();
                        var frame = new Frame(cpt, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
                        var arc = Arc.Create(cpt - XYZ.BasisZ * rad, cpt + XYZ.BasisZ * rad, cpt + XYZ.BasisX * rad);
                        var axis = Line.CreateBound(cpt + XYZ.BasisZ * rad, cpt - XYZ.BasisZ * rad);
                        var loop = CurveLoop.Create(new List<Curve> { arc, axis });
                        spheres.Add(GeometryCreationUtilities.CreateRevolvedGeometry(frame, new List<CurveLoop> { loop }, 0, 2 * Math.PI, nodeOpts));
                    }
                    catch (Exception ex)
                    {
                        Log.Debug("Nó: " + ex.Message);
                    }
                }
                foreach (var chunk in spheres.Chunk(3000))
                {
                    var ds = Shapes.Create(doc, chunk.ToList(), BuiltInCategory.OST_GenericModel, Loc.T("TT Quadro espacial — nós", "TT Space frame — nodes"));
                    GenSystem.Mark(ds, systemId, "gen.spaceframe", idx++, Loc.T("Nós", "Nodes"));
                    created.Add(ds.Id);
                }
            }
            GenSystem.Register(doc, systemId, "gen.spaceframe", o, c, Loc.T("Quadro espacial", "Space frame"));
            if (o.CreateView && r.Nodes.Count > 0)
            {
                var vft = Lookup.ViewFamilyType(doc, ViewFamily.ThreeDimensional);
                if (vft != null)
                {
                    var v = View3D.CreateIsometric(doc, vft.Id);
                    v.Name = Lookup.UniqueViewName(doc, Loc.F("TT Quadro espacial {0}", "TT Space frame {0}", systemId));
                    var min = new XYZ(r.Nodes.Min(n => n.Position.X), r.Nodes.Min(n => n.Position.Y), r.Nodes.Min(n => n.Position.Z)) - new XYZ(1, 1, 1);
                    var max = new XYZ(r.Nodes.Max(n => n.Position.X), r.Nodes.Max(n => n.Position.Y), r.Nodes.Max(n => n.Position.Z)) + new XYZ(1, 1, 1);
                    v.SetSectionBox(new BoundingBoxXYZ { Min = min, Max = max });
                    ctx.Report.Count(Loc.T("Vistas", "Views"));
                }
            }
            ctx.Report.Created(created.Count);
            foreach (var id in created) ctx.Report.AddResult(id.Value);
        });
        ctx.Report.Tables.Add(r.BarSchedule());
        ctx.Report.Tables.Add(r.NodeSchedule());
        ctx.Report.ForceShow = true;
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = Json.Deserialize<SpaceFrameOptions>(optionsJson) ?? new SpaceFrameOptions();
        var c = Json.Deserialize<Context>(contextJson) ?? throw new ToolException("Contexto inválido.");
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "gen.spaceframe");
        ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
        Generate(ctx, o, c, systemId);
    }
}
