#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.World;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public sealed class AupTriggerAnalyticsWindow : EditorWindow
    {
        private const int GraphHeight = 96;
        private Label _summary;
        private Toggle[] _maskBits;
        private TelemetryGraph _graph;

        [MenuItem("Tools/HECTON-8/AUP Trigger Analytics")]
        public static void Open()
        {
            GetWindow<AupTriggerAnalyticsWindow>("AUP Trigger Analytics");
        }

        private void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;
            _summary = new Label("No DataVault telemetry.");
            rootVisualElement.Add(_summary);

            _graph = new TelemetryGraph { style = { height = GraphHeight, marginTop = 8, marginBottom = 8 } };
            rootVisualElement.Add(_graph);

            VisualElement maskRow = new VisualElement { style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap } };
            _maskBits = new Toggle[8];
            for (int i = 0; i < _maskBits.Length; i++)
            {
                int bit = i;
                Toggle toggle = new Toggle("Bit " + bit) { style = { width = 74 } };
                toggle.RegisterValueChangedCallback(evt => WriteMaskBit(bit, evt.newValue));
                _maskBits[i] = toggle;
                maskRow.Add(toggle);
            }

            rootVisualElement.Add(maskRow);
            AupTriggerDebugGizmo.Enabled = true;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            Refresh();
        }

        private void Refresh()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryReadTelemetry(vault, out NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, out int cursor) ||
                !TryReadCounters(vault, out NativeArray<int> counters))
            {
                _summary.text = "No DataVault telemetry.";
                _graph.SetTelemetry(default, 0);
                return;
            }

            int poiCount = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount);
            int evaluated = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastEvaluatedPoiCount);
            int signals = ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastSignalsEmitted);
            uint flags = unchecked((uint)ReadCounter(counters, AupNarrativePoiRuntimeConstants.CounterSlot.LastTelemetryFlags));
            _summary.text = $"POI {poiCount} | evaluated {evaluated} | signals {signals} | flags 0x{flags:X8}";
            _graph.SetTelemetry(telemetry, cursor);

            if (TryReadStateMask(vault, out ulong mask))
            {
                for (int i = 0; i < _maskBits.Length; i++)
                    _maskBits[i].SetValueWithoutNotify((mask & (1UL << i)) != 0UL);
            }
        }

        private static int ReadCounter(NativeArray<int> counters, AupNarrativePoiRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }

        private static bool TryReadTelemetry(IDataVault vault, out NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            if (vault == null ||
                !vault.TryGetGenerationHandle<AupNarrativeTriggerTelemetryEntry>(BufferID.NarrativePoiTelemetryRing, out VaultGenerationHandle<AupNarrativeTriggerTelemetryEntry> handle) ||
                !vault.TryReadHandle(in handle, out telemetry) ||
                !telemetry.IsCreated)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiTelemetryCursor, out VaultGenerationHandle<int> cursorHandle) &&
                vault.TryReadHandle(in cursorHandle, out NativeArray<int> cursorBuffer) &&
                cursorBuffer.IsCreated &&
                cursorBuffer.Length > 0)
            {
                cursor = cursorBuffer[0];
            }

            return true;
        }

        private static bool TryReadCounters(IDataVault vault, out NativeArray<int> counters)
        {
            counters = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiCounters, out VaultGenerationHandle<int> handle) &&
                   vault.TryReadHandle(in handle, out counters) &&
                   counters.IsCreated;
        }

        private static bool TryReadStateMask(IDataVault vault, out ulong mask)
        {
            mask = 0UL;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.NarrativePoiStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return false;
            }

            mask = masks[0];
            return true;
        }

        private static void WriteMaskBit(int bit, bool enabled)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.NarrativePoiStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryResolveHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return;
            }

            ulong mask = masks[0];
            ulong bitValue = 1UL << bit;
            masks[0] = enabled ? mask | bitValue : mask & ~bitValue;
        }

        private sealed class TelemetryGraph : VisualElement
        {
            private NativeArray<AupNarrativeTriggerTelemetryEntry> _telemetry;
            private int _cursor;

            public TelemetryGraph()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            public void SetTelemetry(NativeArray<AupNarrativeTriggerTelemetryEntry> telemetry, int cursor)
            {
                _telemetry = telemetry;
                _cursor = cursor;
                MarkDirtyRepaint();
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Painter2D painter = context.painter2D;
                Rect rect = contentRect;
                painter.strokeColor = new Color(0.08f, 0.78f, 0.95f, 1f);
                painter.lineWidth = 1.5f;
                if (!_telemetry.IsCreated || _telemetry.Length <= 1 || rect.width <= 1f || rect.height <= 1f)
                    return;

                double maxMicros = 1.0d;
                for (int i = 0; i < _telemetry.Length; i++)
                    maxMicros = math.max(maxMicros, _telemetry[i].ExecutionTimeMicroseconds);

                int start = _cursor % _telemetry.Length;
                if (start < 0)
                    start += _telemetry.Length;

                for (int i = 0; i < _telemetry.Length; i++)
                {
                    int sampleIndex = start + i;
                    if (sampleIndex >= _telemetry.Length)
                        sampleIndex -= _telemetry.Length;

                    float x = rect.xMin + rect.width * (i / (float)(_telemetry.Length - 1));
                    float y = rect.yMax - rect.height * (float)math.saturate(_telemetry[sampleIndex].ExecutionTimeMicroseconds / maxMicros);
                    if (i == 0)
                        painter.BeginPath();

                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }

    [InitializeOnLoad]
    public static class AupTriggerDebugGizmo
    {
        public static bool Enabled;

        static AupTriggerDebugGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            if (!Enabled ||
                !TryReadPois(out NativeArray<NarrativePoiDTO> pois, out int count) ||
                count <= 0)
            {
                return;
            }

            double3 runtimeOrigin = GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3();
            ulong questMask = ReadQuestMask();
            int safeCount = math.min(count, pois.Length);
            for (int i = 0; i < safeCount; i++)
            {
                NarrativePoiDTO poi = pois[i];
                if ((poi.StateFlags & NarrativePoiStateFlags.Active) == 0u)
                    continue;

                bool hasPrereq = (questMask & poi.PrerequisiteBitmask) == poi.PrerequisiteBitmask;
                bool triggered = (poi.StateFlags & NarrativePoiStateFlags.Triggered) != 0u;
                Handles.color = triggered ? Color.green : hasPrereq ? Color.yellow : Color.red;
                float3 runtime = (float3)(poi.PoiAUP - runtimeOrigin);
                Vector3 center = new Vector3(runtime.x, runtime.y, runtime.z);
                float radius = math.max(0.01f, poi.TriggerRadiusMeters);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static bool TryReadPois(out NativeArray<NarrativePoiDTO> pois, out int count)
        {
            pois = default;
            count = 0;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<NarrativePoiDTO>(BufferID.NarrativePoiTriggers, out VaultGenerationHandle<NarrativePoiDTO> poiHandle) ||
                !vault.TryReadHandle(in poiHandle, out pois) ||
                !pois.IsCreated)
            {
                return false;
            }

            if (vault.TryGetGenerationHandle<int>(BufferID.NarrativePoiCounters, out VaultGenerationHandle<int> counterHandle) &&
                vault.TryReadHandle(in counterHandle, out NativeArray<int> counters) &&
                counters.IsCreated &&
                counters.Length > (int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount)
            {
                count = counters[(int)AupNarrativePoiRuntimeConstants.CounterSlot.PoiCount];
            }

            return true;
        }

        private static ulong ReadQuestMask()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle<ulong>(BufferID.QuestDagGlobalStateMasks, out VaultGenerationHandle<ulong> handle) ||
                !vault.TryReadHandle(in handle, out NativeArray<ulong> masks) ||
                !masks.IsCreated ||
                masks.Length <= 0)
            {
                return 0UL;
            }

            return masks[0];
        }
    }


}
#endif
