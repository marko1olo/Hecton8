// ============================================================================
// HECTON-8 — SpectrumSystem.cs
// Sistema rezhimov vizora Hecton-OS: SPECTRUM vkladka.
//
// LOR (lor2 Razdel 9):
//   SPECTRUM: Upravlenie vizorom
//   • Teplovizor — teplovye signatury suschestv i oborudovaniya
//   • Sonar — dvizhenie v radiuse 100m (ne pokazyvaet chto — tolko chto est)
//   • Eholot — biomehanicheskie signatury (Atlas-6 drony)
//
// ARHITEKTURA:
//   • Singleton. Pereklyuchaet rezhimy cherez Shader.SetGlobalInt.
//   • Integriruetsya s VisorHUDController cherez GlitchPulse pri smene.
//   • Publikuet sobytiya dlya HUD i post-protsessinga.
//   • ILateFrameTickable - updates sonar presentation pulses in VISUAL_SYNC.
//
// ZERO GC:
//   • Nikakih new/LINQ v Tick.
//   • Cached shader property IDs.
// ============================================================================

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.UI;
using Hecton8.World;
using Hecton.Localization;
using Unity.Collections;
using Unity.Mathematics;
using NASAPunk.Visor;
using UnityEngine;

namespace Hecton8.Visor
{
    public enum SpectrumMode
    {
        Normal      = 0,   // Obychnyy rezhim
        Thermal     = 1,   // Teplovizor
        Sonar       = 2,   // Sonar (dvizhenie)
        Echolocation = 3   // Eholot (biomehanicheskie signatury)
    }

    /// <summary>
    /// Resource-authored active-sonar echo payload forwarded into the procedural audio pipeline.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public readonly struct AcousticEchoEvent
    {
        /// <summary>Build a new active-sonar return payload.</summary>
        public AcousticEchoEvent(Vector3 worldPosition, float distanceMeters, float returnStrength, float resonance)
            : this(worldPosition, distanceMeters, returnStrength, resonance, 0)
        {
        }

        /// <summary>Build a new active-sonar return payload with an authored audio material.</summary>
        public AcousticEchoEvent(Vector3 worldPosition, float distanceMeters, float returnStrength, float resonance, byte audioMaterialId)
        {
            bool hasAup = SpectrumAupProof.TryResolveFromRuntime(worldPosition, out AbsoluteUniversePosition worldAup);
            WorldAup = worldAup;
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            ReturnStrength = returnStrength;
            Resonance = resonance;
            AudioMaterialId = audioMaterialId;
            _hasWorldAup = hasAup ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0u;
        }

        /// <summary>Build a new active-sonar return payload with a pre-resolved AUP origin.</summary>
        public AcousticEchoEvent(Vector3 worldPosition, in AbsoluteUniversePosition worldAup, float distanceMeters, float returnStrength, float resonance)
            : this(worldPosition, worldAup, true, distanceMeters, returnStrength, resonance, 0)
        {
        }

        /// <summary>Build a new active-sonar return payload with a pre-resolved AUP origin and audio material.</summary>
        public AcousticEchoEvent(Vector3 worldPosition, in AbsoluteUniversePosition worldAup, float distanceMeters, float returnStrength, float resonance, byte audioMaterialId)
            : this(worldPosition, worldAup, true, distanceMeters, returnStrength, resonance, audioMaterialId)
        {
        }

        private AcousticEchoEvent(
            Vector3 worldPosition,
            AbsoluteUniversePosition worldAup,
            bool hasWorldAup,
            float distanceMeters,
            float returnStrength,
            float resonance,
            byte audioMaterialId)
        {
            WorldAup = worldAup;
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            ReturnStrength = returnStrength;
            Resonance = resonance;
            AudioMaterialId = audioMaterialId;
            _hasWorldAup = hasWorldAup ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0u;
        }

        /// <summary>Absolute origin of the reflected return, stable across floating-origin shifts.</summary>
        [FieldOffset(0)] public readonly AbsoluteUniversePosition WorldAup;
        /// <summary>World-space origin of the reflected return.</summary>
        [FieldOffset(48)] public readonly Vector3 WorldPosition;
        /// <summary>One-way listener-to-target distance in authored meters.</summary>
        [FieldOffset(60)] public readonly float DistanceMeters;
        /// <summary>Normalized return energy emitted by the struck resource node.</summary>
        [FieldOffset(64)] public readonly float ReturnStrength;
        /// <summary>Pitch scalar used by the echo renderer. 1 = neutral.</summary>
        [FieldOffset(68)] public readonly float Resonance;
        /// <summary>Material route for sonar echo pitch, decay, and low-pass coloration.</summary>
        [FieldOffset(72)] public readonly byte AudioMaterialId;

        [FieldOffset(73)] private readonly byte _hasWorldAup;
        [FieldOffset(74)] private readonly ushort _pad0;
        [FieldOffset(76)] private readonly uint _pad1;

        /// <summary>Returns the stable absolute echo origin, falling back only for legacy payloads.</summary>
        public AbsoluteUniversePosition ResolveWorldAup()
        {
            return _hasWorldAup != 0
                ? WorldAup
                : SpectrumAupProof.ResolveFromRuntimeOrDefault(WorldPosition);
        }
    }

    internal static class SpectrumAupProof
    {
        public static bool TryResolveFromRuntime(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 runtime = default;
            runtime.x = runtimePosition.x;
            runtime.y = runtimePosition.y;
            runtime.z = runtimePosition.z;
            if (!math.all(math.isfinite(runtime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            double3 offsetMeters = default;
            offsetMeters.x = runtimePosition.x;
            offsetMeters.y = runtimePosition.y;
            offsetMeters.z = runtimePosition.z;
            positionAup = AbsoluteUniversePosition.OffsetMeters(in originAup, offsetMeters);
            return AbsoluteUniversePosition.IsFinite(in positionAup);
        }

        public static AbsoluteUniversePosition ResolveFromRuntimeOrDefault(Vector3 runtimePosition)
        {
            return TryResolveFromRuntime(runtimePosition, out AbsoluteUniversePosition positionAup)
                ? positionAup
                : default;
        }
    }

    /// <summary>
    /// Visual-only active-sonar return emitted by the DSP echo path.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80)]
    public readonly struct PingReturnSignal
    {
        public PingReturnSignal(
            Vector3 worldPosition,
            float distanceMeters,
            float returnStrength,
            float echoDelaySeconds,
            byte audioMaterialId)
        {
            bool hasAup = SpectrumAupProof.TryResolveFromRuntime(worldPosition, out AbsoluteUniversePosition worldAup);
            WorldAup = worldAup;
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            ReturnStrength = returnStrength;
            EchoDelaySeconds = echoDelaySeconds;
            AudioMaterialId = audioMaterialId;
            _hasWorldAup = hasAup ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0u;
        }

        public PingReturnSignal(
            Vector3 worldPosition,
            in AbsoluteUniversePosition worldAup,
            float distanceMeters,
            float returnStrength,
            float echoDelaySeconds,
            byte audioMaterialId)
            : this(worldPosition, worldAup, true, distanceMeters, returnStrength, echoDelaySeconds, audioMaterialId)
        {
        }

        private PingReturnSignal(
            Vector3 worldPosition,
            AbsoluteUniversePosition worldAup,
            bool hasWorldAup,
            float distanceMeters,
            float returnStrength,
            float echoDelaySeconds,
            byte audioMaterialId)
        {
            WorldAup = worldAup;
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            ReturnStrength = returnStrength;
            EchoDelaySeconds = echoDelaySeconds;
            AudioMaterialId = audioMaterialId;
            _hasWorldAup = hasWorldAup ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0u;
        }

        [FieldOffset(0)] public readonly AbsoluteUniversePosition WorldAup;
        [FieldOffset(48)] public readonly Vector3 WorldPosition;
        [FieldOffset(60)] public readonly float DistanceMeters;
        [FieldOffset(64)] public readonly float ReturnStrength;
        [FieldOffset(68)] public readonly float EchoDelaySeconds;
        [FieldOffset(72)] public readonly byte AudioMaterialId;

        [FieldOffset(73)] private readonly byte _hasWorldAup;
        [FieldOffset(74)] private readonly ushort _pad0;
        [FieldOffset(76)] private readonly uint _pad1;

        public AbsoluteUniversePosition ResolveWorldAup()
        {
            return _hasWorldAup != 0
                ? WorldAup
                : SpectrumAupProof.ResolveFromRuntimeOrDefault(WorldPosition);
        }
    }

    /// <summary>
    /// Listener for deferred spectrum mode changes.
    /// </summary>
    public interface ISpectrumModeEventListener
    {
        /// <summary>Receives the new active spectrum mode.</summary>
        /// <param name="mode">New mode.</param>
        void OnSpectrumModeChanged(SpectrumMode mode);
    }

    /// <summary>
    /// Listener for deferred sonar pulse radius broadcasts.
    /// </summary>
    public interface ISonarPulseEventListener
    {
        /// <summary>Receives the authored sonar pulse radius.</summary>
        /// <param name="radius">Radius in world meters.</param>
        void OnSonarPulse(float radius);
    }

    /// <summary>
    /// Listener for deferred active sonar ping broadcasts.
    /// </summary>
    public interface ISonarPingEventListener
    {
        /// <summary>Receives the normalized active sonar ping intensity.</summary>
        /// <param name="intensity">Normalized intensity.</param>
        void OnSonarPingSent(float intensity);
    }

    /// <summary>
    /// Listener for deferred sonar contact snapshots.
    /// </summary>
    public interface ISonarSnapshotEventListener
    {
        /// <summary>Receives the latest spatial sonar contact snapshot.</summary>
        /// <param name="snapshot">Snapshot payload.</param>
        void OnSonarSnapshotUpdated(in SpatialSonarSnapshot snapshot);
    }

    /// <summary>
    /// Listener for deferred acoustic echo returns.
    /// </summary>
    public interface IAcousticEchoEventListener
    {
        /// <summary>Receives one active-sonar echo return.</summary>
        /// <param name="echoEvent">Echo payload.</param>
        void OnAcousticEchoReturned(in AcousticEchoEvent echoEvent);
    }

    /// <summary>
    /// Listener for deferred visual-only ping return signals.
    /// </summary>
    public interface IPingReturnSignalListener
    {
        /// <summary>Receives one DSP-timed active-sonar return blip.</summary>
        void OnPingReturnSignal(in PingReturnSignal signal);
    }

    /// <summary>
    /// Queue-backed spectrum bus drained by <see cref="SystemDispatcher"/> in LateUpdate.
    /// </summary>
    public static class SpectrumEvents
    {
        private const int ModeListenerCapacity = 8;
        private const int SonarPulseListenerCapacity = 8;
        private const int SonarPingListenerCapacity = 24;
        private const int SonarSnapshotListenerCapacity = 8;
        private const int AcousticEchoListenerCapacity = 8;
        private const int PingReturnSignalListenerCapacity = 8;

        private static FixedSpectrumListenerRegistry<ISpectrumModeEventListener> _modeListeners = FixedSpectrumListenerRegistry<ISpectrumModeEventListener>.Create(ModeListenerCapacity);
        private static FixedSpectrumListenerRegistry<ISonarPulseEventListener> _sonarPulseListeners = FixedSpectrumListenerRegistry<ISonarPulseEventListener>.Create(SonarPulseListenerCapacity);
        private static FixedSpectrumListenerRegistry<ISonarPingEventListener> _sonarPingListeners = FixedSpectrumListenerRegistry<ISonarPingEventListener>.Create(SonarPingListenerCapacity);
        private static FixedSpectrumListenerRegistry<ISonarSnapshotEventListener> _sonarSnapshotListeners = FixedSpectrumListenerRegistry<ISonarSnapshotEventListener>.Create(SonarSnapshotListenerCapacity);
        private static FixedSpectrumListenerRegistry<IAcousticEchoEventListener> _acousticEchoListeners = FixedSpectrumListenerRegistry<IAcousticEchoEventListener>.Create(AcousticEchoListenerCapacity);
        private static FixedSpectrumListenerRegistry<IPingReturnSignalListener> _pingReturnSignalListeners = FixedSpectrumListenerRegistry<IPingReturnSignalListener>.Create(PingReturnSignalListenerCapacity);

        // Fixed inline slots: SpectrumMode[8] - deferred spectrum mode lane - owner: SpectrumEvents
        private static FixedUiEventQueue<SpectrumMode> _pendingModeChanged;
        // Fixed inline slots: SpectrumMode[8] - next-frame spectrum mode lane - owner: SpectrumEvents
        private static FixedUiEventQueue<SpectrumMode> _nextFrameModeChanged;
        // Fixed inline slots: float[8] - deferred sonar pulse lane - owner: SpectrumEvents
        private static FixedUiEventQueue<float> _pendingSonarPulses;
        // Fixed inline slots: float[8] - next-frame sonar pulse lane - owner: SpectrumEvents
        private static FixedUiEventQueue<float> _nextFrameSonarPulses;
        // Fixed inline slots: float[24] - deferred active sonar ping lane - owner: SpectrumEvents
        private static FixedUiEventQueue<float> _pendingSonarPings;
        // Fixed inline slots: float[24] - next-frame active sonar ping lane - owner: SpectrumEvents
        private static FixedUiEventQueue<float> _nextFrameSonarPings;
        // Fixed inline slots: SpatialSonarSnapshot[8] - deferred sonar snapshot lane - owner: SpectrumEvents
        private static FixedUiEventQueue<SpatialSonarSnapshot> _pendingSonarSnapshots;
        // Fixed inline slots: SpatialSonarSnapshot[8] - next-frame sonar snapshot lane - owner: SpectrumEvents
        private static FixedUiEventQueue<SpatialSonarSnapshot> _nextFrameSonarSnapshots;
        // Fixed inline slots: AcousticEchoEvent[8] - deferred acoustic echo lane - owner: SpectrumEvents
        private static FixedUiEventQueue<AcousticEchoEvent> _pendingAcousticEchoes;
        // Fixed inline slots: AcousticEchoEvent[8] - next-frame acoustic echo lane - owner: SpectrumEvents
        private static FixedUiEventQueue<AcousticEchoEvent> _nextFrameAcousticEchoes;
        // Fixed inline slots: PingReturnSignal[16] - deferred visual ping return lane - owner: SpectrumEvents
        private static FixedUiEventQueue<PingReturnSignal> _pendingPingReturnSignals;
        // Fixed inline slots: PingReturnSignal[16] - next-frame visual ping return lane - owner: SpectrumEvents
        private static FixedUiEventQueue<PingReturnSignal> _nextFramePingReturnSignals;
        private static int _pendingModeChangedCount;
        private static int _nextFrameModeChangedCount;
        private static int _pendingSonarPulseCount;
        private static int _nextFrameSonarPulseCount;
        private static int _pendingSonarPingCount;
        private static int _nextFrameSonarPingCount;
        private static int _pendingSonarSnapshotCount;
        private static int _nextFrameSonarSnapshotCount;
        private static int _pendingAcousticEchoCount;
        private static int _nextFrameAcousticEchoCount;
        private static int _pendingPingReturnSignalCount;
        private static int _nextFramePingReturnSignalCount;
        private static bool _isDispatching;
        private const int PendingModeChangedCapacity = 8;
        private const int PendingSonarPulseCapacity = 8;
        private const int PendingSonarPingCapacity = 24;
        private const int PendingSonarSnapshotCapacity = 8;
        private const int PendingAcousticEchoCapacity = 8;
        private const int PendingPingReturnSignalCapacity = 16;
        private const int SpectrumListenerDispatchBudget = 64;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            DisposeQueues();
            _modeListeners.Clear();
            _sonarPulseListeners.Clear();
            _sonarPingListeners.Clear();
            _sonarSnapshotListeners.Clear();
            _acousticEchoListeners.Clear();
            _pingReturnSignalListeners.Clear();
            LastSonarPulseRadiusMeters = 0f;
        }

        /// <summary>Rezhim vizora izmenilsya.</summary>

        /// <summary>Sonar-puls. float: radius obnaruzheniya.</summary>
        /// <summary>Controller-authored active sonar ping. Float = normalized pulse intensity 0-1.</summary>

        /// <summary>Most recent emitted sonar pulse radius in authored meters.</summary>
        public static float LastSonarPulseRadiusMeters { get; private set; }

        /// <summary>Number of spectrum events waiting for LateUpdate dispatch.</summary>
        public static int PendingCount
        {
            get
            {
                return _pendingModeChangedCount
                    + _nextFrameModeChangedCount
                    + _pendingSonarPulseCount
                    + _nextFrameSonarPulseCount
                    + _pendingSonarPingCount
                    + _nextFrameSonarPingCount
                    + _pendingSonarSnapshotCount
                    + _nextFrameSonarSnapshotCount
                    + _pendingAcousticEchoCount
                    + _nextFrameAcousticEchoCount
                    + _pendingPingReturnSignalCount
                    + _nextFramePingReturnSignalCount;
            }
        }

        /// <summary>Registers a listener for spectrum mode changes.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterModeListener(ISpectrumModeEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _modeListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from spectrum mode changes.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterModeListener(ISpectrumModeEventListener listener)
        {
            if (listener == null)
                return;

            if (!_modeListeners.TryUnregister(listener))
                return;

            if (_modeListeners.Count <= 0)
                DropModeChanged();
        }

        /// <summary>Registers a listener for sonar pulse radius broadcasts.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarPulseListener(ISonarPulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _sonarPulseListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from sonar pulse radius broadcasts.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarPulseListener(ISonarPulseEventListener listener)
        {
            if (listener == null)
                return;

            if (!_sonarPulseListeners.TryUnregister(listener))
                return;

            if (_sonarPulseListeners.Count <= 0)
                DropSonarPulses();
        }

        /// <summary>Registers a listener for active sonar ping events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarPingListener(ISonarPingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _sonarPingListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from active sonar ping events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarPingListener(ISonarPingEventListener listener)
        {
            if (listener == null)
                return;

            if (!_sonarPingListeners.TryUnregister(listener))
                return;

            if (_sonarPingListeners.Count <= 0)
                DropSonarPings();
        }

        /// <summary>Registers a listener for sonar snapshots.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarSnapshotListener(ISonarSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _sonarSnapshotListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from sonar snapshots.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarSnapshotListener(ISonarSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            if (!_sonarSnapshotListeners.TryUnregister(listener))
                return;

            if (_sonarSnapshotListeners.Count <= 0)
                DropSonarSnapshots();
        }

        /// <summary>Registers a listener for acoustic echo returns.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterAcousticEchoListener(IAcousticEchoEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _acousticEchoListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from acoustic echo returns.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterAcousticEchoListener(IAcousticEchoEventListener listener)
        {
            if (listener == null)
                return;

            if (!_acousticEchoListeners.TryUnregister(listener))
                return;

            if (_acousticEchoListeners.Count <= 0)
                DropAcousticEchoes();
        }

        /// <summary>Registers a listener for visual-only ping returns.</summary>
        public static void RegisterPingReturnSignalListener(IPingReturnSignalListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _pingReturnSignalListeners.TryRegister(listener);
        }

        /// <summary>Unregisters a listener from visual-only ping returns.</summary>
        public static void UnregisterPingReturnSignalListener(IPingReturnSignalListener listener)
        {
            if (listener == null)
                return;

            if (!_pingReturnSignalListeners.TryUnregister(listener))
                return;

            if (_pingReturnSignalListeners.Count <= 0)
                DropPingReturnSignals();
        }

        /// <summary>Flushes queued spectrum payloads through registered listeners.</summary>
        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                completed = FlushModeChanged();
                if (completed)
                    completed = FlushSonarPulses();
                if (completed)
                    completed = FlushSonarPings();
                if (completed)
                    completed = FlushSonarSnapshots();
                if (completed)
                    completed = FlushAcousticEchoes();
                if (completed)
                    completed = FlushPingReturnSignals();
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        /// <summary>Queues a spectrum mode change.</summary>
        /// <param name="mode">New spectrum mode.</param>
        public static bool TryRaiseModeChanged(SpectrumMode mode)
        {
            if (_modeListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingModeChangedCount + _nextFrameModeChangedCount >= PendingModeChangedCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFrameModeChanged, ref _nextFrameModeChangedCount, in mode))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingModeChanged, ref _pendingModeChangedCount, in mode))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaiseModeChanged and handle bounded enqueue failure.", true)]
        public static void RaiseModeChanged(SpectrumMode mode)
        {
            TryRaiseModeChanged(mode);
        }

        /// <summary>Queues a sonar pulse radius broadcast.</summary>
        /// <param name="radius">Pulse radius in authored meters.</param>
        public static bool TryRaiseSonarPulse(float radius)
        {
            LastSonarPulseRadiusMeters = math.max(0f, radius);
            float pulseRadius = LastSonarPulseRadiusMeters;
            if (_sonarPulseListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingSonarPulseCount + _nextFrameSonarPulseCount >= PendingSonarPulseCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFrameSonarPulses, ref _nextFrameSonarPulseCount, in pulseRadius))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingSonarPulses, ref _pendingSonarPulseCount, in pulseRadius))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaiseSonarPulse and handle bounded enqueue failure.", true)]
        public static void RaiseSonarPulse(float radius)
        {
            TryRaiseSonarPulse(radius);
        }

        /// <summary>Queues an active sonar ping broadcast.</summary>
        /// <param name="intensity">Normalized ping intensity.</param>
        public static bool TryRaiseSonarPingSent(float intensity)
        {
            if (_sonarPingListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingSonarPingCount + _nextFrameSonarPingCount >= PendingSonarPingCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFrameSonarPings, ref _nextFrameSonarPingCount, in intensity))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingSonarPings, ref _pendingSonarPingCount, in intensity))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaiseSonarPingSent and handle bounded enqueue failure.", true)]
        public static void RaiseSonarPingSent(float intensity)
        {
            TryRaiseSonarPingSent(intensity);
        }

        /// <summary>Queues an updated spatial sonar snapshot.</summary>
        /// <param name="snapshot">Snapshot payload.</param>
        public static bool TryRaiseSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            if (_sonarSnapshotListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingSonarSnapshotCount + _nextFrameSonarSnapshotCount >= PendingSonarSnapshotCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFrameSonarSnapshots, ref _nextFrameSonarSnapshotCount, in snapshot))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingSonarSnapshots, ref _pendingSonarSnapshotCount, in snapshot))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaiseSonarSnapshotUpdated and handle bounded enqueue failure.", true)]
        public static void RaiseSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            TryRaiseSonarSnapshotUpdated(snapshot);
        }

        /// <summary>Queues one acoustic echo return.</summary>
        /// <param name="echoEvent">Echo payload.</param>
        public static bool TryRaiseAcousticEchoReturned(AcousticEchoEvent echoEvent)
        {
            if (_acousticEchoListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingAcousticEchoCount + _nextFrameAcousticEchoCount >= PendingAcousticEchoCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFrameAcousticEchoes, ref _nextFrameAcousticEchoCount, in echoEvent))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingAcousticEchoes, ref _pendingAcousticEchoCount, in echoEvent))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaiseAcousticEchoReturned and handle bounded enqueue failure.", true)]
        public static void RaiseAcousticEchoReturned(AcousticEchoEvent echoEvent)
        {
            TryRaiseAcousticEchoReturned(echoEvent);
        }

        /// <summary>Queues one visual-only DSP-timed ping return.</summary>
        public static bool TryRaisePingReturnSignal(in PingReturnSignal signal)
        {
            if (_pingReturnSignalListeners.Count <= 0)
                return false;

            EnsureInitialized();
            if (_pendingPingReturnSignalCount + _nextFramePingReturnSignalCount >= PendingPingReturnSignalCapacity)
                return false;

            if (_isDispatching)
            {
                if (!EnqueueInto(ref _nextFramePingReturnSignals, ref _nextFramePingReturnSignalCount, in signal))
                    return false;
            }
            else
            {
                if (!EnqueueInto(ref _pendingPingReturnSignals, ref _pendingPingReturnSignalCount, in signal))
                    return false;
            }

            return true;
        }

        [Obsolete("Spectrum producers must use TryRaisePingReturnSignal and handle bounded enqueue failure.", true)]
        public static void RaisePingReturnSignal(in PingReturnSignal signal)
        {
            TryRaisePingReturnSignal(in signal);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingModeChanged.IsCreated)
                _pendingModeChanged.Configure(PendingModeChangedCapacity);
            if (!_nextFrameModeChanged.IsCreated)
                _nextFrameModeChanged.Configure(PendingModeChangedCapacity);
            if (!_pendingSonarPulses.IsCreated)
                _pendingSonarPulses.Configure(PendingSonarPulseCapacity);
            if (!_nextFrameSonarPulses.IsCreated)
                _nextFrameSonarPulses.Configure(PendingSonarPulseCapacity);
            if (!_pendingSonarPings.IsCreated)
                _pendingSonarPings.Configure(PendingSonarPingCapacity);
            if (!_nextFrameSonarPings.IsCreated)
                _nextFrameSonarPings.Configure(PendingSonarPingCapacity);
            if (!_pendingSonarSnapshots.IsCreated)
                _pendingSonarSnapshots.Configure(PendingSonarSnapshotCapacity);
            if (!_nextFrameSonarSnapshots.IsCreated)
                _nextFrameSonarSnapshots.Configure(PendingSonarSnapshotCapacity);
            if (!_pendingAcousticEchoes.IsCreated)
                _pendingAcousticEchoes.Configure(PendingAcousticEchoCapacity);
            if (!_nextFrameAcousticEchoes.IsCreated)
                _nextFrameAcousticEchoes.Configure(PendingAcousticEchoCapacity);
            if (!_pendingPingReturnSignals.IsCreated)
                _pendingPingReturnSignals.Configure(PendingPingReturnSignalCapacity);
            if (!_nextFramePingReturnSignals.IsCreated)
                _nextFramePingReturnSignals.Configure(PendingPingReturnSignalCapacity);
        }

        private static bool EnqueueInto<T>(ref FixedUiEventQueue<T> queue, ref int count, in T payload)
            where T : unmanaged
        {
            if (!queue.Enqueue(in payload))
                return false;

            count++;
            return true;
        }

        private static bool FlushModeChanged()
        {
            if (!_pendingModeChanged.IsCreated)
                return true;

            if (_modeListeners.Count <= 0)
            {
                DropModeChanged();
                return true;
            }

            int scanBudget = _pendingModeChangedCount > 0 ? _pendingModeChangedCount : PendingModeChangedCapacity;
            while (scanBudget > 0 && !_pendingModeChanged.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingModeChanged.TryDequeue(out SpectrumMode mode))
                {
                    _pendingModeChangedCount = 0;
                    return true;
                }

                if (_pendingModeChangedCount > 0)
                    _pendingModeChangedCount--;
                scanBudget--;
                int count = math.min(_modeListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISpectrumModeEventListener listener = _modeListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnSpectrumModeChanged(mode);
                }

            }

            if (_pendingModeChanged.IsEmpty())
                _pendingModeChangedCount = 0;

            return true;
        }

        private static bool FlushSonarPulses()
        {
            if (!_pendingSonarPulses.IsCreated)
                return true;

            if (_sonarPulseListeners.Count <= 0)
            {
                DropSonarPulses();
                return true;
            }

            int scanBudget = _pendingSonarPulseCount > 0 ? _pendingSonarPulseCount : PendingSonarPulseCapacity;
            while (scanBudget > 0 && !_pendingSonarPulses.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarPulses.TryDequeue(out float radius))
                {
                    _pendingSonarPulseCount = 0;
                    return true;
                }

                if (_pendingSonarPulseCount > 0)
                    _pendingSonarPulseCount--;
                scanBudget--;
                int count = math.min(_sonarPulseListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarPulseEventListener listener = _sonarPulseListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnSonarPulse(radius);
                }

            }

            if (_pendingSonarPulses.IsEmpty())
                _pendingSonarPulseCount = 0;

            return true;
        }

        private static bool FlushSonarPings()
        {
            if (!_pendingSonarPings.IsCreated)
                return true;

            if (_sonarPingListeners.Count <= 0)
            {
                DropSonarPings();
                return true;
            }

            int scanBudget = _pendingSonarPingCount > 0 ? _pendingSonarPingCount : PendingSonarPingCapacity;
            while (scanBudget > 0 && !_pendingSonarPings.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarPings.TryDequeue(out float intensity))
                {
                    _pendingSonarPingCount = 0;
                    return true;
                }

                if (_pendingSonarPingCount > 0)
                    _pendingSonarPingCount--;
                scanBudget--;
                int count = math.min(_sonarPingListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarPingEventListener listener = _sonarPingListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnSonarPingSent(intensity);
                }

            }

            if (_pendingSonarPings.IsEmpty())
                _pendingSonarPingCount = 0;

            return true;
        }

        private static bool FlushSonarSnapshots()
        {
            if (!_pendingSonarSnapshots.IsCreated)
                return true;

            if (_sonarSnapshotListeners.Count <= 0)
            {
                DropSonarSnapshots();
                return true;
            }

            int scanBudget = _pendingSonarSnapshotCount > 0 ? _pendingSonarSnapshotCount : PendingSonarSnapshotCapacity;
            while (scanBudget > 0 && !_pendingSonarSnapshots.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarSnapshots.TryDequeue(out SpatialSonarSnapshot snapshot))
                {
                    _pendingSonarSnapshotCount = 0;
                    return true;
                }

                if (_pendingSonarSnapshotCount > 0)
                    _pendingSonarSnapshotCount--;
                scanBudget--;
                int count = math.min(_sonarSnapshotListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarSnapshotEventListener listener = _sonarSnapshotListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnSonarSnapshotUpdated(in snapshot);
                }

            }

            if (_pendingSonarSnapshots.IsEmpty())
                _pendingSonarSnapshotCount = 0;

            return true;
        }

        private static bool FlushAcousticEchoes()
        {
            if (!_pendingAcousticEchoes.IsCreated)
                return true;

            if (_acousticEchoListeners.Count <= 0)
            {
                DropAcousticEchoes();
                return true;
            }

            int scanBudget = _pendingAcousticEchoCount > 0 ? _pendingAcousticEchoCount : PendingAcousticEchoCapacity;
            while (scanBudget > 0 && !_pendingAcousticEchoes.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingAcousticEchoes.TryDequeue(out AcousticEchoEvent echoEvent))
                {
                    _pendingAcousticEchoCount = 0;
                    return true;
                }

                if (_pendingAcousticEchoCount > 0)
                    _pendingAcousticEchoCount--;
                scanBudget--;
                int count = math.min(_acousticEchoListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    IAcousticEchoEventListener listener = _acousticEchoListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnAcousticEchoReturned(in echoEvent);
                }
            }

            if (_pendingAcousticEchoes.IsEmpty())
                _pendingAcousticEchoCount = 0;

            return true;
        }

        private static bool FlushPingReturnSignals()
        {
            if (!_pendingPingReturnSignals.IsCreated)
                return true;

            if (_pingReturnSignalListeners.Count <= 0)
            {
                DropPingReturnSignals();
                return true;
            }

            int scanBudget = _pendingPingReturnSignalCount > 0 ? _pendingPingReturnSignalCount : PendingPingReturnSignalCapacity;
            while (scanBudget > 0 && !_pendingPingReturnSignals.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingPingReturnSignals.TryDequeue(out PingReturnSignal signal))
                {
                    _pendingPingReturnSignalCount = 0;
                    return true;
                }

                if (_pendingPingReturnSignalCount > 0)
                    _pendingPingReturnSignalCount--;
                scanBudget--;
                int count = math.min(_pingReturnSignalListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    IPingReturnSignalListener listener = _pingReturnSignalListeners.GetAt(i);
                    if (listener == null)
                        continue;

                    listener.OnPingReturnSignal(in signal);
                }
            }

            if (_pendingPingReturnSignals.IsEmpty())
                _pendingPingReturnSignalCount = 0;

            return true;
        }

        private static void DisposeQueues()
        {
            _pendingModeChanged.Clear();
            _nextFrameModeChanged.Clear();
            _pendingSonarPulses.Clear();
            _nextFrameSonarPulses.Clear();
            _pendingSonarPings.Clear();
            _nextFrameSonarPings.Clear();
            _pendingSonarSnapshots.Clear();
            _nextFrameSonarSnapshots.Clear();
            _pendingAcousticEchoes.Clear();
            _nextFrameAcousticEchoes.Clear();
            _pendingPingReturnSignals.Clear();
            _nextFramePingReturnSignals.Clear();
            _pendingModeChangedCount = 0;
            _nextFrameModeChangedCount = 0;
            _pendingSonarPulseCount = 0;
            _nextFrameSonarPulseCount = 0;
            _pendingSonarPingCount = 0;
            _nextFrameSonarPingCount = 0;
            _pendingSonarSnapshotCount = 0;
            _nextFrameSonarSnapshotCount = 0;
            _pendingAcousticEchoCount = 0;
            _nextFrameAcousticEchoCount = 0;
            _pendingPingReturnSignalCount = 0;
            _nextFramePingReturnSignalCount = 0;
            _isDispatching = false;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingModeChanged.IsCreated && !_pendingModeChanged.IsEmpty())
                || (_pendingSonarPulses.IsCreated && !_pendingSonarPulses.IsEmpty())
                || (_pendingSonarPings.IsCreated && !_pendingSonarPings.IsEmpty())
                || (_pendingSonarSnapshots.IsCreated && !_pendingSonarSnapshots.IsEmpty())
                || (_pendingAcousticEchoes.IsCreated && !_pendingAcousticEchoes.IsEmpty())
                || (_pendingPingReturnSignals.IsCreated && !_pendingPingReturnSignals.IsEmpty());
        }

        private struct FixedSpectrumListenerRegistry<T>
            where T : class
        {
            private const int MaxCapacity = 24;
            private int _capacity;
            private int _count;
            private T _slot0;
            private T _slot1;
            private T _slot2;
            private T _slot3;
            private T _slot4;
            private T _slot5;
            private T _slot6;
            private T _slot7;
            private T _slot8;
            private T _slot9;
            private T _slot10;
            private T _slot11;
            private T _slot12;
            private T _slot13;
            private T _slot14;
            private T _slot15;
            private T _slot16;
            private T _slot17;
            private T _slot18;
            private T _slot19;
            private T _slot20;
            private T _slot21;
            private T _slot22;
            private T _slot23;

            public int Count => _count;

            public static FixedSpectrumListenerRegistry<T> Create(int capacity)
            {
                FixedSpectrumListenerRegistry<T> registry = default;
                registry._capacity = capacity < 0 ? 0 : capacity > MaxCapacity ? MaxCapacity : capacity;
                return registry;
            }

            public void Clear()
            {
                _slot0 = null;
                _slot1 = null;
                _slot2 = null;
                _slot3 = null;
                _slot4 = null;
                _slot5 = null;
                _slot6 = null;
                _slot7 = null;
                _slot8 = null;
                _slot9 = null;
                _slot10 = null;
                _slot11 = null;
                _slot12 = null;
                _slot13 = null;
                _slot14 = null;
                _slot15 = null;
                _slot16 = null;
                _slot17 = null;
                _slot18 = null;
                _slot19 = null;
                _slot20 = null;
                _slot21 = null;
                _slot22 = null;
                _slot23 = null;
                _count = 0;
            }

            public bool TryRegister(T listener)
            {
                if (listener == null || _count >= _capacity || Contains(listener))
                    return false;

                SetAt(_count, listener);
                _count++;
                return true;
            }

            public bool TryUnregister(T listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(GetAt(i), listener))
                        continue;

                    _count--;
                    SetAt(i, GetAt(_count));
                    SetAt(_count, null);
                    return true;
                }

                return false;
            }

            public bool Contains(T listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(GetAt(i), listener))
                        return true;
                }

                return false;
            }

            public T GetAt(int index)
            {
                return index switch
                {
                    0 => _slot0,
                    1 => _slot1,
                    2 => _slot2,
                    3 => _slot3,
                    4 => _slot4,
                    5 => _slot5,
                    6 => _slot6,
                    7 => _slot7,
                    8 => _slot8,
                    9 => _slot9,
                    10 => _slot10,
                    11 => _slot11,
                    12 => _slot12,
                    13 => _slot13,
                    14 => _slot14,
                    15 => _slot15,
                    16 => _slot16,
                    17 => _slot17,
                    18 => _slot18,
                    19 => _slot19,
                    20 => _slot20,
                    21 => _slot21,
                    22 => _slot22,
                    23 => _slot23,
                    _ => null
                };
            }

            private void SetAt(int index, T listener)
            {
                switch (index)
                {
                    case 0:
                        _slot0 = listener;
                        break;
                    case 1:
                        _slot1 = listener;
                        break;
                    case 2:
                        _slot2 = listener;
                        break;
                    case 3:
                        _slot3 = listener;
                        break;
                    case 4:
                        _slot4 = listener;
                        break;
                    case 5:
                        _slot5 = listener;
                        break;
                    case 6:
                        _slot6 = listener;
                        break;
                    case 7:
                        _slot7 = listener;
                        break;
                    case 8:
                        _slot8 = listener;
                        break;
                    case 9:
                        _slot9 = listener;
                        break;
                    case 10:
                        _slot10 = listener;
                        break;
                    case 11:
                        _slot11 = listener;
                        break;
                    case 12:
                        _slot12 = listener;
                        break;
                    case 13:
                        _slot13 = listener;
                        break;
                    case 14:
                        _slot14 = listener;
                        break;
                    case 15:
                        _slot15 = listener;
                        break;
                    case 16:
                        _slot16 = listener;
                        break;
                    case 17:
                        _slot17 = listener;
                        break;
                    case 18:
                        _slot18 = listener;
                        break;
                    case 19:
                        _slot19 = listener;
                        break;
                    case 20:
                        _slot20 = listener;
                        break;
                    case 21:
                        _slot21 = listener;
                        break;
                    case 22:
                        _slot22 = listener;
                        break;
                    case 23:
                        _slot23 = listener;
                        break;
                }
            }
        }

        private static void DropModeChanged()
        {
            if (_pendingModeChanged.IsCreated)
            {
                while (_pendingModeChanged.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameModeChanged.IsCreated)
            {
                while (_nextFrameModeChanged.TryDequeue(out _))
                {
                }
            }

            _pendingModeChangedCount = 0;
            _nextFrameModeChangedCount = 0;
        }

        private static void DropSonarPulses()
        {
            if (_pendingSonarPulses.IsCreated)
            {
                while (_pendingSonarPulses.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameSonarPulses.IsCreated)
            {
                while (_nextFrameSonarPulses.TryDequeue(out _))
                {
                }
            }

            _pendingSonarPulseCount = 0;
            _nextFrameSonarPulseCount = 0;
        }

        private static void DropSonarPings()
        {
            if (_pendingSonarPings.IsCreated)
            {
                while (_pendingSonarPings.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameSonarPings.IsCreated)
            {
                while (_nextFrameSonarPings.TryDequeue(out _))
                {
                }
            }

            _pendingSonarPingCount = 0;
            _nextFrameSonarPingCount = 0;
        }

        private static void DropSonarSnapshots()
        {
            if (_pendingSonarSnapshots.IsCreated)
            {
                while (_pendingSonarSnapshots.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameSonarSnapshots.IsCreated)
            {
                while (_nextFrameSonarSnapshots.TryDequeue(out _))
                {
                }
            }

            _pendingSonarSnapshotCount = 0;
            _nextFrameSonarSnapshotCount = 0;
        }

        private static void DropAcousticEchoes()
        {
            if (_pendingAcousticEchoes.IsCreated)
            {
                while (_pendingAcousticEchoes.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameAcousticEchoes.IsCreated)
            {
                while (_nextFrameAcousticEchoes.TryDequeue(out _))
                {
                }
            }

            _pendingAcousticEchoCount = 0;
            _nextFrameAcousticEchoCount = 0;
        }

        private static void DropPingReturnSignals()
        {
            if (_pendingPingReturnSignals.IsCreated)
            {
                while (_pendingPingReturnSignals.TryDequeue(out _))
                {
                }
            }

            if (_nextFramePingReturnSignals.IsCreated)
            {
                while (_nextFramePingReturnSignals.TryDequeue(out _))
                {
                }
            }

            _pendingPingReturnSignalCount = 0;
            _nextFramePingReturnSignalCount = 0;
        }

        private static void PromoteNextFrameEvents()
        {
            PromoteQueue(
                ref _nextFrameModeChanged,
                ref _nextFrameModeChangedCount,
                ref _pendingModeChanged,
                ref _pendingModeChangedCount,
                PendingModeChangedCapacity);
            PromoteQueue(
                ref _nextFrameSonarPulses,
                ref _nextFrameSonarPulseCount,
                ref _pendingSonarPulses,
                ref _pendingSonarPulseCount,
                PendingSonarPulseCapacity);
            PromoteQueue(
                ref _nextFrameSonarPings,
                ref _nextFrameSonarPingCount,
                ref _pendingSonarPings,
                ref _pendingSonarPingCount,
                PendingSonarPingCapacity);
            PromoteQueue(
                ref _nextFrameSonarSnapshots,
                ref _nextFrameSonarSnapshotCount,
                ref _pendingSonarSnapshots,
                ref _pendingSonarSnapshotCount,
                PendingSonarSnapshotCapacity);
            PromoteQueue(
                ref _nextFrameAcousticEchoes,
                ref _nextFrameAcousticEchoCount,
                ref _pendingAcousticEchoes,
                ref _pendingAcousticEchoCount,
                PendingAcousticEchoCapacity);
            PromoteQueue(
                ref _nextFramePingReturnSignals,
                ref _nextFramePingReturnSignalCount,
                ref _pendingPingReturnSignals,
                ref _pendingPingReturnSignalCount,
                PendingPingReturnSignalCapacity);
        }

        private static void PromoteQueue<T>(
            ref FixedUiEventQueue<T> nextFrameQueue,
            ref int nextFrameCount,
            ref FixedUiEventQueue<T> pendingQueue,
            ref int pendingCount,
            int pendingCapacity)
            where T : unmanaged
        {
            if (!nextFrameQueue.IsCreated)
            {
                nextFrameCount = 0;
                return;
            }

            if (nextFrameCount <= 0)
            {
                while (nextFrameQueue.TryDequeue(out _))
                {
                }

                nextFrameCount = 0;
                return;
            }

            if (!pendingQueue.IsCreated || pendingCapacity <= 0)
            {
                while (nextFrameQueue.TryDequeue(out _))
                {
                }

                nextFrameCount = 0;
                return;
            }

            int room = math.max(0, pendingCapacity - pendingCount);
            while (nextFrameCount > 0 && room > 0 && nextFrameQueue.TryDequeue(out T payload))
            {
                nextFrameCount--;
                room--;
                if (!pendingQueue.Enqueue(in payload))
                    break;

                pendingCount++;
            }

            while (nextFrameCount > 0 && nextFrameQueue.TryDequeue(out _))
                nextFrameCount--;

            if (nextFrameQueue.IsEmpty())
                nextFrameCount = 0;

            if (pendingQueue.IsEmpty())
                pendingCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class SpectrumSystem : MonoBehaviour, ILateFrameTickable, IAcousticEchoEventListener, IPingReturnSignalListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001SpectrumSystemSignalPushDropCount;
        private const ushort PhysicsEventTypeAcousticPing = (ushort)PhysicsEventType.AcousticPing;
        private const ushort PhysicsEventTypeAcousticImpulse = (ushort)PhysicsEventType.AcousticImpulse;
        private const uint AcousticImpulseFlagLeviathan = 1u << 1;
        private const uint AcousticImpulseFlagLarge = 1u << 3;
        private const int PassiveRadarAzimuthSectorCount = 8;
        private const int PassiveRadarElevationSectorCount = 4;
        private const int PassiveRadarSectorCount = PassiveRadarAzimuthSectorCount * PassiveRadarElevationSectorCount;
        private const int PassiveRadarSourceBudget = 8;
        private const int PassiveRadarAutoGainHistoryLength = 30;
        private const int PassiveRadarSlowTickHz = 10;
        private const float PassiveRadarTickIntervalSeconds = 1f / PassiveRadarSlowTickHz;
        private const float PassiveRadarDecayFactor = 0.75f;
        private const float PassiveRadarEnergyEpsilon = 0.00001f;
        private const float PassiveRadarMinimumDistanceMeters = 0.5f;
        private const float PassiveRadarMaxSourceDistanceMeters = 30f;
        private const float ShaderScalarPublishEpsilon = 0.0001f;
        private const float ShaderVectorPublishEpsilon = 0.00001f;
        private const uint AupDiscoveryDiscoveredBit = 1u;
        private const int ActiveSonarGeoPingCapacity = 4;
        private const int ActiveSonarGeoTelemetryCapacity = 300;
        private const int ActiveSonarGeoTelemetryEntrySizeBytes = 32;
        private const SystemID SpectrumVaultOwner = SystemID.UI;
        private const float ActiveSonarGeoSpeedMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        private const float ActiveSonarGeoMaxRangeMeters = 400f;
        private static readonly BufferID AupDiscoveryGridBufferId = (BufferID)71030;
        private static readonly BufferID ActiveSonarGeoTelemetryRingBufferId = (BufferID)71031;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Radius sonara (metry).")]
        [SerializeField] private float sonarRadius = 100f;

        [Tooltip("Interval sonar-pulsa (sek).")]
        [SerializeField] private float sonarPulseInterval = 3f;

        [Tooltip("Energiya za pereklyuchenie rezhima.")]
        [SerializeField] private float modeSwitchEnergyCost = 2f;

        [Tooltip("Energiya, szhigaemaya kazhdym aktivnym sonar pulse.")]
        [SerializeField] private float sonarPulseEnergyCost = 6f;

        [Tooltip("Intensivnost shumovoy signatury, publikuemoy sonar pulse dlya okruzhayuschey fauny.")]
        [SerializeField, Range(0f, 1f)] private float sonarNoiseSignature01 = 1f;


        [Tooltip("How long the active sonar reveal stays valid for shader and VFX consumers after each pulse.")]
        [SerializeField] private float sonarRevealDuration = 2.4f;

        [Tooltip("How fast the authored active-sonar wavefront travels through the reveal buffer in meters per second.")]
        [SerializeField] private float sonarRevealWaveSpeed = 1500f;

        [Tooltip("How long each revealed contact stays bright after the sonar wavefront reaches it.")]
        [SerializeField] private float sonarRevealFadeDuration = 3f;

        [Header("LIDAR Sync")]
        [Tooltip("How quickly the renderer-owned LIDAR persistence flash decays after an active sonar peak.")]
        [SerializeField, Range(0.25f, 20f)] private float lidarPersistenceDecaySharpness = 7.5f;

        [Header("Abyssal Sonar Distortion")]
        [Tooltip("Depth where abyssal water starts slowing active-sonar propagation and destabilizing returns.")]
        [SerializeField, Range(100f, 6000f)] private float abyssalDistortionStartDepth = 2000f;

        [Tooltip("Depth where abyssal sonar distortion reaches full authored strength.")]
        [SerializeField, Range(200f, 8000f)] private float abyssalDistortionFullDepth = 4000f;

        [Tooltip("Minimum fraction of the authored sonar wave speed retained at full abyssal distortion.")]
        [SerializeField, Range(0.05f, 1f)] private float abyssalWaveSpeedScaleMin = 0.42f;

        [Tooltip("Legacy abyssal return scalar preserved as a cinematic bias for non-physical anchor response strength.")]
        [SerializeField, Range(0f, 12f)] private float abyssalContactJitterRadius = 2.8f;

        [Header("Screen-Space Acoustic Mapping")]
        [Tooltip("Distance where Leviathan fauna stop rendering as bodies and require active sonar to silhouette them.")]
        [SerializeField, Range(0f, 300f)] private float sonarNoirHideDistanceMeters = 44f;

        [Tooltip("Cinematic screen-space wave speed. Detection math keeps the authored acoustic speed; this only controls the visible pulse.")]
        [SerializeField, Range(1f, 300f)] private float sonarScreenSpacePulseSpeedMetersPerSecond = 96f;

        [Tooltip("Multiplier applied to the weaker reflected visual wave spawned by active-sonar echo returns.")]
        [SerializeField, Range(0.05f, 1f)] private float sonarEchoVisualIntensityScale = 0.38f;

        [Tooltip("Speed fraction for reflected visual waves. This is a cinematic fake, not acoustic travel simulation.")]
        [SerializeField, Range(0.05f, 1f)] private float sonarEchoVisualSpeedScale = 0.58f;

        [Tooltip("Energy multiplier for the large acoustic impulse raised by active sonar pings.")]
        [SerializeField, Range(0.1f, 8f)] private float sonarAggroImpulseEnergyScale = 2f;

        [Tooltip("Player speed where acoustic radar ghosting starts.")]
        [SerializeField, Range(0f, 60f)] private float radarDistortionStartSpeedMetersPerSecond = 12f;

        [Tooltip("Player speed where acoustic radar ghosting reaches full strength.")]
        [SerializeField, Range(0.1f, 90f)] private float radarDistortionFullSpeedMetersPerSecond = 28f;

        [Tooltip("Decay rate for Leviathan-scream radar distortion.")]
        [SerializeField, Range(0.1f, 12f)] private float leviathanScreamRadarDecayPerSecond = 2.4f;

        [Header("AUP Discovery Grid")]
        [Tooltip("Persistent sonar-discovery grid width. Rounded to at least 8 cells.")]
        [SerializeField, Range(8, 1024)] private int aupDiscoveryGridWidth = 256;

        [Tooltip("Persistent sonar-discovery grid height. Rounded to at least 8 cells.")]
        [SerializeField, Range(8, 1024)] private int aupDiscoveryGridHeight = 256;

        [Tooltip("AUP meters represented by one discovery-grid cell.")]
        [SerializeField, Range(1f, 128f)] private float aupDiscoveryCellSizeMeters = 16f;

        [Header("── References ──────────────────────────────")]
        [Tooltip("Sistema vyzhivaniya dlya drain energii.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Tooltip("Optional cartographer bridge used to bias sonar contacts toward organic returns when vegetation owns the space.")]
        [SerializeField] private HectonMapMagicVegetationBridge vegetationBridge;

        [Header("── Sonar Grid Overlay ──────────────────────")]
        [Tooltip("Master intensity for the noir sonar-grid overlay rendered on the visor during active pings.")]
        [SerializeField, Range(0f, 3f)] private float sonarGridIntensity = 1.15f;

        [Tooltip("World-space line density used by the visor sonar grid.")]
        [SerializeField, Range(0.05f, 2f)] private float sonarGridLineScale = 0.22f;

        [Tooltip("Half-width of the projected noir grid lines.")]
        [SerializeField, Range(0.001f, 0.08f)] private float sonarGridLineWidth = 0.018f;

        [Tooltip("Boost applied to scene-depth contour edges when the sonar wavefront crosses geometry.")]
        [SerializeField, Range(0f, 8f)] private float sonarGridContourBoost = 2.4f;

        [Tooltip("Tint used for hard structure echoes such as base walls, wreckage, and modules.")]
        [SerializeField] private Color sonarGridHardColor = new Color(0.18f, 1f, 0.94f, 1f);

        [Tooltip("Tint used for softer organic sonar echoes.")]
        [SerializeField] private Color sonarGridOrganicColor = new Color(0.44f, 1f, 0.58f, 1f);

        [Tooltip("Tint reserved for cartographer-owned abyssal anchors so tectonic landmarks read as hostile signatures.")]
        [SerializeField] private Color sonarGridAbyssalColor = new Color(0.86f, 0.34f, 1f, 1f);

        [Header("── Abyssal Anchor Return ──────────────────")]
        [Tooltip("Optional ominous 2D return layered onto active sonar when the ping intersects an abyssal anchor.")]
        [SerializeField] private AudioClip abyssalAnchorReturnClip;

        [Tooltip("Minimum helmet-return volume when the ping only grazes the edge of an abyssal anchor.")]
        [SerializeField, Range(0f, 1f)] private float abyssalAnchorReturnVolumeMin = 0.22f;

        [Tooltip("Maximum helmet-return volume when the player pings directly through an abyssal anchor.")]
        [SerializeField, Range(0f, 1f)] private float abyssalAnchorReturnVolumeMax = 0.64f;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static SpectrumSystem s_activeRuntimeInstance;

        public static SpectrumSystem Instance => s_activeRuntimeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeOwnerState()
        {
            s_activeRuntimeInstance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SpectrumMode _currentMode = SpectrumMode.Normal;
        private float _sonarTimer;
        private bool _registeredLateFrame;
        private bool _serviceRegistered;
        private bool _acousticSignalSubscribed;
        private int _lastPhysicsEventSnapshotGeneration;
        private bool _hasSonarSnapshot;
        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private SpatialSonarSnapshot _lastSonarSnapshot;
        private float _activeSonarWaveFront;
        private float _activeSonarWaveSpeed;
        private float _activeSonarRevealExpireTime;
        private float _activeSonarWaveBandWidth;
        private bool _activeSonarWavefrontActive;
        private float _activeLidarPersistence;
        private float _passiveRadarTickAccumulator;
        private float _passiveRadarPeakEnergy;
        private float _passiveRadarAutoGain = 1f;
        private int _passiveRadarPeakSector = -1;
        private int _passiveRadarAutoGainWriteIndex;
        private float _activeSonarVisualExpireTime;
        private float _activeSonarEchoExpireTime;
        private Vector3 _lastResolvedPlayerForward = Vector3.forward;
        private int _lastPublishedSonarActiveState = -1;
        private float _leviathanScreamRadarDistortion01;
        private IDataVault _dataVault;
        private IAudioService _audioService;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private ISpatialAudioWorldEmitterReadModel _spatialAudioEmitterReadModel;
        private VaultGenerationHandle<uint> _aupDiscoveryGridHandle;
        private VaultGenerationHandle<ActiveSonarGeoTelemetryEntry> _activeSonarGeoTelemetryRingHandle;
        private int _aupDiscoveryGridWidthRuntime;
        private int _aupDiscoveryGridHeightRuntime;
        private float _aupDiscoveryCellSizeRuntime;
        private int _lastPublishedSpectrumMode = int.MinValue;
        private float _lastPublishedLidarPersistence = -1f;
        private bool _hasPublishedAcousticMappingStaticGlobals;
        private Vector4 _lastPublishedSonarColor;
        private float _lastPublishedNoirHideDistance = -1f;
        private bool _hasPublishedRadarDistortion;
        private Vector4 _lastPublishedRadarDistortion;
        private bool _passiveRadarShaderRowsInitialized;
        private bool _hasPublishedPassiveRadarPeak;
        private Vector4 _lastPublishedPassiveRadarPeak;
        private float _lastPublishedPassiveRadarAutoGain = -1f;
        private float _lastPublishedSonarRadius = -1f;
        private float _lastPublishedSonarWaveFront = -1f;
        private int _activeSonarGeoPingCount;
        private int _lastConsumedActiveSonarAcousticSequence;
        private int _activeSonarGeoTelemetryWriteIndex;
        private int _lastPublishedActiveSonarGeoCount = -1;
        private int _lastTelemetryPublishedActiveSonarGeoCount = -1;
        private float _lastPublishedActiveSonarGeoRadius = -1f;
        private Vector4 _lastPublishedActiveSonarGeoState;
        private bool _activeSonarGeoGlobalsDirty = true;
        private bool _hotSwapRegistered;

        // Cached shader IDs
        private static readonly int _ShaderSpectrumMode =
            Shader.PropertyToID("_SpectrumMode");
        private static readonly int _ShaderSonarRadius =
            Shader.PropertyToID("_SonarRadius");
        private static readonly int _ShaderSonarPulseTime =
            Shader.PropertyToID("_SonarPulseTime");
        private static readonly int _ShaderSonarPingCenter =
            Shader.PropertyToID("_SonarPingCenter");
        private static readonly int _ShaderSonarPingParams =
            Shader.PropertyToID("_SonarPingParams");
        private static readonly int _ShaderSonarRevealOrigin =
            Shader.PropertyToID("_SonarRevealOriginWS");
        private static readonly int _ShaderSonarRevealExpireTime =
            Shader.PropertyToID("_SonarRevealExpireTime");
        private static readonly int _ShaderSonarRevealWaveParams =
            Shader.PropertyToID("_SonarRevealWaveParams");
        private static readonly int _ShaderSonarWaveFront =
            Shader.PropertyToID("_SonarWaveFront");
        private static readonly int _ShaderAbyssalDistortion =
            Shader.PropertyToID("_AbyssalDistortion");
        private static readonly int _ShaderLidarPersistence =
            Shader.PropertyToID("_LidarPersistence");
        private static readonly int _ShaderPassiveRadarRows =
            Shader.PropertyToID("_PassiveRadarRows");
        private static readonly int _ShaderPassiveRadarPeak =
            Shader.PropertyToID("_PassiveRadarPeak");
        private static readonly int _ShaderPassiveRadarAutoGain =
            Shader.PropertyToID("_PassiveRadarAutoGain");
        private static readonly int _ShaderHectonSonarPrimaryPulse =
            Shader.PropertyToID("_HectonSonarPrimaryPulse");
        private static readonly int _ShaderHectonSonarEchoPulse =
            Shader.PropertyToID("_HectonSonarEchoPulse");
        private static readonly int _ShaderHectonSonarVisualParams =
            Shader.PropertyToID("_HectonSonarVisualParams");
        private static readonly int _ShaderHectonSonarEchoParams =
            Shader.PropertyToID("_HectonSonarEchoParams");
        private static readonly int _ShaderHectonSonarColor =
            Shader.PropertyToID("_HectonSonarColor");
        private static readonly int _ShaderHectonSonarNoirHideDistance =
            Shader.PropertyToID("_HectonSonarNoirHideDistance");
        private static readonly int _ShaderHectonSonarRadarDistortion =
            Shader.PropertyToID("_HectonSonarRadarDistortion");
        private static readonly int _ShaderSonarActive =
            Shader.PropertyToID("_SonarActive");
        private static readonly int _ShaderActiveSonarCenterAup =
            Shader.PropertyToID("_ActiveSonarCenterAUP");
        private static readonly int _ShaderActiveSonarRadius =
            Shader.PropertyToID("_ActiveSonarRadius");
        private static readonly int _ShaderActiveSonarCentersRadius =
            Shader.PropertyToID("_ActiveSonarCentersRadius");
        private static readonly int _ShaderActiveSonarParams =
            Shader.PropertyToID("_ActiveSonarParams");
        private static readonly int _ShaderActiveSonarGeoParams =
            Shader.PropertyToID("_ActiveSonarGeoParams");
        private static readonly uint _ActiveSonarGeoSystemHash =
            unchecked((uint)LocHash.Compute("SpectrumSystem.ActiveSonarGeoIllumination"));
        private static readonly uint _ActiveSonarGeoRingCountHash =
            unchecked((uint)LocHash.Compute("ActiveSonarGeo.RingCount"));
        private static readonly uint _ActiveSonarGeoDumpFailureHash =
            unchecked((uint)LocHash.Compute("ActiveSonarGeo.DumpFailure"));
        private static readonly uint _ActiveSonarGeoNaNHash =
            unchecked((uint)LocHash.Compute("ActiveSonarGeo.NonFinite"));
        // COLD ALLOC: float[32] — passive hydrophone radar energy grid — owner: SpectrumSystem
        private readonly float[] _passiveRadarGrid = new float[PassiveRadarSectorCount];
        // COLD ALLOC: float[30] — passive hydrophone auto-gain history — owner: SpectrumSystem
        private readonly float[] _passiveRadarPeakHistory = new float[PassiveRadarAutoGainHistoryLength];
        // COLD ALLOC: Vector4[8] — passive hydrophone shader row payload — owner: SpectrumSystem
        private static readonly Vector4[] s_passiveRadarRows = new Vector4[PassiveRadarAzimuthSectorCount];
        // COLD ALLOC: SpatialAudioActiveEmitterSample[32] — active world emitter buffer for passive hydrophone scan — owner: SpectrumSystem
        private static readonly SpatialAudioActiveEmitterSample[] s_passiveRadarEmitterBuffer = new SpatialAudioActiveEmitterSample[32];
        // COLD ALLOC: AbsoluteUniversePosition[8] — nearest emitter AUP shortlist, one conversion during selection instead of projection — owner: SpectrumSystem
        private static readonly AbsoluteUniversePosition[] s_passiveRadarNearestAups = new AbsoluteUniversePosition[PassiveRadarSourceBudget];
        // COLD ALLOC: float[8] — nearest emitter amplitude shortlist for passive hydrophone scan — owner: SpectrumSystem
        private static readonly float[] s_passiveRadarNearestAmplitudes = new float[PassiveRadarSourceBudget];
        // COLD ALLOC: float[8] — nearest emitter distance cache for passive hydrophone scan — owner: SpectrumSystem
        private static readonly double[] s_passiveRadarNearestDistanceSqr = new double[PassiveRadarSourceBudget];
        // COLD ALLOC: Vector4[4] - active sonar geo shader centers/radii upload cache - owner: SpectrumSystem
        private readonly Vector4[] _activeSonarGeoCentersRadius = new Vector4[ActiveSonarGeoPingCapacity];
        // COLD ALLOC: Vector4[4] - active sonar geo shader intensity/start/max-range upload cache - owner: SpectrumSystem
        private readonly Vector4[] _activeSonarGeoParams = new Vector4[ActiveSonarGeoPingCapacity];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SpectrumMode CurrentMode => _currentMode;
        public bool IsThermalActive     => _currentMode == SpectrumMode.Thermal;
        public bool IsSonarActive       => _currentMode == SpectrumMode.Sonar;
        public bool IsEchoActive        => _currentMode == SpectrumMode.Echolocation;
        public bool HasSonarSnapshot    => _hasSonarSnapshot;
        public SpatialSonarSnapshot LastSonarSnapshot => _lastSonarSnapshot;

        public bool TryGetAupDiscoveryGrid(out NativeArray<uint>.ReadOnly discoveryGrid, out int width, out int height, out float cellSizeMeters)
        {
            bool resolved = TryReadAupDiscoveryGrid(out discoveryGrid);
            width = _aupDiscoveryGridWidthRuntime;
            height = _aupDiscoveryGridHeightRuntime;
            cellSizeMeters = _aupDiscoveryCellSizeRuntime;
            return resolved && width > 0 && height > 0;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            SpectrumSystem activeRuntime = s_activeRuntimeInstance ?? GlobalRegistry.Spectrum;
            if (activeRuntime != null && activeRuntime != this) { Destroy(gameObject); return; }
            SonarGridOverlay.ApplyGlobals(
                sonarGridIntensity,
                sonarGridLineScale,
                sonarGridLineWidth,
                sonarGridContourBoost,
                sonarGridHardColor,
                sonarGridOrganicColor,
                sonarGridAbyssalColor);
        }

        private void OnEnable()
        {
            TryRegisterService();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            SubscribeAcousticPingEvents();
            EnsureAupDiscoveryGrid();
            EnsureActiveSonarGeoTelemetryRing();

            TryRegisterLateFrameTick();

            ResolveSurvivalSystem();

            SonarGridOverlay.ApplyGlobals(
                sonarGridIntensity,
                sonarGridLineScale,
                sonarGridLineWidth,
                sonarGridContourBoost,
                sonarGridHardColor,
                sonarGridOrganicColor,
                sonarGridAbyssalColor);
            ApplyAcousticMappingStaticGlobals();
            ApplyShaderMode();
            PublishActiveSonarGeoGlobals(true);
        }

        private void OnDisable()
        {
            UnsubscribeAcousticPingEvents();
            TryUnregisterService();
            TryUnregisterHotSwapListener();

            TryUnregisterLateFrameTick();

            // Sbrasyvaem v Normal pri otklyuchenii
            Shader.SetGlobalInt(_ShaderSpectrumMode, 0);
            _lastPublishedSpectrumMode = 0;
            SonarGridOverlay.ClearGlobals();
            ClearSonarSnapshot();
            ClearAcousticMappingGlobals();
            ClearActiveSonarGeoGlobals();
            ClearCachedRegistryServices();
        }

        private void OnDestroy()
        {
            UnsubscribeAcousticPingEvents();
            TryUnregisterService();
            TryUnregisterHotSwapListener();

            TryUnregisterLateFrameTick();

            SonarGridOverlay.ClearGlobals();
            ClearAcousticMappingGlobals();
            ClearActiveSonarGeoGlobals();
            DisposeAupDiscoveryGrid();
            DisposeActiveSonarGeoTelemetryRing();
            ClearCachedRegistryServices();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            SpectrumSystem activeRuntime = s_activeRuntimeInstance ?? GlobalRegistry.Spectrum;
            if (activeRuntime != null && activeRuntime != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSpectrumRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Spectrum, this);
            if (_serviceRegistered)
                s_activeRuntimeInstance = this;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSpectrumRuntime(this);
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;
            _serviceRegistered = false;
        }

        private void TryRegisterLateFrameTick()
        {
            if (_registeredLateFrame || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_registeredLateFrame)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
            _registeredLateFrame = false;
        }

        public void OnGlobalRegistryServiceReplaced(
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
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            IDataVault oldVault = previousService as IDataVault ?? _dataVault;
            ReleaseVaultBuffer(oldVault, ref _aupDiscoveryGridHandle);
            ReleaseVaultBuffer(oldVault, ref _activeSonarGeoTelemetryRingHandle);
            _dataVault = currentService as IDataVault;
            EnsureAupDiscoveryGrid();
            EnsureActiveSonarGeoTelemetryRing();
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

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
            CacheAudioService(GlobalRegistry.Audio);
            CachePlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void CacheAudioService(IAudioService audioService)
        {
            _audioService = audioService != null && audioService.IsInitialized ? audioService : null;
            _spatialAudioEmitterReadModel = _audioService as ISpatialAudioWorldEmitterReadModel;
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext != null && playerRuntimeContext.IsInitialized ? playerRuntimeContext : null;
            if (_playerRuntimeContext == null)
            {
                _playerTransform = null;
                _playerMovement = null;
                return;
            }

            _playerTransform = _playerRuntimeContext.PlayerTransform;
            _playerMovement = _playerRuntimeContext.PlayerMovement;
        }

        private void ClearCachedRegistryServices()
        {
            _audioService = null;
            _spatialAudioEmitterReadModel = null;
            _playerRuntimeContext = null;
            _playerTransform = null;
            _playerMovement = null;
        }

        private static float ResolveUnityShaderTimeSeconds()
        {
            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUAL_SYNC tick
        // ══════════════════════════════════════════════════════════

        public void LateFrameTick()
        {
            DrainPhysicsEventPayloads();
            RunSpectrumVisualTick(math.max(0f, SystemDispatcher.CurrentFrameDeltaTime));
        }

        private void RunSpectrumVisualTick(float deltaTime)
        {
            if (IsEmpSensorBlindActive())
            {
                ClearSonarSnapshot();
                UpdateLidarPersistence(deltaTime);
                return;
            }

            float now = ResolveUnityShaderTimeSeconds();
            UpdateActiveSonarGeoIllumination(deltaTime, now);
            UpdateActiveSonarWavefront(deltaTime, now);
            UpdateLidarPersistence(deltaTime);
            UpdateAcousticMappingGlobals(deltaTime, now);

            if (_currentMode == SpectrumMode.Sonar)
                UpdatePassiveRadar(deltaTime);
            else if (_passiveRadarPeakSector >= 0)
                ClearPassiveRadarState();

            if (_currentMode != SpectrumMode.Sonar)
                return;

            _sonarTimer += deltaTime;
            if (_sonarTimer < sonarPulseInterval)
                return;

            _sonarTimer = 0f;

            EmitSonarPulse(sonarRadius, sonarRevealDuration, true, false);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Pereklyuchit rezhim vizora.</summary>
        public void SetMode(SpectrumMode mode)
        {
            if (mode == _currentMode) return;

            ResolveSurvivalSystem();

            // Drain energii
            if (survivalSystem != null && modeSwitchEnergyCost > 0f)
                survivalSystem.DrainEnergy(modeSwitchEnergyCost);

            _currentMode = mode;
            _sonarTimer = 0f;

            if (_currentMode != SpectrumMode.Sonar)
                ClearSonarSnapshot();

            ApplyShaderMode();
            SpectrumEvents.TryRaiseModeChanged(mode);

            // Glitch pulse na vizore
            VisorHUDController.PulseActiveControllers(0.2f, 4);

            NotificationEvents.TryPushInfo(ResolveLocalizedModeNotification(mode));
        }

        /// <summary>Tsiklicheskoe pereklyuchenie rezhimov.</summary>
        public void CycleMode()
        {
            int next = ((int)_currentMode + 1) % 4;
            SetMode((SpectrumMode)next);
        }

        /// <summary>
        /// Triggers an immediate one-shot active-sonar ping without requiring sonar visor mode to stay latched.
        /// </summary>
        /// <param name="radius">Pulse radius in world meters.</param>
        /// <param name="revealDurationSeconds">Reveal hold duration for shader/VFX consumers.</param>
        public bool TriggerActiveSonarPing(float radius, float revealDurationSeconds)
        {
            if (IsEmpSensorBlindActive())
                return false;

            float pulseRadius = math.max(1f, radius);
            float revealDurationValue = revealDurationSeconds > 0f ? revealDurationSeconds : sonarRevealDuration;
            return EmitSonarPulse(pulseRadius, revealDurationValue, true, true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShaderMode()
        {
            int mode = (int)_currentMode;
            if (_lastPublishedSpectrumMode != mode)
            {
                Shader.SetGlobalInt(_ShaderSpectrumMode, mode);
                _lastPublishedSpectrumMode = mode;
            }

            PublishSonarRadius(sonarRadius);
        }

        private bool EmitSonarPulse(float pulseRadius, float revealDurationSeconds, bool consumeEnergy, bool isActivePing)
        {
            if (IsEmpSensorBlindActive())
                return false;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return false;

            ResolveSurvivalSystem();
            if (consumeEnergy && survivalSystem != null && sonarPulseEnergyCost > 0f)
                survivalSystem.DrainEnergy(sonarPulseEnergyCost);

            if (!TryResolveRuntimePosition(in playerAup, out Vector3 playerPosition))
                return false;

            float pulseTime = ResolveUnityShaderTimeSeconds();
            float pulseIntensity = math.saturate(pulseRadius * 0.005f);
            float depth = ResolvePlayerMovement() != null ? math.max(0f, _playerMovement.CurrentDepth) : 0f;
            float abyssalDistortion = ResolveAbyssalDistortion(depth);
            float effectiveWaveSpeed = math.max(
                0.01f,
                sonarRevealWaveSpeed * math.lerp(1f, math.max(0.05f, abyssalWaveSpeedScaleMin), abyssalDistortion));
            float waveBandWidth = math.lerp(6f, 2f, pulseIntensity);
            float abyssalReturnScalar = 1f + math.saturate(abyssalContactJitterRadius * 0.08333334f) * 0.35f;
            float abyssalAnchorResponse01 = isActivePing ? math.saturate(pulseIntensity * abyssalDistortion * abyssalReturnScalar) : 0f;
            InitializeActiveSonarWavefront(pulseRadius, pulseTime, effectiveWaveSpeed, revealDurationSeconds, waveBandWidth);
            PublishScreenSpaceSonarPulse(playerPosition, pulseRadius, pulseTime, pulseIntensity, effectiveWaveSpeed, waveBandWidth, revealDurationSeconds);
            SpectrumEvents.TryRaiseSonarPulse(pulseRadius);
            if (isActivePing)
            {
                _activeLidarPersistence = math.max(_activeLidarPersistence, pulseIntensity);
                PublishLidarPersistence(_activeLidarPersistence);
                SpectrumEvents.TryRaiseSonarPingSent(pulseIntensity);
                AcousticPingSignal activeSonarSignal = default;
                activeSonarSignal.PositionAup = playerAup;
                activeSonarSignal.RadiusMeters = math.min(pulseRadius, ActiveSonarGeoMaxRangeMeters);
                activeSonarSignal.Intensity01 = pulseIntensity;
                activeSonarSignal.SourceId = _ActiveSonarGeoSystemHash;
                activeSonarSignal.Channel = AcousticPingSignal.ChannelActiveSonar;
                activeSonarSignal.Flags = AcousticPingSignal.FlagActiveSonar;
                SignalBus<AcousticPingSignal>.TryPushTracked(in activeSonarSignal, ref s_x001SpectrumSystemSignalPushDropCount);
                SubmitActiveSonarGeoPing(in activeSonarSignal, pulseTime, 0f);
                if (SignalBus<AcousticPingSignal>.TryGetLatest(out _, out int activeSonarSequence))
                    _lastConsumedActiveSonarAcousticSequence = activeSonarSequence;
                PublishActiveSonarPhysicsPing(playerPosition, pulseRadius, pulseIntensity, revealDurationSeconds);
                Vector3 playerForward = ResolvePlayerForward();
                PublishActiveSonarDangerImpulse(playerPosition, playerForward, pulseRadius, pulseIntensity);
                TryPlayAbyssalAnchorReturn(abyssalAnchorResponse01);
            }

            Shader.SetGlobalFloat(_ShaderSonarPulseTime, pulseTime);
            PublishSonarRadius(0f);
            PublishSonarReveal(playerPosition, in playerAup, pulseRadius, revealDurationSeconds, pulseTime, pulseIntensity, abyssalDistortion, effectiveWaveSpeed);
            if (!isActivePing)
            {
                WorldSpatialHashGrid.BuildSonarSnapshot(playerPosition, in playerAup, pulseRadius, out _lastSonarSnapshot);
                _hasSonarSnapshot = true;
                NoiseSystem.ReportPlayerSignal(
                    playerPosition,
                    in playerAup,
                    0f,
                    false,
                    0f,
                    0f,
                    math.saturate(sonarNoiseSignature01));
                SpectrumEvents.TryRaiseSonarSnapshotUpdated(_lastSonarSnapshot);
            }

            return true;
        }

        private void SubscribeAcousticPingEvents()
        {
            if (_acousticSignalSubscribed || !Application.isPlaying)
                return;

            SpectrumEvents.RegisterAcousticEchoListener(this);
            SpectrumEvents.RegisterPingReturnSignalListener(this);
            _acousticSignalSubscribed = true;
        }

        private void UnsubscribeAcousticPingEvents()
        {
            if (!_acousticSignalSubscribed)
                return;

            SpectrumEvents.UnregisterAcousticEchoListener(this);
            SpectrumEvents.UnregisterPingReturnSignalListener(this);
            _acousticSignalSubscribed = false;
            _lastPhysicsEventSnapshotGeneration = 0;
        }

        public void OnAcousticEchoReturned(in AcousticEchoEvent echoEvent)
        {
            HandleAcousticEchoReturned(in echoEvent);
        }

        public void OnPingReturnSignal(in PingReturnSignal signal)
        {
            HandlePingReturnSignal(in signal);
        }

        private void DrainPhysicsEventPayloads()
        {
            if (!_acousticSignalSubscribed)
                return;

            int snapshotGeneration = SignalBus<PhysicsEventPayload>.SnapshotGeneration;
            if (snapshotGeneration == _lastPhysicsEventSnapshotGeneration)
                return;

            _lastPhysicsEventSnapshotGeneration = snapshotGeneration;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                ushort eventType = payload.EventType;
                if (eventType == PhysicsEventTypeAcousticPing)
                {
                    HandleAcousticPingPayload(in payload);
                }
                else if (eventType == PhysicsEventTypeAcousticImpulse)
                {
                    HandleAcousticImpulsePayload(in payload);
                }
            }
        }

        private void HandleAcousticPingPayload(in PhysicsEventPayload pingEvent)
        {
            if (pingEvent.RadiusMeters <= 0f || pingEvent.Scalar0 <= 0f || pingEvent.Scalar1 <= 0f)
                return;

            WorldSpatialHashGrid.RegisterTransientEvent(
                pingEvent.RuntimePosition,
                pingEvent.RadiusMeters,
                pingEvent.Scalar0,
                pingEvent.Scalar1,
                SpatialTransientEventType.AcousticImpulse,
                SpatialInteractionFlags.Signal | SpatialInteractionFlags.AcousticReceiver,
                unchecked((FieldTargetRole)pingEvent.StatusBits),
                pingEvent.PrimaryId);
        }

        private void HandleAcousticEchoReturned(in AcousticEchoEvent echoEvent)
        {
            if (echoEvent.ReturnStrength <= 0.001f)
                return;

            float now = ResolveUnityShaderTimeSeconds();
            float speed = math.max(0.01f, sonarScreenSpacePulseSpeedMetersPerSecond * math.max(0.05f, sonarEchoVisualSpeedScale));
            float inverseRevealWaveSpeed = math.rcp(math.max(0.01f, sonarRevealWaveSpeed));
            float delaySeconds = echoEvent.DistanceMeters > 0f
                ? echoEvent.DistanceMeters * inverseRevealWaveSpeed
                : 0f;
            float echoStartTime = now + delaySeconds;
            float echoRadius = math.clamp(echoEvent.DistanceMeters * 0.42f, 10f, math.max(10f, sonarRadius * 0.65f));
            float echoWidth = math.max(1.5f, _activeSonarWaveBandWidth * 1.65f);
            float echoIntensity = math.saturate(echoEvent.ReturnStrength * sonarEchoVisualIntensityScale);

            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoPulse,
                MakeVector4(echoEvent.WorldPosition.x, echoEvent.WorldPosition.y, echoEvent.WorldPosition.z, echoStartTime));
            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoParams,
                MakeVector4(speed, echoRadius, echoWidth, echoIntensity));
            PublishSonarActive(true);

            _activeSonarEchoExpireTime = math.max(
                _activeSonarEchoExpireTime,
                echoStartTime + (echoRadius * math.rcp(speed)) + sonarRevealFadeDuration);

            AbsoluteUniversePosition echoAup = echoEvent.ResolveWorldAup();
            MarkAupDiscoveryCell(in echoAup, echoEvent.ReturnStrength);
        }

        private void HandlePingReturnSignal(in PingReturnSignal signal)
        {
            if (signal.ReturnStrength <= 0.001f)
                return;

            float now = ResolveUnityShaderTimeSeconds();
            float speed = math.max(0.01f, sonarScreenSpacePulseSpeedMetersPerSecond * math.max(0.05f, sonarEchoVisualSpeedScale));
            float echoStartTime = now + math.max(0f, signal.EchoDelaySeconds);
            float echoRadius = math.clamp(signal.DistanceMeters * 0.42f, 10f, math.max(10f, sonarRadius * 0.65f));
            float echoWidth = math.max(1.5f, _activeSonarWaveBandWidth * 1.65f);
            float echoIntensity = math.saturate(signal.ReturnStrength * sonarEchoVisualIntensityScale);

            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoPulse,
                MakeVector4(signal.WorldPosition.x, signal.WorldPosition.y, signal.WorldPosition.z, echoStartTime));
            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoParams,
                MakeVector4(speed, echoRadius, echoWidth, echoIntensity));
            PublishSonarActive(true);

            _activeSonarEchoExpireTime = math.max(
                _activeSonarEchoExpireTime,
                echoStartTime + (echoRadius * math.rcp(speed)) + sonarRevealFadeDuration);

            AbsoluteUniversePosition signalAup = signal.ResolveWorldAup();
            AcousticPingSignal echoGeoSignal = default;
            echoGeoSignal.PositionAup = signalAup;
            echoGeoSignal.RadiusMeters = math.min(math.max(1f, signal.DistanceMeters), ActiveSonarGeoMaxRangeMeters);
            echoGeoSignal.Intensity01 = echoIntensity;
            echoGeoSignal.SourceId = _ActiveSonarGeoSystemHash;
            echoGeoSignal.Channel = AcousticPingSignal.ChannelActiveSonar;
            echoGeoSignal.Flags = AcousticPingSignal.FlagActiveSonar;
            SubmitActiveSonarGeoPing(in echoGeoSignal, now, signal.EchoDelaySeconds);
            MarkAupDiscoveryCell(in signalAup, signal.ReturnStrength);
        }

        private void HandleAcousticImpulsePayload(in PhysicsEventPayload impulseEvent)
        {
            if ((impulseEvent.StatusBits & AcousticImpulseFlagLeviathan) == 0u)
                return;

            float scream01 = math.saturate(impulseEvent.Scalar1 + (impulseEvent.Scalar0 * 0.00008f));
            _leviathanScreamRadarDistortion01 = math.max(_leviathanScreamRadarDistortion01, scream01);
        }

        private void PublishScreenSpaceSonarPulse(
            Vector3 origin,
            float radius,
            float pulseTime,
            float pulseIntensity,
            float effectiveWaveSpeed,
            float waveBandWidth,
            float revealDurationSeconds)
        {
            ApplyAcousticMappingStaticGlobals();
            float visualWaveSpeed = math.max(0.01f, sonarScreenSpacePulseSpeedMetersPerSecond);
            Shader.SetGlobalVector(_ShaderHectonSonarPrimaryPulse, MakeVector4(origin.x, origin.y, origin.z, pulseTime));
            Shader.SetGlobalVector(
                _ShaderHectonSonarVisualParams,
                MakeVector4(
                    visualWaveSpeed,
                    math.max(1f, radius),
                    math.max(0.25f, waveBandWidth),
                    math.saturate(pulseIntensity)));
            PublishSonarActive(true);
            _activeSonarVisualExpireTime = pulseTime
                + math.max(0.05f, revealDurationSeconds)
                + (math.max(1f, radius) * math.rcp(visualWaveSpeed));
        }

        private static void PublishActiveSonarPhysicsPing(Vector3 origin, float radius, float intensity, float lifetimeSeconds)
        {
            PhysicsEventPayload payload = default;
            payload.RuntimePosition = origin;
            payload.RadiusMeters = radius;
            payload.Scalar0 = intensity;
            payload.Scalar1 = lifetimeSeconds;
            payload.Scalar2 = radius * radius * math.max(0.1f, intensity);
            payload.StatusBits = unchecked((uint)FieldTargetRole.Generic);
            payload.EventType = PhysicsEventTypeAcousticPing;
            SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001SpectrumSystemSignalPushDropCount);
        }

        private void PublishActiveSonarDangerImpulse(Vector3 origin, Vector3 forward, float radius, float intensity)
        {
            float safeRadius = math.max(1f, radius);
            float safeIntensity = math.max(0.1f, math.saturate(intensity));
            float energyJoules = safeRadius * safeRadius * safeIntensity * math.max(0.1f, sonarAggroImpulseEnergyScale);
            PhysicsEventPayload payload = default;
            payload.RuntimePosition = origin;
            payload.Direction = forward;
            payload.RadiusMeters = safeRadius;
            payload.Scalar0 = energyJoules;
            payload.Scalar1 = safeIntensity;
            payload.Scalar2 = 1f;
            payload.StatusBits = AcousticImpulseFlagLarge;
            payload.EventType = PhysicsEventTypeAcousticImpulse;
            SignalBus<PhysicsEventPayload>.TryPushTracked(in payload, ref s_x001SpectrumSystemSignalPushDropCount);
        }

        private void ApplyAcousticMappingStaticGlobals()
        {
            Color sonarColor = sonarGridHardColor.linear;
            Vector4 sonarColorPayload = MakeVector4(sonarColor.r, sonarColor.g, sonarColor.b, math.max(0f, sonarGridContourBoost));
            float noirHideDistance = math.max(0f, sonarNoirHideDistanceMeters);
            if (!_hasPublishedAcousticMappingStaticGlobals ||
                !NearlyEqual(_lastPublishedSonarColor, sonarColorPayload, ShaderScalarPublishEpsilon))
            {
                Shader.SetGlobalVector(_ShaderHectonSonarColor, sonarColorPayload);
                _lastPublishedSonarColor = sonarColorPayload;
            }

            if (!_hasPublishedAcousticMappingStaticGlobals ||
                !NearlyEqual(_lastPublishedNoirHideDistance, noirHideDistance, ShaderScalarPublishEpsilon))
            {
                Shader.SetGlobalFloat(_ShaderHectonSonarNoirHideDistance, noirHideDistance);
                _lastPublishedNoirHideDistance = noirHideDistance;
            }

            _hasPublishedAcousticMappingStaticGlobals = true;
        }

        private void UpdateAcousticMappingGlobals(float deltaTime, float now)
        {
            bool sonarActive = now <= _activeSonarVisualExpireTime || now <= _activeSonarEchoExpireTime;
            PublishSonarActive(sonarActive);

            float speedStart = math.max(0f, radarDistortionStartSpeedMetersPerSecond);
            float speedFull = math.max(speedStart + 0.01f, radarDistortionFullSpeedMetersPerSecond);
            float speedStartSqr = speedStart * speedStart;
            float speedFullSqr = speedFull * speedFull;
            float speed01 = math.saturate(
                (ResolvePlayerSpeedMagnitudeSqr() - speedStartSqr) * math.rcp(math.max(0.0001f, speedFullSqr - speedStartSqr)));
            _leviathanScreamRadarDistortion01 = math.max(
                0f,
                _leviathanScreamRadarDistortion01 - (math.max(0f, deltaTime) * math.max(0.1f, leviathanScreamRadarDecayPerSecond)));
            float radarDistortion01 = math.max(speed01, _leviathanScreamRadarDistortion01);
            PublishRadarDistortion(MakeVector4(speed01, _leviathanScreamRadarDistortion01, radarDistortion01, sonarActive ? 1f : 0f));
        }

        private float ResolvePlayerSpeedMagnitudeSqr()
        {
            if (_playerMovement != null)
                return _playerMovement.InterpolatedLinearVelocity.sqrMagnitude;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                return math.lengthsq(movementState.Velocity);
            }

            HectonPlayerMovement movement = ResolvePlayerMovement();
            return movement != null ? movement.InterpolatedLinearVelocity.sqrMagnitude : 0f;
        }

        private void ClearAcousticMappingGlobals()
        {
            Shader.SetGlobalVector(_ShaderHectonSonarPrimaryPulse, Vector4.zero);
            Shader.SetGlobalVector(_ShaderHectonSonarEchoPulse, Vector4.zero);
            Shader.SetGlobalVector(_ShaderHectonSonarVisualParams, Vector4.zero);
            Shader.SetGlobalVector(_ShaderHectonSonarEchoParams, Vector4.zero);
            Shader.SetGlobalVector(_ShaderHectonSonarColor, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderHectonSonarNoirHideDistance, 0f);
            _lastPublishedSonarColor = Vector4.zero;
            _lastPublishedNoirHideDistance = 0f;
            _hasPublishedAcousticMappingStaticGlobals = true;
            PublishRadarDistortion(Vector4.zero);
            PublishSonarActive(false);
        }

        private void PublishSonarActive(bool active)
        {
            int state = active ? 1 : 0;
            if (_lastPublishedSonarActiveState == state)
                return;

            Shader.SetGlobalFloat(_ShaderSonarActive, state);
            _lastPublishedSonarActiveState = state;
        }

        private void PublishSonarRadius(float radius)
        {
            float value = math.max(0f, radius);
            if (NearlyEqual(_lastPublishedSonarRadius, value, ShaderScalarPublishEpsilon))
                return;

            Shader.SetGlobalFloat(_ShaderSonarRadius, value);
            _lastPublishedSonarRadius = value;
        }

        private void PublishSonarWaveFront(float waveFront)
        {
            float value = math.max(0f, waveFront);
            if (NearlyEqual(_lastPublishedSonarWaveFront, value, ShaderScalarPublishEpsilon))
                return;

            Shader.SetGlobalFloat(_ShaderSonarWaveFront, value);
            _lastPublishedSonarWaveFront = value;
        }

        private void PublishLidarPersistence(float persistence)
        {
            float value = math.max(0f, persistence);
            if (NearlyEqual(_lastPublishedLidarPersistence, value, ShaderScalarPublishEpsilon))
                return;

            Shader.SetGlobalFloat(_ShaderLidarPersistence, value);
            _lastPublishedLidarPersistence = value;
        }

        private void PublishRadarDistortion(Vector4 distortion)
        {
            if (_hasPublishedRadarDistortion &&
                NearlyEqual(_lastPublishedRadarDistortion, distortion, ShaderScalarPublishEpsilon))
            {
                return;
            }

            Shader.SetGlobalVector(_ShaderHectonSonarRadarDistortion, distortion);
            _lastPublishedRadarDistortion = distortion;
            _hasPublishedRadarDistortion = true;
        }

        private void EnsureAupDiscoveryGrid()
        {
            if (!Application.isPlaying)
                return;

            if (TryReadAupDiscoveryGrid(out _))
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            _aupDiscoveryGridWidthRuntime = math.max(8, aupDiscoveryGridWidth);
            _aupDiscoveryGridHeightRuntime = math.max(8, aupDiscoveryGridHeight);
            _aupDiscoveryCellSizeRuntime = math.max(1f, aupDiscoveryCellSizeMeters);
            int cellCount = _aupDiscoveryGridWidthRuntime * _aupDiscoveryGridHeightRuntime;
            _aupDiscoveryGridHandle = vault.EnsureGenerationHandle<uint>(
                AupDiscoveryGridBufferId,
                cellCount,
                SpectrumVaultOwner,
                NativeArrayOptions.ClearMemory);
        }

        private void DisposeAupDiscoveryGrid()
        {
            ReleaseVaultBuffer(_dataVault, ref _aupDiscoveryGridHandle);
            _aupDiscoveryGridWidthRuntime = 0;
            _aupDiscoveryGridHeightRuntime = 0;
            _aupDiscoveryCellSizeRuntime = 0f;
        }

        private void MarkAupDiscoveryCell(in AbsoluteUniversePosition aup, float strength01)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _aupDiscoveryGridHandle.BufferID == 0u ||
                _aupDiscoveryGridWidthRuntime <= 0 ||
                _aupDiscoveryGridHeightRuntime <= 0 ||
                !vault.TryAcquireWriteLock(in _aupDiscoveryGridHandle, SpectrumVaultOwner, out NativeArray<uint> discoveryGrid))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !discoveryGrid.IsCreated)
                {
                    return;
                }

                double3 absolute = aup.ToAbsoluteDouble3();
                double invCellSize = 1.0 / math.max(1.0, (double)_aupDiscoveryCellSizeRuntime);
                long cellX = (long)math.floor(absolute.x * invCellSize);
                long cellZ = (long)math.floor(absolute.z * invCellSize);
                MarkAupDiscoveryCellByCoord(discoveryGrid, cellX, cellZ, strength01);
            }
            finally
            {
                vault.ReleaseWriteLock(in _aupDiscoveryGridHandle, SpectrumVaultOwner);
            }
        }

        private void MarkAupDiscoveryCellByCoord(NativeArray<uint> discoveryGrid, long cellX, long cellZ, float strength01)
        {
            if (!discoveryGrid.IsCreated || _aupDiscoveryGridWidthRuntime <= 0 || _aupDiscoveryGridHeightRuntime <= 0)
                return;

            int x = PositiveModulo(cellX, _aupDiscoveryGridWidthRuntime);
            int z = PositiveModulo(cellZ, _aupDiscoveryGridHeightRuntime);
            int index = (z * _aupDiscoveryGridWidthRuntime) + x;
            if ((uint)index >= (uint)discoveryGrid.Length)
                return;

            int strengthLevel = (int)math.clamp(math.floor(math.saturate(strength01) * 7.999f), 0f, 7f);
            uint strengthBit = 1u << (1 + strengthLevel);
            discoveryGrid[index] = discoveryGrid[index] | AupDiscoveryDiscoveredBit | strengthBit;
        }

        private void MarkAupDiscoveryPulseShell(in AbsoluteUniversePosition originAup, float radius, float strength01)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _aupDiscoveryGridHandle.BufferID == 0u ||
                _aupDiscoveryGridWidthRuntime <= 0 ||
                _aupDiscoveryGridHeightRuntime <= 0 ||
                !vault.TryAcquireWriteLock(in _aupDiscoveryGridHandle, SpectrumVaultOwner, out NativeArray<uint> discoveryGrid))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !discoveryGrid.IsCreated)
                {
                    return;
                }

                double3 absolute = originAup.ToAbsoluteDouble3();
                double invCellSize = 1.0 / math.max(1.0, (double)_aupDiscoveryCellSizeRuntime);
                long originCellX = (long)math.floor(absolute.x * invCellSize);
                long originCellZ = (long)math.floor(absolute.z * invCellSize);

                // Discovery is persistent sonar memory, not physics: stamp an octant shell directly in grid space.
                float shellDistance = math.max(_aupDiscoveryCellSizeRuntime, radius);
                long shellCellDelta = (long)math.ceil(shellDistance * invCellSize);
                if (shellCellDelta < 1L)
                    shellCellDelta = 1L;

                long diagonalCellDelta = (long)math.ceil((double)shellCellDelta * 0.7071067811865476d);
                if (diagonalCellDelta < 1L)
                    diagonalCellDelta = 1L;

                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX, originCellZ, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX + shellCellDelta, originCellZ, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX - shellCellDelta, originCellZ, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX, originCellZ + shellCellDelta, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX, originCellZ - shellCellDelta, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX + diagonalCellDelta, originCellZ + diagonalCellDelta, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX - diagonalCellDelta, originCellZ + diagonalCellDelta, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX + diagonalCellDelta, originCellZ - diagonalCellDelta, strength01);
                MarkAupDiscoveryCellByCoord(discoveryGrid, originCellX - diagonalCellDelta, originCellZ - diagonalCellDelta, strength01);
            }
            finally
            {
                vault.ReleaseWriteLock(in _aupDiscoveryGridHandle, SpectrumVaultOwner);
            }
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault != null)
                return _dataVault;

            _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private bool TryReadAupDiscoveryGrid(out NativeArray<uint>.ReadOnly discoveryGrid)
        {
            discoveryGrid = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _aupDiscoveryGridHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _aupDiscoveryGridHandle, out discoveryGrid) &&
                   !vault.IsCompactionFenceActive &&
                   discoveryGrid.IsCreated;
        }

        private bool TryReadActiveSonarGeoTelemetryRing(out NativeArray<ActiveSonarGeoTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   _activeSonarGeoTelemetryRingHandle.BufferID != 0u &&
                   vault.TryReadOnlyHandle(in _activeSonarGeoTelemetryRingHandle, out telemetryRing) &&
                   !vault.IsCompactionFenceActive &&
                   telemetryRing.IsCreated;
        }

        private static void ReleaseVaultBuffer<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : unmanaged
        {
            if (vault != null && handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static int PositiveModulo(long value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            long result = value % modulus;
            return (int)(result < 0 ? result + modulus : result);
        }

        private static bool IsFinite(Vector4 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z) &&
                   IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct ActiveSonarGeoTelemetryEntry
        {
            [FieldOffset(0)]
            public uint Frame;
            [FieldOffset(4)]
            public int ActiveRingCount;
            [FieldOffset(8)]
            public float PrimaryRadius;
            [FieldOffset(12)]
            public float3 PrimaryCenter;
            [FieldOffset(24)]
            public uint Flags;
            [FieldOffset(28)]
            private byte _pad0;
            [FieldOffset(29)]
            private byte _pad1;
            [FieldOffset(30)]
            private byte _pad2;
            [FieldOffset(31)]
            private byte _pad3;
        }

        private static bool NearlyEqual(float a, float b, float epsilon)
        {
            return math.abs(a - b) <= epsilon;
        }

        private static bool NearlyEqual(Vector4 a, Vector4 b, float epsilon)
        {
            return math.abs(a.x - b.x) <= epsilon &&
                   math.abs(a.y - b.y) <= epsilon &&
                   math.abs(a.z - b.z) <= epsilon &&
                   math.abs(a.w - b.w) <= epsilon;
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.SurvivalSystem != null)
            {
                survivalSystem = playerRuntimeContext.SurvivalSystem;
                return true;
            }

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.PlayerTransform != null)
            {
                _playerTransform = playerRuntimeContext.PlayerTransform;
                return true;
            }

            return GameBootstrapper.TryGetCurrentPlayerTransform(out _playerTransform) && _playerTransform != null;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            HectonPlayerMovement movement = ResolvePlayerMovement();
            if (movement != null)
            {
                playerAup = movement.CurrentAup;
                return true;
            }

            playerAup = default;
            return false;
        }

        private static Vector3 ToVector3(float3 value)
        {
            Vector3 result = default;
            result.x = value.x;
            result.y = value.y;
            result.z = value.z;
            return result;
        }

        private static Vector4 MakeVector4(float x, float y, float z, float w)
        {
            Vector4 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            result.w = w;
            return result;
        }

        private static float3 MakeFloat3(float x, float y, float z)
        {
            float3 result = default;
            result.x = x;
            result.y = y;
            result.z = z;
            return result;
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out Vector3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!positionAup.IsFinite() || !originAup.IsFinite())
                return false;

            double3 localDelta = AbsoluteUniversePosition.DeltaMetersClamped(in positionAup, in originAup);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            runtimePosition.x = (float)localDelta.x;
            runtimePosition.y = (float)localDelta.y;
            runtimePosition.z = (float)localDelta.z;
            return math.isfinite(runtimePosition.x) &&
                   math.isfinite(runtimePosition.y) &&
                   math.isfinite(runtimePosition.z);
        }

        private static bool TryResolveRuntimePosition(in AbsoluteUniversePosition positionAup, out float3 runtimePosition)
        {
            runtimePosition = default;
            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!positionAup.IsFinite() || !originAup.IsFinite())
                return false;

            double3 localDelta = AbsoluteUniversePosition.DeltaMetersClamped(in positionAup, in originAup);
            if (!math.all(math.isfinite(localDelta)))
                return false;

            runtimePosition.x = (float)localDelta.x;
            runtimePosition.y = (float)localDelta.y;
            runtimePosition.z = (float)localDelta.z;
            return math.all(math.isfinite(runtimePosition));
        }

        private Vector3 ResolvePlayerForward()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot))
            {
                Vector3 poseForward = ToVector3(poseSnapshot.Forward);
                if (poseForward.sqrMagnitude > 0.0001f)
                {
                    _lastResolvedPlayerForward = ResolveDominantAxisDirection(poseForward);
                    return _lastResolvedPlayerForward;
                }
            }

            if (playerRuntimeContext != null && playerRuntimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
            {
                Vector3 cameraForward = ToVector3(movementState.CameraForward);
                if (cameraForward.sqrMagnitude > 0.0001f)
                {
                    _lastResolvedPlayerForward = ResolveDominantAxisDirection(cameraForward);
                    return _lastResolvedPlayerForward;
                }

                Vector3 movementForward = ToVector3(movementState.Forward);
                if (movementForward.sqrMagnitude > 0.0001f)
                {
                    _lastResolvedPlayerForward = ResolveDominantAxisDirection(movementForward);
                    return _lastResolvedPlayerForward;
                }
            }

            return _lastResolvedPlayerForward.sqrMagnitude > 0.0001f
                ? _lastResolvedPlayerForward
                : Vector3.forward;
        }

        private static Vector3 ResolveDominantAxisDirection(Vector3 direction)
        {
            float absX = math.abs(direction.x);
            float absY = math.abs(direction.y);
            float absZ = math.abs(direction.z);
            if (absX >= absY && absX >= absZ)
                return direction.x < 0f ? Vector3.left : Vector3.right;

            if (absY >= absZ)
                return direction.y < 0f ? Vector3.down : Vector3.up;

            return direction.z < 0f ? Vector3.back : Vector3.forward;
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement != null)
                return _playerMovement;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.PlayerMovement != null)
            {
                _playerMovement = playerRuntimeContext.PlayerMovement;
                return _playerMovement;
            }

            if (ResolvePlayerTransform())
                _playerTransform.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private void ClearSonarSnapshot()
        {
            _hasSonarSnapshot = false;
            _lastSonarSnapshot = default;
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, 0f);
            Shader.SetGlobalVector(_ShaderSonarRevealWaveParams, Vector4.zero);
            PublishSonarWaveFront(0f);
            PublishSonarRadius(0f);
            Shader.SetGlobalVector(_ShaderSonarPingCenter, Vector4.zero);
            Shader.SetGlobalVector(_ShaderSonarPingParams, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, 0f);
            PublishLidarPersistence(0f);
            _activeSonarWaveFront = 0f;
            _activeSonarWaveSpeed = 0f;
            _activeSonarRevealExpireTime = 0f;
            _activeSonarWaveBandWidth = 0f;
            _activeSonarWavefrontActive = false;
            _activeLidarPersistence = 0f;
            _activeSonarVisualExpireTime = 0f;
            _activeSonarEchoExpireTime = 0f;
            ClearAcousticMappingGlobals();
            ClearPassiveRadarState();
            SpectrumEvents.TryRaiseSonarSnapshotUpdated(default);
        }

        private bool IsEmpSensorBlindActive()
        {
            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext == null || playerRuntimeContext.TraumaDispatcher == null)
            {
                return false;
            }

            return playerRuntimeContext.TraumaDispatcher.IsEmpSensorBlindActive;
        }

        private void UpdateActiveSonarGeoIllumination(float deltaTime, float now)
        {
            ApplyActiveSonarGeoAupShifts();
            ConsumeLatestActiveSonarAcousticPing(now);
            StepActiveSonarGeoPings(deltaTime, now);
            PublishActiveSonarGeoGlobals(false);
            WriteActiveSonarGeoTelemetry();
        }

        private void ConsumeLatestActiveSonarAcousticPing(float now)
        {
            if (!SignalBus<AcousticPingSignal>.TryGetLatest(out AcousticPingSignal signal, out int sequence) ||
                sequence == _lastConsumedActiveSonarAcousticSequence)
            {
                return;
            }

            _lastConsumedActiveSonarAcousticSequence = sequence;
            if (signal.Channel != AcousticPingSignal.ChannelActiveSonar ||
                (signal.Flags & AcousticPingSignal.FlagActiveSonar) == 0)
            {
                return;
            }

            SubmitActiveSonarGeoPing(in signal, now, 0f);
        }

        private void SubmitActiveSonarGeoPing(in AcousticPingSignal signal, float now, float audioDelaySeconds)
        {
            float intensity = math.saturate(signal.Intensity01);
            float maxRange = math.clamp(signal.RadiusMeters, 1f, ActiveSonarGeoMaxRangeMeters);
            if (intensity <= 0.0001f || maxRange <= 0.0001f)
                return;

            if (!TryResolveRuntimePosition(in signal.PositionAup, out float3 center))
            {
                HandleActiveSonarGeoNonFinite();
                return;
            }

            int insertIndex = ResolveActiveSonarGeoInsertIndex();
            _activeSonarGeoCentersRadius[insertIndex] = MakeVector4(center.x, center.y, center.z, 0f);
            _activeSonarGeoParams[insertIndex] = MakeVector4(
                intensity,
                now + math.max(0f, audioDelaySeconds),
                maxRange,
                signal.Flags);

            if (_activeSonarGeoPingCount < ActiveSonarGeoPingCapacity)
                _activeSonarGeoPingCount++;

            _activeSonarGeoGlobalsDirty = true;
        }

        private int ResolveActiveSonarGeoInsertIndex()
        {
            if (_activeSonarGeoPingCount < ActiveSonarGeoPingCapacity)
                return _activeSonarGeoPingCount;

            int oldestIndex = 0;
            float oldestStartTime = _activeSonarGeoParams[0].y;
            for (int i = 1; i < ActiveSonarGeoPingCapacity; i++)
            {
                float startTime = _activeSonarGeoParams[i].y;
                if (startTime < oldestStartTime)
                {
                    oldestStartTime = startTime;
                    oldestIndex = i;
                }
            }

            return oldestIndex;
        }

        private void StepActiveSonarGeoPings(float deltaTime, float now)
        {
            if (_activeSonarGeoPingCount <= 0)
                return;

            float dt = math.max(0f, deltaTime);
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _activeSonarGeoPingCount; readIndex++)
            {
                Vector4 centerRadius = _activeSonarGeoCentersRadius[readIndex];
                Vector4 parameters = _activeSonarGeoParams[readIndex];
                float maxRange = math.clamp(parameters.z, 1f, ActiveSonarGeoMaxRangeMeters);
                float radius = centerRadius.w;
                if (now >= parameters.y)
                    radius += dt * ActiveSonarGeoSpeedMetersPerSecond;

                centerRadius.w = radius;
                if (!IsFinite(centerRadius) || !IsFinite(parameters))
                {
                    HandleActiveSonarGeoNonFinite();
                    return;
                }

                if (radius >= maxRange)
                {
                    _activeSonarGeoGlobalsDirty = true;
                    continue;
                }

                if (writeIndex != readIndex)
                {
                    _activeSonarGeoCentersRadius[writeIndex] = centerRadius;
                    _activeSonarGeoParams[writeIndex] = parameters;
                    _activeSonarGeoGlobalsDirty = true;
                }
                else
                {
                    if (!NearlyEqual(_activeSonarGeoCentersRadius[writeIndex], centerRadius, ShaderVectorPublishEpsilon))
                        _activeSonarGeoGlobalsDirty = true;

                    _activeSonarGeoCentersRadius[writeIndex] = centerRadius;
                    _activeSonarGeoParams[writeIndex] = parameters;
                }

                writeIndex++;
            }

            for (int i = writeIndex; i < _activeSonarGeoPingCount; i++)
            {
                _activeSonarGeoCentersRadius[i] = Vector4.zero;
                _activeSonarGeoParams[i] = Vector4.zero;
            }

            if (writeIndex != _activeSonarGeoPingCount)
            {
                _activeSonarGeoPingCount = writeIndex;
                _activeSonarGeoGlobalsDirty = true;
            }
        }

        private void ApplyActiveSonarGeoAupShifts()
        {
            if (_activeSonarGeoPingCount <= 0)
                return;

            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            if (shifts.Length == 0)
                return;

            for (int shiftIndex = 0; shiftIndex < shifts.Length; shiftIndex++)
            {
                float3 shiftMeters = shifts[shiftIndex].ShiftMeters;
                if (!math.all(math.isfinite(shiftMeters)))
                {
                    HandleActiveSonarGeoNonFinite();
                    return;
                }

                for (int pingIndex = 0; pingIndex < _activeSonarGeoPingCount; pingIndex++)
                {
                    Vector4 centerRadius = _activeSonarGeoCentersRadius[pingIndex];
                    centerRadius.x -= shiftMeters.x;
                    centerRadius.y -= shiftMeters.y;
                    centerRadius.z -= shiftMeters.z;
                    _activeSonarGeoCentersRadius[pingIndex] = centerRadius;
                }
            }

            _activeSonarGeoGlobalsDirty = true;
        }

        private static float ResolveActiveSonarGeoQualityWeight()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.isfinite(quality) ? math.saturate(quality) : 0.5f;
        }

        private void PublishActiveSonarGeoGlobals(bool force)
        {
            float quality = ResolveActiveSonarGeoQualityWeight();
            float primaryRadius = _activeSonarGeoPingCount > 0 ? math.max(0f, _activeSonarGeoCentersRadius[0].w) : 0f;
            Vector4 primaryCenter = _activeSonarGeoPingCount > 0
                ? MakeVector4(
                    _activeSonarGeoCentersRadius[0].x,
                    _activeSonarGeoCentersRadius[0].y,
                    _activeSonarGeoCentersRadius[0].z,
                    primaryRadius)
                : Vector4.zero;
            Vector4 state = MakeVector4(
                _activeSonarGeoPingCount,
                ActiveSonarGeoMaxRangeMeters,
                math.lerp(0f, 2f, quality),
                ActiveSonarGeoSpeedMetersPerSecond);

            if (force || _activeSonarGeoGlobalsDirty)
            {
                Shader.SetGlobalVector(_ShaderActiveSonarCenterAup, primaryCenter);
                Shader.SetGlobalVectorArray(_ShaderActiveSonarCentersRadius, _activeSonarGeoCentersRadius);
                Shader.SetGlobalVectorArray(_ShaderActiveSonarParams, _activeSonarGeoParams);
            }

            if (force ||
                _activeSonarGeoGlobalsDirty ||
                _lastPublishedActiveSonarGeoCount != _activeSonarGeoPingCount ||
                !NearlyEqual(_lastPublishedActiveSonarGeoRadius, primaryRadius, ShaderScalarPublishEpsilon))
            {
                Shader.SetGlobalFloat(_ShaderActiveSonarRadius, primaryRadius);
                _lastPublishedActiveSonarGeoRadius = primaryRadius;
                _lastPublishedActiveSonarGeoCount = _activeSonarGeoPingCount;
            }

            if (force || _activeSonarGeoGlobalsDirty || !NearlyEqual(_lastPublishedActiveSonarGeoState, state, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(_ShaderActiveSonarGeoParams, state);
                _lastPublishedActiveSonarGeoState = state;
            }

            PublishActiveSonarGeoRingCountTelemetry();
            _activeSonarGeoGlobalsDirty = false;
        }

        private void PublishActiveSonarGeoRingCountTelemetry()
        {
            if (_lastTelemetryPublishedActiveSonarGeoCount == _activeSonarGeoPingCount)
                return;

            GlobalTelemetryBus.PublishModTelemetry(
                _ActiveSonarGeoSystemHash,
                _ActiveSonarGeoRingCountHash,
                _activeSonarGeoPingCount);
            _lastTelemetryPublishedActiveSonarGeoCount = _activeSonarGeoPingCount;
        }

        private void ClearActiveSonarGeoGlobals()
        {
            _activeSonarGeoPingCount = 0;
            for (int i = 0; i < ActiveSonarGeoPingCapacity; i++)
            {
                _activeSonarGeoCentersRadius[i] = Vector4.zero;
                _activeSonarGeoParams[i] = Vector4.zero;
            }

            _activeSonarGeoGlobalsDirty = true;
            Shader.SetGlobalVector(_ShaderActiveSonarCenterAup, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderActiveSonarRadius, 0f);
            Shader.SetGlobalVectorArray(_ShaderActiveSonarCentersRadius, _activeSonarGeoCentersRadius);
            Shader.SetGlobalVectorArray(_ShaderActiveSonarParams, _activeSonarGeoParams);
            Shader.SetGlobalVector(_ShaderActiveSonarGeoParams, Vector4.zero);
            _lastPublishedActiveSonarGeoCount = 0;
            _lastPublishedActiveSonarGeoRadius = 0f;
            _lastPublishedActiveSonarGeoState = Vector4.zero;
        }

        private void EnsureActiveSonarGeoTelemetryRing()
        {
            if (!Application.isPlaying || TryReadActiveSonarGeoTelemetryRing(out _))
                return;

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
                return;

            _activeSonarGeoTelemetryRingHandle = vault.EnsureGenerationHandle<ActiveSonarGeoTelemetryEntry>(
                ActiveSonarGeoTelemetryRingBufferId,
                ActiveSonarGeoTelemetryCapacity,
                SpectrumVaultOwner,
                NativeArrayOptions.ClearMemory);
        }

        private void DisposeActiveSonarGeoTelemetryRing()
        {
            ReleaseVaultBuffer(_dataVault, ref _activeSonarGeoTelemetryRingHandle);
            _activeSonarGeoTelemetryWriteIndex = 0;
            if (_aupDiscoveryGridHandle.BufferID == 0u)
                _dataVault = null;
        }

        private void WriteActiveSonarGeoTelemetry()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                _activeSonarGeoTelemetryRingHandle.BufferID == 0u ||
                !vault.TryAcquireWriteLock(in _activeSonarGeoTelemetryRingHandle, SpectrumVaultOwner, out NativeArray<ActiveSonarGeoTelemetryEntry> telemetryRing))
            {
                return;
            }

            try
            {
                if (vault.IsCompactionFenceActive ||
                    !telemetryRing.IsCreated ||
                    telemetryRing.Length <= 0)
                {
                    return;
                }

                int index = _activeSonarGeoTelemetryWriteIndex;
                int capacity = math.min(ActiveSonarGeoTelemetryCapacity, telemetryRing.Length);
                if ((uint)index >= (uint)capacity)
                    index = 0;
                _activeSonarGeoTelemetryWriteIndex = (index + 1) % capacity;
                Vector4 primary = _activeSonarGeoPingCount > 0 ? _activeSonarGeoCentersRadius[0] : Vector4.zero;
                ActiveSonarGeoTelemetryEntry entry = default;
                entry.Frame = SystemDispatcher.CurrentFrameId;
                entry.ActiveRingCount = _activeSonarGeoPingCount;
                entry.PrimaryRadius = primary.w;
                entry.PrimaryCenter = MakeFloat3(primary.x, primary.y, primary.z);
                entry.Flags = ResolveActiveSonarGeoQualityWeight() <= 0.15f ? 1u : 0u;
                telemetryRing[index] = entry;
            }
            finally
            {
                vault.ReleaseWriteLock(in _activeSonarGeoTelemetryRingHandle, SpectrumVaultOwner);
            }
        }

        private void HandleActiveSonarGeoNonFinite()
        {
            GlobalTelemetryBus.PublishPerformanceWarning(
                _ActiveSonarGeoNaNHash,
                _ActiveSonarGeoSystemHash,
                _activeSonarGeoPingCount);
            DumpActiveSonarGeoTelemetry();
            ClearActiveSonarGeoGlobals();
        }

        private void DumpActiveSonarGeoTelemetry()
        {
            if (!TryResolveActiveSonarGeoTelemetryCount(out int telemetryCount))
                return;

            try
            {
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "AgentLogs"));
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "Dump_ACTIVE_SONAR_ILLUMINATION.bin");
                using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
                Span<byte> row = stackalloc byte[ActiveSonarGeoTelemetryEntrySizeBytes];
                for (int i = 0; i < telemetryCount; i++)
                {
                    if (!TryReadActiveSonarGeoTelemetryEntry(i, out ActiveSonarGeoTelemetryEntry entry))
                        entry = default;

                    WriteActiveSonarGeoTelemetryEntry(row, in entry);
                    stream.Write(row);
                }
            }
            catch (IOException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
            catch (UnauthorizedAccessException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
            catch (ObjectDisposedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
            catch (InvalidOperationException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
            catch (ArgumentException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
            catch (NotSupportedException)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _ActiveSonarGeoDumpFailureHash,
                    _ActiveSonarGeoSystemHash,
                    1f);
            }
        }

        private bool TryResolveActiveSonarGeoTelemetryCount(out int telemetryCount)
        {
            telemetryCount = 0;
            if (!TryReadActiveSonarGeoTelemetryRing(out NativeArray<ActiveSonarGeoTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated)
            {
                return false;
            }

            telemetryCount = telemetryRing.Length;
            return telemetryCount > 0;
        }

        private bool TryReadActiveSonarGeoTelemetryEntry(int index, out ActiveSonarGeoTelemetryEntry entry)
        {
            entry = default;
            if (index < 0 ||
                !TryReadActiveSonarGeoTelemetryRing(out NativeArray<ActiveSonarGeoTelemetryEntry>.ReadOnly telemetryRing) ||
                !telemetryRing.IsCreated ||
                index >= telemetryRing.Length)
            {
                return false;
            }

            entry = telemetryRing[index];
            return true;
        }

        private static void WriteActiveSonarGeoTelemetryEntry(Span<byte> destination, in ActiveSonarGeoTelemetryEntry entry)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(4, 4), entry.ActiveRingCount);
            WriteFloatLittleEndian(destination.Slice(8, 4), entry.PrimaryRadius);
            WriteFloatLittleEndian(destination.Slice(12, 4), entry.PrimaryCenter.x);
            WriteFloatLittleEndian(destination.Slice(16, 4), entry.PrimaryCenter.y);
            WriteFloatLittleEndian(destination.Slice(20, 4), entry.PrimaryCenter.z);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(28, 4), 0u);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination, BitConverter.SingleToInt32Bits(value));
        }

        private void PublishSonarReveal(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            float radius,
            float revealDurationSeconds,
            float pulseTime,
            float pulseIntensity,
            float abyssalDistortion,
            float effectiveWaveSpeed)
        {
            MarkAupDiscoveryPulseShell(in originAup, radius, pulseIntensity);

            Shader.SetGlobalVector(_ShaderSonarRevealOrigin, MakeVector4(origin.x, origin.y, origin.z, radius));
            Shader.SetGlobalVector(_ShaderSonarPingCenter, MakeVector4(origin.x, origin.y, origin.z, pulseIntensity));
            Shader.SetGlobalVector(
                _ShaderSonarPingParams,
                MakeVector4(
                    radius,
                    math.lerp(6f, 2f, pulseIntensity),
                    pulseTime,
                    pulseTime + math.max(0.05f, revealDurationSeconds)));
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, pulseTime + math.max(0.05f, revealDurationSeconds));
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, abyssalDistortion);
            Shader.SetGlobalVector(
                _ShaderSonarRevealWaveParams,
                MakeVector4(
                    pulseTime,
                    effectiveWaveSpeed,
                    math.max(0.05f, sonarRevealFadeDuration),
                    pulseIntensity));
            PublishSonarWaveFront(_activeSonarWaveFront);
        }

        private void InitializeActiveSonarWavefront(
            float pulseRadius,
            float pulseTime,
            float effectiveWaveSpeed,
            float revealDurationSeconds,
            float waveBandWidth)
        {
            _activeSonarWaveFront = 0f;
            _activeSonarWaveSpeed = math.max(0.01f, effectiveWaveSpeed);
            _activeSonarRevealExpireTime = pulseTime + math.max(0.05f, revealDurationSeconds);
            _activeSonarWaveBandWidth = math.max(0.25f, waveBandWidth);
            _activeSonarWavefrontActive = pulseRadius > 0f;
            PublishSonarWaveFront(0f);
            PublishSonarRadius(0f);
        }

        private void UpdateActiveSonarWavefront(float deltaTime, float now)
        {
            if (!_activeSonarWavefrontActive)
                return;

            _activeSonarWaveFront += math.max(0f, deltaTime) * _activeSonarWaveSpeed;
            PublishSonarWaveFront(_activeSonarWaveFront);
            PublishSonarRadius(_activeSonarWaveFront);

            if (now <= _activeSonarRevealExpireTime)
                return;

            _activeSonarWavefrontActive = false;
            _activeSonarWaveSpeed = 0f;
            _activeSonarWaveBandWidth = 0f;
            PublishSonarRadius(0f);
        }

        private void UpdateLidarPersistence(float deltaTime)
        {
            if (_activeLidarPersistence <= 0.0001f)
            {
                if (_activeLidarPersistence != 0f)
                {
                    _activeLidarPersistence = 0f;
                    PublishLidarPersistence(0f);
                }

                return;
            }

            float decayScale = math.rcp(1f + math.max(0.01f, lidarPersistenceDecaySharpness) * math.max(0f, deltaTime));
            _activeLidarPersistence *= decayScale;
            if (_activeLidarPersistence < 0.0001f)
                _activeLidarPersistence = 0f;

            PublishLidarPersistence(_activeLidarPersistence);
        }

        private void UpdatePassiveRadar(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _passiveRadarTickAccumulator += deltaTime;
            if (_passiveRadarTickAccumulator < PassiveRadarTickIntervalSeconds)
                return;

            _passiveRadarTickAccumulator = 0f;
            StepPassiveRadar();
        }

        private void StepPassiveRadar()
        {
            for (int i = 0; i < _passiveRadarGrid.Length; i++)
            {
                float decayedEnergy = _passiveRadarGrid[i] * PassiveRadarDecayFactor;
                _passiveRadarGrid[i] = decayedEnergy > PassiveRadarEnergyEpsilon ? decayedEnergy : 0f;
            }

            ISpatialAudioWorldEmitterReadModel audioManager = _spatialAudioEmitterReadModel;
            if (!TryResolvePlayerAup(out AbsoluteUniversePosition listenerAup) ||
                audioManager == null)
            {
                UpdatePassiveRadarPeakAndShaderState();
                return;
            }

            int emitterCount = audioManager.CopyActiveWorldEmitterSamples(s_passiveRadarEmitterBuffer);
            int nearestCount = SelectNearestPassiveRadarEmitters(in listenerAup, emitterCount);
            float minimumDistanceSqr = PassiveRadarMinimumDistanceMeters * PassiveRadarMinimumDistanceMeters;
            for (int i = 0; i < nearestCount; i++)
            {
                AbsoluteUniversePosition sampleAup = s_passiveRadarNearestAups[i];
                float amplitude = s_passiveRadarNearestAmplitudes[i];
                float3 deltaAup = AupPrecisionMath.LocalDeltaFloat3(
                    sampleAup.ToAbsoluteDouble3(),
                    listenerAup.ToAbsoluteDouble3(),
                    float3.zero);
                float distanceSqr = math.max(math.lengthsq(deltaAup), minimumDistanceSqr);
                float inverseDistance = math.rcp(distanceSqr);
                int sector = EncodePassiveRadarSectorFast(deltaAup);
                _passiveRadarGrid[sector] += amplitude * inverseDistance;
            }

            UpdatePassiveRadarPeakAndShaderState();
        }

        private static int SelectNearestPassiveRadarEmitters(in AbsoluteUniversePosition listenerAup, int emitterCount)
        {
            for (int i = 0; i < PassiveRadarSourceBudget; i++)
            {
                s_passiveRadarNearestDistanceSqr[i] = double.MaxValue;
                s_passiveRadarNearestAups[i] = default;
                s_passiveRadarNearestAmplitudes[i] = 0f;
            }

            int safeEmitterCount = math.min(emitterCount, s_passiveRadarEmitterBuffer.Length);
            int selectedCount = 0;
            double maxDistanceSqr = (double)PassiveRadarMaxSourceDistanceMeters * PassiveRadarMaxSourceDistanceMeters;
            for (int emitterIndex = 0; emitterIndex < safeEmitterCount; emitterIndex++)
            {
                SpatialAudioActiveEmitterSample sample = s_passiveRadarEmitterBuffer[emitterIndex];
                AbsoluteUniversePosition sampleAup = sample.PositionAup;
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in sampleAup, in listenerAup);
                if (distanceSqr > maxDistanceSqr)
                    continue;

                int targetSlot;
                if (selectedCount < PassiveRadarSourceBudget)
                {
                    targetSlot = selectedCount;
                    selectedCount++;
                }
                else
                {
                    targetSlot = -1;
                    double farthestDistanceSqr = double.MinValue;
                    for (int slot = 0; slot < PassiveRadarSourceBudget; slot++)
                    {
                        double slotDistanceSqr = s_passiveRadarNearestDistanceSqr[slot];
                        if (slotDistanceSqr <= farthestDistanceSqr)
                            continue;

                        farthestDistanceSqr = slotDistanceSqr;
                        targetSlot = slot;
                    }

                    if (targetSlot < 0 || distanceSqr >= farthestDistanceSqr)
                        continue;
                }

                s_passiveRadarNearestDistanceSqr[targetSlot] = distanceSqr;
                s_passiveRadarNearestAups[targetSlot] = sampleAup;
                s_passiveRadarNearestAmplitudes[targetSlot] = sample.Amplitude;
            }

            return selectedCount;
        }

        private static int EncodePassiveRadarSectorFast(float3 delta)
        {
            float ax = math.abs(delta.x);
            float ay = math.abs(delta.y);
            float az = math.abs(delta.z);
            int azimuthSector;
            if (delta.z >= 0f)
            {
                if (az <= ax * 0.0001f)
                {
                    azimuthSector = delta.x >= 0f ? 6 : 2;
                }
                else if (delta.x >= 0f)
                {
                    azimuthSector = ax <= az ? 4 : 5;
                }
                else
                {
                    azimuthSector = ax <= az ? 4 : 3;
                }
            }
            else if (delta.x >= 0f)
            {
                azimuthSector = ax <= az ? 7 : 6;
            }
            else
            {
                azimuthSector = ax <= az ? 0 : 1;
            }

            float horizontalAxis = math.max(ax, az);
            int elevationSector = delta.y < 0f
                ? (ay > horizontalAxis ? 0 : 1)
                : (ay > horizontalAxis ? 3 : 2);
            return (azimuthSector * PassiveRadarElevationSectorCount) + elevationSector;
        }

        private void UpdatePassiveRadarPeakAndShaderState()
        {
            float peakEnergy = 0f;
            int peakSector = -1;
            int activeSectorCount = 0;
            bool rowsChanged = !_passiveRadarShaderRowsInitialized;
            for (int azimuthSector = 0; azimuthSector < PassiveRadarAzimuthSectorCount; azimuthSector++)
            {
                int rowBaseIndex = azimuthSector * PassiveRadarElevationSectorCount;
                Vector4 row = MakeVector4(
                    _passiveRadarGrid[rowBaseIndex],
                    _passiveRadarGrid[rowBaseIndex + 1],
                    _passiveRadarGrid[rowBaseIndex + 2],
                    _passiveRadarGrid[rowBaseIndex + 3]);
                if (!NearlyEqual(s_passiveRadarRows[azimuthSector], row, ShaderVectorPublishEpsilon))
                    rowsChanged = true;

                s_passiveRadarRows[azimuthSector] = row;

                for (int elevationSector = 0; elevationSector < PassiveRadarElevationSectorCount; elevationSector++)
                {
                    float energy = _passiveRadarGrid[rowBaseIndex + elevationSector];
                    if (energy > PassiveRadarEnergyEpsilon)
                        activeSectorCount++;

                    if (energy <= peakEnergy)
                        continue;

                    peakEnergy = energy;
                    peakSector = rowBaseIndex + elevationSector;
                }
            }

            _passiveRadarPeakHistory[_passiveRadarAutoGainWriteIndex] = peakEnergy;
            _passiveRadarAutoGainWriteIndex++;
            if (_passiveRadarAutoGainWriteIndex >= PassiveRadarAutoGainHistoryLength)
                _passiveRadarAutoGainWriteIndex = 0;

            float autoGain = 0f;
            for (int i = 0; i < _passiveRadarPeakHistory.Length; i++)
            {
                if (_passiveRadarPeakHistory[i] > autoGain)
                    autoGain = _passiveRadarPeakHistory[i];
            }

            _passiveRadarPeakEnergy = peakEnergy;
            _passiveRadarPeakSector = peakSector;
            _passiveRadarAutoGain = autoGain > PassiveRadarEnergyEpsilon ? autoGain : 1f;
            int peakAzimuthSector = peakSector >= 0 ? peakSector / PassiveRadarElevationSectorCount : -1;
            int peakElevationSector = peakSector >= 0 ? peakSector & (PassiveRadarElevationSectorCount - 1) : -1;
            PublishPassiveRadarShaderState(
                MakeVector4(peakAzimuthSector, peakElevationSector, peakEnergy, activeSectorCount),
                _passiveRadarAutoGain,
                rowsChanged);
        }

        private void PublishPassiveRadarShaderState(Vector4 peakPayload, float autoGain, bool rowsChanged)
        {
            if (rowsChanged || !_passiveRadarShaderRowsInitialized)
            {
                Shader.SetGlobalVectorArray(_ShaderPassiveRadarRows, s_passiveRadarRows);
                _passiveRadarShaderRowsInitialized = true;
            }

            if (!_hasPublishedPassiveRadarPeak ||
                !NearlyEqual(_lastPublishedPassiveRadarPeak, peakPayload, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(_ShaderPassiveRadarPeak, peakPayload);
                _lastPublishedPassiveRadarPeak = peakPayload;
            }

            if (!_hasPublishedPassiveRadarPeak ||
                !NearlyEqual(_lastPublishedPassiveRadarAutoGain, autoGain, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalFloat(_ShaderPassiveRadarAutoGain, autoGain);
                _lastPublishedPassiveRadarAutoGain = autoGain;
            }

            _hasPublishedPassiveRadarPeak = true;
        }

        private void ClearPassiveRadarState()
        {
            for (int i = 0; i < _passiveRadarGrid.Length; i++)
                _passiveRadarGrid[i] = 0f;

            for (int i = 0; i < _passiveRadarPeakHistory.Length; i++)
                _passiveRadarPeakHistory[i] = 0f;

            for (int i = 0; i < s_passiveRadarRows.Length; i++)
                s_passiveRadarRows[i] = Vector4.zero;

            _passiveRadarTickAccumulator = 0f;
            _passiveRadarPeakEnergy = 0f;
            _passiveRadarAutoGain = 1f;
            _passiveRadarPeakSector = -1;
            _passiveRadarAutoGainWriteIndex = 0;
            PublishPassiveRadarShaderState(Vector4.zero, 1f, true);
        }

        private float ResolveAbyssalDistortion(float depth)
        {
            if (depth <= abyssalDistortionStartDepth)
                return 0f;

            float distortionRange = math.max(0.01f, abyssalDistortionFullDepth - abyssalDistortionStartDepth);
            return math.saturate((depth - abyssalDistortionStartDepth) / distortionRange);
        }

        private void TryPlayAbyssalAnchorReturn(float response01)
        {
            if (abyssalAnchorReturnClip == null || response01 <= 0f)
                return;

            Hecton8.Core.IAudioService audioManager = _audioService;
            if (audioManager == null)
                return;

            float volume = math.lerp(
                abyssalAnchorReturnVolumeMin,
                abyssalAnchorReturnVolumeMax,
                math.saturate(response01));
            audioManager.PlayStatic2D(abyssalAnchorReturnClip, volume, audioManager.InterfaceGroup);
        }

        private static ReadOnlySpan<char> ResolveLocalizedModeNotification(SpectrumMode mode)
        {
            switch (mode)
            {
                case SpectrumMode.Thermal:
                    return ResolveLocalizedSpan(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_THERMAL, "SPECTRUM: THERMAL");
                case SpectrumMode.Sonar:
                    return ResolveLocalizedSpan(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_SONAR, "SPECTRUM: SONAR");
                case SpectrumMode.Echolocation:
                    return ResolveLocalizedSpan(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_ECHOLOCATION, "SPECTRUM: ECHOLOCATION");
                default:
                    return ResolveLocalizedSpan(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_NORMAL, "SPECTRUM: NORMAL");
            }
        }

        private static ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel localization = Hecton8.Core.GlobalRegistry.LocalizationText;
            return localization != null
                ? localization.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

    }
}
