using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldPopulationRule", menuName = "Hecton8/World/Population Rule")]
    public sealed class WorldPopulationRule : ScriptableObject
    {
        [Header("Identity")]
        public string ruleId = "population.rule.generic";
        public string ruleLabel = "Generic Population Rule";

        [Header("Target")]
        public WorldZoneAnchor.ZoneKind zoneKind = WorldZoneAnchor.ZoneKind.Generic;
        public WorldZoneAnchor.ZoneTier minTier = WorldZoneAnchor.ZoneTier.Starter;
        public WorldZoneAnchor.ZoneTier maxTier = WorldZoneAnchor.ZoneTier.Endgame;
        public WorldContentSocket.ContentKind contentKind = WorldContentSocket.ContentKind.Generic;

        [Header("Future Population")]
        public string prefabFamily = string.Empty;
        public WorldPrefabFamilyProfile familyProfile;
        public string gameplayPurpose = "Generic world population.";
        [TextArea(1, 3)] public string biomeFitSummary = string.Empty;
        public HectonBiomeFamilyProfile[] preferredBiomeFamilies;
        [Range(0.1f, 3f)] public float densityWeight = 1f;
        public int suggestedClusterCount = 1;
        public int suggestedMinCount = 1;
        public int suggestedMaxCount = 1;

        public bool Matches(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (zone == null)
                return false;

            if (zoneKind != WorldZoneAnchor.ZoneKind.Generic && zone.Kind != zoneKind)
                return false;

            if (zone.Tier < minTier || zone.Tier > maxTier)
                return false;

            if (contentKind != WorldContentSocket.ContentKind.Generic)
            {
                if (socket == null || socket.Kind != contentKind)
                    return false;
            }

            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0)
            {
                HectonBiomeFamilyProfile zoneBiomeFamily = zone.DominantBiomeFamily;
                bool biomeMatch = false;

                for (int i = 0; i < preferredBiomeFamilies.Length; i++)
                {
                    if (preferredBiomeFamilies[i] == null || preferredBiomeFamilies[i] != zoneBiomeFamily)
                        continue;

                    biomeMatch = true;
                    break;
                }

                if (!biomeMatch)
                    return false;
            }

            return true;
        }

        public float GetEffectiveDensityWeight(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (!Matches(zone, socket))
                return 0f;

            float weight = Mathf.Max(0.05f, densityWeight);
            HectonBiomeMatrixProfile matrixBiome = zone != null ? zone.DominantMatrixBiome : null;

            if (preferredBiomeFamilies != null && preferredBiomeFamilies.Length > 0 && zone != null && zone.DominantBiomeFamily != null)
                weight *= 1.12f;

            if (matrixBiome == null)
                return weight;

            weight *= GetExtractionBiasMultiplier(matrixBiome, socket);
            weight *= GetRewardBiasMultiplier(matrixBiome, socket);
            weight *= GetNavigationBiasMultiplier(matrixBiome, socket);

            return Mathf.Max(0.05f, weight);
        }

        public float GetBorderBlendMultiplier(
            WorldZoneAnchor primaryZone,
            WorldZoneAnchor secondaryZone,
            WorldContentSocket socket,
            float blendFactor)
        {
            if (primaryZone == null || secondaryZone == null || socket == null || blendFactor <= 0.08f)
                return 1f;

            float strength = Mathf.Lerp(1f, 1.22f, Mathf.Clamp01(blendFactor));
            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => IsRouteTransition(primaryZone.Kind, secondaryZone.Kind) ? strength : 1f,
                WorldContentSocket.ContentKind.FabricationStation => IsReliefTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.18f, Mathf.Clamp01(blendFactor)) : 1f,
                WorldContentSocket.ContentKind.HazardPoint => IsHazardTransition(primaryZone.Kind, secondaryZone.Kind) ? strength : 1f,
                WorldContentSocket.ContentKind.CombatPoint => IsHazardTransition(primaryZone.Kind, secondaryZone.Kind) ? strength : 1f,
                WorldContentSocket.ContentKind.Landmark => IsGoalTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.2f, Mathf.Clamp01(blendFactor)) : 1f,
                WorldContentSocket.ContentKind.ResourcePickup => IsRewardTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.14f, Mathf.Clamp01(blendFactor)) : 1f,
                WorldContentSocket.ContentKind.ResourceNode => IsRewardTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.17f, Mathf.Clamp01(blendFactor)) : 1f,
                WorldContentSocket.ContentKind.ServiceTarget => IsHazardTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.15f, Mathf.Clamp01(blendFactor)) : 1f,
                WorldContentSocket.ContentKind.PowerPoint => IsPowerTransition(primaryZone.Kind, secondaryZone.Kind) ? Mathf.Lerp(1f, 1.14f, Mathf.Clamp01(blendFactor)) : 1f,
                _ => 1f
            };
        }

        public string BuildResolvedPurpose(WorldZoneAnchor zone)
        {
            if (zone == null)
                return gameplayPurpose;

            HectonBiomeMatrixProfile matrixBiome = zone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = zone.DominantBiomeFamily;

            if (contentKind == WorldContentSocket.ContentKind.ResourcePickup || contentKind == WorldContentSocket.ContentKind.ResourceNode)
            {
                string farmReason = ResolveFarmReason(zone);
                if (!string.IsNullOrWhiteSpace(farmReason))
                    return farmReason;
            }

            if (contentKind == WorldContentSocket.ContentKind.NavigationMarker || contentKind == WorldContentSocket.ContentKind.Landmark)
            {
                string landmarkReason = matrixBiome != null && !string.IsNullOrWhiteSpace(matrixBiome.landmarkGuidance)
                    ? matrixBiome.landmarkGuidance
                    : biomeFamily != null && biomeFamily.landmarkPlanProfile != null
                        ? biomeFamily.landmarkPlanProfile.routeUse
                        : string.Empty;
                if (!string.IsNullOrWhiteSpace(landmarkReason))
                    return landmarkReason;
            }

            if (contentKind == WorldContentSocket.ContentKind.HazardPoint || contentKind == WorldContentSocket.ContentKind.CombatPoint)
            {
                if (matrixBiome != null && !string.IsNullOrWhiteSpace(matrixBiome.riskSummary))
                    return matrixBiome.riskSummary;
            }

            if (matrixBiome != null && !string.IsNullOrWhiteSpace(matrixBiome.visitPurpose))
                return $"{gameplayPurpose} {matrixBiome.visitPurpose}";

            return gameplayPurpose;
        }

        public string BuildBiomeFitReason(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (zone == null)
                return biomeFitSummary;

            HectonBiomeMatrixProfile matrixBiome = zone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = zone.DominantBiomeFamily;
            string biomeLabel = biomeFamily != null ? biomeFamily.familyLabel : matrixBiome != null ? matrixBiome.biomeName : "Unknown biome";
            string contentLabel = socket != null ? socket.SocketLabel : "socket";

            if (matrixBiome == null)
                return $"{contentLabel}: {biomeFitSummary}";

            string focus = contentKind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => matrixBiome.extractionFocus,
                WorldContentSocket.ContentKind.ResourceNode => matrixBiome.extractionFocus,
                WorldContentSocket.ContentKind.NavigationMarker => matrixBiome.landmarkGuidance,
                WorldContentSocket.ContentKind.Landmark => matrixBiome.landmarkGuidance,
                WorldContentSocket.ContentKind.HazardPoint => matrixBiome.riskSummary,
                WorldContentSocket.ContentKind.CombatPoint => matrixBiome.riskSummary,
                _ => matrixBiome.visitPurpose
            };

            if (string.IsNullOrWhiteSpace(focus))
                focus = biomeFitSummary;

            return $"{biomeLabel}: {focus}";
        }

        public string BuildExtractionFocus(WorldZoneAnchor zone)
        {
            if (zone == null)
                return "None";

            HectonBiomeMatrixProfile matrixBiome = zone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = zone.DominantBiomeFamily;

            if (matrixBiome != null && !string.IsNullOrWhiteSpace(matrixBiome.extractionFocus))
                return matrixBiome.extractionFocus;

            if (biomeFamily != null && biomeFamily.resourcePlanProfile != null)
                return biomeFamily.resourcePlanProfile.extractionStyle;

            return "Mixed field extraction.";
        }

        public string BuildLandmarkGuidance(WorldZoneAnchor zone)
        {
            if (zone == null)
                return "None";

            HectonBiomeMatrixProfile matrixBiome = zone.DominantMatrixBiome;
            HectonBiomeFamilyProfile biomeFamily = zone.DominantBiomeFamily;

            if (matrixBiome != null && !string.IsNullOrWhiteSpace(matrixBiome.landmarkGuidance))
                return matrixBiome.landmarkGuidance;

            if (biomeFamily != null && biomeFamily.landmarkPlanProfile != null)
                return biomeFamily.landmarkPlanProfile.routeUse;

            return "Follow the strongest readable landmark line.";
        }

        public string BuildSpatialRole(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            if (socket == null)
                return "Generic Point";

            bool lateZone = zone != null && zone.Tier >= WorldZoneAnchor.ZoneTier.Late;
            bool highRoutePressure = zone != null && zone.DominantMatrixBiome != null && zone.DominantMatrixBiome.routePressure >= 4;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => "Resource Pocket",
                WorldContentSocket.ContentKind.ResourceNode => "Node Cluster",
                WorldContentSocket.ContentKind.FabricationStation => "Safe Outpost",
                WorldContentSocket.ContentKind.ConstructionPoint => "Build Socket",
                WorldContentSocket.ContentKind.PowerPoint => "Power Spine",
                WorldContentSocket.ContentKind.ServiceTarget => "Service Choke",
                WorldContentSocket.ContentKind.NavigationMarker => highRoutePressure ? "Route Anchor" : "Route Marker",
                WorldContentSocket.ContentKind.HazardPoint => lateZone ? "Rare Objective Gate" : "Hazard Pocket",
                WorldContentSocket.ContentKind.CombatPoint => lateZone ? "Threat Gate" : "Threat Pocket",
                WorldContentSocket.ContentKind.Landmark => lateZone ? "Rare Objective" : "Major Landmark",
                _ => "Generic Point"
            };
        }

        public string BuildSpatialRoleReason(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            HectonBiomeMatrixProfile matrixBiome = zone != null ? zone.DominantMatrixBiome : null;
            HectonBiomeFamilyProfile biomeFamily = zone != null ? zone.DominantBiomeFamily : null;
            HectonBiomeSpatialPatternProfile spatial = biomeFamily != null ? biomeFamily.spatialPatternProfile : null;

            if (socket == null)
                return "No socket role available.";

            string pattern = socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => spatial != null ? spatial.resourcePocketPattern : string.Empty,
                WorldContentSocket.ContentKind.ResourceNode => spatial != null ? spatial.nodeClusterPattern : string.Empty,
                WorldContentSocket.ContentKind.FabricationStation => spatial != null ? spatial.safePocketPattern : string.Empty,
                WorldContentSocket.ContentKind.ConstructionPoint => spatial != null ? spatial.routeAnchorPattern : string.Empty,
                WorldContentSocket.ContentKind.PowerPoint => spatial != null ? spatial.routeAnchorPattern : string.Empty,
                WorldContentSocket.ContentKind.ServiceTarget => spatial != null ? spatial.safePocketPattern : string.Empty,
                WorldContentSocket.ContentKind.NavigationMarker => spatial != null ? spatial.routeAnchorPattern : string.Empty,
                WorldContentSocket.ContentKind.HazardPoint => spatial != null ? spatial.rareObjectivePattern : string.Empty,
                WorldContentSocket.ContentKind.CombatPoint => spatial != null ? spatial.rareObjectivePattern : string.Empty,
                WorldContentSocket.ContentKind.Landmark => spatial != null ? spatial.playerMemoryHook : string.Empty,
                _ => string.Empty
            };

            if (!string.IsNullOrWhiteSpace(pattern))
                return pattern;

            if (matrixBiome != null && socket.Kind == WorldContentSocket.ContentKind.NavigationMarker && !string.IsNullOrWhiteSpace(matrixBiome.landmarkGuidance))
                return matrixBiome.landmarkGuidance;

            if (matrixBiome != null && (socket.Kind == WorldContentSocket.ContentKind.HazardPoint || socket.Kind == WorldContentSocket.ContentKind.CombatPoint) && !string.IsNullOrWhiteSpace(matrixBiome.riskSummary))
                return matrixBiome.riskSummary;

            if (matrixBiome != null && (socket.Kind == WorldContentSocket.ContentKind.ResourcePickup || socket.Kind == WorldContentSocket.ContentKind.ResourceNode) && !string.IsNullOrWhiteSpace(matrixBiome.extractionFocus))
                return matrixBiome.extractionFocus;

            return "Socket follows the biome's default spatial rhythm.";
        }

        public string BuildZoneRoleFamily(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldZonePlanProfile.RolePlan rolePlan = ResolveRolePlan(zone, socket);
            return rolePlan != null && rolePlan.family != null
                ? rolePlan.family.familyLabel
                : "None";
        }

        public string BuildZoneRoleLayout(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldZonePlanProfile.RolePlan rolePlan = ResolveRolePlan(zone, socket);
            if (rolePlan == null)
                return "No role plan.";

            string family = rolePlan.family != null ? rolePlan.family.familyLabel : "None";
            return $"{rolePlan.relation} / {rolePlan.preferredSlice} / count {rolePlan.targetCount} / {family}";
        }

        public string BuildZoneRolePriority(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldZonePlanProfile.RolePlan rolePlan = ResolveRolePlan(zone, socket);
            if (rolePlan == null)
                return "Unplanned";

            bool lateZone = zone != null && zone.Tier >= WorldZoneAnchor.ZoneTier.Late;
            return socket != null ? socket.Kind switch
            {
                WorldContentSocket.ContentKind.FabricationStation => "Primary Hub",
                WorldContentSocket.ContentKind.Landmark => lateZone ? "Primary Goal" : "Primary Landmark",
                WorldContentSocket.ContentKind.NavigationMarker => "Primary Route",
                WorldContentSocket.ContentKind.HazardPoint => lateZone ? "Gate" : "Warning",
                WorldContentSocket.ContentKind.CombatPoint => lateZone ? "Gate" : "Pressure",
                WorldContentSocket.ContentKind.PowerPoint => rolePlan.targetCount >= 2 ? "Backbone" : "Support",
                WorldContentSocket.ContentKind.ServiceTarget => "Support Problem",
                WorldContentSocket.ContentKind.ConstructionPoint => "Build Route",
                WorldContentSocket.ContentKind.ResourceNode => rolePlan.targetCount >= 2 ? "Secondary Reward" : "Reward",
                WorldContentSocket.ContentKind.ResourcePickup => "Support Reward",
                _ => rolePlan.targetCount > 0 ? "Support" : "Optional"
            } : "Unplanned";
        }

        public string BuildBorderBlendRole(WorldZoneAnchor primaryZone, WorldZoneAnchor secondaryZone, WorldContentSocket socket, float blendFactor)
        {
            if (primaryZone == null || secondaryZone == null || socket == null || blendFactor <= 0.12f)
                return "Pure Zone Point";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => "Transition Route Anchor",
                WorldContentSocket.ContentKind.FabricationStation => "Transition Safe Pocket",
                WorldContentSocket.ContentKind.HazardPoint => "Transition Hazard Gate",
                WorldContentSocket.ContentKind.CombatPoint => "Transition Threat Gate",
                WorldContentSocket.ContentKind.Landmark => "Transition Rare Objective",
                WorldContentSocket.ContentKind.ResourcePickup => "Transition Reward Pocket",
                WorldContentSocket.ContentKind.ResourceNode => "Transition Node Cluster",
                WorldContentSocket.ContentKind.ServiceTarget => "Transition Pressure Point",
                WorldContentSocket.ContentKind.PowerPoint => "Transition Power Spine",
                WorldContentSocket.ContentKind.ConstructionPoint => "Transition Build Route",
                _ => "Transition Point"
            };
        }

        public string BuildBorderBlendReason(WorldZoneAnchor primaryZone, WorldZoneAnchor secondaryZone, WorldContentSocket socket, float blendFactor)
        {
            if (primaryZone == null || secondaryZone == null || socket == null || blendFactor <= 0.12f)
                return "Point is still read mostly from one zone.";

            string pair = $"{primaryZone.Kind} <-> {secondaryZone.Kind}";
            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => $"{pair}: маршрут должен удерживать память на стыке двух типов воды.",
                WorldContentSocket.ContentKind.FabricationStation => $"{pair}: точка отдыха нужна там, где спокойный контур начинает уступать давлению.",
                WorldContentSocket.ContentKind.HazardPoint => $"{pair}: опасность должна читаться как явная граница перехода.",
                WorldContentSocket.ContentKind.CombatPoint => $"{pair}: угроза подхватывает игрока именно на переломе маршрута.",
                WorldContentSocket.ContentKind.Landmark => $"{pair}: редкая цель сильнее запоминается на стыке двух идентичностей места.",
                WorldContentSocket.ContentKind.ResourcePickup => $"{pair}: мелкая награда помогает понять, что ты уже входишь в соседний контур.",
                WorldContentSocket.ContentKind.ResourceNode => $"{pair}: плотная награда оправдывает заход чуть глубже за привычный маршрут.",
                WorldContentSocket.ContentKind.ServiceTarget => $"{pair}: сервисная проблема лучше работает как узкое место перехода.",
                WorldContentSocket.ContentKind.PowerPoint => $"{pair}: силовая линия естественно связывает два соседних контура.",
                WorldContentSocket.ContentKind.ConstructionPoint => $"{pair}: стройка лучше читается как связка между двумя режимами пространства.",
                _ => $"{pair}: точка помогает почувствовать переход, а не резкий обрыв зоны."
            };
        }

        public string ResolveFarmReason(WorldZoneAnchor zone)
        {
            if (zone == null || zone.DominantBiomeFamily == null || zone.DominantBiomeFamily.resourcePlanProfile == null)
                return string.Empty;

            HectonBiomeResourcePlanProfile resourcePlan = zone.DominantBiomeFamily.resourcePlanProfile;
            bool late = zone.Tier >= WorldZoneAnchor.ZoneTier.Late;
            return late ? resourcePlan.lateReasonToReturn : resourcePlan.earlyReasonToFarm;
        }

        private float GetExtractionBiasMultiplier(HectonBiomeMatrixProfile matrixBiome, WorldContentSocket socket)
        {
            if (matrixBiome == null)
                return 1f;

            if (socket == null)
                return 1f;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => BiasToMultiplier(matrixBiome.loosePickupBias),
                WorldContentSocket.ContentKind.ResourceNode => BiasToMultiplier(matrixBiome.nodeExtractionBias),
                WorldContentSocket.ContentKind.HazardPoint => BiasToMultiplier(matrixBiome.salvageBias),
                _ => 1f
            };
        }

        private float GetRewardBiasMultiplier(HectonBiomeMatrixProfile matrixBiome, WorldContentSocket socket)
        {
            if (matrixBiome == null || socket == null)
                return 1f;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => BiasToMultiplier(matrixBiome.commonResourceBias),
                WorldContentSocket.ContentKind.ResourceNode => BiasToMultiplier(Mathf.Max(matrixBiome.uncommonResourceBias, matrixBiome.rareResourceBias)),
                WorldContentSocket.ContentKind.ServiceTarget => BiasToMultiplier(matrixBiome.uncommonResourceBias),
                WorldContentSocket.ContentKind.PowerPoint => BiasToMultiplier(matrixBiome.uncommonResourceBias),
                WorldContentSocket.ContentKind.Landmark => BiasToMultiplier(matrixBiome.rareResourceBias),
                _ => 1f
            };
        }

        private float GetNavigationBiasMultiplier(HectonBiomeMatrixProfile matrixBiome, WorldContentSocket socket)
        {
            if (matrixBiome == null || socket == null)
                return 1f;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => BiasToMultiplier(Mathf.Max(matrixBiome.landmarkStrength, 6 - matrixBiome.routePressure)),
                WorldContentSocket.ContentKind.Landmark => BiasToMultiplier(matrixBiome.landmarkStrength),
                WorldContentSocket.ContentKind.HazardPoint => BiasToMultiplier(matrixBiome.routePressure),
                WorldContentSocket.ContentKind.CombatPoint => BiasToMultiplier(Mathf.Max(matrixBiome.survivalPressure, matrixBiome.routePressure)),
                _ => 1f
            };
        }

        private static float BiasToMultiplier(int bias)
        {
            float t = Mathf.InverseLerp(1f, 5f, bias);
            return Mathf.Lerp(0.72f, 1.35f, t);
        }

        private static bool IsRouteTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Resources)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Progression)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Combat);
        }

        private static bool IsReliefTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Service)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Navigation, WorldZoneAnchor.ZoneKind.Service)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Service);
        }

        private static bool IsHazardTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Service)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Power);
        }

        private static bool IsGoalTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Navigation)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Combat)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Progression, WorldZoneAnchor.ZoneKind.Resources);
        }

        private static bool IsRewardTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Navigation)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Power)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Resources, WorldZoneAnchor.ZoneKind.Service);
        }

        private static bool IsPowerTransition(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b)
        {
            return IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Service)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Progression)
                || IsEitherPair(a, b, WorldZoneAnchor.ZoneKind.Power, WorldZoneAnchor.ZoneKind.Navigation);
        }

        private static bool IsEitherPair(WorldZoneAnchor.ZoneKind a, WorldZoneAnchor.ZoneKind b, WorldZoneAnchor.ZoneKind x, WorldZoneAnchor.ZoneKind y)
        {
            return (a == x && b == y) || (a == y && b == x);
        }

        private static WorldZonePlanProfile.RolePlan ResolveRolePlan(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldZonePlanProfile plan = zone != null && zone.Profile != null ? zone.Profile.zonePlanProfile : null;
            if (plan == null || socket == null)
                return null;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => plan.resourcePocketPlan,
                WorldContentSocket.ContentKind.ResourceNode => plan.nodeClusterPlan,
                WorldContentSocket.ContentKind.FabricationStation => plan.safePocketPlan,
                WorldContentSocket.ContentKind.ConstructionPoint => plan.buildSocketPlan,
                WorldContentSocket.ContentKind.PowerPoint => plan.powerSpinePlan,
                WorldContentSocket.ContentKind.ServiceTarget => plan.serviceChokePlan,
                WorldContentSocket.ContentKind.NavigationMarker => plan.routeAnchorPlan,
                WorldContentSocket.ContentKind.HazardPoint => plan.hazardGatePlan,
                WorldContentSocket.ContentKind.CombatPoint => plan.hazardGatePlan,
                WorldContentSocket.ContentKind.Landmark => plan.rareObjectivePlan,
                _ => null
            };
        }
    }
}
