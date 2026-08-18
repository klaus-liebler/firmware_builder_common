using System.Diagnostics;

namespace FirmwareBuilder.Common;

public sealed class ProcessException(string message, int exitCode, string stdOut, string stdErr) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
    public string StdOut { get; } = stdOut;
    public string StdErr { get; } = stdErr;
}

public static class ProcessRunner
{
    private static ProcessStartInfo BuildStartInfo(string fileName, IEnumerable<string> arguments, string workingDirectory, bool redirect)
    {
        var psi = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = redirect,
            RedirectStandardError = redirect,
            UseShellExecute = false,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }
        return psi;
    }

    private static Process StartOrThrow(ProcessStartInfo psi)
    {
        try
        {
            return Process.Start(psi)!;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 2)
        {
            throw new ProcessException($"Datei nicht gefunden: {psi.FileName}", -1, "", "");
        }
    }

    public static string Run(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var psi = BuildStartInfo(fileName, arguments, workingDirectory, redirect: true);
        using var process = StartOrThrow(psi);
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(stdoutTask, stderrTask);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ProcessException(
                $"{fileName} beendet mit Exit-Code {process.ExitCode}.\n{(stderrTask.Result.Length > 0 ? stderrTask.Result : stdoutTask.Result)}",
                process.ExitCode,
                stdoutTask.Result,
                stderrTask.Result);
        }
        return stdoutTask.Result;
    }

    public static byte[] RunBinary(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var psi = BuildStartInfo(fileName, arguments, workingDirectory, redirect: true);
        using var process = StartOrThrow(psi);
        using var stdout = new MemoryStream();
        var copyTask = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var stderrTask = process.StandardError.ReadToEndAsync();
        Task.WaitAll(copyTask, stderrTask);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ProcessException($"{fileName} beendet mit Exit-Code {process.ExitCode}.\n{stderrTask.Result}", process.ExitCode, "", stderrTask.Result);
        }
        return stdout.ToArray();
    }

    public static void RunInherit(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        var psi = BuildStartInfo(fileName, arguments, workingDirectory, redirect: false);
        using var process = StartOrThrow(psi);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ProcessException($"{fileName} beendet mit Exit-Code {process.ExitCode}.", process.ExitCode, "", "");
        }
    }

    public static void RunInheritShellCommand(string command, string workingDirectory)
    {
        var psi = new ProcessStartInfo("cmd.exe")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = false,
            Arguments = $"/c \"{command}\"",
        };
        using var process = StartOrThrow(psi);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new ProcessException($"cmd.exe beendet mit Exit-Code {process.ExitCode}.", process.ExitCode, "", "");
        }
    }
}
