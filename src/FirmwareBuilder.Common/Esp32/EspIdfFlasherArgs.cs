using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common.Esp32;

public sealed class FlashSection
{
    [JsonPropertyName("offset")]
    public required string Offset { get; init; }

    [JsonPropertyName("file")]
    public required string File { get; init; }
}

public sealed class EspIdfFlasherArgs
{
    [JsonPropertyName("bootloader")]
    public required FlashSection Bootloader { get; init; }

    [JsonPropertyName("app")]
    public required FlashSection App { get; init; }

    [JsonPropertyName("partition-table")]
    public required FlashSection PartitionTable { get; init; }

    [JsonPropertyName("otadata")]
    public required FlashSection Otadata { get; init; }

    [JsonPropertyName("storage")]
    public FlashSection? Storage { get; init; }

    public static EspIdfFlasherArgs Load(string buildDir)
    {
        var path = Path.Combine(buildDir, "flasher_args.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{path} nicht gefunden -- zuerst BuildFirmware ausfuehren.");
        }

        return JsonSerializer.Deserialize<EspIdfFlasherArgs>(File.ReadAllText(path))
            ?? throw new InvalidOperationException($"{path} konnte nicht gelesen werden.");
    }
}
