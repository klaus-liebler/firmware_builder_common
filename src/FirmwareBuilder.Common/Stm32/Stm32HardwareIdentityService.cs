using System.Text.RegularExpressions;

namespace FirmwareBuilder.Common.Stm32;

public sealed record Stm32UidReadResult(string Uid, uint[] Words);

public sealed record Stm32HardwareIdentity(
    string ChipUid,
    string ShortId,
    string Hostname,
    byte[] UsbNcmMac,
    byte[] EthMac)
{
    // Reine Funktion von ChipId, dieselbe Formel wie bei ESP32 (s. ChipId.FormatBoardUid) -- der
    // Umweg ueber FromChipUidHex/ToBoardUid statt weiterhin "{ShortId}_{ChipUid}" von Hand zu
    // bauen, ist hier ein No-Op (STM32s 96 Bit sind exakt 3 volle Woerter, s. ChipIdStm32.ToBoardUid-
    // Kommentar), macht aber den gemeinsamen Code-Pfad tatsaechlich gemeinsam.
    public string BoardId => ChipIdStm32.FromChipUidHex(ChipUid).ToBoardUid();
}

public static class Stm32HardwareIdentityService
{
    // Die 96-Bit-Unique-ID liegt je nach Chip woanders. Welcher Chip dran ist, meldet
    // STM32_Programmer_CLI beim Verbinden als "Device ID" (DBGMCU_IDCODE.DEV_ID; die Werte sind die
    // seiner Data_Base/STM32_Prog_DB_0x*.xml):
    //   0x484 STM32H56x/573: UID_BASE 0x08FFF800 (RM0481)
    //   0x450 STM32H742/743/750/753: UID_BASE 0x1FF1E800 (ST stm32h743xx.h)
    private static readonly Dictionary<int, string> UidAddressByDeviceId = new()
    {
        [0x484] = "0x08FFF800",
        [0x450] = "0x1FF1E800",
    };

    public static Stm32UidReadResult ReadUniqueId(IStm32ProgrammerOptions options, string workingDirectory)
    {
        var cliPath = options.ResolveStm32ProgrammerCli();
        var deviceId = ParseDeviceId(RunProgrammer(cliPath, ["-c", "port=SWD"], workingDirectory));
        if (!UidAddressByDeviceId.TryGetValue(deviceId, out var uidAddress))
        {
            throw new InvalidOperationException(
                $"Unbekannter Chip (Device ID 0x{deviceId:X3}): keine Unique-ID-Adresse hinterlegt. Bekannt: " +
                string.Join(", ", UidAddressByDeviceId.Keys.Select(k => $"0x{k:X3}")) + ".");
        }
        var output = RunProgrammer(cliPath, ["-c", "port=SWD", "-r32", uidAddress, "12"], workingDirectory);
        return ParseUniqueId(output, uidAddress);
    }

    /// <summary>Die Device ID aus der Verbindungsausgabe von STM32_Programmer_CLI ("Device ID   : 0x450").</summary>
    public static int ParseDeviceId(string output)
    {
        var match = Regex.Match(output, @"Device ID\s*:\s*0x([0-9A-Fa-f]+)");
        if (!match.Success)
        {
            throw new InvalidOperationException($"Konnte die Device ID nicht aus der STM32_Programmer_CLI-Ausgabe lesen:\n{output}");
        }
        return Convert.ToInt32(match.Groups[1].Value, 16);
    }

    private static string RunProgrammer(string cliPath, string[] args, string workingDirectory)
    {
        try
        {
            return ProcessRunner.Run(cliPath, args, workingDirectory);
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
    }

    /// <summary>Die drei UID-Woerter aus der Ausgabe von "-r32 &lt;uidAddress&gt; 12".</summary>
    public static Stm32UidReadResult ParseUniqueId(string output, string uidAddress)
    {
        var match = Regex.Match(
            output,
            $@"{Regex.Escape(uidAddress)}\s*:\s*([0-9A-Fa-f]{{8}})\s+([0-9A-Fa-f]{{8}})\s+([0-9A-Fa-f]{{8}})");
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
