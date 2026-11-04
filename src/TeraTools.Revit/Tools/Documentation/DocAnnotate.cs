using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Colors;
using TeraTools.Core.Geometry;
using TeraTools.Core.Layout;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Generators;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Tools.Materials;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Documentation;

public sealed class DimOptions
{
    [Opt("Tipo de cota (vazio = padrão)", "Dimension type (empty = default)", Kind = OptKind.DimensionType)]
    public string DimensionType { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.dims", Modules.Documentation, "Cotas internas dos ambientes", "Room interior dimensions", Order = 40, Group = "Anotação|Annotation",
    DescPt = "Cota automaticamente a largura e o comprimento internos de cada ambiente, de face a face de parede, alinhados às paredes — em todos os ambientes da vista de uma vez.",
    DescEn = "Automatically dimensions room interior width and length, or walls with their openings (doors and windows), in the active plan.",
    HelpPt = "As cotas são associativas (referenciam as faces de acabamento das paredes): se a parede mudar, a cota acompanha. Para cotar paredes com portas e janelas use a Cota alinhada nativa do Revit (opção Paredes inteiras com aberturas). Ambientes com paredes curvas na direção da cota são ignorados.",
    HelpEn = "Dimensions are associative (they reference wall faces and opening references): they follow wall changes. Curved walls are skipped.",
    Keywords = "cotas automaticas cotar ambiente parede vaos dimensions automatic room wall openings")]
public sealed class DimsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var view = ctx.ActiveView;
        if (!RevitGeo.IsPlanLike(view)) ToolContext.Fail("Abra uma planta para cotar.", "Open a plan view to dimension.");
        var o = ctx.Ask<DimOptions>();
        var doc = ctx.Doc;
        var dimType = string.IsNullOrWhiteSpace(o.DimensionType) ? null : Lookup.Named<DimensionType>(doc, o.DimensionType);
        var z = RevitGeo.ViewLevel(view)?.Elevation ?? 0;
        var rooms = RoomUtil.SelectedOrInView(ctx).Where(r => r.Area > 0).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms) DimensionRoom(ctx, room, view, z, dimType);
        });
    }

    private static Dimension? Create(Document doc, View view, Line line, ReferenceArray refs, DimensionType? type) =>
        type != null ? doc.Create.NewDimension(view, line, refs, type) : doc.Create.NewDimension(view, line, refs);

    private static void DimensionRoom(ToolContext ctx, Room room, View view, double z, DimensionType? type)
    {
        var doc = ctx.Doc;
        var reg = RevitGeo.RoomRegion(room);
        if (reg == null) return;
        var c = room.Location is LocationPoint lp && reg.Contains(lp.Point.ToVec2()) ? lp.Point.ToVec2() : reg.Outer.Centroid;
        var opts = new SpatialElementBoundaryOptions { SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish };
        var segs = new List<(Segment2 Seg, Wall Wall)>();
        foreach (var s in room.GetBoundarySegments(opts).SelectMany(l => l))
            if (doc.GetElement(s.ElementId) is Wall w && s.GetCurve() is Line l)
                segs.Add((new Segment2(l.GetEndPoint(0).ToVec2(), l.GetEndPoint(1).ToVec2()), w));
        var a = GridLayout.DominantDirection(reg.Outer);
        foreach (var d in new[] { Vec2.FromAngle(a), Vec2.FromAngle(a + Math.PI / 2) })
        {
            (double T, Wall Wall, Vec2 P)? pos = null, neg = null;
            foreach (var (seg, wall) in segs)
            {
                var sd = seg.Vector.Normalized();
                if (Math.Abs(sd.Dot(d)) > 0.05) continue;
                var n = sd.Perp;
                var den = d.Dot(n);
                if (Math.Abs(den) < 1e-9) continue;
                var t = (seg.A - c).Dot(n) / den;
                var q = c + d * t;
                var u = seg.ProjectParameter(q);
                if (u < -0.01 || u > 1.01) continue;
                if (t > 1e-6 && (pos == null || t < pos.Value.T)) pos = (t, wall, q);
                if (t < -1e-6 && (neg == null || t > neg.Value.T)) neg = (t, wall, q);
            }
            if (pos == null || neg == null) continue;
            var r1 = FaceAt(pos.Value.Wall, pos.Value.P, d);
            var r2 = FaceAt(neg.Value.Wall, neg.Value.P, d);
            if (r1 == null || r2 == null) continue;
            var refs = new ReferenceArray();
            refs.Append(r2);
            refs.Append(r1);
            try
            {
                var line = Line.CreateBound(neg.Value.P.ToXYZ(z), pos.Value.P.ToXYZ(z));
                var dim = Create(doc, view, line, refs, type);
                if (dim != null)
                {
                    ctx.Report.Created();
                    ctx.Report.AddResult(dim.Id.Value);
                }
            }
            catch (Exception ex)
            {
                ctx.Report.Warn(RoomUtil.Label(room) + ": " + Shapes.FailText(ex), room.Id.Value);
            }
        }
    }

    /// <summary>Referência da face lateral da parede que passa pelo ponto (normal paralela à direção).</summary>
    private static Reference? FaceAt(Wall wall, Vec2 p, Vec2 dir)
    {
        var refs = new List<Reference>();
        try
        {
            refs.AddRange(HostObjectUtils.GetSideFaces(wall, ShellLayerType.Interior));
            refs.AddRange(HostObjectUtils.GetSideFaces(wall, ShellLayerType.Exterior));
        }
        catch
        {
            return null;
        }
        Reference? best = null;
        var bestD = double.MaxValue;
        foreach (var r in refs)
        {
            if (wall.GetGeometryObjectFromReference(r) is not PlanarFace f) continue;
            var n = f.FaceNormal.ToVec2();
            if (Math.Abs(Math.Abs(n.Normalized().Dot(dir)) - 1) > 0.01) continue;
            var dist = Math.Abs((p - f.Origin.ToVec2()).Dot(n.Normalized()));
            if (dist < bestD)
            {
                bestD = dist;
                best = r;
            }
        }
        return bestD < 0.1 ? best : null;
    }

}

public enum LegendSource
{
    [Text("Acabamentos dos ambientes (piso, parede, forro)", "Room finishes (floor, wall, ceiling)")] RoomFinishes,
    [Text("Materiais escolhidos do projeto", "Chosen project materials")] Materials,
    [Text("Filtros com cor da vista ativa", "Colored filters of the active view")] ViewFilters,
}

public sealed class LegendOptions
{
    [Opt("Conteúdo", "Content")]
    public LegendSource Source { get; set; } = LegendSource.RoomFinishes;

    [Opt("Título", "Title")]
    public string Title { get; set; } = "Legenda";

    [Opt("Tipo de texto", "Text type", Kind = OptKind.TextNoteType)]
    public string TextType { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.legend", Modules.Documentation, "Legenda de acabamentos e cores", "Finish and color legend", Order = 42, Group = "Anotação|Annotation",
    DescPt = "Cria uma legenda com amostras de cor e textos: acabamentos dos ambientes, materiais escolhidos ou os filtros coloridos da vista ativa.",
    DescEn = "Creates a legend with color swatches and labels: room finishes, chosen materials or the colored filters of the active view.",
    Keywords = "legenda acabamentos cores materiais filtros legend finishes colors materials filters")]
public sealed class LegendCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<LegendOptions>();
        var doc = ctx.Doc;
        var theme = new ColorTheme { Name = o.Title };
        var palette = PaletteGenerator.Distinct(24, 0.45, 0.6);
        var k = 0;
        void Add(string key, RgbColor? color)
        {
            if (string.IsNullOrWhiteSpace(key) || theme.Entries.Any(e => e.Key == key)) return;
            var c = color ?? palette[k++ % palette.Count];
            theme.Entries.Add(new ThemeEntry { Key = key, ColorHex = c.Hex });
        }
        switch (o.Source)
        {
            case LegendSource.RoomFinishes:
                foreach (var r in RevitGeo.Rooms(doc).Where(r => r.Area > 0))
                    foreach (var bip in new[] { BuiltInParameter.ROOM_FINISH_FLOOR, BuiltInParameter.ROOM_FINISH_WALL, BuiltInParameter.ROOM_FINISH_CEILING })
                    {
                        var v = RevitGeo.Text(r, bip);
                        Add(v, Lookup.Material(doc, v) is { } m ? MaterialsService.ColorOf(m) : null);
                    }
                break;
            case LegendSource.Materials:
                foreach (var m in Pick.Many(Lookup.Collect<Material>(doc).OrderBy(m => m.Name), m => m.Name, ctx.Tool.Name, Loc.T("Quais materiais?", "Which materials?"), ctx.Owner))
                    Add(m.Name, MaterialsService.ColorOf(m));
                break;
            default:
                var view = ctx.ActiveView;
                foreach (var fid in view.GetFilters())
                {
                    var ogs = view.GetFilterOverrides(fid);
                    var col = ogs.SurfaceForegroundPatternColor.IsValid ? ogs.SurfaceForegroundPatternColor
                        : ogs.CutForegroundPatternColor.IsValid ? ogs.CutForegroundPatternColor : ogs.ProjectionLineColor;
                    if (!col.IsValid) continue;
                    var name = doc.GetElement(fid)?.Name ?? "";
                    Add(name.StartsWith("TT ") ? name[3..] : name, new RgbColor(col.Red, col.Green, col.Blue));
                }
                break;
        }
        if (theme.Entries.Count == 0) ToolContext.Fail("Nada para a legenda.", "Nothing for the legend.");
        theme.Entries.Sort((a, b) => TextUtil.NaturalComparer.Compare(a.Key, b.Key));
        ctx.Modify(ctx.Tool.Name, () => Legend.Create(ctx, theme, o.TextType));
    }
}

public sealed class NoteItem
{
    public string Group { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class NoteLibrary
{
    public List<NoteItem> Notes { get; set; } = new();

    public static string FilePath => Path.Combine(Paths.Libraries, "notas.json");

    public static NoteLibrary Load()
    {
        if (File.Exists(FilePath))
        {
            try { return Json.LoadOrDefault<NoteLibrary>(FilePath); } catch (Exception ex) { Log.Warn("Notas: " + ex.Message); }
        }
        return Starter();
    }

    public void Save() => Json.Save(FilePath, this);

    public static NoteLibrary Starter() => new()
    {
        Notes =
        {
            new() { Group = "Gerais", Title = "Conferência de medidas", Text = "Todas as medidas devem ser conferidas no local antes da execução." },
            new() { Group = "Gerais", Title = "Cotas prevalecem", Text = "Não medir o desenho em escala; prevalecem as cotas indicadas." },
            new() { Group = "Gerais", Title = "Unidades", Text = "Cotas em centímetros e níveis em metros, exceto quando indicado." },
            new() { Group = "Gerais", Title = "Compatibilização", Text = "Compatibilizar com os projetos complementares (elétrico, hidráulico, ar-condicionado, estrutura) antes da execução." },
            new() { Group = "Gerais", Title = "Divergências", Text = "Qualquer divergência entre projeto e obra deve ser comunicada ao responsável técnico antes da execução." },
            new() { Group = "Acabamentos", Title = "Memorial", Text = "Materiais e acabamentos conforme ficha técnica / memorial descritivo do projeto." },
            new() { Group = "Acabamentos", Title = "Paginação", Text = "Iniciar a paginação pelo ponto indicado em planta. Juntas conforme especificação do fabricante." },
            new() { Group = "Elétrica", Title = "Alturas de pontos", Text = "Alturas dos pontos elétricos indicadas a partir do piso acabado, até o eixo da caixa." },
            new() { Group = "Marcenaria", Title = "Medidas finais", Text = "As medidas finais da marcenaria devem ser conferidas pelo fornecedor após a conclusão dos revestimentos." },
            new() { Group = "Marcenaria", Title = "Ferragens", Text = "Ferragens, puxadores e acessórios conforme especificação; amortecedores em portas e gavetas." },
            new() { Group = "Acessibilidade", Title = "Validação", Text = "Verificações de acessibilidade com base em parâmetros de referência; a conformidade deve ser validada pelo responsável técnico conforme a norma vigente." },
        },
    };
}

public enum NotesAction
{
    [Text("Inserir notas na vista", "Insert notes in the view")] Insert,
    [Text("Salvar textos selecionados na biblioteca", "Save selected texts to the library")] Save,
    [Text("Abrir o arquivo da biblioteca", "Open the library file")] Open,
}

public sealed class NotesOptions
{
    [Opt("Ação", "Action")]
    public NotesAction Action { get; set; } = NotesAction.Insert;

    [Opt("Título do bloco", "Block title", VisibleIf = "Action=Insert")]
    public string Title { get; set; } = "NOTAS";

    [Opt("Numerar as notas", "Number the notes", VisibleIf = "Action=Insert")]
    public bool Numbered { get; set; } = true;

    [Opt("Largura do texto (no papel)", "Text width (on paper)", Kind = OptKind.Length, Min = 0.01, VisibleIf = "Action=Insert")]
    public double Width { get; set; } = U.Mm(150);

    [Opt("Tipo de texto", "Text type", Kind = OptKind.TextNoteType, VisibleIf = "Action=Insert")]
    public string TextType { get; set; } = "";

    [Opt("Grupo para salvar", "Group to save in", VisibleIf = "Action=Save")]
    public string Group { get; set; } = "Minhas notas";
}

[Transaction(TransactionMode.Manual)]
[Tool("doc.notes", Modules.Documentation, "Notas padrão", "Standard notes", Order = 43, Group = "Anotação|Annotation",
    DescPt = "Biblioteca de notas padrão (gerais, acabamentos, elétrica, marcenaria...) para inserir como bloco numerado e para salvar novos textos.",
    DescEn = "Library of standard notes (general, finishes, electrical, joinery...) to insert as a numbered block and to save new texts.",
    Keywords = "notas padrao observacoes texto biblioteca standard notes text library")]
public sealed class NotesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<NotesOptions>();
        var lib = NoteLibrary.Load();
        var doc = ctx.Doc;
        switch (o.Action)
        {
            case NotesAction.Open:
                if (!File.Exists(NoteLibrary.FilePath)) lib.Save();
                MatLib.OpenFolder(NoteLibrary.FilePath);
                return;
            case NotesAction.Save:
            {
                var notes = ctx.SelectedOrPick("Selecione os textos", "Select the texts", e => e is TextNote).OfType<TextNote>().ToList();
                foreach (var n in notes)
                {
                    var text = n.Text.Trim();
                    if (text.Length == 0 || lib.Notes.Any(x => x.Text == text)) continue;
                    lib.Notes.Add(new NoteItem { Group = o.Group, Title = text.Length > 40 ? text[..40] + "…" : text, Text = text });
                    ctx.Report.Count(Loc.T("Notas salvas", "Notes saved"));
                }
                lib.Save();
                return;
            }
        }
        var chosen = Pick.Many(lib.Notes, n => $"[{n.Group}] {n.Title}", ctx.Tool.Name, Loc.T("Quais notas inserir?", "Which notes to insert?"), ctx.Owner, detail: n => n.Text);
        if (chosen.Count == 0) ctx.Cancel();
        var p = ctx.PickPoint("Clique onde inserir as notas", "Click where to insert the notes");
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(o.Title)) lines.Add(o.Title);
        for (var i = 0; i < chosen.Count; i++) lines.Add(o.Numbered ? $"{i + 1}. {chosen[i].Text}" : chosen[i].Text);
        var typeId = Lookup.Named<TextNoteType>(doc, o.TextType)?.Id ?? doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var tn = TextNote.Create(doc, ctx.ActiveView.Id, p, o.Width, string.Join("\r", lines), typeId);
            ctx.Report.Created();
            ctx.Report.AddResult(tn.Id.Value);
        });
    }
}
