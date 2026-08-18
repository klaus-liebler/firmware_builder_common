using System.Text;
using System.Text.Json;

namespace FirmwareBuilder.Common;

public sealed record ModbusRegisterMapBuildRequest(
    IBoardsDirectoryOptions BoardStorage,
    string BoardIdCacheFile,
    string RootDir,
    string CoreGeneratedDir,
    string WebGeneratedDir,
    string? ExplicitBoardId,
    string RegisterMapFileName = "register-map.json");

public static class ModbusRegisterMapBuildService
{
    public static void Run(ModbusRegisterMapBuildRequest request)
    {
        var schemaPath = Path.Combine(request.RootDir, request.RegisterMapFileName);
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var schema = doc.RootElement;

        var inputInc = GenerateInputInc(schema);
        var holdingInc = GenerateHoldingInc(schema);
        var maxIndexInc = GenerateMaxIndexInc(schema);
        var ts = GenerateTs(schema);

        var boardId = BoardArchiveContext.ResolveBoardId(request.ExplicitBoardId, request.BoardIdCacheFile);
        var coreOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.CoreGeneratedDir;
        var webOut = boardId is not null ? BoardArchiveContext.BoardGeneratedDir(request.BoardStorage, boardId) : request.WebGeneratedDir;

        Directory.CreateDirectory(coreOut);
        Directory.CreateDirectory(webOut);
        File.WriteAllText(Path.Combine(coreOut, "register_input.inc"), inputInc);
        File.WriteAllText(Path.Combine(coreOut, "register_holding.inc"), holdingInc);
        File.WriteAllText(Path.Combine(coreOut, "register_maxindex.inc"), maxIndexInc);
        File.WriteAllText(Path.Combine(webOut, "register-map.ts"), ts);

        if (boardId is not null)
        {
            Console.WriteLine($"{schemaPath} -> Board-Archiv ({boardId})");
        }
        else
        {
            Console.WriteLine(
                "Kein Board-Kontext bekannt -- schreibe Register-Map direkt nach Core/generated bzw. web/generated (kein Archiv-Eintrag).");
        }
    }

    private static string? GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool GetBool(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.True;

    private static int? GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static List<string> RegCommentParts(JsonElement reg)
    {
        var parts = new List<string>();
        var description = GetString(reg, "description");
        if (description is not null) parts.Add(description);
        var unit = GetString(reg, "unit");
        if (unit is not null) parts.Add($"[{unit}]");
        if (GetBool(reg, "signed")) parts.Add("signed");
        var gpio = GetString(reg, "gpio");
        if (gpio is not null) parts.Add(gpio);
        if (reg.TryGetProperty("i2c", out var i2c) && i2c.ValueKind == JsonValueKind.Object)
        {
            var bus = GetString(i2c, "bus");
            var irqPin = GetString(i2c, "irqPin");
            parts.Add(irqPin is not null ? $"{bus}, IRQ {irqPin}" : bus!);
        }
        return parts;
    }

    private static IEnumerable<JsonElement> FlattenRegisters(JsonElement registers)
    {
        foreach (var entry in registers.EnumerateArray())
        {
            if (entry.TryGetProperty("combine", out var combine))
            {
                foreach (var r in combine.GetProperty("registers").EnumerateArray())
                {
                    yield return r;
                }
            }
            else
            {
                yield return entry;
            }
        }
    }

    private static string GenFlatBank(JsonElement bank)
    {
        var sb = new StringBuilder();
        foreach (var region in bank.GetProperty("regions").EnumerateArray())
        {
            var title = GetString(region, "title");
            var startAddress = GetInt(region, "startAddress")!.Value;
            var endAddress = GetInt(region, "endAddress")!.Value;
            var id = GetString(region, "id");
            sb.Append($"// {title} (Region-Start {startAddress}, Reserve bis {endAddress})\n");
            foreach (var reg in FlattenRegisters(region.GetProperty("registers")))
            {
                var name = GetString(reg, "name");
                var address = GetInt(reg, "address")!.Value;
                var comment = string.Join(" -- ", RegCommentParts(reg));
                sb.Append($"constexpr uint16_t {name} = {address};{(comment.Length > 0 ? " // " + comment : "")}\n");
            }
            sb.Append($"constexpr uint16_t {id}_REGION_START = {startAddress};\n");
            sb.Append($"constexpr uint16_t {id}_REGION_END   = {endAddress};\n\n");
        }
        return sb.ToString();
    }

    private const string IncFileHeader = """
        // GENERIERT von FirmwareBuilder.Common/ModbusRegisterMapBuildService.cs aus register-map.json -- nicht von
        // Hand editieren. Aenderungen an der Register-Map gehoeren in register-map.json, anschliessend die
        // Phase \"ReadModbusRegisterMapAndGenerateFiles\" erneut ausfuehren.
        //
        // Nur per #include aus Core/Src/modbus_register_model.hh eingebunden (innerhalb des dort
        // bereits geoeffneten namespace-Blocks), nicht eigenstaendig uebersetzbar -- enthaelt bewusst
        // nur constexpr-Zeilen, keine eigene namespace-Deklaration/Klammern.

        """;

    private static string GenerateInputInc(JsonElement schema)
    {
        var healthBits = new StringBuilder("// HealthState-Bitfeld (Input::HEALTH_STATE), s. register-map.json healthBits\n");
        foreach (var bit in schema.GetProperty("healthBits").EnumerateArray())
        {
            healthBits.Append($"constexpr uint16_t HEALTH_BIT_{GetString(bit, "name")} = {GetInt(bit, "bit")};\n");
        }

        return $"""
            {IncFileHeader}//
            // Adressen sind Offsets in ModbusRegisterModel::input_registers_.
            // 32-Bit-Werte belegen zwei Register (High-Word zuerst), siehe MbapHeader::serialize.
            // Block-Markierungen heissen *_REGION_START/*_REGION_END statt *_BASE/*_END, da z.B.
            // ETH_BASE/PWR_BASE bereits als CMSIS-Peripherie-Basisadressen vergeben sind.

            {GenFlatBank(schema.GetProperty("banks").GetProperty("input"))}{healthBits}
            """;
    }

    private static string GenerateHoldingInc(JsonElement schema)
    {
        return $"""
            {IncFileHeader}//
            // Adressen sind Offsets in ModbusRegisterModel::holding_registers_.
            // 32-Bit-Werte belegen zwei Register (High-Word zuerst), siehe MbapHeader::serialize.

            {GenFlatBank(schema.GetProperty("banks").GetProperty("holding"))}
            """;
    }

    private static string GenerateMaxIndexInc(JsonElement schema)
    {
        var inputRegions = schema.GetProperty("banks").GetProperty("input").GetProperty("regions").EnumerateArray().ToList();
        var holdingRegions = schema.GetProperty("banks").GetProperty("holding").GetProperty("regions").EnumerateArray().ToList();
        var lastInputRegion = inputRegions[^1];
        var lastHoldingRegion = holdingRegions[^1];

        return $"""
            {IncFileHeader}//
            // Hoechster belegter Index je Register-Bank -- bestimmt die Groesse der
            // ModbusRegisterModel-Arrays (s. modbus_register_model.hh). Bei Erweiterung der Register-Map
            // (neue Region ans Ende angehaengt) hier mitziehen. Referenziert Input::/Holding::, die im
            // umschliessenden modbus_register_model.hh bereits vor dieser #include definiert sind.
            constexpr uint16_t INPUT_REGISTER_MAX_INDEX   = Input::{GetString(lastInputRegion, "id")}_REGION_END;
            constexpr uint16_t HOLDING_REGISTER_MAX_INDEX = Holding::{GetString(lastHoldingRegion, "id")}_REGION_END;

            """;
    }

    private static string TsStringLiteral(string s) => JsonSerializer.Serialize(s, JsonDefaults.Compact);

    private static string GenTsRegister(JsonElement entry, string bank)
    {
        if (entry.TryGetProperty("combine", out var group))
        {
            var registers = group.GetProperty("registers").EnumerateArray().ToList();
            var first = registers[0];
            var fields = new List<string>
            {
                $"name: {TsStringLiteral(GetString(group, "label")!)}",
                $"address: {GetInt(first, "address")}",
                $"bank: {TsStringLiteral(bank)}",
            };
            var description = GetString(group, "description") ?? GetString(first, "description");
            if (description is not null) fields.Add($"description: {TsStringLiteral(description)}");
            var unit = GetString(first, "unit");
            if (unit is not null) fields.Add($"unit: {TsStringLiteral(unit)}");
            if (GetBool(group, "signed")) fields.Add("signed: true");
            fields.Add($"display: {TsStringLiteral(GetString(group, "display") ?? "decimal")}");
            fields.Add($"combine: {{ count: {registers.Count} }}");
            return $"{{ {string.Join(", ", fields)} }}";
        }

        {
            var fields = new List<string>
            {
                $"name: {TsStringLiteral(GetString(entry, "name")!)}",
                $"address: {GetInt(entry, "address")}",
                $"bank: {TsStringLiteral(bank)}",
            };
            var description = GetString(entry, "description");
            if (description is not null) fields.Add($"description: {TsStringLiteral(description)}");
            var unit = GetString(entry, "unit");
            if (unit is not null) fields.Add($"unit: {TsStringLiteral(unit)}");
            var gpio = GetString(entry, "gpio");
            if (gpio is not null) fields.Add($"gpio: {TsStringLiteral(gpio)}");
            if (GetBool(entry, "signed")) fields.Add("signed: true");
            if (entry.TryGetProperty("i2c", out var i2c) && i2c.ValueKind == JsonValueKind.Object)
            {
                var i2cFields = new List<string> { $"bus: {TsStringLiteral(GetString(i2c, "bus")!)}" };
                var irqPin = GetString(i2c, "irqPin");
                if (irqPin is not null) i2cFields.Add($"irqPin: {TsStringLiteral(irqPin)}");
                fields.Add($"i2c: {{ {string.Join(", ", i2cFields)} }}");
            }
            var min = GetInt(entry, "min");
            if (min is not null) fields.Add($"min: {min}");
            var max = GetInt(entry, "max");
            if (max is not null) fields.Add($"max: {max}");
            fields.Add($"display: {TsStringLiteral(GetString(entry, "display") ?? "decimal")}");
            return $"{{ {string.Join(", ", fields)} }}";
        }
    }

    private static string GenerateTs(JsonElement schema)
    {
        var orderedByOriginalLayout = new List<(string Title, string Bank, JsonElement Registers)>();
        foreach (var region in schema.GetProperty("banks").GetProperty("input").GetProperty("regions").EnumerateArray())
        {
            var registers = region.GetProperty("registers");
            if (registers.GetArrayLength() > 0) orderedByOriginalLayout.Add((GetString(region, "title")!, "input", registers));
        }
        foreach (var region in schema.GetProperty("banks").GetProperty("holding").GetProperty("regions").EnumerateArray())
        {
            var registers = region.GetProperty("registers");
            if (registers.GetArrayLength() > 0) orderedByOriginalLayout.Add((GetString(region, "title")!, "holding", registers));
        }

        var regionsSource = string.Join(",\n", orderedByOriginalLayout.Select(region =>
        {
            var regsSource = string.Join(",\n", region.Registers.EnumerateArray().Select(entry => "\t\t\t" + GenTsRegister(entry, region.Bank)));
            return $"\t{{\n\t\ttitle: {TsStringLiteral(region.Title)},\n\t\tregisters: [\n{regsSource}\n\t\t]\n\t}}";
        }));

        var inputRegions = schema.GetProperty("banks").GetProperty("input").GetProperty("regions").EnumerateArray().ToList();
        var holdingRegions = schema.GetProperty("banks").GetProperty("holding").GetProperty("regions").EnumerateArray().ToList();
        var lastInputRegion = inputRegions[^1];
        var lastHoldingRegion = holdingRegions[^1];
        var inputCount = GetInt(lastInputRegion, "endAddress")!.Value + 1;
        var holdingCount = GetInt(lastHoldingRegion, "endAddress")!.Value + 1;

        return $$"""
            // GENERIERT von FirmwareBuilder.Common/ModbusRegisterMapBuildService.cs aus register-map.json -- nicht von Hand
            // editieren. Aenderungen an der Register-Map gehoeren in register-map.json, anschliessend die
            // Phase "ReadModbusRegisterMapAndGenerateFiles" erneut ausfuehren.

            export type RegisterBank = "input" | "holding";
            export type RegisterDisplay = "decimal" | "hex" | "binary" | "bool" | "bool-error" | "status" | "range";

            export interface RegisterDef {
                name: string;
                address: number;
                bank: RegisterBank;
                display: RegisterDisplay;
                description?: string;
                unit?: string;
                signed?: boolean;
                gpio?: string;
                i2c?: { bus: string; irqPin?: string };
                min?: number;
                max?: number;
                combine?: { count: number };
            }

            export interface RegisterRegion {
                title: string;
                registers: RegisterDef[];
            }

            export const REGIONS: RegisterRegion[] = [
            {{regionsSource}}
            ];

            export const INPUT_REGISTER_COUNT = {{inputCount}};
            export const HOLDING_REGISTER_COUNT = {{holdingCount}};

            """;
    }
}
