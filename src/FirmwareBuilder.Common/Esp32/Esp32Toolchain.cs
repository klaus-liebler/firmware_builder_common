namespace FirmwareBuilder.Common.Esp32;

// Einziger sanktionierter Einstiegspunkt fuer ESP32-Konsumenten am Build-Anfang (s. Program.cs
// jedes Konsumenten) -- Pendant zu Stm32Toolchain.CreateBuildContext. Erzwingt ueber den
// generischen Constraint, dass der projekteigene Kontext-Typ tatsaechlich auf der geteilten
// Esp32BuildContextBase aufbaut.
public static class Esp32Toolchain
{
    public static TCtx CreateBuildContext<TCtx>(string[] args, Func<string[], TCtx> factory)
        where TCtx : Esp32BuildContextBase
        => factory(args);
}
