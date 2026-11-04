namespace TeraTools.Core.Geometry;

/// <summary>Caminho 2D parametrizado por comprimento de arco (linhas de fachada, eixos de distribuição).</summary>
public interface IPath2
{
    double Length { get; }
    Vec2 PointAt(double s);
    Vec2 TangentAt(double s);
}

/// <summary>Polilinha parametrizada por comprimento (curvas do Revit são aproximadas por polilinhas).</summary>
public sealed class PolylinePath : IPath2
{
    private readonly Vec2[] _pts;
    private readonly double[] _cum;

    public PolylinePath(IEnumerable<Vec2> points)
    {
        var list = new List<Vec2>();
        foreach (var p in points)
            if (list.Count == 0 || !list[^1].AlmostEquals(p, 1e-9))
                list.Add(p);
        if (list.Count < 2) throw new ArgumentException(Localization.Loc.T("O caminho precisa de pelo menos 2 pontos.", "Path needs at least 2 points."));
        _pts = list.ToArray();
        _cum = new double[_pts.Length];
        for (var i = 1; i < _pts.Length; i++)
            _cum[i] = _cum[i - 1] + _pts[i].DistanceTo(_pts[i - 1]);
    }

    public IReadOnlyList<Vec2> Points => _pts;

    public double Length => _cum[^1];

    private int Segment(double s)
    {
        if (s <= 0) return 0;
        if (s >= Length) return _pts.Length - 2;
        var idx = Array.BinarySearch(_cum, s);
        if (idx < 0) idx = ~idx - 1;
        return Math.Clamp(idx, 0, _pts.Length - 2);
    }

    public Vec2 PointAt(double s)
    {
        var i = Segment(s);
        var segLen = _cum[i + 1] - _cum[i];
        var t = segLen < 1e-12 ? 0 : (s - _cum[i]) / segLen;
        return Vec2.Lerp(_pts[i], _pts[i + 1], t);
    }

    public Vec2 TangentAt(double s)
    {
        var i = Segment(s);
        return (_pts[i + 1] - _pts[i]).Normalized();
    }

    /// <summary>Divide o caminho em trechos retos [s0, s1] (para elementos retos ao longo de curvas).</summary>
    public IEnumerable<(double S0, double S1)> StraightSpans()
    {
        for (var i = 0; i < _pts.Length - 1; i++)
            yield return (_cum[i], _cum[i + 1]);
    }
}
