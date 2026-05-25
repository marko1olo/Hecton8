#if UNITY_EDITOR
using System.Reflection;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Gameplay
{
    [InitializeOnLoad]
    internal static class BallisticsLayoutVerifier
    {
        private const int BallisticTrajectoryStrideBytes = 64;
        private const int AabbPrimitiveStrideBytes = 96;
        private const int BallisticHitResultStrideBytes = 112;
        private const int BallisticImpactVfxStrideBytes = 80;
        private const int BallisticsTuningStrideBytes = 64;
        private const int BallisticsTelemetryStrideBytes = 64;
        private const int BallisticsCountersStrideBytes = 64;

        static BallisticsLayoutVerifier()
        {
            Validate(logSuccess: false);
        }

        [MenuItem("HECTON-8/Combat/Validate Ballistics Layout")]
        public static void ValidateFromMenu()
        {
            Validate(logSuccess: true);
        }

        public static bool Validate(bool logSuccess)
        {
            bool valid =
                UnsafeUtility.SizeOf<BallisticTrajectoryDTO>() == BallisticTrajectoryStrideBytes &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.OriginAUP)) == 0 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Direction)) == 24 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Velocity)) == 36 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Mass)) == 40 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.WeaponHash)) == 44 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.SourceEntityID)) == 48 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO.Flags)) == 52 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO._pad0)) == 56 &&
                OffsetOf<BallisticTrajectoryDTO>(nameof(BallisticTrajectoryDTO._pad1)) == 60 &&
                UnsafeUtility.SizeOf<AABBPrimitiveDTO>() == AabbPrimitiveStrideBytes &&
                UnsafeUtility.SizeOf<BallisticHitResultDTO>() == BallisticHitResultStrideBytes &&
                OffsetOf<BallisticHitResultDTO>(nameof(BallisticHitResultDTO.ImpactDirection)) == 48 &&
                UnsafeUtility.SizeOf<BallisticImpactVfxDTO>() == BallisticImpactVfxStrideBytes &&
                OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.Matrix)) == 0 &&
                OffsetOf<BallisticImpactVfxDTO>(nameof(BallisticImpactVfxDTO.MaterialHash)) == 64 &&
                UnsafeUtility.SizeOf<BallisticsTuningDTO>() == BallisticsTuningStrideBytes &&
                OffsetOf<BallisticsTuningDTO>(nameof(BallisticsTuningDTO.Revision)) == 44 &&
                UnsafeUtility.SizeOf<BallisticsTelemetryEntry>() == BallisticsTelemetryStrideBytes &&
                UnsafeUtility.SizeOf<BallisticsCountersDTO>() == BallisticsCountersStrideBytes;

            if (!valid)
            {
                Hecton8.Core.H8Debug.LogError("[BallisticsLayoutVerifier] Ballistic DTO layout mismatch. SHINOBU_127 cannot be trusted until offsets match the XML contract.");
                return false;
            }

            if (logSuccess)
                Hecton8.Core.H8Debug.Log("[BallisticsLayoutVerifier] BallisticTrajectoryDTO=64B, AABBPrimitiveDTO=96B, BallisticHitResultDTO=112B, ImpactVfx=80B, Tuning/Telemetry/Counters=64B.");

            return true;
        }

        private static int OffsetOf<T>(string fieldName)
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }

    public sealed class AbyssalBallisticsTunerWindow : EditorWindow
    {
        private Slider _dragSlider;
        private Slider _frictionSlider;
        private Slider _velocitySlider;
        private Slider _lethalitySlider;
        private Slider _qualitySlider;
        private Label _telemetryStateLabel;
        private IntegerField _frameField;
        private IntegerField _shotField;
        private IntegerField _hitField;
        private IntegerField _ricochetField;
        private IntegerField _signalField;
        private IntegerField _rejectedField;
        private FloatField _microsecondField;
        private double _nextRefreshTime;

        [MenuItem("HECTON-8/Combat/Abyssal Ballistics Tuner")]
        public static void Open()
        {
            AbyssalBallisticsTunerWindow window = GetWindow<AbyssalBallisticsTunerWindow>();
            window.titleContent = new GUIContent("Abyssal Ballistics Tuner");
            window.minSize = new Vector2(360f, 240f);
        }

        private void OnEnable()
        {
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
            root.style.paddingBottom = 8f;

            _dragSlider = CreateSlider("Water Drag", 0f, 0.6f);
            _frictionSlider = CreateSlider("Ricochet Friction", 0.02f, 0.98f);
            _velocitySlider = CreateSlider("Flora Velocity", 1f, 120f);
            _lethalitySlider = CreateSlider("Lethality Threshold", 0.1f, 80f);
            _qualitySlider = CreateSlider("Quality Weight", 0f, 1f);
            _telemetryStateLabel = new Label("Telemetry: waiting for Vault.");
            _frameField = CreateReadOnlyIntegerField("Frame");
            _shotField = CreateReadOnlyIntegerField("Shots");
            _hitField = CreateReadOnlyIntegerField("Hits");
            _ricochetField = CreateReadOnlyIntegerField("Ricochets");
            _signalField = CreateReadOnlyIntegerField("Signals");
            _rejectedField = CreateReadOnlyIntegerField("Rejected");
            _microsecondField = CreateReadOnlyFloatField("Solve us");

            Button mockButton = new Button(() => BallisticsRuntime.GenerateMockBallistics())
            {
                text = "Generate Mock Firefight"
            };
            Button csvButton = new Button(LoadCsv)
            {
                text = "Load armor_penetration_matrix.csv"
            };
            Button validateButton = new Button(BallisticsLayoutVerifier.ValidateFromMenu)
            {
                text = "Validate 64B Layout"
            };

            root.Add(_dragSlider);
            root.Add(_frictionSlider);
            root.Add(_velocitySlider);
            root.Add(_lethalitySlider);
            root.Add(_qualitySlider);
            root.Add(mockButton);
            root.Add(csvButton);
            root.Add(validateButton);
            root.Add(_telemetryStateLabel);
            root.Add(_frameField);
            root.Add(_shotField);
            root.Add(_hitField);
            root.Add(_ricochetField);
            root.Add(_signalField);
            root.Add(_rejectedField);
            root.Add(_microsecondField);

            _dragSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _frictionSlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _velocitySlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _lethalitySlider.RegisterValueChangedCallback(_ => ApplyTuning());
            _qualitySlider.RegisterValueChangedCallback(_ => ApplyTuning());
            PullTuning();
        }

        private static Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max)
            {
                showInputField = true
            };
            slider.style.marginBottom = 4f;
            return slider;
        }

        private static IntegerField CreateReadOnlyIntegerField(string label)
        {
            IntegerField field = new IntegerField(label);
            field.SetEnabled(false);
            field.style.marginBottom = 2f;
            return field;
        }

        private static FloatField CreateReadOnlyFloatField(string label)
        {
            FloatField field = new FloatField(label);
            field.SetEnabled(false);
            field.style.marginBottom = 2f;
            return field;
        }

        private void PullTuning()
        {
            if (!BallisticsRuntime.TryGetTuning(out BallisticsTuningDTO tuning))
                return;

            _dragSlider.SetValueWithoutNotify(tuning.DragCoefficient);
            _frictionSlider.SetValueWithoutNotify(tuning.RicochetFriction);
            _velocitySlider.SetValueWithoutNotify(tuning.FloraBaseVelocity);
            _lethalitySlider.SetValueWithoutNotify(tuning.LethalityThreshold);
            _qualitySlider.SetValueWithoutNotify(tuning.GlobalQualityWeight);
        }

        private void ApplyTuning()
        {
            if (!BallisticsRuntime.TryGetTuning(out BallisticsTuningDTO tuning))
                return;

            tuning.DragCoefficient = _dragSlider.value;
            tuning.RicochetFriction = _frictionSlider.value;
            tuning.FloraBaseVelocity = _velocitySlider.value;
            tuning.LethalityThreshold = _lethalitySlider.value;
            tuning.GlobalQualityWeight = _qualitySlider.value;
            BallisticsRuntime.WriteTuning(in tuning);
            HomeostasisBrain.SetForcedGlobalQualityWeightForTuner(_qualitySlider.value, true);
        }

        private void LoadCsv()
        {
            string path = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Data", "Balance", "armor_penetration_matrix.csv");
            BallisticsRuntime.TryLoadPenetrationCsv(path);
        }

        private void RefreshTelemetry()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextRefreshTime)
                return;

            _nextRefreshTime = now + 0.25d;
            if (_telemetryStateLabel == null)
                return;

            if (BallisticsRuntime.TryGetLastTelemetry(out BallisticsTelemetryEntry entry))
            {
                _telemetryStateLabel.text = "Telemetry: latest solved frame.";
                _frameField.SetValueWithoutNotify(ToInspectorInt(entry.Frame));
                _shotField.SetValueWithoutNotify(ToInspectorInt(entry.TrajectoriesProcessed));
                _hitField.SetValueWithoutNotify(ToInspectorInt(entry.HitCount));
                _ricochetField.SetValueWithoutNotify(ToInspectorInt(entry.RicochetCount));
                _signalField.SetValueWithoutNotify(ToInspectorInt(entry.SignalCount));
                _rejectedField.SetValueWithoutNotify(ToInspectorInt(entry.RejectedCount));
                _microsecondField.SetValueWithoutNotify(math.max(0f, entry.SolveMicroseconds));
            }
            else
            {
                _telemetryStateLabel.text = "Telemetry: no solved frame yet.";
            }
        }

        private static int ToInspectorInt(uint value)
        {
            return value > (uint)int.MaxValue ? int.MaxValue : (int)value;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Combat/Ballistics Debug Gizmo")]
    public sealed class BallisticsDebugGizmo : MonoBehaviour
    {
        [SerializeField] private bool drawTrajectories = true;
        [SerializeField] private bool drawAabbs = true;
        [SerializeField, Range(1, 512)] private int maxDrawCount = 128;

        private void OnDrawGizmos()
        {
            if (!BallisticsRuntime.TryGetDebugBuffers(
                    out NativeArray<BallisticTrajectoryDTO>.ReadOnly trajectories,
                    out int trajectoryCount,
                    out NativeArray<AABBPrimitiveDTO>.ReadOnly primitives,
                    out int primitiveCount,
                    out NativeArray<BallisticHitResultDTO>.ReadOnly hits))
                return;

            int drawCount = math.min(maxDrawCount, trajectoryCount);
            if (drawTrajectories)
            {
                for (int i = 0; i < drawCount; i++)
                {
                    BallisticTrajectoryDTO trajectory = trajectories[i];
                    Vector3 origin = HectonFloatingOrigin.ToRuntimePosition(trajectory.OriginAUP);
                    Vector3 direction = new Vector3(trajectory.Direction.x, trajectory.Direction.y, trajectory.Direction.z);
                    if (!IsFinite(origin) || !IsFinite(direction))
                        continue;

                    bool hit = i < hits.Length && (hits[i].Flags & BallisticHitFlags.Hit) != 0u;
                    Gizmos.color = hit ? Color.red : Color.yellow;
                    float length = math.clamp(trajectory.Velocity * 0.12f, 0.5f, 24f);
                    Gizmos.DrawLine(origin, origin + (direction.normalized * length));
                    if (hit)
                    {
                        Vector3 impact = HectonFloatingOrigin.ToRuntimePosition(hits[i].HitAUP);
                        if (IsFinite(impact))
                            Gizmos.DrawSphere(impact, 0.055f);
                    }
                }
            }

            if (!drawAabbs)
                return;

            Matrix4x4 previous = Gizmos.matrix;
            int primitiveDrawCount = math.min(maxDrawCount, primitiveCount);
            for (int i = 0; i < primitiveDrawCount; i++)
            {
                AABBPrimitiveDTO primitive = primitives[i];
                if ((primitive.Flags & AABBPrimitiveFlags.Active) == 0u)
                    continue;

                Vector3 center = HectonFloatingOrigin.ToRuntimePosition(primitive.CenterAUP);
                Vector3 half = new Vector3(primitive.HalfExtents.x, primitive.HalfExtents.y, primitive.HalfExtents.z);
                Quaternion rotation = new Quaternion(
                    primitive.Rotation.value.x,
                    primitive.Rotation.value.y,
                    primitive.Rotation.value.z,
                    primitive.Rotation.value.w);
                if (!IsFinite(center) || !IsFinite(half))
                    continue;

                Gizmos.color = (primitive.Flags & AABBPrimitiveFlags.Root) != 0u ? Color.cyan : Color.blue;
                Gizmos.matrix = Matrix4x4.TRS(center, rotation, half * 2f);
                Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
            }

            Gizmos.matrix = previous;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
#endif
