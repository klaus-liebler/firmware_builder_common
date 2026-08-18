using System.Text.Json;
using System.Text.RegularExpressions;

namespace FirmwareBuilder.Common.Esp32;

public sealed record Esp32HardwareIds(string ChipType, long Mac, bool HasFlashEncryptionKey);

public static class EspToolService
{
    private const int UserEmpty = 0;
    private const int XtsAes256Key1 = 2;
    private const int XtsAes256Key2 = 3;
    private const int XtsAes128Key = 4;

    public static Esp32HardwareIds ReadHardwareIds(string workingDirectory)
    {
        var (chipType, mac, port) = ReadMacAndChipType(workingDirectory);
        var hasEncryptionKey = ReadFlashEncryptionKeyPresence(port, workingDirectory);
        return new Esp32HardwareIds(chipType, mac, hasEncryptionKey);
    }

    public static void WriteFlash(IReadOnlyList<(string Offset, string File)> sections, string workingDirectory)
    {
        List<string> args = ["write-flash", "--flash-size", "keep"];
        foreach (var (offset, file) in sections)
        {
            args.Add(offset);
            args.Add(file);
        }

        ProcessRunner.RunInherit("esptool", args, workingDirectory);
    }

    public static void EraseRegion(string offset, long size, string workingDirectory)
    {
        ProcessRunner.RunInherit("esptool", ["erase-region", offset, $"0x{size:X}"], workingDirectory);
    }

    private static (string ChipType, long Mac, string Port) ReadMacAndChipType(string workingDirectory)
    {
        var output = ProcessRunner.Run("esptool", ["read-mac"], workingDirectory);
        var chipMatch = Regex.Match(output, @"Chip type:\s*(\S+)");
        var macMatch = Regex.Match(output, @"MAC:\s*([0-9a-fA-F:]{17})");
        var portMatch = Regex.Match(output, @"Serial port (\S+):");

        if (!chipMatch.Success || !macMatch.Success || !portMatch.Success)
        {
            throw new InvalidOperationException($"\"esptool read-mac\" lieferte kein auswertbares Ergebnis:\n{output}");
        }

        var macBytes = macMatch.Groups[1].Value.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();
        long mac = 0;
        foreach (var b in macBytes)
        {
            mac = (mac << 8) | b;
        }

        return (chipMatch.Groups[1].Value, mac, portMatch.Groups[1].Value);
    }

    private static bool ReadFlashEncryptionKeyPresence(string port, string workingDirectory)
    {
        var output = ProcessRunner.Run("espefuse", ["--port", port, "summary", "--format", "json"], workingDirectory);
        var jsonStart = output.IndexOf('{');
        if (jsonStart < 0)
        {
            throw new InvalidOperationException($"\"espefuse summary --format json\" lieferte kein auswertbares JSON:\n{output}");
        }

        using var doc = JsonDocument.Parse(output[jsonStart..]);
        var root = doc.RootElement;

        if (!root.TryGetProperty("SPI_BOOT_CRYPT_CNT", out _) || !root.TryGetProperty("KEY_PURPOSE_0", out _))
        {
            return false;
        }

        var purpose0 = ReadRawValue(root, "KEY_PURPOSE_0");
        var purpose1 = ReadRawValue(root, "KEY_PURPOSE_1");
        var spiBootCryptCnt = ReadRawValue(root, "SPI_BOOT_CRYPT_CNT");
        var hasOddBitCount = spiBootCryptCnt is 0b1 or 0b11 or 0b111;

        if (purpose0 == XtsAes256Key1 && purpose1 == XtsAes256Key2)
        {
            if (!hasOddBitCount)
            {
                throw new InvalidOperationException(
                    $"Encryption Key ist XTS_AES_256, aber SPI_BOOT_CRYPT_CNT hat keine ungerade Anzahl gesetzter Bits, sondern 0b{Convert.ToString(spiBootCryptCnt, 2)}.");
            }
            return true;
        }

        if (purpose0 == XtsAes128Key && purpose1 == UserEmpty)
        {
            if (!hasOddBitCount)
            {
                throw new InvalidOperationException(
                    $"Encryption Key ist XTS_AES_128, aber SPI_BOOT_CRYPT_CNT hat keine ungerade Anzahl gesetzter Bits, sondern 0b{Convert.ToString(spiBootCryptCnt, 2)}.");
            }
            return true;
        }

        if (purpose0 != UserEmpty || purpose1 != UserEmpty)
        {
            throw new InvalidOperationException("Unerwartete Key-Purposes in KEY_PURPOSE_0/KEY_PURPOSE_1.");
        }

        return false;
    }

    private static int ReadRawValue(JsonElement root, string fieldName)
    {
        var raw = root.GetProperty(fieldName).GetProperty("raw_value").GetString()!;
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[2..];
        }

        return Convert.ToInt32(raw, 16);
    }
}
