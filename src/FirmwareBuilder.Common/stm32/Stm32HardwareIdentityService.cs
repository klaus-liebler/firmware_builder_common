using System.Text.RegularExpressions;

namespace FirmwareBuilder.Common;

public sealed record Stm32UidReadResult(string Uid, uint[] Words);

public sealed record Stm32HardwareIdentity(
    string ChipUid,
    string ShortId,
    string Hostname,
    byte[] UsbNcmMac,
    byte[] EthMac)
{
    public string BoardId => $"{ShortId}_{ChipUid.ToLowerInvariant()}";
}

public static class Stm32HardwareIdentityService
{
    private const string UidAddress = "0x08FFF800";

    public static Stm32UidReadResult ReadUniqueId(IStm32ProgrammerOptions options, string workingDirectory)
    {
        var cliPath = options.ResolveStm32ProgrammerCli();
        string output;
        try
        {
            output = ProcessRunner.Run(cliPath, ["-c", "port=SWD", "-r32", UidAddress, "12"], workingDirectory);
        }
        catch (ProcessException ex)
        {
            if (ex.ExitCode == -1)
            {
                throw new InvalidOperationException(
                    $"STM32_Programmer_CLI.exe nicht gefunden unter:\n  {cliPath}\n" +
                    "Pruefe die STM32CubeProgrammer-Installation bzw. die Umgebungsvariable STM32_PRG_PATH " +
                    "(darf auf die .exe selbst oder deren bin/-Ordner zeigen).", ex);
            }

            var details = (ex.StdErr.Length > 0 ? ex.StdErr : ex.StdOut).Trim();
            throw new InvalidOperationException(
                $"STM32_Programmer_CLI konnte das Board nicht auslesen (Exit-Code {ex.ExitCode}).\n" +
                "Moegliche Ursachen: ST-LINK nicht angeschlossen/nicht mit Strom versorgt, oder bereits von " +
                "einer anderen Anwendung belegt (z.B. eine laufende Debug-Sitzung in STM32CubeIDE -- die " +
                "beenden und erneut versuchen).\n" +
                (details.Length > 0 ? $"Ausgabe des Tools:\n{details}" : ""), ex);
        }

        var match = Regex.Match(
            output,
            $@"{Regex.Escape(UidAddress)}\s*:\s*([0-9A-Fa-f]{{8}})\s+([0-9A-Fa-f]{{8}})\s+([0-9A-Fa-f]{{8}})");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Konnte Unique-ID nicht aus STM32_Programmer_CLI-Ausgabe lesen:\n{output}");
        }

        var words = new[]
        {
            Convert.ToUInt32(match.Groups[1].Value, 16),
            Convert.ToUInt32(match.Groups[2].Value, 16),
            Convert.ToUInt32(match.Groups[3].Value, 16),
        };
        var uid = string.Concat(match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value).ToUpperInvariant();
        return new Stm32UidReadResult(uid, words);
    }

    public static Stm32HardwareIdentity BuildIdentity(Stm32UidReadResult uid)
    {
        var shortId = uid.Uid[^6..].ToLowerInvariant();
        return new Stm32HardwareIdentity(
            ChipUid: uid.Uid,
            ShortId: shortId,
            Hostname: $"factory-box-{shortId}",
            UsbNcmMac: ComputeUsbNcmMac(uid.Words),
            EthMac: ComputeEthMac(uid.Words));
    }

    public static string ToHex2(byte b) => b.ToString("X2");

    private static byte[] ComputeUsbNcmMac(uint[] words)
    {
        var (w0, w1, w2) = (words[0], words[1], words[2]);
        return [0x02, (byte)(w0 >> 24), (byte)w0, (byte)(w1 >> 24), (byte)w1, (byte)w2];
    }

    private static byte[] ComputeEthMac(uint[] words)
    {
        var (w0, w1, w2) = (words[0], words[1], words[2]);
        var folded = w0 ^ w1 ^ w2;
        return
        [
            0x02,
            (byte)(folded >> 24),
            (byte)(folded >> 16),
            (byte)(folded >> 8),
            (byte)folded,
            (byte)(((w0 >> 8) ^ (w1 >> 16) ^ (w2 >> 24)) & 0xff),
        ];
    }
}
