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
/// <remarks>
/// Cuidados que evitam o erro "nenhuma forma pôde ser criada":
/// • os planos de esboço são criados na transação principal — se fossem criados dentro de uma
///   subtransação que falha, seriam apagados junto e todas as formas seguintes falhariam;
/// • os contornos são limpos antes (vértices próximos fundidos), porque pular um segmento curto
///   deixa o laço aberto e o Revit recusa;
/// • as formas vão em lotes; um lote recusado é dividido ao meio até isolar só a forma com defeito.
/// </remarks>
public static class PatternFamilyBuilder
{
    public const string MaterialParam = "Material";

    /// <summary>Formas por extrusão (esboços grandes demais deixam o Revit lento).</summary>
    private const int BatchSize = 150;

    public sealed class Result
    {
        /// <summary>Instância da família; nula quando a família não aceitou nenhuma forma.</summary>
        public FamilyInstance? Instance { get; init; }
        public int Created { get; init; }
        public int Failed { get; init; }
        /// <summary>Primeiro motivo de recusa informado pelo Revit.</summary>
        public string? FirstError { get; init; }
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
        var minEdge = app.ShortCurveTolerance * 2;
        int created = 0, failed = 0;
        string? firstError = null;

        // Limpa os contornos antes de abrir a família (formas degeneradas nem chegam ao Revit).
        var clean = new List<PatternExtrusion>(items.Count);
        foreach (var it in items)
        {
            if (it.Z1 - it.Z0 <= minEdge || it.Local.CleanForSketch(minEdge) is not { } poly)
            {
                failed++;
                firstError ??= Loc.T("forma pequena demais ou com contorno inválido", "shape too small or with an invalid outline");
                continue;
            }
            clean.Add(it with { Local = poly });
        }
        if (clean.Count == 0) return new Result { Instance = null, Created = 0, Failed = failed, FirstError = firstError };

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

                // Planos de esboço: criados AQUI, na transação principal, nunca dentro de uma subtransação.
                var planes = new Dictionary<string, SketchPlane>();
                foreach (var it in clean)
                {
                    var key = FrameKey(it.Frame);
                    if (planes.ContainsKey(key)) continue;
                    var n = it.Frame.ZAxis;
                    planes[key] = SketchPlane.Create(fdoc, Plane.CreateByNormalAndOrigin(new XYZ(n.X, n.Y, n.Z), L(it.Frame.Origin)));
                }
                fdoc.Regenerate();

                CurveArray Loop(Frame3 f, Polygon2 poly)
                {
                    var arr = new CurveArray();
                    var pts = poly.Points.Select(p => L(f.ToWorld(p.X, p.Y, 0))).ToList();
                    for (var i = 0; i < pts.Count; i++) arr.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
                    return arr;
                }

                bool Make(IReadOnlyList<PatternExtrusion> batch)
                {
                    var f = batch[0].Frame;
                    var sp = planes[FrameKey(f)];
                    using var st = new SubTransaction(fdoc);
                    st.Start();
                    try
                    {
                        var profile = new CurveArrArray();
                        foreach (var it in batch) profile.Append(Loop(f, it.Local));
                        var ext = fdoc.FamilyCreate.NewExtrusion(!isVoid, profile, sp, batch[0].Z1);
                        ext.StartOffset = batch[0].Z0;
                        ext.EndOffset = batch[0].Z1;
                        if (pMat != null && ext.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM) is { } mp)
                            fm.AssociateElementParameterToFamilyParameter(mp, pMat);
                        fdoc.Regenerate();
                        st.Commit();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        if (st.HasStarted() && !st.HasEnded()) st.RollBack();
                        if (batch.Count == 1) firstError ??= ex.Message;
                        return false;
                    }
                }

                // Lote recusado → divide ao meio até isolar a forma com defeito.
                void Place(IReadOnlyList<PatternExtrusion> batch)
                {
                    if (Make(batch))
                    {
                        created += batch.Count;
                        return;
                    }
                    if (batch.Count == 1)
                    {
                        failed++;
                        return;
                    }
                    var half = batch.Count / 2;
                    Place(batch.Take(half).ToList());
                    Place(batch.Skip(half).ToList());
                }

                // Formas no mesmo plano e com a mesma espessura vão juntas numa extrusão (família leve).
                foreach (var g in clean.GroupBy(i => (FrameKey(i.Frame), Math.Round(i.Z0, 4), Math.Round(i.Z1, 4))))
                    foreach (var batch in g.Chunk(BatchSize))
                        Place(batch);

                t.Commit();
            }
            // Nenhuma forma entrou na família: quem chamou decide (forma livre ou mensagem com o motivo).
            if (created == 0) return new Result { Instance = null, Created = 0, Failed = failed, FirstError = firstError };
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
        return new Result { Instance = inst, Created = created, Failed = failed, FirstError = firstError };
    }

    /// <summary>
    /// Sólidos do padrão em relevo, sem família (forma livre), para quando a família não aceitar as formas.
    /// Não precisa de transação: só monta a geometria, já com o material escolhido.
    /// </summary>
    public static List<GeometryObject> Solids(Document doc, IReadOnlyList<PatternExtrusion> items, string? materialName, out int failed, out string? firstError)
    {
        var opts = Shapes.Options(doc, materialName);
        var minEdge = doc.Application.ShortCurveTolerance * 2;
        var list = new List<GeometryObject>(items.Count);
        failed = 0;
        firstError = null;
        foreach (var it in items)
        {
            try
            {
                var height = it.Z1 - it.Z0;
                var poly = it.Local.CleanForSketch(minEdge);
                if (poly == null || height <= minEdge)
                    throw new InvalidOperationException(Loc.T("forma pequena demais ou com contorno inválido", "shape too small or with an invalid outline"));
                var pts = poly.Points.Select(p => it.Frame.ToWorld(p.X, p.Y, it.Z0).ToXYZ()).ToList();
                var loop = new CurveLoop();
                for (var i = 0; i < pts.Count; i++) loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));
                list.Add(GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, it.Frame.ZAxis.ToXYZ(), height, opts));
            }
            catch (Exception ex)
            {
                failed++;
                firstError ??= ex.Message;
            }
        }
        return list;
    }

    private static string FrameKey(Frame3 f) =>
        $"{Math.Round(f.Origin.X, 5)}|{Math.Round(f.Origin.Y, 5)}|{Math.Round(f.Origin.Z, 5)}|{Math.Round(f.ZAxis.X, 6)}|{Math.Round(f.ZAxis.Y, 6)}|{Math.Round(f.ZAxis.Z, 6)}|{Math.Round(f.XAxis.X, 6)}|{Math.Round(f.XAxis.Y, 6)}";

    /// <summary>Exclui a família de um padrão (ao regenerar), se não estiver em uso.</summary>
    public static void DeleteFamily(Document doc, string familyName)
    {
        var fam = FamilyFactory.FindFamily(doc, familyName);
        if (fam == null) return;
        try { doc.Delete(fam.Id); } catch (Exception ex) { Log.Warn("Excluir família do padrão: " + ex.Message); }
    }
}
