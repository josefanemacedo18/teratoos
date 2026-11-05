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
    public string Name { get; set; } = DefaultName;
    public string ReferenceNote { get; set; } = DefaultNote;

    public const string DefaultName = "ABNT NBR 9050:2020 — valores de referência (editáveis)";

    public const string DefaultNote =
        "Valores de referência da ABNT NBR 9050:2020: circulação 0,90 m (pessoa em cadeira de rodas em linha reta); " +
        "manobra 360° Ø 1,50 m; módulo de referência 0,80 × 1,20 m; vão livre de porta ≥ 0,80 m; lavatório com borda " +
        "superior entre 0,78 e 0,80 m; bancadas/superfícies de trabalho entre 0,75 e 0,85 m; faixa livre de 0,90 m ao lado " +
        "da cama; tomadas entre 0,40 e 1,00 m e interruptores entre 0,60 e 1,00 m do piso. Confira os valores no texto vigente " +
        "da norma e com o profissional responsável. Este relatório não declara conformidade.";

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
    public List<string> CounterKeywords { get; set; } = new() { "bancada", "balcão", "balcao", "counter", "mesa de trabalho" };

    /// <summary>Lavatório: borda superior (NBR 9050: 0,78 a 0,80 m).</summary>
    public double LavatoryMinHeight { get; set; } = U.M(0.78);
    public double LavatoryMaxHeight { get; set; } = U.M(0.80);
    public List<string> LavatoryKeywords { get; set; } = new() { "lavatorio", "lavatório", "lavabo de apoio", "cuba de apoio", "lavatory", "basin" };

    /// <summary>Vão livre mínimo de porta (NBR 9050: 0,80 m).</summary>
    public double MinDoorWidth { get; set; } = U.M(0.80);

    /// <summary>Faixa livre ao lado da cama, em pelo menos um lado (NBR 9050: 0,90 m).</summary>
    public double BedSideClearance { get; set; } = U.M(0.90);
    public List<string> BedKeywords { get; set; } = new() { "cama", "bed", "beliche" };

    /// <summary>Alturas de comandos (NBR 9050): tomadas 0,40–1,00 m; interruptores 0,60–1,00 m.</summary>
    public double OutletMinHeight { get; set; } = U.M(0.40);
    public double OutletMaxHeight { get; set; } = U.M(1.00);
    public double SwitchMinHeight { get; set; } = U.M(0.60);
    public double SwitchMaxHeight { get; set; } = U.M(1.00);

    public bool CheckDoorSwings { get; set; } = true;
    public bool CheckTurningSpace { get; set; } = true;
    public bool CheckCirculation { get; set; } = true;
    public bool CheckApproach { get; set; } = true;
    public bool CheckHeights { get; set; } = true;
    public bool CheckDoorWidths { get; set; } = true;
    public bool CheckBeds { get; set; } = true;
    public bool CheckControls { get; set; } = true;
}

/// <summary>Comando elétrico (tomada ou interruptor) com a altura acima do piso acabado.</summary>
public sealed record ControlPoint(long Id, string Name, bool IsSwitch, double HeightAboveFloor);

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
                            roomName, U.FormatMm(d), items[i].Name, items[j].Name, U.FormatMm(r.MinCirculation)),
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
                        roomName, items[i].Name, U.FormatMm(wallGap), U.FormatMm(r.MinCirculation)),
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
                        roomName, U.FormatMm(r.ApproachWidth), U.FormatMm(r.ApproachDepth), t.Name, why),
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
                        c.Name, U.FormatMm(h), U.FormatMm(r.CounterMinHeight), U.FormatMm(r.CounterMaxHeight)),
                    ElementIds = { c.Id },
                    Location = c.Outline.Centroid,
                });
            }
        }
        return issues;
    }

    /// <summary>Lavatórios: borda superior dentro da faixa de referência.</summary>
    public static List<ErgonomicIssue> CheckLavatories(IReadOnlyList<Footprint> lavatories, double floorZ, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var c in lavatories)
        {
            var h = c.TopZ - floorZ;
            if (h < r.LavatoryMinHeight - 1e-6 || h > r.LavatoryMaxHeight + 1e-6)
                issues.Add(new ErgonomicIssue
                {
                    Kind = Loc.T("Lavatório", "Lavatory"),
                    Message = Loc.F("\"{0}\": borda superior a {1} do piso (referência {2} a {3}).",
                        "\"{0}\": top edge {1} above floor (reference {2} to {3}).",
                        c.Name, U.FormatMm(h), U.FormatMm(r.LavatoryMinHeight), U.FormatMm(r.LavatoryMaxHeight)),
                    ElementIds = { c.Id },
                    Location = c.Outline.Centroid,
                });
        }
        return issues;
    }

    /// <summary>Portas com largura menor que o vão livre de referência.</summary>
    public static List<ErgonomicIssue> CheckDoorWidths(IReadOnlyList<(long Id, string Name, double Width, Vec2 Location)> doors, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var d in doors)
        {
            if (d.Width <= 0 || d.Width >= r.MinDoorWidth - 1e-6) continue;
            issues.Add(new ErgonomicIssue
            {
                Kind = Loc.T("Porta", "Door"),
                Message = Loc.F("Porta \"{0}\" com {1} de largura (vão livre de referência: {2}).",
                    "Door \"{0}\" is {1} wide (reference clear width: {2}).", d.Name, U.FormatMm(d.Width), U.FormatMm(r.MinDoorWidth)),
                ElementIds = { d.Id },
                Location = d.Location,
            });
        }
        return issues;
    }

    /// <summary>
    /// Camas: faixa livre (largura de referência × comprimento da cama) em pelo menos um dos lados longos,
    /// dentro do ambiente e sem outros móveis.
    /// </summary>
    public static List<ErgonomicIssue> CheckBeds(string roomName, Region2 room, IReadOnlyList<Footprint> items, IReadOnlyList<Footprint> beds, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var b in beds)
        {
            var f = b.Facing.Normalized();
            var c = b.Outline.Centroid;
            var along = b.Outline.Max(p => (p - c).Dot(f)) - b.Outline.Min(p => (p - c).Dot(f));
            var across = b.Outline.Max(p => (p - c).Dot(f.Perp)) - b.Outline.Min(p => (p - c).Dot(f.Perp));
            // lados longos: perpendiculares à direção mais longa da cama
            var sideDir = along >= across ? f.Perp : f;
            var length = Math.Max(along, across);
            Polygon2? best = null;
            foreach (var dir in new[] { sideDir, -sideDir })
            {
                var strip = PolygonOps.FrontRectangle(b.Outline, dir, length, r.BedSideClearance);
                var inside = room.IntersectionArea(strip) >= strip.Area * 0.97;
                var blocked = items.Any(o => o.Id != b.Id && PolygonOps.Intersects(o.Outline, strip));
                if (inside && !blocked)
                {
                    best = strip;
                    break;
                }
                best ??= strip;
            }
            var ok = best != null && room.IntersectionArea(best) >= best.Area * 0.97 && !items.Any(o => o.Id != b.Id && PolygonOps.Intersects(o.Outline, best));
            if (!ok)
                issues.Add(new ErgonomicIssue
                {
                    Kind = Loc.T("Cama", "Bed"),
                    Message = Loc.F("{0}: \"{1}\" não tem faixa livre de {2} em nenhum dos lados.",
                        "{0}: \"{1}\" has no {2} free strip on either side.", roomName, b.Name, U.FormatMm(r.BedSideClearance)),
                    ElementIds = { b.Id },
                    Zone = best,
                    Location = c,
                });
        }
        return issues;
    }

    /// <summary>Tomadas e interruptores fora da faixa de altura de referência.</summary>
    public static List<ErgonomicIssue> CheckControls(IReadOnlyList<ControlPoint> points, ErgonomicRules r)
    {
        var issues = new List<ErgonomicIssue>();
        foreach (var p in points)
        {
            var (min, max) = p.IsSwitch ? (r.SwitchMinHeight, r.SwitchMaxHeight) : (r.OutletMinHeight, r.OutletMaxHeight);
            if (p.HeightAboveFloor >= min - 1e-6 && p.HeightAboveFloor <= max + 1e-6) continue;
            issues.Add(new ErgonomicIssue
            {
                Kind = p.IsSwitch ? Loc.T("Interruptor", "Switch") : Loc.T("Tomada", "Outlet"),
                Message = Loc.F("\"{0}\" a {1} do piso (referência {2} a {3}).", "\"{0}\" at {1} above floor (reference {2} to {3}).",
                    p.Name, U.FormatMm(p.HeightAboveFloor), U.FormatMm(min), U.FormatMm(max)),
                ElementIds = { p.Id },
            });
        }
        return issues;
    }
}
