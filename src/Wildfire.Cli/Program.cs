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
    CpuFireSimulator simulator = new(
        scenario.Grid.Width,
        scenario.Grid.Height,
        scenario.Grid.Depth,
        scenario.Seed,
        scenario.Cells);

    int layer = Math.Clamp(options.Layer, 0, scenario.Grid.Depth - 1);
    bool paused = false;
    bool interactive = options.Ticks is null;

    Console.CursorVisible = false;
    Console.Clear();

    try
    {
        if (interactive)
        {
            RunInteractive(simulator, scenario, layer, options.DelayMilliseconds, paused);
        }
        else if (options.Ticks is int ticks)
        {
            RunFixedTicks(simulator, scenario, layer, ticks, options.DelayMilliseconds);
        }
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

static void RunInteractive(CpuFireSimulator simulator, Scenario scenario, int layer, int delayMilliseconds, bool paused)
{
    while (true)
    {
        while (Console.KeyAvailable)
        {
            ConsoleKey key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Q)
            {
                return;
            }

            if (key == ConsoleKey.Spacebar)
            {
                paused = !paused;
            }

            if (key == ConsoleKey.UpArrow)
            {
                layer = Math.Min(scenario.Grid.Depth - 1, layer + 1);
            }

            if (key == ConsoleKey.DownArrow)
            {
                layer = Math.Max(0, layer - 1);
            }
        }

        FireStepResult result = paused ? new FireStepResult([], 0) : simulator.Tick();
        Render(simulator.Cells, scenario, layer, paused, result.Tick);
        Thread.Sleep(delayMilliseconds);
    }
}

static void RunFixedTicks(CpuFireSimulator simulator, Scenario scenario, int layer, int ticks, int delayMilliseconds)
{
    FireStepResult result = new([], 0);
    foreach (int _ in Enumerable.Range(0, Math.Max(0, ticks)))
    {
        result = simulator.Tick();
        Render(simulator.Cells, scenario, layer, paused: false, result.Tick);
        Thread.Sleep(delayMilliseconds);
    }
}

static void Render(ReadOnlySpan<ushort> cells, Scenario scenario, int layer, bool paused, uint tick)
{
    Console.SetCursorPosition(0, 0);
    Console.ResetColor();
    Console.WriteLine($"Wildfire CLI  scenario={scenario.Name}  seed={scenario.Seed}  {scenario.Grid.Width}x{scenario.Grid.Height}x{scenario.Grid.Depth}  layer={layer}  tick={tick}  {(paused ? "paused" : "running")}  [up/down layer] [space pause] [q quit]");

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
