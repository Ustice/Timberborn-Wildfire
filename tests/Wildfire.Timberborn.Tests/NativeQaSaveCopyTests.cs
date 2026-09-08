using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Wildfire.Timberborn.Tests;

public sealed class NativeQaSaveCopyTests
{
    private static readonly NativeManagedTestContext Native = NativeManagedTestContext.ProxyContracts;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Theory]
    [InlineData("QA-copy_2", true)]
    [InlineData("QA-a", true)]
    [InlineData("QA-", false)]
    [InlineData("original", false)]
    [InlineData("QA-../original", false)]
    [InlineData("QA-name.timber", false)]
    [InlineData("QA-name\\other", false)]
    [InlineData("QA-ä", false)]
    public void SaveNamesAreBoundedBasenames(string name, bool expected) => Assert.Equal(expected,
        CopyType.GetMethod("ValidName", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [name]));

    [Fact]
    public void ActualNativeGameSaverPublishesClosedZipWithEveryRegisteredEntryAndHash()
    {
        using var f = new Fixture();
        var saveSystem = Native.LoadNative("Timberborn.SaveSystem");
        var entries = Array.CreateInstance(saveSystem.GetType("Timberborn.SaveSystem.ISaveEntryWriter")!, 2);
        for (int i = 0; i < 2; i++)
        {
            int index = i;
            entries.SetValue(NativePersistenceProxy.Create(entries.GetType().GetElementType()!, (method, args) =>
            {
                if (method.Name == "get_EntryName") return "native-" + index;
                if (method.Name != "WriteToSaveEntryStream") throw new NotSupportedException(method.Name);
                var bytes = Encoding.UTF8.GetBytes("payload-" + index);
                ((Stream)args[0]!).Write(bytes);
                return null;
            }), i);
        }
        var writer = Activator.CreateInstance(saveSystem.GetType("Timberborn.SaveSystem.SaveWriter")!, [entries]);
        var saverType = Native.LoadNative("Timberborn.GameSaveRuntimeSystem").GetType("Timberborn.GameSaveRuntimeSystem.GameSaver")!;
        var saver = Activator.CreateInstance(saverType, [null, null, writer]);
        var serialize = (Action<Stream>)saverType.GetMethod("SaveWithoutFinishingTick")!.CreateDelegate(typeof(Action<Stream>), saver);
        f.Publish(serialize);
        Assert.Equal(true, f.Get("Published"));
        Assert.False(File.Exists(f.Temp));
        using (var exclusive = new FileStream(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        using (var zip = new ZipArchive(exclusive, ZipArchiveMode.Read))
        {
            Assert.Equal(2, zip.Entries.Count);
            foreach (int i in new[] { 0, 1 })
                Assert.Equal("payload-" + i, new StreamReader(zip.GetEntry("native-" + i)!.Open()).ReadToEnd());
        }
        Assert.Equal(new FileInfo(f.Path).Length, f.Get("Bytes"));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f.Path))), f.Get("Hash"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollisionBeforeOrDuringSerializationNeverOverwritesExistingSave(bool during)
    {
        using var f = new Fixture();
        if (!during) File.WriteAllText(f.Path, "original");
        int calls = 0;
        var failure = Assert.Throws<TargetInvocationException>(() => f.Publish(stream =>
        {
            calls++; stream.WriteByte(7); File.WriteAllText(f.Path, "original");
        }));
        Assert.IsAssignableFrom<IOException>(failure.InnerException);
        Assert.Equal(during ? 1 : 0, calls);
        Assert.Equal("original", File.ReadAllText(f.Path));
        Assert.Equal(false, f.Get("Published"));
        Assert.Equal(during, File.Exists(f.Temp));
    }

    [Fact]
    public void SerializerFailurePreservesCauseClosesPartialAndDoesNotPublish()
    {
        using var f = new Fixture();
        var expected = new InvalidOperationException("native writer callback");
        Assert.Same(expected, Assert.Throws<TargetInvocationException>(() => f.Publish(stream =>
        { stream.WriteByte(17); throw expected; })).InnerException);
        Assert.False(File.Exists(f.Path));
        using var partial = new FileStream(f.Temp, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(17, partial.ReadByte());
    }

    [Fact]
    public void LoadedIdentityRejectsEqualReplacementAndInPlaceMutation()
    {
        using var f = new Fixture();
        Assert.True(f.Matches(f.Loaded));
        var copy = Activator.CreateInstance(f.Loaded.GetType(), "Original", f.Settlement);
        Assert.False(f.Matches(copy));
        f.Loaded.GetType().GetProperty("SaveName")!.SetValue(f.Loaded, "changed");
        Assert.False(f.Matches(f.Loaded));
        f.Loaded.GetType().GetProperty("SaveName")!.SetValue(f.Loaded, "Original");
        f.Settlement.GetType().GetProperty("SettlementName")!.SetValue(f.Settlement, "changed");
        Assert.False(f.Matches(f.Loaded));
    }

    private static Type CopyType => Native.LoadMod().GetType("Wildfire.Timberborn.Qa.TimberbornQaSaveCopy")!;
    private sealed class Fixture : IDisposable
    {
        private readonly string _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wildfire-qa-save-test-" + Guid.NewGuid());
        internal string Path { get; }
        internal string Temp => (string)Get("TempPath")!;
        internal object Loaded { get; }
        internal object Settlement { get; }
        private readonly object _copy;
        internal Fixture()
        {
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "QA-copy.timber");
            var native = Native.LoadNative("Timberborn.GameSaveRepositorySystem");
            Settlement = Activator.CreateInstance(native.GetType("Timberborn.GameSaveRepositorySystem.SettlementReference")!, "Settlement", _directory)!;
            Loaded = Activator.CreateInstance(native.GetType("Timberborn.GameSaveRepositorySystem.SaveReference")!, "Original", Settlement)!;
            _copy = Activator.CreateInstance(CopyType, Hidden, null, [Guid.NewGuid(), Loaded, Path], null)!;
        }
        internal object? Get(string name) => CopyType.GetProperty(name, Hidden)!.GetValue(_copy);
        internal bool Matches(object? current) => (bool)CopyType.GetMethod("Matches", Hidden)!.Invoke(_copy, [current])!;
        internal void Publish(Action<Stream> write) => CopyType.GetMethod("Publish", Hidden)!.Invoke(_copy, [write]);
        public void Dispose() => Directory.Delete(_directory, true);
    }
}
