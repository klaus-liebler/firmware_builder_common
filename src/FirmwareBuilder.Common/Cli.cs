namespace FirmwareBuilder.Common;

// Kleine, wiederverwendbare argv-Hilfsfunktionen -- zuvor in beiden Projekten unabhaengig als
// private Methoden in Program.cs dupliziert. Jeder [BuildStep], der ein eigenes Flag braucht, liest
// es sich selbst aus ctx.Args (s. IBuildContext.Args) mit diesen Hilfsfunktionen.
public static class Cli
{
    public static string GetRequiredArgValue(IReadOnlyList<string> args, string flag)
    {
        return GetOptionalArgValue(args, flag)
            ?? throw new ArgumentException($"Fehlendes Argument \"{flag} <Wert>\".");
    }

    public static string? GetOptionalArgValue(IReadOnlyList<string> args, string flag)
    {
        var i = IndexOf(args, flag);
        if (i < 0 || i + 1 >= args.Count)
        {
            return null;
        }
        return args[i + 1];
    }

    public static bool HasFlag(IReadOnlyList<string> args, string flag) => IndexOf(args, flag) >= 0;

    // Fuer wiederholbare Flags (z.B. --best-binary-buffer-schema-path A --best-binary-buffer-schema-path B).
    public static IReadOnlyList<string> GetAllArgValues(IReadOnlyList<string> args, string flag)
    {
        var values = new List<string>();
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == flag)
            {
                values.Add(args[i + 1]);
            }
        }
        return values;
    }

    private static int IndexOf(IReadOnlyList<string> args, string value)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == value)
            {
                return i;
            }
        }
        return -1;
    }
}
