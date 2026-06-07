// ============================================================================
// HECTON-8 — RandomEventSystem.cs
// Sistema sluchaynyh sobytiy mira.
//
// LOR (lor3 Blok 16 — Random Event Table):
//   • Biolyuminestsentnyy shtorm: glubina > 1000m, vidimost +30%, privlechenie fauny
//   • Termalnyy vybros: riftovaya zona, uron oborudovaniyu, redkie mineraly
//   • Migratsiya stai: lyuboy biom, izmenenie povedeniya fauny
//   • Sboy Hecton-OS: radiatsiya/glubina, glitchi interfeysa
//   • Obrushenie peschery: vokselnaya zona, blokirovka puti, novyy lut
//
// ARHITEKTURA:
//   • ISlowTickable — proverka usloviy raz v 0.5s.
//   • Kazhdoe sobytie: usloviya, chastota, effekt.
//   • Publikuet sobytiya cherez RandomEventEvents.
//   • Integriruetsya s HectonDirectorAI (tension modifier).
//
// ZERO GC:
//   • Pre-allocated massiv sostoyaniy sobytiy.
//   • Nikakih new/LINQ v SlowTick.
// ============================================================================

using System;
using System.Runtime.InteropServices;
using System.Threading;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
#endif
using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.UI;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Unmanaged Mega-Bus payload fired when a meteor shower enters the world event lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct MeteorShowerEvent
    {
        [FieldOffset(0)] public long ObserverGridX;
        [FieldOffset(8)] public long ObserverGridY;
        [FieldOffset(16)] public long ObserverGridZ;
        [FieldOffset(24)] public float DurationSeconds;
        [FieldOffset(28)] public float Intensity;
        [FieldOffset(32)] public int Seed;
        [FieldOffset(36)] public float3 ObserverRuntimePosition;
        [FieldOffset(48)] public float3 ObserverLocalOffset;
        [FieldOffset(60)] public byte HasObserverRuntimePosition;
        [FieldOffset(61)] public byte HasObserverAup;
        [FieldOffset(62)] private ushort _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct SeismicShockwaveEvent
    {
        [FieldOffset(0)] public double3 AupStartDouble;
        [FieldOffset(24)] public double3 AupEndDouble;
        [FieldOffset(48)] public Vector3 EpicenterWS;
        [FieldOffset(60)] public Vector3 AupStart;
        [FieldOffset(72)] public Vector3 AupEnd;
        [FieldOffset(84)] public float ImpulseRadiusMeters;
        [FieldOffset(88)] public float ImpulseMagnitude;
        [FieldOffset(92)] public int AppliedStampCount;
        [FieldOffset(96)] public byte HasAupLineSegment;
        [FieldOffset(97)] private byte _pad0;
        [FieldOffset(98)] private ushort _pad1;
        [FieldOffset(100)] private uint _pad2;
        [FieldOffset(104)] private ulong _pad3;
        [FieldOffset(112)] private ulong _pad4;
        [FieldOffset(120)] private ulong _pad5;

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount)
            : this(
                epicenterWS,
                impulseRadiusMeters,
                impulseMagnitude,
                appliedStampCount,
                double3.zero,
                double3.zero,
                false)
        {
        }

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount,
            Vector3 aupStart,
            Vector3 aupEnd)
            : this(
                epicenterWS,
                impulseRadiusMeters,
                impulseMagnitude,
                appliedStampCount,
                ToDouble3(aupStart),
                ToDouble3(aupEnd),
                true)
        {
        }

        public SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount,
            double3 aupStart,
            double3 aupEnd)
            : this(
                epicenterWS,
                impulseRadiusMeters,
                impulseMagnitude,
                appliedStampCount,
                aupStart,
                aupEnd,
                true)
        {
        }

        private SeismicShockwaveEvent(
            Vector3 epicenterWS,
            float impulseRadiusMeters,
            float impulseMagnitude,
            int appliedStampCount,
            double3 aupStart,
            double3 aupEnd,
            bool hasAupLineSegment)
        {
            EpicenterWS = epicenterWS;
            ImpulseRadiusMeters = impulseRadiusMeters;
            ImpulseMagnitude = impulseMagnitude;
            AppliedStampCount = appliedStampCount;
            AupStart = ToVector3(aupStart);
            AupEnd = ToVector3(aupEnd);
            AupStartDouble = aupStart;
            AupEndDouble = aupEnd;
            HasAupLineSegment = hasAupLineSegment ? (byte)1 : (byte)0;
            _pad0 = 0;
            _pad1 = 0;
            _pad2 = 0u;
            _pad3 = 0ul;
            _pad4 = 0ul;
            _pad5 = 0ul;
        }

        private static Vector3 ToVector3(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private static double3 ToDouble3(Vector3 value)
        {
            return new double3(value.x, value.y, value.z);
        }
    }

    public enum RandomEventType
    {
        BiolumStorm     = 0,   // Biolyuminestsentnyy shtorm
        ThermalEruption = 1,   // Termalnyy vybros
        FaunaMigration  = 2,   // Migratsiya stai
        HectonOSGlitch  = 3,   // Sboy Hecton-OS
        CaveCollapse    = 4,   // Obrushenie peschery
        MeteorShower    = 5,   // Meteor shower
        SolarFlare      = 6    // Solar EMP flare
    }

    /// <summary>
    /// Deferred payload for random-event activation.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct RandomEventStartedPayload
    {
        /// <summary>Activated random-event type.</summary>
        [FieldOffset(0)]
        public RandomEventType Type;

        /// <summary>Normalized authored event intensity.</summary>
        [FieldOffset(4)]
        public float Intensity;
    }

    /// <summary>
    /// Listener contract for queue-backed random world events.
    /// </summary>
    public interface IRandomEventListener
    {
        /// <summary>Called when a random event starts.</summary>
        /// <param name="type">Activated event type.</param>
        /// <param name="intensity">Normalized event intensity.</param>
        void OnRandomEventStarted(RandomEventType type, float intensity);

        /// <summary>Called when a random event ends.</summary>
        /// <param name="type">Ended event type.</param>
        void OnRandomEventEnded(RandomEventType type);

        /// <summary>Called after a seismic shockwave has been queued and flushed.</summary>
        /// <param name="payload">Seismic payload.</param>
        void OnSeismicShockwave(in SeismicShockwaveEvent payload);
    }

    public static class RandomEventEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingStartedCapacity = 16;
        private const int PendingEndedCapacity = 16;
        private const int PendingSeismicShockwaveCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[16] - deferred random event listeners - owner: RandomEventEvents
        private static int _listenerCount;
        private static NativeQueue<RandomEventStartedPayload> _pendingStarted;
        private static NativeQueue<RandomEventStartedPayload> _nextFrameStarted;
        private static NativeQueue<RandomEventType> _pendingEnded;
        private static NativeQueue<RandomEventType> _nextFrameEnded;
        private static NativeQueue<SeismicShockwaveEvent> _pendingSeismicShockwaves;
        private static NativeQueue<SeismicShockwaveEvent> _nextFrameSeismicShockwaves;
        private static int _pendingStartedCount;
        private static int _nextFrameStartedCount;
        private static int _pendingEndedCount;
        private static int _nextFrameEndedCount;
        private static int _pendingSeismicShockwaveCount;
        private static int _nextFrameSeismicShockwaveCount;
        private static int s_x001RandomEventEventsSignalPushDropCount;
        private static bool _isDispatching;

        public static int PendingCount
        {
            get
            {
                return _pendingStartedCount
                    + _nextFrameStartedCount
                    + _pendingEndedCount
                    + _nextFrameEndedCount
                    + _pendingSeismicShockwaveCount
                    + _nextFrameSeismicShockwaveCount;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _pendingStartedCount = 0;
            _nextFrameStartedCount = 0;
            _pendingEndedCount = 0;
            _nextFrameEndedCount = 0;
            _pendingSeismicShockwaveCount = 0;
            _nextFrameSeismicShockwaveCount = 0;
            _isDispatching = false;
            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            _listenerCount = 0;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange stateChange)
        {
            if (stateChange == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                stateChange == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                ResetStaticState();
            }
        }
#endif

        public static void Register(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            RegisterImmediate(listener);
        }

        public static void Unregister(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterImmediate(listener);
        }

        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listenerCount <= 0)
                {
                    completed = DrainWithoutDispatch();
                }
                else
                {
                    completed = FlushStarted();
                    if (completed)
                        completed = FlushEnded();
                    if (completed)
                        completed = FlushSeismicShockwaves();
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!completed || HasPendingFrontEvents())
                return;

            PromoteNextFrameEvents();
        }

        public static void DropPendingAmbient()
        {
            DrainQueueImmediate(ref _pendingStarted);
            DrainQueueImmediate(ref _nextFrameStarted);
            DrainQueueImmediate(ref _pendingEnded);
            DrainQueueImmediate(ref _nextFrameEnded);
            DrainQueueImmediate(ref _pendingSeismicShockwaves);
            DrainQueueImmediate(ref _nextFrameSeismicShockwaves);
            _pendingStartedCount = 0;
            _nextFrameStartedCount = 0;
            _pendingEndedCount = 0;
            _nextFrameEndedCount = 0;
            _pendingSeismicShockwaveCount = 0;
            _nextFrameSeismicShockwaveCount = 0;
            _isDispatching = false;
        }

        public static bool TryRaiseStarted(RandomEventType type, float intensity)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingStartedCount + _nextFrameStartedCount >= PendingStartedCapacity)
                return false;

            RandomEventStartedPayload payload = new RandomEventStartedPayload
            {
                Type = type,
                Intensity = intensity
            };

            if (_isDispatching)
            {
                _nextFrameStarted.Enqueue(payload);
                _nextFrameStartedCount++;
                return true;
            }

            _pendingStarted.Enqueue(payload);
            _pendingStartedCount++;
            return true;
        }

        [Obsolete("Use TryRaiseStarted so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseStarted(RandomEventType type, float intensity) => TryRaiseStarted(type, intensity);

        public static bool TryRaiseEnded(RandomEventType type)
        {
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingEndedCount + _nextFrameEndedCount >= PendingEndedCapacity)
                return false;

            if (_isDispatching)
            {
                _nextFrameEnded.Enqueue(type);
                _nextFrameEndedCount++;
                return true;
            }

            _pendingEnded.Enqueue(type);
            _pendingEndedCount++;
            return true;
        }

        [Obsolete("Use TryRaiseEnded so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseEnded(RandomEventType type) => TryRaiseEnded(type);

        public static bool TryRaiseSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            PhysicsEventPayload physicsPayload = new PhysicsEventPayload
            {
                RuntimePosition = payload.EpicenterWS,
                Direction = default,
                ForceVector = default,
                ImpulseVector = default,
                RadiusMeters = math.max(payload.ImpulseRadiusMeters, payload.ImpulseRadiusMeters * 4f),
                Scalar0 = math.saturate(payload.ImpulseMagnitude / 48f),
                Scalar1 = 8f,
                Scalar2 = payload.ImpulseMagnitude * 1000f,
                PrimaryId = 0,
                DataHash = 0u,
                StatusBits = unchecked((uint)FieldTargetRole.HazardProbe),
                EventType = (ushort)PhysicsEventType.AcousticPing,
                Reserved = 0
            };
            SignalBus<PhysicsEventPayload>.TryPushTracked(in physicsPayload, ref s_x001RandomEventEventsSignalPushDropCount);
            if (_listenerCount <= 0)
                return false;

            EnsureInitialized();
            if (_pendingSeismicShockwaveCount + _nextFrameSeismicShockwaveCount >= PendingSeismicShockwaveCapacity)
                return false;

            if (_isDispatching)
            {
                _nextFrameSeismicShockwaves.Enqueue(payload);
                _nextFrameSeismicShockwaveCount++;
                return true;
            }

            _pendingSeismicShockwaves.Enqueue(payload);
            _pendingSeismicShockwaveCount++;
            return true;
        }

        [Obsolete("Use TryRaiseSeismicShockwave so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseSeismicShockwave(in SeismicShockwaveEvent payload) => TryRaiseSeismicShockwave(in payload);

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingStarted.IsCreated)
                {
                    _pendingStarted = new NativeQueue<RandomEventStartedPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - deferred random-event starts - owner: RandomEventEvents
                    RegisterNativeQueue(ref _pendingStarted, PendingStartedCapacity, nameof(_pendingStarted));
                    PrewarmQueue(ref _pendingStarted, PendingStartedCapacity);
                }
                if (!_nextFrameStarted.IsCreated)
                {
                    _nextFrameStarted = new NativeQueue<RandomEventStartedPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - next-frame random-event starts - owner: RandomEventEvents
                    RegisterNativeQueue(ref _nextFrameStarted, PendingStartedCapacity, nameof(_nextFrameStarted));
                    PrewarmQueue(ref _nextFrameStarted, PendingStartedCapacity);
                }
                if (!_pendingEnded.IsCreated)
                {
                    _pendingEnded = new NativeQueue<RandomEventType>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<RandomEventType>[16] - deferred random-event ends - owner: RandomEventEvents
                    RegisterNativeQueue(ref _pendingEnded, PendingEndedCapacity, nameof(_pendingEnded));
                    PrewarmQueue(ref _pendingEnded, PendingEndedCapacity);
                }
                if (!_nextFrameEnded.IsCreated)
                {
                    _nextFrameEnded = new NativeQueue<RandomEventType>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<RandomEventType>[16] - next-frame random-event ends - owner: RandomEventEvents
                    RegisterNativeQueue(ref _nextFrameEnded, PendingEndedCapacity, nameof(_nextFrameEnded));
                    PrewarmQueue(ref _nextFrameEnded, PendingEndedCapacity);
                }
                if (!_pendingSeismicShockwaves.IsCreated)
                {
                    _pendingSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - deferred seismic shockwaves - owner: RandomEventEvents
                    RegisterNativeQueue(ref _pendingSeismicShockwaves, PendingSeismicShockwaveCapacity, nameof(_pendingSeismicShockwaves));
                    PrewarmQueue(ref _pendingSeismicShockwaves, PendingSeismicShockwaveCapacity);
                }
                if (!_nextFrameSeismicShockwaves.IsCreated)
                {
                    _nextFrameSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - next-frame seismic shockwaves - owner: RandomEventEvents
                    RegisterNativeQueue(ref _nextFrameSeismicShockwaves, PendingSeismicShockwaveCapacity, nameof(_nextFrameSeismicShockwaves));
                    PrewarmQueue(ref _nextFrameSeismicShockwaves, PendingSeismicShockwaveCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                _pendingStartedCount = 0;
                _nextFrameStartedCount = 0;
                _pendingEndedCount = 0;
                _nextFrameEndedCount = 0;
                _pendingSeismicShockwaveCount = 0;
                _nextFrameSeismicShockwaveCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label)
            where T : unmanaged
        {
            int sentinelId = NativeMemorySentinel.RegisterNativeQueue(
                queue,
                capacity,
                nameof(RandomEventEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, label);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingStarted, nameof(_pendingStarted));
            ReleaseNativeQueue(ref _nextFrameStarted, nameof(_nextFrameStarted));
            ReleaseNativeQueue(ref _pendingEnded, nameof(_pendingEnded));
            ReleaseNativeQueue(ref _nextFrameEnded, nameof(_nextFrameEnded));
            ReleaseNativeQueue(ref _pendingSeismicShockwaves, nameof(_pendingSeismicShockwaves));
            ReleaseNativeQueue(ref _nextFrameSeismicShockwaves, nameof(_nextFrameSeismicShockwaves));
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, string label)
            where T : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), label);
            queue.Dispose();
            queue = default;
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

        private static bool FlushStarted()
        {
            if (!_pendingStarted.IsCreated)
                return true;

            int scanBudget = _pendingStartedCount > 0 ? _pendingStartedCount : PendingStartedCapacity;
            while (scanBudget > 0 && !_pendingStarted.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingStarted.TryDequeue(out RandomEventStartedPayload payload))
                {
                    _pendingStartedCount = 0;
                    return true;
                }

                _pendingStartedCount--;
                scanBudget--;
                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = _listeners[i].Listener;
                    if (listener == null)
                        continue;

                    listener.OnRandomEventStarted(payload.Type, payload.Intensity);
                }
            }

            if (_pendingStarted.IsEmpty())
                _pendingStartedCount = 0;

            return true;
        }

        private static bool FlushEnded()
        {
            if (!_pendingEnded.IsCreated)
                return true;

            int scanBudget = _pendingEndedCount > 0 ? _pendingEndedCount : PendingEndedCapacity;
            while (scanBudget > 0 && !_pendingEnded.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingEnded.TryDequeue(out RandomEventType type))
                {
                    _pendingEndedCount = 0;
                    return true;
                }

                _pendingEndedCount--;
                scanBudget--;
                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = _listeners[i].Listener;
                    if (listener == null)
                        continue;

                    listener.OnRandomEventEnded(type);
                }
            }

            if (_pendingEnded.IsEmpty())
                _pendingEndedCount = 0;

            return true;
        }

        private static bool FlushSeismicShockwaves()
        {
            if (!_pendingSeismicShockwaves.IsCreated)
                return true;

            int scanBudget = _pendingSeismicShockwaveCount > 0 ? _pendingSeismicShockwaveCount : PendingSeismicShockwaveCapacity;
            while (scanBudget > 0 && !_pendingSeismicShockwaves.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingSeismicShockwaves.TryDequeue(out SeismicShockwaveEvent payload))
                {
                    _pendingSeismicShockwaveCount = 0;
                    return true;
                }

                _pendingSeismicShockwaveCount--;
                scanBudget--;
                int count = _listenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = _listeners[i].Listener;
                    if (listener == null)
                        continue;

                    listener.OnSeismicShockwave(in payload);
                }
            }

            if (_pendingSeismicShockwaves.IsEmpty())
                _pendingSeismicShockwaveCount = 0;

            return true;
        }

        private static bool DrainWithoutDispatch()
        {
            if (_pendingStarted.IsCreated)
            {
                int scanBudget = _pendingStartedCount > 0 ? _pendingStartedCount : PendingStartedCapacity;
                while (scanBudget > 0 && !_pendingStarted.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingStarted.TryDequeue(out _))
                    {
                        _pendingStartedCount = 0;
                        return true;
                    }

                    _pendingStartedCount--;
                    scanBudget--;
                }

                if (_pendingStarted.IsEmpty())
                    _pendingStartedCount = 0;
            }

            if (_pendingEnded.IsCreated)
            {
                int scanBudget = _pendingEndedCount > 0 ? _pendingEndedCount : PendingEndedCapacity;
                while (scanBudget > 0 && !_pendingEnded.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingEnded.TryDequeue(out _))
                    {
                        _pendingEndedCount = 0;
                        return true;
                    }

                    _pendingEndedCount--;
                    scanBudget--;
                }

                if (_pendingEnded.IsEmpty())
                    _pendingEndedCount = 0;
            }

            if (_pendingSeismicShockwaves.IsCreated)
            {
                int scanBudget = _pendingSeismicShockwaveCount > 0 ? _pendingSeismicShockwaveCount : PendingSeismicShockwaveCapacity;
                while (scanBudget > 0 && !_pendingSeismicShockwaves.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return false;

                    if (!_pendingSeismicShockwaves.TryDequeue(out _))
                    {
                        _pendingSeismicShockwaveCount = 0;
                        return true;
                    }

                    _pendingSeismicShockwaveCount--;
                    scanBudget--;
                }

                if (_pendingSeismicShockwaves.IsEmpty())
                    _pendingSeismicShockwaveCount = 0;
            }

            return true;
        }

        private static void DrainQueueImmediate(ref NativeQueue<RandomEventStartedPayload> queue)
        {
            if (!queue.IsCreated)
                return;

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void DrainQueueImmediate(ref NativeQueue<RandomEventType> queue)
        {
            if (!queue.IsCreated)
                return;

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void DrainQueueImmediate(ref NativeQueue<SeismicShockwaveEvent> queue)
        {
            if (!queue.IsCreated)
                return;

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool HasPendingFrontEvents()
        {
            return (_pendingStarted.IsCreated && !_pendingStarted.IsEmpty())
                || (_pendingEnded.IsCreated && !_pendingEnded.IsEmpty())
                || (_pendingSeismicShockwaves.IsCreated && !_pendingSeismicShockwaves.IsEmpty());
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameStarted.IsCreated)
            {
                while (_nextFrameStartedCount > 0 && _nextFrameStarted.TryDequeue(out RandomEventStartedPayload payload))
                {
                    _nextFrameStartedCount--;
                    _pendingStarted.Enqueue(payload);
                    _pendingStartedCount++;
                }
            }

            if (_nextFrameEnded.IsCreated)
            {
                while (_nextFrameEndedCount > 0 && _nextFrameEnded.TryDequeue(out RandomEventType type))
                {
                    _nextFrameEndedCount--;
                    _pendingEnded.Enqueue(type);
                    _pendingEndedCount++;
                }
            }

            if (_nextFrameSeismicShockwaves.IsCreated)
            {
                while (_nextFrameSeismicShockwaveCount > 0 && _nextFrameSeismicShockwaves.TryDequeue(out SeismicShockwaveEvent payload))
                {
                    _nextFrameSeismicShockwaveCount--;
                    _pendingSeismicShockwaves.Enqueue(payload);
                    _pendingSeismicShockwaveCount++;
                }
            }
        }

        private static void RegisterImmediate(IRandomEventListener listener)
        {
            if (ContainsImmediate(listener) || _listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount].Listener = listener;
            _listenerCount++;
        }

        private static bool TryUnregisterImmediate(IRandomEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IRandomEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private struct ListenerSlot
        {
            public IRandomEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class RandomEventSystem : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        public const int EventTypeCount = 7;
        private static int s_x001RandomEventSystemSignalPushDropCount;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private TectonicActivityProfile tectonicActivityProfile;

        [Header("── Event Probabilities (per SlowTick) ──────")]
        [SerializeField, Range(0f, 0.01f)] private float biolumStormChance    = 0.001f;
        [SerializeField, Range(0f, 0.01f)] private float thermalEruptionChance = 0.0005f;
        [SerializeField, Range(0f, 0.02f)] private float faunaMigrationChance  = 0.002f;
        [SerializeField, Range(0f, 0.01f)] private float glitchChance          = 0.0008f;
        [SerializeField, Range(0f, 0.005f)] private float caveCollapseChance   = 0.0003f;
        [SerializeField, Range(0f, 0.001f)] private float meteorShowerChance   = 0.00012f;
        [SerializeField, Range(0f, 0.001f)] private float solarFlareChance      = 0.00008f;

        [Header("── Event Durations (seconds) ───────────────")]
        [SerializeField] private float biolumStormDuration    = 120f;
        [SerializeField] private float thermalEruptionDuration = 30f;
        [SerializeField] private float faunaMigrationDuration  = 180f;
        [SerializeField] private float glitchDuration          = 15f;
        [SerializeField] private float caveCollapseDuration    = 5f;
        [SerializeField] private float meteorShowerDuration    = 45f;
        [SerializeField] private float solarFlareDuration      = 30f;

        [Header("── Seismic Collapse ───────────────────────")]
        [SerializeField, Min(4f)] private float seismicTargetRadius = 72f;
        [SerializeField, Range(16, 64)] private int seismicOverlapCapacity = 64;
        [SerializeField, Range(16, 128)] private int seismicUniqueBodyCapacity = 48;

        [Header("── Meteor Shower ─────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float meteorShowerIntensity = 0.82f;
        [SerializeField, Range(0.5f, 8f)] private float meteorShowerFlashRate = 2.1f;
        [SerializeField, Range(0.5f, 8f)] private float meteorShowerFadeSeconds = 3f;
        [SerializeField] private Vector2 meteorShowerSkyDirection = new Vector2(-0.82f, -0.38f);
        [SerializeField, Range(0.02f, 0.45f)] private float meteorShowerStreakLength = 0.18f;
        [SerializeField, Range(0.0005f, 0.02f)] private float meteorShowerStreakWidth = 0.0035f;
        [SerializeField, Range(0f, 1f)] private float meteorBoomFlashThreshold = 0.62f;
        [SerializeField, Range(0f, 1f)] private float meteorBoomIntensity = 0.74f;
        [SerializeField, Range(80f, 800f)] private float meteorBoomLowPassCutoffHz = 260f;
        [SerializeField, Range(4f, 36f)] private float meteorBoomVerticalOffsetMeters = 18f;
        [SerializeField, Range(0f, 32f)] private float meteorBoomHorizontalOffsetMeters = 14f;
        [SerializeField, Range(4f, 96f)] private float meteorWaterImpactRadiusMeters = 42f;
        [SerializeField, Range(0.5f, 12f)] private float meteorWaterImpactDurationSeconds = 5.5f;
        [SerializeField, Range(0f, 1f)] private float meteorWaterImpactEnvelopeThreshold = 0.18f;
        [SerializeField] private GameObject meteorWaterSplashPrefab;
        [SerializeField, Range(0, 32)] private int meteorWaterSplashPoolWarmupCount = 8;
        [SerializeField, Range(0f, 8f)] private float meteorWaterSplashPrefabLifetimeSeconds = 6f;

        [Header("Solar EMP Flare")]
        [SerializeField, Range(0f, 1f)] private float solarFlareIntensity = 1f;
        [SerializeField, Range(0f, 4f)] private float solarFlareRadiationExposurePerSecond = 1.25f;
        private const float ThermalEruptionBurnDurationSeconds = 6f;
        private const float ThermalEruptionBurnMagnitude = 1f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // Taymery aktivnyh sobytiy (0 = neaktivno)
        // COLD ALLOC: float[EventTypeCount] - active random-event timers - owner: RandomEventSystem
        private readonly float[] _eventTimers = new float[EventTypeCount];
        // COLD ALLOC: SpatialQueryHit[64] - registered shockwave contact buffer capped for SlowTick impulse routing - owner: RandomEventSystem
        private readonly SpatialQueryHit[] _seismicContacts = new SpatialQueryHit[64];
        // COLD ALLOC: Rigidbody[48] - reusable unique rigidbody buffer for cave-collapse impulse routing - owner: RandomEventSystem
        private readonly Rigidbody[] _seismicBodyBuffer = new Rigidbody[48];
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _registeredRuntime;
        private bool _hotSwapListenerRegistered;
        private ILocalizationTextReadModel _cachedLocalization;
        private IMeteorShowerAudioSink _cachedSpatialAudioManager;
        private IObjectPoolService _cachedObjectPool;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private HectonVoxelEngine _cachedVoxelEngine;
        private SargassumGlobalDragManager _cachedSargassumDrag;
        private IPhysicsService _cachedPhysicsService;
        private double _cachedUniverseTimeSeconds;
        private uint _eventRandomState = 0xA341316Cu;
        private float _meteorSeed = 99173f;
        private int _meteorLastBoomIndex = -1;
        private const float MeteorWaterPlaneY = 0f;
        private const float MeteorThunderSoundSpeedMetersPerSecond = HectonPhysicsContract.SoundSpeedAirMetersPerSecondConst;
        private const float InvSqrtTwo = 0.70710678118f;
        private const byte GlobalTimeSyncValidFlag = 1;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string MeteorSplashQuadVfxTypeName = "MeteorSplashQuadVfx";
        private static readonly List<MonoBehaviour> _meteorSplashValidationScratch = new List<MonoBehaviour>(8);
#endif
        private bool _pendingMeteorWaterBoom;
        private Vector3 _pendingMeteorWaterBoomPosition;
        private float _pendingMeteorWaterBoomTimer;
        private float _pendingMeteorWaterBoomIntensity;
        private bool _biolumStormGlobalDirty;
        private bool _glitchGlobalDirty;
        private bool _meteorShowerGlobalsDirty;
        private bool _meteorWaterImpactGlobalsDirty;
        private float _pendingBiolumStormGlobal;
        private float _pendingGlitchGlobal;
        private Vector4 _pendingMeteorShowerParams;
        private Vector4 _pendingMeteorShowerDirection;
        private Vector4 _pendingMeteorWaterImpactPosition;
        private Vector4 _pendingMeteorWaterImpactParams;
        [SerializeField] private float _debugMeteorFlash;

        // Shader IDs
        private static readonly int _ShaderBiolumStorm  = Shader.PropertyToID("_BiolumStormActive");
        private static readonly int _ShaderGlitchActive = Shader.PropertyToID("_HUDGlitchActive");
        private static readonly int _ShaderMeteorShowerParams = Shader.PropertyToID("_MeteorShowerParams");
        private static readonly int _ShaderMeteorShowerDirection = Shader.PropertyToID("_MeteorShowerDirection");
        private static readonly int _ShaderMeteorWaterImpactPosition = Shader.PropertyToID("_MeteorWaterImpactPosition");
        private static readonly int _ShaderMeteorWaterImpactParams = Shader.PropertyToID("_MeteorWaterImpactParams");

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterRuntime();
            TryRegister();
            TryRegisterLateFrame();

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterRuntime();

            // Sbrasyvaem vse aktivnye sobytiya
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] > 0f)
                {
                    _eventTimers[i] = 0f;
                    RandomEventEvents.TryRaiseEnded((RandomEventType)i);
                }
            }

            PublishBiolumStormGlobalImmediate(0f);
            PublishGlitchGlobalImmediate(0f);
            PublishMeteorShowerGlobalsImmediate(0f, 0f, 0f);
            PublishMeteorWaterImpactGlobalsImmediate(Vector3.zero, 0f, 0f, 0f);
            ClearPendingMeteorWaterBoom();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            TryUnregisterLateFrame();
            TryUnregisterRuntime();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.1f;
            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            TickMeteorWaterBoomDelay(dt);

            // Obnovlyaem taymery aktivnyh sobytiy
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] <= 0f) continue;

                _eventTimers[i] -= dt;
                if (_eventTimers[i] <= 0f)
                {
                    _eventTimers[i] = 0f;
                    OnEventEnd((RandomEventType)i);
                }
            }

            // Proveryaem usloviya dlya novyh sobytiy
            if (IsEventActive(RandomEventType.MeteorShower))
                TickMeteorShowerEvent(dt);
            if (IsEventActive(RandomEventType.SolarFlare))
                ApplySolarFlareRadiation(dt);

            TryTriggerBiolumStorm(depth);
            TryTriggerThermalEruption(depth);
            TryTriggerFaunaMigration();
            TryTriggerGlitch(depth);
            TryTriggerCaveCollapse(depth);
            TryTriggerMeteorShower();
            TryTriggerSolarFlare();
        }

        public void LateFrameTick()
        {
            FlushQueuedRandomEventVisuals();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterRuntime()
        {
            if (_registeredRuntime)
                return;
            if (!Application.isPlaying)
                return;

            GlobalRegistry.RegisterRandomEventRuntime(this);
            _registeredRuntime = GlobalRegistry.RandomEvents == this;
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
            if (_registeredLateFrame || !Application.isPlaying)
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

        private void TryUnregisterRuntime()
        {
            if (!_registeredRuntime)
                return;

            GlobalRegistry.UnregisterRandomEventRuntime(this);
            _registeredRuntime = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public bool IsEventActive(RandomEventType type)
            => _eventTimers[(int)type] > 0f;

        public float GetEventTimeRemaining(RandomEventType type)
            => math.max(0f, _eventTimers[(int)type]);

        public static float EvaluateMeteorFlashForSmoke(float eventAgeSeconds, float seed, float flashRate)
        {
            return RandomEventMeteorMath.EvaluateMeteorFlash(eventAgeSeconds, seed, flashRate);
        }

        public async Awaitable<bool> WarmMeteorSplashPoolAsync(
            IObjectPoolService objectPoolManager,
            double frameBudgetMilliseconds,
            CancellationToken cancellationToken)
        {
            if (objectPoolManager == null ||
                meteorWaterSplashPrefab == null ||
                meteorWaterSplashPoolWarmupCount <= 0)
            {
                return true;
            }

            ValidateMeteorSplashPrefabForCinematicFake(meteorWaterSplashPrefab);
            return await objectPoolManager.WarmupPrefabAsync(
                meteorWaterSplashPrefab,
                meteorWaterSplashPoolWarmupCount,
                frameBudgetMilliseconds,
                cancellationToken);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — EVENT TRIGGERS
        // ══════════════════════════════════════════════════════════

        private void TryTriggerBiolumStorm(float depth)
        {
            if (IsEventActive(RandomEventType.BiolumStorm)) return;
            if (depth < 1000f) return;
            if (NextEventRandom01() > biolumStormChance) return;

            StartEvent(RandomEventType.BiolumStorm, biolumStormDuration, 0.8f);
            PublishBiolumStormGlobal(1f);
            NotificationEvents.TryPushInfo(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_BIOLUM_STORM,
                "BIOLUMINESCENT STORM - VISIBILITY +30%. FAUNA AGITATED."));
        }

        private void TryTriggerThermalEruption(float depth)
        {
            if (IsEventActive(RandomEventType.ThermalEruption)) return;
            if (depth < 3000f) return; // Tolko v riftovyh zonah
            if (NextEventRandom01() > thermalEruptionChance) return;

            StartEvent(RandomEventType.ThermalEruption, thermalEruptionDuration, 1f);
            NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_THERMAL_ERUPTION,
                "THERMAL ERUPTION - BURN HAZARD. RARE MINERALS EXPOSED."));

            QueueThermalEruptionBurnStatus();
        }

        private void QueueThermalEruptionBurnStatus()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            int targetId = playerHealth != null
                ? CombatDamageRuntime.ResolveTargetId(playerHealth.gameObject)
                : (survivalSystem != null ? CombatDamageRuntime.ResolveTargetId(survivalSystem.gameObject) : 0);

            if (targetId == 0 || !CombatDamageRuntime.IsTargetRegistered(targetId))
                return;

            CombatDamageRuntime.TryQueueStatusEffect(
                targetId,
                CombatStatusBits.Burning64,
                ThermalEruptionBurnDurationSeconds,
                DamageSourceIds.EnvironmentHazard,
                ThermalEruptionBurnMagnitude);
        }

        private void TryTriggerFaunaMigration()
        {
            if (IsEventActive(RandomEventType.FaunaMigration)) return;
            if (NextEventRandom01() > faunaMigrationChance) return;

            StartEvent(RandomEventType.FaunaMigration, faunaMigrationDuration, 0.5f);
            NotificationEvents.TryPushInfo(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_FAUNA_MIGRATION,
                "PACK MIGRATION - FAUNA BEHAVIOR SHIFT DETECTED."));
        }

        private void TryTriggerGlitch(float depth)
        {
            if (IsEventActive(RandomEventType.HectonOSGlitch)) return;
            if (depth < 500f) return;
            if (NextEventRandom01() > glitchChance) return;

            StartEvent(RandomEventType.HectonOSGlitch, glitchDuration, 0.6f);
            PublishGlitchGlobal(1f);
            NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_HECTON_OS_GLITCH,
                "HECTON-OS GLITCH - RADIATION INTERFERENCE. READINGS MAY BE INACCURATE."));
        }

        private void TryTriggerCaveCollapse(float depth)
        {
            if (IsEventActive(RandomEventType.CaveCollapse)) return;
            if (depth < 200f) return;
            if (!TryResolveSeismicContext(
                    out Vector3 playerPosition,
                    out AbsoluteUniversePosition playerAup,
                    out HectonVoxelVolume targetVolume,
                    out TectonicActivityProfile.SeismicEventSettings settings))
            {
                return;
            }

            float resolvedChance = caveCollapseChance * settings.collapseChanceMultiplier;
            if (NextEventRandom01() > math.saturate(resolvedChance)) return;
            if (!TryExecuteSeismicShockwave(playerPosition, in playerAup, targetVolume, settings, out SeismicShockwaveEvent seismicEvent))
                return;

            StartEvent(RandomEventType.CaveCollapse, caveCollapseDuration, 1f);
            RandomEventEvents.TryRaiseSeismicShockwave(in seismicEvent);
            NotificationEvents.TryPushWarning(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_CAVE_COLLAPSE,
                "CAVE COLLAPSE - ROUTE BLOCKED. POSSIBLE NEW OPENING."));
        }

        private void TryTriggerMeteorShower()
        {
            if (IsEventActive(RandomEventType.MeteorShower)) return;
            if (NextEventRandom01() > meteorShowerChance) return;

            BeginMeteorShower();
            StartEvent(RandomEventType.MeteorShower, meteorShowerDuration, meteorShowerIntensity);
            NotificationEvents.TryPushInfo(ResolveLocalizedSpan(
                LocalizationKeys.RANDOM_EVENT_METEOR_SHOWER,
                "METEOR SHOWER - SKY FLASHES DETECTED. LOW-FREQUENCY ACOUSTIC BOOMS EXPECTED."));
        }

        private void TryTriggerSolarFlare()
        {
            if (IsEventActive(RandomEventType.SolarFlare)) return;
            if (NextEventRandom01() > solarFlareChance) return;

            StartEvent(RandomEventType.SolarFlare, solarFlareDuration, solarFlareIntensity);
            NotificationEvents.TryPushWarning("SOLAR FLARE - ELECTROMAGNETIC PULSE DETECTED. BASE POWER COLLAPSE EXPECTED.".AsSpan());
        }

        private void StartEvent(RandomEventType type, float duration, float intensity)
        {
            _eventTimers[(int)type] = duration;
            RandomEventEvents.TryRaiseStarted(type, intensity);

            LogEventStarted(type, duration, intensity);
        }

        private void OnEventEnd(RandomEventType type)
        {
            RandomEventEvents.TryRaiseEnded(type);

            // Sbrasyvaem sheydernye effekty
            switch (type)
            {
                case RandomEventType.BiolumStorm:
                    PublishBiolumStormGlobal(0f);
                    break;
                case RandomEventType.HectonOSGlitch:
                    PublishGlitchGlobal(0f);
                    break;
                case RandomEventType.MeteorShower:
                    PublishMeteorShowerGlobals(0f, 0f, 0f);
                    _meteorLastBoomIndex = -1;
                    break;
            }

            LogEventEnded(type);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventStarted(RandomEventType type, float duration, float intensity)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[RandomEvent] Started");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventEnded(RandomEventType type)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.Log("[RandomEvent] Ended");
#endif
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            survivalSystem = playerContext != null ? playerContext.SurvivalSystem : null;
            return survivalSystem != null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregister();
                    TryUnregisterLateFrame();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegister();
                        TryRegisterLateFrame();
                    }
                    break;
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    _cachedLocalization = currentService as ILocalizationTextReadModel;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    CacheMeteorShowerAudioSink(currentService);
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _cachedObjectPool = currentService as IObjectPoolService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                    if (_cachedPlayerContext == null)
                        survivalSystem = null;
                    else if (survivalSystem == null)
                        survivalSystem = _cachedPlayerContext.SurvivalSystem;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _cachedPhysicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    _cachedVoxelEngine = currentService as HectonVoxelEngine;
                    if (ReferenceEquals(voxelEngine, previousService) || voxelEngine == null)
                        voxelEngine = _cachedVoxelEngine;
                    break;
                case GlobalRegistryServiceSlot.SargassumDragRuntime:
                    _cachedSargassumDrag = currentService as SargassumGlobalDragManager;
                    break;
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedLocalization = GlobalRegistry.LocalizationText;
            CacheMeteorShowerAudioSink(GlobalRegistry.Audio);
            _cachedObjectPool = GlobalRegistry.ObjectPoolService;
            _cachedPlayerContext = GlobalRegistry.Player;
            _cachedVoxelEngine = GlobalRegistry.VoxelEngine;
            _cachedSargassumDrag = GlobalRegistry.SargassumDrag;
            _cachedPhysicsService = GlobalRegistry.Physics;

            if (voxelEngine == null)
                voxelEngine = _cachedVoxelEngine;
            if (survivalSystem == null && _cachedPlayerContext != null)
                survivalSystem = _cachedPlayerContext.SurvivalSystem;
        }

        private void CacheMeteorShowerAudioSink(object audioRuntime)
        {
            _cachedSpatialAudioManager = IsAudioRuntimeObjectUsable(audioRuntime)
                ? audioRuntime as IMeteorShowerAudioSink
                : null;
        }

        private IMeteorShowerAudioSink ResolveMeteorShowerAudioSink()
        {
            IMeteorShowerAudioSink spatialAudioManager = _cachedSpatialAudioManager;
            if (IsAudioRuntimeObjectUsable(spatialAudioManager))
                return spatialAudioManager;

            _cachedSpatialAudioManager = null;
            return null;
        }

        private static bool IsAudioRuntimeObjectUsable(object runtime)
        {
            if (runtime == null)
                return false;

            if (runtime is IAudioService audioService && !audioService.IsInitialized)
                return false;

            if (runtime is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }

        private uint NextEventRandomState()
        {
            uint state = _eventRandomState;
            if (state == 0u)
                state = 0xA341316Cu;

            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            _eventRandomState = state != 0u ? state : 0x9E3779B9u;
            return _eventRandomState;
        }

        private float NextEventRandom01()
        {
            return (NextEventRandomState() & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private int NextEventRandomRange(int minInclusive, int maxExclusive)
        {
            int span = maxExclusive - minInclusive;
            if (span <= 0)
                return minInclusive;

            return minInclusive + (int)(NextEventRandomState() % (uint)span);
        }

        private void BeginMeteorShower()
        {
            _meteorSeed = BuildMeteorAupTimeSeed();
            _meteorLastBoomIndex = -1;
            PublishMeteorShowerGlobals(0f, math.saturate(meteorShowerIntensity), 1f);
        }

        private int BuildMeteorAupTimeSeed()
        {
            uint aupSeed = 0u;
            if (TryResolvePlayerEventFrame(out _, out AbsoluteUniversePosition observerAup))
                aupSeed = ResolveAupSeed(in observerAup);

            RefreshUniverseTimeSignalCache();
            double universeTime = ReadCachedUniverseTimeSeconds();

            uint timeSeed = unchecked((uint)(long)math.floor(universeTime * 0.25d) * 747796405u);
            uint state = NextMeteorLcg(timeSeed ^ aupSeed ^ 0x4D45544Fu);
            return (int)((state & 0x00FFFFFFu) + 1u);
        }

        private static uint ResolveAupSeed(in AbsoluteUniversePosition aup)
        {
            unchecked
            {
                uint state = (uint)aup.GridX * 2246822519u;
                state ^= (uint)aup.GridY * 3266489917u;
                state ^= (uint)aup.GridZ * 668265263u;
                state ^= (uint)(int)math.round(aup.LocalX * 4f) * 374761393u;
                state ^= (uint)(int)math.round(aup.LocalY * 4f) * 1103515245u;
                state ^= (uint)(int)math.round(aup.LocalZ * 4f) * 1274126177u;
                state ^= state >> 16;
                state *= 2246822519u;
                state ^= state >> 13;
                state *= 3266489917u;
                state ^= state >> 16;
                return state;
            }
        }

        private static uint NextMeteorLcg(uint state)
        {
            return unchecked((state * 1664525u) + 1013904223u);
        }

        private void TickMeteorShowerEvent(float dt)
        {
            float remaining = GetEventTimeRemaining(RandomEventType.MeteorShower);
            float safeDuration = math.max(0.01f, meteorShowerDuration);
            float eventAge = math.max(0f, safeDuration - remaining);
            float fadeWindow = math.max(0.01f, meteorShowerFadeSeconds);
            float fadeIn = math.saturate(eventAge / fadeWindow);
            float fadeOut = math.saturate(remaining / fadeWindow);
            float envelope = math.saturate(meteorShowerIntensity) * math.min(fadeIn, fadeOut);
            float flash = EvaluateMeteorFlashForSmoke(eventAge, _meteorSeed, meteorShowerFlashRate);
            _debugMeteorFlash = flash;
            PublishMeteorShowerGlobals(eventAge, envelope, flash);
            TryPublishMeteorBoom(eventAge, flash, envelope);
        }

        private void PublishMeteorShowerGlobals(float eventAge, float intensity, float flash)
        {
            Vector2 skyDirection = ResolveMeteorSkyDirection();
            _pendingMeteorShowerParams = new Vector4(
                math.saturate(intensity),
                _meteorSeed,
                math.saturate(flash),
                math.max(0f, eventAge));
            _pendingMeteorShowerDirection = new Vector4(
                skyDirection.x,
                skyDirection.y,
                math.max(0.02f, meteorShowerStreakLength),
                math.max(0.0005f, meteorShowerStreakWidth));
            _meteorShowerGlobalsDirty = true;
        }

        private Vector2 ResolveMeteorSkyDirection()
        {
            Vector2 direction = meteorShowerSkyDirection;
            float2 direction2 = new float2(direction.x, direction.y);
            float magnitudeSqr = math.lengthsq(direction2);
            if (magnitudeSqr < 0.0001f)
                direction = new Vector2(-0.82f, -0.38f);
            else
                direction = new Vector2(direction2.x, direction2.y) * math.rsqrt(magnitudeSqr);

            return direction;
        }

        private void TryPublishMeteorBoom(float eventAge, float flash, float envelope)
        {
            if (flash < meteorBoomFlashThreshold || envelope <= 0.001f)
                return;

            int boomIndex = (int)math.floor(eventAge * math.max(0.1f, meteorShowerFlashRate));
            if (boomIndex == _meteorLastBoomIndex)
                return;

            _meteorLastBoomIndex = boomIndex;
            IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink();
            if (spatialAudioManager == null)
                return;

            if (!TryResolvePlayerEventFrame(out Vector3 playerRuntimePosition, out AbsoluteUniversePosition playerAup))
                return;

            Vector3 sourcePosition = ResolveMeteorBoomPosition(playerRuntimePosition, boomIndex);
            spatialAudioManager.PlayMeteorShowerBoom(
                sourcePosition,
                math.saturate(flash * envelope * meteorBoomIntensity),
                meteorBoomLowPassCutoffHz);
            TryPublishMeteorWaterImpact(sourcePosition, playerRuntimePosition, in playerAup, flash, envelope);
        }

        private Vector3 ResolveMeteorBoomPosition(Vector3 playerPosition, int boomIndex)
        {
            Vector3 horizontal = ResolveMeteorBoomDirection(boomIndex, unchecked((uint)(int)math.round(_meteorSeed)));
            return playerPosition
                 + horizontal * math.max(0f, meteorBoomHorizontalOffsetMeters)
                 + Vector3.up * math.max(4f, meteorBoomVerticalOffsetMeters);
        }

        private static Vector3 ResolveMeteorBoomDirection(int boomIndex, uint meteorSeed)
        {
            uint state = unchecked(((uint)boomIndex * 747796405u) ^ (meteorSeed * 2891336453u) ^ 0x9E3779B9u);
            state ^= state >> 16;
            switch (state & 7u)
            {
                case 0u: return new Vector3(1f, 0f, 0f);
                case 1u: return new Vector3(InvSqrtTwo, 0f, InvSqrtTwo);
                case 2u: return new Vector3(0f, 0f, 1f);
                case 3u: return new Vector3(-InvSqrtTwo, 0f, InvSqrtTwo);
                case 4u: return new Vector3(-1f, 0f, 0f);
                case 5u: return new Vector3(-InvSqrtTwo, 0f, -InvSqrtTwo);
                case 6u: return new Vector3(0f, 0f, -1f);
                default: return new Vector3(InvSqrtTwo, 0f, -InvSqrtTwo);
            }
        }

        private void TryPublishMeteorWaterImpact(
            Vector3 meteorSourcePosition,
            Vector3 observerRuntimePosition,
            in AbsoluteUniversePosition observerAup,
            float flash,
            float envelope)
        {
            float impactEnvelope = math.saturate(flash * envelope);
            if (impactEnvelope < meteorWaterImpactEnvelopeThreshold)
                return;

            float seaLevelY = ResolveCurrentSeaLevelY();
            if (meteorSourcePosition.y < seaLevelY)
                return;

            Vector3 impactPosition = new Vector3(meteorSourcePosition.x, seaLevelY, meteorSourcePosition.z);
            if (!TryResolveOffsetAupFromRuntimeDelta(
                    impactPosition,
                    observerRuntimePosition,
                    in observerAup,
                    out AbsoluteUniversePosition impactAup))
            {
                return;
            }

            float radius = math.max(4f, meteorWaterImpactRadiusMeters);
            float duration = math.max(0.5f, meteorWaterImpactDurationSeconds);
            PublishMeteorWaterImpactGlobals(impactPosition, radius, duration, impactEnvelope);
            PublishMeteorSplashFeedback(impactPosition, in impactAup, radius, impactEnvelope);
            SpawnMeteorWaterSplashPrefab(impactPosition);
            TryApplyMeteorVoxelImpact(impactPosition, radius, impactEnvelope);
            QueueMeteorWaterBoom(impactPosition, in impactAup, in observerAup, impactEnvelope);

            SargassumGlobalDragManager sargassumDrag = _cachedSargassumDrag;
            if (sargassumDrag != null)
                sargassumDrag.RegisterMassiveDisplacement(impactPosition, radius, duration);
        }

        private void TryApplyMeteorVoxelImpact(Vector3 impactPosition, float waterImpactRadius, float intensity)
        {
            if (voxelEngine == null)
                voxelEngine = _cachedVoxelEngine;
            if (voxelEngine == null)
                return;
            if (!voxelEngine.TryGetNearestActiveVolume(impactPosition, out HectonVoxelVolume targetVolume) ||
                targetVolume == null)
            {
                return;
            }

            float craterRadius = ResolveMeteorVoxelCraterRadius(waterImpactRadius, intensity);
            targetVolume.TryApplyExtraterrestrialImpactCrater(impactPosition, craterRadius);
        }

        private static float ResolveMeteorVoxelCraterRadius(float waterImpactRadius, float intensity)
        {
            float horizontalAxis = math.max(2f, waterImpactRadius);
            float verticalAxis = horizontalAxis * 0.45f;
            float axisWeightedRadius = horizontalAxis + horizontalAxis * 0.375f + verticalAxis * 0.25f;
            return math.max(2f, axisWeightedRadius * math.lerp(0.08f, 0.18f, math.saturate(intensity)));
        }

        private static void PublishMeteorSplashFeedback(
            Vector3 impactPosition,
            in AbsoluteUniversePosition impactAup,
            float radius,
            float intensity)
        {
            double3 absoluteUniversePosition = impactAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(absoluteUniversePosition)))
                return;

            float clampedIntensity = math.saturate(intensity);
            SplashEvent splashEvent = new SplashEvent
            {
                RuntimePosition = new float3(impactPosition.x, impactPosition.y, impactPosition.z),
                AbsoluteUniversePosition = new float3(
                    (float)absoluteUniversePosition.x,
                    (float)absoluteUniversePosition.y,
                    (float)absoluteUniversePosition.z),
                SurfaceNormal = new float3(0f, 1f, 0f),
                ImpactSpeedMetersPerSecond = math.lerp(18f, 54f, clampedIntensity),
                KineticEnergyJoules = radius * radius * math.lerp(480f, 3200f, clampedIntensity),
                SubmersionFactor = 1f,
                SampleIndex = -1
            };
            SignalBus<SplashEvent>.TryPushTracked(in splashEvent, ref s_x001RandomEventSystemSignalPushDropCount);
        }

        private void SpawnMeteorWaterSplashPrefab(Vector3 impactPosition)
        {
            if (meteorWaterSplashPrefab == null)
                return;

            IObjectPoolService pool = _cachedObjectPool;
            if (pool == null)
                return;

            GameObject instance = pool.Spawn(meteorWaterSplashPrefab, impactPosition, Quaternion.identity, false);
            if (instance != null && meteorWaterSplashPrefabLifetimeSeconds > 0f)
                pool.Despawn(instance, meteorWaterSplashPrefabLifetimeSeconds);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void ValidateMeteorSplashPrefabForCinematicFake(GameObject prefab)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (prefab == null)
                return;

            if (ComponentReferenceUtility.ResolveOwnedComponent<ParticleSystem>(prefab.transform) != null)
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[RandomEventSystem] Meteor splash prefab contains ParticleSystem. Replace with MeteorSplashQuadVfx two-quad DrawMeshInstanced fake.",
                    prefab);
            }

            if (!HasMeteorSplashQuadVfx(prefab))
            {
                Hecton8.Core.H8Debug.LogWarning(
                    "[RandomEventSystem] Meteor splash prefab has no MeteorSplashQuadVfx. Splash pool is prewarmed, but the asset is not the two-quad cinematic fake.",
                    prefab);
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool HasMeteorSplashQuadVfx(GameObject prefab)
        {
            if (prefab == null)
                return false;

            _meteorSplashValidationScratch.Clear();
            prefab.GetComponentsInChildren(true, _meteorSplashValidationScratch);
            for (int i = 0; i < _meteorSplashValidationScratch.Count; i++)
            {
                MonoBehaviour behaviour = _meteorSplashValidationScratch[i];
                if (behaviour != null && behaviour.GetType().Name == MeteorSplashQuadVfxTypeName)
                {
                    _meteorSplashValidationScratch.Clear();
                    return true;
                }
            }

            _meteorSplashValidationScratch.Clear();
            return false;
        }
#endif

        private void QueueMeteorWaterBoom(
            Vector3 impactPosition,
            in AbsoluteUniversePosition impactAup,
            in AbsoluteUniversePosition observerAup,
            float intensity)
        {
            _pendingMeteorWaterBoom = true;
            _pendingMeteorWaterBoomPosition = impactPosition;
            _pendingMeteorWaterBoomTimer = ResolveMeteorThunderDelaySeconds(in impactAup, in observerAup);
            _pendingMeteorWaterBoomIntensity = math.saturate(intensity * meteorBoomIntensity);
        }

        private static float ResolveMeteorThunderDelaySeconds(
            in AbsoluteUniversePosition impactAup,
            in AbsoluteUniversePosition observerAup)
        {
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in impactAup, in observerAup);
            if (!math.isfinite(distanceSq))
                return 0f;

            double distanceMeters = distanceSq * math.rsqrt(math.max(0.000001d, distanceSq));
            return (float)(distanceMeters / MeteorThunderSoundSpeedMetersPerSecond);
        }

        private bool TryResolvePlayerEventFrame(out Vector3 runtimePosition, out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot pose))
            {
                playerAup = pose.Aup;
                if (!IsFiniteAup(in playerAup))
                {
                    runtimePosition = default;
                    playerAup = default;
                    return false;
                }

                float3 runtime = pose.RuntimePosition;
                runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                return true;
            }

            runtimePosition = default;
            playerAup = default;
            return false;
        }

        private void ApplySolarFlareRadiation(float dt)
        {
            if (!TryResolvePlayerEventFrame(out _, out AbsoluteUniversePosition playerAup))
                return;

            float exposureStep = math.max(0f, dt) *
                                 math.saturate(solarFlareIntensity) *
                                 math.max(0f, solarFlareRadiationExposurePerSecond);
            if (exposureStep <= 0f)
                return;

            RadiationHazardGrid.ReportExternalDose(
                exposureStep,
                math.saturate(solarFlareIntensity),
                in playerAup);
        }

        private void ClearPendingMeteorWaterBoom()
        {
            _pendingMeteorWaterBoom = false;
            _pendingMeteorWaterBoomPosition = Vector3.zero;
            _pendingMeteorWaterBoomTimer = 0f;
            _pendingMeteorWaterBoomIntensity = 0f;
        }

        private void TickMeteorWaterBoomDelay(float dt)
        {
            if (!_pendingMeteorWaterBoom)
                return;

            _pendingMeteorWaterBoomTimer -= math.max(0f, dt);
            if (_pendingMeteorWaterBoomTimer > 0f)
                return;

            Vector3 boomPosition = _pendingMeteorWaterBoomPosition;
            float boomIntensity = _pendingMeteorWaterBoomIntensity;
            ClearPendingMeteorWaterBoom();
            IMeteorShowerAudioSink spatialAudioManager = ResolveMeteorShowerAudioSink();
            if (spatialAudioManager == null)
                return;

            spatialAudioManager.PlayMeteorShowerBoom(
                boomPosition,
                boomIntensity,
                meteorBoomLowPassCutoffHz);
        }

        private static float ResolveCurrentSeaLevelY()
        {
            return MeteorWaterPlaneY;
        }

        private void PublishMeteorWaterImpactGlobals(Vector3 impactPosition, float radius, float duration, float intensity)
        {
            _pendingMeteorWaterImpactPosition = new Vector4(impactPosition.x, impactPosition.y, impactPosition.z, math.saturate(intensity));
            _pendingMeteorWaterImpactParams = new Vector4(
                math.max(0f, radius),
                math.max(0f, duration),
                0f,
                math.saturate(intensity));
            _meteorWaterImpactGlobalsDirty = true;
        }

        private void PublishBiolumStormGlobal(float value)
        {
            _pendingBiolumStormGlobal = math.saturate(value);
            _biolumStormGlobalDirty = true;
        }

        private void PublishGlitchGlobal(float value)
        {
            _pendingGlitchGlobal = math.saturate(value);
            _glitchGlobalDirty = true;
        }

        private bool HasQueuedRandomEventVisuals()
        {
            return _biolumStormGlobalDirty ||
                   _glitchGlobalDirty ||
                   _meteorShowerGlobalsDirty ||
                   _meteorWaterImpactGlobalsDirty;
        }

        private void FlushQueuedRandomEventVisuals()
        {
            if (_biolumStormGlobalDirty)
            {
                _biolumStormGlobalDirty = false;
                PublishBiolumStormGlobalImmediate(_pendingBiolumStormGlobal);
            }

            if (_glitchGlobalDirty)
            {
                _glitchGlobalDirty = false;
                PublishGlitchGlobalImmediate(_pendingGlitchGlobal);
            }

            if (_meteorShowerGlobalsDirty)
            {
                _meteorShowerGlobalsDirty = false;
                Shader.SetGlobalVector(_ShaderMeteorShowerParams, _pendingMeteorShowerParams);
                Shader.SetGlobalVector(_ShaderMeteorShowerDirection, _pendingMeteorShowerDirection);
            }

            if (_meteorWaterImpactGlobalsDirty)
            {
                _meteorWaterImpactGlobalsDirty = false;
                _pendingMeteorWaterImpactParams.z = ResolveMeteorWaterImpactShaderClockSeconds();
                Shader.SetGlobalVector(_ShaderMeteorWaterImpactPosition, _pendingMeteorWaterImpactPosition);
                Shader.SetGlobalVector(_ShaderMeteorWaterImpactParams, _pendingMeteorWaterImpactParams);
            }
        }

        private static void PublishBiolumStormGlobalImmediate(float value)
        {
            Shader.SetGlobalFloat(_ShaderBiolumStorm, math.saturate(value));
        }

        private static void PublishGlitchGlobalImmediate(float value)
        {
            Shader.SetGlobalFloat(_ShaderGlitchActive, math.saturate(value));
        }

        private void PublishMeteorShowerGlobalsImmediate(float eventAge, float intensity, float flash)
        {
            Vector2 skyDirection = ResolveMeteorSkyDirection();
            Shader.SetGlobalVector(
                _ShaderMeteorShowerParams,
                new Vector4(
                    math.saturate(intensity),
                    _meteorSeed,
                    math.saturate(flash),
                    math.max(0f, eventAge)));
            Shader.SetGlobalVector(
                _ShaderMeteorShowerDirection,
                new Vector4(
                    skyDirection.x,
                    skyDirection.y,
                    math.max(0.02f, meteorShowerStreakLength),
                    math.max(0.0005f, meteorShowerStreakWidth)));
        }

        private static void PublishMeteorWaterImpactGlobalsImmediate(Vector3 impactPosition, float radius, float duration, float intensity)
        {
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactPosition,
                new Vector4(impactPosition.x, impactPosition.y, impactPosition.z, math.saturate(intensity)));
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactParams,
                new Vector4(
                    math.max(0f, radius),
                    math.max(0f, duration),
                    ResolveMeteorWaterImpactShaderClockSeconds(),
                    math.saturate(intensity)));
        }

        private static float ResolveMeteorWaterImpactShaderClockSeconds()
        {
            double now = SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (double.IsNaN(now) || double.IsInfinity(now) || now <= 0d)
                return 0f;

            return now >= float.MaxValue ? float.MaxValue : (float)now;
        }

        private bool TryResolveSeismicContext(
            out Vector3 playerPosition,
            out AbsoluteUniversePosition playerAup,
            out HectonVoxelVolume targetVolume,
            out TectonicActivityProfile.SeismicEventSettings settings)
        {
            playerPosition = default;
            playerAup = default;
            targetVolume = null;
            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(null, null)
                : default;

            if (!TryResolvePlayerEventFrame(out playerPosition, out playerAup))
                return false;
            if (voxelEngine == null)
                voxelEngine = _cachedVoxelEngine;

            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(playerPosition, out targetVolume) || targetVolume == null)
                return false;

            float maxTargetRadius = math.max(4f, seismicTargetRadius);
            if (IsAupDistanceGreater(targetVolume.GenerationAbsoluteUniversePositionDouble, in playerAup, maxTargetRadius))
                return false;

            string familyId = null;
            string geologyProfileId = null;
            if (WorldGenerativeGeologyVoxelRuntime.TryGetActiveRuntime(targetVolume, out WorldGenerativeGeologyVoxelRuntime runtime))
            {
                familyId = runtime.FamilyId;
                geologyProfileId = runtime.GeologyProfileId;
            }

            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(familyId, geologyProfileId)
                : new TectonicActivityProfile.SeismicEventSettings
                {
                    collapseChanceMultiplier = 1f,
                    stampCountMin = 2,
                    stampCountMax = 4,
                    stampScatterRadius = 18f,
                    ceilingSearchDepth = 18f,
                    craterRadiusMin = 2.5f,
                    craterRadiusMax = 6f,
                    impulseRadius = 100f,
                    impulseMagnitude = 14f
                }.Sanitize();
            return true;
        }

        private bool TryExecuteSeismicShockwave(
            Vector3 playerPosition,
            in AbsoluteUniversePosition playerAup,
            HectonVoxelVolume targetVolume,
            TectonicActivityProfile.SeismicEventSettings settings,
            out SeismicShockwaveEvent seismicEvent)
        {
            seismicEvent = default;
            if (targetVolume == null)
                return false;
            if (!IsFiniteAup(in playerAup))
                return false;

            int stampCount = NextEventRandomRange(settings.stampCountMin, settings.stampCountMax + 1);
            uint stableSeed = BuildAupTimelineSeed(in playerAup, targetVolume.RuntimeStamp);
            if (!targetVolume.TryApplySeismicShockwave(
                    playerPosition,
                    stampCount,
                    settings.stampScatterRadius,
                    settings.ceilingSearchDepth,
                    settings.craterRadiusMin,
                    settings.craterRadiusMax,
                    stableSeed,
                    out int appliedStampCount))
            {
                return false;
            }

            double3 epicenterAup = playerAup.ToAbsoluteDouble3();
            if (!math.all(math.isfinite(epicenterAup)))
                return false;

            ApplySeismicImpulse(playerPosition, in playerAup, settings.impulseRadius, settings.impulseMagnitude);
            seismicEvent = new SeismicShockwaveEvent(
                playerPosition,
                settings.impulseRadius,
                settings.impulseMagnitude,
                appliedStampCount);
            return true;
        }

        private uint BuildAupTimelineSeed(in AbsoluteUniversePosition aup, int runtimeStamp)
        {
            RefreshUniverseTimeSignalCache();
            double universeTime = ReadCachedUniverseTimeSeconds();

            long timelineSlot = (long)math.floor(universeTime * 2d);
            unchecked
            {
                uint state = (uint)timelineSlot * 2654435761u;
                state ^= (uint)aup.GridX * 2246822519u;
                state ^= (uint)aup.GridY * 3266489917u;
                state ^= (uint)aup.GridZ * 668265263u;
                state ^= (uint)(int)math.round(aup.LocalX * 4f) * 374761393u;
                state ^= (uint)(int)math.round(aup.LocalZ * 4f) * 1274126177u;
                state ^= (uint)runtimeStamp * 2891336453u;
                state ^= state >> 16;
                state *= 2246822519u;
                state ^= state >> 13;
                state *= 3266489917u;
                state ^= state >> 16;
                return state != 0u ? state : 0x9E3779B9u;
            }
        }

        private void RefreshUniverseTimeSignalCache()
        {
            System.ReadOnlySpan<GlobalTimeSyncSignal> signals = SignalBus<GlobalTimeSyncSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                GlobalTimeSyncSignal signal = signals[i];
                double worldSeconds = signal.WorldSeconds;
                if ((signal.Flags & GlobalTimeSyncValidFlag) != 0 && math.isfinite(worldSeconds) && worldSeconds >= 0d)
                    _cachedUniverseTimeSeconds = worldSeconds;
            }
        }

        private double ReadCachedUniverseTimeSeconds()
        {
            double universeTime = _cachedUniverseTimeSeconds;
            return math.isfinite(universeTime) && universeTime >= 0d ? universeTime : 0d;
        }

        private static Vector3 ResolveSeismicEventLineDirection(double3 absoluteEpicenter, uint stableSeed)
        {
            uint seedA = FoldLongToUInt(FastRoundToLong(absoluteEpicenter.x * 0.25d));
            uint seedB = FoldLongToUInt(FastRoundToLong(absoluteEpicenter.z * 0.25d));
            uint state = unchecked(seedA * 747796405u + seedB * 2891336453u + stableSeed);
            state ^= state >> 16;
            state = unchecked(state * 2246822519u);
            state ^= state >> 13;
            state = unchecked(state * 3266489917u);
            state ^= state >> 16;

            switch (state & 7u)
            {
                case 0u: return new Vector3(1f, 0f, 0f);
                case 1u: return new Vector3(InvSqrtTwo, 0f, InvSqrtTwo);
                case 2u: return new Vector3(0f, 0f, 1f);
                case 3u: return new Vector3(-InvSqrtTwo, 0f, InvSqrtTwo);
                case 4u: return new Vector3(-1f, 0f, 0f);
                case 5u: return new Vector3(-InvSqrtTwo, 0f, -InvSqrtTwo);
                case 6u: return new Vector3(0f, 0f, -1f);
                default: return new Vector3(InvSqrtTwo, 0f, -InvSqrtTwo);
            }
        }

        private static uint FoldLongToUInt(long value)
        {
            unchecked
            {
                ulong bits = (ulong)value;
                return (uint)bits ^ (uint)(bits >> 32);
            }
        }

        private static long FastRoundToLong(double value)
        {
            if (!math.isfinite(value))
                return 0L;

            if (value >= long.MaxValue)
                return long.MaxValue;

            if (value <= long.MinValue)
                return long.MinValue;

            return value >= 0d
                ? (long)(value + 0.5d)
                : (long)(value - 0.5d);
        }

        private void ApplySeismicImpulse(
            Vector3 epicenter,
            in AbsoluteUniversePosition epicenterAup,
            float radius,
            float impulseMagnitude)
        {
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Bioform |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            int overlapCapacity = math.clamp(seismicOverlapCapacity, 16, _seismicContacts.Length);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                epicenter,
                math.max(1f, radius),
                kindMask,
                _seismicContacts);
            if (hitCount <= 0)
                return;

            int uniqueCapacity = math.clamp(seismicUniqueBodyCapacity, 16, _seismicBodyBuffer.Length);
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && hitIndex < overlapCapacity; hitIndex++)
            {
                SpatialQueryHit hit = _seismicContacts[hitIndex];
                _seismicContacts[hitIndex] = default;

                Rigidbody body = hit.Rigidbody;
                if (body == null || body.isKinematic)
                    continue;

                bool duplicate = false;
                for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
                {
                    if (_seismicBodyBuffer[bodyIndex] != body)
                        continue;

                    duplicate = true;
                    break;
                }

                if (duplicate)
                    continue;

                _seismicBodyBuffer[uniqueBodyCount++] = body;
                if (uniqueBodyCount >= uniqueCapacity)
                    break;
            }

            float safeRadius = math.max(1f, radius);
            for (int bodyIndex = 0; bodyIndex < uniqueBodyCount; bodyIndex++)
            {
                Rigidbody body = _seismicBodyBuffer[bodyIndex];
                _seismicBodyBuffer[bodyIndex] = null;
                if (body == null)
                    continue;

                ResolveAupDirectionAndDistance(
                    epicenter,
                    in epicenterAup,
                    body.worldCenterOfMass,
                    out Vector3 direction,
                    out float distance);
                if (distance > safeRadius)
                    continue;

                if (distance <= 0.0001f)
                    direction = Vector3.up;
                float3 impulseDirection = new float3(direction.x, math.max(direction.y, 0.25f), direction.z);
                float impulseDirectionLengthSq = math.lengthsq(impulseDirection);
                if (impulseDirectionLengthSq <= 0.0001f)
                {
                    direction = Vector3.up;
                }
                else
                {
                    impulseDirection *= math.rsqrt(impulseDirectionLengthSq);
                    direction = new Vector3(impulseDirection.x, impulseDirection.y, impulseDirection.z);
                }

                float distance01 = 1f - math.saturate(distance / safeRadius);
                float impulseFalloff = math.saturate(distance01 * math.rcp(0.55f + (0.45f * distance01)));
                float resolvedImpulse = impulseMagnitude * impulseFalloff;
                _cachedPhysicsService?.QueueForce(body, direction * resolvedImpulse, ForceMode.Impulse);
            }
        }

        private static bool IsAupDistanceGreater(
            double3 absoluteA,
            in AbsoluteUniversePosition bAup,
            float thresholdMeters)
        {
            float safeThreshold = math.max(0f, thresholdMeters);
            double3 absoluteB = bAup.ToAbsoluteDouble3();
            double3 delta = absoluteA - absoluteB;
            double distanceSq = math.dot(delta, delta);
            if (!math.isfinite(distanceSq))
                return true;

            return distanceSq > (double)safeThreshold * safeThreshold;
        }

        private static void ResolveAupDirectionAndDistance(
            Vector3 fromRuntime,
            in AbsoluteUniversePosition fromAup,
            Vector3 toRuntime,
            out Vector3 direction,
            out float distance)
        {
            if (!TryResolveOffsetAupFromRuntimeDelta(toRuntime, fromRuntime, in fromAup, out AbsoluteUniversePosition toAup))
            {
                direction = Vector3.up;
                distance = float.MaxValue;
                return;
            }

            double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in toAup, in fromAup);
            double distanceSq = math.dot(delta, delta);
            if (!math.isfinite(distanceSq))
            {
                direction = Vector3.up;
                distance = float.MaxValue;
                return;
            }

            if (distanceSq <= 0.000001d)
            {
                direction = Vector3.up;
                distance = 0f;
                return;
            }

            double resolvedDistance = distanceSq * math.rsqrt(distanceSq);
            distance = resolvedDistance > float.MaxValue ? float.MaxValue : (float)resolvedDistance;
            direction = resolvedDistance > 0.0001d
                ? new Vector3(
                    (float)(delta.x / resolvedDistance),
                    (float)(delta.y / resolvedDistance),
                    (float)(delta.z / resolvedDistance))
                : Vector3.up;
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition position)
        {
            return math.isfinite(position.LocalX) &&
                   math.isfinite(position.LocalY) &&
                   math.isfinite(position.LocalZ);
        }

        private static bool TryResolveOffsetAupFromRuntimeDelta(
            Vector3 targetRuntimePosition,
            Vector3 originRuntimePosition,
            in AbsoluteUniversePosition originAup,
            out AbsoluteUniversePosition targetAup)
        {
            targetAup = default;
            if (!IsFiniteAup(in originAup))
                return false;

            float3 targetRuntime = new float3(targetRuntimePosition.x, targetRuntimePosition.y, targetRuntimePosition.z);
            float3 originRuntime = new float3(originRuntimePosition.x, originRuntimePosition.y, originRuntimePosition.z);
            if (!math.all(math.isfinite(targetRuntime)) || !math.all(math.isfinite(originRuntime)))
                return false;

            double3 deltaMeters = new double3(
                (double)targetRuntimePosition.x - originRuntimePosition.x,
                (double)targetRuntimePosition.y - originRuntimePosition.y,
                (double)targetRuntimePosition.z - originRuntimePosition.z);
            targetAup = AbsoluteUniversePosition.OffsetMeters(in originAup, deltaMeters);
            return IsFiniteAup(in targetAup);
        }
    }
}
