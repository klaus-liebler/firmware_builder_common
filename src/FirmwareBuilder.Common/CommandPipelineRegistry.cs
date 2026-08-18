namespace FirmwareBuilder.Common;

public sealed class CommandPipelineRegistry
{
    private readonly Dictionary<string, Action> _commands;
    private readonly Dictionary<string, IReadOnlyList<string>> _pipelines;

    public CommandPipelineRegistry(StringComparer? comparer = null)
    {
        _commands = new Dictionary<string, Action>(comparer ?? StringComparer.Ordinal);
        _pipelines = new Dictionary<string, IReadOnlyList<string>>(comparer ?? StringComparer.Ordinal);
    }

    public void AddCommand(string name, Action action)
    {
        _commands[name] = action;
    }

    public void AddPipeline(string name, IReadOnlyList<string> steps)
    {
        _pipelines[name] = steps;
    }

    public void Run(string name)
    {
        Execute(name, new HashSet<string>(_commands.Comparer));
    }

    private void Execute(string name, HashSet<string> stack)
    {
        if (_commands.TryGetValue(name, out var command))
        {
            command();
            return;
        }

        if (!_pipelines.TryGetValue(name, out var steps))
        {
            throw new ArgumentException($"Unbekannte Phase/Pipeline \"{name}\".");
        }

        if (!stack.Add(name))
        {
            throw new InvalidOperationException($"Zyklische Pipeline erkannt bei \"{name}\".");
        }

        foreach (var step in steps)
        {
            Execute(step, stack);
        }

        stack.Remove(name);
    }
}
