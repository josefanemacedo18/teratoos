using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Geometry;
using TeraTools.Core.Layout;
using TeraTools.Core.Localization;
using TeraTools.Core.Mathx;
using TeraTools.Core.Serialization;
using TeraTools.Core.Settings;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

public enum DistributeMode
{
    [Text("Quantidade", "Count")] Count,
    [Text("Espaçamento", "Spacing")] Spacing,
}

public sealed class FurnitureDistributeOptions
{
    [Opt("Família/tipo (vazio = copiar o elemento escolhido)", "Family/type (empty = copy the picked element)", Kind = OptKind.FamilyType, Filter = InteriorsUtil.FurnitureCatList)]
    public string Type { get; set; } = "";

    [Opt("Distribuir por", "Distribute by")]
    public DistributeMode Mode { get; set; } = DistributeMode.Count;

    [Opt("Quantidade", "Count", Min = 1, Max = 500, VisibleIf = "Mode=Count")]
    public int Count { get; set; } = 4;

    [Opt("Espaçamento", "Spacing", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Mode=Spacing")]
    public double Spacing { get; set; } = U.Mm(600);

    [Opt("Distância do início", "Start distance", Kind = OptKind.Length, Min = 0)]
    public double StartOffset { get; set; } = U.Mm(300);

    [Opt("Distância do fim", "End distance", Kind = OptKind.Length, Min = 0)]
    public double EndOffset { get; set; } = U.Mm(300);

    [Opt("Afastamento da linha/face da parede", "Distance from line/wall face", Kind = OptKind.Length)]
    public double Offset { get; set; } = U.Mm(10);

    [Opt("Lado oposto", "Opposite side")]
    public bool OtherSide { get; set; }

    [Opt("De frente para a linha (em vez de costas)", "Facing the line (instead of back to it)")]
    public bool FaceLine { get; set; }

    [Opt("Rotação adicional", "Additional rotation", Kind = OptKind.Angle)]
    public double Rotation { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.distribute", Modules.Interiors, "Distribuir ao longo de parede/linha", "Distribute along wall/line", Order = 21, Group = "Mobiliário|Furniture",
    DescPt = "Distribui móveis (cadeiras, mesas, armários) ao longo de uma parede ou linha, por quantidade ou espaçamento, alinhados a ela.",
    DescEn = "Distributes furniture (chairs, desks, cabinets) along a wall or line, by count or spacing, aligned to it.",
    Keywords = "distribuir ao longo parede linha cadeiras distribute along wall line chairs")]
public sealed class FurnitureDistributeCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var host = ctx.PickElement("Escolha a parede ou a linha", "Pick the wall or the line", e => e is Wall { Location: LocationCurve } || e is CurveElement);
        var o = ctx.Ask<FurnitureDistributeOptions>();
        FamilyInstance? source = null;
        FamilySymbol? sym = null;
        if (string.IsNullOrWhiteSpace(o.Type))
            source = ctx.PickElement("Escolha o móvel a copiar", "Pick the item to copy", e => e is FamilyInstance { Location: LocationPoint }) as FamilyInstance;
        else
            sym = Lookup.Symbol(doc, o.Type) ?? throw new ToolException(Loc.T("Família/tipo não encontrado.", "Family/type not found."));
        if (sym != null && !InteriorsUtil.IsFreePlacement(sym))
            ToolContext.Fail("A família escolhida é hospedada; escolha uma por nível ou copie um elemento existente.", "The chosen family is hosted; choose a level-based one or copy an existing element.");

        var curve = host is Wall w ? ((LocationCurve)w.Location).Curve : ((CurveElement)host).GeometryCurve;
        var path = new PolylinePath(RevitGeo.TessellateXY(curve));
        var halfWall = host is Wall ww ? ww.Width / 2 : 0;
        var side = o.OtherSide ? -1.0 : 1.0;
        if (host is Wall wall && source != null && source.Location is LocationPoint slp)
        {
            // Por padrão, usa o lado da parede onde está o móvel de origem.
            var s0 = GenHost.ProjectOnPath(path, slp.Point.ToVec2());
            var n0 = path.TangentAt(s0).Perp;
            if ((slp.Point.ToVec2() - path.PointAt(s0)).Dot(n0) < 0) side = -side;
        }
        var level = host is Wall hw ? doc.GetElement(hw.LevelId) as Level : Lookup.LevelBelow(doc, curve.GetEndPoint(0).Z + 0.01);
        level ??= RevitGeo.ViewLevel(ctx.ActiveView);

        var usable = path.Length - o.StartOffset - o.EndOffset;
        if (usable < 0) ToolContext.Fail("As distâncias de início e fim são maiores que a linha.", "Start and end distances exceed the line length.");
        var stations = new List<double>();
        if (o.Mode == DistributeMode.Count)
        {
            if (o.Count == 1) stations.Add(o.StartOffset + usable / 2);
            else for (var i = 0; i < o.Count; i++) stations.Add(o.StartOffset + usable * i / (o.Count - 1));
        }
        else
        {
            var n = (int)Math.Floor(usable / o.Spacing + 1e-9) + 1;
            var start = o.StartOffset + (usable - (n - 1) * o.Spacing) / 2;
            for (var i = 0; i < n; i++) stations.Add(start + i * o.Spacing);
        }
        ctx.CheckCount(stations.Count, "móveis", "items");
        var rot = MathUtil.ToRadians(o.Rotation);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var s in stations)
            {
                var pt = path.PointAt(s);
                var normal = path.TangentAt(s).Perp * side;
                FamilyInstance fi;
                if (source != null)
                {
                    var lp = (LocationPoint)source.Location;
                    var target = pt + normal * (halfWall + o.Offset + 0.5);
                    var ids = ElementTransformUtils.CopyElement(doc, source.Id, new XYZ(target.X - lp.Point.X, target.Y - lp.Point.Y, 0));
                    fi = (FamilyInstance)doc.GetElement(ids.First());
                }
                else
                {
                    var target = pt + normal * (halfWall + o.Offset + 0.5);
                    fi = InteriorsUtil.PlaceFree(doc, sym!, level!, target.ToXYZ(), 0);
                }
                var facing = (o.FaceLine ? normal * -1 : normal).Rotate(rot);
                InteriorsUtil.TurnTo(doc, fi, facing);
                InteriorsUtil.PushOff(doc, fi, pt, normal, halfWall + o.Offset);
                ctx.Report.Created();
                ctx.Report.AddResult(fi.Id.Value);
            }
        });
    }
}

public sealed class AlignWallOptions
{
    [Opt("Distância da parede", "Distance from wall", Kind = OptKind.Length, Min = 0)]
    public double Distance { get; set; } = U.Mm(10);

    [Opt("Girar de costas para a parede", "Turn its back to the wall")]
    public bool Rotate { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.alignwall", Modules.Interiors, "Encostar na parede", "Push against wall", Order = 22, Group = "Mobiliário|Furniture",
    DescPt = "Gira cada móvel de costas para a parede mais próxima do ambiente e o encosta nela, com a folga indicada.",
    DescEn = "Turns each item with its back to the nearest room wall and pushes it against it, with the given gap.",
    Keywords = "encostar alinhar parede mobiliario align wall furniture push")]
public sealed class AlignWallCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var items = ctx.SelectedOrPick("Selecione os móveis", "Select the items", e => e is FamilyInstance { Location: LocationPoint }).OfType<FamilyInstance>().ToList();
        var o = ctx.Ask<AlignWallOptions>();
        var doc = ctx.Doc;
        var rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var fi in items)
            {
                var fp = InteriorsUtil.FootprintOf(fi);
                var c = fp.Outline.Count > 0 ? fp.Outline.Centroid : ((LocationPoint)fi.Location).Point.ToVec2();
                var lz = RevitGeo.LevelOf(fi)?.Elevation ?? ((LocationPoint)fi.Location).Point.Z;
                var room = RevitGeo.RoomAt(doc, new XYZ(c.X, c.Y, lz), rooms);
                var reg = room == null ? null : RevitGeo.RoomRegion(room);
                var near = reg == null ? null : InteriorsUtil.NearestWall(reg, c);
                if (near == null)
                {
                    ctx.Report.Warn(Loc.F("\"{0}\" não está dentro de um ambiente.", "\"{0}\" is not inside a room.", InteriorsUtil.Name(fi)), fi.Id.Value);
                    ctx.Report.Skipped();
                    continue;
                }
                if (o.Rotate) InteriorsUtil.TurnTo(doc, fi, near.Value.Inward);
                InteriorsUtil.PushOff(doc, fi, near.Value.Point, near.Value.Inward, o.Distance);
                ctx.Report.Modified();
                ctx.Report.AddResult(fi.Id.Value);
            }
        });
    }
}

// ============================================================ Presets de layout

internal static class LayoutPresets
{
    public const string Key = "furn.layout";

    public static List<Preset> All(Document doc)
    {
        var list = new List<Preset>(Services.Presets.List(Key));
        try
        {
            list.AddRange(ProjectStorage.LoadPresets(doc).List(Key));
        }
        catch (Exception ex)
        {
            Log.Debug("Presets do projeto: " + ex.Message);
        }
        return list;
    }

    /// <summary>Ponto base e direção do eixo X (segundo clique opcional).</summary>
    public static (XYZ Base, double Angle) PickFrame(ToolContext ctx)
    {
        var b = ctx.PickPoint("Clique no ponto base do layout (ex.: canto do ambiente)", "Click the layout base point (e.g., room corner)");
        double angle = 0;
        try
        {
            var d = ctx.PickPoint("Clique na direção do eixo X (Esc = horizontal)", "Click the X axis direction (Esc = horizontal)");
            var v = d.ToVec2() - b.ToVec2();
            if (v.Length > 1e-3) angle = v.Angle;
        }
        catch (Autodesk.Revit.Exceptions.OperationCanceledException)
        {
        }
        return (b, angle);
    }
}

public sealed class LayoutSaveOptions
{
    [Opt("Nome do layout", "Layout name", Required = true)]
    public string Name { get; set; } = "Layout";

    [Opt("Descrição", "Description")]
    public string Description { get; set; } = "";

    [Opt("Salvar também no projeto (compartilhado com o arquivo)", "Also save in the project (shared with the file)")]
    public bool InProject { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.presetsave", Modules.Interiors, "Salvar layout de mobiliário", "Save furniture layout", Order = 23, Group = "Mobiliário|Furniture",
    DescPt = "Guarda os móveis selecionados como um layout reutilizável (posições relativas a um ponto base e direção).",
    DescEn = "Stores the selected items as a reusable layout (positions relative to a base point and direction).",
    Keywords = "salvar layout preset mobiliario save layout furniture preset")]
public sealed class LayoutSaveCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var items = ctx.SelectedOrPick("Selecione os móveis do layout", "Select the layout items", e => e is FamilyInstance { Location: LocationPoint, SuperComponent: null })
            .OfType<FamilyInstance>().ToList();
        var o = ctx.Ask<LayoutSaveOptions>();
        ctx.EnsureWorkPlane();
        var (b, angle) = LayoutPresets.PickFrame(ctx);
        var preset = new LayoutPreset { Name = o.Name, Description = o.Description };
        foreach (var fi in items)
        {
            var p = ((LocationPoint)fi.Location).Point;
            var local = (p.ToVec2() - b.ToVec2()).Rotate(-angle);
            var lvl = RevitGeo.LevelOf(fi);
            var elev = fi.get_Parameter(BuiltInParameter.INSTANCE_ELEVATION_PARAM)?.AsDouble() ?? (p.Z - (lvl?.Elevation ?? b.Z));
            preset.Items.Add(new LayoutPresetItem
            {
                FamilyName = fi.Symbol.FamilyName,
                TypeName = fi.Symbol.Name,
                Category = InteriorsUtil.CategoryName(fi),
                X = local.X,
                Y = local.Y,
                Z = elev,
                RotationDeg = MathUtil.ToDegrees(InteriorsUtil.Facing(fi).Angle - angle),
                Mirrored = fi.Mirrored,
            });
        }
        var json = Json.Serialize(preset, typeof(LayoutPreset), indented: false);
        var p0 = new Preset { Name = o.Name, Key = LayoutPresets.Key, Json = json, Description = o.Description };
        Services.Presets.Save(LayoutPresets.Key, p0);
        if (o.InProject)
        {
            ctx.Modify(ctx.Tool.Name, () =>
            {
                var pp = ProjectStorage.LoadPresets(ctx.Doc);
                pp.Save(LayoutPresets.Key, new Preset { Name = o.Name, Key = LayoutPresets.Key, Json = json, Description = o.Description, Scope = PresetScope.Project });
                ProjectStorage.SavePresets(ctx.Doc, pp);
            });
        }
        ctx.Report.ForceShow = true;
        ctx.Report.Success(Loc.F("Layout \"{0}\" salvo com {1} itens.", "Layout \"{0}\" saved with {1} items.", o.Name, items.Count));
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.presetapply", Modules.Interiors, "Aplicar layout de mobiliário", "Apply furniture layout", Order = 24, Group = "Mobiliário|Furniture",
    DescPt = "Insere um layout salvo em outro ponto/direção — ideal para repetir quartos, estações de trabalho e banheiros.",
    DescEn = "Places a saved layout at another point/direction — ideal for repeating bedrooms, workstations and bathrooms.",
    Keywords = "aplicar layout preset mobiliario repetir apply layout furniture preset repeat")]
public sealed class LayoutApplyCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var presets = LayoutPresets.All(doc);
        if (presets.Count == 0) ToolContext.Fail("Nenhum layout salvo. Use \"Salvar layout de mobiliário\" primeiro.", "No saved layout. Use \"Save furniture layout\" first.");
        var chosen = Pick.One(presets, p => p.Scope == PresetScope.Project ? p.Name + Loc.T(" (projeto)", " (project)") : p.Name, ctx.Tool.Name,
            Loc.T("Qual layout aplicar?", "Which layout to apply?"), ctx.Owner, p => p.Description ?? "");
        if (chosen == null) ctx.Cancel();
        var layout = Json.Deserialize<LayoutPreset>(chosen!.Json) ?? throw new ToolException(Loc.T("Layout inválido.", "Invalid layout."));
        ctx.EnsureWorkPlane();
        var (b, angle) = LayoutPresets.PickFrame(ctx);
        var level = RevitGeo.ViewLevel(ctx.ActiveView) ?? throw new ToolException(Loc.T("Nenhum nível.", "No level."));
        var symbols = Lookup.Collect<FamilySymbol>(doc).ToList();
        var missing = new SortedSet<string>();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var it in layout.Items)
            {
                var sym = symbols.FirstOrDefault(s => s.FamilyName == it.FamilyName && s.Name == it.TypeName)
                          ?? symbols.FirstOrDefault(s => s.FamilyName == it.FamilyName);
                if (sym == null)
                {
                    missing.Add(it.FamilyName + " : " + it.TypeName);
                    continue;
                }
                if (!InteriorsUtil.IsFreePlacement(sym))
                {
                    ctx.Report.Warn(Loc.F("\"{0}\" é hospedada e foi ignorada.", "\"{0}\" is hosted and was skipped.", it.FamilyName));
                    continue;
                }
                var p = b.ToVec2() + new Vec2(it.X, it.Y).Rotate(angle);
                var fi = InteriorsUtil.PlaceFree(doc, sym, level, p.ToXYZ(), it.Z);
                if (it.Mirrored && fi.Location is LocationPoint lp)
                {
                    var hand = fi.HandOrientation;
                    ElementTransformUtils.MirrorElements(doc, new List<ElementId> { fi.Id }, Plane.CreateByNormalAndOrigin(hand, lp.Point), false);
                }
                InteriorsUtil.TurnTo(doc, fi, Vec2.FromAngle(angle + MathUtil.ToRadians(it.RotationDeg)));
                ctx.Report.Created();
                ctx.Report.AddResult(fi.Id.Value);
            }
        });
        foreach (var m in missing)
            ctx.Report.Warn(Loc.F("Família não carregada no projeto: {0}", "Family not loaded in the project: {0}", m));
    }
}

// ============================================================ Áreas de uso

public sealed class ClearanceOptions
{
    [Opt("Profundidade padrão da área de uso", "Default use area depth", Kind = OptKind.Length, Min = 0.01)]
    public double Depth { get; set; } = U.Mm(900);

    [Opt("Regras: palavra=profundidade em cm (\"lados\" = também nas laterais)", "Rules: word=depth in cm (\"sides\" = also on the sides)", Kind = OptKind.MultiLine)]
    public List<string> Rules { get; set; } = new()
    {
        "cama=60 lados", "bed=60 sides", "guarda-roupa=90", "roupeiro=90", "wardrobe=90", "armário=80", "cabinet=80", "mesa=75", "table=75",
        "sofá=80", "sofa=80", "poltrona=60", "bancada=90", "counter=90", "pia=90", "fogão=100", "cooktop=90", "geladeira=100", "fridge=100",
        "vaso=60", "bacia=60", "toilet=60", "lavatório=70", "lavatorio=70", "chuveiro=70", "rack=60", "escrivaninha=75", "desk=75",
    };

    [Opt("Tipo de região preenchida", "Filled region type", Kind = OptKind.FilledRegionType)]
    public string RegionType { get; set; } = "";

    [Opt("Substituir marcações anteriores nesta vista", "Replace previous markers in this view")]
    public bool Replace { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.clearance", Modules.Interiors, "Áreas de uso do mobiliário", "Furniture use areas", Order = 25, Group = "Mobiliário|Furniture",
    DescPt = "Desenha na planta as áreas de uso em frente (e nas laterais) dos móveis e aponta conflitos com outros móveis e paredes.",
    DescEn = "Draws in plan the use areas in front of (and beside) furniture and reports conflicts with other items and walls.",
    HelpPt = "As profundidades vêm das regras editáveis (palavra no nome da família/tipo). As áreas são regiões preenchidas na vista ativa; rodar de novo substitui as anteriores.",
    HelpEn = "Depths come from editable rules (word in the family/type name). Areas are filled regions in the active view; running again replaces the previous ones.",
    Keywords = "area de uso circulacao frente mobiliario clearance use area furniture")]
public sealed class ClearanceCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var view = ctx.ActiveView;
        if (!RevitGeo.IsPlanLike(view)) ToolContext.Fail("Abra uma planta para desenhar as áreas de uso.", "Open a plan view to draw the use areas.");
        var items = ctx.Selected(InteriorsUtil.IsFurniture).OfType<FamilyInstance>().ToList();
        if (items.Count == 0)
            items = new FilteredElementCollector(ctx.Doc, view.Id).WherePasses(new ElementMulticategoryFilter(InteriorsUtil.FurnitureCats.ToList()))
                .WhereElementIsNotElementType().OfType<FamilyInstance>().Where(f => f.SuperComponent == null).ToList();
        if (items.Count == 0) ToolContext.Fail("Nenhum móvel na vista.", "No furniture in the view.");
        var o = ctx.Ask<ClearanceOptions>();
        var rules = o.Rules.Select(ParseRule).Where(r => r.Word.Length > 0).ToList();
        var doc = ctx.Doc;
        var fps = items.Select(InteriorsUtil.FootprintOf).Where(f => f.Outline.Count >= 3).ToList();
        var rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).ToList();
        var key = "furn.clearance|" + view.Id.Value;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.Replace) InteriorsUtil.ClearMarkers(doc, key);
            var typeId = InteriorsUtil.FilledRegionType(doc, o.RegionType);
            var created = new List<ElementId>();
            foreach (var fp in fps)
            {
                var norm = TextUtil.NormalizeForCompare(fp.Name);
                var rule = rules.FirstOrDefault(r => norm.Contains(r.Word));
                var depth = rule.Word != null && rule.Depth > 0 ? rule.Depth : o.Depth;
                var zones = new List<Polygon2>();
                var f = fp.Facing;
                var side = f.Perp;
                var (w0, w1) = InteriorsUtil.Extent(fp.Outline, side);
                var (d0, d1) = InteriorsUtil.Extent(fp.Outline, f);
                zones.Add(PolygonOps.FrontRectangle(fp.Outline, f, w1 - w0, depth));
                if (rule.Sides)
                {
                    zones.Add(PolygonOps.FrontRectangle(fp.Outline, side, d1 - d0, depth));
                    zones.Add(PolygonOps.FrontRectangle(fp.Outline, side * -1, d1 - d0, depth));
                }
                foreach (var z in zones)
                {
                    var fr = InteriorsUtil.Region(doc, view, typeId, z);
                    if (fr != null) created.Add(fr.Id);
                    var hits = fps.Where(other => other.Id != fp.Id && PolygonOps.Intersects(other.Outline, z, 1e-4)).ToList();
                    foreach (var h in hits)
                        ctx.Report.Warn(Loc.F("Área de uso de \"{0}\" invadida por \"{1}\".", "Use area of \"{0}\" overlapped by \"{1}\".", fp.Name, h.Name), fp.Id, h.Id);
                    var c = z.Centroid;
                    var room = RevitGeo.RoomAt(doc, new XYZ(c.X, c.Y, fp.BottomZ), rooms);
                    var reg = room == null ? null : RevitGeo.RoomRegion(room);
                    if (reg != null && reg.IntersectionArea(z) < Math.Abs(z.Area) * 0.97)
                        ctx.Report.Warn(Loc.F("Área de uso de \"{0}\" ultrapassa o ambiente (parede).", "Use area of \"{0}\" goes beyond the room (wall).", fp.Name), fp.Id);
                }
            }
            InteriorsUtil.SaveMarkers(doc, key, created);
            ctx.Report.Created(created.Count);
        });
        if (!ctx.Report.HasWarnings) ctx.Report.Success(Loc.T("Nenhum conflito de área de uso encontrado.", "No use area conflict found."));
        ctx.Report.ForceShow = true;
    }

    private static (string Word, double Depth, bool Sides) ParseRule(string line)
    {
        var parts = line.Split('=', 2);
        if (parts.Length != 2) return ("", 0, false);
        var word = TextUtil.NormalizeForCompare(parts[0].Trim());
        var rest = parts[1].Trim();
        var num = new string(rest.TakeWhile(ch => char.IsDigit(ch) || ch == ',' || ch == '.').ToArray());
        U.TryParseNumber(num, out var cm);
        var sides = rest.Contains("lado", StringComparison.OrdinalIgnoreCase) || rest.Contains("side", StringComparison.OrdinalIgnoreCase);
        return (word, U.Cm(cm), sides);
    }
}

// ============================================================ Lista de mobiliário

public sealed class FurnitureListOptions
{
    [Opt("Gravar o ambiente em cada móvel (TT_Ambiente)", "Write the room on each item (TT_Ambiente)")]
    public bool WriteRoom { get; set; } = true;

    [Opt("Criar tabela no Revit", "Create Revit schedule")]
    public bool CreateSchedule { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("furn.list", Modules.Interiors, "Lista de mobiliário por ambiente", "Furniture list by room", Order = 26, Group = "Mobiliário|Furniture",
    DescPt = "Lista móveis e equipamentos por ambiente (família, tipo, quantidade), grava o ambiente em cada peça e cria a tabela.",
    DescEn = "Lists furniture and equipment by room (family, type, count), writes the room on each item and creates the schedule.",
    Keywords = "lista mobiliario ambiente quantidade tabela furniture list room schedule")]
public sealed class FurnitureListCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<FurnitureListOptions>();
        var doc = ctx.Doc;
        var rooms = RevitGeo.Rooms(doc).Where(r => r.Area > 0).OrderBy(r => r.Number, TextUtil.NaturalComparer).ToList();
        var assigned = new Dictionary<long, Room>();
        foreach (var r in rooms)
            foreach (var fi in InteriorsUtil.InstancesInRoom(doc, r, InteriorsUtil.FurnitureCats))
                assigned.TryAdd(fi.Id.Value, r);
        var all = new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(InteriorsUtil.FurnitureCats.ToList()))
            .WhereElementIsNotElementType().OfType<FamilyInstance>().Where(f => f.SuperComponent == null).ToList();
        var noRoom = Loc.T("(sem ambiente)", "(no room)");
        var t = new TableData(Loc.T("Mobiliário por ambiente", "Furniture by room"));
        t.AddColumn(Loc.T("Ambiente", "Room")).AddColumn(Loc.T("Categoria", "Category")).AddColumn(Loc.T("Família", "Family"))
         .AddColumn(Loc.T("Tipo", "Type")).AddColumn(Loc.T("Quantidade", "Count"), ColumnKind.Integer);
        var groups = all.GroupBy(f => (Room: assigned.TryGetValue(f.Id.Value, out var r) ? RoomUtil.Label(r) : noRoom, Cat: f.Category?.Name ?? "", Fam: f.Symbol.FamilyName, Type: f.Symbol.Name))
            .OrderBy(g => g.Key.Room == noRoom).ThenBy(g => g.Key.Room, TextUtil.NaturalComparer).ThenBy(g => g.Key.Cat).ThenBy(g => g.Key.Fam).ThenBy(g => g.Key.Type);
        foreach (var g in groups)
            t.AddRowWithIds(g.Select(f => f.Id.Value), g.Key.Room, g.Key.Cat, g.Key.Fam, g.Key.Type, g.Count());
        t.AddTotalsRow(Loc.T("Total", "Total"), 4);
        ctx.Report.Tables.Add(t);
        if (o.WriteRoom || o.CreateSchedule)
        {
            ctx.Modify(ctx.Tool.Name, () =>
            {
                SharedParams.BindToProject(doc, new[] { SharedParams.Ambiente }, InteriorsUtil.FurnitureCats);
                if (o.WriteRoom)
                {
                    foreach (var f in all)
                        if (RevitGeo.Set(f, assigned.TryGetValue(f.Id.Value, out var r) ? RoomUtil.Label(r) : "", SharedParams.Ambiente))
                            ctx.Report.Modified();
                }
                if (o.CreateSchedule)
                {
                    var vs = Schedules.Create(doc, BuiltInCategory.OST_Furniture, Loc.T("TT Mobiliário por ambiente", "TT Furniture by room"),
                        new[] { new Schedules.Spec(SharedParams.Ambiente), new Schedules.Spec("BIP:ELEM_FAMILY_AND_TYPE_PARAM"), new Schedules.Spec("#count", Loc.T("Quantidade", "Count")) },
                        null, new[] { SharedParams.Ambiente, "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report);
                    ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
                    ctx.Report.AddResult(vs.Id.Value);
                }
            });
        }
        ctx.Report.ForceShow = true;
    }
}
