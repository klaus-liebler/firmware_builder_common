using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common;

public sealed class PackageJson
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "0.0.1";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("license")]
    public string? License { get; init; }

    [JsonPropertyName("dependencies")]
    public Dictionary<string, string>? Dependencies { get; init; }
}

public static class NpmProject
{
    public static void CreateAndInstallLazily(string projectRoot, PackageJson packageJson)
    {
        var packageJsonPath = Path.Combine(projectRoot, "package.json");
        var packageJsonContent = JsonSerializer.Serialize(packageJson);
        var needsInstall = false;

        var existedBefore = File.Exists(packageJsonPath);
        if (!existedBefore || File.ReadAllText(packageJsonPath) != packageJsonContent)
        {
            Directory.CreateDirectory(projectRoot);
            File.WriteAllText(packageJsonPath, packageJsonContent);
            Console.WriteLine($"package.json in {projectRoot} neu geschrieben ({(existedBefore ? "geaendert" : "existierte nicht")}) -> npm install");
            needsInstall = true;
        }

        var nodeModulesPath = Path.Combine(projectRoot, "node_modules");
        if (packageJson.Dependencies is not null && !Directory.Exists(nodeModulesPath))
        {
            Console.WriteLine($"{nodeModulesPath} existiert nicht -> npm install");
            needsInstall = true;
        }

        if (!needsInstall)
        {
            return;
        }

        var processStartInfo = new ProcessStartInfo("npm.cmd", "install")
        {
            WorkingDirectory = projectRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        Console.WriteLine($"Fuehre \"npm install\" in {projectRoot} aus...");
        using var process = Process.Start(processStartInfo)!;
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (standardOutput.Length > 0)
        {
            Console.WriteLine(standardOutput);
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"npm install in {projectRoot} fehlgeschlagen (Exit {process.ExitCode}):\n{standardError}");
        }
    }
}
