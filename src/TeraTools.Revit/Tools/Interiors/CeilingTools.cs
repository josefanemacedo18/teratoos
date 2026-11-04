using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

internal static class CeilingUtil
{
    public static ElementId TypeId(Document doc, string? name) =>
        (Lookup.Named<CeilingType>(doc, name) ?? Lookup.Collect<CeilingType>(doc).FirstOrDefault())?.Id
        ?? throw new ToolException(Loc.T("Nenhum tipo de forro no projeto.", "No ceiling type in the project."));

    public static Ceiling Create(Document doc, Region2 region, ElementId typeId, Level level, double height)
    {
        var loops = RevitGeo.RegionToLoops(region, level.Elevation);
        var c = Ceiling.Create(doc, loops, typeId, level.Id);
        RevitGeo.Set(c, BuiltInParameter.CEILING_HEIGHTABOVELEVEL_PARAM, height);
        return c;
    }

    /// <summary>Forros existentes cujo centro está dentro do ambiente.</summary>
    public static List<Ceiling> InRoom(Document doc, Room room)
    {
        var lz = room.Level?.Elevation ?? 0;
        return new FilteredElementCollector(doc).OfClass(typeof(Ceiling)).Cast<Ceiling>()
            .Where(c => c.LevelId == room.LevelId && RevitGeo.BoxCenter(c) is { } p && room.IsPointInRoom(new XYZ(p.X, p.Y, lz + 1.0)))
            .ToList();
    }

    /// <summary>Tipo de parede básica mais fina (para fechamentos de sanca).</summary>
    public static WallType ThinWallType(Document doc, string? name) =>
        Lookup.Named<WallType>(doc, name)
        ?? Lookup.Collect<WallType>(doc).Where(w => w.Kind == WallKind.Basic).OrderBy(w => w.Width).FirstOrDefault()
        ?? throw new ToolException(Loc.T("Nenhum tipo de parede básica no projeto.", "No basic wall type in the project."));
}

public sealed class CeilingByRoomOptions
{
    [Opt("Tipo de forro", "Ceiling type", Kind = OptKind.CeilingType, Required = true)]
    public string Type { get; set; } = "";

    [Opt("Altura do forro (a partir do nível)", "Ceiling height (from level)", Kind = OptKind.Length, Min = 0)]
    public double Height { get; set; } = U.M(2.6);

    [Opt("Afastamento das paredes (tabica)", "Gap from walls (shadow gap)", Kind = OptKind.Length, Min = 0)]
    public double Gap { get; set; }

    [Opt("Substituir forros existentes no ambiente", "Replace existing ceilings in the room")]
    public bool Replace { get; set; }

    [Opt("Preencher \"Acabamento do forro\" do ambiente", "Fill the room \"Ceiling finish\"")]
    public bool FillFinish { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("ceil.byroom", Modules.Interiors, "Forro por ambiente", "Ceiling by room", Order = 50, Group = "Forros|Ceilings",
    DescPt = "Cria o forro de cada ambiente pelo contorno de acabamento, na altura escolhida e, se quiser, com tabica (afastamento) nas paredes.",
    DescEn = "Creates each room's ceiling from its finish boundary, at the chosen height, optionally with a shadow gap at the walls.",
    Keywords = "forro ambiente criar teto tabica ceiling room create")]
public sealed class CeilingByRoomCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var o = ctx.Ask<CeilingByRoomOptions>();
        var doc = ctx.Doc;
        var typeId = CeilingUtil.TypeId(doc, o.Type);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            foreach (var room in rooms)
            {
                var level = room.Level;
                if (level == null) continue;
                if (o.Replace)
                {
                    var old = CeilingUtil.InRoom(doc, room);
                    if (old.Count > 0)
                    {
                        doc.Delete(old.Select(c => c.Id).ToList());
                        ctx.Report.Deleted(old.Count);
                    }
                }
                foreach (var reg in RevitGeo.RoomRegions(room))
                {
                    var r = o.Gap > 0 ? reg.Offset(-o.Gap) : reg;
                    if (r.Outer.IsEmpty || r.Area < 1e-3) continue;
                    try
                    {
                        var c = CeilingUtil.Create(doc, r, typeId, level, o.Height);
                        RevitGeo.Set(c, RoomUtil.Label(room), SharedParams.Ambiente);
                        ctx.Report.Created();
                        ctx.Report.AddResult(c.Id.Value);
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.Warn(RoomUtil.Label(room) + ": " + Shapes.FailText(ex), room.Id.Value);
                    }
                }
                if (o.FillFinish && doc.GetElement(typeId) is CeilingType ct) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_CEILING, ct.Name);
            }
        });
    }
}

public sealed class CoveOptions : IValidatableOptions
{
    [Opt("Tipo do forro central", "Central ceiling type", Kind = OptKind.CeilingType, Required = true, Group = "Forro central|Central ceiling")]
    public string CenterType { get; set; } = "";

    [Opt("Altura do forro central", "Central ceiling height", Kind = OptKind.Length, Min = 0, Group = "Forro central|Central ceiling")]
    public double CenterHeight { get; set; } = U.M(2.70);

    [Opt("Tipo do rebaixo perimetral (vazio = mesmo)", "Perimeter drop type (empty = same)", Kind = OptKind.CeilingType, Group = "Rebaixo perimetral|Perimeter drop")]
    public string BandType { get; set; } = "";

    [Opt("Altura do rebaixo", "Drop height", Kind = OptKind.Length, Min = 0, Group = "Rebaixo perimetral|Perimeter drop")]
    public double BandHeight { get; set; } = U.M(2.50);

    [Opt("Largura do rebaixo", "Drop width", Kind = OptKind.Length, Min = 0.01, Group = "Rebaixo perimetral|Perimeter drop")]
    public double BandWidth { get; set; } = U.Mm(400);

    [Opt("Afastamento das paredes (tabica)", "Gap from walls", Kind = OptKind.Length, Min = 0, Group = "Rebaixo perimetral|Perimeter drop")]
    public double Gap { get; set; }

    [Opt("Criar fechamento vertical entre os níveis", "Create vertical closure between levels", Group = "Sanca|Cove")]
    public bool Closure { get; set; } = true;

    [Opt("Tipo de parede do fechamento", "Closure wall type", Kind = OptKind.WallType, Group = "Sanca|Cove", VisibleIf = "Closure",
        Tip = "Vazio = a parede básica mais fina do projeto.|Empty = the thinnest basic wall in the project.")]
    public string ClosureType { get; set; } = "";

    [Opt("Fenda de luz (altura livre acima do fechamento)", "Light slot (free height above the closure)", Kind = OptKind.Length, Min = 0, Group = "Sanca|Cove", VisibleIf = "Closure",
        Tip = "0 = sanca fechada. Ex.: 100 mm = sanca aberta com fenda para fita de LED.|0 = closed cove. E.g. 100 mm = open cove with an LED slot.")]
    public double Slot { get; set; }

    [Opt("Preencher \"Acabamento do forro\" do ambiente", "Fill the room \"Ceiling finish\"", Group = "Dados|Data")]
    public bool FillFinish { get; set; } = true;

    public IEnumerable<string> Validate()
    {
        if (Math.Abs(CenterHeight - BandHeight) < 1e-6 && Closure)
            yield return Loc.T("As alturas do forro central e do rebaixo são iguais: não há fechamento a criar.", "Central and drop heights are equal: there is no closure to create.");
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("ceil.cove", Modules.Interiors, "Sanca e rebaixo de forro", "Cove and ceiling drop", Order = 51, Group = "Forros|Ceilings",
    DescPt = "Cria rebaixo perimetral (faixa) e forro central em alturas diferentes, com fechamento vertical (sanca fechada ou aberta com fenda de luz) e tabica.",
    DescEn = "Creates a perimeter drop (band) and a central ceiling at different heights, with vertical closure (closed cove or open with light slot) and shadow gap.",
    HelpPt = "O fechamento vertical é feito com paredes finas (sem delimitar ambientes), contornando o forro central. Tudo fica num único Desfazer e editável como elementos nativos do Revit.",
    HelpEn = "The vertical closure is made of thin walls (not room bounding) around the central ceiling. Everything is a single Undo and editable as native Revit elements.",
    Keywords = "sanca rebaixo forro tabica fenda led cove drop ceiling soffit")]
public sealed class CoveCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var regions = rooms.Select(r => (Room: r, Region: RevitGeo.RoomRegion(r))).Where(x => x.Region != null).Select(x => (x.Room, Region: x.Region!)).ToList();
        var o = ctx.Ask<CoveOptions>(preview: x => Preview(x, regions));
        var doc = ctx.Doc;
        var centerType = CeilingUtil.TypeId(doc, o.CenterType);
        var bandType = string.IsNullOrWhiteSpace(o.BandType) ? centerType : CeilingUtil.TypeId(doc, o.BandType);
        var wt = o.Closure ? CeilingUtil.ThinWallType(doc, o.ClosureType) : null;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            GenSystem.BindParams(doc, ctx.Report);
            foreach (var (room, reg) in regions)
            {
                var level = room.Level;
                if (level == null) continue;
                var label = RoomUtil.Label(room);
                var (outer, inner, band) = Split(o, reg);
                if (inner == null || band == null)
                {
                    ctx.Report.Warn(Loc.F("{0}: o rebaixo é largo demais para o ambiente.", "{0}: the drop is too wide for the room.", label), room.Id.Value);
                    continue;
                }
                try
                {
                    var bc = CeilingUtil.Create(doc, band, bandType, level, o.BandHeight);
                    var cc = CeilingUtil.Create(doc, inner, centerType, level, o.CenterHeight);
                    foreach (var c in new[] { bc, cc })
                    {
                        RevitGeo.Set(c, label, SharedParams.Ambiente);
                        ctx.Report.Created();
                        ctx.Report.AddResult(c.Id.Value);
                    }
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(label + ": " + Shapes.FailText(ex), room.Id.Value);
                    continue;
                }
                if (wt != null)
                {
                    var bottom = Math.Min(o.CenterHeight, o.BandHeight);
                    var h = Math.Abs(o.CenterHeight - o.BandHeight) - o.Slot;
                    if (h > U.Mm(5))
                    {
                        // linha central da parede: contorno do forro central deslocado meia espessura para o lado do rebaixo
                        var axis = inner.Outer.Offset(wt.Width / 2);
                        foreach (var e in axis.Edges())
                        {
                            if (e.Length < Tolerance.ShortCurve) continue;
                            try
                            {
                                var line = Line.CreateBound(e.A.ToXYZ(level.Elevation), e.B.ToXYZ(level.Elevation));
                                var w = Wall.Create(doc, line, wt.Id, level.Id, h, bottom, false, false);
                                RevitGeo.Set(w, BuiltInParameter.WALL_ATTR_ROOM_BOUNDING, 0);
                                RevitGeo.Set(w, label, SharedParams.Ambiente);
                                ctx.Report.Count(Loc.T("Fechamentos", "Closures"));
                                ctx.Report.AddResult(w.Id.Value);
                            }
                            catch (Exception ex)
                            {
                                Log.Debug("Fechamento: " + ex.Message);
                            }
                        }
                    }
                }
                if (o.FillFinish && doc.GetElement(centerType) is CeilingType ct) RevitGeo.Set(room, BuiltInParameter.ROOM_FINISH_CEILING, ct.Name);
            }
        });
    }

    private static (Region2 Outer, Region2? Inner, Region2? Band) Split(CoveOptions o, Region2 reg)
    {
        var outer = o.Gap > 0 ? reg.Offset(-o.Gap) : reg;
        var inner = outer.Offset(-o.BandWidth);
        if (inner.Outer.IsEmpty || inner.Area < U.M2ToSqFt(0.05)) return (outer, null, null);
        var band = new Region2(outer.Outer, new[] { inner.Outer });
        return (outer, inner, band);
    }

    private static PreviewModel Preview(CoveOptions o, List<(Room Room, Region2 Region)> regions)
    {
        var pm = new PreviewModel();
        foreach (var (_, reg) in regions)
        {
            var (outer, inner, _) = Split(o, reg);
            pm.Outline(reg.Outer, "#5F6B7A", 1.5);
            pm.Polygon(outer.Outer, "#E9D5C1", "#6B4A33", 1);
            if (inner != null) pm.Polygon(inner.Outer, "#FFFFFF", "#B5652B", 1.2);
        }
        pm.Info = Loc.F("Rebaixo {0} · central {1} · faixa {2}", "Drop {0} · center {1} · band {2}", U.FormatM(o.BandHeight), U.FormatM(o.CenterHeight), U.FormatCm(o.BandWidth));
        return pm;
    }
}

public sealed class RcpOptions
{
    [Opt("Modelo de vista", "View template", Kind = OptKind.ViewTemplate, Filter = "CeilingPlan")]
    public string Template { get; set; } = "";

    [Opt("Modelo do nome", "Name template", Tip = "Marcador: {Nivel}.|Token: {Nivel}.")]
    public string NameTemplate { get; set; } = "Forro - {Nivel}";

    [Opt("Escala (1:n)", "Scale (1:n)", Min = 1, Max = 5000)]
    public int Scale { get; set; } = 50;

    [Opt("Etiquetar forros (altura)", "Tag ceilings (height)", Tip = "Requer uma etiqueta de forro carregada no projeto.|Requires a ceiling tag loaded in the project.")]
    public bool Tag { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("ceil.rcp", Modules.Interiors, "Planta de forro por nível", "Ceiling plan per level", Order = 52, Group = "Forros|Ceilings",
    DescPt = "Cria plantas de forro (RCP) para os níveis escolhidos, com modelo de vista, escala e etiquetas de altura dos forros.",
    DescEn = "Creates reflected ceiling plans for the chosen levels, with view template, scale and ceiling height tags.",
    Keywords = "planta de forro rcp refletida teto reflected ceiling plan")]
public sealed class RcpCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var levels = Pick.Many(Lookup.Collect<Level>(doc).OrderBy(l => l.Elevation), l => l.Name, ctx.Tool.Name, Loc.T("Para quais níveis?", "For which levels?"), ctx.Owner);
        if (levels.Count == 0) ctx.Cancel();
        var o = ctx.Ask<RcpOptions>();
        var vft = Lookup.ViewFamilyType(doc, ViewFamily.CeilingPlan) ?? throw new ToolException(Loc.T("Tipo de vista de forro não encontrado.", "Ceiling plan view type not found."));
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var tagFailed = false;
            foreach (var level in levels)
            {
                var v = ViewPlan.Create(doc, vft.Id, level.Id);
                v.Name = Lookup.UniqueViewName(doc, Core.Text.NameTemplate.Apply(o.NameTemplate, new Dictionary<string, object?> { ["Nivel"] = level.Name }));
                if (Lookup.View(doc, o.Template) is { IsTemplate: true } t) v.ViewTemplateId = t.Id;
                else v.Scale = o.Scale;
                ctx.Report.Created();
                ctx.Report.AddResult(v.Id.Value);
                if (!o.Tag || tagFailed) continue;
                foreach (var c in new FilteredElementCollector(doc).OfClass(typeof(Ceiling)).Cast<Ceiling>().Where(c => c.LevelId == level.Id).ToList())
                {
                    var reg = RevitGeo.SketchRegion(doc, c.SketchId);
                    var p = reg != null ? reg.Outer.Centroid : RevitGeo.BoxCenter(c)?.ToVec2();
                    if (p == null) continue;
                    try
                    {
                        IndependentTag.Create(doc, v.Id, new Reference(c), false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, p.Value.ToXYZ(level.Elevation));
                        ctx.Report.Count(Loc.T("Etiquetas", "Tags"));
                    }
                    catch (Exception ex)
                    {
                        tagFailed = true;
                        ctx.Report.Info(Loc.T("Etiquetas de forro não criadas (carregue uma família de etiqueta de forro): ", "Ceiling tags not created (load a ceiling tag family): ") + ex.Message);
                        break;
                    }
                }
            }
        });
    }
}

public enum OpeningShape
{
    [Text("Retângulo do objeto", "Object rectangle")] Rect,
    [Text("Círculo", "Circle")] Circle,
    [Text("Contorno do objeto (envoltória)", "Object outline (hull)")] Outline,
}

public sealed class CeilingOpeningOptions
{
    [Opt("Formato", "Shape")]
    public OpeningShape Shape { get; set; } = OpeningShape.Rect;

    [Opt("Folga ao redor", "Clearance around", Kind = OptKind.Length, Min = 0)]
    public double Margin { get; set; } = U.Mm(10);

    [Opt("Diâmetro fixo (0 = do objeto)", "Fixed diameter (0 = from object)", Kind = OptKind.Length, Min = 0, VisibleIf = "Shape=Circle")]
    public double Diameter { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("ceil.opening", Modules.Interiors, "Aberturas no forro", "Ceiling openings", Order = 53, Group = "Forros|Ceilings",
    DescPt = "Abre furos no forro sob luminárias, difusores e outros objetos selecionados — retangulares, circulares ou pelo contorno, com folga.",
    DescEn = "Cuts holes in the ceiling under selected fixtures, diffusers and other objects — rectangular, circular or by outline, with clearance.",
    Keywords = "abertura furo forro recorte luminaria difusor ceiling opening hole cutout")]
public sealed class CeilingOpeningCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var items = ctx.SelectedOrPick("Selecione os objetos (luminárias, difusores...)", "Select the objects (fixtures, diffusers...)", e => e is FamilyInstance).ToList();
        var o = ctx.Ask<CeilingOpeningOptions>();
        var finder = new CeilingFinder(doc);
        if (finder.Count == 0) ToolContext.Fail("Não há forros no projeto.", "There are no ceilings in the project.");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var it in items)
            {
                var fp = InteriorsUtil.FootprintOf(it);
                if (fp.Outline.Count < 3) continue;
                var c = fp.Outline.Centroid;
                var hit = finder.Above(new XYZ(c.X, c.Y, fp.BottomZ - 1), fp.BottomZ - U.M(1), fp.TopZ + U.M(1));
                if (hit == null)
                {
                    ctx.Report.Warn(Loc.F("Nenhum forro encontrado em \"{0}\".", "No ceiling found at \"{0}\".", fp.Name), it.Id.Value);
                    continue;
                }
                var (ceiling, _, z) = hit.Value;
                var profile = new CurveArray();
                switch (o.Shape)
                {
                    case OpeningShape.Circle:
                    {
                        var (a0, a1) = InteriorsUtil.Extent(fp.Outline, fp.Facing.Perp);
                        var (b0, b1) = InteriorsUtil.Extent(fp.Outline, fp.Facing);
                        var r = (o.Diameter > 0 ? o.Diameter / 2 : Math.Max(a1 - a0, b1 - b0) / 2) + o.Margin;
                        var center = (fp.Facing.Perp * ((a0 + a1) / 2) + fp.Facing * ((b0 + b1) / 2)).ToXYZ(z);
                        profile.Append(Arc.Create(center, r, 0, Math.PI, XYZ.BasisX, XYZ.BasisY));
                        profile.Append(Arc.Create(center, r, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY));
                        break;
                    }
                    default:
                    {
                        Polygon2 poly;
                        if (o.Shape == OpeningShape.Rect)
                        {
                            var u = fp.Facing.Perp;
                            var v = fp.Facing;
                            var (a0, a1) = InteriorsUtil.Extent(fp.Outline, u);
                            var (b0, b1) = InteriorsUtil.Extent(fp.Outline, v);
                            poly = new Polygon2(new[] { u * a0 + v * b0, u * a1 + v * b0, u * a1 + v * b1, u * a0 + v * b1 }).EnsureCounterClockwise();
                        }
                        else
                        {
                            poly = fp.Outline.EnsureCounterClockwise();
                        }
                        if (o.Margin > 0) poly = poly.Offset(o.Margin);
                        foreach (var e in poly.Edges())
                            if (e.Length > Tolerance.ShortCurve) profile.Append(Line.CreateBound(e.A.ToXYZ(z), e.B.ToXYZ(z)));
                        break;
                    }
                }
                try
                {
                    var op = doc.Create.NewOpening(ceiling, profile, true);
                    ctx.Report.Created();
                    ctx.Report.AddResult(op.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(fp.Name + ": " + Shapes.FailText(ex), it.Id.Value);
                }
            }
        });
    }
}
