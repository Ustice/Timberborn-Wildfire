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
        Assert.Contains("float y = Z + heightOffset;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AshOverlayPresentationBindsAtmosphericAshWithoutProjection()
    {
        string rendererSource = ReadTimberbornGpuFieldRendererSource();
        string shaderSource = ReadUnitySource("AshOverlay.shader");

        Assert.Contains("object? AtmosphericFieldsBuffer = null", rendererSource, StringComparison.Ordinal);
        Assert.Contains("renderBinding.AtmosphericFieldsBuffer", rendererSource, StringComparison.Ordinal);
        Assert.Contains("Shader.PropertyToID(\"_AtmosphericFields\")", rendererSource, StringComparison.Ordinal);
        Assert.Contains("Shader.PropertyToID(\"_UseAtmosphericAsh\")", rendererSource, StringComparison.Ordinal);
        Assert.Contains("StructuredBuffer<uint> _AtmosphericFields;", shaderSource, StringComparison.Ordinal);
        Assert.Contains("AtmosphericFalloutAshAndContamination", shaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (int scanZ = z + 1; scanZ < _GridDepth; scanZ += 1)", shaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("projectedAsh = max(projectedAsh, lerp(atmosphericLower, atmosphericUpper, blend.y));", shaderSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimKeepsDynamicAshOutOfCompanionFields()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("bool IsEntityMaterial(uint companion)", source, StringComparison.Ordinal);
        Assert.Contains("bool IsAshLandingSurface(uint companion, uint3 coordinate)", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 2u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 3u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 4u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 5u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 6u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 7u;", source, StringComparison.Ordinal);
        Assert.Contains("if (CompanionMaterialClass(companion) == 1u)", source, StringComparison.Ordinal);
        Assert.Contains("return CompanionMaterialClass(belowCompanion) != CompanionMaterialClass(companion);", source, StringComparison.Ordinal);
        Assert.Contains("CurrentAtmosphericFields[change.CellIndex] = ApplyAshChange(oldAtmospheric, change);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompanionFields[index] = companion;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("uint companion = UpdateCompanionAsh(CompanionFields[index], id, oldCell, newCell, atmospheric);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimTaintableMaterialsCanEmitContaminatedSmokeFromSoilContamination()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("uint CompanionSoilContamination(uint companion)", source, StringComparison.Ordinal);
        Assert.Contains("return (companion >> 25) & 0x7u;", source, StringComparison.Ordinal);
        Assert.Contains("sourceContamination == 0u && contaminationBehavior == 1u", source, StringComparison.Ordinal);
        Assert.Contains("sourceContamination = CompanionSoilContamination(companion);", source, StringComparison.Ordinal);
        Assert.Contains("AddSmokeContribution(smokeTotal, smokeContaminationWeightedTotal, smokeSource, sourceContamination);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimUsesDeterministicStochasticSmokeMovementAndDilution()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("float SmokeMoveProbability(uint sourceSmoke)", source, StringComparison.Ordinal);
        Assert.Contains("return saturate(0.08f + ((float)sourceSmoke * 0.018f) + (EffectiveWindStrength() * 0.07f));", source, StringComparison.Ordinal);
        Assert.Contains("bool SmokeSourceChoosesTarget(uint3 sourceCoordinate, uint3 targetCoordinate, uint sourceSmoke)", source, StringComparison.Ordinal);
        Assert.Contains("uint SmokeMovedOutAmount(uint3 sourceCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("retainedSmoke = retainedSmoke > movedOutSmoke ? retainedSmoke - movedOutSmoke : 0u;", source, StringComparison.Ordinal);
        Assert.Contains("uint smokeContribution = SmokeMoveAmount(sourceCoordinate, targetCoordinate);", source, StringComparison.Ordinal);
        Assert.Contains("return 0.75f;", source, StringComparison.Ordinal);
        Assert.Contains("return 0.22f;", source, StringComparison.Ordinal);
        Assert.Contains("uint3(sourceCoordinate.x, sourceCoordinate.y, sourceCoordinate.z - 1u)", source, StringComparison.Ordinal);
        Assert.Contains("bool IsSmokeMoveTargetOpen(uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("return Terrain(CurrentCells[ToIndex(targetCoordinate)]) == 0u;", source, StringComparison.Ordinal);
        Assert.Contains("float AddSmokeMoveSinkCandidate(float total, float directionX, float directionY, bool diagonal, bool upward)", source, StringComparison.Ordinal);
        Assert.Contains("void SkipSmokeMoveSink(inout float cursor, float directionX, float directionY, bool diagonal, bool upward)", source, StringComparison.Ordinal);
        Assert.Contains("uint DilutedSmokeContamination(uint smokeTotal, uint contaminationWeightedTotal)", source, StringComparison.Ordinal);
        Assert.Contains("smokeTotal += smokeContribution;", source, StringComparison.Ordinal);
        Assert.Contains("contaminationWeightedTotal += smokeContribution * contamination;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("StochasticSmokeAmount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WindSmokePenalty", source, StringComparison.Ordinal);
        Assert.DoesNotContain("smokeContamination = max(smokeContamination", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeAndSteamRenderersUseReleasePuffTuning()
    {
        string cloudShader = ReadUnitySource("WildfireCloud.shader");
        string ashOverlayShader = ReadUnitySource("AshOverlay.shader");
        string indirectRenderer = ReadTimberbornSource("TimberbornGpuIndirectFireRenderer.cs");

        Assert.Contains("_MaxOpacity     (\"Max Opacity\",               Range(0, 1)) = 0.62", cloudShader, StringComparison.Ordinal);
        Assert.Contains("Smoke: _PuffsPerCell staggered puffs per cell", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float CloudNoise(float2 p)", cloudShader, StringComparison.Ordinal);
        Assert.Contains("worldPos.xz += jitter;", cloudShader, StringComparison.Ordinal);
        Assert.DoesNotContain("sphereShade", cloudShader, StringComparison.Ordinal);
        Assert.Contains("private const int SmokePuffsPerCell  = 6;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("(uint)(cellCount * SmokePuffsPerCell)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetColor(\"_BaseColor\",   new Color(0.27f, 0.28f, 0.27f));", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_Radius\",        1.14f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_HeightOffset\",  3.24f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_MaxOpacity\",    0.62f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_PuffsPerCell\",  (float)SmokePuffsPerCell);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial!.SetVector(\"_Wind\", cloudWind);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("new TimberbornGpuIndirectFireRenderer(computeSim, grid, _logSink, _windProvider)", ReadTimberbornSource("TimberbornFireRuntime.cs"), StringComparison.Ordinal);
        Assert.Contains("float orderedSlot = (float)puffSlot", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float slotActivation = smoothstep(slotThreshold", cloudShader, StringComparison.Ordinal);
        Assert.Contains(": saturate(intensity * 1.35);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float slotThreshold = saturate(lerp(0.0, 0.46, orderedSlot)", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokeBreakup = smoothstep(0.34, 0.92, noise);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokePhase = frac(_Time.y", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokeAngle = seedC * 6.2831853", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float contaminationStrength = saturate(i.contam);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("jitter += windDir * windStrength", cloudShader, StringComparison.Ordinal);
        Assert.Contains("private const float SteamUpSpeed  = 1.45f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamDownSpeed = 3.75f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smoothingShader.SetFloat( \"_SteamDownSpeed\",SteamDownSpeed);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("float puffPhase = frac(_Time.y * 0.18", cloudShader, StringComparison.Ordinal);
        Assert.Contains("jitter += windDir * windStrength * puffPhase * 0.18;", cloudShader, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_Radius\",        0.66f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_MaxSteamHeight\", 1.45f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_MaxOpacity\",    0.36f);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("lerp(0.34, 0.82, intensity)", ashOverlayShader, StringComparison.Ordinal);
    }

    private static string ReadTimberbornGpuFieldRendererSource()
    {
        return ReadTimberbornSource("TimberbornGpuFieldRenderer.cs");
    }

    private static string ReadTimberbornSource(string fileName)
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(
                directory.FullName,
                "src",
                "Wildfire.Timberborn",
                fileName))
            .First(File.Exists);

        return File.ReadAllText(path);
    }

    private static string ReadUnitySource(string fileName)
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(
                directory.FullName,
                "src",
                "Wildfire.Unity",
                fileName))
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
