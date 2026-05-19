using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;
using AudioEvent = Hecton8.Core.Contracts.Signals.AudioEvent;

namespace Hecton8.Audio
{
    public enum ProceduralAudioPingKind : byte
    {
        Sonar = 0,
        PredatorKill = 1,
        MeteorBoom = 2,
        MechanicalWhirr = 3,
        LeviathanRoar = 4,
        AirRelease = 5
    }

    /// <summary>
    /// Zero-allocation payload for sample-accurate procedural audio triggers.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public readonly struct AudioPingTriggerInfo
    {
        /// <summary>
        /// Creates a new audio ping trigger payload.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Audible chirp duration in seconds.</param>
        public AudioPingTriggerInfo(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = intensity;
            ChirpDurationSeconds = chirpDurationSeconds;
            WorldPosition = Vector3.zero;
            AcousticTransmission01 = 1f;
            LowPassCutoffHz = 22000f;
            Kind = ProceduralAudioPingKind.Sonar;
            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0u;
        }

        public AudioPingTriggerInfo(
            Vector3 worldPosition,
            float intensity,
            float chirpDurationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            ProceduralAudioPingKind kind)
        {
            StartSampleFrame = 0L;
            SampleRate = 1;
            Intensity = Mathf.Clamp01(intensity);
            ChirpDurationSeconds = Mathf.Max(0f, chirpDurationSeconds);
            WorldPosition = worldPosition;
            AcousticTransmission01 = Mathf.Clamp01(acousticTransmission01);
            LowPassCutoffHz = Mathf.Clamp(lowPassCutoffHz, 80f, 22000f);
            Kind = kind;
            Reserved0 = 0;
            Reserved1 = 0;
            Reserved2 = 0u;
        }

        /// <summary>Exact output-sample frame where the ping started rendering.</summary>
        [FieldOffset(0)]
        public readonly long StartSampleFrame;

        /// <summary>Audio output sample rate used to resolve the frame timestamp.</summary>
        [FieldOffset(8)]
        public readonly int SampleRate;

        /// <summary>Normalized ping intensity in the 0..1 range.</summary>
        [FieldOffset(12)]
        public readonly float Intensity;

        /// <summary>Primary chirp duration in seconds.</summary>
        [FieldOffset(16)]
        public readonly float ChirpDurationSeconds;

        /// <summary>World-space source for diegetic procedural pings.</summary>
        [FieldOffset(20)]
        public readonly Vector3 WorldPosition;

        /// <summary>Acoustic occlusion transmission in the 0..1 range.</summary>
        [FieldOffset(32)]
        public readonly float AcousticTransmission01;

        /// <summary>Low-pass cutoff after acoustic occlusion.</summary>
        [FieldOffset(36)]
        public readonly float LowPassCutoffHz;

        /// <summary>Semantic route for procedural rendering.</summary>
        [FieldOffset(40)]
        public readonly ProceduralAudioPingKind Kind;
        [FieldOffset(41)]
        public readonly byte Reserved0;
        [FieldOffset(42)]
        public readonly ushort Reserved1;
        [FieldOffset(44)]
        public readonly uint Reserved2;
    }

    /// <summary>
    /// Zero-allocation habitat pressure impulse consumed by structural granular synthesis.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct HullStressSignal
    {
        /// <summary>
        /// Creates a hull stress signal from a pressure derivative snapshot.
        /// </summary>
        public HullStressSignal(
            Vector3 worldPosition,
            float stress01,
            float pressureDelta,
            float depthMeters,
            float pitchScale,
            float acousticTransmission01 = 1f,
            float lowPassCutoffHz = 22000f,
            float acousticDelaySeconds = 0f)
        {
            Vector3 safeWorldPosition = SanitizeWorldPosition(worldPosition);
            WorldPosition = safeWorldPosition;
            SourceAup = AbsoluteUniversePosition.FromRuntimePosition(safeWorldPosition);
            Stress01 = Mathf.Clamp01(SanitizeFiniteValue(stress01));
            PressureDelta = SanitizeFiniteValue(pressureDelta);
            DepthMeters = Mathf.Max(0f, SanitizeFiniteValue(depthMeters));
            PitchScale = Mathf.Max(0.1f, SanitizeFiniteValue(pitchScale));
            AcousticTransmission01 = Mathf.Clamp01(SanitizeFiniteValue(acousticTransmission01));
            LowPassCutoffHz = Mathf.Clamp(SanitizeFiniteOrDefault(lowPassCutoffHz, 22000f), 80f, 22000f);
            AcousticDelaySeconds = Mathf.Max(0f, SanitizeFiniteValue(acousticDelaySeconds));
            Reserved0 = 0u;
            Reserved1 = 0u;
        }

        /// <summary>
        /// Creates a hull stress signal while preserving an already-authoritative AUP source snapshot.
        /// </summary>
        public HullStressSignal(
            in AbsoluteUniversePosition sourceAup,
            Vector3 worldPosition,
            float stress01,
            float pressureDelta,
            float depthMeters,
            float pitchScale,
            float acousticTransmission01 = 1f,
            float lowPassCutoffHz = 22000f,
            float acousticDelaySeconds = 0f)
        {
            WorldPosition = SanitizeWorldPosition(worldPosition);
            SourceAup = sourceAup;
            Stress01 = Mathf.Clamp01(SanitizeFiniteValue(stress01));
            PressureDelta = SanitizeFiniteValue(pressureDelta);
            DepthMeters = Mathf.Max(0f, SanitizeFiniteValue(depthMeters));
            PitchScale = Mathf.Max(0.1f, SanitizeFiniteValue(pitchScale));
            AcousticTransmission01 = Mathf.Clamp01(SanitizeFiniteValue(acousticTransmission01));
            LowPassCutoffHz = Mathf.Clamp(SanitizeFiniteOrDefault(lowPassCutoffHz, 22000f), 80f, 22000f);
            AcousticDelaySeconds = Mathf.Max(0f, SanitizeFiniteValue(acousticDelaySeconds));
            Reserved0 = 0u;
            Reserved1 = 0u;
        }

        /// <summary>World-space origin for portal routing and AUP conversion.</summary>
        [FieldOffset(48)]
        public readonly Vector3 WorldPosition;

        /// <summary>AUP origin snapshot used to survive floating-origin shifts before dispatch.</summary>
        [FieldOffset(0)]
        public readonly AbsoluteUniversePosition SourceAup;

        /// <summary>Normalized structural stress in the [0..1] range.</summary>
        [FieldOffset(60)]
        public readonly float Stress01;

        /// <summary>Signed pressure derivative or compression delta that excited the hull.</summary>
        [FieldOffset(64)]
        public readonly float PressureDelta;

        /// <summary>Depth in meters used for pitch and density cheats.</summary>
        [FieldOffset(68)]
        public readonly float DepthMeters;

        /// <summary>Pitch multiplier consumed by the fallback clip or granular renderer.</summary>
        [FieldOffset(72)]
        public readonly float PitchScale;

        /// <summary>Portal/path transmission in the [0..1] range.</summary>
        [FieldOffset(76)]
        public readonly float AcousticTransmission01;

        /// <summary>Portal/path low-pass cutoff in hertz.</summary>
        [FieldOffset(80)]
        public readonly float LowPassCutoffHz;

        /// <summary>Portal/path delay in seconds.</summary>
        [FieldOffset(84)]
        public readonly float AcousticDelaySeconds;
        [FieldOffset(88)]
        public readonly uint Reserved0;
        [FieldOffset(92)]
        public readonly uint Reserved1;

        internal static float SanitizeFiniteValue(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        internal static float SanitizeFiniteOrDefault(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        internal static Vector3 SanitizeWorldPosition(Vector3 value)
        {
            return new Vector3(
                SanitizeFiniteValue(value.x),
                SanitizeFiniteValue(value.y),
                SanitizeFiniteValue(value.z));
        }
    }

    /// <summary>
    /// Zero-allocation payload for habitat structural stress groan synthesis.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public readonly struct StructuralStressAudioInfo
    {
        /// <summary>
        /// Creates a structural stress audio payload.
        /// </summary>
        public StructuralStressAudioInfo(Vector3 worldPosition, float stress01, float pitchScale)
        {
            Vector3 safeWorldPosition = HullStressSignal.SanitizeWorldPosition(worldPosition);
            WorldPosition = safeWorldPosition;
            SourceAup = AbsoluteUniversePosition.FromRuntimePosition(safeWorldPosition);
            Stress01 = Mathf.Clamp01(HullStressSignal.SanitizeFiniteValue(stress01));
            PitchScale = Mathf.Max(0.1f, HullStressSignal.SanitizeFiniteValue(pitchScale));
            PressureDelta = 0f;
            DepthMeters = 0f;
            AcousticTransmission01 = 1f;
            LowPassCutoffHz = 22000f;
            AcousticDelaySeconds = 0f;
            Reserved0 = 0u;
            Reserved1 = 0u;
        }

        /// <summary>
        /// Creates a structural stress audio payload from the canonical pressure signal.
        /// </summary>
        public StructuralStressAudioInfo(in HullStressSignal signal)
        {
            WorldPosition = signal.WorldPosition;
            SourceAup = signal.SourceAup;
            Stress01 = signal.Stress01;
            PitchScale = signal.PitchScale;
            PressureDelta = signal.PressureDelta;
            DepthMeters = signal.DepthMeters;
            AcousticTransmission01 = signal.AcousticTransmission01;
            LowPassCutoffHz = signal.LowPassCutoffHz;
            AcousticDelaySeconds = signal.AcousticDelaySeconds;
            Reserved0 = 0u;
            Reserved1 = 0u;
        }

        /// <summary>World-space origin for spatial routing.</summary>
        [FieldOffset(48)]
        public readonly Vector3 WorldPosition;

        /// <summary>AUP origin snapshot used to survive floating-origin shifts before dispatch.</summary>
        [FieldOffset(0)]
        public readonly AbsoluteUniversePosition SourceAup;

        /// <summary>Normalized edge stress in the [0..1] range.</summary>
        [FieldOffset(60)]
        public readonly float Stress01;

        /// <summary>Pitch multiplier consumed by the renderer.</summary>
        [FieldOffset(64)]
        public readonly float PitchScale;

        /// <summary>Signed pressure derivative that triggered the structural sound.</summary>
        [FieldOffset(68)]
        public readonly float PressureDelta;

        /// <summary>Depth snapshot in meters used for low-cost pitch/density cheats.</summary>
        [FieldOffset(72)]
        public readonly float DepthMeters;

        /// <summary>Portal/path transmission in the [0..1] range.</summary>
        [FieldOffset(76)]
        public readonly float AcousticTransmission01;

        /// <summary>Portal/path low-pass cutoff in hertz.</summary>
        [FieldOffset(80)]
        public readonly float LowPassCutoffHz;

        /// <summary>Portal/path delay in seconds.</summary>
        [FieldOffset(84)]
        public readonly float AcousticDelaySeconds;
        [FieldOffset(88)]
        public readonly uint Reserved0;
        [FieldOffset(92)]
        public readonly uint Reserved1;
    }

    public enum AudioEventKind : byte
    {
        None = 0,
        AudioPing = 1,
        StructuralStress = 2
    }

    /// <summary>
    /// Listener contract for deferred procedural audio notifications.
    /// </summary>
    public interface IProceduralAudioEventListener
    {
        /// <summary>Called after the procedural renderer starts a sonar ping.</summary>
        /// <param name="info">Sample-accurate ping payload.</param>
        void OnAudioPingTriggered(in AudioPingTriggerInfo info);

        /// <summary>Called when a structure emits audible stress.</summary>
        /// <param name="info">Structural stress audio payload.</param>
        void OnStructuralStressTriggered(in StructuralStressAudioInfo info);
    }

    /// <summary>
    /// Queue-backed main-thread bridge for sample-accurate procedural audio triggers.
    /// </summary>
    public static class ProceduralAudioEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingAudioPingCapacity = 8;
        private const int PendingStructuralStressCapacity = 8;
        private const int PendingAudioEventCapacity = PendingAudioPingCapacity + PendingStructuralStressCapacity;
        private static readonly uint _overflowWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Overflow"));
        private static readonly uint _audioPingQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.AudioPing"));
        private static readonly uint _structuralStressQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.StructuralStress"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerException"));
        private static readonly uint _listenerContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Listeners"));

        // COLD ALLOC: RegistryBucket<IProceduralAudioEventListener>[8] - deferred procedural-audio listeners - owner: ProceduralAudioEvents
        private static readonly RegistryBucket<IProceduralAudioEventListener> _listeners = new RegistryBucket<IProceduralAudioEventListener>(ListenerCapacity);
        // COLD ALLOC: IProceduralAudioEventListener[8] - listener additions deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly IProceduralAudioEventListener[] _deferredRegisterListeners = new IProceduralAudioEventListener[ListenerCapacity];
        // COLD ALLOC: IProceduralAudioEventListener[8] - listener removals deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly IProceduralAudioEventListener[] _deferredUnregisterListeners = new IProceduralAudioEventListener[ListenerCapacity];
        private static NativeQueue<AudioEvent> _pendingAudioEvents;
        private static NativeQueue<AudioEvent> _nextFrameAudioEvents;
        private static int _pendingAudioEventCount;
        private static int _nextFrameAudioEventCount;
        private static int _pendingAudioPingCount;
        private static int _nextFrameAudioPingCount;
        private static int _pendingStructuralStressCount;
        private static int _nextFrameStructuralStressCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedAudioPingCount;
        private static int _droppedStructuralStressCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;
        private static bool _typedSignalLaneConfigured;

        /// <summary>
        /// Number of procedural audio events waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount
        {
            get
            {
                return _pendingAudioEventCount + _nextFrameAudioEventCount;
            }
        }

        internal static int DroppedAudioPingCount => _droppedAudioPingCount;

        internal static int DroppedStructuralStressCount => _droppedStructuralStressCount;
        internal static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        internal static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingAudioEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_pendingAudioEvents));
                _pendingAudioEvents.Dispose();
                _pendingAudioEvents = default;
            }

            if (_nextFrameAudioEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ProceduralAudioEvents), nameof(_nextFrameAudioEvents));
                _nextFrameAudioEvents.Dispose();
                _nextFrameAudioEvents = default;
            }

            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingAudioEventCount = 0;
            _nextFrameAudioEventCount = 0;
            _pendingAudioPingCount = 0;
            _nextFrameAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
            _nextFrameStructuralStressCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedAudioPingCount = 0;
            _droppedStructuralStressCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _typedSignalLaneConfigured = false;
            _listeners.Clear();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void ResetForSmokeTest()
        {
            ResetStaticState();
        }
#endif

        /// <summary>
        /// Registers one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to register.</param>
        public static void Register(IProceduralAudioEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        /// <summary>
        /// Unregisters one procedural audio listener.
        /// </summary>
        /// <param name="listener">Listener to unregister.</param>
        public static void Unregister(IProceduralAudioEventListener listener)
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
                DropQueuedEvents();
        }

        /// <summary>
        /// Flushes queued procedural audio notifications on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
                {
                    DropQueuedEvents();
                    completed = true;
                }
                else
                {
                    completed = FlushAudioEvents();
                }
            }
            finally
            {
                _isDispatching = false;
                ApplyDeferredListenerMutations();
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        /// <summary>
        /// Queues the sample-accurate sonar-ping notification on the main thread.
        /// </summary>
        /// <param name="startSampleFrame">Exact output-sample frame where the ping starts.</param>
        /// <param name="sampleRate">Audio output sample rate used to resolve the frame timestamp.</param>
        /// <param name="intensity">Normalized ping intensity in the 0..1 range.</param>
        /// <param name="chirpDurationSeconds">Primary chirp duration in seconds.</param>
        public static void RaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            AudioPingTriggerInfo info = new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds);
            AudioEvent audioEvent = AudioEvent.FromAudioPing(in info);
            PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return;
            }

            EnqueueAudioPing(in info);
        }

        public static void RaiseAudioPingTriggered(
            Vector3 worldPosition,
            float intensity,
            float chirpDurationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            ProceduralAudioPingKind kind)
        {
            AudioPingTriggerInfo info = new AudioPingTriggerInfo(
                worldPosition,
                intensity,
                chirpDurationSeconds,
                acousticTransmission01,
                lowPassCutoffHz,
                kind);
            AudioEvent audioEvent = AudioEvent.FromAudioPing(in info);
            PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return;
            }

            EnqueueAudioPing(in info);
        }

        private static void EnqueueAudioPing(in AudioPingTriggerInfo info)
        {
            if (_isDispatching)
            {
                _nextFrameAudioEvents.Enqueue(AudioEvent.FromAudioPing(in info));
                _nextFrameAudioEventCount++;
                _nextFrameAudioPingCount++;
            }
            else
            {
                _pendingAudioEvents.Enqueue(AudioEvent.FromAudioPing(in info));
                _pendingAudioEventCount++;
                _pendingAudioPingCount++;
            }
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        public static void RaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            StructuralStressAudioInfo info = new StructuralStressAudioInfo(worldPosition, stress01, pitchScale);
            RaiseStructuralStressTriggered(in info);
        }

        /// <summary>
        /// Queues a pressure-derived hull stress signal on the main thread.
        /// </summary>
        public static void RaiseHullStressSignal(in HullStressSignal signal)
        {
            StructuralStressAudioInfo info = new StructuralStressAudioInfo(in signal);
            RaiseStructuralStressTriggered(in info);
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        public static void RaiseStructuralStressTriggered(in StructuralStressAudioInfo info)
        {
            AudioEvent audioEvent = AudioEvent.FromStructuralStress(in info);
            PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingStructuralStressCount + _nextFrameStructuralStressCount >= PendingStructuralStressCapacity)
            {
                ReportStructuralStressOverflow();
                return;
            }

            EnqueueStructuralStress(in info);
        }

        private static void EnqueueStructuralStress(in StructuralStressAudioInfo info)
        {
            if (_isDispatching)
            {
                _nextFrameAudioEvents.Enqueue(AudioEvent.FromStructuralStress(in info));
                _nextFrameAudioEventCount++;
                _nextFrameStructuralStressCount++;
            }
            else
            {
                _pendingAudioEvents.Enqueue(AudioEvent.FromStructuralStress(in info));
                _pendingAudioEventCount++;
                _pendingStructuralStressCount++;
            }
        }

        private static void PublishTypedAudioEvent(in AudioEvent audioEvent)
        {
            EnsureTypedSignalLaneConfigured();
            SignalBus<AudioEvent>.Push(in audioEvent);
        }

        private static void EnsureTypedSignalLaneConfigured()
        {
            if (_typedSignalLaneConfigured)
                return;

            GlobalSignals.InitializeAllQueues();
            SignalBus<AudioEvent>.EnsureInitialized();
            _typedSignalLaneConfigured = true;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingAudioEvents.IsCreated)
            {
                _pendingAudioEvents = new NativeQueue<AudioEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioEvent>[16] - deferred procedural audio event lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingAudioEvents,
                    PendingAudioEventCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_pendingAudioEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingAudioEvents, PendingAudioEventCapacity);
            }

            if (!_nextFrameAudioEvents.IsCreated)
            {
                _nextFrameAudioEvents = new NativeQueue<AudioEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioEvent>[16] - next-frame procedural audio event lane - owner: ProceduralAudioEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameAudioEvents,
                    PendingAudioEventCapacity,
                    nameof(ProceduralAudioEvents),
                    nameof(_nextFrameAudioEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameAudioEvents, PendingAudioEventCapacity);
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

        private static bool FlushAudioEvents()
        {
            if (!_pendingAudioEvents.IsCreated)
                return true;

            int scanBudget = _pendingAudioEventCount > 0 ? _pendingAudioEventCount : PendingAudioEventCapacity;
            while (scanBudget > 0 && !_pendingAudioEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingAudioEvents.TryDequeue(out AudioEvent audioEvent))
                    return true;

                DecrementFrontEventCount(audioEvent.Kind);
                scanBudget--;
                IProceduralAudioEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = rawArray[i];
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchAudioEventToListener(listener, in audioEvent);
                }
            }

            if (_pendingAudioEvents.IsEmpty())
            {
                _pendingAudioEventCount = 0;
                _pendingAudioPingCount = 0;
                _pendingStructuralStressCount = 0;
            }

            return true;
        }

        private static void DecrementFrontEventCount(AudioEventKind kind)
        {
            if (_pendingAudioEventCount > 0)
                _pendingAudioEventCount--;
            switch (kind)
            {
                case AudioEventKind.AudioPing:
                    if (_pendingAudioPingCount > 0)
                        _pendingAudioPingCount--;
                    break;
                case AudioEventKind.StructuralStress:
                    if (_pendingStructuralStressCount > 0)
                        _pendingStructuralStressCount--;
                    break;
            }
        }

        private static void DropQueuedEvents()
        {
            if (_pendingAudioEvents.IsCreated)
            {
                while (_pendingAudioEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameAudioEvents.IsCreated)
            {
                while (_nextFrameAudioEvents.TryDequeue(out _))
                {
                }
            }

            _pendingAudioEventCount = 0;
            _nextFrameAudioEventCount = 0;
            _pendingAudioPingCount = 0;
            _nextFrameAudioPingCount = 0;
            _pendingStructuralStressCount = 0;
            _nextFrameStructuralStressCount = 0;
        }

        private static void DispatchAudioEventToListener(IProceduralAudioEventListener listener, in AudioEvent audioEvent)
        {
            switch (audioEvent.Kind)
            {
                case AudioEventKind.AudioPing:
                    DispatchAudioPingToListener(listener, in audioEvent.AudioPing);
                    break;
                case AudioEventKind.StructuralStress:
                    DispatchStructuralStressToListener(listener, in audioEvent.StructuralStress);
                    break;
            }
        }

        private static void DispatchAudioPingToListener(IProceduralAudioEventListener listener, in AudioPingTriggerInfo info)
        {
            try
            {
                listener.OnAudioPingTriggered(in info);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        private static void DispatchStructuralStressToListener(IProceduralAudioEventListener listener, in StructuralStressAudioInfo info)
        {
            try
            {
                listener.OnStructuralStressTriggered(in info);
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
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IProceduralAudioEventListener listener)
        {
            if (_listeners.Contains(listener))
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

            _deferredRegisterListeners[_deferredRegisterCount++] = listener;
        }

        private static void QueueDeferredUnregister(IProceduralAudioEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++] = listener;
        }

        private static bool CancelDeferredRegister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i], listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = null;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = null;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i], listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IProceduralAudioEventListener listener = _deferredUnregisterListeners[i];
                _deferredUnregisterListeners[i] = null;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IProceduralAudioEventListener listener = _deferredRegisterListeners[i];
                _deferredRegisterListeners[i] = null;
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IProceduralAudioEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Time.frameCount;
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
            int frame = Time.frameCount;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _listenerExceptionWarningHash,
                _listenerContextHash,
                Mathf.Max(1, _listenerExceptionCount));
        }

        private static bool HasPendingFrontEvents()
        {
            return _pendingAudioEvents.IsCreated && !_pendingAudioEvents.IsEmpty();
        }

        private static void ReportAudioPingOverflow()
        {
            _droppedAudioPingCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _audioPingQueueHash, PendingAudioPingCapacity);
        }

        private static void ReportStructuralStressOverflow()
        {
            _droppedStructuralStressCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _structuralStressQueueHash, PendingStructuralStressCapacity);
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameAudioEvents.IsCreated)
                return;

            while (_nextFrameAudioEventCount > 0 && _nextFrameAudioEvents.TryDequeue(out AudioEvent audioEvent))
            {
                _nextFrameAudioEventCount--;
                _pendingAudioEvents.Enqueue(audioEvent);
                _pendingAudioEventCount++;
                switch (audioEvent.Kind)
                {
                    case AudioEventKind.AudioPing:
                        if (_nextFrameAudioPingCount > 0)
                            _nextFrameAudioPingCount--;
                        _pendingAudioPingCount++;
                        break;
                    case AudioEventKind.StructuralStress:
                        if (_nextFrameStructuralStressCount > 0)
                            _nextFrameStructuralStressCount--;
                        _pendingStructuralStressCount++;
                        break;
                }
            }
        }
    }
}
