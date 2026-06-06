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
using Hecton8.Core.Contracts.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Unity.Mathematics;
using UnityEngine;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

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

        private struct ListenerSlot
        {
            public ISoundscapeEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct SoundscapeListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public SoundscapeListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ISoundscapeEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ISoundscapeEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(ISoundscapeEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public ISoundscapeEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static SoundscapeListenerRegistry _listeners = new SoundscapeListenerRegistry(ListenerCapacity);
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: SoundscapeEventPayload[16] - bounded soundscape tier ring flushed by SystemDispatcher - owner: SoundscapeEvents
        private static SoundscapeEventPayload[] _pendingEvents = new SoundscapeEventPayload[PendingEventCapacity];
        // COLD ALLOC: SoundscapeEventPayload[16] - bounded next-frame soundscape ring for reentrant dispatch - owner: SoundscapeEvents
        private static SoundscapeEventPayload[] _nextFrameEvents = new SoundscapeEventPayload[PendingEventCapacity];
        private static int _pendingEventHead;
        private static int _pendingEventTail;
        private static int _pendingEventCount;
        private static int _nextFrameEventHead;
        private static int _nextFrameEventTail;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _pendingEventHead = 0;
            _pendingEventTail = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
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

        public static bool TryRaiseTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            if (_listeners.Count <= 0 || _pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            SoundscapeEventPayload payload = new SoundscapeEventPayload
            {
                OldTier = oldTier,
                NewTier = newTier
            };

            if (_isDispatching)
                return TryEnqueueNextFrame(payload);

            return TryEnqueuePending(payload);
        }

        [Obsolete("Soundscape producers must use TryRaiseTierChanged and handle bounded enqueue failure.", true)]
        public static void RaiseTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            TryRaiseTierChanged(oldTier, newTier);
        }

        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
            {
                DropPendingAmbient();
                return;
            }

            PromoteNextFrameEvents();
            if (_pendingEventCount <= 0)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && _pendingEventCount > 0)
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!TryDequeuePending(out SoundscapeEventPayload payload))
                    {
                        _pendingEventCount = 0;
                        break;
                    }

                    int count = _listeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ISoundscapeEventListener listener = _listeners.GetAt(i);
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

            if (_pendingEventCount > 0)
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        public static void DropPendingAmbient()
        {
            for (int i = 0; i < _pendingEventCount; i++)
            {
                int index = (_pendingEventHead + i) % PendingEventCapacity;
                _pendingEvents[index] = default;
            }

            for (int i = 0; i < _nextFrameEventCount; i++)
            {
                int index = (_nextFrameEventHead + i) % PendingEventCapacity;
                _nextFrameEvents[index] = default;
            }

            _pendingEventHead = 0;
            _pendingEventTail = 0;
            _pendingEventCount = 0;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        private static void DispatchToListener(ISoundscapeEventListener listener, SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            listener.OnSoundscapeTierChanged(oldTier, newTier);
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool IsDeferredRegisterPending(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void CancelDeferredRegister(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                int tail = _deferredRegisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredRegisterListeners, i + 1, _deferredRegisterListeners, i, tail);

                _deferredRegisterListeners[--_deferredRegisterCount].Clear();
                return;
            }
        }

        private static void CancelDeferredUnregister(ISoundscapeEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                int tail = _deferredUnregisterCount - i - 1;
                if (tail > 0)
                    Array.Copy(_deferredUnregisterListeners, i + 1, _deferredUnregisterListeners, i, tail);

                _deferredUnregisterListeners[--_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                ISoundscapeEventListener listener = _deferredUnregisterListeners[i].Listener;
                if (listener != null)
                    _listeners.TryUnregister(listener);

                _deferredUnregisterListeners[i].Clear();
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                RegisterImmediate(_deferredRegisterListeners[i].Listener);
                _deferredRegisterListeners[i].Clear();
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
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerRejectedWarningHash,
                ListenerContextHash,
                UnityEngine.Mathf.Max(1, _droppedListenerRegistrationCount));
        }

        private static bool TryEnqueuePending(SoundscapeEventPayload payload)
        {
            if (_pendingEventCount >= PendingEventCapacity)
                return false;

            _pendingEvents[_pendingEventTail] = payload;
            _pendingEventTail = (_pendingEventTail + 1) % PendingEventCapacity;
            _pendingEventCount++;
            return true;
        }

        private static bool TryEnqueueNextFrame(SoundscapeEventPayload payload)
        {
            if (_nextFrameEventCount >= PendingEventCapacity)
                return false;

            _nextFrameEvents[_nextFrameEventTail] = payload;
            _nextFrameEventTail = (_nextFrameEventTail + 1) % PendingEventCapacity;
            _nextFrameEventCount++;
            return true;
        }

        private static bool TryDequeuePending(out SoundscapeEventPayload payload)
        {
            if (_pendingEventCount <= 0)
            {
                payload = default;
                return false;
            }

            payload = _pendingEvents[_pendingEventHead];
            _pendingEvents[_pendingEventHead] = default;
            _pendingEventHead = (_pendingEventHead + 1) % PendingEventCapacity;
            _pendingEventCount--;
            return true;
        }

        private static void PromoteNextFrameEvents()
        {
            if (_pendingEventCount > 0 || _nextFrameEventCount <= 0)
                return;

            SoundscapeEventPayload[] swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventHead = _nextFrameEventHead;
            _pendingEventTail = _nextFrameEventTail;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventHead = 0;
            _nextFrameEventTail = 0;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public sealed class SoundscapeSystem : MonoBehaviour,
        ISlowTickable,
        ILateFrameTickable,
        IBiomeMatrixEventListener,
        ISoundscapeTierReadModel,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private const int MaxSignalDrainPerSlowTick = 16;
        private const int LowSignalDrainPerSlowTick = 4;

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

        [Header("Signal Corridor")]
        [SerializeField, Tooltip("One-based SpatialAudioManager AudioEvent table id for metal impact clang playback.")]
        private int impactClangAudioEventId = 1;
        [SerializeField, Range(0f, 1f), Tooltip("Minimum normalized impact intensity required before Soundscape queues a clang.")]
        private float impactClangMinimumIntensity = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("Volume multiplier applied to impact intensity before queuing clang audio.")]
        private float impactClangVolumeScale = 0.65f;
        [SerializeField, Range(0.1f, 3f), Tooltip("Base pitch for impact clang audio events.")]
        private float impactClangPitchBase = 0.95f;
        [SerializeField, Range(0f, 1f), Tooltip("Pitch lift applied from normalized impact intensity.")]
        private float impactClangPitchIntensityScale = 0.2f;

        // ══════════════════════════════════════════════════════════
        //  STATIC LIFECYCLE MIRROR
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SoundscapeTier _currentTier = SoundscapeTier.Surface;
        private SoundscapeTier _pendingShaderTier = SoundscapeTier.Surface;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _soundscapeTierShaderDirty;
        private bool _serviceRegistered;
        private bool _biomeMatrixRegistered;
        private bool _hotSwapRegistered;
        private IAudioService _audioService;
        private HectonMusicDirector _musicDirector;
        private int _lastMatrixBiomeId;
        private HectonMusicBiomeProfile _lastMatrixMusicProfile;
        private static SoundscapeSystem s_activeRuntimeInstance;

        private static readonly int _ShaderSoundscapeTier =
            Shader.PropertyToID("_SoundscapeDepthTier");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SoundscapeTier CurrentTier => _currentTier;

        byte ISoundscapeTierReadModel.CurrentTierCode => (byte)_currentTier;

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
            CacheAudioService(GlobalRegistry.Audio);
            TryRegisterService();
            TryRegister();
            TryRegisterLateFrame();
            TryRegisterBiomeMatrixEvents();
            TryRegisterHotSwapListener();

            ResolveSurvivalSystem();
            SyncMusicDirectorSoundscapeContext(_currentTier, survivalSystem != null ? survivalSystem.Depth : 0f);

            QueueSoundscapeShaderTier(_currentTier);
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterBiomeMatrixEvents();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterService();
            _audioService = null;
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregisterBiomeMatrixEvents();
            TryUnregisterLateFrame();
            TryUnregister();
            TryUnregisterService();
            _audioService = null;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrame()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _registeredLateFrame = false;
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered)
            {
                s_activeRuntimeInstance = this;
                return;
            }

            if (!Application.isPlaying)
                return;

            SoundscapeSystem registered = GlobalRegistry.Soundscape;
            if (registered != null && !ReferenceEquals(registered, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSoundscapeRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Soundscape, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSoundscapeRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
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

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            DrainSignals();

            if (survivalSystem == null && !ResolveSurvivalSystem())
            {
                SyncMusicDirectorSoundscapeContext(_currentTier, 0f);
                return;
            }

            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            SoundscapeTier newTier = CalculateTier(depth, _currentTier);
            SyncMusicDirectorSoundscapeContext(newTier, depth);

            if (newTier == _currentTier) return;

            SoundscapeTier oldTier = _currentTier;
            _currentTier = newTier;

            QueueSoundscapeShaderTier(newTier);
            SoundscapeEvents.TryRaiseTierChanged(oldTier, newTier);

            LogTierChanged();
        }

        public void LateFrameTick()
        {
            if (!_soundscapeTierShaderDirty)
                return;

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)_pendingShaderTier);
            _soundscapeTierShaderDirty = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void DrainSignals()
        {
            IAudioService audio = _audioService;
            float qualityWeight01 = ResolveSoundscapeQualityWeight01();
            int signalDrainBudget = ResolveSignalDrainBudget(qualityWeight01);
            ReadOnlySpan<ImpactSignal> signals = SignalBus<ImpactSignal>.GetFrameSnapshot();
            int signalCount = math.min(signalDrainBudget, signals.Length);
            for (int i = 0; i < signalCount; i++)
            {
                ImpactSignal signal = signals[i];
                HandleImpactSignal(in signal, audio, qualityWeight01);
            }
        }

        private void QueueSoundscapeShaderTier(SoundscapeTier tier)
        {
            _pendingShaderTier = tier;
            _soundscapeTierShaderDirty = true;
        }

        private static int ResolveSignalDrainBudget(float qualityWeight01)
        {
            float quality = SmoothQuality01(qualityWeight01);
            int budget = (int)math.round(math.lerp(LowSignalDrainPerSlowTick, MaxSignalDrainPerSlowTick, quality));
            return math.clamp(budget, LowSignalDrainPerSlowTick, MaxSignalDrainPerSlowTick);
        }

        private static float ResolveSoundscapeQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float SmoothQuality01(float qualityWeight01)
        {
            float q = math.saturate(qualityWeight01);
            return q * q * (3f - 2f * q);
        }

        private void HandleImpactSignal(in ImpactSignal signal, IAudioService audio, float qualityWeight01)
        {
            if (impactClangAudioEventId <= 0 || !float.IsFinite(signal.Intensity))
                return;

            float safeIntensity = math.saturate(signal.Intensity);
            if (safeIntensity < impactClangMinimumIntensity)
                return;

            if (audio == null || !audio.IsInitialized)
                return;

            float3 runtimePosition = signal.PointAup.ToRuntimeFloat3();
            Vector3 position = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            float volume = math.saturate(safeIntensity * impactClangVolumeScale);
            float pitchWeight = SmoothQuality01(qualityWeight01);
            float pitch = math.clamp(impactClangPitchBase + safeIntensity * impactClangPitchIntensityScale * pitchWeight, 0.1f, 3f);
            CoreAudioEvent audioEvent = new CoreAudioEvent((uint)impactClangAudioEventId, position, volume, pitch);
            audio.QueueAudioEvent(in audioEvent);
        }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[Soundscape] Tier changed.");
#endif
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            return survivalSystem != null;
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
            director.SetSoundscapeTierContext(CalculateTier(depthMeters, _currentTier), depthMeters);
            _lastMatrixBiomeId = profile != null ? profile.matrixIndex : 0;
            _lastMatrixMusicProfile = director.ActiveMatrixBiomeMusicProfile;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                CacheAudioService(currentService as IAudioService);

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            }
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
            {
                CacheAudioService(currentService as IAudioService);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registered = false;
                _registeredLateFrame = false;
                if (currentService != null && isActiveAndEnabled)
                {
                    TryRegister();
                    TryRegisterLateFrame();
                }
            }
        }

        private bool TryResolveMusicDirector(out HectonMusicDirector director)
        {
            if (_musicDirector != null)
            {
                director = _musicDirector;
                return true;
            }

            director = GlobalRegistry.MusicDirector;
            _musicDirector = director;
            return _musicDirector != null;
        }

        private void SyncMusicDirectorSoundscapeContext(SoundscapeTier tier, float depthMeters)
        {
            if (!TryResolveMusicDirector(out HectonMusicDirector director))
                return;

            director.SetSoundscapeTierContext(tier, depthMeters);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = audioService;
        }

    }
}
