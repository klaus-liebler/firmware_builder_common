using System.Diagnostics;
using System.Reflection;

namespace FirmwareBuilder.Common;

// Ersetzt CommandPipelineRegistry vollstaendig. Ein "Build-Schritt" ist eine mit [BuildStep]
// markierte, oeffentliche statische Methode mit genau einem Parameter (dem Build-Kontext).
// "Pipelines" sind keine eigene registrierte Datenstruktur mehr, sondern ganz gewoehnliche
// [BuildStep]-Methoden, deren Body per Invoke() andere [BuildStep]-Methoden seriell aufruft
// (Gulp-"series()"-Prinzip) -- jeder Aufruf (egal ob Top-Level von der CLI oder aus einer Pipeline
// heraus) bekommt dieselbe farbige Start/Erfolg/Fehler-Konsolenausgabe samt Zeitmessung.
public static class BuildStepRunner
{
    public static IReadOnlyDictionary<string, MethodInfo> DiscoverSteps(params Type[] stepContainers)
    {
        var steps = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        foreach (var type in stepContainers)
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (method.GetCustomAttribute<BuildStepAttribute>() is null)
                {
                    continue;
                }
                if (method.GetParameters().Length != 1)
                {
                    throw new InvalidOperationException(
                        $"[BuildStep] {type.FullName}.{method.Name} muss genau einen Parameter (den Build-Kontext) haben.");
                }
                if (!steps.TryAdd(method.Name, method))
                {
                    throw new InvalidOperationException(
                        $"Zwei [BuildStep]-Methoden heissen beide \"{method.Name}\" -- Methodennamen muessen ueber alle stepContainers hinweg eindeutig sein.");
                }
            }
        }
        return steps;
    }

    // Alleiniger CLI-Einstiegspunkt fuer Main() in JEDEM Konsumenten (sensact_firmware,
    // firmware_factory_control_unit, ...) -- Main() selbst schrumpft dadurch auf einen Einzeiler.
    // Uebernimmt hier zentral: "kein Schrittname angegeben"/"unbekannter Schritt"-Pruefung (inkl.
    // Liste der bekannten Schritte in beiden Fehlermeldungen), Konstruktion des projektspezifischen
    // Kontexts ERST NACHDEM der Schrittname als vorhanden bekannt ist (ctxFactory bekommt args[1..]),
    // sowie ein einheitliches Exception->Konsolenausgabe-Verhalten (vorher: STM32 fing Fehler
    // freundlich ab, sensact liess den rohen .NET-Stacktrace durch -- jetzt fuer beide gleich).
    public static void Run<TCtx>(string[] args, Func<string[], TCtx> ctxFactory, params Type[] stepContainers)
    {
        try
        {
            var steps = DiscoverSteps(stepContainers);
            var known = string.Join(", ", steps.Keys.OrderBy(k => k, StringComparer.Ordinal));

            if (args.Length == 0)
            {
                throw new ArgumentException($"Kein Schrittname angegeben. Bekannt: {known}");
            }
            if (!steps.TryGetValue(args[0], out var method))
            {
                throw new ArgumentException($"Unbekannter Build-Schritt \"{args[0]}\". Bekannt: {known}");
            }

            var ctx = ctxFactory(args[1..]);
            InvokeCore(args[0], () =>
            {
                try
                {
                    method.Invoke(null, [ctx]);
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw ex.InnerException;
                }
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"\nFehler: {ex.Message}");
            Environment.Exit(1);
        }
    }

    // In-Prozess-Aufruf eines Schritts durch einen anderen (Pipeline-Bodies). Nimmt die
    // Zielmethode als typisierten Delegaten entgegen (keine String-Konstante) und liest deren
    // Namen fuer die Konsolenausgabe reflektiv aus dem Delegaten.
    public static void Invoke<TCtx>(TCtx ctx, Action<TCtx> step)
    {
        InvokeCore(step.Method.Name, () => step(ctx));
    }

    private static void InvokeCore(string stepName, Action invoke)
    {
        WriteBlocks(ConsoleColor.Blue);
        Console.WriteLine($" Start Step {stepName} at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");

        var sw = Stopwatch.StartNew();
        try
        {
            invoke();
            sw.Stop();
            WriteBlocks(ConsoleColor.Green);
            Console.WriteLine($" Success for step {stepName} at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}, took {FormatDuration(sw.Elapsed)}");
            WriteFullLine(ConsoleColor.Green);
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            sw.Stop();
            WriteBlocks(ConsoleColor.Red);
            Console.WriteLine($" Error in Step {stepName} at {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}, took {FormatDuration(sw.Elapsed)}: {ex.Message}");
            throw;
        }
    }

    private static void WriteBlocks(ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(new string('█', 5));
        Console.ForegroundColor = previous;
        Console.Write(' ');
    }

    // Volle Trennzeile nach einer erfolgreichen Stufe, damit aufeinanderfolgende Pipeline-Schritte
    // (viele kurze Konsolenblöcke) beim Ueberfliegen des Logs klar auseinanderfallen.
    private static void WriteFullLine(ConsoleColor color)
    {
        int width;
        try
        {
            width = Console.IsOutputRedirected ? 80 : Console.WindowWidth;
        }
        catch
        {
            width = 80;
        }
        if (width <= 0)
        {
            width = 80;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(new string('─', width));
        Console.ForegroundColor = previous;
    }

    private static string FormatDuration(TimeSpan elapsed) =>
        elapsed.TotalMilliseconds < 1000
            ? $"{elapsed.TotalMilliseconds:F0}ms"
            : $"{elapsed.TotalSeconds:F0}s";
}
