using Timberborn.WindSystem;
using UnityEngine;
using Wildfire.Core;

namespace Wildfire.Timberborn;

public interface ITimberbornWindProvider
{
    FireSimWind CurrentWind { get; }
}

public sealed class TimberbornWindProvider : ITimberbornWindProvider
{
    private readonly WindService _windService;

    public TimberbornWindProvider(WindService windService)
    {
        _windService = windService ?? throw new ArgumentNullException(nameof(windService));
    }

    public FireSimWind CurrentWind
    {
        get
        {
            Vector2 direction = _windService.WindDirection;
            // Timberborn applies WindDirection.y as negative world Z for wind particles.
            // FireSimWind.DirectionY is Timberborn/grid north, which maps to positive world Z.
            return new FireSimWind(direction.x, -direction.y, _windService.WindStrength).Normalized();
        }
    }
}

public sealed class NullTimberbornWindProvider : ITimberbornWindProvider
{
    public static readonly NullTimberbornWindProvider Instance = new();

    private NullTimberbornWindProvider()
    {
    }

    public FireSimWind CurrentWind => FireSimWind.None;
}
