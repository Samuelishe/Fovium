using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fovium.Settings;

internal enum SettingsDiagnosticKind
{
    Malformed,
    UnsupportedSchema,
    ReadFailed,
    WriteFailed,
}

internal sealed record SettingsDiagnostic(
    SettingsDiagnosticKind Kind,
    string Message,
    Exception? Exception = null);

internal sealed record SettingsLoadResult(
    FoviumSettings Settings,
    SettingsDiagnostic? Diagnostic);

internal interface ISettingsStore
{
    Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken);
}

internal sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public async Task<SettingsLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var settings = await JsonSerializer.DeserializeAsync<FoviumSettings>(
                stream,
                SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            if (settings is null)
            {
                return Defaults(
                    SettingsDiagnosticKind.Malformed,
                    "The settings document is empty.");
            }

            if (settings.SchemaVersion != FoviumSettings.CurrentSchemaVersion)
            {
                return Defaults(
                    SettingsDiagnosticKind.UnsupportedSchema,
                    $"Unsupported settings schema version: {settings.SchemaVersion}.");
            }

            return new SettingsLoadResult(settings, null);
        }
        catch (FileNotFoundException)
        {
            return new SettingsLoadResult(FoviumSettings.Default, null);
        }
        catch (DirectoryNotFoundException)
        {
            return new SettingsLoadResult(FoviumSettings.Default, null);
        }
        catch (JsonException exception)
        {
            return Defaults(SettingsDiagnosticKind.Malformed, "Settings JSON is malformed.", exception);
        }
        catch (IOException exception)
        {
            return Defaults(SettingsDiagnosticKind.ReadFailed, "Settings could not be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Defaults(SettingsDiagnosticKind.ReadFailed, "Settings access was denied.", exception);
        }
    }

    public async Task SaveAsync(FoviumSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var directory = System.IO.Path.GetDirectoryName(Path)
            ?? throw new InvalidOperationException("Settings path has no containing directory.");
        var temporaryPath = Path + ".tmp";

        await Task.Run(() => Directory.CreateDirectory(directory), cancellationToken).ConfigureAwait(false);
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    settings,
                    SerializerOptions,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            await Task.Run(
                () => File.Move(temporaryPath, Path, overwrite: true),
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static SettingsLoadResult Defaults(
        SettingsDiagnosticKind kind,
        string message,
        Exception? exception = null) =>
        new(FoviumSettings.Default, new SettingsDiagnostic(kind, message, exception));
}
