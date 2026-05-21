using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Audio;
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
    /// <summary>
    /// Snapshot published by active Atlas signal beacons for PDA and HUD consumers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct SignalBeaconTelemetry
    {
        /// <summary>Camera-relative runtime position of the triangulated beacon centroid.</summary>
        [FieldOffset(0)] public Vector3 RuntimePosition;

        /// <summary>Stable hash of the linked encrypted audio log.</summary>
        [FieldOffset(12)] public uint LinkedAudioLogHash;

        /// <summary>Stable hash of the authored encrypted fragment recovered by this beacon.</summary>
        [FieldOffset(16)] public uint FragmentHash;

        /// <summary>Recovered 4-bit mask for the linked encrypted audio log.</summary>
        [FieldOffset(20)] public uint RecoveredBits;

        /// <summary>Normalized signal strength from AUP distance-squared triangulation.</summary>
        [FieldOffset(24)] public float Strength01;

        /// <summary>Average squared distance from the player AUP to the three authored AUP points.</summary>
        [FieldOffset(28)] public float AverageDistanceSqMeters;

        /// <summary>Normalized noise scalar after cave interference.</summary>
        [FieldOffset(32)] public float ErrorNoise01;

        /// <summary>Normalized fake static scalar for HUD/shader presentation.</summary>
        [FieldOffset(36)] public float Static01;

        /// <summary>Authored fragment bit index, zero through three.</summary>
        [FieldOffset(40)] public int FragmentIndex;
        [FieldOffset(44)] private uint _pad0;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Atlas Signal/Signal Beacon")]
    public sealed class SignalBeacon : MonoBehaviour, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float DefaultBipPeriodSeconds = 0.1f;
        private const double TriangulationCentroidWeight = 1d / 3d;
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

        private HectonPlayerMovement _playerMovement;
        private AbsoluteUniversePosition _pointAup0;
        private AbsoluteUniversePosition _pointAup1;
        private AbsoluteUniversePosition _pointAup2;
        private AbsoluteUniversePosition _beaconAup;
        private Vector3 _cachedBeaconRuntimePosition;
        private int _cachedBeaconRuntimeFrame = -1;
        private Vector3 _cachedPoint0;
        private Vector3 _cachedPoint1;
        private Vector3 _cachedPoint2;
        private float _solveTimer;
        private float _bipTimer;
        private bool _aupCacheValid;
        private bool _beaconAupCacheValid;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _fragmentRecovered;
        private int _registrySlot = -1;
        private AudioLogSystem _audioLogs;
        private SpatialAudioManager _spatialAudio;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private SignalBeaconTelemetry _telemetry;

        private static readonly int _ShaderSignalStatic = Shader.PropertyToID("_AtlasSignalStatic");
        private static float _lastPublishedShaderStatic01 = -1f;

        /// <summary>Latest published beacon telemetry.</summary>
        public SignalBeaconTelemetry Telemetry => _telemetry;

        /// <summary>Latest normalized signal strength.</summary>
        public float Strength01 => _telemetry.Strength01;

        /// <summary>Latest normalized signal error noise.</summary>
        public float ErrorNoise01 => _telemetry.ErrorNoise01;

        /// <summary>Stable hash of the linked encrypted audio log.</summary>
        public uint LinkedAudioLogHash => linkedAudioLogHash;

        /// <summary>Stable hash of this beacon's encrypted fragment.</summary>
        public uint FragmentHash => fragmentHash;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            RefreshAupCache(force: true);
            RefreshBeaconAupCache(force: true);
            _registrySlot = SignalBeaconRegistry.Register(this);
            TryRegisterTick();
            ResolvePlayer();
        }

        private void OnDisable()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            UnregisterBeaconAndRefreshShaderStatic();
        }

        private void OnDestroy()
        {
            TryUnregisterTick();
            TryUnregisterHotSwapListener();
            UnregisterBeaconAndRefreshShaderStatic();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticShaderState()
        {
            _lastPublishedShaderStatic01 = -1f;
        }

        public void Tick(float deltaTime)
        {
            float safeDeltaTime = math.max(0f, deltaTime);
            _solveTimer += safeDeltaTime;
            _bipTimer += safeDeltaTime;

            float solvePeriod = math.max(0.02f, signalSolveIntervalSeconds);
            if (_solveTimer >= solvePeriod)
            {
                _solveTimer = math.min(_solveTimer - solvePeriod, solvePeriod);
                SolveTelemetry();
            }

            float safeBipPeriod = math.max(0.02f, bipPeriodSeconds);
            if (_bipTimer >= safeBipPeriod)
            {
                _bipTimer = math.min(_bipTimer - safeBipPeriod, safeBipPeriod);
                EmitBreadcrumb();
            }
        }

        private void SolveTelemetry()
        {
            RefreshAupCache(force: false);
            if (!_aupCacheValid)
            {
                ClearPublishedTelemetry();
                return;
            }

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
            {
                ClearPublishedTelemetry();
                return;
            }

            Vector3 beaconRuntimePosition = ResolveBeaconRuntimePosition();
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

            _telemetry.RuntimePosition = beaconRuntimePosition;
            _telemetry.LinkedAudioLogHash = linkedAudioLogHash;
            _telemetry.FragmentHash = fragmentHash;
            _telemetry.FragmentIndex = fragmentIndex;
            _telemetry.Strength01 = result.Strength01;
            _telemetry.AverageDistanceSqMeters = result.AverageDistanceSqMeters;
            _telemetry.ErrorNoise01 = result.ErrorNoise01;
            _telemetry.Static01 = result.Static01;

            AudioLogSystem audioLogs = _audioLogs;
            _telemetry.RecoveredBits = audioLogs != null
                ? audioLogs.GetRecoveredEncryptedBits(linkedAudioLogHash)
                : 0u;

            if (!_fragmentRecovered && result.Strength01 >= fragmentRecoveryStrength01)
                TryRecoverFragment();

            SignalBeaconRegistry.PublishTelemetry(_registrySlot, in _telemetry);
            PublishDominantStaticToShader();
        }

        private void ClearPublishedTelemetry()
        {
            _telemetry.Strength01 = 0f;
            _telemetry.Static01 = 0f;
            _telemetry.ErrorNoise01 = 0f;
            _telemetry.AverageDistanceSqMeters = 0f;
            SignalBeaconRegistry.ClearTelemetry(_registrySlot);
            PublishDominantStaticToShader();
        }

        private void PublishDominantStaticToShader()
        {
            if (!publishStaticToShader)
                return;

            PublishDominantStaticToShaderValue();
        }

        private static void PublishDominantStaticToShaderValue()
        {
            float shaderStatic = SignalBeaconRegistry.TryGetDominantTelemetry(out _, out float dominantStatic01)
                ? dominantStatic01
                : 0f;
            if (math.abs(shaderStatic - _lastPublishedShaderStatic01) <= 0.0001f)
                return;

            Shader.SetGlobalFloat(_ShaderSignalStatic, shaderStatic);
            _lastPublishedShaderStatic01 = shaderStatic;
        }

        private void UnregisterBeaconAndRefreshShaderStatic()
        {
            SignalBeaconRegistry.Unregister(this, _registrySlot);
            _registrySlot = -1;
            if (publishStaticToShader || _lastPublishedShaderStatic01 >= 0f)
                PublishDominantStaticToShaderValue();
        }

        private void EmitBreadcrumb()
        {
            if (_telemetry.Strength01 < minimumBipStrength01)
                return;

            float safeBipRadiusMeters = math.max(0f, bipRadiusMeters);
            float safeBipPeriodSeconds = math.max(0.02f, bipPeriodSeconds);
            AcousticPingEvent pingEvent = new AcousticPingEvent(
                ResolveBeaconRuntimePosition(),
                safeBipRadiusMeters,
                math.saturate(bipIntensity01 * math.max(0.1f, _telemetry.Strength01)),
                safeBipPeriodSeconds,
                acousticRole,
                acousticSourceId,
                10f);

            PhysicsEventBus.NotifyAcousticPing(in pingEvent);
        }

        private bool TryRecoverFragment()
        {
            if (linkedAudioLogHash == 0u || fragmentHash == 0u)
                return false;

            AudioLogSystem audioLogs = _audioLogs;
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
            if (!TryResolveAupFromRuntimeOrigin(aupPoint0, out _pointAup0) ||
                !TryResolveAupFromRuntimeOrigin(aupPoint1, out _pointAup1) ||
                !TryResolveAupFromRuntimeOrigin(aupPoint2, out _pointAup2))
            {
                _aupCacheValid = false;
                _beaconAupCacheValid = false;
                _cachedBeaconRuntimeFrame = -1;
                return;
            }

            _aupCacheValid = true;
            _beaconAupCacheValid = false;
            _cachedBeaconRuntimeFrame = -1;
        }

        private Vector3 ResolveBeaconRuntimePosition()
        {
            RefreshBeaconAupCache(force: false);
            return _cachedBeaconRuntimePosition;
        }

        private void RefreshBeaconAupCache(bool force)
        {
            int currentFrame = Time.frameCount;
            if (!force && _beaconAupCacheValid)
            {
                if (_cachedBeaconRuntimeFrame != currentFrame)
                {
                    _cachedBeaconRuntimePosition = _beaconAup.ToRuntimeFloat3();
                    _cachedBeaconRuntimeFrame = currentFrame;
                }

                return;
            }

            if (!_aupCacheValid)
                RefreshAupCache(force: true);
            if (!_aupCacheValid)
            {
                _beaconAupCacheValid = false;
                _cachedBeaconRuntimePosition = Vector3.zero;
                _cachedBeaconRuntimeFrame = currentFrame;
                return;
            }

            _beaconAup = AbsoluteUniversePosition.WeightedAverage3(
                in _pointAup0,
                in _pointAup1,
                in _pointAup2,
                TriangulationCentroidWeight);
            _cachedBeaconRuntimePosition = _beaconAup.ToRuntimeFloat3();
            _cachedBeaconRuntimeFrame = currentFrame;
            _beaconAupCacheValid = true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement == null)
            {
                ResolvePlayer();
                if (_playerMovement == null)
                {
                    playerAup = default;
                    return false;
                }
            }

            playerAup = _playerMovement.CurrentAup;
            return true;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private float ResolveCaveErrorMultiplier()
        {
            SpatialAudioManager spatialAudio = _spatialAudio;
            if (spatialAudio != null &&
                spatialAudio.IsListenerInsideCaveVolume)
            {
                float caveInterior01 = math.saturate(spatialAudio.ListenerCaveInterior01);
                return math.lerp(1f, math.max(1f, caveErrorNoiseMultiplier), caveInterior01);
            }

            return 1f;
        }

        private void ResolvePlayer()
        {
            _playerMovement = null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    _audioLogs = currentService as AudioLogSystem;
                    return;
                case GlobalRegistryServiceSlot.Audio:
                    _spatialAudio = currentService as SpatialAudioManager;
                    return;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    ResolvePlayer();
                    return;
            }
        }

        private void CacheRegistryServicesCold()
        {
            _audioLogs = GlobalRegistry.AudioLogs;
            _spatialAudio = GlobalRegistry.Audio as SpatialAudioManager;
            _playerRuntimeContext = GlobalRegistry.Player;
            ResolvePlayer();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterTick()
        {
            if (_registered || !Application.isPlaying)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
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
        // COLD ALLOC: SignalBeaconTelemetry[32] - cached beacon telemetry for O(1) PDA reads - owner: SignalBeaconRegistry
        private static readonly SignalBeaconTelemetry[] _telemetrySlots = new SignalBeaconTelemetry[Capacity];
        private static SignalBeaconTelemetry _dominantTelemetry;
        private static uint _occupiedMask;
        private static uint _telemetryMask;
        private static int _count;
        private static int _dominantSlot = -1;
        private static int _dominantStaticSlot = -1;
        private static float _dominantStatic01;
        private static bool _hasDominant;

        public static int Count => _count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < _beacons.Length; i++)
            {
                _beacons[i] = null;
                _telemetrySlots[i] = default;
            }

            _occupiedMask = 0u;
            _telemetryMask = 0u;
            _count = 0;
            _dominantSlot = -1;
            _dominantStaticSlot = -1;
            _dominantStatic01 = 0f;
            _dominantTelemetry = default;
            _hasDominant = false;
        }

        public static int Register(SignalBeacon beacon)
        {
            if (beacon == null)
                return -1;

            for (int i = 0; i < _beacons.Length; i++)
            {
                if (ReferenceEquals(_beacons[i], beacon))
                    return i;
            }

            uint freeMask = ~_occupiedMask;
            if (freeMask == 0u)
                return -1;

            int slot = (int)math.tzcnt(freeMask);
            _beacons[slot] = beacon;
            _telemetrySlots[slot] = default;
            _occupiedMask |= 1u << slot;
            _telemetryMask &= ~(1u << slot);
            _count++;
            return slot;
        }

        public static void Unregister(SignalBeacon beacon, int slot)
        {
            if (beacon == null)
                return;

            if (slot >= 0 && slot < Capacity && ReferenceEquals(_beacons[slot], beacon))
            {
                ClearSlot(slot);
                return;
            }

            for (int i = 0; i < _beacons.Length; i++)
            {
                if (!ReferenceEquals(_beacons[i], beacon))
                    continue;

                ClearSlot(i);
                return;
            }
        }

        public static void PublishTelemetry(int slot, in SignalBeaconTelemetry telemetry)
        {
            if (slot < 0 || slot >= Capacity || (_occupiedMask & (1u << slot)) == 0u)
                return;

            uint slotBit = 1u << slot;
            bool hadTelemetry = (_telemetryMask & slotBit) != 0u;
            SignalBeaconTelemetry previousTelemetry = hadTelemetry ? _telemetrySlots[slot] : default;
            _telemetrySlots[slot] = telemetry;
            _telemetryMask |= slotBit;
            bool rebuildDominants = false;
            if (!_hasDominant || telemetry.Strength01 >= _dominantTelemetry.Strength01)
            {
                _dominantTelemetry = telemetry;
                _dominantSlot = slot;
                _hasDominant = true;
            }
            else if (slot == _dominantSlot && telemetry.Strength01 < previousTelemetry.Strength01)
            {
                rebuildDominants = true;
            }

            if (_dominantStaticSlot < 0 || telemetry.Static01 >= _dominantStatic01)
            {
                _dominantStatic01 = telemetry.Static01;
                _dominantStaticSlot = slot;
            }
            else if (slot == _dominantStaticSlot && telemetry.Static01 < previousTelemetry.Static01)
            {
                rebuildDominants = true;
            }

            if (rebuildDominants)
                RebuildDominantFromTelemetrySlots();
        }

        public static bool TryGetDominant(out SignalBeaconTelemetry telemetry)
        {
            telemetry = _dominantTelemetry;
            return _hasDominant;
        }

        public static bool TryGetDominantTelemetry(out float strength01, out float static01)
        {
            if (!_hasDominant)
            {
                strength01 = 0f;
                static01 = 0f;
                return false;
            }

            strength01 = _dominantTelemetry.Strength01;
            static01 = _dominantStatic01;
            return true;
        }

        public static void ClearTelemetry(int slot)
        {
            if (slot < 0 || slot >= Capacity)
                return;

            uint bit = 1u << slot;
            if ((_telemetryMask & bit) == 0u)
                return;

            _telemetrySlots[slot] = default;
            _telemetryMask &= ~bit;

            if (slot == _dominantSlot || slot == _dominantStaticSlot)
                RebuildDominantFromTelemetrySlots();
        }

        private static void ClearSlot(int slot)
        {
            _beacons[slot] = null;
            _telemetrySlots[slot] = default;
            _occupiedMask &= ~(1u << slot);
            _telemetryMask &= ~(1u << slot);
            if (_count > 0)
                _count--;

            if (slot == _dominantSlot || slot == _dominantStaticSlot)
                RebuildDominantFromTelemetrySlots();
        }

        private static void RebuildDominantFromTelemetrySlots()
        {
            uint mask = _telemetryMask;
            _dominantTelemetry = default;
            _dominantSlot = -1;
            _dominantStaticSlot = -1;
            _dominantStatic01 = 0f;
            _hasDominant = false;

            while (mask != 0u)
            {
                int slot = (int)math.tzcnt(mask);
                mask &= mask - 1u;
                SignalBeaconTelemetry candidate = _telemetrySlots[slot];
                if (!_hasDominant || candidate.Strength01 > _dominantTelemetry.Strength01)
                {
                    _dominantTelemetry = candidate;
                    _dominantSlot = slot;
                    _hasDominant = true;
                }

                if (_dominantStaticSlot < 0 || candidate.Static01 >= _dominantStatic01)
                {
                    _dominantStatic01 = candidate.Static01;
                    _dominantStaticSlot = slot;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct SignalBeaconSolveResult
    {
        [FieldOffset(0)] public float Strength01;
        [FieldOffset(4)] public float AverageDistanceSqMeters;
        [FieldOffset(8)] public float ErrorNoise01;
        [FieldOffset(12)] public float Static01;
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal static class SignalBeaconMath
    {
        private const double OneThird = 1d / 3d;

        public delegate float EvaluateSineWaveMatchDelegate(
            float targetFrequencyHz,
            float targetPhase01,
            float inputFrequencyHz,
            float inputPhase01,
            float frequencyToleranceHz,
            float phaseTolerance01);

        public delegate uint MergeRecoveredBitsDelegate(uint recoveredBits, uint fragmentBitMask);

        private static readonly FunctionPointer<EvaluateSineWaveMatchDelegate> _evaluateSineWaveMatch =
            BurstCompiler.CompileFunctionPointer<EvaluateSineWaveMatchDelegate>(EvaluateSineWaveMatchBurst);

        private static readonly FunctionPointer<MergeRecoveredBitsDelegate> _mergeRecoveredBits =
            BurstCompiler.CompileFunctionPointer<MergeRecoveredBitsDelegate>(MergeRecoveredBitsBurst);

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
            SolveTriangulatedStrengthKernel(
                in playerAup,
                in point0,
                in point1,
                in point2,
                maxRangeMeters,
                baseErrorNoise01,
                errorNoiseMultiplier,
                out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SolveTriangulatedStrengthKernel(
            in AbsoluteUniversePosition playerAup,
            in AbsoluteUniversePosition point0,
            in AbsoluteUniversePosition point1,
            in AbsoluteUniversePosition point2,
            float maxRangeMeters,
            float baseErrorNoise01,
            float errorNoiseMultiplier,
            out SignalBeaconSolveResult result)
        {
            double safeRange = math.isfinite(maxRangeMeters)
                ? math.max(0.001f, maxRangeMeters)
                : 0.001d;
            double safeRangeSq = safeRange * safeRange;
            double inverseSafeRangeSq = 1d / safeRangeSq;
            double distanceSq0 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point0);
            double distanceSq1 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point1);
            double distanceSq2 = AbsoluteUniversePosition.DistanceSq(in playerAup, in point2);
            double averageDistanceSq = (distanceSq0 + distanceSq1 + distanceSq2) * OneThird;
            if (!math.isfinite(averageDistanceSq) || averageDistanceSq < 0d)
                averageDistanceSq = safeRangeSq;

            float strength = averageDistanceSq >= safeRangeSq
                ? 0f
                : math.saturate((float)(1d - (averageDistanceSq * inverseSafeRangeSq)));
            float safeBaseErrorNoise = math.isfinite(baseErrorNoise01) ? baseErrorNoise01 : 1f;
            float safeErrorNoiseMultiplier = math.isfinite(errorNoiseMultiplier) ? math.max(1f, errorNoiseMultiplier) : 1f;
            float errorNoise = math.saturate(safeBaseErrorNoise * safeErrorNoiseMultiplier);

            result = new SignalBeaconSolveResult
            {
                Strength01 = strength,
                AverageDistanceSqMeters = (float)math.min(averageDistanceSq, (double)float.MaxValue),
                ErrorNoise01 = errorNoise,
                Static01 = math.saturate((1f - strength) + errorNoise)
            };
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluateSineWaveMatch(
            float targetFrequencyHz,
            float targetPhase01,
            float inputFrequencyHz,
            float inputPhase01,
            float frequencyToleranceHz,
            float phaseTolerance01)
        {
            return _evaluateSineWaveMatch.Invoke(
                targetFrequencyHz,
                targetPhase01,
                inputFrequencyHz,
                inputPhase01,
                frequencyToleranceHz,
                phaseTolerance01);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static float EvaluateSineWaveMatchBurst(
            float targetFrequencyHz,
            float targetPhase01,
            float inputFrequencyHz,
            float inputPhase01,
            float frequencyToleranceHz,
            float phaseTolerance01)
        {
            return EvaluateSineWaveMatchKernel(
                targetFrequencyHz,
                targetPhase01,
                inputFrequencyHz,
                inputPhase01,
                frequencyToleranceHz,
                phaseTolerance01);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateSineWaveMatchKernel(
            float targetFrequencyHz,
            float targetPhase01,
            float inputFrequencyHz,
            float inputPhase01,
            float frequencyToleranceHz,
            float phaseTolerance01)
        {
            if (!math.isfinite(targetFrequencyHz) ||
                !math.isfinite(targetPhase01) ||
                !math.isfinite(inputFrequencyHz) ||
                !math.isfinite(inputPhase01))
            {
                return 0f;
            }

            float safeTargetFrequencyHz = math.max(0.001f, targetFrequencyHz);
            float safeInputFrequencyHz = math.max(0.001f, inputFrequencyHz);
            float safeTargetPhase01 = math.frac(targetPhase01);
            float safeInputPhase01 = math.frac(inputPhase01);
            float safeFrequencyTolerance = math.isfinite(frequencyToleranceHz)
                ? math.max(0.001f, frequencyToleranceHz)
                : 0.001f;
            float safePhaseTolerance = math.isfinite(phaseTolerance01)
                ? math.max(0.001f, phaseTolerance01)
                : 0.001f;
            float inverseFrequencyTolerance = math.rcp(safeFrequencyTolerance);
            float inversePhaseTolerance = math.rcp(safePhaseTolerance);
            float frequencyError01 = math.saturate(math.abs(safeInputFrequencyHz - safeTargetFrequencyHz) * inverseFrequencyTolerance);
            float phaseDelta = math.abs(math.frac(safeInputPhase01 - safeTargetPhase01 + 0.5f) - 0.5f);
            float phaseError01 = math.saturate(phaseDelta * inversePhaseTolerance);
            float waveSampleError = math.abs(EvaluateSineProxy(safeInputPhase01) - EvaluateSineProxy(safeTargetPhase01)) * 0.5f;
            return math.saturate(1f - ((frequencyError01 * 0.55f) + (phaseError01 * 0.35f) + (waveSampleError * 0.10f)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float EvaluateSineProxy(float phase01)
        {
            float triangle = 1f - math.abs((math.frac(phase01 + 0.25f) * 4f) - 2f);
            return triangle * (1.5f - (0.5f * math.abs(triangle)));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint MergeRecoveredBits(uint recoveredBits, uint fragmentBitMask)
        {
            return _mergeRecoveredBits.Invoke(recoveredBits, fragmentBitMask);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static uint MergeRecoveredBitsBurst(uint recoveredBits, uint fragmentBitMask)
        {
            return MergeRecoveredBitsKernel(recoveredBits, fragmentBitMask);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MergeRecoveredBitsKernel(uint recoveredBits, uint fragmentBitMask)
        {
            return (recoveredBits | fragmentBitMask) & 0xFu;
        }
    }
}
