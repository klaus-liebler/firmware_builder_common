namespace TerminalSchema;

// The attributes of a terminal schema (C# files, see TerminalCatalog). TerminalCatalog recognizes them purely by NAME (syntax-only
// Roslyn parsing, like UniversalRegisterAccess and BestBinaryBuffers); these declarations only document
// them and give IntelliSense while authoring.

/// <summary>A command of the Terminal page: the class name is the command, its fields are the parameters
/// in order, string? an optional one. Client: runs in the browser (help, clear), no firmware handler.</summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TerminalCommandAttribute : Attribute
{
	public bool Client { get; init; }
}

/// <summary>The enum of the live value lookups (terminal_catalog::Lookup, in declaration order).</summary>
[AttributeUsage(AttributeTargets.Enum)]
public sealed class TerminalLookupsAttribute : Attribute;

/// <summary>One listed value of a parameter; repeat it for each value.</summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public sealed class ValueAttribute(string value, string doc = "") : Attribute
{
	public string Value { get; } = value;
	public string Doc { get; } = doc;
}

/// <summary>The parameter's values are fetched live from the device: a member of the [TerminalLookups] enum.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class LookupAttribute(object lookup) : Attribute
{
	public object Lookup { get; } = lookup;
}

/// <summary>Values computed in the browser.</summary>
public enum Source { commands, registers }

/// <summary>The parameter's values are computed in the browser: command names or register addresses.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SourceAttribute(Source source) : Attribute
{
	public Source Source { get; } = source;
}
