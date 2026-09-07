using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wildfire.UnityBatchmode
{
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
        private readonly uint slotId;

        public DeltaSnapshot(int cellIndex, ushort oldCell, ushort newCell, uint targetId, uint slotId)
        {
            this.cellIndex = cellIndex;
            this.oldCell = oldCell;
            this.newCell = newCell;
            this.targetId = targetId;
            this.slotId = slotId;
        }

        public void AppendJson(StringBuilder builder, string indent)
        {
            builder.AppendLine(indent + "{");
            builder.AppendLine(indent + "  \"cellIndex\": " + cellIndex.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"oldCell\": " + oldCell.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"newCell\": " + newCell.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"targetId\": " + targetId.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine(indent + "  \"slotId\": " + slotId.ToString(CultureInfo.InvariantCulture));
            builder.Append(indent + "}");
        }
    }

}
