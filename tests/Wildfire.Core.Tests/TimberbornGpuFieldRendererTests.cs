using Wildfire.Timberborn;

namespace Wildfire.Core.Tests;

public sealed class TimberbornGpuFieldRendererTests
{
    [Fact]
    public void QaResultTokenIncludesGpuFieldRendererTelemetry()
    {
        TimberbornQaCommandState state = new(
            IsSimulatorIntegrated: true,
            VisualFieldSurfaceBound: true,
            VisualFieldSurfaceCellCount: 16,
            GpuFieldRendererEnabled: true,
            GpuFieldRendererMaterialReady: true,
            GpuFieldRendererSurfaceBound: true,
            GpuFieldRendererVisibleRegionCount: 0,
            GpuFieldRendererUpdatedRegionCount: 0,
            GpuFieldRendererLastNonZeroUpdatedRegionCount: 0,
            GpuFieldRendererLastNonZeroUpdatedRegionTick: null,
            GpuFieldRendererMaxUpdatedRegionCount: 0,
            GpuFieldRendererDroppedRegionCount: 0,
            GpuFieldRendererInvisibleRegionCount: 0,
            GpuFieldRendererMaterialFailureCount: 0,
            GpuFieldRendererLastUpdatedTick: null);

        TimberbornQaCommandResult result = TimberbornQaCommandResult.CreateSuccess(
            "status",
            state,
            ["status"]);

        Assert.Contains("gpu_field_renderer_enabled=true", result.ResultToken);
        Assert.Contains("gpu_field_renderer_material_ready=true", result.ResultToken);
        Assert.Contains("gpu_field_renderer_visible_regions=0", result.ResultToken);
        Assert.Contains("gpu_field_renderer_updated_regions=0", result.ResultToken);
        Assert.Contains("gpu_field_renderer_dropped_regions=0", result.ResultToken);
        Assert.Contains("gpu_field_renderer_invisible_regions=0", result.ResultToken);
        Assert.Contains("gpu_field_renderer_material_failures=0", result.ResultToken);
    }

    [Fact]
    public void UnityAshOverlayMeshUsesTopFacingTriangleWinding()
    {
        string source = ReadTimberbornGpuFieldRendererSource();

        Assert.Contains("AshOverlayShaderBundleMac = \"wildfire_visual_mac\"", source, StringComparison.Ordinal);
        Assert.Contains("AshOverlayMaskTextureResourceName", source, StringComparison.Ordinal);
        Assert.Contains(
            "return new[] { offset, offset + 2, offset + 1, offset + 2, offset + 3, offset + 1 };",
            source,
            StringComparison.Ordinal);
        Assert.Contains("mesh.uv = uvs;", source, StringComparison.Ordinal);
        Assert.Contains("mesh.uv2 = uv2s;", source, StringComparison.Ordinal);
    }

    private static string ReadTimberbornGpuFieldRendererSource()
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(
                directory.FullName,
                "src",
                "Wildfire.Timberborn",
                "TimberbornGpuFieldRenderer.cs"))
            .First(File.Exists);

        return File.ReadAllText(path);
    }

    private static IEnumerable<DirectoryInfo> SelfAndParents(DirectoryInfo directory)
    {
        return directory.Parent is null
            ? [directory]
            : new[] { directory }.Concat(SelfAndParents(directory.Parent));
    }

    private sealed class RecordingGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
    {
        public TimberbornGpuFieldRendererPresenterState State { get; } = new(
            RendererEnabled: true,
            MaterialReady: true);

        public TimberbornGpuFieldRendererPresentationResult RenderRegions(
            IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
        {
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public TimberbornGpuFieldRendererPresentationResult RenderPresentation(
            TimberbornGpuFieldRendererPresentation presentation)
        {
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public void Clear()
        {
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
        }
    }
}
