namespace FirmwareBuilder.Common.Esp32;

// Generisches ESP-IDF-Flashen (Sektionen aus flasher_args.json, optionaler NVS-Partition-Reset) --
// nichts davon ist sensact-spezifisch: flasher_args.json/partitions.csv sind Standard-ESP-IDF-
// Konventionen, "nvs" ist der uebliche Partitionsname fuer WLAN-/Usersettings in praktisch jedem
// ESP-IDF-Projekt. Nimmt IBuildContextEsp32 direkt entgegen -- alle gebrauchten Werte
// (RootDir/BuildDir/PartitionsCsvPath/FlashEncryptionKeyBurnedAndActivated/Args) sind bereits Teil
// des Kontexts, kein separater Request-Typ noetig.
public static class Esp32FlashService
{
    public static void Run(IBuildContextEsp32 ctx, bool writeStorage = true)
    {
        if (ctx.FlashEncryptionKeyBurnedAndActivated)
        {
            throw new NotImplementedException("Verschluesselter Flash-Pfad ist bewusst nicht implementiert (brennt EFuses permanent).");
        }

        var resetNvsPartition = Cli.HasFlag(ctx.Args, "--resetNVSPartition");

        var f = EspIdfFlasherArgs.Load(ctx.BuildDir);
        List<(string Offset, string File)> sections =
        [
            (f.Bootloader.Offset, Path.Combine(ctx.BuildDir, f.Bootloader.File)),
            (f.App.Offset, Path.Combine(ctx.BuildDir, f.App.File)),
            (f.PartitionTable.Offset, Path.Combine(ctx.BuildDir, f.PartitionTable.File)),
            (f.Otadata.Offset, Path.Combine(ctx.BuildDir, f.Otadata.File)),
        ];
        if (writeStorage && f.Storage is not null)
        {
            sections.Add((f.Storage.Offset, Path.Combine(ctx.BuildDir, f.Storage.File)));
        }

        foreach (var (_, file) in sections)
        {
            if (!File.Exists(file))
            {
                throw new InvalidOperationException($"Zu flashende Datei nicht gefunden: {file} -- zuerst BuildFirmware ausfuehren.");
            }
        }

        Console.WriteLine($"Flashe {sections.Count} Sektionen (unverschluesselt)...");
        EspToolService.WriteFlash(sections, ctx.RootDir);
        Console.WriteLine("Flash (nicht verschluesselt) abgeschlossen.");

        if (resetNvsPartition)
        {
            var nvs = PartitionsCsv.Find(ctx.PartitionsCsvPath, "nvs");
            if (nvs.Offset is null)
            {
                throw new InvalidOperationException("nvs-Partition hat kein Offset in partitions.csv -- kann nicht geloescht werden.");
            }
            Console.WriteLine($"--resetNVSPartition: loesche nvs-Partition (Offset 0x{nvs.Offset:X}, Groesse 0x{nvs.Size:X}) -- WLAN-/Usersettings gehen verloren.");
            EspToolService.EraseRegion($"0x{nvs.Offset:X}", nvs.Size, ctx.RootDir);
            Console.WriteLine("nvs-Partition geloescht.");
        }
    }
}
