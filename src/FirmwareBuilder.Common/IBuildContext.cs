namespace FirmwareBuilder.Common;

// Der einzige Parametertyp, den [BuildStep]-Methoden entgegennehmen (bzw. IBuildContextEsp32/
// IBuildContextStm32 fuer Chip-Spezifika, s. Esp32/IBuildContextEsp32.cs, stm32/IBuildContextStm32.cs).
// Traegt alles, was in praktisch jedem Embedded-Projekt dieser Art gebraucht wird -- ersetzt die
// vormalige Wucherung aus projekteigenen BoardContext/IBoardInfo-Typen (sensact) bzw. acht
// einzelnen *Request-Records + Fabrikfunktionen (STM32-Referenzprojekt).
public interface IBuildContext
{
    string RootDir { get; }
    string FirmwareRoot { get; }
    string WebRoot { get; }
    string CertsDir { get; }
    string BoardsDir { get; }

    // Repo-Root-Cache-Konvention: Pfad zu einer optionalen, projektweiten Kopie des zuletzt
    // bekannten Board-Records (Inhalt/Nutzung ist Sache der jeweiligen Implementierung, z.B.
    // SensactBuildContext.Board -- STM32 nutzt stattdessen BoardIdCacheFile und liest diesen Pfad
    // nicht, das Feld existiert trotzdem einheitlich, damit ein kuenftiges ESP32-Projekt wie
    // labathome dieselbe Konvention ohne Neuimplementierung mitverwenden kann).
    string BoardInfoJsonPath { get; }

    // Projekteigenes Best-Binary-Buffers-Schemaverzeichnis -- identische Konvention in allen
    // Konsumenten (s. BuildContextPaths.BestBinaryBuffersSchemaDir).
    string BestBinaryBuffersSchemaDir { get; }

    // Aufgeloestes Verzeichnis des AKTUELLEN Boards -- liest die Board-Identitaet bei JEDEM Zugriff
    // frisch von der kleinen Cache-Datei auf der Platte (kein gecachtes Feld), damit ein
    // PrepareContextWithRealHardware/PrepareContextWithCommandLineArguments-Aufruf FRUEHER in
    // derselben Pipeline auch fuer spaeter aufgerufene Schritte sofort sichtbar ist.
    string BoardArchiveDir { get; }

    string WebGeneratedDir { get; }
    string FirmwareGeneratedDir { get; }

    // MAC (ESP32) bzw. Chip-UID (STM32) als kanonischer String-Schluessel -- s.o., frisch gelesen.
    string BoardUid { get; }

    // Rohe Chip-Identitaet (MAC bei ESP32, Unique-ID bei STM32) als gemeinsamer, chip-unabhaengiger
    // Speichertyp -- s. ChipId. ESP32-Implementierungen liefern ChipId.FromEsp32Mac48(mac),
    // STM32-Implementierungen ChipId.FromStm32Words(uidWords).
    ChipId ChipId { get; }

    GitInfo Git { get; }

    // Rohe argv (ohne den Step-Namen selbst) -- Escape-Hatch fuer schrittspezifische Flags
    // (--model, --nodeId, --preset, --board, ...), die nicht generisch genug fuer einen eigenen
    // IBuildContext-Member sind.
    IReadOnlyList<string> Args { get; }

    ICertificateAuthorityOptions Certificates { get; }

    string? WebAdminPassword { get; }

    // Generisches, flaches Key-Value-Overrides-Set, projektuebergreifend. Wohlbekannte Schluessel
    // (Konvention, keiner davon zwingend vorhanden) s. BoardSettingsKeys.
    IReadOnlyDictionary<string, string> BoardSettings { get; }

    // Physische Hardware-Familie + Revision (SemVer) -- NICHT dasselbe wie ein projektspezifischer
    // "Software-Funktionsumfang"-Selektor (z.B. sensacts NodeId, s. ISensactContext). Beide Werte
    // duerfen nie null/leer sein -- eine Implementierung, die den Wert nicht auflösen kann, wirft
    // stattdessen eine aussagekraeftige Exception.
    string BoardTypeName { get; }
    string BoardTypeVersion { get; }

    // Hostname des konkreten Boards (ersetzt sensacts vormals eigenes ISensactContext.BoardName) --
    // vom Nutzer explizit ueber BoardSettings[BoardSettingsKeys.OverrideHostname] gesetzt, oder
    // projektspezifisch automatisch erzeugt, wenn nicht gesetzt: sensact -> NodeId; labathome ->
    // "labathome-{mac6hex}"; firmware_factory_control_unit -> "factory-box-{chipUid6hex}" (deckt
    // sich mit dem bereits bestehenden Stm32HardwareIdentity.Hostname-Format). Wie BoardTypeName/
    // BoardTypeVersion nie null/leer -- die Implementierung loest immer auf.
    string Hostname { get; }
}
