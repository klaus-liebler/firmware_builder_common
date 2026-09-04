namespace FirmwareBuilder.Common;

// Best-Binary-Buffers-Protokollgenerierung: erzeugt C++-Header, TS-Modul und ein minimales
// npm-Package fuer TS-Konsumenten aus einem oder mehreren Schema-Quellverzeichnissen. Weder
// projekt- noch chip-spezifisch (nimmt die Basis-IBuildContext entgegen, nur fuer RootDir) --
// einzig projektspezifisch ist, welche Quellverzeichnisse uebergeben werden, wohin generiert wird,
// und wie das npm-Package heisst.
public static class WsProtocolBuildService
{
    // tsPackageName: null, wenn kein TS-Konsument ein eigenstaendiges npm-Package braucht (z.B.
    // STM32, das ws-protocol.ts direkt aus dem Projekt importiert statt ueber ein generiertes
    // Package) -- dann wird kein package.json geschrieben.
    public static List<string> Generate(
        IBuildContext ctx,
        IReadOnlyList<string> sourceDirs,
        string cppOutputDir,
        string tsOutputDir,
        string? tsPackageName = null)
    {
        var files = BestBinaryBuffersProtocolGenerator.Generate(new BestBinaryBuffersGenerateParams(
            RootDir: ctx.RootDir,
            Sources: sourceDirs,
            DefaultSource: sourceDirs[0],
            IdMapPath: Path.Combine(sourceDirs[0], "ids.txt"),
            CppOutputFilePath: Path.Combine(cppOutputDir, "ws_protocol.hh"),
            TsOutputFilePath: Path.Combine(tsOutputDir, "ws-protocol.ts"),
            TsPackageJsonOutputPath: tsPackageName is null ? null : Path.Combine(tsOutputDir, "package.json"),
            TsPackageJsonContent: tsPackageName is null ? null : $$"""{"name":"{{tsPackageName}}","version":"1.0.0","main":"ws-protocol.ts","author":"BestBinaryBuffers","license":"No License","description":"Generated during build from BestBinaryBuffers (s. C:\\repos\\dotnet_libs\\best_binary_buffers)."}"""));

        var sourcesLabel = string.Join(", ", sourceDirs);
        Console.WriteLine($"{files.Count} Datei(en) aus {sourceDirs.Count} Quelle(n) ({sourcesLabel}) -> {cppOutputDir} / {tsOutputDir}");
        return files;
    }
}
