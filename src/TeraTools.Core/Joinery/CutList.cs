using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Nesting;
using TeraTools.Core.Units;

namespace TeraTools.Core.Joinery;

/// <summary>Consolida peças de vários módulos em listas de corte, chapas e fitas.</summary>
public static class CutList
{
    public sealed class Row
    {
        public string Material { get; set; } = "";
        public double Thickness { get; set; }
        public double Length { get; set; }
        public double Width { get; set; }
        public string Edges { get; set; } = "";
        public bool HasGrain { get; set; }
        public int Quantity { get; set; }
        public List<string> Ids { get; } = new();
        public List<string> Names { get; } = new();
        public List<string> Modules { get; } = new();
        public double EdgeLength { get; set; }
    }

    public static List<Row> Aggregate(IEnumerable<(string Module, Board Board)> boards)
    {
        var rows = new Dictionary<string, Row>();
        foreach (var (module, b) in boards)
        {
            var key = $"{b.Material}|{Math.Round(U.ToMm(b.Thickness), 1)}|{Math.Round(U.ToMm(b.Length), 1)}|{Math.Round(U.ToMm(b.Width), 1)}|{b.EdgeCode}|{b.HasGrain}";
            if (!rows.TryGetValue(key, out var r))
            {
                rows[key] = r = new Row
                {
                    Material = b.Material,
                    Thickness = b.Thickness,
                    Length = b.Length,
                    Width = b.Width,
                    Edges = b.EdgeCode.Trim(),
                    HasGrain = b.HasGrain,
                };
            }
            r.Quantity += b.Quantity;
            r.Ids.Add(b.Id);
            if (!r.Names.Contains(b.Name)) r.Names.Add(b.Name);
            if (!r.Modules.Contains(module)) r.Modules.Add(module);
            r.EdgeLength += b.EdgeLength * b.Quantity;
        }
        return rows.Values.OrderBy(r => r.Material).ThenByDescending(r => r.Thickness).ThenByDescending(r => r.Length * r.Width).ToList();
    }

    public static TableData ToTable(List<Row> rows)
    {
        var t = new TableData(Loc.T("Lista de peças (plano de corte)", "Cut list"));
        t.AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Esp. (mm)", "Thk. (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Comp. (mm)", "Length (mm)"), ColumnKind.Number, 1).AddColumn(Loc.T("Larg. (mm)", "Width (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Qtd.", "Qty."), ColumnKind.Integer).AddColumn(Loc.T("Fitas", "Edges"))
         .AddColumn(Loc.T("Veio", "Grain")).AddColumn(Loc.T("Peças", "Parts")).AddColumn(Loc.T("Módulos", "Modules"))
         .AddColumn(Loc.T("Área (m²)", "Area (m²)"), ColumnKind.Number, 3);
        foreach (var r in rows)
            t.AddRow(r.Material, U.ToMm(r.Thickness), U.ToMm(r.Length), U.ToMm(r.Width), r.Quantity, r.Edges,
                r.HasGrain ? Loc.T("comprimento", "length") : "-", string.Join(", ", r.Names), string.Join(", ", r.Modules),
                U.SqFtToM2(r.Length * r.Width * r.Quantity));
        return t;
    }

    public static TableData SheetsByMaterial(List<Row> rows, double sheetAreaM2 = 2.75 * 1.85, double wastePercent = 15)
    {
        var t = new TableData(Loc.T("Chapas por material e espessura (estimativa)", "Sheets by material and thickness (estimate)"));
        t.AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Esp. (mm)", "Thk. (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Peças", "Parts"), ColumnKind.Integer).AddColumn(Loc.T("Área líquida (m²)", "Net area (m²)"), ColumnKind.Number)
         .AddColumn(Loc.T("Chapas estimadas", "Estimated sheets"), ColumnKind.Integer);
        foreach (var g in rows.GroupBy(r => (r.Material, T: Math.Round(U.ToMm(r.Thickness), 1))))
        {
            var area = U.SqFtToM2(g.Sum(r => r.Length * r.Width * r.Quantity));
            t.AddRow(g.Key.Material, g.Key.T, g.Sum(r => r.Quantity), area, (int)Math.Ceiling(area * (1 + wastePercent / 100) / sheetAreaM2 - 1e-9));
        }
        return t;
    }

    public static TableData EdgeBanding(List<Row> rows, double wastePercent)
    {
        var t = new TableData(Loc.T("Fitas de borda", "Edge banding"));
        t.AddColumn(Loc.T("Material", "Material")).AddColumn(Loc.T("Espessura chapa (mm)", "Board thk. (mm)"), ColumnKind.Number, 1)
         .AddColumn(Loc.T("Comprimento (m)", "Length (m)"), ColumnKind.Number).AddColumn(Loc.T("Com perdas (m)", "With waste (m)"), ColumnKind.Number);
        foreach (var g in rows.Where(r => r.EdgeLength > 0).GroupBy(r => (r.Material, T: Math.Round(U.ToMm(r.Thickness), 1))))
        {
            var len = U.ToM(g.Sum(r => r.EdgeLength));
            t.AddRow(g.Key.Material, g.Key.T, len, len * (1 + wastePercent / 100));
        }
        return t;
    }

    public static TableData Hardware(IEnumerable<(string Module, HardwareItem Item)> items)
    {
        var t = new TableData(Loc.T("Ferragens", "Hardware"));
        t.AddColumn(Loc.T("Item", "Item")).AddColumn(Loc.T("Especificação", "Specification"))
         .AddColumn(Loc.T("Quantidade", "Quantity"), ColumnKind.Integer).AddColumn(Loc.T("Comprimento total (m)", "Total length (m)"), ColumnKind.Number)
         .AddColumn(Loc.T("Módulos", "Modules"));
        foreach (var g in items.GroupBy(x => (x.Item.Kind, x.Item.Spec)).OrderBy(g => g.Key.Kind))
        {
            var len = g.Sum(x => x.Item.Unit == "m" ? x.Item.Length * x.Item.Quantity : 0);
            t.AddRow(JoineryNames.Hardware(g.Key.Kind), g.Key.Spec, g.Sum(x => x.Item.Quantity), len > 0 ? U.ToM(len) : null,
                string.Join(", ", g.Select(x => x.Module).Distinct()));
        }
        return t;
    }

    /// <summary>Converte a lista em pedidos para o otimizador de chapas.</summary>
    public static List<PartRequest> ToNesting(List<Row> rows) => rows.Select(r => new PartRequest
    {
        Id = string.Join(",", r.Ids.Take(3)) + (r.Ids.Count > 3 ? "…" : ""),
        Name = string.Join(", ", r.Names),
        Material = r.Material,
        Thickness = r.Thickness,
        Length = r.Length,
        Width = r.Width,
        Quantity = r.Quantity,
        HasGrain = r.HasGrain,
        Edges = r.Edges,
        Module = string.Join(", ", r.Modules),
    }).ToList();

    /// <summary>
    /// Exportação genérica para softwares de corte (colunas comuns: material, espessura, comprimento,
    /// largura, quantidade, veio, fitas). Ajuste o mapeamento conforme o software/máquina.
    /// </summary>
    public static TableData FabricationExport(List<Row> rows)
    {
        var t = new TableData("Pecas");
        t.AddColumn("Material").AddColumn("Espessura", ColumnKind.Number, 1).AddColumn("Comprimento", ColumnKind.Number, 1)
         .AddColumn("Largura", ColumnKind.Number, 1).AddColumn("Quantidade", ColumnKind.Integer).AddColumn("Veio")
         .AddColumn("FitaC1").AddColumn("FitaC2").AddColumn("FitaL1").AddColumn("FitaL2").AddColumn("Descricao").AddColumn("Modulo");
        foreach (var r in rows)
        {
            var e = r.Edges.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
            t.AddRow(r.Material, U.ToMm(r.Thickness), U.ToMm(r.Length), U.ToMm(r.Width), r.Quantity, r.HasGrain ? "1" : "0",
                e.Contains("C1") ? "1" : "0", e.Contains("C2") ? "1" : "0", e.Contains("L1") ? "1" : "0", e.Contains("L2") ? "1" : "0",
                string.Join(" / ", r.Names), string.Join(" / ", r.Modules));
        }
        return t;
    }
}
