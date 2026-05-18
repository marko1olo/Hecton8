#if UNITY_EDITOR
using System.IO;
using Hecton8.Core.Contracts.Signals;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public sealed class SignalTrafficMonitorWindow : EditorWindow
    {
        private const int TelemetryCapacity = 256;
        private const float RowHeight = 18f;
        private const float BarHeight = 10f;
        private const int StressWarningCount = 1000;

        private NativeArray<SignalLaneTelemetry> _telemetry;
        private Vector2 _scroll;
        private InjectKind _injectKind;
        private float _x;
        private float _y;
        private float _z;
        private float _magnitude = 1f;
        private uint _entityId = 1u;
        private string _surfaceName = "steel";

        private enum InjectKind
        {
            MockDamage,
            MockFootstep,
            CombatDamage
        }

        [MenuItem("Hecton8/Diagnostics/Signal Traffic Monitor")]
        public static void Open()
        {
            GetWindow<SignalTrafficMonitorWindow>("Signal Traffic");
        }

        private void OnEnable()
        {
            EnsureTelemetry();
            EditorApplication.update -= Repaint;
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            if (_telemetry.IsCreated)
                _telemetry.Dispose();
        }

        private void OnGUI()
        {
            EnsureTelemetry();
            DrawInjectionControls();
            EditorGUILayout.Space(6f);
            DrawTelemetry();
        }

        private void EnsureTelemetry()
        {
            if (_telemetry.IsCreated)
                return;

            _telemetry = new NativeArray<SignalLaneTelemetry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        }

        private void DrawInjectionControls()
        {
            EditorGUILayout.LabelField("Injection", EditorStyles.boldLabel);
            _injectKind = (InjectKind)EditorGUILayout.EnumPopup("Lane", _injectKind);
            _x = EditorGUILayout.FloatField("AUP X", _x);
            _y = EditorGUILayout.FloatField("AUP Y", _y);
            _z = EditorGUILayout.FloatField("AUP Z", _z);
            _magnitude = math.max(0f, EditorGUILayout.FloatField("Magnitude", _magnitude));
            _entityId = (uint)math.max(1, EditorGUILayout.IntField("Entity Id", unchecked((int)_entityId)));
            _surfaceName = EditorGUILayout.TextField("Surface", _surfaceName);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Push Signal", GUILayout.Height(24f)))
                PushSelectedSignal();

            if (GUILayout.Button("Load CSV Priorities", GUILayout.Height(24f)))
            {
                string csvPath = Path.Combine(Application.dataPath, "StreamingAssets", "signal_priorities.csv");
                SignalPriorityCsvHotSwap.TryLoad(csvPath);
            }

            if (GUILayout.Button("Dump Black Box", GUILayout.Height(24f)))
                SignalTelemetryRingBuffer.DumpToDisk();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawTelemetry()
        {
            int count = SignalBusRegistry.CopyTelemetry(_telemetry);
            EditorGUILayout.LabelField("Lanes", EditorStyles.boldLabel);
            Rect header = EditorGUILayout.GetControlRect(false, RowHeight);
            EditorGUI.LabelField(new Rect(header.x, header.y, 100f, RowHeight), "Hash");
            EditorGUI.LabelField(new Rect(header.x + 110f, header.y, 80f, RowHeight), "Queued");
            EditorGUI.LabelField(new Rect(header.x + 200f, header.y, 80f, RowHeight), "Frame");
            EditorGUI.LabelField(new Rect(header.x + 290f, header.y, 80f, RowHeight), "Dropped");
            EditorGUI.LabelField(new Rect(header.x + 380f, header.y, header.width - 380f, RowHeight), "Load");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < count; i++)
                DrawTelemetryRow(_telemetry[i]);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawTelemetryRow(SignalLaneTelemetry telemetry)
        {
            bool warning = telemetry.QueuedBeforeFlush > StressWarningCount ||
                           telemetry.SnapshotCount > StressWarningCount ||
                           telemetry.DroppedCount > 0;
            Color barColor = warning ? new Color(0.8f, 0.1f, 0.05f, 0.85f) : new Color(0.1f, 0.55f, 0.25f, 0.85f);
            Rect row = EditorGUILayout.GetControlRect(false, RowHeight);
            EditorGUI.LabelField(new Rect(row.x, row.y, 100f, RowHeight), telemetry.LaneHash.ToString("X8"));
            EditorGUI.LabelField(new Rect(row.x + 110f, row.y, 80f, RowHeight), telemetry.QueuedBeforeFlush.ToString());
            EditorGUI.LabelField(new Rect(row.x + 200f, row.y, 80f, RowHeight), telemetry.SnapshotCount.ToString());
            EditorGUI.LabelField(new Rect(row.x + 290f, row.y, 80f, RowHeight), telemetry.DroppedCount.ToString());

            float scale = math.saturate(math.max(telemetry.QueuedBeforeFlush, telemetry.SnapshotCount) / (float)StressWarningCount);
            Rect bar = new Rect(row.x + 380f, row.y + 4f, math.max(2f, (row.width - 390f) * scale), BarHeight);
            EditorGUI.DrawRect(bar, barColor);
        }

        private void PushSelectedSignal()
        {
            double3 aup = new double3(_x, _y, _z);
            switch (_injectKind)
            {
                case InjectKind.MockDamage:
                {
                    SignalWardenMockDamageSignal signal = default;
                    signal.Aup = aup;
                    signal.Normal = new float3(0f, 1f, 0f);
                    signal.Damage = _magnitude;
                    signal.EntityId = _entityId;
                    signal.Flags = 1;
                    SignalBus<SignalWardenMockDamageSignal>.TryPush(in signal);
                    break;
                }
                case InjectKind.MockFootstep:
                {
                    MockPlayerFootstepSignal signal = default;
                    signal.Aup = aup;
                    signal.Normal = new float3(0f, 1f, 0f);
                    signal.Intensity01 = math.saturate(_magnitude);
                    signal.EntityId = _entityId;
                    signal.Frame = unchecked((uint)Time.frameCount);
                    signal.SurfaceName.Append(_surfaceName);
                    signal.Flags = 1;
                    SignalBus<MockPlayerFootstepSignal>.TryPush(in signal);
                    break;
                }
                case InjectKind.CombatDamage:
                {
                    CombatDamageSignal signal = default;
                    signal.WorldPoint = new float3(_x, _y, _z);
                    signal.Direction = new float3(0f, 1f, 0f);
                    signal.Magnitude = _magnitude;
                    signal.TargetHash = _entityId;
                    signal.Frame = unchecked((uint)Time.frameCount);
                    signal.Flags = CombatDamageSignal.DirectRuntimeFlag;
                    SignalBus<CombatDamageSignal>.TryPush(in signal);
                    break;
                }
            }
        }
    }
}
#endif
