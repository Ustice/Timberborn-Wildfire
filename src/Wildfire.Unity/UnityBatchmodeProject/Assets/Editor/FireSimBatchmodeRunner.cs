using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    public static class FireSimBatchmodeRunner
    {
        private const int ThreadGroupSizeX = 8;
        private const int ThreadGroupSizeY = 8;
        private const int ThreadGroupSizeZ = 4;
        private const string GeneratedShaderAssetPath = "Assets/WildfireGenerated/FireSim.compute";

        public static void Capture()
        {
            try
            {
                ConfigureLogging();
                HarnessArguments arguments = HarnessArguments.Parse(Environment.GetCommandLineArgs());
                LogPhase("environment", "start", "project=" + Directory.GetCurrentDirectory());
                Fixture fixture = Fixture.Load(arguments.FixturePath);
                ComputeShader shader = LoadComputeShader(arguments.ShaderPath);
                Snapshot snapshot = DispatchFixture(shader, fixture, arguments.TickCount);
                snapshot.Write(arguments.OutputPath, fixture, arguments.TickCount);
                LogPhase("readback", "ok", "output=" + arguments.OutputPath);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("wildfire_shader_harness phase=failure status=error message=\"" + Escape(exception.Message) + "\"");
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static ComputeShader LoadComputeShader(string shaderPath)
        {
            LogPhase("compile", "start", "shader=" + shaderPath);
            if (!File.Exists(shaderPath))
            {
                throw new FileNotFoundException("FireSim.compute source was not found.", shaderPath);
            }

            string assetDirectory = Path.Combine(Application.dataPath, "WildfireGenerated");
            Directory.CreateDirectory(assetDirectory);
            string absoluteAssetPath = Path.Combine(assetDirectory, "FireSim.compute");
            File.Copy(shaderPath, absoluteAssetPath, overwrite: true);
            AssetDatabase.ImportAsset(GeneratedShaderAssetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            ComputeShader shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(GeneratedShaderAssetPath);
            if (shader == null)
            {
                throw new InvalidOperationException("Unity imported FireSim.compute but did not load a ComputeShader asset.");
            }

            shader.FindKernel("SimulateFullGrid");
            LogPhase("compile", "ok", "asset=" + GeneratedShaderAssetPath);
            return shader;
        }

        private static Snapshot DispatchFixture(ComputeShader shader, Fixture fixture, int tickCount)
        {
            int cellCount = checked(fixture.grid.width * fixture.grid.height * fixture.grid.depth);
            uint[] initialCells = fixture.packedCellValues.values.ConvertAll(cell => (uint)cell);
            if (initialCells.Length != cellCount)
            {
                throw new InvalidOperationException("Fixture cell count " + initialCells.Length + " did not match grid cell count " + cellCount + ".");
            }

            LogPhase("buffer", "start", "cells=" + cellCount);
            ComputeBuffer currentCells = null;
            ComputeBuffer nextCells = null;
            ComputeBuffer externalChanges = null;
            ComputeBuffer deltas = null;
            ComputeBuffer visualFields = null;
            ComputeBuffer currentAtmosphericFields = null;
            ComputeBuffer nextAtmosphericFields = null;
            ComputeBuffer companionFields = null;
            ComputeBuffer deltaCounter = null;
            MaterialHandoffFixtureBuffers material = null;

            try
            {
                material = new MaterialHandoffFixtureBuffers(fixture, cellCount, tickCount);
                currentCells = new ComputeBuffer(cellCount, sizeof(uint), ComputeBufferType.Structured);
                nextCells = new ComputeBuffer(cellCount, sizeof(uint), ComputeBufferType.Structured);
                externalChanges = new ComputeBuffer(Math.Max(1, cellCount), sizeof(uint) * 4, ComputeBufferType.Structured);
                deltas = new ComputeBuffer(checked(cellCount + externalChanges.count), sizeof(uint) * 4, ComputeBufferType.Append);
                visualFields = new ComputeBuffer(cellCount, sizeof(float) * 4, ComputeBufferType.Structured);
                currentAtmosphericFields = new ComputeBuffer(cellCount, sizeof(uint), ComputeBufferType.Structured);
                nextAtmosphericFields = new ComputeBuffer(cellCount, sizeof(uint), ComputeBufferType.Structured);
                companionFields = new ComputeBuffer(cellCount, sizeof(uint), ComputeBufferType.Structured);
                deltaCounter = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Raw);
                currentCells.SetData(initialCells);
                nextCells.SetData(initialCells);
                currentAtmosphericFields.SetData(CellArrayOrZeros(fixture.initialAtmosphericFields, cellCount, "initialAtmosphericFields"));
                nextAtmosphericFields.SetData(new uint[cellCount]);
                companionFields.SetData(CellArrayOrZeros(fixture.companionFields, cellCount, "companionFields"));
                LogPhase("buffer", "ok", "allocated=current,next,external,deltas,visual,atmospheric,companion");

                int kernel = shader.FindKernel("SimulateFullGrid");
                int applyKernel = shader.FindKernel("ApplyExternalChanges");
                fixture.ValidateExternalChanges(cellCount, tickCount);
                TickSnapshot[] ticks = new TickSnapshot[tickCount];
                for (int tick = 1; tick <= tickCount; tick += 1)
                {
                    LogPhase("dispatch", "start", "tick=" + tick);
                    deltas.SetCounterValue(0);
                    material.Upload(tick);
                    FireSimChangeGpu[] changes = fixture.ChangesForTick(tick);
                    uint[] appliedWords = new uint[changes.Length * 4];
                    if (changes.Length > 0)
                    {
                        externalChanges.SetData(changes);
                        Bind(
                            shader, applyKernel, fixture, tick,
                            currentCells, nextCells, externalChanges, deltas, visualFields,
                            currentAtmosphericFields, nextAtmosphericFields, companionFields);
                        material.Bind(shader, applyKernel);
                        shader.SetInt("ChangeCount", changes.Length);
                        shader.Dispatch(applyKernel, 1, 1, 1);
                        var applied = new FireSimChangeGpu[changes.Length];
                        externalChanges.GetData(applied, 0, 0, applied.Length);
                        for (int c = 0; c < applied.Length; c++)
                        {
                            appliedWords[c * 4] = applied[c].CellIndex;
                            appliedWords[c * 4 + 1] = applied[c].SetMask;
                            appliedWords[c * 4 + 2] = applied[c].AddFields;
                            appliedWords[c * 4 + 3] = applied[c].SetValues;
                        }
                        LogPhase("external-changes", "ok", "tick=" + tick + " count=" + changes.Length);
                    }

                    uint[] materialHeader = material.ReadHeader();
                    uint[] materialReceipts = material.ReadReceipts();
                    Bind(
                        shader,
                        kernel,
                        fixture,
                        tick,
                        currentCells,
                        nextCells,
                        externalChanges,
                        deltas,
                        visualFields,
                        currentAtmosphericFields,
                        nextAtmosphericFields,
                        companionFields);
                    material.Bind(shader, kernel);
                    shader.Dispatch(
                        kernel,
                        Groups(fixture.grid.width, ThreadGroupSizeX),
                        Groups(fixture.grid.height, ThreadGroupSizeY),
                        Groups(fixture.grid.depth, ThreadGroupSizeZ));
                    LogPhase("dispatch", "ok", "tick=" + tick);

                    LogPhase("readback", "start", "tick=" + tick);
                    DeltaSnapshot[] tickDeltas = ReadDeltas(deltas, deltaCounter, deltas.count);
                    ticks[tick - 1] = new TickSnapshot(tick, tickDeltas, appliedWords, materialHeader, materialReceipts);
                    LogPhase("readback", "ok", "tick=" + tick + " deltas=" + tickDeltas.Length);
                    Swap(ref currentCells, ref nextCells);
                    Swap(ref currentAtmosphericFields, ref nextAtmosphericFields);
                }

                uint[] finalRawCells = new uint[cellCount];
                currentCells.GetData(finalRawCells);
                uint[] finalAtmosphericFields = new uint[cellCount];
                currentAtmosphericFields.GetData(finalAtmosphericFields);
                uint[] finalCompanionFields = new uint[cellCount];
                companionFields.GetData(finalCompanionFields);
                float[] visualSamples = new float[cellCount * 4];
                visualFields.GetData(visualSamples);
                return new Snapshot(
                    finalRawCells.ConvertAll(cell => (ushort)(cell & 0xFFFFu)),
                    finalAtmosphericFields,
                    finalCompanionFields,
                    ticks,
                    VisualChecksum(visualSamples), material.ReadTargets(), material.ReadSlots());
            }
            finally
            {
                Release(currentCells);
                Release(nextCells);
                Release(externalChanges);
                Release(deltas);
                Release(visualFields);
                Release(currentAtmosphericFields);
                Release(nextAtmosphericFields);
                Release(companionFields);
                Release(deltaCounter);
                material?.Dispose();
            }
        }

        private static void Bind(
            ComputeShader shader,
            int kernel,
            Fixture fixture,
            int tick,
            ComputeBuffer currentCells,
            ComputeBuffer nextCells,
            ComputeBuffer externalChanges,
            ComputeBuffer deltas,
            ComputeBuffer visualFields,
            ComputeBuffer currentAtmosphericFields,
            ComputeBuffer nextAtmosphericFields,
            ComputeBuffer companionFields)
        {
            int cellCount = checked(fixture.grid.width * fixture.grid.height * fixture.grid.depth);
            shader.SetInt("Width", fixture.grid.width);
            shader.SetInt("Height", fixture.grid.height);
            shader.SetInt("Depth", fixture.grid.depth);
            shader.SetInt("CellCount", cellCount);
            shader.SetInt("Tick", tick);
            shader.SetInt("Seed", unchecked((int)fixture.seed));
            shader.SetInt("ChangeCount", 0);
            FixtureWind wind = fixture.wind ?? FixtureWind.None;
            shader.SetFloat("WindDirectionX", wind.directionX);
            shader.SetFloat("WindDirectionY", wind.directionY);
            shader.SetFloat("WindStrength", wind.strength);
            BindParameters(shader, fixture.parameters);
            shader.SetBuffer(kernel, "CurrentCells", currentCells);
            shader.SetBuffer(kernel, "NextCells", nextCells);
            shader.SetBuffer(kernel, "ExternalChanges", externalChanges);
            shader.SetBuffer(kernel, "Deltas", deltas);
            shader.SetBuffer(kernel, "VisualFields", visualFields);
            shader.SetBuffer(kernel, "CurrentAtmosphericFields", currentAtmosphericFields);
            shader.SetBuffer(kernel, "NextAtmosphericFields", nextAtmosphericFields);
            shader.SetBuffer(kernel, "CompanionFields", companionFields);
        }

        private static void BindParameters(ComputeShader shader, FixtureParameters parameters)
        {
            shader.SetFloat("VisualFireBaseIntensity", parameters.visualFireBaseIntensity);
            shader.SetFloat("VisualFireHeatWeight", parameters.visualFireHeatWeight);
            shader.SetFloat("VisualSmokeBaseIntensity", parameters.visualSmokeBaseIntensity);
            shader.SetFloat("VisualSmokeFuelWeight", parameters.visualSmokeFuelWeight);
            shader.SetFloat("VisualSmokeHeatWeight", parameters.visualSmokeHeatWeight);
            shader.SetFloat("VisualAshBaseIntensity", parameters.ashPresentationBaseIntensity);
            shader.SetFloat("VisualAshFuelWeight", parameters.ashPresentationFuelWeight);
            shader.SetFloat("VisualAshHeatWeight", parameters.ashPresentationHeatWeight);
            shader.SetFloat("VisualVisibilityHeatWeight", parameters.visualVisibilityHeatWeight);
            shader.SetFloat("VisualVisibilitySmokeWeight", parameters.visualVisibilitySmokeWeight);
            shader.SetFloat("VisualVisibilityAshWeight", parameters.ashPresentationVisibilityWeight);
            shader.SetInt("FireIgnitionBaseHeat", unchecked((int)parameters.ignitionPoint));
            shader.SetInt("FireWaterIgnitionPenalty", unchecked((int)parameters.fireWaterIgnitionPenalty));
            shader.SetInt("FireWaterFuelLock", 2);
            shader.SetInt("FireWaterEvaporationHeat", 2);
            shader.SetInt("FireFlammabilityBurnPressure", 2);
            shader.SetInt("FireWaterBurnPressurePenalty", 0);
            shader.SetInt("FireBurnHeatBase", unchecked((int)parameters.fireBurnHeatBase));
            shader.SetInt("FireFuelHeatWeight", unchecked((int)parameters.fireFuelHeatWeight));
            shader.SetInt("FireCoolingBase", 0);
            shader.SetInt("FireFuelBurnDownPressureNumerator", unchecked((int)parameters.fireFuelBurnDownPressureNumerator));
            shader.SetInt("FireFuelBurnDownPressureDenominator", unchecked((int)parameters.fireFuelBurnDownPressureDenominator));
            shader.SetInt("FireFuelBurnDownRollSeed", unchecked((int)parameters.fireFuelBurnDownRollSeed));
        }

        private static uint[] CellArrayOrZeros(uint[] values, int cellCount, string fieldName)
        {
            if (values == null || values.Length == 0)
            {
                return new uint[cellCount];
            }

            if (values.Length != cellCount)
            {
                throw new InvalidOperationException(
                    "Fixture " + fieldName + " count " + values.Length + " did not match grid cell count " + cellCount + ".");
            }

            return values;
        }

        private static DeltaSnapshot[] ReadDeltas(ComputeBuffer deltas, ComputeBuffer deltaCounter, int capacity)
        {
            uint[] counter = new uint[1];
            ComputeBuffer.CopyCount(deltas, deltaCounter, 0);
            deltaCounter.GetData(counter);
            if (counter[0] > capacity)
            {
                throw new InvalidOperationException("Delta append counter returned " + counter[0] + " for capacity " + capacity + ".");
            }

            int deltaCount = checked((int)counter[0]);
            if (deltaCount == 0)
            {
                return new DeltaSnapshot[0];
            }

            CellDeltaGpu[] raw = new CellDeltaGpu[deltaCount];
            deltas.GetData(raw, 0, 0, deltaCount);
            DeltaSnapshot[] snapshots = new DeltaSnapshot[deltaCount];
            for (int index = 0; index < deltaCount; index += 1)
            {
                snapshots[index] = new DeltaSnapshot(
                    checked((int)raw[index].Index),
                    (ushort)(raw[index].OldCell & 0xFFFFu),
                    (ushort)(raw[index].NewCell & 0xFFFFu), raw[index].Reserved);
            }

            return snapshots;
        }

        private static string VisualChecksum(float[] samples)
        {
            uint hash = 2166136261u;
            for (int index = 0; index < samples.Length; index += 1)
            {
                byte[] sampleBytes = BitConverter.GetBytes(samples[index]);
                for (int byteIndex = 0; byteIndex < sampleBytes.Length; byteIndex += 1)
                {
                    hash ^= sampleBytes[byteIndex];
                    hash *= 16777619u;
                }
            }

            return "visual-fnv1a32:" + hash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static int Groups(int dimension, int groupSize)
        {
            return (dimension + groupSize - 1) / groupSize;
        }

        private static void Swap(ref ComputeBuffer left, ref ComputeBuffer right)
        {
            ComputeBuffer oldLeft = left;
            left = right;
            right = oldLeft;
        }

        private static void Release(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
            }
        }

        private static void LogPhase(string phase, string status, string detail)
        {
            Debug.Log("wildfire_shader_harness phase=" + phase + " status=" + status + " " + detail);
        }

        private static void ConfigureLogging()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class HarnessArguments
    {
        public string FixturePath;
        public string ShaderPath;
        public string OutputPath;
        public int TickCount;

        public static HarnessArguments Parse(string[] args)
        {
            string fixture = ValueAfter(args, "--fixture");
            string shader = ValueAfter(args, "--shader");
            string output = ValueAfter(args, "--output");
            string tickText = ValueAfter(args, "--ticks");
            int ticks;
            if (!int.TryParse(tickText, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) || ticks <= 0)
            {
                throw new ArgumentException("--ticks must be a positive integer.");
            }

            return new HarnessArguments
            {
                FixturePath = fixture,
                ShaderPath = shader,
                OutputPath = output,
                TickCount = ticks,
            };
        }

        private static string ValueAfter(string[] args, string name)
        {
            for (int index = 0; index < args.Length - 1; index += 1)
            {
                if (args[index] == name && !string.IsNullOrEmpty(args[index + 1]))
                {
                    return args[index + 1];
                }
            }

            throw new ArgumentException("Missing required argument " + name + ".");
        }
    }

    [Serializable]
    internal sealed class Fixture
    {
        public int formatVersion;
        public string scenario;
        public uint seed;
        public FixtureGrid grid;
        public FixtureLayer selectedLayer;
        public PackedCellValues packedCellValues;
        public uint[] initialAtmosphericFields;
        public uint[] companionFields;
        public uint[] initialTargetIds;
        public uint[] initialSlotIds;
        public FixtureMaterialHandoff[] materialHandoffs;
        public FixtureWind wind;
        public FixtureParameters parameters;
        public FixtureExternalChanges[] externalChanges;

        public void ValidateExternalChanges(int cellCount, int tickCount)
        {
            var seenTicks = new System.Collections.Generic.HashSet<int>();
            foreach (FixtureExternalChanges batch in externalChanges ?? new FixtureExternalChanges[0])
            {
                if (batch.tick <= 0 || batch.tick > tickCount || !seenTicks.Add(batch.tick))
                {
                    throw new InvalidOperationException("External changes require one batch per tick within the requested capture.");
                }

                if (batch.words == null || batch.words.Length % 4 != 0 || batch.words.Length / 4 > cellCount)
                {
                    throw new InvalidOperationException("External change words exceed upload capacity or contain a partial command.");
                }

                for (int offset = 0; offset < batch.words.Length; offset += 4)
                {
                    if (batch.words[offset] >= cellCount)
                    {
                        throw new InvalidOperationException("External change cell index is outside the fixture grid.");
                    }
                }
            }
        }

        public FireSimChangeGpu[] ChangesForTick(int tick)
        {
            foreach (FixtureExternalChanges batch in externalChanges ?? new FixtureExternalChanges[0])
            {
                if (batch.tick != tick)
                {
                    continue;
                }

                FireSimChangeGpu[] changes = new FireSimChangeGpu[batch.words.Length / 4];
                for (int index = 0; index < changes.Length; index++)
                {
                    int offset = index * 4;
                    changes[index] = new FireSimChangeGpu
                    {
                        CellIndex = batch.words[offset],
                        SetMask = batch.words[offset + 1],
                        AddFields = batch.words[offset + 2],
                        SetValues = batch.words[offset + 3],
                    };
                }

                return changes;
            }

            return new FireSimChangeGpu[0];
        }

        public static Fixture Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Fixture JSON was not found.", path);
            }

            Fixture fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(path, Encoding.UTF8));
            if (fixture == null || fixture.formatVersion != 1 || fixture.grid == null || fixture.packedCellValues == null || fixture.parameters == null)
            {
                throw new InvalidOperationException("Fixture JSON is missing required shader snapshot fields or parameters; export it with the current fixture serializer.");
            }

            return fixture;
        }
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    internal struct FireSimChangeGpu
    {
        public uint CellIndex;
        public uint SetMask;
        public uint AddFields;
        public uint SetValues;
    }

    [Serializable]
    internal sealed class FixtureExternalChanges
    {
        public int tick;
        public uint[] words;
    }

    [Serializable]
    internal sealed class FixtureParameters
    {
        public float visualFireBaseIntensity;
        public float visualFireHeatWeight;
        public float visualSmokeBaseIntensity;
        public float visualSmokeFuelWeight;
        public float visualSmokeHeatWeight;
        public float ashPresentationBaseIntensity;
        public float ashPresentationFuelWeight;
        public float ashPresentationHeatWeight;
        public float visualVisibilityHeatWeight;
        public float visualVisibilitySmokeWeight;
        public float ashPresentationVisibilityWeight;
        public uint ignitionPoint;
        public uint fireWaterIgnitionPenalty;
        public uint fireBurnHeatBase;
        public uint fireFuelHeatWeight;
        public uint fireFuelBurnDownPressureNumerator;
        public uint fireFuelBurnDownPressureDenominator;
        public uint fireFuelBurnDownRollSeed;
    }

    [Serializable]
    internal sealed class FixtureGrid
    {
        public int width;
        public int height;
        public int depth;
    }

    [Serializable]
    internal sealed class FixtureLayer
    {
        public int index;
        public int offset;
        public int cellCount;
    }

    [Serializable]
    internal sealed class PackedCellValues
    {
        public string valueType;
        public string indexOrder;
        public ushort[] values;
    }

    [Serializable]
    internal sealed class FixtureWind
    {
        public static readonly FixtureWind None = new FixtureWind
        {
            directionX = 0f,
            directionY = 0f,
            strength = 0f,
        };

        public float directionX;
        public float directionY;
        public float strength;
    }

    internal sealed class Snapshot
    {
        private readonly ushort[] finalPackedCells;
        private readonly uint[] finalAtmosphericFields;
        private readonly uint[] finalCompanionFields;
        private readonly TickSnapshot[] ticks;
        private readonly string visualChecksum;
        private readonly uint[] finalTargetIds, finalSlotIds;

        public Snapshot(ushort[] finalPackedCells, uint[] finalAtmosphericFields, uint[] finalCompanionFields, TickSnapshot[] ticks, string visualChecksum, uint[] finalTargetIds, uint[] finalSlotIds)
        {
            this.finalPackedCells = finalPackedCells;
            this.finalAtmosphericFields = finalAtmosphericFields;
            this.finalCompanionFields = finalCompanionFields;
            this.ticks = ticks;
            this.visualChecksum = visualChecksum;
            this.finalTargetIds = finalTargetIds;
            this.finalSlotIds = finalSlotIds;
        }

        public void Write(string path, Fixture fixture, int tickCount)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, ToJson(fixture, tickCount), new UTF8Encoding(false));
        }

        private string ToJson(Fixture fixture, int tickCount)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("{");
            builder.AppendLine("  \"formatVersion\": 1,");
            builder.AppendLine("  \"scenario\": \"" + Escape(fixture.scenario) + "\",");
            builder.AppendLine("  \"seed\": " + fixture.seed.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("  \"grid\": {");
            builder.AppendLine("    \"width\": " + fixture.grid.width.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("    \"height\": " + fixture.grid.height.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("    \"depth\": " + fixture.grid.depth.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine("  },");
            builder.AppendLine("  \"tickCount\": " + tickCount.ToString(CultureInfo.InvariantCulture) + ",");
            AppendUshortArray(builder, "finalPackedCells", finalPackedCells, indent: "  ");
            builder.AppendLine(",");
            AppendUintArray(builder, "finalAtmosphericFields", finalAtmosphericFields, indent: "  ");
            builder.AppendLine(",");
            AppendUintArray(builder, "finalCompanionFields", finalCompanionFields, indent: "  ");
            builder.AppendLine(",");
            AppendUintArray(builder, "finalTargetIds", finalTargetIds, indent: "  ");
            builder.AppendLine(",");
            AppendUintArray(builder, "finalSlotIds", finalSlotIds, indent: "  ");
            builder.AppendLine(",");
            builder.AppendLine("  \"perTickDeltaCounts\": [");
            for (int index = 0; index < ticks.Length; index += 1)
            {
                builder.Append("    " + ticks[index].Deltas.Length.ToString(CultureInfo.InvariantCulture));
                builder.AppendLine(index + 1 == ticks.Length ? string.Empty : ",");
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"perTickDeltas\": [");
            for (int index = 0; index < ticks.Length; index += 1)
            {
                ticks[index].AppendJson(builder, "    ");
                builder.AppendLine(index + 1 == ticks.Length ? string.Empty : ",");
            }

            builder.AppendLine("  ],");
            builder.AppendLine("  \"visual\": {");
            builder.AppendLine("    \"checksum\": \"" + visualChecksum + "\"");
            builder.AppendLine("  }");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendUshortArray(StringBuilder builder, string name, ushort[] values, string indent)
        {
            builder.AppendLine(indent + "\"" + name + "\": [");
            for (int index = 0; index < values.Length; index += 1)
            {
                builder.Append(indent + "  " + values[index].ToString(CultureInfo.InvariantCulture));
                builder.AppendLine(index + 1 == values.Length ? string.Empty : ",");
            }

            builder.Append(indent + "]");
        }

        internal static void AppendUintArray(StringBuilder builder, string name, uint[] values, string indent)
        {
            builder.AppendLine(indent + "\"" + name + "\": [");
            for (int index = 0; index < values.Length; index += 1)
            {
                builder.Append(indent + "  " + values[index].ToString(CultureInfo.InvariantCulture));
                builder.AppendLine(index + 1 == values.Length ? string.Empty : ",");
            }

            builder.Append(indent + "]");
        }

        private static string Escape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal sealed class TickSnapshot
    {
        public readonly int Tick;
        public readonly DeltaSnapshot[] Deltas;
        public readonly uint[] AppliedChangeWords;
        public readonly uint[] MaterialHeader, MaterialReceipts;

        public TickSnapshot(int tick, DeltaSnapshot[] deltas, uint[] appliedChangeWords, uint[] materialHeader, uint[] materialReceipts)
        {
            Tick = tick;
            Deltas = deltas;
            AppliedChangeWords = appliedChangeWords;
            MaterialHeader = materialHeader;
            MaterialReceipts = materialReceipts;
        }

        public void AppendJson(StringBuilder builder, string indent)
        {
            builder.AppendLine(indent + "{");
            builder.AppendLine(indent + "  \"tick\": " + Tick.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"deltaCount\": " + Deltas.Length.ToString(CultureInfo.InvariantCulture) + ",");
            builder.Append(indent + "  \"appliedChangeWords\": [");
            for (int i = 0; i < AppliedChangeWords.Length; i++)
            {
                if (i > 0) builder.Append(",");
                builder.Append(AppliedChangeWords[i].ToString(CultureInfo.InvariantCulture));
            }
            builder.AppendLine("],");
            Snapshot.AppendUintArray(builder, "materialHeader", MaterialHeader, indent + "  ");
            builder.AppendLine(",");
            Snapshot.AppendUintArray(builder, "materialReceipts", MaterialReceipts, indent + "  ");
            builder.AppendLine(",");
            builder.AppendLine(indent + "  \"deltas\": [");
            for (int index = 0; index < Deltas.Length; index += 1)
            {
                Deltas[index].AppendJson(builder, indent + "    ");
                builder.AppendLine(index + 1 == Deltas.Length ? string.Empty : ",");
            }

            builder.AppendLine(indent + "  ]");
            builder.Append(indent + "}");
        }
    }

    internal sealed class DeltaSnapshot
    {
        private readonly int cellIndex;
        private readonly ushort oldCell;
        private readonly ushort newCell;
        private readonly uint targetId;

        public DeltaSnapshot(int cellIndex, ushort oldCell, ushort newCell, uint targetId)
        {
            this.cellIndex = cellIndex;
            this.oldCell = oldCell;
            this.newCell = newCell;
            this.targetId = targetId;
        }

        public void AppendJson(StringBuilder builder, string indent)
        {
            builder.AppendLine(indent + "{");
            builder.AppendLine(indent + "  \"cellIndex\": " + cellIndex.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"oldCell\": " + oldCell.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"newCell\": " + newCell.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"targetId\": " + targetId.ToString(CultureInfo.InvariantCulture));
            builder.Append(indent + "}");
        }
    }

    internal struct CellDeltaGpu
    {
        public uint Index;
        public uint OldCell;
        public uint NewCell;
        public uint Reserved;
    }

    internal static class ArrayExtensions
    {
        public static TResult[] ConvertAll<TSource, TResult>(this TSource[] values, Func<TSource, TResult> convert)
        {
            TResult[] result = new TResult[values.Length];
            for (int index = 0; index < values.Length; index += 1)
            {
                result[index] = convert(values[index]);
            }

            return result;
        }
    }
}
