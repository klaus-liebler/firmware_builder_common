namespace FirmwareBuilder.Common;

// Zentralisiert (vorher projekt-lokal in firmware_factory_control_unit/builder/BuilderSettings.cs) --
// STM32-spezifisch, aber unter dem gemeinsamen stm32/-Zweig, analog zu Stm32HardwareIdentityService etc.
public sealed class Stm32ProgrammerOptions : IStm32ProgrammerOptions
{
    public string Stm32ProgrammerCli { get; set; } = "";

    public string ResolveStm32ProgrammerCli()
    {
        var configured = Environment.GetEnvironmentVariable("STM32_PRG_PATH");
        if (string.IsNullOrEmpty(configured))
        {
            configured = Stm32ProgrammerCli;
        }
        if (Directory.Exists(configured))
        {
            return Path.Combine(configured, "STM32_Programmer_CLI.exe");
        }
        return configured;
    }
}
