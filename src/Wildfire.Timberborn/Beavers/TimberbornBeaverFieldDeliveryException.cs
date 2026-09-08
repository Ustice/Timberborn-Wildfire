namespace Wildfire.Timberborn.Beavers;

/// <summary>One actor changed native state without completing its matching behavior history.</summary>
internal sealed class TimberbornBeaverFieldDeliveryException : InvalidOperationException
{
    internal TimberbornBeaverFieldDeliveryException(string beaverId, Exception cause)
        : base($"Smoke behavior delivery is incomplete for {beaverId}.", cause) { }
}
