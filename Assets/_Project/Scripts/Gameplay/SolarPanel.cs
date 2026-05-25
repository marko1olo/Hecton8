using Hecton8.Core;
using Hecton8.Power;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Gameplay/Solar Panel")]
    public sealed unsafe class SolarPanel : MonoBehaviour, ISlowTickable, ILateFrameTickable, IPoolable, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener
    {
        private const float SlowTickStepSeconds = 0.1f;
        private const float PowerDirtyDeltaWatts = 0.1f;

        private static SolarPanel[] s_instances;
        private static SolarPanel s_leader;
        private static int s_activeCount;
        private static uint s_lastAppliedOutputFrame = uint.MaxValue;

        [Header("Power")]
        [SerializeField] private string stablePanelId = "solar.panel.00";
        [SerializeField] private uint powerNodeHashID;
        [SerializeField, Range(10f, 5000f)] private float basePower = 200f;
        [SerializeField, Range(0.001f, 4f)] private float baseEfficiencyScalar = 0.147f;
        [SerializeField] private bool deriveEfficiencyFromBasePower = true;
        [SerializeField, Range(0f, 4f)] private float waterTurbidity = 1f;
        [SerializeField, Range(0.0001f, 0.5f)] private float waterAttenuationCoefficient = SolarPowerGenerationConstants.DefaultWaterAttenuationCoefficient;
        [SerializeField, Range(0f, 2f)] private float turbidityMultiplier = SolarPowerGenerationConstants.DefaultTurbidityMultiplier;
        [SerializeField, Range(10f, 2000f)] private float initialIrradianceWatts = SolarPowerGenerationConstants.DefaultSolarIrradianceWatts;
        [SerializeField] private bool useMockSolarConditions;

        [Header("Sea Level")]
        [SerializeField] private float seaLevelRuntimeY;

        private Transform _cachedTransform;
        private int _slot = -1;
        private uint _resolvedPowerNodeHash;
        private float _currentPower;
        private float _depthFactor;
        private float _timeFactor;
        private float _skyFactor = 1f;
        private float _opticalDepthMeters;
        private float _solveAccumulator;
        private bool _isProducing;
        private bool _registeredSlow;
        private bool _registeredLate;
        private bool _registeredHotSwap;
        private IWeatherService _cachedWeatherService;

#if UNITY_EDITOR
        private static readonly GUIContent s_gizmoLabelContent = new GUIContent(); // COLD ALLOC: GUIContent[1] - Scene View solar label scratch - owner: SolarPanel editor gizmo
        private static readonly System.Text.StringBuilder s_gizmoLabelBuilder = new System.Text.StringBuilder(128); // COLD ALLOC: StringBuilder[128] - Scene View solar label formatting - owner: SolarPanel editor gizmo
        private static readonly char[] s_gizmoNumberScratch = new char[16]; // COLD ALLOC: char[16] - editor numeric label scratch - owner: SolarPanel editor gizmo

        private string _editorGizmoLabel;
        private uint _editorGizmoLabelHash;
#endif

        public float PowerRating => math.max(0f, _currentPower);
        public int PowerPriority => 0;
        public bool IsProducing => _isProducing;
        public float CurrentPower => _currentPower;
        public float DepthFactor => _depthFactor;
        public float TimeFactor => _timeFactor;
        public float SkyFactor => _skyFactor;
        public float OpticalDepthMeters => _opticalDepthMeters;
        public uint PowerNodeHashID => _resolvedPowerNodeHash;

        public void OnSpawn()
        {
            TryRegisterRuntime();
        }

        public void OnDespawn()
        {
            TryUnregisterRuntime();
        }

        public void SlowTick()
        {
            if (!ReferenceEquals(this, s_leader))
                return;

            if (!SolarPowerGenerationRuntime.TryFinalize())
                return;

            float quality = ResolveGlobalQualityWeight();
            _solveAccumulator = math.min(2f, _solveAccumulator + SlowTickStepSeconds);
            float cadence = ResolveCadenceSeconds(quality);
            if (_solveAccumulator + 0.00001f < cadence)
                return;

            float deltaSeconds = _solveAccumulator;
            _solveAccumulator = 0f;
            WriteAllPanelStates();
            SolarConditionsDTO conditions = BuildConditions(quality, deltaSeconds);
            SolarPowerGenerationRuntime.TrySchedule(s_activeCount, in conditions, deltaSeconds, useMockSolarConditions);
        }

        public void LateFrameTick()
        {
            if (!ReferenceEquals(this, s_leader))
                return;

            if (!SolarPowerGenerationRuntime.TryFinalize())
                return;

            if (!SolarPowerGenerationRuntime.TryGetCompletedOutputFrameIndex(out uint frameIndex) ||
                frameIndex == s_lastAppliedOutputFrame)
            {
                return;
            }

            if (!SolarPowerGenerationRuntime.TryReadOutputSnapshot(out NativeArray<SolarPanelOpticalOutputDTO>.ReadOnly outputs, out uint snapshotFrameIndex) ||
                snapshotFrameIndex != frameIndex)
            {
                return;
            }

            int limit = math.min(s_activeCount, outputs.Length);
            for (int i = 0; i < limit; i++)
            {
                SolarPanel panel = s_instances[i];
                if (panel != null)
                {
                    SolarPanelOpticalOutputDTO output = outputs[i];
                    panel.ApplyOutput(in output);
                }
            }

            s_lastAppliedOutputFrame = frameIndex;
        }

        private void Awake()
        {
            _cachedTransform = transform;
            ResolveIdentity();
            SanitizeInspectorValues();
        }

        private void OnEnable()
        {
            TryRegisterRuntime();
        }

        private void OnDisable()
        {
            TryUnregisterRuntime();
        }

        private void OnDestroy()
        {
            TryUnregisterRuntime();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            SolarPowerGenerationRuntime.ResetForSubsystemRegistration();
            // COLD ALLOC: SolarPanel[512] - scene facade table for authored solar transforms - owner: SolarPanel
            s_instances = new SolarPanel[SolarPowerGenerationConstants.DefaultPanelCapacity];
            s_leader = null;
            s_activeCount = 0;
            s_lastAppliedOutputFrame = uint.MaxValue;
        }

        private void TryRegisterRuntime()
        {
            if (_slot >= 0 || !Application.isPlaying)
                return;

            EnsureInstanceTable();
            if (s_activeCount >= SolarPowerGenerationConstants.DefaultPanelCapacity)
                return;

            ResolveIdentity();
            _slot = s_activeCount;
            s_instances[_slot] = this;
            s_activeCount++;
            WriteSlotStateFromInstance();
            RefreshLeader();
        }

        private void TryUnregisterRuntime()
        {
            if (_slot < 0)
                return;

            int removedSlot = _slot;
            int lastSlot = s_activeCount - 1;
            SolarPanel moved = lastSlot >= 0 ? s_instances[lastSlot] : null;
            if (removedSlot != lastSlot && moved != null)
            {
                s_instances[removedSlot] = moved;
                moved._slot = removedSlot;
                moved.WriteSlotStateFromInstance();
            }

            if (lastSlot >= 0)
                s_instances[lastSlot] = null;

            s_activeCount = math.max(0, s_activeCount - 1);
            _slot = -1;
            SolarPowerGenerationRuntime.TryClearPanelState(lastSlot);
            RefreshLeader();
            UnregisterLeaderLanes();
        }

        private static void EnsureInstanceTable()
        {
            if (s_instances == null || s_instances.Length != SolarPowerGenerationConstants.DefaultPanelCapacity)
            {
                // COLD ALLOC: SolarPanel[512] - subsystem-registration fallback for disabled domain reload - owner: SolarPanel
                s_instances = new SolarPanel[SolarPowerGenerationConstants.DefaultPanelCapacity];
            }
        }

        public void OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Weather)
                _cachedWeatherService = currentService as IWeatherService;
        }

        public void OnGlobalRegistryServiceReplaced(GlobalRegistryServiceSlot serviceSlot, object previousService, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Weather)
                _cachedWeatherService = currentService as IWeatherService;
        }

        private static void RefreshLeader()
        {
            SolarPanel next = s_activeCount > 0 && s_instances != null ? s_instances[0] : null;
            if (ReferenceEquals(s_leader, next))
                return;

            SolarPanel previous = s_leader;
            s_leader = next;
            previous?.UnregisterLeaderLanes();
            next?.RegisterLeaderLanes();
        }

        private void RegisterLeaderLanes()
        {
            if (!Application.isPlaying)
                return;

            _cachedWeatherService = GlobalRegistry.Weather;
            if (!_registeredHotSwap)
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
            if (!_registeredSlow)
                _registeredSlow = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            if (!_registeredLate)
                _registeredLate = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void UnregisterLeaderLanes()
        {
            if (_registeredLate)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = false;
            }

            if (_registeredSlow)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlow = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }

            _cachedWeatherService = null;
        }

        private static void WriteAllPanelStates()
        {
            if (!SolarPowerGenerationRuntime.TryAcquirePanelStateWrite(out NativeArray<SolarPanelStateDTO> states))
                return;

            try
            {
                int limit = math.min(s_activeCount, states.Length);
                double3 runtimeOrigin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
                SolarPanelStateDTO* statePtr = (SolarPanelStateDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(states);
                for (int i = 0; i < limit; i++)
                {
                    SolarPanel panel = s_instances[i];
                    SolarPanelStateDTO state = panel != null ? panel.BuildStateRow(runtimeOrigin) : default;
                    UnsafeUtility.AsRef<SolarPanelStateDTO>(statePtr + i) = state;
                }
            }
            finally
            {
                SolarPowerGenerationRuntime.ReleasePanelStateWrite();
            }
        }

        private bool WriteSlotStateFromInstance()
        {
            if (_slot < 0)
                return false;

            double3 runtimeOrigin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            SolarPanelStateDTO state = BuildStateRow(runtimeOrigin);
            return SolarPowerGenerationRuntime.TryWritePanelState(_slot, in state);
        }

        private SolarPanelStateDTO BuildStateRow(double3 runtimeOrigin)
        {
            Vector3 position = _cachedTransform != null ? _cachedTransform.position : transform.position;
            SolarPanelStateDTO state = default;
            state.PanelAUP = runtimeOrigin + new double3(position.x, position.y, position.z);
            state.BaseEfficiencyScalar = ResolvePanelEfficiencyScalar();
            state.PowerNodeHashID = _resolvedPowerNodeHash;
            return state;
        }

        private SolarConditionsDTO BuildConditions(float quality, float deltaSeconds)
        {
            double3 runtimeOrigin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            CelestialRuntimeSnapshot celestial;
            bool celestialValid = SolarPowerGenerationRuntime.TryReadCelestialSnapshot(out celestial) &&
                                  (celestial.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u &&
                                  math.all(math.isfinite(celestial.SunDirection)) &&
                                  math.lengthsq(celestial.SunDirection) > 0.000001f;

            SolarConditionsDTO tuned = default;
            SolarPowerGenerationRuntime.TryGetTuning(out tuned);
            SolarConditionsDTO conditions = default;
            conditions.RuntimeOriginAUP = runtimeOrigin;
            conditions.SeaLevelAUP = runtimeOrigin + new double3(0.0, seaLevelRuntimeY + (celestialValid ? celestial.TideHeightMeters : 0f), 0.0);
            conditions.SunDirection = celestialValid ? celestial.SunDirection : new float3(0f, 1f, 0f);
            conditions.WaterAttenuationCoefficient = tuned.WaterAttenuationCoefficient > 0f ? tuned.WaterAttenuationCoefficient : waterAttenuationCoefficient;
            conditions.WaterTurbidity = math.max(waterTurbidity, ResolveStormTurbidity());
            conditions.TurbidityMultiplier = tuned.TurbidityMultiplier > 0f ? tuned.TurbidityMultiplier : turbidityMultiplier;
            conditions.InitialIntensityWatts = tuned.InitialIntensityWatts > 0f ? tuned.InitialIntensityWatts : initialIrradianceWatts;
            conditions.BaseEfficiencyScalar = tuned.BaseEfficiencyScalar;
            conditions.GlobalQualityWeight = quality;
            conditions.SimulationTimeSeconds = ResolveRuntimeTimeSeconds();
            conditions.DeltaTimeSeconds = deltaSeconds;
            conditions.VoxelSdfCellSize = new float3(1f);
            conditions.VoxelSdfRangeMeters = SolarPowerGenerationConstants.DefaultSdfRangeMeters;
            return conditions;
        }

        private void ApplyOutput(in SolarPanelOpticalOutputDTO output)
        {
            if (_slot < 0)
                return;

            _currentPower = math.max(0f, math.isfinite(output.GeneratedWatts) ? output.GeneratedWatts : 0f);
            _depthFactor = ResolveDepthFactor(output.OpticalDepthMeters);
            _timeFactor = math.saturate(output.AngleMultiplier);
            _skyFactor = math.saturate(output.ShadowMultiplier);
            _opticalDepthMeters = math.max(0f, output.OpticalDepthMeters);
            _isProducing = _currentPower > PowerDirtyDeltaWatts;
        }

        private void ResolveIdentity()
        {
            _resolvedPowerNodeHash = powerNodeHashID != 0u
                ? powerNodeHashID
                : HashString(stablePanelId);
        }

        private float ResolvePanelEfficiencyScalar()
        {
            if (SolarPowerGenerationRuntime.TryGetTuning(out SolarConditionsDTO tuned) && tuned.BaseEfficiencyScalar > 0f)
                return math.max(0f, tuned.BaseEfficiencyScalar);

            if (deriveEfficiencyFromBasePower)
            {
                float invIrradiance = math.rcp(math.max(1f, initialIrradianceWatts));
                return math.max(0f, basePower * invIrradiance);
            }

            return math.max(0f, baseEfficiencyScalar);
        }

        private static float ResolveDepthFactor(float opticalDepth)
        {
            if (!math.isfinite(opticalDepth))
                return 0f;

            float x = math.clamp(opticalDepth, 0f, 40f);
            return math.saturate(math.rcp(1f + x + 0.5f * x * x));
        }

        private static float ResolveCadenceSeconds(float quality)
        {
            float q = math.saturate(quality);
            return math.lerp(0.05f, 0.5f, 1.0f - q);
        }

        private static float ResolveGlobalQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(weight) ? weight : 1f);
        }

        private static float ResolveRuntimeTimeSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now < 0d)
                return 0f;

            return (float)math.min(now, (double)float.MaxValue);
        }

        private float ResolveStormTurbidity()
        {
            IWeatherService weather = _cachedWeatherService;
            if (weather == null || !weather.IsInitialized)
                return 1f;

            float intensity = weather.WeatherIntensity;
            return math.lerp(1f, 2.4f, math.saturate(math.isfinite(intensity) ? intensity : 0f));
        }

        private static uint HashString(string value)
        {
            uint hash = 2166136261u;
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c >= 'A' && c <= 'Z')
                        c = (char)(c + 32);
                    hash = (hash ^ unchecked((uint)c)) * 16777619u;
                }
            }

            return hash == 0u ? 1u : hash;
        }

        private void SanitizeInspectorValues()
        {
            if (string.IsNullOrEmpty(stablePanelId))
                stablePanelId = "solar.panel.00";
            basePower = math.max(0f, basePower);
            baseEfficiencyScalar = math.max(0f, baseEfficiencyScalar);
            waterTurbidity = math.clamp(waterTurbidity, 0f, 4f);
            waterAttenuationCoefficient = math.max(0.0001f, waterAttenuationCoefficient);
            turbidityMultiplier = math.max(0f, turbidityMultiplier);
            initialIrradianceWatts = math.max(10f, initialIrradianceWatts);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeInspectorValues();
            ResolveIdentity();
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position;
            float3 sun = new float3(0f, 1f, 0f);
            SolarPanelOpticalOutputDTO output = default;
            bool hasOutput = false;
            if (Application.isPlaying)
            {
                if (SolarPowerGenerationRuntime.TryReadPanelState(_slot, out SolarPanelStateDTO state))
                {
                    double3 runtimePosition = state.PanelAUP - HectonFloatingOrigin.CurrentTotalOffsetDouble;
                    origin = new Vector3((float)runtimePosition.x, (float)runtimePosition.y, (float)runtimePosition.z);
                }

                hasOutput = SolarPowerGenerationRuntime.TryReadOutput(_slot, out output);
                if (SolarPowerGenerationRuntime.TryReadCelestialSnapshot(out CelestialRuntimeSnapshot snapshot) &&
                    math.all(math.isfinite(snapshot.SunDirection)) &&
                    math.lengthsq(snapshot.SunDirection) > 0.000001f)
                {
                    sun = math.normalize(snapshot.SunDirection);
                }
            }

            float angle = hasOutput ? math.saturate(output.AngleMultiplier) : math.saturate(_timeFactor);
            Gizmos.color = Color.Lerp(new Color(1f, 0.2f, 0.15f, 0.75f), new Color(1f, 0.9f, 0.2f, 0.75f), angle);
            Vector3 direction = new Vector3(sun.x, sun.y, sun.z);
            Gizmos.DrawLine(origin, origin + direction * 12f);

            if (Application.isPlaying)
            {
                float watts = hasOutput ? output.GeneratedWatts : _currentPower;
                float depth = hasOutput ? output.OpticalDepthMeters : _opticalDepthMeters;
                float shadow = hasOutput ? output.ShadowMultiplier : _skyFactor;
                s_gizmoLabelContent.text = ResolveEditorGizmoLabel(watts, depth, angle, shadow);
                Handles.Label(
                    origin + Vector3.up * 2f,
                    s_gizmoLabelContent);
            }
        }

        private string ResolveEditorGizmoLabel(float watts, float depth, float angle, float shadow)
        {
            int watts10 = QuantizeEditorFloat(watts, 1);
            int depth100 = QuantizeEditorFloat(depth, 2);
            int angle100 = QuantizeEditorFloat(angle, 2);
            int shadow100 = QuantizeEditorFloat(shadow, 2);
            uint hash = 2166136261u;
            hash = MixEditorHash(hash, PowerNodeHashID);
            hash = MixEditorHash(hash, (uint)watts10);
            hash = MixEditorHash(hash, (uint)depth100);
            hash = MixEditorHash(hash, (uint)angle100);
            hash = MixEditorHash(hash, (uint)shadow100);

            if (_editorGizmoLabel != null && hash == _editorGizmoLabelHash)
                return _editorGizmoLabel;

            _editorGizmoLabelHash = hash;
            System.Text.StringBuilder builder = s_gizmoLabelBuilder;
            builder.Length = 0;
            builder.Append("Solar ");
            AppendEditorHex8(builder, PowerNodeHashID);
            builder.Append('\n');
            builder.Append("GeneratedWatts ");
            AppendEditorFixed(builder, watts10, 1);
            builder.Append('\n');
            builder.Append("Beer d=");
            AppendEditorFixed(builder, depth100, 2);
            builder.Append('\n');
            builder.Append("Angle ");
            AppendEditorFixed(builder, angle100, 2);
            builder.Append('\n');
            builder.Append("Shadow ");
            AppendEditorFixed(builder, shadow100, 2);
            _editorGizmoLabel = builder.ToString();
            return _editorGizmoLabel;
        }

        private static int QuantizeEditorFloat(float value, int decimals)
        {
            if (!math.isfinite(value))
                return 0;

            float scale = decimals == 2 ? 100f : 10f;
            float scaled = math.round(value * scale);
            if (scaled > int.MaxValue)
                return int.MaxValue;
            if (scaled < int.MinValue + 1f)
                return int.MinValue + 1;
            return (int)scaled;
        }

        private static void AppendEditorFixed(System.Text.StringBuilder builder, int scaledValue, int decimals)
        {
            int scale = decimals == 2 ? 100 : 10;
            if (scaledValue < 0)
            {
                builder.Append('-');
                scaledValue = -scaledValue;
            }

            int whole = scaledValue / scale;
            int fraction = scaledValue - whole * scale;
            AppendEditorUInt(builder, (uint)whole);
            builder.Append('.');
            if (decimals == 2 && fraction < 10)
                builder.Append('0');
            AppendEditorUInt(builder, (uint)fraction);
        }

        private static void AppendEditorHex8(System.Text.StringBuilder builder, uint value)
        {
            for (int shift = 28; shift >= 0; shift -= 4)
            {
                int nibble = (int)((value >> shift) & 0xFu);
                builder.Append((char)(nibble < 10 ? '0' + nibble : 'A' + nibble - 10));
            }
        }

        private static void AppendEditorUInt(System.Text.StringBuilder builder, uint value)
        {
            if (value == 0u)
            {
                builder.Append('0');
                return;
            }

            int cursor = s_gizmoNumberScratch.Length;
            while (value > 0u && cursor > 0)
            {
                uint digit = value % 10u;
                value /= 10u;
                s_gizmoNumberScratch[--cursor] = (char)('0' + (int)digit);
            }

            for (int i = cursor; i < s_gizmoNumberScratch.Length; i++)
                builder.Append(s_gizmoNumberScratch[i]);
        }

        private static uint MixEditorHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }
#endif
    }
}
