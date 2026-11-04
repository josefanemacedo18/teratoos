using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;
using TeraTools.Core.Reporting;
using TeraTools.Core.Units;

namespace TeraTools.Core.Checks;

/// <summary>Pegada (projeção em planta) de um elemento para verificações geométricas.</summary>
public sealed class Footprint
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public Polygon2 Outline { get; set; } = Polygon2.Empty;
    public double BottomZ { get; set; }
    public double TopZ { get; set; }
    public Vec2 Facing { get; set; } = Vec2.UnitY;
}

/// <summary>
/// Regras de ergonomia/acessibilidade EDITÁVEIS. Os valores padrão são apenas referências
/// e devem ser conferidos pelo profissional com a norma vigente aplicável ao projeto.
/// </summary>
public sealed class ErgonomicRules
{
    public string Name { get; set; } = "Referência (editável)";
    public string ReferenceNote { get; set; } =
        "Valores de referência editáveis. Confirme com a norma vigente (ex.: ABNT NBR 9050) e com o profissional responsável. " +
        "Este relatório não declara conformidade.";

    public double MinCirculation { get; set; } = U.M(0.90);
    public double IgnoreGapsBelow { get; set; } = U.M(0.05);
    public double TurningDiameter { get; set; } = U.M(1.50);
    public double ApproachWidth { get; set; } = U.M(0.80);
    public double ApproachDepth { get; set; } = U.M(1.20);

    /// <summary>Categorias (nomes BuiltInCategory) que exigem área de aproximação frontal.</summary>
    public List<string> ApproachCategories { get; set; } = new() { "OST_PlumbingFixtures" };

    public double CounterMinHeight { get; set; } = U.M(0.75);
    public double CounterMaxHeight { get; set; } = U.M(0.85);

    /// <summary>Palavras que identificam bancadas/superfícies de trabalho pelo nome da família ou tipo.</summary>
    public List<string> CounterKeywords { get; set; } = new() { "bancada", "balcão", "balcao", "counter", "lavatório", "lavatorio" };

    public bool CheckDoorSwings { get; set; } = true;
    public bool CheckTurningSpace { get; set; } = true;
    public bool CheckCirculation { get; set; } = true;
    public bool CheckApproach { get; set; } = true;
    public bool CheckHeights { get; set; } = true;
}

public sealed class ErgonomicIssue
{
    public string Kind { get; set; } = "";
    public string Message { get; set; } = "";
    public Severity Severity { get; set; } = Severity.Warning;
    public List<long> ElementIds { get; set; } = new();
    public Vec2? Location { get; set; }
    public Polygon2? Zone { get; set; }
}

public static class ErgonomicChecker
{
    public static List<ErgonomicIssue> CheckCirculation(string roomName, Region2 room, IReadOnlyList<Footprint> items, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        for (var i = 0; i < items.Count; i++)
        {
            for (var j = i + 1; j < items.Count; j++)
            {
                var d = PolygonOps.Distance(items[i].Outline, items[j].Outline);
                if (d > r.IgnoreGapsBelow && d < r.MinCirculation)
                {
                    issues.Add(new ErgonomicIssue
                    {
                        Kind = Loc.T("Circulação", "Circulation"),
                        Message = Loc.F("{0}: passagem de {1} entre \"{2}\" e \"{3}\" (referência: {4}).",
                            "{0}: {1} passage between \"{2}\" and \"{3}\" (reference: {4}).",
                            roomName, U.FormatCm(d), items[i].Name, items[j].Name, U.FormatCm(r.MinCirculation)),
                        ElementIds = { items[i].Id, items[j].Id },
                        Location = (items[i].Outline.Centroid + items[j].Outline.Centroid) * 0.5,
                    });
                }
            }
            var wallGap = items[i].Outline.Min(p => room.DistanceToBoundary(p));
            if (items[i].Outline.All(p => room.Contains(p)) && wallGap > r.IgnoreGapsBelow && wallGap < r.MinCirculation)
            {
                issues.Add(new ErgonomicIssue
                {
                    Kind = Loc.T("Circulação", "Circulation"),
                    Message = Loc.F("{0}: \"{1}\" deixa {2} até a parede (referência: {3}).",
                        "{0}: \"{1}\" leaves {2} to the wall (reference: {3}).",
                        roomName, items[i].Name, U.FormatCm(wallGap), U.FormatCm(r.MinCirculation)),
                    ElementIds = { items[i].Id },
                    Severity = Severity.Info,
                    Location = items[i].Outline.Centroid,
                });
            }
        }
        return issues;
    }

    /// <summary>Procura um círculo livre de diâmetro D (espaço de manobra) no ambiente.</summary>
    public static (bool Found, Vec2 Center, double Clearance) FindTurningSpace(Region2 room, IReadOnlyList<Footprint> obstacles, double diameter, double step)
    {
        var rad = diameter / 2;
        var b = room.Bounds;
        step = Math.Max(step, diameter / 20);
        var best = (Found: false, Center: Vec2.Zero, Clearance: 0.0);
        for (var y = b.MinY + rad; y <= b.MaxY - rad + 1e-9; y += step)
        {
            for (var x = b.MinX + rad; x <= b.MaxX - rad + 1e-9; x += step)
            {
                var c = new Vec2(x, y);
                if (!room.Contains(c)) continue;
                var clearance = room.DistanceToBoundary(c);
                if (clearance < rad) continue;
                foreach (var o in obstacles)
                {
                    clearance = Math.Min(clearance, PolygonOps.Distance(c, o.Outline));
                    if (clearance < rad) break;
                }
                if (clearance >= rad && clearance > best.Clearance)
                    best = (true, c, clearance);
            }
        }
        return best;
    }

    public static List<ErgonomicIssue> CheckApproach(string roomName, Region2 room, IReadOnlyList<Footprint> items, IReadOnlyList<Footprint> targets, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var t in targets)
        {
            var zone = PolygonOps.FrontRectangle(t.Outline, t.Facing, r.ApproachWidth, r.ApproachDepth);
            var inside = room.IntersectionArea(zone) >= zone.Area * 0.98;
            var blockers = items.Where(o => o.Id != t.Id && PolygonOps.Intersects(o.Outline, zone)).ToList();
            if (!inside || blockers.Count > 0)
            {
                var ids = new List<long> { t.Id };
                ids.AddRange(blockers.Select(bk => bk.Id));
                var why = !inside
                    ? Loc.T("a área sai do ambiente", "the area leaves the room")
                    : Loc.F("obstruída por {0}", "blocked by {0}", string.Join(", ", blockers.Select(bk => "\"" + bk.Name + "\"")));
                issues.Add(new ErgonomicIssue
                {
                    Kind = Loc.T("Aproximação", "Approach"),
                    Message = Loc.F("{0}: área de aproximação de {1} × {2} em frente a \"{3}\" — {4}.",
                        "{0}: {1} × {2} approach area in front of \"{3}\" — {4}.",
                        roomName, U.FormatCm(r.ApproachWidth), U.FormatCm(r.ApproachDepth), t.Name, why),
                    ElementIds = ids,
                    Zone = zone,
                    Location = zone.Centroid,
                });
            }
        }
        return issues;
    }

    public static List<ErgonomicIssue> CheckDoorSwings(IReadOnlyList<(long DoorId, string Name, Polygon2 Swing)> swings, IReadOnlyList<Footprint> items)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var (doorId, name, swing) in swings)
        {
            foreach (var it in items)
            {
                if (it.Id == doorId) continue;
                if (it.Outline.ClipByConvex(swing).Area > U.M2ToSqFt(0.005))
                {
                    issues.Add(new ErgonomicIssue
                    {
                        Kind = Loc.T("Abertura de porta", "Door swing"),
                        Message = Loc.F("A abertura da porta \"{0}\" interfere com \"{1}\".", "Door \"{0}\" swing interferes with \"{1}\".", name, it.Name),
                        ElementIds = { doorId, it.Id },
                        Zone = swing,
                        Location = swing.Centroid,
                    });
                }
            }
        }
        return issues;
    }

    public static List<ErgonomicIssue> CheckHeights(IReadOnlyList<Footprint> counters, double floorZ, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var c in counters)
        {
            var h = c.TopZ - floorZ;
            if (h < r.CounterMinHeight - 1e-6 || h > r.CounterMaxHeight + 1e-6)
            {
                issues.Add(new ErgonomicIssue
                {
                    Kind = Loc.T("Altura", "Height"),
                    Message = Loc.F("\"{0}\" com altura {1} (faixa de referência {2} a {3}).",
                        "\"{0}\" height {1} (reference range {2} to {3}).",
                        c.Name, U.FormatCm(h), U.FormatCm(r.CounterMinHeight), U.FormatCm(r.CounterMaxHeight)),
                    ElementIds = { c.Id },
                    Location = c.Outline.Centroid,
                });
            }
        }
        return issues;
    }
}
