// ============================================================================
// HECTON-8 — HarvestablePlant.cs
// Large plant (Creepvine/Kelp) from which the player can harvest seeds/leaves.
//
// ARCHITECTURE:
//   • Standalone prop — implements ICuttable for knife interaction.
//   • Sub-mesh based harvesting with regrowth.
//   • ITickable for regrowth timer.
//
// ZERO GC:
//   • ICuttable.ApplyCutDamage — event-driven, no Update.
//   • ITickable.Tick() — for regrowth timer only.
//   • Cached Transform, Renderer.
//
// USAGE:
//   1. Place on plant GameObject with multiple sub-meshes (segments).
//   2. Configure harvestable segments and loot.
//   3. Player uses knife to harvest, segment regrows after time.
// ============================================================================

using Hecton8.Audio;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Represents a harvestable segment of the plant.
    /// </summary>
    [System.Serializable]
    public class PlantSegment
    {
        [Tooltip("Mesh renderer for this segment.")]
        public Renderer meshRenderer;

        [Tooltip("Loot prefab to spawn when harvested.")]
        public GameObject lootPrefab;

        [Tooltip("Particle effect when cut.")]
        public ParticleSystem cutParticles;

        [Tooltip("Is this segment currently available?")]
        public bool isAvailable = true;
    }

    /// <summary>
    /// Large plant from which the player can harvest seeds/leaves using a knife.
    /// Implements ICuttable for knife/laser cutter integration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HarvestablePlant : MonoBehaviour, ICuttable, ITickable, IUpdatable, IGlobalRegistryHotSwapListener
    {
        private const float OneOver127 = 1f / 127f;

        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SEGMENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Segments ────────────────────────────────────")]
        [Tooltip("Harvestable segments of the plant.")]
        [SerializeField] private PlantSegment[] segments;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REGROWTH
        // ══════════════════════════════════════════════════════════

        [Header("── Regrowth ─────────────────────────────────────")]
        [Tooltip("Time in seconds for a segment to regrow.")]
        [SerializeField, Range(10f, 600f)] private float regrowTime = 120f;

        [Tooltip("Should segments regrow automatically?")]
        [SerializeField] private bool autoRegrow = true;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HARVEST
        // ══════════════════════════════════════════════════════════

        [Header("── Harvest ──────────────────────────────────────")]
        [Tooltip("Damage required to harvest one segment.")]
        [SerializeField, Range(1f, 50f)] private float harvestDamage = 10f;

        [Tooltip("Scatter radius for loot spawn.")]
        [SerializeField, Range(0f, 2f)] private float lootScatterRadius = 0.3f;

        [Tooltip("Upward force applied to loot.")]
        [SerializeField, Range(0f, 5f)] private float lootUpwardForce = 1f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AUDIO
        // ══════════════════════════════════════════════════════════

        [Header("── Audio ───────────────────────────────────────")]
        [Tooltip("Sound played when segment is cut.")]
        [SerializeField] private AudioClip cutSound;

        [Tooltip("Volume for cut sound.")]
        [SerializeField, Range(0f, 1f)] private float cutVolume = 0.7f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUALS
        // ══════════════════════════════════════════════════════════

        [Header("── Visuals ──────────────────────────────────────")]
        [Tooltip("Color of harvest glow effect.")]
        [SerializeField] private Color harvestGlowColor = new Color(0.5f, 1f, 0.3f);

        // ══════════════════════════════════════════════════════════
        //  EVENTS
        // ══════════════════════════════════════════════════════════

        [Header("── Events ──────────────────────────────────────")]
        [Tooltip("Invoked when a segment is harvested.")]
        [SerializeField] private UnityEvent<int> OnSegmentHarvested;

        [Tooltip("Invoked when a segment regrows.")]
        [SerializeField] private UnityEvent<int> OnSegmentRegrown;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private Transform _transform;
        private float[] _regrowTimers;
        private bool _isRegistered;
        private bool _tickDormant;
        private bool _hotSwapListenerRegistered;
        private bool _poolMissingLogged;
        private uint _lootScatterSeed;
        private IAudioService _audioService;
        private ObjectPoolManager _objectPool;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC ACCESSORS
        // ══════════════════════════════════════════════════════════

        /// <summary>Number of available segments.</summary>
        public int AvailableSegments
        {
            get
            {
                int count = 0;
                for (int i = 0; i < segments.Length; i++)
                {
                    if (segments[i].isAvailable) count++;
                }
                return count;
            }
        }

        /// <summary>Total number of segments.</summary>
        public int TotalSegments => segments.Length;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _transform = transform;
            _lootScatterSeed = MixLootHash(2166136261u, (uint)EntityId.ToULong(gameObject.GetEntityId()));
            CacheRegistryServicesCold();

            // Initialize regrow timers
            _regrowTimers = new float[segments.Length];
            for (int i = 0; i < _regrowTimers.Length; i++)
            {
                _regrowTimers[i] = 0f;
            }
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();

            // Register for tick if any segments are regrowing
            CheckRegistration();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            UnregisterFromTick();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager every frame.
        /// Handles regrowth timers.
        /// </summary>
        /// <param name="deltaTime">Time.deltaTime.</param>
        public void Tick(float deltaTime)
        {
            if (!autoRegrow) return;
            if (_tickDormant) return;

            bool anyRegrowing = false;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].isAvailable) continue;
                if (_regrowTimers[i] <= 0f) continue;

                _regrowTimers[i] -= deltaTime;

                if (_regrowTimers[i] <= 0f)
                {
                    RegrowSegment(i);
                }
                else
                {
                    anyRegrowing = true;
                }
            }

            // Unregister if no more segments regrowing
            if (!anyRegrowing)
            {
                _tickDormant = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ICuttable
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by KnifeTool or LaserCutter when hitting this plant.
        /// Harvests a segment if damage threshold is met.
        /// </summary>
        /// <param name="damage">Damage amount.</param>
        /// <param name="hitPoint">World position of the hit.</param>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            if (damage < harvestDamage) return;

            // Find nearest available segment
            int nearestIndex = FindNearestAvailableSegment(hitPoint);
            if (nearestIndex < 0) return;

            HarvestSegment(nearestIndex, hitPoint);
        }

        // ══════════════════════════════════════════════════════════
        //  HARVESTING
        // ══════════════════════════════════════════════════════════

        private int FindNearestAvailableSegment(Vector3 hitPoint)
        {
            int nearestIndex = -1;
            float nearestDistanceSq = float.MaxValue;

            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].isAvailable) continue;
                if (segments[i].meshRenderer == null) continue;

                Vector3 segmentVisualPosition = segments[i].meshRenderer.transform.position;
                Vector3 segmentVisualDelta = hitPoint - segmentVisualPosition;
                float distanceSq = segmentVisualDelta.sqrMagnitude;
                if (distanceSq < nearestDistanceSq)
                {
                    nearestDistanceSq = distanceSq;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private void HarvestSegment(int index, Vector3 hitPoint)
        {
            if (index < 0 || index >= segments.Length) return;
            if (!segments[index].isAvailable) return;

            PlantSegment segment = segments[index];
            segment.isAvailable = false;

            // Disable mesh
            if (segment.meshRenderer != null)
            {
                segment.meshRenderer.enabled = false;
            }

            // Play cut particles
            if (segment.cutParticles != null)
            {
                segment.cutParticles.transform.position = hitPoint;
                segment.cutParticles.Play();
            }

            // Play cut sound
            IAudioService audio = _audioService;
            if (cutSound != null && audio != null)
            {
                audio.PlayAtPoint(cutSound, hitPoint, cutVolume);
            }

            // Spawn loot
            SpawnLoot(segment, index, hitPoint);

            // Start regrow timer
            _regrowTimers[index] = regrowTime;

            // Register for tick
            RegisterToTick();

            // Fire event
            OnSegmentHarvested?.Invoke(index);
        }

        private void SpawnLoot(PlantSegment segment, int segmentIndex, Vector3 hitPoint)
        {
            if (segment.lootPrefab == null) return;

            ResolveDeterministicLootPose(segment, segmentIndex, hitPoint, out Vector3 spawnPos, out Quaternion spawnRotation);

            ObjectPoolManager pool = _objectPool;
            if (pool == null)
            {
                if (!_poolMissingLogged)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning("[HarvestablePlant] ObjectPoolManager unavailable. Loot spawn skipped to avoid runtime Instantiate.", this);
#endif
                    _poolMissingLogged = true;
                }

                return;
            }

            GameObject loot = pool.Spawn(segment.lootPrefab, spawnPos, spawnRotation);
            if (loot == null) return;

            // Apply upward force
            if (loot.TryGetComponent(out Rigidbody rb))
            {
                PhysicsForceRouter.QueueForce(rb, Vector3.up * lootUpwardForce, ForceMode.Impulse);
            }
        }

        private void ResolveDeterministicLootPose(
            PlantSegment segment,
            int segmentIndex,
            Vector3 hitPoint,
            out Vector3 spawnPosition,
            out Quaternion spawnRotation)
        {
            Vector3 anchor = segment.meshRenderer != null ? segment.meshRenderer.transform.position : hitPoint;

            uint hash = _lootScatterSeed;
            hash = MixLootHash(hash, (uint)segmentIndex);
            if (TryResolveRuntimeAup(anchor, out AbsoluteUniversePosition anchorAup))
            {
                hash = MixLootHash(hash, (uint)anchorAup.GridX);
                hash = MixLootHash(hash, (uint)((ulong)anchorAup.GridX >> 32));
                hash = MixLootHash(hash, (uint)anchorAup.GridY);
                hash = MixLootHash(hash, (uint)((ulong)anchorAup.GridY >> 32));
                hash = MixLootHash(hash, (uint)anchorAup.GridZ);
                hash = MixLootHash(hash, (uint)((ulong)anchorAup.GridZ >> 32));
            }
            else
            {
                hash = MixLootHash(hash, math.asuint(anchor.x));
                hash = MixLootHash(hash, math.asuint(anchor.y));
                hash = MixLootHash(hash, math.asuint(anchor.z));
            }

            hash = FinalizeLootHash(hash);

            float offsetX = SignedUnitFromByte((byte)hash) * lootScatterRadius;
            float offsetZ = SignedUnitFromByte((byte)(hash >> 8)) * lootScatterRadius;
            spawnPosition = hitPoint + new Vector3(offsetX, 0.1f, offsetZ);
            spawnRotation = CardinalYRotation((hash >> 16) & 3u);
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

        private static float SignedUnitFromByte(byte value)
        {
            return (((int)value & 0xFF) - 127) * OneOver127;
        }

        private static Quaternion CardinalYRotation(uint value)
        {
            switch (value & 3u)
            {
                case 1u:
                    return new Quaternion(0f, 0.70710677f, 0f, 0.70710677f);
                case 2u:
                    return new Quaternion(0f, 1f, 0f, 0f);
                case 3u:
                    return new Quaternion(0f, -0.70710677f, 0f, 0.70710677f);
                default:
                    return Quaternion.identity;
            }
        }

        private static uint MixLootHash(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static uint FinalizeLootHash(uint hash)
        {
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return hash;
        }

        private void RegrowSegment(int index)
        {
            if (index < 0 || index >= segments.Length) return;

            PlantSegment segment = segments[index];
            segment.isAvailable = true;

            // Enable mesh
            if (segment.meshRenderer != null)
            {
                segment.meshRenderer.enabled = true;
            }

            // Fire event
            OnSegmentRegrown?.Invoke(index);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Manually harvests a specific segment.
        /// </summary>
        /// <param name="index">Segment index.</param>
        public void HarvestSegment(int index)
        {
            if (index < 0 || index >= segments.Length) return;
            if (segments[index].meshRenderer == null) return;

            HarvestSegment(index, segments[index].meshRenderer.transform.position);
        }

        /// <summary>
        /// Manually regrows a specific segment.
        /// </summary>
        /// <param name="index">Segment index.</param>
        public void ForceRegrow(int index)
        {
            RegrowSegment(index);
        }

        /// <summary>
        /// Regrows all segments immediately.
        /// </summary>
        public void RegrowAll()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].isAvailable)
                {
                    RegrowSegment(i);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  TICK REGISTRATION
        // ══════════════════════════════════════════════════════════

        private void CheckRegistration()
        {
            for (int i = 0; i < _regrowTimers.Length; i++)
            {
                if (_regrowTimers[i] > 0f)
                {
                    RegisterToTick();
                    return;
                }
            }
        }

        private void RegisterToTick()
        {
            _tickDormant = false;
            if (_isRegistered) return;
            if (!Application.isPlaying) return;

            _isRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void UnregisterFromTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
            _tickDormant = false;
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
            _audioService = GlobalRegistry.Audio;
            _objectPool = GlobalRegistry.ObjectPool;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Audio:
                    _audioService = currentService as IAudioService;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as ObjectPoolManager;
                    break;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (segments == null || segments.Length == 0) return;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].meshRenderer == null)
                {
                    Debug.LogWarning($"[HarvestablePlant] Segment {i} has no mesh renderer assigned.", this);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (segments == null) return;

            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].meshRenderer == null) continue;

                Gizmos.color = segments[i].isAvailable
                    ? new Color(0.3f, 1f, 0.5f, 0.3f)
                    : new Color(0.5f, 0.5f, 0.5f, 0.3f);

                Gizmos.DrawWireSphere(segments[i].meshRenderer.transform.position, 0.15f);
            }
        }
#endif
    }
}

