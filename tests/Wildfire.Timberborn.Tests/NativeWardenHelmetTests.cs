using System.IO.Compression;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Nodes;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeWardenHelmetTests
{
    private const string Id = "Wildfire.WardenHelmet.IronTeeth";

    [Fact]
    public void ActualNativeBlueprintMergePreservesEveryAdultAttachmentAndAddsOneLazyHelmet()
    {
        using var native = new NativeManagedTestContext();
        using var zip = ZipFile.OpenRead(Path.Combine(native.ManagedPath, "../StreamingAssets/Modding/Blueprints.zip"));
        using var reader = new StreamReader(zip.GetEntry("Characters/Beaver/BeaverAdult.blueprint.json")!.Open());
        var original = reader.ReadToEnd();
        var patch = File.ReadAllText(Source("src/Wildfire.Timberborn/Data/Characters/Beaver/BeaverAdult.blueprint.json"));
        var jobject = native.LoadNative("Newtonsoft.Json").GetType("Newtonsoft.Json.Linq.JObject")!;
        var parse = jobject.GetMethod("Parse", new[] { typeof(string) })!;
        var inputs = Array.CreateInstance(jobject, 2);
        inputs.SetValue(parse.Invoke(null, new object[] { original }), 0);
        inputs.SetValue(parse.Invoke(null, new object[] { patch }), 1);
        var mergerType = native.LoadNative("Timberborn.SerializationSystem").GetType("Timberborn.SerializationSystem.JsonMerger")!;
        var merged = mergerType.GetMethod("Merge")!.Invoke(Activator.CreateInstance(mergerType), new object[] { inputs })!;
        var result = JsonNode.Parse(merged.ToString()!)!;
        var rows = result["TemplateAttachmentsSpec"]!["Attachments"]!.AsArray();
        var before = JsonNode.Parse(original)!;
        var originals = before["TemplateAttachmentsSpec"]!["Attachments"]!.AsArray();
        Assert.Equal(originals.Count + 1, rows.Count);
        foreach (var row in originals)
            Assert.True(JsonNode.DeepEquals(row, rows.Single(actual => actual!["AttachmentId"]!.GetValue<string>() == row!["AttachmentId"]!.GetValue<string>())));
        var helmet = Assert.Single(rows, row => row!["AttachmentId"]!.GetValue<string>() == Id)!;
        Assert.Equal("#Head", helmet["Parent"]!.GetValue<string>());
        Assert.Equal("Wildfire/Characters/Attachments/WardenHelmet.IronTeeth", helmet["Prefab"]!.GetValue<string>());
        Assert.False(helmet["CreateInstantly"]!.GetValue<bool>());
        // Outfit/carry/animation declarations must not change as a side effect of the patch.
        foreach (var pair in before.AsObject().Where(pair => pair.Key != "TemplateAttachmentsSpec"))
            Assert.True(JsonNode.DeepEquals(pair.Value, result[pair.Key]));
    }

    [Fact]
    public void NativeToggleIsLazyAndTracksOwnedPhaseInsteadOfWaterOrSimulationTime()
    {
        using var f = new Fixture();
        f.Observe(false, "Fetching", true);
        f.Observe(true, "Idle", true);
        f.Observe(true, "Approaching", false);
        Assert.Equal(0, f.Created);
        foreach (var phase in new[] { "Fetching", "Approaching", "Applying", "AwaitingApplication", "Returning" })
            f.Observe(true, phase, true);
        Assert.Equal(1, f.Created);
        Assert.True(f.Visible);
        Assert.Equal(1, f.Events);
        f.Observe(false, "Returning", true); // Native manager replaced executor; old phase never finishes.
        Assert.False(f.Visible);
        f.Observe(true, "Approaching", true); // Also represents paused, fully loaded ownership, with no elapsed-time input.
        Assert.True(f.Visible);
        Assert.Equal(1, f.Created);
        f.Observe(true, "Returning", false); // Death hides even if phase and executor are stale.
        Assert.False(f.Visible);
        f.Observe(false, "Idle", false); // Repeated death/delete cleanup is idempotent.
        Assert.Equal(4, f.Events);
    }

    [Fact]
    public void CreationFailureIsReportedOnceWithoutRepeatedNativeImportOrEscapingThePresentationSeam()
    {
        using var f = new Fixture();
        f.CreationFailure = new InvalidOperationException("Missing #Head");
        f.Observe(true, "Approaching", true);
        f.Observe(true, "Applying", true);
        f.Observe(false, "Idle", false);
        Assert.Equal(1, f.Created);
        Assert.Same(f.CreationFailure, Assert.Single(f.Errors));
    }

    [Fact]
    public void NativeVisibilityCallbackFailureHidesAlreadyChangedToggleAndDisablesFurtherPresentationWrites()
    {
        using var f = new Fixture();
        f.ThrowOnShow = true;
        f.Observe(true, "Applying", true);
        Assert.False(f.Visible); // Show assigned true before its subscriber threw; cleanup uses actual Hide.
        Assert.Single(f.Errors);
        int events = f.Events;
        f.Observe(true, "Returning", true);
        Assert.Equal(events, f.Events);
        Assert.Equal(1, f.Created);
    }

    private static string Source(string path)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Wildfire.slnx"))) root = root.Parent;
        return Path.Combine(root!.FullName, path);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly NativeManagedTestContext _native = new();
        private readonly Type _toggleType;
        private readonly object _state;
        private readonly Type _phase;
        private object? _toggle;
        internal int Created;
        internal int Events;
        internal bool ThrowOnShow;
        internal Exception? CreationFailure;
        internal readonly List<Exception> Errors = new();
        internal bool Visible => _toggle is not null && (bool)_toggleType.GetProperty("IsVisible")!.GetValue(_toggle)!;

        internal Fixture()
        {
            _toggleType = _native.LoadNative("Timberborn.TemplateAttachmentSystem").GetType("Timberborn.TemplateAttachmentSystem.TemplateAttachmentVisibilityToggle")!;
            var mod = _native.LoadMod();
            _phase = mod.GetType("Wildfire.Timberborn.FireResponse.WardenPhase")!;
            var stateType = mod.GetType("Wildfire.Timberborn.FireResponse.Presentation.WardenHelmetVisibility")!;
            var factory = Expression.Lambda(typeof(Func<>).MakeGenericType(_toggleType),
                Expression.Convert(Expression.Invoke(Expression.Constant((Func<object>)Create)), _toggleType)).Compile();
            _state = Activator.CreateInstance(stateType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { factory, (Action<Exception>)Errors.Add }, null)!;
        }

        private object Create()
        {
            Created++;
            if (CreationFailure is not null) throw CreationFailure;
            _toggle = Activator.CreateInstance(_toggleType)!;
            _toggleType.GetEvent("VisibilityChanged")!.AddEventHandler(_toggle, (EventHandler)((_, _) =>
            {
                Events++;
                if (ThrowOnShow && Visible) throw new InvalidOperationException("Native renderer callback failed");
            }));
            return _toggle;
        }

        internal void Observe(bool owner, string phase, bool alive) =>
            _state.GetType().GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(_state, new[] { (object)owner, Enum.Parse(_phase, phase), alive });
        public void Dispose() => _native.Dispose();
    }
}
