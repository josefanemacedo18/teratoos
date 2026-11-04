using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using TeraTools.Revit.Infrastructure;

namespace TeraTools.Revit.Families;

/// <summary>
/// Parâmetros compartilhados do Téra Tools (GUIDs fixos). Por serem compartilhados, aparecem
/// em tabelas e etiquetas, e são os mesmos em todos os projetos e famílias geradas.
/// </summary>
public static class SharedParams
{
    public enum Kind
    {
        Length,
        Text,
        YesNo,
        Integer,
        Material,
        Angle,
        Number,
    }

    public sealed record Def(string Name, string Guid, Kind Kind, string Description);

    public const string Largura = "TT_Largura";
    public const string Profundidade = "TT_Profundidade";
    public const string Altura = "TT_Altura";
    public const string Comprimento = "TT_Comprimento";
    public const string Espessura = "TT_Espessura";
    public const string Material = "TT_Material";
    public const string Codigo = "TT_Codigo";
    public const string Peca = "TT_Peca";
    public const string Modulo = "TT_Modulo";
    public const string Ambiente = "TT_Ambiente";
    public const string FitaC1 = "TT_FitaC1";
    public const string FitaC2 = "TT_FitaC2";
    public const string FitaL1 = "TT_FitaL1";
    public const string FitaL2 = "TT_FitaL2";
    public const string Veio = "TT_Veio";
    public const string Sistema = "TT_Sistema";
    public const string Indice = "TT_Indice";
    public const string Abertura = "TT_Abertura";
    public const string CustoUnitario = "TT_CustoUnitario";
    public const string Especificacao = "TT_Especificacao";
    public const string Acabamento = "TT_Acabamento";
    public const string Ferragem = "TT_Ferragem";
    public const string Quantidade = "TT_Quantidade";
    public const string Diametro = "TT_Diametro";
    public const string Grupo = "TT_Grupo";
    public const string Fornecedor = "TT_Fornecedor";
    public const string Observacao = "TT_Observacao";
    public const string Raio = "TT_Raio";
    public const string Circuito = "TT_Circuito";
    public const string Status = "TT_Status";
    public const string Unidade = "TT_Unidade";

    public static readonly IReadOnlyList<Def> All = new List<Def>
    {
        new(Largura, "CE4ED2DB-620E-4580-AC73-6E72BEE2ECF2", Kind.Length, "Largura (X) do elemento Téra"),
        new(Profundidade, "C30E04C4-104A-44FB-A11E-0CD35133D5C0", Kind.Length, "Profundidade (Y) do elemento Téra"),
        new(Altura, "97851E86-0DDE-4BB4-9BCA-9B3E2AC8A98A", Kind.Length, "Altura (Z) do elemento Téra"),
        new(Comprimento, "568343F7-6858-4294-B9E8-568181FCD4A2", Kind.Length, "Comprimento de corte (sentido do veio)"),
        new(Espessura, "AF251803-B321-4D33-BFFE-7CDD950624DF", Kind.Length, "Espessura da chapa/peça"),
        new(Material, "103DC38F-0954-49ED-BC9B-B431EE5DCA09", Kind.Material, "Material do elemento Téra"),
        new(Codigo, "48B86A3D-682B-4532-B78B-B83E6EE6DDC7", Kind.Text, "Código do item / especificação"),
        new(Peca, "CD4BC128-0629-47B2-9B0C-B73B845A2FA6", Kind.Text, "Identificação da peça"),
        new(Modulo, "27BCD89A-254D-4C4B-BA44-B679C6F91EB6", Kind.Text, "Módulo de marcenaria"),
        new(Ambiente, "5D7F4FC5-A93C-49E6-90AA-0889D39CC7C0", Kind.Text, "Ambiente"),
        new(FitaC1, "9022A60C-9F8D-4CC1-B1DB-8A973E556668", Kind.YesNo, "Fita de borda — lado curto 1"),
        new(FitaC2, "AF2C1E3F-B242-4CFD-BC3F-A4FE5A7F04B6", Kind.YesNo, "Fita de borda — lado curto 2"),
        new(FitaL1, "77EFD20C-6B6D-4A39-8DAC-99CD9C47D88D", Kind.YesNo, "Fita de borda — lado longo 1"),
        new(FitaL2, "100EF10D-8DB3-4067-8725-D62D2E20818B", Kind.YesNo, "Fita de borda — lado longo 2"),
        new(Veio, "98F11CBC-00FF-4D11-AB87-A5557378FA4F", Kind.Text, "Sentido do veio"),
        new(Sistema, "E5F04E02-19A6-472E-947F-D61F848C6CEB", Kind.Text, "Sistema gerado (Téra Tools)"),
        new(Indice, "27C5C435-BC5B-4935-B65D-17F907F169B3", Kind.Integer, "Índice no sistema gerado"),
        new(Abertura, "7892607B-BC0C-4B6E-AB42-C012ABCD48CB", Kind.Angle, "Abertura / ângulo (cinético)"),
        new(CustoUnitario, "7DA4B96F-D4B2-4FF2-B7A1-A57EC1B8DAA4", Kind.Number, "Custo unitário"),
        new(Especificacao, "4EC84FDB-E233-4B0B-A2D7-0CDFF14DA0EA", Kind.Text, "Especificação"),
        new(Acabamento, "FC042EFF-1600-4C8F-98C2-F20C87D563C7", Kind.Text, "Acabamento"),
        new(Ferragem, "FD9C43DC-8B65-42FF-BF7B-84ABC5A8150E", Kind.Text, "Ferragens"),
        new(Quantidade, "BC169B26-C9BA-4A24-8DC0-AF0DFD6DD09D", Kind.Integer, "Quantidade"),
        new(Diametro, "E6B10A84-49D6-4380-9892-238A718224F6", Kind.Length, "Diâmetro"),
        new(Grupo, "710FF01D-6A7D-4161-B419-71E4DD850E9B", Kind.Text, "Grupo / conjunto"),
        new(Fornecedor, "CFCAB02F-4552-4717-BA08-7FB7DD32B064", Kind.Text, "Fornecedor"),
        new(Observacao, "B473700F-8BCE-4201-B55C-6396037241FF", Kind.Text, "Observação"),
        new(Raio, "DD06E3B1-251F-4807-89D4-2E0765FA9041", Kind.Length, "Raio"),
        new(Circuito, "8808E072-B680-4E47-9E19-4B28E05A3E2C", Kind.Text, "Circuito / grupo de iluminação"),
        new(Status, "0472EF52-43A6-4E8C-8208-03D3BCE83730", Kind.Text, "Estado / status"),
        new(Unidade, "EC8AFA11-F540-4428-ACAE-822E356714C1", Kind.Text, "Unidade"),
    };

    public static string FilePath => Path.Combine(Paths.Root, "TeraTools_ParametrosCompartilhados.txt");

    private static ForgeTypeId Spec(Kind k) => k switch
    {
        Kind.Length => SpecTypeId.Length,
        Kind.YesNo => SpecTypeId.Boolean.YesNo,
        Kind.Integer => SpecTypeId.Int.Integer,
        Kind.Material => SpecTypeId.Reference.Material,
        Kind.Angle => SpecTypeId.Angle,
        Kind.Number => SpecTypeId.Number,
        _ => SpecTypeId.String.Text,
    };

    public static ForgeTypeId GroupFor(Kind k) => k switch
    {
        Kind.Length or Kind.Angle => GroupTypeId.Geometry,
        Kind.Material => GroupTypeId.Materials,
        _ => GroupTypeId.Data,
    };

    public static Def Get(string name) => All.First(d => d.Name == name);

    /// <summary>
    /// Abre (criando se preciso) o arquivo de parâmetros do Téra Tools e devolve as definições pedidas.
    /// O arquivo de parâmetros compartilhados do usuário é restaurado ao final.
    /// </summary>
    public static Dictionary<string, ExternalDefinition> Definitions(Application app, IEnumerable<string> names)
    {
        var wanted = names.Distinct().ToList();
        var result = new Dictionary<string, ExternalDefinition>();
        var original = app.SharedParametersFilename;
        try
        {
            if (!File.Exists(FilePath)) File.WriteAllText(FilePath, "", System.Text.Encoding.Unicode);
            app.SharedParametersFilename = FilePath;
            var file = app.OpenSharedParameterFile() ?? throw new ToolException(Core.Localization.Loc.T(
                "Não foi possível abrir o arquivo de parâmetros compartilhados do Téra Tools.",
                "Could not open the Téra Tools shared parameter file."));
            var group = file.Groups.get_Item("TeraTools") ?? file.Groups.Create("TeraTools");
            foreach (var n in wanted)
            {
                var d = Get(n);
                var existing = group.Definitions.get_Item(d.Name) as ExternalDefinition;
                if (existing == null)
                {
                    var opts = new ExternalDefinitionCreationOptions(d.Name, Spec(d.Kind))
                    {
                        GUID = new Guid(d.Guid),
                        Description = d.Description,
                        UserModifiable = true,
                        Visible = true,
                    };
                    existing = (ExternalDefinition)group.Definitions.Create(opts);
                }
                result[n] = existing;
            }
        }
        finally
        {
            try
            {
                app.SharedParametersFilename = original ?? "";
            }
            catch
            {
                // ignora
            }
        }
        return result;
    }

    /// <summary>Vincula parâmetros compartilhados como parâmetros de projeto (instância) nas categorias.</summary>
    public static int BindToProject(Document doc, IEnumerable<string> names, IEnumerable<BuiltInCategory> categories)
    {
        var defs = Definitions(doc.Application, names);
        var cats = new CategorySet();
        foreach (var bic in categories)
        {
            var c = Category.GetCategory(doc, bic);
            if (c != null && c.AllowsBoundParameters) cats.Insert(c);
        }
        if (cats.IsEmpty) return 0;
        var n = 0;
        foreach (var (name, def) in defs)
        {
            var d = Get(name);
            var map = doc.ParameterBindings;
            if (map.Contains(def))
            {
                if (map.get_Item(def) is InstanceBinding ib)
                {
                    var changed = false;
                    foreach (Category c in cats)
                        if (!ib.Categories.Contains(c))
                        {
                            ib.Categories.Insert(c);
                            changed = true;
                        }
                    if (changed && map.ReInsert(def, ib, GroupFor(d.Kind))) n++;
                }
                continue;
            }
            if (map.Insert(def, new InstanceBinding(cats), GroupFor(d.Kind))) n++;
        }
        return n;
    }

    /// <summary>Categorias que recebem os parâmetros de identificação de sistemas gerados.</summary>
    public static readonly BuiltInCategory[] GeneratedCategories =
    {
        BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_Casework, BuiltInCategory.OST_Furniture,
        BuiltInCategory.OST_StructuralFraming, BuiltInCategory.OST_LightingFixtures, BuiltInCategory.OST_Mass,
        BuiltInCategory.OST_Parts, BuiltInCategory.OST_Floors, BuiltInCategory.OST_Ceilings, BuiltInCategory.OST_Walls,
    };
}
