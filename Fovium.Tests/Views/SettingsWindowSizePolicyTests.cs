using Avalonia;
using Fovium.Settings;
using Fovium.Views;

namespace Fovium.Tests.Views;

public sealed class SettingsWindowSizePolicyTests
{
    [Fact]
    public void DefaultsAreComfortablySizedInTheRequestedLogicalRange()
    {
        var defaults = SettingsWindowSizeSettings.Default;

        Assert.InRange(defaults.WidthDip, 900, 950);
        Assert.InRange(defaults.HeightDip, 650, 700);
        Assert.Equal(defaults, defaults.Normalize());
    }

    [Theory]
    [InlineData(double.NaN, 675)]
    [InlineData(double.PositiveInfinity, 675)]
    [InlineData(double.NegativeInfinity, 675)]
    [InlineData(920, double.NaN)]
    [InlineData(920, double.PositiveInfinity)]
    [InlineData(920, double.NegativeInfinity)]
    public void NonFiniteDimensionsFallBackIndependentlyToDefaults(double width, double height)
    {
        var normalized = new SettingsWindowSizeSettings
        {
            WidthDip = width,
            HeightDip = height,
        }.Normalize();

        Assert.Equal(
            double.IsFinite(width) ? width : SettingsWindowSizeSettings.Default.WidthDip,
            normalized.WidthDip);
        Assert.Equal(
            double.IsFinite(height) ? height : SettingsWindowSizeSettings.Default.HeightDip,
            normalized.HeightDip);
    }

    [Fact]
    public void TooSmallAndOversizedDimensionsFallBackToStableFiniteDefaults()
    {
        var small = new SettingsWindowSizeSettings
        {
            WidthDip = -1,
            HeightDip = 0,
        }.Normalize();
        var muchSmaller = new SettingsWindowSizeSettings
        {
            WidthDip = -1_000_000,
            HeightDip = -1_000_000,
        }.Normalize();
        var large = new SettingsWindowSizeSettings
        {
            WidthDip = 1_000_000,
            HeightDip = 1_000_000,
        }.Normalize();
        var muchLarger = new SettingsWindowSizeSettings
        {
            WidthDip = double.MaxValue,
            HeightDip = double.MaxValue,
        }.Normalize();

        Assert.Equal(SettingsWindowSizeSettings.Default, small);
        Assert.Equal(SettingsWindowSizeSettings.Default, muchSmaller);
        Assert.Equal(SettingsWindowSizeSettings.Default, large);
        Assert.Equal(SettingsWindowSizeSettings.Default, muchLarger);
        Assert.Equal(small, small.Normalize());
    }

    [Fact]
    public void ValidNormalDimensionsRoundTripExactly()
    {
        var expected = new SettingsWindowSizeSettings
        {
            WidthDip = 934.5,
            HeightDip = 689.25,
        };

        Assert.Equal(expected, expected.Normalize());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void PreferredLogicalSizeIsStableAcrossRenderScaling(double renderScaling)
    {
        var preferred = new SettingsWindowSizeSettings
        {
            WidthDip = 934,
            HeightDip = 688,
        };

        var result = SettingsWindowSizePolicy.Resolve(
            preferred,
            workAreaWidthPhysicalPixels: 1600 * renderScaling,
            workAreaHeightPhysicalPixels: 1000 * renderScaling,
            renderScaling);

        Assert.Equal(new Size(934, 688), result);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2)]
    public void VisibleInstanceClampsToSmallCurrentWorkAreaAtEveryRenderScaling(
        double renderScaling)
    {
        var result = SettingsWindowSizePolicy.Resolve(
            new SettingsWindowSizeSettings
            {
                WidthDip = double.MaxValue,
                HeightDip = double.MaxValue,
            },
            workAreaWidthPhysicalPixels: 640 * renderScaling,
            workAreaHeightPhysicalPixels: 480 * renderScaling,
            renderScaling);

        Assert.Equal(new Size(592, 432), result);
        Assert.True(result.Width * renderScaling < 640 * renderScaling);
        Assert.True(result.Height * renderScaling < 480 * renderScaling);
    }

    [Fact]
    public void EachOpeningUsesOnlyTheCurrentWorkAreaAndNeverAStoredPosition()
    {
        var preferred = new SettingsWindowSizeSettings { WidthDip = 930, HeightDip = 680 };

        var largeMonitor = SettingsWindowSizePolicy.Resolve(preferred, 2560, 1440, 1);
        var smallMonitor = SettingsWindowSizePolicy.Resolve(preferred, 800, 600, 1);
        var largeMonitorAgain = SettingsWindowSizePolicy.Resolve(preferred, 2560, 1440, 1);

        Assert.Equal(new Size(930, 680), largeMonitor);
        Assert.Equal(new Size(752, 552), smallMonitor);
        Assert.Equal(largeMonitor, largeMonitorAgain);
        Assert.Equal(typeof(Size), largeMonitor.GetType());
    }

    [Theory]
    [InlineData(0, 1080, 1)]
    [InlineData(double.NaN, 1080, 1)]
    [InlineData(1920, 0, 1)]
    [InlineData(1920, double.PositiveInfinity, 1)]
    [InlineData(1920, 1080, 0)]
    [InlineData(1920, 1080, double.NaN)]
    public void InvalidWorkAreaOrRenderScalingFallsBackToNormalizedPreference(
        double widthPhysical,
        double heightPhysical,
        double renderScaling)
    {
        var result = SettingsWindowSizePolicy.Resolve(
            SettingsWindowSizeSettings.Default,
            widthPhysical,
            heightPhysical,
            renderScaling);

        Assert.Equal(
            new Size(
                SettingsWindowSizeSettings.Default.WidthDip,
                SettingsWindowSizeSettings.Default.HeightDip),
            result);
    }
}
