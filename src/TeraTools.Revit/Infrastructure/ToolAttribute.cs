using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Metadados de uma ferramenta. Cada comando do Téra Tools é uma classe derivada de
/// <see cref="ToolCommand"/> marcada com este atributo; a faixa de opções, o painel lateral,
/// a busca e a ajuda são montados automaticamente a partir dele.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ToolAttribute : Attribute
{
    public ToolAttribute(string id, string module, string namePt, string nameEn)
    {
        Id = id;
        Module = module;
        NamePt = namePt;
        NameEn = nameEn;
    }

    public string Id { get; }
    public string Module { get; }
    public string NamePt { get; }
    public string NameEn { get; }
    public string DescPt { get; set; } = "";
    public string DescEn { get; set; } = "";
    public string HelpPt { get; set; } = "";
    public string HelpEn { get; set; } = "";

    /// <summary>Chave do ícone (ver IconLibrary). Vazio = ícone do módulo.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Agrupamento na faixa de opções (menu suspenso), formato "pt|en".</summary>
    public string Group { get; set; } = "";

    public int Order { get; set; }

    /// <summary>Botão grande na faixa de opções.</summary>
    public bool Large { get; set; }

    /// <summary>Exibe na faixa de opções (sempre aparece no painel lateral).</summary>
    public bool Ribbon { get; set; } = true;

    public bool RequiresDocument { get; set; } = true;

    /// <summary>Palavras extras para a busca (pt e en).</summary>
    public string Keywords { get; set; } = "";
}

/// <summary>Informações resolvidas de uma ferramenta.</summary>
public sealed class ToolInfo
{
    public string Id { get; init; } = "";
    public string Module { get; init; } = "";
    public Txt Name { get; init; }
    public Txt Description { get; init; }
    public Txt Help { get; init; }
    public string Icon { get; init; } = "";
    public Txt Group { get; init; }
    public bool HasGroup => !string.IsNullOrEmpty(Group.Pt);
    public int Order { get; init; }
    public bool Large { get; init; }
    public bool Ribbon { get; init; }
    public bool RequiresDocument { get; init; }
    public string Keywords { get; init; } = "";
    public Type Type { get; init; } = typeof(object);
    public string ClassName => Type.FullName ?? Type.Name;

    public bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return true;
        var words = query.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hay = Core.Text.TextUtil.RemoveDiacritics(
            $"{Name.Pt} {Name.En} {Description.Pt} {Description.En} {Keywords} {Id} {Group.Pt} {Group.En}").ToLowerInvariant();
        return words.All(w => hay.Contains(Core.Text.TextUtil.RemoveDiacritics(w).ToLowerInvariant()));
    }

    public override string ToString() => Name.Value;
}

/// <summary>Módulo (aba/painel) do plugin.</summary>
public sealed record ModuleInfo(string Id, Txt Name, Txt Description, string ColorHex, string Icon, int Order);

public static class Modules
{
    public const string General = "general";
    public const string Modeling = "modeling";
    public const string Selection = "selection";
    public const string Analysis = "analysis";
    public const string Generators = "generators";
    public const string Geometry = "geometry";
    public const string Floors = "floors";
    public const string Interiors = "interiors";
    public const string Joinery = "joinery";
    public const string Materials = "materials";
    public const string Documentation = "docs";
    public const string Quantities = "quantities";
    public const string Quality = "quality";
    public const string Project = "project";

    public static readonly IReadOnlyList<ModuleInfo> All = new List<ModuleInfo>
    {
        new(General, new Txt("Geral", "General"), new Txt("Painel, busca, configurações e ajuda", "Panel, search, settings and help"), "#B5652B", "mod.general", 0),
        new(Modeling, new Txt("Modelagem", "Modeling"), new Txt("Alinhar, distribuir, matrizes e transformações", "Align, distribute, arrays and transforms"), "#3B7DD8", "mod.modeling", 10),
        new(Selection, new Txt("Seleção", "Selection"), new Txt("Seleção, filtros e visibilidade", "Selection, filters and visibility"), "#2E9C8A", "mod.selection", 20),
        new(Analysis, new Txt("Análise", "Analysis"), new Txt("Colisões e medições", "Clashes and measurements"), "#C24E4E", "mod.analysis", 30),
        new(Generators, new Txt("Geradores", "Generators"), new Txt("Padrões, imagem, brises, quadros espaciais, cinético e temas", "Patterns, image, louvers, space frames, kinetic and themes"), "#8C59C9", "mod.generators", 40),
        new(Geometry, new Txt("Geometria", "Geometry"), new Txt("Booleanas, partes, aberturas e sólidos", "Booleans, parts, openings and solids"), "#5A6B7D", "mod.geometry", 50),
        new(Floors, new Txt("Pisos", "Floors"), new Txt("Pisos, paginação, rodapés e revestimentos", "Floors, tile layout, baseboards and finishes"), "#B08A3E", "mod.floors", 60),
        new(Interiors, new Txt("Interiores", "Interiors"), new Txt("Ambientes, mobiliário, ergonomia, iluminação, forros e paredes", "Rooms, furniture, ergonomics, lighting, ceilings and walls"), "#D07A3A", "mod.interiors", 70),
        new(Joinery, new Txt("Marcenaria", "Joinery"), new Txt("Módulos planejados, detalhamento e plano de corte", "Cabinetry modules, detailing and cutting plans"), "#8A5A35", "mod.joinery", 80),
        new(Materials, new Txt("Materiais", "Materials"), new Txt("Biblioteca, aplicação e especificação de materiais", "Material library, application and specifications"), "#4F8F3F", "mod.materials", 90),
        new(Documentation, new Txt("Documentação", "Documentation"), new Txt("Vistas, pranchas, anotações e exportação", "Views, sheets, annotations and export"), "#3E6FA3", "mod.docs", 100),
        new(Quantities, new Txt("Quantitativos", "Quantities"), new Txt("Tabelas, quantitativos e orçamento", "Schedules, quantities and budget"), "#2F8F6F", "mod.quantities", 110),
        new(Quality, new Txt("Qualidade", "Quality"), new Txt("Verificações e regras personalizadas", "Checks and custom rules"), "#C2416B", "mod.quality", 120),
        new(Project, new Txt("Projeto", "Project"), new Txt("Configurações, presets, bibliotecas e lote", "Settings, presets, libraries and batch"), "#6A6F78", "mod.project", 130),
    };

    public static ModuleInfo Get(string id) => All.FirstOrDefault(m => m.Id == id) ?? All[0];
}
