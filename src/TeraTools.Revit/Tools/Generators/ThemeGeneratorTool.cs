using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using TeraTools.Core.Colors;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Generators;

public enum ViewTheme
{
    [Text("Apresentação clara", "Light presentation")] Light,
    [Text("Croqui à mão", "Hand sketch")] Sketch,
    [Text("Maquete branca", "White model")] WhiteModel,
    [Text("Monocromático", "Monochrome")] Mono,
    [Text("Realista", "Realistic")] Realistic,
    [Text("Técnico preto e branco", "Technical black & white")] Technical,
    [Text("Personalizado", "Custom")] Custom,
}

public enum ThemeTarget
{
    [Text("Vista ativa", "Active view")] Active,
    [Text("Vistas escolhidas (em lote)", "Chosen views (batch)")] Chosen,
    [Text("Cópias das vistas escolhidas", "Copies of the chosen views")] Copies,
}

public sealed class ThemeOptions
{
    [Opt("Tema", "Theme", Group = "Tema|Theme")]
    public ViewTheme Theme { get; set; } = ViewTheme.Light;

    [Opt("Aplicar em", "Apply to", Group = "Tema|Theme")]
    public ThemeTarget Target { get; set; } = ThemeTarget.Chosen;

    [Opt("Desvincular modelo de vista (necessário para alterar)", "Detach view template (needed to change)", Group = "Tema|Theme")]
    public bool DetachTemplate { get; set; } = true;

    [Opt("Estilo visual", "Visual style", Group = "Personalizado|Custom", VisibleIf = "Theme=Custom")]
    public PresentationStyle Style { get; set; } = PresentationStyle.ShadingWithEdges;

    [Opt("Nível de detalhe", "Detail level", Group = "Personalizado|Custom", VisibleIf = "Theme=Custom")]
    public ViewDetail Detail { get; set; } = ViewDetail.Fine;

    [Opt("Linhas à mão livre", "Sketchy lines", Group = "Personalizado|Custom", VisibleIf = "Theme=Custom")]
    public bool Sketchy { get; set; }

    [Opt("Irregularidade (0–10)", "Jitter (0–10)", Group = "Personalizado|Custom", Min = 0, Max = 10, VisibleIf = "Theme=Custom&Sketchy")]
    public int Jitter { get; set; } = 4;

    [Opt("Prolongamento (0–10)", "Extension (0–10)", Group = "Personalizado|Custom", Min = 0, Max = 10, VisibleIf = "Theme=Custom&Sketchy")]
    public int Extension { get; set; } = 3;

    [Opt("Sombras (0–100)", "Shadows (0–100)", Group = "Personalizado|Custom", Min = 0, Max = 100, VisibleIf = "Theme=Custom")]
    public int Shadows { get; set; } = 40;

    [Opt("Cor das superfícies (vazio = manter)", "Surface color (empty = keep)", Group = "Personalizado|Custom", Kind = OptKind.Color, VisibleIf = "Theme=Custom")]
    public string SurfaceColor { get; set; } = "";

    [Opt("Cor das linhas (vazio = manter)", "Line color (empty = keep)", Group = "Personalizado|Custom", Kind = OptKind.Color, VisibleIf = "Theme=Custom")]
    public string LineColor { get; set; } = "";

    [Opt("Fundo em degradê nas vistas 3D", "Gradient background in 3D views", Group = "Personalizado|Custom", VisibleIf = "Theme=Custom")]
    public bool Gradient { get; set; } = true;

    [Opt("Cor do céu", "Sky color", Group = "Personalizado|Custom", Kind = OptKind.Color, VisibleIf = "Theme=Custom&Gradient")]
    public string Sky { get; set; } = "#DDE6EE";

    [Opt("Cor do horizonte", "Horizon color", Group = "Personalizado|Custom", Kind = OptKind.Color, VisibleIf = "Theme=Custom&Gradient")]
    public string Horizon { get; set; } = "#F7F7F5";

    [Opt("Cor do chão", "Ground color", Group = "Personalizado|Custom", Kind = OptKind.Color, VisibleIf = "Theme=Custom&Gradient")]
    public string Ground { get; set; } = "#C9C3B8";

    [Opt("Ocultar anotações", "Hide annotations", Group = "Personalizado|Custom", VisibleIf = "Theme=Custom")]
    public bool HideAnnotations { get; set; } = true;

    /// <summary>Configuração efetiva do tema escolhido.</summary>
    public ThemeOptions Resolve()
    {
        var t = (ThemeOptions)MemberwiseClone();
        switch (Theme)
        {
            case ViewTheme.Light:
                Set(t, PresentationStyle.ShadingWithEdges, false, 45, "", "", true, "#E3ECF3", "#FBFBF9", "#D6D0C6", true);
                break;
            case ViewTheme.Sketch:
                Set(t, PresentationStyle.HLR, true, 0, "", "#4A4540", false, "", "", "", true);
                t.Jitter = 6;
                t.Extension = 5;
                break;
            case ViewTheme.WhiteModel:
                Set(t, PresentationStyle.FlatColors, false, 60, "#F4F4F2", "#9A9A9A", true, "#EDEFF1", "#FFFFFF", "#E2E2E2", true);
                break;
            case ViewTheme.Mono:
                Set(t, PresentationStyle.HLR, false, 0, "", "#3C3C3C", false, "", "", "", false);
                break;
            case ViewTheme.Realistic:
                Set(t, PresentationStyle.Realistic, false, 55, "", "", true, "#BFD4E6", "#F2F0EA", "#B7AE9F", true);
                break;
            case ViewTheme.Technical:
                Set(t, PresentationStyle.HLR, false, 0, "", "#000000", false, "", "", "", false);
                t.Detail = ViewDetail.Fine;
                break;
        }
        return t;
    }

    private static void Set(ThemeOptions t, PresentationStyle style, bool sketchy, int shadows, string surface, string line, bool gradient, string sky, string horizon, string ground, bool hideAnn)
    {
        t.Style = style;
        t.Sketchy = sketchy;
        t.Shadows = shadows;
        t.SurfaceColor = surface;
        t.LineColor = line;
        t.Gradient = gradient;
        t.Sky = sky;
        t.Horizon = horizon;
        t.Ground = ground;
        t.HideAnnotations = hideAnn;
        t.Detail = ViewDetail.Fine;
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("gen.viewstyle", Modules.Generators, "Gerador de temas", "Theme generator", Order = 62, Large = true, Group = "Temas|Themes",
    DescPt = "Temas de vista prontos (apresentação, croqui, maquete branca, monocromático, realista, técnico) ou personalizados, aplicados em lote a várias vistas para padronizar a apresentação.",
    DescEn = "Ready-made view themes (presentation, sketch, white model, monochrome, realistic, technical) or custom ones, applied in batch to many views.",
    HelpPt = "1. Escolha um tema pronto ou \"Personalizado\".\n2. Escolha onde aplicar: vista ativa, várias vistas de uma vez, ou cópias delas (as originais ficam intactas).\n3. Salve seus temas como presets (pessoais ou do projeto) para manter o padrão gráfico em todo o escritório.\n\nO tema muda estilo visual, linhas à mão, sombras, cores de superfície e linha das categorias de modelo, fundo das vistas 3D e anotações. Vistas controladas por modelo de vista precisam ser desvinculadas (opção marcada por padrão).",
    HelpEn = "1. Choose a ready theme or \"Custom\".\n2. Choose where: active view, many views at once, or copies of them.\n3. Save your themes as presets.",
    Keywords = "tema temas vista apresentacao croqui maquete branca estilo grafico lote theme view presentation sketch white model batch")]
public sealed class ThemeGeneratorCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<ThemeOptions>(okText: Loc.T("Aplicar", "Apply"));
        var r = o.Resolve();
        var views = new List<View>();
        if (o.Target == ThemeTarget.Active)
        {
            views.Add(ctx.ActiveView);
        }
        else
        {
            var selected = ctx.Selected(e => e is View).Select(e => e.Id.Value).ToHashSet();
            views = Pick.Many(Lookup.Collect<View>(doc).Where(v => !v.IsTemplate && v.CanBePrinted && v is not ViewSheet && v is not ViewSchedule && v.ViewType != ViewType.Legend)
                    .OrderBy(v => v.ViewType.ToString()).ThenBy(v => v.Name, TextUtil.NaturalComparer),
                v => v.Name, ctx.Tool.Name, Loc.T("Aplicar o tema em quais vistas?", "Apply the theme to which views?"), ctx.Owner,
                v => selected.Contains(v.Id.Value) || v.Id == ctx.ActiveView.Id, v => v.ViewType.ToString());
            if (views.Count == 0) ctx.Cancel();
        }
        var modelCats = Lookup.ModelCategories(doc).Select(c => c.Id).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var issues = new HashSet<string>();
            foreach (var view in views)
            {
                var v = view;
                if (o.Target == ThemeTarget.Copies)
                {
                    if (!view.CanViewBeDuplicated(ViewDuplicateOption.Duplicate)) { ctx.Report.Skipped(); continue; }
                    v = (View)doc.GetElement(view.Duplicate(ViewDuplicateOption.Duplicate));
                    v.Name = Lookup.UniqueViewName(doc, $"{view.Name} — {EnumLabels.Label(typeof(ViewTheme), o.Theme)}");
                }
                if (v.ViewTemplateId != ElementId.InvalidElementId)
                {
                    if (!o.DetachTemplate) { ctx.Report.Warn(Loc.F("\"{0}\" usa modelo de vista e foi ignorada.", "\"{0}\" uses a view template and was skipped.", v.Name), v.Id.Value); continue; }
                    v.ViewTemplateId = ElementId.InvalidElementId;
                }
                void Try(string what, Action a)
                {
                    try { a(); } catch { issues.Add(what); }
                }
                Try(Loc.T("estilo visual", "visual style"), () => v.DisplayStyle = r.Style switch
                {
                    PresentationStyle.HLR => DisplayStyle.HLR,
                    PresentationStyle.Shading => DisplayStyle.Shading,
                    PresentationStyle.FlatColors => DisplayStyle.FlatColors,
                    PresentationStyle.Realistic => DisplayStyle.Realistic,
                    PresentationStyle.RealisticWithEdges => DisplayStyle.RealisticWithEdges,
                    _ => DisplayStyle.ShadingWithEdges,
                });
                Try(Loc.T("nível de detalhe", "detail level"), () => v.DetailLevel = r.Detail switch { ViewDetail.Coarse => ViewDetailLevel.Coarse, ViewDetail.Medium => ViewDetailLevel.Medium, _ => ViewDetailLevel.Fine });
                Try(Loc.T("linhas à mão", "sketchy lines"), () =>
                {
                    var sk = v.GetSketchyLines();
                    sk.EnableSketchyLines = r.Sketchy;
                    sk.Jitter = r.Jitter;
                    sk.Extension = r.Extension;
                    v.SetSketchyLines(sk);
                });
                Try(Loc.T("sombras", "shadows"), () => v.ShadowIntensity = r.Shadows);
                if (r.Gradient && v is View3D && r.Sky.Length > 0)
                    Try(Loc.T("fundo", "background"), () => v.SetBackground(ViewDisplayBackground.CreateGradient(C(r.Sky), C(r.Horizon), C(r.Ground))));
                Try(Loc.T("anotações", "annotations"), () => v.AreAnnotationCategoriesHidden = r.HideAnnotations && v.ViewType is ViewType.ThreeD);
                if (r.SurfaceColor.Length > 0 || r.LineColor.Length > 0)
                {
                    var ogs = new OverrideGraphicSettings();
                    if (r.SurfaceColor.Length > 0)
                    {
                        var solid = ViewFilters.SolidFill(doc);
                        if (solid != ElementId.InvalidElementId) ogs.SetSurfaceForegroundPatternId(solid);
                        ogs.SetSurfaceForegroundPatternColor(C(r.SurfaceColor));
                    }
                    if (r.LineColor.Length > 0)
                    {
                        ogs.SetProjectionLineColor(C(r.LineColor));
                        ogs.SetCutLineColor(C(r.LineColor));
                    }
                    foreach (var cat in modelCats)
                    {
                        try { if (v.IsCategoryOverridable(cat)) v.SetCategoryOverrides(cat, ogs); } catch { /* categoria não suportada */ }
                    }
                }
                ctx.Report.Modified();
                ctx.Report.AddResult(v.Id.Value);
            }
            if (issues.Count > 0)
                ctx.Report.Info(Loc.T("Alguns ajustes não se aplicam a todos os tipos de vista: ", "Some settings don't apply to every view type: ") + string.Join(", ", issues));
        });
    }

    private static Color C(string hex) => RgbColor.TryParse(hex, out var c) ? new Color(c.R, c.G, c.B) : new Color(255, 255, 255);
}
