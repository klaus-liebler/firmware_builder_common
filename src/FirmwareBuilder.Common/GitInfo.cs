using System.Diagnostics;
using System.Text.Json.Serialization;

namespace FirmwareBuilder.Common;

public sealed class GitInfo
{
    [JsonPropertyName("commitHash")]
    public required string CommitHash { get; init; }

    [JsonPropertyName("branch")]
    public required string Branch { get; init; }

    [JsonPropertyName("tag")]
    public required string Tag { get; init; }

    [JsonPropertyName("commitDateEpoch")]
    public required long CommitDateEpoch { get; init; }

    [JsonPropertyName("commitAuthor")]
    public required string CommitAuthor { get; init; }

    [JsonPropertyName("commitMessage")]
    public required string CommitMessage { get; init; }

    [JsonPropertyName("isDirty")]
    public required bool IsDirty { get; init; }

    [JsonPropertyName("buildTimestampEpoch")]
    public required long BuildTimestampEpoch { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }
}

public static class GitInfoReader
{
    private static string Git(string rootDir, string arguments, string fallback = "unknown")
    {
        try
        {
            var psi = new ProcessStartInfo("git", arguments)
            {
                WorkingDirectory = rootDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            var trimmed = output.Trim();
            return process.ExitCode == 0 && trimmed.Length > 0 ? trimmed : fallback;
        }
        catch
        {
            return fallback;
        }
    }

    public static GitInfo ReadGitInfo(string rootDir)
    {
        var commitHash = Git(rootDir, "rev-parse --short HEAD");
        var branch = Git(rootDir, "rev-parse --abbrev-ref HEAD");
        var tag = Git(rootDir, "describe --tags --always");
        var commitDateEpoch = long.Parse(Git(rootDir, "log -1 --format=%at", "0"));
        var commitAuthor = Git(rootDir, "log -1 --format=%an");
        var commitMessage = Git(rootDir, "log -1 --format=%s");
        var isDirty = Git(rootDir, "status --porcelain", "") != "";
        var buildTimestampEpoch = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return new GitInfo
        {
            CommitHash = commitHash,
            Branch = branch,
            Tag = tag,
            CommitDateEpoch = commitDateEpoch,
            CommitAuthor = commitAuthor,
            CommitMessage = commitMessage,
            IsDirty = isDirty,
            BuildTimestampEpoch = buildTimestampEpoch,
            Version = $"{tag}-{commitHash}",
        };
    }
}
