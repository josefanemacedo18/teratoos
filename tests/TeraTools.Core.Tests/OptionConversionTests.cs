using TeraTools.Core.Serialization;
using Xunit;

namespace TeraTools.Core.Tests;

public enum SampleMode { A, B, C }

public sealed class SampleOptions
{
    public int Count { get; set; } = 4;
    public long Big { get; set; } = 10;
    public double Size { get; set; } = 1.5;
    public float Ratio { get; set; } = 0.5f;
    public int? Optional { get; set; }
    public SampleMode Mode { get; set; } = SampleMode.A;
    public bool Flag { get; set; }
    public string Name { get; set; } = "x";
    public List<string> Items { get; set; } = new() { "padrão" };
}

public class OptionConversionTests
{
    [Theory]
    [InlineData(nameof(SampleOptions.Count), 3.6, 4)]
    [InlineData(nameof(SampleOptions.Big), 7.2, 7L)]
    [InlineData(nameof(SampleOptions.Size), 2.25, 2.25)]
    [InlineData(nameof(SampleOptions.Ratio), 0.25, 0.25f)]
    public void Numeric_value_is_stored_with_the_exact_property_type(string prop, double input, object expected)
    {
        var o = new SampleOptions();
        var p = typeof(SampleOptions).GetProperty(prop)!;
        var boxed = ValueConverter.ToNumeric(input, p.PropertyType);
        Assert.Equal(p.PropertyType, boxed.GetType());
        p.SetValue(o, boxed); // antes da correção: "Object of type 'System.Double' cannot be converted to type 'System.Int32'"
        Assert.Equal(expected, p.GetValue(o));
    }

    [Fact]
    public void Nullable_int_accepts_double()
    {
        var o = new SampleOptions();
        var p = typeof(SampleOptions).GetProperty(nameof(SampleOptions.Optional))!;
        p.SetValue(o, ValueConverter.ToNumeric(5.0, p.PropertyType));
        Assert.Equal(5, o.Optional);
    }

    [Fact]
    public void Old_ternary_pattern_boxed_a_double_and_reproduces_the_bug()
    {
        // Documenta a causa do erro: um operador condicional com ramos int/long/float/double tem tipo double.
        var t = typeof(int);
        double v = 3;
        object boxed = t == typeof(int) ? (int)Math.Round(v) : t == typeof(long) ? (long)Math.Round(v) : t == typeof(float) ? (float)v : v;
        Assert.IsType<double>(boxed);
        var p = typeof(SampleOptions).GetProperty(nameof(SampleOptions.Count))!;
        Assert.ThrowsAny<ArgumentException>(() => p.SetValue(new SampleOptions(), boxed));
    }

    [Fact]
    public void Populate_converts_compatible_values()
    {
        var o = new SampleOptions();
        Json.PopulateObject(o, """{"Count": 3.0, "Big": "12", "Size": "12,5", "Ratio": 1, "Optional": 2.4, "Mode": "c", "Flag": 1, "Name": 7, "Items": ["a","b"]}""");
        Assert.Equal(3, o.Count);
        Assert.Equal(12L, o.Big);
        Assert.Equal(12.5, o.Size, 6);
        Assert.Equal(1f, o.Ratio);
        Assert.Equal(2, o.Optional);
        Assert.Equal(SampleMode.C, o.Mode);
        Assert.True(o.Flag);
        Assert.Equal("7", o.Name);
        Assert.Equal(new[] { "a", "b" }, o.Items);
    }

    [Fact]
    public void Populate_ignores_invalid_values_and_keeps_defaults()
    {
        var o = new SampleOptions();
        Json.PopulateObject(o, """{"Count": "muitos", "Mode": "Z", "Size": [1,2], "Flag": "talvez", "Unknown": 3}""");
        Assert.Equal(4, o.Count);
        Assert.Equal(SampleMode.A, o.Mode);
        Assert.Equal(1.5, o.Size);
        Assert.False(o.Flag);
    }

    [Fact]
    public void Populate_with_broken_json_does_not_throw()
    {
        var o = new SampleOptions();
        Json.PopulateObject(o, "{ isso não é json");
        Assert.Equal(4, o.Count);
    }

    [Fact]
    public void Populate_only_touches_properties_present_in_json()
    {
        var o = new SampleOptions { Count = 9 };
        Json.PopulateObject(o, """{"Size": 3}""");
        Assert.Equal(9, o.Count);
        Assert.Equal(3, o.Size);
    }

    [Fact]
    public void Enum_from_number_must_be_defined()
    {
        Assert.True(ValueConverter.TryConvert(1.0, typeof(SampleMode), out var ok));
        Assert.Equal(SampleMode.B, ok);
        Assert.False(ValueConverter.TryConvert(9.0, typeof(SampleMode), out _));
    }
}
