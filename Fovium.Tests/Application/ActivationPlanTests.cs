using Fovium.Application;

namespace Fovium.Tests.Application;

public sealed class ActivationPlanTests
{
    [Fact]
    public void ZeroPathsSelectsFilePickerMode()
    {
        var plan = ActivationPlan.Create([]);

        Assert.Equal(ActivationMode.FilePicker, plan.Mode);
        Assert.Empty(plan.Paths);
    }

    [Fact]
    public void OnePathSelectsDirectoryMode()
    {
        var plan = ActivationPlan.Create(["photo.jpg"]);

        Assert.Equal(ActivationMode.Directory, plan.Mode);
        Assert.Single(plan.Paths);
    }

    [Fact]
    public void MultiplePathsSelectExplicitModeAndPreserveOrder()
    {
        var plan = ActivationPlan.Create(["A.jpg", "D.jpg", "F.png"]);

        Assert.Equal(ActivationMode.ExplicitSelection, plan.Mode);
        Assert.Equal(
            ["A.jpg", "D.jpg", "F.png"],
            plan.Paths.Select(Path.GetFileName));
    }
}
