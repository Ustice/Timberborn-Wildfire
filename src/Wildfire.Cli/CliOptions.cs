namespace Wildfire.Cli;

public sealed record CliOptions(
    string Scenario,
    uint Seed,
    int? Width,
    int? Height,
    int? Depth,
    int Layer,
    bool ListScenarios,
    bool ShowHelp)
{
    public static CliOptions Parse(string[] args)
    {
        CliOptions options = new(
            Scenario: ScenarioCatalog.DefaultScenarioName,
            Seed: 1,
            Width: null,
            Height: null,
            Depth: null,
            Layer: 0,
            ListScenarios: false,
            ShowHelp: false);

        int positionalIndex = 0;
        foreach (string arg in args)
        {
            options = ParseArg(options, arg, positionalIndex);
            positionalIndex += arg.StartsWith("--", StringComparison.Ordinal) ? 0 : 1;
        }

        return options;
    }

    public static string HelpText()
    {
        string scenarios = string.Join(", ", ScenarioCatalog.Names);
        return $"""
            Wildfire CLI

            Usage:
              dotnet run --project src/Wildfire.Cli -- [options]
              dotnet run --project src/Wildfire.Cli -- <width> <height> <depth> <seed>

            Options:
              --scenario=<name>     Scenario name. Known: {scenarios}
              --seed=<uint>         Deterministic seed for scenario layout.
              --width=<int>         Override scenario width.
              --height=<int>        Override scenario height.
              --depth=<int>         Override scenario depth.
              --layer=<int>         Initial layer to render.
              --list-scenarios      Print scenario names and exit.
              --help                Print this help and exit.
            """;
    }

    private static CliOptions ParseArg(CliOptions options, string arg, int positionalIndex)
    {
        return arg switch
        {
            "--help" or "-h" => options with { ShowHelp = true },
            "--list-scenarios" => options with { ListScenarios = true },
            _ when ReadOption(arg, "--scenario=", value => options with { Scenario = value }) is { } next => next,
            _ when ReadUIntOption(arg, "--seed=", value => options with { Seed = value }) is { } next => next,
            _ when ReadIntOption(arg, "--width=", value => options with { Width = value }) is { } next => next,
            _ when ReadIntOption(arg, "--height=", value => options with { Height = value }) is { } next => next,
            _ when ReadIntOption(arg, "--depth=", value => options with { Depth = value }) is { } next => next,
            _ when ReadIntOption(arg, "--layer=", value => options with { Layer = value }) is { } next => next,
            _ when !arg.StartsWith("--", StringComparison.Ordinal) => ParsePositional(options, arg, positionalIndex),
            _ => throw new ArgumentException($"Unknown option '{arg}'. Use --help for supported options.")
        };
    }

    private static CliOptions ParsePositional(CliOptions options, string arg, int positionalIndex)
    {
        if (!int.TryParse(arg, out int value))
        {
            throw new ArgumentException($"Expected numeric positional argument at position {positionalIndex + 1}, got '{arg}'.");
        }

        return positionalIndex switch
        {
            0 => options with { Width = value },
            1 => options with { Height = value },
            2 => options with { Depth = value },
            3 => options with { Seed = checked((uint)value) },
            _ => throw new ArgumentException($"Unexpected positional argument '{arg}'. Use --help for supported options.")
        };
    }

    private static CliOptions? ReadOption(string arg, string prefix, Func<string, CliOptions> parse)
    {
        return arg.StartsWith(prefix, StringComparison.Ordinal)
            ? parse(arg[prefix.Length..])
            : null;
    }

    private static CliOptions? ReadIntOption(string arg, string prefix, Func<int, CliOptions> parse)
    {
        return ReadOption(arg, prefix, value =>
        {
            if (!int.TryParse(value, out int parsed))
            {
                throw new ArgumentException($"Expected integer value for {prefix[..^1]}, got '{value}'.");
            }

            return parse(parsed);
        });
    }

    private static CliOptions? ReadUIntOption(string arg, string prefix, Func<uint, CliOptions> parse)
    {
        return ReadOption(arg, prefix, value =>
        {
            if (!uint.TryParse(value, out uint parsed))
            {
                throw new ArgumentException($"Expected unsigned integer value for {prefix[..^1]}, got '{value}'.");
            }

            return parse(parsed);
        });
    }
}
