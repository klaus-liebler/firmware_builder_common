namespace FirmwareBuilder.Common;

// Gemeinsame IBuildContext-Basisimplementierung fuer alle Konsumenten (aktuell sensact_firmware,
// firmware_factory_control_unit; perspektivisch labathome_firmware). Buendelt, was projektuebergreifend
// IDENTISCH berechnet wird (Pfade relativ zu RootDir, Git-Info) als konkrete Properties. Optionale
// ESP32/npm-Projekt-Konventionen (NpmPackagesDir, WebmanagerBestBinaryBuffersSchemaDir) leben NICHT
// hier, sondern auf IBuildContextEsp32 -- STM32 implementiert dieses Interface gar nicht erst, kein
// Bedarf fuer einen throwenden Default auf der gemeinsamen Basis.
//
// Board-Identitaets-Properties (BoardArchiveDir/BoardUid/ChipId/WebAdminPassword/BoardSettings/
// BoardTypeName/BoardTypeVersion/Hostname) sowie CertsDir/BoardsDir/Certificates bleiben ABSTRACT --
// die Persistenz-/Ableitungslogik unterscheidet sich zwischen den Projekten grundlegend genug
// (sensact: board_info.json-Root-Cache + numerische MAC; STM32: BoardStateStore + ChipUid-String),
// dass ein gemeinsamer Default hier mehr verschleiern als helfen wuerde.
//
// RootDir/WebRoot/BestBinaryBuffersSchemaDir/BoardInfoJsonPath werden EINMAL PRO PROZESS berechnet
// (statische Felder auf dieser Basisklasse) statt bei jedem Property-Zugriff neu -- unproblematisch,
// weil pro Prozess immer nur EIN Projekt-Builder laeuft (nie sensact und STM32 im selben Prozess),
// und der Wert sich waehrend eines Laufs nie aendert (anders als Board-Identitaet, die sich nach
// PrepareContextWithRealHardware aendern KANN und deshalb bewusst nicht gecacht wird).
public abstract class AbstractBuildContext : IBuildContext
{
    // protected statt private: Ableitungen (SensactBuildContext, Stm32BuildContext) brauchen diese
    // Werte oft auch in EIGENEN statischen Feldern/Methoden (z.B. fuer projekteigene Pfade, die von
    // RootDir abhaengen, oder in statischen Methoden wie SensactBuildContext.EnsureBoard, die schon
    // VOR einer fertigen Instanz laufen) -- ohne protected muessten sie BuildContextPaths.
    // FindRootDir() ein zweites Mal aufrufen (funktional gleich, aber unnoetige Doppelarbeit).
    protected static readonly string RootDirStatic = BuildContextPaths.FindRootDir();
    private static readonly string WebDirStatic = BuildContextPaths.WebDir(RootDirStatic);
    private static readonly string BestBinaryBuffersSchemaDirStatic = BuildContextPaths.BestBinaryBuffersSchemaDir(RootDirStatic);
    protected static readonly string BoardInfoJsonPathStatic = Path.Combine(RootDirStatic, "board_info.json");

    public IReadOnlyList<string> Args { get; }

    protected AbstractBuildContext(string[] args)
    {
        Args = args;
    }

    public string RootDir => RootDirStatic;
    public string FirmwareRoot => RootDirStatic;
    public string WebRoot => WebDirStatic;
    public string BestBinaryBuffersSchemaDir => BestBinaryBuffersSchemaDirStatic;
    public string BoardInfoJsonPath => BoardInfoJsonPathStatic;
    public GitInfo Git => GitInfoReader.ReadGitInfo(RootDir);

    // Reine String-Arithmetik, keine Projektspezifika -- s. bisherige SensactBuildContext.
    // RelativeFileDependency-Doku: npm versteht sowohl "/" als auch "\" in file:-Pfaden unter
    // Windows, hier trotzdem einheitlich "/" fuer bessere Lesbarkeit/Diff-Stabilitaet.
    public static string RelativeFileDependency(string from, string to) =>
        "file:" + Path.GetRelativePath(from, to).Replace('\\', '/');

    // BuilderOptions (sensact) und BuilderSettings (STM32) binden BoardStorage/Certificates schon
    // an dieselben geteilten Typen (s. IBuilderAppSettings) -- nur WELCHE appsettings.json-Klasse
    // geladen wird, ist projektspezifisch. Deshalb hier konkret, nicht mehr in jeder Ableitung neu
    // abgeschrieben.
    protected abstract IBuilderAppSettings Settings { get; }
    public string CertsDir => Settings.Certificates.CertsDir;
    public string BoardsDir => Settings.BoardStorage.BoardsDir;
    public ICertificateAuthorityOptions Certificates => Settings.Certificates;

    // --- Ab hier projektspezifisch, keine sinnvolle gemeinsame Ableitung moeglich ---

    public abstract string BoardArchiveDir { get; }
    public abstract string WebGeneratedDir { get; }
    public abstract string FirmwareGeneratedDir { get; }
    public abstract string BoardUid { get; }
    public abstract ChipId ChipId { get; }
    public abstract string? WebAdminPassword { get; }
    public abstract IReadOnlyDictionary<string, string> BoardSettings { get; }
    public abstract string BoardTypeName { get; }
    public abstract string BoardTypeVersion { get; }
    public abstract string Hostname { get; }
}
