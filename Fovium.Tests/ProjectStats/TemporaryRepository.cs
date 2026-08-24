namespace Fovium.Tests.ProjectStats;

internal sealed class TemporaryRepository : IDisposable
{
    public TemporaryRepository()
    {
        Root = Directory.CreateTempSubdirectory("Fovium.ProjectStats.Tests.").FullName;
    }

    public string Root { get; }

    public string WriteFile(string relativePath, string contents = "content")
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, contents);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }
    }
}
