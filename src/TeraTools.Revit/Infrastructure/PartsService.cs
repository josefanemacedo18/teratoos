using Autodesk.Revit.DB;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Infrastructure;

/// <summary>
/// Partes do Revit (divisão nativa e editável de pisos, paredes, forros e telhados).
/// Usado na paginação de revestimentos e na divisão de elementos.
/// </summary>
public static class PartsService
{
    public enum Side
    {
        Top,
        Bottom,
        All,
    }

    /// <summary>Garante partes para o elemento e devolve as partes "folha" (sem subdivisões).</summary>
    public static List<Part> EnsureParts(Document doc, Element host)
    {
        var ids = new List<ElementId> { host.Id };
        if (!PartUtils.HasAssociatedParts(doc, host.Id))
        {
            if (!PartUtils.AreElementsValidForCreateParts(doc, ids))
                throw new ToolException(Loc.F("\"{0}\" não pode ser dividido em partes.", "\"{0}\" cannot be divided into parts.", Lookup.ElementLabel(host)));
            PartUtils.CreateParts(doc, ids);
            doc.Regenerate();
        }
        return PartUtils.GetAssociatedParts(doc, host.Id, false, true)
            .Select(id => doc.GetElement(id)).OfType<Part>()
            .Where(p => !PartUtils.HasAssociatedParts(doc, p.Id))
            .ToList();
    }

    /// <summary>Escolhe a camada de acabamento: a de cima (pisos), a de baixo (forros) ou a mais próxima de um plano (paredes).</summary>
    public static List<Part> FinishLayer(List<Part> parts, Side side, Plane? nearPlane = null)
    {
        if (parts.Count <= 1 || (side == Side.All && nearPlane == null)) return parts;
        if (nearPlane != null)
        {
            var best = parts.OrderBy(p =>
            {
                var c = RevitGeo.BoxCenter(p) ?? XYZ.Zero;
                return Math.Abs((c - nearPlane.Origin).DotProduct(nearPlane.Normal));
            }).First();
            // Mantém todas as partes da mesma camada (mesmo material e alinhamento).
            var bc = RevitGeo.BoxCenter(best) ?? XYZ.Zero;
            var d0 = (bc - nearPlane.Origin).DotProduct(nearPlane.Normal);
            return parts.Where(p => Math.Abs(((RevitGeo.BoxCenter(p) ?? XYZ.Zero) - nearPlane.Origin).DotProduct(nearPlane.Normal) - d0) < 0.02).ToList();
        }
        var withBox = parts.Select(p => (P: p, B: RevitGeo.Box(p))).Where(x => x.B != null).ToList();
        if (withBox.Count == 0) return parts;
        if (side == Side.Top)
        {
            var top = withBox.Max(x => x.B!.Max.Z);
            return withBox.Where(x => Math.Abs(x.B!.Max.Z - top) < 0.003).Select(x => x.P).ToList();
        }
        var bottom = withBox.Min(x => x.B!.Min.Z);
        return withBox.Where(x => Math.Abs(x.B!.Min.Z - bottom) < 0.003).Select(x => x.P).ToList();
    }

    /// <summary>Partes finais (sem subdivisões) de um elemento.</summary>
    public static List<Part> Leaves(Document doc, ElementId host) =>
        PartUtils.GetAssociatedParts(doc, host, false, true).Select(id => doc.GetElement(id)).OfType<Part>()
            .Where(p => !PartUtils.HasAssociatedParts(doc, p.Id)).ToList();

    /// <summary>Divide as partes pelas curvas (num plano de esboço) com junta (gap) opcional.</summary>
    public static PartMaker? Divide(Document doc, ICollection<ElementId> partIds, IList<Curve> curves, Plane plane, double gap)
    {
        if (curves.Count == 0 || partIds.Count == 0) return null;
        if (!PartUtils.ArePartsValidForDivide(doc, partIds))
            throw new ToolException(Loc.T("As partes escolhidas não podem ser divididas.", "The chosen parts cannot be divided."));
        var sp = SketchPlane.Create(doc, plane);
        var maker = PartUtils.DivideParts(doc, partIds, new List<ElementId>(), curves, sp.Id);
        if (maker != null && gap > 1e-9)
        {
            var method = PartUtils.GetPartMakerMethodToDivideVolumeFW(maker);
            if (method != null) method.DivisionGap = gap;
        }
        return maker;
    }
}
