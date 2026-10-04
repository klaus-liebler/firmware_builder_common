namespace FirmwareBuilder.Common.Stm32;

// IBuildContext-Erweiterung fuer STM32/ThreadX-Projekte: Chip-spezifische Pfade/Werte, insbesondere
// die STM32CubeProgrammer-Toolchain (Stm32Programmer) und das gewaehlte CMake-Preset.
public interface IBuildContextStm32 : IBuildContext
{
    string ChipUid { get; }
    IStm32ProgrammerOptions Stm32Programmer { get; }
    string BuildDir { get; }
    string Preset { get; }

    // Pfad zur kleinen Cache-Datei, die die zuletzt aufgeloeste BoardUid zwischen [BuildStep]-
    // Aufrufen persistiert (s. BoardArchiveContext.WriteCachedBoardId/RequireBoardId). War bisher
    // ein projekteigenes statisches Feld (Stm32BuildContext.BoardIdCacheFile), das Program.cs und
    // die Provisioning-Requests direkt referenzierten -- jetzt echtes Interface-Member, damit
    // Stm32BoardProvisioningService & Co. es ueber ctx statt ueber einen Extra-Parameter bekommen.
    string BoardIdCacheFile { get; }
}
