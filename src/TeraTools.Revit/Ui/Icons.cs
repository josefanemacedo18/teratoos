using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TeraTools.Revit.Ui;

/// <summary>
/// Ícones vetoriais próprios do Téra Tools (grade 32×32). Cada ícone é uma lista de segmentos
/// "prefixo:geometria" — s = traço neutro, a = traço na cor do módulo, d = traço tracejado,
/// f = preenchimento claro com traço, F = preenchimento na cor do módulo, g = preenchimento neutro,
/// w = traço branco (sobre fundo colorido).
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, string> Defs = new(StringComparer.OrdinalIgnoreCase)
    {
        // ---------- Módulos
        ["mod.general"] = "F:M5,4 H27 A2,2 0 0 1 29,6 V26 A2,2 0 0 1 27,28 H5 A2,2 0 0 1 3,26 V6 A2,2 0 0 1 5,4 Z;w:M9,10 H23 M16,10 V24",
        ["mod.modeling"] = "f:M16,4 L27,10 L16,16 L5,10 Z;f:M5,10 L16,16 V28 L5,22 Z;f:M27,10 L16,16 V28 L27,22 Z;a:M16,16 V28",
        ["mod.selection"] = "d:M4,4 H22 V22 H4 Z;F:M14,12 L28,18 L22,20 L26,27 L23,28 L19,21 L14,25 Z",
        ["mod.analysis"] = "s:M13,5 A8,8 0 1 1 12.99,5 Z;a:M19,19 L27,27",
        ["mod.generators"] = "f:M10,4 L16,7.5 V14.5 L10,18 L4,14.5 V7.5 Z;F:M22,4 L28,7.5 V14.5 L22,18 L16,14.5 V7.5 Z;f:M16,14.5 L22,18 V25 L16,28.5 L10,25 V18 Z",
        ["mod.geometry"] = "f:M12,6 A8,8 0 1 1 11.99,6 Z;F:M20,10 A8,8 0 1 1 19.99,10 Z",
        ["mod.floors"] = "f:M4,22 L12,8 H28 L20,22 Z;s:M8,15 H24 M16,8 L12,22 M22,8 L16,22;a:M4,26 H20",
        ["mod.interiors"] = "f:M6,14 H26 V22 H6 Z;f:M8,8 H24 V14 H8 Z;s:M8,22 V26 M24,22 V26;F:M4,14 H8 V22 H4 Z;F:M24,14 H28 V22 H24 Z",
        ["mod.joinery"] = "f:M5,4 H27 V28 H5 Z;s:M16,4 V22 M5,22 H27;a:M12,11 V15 M20,11 V15;F:M8,24 H24 V26 H8 Z",
        ["mod.materials"] = "F:M4,6 H14 V16 H4 Z;f:M18,6 H28 V16 H18 Z;f:M4,18 H14 V28 H4 Z;F:M18,18 H28 V28 H18 Z",
        ["mod.docs"] = "f:M7,3 H20 L26,9 V29 H7 Z;s:M20,3 V9 H26;a:M11,15 H22 M11,19 H22 M11,23 H18",
        ["mod.quantities"] = "f:M4,5 H28 V27 H4 Z;s:M4,11 H28 M4,17 H28 M4,22 H28 M13,5 V27;F:M4,5 H28 V11 H4 Z",
        ["mod.quality"] = "f:M16,3 L27,7 V15 C27,22 22,27 16,29 C10,27 5,22 5,15 V7 Z;a:M11,16 L15,20 L22,12",
        ["mod.project"] = "s:M16,10 A6,6 0 1 1 15.99,10 Z;a:M16,3 V7 M16,25 V29 M3,16 H7 M25,16 H29 M7,7 L10,10 M22,22 L25,25 M7,25 L10,22 M22,10 L25,7",

        // ---------- Geral
        ["general.pane"] = "f:M4,5 H28 V27 H4 Z;F:M19,5 H28 V27 H19 Z;s:M7,10 H15 M7,14 H15 M7,18 H13",
        ["general.settings"] = "s:M16,11 A5,5 0 1 1 15.99,11 Z;a:M16,3 V8 M16,24 V29 M3,16 H8 M24,16 H29 M6.8,6.8 L10.3,10.3 M21.7,21.7 L25.2,25.2 M6.8,25.2 L10.3,21.7 M21.7,10.3 L25.2,6.8",
        ["general.help"] = "s:M16,3 A13,13 0 1 1 15.99,3 Z;a:M11.5,12 A4.5,4.5 0 1 1 16,16.5 V19;F:M14.5,22 H17.5 V25 H14.5 Z",
        ["general.about"] = "f:M4,6 H28 V24 H4 Z;s:M12,28 H20 M16,24 V28;a:M9,17 L13,13 L17,17 L23,11",

        // ---------- Modelagem
        ["modeling.distribute"] = "f:M4,10 H9 V22 H4 Z;f:M13.5,10 H18.5 V22 H13.5 Z;f:M23,10 H28 V22 H23 Z;a:M6.5,27 H25.5 M6.5,25 V29 M16,25 V29 M25.5,25 V29",
        ["modeling.rotate"] = "f:M10,10 H22 V22 H10 Z;a:M26,10 A11,11 0 0 0 6,10;a:M3,6 L6,10 L10,7",

        // ---------- Seleção
        ["selection.filter"] = "f:M4,5 H28 L19,16 V26 L13,28 V16 Z",
        ["selection.byroom"] = "s:M4,6 H28 V26 H4 Z M16,6 V16 M16,20 V26;F:M8,10 H13 V15 H8 Z;F:M20,18 H25 V23 H20 Z",

        // ---------- Análise

        // ---------- Geradores
        ["gen.pattern"] = "F:M6,6 A3,3 0 1 1 5.99,6 Z;F:M16,6 A3,3 0 1 1 15.99,6 Z;F:M26,6 A3,3 0 1 1 25.99,6 Z;F:M11,14 A3,3 0 1 1 10.99,14 Z;F:M21,14 A3,3 0 1 1 20.99,14 Z;f:M6,22 A3,3 0 1 1 5.99,22 Z;f:M16,22 A3,3 0 1 1 15.99,22 Z;f:M26,22 A3,3 0 1 1 25.99,22 Z",
        ["gen.image"] = "f:M3,5 H29 V27 H3 Z;F:M7,9 H11 V13 H7 Z;F:M15,13 H19 V17 H15 Z;F:M11,17 H15 V21 H11 Z;F:M19,9 H23 V13 H19 Z;F:M23,19 H27 V23 H23 Z",
        ["gen.brise"] = "s:M3,4 V28;F:M7,5 L11,4 V28 L7,27 Z;F:M14,5 L18,4 V28 L14,27 Z;F:M21,5 L25,4 V28 L21,27 Z",
        ["gen.spaceframe"] = "s:M3,10 H29 M3,22 H29;a:M3,10 L9,22 L15,10 L21,22 L27,10 M9,22 L3,10 M27,10 L29,14",
        ["gen.kinetic"] = "s:M3,28 H29;f:M5,6 L10,8 V24 L5,26 Z;F:M13,6 L19,10 V22 L13,26 Z;f:M22,6 L28,12 V20 L22,26 Z",
        ["gen.edit"] = "F:M6,6 A3,3 0 1 1 5.99,6 Z;F:M16,6 A3,3 0 1 1 15.99,6 Z;f:M6,16 A3,3 0 1 1 5.99,16 Z;a:M14,28 L16,22 L26,12 L30,16 L20,26 Z",
        ["gen.palette"] = "f:M16,4 C9,4 4,9 4,16 C4,23 9,28 15,28 C18,28 18,25 17,23 C16,21 18,19 21,20 C26,21 28,18 28,15 C28,9 23,4 16,4 Z;F:M10,12 A2,2 0 1 1 9.99,12 Z;F:M16,8 A2,2 0 1 1 15.99,8 Z;F:M22,11 A2,2 0 1 1 21.99,11 Z;g:M10,19 A2,2 0 1 1 9.99,19 Z",
        ["gen.colorby"] = "f:M3,6 H29 V26 H3 Z;F:M6,9 H14 V16 H6 Z;g:M18,9 H26 V16 H18 Z;f:M6,19 H14 V23 H6 Z;F:M18,19 H26 V23 H18 Z",
        ["gen.viewstyle"] = "f:M3,7 H29 V25 H3 Z;F:M3,20 L11,13 L17,18 L22,14 L29,20 V25 H3 Z;s:M22,9 A2,2 0 1 1 21.99,9 Z",

        // ---------- Geometria
        ["geo.convert"] = "d:M3,8 H13 V20;F:M19,8 H29 V24 H19 Z;a:M13,14 H18 M15,11 L18,14 L15,17",

        // ---------- Pisos
        ["floor.byroom"] = "s:M4,4 H28 V28 H4 Z;F:M7,7 H25 V25 H7 Z",
        ["floor.regions"] = "f:M3,5 H29 V27 H3 Z;F:M3,5 H15 V17 H3 Z;a:M15,5 V27 M3,17 H29",
        ["floor.border"] = "F:M3,3 H29 V29 H3 Z;f:M8,8 H24 V24 H8 Z",
        ["floor.paging"] = "f:M3,3 H29 V29 H3 Z;s:M3,11 H29 M3,19 H29 M11,3 V29 M19,3 V29;F:M3,3 H11 V11 H3 Z",
        ["floor.baseboard"] = "s:M4,4 V28 H28;F:M7,23 H28 V28 H7 Z;F:M4,4 H9 V28 H4 Z",
        ["floor.quantities"] = "f:M3,3 H20 V20 H3 Z;s:M3,11.5 H20 M11.5,3 V20;F:M22,14 H29 V29 H14 V22 H22 Z",
        ["floor.uncovered"] = "d:M4,4 H28 V28 H4 Z;a:M10,10 L22,22 M22,10 L10,22",
        ["floor.pagingviews"] = "f:M7,3 H20 L26,9 V29 H7 Z;s:M10,13 H23 M10,18 H23 M10,23 H23 M14.5,13 V27 M19,13 V27",

        // ---------- Ambientes / interiores
        ["room.number"] = "s:M4,4 H28 V28 H4 Z;a:M11,11 L14,9 V23 M18,11 C18,8 24,8 24,11 C24,14 18,18 18,23 H24",
        ["room.rename"] = "s:M4,8 H28 V24 H4 Z;a:M8,20 L11,12 L14,20 M9,17 H13 M17,12 V20 M17,12 H20 A2,2 0 0 1 20,16 H17 M17,16 H21 A2,2 0 0 1 21,20 H17",
        ["room.params"] = "f:M4,5 H28 V27 H4 Z;s:M4,12 H28 M4,19 H28 M12,5 V27;a:M15,9 H25 M15,16 H22 M15,23 H24",
        ["room.check"] = "s:M4,4 H28 V28 H4 Z;F:M14,8 H18 V18 H14 Z;F:M14,21 H18 V25 H14 Z",
        ["room.views"] = "s:M6,6 H26 V26 H6 Z;a:M16,2 V8 M16,24 V30 M2,16 H8 M24,16 H30;F:M13,13 H19 V19 H13 Z",
        ["views.organize"] = "f:M4,4 H13 V13 H4 Z;f:M4,19 H13 V28 H4 Z;a:M17,8 H28 M17,12 H24 M17,23 H28 M17,27 H24",
        ["floor.merge"] = "s:M4,8 H16 V20 H4 Z;s:M14,12 H28 V24 H14 Z;F:M14,12 H16 V20 H14 Z",
        ["floor.filler"] = "s:M4,4 H28 V28 H4 Z;F:M16,16 H28 V28 H16 Z;a:M4,16 H16 V4",
        ["furn.list"] = "s:M6,4 H26 V28 H6 Z;a:M10,10 H22 M10,16 H22 M10,22 H18",
        ["gen.select"] = "s:M5,5 H27 V27 H5 Z;F:M12,12 L24,18 L18,19 L16,25 Z",
        ["furn.distribute"] = "s:M3,3 H29 V29 H3 Z;F:M7,7 H13 V13 H7 Z;F:M19,7 H25 V13 H19 Z;F:M7,19 H13 V25 H7 Z;F:M19,19 H25 V25 H19 Z",
        ["furn.presetsave"] = "f:M4,4 H24 L28,8 V28 H4 Z;F:M9,4 H21 V11 H9 Z;s:M9,17 H23 V28 H9 Z",
        ["furn.presetapply"] = "f:M4,8 H22 V26 H4 Z;F:M8,12 H13 V17 H8 Z;F:M14,18 H19 V23 H14 Z;a:M24,6 H30 M27,3 V9",
        ["furn.alignwall"] = "g:M3,3 H8 V29 H3 Z;f:M12,10 H24 V22 H12 Z;a:M8,16 H12 M10,14 L8,16 L10,18",
        ["furn.clearance"] = "f:M10,4 H22 V12 H10 Z;d:M6,12 H26 V28 H6 Z;a:M16,14 V26",
        ["ergo.check"] = "s:M16,4 A3,3 0 1 1 15.99,4 Z;s:M16,10 V19 M9,13 H23 M16,19 L11,28 M16,19 L21,28;a:M22,22 A6,6 0 1 1 21.99,22",
        ["light.grid"] = "F:M8,8 A3,3 0 1 1 7.99,8 Z;F:M24,8 A3,3 0 1 1 23.99,8 Z;F:M8,24 A3,3 0 1 1 7.99,24 Z;F:M24,24 A3,3 0 1 1 23.99,24 Z;d:M8,8 H24 V24 H8 Z",
        ["light.overfurniture"] = "s:M16,2 V8;F:M10,8 H22 L19,14 H13 Z;a:M13,17 L11,20 M19,17 L21,20 M16,16 V20;f:M5,24 H27 V28 H5 Z",
        ["light.groups"] = "F:M7,8 A3,3 0 1 1 6.99,8 Z;F:M16,8 A3,3 0 1 1 15.99,8 Z;F:M25,8 A3,3 0 1 1 24.99,8 Z;a:M7,11 V20 H25 V11 M16,11 V20 M16,20 V28",
        ["ceil.byroom"] = "F:M3,4 H29 V10 H3 Z;s:M4,10 V28 M28,10 V28 M4,28 H28",
        ["ceil.cove"] = "F:M3,4 H29 V8 H3 Z;f:M3,8 H9 V14 H3 Z;f:M23,8 H29 V14 H23 Z;a:M10,11 L13,17 M22,11 L19,17",
        ["ceil.opening"] = "f:M3,8 H29 V16 H3 Z;F:M10,8 H22 V16 H10 Z;a:M16,18 V26 M12,22 L16,26 L20,22",
        ["wall.paint"] = "f:M4,4 H22 V28 H4 Z;F:M24,4 H28 V16 H24 Z;s:M26,16 V24;a:M8,10 L18,10 M8,15 L18,15",
        ["wall.finish"] = "g:M5,4 H14 V28 H5 Z;F:M14,4 H18 V28 H14 Z;s:M22,8 V24 M26,8 V24",
        ["wall.slats"] = "F:M5,4 H8 V28 H5 Z;F:M11,4 H14 V28 H11 Z;F:M17,4 H20 V28 H17 Z;F:M23,4 H26 V28 H23 Z;s:M3,28 H29",
        ["wall.boiserie"] = "f:M3,3 H29 V29 H3 Z;a:M7,7 H25 V14 H7 Z;a:M7,18 H25 V25 H7 Z",

        // ---------- Marcenaria
        ["join.module"] = "f:M5,4 H27 V28 H5 Z;s:M16,4 V22 M5,22 H27;a:M12,11 V15 M20,11 V15;F:M23,1 H29 V7 H23 Z",
        ["join.run"] = "g:M2,4 H30 V7 H2 Z;f:M3,10 H11 V26 H3 Z;f:M12,10 H20 V26 H12 Z;F:M21,10 H29 V26 H21 Z",
        ["join.edit"] = "f:M5,4 H23 V28 H5 Z;s:M14,4 V28;a:M14,29 L16,23 L26,13 L30,17 L20,27 Z",
        ["join.views"] = "f:M3,8 L11,4 L19,8 L11,12 Z;f:M3,8 V20 L11,24 V12 Z;f:M19,8 V20 L11,24;a:M22,14 H29 M26,11 L29,14 L26,17",
        ["join.cutlist"] = "f:M5,3 H27 V29 H5 Z;s:M9,9 H23 M9,14 H23 M9,19 H23 M9,24 H17;F:M5,3 H27 V6 H5 Z",
        ["join.nesting"] = "f:M3,5 H29 V27 H3 Z;F:M5,7 H15 V16 H5 Z;F:M17,7 H27 V12 H17 Z;F:M17,14 H22 V25 H17 Z;f:M5,18 H15 V25 H5 Z",
        ["join.rules"] = "s:M5,16 A5,5 0 1 1 4.99,16 Z;a:M10,16 H28 M24,16 V20 M28,16 V21",

        // ---------- Materiais
        ["mat.library"] = "F:M4,4 H10 V28 H4 Z;f:M11,4 H17 V28 H11 Z;F:M18,6 L23,4 L29,26 L24,28 Z",
        ["mat.byroom"] = "s:M3,4 H29 V28 H3 Z M16,4 V28;F:M6,7 H13 V14 H6 Z;g:M19,18 H26 V25 H19 Z",
        ["mat.replace"] = "F:M3,6 H13 V16 H3 Z;f:M19,16 H29 V26 H19 Z;a:M15,10 H24 V14 M21,11 L24,14 L27,11",
        ["mat.specs"] = "f:M5,3 H27 V29 H5 Z;F:M9,7 H15 V13 H9 Z;s:M18,9 H24 M9,18 H23 M9,22 H23 M9,26 H17",

        // ---------- Documentação
        ["doc.plans"] = "f:M4,4 H28 V28 H4 Z;s:M4,16 H16 V28 M16,4 V12 M22,16 H28;a:M8,8 H12",
        ["doc.sections"] = "s:M4,8 H28 M4,24 H28;a:M16,3 V29;F:M12,3 H20 L16,8 Z",
        ["doc.axo"] = "f:M16,4 L28,10 V22 L16,28 L4,22 V10 Z;s:M4,10 L16,16 L28,10 M16,16 V28",
        ["doc.sheets"] = "f:M3,6 H29 V26 H3 Z;F:M21,19 H29 V26 H21 Z;s:M6,9 H18 V22 H6 Z",
        ["doc.placeviews"] = "f:M3,6 H29 V26 H3 Z;F:M6,9 H15 V18 H6 Z;F:M17,9 H26 V15 H17 Z;s:M17,18 H26 V23 H17 Z",
        ["doc.renumber"] = "f:M6,3 H26 V29 H6 Z;a:M11,10 H21 M11,16 H21 M11,22 H17;F:M20,20 H29 V29 H20 Z",
        ["doc.dims"] = "s:M5,10 V22 M27,10 V22;a:M5,16 H27 M5,16 L9,13 M5,16 L9,19 M27,16 L23,13 M27,16 L23,19",
        ["doc.legend"] = "f:M4,4 H28 V28 H4 Z;F:M8,8 H13 V13 H8 Z;g:M8,15 H13 V20 H8 Z;F:M8,22 H13 V25 H8 Z;s:M16,10.5 H24 M16,17.5 H24 M16,23.5 H22",
        ["doc.notes"] = "f:M4,4 H28 V28 H4 Z;s:M8,10 H24 M8,15 H24 M8,20 H24 M8,25 H18;F:M4,4 H28 V7 H4 Z",

        // ---------- Quantitativos
        ["qty.schedules"] = "f:M4,5 H28 V27 H4 Z;s:M4,11 H28 M4,17 H28 M4,22 H28 M12,5 V27 M20,5 V27;F:M4,5 H28 V11 H4 Z",
        ["qty.export"] = "f:M4,5 H20 V27 H4 Z;s:M4,11 H20 M4,17 H20 M4,22 H20 M12,5 V27;a:M18,16 H29 M25,12 L29,16 L25,20",
        ["qty.budget"] = "s:M16,3 A13,13 0 1 1 15.99,3 Z;a:M20,10 C18,8 12,8 12,12 C12,16 20,15 20,20 C20,24 13,24 11,22 M16,6 V26",
        ["qty.snapshot"] = "f:M3,8 H13 V24 H3 Z;F:M19,8 H29 V24 H19 Z;a:M15,13 H17 M15,19 H17",

        // ---------- Qualidade
        ["qa.check"] = "f:M16,3 L27,7 V15 C27,22 22,27 16,29 C10,27 5,22 5,15 V7 Z;a:M11,16 L15,20 L22,12",
        ["qa.rules"] = "f:M5,3 H27 V29 H5 Z;a:M9,10 L11,12 L14,8 M9,17 L11,19 L14,15 M9,24 L11,26 L14,22;s:M17,10 H23 M17,17 H23 M17,24 H23",

        // ---------- Projeto
        ["proj.presets"] = "F:M4,6 H16 V18 H4 Z;f:M10,10 H22 V22 H10 Z;f:M16,14 H28 V26 H16 Z",
        ["proj.transfer"] = "f:M3,6 H13 V26 H3 Z;f:M19,6 H29 V26 H19 Z;a:M14,12 H18 M16,10 L18,12 L16,14 M18,20 H14 M16,18 L14,20 L16,22",
        ["proj.batch"] = "f:M3,4 H13 V14 H3 Z;f:M3,18 H13 V28 H3 Z;F:M19,11 H29 V21 H19 Z;a:M13,9 L19,14 M13,23 L19,18",
    };

    private static readonly Dictionary<string, ImageSource> Cache = new();

    public static bool Has(string key) => Defs.ContainsKey(key);

    private static (Color Stroke, Color Light) Neutral() => Theme.IsDark
        ? (Theme.ParseColor("#DCE1E7"), Theme.ParseColor("#4A5260"))
        : (Theme.ParseColor("#3A4552"), Theme.ParseColor("#E9EDF2"));

    private static DrawingGroup BuildDrawing(string key, string accentHex, double strokeWidth)
    {
        var def = Defs.TryGetValue(key, out var d) ? d : Defs["mod.general"];
        var (neutral, light) = Neutral();
        var accent = Theme.ParseColor(accentHex);
        var group = new DrawingGroup();
        // Área fixa 32×32 (garante enquadramento igual em todos os ícones).
        group.Children.Add(new GeometryDrawing(Brushes.Transparent, null, new RectangleGeometry(new Rect(0, 0, 32, 32))));
        foreach (var seg in def.Split(';'))
        {
            var idx = seg.IndexOf(':');
            if (idx < 1) continue;
            var kind = seg[0];
            Geometry geo;
            try
            {
                geo = Geometry.Parse(seg[(idx + 1)..]);
            }
            catch
            {
                continue;
            }
            Brush? fill = null;
            Pen? pen = null;
            switch (kind)
            {
                case 's':
                    pen = new Pen(new SolidColorBrush(neutral), strokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    break;
                case 'a':
                    pen = new Pen(new SolidColorBrush(accent), strokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    break;
                case 'd':
                    pen = new Pen(new SolidColorBrush(neutral), strokeWidth * 0.8) { DashStyle = new DashStyle(new[] { 2.0, 1.6 }, 0) };
                    break;
                case 'w':
                    pen = new Pen(Brushes.White, strokeWidth) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    break;
                case 'f':
                    fill = new SolidColorBrush(light);
                    pen = new Pen(new SolidColorBrush(neutral), strokeWidth * 0.75) { LineJoin = PenLineJoin.Round };
                    break;
                case 'F':
                    fill = new SolidColorBrush(accent);
                    break;
                case 'g':
                    fill = new SolidColorBrush(neutral);
                    break;
            }
            group.Children.Add(new GeometryDrawing(fill, pen, geo));
        }
        group.Freeze();
        return group;
    }

    /// <summary>Ícone vetorial (para o painel e janelas WPF).</summary>
    public static ImageSource Vector(string key, string accentHex)
    {
        var ck = $"v|{key}|{accentHex}|{Theme.IsDark}";
        if (Cache.TryGetValue(ck, out var img)) return img;
        var di = new DrawingImage(BuildDrawing(key, accentHex, 2));
        di.Freeze();
        Cache[ck] = di;
        return di;
    }

    /// <summary>Ícone rasterizado (faixa de opções do Revit): 16 ou 32 pixels.</summary>
    public static BitmapSource Bitmap(string key, string accentHex, int size)
    {
        var ck = $"b|{key}|{accentHex}|{size}|{Theme.IsDark}";
        if (Cache.TryGetValue(ck, out var img) && img is BitmapSource bs) return bs;
        var drawing = BuildDrawing(key, accentHex, size <= 16 ? 2.6 : 2.0);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(size / 32.0, size / 32.0));
            dc.DrawDrawing(drawing);
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        Cache[ck] = rtb;
        return rtb;
    }

    public static void ClearCache() => Cache.Clear();
}
