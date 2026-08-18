namespace FirmwareBuilder.Common;

public sealed record GeneratedArtifactsCopyRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string? ExplicitBoardId,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    string AssetsDir,
    IReadOnlyList<string> CoreFiles,
    IReadOnlyList<string> WebFiles,
    IReadOnlyList<string> AssetFiles);

public static class GeneratedArtifactsCopyService
{
    public static void CopyToBuildDirectories(GeneratedArtifactsCopyRequest request)
    {
        var boardId = BoardArchiveContext.RequireBoardId(request.ExplicitBoardId, request.BoardIdCacheFile, "ReadHardwareIds");
        var sourceDir = BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId);
        if (!Directory.Exists(sourceDir))
        {
            throw new InvalidOperationException(
                $"Board-Archiv {sourceDir} existiert nicht -- zuerst ReadHardwareIds, GenerateCertificatesLazy/GenerateCertificatesForced, GenerateDeviceArtifacts, " +
                $"ReadModbusRegisterMapAndGenerateFiles und ReadGitStatusAndGenerateFiles fuer Board {boardId} ausfuehren.");
        }

        foreach (var file in request.CoreFiles)
        {
            CopyIfExists(sourceDir, request.CoreGeneratedDir, file);
        }

        foreach (var file in request.AssetFiles)
        {
            CopyIfExists(sourceDir, request.AssetsDir, file);
        }

        foreach (var file in request.WebFiles)
        {
            CopyIfExists(sourceDir, request.WebGeneratedDir, file);
        }
    }

    private static void CopyIfExists(string srcDir, string destDir, string filename)
    {
        var src = Path.Combine(srcDir, filename);
        if (!File.Exists(src))
        {
            Console.WriteLine($"Uebersprungen: {filename} nicht im Board-Archiv vorhanden ({src}).");
            return;
        }

        Directory.CreateDirectory(destDir);
        var dest = Path.Combine(destDir, filename);
        File.Copy(src, dest, overwrite: true);
        Console.WriteLine($"{src} -> {dest}");
    }
}
