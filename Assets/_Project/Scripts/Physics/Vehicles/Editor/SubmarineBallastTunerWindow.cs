using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.Vehicles.Editor
{
    public sealed class SubmarineBallastTunerWindow : EditorWindow
    {
        private const int GraphBars = 64;
        private readonly VisualElement[] _bars = new VisualElement[GraphBars];
        private Label _status;
        private Slider _hullVolume;
        private Slider _hullHeight;
        private Slider _tankLiters;
        private Slider _pumpRate;
        private Slider _airPressure;
        private bool _suppressCallbacks;

        [MenuItem("Hecton8/Vehicles/Submarine Ballast/Tuner")]
        public static void Open()
        {
            SubmarineBallastTunerWindow window = GetWindow<SubmarineBallastTunerWindow>();
            window.titleContent = new GUIContent("Submarine Ballast");
            window.minSize = new Vector2(440f, 360f);
            window.Show();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;
            _status = new Label("Vault unavailable");
            root.Add(_status);

            _hullVolume = AddSlider(root, "Hull Volume m3", 1f, 80f);
            _hullHeight = AddSlider(root, "Hull Height m", 0.5f, 12f);
            _tankLiters = AddSlider(root, "Tank Liters", 10f, 5000f);
            _pumpRate = AddSlider(root, "Pump L/s", 1f, 2000f);
            _airPressure = AddSlider(root, "Air ATM", 1f, 80f);

            VisualElement graph = new VisualElement();
            graph.style.flexDirection = FlexDirection.Row;
            graph.style.height = 96f;
            graph.style.marginTop = 8f;
            graph.style.borderTopWidth = 1f;
            graph.style.borderBottomWidth = 1f;
            graph.style.borderLeftWidth = 1f;
            graph.style.borderRightWidth = 1f;
            graph.style.borderTopColor = Color.gray;
            graph.style.borderBottomColor = Color.gray;
            graph.style.borderLeftColor = Color.gray;
            graph.style.borderRightColor = Color.gray;
            root.Add(graph);

            for (int i = 0; i < GraphBars; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.flexGrow = 1f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.marginLeft = 1f;
                bar.style.backgroundColor = new Color(0.95f, 0.82f, 0.18f, 1f);
                graph.Add(bar);
                _bars[i] = bar;
            }

            RefreshFromVault();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            RefreshFromVault();
        }

        private Slider AddSlider(VisualElement root, string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(evt =>
            {
                if (!_suppressCallbacks)
                    WriteTuningToVault();
            });
            root.Add(slider);
            return slider;
        }

        private void RefreshFromVault()
        {
            if (!TryReadTuning(out SubmarineBallastTuningDTO tuning))
            {
                if (_status != null)
                    _status.text = "Vault tuning row unavailable";
                return;
            }

            _suppressCallbacks = true;
            _hullVolume.value = math.max(0f, tuning.HullVolumeCubicMeters);
            _hullHeight.value = math.max(0f, tuning.HullHeightMeters);
            _tankLiters.value = math.max(0f, tuning.MaxTankLiters);
            _pumpRate.value = math.max(0f, tuning.PumpRateLitersPerSecond);
            _airPressure.value = math.max(0f, tuning.AirBankPressureATM);
            _suppressCallbacks = false;

            if (_status != null)
            {
                _status.text = "F " + tuning.LastNetForceY.ToString("0.0") +
                               " N | L " + tuning.LastWaterLiters.ToString("0.0") +
                               " | ATM " + tuning.LastAmbientPressureATM.ToString("0.00") +
                               " | Q " + tuning.GlobalQualityWeight.ToString("0.000");
            }

            RefreshGraph();
        }

        private void RefreshGraph()
        {
            if (!TryResolveTelemetry(out NativeArray<SubmarineBallastTelemetryEntry>.ReadOnly telemetry) || telemetry.Length == 0)
                return;

            int count = math.min(GraphBars, telemetry.Length);
            float maxAbs = 1f;
            for (int i = 0; i < count; i++)
                maxAbs = math.max(maxAbs, math.abs(telemetry[i].NetForceY));

            for (int i = 0; i < GraphBars; i++)
            {
                float height = 1f;
                if (i < count)
                    height = math.lerp(4f, 92f, math.saturate(math.abs(telemetry[i].NetForceY) / maxAbs));

                _bars[i].style.height = height;
            }
        }

        private void WriteTuningToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(SubmarineBallastBufferIds.Tuning, out VaultGenerationHandle<SubmarineBallastTuningDTO> handle))
            {
                return;
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.VehiclesPhysics, out NativeArray<SubmarineBallastTuningDTO> tuning))
                return;

            try
            {
                if (!tuning.IsCreated || tuning.Length == 0)
                    return;

                SubmarineBallastTuningDTO dto = tuning[0];
                dto.HullVolumeCubicMeters = math.max(0.1f, _hullVolume.value);
                dto.HullHeightMeters = math.max(0.1f, _hullHeight.value);
                dto.MaxTankLiters = math.max(0.1f, _tankLiters.value);
                dto.PumpRateLitersPerSecond = math.max(0f, _pumpRate.value);
                dto.AirBankPressureATM = math.max(1f, _airPressure.value);
                dto.SourceHash = SubmarineBallastConstants.SourceHash;
                tuning[0] = dto;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.VehiclesPhysics);
            }
        }

        private static bool TryReadTuning(out SubmarineBallastTuningDTO tuning)
        {
            tuning = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(SubmarineBallastBufferIds.Tuning, out VaultGenerationHandle<SubmarineBallastTuningDTO> handle) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<SubmarineBallastTuningDTO>.ReadOnly rows) ||
                !rows.IsCreated ||
                rows.Length == 0)
            {
                return false;
            }

            tuning = rows[0];
            return true;
        }

        private static bool TryResolveTelemetry(out NativeArray<SubmarineBallastTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle(SubmarineBallastBufferIds.TelemetryRing, out VaultGenerationHandle<SubmarineBallastTelemetryEntry> handle) &&
                   vault.TryReadOnlyHandle(in handle, out telemetry) &&
                   telemetry.IsCreated;
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void DrawBallastGizmos(SubmarineAutoLevelBallastController controller, GizmoType gizmoType)
        {
            if (controller == null ||
                !TryResolveTanks(out NativeArray<BallastTankDTO>.ReadOnly tanks) ||
                tanks.Length == 0)
            {
                return;
            }

            Transform transform = controller.transform;
            Vector3[] offsets =
            {
                new Vector3(0f, -0.35f, 2.4f),
                new Vector3(0f, -0.35f, -2.4f),
                new Vector3(-1.1f, -0.35f, 0f),
                new Vector3(1.1f, -0.35f, 0f)
            };

            int count = math.min(4, tanks.Length);
            for (int i = 0; i < count; i++)
            {
                BallastTankDTO tank = tanks[i];
                float fill01 = math.saturate(tank.CurrentWaterLiters * math.rcp(math.max(0.0001f, tank.TankVolumeLiters)));
                Vector3 center = transform.TransformPoint(offsets[i]);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(center, new Vector3(0.55f, 0.85f, 0.55f));
                Gizmos.color = new Color(0.1f, 0.35f, 1f, 0.45f);
                Gizmos.DrawCube(center + Vector3.down * (0.425f * (1f - fill01)), new Vector3(0.48f, 0.85f * fill01, 0.48f));
            }

            if (TryReadForce(out SubmarineBallastForcePacketDTO force))
            {
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, origin + Vector3.up * math.saturate(math.abs(force.BuoyantForce.y) * 0.00005f));
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, origin + Vector3.down * math.saturate(math.abs(force.BallastGravityForce.y) * 0.0001f));
            }
        }

        private static bool TryResolveTanks(out NativeArray<BallastTankDTO>.ReadOnly tanks)
        {
            tanks = default;
            IDataVault vault = GlobalRegistry.DataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle(SubmarineBallastBufferIds.Tanks, out VaultGenerationHandle<BallastTankDTO> handle) &&
                   vault.TryReadOnlyHandle(in handle, out tanks) &&
                   tanks.IsCreated;
        }

        private static bool TryReadForce(out SubmarineBallastForcePacketDTO force)
        {
            force = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null ||
                !vault.TryGetGenerationHandle(SubmarineBallastBufferIds.ForcePackets, out VaultGenerationHandle<SubmarineBallastForcePacketDTO> handle) ||
                !vault.TryReadOnlyHandle(in handle, out NativeArray<SubmarineBallastForcePacketDTO>.ReadOnly packets) ||
                !packets.IsCreated ||
                packets.Length == 0)
            {
                return false;
            }

            force = packets[0];
            return true;
        }
    }
}
