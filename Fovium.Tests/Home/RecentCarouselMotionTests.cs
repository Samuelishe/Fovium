using Fovium.Home;

namespace Fovium.Tests.Home;

public sealed class RecentCarouselMotionTests
{
    [Fact]
    public void MotionBelowThresholdRemainsAClick()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(100, 20, 50, TimeSpan.Zero);

        var update = motion.Move(96, 21, 300, TimeSpan.FromMilliseconds(40));
        var startedInertia = motion.End(300, TimeSpan.FromMilliseconds(50));

        Assert.False(update.IsDragging);
        Assert.False(startedInertia);
        Assert.False(motion.ConsumeSuppressedActivation());
    }

    [Fact]
    public void HorizontalDragSuppressesExactlyOneActivationAndClampsOffset()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(100, 20, 290, TimeSpan.Zero);

        var update = motion.Move(20, 22, 300, TimeSpan.FromMilliseconds(60));
        motion.End(300, TimeSpan.FromMilliseconds(70));

        Assert.True(update.IsDragging);
        Assert.Equal(300, update.Offset);
        Assert.True(motion.ConsumeSuppressedActivation());
        Assert.False(motion.ConsumeSuppressedActivation());
    }

    [Fact]
    public void MostlyVerticalMovementDoesNotStartHorizontalDrag()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(100, 20, 50, TimeSpan.Zero);

        var update = motion.Move(92, 55, 300, TimeSpan.FromMilliseconds(40));

        Assert.False(update.IsDragging);
        Assert.Equal(50, update.Offset);
    }

    [Fact]
    public void FlickCoastsWithinHardBoundAndEventuallyStops()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(400, 20, 100, TimeSpan.Zero);
        motion.Move(200, 20, 1_000, TimeSpan.FromMilliseconds(20));
        motion.Move(0, 20, 1_000, TimeSpan.FromMilliseconds(40));

        Assert.True(motion.End(1_000, TimeSpan.FromMilliseconds(41)));
        var releaseOffset = motion.Offset;
        for (var index = 0; index < 300 && motion.HasInertia; index++)
        {
            motion.AdvanceInertia(1d / 60d, 1_000);
        }

        Assert.False(motion.HasInertia);
        Assert.InRange(motion.Offset - releaseOffset, 279, 280);
        Assert.InRange(motion.Offset, 0, 1_000);
    }

    [Fact]
    public void OrdinaryFlickDecaysMonotonicallyWithACalmVisibleCoast()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(300, 20, 100, TimeSpan.Zero);
        motion.Move(220, 20, 1_000, TimeSpan.FromMilliseconds(100));
        Assert.True(motion.End(1_000, TimeSpan.FromMilliseconds(101)));
        var releaseOffset = motion.Offset;
        var previous = releaseOffset;
        var frames = 0;
        while (motion.HasInertia && frames < 180)
        {
            var current = motion.AdvanceInertia(1d / 60d, 1_000);
            Assert.True(current >= previous);
            previous = current;
            frames++;
        }

        Assert.False(motion.HasInertia);
        Assert.InRange(frames / 60d, 0.35, 0.45);
        Assert.InRange(motion.Offset - releaseOffset, 100, 115);
    }

    [Fact]
    public void ExtremePointerVelocityIsClampedBeforeCoasting()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(1_000, 20, 100, TimeSpan.Zero);
        motion.Move(0, 20, 2_000, TimeSpan.FromMilliseconds(1));
        Assert.True(motion.End(2_000, TimeSpan.FromMilliseconds(2)));
        var releaseOffset = motion.Offset;

        motion.AdvanceInertia(0.1, 2_000);

        var maximumFirstStep = RecentCarouselMotion.MaximumInertiaVelocity *
                               (1 - Math.Exp(-RecentCarouselMotion.FrictionPerSecond * 0.1)) /
                               RecentCarouselMotion.FrictionPerSecond;
        Assert.InRange(motion.Offset - releaseOffset, 0, maximumFirstStep + 0.01);
    }

    [Fact]
    public void InertiaStopsImmediatelyAtAnEdge()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(200, 20, 250, TimeSpan.Zero);
        motion.Move(0, 20, 300, TimeSpan.FromMilliseconds(50));
        Assert.True(motion.End(300, TimeSpan.FromMilliseconds(51)));

        Assert.Equal(300, motion.AdvanceInertia(1d / 60d, 300));
        Assert.False(motion.HasInertia);
    }

    [Fact]
    public void NewPointerInputCancelsInertia()
    {
        var motion = new RecentCarouselMotion();
        motion.Begin(180, 20, 40, TimeSpan.Zero);
        motion.Move(70, 20, 500, TimeSpan.FromMilliseconds(80));
        Assert.True(motion.End(500, TimeSpan.FromMilliseconds(85)));

        motion.Begin(50, 20, motion.Offset, TimeSpan.FromMilliseconds(90));

        Assert.False(motion.HasInertia);
    }

    [Theory]
    [InlineData(0, 600, 600, false, false, false)]
    [InlineData(0, 900, 600, true, false, true)]
    [InlineData(150, 900, 600, true, true, true)]
    [InlineData(300, 900, 600, true, true, false)]
    public void OverflowControlsGrabAndEdgeFades(
        double offset,
        double extent,
        double viewport,
        bool scrollable,
        bool leftFade,
        bool rightFade)
    {
        var state = RecentCarouselOverflow.Resolve(offset, extent, viewport);

        Assert.Equal(scrollable, state.IsScrollable);
        Assert.Equal(leftFade, state.ShowLeftFade);
        Assert.Equal(rightFade, state.ShowRightFade);
    }
}