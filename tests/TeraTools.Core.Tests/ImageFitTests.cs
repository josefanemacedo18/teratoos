using TeraTools.Core.Imaging;
using Xunit;

namespace TeraTools.Core.Tests;

public class ImageFitTests
{
    [Theory]
    [InlineData(4000, 3000, 1600, 1600, 1200)]
    [InlineData(1000, 3000, 1600, 533, 1600)]
    [InlineData(800, 600, 1600, 800, 600)]
    [InlineData(0, 600, 1600, 0, 0)]
    public void FitKeepsProportionAndNeverEnlarges(int w, int h, int max, int ew, int eh)
    {
        Assert.Equal((ew, eh), ImageFit.Fit(w, h, max));
    }

    [Fact]
    public void FormatsBytes()
    {
        Assert.Equal("1 KB", ImageFit.FormatBytes(10));
        Assert.Equal("500 KB", ImageFit.FormatBytes(500 * 1024));
        Assert.Contains("MB", ImageFit.FormatBytes(3 * 1024 * 1024));
    }
}
