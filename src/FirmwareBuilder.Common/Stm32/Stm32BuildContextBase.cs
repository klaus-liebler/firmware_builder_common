namespace FirmwareBuilder.Common.Stm32;

// Chip-generische IBuildContextStm32-Basisimplementierung -- alles, was zwischen JEDEM STM32-
// Konsumenten identisch ist (aktuell: firmware_factory_control_unit; ein zweiter STM32-Konsument
// wuerde dieselbe Ableitung ohne Neuimplementierung mitverwenden). Board-Identitaet
// (BoardUid/ChipUid/ChipId/Hostname/BoardSettings/BoardTypeName/BoardTypeVersion) wird
// einheitlich aus BoardArchiveContext/BoardStateStore + der ueber Stm32Settings gebundenen
// appsettings-Klasse abgeleitet -- liest wie AbstractBuildContext bei JEDEM Zugriff frisch von der
// Platte (kein gecachtes Feld).
//
// Bleiben in der konkreten Ableitung (s. z.B. firmware_factory_control_unit/builder/
// Stm32BuildContext.cs): WebGeneratedDir/FirmwareGeneratedDir/BuildDir/BoardIdCacheFile (projekt-
// eigene Pfad-Konventionen) sowie Stm32Settings selbst (welche appsettings.json-Klasse gebunden wird).
public abstract class Stm32BuildContextBase : AbstractBuildContext, IBuildContextStm32
{
    protected Stm32BuildContextBase(string[] args) : base(args)
    {
    }

    // Bindet AbstractBuildContext.Settings an dieselbe appsettings-Instanz wie Stm32Settings --
    // Ableitungen implementieren nur noch Stm32Settings, nicht mehr zusaetzlich Settings.
    protected sealed override IBuilderAppSettings Settings => Stm32Settings;

    protected abstract IBuilderAppSettingsStm32 Stm32Settings { get; }

    public abstract string BoardIdCacheFile { get; }

    // Release ist Default, nicht Debug: SecurityPolicy#Basic256Sha256's Software-RSA-2048 (kein
    // Hardware-PKA auf diesem STM32H563) verursacht in einem unoptimierten Debug-Build 13-17s
    // ThreadX-Thread-Starvation-Stalls pro Handshake -- das Board wirkt dann fuer jeden, der es
    // testet, wie haengengeblieben. --preset Debug explizit angeben, wenn wirklich gesteppt wird.
    public string Preset
    {
        get
        {
            var preset = Cli.GetOptionalArgValue(Args, "--preset") ?? "Release";
            if (!CmakeFirmwareBuildService.IsValidPreset(preset))
            {
                throw new ArgumentException($"Unbekanntes Preset \"{preset}\". Gueltig: {string.Join(", ", CmakeFirmwareBuildService.ValidPresets)}.");
            }
            return preset;
        }
    }

    protected string? ExplicitBoardId => Cli.GetOptionalArgValue(Args, "--board");

    public override string BoardUid => BoardArchiveContext.RequireBoardId(ExplicitBoardId, BoardIdCacheFile, "PrepareContextWithRealHardware");

    public override string BoardArchiveDir => BoardArchiveContext.BoardArchiveDir(Stm32Settings.BoardStorage, BoardUid);

    public string ChipUid
    {
        get
        {
            var parts = BoardUid.Split('_');
            return (parts.Length > 1 ? parts[1] : BoardUid).ToUpperInvariant();
        }
    }

    public override ChipId ChipId => ChipIdStm32.FromChipUidHex(ChipUid);

    public IStm32ProgrammerOptions Stm32Programmer => Stm32Settings.Stm32Programmer;

    public override string? WebAdminPassword => BoardStateStore.TryReadBoard(Stm32Settings.BoardStorage, BoardUid)?.WebAdminPassword;

    public override IReadOnlyDictionary<string, string> BoardSettings =>
        BoardStateStore.TryReadBoard(Stm32Settings.BoardStorage, BoardUid)?.BoardSettings ?? new Dictionary<string, string>();

    public override string BoardTypeName =>
        BoardStateStore.TryGetBoardTypeName(Stm32Settings.BoardStorage, BoardUid)
        ?? Stm32Settings.BoardDefaults.DefaultBoardTypeName;

    public override string BoardTypeVersion =>
        BoardStateStore.TryGetBoardTypeVersion(Stm32Settings.BoardStorage, BoardUid)
        ?? Stm32Settings.BoardDefaults.DefaultBoardTypeVersion;

    // "factory-box-{shortId}" deckt sich bewusst mit dem bereits bestehenden
    // Stm32HardwareIdentityService.BuildIdentity's Hostname-Format (dort waehrend ReadHardwareIds
    // aus den ChipUid-Woertern berechnet) -- keine zweite, abweichende Namenskonvention fuers selbe
    // Board einfuehren.
    public override string Hostname => BoardSettings.GetValueOrDefault(BoardSettingsKeys.OverrideHostname) is { Length: > 0 } name
        ? name
        : $"factory-box-{ChipUid[^6..].ToLowerInvariant()}";

    // --- Ab hier bleibt projektspezifisch, keine sinnvolle gemeinsame Ableitung moeglich ---
    // (WebGeneratedDir/FirmwareGeneratedDir sind bereits auf AbstractBuildContext abstract.)

    public abstract string BuildDir { get; }
}
