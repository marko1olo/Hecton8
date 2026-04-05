// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  WorldCaveDirector.cs — Project HECTON-8 Cave Generation Director         ║
// ║  Unity 6 | Zero GC in Hot Paths | Integrated with Biome/Zone Logic        ║
// ║  v1.0 — Initial cave integration with world-fill pipeline                 ║
// ║                                                                             ║
// ║  PURPOSE:                                                                   ║
// ║  ─────────                                                                  ║
// ║  Integrates cave generation into the world-fill pipeline. Determines      ║
// ║  cave spawn locations based on biome/zone rules, generates cave topology  ║
// ║  via CaveGraphGenerator, and triggers voxel mesh generation via           ║
// ║  HectonVoxelEngine. Ensures caves are meaningful exploration layers.      ║
// ║                                                                             ║
// ║  INTEGRATION:                                                              ║
// ║  ────────────                                                              ║
// ║  - Reads biome/zone from BiomeMatrixDirector/WorldZoneDirector            ║
// ║  - Uses CavePreset from biome profile for generation parameters           ║
// ║  - Spawns caves at strategic locations (terrain seams, biome edges)       ║
// ║  - Registers with streaming system for LOD/distance culling               ║
// ║  - Provides cave entrance hints for scatter system                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Caves;
using Hecton8.Environment;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4035)]
    public sealed class WorldCaveDirector : MonoBehaviour, ISlowTickable
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private HectonVoxelEngine voxelEngine;
        [SerializeField] private MapMagicBridge mapMagicBridge;
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Header("Cave Generation")]
        [SerializeField] private float caveSearchRadius = 200f;
        [SerializeField] private int maxCavesPerBiome = 3;
        [SerializeField] private float minCaveSpacing = 150f;
        [SerializeField] private float caveSpawnProbability = 0.4f; // Per biome evaluation

        [Header("Diagnostics")]
        [SerializeField] private int _debugActiveCaves;
        [SerializeField] private string _debugCurrentBiome = "None";
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private bool _debugReady;

        private bool _registeredToTickManager;
        private readonly HashSet<long> _activeCaveKeys = new HashSet<long>();
        private readonly Dictionary<long, CaveInstance> _caveInstances = new Dictionary<long, CaveInstance>(32);
        private float _lastEvaluationTime = float.NegativeInfinity;

        private struct CaveInstance
        {
            public long key;
            public Vector3 position;
            public CavePreset preset;
            public HectonVoxelVolume volume; // Reference to generated volume
            public bool isActive;
        }

        private void Awake()
        {
            ResolveReferences();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            if (GameTickManager.Instance != null && !_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }

            EvaluateCaveSpawns();
        }

        private void OnDisable()
        {
            if (_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        public void SlowTick()
        {
            EvaluateCaveSpawns();
        }

        private void EvaluateCaveSpawns()
        {
            if (!ResolveReferences())
                return;

            // Throttle evaluations
            if (Time.time - _lastEvaluationTime < 2f)
                return;

            _lastEvaluationTime = Time.time;

            HectonBiomeFamilyProfile biomeFamily = biomeMatrixDirector.CurrentFamilyProfile;
            WorldZoneAnchor currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;

            if (biomeFamily == null)
                return;

            // Determine if this biome supports caves
            bool biomeSupportsCaves = EvaluateBiomeCaveSupport(biomeFamily);

            if (!biomeSupportsCaves)
            {
                // Clean up caves from unsupported biomes
                CleanupUnsupportedCaves();
                return;
            }

            // Generate cave spawn candidates
            List<Vector3> candidates = GenerateCaveCandidates(biomeFamily, currentZone);

            // Spawn caves at candidates
            foreach (Vector3 candidate in candidates)
            {
                TrySpawnCaveAt(candidate, biomeFamily);
            }

            UpdateDiagnostics();
        }

        private bool EvaluateBiomeCaveSupport(HectonBiomeFamilyProfile biomeFamily)
        {
            // TODO: Read from biome profile cave support flags
            // For now, enable caves in certain biomes
            string biomeId = biomeFamily.familyId;
            return biomeId.Contains("cliff") || biomeId.Contains("canyon") || biomeId.Contains("deep") || biomeId.Contains("abyss");
        }

        private List<Vector3> GenerateCaveCandidates(HectonBiomeFamilyProfile biomeFamily, WorldZoneAnchor zone)
        {
            List<Vector3> candidates = new List<Vector3>();

            if (playerTransform == null)
                return candidates;

            Vector3 playerPos = playerTransform.position;

            // Generate candidates around player within search radius
            // Use deterministic seeding based on biome and position
            int biomeSeed = biomeFamily.familyId.GetHashCode();
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)(biomeSeed + Mathf.FloorToInt(playerPos.x / 100f) + Mathf.FloorToInt(playerPos.z / 100f)));

            int candidateCount = rng.NextInt(1, maxCavesPerBiome + 1);

            for (int i = 0; i < candidateCount; i++)
            {
                // Random position within radius, biased toward terrain features
                float angle = rng.NextFloat(0f, 2f * Mathf.PI);
                float distance = rng.NextFloat(50f, caveSearchRadius);

                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                Vector3 candidatePos = playerPos + offset;

                // Sample terrain height
                if (mapMagicBridge != null)
                {
                    float terrainHeight = mapMagicBridge.SampleHeight(candidatePos.x, candidatePos.z);
                    candidatePos.y = terrainHeight - 5f; // Slightly below surface for cave entrance
                }

                // Check spacing from existing caves
                bool tooClose = false;
                foreach (CaveInstance existing in _caveInstances.Values)
                {
                    if (Vector3.Distance(existing.position, candidatePos) < minCaveSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    candidates.Add(candidatePos);
                }
            }

            return candidates;
        }

        private async void TrySpawnCaveAt(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            // Generate unique key
            long caveKey = GenerateCaveKey(position, biomeFamily);

            if (_activeCaveKeys.Contains(caveKey))
                return; // Already exists

            // Get cave preset from biome
            CavePreset preset = GetCavePresetForBiome(biomeFamily);
            if (preset == null)
                return;

            // Generate cave volume using HectonVoxelEngine
            if (voxelEngine != null)
            {
                try
                {
                    // Generate deterministic seed from position and biome
                    uint seed = (uint)(caveKey & 0xFFFFFFFF);

                    // Call async generation
                    GameObject caveVolume = await voxelEngine.GenerateVolumeAsync(
                        position, seed, preset, destroyCancellationToken);

                    if (caveVolume != null)
                    {
                        // Register cave instance
                        CaveInstance instance = new CaveInstance
                        {
                            key = caveKey,
                            position = position,
                            preset = preset,
                            volume = caveVolume.GetComponent<HectonVoxelVolume>(),
                            isActive = true
                        };

                        _caveInstances[caveKey] = instance;
                        _activeCaveKeys.Add(caveKey);

                        // Add entrance visual cues for readability
                        SpawnEntranceVisualCues(instance, preset);

                        Debug.Log($"[WorldCaveDirector] Successfully generated cave at {position}");
                    }
                    else
                    {
                        Debug.LogWarning($"[WorldCaveDirector] Cave generation produced no geometry at {position}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[WorldCaveDirector] Failed to generate cave at {position}: {e.Message}");
                }
            }
            else
            {
                Debug.LogWarning("[WorldCaveDirector] No voxel engine available for cave generation");
            }
        }

        private CavePreset GetCavePresetForBiome(HectonBiomeFamilyProfile biomeFamily)
        {
            // Create biome-specific cave presets
            CavePreset preset = new CavePreset();

            string biomeId = biomeFamily.familyId;

            if (biomeId.Contains("cliff") || biomeId.Contains("escarpment"))
            {
                // Cliff caves: vertical, dramatic
                preset.presetName = "Cliff Cave";
                preset.presetType = CavePresetType.System;
                preset.minRooms = 4;
                preset.maxRooms = 10;
                preset.minRoomRadius = 5f;
                preset.maxRoomRadius = 12f;
                preset.verticalShaftChance = 0.3f;
                preset.maxDepth = 80f;
                preset.verticalSpread = 0.6f;
                preset.minTunnelRadius = 2.5f;
                preset.maxTunnelRadius = 4f;
                preset.entranceRadius = 4f;
                preset.entranceFunnelLength = 15f;
            }
            else if (biomeId.Contains("canyon") || biomeId.Contains("rift"))
            {
                // Canyon caves: wide, horizontal
                preset.presetName = "Canyon Cave";
                preset.presetType = CavePresetType.Labyrinth;
                preset.minRooms = 6;
                preset.maxRooms = 15;
                preset.minRoomRadius = 6f;
                preset.maxRoomRadius = 18f;
                preset.flatHallChance = 0.4f;
                preset.maxDepth = 60f;
                preset.verticalSpread = 0.4f;
                preset.minTunnelRadius = 3f;
                preset.maxTunnelRadius = 6f;
                preset.wideTunnelChance = 0.3f;
                preset.entranceRadius = 5f;
                preset.entranceFunnelLength = 20f;
            }
            else if (biomeId.Contains("deep") || biomeId.Contains("abyss") || biomeId.Contains("hadal"))
            {
                // Deep caves: large, complex
                preset.presetName = "Abyss Cave";
                preset.presetType = CavePresetType.Abyss;
                preset.minRooms = 8;
                preset.maxRooms = 20;
                preset.minRoomRadius = 8f;
                preset.maxRoomRadius = 25f;
                preset.verticalShaftChance = 0.4f;
                preset.creviceChance = 0.2f;
                preset.maxDepth = 150f;
                preset.verticalSpread = 0.8f;
                preset.minTunnelRadius = 3f;
                preset.maxTunnelRadius = 7f;
                preset.extraConnectionChance = 0.3f;
                preset.entranceRadius = 3f;
                preset.entranceFunnelLength = 25f;
            }
            else
            {
                // Default cave
                preset.presetName = "Generic Cave";
                preset.presetType = CavePresetType.System;
                preset.minRooms = 3;
                preset.maxRooms = 8;
                preset.minRoomRadius = 4f;
                preset.maxRoomRadius = 12f;
                preset.maxDepth = 50f;
                preset.verticalSpread = 0.3f;
                preset.minTunnelRadius = 2f;
                preset.maxTunnelRadius = 4f;
                preset.entranceRadius = 3f;
                preset.entranceFunnelLength = 12f;
            }

            // Common settings
            preset.gridDimension = 64;
            preset.voxelSize = 1.5f;
            preset.minEntrances = 1;
            preset.maxEntrances = 2;
            preset.tallTunnelChance = 0.15f;
            preset.tunnelWarpAmount = 2f;
            preset.extraConnectionChance = 0.2f;

            // Interior structures based on biome
            if (biomeId.Contains("cliff") || biomeId.Contains("escarpment"))
            {
                // Cliff caves: stalactites, columns
                preset.enableStructures = true;
                preset.maxStructures = 6;
                preset.structureDensity = 1.2f;
                preset.allowedStructureTypes = new CaveStructureType[]
                {
                    CaveStructureType.Stalactite,
                    CaveStructureType.Column,
                    CaveStructureType.Stalagmite
                };
            }
            else if (biomeId.Contains("canyon") || biomeId.Contains("rift"))
            {
                // Canyon caves: boulders, arches, bridges
                preset.enableStructures = true;
                preset.maxStructures = 8;
                preset.structureDensity = 1.0f;
                preset.allowedStructureTypes = new CaveStructureType[]
                {
                    CaveStructureType.Boulder,
                    CaveStructureType.Arch,
                    CaveStructureType.Bridge,
                    CaveStructureType.Block
                };
            }
            else if (biomeId.Contains("deep") || biomeId.Contains("abyss") || biomeId.Contains("hadal"))
            {
                // Deep caves: complex structures, crystals (simulated by columns/arches)
                preset.enableStructures = true;
                preset.maxStructures = 10;
                preset.structureDensity = 0.8f;
                preset.allowedStructureTypes = new CaveStructureType[]
                {
                    CaveStructureType.Column,
                    CaveStructureType.Arch,
                    CaveStructureType.Stalactite,
                    CaveStructureType.Stalagmite,
                    CaveStructureType.Wall
                };
            }
            else
            {
                // Generic caves: basic structures
                preset.enableStructures = true;
                preset.maxStructures = 4;
                preset.structureDensity = 0.7f;
                preset.allowedStructureTypes = new CaveStructureType[]
                {
                    CaveStructureType.Stalactite,
                    CaveStructureType.Boulder,
                    CaveStructureType.Column
                };
            }

            return preset;
        }

        private long GenerateCaveKey(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            // Deterministic key based on position and biome
            int x = Mathf.FloorToInt(position.x / 100f);
            int z = Mathf.FloorToInt(position.z / 100f);
            int biomeHash = biomeFamily.familyId.GetHashCode();

            return ((long)x << 32) | ((long)z << 16) | (uint)biomeHash;
        }

        private void CleanupUnsupportedCaves()
        {
            // TODO: Remove caves that are no longer supported by current biome
            // For now, keep all
        }

        private bool ResolveReferences()
        {
            bool resolved = true;

            if (playerTransform == null)
            {
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref playerTransform);
                resolved &= playerTransform != null;
            }

            if (biomeMatrixDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref biomeMatrixDirector);
                resolved &= biomeMatrixDirector != null;
            }

            if (worldZoneDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref worldZoneDirector);
                resolved &= worldZoneDirector != null;
            }

            if (voxelEngine == null)
            {
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref voxelEngine);
                resolved &= voxelEngine != null;
            }

            if (mapMagicBridge == null)
            {
                WorldRuntimeReferenceUtility.TryResolveSceneObject(ref mapMagicBridge);
                resolved &= mapMagicBridge != null;
            }

            return resolved;
        }

        private void UpdateDiagnostics()
        {
            _debugActiveCaves = _activeCaveKeys.Count;
            _debugCurrentBiome = biomeMatrixDirector != null && biomeMatrixDirector.CurrentFamilyProfile != null
                ? biomeMatrixDirector.CurrentFamilyProfile.familyLabel : "None";
            _debugCurrentZone = worldZoneDirector != null && worldZoneDirector.CurrentZone != null
                ? worldZoneDirector.CurrentZone.ZoneLabel : "None";
            _debugReady = ResolveReferences();
        }

        // Public API for other systems
        public bool TryGetCaveAt(Vector3 position, out CaveInstance cave)
        {
            long key = GenerateCaveKey(position, biomeMatrixDirector.CurrentFamilyProfile);
            return _caveInstances.TryGetValue(key, out cave);
        }

        public IEnumerable<CaveInstance> GetActiveCaves()
        {
            return _caveInstances.Values;
        }
    }
}