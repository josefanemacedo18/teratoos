using TeraTools.Core.Geometry;

namespace TeraTools.Core.Organic;

/// <summary>Malha fechada (sólido) de uma peça orgânica: vértices e triângulos, com a função da peça.</summary>
public sealed class OrganicMesh
{
    public string Role { get; set; } = "";
    public List<Vec3> Vertices { get; } = new();
    public List<(int A, int B, int C)> Triangles { get; } = new();

    public int Add(Vec3 v)
    {
        Vertices.Add(v);
        return Vertices.Count - 1;
    }

    public void Tri(int a, int b, int c)
    {
        if (a != b && b != c && a != c) Triangles.Add((a, b, c));
    }

    public void Quad(int a, int b, int c, int d)
    {
        Tri(a, b, c);
        Tri(a, c, d);
    }

    /// <summary>Volume pelo teorema da divergência (malha fechada e orientada para fora).</summary>
    public double Volume => Math.Abs(Triangles.Sum(t => Vertices[t.A].Dot(Vertices[t.B].Cross(Vertices[t.C]))) / 6.0);

    public double Area => Triangles.Sum(t => (Vertices[t.B] - Vertices[t.A]).Cross(Vertices[t.C] - Vertices[t.A]).Length / 2);

    /// <summary>Fechada: cada aresta é usada por exatamente dois triângulos.</summary>
    public bool IsClosed()
    {
        var edges = new Dictionary<(int, int), int>();
        foreach (var (a, b, c) in Triangles)
            foreach (var (u, v) in new[] { (a, b), (b, c), (c, a) })
            {
                var k = u < v ? (u, v) : (v, u);
                edges[k] = edges.GetValueOrDefault(k) + 1;
            }
        return edges.Count > 0 && edges.Values.All(n => n == 2);
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        var min = new Vec3(double.MaxValue, double.MaxValue, double.MaxValue);
        var max = new Vec3(double.MinValue, double.MinValue, double.MinValue);
        foreach (var v in Vertices)
        {
            min = new Vec3(Math.Min(min.X, v.X), Math.Min(min.Y, v.Y), Math.Min(min.Z, v.Z));
            max = new Vec3(Math.Max(max.X, v.X), Math.Max(max.Y, v.Y), Math.Max(max.Z, v.Z));
        }
        return (min, max);
    }
}

/// <summary>Construção de sólidos por seções (loft) e tubos ao longo de caminhos.</summary>
public static class MeshKit
{
    /// <summary>
    /// Sólido por seções: anéis fechados com a mesma quantidade de pontos, ligados em sequência; as pontas
    /// são fechadas por triangulação do próprio anel (funciona com seções côncavas, como estrelas).
    /// </summary>
    public static OrganicMesh Loft(IReadOnlyList<IReadOnlyList<Vec3>> rings, string role)
    {
        var m = new OrganicMesh { Role = role };
        if (rings.Count < 2 || rings[0].Count < 3) return m;
        var n = rings[0].Count;
        var idx = rings.Select(r => Enumerable.Range(0, n).Select(i => m.Add(r[i])).ToArray()).ToList();
        for (var k = 0; k < rings.Count - 1; k++)
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                m.Quad(idx[k][i], idx[k][j], idx[k + 1][j], idx[k + 1][i]);
            }
        Cap(m, idx[0], rings[0], reverse: true);
        Cap(m, idx[^1], rings[^1], reverse: false);
        Orient(m);
        return m;
    }

    /// <summary>Tubo de parede: superfície externa e interna (mesma quantidade de pontos), aberto nas pontas.</summary>
    public static OrganicMesh HollowLoft(IReadOnlyList<IReadOnlyList<Vec3>> outer, IReadOnlyList<IReadOnlyList<Vec3>> inner, string role)
    {
        var m = new OrganicMesh { Role = role };
        if (outer.Count < 2 || outer.Count != inner.Count || outer[0].Count < 3) return m;
        var n = outer[0].Count;
        var o = outer.Select(r => Enumerable.Range(0, n).Select(i => m.Add(r[i])).ToArray()).ToList();
        var q = inner.Select(r => Enumerable.Range(0, n).Select(i => m.Add(r[i])).ToArray()).ToList();
        var last = outer.Count - 1;
        for (var i = 0; i < n; i++)
        {
            var j = (i + 1) % n;
            for (var k = 0; k < last; k++)
            {
                m.Quad(o[k][i], o[k][j], o[k + 1][j], o[k + 1][i]);
                m.Quad(q[k][i], q[k + 1][i], q[k + 1][j], q[k][j]);
            }
            m.Quad(o[0][j], o[0][i], q[0][i], q[0][j]);
            m.Quad(o[last][i], o[last][j], q[last][j], q[last][i]);
        }
        Orient(m);
        return m;
    }

    /// <summary>Fecha um anel com triângulos (orelhas) no plano médio do anel.</summary>
    private static void Cap(OrganicMesh m, int[] ring, IReadOnlyList<Vec3> pts, bool reverse)
    {
        // normal de Newell: segue o sentido do anel
        var nrm = Vec3.Zero;
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            nrm += new Vec3((a.Y - b.Y) * (a.Z + b.Z), (a.Z - b.Z) * (a.X + b.X), (a.X - b.X) * (a.Y + b.Y));
        }
        nrm = nrm.Normalized();
        if (nrm.Length < 0.5) return;
        var u = (Math.Abs(nrm.Z) < 0.9 ? Vec3.UnitZ : Vec3.UnitX).Cross(nrm).Normalized();
        var v = nrm.Cross(u);
        var c = Centroid(pts);
        var poly = new Polygon2(pts.Select(p => new Vec2((p - c).Dot(u), (p - c).Dot(v))));
        var tris = Triangulation.EarClip(poly);
        if (tris.Count < pts.Count - 2)
        {
            // anel muito torto: leque a partir do centro (melhor do que deixar aberto)
            var ci = m.Add(c);
            for (var i = 0; i < ring.Length; i++)
            {
                var a = ring[i];
                var b = ring[(i + 1) % ring.Length];
                if (reverse) m.Tri(ci, b, a);
                else m.Tri(ci, a, b);
            }
            return;
        }
        foreach (var (a, b, cc) in tris)
        {
            if (reverse) m.Tri(ring[a], ring[cc], ring[b]);
            else m.Tri(ring[a], ring[b], ring[cc]);
        }
    }

    /// <summary>Tubo de seção poligonal ao longo de um caminho (transporte paralelo do referencial).</summary>
    public static OrganicMesh Tube(IReadOnlyList<Vec3> path, double radius, string role, int sides = 6, bool closedPath = false)
    {
        var pts = Dedup(path);
        if (closedPath && pts.Count > 2 && pts[0].DistanceTo(pts[^1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 2 || radius <= 0) return new OrganicMesh { Role = role };
        var frames = Frames(pts, closedPath);
        var rings = new List<IReadOnlyList<Vec3>>();
        for (var k = 0; k < pts.Count; k++)
        {
            var (n, b) = frames[k];
            var ring = new List<Vec3>(sides);
            for (var i = 0; i < sides; i++)
            {
                var a = 2 * Math.PI * i / sides;
                ring.Add(pts[k] + n * (radius * Math.Cos(a)) + b * (radius * Math.Sin(a)));
            }
            rings.Add(ring);
        }
        if (!closedPath) return Loft(rings, role);
        // anel fechado (toro): liga o último ao primeiro, sem tampas
        var m = new OrganicMesh { Role = role };
        var idx = rings.Select(r => r.Select(m.Add).ToArray()).ToList();
        for (var k = 0; k < idx.Count; k++)
        {
            var k2 = (k + 1) % idx.Count;
            for (var i = 0; i < sides; i++)
            {
                var j = (i + 1) % sides;
                m.Quad(idx[k][i], idx[k][j], idx[k2][j], idx[k2][i]);
            }
        }
        Orient(m);
        return m;
    }

    /// <summary>Referenciais ao longo do caminho (normal e binormal), sem torção brusca.</summary>
    public static List<(Vec3 N, Vec3 B)> Frames(IReadOnlyList<Vec3> pts, bool closed = false)
    {
        var list = new List<(Vec3, Vec3)>(pts.Count);
        Vec3 T(int k)
        {
            var a = pts[Math.Max(0, k - 1)];
            var b = pts[Math.Min(pts.Count - 1, k + 1)];
            if (closed)
            {
                a = pts[(k - 1 + pts.Count) % pts.Count];
                b = pts[(k + 1) % pts.Count];
            }
            return (b - a).Normalized();
        }
        var t0 = T(0);
        var refv = Math.Abs(t0.Z) < 0.9 ? Vec3.UnitZ : Vec3.UnitX;
        var n = t0.Cross(refv).Normalized();
        for (var k = 0; k < pts.Count; k++)
        {
            var t = T(k);
            // transporte paralelo: remove de n a componente na direção da tangente
            n = (n - t * n.Dot(t)).Normalized();
            if (n.Length < 1e-9) n = t.Cross(refv).Normalized();
            list.Add((n, t.Cross(n).Normalized()));
        }
        return list;
    }

    public static Vec3 Centroid(IReadOnlyList<Vec3> pts)
    {
        var s = Vec3.Zero;
        foreach (var p in pts) s += p;
        return s * (1.0 / pts.Count);
    }

    private static List<Vec3> Dedup(IReadOnlyList<Vec3> path)
    {
        var list = new List<Vec3>();
        foreach (var p in path)
            if (list.Count == 0 || list[^1].DistanceTo(p) > 1e-6) list.Add(p);
        return list;
    }

    /// <summary>Garante normais para fora (volume com sinal positivo).</summary>
    private static void Orient(OrganicMesh m)
    {
        var signed = m.Triangles.Sum(t => m.Vertices[t.A].Dot(m.Vertices[t.B].Cross(m.Vertices[t.C])));
        if (signed >= 0) return;
        for (var i = 0; i < m.Triangles.Count; i++)
        {
            var (a, b, c) = m.Triangles[i];
            m.Triangles[i] = (a, c, b);
        }
    }

    public static double PathLength(IReadOnlyList<Vec3> path)
    {
        double len = 0;
        for (var i = 1; i < path.Count; i++) len += path[i].DistanceTo(path[i - 1]);
        return len;
    }
}
