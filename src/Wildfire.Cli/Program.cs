using Wildfire.Cli;
using Wildfire.Core;

try
{
    CliOptions options = CliOptions.Parse(args);
    if (options.ShowHelp)
    {
        Console.WriteLine(CliOptions.HelpText());
        return;
    }

    if (options.ListScenarios)
    {
        Console.WriteLine(string.Join(Environment.NewLine, ScenarioCatalog.Names));
        return;
    }

    Scenario scenario = ScenarioCatalog.Build(options);
    int layer = Math.Clamp(options.Layer, 0, scenario.Grid.Depth - 1);

    if (options.ExportFixturePath is not null)
    {
        FixtureExporter.Write(options.ExportFixturePath, scenario, layer);
        Console.WriteLine($"Exported fixture to {options.ExportFixturePath}");
        return;
    }

    Console.CursorVisible = false;
    Console.Clear();

    try
    {
        Render(scenario.Cells, scenario, layer);
    }
    finally
    {
        Console.ResetColor();
        Console.CursorVisible = true;
    }
}
catch (Exception ex) when (ex is ArgumentException or OverflowException)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static void Render(ReadOnlySpan<ushort> cells, Scenario scenario, int layer)
{
    Console.SetCursorPosition(0, 0);
    Console.ResetColor();
    Console.WriteLine($"Wildfire scenario preview  scenario={scenario.Name}  seed={scenario.Seed}  {scenario.Grid.Width}x{scenario.Grid.Height}x{scenario.Grid.Depth}  layer={layer}");

    int offset = layer * scenario.Grid.Width * scenario.Grid.Height;
    for (int y = 0; y < scenario.Grid.Height; y += 1)
    {
        for (int x = 0; x < scenario.Grid.Width; x += 1)
        {
            ushort cell = cells[offset + x + (y * scenario.Grid.Width)];
            Console.ForegroundColor = ColorFor(cell);
            Console.Write(CharFor(cell));
        }

        Console.WriteLine();
    }
}

static char CharFor(ushort cell)
{
    if (PackedCell.Water(cell) > 0)
    {
        return '~';
    }

    if (PackedCell.IsBurning(cell))
    {
        return PackedCell.Heat(cell) > 13 ? '@' : '*';
    }

    if (PackedCell.Fuel(cell) == 0)
    {
        return PackedCell.Heat(cell) > 0 ? ':' : '.';
    }

    return PackedCell.Heat(cell) switch
    {
        >= 10 => '+',
        >= 4 => '-',
        _ => '"'
    };
}

static ConsoleColor ColorFor(ushort cell)
{
    if (PackedCell.Water(cell) > 0)
    {
        return ConsoleColor.Cyan;
    }

    if (PackedCell.IsBurning(cell))
    {
        return ConsoleColor.Red;
    }

    return PackedCell.Heat(cell) switch
    {
        >= 10 => ConsoleColor.Yellow,
        >= 4 => ConsoleColor.DarkYellow,
        _ => ConsoleColor.DarkGray
    };
}
