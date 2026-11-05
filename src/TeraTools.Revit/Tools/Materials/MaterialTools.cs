using System.Diagnostics;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Colors;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Materials;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Materials;

internal static class MatLib
{
    public static string FilePath => Path.Combine(Paths.Libraries, "materiais.json");

    public static MaterialLibrary Load()
    {
        try
        {
            if (File.Exists(FilePath)) return Json.LoadOrDefault<MaterialLibrary>(FilePath);
            var shared = Services.Settings.SharedLibraryFolder;
            if (!string.IsNullOrWhiteSpace(shared) && File.Exists(Path.Combine(shared, "materiais.json")))
                return Json.LoadOrDefault<MaterialLibrary>(Path.Combine(shared, "materiais.json"));
        }
        catch (Exception ex)
        {
            Log.Warn("Biblioteca de materiais: " + ex.Message);
        }
        return MaterialLibrary.Starter();
    }

    public static void Save(MaterialLibrary lib) => Json.Save(FilePath, lib);

    public static Material Ensure(Document doc, MaterialSpec s, bool updateExisting)
    {
        var color = RgbColor.TryParse(s.ColorHex, out var c) ? c : RgbColor.Gray;
        var existed = Lookup.Material(doc, s.Name) != null;
        var m = MaterialsService.Ensure(doc, s.Name, color, s.Transparency, s.Shininess, updateColor: updateExisting);
        if (existed && !updateExisting) return m;
        if (!string.IsNullOrWhiteSpace(s.Category))
        {
            try { m.MaterialCategory = s.Category; } catch { /* somente leitura em alguns materiais */ }
        }
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_DESCRIPTION, s.Description ?? s.Finish ?? "");
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MANUFACTURER, s.Manufacturer ?? "");
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MODEL, s.Reference ?? "");
        RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_MARK, s.Code);
        if (s.UnitCost.HasValue) RevitGeo.Set(m, BuiltInParameter.ALL_MODEL_COST, s.UnitCost.Value);
        return m;
    }

    public static MaterialSpec FromMaterial(Material m) => new()
    {
        Name = m.Name,
        Code = RevitGeo.Text(m, BuiltInParameter.ALL_MODEL_MARK),
        Category = m.MaterialCategory ?? "",
        ColorHex = MaterialsService.ColorOf(m).Hex,
        Manufacturer = NullIfEmpty(RevitGeo.Text(m, BuiltInParameter.ALL_MODEL_MANUFACTURER)),
        Reference = NullIfEmpty(RevitGeo.Text(m, BuiltInParameter.ALL_MODEL_MODEL)),
        Description = NullIfEmpty(RevitGeo.Text(m, BuiltInParameter.ALL_MODEL_DESCRIPTION)),
        UnitCost = m.get_Parameter(BuiltInParameter.ALL_MODEL_COST)?.AsDouble() is { } cost && cost > 0 ? cost : null,
        Transparency = m.Transparency,
        Shininess = m.Shininess,
    };

    private static string? NullIfEmpty(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    public static readonly string[] CsvHeader =
        { "Nome", "Codigo", "Categoria", "Cor", "Fabricante", "Referencia", "Acabamento", "Descricao", "Fornecedor", "Custo", "Unidade", "Uso", "Transparencia" };

    public static TableData ToTable(MaterialLibrary lib)
    {
        var t = new TableData(Loc.T("Biblioteca de materiais", "Material library"));
        foreach (var h in CsvHeader) t.AddColumn(h);
        foreach (var s in lib.Items)
            t.AddRow(s.Name, s.Code, s.Category, s.ColorHex, s.Manufacturer, s.Reference, s.Finish, s.Description, s.Supplier,
                s.UnitCost?.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), s.Unit, s.Use, s.Transparency.ToString());
        return t;
    }

    public static int Import(MaterialLibrary lib, string csvText)
    {
        var rows = CsvWriter.Parse(csvText);
        if (rows.Count < 2) return 0;
        var head = rows[0].Select(h => TextUtil.NormalizeForCompare(h)).ToArray();
        int Col(string name) => Array.FindIndex(head, h => h == TextUtil.NormalizeForCompare(name));
        string? Get(string[] r, string name)
        {
            var i = Col(name);
            return i >= 0 && i < r.Length && !string.IsNullOrWhiteSpace(r[i]) ? r[i].Trim() : null;
        }
        var n = 0;
        foreach (var r in rows.Skip(1))
        {
            var name = Get(r, "Nome");
            if (name == null) continue;
            var s = lib.Find(name);
            if (s == null)
            {
                s = new MaterialSpec { Name = name };
                lib.Items.Add(s);
            }
            s.Code = Get(r, "Codigo") ?? s.Code;
            s.Category = Get(r, "Categoria") ?? s.Category;
            if (Get(r, "Cor") is { } hex && RgbColor.TryParse(hex, out var c)) s.ColorHex = c.Hex;
            s.Manufacturer = Get(r, "Fabricante") ?? s.Manufacturer;
            s.Reference = Get(r, "Referencia") ?? s.Reference;
            s.Finish = Get(r, "Acabamento") ?? s.Finish;
            s.Description = Get(r, "Descricao") ?? s.Description;
            s.Supplier = Get(r, "Fornecedor") ?? s.Supplier;
            if (U.TryParseNumber(Get(r, "Custo"), out var cost)) s.UnitCost = cost;
            s.Unit = Get(r, "Unidade") ?? s.Unit;
            s.Use = Get(r, "Uso") ?? s.Use;
            if (int.TryParse(Get(r, "Transparencia"), out var tr)) s.Transparency = Math.Clamp(tr, 0, 100);
            n++;
        }
        return n;
    }

    public static void OpenFolder(string file)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Warn("Abrir pasta: " + ex.Message);
        }
    }

    public static bool Wildcard(string text, string pattern)
    {
        var t = TextUtil.NormalizeForCompare(text);
        var p = TextUtil.NormalizeForCompare(pattern.Trim());
        if (p.Length == 0 || p == "*") return true;
        var rx = "^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(t, rx);
    }
}

/// <summary>Uso de materiais no modelo (categorias, áreas e volumes).</summary>
internal static class MaterialUsage
{
    public sealed class Usage
    {
        public ElementId MaterialId { get; init; } = ElementId.InvalidElementId;
        public Dictionary<string, (double Area, double Volume, List<long> Ids)> ByCategory { get; } = new();
        public double Area => ByCategory.Values.Sum(v => v.Area);
        public double Volume => ByCategory.Values.Sum(v => v.Volume);
        public int Count => ByCategory.Values.Sum(v => v.Ids.Count);
    }

    public static Dictionary<long, Usage> Scan(Document doc, bool includePainted)
    {
        var result = new Dictionary<long, Usage>();
        var cats = Lookup.ModelCategories(doc).Select(c => c.Id).ToList();
        var col = new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(new ElementMulticategoryFilter(cats));
        foreach (var e in col)
        {
            ICollection<ElementId> ids;
            try
            {
                ids = e.GetMaterialIds(false);
                if (includePainted) ids = ids.Concat(e.GetMaterialIds(true)).Distinct().ToList();
            }
            catch
            {
                continue;
            }
            foreach (var mid in ids)
            {
                if (!result.TryGetValue(mid.Value, out var u)) result[mid.Value] = u = new Usage { MaterialId = mid };
                double area = 0, vol = 0;
                try { area = e.GetMaterialArea(mid, includePainted); } catch { /* sem área */ }
                try { vol = e.GetMaterialVolume(mid); } catch { /* sem volume */ }
                var cat = e.Category?.Name ?? "";
                u.ByCategory.TryGetValue(cat, out var acc);
                acc.Ids ??= new List<long>();
                acc.Ids.Add(e.Id.Value);
                u.ByCategory[cat] = (acc.Area + area, acc.Volume + vol, acc.Ids);
            }
        }
        return result;
    }
}

public enum LibraryAction
{
    [Text("Criar/atualizar materiais no projeto", "Create/update materials in the project")] ToProject,
    [Text("Adicionar materiais do projeto à biblioteca", "Add project materials to the library")] FromProject,
    [Text("Importar planilha (CSV)", "Import spreadsheet (CSV)")] Import,
    [Text("Exportar para planilha (CSV)", "Export to spreadsheet (CSV)")] Export,
}

public sealed class LibraryOptions
{
    [Opt("Ação", "Action")]
    public LibraryAction Action { get; set; } = LibraryAction.ToProject;

    [Opt("Atualizar cores e dados de materiais existentes", "Update colors and data of existing materials", VisibleIf = "Action=ToProject")]
    public bool UpdateExisting { get; set; }

    [Opt("Arquivo CSV", "CSV file", Kind = OptKind.File, Filter = "CSV|*.csv", VisibleIf = "Action=Import")]
    public string CsvFile { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.library", Modules.Materials, "Biblioteca de materiais", "Material library", Order = 10, Large = true,
    DescPt = "Biblioteca pessoal de materiais (cor, código, fabricante, referência, acabamento, fornecedor, custo) para criar no projeto, alimentar do projeto e editar no Excel.",
    DescEn = "Personal material library (color, code, manufacturer, reference, finish, supplier, cost) to create in the project, fill from the project and edit in Excel.",
    HelpPt = "A biblioteca fica em %AppData%\\TeraTools\\bibliotecas\\materiais.json. Para editar em massa: exporte para CSV, edite no Excel (mantenha os cabeçalhos) e importe de volta. Uma pasta de biblioteca compartilhada pode ser definida nas configurações.",
    HelpEn = "The library is at %AppData%\\TeraTools\\bibliotecas\\materiais.json. For bulk editing: export to CSV, edit in Excel (keep headers) and import back. A shared library folder can be set in settings.",
    Keywords = "biblioteca materiais acabamentos catalogo library materials catalog finishes")]
public sealed class MaterialLibraryCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<LibraryOptions>();
        var doc = ctx.Doc;
        var lib = MatLib.Load();
        switch (o.Action)
        {
            case LibraryAction.ToProject:
            {
                var items = Pick.Many(lib.Items.OrderBy(i => i.Category).ThenBy(i => i.Name), i => i.Name, ctx.Tool.Name,
                    Loc.T("Quais materiais criar/atualizar no projeto?", "Which materials to create/update in the project?"), ctx.Owner,
                    detail: i => string.Join(" · ", new[] { i.Category, i.Manufacturer, i.Reference }.Where(x => !string.IsNullOrWhiteSpace(x))));
                if (items.Count == 0) ctx.Cancel();
                ctx.Modify(ctx.Tool.Name, () =>
                {
                    foreach (var s in items)
                    {
                        var existed = Lookup.Material(doc, s.Name) != null;
                        var m = MatLib.Ensure(doc, s, o.UpdateExisting);
                        if (existed) ctx.Report.Modified(); else ctx.Report.Created();
                        ctx.Report.AddResult(m.Id.Value);
                    }
                });
                break;
            }
            case LibraryAction.FromProject:
            {
                var mats = Pick.Many(Lookup.Collect<Material>(doc).OrderBy(m => m.Name), m => m.Name, ctx.Tool.Name,
                    Loc.T("Quais materiais adicionar à biblioteca?", "Which materials to add to the library?"), ctx.Owner);
                if (mats.Count == 0) ctx.Cancel();
                foreach (var m in mats)
                {
                    var spec = MatLib.FromMaterial(m);
                    var existing = lib.Find(m.Name);
                    if (existing != null) lib.Items.Remove(existing);
                    lib.Items.Add(spec);
                    ctx.Report.Count(Loc.T("Adicionados à biblioteca", "Added to the library"));
                }
                MatLib.Save(lib);
                break;
            }
            case LibraryAction.Import:
            {
                if (!File.Exists(o.CsvFile)) ToolContext.Fail("Escolha o arquivo CSV.", "Choose the CSV file.");
                var n = MatLib.Import(lib, File.ReadAllText(o.CsvFile));
                MatLib.Save(lib);
                ctx.Report.Success(Loc.F("{0} materiais importados para a biblioteca.", "{0} materials imported into the library.", n));
                break;
            }
            default:
            {
                var path = Path.Combine(Paths.Exports, "biblioteca-materiais.csv");
                CsvWriter.Write(MatLib.ToTable(lib), path, ctx.Settings.CsvSeparator);
                ctx.Report.Success(Loc.F("Biblioteca exportada: {0}", "Library exported: {0}", path));
                MatLib.OpenFolder(path);
                break;
            }
        }
        ctx.Report.Tables.Add(MatLib.ToTable(lib));
    }
}

public sealed class FinishSchemeOptions
{
    [Opt("Regras: padrão do nome; piso; parede; forro; rodapé", "Rules: name pattern; floor; wall; ceiling; base", Kind = OptKind.MultiLine,
        Tip = "Use * como curinga (ex.: Banh*). A primeira regra que combinar vale. Deixe vazio ou — para não alterar.|Use * as wildcard. The first matching rule applies. Leave empty or — to keep.")]
    public List<string> Rules { get; set; } = new()
    {
        "Banh*; Porcelanato 60x60; Revestimento cerâmico 30x60; Forro de gesso pintado; —",
        "Lavabo*; Porcelanato 60x60; Pintura acrílica branca; Forro de gesso pintado; Porcelanato 7 cm",
        "Cozinha*; Porcelanato 60x60; Revestimento cerâmico 30x60; Forro de gesso pintado; Porcelanato 7 cm",
        "Área de serviço*; Porcelanato 60x60; Revestimento cerâmico 30x60; Forro de gesso pintado; —",
        "Quarto*; Piso vinílico; Pintura acrílica branca; Forro de gesso pintado; Poliestireno 7 cm",
        "Suíte*; Piso vinílico; Pintura acrílica branca; Forro de gesso pintado; Poliestireno 7 cm",
        "*; Porcelanato 60x60; Pintura acrílica branca; Forro de gesso pintado; Poliestireno 7 cm",
    };

    [Opt("Sobrescrever valores já preenchidos", "Overwrite already filled values")]
    public bool Overwrite { get; set; }

    [Opt("Pintar as paredes com o material de parede (se existir no projeto)", "Paint walls with the wall material (if it exists in the project)")]
    public bool PaintWalls { get; set; }

    [Opt("Criar tabela de acabamentos", "Create finish schedule")]
    public bool Schedule { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.byroom", Modules.Materials, "Acabamentos por ambiente", "Finishes by room", Order = 12,
    DescPt = "Preenche piso, parede, forro e rodapé de cada ambiente por regras de nome (ex.: Banh* → porcelanato), pinta paredes se quiser e cria a tabela de acabamentos.",
    DescEn = "Fills floor, wall, ceiling and base finish of each room by name rules (e.g. Bath* → porcelain), optionally paints walls and creates the finish schedule.",
    Keywords = "acabamentos ambiente tabela piso parede forro rodape finishes room schedule")]
public sealed class FinishByRoomCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = RoomUtil.SelectedOrInView(ctx).Where(r => r.Area > 0).ToList();
        var o = ctx.Ask<FinishSchemeOptions>();
        var doc = ctx.Doc;
        var rules = o.Rules.Select(l => l.Split(';').Select(p => p.Trim()).ToArray()).Where(p => p.Length >= 2 && p[0].Length > 0).ToList();
        var bips = new[] { BuiltInParameter.ROOM_FINISH_FLOOR, BuiltInParameter.ROOM_FINISH_WALL, BuiltInParameter.ROOM_FINISH_CEILING, BuiltInParameter.ROOM_FINISH_BASE };
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms)
            {
                var rule = rules.FirstOrDefault(r => MatLib.Wildcard(room.Name ?? "", r[0]));
                if (rule == null)
                {
                    ctx.Report.Skipped();
                    continue;
                }
                var changed = false;
                for (var i = 0; i < 4 && i + 1 < rule.Length; i++)
                {
                    var v = rule[i + 1];
                    if (v.Length == 0 || v == "—" || v == "-") continue;
                    var cur = RevitGeo.Text(room, bips[i]);
                    if (!o.Overwrite && !string.IsNullOrWhiteSpace(cur)) continue;
                    if (cur != v && RevitGeo.Set(room, bips[i], v)) changed = true;
                }
                if (o.PaintWalls && rule.Length > 2 && Lookup.Material(doc, rule[2]) is { } wm)
                    ctx.Report.Count(Loc.T("Faces pintadas", "Painted faces"), RoomWallPainter.Paint(doc, room, wm, ctx.Report));
                if (changed)
                {
                    ctx.Report.Modified();
                    ctx.Report.AddResult(room.Id.Value);
                }
            }
            if (o.Schedule)
            {
                var vs = Schedules.Create(doc, BuiltInCategory.OST_Rooms, Loc.T("TT Tabela de acabamentos", "TT Finish schedule"),
                    new[]
                    {
                        new Schedules.Spec("BIP:ROOM_NUMBER"), new Schedules.Spec("BIP:ROOM_NAME"), new Schedules.Spec("BIP:ROOM_FINISH_FLOOR"),
                        new Schedules.Spec("BIP:ROOM_FINISH_WALL"), new Schedules.Spec("BIP:ROOM_FINISH_CEILING"), new Schedules.Spec("BIP:ROOM_FINISH_BASE"),
                        new Schedules.Spec("BIP:ROOM_AREA", Totals: true),
                    }, null, new[] { "BIP:ROOM_NUMBER" }, report: ctx.Report);
                ctx.Report.Count(Loc.T("Tabelas", "Schedules"));
                ctx.Report.AddResult(vs.Id.Value);
            }
        });
    }
}

public sealed class MatReplaceOptions
{
    [Opt("Material atual", "Current material", Kind = OptKind.Material, Required = true)]
    public string From { get; set; } = "";

    [Opt("Novo material", "New material", Kind = OptKind.Material, Required = true)]
    public string To { get; set; } = "";

    [Opt("Em parâmetros de tipos e instâncias", "In type and instance parameters")]
    public bool Parameters { get; set; } = true;

    [Opt("Nas camadas de paredes, pisos, forros e coberturas", "In wall, floor, ceiling and roof layers")]
    public bool Layers { get; set; } = true;

    [Opt("Nas faces pintadas", "In painted faces")]
    public bool Painted { get; set; } = true;

    [Opt("Excluir o material antigo ao final", "Delete the old material at the end")]
    public bool DeleteOld { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.replace", Modules.Materials, "Substituir material", "Replace material", Order = 13,
    DescPt = "Troca um material por outro em todo o projeto: parâmetros de tipos e instâncias, camadas de sistemas e faces pintadas.",
    DescEn = "Swaps a material for another across the project: type and instance parameters, system layers and painted faces.",
    Keywords = "substituir trocar material projeto replace swap material")]
public sealed class MaterialReplaceCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<MatReplaceOptions>();
        var doc = ctx.Doc;
        var from = Lookup.Material(doc, o.From) ?? throw new ToolException(Loc.T("Material atual não encontrado.", "Current material not found."));
        var to = Lookup.Material(doc, o.To) ?? throw new ToolException(Loc.T("Novo material não encontrado.", "New material not found."));
        if (from.Id == to.Id) ToolContext.Fail("Os materiais são iguais.", "The materials are the same.");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.Parameters)
            {
                var elements = new FilteredElementCollector(doc).WhereElementIsElementType().ToElements()
                    .Concat(new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).ToElements());
                foreach (var e in elements)
                {
                    var hit = false;
                    foreach (Parameter p in e.Parameters)
                    {
                        if (p.StorageType != StorageType.ElementId || p.IsReadOnly || p.AsElementId() != from.Id) continue;
                        try
                        {
                            p.Set(to.Id);
                            hit = true;
                        }
                        catch
                        {
                            // parâmetro bloqueado
                        }
                    }
                    if (hit)
                    {
                        ctx.Report.Count(Loc.T("Parâmetros", "Parameters"));
                        ctx.Report.AddResult(e.Id.Value);
                    }
                }
            }
            if (o.Layers)
            {
                foreach (var t in new FilteredElementCollector(doc).OfClass(typeof(HostObjAttributes)).Cast<HostObjAttributes>())
                {
                    var cs = t.GetCompoundStructure();
                    if (cs == null) continue;
                    var hit = false;
                    for (var i = 0; i < cs.LayerCount; i++)
                    {
                        if (cs.GetMaterialId(i) != from.Id) continue;
                        cs.SetMaterialId(i, to.Id);
                        hit = true;
                    }
                    if (!hit) continue;
                    try
                    {
                        t.SetCompoundStructure(cs);
                        ctx.Report.Count(Loc.T("Tipos de sistema", "System types"));
                    }
                    catch (Exception ex)
                    {
                        ctx.Report.Warn(t.Name + ": " + ex.Message, t.Id.Value);
                    }
                }
            }
            if (o.Painted)
            {
                foreach (var e in new FilteredElementCollector(doc).WhereElementIsNotElementType().WherePasses(new ElementMulticategoryFilter(Lookup.ModelCategories(doc).Select(c => c.Id).ToList())))
                {
                    ICollection<ElementId> painted;
                    try { painted = e.GetMaterialIds(true); } catch { continue; }
                    if (!painted.Contains(from.Id)) continue;
                    foreach (var s in RevitGeo.Solids(e))
                        foreach (Face f in s.Faces)
                        {
                            try
                            {
                                if (doc.GetPaintedMaterial(e.Id, f) == from.Id)
                                {
                                    doc.Paint(e.Id, f, to.Id);
                                    ctx.Report.Count(Loc.T("Faces pintadas", "Painted faces"));
                                }
                            }
                            catch
                            {
                                // face não pintável
                            }
                        }
                }
            }
            if (o.DeleteOld)
            {
                try
                {
                    doc.Delete(from.Id);
                    ctx.Report.Deleted();
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Loc.T("O material antigo não pôde ser excluído: ", "The old material could not be deleted: ") + ex.Message);
                }
            }
        });
    }
}

public sealed class MatSpecsOptions
{
    [Opt("Somente materiais usados no modelo", "Only materials used in the model")]
    public bool UsedOnly { get; set; } = true;

    [Opt("Incluir faces pintadas", "Include painted faces")]
    public bool Painted { get; set; } = true;

    [Opt("Salvar ficha (HTML e Excel)", "Save sheet (HTML and Excel)")]
    public bool Save { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("mat.specs", Modules.Materials, "Ficha técnica de materiais", "Material specification sheet", Order = 14,
    DescPt = "Gera a ficha de materiais (código, fabricante, referência, acabamento, fornecedor, custo) com onde e quanto cada um é usado (área, volume, elementos por categoria).",
    DescEn = "Generates the material specification: code, name, category, manufacturer, reference, finish, supplier, cost and where each is used.",
    Keywords = "ficha tecnica memorial descritivo materiais especificacao specification sheet materials")]
public sealed class MaterialSpecsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<MatSpecsOptions>();
        var doc = ctx.Doc;
        var lib = MatLib.Load();
        var usage = MaterialUsage.Scan(doc, o.Painted);
        var mats = Lookup.Collect<Material>(doc).Where(m => !o.UsedOnly || usage.ContainsKey(m.Id.Value)).OrderBy(m => m.MaterialCategory).ThenBy(m => m.Name).ToList();
        var t = new TableData(Loc.T("Ficha técnica de materiais", "Material specification sheet"));
        t.AddColumn(Loc.T("Código", "Code")).AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Categoria", "Category")).AddColumn(Loc.T("Cor", "Color"))
         .AddColumn(Loc.T("Fabricante", "Manufacturer")).AddColumn(Loc.T("Referência", "Reference")).AddColumn(Loc.T("Acabamento", "Finish"))
         .AddColumn(Loc.T("Fornecedor", "Supplier")).AddColumn(Loc.T("Custo", "Cost"), ColumnKind.Money).AddColumn(Loc.T("Onde é usado", "Where used"))
         .AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number).AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3)
         .AddColumn(Loc.T("Elementos", "Elements"), ColumnKind.Integer);
        foreach (var m in mats)
        {
            var s = lib.Find(m.Name) ?? MatLib.FromMaterial(m);
            var where = usage.TryGetValue(m.Id.Value, out var u) ? string.Join(", ", u.ByCategory.Keys.OrderBy(k => k)) : "";
            t.AddRowWithIds(u?.ByCategory.Values.SelectMany(v => v.Ids).Take(500) ?? Enumerable.Empty<long>(),
                string.IsNullOrWhiteSpace(s.Code) ? RevitGeo.Text(m, BuiltInParameter.ALL_MODEL_MARK) : s.Code, m.Name, s.Category, MaterialsService.ColorOf(m).Hex,
                s.Manufacturer, s.Reference, s.Finish ?? s.Description, s.Supplier, s.UnitCost, where,
                u == null ? 0 : U.SqFtToM2(u.Area), u == null ? 0 : U.CuFtToM3(u.Volume), u?.Count ?? 0);
        }
        ctx.Report.Tables.Add(t);
        var byCat = new TableData(Loc.T("Uso de materiais por categoria", "Material usage by category"));
        byCat.AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Categoria", "Category"))
            .AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number).AddColumn(Loc.T("Volume (m³)", "Volume (m³)"), ColumnKind.Number, 3)
            .AddColumn(Loc.T("Elementos", "Elements"), ColumnKind.Integer);
        foreach (var m in mats)
        {
            if (!usage.TryGetValue(m.Id.Value, out var u)) continue;
            foreach (var (cat, v) in u.ByCategory.OrderBy(k => k.Key))
                byCat.AddRowWithIds(v.Ids, m.Name, cat, U.SqFtToM2(v.Area), U.CuFtToM3(v.Volume), v.Ids.Count);
        }
        byCat.AddTotalsRow(Loc.T("Total", "Total"), 2, 3, 4);
        ctx.Report.Tables.Add(byCat);
        if (o.Save)
        {
            try
            {
                var title = TextUtil.SafeFileName(string.IsNullOrWhiteSpace(doc.Title) ? "projeto" : doc.Title);
                var html = Path.Combine(Paths.Exports, $"{title} - ficha de materiais.html");
                HtmlReportWriter.Write(html, Loc.T("Ficha técnica de materiais", "Material specification sheet"), doc.Title, null, new[] { t, byCat });
                XlsxWriter.Write(Path.ChangeExtension(html, ".xlsx"), t, byCat);
                ctx.Report.Success(Loc.F("Ficha salva: {0}", "Sheet saved: {0}", html));
                MatLib.OpenFolder(html);
            }
            catch (Exception ex)
            {
                ctx.Report.Error(ex.Message);
            }
        }
        ctx.Report.ForceShow = true;
    }
}

