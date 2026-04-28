// ============================================================================
// HECTON-8 — HarvestablePlant.cs
// Large plant (Creepvine/Kelp) from which the player can harvest seeds/leaves.
//
// ARCHITECTURE:
//   • Standalone prop — implements ICuttable for knife interaction.
//   • Sub-mesh based harvesting with regrowth.
//   • MaterialPropertyBlock for harvest glow effect.
//   • ITickable for regrowth timer.
//
// ZERO GC:
//   • ICuttable.ApplyCutDamage — event-driven, no Update.
//   • ITickable.Tick() — for regrowth timer only.
//   • Cached Transform, Renderer.
//   • MaterialPropertyBlock for VFX.
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
    public sealed class HarvestablePlant : MonoBehaviour, ICuttable, ITickable, IUpdatable
    {
        // ══════════════════════════════════════════════════════════
        //  SHADER PROPERTY IDs — cached once, zero GC
        // ══════════════════════════════════════════════════════════

        private static readonly int _HarvestGlowID = Shader.PropertyToID("_HarvestGlow");
        private static readonly int _GlowIntensityID = Shader.PropertyToID("_GlowIntensity");

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
        private bool _poolMissingLogged;

        /// <summary>
        /// Cached MaterialPropertyBlock for harvest VFX.
        /// Allocated once in Awake — zero GC in hot path.
        /// </summary>
        private MaterialPropertyBlock _mpb; // COLD ALLOC: MaterialPropertyBlock[1] — harvest glow VFX — owner: HarvestablePlant

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

            // COLD ALLOC: MaterialPropertyBlock — harvest VFX
            _mpb = new MaterialPropertyBlock();

            // Initialize regrow timers
            _regrowTimers = new float[segments.Length];
            for (int i = 0; i < _regrowTimers.Length; i++)
            {
                _regrowTimers[i] = 0f;
            }
        }

        private void OnEnable()
        {
            // Register for tick if any segments are regrowing
            CheckRegistration();
        }

        private void OnDisable()
        {
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
                UnregisterFromTick();
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
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < segments.Length; i++)
            {
                if (!segments[i].isAvailable) continue;
                if (segments[i].meshRenderer == null) continue;

                float distance = Vector3.Distance(hitPoint, segments[i].meshRenderer.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
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
            if (cutSound != null && SpatialAudioManager.TryGetInstance(out var audio))
            {
                audio.PlayAtPoint(cutSound, hitPoint, cutVolume);
            }

            // Spawn loot
            SpawnLoot(segment, hitPoint);

            // Start regrow timer
            _regrowTimers[index] = regrowTime;

            // Register for tick
            RegisterToTick();

            // Fire event
            OnSegmentHarvested?.Invoke(index);
        }

        private void SpawnLoot(PlantSegment segment, Vector3 hitPoint)
        {
            if (segment.lootPrefab == null) return;

            // Random scatter
            Vector2 offset2D = Random.insideUnitCircle * lootScatterRadius;
            Vector3 spawnPos = hitPoint + new Vector3(offset2D.x, 0.1f, offset2D.y);

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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

            GameObject loot = pool.Spawn(segment.lootPrefab, spawnPos, Random.rotation);
            if (loot == null) return;

            // Apply upward force
            if (loot.TryGetComponent(out Rigidbody rb))
            {
                PhysicsForceRouter.QueueForce(rb, Vector3.up * lootUpwardForce, ForceMode.Impulse);
            }
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
            if (_isRegistered) return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void UnregisterFromTick()
        {
            if (!_isRegistered) return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = false;
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
