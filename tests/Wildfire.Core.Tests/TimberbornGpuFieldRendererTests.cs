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
        Assert.Contains("float vertexAlpha = debugOverlayEnabled ? DebugOverlayVertexAlpha : 0f;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AshOverlayPresentationBindsAtmosphericAshWithoutProjection()
    {
        string rendererSource = ReadTimberbornGpuFieldRendererSource();
        string shaderSource = ReadUnitySource("AshOverlay.shader");

        Assert.Contains("object? TransportFieldsBuffer = null", rendererSource, StringComparison.Ordinal);
        Assert.Contains("renderBinding.TransportFieldsBuffer", rendererSource, StringComparison.Ordinal);
        Assert.Contains("Shader.PropertyToID(\"_AtmosphericFields\")", rendererSource, StringComparison.Ordinal);
        Assert.Contains("Shader.PropertyToID(\"_UseAtmosphericAsh\")", rendererSource, StringComparison.Ordinal);
        Assert.Contains("StructuredBuffer<uint> _AtmosphericFields;", shaderSource, StringComparison.Ordinal);
        Assert.Contains("AtmosphericFalloutAshAndContamination", shaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("for (int scanZ = z + 1; scanZ < _GridDepth; scanZ += 1)", shaderSource, StringComparison.Ordinal);
        Assert.DoesNotContain("projectedAsh = max(projectedAsh, lerp(atmosphericLower, atmosphericUpper, blend.y));", shaderSource, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void PresenterRootActivationKeepsValidSafeDebugOverlayVisible(
        bool debugOverlayEnabled,
        bool expectedActive)
    {
        TimberbornGpuFieldRendererPresentation presentation = new(
            MaterialFieldsBuffer: new object(),
            TransportFieldsBuffer: new object(),
            GridWidth: 4,
            GridHeight: 3,
            GridDepth: 2);

        bool shouldActivate = TimberbornUnityGpuFieldRendererPresenter.ShouldActivateRootForPresentation(
            presentation,
            debugOverlayEnabled);

        Assert.Equal(expectedActive, shouldActivate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PresenterRootActivationRejectsInvalidPresentationInNormalAndDebugModes(bool debugOverlayEnabled)
    {
        TimberbornGpuFieldRendererPresentation presentation = new(
            MaterialFieldsBuffer: new object(),
            TransportFieldsBuffer: null,
            GridWidth: 4,
            GridHeight: 3,
            GridDepth: 2);

        bool shouldActivate = TimberbornUnityGpuFieldRendererPresenter.ShouldActivateRootForPresentation(
            presentation,
            debugOverlayEnabled);

        Assert.False(shouldActivate);
    }

    [Fact]
    public void CompleteVisualEffectDispatchCanRenderRestoredAshBeforeSimulationTick()
    {
        object visualFieldsBuffer = new();
        object materialFieldsBuffer = new();
        object transportFieldsBuffer = new();
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        surface.Bind(new TimberbornGpuVisualFieldSurfaceBinding(
            visualFieldsBuffer,
            transportFieldsBuffer,
            materialFieldsBuffer,
            width: 4,
            height: 3,
            depth: 2,
            cellCount: 24,
            strideBytes: 16,
            channels: TimberbornGpuVisualFieldChannels.All));
        surface.MarkUpdated(37);
        RecordingGpuFieldRendererPresenter presenter = new();
        TimberbornGpuFieldRendererSink renderer = new(
            surface,
            new RecordingFireLogSink(),
            TimberbornGpuFieldRendererOptions.Default,
            presenter);

        renderer.CompleteVisualEffectDispatch(37);

        Assert.Equal(1, presenter.RenderPresentationCallCount);
        Assert.Same(materialFieldsBuffer, presenter.LastPresentation.MaterialFieldsBuffer);
        Assert.Same(transportFieldsBuffer, presenter.LastPresentation.TransportFieldsBuffer);
        Assert.Equal(4, presenter.LastPresentation.GridWidth);
        Assert.Equal(3, presenter.LastPresentation.GridHeight);
        Assert.Equal(2, presenter.LastPresentation.GridDepth);
    }

    [Fact]
    public void RuntimeInitializesGpuFieldRendererPresentationAfterPersistenceRestore()
    {
        string runtimeSource = ReadTimberbornSource("TimberbornFireRuntime.cs");

        Assert.Contains("RestorePersistentConsequenceAndAshState(_pendingPersistenceSnapshot);", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_gpuIndirectRenderer.SeedSmoothedFieldsFromRestoredBuffers(fireSystem.LastTick ?? 0);", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("_gpuFieldRenderer.CompleteVisualEffectDispatch(fireSystem.LastTick ?? 0);", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenceRestoreRepopulatesVisualFieldsBeforeSimulationTick()
    {
        string simulatorSource = ReadTimberbornSource("TimberbornComputeFireSimulator.cs");

        Assert.Contains("_visualFields.SetData(CreateRestoredVisualFields(cells, transportFields, _parameters));", simulatorSource, StringComparison.Ordinal);
        Assert.Contains("private static Vector4[] CreateRestoredVisualFields(", simulatorSource, StringComparison.Ordinal);
        Assert.Contains("float smoke = atmospheric.Smoke / 7f;", simulatorSource, StringComparison.Ordinal);
        Assert.Contains("float ash = atmospheric.Ash / 7f;", simulatorSource, StringComparison.Ordinal);
        Assert.Contains("return new Vector4(fire, smoke, ash, visibility);", simulatorSource, StringComparison.Ordinal);
        Assert.Contains("_visualFieldBindingLifecycle?.MarkUpdated(_tick);", simulatorSource, StringComparison.Ordinal);
    }

    [Fact]
    public void IndirectRendererSeedsSmoothedFieldsFromRestoredBuffers()
    {
        string indirectRenderer = ReadTimberbornSource("TimberbornGpuIndirectFireRenderer.cs");

        Assert.Contains("public void SeedSmoothedFieldsFromRestoredBuffers(uint tick)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_simulator.VisualFieldsBuffer.GetData(visualFields);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_simulator.CurrentAtmosphericFieldsBuffer.GetData(atmosphericFields);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("WildfireTransportFieldState atmospheric = WildfireTransportFieldState.Unpack(atmosphericFields[index]);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("atmospheric.SmokeContamination / 7f", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("atmospheric.Steam / 7f", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smoothedFieldsBuffer!.SetData(smoothedFields);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_gpu_indirect_renderer_seeded", indirectRenderer, StringComparison.Ordinal);
    }

    [Fact]
    public void IndirectRendererBundleUnloadIsDefensiveDuringUnityTeardown()
    {
        string indirectRenderer = ReadTimberbornSource("TimberbornGpuIndirectFireRenderer.cs");

        Assert.Contains("DisposeGpuResources();\n        UnloadBundle();", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("AssetBundle? bundle = _bundle;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_bundle = null;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("if (bundle == null)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("bundle.Unload(unloadAllLoadedObjects: true);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("catch (NullReferenceException exception)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("catch (MissingReferenceException exception)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_effects_bundle_unload_skipped", indirectRenderer, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuFieldRendererClearIsDefensiveDuringUnityTeardown()
    {
        string fieldRenderer = ReadTimberbornGpuFieldRendererSource();

        Assert.Contains("DestroyUnityObject(ref _root);", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("DestroyUnityObject(ref _mesh);", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("DestroyUnityObject(ref _material);", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("UnloadShaderBundle();", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("T? target = unityObject;\n        unityObject = null;", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("UnityEngine.Object.Destroy(target);", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("bundle.Unload(unloadAllLoadedObjects: true);", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("catch (NullReferenceException)", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("catch (MissingReferenceException)", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_gpu_field_renderer_clear_skipped", fieldRenderer, StringComparison.Ordinal);
        Assert.Contains("reason=unity_teardown", fieldRenderer, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuFieldRendererClearSkippedLogDoesNotIncludeExceptionTokens()
    {
        string fieldRenderer = ReadTimberbornGpuFieldRendererSource();

        Assert.Contains(
            "\"wildfire_timberborn_gpu_field_renderer_clear_skipped \" +\n            \"reason=unity_teardown\"",
            fieldRenderer,
            StringComparison.Ordinal);
        Assert.DoesNotContain("reason=unity_teardown exception=", fieldRenderer, StringComparison.Ordinal);
        Assert.DoesNotContain("reason=unity_teardown\" +\n            $\"message=", fieldRenderer, StringComparison.Ordinal);
        Assert.DoesNotContain("Object reference not set to an instance of an object.", fieldRenderer, StringComparison.Ordinal);
    }

    [Fact]
    public void GpuFieldRendererClearLogsUnexpectedCleanupExceptionDiagnostics()
    {
        TimberbornGpuVisualFieldSurface surface = new(new RecordingFireLogSink());
        RecordingFireLogSink logSink = new();
        TimberbornGpuFieldRendererSink renderer = new(
            surface,
            logSink,
            TimberbornGpuFieldRendererOptions.Default,
            new ThrowingClearGpuFieldRendererPresenter());

        renderer.Clear();

        string warning = Assert.Single(logSink.WarningMessages);
        Assert.Contains("wildfire_timberborn_gpu_field_renderer_failed", warning, StringComparison.Ordinal);
        Assert.Contains("stage=clear", warning, StringComparison.Ordinal);
        Assert.Contains("message=\"synthetic unexpected clear failure\"", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimKeepsDynamicAshOutOfCompanionFields()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("bool IsEntityMaterial(uint companion)", source, StringComparison.Ordinal);
        Assert.Contains("bool IsAshLandingSurface(uint companion, uint3 coordinate)", source, StringComparison.Ordinal);
        Assert.Contains("bool IsAshLandingSurfaceForCell(uint cell, uint companion, uint3 coordinate)", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 2u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 3u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 4u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 5u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 6u ||", source, StringComparison.Ordinal);
        Assert.Contains("materialClass == 7u;", source, StringComparison.Ordinal);
        Assert.Contains("if (CompanionMaterialClass(companion) == 1u)", source, StringComparison.Ordinal);
        Assert.Contains("return CompanionMaterialClass(belowCompanion) != CompanionMaterialClass(companion);", source, StringComparison.Ordinal);
        Assert.Contains("return Terrain(cell) == 1u || IsAshLandingSurface(companion, coordinate);", source, StringComparison.Ordinal);
        Assert.Contains("CurrentAtmosphericFields[change.CellIndex] = ApplyAshChange(oldAtmospheric, change);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CompanionFields[index] = companion;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("uint companion = UpdateCompanionAsh(CompanionFields[index], id, oldCell, newCell, atmospheric);", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FireSimFallsNewAshSourcesOffUnsupportedCells()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("uint AshSourceAmount(uint oldCell, uint newCell, uint oldAtmospheric)", source, StringComparison.Ordinal);
        Assert.Contains("uint localAshSource = AshSourceAmount(oldCell, newCell, oldAtmospheric);", source, StringComparison.Ordinal);
        Assert.Contains("uint aboveAshSource = AshSourceAmount(aboveOldCell, aboveNewCell, aboveAtmospheric);", source, StringComparison.Ordinal);
        Assert.Contains("if (!IsAshLandingSurfaceForCell(aboveNewCell, aboveCompanion, aboveCoordinate))", source, StringComparison.Ordinal);
        Assert.Contains("uint fallingAsh = max(AtmosphericAsh(aboveAtmospheric), aboveAshSource);", source, StringComparison.Ordinal);
        Assert.Contains("ash = max(ash, isAshLandingSurface ? localAshSource : 0u);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ash = max(ash, ashSource);", source, StringComparison.Ordinal);
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
        Assert.Contains("float SmokeWindBias(float directionX, float directionY)", source, StringComparison.Ordinal);
        Assert.Contains("float directionalBias = lerp(0.03f, 5.0f, saturate((alignment + 1.0f) * 0.5f));", source, StringComparison.Ordinal);
        Assert.Contains("float SmokeWindAdvectionProbability(uint sourceSmoke, float directionX, float directionY)", source, StringComparison.Ordinal);
        Assert.Contains("float AtmosphericWindAdvectionProbability(uint sourceAmount, float directionX, float directionY, int verticalOffset)", source, StringComparison.Ordinal);
        Assert.Contains("float alignment = saturate((directionX * WindDirectionX) + (directionY * WindDirectionY));", source, StringComparison.Ordinal);
        Assert.Contains("float lift = saturate((strength - 0.55f) * 2.2222223f);", source, StringComparison.Ordinal);
        Assert.Contains("float settling = saturate((0.35f - strength) * 2.857143f);", source, StringComparison.Ordinal);
        Assert.Contains("uint SmokeWindAdvectionTravelAmount(uint sourceSmoke, uint distance)", source, StringComparison.Ordinal);
        Assert.Contains("uint AtmosphericWindAdvectionTravelAmount(uint sourceAmount, uint distance)", source, StringComparison.Ordinal);
        Assert.Contains("bool SmokeWindAdvectionRollPasses(uint sourceIndex, uint sourceSmoke, float directionX, float directionY, uint salt)", source, StringComparison.Ordinal);
        Assert.Contains("bool AtmosphericWindAdvectionRollPasses(uint sourceIndex, uint sourceAmount, float directionX, float directionY, int verticalOffset, uint salt)", source, StringComparison.Ordinal);
        Assert.Contains("bool SmokeSourceChoosesTarget(uint3 sourceCoordinate, uint3 targetCoordinate, uint sourceSmoke)", source, StringComparison.Ordinal);
        Assert.Contains("return SmokeSourceChoosesTarget(sourceCoordinate, targetCoordinate, sourceSteam) ? 1u : 0u;", source, StringComparison.Ordinal);
        Assert.Contains("uint movedOut = SmokeMoveRollPasses(sourceIndex, sourceSteam, SmokeMoveWeightTotal(sourceCoordinate)) ? 1u : 0u;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamMoveWeightTotal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SteamSourceChoosesTarget", source, StringComparison.Ordinal);
        Assert.Contains("uint SmokeMovedOutAmount(uint3 sourceCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("uint movedSmoke = max(1u, (sourceSmoke + 1u) / 2u);", source, StringComparison.Ordinal);
        Assert.Contains("uint SmokeMoveTravelAmount(uint sourceSmoke, uint3 sourceCoordinate, uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("return movedSmoke;", source, StringComparison.Ordinal);
        Assert.Contains("uint retainedSmoke = DecayBy(AtmosphericSmoke(oldAtmospheric), 1u);", source, StringComparison.Ordinal);
        Assert.Contains("retainedSmoke = retainedSmoke > movedOutSmoke ? retainedSmoke - movedOutSmoke : 0u;", source, StringComparison.Ordinal);
        Assert.Contains("uint smokeContribution = SmokeMoveAmount(sourceCoordinate, targetCoordinate);", source, StringComparison.Ordinal);
        Assert.Contains("float pressure = max(smokePressure, contaminationMixing);", source, StringComparison.Ordinal);
        Assert.Contains("float contribution = pressure * SmokeDiffusionWeight(sourceCoordinate, targetCoordinate);", source, StringComparison.Ordinal);
        Assert.Contains("return min(7u, (uint)round(contribution));", source, StringComparison.Ordinal);
        Assert.Contains("movedOut += SmokeMoveTravelAmount(sourceSmoke, sourceCoordinate, candidateCoordinate);", source, StringComparison.Ordinal);
        Assert.Contains("uint SmokeWindMovedOutContribution(", source, StringComparison.Ordinal);
        Assert.Contains("movedOut += SmokeWindMovedOutContribution(sourceCoordinate, sourceSmoke, sourceIndex, int3(2, 0, 0), 1.0f, 0.0f, 2u, 0x1a2b3c41u);", source, StringComparison.Ordinal);
        Assert.Contains("movedOut += SmokeWindMovedOutContribution(sourceCoordinate, sourceSmoke, sourceIndex, int3(0, 0, -1), 0.0f, 0.0f, 1u, 0x1a2b3c51u);", source, StringComparison.Ordinal);
        Assert.Contains("movedOut += SmokeWindMovedOutContribution(sourceCoordinate, sourceSmoke, sourceIndex, int3(2, 0, 1), 1.0f, 0.0f, 2u, 0x1a2b3c56u);", source, StringComparison.Ordinal);
        Assert.Contains("return min(sourceSmoke, movedOut);", source, StringComparison.Ordinal);
        Assert.Contains("float baseWeight = hasHorizontalDrift", source, StringComparison.Ordinal);
        Assert.Contains("? lerp(0.15f, 1.15f, strength)", source, StringComparison.Ordinal);
        Assert.Contains("? lerp(0.62f, 0.12f, strength)", source, StringComparison.Ordinal);
        Assert.Contains("float SmokeDiffusionWeight(uint3 sourceCoordinate, uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("bool TrySmokeMoveOffset(uint3 sourceCoordinate, int3 offset, out uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("float SmokeMoveOffsetSinkWeight(int3 offset)", source, StringComparison.Ordinal);
        Assert.Contains("for (int z = -1; z <= 1; z++)", source, StringComparison.Ordinal);
        Assert.Contains("return SmokeDirectionWeight((float)dx / length, (float)dy / length, diagonal, dz > 0, dz < 0);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("illegalUpDiagonal", source, StringComparison.Ordinal);
        Assert.Contains("TrySmokeMoveOffset(coordinate, int3(x, y, -1), sourceCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("void AddWindAdvectedSmokeContribution(", source, StringComparison.Ordinal);
        Assert.Contains("void AddWindAdvectedSmokeContributions(", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSmokeContributions(", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSmokeContribution(smokeTotal, contaminationWeightedTotal, targetCoordinate, int3(-2, 0, 0), 1.0f, 0.0f, 2u, 0x1a2b3c41u);", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSmokeContribution(smokeTotal, contaminationWeightedTotal, targetCoordinate, int3(0, 0, 1), 0.0f, 0.0f, 1u, 0x1a2b3c51u);", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSmokeContribution(smokeTotal, contaminationWeightedTotal, targetCoordinate, int3(-2, 0, -1), 1.0f, 0.0f, 2u, 0x1a2b3c56u);", source, StringComparison.Ordinal);
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
    public void FireSimTransportsCleanSteamWithoutContaminationLane()
    {
        string source = ReadUnitySource("FireSim.compute");

        Assert.Contains("uint SteamSourceFromMoistureAndHeat(uint cell)", source, StringComparison.Ordinal);
        Assert.Contains("uint SteamMoveAmount(uint3 sourceCoordinate, uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("uint sourceSteam = DecayBy(AtmosphericSteam(NeighborAtmospheric(sourceCoordinate)), 2u);", source, StringComparison.Ordinal);
        Assert.Contains("uint SteamMovedOutAmount(uint3 sourceCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("steam = steam > movedOutSteam ? steam - movedOutSteam : 0u;", source, StringComparison.Ordinal);
        Assert.Contains("void AddMovedSteamContribution(inout uint steam, uint3 sourceCoordinate, uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("steam += SteamMoveAmount(sourceCoordinate, targetCoordinate);", source, StringComparison.Ordinal);
        Assert.Contains("AddMovedSteamContribution(", source, StringComparison.Ordinal);
        Assert.Contains("uint SteamWindMovedOutContribution(", source, StringComparison.Ordinal);
        Assert.Contains("movedOut += SteamWindMovedOutContribution(sourceCoordinate, sourceSteam, sourceIndex, int3(0, 0, -1), 0.0f, 0.0f, 1u, 0x5e2a6d51u);", source, StringComparison.Ordinal);
        Assert.Contains("void AddWindAdvectedSteamContributions(inout uint steam, uint3 targetCoordinate)", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSteamContribution(steam, targetCoordinate, int3(-2, 0, -1), 1.0f, 0.0f, 2u, 0x5e2a6d56u);", source, StringComparison.Ordinal);
        Assert.Contains("AddWindAdvectedSteamContributions(steam, coordinate);", source, StringComparison.Ordinal);
        Assert.Contains("Steam: atmospheric.Steam / 7f", ReadTimberbornSource("TimberbornGpuVisualFieldSurface.cs"), StringComparison.Ordinal);
        Assert.DoesNotContain("SteamContamination", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToxicSteam", source, StringComparison.Ordinal);
        Assert.DoesNotContain("contaminated steam", source, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("private const int SmokePuffsPerCell  = 8;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SmokeRadius = 1.38f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SmokeHeightOffset = 3.55f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SmokeMaxOpacity = 0.74f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const int SteamPuffsPerCell  = 8;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamColorRed = 0.92f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamColorGreen = 0.98f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamColorBlue = 1.0f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamRadius = 1.18f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamHeightOffset = 0.08f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamMaxHeight = 2.85f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamMaxOpacity = 0.72f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("(uint)(cellCount * SmokePuffsPerCell)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("(uint)(cellCount * SteamPuffsPerCell)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetColor(\"_BaseColor\",   new Color(0.34f, 0.35f, 0.34f));", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_Radius\",        SmokeRadius);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_HeightOffset\",  SmokeHeightOffset);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_MaxOpacity\",    Math.Clamp(SmokeMaxOpacity * _options.VisualIntensityScale, 0f, 1f));", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial.SetFloat(\"_PuffsPerCell\",  (float)SmokePuffsPerCell);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("new Color(SteamColorRed, SteamColorGreen, SteamColorBlue)", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_Radius\",        SteamRadius);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_HeightOffset\",  SteamHeightOffset);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_MaxSteamHeight\", SteamMaxHeight);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial.SetFloat(\"_MaxOpacity\",    Math.Clamp(SteamMaxOpacity * _options.VisualIntensityScale, 0f, 1f));", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smokeMaterial!.SetVector(\"_Wind\", cloudWind);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_steamMaterial!.SetVector(\"_Wind\", cloudWind);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_gpu_indirect_renderer_smoke_tuning", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("wildfire_timberborn_gpu_indirect_renderer_steam_tuning", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("field_source=atmospheric_fields clean=true contaminated=false", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("visualSettings.ToGpuIndirectFireRendererOptions()", ReadTimberbornSource("TimberbornFireRuntime.cs"), StringComparison.Ordinal);
        Assert.Contains("float orderedSlot = (float)puffSlot", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float slotActivation = smoothstep(slotThreshold", cloudShader, StringComparison.Ordinal);
        Assert.Contains(": saturate(intensity * 1.35);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float slotThreshold = saturate(lerp(0.0, 0.46, orderedSlot)", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokeBreakup = smoothstep(0.34, 0.92, noise);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokePhase = frac(_Time.y", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float smokeAngle = seedC * 6.2831853", cloudShader, StringComparison.Ordinal);
        Assert.Contains("float contaminationStrength = saturate(i.contam);", cloudShader, StringComparison.Ordinal);
        Assert.Contains("jitter += windDir * windStrength", cloudShader, StringComparison.Ordinal);
        Assert.Contains("private const float SmokeUpSpeed  = 2.8f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SmokeDownSpeed = 0.72f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smoothingShader.SetFloat( \"_SmokeDownSpeed\",SmokeDownSpeed);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamUpSpeed  = 3.10f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("private const float SteamDownSpeed = 0.70f;", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smoothingShader.SetFloat( \"_SteamUpSpeed\",  SteamUpSpeed);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("_smoothingShader.SetFloat( \"_SteamDownSpeed\",SteamDownSpeed);", indirectRenderer, StringComparison.Ordinal);
        Assert.Contains("float puffPhase = frac(_Time.y * 0.18", cloudShader, StringComparison.Ordinal);
        Assert.Contains("jitter += windDir * windStrength * puffPhase * 0.18;", cloudShader, StringComparison.Ordinal);
        Assert.Contains("lerp(0.34, 0.82, intensity)", ashOverlayShader, StringComparison.Ordinal);
    }

    private static string ReadTimberbornGpuFieldRendererSource()
    {
        return ReadTimberbornSource("TimberbornGpuFieldRenderer.cs");
    }

    private static string ReadTimberbornSource(string fileName)
    {
        string path = SelfAndParents(new DirectoryInfo(AppContext.BaseDirectory))
            .Select(directory => Path.Combine(directory.FullName, "src", "Wildfire.Timberborn"))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, fileName, SearchOption.AllDirectories))
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

        public int RenderPresentationCallCount { get; private set; }

        public TimberbornGpuFieldRendererPresentation LastPresentation { get; private set; }

        public TimberbornGpuFieldRendererPresentationResult RenderRegions(
            IReadOnlyList<TimberbornGpuFieldRendererRegionState> regions)
        {
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public TimberbornGpuFieldRendererPresentationResult RenderPresentation(
            TimberbornGpuFieldRendererPresentation presentation)
        {
            RenderPresentationCallCount++;
            LastPresentation = presentation;
            return TimberbornGpuFieldRendererPresentationResult.Applied;
        }

        public void Clear()
        {
        }
    }

    private sealed class ThrowingClearGpuFieldRendererPresenter : ITimberbornGpuFieldRendererPresenter
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
            throw new InvalidOperationException("synthetic unexpected clear failure");
        }
    }

    private sealed class RecordingFireLogSink : ITimberbornFireLogSink
    {
        public List<string> InfoMessages { get; } = [];

        public List<string> WarningMessages { get; } = [];

        public void Info(string message)
        {
            InfoMessages.Add(message);
        }

        public void Warning(string message)
        {
            WarningMessages.Add(message);
        }
    }
}
