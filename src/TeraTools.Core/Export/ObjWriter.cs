using System.Globalization;
using System.Text;
using TeraTools.Core.Geometry;

namespace TeraTools.Core.Export;

/// <summary>
/// Exporta caixas orientadas para OBJ (Blender, Rhino, 3ds Max...), em metros.
/// Usado para levar estados cinéticos e geradores para simulação ou animação externa.
/// </summary>
public static class ObjWriter
{
    public static string Build(IEnumerable<(OrientedBox Box, string Group)> boxes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Téra Tools - OBJ (metros)");
        var v = 1;
        foreach (var (box, group) in boxes)
        {
            sb.Append("g ").AppendLine(string.IsNullOrWhiteSpace(group) ? "item" : group.Replace(' ', '_'));
            var corners = box.Corners().ToArray();
            foreach (var c in corners)
                sb.AppendLine(string.Create(CultureInfo.InvariantCulture, $"v {c.X * 0.3048:0.######} {c.Y * 0.3048:0.######} {c.Z * 0.3048:0.######}"));
            // Índices dos cantos: bit0 = x, bit1 = y, bit2 = z
            int[][] faces =
            [
                [0, 2, 3, 1], [4, 5, 7, 6], [0, 1, 5, 4],
                [2, 6, 7, 3], [0, 4, 6, 2], [1, 3, 7, 5],
            ];
            foreach (var f in faces)
                sb.AppendLine($"f {v + f[0]} {v + f[1]} {v + f[2]} {v + f[3]}");
            v += 8;
        }
        return sb.ToString();
    }

    public static void Write(string path, IEnumerable<(OrientedBox Box, string Group)> boxes)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, Build(boxes), new UTF8Encoding(false));
    }
}
