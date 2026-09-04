namespace FirmwareBuilder.Common;

public static class CmakeFirmwareBuildService
{
    public static readonly string[] ValidPresets = ["Debug", "Release", "Debug-Nucleo"];

    public static bool IsValidPreset(string preset) => ValidPresets.Contains(preset);

    // Nimmt IBuildContextStm32 direkt entgegen -- RootDir/Preset sind bereits Teil des Kontexts.
    public static void Run(IBuildContextStm32 ctx)
    {
        ProcessRunner.RunInherit("cmake", ["--preset", ctx.Preset], ctx.RootDir);
        ProcessRunner.RunInherit("cmake", ["--build", "--preset", ctx.Preset], ctx.RootDir);
    }
}
