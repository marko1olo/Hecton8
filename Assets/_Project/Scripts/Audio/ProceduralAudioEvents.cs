using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
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

        internal AudioPingTriggerInfo(
            long startSampleFrame,
            int sampleRate,
            float intensity,
            float chirpDurationSeconds,
            Vector3 worldPosition,
            float acousticTransmission01,
            float lowPassCutoffHz,
            byte kind)
        {
            StartSampleFrame = startSampleFrame;
            SampleRate = sampleRate > 0 ? sampleRate : 1;
            Intensity = Mathf.Clamp01(intensity);
            ChirpDurationSeconds = Mathf.Max(0f, chirpDurationSeconds);
            WorldPosition = worldPosition;
            AcousticTransmission01 = Mathf.Clamp01(acousticTransmission01);
            LowPassCutoffHz = Mathf.Clamp(lowPassCutoffHz, 80f, 22000f);
            Kind = (ProceduralAudioPingKind)kind;
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
            SourceAup = ResolveSourceAup(safeWorldPosition);
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

        internal static AbsoluteUniversePosition ResolveSourceAup(Vector3 safeWorldPosition)
        {
            if (TryResolveAupFromRuntimeOrigin(safeWorldPosition, out AbsoluteUniversePosition sourceAup))
                return sourceAup;

            AbsoluteUniversePosition fallbackAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            return AbsoluteUniversePosition.IsFinite(in fallbackAup) ? fallbackAup : default;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition sourceAup)
        {
            sourceAup = default;
            if (!math.isfinite(runtimePosition.x) || !math.isfinite(runtimePosition.y) || !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            sourceAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in sourceAup);
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
            SourceAup = HullStressSignal.ResolveSourceAup(safeWorldPosition);
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

        internal StructuralStressAudioInfo(
            in AbsoluteUniversePosition sourceAup,
            Vector3 worldPosition,
            float stress01,
            float pitchScale,
            float pressureDelta,
            float depthMeters,
            float acousticTransmission01,
            float lowPassCutoffHz,
            float acousticDelaySeconds)
        {
            WorldPosition = HullStressSignal.SanitizeWorldPosition(worldPosition);
            SourceAup = sourceAup;
            Stress01 = Mathf.Clamp01(HullStressSignal.SanitizeFiniteValue(stress01));
            PitchScale = Mathf.Max(0.1f, HullStressSignal.SanitizeFiniteValue(pitchScale));
            PressureDelta = HullStressSignal.SanitizeFiniteValue(pressureDelta);
            DepthMeters = Mathf.Max(0f, HullStressSignal.SanitizeFiniteValue(depthMeters));
            AcousticTransmission01 = Mathf.Clamp01(HullStressSignal.SanitizeFiniteValue(acousticTransmission01));
            LowPassCutoffHz = Mathf.Clamp(HullStressSignal.SanitizeFiniteOrDefault(lowPassCutoffHz, 22000f), 80f, 22000f);
            AcousticDelaySeconds = Mathf.Max(0f, HullStressSignal.SanitizeFiniteValue(acousticDelaySeconds));
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
        private static int s_x001DirectSignalPushDropCount_ProceduralAudioEvents;

        private const int ListenerCapacity = 8;
        private const int PendingAudioPingCapacity = 8;
        private const int PendingStructuralStressCapacity = 8;
        private const int PendingAudioEventCapacity = PendingAudioPingCapacity + PendingStructuralStressCapacity;
        private const BufferID PendingAudioEventsBufferId = (BufferID)70885;
        private const BufferID NextFrameAudioEventsBufferId = (BufferID)70886;
        private const SystemID VaultOwner = SystemID.Audio;
        private static readonly uint _overflowWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Overflow"));
        private static readonly uint _audioPingQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.AudioPing"));
        private static readonly uint _structuralStressQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.StructuralStress"));
        private static readonly uint _listenerRejectedWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerRejected"));
        private static readonly uint _listenerExceptionWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.ListenerException"));
        private static readonly uint _listenerContextHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralAudioEvents.Listeners"));

        // COLD ALLOC: ProceduralAudioListenerRegistry[8] - managed legacy listener bridge; hot audio truth uses SignalBus<AudioEvent> - owner: ProceduralAudioEvents
        private static readonly ProceduralAudioListenerRegistry _listeners = new ProceduralAudioListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[8] - listener additions deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener removals deferred while dispatching procedural audio events - owner: ProceduralAudioEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        // VAULT ALIAS: NativeArray<AudioEvent>[16] - deferred procedural audio event ring - vault owner: SystemID.Audio
        private static NativeArray<AudioEvent> _pendingAudioEvents;
        // VAULT ALIAS: NativeArray<AudioEvent>[16] - next-frame procedural audio event ring - vault owner: SystemID.Audio
        private static NativeArray<AudioEvent> _nextFrameAudioEvents;
        private static VaultGenerationHandle<AudioEvent> _pendingAudioEventsHandle;
        private static VaultGenerationHandle<AudioEvent> _nextFrameAudioEventsHandle;
        private static IDataVault _dataVault;
        private static int _pendingAudioEventReadIndex;
        private static int _pendingAudioEventWriteIndex;
        private static int _nextFrameAudioEventReadIndex;
        private static int _nextFrameAudioEventWriteIndex;
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

        private struct ListenerSlot
        {
            public IProceduralAudioEventListener Listener;
        }

        private sealed class ProceduralAudioListenerRegistry
        {
            private readonly ListenerSlot[] _items;
            private readonly int _capacity;
            private int _count;

            public ProceduralAudioListenerRegistry(int capacity)
            {
                _capacity = Math.Max(1, capacity);
                _items = new ListenerSlot[_capacity]; // COLD ALLOC: ListenerSlot[capacity] - managed procedural-audio legacy listener bridge - owner: ProceduralAudioEvents
            }

            public int Count => _count;

            public IProceduralAudioEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _items[index].Listener : null;
            }

            public bool TryRegister(IProceduralAudioEventListener listener)
            {
                if (listener == null || _count >= _capacity || Contains(listener))
                    return false;

                _items[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IProceduralAudioEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_items[i].Listener, listener))
                        continue;

                    _count--;
                    _items[i] = _items[_count];
                    _items[_count] = default;
                    return true;
                }

                return false;
            }

            public bool Contains(IProceduralAudioEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_items[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public void Clear()
            {
                Array.Clear(_items, 0, _count);
                _count = 0;
            }
        }

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
            ReleaseAudioEventBuffers();

            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingAudioEventReadIndex = 0;
            _pendingAudioEventWriteIndex = 0;
            _nextFrameAudioEventReadIndex = 0;
            _nextFrameAudioEventWriteIndex = 0;
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

            EnsureInitialized(allowAllocate: true);
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
        [Obsolete("Use TryRaiseAudioPingTriggered(long,int,float,float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            TryRaiseAudioPingTriggered(startSampleFrame, sampleRate, intensity, chirpDurationSeconds);
        }

        public static bool TryRaiseAudioPingTriggered(long startSampleFrame, int sampleRate, float intensity, float chirpDurationSeconds)
        {
            AudioPingTriggerInfo info = new AudioPingTriggerInfo(startSampleFrame, sampleRate, intensity, chirpDurationSeconds);
            AudioEvent audioEvent = CreateAudioPingEvent(in info);
            bool typedQueued = PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return typedQueued;

            EnsureInitialized(allowAllocate: false);
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return false;
            }

            return typedQueued && EnqueueAudioPing(in info);
        }

        [Obsolete("Use TryRaiseAudioPingTriggered(Vector3,float,float,float,float,ProceduralAudioPingKind) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseAudioPingTriggered(
            Vector3 worldPosition,
            float intensity,
            float chirpDurationSeconds,
            float acousticTransmission01,
            float lowPassCutoffHz,
            ProceduralAudioPingKind kind)
        {
            TryRaiseAudioPingTriggered(worldPosition, intensity, chirpDurationSeconds, acousticTransmission01, lowPassCutoffHz, kind);
        }

        public static bool TryRaiseAudioPingTriggered(
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
            AudioEvent audioEvent = CreateAudioPingEvent(in info);
            bool typedQueued = PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return typedQueued;

            EnsureInitialized(allowAllocate: false);
            if (_pendingAudioPingCount + _nextFrameAudioPingCount >= PendingAudioPingCapacity)
            {
                ReportAudioPingOverflow();
                return false;
            }

            return typedQueued && EnqueueAudioPing(in info);
        }

        private static bool EnqueueAudioPing(in AudioPingTriggerInfo info)
        {
            if (!EnsureInitialized(allowAllocate: false))
            {
                ReportAudioPingOverflow();
                return false;
            }

            AudioEvent audioEvent = CreateAudioPingEvent(in info);
            if (_isDispatching)
            {
                if (!TryWriteAudioEvent(_nextFrameAudioEvents, ref _nextFrameAudioEventWriteIndex, _nextFrameAudioEventCount, in audioEvent))
                {
                    ReportAudioPingOverflow();
                    return false;
                }

                _nextFrameAudioEventCount++;
                _nextFrameAudioPingCount++;
                return true;
            }
            else
            {
                if (!TryWriteAudioEvent(_pendingAudioEvents, ref _pendingAudioEventWriteIndex, _pendingAudioEventCount, in audioEvent))
                {
                    ReportAudioPingOverflow();
                    return false;
                }

                _pendingAudioEventCount++;
                _pendingAudioPingCount++;
                return true;
            }
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        [Obsolete("Use TryRaiseStructuralStressTriggered(Vector3,float,float) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            TryRaiseStructuralStressTriggered(worldPosition, stress01, pitchScale);
        }

        public static bool TryRaiseStructuralStressTriggered(Vector3 worldPosition, float stress01, float pitchScale)
        {
            StructuralStressAudioInfo info = new StructuralStressAudioInfo(worldPosition, stress01, pitchScale);
            return TryRaiseStructuralStressTriggered(in info);
        }

        /// <summary>
        /// Queues a pressure-derived hull stress signal on the main thread.
        /// </summary>
        [Obsolete("Use TryRaiseHullStressSignal(in HullStressSignal) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseHullStressSignal(in HullStressSignal signal)
        {
            TryRaiseHullStressSignal(in signal);
        }

        public static bool TryRaiseHullStressSignal(in HullStressSignal signal)
        {
            StructuralStressAudioInfo info = new StructuralStressAudioInfo(in signal);
            return TryRaiseStructuralStressTriggered(in info);
        }

        /// <summary>
        /// Queues a habitat structural stress groan notification on the main thread.
        /// </summary>
        [Obsolete("Use TryRaiseStructuralStressTriggered(in StructuralStressAudioInfo) so overflow/drop semantics stay visible at the producer.", true)]
        public static void RaiseStructuralStressTriggered(in StructuralStressAudioInfo info)
        {
            TryRaiseStructuralStressTriggered(in info);
        }

        public static bool TryRaiseStructuralStressTriggered(in StructuralStressAudioInfo info)
        {
            AudioEvent audioEvent = CreateStructuralStressEvent(in info);
            bool typedQueued = PublishTypedAudioEvent(in audioEvent);

            if (_listeners.Count <= 0)
                return typedQueued;

            EnsureInitialized(allowAllocate: false);
            if (_pendingStructuralStressCount + _nextFrameStructuralStressCount >= PendingStructuralStressCapacity)
            {
                ReportStructuralStressOverflow();
                return false;
            }

            return typedQueued && EnqueueStructuralStress(in info);
        }

        private static bool EnqueueStructuralStress(in StructuralStressAudioInfo info)
        {
            if (!EnsureInitialized(allowAllocate: false))
            {
                ReportStructuralStressOverflow();
                return false;
            }

            AudioEvent audioEvent = CreateStructuralStressEvent(in info);
            if (_isDispatching)
            {
                if (!TryWriteAudioEvent(_nextFrameAudioEvents, ref _nextFrameAudioEventWriteIndex, _nextFrameAudioEventCount, in audioEvent))
                {
                    ReportStructuralStressOverflow();
                    return false;
                }

                _nextFrameAudioEventCount++;
                _nextFrameStructuralStressCount++;
                return true;
            }
            else
            {
                if (!TryWriteAudioEvent(_pendingAudioEvents, ref _pendingAudioEventWriteIndex, _pendingAudioEventCount, in audioEvent))
                {
                    ReportStructuralStressOverflow();
                    return false;
                }

                _pendingAudioEventCount++;
                _pendingStructuralStressCount++;
                return true;
            }
        }

        private static AudioEvent CreateAudioPingEvent(in AudioPingTriggerInfo info)
        {
            AudioPingTriggerPayload payload = new AudioPingTriggerPayload(
                info.StartSampleFrame,
                info.SampleRate,
                info.Intensity,
                info.ChirpDurationSeconds,
                info.WorldPosition,
                info.AcousticTransmission01,
                info.LowPassCutoffHz,
                (byte)info.Kind);
            return AudioEvent.FromAudioPing(in payload);
        }

        private static AudioEvent CreateStructuralStressEvent(in StructuralStressAudioInfo info)
        {
            StructuralStressAudioPayload payload = new StructuralStressAudioPayload(
                ToAcousticAup(in info.SourceAup),
                info.WorldPosition,
                info.Stress01,
                info.PitchScale,
                info.PressureDelta,
                info.DepthMeters,
                info.AcousticTransmission01,
                info.LowPassCutoffHz,
                info.AcousticDelaySeconds,
                StructuralStressAudioPayload.FlagHasSourceAup);
            return AudioEvent.FromStructuralStress(in payload);
        }

        private static bool PublishTypedAudioEvent(in AudioEvent audioEvent)
        {
            EnsureTypedSignalLaneConfigured();
            return SignalBus<AudioEvent>.TryPushTracked(in audioEvent, ref s_x001DirectSignalPushDropCount_ProceduralAudioEvents);
        }

        private static void EnsureTypedSignalLaneConfigured()
        {
            if (_typedSignalLaneConfigured)
                return;

            SignalCorridorRuntime.EnsureInitialized();
            SignalBus<AudioEvent>.EnsureInitialized();
            _typedSignalLaneConfigured = true;
        }

        private static bool EnsureInitialized(bool allowAllocate)
        {
            if (_pendingAudioEvents.IsCreated && _nextFrameAudioEvents.IsCreated)
                return true;

            IDataVault vault = _dataVault;
            if (vault == null && allowAllocate)
            {
                vault = GlobalRegistry.DataVault;
                _dataVault = vault;
            }

            if (vault == null)
                return false;

            if (!IsVaultHandleCreated(in _pendingAudioEventsHandle))
            {
                if (!allowAllocate || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<AudioEvent>(
                            PendingAudioEventsBufferId,
                            out _pendingAudioEventsHandle))
                    {
                        return false;
                    }
                }
                else
                {
                    _pendingAudioEventsHandle = vault.EnsureGenerationHandle<AudioEvent>(
                        PendingAudioEventsBufferId,
                        PendingAudioEventCapacity,
                        VaultOwner,
                        NativeArrayOptions.ClearMemory);
                }
            }

            if (!IsVaultHandleCreated(in _nextFrameAudioEventsHandle))
            {
                if (!allowAllocate || vault.IsAllocationLocked)
                {
                    if (!vault.TryGetGenerationHandle<AudioEvent>(
                            NextFrameAudioEventsBufferId,
                            out _nextFrameAudioEventsHandle))
                    {
                        return false;
                    }
                }
                else
                {
                    _nextFrameAudioEventsHandle = vault.EnsureGenerationHandle<AudioEvent>(
                        NextFrameAudioEventsBufferId,
                        PendingAudioEventCapacity,
                        VaultOwner,
                        NativeArrayOptions.ClearMemory);
                }
            }

            bool resolved =
                vault.TryResolveHandle(in _pendingAudioEventsHandle, out _pendingAudioEvents) &&
                vault.TryResolveHandle(in _nextFrameAudioEventsHandle, out _nextFrameAudioEvents) &&
                _pendingAudioEvents.IsCreated &&
                _nextFrameAudioEvents.IsCreated &&
                _pendingAudioEvents.Length >= PendingAudioEventCapacity &&
                _nextFrameAudioEvents.Length >= PendingAudioEventCapacity;

            if (!resolved)
            {
                _pendingAudioEvents = default;
                _nextFrameAudioEvents = default;
                return false;
            }

            if (allowAllocate)
            {
                ClearAudioEventRing(_pendingAudioEvents);
                ClearAudioEventRing(_nextFrameAudioEvents);
                _pendingAudioEventReadIndex = 0;
                _pendingAudioEventWriteIndex = 0;
                _nextFrameAudioEventReadIndex = 0;
                _nextFrameAudioEventWriteIndex = 0;
            }
            return true;
        }

        private static bool IsVaultHandleCreated<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u;
        }

        private static void ReleaseAudioEventBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                if (IsVaultHandleCreated(in _pendingAudioEventsHandle))
                    vault.ReleaseBuffer(in _pendingAudioEventsHandle);
                if (IsVaultHandleCreated(in _nextFrameAudioEventsHandle))
                    vault.ReleaseBuffer(in _nextFrameAudioEventsHandle);
            }

            _pendingAudioEvents = default;
            _nextFrameAudioEvents = default;
            _pendingAudioEventsHandle = default;
            _nextFrameAudioEventsHandle = default;
            _dataVault = null;
        }

        private static void ClearAudioEventRing(NativeArray<AudioEvent> buffer)
        {
            if (!buffer.IsCreated)
                return;

            int capacity = buffer.Length < PendingAudioEventCapacity ? buffer.Length : PendingAudioEventCapacity;
            for (int i = 0; i < capacity; i++)
                buffer[i] = default;
        }

        private static bool TryWriteAudioEvent(NativeArray<AudioEvent> buffer, ref int writeIndex, int count, in AudioEvent audioEvent)
        {
            if (!buffer.IsCreated || count >= PendingAudioEventCapacity)
                return false;

            int safeIndex = writeIndex;
            if ((uint)safeIndex >= (uint)PendingAudioEventCapacity)
                safeIndex = 0;

            buffer[safeIndex] = audioEvent;
            writeIndex = safeIndex + 1;
            if (writeIndex >= PendingAudioEventCapacity)
                writeIndex = 0;
            return true;
        }

        private static bool TryReadAudioEvent(NativeArray<AudioEvent> buffer, ref int readIndex, int count, out AudioEvent audioEvent)
        {
            if (!buffer.IsCreated || count <= 0)
            {
                audioEvent = default;
                return false;
            }

            int safeIndex = readIndex;
            if ((uint)safeIndex >= (uint)PendingAudioEventCapacity)
                safeIndex = 0;

            audioEvent = buffer[safeIndex];
            buffer[safeIndex] = default;
            readIndex = safeIndex + 1;
            if (readIndex >= PendingAudioEventCapacity)
                readIndex = 0;
            return true;
        }

        private static bool FlushAudioEvents()
        {
            if (!_pendingAudioEvents.IsCreated)
                return true;

            int scanBudget = _pendingAudioEventCount > 0 ? _pendingAudioEventCount : PendingAudioEventCapacity;
            while (scanBudget > 0 && _pendingAudioEventCount > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!TryReadAudioEvent(_pendingAudioEvents, ref _pendingAudioEventReadIndex, _pendingAudioEventCount, out AudioEvent audioEvent))
                    return true;

                DecrementFrontEventCount(audioEvent.Kind);
                scanBudget--;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IProceduralAudioEventListener listener = _listeners.GetAt(i);
                    if (listener == null || IsDeferredUnregisterPending(listener))
                        continue;

                    DispatchAudioEventToListener(listener, in audioEvent);
                }
            }

            if (_pendingAudioEventCount <= 0)
            {
                _pendingAudioEventCount = 0;
                _pendingAudioPingCount = 0;
                _pendingStructuralStressCount = 0;
                _pendingAudioEventReadIndex = 0;
                _pendingAudioEventWriteIndex = 0;
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
                ClearAudioEventRing(_pendingAudioEvents);

            if (_nextFrameAudioEvents.IsCreated)
                ClearAudioEventRing(_nextFrameAudioEvents);

            _pendingAudioEventReadIndex = 0;
            _pendingAudioEventWriteIndex = 0;
            _nextFrameAudioEventReadIndex = 0;
            _nextFrameAudioEventWriteIndex = 0;
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
                {
                    AudioPingTriggerInfo info = ToAudioInfo(in audioEvent.AudioPing);
                    DispatchAudioPingToListener(listener, in info);
                    break;
                }
                case AudioEventKind.StructuralStress:
                {
                    StructuralStressAudioInfo info = ToAudioInfo(in audioEvent.StructuralStress);
                    DispatchStructuralStressToListener(listener, in info);
                    break;
                }
            }
        }

        private static AudioPingTriggerInfo ToAudioInfo(in AudioPingTriggerPayload payload)
        {
            return new AudioPingTriggerInfo(
                payload.StartSampleFrame,
                payload.SampleRate,
                payload.Intensity,
                payload.ChirpDurationSeconds,
                payload.WorldPosition,
                payload.AcousticTransmission01,
                payload.LowPassCutoffHz,
                payload.Kind);
        }

        private static StructuralStressAudioInfo ToAudioInfo(in StructuralStressAudioPayload payload)
        {
            AbsoluteUniversePosition sourceAup = (payload.Flags & StructuralStressAudioPayload.FlagHasSourceAup) != 0
                ? ToAbsoluteUniversePosition(in payload.SourceAup)
                : HullStressSignal.ResolveSourceAup(payload.WorldPosition);

            return new StructuralStressAudioInfo(
                in sourceAup,
                payload.WorldPosition,
                payload.Stress01,
                payload.PitchScale,
                payload.PressureDelta,
                payload.DepthMeters,
                payload.AcousticTransmission01,
                payload.LowPassCutoffHz,
                payload.AcousticDelaySeconds);
        }

        private static AcousticAup ToAcousticAup(in AbsoluteUniversePosition aup)
        {
            return new AcousticAup(
                aup.GridX,
                aup.GridY,
                aup.GridZ,
                new Unity.Mathematics.float3(aup.LocalX, aup.LocalY, aup.LocalZ));
        }

        private static AbsoluteUniversePosition ToAbsoluteUniversePosition(in AcousticAup aup)
        {
            return new AbsoluteUniversePosition
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.Local.x,
                LocalY = aup.Local.y,
                LocalZ = aup.Local.z
            };
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

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
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

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount] = default;
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount] = default;
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IProceduralAudioEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IProceduralAudioEventListener listener)
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
                IProceduralAudioEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i] = default;
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IProceduralAudioEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i] = default;
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

        private static bool HasPendingFrontEvents()
        {
            return _pendingAudioEventCount > 0;
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

            while (_nextFrameAudioEventCount > 0 &&
                   TryReadAudioEvent(_nextFrameAudioEvents, ref _nextFrameAudioEventReadIndex, _nextFrameAudioEventCount, out AudioEvent audioEvent))
            {
                _nextFrameAudioEventCount--;
                if (!TryWriteAudioEvent(_pendingAudioEvents, ref _pendingAudioEventWriteIndex, _pendingAudioEventCount, in audioEvent))
                    break;

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

            if (_nextFrameAudioEventCount <= 0)
            {
                _nextFrameAudioEventCount = 0;
                _nextFrameAudioPingCount = 0;
                _nextFrameStructuralStressCount = 0;
                _nextFrameAudioEventReadIndex = 0;
                _nextFrameAudioEventWriteIndex = 0;
            }
        }
    }
}
