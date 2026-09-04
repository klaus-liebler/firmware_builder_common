namespace FirmwareBuilder.Common.Esp32;

// IBuildContext-Erweiterung fuer ESP-IDF-Projekte: Chip-spezifische Pfade/Werte, insbesondere die
// ESP-IDF-Toolchain selbst (IdfPath) und das von ihr erzeugte Build-Verzeichnis.
public interface IBuildContextEsp32 : IBuildContext
{
    // Die MAC-Adresse ist ab jetzt IBuildContext.ChipId (s. ChipId.ToEsp32Mac48()) -- kein
    // separat gespeichertes long-Feld mehr.

    // Validierter IDF_PATH (wirft mit einer klaren Meldung, falls die Umgebungsvariable fehlt oder
    // nicht auf eine echte ESP-IDF-Installation zeigt -- export.bat/export.ps1 muss vorher gelaufen sein).
    string IdfPath { get; }

    string BuildDir { get; }
    string PartitionsCsvPath { get; }
    bool FlashEncryptionKeyBurnedAndActivated { get; }

    // npm-Projekt-Konventionen fuer generierte TS-Konsumenten (s. WsProtocolBuildService,
    // GenerateSensactFiles) -- ESP32/Web-spezifisch, deshalb hier statt auf der Basis-IBuildContext
    // (STM32 implementiert dieses Interface nicht, braucht also keinen Default/Wurf).
    string NpmPackagesDir { get; }
    string WebmanagerBestBinaryBuffersSchemaDir { get; }
}
