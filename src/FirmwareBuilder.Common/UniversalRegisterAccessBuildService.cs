using UniversalRegisterAccess;

namespace FirmwareBuilder.Common;

public sealed record UniversalRegisterAccessBuildRequest(
    string RootDir,
    string DefaultSchemaDirectory,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    IReadOnlyList<string> Sources,
    string OpcUaRootName,             // OPC UA-Objekt unter Objects, das die Register-Ordner enthaelt
    string? DocsGeneratedDir = null); // gesetzt -> zusaetzlich modbus-registers.md (Register-Uebersicht)

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

        // rtoos: register_map.hpp (Adressen, Tabellengroessen, typisierte RegisterBank-Zugriffe) und
        // register_map_opcua.hpp (Knoten unter Objects/<OpcUaRootName>, an die Register gebunden).
        SchemaCompiler.Compile(files, request.OpcUaRootName,
            Path.Combine(request.CoreGeneratedDir, "register_map.hpp"),
            Path.Combine(request.WebGeneratedDir, "register-map.ts"),
            Path.Combine(request.CoreGeneratedDir, "register_map_opcua.hpp"),
            request.DocsGeneratedDir is null ? null : Path.Combine(request.DocsGeneratedDir, "modbus-registers.md"));

        var sourcesLabel = request.Sources.Count > 0 ? string.Join(", ", request.Sources) : request.DefaultSchemaDirectory;
        var docsLabel = request.DocsGeneratedDir is null ? "" : $" / {request.DocsGeneratedDir}";
        Console.WriteLine($"{files.Count} Datei(en) aus {sourcesLabel} -> {request.CoreGeneratedDir} / {request.WebGeneratedDir}{docsLabel}");
    }
}
