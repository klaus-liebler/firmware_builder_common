using BestBinaryBuffers;

namespace FirmwareBuilder.Common;

public sealed record BestBinaryBuffersGenerateParams(
    string RootDir,
    IReadOnlyList<string> Sources,
    string DefaultSource,
    string IdMapPath,
    string CppOutputFilePath,
    string TsOutputFilePath,
    string? TsPackageJsonOutputPath = null,
    string? TsPackageJsonContent = null);

public static class SourceFileResolver
{
    public static List<string> ResolveFiles(string rootDir, IReadOnlyList<string>? sources, string defaultSource, string extension)
    {
        var effectiveSources = sources is { Count: > 0 } ? sources : [defaultSource];
        var files = effectiveSources.SelectMany(source => ResolveSource(source, rootDir, extension))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"Keine {extension}-Dateien gefunden (durchsucht: {string.Join(", ", effectiveSources)}).");
        }
        return files;
    }

    private static List<string> ResolveSource(string source, string rootDir, string extension)
    {
        var resolved = Path.IsPathRooted(source) ? source : Path.Combine(rootDir, source);
        if (Directory.Exists(resolved))
        {
            return Directory.GetFiles(resolved, $"*{extension}").ToList();
        }
        if (File.Exists(resolved))
        {
            return [resolved];
        }
        throw new InvalidOperationException(
            $"Quelle nicht gefunden (weder Verzeichnis noch Datei): {resolved}" +
            (source != resolved ? $" (angegeben als \"{source}\")" : ""));
    }
}

public static class BestBinaryBuffersProtocolGenerator
{
    public static List<string> Generate(BestBinaryBuffersGenerateParams p)
    {
        var files = SourceFileResolver.ResolveFiles(p.RootDir, p.Sources, p.DefaultSource, ".cs");

        var idMap = IdMap.Load(p.IdMapPath);
        SchemaCompiler.Compile(files, idMap, p.CppOutputFilePath, p.TsOutputFilePath);
        idMap.SaveIfDirty(p.IdMapPath);

        if (!string.IsNullOrEmpty(p.TsPackageJsonOutputPath) && !string.IsNullOrEmpty(p.TsPackageJsonContent))
        {
            var dir = Path.GetDirectoryName(p.TsPackageJsonOutputPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(p.TsPackageJsonOutputPath, p.TsPackageJsonContent);
        }

        return files;
    }
}
