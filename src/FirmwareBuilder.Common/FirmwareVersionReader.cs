using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record FirmwareVersionInfo(int Major, int Minor, int Patch);

public static class FirmwareVersionReader
{
    public static FirmwareVersionInfo Read(string firmwareVersionJsonPath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(firmwareVersionJsonPath));
        var root = doc.RootElement;
        return new FirmwareVersionInfo(
            root.GetProperty("major").GetInt32(),
            root.GetProperty("minor").GetInt32(),
            root.GetProperty("patch").GetInt32());
    }
}
