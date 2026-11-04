using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using TeraTools.Core.Geometry;
using TeraTools.Core.Localization;

namespace TeraTools.Revit.Ui;

/// <summary>
/// Visualizador 3D da pré-visualização: botão esquerdo gira, botão direito/do meio desloca,
/// roda aproxima, duplo clique enquadra. Mantém a câmera entre atualizações das opções.
/// </summary>
public sealed class Preview3D : Grid
{
    private readonly Viewport3D _viewport = new() { ClipToBounds = true };
    private readonly PerspectiveCamera _camera = new() { FieldOfView = 40, UpDirection = new Vector3D(0, 0, 1), NearPlaneDistance = 0.01, FarPlaneDistance = 1e7 };
    private readonly ModelVisual3D _content = new();
    private readonly TextBlock _hint;
    private Scene3? _scene;
    private Point3D _target;
    private double _azimuth = -55, _elevation = 30, _distance = 10, _radius = 1;
    private Point _last;
    private bool _rotating, _panning, _fitted;
    private Vec3 _center;

    public Preview3D()
    {
        Background = Theme.Get("TT.Surface");
        _viewport.Camera = _camera;
        _viewport.Children.Add(new ModelVisual3D
        {
            Content = new Model3DGroup
            {
                Children =
                {
                    new AmbientLight(Color.FromRgb(105, 105, 105)),
                    new DirectionalLight(Color.FromRgb(175, 175, 170), new Vector3D(-0.5, -0.7, -1)),
                    new DirectionalLight(Color.FromRgb(70, 72, 80), new Vector3D(0.6, 0.4, 0.3)),
                },
            },
        });
        _viewport.Children.Add(_content);
        Children.Add(_viewport);
        _hint = UiKit.Muted(Loc.T("Arraste para girar · botão direito desloca · roda aproxima · duplo clique enquadra",
            "Drag to orbit · right button pans · wheel zooms · double-click fits"), 10.5, false, new Thickness(6, 0, 0, 4));
        _hint.VerticalAlignment = VerticalAlignment.Bottom;
        _hint.IsHitTestVisible = false;
        Children.Add(_hint);
        MinHeight = 220;
        MouseDown += OnDown;
        MouseUp += (_, _) => { _rotating = _panning = false; ReleaseMouseCapture(); };
        MouseMove += OnMove;
        MouseWheel += (_, e) => { _distance *= e.Delta > 0 ? 0.87 : 1.15; _distance = Math.Clamp(_distance, _radius * 0.05, _radius * 50); UpdateCamera(); };
    }

    public Scene3? Scene
    {
        get => _scene;
        set
        {
            _scene = value;
            try
            {
                Rebuild();
            }
            catch (Exception ex)
            {
                Infrastructure.Log.Error("Pré-visualização 3D", ex);
                _content.Content = null;
            }
        }
    }

    public void Fit()
    {
        _azimuth = -55;
        _elevation = 30;
        _target = new Point3D(0, 0, 0);
        _distance = _radius * 2.6;
        UpdateCamera();
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Fit();
            return;
        }
        _last = e.GetPosition(this);
        _rotating = e.ChangedButton == MouseButton.Left;
        _panning = e.ChangedButton is MouseButton.Right or MouseButton.Middle;
        CaptureMouse();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (!_rotating && !_panning) return;
        var p = e.GetPosition(this);
        var dx = p.X - _last.X;
        var dy = p.Y - _last.Y;
        _last = p;
        if (_rotating)
        {
            _azimuth -= dx * 0.4;
            _elevation = Math.Clamp(_elevation + dy * 0.4, -89, 89);
        }
        else
        {
            var look = _camera.LookDirection;
            look.Normalize();
            var right = Vector3D.CrossProduct(look, new Vector3D(0, 0, 1));
            if (right.Length < 1e-6) right = new Vector3D(1, 0, 0);
            right.Normalize();
            var up = Vector3D.CrossProduct(right, look);
            var k = _distance * 0.0016;
            _target += right * (-dx * k) + up * (dy * k);
        }
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        var az = _azimuth * Math.PI / 180;
        var el = _elevation * Math.PI / 180;
        var dir = new Vector3D(Math.Cos(el) * Math.Cos(az), Math.Cos(el) * Math.Sin(az), Math.Sin(el));
        _camera.Position = _target + dir * _distance;
        _camera.LookDirection = -dir * _distance;
        _camera.NearPlaneDistance = Math.Max(_distance * 0.001, 1e-4);
        _camera.FarPlaneDistance = _distance * 100 + _radius * 10;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Gray;
        }
    }

    private void Rebuild()
    {
        if (_scene == null || _scene.IsEmpty)
        {
            _content.Content = null;
            return;
        }
        var b = _scene.Bounds();
        if (b == null) return;
        var (min, max) = b.Value;
        var newCenter = (min + max) * 0.5;
        var newRadius = Math.Max((max - min).Length / 2, 1e-3);
        // Reenquadra só na primeira vez ou quando a cena muda muito de tamanho/posição.
        var refit = !_fitted || newRadius > _radius * 3 || newRadius < _radius / 3 || (newCenter - _center).Length > _radius * 2;
        _center = newCenter;
        _radius = newRadius;
        var group = new Model3DGroup();
        Point3D P(Vec3 v) => new(v.X - _center.X, v.Y - _center.Y, v.Z - _center.Z);
        foreach (var m in _scene.Meshes)
        {
            if (m.Positions.Count == 0) continue;
            var mesh = new MeshGeometry3D();
            var pos = new Point3DCollection(m.Positions.Count);
            foreach (var v in m.Positions) pos.Add(P(v));
            mesh.Positions = pos;
            mesh.TriangleIndices = new Int32Collection(m.Indices);
            mesh.Freeze();
            var c = ParseColor(m.Color);
            c.A = (byte)Math.Clamp(m.Opacity * 255, 20, 255);
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            var mat = new DiffuseMaterial(brush);
            mat.Freeze();
            var gm = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
            gm.Freeze();
            group.Children.Add(gm);
        }
        // Linhas como fitas finas (o WPF 3D não desenha linhas).
        if (_scene.Lines.Count > 0)
        {
            var w = _radius * 0.004;
            foreach (var g in _scene.Lines.GroupBy(l => l.Color))
            {
                var mesh = new MeshGeometry3D();
                var pos = new Point3DCollection();
                var idx = new Int32Collection();
                foreach (var (a, bb, _) in g)
                {
                    var d = bb - a;
                    if (d.Length < 1e-9) continue;
                    var n1 = d.AnyPerpendicular().Normalized() * w;
                    var n2 = d.Normalized().Cross(n1.Normalized()) * w;
                    foreach (var n in new[] { n1, n2 })
                    {
                        var i = pos.Count;
                        pos.Add(P(a - n)); pos.Add(P(a + n)); pos.Add(P(bb + n)); pos.Add(P(bb - n));
                        idx.Add(i); idx.Add(i + 1); idx.Add(i + 2); idx.Add(i); idx.Add(i + 2); idx.Add(i + 3);
                    }
                }
                mesh.Positions = pos;
                mesh.TriangleIndices = idx;
                mesh.Freeze();
                var brush = new SolidColorBrush(ParseColor(g.Key));
                brush.Freeze();
                var mat = new EmissiveMaterial(brush);
                mat.Freeze();
                var gm = new GeometryModel3D(mesh, mat) { BackMaterial = mat };
                gm.Freeze();
                group.Children.Add(gm);
            }
        }
        group.Freeze();
        _content.Content = group;
        if (refit)
        {
            _fitted = true;
            Fit();
        }
        else
        {
            UpdateCamera();
        }
    }
}
