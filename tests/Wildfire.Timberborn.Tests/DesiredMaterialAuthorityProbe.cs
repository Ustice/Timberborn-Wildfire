using Wildfire.Core;

namespace Wildfire.Timberborn.Tests;

// Fault injection around the existing scripted backend; no alternate fire/material implementation.
internal sealed class DesiredMaterialAuthorityProbe(RetiredDetachmentSimulatorFixture inner)
    : IFireSimMaterialHandoffSimulator, IFireSimSnapshotSimulator
{
    internal int Captures;
    internal bool HideArchives, DenyKnown;
    internal FireSimMaterialArchive? SubstituteArchive;
    internal Func<FireSimSnapshot, FireSimSnapshot>? TransformSnapshot;
    public FireSimSnapshotCapability SnapshotCapability { get; set; } = FireSimSnapshotCapability.CompleteMaterialHistory;
    public FireSimSnapshot CaptureSnapshot() { Captures++; var value = inner.CaptureSnapshot(); return TransformSnapshot?.Invoke(value) ?? value; }
    public int Width => inner.Width;
    public int Height => inner.Height;
    public int Depth => inner.Depth;
    public bool IsSlotKnown(FireSimMaterialIdentity identity) => !DenyKnown && inner.IsSlotKnown(identity);
    public bool TryGetMaterialArchive(FireSimMaterialIdentity identity, out FireSimMaterialArchive archive)
    {
        bool available = inner.TryGetMaterialArchive(identity, out archive!);
        if (available && SubstituteArchive is not null) archive = SubstituteArchive;
        return !HideArchives && available;
    }
    public void RegisterChange(FireSimChange change) => inner.RegisterChange(change);
    public GpuFireStepResult Tick() => inner.Tick();
    public IDisposable Subscribe(IFireSimListener listener) => inner.Subscribe(listener);
    public GpuFireStepResult? TryTickWithInput(FireSimChange input, Action commit) => inner.TryTickWithInput(input, commit);
    public GpuFireStepResult? TryHandoffMaterial(FireSimMaterialHandoffBatch batch, Action<FireSimMaterialHandoffReceipt> commit) => inner.TryHandoffMaterial(batch, commit);
}
