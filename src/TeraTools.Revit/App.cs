using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using TeraTools.Core.Localization;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit;

/// <summary>Ponto de entrada do add-in (referenciado no manifesto TeraTools.addin).</summary>
public sealed class App : IExternalApplication
{
    public static readonly DockablePaneId PaneId = new(new Guid("2A91EE5D-638D-4EE9-8BDB-BAE81F500240"));

    public Result OnStartup(UIControlledApplication app)
    {
        try
        {
            Services.Initialize();
            Loc.Current = Services.ResolveLanguage(app.ControlledApplication.Language);
            Theme.Detect();
            EnumTexts.RegisterAll();
            ToolCatalog.Build(typeof(App).Assembly);
            HelpPages.WriteAll();
            RibbonBuilder.Build(app);
            app.RegisterDockablePane(PaneId, "Téra Tools", new PaneProvider());
            RevitTask.Initialize();
            app.ThemeChanged += OnThemeChanged;
            app.ViewActivated += OnViewActivated;
            Log.Info($"Téra Tools {typeof(App).Assembly.GetName().Version} iniciado ({ToolCatalog.All.Count} ferramentas, idioma {Loc.Current}).");
            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            Log.Error("Falha na inicialização do Téra Tools", ex);
            try
            {
                TaskDialog.Show("Téra Tools", Loc.T("O Téra Tools não pôde ser iniciado: ", "Téra Tools could not start: ") + ex.Message);
            }
            catch
            {
                // ignora
            }
            return Result.Failed;
        }
    }

    public Result OnShutdown(UIControlledApplication app)
    {
        try
        {
            app.ThemeChanged -= OnThemeChanged;
            app.ViewActivated -= OnViewActivated;
            Services.SaveSettings();
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao encerrar", ex);
        }
        return Result.Succeeded;
    }

    private static void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        try
        {
            Theme.Refresh();
            Icons.ClearCache();
            RibbonBuilder.RefreshIcons();
        }
        catch (Exception ex)
        {
            Log.Error("Falha ao trocar o tema", ex);
        }
    }

    private static void OnViewActivated(object? sender, ViewActivatedEventArgs e)
    {
        try
        {
            TeraPane.Instance.SetDocument(e.Document);
        }
        catch
        {
            // ignora
        }
    }
}

/// <summary>Monta a aba "Téra Tools" a partir do catálogo de ferramentas.</summary>
public static class RibbonBuilder
{
    public const string TabName = "Téra Tools";
    private static readonly List<(RibbonButton Button, string Icon, string Color)> Tracked = new();

    public static void Build(UIControlledApplication app)
    {
        try
        {
            app.CreateRibbonTab(TabName);
        }
        catch (Autodesk.Revit.Exceptions.ArgumentException)
        {
            // a aba já existe
        }
        var asm = typeof(App).Assembly.Location;
        foreach (var mod in Modules.All.OrderBy(m => m.Order))
        {
            var tools = ToolCatalog.InModule(mod.Id).Where(t => t.Ribbon).ToList();
            if (tools.Count == 0) continue;
            RibbonPanel panel;
            try
            {
                panel = app.CreateRibbonPanel(TabName, mod.Name.Value);
            }
            catch (Exception ex)
            {
                Log.Error($"Falha ao criar painel {mod.Id}", ex);
                continue;
            }

            foreach (var t in tools.Where(t => t.Large))
                Track(panel.AddItem(PushData(t, mod, asm)) as RibbonButton, t.Icon, mod.ColorHex);

            var entries = new List<(RibbonItemData Data, List<ToolInfo> Tools)>();
            foreach (var g in tools.Where(t => !t.Large).GroupBy(t => t.HasGroup ? "g:" + t.Group.Pt : "t:" + t.Id))
            {
                var list = g.ToList();
                if (!list[0].HasGroup)
                {
                    entries.Add((PushData(list[0], mod, asm), list));
                    continue;
                }
                var first = list[0];
                var pd = new PulldownButtonData("tt.pd." + mod.Id + "." + Core.Text.TextUtil.Slug(first.Group.Pt), first.Group.Value)
                {
                    ToolTip = string.Join(" · ", list.Select(x => x.Name.Value)),
                    Image = Icons.Bitmap(first.Icon, mod.ColorHex, 16),
                    LargeImage = Icons.Bitmap(first.Icon, mod.ColorHex, 32),
                };
                entries.Add((pd, list));
            }

            for (var i = 0; i < entries.Count; i += 3)
            {
                var chunk = entries.Skip(i).Take(3).ToList();
                IList<RibbonItem> added;
                try
                {
                    added = chunk.Count switch
                    {
                        3 => panel.AddStackedItems(chunk[0].Data, chunk[1].Data, chunk[2].Data),
                        2 => panel.AddStackedItems(chunk[0].Data, chunk[1].Data),
                        _ => new List<RibbonItem> { panel.AddItem(chunk[0].Data) },
                    };
                }
                catch (Exception ex)
                {
                    Log.Error($"Falha ao adicionar botões em {mod.Id}", ex);
                    continue;
                }
                for (var k = 0; k < added.Count && k < chunk.Count; k++)
                {
                    if (added[k] is PulldownButton pull)
                    {
                        Track(pull, chunk[k].Tools[0].Icon, mod.ColorHex);
                        foreach (var t in chunk[k].Tools)
                        {
                            try
                            {
                                Track(pull.AddPushButton(PushData(t, mod, asm)), t.Icon, mod.ColorHex);
                            }
                            catch (Exception ex)
                            {
                                Log.Error($"Falha ao adicionar {t.Id}", ex);
                            }
                        }
                    }
                    else if (added[k] is RibbonButton rb)
                    {
                        Track(rb, chunk[k].Tools[0].Icon, mod.ColorHex);
                    }
                }
            }
        }
    }

    private static PushButtonData PushData(ToolInfo t, ModuleInfo mod, string asm)
    {
        var icon = Icons.Has(t.Icon) ? t.Icon : mod.Icon;
        var help = t.Help.Value;
        var d = new PushButtonData("tt." + t.Id, t.Name.Value, asm, t.ClassName)
        {
            ToolTip = t.Description.Value,
            LongDescription = help.Length > 600 ? help[..600] + "…" : help,
            Image = Icons.Bitmap(icon, mod.ColorHex, 16),
            LargeImage = Icons.Bitmap(icon, mod.ColorHex, 32),
        };
        try
        {
            d.SetContextualHelp(new ContextualHelp(ContextualHelpType.Url, HelpPages.PathFor(t.Id)));
        }
        catch
        {
            // ajuda contextual é opcional
        }
        if (!t.RequiresDocument) d.AvailabilityClassName = typeof(AlwaysAvailable).FullName;
        return d;
    }

    private static void Track(RibbonButton? b, string icon, string color)
    {
        if (b != null) Tracked.Add((b, icon, color));
    }

    public static void RefreshIcons()
    {
        foreach (var (b, icon, color) in Tracked)
        {
            try
            {
                var key = Icons.Has(icon) ? icon : "mod.general";
                b.Image = Icons.Bitmap(key, color, 16);
                b.LargeImage = Icons.Bitmap(key, color, 32);
            }
            catch
            {
                // ignora
            }
        }
    }
}

/// <summary>Disponibilidade: ferramentas que funcionam mesmo sem projeto aberto.</summary>
public sealed class AlwaysAvailable : IExternalCommandAvailability
{
    public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => true;
}
