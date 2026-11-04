using Autodesk.Revit.DB;
using TeraTools.Core.Colors;
using TeraTools.Core.Geometry;
using TeraTools.Core.Joinery;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Joinery;

/// <summary>Opções editáveis de um módulo de marcenaria (espelho de <see cref="CabinetSpec"/> com rótulos).</summary>
public sealed class ModuleOptions
{
    [Opt("Tipo de módulo", "Module type", Group = "Geral|General")]
    public CabinetKind Kind { get; set; } = CabinetKind.Base;

    [Opt("Código", "Code", Group = "Geral|General", Required = true)]
    public string Code { get; set; } = "M1";

    [Opt("Nome", "Name", Group = "Geral|General")]
    public string Name { get; set; } = "";

    [Opt("Largura", "Width", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Width { get; set; } = U.Mm(800);

    [Opt("Altura", "Height", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Height { get; set; } = U.Mm(870);

    [Opt("Profundidade", "Depth", Kind = OptKind.Length, Min = 0.1, Group = "Geral|General")]
    public double Depth { get; set; } = U.Mm(550);

    [Opt("Altura de instalação (do piso)", "Mounting height (from floor)", Kind = OptKind.Length, Min = 0, Group = "Geral|General")]
    public double Elevation { get; set; }

    [Opt("Espessura da caixa", "Carcass thickness", Kind = OptKind.Length, Min = 0.01, Group = "Caixa|Carcass")]
    public double Thickness { get; set; } = U.Mm(15);

    [Opt("Fundo", "Back", Group = "Caixa|Carcass")]
    public BackKind Back { get; set; } = BackKind.Inset;

    [Opt("Espessura do fundo", "Back thickness", Kind = OptKind.Length, Min = 0.001, Group = "Caixa|Carcass", VisibleIf = "Back!=None")]
    public double BackThickness { get; set; } = U.Mm(6);

    [Opt("Recuo do fundo", "Back recess", Kind = OptKind.Length, Min = 0, Group = "Caixa|Carcass", VisibleIf = "Back=Inset")]
    public double BackRecess { get; set; } = U.Mm(15);

    [Opt("Tampo da caixa", "Carcass top", Group = "Caixa|Carcass")]
    public TopKind Top { get; set; } = TopKind.Rails;

    [Opt("Largura das travessas", "Rail width", Kind = OptKind.Length, Min = 0.01, Group = "Caixa|Carcass", VisibleIf = "Top=Rails")]
    public double RailWidth { get; set; } = U.Mm(100);

    [Opt("Rodapé (base)", "Plinth (base)", Group = "Caixa|Carcass")]
    public bool HasPlinth { get; set; } = true;

    [Opt("Altura do rodapé", "Plinth height", Kind = OptKind.Length, Min = 0, Group = "Caixa|Carcass", VisibleIf = "HasPlinth")]
    public double PlinthHeight { get; set; } = U.Mm(100);

    [Opt("Recuo do rodapé", "Plinth setback", Kind = OptKind.Length, Min = 0, Group = "Caixa|Carcass", VisibleIf = "HasPlinth")]
    public double PlinthSetback { get; set; } = U.Mm(50);

    [Opt("Prateleiras", "Shelves", Min = 0, Max = 30, Group = "Interior|Interior")]
    public int Shelves { get; set; } = 1;

    [Opt("Prateleiras reguláveis (suportes)", "Adjustable shelves (supports)", Group = "Interior|Interior", VisibleIf = "Shelves!=0")]
    public bool AdjustableShelves { get; set; } = true;

    [Opt("Divisórias verticais", "Vertical dividers", Min = 0, Max = 20, Group = "Interior|Interior")]
    public int Dividers { get; set; }

    [Opt("Cabideiro", "Hanging rail", Group = "Interior|Interior")]
    public bool HangingRail { get; set; }

    [Opt("Portas", "Doors", Group = "Frentes|Fronts")]
    public DoorKind Doors { get; set; } = DoorKind.Hinged;

    [Opt("Quantidade de portas (0 = automático)", "Number of doors (0 = automatic)", Min = 0, Max = 12, Group = "Frentes|Fronts", VisibleIf = "Doors!=None")]
    public int DoorCount { get; set; }

    [Opt("Espessura das frentes", "Front thickness", Kind = OptKind.Length, Min = 0.001, Group = "Frentes|Fronts")]
    public double FrontThickness { get; set; } = U.Mm(18);

    [Opt("Folga entre frentes", "Gap between fronts", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts")]
    public double FrontGap { get; set; } = U.Mm(3);

    [Opt("Gavetas", "Drawers", Min = 0, Max = 12, Group = "Frentes|Fronts")]
    public int Drawers { get; set; }

    [Opt("Posição das gavetas", "Drawer position", Group = "Frentes|Fronts", VisibleIf = "Drawers!=0")]
    public DrawerZone DrawerZone { get; set; } = DrawerZone.Top;

    [Opt("Altura da frente de gaveta", "Drawer front height", Kind = OptKind.Length, Min = 0.03, Group = "Frentes|Fronts", VisibleIf = "Drawers!=0")]
    public double DrawerFrontHeight { get; set; } = U.Mm(180);

    [Opt("Puxador", "Handle", Group = "Frentes|Fronts")]
    public HandleKind Handle { get; set; } = HandleKind.Bar;

    [Opt("Comprimento do puxador", "Handle length", Kind = OptKind.Length, Min = 0, Group = "Frentes|Fronts", VisibleIf = "Handle=Bar,Profile")]
    public double HandleLength { get; set; } = U.Mm(160);

    [Opt("Amortecedor / fechamento suave", "Soft close", Group = "Frentes|Fronts")]
    public bool SoftClose { get; set; } = true;

    [Opt("Tampo (bancada)", "Countertop", Group = "Tampo|Countertop")]
    public bool HasCountertop { get; set; }

    [Opt("Espessura do tampo", "Countertop thickness", Kind = OptKind.Length, Min = 0.005, Group = "Tampo|Countertop", VisibleIf = "HasCountertop")]
    public double CountertopThickness { get; set; } = U.Mm(20);

    [Opt("Balanço frontal", "Front overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangFront { get; set; } = U.Mm(20);

    [Opt("Balanço posterior", "Back overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangBack { get; set; }

    [Opt("Balanço lateral", "Side overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo|Countertop", VisibleIf = "HasCountertop")]
    public double OverhangSides { get; set; }

    [Opt("Material da caixa", "Carcass material", Group = "Materiais|Materials", Tip = "Criado automaticamente se não existir.|Created automatically if missing.")]
    public string CarcassMaterial { get; set; } = "MDF Branco TX 15";

    [Opt("Caixa com veio", "Carcass has grain", Group = "Materiais|Materials")]
    public bool CarcassHasGrain { get; set; }

    [Opt("Material das frentes", "Front material", Group = "Materiais|Materials")]
    public string FrontMaterial { get; set; } = "MDF Amadeirado 18";

    [Opt("Frentes com veio", "Fronts have grain", Group = "Materiais|Materials")]
    public bool FrontHasGrain { get; set; } = true;

    [Opt("Material do fundo", "Back material", Group = "Materiais|Materials")]
    public string BackMaterial { get; set; } = "MDF Branco 6";

    [Opt("Material das gavetas", "Drawer material", Group = "Materiais|Materials")]
    public string DrawerMaterial { get; set; } = "MDF Branco TX 15";

    [Opt("Material do tampo", "Countertop material", Group = "Materiais|Materials")]
    public string CountertopMaterial { get; set; } = "Quartzo Branco 20";

    [Opt("Forma livre leve (em vez de peças paramétricas)", "Light free form (instead of parametric pieces)", Group = "Saída|Output")]
    public bool FreeForm { get; set; }

    [Opt("Agrupar como conjunto (assembly)", "Group as assembly", Group = "Saída|Output", VisibleIf = "!FreeForm",
        Tip = "Permite vistas de detalhamento e lista de peças do módulo.|Enables detail views and part list for the module.")]
    public bool Assembly { get; set; } = true;

    public CabinetSpec ToSpec() => new()
    {
        Kind = Kind, Code = Code.Trim(), Name = Name, Width = Width, Height = Height, Depth = Depth, Elevation = Elevation,
        Thickness = Thickness, Back = Back, BackThickness = BackThickness, BackRecess = BackRecess, Top = Top, RailWidth = RailWidth,
        HasPlinth = HasPlinth, PlinthHeight = PlinthHeight, PlinthSetback = PlinthSetback,
        Shelves = Shelves, AdjustableShelves = AdjustableShelves, Dividers = Dividers, HangingRail = HangingRail,
        Doors = Doors, DoorCount = DoorCount, FrontThickness = FrontThickness, FrontGap = FrontGap,
        Drawers = Drawers, DrawerZone = DrawerZone, DrawerFrontHeight = DrawerFrontHeight,
        Handle = Handle, HandleLength = HandleLength, SoftClose = SoftClose,
        HasCountertop = HasCountertop, CountertopThickness = CountertopThickness, CountertopOverhangFront = OverhangFront,
        CountertopOverhangBack = OverhangBack, CountertopOverhangSides = OverhangSides,
        CarcassMaterial = CarcassMaterial, CarcassHasGrain = CarcassHasGrain, FrontMaterial = FrontMaterial, FrontHasGrain = FrontHasGrain,
        BackMaterial = BackMaterial, DrawerMaterial = DrawerMaterial, CountertopMaterial = CountertopMaterial,
    };

    public static ModuleOptions FromSpec(CabinetSpec s, bool freeForm = false, bool assembly = true) => new()
    {
        Kind = s.Kind, Code = s.Code, Name = s.Name, Width = s.Width, Height = s.Height, Depth = s.Depth, Elevation = s.Elevation,
        Thickness = s.Thickness, Back = s.Back, BackThickness = s.BackThickness, BackRecess = s.BackRecess, Top = s.Top, RailWidth = s.RailWidth,
        HasPlinth = s.HasPlinth, PlinthHeight = s.PlinthHeight, PlinthSetback = s.PlinthSetback,
        Shelves = s.Shelves, AdjustableShelves = s.AdjustableShelves, Dividers = s.Dividers, HangingRail = s.HangingRail,
        Doors = s.Doors, DoorCount = s.DoorCount, FrontThickness = s.FrontThickness, FrontGap = s.FrontGap,
        Drawers = s.Drawers, DrawerZone = s.DrawerZone, DrawerFrontHeight = s.DrawerFrontHeight,
        Handle = s.Handle, HandleLength = s.HandleLength, SoftClose = s.SoftClose,
        HasCountertop = s.HasCountertop, CountertopThickness = s.CountertopThickness, OverhangFront = s.CountertopOverhangFront,
        OverhangBack = s.CountertopOverhangBack, OverhangSides = s.CountertopOverhangSides,
        CarcassMaterial = s.CarcassMaterial, CarcassHasGrain = s.CarcassHasGrain, FrontMaterial = s.FrontMaterial, FrontHasGrain = s.FrontHasGrain,
        BackMaterial = s.BackMaterial, DrawerMaterial = s.DrawerMaterial, CountertopMaterial = s.CountertopMaterial,
        FreeForm = freeForm, Assembly = assembly,
    };
}

/// <summary>Registro salvo no projeto para cada módulo (permite editar e gerar a lista de corte).</summary>
public sealed class ModuleRecord
{
    public CabinetSpec Spec { get; set; } = new();
    public bool FreeForm { get; set; }
    public bool Assembly { get; set; } = true;
}

/// <summary>Sistema de coordenadas do módulo: origem no canto frontal esquerdo inferior (no piso), X ao longo, Y para o fundo.</summary>
public sealed class ModuleContext
{
    public double[] Origin { get; set; } = new double[3];
    public double[] XAxis { get; set; } = { 1, 0, 0 };
    public double[] YAxis { get; set; } = { 0, 1, 0 };

    public Frame3 Frame() => new(new Vec3(Origin[0], Origin[1], Origin[2]), new Vec3(XAxis[0], XAxis[1], XAxis[2]),
        new Vec3(YAxis[0], YAxis[1], YAxis[2]), Vec3.UnitZ);

    public static ModuleContext From(XYZ origin, XYZ x) =>
        new ModuleContext { Origin = new[] { origin.X, origin.Y, origin.Z }, XAxis = new[] { x.X, x.Y, 0.0 } }.Fix();

    /// <summary>Garante eixo Y = Z × X invertido (para o fundo) com X horizontal unitário.</summary>
    private ModuleContext Fix()
    {
        var x = new Vec3(XAxis[0], XAxis[1], 0).Normalized();
        // Y aponta para o fundo: X × Y = Z  ⇒  Y = Z × X
        var y = Vec3.UnitZ.Cross(x).Normalized();
        XAxis = new[] { x.X, x.Y, 0.0 };
        YAxis = new[] { y.X, y.Y, 0.0 };
        return this;
    }

    public ModuleContext Offset(double alongX)
    {
        var f = Frame();
        var o = f.Origin + f.XAxis * alongX;
        return new ModuleContext { Origin = new[] { o.X, o.Y, o.Z }, XAxis = XAxis, YAxis = YAxis };
    }
}

internal static class JoineryGen
{
    public const string ToolModule = "join.module";
    public const string ToolExtras = "join.extras";
    public const string RulesKey = "joinery.rules";

    public static JoineryRules Rules(Document doc)
    {
        try
        {
            return ProjectStorage.GetSection<JoineryRules>(doc, RulesKey);
        }
        catch
        {
            return new JoineryRules();
        }
    }

    public static readonly string[] CutParams =
    {
        SharedParams.Comprimento, SharedParams.Espessura, SharedParams.Peca, SharedParams.Modulo, SharedParams.FitaC1, SharedParams.FitaC2,
        SharedParams.FitaL1, SharedParams.FitaL2, SharedParams.Veio, SharedParams.Especificacao, SharedParams.Material, SharedParams.Largura,
        SharedParams.Profundidade, SharedParams.Altura,
    };

    /// <summary>Cor de sombreamento sugerida pelo nome do material.</summary>
    public static RgbColor ColorFor(string material)
    {
        var n = TextUtil.NormalizeForCompare(material);
        if (n.Contains("preto") || n.Contains("black") || n.Contains("grafite")) return new RgbColor(52, 52, 56);
        if (n.Contains("cinza") || n.Contains("gray") || n.Contains("grey") || n.Contains("concreto")) return new RgbColor(150, 152, 156);
        if (n.Contains("quartzo") || n.Contains("marmore") || n.Contains("granito") || n.Contains("pedra")) return new RgbColor(232, 230, 226);
        if (n.Contains("amadeirado") || n.Contains("carvalho") || n.Contains("freijo") || n.Contains("nogueira") || n.Contains("madeira") || n.Contains("louro") || n.Contains("wood"))
            return new RgbColor(176, 128, 82);
        if (n.Contains("verde")) return new RgbColor(96, 128, 96);
        if (n.Contains("azul")) return new RgbColor(84, 110, 150);
        if (n.Contains("branco") || n.Contains("white")) return new RgbColor(240, 240, 236);
        return new RgbColor(222, 210, 190);
    }

    public static OrientedBox ToWorld(Frame3 f, OrientedBox local, double elevation)
    {
        var o = local.Frame.Origin;
        var origin = f.ToWorld(o.X, o.Y, o.Z + elevation);
        return new OrientedBox(new Frame3(origin, f.XAxis, f.YAxis, f.ZAxis), local.SizeX, local.SizeY, local.SizeZ);
    }

    /// <summary>Cria as peças do módulo (e o conjunto). Usa um grupo de transações (um único Desfazer).</summary>
    public static List<ElementId> Generate(ToolContext ctx, ModuleRecord rec, ModuleContext mc, string systemId)
    {
        var doc = ctx.Doc;
        var model = CabinetBuilder.Build(rec.Spec, Rules(doc));
        foreach (var w in model.Warnings) ctx.Report.Warn(rec.Spec.Code + ": " + w);
        var frame = mc.Frame();
        var sink = new BoxSink(ctx, rec.FreeForm ? GenOutput.FreeForm : GenOutput.Parametric, BoxKind.Board, systemId, null, BuiltInCategory.OST_Casework)
        {
            Name = "TT " + rec.Spec.Code,
        };
        sink.Prepare();
        var created = new List<ElementId>();
        ctx.Modify(rec.Spec.Code, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            try
            {
                SharedParams.BindToProject(doc, CutParams, new[] { BuiltInCategory.OST_Casework });
            }
            catch (Exception ex)
            {
                Log.Warn("Parâmetros de corte: " + ex.Message);
            }
            foreach (var m in model.Boards.Select(b => b.Material).Where(m => !string.IsNullOrWhiteSpace(m)).Distinct())
                MaterialsService.Ensure(doc, m, ColorFor(m), updateColor: false);
            for (var i = 0; i < model.Boards.Count; i++)
            {
                var b = model.Boards[i];
                sink.Add(ToWorld(frame, b.Placement, rec.Spec.Elevation), b.Material, i, e => WriteBoard(e, b, rec.Spec.Code));
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, ToolModule, rec, mc, $"{rec.Spec.Code} {JoineryNames.Kind(rec.Spec.Kind)}");
            created.AddRange(sink.Created);
        });
        if (rec.Assembly && !rec.FreeForm && created.Count > 0)
        {
            AssemblyInstance? ai = null;
            try
            {
                ctx.Modify(Loc.T("Criar conjunto", "Create assembly"), () =>
                {
                    ai = AssemblyInstance.Create(doc, created, new ElementId(BuiltInCategory.OST_Casework));
                    GenSystem.Mark(ai, systemId, ToolModule, -1);
                });
                ctx.Modify(Loc.T("Nomear conjunto", "Name assembly"), () =>
                {
                    var baseName = $"{rec.Spec.Code} - {(string.IsNullOrWhiteSpace(rec.Spec.Name) ? JoineryNames.Kind(rec.Spec.Kind) : rec.Spec.Name)}";
                    for (var k = 0; k < 50; k++)
                    {
                        try
                        {
                            ai!.AssemblyTypeName = k == 0 ? baseName : $"{baseName} ({k + 1})";
                            break;
                        }
                        catch
                        {
                            // nome já usado: tenta o próximo
                        }
                    }
                });
                created.Add(ai!.Id);
            }
            catch (Exception ex)
            {
                ctx.Report.Info(Loc.F("{0}: conjunto não criado ({1}); as peças foram mantidas.", "{0}: assembly not created ({1}); pieces were kept.", rec.Spec.Code, ex.Message));
            }
        }
        foreach (var h in model.Hardware)
            ctx.Report.Info(Loc.F("{0}: {1} × {2} {3}", "{0}: {1} × {2} {3}", rec.Spec.Code, h.Quantity, h.Name, h.Spec));
        return created;
    }

    public static void WriteBoard(Element e, Board b, string module)
    {
        RevitGeo.Set(e, b.Id, SharedParams.Peca);
        RevitGeo.Set(e, b.Id, SharedParams.Codigo);
        RevitGeo.Set(e, module, SharedParams.Modulo);
        RevitGeo.Set(e, JoineryNames.Role(b.Role), SharedParams.Grupo);
        RevitGeo.Set(e, b.Length, SharedParams.Comprimento);
        RevitGeo.Set(e, b.Thickness, SharedParams.Espessura);
        RevitGeo.Set(e, b.EdgeL1 ? 1 : 0, SharedParams.FitaL1);
        RevitGeo.Set(e, b.EdgeL2 ? 1 : 0, SharedParams.FitaL2);
        RevitGeo.Set(e, b.EdgeC1 ? 1 : 0, SharedParams.FitaC1);
        RevitGeo.Set(e, b.EdgeC2 ? 1 : 0, SharedParams.FitaC2);
        RevitGeo.Set(e, b.HasGrain ? Loc.T("Comprimento", "Length") : "", SharedParams.Veio);
        RevitGeo.Set(e, Spec(b), SharedParams.Especificacao);
    }

    public static string Spec(Board b) =>
        $"{U.ToMm(b.Length):0} × {U.ToMm(b.Width):0} × {U.ToMm(b.Thickness):0} mm" + (b.EdgeCode.Trim().Length > 0 ? " · " + b.EdgeCode.Trim() : "");

    // ------------------------------------------------------------ Leitura das peças do modelo

    public sealed class BoardRead
    {
        public Element Element { get; init; } = null!;
        public string Module { get; init; } = "";
        public Board Board { get; init; } = new();
        public string? SystemId { get; init; }
    }

    /// <summary>Lê as peças TT_Chapa (e outras famílias de marcenaria com parâmetros TT_) com dimensões reais do modelo.</summary>
    public static List<BoardRead> ReadBoards(Document doc, IEnumerable<Element> elements, out List<(string Module, HardwareItem Item)> hardware, Core.Reporting.OperationReport report)
    {
        var list = new List<BoardRead>();
        hardware = new List<(string, HardwareItem)>();
        var models = new Dictionary<string, (CabinetModel Model, string Code)?>();
        var countBySystem = new Dictionary<string, int>();
        var rules = Rules(doc);
        foreach (var e in elements)
        {
            if (e is not FamilyInstance fi) continue;
            var g = ProjectStorage.ReadGenerated(fi);
            Board? template = null;
            var module = RevitGeo.Param(fi, SharedParams.Modulo)?.AsString() ?? "";
            if (g is { } gg && gg.Index >= 0)
            {
                if (!models.TryGetValue(gg.SystemId, out var entry))
                {
                    entry = null;
                    var sys = ProjectStorage.GetSystem(doc, gg.SystemId);
                    if (sys?.ToolId == ToolModule)
                    {
                        var rec = Json.Deserialize<ModuleRecord>(sys.OptionsJson);
                        if (rec != null) entry = (CabinetBuilder.Build(rec.Spec, rules), rec.Spec.Code);
                    }
                    models[gg.SystemId] = entry;
                }
                if (entry is { } en && gg.Index < en.Model.Boards.Count)
                {
                    template = en.Model.Boards[gg.Index];
                    if (module.Length == 0) module = en.Code;
                    countBySystem[gg.SystemId] = countBySystem.GetValueOrDefault(gg.SystemId) + 1;
                }
            }
            var dims = new[]
            {
                RevitGeo.Param(fi, SharedParams.Largura)?.AsDouble() ?? 0,
                RevitGeo.Param(fi, SharedParams.Profundidade)?.AsDouble() ?? 0,
                RevitGeo.Param(fi, SharedParams.Altura)?.AsDouble() ?? 0,
            };
            if (dims.Any(d => d <= 0)) continue;
            var matId = RevitGeo.Param(fi, SharedParams.Material)?.AsElementId();
            var mat = matId != null && doc.GetElement(matId) is Material m ? m.Name : template?.Material ?? "";
            Board b;
            if (template != null)
            {
                var lp = template.Placement;
                var sizes = new[] { lp.SizeX, lp.SizeY, lp.SizeZ };
                var tAxis = Array.FindIndex(sizes, s => Math.Abs(s - template.Thickness) < 1e-6);
                if (tAxis < 0) tAxis = Array.IndexOf(sizes, sizes.Min());
                var lAxis = Enumerable.Range(0, 3).Where(i => i != tAxis).FirstOrDefault(i => Math.Abs(sizes[i] - template.Length) < 1e-6, -1);
                if (lAxis < 0) lAxis = Enumerable.Range(0, 3).Where(i => i != tAxis).OrderByDescending(i => sizes[i]).First();
                var wAxis = 3 - tAxis - lAxis;
                b = new Board
                {
                    Id = template.Id, Name = template.Name, Role = template.Role, Material = mat, HasGrain = template.HasGrain,
                    Length = dims[lAxis], Width = dims[wAxis], Thickness = dims[tAxis],
                    EdgeL1 = template.EdgeL1, EdgeL2 = template.EdgeL2, EdgeC1 = template.EdgeC1, EdgeC2 = template.EdgeC2,
                };
            }
            else
            {
                var sorted = dims.OrderBy(d => d).ToArray();
                var len = RevitGeo.Param(fi, SharedParams.Comprimento)?.AsDouble() ?? 0;
                var thk = sorted[0];
                double l = sorted[2], w = sorted[1];
                if (len > 0 && Math.Abs(len - sorted[1]) < 1e-6) { l = sorted[1]; w = sorted[2]; }
                b = new Board
                {
                    Id = RevitGeo.Param(fi, SharedParams.Peca)?.AsString() ?? fi.Id.Value.ToString(),
                    Name = RevitGeo.Param(fi, SharedParams.Grupo)?.AsString() ?? fi.Symbol.FamilyName,
                    Role = PanelRole.Panel, Material = mat,
                    HasGrain = !string.IsNullOrWhiteSpace(RevitGeo.Param(fi, SharedParams.Veio)?.AsString()),
                    Length = l, Width = w, Thickness = thk,
                    EdgeL1 = RevitGeo.Param(fi, SharedParams.FitaL1)?.AsInteger() == 1, EdgeL2 = RevitGeo.Param(fi, SharedParams.FitaL2)?.AsInteger() == 1,
                    EdgeC1 = RevitGeo.Param(fi, SharedParams.FitaC1)?.AsInteger() == 1, EdgeC2 = RevitGeo.Param(fi, SharedParams.FitaC2)?.AsInteger() == 1,
                };
            }
            list.Add(new BoardRead { Element = fi, Module = module.Length > 0 ? module : "—", Board = b, SystemId = g?.SystemId });
        }
        // Ferragens: do modelo de cada sistema, multiplicadas pelo número de cópias encontradas.
        foreach (var (sid, entry) in models)
        {
            if (entry is not { } en) continue;
            var copies = Math.Max(1, (int)Math.Round((double)countBySystem.GetValueOrDefault(sid) / Math.Max(1, en.Model.Boards.Count)));
            foreach (var h in en.Model.Hardware)
                hardware.Add((en.Code, new HardwareItem { Kind = h.Kind, Name = h.Name, Spec = h.Spec, Quantity = h.Quantity * copies, Length = h.Length * copies, Unit = h.Unit }));
        }
        if (list.Count == 0) report.Info(Loc.T("Nenhuma peça de marcenaria (TT_Chapa) encontrada.", "No joinery piece (TT_Chapa) found."));
        return list;
    }

    /// <summary>Peças de marcenaria: seleção (módulos/conjuntos/peças) ou o projeto inteiro.</summary>
    public static List<Element> CollectBoards(ToolContext ctx, bool all)
    {
        var doc = ctx.Doc;
        IEnumerable<Element> source;
        if (!all)
        {
            var sel = ctx.Selected();
            if (sel.Count == 0) all = true;
            var ids = new HashSet<long>();
            var list = new List<Element>();
            foreach (var e in sel)
            {
                if (e is AssemblyInstance ai)
                    foreach (var id in ai.GetMemberIds()) { if (ids.Add(id.Value)) list.Add(doc.GetElement(id)); }
                else if (ProjectStorage.ReadGenerated(e) is { } g)
                    foreach (var m in ProjectStorage.FindSystem(doc, g.SystemId)) { if (ids.Add(m.Id.Value)) list.Add(m); }
                else if (ids.Add(e.Id.Value)) list.Add(e);
            }
            source = list;
            if (!all) return source.Where(IsBoard).ToList();
        }
        return new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Casework).WhereElementIsNotElementType().Where(IsBoard).ToList();
    }

    public static bool IsBoard(Element e) =>
        e is FamilyInstance fi && (fi.Symbol.FamilyName == FamilyFactory.BoardFamily || RevitGeo.Param(fi, SharedParams.Espessura) != null && RevitGeo.Param(fi, SharedParams.Largura) != null);

    /// <summary>Pré-visualização: elevação frontal com frentes (esquerda) e interior sem frentes (direita).</summary>
    public static PreviewModel Preview(CabinetSpec spec, JoineryRules rules)
    {
        var pm = new PreviewModel();
        CabinetModel model;
        try
        {
            model = CabinetBuilder.Build(spec, rules);
        }
        catch (Exception ex)
        {
            pm.Info = ex.Message;
            return pm;
        }
        var fronts = new HashSet<PanelRole> { PanelRole.Door, PanelRole.DrawerFront };
        var dx = spec.Width + U.Mm(250);
        foreach (var pass in new[] { 0, 1 })
        {
            var ox = pass * dx;
            var boards = model.Boards.Where(b => pass == 0 || !fronts.Contains(b.Role))
                .OrderBy(b => fronts.Contains(b.Role) ? 2 : b.Role == PanelRole.Back ? 0 : 1);
            foreach (var b in boards)
            {
                var p = b.Placement;
                var o = p.Frame.Origin;
                var poly = Polygon2.Rectangle(new Vec2(ox + o.X, o.Z + spec.Elevation), p.SizeX, p.SizeZ);
                var fill = fronts.Contains(b.Role) ? "#D9B48F" : b.Role == PanelRole.Back ? "#F4EFE8" : b.Role == PanelRole.Countertop ? "#C9C7C2" : "#EFE6DC";
                pm.Polygon(poly, fill, "#6B4A33", 0.7);
            }
        }
        pm.Line(new Vec2(-U.Mm(100), 0), new Vec2(dx + spec.Width + U.Mm(100), 0), "#5F6B7A", 1.5);
        // 3D: o módulo completo (X largura, Y profundidade a partir da frente, Z altura)
        var sc = pm.S3;
        sc.Face(Frame3.World, Polygon2.Rectangle(new Vec2(-U.Mm(300), -U.Mm(600)), spec.Width + U.Mm(600), spec.Depth + U.Mm(900)), "#D9D4CC", 0.25);
        foreach (var b in model.Boards)
        {
            var p = b.Placement;
            var o0 = p.Frame.Origin;
            var color = fronts.Contains(b.Role) ? "#C98F5A" : b.Role == PanelRole.Back ? "#EFE9E0" : b.Role == PanelRole.Countertop ? "#BFBDB8" : b.Role == PanelRole.Plinth ? "#7A7A7A" : "#E8DFD2";
            sc.Box(new OrientedBox(new Frame3(new Vec3(o0.X, o0.Y, o0.Z + spec.Elevation), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), p.SizeX, p.SizeY, p.SizeZ), color);
        }
        var area = model.Boards.Sum(b => b.Area * b.Quantity);
        pm.Info = Loc.F("{0} peças · {1} de chapa · {2} de fita · {3} ferragens{4}", "{0} pieces · {1} of board · {2} of edge band · {3} hardware{4}",
            model.Boards.Count, U.FormatArea(area), U.FormatM(model.EdgeBandLength()), model.Hardware.Sum(h => h.Quantity),
            model.Warnings.Count > 0 ? " · ⚠ " + model.Warnings[0] : "");
        return pm;
    }
}
