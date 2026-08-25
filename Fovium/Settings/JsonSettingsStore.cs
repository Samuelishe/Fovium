using System.Text.Json;
using System.Text.Json.Serialization;
using Fovium.Input;
using Fovium.Stage;

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
    SettingsDiagnostic? Diagnostic,
    bool RequiresSave = false);

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
            using var document = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var schemaElement) ||
                !schemaElement.TryGetInt32(out var schemaVersion))
            {
                return Defaults(
                    SettingsDiagnosticKind.Malformed,
                    "The settings document has no valid schema version.");
            }

            if (schemaVersion == 1)
            {
                var legacy = document.Deserialize<LegacyV1Settings>(SerializerOptions)
                    ?? throw new JsonException("The schema-v1 settings document is empty.");
                return new SettingsLoadResult(MigrateV1(legacy), null, RequiresSave: true);
            }

            if (schemaVersion != FoviumSettings.CurrentSchemaVersion)
            {
                return Defaults(
                    SettingsDiagnosticKind.UnsupportedSchema,
                    $"Unsupported settings schema version: {schemaVersion}.");
            }

            var settings = document.Deserialize<FoviumSettings>(SerializerOptions)
                ?? throw new JsonException("The settings document is empty.");
            return new SettingsLoadResult(settings.Normalize(), null);
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

    private static FoviumSettings MigrateV1(LegacyV1Settings legacy)
    {
        var stage = legacy.StageMode switch
        {
            LegacyStageMode.Black => StageSettings.Default,
            LegacyStageMode.Neutral => StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Neutral,
            },
            LegacyStageMode.Ambient => StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Ambient,
            },
            LegacyStageMode.AmbientMatte => StageSettings.Default with
            {
                BackgroundMode = StageBackgroundMode.Ambient,
                MatteEnabled = true,
            },
            _ => StageSettings.Default,
        };
        return new FoviumSettings
        {
            ImageChangeViewPolicy = legacy.ImageChangeViewPolicy,
            Stage = stage,
            Shortcuts = ShortcutSettings.Default,
        }.Normalize();
    }

    private enum LegacyStageMode
    {
        Black,
        Neutral,
        Ambient,
        AmbientMatte,
    }

    private sealed record LegacyV1Settings
    {
        public int SchemaVersion { get; init; } = 1;

        public ImageChangeViewPolicy ImageChangeViewPolicy { get; init; } =
            ImageChangeViewPolicy.KeepCurrentScale;

        public LegacyStageMode StageMode { get; init; } = LegacyStageMode.Black;
    }
}
