namespace FirmwareBuilder.Common;

// Zentralisierte, konkrete Implementierung von IBoardsDirectoryOptions -- vorher in beiden
// Projekten (sensact_firmware, firmware_factory_control_unit) unabhaengig als projekt-lokale
// Klasse dupliziert.
public sealed class BoardsDirectoryOptions : IBoardsDirectoryOptions
{
    public string BoardsDir { get; set; } = "";
}
