using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using TeraTools.Core.Localization;
using TeraTools.Core.Units;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Families;

public enum BoxKind
{
    /// <summary>TT_Bloco — modelo genérico paramétrico (blocos, aletas, painéis, módulos).</summary>
    Block,
    /// <summary>TT_Chapa — peça de marcenaria (categoria Marcenaria) com parâmetros de corte.</summary>
    Board,
}

/// <summary>
/// Gera as famílias paramétricas do Téra Tools a partir do template de Modelo Genérico:
/// caixa com origem no canto, largura (X), profundidade (Y) e altura (Z) controladas por
/// parâmetros compartilhados, material por instância e baseada em plano de trabalho
/// (pode ser colocada em qualquer orientação). Cada família passa por um teste de flexão
/// antes de ser usada; as famílias ficam em cache em %AppData%\TeraTools\familias.
/// </summary>
public static class FamilyFactory
{
    public const string BlockFamily = "TT_Bloco";
    public const string BoardFamily = "TT_Chapa";
    public const int RecipeVersion = 1;

    private static readonly double W0 = U.Mm(600);
    private static readonly double D0 = U.Mm(400);
    private static readonly double H0 = U.Mm(300);

    public sealed class LoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = false;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            source = FamilySource.Family;
            overwriteParameterValues = false;
            return true;
        }
    }

    public static string FamilyName(BoxKind kind) => kind == BoxKind.Board ? BoardFamily : BlockFamily;

    public static string CachePath(BoxKind kind) =>
        Path.Combine(Paths.Ensure(Path.Combine(Paths.Families, "v" + RecipeVersion)), FamilyName(kind) + ".rfa");

    public static Family? FindFamily(Document doc, string name) =>
        new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(f => f.Name == name);

    /// <summary>
    /// Garante a família no projeto e devolve um tipo ativo. Deve ser chamada FORA de transações
    /// (abre a própria transação para carregar a família).
    /// </summary>
    public static FamilySymbol Ensure(Document doc, BoxKind kind)
    {
        var name = FamilyName(kind);
        var fam = FindFamily(doc, name);
        if (fam == null)
        {
            var path = CachePath(kind);
            if (!File.Exists(path)) Build(doc.Application, kind, path);
            Tx.Run(doc, "Téra Tools: carregar " + name, () =>
            {
                if (!doc.LoadFamily(path, new LoadOptions(), out fam) || fam == null)
                    fam = FindFamily(doc, name);
            });
            if (fam == null)
                throw new ToolException(Loc.F("Não foi possível carregar a família {0}.", "Could not load family {0}.", name));
        }
        var symbol = fam.GetFamilySymbolIds().Select(id => doc.GetElement(id)).OfType<FamilySymbol>().FirstOrDefault()
                     ?? throw new ToolException(Loc.F("A família {0} não tem tipos.", "Family {0} has no types.", name));
        if (!symbol.IsActive)
            Tx.Run(doc, "Téra Tools: ativar " + name, () => symbol.Activate());
        return symbol;
    }

    /// <summary>Apaga o cache (força recriar as famílias na próxima vez).</summary>
    public static void ClearCache()
    {
        foreach (var k in new[] { BoxKind.Block, BoxKind.Board })
        {
            try
            {
                if (File.Exists(CachePath(k))) File.Delete(CachePath(k));
            }
            catch
            {
                // ignora
            }
        }
    }

    private static void Build(Application app, BoxKind kind, string path)
    {
        var template = FamilyTemplates.Find(app, TemplateKind.GenericModel, Services.Settings.FamilyTemplateFolder)
                       ?? throw new ToolException(Loc.T(
                           "Template de família \"Modelo genérico\" não encontrado. Em Configurações, indique a pasta de templates de família do Revit (ex.: C:\\ProgramData\\Autodesk\\RVT 2027\\Family Templates).",
                           "\"Generic Model\" family template not found. In Settings, set the Revit family templates folder (e.g., C:\\ProgramData\\Autodesk\\RVT 2027\\Family Templates)."));
        Log.Info($"Criando família {FamilyName(kind)} a partir de {template}");

        var names = new List<string> { SharedParams.Largura, SharedParams.Profundidade, SharedParams.Altura, SharedParams.Material, SharedParams.Codigo };
        if (kind == BoxKind.Board)
            names.AddRange(new[] { SharedParams.Comprimento, SharedParams.Espessura, SharedParams.Peca, SharedParams.Modulo, SharedParams.FitaC1, SharedParams.FitaC2, SharedParams.FitaL1, SharedParams.FitaL2, SharedParams.Veio });
        Dictionary<string, ExternalDefinition> defs;
        try
        {
            defs = SharedParams.Definitions(app, names);
        }
        catch (Exception ex)
        {
            Log.Warn("Parâmetros compartilhados indisponíveis, usando parâmetros de família: " + ex.Message);
            defs = new Dictionary<string, ExternalDefinition>();
        }

        var fdoc = app.NewFamilyDocument(template) ?? throw new ToolException(Loc.T("Falha ao abrir o template de família.", "Failed to open the family template."));
        try
        {
            Extrusion ext;
            FamilyParameter pW, pD, pH;
            using (var t = new Transaction(fdoc, "Téra Tools: família"))
            {
                t.Start();
                var fm = fdoc.FamilyManager;
                var owner = fdoc.OwnerFamily;
                if (kind == BoxKind.Board)
                {
                    var cw = Category.GetCategory(fdoc, BuiltInCategory.OST_Casework);
                    if (cw != null) owner.FamilyCategory = cw;
                }
                owner.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED)?.Set(1);
                owner.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL)?.Set(0);

                FamilyParameter Add(string n)
                {
                    var existing = fm.get_Parameter(n);
                    if (existing != null) return existing;
                    var d = SharedParams.Get(n);
                    if (defs.TryGetValue(n, out var ed))
                        return fm.AddParameter(ed, SharedParams.GroupFor(d.Kind), true);
                    var spec = d.Kind switch
                    {
                        SharedParams.Kind.Length => SpecTypeId.Length,
                        SharedParams.Kind.Material => SpecTypeId.Reference.Material,
                        SharedParams.Kind.YesNo => SpecTypeId.Boolean.YesNo,
                        SharedParams.Kind.Integer => SpecTypeId.Int.Integer,
                        _ => SpecTypeId.String.Text,
                    };
                    return fm.AddParameter(n, SharedParams.GroupFor(d.Kind), spec, true);
                }

                if (fm.CurrentType == null) fm.NewType(Loc.T("Padrão", "Default"));
                pW = Add(SharedParams.Largura);
                pD = Add(SharedParams.Profundidade);
                pH = Add(SharedParams.Altura);
                var pM = Add(SharedParams.Material);
                foreach (var n in names.Skip(4)) Add(n);
                fm.Set(pW, W0);
                fm.Set(pD, D0);
                fm.Set(pH, H0);

                var level = new FilteredElementCollector(fdoc).OfClass(typeof(Level)).Cast<Level>().FirstOrDefault()
                            ?? throw new ToolException(Loc.T("Template sem nível de referência.", "Template without reference level."));
                var plan = new FilteredElementCollector(fdoc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                               .FirstOrDefault(v => !v.IsTemplate && v.GenLevel != null && v.GenLevel.Id == level.Id)
                           ?? new FilteredElementCollector(fdoc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().First(v => !v.IsTemplate);

                var sp = SketchPlane.Create(fdoc, level.GetPlaneReference());
                var arr = new CurveArray();
                arr.Append(Line.CreateBound(new XYZ(0, 0, 0), new XYZ(W0, 0, 0)));
                arr.Append(Line.CreateBound(new XYZ(W0, 0, 0), new XYZ(W0, D0, 0)));
                arr.Append(Line.CreateBound(new XYZ(W0, D0, 0), new XYZ(0, D0, 0)));
                arr.Append(Line.CreateBound(new XYZ(0, D0, 0), new XYZ(0, 0, 0)));
                var profile = new CurveArrArray();
                profile.Append(arr);
                ext = fdoc.FamilyCreate.NewExtrusion(true, profile, sp, H0);
                ext.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM)?.Set(0.0);
                fm.AssociateElementParameterToFamilyParameter(ext.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), pH);
                var matParam = ext.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                if (matParam != null) fm.AssociateElementParameterToFamilyParameter(matParam, pM);

                var planes = new FilteredElementCollector(fdoc).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().ToList();
                var x0 = planes.FirstOrDefault(p => PlaneAt(p, XYZ.BasisX, 0)) ?? fdoc.FamilyCreate.NewReferencePlane(new XYZ(0, -1, 0), new XYZ(0, 1, 0), XYZ.BasisZ, plan);
                var y0 = planes.FirstOrDefault(p => PlaneAt(p, XYZ.BasisY, 0)) ?? fdoc.FamilyCreate.NewReferencePlane(new XYZ(-1, 0, 0), new XYZ(1, 0, 0), XYZ.BasisZ, plan);
                var x1 = fdoc.FamilyCreate.NewReferencePlane(new XYZ(W0, -1, 0), new XYZ(W0, 1, 0), XYZ.BasisZ, plan);
                var y1 = fdoc.FamilyCreate.NewReferencePlane(new XYZ(-1, D0, 0), new XYZ(1, D0, 0), XYZ.BasisZ, plan);
                x1.Name = "TT Largura";
                y1.Name = "TT Profundidade";
                fdoc.Regenerate();

                var faces = new List<PlanarFace>();
                var ge = ext.get_Geometry(new Options { ComputeReferences = true, IncludeNonVisibleObjects = true });
                foreach (var obj in ge)
                    if (obj is Solid s)
                        foreach (Face f in s.Faces)
                            if (f is PlanarFace pf) faces.Add(pf);
                PlanarFace FaceFor(XYZ normal, double at) =>
                    faces.FirstOrDefault(f => f.FaceNormal.IsAlmostEqualTo(normal) && Math.Abs(f.Origin.DotProduct(normal) - at) < 1e-6)
                    ?? throw new ToolException(Loc.T("Faces da extrusão não encontradas.", "Extrusion faces not found."));
                fdoc.FamilyCreate.NewAlignment(plan, x0.GetReference(), FaceFor(XYZ.BasisX.Negate(), 0).Reference);
                fdoc.FamilyCreate.NewAlignment(plan, x1.GetReference(), FaceFor(XYZ.BasisX, W0).Reference);
                fdoc.FamilyCreate.NewAlignment(plan, y0.GetReference(), FaceFor(XYZ.BasisY.Negate(), 0).Reference);
                fdoc.FamilyCreate.NewAlignment(plan, y1.GetReference(), FaceFor(XYZ.BasisY, D0).Reference);

                var rw = new ReferenceArray();
                rw.Append(x0.GetReference());
                rw.Append(x1.GetReference());
                var dw = fdoc.FamilyCreate.NewDimension(plan, Line.CreateBound(new XYZ(0, -0.6, 0), new XYZ(W0, -0.6, 0)), rw);
                dw.FamilyLabel = pW;
                var rd = new ReferenceArray();
                rd.Append(y0.GetReference());
                rd.Append(y1.GetReference());
                var dd = fdoc.FamilyCreate.NewDimension(plan, Line.CreateBound(new XYZ(-0.6, 0, 0), new XYZ(-0.6, D0, 0)), rd);
                dd.FamilyLabel = pD;
                t.Commit();
            }

            FlexTest(fdoc, ext, pW, pD, pH);
            fdoc.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
            Log.Info($"Família criada: {path}");
        }
        finally
        {
            try
            {
                fdoc.Close(false);
            }
            catch
            {
                // ignora
            }
        }
    }

    private static bool PlaneAt(ReferencePlane p, XYZ normal, double offset) =>
        Math.Abs(Math.Abs(p.Normal.DotProduct(normal)) - 1) < 1e-6 && Math.Abs(p.BubbleEnd.DotProduct(normal) - offset) < 1e-6;

    /// <summary>Muda os parâmetros e confere se a geometria acompanha; desfaz ao final.</summary>
    private static void FlexTest(Document fdoc, Extrusion ext, FamilyParameter pW, FamilyParameter pD, FamilyParameter pH)
    {
        using var t = new Transaction(fdoc, "Téra Tools: teste de flexão");
        t.Start();
        var fm = fdoc.FamilyManager;
        var w = W0 * 1.7;
        var d = D0 * 0.55;
        var h = H0 * 1.45;
        fm.Set(pW, w);
        fm.Set(pD, d);
        fm.Set(pH, h);
        fdoc.Regenerate();
        var bb = ext.get_BoundingBox(null);
        var ok = bb != null
                 && Math.Abs(bb.Max.X - bb.Min.X - w) < 1e-4
                 && Math.Abs(bb.Max.Y - bb.Min.Y - d) < 1e-4
                 && Math.Abs(bb.Max.Z - bb.Min.Z - h) < 1e-4
                 && Math.Abs(bb.Min.X) < 1e-4 && Math.Abs(bb.Min.Y) < 1e-4;
        t.RollBack();
        if (!ok)
            throw new ToolException(Loc.T("A família paramétrica gerada não passou no teste de flexão.", "The generated parametric family failed the flex test."));
    }
}
