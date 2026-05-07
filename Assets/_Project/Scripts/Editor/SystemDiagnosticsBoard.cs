#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Hecton8.Core;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Manual Odin diagnostics asset for registry and telemetry snapshot inspection.
    /// </summary>
    [CreateAssetMenu(fileName = "SystemDiagnosticsBoard", menuName = "HECTON8/Diagnostics/System Diagnostics Board")]
    public sealed class SystemDiagnosticsBoard : ScriptableObject
    {
        [Serializable]
        private struct RegistryEntrySnapshot
        {
            [TableColumnWidth(48, Resizable = false)]
            public int Index;

            [TableColumnWidth(120)]
            public string Lane;

            [TableColumnWidth(220)]
            public string Type;

            [TableColumnWidth(220)]
            public string Owner;
        }

        [Serializable]
        private struct TelemetrySnapshotRow
        {
            [TableColumnWidth(64, Resizable = false)]
            public uint Frame;

            [TableColumnWidth(120)]
            public string Systems;

            [TableColumnWidth(72, Resizable = false)]
            public float Dt;

            [TableColumnWidth(72, Resizable = false)]
            public float LatencyMs;

            [TableColumnWidth(72, Resizable = false)]
            public float GpuMs;

            [TableColumnWidth(88, Resizable = false)]
            public float ReservedMb;

            [TableColumnWidth(180)]
            public Vector3 PlayerAup;

            [TableColumnWidth(96)]
            public string ErrorFlags;

            [TableColumnWidth(96)]
            public string ExportReason;
        }

        // COLD ALLOC: List<RegistryEntrySnapshot>[128] - editor updatable registry snapshot rows - owner: SystemDiagnosticsBoard
        private readonly List<RegistryEntrySnapshot> _updatableRows = new List<RegistryEntrySnapshot>(128);
        // COLD ALLOC: List<RegistryEntrySnapshot>[64] - editor renderable registry snapshot rows - owner: SystemDiagnosticsBoard
        private readonly List<RegistryEntrySnapshot> _renderableRows = new List<RegistryEntrySnapshot>(64);
        // COLD ALLOC: List<CrashTelemetryBuffer.EditorSnapshotEntry>[50] - temporary crash telemetry snapshot buffer - owner: SystemDiagnosticsBoard
        private readonly List<CrashTelemetryBuffer.EditorSnapshotEntry> _telemetryScratch = new List<CrashTelemetryBuffer.EditorSnapshotEntry>(50);
        // COLD ALLOC: List<TelemetrySnapshotRow>[50] - formatted crash telemetry dashboard rows - owner: SystemDiagnosticsBoard
        private readonly List<TelemetrySnapshotRow> _telemetryRows = new List<TelemetrySnapshotRow>(50);

        [ShowInInspector, ReadOnly, PropertySpace(SpaceBefore = 4f)]
        private string LastRefreshUtc { get; set; } = "Never";

        [ShowInInspector, ReadOnly]
        private int UpdatableCount { get; set; }

        [ShowInInspector, ReadOnly]
        private int RenderableCount { get; set; }

        [ShowInInspector, ReadOnly]
        private int TelemetryFrameCount { get; set; }

        [ShowInInspector, TableList(AlwaysExpanded = true), PropertySpace(SpaceBefore = 10f)]
        private List<RegistryEntrySnapshot> Updatables => _updatableRows;

        [ShowInInspector, TableList(AlwaysExpanded = true), PropertySpace(SpaceBefore = 10f)]
        private List<RegistryEntrySnapshot> Renderables => _renderableRows;

        [ShowInInspector, TableList(AlwaysExpanded = true), PropertySpace(SpaceBefore = 10f)]
        private List<TelemetrySnapshotRow> CrashTelemetry => _telemetryRows;

        [Button("Refresh Snapshot")]
        private void RefreshSnapshot()
        {
            RefreshRegistryBucket(GlobalRegistry.Updatables, _updatableRows, true);
            RefreshRegistryBucket(GlobalRegistry.Renderables, _renderableRows, false);
            RefreshTelemetryRows();

            UpdatableCount = _updatableRows.Count;
            RenderableCount = _renderableRows.Count;
            TelemetryFrameCount = _telemetryRows.Count;
            LastRefreshUtc = DateTime.UtcNow.ToString("u");

            EditorUtility.SetDirty(this);
        }

        private void RefreshRegistryBucket<T>(RegistryBucket<T> bucket, List<RegistryEntrySnapshot> destination, bool includeLane) where T : class
        {
            destination.Clear();
            if (bucket == null)
                return;

            T[] rawArray = bucket.RawArray;
            int count = bucket.Count;
            for (int i = 0; i < count; i++)
            {
                T entry = rawArray[i];
                if (entry == null)
                    continue;

                RegistryEntrySnapshot snapshot = default;
                snapshot.Index = i;
                snapshot.Lane = includeLane ? ResolveLane(entry as IUpdatable) : "N/A";
                snapshot.Type = entry.GetType().FullName;
                snapshot.Owner = ResolveOwnerName(entry);
                destination.Add(snapshot);
            }
        }

        private void RefreshTelemetryRows()
        {
            _telemetryScratch.Clear();
            _telemetryRows.Clear();

            CrashTelemetryBuffer telemetry = UnityEngine.Object.FindAnyObjectByType<CrashTelemetryBuffer>(FindObjectsInactive.Include);
            if (telemetry == null)
                return;

            telemetry.CopyEditorSnapshot(_telemetryScratch);
            int count = _telemetryScratch.Count;
            for (int i = 0; i < count; i++)
            {
                CrashTelemetryBuffer.EditorSnapshotEntry entry = _telemetryScratch[i];
                TelemetrySnapshotRow row = default;
                row.Frame = entry.FrameIndex;
                row.Systems = ResolveSystemMask(entry.SystemMask);
                row.Dt = entry.DeltaTime;
                row.LatencyMs = entry.LatencyMs;
                row.GpuMs = entry.GpuFrameTime;
                row.ReservedMb = entry.MemoryUsedMb;
                row.PlayerAup = entry.PlayerAup;
                row.ErrorFlags = $"0x{entry.ErrorFlags:X8}";
                row.ExportReason = entry.ExportReason == 0u ? "None" : $"0x{entry.ExportReason:X8}";
                _telemetryRows.Add(row);
            }
        }

        private static string ResolveOwnerName(object entry)
        {
            if (entry is Component component)
                return component.gameObject.name;

            return entry.GetType().Name;
        }

        private static string ResolveLane(IUpdatable updatable)
        {
            if (updatable == null)
                return "N/A";

            if (ContainsLaneEntry(SystemDispatcher.GetLane(PriorityLayer.Core), updatable))
                return nameof(PriorityLayer.Core);
            if (ContainsLaneEntry(SystemDispatcher.GetLane(PriorityLayer.Environment), updatable))
                return nameof(PriorityLayer.Environment);
            if (ContainsLaneEntry(SystemDispatcher.GetLane(PriorityLayer.Player), updatable))
                return nameof(PriorityLayer.Player);
            if (ContainsLaneEntry(SystemDispatcher.GetLane(PriorityLayer.UI), updatable))
                return nameof(PriorityLayer.UI);

            return "Untracked";
        }

        private static bool ContainsLaneEntry(RegistryBucket<IUpdatable> lane, IUpdatable target)
        {
            if (lane == null || target == null)
                return false;

            IUpdatable[] rawArray = lane.RawArray;
            int count = lane.Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(rawArray[i], target))
                    return true;
            }

            return false;
        }

        private static string ResolveSystemMask(uint mask)
        {
            if (mask == 0u)
                return "None";

            string result = string.Empty;
            AppendSystemName(ref result, mask, 1u << 0, "Physics");
            AppendSystemName(ref result, mask, 1u << 1, "Voxel");
            AppendSystemName(ref result, mask, 1u << 2, "AI");
            AppendSystemName(ref result, mask, 1u << 3, "Fluid");
            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }

        private static void AppendSystemName(ref string current, uint mask, uint bit, string name)
        {
            if ((mask & bit) == 0u)
                return;

            current = string.IsNullOrEmpty(current) ? name : current + "|" + name;
        }
    }
}
#endif
