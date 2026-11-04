using System.Globalization;

namespace TeraTools.Core.Serialization;

/// <summary>
/// Converte valores para o tipo exato de uma propriedade antes de gravá-los por reflexão.
/// Evita o erro "Object of type 'System.Double' cannot be converted to type 'System.Int32'".
/// </summary>
public static class ValueConverter
{
    /// <summary>Converte um número para o tipo numérico da propriedade (int, long, short, byte, float, double, decimal, nullable).</summary>
    public static object ToNumeric(double value, Type propertyType)
    {
        var t = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (double.IsNaN(value) || double.IsInfinity(value)) value = 0;
        if (t == typeof(int)) return (object)(int)Math.Clamp(Math.Round(value), int.MinValue, int.MaxValue);
        if (t == typeof(long)) return (object)(long)Math.Clamp(Math.Round(value), long.MinValue, long.MaxValue);
        if (t == typeof(short)) return (object)(short)Math.Clamp(Math.Round(value), short.MinValue, short.MaxValue);
        if (t == typeof(byte)) return (object)(byte)Math.Clamp(Math.Round(value), byte.MinValue, byte.MaxValue);
        if (t == typeof(float)) return (object)(float)value;
        if (t == typeof(decimal)) return (object)(decimal)value;
        if (t == typeof(double)) return (object)value;
        throw new ArgumentException($"Tipo numérico não suportado: {t.Name}", nameof(propertyType));
    }

    public static bool IsNumeric(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
               || t == typeof(float) || t == typeof(double) || t == typeof(decimal);
    }

    public static bool IsInteger(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte);
    }

    /// <summary>
    /// Converte qualquer valor (número, texto, booleano, enum) para o tipo da propriedade.
    /// Retorna false se não for possível (o chamador mantém o valor atual).
    /// </summary>
    public static bool TryConvert(object? value, Type propertyType, out object? result)
    {
        result = null;
        var t = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        var nullable = Nullable.GetUnderlyingType(propertyType) != null || !propertyType.IsValueType;
        if (value == null)
        {
            result = null;
            return nullable;
        }
        if (t.IsInstanceOfType(value))
        {
            result = value;
            return true;
        }
        try
        {
            if (IsNumeric(t))
            {
                double d;
                switch (value)
                {
                    case string s when !Units.U.TryParseNumber(s, out d):
                        return false;
                    case string:
                        Units.U.TryParseNumber((string)value, out d);
                        break;
                    case bool b:
                        d = b ? 1 : 0;
                        break;
                    case Enum e:
                        d = Convert.ToDouble(e, CultureInfo.InvariantCulture);
                        break;
                    default:
                        d = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                        break;
                }
                result = ToNumeric(d, t);
                return true;
            }
            if (t == typeof(bool))
            {
                switch (value)
                {
                    case string s:
                        var n = s.Trim().ToLowerInvariant();
                        if (n is "1" or "true" or "sim" or "yes") { result = true; return true; }
                        if (n is "0" or "false" or "nao" or "não" or "no" or "") { result = false; return true; }
                        return false;
                    default:
                        result = Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0;
                        return true;
                }
            }
            if (t.IsEnum)
            {
                if (value is string es)
                {
                    if (Enum.TryParse(t, es.Trim(), true, out var ev) && Enum.IsDefined(t, ev!)) { result = ev; return true; }
                    return false;
                }
                var num = Convert.ToInt64(Math.Round(Convert.ToDouble(value, CultureInfo.InvariantCulture)));
                var obj = Enum.ToObject(t, num);
                if (!Enum.IsDefined(t, obj)) return false;
                result = obj;
                return true;
            }
            if (t == typeof(string))
            {
                result = Convert.ToString(value, CultureInfo.InvariantCulture);
                return true;
            }
        }
        catch
        {
            return false;
        }
        return false;
    }
}
