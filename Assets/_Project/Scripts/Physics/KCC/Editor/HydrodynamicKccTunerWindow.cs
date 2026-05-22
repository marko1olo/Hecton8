#if UNITY_EDITOR
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Physics.KCC.Editor
{
    public sealed class HydrodynamicKccTunerWindow : EditorWindow
    {
        private Slider _baseDrag;
        private Slider _fluidDensity;
        private Slider _maxSpeed;
        private Slider _gravityMultiplier;
        private Slider _buoyancyScalar;
        private Slider _quality;
        private Slider _maxSlopeAngle;
        private Slider _currentAdvection;
        private Slider _environmentFriction;
        private Slider _exhaustionPenalty;
        private EnvironmentGraphElement _graph;
        private double _nextGraphRepaintTime;

        [MenuItem("HECTON-8/Kinematics/Hydrodynamic KCC Tuner")]
        public static void Open()
        {
            GetWindow<HydrodynamicKccTunerWindow>("Hydro KCC");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _baseDrag = AddSlider("Base Drag", 0f, 3f);
            _fluidDensity = AddSlider("Fluid Density", 0f, 2.5f);
            _maxSpeed = AddSlider("Max Speed", 0.1f, 18f);
            _gravityMultiplier = AddSlider("Gravity Multiplier", 0f, 2.5f);
            _buoyancyScalar = AddSlider("Buoyancy", 0f, 2.5f);
            _quality = AddSlider("Global Quality Weight", 0f, 1f);
            _maxSlopeAngle = AddSlider("Max Slope Angle", 1f, 89f);
            _currentAdvection = AddSlider("Current Advection Scalar", 0f, 8f);
            _environmentFriction = AddSlider("Environment Friction", 0f, 8f);
            _exhaustionPenalty = AddSlider("Exhaustion Penalty Max", 0f, 1f);

            _graph = new EnvironmentGraphElement();
            _graph.style.height = 112;
            _graph.style.marginTop = 8;
            rootVisualElement.Add(_graph);

            RegisterCallbacks();
            ReadFromVault();
        }

        private Slider AddSlider(string label, float low, float high)
        {
            Slider slider = new Slider(label, low, high);
            slider.showInputField = true;
            slider.style.marginBottom = 4;
            rootVisualElement.Add(slider);
            return slider;
        }

        private void RegisterCallbacks()
        {
            _baseDrag.RegisterValueChangedCallback(_ => WriteToVault());
            _fluidDensity.RegisterValueChangedCallback(_ => WriteToVault());
            _maxSpeed.RegisterValueChangedCallback(_ => WriteToVault());
            _gravityMultiplier.RegisterValueChangedCallback(_ => WriteToVault());
            _buoyancyScalar.RegisterValueChangedCallback(_ => WriteToVault());
            _quality.RegisterValueChangedCallback(_ => WriteToVault());
            _maxSlopeAngle.RegisterValueChangedCallback(_ => WriteToVault());
            _currentAdvection.RegisterValueChangedCallback(_ => WriteToVault());
            _environmentFriction.RegisterValueChangedCallback(_ => WriteToVault());
            _exhaustionPenalty.RegisterValueChangedCallback(_ => WriteToVault());
        }

        private void Update()
        {
            double now = EditorApplication.timeSinceStartup;
            if (_graph != null && now >= _nextGraphRepaintTime)
            {
                _nextGraphRepaintTime = now + 0.05d;
                _graph.MarkDirtyRepaint();
            }
        }

        private void ReadFromVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            HydrodynamicKccTuningDTO tuning = TryReadBuffer(
                    vault,
                    BufferID.ShinobuHydroKccTuning,
                    out NativeArray<HydrodynamicKccTuningDTO> tuningBuffer)
                ? tuningBuffer[0]
                : DefaultTuning();
            if (tuning.MaxSpeed <= 0f)
                tuning = DefaultTuning();

            SetSliderWithoutNotify(_baseDrag, tuning.BaseDrag);
            SetSliderWithoutNotify(_fluidDensity, tuning.FluidDensity);
            SetSliderWithoutNotify(_maxSpeed, tuning.MaxSpeed);
            SetSliderWithoutNotify(_gravityMultiplier, tuning.GravityMultiplier);
            SetSliderWithoutNotify(_buoyancyScalar, tuning.BuoyancyScalar);
            SetSliderWithoutNotify(_quality, tuning.GlobalQualityWeight);

            KccEnvironmentProfileDTO environment = TryReadBuffer(
                    vault,
                    BufferID.ShinobuKccEnvironmentProfile,
                    out NativeArray<KccEnvironmentProfileDTO> environmentBuffer)
                ? environmentBuffer[0]
                : DefaultEnvironmentProfile();
            if (environment.MaxSlopeAngle <= 0f)
                environment = DefaultEnvironmentProfile();

            SetSliderWithoutNotify(_maxSlopeAngle, environment.MaxSlopeAngle);
            SetSliderWithoutNotify(_currentAdvection, environment.CurrentAdvectionScalar);
            SetSliderWithoutNotify(_environmentFriction, environment.FrictionCoefficient);
            SetSliderWithoutNotify(_exhaustionPenalty, environment.ExhaustionPenaltyMax);
        }

        private void WriteToVault()
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null)
                return;

            if (!TryAcquireEditorWriteView(
                    vault,
                    BufferID.ShinobuHydroKccTuning,
                    1,
                    out VaultGenerationHandle<HydrodynamicKccTuningDTO> tuningHandle,
                    out NativeArray<HydrodynamicKccTuningDTO> tuningBuffer))
            {
                return;
            }

            bool environmentAcquired = TryAcquireEditorWriteView(
                vault,
                BufferID.ShinobuKccEnvironmentProfile,
                1,
                out VaultGenerationHandle<KccEnvironmentProfileDTO> environmentHandle,
                out NativeArray<KccEnvironmentProfileDTO> environmentBuffer);

            try
            {
                HydrodynamicKccTuningDTO tuning = tuningBuffer[0];
                tuning.BaseDrag = math.max(0f, _baseDrag.value);
                tuning.FluidDensity = math.max(0f, _fluidDensity.value);
                tuning.MaxSpeed = math.max(0.1f, _maxSpeed.value);
                tuning.GravityMultiplier = math.max(0f, _gravityMultiplier.value);
                tuning.BuoyancyScalar = math.max(0f, _buoyancyScalar.value);
                tuning.GlobalQualityWeight = math.saturate(_quality.value);
                tuning.ProfileHash = HydrodynamicKccMath.SourceHash;
                tuning.Flags = 1u;
                tuningBuffer[0] = tuning;

                if (environmentAcquired)
                {
                    environmentBuffer[0] = new KccEnvironmentProfileDTO
                    {
                        MaxSlopeAngle = math.clamp(_maxSlopeAngle.value, 1f, 89f),
                        CurrentAdvectionScalar = math.clamp(_currentAdvection.value, 0f, 8f),
                        FrictionCoefficient = math.clamp(_environmentFriction.value, 0f, 8f),
                        ExhaustionPenaltyMax = math.saturate(_exhaustionPenalty.value)
                    };
                }
            }
            finally
            {
                if (environmentAcquired)
                    vault.ReleaseWriteLock(in environmentHandle, SystemID.CoreDiagnostics);
                vault.ReleaseWriteLock(in tuningHandle, SystemID.CoreDiagnostics);
            }
        }

        private static HydrodynamicKccTuningDTO DefaultTuning()
        {
            return new HydrodynamicKccTuningDTO
            {
                BaseDrag = 0.18f,
                FluidDensity = 1f,
                MaxSpeed = 6f,
                GravityMultiplier = 1f,
                BuoyancyScalar = 1.08f,
                GlobalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight),
                ProfileHash = HydrodynamicKccMath.SourceHash,
                Flags = 1u
            };
        }

        private static bool TryReadBuffer<T>(IDataVault vault, BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.ActiveBurstLockMask == 0u &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool TryAcquireEditorWriteView<T>(
            IDataVault vault,
            BufferID bufferId,
            int requiredLength,
            out VaultGenerationHandle<T> handle,
            out NativeArray<T> buffer)
            where T : struct
        {
            handle = default;
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.ActiveBurstLockMask != 0u)
                return false;

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                vault.TryReadHandle(in existing, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existing;
            }
            else
            {
                if (vault.IsAllocationLocked)
                    return false;

                handle = vault.GetGenerationHandle<T>(
                    bufferId,
                    requiredLength,
                    SystemID.Physics,
                    NativeArrayOptions.ClearMemory);
            }

            if (!vault.TryAcquireWriteLock(in handle, SystemID.CoreDiagnostics, out buffer))
                return false;

            if (buffer.IsCreated && buffer.Length >= requiredLength)
                return true;

            vault.ReleaseWriteLock(in handle, SystemID.CoreDiagnostics);
            buffer = default;
            return false;
        }

        private static KccEnvironmentProfileDTO DefaultEnvironmentProfile()
        {
            return new KccEnvironmentProfileDTO
            {
                MaxSlopeAngle = 48f,
                CurrentAdvectionScalar = 1f,
                FrictionCoefficient = 0.85f,
                ExhaustionPenaltyMax = 0.35f
            };
        }

        private static void SetSliderWithoutNotify(Slider slider, float value)
        {
            if (slider != null)
                slider.SetValueWithoutNotify(value);
        }

        private sealed class EnvironmentGraphElement : VisualElement
        {
            public EnvironmentGraphElement()
            {
                generateVisualContent += OnGenerateVisualContent;
            }

            private void OnGenerateVisualContent(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 1f;
                if (!HydrodynamicKccRuntime.TryGetEditorEnvironmentTelemetryVaultView(out NativeArray<KccEnvironmentTelemetryEntry>.ReadOnly telemetry, out int cursorIndex, out int telemetryLength) ||
                    telemetryLength <= 0)
                {
                    painter.strokeColor = new Color(0.18f, 0.18f, 0.18f, 1f);
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.Stroke();
                    return;
                }

                cursorIndex = math.clamp(cursorIndex, 0, telemetryLength - 1);
                int startIndex = (cursorIndex + 1) % telemetryLength;
                float maxFlow = 0.001f;
                float maxSlope = 1f;
                float maxExhaustion = 1f;
                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = (startIndex + i) % telemetryLength;
                    KccEnvironmentTelemetryEntry entry = telemetry[telemetryIndex];
                    maxFlow = math.max(maxFlow, HydrodynamicKccMath.LengthSafe(entry.AppliedFlow));
                    maxSlope = math.max(maxSlope, entry.SlopeAngleDegrees);
                    maxExhaustion = math.max(maxExhaustion, entry.ExhaustionPenalty);
                }

                DrawSeries(painter, rect, telemetry, telemetryLength, startIndex, maxFlow, 0, new Color(0.05f, 0.75f, 1f, 1f));
                DrawSeries(painter, rect, telemetry, telemetryLength, startIndex, math.max(1f, maxSlope), 1, new Color(1f, 0.35f, 0.08f, 1f));
                DrawSeries(painter, rect, telemetry, telemetryLength, startIndex, math.max(0.001f, maxExhaustion), 2, new Color(0.9f, 0.85f, 0.1f, 1f));
            }

            private static void DrawSeries(
                Painter2D painter,
                Rect rect,
                NativeArray<KccEnvironmentTelemetryEntry>.ReadOnly telemetry,
                int telemetryLength,
                int startIndex,
                float maxValue,
                int series,
                Color color)
            {
                painter.lineWidth = 1.5f;
                painter.strokeColor = color;
                painter.BeginPath();
                for (int i = 0; i < telemetryLength; i++)
                {
                    int telemetryIndex = (startIndex + i) % telemetryLength;
                    KccEnvironmentTelemetryEntry entry = telemetry[telemetryIndex];
                    float value = series == 0
                        ? HydrodynamicKccMath.LengthSafe(entry.AppliedFlow)
                        : series == 1
                            ? entry.SlopeAngleDegrees
                            : entry.ExhaustionPenalty;
                    float x = rect.xMin + rect.width * (i / math.max(1f, telemetryLength - 1f));
                    float y = rect.yMax - rect.height * math.saturate(value / math.max(0.001f, maxValue));
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
