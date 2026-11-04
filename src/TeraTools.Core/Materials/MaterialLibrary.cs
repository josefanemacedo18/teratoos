using TeraTools.Core.Colors;
using TeraTools.Core.Localization;

namespace TeraTools.Core.Materials;

/// <summary>Especificação de material/acabamento (biblioteca).</summary>
public sealed class MaterialSpec
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string Category { get; set; } = "";
    public string ColorHex { get; set; } = "#BFBFBF";
    public string? Manufacturer { get; set; }
    public string? Reference { get; set; }
    public string? Finish { get; set; }
    public string? Description { get; set; }
    public string? Supplier { get; set; }
    public double? UnitCost { get; set; }
    public string? Unit { get; set; }
    public int Transparency { get; set; }
    public int Shininess { get; set; } = 20;

    /// <summary>Elemento de aplicação sugerido (piso, parede, forro, marcenaria...).</summary>
    public string? Use { get; set; }

    public RgbColor Color => RgbColor.TryParse(ColorHex, out var c) ? c : RgbColor.Gray;
}

public sealed class MaterialLibrary
{
    public string Name { get; set; } = "";
    public List<MaterialSpec> Items { get; set; } = new();

    public MaterialSpec? Find(string name) =>
        Items.FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Items.FirstOrDefault(m => !string.IsNullOrEmpty(m.Code) && string.Equals(m.Code, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Biblioteca inicial genérica de acabamentos de interiores (sem marcas), para começar a usar.</summary>
    public static MaterialLibrary Starter()
    {
        var lib = new MaterialLibrary { Name = Loc.T("Biblioteca inicial Téra", "Téra starter library") };
        void A(string code, string name, string cat, string hex, string use, string finish, string unit, int shine = 20, int transp = 0) =>
            lib.Items.Add(new MaterialSpec { Code = code, Name = name, Category = cat, ColorHex = hex, Use = use, Finish = finish, Unit = unit, Shininess = shine, Transparency = transp });

        A("PS-01", "TT Porcelanato Cinza Claro 60x60", "Revestimento", "#C9C7C2", "Piso", "Acetinado", "m²", 35);
        A("PS-02", "TT Porcelanato Bege 90x90", "Revestimento", "#D8CBB5", "Piso", "Polido", "m²", 70);
        A("PS-03", "TT Porcelanato Amadeirado 20x120", "Revestimento", "#A57C55", "Piso", "Fosco", "m²", 15);
        A("PS-04", "TT Vinílico Carvalho", "Revestimento", "#B48A62", "Piso", "Fosco", "m²", 10);
        A("PS-05", "TT Cimento Queimado", "Revestimento", "#9D9D99", "Piso", "Fosco", "m²", 15);
        A("PR-01", "TT Revestimento Branco 30x60", "Revestimento", "#F2F1EE", "Parede", "Brilhante", "m²", 70);
        A("PR-02", "TT Subway Branco 7,5x15", "Revestimento", "#F5F5F2", "Parede", "Brilhante", "m²", 75);
        A("PR-03", "TT Pastilha Verde Sálvia", "Revestimento", "#8FA38A", "Parede", "Brilhante", "m²", 60);
        A("PT-01", "TT Pintura Branco Neve", "Pintura", "#F7F7F4", "Parede/Forro", "Fosco", "m²", 5);
        A("PT-02", "TT Pintura Off-White", "Pintura", "#EFEADF", "Parede", "Fosco", "m²", 5);
        A("PT-03", "TT Pintura Cinza Urbano", "Pintura", "#8C8D8B", "Parede", "Acetinado", "m²", 12);
        A("PT-04", "TT Pintura Terracota", "Pintura", "#B4674A", "Parede", "Fosco", "m²", 5);
        A("PT-05", "TT Pintura Azul Petróleo", "Pintura", "#2F5560", "Parede", "Fosco", "m²", 5);
        A("MD-01", "TT MDF Branco TX 15", "Madeira", "#F1F1EE", "Marcenaria", "Texturizado", "m²", 15);
        A("MD-02", "TT MDF Freijó 18", "Madeira", "#B88A5A", "Marcenaria", "Natural", "m²", 20);
        A("MD-03", "TT MDF Carvalho Malva 18", "Madeira", "#9C7B5E", "Marcenaria", "Natural", "m²", 20);
        A("MD-04", "TT MDF Grafite 18", "Madeira", "#4A4B4D", "Marcenaria", "Fosco", "m²", 10);
        A("MD-05", "TT MDF Verde Oliva 18", "Madeira", "#6E7253", "Marcenaria", "Fosco", "m²", 10);
        A("MD-06", "TT Lâmina Natural Nogueira", "Madeira", "#6F4E37", "Marcenaria", "Verniz fosco", "m²", 25);
        A("PD-01", "TT Quartzo Branco", "Pedra", "#ECECE8", "Bancada", "Polido", "m²", 80);
        A("PD-02", "TT Granito Preto Absoluto", "Pedra", "#1E1E1F", "Bancada", "Polido", "m²", 85);
        A("PD-03", "TT Mármore Carrara", "Pedra", "#E7E6E2", "Bancada", "Polido", "m²", 80);
        A("PD-04", "TT Travertino", "Pedra", "#D4C3A3", "Parede/Bancada", "Levigado", "m²", 30);
        A("MT-01", "TT Aço Inox Escovado", "Metal", "#B5B8BA", "Metais", "Escovado", "un", 70);
        A("MT-02", "TT Metal Preto Fosco", "Metal", "#232323", "Metais", "Fosco", "un", 20);
        A("MT-03", "TT Latão Escovado", "Metal", "#B69A5B", "Metais", "Escovado", "un", 70);
        A("VD-01", "TT Vidro Incolor", "Vidro", "#DDE8EA", "Esquadria", "Liso", "m²", 90, 85);
        A("VD-02", "TT Vidro Fumê", "Vidro", "#7C8285", "Esquadria", "Liso", "m²", 90, 60);
        A("VD-03", "TT Espelho Prata", "Vidro", "#D9DCDD", "Parede", "Espelhado", "m²", 95);
        A("TC-01", "TT Tecido Linho Cru", "Tecido", "#D7CDB9", "Estofado", "Natural", "m", 5);
        A("TC-02", "TT Tecido Bouclé Off", "Tecido", "#E8E2D6", "Estofado", "Natural", "m", 5);
        A("FR-01", "TT Gesso Liso Branco", "Forro", "#F6F6F4", "Forro", "Pintado", "m²", 5);
        A("FR-02", "TT Forro Ripado Freijó", "Forro", "#B88A5A", "Forro", "Natural", "m²", 20);
        return lib;
    }
}

/// <summary>Esquema de acabamentos por ambiente (piso, parede, forro, rodapé).</summary>
public sealed class RoomFinishScheme
{
    public string Name { get; set; } = "";

    /// <summary>Padrão do nome do ambiente (curinga *), ex.: "Banho*".</summary>
    public string RoomPattern { get; set; } = "*";

    public string? FloorMaterial { get; set; }
    public string? WallMaterial { get; set; }
    public string? CeilingMaterial { get; set; }
    public string? BaseboardMaterial { get; set; }
}
