namespace Wildfire.Timberborn.Resources;

/// <summary>Physical arrival is retained by the responder; collection runs after settled native water credit.</summary>
internal interface INativeNaturalWaterCollector
{
    void TryCollectPendingWater();
}
