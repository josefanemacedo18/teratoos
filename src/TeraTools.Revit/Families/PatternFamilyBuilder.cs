using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Families;

/// <summary>Uma extrusão do padrão: polígono no plano XY de <see cref="Frame"/>, de Z0 até Z1 (ao longo da normal).</summary>
public readonly record struct PatternExtrusion(Frame3 Frame, Polygon2 Local, double Z0, double Z1);

/// <summary>
/// Cria, em tempo de execução, uma família de Modelo genérico com as extrusões do padrão
/// (sólidas para relevo, vazias para furos) e a insere no projeto. Os vazios recortam o hospedeiro
/// com o corte nativo do Revit (InstanceVoidCutUtils), em qualquer forma e também em faces curvas.
/// </summary>
public static class PatternFamilyBuilder
{
    public const string MaterialParam = "Material";

    public sealed class Result
    {
        public FamilyInstance Instance { get; init; } = null!;
        public int Created { get; init; }
        public int Failed { get; init; }
    }

    /// <summary>Monta a família, carrega e insere. Deve ser chamado FORA de transações.</summary>
    public static Result Build(ToolContext ctx, string familyName, IReadOnlyList<PatternExtrusion> items, bool isVoid, Vec3 nearPoint)
    {
        var doc = ctx.Doc;
        var app = doc.Application;
        var level = Lookup.LevelBelow(doc, nearPoint.Z + 0.01) ?? Lookup.Collect<Level>(doc).OrderBy(l => l.Elevation).FirstOrDefault()
                    ?? throw new ToolException(Loc.T("O projeto não tem níveis.", "The project has no levels."));
        // Origem da família no projeto: ponto da face na cota do nível (instância sem deslocamento).
        var origin = new Vec3(nearPoint.X, nearPoint.Y, level.Elevation);
        var template = FamilyTemplates.Find(app, TemplateKind.GenericModel, ctx.Settings.FamilyTemplateFolder)
                       ?? throw new ToolException(Loc.T(
                           "Template \"Modelo genérico\" não encontrado. Em Geral ▸ Configurações, indique a pasta de templates de família do Revit.",
                           "\"Generic Model\" template not found. In General ▸ Settings, set the Revit family templates folder."));
        var path = Path.Combine(Paths.Temp, Core.Text.TextUtil.SafeFileName(familyName) + ".rfa");
        int created = 0, failed = 0;
        var fdoc = app.NewFamilyDocument(template) ?? throw new ToolException(Loc.T("Falha ao abrir o template de família.", "Failed to open the family template."));
        try
        {
            using (var t = new Transaction(fdoc, "Téra Tools: padrão"))
            {
                t.Start();
                var owner = fdoc.OwnerFamily;
                if (isVoid) owner.get_Parameter(BuiltInParameter.FAMILY_ALLOW_CUT_WITH_VOIDS)?.Set(1);
                var fm = fdoc.FamilyManager;
                if (fm.CurrentType == null) fm.NewType(Loc.T("Padrão", "Default"));
                FamilyParameter? pMat = null;
                if (!isVoid)
                    pMat = fm.get_Parameter(MaterialParam) ?? fm.AddParameter(MaterialParam, GroupTypeId.Materials, SpecTypeId.Reference.Material, false);

                XYZ L(Vec3 w) => new(w.X - origin.X, w.Y - origin.Y, w.Z - origin.Z);
                var planes = new Dictionary<string, SketchPlane>();
                SketchPlane PlaneFor(Frame3 f)
                {
                    var n = f.ZAxis;
                    var o = L(f.Origin);
                    var key = $"{Math.Round(n.X, 6)}|{Math.Round(n.Y, 6)}|{Math.Round(n.Z, 6)}|{Math.Round(new XYZ(n.X, n.Y, n.Z).DotProduct(o), 5)}";
                    if (!planes.TryGetValue(key, out var sp))
                        planes[key] = sp = SketchPlane.Create(fdoc, Plane.CreateByNormalAndOrigin(new XYZ(n.X, n.Y, n.Z), o));
                    return sp;
                }

                CurveArray? Loop(Frame3 f, Polygon2 poly)
                {
                    var arr = new CurveArray();
                    var pts = poly.Simplify(1e-5).Points.Select(p => L(f.ToWorld(p.X, p.Y, 0))).ToList();
                    for (var i = 0; i < pts.Count; i++)
                    {
                        var a = pts[i];
                        var b = pts[(i + 1) % pts.Count];
                        if (a.DistanceTo(b) < app.ShortCurveTolerance * 1.5) continue;
                        arr.Append(Line.CreateBound(a, b));
                    }
                    return arr.Size >= 3 ? arr : null;
                }

                bool Make(Frame3 f, IReadOnlyList<Polygon2> polys, double z0, double z1)
                {
                    var profile = new CurveArrArray();
                    foreach (var p in polys)
                        if (Loop(f, p) is { } loop) profile.Append(loop);
                    if (profile.Size == 0) return false;
                    using var st = new SubTransaction(fdoc);
                    st.Start();
                    try
                    {
                        var ext = fdoc.FamilyCreate.NewExtrusion(!isVoid, profile, PlaneFor(f), z1);
                        ext.StartOffset = z0;
                        ext.EndOffset = z1;
                        if (pMat != null && ext.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM) is { } mp)
                            fm.AssociateElementParameterToFamilyParameter(mp, pMat);
                        fdoc.Regenerate();
                        st.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                        Log.Debug("Extrusão do padrão: " + ex.Message);
                        return false;
                    }
                }

                // Agrupa células coplanares com a mesma espessura numa única extrusão (muito mais rápido).
                foreach (var g in items.GroupBy(i => (FrameKey(i.Frame), Math.Round(i.Z0, 4), Math.Round(i.Z1, 4))))
                {
                    var list = g.ToList();
                    var f = list[0].Frame;
                    if (list.Count > 1 && Make(f, list.Select(x => x.Local).ToList(), list[0].Z0, list[0].Z1))
                    {
                        created += list.Count;
                        continue;
                    }
                    foreach (var it in list)
                    {
                        if (Make(it.Frame, new[] { it.Local }, it.Z0, it.Z1)) created++;
                        else failed++;
                    }
                }
                t.Commit();
            }
            if (created == 0) throw new ToolException(Loc.T("Nenhuma forma do padrão pôde ser criada (formas pequenas demais ou sobrepostas).", "No pattern shape could be created (shapes too small or overlapping)."));
            fdoc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
        }
        finally
        {
            try { fdoc.Close(false); } catch { /* já fechado */ }
        }

        Family? family = null;
        FamilyInstance? inst = null;
        ctx.Modify(Loc.T("Inserir padrão", "Place pattern"), () =>
        {
            if (!doc.LoadFamily(path, new FamilyFactory.LoadOptions(), out family) || family == null)
                family = FamilyFactory.FindFamily(doc, familyName);
            if (family == null) throw new ToolException(Loc.T("Não foi possível carregar a família do padrão.", "Could not load the pattern family."));
            var sym = family.GetFamilySymbolIds().Select(id => doc.GetElement(id)).OfType<FamilySymbol>().First();
            if (!sym.IsActive) sym.Activate();
            doc.Regenerate();
            inst = doc.Create.NewFamilyInstance(new XYZ(origin.X, origin.Y, origin.Z), sym, level, StructuralType.NonStructural);
        });
        try { File.Delete(path); } catch { /* temporário */ }
        return new Result { Instance = inst!, Created = created, Failed = failed };
    }

    private static string FrameKey(Frame3 f) =>
        $"{Math.Round(f.Origin.X, 5)}|{Math.Round(f.Origin.Y, 5)}|{Math.Round(f.Origin.Z, 5)}|{Math.Round(f.ZAxis.X, 6)}|{Math.Round(f.ZAxis.Y, 6)}|{Math.Round(f.ZAxis.Z, 6)}|{Math.Round(f.XAxis.X, 6)}|{Math.Round(f.XAxis.Y, 6)}";

    /// <summary>Exclui a família de um padrão (ao regenerar), se não estiver em uso.</summary>
    public static void DeleteFamily(Document doc, string familyName)
    {
        var fam = FamilyFactory.FindFamily(doc, familyName);
        if (fam == null) return;
        try { doc.Delete(fam.Id); } catch (Exception ex) { Log.Debug("Excluir família do padrão: " + ex.Message); }
    }
}
