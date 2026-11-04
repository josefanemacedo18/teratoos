using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using TeraTools.Core.Colors;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Families;

public enum GenOutput
{
    [Text("Família paramétrica (BIM editável)", "Parametric family (editable BIM)")] Parametric,
    [Text("Forma livre leve (DirectShape)", "Light free form (DirectShape)")] FreeForm,
    [Text("Família/tipo escolhido no projeto", "Family/type chosen in the project")] UserFamily,
}

/// <summary>Materiais criados/atualizados pelo plugin.</summary>
public static class MaterialsService
{
    /// <summary>Cria ou atualiza um material com cor de sombreamento (deve estar em transação).</summary>
    public static Material Ensure(Document doc, string name, RgbColor color, int transparency = 0, int shininess = 20, bool updateColor = true)
    {
        var m = Lookup.Material(doc, name);
        if (m == null)
        {
            var id = Material.Create(doc, name);
            m = (Material)doc.GetElement(id);
            updateColor = true;
        }
        if (updateColor)
        {
            m.Color = new Color(color.R, color.G, color.B);
            m.Transparency = Math.Clamp(transparency, 0, 100);
            m.Shininess = Math.Clamp(shininess, 0, 128);
            m.SurfaceForegroundPatternColor = new Color(color.R, color.G, color.B);
        }
        return m;
    }

    public static RgbColor ColorOf(Material m) => m.Color.IsValid ? new RgbColor(m.Color.Red, m.Color.Green, m.Color.Blue) : RgbColor.Gray;
}

/// <summary>Registro e marcação de sistemas gerados (permite editar/regenerar depois).</summary>
public static class GenSystem
{
    public static string NewId() => Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();

    private static readonly HashSet<string> BoundDocs = new();

    /// <summary>Vincula os parâmetros de identificação do Téra Tools às categorias (uma vez por documento/sessão).</summary>
    public static void BindParams(Document doc, Core.Reporting.OperationReport? report)
    {
        var key = doc.PathName + "|" + doc.Title;
        if (BoundDocs.Contains(key)) return;
        try
        {
            SharedParams.BindToProject(doc, new[] { SharedParams.Sistema, SharedParams.Indice, SharedParams.Status, SharedParams.Codigo, SharedParams.Abertura, SharedParams.Grupo, SharedParams.Material, SharedParams.Ambiente }, SharedParams.GeneratedCategories);
            BoundDocs.Add(key);
        }
        catch (Exception ex)
        {
            Log.Warn("Falha ao vincular parâmetros do projeto: " + ex.Message);
            report?.Info(Loc.T("Parâmetros de identificação não puderam ser vinculados (o sistema continua editável pelo plugin).",
                "Identification parameters could not be bound (the system remains editable by the plugin)."));
        }
    }

    public static void Mark(Element e, string systemId, string toolId, int index, string? status = null)
    {
        try
        {
            ProjectStorage.MarkGenerated(e, systemId, toolId, index);
        }
        catch (Exception ex)
        {
            Log.Debug("MarkGenerated: " + ex.Message);
        }
        RevitGeo.Set(e, systemId, SharedParams.Sistema);
        RevitGeo.Set(e, index, SharedParams.Indice);
        if (status != null) RevitGeo.Set(e, status, SharedParams.Status);
    }

    public static void Register(Document doc, string systemId, string toolId, object options, object context, string? name)
    {
        ProjectStorage.RegisterSystem(doc, systemId, new ProjectStorage.GeneratedSystem
        {
            ToolId = toolId,
            OptionsJson = Json.Serialize(options, options.GetType(), indented: false),
            ContextJson = Json.Serialize(context, context.GetType(), indented: false),
            Name = name,
        });
    }

    public static int Delete(Document doc, string systemId)
    {
        var ids = ProjectStorage.FindSystem(doc, systemId).Select(e => e.Id).ToList();
        if (ids.Count > 0) doc.Delete(ids);
        return ids.Count;
    }
}

/// <summary>
/// Destino das caixas geradas: família paramétrica TT_Bloco/TT_Chapa, forma livre agrupada
/// por material, ou uma família escolhida pelo usuário (com parâmetros por nome).
/// </summary>
public sealed class BoxSink
{
    private readonly ToolContext _ctx;
    private readonly BoxKind _kind;
    private readonly string? _userSymbolKey;
    private readonly BuiltInCategory _freeCategory;
    private readonly string _systemId;
    private readonly string _status;
    private readonly Dictionary<string, SketchPlane> _planes = new();
    private readonly Dictionary<string, List<GeometryObject>> _free = new();
    private FamilySymbol? _symbol;

    public GenOutput Output { get; private set; }
    public List<ElementId> Created { get; } = new();
    public string Name { get; set; } = "TT";

    public BoxSink(ToolContext ctx, GenOutput output, BoxKind kind, string systemId, string? userSymbolKey = null,
        BuiltInCategory freeCategory = BuiltInCategory.OST_GenericModel, string? status = null)
    {
        _ctx = ctx;
        Output = output;
        _kind = kind;
        _userSymbolKey = userSymbolKey;
        _freeCategory = freeCategory;
        _systemId = systemId;
        _status = status ?? "";
    }

    /// <summary>Prepara famílias (fora de transação). Cai para forma livre se a família falhar.</summary>
    public void Prepare()
    {
        var doc = _ctx.Doc;
        if (Output == GenOutput.Parametric)
        {
            try
            {
                _symbol = FamilyFactory.Ensure(doc, _kind);
            }
            catch (Exception ex)
            {
                Log.Error("Família paramétrica indisponível", ex);
                _ctx.Report.Warn(Loc.F("Família paramétrica indisponível ({0}). Gerado como forma livre.",
                    "Parametric family unavailable ({0}). Generated as free form.", ex.Message));
                Output = GenOutput.FreeForm;
            }
        }
        else if (Output == GenOutput.UserFamily)
        {
            _symbol = Lookup.Symbol(doc, _userSymbolKey);
            if (_symbol == null)
                throw new ToolException(Loc.T("Escolha a família/tipo a usar.", "Choose the family/type to use."));
            if (!_symbol.IsActive) Tx.Run(doc, "Téra Tools: ativar tipo", () => _symbol.Activate());
        }
    }

    /// <summary>Adiciona uma caixa (dentro de uma transação).</summary>
    public Element? Add(OrientedBox box, string? material, int index, Action<Element>? extra = null)
    {
        if (box.SizeX < Tolerance.ShortCurve || box.SizeY < Tolerance.ShortCurve || box.SizeZ < Tolerance.ShortCurve) return null;
        var doc = _ctx.Doc;
        if (Output == GenOutput.FreeForm)
        {
            var key = material ?? "";
            if (!_free.TryGetValue(key, out var list)) _free[key] = list = new List<GeometryObject>();
            try
            {
                list.Add(Shapes.Box(box, Shapes.Options(doc, material)));
            }
            catch (Exception ex)
            {
                Log.Debug("Box solid: " + ex.Message);
            }
            return null;
        }

        FamilyInstance fi;
        var symbol = _symbol!;
        var placement = symbol.Family.FamilyPlacementType;
        if (Output == GenOutput.Parametric || placement is FamilyPlacementType.WorkPlaneBased)
        {
            var n = box.Frame.ZAxis.ToXYZ();
            var o = box.Frame.Origin.ToXYZ();
            var key = $"{Math.Round(n.X, 6)}|{Math.Round(n.Y, 6)}|{Math.Round(n.Z, 6)}|{Math.Round(n.DotProduct(o), 5)}";
            if (!_planes.TryGetValue(key, out var sp))
            {
                sp = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(n, o));
                _planes[key] = sp;
            }
            var origin = Output == GenOutput.Parametric ? o : box.Center.ToXYZ() - n * (box.SizeZ / 2);
            fi = doc.Create.NewFamilyInstance(sp.GetPlaneReference(), origin, box.Frame.XAxis.ToXYZ(), symbol);
        }
        else
        {
            if (!box.IsUpright)
                throw new ToolException(Loc.T("A família escolhida não é baseada em plano de trabalho e não pode ser inclinada.", "The chosen family is not work-plane based and cannot be tilted."));
            var c = box.Center.ToXYZ();
            var basePt = new XYZ(c.X, c.Y, box.Frame.Origin.Z);
            var level = Lookup.LevelBelow(doc, basePt.Z) ?? throw new ToolException(Loc.T("Nenhum nível no projeto.", "No level in the project."));
            fi = doc.Create.NewFamilyInstance(basePt, symbol, level, StructuralType.NonStructural);
            var rot = box.Frame.PlanRotation;
            if (Math.Abs(rot) > 1e-9)
                ElementTransformUtils.RotateElement(doc, fi.Id, Line.CreateBound(basePt, basePt + XYZ.BasisZ), rot);
        }

        if (Output == GenOutput.Parametric)
        {
            RevitGeo.Set(fi, box.SizeX, SharedParams.Largura);
            RevitGeo.Set(fi, box.SizeY, SharedParams.Profundidade);
            RevitGeo.Set(fi, box.SizeZ, SharedParams.Altura);
        }
        else
        {
            RevitGeo.Set(fi, box.SizeX, SharedParams.Largura, "Largura", "Width", "Comprimento", "Length");
            RevitGeo.Set(fi, box.SizeY, SharedParams.Profundidade, "Profundidade", "Depth");
            RevitGeo.Set(fi, box.SizeZ, SharedParams.Altura, "Altura", "Height", "Espessura", "Thickness");
        }
        if (!string.IsNullOrEmpty(material) && Lookup.Material(doc, material) is { } m)
            RevitGeo.Set(fi, m.Id, SharedParams.Material, "Material");
        GenSystem.Mark(fi, _systemId, _ctx.Tool.Id, index, _status.Length > 0 ? _status : null);
        extra?.Invoke(fi);
        Created.Add(fi.Id);
        return fi;
    }

    /// <summary>Adiciona um sólido qualquer à forma livre (formas não retangulares).</summary>
    public void AddSolid(Solid solid, string? material)
    {
        var key = material ?? "";
        if (!_free.TryGetValue(key, out var list)) _free[key] = list = new List<GeometryObject>();
        list.Add(solid);
    }

    /// <summary>Finaliza (cria as formas livres acumuladas). Dentro da transação.</summary>
    public void Finish()
    {
        if (Output != GenOutput.FreeForm) return;
        var idx = 0;
        foreach (var (mat, geo) in _free)
        {
            if (geo.Count == 0) continue;
            var name = string.IsNullOrEmpty(mat) ? Name : $"{Name} — {mat}";
            foreach (var chunk in geo.Chunk(4000))
            {
                var ds = Shapes.Create(_ctx.Doc, chunk.ToList(), _freeCategory, name);
                GenSystem.Mark(ds, _systemId, _ctx.Tool.Id, idx++, _status.Length > 0 ? _status : null);
                Created.Add(ds.Id);
            }
        }
        _free.Clear();
    }
}
