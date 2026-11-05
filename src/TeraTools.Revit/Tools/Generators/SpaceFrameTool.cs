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

public enum SpaceFramePlacement
{
    [Text("Acima da base (nível, piso ou ambiente)", "Above the base (level, floor or room)")] AboveBase,
    [Text("Embaixo da cobertura/laje escolhida", "Under the picked roof/slab")] UnderElement,
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

    [Opt("Posição", "Position", Group = "Posição|Position",
        Tip = "Embaixo do elemento: o banzo superior encosta na face inferior da cobertura/laje escolhida (a estrutura sustenta o elemento). Acima da base: o banzo superior fica na altura indicada acima do nível, piso ou ambiente.|Under the element: the top chord touches the underside of the picked roof/slab. Above the base: the top chord sits at the given height above the level, floor or room.")]
    public SpaceFramePlacement Placement { get; set; } = SpaceFramePlacement.AboveBase;

    [Opt("Altura do banzo superior acima da base", "Top chord height above the base", Group = "Posição|Position", Kind = OptKind.Length, VisibleIf = "Placement=AboveBase")]
    public double TopOffset { get; set; } = U.M(4);

    [Opt("Folga sob o elemento", "Gap under the element", Group = "Posição|Position", Kind = OptKind.Length, Min = 0, VisibleIf = "Placement=UnderElement")]
    public double UnderGap { get; set; }

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
    public enum BaseKind
    {
        Rectangle,
        Room,
        Floor,
        Roof,
    }

    public sealed class Context
    {
        public List<double[]> Footprint { get; set; } = new();
        /// <summary>Cota da base (nível do ambiente/retângulo ou topo do piso).</summary>
        public double BaseZ { get; set; }
        /// <summary>Face inferior do elemento escolhido (cobertura/laje), para a posição "embaixo do elemento".</summary>
        public double UnderZ { get; set; }
        public BaseKind Kind { get; set; }
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
            var box = RevitGeo.Box(e);
            if (reg == null && box != null)
                reg = new Region2(Rect2.FromPoints(new[] { box.Min.ToVec2(), box.Max.ToVec2() }).ToPolygon());
            if (reg == null || reg.Area <= 0)
                ToolContext.Fail("Não foi possível ler o contorno do elemento escolhido.", "Could not read the outline of the picked element.");
            c.Footprint = reg!.Outer.Select(p => new[] { p.X, p.Y }).ToList();
            switch (e)
            {
                case Room rr:
                    c.Kind = BaseKind.Room;
                    c.BaseZ = rr.Level?.Elevation ?? 0;
                    c.UnderZ = c.BaseZ + (rr.get_Parameter(BuiltInParameter.ROOM_HEIGHT)?.AsDouble() ?? U.M(3));
                    break;
                case RoofBase:
                    c.Kind = BaseKind.Roof;
                    c.BaseZ = (e.LevelId != ElementId.InvalidElementId ? (ctx.Doc.GetElement(e.LevelId) as Level)?.Elevation : null) ?? box?.Min.Z ?? 0;
                    c.UnderZ = box?.Min.Z ?? c.BaseZ;
                    break;
                default:
                    c.Kind = BaseKind.Floor;
                    c.BaseZ = box?.Max.Z ?? 0;
                    c.UnderZ = box?.Min.Z ?? c.BaseZ;
                    break;
            }
        }
        else
        {
            var a = ctx.PickPoint("Primeiro canto", "First corner");
            var b = ctx.PickPoint("Canto oposto", "Opposite corner");
            var r = Rect2.FromPoints(new[] { a.ToVec2(), b.ToVec2() });
            if (r.Width < U.Mm(100) || r.Height < U.Mm(100)) ToolContext.Fail("Retângulo pequeno demais.", "Rectangle too small.");
            c.Kind = BaseKind.Rectangle;
            c.Footprint = r.ToPolygon().Select(p => new[] { p.X, p.Y }).ToList();
            c.BaseZ = (Lookup.LevelBelow(ctx.Doc, a.Z)?.Elevation) ?? a.Z;
            c.UnderZ = c.BaseZ + U.M(4);
        }
        var o = ctx.LoadOptions<SpaceFrameOptions>();
        // A posição padrão acompanha o que foi escolhido: sob a cobertura; acima do piso/ambiente/retângulo.
        o.Placement = c.Kind == BaseKind.Roof ? SpaceFramePlacement.UnderElement : SpaceFramePlacement.AboveBase;
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Gerar", "Generate"));
        if (o.FollowSurface)
        {
            var r = ctx.PickFace("Escolha a superfície a seguir", "Pick the surface to follow");
            c.FaceReference = r.ConvertToStableRepresentation(ctx.Doc);
        }
        Generate(ctx, o, c, GenSystem.NewId());
    }

    private static Region2 Footprint(Context c) => new(new Polygon2(c.Footprint.Select(p => new Vec2(p[0], p[1]))));

    /// <summary>Cota do banzo superior conforme a posição escolhida.</summary>
    private static double TopZ(SpaceFrameOptions o, Context c) =>
        o.Placement == SpaceFramePlacement.UnderElement ? c.UnderZ - Math.Max(0, o.UnderGap) - o.TubeDiameter / 2 : c.BaseZ + o.TopOffset;

    private static SpaceFrameResult Compute(ToolContext ctx, SpaceFrameOptions o, Context c)
    {
        Func<Vec2, (Vec3, Vec3)>? surface = null;
        var top = TopZ(o, c);
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
        SpaceFrameResult res2;
        try
        {
            res2 = SpaceFrameGenerator.Generate(o.ToCore(), Footprint(c), surface, ctx.Settings.ElementHardLimit);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            throw new ToolException(ex.Message);
        }
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

    private static string RoleColor(BarRole r) => r switch
    {
        BarRole.TopChord => "#B5652B",
        BarRole.BottomChord => "#3B7DD8",
        BarRole.Vertical => "#2F7D4F",
        _ => "#9AA3AE",
    };

    private static PreviewModel Preview(ToolContext ctx, SpaceFrameOptions o, Context c)
    {
        var r = Compute(ctx, o, c);
        var pm = new PreviewModel();
        pm.Outline(Footprint(c).Outer, "#5F6B7A", 2);
        foreach (var b in r.Bars.Take(15000))
            pm.Line(b.Start.XY, b.End.XY, RoleColor(b.Role), b.Role == BarRole.Diagonal ? 0.6 : 1.2);
        var sc = pm.S3;
        var fp = Footprint(c).Outer;
        sc.Face(new Frame3(new Vec3(0, 0, c.BaseZ), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), fp, "#D9D4CC", 0.25);
        if (o.Placement == SpaceFramePlacement.UnderElement)
            sc.Face(new Frame3(new Vec3(0, 0, c.UnderZ), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), fp, "#8A9099", 0.2);
        var tube = Math.Max(o.TubeDiameter, U.Mm(10));
        // A 3D é um esboço: limita a quantidade para o diálogo continuar leve.
        foreach (var b in r.Bars.Take(4000)) sc.Cylinder(b.Start, b.End, tube / 2, RoleColor(b.Role), 6);
        if (o.CreateNodes)
            foreach (var n in r.Nodes.Take(1500)) sc.Sphere(n.Position, Math.Max(o.NodeDiameter, tube) / 2, "#5F6B7A", 8);
        var top = TopZ(o, c);
        pm.Info = Loc.F("{0} nós · {1} barras · {2} de barras · banzo superior a {3} da base",
            "{0} nodes · {1} bars · {2} of bars · top chord {3} above the base",
            r.Nodes.Count, r.Bars.Count, U.FormatM(r.TotalBarLength), U.FormatMm(top - c.BaseZ));
        if (r.Bars.Count == 0)
            pm.Info = Loc.T("Nenhuma barra cabe no contorno com esse módulo. Diminua o módulo ou confira o contorno.",
                "No bar fits the outline with this module. Reduce the module or check the outline.");
        return pm;
    }

    internal static void Generate(ToolContext ctx, SpaceFrameOptions o, Context c, string systemId)
    {
        var doc = ctx.Doc;
        var r = Compute(ctx, o, c);
        if (r.Bars.Count == 0)
            ToolContext.Fail("Nenhuma barra cabe no contorno com esse módulo. Diminua o módulo ou confira o contorno escolhido.",
                "No bar fits the outline with this module. Reduce the module or check the picked outline.");
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
        ElementId? viewId = null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            var barOpts = Shapes.Options(doc, o.BarMaterial);
            var nodeOpts = Shapes.Options(doc, o.NodeMaterial);
            var minZ = r.Nodes.Count > 0 ? r.Nodes.Min(n => n.Position.Z) : c.BaseZ;
            var level = Lookup.LevelBelow(doc, minZ + 0.01) ?? Lookup.Collect<Level>(doc).OrderBy(l => l.Elevation).FirstOrDefault();
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
                            if (level == null) throw new InvalidOperationException(Loc.T("o projeto não tem níveis", "the project has no levels"));
                            var st = Math.Abs(dir.Z) > 0.1 ? StructuralType.Brace : StructuralType.Beam;
                            var fi = doc.Create.NewFamilyInstance(Line.CreateBound(a, e), beam!, level, st);
                            GenSystem.Mark(fi, systemId, "gen.spaceframe", idx, SpaceFrameResult.RoleName(b.Role));
                            created.Add(fi.Id);
                        }
                        catch (Exception ex)
                        {
                            ctx.ItemFailed("Barras estruturais", "Structural bars", ex);
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
                            byRole.TryGetValue(b.Role, out var l);
                            if (l == null) byRole[b.Role] = l = new List<GeometryObject>();
                            l.Add(Tube(a, e, o.TubeDiameter / 2, barOpts));
                        }
                        catch (Exception ex)
                        {
                            ctx.ItemFailed("Tubos", "Tubes", ex);
                        }
                        break;
                }
                idx++;
            }
            sink.Finish();
            created.AddRange(sink.Created);
            foreach (var (role, geo) in byRole)
            {
                foreach (var chunk in geo.Chunk(2000))
                {
                    try
                    {
                        var ds = Shapes.Create(doc, chunk.ToList(), BuiltInCategory.OST_GenericModel, "TT Quadro espacial — " + SpaceFrameResult.RoleName(role));
                        GenSystem.Mark(ds, systemId, "gen.spaceframe", idx++, SpaceFrameResult.RoleName(role));
                        created.Add(ds.Id);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.ItemsFailed(Loc.T("Tubos", "Tubes"), chunk.Length, ex.Message);
                    }
                }
            }
            if (o.CreateNodes && r.Nodes.Count > 0)
            {
                var spheres = new List<GeometryObject>();
                foreach (var n in r.Nodes)
                {
                    try
                    {
                        spheres.Add(Sphere(n.Position.ToXYZ(), o.NodeDiameter / 2, nodeOpts));
                    }
                    catch (Exception ex)
                    {
                        ctx.ItemFailed("Nós", "Nodes", ex);
                    }
                }
                foreach (var chunk in spheres.Chunk(2000))
                {
                    try
                    {
                        var ds = Shapes.Create(doc, chunk.ToList(), BuiltInCategory.OST_GenericModel, Loc.T("TT Quadro espacial — nós", "TT Space frame — nodes"));
                        GenSystem.Mark(ds, systemId, "gen.spaceframe", idx++, Loc.T("Nós", "Nodes"));
                        created.Add(ds.Id);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.ItemsFailed(Loc.T("Nós", "Nodes"), chunk.Length, ex.Message);
                    }
                }
            }
            ctx.RequireCreated(created.Count, "barras do quadro espacial", "space frame bars");
            GenSystem.Register(doc, systemId, "gen.spaceframe", o, c, Loc.T("Quadro espacial", "Space frame"));
            if (o.CreateView && r.Nodes.Count > 0)
            {
                try
                {
                    var vft = Lookup.ViewFamilyType(doc, ViewFamily.ThreeDimensional);
                    if (vft != null)
                    {
                        var v = View3D.CreateIsometric(doc, vft.Id);
                        v.Name = Lookup.UniqueViewName(doc, Loc.F("TT Quadro espacial {0}", "TT Space frame {0}", systemId));
                        var min = new XYZ(r.Nodes.Min(n => n.Position.X), r.Nodes.Min(n => n.Position.Y), r.Nodes.Min(n => n.Position.Z)) - new XYZ(1, 1, 1);
                        var max = new XYZ(r.Nodes.Max(n => n.Position.X), r.Nodes.Max(n => n.Position.Y), r.Nodes.Max(n => n.Position.Z)) + new XYZ(1, 1, 1);
                        v.SetSectionBox(new BoundingBoxXYZ { Min = min, Max = max });
                        viewId = v.Id;
                        ctx.Report.Count(Loc.T("Vistas", "Views"));
                    }
                }
                catch (Exception ex)
                {
                    ctx.ItemFailed("Vista 3D do quadro", "Space frame 3D view", ex);
                }
            }
            ctx.Report.Created(created.Count);
            foreach (var id in created) ctx.Report.AddResult(id.Value);
        });
        var topZ = r.Nodes.Count > 0 ? r.Nodes.Max(n => n.Position.Z) : TopZ(o, c);
        ctx.Report.Info(Loc.F("Banzo superior na cota {0} em relação à base escolhida. Numa planta, a estrutura pode ficar acima da faixa de vista: confira na vista 3D.",
            "Top chord at {0} relative to the picked base. In a plan, the structure may be above the view range: check the 3D view.", U.FormatMm(topZ - c.BaseZ)));
        // Mostra o resultado: abre a vista 3D criada (fora da transação).
        if (viewId != null && doc.GetElement(viewId) is View3D view)
        {
            try
            {
                ctx.UiDoc.ActiveView = view;
            }
            catch (Exception ex)
            {
                Log.Warn("Abrir vista 3D do quadro: " + ex.Message);
            }
        }
        ctx.Report.Tables.Add(r.BarSchedule());
        ctx.Report.Tables.Add(r.NodeSchedule());
        ctx.Report.ForceShow = true;
    }

    /// <summary>Tubo cilíndrico entre dois pontos.</summary>
    private static Solid Tube(XYZ a, XYZ e, double radius, SolidOptions opts)
    {
        var dir = (e - a).Normalize();
        var plane = Plane.CreateByNormalAndOrigin(dir, a);
        var loop = CurveLoop.Create(new List<Curve> { Arc.Create(plane, radius, 0, Math.PI), Arc.Create(plane, radius, Math.PI, 2 * Math.PI) });
        return GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, dir, a.DistanceTo(e), opts);
    }

    /// <summary>Esfera por revolução de um semicírculo em torno do eixo vertical.</summary>
    private static Solid Sphere(XYZ c, double radius, SolidOptions opts)
    {
        var frame = new Frame(c, XYZ.BasisX, XYZ.BasisY, XYZ.BasisZ);
        var arc = Arc.Create(c - XYZ.BasisZ * radius, c + XYZ.BasisZ * radius, c + XYZ.BasisX * radius);
        var axis = Line.CreateBound(c + XYZ.BasisZ * radius, c - XYZ.BasisZ * radius);
        var loop = CurveLoop.Create(new List<Curve> { arc, axis });
        return GeometryCreationUtilities.CreateRevolvedGeometry(frame, new List<CurveLoop> { loop }, 0, 2 * Math.PI, opts);
    }

    internal static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var o = new SpaceFrameOptions();
        Json.PopulateObject(o, optionsJson);
        var c = Json.Deserialize<Context>(contextJson) ?? throw new ToolException(Loc.T("Registro do quadro espacial inválido.", "Invalid space frame record."));
        o = ctx.Ask(o, preview: x => Preview(ctx, x, c), okText: Loc.T("Regenerar", "Regenerate"), key: "gen.spaceframe");
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover sistema anterior", "Remove previous system"), () => GenSystem.Delete(ctx.Doc, systemId));
            Generate(ctx, o, c, systemId);
        });
    }
}
