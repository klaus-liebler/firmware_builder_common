namespace FirmwareBuilder.Common;

public static class CmakeFirmwareBuildService
{
    public static readonly string[] ValidPresets = ["Debug", "Release", "Debug-Nucleo"];

    public static bool IsValidPreset(string preset) => ValidPresets.Contains(preset);

    public static void Run(string rootDir, string preset)
    {
        ProcessRunner.RunInherit("cmake", ["--preset", preset], rootDir);
        ProcessRunner.RunInherit("cmake", ["--build", "--preset", preset], rootDir);
    }
}
