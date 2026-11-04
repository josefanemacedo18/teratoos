using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Interiors;

internal static class RoomUtil
{
    public static string Label(Room r) => $"{r.Number} {r.Name}".Trim();

    public static List<Room> SelectedOrInView(ToolContext ctx)
    {
        var sel = ctx.Selected(e => e is Room).OfType<Room>().ToList();
        if (sel.Count > 0) return sel;
        var inView = new FilteredElementCollector(ctx.Doc, ctx.ActiveView.Id).OfCategory(BuiltInCategory.OST_Rooms).OfType<Room>().ToList();
        if (inView.Count > 0) return inView;
        return RevitGeo.Rooms(ctx.Doc).ToList();
    }
}

public sealed class RoomCreateOptions
{
    [Opt("Nível", "Level", Kind = OptKind.Level, Tip = "Vazio = nível da vista ativa.|Empty = active view level.")]
    public string Level { get; set; } = "";

    [Opt("Fase", "Phase", Kind = OptKind.Phase, Tip = "Vazio = fase da vista ativa.|Empty = active view phase.")]
    public string Phase { get; set; } = "";

    [Opt("Nome padrão", "Default name")]
    public string Name { get; set; } = "Ambiente";

    [Opt("Numeração — prefixo", "Numbering — prefix")]
    public string Prefix { get; set; } = "";

    [Opt("Numeração — início", "Numbering — start", Min = 0)]
    public int Start { get; set; } = 1;

    [Opt("Numeração — dígitos", "Numbering — digits", Min = 1, Max = 6)]
    public int Digits { get; set; } = 2;
}

[Transaction(TransactionMode.Manual)]
[Tool("room.create", Modules.Interiors, "Criar ambientes automaticamente", "Create rooms automatically", Order = 10, Group = "Ambientes|Rooms",
    DescPt = "Identifica todos os espaços fechados do nível e cria os ambientes com nome e numeração em sequência.",
    DescEn = "Finds all closed spaces on the level and creates rooms with sequential names and numbers.",
    Keywords = "criar ambientes identificar automatico rooms create auto")]
public sealed class RoomCreateCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<RoomCreateOptions>();
        var level = Lookup.Level(ctx.Doc, o.Level) ?? RevitGeo.ViewLevel(ctx.ActiveView) ?? throw new ToolException(Loc.T("Nenhum nível.", "No level."));
        var phaseId = ctx.ActiveView.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId();
        var phase = Lookup.Phase(ctx.Doc, o.Phase) ?? (phaseId != null ? ctx.Doc.GetElement(phaseId) as Phase : null) ?? ctx.Doc.Phases.Cast<Phase>().Last();
        var seq = new NumberingSequence { Prefix = o.Prefix, Start = o.Start, Digits = o.Digits };
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var ids = ctx.Doc.Create.NewRooms2(level, phase);
            ctx.Doc.Regenerate();
            var rooms = ids.Select(id => ctx.Doc.GetElement(id)).OfType<Room>()
                .Where(r => r.Location is LocationPoint)
                .OrderByDescending(r => Math.Round(((LocationPoint)r.Location).Point.Y, 1))
                .ThenBy(r => ((LocationPoint)r.Location).Point.X).ToList();
            var existing = RevitGeo.Rooms(ctx.Doc).Select(r => r.Number).ToHashSet();
            var n = 0;
            foreach (var r in rooms)
            {
                string num;
                do num = seq.Format(n++);
                while (existing.Contains(num) && n < 100000);
                existing.Add(num);
                r.Number = num;
                r.Name = o.Name;
                ctx.Report.AddResult(r.Id.Value);
            }
            ctx.Report.Created(rooms.Count);
            if (rooms.Count == 0) ctx.Report.Info(Loc.T("Nenhum espaço fechado novo encontrado neste nível.", "No new closed space found on this level."));
        });
    }
}

public enum RoomOrder
{
    [Text("Na ordem dos cliques", "In click order")] Click,
    [Text("Esquerda→direita, de cima para baixo", "Left→right, top to bottom")] Rows,
    [Text("De cima para baixo, esquerda→direita", "Top to bottom, left→right")] Columns,
    [Text("Sentido horário em torno do centro", "Clockwise around the center")] Clockwise,
    [Text("Por nível e depois posição", "By level, then position")] Level,
}

public sealed class RoomNumberOptions
{
    [Opt("Ordem", "Order")]
    public RoomOrder Order { get; set; } = RoomOrder.Click;

    [Opt("Prefixo", "Prefix")]
    public string Prefix { get; set; } = "";

    [Opt("Início", "Start", Min = 0)]
    public int Start { get; set; } = 1;

    [Opt("Passo", "Step", Min = 1)]
    public int Step { get; set; } = 1;

    [Opt("Dígitos", "Digits", Min = 1, Max = 6)]
    public int Digits { get; set; } = 2;

    [Opt("Sufixo", "Suffix")]
    public string Suffix { get; set; } = "";

    [Opt("Estilo", "Style")]
    public NumberingStyle Style { get; set; } = NumberingStyle.Numbers;

    [Opt("Prefixar com o nome do nível", "Prefix with level name", Tip = "Ex.: T-01, T-02 (usa o início do nome do nível).|E.g. T-01 (uses the start of the level name).")]
    public bool LevelPrefix { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("room.number", Modules.Interiors, "Numerar ambientes", "Number rooms", Order = 11, Group = "Ambientes|Rooms",
    DescPt = "Renumera ambientes em sequência: por cliques, por posição, sentido horário ou por nível; evita números repetidos.",
    DescEn = "Renumbers rooms in sequence: by clicks, position, clockwise or by level; avoids duplicates.",
    Keywords = "numerar numeracao ambientes sequencia number rooms sequence")]
public sealed class RoomNumberCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<RoomNumberOptions>();
        List<Room> rooms;
        if (o.Order == RoomOrder.Click)
        {
            rooms = new List<Room>();
            var seen = new HashSet<long>();
            while (true)
            {
                try
                {
                    var r = ctx.PickElement(Loc.F("Clique no ambiente nº {0} (Esc termina)", "Click room #{0} (Esc finishes)", rooms.Count + 1), "", e => e is Room rr && !seen.Contains(rr.Id.Value)) as Room;
                    if (r == null) break;
                    seen.Add(r.Id.Value);
                    rooms.Add(r);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    break;
                }
            }
        }
        else
        {
            rooms = RoomUtil.SelectedOrInView(ctx).Where(r => r.Location is LocationPoint).ToList();
            XYZ P(Room r) => ((LocationPoint)r.Location).Point;
            var c = rooms.Count == 0 ? XYZ.Zero : new XYZ(rooms.Average(r => P(r).X), rooms.Average(r => P(r).Y), 0);
            rooms = o.Order switch
            {
                RoomOrder.Columns => rooms.OrderBy(r => Math.Round(P(r).X, 0)).ThenByDescending(r => P(r).Y).ToList(),
                RoomOrder.Clockwise => rooms.OrderByDescending(r => Math.Atan2(P(r).Y - c.Y, P(r).X - c.X)).ToList(),
                RoomOrder.Level => rooms.OrderBy(r => r.Level?.Elevation ?? 0).ThenByDescending(r => Math.Round(P(r).Y, 0)).ThenBy(r => P(r).X).ToList(),
                _ => rooms.OrderByDescending(r => Math.Round(P(r).Y, 0)).ThenBy(r => P(r).X).ToList(),
            };
        }
        if (rooms.Count == 0) ctx.Cancel();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            for (var i = 0; i < rooms.Count; i++) rooms[i].Number = $"~tt{i}~{Guid.NewGuid():N}";
            var counters = new Dictionary<string, int>();
            foreach (var r in rooms)
            {
                var levelKey = o.LevelPrefix ? (r.Level?.Name ?? "").Split(' ', '-', '_').FirstOrDefault() ?? "" : "";
                counters.TryGetValue(levelKey, out var k);
                counters[levelKey] = k + 1;
                var seq = new NumberingSequence { Prefix = (levelKey.Length > 0 ? levelKey + "-" : "") + o.Prefix, Start = o.Start, Step = o.Step, Digits = o.Digits, Suffix = o.Suffix, Style = o.Style };
                r.Number = seq.Format(k);
                ctx.Report.Modified();
                ctx.Report.AddResult(r.Id.Value);
            }
        });
    }
}

public sealed class RoomRenameOptions
{
    [Opt("Localizar", "Find", Group = "Nome|Name")]
    public string Find { get; set; } = "";

    [Opt("Substituir por", "Replace with", Group = "Nome|Name")]
    public string Replace { get; set; } = "";

    [Opt("Usar expressão regular", "Use regular expression", Group = "Nome|Name")]
    public bool Regex { get; set; }

    [Opt("Maiúsculas/minúsculas", "Case", Group = "Nome|Name")]
    public TextCase Case { get; set; } = TextCase.Keep;

    [Opt("Prefixo", "Prefix", Group = "Nome|Name")]
    public string Prefix { get; set; } = "";

    [Opt("Sufixo", "Suffix", Group = "Nome|Name")]
    public string Suffix { get; set; } = "";

    [Opt("Códigos por nome (Nome=CÓDIGO, um por linha)", "Codes by name (Name=CODE, one per line)", Group = "Código|Code", Kind = OptKind.MultiLine)]
    public List<string> CodeMap { get; set; } = new() { "Sala=SAL", "Quarto=QTO", "Suíte=STE", "Cozinha=COZ", "Banheiro=BWC", "Lavabo=LAV", "Área de serviço=ASV", "Varanda=VAR", "Circulação=CIR", "Escritório=ESC" };

    [Opt("Modelo do código", "Code template", Group = "Código|Code", Tip = "Marcadores: {Codigo} {Numero} {Nome} {Nivel}.|Tokens: {Codigo} {Numero} {Nome} {Nivel}.")]
    public string CodeTemplate { get; set; } = "{Codigo}-{Numero}";

    [Opt("Gravar código no parâmetro", "Write code to parameter", Group = "Código|Code", Tip = "TT_Codigo é criado automaticamente.|TT_Codigo is created automatically.")]
    public string CodeParameter { get; set; } = SharedParams.Codigo;
}

[Transaction(TransactionMode.Manual)]
[Tool("room.rename", Modules.Interiors, "Padronizar nomes e códigos", "Standardize names and codes", Order = 12, Group = "Ambientes|Rooms",
    DescPt = "Localiza/substitui, padroniza maiúsculas e aplica códigos de ambiente por dicionário e modelo (ex.: SAL-01).",
    DescEn = "Find/replace, normalizes case and applies room codes from a dictionary and template (e.g., SAL-01).",
    Keywords = "renomear padronizar codigo nomes ambientes rename standard code rooms")]
public sealed class RoomRenameCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = RoomUtil.SelectedOrInView(ctx);
        var o = ctx.Ask<RoomRenameOptions>();
        var rule = new RenameRule { Find = o.Find, Replace = o.Replace, UseRegex = o.Regex, Case = o.Case, Prefix = o.Prefix, Suffix = o.Suffix };
        var map = o.CodeMap.Select(l => l.Split('=', 2)).Where(p => p.Length == 2).Select(p => (Key: TextUtil.NormalizeForCompare(p[0]), Code: p[1].Trim())).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.CodeParameter == SharedParams.Codigo)
                SharedParams.BindToProject(ctx.Doc, new[] { SharedParams.Codigo }, new[] { BuiltInCategory.OST_Rooms });
            foreach (var r in rooms)
            {
                var newName = rule.Apply(r.Name ?? "");
                if (newName != r.Name && !string.IsNullOrWhiteSpace(newName))
                {
                    r.Name = newName;
                    ctx.Report.Modified();
                }
                var norm = TextUtil.NormalizeForCompare(r.Name);
                var code = map.FirstOrDefault(m => norm.StartsWith(m.Key)).Code;
                if (!string.IsNullOrEmpty(code) && !string.IsNullOrWhiteSpace(o.CodeParameter))
                {
                    var value = NameTemplate.Apply(o.CodeTemplate, new Dictionary<string, object?> { ["Codigo"] = code, ["Numero"] = r.Number, ["Nome"] = r.Name, ["Nivel"] = r.Level?.Name });
                    if (RevitGeo.Set(r, value, o.CodeParameter)) ctx.Report.Count(Loc.T("Códigos", "Codes"));
                }
                ctx.Report.AddResult(r.Id.Value);
            }
        });
    }
}

public enum RoomDataMode
{
    [Text("Exportar planilha (Excel/CSV)", "Export spreadsheet (Excel/CSV)")] Export,
    [Text("Importar alterações de CSV", "Import changes from CSV")] Import,
    [Text("Definir valor em lote", "Set value in batch")] Set,
}

public sealed class RoomDataOptions
{
    [Opt("Modo", "Mode")]
    public RoomDataMode Mode { get; set; } = RoomDataMode.Export;

    [Opt("Parâmetros extras (um por linha)", "Extra parameters (one per line)", Kind = OptKind.MultiLine, VisibleIf = "Mode=Export")]
    public List<string> Parameters { get; set; } = new() { SharedParams.Codigo };

    [Opt("Arquivo CSV", "CSV file", Kind = OptKind.File, Filter = "CSV|*.csv", VisibleIf = "Mode=Import")]
    public string CsvPath { get; set; } = "";

    [Opt("Parâmetro", "Parameter", VisibleIf = "Mode=Set")]
    public string Parameter { get; set; } = "Departamento";

    [Opt("Valor", "Value", VisibleIf = "Mode=Set")]
    public string Value { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("room.params", Modules.Interiors, "Parâmetros de ambientes", "Room parameters", Order = 13, Group = "Ambientes|Rooms",
    DescPt = "Exporta os ambientes para planilha, importa as alterações de volta (por Id) ou define valores em lote.",
    DescEn = "Exports rooms to a spreadsheet, imports changes back (by Id) or sets values in batch.",
    HelpPt = "Fluxo planilha: 1) Exportar CSV. 2) Edite no Excel (não altere a coluna Id). 3) Importar: cada coluna com nome de parâmetro editável é atualizada.",
    HelpEn = "Spreadsheet flow: 1) Export CSV. 2) Edit in Excel (do not change the Id column). 3) Import: each column named after an editable parameter is updated.",
    Keywords = "parametros ambientes planilha excel csv importar exportar room parameters spreadsheet")]
public sealed class RoomDataCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<RoomDataOptions>();
        switch (o.Mode)
        {
            case RoomDataMode.Export:
            {
                var rooms = RevitGeo.Rooms(ctx.Doc).OrderBy(r => r.Number, TextUtil.NaturalComparer).ToList();
                var t = new TableData(Loc.T("Ambientes", "Rooms"));
                t.AddColumn("Id").AddColumn(Loc.T("Número", "Number")).AddColumn(Loc.T("Nome", "Name")).AddColumn(Loc.T("Nível", "Level"))
                 .AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number).AddColumn(Loc.T("Perímetro (m)", "Perimeter (m)"), ColumnKind.Number)
                 .AddColumn(Loc.T("Departamento", "Department")).AddColumn(Loc.T("Acab. piso", "Floor finish")).AddColumn(Loc.T("Acab. parede", "Wall finish"))
                 .AddColumn(Loc.T("Acab. forro", "Ceiling finish")).AddColumn(Loc.T("Acab. rodapé", "Base finish"));
                foreach (var p in o.Parameters) t.AddColumn(p);
                foreach (var r in rooms)
                {
                    var row = new List<object?>
                    {
                        r.Id.Value.ToString(), r.Number, r.Name, r.Level?.Name, U.SqFtToM2(r.Area), U.ToM(r.Perimeter),
                        RevitGeo.Text(r, BuiltInParameter.ROOM_DEPARTMENT), RevitGeo.Text(r, BuiltInParameter.ROOM_FINISH_FLOOR),
                        RevitGeo.Text(r, BuiltInParameter.ROOM_FINISH_WALL), RevitGeo.Text(r, BuiltInParameter.ROOM_FINISH_CEILING),
                        RevitGeo.Text(r, BuiltInParameter.ROOM_FINISH_BASE),
                    };
                    foreach (var p in o.Parameters) row.Add(RevitGeo.Display(r.LookupParameter(p)));
                    t.AddRowWithIds(new[] { r.Id.Value }, row.ToArray());
                }
                ctx.Report.Tables.Add(t);
                ctx.Report.ForceShow = true;
                ctx.Report.Info(Loc.T("Use \"Exportar Excel\" ou \"CSV\" no relatório. Para reimportar, mantenha a coluna Id.", "Use \"Export Excel\" or \"CSV\" in the report. To re-import, keep the Id column."));
                break;
            }
            case RoomDataMode.Import:
            {
                if (!File.Exists(o.CsvPath)) ToolContext.Fail("Escolha o arquivo CSV.", "Choose the CSV file.");
                var rows = CsvWriter.Parse(File.ReadAllText(o.CsvPath));
                if (rows.Count < 2) ToolContext.Fail("CSV vazio.", "Empty CSV.");
                var header = rows[0];
                var idCol = Array.FindIndex(header, h => h.Trim().Equals("Id", StringComparison.OrdinalIgnoreCase));
                if (idCol < 0) ToolContext.Fail("Coluna \"Id\" não encontrada.", "\"Id\" column not found.");
                var builtIn = new Dictionary<string, BuiltInParameter>(StringComparer.OrdinalIgnoreCase)
                {
                    [Loc.T("Número", "Number")] = BuiltInParameter.ROOM_NUMBER,
                    [Loc.T("Nome", "Name")] = BuiltInParameter.ROOM_NAME,
                    [Loc.T("Departamento", "Department")] = BuiltInParameter.ROOM_DEPARTMENT,
                    [Loc.T("Acab. piso", "Floor finish")] = BuiltInParameter.ROOM_FINISH_FLOOR,
                    [Loc.T("Acab. parede", "Wall finish")] = BuiltInParameter.ROOM_FINISH_WALL,
                    [Loc.T("Acab. forro", "Ceiling finish")] = BuiltInParameter.ROOM_FINISH_CEILING,
                    [Loc.T("Acab. rodapé", "Base finish")] = BuiltInParameter.ROOM_FINISH_BASE,
                };
                ctx.Modify(ctx.Tool.Name, () =>
                {
                    foreach (var row in rows.Skip(1))
                    {
                        if (idCol >= row.Length || !long.TryParse(row[idCol].Trim(), out var id)) continue;
                        if (ctx.Doc.GetElement(new ElementId(id)) is not Room room) continue;
                        var changed = false;
                        for (var c = 0; c < header.Length && c < row.Length; c++)
                        {
                            if (c == idCol) continue;
                            var name = header[c].Trim();
                            var p = builtIn.TryGetValue(name, out var bip) ? room.get_Parameter(bip) : room.LookupParameter(name);
                            if (p == null || p.IsReadOnly) continue;
                            var v = row[c];
                            if (RevitGeo.Display(p) == v) continue;
                            try
                            {
                                if (p.StorageType == StorageType.String) p.Set(v);
                                else p.SetValueString(v);
                                changed = true;
                            }
                            catch
                            {
                                ctx.Report.Warn(Loc.F("{0}: valor \"{1}\" inválido para {2}.", "{0}: invalid value \"{1}\" for {2}.", RoomUtil.Label(room), v, name), room.Id.Value);
                            }
                        }
                        if (changed)
                        {
                            ctx.Report.Modified();
                            ctx.Report.AddResult(room.Id.Value);
                        }
                    }
                });
                break;
            }
            default:
            {
                var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room).OfType<Room>().ToList();
                ctx.Modify(ctx.Tool.Name, () =>
                {
                    foreach (var r in rooms)
                    {
                        var p = r.LookupParameter(o.Parameter);
                        if (p == null || p.IsReadOnly)
                        {
                            ctx.Report.Warn(Loc.F("{0}: parâmetro \"{1}\" não editável.", "{0}: parameter \"{1}\" not editable.", RoomUtil.Label(r), o.Parameter), r.Id.Value);
                            continue;
                        }
                        if (p.StorageType == StorageType.String) p.Set(o.Value);
                        else p.SetValueString(o.Value);
                        ctx.Report.Modified();
                    }
                });
                break;
            }
        }
    }
}

[Transaction(TransactionMode.Manual)]
[Tool("room.schedule", Modules.Interiors, "Tabela de ambientes", "Room schedule", Order = 14, Group = "Ambientes|Rooms",
    DescPt = "Cria a tabela de ambientes com número, nome, nível, área, perímetro, departamento e acabamentos.",
    DescEn = "Creates the room schedule with number, name, level, area, perimeter, department and finishes.",
    Keywords = "tabela ambientes area schedule rooms")]
public sealed class RoomScheduleCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        ctx.Modify(ctx.Tool.Name, () =>
        {
            var vs = Schedules.Create(ctx.Doc, BuiltInCategory.OST_Rooms, Loc.T("TT Tabela de ambientes", "TT Room schedule"),
                new[]
                {
                    new Schedules.Spec("BIP:ROOM_NUMBER"), new Schedules.Spec("BIP:ROOM_NAME"), new Schedules.Spec("BIP:ROOM_LEVEL_ID"),
                    new Schedules.Spec("BIP:ROOM_AREA", Totals: true), new Schedules.Spec("BIP:ROOM_PERIMETER"),
                    new Schedules.Spec("BIP:ROOM_DEPARTMENT"), new Schedules.Spec("BIP:ROOM_FINISH_FLOOR"), new Schedules.Spec("BIP:ROOM_FINISH_WALL"),
                    new Schedules.Spec("BIP:ROOM_FINISH_CEILING"), new Schedules.Spec("BIP:ROOM_FINISH_BASE"),
                }, null, new[] { "BIP:ROOM_LEVEL_ID", "BIP:ROOM_NUMBER" }, report: ctx.Report);
            ctx.Report.Created();
            ctx.Report.AddResult(vs.Id.Value);
        });
        ctx.Report.ForceShow = true;
        ctx.Report.Success(Loc.T("Tabela criada no navegador de projeto (Tabelas/Quantidades).", "Schedule created in the Project Browser (Schedules/Quantities)."));
    }
}

public sealed class RoomCheckOptions
{
    [Opt("Área mínima", "Minimum area", Suffix = "m²", Min = 0)]
    public double MinAreaM2 { get; set; } = 1.0;

    [Opt("Nomes considerados \"sem nome\"", "Names considered \"unnamed\"", Kind = OptKind.MultiLine)]
    public List<string> DefaultNames { get; set; } = new() { "Ambiente", "Room", "Sala" };

    [Opt("Exigir departamento", "Require department")]
    public bool RequireDepartment { get; set; }
}

[Transaction(TransactionMode.Manual)]
[Tool("room.check", Modules.Interiors, "Verificar ambientes", "Check rooms", Order = 15, Group = "Ambientes|Rooms",
    DescPt = "Encontra ambientes não colocados, não fechados/redundantes, sem nome, com número repetido ou área muito pequena.",
    DescEn = "Finds unplaced, not enclosed/redundant, unnamed, duplicate-number or very small rooms.",
    Keywords = "verificar ambientes nao fechados redundantes duplicados check rooms unplaced unenclosed")]
public sealed class RoomCheckCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<RoomCheckOptions>();
        var rooms = new FilteredElementCollector(ctx.Doc).OfCategory(BuiltInCategory.OST_Rooms).OfType<Room>().ToList();
        var defaults = o.DefaultNames.Select(TextUtil.NormalizeForCompare).ToHashSet();
        foreach (var r in rooms)
        {
            if (r.Location == null) ctx.Report.Warn(Loc.F("Não colocado: {0}", "Unplaced: {0}", RoomUtil.Label(r)), r.Id.Value);
            else if (r.Area <= 0) ctx.Report.Error(Loc.F("Não fechado ou redundante: {0}", "Not enclosed or redundant: {0}", RoomUtil.Label(r)), r.Id.Value);
            else if (U.SqFtToM2(r.Area) < o.MinAreaM2) ctx.Report.Warn(Loc.F("Área muito pequena ({1}): {0}", "Very small area ({1}): {0}", RoomUtil.Label(r), U.FormatArea(r.Area)), r.Id.Value);
            if (string.IsNullOrWhiteSpace(r.Name) || defaults.Contains(TextUtil.NormalizeForCompare(r.Name)))
                ctx.Report.Warn(Loc.F("Sem nome definido: {0}", "No proper name: {0}", RoomUtil.Label(r)), r.Id.Value);
            if (o.RequireDepartment && string.IsNullOrWhiteSpace(RevitGeo.Text(r, BuiltInParameter.ROOM_DEPARTMENT)))
                ctx.Report.Info(Loc.F("Sem departamento: {0}", "No department: {0}", RoomUtil.Label(r)), r.Id.Value);
        }
        foreach (var g in rooms.Where(r => !string.IsNullOrWhiteSpace(r.Number)).GroupBy(r => r.Number).Where(g => g.Count() > 1))
            ctx.Report.Error(Loc.F("Número repetido \"{0}\" em {1} ambientes", "Number \"{0}\" repeated in {1} rooms", g.Key, g.Count()), g.Select(r => r.Id.Value));
        if (!ctx.Report.HasWarnings && !ctx.Report.HasErrors) ctx.Report.Success(Loc.F("{0} ambientes verificados sem pendências.", "{0} rooms checked with no issues.", rooms.Count));
        ctx.Report.ForceShow = true;
    }
}

public sealed class RoomViewsOptions
{
    [Opt("Criar planta recortada", "Create cropped plan")]
    public bool CreatePlan { get; set; } = true;

    [Opt("Margem do recorte", "Crop margin", Kind = OptKind.Length, Min = 0, VisibleIf = "CreatePlan")]
    public double Margin { get; set; } = U.Mm(500);

    [Opt("Modelo de vista da planta", "Plan view template", Kind = OptKind.ViewTemplate, Filter = "FloorPlan", VisibleIf = "CreatePlan")]
    public string PlanTemplate { get; set; } = "";

    [Opt("Criar elevações internas (4)", "Create interior elevations (4)")]
    public bool CreateElevations { get; set; } = true;

    [Opt("Escala das elevações (1:n)", "Elevation scale (1:n)", Min = 1, Max = 1000, VisibleIf = "CreateElevations")]
    public int Scale { get; set; } = 50;

    [Opt("Modelo de vista das elevações", "Elevation view template", Kind = OptKind.ViewTemplate, Filter = "Elevation", VisibleIf = "CreateElevations")]
    public string ElevationTemplate { get; set; } = "";

    [Opt("Modelo do nome", "Name template", Tip = "Marcadores: {Numero} {Nome} {Nivel} {Vista}.|Tokens: {Numero} {Nome} {Nivel} {Vista}.")]
    public string NameTemplate { get; set; } = "{Numero} {Nome} - {Vista}";
}

[Transaction(TransactionMode.Manual)]
[Tool("room.views", Modules.Interiors, "Vistas por ambiente", "Views per room", Order = 16, Group = "Ambientes|Rooms",
    DescPt = "Cria para cada ambiente uma planta recortada e as quatro elevações internas, já enquadradas e nomeadas.",
    DescEn = "Creates for each room a cropped plan and the four interior elevations, framed and named.",
    Keywords = "vistas ambiente elevacao interna planta recortada views room interior elevations")]
public sealed class RoomViewsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var rooms = ctx.SelectedOrPick("Selecione os ambientes", "Select the rooms", e => e is Room r && r.Area > 0).OfType<Room>().ToList();
        var o = ctx.Ask<RoomViewsOptions>();
        var doc = ctx.Doc;
        var elevType = Lookup.ViewFamilyType(doc, ViewFamily.Elevation);
        var letters = new[] { "A", "B", "C", "D" };
        ctx.CheckCount(rooms.Count * ((o.CreatePlan ? 1 : 0) + (o.CreateElevations ? 4 : 0)), "vistas", "views");
        ctx.Modify(ctx.Tool.Name, () =>
        {
            foreach (var room in rooms)
            {
                var level = room.Level;
                var bb = RevitGeo.Box(room);
                if (level == null || bb == null) continue;
                var basePlan = Lookup.Collect<ViewPlan>(doc).FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.GenLevel?.Id == level.Id);
                if (basePlan == null)
                {
                    ctx.Report.Warn(Loc.F("{0}: nenhuma planta do nível {1}.", "{0}: no floor plan for level {1}.", RoomUtil.Label(room), level.Name), room.Id.Value);
                    continue;
                }
                string Name(string view) => Lookup.UniqueViewName(doc, Core.Text.NameTemplate.Apply(o.NameTemplate, new Dictionary<string, object?> { ["Numero"] = room.Number, ["Nome"] = room.Name, ["Nivel"] = level.Name, ["Vista"] = view }));
                if (o.CreatePlan)
                {
                    var v = (View)doc.GetElement(basePlan.Duplicate(ViewDuplicateOption.WithDetailing));
                    v.Name = Name(Loc.T("Planta", "Plan"));
                    var cb = v.CropBox;
                    cb.Min = new XYZ(bb.Min.X - o.Margin, bb.Min.Y - o.Margin, cb.Min.Z);
                    cb.Max = new XYZ(bb.Max.X + o.Margin, bb.Max.Y + o.Margin, cb.Max.Z);
                    v.CropBox = cb;
                    v.CropBoxActive = true;
                    v.CropBoxVisible = false;
                    if (Lookup.View(doc, o.PlanTemplate) is { IsTemplate: true } t) v.ViewTemplateId = t.Id;
                    ctx.Report.Count(Loc.T("Plantas", "Plans"));
                    ctx.Report.AddResult(v.Id.Value);
                }
                if (o.CreateElevations && elevType != null)
                {
                    var center = room.Location is LocationPoint lp ? lp.Point : (bb.Min + bb.Max) / 2;
                    var marker = ElevationMarker.CreateElevationMarker(doc, elevType.Id, new XYZ(center.X, center.Y, level.Elevation), o.Scale);
                    for (var i = 0; i < 4; i++)
                    {
                        var ev = marker.CreateElevation(doc, basePlan.Id, i);
                        ev.Name = Name(Loc.T("Elevação ", "Elevation ") + letters[i]);
                        FitElevation(ev, bb, level.Elevation);
                        if (Lookup.View(doc, o.ElevationTemplate) is { IsTemplate: true } t) ev.ViewTemplateId = t.Id;
                        ctx.Report.Count(Loc.T("Elevações", "Elevations"));
                        ctx.Report.AddResult(ev.Id.Value);
                    }
                }
            }
        });
    }

    /// <summary>Ajusta o recorte da elevação aos limites do ambiente.</summary>
    internal static void FitElevation(View ev, BoundingBoxXYZ room, double levelZ)
    {
        try
        {
            var cb = ev.CropBox;
            var inv = cb.Transform.Inverse;
            double minX = double.MaxValue, maxX = double.MinValue, minZ = double.MaxValue, maxZ = double.MinValue;
            foreach (var x in new[] { room.Min.X, room.Max.X })
                foreach (var y in new[] { room.Min.Y, room.Max.Y })
                {
                    var p = inv.OfPoint(new XYZ(x, y, levelZ));
                    minX = Math.Min(minX, p.X);
                    maxX = Math.Max(maxX, p.X);
                    minZ = Math.Min(minZ, p.Z);
                    maxZ = Math.Max(maxZ, p.Z);
                }
            var yb = inv.OfPoint(new XYZ(room.Min.X, room.Min.Y, levelZ)).Y;
            var yt = inv.OfPoint(new XYZ(room.Min.X, room.Min.Y, room.Max.Z)).Y;
            cb.Min = new XYZ(minX, Math.Min(yb, yt) - 0.2, cb.Min.Z);
            cb.Max = new XYZ(maxX, Math.Max(yb, yt) + 0.2, cb.Max.Z);
            ev.CropBox = cb;
            ev.CropBoxActive = true;
            var far = ev.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
            if (far is { IsReadOnly: false }) far.Set(Math.Max(0.5, maxZ - minZ));
        }
        catch (Exception ex)
        {
            Log.Debug("Recorte de elevação: " + ex.Message);
        }
    }
}

public sealed class OrganizeViewsOptions
{
    [Opt("Regras (texto no nome => grupo)", "Rules (text in name => group)", Kind = OptKind.MultiLine)]
    public List<string> Rules { get; set; } = new()
    {
        "Layout => Layout", "Demoli => Demolir/Construir", "Constru => Demolir/Construir", "Acabamento => Acabamentos",
        "Paginação => Acabamentos", "Ilumina => Iluminação", "Forro => Forro", "Elevação => Elevações internas",
        "Marcenaria => Marcenaria", "Corte => Cortes e detalhes", "Detalhe => Cortes e detalhes", "3D => Apresentação",
    };

    [Opt("Grupo padrão por tipo de vista", "Default group by view type")]
    public bool ByViewType { get; set; } = true;

    [Opt("Gravar em", "Write to", Tip = "TT_Grupo é criado nas vistas automaticamente; use-o na organização do navegador.|TT_Grupo is created on views automatically; use it in the browser organization.")]
    public string Parameter { get; set; } = SharedParams.Grupo;
}

[Transaction(TransactionMode.Manual)]
[Tool("views.organize", Modules.Interiors, "Organizar vistas por setor", "Organize views by group", Order = 17, Group = "Ambientes|Rooms",
    DescPt = "Classifica vistas em grupos (layout, acabamentos, iluminação, forro...) por regras de nome, para organizar o navegador de projeto.",
    DescEn = "Classifies views into groups (layout, finishes, lighting, ceiling...) by name rules, to organize the Project Browser.",
    Keywords = "organizar vistas navegador disciplina setor organize views browser")]
public sealed class OrganizeViewsCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<OrganizeViewsOptions>();
        var rules = o.Rules.Select(r => r.Split("=>", 2)).Where(p => p.Length == 2).Select(p => (Key: p[0].Trim(), Group: p[1].Trim())).ToList();
        ctx.Modify(ctx.Tool.Name, () =>
        {
            if (o.Parameter == SharedParams.Grupo)
                SharedParams.BindToProject(ctx.Doc, new[] { SharedParams.Grupo }, new[] { BuiltInCategory.OST_Views, BuiltInCategory.OST_Sheets });
            foreach (var v in Lookup.Collect<View>(ctx.Doc).Where(v => !v.IsTemplate && v.CanBePrinted))
            {
                var g = rules.FirstOrDefault(r => v.Name.Contains(r.Key, StringComparison.OrdinalIgnoreCase)).Group;
                if (g == null && o.ByViewType)
                {
                    g = v.ViewType switch
                    {
                        ViewType.FloorPlan => Loc.T("Plantas", "Plans"),
                        ViewType.CeilingPlan => Loc.T("Forro", "Ceiling"),
                        ViewType.Elevation => Loc.T("Elevações", "Elevations"),
                        ViewType.Section => Loc.T("Cortes e detalhes", "Sections and details"),
                        ViewType.Detail => Loc.T("Cortes e detalhes", "Sections and details"),
                        ViewType.ThreeD => Loc.T("Apresentação", "Presentation"),
                        ViewType.Schedule => Loc.T("Tabelas", "Schedules"),
                        ViewType.DraftingView => Loc.T("Detalhes", "Details"),
                        ViewType.Legend => Loc.T("Legendas", "Legends"),
                        _ => null,
                    };
                }
                if (g != null && RevitGeo.Set(v, g, o.Parameter)) ctx.Report.Modified();
            }
        });
    }
}
