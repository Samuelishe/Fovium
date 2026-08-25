using Fovium.Presentation;
using Fovium.Rendering;

namespace Fovium.Tests.Presentation;

public sealed class MarkupConstraintGeometryTests
{
    [Theory]
    [InlineData(10, 2, 10.1980390272, 0)]
    [InlineData(10, 8, 9.0553851381, 9.0553851381)]
    [InlineData(2, 10, 0, 10.1980390272)]
    [InlineData(-8, 10, -9.0553851381, 9.0553851381)]
    [InlineData(-10, -2, -10.1980390272, 0)]
    [InlineData(-8, -10, -9.0553851381, -9.0553851381)]
    [InlineData(2, -10, 0, -10.1980390272)]
    [InlineData(8, -10, 9.0553851381, -9.0553851381)]
    public void SnapVectorToNearest45DegreesPreservesRadiusAcrossQuadrants(
        double x,
        double y,
        double expectedX,
        double expectedY)
    {
        var result = MarkupConstraintGeometry.SnapEndpointTo45Degrees(
            new PointD(0, 0),
            new PointD(x, y));

        Assert.Equal(expectedX, result.X, 8);
        Assert.Equal(expectedY, result.Y, 8);
        Assert.Equal(Math.Sqrt(x * x + y * y), Math.Sqrt(result.X * result.X + result.Y * result.Y), 8);
    }

    [Fact]
    public void SnapNearZeroVectorReturnsFiniteStart()
    {
        var start = new PointD(12.5, 18.75);

        var result = MarkupConstraintGeometry.SnapEndpointTo45Degrees(start, start);

        Assert.Equal(start, result);
        Assert.True(double.IsFinite(result.X));
        Assert.True(double.IsFinite(result.Y));
    }

    [Theory]
    [InlineData(10, 0)]
    [InlineData(10, 10)]
    [InlineData(0, 10)]
    [InlineData(-10, 10)]
    [InlineData(-10, 0)]
    [InlineData(-10, -10)]
    [InlineData(0, -10)]
    [InlineData(10, -10)]
    public void Exact45DegreeDirectionsRemainUnchanged(double x, double y)
    {
        var result = MarkupConstraintGeometry.SnapEndpointTo45Degrees(
            new PointD(0, 0),
            new PointD(x, y));

        Assert.Equal(x, result.X, 8);
        Assert.Equal(y, result.Y, 8);
    }

    [Theory]
    [InlineData(4, 9, 9, 9)]
    [InlineData(-4, 9, -9, 9)]
    [InlineData(4, -9, 9, -9)]
    [InlineData(-4, -9, -9, -9)]
    [InlineData(0, 9, 9, 9)]
    [InlineData(9, 0, 9, 9)]
    public void SquareEndpointUsesMaximumSideAndPreservesQuadrant(
        double dx,
        double dy,
        double expectedX,
        double expectedY)
    {
        var result = MarkupConstraintGeometry.SquareEndpoint(
            new PointD(0, 0),
            new PointD(dx, dy));

        Assert.Equal(new PointD(expectedX, expectedY), result);
        Assert.Equal(Math.Abs(result.X), Math.Abs(result.Y));
    }

    [Fact]
    public void ClipEndpointAlongRayPreservesConstrainedDirectionAtSourceBoundary()
    {
        var result = MarkupConstraintGeometry.ClipEndpointAlongRay(
            new PointD(80, 70),
            new PointD(120, 110),
            new PixelSize(100, 80));

        Assert.Equal(new PointD(90, 80), result);
        Assert.Equal(result.X - 80, result.Y - 70);
    }
}
