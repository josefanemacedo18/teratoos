using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using TeraTools.Core.Costing;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Serialization;
using TeraTools.Core.Text;
using TeraTools.Core.Units;
using TeraTools.Revit.Families;
using TeraTools.Revit.Infrastructure;
using TeraTools.Revit.Tools.Interiors;
using TeraTools.Revit.Tools.Materials;
using TeraTools.Revit.Ui;

namespace TeraTools.Revit.Tools.Quantities;

public sealed class QuantityScope
{
    public bool Hosts { get; set; } = true;
    public bool Families { get; set; } = true;
    public bool Boards { get; set; } = true;
    public bool RoomFinishes { get; set; }
}

/// <summary>Opções de abrangência do levantamento (herdadas pelos diálogos de quantitativos).</summary>
public abstract class ScopeOptions
{
    [Opt("Paredes, pisos, forros e coberturas (m²)", "Walls, floors, ceilings and roofs (m²)", Group = "Abrangência|Scope")]
    public bool Hosts { get; set; } = true;

    [Opt("Portas, janelas, mobiliário, luminárias e equipamentos (un)", "Doors, windows, furniture, fixtures and equipment (each)", Group = "Abrangência|Scope")]
    public bool Families { get; set; } = true;

    [Opt("Marcenaria: chapas por material (m²)", "Joinery: boards by material (m²)", Group = "Abrangência|Scope")]
    public bool Boards { get; set; } = true;

    [Opt("Acabamentos informados nos ambientes (piso, parede, forro, rodapé)", "Finishes entered in rooms (floor, wall, ceiling, base)", Group = "Abrangência|Scope",
        Tip = "Estimativa pelo ambiente: piso/forro = área; parede = perímetro × altura; rodapé = perímetro.|Room-based estimate: floor/ceiling = area; wall = perimeter × height; base = perimeter.")]
    public bool RoomFinishes { get; set; }

    public QuantityScope ToScope() => new() { Hosts = Hosts, Families = Families, Boards = Boards, RoomFinishes = RoomFinishes };
}

internal static class QuantityCollector
{
    private static readonly BuiltInCategory[] HostCats = { BuiltInCategory.OST_Walls, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Roofs };

    private static readonly BuiltInCategory[] FamilyCats =
    {
        BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows, BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems,
        BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_SpecialityEquipment,
        BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Casework,
    };

    public static List<QuantityRecord> Collect(Document doc, QuantityScope s)
    {
        var list = new List<QuantityRecord>();
        string RoomOf(Element e)
        {
            var tt = RevitGeo.Param(e, SharedParams.Ambiente)?.AsString();
            if (!string.IsNullOrWhiteSpace(tt)) return tt!;
            try
            {
                if (e is FamilyInstance fi && fi.Room is { } r0) return RoomUtil.Label(r0);
                var p = RevitGeo.Anchor(e);
                if (p != null && doc.GetRoomAtPoint(p + new XYZ(0, 0, 0.5)) is { } r) return RoomUtil.Label(r);
            }
            catch
            {
                // sem ambiente
            }
            return "";
        }
        string Code(Element e)
        {
            var c = RevitGeo.Param(e, SharedParams.Codigo)?.AsString();
            if (!string.IsNullOrWhiteSpace(c)) return c!;
            return doc.GetElement(e.GetTypeId()) is { } t ? RevitGeo.Text(t, BuiltInParameter.ALL_MODEL_TYPE_MARK) : "";
        }
        string FamType(Element e) => doc.GetElement(e.GetTypeId()) is ElementType t ? $"{t.FamilyName} : {t.Name}" : e.Name;
        if (s.Hosts)
        {
            foreach (var e in new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(HostCats.ToList())).WhereElementIsNotElementType())
            {
                var area = e.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0;
                if (area <= 0) continue;
                var mat = "";
                if (doc.GetElement(e.GetTypeId()) is HostObjAttributes ht && ht.GetCompoundStructure() is { } cs && cs.LayerCount > 0)
                {
                    var idx = cs.GetFirstCoreLayerIndex();
                    var mid = cs.GetMaterialId(Math.Max(0, idx));
                    mat = doc.GetElement(mid)?.Name ?? "";
                }
                list.Add(new QuantityRecord
                {
                    Room = RoomOf(e), Category = e.Category?.Name ?? "", Code = Code(e), FamilyType = FamType(e), Material = mat,
                    Description = FamType(e), Unit = "m²", Quantity = U.SqFtToM2(area), ElementIds = { e.Id.Value },
                });
            }
        }
        if (s.Families || s.Boards)
        {
            foreach (var e in new FilteredElementCollector(doc).WherePasses(new ElementMulticategoryFilter(FamilyCats.ToList())).WhereElementIsNotElementType().OfType<FamilyInstance>())
            {
                if (e.SuperComponent != null) continue;
                var isBoard = e.Symbol.FamilyName == FamilyFactory.BoardFamily;
                if (isBoard)
                {
                    if (!s.Boards) continue;
                    var dims = new[] { SharedParams.Largura, SharedParams.Profundidade, SharedParams.Altura }.Select(n => RevitGeo.Param(e, n)?.AsDouble() ?? 0).OrderBy(d => d).ToArray();
                    var mat = RevitGeo.Param(e, SharedParams.Material)?.AsElementId() is { } mid && doc.GetElement(mid) is Material m ? m.Name : "";
                    list.Add(new QuantityRecord
                    {
                        Room = RoomOf(e), Category = Loc.T("Marcenaria (chapas)", "Joinery (boards)"), Code = "CHAPA", FamilyType = $"{mat} {U.ToMm(dims[0]):0} mm".Trim(),
                        Material = mat, Description = Loc.F("Chapa {0} {1} mm", "Board {0} {1} mm", mat, Math.Round(U.ToMm(dims[0]))), Unit = "m²",
                        Quantity = U.SqFtToM2(dims[1] * dims[2]), ElementIds = { e.Id.Value },
                    });
                    continue;
                }
                if (!s.Families) continue;
                list.Add(new QuantityRecord
                {
                    Room = RoomOf(e), Category = e.Category?.Name ?? "", Code = Code(e), FamilyType = FamType(e), Material = "",
                    Description = FamType(e), Unit = "un", Quantity = 1, ElementIds = { e.Id.Value },
                });
            }
        }
        if (s.RoomFinishes)
        {
            foreach (var r in RevitGeo.Rooms(doc).Where(r => r.Area > 0))
            {
                var label = RoomUtil.Label(r);
                var areaM2 = U.SqFtToM2(r.Area);
                var perim = U.ToM(r.Perimeter);
                var h = U.ToM(r.UnboundedHeight > 0 ? r.UnboundedHeight : U.M(2.6));
                void F(BuiltInParameter bip, string kindPt, string kindEn, string unit, double q)
                {
                    var v = RevitGeo.Text(r, bip);
                    if (string.IsNullOrWhiteSpace(v) || q <= 0) return;
                    list.Add(new QuantityRecord { Room = label, Category = Loc.T(kindPt, kindEn), Code = v, FamilyType = v, Material = v, Description = v, Unit = unit, Quantity = q, ElementIds = { r.Id.Value } });
                }
                F(BuiltInParameter.ROOM_FINISH_FLOOR, "Acabamento de piso", "Floor finish", "m²", areaM2);
                F(BuiltInParameter.ROOM_FINISH_WALL, "Acabamento de parede", "Wall finish", "m²", perim * h);
                F(BuiltInParameter.ROOM_FINISH_CEILING, "Acabamento de forro", "Ceiling finish", "m²", areaM2);
                F(BuiltInParameter.ROOM_FINISH_BASE, "Rodapé", "Base", "m", perim);
            }
        }
        return list;
    }

    /// <summary>Agrupa registros iguais (ambiente, categoria, item, material, unidade).</summary>
    public static List<QuantityRecord> Aggregate(IEnumerable<QuantityRecord> records, bool byRoom) =>
        records.GroupBy(r => (Room: byRoom ? r.Room : "", r.Category, r.Code, r.FamilyType, r.Material, r.Unit, r.Description))
            .Select(g => new QuantityRecord
            {
                Room = g.Key.Room, Category = g.Key.Category, Code = g.Key.Code, FamilyType = g.Key.FamilyType, Material = g.Key.Material,
                Unit = g.Key.Unit, Description = g.Key.Description, Quantity = g.Sum(x => x.Quantity), ElementIds = g.SelectMany(x => x.ElementIds).ToList(),
            })
            .OrderBy(r => r.Room, TextUtil.NaturalComparer).ThenBy(r => r.Category).ThenBy(r => r.FamilyType).ToList();

    public static TableData Table(List<QuantityRecord> rows, string title, bool byRoom)
    {
        var t = new TableData(title);
        if (byRoom) t.AddColumn(Loc.T("Ambiente", "Room"));
        t.AddColumn(Loc.T("Categoria", "Category")).AddColumn(Loc.T("Código", "Code")).AddColumn(Loc.T("Item (família : tipo)", "Item (family : type)"))
         .AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Unid.", "Unit")).AddColumn(Loc.T("Quantidade", "Quantity"), ColumnKind.Number);
        foreach (var r in rows)
        {
            var cells = new List<object?>();
            if (byRoom) cells.Add(string.IsNullOrEmpty(r.Room) ? Loc.T("(sem ambiente)", "(no room)") : r.Room);
            cells.AddRange(new object?[] { r.Category, r.Code, r.FamilyType, r.Material, r.Unit, Math.Round(r.Quantity, 3) });
            t.AddRowWithIds(r.ElementIds, cells.ToArray());
        }
        return t;
    }

    public static string Save(Document doc, string suffix, params TableData[] tables)
    {
        var title = TextUtil.SafeFileName(string.IsNullOrWhiteSpace(doc.Title) ? "projeto" : doc.Title);
        var path = Path.Combine(Paths.Exports, $"{title} - {suffix} {DateTime.Now:yyyyMMdd-HHmm}.xlsx");
        XlsxWriter.Write(path, tables);
        return path;
    }
}

public sealed class QtySchedulesOptions
{
    [Opt("Ambientes (número, nome, área, acabamentos)", "Rooms (number, name, area, finishes)")] public bool Rooms { get; set; } = true;
    [Opt("Pisos por tipo", "Floors by type")] public bool Floors { get; set; } = true;
    [Opt("Paredes por tipo", "Walls by type")] public bool Walls { get; set; } = true;
    [Opt("Forros por tipo", "Ceilings by type")] public bool Ceilings { get; set; } = true;
    [Opt("Portas", "Doors")] public bool Doors { get; set; } = true;
    [Opt("Janelas", "Windows")] public bool Windows { get; set; } = true;
    [Opt("Mobiliário por ambiente", "Furniture by room")] public bool Furniture { get; set; } = true;
    [Opt("Luminárias por grupo/ambiente", "Fixtures by group/room")] public bool Lighting { get; set; } = true;
    [Opt("Marcenaria por módulo", "Joinery by module")] public bool Joinery { get; set; } = true;
    [Opt("Levantamento de materiais (pisos e paredes)", "Material takeoff (floors and walls)")] public bool Takeoff { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("qty.schedules", Modules.Quantities, "Tabelas de quantitativos", "Quantity schedules", Order = 10, Large = true,
    DescPt = "Cria de uma vez as tabelas de quantitativos do projeto de interiores: pisos, paredes, forros, portas, janelas, mobiliário, luminárias, marcenaria e materiais.",
    DescEn = "Creates the interior design quantity schedules at once: floors, walls, ceilings, doors, windows, furniture, fixtures, joinery and materials.",
    Keywords = "tabelas quantitativos levantamento pisos paredes portas schedules quantities takeoff")]
public sealed class QtySchedulesCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<QtySchedulesOptions>();
        var doc = ctx.Doc;
        ctx.Modify(ctx.Tool.Name, () =>
        {
            void Make(bool on, Func<ViewSchedule> create)
            {
                if (!on) return;
                try
                {
                    var vs = create();
                    ctx.Report.Created();
                    ctx.Report.AddResult(vs.Id.Value);
                }
                catch (Exception ex)
                {
                    ctx.Report.Warn(Shapes.FailText(ex));
                }
            }
            var famType = new Schedules.Spec("BIP:ELEM_FAMILY_AND_TYPE_PARAM");
            var count = new Schedules.Spec("#count", Loc.T("Quantidade", "Count"));
            Make(o.Rooms, () => Schedules.Create(doc, BuiltInCategory.OST_Rooms, Loc.T("TT Tabela de ambientes", "TT Room schedule"),
                new[]
                {
                    new Schedules.Spec("BIP:ROOM_NUMBER"), new Schedules.Spec("BIP:ROOM_NAME"), new Schedules.Spec("BIP:ROOM_LEVEL_ID"),
                    new Schedules.Spec("BIP:ROOM_AREA", Totals: true), new Schedules.Spec("BIP:ROOM_PERIMETER"), new Schedules.Spec("BIP:ROOM_DEPARTMENT"),
                    new Schedules.Spec("BIP:ROOM_FINISH_FLOOR"), new Schedules.Spec("BIP:ROOM_FINISH_WALL"), new Schedules.Spec("BIP:ROOM_FINISH_CEILING"),
                    new Schedules.Spec("BIP:ROOM_FINISH_BASE"),
                }, null, new[] { "BIP:ROOM_LEVEL_ID", "BIP:ROOM_NUMBER" }, report: ctx.Report));
            Make(o.Floors, () => Schedules.Create(doc, BuiltInCategory.OST_Floors, Loc.T("TT Pisos por tipo", "TT Floors by type"),
                new[] { famType, new Schedules.Spec("BIP:HOST_AREA_COMPUTED", Totals: true), count }, null, new[] { "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report));
            Make(o.Walls, () => Schedules.Create(doc, BuiltInCategory.OST_Walls, Loc.T("TT Paredes por tipo", "TT Walls by type"),
                new[] { famType, new Schedules.Spec("BIP:CURVE_ELEM_LENGTH", Totals: true), new Schedules.Spec("BIP:HOST_AREA_COMPUTED", Totals: true), count },
                null, new[] { "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report));
            Make(o.Ceilings, () => Schedules.Create(doc, BuiltInCategory.OST_Ceilings, Loc.T("TT Forros por tipo", "TT Ceilings by type"),
                new[] { famType, new Schedules.Spec("BIP:HOST_AREA_COMPUTED", Totals: true), count }, null, new[] { "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report));
            Make(o.Doors, () => Schedules.Create(doc, BuiltInCategory.OST_Doors, Loc.T("TT Portas", "TT Doors"),
                new[] { new Schedules.Spec("BIP:ALL_MODEL_MARK"), famType, new Schedules.Spec("BIP:DOOR_WIDTH"), new Schedules.Spec("BIP:DOOR_HEIGHT"), count },
                null, new[] { "BIP:ALL_MODEL_MARK" }, report: ctx.Report));
            Make(o.Windows, () => Schedules.Create(doc, BuiltInCategory.OST_Windows, Loc.T("TT Janelas", "TT Windows"),
                new[] { new Schedules.Spec("BIP:ALL_MODEL_MARK"), famType, new Schedules.Spec("BIP:WINDOW_WIDTH"), new Schedules.Spec("BIP:WINDOW_HEIGHT"), new Schedules.Spec("BIP:INSTANCE_SILL_HEIGHT_PARAM"), count },
                null, new[] { "BIP:ALL_MODEL_MARK" }, report: ctx.Report));
            if (o.Furniture) SharedParams.BindToProject(doc, new[] { SharedParams.Ambiente }, InteriorsUtil.FurnitureCats);
            Make(o.Furniture, () => Schedules.Create(doc, BuiltInCategory.OST_Furniture, Loc.T("TT Mobiliário", "TT Furniture"),
                new[] { new Schedules.Spec(SharedParams.Ambiente), famType, count }, null, new[] { SharedParams.Ambiente, "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report));
            if (o.Lighting) SharedParams.BindToProject(doc, new[] { SharedParams.Grupo, SharedParams.Ambiente }, new[] { BuiltInCategory.OST_LightingFixtures });
            Make(o.Lighting, () => Schedules.Create(doc, BuiltInCategory.OST_LightingFixtures, Loc.T("TT Luminárias", "TT Lighting fixtures"),
                new[] { new Schedules.Spec(SharedParams.Grupo), new Schedules.Spec(SharedParams.Ambiente), famType, count },
                null, new[] { SharedParams.Grupo, SharedParams.Ambiente, "BIP:ELEM_FAMILY_AND_TYPE_PARAM" }, itemized: false, report: ctx.Report));
            Make(o.Joinery, () => Schedules.Create(doc, BuiltInCategory.OST_Casework, Loc.T("TT Marcenaria por módulo", "TT Joinery by module"),
                new[] { new Schedules.Spec(SharedParams.Modulo, Loc.T("Módulo", "Module")), new Schedules.Spec(SharedParams.Material), count },
                null, new[] { SharedParams.Modulo, SharedParams.Material }, itemized: false, report: ctx.Report));
            foreach (var (bic, pt, en) in new[] { (BuiltInCategory.OST_Floors, "TT Materiais de pisos", "TT Floor materials"), (BuiltInCategory.OST_Walls, "TT Materiais de paredes", "TT Wall materials") })
                Make(o.Takeoff, () => Schedules.Configure(doc, ViewSchedule.CreateMaterialTakeoff(doc, new ElementId(bic)), Loc.T(pt, en),
                    new[] { new Schedules.Spec("BIP:MATERIAL_NAME"), new Schedules.Spec("BIP:MATERIAL_AREA", Totals: true) }, null, new[] { "BIP:MATERIAL_NAME" }, itemized: false, report: ctx.Report));
        });
    }
}

public sealed class QtyExportOptions : ScopeOptions
{
    [Opt("Detalhar por ambiente", "Break down by room")]
    public bool ByRoom { get; set; } = true;

    [Opt("Salvar planilha Excel", "Save Excel workbook")]
    public bool Save { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("qty.export", Modules.Quantities, "Levantamento de quantitativos", "Quantity takeoff", Order = 11,
    DescPt = "Levanta áreas e quantidades do modelo (sistemas, famílias, chapas de marcenaria e acabamentos dos ambientes), por ambiente, e salva em Excel.",
    DescEn = "Takes off areas and counts from the model (systems, families, joinery boards and room finishes), by room, and saves to Excel.",
    Keywords = "levantamento quantitativos exportar excel areas quantidades takeoff export quantities")]
public sealed class QtyExportCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<QtyExportOptions>();
        var doc = ctx.Doc;
        var records = QuantityCollector.Collect(doc, o.ToScope());
        var summary = QuantityCollector.Table(QuantityCollector.Aggregate(records, false), Loc.T("Resumo de quantitativos", "Quantity summary"), false);
        var tables = new List<TableData> { summary };
        if (o.ByRoom) tables.Add(QuantityCollector.Table(QuantityCollector.Aggregate(records, true), Loc.T("Quantitativos por ambiente", "Quantities by room"), true));
        ctx.Report.Tables.AddRange(tables);
        if (o.Save)
        {
            try
            {
                var path = QuantityCollector.Save(doc, Loc.T("quantitativos", "quantities"), tables.ToArray());
                ctx.Report.Success(Loc.F("Planilha salva: {0}", "Workbook saved: {0}", path));
            }
            catch (Exception ex)
            {
                ctx.Report.Error(ex.Message);
            }
        }
        ctx.Report.ForceShow = true;
    }
}

public enum BudgetAction
{
    [Text("Calcular orçamento com a tabela de preços", "Compute budget with the price table")] Compute,
    [Text("Gerar modelo de tabela de preços a partir do modelo", "Generate a price table template from the model")] Template,
}

public sealed class BudgetOptions : ScopeOptions
{
    public BudgetOptions() => RoomFinishes = true;

    [Opt("Ação", "Action")]
    public BudgetAction Action { get; set; } = BudgetAction.Compute;

    [Opt("Tabela de preços (CSV)", "Price table (CSV)", Kind = OptKind.File, Filter = "CSV|*.csv", VisibleIf = "Action=Compute",
        Tip = "Colunas: Codigo;Descricao;Unidade;CustoUnitario;Perda;Categoria;CasarPor;Padrao;MaoDeObra;Fornecedor. CasarPor: Codigo, FamiliaTipo, Material ou Categoria. Padrao aceita * como curinga.|Columns as in the generated template. Pattern accepts * wildcard.")]
    public string PriceTable { get; set; } = "";

    [Opt("Salvar planilha Excel", "Save Excel workbook")]
    public bool Save { get; set; } = true;
}

[Transaction(TransactionMode.Manual)]
[Tool("qty.budget", Modules.Quantities, "Orçamento", "Budget", Order = 12, Large = true,
    DescPt = "Cruza os quantitativos do modelo com a sua tabela de preços (com perdas e mão de obra) e gera o orçamento por item, ambiente e categoria.",
    DescEn = "Matches model quantities with your price table (with waste and labor) and produces the budget by item, room and category.",
    HelpPt = "1) Use \"Gerar modelo de tabela de preços\": uma planilha com os itens do seu projeto é criada. 2) Preencha CustoUnitario (e Perda %, MaoDeObra) no Excel. 3) Rode \"Calcular orçamento\" escolhendo essa planilha. Itens sem preço aparecem destacados.\n\nÉ uma estimativa: confira quantidades e preços antes de usar comercialmente.",
    HelpEn = "1) Use \"Generate a price table template\": a spreadsheet with your project's items is created. 2) Fill in UnitCost (and Loss %, Labor) in Excel. 3) Run \"Compute budget\" with that file. Items without price are flagged.\n\nIt is an estimate: check quantities and prices before commercial use.",
    Keywords = "orcamento custo precos tabela estimativa budget cost price estimate")]
public sealed class BudgetCommand : ToolCommand
{
    public override void Run(ToolContext ctx)
    {
        var o = ctx.Ask<BudgetOptions>();
        var doc = ctx.Doc;
        var records = QuantityCollector.Collect(doc, o.ToScope());
        if (o.Action == BudgetAction.Template)
        {
            var t = new TableData(Loc.T("Tabela de preços", "Price table"));
            foreach (var h in new[] { "Codigo", "Descricao", "Unidade", "CustoUnitario", "Perda", "Categoria", "CasarPor", "Padrao", "MaoDeObra", "Fornecedor" }) t.AddColumn(h);
            foreach (var g in records.GroupBy(r => (r.Category, r.FamilyType, r.Unit)).OrderBy(g => g.Key.Category).ThenBy(g => g.Key.FamilyType))
            {
                var first = g.First();
                t.AddRow(first.Code, first.Description, g.Key.Unit, "0", g.Key.Unit == "m²" ? "10" : "0", g.Key.Category, "FamiliaTipo", g.Key.FamilyType, "0", "");
            }
            var title = TextUtil.SafeFileName(string.IsNullOrWhiteSpace(doc.Title) ? "projeto" : doc.Title);
            var path = Path.Combine(Paths.Exports, $"{title} - tabela de precos.csv");
            CsvWriter.Write(t, path, ctx.Settings.CsvSeparator);
            ctx.Report.Tables.Add(t);
            ctx.Report.Success(Loc.F("Modelo salvo: {0}. Preencha os custos no Excel e rode \"Calcular orçamento\".", "Template saved: {0}. Fill in the costs in Excel and run \"Compute budget\".", path));
            MatLib.OpenFolder(path);
            ctx.Report.ForceShow = true;
            return;
        }
        if (!File.Exists(o.PriceTable)) ToolContext.Fail("Escolha a tabela de preços (CSV). Gere um modelo primeiro, se precisar.", "Choose the price table (CSV). Generate a template first if needed.");
        var table = PriceTable.FromCsv(File.ReadAllText(o.PriceTable), Path.GetFileNameWithoutExtension(o.PriceTable));
        var budget = BudgetCalculator.Calculate(QuantityCollector.Aggregate(records, true), table);
        var tables = new[] { budget.Detailed(), budget.ByRoom(), budget.ByCategory() };
        ctx.Report.Tables.AddRange(tables);
        var unpriced = budget.Lines.Where(l => !l.Priced).ToList();
        foreach (var g in unpriced.GroupBy(l => l.Description).Take(60))
            ctx.Report.Warn(Loc.F("Sem preço: {0}", "No price: {0}", g.Key), g.SelectMany(l => l.ElementIds).Take(200));
        ctx.Report.Info(Loc.F("Total estimado: {0} ({1} itens, {2} sem preço).", "Estimated total: {0} ({1} items, {2} without price).",
            U.FormatMoney(budget.Lines.Sum(l => l.Total)), budget.Lines.Count, unpriced.Count));
        if (o.Save)
        {
            try
            {
                ctx.Report.Success(Loc.F("Planilha salva: {0}", "Workbook saved: {0}", QuantityCollector.Save(doc, Loc.T("orcamento", "budget"), tables)));
            }
            catch (Exception ex)
            {
                ctx.Report.Error(ex.Message);
            }
        }
        ctx.Report.ForceShow = true;
    }
}

public sealed class SnapshotSection
{
    public Dictionary<string, List<QuantityRecord>> Snapshots { get; set; } = new();
}

public enum SnapshotAction
{
    [Text("Salvar o levantamento atual (referência)", "Save the current takeoff (reference)")] Save,
    [Text("Comparar o modelo atual com uma referência", "Compare the current model with a reference")] Compare,
}

public sealed class SnapshotOptions : ScopeOptions
{
    [Opt("Ação", "Action")]
    public SnapshotAction Action { get; set; } = SnapshotAction.Compare;

    [Opt("Nome da referência", "Reference name", VisibleIf = "Action=Save")]
    public string Name { get; set; } = "";
}

[Transaction(TransactionMode.Manual)]
[Tool("qty.snapshot", Modules.Quantities, "Comparar quantitativos (antes × depois)", "Compare quantities (before × after)", Order = 13,
    DescPt = "Salva levantamentos de referência no projeto e compara com o modelo atual, mostrando o que aumentou, diminuiu, entrou ou saiu.",
    DescEn = "Saves reference takeoffs in the project and compares with the current model, showing what increased, decreased, was added or removed.",
    Keywords = "comparar quantitativos versoes revisao diferenca snapshot compare quantities revision difference")]
public sealed class SnapshotCommand : ToolCommand
{
    private const string Key = "qty.snapshots";

    public override void Run(ToolContext ctx)
    {
        var doc = ctx.Doc;
        var o = ctx.Ask<SnapshotOptions>();
        var current = QuantityCollector.Aggregate(QuantityCollector.Collect(doc, o.ToScope()), true);
        var sec = ProjectStorage.GetSection<SnapshotSection>(doc, Key);
        if (o.Action == SnapshotAction.Save)
        {
            var name = string.IsNullOrWhiteSpace(o.Name) ? DateTime.Now.ToString("yyyy-MM-dd HH:mm") : o.Name.Trim();
            foreach (var r in current) r.ElementIds.Clear();
            sec.Snapshots[name] = current;
            ctx.Modify(ctx.Tool.Name, () => ProjectStorage.SetSection(doc, Key, sec));
            ctx.Report.Success(Loc.F("Referência \"{0}\" salva no projeto ({1} itens).", "Reference \"{0}\" saved in the project ({1} items).", name, current.Count));
            return;
        }
        if (sec.Snapshots.Count == 0) ToolContext.Fail("Nenhuma referência salva. Use \"Salvar o levantamento atual\" primeiro.", "No saved reference. Use \"Save the current takeoff\" first.");
        var chosen = Pick.One(sec.Snapshots.Keys.OrderByDescending(k => k).ToList(), k => k, ctx.Tool.Name, Loc.T("Comparar com qual referência?", "Compare with which reference?"), ctx.Owner);
        if (chosen == null) ctx.Cancel();
        var diff = QuantityDiff.Compare(sec.Snapshots[chosen!], current, 1e-3);
        ctx.Report.Tables.Add(diff);
        ctx.Report.Info(Loc.F("{0} linhas com diferença em relação a \"{1}\".", "{0} rows differ from \"{1}\".", diff.Rows.Count, chosen));
        ctx.Report.ForceShow = true;
    }
}
