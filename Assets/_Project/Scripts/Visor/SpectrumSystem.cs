// ============================================================================
// HECTON-8 — SpectrumSystem.cs
// Система режимов визора Hecton-OS: SPECTRUM вкладка.
//
// ЛОР (лор2 Раздел 9):
//   SPECTRUM: Управление визором
//   • Тепловизор — тепловые сигнатуры существ и оборудования
//   • Сонар — движение в радиусе 100м (не показывает что — только что есть)
//   • Эхолот — биомеханические сигнатуры (Атлас-6 дроны)
//
// АРХИТЕКТУРА:
//   • Singleton. Переключает режимы через Shader.SetGlobalInt.
//   • Интегрируется с VisorHUDController через GlitchPulse при смене.
//   • Публикует события для HUD и пост-процессинга.
//   • ITickable — обновляет сонар-пульс.
//
// ZERO GC:
//   • Никаких new/LINQ в Tick.
//   • Cached shader property IDs.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using Hecton8.AI;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
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
        Normal      = 0,   // Обычный режим
        Thermal     = 1,   // Тепловизор
        Sonar       = 2,   // Сонар (движение)
        Echolocation = 3   // Эхолот (биомеханические сигнатуры)
    }

    /// <summary>
    /// Resource-authored active-sonar echo payload forwarded into the procedural audio pipeline.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
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
            WorldPosition = worldPosition;
            DistanceMeters = distanceMeters;
            ReturnStrength = returnStrength;
            Resonance = resonance;
            AudioMaterialId = audioMaterialId;
        }

        /// <summary>World-space origin of the reflected return.</summary>
        public Vector3 WorldPosition { get; }
        /// <summary>One-way listener-to-target distance in authored meters.</summary>
        public float DistanceMeters { get; }
        /// <summary>Normalized return energy emitted by the struck resource node.</summary>
        public float ReturnStrength { get; }
        /// <summary>Pitch scalar used by the echo renderer. 1 = neutral.</summary>
        public float Resonance { get; }
        /// <summary>Material route for sonar echo pitch, decay, and low-pass coloration.</summary>
        public byte AudioMaterialId { get; }
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
    /// Queue-backed spectrum bus drained by <see cref="SystemDispatcher"/> in LateUpdate.
    /// </summary>
    public static class SpectrumEvents
    {
        private const int ModeListenerCapacity = 8;
        private const int SonarPulseListenerCapacity = 8;
        private const int SonarPingListenerCapacity = 24;
        private const int SonarSnapshotListenerCapacity = 8;
        private const int AcousticEchoListenerCapacity = 8;

        // COLD ALLOC: RegistryBucket<ISpectrumModeEventListener>[8] - deferred spectrum mode listeners - owner: SpectrumEvents
        private static readonly RegistryBucket<ISpectrumModeEventListener> _modeListeners =
            new RegistryBucket<ISpectrumModeEventListener>(ModeListenerCapacity);
        // COLD ALLOC: RegistryBucket<ISonarPulseEventListener>[8] - deferred sonar pulse listeners - owner: SpectrumEvents
        private static readonly RegistryBucket<ISonarPulseEventListener> _sonarPulseListeners =
            new RegistryBucket<ISonarPulseEventListener>(SonarPulseListenerCapacity);
        // COLD ALLOC: RegistryBucket<ISonarPingEventListener>[24] - deferred active sonar ping listeners - owner: SpectrumEvents
        private static readonly RegistryBucket<ISonarPingEventListener> _sonarPingListeners =
            new RegistryBucket<ISonarPingEventListener>(SonarPingListenerCapacity);
        // COLD ALLOC: RegistryBucket<ISonarSnapshotEventListener>[8] - deferred sonar snapshot listeners - owner: SpectrumEvents
        private static readonly RegistryBucket<ISonarSnapshotEventListener> _sonarSnapshotListeners =
            new RegistryBucket<ISonarSnapshotEventListener>(SonarSnapshotListenerCapacity);
        // COLD ALLOC: RegistryBucket<IAcousticEchoEventListener>[8] - deferred acoustic echo listeners - owner: SpectrumEvents
        private static readonly RegistryBucket<IAcousticEchoEventListener> _acousticEchoListeners =
            new RegistryBucket<IAcousticEchoEventListener>(AcousticEchoListenerCapacity);

        private static NativeQueue<SpectrumMode> _pendingModeChanged;
        private static NativeQueue<SpectrumMode> _nextFrameModeChanged;
        private static NativeQueue<float> _pendingSonarPulses;
        private static NativeQueue<float> _nextFrameSonarPulses;
        private static NativeQueue<float> _pendingSonarPings;
        private static NativeQueue<float> _nextFrameSonarPings;
        private static NativeQueue<SpatialSonarSnapshot> _pendingSonarSnapshots;
        private static NativeQueue<SpatialSonarSnapshot> _nextFrameSonarSnapshots;
        private static NativeQueue<AcousticEchoEvent> _pendingAcousticEchoes;
        private static NativeQueue<AcousticEchoEvent> _nextFrameAcousticEchoes;
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
        private static bool _isDispatching;
        private const int PendingModeChangedCapacity = 8;
        private const int PendingSonarPulseCapacity = 8;
        private const int PendingSonarPingCapacity = 24;
        private const int PendingSonarSnapshotCapacity = 8;
        private const int PendingAcousticEchoCapacity = 8;
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
            LastSonarPulseRadiusMeters = 0f;
        }

        /// <summary>Режим визора изменился.</summary>

        /// <summary>Сонар-пульс. float: радиус обнаружения.</summary>
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
                    + _nextFrameAcousticEchoCount;
            }
        }

        /// <summary>Registers a listener for spectrum mode changes.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterModeListener(ISpectrumModeEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_modeListeners.Contains(listener))
                _modeListeners.Register(listener);
        }

        /// <summary>Unregisters a listener from spectrum mode changes.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterModeListener(ISpectrumModeEventListener listener)
        {
            if (listener == null)
                return;

            if (_modeListeners.Contains(listener))
                _modeListeners.Unregister(listener);
        }

        /// <summary>Registers a listener for sonar pulse radius broadcasts.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarPulseListener(ISonarPulseEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_sonarPulseListeners.Contains(listener))
                _sonarPulseListeners.Register(listener);
        }

        /// <summary>Unregisters a listener from sonar pulse radius broadcasts.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarPulseListener(ISonarPulseEventListener listener)
        {
            if (listener == null)
                return;

            if (_sonarPulseListeners.Contains(listener))
                _sonarPulseListeners.Unregister(listener);
        }

        /// <summary>Registers a listener for active sonar ping events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarPingListener(ISonarPingEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_sonarPingListeners.Contains(listener))
                _sonarPingListeners.Register(listener);
        }

        /// <summary>Unregisters a listener from active sonar ping events.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarPingListener(ISonarPingEventListener listener)
        {
            if (listener == null)
                return;

            if (_sonarPingListeners.Contains(listener))
                _sonarPingListeners.Unregister(listener);
        }

        /// <summary>Registers a listener for sonar snapshots.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterSonarSnapshotListener(ISonarSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_sonarSnapshotListeners.Contains(listener))
                _sonarSnapshotListeners.Register(listener);
        }

        /// <summary>Unregisters a listener from sonar snapshots.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterSonarSnapshotListener(ISonarSnapshotEventListener listener)
        {
            if (listener == null)
                return;

            if (_sonarSnapshotListeners.Contains(listener))
                _sonarSnapshotListeners.Unregister(listener);
        }

        /// <summary>Registers a listener for acoustic echo returns.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterAcousticEchoListener(IAcousticEchoEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_acousticEchoListeners.Contains(listener))
                _acousticEchoListeners.Register(listener);
        }

        /// <summary>Unregisters a listener from acoustic echo returns.</summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterAcousticEchoListener(IAcousticEchoEventListener listener)
        {
            if (listener == null)
                return;

            if (_acousticEchoListeners.Contains(listener))
                _acousticEchoListeners.Unregister(listener);
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
        public static void RaiseModeChanged(SpectrumMode mode)
        {
            EnsureInitialized();
            if (_pendingModeChangedCount + _nextFrameModeChangedCount >= PendingModeChangedCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameModeChanged.Enqueue(mode);
                _nextFrameModeChangedCount++;
            }
            else
            {
                _pendingModeChanged.Enqueue(mode);
                _pendingModeChangedCount++;
            }
        }

        /// <summary>Queues a sonar pulse radius broadcast.</summary>
        /// <param name="radius">Pulse radius in authored meters.</param>
        public static void RaiseSonarPulse(float radius)
        {
            LastSonarPulseRadiusMeters = Mathf.Max(0f, radius);
            EnsureInitialized();
            if (_pendingSonarPulseCount + _nextFrameSonarPulseCount >= PendingSonarPulseCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSonarPulses.Enqueue(LastSonarPulseRadiusMeters);
                _nextFrameSonarPulseCount++;
            }
            else
            {
                _pendingSonarPulses.Enqueue(LastSonarPulseRadiusMeters);
                _pendingSonarPulseCount++;
            }
        }

        /// <summary>Queues an active sonar ping broadcast.</summary>
        /// <param name="intensity">Normalized ping intensity.</param>
        public static void RaiseSonarPingSent(float intensity)
        {
            EnsureInitialized();
            if (_pendingSonarPingCount + _nextFrameSonarPingCount >= PendingSonarPingCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSonarPings.Enqueue(intensity);
                _nextFrameSonarPingCount++;
            }
            else
            {
                _pendingSonarPings.Enqueue(intensity);
                _pendingSonarPingCount++;
            }
        }

        /// <summary>Queues an updated spatial sonar snapshot.</summary>
        /// <param name="snapshot">Snapshot payload.</param>
        public static void RaiseSonarSnapshotUpdated(SpatialSonarSnapshot snapshot)
        {
            EnsureInitialized();
            if (_pendingSonarSnapshotCount + _nextFrameSonarSnapshotCount >= PendingSonarSnapshotCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSonarSnapshots.Enqueue(snapshot);
                _nextFrameSonarSnapshotCount++;
            }
            else
            {
                _pendingSonarSnapshots.Enqueue(snapshot);
                _pendingSonarSnapshotCount++;
            }
        }

        /// <summary>Queues one acoustic echo return.</summary>
        /// <param name="echoEvent">Echo payload.</param>
        public static void RaiseAcousticEchoReturned(AcousticEchoEvent echoEvent)
        {
            EnsureInitialized();
            if (_pendingAcousticEchoCount + _nextFrameAcousticEchoCount >= PendingAcousticEchoCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameAcousticEchoes.Enqueue(echoEvent);
                _nextFrameAcousticEchoCount++;
            }
            else
            {
                _pendingAcousticEchoes.Enqueue(echoEvent);
                _pendingAcousticEchoCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingModeChanged.IsCreated)
            {
                _pendingModeChanged = new NativeQueue<SpectrumMode>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SpectrumMode>[8] - deferred spectrum mode lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingModeChanged,
                    PendingModeChangedCapacity,
                    nameof(SpectrumEvents),
                    nameof(_pendingModeChanged),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameModeChanged.IsCreated)
            {
                _nextFrameModeChanged = new NativeQueue<SpectrumMode>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SpectrumMode>[8] - next-frame spectrum mode lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameModeChanged,
                    PendingModeChangedCapacity,
                    nameof(SpectrumEvents),
                    nameof(_nextFrameModeChanged),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingSonarPulses.IsCreated)
            {
                _pendingSonarPulses = new NativeQueue<float>(Allocator.Persistent); // COLD ALLOC: NativeQueue<float>[8] - deferred sonar pulse lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSonarPulses,
                    PendingSonarPulseCapacity,
                    nameof(SpectrumEvents),
                    nameof(_pendingSonarPulses),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameSonarPulses.IsCreated)
            {
                _nextFrameSonarPulses = new NativeQueue<float>(Allocator.Persistent); // COLD ALLOC: NativeQueue<float>[8] - next-frame sonar pulse lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSonarPulses,
                    PendingSonarPulseCapacity,
                    nameof(SpectrumEvents),
                    nameof(_nextFrameSonarPulses),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingSonarPings.IsCreated)
            {
                _pendingSonarPings = new NativeQueue<float>(Allocator.Persistent); // COLD ALLOC: NativeQueue<float>[24] - deferred active sonar ping lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSonarPings,
                    PendingSonarPingCapacity,
                    nameof(SpectrumEvents),
                    nameof(_pendingSonarPings),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameSonarPings.IsCreated)
            {
                _nextFrameSonarPings = new NativeQueue<float>(Allocator.Persistent); // COLD ALLOC: NativeQueue<float>[24] - next-frame active sonar ping lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSonarPings,
                    PendingSonarPingCapacity,
                    nameof(SpectrumEvents),
                    nameof(_nextFrameSonarPings),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingSonarSnapshots.IsCreated)
            {
                _pendingSonarSnapshots = new NativeQueue<SpatialSonarSnapshot>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SpatialSonarSnapshot>[8] - deferred sonar snapshot lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSonarSnapshots,
                    PendingSonarSnapshotCapacity,
                    nameof(SpectrumEvents),
                    nameof(_pendingSonarSnapshots),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameSonarSnapshots.IsCreated)
            {
                _nextFrameSonarSnapshots = new NativeQueue<SpatialSonarSnapshot>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SpatialSonarSnapshot>[8] - next-frame sonar snapshot lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSonarSnapshots,
                    PendingSonarSnapshotCapacity,
                    nameof(SpectrumEvents),
                    nameof(_nextFrameSonarSnapshots),
                    NativeAllocationLifetime.Session);
            }
            if (!_pendingAcousticEchoes.IsCreated)
            {
                _pendingAcousticEchoes = new NativeQueue<AcousticEchoEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AcousticEchoEvent>[8] - deferred acoustic echo lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingAcousticEchoes,
                    PendingAcousticEchoCapacity,
                    nameof(SpectrumEvents),
                    nameof(_pendingAcousticEchoes),
                    NativeAllocationLifetime.Session);
            }
            if (!_nextFrameAcousticEchoes.IsCreated)
            {
                _nextFrameAcousticEchoes = new NativeQueue<AcousticEchoEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AcousticEchoEvent>[8] - next-frame acoustic echo lane - owner: SpectrumEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameAcousticEchoes,
                    PendingAcousticEchoCapacity,
                    nameof(SpectrumEvents),
                    nameof(_nextFrameAcousticEchoes),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool FlushModeChanged()
        {
            if (!_pendingModeChanged.IsCreated)
                return true;

            int scanBudget = _pendingModeChangedCount > 0 ? _pendingModeChangedCount : PendingModeChangedCapacity;
            while (scanBudget > 0 && !_pendingModeChanged.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingModeChanged.TryDequeue(out SpectrumMode mode))
                    return true;

                if (_pendingModeChangedCount > 0)
                    _pendingModeChangedCount--;
                scanBudget--;
                ISpectrumModeEventListener[] rawArray = _modeListeners.RawArray;
                int count = Mathf.Min(_modeListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISpectrumModeEventListener listener = rawArray[i];
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

            int scanBudget = _pendingSonarPulseCount > 0 ? _pendingSonarPulseCount : PendingSonarPulseCapacity;
            while (scanBudget > 0 && !_pendingSonarPulses.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarPulses.TryDequeue(out float radius))
                    return true;

                if (_pendingSonarPulseCount > 0)
                    _pendingSonarPulseCount--;
                scanBudget--;
                ISonarPulseEventListener[] rawArray = _sonarPulseListeners.RawArray;
                int count = Mathf.Min(_sonarPulseListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarPulseEventListener listener = rawArray[i];
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

            int scanBudget = _pendingSonarPingCount > 0 ? _pendingSonarPingCount : PendingSonarPingCapacity;
            while (scanBudget > 0 && !_pendingSonarPings.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarPings.TryDequeue(out float intensity))
                    return true;

                if (_pendingSonarPingCount > 0)
                    _pendingSonarPingCount--;
                scanBudget--;
                ISonarPingEventListener[] rawArray = _sonarPingListeners.RawArray;
                int count = Mathf.Min(_sonarPingListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarPingEventListener listener = rawArray[i];
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

            int scanBudget = _pendingSonarSnapshotCount > 0 ? _pendingSonarSnapshotCount : PendingSonarSnapshotCapacity;
            while (scanBudget > 0 && !_pendingSonarSnapshots.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSonarSnapshots.TryDequeue(out SpatialSonarSnapshot snapshot))
                    return true;

                if (_pendingSonarSnapshotCount > 0)
                    _pendingSonarSnapshotCount--;
                scanBudget--;
                ISonarSnapshotEventListener[] rawArray = _sonarSnapshotListeners.RawArray;
                int count = Mathf.Min(_sonarSnapshotListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    ISonarSnapshotEventListener listener = rawArray[i];
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

            int scanBudget = _pendingAcousticEchoCount > 0 ? _pendingAcousticEchoCount : PendingAcousticEchoCapacity;
            while (scanBudget > 0 && !_pendingAcousticEchoes.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingAcousticEchoes.TryDequeue(out AcousticEchoEvent echoEvent))
                    return true;

                if (_pendingAcousticEchoCount > 0)
                    _pendingAcousticEchoCount--;
                scanBudget--;
                IAcousticEchoEventListener[] rawArray = _acousticEchoListeners.RawArray;
                int count = Mathf.Min(_acousticEchoListeners.Count, SpectrumListenerDispatchBudget);
                for (int i = count - 1; i >= 0; i--)
                {
                    IAcousticEchoEventListener listener = rawArray[i];
                    if (listener == null)
                        continue;

                    listener.OnAcousticEchoReturned(in echoEvent);
                }
            }

            if (_pendingAcousticEchoes.IsEmpty())
                _pendingAcousticEchoCount = 0;

            return true;
        }

        private static void DisposeQueues()
        {
            if (_pendingModeChanged.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_pendingModeChanged));
                _pendingModeChanged.Dispose();
                _pendingModeChanged = default;
            }

            if (_nextFrameModeChanged.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_nextFrameModeChanged));
                _nextFrameModeChanged.Dispose();
                _nextFrameModeChanged = default;
            }

            if (_pendingSonarPulses.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_pendingSonarPulses));
                _pendingSonarPulses.Dispose();
                _pendingSonarPulses = default;
            }

            if (_nextFrameSonarPulses.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_nextFrameSonarPulses));
                _nextFrameSonarPulses.Dispose();
                _nextFrameSonarPulses = default;
            }

            if (_pendingSonarPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_pendingSonarPings));
                _pendingSonarPings.Dispose();
                _pendingSonarPings = default;
            }

            if (_nextFrameSonarPings.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_nextFrameSonarPings));
                _nextFrameSonarPings.Dispose();
                _nextFrameSonarPings = default;
            }

            if (_pendingSonarSnapshots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_pendingSonarSnapshots));
                _pendingSonarSnapshots.Dispose();
                _pendingSonarSnapshots = default;
            }

            if (_nextFrameSonarSnapshots.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_nextFrameSonarSnapshots));
                _nextFrameSonarSnapshots.Dispose();
                _nextFrameSonarSnapshots = default;
            }

            if (_pendingAcousticEchoes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_pendingAcousticEchoes));
                _pendingAcousticEchoes.Dispose();
                _pendingAcousticEchoes = default;
            }

            if (_nextFrameAcousticEchoes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpectrumEvents), nameof(_nextFrameAcousticEchoes));
                _nextFrameAcousticEchoes.Dispose();
                _nextFrameAcousticEchoes = default;
            }

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
            _isDispatching = false;
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingModeChanged.IsCreated && !_pendingModeChanged.IsEmpty())
                || (_pendingSonarPulses.IsCreated && !_pendingSonarPulses.IsEmpty())
                || (_pendingSonarPings.IsCreated && !_pendingSonarPings.IsEmpty())
                || (_pendingSonarSnapshots.IsCreated && !_pendingSonarSnapshots.IsEmpty())
                || (_pendingAcousticEchoes.IsCreated && !_pendingAcousticEchoes.IsEmpty());
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameModeChanged.IsCreated)
            {
                while (_nextFrameModeChangedCount > 0 && _nextFrameModeChanged.TryDequeue(out SpectrumMode mode))
                {
                    _nextFrameModeChangedCount--;
                    _pendingModeChanged.Enqueue(mode);
                    _pendingModeChangedCount++;
                }
            }

            if (_nextFrameSonarPulses.IsCreated)
            {
                while (_nextFrameSonarPulseCount > 0 && _nextFrameSonarPulses.TryDequeue(out float radius))
                {
                    _nextFrameSonarPulseCount--;
                    _pendingSonarPulses.Enqueue(radius);
                    _pendingSonarPulseCount++;
                }
            }

            if (_nextFrameSonarPings.IsCreated)
            {
                while (_nextFrameSonarPingCount > 0 && _nextFrameSonarPings.TryDequeue(out float intensity))
                {
                    _nextFrameSonarPingCount--;
                    _pendingSonarPings.Enqueue(intensity);
                    _pendingSonarPingCount++;
                }
            }

            if (_nextFrameSonarSnapshots.IsCreated)
            {
                while (_nextFrameSonarSnapshotCount > 0 && _nextFrameSonarSnapshots.TryDequeue(out SpatialSonarSnapshot snapshot))
                {
                    _nextFrameSonarSnapshotCount--;
                    _pendingSonarSnapshots.Enqueue(snapshot);
                    _pendingSonarSnapshotCount++;
                }
            }

            if (_nextFrameAcousticEchoes.IsCreated)
            {
                while (_nextFrameAcousticEchoCount > 0 && _nextFrameAcousticEchoes.TryDequeue(out AcousticEchoEvent echoEvent))
                {
                    _nextFrameAcousticEchoCount--;
                    _pendingAcousticEchoes.Enqueue(echoEvent);
                    _pendingAcousticEchoCount++;
                }
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-95)]
    public sealed class SpectrumSystem : MonoBehaviour, ITickable, IAcousticPingEventListener, IAcousticEchoEventListener, IPhysicsAcousticImpulseEventListener
    {
        private const int SonarRevealMaxContacts = 24;
        private const int AbyssalAnchorScanBudget = 64;
        private const int PassiveRadarAzimuthSectorCount = 8;
        private const int PassiveRadarElevationSectorCount = 4;
        private const int PassiveRadarSectorCount = PassiveRadarAzimuthSectorCount * PassiveRadarElevationSectorCount;
        private const int PassiveRadarSourceBudget = 8;
        private const int PassiveRadarAutoGainHistoryLength = 30;
        private const int PassiveRadarSlowTickHz = 10;
        private const float PassiveRadarTickIntervalSeconds = 1f / PassiveRadarSlowTickHz;
        private const float PassiveRadarDecayFactor = 0.75f;
        private const float PassiveRadarMinimumDistanceMeters = 0.5f;
        private const float PassiveRadarMaxSourceDistanceMeters = 30f;
        private const uint AupDiscoveryDiscoveredBit = 1u;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Радиус сонара (метры).")]
        [SerializeField] private float sonarRadius = 100f;

        [Tooltip("Интервал сонар-пульса (сек).")]
        [SerializeField] private float sonarPulseInterval = 3f;

        [Tooltip("Энергия за переключение режима.")]
        [SerializeField] private float modeSwitchEnergyCost = 2f;

        [Tooltip("Энергия, сжигаемая каждым активным sonar pulse.")]
        [SerializeField] private float sonarPulseEnergyCost = 6f;

        [Tooltip("Интенсивность шумовой сигнатуры, публикуемой sonar pulse для окружающей фауны.")]
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

        [Tooltip("Maximum world-space positional jitter injected into returned sonar contacts at full abyssal distortion.")]
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
        [Tooltip("Система выживания для drain энергии.")]
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

        public static SpectrumSystem Instance => GlobalRegistry.Spectrum;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SpectrumMode _currentMode = SpectrumMode.Normal;
        private float _sonarTimer;
        private bool _registered;
        private bool _serviceRegistered;
        private bool _acousticPingSubscribed;
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
        private float _leviathanScreamRadarDistortion01;
        private NativeArray<uint> _aupDiscoveryGrid;
        private int _aupDiscoveryGridWidthRuntime;
        private int _aupDiscoveryGridHeightRuntime;
        private float _aupDiscoveryCellSizeRuntime;

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
        private static readonly int _ShaderSonarRevealContactCount =
            Shader.PropertyToID("_SonarRevealContactCount");
        private static readonly int _ShaderSonarRevealContacts =
            Shader.PropertyToID("_SonarRevealContacts");
        private static readonly int _ShaderSonarRevealContactMeta =
            Shader.PropertyToID("_SonarRevealContactMeta");
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
        private static readonly System.Collections.Generic.List<VisorHUDController> s_glitchControllers =
            new System.Collections.Generic.List<VisorHUDController>(4); // COLD ALLOC: shared glitch pulse controller buffer
        // COLD ALLOC: SpatialQueryHit[24] — active-sonar reveal contact buffer — owner: SpectrumSystem
        private static readonly SpatialQueryHit[] s_sonarRevealBuffer = new SpatialQueryHit[SonarRevealMaxContacts];
        // COLD ALLOC: Vector4[24] — active-sonar reveal shader payload buffer — owner: SpectrumSystem
        private static readonly Vector4[] s_sonarRevealContacts = new Vector4[SonarRevealMaxContacts];
        // COLD ALLOC: Vector4[24] — active-sonar semantic shader payload buffer — owner: SpectrumSystem
        private static readonly Vector4[] s_sonarRevealContactMeta = new Vector4[SonarRevealMaxContacts];
        // COLD ALLOC: float[32] â€” passive hydrophone radar energy grid â€” owner: SpectrumSystem
        private readonly float[] _passiveRadarGrid = new float[PassiveRadarSectorCount];
        // COLD ALLOC: float[30] â€” passive hydrophone auto-gain history â€” owner: SpectrumSystem
        private readonly float[] _passiveRadarPeakHistory = new float[PassiveRadarAutoGainHistoryLength];
        // COLD ALLOC: Vector4[8] â€” passive hydrophone shader row payload â€” owner: SpectrumSystem
        private static readonly Vector4[] s_passiveRadarRows = new Vector4[PassiveRadarAzimuthSectorCount];
        // COLD ALLOC: ActiveEmitterSample[32] â€” active world emitter buffer for passive hydrophone scan â€” owner: SpectrumSystem
        private static readonly SpatialAudioManager.ActiveEmitterSample[] s_passiveRadarEmitterBuffer = new SpatialAudioManager.ActiveEmitterSample[32];
        // COLD ALLOC: ActiveEmitterSample[8] â€” nearest emitter shortlist for passive hydrophone scan â€” owner: SpectrumSystem
        private static readonly SpatialAudioManager.ActiveEmitterSample[] s_passiveRadarNearestBuffer = new SpatialAudioManager.ActiveEmitterSample[PassiveRadarSourceBudget];
        // COLD ALLOC: float[8] â€” nearest emitter distance cache for passive hydrophone scan â€” owner: SpectrumSystem
        private static readonly double[] s_passiveRadarNearestDistanceSqr = new double[PassiveRadarSourceBudget];

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SpectrumMode CurrentMode => _currentMode;
        public bool IsThermalActive     => _currentMode == SpectrumMode.Thermal;
        public bool IsSonarActive       => _currentMode == SpectrumMode.Sonar;
        public bool IsEchoActive        => _currentMode == SpectrumMode.Echolocation;
        public bool HasSonarSnapshot    => _hasSonarSnapshot;
        public SpatialSonarSnapshot LastSonarSnapshot => _lastSonarSnapshot;

        public bool TryGetAupDiscoveryGrid(out NativeArray<uint> discoveryGrid, out int width, out int height, out float cellSizeMeters)
        {
            discoveryGrid = _aupDiscoveryGrid;
            width = _aupDiscoveryGridWidthRuntime;
            height = _aupDiscoveryGridHeightRuntime;
            cellSizeMeters = _aupDiscoveryCellSizeRuntime;
            return discoveryGrid.IsCreated && width > 0 && height > 0;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            SpectrumSystem activeRuntime = GlobalRegistry.Spectrum;
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
            SubscribeAcousticPingEvents();
            EnsureAupDiscoveryGrid();

            if (!_registered && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.UI);
                _registered = GlobalRegistry.Updatables.Contains(this);
            }

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
        }

        private void OnDisable()
        {
            UnsubscribeAcousticPingEvents();
            TryUnregisterService();

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

                _registered = false;
            }

            // Сбрасываем в Normal при отключении
            Shader.SetGlobalInt(_ShaderSpectrumMode, 0);
            SonarGridOverlay.ClearGlobals();
            ClearSonarSnapshot();
            ClearAcousticMappingGlobals();
        }

        private void OnDestroy()
        {
            UnsubscribeAcousticPingEvents();
            TryUnregisterService();

            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);

                _registered = false;
            }

            SonarGridOverlay.ClearGlobals();
            ClearAcousticMappingGlobals();
            DisposeAupDiscoveryGrid();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            SpectrumSystem activeRuntime = GlobalRegistry.Spectrum;
            if (activeRuntime != null && activeRuntime != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSpectrumRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Spectrum, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSpectrumRuntime(this);
            _serviceRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            if (IsEmpSensorBlindActive())
            {
                ClearSonarSnapshot();
                UpdateLidarPersistence(deltaTime);
                return;
            }

            UpdateActiveSonarWavefront(deltaTime);
            UpdateLidarPersistence(deltaTime);
            UpdateAcousticMappingGlobals(deltaTime);

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

        /// <summary>Переключить режим визора.</summary>
        public void SetMode(SpectrumMode mode)
        {
            if (mode == _currentMode) return;

            ResolveSurvivalSystem();

            // Drain энергии
            if (survivalSystem != null && modeSwitchEnergyCost > 0f)
                survivalSystem.DrainEnergy(modeSwitchEnergyCost);

            _currentMode = mode;
            _sonarTimer = 0f;

            if (_currentMode != SpectrumMode.Sonar)
                ClearSonarSnapshot();

            ApplyShaderMode();
            SpectrumEvents.RaiseModeChanged(mode);

            // Glitch pulse на визоре
            VisorHUDController.CopyActiveControllersTo(s_glitchControllers);
            for (int i = 0; i < s_glitchControllers.Count; i++)
                s_glitchControllers[i]?.GlitchPulse(0.2f);

            NotificationEvents.PushInfo(ResolveLocalizedModeNotification(mode));
        }

        /// <summary>Циклическое переключение режимов.</summary>
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

            float pulseRadius = Mathf.Max(1f, radius);
            float revealDurationValue = revealDurationSeconds > 0f ? revealDurationSeconds : sonarRevealDuration;
            return EmitSonarPulse(pulseRadius, revealDurationValue, true, true);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private void ApplyShaderMode()
        {
            Shader.SetGlobalInt(_ShaderSpectrumMode, (int)_currentMode);
            Shader.SetGlobalFloat(_ShaderSonarRadius, sonarRadius);
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

            Vector3 playerPosition = ToVector3(playerAup.ToRuntimeFloat3());
            float pulseTime = Time.time;
            float pulseIntensity = math.saturate(pulseRadius / 200f);
            float depth = ResolvePlayerMovement() != null ? Mathf.Max(0f, _playerMovement.CurrentDepth) : 0f;
            float abyssalDistortion = ResolveAbyssalDistortion(depth);
            float effectiveWaveSpeed = Mathf.Max(
                0.01f,
                sonarRevealWaveSpeed * math.lerp(1f, Mathf.Max(0.05f, abyssalWaveSpeedScaleMin), abyssalDistortion));
            float waveBandWidth = math.lerp(6f, 2f, pulseIntensity);
            float abyssalAnchorResponse01 = isActivePing ? ResolveAbyssalAnchorResponse01(playerPosition, pulseRadius) : 0f;
            InitializeActiveSonarWavefront(pulseRadius, pulseTime, effectiveWaveSpeed, revealDurationSeconds, waveBandWidth);
            PublishScreenSpaceSonarPulse(playerPosition, pulseRadius, pulseTime, pulseIntensity, effectiveWaveSpeed, waveBandWidth, revealDurationSeconds);
            SpectrumEvents.RaiseSonarPulse(pulseRadius);
            if (isActivePing)
            {
                _activeLidarPersistence = Mathf.Max(_activeLidarPersistence, pulseIntensity);
                Shader.SetGlobalFloat(_ShaderLidarPersistence, _activeLidarPersistence);
                SpectrumEvents.RaiseSonarPingSent(pulseIntensity);
                PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(
                    playerPosition,
                    pulseRadius,
                    pulseIntensity,
                    revealDurationSeconds,
                    FieldTargetRole.Generic,
                    0,
                    pulseRadius * pulseRadius * math.max(0.1f, pulseIntensity)));
                Vector3 playerForward = _playerTransform != null ? _playerTransform.forward : Vector3.forward;
                PublishActiveSonarDangerImpulse(playerPosition, playerForward, pulseRadius, pulseIntensity);
                TryPlayAbyssalAnchorReturn(abyssalAnchorResponse01);
            }

            Shader.SetGlobalFloat(_ShaderSonarPulseTime, pulseTime);
            Shader.SetGlobalFloat(_ShaderSonarRadius, 0f);
            PublishSonarReveal(playerPosition, pulseRadius, revealDurationSeconds, pulseTime, pulseIntensity, abyssalDistortion, effectiveWaveSpeed);
            WorldSpatialHashGrid.BuildSonarSnapshot(playerPosition, pulseRadius, out _lastSonarSnapshot);
            _hasSonarSnapshot = true;
            NoiseSystem.ReportPlayerSignal(playerPosition, 0f, false, 0f, 0f, math.saturate(sonarNoiseSignature01));
            if (isActivePing)
                NoiseSystem.ReportActiveSonarPing(playerPosition, pulseIntensity);
            SpectrumEvents.RaiseSonarSnapshotUpdated(_lastSonarSnapshot);
            return true;
        }

        private void SubscribeAcousticPingEvents()
        {
            if (_acousticPingSubscribed || !Application.isPlaying)
                return;

            PhysicsEventBus.Register((IAcousticPingEventListener)this);
            PhysicsEventBus.Register((IPhysicsAcousticImpulseEventListener)this);
            SpectrumEvents.RegisterAcousticEchoListener(this);
            _acousticPingSubscribed = true;
        }

        private void UnsubscribeAcousticPingEvents()
        {
            if (!_acousticPingSubscribed)
                return;

            PhysicsEventBus.Unregister((IAcousticPingEventListener)this);
            PhysicsEventBus.Unregister((IPhysicsAcousticImpulseEventListener)this);
            SpectrumEvents.UnregisterAcousticEchoListener(this);
            _acousticPingSubscribed = false;
        }

        /// <summary>
        /// Receives deferred acoustic ping events from the physics event lane.
        /// </summary>
        public void OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            HandleAcousticPing(in pingEvent);
        }

        public void OnAcousticEchoReturned(in AcousticEchoEvent echoEvent)
        {
            HandleAcousticEchoReturned(in echoEvent);
        }

        public void OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            HandleAcousticImpulse(in impulseEvent);
        }

        private void HandleAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (pingEvent.RadiusMeters <= 0f || pingEvent.Intensity01 <= 0f || pingEvent.LifetimeSeconds <= 0f)
                return;

            WorldSpatialHashGrid.RegisterTransientEvent(
                pingEvent.RuntimePosition,
                pingEvent.RadiusMeters,
                pingEvent.Intensity01,
                pingEvent.LifetimeSeconds,
                SpatialTransientEventType.AcousticImpulse,
                SpatialInteractionFlags.Signal | SpatialInteractionFlags.AcousticReceiver,
                pingEvent.SignalRole,
                pingEvent.SourceSpeciesId);
        }

        private void HandleAcousticEchoReturned(in AcousticEchoEvent echoEvent)
        {
            if (echoEvent.ReturnStrength <= 0.001f)
                return;

            float now = Time.time;
            float speed = Mathf.Max(0.01f, sonarScreenSpacePulseSpeedMetersPerSecond * Mathf.Max(0.05f, sonarEchoVisualSpeedScale));
            float delaySeconds = echoEvent.DistanceMeters > 0f
                ? echoEvent.DistanceMeters / Mathf.Max(0.01f, sonarRevealWaveSpeed)
                : 0f;
            float echoStartTime = now + delaySeconds;
            float echoRadius = Mathf.Clamp(echoEvent.DistanceMeters * 0.42f, 10f, Mathf.Max(10f, sonarRadius * 0.65f));
            float echoWidth = Mathf.Max(1.5f, _activeSonarWaveBandWidth * 1.65f);
            float echoIntensity = math.saturate(echoEvent.ReturnStrength * sonarEchoVisualIntensityScale);

            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoPulse,
                new Vector4(echoEvent.WorldPosition.x, echoEvent.WorldPosition.y, echoEvent.WorldPosition.z, echoStartTime));
            Shader.SetGlobalVector(
                _ShaderHectonSonarEchoParams,
                new Vector4(speed, echoRadius, echoWidth, echoIntensity));
            Shader.SetGlobalFloat(_ShaderSonarActive, 1f);

            _activeSonarEchoExpireTime = Mathf.Max(
                _activeSonarEchoExpireTime,
                echoStartTime + (echoRadius / speed) + sonarRevealFadeDuration);

            MarkAupDiscoveryCell(echoEvent.WorldPosition, echoEvent.ReturnStrength);
        }

        private void HandleAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            if ((impulseEvent.Flags & AcousticImpulseFlags.Leviathan) == 0)
                return;

            float scream01 = math.saturate(impulseEvent.Volume01 + (impulseEvent.KineticEnergyJoules * 0.00008f));
            _leviathanScreamRadarDistortion01 = Mathf.Max(_leviathanScreamRadarDistortion01, scream01);
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
            float visualWaveSpeed = Mathf.Max(0.01f, sonarScreenSpacePulseSpeedMetersPerSecond);
            Shader.SetGlobalVector(_ShaderHectonSonarPrimaryPulse, new Vector4(origin.x, origin.y, origin.z, pulseTime));
            Shader.SetGlobalVector(
                _ShaderHectonSonarVisualParams,
                new Vector4(
                    visualWaveSpeed,
                    Mathf.Max(1f, radius),
                    Mathf.Max(0.25f, waveBandWidth),
                    math.saturate(pulseIntensity)));
            Shader.SetGlobalFloat(_ShaderSonarActive, 1f);
            _activeSonarVisualExpireTime = pulseTime
                + Mathf.Max(0.05f, revealDurationSeconds)
                + (Mathf.Max(1f, radius) / visualWaveSpeed);
        }

        private void PublishActiveSonarDangerImpulse(Vector3 origin, Vector3 forward, float radius, float intensity)
        {
            float safeRadius = Mathf.Max(1f, radius);
            float safeIntensity = math.max(0.1f, math.saturate(intensity));
            float energyJoules = safeRadius * safeRadius * safeIntensity * Mathf.Max(0.1f, sonarAggroImpulseEnergyScale);
            PhysicsEventBus.NotifyAcousticImpulse(new AcousticImpulseEvent(
                origin,
                forward,
                energyJoules,
                safeIntensity,
                1f,
                safeRadius,
                0,
                0,
                AcousticImpulseFlags.Large));
        }

        private void ApplyAcousticMappingStaticGlobals()
        {
            Color sonarColor = sonarGridHardColor.linear;
            Shader.SetGlobalVector(
                _ShaderHectonSonarColor,
                new Vector4(sonarColor.r, sonarColor.g, sonarColor.b, Mathf.Max(0f, sonarGridContourBoost)));
            Shader.SetGlobalFloat(_ShaderHectonSonarNoirHideDistance, Mathf.Max(0f, sonarNoirHideDistanceMeters));
        }

        private void UpdateAcousticMappingGlobals(float deltaTime)
        {
            float now = Time.time;
            bool sonarActive = now <= _activeSonarVisualExpireTime || now <= _activeSonarEchoExpireTime;
            Shader.SetGlobalFloat(_ShaderSonarActive, sonarActive ? 1f : 0f);

            float speedStart = Mathf.Max(0f, radarDistortionStartSpeedMetersPerSecond);
            float speedFull = Mathf.Max(speedStart + 0.01f, radarDistortionFullSpeedMetersPerSecond);
            float speedStartSqr = speedStart * speedStart;
            float speedFullSqr = speedFull * speedFull;
            float speed01 = math.saturate(
                (ResolvePlayerSpeedMagnitudeSqr() - speedStartSqr) / math.max(0.0001f, speedFullSqr - speedStartSqr));
            _leviathanScreamRadarDistortion01 = Mathf.Max(
                0f,
                _leviathanScreamRadarDistortion01 - (Mathf.Max(0f, deltaTime) * Mathf.Max(0.1f, leviathanScreamRadarDecayPerSecond)));
            float radarDistortion01 = Mathf.Max(speed01, _leviathanScreamRadarDistortion01);
            Shader.SetGlobalVector(
                _ShaderHectonSonarRadarDistortion,
                new Vector4(speed01, _leviathanScreamRadarDistortion01, radarDistortion01, sonarActive ? 1f : 0f));
        }

        private float ResolvePlayerSpeedMagnitudeSqr()
        {
            if (_playerMovement != null)
                return _playerMovement.InterpolatedLinearVelocity.sqrMagnitude;

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                return math.lengthsq(runtimeContext.MovementState.Velocity);
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
            Shader.SetGlobalVector(_ShaderHectonSonarRadarDistortion, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderSonarActive, 0f);
        }

        private void EnsureAupDiscoveryGrid()
        {
            if (!Application.isPlaying)
                return;

            if (_aupDiscoveryGrid.IsCreated)
                return;

            _aupDiscoveryGridWidthRuntime = Mathf.Max(8, aupDiscoveryGridWidth);
            _aupDiscoveryGridHeightRuntime = Mathf.Max(8, aupDiscoveryGridHeight);
            _aupDiscoveryCellSizeRuntime = Mathf.Max(1f, aupDiscoveryCellSizeMeters);
            int cellCount = _aupDiscoveryGridWidthRuntime * _aupDiscoveryGridHeightRuntime;
            _aupDiscoveryGrid = new NativeArray<uint>(
                cellCount,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<uint>[AUP discovery grid] - persistent sonar map bits - owner: SpectrumSystem
            NativeMemorySentinel.RegisterNativeArray(
                _aupDiscoveryGrid,
                nameof(SpectrumSystem),
                nameof(_aupDiscoveryGrid),
                NativeAllocationLifetime.Scene);
        }

        private void DisposeAupDiscoveryGrid()
        {
            if (!_aupDiscoveryGrid.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_aupDiscoveryGrid);
            _aupDiscoveryGrid.Dispose();
            _aupDiscoveryGrid = default;
            _aupDiscoveryGridWidthRuntime = 0;
            _aupDiscoveryGridHeightRuntime = 0;
            _aupDiscoveryCellSizeRuntime = 0f;
        }

        private void MarkAupDiscoveryCell(Vector3 runtimePosition, float strength01)
        {
            if (!_aupDiscoveryGrid.IsCreated || _aupDiscoveryGridWidthRuntime <= 0 || _aupDiscoveryGridHeightRuntime <= 0)
                return;

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            double3 absolute = aup.ToAbsoluteDouble3();
            double invCellSize = 1.0 / System.Math.Max(1.0, _aupDiscoveryCellSizeRuntime);
            long cellX = (long)math.floor(absolute.x * invCellSize);
            long cellZ = (long)math.floor(absolute.z * invCellSize);
            int x = PositiveModulo(cellX, _aupDiscoveryGridWidthRuntime);
            int z = PositiveModulo(cellZ, _aupDiscoveryGridHeightRuntime);
            int index = (z * _aupDiscoveryGridWidthRuntime) + x;
            int strengthLevel = (int)math.clamp(math.floor(math.saturate(strength01) * 7.999f), 0f, 7f);
            uint strengthBit = 1u << (1 + strengthLevel);
            _aupDiscoveryGrid[index] = _aupDiscoveryGrid[index] | AupDiscoveryDiscoveredBit | strengthBit;
        }

        private void MarkAupDiscoveryPulseShell(Vector3 origin, float radius, float strength01)
        {
            MarkAupDiscoveryCell(origin, strength01);

            float shellDistance = Mathf.Max(_aupDiscoveryCellSizeRuntime, radius);
            float diagonalDistance = shellDistance * 0.70710678f;
            MarkAupDiscoveryCell(origin + new Vector3(shellDistance, 0f, 0f), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(-shellDistance, 0f, 0f), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(0f, 0f, shellDistance), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(0f, 0f, -shellDistance), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(diagonalDistance, 0f, diagonalDistance), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(-diagonalDistance, 0f, diagonalDistance), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(diagonalDistance, 0f, -diagonalDistance), strength01);
            MarkAupDiscoveryCell(origin + new Vector3(-diagonalDistance, 0f, -diagonalDistance), strength01);
        }

        private static int PositiveModulo(long value, int modulus)
        {
            if (modulus <= 0)
                return 0;

            long result = value % modulus;
            return (int)(result < 0 ? result + modulus : result);
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
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

            return SceneBootstrap.TryGetCurrentPlayerTransform(out _playerTransform) && _playerTransform != null;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            if (_playerMovement != null)
            {
                playerAup = _playerMovement.CurrentAup;
                return true;
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                playerAup = runtimeContext.MovementState.PredictedAup;
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
            return new Vector3(value.x, value.y, value.z);
        }

        private HectonPlayerMovement ResolvePlayerMovement()
        {
            if (_playerMovement != null)
                return _playerMovement;

            if (ResolvePlayerTransform())
                _playerTransform.TryGetComponent(out _playerMovement);

            return _playerMovement;
        }

        private void ClearSonarSnapshot()
        {
            _hasSonarSnapshot = false;
            _lastSonarSnapshot = default;
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, 0);
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, 0f);
            Shader.SetGlobalVector(_ShaderSonarRevealWaveParams, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, 0f);
            Shader.SetGlobalFloat(_ShaderSonarRadius, 0f);
            Shader.SetGlobalVector(_ShaderSonarPingCenter, Vector4.zero);
            Shader.SetGlobalVector(_ShaderSonarPingParams, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, 0f);
            Shader.SetGlobalFloat(_ShaderLidarPersistence, 0f);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
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
            SpectrumEvents.RaiseSonarSnapshotUpdated(default);
        }

        private static bool IsEmpSensorBlindActive()
        {
            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.TraumaDispatcher == null)
            {
                return false;
            }

            return runtimeContext.TraumaDispatcher.IsEmpSensorBlindActive;
        }

        private void PublishSonarReveal(
            Vector3 origin,
            float radius,
            float revealDurationSeconds,
            float pulseTime,
            float pulseIntensity,
            float abyssalDistortion,
            float effectiveWaveSpeed)
        {
            int contactCount = 0;
            MarkAupDiscoveryPulseShell(origin, radius, pulseIntensity);

            contactCount = AppendAbyssalAnchorContacts(
                origin,
                radius,
                pulseTime,
                effectiveWaveSpeed,
                abyssalDistortion,
                contactCount);

            int sceneContactCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                origin,
                radius,
                SpatialTargetKind.Signal | SpatialTargetKind.Module | SpatialTargetKind.Resource | SpatialTargetKind.Pickup | SpatialTargetKind.Scannable,
                s_sonarRevealBuffer);

            for (int i = 0; i < sceneContactCount && contactCount < SonarRevealMaxContacts; i++, contactCount++)
            {
                SpatialQueryHit hit = s_sonarRevealBuffer[i];
                Vector3 contactPosition = hit.Position;
                if (abyssalDistortion > 0.001f)
                    contactPosition += ResolveAbyssalContactJitter(origin, hit.Position, pulseTime, contactCount, abyssalDistortion);

                float arrivalOffset = ResolveApproximateEchoArrivalOffset(hit.DistanceSqr, radius, effectiveWaveSpeed);
                s_sonarRevealContacts[contactCount] = new Vector4(contactPosition.x, contactPosition.y, contactPosition.z, arrivalOffset);
                s_sonarRevealContactMeta[contactCount] = ResolveRevealContactMeta(hit);
                MarkAupDiscoveryCell(contactPosition, pulseIntensity);
            }

            Shader.SetGlobalVector(_ShaderSonarRevealOrigin, new Vector4(origin.x, origin.y, origin.z, radius));
            Shader.SetGlobalVector(_ShaderSonarPingCenter, new Vector4(origin.x, origin.y, origin.z, pulseIntensity));
            Shader.SetGlobalVector(
                _ShaderSonarPingParams,
                new Vector4(
                    radius,
                    math.lerp(6f, 2f, pulseIntensity),
                    pulseTime,
                    pulseTime + Mathf.Max(0.05f, revealDurationSeconds)));
            Shader.SetGlobalFloat(_ShaderSonarRevealExpireTime, pulseTime + Mathf.Max(0.05f, revealDurationSeconds));
            Shader.SetGlobalFloat(_ShaderAbyssalDistortion, abyssalDistortion);
            Shader.SetGlobalVector(
                _ShaderSonarRevealWaveParams,
                new Vector4(
                    pulseTime,
                    effectiveWaveSpeed,
                    Mathf.Max(0.05f, sonarRevealFadeDuration),
                    pulseIntensity));
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, _activeSonarWaveFront);
            Shader.SetGlobalInt(_ShaderSonarRevealContactCount, contactCount);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContacts, s_sonarRevealContacts);
            Shader.SetGlobalVectorArray(_ShaderSonarRevealContactMeta, s_sonarRevealContactMeta);
        }

        private void InitializeActiveSonarWavefront(
            float pulseRadius,
            float pulseTime,
            float effectiveWaveSpeed,
            float revealDurationSeconds,
            float waveBandWidth)
        {
            _activeSonarWaveFront = 0f;
            _activeSonarWaveSpeed = Mathf.Max(0.01f, effectiveWaveSpeed);
            _activeSonarRevealExpireTime = pulseTime + Mathf.Max(0.05f, revealDurationSeconds);
            _activeSonarWaveBandWidth = Mathf.Max(0.25f, waveBandWidth);
            _activeSonarWavefrontActive = pulseRadius > 0f;
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, 0f);
            Shader.SetGlobalFloat(_ShaderSonarRadius, 0f);
        }

        private void UpdateActiveSonarWavefront(float deltaTime)
        {
            if (!_activeSonarWavefrontActive)
                return;

            _activeSonarWaveFront += Mathf.Max(0f, deltaTime) * _activeSonarWaveSpeed;
            Shader.SetGlobalFloat(_ShaderSonarWaveFront, _activeSonarWaveFront);
            Shader.SetGlobalFloat(_ShaderSonarRadius, _activeSonarWaveFront);

            if (Time.time <= _activeSonarRevealExpireTime)
                return;

            _activeSonarWavefrontActive = false;
            _activeSonarWaveSpeed = 0f;
            _activeSonarWaveBandWidth = 0f;
            Shader.SetGlobalFloat(_ShaderSonarRadius, 0f);
        }

        private void UpdateLidarPersistence(float deltaTime)
        {
            if (_activeLidarPersistence <= 0.0001f)
            {
                if (_activeLidarPersistence != 0f)
                {
                    _activeLidarPersistence = 0f;
                    Shader.SetGlobalFloat(_ShaderLidarPersistence, 0f);
                }

                return;
            }

            float decayScale = 1f / (1f + Mathf.Max(0.01f, lidarPersistenceDecaySharpness) * Mathf.Max(0f, deltaTime));
            _activeLidarPersistence *= decayScale;
            if (_activeLidarPersistence < 0.0001f)
                _activeLidarPersistence = 0f;

            Shader.SetGlobalFloat(_ShaderLidarPersistence, _activeLidarPersistence);
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
                _passiveRadarGrid[i] *= PassiveRadarDecayFactor;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition listenerAup) ||
                !(Hecton8.Core.GlobalRegistry.Audio is SpatialAudioManager audioManager))
            {
                UpdatePassiveRadarPeakAndShaderState();
                return;
            }

            int emitterCount = audioManager.CopyActiveWorldEmitterSamples(s_passiveRadarEmitterBuffer);
            int nearestCount = SelectNearestPassiveRadarEmitters(in listenerAup, emitterCount);
            float minimumDistanceSqr = PassiveRadarMinimumDistanceMeters * PassiveRadarMinimumDistanceMeters;
            for (int i = 0; i < nearestCount; i++)
            {
                SpatialAudioManager.ActiveEmitterSample sample = s_passiveRadarNearestBuffer[i];
                AbsoluteUniversePosition sampleAup = AbsoluteUniversePosition.FromRuntimePosition(sample.Position);
                float3 deltaAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sampleAup, in listenerAup);
                float distanceSqr = math.max(math.lengthsq(deltaAup), minimumDistanceSqr);
                float inverseDistance = 1f / distanceSqr;
                float inverseDistanceMeters = math.rsqrt(distanceSqr);
                Vector3 direction = new Vector3(
                    deltaAup.x * inverseDistanceMeters,
                    deltaAup.y * inverseDistanceMeters,
                    deltaAup.z * inverseDistanceMeters);
                int sector = EncodePassiveRadarSector(direction);
                _passiveRadarGrid[sector] += sample.Amplitude * inverseDistance;
            }

            UpdatePassiveRadarPeakAndShaderState();
        }

        private static int SelectNearestPassiveRadarEmitters(in AbsoluteUniversePosition listenerAup, int emitterCount)
        {
            for (int i = 0; i < PassiveRadarSourceBudget; i++)
            {
                s_passiveRadarNearestDistanceSqr[i] = double.MaxValue;
                s_passiveRadarNearestBuffer[i] = default;
            }

            int safeEmitterCount = Mathf.Min(emitterCount, s_passiveRadarEmitterBuffer.Length);
            int selectedCount = 0;
            double maxDistanceSqr = (double)PassiveRadarMaxSourceDistanceMeters * PassiveRadarMaxSourceDistanceMeters;
            for (int emitterIndex = 0; emitterIndex < safeEmitterCount; emitterIndex++)
            {
                SpatialAudioManager.ActiveEmitterSample sample = s_passiveRadarEmitterBuffer[emitterIndex];
                AbsoluteUniversePosition sampleAup = AbsoluteUniversePosition.FromRuntimePosition(sample.Position);
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
                s_passiveRadarNearestBuffer[targetSlot] = sample;
            }

            return selectedCount;
        }

        private static int EncodePassiveRadarSector(Vector3 direction)
        {
            float azimuth = Mathf.Atan2(direction.x, direction.z);
            float elevation = Mathf.Asin(Mathf.Clamp(direction.y, -1f, 1f));
            int azimuthSector = Mathf.Clamp(
                Mathf.FloorToInt(((azimuth + Mathf.PI) / (Mathf.PI * 2f)) * PassiveRadarAzimuthSectorCount),
                0,
                PassiveRadarAzimuthSectorCount - 1);
            float elevation01 = Mathf.InverseLerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, elevation);
            int elevationSector = Mathf.Clamp(
                Mathf.FloorToInt(elevation01 * PassiveRadarElevationSectorCount),
                0,
                PassiveRadarElevationSectorCount - 1);
            return (azimuthSector * PassiveRadarElevationSectorCount) + elevationSector;
        }

        private void UpdatePassiveRadarPeakAndShaderState()
        {
            float peakEnergy = 0f;
            int peakSector = -1;
            int activeSectorCount = 0;
            for (int azimuthSector = 0; azimuthSector < PassiveRadarAzimuthSectorCount; azimuthSector++)
            {
                int rowBaseIndex = azimuthSector * PassiveRadarElevationSectorCount;
                Vector4 row = new Vector4(
                    _passiveRadarGrid[rowBaseIndex],
                    _passiveRadarGrid[rowBaseIndex + 1],
                    _passiveRadarGrid[rowBaseIndex + 2],
                    _passiveRadarGrid[rowBaseIndex + 3]);
                s_passiveRadarRows[azimuthSector] = row;

                for (int elevationSector = 0; elevationSector < PassiveRadarElevationSectorCount; elevationSector++)
                {
                    float energy = _passiveRadarGrid[rowBaseIndex + elevationSector];
                    if (energy > 0.0001f)
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
            _passiveRadarAutoGain = autoGain > 0.0001f ? autoGain : 1f;
            int peakAzimuthSector = peakSector >= 0 ? peakSector / PassiveRadarElevationSectorCount : -1;
            int peakElevationSector = peakSector >= 0 ? peakSector & (PassiveRadarElevationSectorCount - 1) : -1;
            Shader.SetGlobalVectorArray(_ShaderPassiveRadarRows, s_passiveRadarRows);
            Shader.SetGlobalVector(
                _ShaderPassiveRadarPeak,
                new Vector4(peakAzimuthSector, peakElevationSector, peakEnergy, activeSectorCount));
            Shader.SetGlobalFloat(_ShaderPassiveRadarAutoGain, _passiveRadarAutoGain);
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
            Shader.SetGlobalVectorArray(_ShaderPassiveRadarRows, s_passiveRadarRows);
            Shader.SetGlobalVector(_ShaderPassiveRadarPeak, Vector4.zero);
            Shader.SetGlobalFloat(_ShaderPassiveRadarAutoGain, 1f);
        }

        private float ResolveAbyssalDistortion(float depth)
        {
            if (depth <= abyssalDistortionStartDepth)
                return 0f;

            return Mathf.InverseLerp(
                abyssalDistortionStartDepth,
                Mathf.Max(abyssalDistortionStartDepth + 0.01f, abyssalDistortionFullDepth),
                depth);
        }

        private Vector3 ResolveAbyssalContactJitter(Vector3 origin, Vector3 position, float pulseTime, int index, float distortion)
        {
            if (abyssalContactJitterRadius <= 0f || distortion <= 0f)
                return Vector3.zero;

            float seed = pulseTime * 1.6180339f + index * 12.9898f + origin.x * 0.173f + origin.y * 0.117f + origin.z * 0.061f;
            float x = HashSigned(seed + position.x * 0.193f);
            float y = HashSigned(seed + position.y * 0.271f + 7.13f);
            float z = HashSigned(seed + position.z * 0.347f + 13.71f);
            Vector3 direction = new Vector3(x, y, z);
            if (direction.sqrMagnitude <= 0.0001f)
                direction = Vector3.up;
            else
                direction.Normalize();

            float amplitude = abyssalContactJitterRadius * distortion * (0.35f + 0.65f * Hash01(seed + 19.37f));
            return direction * amplitude;
        }

        private static float Hash01(float seed)
        {
            return Mathf.Repeat(Mathf.Sin(seed) * 43758.5453f, 1f);
        }

        private static float HashSigned(float seed)
        {
            return Hash01(seed) * 2f - 1f;
        }

        private void TryPlayAbyssalAnchorReturn(float response01)
        {
            if (abyssalAnchorReturnClip == null || response01 <= 0f)
                return;

            Hecton8.Core.IAudioService audioManager = Hecton8.Core.GlobalRegistry.Audio;
            if (audioManager == null)
                return;

            float volume = math.lerp(
                abyssalAnchorReturnVolumeMin,
                abyssalAnchorReturnVolumeMax,
                math.saturate(response01));
            audioManager.PlayStatic2D(abyssalAnchorReturnClip, volume, audioManager.InterfaceGroup);
        }

        private float ResolveAbyssalAnchorResponse01(Vector3 origin, float radius)
        {
            double nearestAnchorDistanceSqr = double.PositiveInfinity;
            if (TryResolveNearestAbyssalAnchorDistanceSqr(origin, radius, out double resolvedDistanceSqr))
                nearestAnchorDistanceSqr = resolvedDistanceSqr;

            if (double.IsPositiveInfinity(nearestAnchorDistanceSqr))
                return 0f;

            double radiusSqr = math.max(1.0, (double)radius * radius);
            return 1f - math.saturate((float)(nearestAnchorDistanceSqr / radiusSqr));
        }

        private bool TryResolveNearestAbyssalAnchorDistanceSqr(Vector3 origin, float radius, out double nearestDistanceSqr)
        {
            nearestDistanceSqr = double.PositiveInfinity;
            if (vegetationBridge == null)
                return false;

            double radiusSqr = (double)radius * radius;
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);

            NativeArray<Vector3> anchorsNative = vegetationBridge.ActiveAbyssalAnchorsNative;
            int anchorCount = Mathf.Min(
                AbyssalAnchorScanBudget,
                Mathf.Min(
                    vegetationBridge.ActiveAbyssalAnchorCount,
                    anchorsNative.IsCreated ? anchorsNative.Length : 0));
            for (int i = 0; i < anchorCount; i++)
            {
                AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchorsNative[i]);
                double distanceSqr = AbsoluteUniversePosition.DistanceSq(in anchorAup, in originAup);
                if (distanceSqr > radiusSqr || distanceSqr >= nearestDistanceSqr)
                    continue;

                nearestDistanceSqr = distanceSqr;
            }

            return !double.IsPositiveInfinity(nearestDistanceSqr);
        }

        private int AppendAbyssalAnchorContacts(
            Vector3 origin,
            float radius,
            float pulseTime,
            float effectiveWaveSpeed,
            float abyssalDistortion,
            int startIndex)
        {
            int writeIndex = startIndex;
            if (vegetationBridge == null)
                return writeIndex;

            double radiusSqr = (double)radius * radius;
            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromRuntimePosition(origin);
            NativeArray<Vector3> anchorsNative = vegetationBridge.ActiveAbyssalAnchorsNative;
            int anchorCount = Mathf.Min(
                AbyssalAnchorScanBudget,
                Mathf.Min(
                    vegetationBridge.ActiveAbyssalAnchorCount,
                    anchorsNative.IsCreated ? anchorsNative.Length : 0));
            for (int i = 0; i < anchorCount && writeIndex < SonarRevealMaxContacts; i++)
            {
                Vector3 anchorPosition = anchorsNative[i];
                AbsoluteUniversePosition anchorAup = AbsoluteUniversePosition.FromRuntimePosition(anchorPosition);
                if (AbsoluteUniversePosition.DistanceSq(in anchorAup, in originAup) > radiusSqr)
                    continue;

                WriteAbyssalAnchorContact(origin, in originAup, anchorPosition, in anchorAup, pulseTime, effectiveWaveSpeed, abyssalDistortion, writeIndex);
                writeIndex++;
            }

            return writeIndex;
        }

        private void WriteAbyssalAnchorContact(
            Vector3 origin,
            in AbsoluteUniversePosition originAup,
            Vector3 anchorPosition,
            in AbsoluteUniversePosition anchorAup,
            float pulseTime,
            float effectiveWaveSpeed,
            float abyssalDistortion,
            int writeIndex)
        {
            Vector3 contactPosition = anchorPosition;
            if (abyssalDistortion > 0.001f)
                contactPosition += ResolveAbyssalContactJitter(origin, anchorPosition, pulseTime, writeIndex, abyssalDistortion * 0.45f);

            float arrivalOffset = ResolveApproximateEchoDistanceMeters(in originAup, in anchorAup) / effectiveWaveSpeed;
            s_sonarRevealContacts[writeIndex] = new Vector4(contactPosition.x, contactPosition.y, contactPosition.z, arrivalOffset);
            s_sonarRevealContactMeta[writeIndex] = new Vector4(0f, 0f, 8.5f, 1f);
        }

        private static float ResolveApproximateEchoArrivalOffset(float distanceSqr, float radius, float effectiveWaveSpeed)
        {
            float safeRadius = math.max(1f, radius);
            float normalizedDistanceSqr = math.saturate(distanceSqr / math.max(1f, safeRadius * safeRadius));
            return normalizedDistanceSqr * (safeRadius / math.max(0.01f, effectiveWaveSpeed));
        }

        private static float ResolveApproximateEchoDistanceMeters(in AbsoluteUniversePosition originAup, in AbsoluteUniversePosition anchorAup)
        {
            double3 delta = anchorAup.ToAbsoluteDouble3() - originAup.ToAbsoluteDouble3();
            double ax = math.abs(delta.x);
            double ay = math.abs(delta.y);
            double az = math.abs(delta.z);
            double max = math.max(ax, math.max(ay, az));
            double min = math.min(ax, math.min(ay, az));
            double mid = ax + ay + az - max - min;
            return (float)math.min(float.MaxValue, max + mid * 0.375d + min * 0.125d);
        }

        private static string ResolveLocalizedModeName(SpectrumMode mode)
        {
            switch (mode)
            {
                case SpectrumMode.Thermal:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_THERMAL, "THERMAL");
                case SpectrumMode.Sonar:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_SONAR, "SONAR");
                case SpectrumMode.Echolocation:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_ECHOLOCATION, "ECHOLOCATION");
                default:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_MODE_NORMAL, "NORMAL");
            }
        }

        private static string ResolveLocalizedModeNotification(SpectrumMode mode)
        {
            switch (mode)
            {
                case SpectrumMode.Thermal:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_THERMAL, "SPECTRUM: THERMAL");
                case SpectrumMode.Sonar:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_SONAR, "SPECTRUM: SONAR");
                case SpectrumMode.Echolocation:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_ECHOLOCATION, "SPECTRUM: ECHOLOCATION");
                default:
                    return ResolveLocalized(LocalizationKeys.SPECTRUM_NOTIFICATION_MODE_NORMAL, "SPECTRUM: NORMAL");
            }
        }

        private Vector4 ResolveRevealContactMeta(SpatialQueryHit hit)
        {
            float hardResponse = 0.7f;
            float organicResponse = 0.15f;
            float contactRadius = 4.5f;

            if ((hit.Kind & SpatialTargetKind.Module) != 0)
            {
                hardResponse = 1f;
                organicResponse = 0f;
                contactRadius = 7.5f;
            }
            else if ((hit.Kind & SpatialTargetKind.Signal) != 0)
            {
                if (hit.SignalRole == FieldTargetRole.DistressBeacon)
                {
                    hardResponse = 0.18f;
                    organicResponse = 0.95f;
                    contactRadius = 7.25f;
                }
                else
                {
                    hardResponse = 0.92f;
                    organicResponse = 0.05f;
                    contactRadius = 6.25f;
                }
            }
            else if ((hit.Kind & SpatialTargetKind.Scannable) != 0)
            {
                hardResponse = 0.84f;
                organicResponse = 0.08f;
                contactRadius = 5.2f;
            }
            else if ((hit.Kind & SpatialTargetKind.Resource) != 0)
            {
                hardResponse = 0.38f;
                organicResponse = 0.44f;
                contactRadius = 4.8f;
            }
            else if ((hit.Kind & SpatialTargetKind.Pickup) != 0)
            {
                hardResponse = 0.55f;
                organicResponse = 0.2f;
                contactRadius = 4.2f;
            }

            if (vegetationBridge != null)
            {
                HectonMapMagicVegetationBridge.VegetationDensitySample vegetationSample =
                    vegetationBridge.GetVegetationDensity(hit.Position);
                if (vegetationSample.HasVegetation)
                {
                    float density = math.saturate(vegetationSample.Density);
                    float densityOrganicBoost =
                        vegetationSample.SemanticType == HectonMapMagicVegetationBridge.VegetationSemanticType.FloatingSargassum
                            ? math.lerp(0.3f, 1f, density)
                            : math.lerp(0.18f, 0.78f, density);
                    organicResponse = Mathf.Max(organicResponse, densityOrganicBoost);
                    hardResponse *= 1f - (organicResponse * 0.45f);
                    contactRadius = Mathf.Max(contactRadius, math.lerp(4f, 8.5f, density));
                }
            }

            return new Vector4(
                math.saturate(hardResponse),
                math.saturate(organicResponse),
                Mathf.Max(0.5f, contactRadius),
                0f);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager localization = Hecton8.Core.GlobalRegistry.Localization;
            return localization != null ? localization.GetOrFallback(localization.CurrentLanguage, key, fallback) : fallback;
        }
    }
}
