using Fovium.Application;

namespace Fovium.Tests.Application;

public sealed class ActivationPlanTests
{
    [Fact]
    public void ZeroPathsSelectsHomeModeWithoutRequestingAFilePicker()
    {
        var plan = ActivationPlan.Create([]);

        Assert.Equal(ActivationMode.Home, plan.Mode);
        Assert.Empty(plan.Paths);
    }

    [Fact]
    public void ExistingFolderSelectsFolderMode()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.ActivationPlan.Tests.");
        try
        {
            var plan = ActivationPlan.Create([directory.FullName]);

            Assert.Equal(ActivationMode.Folder, plan.Mode);
            Assert.Equal(directory.FullName, Assert.Single(plan.Paths));
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void DropWithFilesAndFolderPreservesOnlyExplicitFileOrder()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.ActivationDrop.Tests.");
        try
        {
            var first = Path.Combine(directory.FullName, "first.jpg");
            var second = Path.Combine(directory.FullName, "second.png");
            File.WriteAllText(first, "fixture");
            File.WriteAllText(second, "fixture");

            var plan = ActivationPlan.CreateDrop([directory.FullName, second, first]);

            Assert.NotNull(plan);
            Assert.Equal(ActivationMode.ExplicitSelection, plan.Mode);
            Assert.Equal([second, first], plan.Paths);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void DropWithOnlyFolderCreatesFolderActivation()
    {
        var directory = Directory.CreateTempSubdirectory("Fovium.ActivationFolderDrop.Tests.");
        try
        {
            var plan = ActivationPlan.CreateDrop([directory.FullName]);

            Assert.NotNull(plan);
            Assert.Equal(ActivationMode.Folder, plan.Mode);
            Assert.Equal(directory.FullName, Assert.Single(plan.Paths));
        }
        finally
        {
            directory.Delete(true);
        }
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