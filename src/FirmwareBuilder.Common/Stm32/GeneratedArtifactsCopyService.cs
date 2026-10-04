namespace FirmwareBuilder.Common.Stm32;

// STM32-only in der Praxis (sensact_firmware ruft dies nie auf), deshalb hier unter Stm32/ statt im
// Wurzelverzeichnis. Nimmt IBuildContextStm32 direkt entgegen -- assetsDir/coreFiles/webFiles/
// assetFiles bleiben explizite Parameter, weil sie projektspezifische Dateilisten sind (kein
// generischer Interface-Wert).
public static class GeneratedArtifactsCopyService
{
    public static void CopyToBuildDirectories(
        IBuildContextStm32 ctx,
        string assetsDir,
        IReadOnlyList<string> coreFiles,
        IReadOnlyList<string> webFiles,
        IReadOnlyList<string> assetFiles)
    {
        var sourceDir = Path.Combine(ctx.BoardArchiveDir, "generated");
        if (!Directory.Exists(sourceDir))
        {
            throw new InvalidOperationException(
                $"Board-Archiv {sourceDir} existiert nicht -- zuerst ReadHardwareIds, GenerateCertificatesLazy/GenerateCertificatesForced, GenerateDeviceArtifacts, " +
                $"ReadModbusRegisterMapAndGenerateFiles und ReadGitStatusAndGenerateFiles fuer Board {ctx.BoardUid} ausfuehren.");
        }

        foreach (var file in coreFiles)
        {
            CopyIfExists(sourceDir, ctx.FirmwareGeneratedDir, file);
        }

        foreach (var file in assetFiles)
        {
            CopyIfExists(sourceDir, assetsDir, file);
        }

        foreach (var file in webFiles)
        {
            CopyIfExists(sourceDir, ctx.WebGeneratedDir, file);
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
