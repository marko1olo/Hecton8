using Hecton8.Environment;
using Hecton8.Items;
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
            weight *= GetMotivationBiasMultiplier(zone, socket);

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
                return string.Concat(gameplayPurpose, " ", matrixBiome.visitPurpose);

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
                return string.Concat(contentLabel, ": ", biomeFitSummary);

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

            return string.Concat(biomeLabel, ": ", focus);
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
            return BuildZoneRoleLayoutText(rolePlan.relation, rolePlan.preferredSlice, rolePlan.targetCount, family);
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
                WorldContentSocket.ContentKind.NavigationMarker => "Strong Anchor",
                WorldContentSocket.ContentKind.HazardPoint => lateZone ? "Pressure Threshold" : "Warning",
                WorldContentSocket.ContentKind.CombatPoint => lateZone ? "Pressure Threshold" : "Pressure",
                WorldContentSocket.ContentKind.PowerPoint => rolePlan.targetCount >= 2 ? "Backbone" : "Support",
                WorldContentSocket.ContentKind.ServiceTarget => "Support Problem",
                WorldContentSocket.ContentKind.ConstructionPoint => "Build Opportunity",
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

            WorldZoneAnchor.ZoneKind primaryKind = primaryZone.Kind;
            WorldZoneAnchor.ZoneKind secondaryKind = secondaryZone.Kind;
            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => BuildBorderBlendReasonText(primaryKind, secondaryKind, "marshrut dolzhen uderzhivat pamyat na styke dvuh tipov vody."),
                WorldContentSocket.ContentKind.FabricationStation => BuildBorderBlendReasonText(primaryKind, secondaryKind, "tochka otdyha nuzhna tam, gde spokoynyy kontur nachinaet ustupat davleniyu."),
                WorldContentSocket.ContentKind.HazardPoint => BuildBorderBlendReasonText(primaryKind, secondaryKind, "opasnost dolzhna chitatsya kak yavnaya granitsa perehoda."),
                WorldContentSocket.ContentKind.CombatPoint => BuildBorderBlendReasonText(primaryKind, secondaryKind, "ugroza podhvatyvaet igroka imenno na perelome marshruta."),
                WorldContentSocket.ContentKind.Landmark => BuildBorderBlendReasonText(primaryKind, secondaryKind, "redkaya tsel silnee zapominaetsya na styke dvuh identichnostey mesta."),
                WorldContentSocket.ContentKind.ResourcePickup => BuildBorderBlendReasonText(primaryKind, secondaryKind, "melkaya nagrada pomogaet ponyat, chto ty uzhe vhodish v sosedniy kontur."),
                WorldContentSocket.ContentKind.ResourceNode => BuildBorderBlendReasonText(primaryKind, secondaryKind, "plotnaya nagrada opravdyvaet zahod chut glubzhe za privychnyy marshrut."),
                WorldContentSocket.ContentKind.ServiceTarget => BuildBorderBlendReasonText(primaryKind, secondaryKind, "servisnaya problema luchshe rabotaet kak uzkoe mesto perehoda."),
                WorldContentSocket.ContentKind.PowerPoint => BuildBorderBlendReasonText(primaryKind, secondaryKind, "silovaya liniya estestvenno svyazyvaet dva sosednih kontura."),
                WorldContentSocket.ContentKind.ConstructionPoint => BuildBorderBlendReasonText(primaryKind, secondaryKind, "stroyka luchshe chitaetsya kak svyazka mezhdu dvumya rezhimami prostranstva."),
                _ => BuildBorderBlendReasonText(primaryKind, secondaryKind, "tochka pomogaet pochuvstvovat perehod, a ne rezkiy obryv zony.")
            };
        }

        public string BuildResourceChannelItem(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            ItemData item = ResolveResourceChannelItem(zone, socket);
            if (item == null)
                return "None";

            return string.IsNullOrWhiteSpace(item.itemName) ? item.name : item.itemName;
        }

        public string BuildResourceChannelReason(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            HectonBiomeResourceChannelProfile channels = zone != null && zone.DominantBiomeFamily != null
                ? zone.DominantBiomeFamily.resourceChannelProfile
                : null;
            if (channels == null || socket == null)
                return "No resource-channel mapping.";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => channels.pocketRead,
                WorldContentSocket.ContentKind.ResourceNode => channels.nodeRead,
                WorldContentSocket.ContentKind.FabricationStation => channels.safePocketRead,
                WorldContentSocket.ContentKind.NavigationMarker => channels.routeRead,
                WorldContentSocket.ContentKind.Landmark => channels.rareRead,
                WorldContentSocket.ContentKind.HazardPoint => channels.rareRead,
                WorldContentSocket.ContentKind.CombatPoint => channels.rareRead,
                _ => channels.routeRead
            };
        }

        public string BuildSandboxAttractionRole(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldSandboxAttractionProfile attraction = zone != null && zone.Profile != null
                ? zone.Profile.sandboxAttractionProfile
                : null;
            if (attraction == null || socket == null)
                return "None";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => attraction.entryRead,
                WorldContentSocket.ContentKind.ResourcePickup => attraction.ambientValue,
                WorldContentSocket.ContentKind.ResourceNode => attraction.detourValue,
                WorldContentSocket.ContentKind.FabricationStation => attraction.shelterRead,
                WorldContentSocket.ContentKind.ConstructionPoint => attraction.detourValue,
                WorldContentSocket.ContentKind.PowerPoint => attraction.pressureRead,
                WorldContentSocket.ContentKind.ServiceTarget => attraction.pressureRead,
                WorldContentSocket.ContentKind.HazardPoint => attraction.pressureRead,
                WorldContentSocket.ContentKind.CombatPoint => attraction.pressureRead,
                WorldContentSocket.ContentKind.Landmark => attraction.deepLure,
                _ => attraction.playerPromise
            };
        }

        public string BuildSandboxAttractionReason(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldSandboxAttractionProfile attraction = zone != null && zone.Profile != null
                ? zone.Profile.sandboxAttractionProfile
                : null;
            if (attraction == null || socket == null)
                return "No sandbox attraction profile.";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.NavigationMarker => attraction.memoryRule,
                WorldContentSocket.ContentKind.ResourcePickup => attraction.curiosityRule,
                WorldContentSocket.ContentKind.ResourceNode => attraction.crosslinkRule,
                WorldContentSocket.ContentKind.FabricationStation => attraction.reentryRule,
                WorldContentSocket.ContentKind.ConstructionPoint => attraction.curiosityRule,
                WorldContentSocket.ContentKind.PowerPoint => attraction.dangerRule,
                WorldContentSocket.ContentKind.ServiceTarget => attraction.dangerRule,
                WorldContentSocket.ContentKind.HazardPoint => attraction.dangerRule,
                WorldContentSocket.ContentKind.CombatPoint => attraction.dangerRule,
                WorldContentSocket.ContentKind.Landmark => attraction.masteryRule,
                _ => attraction.freedomRule
            };
        }

        public string BuildMotivationPull(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldMotivationProfile motivation = zone != null && zone.Profile != null
                ? zone.Profile.motivationProfile
                : null;
            if (motivation == null || socket == null)
                return "None";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => motivation.resourceNeed,
                WorldContentSocket.ContentKind.ResourceNode => motivation.resourceNeed,
                WorldContentSocket.ContentKind.FabricationStation => motivation.survivalNeed,
                WorldContentSocket.ContentKind.ConstructionPoint => motivation.engineeringNeed,
                WorldContentSocket.ContentKind.PowerPoint => motivation.engineeringNeed,
                WorldContentSocket.ContentKind.ServiceTarget => motivation.engineeringNeed,
                WorldContentSocket.ContentKind.NavigationMarker => motivation.curiosityPull,
                WorldContentSocket.ContentKind.HazardPoint => motivation.curiosityPull,
                WorldContentSocket.ContentKind.CombatPoint => motivation.survivalNeed,
                WorldContentSocket.ContentKind.Landmark => Mathf.Max(motivation.storyPullWeight, motivation.rareValuePullWeight) >= motivation.curiosityPullWeight
                    ? (motivation.storyPullWeight >= motivation.rareValuePullWeight ? motivation.storyPull : motivation.rareValuePull)
                    : motivation.curiosityPull,
                _ => motivation.optionalityRule
            };
        }

        public string BuildMotivationReason(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldMotivationProfile motivation = zone != null && zone.Profile != null
                ? zone.Profile.motivationProfile
                : null;
            if (motivation == null || socket == null)
                return "No motivation profile.";

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => motivation.returnRule,
                WorldContentSocket.ContentKind.ResourceNode => motivation.returnRule,
                WorldContentSocket.ContentKind.FabricationStation => motivation.optionalityRule,
                WorldContentSocket.ContentKind.ConstructionPoint => motivation.ownershipRule,
                WorldContentSocket.ContentKind.PowerPoint => motivation.ownershipRule,
                WorldContentSocket.ContentKind.ServiceTarget => motivation.ownershipRule,
                WorldContentSocket.ContentKind.NavigationMarker => motivation.ownershipRule,
                WorldContentSocket.ContentKind.HazardPoint => motivation.optionalityRule,
                WorldContentSocket.ContentKind.CombatPoint => motivation.optionalityRule,
                WorldContentSocket.ContentKind.Landmark => motivation.returnRule,
                _ => motivation.optionalityRule
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

        private float GetMotivationBiasMultiplier(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            WorldMotivationProfile motivation = zone != null && zone.Profile != null
                ? zone.Profile.motivationProfile
                : null;
            if (motivation == null || socket == null)
                return 1f;

            float weight = socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => motivation.resourceNeedWeight,
                WorldContentSocket.ContentKind.ResourceNode => Mathf.Max(motivation.resourceNeedWeight, motivation.rareValuePullWeight),
                WorldContentSocket.ContentKind.FabricationStation => Mathf.Max(motivation.survivalNeedWeight, motivation.engineeringNeedWeight),
                WorldContentSocket.ContentKind.ConstructionPoint => Mathf.Max(motivation.engineeringNeedWeight, motivation.curiosityPullWeight),
                WorldContentSocket.ContentKind.PowerPoint => Mathf.Max(motivation.engineeringNeedWeight, motivation.storyPullWeight),
                WorldContentSocket.ContentKind.ServiceTarget => Mathf.Max(motivation.engineeringNeedWeight, motivation.survivalNeedWeight),
                WorldContentSocket.ContentKind.NavigationMarker => Mathf.Max(motivation.curiosityPullWeight, motivation.survivalNeedWeight),
                WorldContentSocket.ContentKind.HazardPoint => Mathf.Max(motivation.curiosityPullWeight, motivation.rareValuePullWeight),
                WorldContentSocket.ContentKind.CombatPoint => Mathf.Max(motivation.survivalNeedWeight, motivation.rareValuePullWeight),
                WorldContentSocket.ContentKind.Landmark => Mathf.Max(motivation.storyPullWeight, motivation.rareValuePullWeight, motivation.curiosityPullWeight),
                _ => 1f
            };

            return Mathf.Lerp(0.82f, 1.28f, Mathf.InverseLerp(0.1f, 2f, weight));
        }

        private static float BiasToMultiplier(int bias)
        {
            float t = Mathf.InverseLerp(1f, 5f, bias);
            return Mathf.Lerp(0.72f, 1.35f, t);
        }

        private static ItemData ResolveResourceChannelItem(WorldZoneAnchor zone, WorldContentSocket socket)
        {
            HectonBiomeResourceChannelProfile channels = zone != null && zone.DominantBiomeFamily != null
                ? zone.DominantBiomeFamily.resourceChannelProfile
                : null;
            if (channels == null || socket == null)
                return null;

            return socket.Kind switch
            {
                WorldContentSocket.ContentKind.ResourcePickup => channels.resourcePocketItem,
                WorldContentSocket.ContentKind.ResourceNode => channels.nodeClusterItem,
                WorldContentSocket.ContentKind.FabricationStation => channels.safePocketItem,
                WorldContentSocket.ContentKind.ConstructionPoint => channels.buildSocketItem,
                WorldContentSocket.ContentKind.PowerPoint => channels.powerSpineItem,
                WorldContentSocket.ContentKind.ServiceTarget => channels.serviceChokeItem,
                WorldContentSocket.ContentKind.NavigationMarker => channels.routeAnchorHintItem,
                WorldContentSocket.ContentKind.HazardPoint => channels.hazardGateRewardItem,
                WorldContentSocket.ContentKind.CombatPoint => channels.hazardGateRewardItem,
                WorldContentSocket.ContentKind.Landmark => channels.rareObjectiveRewardItem,
                _ => null
            };
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

        private static string BuildZoneRoleLayoutText(
            WorldZonePlanProfile.SpatialRelation relation,
            WorldSliceAnchor.SliceState preferredSlice,
            int targetCount,
            string family)
        {
            string relationLabel = ResolveSpatialRelationLabel(relation);
            string preferredSliceLabel = ResolveSliceStateLabel(preferredSlice);
            family ??= string.Empty;

            const string roleSeparator = " / ";
            const string countPrefix = " / count ";
            int digitCount = CountIntDigits(targetCount);
            int length = relationLabel.Length + roleSeparator.Length + preferredSliceLabel.Length + countPrefix.Length + digitCount + roleSeparator.Length + family.Length;
            return string.Create(length, (relationLabel, preferredSliceLabel, targetCount, family), (buffer, state) =>
            {
                int write = 0;
                write = CopyString(state.relationLabel, buffer, write);
                write = CopyString(roleSeparator, buffer, write);
                write = CopyString(state.preferredSliceLabel, buffer, write);
                write = CopyString(countPrefix, buffer, write);
                write = WriteInt(state.targetCount, buffer, write);
                write = CopyString(roleSeparator, buffer, write);
                CopyString(state.family, buffer, write);
            });
        }

        private static string BuildBorderBlendReasonText(
            WorldZoneAnchor.ZoneKind primaryKind,
            WorldZoneAnchor.ZoneKind secondaryKind,
            string reason)
        {
            const string pairSeparator = " <-> ";
            const string reasonSeparator = ": ";
            string primary = ResolveZoneKindLabel(primaryKind);
            string secondary = ResolveZoneKindLabel(secondaryKind);
            reason ??= string.Empty;

            int length = primary.Length + pairSeparator.Length + secondary.Length + reasonSeparator.Length + reason.Length;
            return string.Create(length, (primary, secondary, reason), (buffer, state) =>
            {
                int write = 0;
                write = CopyString(state.primary, buffer, write);
                write = CopyString(pairSeparator, buffer, write);
                write = CopyString(state.secondary, buffer, write);
                write = CopyString(reasonSeparator, buffer, write);
                CopyString(state.reason, buffer, write);
            });
        }

        private static string ResolveZoneKindLabel(WorldZoneAnchor.ZoneKind kind)
        {
            return kind switch
            {
                WorldZoneAnchor.ZoneKind.Generic => "Generic",
                WorldZoneAnchor.ZoneKind.Resources => "Resources",
                WorldZoneAnchor.ZoneKind.Fabrication => "Fabrication",
                WorldZoneAnchor.ZoneKind.Trial => "Trial",
                WorldZoneAnchor.ZoneKind.Construction => "Construction",
                WorldZoneAnchor.ZoneKind.Power => "Power",
                WorldZoneAnchor.ZoneKind.Service => "Service",
                WorldZoneAnchor.ZoneKind.Progression => "Progression",
                WorldZoneAnchor.ZoneKind.Combat => "Combat",
                WorldZoneAnchor.ZoneKind.Navigation => "Navigation",
                _ => "Unknown"
            };
        }

        private static string ResolveSpatialRelationLabel(WorldZonePlanProfile.SpatialRelation relation)
        {
            return relation switch
            {
                WorldZonePlanProfile.SpatialRelation.AlongMainRoute => "AlongMainRoute",
                WorldZonePlanProfile.SpatialRelation.NearRouteAnchor => "NearRouteAnchor",
                WorldZonePlanProfile.SpatialRelation.BehindCover => "BehindCover",
                WorldZonePlanProfile.SpatialRelation.OffMainRoute => "OffMainRoute",
                WorldZonePlanProfile.SpatialRelation.AtBranchPoint => "AtBranchPoint",
                WorldZonePlanProfile.SpatialRelation.AroundHeroObject => "AroundHeroObject",
                WorldZonePlanProfile.SpatialRelation.BehindHazardGate => "BehindHazardGate",
                WorldZonePlanProfile.SpatialRelation.AtRouteTerminus => "AtRouteTerminus",
                _ => "OffMainRoute"
            };
        }

        private static string ResolveSliceStateLabel(WorldSliceAnchor.SliceState state)
        {
            return state switch
            {
                WorldSliceAnchor.SliceState.Near => "Near",
                WorldSliceAnchor.SliceState.Mid => "Mid",
                _ => "Far"
            };
        }

        private static int CountIntDigits(int value)
        {
            long remaining = value;
            int digits = remaining < 0L ? 2 : 1;
            if (remaining < 0L)
                remaining = -remaining;

            while (remaining >= 10L)
            {
                remaining /= 10L;
                digits++;
            }

            return digits;
        }

        private static int WriteInt(int value, System.Span<char> buffer, int start)
        {
            long remaining = value;
            bool negative = remaining < 0L;
            if (negative)
            {
                buffer[start++] = '-';
                remaining = -remaining;
            }

            int digitCount = CountPositiveIntDigits(remaining);
            int write = start + digitCount - 1;
            do
            {
                buffer[write--] = (char)('0' + remaining % 10L);
                remaining /= 10L;
            }
            while (write >= start);

            return start + digitCount;
        }

        private static int CountPositiveIntDigits(long value)
        {
            int digits = 1;
            while (value >= 10L)
            {
                value /= 10L;
                digits++;
            }

            return digits;
        }

        private static int CopyString(string value, System.Span<char> buffer, int start)
        {
            for (int i = 0; i < value.Length; i++)
                buffer[start + i] = value[i];

            return start + value.Length;
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
