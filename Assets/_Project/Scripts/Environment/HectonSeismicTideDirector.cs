using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Environment
{
    /// <summary>
    /// Deterministic macro-world tide and seismic director. Physical outcomes are emitted as presentation signals.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Environment/Seismic Tide Director")]
    public sealed class HectonSeismicTideDirector : MonoBehaviour, ISeismicDirector, IUpdatable, ISlowTickable, IServiceHeartbeat, IServiceShutdown
    {
        private const int TelemetryCapacity = 300;
        private const float TidePeriod11Hours = 11f * 3600f;
        private const float TidePeriod17Hours = 17f * 3600f;
        private const float TidePeriod23Hours = 23f * 3600f;
        private const double TidePeriod11HoursRcp = 1d / TidePeriod11Hours;
        private const double TidePeriod17HoursRcp = 1d / TidePeriod17Hours;
        private const double TidePeriod23HoursRcp = 1d / TidePeriod23Hours;
        private const double HourSecondsRcp = 1d / 3600d;
        private const float TwoPi = 6.28318530718f;
        private const float VectorNormalizeEpsilonSq = 0.000001f;
        private const float Hash24ToUnit = 1f / 16777216f;
        private const float HighTremorThreshold = 0.8f;
        private const float AbyssDepthY = -500f;
        private const double ShaderShakeLodHysteresisSeconds = 2.5d;
        private const uint DefaultWorldSeed = 0x8E1571D5u;
        private const uint RockfallSpeciesHash = 0x5246434Cu;
        private const uint SubLowRumbleHash = 0x5355424Cu;
        private const string TelemetryDumpPath = "Docs/AgentLogs/Dump_WORLD_SEISMIC_GENERATOR.bin";

        private static readonly int _HectonWorldShakeId = Shader.PropertyToID("_HectonWorldShake");

        [Header("Tide")]
        [SerializeField, Min(0f), Tooltip("Peak deterministic tide displacement in meters before harmonic weighting.")]
        private float tideAmplitudeMeters = 3.5f;

        [Header("Seismic Presentation")]
        [SerializeField, Range(0f, 1f), Tooltip("Low-amplitude deterministic tremor floor used between hour-bucket quake events.")]
        private float microTremorIntensity = 0.08f;

        [SerializeField, Range(0f, 1f), Tooltip("Per-hour deterministic chance that the current world-seed bucket produces a visible quake.")]
        private float tremorEventProbability = 0.28f;

        [SerializeField, Min(0f), Tooltip("Maximum CoreLit world-space vertex offset in meters for non-low tiers.")]
        private float shaderShakeMaxMeters = 0.08f;

        [SerializeField, Min(0f), Tooltip("Camera micro-jitter scalar published through SeismicSignal.")]
        private float cameraJitterScale = 0.24f;

        [SerializeField, Min(0f), Tooltip("Audio rumble scalar published through ImpactSignal.")]
        private float audioRumbleScale = 0.9f;

        private NativeArray<SeismicTideTelemetryEntry> _telemetry;
        private ITickDispatcher _tickDispatcher;
        private IWorldSeedProvider _worldSeedProvider;
        private IPlayerRuntimeContext _playerRuntime;
        private CelestialRuntimeSnapshot _celestialSnapshot;
        private HectonQualityTier _scalabilityTier = HectonQualityTier.Unknown;
        private MathPrecisionLevel _mathPrecision = MathPrecisionLevel.Low;
        private double _fallbackAbsoluteUniverseTime;
        private uint _cachedWorldSeed = DefaultWorldSeed;
        private bool _telemetryRegistered;
        private bool _isInitialized;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredService;
        private bool _dumpedInvalidTelemetry;
        private bool _lowMemoryProfile = true;
        private bool _shaderShakeDisabled = true;
        private bool _hasShaderShakeState;
        private bool _hasPendingShaderShakeState;
        private bool _pendingShaderShakeDisabled;
        private int _telemetryWriteIndex;
        private int _tickCount;
        private int _lastCollapseHourBucket = int.MinValue;
        private double _shaderShakeLodSwitchTime;
        private uint _sequence;
        private SeismicRuntimeSnapshot _snapshot;
        private TideSolveResult _cachedTide;
        private Vector4 _lastWorldShake;
        private bool _hasCachedTide;

        /// <inheritdoc />
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public float SeismicIntensity01 => _snapshot.SeismicIntensity01;

        /// <inheritdoc />
        public float3 SeismicDirection => _snapshot.SeismicDirection;

        /// <inheritdoc />
        public float TideHeightMeters => _snapshot.TideHeightMeters;

        /// <inheritdoc />
        public float TideHigh01 => _snapshot.TideHigh01;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

        /// <inheritdoc />
        public int TickCount => _tickCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticShaderState()
        {
            Shader.SetGlobalVector(_HectonWorldShakeId, Vector4.zero);
        }

        /// <summary>
        /// Ensures the bootstrap-owned runtime component exists without scene-wide object discovery.
        /// </summary>
        public static HectonSeismicTideDirector EnsureRuntimeInstance()
        {
            HectonSeismicTideDirector registered = GlobalRegistry.SeismicDirector as HectonSeismicTideDirector;
            if (registered != null)
                return registered;

            GameObject runtimeRoot = new GameObject("[HectonSeismicTideDirector]"); // COLD ALLOC: GameObject[1] - bootstrap-owned seismic tide runtime root - owner: HectonSeismicTideDirector
            return runtimeRoot.AddComponent<HectonSeismicTideDirector>();
        }

        /// <summary>
        /// Explicit bootstrap entry point.
        /// </summary>
        public void InitializeService()
        {
            ISeismicDirector registered = GlobalRegistry.SeismicDirector;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                enabled = false;
                return;
            }

            EnsureTelemetryRing();
            RefreshCachedRuntimeState();
            if (!_registeredService)
            {
                GlobalRegistry.RegisterSeismicDirector(this);
                _registeredService = ReferenceEquals(GlobalRegistry.SeismicDirector, this);
            }

            _isInitialized = _registeredService;
            TryRegisterTickLanes();
            EvaluateAndPublish(refreshTide: true, publishSignals: false, publishCelestial: true);
        }

        /// <inheritdoc />
        public SeismicRuntimeSnapshot GetRuntimeSnapshot()
        {
            return _snapshot;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!_isInitialized)
                return;

            _ = deltaTime;
            _tickCount++;
            EvaluateAndPublish(refreshTide: false, publishSignals: false, publishCelestial: false);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!_isInitialized)
                return;

            RefreshCachedRuntimeState();
            EvaluateAndPublish(refreshTide: true, publishSignals: true, publishCelestial: true);
            WriteTelemetryEntry();
        }

        private void OnEnable()
        {
            if (_isInitialized)
            {
                RefreshCachedRuntimeState();
                TryRegisterTickLanes();
            }
        }

        private void OnDisable()
        {
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            ClearCachedRuntimeState();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            TryUnregisterTickLanes();
            if (_registeredService)
            {
                GlobalRegistry.UnregisterSeismicDirector(this);
                _registeredService = false;
            }

            _isInitialized = false;
            PushWorldShake(Vector4.zero);
            DisposeTelemetryRing();
            ClearCachedRuntimeState();
        }

        private void TryRegisterTickLanes()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredUpdatable)
                _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredSlowTickable)
                _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterTickLanes()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTickable = false;
            }
        }

        private void EvaluateAndPublish(bool refreshTide, bool publishSignals, bool publishCelestial)
        {
            double h8Time = ResolveH8TimeSeconds();
            int hourBucket = ResolveHourBucket(h8Time);
            uint seed = LCG_Hash(ResolveWorldSeed() + unchecked((uint)hourBucket));
            TideSolveResult tide = ResolveTideSolve(h8Time, seed, refreshTide);
            SeismicSolveResult seismic = EvaluateSeismicStateBurst(h8Time, seed, microTremorIntensity, tremorEventProbability);

            bool hasPlayerAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);
            bool abyssDepth = false;
            if (hasPlayerAup)
            {
                double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
                abyssDepth = math.isfinite(playerAbsolute.y) && playerAbsolute.y < AbyssDepthY;
            }

            bool lowTier = IsLowTierShaderShakeDisabled();
            float cameraJitter = math.saturate(seismic.Intensity01 * cameraJitterScale * (abyssDepth ? 0.5f : 1f));
            float audioRumble = math.saturate(seismic.Intensity01 * audioRumbleScale * (abyssDepth ? 1.5f : 1f));
            float thermalScalar = seismic.Intensity01 > HighTremorThreshold ? 2f : 1f;
            uint flags = (uint)(SeismicRuntimeFlags.Valid);
            if (lowTier)
                flags |= (uint)SeismicRuntimeFlags.LowTierShaderShakeDisabled;
            if (abyssDepth)
                flags |= (uint)SeismicRuntimeFlags.AbyssDepthAttenuation;
            if (seismic.Intensity01 > HighTremorThreshold)
                flags |= (uint)SeismicRuntimeFlags.HighTremor;

            _sequence++;
            _snapshot = new SeismicRuntimeSnapshot
            {
                AbsoluteUniverseTime = h8Time,
                SeismicDirection = seismic.Direction,
                SeismicIntensity01 = seismic.Intensity01,
                TideHeightMeters = tide.HeightMeters,
                TideHigh01 = tide.High01,
                CameraJitter01 = cameraJitter,
                AudioRumble01 = audioRumble,
                ThermalEruptionProbabilityScalar = thermalScalar,
                Flags = flags,
                Sequence = _sequence
            };

            if (!IsSnapshotFinite(in _snapshot))
            {
                DumpTelemetryRingOnce();
                _snapshot = default;
                _hasCachedTide = false;
                PushWorldShake(Vector4.zero);
                return;
            }

            if (publishCelestial)
                PublishCelestialTideSnapshot(h8Time, in tide);

            PublishShaderWorldShake(in seismic, lowTier);

            if (!publishSignals)
                return;

            PublishSeismicSignal(cameraJitter, audioRumble, thermalScalar, abyssDepth, lowTier);
            PublishRumbleSignal(audioRumble, hasPlayerAup, in playerAup);

            if (seismic.Intensity01 > HighTremorThreshold && _lastCollapseHourBucket != hourBucket)
            {
                _lastCollapseHourBucket = hourBucket;
                _snapshot.Flags |= (uint)SeismicRuntimeFlags.CollapseDebrisQueued;
                PublishRockfallDebris(seed, hasPlayerAup, in playerAup);
            }
        }

        private TideSolveResult ResolveTideSolve(double h8Time, uint seed, bool refreshTide)
        {
            if (refreshTide || !_hasCachedTide)
            {
                _cachedTide = EvaluateTideHarmonicsBurst(h8Time, seed, math.max(0f, tideAmplitudeMeters));
                _hasCachedTide = true;
            }

            return _cachedTide;
        }

        private void PublishCelestialTideSnapshot(double h8Time, in TideSolveResult tide)
        {
            CelestialRuntimeSnapshot celestial = _celestialSnapshot;
            celestial.AbsoluteUniverseTime = h8Time;
            celestial.TideHeightMeters = tide.HeightMeters;
            celestial.TideHigh01 = tide.High01;
            celestial.TidePullVector = tide.PullDirection;
            celestial.Flags |= (uint)CelestialRuntimeFlags.Valid;
            if (tide.High01 >= 0.66f)
                celestial.Flags |= (uint)CelestialRuntimeFlags.HighTide;
            else
                celestial.Flags &= ~(uint)CelestialRuntimeFlags.HighTide;

            celestial.Sequence = unchecked(celestial.Sequence + 1u);
            _celestialSnapshot = celestial;
            GlobalRegistry.PublishCelestialRuntimeSnapshot(in celestial);
        }

        private void PublishShaderWorldShake(in SeismicSolveResult seismic, bool lowTier)
        {
            if (lowTier || seismic.Intensity01 <= 0.0001f || shaderShakeMaxMeters <= 0f)
            {
                PushWorldShake(Vector4.zero);
                return;
            }

            float displacement = math.saturate(seismic.Intensity01) * math.max(0f, shaderShakeMaxMeters);
            float3 shake = seismic.Direction * displacement;
            PushWorldShake(new Vector4(shake.x, shake.y, shake.z, seismic.Intensity01));
        }

        private void PushWorldShake(Vector4 value)
        {
            if (ApproximatelyEqual(_lastWorldShake, value))
                return;

            Shader.SetGlobalVector(_HectonWorldShakeId, value);
            _lastWorldShake = value;
        }

        private void PublishSeismicSignal(float cameraJitter, float audioRumble, float thermalScalar, bool abyssDepth, bool lowTier)
        {
            byte depthFlags = abyssDepth ? (byte)1 : (byte)0;
            byte flags = lowTier ? (byte)2 : (byte)0;
            SeismicSignal signal = new SeismicSignal
            {
                Direction = _snapshot.SeismicDirection,
                Intensity01 = _snapshot.SeismicIntensity01,
                CameraJitter01 = cameraJitter,
                AudioIntensity01 = audioRumble,
                ThermalEruptionProbabilityScalar = thermalScalar,
                Sequence = unchecked((ushort)_sequence),
                DepthFlags = depthFlags,
                Flags = flags
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishRumbleSignal(float audioRumble, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            if (audioRumble <= 0.001f)
                return;

            AbsoluteUniversePosition pointAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));

            ImpactSignal signal = new ImpactSignal
            {
                PointAup = pointAup,
                Force = audioRumble * 8000f,
                Intensity = audioRumble,
                MaterialHash = SubLowRumbleHash,
                WeightClass = 3,
                PrimaryMaterialId = 0,
                SecondaryMaterialId = 0,
                Flags = 1
            };
            GlobalSignals.Publish(in signal);
        }

        private void PublishRockfallDebris(uint seed, bool hasPlayerAup, in AbsoluteUniversePosition playerAup)
        {
            AbsoluteUniversePosition originAup = hasPlayerAup
                ? playerAup
                : AbsoluteUniversePosition.FromAbsolutePosition(new double3(0d, -250d, 0d));
            double3 origin = originAup.ToAbsoluteDouble3();
            float intensity = _snapshot.SeismicIntensity01;

            for (int i = 0; i < 3; i++)
            {
                uint debrisSeed = LCG_Hash(seed ^ unchecked((uint)(0x9E3779B9u + i * 0x45D9F3Bu)));
                float angle = Hash01(debrisSeed) * TwoPi;
                math.sincos(angle, out float angleSin, out float angleCos);
                float radius = math.lerp(18f, 54f, Hash01(debrisSeed ^ 0xB5297A4Du));
                float vertical = math.lerp(-5f, 11f, Hash01(debrisSeed ^ 0x68E31DA4u));
                double3 offset = new double3(angleCos * radius, vertical, angleSin * radius);
                DebrisSpawnSignal debris = new DebrisSpawnSignal
                {
                    PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(origin + offset),
                    SpeciesHash = RockfallSpeciesHash,
                    SourceEntityId = debrisSeed,
                    Intensity01 = intensity,
                    DebrisKind = 2,
                    Flags = 1
                };
                GlobalSignals.Publish(in debris);
            }
        }

        private void EnsureTelemetryRing()
        {
            if (_telemetry.IsCreated)
                return;

            _telemetry = new NativeArray<SeismicTideTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<SeismicTideTelemetryEntry>[300] - black box seismic/tide ring - owner: HectonSeismicTideDirector
            NativeMemorySentinel.RegisterNativeArray(_telemetry, nameof(HectonSeismicTideDirector), nameof(_telemetry), NativeAllocationLifetime.Session);
            _telemetryRegistered = true;
        }

        private void DisposeTelemetryRing()
        {
            if (!_telemetry.IsCreated)
                return;

            if (_telemetryRegistered)
            {
                NativeMemorySentinel.UnregisterNativeArray(_telemetry);
                _telemetryRegistered = false;
            }

            _telemetry.Dispose();
            _telemetry = default;
            _telemetryWriteIndex = 0;
        }

        private void WriteTelemetryEntry()
        {
            if (!_telemetry.IsCreated)
                return;

            _telemetry[_telemetryWriteIndex] = new SeismicTideTelemetryEntry
            {
                TimeSeconds = _snapshot.AbsoluteUniverseTime,
                TideLevel = _snapshot.TideHeightMeters,
                LastTremorIntensity = _snapshot.SeismicIntensity01,
                Direction = _snapshot.SeismicDirection,
                Flags = _snapshot.Flags,
                Sequence = _snapshot.Sequence
            };
            _telemetryWriteIndex++;
            if (_telemetryWriteIndex >= TelemetryCapacity)
                _telemetryWriteIndex = 0;
        }

        private void DumpTelemetryRingOnce()
        {
            if (_dumpedInvalidTelemetry)
                return;

            _dumpedInvalidTelemetry = true;
            DumpTelemetryRing();
        }

        private void DumpTelemetryRing()
        {
            if (!_telemetry.IsCreated)
                return;

            try
            {
                Directory.CreateDirectory("Docs/AgentLogs");
                using (FileStream stream = new FileStream(TelemetryDumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(TelemetryCapacity);
                    writer.Write(_telemetryWriteIndex);
                    for (int i = 0; i < TelemetryCapacity; i++)
                    {
                        int index = (_telemetryWriteIndex + i) % TelemetryCapacity;
                        SeismicTideTelemetryEntry entry = _telemetry[index];
                        writer.Write(entry.TimeSeconds);
                        writer.Write(entry.TideLevel);
                        writer.Write(entry.LastTremorIntensity);
                        writer.Write(entry.Direction.x);
                        writer.Write(entry.Direction.y);
                        writer.Write(entry.Direction.z);
                        writer.Write(entry.Flags);
                        writer.Write(entry.Sequence);
                    }
                }
            }
            catch (Exception)
            {
#if UNITY_EDITOR
                Debug.LogError("[HectonSeismicTideDirector] telemetry dump failed.");
#endif
            }
        }

        private void RefreshCachedRuntimeState()
        {
            _tickDispatcher = GlobalRegistry.TickDispatcher;
            _worldSeedProvider = GlobalRegistry.WorldSeedProvider;
            _playerRuntime = GlobalRegistry.Player;
            _fallbackAbsoluteUniverseTime = GlobalRegistry.AbsoluteUniverseTime;
            _celestialSnapshot = GlobalRegistry.CelestialRuntimeSnapshot;

            _cachedWorldSeed = _worldSeedProvider != null && _worldSeedProvider.IsInitialized
                ? unchecked((uint)_worldSeedProvider.RuntimeWorldSeed)
                : DefaultWorldSeed;

            _scalabilityTier = GlobalRegistry.ScalabilityTier;
            _mathPrecision = GlobalRegistry.MathPrecision;
            _lowMemoryProfile = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            bool requestedShaderShakeDisabled = _lowMemoryProfile ||
                                                _mathPrecision == MathPrecisionLevel.Low ||
                                                _scalabilityTier == HectonQualityTier.Low ||
                                                _scalabilityTier == HectonQualityTier.Mx350 ||
                                                _scalabilityTier == HectonQualityTier.Unknown;
            UpdateShaderShakeLodState(requestedShaderShakeDisabled);
        }

        private void UpdateShaderShakeLodState(bool requestedDisabled)
        {
            double now = ResolveH8TimeSeconds();
            if (!_hasShaderShakeState)
            {
                _shaderShakeDisabled = requestedDisabled;
                _hasShaderShakeState = true;
                _hasPendingShaderShakeState = false;
                return;
            }

            if (requestedDisabled == _shaderShakeDisabled)
            {
                _hasPendingShaderShakeState = false;
                return;
            }

            if (!_hasPendingShaderShakeState || _pendingShaderShakeDisabled != requestedDisabled)
            {
                _pendingShaderShakeDisabled = requestedDisabled;
                _shaderShakeLodSwitchTime = now + ShaderShakeLodHysteresisSeconds;
                _hasPendingShaderShakeState = true;
                return;
            }

            if (now < _shaderShakeLodSwitchTime)
                return;

            _shaderShakeDisabled = requestedDisabled;
            _hasPendingShaderShakeState = false;
        }

        private void ClearCachedRuntimeState()
        {
            _tickDispatcher = null;
            _worldSeedProvider = null;
            _playerRuntime = null;
            _celestialSnapshot = default;
            _fallbackAbsoluteUniverseTime = 0d;
            _cachedWorldSeed = DefaultWorldSeed;
            _cachedTide = default;
            _hasCachedTide = false;
            _scalabilityTier = HectonQualityTier.Unknown;
            _mathPrecision = MathPrecisionLevel.Low;
            _lowMemoryProfile = true;
            _shaderShakeDisabled = true;
            _hasShaderShakeState = false;
            _hasPendingShaderShakeState = false;
            _pendingShaderShakeDisabled = false;
            _shaderShakeLodSwitchTime = 0d;
        }

        private double ResolveH8TimeSeconds()
        {
            ITickDispatcher dispatcher = _tickDispatcher;
            if (dispatcher != null && math.isfinite(dispatcher.DilatedTimeSeconds))
                return dispatcher.DilatedTimeSeconds;

            return math.isfinite(_fallbackAbsoluteUniverseTime) ? _fallbackAbsoluteUniverseTime : 0d;
        }

        private static int ResolveHourBucket(double h8Time)
        {
            if (!math.isfinite(h8Time))
                return 0;

            double hour = math.floor(h8Time * HourSecondsRcp);
            if (hour > int.MaxValue)
                return int.MaxValue;
            if (hour < int.MinValue)
                return int.MinValue;

            return (int)hour;
        }

        private uint ResolveWorldSeed()
        {
            return _cachedWorldSeed;
        }

        private bool IsLowTierShaderShakeDisabled()
        {
            return _shaderShakeDisabled;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition aup)
        {
            IPlayerRuntimeContext player = _playerRuntime;
            Transform transform = player != null ? player.PlayerTransform : null;
            if (transform == null)
            {
                aup = default;
                return false;
            }

            Vector3 position = transform.position;
            if (!math.all(math.isfinite((float3)position)))
            {
                aup = default;
                return false;
            }

            aup = AbsoluteUniversePosition.FromRuntimePosition(position);
            return true;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static TideSolveResult EvaluateTideHarmonicsBurst(double h8Time, uint seed, float amplitudeMeters)
        {
            float phase0 = Hash01(seed ^ 0xA511E9B3u) * TwoPi;
            float phase1 = Hash01(seed ^ 0x63D83595u) * TwoPi;
            float phase2 = Hash01(seed ^ 0x9D2C5680u) * TwoPi;
            HarmonicSinCos(h8Time, TidePeriod11HoursRcp, phase0, out float h0, out float c0);
            HarmonicSinCos(h8Time, TidePeriod17HoursRcp, phase1, out float h1, out float c1);
            HarmonicSinCos(h8Time, TidePeriod23HoursRcp, phase2, out float h2, out _);
            float combined = (h0 * 0.52f) + (h1 * 0.31f) + (h2 * 0.17f);
            float height = combined * math.max(0f, amplitudeMeters);
            float high01 = math.saturate((combined * 0.5f) + 0.5f);
            float3 pull = NormalizeSafe(new float3(
                c0,
                0.05f + high01 * 0.08f,
                h1 * 0.72f + c1 * 0.28f),
                new float3(1f, 0f, 0f));

            return new TideSolveResult
            {
                HeightMeters = height,
                High01 = high01,
                PullDirection = pull
            };
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private static SeismicSolveResult EvaluateSeismicStateBurst(double h8Time, uint seed, float microIntensity, float eventProbability)
        {
            float hourPhase = (float)(h8Time * HourSecondsRcp);
            float eventRoll = Hash01(seed ^ 0xBADC0DEu);
            float eventGate = eventRoll <= math.saturate(eventProbability) ? math.lerp(0.55f, 1f, Hash01(seed ^ 0xC001D00Du)) : 0f;
            float eventEnvelope = TriangleWave01(hourPhase + Hash01(seed ^ 0x51ED270Bu));
            float micro = TriangleWave01((float)(h8Time * 0.071d) + Hash01(seed ^ 0x72E4A13Bu)) * math.saturate(microIntensity);
            float intensity = math.saturate(eventEnvelope * eventGate + micro);
            float yaw = Hash01(seed ^ 0xA2F2D13Fu) * TwoPi;
            math.sincos(yaw, out float yawSin, out float yawCos);
            float tilt = (Hash01(seed ^ 0x9E3779B9u) - 0.5f) * 0.12f;
            float3 direction = NormalizeSafe(new float3(yawCos, tilt, yawSin), new float3(1f, 0f, 0f));

            return new SeismicSolveResult
            {
                Intensity01 = intensity,
                Direction = direction
            };
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void HarmonicSinCos(double h8Time, double inversePeriodSeconds, float phase, out float sine, out float cosine)
        {
            double cycle = h8Time * inversePeriodSeconds;
            double wrapped = cycle - math.floor(cycle);
            math.sincos((float)wrapped * TwoPi + phase, out sine, out cosine);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float TriangleWave01(float phase)
        {
            float wrapped = phase - math.floor(phase);
            return 1f - math.abs(wrapped * 2f - 1f);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint LCG_Hash(uint value)
        {
            value = unchecked(value * 1664525u + 1013904223u);
            value ^= value >> 16;
            value = unchecked(value * 2246822519u + 3266489917u);
            value ^= value >> 13;
            value = unchecked(value * 3266489917u + 668265263u);
            return value ^ (value >> 16);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Hash01(uint value)
        {
            return (LCG_Hash(value) & 0x00FFFFFFu) * Hash24ToUnit;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeSafe(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            return math.all(math.isfinite(value)) && math.isfinite(lengthSq) && lengthSq > VectorNormalizeEpsilonSq
                ? value * math.rsqrt(lengthSq)
                : fallback;
        }

        private static bool IsSnapshotFinite(in SeismicRuntimeSnapshot snapshot)
        {
            return math.isfinite(snapshot.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(snapshot.SeismicDirection)) &&
                   math.isfinite(snapshot.SeismicIntensity01) &&
                   math.isfinite(snapshot.TideHeightMeters) &&
                   math.isfinite(snapshot.TideHigh01) &&
                   math.isfinite(snapshot.CameraJitter01) &&
                   math.isfinite(snapshot.AudioRumble01) &&
                   math.isfinite(snapshot.ThermalEruptionProbabilityScalar);
        }

        private static bool ApproximatelyEqual(Vector4 a, Vector4 b)
        {
            return math.abs(a.x - b.x) <= 0.000001f &&
                   math.abs(a.y - b.y) <= 0.000001f &&
                   math.abs(a.z - b.z) <= 0.000001f &&
                   math.abs(a.w - b.w) <= 0.000001f;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TideSolveResult
        {
            public float HeightMeters;
            public float High01;
            public float3 PullDirection;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SeismicSolveResult
        {
            public float Intensity01;
            public float3 Direction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SeismicTideTelemetryEntry
        {
            public double TimeSeconds;
            public float TideLevel;
            public float LastTremorIntensity;
            public float3 Direction;
            public uint Flags;
            public uint Sequence;
        }
    }
}
