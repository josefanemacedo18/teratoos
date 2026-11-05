using System.Globalization;
using TeraTools.Core.Colors;
using TeraTools.Core.Export;
using TeraTools.Core.Localization;
using TeraTools.Core.Text;
using TeraTools.Core.Tiling;
using TeraTools.Core.Units;

namespace TeraTools.Core.Finishes;

/// <summary>Tipo do acabamento (o que ele é).</summary>
public enum FinishKind
{
    WoodFloor,
    Laminate,
    Vinyl,
    Porcelain,
    Ceramic,
    NaturalStone,
    Cementitious,
    Mosaic,
    Carpet,
    Baseboard,
    Paint,
    Wallpaper,
    WallPanel,
    Ceiling,
    Joinery,
    Countertop,
    Metal,
    Glass,
    Fabric,
    Other,
}

/// <summary>Onde o acabamento é aplicado (define o que o plugin cria no Revit).</summary>
public enum FinishUse
{
    Floor,
    Wall,
    Ceiling,
    Baseboard,
    Countertop,
    Joinery,
    Other,
}

public enum PriceUnit
{
    SquareMeter,
    Meter,
    Unit,
    Box,
    Liter,
}

/// <summary>
/// Um acabamento do catálogo do projeto. As medidas ficam em MILÍMETROS (como na planilha),
/// e são convertidas para as unidades internas só na hora de usar.
/// </summary>
public sealed class FinishItem
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public FinishKind Kind { get; set; } = FinishKind.Porcelain;
    public FinishUse Use { get; set; } = FinishUse.Floor;
    public string Supplier { get; set; } = "";
    public string Line { get; set; } = "";
    public string ColorHex { get; set; } = "#C8BBA8";
    public string Surface { get; set; } = "";

    public double LengthMm { get; set; }
    public double WidthMm { get; set; }
    public double ThicknessMm { get; set; }
    public double JointMm { get; set; }
    public string JointColorHex { get; set; } = "";

    public TileLayout Layout { get; set; } = TileLayout.Grid;
    public double BondOffset { get; set; } = 0.5;
    public double RotationDeg { get; set; }
    public double ChevronAngleDeg { get; set; } = 45;
    public double MinPlankRatio { get; set; } = 0.4;

    /// <summary>Quantidade de tonalidades (1 = uniforme). Madeira e pedra costumam ter 3 a 5.</summary>
    public int Tones { get; set; } = 1;

    /// <summary>Variação de claro/escuro entre as tonalidades (%).</summary>
    public double ToneVariation { get; set; } = 8;

    public double LossPercent { get; set; } = 10;
    public double Price { get; set; }
    public PriceUnit PriceUnit { get; set; } = PriceUnit.SquareMeter;
    public double PiecesPerBox { get; set; }
    public double M2PerBox { get; set; }
    public string Rooms { get; set; } = "";
    public string Notes { get; set; } = "";
    public string TexturePath { get; set; } = "";

    /// <summary>Nome do material no Revit.</summary>
    public string MaterialName => $"{Code} - {Name}".Trim(' ', '-');

    /// <summary>Nome dos tipos (piso/parede/forro) criados para o item.</summary>
    public string TypeName => "TT " + MaterialName;

    public bool HasPieces => LengthMm > 0 && WidthMm > 0;

    public double Length => U.Mm(LengthMm);
    public double Width => U.Mm(WidthMm);
    public double Thickness => U.Mm(ThicknessMm);
    public double Joint => U.Mm(JointMm);

    public FinishItem Clone() => (FinishItem)MemberwiseClone();

    /// <summary>Cores das tonalidades: a cor base e variações mais claras/escuras, sempre na mesma ordem.</summary>
    public List<RgbColor> ToneColors()
    {
        var baseColor = RgbColor.TryParse(ColorHex, out var c) ? c : RgbColor.Gray;
        var n = Math.Clamp(Tones, 1, 9);
        var list = new List<RgbColor> { baseColor };
        var step = Math.Clamp(ToneVariation, 0, 60) / 100.0;
        for (var i = 1; i < n; i++)
        {
            // alterna claro/escuro: +1, -1, +2, -2...
            var k = (i + 1) / 2 * (i % 2 == 1 ? 1 : -1);
            list.Add(Shade(baseColor, k * step / Math.Max(1, (n - 1) / 2.0)));
        }
        return list;
    }

    private static RgbColor Shade(RgbColor c, double amount)
    {
        byte F(byte v) => (byte)Math.Clamp(Math.Round(amount >= 0 ? v + (255 - v) * amount : v * (1 + amount)), 0, 255);
        return new RgbColor(F(c.R), F(c.G), F(c.B));
    }

    /// <summary>Configurações de paginação a partir do item.</summary>
    public TileSettings ToTileSettings() => new()
    {
        TileLength = Length,
        TileWidth = Width,
        Joint = Joint,
        Layout = Layout,
        BondOffset = BondOffset,
        RotationDeg = RotationDeg,
        ChevronAngleDeg = ChevronAngleDeg,
        MinPlankRatio = MinPlankRatio,
        LossPercent = LossPercent,
        TilesPerBox = (int)Math.Round(PiecesPerBox),
    };
}

/// <summary>Rótulos em português dos campos do catálogo (planilha e interface).</summary>
public static class FinishLabels
{
    public static readonly (FinishKind Value, string Pt)[] Kinds =
    {
        (FinishKind.WoodFloor, "Piso de madeira"),
        (FinishKind.Laminate, "Piso laminado"),
        (FinishKind.Vinyl, "Piso vinílico"),
        (FinishKind.Porcelain, "Porcelanato"),
        (FinishKind.Ceramic, "Cerâmica"),
        (FinishKind.NaturalStone, "Pedra natural"),
        (FinishKind.Cementitious, "Cimentício (cimento queimado, microcimento)"),
        (FinishKind.Mosaic, "Pastilha / mosaico"),
        (FinishKind.Carpet, "Carpete"),
        (FinishKind.Baseboard, "Rodapé"),
        (FinishKind.Paint, "Pintura"),
        (FinishKind.Wallpaper, "Papel de parede"),
        (FinishKind.WallPanel, "Painel / revestimento de parede"),
        (FinishKind.Ceiling, "Forro"),
        (FinishKind.Joinery, "Marcenaria (MDF, laminado, lâmina)"),
        (FinishKind.Countertop, "Tampo / bancada"),
        (FinishKind.Metal, "Metal"),
        (FinishKind.Glass, "Vidro / espelho"),
        (FinishKind.Fabric, "Tecido / estofado"),
        (FinishKind.Other, "Outro"),
    };

    public static readonly (FinishUse Value, string Pt)[] Uses =
    {
        (FinishUse.Floor, "Piso"),
        (FinishUse.Wall, "Parede"),
        (FinishUse.Ceiling, "Forro"),
        (FinishUse.Baseboard, "Rodapé"),
        (FinishUse.Countertop, "Bancada"),
        (FinishUse.Joinery, "Marcenaria"),
        (FinishUse.Other, "Outro"),
    };

    public static readonly (PriceUnit Value, string Pt)[] Units =
    {
        (PriceUnit.SquareMeter, "m²"),
        (PriceUnit.Meter, "m"),
        (PriceUnit.Unit, "unidade"),
        (PriceUnit.Box, "caixa"),
        (PriceUnit.Liter, "litro"),
    };

    public static readonly (TileLayout Value, string Pt)[] Layouts =
    {
        (TileLayout.Grid, "Alinhada"),
        (TileLayout.RunningBond, "Amarração"),
        (TileLayout.Diagonal, "Diagonal"),
        (TileLayout.Herringbone, "Espinha de peixe"),
        (TileLayout.Basketweave, "Trama"),
        (TileLayout.ModularThreeSizes, "Modular 3 tamanhos"),
        (TileLayout.ModularPythagorean, "Modular pitagórico"),
        (TileLayout.Chevron, "Chevron"),
        (TileLayout.Hexagon, "Hexágono"),
        (TileLayout.OctagonDot, "Octógono com taco"),
        (TileLayout.OpusRomano, "Opus romano"),
        (TileLayout.PlankRandom, "Madeira aleatória"),
        (TileLayout.Rhombus3D, "Losangos 3D"),
    };

    public static string Label(FinishKind v) => Kinds.FirstOrDefault(x => x.Value == v).Pt ?? v.ToString();
    public static string Label(FinishUse v) => Uses.FirstOrDefault(x => x.Value == v).Pt ?? v.ToString();
    public static string Label(PriceUnit v) => Units.FirstOrDefault(x => x.Value == v).Pt ?? v.ToString();
    public static string Label(TileLayout v) => Layouts.FirstOrDefault(x => x.Value == v).Pt ?? v.ToString();

    /// <summary>Lê um rótulo (com ou sem acento, ou o nome interno) para o valor do enum.</summary>
    public static bool TryParse<T>((T Value, string Pt)[] table, string? text, out T value) where T : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var n = TextUtil.NormalizeForCompare(text);
        foreach (var (v, pt) in table)
        {
            var p = TextUtil.NormalizeForCompare(pt);
            if (p == n || p.StartsWith(n + " ", StringComparison.Ordinal) || TextUtil.NormalizeForCompare(v.ToString()) == n)
            {
                value = v;
                return true;
            }
        }
        // aceita o começo do rótulo ("pedra" → Pedra natural)
        foreach (var (v, pt) in table)
        {
            if (TextUtil.NormalizeForCompare(pt).StartsWith(n, StringComparison.Ordinal))
            {
                value = v;
                return true;
            }
        }
        return Enum.TryParse(text.Trim(), true, out value);
    }

    /// <summary>Aplicação natural de cada tipo de acabamento.</summary>
    public static FinishUse DefaultUse(FinishKind k) => k switch
    {
        FinishKind.Baseboard => FinishUse.Baseboard,
        FinishKind.Paint or FinishKind.Wallpaper or FinishKind.WallPanel => FinishUse.Wall,
        FinishKind.Ceiling => FinishUse.Ceiling,
        FinishKind.Joinery => FinishUse.Joinery,
        FinishKind.Countertop => FinishUse.Countertop,
        FinishKind.Metal or FinishKind.Glass or FinishKind.Fabric or FinishKind.Other => FinishUse.Other,
        _ => FinishUse.Floor,
    };

    /// <summary>Prefixo do código automático (PI-01, PA-01...).</summary>
    public static string Prefix(FinishUse u) => u switch
    {
        FinishUse.Floor => "PI",
        FinishUse.Wall => "PA",
        FinishUse.Ceiling => "FO",
        FinishUse.Baseboard => "RO",
        FinishUse.Countertop => "BA",
        FinishUse.Joinery => "MA",
        _ => "AC",
    };
}

/// <summary>Catálogo de acabamentos do projeto (fica dentro do .rvt; importa e exporta CSV).</summary>
public sealed class FinishCatalog
{
    public const string SectionKey = "finishCatalog";

    public List<FinishItem> Items { get; set; } = new();

    public FinishItem? Find(string? codeOrName)
    {
        if (string.IsNullOrWhiteSpace(codeOrName)) return null;
        var n = TextUtil.NormalizeForCompare(codeOrName);
        return Items.FirstOrDefault(i => TextUtil.NormalizeForCompare(i.Code) == n)
               ?? Items.FirstOrDefault(i => TextUtil.NormalizeForCompare(i.MaterialName) == n)
               ?? Items.FirstOrDefault(i => TextUtil.NormalizeForCompare(i.Name) == n);
    }

    /// <summary>Próximo código livre para a aplicação (ex.: PI-03).</summary>
    public string NewCode(FinishUse use)
    {
        var prefix = FinishLabels.Prefix(use);
        for (var i = 1; i < 1000; i++)
        {
            var code = $"{prefix}-{i:00}";
            if (Items.All(x => !string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase))) return code;
        }
        return prefix + "-" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
    }

    /// <summary>Problemas que impedem salvar (códigos repetidos, nomes vazios, medidas negativas).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        foreach (var g in Items.Where(i => !string.IsNullOrWhiteSpace(i.Code)).GroupBy(i => i.Code.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add(Loc.F("O código \"{0}\" aparece {1} vezes.", "Code \"{0}\" appears {1} times.", g.Key, g.Count()));
        foreach (var i in Items)
        {
            var who = string.IsNullOrWhiteSpace(i.Code) ? i.Name : i.Code;
            if (string.IsNullOrWhiteSpace(i.Code)) errors.Add(Loc.F("\"{0}\" está sem código.", "\"{0}\" has no code.", i.Name));
            if (string.IsNullOrWhiteSpace(i.Name)) errors.Add(Loc.F("\"{0}\" está sem nome.", "\"{0}\" has no name.", who));
            if (i.LengthMm < 0 || i.WidthMm < 0 || i.ThicknessMm < 0 || i.JointMm < 0)
                errors.Add(Loc.F("\"{0}\": medidas não podem ser negativas.", "\"{0}\": sizes cannot be negative.", who));
            if (i.LossPercent < 0 || i.LossPercent > 100)
                errors.Add(Loc.F("\"{0}\": perda deve ficar entre 0 e 100%.", "\"{0}\": loss must be 0–100%.", who));
            if (!string.IsNullOrWhiteSpace(i.ColorHex) && !RgbColor.TryParse(i.ColorHex, out _))
                errors.Add(Loc.F("\"{0}\": cor inválida ({1}). Use o formato #RRGGBB.", "\"{0}\": invalid color ({1}). Use #RRGGBB.", who, i.ColorHex));
        }
        return errors;
    }

    // ------------------------------------------------------------------ Planilha (CSV)

    public static readonly string[] Columns =
    {
        "Código", "Nome", "Tipo", "Aplicação", "Fornecedor", "Linha / referência", "Cor", "Acabamento",
        "Comprimento (mm)", "Largura (mm)", "Espessura (mm)", "Junta (mm)", "Cor da junta",
        "Paginação", "Amarração (0–1)", "Rotação (°)", "Ângulo do chevron (°)", "Menor régua (fração)", "Tonalidades", "Variação de tom (%)",
        "Perda (%)", "Preço (R$)", "Unidade do preço", "Peças por caixa", "m² por caixa",
        "Ambientes", "Observações", "Textura",
    };

    public TableData ToTable()
    {
        var t = new TableData(Loc.T("Catálogo de acabamentos", "Finish catalog"));
        foreach (var c in Columns) t.AddColumn(c);
        foreach (var i in Items)
            t.AddRow(i.Code, i.Name, FinishLabels.Label(i.Kind), FinishLabels.Label(i.Use), i.Supplier, i.Line, i.ColorHex, i.Surface,
                N(i.LengthMm), N(i.WidthMm), N(i.ThicknessMm), N(i.JointMm), i.JointColorHex,
                FinishLabels.Label(i.Layout), N(i.BondOffset), N(i.RotationDeg), N(i.ChevronAngleDeg), N(i.MinPlankRatio), i.Tones.ToString(CultureInfo.InvariantCulture), N(i.ToneVariation),
                N(i.LossPercent), N(i.Price), FinishLabels.Label(i.PriceUnit), N(i.PiecesPerBox), N(i.M2PerBox),
                i.Rooms, i.Notes, i.TexturePath);
        return t;
    }

    private static string N(double v) => v == 0 ? "" : v.ToString("0.###", Loc.Culture);

    /// <summary>
    /// Importa uma planilha CSV. Linhas com código existente ATUALIZAM o item (só as colunas preenchidas);
    /// códigos novos criam itens. Linhas sem código recebem um código automático.
    /// </summary>
    public (int Added, int Updated, List<string> Errors) ImportCsv(string csvText)
    {
        var errors = new List<string>();
        var rows = CsvWriter.Parse(csvText);
        if (rows.Count < 2)
        {
            errors.Add(Loc.T("A planilha não tem linhas de dados.", "The sheet has no data rows."));
            return (0, 0, errors);
        }
        var head = rows[0].Select(TextUtil.NormalizeForCompare).ToArray();
        int Col(string name)
        {
            var n = TextUtil.NormalizeForCompare(name);
            var i = Array.FindIndex(head, h => h == n);
            if (i >= 0) return i;
            // aceita cabeçalho sem a unidade: "Comprimento" para "Comprimento (mm)"
            var shortName = TextUtil.NormalizeForCompare(name.Split('(')[0]);
            return Array.FindIndex(head, h => TextUtil.NormalizeForCompare(h.Split('(')[0]) == shortName);
        }
        if (Col("Nome") < 0) errors.Add(Loc.T("A planilha precisa da coluna \"Nome\".", "The sheet needs a \"Nome\" column."));
        if (errors.Count > 0) return (0, 0, errors);

        int added = 0, updated = 0;
        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            string? Get(string name)
            {
                var i = Col(name);
                return i >= 0 && i < row.Length && !string.IsNullOrWhiteSpace(row[i]) ? row[i].Trim() : null;
            }
            double? Num(string name)
            {
                var s = Get(name);
                if (s == null) return null;
                if (U.TryParseNumber(s.Replace("R$", "").Replace("%", "").Trim(), out var v)) return v;
                errors.Add(Loc.F("Linha {0}: \"{1}\" não é um número ({2}).", "Row {0}: \"{1}\" is not a number ({2}).", r + 1, s, name));
                return null;
            }
            var name = Get("Nome");
            if (name == null) continue;
            var code = Get("Código");
            var item = code != null ? Find(code) : null;
            var isNew = item == null;
            item ??= new FinishItem { Name = name };
            item.Name = name;
            if (Get("Tipo") is { } k)
            {
                if (FinishLabels.TryParse(FinishLabels.Kinds, k, out var kind)) item.Kind = kind;
                else errors.Add(Loc.F("Linha {0}: tipo \"{1}\" desconhecido.", "Row {0}: unknown type \"{1}\".", r + 1, k));
            }
            if (Get("Aplicação") is { } u)
            {
                if (FinishLabels.TryParse(FinishLabels.Uses, u, out var use)) item.Use = use;
                else errors.Add(Loc.F("Linha {0}: aplicação \"{1}\" desconhecida.", "Row {0}: unknown use \"{1}\".", r + 1, u));
            }
            else if (isNew)
            {
                item.Use = FinishLabels.DefaultUse(item.Kind);
            }
            item.Supplier = Get("Fornecedor") ?? item.Supplier;
            item.Line = Get("Linha / referência") ?? Get("Linha") ?? item.Line;
            if (Get("Cor") is { } hex)
            {
                if (RgbColor.TryParse(hex, out var c)) item.ColorHex = c.Hex;
                else errors.Add(Loc.F("Linha {0}: cor \"{1}\" inválida (use #RRGGBB).", "Row {0}: invalid color \"{1}\".", r + 1, hex));
            }
            item.Surface = Get("Acabamento") ?? item.Surface;
            item.LengthMm = Num("Comprimento (mm)") ?? item.LengthMm;
            item.WidthMm = Num("Largura (mm)") ?? item.WidthMm;
            item.ThicknessMm = Num("Espessura (mm)") ?? item.ThicknessMm;
            item.JointMm = Num("Junta (mm)") ?? item.JointMm;
            if (Get("Cor da junta") is { } jhex && RgbColor.TryParse(jhex, out var jc)) item.JointColorHex = jc.Hex;
            if (Get("Paginação") is { } lay)
            {
                if (FinishLabels.TryParse(FinishLabels.Layouts, lay, out var layout)) item.Layout = layout;
                else errors.Add(Loc.F("Linha {0}: paginação \"{1}\" desconhecida.", "Row {0}: unknown layout \"{1}\".", r + 1, lay));
            }
            item.BondOffset = Num("Amarração (0–1)") ?? item.BondOffset;
            item.RotationDeg = Num("Rotação (°)") ?? item.RotationDeg;
            item.ChevronAngleDeg = Num("Ângulo do chevron (°)") ?? item.ChevronAngleDeg;
            item.MinPlankRatio = Num("Menor régua (fração)") ?? item.MinPlankRatio;
            if (Num("Tonalidades") is { } tones) item.Tones = (int)Math.Clamp(Math.Round(tones), 1, 9);
            item.ToneVariation = Num("Variação de tom (%)") ?? item.ToneVariation;
            item.LossPercent = Num("Perda (%)") ?? item.LossPercent;
            item.Price = Num("Preço (R$)") ?? item.Price;
            if (Get("Unidade do preço") is { } pu && FinishLabels.TryParse(FinishLabels.Units, pu, out var unit)) item.PriceUnit = unit;
            item.PiecesPerBox = Num("Peças por caixa") ?? item.PiecesPerBox;
            item.M2PerBox = Num("m² por caixa") ?? item.M2PerBox;
            item.Rooms = Get("Ambientes") ?? item.Rooms;
            item.Notes = Get("Observações") ?? item.Notes;
            item.TexturePath = Get("Textura") ?? item.TexturePath;
            if (isNew)
            {
                item.Code = code ?? NewCode(item.Use);
                Items.Add(item);
                added++;
            }
            else
            {
                updated++;
            }
        }
        return (added, updated, errors);
    }

    /// <summary>Exemplos para começar (o usuário edita, apaga ou acrescenta).</summary>
    public static FinishCatalog Starter() => new()
    {
        Items =
        {
            new FinishItem { Code = "PI-01", Name = "Porcelanato acetinado 90×90", Kind = FinishKind.Porcelain, Use = FinishUse.Floor, ColorHex = "#D9D2C5", Surface = "Acetinado", LengthMm = 900, WidthMm = 900, ThicknessMm = 10, JointMm = 2, JointColorHex = "#BDB5A8", Layout = TileLayout.Grid, LossPercent = 10, PriceUnit = PriceUnit.SquareMeter, M2PerBox = 1.62, PiecesPerBox = 2 },
            new FinishItem { Code = "PI-02", Name = "Madeira em espinha de peixe", Kind = FinishKind.WoodFloor, Use = FinishUse.Floor, ColorHex = "#A47148", Surface = "Natural", LengthMm = 600, WidthMm = 100, ThicknessMm = 15, JointMm = 0.5, Layout = TileLayout.Herringbone, Tones = 4, ToneVariation = 12, LossPercent = 15, PriceUnit = PriceUnit.SquareMeter },
            new FinishItem { Code = "PI-05", Name = "Carvalho em chevron 45°", Kind = FinishKind.WoodFloor, Use = FinishUse.Floor, ColorHex = "#C49A6C", Surface = "Natural fosco", LengthMm = 600, WidthMm = 120, ThicknessMm = 15, JointMm = 0.5, Layout = TileLayout.Chevron, ChevronAngleDeg = 45, Tones = 4, ToneVariation = 10, LossPercent = 15, PriceUnit = PriceUnit.SquareMeter },
            new FinishItem { Code = "PI-06", Name = "Ladrilho hexagonal 20 cm", Kind = FinishKind.Ceramic, Use = FinishUse.Floor, ColorHex = "#8FA7A0", Surface = "Fosco", LengthMm = 200, WidthMm = 200, ThicknessMm = 8, JointMm = 3, JointColorHex = "#E8E4DC", Layout = TileLayout.Hexagon, Tones = 2, ToneVariation = 6, LossPercent = 12 },
            new FinishItem { Code = "PI-03", Name = "Piso vinílico régua", Kind = FinishKind.Vinyl, Use = FinishUse.Floor, ColorHex = "#B89B7A", LengthMm = 1220, WidthMm = 180, ThicknessMm = 3, JointMm = 0.5, Layout = TileLayout.PlankRandom, MinPlankRatio = 0.4, Tones = 3, ToneVariation = 8, LossPercent = 8 },
            new FinishItem { Code = "PI-04", Name = "Mármore travertino", Kind = FinishKind.NaturalStone, Use = FinishUse.Floor, ColorHex = "#E4D8C4", Surface = "Levigado", LengthMm = 600, WidthMm = 400, ThicknessMm = 20, JointMm = 1, Layout = TileLayout.RunningBond, BondOffset = 0.5, Tones = 3, ToneVariation = 6, LossPercent = 12 },
            new FinishItem { Code = "RO-01", Name = "Rodapé poliestireno branco 7 cm", Kind = FinishKind.Baseboard, Use = FinishUse.Baseboard, ColorHex = "#F2F2F0", WidthMm = 70, ThicknessMm = 15, LossPercent = 5, PriceUnit = PriceUnit.Meter },
            new FinishItem { Code = "PA-01", Name = "Pintura acrílica fosca branco gelo", Kind = FinishKind.Paint, Use = FinishUse.Wall, ColorHex = "#EEEDE7", Surface = "Fosco", LossPercent = 5, PriceUnit = PriceUnit.SquareMeter },
            new FinishItem { Code = "PA-02", Name = "Revestimento metrô 7,5×15", Kind = FinishKind.Ceramic, Use = FinishUse.Wall, ColorHex = "#F4F4F2", Surface = "Brilhante", LengthMm = 150, WidthMm = 75, ThicknessMm = 7, JointMm = 2, JointColorHex = "#D0D0D0", Layout = TileLayout.RunningBond, BondOffset = 0.5, LossPercent = 10 },
            new FinishItem { Code = "FO-01", Name = "Forro de gesso liso pintado", Kind = FinishKind.Ceiling, Use = FinishUse.Ceiling, ColorHex = "#FAFAF8", ThicknessMm = 12.5, LossPercent = 5 },
        },
    };
}
