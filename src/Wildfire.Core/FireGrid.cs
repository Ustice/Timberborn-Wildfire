namespace Wildfire.Core;

public readonly record struct FireGrid(int Width, int Height, int Depth)
{
    public int CellCount => Width * Height * Depth;

    public int ToIndex(int x, int y, int z)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height || (uint)z >= (uint)Depth)
        {
            throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be inside the fire grid.");
        }

        return x + (y * Width) + (z * Width * Height);
    }

    public (int X, int Y, int Z) FromIndex(int index)
    {
        if ((uint)index >= (uint)CellCount)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "Index must be inside the fire grid.");
        }

        int layerSize = Width * Height;
        int z = index / layerSize;
        int remainder = index % layerSize;
        int y = remainder / Width;
        int x = remainder % Width;
        return (x, y, z);
    }
}
