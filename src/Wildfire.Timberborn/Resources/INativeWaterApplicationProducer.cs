using Wildfire.Core;

namespace Wildfire.Timberborn.Resources;

/// <summary>The native responder retains its intent and cargo until the simulator accepts this application.</summary>
internal interface INativeWaterApplicationProducer
{
    bool TryPrepareApplication(out FireSimChange input, out Action commit);
}
