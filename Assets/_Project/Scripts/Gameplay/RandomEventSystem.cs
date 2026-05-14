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

using System.Runtime.InteropServices;
using System.Threading;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
#endif
using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Modding;
using Hecton8.Physics;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct MeteorShowerEvent
    {
        public float DurationSeconds;
        public float Intensity;
        public int Seed;
        public float3 ObserverRuntimePosition;
        public long ObserverGridX;
        public long ObserverGridY;
        public long ObserverGridZ;
        public float3 ObserverLocalOffset;
        public byte HasObserverRuntimePosition;
        public byte HasObserverAup;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct SeismicShockwaveEvent
    {
        public readonly Vector3 EpicenterWS;
        public readonly float ImpulseRadiusMeters;
        public readonly float ImpulseMagnitude;
        public readonly int AppliedStampCount;
        public readonly Vector3 AupStart;
        public readonly Vector3 AupEnd;
        public readonly double3 AupStartDouble;
        public readonly double3 AupEndDouble;
        private readonly byte _hasAupLineSegment;

        public bool HasAupLineSegment => _hasAupLineSegment != 0;

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
            _hasAupLineSegment = hasAupLineSegment ? (byte)1 : (byte)0;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct RandomEventStartedPayload
    {
        /// <summary>Activated random-event type.</summary>
        public RandomEventType Type;

        /// <summary>Normalized authored event intensity.</summary>
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

        // COLD ALLOC: RegistryBucket<IRandomEventListener>[16] - deferred random event listeners - owner: RandomEventEvents
        private static readonly RegistryBucket<IRandomEventListener> _listeners = new RegistryBucket<IRandomEventListener>(ListenerCapacity);
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
            if (_pendingStarted.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingStarted));
                _pendingStarted.Dispose();
                _pendingStarted = default;
            }

            if (_nextFrameStarted.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameStarted));
                _nextFrameStarted.Dispose();
                _nextFrameStarted = default;
            }

            if (_pendingEnded.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingEnded));
                _pendingEnded.Dispose();
                _pendingEnded = default;
            }

            if (_nextFrameEnded.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameEnded));
                _nextFrameEnded.Dispose();
                _nextFrameEnded = default;
            }

            if (_pendingSeismicShockwaves.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_pendingSeismicShockwaves));
                _pendingSeismicShockwaves.Dispose();
                _pendingSeismicShockwaves = default;
            }

            if (_nextFrameSeismicShockwaves.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(RandomEventEvents), nameof(_nextFrameSeismicShockwaves));
                _nextFrameSeismicShockwaves.Dispose();
                _nextFrameSeismicShockwaves = default;
            }

            _pendingStartedCount = 0;
            _nextFrameStartedCount = 0;
            _pendingEndedCount = 0;
            _nextFrameEndedCount = 0;
            _pendingSeismicShockwaveCount = 0;
            _nextFrameSeismicShockwaveCount = 0;
            _isDispatching = false;
            _listeners.Clear();
        }

        public static void Register(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(IRandomEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            bool completed = false;
            _isDispatching = true;
            try
            {
                if (_listeners.Count <= 0)
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

        public static void RaiseStarted(RandomEventType type, float intensity)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingStartedCount + _nextFrameStartedCount >= PendingStartedCapacity)
                return;

            RandomEventStartedPayload payload = new RandomEventStartedPayload
            {
                Type = type,
                Intensity = intensity
            };

            if (_isDispatching)
            {
                _nextFrameStarted.Enqueue(payload);
                _nextFrameStartedCount++;
            }
            else
            {
                _pendingStarted.Enqueue(payload);
                _pendingStartedCount++;
            }
        }

        public static void RaiseEnded(RandomEventType type)
        {
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingEndedCount + _nextFrameEndedCount >= PendingEndedCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameEnded.Enqueue(type);
                _nextFrameEndedCount++;
            }
            else
            {
                _pendingEnded.Enqueue(type);
                _pendingEndedCount++;
            }
        }

        public static void RaiseSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(
                payload.EpicenterWS,
                math.max(payload.ImpulseRadiusMeters, payload.ImpulseRadiusMeters * 4f),
                math.saturate(payload.ImpulseMagnitude / 48f),
                8f,
                FieldTargetRole.HazardProbe,
                0,
                payload.ImpulseMagnitude * 1000f));
            if (_listeners.Count <= 0)
                return;

            EnsureInitialized();
            if (_pendingSeismicShockwaveCount + _nextFrameSeismicShockwaveCount >= PendingSeismicShockwaveCapacity)
                return;

            if (_isDispatching)
            {
                _nextFrameSeismicShockwaves.Enqueue(payload);
                _nextFrameSeismicShockwaveCount++;
            }
            else
            {
                _pendingSeismicShockwaves.Enqueue(payload);
                _pendingSeismicShockwaveCount++;
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingStarted.IsCreated)
            {
                _pendingStarted = new NativeQueue<RandomEventStartedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - deferred random-event starts - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingStarted,
                    PendingStartedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingStarted),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingStarted, PendingStartedCapacity);
            }
            if (!_nextFrameStarted.IsCreated)
            {
                _nextFrameStarted = new NativeQueue<RandomEventStartedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventStartedPayload>[16] - next-frame random-event starts - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameStarted,
                    PendingStartedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameStarted),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameStarted, PendingStartedCapacity);
            }
            if (!_pendingEnded.IsCreated)
            {
                _pendingEnded = new NativeQueue<RandomEventType>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventType>[16] - deferred random-event ends - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEnded,
                    PendingEndedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingEnded),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEnded, PendingEndedCapacity);
            }
            if (!_nextFrameEnded.IsCreated)
            {
                _nextFrameEnded = new NativeQueue<RandomEventType>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RandomEventType>[16] - next-frame random-event ends - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEnded,
                    PendingEndedCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameEnded),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEnded, PendingEndedCapacity);
            }
            if (!_pendingSeismicShockwaves.IsCreated)
            {
                _pendingSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - deferred seismic shockwaves - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingSeismicShockwaves,
                    PendingSeismicShockwaveCapacity,
                    nameof(RandomEventEvents),
                    nameof(_pendingSeismicShockwaves),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingSeismicShockwaves, PendingSeismicShockwaveCapacity);
            }
            if (!_nextFrameSeismicShockwaves.IsCreated)
            {
                _nextFrameSeismicShockwaves = new NativeQueue<SeismicShockwaveEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SeismicShockwaveEvent>[8] - next-frame seismic shockwaves - owner: RandomEventEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameSeismicShockwaves,
                    PendingSeismicShockwaveCapacity,
                    nameof(RandomEventEvents),
                    nameof(_nextFrameSeismicShockwaves),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameSeismicShockwaves, PendingSeismicShockwaveCapacity);
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
                    return true;

                _pendingStartedCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
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
                    return true;

                _pendingEndedCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
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
                    return true;

                _pendingSeismicShockwaveCount--;
                scanBudget--;
                IRandomEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                {
                    IRandomEventListener listener = rawArray[i];
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
                        return true;

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
                        return true;

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
                        return true;

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
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-70)]
    public sealed class RandomEventSystem : MonoBehaviour, ISlowTickable
    {
        public const int EventTypeCount = 7;

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

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        // Taymery aktivnyh sobytiy (0 = neaktivno)
        // COLD ALLOC: float[EventTypeCount] - active random-event timers - owner: RandomEventSystem
        private readonly float[] _eventTimers = new float[EventTypeCount];
        // COLD ALLOC: Collider[64] - reusable shockwave overlap buffer capped for SlowTick impulse routing - owner: RandomEventSystem
        private readonly Collider[] _seismicOverlapBuffer = new Collider[64];
        // COLD ALLOC: Rigidbody[48] - reusable unique rigidbody buffer for cave-collapse impulse routing - owner: RandomEventSystem
        private readonly Rigidbody[] _seismicBodyBuffer = new Rigidbody[48];
        private bool _registered;
        private bool _registeredRuntime;
        private uint _eventRandomState = 0xA341316Cu;
        private float _meteorSeed = 99173f;
        private int _meteorLastBoomIndex = -1;
        private const float MeteorWaterPlaneY = 0f;
        private const float MeteorThunderSoundSpeedMetersPerSecond = 343f;
        private const float InvSqrtTwo = 0.70710678118f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const string MeteorSplashQuadVfxTypeName = "MeteorSplashQuadVfx";
        private static readonly List<MonoBehaviour> _meteorSplashValidationScratch = new List<MonoBehaviour>(8);
#endif
        private bool _pendingMeteorWaterBoom;
        private Vector3 _pendingMeteorWaterBoomPosition;
        private float _pendingMeteorWaterBoomTimer;
        private float _pendingMeteorWaterBoomIntensity;
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
            TryRegisterRuntime();
            TryRegister();

            ResolveSurvivalSystem();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterRuntime();

            // Sbrasyvaem vse aktivnye sobytiya
            for (int i = 0; i < _eventTimers.Length; i++)
            {
                if (_eventTimers[i] > 0f)
                {
                    _eventTimers[i] = 0f;
                    RandomEventEvents.RaiseEnded((RandomEventType)i);
                }
            }

            Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
            PublishMeteorShowerGlobals(0f, 0f, 0f);
            PublishMeteorWaterImpactGlobals(Vector3.zero, 0f, 0f, 0f);
            ClearPendingMeteorWaterBoom();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterRuntime();
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            const float dt = 0.5f;
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

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
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
            ObjectPoolManager objectPoolManager,
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
            Shader.SetGlobalFloat(_ShaderBiolumStorm, 1f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_BIOLUM_STORM,
                "BIOLUMINESCENT STORM - VISIBILITY +30%. FAUNA AGITATED."));
        }

        private void TryTriggerThermalEruption(float depth)
        {
            if (IsEventActive(RandomEventType.ThermalEruption)) return;
            if (depth < 3000f) return; // Tolko v riftovyh zonah
            if (NextEventRandom01() > thermalEruptionChance) return;

            StartEvent(RandomEventType.ThermalEruption, thermalEruptionDuration, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_THERMAL_ERUPTION,
                "THERMAL ERUPTION - BURN HAZARD. RARE MINERALS EXPOSED."));

            // Uron oborudovaniyu
            if (survivalSystem != null)
                survivalSystem.TakeDamage(5f);
        }

        private void TryTriggerFaunaMigration()
        {
            if (IsEventActive(RandomEventType.FaunaMigration)) return;
            if (NextEventRandom01() > faunaMigrationChance) return;

            StartEvent(RandomEventType.FaunaMigration, faunaMigrationDuration, 0.5f);
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_FAUNA_MIGRATION,
                "PACK MIGRATION - FAUNA BEHAVIOR SHIFT DETECTED."));
        }

        private void TryTriggerGlitch(float depth)
        {
            if (IsEventActive(RandomEventType.HectonOSGlitch)) return;
            if (depth < 500f) return;
            if (NextEventRandom01() > glitchChance) return;

            StartEvent(RandomEventType.HectonOSGlitch, glitchDuration, 0.6f);
            Shader.SetGlobalFloat(_ShaderGlitchActive, 1f);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_HECTON_OS_GLITCH,
                "HECTON-OS GLITCH - RADIATION INTERFERENCE. READINGS MAY BE INACCURATE."));
        }

        private void TryTriggerCaveCollapse(float depth)
        {
            if (IsEventActive(RandomEventType.CaveCollapse)) return;
            if (depth < 200f) return;
            if (!TryResolveSeismicContext(
                    out Vector3 playerPosition,
                    out HectonVoxelVolume targetVolume,
                    out TectonicActivityProfile.SeismicEventSettings settings))
            {
                return;
            }

            float resolvedChance = caveCollapseChance * settings.collapseChanceMultiplier;
            if (NextEventRandom01() > math.saturate(resolvedChance)) return;
            if (!TryExecuteSeismicShockwave(playerPosition, targetVolume, settings, out SeismicShockwaveEvent seismicEvent))
                return;

            StartEvent(RandomEventType.CaveCollapse, caveCollapseDuration, 1f);
            RandomEventEvents.RaiseSeismicShockwave(in seismicEvent);
            NotificationEvents.PushWarning(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_CAVE_COLLAPSE,
                "CAVE COLLAPSE - ROUTE BLOCKED. POSSIBLE NEW OPENING."));
        }

        private void TryTriggerMeteorShower()
        {
            if (IsEventActive(RandomEventType.MeteorShower)) return;
            if (NextEventRandom01() > meteorShowerChance) return;

            BeginMeteorShower();
            StartEvent(RandomEventType.MeteorShower, meteorShowerDuration, meteorShowerIntensity);
            PublishMeteorShowerMegaBus();
            NotificationEvents.PushInfo(ResolveLocalized(
                LocalizationKeys.RANDOM_EVENT_METEOR_SHOWER,
                "METEOR SHOWER - SKY FLASHES DETECTED. LOW-FREQUENCY ACOUSTIC BOOMS EXPECTED."));
        }

        private void TryTriggerSolarFlare()
        {
            if (IsEventActive(RandomEventType.SolarFlare)) return;
            if (NextEventRandom01() > solarFlareChance) return;

            StartEvent(RandomEventType.SolarFlare, solarFlareDuration, solarFlareIntensity);
            NotificationEvents.PushWarning("SOLAR FLARE - ELECTROMAGNETIC PULSE DETECTED. BASE POWER COLLAPSE EXPECTED.");
        }

        private void StartEvent(RandomEventType type, float duration, float intensity)
        {
            _eventTimers[(int)type] = duration;
            RandomEventEvents.RaiseStarted(type, intensity);

            LogEventStarted(type, duration, intensity);
        }

        private void OnEventEnd(RandomEventType type)
        {
            RandomEventEvents.RaiseEnded(type);

            // Sbrasyvaem sheydernye effekty
            switch (type)
            {
                case RandomEventType.BiolumStorm:
                    Shader.SetGlobalFloat(_ShaderBiolumStorm, 0f);
                    break;
                case RandomEventType.HectonOSGlitch:
                    Shader.SetGlobalFloat(_ShaderGlitchActive, 0f);
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
            Debug.Log("[RandomEvent] Started");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogEventEnded(RandomEventType type)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log("[RandomEvent] Ended");
#endif
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null
                ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback)
                : fallback;
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
            _meteorSeed = ResolveMeteorAupTimeSeed();
            _meteorLastBoomIndex = -1;
            PublishMeteorShowerGlobals(0f, math.saturate(meteorShowerIntensity), 1f);
        }

        private static int ResolveMeteorAupTimeSeed()
        {
            uint aupSeed = 0u;
            if (TryResolvePlayerEventFrame(out _, out AbsoluteUniversePosition observerAup))
                aupSeed = ResolveAupSeed(in observerAup);

            double universeTime = GlobalRegistry.AbsoluteUniverseTime;
            if (!math.isfinite(universeTime) || universeTime < 0d)
                universeTime = 0d;

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

        private void PublishMeteorShowerMegaBus()
        {
            MeteorShowerEvent meteorEvent = default;
            meteorEvent.DurationSeconds = math.max(0f, meteorShowerDuration);
            meteorEvent.Intensity = math.saturate(meteorShowerIntensity);
            meteorEvent.Seed = (int)math.round(_meteorSeed);

            if (TryResolvePlayerEventFrame(out Vector3 observerRuntimePosition, out AbsoluteUniversePosition observerAup))
            {
                meteorEvent.ObserverRuntimePosition = new float3(observerRuntimePosition.x, observerRuntimePosition.y, observerRuntimePosition.z);
                meteorEvent.ObserverGridX = observerAup.GridX;
                meteorEvent.ObserverGridY = observerAup.GridY;
                meteorEvent.ObserverGridZ = observerAup.GridZ;
                meteorEvent.ObserverLocalOffset = new float3(observerAup.LocalX, observerAup.LocalY, observerAup.LocalZ);
                meteorEvent.HasObserverRuntimePosition = 1;
                meteorEvent.HasObserverAup = 1;
            }

            HectonEventBus.Publish(in meteorEvent);
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
            if (!(GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager))
                return;

            if (!TryResolvePlayerEventFrame(out Vector3 playerRuntimePosition, out AbsoluteUniversePosition playerAup))
                return;

            Vector3 sourcePosition = ResolveMeteorBoomPosition(playerRuntimePosition, boomIndex);
            spatialAudioManager.PlayMeteorShowerBoom(
                sourcePosition,
                math.saturate(flash * envelope * meteorBoomIntensity),
                meteorBoomLowPassCutoffHz);
            TryPublishMeteorWaterImpact(sourcePosition, in playerAup, flash, envelope);
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

        private void TryPublishMeteorWaterImpact(Vector3 meteorSourcePosition, in AbsoluteUniversePosition observerAup, float flash, float envelope)
        {
            float impactEnvelope = math.saturate(flash * envelope);
            if (impactEnvelope < meteorWaterImpactEnvelopeThreshold)
                return;

            float seaLevelY = ResolveCurrentSeaLevelY();
            if (meteorSourcePosition.y < seaLevelY)
                return;

            Vector3 impactPosition = new Vector3(meteorSourcePosition.x, seaLevelY, meteorSourcePosition.z);
            float radius = math.max(4f, meteorWaterImpactRadiusMeters);
            float duration = math.max(0.5f, meteorWaterImpactDurationSeconds);
            PublishMeteorWaterImpactGlobals(impactPosition, radius, duration, impactEnvelope);
            PublishMeteorSplashFeedback(impactPosition, radius, impactEnvelope);
            SpawnMeteorWaterSplashPrefab(impactPosition);
            TryApplyMeteorVoxelImpact(impactPosition, radius, impactEnvelope);
            QueueMeteorWaterBoom(impactPosition, in observerAup, impactEnvelope);

            SargassumGlobalDragManager sargassumDrag = GlobalRegistry.SargassumDrag;
            if (sargassumDrag != null)
                sargassumDrag.RegisterMassiveDisplacement(impactPosition, radius, duration);
        }

        private void TryApplyMeteorVoxelImpact(Vector3 impactPosition, float waterImpactRadius, float intensity)
        {
            if (voxelEngine == null)
                voxelEngine = GlobalRegistry.VoxelEngine;
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

        private static void PublishMeteorSplashFeedback(Vector3 impactPosition, float radius, float intensity)
        {
            double3 absoluteUniversePosition = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(impactPosition);
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
            FluidFeedbackEvents.PublishSplashQueued(in splashEvent);
        }

        private void SpawnMeteorWaterSplashPrefab(Vector3 impactPosition)
        {
            if (meteorWaterSplashPrefab == null)
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
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

            if (prefab.GetComponentInChildren<ParticleSystem>(true) != null)
            {
                Debug.LogWarning(
                    "[RandomEventSystem] Meteor splash prefab contains ParticleSystem. Replace with MeteorSplashQuadVfx two-quad DrawMeshInstanced fake.",
                    prefab);
            }

            if (!HasMeteorSplashQuadVfx(prefab))
            {
                Debug.LogWarning(
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

        private void QueueMeteorWaterBoom(Vector3 impactPosition, in AbsoluteUniversePosition observerAup, float intensity)
        {
            _pendingMeteorWaterBoom = true;
            _pendingMeteorWaterBoomPosition = impactPosition;
            _pendingMeteorWaterBoomTimer = ResolveMeteorThunderDelaySeconds(impactPosition, in observerAup);
            _pendingMeteorWaterBoomIntensity = math.saturate(intensity * meteorBoomIntensity);
        }

        private static float ResolveMeteorThunderDelaySeconds(Vector3 impactPosition, in AbsoluteUniversePosition observerAup)
        {
            AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromRuntimePosition(impactPosition);
            double distanceSq = AbsoluteUniversePosition.DistanceSq(in impactAup, in observerAup);
            double distanceMeters = distanceSq * math.rsqrt(math.max(0.000001d, distanceSq));
            return (float)(distanceMeters / MeteorThunderSoundSpeedMetersPerSecond);
        }

        private static bool TryResolvePlayerEventFrame(out Vector3 runtimePosition, out AbsoluteUniversePosition playerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerMovement playerMovement = playerContext != null ? playerContext.PlayerMovement : null;
            if (playerMovement != null)
            {
                playerAup = playerMovement.CurrentAup;
                float3 runtime = playerAup.ToRuntimeFloat3();
                runtimePosition = new Vector3(runtime.x, runtime.y, runtime.z);
                return true;
            }

            runtimePosition = default;
            playerAup = default;
            return false;
        }

        private void ApplySolarFlareRadiation(float dt)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            HectonPlayerHealth playerHealth = playerContext != null ? playerContext.PlayerHealth : null;
            if (playerHealth == null)
                return;

            float exposureStep = math.max(0f, dt) *
                                 math.saturate(solarFlareIntensity) *
                                 math.max(0f, solarFlareRadiationExposurePerSecond);
            if (exposureStep <= 0f)
                return;

            playerHealth.ApplyRadiationExposure(playerHealth.RadiationExposureSeconds + exposureStep);
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
            if (!(GlobalRegistry.Audio is SpatialAudioManager spatialAudioManager))
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

        private static void PublishMeteorWaterImpactGlobals(Vector3 impactPosition, float radius, float duration, float intensity)
        {
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactPosition,
                new Vector4(impactPosition.x, impactPosition.y, impactPosition.z, math.saturate(intensity)));
            Shader.SetGlobalVector(
                _ShaderMeteorWaterImpactParams,
                new Vector4(math.max(0f, radius), math.max(0f, duration), Time.time, math.saturate(intensity)));
        }

        private bool TryResolveSeismicContext(
            out Vector3 playerPosition,
            out HectonVoxelVolume targetVolume,
            out TectonicActivityProfile.SeismicEventSettings settings)
        {
            playerPosition = default;
            targetVolume = null;
            settings = tectonicActivityProfile != null
                ? tectonicActivityProfile.ResolveSeismicSettings(null, null)
                : default;

            if (!TryResolvePlayerEventFrame(out playerPosition, out _))
                return false;
            if (voxelEngine == null)
                voxelEngine = GlobalRegistry.VoxelEngine;

            if (voxelEngine == null || !voxelEngine.TryGetNearestActiveVolume(playerPosition, out targetVolume) || targetVolume == null)
                return false;

            float maxTargetRadius = math.max(4f, seismicTargetRadius);
            if (IsAupDistanceGreater(targetVolume.generationPosition, playerPosition, maxTargetRadius))
                return false;

            string familyId = null;
            string geologyProfileId = null;
            if (targetVolume.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime runtime))
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
            HectonVoxelVolume targetVolume,
            TectonicActivityProfile.SeismicEventSettings settings,
            out SeismicShockwaveEvent seismicEvent)
        {
            seismicEvent = default;
            if (targetVolume == null)
                return false;

            int stampCount = NextEventRandomRange(settings.stampCountMin, settings.stampCountMax + 1);
            uint stableSeed = ResolveAupTimelineSeed(playerPosition, targetVolume.RuntimeStamp);
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

            ApplySeismicImpulse(playerPosition, settings.impulseRadius, settings.impulseMagnitude);
            double3 epicenterAup = HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(playerPosition);
            Vector3 trenchDirection = ResolveSeismicEventLineDirection(epicenterAup, stableSeed);
            float halfTrenchLength = math.max(2f, settings.impulseRadius * 0.5f);
            double3 trenchDirectionDouble = new double3(trenchDirection.x, trenchDirection.y, trenchDirection.z);
            seismicEvent = new SeismicShockwaveEvent(
                playerPosition,
                settings.impulseRadius,
                settings.impulseMagnitude,
                appliedStampCount,
                epicenterAup - trenchDirectionDouble * halfTrenchLength,
                epicenterAup + trenchDirectionDouble * halfTrenchLength);
            return true;
        }

        private static uint ResolveAupTimelineSeed(Vector3 runtimePosition, int runtimeStamp)
        {
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            double universeTime = GlobalRegistry.AbsoluteUniverseTime;
            if (!math.isfinite(universeTime) || universeTime < 0d)
                universeTime = 0d;

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

        private void ApplySeismicImpulse(Vector3 epicenter, float radius, float impulseMagnitude)
        {
            int overlapCapacity = math.clamp(seismicOverlapCapacity, 16, _seismicOverlapBuffer.Length);
            int hitCount = UnityEngine.Physics.OverlapSphereNonAlloc(
                epicenter,
                math.max(1f, radius),
                _seismicOverlapBuffer,
                HectonLayerMasks.DefaultRaycastLayerMask,
                QueryTriggerInteraction.Ignore);
            if (hitCount <= 0)
                return;

            int uniqueCapacity = math.clamp(seismicUniqueBodyCapacity, 16, _seismicBodyBuffer.Length);
            int uniqueBodyCount = 0;
            for (int hitIndex = 0; hitIndex < hitCount && hitIndex < overlapCapacity; hitIndex++)
            {
                Collider collider = _seismicOverlapBuffer[hitIndex];
                _seismicOverlapBuffer[hitIndex] = null;
                if (collider == null)
                    continue;

                Rigidbody body = collider.attachedRigidbody;
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

                ResolveAupDirectionAndDistance(epicenter, body.worldCenterOfMass, out Vector3 direction, out float distance);
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
                PhysicsForceRouter.QueueForce(body, direction * resolvedImpulse, ForceMode.Impulse);
            }
        }

        private static bool IsAupDistanceGreater(Vector3 runtimeA, Vector3 runtimeB, float thresholdMeters)
        {
            float safeThreshold = math.max(0f, thresholdMeters);
            AbsoluteUniversePosition aupA = AbsoluteUniversePosition.FromRuntimePosition(runtimeA);
            AbsoluteUniversePosition aupB = AbsoluteUniversePosition.FromRuntimePosition(runtimeB);
            return AbsoluteUniversePosition.DistanceSq(in aupA, in aupB) > (double)safeThreshold * safeThreshold;
        }

        private static void ResolveAupDirectionAndDistance(
            Vector3 fromRuntime,
            Vector3 toRuntime,
            out Vector3 direction,
            out float distance)
        {
            AbsoluteUniversePosition fromAup = AbsoluteUniversePosition.FromRuntimePosition(fromRuntime);
            AbsoluteUniversePosition toAup = AbsoluteUniversePosition.FromRuntimePosition(toRuntime);
            double3 delta = toAup.ToAbsoluteDouble3() - fromAup.ToAbsoluteDouble3();
            double distanceSq = math.dot(delta, delta);
            double resolvedDistance = distanceSq * math.rsqrt(math.max(0.000001d, distanceSq));
            distance = resolvedDistance > float.MaxValue ? float.MaxValue : (float)resolvedDistance;
            direction = resolvedDistance > 0.0001d
                ? new Vector3(
                    (float)(delta.x / resolvedDistance),
                    (float)(delta.y / resolvedDistance),
                    (float)(delta.z / resolvedDistance))
                : Vector3.up;
        }
    }
}
