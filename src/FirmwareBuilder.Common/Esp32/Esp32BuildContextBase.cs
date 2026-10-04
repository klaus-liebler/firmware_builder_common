using System.Text.Json;

namespace FirmwareBuilder.Common.Esp32;

// Chip-generische IBuildContextEsp32-Basisimplementierung -- alles, was fuer JEDES ESP-IDF-Projekt
// identisch gilt (Board-Identitaet ist Mac-basiert, s. BoardPaths/BoardArchiveContext/
// BoardStateStore; IdfPath/PartitionsCsvPath/BuildDir sind reine ESP-IDF-Projektkonventionen).
// Aktuell EIN Konsument (sensact_firmware); ein kuenftiges ESP32-Projekt (labathome_firmware) kann
// dieselbe Ableitung ohne Neuimplementierung mitverwenden.
//
// Bleiben in der konkreten Ableitung (s. sensact_firmware/builder/SensactBuildContext.cs):
// WebGeneratedDir/FirmwareGeneratedDir (projekteigene generated/-Struktur) sowie
// BoardTypeName/BoardTypeVersion/Hostname -- sensact leitet diese dynamisch aus node_id ab
// (s. NodeIdBoardTypeCatalog), ein anderes ESP32-Projekt koennte das grundlegend anders loesen,
// deshalb kein gemeinsamer Default hier.
public abstract class Esp32BuildContextBase : AbstractBuildContext, IBuildContextEsp32
{
    protected Esp32BuildContextBase(string[] args) : base(args)
    {
    }

    // Bindet AbstractBuildContext.Settings an dieselbe appsettings-Instanz wie Esp32Settings --
    // Ableitungen implementieren nur noch Esp32Settings, nicht mehr zusaetzlich Settings.
    protected sealed override IBuilderAppSettings Settings => Esp32Settings;

    protected abstract IBuilderAppSettingsEsp32 Esp32Settings { get; }

    public string NpmPackagesDir => Esp32Settings.NpmPackagesDir;
    public string WebmanagerBestBinaryBuffersSchemaDir => Esp32Settings.WebmanagerBestBinaryBuffersSchemaDir;

    public string BuildDir => BuildDirStatic;
    private static readonly string BuildDirStatic = BuildContextPaths.BuildDir(RootDirStatic);

    public string PartitionsCsvPath => PartitionsCsvPathStatic;
    private static readonly string PartitionsCsvPathStatic = Path.Combine(RootDirStatic, "partitions.csv");

    public string IdfPath
    {
        get
        {
            var idfPath = Environment.GetEnvironmentVariable("IDF_PATH");
            if (string.IsNullOrWhiteSpace(idfPath))
            {
                throw new InvalidOperationException("IDF_PATH ist nicht gesetzt -- muss auf die ESP-IDF-Installationswurzel zeigen (enthaelt export.bat).");
            }
            if (!File.Exists(Path.Combine(idfPath, "export.bat")))
            {
                throw new InvalidOperationException($"IDF_PATH ist auf \"{idfPath}\" gesetzt, aber \"export.bat\" existiert dort nicht -- IDF_PATH korrigieren.");
            }
            return idfPath;
        }
    }

    // Liest die gecachte board_info.json aus dem Repo-Root frisch (kein gecachtes Feld). Nutzt den
    // geteilten BoardRecord-Typ (FirmwareBuilder.Common).
    public BoardRecord Board
    {
        get
        {
            if (!File.Exists(BoardInfoJsonPath))
            {
                throw new InvalidOperationException(
                    $"Keine board_info.json unter {BoardInfoJsonPath} -- noch kein Board jemals verbunden gewesen " +
                    "(fuer eine Live-Board-Abfrage s. PrepareContextWithRealHardware, fuer Offline-Arbeit s. projekteigenes Offline-Pendant).");
            }
            return JsonSerializer.Deserialize<BoardRecord>(File.ReadAllText(BoardInfoJsonPath))
                ?? throw new InvalidOperationException($"{BoardInfoJsonPath} konnte nicht gelesen werden.");
        }
    }

    private long BoardMac => Board.Mac ?? throw new InvalidOperationException($"{BoardInfoJsonPath}: Feld \"mac\" fehlt (unerwartet fuer ein ESP32-Board-Archiv).");

    public override ChipId ChipId => ChipIdEsp32.FromEsp32Mac48(BoardMac);

    public override string BoardUid => ChipId.ToBoardUid();
    public override string BoardArchiveDir => BoardArchiveContext.BoardArchiveDir(Esp32Settings.BoardStorage, BoardUid);

    public override string? WebAdminPassword => Board.WebAdminPassword;
    public override IReadOnlyDictionary<string, string> BoardSettings => Board.BoardSettings ?? [];
    public bool FlashEncryptionKeyBurnedAndActivated => Board.FlashEncryptionKeyBurnedAndActivated ?? false;
}
