// ============================================================================
// HECTON-8 — SoundscapeSystem.cs
// Sistema zvukovyh sloev po glubine.
//
// LOR (lor1 — Zvukovoy dizayn, detalnaya karta):
//   POVERHNOST:    volny, veter, gravitatsionnyy gul Aegira
//   0-150m:         "penie" vody, ryby, metallicheskie stony moduley
//   150-500m:       tishina narastaet, skrip skafandra
//   500-1000m:      tolko skafandr i dyhanie, biolyum schelchki
//   1000-2000m:     mehanicheskiy skrip, postoyannyy gul davleniya
//   2000-4000m:     subzvuk davleniya, vibratsiya kontrollera
//   4000-5000m:     termalnye potoki, treskotnya mineralnyh bashen
//
// ARHITEKTURA:
//   • Publikuet _SoundscapeDepthTier v sheydery.
//   • Publikuet sobytiya dlya AudioManager (smena embienta).
//   • ISlowTickable — obnovlenie tira raz v 0.5s.
//   • Integriruetsya s DepthZoneDirector.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    public enum SoundscapeTier
    {
        Surface     = 0,   // Poverhnost
        Shallow     = 1,   // 0-150m
        Twilight    = 2,   // 150-500m
        Darkness    = 3,   // 500-1000m
        Abyss       = 4,   // 1000-2000m
        DeepAbyss   = 5,   // 2000-4000m
        Thermal     = 6    // 4000-5000m
    }

    /// <summary>
    /// Listener contract for queue-backed soundscape tier notifications.
    /// </summary>
    public interface ISoundscapeEventListener
    {
        /// <summary>Called when the active soundscape tier changes.</summary>
        /// <param name="oldTier">Previous tier.</param>
        /// <param name="newTier">New tier.</param>
        void OnSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier);
    }

    public static class SoundscapeEvents
    {
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 16;
        private const uint ListenerRejectedWarningHash = 0x5353524Au;
        private const uint ListenerExceptionWarningHash = 0x53534558u;
        private const uint ListenerContextHash = 0x53534C53u;

        private struct SoundscapeEventPayload
        {
            public SoundscapeTier OldTier;
            public SoundscapeTier NewTier;
        }

        private static readonly RegistryBucket<ISoundscapeEventListener> _listeners = new RegistryBucket<ISoundscapeEventListener>(ListenerCapacity);
        private static readonly ISoundscapeEventListener[] _deferredRegisterListeners = new ISoundscapeEventListener[ListenerCapacity];
        private static readonly ISoundscapeEventListener[] _deferredUnregisterListeners = new ISoundscapeEventListener[ListenerCapacity];
        private static NativeQueue<SoundscapeEventPayload> _pendingEvents;
        private static NativeQueue<SoundscapeEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SoundscapeEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SoundscapeEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterListeners.Length);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterListeners.Length);
        }

        /// <summary>Zvukovoy tir izmenilsya. (oldTier, newTier)</summary>
        public static void Register(ISoundscapeEventListener listener)
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

        public static void Unregister(ISoundscapeEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                return;

            if (_listeners.Count <= 0)
                DropPendingAmbient();
        }

        public static void RaiseTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            if (_listeners.Count <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(new SoundscapeEventPayload
                {
                    OldTier = oldTier,
                    NewTier = newTier
                });
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(new SoundscapeEventPayload
            {
                OldTier = oldTier,
                NewTier = newTier
            });
            _pendingEventCount++;
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DropPendingAmbient();
                return;
            }

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out SoundscapeEventPayload payload))
                        break;

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    ISoundscapeEventListener[] rawArray = _listeners.RawArray;
                    int count = _listeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ISoundscapeEventListener listener = rawArray[i];
                        if (listener != null && !IsDeferredUnregisterPending(listener))
                            DispatchToListener(listener, payload.OldTier, payload.NewTier);
                    }
                }
            }
            finally
            {
                _isDispatching = false;
                ApplyDeferredListenerMutations();
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        public static void DropPendingAmbient()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void DispatchToListener(ISoundscapeEventListener listener, SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            try
            {
                listener.OnSoundscapeTierChanged(oldTier, newTier);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(ISoundscapeEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            CancelDeferredUnregister(listener);
            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(ISoundscapeEventListener listener)
        {
            CancelDeferredRegister(listener);
            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool IsDeferredRegisterPending(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void CancelDeferredRegister(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                int tail = _deferredRegisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredRegisterListeners, i + 1, _deferredRegisterListeners, i, tail);

                _deferredRegisterListeners[--_deferredRegisterCount] = null;
                return;
            }
        }

        private static void CancelDeferredUnregister(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                int tail = _deferredUnregisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredUnregisterListeners, i + 1, _deferredUnregisterListeners, i, tail);

                _deferredUnregisterListeners[--_deferredUnregisterCount] = null;
                return;
            }
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISoundscapeEventListener listener = _deferredUnregisterListeners[i];
                if (listener != null)
                    _listeners.TryUnregister(listener);

                _deferredUnregisterListeners[i] = null;
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                RegisterImmediate(_deferredRegisterListeners[i]);
                _deferredRegisterListeners[i] = null;
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropPendingAmbient();
        }

        private static void RegisterImmediate(ISoundscapeEventListener listener)
        {
            if (listener == null || _listeners.Contains(listener))
                return;

            if (_listeners.Count >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
                UnityEngine.Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException(Exception exception)
        {
            _listenerExceptionCount = UnityEngine.Mathf.Min(_listenerExceptionCount + 1, int.MaxValue);
            LogListenerDispatchException(exception);

            int frame = UnityEngine.Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerExceptionWarningHash,
                ListenerContextHash,
                UnityEngine.Mathf.Max(1, _listenerExceptionCount));
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SoundscapeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SoundscapeEventPayload>[16] - soundscape tier event lane flushed by SystemDispatcher - owner: SoundscapeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(SoundscapeEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<SoundscapeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SoundscapeEventPayload>[16] - next-frame soundscape events raised by listeners - owner: SoundscapeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(SoundscapeEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
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

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out SoundscapeEventPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public sealed class SoundscapeSystem : MonoBehaviour, ISlowTickable, IBiomeMatrixEventListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Depth Thresholds (meters) ───────────────")]
        [SerializeField] private float shallowDepth   = 0f;
        [SerializeField] private float twilightDepth  = 150f;
        [SerializeField] private float darknessDepth  = 500f;
        [SerializeField] private float abyssDepth     = 1000f;
        [SerializeField] private float deepAbyssDepth = 2000f;
        [SerializeField] private float thermalDepth   = 4000f;
        [SerializeField] private float tierDepthHysteresis = 18f;

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static SoundscapeSystem Instance => GlobalRegistry.Soundscape;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SoundscapeTier _currentTier = SoundscapeTier.Surface;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _biomeMatrixRegistered;
        private HectonMusicDirector _musicDirector;
        private int _lastMatrixBiomeId;
        private HectonMusicBiomeProfile _lastMatrixMusicProfile;

        private static readonly int _ShaderSoundscapeTier =
            Shader.PropertyToID("_SoundscapeDepthTier");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SoundscapeTier CurrentTier => _currentTier;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            SoundscapeSystem registered = GlobalRegistry.Soundscape;
            if (Application.isPlaying && registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }
        }

        private void OnEnable()
        {
            TryRegisterService();
            TryRegister();
            TryRegisterBiomeMatrixEvents();

            ResolveSurvivalSystem();

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)_currentTier);
        }

        private void OnDisable()
        {
            TryUnregisterBiomeMatrixEvents();
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterBiomeMatrixEvents();
            TryUnregister();
            TryUnregisterService();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            SoundscapeSystem registered = GlobalRegistry.Soundscape;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSoundscapeRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Soundscape, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSoundscapeRuntime(this);
            _serviceRegistered = false;
        }

        private void TryRegisterBiomeMatrixEvents()
        {
            if (_biomeMatrixRegistered || !Application.isPlaying)
                return;

            BiomeMatrixEvents.Register(this);
            _biomeMatrixRegistered = true;
        }

        private void TryUnregisterBiomeMatrixEvents()
        {
            if (!_biomeMatrixRegistered)
                return;

            BiomeMatrixEvents.Unregister(this);
            _biomeMatrixRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            SoundscapeTier newTier = CalculateTier(depth, _currentTier);

            if (newTier == _currentTier) return;

            SoundscapeTier oldTier = _currentTier;
            _currentTier = newTier;

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)newTier);
            SoundscapeEvents.RaiseTierChanged(oldTier, newTier);

            LogTierChanged();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private SoundscapeTier CalculateTier(float depth, SoundscapeTier currentTier)
        {
            float hysteresis = Mathf.Max(0f, tierDepthHysteresis);

            switch (currentTier)
            {
                case SoundscapeTier.Surface:
                    return depth >= shallowDepth + hysteresis
                        ? SoundscapeTier.Shallow
                        : SoundscapeTier.Surface;

                case SoundscapeTier.Shallow:
                    if (depth < shallowDepth - hysteresis)
                        return SoundscapeTier.Surface;
                    if (depth >= twilightDepth + hysteresis)
                        return SoundscapeTier.Twilight;
                    return SoundscapeTier.Shallow;

                case SoundscapeTier.Twilight:
                    if (depth < twilightDepth - hysteresis)
                        return SoundscapeTier.Shallow;
                    if (depth >= darknessDepth + hysteresis)
                        return SoundscapeTier.Darkness;
                    return SoundscapeTier.Twilight;

                case SoundscapeTier.Darkness:
                    if (depth < darknessDepth - hysteresis)
                        return SoundscapeTier.Twilight;
                    if (depth >= abyssDepth + hysteresis)
                        return SoundscapeTier.Abyss;
                    return SoundscapeTier.Darkness;

                case SoundscapeTier.Abyss:
                    if (depth < abyssDepth - hysteresis)
                        return SoundscapeTier.Darkness;
                    if (depth >= deepAbyssDepth + hysteresis)
                        return SoundscapeTier.DeepAbyss;
                    return SoundscapeTier.Abyss;

                case SoundscapeTier.DeepAbyss:
                    if (depth < deepAbyssDepth - hysteresis)
                        return SoundscapeTier.Abyss;
                    if (depth >= thermalDepth + hysteresis)
                        return SoundscapeTier.Thermal;
                    return SoundscapeTier.DeepAbyss;

                case SoundscapeTier.Thermal:
                    return depth < thermalDepth - hysteresis
                        ? SoundscapeTier.DeepAbyss
                        : SoundscapeTier.Thermal;

                default:
                    if (depth < shallowDepth)
                        return SoundscapeTier.Surface;
                    if (depth < twilightDepth)
                        return SoundscapeTier.Shallow;
                    if (depth < darknessDepth)
                        return SoundscapeTier.Twilight;
                    if (depth < abyssDepth)
                        return SoundscapeTier.Darkness;
                    if (depth < deepAbyssDepth)
                        return SoundscapeTier.Abyss;
                    if (depth < thermalDepth)
                        return SoundscapeTier.DeepAbyss;
                    return SoundscapeTier.Thermal;
            }
        }

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogTierChanged()
        {
            Debug.Log("[Soundscape] Tier changed.");
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        void IBiomeMatrixEventListener.OnMatrixBiomeChanged(HectonBiomeMatrixProfile profile)
        {
            int matrixBiomeId = profile != null ? profile.matrixIndex : 0;
            if (matrixBiomeId == _lastMatrixBiomeId && _lastMatrixMusicProfile != null)
                return;

            if (!TryResolveMusicDirector(out HectonMusicDirector director))
                return;

            director.SetMatrixBiomeProfile(profile);
            _lastMatrixBiomeId = matrixBiomeId;
            _lastMatrixMusicProfile = director.ActiveMatrixBiomeMusicProfile;
        }

        void IBiomeMatrixEventListener.OnDepthTierChanged(int depthTier, float depthMeters)
        {
            if (!TryResolveMusicDirector(out HectonMusicDirector director))
                return;

            HectonBiomeMatrixProfile profile = BiomeMatrixDirector.ActiveRuntimeInstance != null
                ? BiomeMatrixDirector.ActiveRuntimeInstance.CurrentProfile
                : null;
            director.SetMatrixBiomeProfile(profile);
            _lastMatrixBiomeId = profile != null ? profile.matrixIndex : 0;
            _lastMatrixMusicProfile = director.ActiveMatrixBiomeMusicProfile;
        }

        private bool TryResolveMusicDirector(out HectonMusicDirector director)
        {
            if (_musicDirector != null)
            {
                director = _musicDirector;
                return true;
            }

            director = GlobalRegistry.MusicDirector;
            if (director == null)
                HectonMusicDirector.TryGetInstance(out director);

            _musicDirector = director;
            return _musicDirector != null;
        }
    }
}
