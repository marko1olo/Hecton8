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

using System;
using System.Collections.Generic;
using System.Threading;
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
        private enum CaveBiomePresetKind : byte
        {
            Generic = 0,
            Cliff = 1,
            Canyon = 2,
            Abyss = 3
        }

        private sealed class PendingCaveSpawnState : IDisposable
        {
            public CancellationTokenSource Cancellation;

            public void Dispose()
            {
                if (Cancellation == null)
                    return;

                Cancellation.Dispose();
                Cancellation = null;
            }
        }

        private struct CachedBiomeRuntimeContext
        {
            public HectonBiomeFamilyProfile Family;
            public string FamilyId;
            public string FamilyLabel;
            public int FamilyHash;
            public bool SupportsCaves;
            public CaveBiomePresetKind PresetKind;
        }

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
        [SerializeField] private int _debugPendingCaves;
        [SerializeField] private string _debugCurrentBiome = "None";
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private bool _debugReady;

        private bool _registeredToTickManager;
        private readonly HashSet<long> _activeCaveKeys = new HashSet<long>();
        private readonly Dictionary<long, CaveInstance> _caveInstances = new Dictionary<long, CaveInstance>(32);
        private readonly Dictionary<long, PendingCaveSpawnState> _pendingCaveSpawns = new Dictionary<long, PendingCaveSpawnState>(16);
        private readonly List<Vector3> _candidateBuffer = new List<Vector3>(8); // COLD ALLOC: buffered cave candidates, capped by maxCavesPerBiome.
        private readonly List<long> _staleCaveKeyBuffer = new List<long>(16); // COLD ALLOC: stale cave cleanup buffer, capped by active cave count around player.
        private readonly List<long> _pendingCaveKeyBuffer = new List<long>(16); // COLD ALLOC: buffered pending cave keys for deterministic cancel/cleanup without mutating dictionaries during enumeration.
        private CachedBiomeRuntimeContext _cachedBiomeRuntimeContext;
        private float _lastEvaluationTime = float.NegativeInfinity;
        private CancellationTokenSource _lifetimeCancellation;
        private static readonly int _CrustIntensityId = Shader.PropertyToID("_CrustIntensity");
        private static readonly int _CrustColorId = Shader.PropertyToID("_CrustColor");
        private static readonly int _CrustRoughnessId = Shader.PropertyToID("_CrustRoughness");
        private static MaterialPropertyBlock _CaveSurfacePropertyBlock;
        private static readonly CaveStructureType[] _CliffStructureTypes =
        {
            CaveStructureType.Stalactite,
            CaveStructureType.Column,
            CaveStructureType.Stalagmite
        };
        private static readonly CaveStructureType[] _CanyonStructureTypes =
        {
            CaveStructureType.Boulder,
            CaveStructureType.Arch,
            CaveStructureType.Bridge,
            CaveStructureType.Block,
            CaveStructureType.Wall
        };
        private static readonly CaveStructureType[] _AbyssStructureTypes =
        {
            CaveStructureType.Column,
            CaveStructureType.Arch,
            CaveStructureType.Stalactite,
            CaveStructureType.Stalagmite,
            CaveStructureType.Wall
        };
        private static readonly CaveStructureType[] _GenericStructureTypes =
        {
            CaveStructureType.Stalactite,
            CaveStructureType.Boulder,
            CaveStructureType.Column
        };
        private static readonly CavePreset _CliffPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Cliff);
        private static readonly CavePreset _CanyonPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Canyon);
        private static readonly CavePreset _AbyssPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Abyss);
        private static readonly CavePreset _GenericPresetTemplate = CreateBiomePresetTemplate(CaveBiomePresetKind.Generic);

        /// <summary>Represents an active cave instance in the world.</summary>
        public struct CaveInstance
        {
            public long key;
            public Vector3 position;
            public CavePreset preset;
            public HectonVoxelVolume volume; // Reference to generated volume
            public bool isActive;
        }

        private void Awake()
        {
            if (_CaveSurfacePropertyBlock == null)
                _CaveSurfacePropertyBlock = new MaterialPropertyBlock(); // COLD ALLOC: shared cave-surface block for dressing overlays.

            ResolveReferences();
            UpdateDiagnostics();
        }

        private void OnEnable()
        {
            EnsureLifetimeCancellation();
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

            CancelLifetimeCancellation();
            CancelAllPendingSpawns();
        }

        public void SlowTick()
        {
            RefreshCaveLifecycleState();
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

            RefreshBiomeRuntimeContext(biomeFamily);

            if (!_cachedBiomeRuntimeContext.SupportsCaves)
            {
                // Clean up caves from unsupported biomes
                CleanupUnsupportedCaves();
                return;
            }

            // Generate cave spawn candidates
            List<Vector3> candidates = GenerateCaveCandidates(currentZone);

            // Spawn caves at candidates
            foreach (Vector3 candidate in candidates)
            {
                TryQueueCaveSpawn(candidate, biomeFamily);
            }

            UpdateDiagnostics();
        }

        private static bool EvaluateBiomeCaveSupport(string biomeId)
        {
            return biomeId.Contains("cliff") || biomeId.Contains("canyon") || biomeId.Contains("deep") || biomeId.Contains("abyss");
        }

        private List<Vector3> GenerateCaveCandidates(WorldZoneAnchor zone)
        {
            _candidateBuffer.Clear();

            if (playerTransform == null)
                return _candidateBuffer;

            Vector3 playerPos = playerTransform.position;

            // Generate candidates around player within search radius
            // Use deterministic seeding based on biome and position
            int biomeSeed = _cachedBiomeRuntimeContext.FamilyHash;
            Unity.Mathematics.Random rng = new Unity.Mathematics.Random((uint)(biomeSeed + Mathf.FloorToInt(playerPos.x / 100f) + Mathf.FloorToInt(playerPos.z / 100f)));
            float spawnChance = math.saturate(caveSpawnProbability);

            if (rng.NextFloat() > spawnChance)
                return _candidateBuffer;

            int candidateCount = rng.NextInt(1, maxCavesPerBiome + 1);

            for (int i = 0; i < candidateCount; i++)
            {
                // Random position within radius, biased toward terrain features
                float angle = rng.NextFloat(0f, 2f * Mathf.PI);
                float distance = rng.NextFloat(50f, caveSearchRadius);

                Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
                Vector3 candidatePos = playerPos + offset;

                // Sample terrain height
                if (mapMagicBridge != null && mapMagicBridge.TryGetHeight(candidatePos.x, candidatePos.z, out float terrainHeight))
                {
                    candidatePos.y = terrainHeight - 5f; // Slightly below surface for cave entrance
                }

                // Check spacing from existing caves
                bool tooClose = false;
                Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
                while (caveEnumerator.MoveNext())
                {
                    CaveInstance existing = caveEnumerator.Current.Value;

                    if (Vector3.Distance(existing.position, candidatePos) < minCaveSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    _candidateBuffer.Add(candidatePos);
                }
            }

            return _candidateBuffer;
        }

        private void TryQueueCaveSpawn(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            long caveKey = GenerateCaveKey(position, biomeFamily);

            if (_activeCaveKeys.Contains(caveKey) || _pendingCaveSpawns.ContainsKey(caveKey))
                return;

            if (voxelEngine == null)
            {
                LogMissingVoxelEngine();
                return;
            }

            CavePreset preset = GetCavePresetForBiome(biomeFamily);
            if (preset == null)
                return;

            PendingCaveSpawnState pendingState = CreatePendingSpawnState();
            _pendingCaveSpawns[caveKey] = pendingState;
            _debugPendingCaves = _pendingCaveSpawns.Count;

            uint seed = unchecked((uint)caveKey);
            _ = SpawnCaveAsync(caveKey, position, preset, seed, pendingState);
        }

        private async Awaitable SpawnCaveAsync(
            long caveKey,
            Vector3 position,
            CavePreset preset,
            uint seed,
            PendingCaveSpawnState pendingState)
        {
            GameObject caveVolume = null;
            CancellationToken token = pendingState != null && pendingState.Cancellation != null
                ? pendingState.Cancellation.Token
                : default;

            try
            {
                if (voxelEngine == null)
                    return;

                caveVolume = await voxelEngine.GenerateVolumeAsync(position, seed, preset, token);
                if (caveVolume == null)
                {
                    LogNoGeometry(position);
                    return;
                }

                if (!isActiveAndEnabled ||
                    !_pendingCaveSpawns.TryGetValue(caveKey, out PendingCaveSpawnState currentState) ||
                    !ReferenceEquals(currentState, pendingState))
                {
                    CleanupSpawnedVolume(caveVolume);
                    return;
                }

                        CaveInstance instance = new CaveInstance
                        {
                            key = caveKey,
                            position = position,
                            preset = preset,
                    volume = caveVolume.GetComponent<HectonVoxelVolume>(),
                            isActive = true
                        };

                        if (instance.volume != null)
                        {
                            instance.volume.caveKey = caveKey;
                            instance.volume.generationPosition = position;
                            instance.volume.preset = preset;
                        }

                        _caveInstances[caveKey] = instance;
                        _activeCaveKeys.Add(caveKey);
                SpawnEntranceVisualCues(instance, preset, position, seed);
                ApplyEntranceQualityPass(instance, preset);
                InitializeCaveDressingLayer(instance, preset);
                LogCaveGenerated(position);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (caveVolume != null)
                    CleanupSpawnedVolume(caveVolume);

                LogCaveSpawnFailure(position, exception.Message);
            }
            finally
            {
                CompletePendingSpawn(caveKey, pendingState);
                RefreshCaveLifecycleState();
                UpdateDiagnostics();
            }
        }

        private CavePreset GetCavePresetForBiome(HectonBiomeFamilyProfile biomeFamily)
        {
            RefreshBiomeRuntimeContext(biomeFamily);
            return ResolveBiomePresetTemplate(_cachedBiomeRuntimeContext.PresetKind);
        }

        private static CaveBiomePresetKind ResolveBiomePresetKind(string biomeId)
        {
            if (string.IsNullOrEmpty(biomeId))
                return CaveBiomePresetKind.Generic;

            if (biomeId.Contains("cliff") || biomeId.Contains("escarpment"))
                return CaveBiomePresetKind.Cliff;

            if (biomeId.Contains("canyon") || biomeId.Contains("rift"))
                return CaveBiomePresetKind.Canyon;

            if (biomeId.Contains("deep") || biomeId.Contains("abyss") || biomeId.Contains("hadal"))
                return CaveBiomePresetKind.Abyss;

            return CaveBiomePresetKind.Generic;
        }

        private static CavePreset ResolveBiomePresetTemplate(CaveBiomePresetKind presetKind)
        {
            return presetKind switch
            {
                CaveBiomePresetKind.Cliff => _CliffPresetTemplate,
                CaveBiomePresetKind.Canyon => _CanyonPresetTemplate,
                CaveBiomePresetKind.Abyss => _AbyssPresetTemplate,
                _ => _GenericPresetTemplate
            };
        }

        private static CavePreset CreateBiomePresetTemplate(CaveBiomePresetKind presetKind)
        {
            CavePreset preset = new CavePreset
            {
                gridDimension = 64,
                voxelSize = 1.5f,
                minEntrances = 1,
                maxEntrances = 2,
                tallTunnelChance = 0.15f,
                tunnelWarpAmount = 2f,
                extraConnectionChance = 0.2f,
                enableStructures = true
            };

            switch (presetKind)
            {
                case CaveBiomePresetKind.Cliff:
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
                    preset.spawnContext = SpawnContext.CaveShallow;
                    preset.hazardLevel = 0.2f;
                    preset.moodLevel = 0.4f;
                    preset.maxStructures = 6;
                    preset.structureDensity = 1.2f;
                    preset.allowedStructureTypes = _CliffStructureTypes;
                    break;

                case CaveBiomePresetKind.Canyon:
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
                    preset.spawnContext = SpawnContext.CaveMid;
                    preset.hazardLevel = 0.5f;
                    preset.moodLevel = 0.6f;
                    preset.maxStructures = 8;
                    preset.structureDensity = 1.0f;
                    preset.isRuinLinked = true;
                    preset.allowedStructureTypes = _CanyonStructureTypes;
                    break;

                case CaveBiomePresetKind.Abyss:
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
                    preset.spawnContext = SpawnContext.CaveDeep;
                    preset.hazardLevel = 0.8f;
                    preset.moodLevel = 0.2f;
                    preset.maxStructures = 10;
                    preset.structureDensity = 0.8f;
                    preset.allowedStructureTypes = _AbyssStructureTypes;
                    break;

                default:
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
                    preset.spawnContext = SpawnContext.CaveShallow;
                    preset.hazardLevel = 0.3f;
                    preset.moodLevel = 0.3f;
                    preset.maxStructures = 4;
                    preset.structureDensity = 0.7f;
                    preset.allowedStructureTypes = _GenericStructureTypes;
                    break;
            }

            return preset;
        }

        private long GenerateCaveKey(Vector3 position, HectonBiomeFamilyProfile biomeFamily)
        {
            RefreshBiomeRuntimeContext(biomeFamily);

            // Deterministic key based on position and biome
            int x = Mathf.FloorToInt(position.x / 100f);
            int z = Mathf.FloorToInt(position.z / 100f);
            int biomeHash = _cachedBiomeRuntimeContext.FamilyHash;

            return ((long)x << 32) | ((long)z << 16) | (uint)biomeHash;
        }

        private void CleanupUnsupportedCaves()
        {
            CancelAllPendingSpawns();

            _staleCaveKeyBuffer.Clear();
            Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
            while (caveEnumerator.MoveNext())
                _staleCaveKeyBuffer.Add(caveEnumerator.Current.Key);

            for (int i = 0; i < _staleCaveKeyBuffer.Count; i++)
                RemoveTrackedCave(_staleCaveKeyBuffer[i], despawnOwnedVolume: true);

            UpdateDiagnostics();
        }

        private bool ResolveReferences()
        {
            bool resolved = true;

            if (playerTransform == null)
            {
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
                resolved &= playerTransform != null;
            }

            if (biomeMatrixDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
                resolved &= biomeMatrixDirector != null;
            }

            if (worldZoneDirector == null)
            {
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
                resolved &= worldZoneDirector != null;
            }

            if (voxelEngine == null)
            {
                WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
                resolved &= voxelEngine != null;
            }

            if (mapMagicBridge == null)
            {
                WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref mapMagicBridge);
                resolved &= mapMagicBridge != null;
            }

            return resolved;
        }

        private void UpdateDiagnostics()
        {
            _debugActiveCaves = _activeCaveKeys.Count;
            _debugPendingCaves = _pendingCaveSpawns.Count;
            if (biomeMatrixDirector != null && biomeMatrixDirector.CurrentFamilyProfile != null)
            {
                RefreshBiomeRuntimeContext(biomeMatrixDirector.CurrentFamilyProfile);
                _debugCurrentBiome = _cachedBiomeRuntimeContext.FamilyLabel;
            }
            else
            {
                _debugCurrentBiome = "None";
            }

            _debugCurrentZone = worldZoneDirector != null && worldZoneDirector.CurrentZone != null
                ? worldZoneDirector.CurrentZone.ZoneLabel : "None";
            _debugReady = ResolveReferences();
        }

        // Public API for other systems
        public bool TryGetCaveAt(Vector3 position, out CaveInstance cave)
        {
            cave = default;
            if (biomeMatrixDirector == null || biomeMatrixDirector.CurrentFamilyProfile == null)
                return false;

            long key = GenerateCaveKey(position, biomeMatrixDirector.CurrentFamilyProfile);
            if (!_caveInstances.TryGetValue(key, out cave))
                return false;

            if (IsTrackedVolumeAlive(key, cave.volume))
                return true;

            RemoveTrackedCave(key);
            cave = default;
            return false;
        }

        public IEnumerable<CaveInstance> GetActiveCaves()
        {
            return _caveInstances.Values;
        }

        private void SpawnEntranceVisualCues(CaveInstance instance, CavePreset preset, Vector3 position, uint seed)
        {
            if (instance.volume == null)
                return;

            // Generate cave graph to get entrance positions
            float volumeHalfExtent = preset.VolumeCoverage * 0.5f;
            float terrainHeight = position.y; // Approximate

            CaveGraphGenerator.Generate(
                seed, preset, position, terrainHeight, volumeHalfExtent,
                out var nodes, out var tunnels, out var entrances, out var structures,
                Allocator.Temp);

            try
            {
                Transform markerRoot = instance.volume.GetOrCreateRuntimeRoot("_EntranceMarkers");
                int usedMarkerCount = 0;

                // Spawn visual cues at entrance positions
                for (int i = 0; i < entrances.Length; i++)
                {
                    CaveEntrance entrance = entrances[i];
                    SpawnEntranceMarker(markerRoot, usedMarkerCount, entrance.surfacePosition, entrance.inwardDirection, instance);
                    usedMarkerCount++;
                }

                DisableUnusedChildren(markerRoot, usedMarkerCount);
            }
            finally
            {
                // Dispose temp arrays
                if (nodes.IsCreated) nodes.Dispose();
                if (tunnels.IsCreated) tunnels.Dispose();
                if (entrances.IsCreated) entrances.Dispose();
                if (structures.IsCreated) structures.Dispose();
            }
        }

        private void SpawnEntranceMarker(Transform markerRoot, int markerIndex, Vector3 position, Vector3 inwardDirection, CaveInstance instance)
        {
            if (markerRoot == null)
                return;

            // Spawn a simple visual marker (light or particle system) at entrance
            Transform markerTransform = markerIndex < markerRoot.childCount
                ? markerRoot.GetChild(markerIndex)
                : null;
            GameObject marker = markerTransform != null
                ? markerTransform.gameObject
                : new GameObject($"Marker_{markerIndex}");
            if (markerTransform == null)
            {
                markerTransform = marker.transform;
                markerTransform.SetParent(markerRoot, false);
            }

            marker.name = $"Marker_{markerIndex}";
            markerTransform.position = position + Vector3.up * 0.5f; // Slightly above ground
            markerTransform.rotation = inwardDirection.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(inwardDirection.normalized, Vector3.up)
                : Quaternion.identity;
            if (!marker.activeSelf)
                marker.SetActive(true);

            // Adjust effects based on cave mood and hazard
            float mood = instance.preset.moodLevel;
            float hazard = instance.preset.hazardLevel;

            // Light color based on mood/hazard
            Color lightColor;
            if (hazard > 0.7f)
            {
                lightColor = new Color(0.9f, 0.3f, 0.2f); // Red for danger
            }
            else if (mood > 0.6f)
            {
                lightColor = new Color(0.4f, 0.8f, 0.4f); // Green for life
            }
            else
            {
                lightColor = new Color(0.8f, 0.6f, 0.2f); // Warm for neutral
            }

            // Add a light for visibility
            Light entranceLight = marker.GetComponent<Light>();
            if (entranceLight == null)
                entranceLight = marker.AddComponent<Light>();
            entranceLight.type = LightType.Point;
            entranceLight.color = lightColor;
            entranceLight.intensity = 1f + mood * 2f; // Brighter for active caves
            entranceLight.range = 4f + hazard * 2f; // Wider for dangerous caves

            // Add particle system for atmospheric effect
            ParticleSystem ps = marker.GetComponent<ParticleSystem>();
            if (ps == null)
                ps = marker.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startSize = 0.05f + mood * 0.15f;
            main.startSpeed = 0.2f + mood * 0.8f;
            main.startLifetime = 2f + mood * 2f;
            main.maxParticles = 10 + (int)(mood * 30);

            var emission = ps.emission;
            emission.rateOverTime = 3f + mood * 10f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f + hazard * 0.4f;

            // Particle color based on context
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            if (instance.preset.spawnContext == SpawnContext.CaveDeep)
            {
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0f), new GradientColorKey(Color.clear, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) }
                );
            }
            else
            {
                gradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(lightColor, 0f), new GradientColorKey(Color.clear, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(0.3f, 0f), new GradientAlphaKey(0f, 1f) }
                );
            }
            colorOverLifetime.color = gradient;
        }

        private void ApplyEntranceQualityPass(CaveInstance instance, CavePreset preset)
        {
            // Entrance quality improvements:
            // 1. Mark entrance zone as "safe" for debris placement
            // 2. Add subtle entrance glow aura
            // 3. Ensure entrance seams are clean (no floating geometry)

            if (instance.volume == null) return;

            // Create an entrance quality marker for in-game logic
            Transform entranceQualityRoot = instance.volume.GetOrCreateRuntimeRoot("_EntranceQualityZone");
            if (entranceQualityRoot == null)
                return;

            GameObject entranceQualityGO = entranceQualityRoot.gameObject;
            entranceQualityRoot.localPosition = Vector3.zero;
            entranceQualityRoot.localRotation = Quaternion.identity;
            entranceQualityRoot.localScale = Vector3.one;

            // Add collider as "quality zone" marker
            var sphereCollider = entranceQualityGO.GetComponent<SphereCollider>();
            if (sphereCollider == null)
                sphereCollider = entranceQualityGO.AddComponent<SphereCollider>();
            sphereCollider.radius = preset.entranceRadius * 2f;
            sphereCollider.isTrigger = true;

            // Add light glow aura at entrance for safe zone feel
            Light entranceGlow = entranceQualityGO.GetComponent<Light>();
            if (entranceGlow == null)
                entranceGlow = entranceQualityGO.AddComponent<Light>();
            entranceGlow.type = LightType.Point;
            entranceGlow.color = new Color(0.8f, 0.7f, 0.5f); // warm safety glow
            entranceGlow.intensity = 0.5f;
            entranceGlow.range = preset.entranceRadius * 3f;
            entranceGlow.renderingLayerMask = -1;
        }

        private void InitializeCaveDressingLayer(CaveInstance instance, CavePreset preset)
        {
            // Initialize cheap dressing layer for cave interiors:
            // 1. Get dressing config based on spawn context + hazard
            // 2. Apply shader overlays (mineral crust, wall growth)
            // 3. Place simple sediment shelf meshes
            // 4. Spawn fungi particle systems

            if (instance.volume == null) return;

            // Get dressing config for this cave type
            CaveDressingConfig dressingConfig = CaveDressingConfig.GetConfigForContext(preset.spawnContext);

            // Create dressing layer parent
            Transform dressingRoot = GetOrCreateDressingRoot(instance.volume.transform);

            // Apply mineral crust if enabled
            if (dressingConfig.mineralCrust.enabled)
            {
                ApplyMineralCrustToVolume(instance.volume, dressingConfig.mineralCrust);
            }

            if (dressingConfig.wallGrowth.enabled)
            {
                ApplyWallGrowth(instance, dressingConfig);
            }

            if (dressingConfig.glowingTissue.enabled)
            {
                ApplyGlowingTissue(instance, dressingConfig);
            }

            // Spawn sediment shelves if enabled
            if (dressingConfig.sedimentShelves.enabled)
            {
                SpawnSedimentShelves(dressingRoot.gameObject, instance, dressingConfig);
            }

            if (dressingConfig.serviceRemnants.enabled)
            {
                ApplyServiceRemnants(instance, dressingConfig);
            }

            // Spawn fungi particles if enabled
            if (dressingConfig.deepFungi.enabled)
            {
                SpawnDeepFungiParticles(dressingRoot.gameObject, instance, dressingConfig.deepFungi);
            }
        }

        private void ApplyMineralCrustToVolume(HectonVoxelVolume volume, MineralCrustConfig config)
        {
            // Apply mineral crust as material property block to the cave mesh
            var meshRenderer = volume.GetComponent<MeshRenderer>();
            if (meshRenderer == null) return;

            _CaveSurfacePropertyBlock.Clear();
            meshRenderer.GetPropertyBlock(_CaveSurfacePropertyBlock);

            // Set crust parameters (assuming shader has these properties)
            _CaveSurfacePropertyBlock.SetFloat(_CrustIntensityId, config.intensity * config.scale);
            _CaveSurfacePropertyBlock.SetColor(_CrustColorId, config.tint);
            _CaveSurfacePropertyBlock.SetFloat(_CrustRoughnessId, config.roughnessBoost);

            meshRenderer.SetPropertyBlock(_CaveSurfacePropertyBlock);
        }

        private void ApplyWallGrowth(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            Transform dressingRoot = GetOrCreateDressingRoot(instance.volume.transform);
            CaveWallGrowthRuntimeBuilder.Build(
                dressingRoot,
                instance.volume,
                instance.preset,
                dressingConfig.wallGrowth,
                dressingConfig.globalIntensity);
        }

        private void ApplyGlowingTissue(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            Transform dressingRoot = GetOrCreateDressingRoot(instance.volume.transform);
            CaveGlowingTissueRuntimeBuilder.Build(
                dressingRoot,
                instance.volume,
                instance.preset,
                dressingConfig.glowingTissue,
                dressingConfig.globalIntensity);
        }

        private void ApplyServiceRemnants(CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (instance.volume == null || dressingConfig == null)
                return;

            Transform dressingRoot = GetOrCreateDressingRoot(instance.volume.transform);
            CaveServiceRemnantRuntimeBuilder.Build(
                dressingRoot,
                instance.volume,
                instance.preset,
                dressingConfig.serviceRemnants,
                dressingConfig.globalIntensity);
        }

        private void SpawnSedimentShelves(GameObject parent, CaveInstance instance, CaveDressingConfig dressingConfig)
        {
            if (parent == null || instance.volume == null || dressingConfig == null)
                return;

            CaveSedimentShelfRuntimeBuilder.Build(
                parent.transform,
                instance.volume,
                instance.preset,
                dressingConfig.sedimentShelves,
                dressingConfig.globalIntensity);
        }

        private void SpawnDeepFungiParticles(GameObject parent, CaveInstance instance, DeepFungiConfig config)
        {
            if (parent == null || instance.volume == null || config == null)
                return;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(instance.volume, instance.preset, out Bounds volumeBounds))
                return;

            Transform fungiTransform = parent.transform.Find("_DeepFungi");
            GameObject fungiGO = fungiTransform != null ? fungiTransform.gameObject : new GameObject("_DeepFungi");
            if (fungiTransform == null)
            {
                fungiTransform = fungiGO.transform;
                fungiTransform.SetParent(parent.transform, false);
            }

            float verticalBias = Mathf.Clamp01(config.verticalBias);
            float verticalMin = Mathf.Lerp(volumeBounds.min.y, volumeBounds.center.y, 0.2f);
            float verticalMax = Mathf.Lerp(volumeBounds.center.y, volumeBounds.max.y, 0.85f);
            Vector3 emissionCenter = new Vector3(
                volumeBounds.center.x,
                Mathf.Lerp(verticalMin, verticalMax, verticalBias),
                volumeBounds.center.z);
            Vector3 emissionSize = new Vector3(
                Mathf.Max(2f, volumeBounds.size.x * 0.72f),
                Mathf.Max(1.5f, volumeBounds.size.y * 0.28f),
                Mathf.Max(2f, volumeBounds.size.z * 0.72f));
            float volumeFactor = Mathf.Clamp01((volumeBounds.size.x * volumeBounds.size.y * volumeBounds.size.z) / 6000f);

            fungiTransform.localPosition = emissionCenter;

            ParticleSystem ps = fungiGO.GetComponent<ParticleSystem>();
            if (ps == null)
                ps = fungiGO.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startSize = new ParticleSystem.MinMaxCurve(config.particleSize * 0.5f, config.particleSize * 1.5f);
            main.startLifetime = config.lifetime;
            main.maxParticles = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(18f, 84f, volumeFactor) * Mathf.Clamp01(config.density)),
                8,
                96);

            var emission = ps.emission;
            emission.rateOverTime = config.emissionRate * Mathf.Lerp(0.7f, 1.2f, volumeFactor);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.BoxShell;
            shape.scale = emissionSize;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(config.glowColor, 0f), new GradientColorKey(Color.clear, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }
        }

        private static Transform GetOrCreateDressingRoot(Transform volumeRoot)
        {
            if (volumeRoot == null)
                return null;

            HectonVoxelVolume volume = volumeRoot.GetComponent<HectonVoxelVolume>();
            if (volume != null)
                return volume.GetOrCreateRuntimeRoot("_CaveDressing");

            Transform dressingRoot = volumeRoot.Find("_CaveDressing");
            if (dressingRoot == null)
            {
                GameObject dressingRootObject = new GameObject("_CaveDressing");
                dressingRoot = dressingRootObject.transform;
                dressingRoot.SetParent(volumeRoot, false);
            }

            if (!dressingRoot.gameObject.activeSelf)
                dressingRoot.gameObject.SetActive(true);
            return dressingRoot;
        }

        private static void DisableUnusedChildren(Transform root, int usedChildCount)
        {
            if (root == null)
                return;

            for (int i = usedChildCount; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.gameObject.activeSelf)
                    child.gameObject.SetActive(false);
            }
        }

        private void RefreshCaveLifecycleState()
        {
            _staleCaveKeyBuffer.Clear();
            Dictionary<long, CaveInstance>.Enumerator caveEnumerator = _caveInstances.GetEnumerator();
            while (caveEnumerator.MoveNext())
            {
                KeyValuePair<long, CaveInstance> pair = caveEnumerator.Current;
                CaveInstance instance = pair.Value;
                if (IsTrackedVolumeAlive(pair.Key, instance.volume))
                    continue;

                _staleCaveKeyBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _staleCaveKeyBuffer.Count; i++)
                RemoveTrackedCave(_staleCaveKeyBuffer[i], despawnOwnedVolume: false);
        }

        private bool IsTrackedVolumeAlive(long caveKey, HectonVoxelVolume volume)
        {
            if (volume == null)
                return false;

            GameObject volumeObject = volume.gameObject;
            if (volumeObject == null || !volumeObject.activeInHierarchy)
                return false;

            return volume.caveKey == caveKey;
        }

        private void RemoveTrackedCave(long caveKey)
        {
            RemoveTrackedCave(caveKey, despawnOwnedVolume: false);
        }

        private void RemoveTrackedCave(long caveKey, bool despawnOwnedVolume)
        {
            if (despawnOwnedVolume &&
                _caveInstances.TryGetValue(caveKey, out CaveInstance instance) &&
                IsTrackedVolumeAlive(caveKey, instance.volume))
            {
                CleanupSpawnedVolume(instance.volume.gameObject);
            }

            _caveInstances.Remove(caveKey);
            _activeCaveKeys.Remove(caveKey);
        }

        private void RefreshBiomeRuntimeContext(HectonBiomeFamilyProfile biomeFamily)
        {
            if (biomeFamily == null)
            {
                _cachedBiomeRuntimeContext = default;
                return;
            }

            string familyId = biomeFamily.familyId ?? string.Empty;
            if (ReferenceEquals(_cachedBiomeRuntimeContext.Family, biomeFamily) &&
                string.Equals(_cachedBiomeRuntimeContext.FamilyId, familyId, StringComparison.Ordinal))
            {
                return;
            }

            _cachedBiomeRuntimeContext.Family = biomeFamily;
            _cachedBiomeRuntimeContext.FamilyId = familyId;
            _cachedBiomeRuntimeContext.FamilyLabel = string.IsNullOrEmpty(biomeFamily.familyLabel) ? "None" : biomeFamily.familyLabel;
            _cachedBiomeRuntimeContext.FamilyHash = familyId.GetHashCode();
            _cachedBiomeRuntimeContext.SupportsCaves = EvaluateBiomeCaveSupport(familyId);
            _cachedBiomeRuntimeContext.PresetKind = ResolveBiomePresetKind(familyId);
        }

        private PendingCaveSpawnState CreatePendingSpawnState()
        {
            CancellationTokenSource lifetime = EnsureLifetimeCancellation();
            return new PendingCaveSpawnState
            {
                Cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)
            };
        }

        private CancellationTokenSource EnsureLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                _lifetimeCancellation = new CancellationTokenSource();

            return _lifetimeCancellation;
        }

        private void CancelLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                return;

            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }

        private void CancelAllPendingSpawns()
        {
            _pendingCaveKeyBuffer.Clear();
            Dictionary<long, PendingCaveSpawnState>.Enumerator pendingEnumerator = _pendingCaveSpawns.GetEnumerator();
            while (pendingEnumerator.MoveNext())
                _pendingCaveKeyBuffer.Add(pendingEnumerator.Current.Key);

            for (int i = 0; i < _pendingCaveKeyBuffer.Count; i++)
            {
                long caveKey = _pendingCaveKeyBuffer[i];
                if (!_pendingCaveSpawns.TryGetValue(caveKey, out PendingCaveSpawnState state))
                    continue;

                if (state != null && state.Cancellation != null)
                    state.Cancellation.Cancel();

                state?.Dispose();
                _pendingCaveSpawns.Remove(caveKey);
            }

            _debugPendingCaves = 0;
        }

        private void CompletePendingSpawn(long caveKey, PendingCaveSpawnState pendingState)
        {
            if (_pendingCaveSpawns.TryGetValue(caveKey, out PendingCaveSpawnState currentState) &&
                ReferenceEquals(currentState, pendingState))
            {
                _pendingCaveSpawns.Remove(caveKey);
            }

            pendingState?.Dispose();
        }

        private void CleanupSpawnedVolume(GameObject caveVolume)
        {
            if (caveVolume == null)
                return;

            if (voxelEngine != null)
            {
                voxelEngine.DespawnVolume(caveVolume);
                return;
            }

            Destroy(caveVolume);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCaveGenerated(Vector3 position)
        {
            Debug.Log($"[WorldCaveDirector] Successfully generated cave at {position}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogNoGeometry(Vector3 position)
        {
            Debug.LogWarning($"[WorldCaveDirector] Cave generation produced no geometry at {position}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogCaveSpawnFailure(Vector3 position, string message)
        {
            Debug.LogError($"[WorldCaveDirector] Failed to generate cave at {position}: {message}");
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingVoxelEngine()
        {
            Debug.LogWarning("[WorldCaveDirector] No voxel engine available for cave generation");
        }
    }
}
