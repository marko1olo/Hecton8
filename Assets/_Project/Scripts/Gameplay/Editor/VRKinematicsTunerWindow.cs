#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay.Editor
{
    public sealed class VRKinematicsTunerWindow : EditorWindow
    {
        private IDataVault _dataVault;
        private VaultGenerationHandle<PlayerKinematicsRuntime.IkHandConfigDTO> _configHandle;
        private VaultGenerationHandle<PlayerKinematicsRuntime.IkHandTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<IkHandStateDTO> _statesHandle;
        private VaultGenerationHandle<PlayerKinematicsRuntime.IkHandTargetDTO> _targetsHandle;

        private Slider _maxIterationsSlider;
        private Slider _blendOutSpeedSlider;
        private Slider _qualityOverrideSlider;
        private Toggle _mockTargetsToggle;
        private Label _layoutLabel;
        private Label _telemetryLabel;
        private HandIkHistogramElement _histogram;

        [MenuItem("Hecton8/Player/VR Kinematics Tuner")]
        public static void Open()
        {
            GetWindow<VRKinematicsTunerWindow>("VR Kinematics");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _layoutLabel = new Label();
            _layoutLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_layoutLabel);

            _maxIterationsSlider = new Slider("Max FABRIK Iterations", 1f, 8f);
            _blendOutSpeedSlider = new Slider("Blend Out Speed", 0.25f, 16f);
            _qualityOverrideSlider = new Slider("GlobalQualityWeight Override", -1f, 1f);
            _mockTargetsToggle = new Toggle("Mock Figure-Eight Targets");
            root.Add(_maxIterationsSlider);
            root.Add(_blendOutSpeedSlider);
            root.Add(_qualityOverrideSlider);
            root.Add(_mockTargetsToggle);
            root.Add(new Button(RefreshVaultReadout) { text = "Refresh Vault" });
            root.Add(new Button(ApplyControlsToVault) { text = "Apply To Vault" });

            _telemetryLabel = new Label();
            _telemetryLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_telemetryLabel);

            _histogram = new HandIkHistogramElement();
            root.Add(_histogram);
            RefreshVaultReadout();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= DrawHandIkGizmos;
            SceneView.duringSceneGui += DrawHandIkGizmos;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawHandIkGizmos;
            ReleaseVaultHandles();
            ClearHandles();
        }

        private void OnInspectorUpdate()
        {
            RefreshVaultReadout();
        }

        private void RefreshVaultReadout()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
            {
                if (_layoutLabel != null)
                    _layoutLabel.text = "GlobalDataVault unavailable.";
                return;
            }

            BindVault(vault);
            if (TryResolveEditorVaultView(
                    vault,
                    ref _configHandle,
                    PlayerKinematicsRuntime.HandIkConfigBuffer,
                    1,
                    out NativeArray<PlayerKinematicsRuntime.IkHandConfigDTO> config))
            {
                PlayerKinematicsRuntime.IkHandConfigDTO dto = config[0];
                _maxIterationsSlider.SetValueWithoutNotify(math.clamp(dto.MaxFabrikIterations <= 0f ? 8f : dto.MaxFabrikIterations, 1f, 8f));
                _blendOutSpeedSlider.SetValueWithoutNotify(math.clamp(dto.BlendOutSpeed <= 0f ? 4f : dto.BlendOutSpeed, 0.25f, 16f));
                _qualityOverrideSlider.SetValueWithoutNotify(math.clamp(dto.GlobalQualityWeightOverride, -1f, 1f));
                _mockTargetsToggle.SetValueWithoutNotify((dto.Flags & PlayerKinematicsRuntime.IkHandFlags.ConfigMockTargets) != 0u);
                _layoutLabel.text =
                    "IkHandStateDTO: 64B shoulder@0 elbow@12 wrist@24 upper@36 forearm@40 targetHash@44 flags@48 pad@52..63";
            }

            if (TryResolveEditorVaultView(
                    vault,
                    ref _telemetryHandle,
                    PlayerKinematicsRuntime.HandIkTelemetryRingBuffer,
                    PlayerKinematicsRuntime.HandIkTelemetryFrameCount,
                    out NativeArray<PlayerKinematicsRuntime.IkHandTelemetryEntry> telemetry))
            {
                PlayerKinematicsRuntime.IkHandTelemetryEntry latest = ResolveLatestTelemetry(telemetry);
                _telemetryLabel.text =
                    $"Frame={latest.FrameIndex} arms={latest.ArmsProcessed} iter={latest.ActiveIterationLimit} " +
                    $"error={latest.MaxDistanceErrorMeters:0.0000}m pole={latest.MaxPoleErrorMeters:0.0000}m " +
                    $"micros={latest.CompletionMicros:0.0} flags=0x{latest.Flags:X8}";
                _histogram.SetSamples(telemetry);
            }
        }

        private void ApplyControlsToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !TryResolveEditorVaultView(
                    vault,
                    ref _configHandle,
                    PlayerKinematicsRuntime.HandIkConfigBuffer,
                    1,
                    out NativeArray<PlayerKinematicsRuntime.IkHandConfigDTO> config))
            {
                return;
            }

            PlayerKinematicsRuntime.IkHandConfigDTO dto = config[0];
            dto.MaxFabrikIterations = math.clamp(_maxIterationsSlider.value, 1f, 8f);
            dto.BlendOutSpeed = math.max(0.25f, _blendOutSpeedSlider.value);
            dto.BlendOutSeconds = math.rcp(dto.BlendOutSpeed);
            dto.GlobalQualityWeightOverride = math.clamp(_qualityOverrideSlider.value, -1f, 1f);
            if (_mockTargetsToggle.value)
                dto.Flags |= PlayerKinematicsRuntime.IkHandFlags.ConfigMockTargets;
            else
                dto.Flags &= ~PlayerKinematicsRuntime.IkHandFlags.ConfigMockTargets;

            config[0] = dto;
            SceneView.RepaintAll();
        }

        private void DrawHandIkGizmos(SceneView sceneView)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            BindVault(vault);
            if (!TryResolveEditorVaultView(
                    vault,
                    ref _statesHandle,
                    PlayerKinematicsRuntime.HandIkStatesBuffer,
                    PlayerKinematicsRuntime.HandIkHandCount,
                    out NativeArray<IkHandStateDTO> states) ||
                !TryResolveEditorVaultView(
                    vault,
                    ref _targetsHandle,
                    PlayerKinematicsRuntime.HandIkTargetsBuffer,
                    PlayerKinematicsRuntime.HandIkHandCount,
                    out NativeArray<PlayerKinematicsRuntime.IkHandTargetDTO> targets))
            {
                return;
            }

            Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            for (int i = 0; i < math.min(states.Length, PlayerKinematicsRuntime.HandIkHandCount); i++)
            {
                IkHandStateDTO state = states[i];
                Vector3 shoulder = ToVector3(state.ShoulderPos);
                Vector3 elbow = ToVector3(state.ElbowPos);
                Vector3 wrist = ToVector3(state.WristPos);
                Handles.color = i == 0 ? Color.cyan : Color.magenta;
                Handles.DrawLine(shoulder, elbow, 3f);
                Handles.DrawLine(elbow, wrist, 3f);
                if ((uint)i < (uint)targets.Length)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawLine(shoulder, ToVector3(targets[i].PoleLocal), 1.5f);
                }
            }
        }

        private void BindVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultHandles();
            ClearHandles();
            _dataVault = vault;
        }

        private static PlayerKinematicsRuntime.IkHandTelemetryEntry ResolveLatestTelemetry(NativeArray<PlayerKinematicsRuntime.IkHandTelemetryEntry> telemetry)
        {
            PlayerKinematicsRuntime.IkHandTelemetryEntry latest = default;
            for (int i = 0; i < telemetry.Length; i++)
            {
                PlayerKinematicsRuntime.IkHandTelemetryEntry entry = telemetry[i];
                if (entry.Marker == PlayerKinematicsRuntime.HandIkTelemetryMarker && entry.FrameIndex >= latest.FrameIndex)
                    latest = entry;
            }

            return latest;
        }

        private static bool TryResolveEditorVaultView<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsHandleCreated(in handle) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> acquired))
                return false;

            if (!IsHandleCreated(in acquired) ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            handle = acquired;
            return true;
        }

        private static bool IsHandleCreated<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private void ReleaseVaultHandles()
        {
            ClearHandles();
        }

        private void ClearHandles()
        {
            _configHandle = default;
            _telemetryHandle = default;
            _statesHandle = default;
            _targetsHandle = default;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private sealed class HandIkHistogramElement : VisualElement
        {
            private readonly float[] _micros = new float[PlayerKinematicsRuntime.HandIkTelemetryFrameCount];
            private readonly float[] _errors = new float[PlayerKinematicsRuntime.HandIkTelemetryFrameCount];
            private int _count;

            public HandIkHistogramElement()
            {
                style.height = 96;
                style.marginTop = 6;
                generateVisualContent += Draw;
            }

            public void SetSamples(NativeArray<PlayerKinematicsRuntime.IkHandTelemetryEntry> telemetry)
            {
                _count = math.min(telemetry.IsCreated ? telemetry.Length : 0, PlayerKinematicsRuntime.HandIkTelemetryFrameCount);
                for (int i = 0; i < _count; i++)
                {
                    PlayerKinematicsRuntime.IkHandTelemetryEntry entry = telemetry[i];
                    _micros[i] = entry.Marker == PlayerKinematicsRuntime.HandIkTelemetryMarker ? entry.CompletionMicros : 0f;
                    _errors[i] = entry.Marker == PlayerKinematicsRuntime.HandIkTelemetryMarker ? entry.MaxDistanceErrorMeters : 0f;
                }

                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (_count <= 1 || r.width <= 1f || r.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                DrawSeries(painter, r, _micros, PlayerKinematicsRuntime.HandIkBudgetMicros, new Color(0.1f, 0.8f, 1f, 0.9f));
                DrawSeries(painter, r, _errors, 0.05f, new Color(1f, 0.85f, 0.1f, 0.9f));
            }

            private void DrawSeries(Painter2D painter, Rect r, float[] values, float maxValue, Color color)
            {
                float safeMax = math.max(0.0001f, maxValue);
                painter.strokeColor = color;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                float invSpan = 1.0f / math.max(1, _count - 1);
                for (int i = 0; i < _count; i++)
                {
                    float x = r.xMin + (i * r.width * invSpan);
                    float normalized = math.saturate(values[i] * math.rcp(safeMax));
                    float y = r.yMax - normalized * r.height;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }
}
#endif
