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

    /// <summary>Imagem de textura (nome do arquivo da pasta de texturas do plugin ou caminho completo).</summary>
    public string? Texture { get; set; }

    /// <summary>Tamanho real que a imagem representa (mm), para a textura sair em escala.</summary>
    public double TextureSizeMm { get; set; } = 1000;

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
        A("MD-07", "TT MDF Preto TX 18", "Madeira", "#1F1F20", "Marcenaria", "Texturizado", "m²", 10);
        A("MD-08", "TT MDF Cinza Cristal 18", "Madeira", "#B9BBBA", "Marcenaria", "Fosco", "m²", 10);
        A("MD-09", "TT MDF Azul Petróleo 18", "Madeira", "#2E4D57", "Marcenaria", "Fosco", "m²", 10);
        A("MD-10", "TT MDF Rosa Antigo 18", "Madeira", "#C9A19A", "Marcenaria", "Fosco", "m²", 10);
        A("LC-01", "TT Laca Off-White Brilho", "Laca", "#F1ECE2", "Marcenaria", "Alto brilho", "m²", 95);
        A("LC-02", "TT Laca Verde Musgo Fosca", "Laca", "#56604A", "Marcenaria", "Fosco", "m²", 10);
        A("VD-04", "TT Vidro Canelado", "Vidro", "#CFDDE0", "Esquadria/Marcenaria", "Canelado", "m²", 80, 60);
        A("VD-05", "TT Vidro Bronze", "Vidro", "#8A6E55", "Esquadria", "Liso", "m²", 90, 55);
        A("VD-06", "TT Espelho Bronze", "Vidro", "#8C7660", "Parede", "Espelhado", "m²", 95);
        A("PT-06", "TT Pintura Verde Sálvia", "Pintura", "#9CAA93", "Parede", "Fosco", "m²", 5);
        A("PT-07", "TT Pintura Amarelo Ocre", "Pintura", "#C59A45", "Parede", "Fosco", "m²", 5);
        A("PT-08", "TT Pintura Preto Fosco", "Pintura", "#2A2A2A", "Parede", "Fosco", "m²", 5);

        // Com textura (imagens CC0 do ambientCG, domínio público), em escala real.
        void T(string code, string name, string cat, string hex, string use, string finish, string texture, double sizeMm, int shine) =>
            lib.Items.Add(new MaterialSpec { Code = code, Name = name, Category = cat, ColorHex = hex, Use = use, Finish = finish, Unit = "m²", Shininess = shine, Texture = texture, TextureSizeMm = sizeMm });
        T("MA-01", "TT Madeira carvalho natural", "Madeira", "#7B634E", "Marcenaria/Painel", "Natural", "madeira-carvalho-natural.jpg", 800, 20);
        T("MA-02", "TT Madeira nogueira escura", "Madeira", "#41342E", "Marcenaria/Painel", "Verniz fosco", "madeira-nogueira-escura.jpg", 800, 25);
        T("MA-03", "TT Madeira freijó", "Madeira", "#A0795B", "Marcenaria/Painel", "Natural", "madeira-freijo.jpg", 400, 20);
        T("MA-04", "TT Madeira tabaco", "Madeira", "#593313", "Marcenaria/Painel", "Verniz fosco", "madeira-tabaco.jpg", 400, 25);
        T("MA-05", "TT Madeira pinus claro", "Madeira", "#B0733F", "Marcenaria/Forro", "Natural", "madeira-pinus-claro.jpg", 800, 15);
        T("MA-06", "TT Madeira de demolição", "Madeira", "#4F3D2E", "Painel/Piso", "Rústico", "madeira-demolicao.jpg", 1500, 10);
        T("PM-01", "TT Piso de madeira tábuas claras", "Madeira", "#B78755", "Piso", "Natural", "piso-madeira-tabuas-claras.jpg", 1900, 20);
        T("PM-02", "TT Piso de madeira rústico", "Madeira", "#664625", "Piso", "Rústico", "piso-madeira-rustico.jpg", 1300, 15);
        T("PM-03", "TT Piso de madeira carvalho em tábuas", "Madeira", "#9B7B5D", "Piso", "Natural", "piso-madeira-carvalho-tabuas.jpg", 1800, 20);
        T("PM-04", "TT Piso de madeira mel", "Madeira", "#6C4314", "Piso", "Verniz", "piso-madeira-mel.jpg", 1800, 35);
        T("PM-05", "TT Piso de madeira parquet (taco)", "Madeira", "#59360E", "Piso", "Verniz", "piso-madeira-parquet.jpg", 1200, 35);
        T("PE-01", "TT Mármore branco Carrara", "Pedra", "#F4F2F0", "Bancada/Piso/Parede", "Polido", "marmore-branco-carrara.jpg", 1500, 80);
        T("PE-02", "TT Mármore cinza Bardiglio", "Pedra", "#ADAEB6", "Bancada/Piso/Parede", "Polido", "marmore-cinza-bardiglio.jpg", 1500, 80);
        T("PE-03", "TT Mármore preto Nero Marquina", "Pedra", "#191919", "Bancada/Piso/Parede", "Polido", "marmore-preto-nero-marquina.jpg", 1500, 85);
        T("PE-04", "TT Mármore grafite com veios brancos", "Pedra", "#3D3F3E", "Bancada/Parede", "Polido", "marmore-grafite-veios-brancos.jpg", 1500, 80);
        T("PE-05", "TT Ônix cinza ondulado", "Pedra", "#CDCBC8", "Parede/Bancada", "Polido", "onix-cinza-ondulado.jpg", 1500, 85);
        T("PE-06", "TT Travertino romano bege", "Pedra", "#DFCBAC", "Piso/Parede/Bancada", "Levigado", "travertino-romano-bege.jpg", 1200, 30);
        T("PE-07", "TT Travertino noce", "Pedra", "#5F5147", "Piso/Parede", "Levigado", "travertino-noce.jpg", 1200, 30);
        T("PE-08", "TT Granito cinza andorinha", "Pedra", "#B5B2AD", "Bancada/Piso", "Polido", "granito-cinza-andorinha.jpg", 1000, 80);
        T("PE-09", "TT Granito rosa", "Pedra", "#A1989A", "Bancada/Piso", "Polido", "granito-rosa.jpg", 1000, 80);
        T("PE-10", "TT Granilite cinza", "Pedra", "#C1BCB8", "Piso", "Polido", "granilite-cinza.jpg", 1000, 40);
        T("PE-11", "TT Terrazzo colorido miúdo", "Pedra", "#D1CCC7", "Piso/Bancada", "Polido", "terrazzo-colorido-miudo.jpg", 1000, 40);
        T("PE-12", "TT Terrazzo graúdo", "Pedra", "#BDB3A9", "Piso/Bancada", "Polido", "terrazzo-graudo.jpg", 1000, 40);
        T("CI-01", "TT Cimento queimado grafite", "Cimentício", "#64615C", "Piso/Parede", "Fosco", "cimento-queimado-grafite.jpg", 1500, 15);
        T("CI-02", "TT Microcimento claro", "Cimentício", "#B8B8B8", "Piso/Parede", "Acetinado", "microcimento-claro.jpg", 1100, 20);
        T("CI-03", "TT Concreto lavado", "Cimentício", "#7A6F63", "Piso externo", "Rústico", "concreto-lavado.jpg", 1100, 5);
        T("CI-04", "TT Reboco massa fina branca", "Cimentício", "#D7D3CF", "Parede", "Fosco", "reboco-massa-fina-branca.jpg", 1500, 5);
        T("PI-01", "TT Pintura acrílica cinza claro", "Pintura", "#BEBFBB", "Parede/Forro", "Fosco", "pintura-acrilica-cinza-claro.jpg", 1500, 5);
        T("RV-01", "TT Revestimento branco 10×10", "Revestimento", "#F8F8F7", "Parede", "Brilhante", "revestimento-branco-10x10.jpg", 1000, 70);
        T("RV-02", "TT Pastilha de vidro água", "Revestimento", "#D1DDE0", "Parede/Piscina", "Brilhante", "pastilha-vidro-agua.jpg", 300, 85);
        T("RV-03", "TT Revestimento preto com rejunte claro", "Revestimento", "#353636", "Parede/Piso", "Acetinado", "revestimento-preto-rejunte.jpg", 1800, 40);
        T("RV-04", "TT Porcelanato pedra clara 60×60", "Revestimento", "#D3CBC3", "Piso", "Acetinado", "porcelanato-pedra-clara-60x60.jpg", 2000, 35);
        T("RV-05", "TT Piso xadrez preto e bege", "Revestimento", "#554B43", "Piso", "Polido", "piso-xadrez-preto-bege.jpg", 2100, 60);
        T("RV-06", "TT Porcelanato marmorizado branco", "Revestimento", "#DBD9DB", "Piso/Parede", "Polido", "porcelanato-marmorizado-branco.jpg", 1500, 70);
        T("RV-07", "TT Porcelanato terracota", "Revestimento", "#9C8675", "Piso", "Fosco", "porcelanato-terracota.jpg", 1500, 15);
        T("ME-02", "TT Cobre", "Metal", "#AE713A", "Painel/Fachada/Metais", "Acetinado", "cobre.jpg", 1000, 75);
        T("ME-04", "TT Bronze oxidado", "Metal", "#6A5042", "Painel/Fachada", "Rústico", "bronze-oxidado.jpg", 1000, 30);
        T("ME-05", "TT Latão polido (dourado)", "Metal", "#E3B108", "Metais", "Polido", "latao-polido.jpg", 1000, 90);
        T("ME-06", "TT Aço preto fosco", "Metal", "#3F3F3F", "Serralheria/Puxador", "Fosco", "aco-preto-fosco.jpg", 1000, 20);
        T("ME-07", "TT Aço galvanizado", "Metal", "#C0C2C1", "Estrutura", "Natural", "aco-galvanizado.jpg", 1000, 40);
        T("ME-08", "TT Alumínio anodizado", "Metal", "#7D8893", "Esquadria/Perfil", "Acetinado", "aluminio-anodizado.jpg", 1000, 60);
        T("ME-09", "TT Aço corten", "Metal", "#8C4C27", "Fachada/Painel", "Oxidado", "aco-corten.jpg", 1000, 10);
        T("TE-01", "TT Tecido linho cinza", "Tecido", "#A9AAAF", "Estofado/Cortina", "Natural", "tecido-linho-cinza.jpg", 500, 5);
        T("TE-03", "TT Tecido bouclé", "Tecido", "#8D8883", "Estofado", "Natural", "tecido-boucle.jpg", 400, 5);
        T("TE-04", "TT Tecido veludo grafite", "Tecido", "#5B5B5B", "Estofado", "Aveludado", "tecido-veludo-grafite.jpg", 500, 15);
        T("TE-05", "TT Carpete azul-marinho", "Tecido", "#343B48", "Piso", "Natural", "carpete-azul-marinho.jpg", 1700, 5);
        T("TE-06", "TT Carpete bege", "Tecido", "#BCB093", "Piso", "Natural", "carpete-bege.jpg", 1700, 5);
        T("TE-07", "TT Couro café", "Couro", "#3A2721", "Estofado/Puxador", "Natural", "couro-cafe.jpg", 450, 25);
        T("TE-08", "TT Couro conhaque", "Couro", "#643829", "Estofado", "Natural", "couro-conhaque.jpg", 450, 25);
        T("NA-01", "TT Palhinha trançada escura", "Natural", "#634532", "Marcenaria/Painel", "Natural", "palhinha-escura.jpg", 250, 10);
        T("NA-02", "TT Palhinha natural", "Natural", "#937352", "Marcenaria/Painel", "Natural", "palhinha-natural.jpg", 300, 10);
        T("NA-03", "TT Cortiça", "Natural", "#AA703D", "Piso/Painel", "Natural", "cortica.jpg", 600, 5);
        T("NA-04", "TT Tijolo aparente rústico", "Alvenaria", "#927955", "Parede", "Rústico", "tijolo-aparente-rustico.jpg", 1500, 5);

        // Itens antigos (só cor) que ganharam textura.
        void Tex(string code, string texture, double sizeMm)
        {
            var i = lib.Items.First(x => x.Code == code);
            i.Texture = texture;
            i.TextureSizeMm = sizeMm;
        }
        Tex("MT-01", "aco-inox-escovado.jpg", 1000);
        Tex("MT-03", "latao-escovado.jpg", 1000);
        Tex("TC-01", "tecido-linho-cru.jpg", 500);
        Tex("PD-03", "marmore-branco-carrara.jpg", 1500);
        Tex("PD-04", "travertino-romano-bege.jpg", 1200);
        Tex("PS-05", "cimento-queimado-grafite.jpg", 1500);
        Tex("MD-02", "madeira-freijo.jpg", 400);
        Tex("MD-06", "madeira-nogueira-escura.jpg", 800);
        Tex("TC-02", "tecido-boucle.jpg", 400);
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
