using Wildfire.Core;

namespace Wildfire.Timberborn.Mapping;

/// <summary>Copied internal policy choices for a genuinely new fire world, with no identities or historical state.</summary>
internal sealed class TimberbornInitialOwnedPlan
{
    internal TimberbornInitialOwnedPlan(IEnumerable<TimberbornInitialBodySelection> bodies, FireSimParameters parameters, uint seed)
    {
        var choices = bodies?.ToArray() ?? throw new ArgumentNullException(nameof(bodies));
        if (choices.Any(body => body is null) || choices.Select(body => body.EntityId).Distinct().Count() != choices.Length)
            throw new ArgumentException("Initial plan must contain unique explicit body selections.");
        Bodies = Array.AsReadOnly(choices); Parameters = parameters; Seed = seed;
    }
    internal IReadOnlyList<TimberbornInitialBodySelection> Bodies { get; }
    internal FireSimParameters Parameters { get; }
    internal uint Seed { get; }
}
