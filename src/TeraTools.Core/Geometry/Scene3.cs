namespace TeraTools.Core.Geometry;

/// <summary>Malha de triângulos de uma cor (usada pela pré-visualização 3D).</summary>
public sealed class Mesh3
{
    public string Color { get; init; } = "#BFBFBF";
    public double Opacity { get; init; } = 1;
    public List<Vec3> Positions { get; } = new();
    public List<int> Indices { get; } = new();
    public int TriangleCount => Indices.Count / 3;

    public void AddTriangle(Vec3 a, Vec3 b, Vec3 c)
    {
        var i = Positions.Count;
        Positions.Add(a);
        Positions.Add(b);
        Positions.Add(c);
        Indices.Add(i);
        Indices.Add(i + 1);
        Indices.Add(i + 2);
    }

    public void AddQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d)
    {
        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }
}

/// <summary>
/// Cena 3D simples e independente do Revit: caixas, prismas de qualquer polígono, esferas, cilindros,
/// faces de referência e linhas. A interface converte isso em um visualizador 3D interativo.
/// </summary>
public sealed class Scene3
{
    private readonly Dictionary<(string, double), Mesh3> _meshes = new();

    public IEnumerable<Mesh3> Meshes => _meshes.Values;
    public List<(Vec3 A, Vec3 B, string Color)> Lines { get; } = new();

    /// <summary>Limite de triângulos (acima dele os itens seguintes são ignorados e <see cref="Truncated"/> fica verdadeiro).</summary>
    public int MaxTriangles { get; set; } = 400_000;
    public bool Truncated { get; private set; }
    public int TriangleCount { get; private set; }
    public int ItemCount { get; private set; }
    public bool IsEmpty => TriangleCount == 0 && Lines.Count == 0;

    public Mesh3 Mesh(string color, double opacity = 1)
    {
        var key = (color.ToUpperInvariant(), Math.Round(opacity, 2));
        if (!_meshes.TryGetValue(key, out var m)) _meshes[key] = m = new Mesh3 { Color = color, Opacity = opacity };
        return m;
    }

    private bool Reserve(int triangles)
    {
        if (TriangleCount + triangles > MaxTriangles)
        {
            Truncated = true;
            return false;
        }
        TriangleCount += triangles;
        ItemCount++;
        return true;
    }

    public Scene3 Box(OrientedBox b, string color, double opacity = 1)
    {
        if (b.SizeX <= 0 || b.SizeY <= 0 || b.SizeZ <= 0 || !Reserve(12)) return this;
        var f = b.Frame;
        Vec3 P(double x, double y, double z) => f.ToWorld(x * b.SizeX, y * b.SizeY, z * b.SizeZ);
        var m = Mesh(color, opacity);
        Vec3 p000 = P(0, 0, 0), p100 = P(1, 0, 0), p110 = P(1, 1, 0), p010 = P(0, 1, 0);
        Vec3 p001 = P(0, 0, 1), p101 = P(1, 0, 1), p111 = P(1, 1, 1), p011 = P(0, 1, 1);
        m.AddQuad(p000, p010, p110, p100); // base
        m.AddQuad(p001, p101, p111, p011); // topo
        m.AddQuad(p000, p100, p101, p001);
        m.AddQuad(p100, p110, p111, p101);
        m.AddQuad(p110, p010, p011, p111);
        m.AddQuad(p010, p000, p001, p011);
        return this;
    }

    /// <summary>Prisma: polígono no plano XY do sistema <paramref name="f"/>, extrudado de z0 até z0+depth.</summary>
    public Scene3 Prism(Frame3 f, Polygon2 poly, double z0, double depth, string color, double opacity = 1)
    {
        if (poly.IsEmpty || Math.Abs(depth) < 1e-9) return this;
        var p = poly.EnsureCounterClockwise();
        var tris = Triangulation.EarClip(p);
        if (tris.Count == 0 || !Reserve(tris.Count * 2 + p.Count * 2)) return this;
        var m = Mesh(color, opacity);
        Vec3 W(Vec2 v, double z) => f.ToWorld(v.X, v.Y, z);
        var z1 = z0 + depth;
        foreach (var (a, b, c) in tris)
        {
            m.AddTriangle(W(p[a], z0), W(p[c], z0), W(p[b], z0));
            m.AddTriangle(W(p[a], z1), W(p[b], z1), W(p[c], z1));
        }
        for (var i = 0; i < p.Count; i++)
        {
            var a = p[i];
            var b = p[(i + 1) % p.Count];
            m.AddQuad(W(a, z0), W(b, z0), W(b, z1), W(a, z1));
        }
        return this;
    }

    /// <summary>Face plana (contorno externo) — usada para mostrar a parede/piso hospedeiro, semitransparente.</summary>
    public Scene3 Face(Frame3 f, Polygon2 outline, string color, double opacity = 0.35, double z = 0)
    {
        if (outline.IsEmpty) return this;
        var p = outline.EnsureCounterClockwise();
        var tris = Triangulation.EarClip(p);
        if (tris.Count == 0 || !Reserve(tris.Count * 2)) return this;
        var m = Mesh(color, opacity);
        foreach (var (a, b, c) in tris)
        {
            var A = f.ToWorld(p[a].X, p[a].Y, z);
            var B = f.ToWorld(p[b].X, p[b].Y, z);
            var C = f.ToWorld(p[c].X, p[c].Y, z);
            m.AddTriangle(A, B, C);
            m.AddTriangle(A, C, B);
        }
        for (var i = 0; i < p.Count; i++)
            Line(f.ToWorld(p[i].X, p[i].Y, z), f.ToWorld(p[(i + 1) % p.Count].X, p[(i + 1) % p.Count].Y, z), "#5F6B7A");
        return this;
    }

    public Scene3 Sphere(Vec3 c, double r, string color, int segments = 10)
    {
        if (r <= 0) return this;
        var rings = Math.Max(3, segments / 2);
        if (!Reserve(segments * rings * 2)) return this;
        var m = Mesh(color);
        Vec3 P(int i, int j)
        {
            var th = Math.PI * i / rings;
            var ph = 2 * Math.PI * j / segments;
            return c + new Vec3(Math.Sin(th) * Math.Cos(ph), Math.Sin(th) * Math.Sin(ph), Math.Cos(th)) * r;
        }
        for (var i = 0; i < rings; i++)
            for (var j = 0; j < segments; j++)
                m.AddQuad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1));
        return this;
    }

    public Scene3 Cylinder(Vec3 a, Vec3 b, double r, string color, int segments = 8)
    {
        var axis = b - a;
        if (axis.Length < 1e-9 || r <= 0 || !Reserve(segments * 2)) return this;
        var u = axis.AnyPerpendicular().Normalized();
        var v = axis.Normalized().Cross(u);
        var m = Mesh(color);
        for (var k = 0; k < segments; k++)
        {
            var t0 = 2 * Math.PI * k / segments;
            var t1 = 2 * Math.PI * (k + 1) / segments;
            var d0 = (u * Math.Cos(t0) + v * Math.Sin(t0)) * r;
            var d1 = (u * Math.Cos(t1) + v * Math.Sin(t1)) * r;
            m.AddQuad(a + d0, a + d1, b + d1, b + d0);
        }
        return this;
    }

    public Scene3 Line(Vec3 a, Vec3 b, string color = "#5F6B7A")
    {
        if (Lines.Count < 20000) Lines.Add((a, b, color));
        return this;
    }

    /// <summary>Caixa envolvente de toda a cena (para enquadrar a câmera).</summary>
    public (Vec3 Min, Vec3 Max)? Bounds()
    {
        double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
        void Acc(Vec3 p)
        {
            x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
            x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
        }
        foreach (var m in _meshes.Values) foreach (var p in m.Positions) Acc(p);
        foreach (var (a, b, _) in Lines) { Acc(a); Acc(b); }
        if (x0 == double.MaxValue) return null;
        return (new Vec3(x0, y0, z0), new Vec3(x1, y1, z1));
    }
}

/// <summary>Triangulação de polígonos simples (inclusive côncavos, como estrelas) por remoção de orelhas.</summary>
public static class Triangulation
{
    public static List<(int A, int B, int C)> EarClip(Polygon2 poly)
    {
        var result = new List<(int, int, int)>();
        var n = poly.Count;
        if (n < 3) return result;
        var idx = Enumerable.Range(0, n).ToList();
        if (poly.SignedArea < 0) idx.Reverse();
        var guard = 0;
        while (idx.Count > 3 && guard++ < n * n)
        {
            var clipped = false;
            for (var i = 0; i < idx.Count; i++)
            {
                int ia = idx[(i - 1 + idx.Count) % idx.Count], ib = idx[i], ic = idx[(i + 1) % idx.Count];
                Vec2 a = poly[ia], b = poly[ib], c = poly[ic];
                if ((b - a).Cross(c - b) <= 1e-12) continue; // vértice reflexo
                var inside = false;
                foreach (var j in idx)
                {
                    if (j == ia || j == ib || j == ic) continue;
                    if (PointInTriangle(poly[j], a, b, c)) { inside = true; break; }
                }
                if (inside) continue;
                result.Add((ia, ib, ic));
                idx.RemoveAt(i);
                clipped = true;
                break;
            }
            if (!clipped) break; // polígono degenerado: devolve o que conseguiu
        }
        if (idx.Count == 3) result.Add((idx[0], idx[1], idx[2]));
        return result;
    }

    private static bool PointInTriangle(Vec2 p, Vec2 a, Vec2 b, Vec2 c)
    {
        var d1 = (b - a).Cross(p - a);
        var d2 = (c - b).Cross(p - b);
        var d3 = (a - c).Cross(p - c);
        var neg = d1 < -1e-12 || d2 < -1e-12 || d3 < -1e-12;
        var pos = d1 > 1e-12 || d2 > 1e-12 || d3 > 1e-12;
        return !(neg && pos);
    }
}
