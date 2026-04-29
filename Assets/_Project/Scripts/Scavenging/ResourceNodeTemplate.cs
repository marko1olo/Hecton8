using System;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Items;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Scavenging
{
    /// <summary>
    /// Authoring template for harvestable resource-node families.
    /// Owns environmental envelopes, runtime yield tables, ghost-box dimensions, and optional presentation assets.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ResourceNodeTemplate",
        menuName = "Hecton8/Scavenging/Resource Node Template",
        order = 112)]
    public sealed class ResourceNodeTemplate : ScriptableObject
    {
        public enum HarvestToolClass : byte
        {
            Any = 0,
            Knife = 1,
            Drill = 2,
            Laser = 3,
            Salvage = 4
        }

        public enum ColliderShape : byte
        {
            Box = 0,
            Sphere = 1
        }

        [Serializable]
        public struct YieldAuthoringEntry
        {
            [Tooltip("Authored item asset granted by this harvest table entry.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Minimum stack count granted when the entry resolves.")]
            public ushort minimumAmount;

            [Min(1)]
            [Tooltip("Maximum stack count granted when the entry resolves.")]
            public ushort maximumAmount;

            [Range(1, 255)]
            [Tooltip("Relative weight consumed by the runtime weighted picker.")]
            public byte weight;
        }

        [Serializable]
        public struct RarityDropAuthoringEntry
        {
            [Range(0, 15)]
            [Tooltip("Rarity tier emitted into the runtime descriptor lane.")]
            public byte rarityTier;

            [Range(0f, 1f)]
            [Tooltip("Normalized probability for the rarity pass before weighted entry resolution.")]
            public float probability;

            [Tooltip("Hash-stable item granted by the rarity pass.")]
            public ItemData item;

            [Min(1)]
            [Tooltip("Quantity granted when the rarity drop resolves.")]
            public ushort amount;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct YieldRuntimeEntry
        {
            public int ItemHashId;
            public ushort MinimumAmount;
            public ushort MaximumAmount;
            public byte Weight;
            public byte Reserved0;
            public ushort Reserved1;
            public uint Reserved2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 16)]
        public struct RarityDropRuntimeEntry
        {
            public int ItemHashId;
            public ushort Amount;
            public byte RarityTier;
            public byte ProbabilityByte;
            public uint Reserved0;
            public uint Reserved1;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4, Size = 48)]
        public struct RuntimeDescriptor
        {
            public int StableHashId;
            public float ToolResistance;
            public float HarvestDurationSeconds;
            public int ValidLayerMask;
            public byte RequiredToolClass;
            public byte YieldCount;
            public byte RarityDropCount;
            public byte DefaultLootCount;
            public float MinimumDensity;
            public float MaximumDensity;
            public float MinimumDepthMeters;
            public float MaximumDepthMeters;
            public float MinimumTemperatureCelsius;
            public float MaximumTemperatureCelsius;
            public float MinimumSlopeDegrees;
            public float MaximumSlopeDegrees;
        }

        [Header("Identity")]
        [SerializeField]
        [Tooltip("Stable string identifier used by persistence and procedural placement systems.")]
        private string stableId = "resource.node.generic";

        [SerializeField]
        [Tooltip("Artist-facing label shown in authoring inspectors.")]
        private string displayName = "Generic Resource Node";

        [Header("Harvest")]
        [SerializeField, Min(0.01f)]
        [Tooltip("Scalar resistance applied to tool damage validation before the node yields.")]
        private float toolResistance = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Base interaction duration for harvesting this node family.")]
        private float harvestDurationSeconds = 1.25f;

        [SerializeField]
        [Tooltip("Tool family required to extract this node. Logic tier consumes the byte enum only.")]
        private HarvestToolClass requiredToolClass = HarvestToolClass.Any;

        [SerializeField, Min(1f)]
        [Tooltip("Node integrity applied to runtime ResourceNode instances built from this template.")]
        private float maxIntegrity = 100f;

        [SerializeField, Min(0)]
        [Tooltip("Default number of pickup pieces emitted when the runtime node resolves into pooled loot.")]
        private int defaultLootCount = 1;

        [SerializeField]
        [Tooltip("Optional pooled pickup prefab emitted by legacy ResourceNode loot spawning. Leave empty to use yield metadata only.")]
        private GameObject lootPickupPrefab;

        [Header("Placement Envelope")]
        [SerializeField, Min(0f)]
        [Tooltip("Minimum water depth in meters where this template is eligible.")]
        private float minimumDepthMeters = 0f;

        [SerializeField, Min(0f)]
        [Tooltip("Maximum water depth in meters where this template is eligible.")]
        private float maximumDepthMeters = 1200f;

        [SerializeField]
        [Tooltip("Minimum ambient water temperature in Celsius where this template is eligible.")]
        private float minimumTemperatureCelsius = 0f;

        [SerializeField]
        [Tooltip("Maximum ambient water temperature in Celsius where this template is eligible.")]
        private float maximumTemperatureCelsius = 30f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Minimum terrain slope angle in degrees where this template is eligible.")]
        private float minimumSlopeDegrees = 0f;

        [SerializeField, Range(0f, 90f)]
        [Tooltip("Maximum terrain slope angle in degrees where this template is eligible.")]
        private float maximumSlopeDegrees = 60f;

        [SerializeField, Min(0f)]
        [Tooltip("Vertical offset above the sampled seabed used when placing the node root.")]
        private float spawnOffsetMeters = 0.15f;

        [SerializeField, Range(1, 16)]
        [Tooltip("How many deterministic candidate tests this template receives per sector.")]
        private byte candidateBudgetPerSector = 4;

        [SerializeField, Range(1, 8)]
        [Tooltip("Hard cap for live instances of this template spawned per runtime sector.")]
        private byte maxInstancesPerSector = 2;

        [SerializeField, Range(0.01f, 1f)]
        [Tooltip("Final probability gate after the environmental envelope passes.")]
        private float placementProbability = 1f;

        [Header("Scatter")]
        [SerializeField, Range(0f, 64f)]
        [Tooltip("Lower density bound exported to scatter/runtime placement lanes.")]
        private float minimumDensity = 0.5f;

        [SerializeField, Range(0f, 64f)]
        [Tooltip("Upper density bound exported to scatter/runtime placement lanes.")]
        private float maximumDensity = 2f;

        [SerializeField]
        [Tooltip("Terrain or biome layers allowed to host this node family.")]
        private LayerMask validLayers = ~0;

        [Header("Presentation")]
        [SerializeField]
        [Tooltip("Optional authored mesh used by the runtime node root. If empty, the spawner applies the ghost-box standard.")]
        private Mesh nodeMesh;

        [SerializeField]
        [Tooltip("Optional authored shared material paired with the authored mesh.")]
        private Material nodeMaterial;

        [SerializeField]
        [Tooltip("Primitive collider family used by runtime nodes. MeshCollider is forbidden.")]
        private ColliderShape colliderShape = ColliderShape.Box;

        [SerializeField]
        [Tooltip("Exact physical dimensions in meters for the runtime node root and ghost placeholder.")]
        private Vector3 physicalSize = new Vector3(1f, 1f, 1f);

        [Header("Yield")]
        [SerializeField]
        [Tooltip("Primary weighted harvest table. Resolved to hash-based rows at runtime.")]
        private YieldAuthoringEntry[] harvestYield;

        [SerializeField]
        [Tooltip("Optional rarity table evaluated before the primary weighted harvest table.")]
        private RarityDropAuthoringEntry[] rarityDrops;

        /// <summary>Stable authored identifier used by persistence-facing systems.</summary>
        public string StableId => stableId;

        /// <summary>Artist-facing label for authoring tools.</summary>
        public string DisplayName => displayName;

        /// <summary>Stable runtime hash used by scanner, save, and placement tables.</summary>
        public int StableHashId => string.IsNullOrWhiteSpace(stableId) ? 0 : LocHash.Compute(stableId);

        /// <summary>Legacy loot prefab used by pooled pickup emission.</summary>
        public GameObject LootPickupPrefab => lootPickupPrefab;

        /// <summary>Maximum node integrity seeded into pooled runtime nodes.</summary>
        public float MaxIntegrity => math.max(1f, maxIntegrity);

        /// <summary>Default pooled pickup count emitted by runtime nodes.</summary>
        public int DefaultLootCount => math.max(0, defaultLootCount);

        /// <summary>Environmental minimum depth in meters.</summary>
        public float MinimumDepthMeters => math.max(0f, minimumDepthMeters);

        /// <summary>Environmental maximum depth in meters.</summary>
        public float MaximumDepthMeters => math.max(MinimumDepthMeters, maximumDepthMeters);

        /// <summary>Environmental minimum water temperature in Celsius.</summary>
        public float MinimumTemperatureCelsius => math.min(minimumTemperatureCelsius, maximumTemperatureCelsius);

        /// <summary>Environmental maximum water temperature in Celsius.</summary>
        public float MaximumTemperatureCelsius => math.max(minimumTemperatureCelsius, maximumTemperatureCelsius);

        /// <summary>Environmental minimum slope angle in degrees.</summary>
        public float MinimumSlopeDegrees => math.clamp(minimumSlopeDegrees, 0f, 90f);

        /// <summary>Environmental maximum slope angle in degrees.</summary>
        public float MaximumSlopeDegrees => math.clamp(math.max(minimumSlopeDegrees, maximumSlopeDegrees), 0f, 90f);

        /// <summary>Vertical offset above the sampled seabed.</summary>
        public float SpawnOffsetMeters => math.max(0f, spawnOffsetMeters);

        /// <summary>Deterministic candidate count evaluated per runtime sector.</summary>
        public int CandidateBudgetPerSector => math.max(1, candidateBudgetPerSector);

        /// <summary>Hard cap for live instances of this template inside one runtime sector.</summary>
        public int MaxInstancesPerSector => math.max(1, maxInstancesPerSector);

        /// <summary>Final probability gate after the environmental envelope passes.</summary>
        public float PlacementProbability => math.saturate(placementProbability);

        /// <summary>Optional authored mesh for the runtime node root.</summary>
        public Mesh NodeMesh => nodeMesh;

        /// <summary>Optional authored shared material paired with the node mesh.</summary>
        public Material NodeMaterial => nodeMaterial;

        /// <summary>Primitive collider family used by runtime nodes.</summary>
        public ColliderShape RuntimeColliderShape => colliderShape;

        /// <summary>Exact physical extents in meters used by runtime nodes and ghost placeholders.</summary>
        public Vector3 PhysicalSize => new Vector3(
            math.max(0.1f, physicalSize.x),
            math.max(0.1f, physicalSize.y),
            math.max(0.1f, physicalSize.z));

        /// <summary>Half extents in meters used by the spatial broadphase.</summary>
        public float3 HalfExtents => (float3)(PhysicalSize * 0.5f);

        /// <summary>Returns true when the template carries an authored presentation mesh.</summary>
        public bool HasPresentationMesh => nodeMesh != null;

        /// <summary>Builds the blittable runtime descriptor consumed by scatter/harvest SOA lanes.</summary>
        public RuntimeDescriptor BuildRuntimeDescriptor()
        {
            return new RuntimeDescriptor
            {
                StableHashId = StableHashId,
                ToolResistance = math.max(0.01f, toolResistance),
                HarvestDurationSeconds = math.max(0f, harvestDurationSeconds),
                ValidLayerMask = validLayers.value,
                RequiredToolClass = (byte)requiredToolClass,
                YieldCount = (byte)math.min(byte.MaxValue, harvestYield != null ? harvestYield.Length : 0),
                RarityDropCount = (byte)math.min(byte.MaxValue, rarityDrops != null ? rarityDrops.Length : 0),
                DefaultLootCount = (byte)math.min(byte.MaxValue, math.max(0, defaultLootCount)),
                MinimumDensity = math.max(0f, minimumDensity),
                MaximumDensity = math.max(math.max(0f, minimumDensity), maximumDensity),
                MinimumDepthMeters = MinimumDepthMeters,
                MaximumDepthMeters = MaximumDepthMeters,
                MinimumTemperatureCelsius = MinimumTemperatureCelsius,
                MaximumTemperatureCelsius = MaximumTemperatureCelsius,
                MinimumSlopeDegrees = MinimumSlopeDegrees,
                MaximumSlopeDegrees = MaximumSlopeDegrees
            };
        }

        /// <summary>
        /// Returns true when the supplied environmental sample passes this template envelope.
        /// </summary>
        public bool MatchesEnvelope(float depthMeters, float temperatureCelsius, float slopeDegrees)
        {
            if (depthMeters < MinimumDepthMeters || depthMeters > MaximumDepthMeters)
                return false;

            if (temperatureCelsius < MinimumTemperatureCelsius || temperatureCelsius > MaximumTemperatureCelsius)
                return false;

            return slopeDegrees >= MinimumSlopeDegrees && slopeDegrees <= MaximumSlopeDegrees;
        }

        /// <summary>
        /// Copies the authored primary yield table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyYieldTableNonAlloc(NativeList<YieldRuntimeEntry> destination)
        {
            if (!destination.IsCreated || harvestYield == null)
                return 0;

            int copiedCount = 0;
            int maxEntries = math.min(harvestYield.Length, destination.Capacity - destination.Length);
            for (int i = 0; i < maxEntries; i++)
            {
                YieldAuthoringEntry source = harvestYield[i];
                if (source.item == null || string.IsNullOrWhiteSpace(source.item.PersistentId))
                    continue;

                YieldRuntimeEntry runtimeEntry = new YieldRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    MinimumAmount = (ushort)math.max(1, (int)source.minimumAmount),
                    MaximumAmount = (ushort)math.max(math.max(1, (int)source.minimumAmount), (int)source.maximumAmount),
                    Weight = (byte)math.max(1, (int)source.weight),
                    Reserved0 = 0,
                    Reserved1 = 0,
                    Reserved2 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

        /// <summary>
        /// Copies the authored rarity table into caller-owned scratch storage without allocating runtime arrays.
        /// </summary>
        public int CopyRarityTableNonAlloc(NativeList<RarityDropRuntimeEntry> destination)
        {
            if (!destination.IsCreated || rarityDrops == null)
                return 0;

            int copiedCount = 0;
            int maxEntries = math.min(rarityDrops.Length, destination.Capacity - destination.Length);
            for (int i = 0; i < maxEntries; i++)
            {
                RarityDropAuthoringEntry source = rarityDrops[i];
                if (source.item == null || string.IsNullOrWhiteSpace(source.item.PersistentId))
                    continue;

                RarityDropRuntimeEntry runtimeEntry = new RarityDropRuntimeEntry
                {
                    ItemHashId = LocHash.Compute(source.item.PersistentId),
                    Amount = (ushort)math.max(1, (int)source.amount),
                    RarityTier = (byte)math.clamp((int)source.rarityTier, 0, 15),
                    ProbabilityByte = (byte)math.clamp(math.round(source.probability * 255f), 0f, 255f),
                    Reserved0 = 0u,
                    Reserved1 = 0u
                };

                destination.AddNoResize(runtimeEntry);
                copiedCount++;
            }

            return copiedCount;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(stableId) && !string.IsNullOrWhiteSpace(name))
                stableId = name;

            minimumDensity = math.max(0f, minimumDensity);
            maximumDensity = math.max(minimumDensity, maximumDensity);
            toolResistance = math.max(0.01f, toolResistance);
            harvestDurationSeconds = math.max(0f, harvestDurationSeconds);
            maxIntegrity = math.max(1f, maxIntegrity);
            defaultLootCount = math.max(0, defaultLootCount);
            minimumDepthMeters = math.max(0f, minimumDepthMeters);
            maximumDepthMeters = math.max(minimumDepthMeters, maximumDepthMeters);
            minimumSlopeDegrees = math.clamp(minimumSlopeDegrees, 0f, 90f);
            maximumSlopeDegrees = math.clamp(math.max(minimumSlopeDegrees, maximumSlopeDegrees), 0f, 90f);
            spawnOffsetMeters = math.max(0f, spawnOffsetMeters);
            placementProbability = math.saturate(placementProbability);
            physicalSize.x = math.max(0.1f, physicalSize.x);
            physicalSize.y = math.max(0.1f, physicalSize.y);
            physicalSize.z = math.max(0.1f, physicalSize.z);
        }
#endif
    }
}
