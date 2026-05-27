// ============================================================================
// HECTON-8 - LaserCutter.cs v2.2
// Laser cutter - PlayerTool with thermal management.
//
// v2.2 CHANGES (ZERO-GC REFACTOR):
//   [ZERO-GC] Diagnosis system entirely refactored to use FixedCharBuffer.
//     - Eliminated managed formatting in diagnosis and operational summaries.
//     - Removed legacy CutterDiagnosis fields (headline/summary) in favor of persistent buffers.
//     - Consolidated state management and removed clobbered field declarations.
//
//   [OPT] Player inventory resolve moved out of hot loop (EnsurePlayerInventory)
//     to one-time initialization in Awake().
//
// ============================================================================

namespace Hecton8.Gameplay
{
    using System;
    using Hecton8.Core;
    using Hecton8.Core.Contracts;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Interaction;
    using Hecton8.Inventory;
    using Hecton.Localization;
    using Hecton8.Scavenging;
    using Hecton8.Tools;
    using Hecton8.World;
    using EquipmentInteractionPacket = Hecton8.Interaction.InteractionPacket;
    using EquipmentInteractionSignal = Hecton8.Interaction.InteractionSignal;
    using LaserCutterEventPayloadSignal = Hecton8.Core.Contracts.Signals.LaserCutterEventPayload;
    using LaserCutterEventTypeSignal = Hecton8.Core.Contracts.Signals.LaserCutterEventType;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.Audio;

    /// <summary>
    /// Listener contract for deferred laser cutter events.
    /// </summary>
    public interface ILaserCutterEventListener
    {
        /// <summary>
        /// Receives a laser cutter event during <see cref="SystemDispatcher"/> LateUpdate.
        /// </summary>
        /// <param name="payload">Blittable cutter event payload.</param>
        void OnLaserCutterEvent(in LaserCutterEventPayloadSignal payload);
    }

    /// <summary>
    /// Typed-lane laser cutter event bridge with a sidecar source registry for live transform resolution.
    /// </summary>
    public static class LaserCutterEvents
    {
        private static int s_x001DirectSignalPushDropCount_LaserCutter;

        private static int s_x001LaserCutterSignalPushDropCount;
        private const int PendingEventCapacity = 16;
        private const int ListenerCapacity = 8;
        private const int SourceCapacity = 8;

        private struct SourceRecord
        {
            public LaserCutter Source;
            public int CutterInstanceId;
            public Transform CachedTransform;
        }

        private struct ListenerSlot
        {
            public ILaserCutterEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct LaserCutterListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public LaserCutterListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int ReadCount()
            {
                return _count;
            }

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ILaserCutterEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ILaserCutterEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                if (Contains(listener))
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void Unregister(ILaserCutterEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public ILaserCutterEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - cutter listeners drained by SystemDispatcher LateUpdate - owner: LaserCutterEvents
        private static LaserCutterListenerRegistry _listeners = new LaserCutterListenerRegistry(ListenerCapacity);
        // COLD ALLOC: SourceRecord[8] - cutter source sidecar for live Transform resolution - owner: LaserCutterEvents
        private static readonly SourceRecord[] _sources = new SourceRecord[SourceCapacity];
        private static int _pendingEventCount;
        private static int _sourceCount;
        private static bool _laneConfigured;

        /// <summary>
        /// Pending payload count in the cutter event lane.
        /// </summary>
        public static int ReadPendingCount()
        {
            return _pendingEventCount;
        }

        /// <summary>
        /// Registers a cutter event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(ILaserCutterEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.TryRegister(listener);
        }

        /// <summary>
        /// Unregisters a cutter event listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(ILaserCutterEventListener listener)
        {
            if (listener == null || !_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
        }

        /// <summary>
        /// Flushes queued cutter events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_laneConfigured && _pendingEventCount <= 0)
                return;

            if (!_laneConfigured)
            {
                _pendingEventCount = 0;
                return;
            }

            ReadOnlySpan<LaserCutterEventPayloadSignal> payloads = SignalBus<LaserCutterEventPayloadSignal>.GetFrameSnapshot();
            if (payloads.Length <= 0)
                return;

            if (_listeners.ReadCount() <= 0)
            {
                _pendingEventCount = math.max(0, _pendingEventCount - payloads.Length);
                return;
            }

            int dispatchedCount = 0;
            for (int eventIndex = 0; eventIndex < payloads.Length; eventIndex++)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                {
                    RequeueRemaining(payloads, eventIndex);
                    _pendingEventCount = math.max(0, _pendingEventCount - dispatchedCount);
                    return;
                }

                LaserCutterEventPayloadSignal payload = payloads[eventIndex];
                int count = _listeners.ReadCount();
                for (int i = count - 1; i >= 0; i--)
                {
                    ILaserCutterEventListener listener = _listeners.GetAt(i);
                    if (listener != null)
                        listener.OnLaserCutterEvent(in payload);
                }

                dispatchedCount++;
            }

            _pendingEventCount = math.max(0, _pendingEventCount - dispatchedCount);
        }

        /// <summary>
        /// Resolves a live cutter transform from the sidecar source registry.
        /// </summary>
        /// <param name="cutterInstanceId">Runtime entity id hash of the cutter source.</param>
        /// <param name="cutterTransform">Resolved live transform, if present.</param>
        /// <returns>True when the source is still registered and has a transform.</returns>
        public static bool TryResolveCutterTransform(int cutterInstanceId, out Transform cutterTransform)
        {
            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                SourceRecord record = _sources[i];
                if (record.Source == null || record.CutterInstanceId != cutterInstanceId)
                    continue;

                cutterTransform = record.CachedTransform;
                return cutterTransform != null;
            }

            cutterTransform = null;
            return false;
        }

        internal static void EnsureInitialized()
        {
            if (_laneConfigured)
                return;

            SignalBus<LaserCutterEventPayloadSignal>.EnsureInitialized();
            _laneConfigured = true;
        }

        internal static void RegisterSource(LaserCutter source, int cutterInstanceId, Transform cachedTransform)
        {
            if (source == null || cutterInstanceId == 0)
                return;

            EnsureInitialized();
            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                if (_sources[i].Source != source)
                    continue;

                _sources[i] = new SourceRecord
                {
                    Source = source,
                    CutterInstanceId = cutterInstanceId,
                    CachedTransform = cachedTransform
                };
                return;
            }

            if (_sourceCount >= SourceCapacity)
                return;

            _sources[_sourceCount] = new SourceRecord
            {
                Source = source,
                CutterInstanceId = cutterInstanceId,
                CachedTransform = cachedTransform
            };
            _sourceCount++;
        }

        internal static void UnregisterSource(LaserCutter source)
        {
            if (source == null || _sourceCount <= 0)
                return;

            for (int i = _sourceCount - 1; i >= 0; i--)
            {
                if (_sources[i].Source != source)
                    continue;

                int lastIndex = _sourceCount - 1;
                _sources[i] = _sources[lastIndex];
                _sources[lastIndex] = default;
                _sourceCount = lastIndex;
                return;
            }
        }

        internal static bool TryRaiseHeatChanged(float heat01, int cutterInstanceId, int rootInstanceId)
        {
            return Enqueue(new LaserCutterEventPayloadSignal
            {
                Heat01 = math.saturate(heat01),
                CutterInstanceId = cutterInstanceId,
                CutterRootInstanceId = rootInstanceId,
                EventType = (ushort)LaserCutterEventTypeSignal.HeatChanged,
                StateFlags = 0
            });
        }

        [Obsolete("Use TryRaiseHeatChanged so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseHeatChanged(float heat01, int cutterInstanceId, int rootInstanceId)
            => TryRaiseHeatChanged(heat01, cutterInstanceId, rootInstanceId);

        internal static bool TryRaiseBeamStateChanged(int cutterInstanceId, int rootInstanceId, bool isActive)
        {
            return Enqueue(new LaserCutterEventPayloadSignal
            {
                Heat01 = 0f,
                CutterInstanceId = cutterInstanceId,
                CutterRootInstanceId = rootInstanceId,
                EventType = (ushort)LaserCutterEventTypeSignal.BeamStateChanged,
                StateFlags = isActive ? LaserCutterEventPayloadSignal.StateFlagBeamActive : (ushort)0
            });
        }

        [Obsolete("Use TryRaiseBeamStateChanged so bounded queue refusal is visible at the producer.", true)]
        internal static void RaiseBeamStateChanged(int cutterInstanceId, int rootInstanceId, bool isActive)
            => TryRaiseBeamStateChanged(cutterInstanceId, rootInstanceId, isActive);

        /// <summary>
        /// Tests the beam-active flag in a cutter event payload.
        /// </summary>
        /// <param name="payload">Payload to inspect.</param>
        /// <returns>True when the payload marks the cutter beam active.</returns>
        public static bool IsBeamActive(in LaserCutterEventPayloadSignal payload)
        {
            return (payload.StateFlags & LaserCutterEventPayloadSignal.StateFlagBeamActive) != 0;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _listeners.Clear();
            for (int i = 0; i < _sourceCount; i++)
                _sources[i] = default;

            _sourceCount = 0;
            _pendingEventCount = 0;
            _laneConfigured = false;
        }

        private static bool Enqueue(in LaserCutterEventPayloadSignal payload)
        {
            if (payload.CutterInstanceId == 0 || payload.CutterRootInstanceId == 0)
                return false;

            if (!_laneConfigured)
                return false;

            if (_pendingEventCount >= PendingEventCapacity)
                return false;

            if (!SignalBus<LaserCutterEventPayloadSignal>.TryPushTracked(in payload, ref s_x001DirectSignalPushDropCount_LaserCutter))
                return false;

            _pendingEventCount++;
            return true;
        }

        private static void RequeueRemaining(ReadOnlySpan<LaserCutterEventPayloadSignal> payloads, int startIndex)
        {
            if (startIndex < 0 || startIndex >= payloads.Length)
                return;

            for (int i = startIndex; i < payloads.Length; i++)
            {
                LaserCutterEventPayloadSignal payload = payloads[i];
                SignalBus<LaserCutterEventPayloadSignal>.TryPushTracked(in payload, ref s_x001LaserCutterSignalPushDropCount);
            }
        }
    }

    internal static class LaserCutterTargetRegistry
    {
        private const int TargetCapacity = 4096;
        private const int TargetMask = TargetCapacity - 1;
        private const int ModuleTraversalCapacity = TargetCapacity;
        private const int ParentComponentResolveDepth = 32;
        private const byte SlotEmpty = 0;
        private const byte SlotOccupied = 1;
        private const byte SlotDeleted = 2;

        // COLD ALLOC: collider id table - lifecycle-owned target cache for cutter hit routes - owner: LaserCutterTargetRegistry
        private static readonly ulong[] s_keys = new ulong[TargetCapacity];
        // COLD ALLOC: IWfcDoorLaserCutTarget[4096] - WFC door target cache values - owner: LaserCutterTargetRegistry
        private static readonly IWfcDoorLaserCutTarget[] s_doors = new IWfcDoorLaserCutTarget[TargetCapacity];
        // COLD ALLOC: BaseModule[4096] - salvage module target cache values - owner: LaserCutterTargetRegistry
        private static readonly BaseModule[] s_modules = new BaseModule[TargetCapacity];
        // COLD ALLOC: byte[4096] - open-address slot states - owner: LaserCutterTargetRegistry
        private static readonly byte[] s_states = new byte[TargetCapacity];
        // COLD ALLOC: Transform[4096] - fixed stack for module lifecycle collider registration - owner: LaserCutterTargetRegistry
        private static readonly Transform[] s_moduleTraversalStack = new Transform[ModuleTraversalCapacity];

        internal static void RegisterDoor(IWfcDoorLaserCutTarget door, Collider collider)
        {
            if (door == null || collider == null)
                return;

            int slot = FindSlot(ResolveColliderKey(collider), true);
            if (slot < 0)
                return;

            s_doors[slot] = door;
        }

        internal static void UnregisterDoor(IWfcDoorLaserCutTarget door, Collider collider)
        {
            if (door == null || collider == null)
                return;

            int slot = FindSlot(ResolveColliderKey(collider), false);
            if (slot < 0 || !ReferenceEquals(s_doors[slot], door))
                return;

            s_doors[slot] = null;
            ClearSlotIfUnowned(slot);
        }

        internal static void RegisterModuleTree(BaseModule module)
        {
            if (module == null)
                return;

            RegisterModuleColliderTree(module);
        }

        internal static void UnregisterModuleTree(BaseModule module)
        {
            if (module == null)
                return;

            UnregisterModuleSlots(module);
        }

        internal static bool TryResolveDoor(Collider collider, out IWfcDoorLaserCutTarget door)
        {
            door = null;
            if (collider == null)
                return false;

            int slot = FindSlot(ResolveColliderKey(collider), false);
            if (slot < 0)
                return false;

            door = s_doors[slot];
            return door != null;
        }

        internal static bool TryResolveModule(Collider collider, out BaseModule module)
        {
            module = null;
            if (collider == null)
                return false;

            int slot = FindSlot(ResolveColliderKey(collider), false);
            if (slot >= 0)
            {
                module = s_modules[slot];
                if (module != null)
                    return true;
            }

            if (!TryResolveParentComponent(collider.transform, out module))
                return false;

            RegisterModule(module, collider);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            for (int i = 0; i < TargetCapacity; i++)
            {
                s_keys[i] = 0UL;
                s_doors[i] = null;
                s_modules[i] = null;
                s_states[i] = SlotEmpty;
                s_moduleTraversalStack[i] = null;
            }
        }

        private static void RegisterModuleColliderTree(BaseModule module)
        {
            Transform root = module != null ? module.transform : null;
            if (root == null)
                return;

            int stackCount = 0;
            TryPushTraversal(root, ref stackCount);

            while (stackCount > 0)
            {
                Transform current = PopTraversal(ref stackCount);
                if (current == null)
                    continue;

                if (current.TryGetComponent(out Collider collider))
                    RegisterModule(module, collider);

                int childCount = current.childCount;
                for (int childIndex = 0; childIndex < childCount; childIndex++)
                {
                    if (!TryPushTraversal(current.GetChild(childIndex), ref stackCount))
                        break;
                }
            }
        }

        private static void UnregisterModuleSlots(BaseModule module)
        {
            for (int i = 0; i < TargetCapacity; i++)
            {
                if (!ReferenceEquals(s_modules[i], module))
                    continue;

                s_modules[i] = null;
                ClearSlotIfUnowned(i);
            }
        }

        private static bool TryPushTraversal(Transform transform, ref int stackCount)
        {
            if (transform == null || stackCount >= ModuleTraversalCapacity)
                return false;

            s_moduleTraversalStack[stackCount] = transform;
            stackCount++;
            return true;
        }

        private static Transform PopTraversal(ref int stackCount)
        {
            stackCount--;
            Transform transform = s_moduleTraversalStack[stackCount];
            s_moduleTraversalStack[stackCount] = null;
            return transform;
        }

        private static bool TryResolveParentComponent<T>(Transform start, out T component)
            where T : Component
        {
            Transform current = start;
            for (int depth = 0; current != null && depth < ParentComponentResolveDepth; depth++)
            {
                if (current.TryGetComponent(out component))
                    return true;

                current = current.parent;
            }

            component = null;
            return false;
        }

        private static void RegisterModule(BaseModule module, Collider collider)
        {
            if (module == null || collider == null)
                return;

            int slot = FindSlot(ResolveColliderKey(collider), true);
            if (slot < 0)
                return;

            s_modules[slot] = module;
        }

        private static void UnregisterModule(BaseModule module, Collider collider)
        {
            if (module == null || collider == null)
                return;

            int slot = FindSlot(ResolveColliderKey(collider), false);
            if (slot < 0 || !ReferenceEquals(s_modules[slot], module))
                return;

            s_modules[slot] = null;
            ClearSlotIfUnowned(slot);
        }

        private static int FindSlot(ulong key, bool create)
        {
            if (key == 0UL)
                return -1;

            int index = HashKey(key);
            int firstReusable = -1;
            for (int probe = 0; probe < TargetCapacity; probe++)
            {
                byte state = s_states[index];
                if (state == SlotOccupied)
                {
                    if (s_keys[index] == key)
                        return index;
                }
                else
                {
                    if (firstReusable < 0)
                        firstReusable = index;

                    if (state == SlotEmpty)
                        break;
                }

                index = (index + 1) & TargetMask;
            }

            if (!create || firstReusable < 0)
                return -1;

            s_keys[firstReusable] = key;
            s_states[firstReusable] = SlotOccupied;
            return firstReusable;
        }

        private static void ClearSlotIfUnowned(int slot)
        {
            if ((uint)slot >= (uint)TargetCapacity || s_doors[slot] != null || s_modules[slot] != null)
                return;

            s_keys[slot] = 0UL;
            s_states[slot] = SlotDeleted;
        }

        private static ulong ResolveColliderKey(Collider collider)
        {
            return collider != null ? EntityId.ToULong(collider.GetEntityId()) : 0UL;
        }

        private static int HashKey(ulong key)
        {
            unchecked
            {
                key ^= key >> 33;
                key *= 0xff51afd7ed558ccdUL;
                key ^= key >> 33;
                key *= 0xc4ceb9fe1a85ec53UL;
                key ^= key >> 33;
                return (int)key & TargetMask;
            }
        }
    }

    [DisallowMultipleComponent]
    public sealed class LaserCutter : PlayerTool, IToolModule, ILateFrameTickable, ILocalizationLanguageChangedListener, IGlobalRegistryHotSwapListener
    {
        private static int s_x001LaserCutterSignalPushDropCount;
        private const string CutterCategory = "CUTTER";
        private const int RecoveryProgressMaxPercent = 100;
        private const float MaxRecoilImpulse = 12f;
        private const float MinEffectiveBeamPower = 0.02f;
        private const float LowPowerThresholdNormalized = 0.12f;
        private const float LowPowerOutputScale = 0.35f;
        private const float InvTau = 0.15915494f;
        private const float LaserJitterSecondaryScale = 1.37f;
        private const float LaserJitterSecondaryOffset = 2.1f;
        private const float QuaternionHalfSqrtTwo = 0.70710678f;
        private const float ShaderFloatPublishEpsilon = 0.0001f;
        private static int _WaterLayer = int.MinValue;
        private static int _TransparentFxLayer = int.MinValue;
        private const byte IdleState = (byte)ToolStateBits.Idle;
        private const byte ActiveState = (byte)ToolStateBits.Active;
        private const byte BusyState = (byte)ToolStateBits.Busy;
        private const byte OverheatedState = (byte)ToolStateBits.Overheated;
        private const byte LowPowerState = (byte)ToolStateBits.LowPower;
        private const byte CooldownState = (byte)ToolStateBits.Cooldown;
        private const byte DiagnosisSeverityInfo = 0;
        private const byte DiagnosisSeverityWarn = 1;
        private const byte DiagnosisSeverityCritical = 2;
        private const uint DiagnosisDisplayFrameWindow = 45u;

        private struct CutterDiagnosis
        {
            public byte Severity;
        }

        private static readonly int _LaserHitHeatId = Shader.PropertyToID("_LaserHitHeat");
        private static readonly Quaternion _SparkRotationForward = Quaternion.identity;
        private static readonly Quaternion _SparkRotationBack = new Quaternion(0f, 1f, 0f, 0f);
        private static readonly Quaternion _SparkRotationRight = new Quaternion(0f, QuaternionHalfSqrtTwo, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationLeft = new Quaternion(0f, -QuaternionHalfSqrtTwo, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationUp = new Quaternion(-QuaternionHalfSqrtTwo, 0f, 0f, QuaternionHalfSqrtTwo);
        private static readonly Quaternion _SparkRotationDown = new Quaternion(QuaternionHalfSqrtTwo, 0f, 0f, QuaternionHalfSqrtTwo);
        private bool _lateFrameRegistered;
        private bool _pendingVisualActiveDirty;
        private bool _pendingVisualActive;
        private bool _pendingLaserLineDirty;
        private bool _pendingLaserLineDidHit;
        private bool _pendingCutAudioDirty;
        private bool _pendingCutAudioShouldPlay;
        private bool _pendingOverheatLockoutCueDirty;
        private bool _pendingLaserHeatOutputDirty;
        private bool _pendingWfcCutDecalDirty;
        private bool _pendingWfcCutDecalActive;
        private Vector3 _pendingWfcCutDecalPosition;
        private Quaternion _pendingWfcCutDecalRotation;
        private Vector3 _pendingWfcCutDecalScale;

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LASER SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Laser Settings ────────────────────────────")]
        [Tooltip("Maximum beam range (meters).")]
        [SerializeField] private float maxRange = 5f;

        [Tooltip("Base damage per second when cutting.")]
        [SerializeField] private float damagePerSecond = 25f;

        [Tooltip("LayerMask for typed surface targets.")]
        [SerializeField] private LayerMask cuttableLayer = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Header("── Heat Management ───────────────────────────")]
        [Tooltip("Seconds of continuous firing to reach overheat (heat 0→1).")]
        [SerializeField] private float overheatTime = 5f;

        [Tooltip("Heat units lost per second when NOT firing.\n0.3 = full cooldown from max in ~3.3 seconds.")]
        [SerializeField] private float cooldownRate = 0.3f;

        [Tooltip("Lockout duration after overheat (seconds).\nTool is completely disabled during this time.")]
        [SerializeField] private float overheatLockoutTime = 2f;

        [Tooltip("Bonus damage multiplier at maximum heat.\n0.15 = 15% more damage when red-hot.\nRisk/reward: more efficient but lockout is the cost.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float heatDamageBonus = 0.15f;

        [Tooltip("Base recoil impulse used for deferred player-body kickback.")]
        [SerializeField, Range(0f, 12f)] private float recoilImpulseBase = 4f;

        [Tooltip("Additional recoil damping applied while submerged.")]
        [SerializeField, Range(0.1f, 1f)] private float submergedRecoilScale = 0.6f;

        [Tooltip("Thermal coupling scale that converts cutter damage units into seawater heat energy for localized boil anomalies.")]
        [SerializeField, Min(0f)] private float waterHeatCouplingScale = 250000f;

        [Header("── Beam Visual ───────────────────────────────")]
        [Tooltip("Maximum jitter amplitude at full heat (meters).\nBeam endpoint vibrates more as tool heats up.")]
        [SerializeField] private float maxJitterAmplitude = 0.008f;

        [Tooltip("Jitter frequency (Hz). Higher = faster vibration.")]
        [SerializeField] private float jitterFrequency = 50f;

        [Header("── Deconstruction ────────────────────────────")]
        [Tooltip("Seconds of continuous cutting to fully deconstruct a module.\nProgress resets if target changes or R/LKM released.")]
        [SerializeField] private float deconstructThreshold = 3f;
        [Tooltip("Normalized spring load required before salvage recovery progress can move.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredTension01 = 0.42f;
        [Tooltip("Normalized pull-back intent required to tear a heavy module free while cutting.")]
        [SerializeField, Range(0f, 1f)] private float heavySalvageRequiredPull01 = 0.36f;
        [Tooltip("Velocity away from the cut seam that counts as full pull intent.")]
        [SerializeField, Range(0.1f, 6f)] private float heavySalvagePullVelocityForFullIntent = 1.75f;
        [Tooltip("Retracts the cutter anchor slightly into the surface so the spring loads against the seam instead of hovering in open air.")]
        [SerializeField, Range(0f, 0.2f)] private float heavySalvageAnchorRetraction = 0.03f;

        [Header("── Visual References ─────────────────────────")]
        [Tooltip("LineRenderer for beam visualization.")]
        [SerializeField] private LineRenderer laserLine;

        [Tooltip("Optional low-tier glowing decal proxy for WFC sealed-door cuts.")]
        [SerializeField] private Transform wfcCutDecalProxy;

        [Tooltip("Optional renderer on the low-tier WFC cut decal proxy.")]
        [SerializeField] private Renderer wfcCutDecalRenderer;

        [Tooltip("Maximum decal scale for completed WFC sealed-door cuts.")]
        [SerializeField, Range(0.05f, 1.25f)] private float wfcCutDecalMaxScale = 0.55f;

        [Header("── Audio ─────────────────────────────────────")]
        [Tooltip("Looping AudioSource for cutting sound.")]
        [SerializeField] private AudioSource cutAudio;

        [Tooltip("Sound played when attempting to fire during overheat lockout.")]
        [SerializeField] private AudioClip overheatErrorClip;

        [Tooltip("Base pitch of cutting audio (at zero heat).")]
        [SerializeField] private float basePitch = 1.0f;

        [Tooltip("Maximum pitch of cutting audio (at full heat).")]
        [SerializeField] private float maxPitch = 1.3f;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>Typed surface hit result (reused, zero GC).</summary>
        private InteractionSurfaceHit _hitInfo;

        /// <summary>Cached diagnosis result (reused, zero GC).</summary>
        private CutterDiagnosis _cachedDiagnosis;

        /// <summary>Is cached diagnosis still within its explicit secondary-fire display window.</summary>
        private bool _diagnosisCached;
        private uint _diagnosisFrame;

        /// <summary>Is beam active this frame.</summary>
        private bool _isFiring;

        /// <summary>Was beam active last frame (for toggle VFX).</summary>
        private bool _wasFiringLastFrame;

        // ── Heat State ──

        /// <summary>
        /// Current heat level [0..1].
        /// 0 = cold, 1 = overheated.
        /// Accumulates during firing, decays during idle.
        /// </summary>
        private float _heatLevel;

        /// <summary>Is tool currently in overheat lockout.</summary>
        private bool _isLockedOut;

        /// <summary>Remaining lockout time in seconds.</summary>
        private float _lockoutTimer;

        /// <summary>Last published heat value (for event throttling).</summary>
        private float _lastPublishedHeat;
        private float _lastPublishedLaserHitHeat = float.NaN;
        private bool _lastPublishedBeamActive;

        /// <summary>Has the error clip been played this lockout cycle.
        /// Prevents spamming the error sound every frame while locked.</summary>
        private bool _lockoutSoundPlayed;

        // ── Deconstruct State ──

        /// <summary>Accumulated deconstruct progress (seconds).</summary>
        private float _deconstructProgress;

        /// <summary>InstanceID of current deconstruct target (-1 = none).</summary>
        private int _cachedDeconstructTargetId = -1;

        /// <summary>Cached BaseModule of current deconstruct target.</summary>
        private BaseModule _cachedDeconstructModule;

        /// <summary>Cached PlayerInventory for Deconstruct calls.</summary>
        private PlayerInventory _cachedInventory;
        private IPlayerCuttingTensionService _cachedCuttingTensionService;
        private IAudioService _cachedAudioService;
        private IAudioResidencyService _cachedAudioResidencyService;
        private AudioMixerGroup _cachedCutAudioMixerGroup;
        private IInputService _cachedInputService;
        private IInteractionSignalService _cachedInteractionService;
        private IHabitatDeconstructionSystem _cachedHabitatDeconstructionSystem;
        private ISargassumCutWriteService _cachedSargassumCutWriter;
        private IOrganicToolHitService _cachedOrganicToolHits;
        private IBabelLocalization _cachedBabelLocalization;
        private ushort _cachedLaserLocalizationLanguageId = ushort.MaxValue;
        private bool _registeredLocalizationListener;
        private bool _registeredHotSwapListener;
        private string _laserCategory;
        private string _laserDiagMessage;
        private string _laserDirectiveHot;
        private string _laserDirectiveLockout;
        private string _laserDirectiveReady;
        private string _laserDirectiveRecovery;
        private string _laserHeadlineCuttableContact;
        private string _laserHeadlineInvalidTarget;
        private string _laserHeadlineModuleLocked;
        private string _laserHeadlineModuleStable;
        private string _laserHeadlineNoTarget;
        private string _laserHudCoreOverheated;
        private string _laserHudCoreStable;
        private string _laserHudOverheatLockout;
        private string _laserHudRecoveryModuleLocked;
        private string _laserHudRecoveryNoModule;
        private string _laserLogOverheatMessage;
        private string _laserLogOverheatTitle;
        private string _laserOperationalDiagnosis;
        private string _laserOperationalHeat;
        private string _laserOperationalLockout;
        private string _laserOperationalReady;
        private string _laserOperationalRecovery;
        private string _laserRecoveryProgress;
        private string _laserSummaryCuttableContact;
        private string _laserSummaryInvalidTarget;
        private string _laserSummaryModuleLocked;
        private string _laserSummaryModuleStable;
        private string _laserSummaryNoTarget;
        private AbsoluteUniversePosition _cachedRuntimeOriginAup;
        private bool _hasCachedRuntimeOriginAup;

        // COLD ALLOC: persistent buffers for diagnosis and telemetry
        private FixedCharBuffer _diagnosisHeadline = new FixedCharBuffer(64);
        private FixedCharBuffer _diagnosisSummary = new FixedCharBuffer(256);
        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - legacy string bridge scratch - owner: LaserCutter
        private FixedCharBuffer _telemetryBuffer = new FixedCharBuffer(512);
        private FixedCharBuffer _recoveryFeedbackBuffer = new FixedCharBuffer(128); // COLD ALLOC: char[128] - cutter recovery HUD feedback scratch - owner: LaserCutter
        private bool _secondaryLatched;
        private bool _deconstructStartReported;
        private bool _deconstructBlockedReported;
        private float _nextProgressFeedbackAt;
        private float _visualClockSeconds;
        private Vector3 _cachedDeconstructAnchorPoint;
        private Vector3 _cachedDeconstructAnchorNormal = Vector3.up;
        private int _cachedWfcDoorTargetId = -1;
        private IWfcDoorLaserCutTarget _cachedWfcDoor;
        private uint _cachedToolId;
        private ulong _surfaceRequesterId;
        private byte _toolStateFlags = IdleState;

        // ── Sparks cache ──


        // ══════════════════════════════════════════════════════════
        //  PUBLIC READ METHODS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Current heat level [0..1]. Read by HUD systems.
        /// 0 = cold, 1 = overheated/locked.
        /// </summary>
        public float ReadHeatLevel()
        {
            return _heatLevel;
        }

        internal override float ResolveModularHeatNormalized()
        {
            return _heatLevel;
        }

        protected override void ConfigureModularRuntimeProfile(ref ToolRuntimeProfile profile)
        {
            profile.MaxRange = Mathf.Max(0.1f, maxRange);
            profile.PowerScalar = Mathf.Max(0.1f, damagePerSecond);
            profile.HeatGenerationRate = math.rcp(math.max(overheatTime, 0.1f));
            profile.CooldownRate = Mathf.Max(0f, cooldownRate);
            profile.RecoilImpulse = Mathf.Max(0f, recoilImpulseBase);
        }

        public bool DebugRecoverModule(BaseModule module)
        {
            if (module == null || !module.CanDeconstruct())
                return false;

            EnsurePlayerInventory();
            if (!TryResolveToolPose(out Vector3 toolOrigin, out Vector3 toolForward, out _))
                return false;

            float debugRange = math.min(GetRuntimeMaxRange(maxRange), 2f);
            Vector3 targetPoint = toolOrigin + toolForward * debugRange;
            if (!TryRequestModuleDeconstruction(module, targetPoint, toolOrigin, toolForward, debugRange, 2))
                return false;

            PublishInfoMessage("LASER CUTTER - RECOVERY QUEUED");
            
            _telemetryBuffer.Clear();
            _telemetryBuffer.Append("Laser-assisted deconstruction queued for habitat rollback validation on target module.");

            FieldOperationLogSystem.RecordOperation(
                StableText(H8ToolLocHashes.LASER_CATEGORY, CutterCategory),
                "MODULE RECOVERY QUEUED",
                _telemetryBuffer,
                "INFO");
            ResetDeconstructState();
            return true;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            EnsureLayerCache();
            CacheToolId();
            CacheSurfaceRequesterId();
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            EnsureDodRuntimesInitialized();
            CacheWfcCutDecalRenderer();
            SetVisualsActive(false);
            TryAssignCutAudioMixerRoute();
            EnsurePlayerBindings();
        }

        private void OnEnable()
        {
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            TryAssignCutAudioMixerRoute();
            RegisterLaserLocalizationRoutes();
            if (Application.isPlaying)
                LaserCutterEvents.RegisterSource(this, ResolveEventCutterId(), transform);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                ClearColdDependencies();
                return;
            }

            PublishBeamState(false);
            LaserCutterEvents.UnregisterSource(this);
            ReleaseEquippedAudio();
            UnregisterLaserLocalizationRoutes();
            ClearPendingLaserVisualSync();
            TryUnregisterLateFrameTick();
            ClearColdDependencies();
        }

        private void OnDestroy()
        {
            if (Application.isPlaying)
                LaserCutterEvents.UnregisterSource(this);

            UnregisterLaserLocalizationRoutes();
            ClearColdDependencies();
        }

        private static void EnsureLayerCache()
        {
            if (_WaterLayer == int.MinValue)
                _WaterLayer = Hecton8.Core.HectonLayerMasks.Water;
            if (_TransparentFxLayer == int.MinValue)
                _TransparentFxLayer = Hecton8.Core.HectonLayerMasks.TransparentFX;
        }

        private void TryAssignCutAudioMixerRoute()
        {
            if (cutAudio == null || cutAudio.outputAudioMixerGroup != null)
                return;

            AudioMixerGroup mixerGroup = _cachedCutAudioMixerGroup;
            if (mixerGroup != null)
                cutAudio.outputAudioMixerGroup = mixerGroup;
        }

        private void CacheColdDependencies()
        {
            _cachedAudioService = GlobalRegistry.Audio;
            _cachedAudioResidencyService = _cachedAudioService as IAudioResidencyService;
            _cachedCutAudioMixerGroup = _cachedAudioService != null ? _cachedAudioService.AmbientGroup : null;
            _cachedInputService = GlobalRegistry.Input;
            _cachedInteractionService = GlobalRegistry.InteractionSignals;
            _cachedHabitatDeconstructionSystem = GlobalRegistry.HabitatDeconstruction;
            _cachedSargassumCutWriter = GlobalRegistry.SargassumCutWrite;
            _cachedOrganicToolHits = GlobalRegistry.OrganicToolHits;
            CacheLaserLocalizationCold();
        }

        private void ClearColdDependencies()
        {
            _cachedAudioService = null;
            _cachedAudioResidencyService = null;
            _cachedCutAudioMixerGroup = null;
            _cachedInputService = null;
            _cachedInteractionService = null;
            _cachedHabitatDeconstructionSystem = null;
            _cachedSargassumCutWriter = null;
            _cachedOrganicToolHits = null;
            _cachedBabelLocalization = null;
            _cachedLaserLocalizationLanguageId = ushort.MaxValue;
            _cachedInventory = null;
            _cachedCuttingTensionService = null;
            _cachedRuntimeOriginAup = default;
            _hasCachedRuntimeOriginAup = false;
        }

        private void RegisterLaserLocalizationRoutes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredLocalizationListener)
            {
                LocalizationEvents.RegisterLanguageListener(this);
                _registeredLocalizationListener = true;
            }

            if (!_registeredHotSwapListener)
                _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void UnregisterLaserLocalizationRoutes()
        {
            if (_registeredLocalizationListener)
            {
                LocalizationEvents.UnregisterLanguageListener(this);
                _registeredLocalizationListener = false;
            }

            if (_registeredHotSwapListener)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwapListener = false;
            }
        }

        public void OnLocalizationLanguageChanged(in LocalizationEventPayload payload)
        {
            RefreshLaserLocalizationCacheCold(_cachedBabelLocalization);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.LocalizationRuntime:
                    RefreshLaserLocalizationCacheCold(currentService as IBabelLocalization);
                    break;
                case GlobalRegistryServiceSlot.SargassumCutRuntime:
                    _cachedSargassumCutWriter = currentService as ISargassumCutWriteService;
                    break;
                case GlobalRegistryServiceSlot.DestructibleOrganicRuntime:
                    _cachedOrganicToolHits = currentService as IOrganicToolHitService;
                    break;
                case GlobalRegistryServiceSlot.VoxelEngineRuntime:
                    LaserCutterDodRuntime.CacheVoxelSdfReadModel(currentService as IVoxelSonarSdfReadModel);
                    break;
                case GlobalRegistryServiceSlot.InteractionSignals:
                    _cachedInteractionService = currentService as IInteractionSignalService;
                    break;
                case GlobalRegistryServiceSlot.HabitatDeconstructionRuntime:
                    _cachedHabitatDeconstructionSystem = currentService as IHabitatDeconstructionSystem;
                    break;
            }
        }

        private static void EnsureDodRuntimesInitialized()
        {
            var vault = GlobalRegistry.DataVault;
            LaserCutterDodRuntime.EnsureInitialized(vault);
            if (WfcLaserCutRuntime.EnsureInitialized(vault))
                WfcLaserCutRuntime.RefreshOwnerPhaseContext();
        }

        private void PrewarmEquippedAudio()
        {
            IAudioResidencyService residency = _cachedAudioResidencyService;
            if (residency == null)
                return;

            residency.PrewarmAudioSource(cutAudio, AudioResidencyDomainIds.Player);
            residency.TouchClip(overheatErrorClip, AudioResidencyDomainIds.Player, true);
        }

        private void ReleaseEquippedAudio()
        {
            IAudioResidencyService residency = _cachedAudioResidencyService;
            if (residency == null)
                return;

            residency.ReleaseAudioSource(cutAudio);
            residency.ReleaseClip(overheatErrorClip);
        }

        public override void OnSpawn()
        {
            base.OnSpawn();
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            RegisterLaserLocalizationRoutes();
            TryAssignCutAudioMixerRoute();
            LaserCutterEvents.RegisterSource(this, ResolveEventCutterId(), transform);
            CacheToolId();
            CacheSurfaceRequesterId();
            EnsureDodRuntimesInitialized();
            ResetAllState();
            SetVisualsActive(false);
        }

        public override void OnDespawn()
        {
            base.OnDespawn();
            PublishBeamState(false);
            LaserCutterEvents.UnregisterSource(this);
            ResetAllState();
            SetVisualsActive(false);
            ReleaseEquippedAudio();
            UnregisterLaserLocalizationRoutes();
            ClearPendingLaserVisualSync();
            TryUnregisterLateFrameTick();
            ClearColdDependencies();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            CacheColdDependencies();
            RefreshCachedRuntimeOriginAup();
            RegisterLaserLocalizationRoutes();
            EnsurePlayerBindings();
            EnsureDodRuntimesInitialized();
            TryAssignCutAudioMixerRoute();
            PrewarmEquippedAudio();
        }

        public override void OnUnequip()
        {
            CancelAction();
            ResetDeconstructState();
            SetVisualsActive(false);
            ReleaseEquippedAudio();
            UnregisterLaserLocalizationRoutes();
            base.OnUnequip();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL ACTIONS
        // ══════════════════════════════════════════════════════════

        public override void UsePrimary(float deltaTime)
        {
            RefreshCachedRuntimeOriginAup();
            SyncCentralThermalBatteryState();

            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }

            if (_isLockedOut)
            {
                SetOverheatedState();
                if (!_lockoutSoundPlayed && overheatErrorClip != null)
                {
                    QueueOverheatLockoutCue();
                    _lockoutSoundPlayed = true;
                    PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_OVERHEAT_LOCKOUT, "LASER CUTTER - OVERHEAT LOCKOUT"));
                }
                return;
            }

            if (!TryBeginToolUse(deltaTime, true))
                return;

            Activate();
            _isFiring = true;
            PublishBeamState(true);
            StageDodSurfaceRequest();

            bool didHit = ProbeCutHitForOwnerAction(out _hitInfo);

            UpdateLaserLine(didHit);
            UpdateSparks(didHit);
            UpdateAudioState(true);

            if (didHit)
            {
                IInputService inputService = _cachedInputService;
                PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                    ? inputService.GetState()
                    : default;
                bool deconstructMode = inputState.HasAction(PlayerInputAction.SecondaryFire);

                if (deconstructMode)
                    ProcessDeconstructMode(deltaTime);
                else
                {
                    ResetDeconstructState();
                    ApplyCutDamage(deltaTime);
                }
            }
            else
            {
                ResetDeconstructState();
                ApplyOpenWaterBoil(deltaTime);
            }

            SyncHeatOutputs();
            PublishHeat();
        }

        public void LateFrameTick()
        {
            if (_pendingVisualActiveDirty)
            {
                _pendingVisualActiveDirty = false;
                ApplyVisualsActive(_pendingVisualActive);
            }

            if (_pendingLaserLineDirty)
            {
                _pendingLaserLineDirty = false;
                ApplyLaserLine(_pendingLaserLineDidHit);
            }

            if (_pendingWfcCutDecalDirty)
            {
                _pendingWfcCutDecalDirty = false;
                ApplyWfcCutDecalVisual();
            }

            if (_pendingCutAudioDirty)
            {
                _pendingCutAudioDirty = false;
                ApplyAudioState(_pendingCutAudioShouldPlay);
            }

            if (_pendingOverheatLockoutCueDirty)
            {
                _pendingOverheatLockoutCueDirty = false;
                ApplyOverheatLockoutCue();
            }

            if (_pendingLaserHeatOutputDirty)
            {
                _pendingLaserHeatOutputDirty = false;
                ApplyHeatOutputs();
            }

            if (!IsEquipped &&
                !_pendingVisualActiveDirty &&
                !_pendingLaserLineDirty &&
                !_pendingWfcCutDecalDirty &&
                !_pendingCutAudioDirty &&
                !_pendingOverheatLockoutCueDirty &&
                !_pendingLaserHeatOutputDirty)
            {
                TryUnregisterLateFrameTick();
            }
        }

        public override void UseSecondary(float deltaTime)
        {
            RefreshCachedRuntimeOriginAup();
            if (IsBroken)
            {
                OnToolBrokenWhileUsing();
                return;
            }
            if (_secondaryLatched)
                return;

            if (!TryBeginToolUse(deltaTime, false))
                return;

            _secondaryLatched = true;

            InteractionSurfaceHit diagHit;
            bool didHit = ProbeCutHitForOwnerAction(out diagHit);

            BuildDiagnosisFromHit(diagHit, didHit, out byte severity);
            _cachedDiagnosis.Severity = severity;
            _diagnosisCached = true;
            _diagnosisFrame = ResolveCurrentFrameId();
            
            PublishDiagnosis();
            string severityText = ResolveDiagnosisSeverityText(severity);
            FieldOperationLogSystem.RecordOperation(
                StableText(H8ToolLocHashes.LASER_CATEGORY, CutterCategory),
                _diagnosisHeadline,
                _diagnosisSummary,
                severityText);
        }

        public override void ToolTick(float deltaTime)
        {
            RefreshCachedRuntimeOriginAup();
            _visualClockSeconds += ClampFiniteDeltaTime(deltaTime);
            SyncCentralThermalBatteryState();

            if (!_isFiring && !_isLockedOut)
            {
                if (_heatLevel > 0f)
                {
                    EnterCooldownState();
                }
                else
                {
                    Deactivate();
                }
            }

            if (_wasFiringLastFrame && !_isFiring)
            {
                PublishBeamState(false);
                SetVisualsActive(false);
                ResetDeconstructState();
            }

            _wasFiringLastFrame = _isFiring;
            _isFiring = false;
            if (_diagnosisCached && !HasActiveDiagnosis())
                _diagnosisCached = false;

            IInputService inputService = _cachedInputService;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            if (!inputState.HasAction(PlayerInputAction.SecondaryFire))
                _secondaryLatched = false;
        }

        public override string BuildLegacyOperationalSummaryString()
        {
            return CutterCategory;
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (_isLockedOut)
            {
                buffer.Append(StableText(H8ToolLocHashes.LASER_OPERATIONAL_LOCKOUT, "LASER CUTTER // LOCKOUT "));
                buffer.AppendInt((int)(_heatLevel * 100f));
                buffer.Append("%");
                return;
            }

            if (_cachedDeconstructModule != null)
            {
                float progress = math.saturate(_deconstructProgress * math.rcp(math.max(0.01f, deconstructThreshold)));
                buffer.Append(StableText(H8ToolLocHashes.LASER_OPERATIONAL_RECOVERY, "LASER CUTTER // RECOVERY "));
                buffer.AppendInt((int)(progress * 100f));
                buffer.Append("%");
                return;
            }

            if (HasActiveDiagnosis() && _diagnosisHeadline.Length > 0)
            {
                buffer.Append(StableText(H8ToolLocHashes.LASER_OPERATIONAL_DIAGNOSIS, "LASER CUTTER // "));
                buffer.Append(_diagnosisHeadline);
                return;
            }

            if (_heatLevel > 0.01f)
            {
                buffer.Append(StableText(H8ToolLocHashes.LASER_OPERATIONAL_HEAT, "LASER CUTTER // HEAT "));
                buffer.AppendInt((int)(_heatLevel * 100f));
                buffer.Append("%");
                return;
            }

            buffer.Append(StableText(H8ToolLocHashes.LASER_OPERATIONAL_READY, "LASER CUTTER // READY"));
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Primary cuts. Secondary diagnoses and holds recovery mode on modules.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (_isLockedOut)
            {
                AppendText(ref buffer, StableText(H8ToolLocHashes.LASER_DIRECTIVE_LOCKOUT, "Wait for the core to cool before firing again."));
                return;
            }

            if (_cachedDeconstructModule != null)
            {
                AppendText(ref buffer, StableText(H8ToolLocHashes.LASER_DIRECTIVE_RECOVERY, "Hold the beam steady to finish recovery on the locked module."));
                return;
            }

            if (HasActiveDiagnosis() && _diagnosisSummary.Length > 0)
            {
                buffer.Append(in _diagnosisSummary);
                return;
            }

            if (_heatLevel >= 0.75f)
            {
                AppendText(ref buffer, StableText(H8ToolLocHashes.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout."));
                return;
            }

            AppendText(ref buffer, StableText(H8ToolLocHashes.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules."));
        }

        // ══════════════════════════════════════════════════════════
        //  HEAT MANAGEMENT
        // ══════════════════════════════════════════════════════════

        private void TriggerOverheatLockout()
        {
            PublishBeamState(false);
            _isLockedOut = true;
            _lockoutTimer = math.max(0f, overheatLockoutTime);
            _lockoutSoundPlayed = false;
            _isFiring = false;
            SetOverheatedState();
            SetVisualsActive(false);
            ResetDeconstructState();
            PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_CORE_OVERHEATED, "LASER CUTTER - CORE OVERHEATED"));
            FieldOperationLogSystem.RecordOperation(
                StableText(H8ToolLocHashes.LASER_CATEGORY, CutterCategory),
                StableText(H8ToolLocHashes.LASER_LOG_OVERHEAT_TITLE, "LASER CORE OVERHEATED"),
                StableText(H8ToolLocHashes.LASER_LOG_OVERHEAT_MESSAGE, "Cutter entered forced thermal lockout. Reduce sustained beam exposure before the next recovery pass."),
                "CRITICAL");
        }

        private void SyncCentralThermalBatteryState()
        {
            if (!TryGetModularEquipment(out IModularEquipmentService service) || RuntimeToolId == 0u)
                return;

            if (!service.TryGetToolState(RuntimeToolId, out ToolState state))
                return;

            float nextHeat = math.saturate(state.InternalHeat);
            bool heatChanged = math.abs(_heatLevel - nextHeat) > 0.002f;
            _heatLevel = nextHeat;
            bool overheated = (state.StatusMask & ToolRuntimeStatusMasks.Overheated) != 0u;
            if (overheated)
            {
                if (!_isLockedOut)
                    TriggerOverheatLockout();
                else
                    SetOverheatedState();
            }
            else if (_isLockedOut)
            {
                _isLockedOut = false;
                _lockoutTimer = 0f;
                _lockoutSoundPlayed = false;
                ClearFlag(OverheatedState);
                EnterCooldownState();
                PublishInfoMessage(StableText(H8ToolLocHashes.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE"));
                heatChanged = true;
            }

            if (heatChanged)
            {
                SyncHeatOutputs();
                PublishHeat();
            }
        }

        private void PublishHeat()
        {
            if (math.abs(_heatLevel - _lastPublishedHeat) > 0.02f)
            {
                _lastPublishedHeat = _heatLevel;
                LaserCutterEvents.TryRaiseHeatChanged(_heatLevel, ResolveEventCutterId(), ResolveEventRootInstanceId());
            }
        }

        private void SyncHeatOutputs()
        {
            _pendingLaserHeatOutputDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyHeatOutputs()
        {
            if (!math.isfinite(_lastPublishedLaserHitHeat) ||
                math.abs(_heatLevel - _lastPublishedLaserHitHeat) > ShaderFloatPublishEpsilon)
            {
                Shader.SetGlobalFloat(_LaserHitHeatId, _heatLevel);
                _lastPublishedLaserHitHeat = _heatLevel;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CUT DAMAGE
        // ══════════════════════════════════════════════════════════

        private bool TryResolveToolPose(out Vector3 origin, out Vector3 direction, out double3 originAup)
        {
            origin = default;
            direction = Vector3.forward;
            originAup = default;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                !playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                return false;
            }

            float3 runtimePosition = snapshot.RuntimePosition;
            float3 forward = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward);
            if (!math.all(math.isfinite(runtimePosition)) ||
                !math.all(math.isfinite(forward)) ||
                !math.isfinite(forwardLengthSq) ||
                forwardLengthSq <= 0.0001f ||
                !TryResolveAbsoluteUniversePosition(new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z), out AbsoluteUniversePosition aup))
            {
                return false;
            }

            forward *= math.rsqrt(math.max(forwardLengthSq, 0.0001f));
            origin = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            direction = new Vector3(forward.x, forward.y, forward.z);
            originAup = aup.ToAbsoluteDouble3();
            return math.all(math.isfinite(originAup));
        }

        private void ApplyCutDamage(float deltaTime)
        {
            if (_hitInfo.collider == null)
                return;

            IInteractionSignalService interactionService = _cachedInteractionService;
            if (interactionService == null || !interactionService.IsInitialized)
                return;

            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            float energyNormalized = ReadCachedSuitEnergyNormalized();
            if (energyNormalized < LowPowerThresholdNormalized)
            {
                powerScale *= LowPowerOutputScale;
                SetFlag(LowPowerState);
            }
            else
            {
                ClearFlag(LowPowerState);
            }

            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float runtimePower = GetRuntimePowerScalar(damagePerSecond);
            float damage = runtimePower * deltaTime * powerScale * heatMultiplier;
            if (damage <= 0f)
                return;

            if (!TryResolveToolPose(out _, out Vector3 direction, out double3 absoluteOriginAup) ||
                !TryResolveAbsoluteUniversePointDouble3(_hitInfo.point, out double3 absoluteHitAup))
            {
                return;
            }

            float normalizedPower = ResolveNormalizedPower((runtimePower * math.rcp(math.max(damagePerSecond, 0.0001f))) * powerScale, heatMultiplier);
            if (normalizedPower < MinEffectiveBeamPower)
            {
                SetFlag(LowPowerState);
                return;
            }

            ClearFlag(LowPowerState);
            if (TryApplyWfcDoorCut(deltaTime, normalizedPower, absoluteOriginAup, absoluteHitAup, out _, out _))
            {
                ApplyRecoilImpulse(direction, normalizedPower);
                return;
            }

            Vector3 absoluteOrigin = ToFloatVector(absoluteOriginAup);
            Vector3 absoluteHitPoint = ToFloatVector(absoluteHitAup);
            EquipmentInteractionPacket packet = new EquipmentInteractionPacket(
                _cachedToolId,
                new float3(absoluteOrigin.x, absoluteOrigin.y, absoluteOrigin.z),
                new float3(direction.x, direction.y, direction.z),
                normalizedPower,
                GetRuntimeMaxRange(maxRange),
                (byte)ToolActionMode.Primary,
                _toolStateFlags,
                ResolveCurrentFrameId());
            EquipmentInteractionSignal signal = new EquipmentInteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z),
                new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z),
                damage,
                (byte)InteractionEffectType.PlasmaCut,
                0);

            if (interactionService.Publish(signal, _hitInfo.collider))
            {
                TryPublishBoilSignal(interactionService, packet, damage, normalizedPower);

                ISargassumCutWriteService cutWriter = _cachedSargassumCutWriter;
                if (cutWriter != null)
                {
                    float terrainDamageRadius = math.lerp(0.2f, 0.75f, normalizedPower);
                    cutWriter.TryRegisterExternalCut(_hitInfo.point, terrainDamageRadius, normalizedPower, direction, 0.1f);
                }

                IOrganicToolHitService organicHits = _cachedOrganicToolHits;
                if (organicHits != null)
                    organicHits.TryApplyOrganicToolHit(_hitInfo.point, _hitInfo.normal, direction, damage, normalizedPower, GetCapabilityMask());

                ApplyRecoilImpulse(direction, normalizedPower);
                PublishHeatMicroVibration(normalizedPower);
            }
        }

        private bool TryApplyWfcDoorCut(
            float deltaTime,
            float normalizedPower,
            double3 absoluteOriginAup,
            double3 absoluteHitAup,
            out float progress01,
            out bool completed)
        {
            progress01 = 0f;
            completed = false;
            if (_hitInfo.collider == null)
                return false;

            int targetId = unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId()));
            if (targetId != _cachedWfcDoorTargetId)
            {
                _cachedWfcDoorTargetId = targetId;
                if (!LaserCutterTargetRegistry.TryResolveDoor(_hitInfo.collider, out _cachedWfcDoor))
                    _cachedWfcDoor = null;
            }

            IWfcDoorLaserCutTarget door = _cachedWfcDoor;

            if (door == null || !door.TryReadWfcDoorLaserCutState(out WfcDoorLaserCutReadSnapshot doorState))
            {
                SetWfcCutDecalActive(false);
                return false;
            }

            WfcLaserCutRuntime.RefreshOwnerPhaseContext();
            float progressDelta01 = math.max(0f, deltaTime) * math.saturate(normalizedPower);
            bool handled = WfcLaserCutRuntime.TryApplyDoorCut(
                doorState.SectorHash,
                doorState.CellIndex,
                doorState.CurrentFlags,
                _cachedToolId,
                absoluteOriginAup,
                absoluteHitAup,
                _hitInfo.point,
                progressDelta01,
                normalizedPower,
                _heatLevel,
                out progress01,
                out completed,
                out uint cutFrame);

            if (handled)
            {
                door.ApplyWfcDoorLaserCutProgress(progress01, cutFrame);
                UpdateWfcCutDecalVisual(progress01);
            }
            else
            {
                SetWfcCutDecalActive(false);
            }

            return handled;
        }

        private void ApplyOpenWaterBoil(float deltaTime)
        {
            if (!TryReadPlayerMovementSnapshot(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) == 0u)
                return;

            IWaterHeatInjectionService waterHeatInjection = TryGetSubmarineRuntimeContext(out ISubmarineRuntimeContext submarine)
                ? submarine.WaterHeatInjectionService
                : null;
            if (waterHeatInjection == null)
                return;

            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            float energyNormalized = ReadCachedSuitEnergyNormalized();
            if (energyNormalized < LowPowerThresholdNormalized)
                powerScale *= LowPowerOutputScale;

            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float runtimePower = GetRuntimePowerScalar(damagePerSecond);
            float cutStrength = runtimePower * deltaTime * powerScale * heatMultiplier * math.max(0f, waterHeatCouplingScale);
            if (cutStrength <= 0f)
                return;

            if (!TryResolveToolPose(out Vector3 toolOrigin, out Vector3 direction, out _))
                return;

            float normalizedPower = ResolveNormalizedPower((runtimePower * math.rcp(math.max(damagePerSecond, 0.0001f))) * powerScale, heatMultiplier);
            if (normalizedPower < MinEffectiveBeamPower)
                return;

            float runtimeRange = GetRuntimeMaxRange(maxRange);
            Vector3 samplePoint = toolOrigin + (direction * math.min(runtimeRange, 8f));
            waterHeatInjection.TryInjectLocalizedWaterHeat(samplePoint, direction, cutStrength, normalizedPower);
        }

        // ══════════════════════════════════════════════════════════
        //  DECONSTRUCT MODE
        // ══════════════════════════════════════════════════════════

        private void ProcessDeconstructMode(float deltaTime)
        {
            if (_hitInfo.collider == null)
            {
                ResetDeconstructState();
                return;
            }

            int targetId = unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId()));

            if (targetId != _cachedDeconstructTargetId)
            {
                SetCachedDeconstructionPreview(false);
                _deconstructProgress = 0f;
                _cachedDeconstructTargetId = targetId;
                if (!LaserCutterTargetRegistry.TryResolveModule(_hitInfo.collider, out _cachedDeconstructModule))
                    _cachedDeconstructModule = null;

                if (_cachedDeconstructModule != null && _cachedDeconstructModule.CanDeconstruct())
                    SetCachedDeconstructionPreview(true);
            }

            float hitNormalSqrMagnitude = _hitInfo.normal.sqrMagnitude;
            if (hitNormalSqrMagnitude > 0.0001f)
                _cachedDeconstructAnchorNormal = _hitInfo.normal * math.rsqrt(hitNormalSqrMagnitude);
            else
                _cachedDeconstructAnchorNormal = Vector3.up;

            if (_cachedDeconstructModule == null)
            {
                if (!_deconstructBlockedReported)
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_RECOVERY_NO_MODULE, "RECOVERY MODE - NO MODULE"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            if (!_cachedDeconstructModule.CanDeconstruct())
            {
                SetCachedDeconstructionPreview(false);
                if (!_deconstructBlockedReported)
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    _deconstructBlockedReported = true;
                }
                ApplyCutDamage(deltaTime);
                return;
            }

            _cachedDeconstructAnchorPoint = _hitInfo.point - _cachedDeconstructAnchorNormal * heavySalvageAnchorRetraction;
            IPlayerCuttingTensionService cuttingTension = _cachedCuttingTensionService;
            if (cuttingTension != null)
            {
                cuttingTension.TryApplyCuttingTensionAnchor(
                    new float3(_cachedDeconstructAnchorPoint.x, _cachedDeconstructAnchorPoint.y, _cachedDeconstructAnchorPoint.z),
                    new float3(_cachedDeconstructAnchorNormal.x, _cachedDeconstructAnchorNormal.y, _cachedDeconstructAnchorNormal.z));
            }

            float tension01 = ReadCachedCuttingTension01();
            float pull01 = ReadCachedDetachmentPull01(_cachedDeconstructAnchorPoint);
            if (tension01 < heavySalvageRequiredTension01 || pull01 < heavySalvageRequiredPull01)
            {
                if (!_deconstructStartReported)
                {
                    PublishInfoMessage("RECOVERY MODE - LOAD THE CUT");
                    _deconstructStartReported = true;
                }

                if (_visualClockSeconds >= _nextProgressFeedbackAt)
                {
                    int tensionPercent = FastRoundPercent(tension01);
                    int pullPercent = FastRoundPercent(pull01);
                    ShowRecoveryPullBackFeedback(tensionPercent, pullPercent);
                    _nextProgressFeedbackAt = _visualClockSeconds + 0.6f;
                }
                return;
            }

            float progressGain = deltaTime * tension01 * math.lerp(0.75f, 1.25f, pull01);
            _deconstructProgress += progressGain;
            if (!_deconstructStartReported)
            {
                PublishInfoMessage("RECOVERY MODE - TEAR IT FREE");
                _deconstructStartReported = true;
            }

            if (_visualClockSeconds >= _nextProgressFeedbackAt)
            {
                float progress01 = math.saturate(_deconstructProgress * math.rcp(math.max(deconstructThreshold, 0.01f)));
                ShowRecoveryProgressFeedback(progress01);
                _nextProgressFeedbackAt = _visualClockSeconds + 0.6f;
            }

            if (_deconstructProgress >= deconstructThreshold)
            {
                EnsurePlayerInventory();
                BaseModule recoveredModule = _cachedDeconstructModule;
                if (!TryResolveToolPose(out Vector3 toolOrigin, out Vector3 toolForward, out _))
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    ResetDeconstructState();
                    return;
                }

                if (!TryRequestModuleDeconstruction(
                        recoveredModule,
                        _hitInfo.point,
                        toolOrigin,
                        toolForward,
                        GetRuntimeMaxRange(maxRange),
                        2))
                {
                    PublishWarningMessage(StableText(H8ToolLocHashes.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED"));
                    ResetDeconstructState();
                    return;
                }

                PublishInfoMessage("LASER CUTTER - RECOVERY QUEUED");
                
                _telemetryBuffer.Clear();
                _telemetryBuffer.Append("Laser-assisted deconstruction queued for habitat rollback validation on target module.");

                FieldOperationLogSystem.RecordOperation(
                    StableText(H8ToolLocHashes.LASER_CATEGORY, CutterCategory),
                    "MODULE RECOVERY QUEUED",
                    _telemetryBuffer,
                    "INFO");
                ResetDeconstructState();
            }
        }

        private void ResetDeconstructState()
        {
            SetCachedDeconstructionPreview(false);

            _cachedCuttingTensionService?.ClearCuttingTensionAnchor();

            _deconstructProgress = 0f;
            _cachedDeconstructTargetId = -1;
            _cachedDeconstructModule = null;
            _deconstructStartReported = false;
            _deconstructBlockedReported = false;
            _nextProgressFeedbackAt = 0f;
            _cachedDeconstructAnchorPoint = Vector3.zero;
            _cachedDeconstructAnchorNormal = Vector3.up;
        }

        private void SetCachedDeconstructionPreview(bool enabled)
        {
            if (_cachedDeconstructModule == null)
                return;

            IHabitatDeconstructionSystem deconstructionSystem = _cachedHabitatDeconstructionSystem;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return;

            uint targetEntityId = unchecked((uint)EntityId.ToULong(_cachedDeconstructModule.gameObject.GetEntityId()));
            deconstructionSystem.TrySetDeconstructionPreview(targetEntityId, enabled);
        }

        private bool TryRequestModuleDeconstruction(
            BaseModule module,
            Vector3 targetPoint,
            Vector3 rayOrigin,
            Vector3 rayDirection,
            float maxDistance,
            byte toolKind)
        {
            if (module == null)
                return false;

            IHabitatDeconstructionSystem deconstructionSystem = _cachedHabitatDeconstructionSystem;
            if (deconstructionSystem == null || !deconstructionSystem.IsInitialized)
                return false;

            float directionLengthSq = rayDirection.sqrMagnitude;
            if (directionLengthSq <= 0.0001f)
                rayDirection = Vector3.down;
            else
                rayDirection *= math.rsqrt(directionLengthSq);

            if (!TryResolveAbsoluteUniversePosition(targetPoint, out AbsoluteUniversePosition targetAup) ||
                !TryResolveAbsoluteUniversePosition(rayOrigin, out AbsoluteUniversePosition rayOriginAup))
            {
                return false;
            }

            DeconstructRequestSignal request = new DeconstructRequestSignal
            {
                TargetAup = targetAup,
                RayOriginAup = rayOriginAup,
                TargetEntityId = unchecked((uint)EntityId.ToULong(module.gameObject.GetEntityId())),
                RequesterEntityId = unchecked((uint)_surfaceRequesterId),
                MaxDistance = Mathf.Max(0f, maxDistance),
                RayDirection = new float3(rayDirection.x, rayDirection.y, rayDirection.z),
                Frame = ResolveCurrentFrameId(),
                ToolKind = toolKind,
                Flags = 0
            };

            return deconstructionSystem.EnqueueDeconstruction(in request);
        }

        private void ShowRecoveryPullBackFeedback(int tensionPercent, int pullPercent)
        {
            _recoveryFeedbackBuffer.Clear();
            _recoveryFeedbackBuffer.Append("RECOVERY MODE - PULL BACK ");
            _recoveryFeedbackBuffer.AppendInt(math.clamp(tensionPercent, 0, RecoveryProgressMaxPercent));
            _recoveryFeedbackBuffer.Append("/");
            _recoveryFeedbackBuffer.AppendInt(math.clamp(pullPercent, 0, RecoveryProgressMaxPercent));
            ToolHitUtility.ShowInfo(in _recoveryFeedbackBuffer);
        }

        private void ShowRecoveryProgressFeedback(float progress01)
        {
            int percent = math.clamp((int)(math.saturate(progress01) * 100f + 0.5f), 0, RecoveryProgressMaxPercent);
            string template = StableText(H8ToolLocHashes.LASER_RECOVERY_PROGRESS, "RECOVERY PROGRESS - {0}%");

            _recoveryFeedbackBuffer.Clear();
            if (!_recoveryFeedbackBuffer.AppendTemplate(template.AsSpan(), LocNumericArg.Int(percent)))
            {
                _recoveryFeedbackBuffer.Clear();
                _recoveryFeedbackBuffer.Append("RECOVERY PROGRESS - ");
                _recoveryFeedbackBuffer.AppendInt(percent);
                _recoveryFeedbackBuffer.Append("%");
            }

            ToolHitUtility.ShowInfo(in _recoveryFeedbackBuffer);
        }

        private void PublishInfoMessage(string message)
        {
            _telemetryBuffer.Clear();
            if (AppendText(ref _telemetryBuffer, message))
                ToolHitUtility.ShowInfo(in _telemetryBuffer);
        }

        private void PublishWarningMessage(string message)
        {
            _telemetryBuffer.Clear();
            if (AppendText(ref _telemetryBuffer, message))
                ToolHitUtility.ShowWarning(in _telemetryBuffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private void EnsurePlayerInventory()
        {
            EnsurePlayerBindings();
        }

        private void EnsurePlayerBindings()
        {
            if (_cachedInventory != null &&
                _cachedCuttingTensionService != null)
                return;

            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext))
                return;

            _cachedInventory = playerContext.Inventory;
            _cachedCuttingTensionService = playerContext.CuttingTensionService;
        }

        private float ReadCachedSuitEnergyNormalized()
        {
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState) &&
                math.isfinite(survivalState.EnergyNormalized))
            {
                return math.saturate(survivalState.EnergyNormalized);
            }

            return 1f;
        }

        private bool TryReadPlayerMovementSnapshot(out PlayerMovementRuntimeState movementState)
        {
            movementState = default;
            return TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                   playerContext.TryGetMovementRuntimeState(out movementState) &&
                   math.all(math.isfinite(movementState.WorldPosition)) &&
                   math.all(math.isfinite(movementState.Velocity));
        }

        private float ReadCachedCuttingTension01()
        {
            IPlayerCuttingTensionService cuttingTension = _cachedCuttingTensionService;
            return cuttingTension != null && cuttingTension.TryReadCuttingTensionNormalized(out float tension01)
                ? math.saturate(tension01)
                : 0f;
        }

        private float ReadCachedDetachmentPull01(Vector3 anchorPoint)
        {
            if (!TryResolvePlayerAnchorOffset(anchorPoint, out Vector3 awayFromAnchor))
                return 0f;

            float sqrMagnitude = awayFromAnchor.sqrMagnitude;
            if (sqrMagnitude <= 0.0001f)
                return 0f;

            awayFromAnchor *= math.rsqrt(sqrMagnitude);
            Vector3 playerForward = awayFromAnchor;
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                float3 snapshotForward = snapshot.Forward;
                snapshotForward.y = 0f;
                float forwardSqrMagnitude = math.lengthsq(snapshotForward);
                if (math.all(math.isfinite(snapshotForward)) && math.isfinite(forwardSqrMagnitude) && forwardSqrMagnitude > 0.0001f)
                {
                    snapshotForward *= math.rsqrt(math.max(forwardSqrMagnitude, 0.0001f));
                    playerForward = new Vector3(snapshotForward.x, snapshotForward.y, snapshotForward.z);
                }
            }

            float facingAway01 = math.saturate((math.dot((float3)playerForward, (float3)awayFromAnchor) + 1f) * 0.5f);
            float backpedal01 = 0f;
            IInputService inputService = _cachedInputService;
            PlayerInputState inputState = inputService != null && inputService.IsPlayerInputEnabled
                ? inputService.GetState()
                : default;
            backpedal01 = math.saturate(-inputState.MoveDelta.y);

            float awayVelocity01 = 0f;
            if (heavySalvagePullVelocityForFullIntent > 0.01f &&
                TryReadPlayerMovementSnapshot(out PlayerMovementRuntimeState movementState) &&
                math.all(math.isfinite(movementState.Velocity)))
            {
                float awayVelocity = math.max(0f, math.dot(movementState.Velocity, (float3)awayFromAnchor));
                awayVelocity01 = math.saturate(awayVelocity / heavySalvagePullVelocityForFullIntent);
            }

            return math.max(awayVelocity01, backpedal01 * facingAway01);
        }

        private bool TryResolvePlayerAnchorOffset(Vector3 anchorPoint, out Vector3 awayFromAnchor)
        {
            if (TryResolveAbsoluteUniversePosition(anchorPoint, out AbsoluteUniversePosition anchorAup) &&
                TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot poseSnapshot))
            {
                AbsoluteUniversePosition playerAup = poseSnapshot.Aup;
                if (!IsFiniteAup(in playerAup))
                {
                    awayFromAnchor = default;
                    return false;
                }

                double3 delta = AbsoluteUniversePosition.DeltaMetersClamped(in playerAup, in anchorAup);
                awayFromAnchor = default;
                awayFromAnchor.x = (float)delta.x;
                awayFromAnchor.z = (float)delta.z;
                return true;
            }

            awayFromAnchor = default;
            return false;
        }

        // ══════════════════════════════════════════════════════════
        //  VISUALS
        // ══════════════════════════════════════════════════════════

        private void UpdateLaserLine(bool didHit)
        {
            _pendingLaserLineDidHit = didHit;
            _pendingLaserLineDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyLaserLine(bool didHit)
        {
            if (laserLine == null) return;

            if (!laserLine.enabled)
                laserLine.enabled = true;

            laserLine.SetPosition(0, Vector3.zero);

            if (didHit)
            {
                Vector3 localHitPoint = Vector3.forward * GetRuntimeMaxRange(maxRange);
                if (TryResolveToolPose(out Vector3 toolOrigin, out _, out _))
                {
                    float3 hitDelta = new float3(
                        _hitInfo.point.x - toolOrigin.x,
                        _hitInfo.point.y - toolOrigin.y,
                        _hitInfo.point.z - toolOrigin.z);
                    float hitDistanceSq = math.lengthsq(hitDelta);
                    if (math.isfinite(hitDistanceSq) && hitDistanceSq > 0.0001f)
                        localHitPoint = Vector3.forward * math.sqrt(hitDistanceSq);
                }

                float jitterAmp = _heatLevel * maxJitterAmplitude;
                if (jitterAmp > 0.0001f)
                {
                    float t = _visualClockSeconds * jitterFrequency;
                    float jx = FastTriangleSigned(t * InvTau) * jitterAmp;
                    float jy = FastTriangleSigned((t * LaserJitterSecondaryScale + LaserJitterSecondaryOffset) * InvTau) * jitterAmp * 0.7f;
                    localHitPoint.x += jx;
                    localHitPoint.y += jy;
                }

                laserLine.SetPosition(1, localHitPoint);
            }
            else
            {
                laserLine.SetPosition(1, Vector3.forward * GetRuntimeMaxRange(maxRange));
            }
        }

        private void UpdateSparks(bool didHit)
        {
            if (!didHit)
                return;

            if (!TryResolveAbsoluteUniversePointDouble3(_hitInfo.point, out double3 hitAup))
                return;

            float3 normal = new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z);
            LaserCutterDodRuntime.StageGpuSparkSignal(
                hitAup,
                normal,
                _heatLevel,
                ResolveCurrentNormalizedPower01(),
                _cachedToolId,
                unchecked((uint)_surfaceRequesterId),
                ResolveCurrentFrameId());
        }

        private static float FastTriangleSigned(float phase)
        {
            float triangle01 = 1f - math.abs(math.frac(phase + 0.25f) * 2f - 1f);
            return triangle01 * 2f - 1f;
        }

        private static int FastRoundPercent(float value01)
        {
            return (int)(math.saturate(value01) * RecoveryProgressMaxPercent + 0.5f);
        }

        private static Quaternion ResolveDominantAxisRotation(Vector3 normal)
        {
            float absX = math.abs(normal.x);
            float absY = math.abs(normal.y);
            float absZ = math.abs(normal.z);

            if (absY >= absX && absY >= absZ)
                return normal.y >= 0f ? _SparkRotationUp : _SparkRotationDown;

            if (absX >= absZ)
                return normal.x >= 0f ? _SparkRotationRight : _SparkRotationLeft;

            return normal.z >= 0f ? _SparkRotationForward : _SparkRotationBack;
        }

        private void UpdateAudioState(bool shouldPlay)
        {
            _pendingCutAudioShouldPlay = shouldPlay;
            _pendingCutAudioDirty = true;
            TryRegisterLateFrameTick();
        }

        private void QueueOverheatLockoutCue()
        {
            _pendingOverheatLockoutCueDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyOverheatLockoutCue()
        {
            if (overheatErrorClip != null && _cachedAudioService != null)
                _cachedAudioService.PlayStatic2D(overheatErrorClip, 0.5f);
        }

        private void ApplyAudioState(bool shouldPlay)
        {
            if (cutAudio == null) return;

            if (shouldPlay)
            {
                TryAssignCutAudioMixerRoute();
                if (!cutAudio.isPlaying)
                    cutAudio.Play();

                cutAudio.pitch = math.lerp(basePitch, maxPitch, _heatLevel);
                PublishLaserLoopAcoustic(math.saturate(_heatLevel));
            }
            else
            {
                if (cutAudio.isPlaying)
                    cutAudio.Stop();

                cutAudio.pitch = basePitch;
            }
        }

        private void PublishLaserLoopAcoustic(float progress01)
        {
            uint targetHash = _hitInfo.collider != null
                ? unchecked((uint)EntityId.ToULong(_hitInfo.collider.GetEntityId()))
                : 0u;

            ToolAcousticSignal signal = new ToolAcousticSignal
            {
                ToolHash = _cachedToolId,
                TargetHash = targetHash,
                Progress01 = math.saturate(progress01),
                PitchScale = math.lerp(basePitch, maxPitch, _heatLevel),
                Intensity01 = math.saturate(0.35f + _heatLevel * 0.65f),
                Frame = ResolveCurrentFrameId(),
                State = ToolAcousticSignal.StateLaserLoop,
                Flags = ToolAcousticSignal.FlagLooping
            };
            SignalBus<ToolAcousticSignal>.TryPushTracked(in signal, ref s_x001LaserCutterSignalPushDropCount);
        }

        private void PublishHeatMicroVibration(float normalizedPower)
        {
            float intensity = math.saturate(normalizedPower * (0.25f + _heatLevel * 0.75f));
            if (intensity <= 0.0001f)
                return;

            HapticRequest request = new HapticRequest
            {
                Intensity01 = intensity,
                DurationSeconds = 0.04f,
                Frequency01 = math.saturate(0.55f + _heatLevel * 0.45f),
                SourceHash = _cachedToolId,
                Frame = ResolveCurrentFrameId(),
                Channel = HapticRequest.ChannelMicroVibration,
                Flags = HapticRequest.FlagMicroVibration
            };
            SignalBus<HapticRequest>.TryPushTracked(in request, ref s_x001LaserCutterSignalPushDropCount);
        }

        private void UpdateWfcCutDecalVisual(float progress01)
        {
            if (wfcCutDecalProxy == null)
                return;

            float clampedProgress = math.saturate(progress01);
            float scale = math.lerp(0.04f, math.max(0.05f, wfcCutDecalMaxScale), clampedProgress);
            _pendingWfcCutDecalPosition = _hitInfo.point + _hitInfo.normal * 0.006f;
            _pendingWfcCutDecalRotation = ResolveDominantAxisRotation(_hitInfo.normal);
            _pendingWfcCutDecalScale = new Vector3(scale, scale, scale);
            SetWfcCutDecalActive(true);
        }

        private void SetWfcCutDecalActive(bool active)
        {
            _pendingWfcCutDecalActive = active;
            _pendingWfcCutDecalDirty = true;
            TryRegisterLateFrameTick();
        }

        private void ApplyWfcCutDecalVisual()
        {
            if (_pendingWfcCutDecalActive && wfcCutDecalProxy != null)
            {
                wfcCutDecalProxy.position = _pendingWfcCutDecalPosition;
                wfcCutDecalProxy.rotation = _pendingWfcCutDecalRotation;
                wfcCutDecalProxy.localScale = _pendingWfcCutDecalScale;
            }

            if (wfcCutDecalRenderer != null && wfcCutDecalRenderer.enabled != _pendingWfcCutDecalActive)
                wfcCutDecalRenderer.enabled = _pendingWfcCutDecalActive;
        }

        private void SetVisualsActive(bool active)
        {
            _pendingVisualActive = active;
            _pendingVisualActiveDirty = true;
            if (!active)
            {
                UpdateAudioState(false);
                SetWfcCutDecalActive(false);
            }

            TryRegisterLateFrameTick();
        }

        private void ApplyVisualsActive(bool active)
        {
            if (laserLine != null)
                laserLine.enabled = active;

            if (!active && wfcCutDecalRenderer != null && wfcCutDecalRenderer.enabled)
                wfcCutDecalRenderer.enabled = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE HELPERS
        // ══════════════════════════════════════════════════════════

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private void ClearPendingLaserVisualSync()
        {
            _pendingVisualActiveDirty = false;
            _pendingLaserLineDirty = false;
            _pendingCutAudioDirty = false;
            _pendingOverheatLockoutCueDirty = false;
            _pendingLaserHeatOutputDirty = false;
            _pendingWfcCutDecalDirty = false;
            _pendingVisualActive = false;
            _pendingLaserLineDidHit = false;
            _pendingCutAudioShouldPlay = false;
            _pendingWfcCutDecalActive = false;
        }

        private void CacheWfcCutDecalRenderer()
        {
            if (wfcCutDecalRenderer == null && wfcCutDecalProxy != null)
                wfcCutDecalProxy.TryGetComponent(out wfcCutDecalRenderer);

            SetWfcCutDecalActive(false);
        }

        private void ResetAllState()
        {
            CancelAction();
            _heatLevel = 0f;
            _isLockedOut = false;
            _lockoutTimer = 0f;
            _lockoutSoundPlayed = false;
            _lastPublishedHeat = -1f;
            _lastPublishedLaserHitHeat = float.NaN;
            _secondaryLatched = false;
            _visualClockSeconds = 0f;
            _cachedWfcDoorTargetId = -1;
            _cachedWfcDoor = null;
            SyncHeatOutputs();
            ResetDeconstructState();
        }

        public void Activate()
        {
            SetFlag(ActiveState);
            ClearFlag(IdleState);
            ClearFlag(CooldownState);
        }

        public void Deactivate()
        {
            SetFlag(IdleState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        public void CancelAction()
        {
            PublishBeamState(false);
            _isFiring = false;
            _wasFiringLastFrame = false;
            _toolStateFlags = IdleState;
        }

        public uint GetCapabilityMask()
        {
            return ToolCapabilityMasks.PlasmaCut;
        }

        private void PublishBeamState(bool isActive)
        {
            if (_lastPublishedBeamActive == isActive)
                return;

            _lastPublishedBeamActive = isActive;
            LaserCutterEvents.TryRaiseBeamStateChanged(ResolveEventCutterId(), ResolveEventRootInstanceId(), isActive);
        }

        private int ResolveEventCutterId()
        {
            return unchecked((int)EntityId.ToULong(GetEntityId()));
        }

        private int ResolveEventRootInstanceId()
        {
            return ResolveEventCutterId();
        }

        private void BuildDiagnosisFromHit(InteractionSurfaceHit hit, bool didHit, out byte severity)
        {
            _diagnosisHeadline.Clear();
            _diagnosisSummary.Clear();

            if (!didHit)
            {
                _diagnosisHeadline.Append(StableText(H8ToolLocHashes.LASER_HEADLINE_NO_TARGET, "NO CONTACT"));
                _diagnosisSummary.Append(StableText(H8ToolLocHashes.LASER_SUMMARY_NO_TARGET, "Beam is firing into open water. No thermal resonance detected."));
                severity = DiagnosisSeverityInfo;
                return;
            }

            if (LaserCutterTargetRegistry.TryResolveModule(hit.collider, out BaseModule module))
            {
                if (module.CanDeconstruct())
                {
                    _diagnosisHeadline.Append(StableText(H8ToolLocHashes.LASER_HEADLINE_MODULE_LOCKED, "MODULE SECURED"));
                    _diagnosisSummary.Append(StableText(H8ToolLocHashes.LASER_SUMMARY_MODULE_LOCKED, "Base module detected. Hold secondary beam to initialize salvage recovery."));
                    severity = DiagnosisSeverityInfo;
                }
                else
                {
                    _diagnosisHeadline.Append(StableText(H8ToolLocHashes.LASER_HEADLINE_MODULE_STABLE, "MODULE INTEGRITY HIGH"));
                    _diagnosisSummary.Append(StableText(H8ToolLocHashes.LASER_SUMMARY_MODULE_STABLE, "Module is active or structurally reinforced. Deconstruction impossible."));
                    severity = DiagnosisSeverityWarn;
                }
                return;
            }

            if (hit.collider != null)
            {
                _diagnosisHeadline.Append(StableText(H8ToolLocHashes.LASER_HEADLINE_CUTTABLE_CONTACT, "CUTTABLE CONTACT"));
                _diagnosisSummary.Append(StableText(H8ToolLocHashes.LASER_SUMMARY_CUTTABLE_CONTACT, "Target accepts thermal damage but is not recoverable as a base module."));
                severity = DiagnosisSeverityInfo;
                return;
            }

            _diagnosisHeadline.Append(StableText(H8ToolLocHashes.LASER_HEADLINE_INVALID_TARGET, "INVALID TARGET"));
            _diagnosisSummary.Append(StableText(H8ToolLocHashes.LASER_SUMMARY_INVALID_TARGET, "Target is inside beam range but does not respond to cutter operations."));
            severity = DiagnosisSeverityWarn;
        }

        private bool ProbeCutHitForOwnerAction(out InteractionSurfaceHit hit)
        {
            IInteractionSignalService interactionService = _cachedInteractionService;
            if (interactionService != null &&
                interactionService.IsInitialized &&
                TryResolveToolPose(out Vector3 origin, out Vector3 direction, out _))
            {
                return interactionService.RequestPrimarySurfaceHit(_surfaceRequesterId, origin, direction, GetRuntimeMaxRange(maxRange), ResolveCuttableSurfaceMask(), QueryTriggerInteraction.Ignore, out hit);
            }

            hit = default;
            return false;
        }

        private void TryPublishBoilSignal(IInteractionSignalService interactionService, in EquipmentInteractionPacket packet, float deliveredDamage, float normalizedPower)
        {
            if (interactionService == null ||
                _hitInfo.collider == null ||
                !TryReadPlayerMovementSnapshot(out PlayerMovementRuntimeState movementState) ||
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) == 0u)
            {
                return;
            }

            float coupledCutStrength = deliveredDamage * math.max(0f, waterHeatCouplingScale);
            if (coupledCutStrength <= 0f || normalizedPower < MinEffectiveBeamPower)
                return;

            if (!TryResolveAbsoluteUniversePointDouble3(_hitInfo.point, out double3 absoluteHitAup))
                return;

            Vector3 absoluteHitPoint = ToFloatVector(absoluteHitAup);
            EquipmentInteractionSignal boilSignal = new EquipmentInteractionSignal(
                packet,
                unchecked((int)EntityId.ToULong(_hitInfo.collider.GetEntityId())),
                new float3(absoluteHitPoint.x, absoluteHitPoint.y, absoluteHitPoint.z),
                new float3(_hitInfo.normal.x, _hitInfo.normal.y, _hitInfo.normal.z),
                coupledCutStrength,
                (byte)InteractionEffectType.Boil,
                0);

            interactionService.Publish(in boilSignal, _hitInfo.collider);
        }

        private int ResolveCuttableSurfaceMask()
        {
            int mask = cuttableLayer.value;
            if (_WaterLayer >= 0)
                mask &= ~(1 << _WaterLayer);
            if (_TransparentFxLayer >= 0)
                mask &= ~(1 << _TransparentFxLayer);
            return mask;
        }

        private void PublishDiagnosis()
        {
            _telemetryBuffer.Clear();
            _telemetryBuffer.Append(StableText(H8ToolLocHashes.LASER_DIAG_MESSAGE, "LASER DIAG - "));
            _telemetryBuffer.Append(_diagnosisHeadline);

            if (_cachedDiagnosis.Severity >= DiagnosisSeverityWarn)
                ToolHitUtility.ShowWarning(_telemetryBuffer);
            else
                ToolHitUtility.ShowInfo(_telemetryBuffer);
        }

        private bool HasActiveDiagnosis()
        {
            if (!_diagnosisCached)
                return false;

            uint frame = ResolveCurrentFrameId();
            return unchecked(frame - _diagnosisFrame) <= DiagnosisDisplayFrameWindow;
        }

        private static string ResolveDiagnosisSeverityText(byte severity)
        {
            if (severity >= DiagnosisSeverityCritical)
                return "CRITICAL";

            return severity >= DiagnosisSeverityWarn ? "WARN" : "INFO";
        }

        private void CacheLaserLocalizationCold()
        {
            RefreshLaserLocalizationCacheCold(GlobalRegistry.BabelLocalization);
        }

        private void RefreshLaserLocalizationCacheCold(IBabelLocalization localization)
        {
            ushort languageId = localization != null ? localization.ActiveLanguageId : ushort.MaxValue;
            if (ReferenceEquals(_cachedBabelLocalization, localization) &&
                _cachedLaserLocalizationLanguageId == languageId)
            {
                return;
            }

            _cachedBabelLocalization = localization;
            _cachedLaserLocalizationLanguageId = languageId;
            _laserCategory = ResolveBabelString(localization, H8ToolLocHashes.LASER_CATEGORY, CutterCategory);
            _laserDiagMessage = ResolveBabelString(localization, H8ToolLocHashes.LASER_DIAG_MESSAGE, "LASER DIAG - ");
            _laserDirectiveHot = ResolveBabelString(localization, H8ToolLocHashes.LASER_DIRECTIVE_HOT, "Core is running hot. Finish the cut or vent heat before lockout.");
            _laserDirectiveLockout = ResolveBabelString(localization, H8ToolLocHashes.LASER_DIRECTIVE_LOCKOUT, "Wait for the core to cool before firing again.");
            _laserDirectiveReady = ResolveBabelString(localization, H8ToolLocHashes.LASER_DIRECTIVE_READY, "Primary cuts. Secondary diagnoses and holds recovery mode on modules.");
            _laserDirectiveRecovery = ResolveBabelString(localization, H8ToolLocHashes.LASER_DIRECTIVE_RECOVERY, "Hold the beam steady to finish recovery on the locked module.");
            _laserHeadlineCuttableContact = ResolveBabelString(localization, H8ToolLocHashes.LASER_HEADLINE_CUTTABLE_CONTACT, "CUTTABLE CONTACT");
            _laserHeadlineInvalidTarget = ResolveBabelString(localization, H8ToolLocHashes.LASER_HEADLINE_INVALID_TARGET, "INVALID TARGET");
            _laserHeadlineModuleLocked = ResolveBabelString(localization, H8ToolLocHashes.LASER_HEADLINE_MODULE_LOCKED, "MODULE SECURED");
            _laserHeadlineModuleStable = ResolveBabelString(localization, H8ToolLocHashes.LASER_HEADLINE_MODULE_STABLE, "MODULE INTEGRITY HIGH");
            _laserHeadlineNoTarget = ResolveBabelString(localization, H8ToolLocHashes.LASER_HEADLINE_NO_TARGET, "NO CONTACT");
            _laserHudCoreOverheated = ResolveBabelString(localization, H8ToolLocHashes.LASER_HUD_CORE_OVERHEATED, "LASER CUTTER - CORE OVERHEATED");
            _laserHudCoreStable = ResolveBabelString(localization, H8ToolLocHashes.LASER_HUD_CORE_STABLE, "LASER CUTTER - CORE STABLE");
            _laserHudOverheatLockout = ResolveBabelString(localization, H8ToolLocHashes.LASER_HUD_OVERHEAT_LOCKOUT, "LASER CUTTER - OVERHEAT LOCKOUT");
            _laserHudRecoveryModuleLocked = ResolveBabelString(localization, H8ToolLocHashes.LASER_HUD_RECOVERY_MODULE_LOCKED, "RECOVERY MODE - MODULE LOCKED");
            _laserHudRecoveryNoModule = ResolveBabelString(localization, H8ToolLocHashes.LASER_HUD_RECOVERY_NO_MODULE, "RECOVERY MODE - NO MODULE");
            _laserLogOverheatMessage = ResolveBabelString(localization, H8ToolLocHashes.LASER_LOG_OVERHEAT_MESSAGE, "Cutter entered forced thermal lockout. Reduce sustained beam exposure before the next recovery pass.");
            _laserLogOverheatTitle = ResolveBabelString(localization, H8ToolLocHashes.LASER_LOG_OVERHEAT_TITLE, "LASER CORE OVERHEATED");
            _laserOperationalDiagnosis = ResolveBabelString(localization, H8ToolLocHashes.LASER_OPERATIONAL_DIAGNOSIS, "LASER CUTTER // ");
            _laserOperationalHeat = ResolveBabelString(localization, H8ToolLocHashes.LASER_OPERATIONAL_HEAT, "LASER CUTTER // HEAT ");
            _laserOperationalLockout = ResolveBabelString(localization, H8ToolLocHashes.LASER_OPERATIONAL_LOCKOUT, "LASER CUTTER // LOCKOUT ");
            _laserOperationalReady = ResolveBabelString(localization, H8ToolLocHashes.LASER_OPERATIONAL_READY, "LASER CUTTER // READY");
            _laserOperationalRecovery = ResolveBabelString(localization, H8ToolLocHashes.LASER_OPERATIONAL_RECOVERY, "LASER CUTTER // RECOVERY ");
            _laserRecoveryProgress = ResolveBabelString(localization, H8ToolLocHashes.LASER_RECOVERY_PROGRESS, "RECOVERY PROGRESS - {0}%");
            _laserSummaryCuttableContact = ResolveBabelString(localization, H8ToolLocHashes.LASER_SUMMARY_CUTTABLE_CONTACT, "Target accepts thermal damage but is not recoverable as a base module.");
            _laserSummaryInvalidTarget = ResolveBabelString(localization, H8ToolLocHashes.LASER_SUMMARY_INVALID_TARGET, "Target is inside beam range but does not respond to cutter operations.");
            _laserSummaryModuleLocked = ResolveBabelString(localization, H8ToolLocHashes.LASER_SUMMARY_MODULE_LOCKED, "Base module detected. Hold secondary beam to initialize salvage recovery.");
            _laserSummaryModuleStable = ResolveBabelString(localization, H8ToolLocHashes.LASER_SUMMARY_MODULE_STABLE, "Module is active or structurally reinforced. Deconstruction impossible.");
            _laserSummaryNoTarget = ResolveBabelString(localization, H8ToolLocHashes.LASER_SUMMARY_NO_TARGET, "Beam is firing into open water. No thermal resonance detected.");
        }

        private string StableText(uint keyHash, string fallback)
        {
            string cached = keyHash switch
            {
                H8ToolLocHashes.LASER_CATEGORY => _laserCategory,
                H8ToolLocHashes.LASER_DIAG_MESSAGE => _laserDiagMessage,
                H8ToolLocHashes.LASER_DIRECTIVE_HOT => _laserDirectiveHot,
                H8ToolLocHashes.LASER_DIRECTIVE_LOCKOUT => _laserDirectiveLockout,
                H8ToolLocHashes.LASER_DIRECTIVE_READY => _laserDirectiveReady,
                H8ToolLocHashes.LASER_DIRECTIVE_RECOVERY => _laserDirectiveRecovery,
                H8ToolLocHashes.LASER_HEADLINE_CUTTABLE_CONTACT => _laserHeadlineCuttableContact,
                H8ToolLocHashes.LASER_HEADLINE_INVALID_TARGET => _laserHeadlineInvalidTarget,
                H8ToolLocHashes.LASER_HEADLINE_MODULE_LOCKED => _laserHeadlineModuleLocked,
                H8ToolLocHashes.LASER_HEADLINE_MODULE_STABLE => _laserHeadlineModuleStable,
                H8ToolLocHashes.LASER_HEADLINE_NO_TARGET => _laserHeadlineNoTarget,
                H8ToolLocHashes.LASER_HUD_CORE_OVERHEATED => _laserHudCoreOverheated,
                H8ToolLocHashes.LASER_HUD_CORE_STABLE => _laserHudCoreStable,
                H8ToolLocHashes.LASER_HUD_OVERHEAT_LOCKOUT => _laserHudOverheatLockout,
                H8ToolLocHashes.LASER_HUD_RECOVERY_MODULE_LOCKED => _laserHudRecoveryModuleLocked,
                H8ToolLocHashes.LASER_HUD_RECOVERY_NO_MODULE => _laserHudRecoveryNoModule,
                H8ToolLocHashes.LASER_LOG_OVERHEAT_MESSAGE => _laserLogOverheatMessage,
                H8ToolLocHashes.LASER_LOG_OVERHEAT_TITLE => _laserLogOverheatTitle,
                H8ToolLocHashes.LASER_OPERATIONAL_DIAGNOSIS => _laserOperationalDiagnosis,
                H8ToolLocHashes.LASER_OPERATIONAL_HEAT => _laserOperationalHeat,
                H8ToolLocHashes.LASER_OPERATIONAL_LOCKOUT => _laserOperationalLockout,
                H8ToolLocHashes.LASER_OPERATIONAL_READY => _laserOperationalReady,
                H8ToolLocHashes.LASER_OPERATIONAL_RECOVERY => _laserOperationalRecovery,
                H8ToolLocHashes.LASER_RECOVERY_PROGRESS => _laserRecoveryProgress,
                H8ToolLocHashes.LASER_SUMMARY_CUTTABLE_CONTACT => _laserSummaryCuttableContact,
                H8ToolLocHashes.LASER_SUMMARY_INVALID_TARGET => _laserSummaryInvalidTarget,
                H8ToolLocHashes.LASER_SUMMARY_MODULE_LOCKED => _laserSummaryModuleLocked,
                H8ToolLocHashes.LASER_SUMMARY_MODULE_STABLE => _laserSummaryModuleStable,
                H8ToolLocHashes.LASER_SUMMARY_NO_TARGET => _laserSummaryNoTarget,
                _ => null
            };

            return cached ?? fallback ?? string.Empty;
        }

        private static string ResolveBabelString(IBabelLocalization localization, uint keyHash, string fallback)
        {
            return fallback ?? string.Empty;
        }

        private void CacheToolId()
        {
            string toolIdSource = RuntimeMetadata != null && !string.IsNullOrWhiteSpace(RuntimeMetadata.toolID) ? RuntimeMetadata.toolID : "tool_laser_cutter";
            _cachedToolId = unchecked((uint)Animator.StringToHash(toolIdSource));
        }

        private void CacheSurfaceRequesterId()
        {
            _surfaceRequesterId = EntityId.ToULong(gameObject.GetEntityId());
        }

        private void StageDodSurfaceRequest()
        {
            if (!TryResolveToolPose(out _, out Vector3 direction, out double3 originAup))
                return;

            LaserCutterDodRuntime.QueueLiveRequest(
                originAup,
                new float3(direction.x, direction.y, direction.z),
                ResolveCurrentNormalizedPower01(),
                GetRuntimeMaxRange(maxRange),
                _cachedToolId,
                unchecked((uint)_surfaceRequesterId),
                ResolveCurrentFrameId());
        }

        private static uint ResolveCurrentFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private static float ClampFiniteDeltaTime(float deltaTime)
        {
            return math.isfinite(deltaTime) && deltaTime > 0f ? math.min(deltaTime, 0.1f) : 0f;
        }

        private float ResolveCurrentNormalizedPower01()
        {
            float powerScale = GetEfficiency() * GetConditionPerformanceScale();
            if (ReadCachedSuitEnergyNormalized() < LowPowerThresholdNormalized)
                powerScale *= LowPowerOutputScale;

            float heatMultiplier = 1f + _heatLevel * heatDamageBonus;
            float runtimePower = GetRuntimePowerScalar(damagePerSecond);
            return ResolveNormalizedPower((runtimePower * math.rcp(math.max(damagePerSecond, 0.0001f))) * powerScale, heatMultiplier);
        }

        private float ResolveNormalizedPower(float powerScale, float heatMultiplier)
        {
            float normalizedPower = powerScale * (heatMultiplier * math.rcp(math.max(1f + heatDamageBonus, 0.0001f)));
            return math.saturate(normalizedPower);
        }

        private static Vector3 ToFloatVector(double3 value)
        {
            return new Vector3((float)value.x, (float)value.y, (float)value.z);
        }

        private bool TryResolveAbsoluteUniversePointDouble3(Vector3 runtimePoint, out double3 absolutePoint)
        {
            absolutePoint = default;
            if (!TryResolveAbsoluteUniversePosition(runtimePoint, out AbsoluteUniversePosition pointAup))
                return false;

            absolutePoint = pointAup.ToAbsoluteDouble3();
            return math.all(math.isfinite(absolutePoint));
        }

        private bool TryResolveAbsoluteUniversePosition(Vector3 runtimePoint, out AbsoluteUniversePosition pointAup)
        {
            pointAup = default;
            if (!IsFiniteRuntimePosition(runtimePoint) ||
                !TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup))
            {
                return false;
            }

            pointAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePoint.x, runtimePoint.y, runtimePoint.z));
            return IsFiniteAup(in pointAup);
        }

        private bool TryResolveCurrentRuntimeOriginAup(out AbsoluteUniversePosition originAup)
        {
            originAup = _cachedRuntimeOriginAup;
            return _hasCachedRuntimeOriginAup && IsFiniteAup(in originAup);
        }

        private bool RefreshCachedRuntimeOriginAup()
        {
            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
            {
                LaserCutterDodRuntime.ClearPresentationOriginAup();
                _cachedRuntimeOriginAup = default;
                _hasCachedRuntimeOriginAup = false;
                return false;
            }

            LaserCutterDodRuntime.CachePresentationOriginAup(origin);
            _cachedRuntimeOriginAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            _hasCachedRuntimeOriginAup = IsFiniteAup(in _cachedRuntimeOriginAup);
            return _hasCachedRuntimeOriginAup;
        }

        private static bool IsFiniteRuntimePosition(Vector3 runtimePoint)
        {
            return math.isfinite(runtimePoint.x) &&
                   math.isfinite(runtimePoint.y) &&
                   math.isfinite(runtimePoint.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition positionAup)
        {
            return math.isfinite(positionAup.LocalX) &&
                   math.isfinite(positionAup.LocalY) &&
                   math.isfinite(positionAup.LocalZ);
        }

        private void ApplyRecoilImpulse(Vector3 direction, float normalizedPower)
        {
            if (normalizedPower <= 0f)
                return;

            float mass = 1f;
            float recoilScale =
                TryReadPlayerMovementSnapshot(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u
                    ? submergedRecoilScale
                    : 1f;
            float runtimeRecoil = GetRuntimeRecoilImpulse(recoilImpulseBase);
            float impulseMagnitude = Mathf.Min(MaxRecoilImpulse, (runtimeRecoil * normalizedPower * recoilScale) / mass);
            if (impulseMagnitude <= 0.0001f)
                return;

            TryQueuePlayerToolRecoil(direction, impulseMagnitude);
            QueueToolHapticFeedback(normalizedPower, 1f);
        }

        private void EnterCooldownState()
        {
            SetFlag(CooldownState);
            SetFlag(IdleState);
            ClearFlag(ActiveState);
        }

        private void SetOverheatedState()
        {
            SetFlag(OverheatedState);
            ClearFlag(ActiveState);
            ClearFlag(BusyState);
        }

        private void SetFlag(byte flag)
        {
            _toolStateFlags |= flag;
        }

        private void ClearFlag(byte flag)
        {
            _toolStateFlags &= unchecked((byte)~flag);
        }
    }
}
