using Wildfire.Core;

int width = ParseArg(args, 0, 32);
int height = ParseArg(args, 1, 16);
int depth = ParseArg(args, 2, 4);
uint seed = (uint)ParseArg(args, 3, 1);

ushort[] cells = new ushort[width * height * depth];
int center = (width / 2) + ((height / 2) * width);

for (int index = 0; index < cells.Length; index += 1)
{
    cells[index] = PackedCell.Pack(fuel: 8, heat: 0, flammability: 2, water: 0, terrain: 1, heatLoss: 2);
}

CpuFireSimulator simulator = new(width, height, depth, seed, cells);
simulator.RegisterChange(new FireSimChange(center, AddHeat: 15));

Console.CursorVisible = false;
Console.Clear();

int layer = 0;
bool paused = false;
while (true)
{
    while (Console.KeyAvailable)
    {
        ConsoleKey key = Console.ReadKey(intercept: true).Key;
        if (key == ConsoleKey.Q)
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            return;
        }

        if (key == ConsoleKey.Spacebar)
        {
            paused = !paused;
        }

        if (key == ConsoleKey.UpArrow)
        {
            layer = Math.Min(depth - 1, layer + 1);
        }

        if (key == ConsoleKey.DownArrow)
        {
            layer = Math.Max(0, layer - 1);
        }
    }

    FireStepResult result = paused ? new FireStepResult([], 0) : simulator.Tick();
    Render(simulator.Cells, width, height, depth, layer, paused, result.Tick);
    Thread.Sleep(100);
}

static int ParseArg(string[] args, int index, int fallback)
{
    return args.Length > index && int.TryParse(args[index], out int value) ? value : fallback;
}

static void Render(ReadOnlySpan<ushort> cells, int width, int height, int depth, int layer, bool paused, uint tick)
{
    Console.SetCursorPosition(0, 0);
    Console.ResetColor();
    Console.WriteLine($"Wildfire CLI  {width}x{height}x{depth}  layer={layer}  tick={tick}  {(paused ? "paused" : "running")}  [up/down layer] [space pause] [q quit]");

    int offset = layer * width * height;
    for (int y = 0; y < height; y += 1)
    {
        for (int x = 0; x < width; x += 1)
        {
            ushort cell = cells[offset + x + (y * width)];
            Console.ForegroundColor = ColorFor(cell);
            Console.Write(CharFor(cell));
        }

        Console.WriteLine();
    }

    Console.ResetColor();
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
