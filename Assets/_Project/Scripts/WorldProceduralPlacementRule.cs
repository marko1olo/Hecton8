using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldProceduralPlacementRule", menuName = "Hecton8/World/Procedural Placement Rule")]
    public sealed class WorldProceduralPlacementRule : ScriptableObject
    {
        [System.Flags]
        public enum FloraSubstrateMask : byte
        {
            None = 0,
            Sand = 1 << 0,
            Rock = 1 << 1,
            Reef = 1 << 2,
            Brine = 1 << 3,
            Seep = 1 << 4,
            Nodule = 1 << 5,
            Rubble = 1 << 6,
            AnyGeology = Sand | Rock | Reef | Brine | Seep | Nodule | Rubble,
            Any = Sand | Rock
        }

        [Header("Identity")]
        public string ruleId = "procedural.rule.generic";
        public string ruleLabel = "Generic Procedural Rule";

        [Header("Target Family")]
        public WorldPrefabFamilyProfile familyProfile;
        [TextArea(2, 4)] public string gameplayIntent = "Generic procedural fill rule.";

        [Header("Biome & Zone Filters")]
        public HectonBiomeFamilyProfile[] preferredBiomeFamilies = new HectonBiomeFamilyProfile[0];
        public WorldZoneAnchor.ZoneKind[] preferredZoneKinds = new WorldZoneAnchor.ZoneKind[0];
        public WorldContentSocket.ContentKind[] preferredSocketKinds = new WorldContentSocket.ContentKind[0];
        public WorldSliceAnchor.SliceState preferredFidelity = WorldSliceAnchor.SliceState.Mid;

        [Header("Spatial Filters")]
        [Min(0f)] public float minDepthMeters;
        [Min(0f)] public float maxDepthMeters = 5000f;
        [Range(0f, 90f)] public float minSlopeDegrees;
        [Range(0f, 90f)] public float maxSlopeDegrees = 45f;
        public FloraSubstrateMask requiredSubstrate = FloraSubstrateMask.Any;
        [Range(0f, 85f)] public float maxTiltAngleDegrees = 28f;
        public string requiredHeatmapChannel = string.Empty;
        [Range(0f, 1f)] public float minHeatmapValue = 0.35f;

        [Header("Scatter Weights")]
        [Range(0.1f, 4f)] public float densityScale = 1f;
        [Min(0f)] public float minSpacingOverrideMeters;
        [Min(0f)] public float clusterRadiusOverrideMeters;
        [Min(0.001f)] public float clusterNoiseScale = 0.009f;
        [Range(0f, 1f)] public float clusterNoiseThreshold = 0.3f;
        [Min(0)] public int minInstances = 1;
        [Min(0)] public int maxInstances = 3;

        [Header("Flags")]
        public bool preferNearCaves;
        public bool preferOpenWater;
        public bool preferSeafloor;
        public bool suppressInTightRoutes;
        public bool runtimeOnly;
        public bool strictEnvelopeMapping = true;

        public bool Matches(HectonBiomeFamilyProfile biomeFamily, WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (familyProfile == null)
                return false;

            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0)
            {
                bool biomeMatched = false;
                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == null || preferredBiomeFamilies[i] != biomeFamily)
                        continue;

                    biomeMatched = true;
                    break;
                }

                if (!biomeMatched)
                    return false;
            }

            if (preferredZoneKinds != null && preferredZoneKinds.Length > 0)
            {
                if (zone == null)
                    return false;

                bool zoneMatched = false;
                for (int i = 0; i < preferredZoneKinds.Length; i++)
                {
                    if (preferredZoneKinds[i] != zone.Kind)
                        continue;

                    zoneMatched = true;
                    break;
                }

                if (!zoneMatched)
                    return false;
            }

            if (preferredSocketKinds != null && preferredSocketKinds.Length > 0)
            {
                if (socket == null)
                    return false;

                bool socketMatched = false;
                for (int i = 0; i < preferredSocketKinds.Length; i++)
                {
                    if (preferredSocketKinds[i] != socket.Kind)
                        continue;

                    socketMatched = true;
                    break;
                }

                if (!socketMatched)
                    return false;
            }

            return true;
        }

        public bool MatchesScatter(
            HectonBiomeFamilyProfile biomeFamily,
            WorldZoneAnchor zone,
            WorldZoneAnchor.ZoneKind zoneKindHint,
            WorldContentSocket.ContentKind scatterKind,
            float depthMeters,
            float slopeDegrees)
        {
            if (familyProfile == null)
                return false;

            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0)
            {
                bool biomeMatched = false;
                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == null || preferredBiomeFamilies[i] != biomeFamily)
                        continue;

                    biomeMatched = true;
                    break;
                }

                if (!biomeMatched)
                    return false;
            }

            if (preferredZoneKinds != null && preferredZoneKinds.Length > 0)
            {
                bool zoneMatched = false;
                WorldZoneAnchor.ZoneKind effectiveZoneKind = zone != null ? zone.Kind : zoneKindHint;
                for (int i = 0; i < preferredZoneKinds.Length; i++)
                {
                    if (preferredZoneKinds[i] != effectiveZoneKind)
                        continue;

                    zoneMatched = true;
                    break;
                }

                if (!zoneMatched)
                    return false;
            }

            if (preferredSocketKinds != null && preferredSocketKinds.Length > 0)
            {
                bool kindMatched = false;
                for (int i = 0; i < preferredSocketKinds.Length; i++)
                {
                    if (preferredSocketKinds[i] != scatterKind)
                        continue;

                    kindMatched = true;
                    break;
                }

                if (!kindMatched)
                    return false;
            }

            if (depthMeters < minDepthMeters || depthMeters > maxDepthMeters)
                return false;

            if (slopeDegrees < minSlopeDegrees || slopeDegrees > maxSlopeDegrees)
                return false;

            return true;
        }

        public WorldContentSocket.ContentKind GetScatterContentKind()
        {
            if (preferredSocketKinds != null && preferredSocketKinds.Length > 0)
                return preferredSocketKinds[0];

            return familyProfile != null ? familyProfile.proceduralDomain switch
            {
                WorldPrefabFamilyProfile.ProceduralDomain.CaveEntrance => WorldContentSocket.ContentKind.NavigationMarker,
                WorldPrefabFamilyProfile.ProceduralDomain.Landmark => WorldContentSocket.ContentKind.Landmark,
                WorldPrefabFamilyProfile.ProceduralDomain.CreatureSpawn => WorldContentSocket.ContentKind.CombatPoint,
                WorldPrefabFamilyProfile.ProceduralDomain.ResourcePocket => WorldContentSocket.ContentKind.ResourcePickup,
                WorldPrefabFamilyProfile.ProceduralDomain.HazardPocket => WorldContentSocket.ContentKind.HazardPoint,
                WorldPrefabFamilyProfile.ProceduralDomain.SafePocket => WorldContentSocket.ContentKind.FabricationStation,
                WorldPrefabFamilyProfile.ProceduralDomain.PowerRoute => WorldContentSocket.ContentKind.PowerPoint,
                WorldPrefabFamilyProfile.ProceduralDomain.ServiceScar => WorldContentSocket.ContentKind.ServiceTarget,
                WorldPrefabFamilyProfile.ProceduralDomain.RuinModule => WorldContentSocket.ContentKind.Landmark,
                _ => WorldContentSocket.ContentKind.Generic
            } : WorldContentSocket.ContentKind.Generic;
        }
    }
}
