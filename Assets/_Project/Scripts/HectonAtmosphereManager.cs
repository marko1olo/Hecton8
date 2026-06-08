// ══════════════════════════════════════════════════════════════════
// HectonAtmosphereManager.cs  v2.1 (OPTIMIZATION PASS)
// Orbitalnaya model solntsa + vremya sutok + zatmeniya + _SunDirection
//
// ═══════════════════════════════════════════════════════════════
// v2.1 CHANGES (OPTIMIZATION):
// ═══════════════════════════════════════════════════════════════
//
//   [OPT] Shader property dirty-write batching in RotateSun()
//     • Caches _cachedShaderSunDirection (float3, stack)
//     • Only calls Shader.SetGlobalVector() if changed
//     • Impact: eliminates redundant GPU command buffer writes
//
//   [OPT] Dictionary<int, AtmosphereProfile> for biome lookup
//     • HandleBiomeChanged() from O(N) linear search → O(1) TryGetValue
//     • Built once in Awake() from _biomeOverrides[]
//     • Impact: O(N) one-time cost, O(1) per biome change
//
// ═══════════════════════════════════════════════════════════════
// v4.3 BASELINE (PRESERVED):
// ═══════════════════════════════════════════════════════════════
//
//   [FIX] DefaultExecutionOrder(-6000):
//     Guarantees AtmosphereManager.OnEnable() fires BEFORE
//     UnderwaterVisuals(-4000) and CelestialEngine(-3000).
//     This means AtmosphereManager registers with GameTickManager FIRST
//     → ticks FIRST → ProfileSunIntensity and ComputedHorizonFade
//     are FRESH when UnderwaterVisuals reads them.
//
//     EXECUTION CHAIN (deterministic):
//       1. AtmosphereManager.Tick()  → compute profile, horizon, _SunDirection
//       2. UnderwaterVisuals.Tick()  → write sunLight.intensity (sole authority)
//       3. CelestialEngine.Tick()    → multiply sunLight.intensity by visibility
//
// ═══════════════════════════════════════════════════════════════
// v4.2 DETAILS:
//   ✓ [ExecuteAlways] for Scene View preview
//   ✓ sunLight.intensity NEVER WRITTEN (read-only)
//   ✓ ProfileSunIntensity = profile × transition
//   ✓ ComputedHorizonFade = smoothstep by SunElevation
//   ✓ Biome atmosphere overrides
//   ✓ Eclipse timer + state machine
// ══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Celestial;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.World;
using Hecton.Localization;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Atmosphere
{
    /// <summary>
    /// Single write facade for global atmosphere render settings owned by runtime visual systems.
    /// </summary>
    public static class AtmosphereDirector
    {
        public static Material Skybox => RenderSettings.skybox;

        public static bool IsSkybox(Material material)
        {
            return ReferenceEquals(RenderSettings.skybox, material);
        }

        public static bool SetSkybox(Material material)
        {
            if (ReferenceEquals(RenderSettings.skybox, material))
                return false;

            RenderSettings.skybox = material;
            return true;
        }
    }

    /// <summary>
    /// Main-thread listener for deferred atmosphere state changes.
    /// </summary>
    public interface IAtmosphereStateEventListener
    {
        /// <summary>Called when the atmosphere state changes.</summary>
        void OnAtmosphereStateChanged(EnvironmentState state);
    }

    /// <summary>
    /// Queue-backed atmosphere state event lane.
    /// </summary>
    public static class AtmosphereEvents
    {
        private const int ExpectedPendingStateEventCapacity = 8;
        private const int ListenerCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)LocHash.Compute("AtmosphereEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)LocHash.Compute("AtmosphereEvents.ListenerException"));
        private static readonly uint _listenerContextHash = unchecked((uint)LocHash.Compute("AtmosphereEvents.Listeners"));

        private struct ListenerSlot
        {
            public IAtmosphereStateEventListener Listener;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - bounded managed atmosphere state listeners, no interface array hot dispatch - owner: AtmosphereEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener additions deferred while dispatching atmosphere state events - owner: AtmosphereEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener removals deferred while dispatching atmosphere state events - owner: AtmosphereEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<EnvironmentState> _pendingStates;
        private static NativeQueue<EnvironmentState> _nextFrameStates;
        private static int _pendingStatesSentinelId;
        private static int _nextFrameStatesSentinelId;
        private static int _listenerCount;
        private static int _pendingStateCount;
        private static int _nextFrameStateCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued atmosphere state changes awaiting dispatch.
        /// </summary>
        public static int PendingCount => _pendingStateCount + _nextFrameStateCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            for (int i = 0; i < _deferredRegisterCount; i++)
                _deferredRegisterListeners[i].Clear();

            for (int i = 0; i < _deferredUnregisterCount; i++)
                _deferredUnregisterListeners[i].Clear();

            _listenerCount = 0;
            _pendingStateCount = 0;
            _nextFrameStateCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a main-thread atmosphere listener.
        /// </summary>
        public static void Register(IAtmosphereStateEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters a main-thread atmosphere listener.
        /// </summary>
        public static void Unregister(IAtmosphereStateEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            TryUnregisterImmediate(listener);
        }

        /// <summary>
        /// Queues a new atmosphere state change.
        /// </summary>
        public static bool TryRaiseStateChanged(EnvironmentState state)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingStateCount + _nextFrameStateCount >= ExpectedPendingStateEventCapacity)
                return false;

            if (_isDispatching)
            {
                _nextFrameStates.Enqueue(state);
                _nextFrameStateCount++;
                return true;
            }

            _pendingStates.Enqueue(state);
            _pendingStateCount++;
            return true;
        }

        [Obsolete("Atmosphere state producers must use TryRaiseStateChanged and handle bounded enqueue failure.", true)]
        public static void RaiseStateChanged(EnvironmentState state)
        {
            TryRaiseStateChanged(state);
        }

        /// <summary>
        /// Flushes queued atmosphere state changes on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingStates.IsCreated)
                return;

            PromoteNextFrameStatesIfFrontEmpty();
            int scanBudget = _pendingStateCount > 0 ? _pendingStateCount : ExpectedPendingStateEventCapacity;
            while (scanBudget > 0 && !_pendingStates.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingStates.TryDequeue(out EnvironmentState state))
                {
                    _pendingStateCount = 0;
                    return;
                }

                if (_pendingStateCount > 0)
                    _pendingStateCount--;
                scanBudget--;
                int listenerCount = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = listenerCount - 1; i >= 0; i--)
                    {
                        IAtmosphereStateEventListener listener = _listeners[i].Listener;
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, state);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }
            }

            if (_pendingStates.IsEmpty())
            {
                _pendingStateCount = 0;
                PromoteNextFrameStatesIfFrontEmpty();
            }
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingStates.IsCreated)
                {
                    _pendingStates = new NativeQueue<EnvironmentState>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EnvironmentState>[8] - deferred atmosphere state lane flushed by SystemDispatcher - owner: AtmosphereEvents
                    RegisterNativeQueue(ref _pendingStates, ExpectedPendingStateEventCapacity, nameof(_pendingStates), out _pendingStatesSentinelId);
                    PrewarmQueue(ref _pendingStates, ExpectedPendingStateEventCapacity);
                }

                if (!_nextFrameStates.IsCreated)
                {
                    _nextFrameStates = new NativeQueue<EnvironmentState>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<EnvironmentState>[8] - next-frame atmosphere state lane prevents same-frame reentrant dispatch - owner: AtmosphereEvents
                    RegisterNativeQueue(ref _nextFrameStates, ExpectedPendingStateEventCapacity, nameof(_nextFrameStates), out _nextFrameStatesSentinelId);
                    PrewarmQueue(ref _nextFrameStates, ExpectedPendingStateEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingStateCount = 0;
                _nextFrameStateCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(AtmosphereEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingStates, ref _pendingStatesSentinelId);
            ReleaseNativeQueue(ref _nextFrameStates, ref _nextFrameStatesSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
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

        private static void PromoteNextFrameStatesIfFrontEmpty()
        {
            if (!_pendingStates.IsCreated ||
                !_nextFrameStates.IsCreated ||
                !_pendingStates.IsEmpty() ||
                _nextFrameStateCount <= 0)
            {
                return;
            }

            NativeQueue<EnvironmentState> swap = _pendingStates;
            _pendingStates = _nextFrameStates;
            _nextFrameStates = swap;
            int sentinelIdSwap = _pendingStatesSentinelId;
            _pendingStatesSentinelId = _nextFrameStatesSentinelId;
            _nextFrameStatesSentinelId = sentinelIdSwap;
            _pendingStateCount = _nextFrameStateCount;
            _nextFrameStateCount = 0;
        }

        private static void DispatchToListener(IAtmosphereStateEventListener listener, EnvironmentState state)
        {
            try
            {
                listener.OnAtmosphereStateChanged(state);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IAtmosphereStateEventListener listener)
        {
            if (ContainsImmediate(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IAtmosphereStateEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!ContainsImmediate(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IAtmosphereStateEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IAtmosphereStateEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IAtmosphereStateEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAtmosphereStateEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IAtmosphereStateEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    TryUnregisterImmediate(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAtmosphereStateEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IAtmosphereStateEventListener listener)
        {
            if (ContainsImmediate(listener))
                return;

            if (_listenerCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IAtmosphereStateEventListener listener)
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

        private static bool ContainsImmediate(IAtmosphereStateEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _listenerRejectedWarningHash,
                _listenerContextHash,
                Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _listenerExceptionWarningHash,
                _listenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton/Atmosphere Manager")]
    [DefaultExecutionOrder(-6000)]  // v4.3: MUST tick before UnderwaterVisuals(-4000)
    [ExecuteAlways]
    public class HectonAtmosphereManager : MonoBehaviour, ISlowTickable, ILateFrameTickable, IBiomeMatrixEventListener, IMapMagicBiomeEventListener, IAtmosphereRenderSettingsBridge, IAtmosphereReadModel, IGlobalRegistryHotSwapListener
    {
        private const float VisualEnterUnderwaterDepth = 0.01f;
        private const float VisualExitUnderwaterDepth = 0.005f;
        private const float DefaultWaterSurfaceY = 14.02f;
        private const int AbyssAtmospherePresentationDtoStrideBytes = 48;

        Material IAtmosphereRenderSettingsBridge.Skybox => AtmosphereDirector.Skybox;

        bool IAtmosphereRenderSettingsBridge.SetSkybox(Material material)
        {
            return AtmosphereDirector.SetSkybox(material);
        }

        #region ══════════ AtmosphereSnapshot ══════════

        private struct AtmosphereSnapshot
        {
            public Color  fogColor;
            public float  fogDensity;
            public float  fogAttenuationDistance;
            public float  skyExposure;
            public Color  ambientColor;
            public float  sunIntensity;
            public float  temperature;
            public float  radiation;

            public static AtmosphereSnapshot Default => new AtmosphereSnapshot
            {
                fogColor      = new Color(0.75f, 0.78f, 0.85f, 1f),
                fogDensity    = 0.008f,
                fogAttenuationDistance = 100f,
                skyExposure  = 1f,
                ambientColor = new Color(0.45f, 0.45f, 0.55f, 1f),
                sunIntensity = 1f,
                temperature  = 20f,
                radiation    = 0f
            };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot FromProfile(AtmosphereProfile p)
            {
                return new AtmosphereSnapshot
                {
                    fogColor      = p.fogColor,
                    fogDensity    = p.fogDensity,
                    fogAttenuationDistance = math.max(0.001f, p.fogAttenuationDistanceMeters),
                    skyExposure  = p.skyExposure,
                    ambientColor = p.ambientColor,
                    sunIntensity = p.sunIntensity,
                    temperature  = p.temperature,
                    radiation    = p.radiation
                };
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static AtmosphereSnapshot Lerp(
                in AtmosphereSnapshot from,
                in AtmosphereSnapshot to,
                float t)
            {
                return new AtmosphereSnapshot
                {
                    fogColor      = Color.Lerp(from.fogColor, to.fogColor, t),
                    fogDensity    = math.lerp(from.fogDensity, to.fogDensity, t),
                    fogAttenuationDistance = math.lerp(from.fogAttenuationDistance, to.fogAttenuationDistance, t),
                    skyExposure  = math.lerp(from.skyExposure,  to.skyExposure,  t),
                    ambientColor = Color.Lerp(from.ambientColor, to.ambientColor, t),
                    sunIntensity = math.lerp(from.sunIntensity, to.sunIntensity, t),
                    temperature  = math.lerp(from.temperature,  to.temperature,  t),
                    radiation    = math.lerp(from.radiation,    to.radiation,    t)
                };
            }
        }

        #endregion

        #region ══════════ Singleton ══════════

        #endregion

        #region ══════════ Events ══════════

        #endregion

        #region ══════════ Shader IDs ══════════

        private static readonly int _shaderID_SunDirection =
            Shader.PropertyToID("_SunDirection");
        private static readonly int _shaderID_HectonTimeOfDay01 =
            Shader.PropertyToID("_HectonTimeOfDay01");
        private static readonly int _shaderID_HectonNightFactor =
            Shader.PropertyToID("_HectonNightFactor");
        private static readonly int _shaderID_SargassumBiolumPhaseMultiplier =
            Shader.PropertyToID("_SargassumBiolumPhaseMultiplier");
        private static readonly int _shaderID_FinalGiantAbyssLight =
            Shader.PropertyToID("_FinalGiantAbyssLight");
        private static readonly int _shaderID_AegirDirection =
            Shader.PropertyToID("_AegirDirection");
        private static readonly int _shaderID_H8AbyssAbsorptionColor =
            Shader.PropertyToID("_H8AbyssAbsorptionColor");
        private static readonly int _shaderID_H8AbyssAtmosphereParams =
            Shader.PropertyToID("_H8AbyssAtmosphereParams");
        private static readonly int _shaderID_CausticOffset =
            Shader.PropertyToID("_CausticOffset");

        #endregion

        #region ══════════ Inspector ══════════

        [Header("═══ Sun & Time Cycle ═══")]
        [Tooltip("Directional sun controlled by the atmosphere cycle. Falls back to RenderSettings.sun during play-mode validation.")]
        [SerializeField] private Light _sunLight;

        [SerializeField, Min(1f)]
        private float _cycleDuration = 3600f;

        [SerializeField, Range(0f, 1f)]
        private float _initialTimeOfDay = 0.25f;

        [SerializeField, Range(0f, 360f)]
        private float _sunOrbitalYAngle = 170f;

        [SerializeField, Range(0f, 90f)]
        private float _orbitalInclination = 23.5f;

        [SerializeField, Range(1f, 30f)]
        private float _nightThresholdAngle = 10f;

        [Tooltip("Angular zone below horizon for smooth sun intensity fade.\n" +
                 "At dot ∈ [0, -sin(fadeAngle)] intensity smoothly → 0.")]
        [SerializeField, Range(1f, 30f)]
        private float _sunHorizonFadeAngle = 10f;
        [Tooltip("Binds an authored striped directional-light cookie for Aegir ring banding. Runtime cookie synthesis is forbidden.")]
        [SerializeField] private bool _useAegirRingShadowCookie = true;
        [SerializeField] private Texture2D _authoredAegirRingShadowCookie;

        [Header("═══ Atmosphere Profiles ═══")]
        [SerializeField] private AtmosphereProfile _profileDay;
        [SerializeField] private AtmosphereProfile _profileNight;
        [SerializeField] private AtmosphereProfile _profileUnderwater;
        [SerializeField] private AtmosphereProfile _profileEclipse;

        [Header("═══ Transition ═══")]
        [SerializeField, Range(0.1f, 5f)]
        private float _transitionSpeed = 1.5f;

        [Header("═══ Underwater Detection ═══")]
        [SerializeField] private Transform _playerTransform;
        [SerializeField] private float _waterSurfaceY = DefaultWaterSurfaceY;
        [SerializeField] private bool _useAutoUnderwaterDetection = true;

        [Header("Giant Abyss Light")]
        [Tooltip("Linearized surface-color source used before depth absorption attenuates Aegir's planet-shine in water.")]
        [SerializeField, ColorUsage(false, true)] private Color _giantAbyssSurfaceLightColor = new Color(0.48f, 0.72f, 1f, 1f);
        [Tooltip("RGB absorption coefficients per meter for exp(-depthMeters * sigmaRgbPerMeter).")]
        [SerializeField] private Vector3 _giantAbyssSigmaRgbPerMeter = new Vector3(0.0035f, 0.0012f, 0.00055f);
        [Tooltip("Scalar applied to Aegir's phase-lit planet-shine before it is published to shaders.")]
        [SerializeField, Range(0f, 2f)] private float _giantAbyssLightIntensity = 0.35f;
        [Tooltip("Abyssal biolume tint added as a low-strength fill so deep water never collapses to pure black.")]
        [SerializeField, ColorUsage(false, true)] private Color _giantAbyssBiolumeColor = new Color(0f, 0.38f, 0.55f, 1f);
        [Tooltip("Maximum biolume fill added to the final giant abyss light at depth.")]
        [SerializeField, Range(0f, 0.25f)] private float _giantAbyssBiolumeIntensity = 0.055f;

        [Header("Abyss Atmosphere Presentation")]
        [Tooltip("Depth where abyss absorption begins affecting fog and readability globals.")]
        [SerializeField, Min(0f)] private float _abyssAbsorptionStartDepthMeters = 18f;
        [Tooltip("Depth where abyss absorption reaches full visual weight.")]
        [SerializeField, Min(1f)] private float _abyssAbsorptionFullDepthMeters = 420f;
        [Tooltip("Minimum deep-water luminance floor. Prevents black crush without lifting surface lighting.")]
        [SerializeField, Range(0f, 0.02f)] private float _abyssMinimumReadableLuminance = 0.0025f;
        [Tooltip("Extra noir fog density multiplier applied at full abyss depth.")]
        [SerializeField, Range(0f, 2f)] private float _abyssFogDensityBoost = 0.38f;
        [Tooltip("Seconds used to smooth procedural biome fog influence after hysteresis commits.")]
        [SerializeField, Range(0.1f, 20f)] private float _biomeInfluenceBlendSeconds = 7.5f;
        [Tooltip("Wrapped presentation-only caustic scroll speed in meters per second.")]
        [SerializeField, Range(0f, 2f)] private float _abyssCausticScrollMetersPerSecond = 0.11f;

        [Header("═══ Vertical Runtime ═══")]
        [SerializeField] private BiomeMatrixDirector _biomeMatrixDirector;
        [SerializeField] private WorldProceduralFieldSampler _proceduralFieldSampler;
        [SerializeField, Min(0.05f)] private float _biomeInfluenceRefreshInterval = 0.35f;
        [SerializeField, Min(0f)] private float _biomeInfluenceTransitionHysteresisMeters = 10f;

        [Header("═══ Biome Overrides ═══")]
        [SerializeField] private BiomeAtmosphereOverride[] _biomeOverrides;

        #endregion

        #region ══════════ Runtime State ══════════

        private EnvironmentState _currentState = EnvironmentState.SURFACE_DAY;

        private float _cycleTimer;
        private double _elapsedCycleTimeSeconds;
        private float _sunAngleDegrees;
        private float _sunElevationDot;

        private bool  _eclipseActive;
        private float _eclipseRemainingTime;

        private bool _underwaterExternalFlag;
        private bool _autoUnderwaterState;
        private HectonPlayerMovement _playerMovement;
        private Transform _playerCameraTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonCelestialEngine _cachedCelestialEngine;
        private bool _renderSettingsSunFallbackChecked;

        private AtmosphereSnapshot _transitionOrigin;
        private AtmosphereSnapshot _currentValues;
        private float              _transitionProgress;

        private bool _registeredToTickManager;
        private bool _registeredLateFrameTick;
        private bool _registeredAtmosphereRuntime;
        private bool _registeredHotSwapListener;
        private float _lastAtmosphereSlowTickTime;
        private float _atmosphereTimelineAccumulator;
        private int _nextAtmosphereTimelineWarningFrame;
        private const float AtmosphereTimelineStepSeconds = 0.1f;
        private const float AtmosphereTimelineClockMaxSeconds = 16777215f;
        private const int AtmosphereTimelineMaxStepsPerSlowTick = 5;
        private static readonly uint _AtmosphereTimelineBudgetWarningHash = unchecked((uint)LocHash.Compute("HectonAtmosphereManager.AtmosphereTimelineBudget"));
        private static readonly uint _AtmosphereTimelineContextHash = unchecked((uint)LocHash.Compute("HectonAtmosphereManager.SlowTick"));
        private static readonly uint _AbyssAtmosphereLayoutWarningHash = unchecked((uint)LocHash.Compute("HectonAtmosphereManager.AbyssAtmosphereLayout"));
        private static readonly uint _AbyssAtmosphereLayoutContextHash = unchecked((uint)LocHash.Compute("HectonAtmosphereManager.Awake"));
        private const double AtmosphereTimelineBudgetMilliseconds = 0.2d;
        private const int AtmosphereTimelineWarningCooldownFrames = 30;

        private AtmosphereProfile _activeBiomeProfile;
        private AtmosphereProfile _activeMatrixProfile;
        private WorldProceduralFieldSampler.BiomeInfluenceCell _currentBiomeInfluence;
        private HectonBiomeMatrixProfile _biomeInfluencePrimaryProfile;
        private HectonBiomeMatrixProfile _biomeInfluenceSecondaryProfile;
        private float _nextBiomeInfluenceRefreshTime = float.NegativeInfinity;
        private bool _hasBiomeInfluenceAtmosphere;
        private bool _hasSmoothedBiomeInfluenceAtmosphere;
        private AtmosphereSnapshot _smoothedBiomeInfluenceAtmosphere;
        private bool _hasStableBiomeInfluencePrimary;
        private bool _hasPendingBiomeInfluencePrimary;
        private byte _stableBiomeInfluencePrimaryId;
        private byte _pendingBiomeInfluencePrimaryId;
        private AbsoluteUniversePosition _pendingBiomeInfluencePrimaryAup;
        private int _currentBiomeID = -1;
        private bool _editorInitialized;
        private bool _editorPreviewDirty;

        private float _computedHorizonFade;
        private float _computedSunIntensity;

        /// <summary>Cached shader property values (for dirty-write batching).</summary>
        private float3 _cachedShaderSunDirection = new float3(0f, -1f, 0f);
        private float _cachedShaderTimeOfDay01 = -1f;
        private float _cachedShaderNightFactor = -1f;
        private float _cachedShaderSargassumBiolumPhaseMultiplier = float.NaN;
        private float4 _cachedFinalGiantAbyssLight = new float4(-1f, -1f, -1f, -1f);
        private float4 _cachedAegirDirection = new float4(0f, 0f, 0f, -999f);
        private AbyssAtmospherePresentationDTO _cachedAbyssAtmospherePresentation = CreateInvalidAbyssAtmospherePresentation();
        private bool _pendingSunPresentationDirty;
        private Vector3 _pendingSunForwardVector = Vector3.forward;
        private bool _pendingSunDirectionShaderDirty;
        private float3 _pendingShaderSunDirection = new float3(0f, 0f, 1f);
        private bool _pendingCycleShaderDirty;
        private float _pendingShaderTimeOfDay01;
        private float _pendingShaderNightFactor;
        private float _pendingShaderSargassumBiolumPhaseMultiplier = HectonVegetationConstants.SargassumBiolumPhaseMultiplier;
        private bool _pendingGiantAbyssLightDirty;
        private bool _pendingAbyssAtmospherePresentationDirty;
        private AbyssAtmospherePresentationDTO _pendingAbyssAtmospherePresentation;
        private Texture2D _aegirRingShadowCookie;

        /// <summary>Dictionary for O(1) biome profile lookup (instead of linear search).</summary>
        private Dictionary<int, AtmosphereProfile> _biomeProfileDict;

        #endregion

        #region ══════════ Biome Override Struct ══════════

        [Serializable]
        public struct BiomeAtmosphereOverride
        {
            public int biomeID;
            public AtmosphereProfile profile;
        }

        [StructLayout(LayoutKind.Explicit, Size = AbyssAtmospherePresentationDtoStrideBytes)]
        private struct AbyssAtmospherePresentationDTO
        {
            [FieldOffset(0)]
            public float4 AbsorptionColorAndDepthMask;
            [FieldOffset(16)]
            public float4 FogDensityQualityDepth;
            [FieldOffset(32)]
            public float4 CausticOffset;
        }

        #endregion

        #region ══════════ Public Properties ══════════

        public EnvironmentState CurrentState   => _currentState;
        public float TimeOfDay                 => _cycleTimer / _cycleDuration;
        public float SunAngle                  => _sunAngleDegrees;
        public float SunElevation              => _sunElevationDot;
        public double ElapsedCycleTimeSeconds  => _elapsedCycleTimeSeconds;
        public Color CurrentFogColor           => _currentValues.fogColor;
        public float CurrentFogDensity         => _currentValues.fogDensity;
        public float CurrentFogAttenuationDistance => _currentValues.fogAttenuationDistance;
        public float CurrentSkyExposure        => _currentValues.skyExposure;
        public Color CurrentAmbientColor       => _currentValues.ambientColor;
        public bool  IsEclipseActive           => _eclipseActive;
        public float EclipseRemainingTime      => _eclipseRemainingTime;
        public float CycleDuration             => _cycleDuration;
        public float OrbitalInclination        => _orbitalInclination;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Raw profile sun intensity AFTER transition interpolation.
        /// This is the "what the sun WANTS to be" value.
        /// UnderwaterVisuals multiplies this by horizon × depth.
        /// CelestialEngine then multiplies by eclipse visibility.
        ///
        /// NEVER written to sunLight.intensity by this script.
        /// </summary>
        public float ProfileSunIntensity => _currentValues.sunIntensity;

        /// <summary>
        /// v4.3 CLARIFICATION:
        /// Horizon fade factor [0..1].
        /// Computed from sun elevation angle via smoothstep.
        /// 0 = sun fully below horizon, 1 = sun fully above.
        ///
        /// Read by UnderwaterVisuals to compute:
        ///   sunLight.intensity = ProfileSunIntensity × ComputedHorizonFade × depthFactor
        /// </summary>
        public float ComputedHorizonFade => _computedHorizonFade;

        public float CurrentSunIntensity => _computedSunIntensity;

        public float CurrentTemperature  => _currentValues.temperature;
        public float CurrentRadiation    => _currentValues.radiation;
        public float SeaLevelY           => ResolveSeaLevelY();
        public bool IsUnderwaterState    => _currentState == EnvironmentState.UNDERWATER;

        #endregion

        #region ══════════ Lifecycle ══════════

        private void Awake()
        {
            if (Application.isPlaying)
            {
                HectonAtmosphereManager registeredAtmosphere = GlobalRegistry.Atmosphere;
                if (registeredAtmosphere != null && !ReferenceEquals(registeredAtmosphere, this))
                {
                    Destroy(this);
                    return;
                }
            }

            _registeredToTickManager = false;
            ValidateAbyssAtmospherePresentationLayout();

            // Build biome profile dictionary (ONE-TIME, O(n) initialization)
            _biomeProfileDict = new Dictionary<int, AtmosphereProfile>(16);
            if (_biomeOverrides != null)
            {
                for (int i = 0; i < _biomeOverrides.Length; i++)
                {
                    _biomeProfileDict[_biomeOverrides[i].biomeID] = _biomeOverrides[i].profile;
                }
            }

            InitializeCycleTimer();
            InitializeAtmosphereValues();
            CacheRegistryRuntimeReferences();
            CachePlayerMovement();
            EnsureAegirRingShadowCookie();
            _lastAtmosphereSlowTickTime = 0f;
            _atmosphereTimelineAccumulator = 0f;
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (EditorApplication.isCompiling)
                return;
#endif

            if (Application.isPlaying)
            {
                TryRegisterService();
                TryRegisterHotSwapListener();
                TryRegister();
                TryRegisterLateFrame();

                MapMagicBiomeEvents.Register(this);
                BiomeMatrixEvents.Register(this);
                CacheRegistryRuntimeReferences();
                RefreshBiomeMatrixDirectorFromRegistry();
                ApplyCurrentMatrixAtmosphereOverride();
            }
#if UNITY_EDITOR
            else
            {
                _editorPreviewDirty = true;
                EditorApplication.update -= EditorTick;
                EditorApplication.update += EditorTick;
            }
#endif
        }

        private void Start()
        {
            if (!Application.isPlaying) return;

            if (!_registeredToTickManager)
            {
                TryRegister();
                TryRegisterLateFrame();
                if (!_registeredToTickManager)
                {
                    Hecton8.Core.H8Debug.LogError(
                        "[HectonAtmosphere] SystemDispatcher registration failed in Start(). " +
                        "Atmosphere will NOT update.", this);
                }
            }

            if (_biomeMatrixDirector == null)
                RefreshBiomeMatrixDirectorFromRegistry();

            ApplyCurrentMatrixAtmosphereOverride();
            EnsureAegirRingShadowCookie();
        }

        private void OnDisable()
        {
            bool wasRegisteredRuntime = ReferenceEquals(GlobalRegistry.Atmosphere, this);
            _autoUnderwaterState = false;

            if (Application.isPlaying)
            {
                TryUnregister();
                TryUnregisterLateFrame();
                TryUnregisterHotSwapListener();
                TryUnregisterService();

                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.update -= EditorTick;
            }
#endif

            if (!Application.isPlaying || wasRegisteredRuntime)
                ResetCycleShaderGlobals();
            ReleaseAegirRingShadowCookie();
        }

        private void OnDestroy()
        {
            bool wasRegisteredRuntime = ReferenceEquals(GlobalRegistry.Atmosphere, this);
            if (Application.isPlaying)
            {
                MapMagicBiomeEvents.Unregister(this);
                BiomeMatrixEvents.Unregister(this);
                TryUnregister();
                TryUnregisterLateFrame();
                TryUnregisterHotSwapListener();
                TryUnregisterService();
            }

            if (!Application.isPlaying || wasRegisteredRuntime)
                ResetCycleShaderGlobals();
            ReleaseAegirRingShadowCookie();

        }

        private void EnsureAegirRingShadowCookie()
        {
            if (_cachedCelestialEngine != null)
            {
                if (_aegirRingShadowCookie != null)
                    ReleaseAegirRingShadowCookie();
                return;
            }

            if (!_useAegirRingShadowCookie)
            {
                if (_aegirRingShadowCookie != null)
                    ReleaseAegirRingShadowCookie();
                return;
            }

            if (_sunLight == null)
                return;

            Texture2D authoredCookie = _authoredAegirRingShadowCookie;
            if (authoredCookie == null)
            {
                if (_aegirRingShadowCookie != null)
                    ReleaseAegirRingShadowCookie();
                return;
            }

            if (!ReferenceEquals(_aegirRingShadowCookie, authoredCookie))
            {
                ReleaseAegirRingShadowCookie();
                _aegirRingShadowCookie = authoredCookie;
            }

            if (_sunLight.cookie != _aegirRingShadowCookie)
                _sunLight.cookie = _aegirRingShadowCookie;
        }

        private void ReleaseAegirRingShadowCookie()
        {
            Texture2D boundCookie = _aegirRingShadowCookie;
            if (boundCookie != null && _sunLight != null && ReferenceEquals(_sunLight.cookie, boundCookie))
                _sunLight.cookie = null;

            _aegirRingShadowCookie = null;
        }

        private void TryRegister()
        {
            if (_registeredToTickManager || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredToTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrameTick || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void TryUnregister()
        {
            if (!_registeredToTickManager)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registeredToTickManager = false;
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrameTick)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrameTick = false;
        }

        private void TryRegisterService()
        {
            if (_registeredAtmosphereRuntime || !Application.isPlaying)
                return;

            HectonAtmosphereManager registeredAtmosphere = GlobalRegistry.Atmosphere;
            if (registeredAtmosphere != null && !ReferenceEquals(registeredAtmosphere, this))
            {
                Destroy(this);
                return;
            }

            GlobalRegistry.RegisterAtmosphereRuntime(this);
            _registeredAtmosphereRuntime = ReferenceEquals(GlobalRegistry.Atmosphere, this);
        }

        private void TryUnregisterService()
        {
            if (!_registeredAtmosphereRuntime)
                return;

            if (ReferenceEquals(GlobalRegistry.Atmosphere, this))
                GlobalRegistry.UnregisterAtmosphereRuntime(this);

            _registeredAtmosphereRuntime = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    CachePlayerMovement();
                    break;
                case GlobalRegistryServiceSlot.CelestialEngineRuntime:
                    _cachedCelestialEngine = currentService as HectonCelestialEngine;
                    EnsureAegirRingShadowCookie();
                    break;
                case GlobalRegistryServiceSlot.BiomeMatrixRuntime:
                    if (_biomeMatrixDirector == null || ReferenceEquals(previousService, _biomeMatrixDirector))
                    {
                        _biomeMatrixDirector = currentService as BiomeMatrixDirector;
                        ApplyCurrentMatrixAtmosphereOverride();
                    }
                    break;
                case GlobalRegistryServiceSlot.ProceduralFieldSamplerRuntime:
                    if (_proceduralFieldSampler == null || ReferenceEquals(previousService, _proceduralFieldSampler))
                    {
                        _proceduralFieldSampler = currentService as WorldProceduralFieldSampler;
                        if (_proceduralFieldSampler == null)
                            ClearProceduralBiomeInfluenceState();
                    }
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrame();
                    if (isActiveAndEnabled)
                    {
                        if (currentService != null)
                        {
                            TryRegister();
                            TryRegisterLateFrame();
                        }
                    }
                    break;
            }

            _ = previousService;
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.update -= EditorTick;
                return;
            }

            if (Application.isPlaying || this == null)
                return;

            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
                return;

            if (!_editorInitialized)
            {
                InitializeCycleTimer();
                InitializeAtmosphereValues();
                _editorInitialized = true;
                _editorPreviewDirty = true;
            }

            bool sunMoved = SyncEditorPreviewFromSunTransform();
            if (!_editorPreviewDirty && !sunMoved)
                return;

            RunAtmosphereTimeline(0f);
            _editorPreviewDirty = false;
        }
#endif

#if UNITY_EDITOR
        /// <summary>
        /// Synchronizes edit-mode atmosphere state from the live directional-light rotation.
        /// </summary>
        /// <remarks>
        /// Scene View time-of-day authoring must treat the sun transform as the source of truth
        /// while the editor is not playing; otherwise preview visuals drift away from runtime.
        /// </remarks>
        public bool SyncEditorPreviewFromSunTransform()
        {
            if (Application.isPlaying || _sunLight == null)
                return false;

            Transform sunTransform = _sunLight.transform;
            if (sunTransform == null || !sunTransform.hasChanged)
                return false;

            _editorInitialized = true;
            SyncCycleFromEditorSunTransform();
            sunTransform.hasChanged = false;
            _editorPreviewDirty = true;
            return true;
        }

        /// <summary>
        /// Consumes the edit-mode preview dirty flag set by atmosphere authoring changes.
        /// </summary>
        public bool ConsumeEditorPreviewDirty()
        {
            if (Application.isPlaying)
                return false;

            bool wasDirty = _editorPreviewDirty;
            _editorPreviewDirty = false;
            return wasDirty;
        }

        private void SyncCycleFromEditorSunTransform()
        {
            float3 sunForward = NormalizeVisualRsqrt(
                (float3)_sunLight.transform.forward,
                new float3(0f, 0f, 1f));

            quaternion qInclination = quaternion.RotateZ(math.radians(_orbitalInclination));
            quaternion qAzimuth = quaternion.RotateY(math.radians(_sunOrbitalYAngle));
            quaternion orbitFrame = math.mul(qAzimuth, qInclination);
            float3 localForward = math.mul(math.inverse(orbitFrame), sunForward);
            localForward = NormalizeVisualRsqrt(localForward, new float3(0f, 0f, 1f));

            float resolvedSunAngle = math.degrees(
                MathLodApproximation.ApproxAtan2Fast(-localForward.y, localForward.z));
            if (resolvedSunAngle < 0f)
                resolvedSunAngle += 360f;

            _sunAngleDegrees = resolvedSunAngle;
            _cycleTimer = (_sunAngleDegrees / 360f) * _cycleDuration;

            double completedCycles = _cycleDuration > 0f
                ? math.floor(_elapsedCycleTimeSeconds / _cycleDuration)
                : 0d;
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;

            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            if (math.any(math.abs(sunForward - _cachedShaderSunDirection) > 0.000001f))
            {
                _cachedShaderSunDirection = sunForward;
                Shader.SetGlobalVector(
                    _shaderID_SunDirection,
                    new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
            }

            PublishCycleShaderGlobals(_cycleTimer / _cycleDuration);

            SyncWaterSurfaceFromPlayerMovement();

            EnvironmentState resolvedState = ResolveState();
            AtmosphereProfile resolvedProfile = ResolveProfile(resolvedState);

            _currentState = resolvedState;
            _currentValues = resolvedProfile != null
                ? AtmosphereSnapshot.FromProfile(resolvedProfile)
                : AtmosphereSnapshot.Default;
            _transitionOrigin = _currentValues;
            _transitionProgress = 1f;

            ComputeSunValues();
            PublishGiantAbyssLight();
        }

        private void OnValidate()
        {
            if (EditorApplication.isCompiling || Application.isPlaying)
                return;

            _editorInitialized = false;
            _editorPreviewDirty = true;
            _cycleDuration = math.max(_cycleDuration, 1f);

            if (_sunLight == null)
                CacheRenderSettingsSunCold();
            EnsureAegirRingShadowCookie();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Vector3 center = new Vector3(
                transform.position.x, _waterSurfaceY, transform.position.z);
            Gizmos.DrawCube(center, new Vector3(200f, 0.05f, 200f));

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            const int segments = 64;
            const float orbitRadius = 50f;

            float incRad = math.radians(_orbitalInclination);
            float azRad  = math.radians(_sunOrbitalYAngle);

            quaternion qAzimuth     = quaternion.RotateY(azRad);
            quaternion qInclination = quaternion.RotateZ(incRad);
            quaternion orbitFrame   = math.mul(qAzimuth, qInclination);

            Vector3 prevPoint = Vector3.zero;
            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * math.PI * 2f;
                float3 localPoint = new float3(
                    CinematicMath.FastCos(angle) * orbitRadius,
                    CinematicMath.FastSin(angle) * orbitRadius, 0f);
                float3 worldPoint = math.mul(orbitFrame, localPoint);
                Vector3 wp = transform.position + (Vector3)worldPoint;

                if (i > 0) Gizmos.DrawLine(prevPoint, wp);
                prevPoint = wp;
            }

            if (_sunLight != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(
                    transform.position -
                    (Vector3)(_sunLight.transform.forward * orbitRadius), 2f);
            }
        }
#endif

        #endregion

        #region ══════════ ITickable ══════════

        public void SlowTick()
        {
            long timelineStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            float now = ResolveAtmosphereTimelineClockSeconds();
            float elapsed = _lastAtmosphereSlowTickTime > 0f
                ? math.clamp(now - _lastAtmosphereSlowTickTime, AtmosphereTimelineStepSeconds, AtmosphereTimelineStepSeconds * AtmosphereTimelineMaxStepsPerSlowTick)
                : AtmosphereTimelineStepSeconds;
            _lastAtmosphereSlowTickTime = now;
            _atmosphereTimelineAccumulator = math.min(
                _atmosphereTimelineAccumulator + elapsed,
                AtmosphereTimelineStepSeconds * AtmosphereTimelineMaxStepsPerSlowTick);

            int steps = 0;
            while (_atmosphereTimelineAccumulator >= AtmosphereTimelineStepSeconds &&
                   steps < AtmosphereTimelineMaxStepsPerSlowTick)
            {
                RunAtmosphereTimeline(AtmosphereTimelineStepSeconds);
                _atmosphereTimelineAccumulator -= AtmosphereTimelineStepSeconds;
                steps++;
            }

            PublishAtmosphereTimelineBudgetWarningIfNeeded(timelineStartTicks);
        }

        public void LateFrameTick()
        {
            FlushLateFramePresentation();
        }

        void ILateFrameTickable.LateFrameTick()
        {
            FlushLateFramePresentation();
        }

        private void FlushLateFramePresentation()
        {
            FlushSunPresentation();
            FlushCycleShaderGlobals();
            FlushGiantAbyssLight();
            FlushAbyssAtmospherePresentation();
        }

        private static float ResolveAtmosphereTimelineClockSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            if (dispatcher == null)
                return 0f;

            double timeSeconds = dispatcher.DilatedTimeSeconds;
            if (!math.isfinite(timeSeconds) || timeSeconds <= 0d)
                return 0f;

            return (float)math.min(AtmosphereTimelineClockMaxSeconds, timeSeconds);
        }

        private void PublishAtmosphereTimelineBudgetWarningIfNeeded(long timelineStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - timelineStartTicks;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / System.Diagnostics.Stopwatch.Frequency;
            int currentFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (elapsedMilliseconds <= AtmosphereTimelineBudgetMilliseconds ||
                currentFrame < _nextAtmosphereTimelineWarningFrame)
            {
                return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                _AtmosphereTimelineBudgetWarningHash,
                _AtmosphereTimelineContextHash,
                (float)elapsedMilliseconds);
            _nextAtmosphereTimelineWarningFrame = currentFrame + AtmosphereTimelineWarningCooldownFrames;
        }

        private void RunAtmosphereTimeline(float deltaTime)
        {
            SyncWaterSurfaceFromPlayerMovement();
            AdvanceCycleTimer(deltaTime);
            bool skipWindDirectionMatrix = SystemDispatcher.IsLateFrameAmbientEventSheddingActive;
            if (!skipWindDirectionMatrix)
                RotateSun();
            TickEclipseTimer(deltaTime);

            EnvironmentState resolved = ResolveState();
            ProcessStateTransition(resolved);

            InterpolateAtmosphere(deltaTime);
            ApplyProceduralBiomeInfluenceAtmosphere(deltaTime);

            ComputeSunValues();
            QueueGiantAbyssLight();
            QueueAbyssAtmospherePresentation();
        }

        #endregion

        #region ══════════ Cycle & Sun ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InitializeCycleTimer()
        {
            _cycleTimer = _initialTimeOfDay * _cycleDuration;
            _elapsedCycleTimeSeconds = _cycleTimer;
        }

        private void InitializeAtmosphereValues()
        {
            AtmosphereProfile profile = ResolveProfile(_currentState);
            _currentValues = profile != null
                ? AtmosphereSnapshot.FromProfile(profile)
                : AtmosphereSnapshot.Default;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 1f;

            _computedHorizonFade = 1f;
            _computedSunIntensity = _currentValues.sunIntensity;

            RotateSun();
            ComputeSunValues();
            QueueGiantAbyssLight();
            QueueAbyssAtmospherePresentation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AdvanceCycleTimer(float deltaTime)
        {
            _elapsedCycleTimeSeconds += deltaTime;
            _cycleTimer += deltaTime;
            _cycleTimer  = math.fmod(_cycleTimer, _cycleDuration);
        }

        private void RotateSun()
        {
            if (_sunLight == null) return;

            float normalized = _cycleTimer / _cycleDuration;
            _sunAngleDegrees = normalized * 360f;

            float dailyRad       = math.radians(_sunAngleDegrees);
            float inclinationRad = math.radians(_orbitalInclination);
            float azimuthRad     = math.radians(_sunOrbitalYAngle);

            float4x4 rotationMatrix = math.mul(
                math.mul(
                    BuildAxisAngleRotationMatrix(new float3(0f, 1f, 0f), azimuthRad),
                    BuildAxisAngleRotationMatrix(new float3(0f, 0f, 1f), inclinationRad)),
                BuildAxisAngleRotationMatrix(new float3(1f, 0f, 0f), dailyRad));
            float3 sunForwardMath = math.mul(rotationMatrix, new float4(0f, 0f, 1f, 0f)).xyz;
            Vector3 sunForwardVector = new Vector3(sunForwardMath.x, sunForwardMath.y, sunForwardMath.z);
            if (sunForwardVector.sqrMagnitude > 0.0001f)
            {
                _pendingSunForwardVector = sunForwardVector;
                _pendingSunPresentationDirty = true;
            }

            float3 sunForward = new float3(sunForwardVector.x, sunForwardVector.y, sunForwardVector.z);
            _sunElevationDot = math.dot(-sunForward, new float3(0f, 1f, 0f));

            // v2.1 OPT: Dirty-write batching — only write to shader if changed
            if (!sunForward.Equals(_cachedShaderSunDirection))
            {
                _pendingShaderSunDirection = sunForward;
                _pendingSunDirectionShaderDirty = true;
            }

            PublishCycleShaderGlobals(normalized);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 NormalizeVisualRsqrt(float3 value, float3 fallback)
        {
            float lengthSq = math.lengthsq(value);
            if (!math.isfinite(lengthSq) || lengthSq <= 0.0001f)
                return fallback;

            return value * math.rsqrt(lengthSq);
        }

        private static float4x4 BuildAxisAngleRotationMatrix(float3 axis, float radians)
        {
            axis = NormalizeVisualRsqrt(axis, new float3(1f, 0f, 0f));
            float x = axis.x;
            float y = axis.y;
            float z = axis.z;
            float sin = CinematicMath.FastSin(radians);
            float cos = CinematicMath.FastCos(radians);
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

        private void PublishCycleShaderGlobals(float normalizedTime)
        {
            float timeOfDay01 = math.saturate(normalizedTime);
            float thresholdSin = CinematicMath.FastSin(math.radians(_nightThresholdAngle));
            float thresholdSpan = math.max(thresholdSin * 2f, 0.001f);
            float daytimeLerp = math.saturate((_sunElevationDot + thresholdSin) / thresholdSpan);
            float smoothedDaytime = daytimeLerp * daytimeLerp * (3f - 2f * daytimeLerp);
            float nightFactor = 1f - smoothedDaytime;
            float biolumPhaseMultiplier = HectonVegetationConstants.SargassumBiolumPhaseMultiplier;

            if (math.abs(timeOfDay01 - _cachedShaderTimeOfDay01) > 0.0001f ||
                math.abs(nightFactor - _cachedShaderNightFactor) > 0.0001f ||
                math.abs(biolumPhaseMultiplier - _cachedShaderSargassumBiolumPhaseMultiplier) > 0.0001f ||
                float.IsNaN(_cachedShaderSargassumBiolumPhaseMultiplier))
            {
                _pendingShaderTimeOfDay01 = timeOfDay01;
                _pendingShaderNightFactor = nightFactor;
                _pendingShaderSargassumBiolumPhaseMultiplier = biolumPhaseMultiplier;
                _pendingCycleShaderDirty = true;
            }
        }

        private void ResetCycleShaderGlobals()
        {
            _cachedShaderTimeOfDay01 = -1f;
            _cachedShaderNightFactor = -1f;
            _cachedShaderSargassumBiolumPhaseMultiplier = float.NaN;
            _cachedFinalGiantAbyssLight = new float4(-1f, -1f, -1f, -1f);
            _cachedAegirDirection = new float4(0f, 0f, 0f, -999f);
            _cachedAbyssAtmospherePresentation = CreateInvalidAbyssAtmospherePresentation();
            _pendingSunPresentationDirty = false;
            _pendingSunDirectionShaderDirty = false;
            _pendingCycleShaderDirty = false;
            _pendingGiantAbyssLightDirty = false;
            _pendingAbyssAtmospherePresentationDirty = false;
            Shader.SetGlobalFloat(_shaderID_HectonTimeOfDay01, 0f);
            Shader.SetGlobalFloat(_shaderID_HectonNightFactor, 0f);
            Shader.SetGlobalFloat(_shaderID_SargassumBiolumPhaseMultiplier, HectonVegetationConstants.SargassumBiolumPhaseMultiplier);
            Shader.SetGlobalVector(_shaderID_FinalGiantAbyssLight, Vector4.zero);
            Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(0f, 0f, 1f, 0f));
            Shader.SetGlobalVector(_shaderID_H8AbyssAbsorptionColor, Vector4.zero);
            Shader.SetGlobalVector(_shaderID_H8AbyssAtmosphereParams, Vector4.zero);
            Shader.SetGlobalVector(_shaderID_CausticOffset, Vector4.zero);
        }

        private void FlushSunPresentation()
        {
            if (_pendingSunPresentationDirty)
            {
                _pendingSunPresentationDirty = false;
                if (_sunLight != null && _pendingSunForwardVector.sqrMagnitude > 0.0001f)
                    _sunLight.transform.forward = _pendingSunForwardVector;
            }

            if (!_pendingSunDirectionShaderDirty)
                return;

            _pendingSunDirectionShaderDirty = false;
            float3 sunForward = _pendingShaderSunDirection;
            if (sunForward.Equals(_cachedShaderSunDirection))
                return;

            _cachedShaderSunDirection = sunForward;
            Shader.SetGlobalVector(_shaderID_SunDirection, new Vector4(sunForward.x, sunForward.y, sunForward.z, 0f));
        }

        private void FlushCycleShaderGlobals()
        {
            if (!_pendingCycleShaderDirty)
                return;

            _pendingCycleShaderDirty = false;

            if (math.abs(_pendingShaderTimeOfDay01 - _cachedShaderTimeOfDay01) > 0.0001f)
            {
                _cachedShaderTimeOfDay01 = _pendingShaderTimeOfDay01;
                Shader.SetGlobalFloat(_shaderID_HectonTimeOfDay01, _pendingShaderTimeOfDay01);
            }

            if (math.abs(_pendingShaderNightFactor - _cachedShaderNightFactor) > 0.0001f)
            {
                _cachedShaderNightFactor = _pendingShaderNightFactor;
                Shader.SetGlobalFloat(_shaderID_HectonNightFactor, _pendingShaderNightFactor);
            }

            if (math.abs(_pendingShaderSargassumBiolumPhaseMultiplier - _cachedShaderSargassumBiolumPhaseMultiplier) > 0.0001f ||
                float.IsNaN(_cachedShaderSargassumBiolumPhaseMultiplier))
            {
                _cachedShaderSargassumBiolumPhaseMultiplier = _pendingShaderSargassumBiolumPhaseMultiplier;
                Shader.SetGlobalFloat(_shaderID_SargassumBiolumPhaseMultiplier, _pendingShaderSargassumBiolumPhaseMultiplier);
            }
        }

        #endregion

        #region ══════════ Eclipse ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void TickEclipseTimer(float deltaTime)
        {
            if (!_eclipseActive) return;
            _eclipseRemainingTime -= deltaTime;
            if (_eclipseRemainingTime <= 0f)
            {
                _eclipseRemainingTime = 0f;
                _eclipseActive        = false;
            }
        }

        #endregion

        #region ══════════ State Machine ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private EnvironmentState ResolveState()
        {
            if (_eclipseActive)       return EnvironmentState.ECLIPSE;
            if (EvaluateUnderwater()) return EnvironmentState.UNDERWATER;
            return EvaluateDaytime()
                ? EnvironmentState.SURFACE_DAY
                : EnvironmentState.SURFACE_NIGHT;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateUnderwater()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _autoUnderwaterState = false;
                return false;
            }
#endif

            if (_underwaterExternalFlag)
            {
                _autoUnderwaterState = true;
                return true;
            }

            if (!_useAutoUnderwaterDetection)
            {
                _autoUnderwaterState = false;
                return false;
            }

            if (_playerMovement != null)
            {
                _autoUnderwaterState = ResolveMovementUnderwaterState();
                return _autoUnderwaterState;
            }

            float depth = ResolvePlayerDepth();
            _autoUnderwaterState =
                SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    depth,
                    _autoUnderwaterState,
                    VisualEnterUnderwaterDepth,
                    VisualExitUnderwaterDepth);

            return _autoUnderwaterState;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ResolveMovementUnderwaterState()
        {
            if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                float depth = ResolvePlayerDepth();
                if ((movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u)
                    return true;

                return SurfaceStateUtility.ResolveUnderwaterFromDepth(
                    depth,
                    _autoUnderwaterState,
                    VisualEnterUnderwaterDepth,
                    VisualExitUnderwaterDepth);
            }

            if (HasPlayerRuntimeContext())
                return false;

            if (_playerMovement == null)
                return false;

            switch (_playerMovement.CurrentLocomotionMode)
            {
                case PlayerLocomotionMode.UnderwaterSwim:
                    return true;

                case PlayerLocomotionMode.ExosuitLocomotion:
                    return ResolvePlayerDepth() > 0.01f || _playerMovement.IsPlayerSubmerged;

                case PlayerLocomotionMode.SurfaceSwim:
                    return _playerMovement.IsPlayerSubmerged;

                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool EvaluateDaytime()
        {
            float thresholdSin = CinematicMath.FastSin(math.radians(_nightThresholdAngle));
            return _sunElevationDot > thresholdSin;
        }

        private void ProcessStateTransition(EnvironmentState newState)
        {
            if (newState == _currentState) return;

            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;

            _currentState = newState;

            if (Application.isPlaying)
                AtmosphereEvents.TryRaiseStateChanged(_currentState);
        }

        #endregion

        #region ══════════ Interpolation ══════════

        private void InterpolateAtmosphere(float deltaTime)
        {
            if (_transitionProgress >= 1f) return;

            _transitionProgress = math.saturate(
                _transitionProgress + deltaTime * _transitionSpeed);

            float t = _transitionProgress;
            float smoothT = t * t * (3f - 2f * t);

            AtmosphereProfile target = ResolveProfile(_currentState);
            if (target == null) return;

            AtmosphereSnapshot targetSnap = AtmosphereSnapshot.FromProfile(target);
            _currentValues = AtmosphereSnapshot.Lerp(
                in _transitionOrigin, in targetSnap, smoothT);
        }

        #endregion

        #region ══════════ Sun Values (COMPUTE ONLY, NO WRITE) ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ComputeSunValues()
        {
            float fadeThreshold = CinematicMath.FastSin(math.radians(_sunHorizonFadeAngle));

            if (_sunElevationDot <= 0f)
            {
                _computedHorizonFade = 0f;
            }
            else if (_sunElevationDot >= fadeThreshold)
            {
                _computedHorizonFade = 1f;
            }
            else
            {
                float st = _sunElevationDot / fadeThreshold;
                _computedHorizonFade = st * st * (3f - 2f * st);
            }

            _computedSunIntensity = _currentValues.sunIntensity * _computedHorizonFade;
        }

        private void QueueGiantAbyssLight()
        {
            _pendingGiantAbyssLightDirty = true;
        }

        private void FlushGiantAbyssLight()
        {
            if (!_pendingGiantAbyssLightDirty)
                return;

            _pendingGiantAbyssLightDirty = false;
            PublishGiantAbyssLight();
        }

        private void QueueAbyssAtmospherePresentation()
        {
            AbyssAtmospherePresentationDTO payload = BuildAbyssAtmospherePresentation();
            if (!AbyssAtmospherePresentationChanged(in payload, in _cachedAbyssAtmospherePresentation))
                return;

            _pendingAbyssAtmospherePresentation = payload;
            _pendingAbyssAtmospherePresentationDirty = true;
        }

        private void FlushAbyssAtmospherePresentation()
        {
            if (!_pendingAbyssAtmospherePresentationDirty)
                return;

            _pendingAbyssAtmospherePresentationDirty = false;
            AbyssAtmospherePresentationDTO payload = _pendingAbyssAtmospherePresentation;
            if (!AbyssAtmospherePresentationChanged(in payload, in _cachedAbyssAtmospherePresentation))
                return;

            _cachedAbyssAtmospherePresentation = payload;
            Shader.SetGlobalVector(
                _shaderID_H8AbyssAbsorptionColor,
                new Vector4(
                    payload.AbsorptionColorAndDepthMask.x,
                    payload.AbsorptionColorAndDepthMask.y,
                    payload.AbsorptionColorAndDepthMask.z,
                    payload.AbsorptionColorAndDepthMask.w));
            Shader.SetGlobalVector(
                _shaderID_H8AbyssAtmosphereParams,
                new Vector4(
                    payload.FogDensityQualityDepth.x,
                    payload.FogDensityQualityDepth.y,
                    payload.FogDensityQualityDepth.z,
                    payload.FogDensityQualityDepth.w));
            Shader.SetGlobalVector(
                _shaderID_CausticOffset,
                new Vector4(
                    payload.CausticOffset.x,
                    payload.CausticOffset.y,
                    payload.CausticOffset.z,
                    payload.CausticOffset.w));
        }

        private AbyssAtmospherePresentationDTO BuildAbyssAtmospherePresentation()
        {
            float depthMeters = math.max(0f, ResolvePlayerDepth());
            float depthMask = ResolveAbyssDepthMask(depthMeters);
            float rawQuality = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.isfinite(rawQuality) ? rawQuality : 1f);
            float qualityCurve = Smooth01(quality);

            Color surfaceLinearColor = _giantAbyssSurfaceLightColor.linear;
            float3 giantSurfaceColor = new float3(surfaceLinearColor.r, surfaceLinearColor.g, surfaceLinearColor.b);
            Color biolumeLinearColor = _giantAbyssBiolumeColor.linear;
            float3 biolumeColor = new float3(biolumeLinearColor.r, biolumeLinearColor.g, biolumeLinearColor.b);
            float3 sigmaRgbPerMeter = math.max(
                new float3(
                    _giantAbyssSigmaRgbPerMeter.x,
                    _giantAbyssSigmaRgbPerMeter.y,
                    _giantAbyssSigmaRgbPerMeter.z),
                float3.zero);
            float3 transmittance = ApproximateExpNegPositive(depthMeters * sigmaRgbPerMeter);
            float minLuminance = math.max(0f, _abyssMinimumReadableLuminance);
            float3 readableFloor = new float3(minLuminance, minLuminance * 1.35f, minLuminance * 1.9f) *
                                   math.lerp(0.8f, 1.35f, qualityCurve);
            float3 absorptionColor = (giantSurfaceColor * transmittance * 0.18f) +
                                     (biolumeColor * math.max(0f, _giantAbyssBiolumeIntensity));
            absorptionColor = math.all(math.isfinite(absorptionColor))
                ? math.max(absorptionColor, readableFloor)
                : readableFloor;

            float fogBoost = depthMask * math.max(0f, _abyssFogDensityBoost);
            float fogDetail = depthMask * math.lerp(0.35f, 1f, qualityCurve);
            float wrappedTime = RepeatPositiveSeconds(_elapsedCycleTimeSeconds, 4096d);
            float shallowCausticMask = math.saturate(1f - depthMeters * (1f / 96f));
            float causticScale = math.max(0f, _abyssCausticScrollMetersPerSecond) *
                                 math.lerp(0.35f, 1.2f, qualityCurve) *
                                 shallowCausticMask;
            float causticPhase = math.frac(wrappedTime * 0.03125f);
            float2 causticOffset = new float2(
                wrappedTime * 0.73f,
                CinematicMath.FastSin(wrappedTime * 0.071f) * 0.5f) * causticScale;

            AbyssAtmospherePresentationDTO payload;
            payload.AbsorptionColorAndDepthMask = new float4(absorptionColor, depthMask);
            payload.FogDensityQualityDepth = new float4(fogBoost, fogDetail, quality, depthMeters);
            payload.CausticOffset = new float4(causticOffset.x, causticOffset.y, shallowCausticMask, causticPhase);
            return payload;
        }

        private void PublishGiantAbyssLight()
        {
            HectonCelestialEngine celestial = _cachedCelestialEngine;
            float3 aegirDirection = new float3(0f, 0f, 1f);
            float planetPhase = 0f;
            float eclipseBacklit = 0f;

            if (celestial != null)
            {
                planetPhase = celestial.PlanetPhase;
                eclipseBacklit = math.saturate(celestial.EclipseBacklitFactor);
                if (celestial.TryGetAegirSkyDirection(out Vector3 direction))
                    aegirDirection = NormalizeVisualRsqrt(new float3(direction.x, direction.y, direction.z), new float3(0f, 0f, 1f));
            }

            float depthMeters = math.max(0f, ResolvePlayerDepth());
            float3 sigmaRgbPerMeter = math.max(
                new float3(
                    _giantAbyssSigmaRgbPerMeter.x,
                    _giantAbyssSigmaRgbPerMeter.y,
                    _giantAbyssSigmaRgbPerMeter.z),
                float3.zero);
            float3 waterTransmittance = ApproximateExpNegPositive(depthMeters * sigmaRgbPerMeter);

            Color surfaceLinearColor = _giantAbyssSurfaceLightColor.linear;
            float3 giantSurfaceColor = new float3(surfaceLinearColor.r, surfaceLinearColor.g, surfaceLinearColor.b);
            float phase01 = math.saturate((planetPhase * 0.5f) + 0.5f);
            float ringShadowMultiplier = ResolveAegirRingShadowMultiplier(celestial, aegirDirection);
            float planetShineIntensity = math.max(0f, _giantAbyssLightIntensity) *
                                          math.saturate((phase01 * phase01) + (eclipseBacklit * 0.35f)) *
                                          ringShadowMultiplier;

            Color biolumeLinearColor = _giantAbyssBiolumeColor.linear;
            float3 biolumeColor = new float3(biolumeLinearColor.r, biolumeLinearColor.g, biolumeLinearColor.b);
            float biolumeDepthMask = math.saturate(depthMeters * 0.0025f) * math.saturate(_cachedShaderNightFactor);

            float3 finalGiantAbyssLight = (giantSurfaceColor * waterTransmittance * planetShineIntensity) +
                                           (biolumeColor * math.max(0f, _giantAbyssBiolumeIntensity) * biolumeDepthMask);
            float abyssDepthMask = ResolveAbyssDepthMask(depthMeters);
            float minLuminance = math.max(0f, _abyssMinimumReadableLuminance);
            float3 readabilityFloor = new float3(minLuminance, minLuminance * 1.35f, minLuminance * 1.9f) *
                                      abyssDepthMask;
            if (!math.all(math.isfinite(finalGiantAbyssLight)))
                finalGiantAbyssLight = float3.zero;
            finalGiantAbyssLight = math.max(finalGiantAbyssLight, readabilityFloor);
            float4 finalPayload = new float4(finalGiantAbyssLight, planetShineIntensity);
            if (math.all(math.isfinite(finalPayload)) &&
                math.any(math.abs(finalPayload - _cachedFinalGiantAbyssLight) > 0.0001f))
            {
                _cachedFinalGiantAbyssLight = finalPayload;
                Shader.SetGlobalVector(_shaderID_FinalGiantAbyssLight, new Vector4(finalPayload.x, finalPayload.y, finalPayload.z, finalPayload.w));
            }

            float4 directionPayload = new float4(aegirDirection, planetPhase);
            if (math.all(math.isfinite(directionPayload)) &&
                math.any(math.abs(directionPayload - _cachedAegirDirection) > 0.0001f))
            {
                _cachedAegirDirection = directionPayload;
                Shader.SetGlobalVector(_shaderID_AegirDirection, new Vector4(directionPayload.x, directionPayload.y, directionPayload.z, directionPayload.w));
            }
        }

        private float ResolveAegirRingShadowMultiplier(HectonCelestialEngine celestial, float3 aegirDirection)
        {
            return 1f;
        }

        private float ResolveAbyssDepthMask(float depthMeters)
        {
            float start = math.max(0f, _abyssAbsorptionStartDepthMeters);
            float end = math.max(start + 1f, _abyssAbsorptionFullDepthMeters);
            return Smooth01(math.saturate((math.max(0f, depthMeters) - start) * math.rcp(end - start)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Smooth01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - 2f * t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float RepeatPositiveSeconds(double value, double period)
        {
            double safePeriod = period > 0d && !double.IsNaN(period) && !double.IsInfinity(period) ? period : 1d;
            double wrapped = value % safePeriod;
            if (wrapped < 0d)
                wrapped += safePeriod;
            return (float)wrapped;
        }

        private static bool AbyssAtmospherePresentationChanged(
            in AbyssAtmospherePresentationDTO current,
            in AbyssAtmospherePresentationDTO cached)
        {
            return math.any(math.abs(current.AbsorptionColorAndDepthMask - cached.AbsorptionColorAndDepthMask) > 0.0001f) ||
                   math.any(math.abs(current.FogDensityQualityDepth - cached.FogDensityQualityDepth) > 0.0001f) ||
                   math.any(math.abs(current.CausticOffset - cached.CausticOffset) > 0.0001f);
        }

        private static AbyssAtmospherePresentationDTO CreateInvalidAbyssAtmospherePresentation()
        {
            AbyssAtmospherePresentationDTO payload;
            payload.AbsorptionColorAndDepthMask = new float4(-1f);
            payload.FogDensityQualityDepth = new float4(-1f);
            payload.CausticOffset = new float4(-1f);
            return payload;
        }

        private static void ValidateAbyssAtmospherePresentationLayout()
        {
            int sizeBytes = UnsafeUtility.SizeOf<AbyssAtmospherePresentationDTO>();
            if (sizeBytes == AbyssAtmospherePresentationDtoStrideBytes &&
                (sizeBytes & 7) == 0)
            {
                return;
            }

            GlobalTelemetryBus.PublishPerformanceWarning(
                _AbyssAtmosphereLayoutWarningHash,
                _AbyssAtmosphereLayoutContextHash,
                math.max(1f, sizeBytes));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float3 ApproximateExpNegPositive(float3 x)
        {
            float3 clamped = math.clamp(x, float3.zero, new float3(8f));
            float3 x2 = clamped * clamped;
            float3 x3 = x2 * clamped;
            float3 numerator = 120f - (60f * clamped) + (12f * x2) - x3;
            float3 denominator = 120f + (60f * clamped) + (12f * x2) + x3;
            return math.saturate(numerator / math.max(denominator, new float3(0.0001f)));
        }

        #endregion

        #region ══════════ Profile Resolution ══════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private AtmosphereProfile ResolveProfile(EnvironmentState state)
        {
            if (state == EnvironmentState.ECLIPSE)
                return _profileEclipse != null ? _profileEclipse : _profileDay;

            if (state == EnvironmentState.UNDERWATER)
                return _profileUnderwater != null ? _profileUnderwater : _profileDay;

            if (_activeMatrixProfile != null)
                return _activeMatrixProfile;

            if (_activeBiomeProfile != null)
                return _activeBiomeProfile;

            AtmosphereProfile profile = state switch
            {
                EnvironmentState.SURFACE_DAY   => _profileDay,
                EnvironmentState.SURFACE_NIGHT => _profileNight,
                _                              => _profileDay
            };

            return profile != null ? profile : _profileDay;
        }

        #endregion

        #region ══════════ Biome Handler ══════════

        private void ApplyProceduralBiomeInfluenceAtmosphere(float deltaTime)
        {
            if (!Application.isPlaying || _currentState == EnvironmentState.ECLIPSE)
                return;

            RefreshProceduralBiomeInfluenceSnapshotIfNeeded();
            if (!_hasBiomeInfluenceAtmosphere)
                return;

            AtmosphereProfile primary = ResolveMatrixAtmosphereProfile(_biomeInfluencePrimaryProfile);
            if (primary == null)
                return;

            AtmosphereProfile secondary = ResolveMatrixAtmosphereProfile(_biomeInfluenceSecondaryProfile);
            AtmosphereSnapshot target = _currentValues;
            if (secondary == null || _currentBiomeInfluence.Blend255 == 0)
            {
                target.fogColor = primary.fogColor;
                target.fogDensity = primary.fogDensity;
                target.fogAttenuationDistance = math.max(0.001f, primary.fogAttenuationDistanceMeters);
                ApplySmoothedProceduralBiomeFog(in target, deltaTime);
                return;
            }

            float blend = _currentBiomeInfluence.Blend255 * (1f / 255f);
            target.fogColor = Color.Lerp(primary.fogColor, secondary.fogColor, blend);
            target.fogDensity = math.lerp(primary.fogDensity, secondary.fogDensity, blend);
            target.fogAttenuationDistance = math.max(
                0.001f,
                math.lerp(primary.fogAttenuationDistanceMeters, secondary.fogAttenuationDistanceMeters, blend));
            ApplySmoothedProceduralBiomeFog(in target, deltaTime);
        }

        private void ApplySmoothedProceduralBiomeFog(in AtmosphereSnapshot target, float deltaTime)
        {
            if (!_hasSmoothedBiomeInfluenceAtmosphere)
            {
                _smoothedBiomeInfluenceAtmosphere = _currentValues;
                _hasSmoothedBiomeInfluenceAtmosphere = true;
            }

            float blend = math.saturate(math.max(0f, deltaTime) * math.rcp(math.max(0.1f, _biomeInfluenceBlendSeconds)));
            _smoothedBiomeInfluenceAtmosphere.fogColor = Color.Lerp(
                _smoothedBiomeInfluenceAtmosphere.fogColor,
                target.fogColor,
                blend);
            _smoothedBiomeInfluenceAtmosphere.fogDensity = math.lerp(
                _smoothedBiomeInfluenceAtmosphere.fogDensity,
                target.fogDensity,
                blend);
            _smoothedBiomeInfluenceAtmosphere.fogAttenuationDistance = math.max(
                0.001f,
                math.lerp(
                    _smoothedBiomeInfluenceAtmosphere.fogAttenuationDistance,
                    target.fogAttenuationDistance,
                    blend));

            _currentValues.fogColor = _smoothedBiomeInfluenceAtmosphere.fogColor;
            _currentValues.fogDensity = _smoothedBiomeInfluenceAtmosphere.fogDensity;
            _currentValues.fogAttenuationDistance = _smoothedBiomeInfluenceAtmosphere.fogAttenuationDistance;
        }

        private void RefreshProceduralBiomeInfluenceSnapshotIfNeeded()
        {
            float now = ResolveAtmosphereTimelineClockSeconds();
            if (now < _nextBiomeInfluenceRefreshTime)
                return;

            _nextBiomeInfluenceRefreshTime = now + math.max(0.05f, _biomeInfluenceRefreshInterval);

            Transform sampleTransform = _playerCameraTransform != null ? _playerCameraTransform : _playerTransform;
            if (_proceduralFieldSampler == null || sampleTransform == null)
            {
                ClearProceduralBiomeInfluenceState();
                return;
            }

            if (_proceduralFieldSampler.TrySampleBiomeInfluence(
                    sampleTransform.position,
                    out WorldProceduralFieldSampler.BiomeInfluenceCell influence,
                    out HectonBiomeMatrixProfile primary,
                    out HectonBiomeMatrixProfile secondary))
            {
                if (!ShouldCommitProceduralBiomeInfluence(in influence, sampleTransform.position))
                    return;

                _currentBiomeInfluence = influence;
                _biomeInfluencePrimaryProfile = primary;
                _biomeInfluenceSecondaryProfile = secondary;
                _hasBiomeInfluenceAtmosphere = primary != null;
                return;
            }

            ClearProceduralBiomeInfluenceState();
        }

        private bool ShouldCommitProceduralBiomeInfluence(
            in WorldProceduralFieldSampler.BiomeInfluenceCell influence,
            Vector3 samplePosition)
        {
            byte nextPrimaryId = influence.PrimaryVisualFamilyId;
            if (!_hasStableBiomeInfluencePrimary ||
                nextPrimaryId == _stableBiomeInfluencePrimaryId ||
                _biomeInfluenceTransitionHysteresisMeters <= 0f)
            {
                _stableBiomeInfluencePrimaryId = nextPrimaryId;
                _hasStableBiomeInfluencePrimary = true;
                _hasPendingBiomeInfluencePrimary = false;
                return true;
            }

            if (!TryBuildAupFromRuntimePosition(samplePosition, out AbsoluteUniversePosition currentAup))
                return false;

            if (!_hasPendingBiomeInfluencePrimary ||
                _pendingBiomeInfluencePrimaryId != nextPrimaryId)
            {
                _pendingBiomeInfluencePrimaryId = nextPrimaryId;
                _pendingBiomeInfluencePrimaryAup = currentAup;
                _hasPendingBiomeInfluencePrimary = true;
                return false;
            }

            double requiredDistanceSq = (double)_biomeInfluenceTransitionHysteresisMeters *
                                        _biomeInfluenceTransitionHysteresisMeters;
            if (AbsoluteUniversePosition.DistanceSq(in currentAup, in _pendingBiomeInfluencePrimaryAup) < requiredDistanceSq)
                return false;

            _stableBiomeInfluencePrimaryId = nextPrimaryId;
            _hasPendingBiomeInfluencePrimary = false;
            return true;
        }

        private static bool TryBuildAupFromRuntimePosition(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private void ClearProceduralBiomeInfluenceHysteresis()
        {
            _hasStableBiomeInfluencePrimary = false;
            _hasPendingBiomeInfluencePrimary = false;
            _stableBiomeInfluencePrimaryId = 0;
            _pendingBiomeInfluencePrimaryId = 0;
            _pendingBiomeInfluencePrimaryAup = default;
        }

        private void ClearProceduralBiomeInfluenceState()
        {
            ClearProceduralBiomeInfluenceHysteresis();
            _hasBiomeInfluenceAtmosphere = false;
            _hasSmoothedBiomeInfluenceAtmosphere = false;
            _currentBiomeInfluence = default;
            _biomeInfluencePrimaryProfile = null;
            _biomeInfluenceSecondaryProfile = null;
        }

        private static AtmosphereProfile ResolveMatrixAtmosphereProfile(HectonBiomeMatrixProfile profile)
        {
            return profile != null && profile.familyProfile != null
                ? profile.familyProfile.atmosphereProfile
                : null;
        }

        private void HandleBiomeChanged(int biomeID)
        {
            _currentBiomeID = biomeID;

            // v2.1 OPT: O(1) dictionary lookup instead of O(n) linear search
            AtmosphereProfile biomeProfile = null;
            if (_biomeProfileDict != null && _biomeProfileDict.TryGetValue(biomeID, out var profile))
            {
                biomeProfile = profile;
            }

            _activeBiomeProfile = biomeProfile;
            _transitionOrigin   = _currentValues;
            _transitionProgress = 0f;
        }

        private void HandleMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            AtmosphereProfile nextProfile = profile != null && profile.familyProfile != null
                ? profile.familyProfile.atmosphereProfile
                : null;

            if (_activeMatrixProfile == nextProfile)
                return;

            _activeMatrixProfile = nextProfile;
            if (_currentState == EnvironmentState.UNDERWATER)
                return;

            _transitionOrigin = _currentValues;
            _transitionProgress = 0f;
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            HandleMatrixBiomeChanged(profile);
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
        }

        void IMapMagicBiomeEventListener.OnMapMagicBiomeChanged(int biomeId)
        {
            HandleBiomeChanged(biomeId);
        }

        #endregion

        #region ══════════ Public API ══════════

        public void TriggerEclipse(float duration)
        {
            if (duration <= 0f) return;
            _eclipseActive        = true;
            _eclipseRemainingTime = duration;
        }

        public void EndEclipse()
        {
            _eclipseActive        = false;
            _eclipseRemainingTime = 0f;
        }

        public void SetUnderwater(bool isUnderwater)
        {
            _underwaterExternalFlag = isUnderwater;
        }

        public void SetTimeOfDay(float normalized)
        {
            _cycleTimer = math.saturate(normalized) * _cycleDuration;
            double completedCycles = math.floor(_elapsedCycleTimeSeconds / _cycleDuration);
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;
        }

        public void SetWaterSurfaceLevel(float worldY)
        {
            _waterSurfaceY = SanitizeWaterSurfaceY(worldY);
        }

        public void SetPlayerTransform(Transform player)
        {
            _playerTransform = player;
            CachePlayerMovement();
        }

        public void SetCycleDuration(float seconds)
        {
            float normalized = _cycleDuration > 0f ? _cycleTimer / _cycleDuration : 0f;
            double completedCycles = _cycleDuration > 0f
                ? math.floor(_elapsedCycleTimeSeconds / _cycleDuration)
                : 0d;

            _cycleDuration = math.max(seconds, 1f);
            _cycleTimer = normalized * _cycleDuration;
            _elapsedCycleTimeSeconds = completedCycles * _cycleDuration + _cycleTimer;
        }

        public void SetTransitionSpeed(float speed)
        {
            _transitionSpeed = math.clamp(speed, 0.1f, 10f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SyncWaterSurfaceFromPlayerMovement()
        {
            if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                _waterSurfaceY = SanitizeWaterSurfaceY(movementState.WorldPosition.y + math.max(0f, movementState.DepthMeters));
                return;
            }

            if (HasPlayerRuntimeContext())
                return;

            if (_playerMovement == null)
                return;

            _waterSurfaceY = SanitizeWaterSurfaceY(_playerMovement.CurrentWaterSurfaceY);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolveSeaLevelY()
        {
            if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return SanitizeWaterSurfaceY(movementState.WorldPosition.y + math.max(0f, movementState.DepthMeters));

            if (!HasPlayerRuntimeContext() && _playerMovement != null)
                return SanitizeWaterSurfaceY(_playerMovement.CurrentWaterSurfaceY);

            return SanitizeWaterSurfaceY(_waterSurfaceY);
        }

        private static float SanitizeWaterSurfaceY(float worldY)
        {
            return math.isfinite(worldY) &&
                   math.abs(worldY) > 0.0001f &&
                   math.abs(worldY) <= 1000f
                ? worldY
                : DefaultWaterSurfaceY;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float ResolvePlayerDepth()
        {
            if (TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return math.max(0f, movementState.DepthMeters);

            if (HasPlayerRuntimeContext())
                return 0f;

            if (_playerMovement != null && math.isfinite(_playerMovement.CurrentDepth))
                return math.max(0f, _playerMovement.CurrentDepth);

            if (_playerCameraTransform != null)
                return math.max(0f, _waterSurfaceY - _playerCameraTransform.position.y);

            if (_playerTransform != null)
                return math.max(0f, _waterSurfaceY - _playerTransform.position.y);

            return 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryResolveMovementRuntimeState(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext == null ||
                !playerContext.IsInitialized ||
                !playerContext.TryGetMovementRuntimeState(out movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) == 0u ||
                !math.isfinite(movementState.DepthMeters) ||
                !math.all(math.isfinite(movementState.WorldPosition)))
            {
                movementState = default;
                return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool HasPlayerRuntimeContext()
        {
            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            return playerContext != null;
        }

        private void CachePlayerMovement()
        {
            _playerMovement = null;
            _playerCameraTransform = null;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
            {
                Transform contextTransform = playerContext.PlayerTransform;
                if (_playerTransform == null && contextTransform != null)
                    _playerTransform = contextTransform;

                if (_playerTransform != null && ReferenceEquals(_playerTransform, contextTransform))
                {
                    _playerMovement = playerContext.PlayerMovement;
                    Camera contextCamera = playerContext.PlayerCamera;
                    if (contextCamera != null)
                        _playerCameraTransform = contextCamera.transform;
                }
            }

            if (_playerTransform != null)
            {
                if (_playerMovement == null)
                    _playerTransform.TryGetComponent(out _playerMovement);

                if (_playerCameraTransform == null)
                {
                    Camera playerOwnedCamera = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Camera>(_playerTransform);
                    if (playerOwnedCamera != null)
                        _playerCameraTransform = playerOwnedCamera.transform;
                }
            }
        }

        private void CacheRegistryRuntimeReferences()
        {
            _playerRuntimeContext = GlobalRegistry.Player;
            _cachedCelestialEngine = GlobalRegistry.CelestialEngine;
            CacheRenderSettingsSunCold();
            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = GlobalRegistry.BiomeMatrix;
            if (_proceduralFieldSampler == null)
                _proceduralFieldSampler = GlobalRegistry.ProceduralFieldSampler;
        }

        private void CacheRenderSettingsSunCold()
        {
            if (_sunLight != null)
                return;

            if (Application.isPlaying && _renderSettingsSunFallbackChecked)
                return;

            _renderSettingsSunFallbackChecked = true;
            Light renderSettingsSun = RenderSettings.sun;
            if (renderSettingsSun != null && renderSettingsSun.type == LightType.Directional)
            {
                _sunLight = renderSettingsSun;
                EnsureAegirRingShadowCookie();
            }
        }

        private void RefreshBiomeMatrixDirectorFromRegistry()
        {
            if (!Application.isPlaying)
                return;

            if (_biomeMatrixDirector == null)
                _biomeMatrixDirector = GlobalRegistry.BiomeMatrix;
        }

        private void ApplyCurrentMatrixAtmosphereOverride()
        {
            if (!Application.isPlaying)
                return;

            if (_biomeMatrixDirector == null)
                return;

            HandleMatrixBiomeChanged(_biomeMatrixDirector.CurrentProfile);
        }

        public void SetOrbitalInclination(float degrees)
        {
            _orbitalInclination = math.clamp(degrees, 0f, 90f);
        }

        #endregion
    }
}
