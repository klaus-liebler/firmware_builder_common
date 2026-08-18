namespace FirmwareBuilder.Common;

public static class BoardArchiveContext
{
    public static string BoardArchiveDir(IBoardsDirectoryOptions options, string boardId) =>
        Path.Combine(options.BoardsDir, boardId);

    public static string BoardGeneratedDir(IBoardsDirectoryOptions options, string boardId) =>
        Path.Combine(BoardArchiveDir(options, boardId), "generated");

    public static string? ReadCachedBoardId(string boardIdCacheFile)
    {
        if (!File.Exists(boardIdCacheFile))
        {
            return null;
        }

        var content = File.ReadAllText(boardIdCacheFile).Trim();
        return content.Length > 0 ? content : null;
    }

    public static void WriteCachedBoardId(string buildDir, string boardIdCacheFile, string boardId)
    {
        Directory.CreateDirectory(buildDir);
        File.WriteAllText(boardIdCacheFile, boardId);
    }

    public static string? ResolveBoardId(string? explicitBoardId, string boardIdCacheFile) =>
        explicitBoardId ?? ReadCachedBoardId(boardIdCacheFile);

    public static string RequireBoardId(string? explicitBoardId, string boardIdCacheFile, string hintPhaseName)
    {
        var boardId = ResolveBoardId(explicitBoardId, boardIdCacheFile);
        if (boardId is null)
        {
            throw new InvalidOperationException(
                "Kein Board-Kontext bekannt (weder --board angegeben noch build/.last-board-id vorhanden). " +
                $"Zuerst '{hintPhaseName}' mit angeschlossenem Board ausfuehren.");
        }

        return boardId;
    }
}
