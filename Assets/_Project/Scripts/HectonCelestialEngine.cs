// ============================================================================
// HECTON-8 — HectonCelestialEngine.cs  v5.1
// Nebesnaya mehanika: gazovyy gigant, zatmeniya, planet-shine, okklyuziya, nebo.
//
// ═══════════════════════════════════════════════════════════════
// v5.1 CHANGES — RACE CONDITION FIX + SKY OCCLUSION:
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] DefaultExecutionOrder(-3000):
//     Runs AFTER AtmosphereManager(-6000) and UnderwaterVisuals(-4000).
//     By the time ApplySunOcclusion() executes, sunLight.intensity
//     already contains: ProfileSunIntensity × horizonFade × depthFactor
//     (written by UnderwaterVisuals).
//
//   [FIX] ApplySunOcclusion() — MULTIPLY, NOT OVERWRITE:
//     OLD (v4.x, RACE CONDITION):
//       sunLight.intensity = ProfileSunIntensity × horizonFade × visibility
//       ← LOST depth factor! Underwater sun was too bright.
//       ← Sunset flicker: UnderwaterVisuals wrote correct value,
//          then CelestialEngine overwrote without depth.
//
//     NEW (v5.1, CORRECT):
//       sunLight.intensity *= visibility
//       ← Preserves ALL previous factors (profile × horizon × depth).
//       ← Eclipse just dims whatever is already there.
//       ← Zero knowledge of depth system required.
//
//   [FIX] _EclipseOcclusion → sky shader:
//     Already existed in v4.0 but now documented as critical path.
//     Shader must multiply sun glow by (1 - _EclipseOcclusion).
//     Without this, skybox sun disc stays bright during eclipse.
//
//   [FIX] UpdateSkyboxBlend — eclipse triggers night sky:
//     _currentBlend = max(timeBlend, _smoothedOcclusionFactor)
//     During eclipse, sky darkens to night profile even if it's noon.
//     Stars appear. Horizon dims. Looks correct.
//
// ═══════════════════════════════════════════════════════════════
// EXECUTION CHAIN (deterministic via DefaultExecutionOrder):
//   1. AtmosphereManager(-6000).Tick() → ProfileSunIntensity, HorizonFade
//   2. UnderwaterVisuals(-4000).Tick() → sunLight.intensity = P × H × depth
//   3. CelestialEngine(-3000).Tick()   → sunLight.intensity *= visibility
//                                       → sky shader receives _EclipseOcclusion
//
// PRESERVED FROM v4.x:
//   ✓ Gas giant rendering (MaterialPropertyBlock)
//   ✓ Eclipse detection (angular occlusion + hysteresis)
//   ✓ Eclipse backlight (Fresnel)
//   ✓ Planet-shine
//   ✓ Lens flare occlusion
//   ✓ Skybox day/night blend
//   ✓ Sun visual disc positioning
//   ✓ _GameTime for sky shader (seamless cloud scrolling)
//   ✓ _NightBlend, _SunElevation for sky shader
//   ✓ Zero GC in hot path
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton.Localization;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Hecton8.Atmosphere;
using Hecton8.World;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Celestial
{
    /// <summary>
    /// Main-thread listener for deferred celestial events.
    /// </summary>
    public interface ICelestialEventListener
    {
        /// <summary>Called when an eclipse begins.</summary>
        void OnCelestialEclipseStarted();

        /// <summary>Called when an eclipse ends.</summary>
        void OnCelestialEclipseEnded();

        /// <summary>Called with the latest sun orbital angle in degrees.</summary>
        void OnCelestialSunAngleChanged(float angleDegrees);

        /// <summary>Called with the latest planet phase value.</summary>
        void OnCelestialPlanetPhaseChanged(float phase);
    }

    /// <summary>
    /// Queue-backed celestial event lane.
    /// </summary>
    public static class CelestialEvents
    {
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        private struct CelestialEventPayload
        {
            [FieldOffset(0)]
            public byte EventType;
            [FieldOffset(1)]
            private byte _pad0;
            [FieldOffset(2)]
            private ushort _pad1;
            [FieldOffset(4)]
            private uint _pad2;
            [FieldOffset(8)]
            private ulong _pad3;
        }

        private const byte EclipseStartedEventType = 1;
        private const byte EclipseEndedEventType = 2;
        private const byte SunAngleChangedEventType = 3;
        private const byte PlanetPhaseChangedEventType = 4;
        private const int ExpectedPendingEventCapacity = 8;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public ICelestialEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - celestial listeners drained by SystemDispatcher without interface array dispatch - owner: CelestialEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<CelestialEventPayload> _pendingEvents;
        private static NativeQueue<CelestialEventPayload> _nextFrameEvents;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static bool _sunAngleQueued;
        private static bool _planetPhaseQueued;
        private static float _latestSunAngleDegrees;
        private static float _latestPlanetPhase;

        /// <summary>
        /// Number of queued celestial event payloads awaiting dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CelestialEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(CelestialEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _sunAngleQueued = false;
            _planetPhaseQueued = false;
            _latestSunAngleDegrees = 0f;
            _latestPlanetPhase = 0f;
        }

        /// <summary>
        /// Registers a main-thread celestial listener.
        /// </summary>
        public static void Register(ICelestialEventListener listener)
        {
            if (listener != null)
                RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a main-thread celestial listener.
        /// </summary>
        public static void Unregister(ICelestialEventListener listener)
        {
            if (listener != null)
                TryUnregisterImmediate(listener);
        }

        /// <summary>Queues an eclipse-start signal.</summary>
        public static void RaiseEclipseStarted()
        {
            Enqueue(EclipseStartedEventType);
        }

        /// <summary>Queues an eclipse-end signal.</summary>
        public static void RaiseEclipseEnded()
        {
            Enqueue(EclipseEndedEventType);
        }

        /// <summary>Queues or coalesces a sun-angle signal.</summary>
        public static void RaiseSunAngleChanged(float angleDegrees)
        {
            if (_listenerCount <= 0)
                return;

            _latestSunAngleDegrees = angleDegrees;
            if (_sunAngleQueued)
                return;

            if (Enqueue(SunAngleChangedEventType))
                _sunAngleQueued = true;
        }

        /// <summary>Queues or coalesces a planet-phase signal.</summary>
        public static void RaisePlanetPhaseChanged(float phase)
        {
            if (_listenerCount <= 0)
                return;

            _latestPlanetPhase = phase;
            if (_planetPhaseQueued)
                return;

            if (Enqueue(PlanetPhaseChangedEventType))
                _planetPhaseQueued = true;
        }

        /// <summary>
        /// Flushes queued celestial events on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : ExpectedPendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out CelestialEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static bool Enqueue(byte eventType)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

            CelestialEventPayload payload = new CelestialEventPayload { EventType = eventType };
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void Dispatch(in CelestialEventPayload payload)
        {
            int listenerCount = _listenerCount;
            switch (payload.EventType)
            {
                case EclipseStartedEventType:
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        ICelestialEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnCelestialEclipseStarted();
                    }
                    break;
                case EclipseEndedEventType:
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        ICelestialEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnCelestialEclipseEnded();
                    }
                    break;
                case SunAngleChangedEventType:
                    _sunAngleQueued = false;
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        ICelestialEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnCelestialSunAngleChanged(_latestSunAngleDegrees);
                    }
                    break;
                case PlanetPhaseChangedEventType:
                    _planetPhaseQueued = false;
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        ICelestialEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnCelestialPlanetPhaseChanged(_latestPlanetPhase);
                    }
                    break;
            }
        }

        private static void RegisterImmediate(ICelestialEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(ICelestialEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                _listenerCount--;
                _listeners[i] = _listeners[_listenerCount];
                _listeners[_listenerCount].Clear();
                return true;
            }

            return false;
        }

        private static void EnsureInitialized()
        {
            if (_pendingEvents.IsCreated)
            {
                if (_nextFrameEvents.IsCreated)
                    return;
            }
            else
            {
                _pendingEvents = new NativeQueue<CelestialEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CelestialEventPayload>[8] - deferred celestial event lane flushed by SystemDispatcher - owner: CelestialEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    ExpectedPendingEventCapacity,
                    nameof(CelestialEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, ExpectedPendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<CelestialEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CelestialEventPayload>[8] - next-frame celestial event lane prevents same-frame reentrant dispatch - owner: CelestialEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    ExpectedPendingEventCapacity,
                    nameof(CelestialEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, ExpectedPendingEventCapacity);
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                !_pendingEvents.IsEmpty() ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<CelestialEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [Serializable]
    public struct SkyColorProfile
    {
        [ColorUsage(false, true)]
        [Tooltip("Sky color at zenith")]
        public Color zenithColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at horizon")]
        public Color horizonColor;

        [ColorUsage(false, true)]
        [Tooltip("Sky color at nadir")]
        public Color nadirColor;

        public static SkyColorProfile Default(
            Color zenith, Color horizon, Color nadir)
        {
            return new SkyColorProfile
            {
                zenithColor  = zenith,
                horizonColor = horizon,
                nadirColor   = nadir
            };
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-3000)]  // v5.1: MUST tick AFTER UnderwaterVisuals(-4000)
    public class HectonCelestialEngine : MonoBehaviour, ISlowTickable, ILateFrameTickable, IBiomeMatrixEventListener, IWeatherEventListener
    {
        private const string MandatedSkyMaterialName = "Mat_HectonSky";
        private const float SurfaceCloudShadowCookieEpsilon = 0.0001f;
        private const float SurfaceReadableSunIntensityFloor = 0.85f;
        private const float SurfaceReadableAmbientIntensityFloor = 1.1f;
        private const float SurfaceReadableFogDensityCeiling = 0.001f;
        private const float SurfaceEclipseVisibilityFloor = 0.7f;
        private const float SurfaceEclipseSkyNightBlendCeiling = 0.22f;
        private const float SurfaceEclipseShaderOcclusionCeiling = 0.24f;
        private static readonly Color SurfaceReadableSkyAmbientFloor = new Color(0.240f, 0.320f, 0.350f, 1f);
        private static readonly Color SurfaceReadableEquatorAmbientFloor = new Color(0.200f, 0.280f, 0.310f, 1f);
        private static readonly Color SurfaceReadableGroundAmbientFloor = new Color(0.120f, 0.160f, 0.180f, 1f);
        private static readonly Color SurfaceReadableSkyZenithFloor = new Color(0.160f, 0.270f, 0.320f, 1f);
        private static readonly Color SurfaceReadableSkyHorizonFloor = new Color(0.360f, 0.460f, 0.500f, 1f);
        private static readonly Color SurfaceReadableSkyNadirFloor = new Color(0.110f, 0.165f, 0.190f, 1f);
        private static AtmosphericLightingState _currentAtmosphericLightingState = AtmosphericLightingState.Default;
        private static bool _hasAtmosphericLightingState;

        /// <summary>
        /// Returns the latest surface-atmosphere snapshot authored by the celestial pipeline.
        /// Consumers use this instead of re-deriving surface fog, ambient, or light tint.
        /// </summary>
        public static bool TryGetCurrentAtmosphericLightingState(out AtmosphericLightingState state)
        {
            state = _currentAtmosphericLightingState;
            return _hasAtmosphericLightingState && state.IsValid != 0;
        }
        // Configuration

        [Serializable]
        private struct CinematicOrbitDefinition
        {
            public float semiMajorAxisMeters;
            [Range(0f, 0.95f)] public float eccentricity;
            [Min(1f)] public float orbitalPeriodSeconds;
            public float epochMeanAnomalyDegrees;
            public float epochUniverseTimeSeconds;
            public float inclinationDegrees;
            public float longitudeAscendingNodeDegrees;
            public float argumentOfPeriapsisDegrees;
            public float orbitalDriftDegreesPerYear;
            [Range(0f, 1f)] public float gravityWeight;
            [Min(1f)] public float registryOffsetMeters;

            public static CinematicOrbitDefinition GasGiantDefault()
            {
                return new CinematicOrbitDefinition
                {
                    semiMajorAxisMeters = 84000000f,
                    eccentricity = 0.035f,
                    orbitalPeriodSeconds = 172800f,
                    epochMeanAnomalyDegrees = 40f,
                    epochUniverseTimeSeconds = 0f,
                    inclinationDegrees = 6f,
                    longitudeAscendingNodeDegrees = 18f,
                    argumentOfPeriapsisDegrees = 90f,
                    orbitalDriftDegreesPerYear = 0.08f,
                    gravityWeight = 0.18f,
                    registryOffsetMeters = 96000f
                };
            }

            public static CinematicOrbitDefinition Moon0Default()
            {
                return new CinematicOrbitDefinition
                {
                    semiMajorAxisMeters = 410000f,
                    eccentricity = 0.072f,
                    orbitalPeriodSeconds = 28800f,
                    epochMeanAnomalyDegrees = 12f,
                    epochUniverseTimeSeconds = 0f,
                    inclinationDegrees = 4.8f,
                    longitudeAscendingNodeDegrees = 34f,
                    argumentOfPeriapsisDegrees = 12f,
                    orbitalDriftDegreesPerYear = 1.2f,
                    gravityWeight = 1f,
                    registryOffsetMeters = 56000f
                };
            }

            public static CinematicOrbitDefinition Moon1Default()
            {
                return new CinematicOrbitDefinition
                {
                    semiMajorAxisMeters = 690000f,
                    eccentricity = 0.118f,
                    orbitalPeriodSeconds = 43200f,
                    epochMeanAnomalyDegrees = 186f,
                    epochUniverseTimeSeconds = 0f,
                    inclinationDegrees = -7.2f,
                    longitudeAscendingNodeDegrees = -22f,
                    argumentOfPeriapsisDegrees = 51f,
                    orbitalDriftDegreesPerYear = -0.64f,
                    gravityWeight = 0.72f,
                    registryOffsetMeters = 76000f
                };
            }
        }

        private struct CinematicOrbitState
        {
            public float3 RegistryOffset;
            public float3 Direction;
            public float Phase01;
            public float Fullness01;
        }

        [StructLayout(LayoutKind.Explicit, Size = 192)]
        private struct CelestialOrbitJobOutput
        {
            [FieldOffset(0)] public CelestialRuntimeSnapshot Snapshot;
            [FieldOffset(144)] public byte Valid;
            [FieldOffset(145)] private byte _pad0;
            [FieldOffset(146)] private ushort _pad1;
            [FieldOffset(148)] private uint _pad2;
            [FieldOffset(152)] private ulong _pad3;
            [FieldOffset(160)] private ulong _pad4;
            [FieldOffset(168)] private ulong _pad5;
            [FieldOffset(176)] private ulong _pad6;
            [FieldOffset(184)] private ulong _pad7;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CelestialBlackBoxEntry
        {
            [FieldOffset(0)] public uint FrameIndex;
            [FieldOffset(4)] public uint Sequence;
            [FieldOffset(8)] public uint Flags;
            [FieldOffset(12)] public float TimeOfDay01;
            [FieldOffset(16)] public float EclipseState01;
            [FieldOffset(20)] public float SunDirectionY;
            [FieldOffset(24)] public float AegirDirectionY;
            [FieldOffset(28)] public float StormCloudDensity01;
            [FieldOffset(32)] public float LightningFlash01;
            [FieldOffset(36)] public float DepthMeters;
            [FieldOffset(40)] private ulong _pad0;
            [FieldOffset(48)] private ulong _pad1;
            [FieldOffset(56)] private ulong _pad2;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CelestialOrbitMathJob : IJob
        {
            public double AbsoluteUniverseTime;
            public uint DeterministicSeed;
            public float3 SunDirection;
            public CinematicOrbitDefinition GasGiantDefinition;
            public float GasGiantPeriodReciprocal;
            public CinematicOrbitDefinition Moon0Definition;
            public float Moon0PeriodReciprocal;
            public CinematicOrbitDefinition Moon1Definition;
            public float Moon1PeriodReciprocal;
            public float DayPeriodSeconds;
            public float InverseYearSeconds;
            public float TideAmplitudeMeters;
            public float HighTideThreshold;
            public float FullMoonBloomThreshold;
            public float EclipseOcclusion01;
            public byte EclipseActive;
            public float RadiationStorm01;
            public float ResonanceBiolumMultiplier;
            public uint Sequence;

            [WriteOnly] public NativeArray<CelestialOrbitJobOutput> Output;

            public void Execute()
            {
                CelestialRuntimeSnapshot snapshot = EvaluateAnalyticalOrbitSnapshot(
                    AbsoluteUniverseTime,
                    DeterministicSeed,
                    SunDirection,
                    in GasGiantDefinition,
                    GasGiantPeriodReciprocal,
                    in Moon0Definition,
                    Moon0PeriodReciprocal,
                    in Moon1Definition,
                    Moon1PeriodReciprocal,
                    DayPeriodSeconds,
                    InverseYearSeconds,
                    TideAmplitudeMeters,
                    HighTideThreshold,
                    FullMoonBloomThreshold,
                    EclipseOcclusion01,
                    EclipseActive != 0,
                    RadiationStorm01,
                    ResonanceBiolumMultiplier,
                    Sequence);

                Output[0] = new CelestialOrbitJobOutput
                {
                    Snapshot = snapshot,
                    Valid = 1
                };
            }
        }

        [SerializeField] private Light sunLight;
        [SerializeField] private Transform aegirTransform;
        [SerializeField] private ObserverRelativeCelestialBody aegirObserverRelativeBody;
        [SerializeField] private Renderer aegirRenderer;
        [SerializeField] private Transform playerTransform;
        [SerializeField] private HectonAtmosphereManager _atmosphereManager;

        [Header("Sky Material")]
        [SerializeField] private Material _skyMaterial;
        [SerializeField] private float _cloudSpeed = 0.01f;

        [Header("Surface Cloud Shadow Cookie")]
        [Tooltip("Authored Perlin cloud-shadow cookie. Runtime procedural texture generation is forbidden in the celestial tick path.")]
        [SerializeField] private Texture2D _surfaceCloudShadowCookie;
        [SerializeField, Min(8f)] private float _surfaceCloudShadowCookieSize = 420f;
        [SerializeField, Min(0f)] private float _surfaceCloudShadowCookieScrollSpeed = 8f;

        [Header("Aegir Ring Shadow Cookie")]
        [Tooltip("Authored directional-light cookie with parallel ring-shadow stripes. Enabled only while Aegir is above the observer horizon.")]
        [SerializeField] private Texture2D aegirRingShadowCookie;
        [SerializeField, Min(8f)] private float aegirRingShadowCookieSize = 1800f;
        [SerializeField, Range(-0.25f, 0.25f)] private float aegirRingShadowHorizonThreshold = 0.02f;

        [Header("Sky Color Profiles")]
        [HideInInspector]
        [SerializeField] private SkyColorProfile _dayProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.05f, 0.08f, 0.25f, 1f),
            horizonColor = new Color(0.4f, 0.35f, 0.5f, 1f),
            nadirColor   = new Color(0.02f, 0.03f, 0.08f, 1f)
        };

        [HideInInspector]
        [SerializeField] private SkyColorProfile _sunsetProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.24f, 0.16f, 0.38f, 1f),
            horizonColor = new Color(1.18f, 0.58f, 0.28f, 1f),
            nadirColor   = new Color(0.06f, 0.04f, 0.10f, 1f)
        };

        [HideInInspector]
        [SerializeField] private SkyColorProfile _nightProfile = new SkyColorProfile
        {
            zenithColor  = new Color(0.01f, 0.005f, 0.03f, 1f),
            horizonColor = new Color(0.08f, 0.05f, 0.12f, 1f),
            nadirColor   = new Color(0.005f, 0.003f, 0.01f, 1f)
        };

        [Header("Horizon Response")]
        [HideInInspector]
        [Tooltip("Compresses horizon luminance so the sky and gas giant dissolve into the same atmospheric band instead of reading as separate layers.")]
        [SerializeField, Range(0.25f, 1f)] private float _horizonBrightnessScale = 0.72f;
        [HideInInspector]
        [Tooltip("Pulls the horizon tint back toward the zenith hue to avoid a chalk-white band near the waterline.")]
        [SerializeField, Range(0f, 1f)] private float _horizonZenithBlend = 0.3f;

        [Header("Celestial Atmosphere Veil")]
        [Tooltip("Thickens the horizon veil. Raise this first when the bottom of Aegir or the moons still reads as a hard cutout at the sea line.")]
        [SerializeField, Range(0f, 4f)] private float horizonDensity = 1.35f;
        [Tooltip("Clears the zenith portion of the atmosphere so the gas giant belts and moon detail stay readable overhead.")]
        [SerializeField, Range(0f, 1f)] private float zenithTransparency = 0.78f;
        [Tooltip("Controls how long the horizon veil holds before relaxing toward the zenith. Higher values keep the sky-color dissolve lower for longer.")]
        [SerializeField, Range(0.35f, 4f)] private float atmosphereBlendPower = 1.4f;

        [Header("Surface Haze Tuning")]
        [Tooltip("Master multiplier for above-water distance haze. Raise this first when coastlines and sea stay too sharp at long range.")]
        [SerializeField, Range(0.5f, 3f)] private float _surfaceFogDensityMultiplier = 1.35f;
        [Tooltip("Extra strength for the sky-material horizon haze band. Raise this if the air still feels too clean after fog density is correct.")]
        [SerializeField, Range(0.25f, 2.5f)] private float _surfaceSkyHazeIntensityMultiplier = 1.2f;
        [Tooltip("How much sky hue is allowed to leak into above-water haze. Lower keeps the fog neutral; higher pushes stylized blue-violet air.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceHazeSkyTintInfluence = 0.12f;
        [Tooltip("0 = fog inherits the atmosphere color directly. 1 = fully use the manual fog color below. This is the explicit color override for surface fog.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogManualColorBlend = 0f;
        [Tooltip("Manual surface fog color override. Keep blend at 0 to stay fully linked to atmosphere, raise blend when art direction needs a deliberate fog tint shift.")]
        [SerializeField, ColorUsage(false, true)] private Color _surfaceFogManualColor = new Color(0.66f, 0.71f, 0.77f, 1f);
        [Tooltip("How strongly the sky horizon tint pushes the final fog color. Raise this when the waterline still reads as a hard cut instead of dissolving into the air.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogSkyColorInfluence = 0.32f;
        [Tooltip("How much ambient scene color lifts the final fog color. Raise this when the fog still feels disconnected from the world lighting.")]
        [SerializeField, Range(0f, 1f)] private float _surfaceFogAmbientColorInfluence = 0.22f;
        [Tooltip("Widens the haze band away from the exact horizon line. Raise this when haze controls feel like they only affect a razor-thin strip.")]
        [SerializeField, Range(0.5f, 2.5f)] private float _surfaceHazeHorizonSpread = 1.35f;
        [Tooltip("Strength of the low mist shelf that sits directly on the waterline. Raise this when the far ocean still reads as a cut plane instead of dissolving into air.")]
        [SerializeField, Range(0f, 2f)] private float _surfaceHorizonMistShelfIntensity = 1f;
        [Tooltip("Vertical reach of the horizon mist shelf above the waterline. Raise this when the dissolve stays too thin and only touches the exact seam.")]
        [SerializeField, Range(0.04f, 0.32f)] private float _surfaceHorizonMistShelfHeight = 0.16f;
        [Tooltip("Softness of the horizon mist shelf transition. Raise this when the shelf still reads as a stripe instead of a gradual atmospheric mass.")]
        [SerializeField, Range(0.02f, 0.24f)] private float _surfaceHorizonMistShelfSoftness = 0.1f;

        [Space(10)]
        [Header("Aegir Atmosphere Composite")]
        [Tooltip("Base transmittance multiplier pushed into Aegir. Lower values keep more cloud-band body color visible through haze; higher values let the sky occlude the disc earlier.")]
        [SerializeField, Range(0f, 1.5f)] private float _atmosphereTransmittanceWeight = 0.92f;
        [Tooltip("Base in-scattering multiplier pushed into Aegir. Raise this when you want more sky glow near the horizon; lower it when Aegir starts reading as a flat fog disk.")]
        [SerializeField, Range(0f, 2f)] private float _atmosphereInscatterWeight = 0.78f;
        [Tooltip("Reduces how much shared sky transmittance is pushed into moon materials. Lower values keep moon albedo readable against bright daytime haze.")]
        [SerializeField, Range(0f, 1.5f)] private float _moonAtmosphereTransmittanceMultiplier = 0.78f;
        [Tooltip("Reduces how much shared sky in-scatter is pushed into moon materials. Lower values stop small moons from dissolving into a foggy point.")]
        [SerializeField, Range(0f, 2f)] private float _moonAtmosphereInscatterMultiplier = 0.42f;

        [Space(8)]
        [Tooltip("Day profile modulation from Horizon (0.0) to Zenith (1.0). This multiplies the live skybox color instead of replacing it.")]
        [SerializeField] private Gradient dayAtmosphere = CreateDefaultDayAtmosphereGradient();
        [Tooltip("Sunset profile modulation from Horizon (0.0) to Zenith (1.0). Keep this near neutral if you want the LUT to stay tightly glued to the authored skybox colors.")]
        [SerializeField] private Gradient sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();
        [Tooltip("Night profile modulation from Horizon (0.0) to Zenith (1.0). Use this to control how much sky color survives on shadow sides at night.")]
        [SerializeField] private Gradient nightAtmosphere = CreateDefaultNightAtmosphereGradient();
        [Tooltip("Day profile transmittance curve from Horizon (0.0) to Zenith (1.0). Higher values near the first key make the horizon denser.")]
        [SerializeField] private AnimationCurve dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();
        [Tooltip("Sunset profile transmittance curve from Horizon (0.0) to Zenith (1.0). Raise the first key to thicken dusk at the horizon.")]
        [SerializeField] private AnimationCurve sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();
        [Tooltip("Night profile transmittance curve from Horizon (0.0) to Zenith (1.0). Use this to keep night silhouettes dissolved into the sky instead of becoming black cutouts.")]
        [SerializeField] private AnimationCurve nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();
        [Tooltip("Day profile density multiplier applied before the LUT alpha is converted to transmittance.")]
        [SerializeField, Range(0f, 2f)] private float dayAtmosphereDensityScale = 1f;
        [Tooltip("Sunset profile density multiplier. Raise this to make dusk burn thicker around the horizon.")]
        [SerializeField, Range(0f, 2f)] private float sunsetAtmosphereDensityScale = 0.52f;
        [Tooltip("Night profile density multiplier. Lower values keep giant and moon detail visible against space at night.")]
        [SerializeField, Range(0f, 2f)] private float nightAtmosphereDensityScale = 0.24f;
        [Tooltip("Day profile in-scattering exposure. Raise this to make daytime haze brighter without changing transmittance.")]
        [SerializeField, Range(0f, 4f)] private float dayAtmosphereExposure = 1.02f;
        [Tooltip("Sunset profile in-scattering exposure. Raise this for stronger HDR burn at dusk and dawn.")]
        [SerializeField, Range(0f, 4f)] private float sunsetAtmosphereExposure = 0.58f;
        [Tooltip("Night profile in-scattering exposure. Lower values let celestial bodies fade into darkness instead of glowing.")]
        [SerializeField, Range(0f, 4f)] private float nightAtmosphereExposure = 0.001f;
        [Tooltip("Half-width in sun elevation degrees for the sunset profile window around the horizon.")]
        [SerializeField, Range(1f, 35f)] private float sunsetAtmosphereBandDegrees = 14f;
        [Tooltip("Transition depth in degrees below twilight end before the night profile reaches full weight.")]
        [SerializeField, Range(1f, 35f)] private float nightAtmosphereTransitionDegrees = 12f;
        [Tooltip("Only rebakes the atmosphere LUT when sun elevation moves by at least this many degrees, preventing pointless runtime texture uploads.")]
        [SerializeField, Range(0.05f, 5f)] private float atmosphereLutRebuildSunAngleThreshold = 0.35f;
        [HideInInspector]
        [SerializeField] private int _visualDefaultsVersion;

        [Header("Sun Occlusion")]
        [SerializeField] private LensFlareComponentSRP _sunLensFlare;
        [SerializeField] private float sunDistance = 100000f;
        [SerializeField] private Transform sunVisualTransform;
        [SerializeField] private float flareFadeSpeed = 5.0f;

        [Header("Skybox")]
        [SerializeField] private Material daySkybox;
        [SerializeField] private Material nightSkybox;
        [SerializeField] private Material blendedSkyboxMaterial;

        [Header("Deep VRAM Gate")]
        [Tooltip("Below this depth, celestial textures are detached from runtime materials to reduce deep-water VRAM residency. Asset imports are not modified.")]
        [SerializeField, Min(0f)] private float deepTextureUnloadDepth = 1000f;
        [Tooltip("Keeps celestial texture residency reduced until the player climbs clearly out of the deep-water threshold instead of thrashing at one boundary.")]
        [SerializeField, Min(0f)] private float deepTextureDepthHysteresis = 120f;
        [Tooltip("Allows weak hardware to drop heavy celestial textures earlier when dynamic resolution has already collapsed and the player is no longer in shallow water.")]
        [SerializeField] private bool enableAdaptiveDeepTextureResidency = true;
        [Tooltip("Do not reduce celestial texture residency from perf pressure in shallow water. This keeps near-surface sky readability intact.")]
        [SerializeField, Min(0f)] private float adaptiveDeepTextureMinDepth = 350f;
        [Tooltip("Render-scale threshold that triggers early celestial texture detachment under perf pressure.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveDeepTextureUnloadRenderScale = 0.76f;
        [Tooltip("Render-scale threshold required before celestial textures are restored after a perf-pressure reduction.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveDeepTextureRestoreRenderScale = 0.9f;

        [Header("Orbital Parameters")]
        [SerializeField] private float orbitalPeriod = 3600f;
        [SerializeField] private Vector3 sunOrbitAxis = Vector3.right;
        [SerializeField] private float sunStartAngle;

        [Header("Cinematic Orbit Fakes")]
        [SerializeField] private bool enableAnalyticalOrbitSolver = true;
        [SerializeField] private bool driveObserverBodiesFromAnalyticalOrbits = true;
        [SerializeField] private CinematicOrbitDefinition gasGiantOrbit = CinematicOrbitDefinition.GasGiantDefault();
        [SerializeField] private CinematicOrbitDefinition moon0Orbit = CinematicOrbitDefinition.Moon0Default();
        [SerializeField] private CinematicOrbitDefinition moon1Orbit = CinematicOrbitDefinition.Moon1Default();
        [SerializeField, Range(0f, 8f)] private float celestialTideAmplitudeMeters = 2.25f;
        [SerializeField, Range(0f, 1f)] private float highTideThreshold01 = 0.78f;
        [SerializeField, Range(0f, 1f)] private float fullMoonBloomThreshold01 = 0.92f;
        [SerializeField, Range(1f, 3650f)] private float inGameYearDays = 365f;

        [Header("Eclipse Detection")]
        [SerializeField] private float eclipseAngularRadiusOverride;
        [SerializeField] private bool useCinematicEclipseOccluderRadius = true;
        [SerializeField, Range(0.05f, 5f)] private float cinematicEclipseOccluderRadiusDegrees = 1.15f;
        [SerializeField] private float eclipseHysteresisMargin = 0.5f;
        [SerializeField, Range(0.01f, 5f)] private float sunAngularRadiusDegrees = 0.27f;
        [SerializeField, Range(0.01f, 1f)] private float eclipseEventStartPenumbraThreshold = 0.5f;
        [SerializeField, Range(-0.1f, 0.1f)] private float eclipseAegirHorizonCullThreshold = -0.015f;

        [Header("Lunar Resonance")]
        [SerializeField, Range(0.5f, 15f)] private float lunarResonanceAlignmentDegrees = 5f;
        [SerializeField, Range(1f, 5f)] private float lunarResonanceBiolumMultiplier = 3f;

        [Header("Eclipse Backlight")]
        [SerializeField] private float backlitAlignmentSoftStart = 0.97f;
        [SerializeField] private float backlitAlignmentFullStart = 0.995f;
        [SerializeField] private float backlitFactorMultiplier = 1.0f;

        [Header("Planet Shine")]
        [SerializeField] private float planetShineMaxIntensity = 0.35f;
        [SerializeField] private Color planetShineColor = Color.HSVToRGB(0.75f, 0.2f, 0.9f);
        [SerializeField] private float planetShineNewMoonThreshold = 0.1f;

        [Header("Moon Phase Shadows")]
        [SerializeField] private bool enableMoonPhaseShadowModulation = true;
        [SerializeField, Range(0f, 0.5f)] private float moonPhaseShadowStrength = 0.18f;
        [SerializeField, Range(0.5f, 0.999f)] private float moonPhaseShadowStartDot = 0.82f;
        [SerializeField, Range(0.5f, 0.999f)] private float moonPhaseShadowFullDot = 0.985f;

        [Header("Shader Parameters")]
        [SerializeField] private float equatorialRotationSpeed = 0.02f;
        [SerializeField] private float polarRotationMultiplier = 0.4f;
        [SerializeField] private float backlitIntensity = 0.08f;
        [SerializeField] private float stormEmissionIntensity = 1.0f;
        [SerializeField] private float starMapSeed = 99173f;

        [Header("GPU Firmament Bake")]
        [SerializeField] private ComputeShader firmamentBakeCompute;
        [SerializeField] private bool enableGpuFirmamentBake = true;
        [SerializeField, Range(256, 8192)] private int firmamentCubemapResolution = 8192;
        [SerializeField, Range(0.1f, 6f)] private float firmamentStarIntensity = 1.35f;
        [SerializeField] private Texture2D starTwinkleNoiseLut;
        [SerializeField, Range(0.02f, 0.32f)] private float firmamentMilkyWayHalfWidthRadians = 0.11f;
        [SerializeField, Range(0f, 1f)] private float firmamentMilkyWayProbability = 0.76f;
        [SerializeField, Range(0.1f, 4f)] private float firmamentMilkyWayCoreBias = 1.8f;
        [SerializeField, Range(0f, 1f)] private float firmamentStarHaloGain = 0.35f;
        [SerializeField, Range(0.05f, 1f)] private float firmamentLatitudeCompression = 0.22f;
        [SerializeField, Range(64, 512)] private int atmosphereScatteringLutWidth = 256;
        [SerializeField, Range(16, 128)] private int atmosphereScatteringLutHeight = 64;
        [SerializeField, Range(0f, 4f)] private float atmosphereScatteringDensity = 1f;
        [SerializeField, Range(0f, 12f)] private float atmosphereScatteringExposure = 4.4f;

        [Header("Ocean Celestial Projection")]
        [SerializeField, Min(256f)] private float eclipseWaterShadowRadiusMeters = 4200f;
        [SerializeField, Range(0f, 1f)] private float eclipseWaterShadowDarkening = 0.58f;
        [SerializeField, Range(0.02f, 0.85f)] private float eclipseWaterShadowSoftness = 0.34f;
        [SerializeField, Range(0f, 80f)] private float eclipseWaterShadowScrollMetersPerSecond = 11f;
        [SerializeField, Range(0f, 1f)] private float aegirRingCausticStrength = 0.22f;
        [SerializeField, Range(0.0005f, 0.08f)] private float aegirRingCausticStripeScale = 0.018f;
        [SerializeField, Range(0.005f, 0.45f)] private float aegirRingCausticSoftness = 0.08f;
        [SerializeField, Range(0f, 0.25f)] private float aegirRingCausticScrollSpeed = 0.014f;

        [Header("Transition Curves")]
        [SerializeField] private float twilightStartAngle = 5f;
        [SerializeField] private float twilightEndAngle = -5f;

        // ─────────────────────────────────────────────
        // SOBYTIYa
        // ─────────────────────────────────────────────

        // ─────────────────────────────────────────────
        // RUNTIME STATE
        // ─────────────────────────────────────────────

        private Light _planetShineLight;
        private GameObject _planetShineLightGO;

        private MaterialPropertyBlock _aegirMPB;
        private MaterialPropertyBlock _moonMPB;
        private MaterialPropertyBlock _sunDiscMPB;

        private float _currentSunAngle;
        private float _currentBlend;
        private float _currentStarIntensity;
        private float _currentAtmosphereDensity;
        private float _resolvedStarMapSeed = 99173f;
        private float _lunarResonanceMultiplier = 1f;
        private float _currentPhase;
        private float _moonPhaseShadowVisibility = 1f;
        private bool _isEclipseActive;
        private bool _lunarResonanceActive;
        private float _eclipseAngularRadius;
        private float _accumulatedOrbitalAngle;
        private float _currentBacklitFactor;
        private float4x4 _sunOrbitRotationMatrix = float4x4.identity;
        private Vector3 _resolvedSunForward = Vector3.forward;

        private double _rotationAccumulator;
        private float _rotationTimer;
        private float _rotationPhase;
        private float _gameTime;
        private float _lastCelestialSlowTickTime;
        private float _celestialTimelineAccumulator;
        private int _nextCelestialTimelineWarningFrame;
        private float _debugCelestialTimeScale = 1f;

        private float _previousBlendForColors;
        private const float COLOR_BLEND_EPSILON = 0.001f;
        private Color _lastAppliedSkyZenith;
        private Color _lastAppliedSkyHorizon;
        private Color _lastAppliedSkyNadir;

        private float _sunOcclusionFactor;
        private float _smoothedOcclusionFactor;
        private float _baseSunIntensity;
        private bool _baseSunIntensityCaptured;
        private Color _baseSunColor = Color.white;
        private bool _baseSunColorCaptured;
        private float _baseFlareIntensity;
        private float _baseFlareScale;
        private bool _baseFlareValuesCaptured;
        private UniversalAdditionalLightData _sunAdditionalLightData;
        private Texture _cachedSunCookie;
        private Vector2 _cachedSunCookieSize = Vector2.one;
        private Vector2 _cachedSunCookieOffset;
        private bool _sunCookieDefaultsCaptured;
        private Vector2 _surfaceCloudShadowCookieOffset;
        private Vector2 _aegirRingShadowCookieOffset;
        private Texture2D _runtimeAegirRingShadowCookie;
        private bool _aegirRingShadowCookieBound;
        private bool _sunDirectionResolvedFromMatrix;

        private float3 _resolvedSunDirection;
        private CelestialRuntimeSnapshot _celestialRuntimeSnapshot;
        private uint _celestialRuntimeSequence;
        private float _penumbraFactor;
        private Color _resolvedSkyZenith;
        private Color _resolvedSkyHorizon;
        private Color _resolvedSkyNadir;
        private Texture2D _celestialAtmosphereLutTexture;
        private RenderTexture _bakedStarCubemap;
        private RenderTexture _atmosphereScatteringLutTexture;
        private bool _firmamentBakeComplete;
        private int _firmamentBakedSeed;
        private int _firmamentBakedResolution;
        private int _atmosphereScatteringBakedWidth;
        private int _atmosphereScatteringBakedHeight;
        private int _firmamentClearKernel = -1;
        private int _firmamentStarKernel = -1;
        private int _firmamentAtmosphereKernel = -1;
        private bool _firmamentResolutionWarningPublished;
        private readonly Vector4[] _skyOccluders = new Vector4[CelestialBodyCacheCapacity]; // COLD ALLOC: Vector4[8] - sky star occluder upload cache - owner: HectonCelestialEngine
        private float _currentAtmosphereExposure = 1f;
        private float _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
        private float _lastAtmosphereBakeDayWeight = -1f;
        private float _lastAtmosphereBakeSunsetWeight = -1f;
        private float _lastAtmosphereBakeNightWeight = -1f;
        private Color _lastAtmosphereBakeSkyZenith;
        private Color _lastAtmosphereBakeSkyHorizon;
        private Color _lastAtmosphereBakeSkyNadir;

        private bool _eclipseRadiusCalculated;

        private Renderer _cachedSunDiscRenderer;
        private bool _sunDiscRendererCached;

        private float _cachedAegirRadius;
        private Material _aegirSharedMaterial;
        private bool _deepTextureResidencyReduced;
        private float _currentDepthMeters;
        private float _currentAdaptiveRenderScale = 1f;

        private Texture _skyHighCloudTexDefault;
        private Texture _skyMainCloudAtlasDefault;
        private Texture _skyMainCloudTexDefault;
        private Texture _daySkyboxMainTexDefault;
        private Texture _daySkyboxEmissionTexDefault;
        private Texture _nightSkyboxMainTexDefault;
        private Texture _nightSkyboxEmissionTexDefault;
        private Texture _blendedDayCubemapDefault;
        private Texture _blendedNightCubemapDefault;
        private Texture _aegirMainTexDefault;
        private Texture _aegirDetailTexDefault;
        private Texture _aegirEmissionMapDefault;
        private Texture _aegirCelestialOcclusionTexDefault;

        private float _defaultCloudDensityThreshold;
        private float _defaultCloudSoftness;
        private float _defaultCloudSpeedMultiplier;
        private Vector4 _defaultWindDirection;
        private Color _defaultCloudLitColor;
        private Color _defaultCloudShadowColor;
        private Color _defaultSunsetCloudColor;
        private Color _defaultNightCloudColor;
        private Color _defaultSunDiscColor;
        private Color _defaultSunScatterColor;
        private bool _cachedSkyWeatherDefaults;

        private bool _surfaceWeatherOverrideActive;
        private float _surfaceWeatherCloudDensityThreshold;
        private float _surfaceWeatherCloudSoftness;
        private float _surfaceWeatherCloudSpeedMultiplier = 1f;
        private Vector4 _surfaceWeatherWindDirection = new Vector4(1f, 0f, 0f, 0f);
        private float _surfaceWeatherStarVisibilityMultiplier = 1f;
        private float _surfaceWeatherStormEmissionMultiplier = 1f;
        private float _surfaceWeatherSkyLuminanceMultiplier = 1f;
        private bool _registeredToTickManager;
        private bool _firmamentStartupBakeAttempted;
        private float _surfaceWeatherSunDiscMultiplier = 1f;
        private float _surfaceWeatherSunScatterMultiplier = 1f;
        private Color _surfaceWeatherCloudLitColor = Color.white;
        private Color _surfaceWeatherCloudShadowColor = Color.white;
        private Color _surfaceWeatherSunsetCloudColor = Color.white;
        private Color _surfaceWeatherNightCloudColor = Color.white;
        private bool _surfaceWeatherFogOverrideActive;
        private Color _surfaceWeatherFogColor = Color.white;
        private float _surfaceWeatherFogDensity;
        private Color _surfaceWeatherAmbientColor = Color.white;
        private float _surfaceWeatherSunMultiplier = 1f;
        private bool _renderSettingsGuardAcquired;
        private AtmosphericLightingState _surfaceAtmosphericLightingState = AtmosphericLightingState.Default;
        private const int CelestialBodyCacheCapacity = 8;
        private const float AtmosphereWeightBlendThreshold = 0.01f;
        private const int CelestialAtmosphereLutResolution = 64;
        private const int FirmamentStartupStarCount = 100000;
        private const int FirmamentStarBakeThreads = 64;
        private const float ComputeThreadGroup8Inv = 0.125f;
        private const float FirmamentStarBakeThreadsInv = 0.015625f;
        private const float HorizonDensityQuarter = 0.25f;
        private const float CelestialTimelineStepSeconds = 0.1f;
        private const float CelestialTimelineClockMaxSeconds = 16777215f;
        private const int CelestialSnapshotFrameIntervalHigh = 60;
        private const int CelestialSnapshotFrameIntervalLow = 300;
        private const float OrbitDegreesToTurns = 0.0027777778f;
        private const float SeedToUnit = 0.00000000023283064f;
        private const float Inv90 = 0.011111111f;
        private const float Inv88 = 0.011363636f;
        private const float Inv64 = 0.015625f;
        private const float Inv55 = 1.8181818f;
        private const double InvCelestialGlobalSunUploadPeriodSeconds = 0.016666666666666666d;
        private const int CelestialTimelineMaxStepsPerSlowTick = 5;
        private const int FirmamentMinResolution = 256;
        private const int FirmamentMx350ResolutionCap = 2048;
        private const int FirmamentMidVramResolutionCap = 4096;
        private const int FirmamentHighVramResolutionCap = 8192;
        private const int FirmamentMx350MemoryCapMb = 3072;
        private const int FirmamentMidVramMemoryCapMb = 8192;
        private const int AegirRingShadowGeneratedCookieResolution = 256;
        private const float AegirRingShadowGeneratedLineMinLuma = 0.094117647f;
        private const int BestVisualDefaultsVersion = 5;
        private const float NightAtmosphereInscatterFloor = 0.001f;
        private const int AtmosphereGradientSampleCount = 8;
        private const int CelestialBlackBoxFrameCount = 300;
        private const float AbyssalCelestialCullY = -200f;
        private const float LowTierLockedSunPitchDegrees = 45f;
        private const float LightningFlashDecayLerpPerLateFrame = 0.42f;
        private const float ShaderScalarEpsilon = 0.0001f;
        private const float LightningFlashEpsilon = ShaderScalarEpsilon;
        private const float EclipseBiolumMultiplier = 1.65f;
        private const uint CelestialBlackBoxFlagLowTierLocked = 1u << 24;
        private const uint CelestialBlackBoxFlagAbyssalCulled = 1u << 25;
        private const uint CelestialBlackBoxFlagStorm = 1u << 26;
        private const string CelestialBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_CELESTIAL_MECHANICS.bin";
        private static readonly double StopwatchTickToMilliseconds =
            1000.0d * math.rcp((double)System.Diagnostics.Stopwatch.Frequency);
#if UNITY_EDITOR
        private const string FirmamentBakeComputeAssetPath = "Assets/_Project/Art/Shaders/HectonFirmamentBake.compute";
#endif
        private bool _editorPreviewDirty;
        private readonly List<ObserverRelativeCelestialBody> _observerBodyCache = new List<ObserverRelativeCelestialBody>(CelestialBodyCacheCapacity); // COLD ALLOC: List<ObserverRelativeCelestialBody>[8] - cold observer-body cache for moon renderer discovery - owner: HectonCelestialEngine
        private readonly List<Renderer> _moonRenderers = new List<Renderer>(CelestialBodyCacheCapacity); // COLD ALLOC: List<Renderer>[8] - cached moon renderers for shared atmosphere overrides - owner: HectonCelestialEngine
        private readonly Color[] _celestialAtmosphereLutPixels = new Color[CelestialAtmosphereLutResolution]; // COLD ALLOC: Color[64] - celestial atmosphere LUT bake buffer - owner: HectonCelestialEngine
        private int _nextCelestialSnapshotFrame;
        private int _lastSunDirectionGlobalUploadMinute = int.MinValue;
        private float _sunOrbitPeriodReciprocal;
        private float _gasGiantOrbitPeriodReciprocal;
        private float _moon0OrbitPeriodReciprocal;
        private float _moon1OrbitPeriodReciprocal;
        private float _inGameYearSecondsReciprocal;
        private uint _lastPublishedCelestialSequence = uint.MaxValue;
        private uint _lastPublishedCelestialFlags = uint.MaxValue;
        private float _lastPublishedCelestialEclipseOcclusion = -1f;
        private float _lastPublishedCelestialRadiationStorm = -1f;
        private NativeArray<CelestialOrbitJobOutput> _orbitJobOutput;
        private JobHandle _orbitJobHandle;
        private bool _orbitJobScheduled;
        private bool _orbitJobPrimed;
        private bool _registeredLateFrameTick;
        private NativeArray<CelestialBlackBoxEntry> _celestialBlackBox;
        private int _celestialBlackBoxCursor;
        private int _celestialBlackBoxCount;
        private bool _celestialBlackBoxDumped;
        private NativeArray<float4> _dayAtmosphereGradientSamples;
        private NativeArray<float4> _sunsetAtmosphereGradientSamples;
        private NativeArray<float4> _nightAtmosphereGradientSamples;
        private bool _atmosphereGradientSamplesDirty = true;
        private bool _lowTierSunLockApplied;
        private bool _ambientProbeEclipseActive;
        private float _stormCloudDensity01;
        private float _lastUploadedStormCloudDensity01 = -1f;
        private float _lightningFlash01;
        private float _lastUploadedLightningFlash01 = -1f;

        // ─────────────────────────────────────────────
        // SHADER PROPERTY IDs
        // ─────────────────────────────────────────────

        private static readonly int _ID_SunDirection       = Shader.PropertyToID("_SunDirection");
        private static readonly int _ID_DirectionalLightColor = Shader.PropertyToID("_DirectionalLightColor");
        private static readonly int _ID_BacklitIntensity   = Shader.PropertyToID("_BacklitIntensity");
        private static readonly int _ID_EquatorialSpeed    = Shader.PropertyToID("_EquatorialSpeed");
        private static readonly int _ID_PolarMultiplier    = Shader.PropertyToID("_PolarMultiplier");
        private static readonly int _ID_PlanetPhase        = Shader.PropertyToID("_PlanetPhase");
        private static readonly int _ID_StormEmission      = Shader.PropertyToID("_StormEmission");
        private static readonly int _ID_Blend              = Shader.PropertyToID("_Blend");
        private static readonly int _ID_StarIntensity      = Shader.PropertyToID("_StarIntensity");
        private static readonly int _ID_StarSeed           = Shader.PropertyToID("_StarSeed");
        private static readonly int _ID_StarTwinkleLut     = Shader.PropertyToID("_StarTwinkleLUT");
        private static readonly int _ID_BakedStarCubemap   = Shader.PropertyToID("_BakedStarCubemap");
        private static readonly int _ID_BakedStarCubemapReady = Shader.PropertyToID("_BakedStarCubemapReady");
        private static readonly int _ID_HectonStarBakeParams = Shader.PropertyToID("_HectonStarBakeParams");
        private static readonly int _ID_HectonStarDistribution = Shader.PropertyToID("_HectonStarDistribution");
        private static readonly int _ID_HectonGalaxyArmShape = Shader.PropertyToID("_HectonGalaxyArmShape");
        private static readonly int _ID_HectonAtmosphereScatteringLut = Shader.PropertyToID("_HectonAtmosphereScatteringLUT");
        private static readonly int _ID_HectonAtmosphereScatteringLutReady = Shader.PropertyToID("_HectonAtmosphereScatteringLUTReady");
        private static readonly int _ID_HectonAtmosphereLutSize = Shader.PropertyToID("_HectonAtmosphereLutSize");
        private static readonly int _ID_HectonRayleighBeta = Shader.PropertyToID("_HectonRayleighBeta");
        private static readonly int _ID_HectonMieBeta = Shader.PropertyToID("_HectonMieBeta");
        private static readonly int _ID_HectonAtmosphereParams = Shader.PropertyToID("_HectonAtmosphereParams");
        private static readonly int _ID_HectonEclipseWaterShadowParams = Shader.PropertyToID("_HectonEclipseWaterShadowParams");
        private static readonly int _ID_HectonEclipseWaterShadowDirection = Shader.PropertyToID("_HectonEclipseWaterShadowDirection");
        private static readonly int _ID_HectonRingCausticsParams = Shader.PropertyToID("_HectonRingCausticsParams");
        private static readonly int _ID_HectonRingCausticsDirection = Shader.PropertyToID("_HectonRingCausticsDirection");
        private static readonly int _ID_HectonSkyRotation = Shader.PropertyToID("_HectonSkyRotation");
        private static readonly int _ID_HectonSkyOccluderCount = Shader.PropertyToID("_HectonSkyOccluderCount");
        private static readonly int _ID_HectonSkyOccluders = Shader.PropertyToID("_HectonSkyOccluders");
        private static readonly int _ID_HectonCelestialTidePull = Shader.PropertyToID("_HectonCelestialTidePull");
        private static readonly int _ID_HectonCelestialTideHeight = Shader.PropertyToID("_HectonCelestialTideHeight");
        private static readonly int _ID_HectonCelestialGasGiantOffset = Shader.PropertyToID("_HectonCelestialGasGiantOffset");
        private static readonly int _ID_HectonCelestialMoon0Offset = Shader.PropertyToID("_HectonCelestialMoon0Offset");
        private static readonly int _ID_HectonCelestialMoon1Offset = Shader.PropertyToID("_HectonCelestialMoon1Offset");
        private static readonly int _ID_HectonCelestialPhaseParams = Shader.PropertyToID("_HectonCelestialPhaseParams");
        private static readonly int _ID_HectonCelestialRuntimeFlags = Shader.PropertyToID("_HectonCelestialRuntimeFlags");
        private static readonly int _ID_HectonCelestialRadiationStorm = Shader.PropertyToID("_HectonCelestialRadiationStorm");
        private static readonly int _ID_HectonCelestialBiolumMultiplier = Shader.PropertyToID("_HectonCelestialBiolumMultiplier");
        private static readonly int _ID_HectonAtmosphereColor = Shader.PropertyToID("_HectonAtmosphereColor");
        private static readonly int _ID_HectonStormCloudDensity = Shader.PropertyToID("_HectonStormCloudDensity");
        private static readonly int _ID_HectonLightningFlash = Shader.PropertyToID("_HectonLightningFlash");
        private static readonly int _ID_HectonMoonPhaseTextureIndex = Shader.PropertyToID("_HectonMoonPhaseTextureIndex");
        private static readonly int _ID_HectonMoonPhase01 = Shader.PropertyToID("_HectonMoonPhase01");
        private static readonly uint _FirmamentResolutionClampWarningHash = unchecked((uint)LocHash.Compute("HectonCelestialEngine.FirmamentResolutionClamp"));
        private static readonly uint _FirmamentBakeContextHash = unchecked((uint)LocHash.Compute("HectonCelestialEngine.FirmamentBake"));
        private static readonly uint _CelestialTimelineBudgetWarningHash = unchecked((uint)LocHash.Compute("HectonCelestialEngine.CelestialTimelineBudget"));
        private static readonly uint _CelestialTimelineContextHash = unchecked((uint)LocHash.Compute("HectonCelestialEngine.SlowTick"));
        private const double CelestialTimelineBudgetMilliseconds = 0.2d;
        private const int CelestialTimelineWarningCooldownFrames = 30;
        private static readonly int _ID_FresnelSunDir      = Shader.PropertyToID("_FresnelSunDir");
        private static readonly int _ID_SunBacklitFactor   = Shader.PropertyToID("_SunBacklitFactor");
        private static readonly int _ID_GlobalRotation     = Shader.PropertyToID("_GlobalRotation");
        private static readonly int _ID_OcclusionFactor    = Shader.PropertyToID("_OcclusionFactor");
        private static readonly int _ID_EmissionColor      = Shader.PropertyToID("_EmissionColor");
        private static readonly int _ID_AegirDirection     = Shader.PropertyToID("_AegirDirection");
        private static readonly int _ID_SkyColorZenith     = Shader.PropertyToID("_SkyColorZenith");
        private static readonly int _ID_SkyColorHorizon    = Shader.PropertyToID("_SkyColorHorizon");
        private static readonly int _ID_SkyColorNadir      = Shader.PropertyToID("_SkyColorNadir");
        private static readonly int _ID_CelestialAtmosphereLut = Shader.PropertyToID("_CelestialAtmosphereLUT");
        private static readonly int _ID_CelestialAtmosphereLutReady = Shader.PropertyToID("_CelestialAtmosphereLUTReady");
        private static readonly int _ID_AtmosphereExposure = Shader.PropertyToID("_AtmosphereExposure");
        private static readonly int _ID_CelestialHorizonDensity = Shader.PropertyToID("_CelestialHorizonDensity");
        private static readonly int _ID_CelestialZenithTransparency = Shader.PropertyToID("_CelestialZenithTransparency");
        private static readonly int _ID_CelestialAtmosphereBlendPower = Shader.PropertyToID("_CelestialAtmosphereBlendPower");
        private static readonly int _ID_AtmosphereTransmittanceWeight = Shader.PropertyToID("_AtmosphereTransmittanceWeight");
        private static readonly int _ID_AtmosphereInscatterWeight = Shader.PropertyToID("_AtmosphereInscatterWeight");
        private static readonly int _ID_AtmosphereDensity = Shader.PropertyToID("_AtmosphereDensity");
        private static readonly int _ID_GameTime           = Shader.PropertyToID("_GameTime");
        private static readonly int _ID_WindDirection      = Shader.PropertyToID("_WindDirection");
        private static readonly int _ID_NightBlend         = Shader.PropertyToID("_NightBlend");
        private static readonly int _ID_SunElevation       = Shader.PropertyToID("_SunElevation");
        private static readonly int _ID_EclipseOcclusion   = Shader.PropertyToID("_EclipseOcclusion");
        private static readonly int _ID_PenumbraFactor     = Shader.PropertyToID("_PenumbraFactor");
        private static readonly int _ID_CloudDensityThreshold = Shader.PropertyToID("_CloudDensityThreshold");
        private static readonly int _ID_CloudSoftness = Shader.PropertyToID("_CloudSoftness");
        private static readonly int _ID_CloudSpeedMult = Shader.PropertyToID("_CloudSpeedMult");
        private static readonly int _ID_CloudColorLit = Shader.PropertyToID("_CloudColorLit");
        private static readonly int _ID_CloudColorShadow = Shader.PropertyToID("_CloudColorShadow");
        private static readonly int _ID_SunsetHorizonColor = Shader.PropertyToID("_SunsetHorizonColor");
        private static readonly int _ID_SunsetCloudColor = Shader.PropertyToID("_SunsetCloudColor");
        private static readonly int _ID_NightCloudColor = Shader.PropertyToID("_NightCloudColor");
        private static readonly int _ID_AegirGlowIntensity = Shader.PropertyToID("_AegirGlowIntensity");
        private static readonly int _ID_SunDiscColor = Shader.PropertyToID("_SunDiscColor");
        private static readonly int _ID_SunScatterColor = Shader.PropertyToID("_SunScatterColor");
        private static readonly int _ID_SkyLuminanceMultiplier = Shader.PropertyToID("_SkyLuminanceMultiplier");
        private static readonly int _ID_HazeIntensity = Shader.PropertyToID("_HazeIntensity");
        private static readonly int _ID_HazeFalloff = Shader.PropertyToID("_HazeFalloff");
        private static readonly int _ID_HazeColor = Shader.PropertyToID("_HazeColor");
        private static readonly int _ID_HazeSunTintStrength = Shader.PropertyToID("_HazeSunTintStrength");
        private static readonly int _ID_HorizonMistShelfIntensity = Shader.PropertyToID("_HorizonMistShelfIntensity");
        private static readonly int _ID_HorizonMistShelfHeight = Shader.PropertyToID("_HorizonMistShelfHeight");
        private static readonly int _ID_HorizonMistShelfSoftness = Shader.PropertyToID("_HorizonMistShelfSoftness");
        private static readonly int _ID_HighCloudTex       = Shader.PropertyToID("_HighCloudTex");
        private static readonly int _ID_MainCloudAtlas     = Shader.PropertyToID("_MainCloudAtlas");
        private static readonly int _ID_MainCloudTex       = Shader.PropertyToID("_MainCloudTex");
        private static readonly int _ID_MainTex            = Shader.PropertyToID("_MainTex");
        private static readonly int _ID_EmissionMap        = Shader.PropertyToID("_EmissionMap");
        private static readonly int _ID_DayCubemap         = Shader.PropertyToID("_DayCubemap");
        private static readonly int _ID_NightCubemap       = Shader.PropertyToID("_NightCubemap");
        private static readonly int _ID_DetailTex          = Shader.PropertyToID("_DetailTex");
        private static readonly int _ID_CelestialOcclusionTex = Shader.PropertyToID("_CelestialOcclusionTex");

        private static Gradient CreateDefaultDayAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.04f, 1.01f, 0.97f), 0f),
                    new GradientColorKey(new Color(0.98f, 1.0f, 1.02f), 0.38f),
                    new GradientColorKey(new Color(0.9f, 0.95f, 1.0f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.45f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultSunsetAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.12f, 0.98f, 0.92f), 0f),
                    new GradientColorKey(new Color(1.04f, 0.88f, 0.8f), 0.26f),
                    new GradientColorKey(new Color(0.8f, 0.82f, 0.9f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.35f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static Gradient CreateDefaultNightAtmosphereGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.32f, 0.36f, 0.44f), 0f),
                    new GradientColorKey(new Color(0.15f, 0.19f, 0.26f), 0.42f),
                    new GradientColorKey(new Color(0.05f, 0.08f, 0.12f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.4f),
                    new GradientAlphaKey(1f, 1f)
                });
            return gradient;
        }

        private static AnimationCurve CreateDefaultDayAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.56f, 0f, -1.1f),
                new Keyframe(0.18f, 0.34f, -0.72f, -0.38f),
                new Keyframe(0.56f, 0.09f, -0.18f, -0.08f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        private static AnimationCurve CreateDefaultSunsetAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.82f, 0f, -1.0f),
                new Keyframe(0.18f, 0.48f, -0.64f, -0.34f),
                new Keyframe(0.56f, 0.14f, -0.18f, -0.08f),
                new Keyframe(1f, 0.02f, 0f, 0f));
        }

        private static AnimationCurve CreateDefaultNightAtmosphereDensityCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0.12f, 0f, -0.3f),
                new Keyframe(0.24f, 0.05f, -0.12f, -0.06f),
                new Keyframe(0.66f, 0.012f, -0.02f, -0.01f),
                new Keyframe(1f, 0f, 0f, 0f));
        }

        // ─────────────────────────────────────────────
        // LIFECYCLE
        // ─────────────────────────────────────────────

        private void Awake()
        {
            CacheCelestialOrbitReciprocals();
            ForceMandatedSkyMaterialReference();
            EnsureCelestialAtmosphereLutReady(publishOnRebuild: false);
            ResolveFirmamentBakeCompute();
        }

        private void OnEnable()
        {
            if (!_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Acquire(this);
                _renderSettingsGuardAcquired = true;
            }

            if (Application.isPlaying)
            {
                GlobalTelemetryBus.Initialize();
                GlobalRegistry.RegisterCelestialEngineRuntime(this);
            }

            ForceMandatedSkyMaterialReference();
            ValidateReferences();
            EnsureAegirRingShadowCookieReady();
            EnsureCelestialAtmosphereLutReady();
            ResolveFirmamentBakeCompute();
            InitializeMaterialPropertyBlocks();
            InitializePlanetShineLight();
            CacheCelestialTextureDefaults();
            CacheMoonRenderers();

            _accumulatedOrbitalAngle = sunStartAngle;
            _currentBacklitFactor = 0f;
            _smoothedOcclusionFactor = 0f;
            _sunOcclusionFactor = 0f;
            _moonPhaseShadowVisibility = 1f;
            _baseSunIntensityCaptured = false;
            _baseSunColorCaptured = false;
            _baseFlareValuesCaptured = false;
            _eclipseRadiusCalculated = false;
            _sunDiscRendererCached = false;
            _sunDirectionResolvedFromMatrix = false;

            _rotationAccumulator = 0.0;
            _rotationTimer = 0f;
            _gameTime = 0f;
            _lastCelestialSlowTickTime = 0f;
            _celestialTimelineAccumulator = 0f;
            _firmamentStartupBakeAttempted = false;
            _nextCelestialSnapshotFrame = 0;
            _lastSunDirectionGlobalUploadMinute = int.MinValue;
            _orbitJobPrimed = false;
            _lowTierSunLockApplied = false;
            _ambientProbeEclipseActive = false;
            _stormCloudDensity01 = 0f;
            _lastUploadedStormCloudDensity01 = -1f;
            _lightningFlash01 = 0f;
            _lastUploadedLightningFlash01 = -1f;
            CacheCelestialOrbitReciprocals();
            MarkAtmosphereGradientSamplesDirty();

            _previousBlendForColors = -1f;
            _lastAppliedSkyZenith = default;
            _lastAppliedSkyHorizon = default;
            _lastAppliedSkyNadir = default;
            _editorPreviewDirty = true;

            if (sunLight != null)
            {
                _baseSunIntensity = sunLight.intensity;
                _baseSunIntensityCaptured = true;
                _baseSunColor = sunLight.color;
                _baseSunColorCaptured = true;
            }

            CaptureBaseFlareValues();
            SyncCrestPrimaryLight();

            ApplySkyboxMaterialOwnership(forceAssignment: true);
            ApplyFirmamentStaticMaterialBindings(_skyMaterial);

            if (Application.isPlaying)
            {
                EnsureCelestialRuntimeBuffers();
                EnsureAtmosphereGradientSamples();
                _stormCloudDensity01 = 0f;
                _lastUploadedStormCloudDensity01 = -1f;
                UploadStormCloudDensityShaderGlobal(0f, forceUpload: true);
                _lightningFlash01 = 0f;
                _lastUploadedLightningFlash01 = -1f;
                UploadLightningFlashShaderGlobal(0f, forceUpload: true);

                BiomeMatrixEvents.Unregister(this);
                BiomeMatrixEvents.Register(this);
                WeatherEvents.Unregister(this);
                WeatherEvents.Register(this);
                TryRegisterToTickManager();
                TryRegisterLateFrameTickable();
                InitializeFirmamentBakeAtStartup();

                BiomeMatrixDirector director = GlobalRegistry.BiomeMatrix;
                if (director != null)
                {
                    _currentDepthMeters = Mathf.Max(0f, director.CurrentDepthMeters);
                    UpdateDeepTextureResidencyState();
                }
                else
                {
                    _currentDepthMeters = 0f;
                    RestoreCelestialTextureDefaults();
                }
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorTick;
                EditorApplication.update += EditorTick;
            }
#endif
        }

        private void Start()
        {
            TryRegisterToTickManager();
            TryRegisterLateFrameTickable();
            InitializeFirmamentBakeAtStartup();
        }

        private void OnDisable()
        {
            if (GlobalRegistry.CelestialEngine == this)
                GlobalRegistry.UnregisterCelestialEngineRuntime(this);

            _editorPreviewDirty = false;
            _surfaceAtmosphericLightingState = AtmosphericLightingState.Default;
            _currentAtmosphericLightingState = AtmosphericLightingState.Default;
            _hasAtmosphericLightingState = false;

            if (Application.isPlaying)
            {
                BiomeMatrixEvents.Unregister(this);
                WeatherEvents.Unregister(this);
            }

            ReleaseCelestialAtmosphereLut();
            ReleaseFirmamentBakeResources();
            RestoreCelestialTextureDefaults();
            RestoreSurfaceCloudShadowCookie();
            ReleaseRuntimeAegirRingShadowCookie();
            RestoreSunDefaults();
            CleanupPlanetShineLight();
            TryUnregisterFromTickManager();
            TryUnregisterLateFrameTickable();
            DisposeCelestialRuntimeBuffers(forceCompleteOrbitJob: true);
            ClearCelestialRuntimeSnapshot();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorTick;
            }
#endif

            if (_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Release(this);
                _renderSettingsGuardAcquired = false;
            }
        }

        private void OnDestroy()
        {
            if (GlobalRegistry.CelestialEngine == this)
                GlobalRegistry.UnregisterCelestialEngineRuntime(this);

            if (Application.isPlaying)
            {
                BiomeMatrixEvents.Unregister(this);
                WeatherEvents.Unregister(this);
            }

            if (_renderSettingsGuardAcquired)
            {
                RenderSettingsLifecycleGuard.Release(this);
                _renderSettingsGuardAcquired = false;
            }

            ReleaseCelestialAtmosphereLut();
            ReleaseFirmamentBakeResources();
            RestoreSurfaceCloudShadowCookie();
            ReleaseRuntimeAegirRingShadowCookie();
            TryUnregisterFromTickManager();
            TryUnregisterLateFrameTickable();
            DisposeCelestialRuntimeBuffers(forceCompleteOrbitJob: true);
            ClearCelestialRuntimeSnapshot();
        }

        private void TryRegisterToTickManager()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterFromTickManager()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTickable()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        public void LateFrameTick()
        {
            TryCompleteOrbitMathJob(forceComplete: false);
            if (_lightningFlash01 > 0f)
                UpdateLightningFlashShaderGlobal(forceUpload: false);
        }

        public void OnWeatherEvent(in WeatherEventPayload payload)
        {
            if (payload.EventType == (ushort)WeatherEventType.Lightning)
            {
                float strikeIntensity01 = math.saturate(payload.WeatherIntensity);
                _lightningFlash01 = math.max(_lightningFlash01, strikeIntensity01);
                UploadLightningFlashShaderGlobal(_lightningFlash01, forceUpload: false);
                return;
            }

            if (payload.EventType == (ushort)WeatherEventType.SnapshotUpdated)
            {
                float stormDensity = (payload.StateMask & (uint)WeatherState.Storm) != 0u
                    ? math.saturate(payload.WeatherIntensity)
                    : 0f;
                if (_surfaceWeatherOverrideActive)
                    stormDensity = math.max(stormDensity, math.saturate(_surfaceWeatherCloudDensityThreshold));

                _stormCloudDensity01 = stormDensity;
                UploadStormCloudDensityShaderGlobal(stormDensity, forceUpload: false);
            }
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.isPlaying || this == null)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            bool sunMoved = TryConsumeEditorSunTransformChange();
            if (!_editorPreviewDirty && !sunMoved)
                return;

            _editorPreviewDirty = false;
            SceneView.RepaintAll();
        }

        private void OnValidate()
        {
            CacheCelestialOrbitReciprocals();
            MarkAtmosphereGradientSamplesDirty();
            CacheMoonRenderers();
            sunAngularRadiusDegrees = Mathf.Max(0.01f, sunAngularRadiusDegrees);
            celestialTideAmplitudeMeters = Mathf.Clamp(celestialTideAmplitudeMeters, 0f, 8f);
            highTideThreshold01 = Mathf.Clamp01(highTideThreshold01);
            fullMoonBloomThreshold01 = Mathf.Clamp01(fullMoonBloomThreshold01);
            inGameYearDays = Mathf.Max(1f, inGameYearDays);
            CacheCelestialOrbitReciprocals();
            cinematicEclipseOccluderRadiusDegrees = Mathf.Max(0.01f, cinematicEclipseOccluderRadiusDegrees);
            eclipseEventStartPenumbraThreshold = Mathf.Clamp(eclipseEventStartPenumbraThreshold, 0.01f, 1f);
            aegirRingShadowCookieSize = Mathf.Max(8f, aegirRingShadowCookieSize);
            aegirRingShadowHorizonThreshold = Mathf.Clamp(aegirRingShadowHorizonThreshold, -0.25f, 0.25f);
            _editorPreviewDirty = true;

            if (Application.isPlaying)
            {
                EnsureCelestialAtmosphereLutReady(publishOnRebuild: true);
                return;
            }

            InitializeMaterialPropertyBlocks();
            DisposeAtmosphereGradientSamples();

#if UNITY_EDITOR
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
#endif
        }

        private bool TryConsumeEditorSunTransformChange()
        {
            if (sunLight == null)
                return false;

            if (_atmosphereManager != null)
            {
                bool sunSynced = _atmosphereManager.SyncEditorPreviewFromSunTransform();
                bool previewDirty = _atmosphereManager.ConsumeEditorPreviewDirty();
                return sunSynced || previewDirty;
            }

            Transform sunTransform = sunLight.transform;
            if (sunTransform == null || !sunTransform.hasChanged)
                return false;

            SyncEditorOrbitFromSunTransform();
            sunTransform.hasChanged = false;
            return true;
        }

        private void SyncEditorOrbitFromSunTransform()
        {
            float3 orbitAxis = ResolveDominantAxisDirection((float3)sunOrbitAxis, new float3(1f, 0f, 0f));
            Vector3 worldOrbitAxis = new Vector3(orbitAxis.x, orbitAxis.y, orbitAxis.z);
            Vector3 referenceForward = Vector3.ProjectOnPlane(Vector3.forward, worldOrbitAxis);
            Vector3 currentForward = Vector3.ProjectOnPlane(sunLight.transform.forward, worldOrbitAxis);

            if (referenceForward.sqrMagnitude <= 0.0001f ||
                currentForward.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _accumulatedOrbitalAngle = Vector3.SignedAngle(
                referenceForward,
                currentForward,
                worldOrbitAxis);

            if (_accumulatedOrbitalAngle < 0f)
                _accumulatedOrbitalAngle += 360f;
        }
#endif

        // ─────────────────────────────────────────────
        // ITickable — MAIN LOOP
        // ─────────────────────────────────────────────

        public void SlowTick()
        {
            if (ShouldCullCelestialForAbyss(out float abyssDepthMeters))
            {
                WriteCelestialBlackBoxTelemetry(
                    ResolveTimeOfDay01(),
                    _smoothedOcclusionFactor,
                    CelestialBlackBoxFlagAbyssalCulled,
                    abyssDepthMeters);
                _lightningFlash01 = 0f;
                UploadLightningFlashShaderGlobal(0f, forceUpload: false);
                return;
            }

            long timelineStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float now = ResolveCelestialTimelineClockSeconds();
            float elapsed = _lastCelestialSlowTickTime > 0f
                ? math.clamp(now - _lastCelestialSlowTickTime, CelestialTimelineStepSeconds, CelestialTimelineStepSeconds * CelestialTimelineMaxStepsPerSlowTick)
                : CelestialTimelineStepSeconds;
            _lastCelestialSlowTickTime = now;
            _celestialTimelineAccumulator = math.min(
                _celestialTimelineAccumulator + elapsed,
                CelestialTimelineStepSeconds * CelestialTimelineMaxStepsPerSlowTick);

            int steps = 0;
            while (_celestialTimelineAccumulator >= CelestialTimelineStepSeconds &&
                   steps < CelestialTimelineMaxStepsPerSlowTick)
            {
                RunCelestialTimeline(CelestialTimelineStepSeconds);
                _celestialTimelineAccumulator -= CelestialTimelineStepSeconds;
                steps++;
            }

            PublishCelestialTimelineBudgetWarningIfNeeded(timelineStartTicks);
        }

        private static float ResolveCelestialTimelineClockSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(CelestialTimelineClockMaxSeconds, timeSeconds);
        }

        private void PublishCelestialTimelineBudgetWarningIfNeeded(long timelineStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - timelineStartTicks;
            double elapsedMilliseconds = elapsedTicks * StopwatchTickToMilliseconds;
            if (elapsedMilliseconds <= CelestialTimelineBudgetMilliseconds ||
                Time.frameCount < _nextCelestialTimelineWarningFrame)
            {
                return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                _CelestialTimelineBudgetWarningHash,
                _CelestialTimelineContextHash,
                (float)elapsedMilliseconds);
            _nextCelestialTimelineWarningFrame = Time.frameCount + CelestialTimelineWarningCooldownFrames;
        }

        private bool ShouldCullCelestialForAbyss(out float depthMeters)
        {
            depthMeters = 0f;
            if (!Application.isPlaying)
                return false;

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
            {
                PlayerMovementRuntimeState movementState = runtimeContext.MovementState;
                if (math.isfinite(movementState.DepthMeters))
                {
                    depthMeters = math.max(0f, movementState.DepthMeters);
                    if (depthMeters >= math.abs(AbyssalCelestialCullY))
                        return true;
                }

                float3 predictedRuntime = movementState.PredictedAup.ToRuntimeFloat3();
                if (math.all(math.isfinite(predictedRuntime)) && predictedRuntime.y < AbyssalCelestialCullY)
                {
                    depthMeters = math.max(depthMeters, -predictedRuntime.y);
                    return true;
                }

                HectonPlayerMovement playerMovement = runtimeContext.PlayerMovement;
                if (playerMovement != null)
                {
                    float3 currentRuntime = playerMovement.CurrentAup.ToRuntimeFloat3();
                    if (math.all(math.isfinite(currentRuntime)) && currentRuntime.y < AbyssalCelestialCullY)
                    {
                        depthMeters = math.max(depthMeters, -currentRuntime.y);
                        return true;
                    }
                }
            }

            BiomeMatrixDirector biomeMatrix = GlobalRegistry.BiomeMatrix;
            if (biomeMatrix == null)
                return false;

            depthMeters = math.max(0f, biomeMatrix.CurrentDepthMeters);
            return depthMeters >= math.abs(AbyssalCelestialCullY);
        }

        private void RunCelestialTimeline(float deltaTime)
        {
            float celestialDeltaTime = deltaTime * math.max(0f, _debugCelestialTimeScale);
            _rotationAccumulator += (double)celestialDeltaTime;
            if (_rotationAccumulator > 10000.0)
                _rotationAccumulator -= 10000.0;
            _rotationTimer = (float)_rotationAccumulator;
            _rotationPhase = (float)(_rotationAccumulator % 1.0);

            _gameTime += celestialDeltaTime * math.max(0f, _cloudSpeed * ResolveCloudSpeedMultiplier());

            if (!_eclipseRadiusCalculated && aegirTransform != null && playerTransform != null)
            {
                CalculateEclipseAngularRadius();
                _eclipseRadiusCalculated = true;
            }

            _cachedAegirRadius = ComputeAegirWorldRadius();

            UpdateSunPosition(celestialDeltaTime);
            ResolveSunDirection();
            SyncCrestPrimaryLight();
            ApplySurfaceCloudShadowCookie(celestialDeltaTime);
            UpdateSunVisualPosition();
            UpdateAnalyticalCelestialState();

            float sunElevation = CalculateSunElevation();
            _currentSunAngle = sunElevation;

            CalculateEclipseBacklight();
            DetectEclipse();
            DetectLunarResonance();
            UpdateSunOcclusion(celestialDeltaTime);

            UpdateSkyboxBlend(sunElevation);
            UpdateStarIntensity(sunElevation);
            _resolvedStarMapSeed = ResolveStarMapSeed();
            ResolveSkyColors(out _resolvedSkyZenith, out _resolvedSkyHorizon, out _resolvedSkyNadir);
            UpdateDynamicCelestialAtmosphere(sunElevation, forceRebuild: false, publishOnRebuild: false);
            UpdateGlobalShaderData();
            PushSkyToRenderSettings();

            UpdateSkyMaterial();

            UpdateAegirMaterial();
            UpdateMoonMaterialOverrides();
            UpdatePlanetShine();
            UpdateMoonPhaseShadowVisibility();
            UpdateDeepTextureResidencyState();

            // v5.1: ApplySunOcclusion is the LAST intensity writer.
            // It MULTIPLIES whatever UnderwaterVisuals wrote.
            ApplySunOcclusion();
            PublishCelestialRuntimeSnapshot();
            WriteCelestialBlackBoxTelemetry(ResolveTimeOfDay01(), _smoothedOcclusionFactor, ResolveCelestialBlackBoxRuntimeFlags(), _currentDepthMeters);

            if (Application.isPlaying)
                CelestialEvents.RaiseSunAngleChanged(_currentSunAngle);
        }

        /// <summary>
        /// Applies the tuned first-run celestial atmosphere defaults used to preserve gas giant detail across day and night.
        /// </summary>
        [ContextMenu("Apply Best Visual Defaults")]
        public void ApplyBestVisualDefaults()
        {
            ApplyBestVisualDefaultsInternal();
            EnsureCelestialAtmosphereLutReady();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>
        /// Forces an immediate celestial atmosphere LUT rebuild from the current script-owned sky authoring without waiting for sun movement.
        /// </summary>
        [ContextMenu("Manual Re-bake LUT")]
        public void ManualRebakeLut()
        {
            InvalidateCelestialAtmosphereLutCache();
            EnsureCelestialAtmosphereLutReady(publishOnRebuild: false);
            _resolvedStarMapSeed = ResolveStarMapSeed();
            UpdateGlobalShaderData();
            PushSkyToRenderSettings();
            UpdateSkyMaterial();
            UpdateAegirMaterial();
            UpdateMoonMaterialOverrides();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorUtility.SetDirty(this);
#endif
        }

        // ─────────────────────────────────────────────
        // INITIALIZATION
        // ─────────────────────────────────────────────

        private void ValidateReferences()
        {
            if (sunLight == null && RenderSettings.sun != null)
                sunLight = RenderSettings.sun;

            if (sunLight == null)
                Debug.LogError("[HectonCelestialEngine] Sun Light is not assigned!", this);

            if (aegirTransform == null)
                Debug.LogError("[HectonCelestialEngine] Aegir Transform is not assigned!", this);
            else if (aegirObserverRelativeBody == null)
                aegirTransform.TryGetComponent(out aegirObserverRelativeBody);

            EnforceAegirFixedDirectionLock();

            if (playerTransform == null)
            {
                if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform currentPlayer) && currentPlayer != null)
                {
                    playerTransform = currentPlayer;
                    Debug.LogWarning("[HectonCelestialEngine] Player not assigned, using GameBootstrapper player transform.");
                }
            }

            Material activeSkybox = AtmosphereDirector.Skybox;
            if (blendedSkyboxMaterial == null && IsBlendSkyboxMaterial(activeSkybox))
                blendedSkyboxMaterial = activeSkybox;

            if (_skyMaterial == null)
                Debug.LogWarning("[HectonCelestialEngine] Sky Material is not assigned!", this);
        }

        private void EnforceAegirFixedDirectionLock()
        {
            if (aegirObserverRelativeBody == null)
                return;

            Vector3 fallbackDirection = Vector3.forward;
            if (aegirTransform != null && aegirTransform.localPosition.sqrMagnitude > 0.0001f)
                fallbackDirection = aegirTransform.localPosition;

            aegirObserverRelativeBody.EnforceFixedDirectionLock(fallbackDirection);
        }

        private void SyncCrestPrimaryLight()
        {
            if (sunLight == null)
                return;

            if (!ReferenceEquals(RenderSettings.sun, sunLight))
                RenderSettings.sun = sunLight;

            IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
            if (oceanKinematicsService == null)
                return;

            IHectonOceanKinematics oceanKinematics = oceanKinematicsService.ActiveProvider;
            if (oceanKinematics == null)
                return;

            oceanKinematics.TryAssignPrimaryLight(sunLight);
        }

        private void ApplySurfaceCloudShadowCookie(float deltaTime)
        {
            if (sunLight == null || sunLight.type != LightType.Directional)
                return;

            if (!TryResolveSunAdditionalLightData(out UniversalAdditionalLightData lightData))
                return;

            Texture2D selectedCookie = ResolveDirectionalShadowCookie(
                deltaTime,
                out Vector2 targetCookieSize,
                out Vector2 targetCookieOffset);
            if (selectedCookie == null)
            {
                if (_aegirRingShadowCookieBound)
                    DetachAegirRingShadowCookie();
                else if (_sunCookieDefaultsCaptured)
                    RestoreSurfaceCloudShadowCookie();
                return;
            }

            CaptureSunCookieDefaults(lightData);
            _aegirRingShadowCookieBound = ReferenceEquals(selectedCookie, ResolveAegirRingShadowCookie());

            if (!ReferenceEquals(sunLight.cookie, selectedCookie))
                sunLight.cookie = selectedCookie;

            if ((lightData.lightCookieSize - targetCookieSize).sqrMagnitude > SurfaceCloudShadowCookieEpsilon)
                lightData.lightCookieSize = targetCookieSize;

            if ((lightData.lightCookieOffset - targetCookieOffset).sqrMagnitude > SurfaceCloudShadowCookieEpsilon)
                lightData.lightCookieOffset = targetCookieOffset;
        }

        private Texture2D ResolveDirectionalShadowCookie(
            float deltaTime,
            out Vector2 targetCookieSize,
            out Vector2 targetCookieOffset)
        {
            if (IsAegirRingCookieVisible())
            {
                Texture2D ringCookie = ResolveAegirRingShadowCookie();
                float ringSize = Mathf.Max(8f, aegirRingShadowCookieSize);
                targetCookieSize = new Vector2(ringSize, ringSize);
                targetCookieOffset = _aegirRingShadowCookieOffset;
                return ringCookie;
            }

            if (_surfaceCloudShadowCookie == null)
            {
                targetCookieSize = Vector2.zero;
                targetCookieOffset = Vector2.zero;
                return null;
            }

            float cookieSize = Mathf.Max(8f, _surfaceCloudShadowCookieSize);
            Vector2 scrollDirection = ResolveSurfaceCloudShadowScrollDirection();
            _surfaceCloudShadowCookieOffset += scrollDirection * (Mathf.Max(0f, _surfaceCloudShadowCookieScrollSpeed) * Mathf.Max(0f, deltaTime));
            _surfaceCloudShadowCookieOffset.x = RepeatCookieOffset(_surfaceCloudShadowCookieOffset.x, cookieSize);
            _surfaceCloudShadowCookieOffset.y = RepeatCookieOffset(_surfaceCloudShadowCookieOffset.y, cookieSize);

            targetCookieSize = new Vector2(cookieSize, cookieSize);
            targetCookieOffset = _surfaceCloudShadowCookieOffset;
            return _surfaceCloudShadowCookie;
        }

        private bool IsAegirRingCookieVisible()
        {
            if (ResolveAegirRingShadowCookie() == null)
                return false;

            if (!TryResolveAegirSkyDirection(out float3 toAegir))
                return false;

            return toAegir.y > aegirRingShadowHorizonThreshold;
        }

        private Texture2D ResolveAegirRingShadowCookie()
        {
            return aegirRingShadowCookie != null
                ? aegirRingShadowCookie
                : _runtimeAegirRingShadowCookie;
        }

        private void EnsureAegirRingShadowCookieReady()
        {
            if (aegirRingShadowCookie != null)
            {
                ReleaseRuntimeAegirRingShadowCookie();
                return;
            }

            if (_runtimeAegirRingShadowCookie != null)
                return;

            int resolution = AegirRingShadowGeneratedCookieResolution;
            _runtimeAegirRingShadowCookie = new Texture2D(
                resolution,
                resolution,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "__HectonAegirRingShadowCookie",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            }; // COLD ALLOC: Texture2D[256x256 RGBA32] - fallback Aegir parallel-line directional-light cookie when no authored asset is wired - owner: HectonCelestialEngine

            float invResolution = math.rcp(Mathf.Max(1, resolution - 1));
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float u = x * invResolution;
                    float primary = CinematicMath.FastTriangleWave01((u * 13f) + 0.17f);
                    float secondary = CinematicMath.FastTriangleWave01((u * 29f) + 0.41f);
                    float primaryLine = 1f - Mathf.SmoothStep(0.018f, 0.085f, primary);
                    float secondaryLine = 1f - Mathf.SmoothStep(0.012f, 0.052f, secondary);
                    float lineMask = Mathf.Clamp01(Mathf.Max(primaryLine, secondaryLine * 0.62f));
                    byte luma = (byte)Mathf.RoundToInt(Mathf.Lerp(255f, AegirRingShadowGeneratedLineMinLuma * 255f, lineMask));
                    _runtimeAegirRingShadowCookie.SetPixel(x, y, new Color32(luma, luma, luma, 255));
                }
            }

            _runtimeAegirRingShadowCookie.Apply(false, true);
        }

        private void ReleaseRuntimeAegirRingShadowCookie()
        {
            if (_runtimeAegirRingShadowCookie == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeAegirRingShadowCookie);
            else
                DestroyImmediate(_runtimeAegirRingShadowCookie);

            _runtimeAegirRingShadowCookie = null;
        }

        private bool TryResolveSunAdditionalLightData(out UniversalAdditionalLightData lightData)
        {
            lightData = null;
            if (sunLight == null)
                return false;

            if (_sunAdditionalLightData != null && _sunAdditionalLightData.transform == sunLight.transform)
            {
                lightData = _sunAdditionalLightData;
                return true;
            }

            if (!sunLight.TryGetComponent(out _sunAdditionalLightData))
                return false;

            lightData = _sunAdditionalLightData;
            return true;
        }

        private void CaptureSunCookieDefaults(UniversalAdditionalLightData lightData)
        {
            if (_sunCookieDefaultsCaptured || sunLight == null || lightData == null)
                return;

            _cachedSunCookie = sunLight.cookie;
            _cachedSunCookieSize = lightData.lightCookieSize;
            _cachedSunCookieOffset = lightData.lightCookieOffset;
            _surfaceCloudShadowCookieOffset = _cachedSunCookieOffset;
            _aegirRingShadowCookieOffset = Vector2.zero;
            _sunCookieDefaultsCaptured = true;
        }

        private Vector2 ResolveSurfaceCloudShadowScrollDirection()
        {
            Vector2 wind = new Vector2(_surfaceWeatherWindDirection.x, _surfaceWeatherWindDirection.y);
            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService != null && weatherService.IsInitialized)
            {
                Vector3 globalWind = weatherService.GlobalWindVector;
                wind = new Vector2(globalWind.x, globalWind.z);
            }

            if (wind.sqrMagnitude <= 0.0001f)
                wind = Vector2.right;

            return Mathf.Abs(wind.x) >= Mathf.Abs(wind.y)
                ? new Vector2(wind.x < 0f ? -1f : 1f, 0f)
                : new Vector2(0f, wind.y < 0f ? -1f : 1f);
        }

        private static float RepeatCookieOffset(float value, float size)
        {
            if (size <= 0.0001f)
                return value;

            return Mathf.Repeat(value + size * 0.5f, size) - size * 0.5f;
        }

        private void RestoreSurfaceCloudShadowCookie()
        {
            if (!_sunCookieDefaultsCaptured)
                return;

            if (sunLight != null)
                sunLight.cookie = _cachedSunCookie;

            if (_sunAdditionalLightData != null)
            {
                _sunAdditionalLightData.lightCookieSize = _cachedSunCookieSize;
                _sunAdditionalLightData.lightCookieOffset = _cachedSunCookieOffset;
            }

            _aegirRingShadowCookieBound = false;
            _sunCookieDefaultsCaptured = false;
        }

        private void DetachAegirRingShadowCookie()
        {
            if (sunLight != null && ReferenceEquals(sunLight.cookie, ResolveAegirRingShadowCookie()))
                sunLight.cookie = null;

            _aegirRingShadowCookieBound = false;

            if (_sunCookieDefaultsCaptured)
                RestoreSurfaceCloudShadowCookie();
        }

        private void EnsureCelestialAtmosphereLutReady(bool publishOnRebuild = true)
        {
            EnsureCelestialAtmosphereAuthoring();
            EnsureCelestialAtmosphereTexture();
            float sunElevation = GetCurrentSunElevationForAtmosphere();
            _currentSunAngle = sunElevation;
            UpdateSkyboxBlend(sunElevation);
            ResolveSkyColors(out _resolvedSkyZenith, out _resolvedSkyHorizon, out _resolvedSkyNadir);
            UpdateDynamicCelestialAtmosphere(
                sunElevation,
                forceRebuild: true,
                publishOnRebuild: publishOnRebuild);
        }

        private void InvalidateCelestialAtmosphereLutCache()
        {
            _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
            _lastAtmosphereBakeDayWeight = -1f;
            _lastAtmosphereBakeSunsetWeight = -1f;
            _lastAtmosphereBakeNightWeight = -1f;
            _lastAtmosphereBakeSkyZenith = default;
            _lastAtmosphereBakeSkyHorizon = default;
            _lastAtmosphereBakeSkyNadir = default;
        }

        private void EnsureCelestialAtmosphereAuthoring()
        {
            EnsureBestVisualDefaults();

            bool gradientChanged = false;
            if (dayAtmosphere == null)
            {
                dayAtmosphere = CreateDefaultDayAtmosphereGradient();
                gradientChanged = true;
            }

            if (sunsetAtmosphere == null)
            {
                sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();
                gradientChanged = true;
            }

            if (nightAtmosphere == null)
            {
                nightAtmosphere = CreateDefaultNightAtmosphereGradient();
                gradientChanged = true;
            }

            if (dayAtmosphereDensity == null || dayAtmosphereDensity.length == 0)
                dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();

            if (sunsetAtmosphereDensity == null || sunsetAtmosphereDensity.length == 0)
                sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();

            if (nightAtmosphereDensity == null || nightAtmosphereDensity.length == 0)
                nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();

            if (gradientChanged)
                MarkAtmosphereGradientSamplesDirty();
        }

        private void EnsureBestVisualDefaults()
        {
            if (_visualDefaultsVersion >= BestVisualDefaultsVersion)
                return;

            ApplyBestVisualDefaultsInternal();
        }

        private void ApplyBestVisualDefaultsInternal()
        {
            if (_visualDefaultsVersion < 3)
            {
                horizonDensity = 1.1f;
                zenithTransparency = 0.84f;
                atmosphereBlendPower = 1.65f;
                _surfaceFogDensityMultiplier = 1.35f;
                _surfaceSkyHazeIntensityMultiplier = 1.2f;
                _surfaceHazeSkyTintInfluence = 0.12f;
                _atmosphereTransmittanceWeight = 0.92f;
                _atmosphereInscatterWeight = 0.78f;
                _moonAtmosphereTransmittanceMultiplier = 0.78f;
                _moonAtmosphereInscatterMultiplier = 0.42f;

                _horizonBrightnessScale = 0.7f;
                _horizonZenithBlend = 0.22f;

                dayAtmosphere = CreateDefaultDayAtmosphereGradient();
                sunsetAtmosphere = CreateDefaultSunsetAtmosphereGradient();
                nightAtmosphere = CreateDefaultNightAtmosphereGradient();
                MarkAtmosphereGradientSamplesDirty();

                dayAtmosphereDensity = CreateDefaultDayAtmosphereDensityCurve();
                sunsetAtmosphereDensity = CreateDefaultSunsetAtmosphereDensityCurve();
                nightAtmosphereDensity = CreateDefaultNightAtmosphereDensityCurve();

                dayAtmosphereDensityScale = 1f;
                sunsetAtmosphereDensityScale = 0.78f;
                nightAtmosphereDensityScale = 0.16f;

                dayAtmosphereExposure = 1.02f;
                sunsetAtmosphereExposure = 0.58f;
                nightAtmosphereExposure = NightAtmosphereInscatterFloor;

                sunsetAtmosphereBandDegrees = 16f;
                nightAtmosphereTransitionDegrees = 10f;
                atmosphereLutRebuildSunAngleThreshold = 0.35f;
            }

            if (_visualDefaultsVersion < 4)
            {
                _surfaceFogManualColorBlend = 0f;
                _surfaceFogManualColor = new Color(0.66f, 0.71f, 0.77f, 1f);
                _surfaceFogSkyColorInfluence = 0.32f;
                _surfaceFogAmbientColorInfluence = 0.22f;
                _surfaceHazeHorizonSpread = 1.35f;
            }

            if (_visualDefaultsVersion < 5)
            {
                _surfaceHorizonMistShelfIntensity = 1f;
                _surfaceHorizonMistShelfHeight = 0.16f;
                _surfaceHorizonMistShelfSoftness = 0.1f;
            }

            _visualDefaultsVersion = BestVisualDefaultsVersion;
        }

        private void EnsureCelestialAtmosphereTexture()
        {
            if (_celestialAtmosphereLutTexture != null)
                return;

            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf)
                ? TextureFormat.RGBAHalf
                : TextureFormat.RGBAFloat;

            _celestialAtmosphereLutTexture = new Texture2D(
                CelestialAtmosphereLutResolution,
                1,
                textureFormat,
                false,
                true)
            {
                name = "HectonCelestialAtmosphereLUT",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void UpdateDynamicCelestialAtmosphere(
            float sunElevation,
            bool forceRebuild,
            bool publishOnRebuild)
        {
            EnsureCelestialAtmosphereAuthoring();
            EnsureCelestialAtmosphereTexture();

            EvaluateCelestialAtmosphereProfileWeights(
                sunElevation,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            float blendedExposure =
                dayWeight * dayAtmosphereExposure +
                sunsetWeight * sunsetAtmosphereExposure +
                nightWeight * nightAtmosphereExposure;
            _currentAtmosphereDensity = Mathf.Clamp01(EvaluateAtmosphereDensityRaw(0f, dayWeight, sunsetWeight, nightWeight));

            bool sunMovedEnough = float.IsPositiveInfinity(_lastAtmosphereBakeSunElevation) ||
                                  Mathf.Abs(Mathf.DeltaAngle(_lastAtmosphereBakeSunElevation, sunElevation)) >= atmosphereLutRebuildSunAngleThreshold;

            bool profileShifted = Mathf.Abs(dayWeight - _lastAtmosphereBakeDayWeight) >= 0.01f ||
                                  Mathf.Abs(sunsetWeight - _lastAtmosphereBakeSunsetWeight) >= 0.01f ||
                                  Mathf.Abs(nightWeight - _lastAtmosphereBakeNightWeight) >= 0.01f;

            bool skyShifted = HasMeaningfulColorShift(_resolvedSkyZenith, _lastAtmosphereBakeSkyZenith) ||
                              HasMeaningfulColorShift(_resolvedSkyHorizon, _lastAtmosphereBakeSkyHorizon) ||
                              HasMeaningfulColorShift(_resolvedSkyNadir, _lastAtmosphereBakeSkyNadir);

            _currentAtmosphereExposure = Mathf.Max(0f, blendedExposure);

            if (!forceRebuild && !sunMovedEnough && !profileShifted && !skyShifted)
                return;

            RebuildCelestialAtmosphereLut(dayWeight, sunsetWeight, nightWeight, publishOnRebuild);
            _lastAtmosphereBakeSunElevation = sunElevation;
            _lastAtmosphereBakeDayWeight = dayWeight;
            _lastAtmosphereBakeSunsetWeight = sunsetWeight;
            _lastAtmosphereBakeNightWeight = nightWeight;
            _lastAtmosphereBakeSkyZenith = _resolvedSkyZenith;
            _lastAtmosphereBakeSkyHorizon = _resolvedSkyHorizon;
            _lastAtmosphereBakeSkyNadir = _resolvedSkyNadir;
        }

        private void RebuildCelestialAtmosphereLut(
            float dayWeight,
            float sunsetWeight,
            float nightWeight,
            bool publishOnRebuild)
        {
            if (_celestialAtmosphereLutTexture == null)
                return;

            float lutTStep = CelestialAtmosphereLutResolution > 1
                ? math.rcp(CelestialAtmosphereLutResolution - 1f)
                : 0f;
            for (int i = 0; i < CelestialAtmosphereLutResolution; i++)
            {
                float t = i * lutTStep;

                float sample01 = EvaluateAtmosphereBlend01(t);
                Color profileColor = EvaluateAtmosphereGradientColor(
                    sample01,
                    dayWeight,
                    sunsetWeight,
                    nightWeight);

                Color skyGradient = EvaluateSkySourceGradientColor(sample01);
                Color lutColor = MultiplyRgb(skyGradient, profileColor);
                lutColor.a = EvaluateAtmosphereTransmittance(
                    sample01,
                    dayWeight,
                    sunsetWeight,
                    nightWeight);
                _celestialAtmosphereLutPixels[i] = lutColor;
            }

            _celestialAtmosphereLutTexture.SetPixels(_celestialAtmosphereLutPixels, 0);
            _celestialAtmosphereLutTexture.Apply(false, false);
            if (publishOnRebuild)
                PublishCelestialAtmosphereLut(pushRenderSettings: true);
        }

        private Color EvaluateAtmosphereGradientColor(
            float t,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            EnsureAtmosphereGradientSamples();
            float4 color =
                SampleAtmosphereGradient(_dayAtmosphereGradientSamples, t) * dayWeight +
                SampleAtmosphereGradient(_sunsetAtmosphereGradientSamples, t) * sunsetWeight +
                SampleAtmosphereGradient(_nightAtmosphereGradientSamples, t) * nightWeight;

            return new Color(color.x, color.y, color.z, 1f);
        }

        private void EnsureAtmosphereGradientSamples()
        {
            EnsureAtmosphereGradientSampleBuffer(ref _dayAtmosphereGradientSamples, nameof(_dayAtmosphereGradientSamples));
            EnsureAtmosphereGradientSampleBuffer(ref _sunsetAtmosphereGradientSamples, nameof(_sunsetAtmosphereGradientSamples));
            EnsureAtmosphereGradientSampleBuffer(ref _nightAtmosphereGradientSamples, nameof(_nightAtmosphereGradientSamples));

            if (!_atmosphereGradientSamplesDirty)
                return;

            RebuildAtmosphereGradientSamples();
            _atmosphereGradientSamplesDirty = false;
        }

        private void EnsureAtmosphereGradientSampleBuffer(ref NativeArray<float4> buffer, string label)
        {
            if (buffer.IsCreated)
                return;

            buffer = new NativeArray<float4>(AtmosphereGradientSampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<float4>[8] - packed atmosphere Gradient samples for manual lerp - owner: HectonCelestialEngine
            NativeMemorySentinel.RegisterNativeArray(buffer, nameof(HectonCelestialEngine), label, NativeAllocationLifetime.Scene);
            _atmosphereGradientSamplesDirty = true;
        }

        private void RebuildAtmosphereGradientSamples()
        {
            float denominator = AtmosphereGradientSampleCount > 1
                ? math.rcp(AtmosphereGradientSampleCount - 1f)
                : 0f;

            for (int i = 0; i < AtmosphereGradientSampleCount; i++)
            {
                float t = i * denominator;
                _dayAtmosphereGradientSamples[i] = EvaluateGradientPacked(dayAtmosphere, t);
                _sunsetAtmosphereGradientSamples[i] = EvaluateGradientPacked(sunsetAtmosphere, t);
                _nightAtmosphereGradientSamples[i] = EvaluateGradientPacked(nightAtmosphere, t);
            }
        }

        private static float4 EvaluateGradientPacked(Gradient gradient, float t)
        {
            if (gradient == null)
                return new float4(1f, 1f, 1f, 1f);

            float normalizedTime = math.saturate(t);
            Color rgb = EvaluateGradientColorKeys(gradient.colorKeys, normalizedTime);
            float alpha = EvaluateGradientAlphaKeys(gradient.alphaKeys, normalizedTime);
            return new float4(rgb.r, rgb.g, rgb.b, alpha);
        }

        private static Color EvaluateGradientColorKeys(GradientColorKey[] keys, float t)
        {
            if (keys == null || keys.Length == 0)
                return Color.white;

            GradientColorKey previous = keys[0];
            if (t <= previous.time)
                return previous.color;

            for (int i = 1; i < keys.Length; i++)
            {
                GradientColorKey next = keys[i];
                if (t <= next.time)
                    return LerpColor(previous.color, next.color, ResolveKeyLerp(previous.time, next.time, t));

                previous = next;
            }

            return previous.color;
        }

        private static float EvaluateGradientAlphaKeys(GradientAlphaKey[] keys, float t)
        {
            if (keys == null || keys.Length == 0)
                return 1f;

            GradientAlphaKey previous = keys[0];
            if (t <= previous.time)
                return math.saturate(previous.alpha);

            for (int i = 1; i < keys.Length; i++)
            {
                GradientAlphaKey next = keys[i];
                if (t <= next.time)
                    return math.saturate(math.lerp(previous.alpha, next.alpha, ResolveKeyLerp(previous.time, next.time, t)));

                previous = next;
            }

            return math.saturate(previous.alpha);
        }

        private static float ResolveKeyLerp(float startTime, float endTime, float t)
        {
            return math.saturate((t - startTime) * math.rcp(math.max(endTime - startTime, 0.0001f)));
        }

        private static Color LerpColor(Color start, Color end, float t)
        {
            return new Color(
                math.lerp(start.r, end.r, t),
                math.lerp(start.g, end.g, t),
                math.lerp(start.b, end.b, t),
                math.lerp(start.a, end.a, t));
        }

        private static float4 SampleAtmosphereGradient(NativeArray<float4> samples, float t)
        {
            if (!samples.IsCreated || samples.Length == 0)
                return new float4(1f, 1f, 1f, 1f);

            float scaled = math.saturate(t) * (samples.Length - 1);
            int index = (int)math.floor(scaled);
            int next = math.min(index + 1, samples.Length - 1);
            return math.lerp(samples[index], samples[next], scaled - index);
        }

        private static float4 ToFloat4(Color color)
        {
            return new float4(color.r, color.g, color.b, color.a);
        }

        private float EvaluateAtmosphereTransmittance(
            float t,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            float density = EvaluateAtmosphereDensityRaw(t, dayWeight, sunsetWeight, nightWeight);
            return 1f - Mathf.Clamp01(density);
        }

        private float EvaluateAtmosphereDensityRaw(
            float t,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            float density =
                EvaluateAnimationCurveManual(dayAtmosphereDensity, t, 0f) * dayWeight * dayAtmosphereDensityScale +
                EvaluateAnimationCurveManual(sunsetAtmosphereDensity, t, 0f) * sunsetWeight * sunsetAtmosphereDensityScale +
                EvaluateAnimationCurveManual(nightAtmosphereDensity, t, 0f) * nightWeight * nightAtmosphereDensityScale;

            float zenithDensityScale = Mathf.Lerp(1f, 0.05f, zenithTransparency);
            float altitudeDensityScale = Mathf.Lerp(
                Mathf.Max(0f, horizonDensity),
                zenithDensityScale,
                t);
            return Mathf.Max(0f, density * altitudeDensityScale);
        }

        private static float EvaluateAnimationCurveManual(AnimationCurve curve, float t, float fallback)
        {
            if (curve == null || curve.length == 0)
                return fallback;

            float sampleTime = math.saturate(t);
            Keyframe previous = curve[0];
            if (sampleTime <= previous.time)
                return previous.value;

            int keyCount = curve.length;
            for (int i = 1; i < keyCount; i++)
            {
                Keyframe next = curve[i];
                if (sampleTime <= next.time)
                    return EvaluateKeyframeHermite(previous, next, sampleTime);

                previous = next;
            }

            return previous.value;
        }

        private static float EvaluateKeyframeHermite(Keyframe previous, Keyframe next, float sampleTime)
        {
            if (float.IsInfinity(previous.outTangent) || float.IsInfinity(next.inTangent))
                return previous.value;

            float duration = math.max(next.time - previous.time, 0.0001f);
            float normalizedTime = math.saturate((sampleTime - previous.time) * math.rcp(duration));
            float normalizedTime2 = normalizedTime * normalizedTime;
            float normalizedTime3 = normalizedTime2 * normalizedTime;

            float basis00 = (2f * normalizedTime3) - (3f * normalizedTime2) + 1f;
            float basis10 = normalizedTime3 - (2f * normalizedTime2) + normalizedTime;
            float basis01 = (-2f * normalizedTime3) + (3f * normalizedTime2);
            float basis11 = normalizedTime3 - normalizedTime2;

            return
                (basis00 * previous.value) +
                (basis10 * previous.outTangent * duration) +
                (basis01 * next.value) +
                (basis11 * next.inTangent * duration);
        }

        private float EvaluateAtmosphereBlend01(float t)
        {
            float clamped = Mathf.Clamp01(t);
            return Mathf.Pow(clamped, Mathf.Max(0.01f, atmosphereBlendPower));
        }

        private Color EvaluateSkySourceGradientColor(float t)
        {
            Color color = Color.Lerp(_resolvedSkyHorizon, _resolvedSkyZenith, t);
            color.a = 1f;
            return color;
        }

        private static Color MultiplyRgb(Color lhs, Color rhs)
        {
            return new Color(
                lhs.r * rhs.r,
                lhs.g * rhs.g,
                lhs.b * rhs.b,
                1f);
        }

        private void EvaluateCelestialAtmosphereProfileWeights(
            float sunElevation,
            out float dayWeight,
            out float sunsetWeight,
            out float nightWeight)
        {
            float safeSunsetBand = Mathf.Max(1f, sunsetAtmosphereBandDegrees);
            float safeNightTransition = Mathf.Max(1f, nightAtmosphereTransitionDegrees);
            float safeTwilightStart = Mathf.Max(0.01f, twilightStartAngle);

            float sunsetWindowT = 1f - Mathf.Clamp01(Mathf.Abs(sunElevation) * math.rcp(safeSunsetBand));
            float twilightWeight = Mathf.SmoothStep(0f, 1f, sunsetWindowT);

            float dayBase = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01(sunElevation * math.rcp(safeTwilightStart)));

            float nightBase = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((twilightEndAngle - sunElevation) * math.rcp(safeNightTransition)));

            dayWeight = dayBase * (1f - twilightWeight);
            sunsetWeight = twilightWeight * (1f - nightBase * 0.85f);
            nightWeight = nightBase * (1f - twilightWeight);

            float totalWeight = dayWeight + sunsetWeight + nightWeight;
            if (totalWeight <= 0.0001f)
            {
                if (sunElevation > twilightStartAngle)
                {
                    dayWeight = 1f;
                    sunsetWeight = 0f;
                    nightWeight = 0f;
                }
                else if (sunElevation < twilightEndAngle)
                {
                    dayWeight = 0f;
                    sunsetWeight = 0f;
                    nightWeight = 1f;
                }
                else
                {
                    dayWeight = 0f;
                    sunsetWeight = 1f;
                    nightWeight = 0f;
                }
                return;
            }

            float invTotalWeight = math.rcp(totalWeight);
            dayWeight *= invTotalWeight;
            sunsetWeight *= invTotalWeight;
            nightWeight *= invTotalWeight;
        }

        private float GetCurrentSunElevationForAtmosphere()
        {
            float3 sunDirection = _resolvedSunDirection;
            if (math.lengthsq(sunDirection) <= 0.0001f && sunLight != null)
                sunDirection = -(float3)sunLight.transform.forward;

            float sinElevation = math.dot(NormalizeVisualRsqrt(sunDirection, new float3(0f, 1f, 0f)), new float3(0f, 1f, 0f));
            return math.degrees(math.asin(math.clamp(sinElevation, -1f, 1f)));
        }

        private static bool HasMeaningfulColorShift(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) >= 0.01f ||
                   Mathf.Abs(a.g - b.g) >= 0.01f ||
                   Mathf.Abs(a.b - b.b) >= 0.01f;
        }

        private static float ComputePerceivedLuminance(Color color)
        {
            return color.r * 0.2126f +
                   color.g * 0.7152f +
                   color.b * 0.0722f;
        }

        private static Color DesaturateColor(Color color, float amount)
        {
            float luminance = ComputePerceivedLuminance(color);
            Color grayscale = new Color(luminance, luminance, luminance, 1f);
            Color result = Color.Lerp(color, grayscale, Mathf.Clamp01(amount));
            result.a = 1f;
            return result;
        }

        private static Color LiftColorTowardsLuminance(Color color, float targetLuminance, float maxWhiteBlend)
        {
            float currentLuminance = ComputePerceivedLuminance(color);
            if (currentLuminance >= targetLuminance)
            {
                color.a = 1f;
                return color;
            }

            float safeTarget = Mathf.Max(targetLuminance, 0.0001f);
            float liftFactor = Mathf.Clamp01((safeTarget - currentLuminance) * math.rcp(safeTarget));
            Color lifted = Color.Lerp(color, Color.white, liftFactor * Mathf.Clamp01(maxWhiteBlend));
            lifted.a = 1f;
            return lifted;
        }

        private static Color NormalizeColorToMax(Color color)
        {
            float maxComponent = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
            if (maxComponent <= 0.0001f)
                return Color.white;

            float invMaxComponent = math.rcp(maxComponent);
            Color normalized = new Color(
                color.r * invMaxComponent,
                color.g * invMaxComponent,
                color.b * invMaxComponent,
                1f);
            return normalized;
        }

        private static bool HasUsableSurfaceColor(Color color)
        {
            return ComputePerceivedLuminance(color) > 0.001f;
        }

        private float ResolveSurfaceSkyExposure()
        {
            if (_atmosphereManager != null)
            {
                float exposure = _atmosphereManager.CurrentSkyExposure;
                if (exposure > 0.001f)
                    return exposure;
            }

            float zenithLuminance = ComputePerceivedLuminance(_resolvedSkyZenith);
            float horizonLuminance = ComputePerceivedLuminance(_resolvedSkyHorizon);
            float nadirLuminance = ComputePerceivedLuminance(_resolvedSkyNadir);
            float blendedLuminance = Mathf.Max(
                0.16f,
                zenithLuminance * 0.42f +
                horizonLuminance * 0.46f +
                nadirLuminance * 0.12f);
            return Mathf.Clamp(blendedLuminance * 1.85f, 0.28f, 1.8f);
        }

        private void PublishCelestialAtmosphereLut(bool pushRenderSettings)
        {
            if (_celestialAtmosphereLutTexture != null)
            {
                Shader.SetGlobalTexture(_ID_CelestialAtmosphereLut, _celestialAtmosphereLutTexture);
                Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, 1f);
                Shader.SetGlobalFloat(_ID_AtmosphereExposure, _currentAtmosphereExposure);
                Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, horizonDensity);
                Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, zenithTransparency);
                Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, atmosphereBlendPower);
                if (pushRenderSettings)
                    PushSkyToRenderSettings();
            }
        }

        private void ReleaseCelestialAtmosphereLut()
        {
            if (_celestialAtmosphereLutTexture == null)
                return;

            Shader.SetGlobalTexture(_ID_CelestialAtmosphereLut, Texture2D.blackTexture);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, 0f);
            Shader.SetGlobalFloat(_ID_AtmosphereExposure, 0f);
            Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, 0f);
            Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, 0f);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, 1f);

            if (Application.isPlaying)
                Destroy(_celestialAtmosphereLutTexture);
            else
                DestroyImmediate(_celestialAtmosphereLutTexture);

            _celestialAtmosphereLutTexture = null;
            _lastAtmosphereBakeSunElevation = float.PositiveInfinity;
            _lastAtmosphereBakeDayWeight = -1f;
            _lastAtmosphereBakeSunsetWeight = -1f;
            _lastAtmosphereBakeNightWeight = -1f;
            _lastAtmosphereBakeSkyZenith = default;
            _lastAtmosphereBakeSkyHorizon = default;
            _lastAtmosphereBakeSkyNadir = default;
        }

        private void EnsureCelestialRuntimeBuffers()
        {
            if (!_orbitJobOutput.IsCreated)
            {
                _orbitJobOutput = new NativeArray<CelestialOrbitJobOutput>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CelestialOrbitJobOutput>[1] - deferred Burst celestial orbit output - owner: HectonCelestialEngine
                NativeMemorySentinel.RegisterNativeArray(_orbitJobOutput, nameof(HectonCelestialEngine), nameof(_orbitJobOutput), NativeAllocationLifetime.Scene);
            }

            EnsureCelestialBlackBoxBuffer();
        }

        private void EnsureCelestialBlackBoxBuffer()
        {
            if (_celestialBlackBox.IsCreated)
                return;

            _celestialBlackBox = new NativeArray<CelestialBlackBoxEntry>(CelestialBlackBoxFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<CelestialBlackBoxEntry>[300] - fixed celestial blackbox ring - owner: HectonCelestialEngine
            NativeMemorySentinel.RegisterNativeArray(_celestialBlackBox, nameof(HectonCelestialEngine), nameof(_celestialBlackBox), NativeAllocationLifetime.Scene);
            _celestialBlackBoxCursor = 0;
            _celestialBlackBoxCount = 0;
            _celestialBlackBoxDumped = false;
        }

        private void DisposeCelestialRuntimeBuffers(bool forceCompleteOrbitJob)
        {
            if (_orbitJobScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _orbitJobHandle, forceCompleteOrbitJob);
                _orbitJobScheduled = false;
            }

            if (_orbitJobOutput.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_orbitJobOutput);
                _orbitJobOutput.Dispose();
                _orbitJobOutput = default;
            }

            if (_celestialBlackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_celestialBlackBox);
                _celestialBlackBox.Dispose();
                _celestialBlackBox = default;
            }

            DisposeAtmosphereGradientSamples();
            _orbitJobPrimed = false;
            _celestialBlackBoxCursor = 0;
            _celestialBlackBoxCount = 0;
        }

        private void MarkAtmosphereGradientSamplesDirty()
        {
            _atmosphereGradientSamplesDirty = true;
        }

        private void DisposeAtmosphereGradientSamples()
        {
            DisposeAtmosphereGradientSampleBuffer(ref _dayAtmosphereGradientSamples);
            DisposeAtmosphereGradientSampleBuffer(ref _sunsetAtmosphereGradientSamples);
            DisposeAtmosphereGradientSampleBuffer(ref _nightAtmosphereGradientSamples);
            _atmosphereGradientSamplesDirty = true;
        }

        private static void DisposeAtmosphereGradientSampleBuffer(ref NativeArray<float4> buffer)
        {
            if (!buffer.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(buffer);
            buffer.Dispose();
            buffer = default;
        }

        private void InitializeFirmamentBakeAtStartup()
        {
            if (!Application.isPlaying || _firmamentStartupBakeAttempted)
                return;

            _firmamentStartupBakeAttempted = true;
            _resolvedStarMapSeed = ResolveStarMapSeed();
            TryBakeFirmamentOnce();
        }

        private void TryBakeFirmamentOnce()
        {
            if (_firmamentBakeComplete)
            {
                return;
            }

            if (!Application.isPlaying || !enableGpuFirmamentBake)
            {
                PublishFirmamentBakeGlobals();
                return;
            }

            ResolveFirmamentBakeCompute();
            if (firmamentBakeCompute == null || !TryResolveFirmamentKernels())
            {
                PublishFirmamentBakeGlobals();
                return;
            }

            int starResolution = ResolveFirmamentCubemapResolution();
            int atmosphereWidth = Mathf.Clamp(atmosphereScatteringLutWidth, 64, 512);
            int atmosphereHeight = Mathf.Clamp(atmosphereScatteringLutHeight, 16, 128);
            EnsureFirmamentStarCubemap(starResolution);
            EnsureAtmosphereScatteringLut(atmosphereWidth, atmosphereHeight);

            if (_bakedStarCubemap == null || _atmosphereScatteringLutTexture == null)
            {
                PublishFirmamentBakeGlobals();
                return;
            }

            int seed = Mathf.Max(1, Mathf.RoundToInt(_resolvedStarMapSeed));
            firmamentBakeCompute.SetVector(
                _ID_HectonStarBakeParams,
                new Vector4(
                    FirmamentStartupStarCount,
                    starResolution,
                    seed,
                    Mathf.Max(0f, firmamentStarIntensity)));
            firmamentBakeCompute.SetVector(
                _ID_HectonStarDistribution,
                new Vector4(
                    Mathf.Max(0.001f, firmamentMilkyWayHalfWidthRadians),
                    Mathf.Clamp01(firmamentMilkyWayProbability),
                    Mathf.Max(0.001f, firmamentMilkyWayCoreBias),
                    Mathf.Clamp01(firmamentStarHaloGain)));
            firmamentBakeCompute.SetVector(
                _ID_HectonGalaxyArmShape,
                new Vector4(
                    Mathf.Clamp(firmamentLatitudeCompression, 0.05f, 1f),
                    0f,
                    0f,
                    0f));
            firmamentBakeCompute.SetTexture(_firmamentClearKernel, _ID_BakedStarCubemap, _bakedStarCubemap);
            firmamentBakeCompute.SetTexture(_firmamentStarKernel, _ID_BakedStarCubemap, _bakedStarCubemap);
            firmamentBakeCompute.Dispatch(
                _firmamentClearKernel,
                Mathf.CeilToInt(starResolution * ComputeThreadGroup8Inv),
                Mathf.CeilToInt(starResolution * ComputeThreadGroup8Inv),
                6);
            firmamentBakeCompute.Dispatch(
                _firmamentStarKernel,
                Mathf.CeilToInt(FirmamentStartupStarCount * FirmamentStarBakeThreadsInv),
                1,
                1);

            firmamentBakeCompute.SetTexture(_firmamentAtmosphereKernel, _ID_HectonAtmosphereScatteringLut, _atmosphereScatteringLutTexture);
            firmamentBakeCompute.SetVector(_ID_HectonAtmosphereLutSize, new Vector4(atmosphereWidth, atmosphereHeight, 0f, 0f));
            firmamentBakeCompute.SetVector(_ID_HectonRayleighBeta, new Vector4(0.0058f, 0.0135f, 0.0331f, 0f));
            firmamentBakeCompute.SetVector(_ID_HectonMieBeta, new Vector4(0.004f, 0.004f, 0.004f, 0.8f));
            firmamentBakeCompute.SetVector(
                _ID_HectonAtmosphereParams,
                new Vector4(
                    8f,
                    2f,
                    Mathf.Max(0f, atmosphereScatteringDensity),
                    Mathf.Max(0f, atmosphereScatteringExposure)));
            firmamentBakeCompute.Dispatch(
                _firmamentAtmosphereKernel,
                Mathf.CeilToInt(atmosphereWidth * ComputeThreadGroup8Inv),
                Mathf.CeilToInt(atmosphereHeight * ComputeThreadGroup8Inv),
                1);

            _firmamentBakedSeed = seed;
            _firmamentBakedResolution = starResolution;
            _atmosphereScatteringBakedWidth = atmosphereWidth;
            _atmosphereScatteringBakedHeight = atmosphereHeight;
            _firmamentBakeComplete = true;
            PublishFirmamentBakeGlobals();
        }

        private int ResolveFirmamentCubemapResolution()
        {
            int requested = Mathf.Clamp(
                firmamentCubemapResolution,
                FirmamentMinResolution,
                FirmamentHighVramResolutionCap);
            int hardwareMax = SystemInfo.maxTextureSize > 0
                ? Mathf.Min(SystemInfo.maxTextureSize, FirmamentHighVramResolutionCap)
                : FirmamentMx350ResolutionCap;
            int memoryMb = SystemInfo.graphicsMemorySize;
            int memoryMax = FirmamentMx350ResolutionCap;
            if (memoryMb > FirmamentMidVramMemoryCapMb)
                memoryMax = FirmamentHighVramResolutionCap;
            else if (memoryMb > FirmamentMx350MemoryCapMb)
                memoryMax = FirmamentMidVramResolutionCap;

            int capped = Mathf.Clamp(
                Mathf.Min(requested, hardwareMax, memoryMax),
                FirmamentMinResolution,
                FirmamentHighVramResolutionCap);
            int resolved = FirmamentMinResolution;
            while (resolved < capped && resolved < FirmamentHighVramResolutionCap)
                resolved <<= 1;
            if (resolved > capped)
                resolved >>= 1;
            resolved = Mathf.Max(FirmamentMinResolution, resolved);

            if (resolved < requested && !_firmamentResolutionWarningPublished)
            {
                _firmamentResolutionWarningPublished = true;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _FirmamentResolutionClampWarningHash,
                    _FirmamentBakeContextHash,
                    requested - resolved);
            }

            return resolved;
        }

        private void ResolveFirmamentBakeCompute()
        {
#if UNITY_EDITOR
            if (firmamentBakeCompute == null)
                firmamentBakeCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(FirmamentBakeComputeAssetPath);
#endif
        }

        private bool TryResolveFirmamentKernels()
        {
            if (firmamentBakeCompute == null)
                return false;

            if (_firmamentClearKernel < 0)
            {
                if (!firmamentBakeCompute.HasKernel("ClearStarCubemap"))
                    return false;

                _firmamentClearKernel = firmamentBakeCompute.FindKernel("ClearStarCubemap");
            }

            if (_firmamentStarKernel < 0)
            {
                if (!firmamentBakeCompute.HasKernel("BakeSpectralStars"))
                    return false;

                _firmamentStarKernel = firmamentBakeCompute.FindKernel("BakeSpectralStars");
            }

            if (_firmamentAtmosphereKernel < 0)
            {
                if (!firmamentBakeCompute.HasKernel("BakeAtmosphereLut"))
                    return false;

                _firmamentAtmosphereKernel = firmamentBakeCompute.FindKernel("BakeAtmosphereLut");
            }

            return true;
        }

        private void EnsureFirmamentStarCubemap(int resolution)
        {
            if (_bakedStarCubemap != null &&
                _bakedStarCubemap.width == resolution &&
                _bakedStarCubemap.IsCreated())
            {
                return;
            }

            ReleaseFirmamentStarCubemap();
            _bakedStarCubemap = new RenderTexture(
                resolution,
                resolution,
                0,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "__HectonBakedSpectralStarCubemap",
                dimension = TextureDimension.Cube,
                volumeDepth = 6,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            }; // COLD ALLOC: RenderTexture[cubemap] - startup spectral star bake target - owner: HectonCelestialEngine
            _bakedStarCubemap.Create();
        }

        private void EnsureAtmosphereScatteringLut(int width, int height)
        {
            if (_atmosphereScatteringLutTexture != null &&
                _atmosphereScatteringLutTexture.width == width &&
                _atmosphereScatteringLutTexture.height == height &&
                _atmosphereScatteringLutTexture.IsCreated())
            {
                return;
            }

            ReleaseAtmosphereScatteringLut();
            _atmosphereScatteringLutTexture = new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGBHalf,
                RenderTextureReadWrite.Linear)
            {
                name = "__HectonAtmosphereScatteringLUT",
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            }; // COLD ALLOC: RenderTexture[2D LUT] - startup Rayleigh/Mie scattering lookup - owner: HectonCelestialEngine
            _atmosphereScatteringLutTexture.Create();
        }

        private void PublishFirmamentBakeGlobals()
        {
            bool hasBakedStars = _firmamentBakeComplete && _bakedStarCubemap != null;
            bool hasAtmosphereLut = _firmamentBakeComplete && _atmosphereScatteringLutTexture != null;
            Texture2D twinkleLut = starTwinkleNoiseLut != null ? starTwinkleNoiseLut : Texture2D.whiteTexture;

            Shader.SetGlobalTexture(_ID_StarTwinkleLut, twinkleLut);
            Shader.SetGlobalTexture(_ID_BakedStarCubemap, hasBakedStars ? _bakedStarCubemap : null);
            Shader.SetGlobalFloat(_ID_BakedStarCubemapReady, hasBakedStars ? 1f : 0f);
            Shader.SetGlobalTexture(_ID_HectonAtmosphereScatteringLut, hasAtmosphereLut ? (Texture)_atmosphereScatteringLutTexture : Texture2D.blackTexture);
            Shader.SetGlobalFloat(_ID_HectonAtmosphereScatteringLutReady, hasAtmosphereLut ? 1f : 0f);
            ApplyFirmamentStaticMaterialBindings(_skyMaterial);
        }

        private void ApplyFirmamentStaticMaterialBindings(Material targetMaterial)
        {
            if (targetMaterial == null)
                return;

            bool hasBakedStars = _firmamentBakeComplete && _bakedStarCubemap != null;
            bool hasAtmosphereLut = _firmamentBakeComplete && _atmosphereScatteringLutTexture != null;
            Texture2D twinkleLut = starTwinkleNoiseLut != null ? starTwinkleNoiseLut : Texture2D.whiteTexture;

            SetMaterialTexture(targetMaterial, _ID_StarTwinkleLut, twinkleLut);
            SetMaterialTexture(targetMaterial, _ID_BakedStarCubemap, hasBakedStars ? _bakedStarCubemap : null);
            SetMaterialFloat(targetMaterial, _ID_BakedStarCubemapReady, hasBakedStars ? 1f : 0f);
            SetMaterialTexture(targetMaterial, _ID_HectonAtmosphereScatteringLut, hasAtmosphereLut ? (Texture)_atmosphereScatteringLutTexture : Texture2D.blackTexture);
            SetMaterialFloat(targetMaterial, _ID_HectonAtmosphereScatteringLutReady, hasAtmosphereLut ? 1f : 0f);
        }

        private void ReleaseFirmamentBakeResources()
        {
            Shader.SetGlobalTexture(_ID_BakedStarCubemap, null);
            Shader.SetGlobalFloat(_ID_BakedStarCubemapReady, 0f);
            Shader.SetGlobalTexture(_ID_StarTwinkleLut, Texture2D.whiteTexture);
            Shader.SetGlobalTexture(_ID_HectonAtmosphereScatteringLut, Texture2D.blackTexture);
            Shader.SetGlobalFloat(_ID_HectonAtmosphereScatteringLutReady, 0f);
            Shader.SetGlobalVector(_ID_HectonEclipseWaterShadowParams, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonEclipseWaterShadowDirection, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonRingCausticsParams, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonRingCausticsDirection, Vector4.zero);
            Shader.SetGlobalMatrix(_ID_HectonSkyRotation, Matrix4x4.identity);
            Shader.SetGlobalInt(_ID_HectonSkyOccluderCount, 0);
            Shader.SetGlobalVectorArray(_ID_HectonSkyOccluders, _skyOccluders);

            ReleaseFirmamentStarCubemap();
            ReleaseAtmosphereScatteringLut();
            _firmamentBakeComplete = false;
            _firmamentBakedSeed = 0;
            _firmamentBakedResolution = 0;
            _atmosphereScatteringBakedWidth = 0;
            _atmosphereScatteringBakedHeight = 0;
            ApplyFirmamentStaticMaterialBindings(_skyMaterial);
        }

        private void ReleaseFirmamentStarCubemap()
        {
            if (_bakedStarCubemap == null)
                return;

            if (_bakedStarCubemap.IsCreated())
                _bakedStarCubemap.Release();

            if (Application.isPlaying)
                Destroy(_bakedStarCubemap);
            else
                DestroyImmediate(_bakedStarCubemap);

            _bakedStarCubemap = null;
        }

        private void ReleaseAtmosphereScatteringLut()
        {
            if (_atmosphereScatteringLutTexture == null)
                return;

            if (_atmosphereScatteringLutTexture.IsCreated())
                _atmosphereScatteringLutTexture.Release();

            if (Application.isPlaying)
                Destroy(_atmosphereScatteringLutTexture);
            else
                DestroyImmediate(_atmosphereScatteringLutTexture);

            _atmosphereScatteringLutTexture = null;
        }

        private void PushSkyToRenderSettings()
        {
            AtmosphericLightingState state = BuildSurfaceAtmosphericLightingState();
            ApplySurfaceAtmosphericLightingState(state);
        }

        private AtmosphericLightingState BuildSurfaceAtmosphericLightingState()
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            Color skyHorizonColor = _resolvedSkyHorizon;
            skyHorizonColor.a = 1f;
            Color skyZenithColor = _resolvedSkyZenith;
            skyZenithColor.a = 1f;
            Color skyNadirColor = _resolvedSkyNadir;
            skyNadirColor.a = 1f;

            Color skyFogAnchor = Color.Lerp(skyHorizonColor, skyZenithColor, 0.18f);
            skyFogAnchor = Color.Lerp(skyFogAnchor, skyNadirColor, 0.08f);

            Color atmosphereFogColor = _atmosphereManager != null
                ? _atmosphereManager.CurrentFogColor
                : skyFogAnchor;
            if (!HasUsableSurfaceColor(atmosphereFogColor))
                atmosphereFogColor = skyFogAnchor;
            atmosphereFogColor.a = 1f;

            float horizonTransmittance = EvaluateAtmosphereTransmittance(
                0f,
                dayWeight,
                sunsetWeight,
                nightWeight);
            float horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
            float lowSunFactor = 1f - Mathf.Clamp01((_currentSunAngle + 8f) * Inv88);
            float hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
            Color ambientBaseColor = ResolveSurfaceAmbientBaseColor();
            Color horizonSkyTint = Color.Lerp(skyHorizonColor, skyZenithColor, 0.14f);
            horizonSkyTint = Color.Lerp(horizonSkyTint, skyNadirColor, 0.05f);
            if (_celestialAtmosphereLutPixels.Length > 0)
            {
                horizonSkyTint = Color.Lerp(horizonSkyTint, _celestialAtmosphereLutPixels[0], 0.14f);
                horizonTransmittance = Mathf.Clamp01(_celestialAtmosphereLutPixels[0].a);
                horizonHaze = 1f - Mathf.Clamp01(horizonTransmittance);
                hazeResponse = Mathf.Clamp01(horizonHaze * 0.72f + lowSunFactor * 0.28f);
            }
            horizonSkyTint.a = 1f;

            float baseFogDensity = ResolveSurfaceBaseFogDensity();
            float dayVisibility = Mathf.Clamp01((_currentSunAngle + 2f) * Inv64);
            float middayFogReduction = Mathf.Lerp(1.08f, 0.82f, dayVisibility);
            float fogDensity = Mathf.Max(
                0.0001f,
                baseFogDensity *
                Mathf.Lerp(0.82f, 1.28f, hazeResponse) *
                middayFogReduction *
                Mathf.Max(0.25f, _surfaceFogDensityMultiplier));

            Color fogOwnerColor = _surfaceWeatherFogOverrideActive
                ? _surfaceWeatherFogColor
                : Color.Lerp(
                    atmosphereFogColor,
                    _surfaceFogManualColor,
                    Mathf.Clamp01(_surfaceFogManualColorBlend));
            fogOwnerColor.a = 1f;

            float skyTintWeight =
                Mathf.Lerp(0.06f, 0.18f, hazeResponse) * Mathf.Clamp01(_surfaceFogSkyColorInfluence) +
                Mathf.Lerp(0.02f, 0.18f, hazeResponse) * _surfaceHazeSkyTintInfluence;
            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 1.22f, sunsetWeight);
            skyTintWeight = Mathf.Lerp(skyTintWeight, skyTintWeight * 0.82f, nightWeight);
            skyTintWeight = Mathf.Clamp01(skyTintWeight);

            float ambientWeight = Mathf.Lerp(0.08f, 0.24f, 1f - dayVisibility) *
                                  Mathf.Clamp01(_surfaceFogAmbientColorInfluence);

            Color horizonFogColor = Color.Lerp(fogOwnerColor, horizonSkyTint, skyTintWeight);
            horizonFogColor = Color.Lerp(horizonFogColor, ambientBaseColor, ambientWeight);
            float atmosphereRestoreWeight =
                (1f - Mathf.Clamp01(_surfaceFogManualColorBlend)) *
                Mathf.Lerp(0.18f, 0.36f, hazeResponse);
            horizonFogColor = Color.Lerp(horizonFogColor, atmosphereFogColor, atmosphereRestoreWeight);
            float fogTargetLuminance = Mathf.Max(
                ComputePerceivedLuminance(fogOwnerColor) * Mathf.Lerp(1f, 0.88f, hazeResponse),
                ComputePerceivedLuminance(horizonSkyTint),
                ComputePerceivedLuminance(skyZenithColor) * Mathf.Lerp(0.42f, 0.58f, dayVisibility));
            horizonFogColor = LiftColorTowardsLuminance(
                horizonFogColor,
                fogTargetLuminance,
                Mathf.Lerp(0.22f, 0.38f, dayVisibility));
            horizonFogColor = DesaturateColor(
                horizonFogColor,
                Mathf.Lerp(0.14f, 0.22f, dayWeight) + hazeResponse * 0.04f);
            horizonFogColor.a = 1f;

            float hazeSpread = Mathf.Max(0.5f, _surfaceHazeHorizonSpread);
            float hazeIntensity = Mathf.Lerp(0.12f, 0.34f, hazeResponse) *
                                  Mathf.Max(0.25f, _surfaceSkyHazeIntensityMultiplier);
            hazeIntensity *= Mathf.Lerp(1f, 1f + (hazeSpread - 1f) * 0.35f, hazeResponse);
            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 1.18f, sunsetWeight);
            hazeIntensity = Mathf.Lerp(hazeIntensity, hazeIntensity * 0.42f, nightWeight);

            float hazeFalloff = Mathf.Lerp(6.1f, 3.8f, hazeResponse) * math.rcp(hazeSpread);
            hazeFalloff = Mathf.Lerp(hazeFalloff, hazeFalloff * 0.9f, sunsetWeight);
            hazeFalloff = Mathf.Lerp(hazeFalloff, hazeFalloff * 1.15f, nightWeight);
            hazeFalloff = Mathf.Clamp(hazeFalloff, 1.35f, 8f);

            Color hazeColor = Color.Lerp(
                horizonFogColor,
                horizonSkyTint,
                Mathf.Clamp01(0.08f + _surfaceHazeSkyTintInfluence * 0.38f));
            hazeColor = Color.Lerp(hazeColor, fogOwnerColor, 0.38f);
            hazeColor = Color.Lerp(hazeColor, ResolveScriptSunsetHorizonColor(), sunsetWeight * 0.16f);
            hazeColor = LiftColorTowardsLuminance(
                hazeColor,
                fogTargetLuminance,
                Mathf.Lerp(0.24f, 0.42f, dayVisibility));
            hazeColor = DesaturateColor(hazeColor, Mathf.Lerp(0.18f, 0.28f, dayWeight));
            hazeColor.a = 1f;

            float hazeSunTintStrength = Mathf.Lerp(0.1f, 0.3f, hazeResponse);
            hazeSunTintStrength = Mathf.Lerp(hazeSunTintStrength, hazeSunTintStrength * 1.25f, sunsetWeight);
            hazeSunTintStrength = Mathf.Lerp(hazeSunTintStrength, hazeSunTintStrength * 0.6f, nightWeight);

            float mistShelfIntensity = Mathf.Lerp(0.22f, 0.56f, hazeResponse) *
                                       Mathf.Clamp(_surfaceHorizonMistShelfIntensity, 0f, 2f);
            mistShelfIntensity *= Mathf.Lerp(1f, 1f + (hazeSpread - 1f) * 0.24f, hazeResponse);
            mistShelfIntensity = Mathf.Lerp(mistShelfIntensity, mistShelfIntensity * 1.12f, sunsetWeight);
            mistShelfIntensity = Mathf.Lerp(mistShelfIntensity, mistShelfIntensity * 0.34f, nightWeight);
            mistShelfIntensity = Mathf.Clamp(mistShelfIntensity, 0f, 2f);

            float mistShelfHeight = _surfaceHorizonMistShelfHeight *
                                    Mathf.Lerp(0.92f, 1.18f, hazeResponse);
            mistShelfHeight = Mathf.Clamp(mistShelfHeight, 0.04f, 0.32f);

            float mistShelfSoftness = _surfaceHorizonMistShelfSoftness *
                                      Mathf.Lerp(0.9f, 1.12f, hazeResponse);
            mistShelfSoftness = Mathf.Clamp(mistShelfSoftness, 0.02f, 0.24f);

            Color ambientSkyColor = Color.Lerp(ambientBaseColor, skyZenithColor, 0.7f);
            Color ambientEquatorColor = Color.Lerp(ambientBaseColor, horizonFogColor, 0.62f);
            Color ambientGroundColor = Color.Lerp(skyNadirColor, ambientEquatorColor, 0.46f);
            ambientSkyColor.a = 1f;
            ambientEquatorColor.a = 1f;
            ambientGroundColor.a = 1f;

            float exposureBase = ResolveSurfaceSkyExposure();
            float skyLuminanceMultiplier = Mathf.Max(0.35f, ResolveSkyLuminanceMultiplier());
            float horizonBrightness = Mathf.Max(
                ComputePerceivedLuminance(skyHorizonColor),
                ComputePerceivedLuminance(horizonFogColor));
            float ambientBrightnessLift = Mathf.Lerp(0.82f, 1.26f, dayVisibility);
            ambientBrightnessLift *= Mathf.Lerp(0.86f, 1.08f, Mathf.Clamp01(horizonBrightness * 1.35f));
            float ambientIntensity = Mathf.Clamp(
                Mathf.Max(0.24f, exposureBase) *
                skyLuminanceMultiplier *
                ambientBrightnessLift *
                Mathf.Lerp(0.9f, 1.08f, 1f - nightWeight * 0.2f),
                0.24f,
                2.4f);
            float sunIntensityMultiplier = ResolveSurfaceSunMultiplier();
            float directionalLightIntensity = ResolveSurfaceDirectionalLightIntensity(sunIntensityMultiplier);

            return new AtmosphericLightingState
            {
                IsValid = 1,
                SunElevationDegrees = _currentSunAngle,
                SkyExposure = Mathf.Max(0f, exposureBase),
                FogDensity = fogDensity,
                AmbientIntensity = ambientIntensity,
                SunIntensityMultiplier = sunIntensityMultiplier,
                DirectionalLightIntensity = directionalLightIntensity,
                HorizonHazeIntensity = hazeIntensity,
                HorizonHazeFalloff = hazeFalloff,
                HorizonHazeSunTintStrength = hazeSunTintStrength,
                HorizonMistShelfIntensity = mistShelfIntensity,
                HorizonMistShelfHeight = mistShelfHeight,
                HorizonMistShelfSoftness = mistShelfSoftness,
                SkyZenithColor = _resolvedSkyZenith,
                SkyHorizonColor = _resolvedSkyHorizon,
                SkyNadirColor = _resolvedSkyNadir,
                FogColor = horizonFogColor,
                HorizonHazeColor = hazeColor,
                AmbientSkyColor = ambientSkyColor,
                AmbientEquatorColor = ambientEquatorColor,
                AmbientGroundColor = ambientGroundColor,
                DirectionalLightColor = ResolveSurfaceSunLightColor(horizonFogColor, sunsetWeight, horizonHaze)
            };
        }

        private static float ResolveReadableSurfaceSunIntensity(float intensity)
        {
            return Mathf.Max(IsFinite(intensity) ? intensity : 0f, SurfaceReadableSunIntensityFloor);
        }

        private static float ResolveReadableSurfaceAmbientIntensity(float intensity)
        {
            return Mathf.Max(IsFinite(intensity) ? intensity : 0f, SurfaceReadableAmbientIntensityFloor);
        }

        private static float ResolveReadableSurfaceFogDensity(float density)
        {
            if (!IsFinite(density))
                return SurfaceReadableFogDensityCeiling;

            return Mathf.Min(Mathf.Max(0f, density), SurfaceReadableFogDensityCeiling);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Color ResolveReadableSurfaceAmbientColor(Color source, Color floor)
        {
            source.r = Mathf.Max(source.r, floor.r);
            source.g = Mathf.Max(source.g, floor.g);
            source.b = Mathf.Max(source.b, floor.b);
            source.a = 1f;
            return source;
        }

        private static float ResolveReadableEclipseSkyBlend(float occlusion)
        {
            return Mathf.Min(Mathf.Clamp01(occlusion), SurfaceEclipseSkyNightBlendCeiling);
        }

        private static float ResolveReadableEclipseShaderOcclusion(float occlusion)
        {
            return Mathf.Min(Mathf.Clamp01(occlusion), SurfaceEclipseShaderOcclusionCeiling);
        }

        private static void ApplyReadableSkyColorFloors(ref Color zenith, ref Color horizon, ref Color nadir)
        {
            zenith = ResolveReadableSurfaceAmbientColor(zenith, SurfaceReadableSkyZenithFloor);
            horizon = ResolveReadableSurfaceAmbientColor(horizon, SurfaceReadableSkyHorizonFloor);
            nadir = ResolveReadableSurfaceAmbientColor(nadir, SurfaceReadableSkyNadirFloor);
        }

        private void ApplySurfaceAtmosphericLightingState(AtmosphericLightingState state)
        {
            _surfaceAtmosphericLightingState = state;
            _currentAtmosphericLightingState = state;
            _hasAtmosphericLightingState = state.IsValid != 0;

            if (state.IsValid == 0)
                return;

            if (!RenderSettings.fog || RenderSettings.fogMode != FogMode.ExponentialSquared)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
            }

            float readableFogDensity = ResolveReadableSurfaceFogDensity(state.FogDensity);
            if (Mathf.Abs(RenderSettings.fogDensity - readableFogDensity) >= 0.0001f)
                RenderSettings.fogDensity = readableFogDensity;

            IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
            bool giRelayAmbientAuthority = giRelay != null && giRelay.IsAmbientProbeAuthorityActive;
            if (!giRelayAmbientAuthority)
            {
                if (RenderSettings.ambientMode != AmbientMode.Trilight)
                    RenderSettings.ambientMode = AmbientMode.Trilight;

                Color readableSkyAmbient = ResolveReadableSurfaceAmbientColor(state.AmbientSkyColor, SurfaceReadableSkyAmbientFloor);
                Color readableEquatorAmbient = ResolveReadableSurfaceAmbientColor(state.AmbientEquatorColor, SurfaceReadableEquatorAmbientFloor);
                Color readableGroundAmbient = ResolveReadableSurfaceAmbientColor(state.AmbientGroundColor, SurfaceReadableGroundAmbientFloor);
                if (HasMeaningfulColorShift(readableSkyAmbient, RenderSettings.ambientSkyColor))
                    RenderSettings.ambientSkyColor = readableSkyAmbient;

                if (HasMeaningfulColorShift(readableEquatorAmbient, RenderSettings.ambientEquatorColor))
                    RenderSettings.ambientEquatorColor = readableEquatorAmbient;

                if (HasMeaningfulColorShift(readableGroundAmbient, RenderSettings.ambientGroundColor))
                    RenderSettings.ambientGroundColor = readableGroundAmbient;

                float readableAmbientIntensity = ResolveReadableSurfaceAmbientIntensity(state.AmbientIntensity);
                if (Mathf.Abs(RenderSettings.ambientIntensity - readableAmbientIntensity) >= 0.0001f)
                    RenderSettings.ambientIntensity = readableAmbientIntensity;
            }

            Color globalDirectionalColor = state.DirectionalLightColor * ResolveReadableSurfaceSunIntensity(state.SunIntensityMultiplier);
            globalDirectionalColor.a = 1f;
            Shader.SetGlobalColor(_ID_DirectionalLightColor, globalDirectionalColor);
            PushAtmosphereColorAndAmbientProbe(in state);

            if (sunLight == null)
                return;

            HectonUnderwaterVisuals underwaterVisuals = GlobalRegistry.UnderwaterVisuals;
            bool allowSurfaceDirectionalLight =
                underwaterVisuals == null ||
                !underwaterVisuals.IsUnderwater;

            if (!allowSurfaceDirectionalLight)
                return;

            if (_baseSunColorCaptured && HasMeaningfulColorShift(state.DirectionalLightColor, sunLight.color))
                sunLight.color = state.DirectionalLightColor;

            float readableDirectionalIntensity = ResolveReadableSurfaceSunIntensity(state.DirectionalLightIntensity);
            if (Mathf.Abs(sunLight.intensity - readableDirectionalIntensity) >= 0.0001f)
                sunLight.intensity = readableDirectionalIntensity;
        }

        private void PushAtmosphereColorAndAmbientProbe(in AtmosphericLightingState state)
        {
            float eclipse01 = math.saturate(_smoothedOcclusionFactor);
            Color eclipseTint = new Color(0.025f, 0.045f, 0.070f, 1f);
            Color atmosphereColor = Color.Lerp(state.FogColor, eclipseTint, eclipse01 * 0.45f);
            atmosphereColor.a = 1f;
            IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
            if (giRelay == null || !giRelay.IsAmbientProbeAuthorityActive)
                Shader.SetGlobalColor(_ID_HectonAtmosphereColor, atmosphereColor);

            PushAmbientProbeForEclipse(in state, eclipse01);
        }

        private void PushAmbientProbeForEclipse(in AtmosphericLightingState state, float eclipse01)
        {
            IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
            if (giRelay != null && giRelay.IsAmbientProbeAuthorityActive)
            {
                _ambientProbeEclipseActive = false;
                return;
            }

            if (eclipse01 <= 0.001f && !_ambientProbeEclipseActive)
                return;

            float dimming = 1f - eclipse01 * 0.55f;
            Color ambient = Color.Lerp(state.AmbientSkyColor, state.AmbientGroundColor, 0.32f) *
                            ResolveReadableSurfaceAmbientIntensity(state.AmbientIntensity) *
                            dimming;
            ambient.a = 1f;

            Shader.SetGlobalColor(_ID_HectonAtmosphereColor, ambient);
            _ambientProbeEclipseActive = eclipse01 > 0.001f;
        }

        private float ResolveSurfaceBaseFogDensity()
        {
            if (_surfaceWeatherFogOverrideActive)
                return Mathf.Max(0.0001f, _surfaceWeatherFogDensity);

            if (_atmosphereManager != null)
            {
                float fogDensity = _atmosphereManager.CurrentFogDensity;
                if (fogDensity > 0.0001f)
                    return fogDensity;
            }

            float densityFromHaze = Mathf.Lerp(0.0011f, 0.0036f, Mathf.Clamp01(horizonDensity * HorizonDensityQuarter));
            return densityFromHaze;
        }

        private Color ResolveSurfaceAmbientBaseColor()
        {
            Color skyAmbientAnchor = Color.Lerp(_resolvedSkyZenith, _resolvedSkyHorizon, 0.34f);
            skyAmbientAnchor = Color.Lerp(skyAmbientAnchor, _resolvedSkyNadir, 0.12f);

            Color atmosphereAmbient = _atmosphereManager != null
                ? _atmosphereManager.CurrentAmbientColor
                : skyAmbientAnchor;
            if (!HasUsableSurfaceColor(atmosphereAmbient))
                atmosphereAmbient = skyAmbientAnchor;

            Color ambientBase = Color.Lerp(skyAmbientAnchor, atmosphereAmbient, 0.55f);
            if (_surfaceWeatherFogOverrideActive)
                ambientBase = Color.Lerp(ambientBase, _surfaceWeatherAmbientColor, 0.24f);
            ambientBase.a = 1f;
            return ambientBase;
        }

        private float ResolveSurfaceSunMultiplier()
        {
            float weatherSun = _surfaceWeatherFogOverrideActive
                ? Mathf.Max(0f, _surfaceWeatherSunMultiplier)
                : 1f;
            float stormCloudDensity = ResolveStormCloudDensity01();
            return weatherSun * math.lerp(1f, 0.42f, stormCloudDensity);
        }

        private float ResolveFallbackSurfaceSunIntensity()
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            float dayIntensity = _baseSunIntensityCaptured && _baseSunIntensity > 0.0001f
                ? _baseSunIntensity
                : 1.55f;
            float sunsetIntensity = Mathf.Max(0.32f, dayIntensity * 0.72f);
            float nightIntensity = 0.08f;

            return Mathf.Max(
                0f,
                dayWeight * dayIntensity +
                sunsetWeight * sunsetIntensity +
                nightWeight * nightIntensity);
        }

        private float ResolveSurfaceDirectionalLightIntensity(float sunMultiplier)
        {
            if (_atmosphereManager != null)
            {
                float computedIntensity = Mathf.Max(
                    _atmosphereManager.CurrentSunIntensity,
                    _atmosphereManager.ProfileSunIntensity * _atmosphereManager.ComputedHorizonFade);
                if (computedIntensity <= 0.0001f)
                    computedIntensity = ResolveFallbackSurfaceSunIntensity();
                if (computedIntensity <= 0.0001f)
                    computedIntensity = 1f;
                return Mathf.Max(
                    0f,
                    computedIntensity *
                    sunMultiplier);
            }

            if (sunLight != null)
                return Mathf.Max(0f, sunLight.intensity * sunMultiplier);

            return sunMultiplier;
        }

        private Color ResolveSurfaceSunLightColor(Color horizonFogColor, float sunsetWeight, float horizonHaze)
        {
            Color baseSunColor = _baseSunColorCaptured ? NormalizeColorToMax(_baseSunColor) : Color.white;
            Color daylightColor = Color.Lerp(Color.white, baseSunColor, 0.22f);
            Color sunsetTint = ResolveScriptSunsetHorizonColor();
            Color skyLightTint = Color.Lerp(_resolvedSkyHorizon, horizonFogColor, 0.3f);
            Color sunColor = Color.Lerp(
                daylightColor,
                sunsetTint,
                Mathf.Clamp01(sunsetWeight * 0.85f));
            sunColor = Color.Lerp(
                sunColor,
                skyLightTint,
                Mathf.Clamp01(horizonHaze * 0.08f));
            sunColor.a = 1f;
            return sunColor;
        }

        private void InitializeMaterialPropertyBlocks()
        {
            _aegirMPB ??= new MaterialPropertyBlock();   // COLD ALLOC: MaterialPropertyBlock[1] — gas giant shader state bridge — owner: HectonCelestialEngine
            _moonMPB ??= new MaterialPropertyBlock();    // COLD ALLOC: MaterialPropertyBlock[1] — moon shader state bridge — owner: HectonCelestialEngine
            _sunDiscMPB ??= new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] — sun-disc shader state bridge — owner: HectonCelestialEngine
        }

        private void CacheMoonRenderers()
        {
            _observerBodyCache.Clear();
            _moonRenderers.Clear();

            Transform searchRoot = aegirTransform != null
                ? aegirTransform.root
                : transform.root;
            if (searchRoot == null)
                return;

            searchRoot.GetComponentsInChildren(true, _observerBodyCache);
            for (int i = 0; i < _observerBodyCache.Count; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (body == null || body == aegirObserverRelativeBody)
                    continue;

                if (!body.TryGetComponent(out Renderer renderer) || renderer == null || renderer == aegirRenderer)
                    continue;

                Material sharedMaterial = renderer.sharedMaterial;
                if (sharedMaterial == null || sharedMaterial.shader == null)
                    continue;

                if (!sharedMaterial.HasProperty(_ID_AtmosphereTransmittanceWeight) ||
                    !sharedMaterial.HasProperty(_ID_AtmosphereInscatterWeight))
                {
                    continue;
                }

                if (!string.Equals(sharedMaterial.shader.name, "HECTON/Celestial/Hecton_CelestialMoon", StringComparison.Ordinal))
                    continue;

                _moonRenderers.Add(renderer);
            }
        }

        private void CacheCelestialTextureDefaults()
        {
            _aegirSharedMaterial = aegirRenderer != null ? aegirRenderer.sharedMaterial : null;

            _skyHighCloudTexDefault = GetMaterialTexture(_skyMaterial, _ID_HighCloudTex);
            _skyMainCloudAtlasDefault = GetMaterialTexture(_skyMaterial, _ID_MainCloudAtlas);
            _skyMainCloudTexDefault = GetMaterialTexture(_skyMaterial, _ID_MainCloudTex);

            _daySkyboxMainTexDefault = GetMaterialTexture(daySkybox, _ID_MainTex);
            _daySkyboxEmissionTexDefault = GetMaterialTexture(daySkybox, _ID_EmissionMap);
            _nightSkyboxMainTexDefault = GetMaterialTexture(nightSkybox, _ID_MainTex);
            _nightSkyboxEmissionTexDefault = GetMaterialTexture(nightSkybox, _ID_EmissionMap);

            _blendedDayCubemapDefault = GetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap);
            _blendedNightCubemapDefault = GetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap);

            _aegirMainTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_MainTex);
            _aegirDetailTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex);
            _aegirEmissionMapDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap);
            _aegirCelestialOcclusionTexDefault = GetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex);

            CacheSkyWeatherDefaults();
        }

        private void CacheSkyWeatherDefaults()
        {
            if (_skyMaterial == null || _cachedSkyWeatherDefaults)
                return;

            _defaultCloudDensityThreshold = GetMaterialFloat(_skyMaterial, _ID_CloudDensityThreshold, 0.2f);
            _defaultCloudSoftness = GetMaterialFloat(_skyMaterial, _ID_CloudSoftness, 0.28f);
            _defaultCloudSpeedMultiplier = GetMaterialFloat(_skyMaterial, _ID_CloudSpeedMult, 0.3f);
            _defaultWindDirection = GetMaterialVector(_skyMaterial, _ID_WindDirection, new Vector4(1f, 0.2f, 0f, 0f));
            _defaultCloudLitColor = GetMaterialColor(_skyMaterial, _ID_CloudColorLit, Color.white);
            _defaultCloudShadowColor = GetMaterialColor(_skyMaterial, _ID_CloudColorShadow, Color.white);
            _defaultSunsetCloudColor = GetMaterialColor(_skyMaterial, _ID_SunsetCloudColor, Color.white);
            _defaultNightCloudColor = GetMaterialColor(_skyMaterial, _ID_NightCloudColor, Color.white);
            _defaultSunDiscColor = GetMaterialColor(_skyMaterial, _ID_SunDiscColor, Color.white);
            _defaultSunScatterColor = GetMaterialColor(_skyMaterial, _ID_SunScatterColor, Color.white);
            _cachedSkyWeatherDefaults = true;
        }

        private void InitializePlanetShineLight()
        {
            const string lightName = "AegirSecondaryLight_PlanetShine";

            var existing = transform.Find(lightName);
            if (existing != null)
            {
                _planetShineLightGO = existing.gameObject;
                _planetShineLight = _planetShineLightGO.GetComponent<Light>();
            }
            else
            {
                _planetShineLightGO = new GameObject(lightName);
                _planetShineLightGO.transform.SetParent(transform, false);
                _planetShineLightGO.hideFlags = HideFlags.DontSave;
                _planetShineLight = _planetShineLightGO.AddComponent<Light>();
            }

            _planetShineLight.type = LightType.Directional;
            _planetShineLight.color = planetShineColor;
            _planetShineLight.intensity = 0f;
            _planetShineLight.shadows = LightShadows.None;
            _planetShineLight.renderMode = LightRenderMode.Auto;
            _planetShineLight.cullingMask = HectonLayerMasks.AllDefinedProjectLayersMask & ~HectonLayerMasks.CelestialLayerMask;
        }

        private void CleanupPlanetShineLight()
        {
            if (_planetShineLightGO != null)
            {
                if (Application.isPlaying)
                    Destroy(_planetShineLightGO);
                else
                    DestroyImmediate(_planetShineLightGO);
            }
        }

        private void CaptureBaseFlareValues()
        {
            if (_baseFlareValuesCaptured) return;
            if (_sunLensFlare == null) return;

            _baseFlareIntensity = _sunLensFlare.intensity;
            _baseFlareScale = _sunLensFlare.scale;
            _baseFlareValuesCaptured = true;
            DisableLegacySunFlare();
        }

        private void CalculateEclipseAngularRadius()
        {
            if (eclipseAngularRadiusOverride > 0f)
            {
                _eclipseAngularRadius = eclipseAngularRadiusOverride;
                return;
            }

            _eclipseAngularRadius = GetAegirAngularRadiusDegrees();
        }

        private float ComputeAegirWorldRadius()
        {
            if (aegirRenderer != null)
            {
                float3 extents = (float3)aegirRenderer.bounds.extents;
                return math.cmax(extents);
            }
            if (aegirTransform != null)
            {
                float3 scale = (float3)aegirTransform.lossyScale;
                return math.cmax(scale) * 0.5f;
            }
            return 1f;
        }

        private float GetAegirWorldRadius() => _cachedAegirRadius;

        private bool TryResolveAegirSkyDirection(out float3 direction)
        {
            if (aegirObserverRelativeBody != null)
            {
                float3 currentDirection = (float3)aegirObserverRelativeBody.CurrentDirection;
                float currentDirectionSq = math.lengthsq(currentDirection);
                if (currentDirectionSq > 0.0001f)
                {
                    direction = currentDirection * math.rsqrt(currentDirectionSq);
                    return true;
                }
            }

            if (aegirTransform != null)
            {
                float3 localDirection = (float3)aegirTransform.localPosition;
                float localDirectionSq = math.lengthsq(localDirection);
                if (localDirectionSq > 0.0001f)
                {
                    direction = localDirection * math.rsqrt(localDirectionSq);
                    return true;
                }
            }

            if (aegirTransform != null && playerTransform != null)
            {
                direction = ResolveAupDirectionBetweenTransforms(playerTransform, aegirTransform);
                if (math.lengthsq(direction) > 0.0001f)
                    return true;
            }

            direction = float3.zero;
            return false;
        }

        private float GetAegirAngularRadiusDegrees()
        {
            if (eclipseAngularRadiusOverride > 0f)
                return math.max(eclipseAngularRadiusOverride, 0.01f);

            if (useCinematicEclipseOccluderRadius)
                return math.max(cinematicEclipseOccluderRadiusDegrees, 0.01f);

            if (aegirObserverRelativeBody != null)
                return math.max(aegirObserverRelativeBody.AngularDiameterDegrees * 0.5f, 0.01f);

            if (aegirTransform != null && playerTransform != null)
            {
                float radius = GetAegirWorldRadius();
                float distance = math.max(
                    ResolveAupDistanceMeters(playerTransform, aegirTransform),
                    0.01f);
                return math.degrees(math.atan2(radius, distance));
            }

            return math.max(_eclipseAngularRadius, 0.01f);
        }

        // ─────────────────────────────────────────────
        // SUN DIRECTION RESOLUTION
        // ─────────────────────────────────────────────

        private void ResolveSunDirection()
        {
            if (_sunDirectionResolvedFromMatrix)
                return;

            if (sunLight != null)
                _resolvedSunDirection = -(float3)sunLight.transform.forward;
        }

        // ─────────────────────────────────────────────
        // SUN ORBITAL LOGIC
        // ─────────────────────────────────────────────

        private void UpdateSunPosition(float dt)
        {
            if (sunLight == null)
            {
                _sunDirectionResolvedFromMatrix = false;
                return;
            }

            if (IsLowTierLockedSunMode())
            {
                ApplyLowTierLockedSunDirection();
                return;
            }

            _lowTierSunLockApplied = false;

            if (_atmosphereManager != null)
            {
                _accumulatedOrbitalAngle = _atmosphereManager.SunAngle;
                _sunDirectionResolvedFromMatrix = false;
                return;
            }

            UpdateInternalOrbit(dt);

            float3 axis = ResolveDominantAxisDirection((float3)sunOrbitAxis, new float3(1f, 0f, 0f));
            _sunOrbitRotationMatrix = BuildAxisAngleRotationMatrix(axis, math.radians(_accumulatedOrbitalAngle));
            float3 resolvedSunForward = math.mul(_sunOrbitRotationMatrix, new float4(0f, 0f, 1f, 0f)).xyz;
            if (math.lengthsq(resolvedSunForward) <= 0.0001f)
                _resolvedSunForward = Vector3.forward;
            else
                _resolvedSunForward = new Vector3(resolvedSunForward.x, resolvedSunForward.y, resolvedSunForward.z);

            sunLight.transform.forward = _resolvedSunForward;
            _resolvedSunDirection = new float3(-_resolvedSunForward.x, -_resolvedSunForward.y, -_resolvedSunForward.z);
            _sunDirectionResolvedFromMatrix = true;
        }

        private bool IsLowTierLockedSunMode()
        {
            if (!Application.isPlaying)
                return false;

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier == HectonQualityTier.Low ||
                   tier == HectonQualityTier.Mx350 ||
                   tier == HectonQualityTier.Unknown;
        }

        private void ApplyLowTierLockedSunDirection()
        {
            Quaternion lockedRotation = Quaternion.Euler(LowTierLockedSunPitchDegrees, 0f, 0f);
            if (!_lowTierSunLockApplied || Quaternion.Angle(sunLight.transform.rotation, lockedRotation) > 0.01f)
                sunLight.transform.rotation = lockedRotation;

            _lowTierSunLockApplied = true;
            _resolvedSunForward = sunLight.transform.forward;
            _resolvedSunDirection = new float3(-_resolvedSunForward.x, -_resolvedSunForward.y, -_resolvedSunForward.z);
            _sunDirectionResolvedFromMatrix = true;
        }

        private void UpdateInternalOrbit(float dt)
        {
            float degreesPerSecond = 360f * _sunOrbitPeriodReciprocal;
            _accumulatedOrbitalAngle += degreesPerSecond * dt;
            _accumulatedOrbitalAngle -= FastFloorToInt(_accumulatedOrbitalAngle * OrbitDegreesToTurns) * 360f;
        }

        private static float4x4 BuildAxisAngleRotationMatrix(float3 axis, float radians)
        {
            axis = ResolveDominantAxisDirection(axis, new float3(1f, 0f, 0f));
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;
            float sin = FastSinRadians(radians);
            float cos = FastCosRadians(radians);
            float oneMinusCos = 1f - cos;

            float m00 = oneMinusCos * x * x + cos;
            float m01 = oneMinusCos * x * y - sin * z;
            float m02 = oneMinusCos * x * z + sin * y;
            float m10 = oneMinusCos * y * x + sin * z;
            float m11 = oneMinusCos * y * y + cos;
            float m12 = oneMinusCos * y * z - sin * x;
            float m20 = oneMinusCos * z * x - sin * y;
            float m21 = oneMinusCos * z * y + sin * x;
            float m22 = oneMinusCos * z * z + cos;

            return new float4x4(
                new float4(m00, m10, m20, 0f),
                new float4(m01, m11, m21, 0f),
                new float4(m02, m12, m22, 0f),
                new float4(0f, 0f, 0f, 1f));
        }

        private void CacheCelestialOrbitReciprocals()
        {
            _sunOrbitPeriodReciprocal = ResolvePeriodReciprocal(orbitalPeriod);
            _gasGiantOrbitPeriodReciprocal = ResolveOrbitPeriodReciprocal(in gasGiantOrbit);
            _moon0OrbitPeriodReciprocal = ResolveOrbitPeriodReciprocal(in moon0Orbit);
            _moon1OrbitPeriodReciprocal = ResolveOrbitPeriodReciprocal(in moon1Orbit);
            _inGameYearSecondsReciprocal = ResolveYearSecondsReciprocal(orbitalPeriod, inGameYearDays);
        }

        private bool ShouldEvaluateCelestialSnapshotThisFrame()
        {
            if (!Application.isPlaying)
                return true;

            return Time.frameCount >= _nextCelestialSnapshotFrame;
        }

        private void ScheduleNextCelestialSnapshotFrame()
        {
            if (!Application.isPlaying)
            {
                _nextCelestialSnapshotFrame = 0;
                return;
            }

            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            bool highTier = tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
            int interval = highTier ? CelestialSnapshotFrameIntervalHigh : CelestialSnapshotFrameIntervalLow;
            _nextCelestialSnapshotFrame = Time.frameCount + interval;
        }

        private static float ResolveOrbitPeriodReciprocal(in CinematicOrbitDefinition orbit)
        {
            return ResolvePeriodReciprocal(orbit.orbitalPeriodSeconds);
        }

        private static float ResolveYearSecondsReciprocal(float dayPeriodSeconds, float yearDays)
        {
            float safeDaySeconds = math.max(1f, math.isfinite(dayPeriodSeconds) ? dayPeriodSeconds : 1f);
            float safeYearDays = math.max(1f, math.isfinite(yearDays) ? yearDays : 1f);
            return math.rcp(safeDaySeconds * safeYearDays);
        }

        private static float ResolvePeriodReciprocal(float periodSeconds)
        {
            return math.rcp(math.max(1f, math.isfinite(periodSeconds) ? periodSeconds : 1f));
        }

        private void UpdateAnalyticalCelestialState()
        {
            if (!Application.isPlaying)
            {
                _nextCelestialSnapshotFrame = 0;
                BuildFallbackCelestialRuntimeSnapshot();
                return;
            }

            if (!enableAnalyticalOrbitSolver)
            {
                _nextCelestialSnapshotFrame = 0;
                BuildFallbackCelestialRuntimeSnapshot();
                return;
            }

            TryFinalizeCompletedOrbitMathJob();

            if ((_celestialRuntimeSnapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u &&
                !ShouldEvaluateCelestialSnapshotThisFrame())
            {
                return;
            }

            if (_orbitJobScheduled)
                return;

            if (IsLowTierLockedSunMode())
            {
                _nextCelestialSnapshotFrame = 0;
                BuildFallbackCelestialRuntimeSnapshot();
                return;
            }

            ScheduleNextCelestialSnapshotFrame();

            double universeTime = ResolveSynchronizedUniverseTimeSeconds();
            uint seed = ResolveDeterministicStarSeed();
            CelestialOrbitMathJob job = BuildCelestialOrbitMathJob(universeTime, seed);
            if (!Application.isPlaying || !_orbitJobPrimed)
            {
                job.Execute(); // COLD SYNC JOB: primes deterministic state before the first deferred SlowTick schedule.
                CommitOrbitMathOutput(_orbitJobOutput[0]);
                return;
            }

            _orbitJobHandle = job.Schedule();
            _orbitJobScheduled = true;
        }

        private CelestialOrbitMathJob BuildCelestialOrbitMathJob(double universeTime, uint seed)
        {
            EnsureCelestialRuntimeBuffers();
            return new CelestialOrbitMathJob
            {
                AbsoluteUniverseTime = universeTime,
                DeterministicSeed = seed,
                SunDirection = _resolvedSunDirection,
                GasGiantDefinition = gasGiantOrbit,
                GasGiantPeriodReciprocal = _gasGiantOrbitPeriodReciprocal,
                Moon0Definition = moon0Orbit,
                Moon0PeriodReciprocal = _moon0OrbitPeriodReciprocal,
                Moon1Definition = moon1Orbit,
                Moon1PeriodReciprocal = _moon1OrbitPeriodReciprocal,
                DayPeriodSeconds = orbitalPeriod,
                InverseYearSeconds = _inGameYearSecondsReciprocal,
                TideAmplitudeMeters = celestialTideAmplitudeMeters,
                HighTideThreshold = highTideThreshold01,
                FullMoonBloomThreshold = fullMoonBloomThreshold01,
                EclipseOcclusion01 = _smoothedOcclusionFactor,
                EclipseActive = (byte)(_isEclipseActive ? 1 : 0),
                RadiationStorm01 = ResolveRadiationStorm01(),
                ResonanceBiolumMultiplier = _lunarResonanceMultiplier,
                Sequence = _celestialRuntimeSequence + 1u,
                Output = _orbitJobOutput
            };
        }

        private void TryFinalizeCompletedOrbitMathJob()
        {
            if (!_orbitJobScheduled)
                return;

            if (!DispatcherJobSwap.TryFinalizeCompleted(ref _orbitJobHandle))
                return;

            _orbitJobScheduled = false;
            CommitOrbitMathOutput(_orbitJobOutput[0]);
        }

        private void TryCompleteOrbitMathJob(bool forceComplete)
        {
            if (!_orbitJobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _orbitJobHandle, forceComplete))
                return;

            _orbitJobScheduled = false;
            CommitOrbitMathOutput(_orbitJobOutput[0]);
        }

        private void CommitOrbitMathOutput(in CelestialOrbitJobOutput output)
        {
            if (output.Valid == 0)
                return;

            CelestialRuntimeSnapshot snapshot = output.Snapshot;
            if (!IsCelestialSnapshotFinite(in snapshot))
            {
                DumpCelestialBlackBox();
                BuildFallbackCelestialRuntimeSnapshot();
                return;
            }

            _celestialRuntimeSnapshot = snapshot;
            _celestialRuntimeSequence = snapshot.Sequence;
            _orbitJobPrimed = true;

            if (driveObserverBodiesFromAnalyticalOrbits)
                ApplyAnalyticalObserverDirections(in _celestialRuntimeSnapshot);
        }

        private static bool IsCelestialSnapshotFinite(in CelestialRuntimeSnapshot snapshot)
        {
            return !double.IsNaN(snapshot.AbsoluteUniverseTime) &&
                   !double.IsInfinity(snapshot.AbsoluteUniverseTime) &&
                   math.all(math.isfinite(snapshot.SunDirection)) &&
                   math.all(math.isfinite(snapshot.GasGiantOffset)) &&
                   math.all(math.isfinite(snapshot.Moon0Offset)) &&
                   math.all(math.isfinite(snapshot.Moon1Offset)) &&
                   math.all(math.isfinite(snapshot.GasGiantDirection)) &&
                   math.all(math.isfinite(snapshot.Moon0Direction)) &&
                   math.all(math.isfinite(snapshot.Moon1Direction)) &&
                   math.all(math.isfinite(snapshot.TidePullVector)) &&
                   math.isfinite(snapshot.TideHeightMeters) &&
                   math.isfinite(snapshot.TideHigh01) &&
                   math.isfinite(snapshot.EclipseOcclusion01) &&
                   math.isfinite(snapshot.GlobalBiolumMultiplier);
        }

        private void BuildFallbackCelestialRuntimeSnapshot()
        {
            CelestialRuntimeSnapshot snapshot = default;
            snapshot.AbsoluteUniverseTime = ResolveSynchronizedUniverseTimeSeconds();
            snapshot.SunDirection = NormalizeVisualRsqrt(_resolvedSunDirection, new float3(0f, 1f, 0f));
            snapshot.GasGiantDirection = TryResolveAegirSkyDirection(out float3 aegirDirection)
                ? aegirDirection
                : float3.zero;
            snapshot.GasGiantOffset = snapshot.GasGiantDirection * math.max(1f, gasGiantOrbit.registryOffsetMeters);
            snapshot.EclipseOcclusion01 = math.saturate(_smoothedOcclusionFactor);
            snapshot.RadiationStorm01 = ResolveRadiationStorm01();
            snapshot.Flags = PackCelestialRuntimeFlags(
                _isEclipseActive,
                snapshot.EclipseOcclusion01,
                0f,
                1f,
                0f,
                1f,
                snapshot.RadiationStorm01);
            float globalBiolumMultiplier = math.max(1f, _lunarResonanceMultiplier);
            if ((snapshot.Flags & (uint)CelestialRuntimeFlags.EclipseActive) != 0u)
                globalBiolumMultiplier = math.max(globalBiolumMultiplier, math.lerp(1f, EclipseBiolumMultiplier, snapshot.EclipseOcclusion01));

            snapshot.GlobalBiolumMultiplier = globalBiolumMultiplier;
            snapshot.Sequence = _celestialRuntimeSequence + 1u;

            _celestialRuntimeSnapshot = snapshot;
            _celestialRuntimeSequence = snapshot.Sequence;
        }

        private void ApplyAnalyticalObserverDirections(in CelestialRuntimeSnapshot snapshot)
        {
            if (aegirObserverRelativeBody != null)
                aegirObserverRelativeBody.SetFixedDirection(ToVector3(snapshot.GasGiantDirection));

            int moonWriteIndex = 0;
            for (int i = 0; i < _observerBodyCache.Count && moonWriteIndex < 2; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (body == null || body == aegirObserverRelativeBody)
                    continue;

                float3 direction = moonWriteIndex == 0 ? snapshot.Moon0Direction : snapshot.Moon1Direction;
                body.SetFixedDirection(ToVector3(direction));
                moonWriteIndex++;
            }
        }

        private void PublishCelestialRuntimeSnapshot()
        {
            CelestialRuntimeSnapshot snapshot = _celestialRuntimeSnapshot;
            if ((snapshot.Flags & (uint)CelestialRuntimeFlags.Valid) == 0u)
                return;

            snapshot.EclipseOcclusion01 = math.saturate(_smoothedOcclusionFactor);

            float radiationStorm = ResolveRadiationStorm01();
            snapshot.RadiationStorm01 = radiationStorm;
            snapshot.Flags = PackCelestialRuntimeFlags(
                _isEclipseActive,
                snapshot.EclipseOcclusion01,
                snapshot.TideHigh01,
                highTideThreshold01,
                math.max(snapshot.Moon0Phase01, snapshot.Moon1Phase01),
                fullMoonBloomThreshold01,
                radiationStorm);

            _celestialRuntimeSnapshot = snapshot;
            if (snapshot.Sequence == _lastPublishedCelestialSequence &&
                snapshot.Flags == _lastPublishedCelestialFlags &&
                snapshot.EclipseOcclusion01 == _lastPublishedCelestialEclipseOcclusion &&
                snapshot.RadiationStorm01 == _lastPublishedCelestialRadiationStorm)
            {
                return;
            }

            _lastPublishedCelestialSequence = snapshot.Sequence;
            _lastPublishedCelestialFlags = snapshot.Flags;
            _lastPublishedCelestialEclipseOcclusion = snapshot.EclipseOcclusion01;
            _lastPublishedCelestialRadiationStorm = snapshot.RadiationStorm01;
            if (Application.isPlaying)
            {
                GlobalRegistry.PublishCelestialRuntimeSnapshot(in snapshot);
                PublishGlobalTimeSyncSignal(in snapshot);
            }

            Shader.SetGlobalVector(
                _ID_HectonCelestialTidePull,
                new Vector4(snapshot.TidePullVector.x, snapshot.TidePullVector.y, snapshot.TidePullVector.z, snapshot.TideHigh01));
            Shader.SetGlobalFloat(_ID_HectonCelestialTideHeight, snapshot.TideHeightMeters);
            Shader.SetGlobalVector(
                _ID_HectonCelestialGasGiantOffset,
                new Vector4(snapshot.GasGiantOffset.x, snapshot.GasGiantOffset.y, snapshot.GasGiantOffset.z, 0f));
            Shader.SetGlobalVector(
                _ID_HectonCelestialMoon0Offset,
                new Vector4(snapshot.Moon0Offset.x, snapshot.Moon0Offset.y, snapshot.Moon0Offset.z, snapshot.Moon0Phase01));
            Shader.SetGlobalVector(
                _ID_HectonCelestialMoon1Offset,
                new Vector4(snapshot.Moon1Offset.x, snapshot.Moon1Offset.y, snapshot.Moon1Offset.z, snapshot.Moon1Phase01));
            Shader.SetGlobalVector(
                _ID_HectonCelestialPhaseParams,
                new Vector4(snapshot.Moon0Phase01, snapshot.Moon1Phase01, snapshot.GasGiantPhase01, snapshot.TideHigh01));
            Shader.SetGlobalInt(_ID_HectonCelestialRuntimeFlags, unchecked((int)snapshot.Flags));
            Shader.SetGlobalFloat(_ID_HectonCelestialRadiationStorm, snapshot.RadiationStorm01);
            Shader.SetGlobalFloat(_ID_HectonCelestialBiolumMultiplier, snapshot.GlobalBiolumMultiplier);
        }

        private void PublishGlobalTimeSyncSignal(in CelestialRuntimeSnapshot snapshot)
        {
            GlobalTimeSyncSignal signal = new GlobalTimeSyncSignal
            {
                WorldSeconds = snapshot.AbsoluteUniverseTime,
                TimeScale = math.max(0f, _debugCelestialTimeScale),
                MoonPhase01 = math.saturate(math.max(snapshot.Moon0Phase01, snapshot.Moon1Phase01)),
                Sequence = snapshot.Sequence,
                Flags = (byte)((snapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u ? 1 : 0)
            };
            GlobalSignals.Publish(in signal);
        }

        private void ClearCelestialRuntimeSnapshot()
        {
            _celestialRuntimeSnapshot = default;
            _celestialRuntimeSequence = 0u;
            _nextCelestialSnapshotFrame = 0;
            _lastPublishedCelestialSequence = uint.MaxValue;
            _lastPublishedCelestialFlags = uint.MaxValue;
            _lastPublishedCelestialEclipseOcclusion = -1f;
            _lastPublishedCelestialRadiationStorm = -1f;
            CelestialRuntimeSnapshot emptySnapshot = default;
            GlobalRegistry.PublishCelestialRuntimeSnapshot(in emptySnapshot);
            Shader.SetGlobalVector(_ID_HectonCelestialTidePull, Vector4.zero);
            Shader.SetGlobalFloat(_ID_HectonCelestialTideHeight, 0f);
            Shader.SetGlobalVector(_ID_HectonCelestialGasGiantOffset, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonCelestialMoon0Offset, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonCelestialMoon1Offset, Vector4.zero);
            Shader.SetGlobalVector(_ID_HectonCelestialPhaseParams, Vector4.zero);
            Shader.SetGlobalInt(_ID_HectonCelestialRuntimeFlags, 0);
            Shader.SetGlobalFloat(_ID_HectonCelestialRadiationStorm, 0f);
            Shader.SetGlobalFloat(_ID_HectonCelestialBiolumMultiplier, 1f);
            Shader.SetGlobalColor(_ID_HectonAtmosphereColor, Color.black);
            _stormCloudDensity01 = 0f;
            UploadStormCloudDensityShaderGlobal(0f, forceUpload: true);
            _lightningFlash01 = 0f;
            UploadLightningFlashShaderGlobal(0f, forceUpload: false);
        }

        private double ResolveSynchronizedUniverseTimeSeconds()
        {
            double universeTime = Time.timeAsDouble;
            if (double.IsNaN(universeTime) || double.IsInfinity(universeTime) || universeTime < 0d)
                universeTime = 0d;

            double timeScale = _debugCelestialTimeScale < 1f ? 1d : _debugCelestialTimeScale;
            return universeTime * timeScale;
        }

        private uint ResolveDeterministicStarSeed()
        {
            float seedValue = ResolveStarMapSeed();
            if (!float.IsFinite(seedValue))
                seedValue = 1f;

            int rounded = Mathf.RoundToInt(seedValue);
            return rounded == 0 ? 1u : unchecked((uint)rounded);
        }

        private float ResolveRadiationStorm01()
        {
            RandomEventSystem randomEvents = GlobalRegistry.RandomEvents;
            return randomEvents != null && randomEvents.IsEventActive(RandomEventType.SolarFlare)
                ? 1f
                : 0f;
        }

        private bool ShouldUploadGlobalSunDirectionThisMinute()
        {
            if (!Application.isPlaying)
                return true;

            double universeTime = ResolveSynchronizedUniverseTimeSeconds();
            int minuteIndex = (int)math.floor(universeTime * InvCelestialGlobalSunUploadPeriodSeconds);
            if (minuteIndex == _lastSunDirectionGlobalUploadMinute)
                return false;

            _lastSunDirectionGlobalUploadMinute = minuteIndex;
            return true;
        }

        public static CelestialRuntimeSnapshot EvaluateAnalyticalOrbitSnapshotForSmoke(double absoluteUniverseTime, uint deterministicSeed)
        {
            CinematicOrbitDefinition gasGiant = CinematicOrbitDefinition.GasGiantDefault();
            CinematicOrbitDefinition moon0 = CinematicOrbitDefinition.Moon0Default();
            CinematicOrbitDefinition moon1 = CinematicOrbitDefinition.Moon1Default();
            float dayPeriodSeconds = 3600f;
            float inverseYearSeconds = ResolveYearSecondsReciprocal(dayPeriodSeconds, 365f);
            return EvaluateAnalyticalOrbitSnapshot(
                absoluteUniverseTime,
                deterministicSeed,
                new float3(0f, 1f, 0f),
                gasGiant,
                ResolveOrbitPeriodReciprocal(in gasGiant),
                moon0,
                ResolveOrbitPeriodReciprocal(in moon0),
                moon1,
                ResolveOrbitPeriodReciprocal(in moon1),
                dayPeriodSeconds,
                inverseYearSeconds,
                2.25f,
                0.78f,
                0.92f,
                0f,
                false,
                0f,
                1f,
                1u);
        }

        private static CelestialRuntimeSnapshot EvaluateAnalyticalOrbitSnapshot(
            double absoluteUniverseTime,
            uint deterministicSeed,
            float3 sunDirection,
            in CinematicOrbitDefinition gasGiantDefinition,
            float gasGiantPeriodReciprocal,
            in CinematicOrbitDefinition moon0Definition,
            float moon0PeriodReciprocal,
            in CinematicOrbitDefinition moon1Definition,
            float moon1PeriodReciprocal,
            float dayPeriodSeconds,
            float inverseYearSeconds,
            float tideAmplitudeMeters,
            float highTideThreshold,
            float fullMoonBloomThreshold,
            float eclipseOcclusion01,
            bool eclipseActive,
            float radiationStorm01,
            float resonanceBiolumMultiplier,
            uint sequence)
        {
            float3 safeSunDirection = NormalizeVisualRsqrt(sunDirection, new float3(0f, 1f, 0f));
            float seedPhase01 = ResolveSeedPhase01(deterministicSeed);

            CinematicOrbitState gasGiant = EvaluateCinematicOrbit(
                in gasGiantDefinition,
                absoluteUniverseTime,
                seedPhase01 + 0.17320508f,
                gasGiantPeriodReciprocal,
                inverseYearSeconds,
                safeSunDirection);
            CinematicOrbitState moon0 = EvaluateCinematicOrbit(
                in moon0Definition,
                absoluteUniverseTime,
                seedPhase01 + 0.41421356f,
                moon0PeriodReciprocal,
                inverseYearSeconds,
                safeSunDirection);
            CinematicOrbitState moon1 = EvaluateCinematicOrbit(
                in moon1Definition,
                absoluteUniverseTime,
                seedPhase01 + 0.73205084f,
                moon1PeriodReciprocal,
                inverseYearSeconds,
                safeSunDirection);

            float3 weightedPull =
                moon0.Direction * math.max(0f, moon0Definition.gravityWeight) +
                moon1.Direction * math.max(0f, moon1Definition.gravityWeight) +
                gasGiant.Direction * math.max(0f, gasGiantDefinition.gravityWeight);
            float3 tidePull = NormalizeVisualRsqrt(weightedPull, new float3(0f, 1f, 0f));
            float moonAlignment01 = math.saturate(0.5f + 0.5f * math.dot(moon0.Direction, moon1.Direction));
            float verticalPull01 = math.saturate(0.5f + 0.5f * tidePull.y);
            float tideHigh01 = math.saturate((verticalPull01 * 0.65f) + (moonAlignment01 * 0.35f));
            float tideHeight = ((tideHigh01 * 2f) - 1f) * math.max(0f, tideAmplitudeMeters);
            float bloom01 = math.max(moon0.Fullness01, moon1.Fullness01);

            uint flags = PackCelestialRuntimeFlags(
                eclipseActive,
                eclipseOcclusion01,
                tideHigh01,
                highTideThreshold,
                bloom01,
                fullMoonBloomThreshold,
                radiationStorm01);

            CelestialRuntimeSnapshot snapshot = default;
            snapshot.AbsoluteUniverseTime = absoluteUniverseTime;
            snapshot.SunDirection = safeSunDirection;
            snapshot.GasGiantOffset = gasGiant.RegistryOffset;
            snapshot.Moon0Offset = moon0.RegistryOffset;
            snapshot.Moon1Offset = moon1.RegistryOffset;
            snapshot.GasGiantDirection = gasGiant.Direction;
            snapshot.Moon0Direction = moon0.Direction;
            snapshot.Moon1Direction = moon1.Direction;
            snapshot.TidePullVector = tidePull;
            snapshot.TideHeightMeters = tideHeight;
            snapshot.TideHigh01 = tideHigh01;
            snapshot.Moon0Phase01 = moon0.Fullness01;
            snapshot.Moon1Phase01 = moon1.Fullness01;
            snapshot.GasGiantPhase01 = gasGiant.Fullness01;
            snapshot.EclipseOcclusion01 = math.saturate(eclipseOcclusion01);
            snapshot.RadiationStorm01 = math.saturate(radiationStorm01);
            float globalBiolumMultiplier = ((flags & (uint)CelestialRuntimeFlags.FullMoonBloom) != 0u)
                ? math.max(2f, resonanceBiolumMultiplier)
                : math.max(1f, resonanceBiolumMultiplier);
            if ((flags & (uint)CelestialRuntimeFlags.EclipseActive) != 0u)
                globalBiolumMultiplier = math.max(globalBiolumMultiplier, math.lerp(1f, EclipseBiolumMultiplier, math.saturate(eclipseOcclusion01)));

            snapshot.GlobalBiolumMultiplier = globalBiolumMultiplier;
            snapshot.Flags = flags;
            snapshot.Sequence = sequence;
            return snapshot;
        }

        private static uint PackCelestialRuntimeFlags(
            bool eclipseActive,
            float eclipseOcclusion01,
            float tideHigh01,
            float highTideThreshold,
            float fullMoonBloom01,
            float fullMoonBloomThreshold,
            float radiationStorm01)
        {
            uint flags = (uint)CelestialRuntimeFlags.Valid;
            flags |= math.select(0u, (uint)CelestialRuntimeFlags.EclipseActive, eclipseActive || eclipseOcclusion01 > 0.01f);
            flags |= math.select(0u, (uint)CelestialRuntimeFlags.HighTide, tideHigh01 >= math.saturate(highTideThreshold));
            flags |= math.select(0u, (uint)CelestialRuntimeFlags.FullMoonBloom, fullMoonBloom01 >= math.saturate(fullMoonBloomThreshold));
            flags |= math.select(0u, (uint)CelestialRuntimeFlags.SolarRadiationStorm, radiationStorm01 > 0f);
            return flags;
        }

        private static CinematicOrbitState EvaluateCinematicOrbit(
            in CinematicOrbitDefinition orbit,
            double absoluteUniverseTime,
            float seedPhase01,
            float orbitPeriodReciprocal,
            float inverseYearSeconds,
            float3 sunDirection)
        {
            float elapsedSeconds = (float)(absoluteUniverseTime - orbit.epochUniverseTimeSeconds);
            float driftTurns = elapsedSeconds * inverseYearSeconds * orbit.orbitalDriftDegreesPerYear * OrbitDegreesToTurns;
            float phase01 = Wrap01(
                (elapsedSeconds * orbitPeriodReciprocal) +
                (orbit.epochMeanAnomalyDegrees * OrbitDegreesToTurns) +
                seedPhase01 +
                driftTurns);
            float nodeTurns = orbit.longitudeAscendingNodeDegrees * OrbitDegreesToTurns;
            float periapsisTurns = orbit.argumentOfPeriapsisDegrees * OrbitDegreesToTurns;
            float eccentricity01 = math.saturate(orbit.eccentricity);
            float inclination01 = math.saturate(math.abs(orbit.inclinationDegrees) * Inv90);
            float inclinationSign = orbit.inclinationDegrees < 0f ? -1f : 1f;
            float3 directionInput = new float3(
                TriangleSigned(phase01 + nodeTurns) + (TriangleSigned(phase01 + periapsisTurns) * eccentricity01 * 0.35f),
                TriangleSigned(phase01 + 0.125f) * inclination01 * inclinationSign,
                TriangleSigned(phase01 + 0.25f + periapsisTurns) * (1f - (eccentricity01 * 0.25f)));
            float3 direction = NormalizeVisualRsqrt(directionInput, new float3(0f, 0f, 1f));
            float cinematicRadialPulse = 1f + (TriangleSigned(phase01 + 0.375f) * eccentricity01 * 0.04f);
            float registryDistance = math.max(1f, orbit.registryOffsetMeters * cinematicRadialPulse);

            CinematicOrbitState state = default;
            state.Direction = direction;
            state.RegistryOffset = direction * registryDistance;
            state.Phase01 = phase01;
            state.Fullness01 = math.saturate(0.5f + (0.5f * math.dot(-sunDirection, direction)));
            return state;
        }

        private static float ResolveSeedPhase01(uint seed)
        {
            uint mixed = seed;
            mixed ^= mixed >> 16;
            mixed *= 0x7feb352du;
            mixed ^= mixed >> 15;
            mixed *= 0x846ca68bu;
            mixed ^= mixed >> 16;
            return mixed * SeedToUnit;
        }

        private static float Wrap01(float value)
        {
            return value - FastFloorToInt(value);
        }

        private static float TriangleSigned(float phase01)
        {
            return (TriangleWave01(phase01) * 2f) - 1f;
        }

        private static float TriangleWave01(float phase01)
        {
            return math.abs(math.frac(phase01) * 2f - 1f);
        }

        private static float ResolveMoonPhaseTextureIndex(float phase01)
        {
            return math.floor(math.saturate(phase01) * 7.999f);
        }

        private static int FastFloorToInt(float value)
        {
            int whole = (int)value;
            return value < whole ? whole - 1 : whole;
        }

        private static float3 NormalizeVisualRsqrt(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float FastSinRadians(float radians)
        {
            float phase = radians * 0.15915494309f;
            int whole = (int)phase;
            phase -= whole;
            if (phase < 0f)
                phase += 1f;
            else if (phase >= 1f)
                phase -= 1f;

            float centered = phase > 0.5f ? phase - 1f : phase;
            float wave = (4f * centered) - (8f * centered * math.abs(centered));
            return wave + 0.225f * ((wave * math.abs(wave)) - wave);
        }

        private static float FastCosRadians(float radians)
        {
            return FastSinRadians(radians + 1.57079632679f);
        }

        private static float3 ResolveDominantAxisDirection(float3 direction, float3 fallback)
        {
            float3 absDirection = math.abs(direction);
            if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
                return absDirection.x > 0.0001f
                    ? new float3(direction.x < 0f ? -1f : 1f, 0f, 0f)
                    : fallback;

            if (absDirection.y >= absDirection.z)
                return absDirection.y > 0.0001f
                    ? new float3(0f, direction.y < 0f ? -1f : 1f, 0f)
                    : fallback;

            return absDirection.z > 0.0001f
                ? new float3(0f, 0f, direction.z < 0f ? -1f : 1f)
                : fallback;
        }

        private static Vector3 ToVector3(float3 value)
        {
            return new Vector3(value.x, value.y, value.z);
        }

        private void UpdateSunVisualPosition()
        {
            if (_atmosphereManager != null)
            {
                if (sunVisualTransform != null && sunVisualTransform.gameObject.activeSelf)
                    sunVisualTransform.gameObject.SetActive(false);
                return;
            }

            if (sunVisualTransform == null || sunLight == null) return;

            Vector3 towardSun = -sunLight.transform.forward;
            bool hasObserver = TryResolvePlayerRuntimePosition(out Vector3 observerPos);

            sunVisualTransform.position = observerPos + towardSun * sunDistance;

            if (hasObserver)
                OrientSunVisualTowardObserver(observerPos);
        }

        private void OrientSunVisualTowardObserver(Vector3 observerPos)
        {
            Vector3 toObserver = observerPos - sunVisualTransform.position;
            float distanceSqr = toObserver.sqrMagnitude;
            if (distanceSqr <= 0.0001f)
                return;

            Vector3 forward = toObserver * math.rsqrt(distanceSqr);
            Vector3 referenceUp = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.98f
                ? Vector3.right
                : Vector3.up;

            Vector3 right = Vector3.Cross(referenceUp, forward);
            float rightSqr = right.sqrMagnitude;
            if (rightSqr <= 0.0001f)
            {
                referenceUp = Vector3.forward;
                right = Vector3.Cross(referenceUp, forward);
                rightSqr = right.sqrMagnitude;
                if (rightSqr <= 0.0001f)
                    return;
            }

            right *= math.rsqrt(rightSqr);
            Vector3 stableUp = Vector3.Cross(forward, right);
            sunVisualTransform.rotation = Quaternion.LookRotation(forward, stableUp);
        }

        private float CalculateSunElevation()
        {
            float3 toSun = _resolvedSunDirection;
            float sinElevation = math.dot(toSun, new float3(0, 1, 0));
            return math.degrees(math.asin(math.clamp(sinElevation, -1f, 1f)));
        }

        private void HandleDepthTierChanged(int depthTier, float depthMeters)
        {
            _currentDepthMeters = Mathf.Max(0f, depthMeters);
            UpdateDeepTextureResidencyState();
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
            HandleDepthTierChanged(depthTier, depthMeters);
        }

        private void UpdateDeepTextureResidencyState()
        {
            float depthMeters = Mathf.Max(0f, _currentDepthMeters);
            DynamicResolutionScaler scaler = GlobalRegistry.DynamicResolution;
            _currentAdaptiveRenderScale = scaler != null
                ? Mathf.Clamp01(scaler.CurrentRenderScale)
                : 1f;

            bool shouldReduceResidency = ShouldReduceDeepTextureResidency(depthMeters, _currentAdaptiveRenderScale);
            if (shouldReduceResidency == _deepTextureResidencyReduced)
                return;

            if (shouldReduceResidency)
                DetachDeepCelestialTextures();
            else
                RestoreCelestialTextureDefaults();
        }

        private bool ShouldReduceDeepTextureResidency(float depthMeters, float renderScale)
        {
            float depthReleaseThreshold = Mathf.Max(0f, deepTextureUnloadDepth - deepTextureDepthHysteresis);
            float adaptiveDepthReleaseThreshold = Mathf.Max(0f, adaptiveDeepTextureMinDepth - deepTextureDepthHysteresis);

            if (_deepTextureResidencyReduced)
            {
                bool keepDepthReduced = depthMeters >= depthReleaseThreshold;
                bool keepPerfReduced =
                    enableAdaptiveDeepTextureResidency &&
                    depthMeters >= adaptiveDepthReleaseThreshold &&
                    renderScale <= adaptiveDeepTextureRestoreRenderScale;
                return keepDepthReduced || keepPerfReduced;
            }

            bool reduceByDepth = depthMeters >= deepTextureUnloadDepth;
            bool reduceByPerfPressure =
                enableAdaptiveDeepTextureResidency &&
                depthMeters >= adaptiveDeepTextureMinDepth &&
                renderScale <= adaptiveDeepTextureUnloadRenderScale;

            return reduceByDepth || reduceByPerfPressure;
        }

        private void DetachDeepCelestialTextures()
        {
            SetSkyTextureAllTargets(_ID_HighCloudTex, null);
            SetSkyTextureAllTargets(_ID_MainCloudAtlas, null);
            SetSkyTextureAllTargets(_ID_MainCloudTex, null);

            SetMaterialTexture(daySkybox, _ID_MainTex, null);
            SetMaterialTexture(daySkybox, _ID_EmissionMap, null);
            SetMaterialTexture(nightSkybox, _ID_MainTex, null);
            SetMaterialTexture(nightSkybox, _ID_EmissionMap, null);

            SetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap, null);
            SetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap, null);

            SetMaterialTexture(_aegirSharedMaterial, _ID_MainTex, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap, null);
            SetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex, null);

            _deepTextureResidencyReduced = true;
        }

        private void RestoreCelestialTextureDefaults()
        {
            SetSkyTextureAllTargets(_ID_HighCloudTex, _skyHighCloudTexDefault);
            SetSkyTextureAllTargets(_ID_MainCloudAtlas, _skyMainCloudAtlasDefault);
            SetSkyTextureAllTargets(_ID_MainCloudTex, _skyMainCloudTexDefault);

            SetMaterialTexture(daySkybox, _ID_MainTex, _daySkyboxMainTexDefault);
            SetMaterialTexture(daySkybox, _ID_EmissionMap, _daySkyboxEmissionTexDefault);
            SetMaterialTexture(nightSkybox, _ID_MainTex, _nightSkyboxMainTexDefault);
            SetMaterialTexture(nightSkybox, _ID_EmissionMap, _nightSkyboxEmissionTexDefault);

            SetMaterialTexture(blendedSkyboxMaterial, _ID_DayCubemap, _blendedDayCubemapDefault);
            SetMaterialTexture(blendedSkyboxMaterial, _ID_NightCubemap, _blendedNightCubemapDefault);

            SetMaterialTexture(_aegirSharedMaterial, _ID_MainTex, _aegirMainTexDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_DetailTex, _aegirDetailTexDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_EmissionMap, _aegirEmissionMapDefault);
            SetMaterialTexture(_aegirSharedMaterial, _ID_CelestialOcclusionTex, _aegirCelestialOcclusionTexDefault);

            _deepTextureResidencyReduced = false;
        }

        private static Texture GetMaterialTexture(Material material, int propertyId)
        {
            if (material == null || !material.HasProperty(propertyId))
                return null;

            return material.GetTexture(propertyId);
        }

        private static void SetMaterialTexture(Material material, int propertyId, Texture texture)
        {
            if (material == null || !material.HasProperty(propertyId))
                return;

            material.SetTexture(propertyId, texture);
        }

        private static void SetMaterialFloat(Material material, int propertyId, float value)
        {
            if (material == null || !material.HasProperty(propertyId))
                return;

            material.SetFloat(propertyId, value);
        }

        private static float GetMaterialFloat(Material material, int propertyId, float fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetFloat(propertyId);
        }

        private static Vector4 GetMaterialVector(Material material, int propertyId, Vector4 fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetVector(propertyId);
        }

        private static Color GetMaterialColor(Material material, int propertyId, Color fallback)
        {
            if (material == null || !material.HasProperty(propertyId))
                return fallback;

            return material.GetColor(propertyId);
        }

        private float ResolveCloudSpeedMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherCloudSpeedMultiplier);
        }

        private float ResolveStarVisibilityMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Clamp01(_surfaceWeatherStarVisibilityMultiplier);
        }

        private float ResolveStarMapSeed()
        {
            int seed = Mathf.RoundToInt(starMapSeed);
            global::HectonWorldGenerator generator = GlobalRegistry.WorldSeedProvider as global::HectonWorldGenerator;
            if (generator == null)
                return seed & 0x00FFFFFF;

            unchecked
            {
                if (generator.spine != null)
                {
                    seed = FoldStarSeed(seed, generator.spine.warpNoise);
                    seed = FoldStarSeed(seed, generator.spine.islandNoise);
                }

                if (generator.biomes != null)
                {
                    seed = FoldStarSeed(seed, generator.biomes.biomeNoise);
                    seed = FoldStarSeed(seed, generator.biomes.flatSurfaceNoise);
                    seed = FoldStarSeed(seed, generator.biomes.aggressiveSurfaceNoise);
                }

                if (generator.displacement != null)
                    seed = FoldStarSeed(seed, generator.displacement.noise);

                if (generator.caves != null)
                    seed = FoldStarSeed(seed, generator.caves.noise);
            }

            return seed & 0x00FFFFFF;
        }

        private static int FoldStarSeed(int seed, global::HectonNoiseLayer layer)
        {
            if (layer == null)
                return seed;

            unchecked
            {
                return (seed * 397) ^ layer.seed;
            }
        }

        private float ResolveStormEmissionMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherStormEmissionMultiplier);
        }

        private void ApplySurfaceWeatherSkyProperties(Material targetMaterial)
        {
            CacheSkyWeatherDefaults();

            if (!_cachedSkyWeatherDefaults || targetMaterial == null)
                return;

            targetMaterial.SetFloat(_ID_SkyLuminanceMultiplier, ResolveSkyLuminanceMultiplier());

            if (!_surfaceWeatherOverrideActive)
            {
                targetMaterial.SetFloat(_ID_CloudDensityThreshold, _defaultCloudDensityThreshold);
                targetMaterial.SetFloat(_ID_CloudSoftness, _defaultCloudSoftness);
                targetMaterial.SetFloat(_ID_CloudSpeedMult, _defaultCloudSpeedMultiplier);
                targetMaterial.SetVector(_ID_WindDirection, _defaultWindDirection);
                targetMaterial.SetColor(_ID_CloudColorLit, _defaultCloudLitColor);
                targetMaterial.SetColor(_ID_CloudColorShadow, _defaultCloudShadowColor);
                targetMaterial.SetColor(_ID_SunsetCloudColor, ResolveScriptSunsetCloudColor());
                targetMaterial.SetColor(_ID_NightCloudColor, ResolveScriptNightCloudColor());
                targetMaterial.SetColor(_ID_SunDiscColor, _defaultSunDiscColor);
                targetMaterial.SetColor(_ID_SunScatterColor, _defaultSunScatterColor);
                return;
            }

            targetMaterial.SetFloat(_ID_CloudDensityThreshold, _surfaceWeatherCloudDensityThreshold);
            targetMaterial.SetFloat(_ID_CloudSoftness, _surfaceWeatherCloudSoftness);
            targetMaterial.SetFloat(_ID_CloudSpeedMult, _defaultCloudSpeedMultiplier * ResolveCloudSpeedMultiplier());
            targetMaterial.SetVector(_ID_WindDirection, _surfaceWeatherWindDirection);
            targetMaterial.SetColor(_ID_CloudColorLit, _surfaceWeatherCloudLitColor);
            targetMaterial.SetColor(_ID_CloudColorShadow, _surfaceWeatherCloudShadowColor);
            targetMaterial.SetColor(_ID_SunsetCloudColor, _surfaceWeatherSunsetCloudColor);
            targetMaterial.SetColor(_ID_NightCloudColor, _surfaceWeatherNightCloudColor);
            targetMaterial.SetColor(_ID_SunDiscColor, _defaultSunDiscColor * Mathf.Max(0f, _surfaceWeatherSunDiscMultiplier));
            targetMaterial.SetColor(_ID_SunScatterColor, _defaultSunScatterColor * Mathf.Max(0f, _surfaceWeatherSunScatterMultiplier));
        }

        private Color ResolveScriptSunsetCloudColor()
        {
            EnsureAtmosphereGradientSamples();
            float4 sunsetSample = SampleAtmosphereGradient(_sunsetAtmosphereGradientSamples, 0f);
            Color sunsetAtmosphereColor = new Color(sunsetSample.x, sunsetSample.y, sunsetSample.z, 1f);
            Color sunsetCloudColor = MultiplyRgb(_sunsetProfile.horizonColor, sunsetAtmosphereColor);
            sunsetCloudColor.a = 1f;
            return sunsetCloudColor;
        }

        private Color ResolveScriptNightCloudColor()
        {
            EnsureAtmosphereGradientSamples();
            float4 nightSample = SampleAtmosphereGradient(_nightAtmosphereGradientSamples, 0f);
            Color nightAtmosphereColor = new Color(nightSample.x, nightSample.y, nightSample.z, 1f);
            Color nightCloudColor = MultiplyRgb(_nightProfile.horizonColor, nightAtmosphereColor);
            nightCloudColor.a = 1f;
            return nightCloudColor;
        }

        private Color ResolveScriptSunsetHorizonColor()
        {
            EnsureAtmosphereGradientSamples();
            float4 sunsetSample = SampleAtmosphereGradient(_sunsetAtmosphereGradientSamples, 0f);
            Color sunsetAtmosphereColor = new Color(sunsetSample.x, sunsetSample.y, sunsetSample.z, 1f);
            Color sunsetHorizonColor = MultiplyRgb(_sunsetProfile.horizonColor, sunsetAtmosphereColor);
            sunsetHorizonColor.a = 1f;
            return sunsetHorizonColor;
        }

        private float ResolveScriptAegirNightGlow()
        {
            return Mathf.Max(0f, _currentBacklitFactor * _atmosphereInscatterWeight);
        }

        private void ApplySurfaceWeatherSkyLuminance(ref Color zenith, ref Color horizon, ref Color nadir)
        {
            float multiplier = ResolveSkyLuminanceMultiplier();
            zenith *= multiplier;
            horizon *= multiplier;
            nadir *= multiplier;
        }

        private float ResolveSkyLuminanceMultiplier()
        {
            if (!_surfaceWeatherOverrideActive)
                return 1f;

            return Mathf.Max(0f, _surfaceWeatherSkyLuminanceMultiplier);
        }

        // ─────────────────────────────────────────────
        // SKY MATERIAL UPDATE
        // ─────────────────────────────────────────────

        private void UpdateSkyMaterial()
        {
            if (_skyMaterial == null)
                return;

            float sunElevationNormalized = math.clamp(_currentSunAngle * Inv90, -1f, 1f);
            float3 fromSun = -_resolvedSunDirection;
            Vector4 sunDirection = new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f);
            Vector4 aegirDirection = Vector4.zero;
            if (TryResolveAegirSkyDirection(out float3 toAegir))
            {
                aegirDirection = new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f);
            }

            ApplySkyMaterialProperties(_skyMaterial, sunElevationNormalized, sunDirection, aegirDirection);
            _previousBlendForColors = _currentBlend;
            _lastAppliedSkyZenith = _resolvedSkyZenith;
            _lastAppliedSkyHorizon = _resolvedSkyHorizon;
            _lastAppliedSkyNadir = _resolvedSkyNadir;
        }

        private void ApplySkyboxMaterialOwnership(bool forceAssignment)
        {
            ForceMandatedSkyMaterialReference();
            Material targetSkyboxMaterial = ResolvePreferredSkyboxMaterial();
            if (targetSkyboxMaterial == null)
                return;

            Material activeSkybox = AtmosphereDirector.Skybox;
            if (forceAssignment ||
                !ReferenceEquals(activeSkybox, targetSkyboxMaterial) ||
                ReferenceEquals(activeSkybox, _skyMaterial))
            {
                AtmosphereDirector.SetSkybox(targetSkyboxMaterial);
            }
        }

        private void ForceMandatedSkyMaterialReference()
        {
            if (!IsMandatedSkyMaterial(_skyMaterial))
            {
                if (HectonUnderwaterVisuals.TryGetRuntimeSkyMaterialReference(out Material underwaterSkyMaterial) &&
                    IsMandatedSkyMaterial(underwaterSkyMaterial))
                {
                    _skyMaterial = underwaterSkyMaterial;
                }
                else if (IsMandatedSkyMaterial(AtmosphereDirector.Skybox))
                {
                    _skyMaterial = AtmosphereDirector.Skybox;
                }
            }

            if (_skyMaterial != null && !ReferenceEquals(AtmosphereDirector.Skybox, _skyMaterial))
                AtmosphereDirector.SetSkybox(_skyMaterial);
        }

        private Material ResolvePreferredSkyboxMaterial()
        {
            ForceMandatedSkyMaterialReference();
            return _skyMaterial;
        }

        private static bool IsBlendSkyboxMaterial(Material material)
        {
            return material != null &&
                   material.HasProperty(_ID_Blend) &&
                   material.HasProperty(_ID_DayCubemap) &&
                   material.HasProperty(_ID_NightCubemap);
        }

        private static bool IsMandatedSkyMaterial(Material material)
        {
            return material != null &&
                   material.name.StartsWith(MandatedSkyMaterialName, StringComparison.Ordinal);
        }

        private void SetSkyTextureAllTargets(int propertyId, Texture texture)
        {
            SetMaterialTexture(_skyMaterial, propertyId, texture);
        }

        private void ApplySkyMaterialProperties(
            Material targetMaterial,
            float sunElevationNormalized,
            Vector4 sunDirection,
            Vector4 aegirDirection)
        {
            if (targetMaterial == null)
                return;

            targetMaterial.SetFloat(_ID_GameTime, _gameTime);
            targetMaterial.SetFloat(_ID_NightBlend, _currentBlend);
            targetMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
            targetMaterial.SetFloat(_ID_StarSeed, _resolvedStarMapSeed);
            targetMaterial.SetFloat(_ID_SunElevation, sunElevationNormalized);
            targetMaterial.SetFloat(_ID_EclipseOcclusion, ResolveReadableEclipseShaderOcclusion(_smoothedOcclusionFactor));
            targetMaterial.SetFloat(_ID_PenumbraFactor, _penumbraFactor);
            targetMaterial.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            targetMaterial.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);
            targetMaterial.SetVector(_ID_SunDirection, sunDirection);
            targetMaterial.SetVector(_ID_AegirDirection, aegirDirection);
            targetMaterial.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            targetMaterial.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            targetMaterial.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);
            targetMaterial.SetColor(_ID_SunsetHorizonColor, ResolveScriptSunsetHorizonColor());
            targetMaterial.SetFloat(_ID_AegirGlowIntensity, ResolveScriptAegirNightGlow());
            ApplySkyMaterialHazeProperties(targetMaterial);
            ApplySurfaceWeatherSkyProperties(targetMaterial);
        }

        private void ApplySkyMaterialHazeProperties(Material targetMaterial)
        {
            AtmosphericLightingState state = _surfaceAtmosphericLightingState.IsValid != 0
                ? _surfaceAtmosphericLightingState
                : BuildSurfaceAtmosphericLightingState();

            targetMaterial.SetFloat(_ID_HazeIntensity, state.HorizonHazeIntensity);
            targetMaterial.SetFloat(_ID_HazeFalloff, state.HorizonHazeFalloff);
            targetMaterial.SetColor(_ID_HazeColor, state.HorizonHazeColor);
            targetMaterial.SetFloat(_ID_HazeSunTintStrength, state.HorizonHazeSunTintStrength);
            targetMaterial.SetFloat(_ID_HorizonMistShelfIntensity, state.HorizonMistShelfIntensity);
            targetMaterial.SetFloat(_ID_HorizonMistShelfHeight, state.HorizonMistShelfHeight);
            targetMaterial.SetFloat(_ID_HorizonMistShelfSoftness, state.HorizonMistShelfSoftness);
        }

        private void ResolveSkyColors(out Color zenith, out Color horizon, out Color nadir)
        {
            EvaluateCelestialAtmosphereProfileWeights(
                _currentSunAngle,
                out float dayWeight,
                out float sunsetWeight,
                out float nightWeight);

            zenith = ResolveSkyProfileColor(
                _dayProfile.zenithColor,
                _sunsetProfile.zenithColor,
                _nightProfile.zenithColor,
                dayWeight,
                sunsetWeight,
                nightWeight);
            horizon = ResolveSkyProfileColor(
                _dayProfile.horizonColor,
                _sunsetProfile.horizonColor,
                _nightProfile.horizonColor,
                dayWeight,
                sunsetWeight,
                nightWeight);
            nadir = ResolveSkyProfileColor(
                _dayProfile.nadirColor,
                _sunsetProfile.nadirColor,
                _nightProfile.nadirColor,
                dayWeight,
                sunsetWeight,
                nightWeight);

            if (_smoothedOcclusionFactor > 0f)
            {
                float eclipseNight = ResolveReadableEclipseSkyBlend(_smoothedOcclusionFactor);
                zenith = Color.Lerp(zenith, _nightProfile.zenithColor, eclipseNight);
                horizon = Color.Lerp(horizon, _nightProfile.horizonColor, eclipseNight);
                nadir = Color.Lerp(nadir, _nightProfile.nadirColor, eclipseNight);
            }

            horizon = CompressHorizonColor(horizon, zenith, dayWeight, sunsetWeight, nightWeight);
            ApplySurfaceWeatherSkyColorInfluence(ref zenith, ref horizon, ref nadir, dayWeight, sunsetWeight, nightWeight);
            ApplySurfaceWeatherSkyLuminance(ref zenith, ref horizon, ref nadir);
            ApplyReadableSkyColorFloors(ref zenith, ref horizon, ref nadir);
        }

        private void ApplySurfaceWeatherSkyColorInfluence(
            ref Color zenith,
            ref Color horizon,
            ref Color nadir,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            if (!_surfaceWeatherOverrideActive)
                return;

            Color weatherHorizonAnchor = Color.Lerp(_surfaceWeatherFogColor, _surfaceWeatherAmbientColor, 0.35f);
            Color weatherZenithAnchor = Color.Lerp(_surfaceWeatherAmbientColor, weatherHorizonAnchor, 0.22f);
            float horizonBlend = dayWeight * 0.24f + sunsetWeight * 0.14f;
            float zenithBlend = dayWeight * 0.08f + sunsetWeight * 0.04f;
            float nadirBlend = dayWeight * 0.05f + nightWeight * 0.02f;

            horizon = Color.Lerp(horizon, weatherHorizonAnchor, Mathf.Clamp01(horizonBlend));
            horizon = DesaturateColor(horizon, Mathf.Lerp(0.18f, 0.08f, sunsetWeight));
            zenith = Color.Lerp(zenith, weatherZenithAnchor, Mathf.Clamp01(zenithBlend));
            nadir = Color.Lerp(nadir, weatherHorizonAnchor, Mathf.Clamp01(nadirBlend * 0.35f));

            horizon.a = 1f;
            zenith.a = 1f;
            nadir.a = 1f;
        }

        private static Color ResolveSkyProfileColor(
            Color dayColor,
            Color sunsetColor,
            Color nightColor,
            float dayWeight,
            float sunsetWeight,
            float nightWeight)
        {
            Color blendedColor =
                dayColor * dayWeight +
                sunsetColor * sunsetWeight +
                nightColor * nightWeight;
            blendedColor.a = 1f;
            return blendedColor;
        }

        private Color CompressHorizonColor(Color horizon, Color zenith, float dayWeight, float sunsetWeight, float nightWeight)
        {
            float effectiveZenithBlend =
                dayWeight * (_horizonZenithBlend * 0.35f) +
                sunsetWeight * (_horizonZenithBlend * 0.7f) +
                nightWeight * _horizonZenithBlend;
            float effectiveBrightnessScale =
                dayWeight * Mathf.Lerp(1f, _horizonBrightnessScale, 0.28f) +
                sunsetWeight * Mathf.Lerp(1f, _horizonBrightnessScale, 0.5f) +
                nightWeight * _horizonBrightnessScale;

            Color compressed = Color.Lerp(horizon, zenith, effectiveZenithBlend);
            compressed *= effectiveBrightnessScale;
            float targetLuminance =
                ComputePerceivedLuminance(horizon) *
                (dayWeight * 0.92f + sunsetWeight * 0.82f + nightWeight * 0.68f);
            compressed = LiftColorTowardsLuminance(
                compressed,
                targetLuminance,
                0.34f);
            compressed = DesaturateColor(
                compressed,
                dayWeight * 0.26f + nightWeight * 0.04f);
            compressed.a = horizon.a;
            return compressed;
        }

        // ─────────────────────────────────────────────
        // SUN OCCLUSION
        // ─────────────────────────────────────────────

        private void UpdateSunOcclusion(float dt)
        {
            _sunOcclusionFactor = math.saturate(_penumbraFactor);

#if UNITY_EDITOR
            if (!Application.isPlaying && dt <= 0f)
            {
                _smoothedOcclusionFactor = _sunOcclusionFactor;
                return;
            }
#endif
            _smoothedOcclusionFactor = math.lerp(
                _smoothedOcclusionFactor,
                _sunOcclusionFactor,
                math.saturate(flareFadeSpeed * dt));

            if (_smoothedOcclusionFactor < 0.001f) _smoothedOcclusionFactor = 0f;
            if (_smoothedOcclusionFactor > 0.999f) _smoothedOcclusionFactor = 1f;
        }

        /// <summary>
        /// v5.1 FIX: MULTIPLY, NOT OVERWRITE.
        ///
        /// By this point in the tick chain:
        ///   - AtmosphereManager has computed ProfileSunIntensity + HorizonFade
        ///   - UnderwaterVisuals has written:
        ///       sunLight.intensity = profile × horizon × depthFactor
        ///
        /// We simply multiply by eclipse visibility:
        ///   sunLight.intensity *= (1 - occlusionFactor)
        ///
        /// This preserves ALL previous factors. Eclipse just dims
        /// whatever is already there. Zero knowledge of depth system.
        ///
        /// STANDALONE MODE (no AtmosphereManager):
        ///   If nothing else has written sunLight.intensity this frame,
        ///   we use our captured _baseSunIntensity as the starting point.
        ///   This handles the case where CelestialEngine runs alone.
        ///
        /// EDGE CASE — visibility ≈ 1 (no eclipse):
        ///   Skip the multiply entirely. This avoids unnecessary
        ///   float imprecision that could cause 0.9999 × 1.0 = 0.9999
        ///   drift over thousands of frames.
        /// </summary>
        private void ApplySunOcclusion()
        {
            float rawVisibility = (1.0f - _smoothedOcclusionFactor) * _moonPhaseShadowVisibility;
            float visibility = Mathf.Max(rawVisibility, SurfaceEclipseVisibilityFloor);
            bool skyOwnsPrimarySunDisc = _atmosphereManager != null;

            // ── Sun Light Intensity ──
            if (sunLight != null)
            {
                if (_atmosphereManager != null)
                {
                    // v5.1: MULTIPLY the existing intensity.
                    // UnderwaterVisuals already wrote: profile × horizon × depth.
                    // We just dim by eclipse.
                    if (visibility < 0.999f)
                    {
                        sunLight.intensity *= visibility;
                    }
                    // else: visibility ≈ 1, no eclipse active, skip multiply
                }
                else if (_baseSunIntensityCaptured)
                {
                    // Standalone: no UnderwaterVisuals pipeline.
                    // We are the sole intensity controller.
                    sunLight.intensity = Mathf.Max(_baseSunIntensity, SurfaceReadableSunIntensityFloor) * visibility;
                }
            }

            // ── Lens Flare ──
            DisableLegacySunFlare();

            // ── Sun Visual Disc ──
            if (sunVisualTransform != null)
            {
                bool shouldBeActive = visibility > 0.001f;

                if (!skyOwnsPrimarySunDisc
                    && sunVisualTransform.gameObject.activeSelf != shouldBeActive)
                {
                    sunVisualTransform.gameObject.SetActive(shouldBeActive);
                }

                if (shouldBeActive && sunVisualTransform.gameObject.activeSelf)
                {
                    Renderer sunRenderer = GetCachedSunDiscRenderer();
                    if (sunRenderer != null)
                    {
                        sunRenderer.GetPropertyBlock(_sunDiscMPB);
                        _sunDiscMPB.SetFloat(_ID_OcclusionFactor, visibility);
                        _sunDiscMPB.SetColor(_ID_EmissionColor, Color.white * visibility);
                        sunRenderer.SetPropertyBlock(_sunDiscMPB);
                    }
                }
            }
        }

        private Renderer GetCachedSunDiscRenderer()
        {
            if (!_sunDiscRendererCached && sunVisualTransform != null)
            {
                sunVisualTransform.TryGetComponent(out _cachedSunDiscRenderer);
                _sunDiscRendererCached = true;
            }
            return _cachedSunDiscRenderer;
        }

        private void HideSunVisualDisc()
        {
            if (sunVisualTransform != null && sunVisualTransform.gameObject.activeSelf)
                sunVisualTransform.gameObject.SetActive(false);
        }

        private void RestoreSunDefaults()
        {
            if (sunLight != null && _baseSunIntensityCaptured && _atmosphereManager == null)
                sunLight.intensity = _baseSunIntensity;

            if (sunLight != null && _baseSunColorCaptured)
                sunLight.color = _baseSunColor;

            DisableLegacySunFlare();

            if (_atmosphereManager != null)
            {
                HideSunVisualDisc();
            }
            else if (sunVisualTransform != null && !sunVisualTransform.gameObject.activeSelf)
            {
                sunVisualTransform.gameObject.SetActive(true);
            }
        }

        // ─────────────────────────────────────────────
        // SKYBOX BLEND
        // ─────────────────────────────────────────────

        private void UpdateSkyboxBlend(float sunElevation)
        {
            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            float rangeInv = math.rcp(range);
            float timeBlend = math.saturate((twilightStartAngle - sunElevation) * rangeInv);
            timeBlend = SmoothStep01(timeBlend);

            // v5.1: Eclipse also triggers night sky, but presentation must stay readable.
            _currentBlend = math.max(timeBlend, ResolveReadableEclipseSkyBlend(_smoothedOcclusionFactor));
            ApplySkyboxMaterialOwnership(forceAssignment: false);

            if (blendedSkyboxMaterial != null)
                blendedSkyboxMaterial.SetFloat(_ID_Blend, _currentBlend);
        }

        private void UpdateStarIntensity(float sunElevation)
        {
            float range = twilightStartAngle - twilightEndAngle;
            if (range < 0.001f) range = 10f;

            float rangeInv = math.rcp(range);
            float timeStars = math.saturate((twilightStartAngle - sunElevation) * rangeInv);
            timeStars = SmoothStep01(timeStars);

            _currentStarIntensity = math.max(timeStars, _smoothedOcclusionFactor);
            _currentStarIntensity *= ResolveStarVisibilityMultiplier();

            if (blendedSkyboxMaterial != null)
                blendedSkyboxMaterial.SetFloat(_ID_StarIntensity, _currentStarIntensity);
        }

        // ─────────────────────────────────────────────
        // GLOBAL SHADER DATA
        // ─────────────────────────────────────────────

        private void UpdateGlobalShaderData()
        {
            if (_atmosphereManager == null && ShouldUploadGlobalSunDirectionThisMinute())
            {
                float3 fromSun = -_resolvedSunDirection;
                Shader.SetGlobalVector(_ID_SunDirection,
                    new Vector4(fromSun.x, fromSun.y, fromSun.z, 0f));
            }

            Vector4 aegirDirection = Vector4.zero;
            if (TryResolveAegirSkyDirection(out float3 toAegir))
                aegirDirection = new Vector4(toAegir.x, toAegir.y, toAegir.z, 0f);

            Shader.SetGlobalVector(_ID_AegirDirection, aegirDirection);
            PublishSkyRotationAndOccluders(aegirDirection);
            Shader.SetGlobalColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            Shader.SetGlobalColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            Shader.SetGlobalColor(_ID_SkyColorNadir, _resolvedSkyNadir);
            Shader.SetGlobalFloat(_ID_NightBlend, _currentBlend);
            Shader.SetGlobalFloat(_ID_EclipseOcclusion, ResolveReadableEclipseShaderOcclusion(_smoothedOcclusionFactor));
            Shader.SetGlobalFloat(_ID_PenumbraFactor, _penumbraFactor);
            Shader.SetGlobalFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            Shader.SetGlobalFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);
            Shader.SetGlobalFloat(_ID_AtmosphereDensity, _currentAtmosphereDensity);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereLutReady, _celestialAtmosphereLutTexture != null ? 1f : 0f);
            Shader.SetGlobalFloat(_ID_AtmosphereExposure, _currentAtmosphereExposure);
            Shader.SetGlobalFloat(_ID_StarSeed, _resolvedStarMapSeed);
            Shader.SetGlobalFloat(_ID_CelestialHorizonDensity, horizonDensity);
            Shader.SetGlobalFloat(_ID_CelestialZenithTransparency, zenithTransparency);
            Shader.SetGlobalFloat(_ID_CelestialAtmosphereBlendPower, atmosphereBlendPower);
            Shader.SetGlobalFloat(_ID_GameTime, _gameTime);
            UploadStormCloudDensityShaderGlobal(ResolveStormCloudDensity01(), forceUpload: false);
            UploadLightningFlashShaderGlobal(_lightningFlash01, forceUpload: false);
            PublishOceanCelestialProjectionGlobals(aegirDirection);
            PublishCelestialAtmosphereLut(pushRenderSettings: false);
        }

        private void UploadStormCloudDensityShaderGlobal(float stormCloudDensity01, bool forceUpload)
        {
            float safeStormCloudDensity01 = math.isfinite(stormCloudDensity01)
                ? math.saturate(stormCloudDensity01)
                : 0f;
            if (!forceUpload && math.abs(safeStormCloudDensity01 - _lastUploadedStormCloudDensity01) <= ShaderScalarEpsilon)
                return;

            Shader.SetGlobalFloat(_ID_HectonStormCloudDensity, safeStormCloudDensity01);
            _lastUploadedStormCloudDensity01 = safeStormCloudDensity01;
        }

        private void UpdateLightningFlashShaderGlobal(bool forceUpload)
        {
            if (_lightningFlash01 > LightningFlashEpsilon)
            {
                _lightningFlash01 = math.lerp(_lightningFlash01, 0f, LightningFlashDecayLerpPerLateFrame);
            }
            else if (_lightningFlash01 != 0f)
            {
                _lightningFlash01 = 0f;
            }
            else if (!forceUpload)
            {
                return;
            }

            UploadLightningFlashShaderGlobal(_lightningFlash01, forceUpload);
        }

        private void UploadLightningFlashShaderGlobal(float lightningFlash01, bool forceUpload)
        {
            float safeLightningFlash01 = math.isfinite(lightningFlash01)
                ? math.saturate(lightningFlash01)
                : 0f;
            if (!forceUpload && math.abs(safeLightningFlash01 - _lastUploadedLightningFlash01) <= LightningFlashEpsilon)
                return;

            Shader.SetGlobalFloat(_ID_HectonLightningFlash, safeLightningFlash01);
            _lastUploadedLightningFlash01 = safeLightningFlash01;
        }

        private float ResolveStormCloudDensity01()
        {
            float density = 0f;
            IWeatherService weather = GlobalRegistry.Weather;
            if (weather != null && (weather.CurrentWeatherState & WeatherState.Storm) != 0)
                density = math.saturate(weather.WeatherIntensity);

            if (_surfaceWeatherOverrideActive)
                density = math.max(density, math.saturate(_surfaceWeatherCloudDensityThreshold));

            _stormCloudDensity01 = density;
            return density;
        }

        private void PublishSkyRotationAndOccluders(Vector4 aegirDirection)
        {
            float timeOfDay01 = _atmosphereManager != null
                ? Mathf.Repeat(_atmosphereManager.TimeOfDay, 1f)
                : Mathf.Repeat(_rotationPhase, 1f);
            float skyAngleRad = timeOfDay01 * math.PI * 2f;
            float skyCos = FastCosRadians(skyAngleRad);
            float skySin = FastSinRadians(skyAngleRad);
            Matrix4x4 skyRotation = Matrix4x4.identity;
            skyRotation.m00 = skyCos;
            skyRotation.m02 = skySin;
            skyRotation.m20 = -skySin;
            skyRotation.m22 = skyCos;
            Shader.SetGlobalMatrix(_ID_HectonSkyRotation, skyRotation);

            int occluderCount = 0;
            if (aegirDirection.sqrMagnitude > 0.0001f)
            {
                _skyOccluders[occluderCount++] = new Vector4(
                    aegirDirection.x,
                    aegirDirection.y,
                    aegirDirection.z,
                    math.radians(math.max(GetAegirAngularRadiusDegrees(), 0.01f)));
            }

            for (int i = 0; i < _observerBodyCache.Count && occluderCount < CelestialBodyCacheCapacity; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (body == null || body == aegirObserverRelativeBody)
                    continue;

                Vector3 bodyDirectionManaged = body.CurrentDirection;
                float sqrMagnitude = bodyDirectionManaged.sqrMagnitude;
                if (sqrMagnitude <= 0.0001f || !float.IsFinite(sqrMagnitude))
                    continue;

                float invMagnitude = math.rsqrt(sqrMagnitude);
                _skyOccluders[occluderCount++] = new Vector4(
                    bodyDirectionManaged.x * invMagnitude,
                    bodyDirectionManaged.y * invMagnitude,
                    bodyDirectionManaged.z * invMagnitude,
                    math.radians(math.max(body.AngularDiameterDegrees * 0.5f, 0.01f)));
            }

            for (int i = occluderCount; i < CelestialBodyCacheCapacity; i++)
                _skyOccluders[i] = Vector4.zero;

            Shader.SetGlobalInt(_ID_HectonSkyOccluderCount, occluderCount);
            Shader.SetGlobalVectorArray(_ID_HectonSkyOccluders, _skyOccluders);
        }

        private void PublishOceanCelestialProjectionGlobals(Vector4 aegirDirection)
        {
            float2 planarDirection = new float2(aegirDirection.x, aegirDirection.z);
            float planarLengthSq = math.lengthsq(planarDirection);
            if (planarLengthSq <= 0.0001f)
                planarDirection = new float2(1f, 0f);
            else
                planarDirection *= math.rsqrt(planarLengthSq);

            float radius = math.max(256f, eclipseWaterShadowRadiusMeters);
            float travelSpan = radius * 2f;
            float travel = math.fmod(_gameTime * math.max(0f, eclipseWaterShadowScrollMetersPerSecond), travelSpan);
            float2 shadowCenter = ResolveAupOceanShadowCenterRuntimeXZ(planarDirection, travel - radius);
            float waterShadowStrength = math.saturate(_penumbraFactor * eclipseWaterShadowDarkening);
            Shader.SetGlobalVector(
                _ID_HectonEclipseWaterShadowParams,
                new Vector4(shadowCenter.x, shadowCenter.y, radius, waterShadowStrength));
            Shader.SetGlobalVector(
                _ID_HectonEclipseWaterShadowDirection,
                new Vector4(planarDirection.x, planarDirection.y, math.saturate(eclipseWaterShadowSoftness), _penumbraFactor));

            float sunAegirAlignment = math.saturate((math.dot(_resolvedSunDirection, new float3(aegirDirection.x, aegirDirection.y, aegirDirection.z)) - 0.45f) * Inv55);
            sunAegirAlignment = SmoothStep01(sunAegirAlignment);
            float ringStrength = math.saturate(aegirRingCausticStrength * sunAegirAlignment);
            Shader.SetGlobalVector(
                _ID_HectonRingCausticsParams,
                new Vector4(
                    ringStrength,
                    math.max(0.0005f, aegirRingCausticStripeScale),
                    _gameTime * math.max(0f, aegirRingCausticScrollSpeed),
                    math.max(0.001f, aegirRingCausticSoftness)));
            Shader.SetGlobalVector(
                _ID_HectonRingCausticsDirection,
                new Vector4(planarDirection.x, planarDirection.y, sunAegirAlignment, 0f));
        }

        private float2 ResolveAupOceanShadowCenterRuntimeXZ(float2 planarDirection, float signedTravelMeters)
        {
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return planarDirection * signedTravelMeters;

            double3 playerAbsolute = playerAup.ToAbsoluteDouble3();
            double2 shadowAbsoluteXZ = new double2(playerAbsolute.x, playerAbsolute.z) +
                                       (new double2(planarDirection.x, planarDirection.y) * signedTravelMeters);
            AbsoluteUniversePosition shadowAup = AbsoluteUniversePosition.FromAbsolutePosition(
                new double3(shadowAbsoluteXZ.x, playerAbsolute.y, shadowAbsoluteXZ.y));
            float3 shadowRuntime = shadowAup.ToRuntimeFloat3();
            return new float2(shadowRuntime.x, shadowRuntime.z);
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;

            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                playerAup = snapshot.Aup;
                return playerAup.IsFinite();
            }

            HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return playerAup.IsFinite();
        }

        private bool TryResolvePlayerRuntimePosition(out Vector3 runtimePosition)
        {
            runtimePosition = Vector3.zero;

            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            if (playerContext == null)
                return false;

            if (playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                math.all(math.isfinite(snapshot.RuntimePosition)))
            {
                runtimePosition = new Vector3(
                    snapshot.RuntimePosition.x,
                    snapshot.RuntimePosition.y,
                    snapshot.RuntimePosition.z);
                return true;
            }

            HectonPlayerMovement playerMovement = playerContext.PlayerMovement;
            if (playerMovement == null)
                return false;

            float3 runtime = playerMovement.CurrentAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtime)))
                return false;

            runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
            return true;
        }

        private static float ResolveAupDistanceMeters(Transform fromTransform, Transform toTransform)
        {
            if (fromTransform == null || toTransform == null)
                return 0f;

            Vector3 visualDelta = toTransform.position - fromTransform.position;
            float distanceSq = math.lengthsq((float3)visualDelta);
            return math.sqrt(math.max(0f, distanceSq));
        }

        private static float3 ResolveAupDirectionBetweenTransforms(Transform fromTransform, Transform toTransform)
        {
            if (fromTransform == null || toTransform == null)
                return float3.zero;

            Vector3 visualDelta = toTransform.position - fromTransform.position;
            return NormalizeVisualRsqrt((float3)visualDelta, float3.zero);
        }

        private void DisableLegacySunFlare()
        {
            if (_sunLensFlare == null)
                return;

            _sunLensFlare.intensity = 0f;
            _sunLensFlare.scale = 0f;
            if (_sunLensFlare.enabled)
                _sunLensFlare.enabled = false;
        }

        // ─────────────────────────────────────────────
        // ECLIPSE BACKLIGHT
        // ─────────────────────────────────────────────

        private void CalculateEclipseBacklight()
        {
            _currentBacklitFactor = 0f;

            if (!TryResolveAegirSkyDirection(out float3 playerToGiant) ||
                !IsAegirAboveEclipseHorizon(playerToGiant))
            {
                return;
            }

            float3 playerToSun = _resolvedSunDirection;

            float alignment = math.dot(playerToSun, playerToGiant);

            if (alignment > backlitAlignmentSoftStart)
            {
                float range = math.max(
                    backlitAlignmentFullStart - backlitAlignmentSoftStart, 0.001f);
                float t = math.saturate(
                    (alignment - backlitAlignmentSoftStart) * math.rcp(range));
                t = SmoothStep01(t);

                _currentBacklitFactor = math.saturate(t * backlitFactorMultiplier);
            }
        }

        // ─────────────────────────────────────────────
        // AEGIR MATERIAL
        // ─────────────────────────────────────────────

        private void UpdateAegirMaterial()
        {
            if (aegirRenderer == null) return;

            aegirRenderer.GetPropertyBlock(_aegirMPB);

            float3 toSun = _resolvedSunDirection;

            if (TryResolveAegirSkyDirection(out float3 playerToAegir))
            {
                float3 aegirToPlayer = -playerToAegir;
                _currentPhase = math.dot(toSun, aegirToPlayer);
            }
            else
            {
                _currentPhase = math.dot(toSun, new float3(0, 0, 1));
            }

            _aegirMPB.SetVector(_ID_FresnelSunDir, new Vector4(toSun.x, toSun.y, toSun.z, 0));
            _aegirMPB.SetFloat(_ID_BacklitIntensity, backlitIntensity);
            _aegirMPB.SetFloat(_ID_EquatorialSpeed, equatorialRotationSpeed);
            _aegirMPB.SetFloat(_ID_PolarMultiplier, polarRotationMultiplier);
            _aegirMPB.SetFloat(_ID_PlanetPhase, _currentPhase);
            _aegirMPB.SetFloat(_ID_StormEmission, stormEmissionIntensity * ResolveStormEmissionMultiplier());
            _aegirMPB.SetFloat(_ID_SunBacklitFactor, _currentBacklitFactor);
            _aegirMPB.SetFloat(_ID_GlobalRotation, _rotationPhase);
            _aegirMPB.SetFloat(_ID_GameTime, _gameTime);
            _aegirMPB.SetFloat(_ID_NightBlend, _currentBlend);
            _aegirMPB.SetFloat(_ID_AtmosphereTransmittanceWeight, _atmosphereTransmittanceWeight);
            _aegirMPB.SetFloat(_ID_AtmosphereInscatterWeight, _atmosphereInscatterWeight);

            _aegirMPB.SetColor(_ID_SkyColorZenith, _resolvedSkyZenith);
            _aegirMPB.SetColor(_ID_SkyColorHorizon, _resolvedSkyHorizon);
            _aegirMPB.SetColor(_ID_SkyColorNadir, _resolvedSkyNadir);

            if (_skyMaterial != null && _skyMaterial.HasProperty(_ID_WindDirection))
                _aegirMPB.SetVector(_ID_WindDirection, _skyMaterial.GetVector(_ID_WindDirection));

            aegirRenderer.SetPropertyBlock(_aegirMPB);

            CelestialEvents.RaisePlanetPhaseChanged(_currentPhase);
        }

        // ─────────────────────────────────────────────
        // PLANET-SHINE
        // ─────────────────────────────────────────────

        private void UpdateMoonMaterialOverrides()
        {
            if (_moonMPB == null)
                return;

            CelestialRuntimeSnapshot snapshot = _celestialRuntimeSnapshot;
            int moonWriteIndex = 0;
            for (int i = 0; i < _moonRenderers.Count; i++)
            {
                Renderer moonRenderer = _moonRenderers[i];
                if (moonRenderer == null)
                    continue;

                float phase01 = moonWriteIndex == 0 ? snapshot.Moon0Phase01 : snapshot.Moon1Phase01;
                moonRenderer.GetPropertyBlock(_moonMPB);
                _moonMPB.SetFloat(
                    _ID_AtmosphereTransmittanceWeight,
                    _atmosphereTransmittanceWeight * _moonAtmosphereTransmittanceMultiplier);
                _moonMPB.SetFloat(
                    _ID_AtmosphereInscatterWeight,
                    _atmosphereInscatterWeight * _moonAtmosphereInscatterMultiplier);
                _moonMPB.SetFloat(_ID_HectonMoonPhase01, phase01);
                _moonMPB.SetFloat(_ID_HectonMoonPhaseTextureIndex, ResolveMoonPhaseTextureIndex(phase01));
                moonRenderer.SetPropertyBlock(_moonMPB);
                moonWriteIndex++;
            }
        }

        private void UpdatePlanetShine()
        {
            if (_planetShineLight == null || !TryResolveAegirSkyDirection(out float3 playerToAegir))
                return;

            float3 aegirToPlayer = -playerToAegir;
            float3 aegirToSun = _resolvedSunDirection;

            float rawPhase = math.dot(aegirToSun, aegirToPlayer);

            float phaseFactor = math.saturate(
                (rawPhase - planetShineNewMoonThreshold) /
                math.max(1f - planetShineNewMoonThreshold, 0.01f));
            phaseFactor = phaseFactor * phaseFactor;

            float eclipseDim = 1f - _currentBacklitFactor;
            float intensity = phaseFactor * eclipseDim * planetShineMaxIntensity;

            _planetShineLight.transform.rotation = Quaternion.LookRotation(
                (Vector3)(-aegirToPlayer));
            _planetShineLight.intensity = intensity;
            _planetShineLight.color = planetShineColor;
        }

        private void UpdateMoonPhaseShadowVisibility()
        {
            if (!enableMoonPhaseShadowModulation || moonPhaseShadowStrength <= 0f)
            {
                _moonPhaseShadowVisibility = 1f;
                return;
            }

            float bestAlignment = 0f;
            for (int i = 0; i < _observerBodyCache.Count; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (!TryGetMoonDirection(body, out Vector3 moonDirection))
                    continue;

                float alignment = math.dot((float3)moonDirection, _resolvedSunDirection);
                if (alignment > bestAlignment)
                    bestAlignment = alignment;
            }

            float startDot = math.min(moonPhaseShadowStartDot, moonPhaseShadowFullDot);
            float fullDot = math.max(moonPhaseShadowStartDot, moonPhaseShadowFullDot);
            float phaseT = math.saturate((bestAlignment - startDot) * math.rcp(math.max(fullDot - startDot, 0.0001f)));
            phaseT = phaseT * phaseT * (3f - 2f * phaseT);
            _moonPhaseShadowVisibility = 1f - phaseT * math.saturate(moonPhaseShadowStrength);
        }

        // ─────────────────────────────────────────────
        // ECLIPSE DETECTION
        // ─────────────────────────────────────────────

        private void DetectEclipse()
        {
            if (!TryResolveSunOcclusion(out bool sunOccluded, out bool insideExitBand, out float penumbraFactor))
            {
                _penumbraFactor = 0f;
                ApplyEclipseStateBranchless(false, false);
                return;
            }

            _penumbraFactor = penumbraFactor;
            ApplyEclipseStateBranchless(sunOccluded, insideExitBand);
        }

        private void ApplyEclipseStateBranchless(bool sunOccluded, bool insideExitBand)
        {
            bool wasActive = _isEclipseActive;
            int active01 = wasActive ? 1 : 0;
            active01 = math.select(active01, 1, sunOccluded);
            bool shouldExit = wasActive && !sunOccluded && !insideExitBand;
            active01 = math.select(active01, 0, shouldExit);
            bool isActive = active01 != 0;
            _isEclipseActive = isActive;

            if (!wasActive && isActive)
            {
                CelestialEvents.RaiseEclipseStarted();
            }
            else if (wasActive && !isActive)
            {
                CelestialEvents.RaiseEclipseEnded();
            }
        }

        private void DetectLunarResonance()
        {
            bool active = false;
            float thresholdDegrees = Mathf.Max(0.01f, lunarResonanceAlignmentDegrees);
            float thresholdDot = FastCosRadians(math.radians(thresholdDegrees));

            for (int i = 0; i < _observerBodyCache.Count && !active; i++)
            {
                ObserverRelativeCelestialBody first = _observerBodyCache[i];
                if (!TryGetMoonDirection(first, out Vector3 firstDirection))
                    continue;

                for (int j = i + 1; j < _observerBodyCache.Count; j++)
                {
                    ObserverRelativeCelestialBody second = _observerBodyCache[j];
                    if (!TryGetMoonDirection(second, out Vector3 secondDirection))
                        continue;

                    if (Vector3.Dot(firstDirection, secondDirection) >= thresholdDot)
                    {
                        active = true;
                        break;
                    }
                }
            }

            _lunarResonanceActive = active;
            _lunarResonanceMultiplier = active ? Mathf.Max(1f, lunarResonanceBiolumMultiplier) : 1f;
        }

        private bool TryGetMoonDirection(ObserverRelativeCelestialBody body, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (body == null || body == aegirObserverRelativeBody)
                return false;

            Vector3 rawDirection = body.CurrentDirection;
            float sqrMagnitude = rawDirection.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f || !float.IsFinite(sqrMagnitude))
                return false;

            direction = rawDirection * math.rsqrt(sqrMagnitude);
            return true;
        }

        private bool TryResolveSunOcclusion(out bool sunOccluded, out bool insideExitBand, out float penumbraFactor)
        {
            sunOccluded = false;
            insideExitBand = false;
            penumbraFactor = 0f;
            bool hasOccluder = false;
            float3 toSun = _resolvedSunDirection;

            if (TryResolveAegirSkyDirection(out float3 toAegir) &&
                IsAegirAboveEclipseHorizon(toAegir))
            {
                hasOccluder = true;
                EvaluateSunOccluder(
                    toSun,
                    toAegir,
                    GetAegirAngularRadiusDegrees(),
                    ref sunOccluded,
                    ref insideExitBand,
                    ref penumbraFactor);
            }

            for (int i = 0; i < _observerBodyCache.Count; i++)
            {
                ObserverRelativeCelestialBody body = _observerBodyCache[i];
                if (body == null || body == aegirObserverRelativeBody)
                    continue;

                Vector3 bodyDirectionManaged = body.CurrentDirection;
                float sqrMagnitude = bodyDirectionManaged.sqrMagnitude;
                if (sqrMagnitude <= 0.0001f || !float.IsFinite(sqrMagnitude))
                    continue;

                hasOccluder = true;
                float3 bodyDirection = (float3)bodyDirectionManaged * math.rsqrt(sqrMagnitude);
                float angularRadius = math.max(body.AngularDiameterDegrees * 0.5f, 0.01f);
                EvaluateSunOccluder(
                    toSun,
                    bodyDirection,
                    angularRadius,
                    ref sunOccluded,
                    ref insideExitBand,
                    ref penumbraFactor);
            }

            return hasOccluder;
        }

        private bool IsAegirAboveEclipseHorizon(float3 toAegir)
        {
            return math.isfinite(toAegir.y) && toAegir.y > eclipseAegirHorizonCullThreshold;
        }

        private void EvaluateSunOccluder(
            float3 toSun,
            float3 toOccluder,
            float angularRadius,
            ref bool sunOccluded,
            ref bool insideExitBand,
            ref float penumbraFactor)
        {
            float sunRadius = math.max(0.001f, sunAngularRadiusDegrees);
            float occluderRadius = math.max(0.001f, angularRadius);
            float dotSunOccluder = math.clamp(math.dot(toSun, toOccluder), -1f, 1f);
            float overlap01 = ComputeCheapPenumbraOverlapFromDot(dotSunOccluder, sunRadius, occluderRadius);
            penumbraFactor = math.max(penumbraFactor, overlap01);

            if (overlap01 >= math.clamp(eclipseEventStartPenumbraThreshold, 0.01f, 1f))
                sunOccluded = true;

            float exitAngle = sunRadius + occluderRadius + math.max(0f, eclipseHysteresisMargin);
            float exitDot = FastCosRadians(math.radians(math.max(0f, exitAngle)));
            if (dotSunOccluder >= exitDot)
                insideExitBand = true;
        }

        // ─────────────────────────────────────────────
        // UTILITY
        // ─────────────────────────────────────────────

        private static float ComputeCheapPenumbraOverlapFromDot(float dotSunOccluder, float sunRadiusDeg, float occluderRadiusDeg)
        {
            float sunRadius = math.max(0.0001f, sunRadiusDeg);
            float occluderRadius = math.max(0.0001f, occluderRadiusDeg);
            float thresholdEnter = FastCosRadians(math.radians(sunRadius + occluderRadius));
            float thresholdFull = FastCosRadians(math.radians(math.abs(occluderRadius - sunRadius)));
            float t = (math.clamp(dotSunOccluder, -1f, 1f) - thresholdEnter) *
                      math.rcp(math.max(0.0001f, thresholdFull - thresholdEnter));
            return SmoothStep01(t);
        }

        private static float ComputeCheapPenumbraOverlapFromSeparation(float sunRadiusDeg, float occluderRadiusDeg, float separationDeg)
        {
            float dotSunOccluder = FastCosRadians(math.radians(math.max(0f, separationDeg)));
            return ComputeCheapPenumbraOverlapFromDot(dotSunOccluder, sunRadiusDeg, occluderRadiusDeg);
        }

        private static float SmoothStep01(float t)
        {
            t = math.saturate(t);
            return t * t * (3f - 2f * t);
        }

        private float ResolveTimeOfDay01()
        {
            return _atmosphereManager != null
                ? Mathf.Repeat(_atmosphereManager.TimeOfDay, 1f)
                : Mathf.Repeat(_rotationPhase, 1f);
        }

        private uint ResolveCelestialBlackBoxRuntimeFlags()
        {
            uint flags = _celestialRuntimeSnapshot.Flags;
            if (IsLowTierLockedSunMode())
                flags |= CelestialBlackBoxFlagLowTierLocked;

            if (_stormCloudDensity01 > 0.001f)
                flags |= CelestialBlackBoxFlagStorm;

            return flags;
        }

        private void WriteCelestialBlackBoxTelemetry(float timeOfDay01, float eclipseState01, uint extraFlags, float depthMeters)
        {
            if (!Application.isPlaying)
                return;

            EnsureCelestialBlackBoxBuffer();

            float lightningFlash01 = math.max(_lightningFlash01, math.max(0f, _lastUploadedLightningFlash01));
            float aegirY = (_celestialRuntimeSnapshot.Flags & (uint)CelestialRuntimeFlags.Valid) != 0u
                ? _celestialRuntimeSnapshot.GasGiantDirection.y
                : 0f;

            CelestialBlackBoxEntry entry = new CelestialBlackBoxEntry
            {
                FrameIndex = unchecked((uint)Time.frameCount),
                Sequence = _celestialRuntimeSequence,
                Flags = _celestialRuntimeSnapshot.Flags | extraFlags,
                TimeOfDay01 = math.saturate(timeOfDay01),
                EclipseState01 = math.saturate(eclipseState01),
                SunDirectionY = _resolvedSunDirection.y,
                AegirDirectionY = aegirY,
                StormCloudDensity01 = math.saturate(_stormCloudDensity01),
                LightningFlash01 = math.saturate(lightningFlash01),
                DepthMeters = math.max(0f, depthMeters)
            };

            _celestialBlackBox[_celestialBlackBoxCursor] = entry;
            _celestialBlackBoxCursor = (_celestialBlackBoxCursor + 1) % CelestialBlackBoxFrameCount;
            _celestialBlackBoxCount = math.min(_celestialBlackBoxCount + 1, CelestialBlackBoxFrameCount);

            if (!IsCelestialBlackBoxEntryFinite(in entry))
                DumpCelestialBlackBox();
        }

        private static bool IsCelestialBlackBoxEntryFinite(in CelestialBlackBoxEntry entry)
        {
            return math.isfinite(entry.TimeOfDay01) &&
                   math.isfinite(entry.EclipseState01) &&
                   math.isfinite(entry.SunDirectionY) &&
                   math.isfinite(entry.AegirDirectionY) &&
                   math.isfinite(entry.StormCloudDensity01) &&
                   math.isfinite(entry.LightningFlash01) &&
                   math.isfinite(entry.DepthMeters);
        }

        private void DumpCelestialBlackBox()
        {
            if (_celestialBlackBoxDumped || !_celestialBlackBox.IsCreated)
                return;

            _celestialBlackBoxDumped = true;
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
                if (string.IsNullOrEmpty(projectRoot))
                    return;

                string dumpPath = Path.Combine(projectRoot, CelestialBlackBoxDumpRelativePath);
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(0x43454C42u);
                    writer.Write(CelestialBlackBoxFrameCount);
                    writer.Write(_celestialBlackBoxCount);
                    writer.Write(_celestialBlackBoxCursor);

                    int start = _celestialBlackBoxCount == CelestialBlackBoxFrameCount
                        ? _celestialBlackBoxCursor
                        : 0;

                    for (int i = 0; i < _celestialBlackBoxCount; i++)
                    {
                        CelestialBlackBoxEntry entry = _celestialBlackBox[(start + i) % CelestialBlackBoxFrameCount];
                        writer.Write(entry.FrameIndex);
                        writer.Write(entry.Sequence);
                        writer.Write(entry.Flags);
                        writer.Write(entry.TimeOfDay01);
                        writer.Write(entry.EclipseState01);
                        writer.Write(entry.SunDirectionY);
                        writer.Write(entry.AegirDirectionY);
                        writer.Write(entry.StormCloudDensity01);
                        writer.Write(entry.LightningFlash01);
                        writer.Write(entry.DepthMeters);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogError(exception);
            }
        }

        // ─────────────────────────────────────────────
        // PUBLIC API
        // ─────────────────────────────────────────────

        internal void SetSurfaceWeatherOverride(
            float cloudDensityThreshold,
            float cloudSoftness,
            float cloudSpeedMultiplier,
            Vector2 windDirection,
            Color fogColor,
            float fogDensity,
            Color ambientColor,
            float sunMultiplier,
            float starVisibilityMultiplier,
            float stormEmissionMultiplier,
            float skyLuminanceMultiplier,
            float sunDiscMultiplier,
            float sunScatterMultiplier,
            Color cloudLitColor,
            Color cloudShadowColor,
            Color sunsetCloudColor,
            Color nightCloudColor)
        {
            CacheSkyWeatherDefaults();

            Vector2 resolvedDirection = ResolveWeatherWindDirection(windDirection, new Vector2(_defaultWindDirection.x, _defaultWindDirection.y));

            _surfaceWeatherOverrideActive = true;
            _surfaceWeatherFogOverrideActive = true;
            _surfaceWeatherCloudDensityThreshold = Mathf.Clamp01(cloudDensityThreshold);
            _surfaceWeatherCloudSoftness = Mathf.Clamp(cloudSoftness, 0.01f, 0.5f);
            _surfaceWeatherCloudSpeedMultiplier = Mathf.Max(0f, cloudSpeedMultiplier);
            _surfaceWeatherWindDirection = new Vector4(resolvedDirection.x, resolvedDirection.y, 0f, 0f);
            _surfaceWeatherFogColor = fogColor;
            _surfaceWeatherFogColor.a = 1f;
            _surfaceWeatherFogDensity = Mathf.Max(0.0001f, fogDensity);
            _surfaceWeatherAmbientColor = ambientColor;
            _surfaceWeatherAmbientColor.a = 1f;
            _surfaceWeatherSunMultiplier = Mathf.Max(0f, sunMultiplier);
            _surfaceWeatherStarVisibilityMultiplier = Mathf.Clamp01(starVisibilityMultiplier);
            _surfaceWeatherStormEmissionMultiplier = Mathf.Max(0f, stormEmissionMultiplier);
            _surfaceWeatherSkyLuminanceMultiplier = Mathf.Max(0f, skyLuminanceMultiplier);
            _surfaceWeatherSunDiscMultiplier = Mathf.Max(0f, sunDiscMultiplier);
            _surfaceWeatherSunScatterMultiplier = Mathf.Max(0f, sunScatterMultiplier);
            _surfaceWeatherCloudLitColor = cloudLitColor;
            _surfaceWeatherCloudShadowColor = cloudShadowColor;
            _surfaceWeatherSunsetCloudColor = sunsetCloudColor;
            _surfaceWeatherNightCloudColor = nightCloudColor;
        }

        internal void ClearSurfaceWeatherOverride()
        {
            _surfaceWeatherOverrideActive = false;
            _surfaceWeatherFogOverrideActive = false;
            _surfaceWeatherCloudSpeedMultiplier = 1f;
            _surfaceWeatherStarVisibilityMultiplier = 1f;
            _surfaceWeatherStormEmissionMultiplier = 1f;
            _surfaceWeatherSkyLuminanceMultiplier = 1f;
            _surfaceWeatherSunDiscMultiplier = 1f;
            _surfaceWeatherSunScatterMultiplier = 1f;
            _surfaceWeatherSunMultiplier = 1f;
        }

        public float SunElevation => _currentSunAngle;
        public float DayNightBlend => _currentBlend;
        public float PlanetPhase => _currentPhase;
        public bool IsEclipseActive => _isEclipseActive;
        public float EclipseBacklitFactor => _currentBacklitFactor;
        public float StarIntensity => _currentStarIntensity;
        public float ResolvedStarMapSeed => _resolvedStarMapSeed;
        public Vector3 ResolvedSunDirection => (Vector3)_resolvedSunDirection;
        public float SunOcclusionFactor => _smoothedOcclusionFactor;
        public float PenumbraFactor => _penumbraFactor;
        public CelestialRuntimeSnapshot RuntimeSnapshot => _celestialRuntimeSnapshot;
        public float TideHeightMeters => _celestialRuntimeSnapshot.TideHeightMeters;
        public Vector3 TidePullVector => ToVector3(_celestialRuntimeSnapshot.TidePullVector);
        public bool IsLunarResonanceActive => _lunarResonanceActive;
        public float LunarResonanceBiolumMultiplier => _lunarResonanceMultiplier;
        public float AtmosphereDensity => _currentAtmosphereDensity;
        public bool IsAegirFixedDirectionLocked =>
            aegirObserverRelativeBody != null && aegirObserverRelativeBody.UsesFixedDirection;
        public float RotationTimer => _rotationTimer;
        public float GameTime => _gameTime;
        public float DebugCelestialTimeScale => _debugCelestialTimeScale;

        public static float EvaluatePenumbraOverlapForSmoke(float sunRadiusDeg, float occluderRadiusDeg, float separationDeg)
        {
            return ComputeCheapPenumbraOverlapFromSeparation(sunRadiusDeg, occluderRadiusDeg, separationDeg);
        }

        private static Vector2 ResolveWeatherWindDirection(Vector2 requestedDirection, Vector2 fallbackDirection)
        {
            float2 selected = requestedDirection.sqrMagnitude > 0.0001f
                ? new float2(requestedDirection.x, requestedDirection.y)
                : new float2(fallbackDirection.x, fallbackDirection.y);
            float lengthSq = math.lengthsq(selected);
            if (lengthSq <= 0.0001f || !math.isfinite(lengthSq))
                return Vector2.right;

            float2 resolved = selected * math.rsqrt(lengthSq);
            return new Vector2(resolved.x, resolved.y);
        }

        public bool TryGetAegirSkyDirection(out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!TryResolveAegirSkyDirection(out float3 resolvedDirection))
                return false;

            direction = new Vector3(resolvedDirection.x, resolvedDirection.y, resolvedDirection.z);
            return true;
        }

        public void SetOrbitalAngle(float angleDegrees)
        {
            _accumulatedOrbitalAngle = angleDegrees % 360f;
        }

        public void SetDebugCelestialTimeScale(float multiplier)
        {
            _debugCelestialTimeScale = Mathf.Max(1f, multiplier);
        }

        // ─────────────────────────────────────────────
        // EDITOR GIZMOS
        // ─────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (aegirTransform == null || !TryResolvePlayerRuntimePosition(out Vector3 playerRuntimePosition)) return;

            float3 aegirPos  = (float3)aegirTransform.position;
            float3 playerPos = new float3(playerRuntimePosition.x, playerRuntimePosition.y, playerRuntimePosition.z);

            Gizmos.color = planetShineColor;
            Gizmos.DrawLine((Vector3)aegirPos, (Vector3)playerPos);

            Gizmos.color = _isEclipseActive ? Color.red : Color.yellow;
            float3 toSun = _resolvedSunDirection;
            Gizmos.DrawRay((Vector3)playerPos, (Vector3)(toSun * 50f));

            float3 toAegir = ResolveAupDirectionBetweenTransforms(playerTransform, aegirTransform);
            Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
            Gizmos.DrawRay((Vector3)playerPos,
                (Vector3)(toAegir * ResolveAupDistanceMeters(playerTransform, aegirTransform)));

            float gizmoRadius = GetAegirWorldRadius();
            if (_currentBacklitFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.8f, 0.2f, _currentBacklitFactor);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.05f);
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius);

            if (_smoothedOcclusionFactor > 0.01f)
            {
                Gizmos.color = new Color(1f, 0.2f, 0f, _smoothedOcclusionFactor * 0.6f);
                Gizmos.DrawWireSphere((Vector3)aegirPos, gizmoRadius * 1.02f);
            }

            if (sunVisualTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(sunVisualTransform.position, 500f);
            }

            if (_skyMaterial != null)
            {
                Gizmos.color = new Color(0.6f, 0.5f, 0.8f, 0.7f);
                Gizmos.DrawRay((Vector3)playerPos,
                    (Vector3)(toAegir * 30f));
            }
        }
#endif
    }
}
