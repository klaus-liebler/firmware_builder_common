namespace FirmwareBuilder.Common.Esp32;

// ESP32-Pendant zu CmakeFirmwareBuildService (STM32) -- ESP-IDFs eigenes Build-System (idf.py)
// braucht vorher export.bat/export.ps1 in DERSELBEN Shell-Session, deshalb ein einzelner
// Shell-Befehl statt zweier getrennter ProcessRunner-Aufrufe.
public static class Esp32FirmwareBuildService
{
    public static void Run(IBuildContextEsp32 ctx)
    {
        var exportBat = Path.Combine(ctx.IdfPath, "export.bat");
        ProcessRunner.RunInheritShellCommand($"\"{exportBat}\" && idf.py build", ctx.RootDir);
        Console.WriteLine("Firmware gebaut.");
    }
}
