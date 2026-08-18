namespace FirmwareBuilder.Common;

public sealed record BestBinaryBufferBuildRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string RootDir,
    string DefaultSchemaDirectory,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    string? ExplicitBoardId,
    IReadOnlyList<string> Sources);

public static class BestBinaryBufferBuildService
{
    public static void Run(BestBinaryBufferBuildRequest request)
    {
        var boardId = BoardArchiveContext.ResolveBoardId(request.ExplicitBoardId, request.BoardIdCacheFile);
        var coreOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.CoreGeneratedDir;
        var webOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.WebGeneratedDir;
        Directory.CreateDirectory(coreOut);
        Directory.CreateDirectory(webOut);

        var files = BestBinaryBuffersProtocolGenerator.Generate(new BestBinaryBuffersGenerateParams(
            RootDir: request.RootDir,
            Sources: request.Sources,
            DefaultSource: request.DefaultSchemaDirectory,
            IdMapPath: Path.Combine(request.DefaultSchemaDirectory, "ids.txt"),
            CppOutputFilePath: Path.Combine(coreOut, "ws_protocol.hh"),
            TsOutputFilePath: Path.Combine(webOut, "ws-protocol.ts")));

        var sourcesLabel = request.Sources.Count > 0
            ? string.Join(", ", request.Sources)
            : request.DefaultSchemaDirectory;

        Console.WriteLine(boardId is not null
            ? $"{files.Count} Datei(en) aus {sourcesLabel} -> Board-Archiv ({boardId})"
            : $"Kein Board-Kontext bekannt -- schreibe ws-protocol ({files.Count} Datei(en) aus {sourcesLabel}) " +
              "direkt nach Core/generated bzw. web/generated (kein Archiv-Eintrag).");
    }
}
