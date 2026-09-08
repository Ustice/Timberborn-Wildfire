using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wildfire.UnityBatchmode
{
    [Serializable]
    internal sealed class FixtureMaterialHandoff
    {
        public int tick;
        public uint[] requests;
    }

    // Mirrors production buffer layouts; the fixture uploads the shared encoder's exact words.
    internal sealed class MaterialHandoffFixtureBuffers : IDisposable
    {
        private readonly Fixture fixture;
        private readonly int cellCount;
        private ComputeBuffer targets, slots, requests, receipts;
        private int requestCount;

        public MaterialHandoffFixtureBuffers(Fixture fixture, int cellCount, int tickCount)
        {
            this.fixture = fixture;
            this.cellCount = cellCount;
            int capacity = 1;
            var ticks = new HashSet<int>();
            foreach (var batch in fixture.materialHandoffs ?? new FixtureMaterialHandoff[0])
            {
                if (batch.tick <= 0 || batch.tick > tickCount || !ticks.Add(batch.tick) ||
                    batch.requests == null || batch.requests.Length == 0 || batch.requests.Length % 10 != 0 ||
                    batch.requests.Length / 10 > cellCount)
                    throw new InvalidOperationException("Invalid material fixture request batch.");
                capacity = Math.Max(capacity, batch.requests.Length / 10);
            }
            int receiptRows = checked(capacity + 1);
            _ = checked(receiptRows * 10);
            try
            {
                targets = new ComputeBuffer(cellCount, 4, ComputeBufferType.Structured);
                slots = new ComputeBuffer(cellCount, 4, ComputeBufferType.Structured);
                requests = new ComputeBuffer(capacity, 40, ComputeBufferType.Structured);
                receipts = new ComputeBuffer(receiptRows, 40, ComputeBufferType.Structured);
                targets.SetData(CellWords(fixture.initialTargetIds));
                slots.SetData(CellWords(fixture.initialSlotIds));
            }
            catch { Dispose(); throw; }
        }

        public void Upload(int tick)
        {
            requestCount = 0;
            receipts.SetData(new uint[10]);
            foreach (var batch in fixture.materialHandoffs ?? new FixtureMaterialHandoff[0])
                if (batch.tick == tick)
                {
                    requestCount = batch.requests.Length / 10;
                    requests.SetData(batch.requests);
                }
        }

        public void Bind(ComputeShader shader, int kernel)
        {
            shader.SetBuffer(kernel, "MaterialTargetIds", targets);
            shader.SetBuffer(kernel, "MaterialSlotIds", slots);
            shader.SetBuffer(kernel, "MaterialRequests", requests);
            shader.SetBuffer(kernel, "MaterialReceipts", receipts);
            shader.SetInt("MaterialRequestCapacity", requests.count);
        }

        public uint[] ReadHeader() => requestCount == 0 ? new uint[0] : Read(receipts, 10).Take(4).ToArray();
        public uint[] ReadReceipts() => requestCount == 0 ? new uint[0] : Read(receipts, checked((requestCount + 1) * 10)).Skip(10).ToArray();
        public uint[] ReadTargets() => Read(targets, cellCount);
        public uint[] ReadSlots() => Read(slots, cellCount);
        private static uint[] Read(ComputeBuffer buffer, int count)
        {
            var words = new uint[count];
            buffer.GetData(words, 0, 0, count);
            return words;
        }
        private uint[] CellWords(uint[] values)
        {
            if (values == null || values.Length == 0) return new uint[cellCount];
            if (values.Length != cellCount) throw new InvalidOperationException("Material identity cell count mismatch.");
            return values;
        }
        public void Dispose()
        {
            targets?.Release(); slots?.Release(); requests?.Release(); receipts?.Release();
        }
    }
}
