namespace FirmwareBuilder.Common;

// IBuildContext-Erweiterung fuer STM32/ThreadX-Projekte: Chip-spezifische Pfade/Werte, insbesondere
// die STM32CubeProgrammer-Toolchain (Stm32Programmer) und das gewaehlte CMake-Preset.
public interface IBuildContextStm32 : IBuildContext
{
    string ChipUid { get; }
    IStm32ProgrammerOptions Stm32Programmer { get; }
    string BuildDir { get; }
    string Preset { get; }
}
