using TeraTools.Core.Checks;
using TeraTools.Core.Colors;
using TeraTools.Core.Costing;
using TeraTools.Core.Facade;
using TeraTools.Core.Imaging;
using TeraTools.Core.Joinery;
using TeraTools.Core.Kinetic;
using TeraTools.Core.Nesting;
using TeraTools.Core.Patterns;
using TeraTools.Core.Reporting;
using TeraTools.Core.Structure;
using TeraTools.Core.Text;
using TeraTools.Core.Tiling;

namespace TeraTools.Revit.Ui;

/// <summary>Rótulos bilíngues dos enums do núcleo usados nos diálogos.</summary>
public static class EnumTexts
{
    public static void RegisterAll()
    {
        // Catálogo de acabamentos: mesmos rótulos da planilha (definidos no núcleo).
        EnumLabels.Register(Core.Finishes.FinishLabels.Kinds.Select(k => (k.Value, k.Pt, k.Value.ToString())).ToArray());
        EnumLabels.Register(Core.Finishes.FinishLabels.Uses.Select(k => (k.Value, k.Pt, k.Value.ToString())).ToArray());
        EnumLabels.Register(Core.Finishes.FinishLabels.Units.Select(k => (k.Value, k.Pt, k.Value.ToString())).ToArray());
        EnumLabels.Register(
            (PixyPattern.Hexagonal, "Hexagonal (colmeia)", "Hexagonal (honeycomb)"),
            (PixyPattern.Voronoi, "Voronoi (orgânico)", "Voronoi (organic)"),
            (PixyPattern.StarBurst, "Star Burst (estrelas radiais)", "Star Burst (radial stars)"),
            (PixyPattern.Circles, "Círculos", "Circles"),
            (PixyPattern.Diamonds, "Losangos", "Diamonds"),
            (PixyPattern.Triangles, "Triângulos", "Triangles"),
            (PixyPattern.Squares, "Quadrados", "Squares"),
            (PixyPattern.Waves, "Ondas", "Waves"),
            (PixyPattern.Bricks, "Tijolos (desencontrado)", "Bricks (staggered)"));
        EnumLabels.Register(
            (FadeMode.None, "Nenhum", "None"),
            (FadeMode.Linear, "Linear (de um lado ao outro)", "Linear (side to side)"),
            (FadeMode.Radial, "Radial (a partir de um centro)", "Radial (from a center)"),
            (FadeMode.Noise, "Orgânico (ruído)", "Organic (noise)"));
        EnumLabels.Register(
            (PatternLayout.Linear, "Linear", "Linear"),
            (PatternLayout.Rectangular, "Retangular (quadrangular)", "Rectangular (square)"),
            (PatternLayout.Staggered, "Desencontrado (quincôncio)", "Staggered"),
            (PatternLayout.Diamond, "Losango", "Diamond"),
            (PatternLayout.Triangular, "Triangular", "Triangular"),
            (PatternLayout.Hexagonal, "Hexagonal", "Hexagonal"),
            (PatternLayout.Radial, "Radial", "Radial"),
            (PatternLayout.Spiral, "Espiral (filotaxia)", "Spiral (phyllotaxis)"),
            (PatternLayout.Random, "Aleatório controlado", "Controlled random"));
        EnumLabels.Register(
            (ModuleShape.Rectangle, "Retângulo", "Rectangle"),
            (ModuleShape.Circle, "Círculo", "Circle"),
            (ModuleShape.Triangle, "Triângulo", "Triangle"),
            (ModuleShape.Hexagon, "Hexágono", "Hexagon"),
            (ModuleShape.Diamond, "Losango", "Diamond"));
        EnumLabels.Register(
            (GradientMode.None, "Nenhum", "None"),
            (GradientMode.Linear, "Linear (direção)", "Linear (direction)"),
            (GradientMode.Radial, "Radial (ponto atrator)", "Radial (attractor point)"),
            (GradientMode.Noise, "Ruído orgânico", "Organic noise"),
            (GradientMode.Formula, "Fórmula", "Formula"),
            (GradientMode.Image, "Imagem (brilho)", "Image (brightness)"));
        EnumLabels.Register(
            (ClipMode.CenterInside, "Centro dentro da região", "Center inside region"),
            (ClipMode.FullyInside, "Módulo inteiro dentro", "Whole module inside"),
            (ClipMode.Touching, "Qualquer parte dentro", "Any part inside"));
        EnumLabels.Register(
            (PatternAnchor.RegionCenter, "Centro da região", "Region center"),
            (PatternAnchor.RegionCorner, "Canto da região", "Region corner"),
            (PatternAnchor.Origin, "Origem da superfície", "Surface origin"));
        EnumLabels.Register(
            (BlockColorMode.Single, "Um material", "Single material"),
            (BlockColorMode.AutoPalette, "Paleta automática", "Automatic palette"),
            (BlockColorMode.FixedPalette, "Cores definidas", "Defined colors"),
            (BlockColorMode.Threshold, "Dois materiais (limiar)", "Two materials (threshold)"),
            (BlockColorMode.GrayLevels, "Tons de cinza", "Gray levels"));
        EnumLabels.Register(
            (HeightSource.Flat, "Plano (sem relevo)", "Flat (no relief)"),
            (HeightSource.Brightness, "Claro = mais alto", "Bright = higher"),
            (HeightSource.Darkness, "Escuro = mais alto", "Dark = higher"));
        EnumLabels.Register(
            (FinOrientation.Vertical, "Verticais", "Vertical"),
            (FinOrientation.Horizontal, "Horizontais", "Horizontal"));
        EnumLabels.Register(
            (FinDistribution.BySpacing, "Por espaçamento", "By spacing"),
            (FinDistribution.ByCount, "Por quantidade", "By count"),
            (FinDistribution.FitSpacing, "Espaçamento ajustado às margens", "Spacing fitted to margins"));
        EnumLabels.Register(
            (FinSide.Left, "Lado esquerdo da linha", "Left side of line"),
            (FinSide.Right, "Lado direito da linha", "Right side of line"));
        EnumLabels.Register(
            (FinPivot.Center, "Centro da aleta", "Fin center"),
            (FinPivot.FacadeEdge, "Borda junto à fachada", "Facade edge"),
            (FinPivot.OuterEdge, "Borda externa", "Outer edge"));
        EnumLabels.Register(
            (VariationMode.None, "Sem variação", "No variation"),
            (VariationMode.Linear, "Linear", "Linear"),
            (VariationMode.Sine, "Senoidal (onda)", "Sine (wave)"),
            (VariationMode.Random, "Aleatória (semente)", "Random (seed)"),
            (VariationMode.Attractor, "Atrator", "Attractor"),
            (VariationMode.Formula, "Fórmula", "Formula"));
        EnumLabels.Register(
            (SpaceFrameType.SquareOnSquareOffset, "Quadrado sobre quadrado deslocado", "Square on offset square"),
            (SpaceFrameType.SquareOnSquareAligned, "Quadrado sobre quadrado alinhado", "Square on aligned square"),
            (SpaceFrameType.TriangularOffset, "Triangular (tetraédrica)", "Triangular (tetrahedral)"));
        EnumLabels.Register(
            (DiagonalPattern.All, "Todas", "All"),
            (DiagonalPattern.Alternate, "Alternadas", "Alternate"));
        EnumLabels.Register(
            (HingeAxis.Vertical, "Eixo vertical", "Vertical axis"),
            (HingeAxis.Horizontal, "Eixo horizontal", "Horizontal axis"));
        EnumLabels.Register(
            (HingePosition.Center, "Pivô central", "Center pivot"),
            (HingePosition.Start, "Borda inicial (esq./inferior)", "Start edge (left/bottom)"),
            (HingePosition.End, "Borda final (dir./superior)", "End edge (right/top)"));
        EnumLabels.Register(
            (KineticMode.Uniform, "Uniforme", "Uniform"),
            (KineticMode.Stagger, "Escalonada (dominó)", "Staggered (domino)"),
            (KineticMode.Wave, "Onda", "Wave"),
            (KineticMode.Random, "Aleatória", "Random"),
            (KineticMode.Attractor, "Atrator", "Attractor"),
            (KineticMode.Formula, "Fórmula", "Formula"));
        EnumLabels.Register(
            (TileLayout.Grid, "Alinhada (junta a prumo)", "Grid (stack bond)"),
            (TileLayout.RunningBond, "Amarração (junta deslocada)", "Running bond"),
            (TileLayout.Diagonal, "Diagonal 45°", "Diagonal 45°"),
            (TileLayout.Herringbone, "Espinha de peixe", "Herringbone"),
            (TileLayout.Basketweave, "Trama (cestaria)", "Basketweave"),
            (TileLayout.ModularThreeSizes, "Modular 3 tamanhos", "Modular 3 sizes"),
            (TileLayout.ModularPythagorean, "Modular pitagórico (2 tamanhos)", "Pythagorean (2 sizes)"));
        EnumLabels.Register(
            (TileStart.Corner, "Canto do ambiente", "Room corner"),
            (TileStart.Center, "Peça centralizada", "Centered tile"),
            (TileStart.CenterJoint, "Junta centralizada", "Centered joint"),
            (TileStart.Custom, "Ponto escolhido", "Picked point"));
        EnumLabels.Register(
            (CabinetKind.Base, "Armário inferior", "Base cabinet"),
            (CabinetKind.Wall, "Armário superior", "Wall cabinet"),
            (CabinetKind.Tall, "Torre / coluna", "Tall cabinet"),
            (CabinetKind.OpenNiche, "Nicho aberto", "Open niche"),
            (CabinetKind.ClosedNiche, "Nicho fechado", "Closed niche"),
            (CabinetKind.Wardrobe, "Roupeiro / closet", "Wardrobe"),
            (CabinetKind.Vanity, "Gabinete de banheiro", "Bathroom vanity"),
            (CabinetKind.Counter, "Bancada", "Counter"),
            (CabinetKind.Island, "Ilha", "Island"),
            (CabinetKind.TvPanel, "Painel de TV", "TV panel"),
            (CabinetKind.Bookcase, "Estante / biblioteca", "Bookcase"),
            (CabinetKind.Custom, "Personalizado", "Custom"));
        EnumLabels.Register(
            (DoorKind.None, "Sem portas", "No doors"),
            (DoorKind.Hinged, "De abrir (automático 1 ou 2)", "Hinged (auto 1 or 2)"),
            (DoorKind.HingedDouble, "De abrir — 2 folhas", "Hinged — double"),
            (DoorKind.Sliding, "De correr", "Sliding"),
            (DoorKind.FlipUp, "Basculante", "Flip-up"));
        EnumLabels.Register(
            (HandleKind.None, "Sem puxador", "No handle"),
            (HandleKind.Bar, "Barra", "Bar"),
            (HandleKind.Knob, "Botão", "Knob"),
            (HandleKind.Profile, "Perfil", "Profile"),
            (HandleKind.Cava, "Cava usinada", "Routed grip"),
            (HandleKind.PushOpen, "Toque (push)", "Push to open"));
        EnumLabels.Register(
            (BackKind.None, "Sem fundo", "No back"),
            (BackKind.Inset, "Encaixado (canal)", "Inset (groove)"),
            (BackKind.Overlay, "Sobreposto", "Overlay"));
        EnumLabels.Register(
            (TopKind.Full, "Tampo inteiro", "Full top"),
            (TopKind.Rails, "Travessas", "Rails"),
            (TopKind.None, "Sem tampo", "No top"));
        EnumLabels.Register(
            (DrawerZone.Top, "Gavetas em cima", "Drawers on top"),
            (DrawerZone.Bottom, "Gavetas embaixo", "Drawers at bottom"));
        EnumLabels.Register(
            (NestingMethod.Guillotine, "Guilhotina (seccionadora)", "Guillotine (panel saw)"),
            (NestingMethod.MaxRects, "Encaixe livre (CNC)", "Free nesting (CNC)"));
        EnumLabels.Register(
            (Harmony.Monochromatic, "Monocromática", "Monochromatic"),
            (Harmony.Analogous, "Análoga", "Analogous"),
            (Harmony.Complementary, "Complementar", "Complementary"),
            (Harmony.SplitComplementary, "Complementar dividida", "Split complementary"),
            (Harmony.Triadic, "Tríade", "Triadic"),
            (Harmony.Tetradic, "Tétrade", "Tetradic"),
            (Harmony.WarmNeutrals, "Neutros quentes", "Warm neutrals"),
            (Harmony.CoolNeutrals, "Neutros frios", "Cool neutrals"),
            (Harmony.Gradient, "Degradê entre duas cores", "Gradient between two colors"));
        EnumLabels.Register(
            (NumberingStyle.Numbers, "Números (1, 2, 3)", "Numbers (1, 2, 3)"),
            (NumberingStyle.Letters, "Letras (A, B, C)", "Letters (A, B, C)"),
            (NumberingStyle.LowerLetters, "Letras minúsculas (a, b, c)", "Lowercase letters (a, b, c)"),
            (NumberingStyle.Roman, "Romanos (I, II, III)", "Roman (I, II, III)"));
        EnumLabels.Register(
            (TextCase.Keep, "Manter", "Keep"),
            (TextCase.Upper, "MAIÚSCULAS", "UPPERCASE"),
            (TextCase.Lower, "minúsculas", "lowercase"),
            (TextCase.Title, "Primeiras Maiúsculas", "Title Case"),
            (TextCase.Sentence, "Frase", "Sentence case"));
        EnumLabels.Register(
            (Severity.Info, "Informação", "Info"),
            (Severity.Success, "Sucesso", "Success"),
            (Severity.Warning, "Aviso", "Warning"),
            (Severity.Error, "Erro", "Error"));
        EnumLabels.Register(
            (RuleKind.Required, "Obrigatório (não vazio)", "Required (not empty)"),
            (RuleKind.Range, "Faixa numérica", "Numeric range"),
            (RuleKind.Pattern, "Padrão de texto (regex)", "Text pattern (regex)"),
            (RuleKind.AllowedValues, "Valores permitidos", "Allowed values"),
            (RuleKind.Unique, "Valor único", "Unique value"),
            (RuleKind.NamePattern, "Padrão do nome", "Name pattern"));
        EnumLabels.Register(
            (PriceMatch.Code, "Código", "Code"),
            (PriceMatch.FamilyType, "Família : Tipo", "Family : Type"),
            (PriceMatch.Material, "Material", "Material"),
            (PriceMatch.Category, "Categoria", "Category"));
    }
}
