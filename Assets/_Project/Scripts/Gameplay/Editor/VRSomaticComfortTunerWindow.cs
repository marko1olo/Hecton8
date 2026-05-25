#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay.Editor
{
    public sealed class VRSomaticComfortTunerWindow : EditorWindow
    {
        private const int HorizonTelemetryFrameCount = 300;

        private IDataVault _dataVault;
        private VaultGenerationHandle<VrComfortProfileDTO> _profileHandle;
        private VaultGenerationHandle<VRSomaticComfortDTO> _horizonStateHandle;
        private VaultGenerationHandle<SomaticTelemetryEntry> _horizonTelemetryHandle;

        private Slider _springDampingSlider;
        private Slider _tunnelThresholdSlider;
        private Slider _maxTunnelSlider;
        private Label _stateLabel;
        private HorizonGraphElement _graph;

        [MenuItem("Hecton8/Player/VR Comfort & Horizon Tuner")]
        public static void Open()
        {
            GetWindow<VRSomaticComfortTunerWindow>("VR Comfort");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _stateLabel = new Label();
            _stateLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_stateLabel);

            _springDampingSlider = new Slider("Spring Damping Factor", 0.01f, 36f);
            _tunnelThresholdSlider = new Slider("Tunnel Activation Threshold", 0.05f, 4f);
            _maxTunnelSlider = new Slider("Max Tunnel Intensity", 0f, 1f);
            root.Add(_springDampingSlider);
            root.Add(_tunnelThresholdSlider);
            root.Add(_maxTunnelSlider);
            root.Add(new Button(RefreshVaultReadout) { text = "Refresh Vault" });
            root.Add(new Button(ApplyControlsToVault) { text = "Apply To Vault" });

            _graph = new HorizonGraphElement();
            root.Add(_graph);
            RefreshVaultReadout();
        }

        private void OnDisable()
        {
            _dataVault = null;
            _profileHandle = default;
            _horizonStateHandle = default;
            _horizonTelemetryHandle = default;
        }

        private void OnInspectorUpdate()
        {
            RefreshVaultReadout();
        }

        private void RefreshVaultReadout()
        {
            if (!TryResolveEditorVault(out IDataVault vault))
            {
                if (_stateLabel != null)
                    _stateLabel.text = "GlobalDataVault unavailable.";
                return;
            }

            BindVault(vault);
            if (TryResolveEditorVaultView(
                    vault,
                    ref _profileHandle,
                    BufferID.ShinobuVRSomaticProfile,
                    1,
                    out NativeArray<VrComfortProfileDTO> profiles))
            {
                VrComfortProfileDTO profile = profiles[0];
                _springDampingSlider.SetValueWithoutNotify(math.clamp(profile.HorizonLockSpeed <= 0f ? 18f : profile.HorizonLockSpeed, 0.01f, 36f));
                _tunnelThresholdSlider.SetValueWithoutNotify(math.clamp(profile.AngularVelocitySoftRadS <= 0f ? 1f : profile.AngularVelocitySoftRadS, 0.05f, 4f));
                _maxTunnelSlider.SetValueWithoutNotify(math.clamp(profile.VrBaselineFovTunnel, 0f, 1f));
            }

            VRSomaticComfortDTO state = default;
            bool hasState = TryResolveEditorVaultView(
                vault,
                ref _horizonStateHandle,
                BufferID.ShinobuVRSomaticHorizonRead,
                1,
                out NativeArray<VRSomaticComfortDTO> horizonState);
            if (hasState)
                state = horizonState[0];

            if (TryResolveEditorVaultView(
                    vault,
                    ref _horizonTelemetryHandle,
                    BufferID.ShinobuVRSomaticHorizonTelemetry,
                    HorizonTelemetryFrameCount,
                    out NativeArray<SomaticTelemetryEntry> telemetry))
            {
                SomaticTelemetryEntry latest = ResolveLatestTelemetry(telemetry);
                _graph.SetSamples(telemetry);
                if (_stateLabel != null)
                {
                    _stateLabel.text =
                        $"VRSomaticComfortDTO=32B rot@0 fov@16 pitch@20 flags@24 | " +
                        $"latest frame={latest.Frame} fov={state.FovTunnelScalar:0.000} pitch={state.PitchDampening:0.000} " +
                        $"raw={math.length(latest.RawAngularVelocity):0.00}rad/s micros={latest.BurstExecutionMicroseconds:0.0} flags=0x{latest.Flags:X8}";
                }
            }
            else if (_stateLabel != null)
            {
                _stateLabel.text = hasState
                    ? $"VRSomaticComfortDTO=32B | fov={state.FovTunnelScalar:0.000} pitch={state.PitchDampening:0.000} flags=0x{state.ComfortFlags:X8}"
                    : "Horizon Vault buffers unavailable.";
            }
        }

        private void ApplyControlsToVault()
        {
            ApplyControlsToVaultUnsafe();
        }

        private unsafe void ApplyControlsToVaultUnsafe()
        {
            if (!TryResolveEditorVault(out IDataVault vault) ||
                !TryResolveEditorVaultView(
                    vault,
                    ref _profileHandle,
                    BufferID.ShinobuVRSomaticProfile,
                    1,
                    out NativeArray<VrComfortProfileDTO> profiles))
            {
                return;
            }

            ref VrComfortProfileDTO profile = ref UnsafeUtility.AsRef<VrComfortProfileDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(profiles));
            profile.HorizonLockSpeed = math.max(0.01f, _springDampingSlider.value);
            profile.AngularVelocitySoftRadS = math.max(0.05f, _tunnelThresholdSlider.value);
            profile.VrBaselineFovTunnel = math.saturate(_maxTunnelSlider.value);
            SceneView.RepaintAll();
        }

        private void BindVault(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            _dataVault = vault;
            _profileHandle = default;
            _horizonStateHandle = default;
            _horizonTelemetryHandle = default;
        }

        private static SomaticTelemetryEntry ResolveLatestTelemetry(NativeArray<SomaticTelemetryEntry> telemetry)
        {
            SomaticTelemetryEntry latest = default;
            for (int i = 0; i < telemetry.Length; i++)
            {
                SomaticTelemetryEntry entry = telemetry[i];
                if (entry.Frame >= latest.Frame)
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

        private static bool TryResolveEditorVault(out IDataVault vault)
        {
            if (GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
            {
                vault = latest;
                return true;
            }

            vault = null;
            return false;
        }

        private sealed class HorizonGraphElement : VisualElement
        {
            private readonly float[] _rawAngular = new float[HorizonTelemetryFrameCount];
            private readonly float[] _tunnel = new float[HorizonTelemetryFrameCount];
            private int _count;

            public HorizonGraphElement()
            {
                style.height = 112;
                style.marginTop = 6;
                generateVisualContent += Draw;
            }

            public void SetSamples(NativeArray<SomaticTelemetryEntry> telemetry)
            {
                _count = math.min(telemetry.IsCreated ? telemetry.Length : 0, HorizonTelemetryFrameCount);
                for (int i = 0; i < _count; i++)
                {
                    SomaticTelemetryEntry entry = telemetry[i];
                    _rawAngular[i] = math.length(entry.RawAngularVelocity);
                    _tunnel[i] = entry.FovTunnelScalar;
                }

                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                Rect r = contentRect;
                if (_count <= 1 || r.width <= 1f || r.height <= 1f)
                    return;

                Painter2D painter = context.painter2D;
                DrawSeries(painter, r, _rawAngular, 16f, new Color(0.1f, 0.85f, 1f, 0.9f));
                DrawSeries(painter, r, _tunnel, 1f, new Color(1f, 0.2f, 0.16f, 0.9f));
            }

            private void DrawSeries(Painter2D painter, Rect r, float[] values, float maxValue, Color color)
            {
                painter.strokeColor = color;
                painter.lineWidth = 1.5f;
                painter.BeginPath();
                float invSpan = 1.0f / math.max(1, _count - 1);
                float safeMax = math.max(0.0001f, maxValue);
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
