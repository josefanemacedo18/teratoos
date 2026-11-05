using System.Diagnostics;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Joinery;
using TeraTools.Core.Localization;
using TeraTools.Core.Organic;
using TeraTools.Core.Reporting;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Floors;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Tools.Joinery;
using TeraTools.Revit.Tools.Organic;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.General;

/// <summary>
/// Autoteste dentro do Revit: monta um cenário temporário longe do modelo (paredes, laje, ambiente e uma
/// parede de fachada), roda os geradores e as ferramentas de interiores com opções padrão e desfaz tudo no
/// fim. O modelo não é alterado; o relatório diz o que funcionou neste computador e nesta versão do Revit.
/// </summary>
[Transaction(TransactionMode.Manual)]
[Tool("general.selftest", Modules.General, "Autoteste do plugin", "Plugin self-test", Order = 30,
    DescPt = "Roda os geradores e as ferramentas principais num cenário temporário e desfaz tudo no fim: mostra o que funciona neste Revit, sem alterar o modelo.",
    DescEn = "Runs the generators and main tools in a temporary scenario and undoes everything: shows what works in this Revit without changing the model.",
    HelpPt = "Use depois de instalar ou atualizar o plugin, ou quando alguma ferramenta \"não gerar nada\".\n\nO teste cria, longe do modelo, quatro paredes com laje e um ambiente, e uma parede de fachada. Em seguida roda: colunas e coberturas orgânicas, lâminas de fachada, quadro espacial, aletas, padrões, painéis cinéticos, piso, rodapé, forro e revestimento por ambiente, paginação e marcenaria (forma livre e famílias). No fim, tudo é desfeito.\n\nO relatório mostra, para cada item: OK, Atenção (gerou, mas com avisos) ou Falhou (com o motivo). Envie esse relatório junto com o seu retorno.",
    HelpEn = "Builds a temporary scenario far from the model, runs the generators and main tools with default options and rolls everything back. The report lists OK / Warning / Failed per tool.",
    Keywords = "autoteste teste diagnostico verificar funcionamento self test diagnostics check")]
public sealed class SelfTestCommand : ToolCommand
{
    private sealed record Scenario(Level Level, XYZ Origin, List<Wall> RoomWalls, Wall Facade, Floor Slab, Room? Room);

    private sealed class Outcome
    {
        public string Name = "";
        public string Status = "";
        public int Created;
        public int Warnings;
        public double Seconds;
        public string Message = "";
    }

    public override void Run(ToolContext ctx)
    {
        if (!Msg.Confirm(Loc.T("O autoteste cria um cenário temporário longe do modelo, roda as ferramentas e desfaz tudo no fim (leva de alguns segundos a um minuto). Continuar?",
                "The self-test builds a temporary scenario far from the model, runs the tools and undoes everything (a few seconds to a minute). Continue?"), ctx.Tool.Name))
            ctx.Cancel();
        var doc = ctx.Doc;
        var results = new List<Outcome>();
        var total = Stopwatch.StartNew();
        using (var group = new TransactionGroup(doc, "Téra Tools: autoteste"))
        {
            group.Start();
            try
            {
                Scenario? sc = null;
                results.Add(Step(ctx, "general.selftest", Loc.T("Cenário de teste (paredes, laje e ambiente)", "Test scenario (walls, slab and room)"), sub =>
                {
                    sc = Build(sub);
                    sub.Report.Created(sc.RoomWalls.Count + 2 + (sc.Room != null ? 1 : 0));
                    if (sc.Room == null) sub.Report.Warn(Loc.T("O ambiente de teste não pôde ser criado: os testes por ambiente vão falhar.", "The test room could not be created."));
                }));
                if (sc != null) RunAll(ctx, sc, results);
            }
            finally
            {
                // sempre desfaz: o modelo volta exatamente ao que era
                if (group.GetStatus() == TransactionStatus.Started) group.RollBack();
            }
        }
        total.Stop();

        var t = new TableData(Loc.T("Resultado do autoteste", "Self-test result"));
        t.AddColumn(Loc.T("Ferramenta", "Tool"))
         .AddColumn(Loc.T("Resultado", "Result"))
         .AddColumn(Loc.T("Criados", "Created"), ColumnKind.Integer)
         .AddColumn(Loc.T("Avisos", "Warnings"), ColumnKind.Integer)
         .AddColumn(Loc.T("Tempo (s)", "Time (s)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Mensagem", "Message"));
        foreach (var r in results) t.AddRow(r.Name, r.Status, r.Created, r.Warnings, r.Seconds, r.Message);
        ctx.Report.Tables.Add(t);
        var ok = results.Count(r => r.Status == StatusOk);
        var warn = results.Count(r => r.Status == StatusWarn);
        var fail = results.Count(r => r.Status == StatusFail);
        var summary = Loc.F("Autoteste: {0} OK, {1} com atenção, {2} falharam ({3} s). Revit {4}, Téra Tools {5}. Tudo foi desfeito: o modelo não foi alterado.",
            "Self-test: {0} OK, {1} with warnings, {2} failed ({3} s). Revit {4}, Téra Tools {5}. Everything was undone: the model was not changed.",
            ok, warn, fail, Math.Round(total.Elapsed.TotalSeconds, 1), ctx.App.Application.VersionNumber + " " + ctx.App.Application.SubVersionNumber,
            typeof(SelfTestCommand).Assembly.GetName().Version?.ToString(3));
        if (fail > 0) ctx.Report.Warn(summary);
        else ctx.Report.Info(summary);
        Log.Info(summary);
        foreach (var r in results.Where(r => r.Status != StatusOk)) Log.Info($"Autoteste — {r.Name}: {r.Status} — {r.Message}");
        ctx.Report.ForceShow = true;
    }

    private static string StatusOk => Loc.T("OK", "OK");
    private static string StatusWarn => Loc.T("Atenção", "Warning");
    private static string StatusFail => Loc.T("Falhou", "Failed");

    /// <summary>Roda uma etapa com um contexto próprio (relatório separado) e resume o resultado.</summary>
    private static Outcome Step(ToolContext parent, string toolId, string name, Action<ToolContext> action)
    {
        var info = ToolCatalog.Get(toolId) ?? parent.Tool;
        var sub = new ToolContext(parent.App, info) { Silent = true };
        var o = new Outcome { Name = name };
        var sw = Stopwatch.StartNew();
        try
        {
            action(sub);
            var created = sub.Report.GetCount(Loc.T("Criados", "Created"));
            o.Created = Math.Max(created, sub.Report.ResultElementIds.Count);
            var problems = sub.Report.Items.Where(i => i.Severity is Severity.Warning or Severity.Error).ToList();
            o.Warnings = problems.Count;
            o.Status = o.Created == 0 ? StatusFail : problems.Count > 0 ? StatusWarn : StatusOk;
            o.Message = o.Created == 0
                ? Loc.T("Terminou sem criar nada.", "Finished without creating anything.") + (problems.Count > 0 ? " " + problems[0].Message : "")
                : problems.FirstOrDefault()?.Message ?? "";
        }
        catch (Exception ex)
        {
            o.Status = StatusFail;
            o.Message = ex is ToolException ? ex.Message : ex.GetType().Name + ": " + ex.Message;
            Log.Error("Autoteste — " + name, ex);
        }
        o.Seconds = Math.Round(sw.Elapsed.TotalSeconds, 2);
        if (o.Message.Length > 240) o.Message = o.Message[..240] + "…";
        return o;
    }

    // ------------------------------------------------------------------ cenário

    private static Scenario Build(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var level = (ctx.ActiveView as ViewPlan)?.GenLevel
                    ?? Lookup.Collect<Level>(doc).OrderBy(l => l.Elevation).FirstOrDefault()
                    ?? throw new ToolException(Loc.T("O projeto não tem níveis.", "The project has no levels."));
        var wallType = Lookup.Collect<WallType>(doc).Where(w => w.Kind == WallKind.Basic).OrderByDescending(w => w.Width).FirstOrDefault()
                       ?? throw new ToolException(Loc.T("O projeto não tem tipos de parede básica.", "No basic wall types."));
        var floorType = Lookup.Collect<FloorType>(doc).Where(f => !f.IsFoundationSlab).OrderByDescending(f => f.GetCompoundStructure()?.GetWidth() ?? 0).FirstOrDefault()
                        ?? throw new ToolException(Loc.T("O projeto não tem tipos de piso.", "No floor types."));
        // longe do modelo (500 m), para não interferir em nada
        var o = new XYZ(U.M(500), U.M(500), level.Elevation);
        Scenario? sc = null;
        ctx.Modify(Loc.T("Cenário de teste", "Test scenario"), () =>
        {
            var w = U.M(6);
            var d = U.M(4);
            var pts = new[] { o, o + new XYZ(w, 0, 0), o + new XYZ(w, d, 0), o + new XYZ(0, d, 0) };
            var walls = new List<Wall>();
            for (var i = 0; i < 4; i++)
                walls.Add(Wall.Create(doc, Line.CreateBound(pts[i], pts[(i + 1) % 4]), wallType.Id, level.Id, U.M(3), 0, false, false));
            var loop = CurveLoop.Create(Enumerable.Range(0, 4).Select(i => (Curve)Line.CreateBound(pts[i], pts[(i + 1) % 4])).ToList());
            var slab = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
            var facadeStart = o + new XYZ(0, -U.M(4), 0);
            var facade = Wall.Create(doc, Line.CreateBound(facadeStart, facadeStart + new XYZ(U.M(10), 0, 0)), wallType.Id, level.Id, U.M(6), 0, false, false);
            doc.Regenerate();
            Room? room = null;
            try
            {
                room = doc.Create.NewRoom(level, new UV(o.X + w / 2, o.Y + d / 2));
                room.Name = "TT Autoteste";
                doc.Regenerate();
            }
            catch (Exception ex)
            {
                ctx.ItemFailed("Ambiente de teste", "Test room", ex);
            }
            sc = new Scenario(level, o, walls, facade, slab, room is { Area: > 0 } ? room : null);
        });
        return sc!;
    }

    private static (Region2 Region, Frame3 Frame, Reference Ref) ExteriorFace(Document doc, Wall wall)
    {
        foreach (var r in HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior))
            if (wall.GetGeometryObjectFromReference(r) is PlanarFace pf)
            {
                var (reg, frame) = GenHost.PlanarFaceRegion(pf, Transform.Identity);
                return (reg, frame, r);
            }
        throw new ToolException(Loc.T("A parede de teste não tem face externa plana.", "The test wall has no planar exterior face."));
    }

    // ------------------------------------------------------------------ testes

    private static void RunAll(ToolContext ctx, Scenario sc, List<Outcome> results)
    {
        var doc = ctx.Doc;
        var o = sc.Origin;
        var baseZ = sc.Level.Elevation;
        var rooms = sc.Room != null ? new List<Room> { sc.Room } : new List<Room>();
        string Thinnest<T>(Func<T, double> width) where T : ElementType => Lookup.Collect<T>(doc).OrderBy(width).Select(t => t.Name).FirstOrDefault() ?? "";
        var thinWall = Lookup.Collect<WallType>(doc).Where(w => w.Kind == WallKind.Basic).OrderBy(w => w.Width).Select(w => w.Name).FirstOrDefault() ?? "";
        var thinFloor = Lookup.Collect<FloorType>(doc).Where(f => !f.IsFoundationSlab).OrderBy(f => f.GetCompoundStructure()?.GetWidth() ?? 0).Select(f => f.Name).FirstOrDefault() ?? "";
        var ceiling = Thinnest<CeilingType>(c => c.GetCompoundStructure()?.GetWidth() ?? 0);

        // ---- Orgânicos
        results.Add(Step(ctx, "org.column", Loc.T("Colunas orgânicas (tulipa e treliça)", "Organic columns (tulip and lattice)"), sub =>
        {
            var opt = new OrganicColumnOptions();
            opt.ApplyStyle(ColumnStyles.Find("tulipa")!.Create());
            var c = new ColumnContext { Points = { new[] { o.X + U.M(8), o.Y + U.M(1), baseZ } } };
            OrganicColumnCommand.Generate(sub, opt, c, GenSystem.NewId());
            var lattice = new OrganicColumnOptions();
            lattice.ApplyStyle(ColumnStyles.Find("trelica")!.Create());
            OrganicColumnCommand.Generate(sub, lattice, new ColumnContext { Points = { new[] { o.X + U.M(8), o.Y + U.M(3), baseZ } } }, GenSystem.NewId());
        }));
        results.Add(Step(ctx, "org.canopy", Loc.T("Cobertura orgânica (almofada sobre árvores)", "Organic canopy (pillow on trees)"), sub =>
        {
            var opt = new OrganicCanopyOptions();
            var st = CanopyStyles.Find("arvores")!;
            opt.ApplyStyle(st.Create(), st.SupportsU, st.SupportsV);
            var poly = Polygon2.Rectangle(new Vec2(o.X + U.M(12), o.Y), U.M(8), U.M(6));
            OrganicCanopyCommand.Generate(sub, opt, new CanopyContext { Footprint = OrganicGeo.Pack(poly), BaseZ = baseZ }, GenSystem.NewId());
        }));
        results.Add(Step(ctx, "org.fins", Loc.T("Lâminas de fachada (cobre com arco)", "Facade fins (copper with arch)"), sub =>
        {
            var opt = new OrganicFinOptions();
            var s = new FinFacadeSettings();
            FinStyles.Find("cobre")!.Apply(s);
            opt.ApplyStyle(s);
            var (path, _, z0, z1, ext) = GenHost.WallFacade(sc.Facade);
            opt.Side = path.TangentAt(0).Perp.Dot(ext) >= 0 ? Core.Facade.FinSide.Left : Core.Facade.FinSide.Right;
            opt.TopZ = z1 - z0;
            opt.ArchWidth = U.M(3);
            opt.ArchCrown = U.M(3.2);
            opt.ArchSpring = U.M(2);
            var c = new FinContext { Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList(), BaseZ = z0, FacadeOffset = sc.Facade.Width / 2, WallId = sc.Facade.Id.Value };
            OrganicFinCommand.Generate(sub, opt, c, GenSystem.NewId());
        }));

        // ---- Geradores
        results.Add(Step(ctx, "gen.spaceframe", Loc.T("Quadro espacial", "Space frame"), sub =>
        {
            var opt = new SpaceFrameOptions { CreateView = false, ModuleX = U.M(1.5), ModuleY = U.M(1.5) };
            var r = Polygon2.Rectangle(new Vec2(o.X + U.M(22), o.Y), U.M(6), U.M(4.5));
            var c = new SpaceFrameCommand.Context { Footprint = r.Select(p => new[] { p.X, p.Y }).ToList(), BaseZ = baseZ, UnderZ = baseZ + U.M(4), Kind = SpaceFrameCommand.BaseKind.Rectangle };
            SpaceFrameCommand.Generate(sub, opt, c, GenSystem.NewId());
        }));
        results.Add(Step(ctx, "gen.brise", Loc.T("Aletas e brises (parede)", "Louvers (wall)"), sub =>
        {
            var (path, facade, z0, z1, ext) = GenHost.WallFacade(sc.Facade);
            var c = new BriseCommand.Context
            {
                Path = path.Points.Select(p => new[] { p.X, p.Y }).ToList(), Z0 = z0, Z1 = z1, LevelZ = baseZ, WallId = sc.Facade.Id.Value,
                Left = path.TangentAt(0).Perp.Dot(ext) >= 0, FacadeOffset = sc.Facade.Width / 2, Facade = PlanarContext.From(Frame3.World, facade),
            };
            BriseCommand.Generate(sub, new BriseOptions(), c, GenSystem.NewId());
        }));
        results.Add(Step(ctx, "gen.pattern", Loc.T("Padrões (face da parede)", "Patterns (wall face)"), sub =>
        {
            var (reg, frame, r) = ExteriorFace(doc, sc.Facade);
            var c = PlanarContext.From(frame, reg, sc.Facade.Id.Value);
            c.FaceReference = r.ConvertToStableRepresentation(doc);
            PatternCommand.Generate(sub, new PatternOptions(), c, GenSystem.NewId());
        }));
        results.Add(Step(ctx, "gen.kinetic", Loc.T("Painéis cinéticos (face da parede)", "Kinetic panels (wall face)"), sub =>
        {
            var (reg, frame, _) = ExteriorFace(doc, sc.Facade);
            var c = new KineticCommand.Context { Frame = PlanarContext.From(frame, reg).Frame, FaceWidth = reg.Bounds.Width, FaceHeight = reg.Bounds.Height };
            KineticCommand.Generate(sub, new KineticOptions(), c, GenSystem.NewId());
        }));

        // ---- Interiores (precisam do ambiente)
        results.Add(Step(ctx, "floor.byroom", Loc.T("Piso por ambiente", "Floor by room"), sub =>
        {
            if (rooms.Count == 0) throw new ToolException(Loc.T("Sem ambiente de teste.", "No test room."));
            FloorByRoomCommand.Execute(sub, rooms, new FloorByRoomOptions { FloorType = thinFloor });
        }));
        results.Add(Step(ctx, "floor.baseboard", Loc.T("Rodapé por ambiente", "Baseboard by room"), sub =>
        {
            if (rooms.Count == 0) throw new ToolException(Loc.T("Sem ambiente de teste.", "No test room."));
            BaseboardCommand.Execute(sub, rooms, new BaseboardOptions { WallType = thinWall });
        }));
        results.Add(Step(ctx, "ceiling.byroom", Loc.T("Forro por ambiente", "Ceiling by room"), sub =>
        {
            if (rooms.Count == 0) throw new ToolException(Loc.T("Sem ambiente de teste.", "No test room."));
            if (ceiling == "") throw new ToolException(Loc.T("O projeto não tem tipos de forro.", "No ceiling types."));
            CeilingByRoomCommand.Execute(sub, rooms, new CeilingByRoomOptions { Type = ceiling });
        }));
        results.Add(Step(ctx, "wall.finish", Loc.T("Revestimento de parede por ambiente", "Wall finish by room"), sub =>
        {
            if (rooms.Count == 0) throw new ToolException(Loc.T("Sem ambiente de teste.", "No test room."));
            WallFinishCommand.Execute(sub, rooms, new WallFinishOptions { Type = thinWall });
        }));
        results.Add(Step(ctx, "floor.paging", Loc.T("Paginação de piso (laje de teste)", "Floor tile layout (test slab)"), sub =>
        {
            var job = PagingCommand.JobFor(sc.Slab, true) ?? throw new ToolException(Loc.T("Não foi possível ler a laje de teste.", "Could not read the test slab."));
            PagingCommand.Execute(sub, new List<(Element Host, Region2 Region, Frame3 Frame, Plane Plane, PartsService.Side Side)> { job }, new PagingOptions { CreateSchedule = false }, null);
        }));

        // ---- Marcenaria
        results.Add(Step(ctx, "join.module", Loc.T("Marcenaria: módulo em forma livre", "Joinery: free-form module"), sub =>
        {
            var rec = new ModuleRecord { Spec = CabinetSpec.Defaults(CabinetKind.Base), FreeForm = true, Assembly = false };
            var ids = JoineryGen.Generate(sub, rec, ModuleContext.From(o + new XYZ(U.M(1), U.M(0.5), 0), XYZ.BasisX), GenSystem.NewId());
            sub.Report.Created(ids.Count);
        }));
        results.Add(Step(ctx, "join.module", Loc.T("Marcenaria: módulo com famílias TT_Chapa", "Joinery: module with TT_Chapa families"), sub =>
        {
            var rec = new ModuleRecord { Spec = CabinetSpec.Defaults(CabinetKind.Wall), FreeForm = false, Assembly = true };
            var ids = JoineryGen.Generate(sub, rec, ModuleContext.From(o + new XYZ(U.M(3), U.M(0.5), 0), XYZ.BasisX), GenSystem.NewId());
            sub.Report.Created(ids.Count);
        }));
        results.Add(Step(ctx, "join.free", Loc.T("Marcenaria: móvel livre (modelo pronto)", "Joinery: free cabinet (template)"), sub =>
        {
            var rec = new FreeRecord { Design = FreeDesign.FromTemplate(0), FreeForm = true, Assembly = false };
            var ids = JoineryGen.Generate(sub, rec, ModuleContext.From(o + new XYZ(U.M(1), U.M(2.5), 0), XYZ.BasisX), GenSystem.NewId());
            sub.Report.Created(ids.Count);
        }));
    }
}
