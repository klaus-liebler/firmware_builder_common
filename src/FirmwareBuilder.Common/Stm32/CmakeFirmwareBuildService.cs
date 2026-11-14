using System.Text.Json;

namespace FirmwareBuilder.Common.Stm32;

public static class CmakeFirmwareBuildService
{
    // Die gueltigen Presets sind die nicht-versteckten configurePresets aus dem CMakePresets.json
    // des Projekts (Standardmechanismus von CMake) -- keine zweite, von Hand gepflegte Liste hier.
    public static IReadOnlyList<string> ConfigurePresets(string rootDir)
    {
        var path = Path.Combine(rootDir, "CMakePresets.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Kein CMakePresets.json unter {rootDir}.");
        }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var names = new List<string>();
        if (doc.RootElement.TryGetProperty("configurePresets", out var presets))
        {
            foreach (var p in presets.EnumerateArray())
            {
                var hidden = p.TryGetProperty("hidden", out var h) && h.ValueKind == JsonValueKind.True;
                if (!hidden && p.TryGetProperty("name", out var n) && n.GetString() is { } name)
                {
                    names.Add(name);
                }
            }
        }
        return names;
    }

    public static bool IsValidPreset(string rootDir, string preset) => ConfigurePresets(rootDir).Contains(preset);

    // Nimmt IBuildContextStm32 direkt entgegen -- RootDir/Preset sind bereits Teil des Kontexts.
    public static void Run(IBuildContextStm32 ctx)
    {
        ProcessRunner.RunInherit("cmake", ["--preset", ctx.Preset], ctx.RootDir);
        ProcessRunner.RunInherit("cmake", ["--build", "--preset", ctx.Preset], ctx.RootDir);
    }
}
