using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FirmwareBuilder.Common;

public static class RuntimeConfigWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static string StringifyValue(object value) => JsonSerializer.Serialize(value, Options);

    public static void CreateCppConfigurationHeader(string outDir, IReadOnlyDictionary<string, object> defines)
    {
        Directory.CreateDirectory(outDir);

        var definesBuilder = new StringBuilder("#pragma once\n");
        foreach (var (key, value) in defines)
        {
            definesBuilder.Append($"#define __{key}__ {StringifyValue(value)}\n");
        }
        File.WriteAllText(Path.Combine(outDir, "runtimeconfig_defines.hh"), definesBuilder.ToString());

        var namespaceBuilder = new StringBuilder("#pragma once\nnamespace cfg{\n");
        foreach (var (key, value) in defines)
        {
            namespaceBuilder.Append($"\tconstexpr auto {key}{{{StringifyValue(value)}}};\n");
        }
        namespaceBuilder.Append("}//namespace\n");
        File.WriteAllText(Path.Combine(outDir, "runtimeconfig.hh"), namespaceBuilder.ToString());
    }

    public static void CreateCMakeJsonConfigFile(string outDir, IReadOnlyDictionary<string, object> defines)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "config.json"), JsonSerializer.Serialize(defines, Options));
    }

    public static void CreateTypeScriptRuntimeConfig(string outDir, IReadOnlyDictionary<string, object> defines)
    {
        Directory.CreateDirectory(outDir);
        var builder = new StringBuilder();
        foreach (var (key, value) in defines)
        {
            builder.Append($"export const {key}={StringifyValue(value)}\n");
        }
        File.WriteAllText(Path.Combine(outDir, "index.ts"), builder.ToString());
        File.WriteAllText(
            Path.Combine(outDir, "package.json"),
            """{"name":"@generated/runtimeconfig_ts","version":"1.0.0","main":"index.ts","license":"No License","description":"Generated during build."}""");
    }
}
