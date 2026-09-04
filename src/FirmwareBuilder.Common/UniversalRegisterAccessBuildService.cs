using UniversalRegisterAccess;

namespace FirmwareBuilder.Common;

public sealed record UniversalRegisterAccessBuildRequest(
    string RootDir,
    string DefaultSchemaDirectory,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    IReadOnlyList<string> Sources);

// Generierte Register-Access-Dateien sind reine, deterministische Ableitung aus Schema-Quellen --
// gehoeren ins Projekt (Core/generated, web/generated), nicht ins Board-Archiv (s. Projektgedaechtnis
// "generierte Dateien nur im Projekt, Ausnahmen: Zertifikate/ESP32-Keys/kostenpflichtige Assets").
public static class UniversalRegisterAccessBuildService
{
    public static void Run(UniversalRegisterAccessBuildRequest request)
    {
        var files = SourceFileResolver.ResolveFiles(request.RootDir, request.Sources, request.DefaultSchemaDirectory, ".cs");

        Directory.CreateDirectory(request.CoreGeneratedDir);
        Directory.CreateDirectory(request.WebGeneratedDir);

        SchemaCompiler.Compile(files,
            Path.Combine(request.CoreGeneratedDir, "modbus_registers_generated.hh"),
            Path.Combine(request.WebGeneratedDir, "register-map.ts"),
            Path.Combine(request.CoreGeneratedDir, "opcua_registers_generated.hh"));

        var sourcesLabel = request.Sources.Count > 0 ? string.Join(", ", request.Sources) : request.DefaultSchemaDirectory;
        Console.WriteLine($"{files.Count} Datei(en) aus {sourcesLabel} -> {request.CoreGeneratedDir} / {request.WebGeneratedDir}");
    }
}
