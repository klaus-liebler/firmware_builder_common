using System.Text.RegularExpressions;

namespace FirmwareBuilder.Common;

public static class Stm32FlashService
{
    public static void FlashElfAndVerify(IStm32ProgrammerOptions programmer, string elfPath, string rootDir)
    {
        ProcessRunner.RunInherit(programmer.ResolveStm32ProgrammerCli(), ["-c", "port=SWD", "-w", elfPath, "-v", "-rst"], rootDir);
    }

    public static string? TryDetectStlinkProbeSerial(IStm32ProgrammerOptions programmer, string rootDir)
    {
        try
        {
            var output = ProcessRunner.Run(programmer.ResolveStm32ProgrammerCli(), ["-l"], rootDir);
            var match = Regex.Match(output, @"ST-LINK SN\s*:\s*(\S+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch
        {
            return null;
        }
    }
}
