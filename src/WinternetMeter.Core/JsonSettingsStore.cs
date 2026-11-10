using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinternetMeter.Core;

/// <summary>
/// Reads/writes <see cref="AppSettings"/> as human-readable JSON.
///
/// Location:
///   Windows: %AppData%\WinternetMeter\settings.json
///   Linux:   ~/.config/winternetmeter/settings.json
///
/// Unlike the old Properties.Settings (stored in a folder named after the exe's
/// path hash and version), this survives updates and moving the exe.
/// </summary>
public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public JsonSettingsStore(string filePath) => FilePath = filePath;

    public string FilePath { get; }

    public bool Exists => File.Exists(FilePath);

    public static string DefaultPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrEmpty(baseDir))
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        var appDir = OperatingSystem.IsWindows() ? "WinternetMeter" : "winternetmeter";
        return Path.Combine(baseDir, appDir, "settings.json");
    }

    /// <summary>Loads settings; a missing or unreadable file gives defaults (a corrupt file is kept as .bad).</summary>
    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
            return new AppSettings().Normalize();

        try
        {
            var json = File.ReadAllText(FilePath);
            return (JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings()).Normalize();
        }
        catch (JsonException)
        {
            TryBackupCorrupt();
            return new AppSettings().Normalize();
        }
        catch (IOException)
        {
            return new AppSettings().Normalize();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings().Normalize();
        }
    }

    /// <summary>Writes atomically (temp file + rename) so a crash mid-write can't leave a half file.</summary>
    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, FilePath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void TryBackupCorrupt()
    {
        try { File.Copy(FilePath, FilePath + ".bad", overwrite: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
