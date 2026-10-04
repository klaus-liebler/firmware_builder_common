namespace FirmwareBuilder.Common.Stm32;

// Einziger sanktionierter Einstiegspunkt fuer STM32-Konsumenten am Build-Anfang (s. Program.cs
// jedes Konsumenten). Erzwingt ueber den generischen Constraint, dass der projekteigene Kontext-Typ
// tatsaechlich auf der geteilten Stm32BuildContextBase aufbaut (statt IBuildContextStm32 komplett
// neu/eigenstaendig zu implementieren) -- factory bleibt trotzdem projekteigen, weil nur der
// Konsument weiss, welche konkrete Klasse (mit ihren eigenen Pfad-Konventionen) instanziiert wird.
public static class Stm32Toolchain
{
    public static TCtx CreateBuildContext<TCtx>(string[] args, Func<string[], TCtx> factory)
        where TCtx : Stm32BuildContextBase
        => factory(args);
}
