using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Geometry;

internal static class Geo
{
    public static bool IsModel(Element e) => e.Category is { CategoryType: CategoryType.Model } && e is not ElementType;

    public static List<Element> PickModel(ToolContext ctx, string pt, string en) => ctx.SelectedOrPick(pt, en, IsModel);
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.join", Modules.Geometry, "Unir geometria", "Join geometry", Order = 10, Group = "Unir e recortar|Join and cut",
    DescPt = "Une a geometria de todos os pares de elementos selecionados que se tocam (paredes, pisos, vigas, massas...).",
    DescEn = "Joins the geometry of all touching pairs among the selected elements (walls, floors, beams, masses...).",
    Keywords = "unir juntar join geometria")]
public sealed class JoinCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Geo.PickModel(ctx, "Selecione os elementos a unir", "Select the elements to join");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < els.Count; i++)
            {
                var bi = RevitGeo.Box(els[i]);
                if (bi == null) continue;
                for (var j = i + 1; j < els.Count; j++)
                {
                    var bj = RevitGeo.Box(els[j]);
                    if (bj == null || bi.Max.X < bj.Min.X || bj.Max.X < bi.Min.X || bi.Max.Y < bj.Min.Y || bj.Max.Y < bi.Min.Y || bi.Max.Z < bj.Min.Z || bj.Max.Z < bi.Min.Z)
                        continue;
                    if (JoinGeometryUtils.AreElementsJoined(ctx.Doc, els[i], els[j])) continue;
                    if (Tx.TrySub(ctx.Doc, () => JoinGeometryUtils.JoinGeometry(ctx.Doc, els[i], els[j])))
                        ctx.Report.Count(Loc.T("Pares unidos", "Pairs joined"));
                }
            }
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.unjoin", Modules.Geometry, "Desunir geometria", "Unjoin geometry", Order = 11, Group = "Unir e recortar|Join and cut",
    DescPt = "Desfaz as uniões de geometria entre os elementos selecionados (ou com qualquer elemento, se só um for selecionado).",
    DescEn = "Removes geometry joins among the selected elements (or with any element if only one is selected).",
    Keywords = "desunir separar unjoin")]
public sealed class UnjoinCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Geo.PickModel(ctx, "Selecione os elementos", "Select the elements");
        var set = els.Select(e => e.Id.Value).ToHashSet();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var e in els)
            {
                foreach (var otherId in JoinGeometryUtils.GetJoinedElements(ctx.Doc, e).ToList())
                {
                    if (els.Count > 1 && !set.Contains(otherId.Value)) continue;
                    var other = ctx.Doc.GetElement(otherId);
                    if (other == null || !JoinGeometryUtils.AreElementsJoined(ctx.Doc, e, other)) continue;
                    if (Tx.TrySub(ctx.Doc, () => JoinGeometryUtils.UnjoinGeometry(ctx.Doc, e, other)))
                        ctx.Report.Count(Loc.T("Uniões desfeitas", "Joins removed"));
                }
            }
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.cut", Modules.Geometry, "Recortar com sólido/vazio", "Cut with solid/void", Order = 12, Group = "Unir e recortar|Join and cut",
    DescPt = "Recorta elementos com um sólido ou instância de vazio (corte nativo e editável do Revit).",
    DescEn = "Cuts elements with a solid or void instance (native, editable Revit cut).",
    HelpPt = "Primeiro selecione os elementos a recortar; depois escolha o elemento que recorta. Funciona com famílias de vazio (\"Cortar com vazios ao carregar\") e com sólidos que permitem corte (massas, modelos genéricos). Combinações não suportadas pelo Revit são listadas no relatório.",
    HelpEn = "First select the elements to be cut; then pick the cutting element. Works with void families and with solids that allow cutting. Unsupported combinations are listed in the report.",
    Keywords = "recortar cortar subtrair vazio void cut subtract")]
public sealed class CutCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var targets = Geo.PickModel(ctx, "Selecione os elementos a recortar", "Select the elements to cut");
        var cutter = ctx.PickElement("Escolha o elemento que recorta (sólido ou vazio)", "Pick the cutting element (solid or void)", Geo.IsModel);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var t in targets)
            {
                if (t.Id == cutter.Id) continue;
                var done = false;
                if (InstanceVoidCutUtils.CanBeCutWithVoid(t) && cutter is FamilyInstance)
                    done = Tx.TrySub(ctx.Doc, () => InstanceVoidCutUtils.AddInstanceVoidCut(ctx.Doc, t, cutter));
                if (!done && SolidSolidCutUtils.IsAllowedForSolidCut(t) && SolidSolidCutUtils.IsAllowedForSolidCut(cutter))
                    done = Tx.TrySub(ctx.Doc, () => SolidSolidCutUtils.AddCutBetweenSolids(ctx.Doc, t, cutter));
                if (done)
                {
                    ctx.Report.Modified();
                }
                else
                {
                    ctx.Report.Warn(Loc.F("O Revit não permite recortar \"{0}\" com \"{1}\". Para forma livre, use Booleanas.",
                        "Revit does not allow cutting \"{0}\" with \"{1}\". For free form, use Booleans.", Lookup.ElementLabel(t), Lookup.ElementLabel(cutter)), t.Id.Value);
                }
            }
        });
    }
}

public enum BoolOp
{
    [Text("União", "Union")] Union,
    [Text("Subtração (primeiro − demais)", "Difference (first − others)")] Difference,
    [Text("Interseção", "Intersection")] Intersection,
}

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

public sealed class BooleanOptions
{
    [Opt("Operação", "Operation")]
    public BoolOp Operation { get; set; } = BoolOp.Union;

    [Opt("Categoria do resultado", "Result category")]
    public ResultCategory Category { get; set; } = ResultCategory.GenericModel;

    [Opt("Material do resultado", "Result material", Kind = OptKind.Material, Tip = "Vazio = mantém os materiais das faces.|Empty = keeps face materials.")]
    public string Material { get; set; } = "";

    [Opt("Nome", "Name")]
    public string Name { get; set; } = "TT Forma livre";

    [Opt("Excluir os elementos originais", "Delete the original elements")]
    public bool DeleteOriginals { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.boolean", Modules.Geometry, "Booleanas (forma livre)", "Booleans (free form)", Order = 20, Large = true,
    DescPt = "União, subtração ou interseção dos sólidos dos elementos, gerando uma forma livre (DirectShape).",
    DescEn = "Union, difference or intersection of element solids, producing a free form (DirectShape).",
    HelpPt = "Limitação: o resultado é uma forma livre (geometria de apresentação com categoria, material e parâmetros), não uma família paramétrica. Operações booleanas dependem da representação geométrica: elementos sem sólidos ou com geometria inválida são ignorados e listados.",
    HelpEn = "Limitation: the result is a free form (presentation geometry with category, material and parameters), not a parametric family. Elements without solids are skipped and listed.",
    Keywords = "booleana uniao subtracao intersecao boolean union difference intersection directshape")]
public sealed class BooleanCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Geo.PickModel(ctx, "Selecione os elementos (o primeiro é a base na subtração)", "Select the elements (first is the base for difference)");
        if (els.Count < 2) ToolContext.Fail("Selecione pelo menos 2 elementos.", "Select at least 2 elements.");
        var o = ctx.Ask<BooleanOptions>();
        var solids = new List<(Element E, Solid S)>();
        foreach (var e in els)
        {
            var s = RevitGeo.UnionSolid(e);
            if (s == null || s.Volume <= 1e-9) ctx.Report.Warn(Loc.F("Sem sólido válido: {0}", "No valid solid: {0}", Lookup.ElementLabel(e)), e.Id.Value);
            else solids.Add((e, s));
        }
        if (solids.Count < 2) ToolContext.Fail("Menos de 2 elementos com sólidos válidos.", "Fewer than 2 elements with valid solids.");
        Solid result = solids[0].S;
        foreach (var (e, s) in solids.Skip(1))
        {
            try
            {
                result = BooleanOperationsUtils.ExecuteBooleanOperation(result, s, o.Operation switch
                {
                    BoolOp.Difference => BooleanOperationsType.Difference,
                    BoolOp.Intersection => BooleanOperationsType.Intersect,
                    _ => BooleanOperationsType.Union,
                });
            }
            catch (Exception ex)
            {
                ctx.Report.Warn(Loc.F("Operação com \"{0}\" falhou: {1}", "Operation with \"{0}\" failed: {1}", Lookup.ElementLabel(e), ex.Message), e.Id.Value);
            }
        }
        if (result.Volume <= 1e-9) ToolContext.Fail("O resultado ficou vazio.", "The result is empty.");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var ds = Shapes.Create(ctx.Doc, new List<GeometryObject> { result }, Cat.Of(o.Category), o.Name);
            var mat = Lookup.Material(ctx.Doc, o.Material);
            if (mat != null)
            {
                ctx.Doc.Regenerate();
                var painted = 0;
                foreach (var s in RevitGeo.Solids(ds, RevitGeo.GeometryOptions(references: true)))
                    foreach (Face f in s.Faces)
                        if (Tx.TrySub(ctx.Doc, () => ctx.Doc.Paint(ds.Id, f, mat.Id))) painted++;
                if (painted == 0) ctx.Report.Info(Loc.T("O material não pôde ser pintado nesta forma; as faces mantêm os materiais de origem.", "The material could not be painted on this form; faces keep their source materials."));
            }
            ctx.Report.Created();
            ctx.Report.AddResult(ds.Id.Value);
            ctx.Report.Info(Loc.F("Volume do resultado: {0}", "Result volume: {0}", U.FormatVolume(result.Volume)));
            if (o.DeleteOriginals)
            {
                ctx.Doc.Delete(solids.Select(x => x.E.Id).ToList());
                ctx.Report.Deleted(solids.Count);
            }
        });
    }
}

public sealed class PartsOptions
{
    [Opt("Dividir em grade", "Divide by grid")]
    public bool Divide { get; set; } = true;

    [Opt("Módulo X (comprimento)", "Module X (length)", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Divide")]
    public double SizeX { get; set; } = U.Mm(1200);

    [Opt("Módulo Y (largura/altura)", "Module Y (width/height)", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Divide")]
    public double SizeY { get; set; } = U.Mm(600);

    [Opt("Junta entre partes", "Gap between parts", Kind = OptKind.Length, Min = 0, VisibleIf = "Divide")]
    public double Gap { get; set; } = U.Mm(3);

    [Opt("Só a camada de acabamento", "Finish layer only", Tip = "Pisos: camada de cima; forros: de baixo.|Floors: top layer; ceilings: bottom layer.", VisibleIf = "Divide")]
    public bool FinishOnly { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.parts", Modules.Geometry, "Dividir em partes", "Divide into parts", Order = 30,
    DescPt = "Cria partes do Revit e as divide em grade, com junta — base para paginação e fabricação.",
    DescEn = "Creates Revit parts and divides them by a grid with gaps — basis for tiling and fabrication.",
    HelpPt = "Partes são elementos nativos: cada peça pode ter material próprio, ser excluída, ter faces deslocadas e entrar em tabelas de partes. Funciona com pisos, forros, telhados e paredes.",
    HelpEn = "Parts are native elements: each piece can have its own material, be excluded, have offset faces and appear in part schedules.",
    Keywords = "partes dividir parts divide grade")]
public sealed class PartsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = ctx.SelectedOrPick("Selecione pisos, forros, telhados ou paredes", "Select floors, ceilings, roofs or walls",
            e => e is Floor or Ceiling or RoofBase or Wall);
        var o = ctx.Ask<PartsOptions>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var host in els)
            {
                List<Part> parts;
                try
                {
                    parts = PartsService.EnsureParts(ctx.Doc, host);
                }
                catch (ToolException ex)
                {
                    ctx.Report.Warn(ex.Message, host.Id.Value);
                    continue;
                }
                ctx.Report.Count(Loc.T("Partes criadas", "Parts created"), parts.Count);
                if (!o.Divide) continue;
                var side = host is Ceiling ? PartsService.Side.Bottom : PartsService.Side.Top;
                var target = o.FinishOnly && host is not Wall ? PartsService.FinishLayer(parts, side) : parts;
                var (curves, plane) = GridCurves(host, o.SizeX, o.SizeY);
                if (curves.Count == 0) continue;
                try
                {
                    PartsService.Divide(ctx.Doc, target.Select(p => p.Id).ToList(), curves, plane, o.Gap);
                    ctx.Report.Count(Loc.T("Elementos divididos", "Elements divided"));
                    ctx.Report.AddResult(host.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("{0}: divisão {1}", "{0}: division {1}", Lookup.ElementLabel(host), Shapes.FailText(ex)), host.Id.Value);
                }
            }
        });
    }

    /// <summary>Linhas de divisão em grade sobre o elemento (planta para horizontais; plano da parede para paredes).</summary>
    internal static (List<Curve> Curves, Plane Plane) GridCurves(Element host, double sx, double sy)
    {
        var curves = new List<Curve>();
        var b = RevitGeo.Box(host)!;
        if (host is Wall w && w.Location is LocationCurve lc && lc.Curve is Line line)
        {
            var dir = line.Direction.Normalize();
            var n = dir.CrossProduct(XYZ.BasisZ);
            var p0 = line.GetEndPoint(0);
            var len = line.Length;
            var plane = Plane.CreateByNormalAndOrigin(n, p0);
            for (var s = sx; s < len - 1e-6; s += sx)
                curves.Add(Line.CreateBound(new XYZ(p0.X, p0.Y, b.Min.Z - 1) + dir * s, new XYZ(p0.X, p0.Y, b.Max.Z + 1) + dir * s));
            for (var z = b.Min.Z + sy; z < b.Max.Z - 1e-6; z += sy)
                curves.Add(Line.CreateBound(new XYZ(p0.X, p0.Y, z) - dir, new XYZ(p0.X, p0.Y, z) + dir * (len + 1)));
            return (curves, plane);
        }
        var zTop = b.Max.Z;
        var hp = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, zTop));
        for (var x = b.Min.X + sx; x < b.Max.X - 1e-6; x += sx)
            curves.Add(Line.CreateBound(new XYZ(x, b.Min.Y - 1, zTop), new XYZ(x, b.Max.Y + 1, zTop)));
        for (var y = b.Min.Y + sy; y < b.Max.Y - 1e-6; y += sy)
            curves.Add(Line.CreateBound(new XYZ(b.Min.X - 1, y, zTop), new XYZ(b.Max.X + 1, y, zTop)));
        return (curves, hp);
    }
}

public enum OpeningShape
{
    [Text("Retangular", "Rectangular")] Rectangle,
    [Text("Circular", "Circular")] Circle,
}

public sealed class OpeningOptions
{
    [Opt("Forma", "Shape")]
    public OpeningShape Shape { get; set; } = OpeningShape.Rectangle;

    [Opt("Largura / diâmetro", "Width / diameter", Kind = OptKind.Length, Min = 0.01)]
    public double Width { get; set; } = U.Mm(600);

    [Opt("Altura (ou comprimento em planta)", "Height (or length in plan)", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Shape=Rectangle")]
    public double Height { get; set; } = U.Mm(600);

    [Opt("Altura do peitoril (paredes)", "Sill height (walls)", Kind = OptKind.Length)]
    public double Sill { get; set; } = U.Mm(900);

    [Opt("Rotação (pisos/forros)", "Rotation (floors/ceilings)", Kind = OptKind.Angle)]
    public double Rotation { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.opening", Modules.Geometry, "Aberturas e vazios", "Openings and voids", Order = 40,
    DescPt = "Cria aberturas retangulares ou circulares em pisos, forros e telhados, e aberturas retangulares em paredes.",
    DescEn = "Creates rectangular or circular openings in floors, ceilings and roofs, and rectangular openings in walls.",
    HelpPt = "Escolha o hospedeiro e clique nos centros das aberturas (Esc para terminar). Em paredes, a abertura é posicionada pelo ponto clicado e pelo peitoril.",
    HelpEn = "Pick the host and click the opening centers (Esc to finish). In walls the opening uses the clicked point and the sill height.",
    Keywords = "abertura furo vazio shaft opening hole void")]
public sealed class OpeningCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var host = ctx.PickElement("Escolha o piso, forro, telhado ou parede", "Pick the floor, ceiling, roof or wall", e => e is Floor or Ceiling or RoofBase or Wall);
        var o = ctx.Ask<OpeningOptions>();
        var points = new List<XYZ>();
        while (true)
        {
            try
            {
                points.Add(ctx.PickPoint("Clique no centro da abertura (Esc termina)", "Click the opening center (Esc finishes)"));
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                break;
            }
        }
        if (points.Count == 0) ctx.Cancel();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var p in points)
            {
                try
                {
                    Element opening;
                    if (host is Wall wall && wall.Location is LocationCurve lc)
                    {
                        var curve = lc.Curve;
                        var proj = curve.Project(p).XYZPoint;
                        var dir = curve is Line l ? l.Direction : (curve.GetEndPoint(1) - curve.GetEndPoint(0)).Normalize();
                        var baseZ = (ctx.Doc.GetElement(wall.LevelId) as Level)?.Elevation ?? 0;
                        baseZ += wall.get_Parameter(BuiltInParameter.WALL_BASE_OFFSET)?.AsDouble() ?? 0;
                        var a = new XYZ(proj.X, proj.Y, baseZ + o.Sill) - dir * (o.Width / 2);
                        var b = new XYZ(proj.X, proj.Y, baseZ + o.Sill + o.Height) + dir * (o.Width / 2);
                        opening = ctx.Doc.Create.NewOpening(wall, a, b);
                    }
                    else
                    {
                        var poly = o.Shape == OpeningShape.Circle
                            ? Polygon2.Circle(p.ToVec2(), o.Width / 2, 36)
                            : Polygon2.Rectangle(p.ToVec2(), o.Width, o.Height, Core.Mathx.MathUtil.ToRadians(o.Rotation));
                        var arr = new CurveArray();
                        if (o.Shape == OpeningShape.Circle)
                        {
                            var c = p;
                            arr.Append(Arc.Create(c, o.Width / 2, 0, Math.PI, XYZ.BasisX, XYZ.BasisY));
                            arr.Append(Arc.Create(c, o.Width / 2, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
                        }
                        else
                        {
                            foreach (var curve in RevitGeo.PolygonToLoop(poly, p.Z)) arr.Append(curve);
                        }
                        opening = ctx.Doc.Create.NewOpening(host, arr, true);
                    }
                    ctx.Report.Created();
                    ctx.Report.AddResult(opening.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.F("Abertura em ({0:0.00}; {1:0.00}) {2}", "Opening at ({0:0.00}; {1:0.00}) {2}", U.ToM(p.X), U.ToM(p.Y), Shapes.FailText(ex)));
                }
            }
        });
    }
}

public enum SolidMode
{
    [Text("Extrusão", "Extrusion")] Extrusion,
    [Text("Revolução (eixo = linha escolhida)", "Revolution (axis = picked line)")] Revolution,
    [Text("Varredura (caminho = linhas escolhidas)", "Sweep (path = picked lines)")] Sweep,
}

public sealed class SolidOptionsUi
{
    [Opt("Tipo de sólido", "Solid type")]
    public SolidMode Mode { get; set; } = SolidMode.Extrusion;

    [Opt("Altura da extrusão", "Extrusion height", Kind = OptKind.Length, VisibleIf = "Mode=Extrusion")]
    public double Height { get; set; } = U.Mm(1000);

    [Opt("Ângulo da revolução", "Revolution angle", Kind = OptKind.Angle, Min = 1, Max = 360, VisibleIf = "Mode=Revolution")]
    public double Angle { get; set; } = 360;

    [Opt("Categoria", "Category")]
    public ResultCategory Category { get; set; } = ResultCategory.GenericModel;

    [Opt("Material", "Material", Kind = OptKind.Material)]
    public string Material { get; set; } = "";

    [Opt("Nome", "Name")]
    public string Name { get; set; } = "TT Sólido";

    [Opt("Excluir as linhas do perfil", "Delete the profile lines")]
    public bool DeleteLines { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.solid", Modules.Geometry, "Sólido por perfil", "Solid from profile", Order = 50,
    DescPt = "Cria sólidos por extrusão, revolução ou varredura a partir de linhas de modelo fechadas.",
    DescEn = "Creates solids by extrusion, revolution or sweep from closed model lines.",
    HelpPt = "Selecione as linhas do perfil (devem formar um ou mais contornos fechados e planos). Para revolução, depois escolha a linha do eixo; para varredura, escolha as linhas do caminho. O resultado é uma forma livre com categoria e material.",
    HelpEn = "Select the profile lines (one or more closed, planar loops). For revolution pick the axis line; for sweep pick the path lines. The result is a free form with category and material.",
    Keywords = "solido extrusao revolucao varredura perfil extrude revolve sweep profile")]
public sealed class SolidCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var lines = ctx.SelectedOrPick("Selecione as linhas do perfil", "Select the profile lines", e => e is CurveElement);
        var o = ctx.Ask<SolidOptionsUi>();
        var chains = Shapes.ChainLoops(lines.OfType<CurveElement>().Select(c => c.GeometryCurve));
        var loops = chains.Select(Shapes.ToCurveLoop).Where(l => l != null).Select(l => l!).ToList();
        if (loops.Count == 0) ToolContext.Fail("As linhas não formam um contorno fechado.", "The lines do not form a closed loop.");
        var opts = Shapes.Options(ctx.Doc, o.Material);
        Solid solid;
        switch (o.Mode)
        {
            case SolidMode.Revolution:
            {
                var axisEl = ctx.PickElement("Escolha a linha do eixo de revolução", "Pick the revolution axis line", e => e is CurveElement ce && ce.GeometryCurve is Line);
                var axis = (Line)((CurveElement)axisEl).GeometryCurve;
                var plane = loops[0].GetPlane();
                var z = axis.Direction.Normalize();
                var x = plane.Normal.CrossProduct(z).Normalize();
                var frame = new Frame(axis.GetEndPoint(0), x, z.CrossProduct(x), z);
                solid = GeometryCreationUtilities.CreateRevolvedGeometry(frame, loops, 0, Core.Mathx.MathUtil.ToRadians(o.Angle), opts);
                break;
            }
            case SolidMode.Sweep:
            {
                var pathEls = ctx.UiDoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Element,
                    new PredicateFilter(e => e is CurveElement), Loc.T("Escolha as linhas do caminho e conclua", "Pick the path lines and finish"));
                var path = new CurveLoop();
                var pc = pathEls.Select(r => ((CurveElement)ctx.Doc.GetElement(r)).GeometryCurve).ToList();
                var chain = Shapes.ChainLoops(pc).FirstOrDefault() ?? pc;
                foreach (var c in chain) path.Append(c);
                solid = GeometryCreationUtilities.CreateSweptGeometry(path, 0, path.First().GetEndParameter(0), loops, opts);
                break;
            }
            default:
            {
                var normal = loops[0].GetPlane().Normal;
                if (normal.Z < 0) normal = normal.Negate();
                solid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, normal, o.Height, opts);
                break;
            }
        }
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var ds = Shapes.Create(ctx.Doc, new List<GeometryObject> { solid }, Cat.Of(o.Category), o.Name);
            ctx.Report.Created();
            ctx.Report.AddResult(ds.Id.Value);
            if (o.DeleteLines) ctx.Doc.Delete(lines.Select(l => l.Id).ToList());
        });
    }
}

public enum ThickenSide
{
    [Text("Para fora (sentido da normal)", "Outward (normal direction)")] Out,
    [Text("Para dentro", "Inward")] In,
    [Text("Centralizada", "Centered")] Both,
}

public sealed class ThickenOptions
{
    [Opt("Espessura", "Thickness", Kind = OptKind.Length, Min = 0.001)]
    public double Thickness { get; set; } = U.Mm(20);

    [Opt("Lado", "Side")]
    public ThickenSide Side { get; set; } = ThickenSide.Out;

    [Opt("Categoria", "Category")]
    public ResultCategory Category { get; set; } = ResultCategory.GenericModel;

    [Opt("Material", "Material", Kind = OptKind.Material)]
    public string Material { get; set; } = "";

    [Opt("Nome", "Name")]
    public string Name { get; set; } = "TT Revestimento";
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.thicken", Modules.Geometry, "Aplicar espessura em face", "Thicken face", Order = 51,
    DescPt = "Cria uma placa com espessura a partir de uma face plana (revestimentos, tampos, painéis).",
    DescEn = "Creates a plate with thickness from a planar face (claddings, tops, panels).",
    Keywords = "espessura face placa revestimento thicken plate cladding")]
public sealed class ThickenCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<ThickenOptions>();
        var created = 0;
        while (true)
        {
            Reference r;
            try
            {
                r = ctx.PickFace("Escolha uma face plana (Esc termina)", "Pick a planar face (Esc finishes)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                break;
            }
            var el = ctx.Doc.GetElement(r);
            var face = el?.GetGeometryObjectFromReference(r) as PlanarFace;
            if (face == null)
            {
                Msg.Warn(Loc.T("A face escolhida não é plana.", "The picked face is not planar."));
                continue;
            }
            var t = (el as FamilyInstance)?.GetTotalTransform() ?? Transform.Identity;
            var loops = face.GetEdgesAsCurveLoops().Select(l => CurveLoop.CreateViaTransform(l, t)).ToList();
            var n = t.OfVector(face.FaceNormal).Normalize();
            var opts = Shapes.Options(ctx.Doc, o.Material);
            Solid solid;
            switch (o.Side)
            {
                case ThickenSide.In:
                    solid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, n.Negate(), o.Thickness, opts);
                    break;
                case ThickenSide.Both:
                    var shifted = loops.Select(l => CurveLoop.CreateViaTransform(l, Transform.CreateTranslation(n.Negate() * (o.Thickness / 2)))).ToList();
                    solid = GeometryCreationUtilities.CreateExtrusionGeometry(shifted, n, o.Thickness, opts);
                    break;
                default:
                    solid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, n, o.Thickness, opts);
                    break;
            }
            ctx.Modify(ctx.Tool.Name, () =>
            {
                var ds = Shapes.Create(ctx.Doc, new List<GeometryObject> { solid }, Cat.Of(o.Category), o.Name);
                ctx.Report.AddResult(ds.Id.Value);
            });
            created++;
        }
        if (created == 0) ctx.Cancel();
        ctx.Report.Created(created);
    }
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
[Tool("geo.convert", Modules.Geometry, "Converter linhas/CAD em elementos", "Convert lines/CAD to elements", Order = 60, Large = true,
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
                            ctx.Report.Warn(Shapes.FailText(ex));
                        }
                    }
                    break;
                }
                case ConvertTarget.Floors:
                case ConvertTarget.Ceilings:
                {
                    var loops = Shapes.ChainLoops(flat, 0.01).Select(Shapes.ToCurveLoop).Where(l => l != null).Select(l => l!).ToList();
                    if (loops.Count == 0) throw new ToolException(Loc.T("As linhas não formam contornos fechados.", "The lines do not form closed loops."));
                    foreach (var loop in loops)
                    {
                        try
                        {
                            Element created;
                            if (o.Target == ConvertTarget.Floors)
                            {
                                var ft = Lookup.Named<FloorType>(ctx.Doc, o.FloorType)?.Id ?? ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.FloorType);
                                created = Floor.Create(ctx.Doc, new List<CurveLoop> { loop }, ft, level.Id);
                            }
                            else
                            {
                                var ct = Lookup.Named<CeilingType>(ctx.Doc, o.CeilingType)?.Id ?? ctx.Doc.GetDefaultElementTypeId(ElementTypeGroup.CeilingType);
                                var ceiling = Ceiling.Create(ctx.Doc, new List<CurveLoop> { loop }, ct, level.Id);
                                RevitGeo.Set(ceiling, BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM, o.CeilingHeight);
                                created = ceiling;
                            }
                            ctx.Report.Created();
                            ctx.Report.AddResult(created.Id.Value);
                        }
                        catch (Exception ex)
                        {
                            ctx.Report.Warn(Shapes.FailText(ex));
                        }
                    }
                    break;
                }
                case ConvertTarget.RoomSeparators:
                {
                    var arr = new CurveArray();
                    foreach (var c in flat) arr.Append(c);
                    var sp = SketchPlane.Create(ctx.Doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z)));
                    var view = RevitGeo.IsPlanLike(ctx.ActiveView) ? ctx.ActiveView : null;
                    var res = ctx.Doc.Create.NewRoomBoundaryLines(sp, arr, view);
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
}

public enum SimplifyShape
{
    [Text("Caixa envolvente", "Bounding box")] Box,
    [Text("Contorno convexo em planta (extrudado)", "Convex outline in plan (extruded)")] Hull,
}

public sealed class SimplifyOptions
{
    [Opt("Forma simplificada", "Simplified shape")]
    public SimplifyShape Shape { get; set; } = SimplifyShape.Hull;

    [Opt("Um volume para todo o conjunto", "One volume for the whole set")]
    public bool Merge { get; set; }

    [Opt("Ocultar originais na vista", "Hide originals in view")]
    public bool HideOriginals { get; set; } = true;

    [Opt("Categoria", "Category")]
    public ResultCategory Category { get; set; } = ResultCategory.Mass;
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.simplify", Modules.Geometry, "Volume simplificado", "Simplified volume", Order = 70,
    DescPt = "Gera volumes leves (caixa ou contorno convexo) que representam elementos complexos — útil para estudos e desempenho.",
    DescEn = "Generates light volumes (box or convex outline) representing complex elements — useful for studies and performance.",
    Keywords = "simplificar volume caixa leve simplify massing")]
public sealed class SimplifyCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var els = Geo.PickModel(ctx, "Selecione os elementos complexos", "Select the complex elements");
        var o = ctx.Ask<SimplifyOptions>();
        var groups = o.Merge ? new List<List<Element>> { els } : els.Select(e => new List<Element> { e }).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var g in groups)
            {
                var boxes = g.Select(e => RevitGeo.Box(e)).Where(b => b != null).Select(b => b!).ToList();
                if (boxes.Count == 0) continue;
                var zMin = boxes.Min(b => b.Min.Z);
                var zMax = boxes.Max(b => b.Max.Z);
                Polygon2 outline;
                if (o.Shape == SimplifyShape.Box)
                {
                    outline = Rect2.FromPoints(boxes.SelectMany(b => new[] { b.Min.ToVec2(), b.Max.ToVec2() })).ToPolygon();
                }
                else
                {
                    var pts = new List<Vec2>();
                    foreach (var e in g)
                        foreach (var s in RevitGeo.Solids(e))
                            foreach (Edge ed in s.Edges)
                                foreach (var p in ed.Tessellate()) pts.Add(p.ToVec2());
                    if (pts.Count < 3) pts.AddRange(boxes.SelectMany(b => new[] { b.Min.ToVec2(), b.Max.ToVec2(), new Vec2(b.Min.X, b.Max.Y), new Vec2(b.Max.X, b.Min.Y) }));
                    outline = PolygonOps.ConvexHull(pts);
                }
                if (outline.IsEmpty || zMax - zMin < 1e-6) continue;
                var solid = Shapes.Prism(outline, zMin, zMax - zMin);
                var ds = Shapes.Create(ctx.Doc, new List<GeometryObject> { solid }, Cat.Of(o.Category), "TT Volume simplificado");
                ctx.Report.Created();
                ctx.Report.AddResult(ds.Id.Value);
            }
            if (o.HideOriginals)
            {
                var hide = els.Where(e => e.CanBeHidden(ctx.ActiveView)).Select(e => e.Id).ToList();
                if (hide.Count > 0) ctx.ActiveView.HideElements(hide);
            }
        });
    }
}

public sealed class FaceOffsetOptions
{
    [Opt("Deslocamento da face", "Face offset", Kind = OptKind.Length, Tip = "Positivo engrossa a parte; negativo afina.|Positive thickens the part; negative thins it.")]
    public double Offset { get; set; } = U.Mm(10);
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.offsetface", Modules.Geometry, "Deslocar face de parte", "Offset part face", Order = 75,
    DescPt = "Desloca faces de partes (recurso nativo para ajustar espessuras de peças de revestimento).",
    DescEn = "Offsets faces of parts (native feature to adjust thicknesses of cladding pieces).",
    HelpPt = "Limitação: o Revit só permite deslocar faces de PARTES. Crie partes primeiro (Dividir em partes ou Paginação).",
    HelpEn = "Limitation: Revit only allows offsetting faces of PARTS. Create parts first.",
    Keywords = "deslocar face offset parte part espessura")]
public sealed class FaceOffsetCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<FaceOffsetOptions>();
        var refs = ctx.UiDoc.Selection.PickObjects(Autodesk.Revit.UI.Selection.ObjectType.Face, new PredicateFilter(e => e is Part),
            Loc.T("Escolha faces de partes e conclua", "Pick part faces and finish"));
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var r in refs)
            {
                if (ctx.Doc.GetElement(r) is not Part part) continue;
                if (part.GetGeometryObjectFromReference(r) is not Face face) continue;
                if (!part.CanOffsetFace(face))
                {
                    ctx.Report.Warn(Loc.T("Esta face não pode ser deslocada.", "This face cannot be offset."), part.Id.Value);
                    continue;
                }
                part.SetFaceOffset(face, part.GetFaceOffset(face) + o.Offset);
                ctx.Report.Modified();
            }
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("geo.validate", Modules.Geometry, "Verificar geometria", "Validate geometry", Order = 80,
    DescPt = "Procura elementos sem geometria, com volume nulo, linhas curtas demais, duplicados no mesmo lugar e avisos do Revit.",
    DescEn = "Finds elements without geometry, zero volume, too-short lines, duplicates in the same place and Revit warnings.",
    Keywords = "verificar validar geometria invalida degenerada duplicado validate invalid degenerate duplicate")]
public sealed class ValidateCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var els = ctx.Selected(Geo.IsModel);
        if (els.Count == 0)
            els = new FilteredElementCollector(doc, ctx.ActiveView.Id).WhereElementIsNotElementType().Where(Geo.IsModel).ToList();
        var noGeom = new List<long>();
        var zeroVol = new List<long>();
        var shortLines = new List<long>();
        var dupKey = new Dictionary<string, List<long>>();
        foreach (var e in els)
        {
            if (e is CurveElement ce)
            {
                if (ce.GeometryCurve.Length < Tolerance.ShortCurve * 4) shortLines.Add(e.Id.Value);
                continue;
            }
            if (e is FamilyInstance or HostObject or DirectShape)
            {
                var solids = RevitGeo.Solids(e);
                if (solids.Count == 0 && e is not FamilyInstance { Symbol.Family.FamilyPlacementType: FamilyPlacementType.ViewBased }) noGeom.Add(e.Id.Value);
                else if (solids.Sum(s => s.Volume) < 1e-6 && e is HostObject) zeroVol.Add(e.Id.Value);
            }
            if (e is FamilyInstance fi && fi.Location is LocationPoint lp)
            {
                var k = $"{fi.Symbol.Id.Value}|{Math.Round(lp.Point.X, 3)}|{Math.Round(lp.Point.Y, 3)}|{Math.Round(lp.Point.Z, 3)}|{Math.Round(lp.Rotation, 3)}";
                if (!dupKey.TryGetValue(k, out var l)) dupKey[k] = l = new List<long>();
                l.Add(e.Id.Value);
            }
        }
        if (noGeom.Count > 0) ctx.Report.Warn(Loc.F("{0} elemento(s) sem geometria sólida.", "{0} element(s) without solid geometry.", noGeom.Count), noGeom);
        if (zeroVol.Count > 0) ctx.Report.Warn(Loc.F("{0} elemento(s) hospedeiro(s) com volume nulo.", "{0} host element(s) with zero volume.", zeroVol.Count), zeroVol);
        if (shortLines.Count > 0) ctx.Report.Warn(Loc.F("{0} linha(s) muito curta(s) (podem causar falhas).", "{0} very short line(s) (may cause failures).", shortLines.Count), shortLines);
        foreach (var d in dupKey.Values.Where(v => v.Count > 1))
            ctx.Report.Warn(Loc.F("{0} instâncias idênticas no mesmo lugar.", "{0} identical instances in the same place.", d.Count), d);

        var warnings = doc.GetWarnings();
        if (warnings.Count > 0)
        {
            var t = new TableData(Loc.T("Avisos do Revit", "Revit warnings"));
            t.AddColumn(Loc.T("Aviso", "Warning")).AddColumn(Loc.T("Elementos", "Elements"), ColumnKind.Integer);
            foreach (var w in warnings.Take(2000))
            {
                var ids = w.GetFailingElements().Concat(w.GetAdditionalElements()).Select(i => i.Value).ToList();
                t.AddRowWithIds(ids, w.GetDescriptionText(), ids.Count);
            }
            ctx.Report.Tables.Add(t);
            ctx.Report.Info(Loc.F("O projeto tem {0} aviso(s) do Revit.", "The project has {0} Revit warning(s).", warnings.Count));
        }
        if (!ctx.Report.HasWarnings) ctx.Report.Success(Loc.F("{0} elementos verificados sem problemas de geometria.", "{0} elements checked with no geometry problems.", els.Count));
        ctx.Report.ForceShow = true;
    }
}
