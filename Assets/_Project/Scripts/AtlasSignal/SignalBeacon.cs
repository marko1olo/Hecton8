using System.Runtime.CompilerServices;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Narrative;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    public struct SignalBeaconTelemetry
    {
        public Vector3 RuntimePosition;
        public uint LinkedAudioLogHash;
        public uint FragmentHash;
        public uint RecoveredBits;
        public float Strength01;
        public float AverageDistanceSqMeters;
        public float ErrorNoise01;
        public float Static01;
        public int FragmentIndex;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Atlas Signal/Signal Beacon")]
    public sealed class SignalBeacon : MonoBehaviour, IUpdatable
    {
        private const float DefaultBipPeriodSeconds = 0.1f;
        [Header("AUP Triangulation Points")]
        [SerializeField] private Vector3 aupPoint0 = new Vector3(0f, -5000f, 0f);
        [SerializeField] private Vector3 aupPoint1 = new Vector3(80f, -5000f, 70f);
        [SerializeField] private Vector3 aupPoint2 = new Vector3(-90f, -5000f, 40f);
        [SerializeField, Min(1f)] private float maxSignalRangeMeters = 800f;

        [Header("Interference")]
        [SerializeField, Range(0f, 1f)] private float baseErrorNoise01 = 0.04f;
        [SerializeField, Range(1f, 16f)] private float caveErrorNoiseMultiplier = 4f;
        [SerializeField] private bool publishStaticToShader = true;

        [Header("Acoustic Breadcrumb")]
        [SerializeField] private FieldTargetRole acousticRole = FieldTargetRole.DistressBeacon;
        [SerializeField] private int acousticSourceId = 10;
        [SerializeField, Range(0f, 1f)] private float bipIntensity01 = 0.42f;
        [SerializeField, Min(0f)] private float bipRadiusMeters = 100f;
        [SerializeField, Min(0.02f)] private float bipPeriodSeconds = DefaultBipPeriodSeconds;
        [SerializeField, Range(0f, 1f)] private float minimumBipStrength01 = 0.02f;

        [Header("Encrypted Fragment")]
        [SerializeField] private uint linkedAudioLogHash;
        [SerializeField] private uint fragmentHash;
        [SerializeField, Range(0, 3)] private int fragmentIndex;
        [SerializeField, Range(0f, 1f)] private float fragmentRecoveryStrength01 = 0.86f;

        [Header("Runtime Cadence")]
        [SerializeField, Min(0.02f)] private float signalSolveIntervalSeconds = 0.1f;

        private Transform _playerTransform;
        private AbsoluteUniversePosition _pointAup0;
        private AbsoluteUniversePosition _pointAup1;
        private AbsoluteUniversePosition _pointAup2;
        private Vector3 _cachedPoint0;
        private Vector3 _cachedPoint1;
        private Vector3 _cachedPoint2;
        private float _solveTimer;
        private float _bipTimer;
        private bool _aupCacheValid;
        private bool _registered;
        private bool _fragmentRecovered;
        private SignalBeaconTelemetry _telemetry;

        private static readonly int _ShaderSignalStatic = Shader.PropertyToID("_AtlasSignalStatic");

        public SignalBeaconTelemetry Telemetry => _telemetry;
        public float Strength01 => _telemetry.Strength01;
        public float ErrorNoise01 => _telemetry.ErrorNoise01;
        public uint LinkedAudioLogHash => linkedAudioLogHash;
        public uint FragmentHash => fragmentHash;

        private void OnEnable()
        {
            RefreshAupCache(force: true);
            SignalBeaconRegistry.Register(this);
            TryRegisterTick();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            SignalBeaconRegistry.Unregister(this);
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            SignalBeaconRegistry.Unregister(this);
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            _solveTimer += safeDeltaTime;
            _bipTimer += safeDeltaTime;

            float solvePeriod = math.max(0.02f, signalSolveIntervalSeconds);
            if (_solveTimer >= solvePeriod)
            {
                _solveTimer = 0f;
                SolveTelemetry();
            }

            float safeBipPeriod = math.max(0.02f, bipPeriodSeconds);
            if (_bipTimer >= safeBipPeriod)
            {
                _bipTimer = 0f;
                EmitBreadcrumb();
            }
        }

        private void SolveTelemetry()
        {
            if (_playerTransform == null)
            {
                ResolvePlayer();
                if (_playerTransform == null)
                    return;
            }

            RefreshAupCache(force: false);

            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(_playerTransform.position);
            float caveMultiplier = ResolveCaveErrorMultiplier();
            SignalBeaconMath.SolveTriangulatedStrength(
                in playerAup,
                in _pointAup0,
                in _pointAup1,
                in _pointAup2,
                maxSignalRangeMeters,
                baseErrorNoise01,
                caveMultiplier,
                out SignalBeaconSolveResult result);

            _telemetry.RuntimePosition = transform.position;
            _telemetry.LinkedAudioLogHash = linkedAudioLogHash;
            _telemetry.FragmentHash = fragmentHash;
            _telemetry.FragmentIndex = fragmentIndex;
            _telemetry.Strength01 = result.Strength01;
            _telemetry.AverageDistanceSqMeters = result.AverageDistanceSqMeters;
            _telemetry.ErrorNoise01 = result.ErrorNoise01;
            _telemetry.Static01 = result.Static01;

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            _telemetry.RecoveredBits = audioLogs != null
                ? audioLogs.GetRecoveredEncryptedBits(linkedAudioLogHash)
                : 0u;

            if (!_fragmentRecovered && result.Strength01 >= fragmentRecoveryStrength01)
                TryRecoverFragment();

            if (publishStaticToShader)
                Shader.SetGlobalFloat(_ShaderSignalStatic, result.Static01);
        }

        private void EmitBreadcrumb()
        {
            if (_telemetry.Strength01 < minimumBipStrength01)
                return;

            AcousticPingEvent pingEvent = new AcousticPingEvent(
                transform.position,
                bipRadiusMeters,
                math.saturate(bipIntensity01 * math.max(0.1f, _telemetry.Strength01)),
                bipPeriodSeconds,
                acousticRole,
                acousticSourceId,
                10f);

            PhysicsEventBus.NotifyAcousticPing(in pingEvent);
        }

        private bool TryRecoverFragment()
        {
            if (linkedAudioLogHash == 0u || fragmentHash == 0u)
                return false;

            AudioLogSystem audioLogs = GlobalRegistry.AudioLogs;
            if (audioLogs == null)
                return false;

            _fragmentRecovered = audioLogs.RecoverEncryptedFragment(linkedAudioLogHash, fragmentHash);
            _telemetry.RecoveredBits = audioLogs.GetRecoveredEncryptedBits(linkedAudioLogHash);
            return _fragmentRecovered;
        }

        private void RefreshAupCache(bool force)
        {
            if (!force &&
                _aupCacheValid &&
                _cachedPoint0 == aupPoint0 &&
                _cachedPoint1 == aupPoint1 &&
                _cachedPoint2 == aupPoint2)
            {
                return;
            }

            _cachedPoint0 = aupPoint0;
            _cachedPoint1 = aupPoint1;
            _cachedPoint2 = aupPoint2;
            _pointAup0 = AbsoluteUniversePosition.FromRuntimePosition(aupPoint0);
            _pointAup1 = AbsoluteUniversePosition.FromRuntimePosition(aupPoint1);
            _pointAup2 = AbsoluteUniversePosition.FromRuntimePosition(aupPoint2);
            _aupCacheValid = true;
        }

        private float ResolveCaveErrorMultiplier()
        {
            if (GlobalRegistry.Audio is SpatialAudioManager spatialAudio &&
                spatialAudio.IsListenerInsideCaveVolume)
            {
                return math.lerp(1f, math.max(1f, caveErrorNoiseMultiplier), spatialAudio.ListenerCaveInterior01);
            }

            return 1f;
        }

        private void ResolvePlayer()
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                _playerTransform = playerContext.PlayerTransform;
                return;
            }

            SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform);
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregisterTick()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
            _registered = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            maxSignalRangeMeters = math.max(1f, maxSignalRangeMeters);
            signalSolveIntervalSeconds = math.max(0.02f, signalSolveIntervalSeconds);
            bipPeriodSeconds = math.max(0.02f, bipPeriodSeconds);
            if (bipRadiusMeters < 0f)
                bipRadiusMeters = 0f;
        }
#endif
    }

    public static class SignalBeaconRegistry
    {
        private const int Capacity = 32;
        // COLD ALLOC: SignalBeacon[32] - active hash-only signal beacon registry - owner: SignalBeaconRegistry
        private static readonly SignalBeacon[] _beacons = new SignalBeacon[Capacity];
        private static int _count;

        public static int Count => _count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _beacons.Length; i++)
                _beacons[i] = null;

            _count = 0;
        }

        public static void Register(SignalBeacon beacon)
        {
            if (beacon == null)
                return;

            for (int i = 0; i < _beacons.Length; i++)
            {
                if (ReferenceEquals(_beacons[i], beacon))
                    return;
            }

            for (int i = 0; i < _beacons.Length; i++)
            {
                if (_beacons[i] != null)
                    continue;

                _beacons[i] = beacon;
                _count++;
                return;
            }
        }

        public static void Unregister(SignalBeacon beacon)
        {
            if (beacon == null)
                return;

            for (int i = 0; i < _beacons.Length; i++)
            {
                if (!ReferenceEquals(_beacons[i], beacon))
                    continue;

                _beacons[i] = null;
                if (_count > 0)
                    _count--;
                return;
            }
        }

        public static bool TryGetDominant(out SignalBeaconTelemetry telemetry)
        {
            telemetry = default;
            float strongest = 0f;
            bool found = false;

            for (int i = 0; i < _beacons.Length; i++)
            {
                SignalBeacon beacon = _beacons[i];
                if (beacon == null || !beacon.isActiveAndEnabled)
                    continue;

                SignalBeaconTelemetry candidate = beacon.Telemetry;
                if (found && candidate.Strength01 <= strongest)
                    continue;

                strongest = candidate.Strength01;
                telemetry = candidate;
                found = true;
            }

            return found;
        }

        public static bool TryGetDominantTelemetry(out float strength01, out float static01)
        {
            strength01 = 0f;
            static01 = 0f;
            float strongest = 0f;
            bool found = false;

            for (int i = 0; i < _beacons.Length; i++)
            {
                SignalBeacon beacon = _beacons[i];
                if (beacon == null || !beacon.isActiveAndEnabled)
                    continue;

                SignalBeaconTelemetry candidate = beacon.Telemetry;
                if (found && candidate.Strength01 <= strongest)
                    continue;

                strongest = candidate.Strength01;
                strength01 = candidate.Strength01;
                static01 = candidate.Static01;
                found = true;
            }

            return found;
        }
    }

    public struct SignalBeaconSolveResult
    {
        public float Strength01;
        public float AverageDistanceSqMeters;
        public float ErrorNoise01;
        public float Static01;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal static class SignalBeaconMath
    {
        private const double OneThird = 1d / 3d;

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SolveTriangulatedStrength(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition point0,
            in AbsoluteUniversePosition point1,
            in AbsoluteUniversePosition point2,
            float maxRangeMeters,
            float baseErrorNoise01,
            float errorNoiseMultiplier,
            out SignalBeaconSolveResult result)
        {
            double safeRange = math.max(0.001f, maxRangeMeters);
            double safeRangeSq = safeRange * safeRange;
            double distanceSq0 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point0);
            double distanceSq1 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point1);
            double distanceSq2 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point2);
            double averageDistanceSq = (distanceSq0 + distanceSq1 + distanceSq2) * OneThird;
            float strength = averageDistanceSq >= safeRangeSq
                ? 0f
                : math.saturate((float)(1d - (averageDistanceSq / safeRangeSq)));
            float errorNoise = math.saturate(baseErrorNoise01 * math.max(1f, errorNoiseMultiplier));

            result = new SignalBeaconSolveResult
            {
                Strength01 = strength,
                AverageDistanceSqMeters = (float)math.min(averageDistanceSq, (double)float.MaxValue),
                ErrorNoise01 = errorNoise,
                Static01 = math.saturate((1f - strength) + errorNoise)
            };
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSineWaveMatch(
            float targetFrequencyHz,
            float targetPhase01,
            float inputFrequencyHz,
            float inputPhase01,
            float frequencyToleranceHz,
            float phaseTolerance01)
        {
            float safeFrequencyTolerance = math.max(0.001f, frequencyToleranceHz);
            float safePhaseTolerance = math.max(0.001f, phaseTolerance01);
            float frequencyError01 = math.saturate(math.abs(inputFrequencyHz - targetFrequencyHz) / safeFrequencyTolerance);
            float phaseDelta = math.abs(math.frac(inputPhase01 - targetPhase01 + 0.5f) - 0.5f);
            float phaseError01 = math.saturate(phaseDelta / safePhaseTolerance);
            float waveSampleError = math.abs(math.sin(inputPhase01 * math.PI * 2f) - math.sin(targetPhase01 * math.PI * 2f)) * 0.5f;
            return math.saturate(1f - ((frequencyError01 * 0.55f) + (phaseError01 * 0.35f) + (waveSampleError * 0.10f)));
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MergeRecoveredBits(uint recoveredBits, uint fragmentBitMask)
        {
            return (recoveredBits | fragmentBitMask) & 0xFu;
        }
    }
}
