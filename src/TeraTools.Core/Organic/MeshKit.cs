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
        return closedPath ? RingLoop(rings, role) : Loft(rings, role);
    }

    /// <summary>Anéis ligados em laço (o último volta ao primeiro), sem tampas — tubos e vigas fechados.</summary>
    public static OrganicMesh RingLoop(IReadOnlyList<IReadOnlyList<Vec3>> rings, string role)
    {
        var m = new OrganicMesh { Role = role };
        if (rings.Count < 3 || rings[0].Count < 3) return m;
        var n = rings[0].Count;
        var idx = rings.Select(r => Enumerable.Range(0, n).Select(i => m.Add(r[i])).ToArray()).ToList();
        for (var k = 0; k < idx.Count; k++)
        {
            var k2 = (k + 1) % idx.Count;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                m.Quad(idx[k][i], idx[k][j], idx[k2][j], idx[k2][i]);
            }
        }
        Orient(m);
        return m;
    }

    /// <summary>
    /// Varredura de um perfil 2D ao longo de um caminho, mantendo o "para cima" do perfil alinhado a
    /// <paramref name="up"/> (vigas com alma vertical que acompanham uma superfície). O perfil usa
    /// X = lateral, Y = para cima.
    /// </summary>
    public static OrganicMesh Sweep(IReadOnlyList<Vec3> path, IReadOnlyList<Vec2> profile, Vec3 up, string role, bool closedPath = false)
    {
        var pts = Dedup(path);
        if (closedPath && pts.Count > 2 && pts[0].DistanceTo(pts[^1]) < 1e-6) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < 2 || profile.Count < 3) return new OrganicMesh { Role = role };
        var frames = Frames(pts, closedPath);
        var rings = new List<IReadOnlyList<Vec3>>(pts.Count);
        for (var k = 0; k < pts.Count; k++)
        {
            var a = pts[closedPath ? (k - 1 + pts.Count) % pts.Count : Math.Max(0, k - 1)];
            var b = pts[closedPath ? (k + 1) % pts.Count : Math.Min(pts.Count - 1, k + 1)];
            var t = (b - a).Normalized();
            var side = t.Cross(up);
            Vec3 u;
            if (side.Length < 1e-6)
            {
                side = frames[k].N;
                u = frames[k].B;
            }
            else
            {
                side = side.Normalized();
                u = side.Cross(t).Normalized();
            }
            var p = pts[k];
            rings.Add(profile.Select(q => p + side * q.X + u * q.Y).ToList());
        }
        return closedPath ? RingLoop(rings, role) : Loft(rings, role);
    }

    /// <summary>Esfera (para nós e juntas de galhos).</summary>
    public static OrganicMesh Sphere(Vec3 c, double r, string role, int segments = 12)
    {
        var m = new OrganicMesh { Role = role };
        if (r <= 0) return m;
        var rings = Math.Max(3, segments / 2);
        var top = m.Add(c + Vec3.UnitZ * r);
        var bottom = m.Add(c - Vec3.UnitZ * r);
        var idx = new int[rings - 1, segments];
        for (var i = 1; i < rings; i++)
        {
            var th = Math.PI * i / rings;
            for (var j = 0; j < segments; j++)
            {
                var ph = 2 * Math.PI * j / segments;
                idx[i - 1, j] = m.Add(c + new Vec3(Math.Sin(th) * Math.Cos(ph), Math.Sin(th) * Math.Sin(ph), Math.Cos(th)) * r);
            }
        }
        for (var j = 0; j < segments; j++)
        {
            var j2 = (j + 1) % segments;
            m.Tri(top, idx[0, j], idx[0, j2]);
            m.Tri(bottom, idx[rings - 2, j2], idx[rings - 2, j]);
            for (var i = 0; i < rings - 2; i++) m.Quad(idx[i, j], idx[i + 1, j], idx[i + 1, j2], idx[i, j2]);
        }
        Orient(m);
        return m;
    }

    /// <summary>
    /// Placa que acompanha uma superfície z = f(x, y): o contorno é recortado numa malha de células,
    /// triangulado, e ganha espessura para baixo. Resultado fechado (vértices compartilhados).
    /// </summary>
    public static OrganicMesh SurfaceSlab(Polygon2 region, double cell, Func<Vec2, double> z, double thickness, string role, double rotation = 0)
    {
        var poly = region.EnsureCounterClockwise();
        if (poly.IsEmpty) return new OrganicMesh { Role = role };
        return SurfaceSlab(ConvexPieces(poly), cell, z, thickness, role, rotation);
    }

    /// <summary>Triângulos da planta (peças convexas), para recortes exatos por células convexas.</summary>
    public static List<Polygon2> ConvexPieces(Polygon2 region)
    {
        var poly = region.EnsureCounterClockwise();
        return Triangulation.EarClip(poly).Select(t => new Polygon2(new[] { poly[t.A], poly[t.B], poly[t.C] })).ToList();
    }

    /// <summary>Placa sobre uma região dada por peças convexas (que se encostam sem se sobrepor).</summary>
    public static OrganicMesh SurfaceSlab(IReadOnlyList<Polygon2> pieces, double cell, Func<Vec2, double> z, double thickness, string role, double rotation = 0)
    {
        var m = new OrganicMesh { Role = role };
        var all = pieces.Where(p => !p.IsEmpty).ToList();
        if (all.Count == 0 || cell <= 0) return m;
        var poly = new Polygon2(all.SelectMany(p => p));
        var c = new Vec2(poly.Average(p => p.X), poly.Average(p => p.Y));
        var ax = Vec2.FromAngle(rotation);
        var ay = ax.Perp;
        double u0 = double.MaxValue, u1 = double.MinValue, v0 = double.MaxValue, v1 = double.MinValue;
        foreach (var p in poly)
        {
            var d = p - c;
            u0 = Math.Min(u0, d.Dot(ax)); u1 = Math.Max(u1, d.Dot(ax));
            v0 = Math.Min(v0, d.Dot(ay)); v1 = Math.Max(v1, d.Dot(ay));
        }
        var nu = Math.Max(1, (int)Math.Ceiling((u1 - u0) / cell - 1e-9));
        var nv = Math.Max(1, (int)Math.Ceiling((v1 - v0) / cell - 1e-9));
        var du = (u1 - u0) / nu;
        var dv = (v1 - v0) / nv;
        var keys = new Dictionary<(long, long), int>();
        var top = new List<(int, int, int)>();
        // pontos a menos de 1 mm viram um só (o recorte pela borda cria lascas que o Revit recusaria)
        var snap = 1 / 304.8;
        int Vertex(Vec2 p)
        {
            var k = ((long)Math.Round(p.X / snap), (long)Math.Round(p.Y / snap));
            if (!keys.TryGetValue(k, out var i)) keys[k] = i = m.Add(new Vec3(p.X, p.Y, z(p)));
            return i;
        }
        Vec2 W(double u, double v) => c + ax * u + ay * v;
        // A planta é triangulada uma vez; cada triângulo (convexo) é recortado pelas células (convexas).
        // Recortar convexo por convexo é exato — recortar a planta côncava direto criaria pontes de largura zero.
        var outline = all;
        var local = outline.Select(t =>
        {
            double a = double.MaxValue, b = double.MinValue, e = double.MaxValue, g = double.MinValue;
            foreach (var p in t)
            {
                var d = p - c;
                a = Math.Min(a, d.Dot(ax)); b = Math.Max(b, d.Dot(ax));
                e = Math.Min(e, d.Dot(ay)); g = Math.Max(g, d.Dot(ay));
            }
            return (U0: a, U1: b, V0: e, V1: g);
        }).ToList();
        for (var i = 0; i < nu; i++)
            for (var j = 0; j < nv; j++)
            {
                double a0 = u0 + i * du, a1 = u0 + (i + 1) * du, b0 = v0 + j * dv, b1 = v0 + (j + 1) * dv;
                var cellPoly = new Polygon2(new[] { W(a0, b0), W(a1, b0), W(a1, b1), W(a0, b1) });
                for (var k = 0; k < outline.Count; k++)
                {
                    var bb = local[k];
                    if (bb.U1 < a0 - 1e-9 || bb.U0 > a1 + 1e-9 || bb.V1 < b0 - 1e-9 || bb.V0 > b1 + 1e-9) continue;
                    var piece = outline[k].ClipByConvex(cellPoly);
                    if (piece.IsEmpty || piece.Area < 1e-8) continue;
                    piece = piece.EnsureCounterClockwise();
                    var ids = piece.Select(Vertex).ToArray();
                    for (var q = 1; q + 1 < ids.Length; q++)
                        if (ids[0] != ids[q] && ids[q] != ids[q + 1] && ids[0] != ids[q + 1]) top.Add((ids[0], ids[q], ids[q + 1]));
                }
            }
        if (top.Count == 0) return m;
        var nTop = m.Vertices.Count;
        for (var i = 0; i < nTop; i++) m.Add(m.Vertices[i] - Vec3.UnitZ * thickness);
        foreach (var (a, b, cc) in top)
        {
            m.Tri(a, b, cc);
            m.Tri(a + nTop, cc + nTop, b + nTop);
        }
        // bordas livres (usadas por um triângulo só) viram paredes laterais
        var edges = new Dictionary<(int, int), int>();
        foreach (var (a, b, cc) in top)
            foreach (var (p, q) in new[] { (a, b), (b, cc), (cc, a) })
            {
                var k = p < q ? (p, q) : (q, p);
                edges[k] = edges.GetValueOrDefault(k) + 1;
            }
        foreach (var (a, b, cc) in top)
            foreach (var (p, q) in new[] { (a, b), (b, cc), (cc, a) })
            {
                var k = p < q ? (p, q) : (q, p);
                if (edges[k] == 1) m.Quad(q, p, p + nTop, q + nTop);
            }
        Orient(m);
        return m;
    }

    /// <summary>Pontos de uma curva de Bézier quadrática.</summary>
    public static List<Vec3> Bezier(Vec3 a, Vec3 control, Vec3 b, int samples = 12)
    {
        var list = new List<Vec3>(samples + 1);
        for (var i = 0; i <= samples; i++)
        {
            var t = (double)i / samples;
            list.Add(a * ((1 - t) * (1 - t)) + control * (2 * (1 - t) * t) + b * (t * t));
        }
        return list;
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
