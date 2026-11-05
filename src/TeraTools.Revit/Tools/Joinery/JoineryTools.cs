using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Joinery;
using TeraTools.Core.Localization;
using TeraTools.Core.Nesting;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Joinery;

internal static class JoineryUi
{
    public static CabinetKind? PickKind(ToolContext ctx)
    {
        var kinds = Enum.GetValues<CabinetKind>().Select(k => (object)k).ToList();
        var k = Pick.One(kinds, o => EnumLabels.Label(typeof(CabinetKind), o), ctx.Tool.Name, Loc.T("Qual tipo de módulo?", "Which module type?"), ctx.Owner);
        return k == null ? null : (CabinetKind)k;
    }

    /// <summary>Opções iniciais: últimas usadas para o tipo ou os padrões do tipo.</summary>
    public static ModuleOptions Initial(ToolContext ctx, CabinetKind kind, string key)
    {
        var o = Services.Presets.GetLastUsed(key) != null ? ctx.LoadOptions<ModuleOptions>(key) : ModuleOptions.FromSpec(CabinetSpec.Defaults(kind));
        o.Kind = kind;
        return o;
    }

    /// <summary>Próximo código livre (M1, M2...).</summary>
    public static string NextCode(Document doc, string prefix = "M")
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var s in ProjectStorage.GetSection<ProjectStorage.GeneratedSystems>(doc, ProjectStorage.SectionSystems).Systems.Values)
            {
                if (s.ToolId != JoineryGen.ToolModule) continue;
                var rec = Json.Deserialize<ModuleRecord>(s.OptionsJson);
                if (rec != null) used.Add(rec.Spec.Code);
            }
        }
        catch
        {
            // sem registro
        }
        for (var i = 1; i < 10000; i++)
            if (!used.Contains(prefix + i)) return prefix + i;
        return prefix + Guid.NewGuid().ToString("N")[..4];
    }

    /// <summary>Posição do módulo: encostado numa face de parede ou por dois pontos.</summary>
    public static ModuleContext PickPlacement(ToolContext ctx, double depth, out Level level)
    {
        var doc = ctx.Doc;
        var i = Msg.Choose(Loc.T("Como posicionar o módulo?", "How to place the module?"), ctx.Tool.Name,
            Loc.T("Encostado numa parede (clique na face, onde fica o canto esquerdo)", "Against a wall (click the face where the left corner goes)"),
            Loc.T("Por dois pontos (canto frontal esquerdo e um ponto à direita, ao longo da frente)", "By two points (front-left corner and a point to the right, along the front)"));
        if (i < 0) ctx.Cancel();
        if (i == 0)
        {
            var (p, front, lvl) = PickWallPoint(ctx, "Clique na face da parede (canto esquerdo do móvel)", "Click the wall face (cabinet's left corner)");
            level = lvl;
            var y = front.Negate();
            var x = y.CrossProduct(XYZ.BasisZ).Normalize();
            var o = p - y * depth;
            return ModuleContext.From(new XYZ(o.X, o.Y, level.Elevation), x);
        }
        ctx.EnsureWorkPlane();
        var a = ctx.PickPoint("Clique no canto frontal esquerdo", "Click the front-left corner");
        var b = ctx.PickPoint("Clique um ponto à direita, ao longo da frente", "Click a point to the right, along the front");
        var dir = new XYZ(b.X - a.X, b.Y - a.Y, 0);
        if (dir.GetLength() < 1e-6) dir = XYZ.BasisX;
        level = RevitGeo.ViewLevel(ctx.ActiveView) ?? throw new ToolException(Loc.T("Nenhum nível.", "No level."));
        return ModuleContext.From(new XYZ(a.X, a.Y, level.Elevation), dir.Normalize());
    }

    /// <summary>Ponto clicado numa face de parede, a normal horizontal (para o ambiente) e o nível da parede.</summary>
    public static (XYZ Point, XYZ Front, Level Level) PickWallPoint(ToolContext ctx, string pt, string en)
    {
        var doc = ctx.Doc;
        var r = ctx.PickFace(pt, en, e => e is Wall);
        var wall = (Wall)doc.GetElement(r);
        var face = wall.GetGeometryObjectFromReference(r) as Face ?? throw new ToolException(Loc.T("Face inválida.", "Invalid face."));
        var p = r.GlobalPoint;
        XYZ n;
        if (face is PlanarFace pf) n = pf.FaceNormal;
        else
        {
            var ir = face.Project(p);
            n = ir != null ? face.ComputeNormal(ir.UVPoint) : wall.Orientation;
        }
        n = new XYZ(n.X, n.Y, 0);
        if (n.GetLength() < 1e-6) ToolContext.Fail("Escolha uma face vertical da parede.", "Pick a vertical wall face.");
        var level = doc.GetElement(wall.LevelId) as Level ?? RevitGeo.ViewLevel(ctx.ActiveView) ?? throw new ToolException(Loc.T("Nenhum nível.", "No level."));
        return (p, n.Normalize(), level);
    }

    /// <summary>Exclui um módulo (peças, conjunto e o tipo de conjunto que ficar sem uso).</summary>
    public static void DeleteModule(Document doc, string systemId)
    {
        var els = ProjectStorage.FindSystem(doc, systemId);
        var types = els.OfType<AssemblyInstance>().Select(a => a.GetTypeId()).Where(t => t != ElementId.InvalidElementId).ToList();
        foreach (var a in els.OfType<AssemblyInstance>())
            foreach (var id in a.GetMemberIds())
                if (els.All(e => e.Id != id) && doc.GetElement(id) is { } m) els.Add(m);
        var ids = els.Select(e => e.Id).ToList();
        if (ids.Count > 0) doc.Delete(ids);
        foreach (var t in types)
        {
            var used = new FilteredElementCollector(doc).OfClass(typeof(AssemblyInstance)).Any(a => a.GetTypeId() == t);
            if (!used && doc.GetElement(t) != null)
            {
                try
                {
                    doc.Delete(t);
                }
                catch
                {
                    // tipo em uso por outro elemento
                }
            }
        }
    }

    public static (string SystemId, ModuleRecord Record, ModuleContext Context) PickModule(ToolContext ctx)
    {
        var doc = ctx.Doc;
        Element? e = ctx.Selected().FirstOrDefault(IsModuleElement);
        e ??= ctx.PickElement("Escolha uma peça ou o conjunto do módulo", "Pick a piece or the module assembly", IsModuleElement);
        if (e.AssemblyInstanceId != ElementId.InvalidElementId && ProjectStorage.ReadGenerated(e) == null) e = doc.GetElement(e.AssemblyInstanceId);
        var g = ProjectStorage.ReadGenerated(e!) ?? throw new ToolException(Loc.T("O elemento não pertence a um módulo do Téra Tools.", "The element is not part of a Téra Tools module."));
        var sys = ProjectStorage.GetSystem(doc, g.SystemId);
        if (sys == null || sys.ToolId != JoineryGen.ToolModule) throw new ToolException(Loc.T("Registro do módulo não encontrado no projeto.", "Module record not found in the project."));
        var rec = Json.Deserialize<ModuleRecord>(sys.OptionsJson) ?? throw new ToolException(Loc.T("Registro do módulo inválido.", "Invalid module record."));
        var mc = Json.Deserialize<ModuleContext>(sys.ContextJson) ?? throw new ToolException(Loc.T("Posição do módulo inválida.", "Invalid module position."));
        return (g.SystemId, rec, mc);
    }

    private static bool IsModuleElement(Element e)
    {
        if (ProjectStorage.ReadGenerated(e) is { } g) return ProjectStorage.GetSystem(e.Document, g.SystemId)?.ToolId == JoineryGen.ToolModule;
        if (e.AssemblyInstanceId != ElementId.InvalidElementId && e.Document.GetElement(e.AssemblyInstanceId) is { } a)
            return ProjectStorage.ReadGenerated(a) is { } ga && ProjectStorage.GetSystem(e.Document, ga.SystemId)?.ToolId == JoineryGen.ToolModule;
        return false;
    }

    public static void Regenerate(ToolContext ctx, string optionsJson, string contextJson, string systemId)
    {
        var rec = Json.Deserialize<ModuleRecord>(optionsJson) ?? throw new ToolException("Registro inválido.");
        var mc = Json.Deserialize<ModuleContext>(contextJson) ?? throw new ToolException("Contexto inválido.");
        Edit(ctx, systemId, rec, mc);
    }

    public static void Edit(ToolContext ctx, string systemId, ModuleRecord rec, ModuleContext mc)
    {
        var rules = JoineryGen.Rules(ctx.Doc);
        var o = ModuleOptions.FromSpec(rec.Spec, rec.FreeForm, rec.Assembly);
        o = ctx.Ask(o, preview: x => JoineryGen.Preview(x.ToSpec(), rules), okText: Loc.T("Atualizar módulo", "Update module"), key: "join.module." + rec.Spec.Kind);
        ctx.Group(ctx.Tool.Name, () =>
        {
            ctx.Modify(Loc.T("Remover versão anterior", "Remove previous version"), () => DeleteModule(ctx.Doc, systemId));
            var ids = JoineryGen.Generate(ctx, new ModuleRecord { Spec = o.ToSpec(), FreeForm = o.FreeForm, Assembly = o.Assembly }, mc, systemId);
            ctx.Report.Modified();
            foreach (var id in ids) ctx.Report.AddResult(id.Value);
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("join.module", Modules.Joinery, "Módulo de marcenaria", "Joinery module", Order = 10, Large = true, Group = "Módulos|Modules",
    DescPt = "Cria um módulo paramétrico (armário, aéreo, torre, nicho, roupeiro, gabinete, bancada, ilha, painel, estante) com caixa, fundo, prateleiras, portas, gavetas, tampo e ferragens.",
    DescEn = "Creates a parametric module (base, wall, tall, niche, wardrobe, vanity, counter, island, panel, bookcase) with carcass, back, shelves, doors, drawers, top and hardware.",
    HelpPt = "Cada peça é uma chapa TT_Chapa (categoria Marcenaria) com dados de corte: código, módulo, comprimento, largura, espessura, fitas de borda e veio. As peças formam um conjunto (assembly) para vistas de detalhamento. Para alterar depois, use \"Editar módulo\": o módulo é refeito no mesmo lugar.\n\nRegras de ferragens (dobradiças por altura, corrediças, folgas) ficam em \"Regras de marcenaria\".",
    HelpEn = "Each piece is a TT_Chapa board (Casework category) with cutting data: code, module, length, width, thickness, edge banding and grain. Pieces form an assembly for detail views. To change it later, use \"Edit module\": the module is rebuilt in place.\n\nHardware rules (hinges by height, slides, clearances) are in \"Joinery rules\".",
    Keywords = "marcenaria modulo armario aereo torre nicho roupeiro gabinete bancada ilha estante cabinet joinery millwork casework")]
public sealed class ModuleCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var kind = JoineryUi.PickKind(ctx) ?? throw new OperationCanceledException();
        var key = "join.module." + kind;
        var o = JoineryUi.Initial(ctx, kind, key);
        o.Code = JoineryUi.NextCode(ctx.Doc, new string(o.Code.TakeWhile(char.IsLetter).ToArray()) is { Length: > 0 } p ? p : "M");
        var rules = JoineryGen.Rules(ctx.Doc);
        o = ctx.Ask(o, preview: x => JoineryGen.Preview(x.ToSpec(), rules), okText: Loc.T("Posicionar", "Place"), key: key);
        var mc = JoineryUi.PickPlacement(ctx, o.Depth, out _);
        ctx.Group(ctx.Tool.Name, () =>
        {
            var ids = JoineryGen.Generate(ctx, new ModuleRecord { Spec = o.ToSpec(), FreeForm = o.FreeForm, Assembly = o.Assembly }, mc, GenSystem.NewId());
            ctx.Report.Created(ids.Count);
            foreach (var id in ids) ctx.Report.AddResult(id.Value);
        });
    }
}

public enum FillerSide
{
    [Text("No fim", "At the end")] End,
    [Text("No início", "At the start")] Start,
    [Text("Dividido nas duas pontas", "Split at both ends")] Both,
}

public sealed class RunOptions
{
    [Opt("Prefixo do código", "Code prefix", Group = "Composição|Composition")]
    public string Prefix { get; set; } = "M";

    [Opt("Largura mínima do módulo", "Minimum module width", Kind = OptKind.Length, Min = 0.05, Group = "Composição|Composition")]
    public double MinWidth { get; set; } = U.Mm(300);

    [Opt("Largura máxima do módulo", "Maximum module width", Kind = OptKind.Length, Min = 0.05, Group = "Composição|Composition")]
    public double MaxWidth { get; set; } = U.Mm(900);

    [Opt("Largura preferida", "Preferred width", Kind = OptKind.Length, Min = 0.05, Group = "Composição|Composition")]
    public double PreferredWidth { get; set; } = U.Mm(600);

    [Opt("Arredondar larguras para múltiplos de (0 = exato)", "Round widths to multiples of (0 = exact)", Kind = OptKind.Length, Min = 0, Group = "Composição|Composition")]
    public double RoundStep { get; set; } = U.Mm(50);

    [Opt("Complemento (enchimento) máximo", "Maximum filler", Kind = OptKind.Length, Min = 0, Group = "Composição|Composition")]
    public double FillerMax { get; set; } = U.Mm(100);

    [Opt("Posição do complemento", "Filler position", Group = "Composição|Composition")]
    public FillerSide FillerSide { get; set; } = FillerSide.End;

    [Opt("Recuo no início", "Start clearance", Kind = OptKind.Length, Min = 0, Group = "Composição|Composition")]
    public double StartGap { get; set; }

    [Opt("Recuo no fim", "End clearance", Kind = OptKind.Length, Min = 0, Group = "Composição|Composition")]
    public double EndGap { get; set; }

    [Opt("Tampo contínuo sobre os módulos", "Continuous top over the modules", Group = "Tampo|Countertop")]
    public bool ContinuousTop { get; set; } = true;

    [Opt("Espessura do tampo", "Top thickness", Kind = OptKind.Length, Min = 0.005, Group = "Tampo|Countertop", VisibleIf = "ContinuousTop")]
    public double TopThickness { get; set; } = U.Mm(20);

    [Opt("Balanço frontal do tampo", "Top front overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo|Countertop", VisibleIf = "ContinuousTop")]
    public double TopFront { get; set; } = U.Mm(20);

    [Opt("Balanço lateral do tampo", "Top side overhang", Kind = OptKind.Length, Min = 0, Group = "Tampo|Countertop", VisibleIf = "ContinuousTop")]
    public double TopSides { get; set; }

    [Opt("Material do tampo", "Top material", Group = "Tampo|Countertop", VisibleIf = "ContinuousTop")]
    public string TopMaterial { get; set; } = "Quartzo Branco 20";
}

[Transaction(TransactionMode.Manual)]
[Tool("join.run", Modules.Joinery, "Compor módulos na parede", "Compose modules along wall", Order = 11, Group = "Módulos|Modules",
    DescPt = "Divide um trecho de parede em módulos de larguras equilibradas (com arredondamento e complemento), cria todos e, se quiser, um tampo contínuo.",
    DescEn = "Splits a wall stretch into balanced module widths (with rounding and filler), creates them all and optionally a continuous top.",
    HelpPt = "1) Escolha o tipo e configure o módulo-base. 2) Clique na face da parede no início e no fim do trecho. 3) Ajuste larguras mínima/máxima/preferida e o complemento: a pré-visualização mostra a divisão.",
    HelpEn = "1) Choose the type and set the base module. 2) Click the wall face at the start and end of the stretch. 3) Adjust min/max/preferred widths and filler: the preview shows the split.",
    Keywords = "compor modulos parede cozinha dividir largura enchimento complemento run compose kitchen filler")]
public sealed class RunCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var kind = JoineryUi.PickKind(ctx) ?? throw new OperationCanceledException();
        var (p1, front, level) = JoineryUi.PickWallPoint(ctx, "Clique na face da parede no INÍCIO do trecho", "Click the wall face at the START of the stretch");
        var (p2, _, _) = JoineryUi.PickWallPoint(ctx, "Clique na face da parede no FIM do trecho", "Click the wall face at the END of the stretch");
        var y = front.Negate();
        var x = y.CrossProduct(XYZ.BasisZ).Normalize();
        var s2 = (p2 - p1).DotProduct(x);
        var start = s2 >= 0 ? p1 : p1 + x * s2;
        var length = Math.Abs(s2);
        if (length < U.Mm(100)) ToolContext.Fail("O trecho é curto demais.", "The stretch is too short.");

        var key = "join.module." + kind;
        var tpl = JoineryUi.Initial(ctx, kind, key);
        var rules = JoineryGen.Rules(doc);
        tpl = ctx.Ask(tpl, title: Loc.T("Módulo-base da composição", "Composition base module"), preview: t => JoineryGen.Preview(t.ToSpec(), rules), key: key);
        var o = ctx.Ask<RunOptions>(preview: r => Preview(r, tpl, length), okText: Loc.T("Criar", "Create"));
        var plan = Plan(o, length);
        if (!plan.Result.Success) throw new ToolException(plan.Result.Message ?? Loc.T("Não foi possível dividir o trecho.", "Could not split the stretch."));
        var origin = new XYZ(start.X, start.Y, level.Elevation) - y * tpl.Depth;
        var baseCtx = ModuleContext.From(origin, x);
        var first = 1;
        var used = JoineryUi.NextCode(doc, o.Prefix);
        if (int.TryParse(used[o.Prefix.Length..], out var n0)) first = n0;
        ctx.CheckCount(plan.Result.Widths.Count, "módulos", "modules");
        ctx.Group(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < plan.Result.Widths.Count; i++)
            {
                var spec = tpl.ToSpec();
                spec.Width = plan.Result.Widths[i];
                spec.Code = o.Prefix + (first + i);
                if (o.ContinuousTop) spec.HasCountertop = false;
                var ids = JoineryGen.Generate(ctx, new ModuleRecord { Spec = spec, FreeForm = tpl.FreeForm, Assembly = tpl.Assembly }, baseCtx.Offset(plan.Positions[i]), GenSystem.NewId());
                ctx.Report.Count(Loc.T("Módulos", "Modules"));
                foreach (var id in ids) ctx.Report.AddResult(id.Value);
            }
            Extras(ctx, o, tpl, plan, baseCtx, length, o.Prefix + "C" + first);
        });
        ctx.Report.Info(Loc.F("Trecho de {0}: {1} módulos ({2}){3}.", "Stretch of {0}: {1} modules ({2}){3}.", U.FormatM(length), plan.Result.Widths.Count,
            string.Join(" + ", plan.Result.Widths.Select(w => U.FormatMm(w))), plan.Result.Filler > 1e-6 ? Loc.F(" + complemento {0}", " + filler {0}", U.FormatMm(plan.Result.Filler)) : ""));
    }

    internal sealed record RunPlan(CompositionResult Result, List<double> Positions, List<(double X, double W)> Fillers);

    internal static RunPlan Plan(RunOptions o, double length)
    {
        var avail = length - o.StartGap - o.EndGap;
        var r = Composition.SplitEqual(avail, o.MinWidth, o.MaxWidth, o.PreferredWidth, o.RoundStep, o.FillerMax);
        var pos = new List<double>();
        var fillers = new List<(double, double)>();
        if (!r.Success) return new RunPlan(r, pos, fillers);
        var f = r.Filler;
        var x = o.StartGap;
        if (f > 1e-6 && o.FillerSide == FillerSide.Start) { fillers.Add((x, f)); x += f; }
        if (f > 1e-6 && o.FillerSide == FillerSide.Both) { fillers.Add((x, f / 2)); x += f / 2; }
        foreach (var w in r.Widths)
        {
            pos.Add(x);
            x += w;
        }
        if (f > 1e-6 && o.FillerSide == FillerSide.End) fillers.Add((x, f));
        if (f > 1e-6 && o.FillerSide == FillerSide.Both) fillers.Add((x, f / 2));
        return new RunPlan(r, pos, fillers);
    }

    private static PreviewModel Preview(RunOptions o, ModuleOptions tpl, double length)
    {
        var pm = new PreviewModel();
        var plan = Plan(o, length);
        pm.Line(new Vec2(0, 0), new Vec2(length, 0), "#5F6B7A", 2);
        pm.Line(new Vec2(0, -U.Mm(50)), new Vec2(0, tpl.Height + U.Mm(50)), "#5F6B7A", 1);
        pm.Line(new Vec2(length, -U.Mm(50)), new Vec2(length, tpl.Height + U.Mm(50)), "#5F6B7A", 1);
        if (!plan.Result.Success)
        {
            pm.Info = plan.Result.Message ?? Loc.T("Não foi possível dividir.", "Could not split.");
            return pm;
        }
        for (var i = 0; i < plan.Positions.Count; i++)
            pm.Polygon(Polygon2.Rectangle(new Vec2(plan.Positions[i], tpl.Elevation), plan.Result.Widths[i], tpl.Height), "#D9B48F", "#6B4A33", 1);
        foreach (var (fx, fw) in plan.Fillers)
            pm.Polygon(Polygon2.Rectangle(new Vec2(fx, tpl.Elevation), fw, tpl.Height), "#B9A48F", "#6B4A33", 0.6);
        if (o.ContinuousTop)
            pm.Polygon(Polygon2.Rectangle(new Vec2(o.StartGap - o.TopSides, tpl.Elevation + tpl.Height), length - o.StartGap - o.EndGap + 2 * o.TopSides, o.TopThickness), "#C9C7C2", "#5F6B7A", 0.8);
        // 3D: parede ao fundo, módulos, complementos e tampo
        var sc = pm.S3;
        var wallF = new Frame3(new Vec3(0, tpl.Depth, 0), Vec3.UnitX, Vec3.UnitZ, Vec3.UnitY);
        sc.Face(wallF, Polygon2.Rectangle(new Vec2(-U.Mm(200), 0), length + U.Mm(400), Math.Max(tpl.Elevation + tpl.Height + U.Mm(400), U.M(2.6))), "#D9D4CC", 0.35);
        Frame3 At(double x, double y, double z) => new(new Vec3(x, y, z), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ);
        for (var i = 0; i < plan.Positions.Count; i++)
        {
            var w = plan.Result.Widths[i];
            sc.Box(new OrientedBox(At(plan.Positions[i] + U.Mm(1), 0, tpl.Elevation), w - U.Mm(2), tpl.Depth, tpl.Height), "#E8DFD2");
            sc.Box(new OrientedBox(At(plan.Positions[i] + U.Mm(2), -tpl.FrontThickness, tpl.Elevation + (tpl.HasPlinth ? tpl.PlinthHeight : 0)), w - U.Mm(4), tpl.FrontThickness, tpl.Height - (tpl.HasPlinth ? tpl.PlinthHeight : 0)), "#C98F5A");
        }
        foreach (var (fx, fw) in plan.Fillers)
            sc.Box(new OrientedBox(At(fx, -tpl.FrontThickness, tpl.Elevation), fw, tpl.FrontThickness, tpl.Height), "#B9A48F");
        if (o.ContinuousTop)
            sc.Box(new OrientedBox(At(o.StartGap - o.TopSides, -o.TopFront - tpl.FrontThickness, tpl.Elevation + tpl.Height), length - o.StartGap - o.EndGap + 2 * o.TopSides, tpl.Depth + o.TopFront + tpl.FrontThickness, o.TopThickness), "#BFBDB8");
        pm.Info = Loc.F("{0} módulos: {1}{2}", "{0} modules: {1}{2}", plan.Result.Widths.Count, string.Join(" + ", plan.Result.Widths.Select(w => U.FormatMm(w))),
            plan.Result.Filler > 1e-6 ? Loc.F(" · complemento {0}", " · filler {0}", U.FormatMm(plan.Result.Filler)) : "");
        return pm;
    }

    /// <summary>Complementos (enchimentos) e tampo contínuo, registrados como um sistema de acessórios.</summary>
    private static void Extras(ToolContext ctx, RunOptions o, ModuleOptions tpl, RunPlan plan, ModuleContext baseCtx, double length, string code)
    {
        var boards = new List<Board>();
        var z0 = tpl.HasPlinth ? tpl.PlinthHeight : 0;
        var k = 0;
        foreach (var (fx, fw) in plan.Fillers)
        {
            var h = tpl.Height - z0;
            boards.Add(new Board
            {
                Id = $"{code}-CP{++k:00}", Name = Loc.F("Complemento {0}", "Filler {0}", k), Role = PanelRole.Filler,
                Length = Math.Max(h, fw), Width = Math.Min(h, fw), Thickness = tpl.FrontThickness, Material = tpl.FrontMaterial, HasGrain = tpl.FrontHasGrain && h >= fw,
                EdgeL1 = true, EdgeL2 = true,
                Placement = new OrientedBox(new Frame3(new Vec3(fx, -tpl.FrontThickness, z0), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), fw, tpl.FrontThickness, h),
            });
        }
        if (o.ContinuousTop)
        {
            var tx0 = o.StartGap - o.TopSides;
            var tl = length - o.StartGap - o.EndGap + 2 * o.TopSides;
            var ty0 = -o.TopFront - tpl.FrontThickness;
            var tw = tpl.Depth - ty0;
            boards.Add(new Board
            {
                Id = $"{code}-TP01", Name = Loc.T("Tampo contínuo", "Continuous top"), Role = PanelRole.Countertop,
                Length = tl, Width = tw, Thickness = o.TopThickness, Material = o.TopMaterial, EdgeL1 = true,
                Placement = new OrientedBox(new Frame3(new Vec3(tx0, ty0, tpl.Height), Vec3.UnitX, Vec3.UnitY, Vec3.UnitZ), tl, tw, o.TopThickness),
            });
        }
        if (boards.Count == 0) return;
        var doc = ctx.Doc;
        var systemId = GenSystem.NewId();
        var sink = new BoxSink(ctx, tpl.FreeForm ? GenOutput.FreeForm : GenOutput.Parametric, BoxKind.Board, systemId, null, BuiltInCategory.OST_Casework) { Name = "TT " + code };
        sink.Prepare();
        var frame = baseCtx.Frame();
        ctx.Modify(Loc.T("Complementos e tampo", "Fillers and top"), () =>
        {
            foreach (var m in boards.Select(b => b.Material).Distinct()) MaterialsService.Ensure(doc, m, JoineryGen.ColorFor(m), updateColor: false);
            for (var i = 0; i < boards.Count; i++)
            {
                var b = boards[i];
                sink.Add(JoineryGen.ToWorld(frame, b.Placement, tpl.Elevation), b.Material, i, e => JoineryGen.WriteBoard(e, b, code));
            }
            sink.Finish();
            GenSystem.Register(doc, systemId, JoineryGen.ToolExtras, new { Code = code }, baseCtx, Loc.T("Complementos/tampo", "Fillers/top"));
            ctx.Report.Count(Loc.T("Complementos/tampo", "Fillers/top"), sink.Created.Count);
            foreach (var id in sink.Created) ctx.Report.AddResult(id.Value);
        });
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("join.edit", Modules.Joinery, "Editar módulo", "Edit module", Order = 12, Group = "Módulos|Modules",
    DescPt = "Reabre as opções de um módulo existente (medidas, frentes, gavetas, materiais) e o refaz no mesmo lugar, mantendo o código.",
    DescEn = "Reopens the options of an existing module (sizes, fronts, drawers, materials) and rebuilds it in place, keeping the code.",
    Keywords = "editar modulo marcenaria alterar medidas edit module joinery")]
public sealed class EditModuleCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var (id, rec, mc) = JoineryUi.PickModule(ctx);
        JoineryUi.Edit(ctx, id, rec, mc);
    }
}

public sealed class JoineryViewsOptions
{
    [Opt("Elevação frontal", "Front elevation", Group = "Vistas|Views")]
    public bool Front { get; set; } = true;

    [Opt("Elevação lateral", "Side elevation", Group = "Vistas|Views")]
    public bool Side { get; set; } = true;

    [Opt("Planta (corte horizontal)", "Plan (horizontal section)", Group = "Vistas|Views")]
    public bool Plan { get; set; } = true;

    [Opt("Corte vertical", "Vertical section", Group = "Vistas|Views")]
    public bool Section { get; set; } = true;

    [Opt("3D isométrica", "3D isometric", Group = "Vistas|Views")]
    public bool Iso { get; set; } = true;

    [Opt("3D explodida", "Exploded 3D", Group = "Vistas|Views")]
    public bool Exploded { get; set; } = true;

    [Opt("Fator de explosão", "Explosion factor", Min = 0.1, Max = 5, Group = "Vistas|Views", VisibleIf = "Exploded")]
    public double ExplodeFactor { get; set; } = 0.6;

    [Opt("Lista de peças", "Part list", Group = "Vistas|Views")]
    public bool PartList { get; set; } = true;

    [Opt("Escala (1:n)", "Scale (1:n)", Min = 1, Max = 500, Group = "Vistas|Views")]
    public int Scale { get; set; } = 20;

    [Opt("Criar prancha com as vistas", "Create sheet with the views", Group = "Prancha|Sheet")]
    public bool Sheet { get; set; } = true;

    [Opt("Carimbo", "Title block", Kind = OptKind.TitleBlock, Group = "Prancha|Sheet", VisibleIf = "Sheet")]
    public string TitleBlock { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("join.views", Modules.Joinery, "Detalhamento do módulo", "Module detailing", Order = 20, Large = true, Group = "Detalhamento|Detailing",
    DescPt = "Cria vistas de detalhamento do conjunto (frontal, lateral, planta, corte, 3D e 3D explodida), lista de peças e a prancha montada.",
    DescEn = "Creates assembly detail views (front, side, plan, section, 3D and exploded 3D), part list and the laid-out sheet.",
    Keywords = "detalhamento vistas modulo explodida prancha lista de pecas detail views exploded sheet part list assembly")]
public sealed class JoineryViewsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var sel = ctx.SelectedOrPick("Selecione os módulos (conjuntos)", "Select the modules (assemblies)",
            e => e is AssemblyInstance || e.AssemblyInstanceId != ElementId.InvalidElementId);
        var assemblies = sel.Select(e => e as AssemblyInstance ?? doc.GetElement(e.AssemblyInstanceId) as AssemblyInstance)
            .Where(a => a != null).DistinctBy(a => a!.Id.Value).Cast<AssemblyInstance>().ToList();
        if (assemblies.Count == 0) ToolContext.Fail("Nenhum conjunto selecionado. Crie os módulos com \"Agrupar como conjunto\".", "No assembly selected. Create modules with \"Group as assembly\".");
        var o = ctx.Ask<JoineryViewsOptions>();
        var tb = o.Sheet ? SheetPlacer.TitleBlockId(doc, o.TitleBlock) : ElementId.InvalidElementId;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var ai in assemblies)
            {
                var name = ai.AssemblyTypeName;
                var views = new List<View>();
                View? Make(Func<View> create, string label)
                {
                    try
                    {
                        var v = create();
                        try { v.Name = Lookup.UniqueViewName(doc, $"{name} - {label}"); } catch { /* nome automático */ }
                        if (v is not ViewSchedule) v.Scale = o.Scale;
                        ctx.Report.Count(Loc.T("Vistas", "Views"));
                        ctx.Report.AddResult(v.Id.Value);
                        return v;
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.Warn($"{name} - {label}: {ex.Message}", ai.Id.Value);
                        return null;
                    }
                }
                if (o.Front && Make(() => AssemblyViewUtils.CreateDetailSection(doc, ai.Id, AssemblyDetailViewOrientation.ElevationFront), Loc.T("Frontal", "Front")) is { } vf) views.Add(vf);
                if (o.Side && Make(() => AssemblyViewUtils.CreateDetailSection(doc, ai.Id, AssemblyDetailViewOrientation.ElevationLeft), Loc.T("Lateral", "Side")) is { } vs) views.Add(vs);
                if (o.Plan && Make(() => AssemblyViewUtils.CreateDetailSection(doc, ai.Id, AssemblyDetailViewOrientation.HorizontalDetail), Loc.T("Planta", "Plan")) is { } vp) views.Add(vp);
                if (o.Section && Make(() => AssemblyViewUtils.CreateDetailSection(doc, ai.Id, AssemblyDetailViewOrientation.DetailSectionA), Loc.T("Corte", "Section")) is { } vc) views.Add(vc);
                if (o.Iso && Make(() => AssemblyViewUtils.Create3DOrthographic(doc, ai.Id), "3D") is { } v3) views.Add(v3);
                if (o.Exploded && Make(() => AssemblyViewUtils.Create3DOrthographic(doc, ai.Id), Loc.T("3D explodida", "Exploded 3D")) is View3D ve)
                {
                    Explode(doc, ai, ve, o.ExplodeFactor, ctx);
                    views.Add(ve);
                }
                ViewSchedule? parts = null;
                if (o.PartList) parts = Make(() => AssemblyViewUtils.CreatePartList(doc, ai.Id), Loc.T("Lista de peças", "Part list")) as ViewSchedule;
                if (!o.Sheet) continue;
                try
                {
                    var sheet = AssemblyViewUtils.CreateSheet(doc, ai.Id, tb);
                    try { sheet.Name = name; } catch { /* mantém */ }
                    var area = SheetPlacer.UsableArea(sheet, U.Mm(12), U.Mm(0));
                    var left = new Rect2(area.MinX, area.MinY, area.MaxX - (parts != null ? area.Width * 0.3 : 0), area.MaxY);
                    var rest = SheetPlacer.Place(doc, sheet, views, U.Mm(10), left);
                    foreach (var v in rest) ctx.Report.Info(Loc.F("\"{0}\" não coube na prancha.", "\"{0}\" did not fit on the sheet.", v.Name));
                    if (parts != null) ScheduleSheetInstance.Create(doc, sheet.Id, parts.Id, new XYZ(left.MaxX + U.Mm(8), area.MaxY - U.Mm(5), 0));
                    ctx.Report.Count(Loc.T("Pranchas", "Sheets"));
                    ctx.Report.AddResult(sheet.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(name + ": " + Shapes.FailText(ex), ai.Id.Value);
                }
            }
        });
    }

    private static void Explode(Document doc, AssemblyInstance ai, View3D view, double factor, ToolContext ctx)
    {
        doc.Regenerate();
        var center = RevitGeo.BoxCenter(ai);
        if (center == null) return;
        foreach (var id in ai.GetMemberIds())
        {
            var m = doc.GetElement(id);
            var c = m == null ? null : RevitGeo.BoxCenter(m);
            if (c == null || !DisplacementElement.IsAllowedAsDisplacedElement(m)) continue;
            var d = (c - center) * factor;
            if (d.GetLength() < 1e-4) continue;
            try
            {
                DisplacementElement.Create(doc, new List<ElementId> { id }, d, view, null);
            }
            catch (Exception ex)
            {
                Log.Warn("Vista explodida (deslocar peça): " + ex.Message);
            }
        }
        ctx.Report.Count(Loc.T("Vistas explodidas", "Exploded views"));
    }
}

public enum JoineryScope
{
    [Text("Seleção (ou o projeto, se nada estiver selecionado)", "Selection (or the project, if nothing is selected)")] Selection,
    [Text("Projeto inteiro", "Whole project")] Project,
}

public sealed class CutListOptions
{
    [Opt("Abrangência", "Scope")]
    public JoineryScope Scope { get; set; } = JoineryScope.Selection;

    [Opt("Área útil da chapa", "Usable sheet area", Suffix = "m²", Min = 0.1)]
    public double SheetAreaM2 { get; set; } = 2.75 * 1.85;

    [Opt("Perda de chapa (%)", "Sheet waste (%)", Min = 0, Max = 80)]
    public double SheetWaste { get; set; } = 15;

    [Opt("Perda de fita de borda (%)", "Edge band waste (%)", Min = 0, Max = 80)]
    public double EdgeWaste { get; set; } = 10;

    [Opt("Atualizar dados de corte nas peças (TT_Comprimento, TT_Especificação)", "Update cutting data on pieces (TT_Comprimento, TT_Especificacao)")]
    public bool WriteBack { get; set; } = true;

    [Opt("Salvar planilha Excel e CSV de fabricação", "Save Excel workbook and fabrication CSV")]
    public bool Save { get; set; } = true;

    [Opt("Pasta", "Folder", Kind = OptKind.Folder, VisibleIf = "Save", Tip = "Vazio = pasta de exportações do Téra Tools.|Empty = Téra Tools exports folder.")]
    public string Folder { get; set; } = "";

    [Opt("Criar tabela de peças no Revit", "Create part schedule in Revit")]
    public bool Schedule { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("join.cutlist", Modules.Joinery, "Plano de corte e ferragens", "Cut list and hardware", Order = 30, Large = true, Group = "Produção|Production",
    DescPt = "Gera a lista de peças (medidas de corte, fitas, veio), consumo de chapas e fitas, lista de ferragens e o arquivo para fabricação (Excel/CSV).",
    DescEn = "Generates the part list (cut sizes, edging, grain), board and edge band usage, hardware list and the fabrication file (Excel/CSV).",
    HelpPt = "As medidas vêm das peças do modelo (se você alterou uma peça no Revit, a alteração entra na lista). O CSV de fabricação usa colunas comuns (material, espessura, comprimento, largura, quantidade, veio, fitas); ajuste o mapeamento ao seu software de corte/máquina.",
    HelpEn = "Sizes come from the model pieces (if you edited a piece in Revit, the change is included). The fabrication CSV uses common columns; adjust the mapping to your cutting software/machine.",
    Keywords = "plano de corte lista de pecas chapas fitas ferragens excel csv fabricacao cut list hardware edge band")]
public sealed class CutListCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<CutListOptions>();
        var doc = ctx.Doc;
        var elements = JoineryGen.CollectBoards(ctx, o.Scope == JoineryScope.Project);
        var boards = JoineryGen.ReadBoards(doc, elements, out var hardware, ctx.Report);
        if (boards.Count == 0) return;
        var rows = CutList.Aggregate(boards.Select(b => (b.Module, b.Board)));
        var tables = new List<TableData>
        {
            CutList.ToTable(rows),
            CutList.SheetsByMaterial(rows, o.SheetAreaM2, o.SheetWaste),
            CutList.EdgeBanding(rows, o.EdgeWaste),
            CutList.Hardware(hardware),
            CutList.FabricationExport(rows),
        };
        ctx.Report.Tables.AddRange(tables);
        if (o.WriteBack || o.Schedule)
        {
            ctx.Modify(ctx.Tool.Name, () =>
            {
                if (o.WriteBack)
                {
                    foreach (var b in boards)
                    {
                        var changed = RevitGeo.Set(b.Element, b.Board.Length, SharedParams.Comprimento) | RevitGeo.Set(b.Element, b.Board.Thickness, SharedParams.Espessura)
                                      | RevitGeo.Set(b.Element, JoineryGen.Spec(b.Board), SharedParams.Especificacao);
                        if (changed) ctx.Report.Modified();
                    }
                }
                if (o.Schedule)
                {
                    var vs = Schedules.Create(doc, BuiltInCategory.OST_Casework, Loc.T("TT Plano de corte", "TT Cut list"),
                        new[]
                        {
                            new Schedules.Spec(SharedParams.Modulo, Loc.T("Módulo", "Module")), new Schedules.Spec(SharedParams.Peca, Loc.T("Peça", "Piece")),
                            new Schedules.Spec(SharedParams.Grupo, Loc.T("Função", "Role")), new Schedules.Spec(SharedParams.Material),
                            new Schedules.Spec(SharedParams.Especificacao, Loc.T("Corte (C × L × E)", "Cut (L × W × T)")),
                            new Schedules.Spec(SharedParams.Veio), new Schedules.Spec("#count", Loc.T("Qtd.", "Qty.")),
                        },
                        null, new[] { SharedParams.Modulo, SharedParams.Peca }, itemized: true, report: ctx.Report);
                    ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
                    ctx.Report.AddResult(vs.Id.Value);
                }
            });
        }
        if (o.Save)
        {
            try
            {
                var folder = string.IsNullOrWhiteSpace(o.Folder) ? Paths.Exports : o.Folder;
                Directory.CreateDirectory(folder);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmm");
                var title = TextUtil.SafeFileName(string.IsNullOrWhiteSpace(doc.Title) ? "projeto" : doc.Title);
                var xlsx = Path.Combine(folder, $"{title} - plano de corte {stamp}.xlsx");
                XlsxWriter.Write(xlsx, tables);
                var csv = Path.Combine(folder, $"{title} - fabricacao {stamp}.csv");
                CsvWriter.Write(tables[^1], csv, ctx.Settings.CsvSeparator);
                ctx.Report.Success(Loc.F("Arquivos salvos: {0} e {1}", "Files saved: {0} and {1}", xlsx, csv));
            }
            catch (Exception ex)
            {
                ctx.Report.Error(Loc.T("Não foi possível salvar os arquivos: ", "Could not save the files: ") + ex.Message);
            }
        }
        ctx.Report.Info(Loc.F("{0} peças em {1} linhas · {2} de chapa · {3} de fita", "{0} pieces in {1} rows · {2} of board · {3} of edge band",
            boards.Count, rows.Count, U.FormatArea(rows.Sum(r => r.Length * r.Width * r.Quantity)), U.FormatM(rows.Sum(r => r.EdgeLength))));
        ctx.Report.ForceShow = true;
    }
}

public sealed class NestingOptions
{
    [Opt("Abrangência", "Scope")]
    public JoineryScope Scope { get; set; } = JoineryScope.Selection;

    [Opt("Comprimento da chapa", "Sheet length", Kind = OptKind.Length, Min = 0.1, Group = "Chapa|Sheet")]
    public double SheetLength { get; set; } = U.Mm(2750);

    [Opt("Largura da chapa", "Sheet width", Kind = OptKind.Length, Min = 0.1, Group = "Chapa|Sheet")]
    public double SheetWidth { get; set; } = U.Mm(1850);

    [Opt("Refilo (borda descartada)", "Trim (discarded edge)", Kind = OptKind.Length, Min = 0, Group = "Chapa|Sheet")]
    public double Trim { get; set; } = U.Mm(10);

    [Opt("Espessura da serra (kerf)", "Saw kerf", Kind = OptKind.Length, Min = 0, Group = "Corte|Cutting")]
    public double Kerf { get; set; } = U.Mm(4);

    [Opt("Método", "Method", Group = "Corte|Cutting")]
    public NestingMethod Method { get; set; } = NestingMethod.Guillotine;

    [Opt("Permitir girar peças sem veio", "Allow rotating pieces without grain", Group = "Corte|Cutting")]
    public bool AllowRotation { get; set; } = true;

    [Opt("Sobra mínima reaproveitável", "Minimum reusable offcut", Kind = OptKind.Length, Min = 0, Group = "Corte|Cutting")]
    public double MinOffcut { get; set; } = U.Mm(300);

    [Opt("Tentativas de otimização", "Optimization attempts", Min = 1, Max = 200, Group = "Corte|Cutting")]
    public int Restarts { get; set; } = 12;

    [Opt("Desenhar planos de corte (vistas de desenho)", "Draw cutting plans (drafting views)", Group = "Saída|Output")]
    public bool Draw { get; set; } = true;

    [Opt("Escala dos desenhos (1:n)", "Drawing scale (1:n)", Min = 1, Max = 200, Group = "Saída|Output", VisibleIf = "Draw")]
    public int Scale { get; set; } = 20;
}

[Transaction(TransactionMode.Manual)]
[Tool("join.nesting", Modules.Joinery, "Otimizar chapas (nesting)", "Optimize sheets (nesting)", Order = 31, Group = "Produção|Production",
    DescPt = "Distribui as peças nas chapas (corte guilhotinado ou livre), respeitando veio, serra e refilo; mostra aproveitamento, sobras e desenha os planos de corte.",
    DescEn = "Lays the pieces out on sheets (guillotine or free cuts), respecting grain, kerf and trim; shows yield, offcuts and draws the cutting plans.",
    HelpPt = "É um otimizador heurístico: o resultado é bom para orçamento e planejamento, mas confira com o seu fornecedor/máquina. Arquivos específicos de CNC dependem do formato de cada máquina e não são gerados automaticamente.",
    HelpEn = "It is a heuristic optimizer: good for budgeting and planning, but check with your supplier/machine. CNC-specific files depend on each machine's format and are not generated automatically.",
    Keywords = "nesting otimizar chapas plano de corte aproveitamento sobras optimize sheets cutting plan yield offcuts")]
public sealed class NestingCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<NestingOptions>();
        var doc = ctx.Doc;
        var elements = JoineryGen.CollectBoards(ctx, o.Scope == JoineryScope.Project);
        var boards = JoineryGen.ReadBoards(doc, elements, out _, ctx.Report);
        if (boards.Count == 0) return;
        var rows = CutList.Aggregate(boards.Select(b => (b.Module, b.Board)));
        var parts = CutList.ToNesting(rows);
        var sheets = parts.GroupBy(p => (p.Material, T: Math.Round(U.ToMm(p.Thickness), 1)))
            .Select(g => new SheetSpec { Material = g.Key.Material, Thickness = U.Mm(g.Key.T), Length = o.SheetLength, Width = o.SheetWidth, HasGrain = g.Any(p => p.HasGrain) })
            .ToList();
        var settings = new NestingSettings { Method = o.Method, Kerf = o.Kerf, Trim = o.Trim, AllowRotation = o.AllowRotation, RandomRestarts = o.Restarts, MinReusableOffcut = o.MinOffcut };
        var result = Nester.Nest(parts, sheets, settings);
        ctx.Report.Tables.Add(result.Summary());
        ctx.Report.Tables.Add(result.PartList());
        foreach (var u in result.Unplaced)
            ctx.Report.Warn(Loc.F("Peça maior que a chapa: {0} ({1} × {2})", "Piece larger than the sheet: {0} ({1} × {2})", u.Name, U.FormatMm(u.Length), U.FormatMm(u.Width)));
        if (o.Draw && result.Sheets.Count > 0)
        {
            ctx.Modify(ctx.Tool.Name, () =>
            {
                var vft = Lookup.ViewFamilyType(doc, ViewFamily.Drafting) ?? throw new ToolException(Loc.T("Tipo de vista de desenho não encontrado.", "Drafting view type not found."));
                var textType = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                foreach (var g in result.Sheets.GroupBy(s => (s.Sheet.Material, T: Math.Round(U.ToMm(s.Sheet.Thickness)))))
                {
                    var view = ViewDrafting.Create(doc, vft.Id);
                    view.Name = Lookup.UniqueViewName(doc, Loc.F("TT Plano de corte - {0} {1} mm", "TT Cutting plan - {0} {1} mm", g.Key.Material, g.Key.T));
                    view.Scale = o.Scale;
                    var y = 0.0;
                    var list = g.ToList();
                    for (var i = 0; i < list.Count; i++)
                    {
                        DrawSheet(doc, view, textType, list[i], y, i + 1, list.Count, o.Scale);
                        y -= list[i].Sheet.Width + U.Mm(400);
                    }
                    ctx.Report.Count(Loc.T("Vistas de plano de corte", "Cutting plan views"));
                    ctx.Report.AddResult(view.Id.Value);
                }
            });
        }
        ctx.Report.ForceShow = true;
    }

    private static void DrawSheet(Document doc, View view, ElementId textType, SheetLayout layout, double y0, int index, int count, int scale)
    {
        void Rect(double x, double y, double w, double h)
        {
            var p = new[] { new XYZ(x, y, 0), new XYZ(x + w, y, 0), new XYZ(x + w, y + h, 0), new XYZ(x, y + h, 0) };
            for (var k = 0; k < 4; k++)
            {
                var a = p[k];
                var b = p[(k + 1) % 4];
                if (a.DistanceTo(b) > 1e-3) doc.Create.NewDetailCurve(view, Line.CreateBound(a, b));
            }
        }
        void Text(double x, double y, string s)
        {
            try
            {
                TextNote.Create(doc, view.Id, new XYZ(x, y, 0), s, textType);
            }
            catch (Exception ex)
            {
                Log.Warn("Texto do plano de corte: " + ex.Message);
            }
        }
        var sh = layout.Sheet;
        Rect(0, y0, sh.Length, sh.Width);
        Text(0, y0 + sh.Width + U.Mm(2.5 * scale), Loc.F("Chapa {0}/{1} — {2} {3} — {4} × {5} — aproveitamento {6:0.0}%",
            "Sheet {0}/{1} — {2} {3} — {4} × {5} — yield {6:0.0}%", index, count, sh.Material, U.FormatMm(sh.Thickness), U.FormatMm(sh.Length), U.FormatMm(sh.Width), layout.Utilization * 100));
        foreach (var p in layout.Parts)
        {
            Rect(p.X, y0 + p.Y, p.SizeX, p.SizeY);
            var label = $"{p.Part.Name.Split(',')[0]}\r{U.ToMm(p.Rotated ? p.Part.Width : p.Part.Length):0}×{U.ToMm(p.Rotated ? p.Part.Length : p.Part.Width):0}{(p.Rotated ? " ↻" : "")}";
            if (p.SizeX > U.Mm(6 * scale) && p.SizeY > U.Mm(4 * scale)) Text(p.X + U.Mm(1.5 * scale), y0 + p.Y + p.SizeY - U.Mm(1.5 * scale), label);
        }
    }
}

public sealed class JoineryRulesOptions
{
    [Opt("Dobradiças por altura da porta (altura máx. mm = quantidade)", "Hinges by door height (max height mm = count)", Kind = OptKind.MultiLine, Group = "Ferragens|Hardware")]
    public List<string> Hinges { get; set; } = new() { "900=2", "1600=3", "2000=4", "2600=5" };

    [Opt("Comprimentos de corrediça disponíveis (mm)", "Available slide lengths (mm)", Kind = OptKind.MultiLine, Group = "Ferragens|Hardware")]
    public List<string> Slides { get; set; } = new() { "250", "300", "350", "400", "450", "500", "550" };

    [Opt("Folga lateral da corrediça (mm por lado)", "Slide side clearance (mm per side)", Min = 0, Group = "Ferragens|Hardware")]
    public double SlideClearance { get; set; } = 12.7;

    [Opt("Folga traseira da gaveta (mm)", "Drawer rear clearance (mm)", Min = 0, Group = "Ferragens|Hardware")]
    public double DrawerRear { get; set; } = 20;

    [Opt("Redução da altura da caixa de gaveta (mm)", "Drawer box height reduction (mm)", Min = 0, Group = "Ferragens|Hardware")]
    public double DrawerReduction { get; set; } = 50;

    [Opt("Suportes por prateleira", "Supports per shelf", Min = 0, Max = 12, Group = "Ferragens|Hardware")]
    public int ShelfSupports { get; set; } = 4;

    [Opt("Vão máximo de prateleira sem divisória (mm)", "Maximum shelf span without divider (mm)", Min = 100, Group = "Estrutura|Structure")]
    public double MaxShelfSpan { get; set; } = 900;

    [Opt("Espaçamento máximo entre pés (mm)", "Maximum foot spacing (mm)", Min = 100, Group = "Estrutura|Structure")]
    public double FootSpacing { get; set; } = 800;

    [Opt("Conectores por junção", "Connectors per joint", Min = 0, Max = 10, Group = "Estrutura|Structure")]
    public int Connectors { get; set; } = 2;

    [Opt("Largura mínima de porta (mm)", "Minimum door width (mm)", Min = 50, Group = "Frentes|Fronts")]
    public double MinDoor { get; set; } = 200;

    [Opt("Largura máxima de porta única (mm)", "Maximum single door width (mm)", Min = 100, Group = "Frentes|Fronts")]
    public double MaxSingleDoor { get; set; } = 600;

    [Opt("Perda de fita de borda (%)", "Edge band waste (%)", Min = 0, Max = 80, Group = "Consumo|Usage")]
    public double EdgeWaste { get; set; } = 10;

    public static JoineryRulesOptions From(JoineryRules r) => new()
    {
        Hinges = r.Hinges.Select(h => $"{h.MaxHeightMm:0}={h.Count}").ToList(),
        Slides = r.SlideLengthsMm.Select(s => s.ToString("0")).ToList(),
        SlideClearance = r.SlideSideClearanceMm, DrawerRear = r.DrawerRearClearanceMm, DrawerReduction = r.DrawerBoxHeightReductionMm,
        ShelfSupports = r.ShelfSupportsPerShelf, MaxShelfSpan = r.MaxShelfSpanMm, FootSpacing = r.FootSpacingMaxMm, Connectors = r.ConnectorsPerJoint,
        MinDoor = r.MinDoorWidthMm, MaxSingleDoor = r.MaxSingleDoorWidthMm, EdgeWaste = r.EdgeBandWastePercent,
    };

    public JoineryRules ToRules()
    {
        var r = new JoineryRules
        {
            SlideSideClearanceMm = SlideClearance, DrawerRearClearanceMm = DrawerRear, DrawerBoxHeightReductionMm = DrawerReduction,
            ShelfSupportsPerShelf = ShelfSupports, MaxShelfSpanMm = MaxShelfSpan, FootSpacingMaxMm = FootSpacing, ConnectorsPerJoint = Connectors,
            MinDoorWidthMm = MinDoor, MaxSingleDoorWidthMm = MaxSingleDoor, EdgeBandWastePercent = EdgeWaste,
        };
        var hinges = new List<HingeRule>();
        foreach (var line in Hinges)
        {
            var p = line.Split('=', 2);
            if (p.Length == 2 && U.TryParseNumber(p[0], out var h) && int.TryParse(p[1].Trim(), out var n) && n > 0) hinges.Add(new HingeRule { MaxHeightMm = h, Count = n });
        }
        if (hinges.Count > 0) r.Hinges = hinges.OrderBy(h => h.MaxHeightMm).ToList();
        var slides = Slides.Select(s => U.TryParseNumber(s, out var v) ? v : 0).Where(v => v > 0).OrderBy(v => v).ToList();
        if (slides.Count > 0) r.SlideLengthsMm = slides;
        return r;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("join.rules", Modules.Joinery, "Regras de marcenaria", "Joinery rules", Order = 40, Group = "Produção|Production",
    DescPt = "Define as regras do projeto: dobradiças por altura, corrediças, folgas, suportes, conectores, larguras de porta e perdas.",
    DescEn = "Sets the project rules: hinges by height, slides, clearances, supports, connectors, door widths and waste.",
    HelpPt = "As regras ficam salvas no próprio arquivo do projeto e valem para novos módulos, edições e listas de ferragens. Salve presets para reutilizar em outros projetos.",
    HelpEn = "Rules are stored in the project file and apply to new modules, edits and hardware lists. Save presets to reuse in other projects.",
    Keywords = "regras marcenaria dobradicas corredicas folgas ferragens rules joinery hinges slides hardware")]
public sealed class JoineryRulesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var current = JoineryRulesOptions.From(JoineryGen.Rules(ctx.Doc));
        var o = ctx.Ask(current);
        ctx.Modify(ctx.Tool.Name, () => ProjectStorage.SetSection(ctx.Doc, JoineryGen.RulesKey, o.ToRules()));
        ctx.Report.Success(Loc.T("Regras salvas no projeto. Use \"Editar módulo\" para aplicá-las a módulos existentes.", "Rules saved in the project. Use \"Edit module\" to apply them to existing modules."));
    }
}
