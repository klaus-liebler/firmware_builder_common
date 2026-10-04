namespace FirmwareBuilder.Common.Stm32;

public interface IStm32ProgrammerOptions
{
    string ResolveStm32ProgrammerCli();
}

// Board-Typ-Defaults, auf die IBuildContext.BoardTypeName/-Version zurueckfallen, solange fuer ein
// Board noch kein echter Wert im Board-Archiv gepflegt wurde (s. BoardRecord). Aktuell STM32-only
// (sensact leitet BoardTypeName/-Version dynamisch aus node_id ab, s. NodeIdBoardTypeCatalog in
// sensact_firmware, braucht also keine appsettings-Defaults).
public interface IBoardDefaultsOptions
{
    string DefaultBoardTypeName { get; }
    string DefaultBoardTypeVersion { get; }
}

// Von Stm32BuildContextBase (s. dort) verlangte appsettings-Form -- jeder STM32-Konsument bindet
// seine appsettings.json-POCO zusaetzlich hieran (s. firmware_factory_control_unit/builder/
// BuilderSettings.cs), damit die geteilte Basisklasse Stm32Programmer/BoardDefaults generisch lesen
// kann, ohne den konkreten Settings-Typ zu kennen.
public interface IBuilderAppSettingsStm32 : IBuilderAppSettings
{
    IStm32ProgrammerOptions Stm32Programmer { get; }
    IBoardDefaultsOptions BoardDefaults { get; }
}
