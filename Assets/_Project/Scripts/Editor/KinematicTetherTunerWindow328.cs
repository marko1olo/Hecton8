#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Physics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{

    public unsafe sealed class KinematicTetherTunerWindow328 : EditorWindow
    {
        private Slider _tensionConstant;
        private Slider _maxStrength;
        private Slider _snapStressSeconds;
        private Slider _gravityY;
        private Slider _qualityOverride;
        private SliderInt _nodes;
        private SliderInt _iterations;
        private Label _status;
        private Label _telemetry;
        private VisualElement _graph;
        private double _nextRefresh;
        private float _lastMaxTension;
        private float _lastQuality;

        [MenuItem("Hecton8/Physics/Kinematic Tether Tuner SHINOBU 328")]
        public static void Open()
        {
            GetWindow<KinematicTetherTunerWindow328>("Kinematic Tether");
        }

        private void OnEnable()
        {
            EditorApplication.update -= RefreshTelemetry;
            EditorApplication.update += RefreshTelemetry;
        }

        private void OnDisable()
        {
            EditorApplication.update -= RefreshTelemetry;
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;

            _tensionConstant = new Slider("Tension Constant", 0f, 50000f) { value = HarpoonTensionSolver328Constants.DefaultTensionConstant };
            _maxStrength = new Slider("Max Tensile Strength", 1000f, 500000f) { value = HarpoonTensionSolver328Constants.DefaultMaxTensileStrength };
            _snapStressSeconds = new Slider("Snap Stress Seconds", 0.016666667f, 2f) { value = HarpoonTensionSolver328Constants.DefaultSnapStressSeconds };
            _gravityY = new Slider("Node Gravity Y", -40f, 10f) { value = -9.81f };
            _qualityOverride = new Slider("Global Quality Override", -1f, 1f) { value = -1f };
            _nodes = new SliderInt("Nodes Per Tether", 6, 64) { value = HarpoonTensionSolver328Constants.MockNodesPerTether };
            _iterations = new SliderInt("Max Constraint Iterations", 2, 8) { value = 8 };
            _status = new Label("Vault not sampled.");
            _telemetry = new Label("Telemetry: --");
            _graph = new VisualElement();
            _graph.style.height = 80f;
            _graph.style.marginTop = 6f;
            _graph.style.marginBottom = 6f;
            _graph.generateVisualContent += DrawGraph;

            root.Add(_tensionConstant);
            root.Add(_maxStrength);
            root.Add(_snapStressSeconds);
            root.Add(_gravityY);
            root.Add(_qualityOverride);
            root.Add(_nodes);
            root.Add(_iterations);
            root.Add(new Button(PullFromVault) { text = "Read Vault" });
            root.Add(new Button(ApplyToVault) { text = "Apply Tuning" });
            root.Add(new Button(ReloadCsv) { text = "Reload tether_material_profiles.csv" });
            root.Add(new Button(DumpFaultRing) { text = "Dump Fault Ring" });
            root.Add(_graph);
            root.Add(_telemetry);
            root.Add(_status);
            PullFromVault();
        }

        private void PullFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.Tuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<HarpoonTensionTuningDTO> handle,
                    out NativeArray<HarpoonTensionTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                ref HarpoonTensionTuningDTO dto = ref UnsafeUtility.AsRef<HarpoonTensionTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
                if (dto.TensionConstant <= 0f || !math.isfinite(dto.TensionConstant))
                    dto = HarpoonTensionSolver328.DefaultTuning();
                _tensionConstant.SetValueWithoutNotify(dto.TensionConstant);
                _maxStrength.SetValueWithoutNotify(dto.MaxTensileStrength);
                float snapStressSeconds = math.isfinite(dto.SnapStressSeconds) ? dto.SnapStressSeconds : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds;
                _snapStressSeconds.SetValueWithoutNotify(math.clamp(snapStressSeconds, 0.016666667f, 2f));
                _gravityY.SetValueWithoutNotify(dto.NodeGravity.y);
                _qualityOverride.SetValueWithoutNotify(dto.GlobalQualityWeightOverride);
                _nodes.SetValueWithoutNotify(math.clamp(dto.NodesPerTether, 6, 64));
                _iterations.SetValueWithoutNotify(math.clamp(dto.MaxConstraintIterations, 2, 8));
                _status.text = "Vault sampled.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ApplyToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.Tuning,
                    1,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<HarpoonTensionTuningDTO> handle,
                    out NativeArray<HarpoonTensionTuningDTO> tuning))
            {
                _status.text = "GlobalDataVault unavailable.";
                return;
            }

            try
            {
                ref HarpoonTensionTuningDTO dto = ref UnsafeUtility.AsRef<HarpoonTensionTuningDTO>(
                    NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning));
                dto = HarpoonTensionSolver328.DefaultTuning();
                dto.TensionConstant = math.max(0f, _tensionConstant.value);
                dto.MaxTensileStrength = math.max(1f, _maxStrength.value);
                dto.SnapStressSeconds = math.clamp(
                    math.isfinite(_snapStressSeconds.value) ? _snapStressSeconds.value : HarpoonTensionSolver328Constants.DefaultSnapStressSeconds,
                    0.016666667f,
                    2f);
                dto.NodeGravity = new float3(0f, _gravityY.value, 0f);
                dto.GlobalQualityWeightOverride = _qualityOverride.value;
                dto.NodesPerTether = math.clamp(_nodes.value, 6, 64);
                dto.MaxConstraintIterations = math.clamp(_iterations.value, 2, 8);
                _status.text = "Tuning written through UnsafeUtility.AsRef.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void ReloadCsv()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, "tether_material_profiles.csv");
            if (!File.Exists(path))
            {
                _status.text = "tether_material_profiles.csv not found.";
                return;
            }

            IDataVault vault = GlobalRegistry.DataVault;
            if (!TryAcquireEditorWriteView(
                    vault,
                    HarpoonTensionSolver328BufferIds.MaterialProfiles,
                    HarpoonTensionSolver328Constants.MaterialProfileCapacity,
                    SystemID.Physics,
                    NativeArrayOptions.UninitializedMemory,
                    out VaultGenerationHandle<TetherMaterialProfileDTO> handle,
                    out NativeArray<TetherMaterialProfileDTO> profiles))
            {
                _status.text = "Material profile Vault lane unavailable.";
                return;
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                bool parsed = HarpoonTensionSolver328.TryParseTetherMaterialProfiles(bytes.AsSpan(), profiles, out int count);
                _status.text = parsed ? "CSV profiles applied: " + count : "CSV parsed no profile rows.";
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            }
        }

        private void DumpFaultRing()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            bool dumped = HarpoonTensionSolver328.TryDumpTelemetryIfFault(GlobalRegistry.DataVault, projectRoot, 1);
            _status.text = dumped ? "Dump_SHINOBU_328.bin written." : "No SHINOBU_328 fault flags.";
        }

        private void RefreshTelemetry()
        {
            if (_telemetry == null)
                return;

            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefresh)
                return;
            _nextRefresh = now + 0.25d;

            if (!TryReadLatestTelemetry(out TetherTelemetryEntry entry))
            {
                _telemetry.text = "Telemetry: --";
                return;
            }

            _lastMaxTension = entry.MaxTension;
            _lastQuality = entry.GlobalQualityWeight;
            _telemetry.text = "Tension " + entry.MaxTension.ToString("F1") +
                              " N / iterations " + entry.IterationCount +
                              " / nodes " + entry.NodeCount +
                              " / us " + entry.CpuMicroseconds.ToString("F2");
            _graph.MarkDirtyRepaint();
        }

        private void DrawGraph(MeshGenerationContext context)
        {
            Painter2D painter = context.painter2D;
            Rect r = _graph.contentRect;
            float tension01 = math.saturate(_lastMaxTension / HarpoonTensionSolver328Constants.DefaultMaxTensileStrength);
            float quality = math.saturate(_lastQuality);
            painter.lineWidth = 2f;
            painter.strokeColor = Color.Lerp(Color.green, Color.red, tension01);
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin, r.yMax - r.height * tension01));
            painter.LineTo(new Vector2(r.xMax, r.yMax - r.height * tension01));
            painter.Stroke();
            painter.strokeColor = new Color(0.2f, 0.55f, 1f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(r.xMin, r.yMax - r.height * quality));
            painter.LineTo(new Vector2(r.xMax, r.yMax - r.height * quality));
            painter.Stroke();
        }

        private static bool TryReadLatestTelemetry(out TetherTelemetryEntry entry)
        {
            entry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TelemetryRing, out VaultGenerationHandle<TetherTelemetryEntry> ringHandle) ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TelemetryHead, out VaultGenerationHandle<int> headHandle) ||
                !vault.TryReadHandle(in ringHandle, out NativeArray<TetherTelemetryEntry> ring) ||
                !vault.TryReadHandle(in headHandle, out NativeArray<int> head) ||
                !ring.IsCreated ||
                ring.Length == 0 ||
                !head.IsCreated ||
                head.Length == 0)
            {
                return false;
            }

            int capacity = math.min(ring.Length, HarpoonTensionSolver328Constants.TelemetryCapacity);
            int index = head[0] - 1;
            if (index < 0)
                index = capacity - 1;
            index = math.clamp(index, 0, capacity - 1);
            entry = ring[index];
            return entry.FrameIndex != 0u || entry.NodeCount > 0;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            SystemID owner,
            NativeArrayOptions options,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            int required = math.max(1, requiredLength);
            if (vault == null)
                return false;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= required)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;
                handle = vault.EnsureGenerationHandle<T>(bufferId, required, owner, options);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;
            if (buffer.IsCreated && buffer.Length >= required)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }
    }

    [InitializeOnLoad]
    public static class LiveVerletDebugGizmo328
    {
        private static bool _enabled;

        static LiveVerletDebugGizmo328()
        {
            SceneView.duringSceneGui -= DrawScene;
            SceneView.duringSceneGui += DrawScene;
        }

        [MenuItem("Hecton8/Physics/Live Verlet Debug Gizmo SHINOBU 328")]
        public static void Toggle()
        {
            _enabled = !_enabled;
            SceneView.RepaintAll();
        }

        private static void DrawScene(SceneView view)
        {
            if (!_enabled)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TetherStates, out VaultGenerationHandle<TetherStateDTO> stateHandle) ||
                !vault.TryGetGenerationHandle(HarpoonTensionSolver328BufferIds.TetherNodes, out VaultGenerationHandle<float3> nodeHandle) ||
                !vault.TryReadHandle(in stateHandle, out NativeArray<TetherStateDTO> states) ||
                !vault.TryReadHandle(in nodeHandle, out NativeArray<float3> nodes) ||
                !states.IsCreated ||
                !nodes.IsCreated)
            {
                return;
            }

            int nodesPerTether = HarpoonTensionSolver328Constants.MockNodesPerTether;
            for (int tether = 0; tether < states.Length; tether++)
            {
                TetherStateDTO state = states[tether];
                if ((state.Flags & TetherStateFlags328.Active) == 0u)
                    continue;

                float tension01 = math.saturate(state.CurrentTension / math.max(1f, HarpoonTensionSolver328Constants.DefaultMaxTensileStrength));
                Handles.color = Color.Lerp(Color.green, Color.red, tension01);
                int offset = tether * nodesPerTether;
                int last = math.min(offset + nodesPerTether - 1, nodes.Length - 1);
                for (int i = offset; i < last; i++)
                {
                    float3 a = nodes[i];
                    float3 b = nodes[i + 1];
                    Handles.DrawLine(new Vector3(a.x, a.y, a.z), new Vector3(b.x, b.y, b.z));
                }
            }
        }
    }
}
#endif
