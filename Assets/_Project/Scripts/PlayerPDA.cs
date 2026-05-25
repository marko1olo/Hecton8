// ============================================================================
// HECTON-8 — PlayerPDA.cs  v2.0 ENTERPRISE
// Personalnyy data-assistent (inventory / loadout / construction / barter / data log).
// Naznachit na Player root. Upravlyaet Canvas-panelyu PDA.
//
// v2.0 ENTERPRISE ADDITIONS:
//   [ADD] PDAEvents — queue-backed global PDA event lane (Opened, Closed, TabChanged)
//   [ADD] Audio feedback — open/close/tab switch sounds cherez SpatialAudioManager
//   [ADD] Panel slide animation — plavnoe poyavlenie/ischeznovenie Canvas
//   [ADD] Battery drain system — PDA potreblyaet energiyu iz HectonSurvivalSystem
//   [ADD] Low battery warning — avtozakrytie pri kriticheskom zaryade
//   [ADD] Tab history stack — vozvrat na predyduschuyu vkladku cherez Backspace
//   [ADD] Diagnostics — _debugIsOpen, _debugActiveTab, _debugBatteryDrain
//   [ADD] Null-safety — vse ssylki proveryayutsya, graceful degradation
//   [ADD] CanvasGroup fade — alpha transition dlya plavnogo poyavleniya
//
// ARHITEKTURA:
//   • IsOpen — staticheskoe svoystvo, chitaetsya HectonPlayerMovement
//     i PlayerInteraction dlya blokirovki vvoda (analogichno HectonFabricatorUI).
//   • Klavisha M (ili iz ControlScheme).
//   • Canvas-panel naznachaetsya v inspektore — PDA ne znaet o soderzhimom.
//   • Vkladki (inventory, loadout, controls, data log) — dochernie GameObject'y paneli,
//     pereklyuchayutsya cherez SetActiveTab(int).
//   • Battery drain — optsionalnaya integratsiya s HectonSurvivalSystem.
//
// ZERO GC:
//   • Vse sobytiya — delegaty bez boxing
//   • Tab history — pre-allocated stack (max 8 entries)
//   • Audio clips — cached references, no string lookups
//   • CanvasGroup — cached component, no GetComponent per frame
//
// INTEGRATsIYa:
//   HectonPlayerMovement.Tick() — gard: if (PlayerPDA.IsOpen) return;
//   PlayerInteraction.Tick()    — gard: if (PlayerPDA.IsOpen) return;
//   HectonSurvivalSystem        — optsionalno: DrainEnergy(batteryDrainRate * dt)
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Crafting;
using Hecton8.Gameplay;
using Hecton8.World;
using System;
using System.Runtime.InteropServices;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Hecton8.UI
{
    /// <summary>
    /// Globalnaya shina sobytiy PDA. Zero GC, thread-safe.
    /// Podpischiki: HUD, audio, analitika, sohraneniya.
    /// </summary>
    public enum PDAEventType : byte
    {
        Opened = 0,
        Closed = 1,
        TabChanged = 2,
        LowBatteryShutdown = 3,
        MapChunkExplored = 4,
        MarkerChanged = 5,
        LogbookChanged = 6,
        UndoRequest = 7
    }

    /// <summary>
    /// Blittable PDA event payload queued by <see cref="PDAEvents"/>.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PDAEventPayload
    {
        [FieldOffset(0)] public float DurationSeconds;
        [FieldOffset(4)] public int PreviousTab;
        [FieldOffset(8)] public int CurrentTab;
        [FieldOffset(12)] public int PayloadA;
        [FieldOffset(16)] public int PayloadB;
        [FieldOffset(20)] public uint MarkerHashID;
        [FieldOffset(24)] public uint LogEventHashID;
        [FieldOffset(28)] public uint EventHashID;
        [FieldOffset(32)] public uint SourceID;
        [FieldOffset(36)] public ushort EventType;
        [FieldOffset(38)] public ushort Reserved;
        [FieldOffset(40)] private ulong _pad0;
        [FieldOffset(48)] private ulong _pad1;
        [FieldOffset(56)] private ulong _pad2;
    }

    /// <summary>
    /// Listener contract for queue-drained PDA events.
    /// </summary>
    public interface IPDAEventListener
    {
        void OnPDAEvent(in PDAEventPayload payload);
    }

    /// <summary>
    /// Queue-backed PDA event lane flushed from SystemDispatcher.LateUpdate.
    /// </summary>
    public static class PDAEvents
    {
        private const int ListenerCapacity = 32;
        private const int PendingEventCapacity = 32;
        private const int EventDedupCapacity = 128;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;

        private struct ListenerSlot
        {
            public IPDAEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[32] - PDA listeners drained by SystemDispatcher LateUpdate without interface array dispatch - owner: PDAEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<PDAEventPayload> _pendingEvents;
        private static NativeQueue<PDAEventPayload> _nextFrameEvents;
        private static NativeParallelHashSet<ulong> _queuedEventKeys;
        private static int _listenerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _dedupFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static void Register(IPDAEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            RegisterImmediate(listener);
        }

        public static void Unregister(IPDAEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterImmediate(listener);
        }

        public static bool IsRegistered(IPDAEventListener listener)
        {
            return listener != null && ContainsImmediate(listener);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AssertUnregistered(IPDAEventListener listener, string ownerName)
        {
            if (listener == null || !ContainsImmediate(listener))
                return;

            Debug.LogError("[PDAEvents] Listener destroyed while still registered as an IPDAEventListener.");
        }

        /// <summary>
        /// Flushes all queued PDA events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            FlushPending(int.MaxValue);
        }

        /// <summary>
        /// Flushes a capped number of queued PDA events through registered listeners.
        /// </summary>
        /// <param name="maxEventsPerFrame">Maximum payload count to dequeue this frame.</param>
        public static void FlushPending(int maxEventsPerFrame)
        {
            if (!_pendingEvents.IsCreated || _listenerCount <= 0)
            {
                DrainWithoutDispatch(maxEventsPerFrame);
                return;
            }

            if (maxEventsPerFrame <= 0)
                return;

            int processedCount = 0;
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget > 0 && !_pendingEvents.IsEmpty() && processedCount < maxEventsPerFrame)
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out PDAEventPayload payload))
                    {
                        _pendingEventCount = 0;
                        return;
                    }

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;
                    scanBudget--;
                    ApplySimulationSideEffects(in payload);
                    int count = _listenerCount;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IPDAEventListener listener = _listeners[i].Listener;
                        if (listener != null)
                            listener.OnPDAEvent(in payload);
                    }

                    processedCount++;
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        internal static bool TryRaiseOpened(int tab)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = tab,
                EventType = (ushort)PDAEventType.Opened,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseOpened so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseOpened(int tab) => TryRaiseOpened(tab);

        internal static bool TryRaiseClosed(float duration)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = duration,
                PreviousTab = -1,
                CurrentTab = -1,
                EventType = (ushort)PDAEventType.Closed,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseClosed so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseClosed(float duration) => TryRaiseClosed(duration);

        internal static bool TryRaiseTabChanged(int oldTab, int newTab)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = oldTab,
                CurrentTab = newTab,
                EventType = (ushort)PDAEventType.TabChanged,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseTabChanged so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseTabChanged(int oldTab, int newTab) => TryRaiseTabChanged(oldTab, newTab);

        internal static bool TryRaiseLowBatteryShutdown()
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                PayloadA = 0,
                PayloadB = 0,
                MarkerHashID = 0u,
                LogEventHashID = 0u,
                EventType = (ushort)PDAEventType.LowBatteryShutdown,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseLowBatteryShutdown so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseLowBatteryShutdown() => TryRaiseLowBatteryShutdown();

        internal static bool TryRaiseMapChunkExplored(int chunkX, int chunkY)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                PayloadA = chunkX,
                PayloadB = chunkY,
                MarkerHashID = 0u,
                LogEventHashID = 0u,
                EventType = (ushort)PDAEventType.MapChunkExplored,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseMapChunkExplored so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseMapChunkExplored(int chunkX, int chunkY) => TryRaiseMapChunkExplored(chunkX, chunkY);

        internal static bool TryRaiseMarkerChanged(uint markerHashId, int markerCount)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                PayloadA = markerCount,
                PayloadB = 0,
                MarkerHashID = markerHashId,
                LogEventHashID = 0u,
                EventType = (ushort)PDAEventType.MarkerChanged,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseMarkerChanged so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseMarkerChanged(uint markerHashId, int markerCount) => TryRaiseMarkerChanged(markerHashId, markerCount);

        internal static bool TryRaiseLogbookChanged(int entryCount, uint latestEventHash = 0u)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                PayloadA = entryCount,
                PayloadB = 0,
                MarkerHashID = 0u,
                LogEventHashID = latestEventHash,
                EventType = (ushort)PDAEventType.LogbookChanged,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseLogbookChanged so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseLogbookChanged(int entryCount, uint latestEventHash = 0u) => TryRaiseLogbookChanged(entryCount, latestEventHash);

        internal static bool TryRaiseUndoRequest(int framesBack = 1)
        {
            return Enqueue(new PDAEventPayload
            {
                DurationSeconds = 0f,
                PreviousTab = -1,
                CurrentTab = -1,
                PayloadA = Mathf.Max(1, framesBack),
                PayloadB = 0,
                MarkerHashID = 0u,
                LogEventHashID = 0u,
                EventType = (ushort)PDAEventType.UndoRequest,
                Reserved = 0
            });
        }

        [System.Obsolete("Use TryRaiseUndoRequest so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseUndoRequest(int framesBack = 1) => TryRaiseUndoRequest(framesBack);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PDAEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(PDAEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            if (_queuedEventKeys.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeParallelHashSet(nameof(PDAEvents), nameof(_queuedEventKeys));
                _queuedEventKeys.Dispose();
                _queuedEventKeys = default;
            }

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _dedupFrame = -1;
            _isDispatching = false;
        }

        private static void RegisterImmediate(IPDAEventListener listener)
        {
            if (ContainsImmediate(listener))
                return;

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        private static bool TryUnregisterImmediate(IPDAEventListener listener)
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

        private static bool ContainsImmediate(IPDAEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<PDAEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PDAEventPayload>[32] — deferred PDA event lane flushed by SystemDispatcher LateUpdate — owner: PDAEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(PDAEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<PDAEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<PDAEventPayload>[32] — next-frame PDA events raised by listeners — owner: PDAEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(PDAEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }

            if (!_queuedEventKeys.IsCreated)
            {
                _queuedEventKeys = new NativeParallelHashSet<ulong>(EventDedupCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeParallelHashSet<ulong>[128] - per-frame PDA duplicate suppression keys - owner: PDAEvents
                NativeMemorySentinel.RegisterNativeParallelHashSet(
                    _queuedEventKeys,
                    nameof(PDAEvents),
                    nameof(_queuedEventKeys),
                    NativeAllocationLifetime.Session);
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

        private static bool Enqueue(in PDAEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            PrepareDedupFrame();
            PDAEventPayload resolvedPayload = payload;
            ResolveDedupFields(ref resolvedPayload);
            ulong dedupKey = ComposeDedupKey(in resolvedPayload);
            if (dedupKey != 0UL && !TryRegisterDedupKey(dedupKey))
                return false;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(resolvedPayload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(resolvedPayload);
            _pendingEventCount++;
            return true;
        }

        private static void DrainWithoutDispatch(int maxEventsPerFrame)
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (maxEventsPerFrame <= 0)
                return;

            int processedCount = 0;
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget > 0 && processedCount < maxEventsPerFrame && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out PDAEventPayload payload))
                {
                    _pendingEventCount = 0;
                    return;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
                scanBudget--;
                ApplySimulationSideEffects(in payload);
                processedCount++;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out PDAEventPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }

        private static void ApplySimulationSideEffects(in PDAEventPayload payload)
        {
            if ((PDAEventType)payload.EventType == PDAEventType.UndoRequest)
                UIStateStore.TryRollbackPDAState(payload.PayloadA <= 0 ? 1 : payload.PayloadA);
        }

        private static void PrepareDedupFrame()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_dedupFrame == frame)
                return;

            if (_queuedEventKeys.IsCreated)
                _queuedEventKeys.Clear();

            _dedupFrame = frame;
        }

        private static bool TryRegisterDedupKey(ulong dedupKey)
        {
            if (!_queuedEventKeys.IsCreated)
                return true;

            int currentCount = _queuedEventKeys.Count();
            if (currentCount >= _queuedEventKeys.Capacity)
                return true;

            return _queuedEventKeys.Add(dedupKey);
        }

        private static void ResolveDedupFields(ref PDAEventPayload payload)
        {
            if (payload.EventHashID == 0u)
                payload.EventHashID = ResolveEventHashID(in payload);

            if (payload.SourceID == 0u)
                payload.SourceID = ResolveSourceID(in payload);
        }

        private static ulong ComposeDedupKey(in PDAEventPayload payload)
        {
            uint eventHash = payload.EventHashID != 0u ? payload.EventHashID : ResolveEventHashID(in payload);
            uint sourceId = payload.SourceID != 0u ? payload.SourceID : ResolveSourceID(in payload);
            return ((ulong)sourceId << 32) | eventHash;
        }

        private static uint ResolveEventHashID(in PDAEventPayload payload)
        {
            PDAEventType eventType = (PDAEventType)payload.EventType;
            if (eventType == PDAEventType.MarkerChanged && payload.MarkerHashID != 0u)
                return payload.MarkerHashID;

            if (eventType == PDAEventType.LogbookChanged && payload.LogEventHashID != 0u)
                return payload.LogEventHashID;

            return Mix32((uint)payload.EventType, PackSigned(payload.PayloadA), PackSigned(payload.PayloadB), PackSigned(payload.CurrentTab));
        }

        private static uint ResolveSourceID(in PDAEventPayload payload)
        {
            PDAEventType eventType = (PDAEventType)payload.EventType;
            switch (eventType)
            {
                case PDAEventType.Opened:
                    return PackSigned(payload.CurrentTab);
                case PDAEventType.Closed:
                    return 1u;
                case PDAEventType.TabChanged:
                    return Mix32(PackSigned(payload.PreviousTab), PackSigned(payload.CurrentTab), 0u, 0u);
                case PDAEventType.MapChunkExplored:
                    return Mix32(PackSigned(payload.PayloadA), PackSigned(payload.PayloadB), 0u, 0u);
                case PDAEventType.MarkerChanged:
                    return payload.MarkerHashID != 0u ? payload.MarkerHashID : PackSigned(payload.PayloadA);
                case PDAEventType.LogbookChanged:
                    return payload.LogEventHashID != 0u ? payload.LogEventHashID : PackSigned(payload.PayloadA);
                case PDAEventType.UndoRequest:
                    return PackSigned(payload.PayloadA);
                default:
                    return (uint)eventType;
            }
        }

        private static uint PackSigned(int value)
        {
            return unchecked((uint)value);
        }

        private static uint Mix32(uint a, uint b, uint c, uint d)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ a) * 16777619u;
                hash = (hash ^ b) * 16777619u;
                hash = (hash ^ c) * 16777619u;
                hash = (hash ^ d) * 16777619u;
                return hash == 0u ? 1u : hash;
            }
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Player/Player PDA")]
    public sealed class PlayerPDA : MonoBehaviour, ITickable, ILateFrameTickable, ICraftingEventListener, IGlobalRegistryHotSwapListener
    {
        private const int PendingPdaSoundCapacity = 4;
        private const float CraftStartedClickPitch = 0.92f;
        private const float CraftCompletedClickPitch = 1.08f;
        private const float CraftCancelledClickPitch = 0.74f;
        private const float CraftFailedClickPitch = 0.58f;
        private const float CraftClickVolumeScale = 0.86f;
        private const float PdaClockMaxSeconds = 16777215f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REFERENCES
        // ══════════════════════════════════════════════════════════

        [Header("── References ──────────────────────────────")]
        [Tooltip("Kornevoy GameObject Canvas-paneli PDA.")]
        [SerializeField] private GameObject pdaPanel;

        [Tooltip("CanvasGroup dlya fade-animatsii. Esli null — mgnovennoe poyavlenie.")]
        [SerializeField] private CanvasGroup pdaCanvasGroup;

        [Tooltip("Vkladki PDA. Poryadok: 0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log, 5=Spectrum, 6=Atlas Signal, 7=Diagnostics.")]
        [SerializeField] private GameObject[] tabs = new GameObject[8];

        [Tooltip("HectonSurvivalSystem dlya battery drain. Optsionalno.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        [Tooltip("Shader forwarded into runtime-created PDA spectrum/map tabs for GPU sonar point-cloud rendering.")]
        [SerializeField] private Shader pdaSonarPointCloudShader;

        [Tooltip("Compute shader forwarded into runtime-created PDA spectrum/map tabs for GPU sonar point-cloud rendering.")]
        [SerializeField] private ComputeShader pdaSonarMapCompute;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Settings ────────────────────────────────")]
        [Tooltip("Vkladka po umolchaniyu pri otkrytii (0=Inventory, 1=Loadout, 2=Construction, 3=Barter, 4=Data Log, 5=Spectrum, 6=Atlas Signal, 7=Diagnostics).")]
        [SerializeField] private int defaultTab = 0;

        [Tooltip("Skorost fade-animatsii (alpha/sec). 0 = mgnovenno.")]
        [SerializeField, Range(0f, 10f)] private float fadeSpeed = 5f;

        [Tooltip("Vklyuchit battery drain. PDA potreblyaet energiyu pri otkrytii.")]
        [SerializeField] private bool enableBatteryDrain = true;

        [Tooltip("Energiya/sek pri otkrytom PDA. 0.5 = 2 sekundy na 1%.")]
        [SerializeField, Range(0f, 5f)] private float batteryDrainRate = 0.5f;

        [Tooltip("Kriticheskiy uroven energii (%). Nizhe — PDA avtozakryvaetsya.")]
        [SerializeField, Range(0f, 20f)] private float lowBatteryThreshold = 5f;

        [Tooltip("Vklyuchit tab history (Backspace = nazad).")]
        [SerializeField] private bool enableTabHistory = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────")]
        [Tooltip("Zvuk otkrytiya PDA (holographic deploy).")]
        [SerializeField] private AudioClip openSound;

        [Tooltip("Zvuk zakrytiya PDA (holographic collapse).")]
        [SerializeField] private AudioClip closeSound;

        [Tooltip("Zvuk pereklyucheniya vkladki (soft beep).")]
        [SerializeField] private AudioClip tabSwitchSound;

        [Tooltip("Zvuk low battery warning (alert tone).")]
        [SerializeField] private AudioClip lowBatterySound;

        [Tooltip("Gromkost zvukov PDA.")]
        [SerializeField, Range(0f, 1f)] private float audioVolume = 0.6f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ─────────────────────────────")]
        [SerializeField] private bool _debugIsOpen;
        [SerializeField] private int _debugActiveTab = -1;
        [SerializeField] private float _debugOpenDuration;
        [SerializeField] private float _debugCurrentAlpha;
        [SerializeField] private float _debugBatteryDrainAccum;
        [SerializeField] private int _debugTabHistoryDepth;

        // ══════════════════════════════════════════════════════════
        //  STATIC STATE — chitaetsya drugimi sistemami
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// True kogda PDA otkryt. Chitaetsya HectonPlayerMovement i
        /// PlayerInteraction dlya blokirovki vvoda.
        /// </summary>
        public static bool IsOpen { get; private set; }
        internal static PlayerPDA ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            IsOpen = false;
            ActiveRuntimeInstance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private int _activeTab = -1;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _craftingEventsRegistered;
        private bool _hotSwapRegistered;
        private bool _missingUiShellReported;
        private bool _missingInputServiceReported;
        private uint _observedUIStateCommandSequence;
        private uint _lastPlayerInputSignalSequence;
        private const uint PlayerInputSignalSourceHash = 0x504C494Eu;
        private IInputService _inputService;
        private IAudioService _audioService;
        private IRenderTexturePoolService _renderTexturePool;
        private IPlayerRuntimeContext _playerRuntimeContext;

        // Fade animation
        private float _targetAlpha;
        private float _currentAlpha;
        private bool _isFading;

        // Battery drain
        private float _pdaClockSeconds;
        private float _openStartTime;
        private float _batteryDrainAccumulator;
        private bool _lowBatteryWarningPlayed;

        // Tab history (pre-allocated stack, max 8 entries)
        private readonly int[] _tabHistory = new int[8];
        private int _tabHistoryCount;
        private CanvasGroup[] _tabCanvasGroups;
        private readonly AudioClip[] _pendingSoundClips = new AudioClip[PendingPdaSoundCapacity];
        private readonly float[] _pendingSoundVolumes = new float[PendingPdaSoundCapacity];
        private readonly float[] _pendingSoundPitches = new float[PendingPdaSoundCapacity];
        private int _pendingSoundCount;
        private bool _pendingLowBatteryShutdownClose;
        private bool _survivalResolveDirty;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public int ActiveTab => _activeTab;
        public bool IsFading => _isFading;
        public float CurrentAlpha => _currentAlpha;
        public GameObject PanelRoot => pdaPanel;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ResolveTabReferences(createMissingTabs: false);
            IsOpen = false;
            _currentAlpha = 0f;
            _targetAlpha = 0f;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            // Auto-resolve CanvasGroup if not assigned
            if (pdaCanvasGroup == null && pdaPanel != null)
            {
                if (!pdaPanel.TryGetComponent(out pdaCanvasGroup))
                {
                    Debug.LogWarning(
                        "[PlayerPDA] No CanvasGroup found. Adding one for fade animation.");
                    pdaCanvasGroup = pdaPanel.AddComponent<CanvasGroup>();
                }
            }

            PrepareRuntimeVisibility();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                ActiveRuntimeInstance = this;
                UIStateStore.EnsureInitialized();
                TryRegisterHotSwapListener();
                RefreshColdRegistryReferences();
            }

            BaselinePlayerInputSignalSequence();
            TryRegister();
            TryRegisterCraftingEvents();
        }

        private void Start()
        {
            ResolveTabReferences(createMissingTabs: false);
            RefreshColdRegistryReferences();
            TryRegister();
            TryRegisterCraftingEvents();

            if (!_registered)
            {
                Debug.LogError(
                    "[PlayerPDA] PDA dispatcher registration failed at Start(). PDA tick loop will not run.");
            }

            IInputService inputManager = _inputService;
            if (inputManager == null || !inputManager.IsInitialized)
            {
                Debug.LogError("[PlayerPDA] GlobalRegistry.Input is not initialized at Start. PDA will not function.");
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            ResolveEditorReferences();
        }

        private void ResolveEditorReferences()
        {
            EnsureTabArrayCapacity();

            if (pdaPanel == null)
            {
                ClearResolvedTabs();
            }
            else
            {
                Transform root = pdaPanel.transform;
                tabs[0] = ResolveExistingTab(root, "Tab_Inventory");
                tabs[1] = ResolveExistingTab(root, "Tab_Loadout");
                tabs[2] = ResolveExistingTab(root, "Tab_Construction");
                tabs[3] = ResolveExistingTab(root, "Tab_Barter");
                tabs[4] = ResolveExistingTab(root, "Tab_DataLog", "Tab_Reserved");
                tabs[5] = ResolveExistingTab(root, "Tab_Spectrum");
                tabs[6] = ResolveExistingTab(root, "Tab_AtlasSignal");
                tabs[7] = ResolveExistingTab(root, "Tab_Diagnostics");
            }

            if (pdaPanel != null && pdaCanvasGroup == null)
                pdaPanel.TryGetComponent(out pdaCanvasGroup);
        }

        private void ResolveTabReferences(bool createMissingTabs)
        {
            EnsureTabArrayCapacity();

            if (pdaPanel == null)
            {
                ClearResolvedTabs();
                return;
            }

            Transform root = pdaPanel.transform;
            GameObject inventory = ResolveExistingTab(root, "Tab_Inventory");
            GameObject loadout = ResolveExistingTab(root, "Tab_Loadout");
            GameObject construction = ResolveExistingTab(root, "Tab_Construction");
            GameObject barter = ResolveExistingTab(root, "Tab_Barter");
            GameObject dataLog = ResolveExistingTab(root, "Tab_DataLog", "Tab_Reserved");
            GameObject spectrum = ResolveExistingTab(root, "Tab_Spectrum");
            GameObject atlasSignal = ResolveExistingTab(root, "Tab_AtlasSignal");
            GameObject diagnostics = ResolveExistingTab(root, "Tab_Diagnostics");

            if (createMissingTabs)
            {
                if (barter == null)
                    barter = EnsureRuntimeTab(root, "Tab_Barter", typeof(PDABarterTab));

                if (spectrum == null)
                    spectrum = EnsureRuntimeTab(root, "Tab_Spectrum", typeof(Hecton8.UI.PDASpectrumTab));

                if (atlasSignal == null)
                    atlasSignal = EnsureRuntimeTab(root, "Tab_AtlasSignal", typeof(Hecton8.UI.PDAAtlasSignalTab));

                if (diagnostics == null)
                    diagnostics = EnsureRuntimeTab(root, "Tab_Diagnostics", typeof(Hecton8.UI.PDADiagnosticTerminal));
            }

            if (inventory == null &&
                loadout == null &&
                construction == null &&
                barter == null &&
                dataLog == null &&
                spectrum == null &&
                atlasSignal == null &&
                diagnostics == null)
            {
                ClearResolvedTabs();
                return;
            }

            ClearResolvedTabs();
            if (inventory != null)   tabs[0] = inventory;
            if (loadout != null)     tabs[1] = loadout;
            if (construction != null) tabs[2] = construction;
            if (barter != null)      tabs[3] = barter;
            if (dataLog != null)     tabs[4] = dataLog;
            if (spectrum != null)
            {
                ConfigureSpectrumRuntimeAssets(spectrum);
                tabs[5] = spectrum;
            }
            if (atlasSignal != null) tabs[6] = atlasSignal;
            if (diagnostics != null) tabs[7] = diagnostics;
        }

        private void ConfigureSpectrumRuntimeAssets(GameObject spectrum)
        {
            if (spectrum == null || !spectrum.TryGetComponent(out PDASpectrumTab spectrumTab))
                return;

            spectrumTab.ConfigureMapRuntimeAssets(pdaSonarPointCloudShader, pdaSonarMapCompute);
        }

        private static GameObject EnsureRuntimeTab(Transform root, string name, Type tabComponentType)
        {
            if (root == null)
                return null;

            Transform existing = root.Find(name);
            if (existing != null)
            {
                if (tabComponentType != null &&
                    !existing.gameObject.TryGetComponent(tabComponentType, out Component _))
                {
                    existing.gameObject.AddComponent(tabComponentType);
                }
                return existing.gameObject;
            }

            GameObject tab = new GameObject(name, typeof(RectTransform));
            tab.layer = root.gameObject.layer;
            RectTransform rect = (RectTransform)tab.transform;
            rect.SetParent(root, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(24f, 24f);
            rect.offsetMax = new Vector2(-24f, -72f);
            if (tabComponentType != null)
                tab.AddComponent(tabComponentType);

            if (!tab.TryGetComponent(out CanvasGroup canvasGroup))
                canvasGroup = tab.AddComponent<CanvasGroup>();

            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            return tab;
        }

        private void EnsureTabArrayCapacity()
        {
            if (tabs == null || tabs.Length != 8)
                tabs = new GameObject[8]; // COLD ALLOC: GameObject[8] — PDA tab reference cache — owner: PlayerPDA
        }

        private void ClearResolvedTabs()
        {
            if (tabs == null)
                return;

            for (int i = 0; i < tabs.Length; i++)
                tabs[i] = null;
        }

        private static GameObject ResolveExistingTab(Transform root, string primaryName, string alternateName = null)
        {
            if (root == null)
                return null;

            Transform primary = root.Find(primaryName);
            if (primary != null)
                return primary.gameObject;

            if (!string.IsNullOrEmpty(alternateName))
            {
                Transform alternate = root.Find(alternateName);
                if (alternate != null)
                    return alternate.gameObject;
            }

            return null;
        }

#if UNITY_EDITOR
        [ContextMenu("Rebuild PDA")]
        private void RebuildPda()
        {
            ResolveTabReferences(createMissingTabs: true);
            ResolveEditorReferences();
        }
#endif

        private void OnDisable()
        {
            TryUnregister();
            UnregisterCraftingEvents();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            // Zakryvaem pri otklyuchenii komponenta
            if (IsOpen) ForceClose();
        }

        private void OnDestroy()
        {
            TryUnregister();
            UnregisterCraftingEvents();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.NativeInputManagerRuntime:
                    if (_inputService == null)
                        _inputService = currentService as IInputService;
                    break;
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.RenderTexturePoolRuntime:
                    _renderTexturePool = currentService as IRenderTexturePoolService;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _registered = false;
                    _registeredLateFrame = false;
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        private void RefreshColdRegistryReferences()
        {
            _inputService = GlobalRegistry.Input;
            _audioService = Hecton8.Audio.SpatialAudioManager.ActiveRuntimeInstance;
            _renderTexturePool = GlobalRegistry.RenderTexturePoolService;
            CachePlayerRuntimeContext(GlobalRegistry.Player);
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext;

            if (survivalSystem != null || _playerRuntimeContext == null)
                return;

            Transform playerTransform = _playerRuntimeContext.PlayerTransform;
            if (playerTransform != null)
                playerTransform.TryGetComponent(out survivalSystem);
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

        private void TryRegisterCraftingEvents()
        {
            if (_craftingEventsRegistered || !Application.isPlaying)
                return;

            CraftingEvents.Register(this);
            _craftingEventsRegistered = true;
        }

        private void UnregisterCraftingEvents()
        {
            if (!_craftingEventsRegistered)
                return;

            CraftingEvents.Unregister(this);
            _craftingEventsRegistered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  TICK
        // ══════════════════════════════════════════════════════════

        public void Tick(float deltaTime)
        {
            AdvancePdaClock(deltaTime);
            ConsumePlayerInputSignals();

            // ── Battery drain ──
            if (IsOpen && enableBatteryDrain)
            {
                if (survivalSystem == null)
                    _survivalResolveDirty = true;

                if (survivalSystem != null)
                    ProcessBatteryDrain(deltaTime);
            }
        }

        public void LateFrameTick()
        {
            float deltaTime = math.max(0f, SystemDispatcher.CurrentFrameDeltaTime);
            if (_survivalResolveDirty)
            {
                _survivalResolveDirty = false;
                TryResolveSurvivalSystemFromRuntimeContext();
            }

            ApplyHeadlessUIState();

            if (_pendingLowBatteryShutdownClose)
            {
                _pendingLowBatteryShutdownClose = false;
                PDAEvents.TryRaiseLowBatteryShutdown();
                Close();
            }

            if (_isFading)
                ProcessFadeAnimation(deltaTime);

            UpdateDiagnostics();
            FlushPendingSounds();
        }

        public void OnCraftingEvent(in CraftingEventPayload payload)
        {
            if (!IsOpen)
                return;

            switch ((CraftingEventType)payload.EventType)
            {
                case CraftingEventType.CraftStarted:
                    PlayCraftingClick(CraftStartedClickPitch);
                    break;
                case CraftingEventType.CraftCompleted:
                    PlayCraftingClick(CraftCompletedClickPitch);
                    break;
                case CraftingEventType.CraftCancelled:
                    PlayCraftingClick(CraftCancelledClickPitch);
                    break;
                case CraftingEventType.CraftFailed:
                    PlayCraftingClick(CraftFailedClickPitch);
                    break;
            }
        }

        private void ApplyHeadlessUIState()
        {
            UIStateData state = UIStateStore.GetPDAState();
            if (state.CommandSequence == _observedUIStateCommandSequence)
                return;

            bool wantsOpen = (state.Flags & (ushort)UIStateFlags.PDAOpen) != 0;
            int requestedTab = Mathf.Clamp(state.ActiveTab, 0, tabs != null && tabs.Length > 0 ? tabs.Length - 1 : 0);

            if (wantsOpen)
            {
                if (!IsOpen)
                    Open(requestedTab);
                else if (requestedTab != _activeTab)
                    SetActiveTab(requestedTab);
            }
            else if (IsOpen)
            {
                Close();
            }

            _observedUIStateCommandSequence = UIStateStore.GetPDAState().CommandSequence;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        public void Open(int tab = -1)
        {
            if (IsOpen) return;

            if (!TryPrepareRenderableShell())
                return;

            IsOpen = true;
            _openStartTime = ResolvePdaClockSeconds();
            _batteryDrainAccumulator = 0f;
            _lowBatteryWarningPlayed = false;

            // Switch to UI input map
            SwitchToUIInputIfAvailable();
            SystemDispatcher.RequestPdaDepthOfField(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            int targetTab = tab >= 0 ? tab : defaultTab;
            SetActiveTab(targetTab);

            // Start fade-in animation
            _targetAlpha = 1f;
            _isFading = true;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.interactable = false; // block until fade complete
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PlaySound(openSound);
            PDAEvents.TryRaiseOpened(targetTab);
        }

        public void Close()
        {
            if (!IsOpen) return;

            float duration = ResolvePdaOpenDurationSeconds();

            IsOpen = false;
            SystemDispatcher.RequestPdaDepthOfField(false);

            // Switch back to Player input map
            SwitchToPlayerInputIfAvailable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;

            // Start fade-out animation
            _targetAlpha = 0f;
            _isFading = true;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PlaySound(closeSound);
            PDAEvents.TryRaiseClosed(duration);
            ReclaimPdaRenderTextures();

            ClearTabHistory();
        }

        /// <summary>Pereklyuchit vkladku (0=Inventory, 1=Controls, 2=Data Log).</summary>
        public void SetActiveTab(int index)
        {
            if (tabs == null || tabs.Length == 0) return;

            int newTab = Mathf.Clamp(index, 0, tabs.Length - 1);
            if (newTab == _activeTab) return;

            int oldTab = _activeTab;

            // Push old tab to history (if valid and history enabled)
            if (enableTabHistory && oldTab >= 0)
                PushTabHistory(oldTab);

            _activeTab = newTab;
            ApplyTabVisibility(_activeTab);

            if (oldTab >= 0) // not initial open
            {
                PlaySound(tabSwitchSound);
                PDAEvents.TryRaiseTabChanged(oldTab, newTab);
            }
        }

        /// <summary>Programmnoe zakrytie bez animatsii (dlya OnDisable).</summary>
        public void ForceClose()
        {
            if (!IsOpen) return;

            float duration = ResolvePdaOpenDurationSeconds();

            IsOpen = false;
            SystemDispatcher.RequestPdaDepthOfField(false);
            _isFading = false;
            _currentAlpha = 0f;
            _targetAlpha = 0f;

            if (pdaCanvasGroup != null)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            // Switch back to Player input map on force close
            SwitchToPlayerInputIfAvailable();

            PDAEvents.TryRaiseClosed(duration);
            ReclaimPdaRenderTextures();
            ClearTabHistory();
        }

        /// <summary>
        /// Allows runtime-generated UI to wire the PDA shell without reflection hacks.
        /// </summary>
        public void ConfigureUI(GameObject panelRoot, CanvasGroup panelCanvasGroup, GameObject[] configuredTabs)
        {
            pdaPanel = panelRoot;
            pdaCanvasGroup = panelCanvasGroup;
            tabs = configuredTabs ?? Array.Empty<GameObject>();
            _missingUiShellReported = false;

            if (pdaCanvasGroup != null && !IsOpen)
            {
                pdaCanvasGroup.alpha = 0f;
                pdaCanvasGroup.interactable = false;
                pdaCanvasGroup.blocksRaycasts = false;
            }

            PrepareRuntimeVisibility();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — FADE ANIMATION
        // ══════════════════════════════════════════════════════════

        private bool TryPrepareRenderableShell()
        {
            PrepareRuntimeVisibility();

            if (pdaPanel == null)
            {
                ReportMissingUiShellOnce();
                return false;
            }

            bool resolvedTabsDuringOpen = false;
            if (!HasAnyResolvedTab())
            {
                ResolveTabReferences(createMissingTabs: false);
                if (!HasAnyResolvedTab())
                {
                    ReportMissingUiShellOnce();
                    return false;
                }

                resolvedTabsDuringOpen = true;
            }

            if (resolvedTabsDuringOpen)
                PrepareRuntimeVisibility();

            return true;
        }

        private bool HasAnyResolvedTab()
        {
            if (tabs == null)
                return false;

            for (int i = 0; i < tabs.Length; i++)
            {
                if (tabs[i] != null)
                    return true;
            }

            return false;
        }

        private void SwitchToUIInputIfAvailable()
        {
            IInputService inputManager = _inputService;
            if (inputManager != null && inputManager.IsInitialized)
            {
                inputManager.SwitchToUIInput();
                return;
            }

            ReportMissingInputServiceOnce();
        }

        private void SwitchToPlayerInputIfAvailable()
        {
            IInputService inputManager = _inputService;
            if (inputManager != null && inputManager.IsInitialized)
            {
                inputManager.SwitchToPlayerInput();
                return;
            }

            ReportMissingInputServiceOnce();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ReportMissingUiShellOnce()
        {
            if (_missingUiShellReported)
                return;

            _missingUiShellReported = true;
            Debug.LogError("[PlayerPDA] Refusing to open: no configured PDA panel/tabs or DiegeticPDAController bridge has configured the shell.");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void ReportMissingInputServiceOnce()
        {
            if (_missingInputServiceReported)
                return;

            _missingInputServiceReported = true;
            Debug.LogError("[PlayerPDA] GlobalRegistry.Input is missing or not initialized; PDA input-map switch skipped.");
        }

        private void ReclaimPdaRenderTextures()
        {
            IRenderTexturePoolService pool = _renderTexturePool;
            if (pool != null)
                pool.ReclaimPdaRenderTextures();
        }

        private void ProcessFadeAnimation(float deltaTime)
        {
            if (pdaCanvasGroup == null || fadeSpeed <= 0f)
            {
                // No CanvasGroup or instant mode — snap to target
                _currentAlpha = _targetAlpha;
                _isFading = false;
                return;
            }

            float fadeStep = Mathf.Max(0f, fadeSpeed * deltaTime);
            float t = fadeStep / (1f + fadeStep);
            _currentAlpha = math.lerp(_currentAlpha, _targetAlpha, t);

            pdaCanvasGroup.alpha = _currentAlpha;

            // Check completion
            if (Mathf.Abs(_currentAlpha - _targetAlpha) < 0.01f)
            {
                _currentAlpha = _targetAlpha;
                pdaCanvasGroup.alpha = _currentAlpha;
                _isFading = false;

                if (_targetAlpha >= 1f)
                {
                    // Fade-in complete — enable interaction
                    pdaCanvasGroup.interactable = true;
                    pdaCanvasGroup.blocksRaycasts = true;
                }
            }
        }

        private void PrepareRuntimeVisibility()
        {
            if (!Application.isPlaying || pdaPanel == null)
                return;

            if (pdaCanvasGroup == null)
            {
                if (!pdaPanel.TryGetComponent(out pdaCanvasGroup))
                    pdaCanvasGroup = pdaPanel.AddComponent<CanvasGroup>();
            }

            EnsureTabCanvasGroups();

            pdaCanvasGroup.alpha = 0f;
            pdaCanvasGroup.interactable = false;
            pdaCanvasGroup.blocksRaycasts = false;

            ApplyTabVisibility(_activeTab);
        }

        private bool TryResolveSurvivalSystemFromRuntimeContext()
        {
            if (survivalSystem != null)
                return true;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null)
                return false;

            return playerTransform.TryGetComponent(out survivalSystem);
        }

        private void EnsureTabCanvasGroups()
        {
            if (tabs == null || tabs.Length == 0)
            {
                _tabCanvasGroups = Array.Empty<CanvasGroup>();
                return;
            }

            if (_tabCanvasGroups == null || _tabCanvasGroups.Length != tabs.Length)
                _tabCanvasGroups = new CanvasGroup[tabs.Length];

            for (int i = 0; i < tabs.Length; i++)
            {
                GameObject tab = tabs[i];
                if (tab == null)
                {
                    _tabCanvasGroups[i] = null;
                    continue;
                }

                CanvasGroup group = _tabCanvasGroups[i];
                if (group == null)
                {
                    if (!tab.TryGetComponent(out group))
                        group = tab.AddComponent<CanvasGroup>();
                    _tabCanvasGroups[i] = group;
                }

                SetCanvasGroupVisible(group, i == _activeTab);
            }
        }

        private void ApplyTabVisibility(int activeTab)
        {
            if (_tabCanvasGroups == null)
                return;

            for (int i = 0; i < _tabCanvasGroups.Length; i++)
                SetCanvasGroupVisible(_tabCanvasGroups[i], i == activeTab);
        }

        private static void SetCanvasGroupVisible(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BATTERY DRAIN
        // ══════════════════════════════════════════════════════════

        private void ProcessBatteryDrain(float deltaTime)
        {
            _batteryDrainAccumulator += batteryDrainRate * deltaTime;

            // Drain energy every 1.0 accumulated units
            if (_batteryDrainAccumulator >= 1f)
            {
                int drainAmount = Mathf.FloorToInt(_batteryDrainAccumulator);
                _batteryDrainAccumulator -= drainAmount;

                survivalSystem.DrainEnergy(drainAmount);
            }

            // Check low battery
            float energyPercent = survivalSystem.EnergyPercent;

            if (energyPercent <= lowBatteryThreshold)
            {
                if (!_lowBatteryWarningPlayed)
                {
                    PlaySound(lowBatterySound);
                    _lowBatteryWarningPlayed = true;
                }

                // Force close on critical
                if (energyPercent <= 1f)
                {
                    _pendingLowBatteryShutdownClose = true;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TAB HISTORY
        // ══════════════════════════════════════════════════════════

        private void PushTabHistory(int tab)
        {
            if (_tabHistoryCount >= _tabHistory.Length)
            {
                // Stack full — shift left (drop oldest)
                for (int i = 0; i < _tabHistory.Length - 1; i++)
                    _tabHistory[i] = _tabHistory[i + 1];

                _tabHistoryCount = _tabHistory.Length - 1;
            }

            _tabHistory[_tabHistoryCount++] = tab;
        }

        private void PopTabHistory()
        {
            if (!TryPopTabHistory(out int previousTab))
                return;

            SetActiveTab(previousTab);
        }

        private bool TryPopTabHistory(out int previousTab)
        {
            previousTab = 0;
            if (_tabHistoryCount <= 0)
                return false;

            previousTab = _tabHistory[--_tabHistoryCount];
            return true;
        }

        private void ClearTabHistory()
        {
            _tabHistoryCount = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — AUDIO
        // ══════════════════════════════════════════════════════════

        private void PlaySound(AudioClip clip)
        {
            PlaySound(clip, audioVolume, 1f);
        }

        private void PlaySound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            if (_pendingSoundCount >= PendingPdaSoundCapacity)
                _pendingSoundCount = PendingPdaSoundCapacity - 1;

            int index = _pendingSoundCount++;
            _pendingSoundClips[index] = clip;
            _pendingSoundVolumes[index] = volume;
            _pendingSoundPitches[index] = pitch;
        }

        private void FlushPendingSounds()
        {
            if (_pendingSoundCount <= 0)
                return;

            IAudioService audioManager = _audioService;
            if (audioManager == null)
            {
                _pendingSoundCount = 0;
                return;
            }

            Vector3 position = ResolvePdaAudioPosition();
            for (int i = 0; i < _pendingSoundCount; i++)
            {
                AudioClip clip = _pendingSoundClips[i];
                if (clip != null)
                    audioManager.PlayAtPoint(clip, position, _pendingSoundVolumes[i], _pendingSoundPitches[i], audioManager.InterfaceGroup);

                _pendingSoundClips[i] = null;
            }

            _pendingSoundCount = 0;
        }

        private Vector3 ResolvePdaAudioPosition()
        {
            return pdaPanel != null ? pdaPanel.transform.position : transform.position;
        }

        private void PlayCraftingClick(float pitch)
        {
            AudioClip resolvedClip = tabSwitchSound != null ? tabSwitchSound : openSound;
            PlaySound(resolvedClip, audioVolume * CraftClickVolumeScale, pitch);
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        private void AdvancePdaClock(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return;

            _pdaClockSeconds = math.min(PdaClockMaxSeconds, _pdaClockSeconds + deltaTime);
        }

        private float ResolvePdaClockSeconds()
        {
            return _pdaClockSeconds;
        }

        private float ResolvePdaOpenDurationSeconds()
        {
            return math.max(0f, ResolvePdaClockSeconds() - _openStartTime);
        }

        private void UpdateDiagnostics()
        {
            _debugIsOpen = IsOpen;
            _debugActiveTab = _activeTab;
            _debugOpenDuration = IsOpen ? ResolvePdaOpenDurationSeconds() : 0f;
            _debugCurrentAlpha = _currentAlpha;
            _debugBatteryDrainAccum = _batteryDrainAccumulator;
            _debugTabHistoryDepth = _tabHistoryCount;
        }

        // ══════════════════════════════════════════════════════════
        //  INPUT CALLBACKS (ZERO GC)
        // ══════════════════════════════════════════════════════════

        private void ConsumePlayerInputSignals()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash != PlayerInputSignalSourceHash ||
                    !IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    continue;

                _lastPlayerInputSignalSequence = signal.Sequence;
                switch (signal.Command)
                {
                    case PlayerInputSignalCommands.TogglePda:
                        HandlePDAInput();
                        break;
                    case PlayerInputSignalCommands.ToggleInventory:
                        HandleInventoryInput();
                        break;
                    case PlayerInputSignalCommands.Cancel:
                        HandleCancelInput();
                        break;
                    case PlayerInputSignalCommands.TabPrevious:
                        HandleBackInput();
                        break;
                    case PlayerInputSignalCommands.TabNext:
                        HandleTabNextInput();
                        break;
                }
            }
        }

        private void BaselinePlayerInputSignalSequence()
        {
            ReadOnlySpan<PlayerInputSignal> signals = SignalBus<PlayerInputSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PlayerInputSignal signal = signals[i];
                if (signal.SourceHash == PlayerInputSignalSourceHash &&
                    IsNewerInputSequence(signal.Sequence, _lastPlayerInputSignalSequence))
                    _lastPlayerInputSignalSequence = signal.Sequence;
            }
        }

        private static bool IsNewerInputSequence(uint candidate, uint current)
        {
            return candidate != 0u && candidate != current && unchecked(candidate - current) < 0x80000000u;
        }

        private void HandlePDAInput()
        {
            // PDA toggle is usually a player-map action, but if PDA is open, 
            // the UI map might also have a toggle or the Player map is disabled.
            // In our case, Open() switches to UI, but UI map might not have "PDA" action.
            // If InputManager handles "PDA" in both maps or if we stay in Player map for toggle:
            EnqueuePDAStateCommand(IsOpen ? -1 : defaultTab);
        }

        private void HandleInventoryInput()
        {
            EnqueuePDAStateCommand(0);
        }

        private void HandleCancelInput()
        {
            if (IsOpen)
                EnqueuePDAStateCommand(-1);
        }

        private void HandleBackInput()
        {
            if (IsOpen && enableTabHistory && TryPopTabHistory(out int previousTab))
                EnqueuePDAStateCommand(previousTab);
        }
        private void HandleTabNextInput()
        {
            if (!IsOpen) return;
            if (tabs == null || tabs.Length == 0) return;

            int next = _activeTab + 1;
            if (next >= tabs.Length) next = 0;
            EnqueuePDAStateCommand(next);
        }

        private static void EnqueuePDAStateCommand(int tabIndex)
        {
            EntityCommand command = tabIndex < 0
                ? EntityCommand.CreateClosePDA()
                : EntityCommand.CreateOpenPDATab(tabIndex);
            ThreadSafeCommandQueue.TryEnqueue(in command);
        }
    }

    /// <summary>
    /// PDA diagnostics tab showing slow-tick memory and FPS state in a monospaced terminal layout.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/UI/PDA Diagnostic Terminal")]
    public sealed class PDADiagnosticTerminal : MonoBehaviour, ISlowTickable, ILateFrameTickable, IPDAEventListener, IGlobalRegistryHotSwapListener
    {
        private const int DiagnosticsTabIndex = 7;
        private const string TitleText = "DIAGNOSTIC TERMINAL // PERF / HULL / OFFSET";
        private static readonly char[] TitleTextBuffer = TitleText.ToCharArray();

        private static readonly Color BackgroundColor = new Color(0.03f, 0.08f, 0.10f, 0.86f);
        private static readonly Color RuleColor = new Color(0.46f, 0.98f, 0.94f, 0.16f);
        private static readonly Color TitleColor = new Color(0.79f, 0.96f, 0.92f, 0.96f);
        private static readonly Color BodyColor = new Color(0.84f, 0.94f, 0.88f, 0.92f);

        [Header("References")]
        [SerializeField] private PlayerPDA playerPda;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private TMP_FontAsset numericFont;

        // COLD ALLOC: char[192] - PDA diagnostics terminal TMP staging buffer - owner: PDADiagnosticTerminal
        private readonly char[] _diagnosticTextBuffer = new char[192];

        private bool _built;
        private bool _registered;
        private bool _registeredLateFrame;
        private bool _hotSwapRegistered;
        private bool _terminalRefreshDirty;
        private bool _terminalForceRefresh;
        private CanvasGroup _group;
        private TextMeshProUGUI _titleLabel;
        private TextMeshProUGUI _bodyLabel;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonPlayerMovement _playerMovement;
        private SargassumMicroFaunaBoids _microFaunaBoids;
        private int _lastMemoryMb = int.MinValue;
        private int _lastFps = int.MinValue;
        private int _lastBoidCount = int.MinValue;
        private int _lastHullStressPercent = int.MinValue;
        private double3 _lastUniverseOffset = new double3(double.NaN, double.NaN, double.NaN);

        private void Awake()
        {
            if (playerPda == null)
                playerPda = ResolvePlayerPdaInParents(transform);

            CachePlayerRuntimeContext(GlobalRegistry.Player);

            labelFont = LocalizedFontResolver.ResolveReadableFont(labelFont);
            numericFont = LocalizedFontResolver.ResolveNumericFont(numericFont, labelFont);
        }

        private void OnEnable()
        {
            TryRegisterHotSwapListener();
            CachePlayerRuntimeContext(GlobalRegistry.Player);
            EnsureBuilt();
            PDAEvents.Register(this);
            EvaluateTickRegistration();
            QueueTerminalRefresh(force: true);
        }

        private void OnDisable()
        {
            PDAEvents.Unregister(this);
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            PDAEvents.Unregister(this);
            PDAEvents.AssertUnregistered(this, nameof(PDADiagnosticTerminal));
            UnregisterFromTickManager();
            TryUnregisterHotSwapListener();
        }

        public void SlowTick()
        {
            if (!IsDiagnosticsVisible())
                return;

            QueueTerminalRefresh(force: false);
        }

        public void LateFrameTick()
        {
            if (!_terminalRefreshDirty || !IsDiagnosticsVisible())
                return;

            bool force = _terminalForceRefresh;
            _terminalRefreshDirty = false;
            _terminalForceRefresh = false;
            RefreshTerminal(force);
        }

        public void OnPDAEvent(in PDAEventPayload payload)
        {
            switch ((PDAEventType)payload.EventType)
            {
                case PDAEventType.Opened:
                    HandlePdaStateChanged(payload.CurrentTab);
                    break;
                case PDAEventType.Closed:
                    HandlePdaClosed(payload.DurationSeconds);
                    break;
                case PDAEventType.TabChanged:
                    HandlePdaTabChanged(payload.PreviousTab, payload.CurrentTab);
                    break;
            }
        }

        private void HandlePdaStateChanged(int initialTab)
        {
            EvaluateTickRegistration();
            if (initialTab == DiagnosticsTabIndex)
                QueueTerminalRefresh(force: true);
        }

        private void HandlePdaClosed(float openDuration)
        {
            UnregisterFromTickManager();
        }

        private void HandlePdaTabChanged(int previousTab, int newTab)
        {
            EvaluateTickRegistration();
            if (newTab == DiagnosticsTabIndex)
                QueueTerminalRefresh(force: true);
        }

        private void QueueTerminalRefresh(bool force)
        {
            _terminalRefreshDirty = true;
            _terminalForceRefresh |= force;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;

            if (!TryGetComponent(out RectTransform root))
                return;

            if (!TryGetComponent(out Image background))
                background = gameObject.AddComponent<Image>();
            background.color = BackgroundColor;

            if (!TryGetComponent(out _group))
                _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            CreateRule(root, "RuleTop", -54f);
            CreateRule(root, "RuleBottom", -118f);

            _titleLabel = CreateText(root, "Title", labelFont, 12f, FontStyles.Bold, TextAlignmentOptions.TopLeft, TitleColor);
            Anchor(_titleLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -14f), new Vector2(-16f, -42f));
            _titleLabel.SetCharArray(TitleTextBuffer, 0, TitleTextBuffer.Length);

            _bodyLabel = CreateText(root, "Body", numericFont != null ? numericFont : labelFont, 15f, FontStyles.Normal, TextAlignmentOptions.TopLeft, BodyColor);
            _bodyLabel.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(_bodyLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, -72f), new Vector2(-16f, -236f));
            _bodyLabel.SetCharArray(Array.Empty<char>(), 0, 0);

            _built = true;
        }

        private void RefreshTerminal(bool force)
        {
            if (!_built || _bodyLabel == null)
                return;

            ResolveDiagnosticsSources();
            int fps = Mathf.RoundToInt(1f / Mathf.Max(0.0001f, SystemDispatcher.CurrentFrameUnscaledDeltaTime));
            long totalMemoryBytes = GC.GetTotalMemory(false);
            int memoryMb = (int)(totalMemoryBytes / (1024L * 1024L));
            int boidCount = _microFaunaBoids != null ? _microFaunaBoids.BoidCount : 0;
            int hullStressPercent = _playerMovement != null
                ? Mathf.RoundToInt(Mathf.Clamp01(_playerMovement.CurrentHullStress01) * 100f)
                : 0;
            double3 universeOffsetDouble = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (math.lengthsq(universeOffsetDouble) <= 0.000000001d)
                universeOffsetDouble = HectonMapMagicVegetationBridge.GlobalTotalUniverseOffsetDouble;

            if (!force &&
                fps == _lastFps &&
                memoryMb == _lastMemoryMb &&
                boidCount == _lastBoidCount &&
                hullStressPercent == _lastHullStressPercent &&
                math.all(universeOffsetDouble == _lastUniverseOffset))
            {
                return;
            }

            _lastFps = fps;
            _lastMemoryMb = memoryMb;
            _lastBoidCount = boidCount;
            _lastHullStressPercent = hullStressPercent;
            _lastUniverseOffset = universeOffsetDouble;

            Span<char> buffer = _diagnosticTextBuffer.AsSpan();
            int cursor = 0;
            bool written =
                TryAppend(buffer, ref cursor, "GC RESERVED  ".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, memoryMb) &&
                TryAppendLine(buffer, ref cursor, " MB".AsSpan()) &&
                TryAppend(buffer, ref cursor, "FRAME RATE   ".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, fps) &&
                TryAppendLine(buffer, ref cursor, " FPS".AsSpan()) &&
                TryAppend(buffer, ref cursor, "BOIDS LIVE   ".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, boidCount) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppend(buffer, ref cursor, "HULL STRESS  ".AsSpan()) &&
                TryAppendInt(buffer, ref cursor, hullStressPercent) &&
                TryAppendLine(buffer, ref cursor, "%".AsSpan()) &&
                TryAppend(buffer, ref cursor, "UNIV OFFSET  ".AsSpan()) &&
                TryAppendSignedRoundedVector(buffer, ref cursor, universeOffsetDouble) &&
                TryAppendNewLine(buffer, ref cursor) &&
                TryAppendLine(buffer, ref cursor, "SLOW TICK    2 HZ".AsSpan()) &&
                TryAppend(buffer, ref cursor, "STATUS       ONLINE".AsSpan());

            if (written)
                _bodyLabel.SetCharArray(_diagnosticTextBuffer, 0, cursor);
        }

        private void ResolveDiagnosticsSources()
        {
            ResolvePlayerMovementFromRuntimeContext();

            if (_microFaunaBoids == null)
                _microFaunaBoids = SargassumMicroFaunaBoids.ActiveRuntimeInstance;
        }

        private void ResolvePlayerMovementFromRuntimeContext()
        {
            if (_playerMovement != null)
                return;

            IPlayerRuntimeContext playerContext = _playerRuntimeContext;
            if (playerContext != null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private bool IsDiagnosticsVisible()
        {
            return isActiveAndEnabled &&
                   gameObject.activeInHierarchy &&
                   PlayerPDA.IsOpen &&
                   playerPda != null &&
                   playerPda.ActiveTab == DiagnosticsTabIndex;
        }

        private void EvaluateTickRegistration()
        {
            if (IsDiagnosticsVisible())
                RegisterToTickManager();
            else
                UnregisterFromTickManager();
        }

        private void RegisterToTickManager()
        {
            if (!Application.isPlaying)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void UnregisterFromTickManager()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _registered = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _registeredLateFrame = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                CachePlayerRuntimeContext(currentService as IPlayerRuntimeContext);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _registered = false;
                _registeredLateFrame = false;
                EvaluateTickRegistration();
            }
        }

        private void CachePlayerRuntimeContext(IPlayerRuntimeContext playerRuntimeContext)
        {
            _playerRuntimeContext = playerRuntimeContext;
            _playerMovement = _playerRuntimeContext != null ? _playerRuntimeContext.PlayerMovement : null;
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

        private static void CreateRule(RectTransform parent, string name, float anchoredY)
        {
            // COLD ALLOC: GameObject[1] — PDA diagnostics divider rule — owner: PDADiagnosticTerminal
            GameObject ruleObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            ruleObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)ruleObject.transform;
            rect.SetParent(parent, false);
            Anchor(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(16f, anchoredY - 1f), new Vector2(-16f, anchoredY + 1f));
            ruleObject.TryGetComponent(out Image image);
            image.color = RuleColor;
            image.raycastTarget = false;
        }

        private static TextMeshProUGUI CreateText(
            RectTransform parent,
            string name,
            TMP_FontAsset font,
            float fontSize,
            FontStyles fontStyle,
            TextAlignmentOptions alignment,
            Color color)
        {
            // COLD ALLOC: GameObject[1] — PDA diagnostics TMP label — owner: PDADiagnosticTerminal
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            textObject.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)textObject.transform;
            rect.SetParent(parent, false);

            TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            Hecton8.UI.TMP_TextRegistry.EnsureRegistered(text);
            return text;
        }

        private static PlayerPDA ResolvePlayerPdaInParents(Transform start)
        {
            Transform current = start;
            while (current != null)
            {
                if (current.TryGetComponent(out PlayerPDA playerPda))
                    return playerPda;

                current = current.parent;
            }

            return null;
        }

        private static bool TryAppendSignedRoundedVector(Span<char> buffer, ref int cursor, double3 value)
        {
            return TryAppend(buffer, ref cursor, "[".AsSpan()) &&
                TryAppendSignedRounded(buffer, ref cursor, value.x) &&
                TryAppend(buffer, ref cursor, ",".AsSpan()) &&
                TryAppendSignedRounded(buffer, ref cursor, value.y) &&
                TryAppend(buffer, ref cursor, ",".AsSpan()) &&
                TryAppendSignedRounded(buffer, ref cursor, value.z) &&
                TryAppend(buffer, ref cursor, "]".AsSpan());
        }

        private static bool TryAppendSignedRounded(Span<char> buffer, ref int cursor, double value)
        {
            if (!math.isfinite(value))
                return TryAppend(buffer, ref cursor, "NaN".AsSpan());

            double roundedDouble = math.round(value);
            long rounded;
            if (roundedDouble >= (double)long.MaxValue)
                rounded = long.MaxValue;
            else if (roundedDouble <= (double)long.MinValue)
                rounded = long.MinValue;
            else
                rounded = (long)roundedDouble;
            if (rounded >= 0 && !TryAppend(buffer, ref cursor, "+".AsSpan()))
                return false;

            return TryAppendLong(buffer, ref cursor, rounded);
        }

        private static bool TryAppendLine(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            return TryAppend(buffer, ref cursor, value) && TryAppendNewLine(buffer, ref cursor);
        }

        private static bool TryAppendNewLine(Span<char> buffer, ref int cursor)
        {
            if (cursor < 0 || cursor >= buffer.Length)
                return false;

            buffer[cursor++] = '\n';
            return true;
        }

        private static bool TryAppendInt(Span<char> buffer, ref int cursor, int value)
        {
            if ((uint)cursor > (uint)buffer.Length ||
                !value.TryFormat(buffer.Slice(cursor), out int written))
            {
                return false;
            }

            cursor += written;
            return true;
        }

        private static bool TryAppendLong(Span<char> buffer, ref int cursor, long value)
        {
            if ((uint)cursor > (uint)buffer.Length ||
                !value.TryFormat(buffer.Slice(cursor), out int written))
            {
                return false;
            }

            cursor += written;
            return true;
        }

        private static bool TryAppend(Span<char> buffer, ref int cursor, ReadOnlySpan<char> value)
        {
            if (cursor < 0 || cursor + value.Length > buffer.Length)
                return false;

            value.CopyTo(buffer.Slice(cursor));
            cursor += value.Length;
            return true;
        }

        private static void Anchor(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            if (rect == null)
                return;

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}


