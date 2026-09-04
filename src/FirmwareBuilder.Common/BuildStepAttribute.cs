namespace FirmwareBuilder.Common;

// Markiert eine Methode als per CLI (ueber ihren Methodennamen) aufrufbaren Build-Schritt, s.
// BuildStepRunner. Ersetzt die frueheren String-Konstanten-Registrierungen
// (CommandPipelineRegistry.AddCommand("Name", ...)) durch reflektiv entdeckte, namensgleiche
// Methoden -- ein Rename der Methode benennt automatisch auch den CLI-Befehl um.
[AttributeUsage(AttributeTargets.Method)]
public sealed class BuildStepAttribute : Attribute
{
}
