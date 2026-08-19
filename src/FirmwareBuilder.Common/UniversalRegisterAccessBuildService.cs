using UniversalRegisterAccess;

namespace FirmwareBuilder.Common;

public sealed record UniversalRegisterAccessBuildRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string RootDir,
    string DefaultSchemaDirectory,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    string? ExplicitBoardId,
    IReadOnlyList<string> Sources);

public static class UniversalRegisterAccessBuildService
{
    public static void Run(UniversalRegisterAccessBuildRequest request)
    {
        var files = SourceFileResolver.ResolveFiles(request.RootDir, request.Sources, request.DefaultSchemaDirectory, ".cs");

        var boardId = BoardArchiveContext.ResolveBoardId(request.ExplicitBoardId, request.BoardIdCacheFile);
        var coreOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.CoreGeneratedDir;
        var webOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.WebGeneratedDir;
        Directory.CreateDirectory(coreOut);
        Directory.CreateDirectory(webOut);

        SchemaCompiler.Compile(files,
            Path.Combine(coreOut, "modbus_registers_generated.hh"),
            Path.Combine(webOut, "register-map.ts"),
            Path.Combine(coreOut, "opcua_registers_generated.hh"));

        var sourcesLabel = request.Sources.Count > 0 ? string.Join(", ", request.Sources) : request.DefaultSchemaDirectory;
        Console.WriteLine(boardId is not null
            ? $"{files.Count} Datei(en) aus {sourcesLabel} -> Board-Archiv ({boardId})"
            : $"Kein Board-Kontext bekannt -- schreibe Register-Map ({files.Count} Datei(en) aus {sourcesLabel}) " +
              "direkt nach Core/generated bzw. web/generated (kein Archiv-Eintrag).");
    }
}
