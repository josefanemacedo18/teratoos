using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Organic;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Organic;

/// <summary>Categoria do Revit em que as formas orgânicas são criadas.</summary>
public enum OrganicCategory
{
    [Text("Colunas (arquitetura)", "Columns (architectural)")] Columns,
    [Text("Pilares estruturais", "Structural columns")] StructuralColumns,
    [Text("Coberturas", "Roofs")] Roofs,
    [Text("Modelo genérico", "Generic model")] GenericModel,
}

/// <summary>Conversão das malhas orgânicas em formas livres do Revit e leitura de linhas desenhadas.</summary>
internal static class OrganicGeo
{
    public static BuiltInCategory Category(OrganicCategory c) => c switch
    {
        OrganicCategory.Columns => BuiltInCategory.OST_Columns,
        OrganicCategory.StructuralColumns => BuiltInCategory.OST_StructuralColumns,
        OrganicCategory.Roofs => BuiltInCategory.OST_Roofs,
        _ => BuiltInCategory.OST_GenericModel,
    };

    /// <summary>
    /// Monta a geometria (um conjunto de faces por peça) com material por função. Tenta sólido; se o Revit
    /// recusar alguma peça, ela vira malha (continua visível e com material).
    /// </summary>
    public static IList<GeometryObject> Build(IEnumerable<OrganicMesh> meshes, Vec3 offset, Func<string, ElementId> material,
        out int failed, out string? firstError)
    {
        failed = 0;
        firstError = null;
        var b = new TessellatedShapeBuilder
        {
            Target = TessellatedShapeBuilderTarget.AnyGeometry,
            Fallback = TessellatedShapeBuilderFallback.Mesh,
            GraphicsStyleId = ElementId.InvalidElementId,
        };
        var minEdge = Tolerance.ShortCurve * 0.6;
        foreach (var m in meshes)
        {
            try
            {
                var mat = material(m.Role);
                b.OpenConnectedFaceSet(true);
                var faces = 0;
                foreach (var (i, j, k) in m.Triangles)
                {
                    var a = (m.Vertices[i] + offset).ToXYZ();
                    var c = (m.Vertices[j] + offset).ToXYZ();
                    var d = (m.Vertices[k] + offset).ToXYZ();
                    // triângulos degenerados (lados abaixo da tolerância do Revit) são descartados
                    if (a.DistanceTo(c) < minEdge || c.DistanceTo(d) < minEdge || d.DistanceTo(a) < minEdge) continue;
                    if ((c - a).CrossProduct(d - a).GetLength() < 1e-10) continue;
                    b.AddFace(new TessellatedFace(new List<XYZ> { a, c, d }, mat));
                    faces++;
                }
                if (faces == 0)
                {
                    b.CancelConnectedFaceSet();
                    failed++;
                    continue;
                }
                b.CloseConnectedFaceSet();
            }
            catch (Exception ex)
            {
                if (b.IsFaceSetOpen) b.CancelConnectedFaceSet();
                failed++;
                firstError ??= ex.Message;
            }
        }
        if (b.NumberOfCompletedFaceSets == 0) return new List<GeometryObject>();
        b.Build();
        var res = b.GetBuildResult();
        if (res.Outcome == TessellatedShapeBuilderOutcome.Nothing)
        {
            firstError ??= Loc.T("o Revit não aceitou a malha", "Revit did not accept the mesh");
            return new List<GeometryObject>();
        }
        return res.GetGeometricalObjects();
    }

    public static string Color(string role) => role switch
    {
        ColumnRole.Shaft => "#C9B79C",
        ColumnRole.Member => "#8C6A4F",
        ColumnRole.Ring => "#6B4A33",
        ColumnRole.Core => "#9AA3AE",
        ColumnRole.Base or ColumnRole.Capital => "#7D8A96",
        _ => "#B5A48C",
    };

    public static void AddToScene(Scene3 sc, IEnumerable<OrganicMesh> meshes, Vec3 offset, Func<string, string>? color = null)
    {
        foreach (var m in meshes)
        {
            var verts = offset == Vec3.Zero ? m.Vertices : m.Vertices.Select(v => v + offset).ToList();
            sc.Triangles(verts, m.Triangles, (color ?? Color)(m.Role));
        }
    }

    // ------------------------------------------------------------------ linhas desenhadas

    /// <summary>Pede linhas (de modelo ou de detalhe), sem reaproveitar a seleção atual.</summary>
    public static List<Curve> PickCurves(ToolContext ctx, string promptPt, string promptEn)
    {
        var refs = ctx.UiDoc.Selection.PickObjects(ObjectType.Element, new PredicateFilter(e => e is CurveElement),
            Loc.T(promptPt, promptEn) + Loc.T(" (Concluir na barra de opções)", " (Finish on the options bar)"));
        var curves = refs.Select(r => ctx.Doc.GetElement(r)).OfType<CurveElement>().Select(c => c.GeometryCurve).Where(c => c != null).ToList();
        if (curves.Count == 0) ctx.Cancel();
        return curves;
    }

    private static List<XYZ> Points(IEnumerable<Curve> ordered)
    {
        var pts = new List<XYZ>();
        foreach (var c in ordered)
        {
            IList<XYZ> t;
            try
            {
                t = c.Tessellate();
            }
            catch
            {
                t = new List<XYZ> { c.GetEndPoint(0), c.GetEndPoint(1) };
            }
            foreach (var p in t)
                if (pts.Count == 0 || pts[^1].DistanceTo(p) > 1e-6) pts.Add(p);
        }
        return pts;
    }

    /// <summary>Contorno fechado desenhado, nas coordenadas do plano em que foi desenhado.</summary>
    public static List<Vec2> ReadClosedOutline(List<Curve> curves)
    {
        var loop = Shapes.ChainLoops(curves, 0.02).OrderByDescending(l => l.Sum(c => c.Length)).FirstOrDefault()
                   ?? throw new ToolException(Loc.T("As linhas escolhidas não formam um contorno fechado.", "The picked lines do not form a closed outline."));
        var pts = Points(loop);
        if (pts.Count > 2 && pts[0].DistanceTo(pts[^1]) < 0.02) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 3) throw new ToolException(Loc.T("Contorno com poucos pontos.", "Outline with too few points."));
        // plano médio do contorno (normal de Newell)
        var n = XYZ.Zero;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            n += new XYZ((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        if (n.GetLength() < 1e-9) throw new ToolException(Loc.T("O contorno não tem área.", "The outline has no area."));
        n = n.Normalize();
        if (n.Z < -1e-6 || (Math.Abs(n.Z) < 1e-6 && n.Y < 0)) n = n.Negate();
        var u = Math.Abs(n.Z) > 0.9 ? XYZ.BasisX : XYZ.BasisZ.CrossProduct(n).Normalize();
        var v = n.CrossProduct(u);
        var o = pts[0];
        return pts.Select(p => new Vec2((p - o).DotProduct(u), (p - o).DotProduct(v))).ToList();
    }

    /// <summary>Linha aberta ordenada em 3D (a partir da ponta mais baixa).</summary>
    public static List<Vec3> ReadOpenPath(List<Curve> curves)
    {
        var ordered = GenHost.OrderOpenChain(curves);
        var pts = Points(ordered);
        if (pts.Count < 2) throw new ToolException(Loc.T("Linha com poucos pontos.", "Line with too few points."));
        if (pts[^1].Z < pts[0].Z - 1e-6) pts.Reverse();
        return pts.Select(p => p.ToVec3()).ToList();
    }

    /// <summary>Perfil (raio × altura): distância horizontal de cada ponto até o eixo vertical que passa pelo ponto clicado.</summary>
    public static List<Vec2> ReadProfile(List<Curve> curves, XYZ axis)
    {
        var path = ReadOpenPath(curves);
        var z0 = path.Min(p => p.Z);
        return path.Select(p => new Vec2(Math.Sqrt(Math.Pow(p.X - axis.X, 2) + Math.Pow(p.Y - axis.Y, 2)), p.Z - z0))
            .OrderBy(p => p.Y).ToList();
    }

    public static List<double[]> Pack(IEnumerable<Vec2> pts) => pts.Select(p => new[] { p.X, p.Y }).ToList();
    public static List<double[]> Pack(IEnumerable<Vec3> pts) => pts.Select(p => new[] { p.X, p.Y, p.Z }).ToList();
    public static List<Vec2> Unpack2(List<double[]>? pts) => pts?.Where(p => p.Length >= 2).Select(p => new Vec2(p[0], p[1])).ToList() ?? new List<Vec2>();
    public static List<Vec3> Unpack3(List<double[]>? pts) => pts?.Where(p => p.Length >= 3).Select(p => new Vec3(p[0], p[1], p[2])).ToList() ?? new List<Vec3>();

    /// <summary>Cota do próximo nível acima de z (mais de 15 cm acima), ou null.</summary>
    public static double? LevelAbove(Document doc, double z) =>
        Lookup.Collect<Level>(doc).Select(l => l.Elevation).Where(e => e > z + U.Mm(150)).OrderBy(e => e).Cast<double?>().FirstOrDefault();

    /// <summary>Abre (ou cria) uma vista 3D isométrica enquadrando a caixa dada.</summary>
    public static ElementId? View3DFor(ToolContext ctx, string name, XYZ min, XYZ max)
    {
        try
        {
            var vft = Lookup.ViewFamilyType(ctx.Doc, ViewFamily.ThreeDimensional);
            if (vft == null) return null;
            var v = View3D.CreateIsometric(ctx.Doc, vft.Id);
            v.Name = Lookup.UniqueViewName(ctx.Doc, name);
            var pad = new XYZ(1, 1, 1);
            v.SetSectionBox(new BoundingBoxXYZ { Min = min - pad, Max = max + pad });
            return v.Id;
        }
        catch (Exception ex)
        {
            ctx.ItemFailed("Vista 3D", "3D view", ex);
            return null;
        }
    }
}
