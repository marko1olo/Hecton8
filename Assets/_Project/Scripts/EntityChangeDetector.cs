// ============================================================================
// HECTON-8 — EntityChangeDetector.cs  v1.0
// Efficient change detection for game entities without continuous polling.
//
// PURPOSE:
//   Many systems need to react to entity changes (position, rotation, health, state).
//   Polling every entity every frame = O(N) CPU waste.
//   This offers event-driven change notification with manual dirty flagging.
//
// WHY THIS MATTERS:
//   • Creature movement: don't check 100+ creatures every frame for position changes.
//   • Player state changes: only notify when health/inventory/tools actually change.
//   • Building placement: only re-validate placement when position changes.
//   • Reduces O(N) polling loops to event-driven notifications.
//
// USAGE:
//
//   // Mark position as changed
//   detector.MarkDirty(EntityChangeFlag.Position);
//
//   // In update loop, invoke pending changes
//   detector.FlushChanges(); // Calls all registered callbacks
//
//   // Subscribe to specific changes
//   detector.OnPositionChanged += (oldPos, newPos) => { };
//   detector.OnHealthChanged += (oldHealth, newHealth) => { };
//
// ZERO-GC DESIGN:
//   • Flags are bitmask (byte) — stack allocated.
//   • Events are Action<T1, T2> delegates (cached, no closure).
//   • No List allocations in hot path.
//   • FlushChanges() is O(1) per detector.
//
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Bitmask for entity change types.
    /// Use as flags: MarkDirty(Position | Rotation | Health).
    /// </summary>
    [Flags]
    public enum EntityChangeFlag : byte
    {
        None = 0,
        Position = 1 << 0,    // Transform position
        Rotation = 1 << 1,    // Transform rotation
        Scale = 1 << 2,       // Transform scale
        Health = 1 << 3,      // Health/integrity status
        State = 1 << 4,       // FSM/behavioral state
        Inventory = 1 << 5,   // Inventory contents
        Active = 1 << 6,      // Active/inactive status
        Velocity = 1 << 7,    // Linear/angular velocity

        All = byte.MaxValue
    }

    /// <summary>
    /// Change detector for a single entity.
    /// Struct-based, zero heap allocation for metadata.
    /// </summary>
    public class EntityChangeDetector
    {
        private readonly string _entityId;
        private EntityChangeFlag _dirtyFlags = EntityChangeFlag.None;

        // ════════════════════════════════════════════════════════════
        //  EVENTS — One per change type
        // ════════════════════════════════════════════════════════════

        public event Action<Vector3, Vector3> OnPositionChanged;
        public event Action<Quaternion, Quaternion> OnRotationChanged;
        public event Action<Vector3, Vector3> OnScaleChanged;
        public event Action<float, float> OnHealthChanged;
        public event Action<int, int> OnStateChanged;
        public event Action<int> OnInventoryChanged;
        public event Action<bool, bool> OnActiveChanged;
        public event Action<Vector3, Vector3> OnVelocityChanged;

        // ════════════════════════════════════════════════════════════
        //  CACHED VALUES — For change comparison
        // ════════════════════════════════════════════════════════════

        private Vector3 _lastPosition = Vector3.zero;
        private Quaternion _lastRotation = Quaternion.identity;
        private Vector3 _lastScale = Vector3.one;
        private float _lastHealth = 1f;
        private int _lastState = 0;
        private int _lastInventoryHash = 0;
        private bool _lastActive = true;
        private Vector3 _lastVelocity = Vector3.zero;

        // ════════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ════════════════════════════════════════════════════════════

        public EntityChangeDetector(string entityId)
        {
            _entityId = entityId;
        }

        /// <summary>
        /// Mark one or more changes as dirty.
        /// Call when you know an entity property has changed.
        /// </summary>
        public void MarkDirty(EntityChangeFlag flags)
        {
            _dirtyFlags |= flags;
        }

        /// <summary>
        /// Mark all changes as clean (no pending notifications).
        /// </summary>
        public void ClearDirty()
        {
            _dirtyFlags = EntityChangeFlag.None;
        }

        /// <summary>
        /// Check if specific change is marked dirty.
        /// </summary>
        public bool IsDirty(EntityChangeFlag flag)
        {
            return (_dirtyFlags & flag) != 0;
        }

        // ════════════════════════════════════════════════════════════
        //  FLUSH CHANGES — Invoke callbacks for dirty flags
        // ════════════════════════════════════════════════════════════

        /// <summary>
        /// Invoke all pending change callbacks.
        /// Caller provides current values for comparison.
        /// </summary>
        public void FlushChanges(Vector3? currentPos = null, Quaternion? currentRot = null,
            Vector3? currentScale = null, float? currentHealth = null, int? currentState = null,
            int? inventoryHash = null, bool? isActive = null, Vector3? currentVelocity = null)
        {
            if (_dirtyFlags == EntityChangeFlag.None)
                return;

            // Position
            if ((_dirtyFlags & EntityChangeFlag.Position) != 0 && currentPos.HasValue)
            {
                Vector3 pos = currentPos.Value;
                if (pos != _lastPosition)
                {
                    OnPositionChanged?.Invoke(_lastPosition, pos);
                    _lastPosition = pos;
                }
            }

            // Rotation
            if ((_dirtyFlags & EntityChangeFlag.Rotation) != 0 && currentRot.HasValue)
            {
                Quaternion rot = currentRot.Value;
                if (rot != _lastRotation)
                {
                    OnRotationChanged?.Invoke(_lastRotation, rot);
                    _lastRotation = rot;
                }
            }

            // Scale
            if ((_dirtyFlags & EntityChangeFlag.Scale) != 0 && currentScale.HasValue)
            {
                Vector3 scale = currentScale.Value;
                if (scale != _lastScale)
                {
                    OnScaleChanged?.Invoke(_lastScale, scale);
                    _lastScale = scale;
                }
            }

            // Health
            if ((_dirtyFlags & EntityChangeFlag.Health) != 0 && currentHealth.HasValue)
            {
                float health = currentHealth.Value;
                if (health != _lastHealth)
                {
                    OnHealthChanged?.Invoke(_lastHealth, health);
                    _lastHealth = health;
                }
            }

            // State
            if ((_dirtyFlags & EntityChangeFlag.State) != 0 && currentState.HasValue)
            {
                int state = currentState.Value;
                if (state != _lastState)
                {
                    OnStateChanged?.Invoke(_lastState, state);
                    _lastState = state;
                }
            }

            // Inventory
            if ((_dirtyFlags & EntityChangeFlag.Inventory) != 0 && inventoryHash.HasValue)
            {
                int hash = inventoryHash.Value;
                if (hash != _lastInventoryHash)
                {
                    OnInventoryChanged?.Invoke(hash);
                    _lastInventoryHash = hash;
                }
            }

            // Active
            if ((_dirtyFlags & EntityChangeFlag.Active) != 0 && isActive.HasValue)
            {
                bool active = isActive.Value;
                if (active != _lastActive)
                {
                    OnActiveChanged?.Invoke(_lastActive, active);
                    _lastActive = active;
                }
            }

            // Velocity
            if ((_dirtyFlags & EntityChangeFlag.Velocity) != 0 && currentVelocity.HasValue)
            {
                Vector3 vel = currentVelocity.Value;
                if (vel != _lastVelocity)
                {
                    OnVelocityChanged?.Invoke(_lastVelocity, vel);
                    _lastVelocity = vel;
                }
            }

            _dirtyFlags = EntityChangeFlag.None;
        }

        /// <summary>
        /// Simplified flush for Transform-only changes.
        /// Checks Position, Rotation, Scale, Active.
        /// </summary>
        public void FlushTransformChanges(Transform transform)
        {
            if (transform == null) return;

            FlushChanges(
                currentPos: (_dirtyFlags & EntityChangeFlag.Position) != 0 ? transform.position : (Vector3?)null,
                currentRot: (_dirtyFlags & EntityChangeFlag.Rotation) != 0 ? transform.rotation : (Quaternion?)null,
                currentScale: (_dirtyFlags & EntityChangeFlag.Scale) != 0 ? transform.localScale : (Vector3?)null,
                isActive: (_dirtyFlags & EntityChangeFlag.Active) != 0 ? transform.gameObject.activeSelf : (bool?)null
            );
        }

        // ════════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ════════════════════════════════════════════════════════════

        public override string ToString() =>
            $"[EntityChangeDetector:{_entityId}] Dirty={_dirtyFlags}";
    }

    /// <summary>
    /// Global manager for entity change detectors.
    /// Runtime owners flush detectors explicitly when their own state changes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EntityChangeManager : MonoBehaviour, IServiceHeartbeat, IServiceShutdown
    {
        // COLD ALLOC: Dictionary<string, EntityChangeDetector>[256] - entity change detector registry - owner: EntityChangeManager
        private static readonly Dictionary<string, EntityChangeDetector> _detectors = new Dictionary<string, EntityChangeDetector>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _detectors.Clear();
        }
        private bool _serviceRegistered;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        /// <summary>
        /// Get or create detector for an entity.
        /// </summary>
        public static EntityChangeDetector GetOrCreateDetector(string entityId)
        {
            if (!_detectors.TryGetValue(entityId, out var detector))
            {
                detector = new EntityChangeDetector(entityId);
                _detectors[entityId] = detector;
            }

            return detector;
        }

        /// <summary>
        /// Remove detector for an entity.
        /// </summary>
        public static void RemoveDetector(string entityId)
        {
            _detectors.Remove(entityId);
        }

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;
        }

        private void OnEnable()
        {
            TryRegisterService();
        }

        private void OnDisable()
        {
            OnServiceShutdown();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        /// <inheritdoc />
        public void OnServiceShutdown()
        {
            TryUnregisterService();
            _detectors.Clear();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterEntityChangeManagerRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.EntityChanges, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            EntityChangeManager registered = GlobalRegistry.EntityChanges;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsEntityChangeRuntimeUsable(registered))
            {
                Destroy(gameObject);
                return true;
            }

            GlobalRegistry.UnregisterEntityChangeManagerRuntime(registered);
            return false;
        }

        private static bool IsEntityChangeRuntimeUsable(EntityChangeManager manager)
        {
            return manager != null &&
                   manager._serviceRegistered &&
                   manager.isActiveAndEnabled;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterEntityChangeManagerRuntime(this);
            _serviceRegistered = false;
        }

        /// <summary>
        /// Get count of active detectors (for diagnostics).
        /// </summary>
        public static int GetActiveDetectorCount() => _detectors.Count;
    }
}
