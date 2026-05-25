using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hecton8
{
    /// <summary>
    /// Defines the complete content contract for a biome family.
    /// Every biome must implement this to ensure consistent world meaning and player experience.
    /// </summary>
    [Serializable]
    public struct BiomeContentPackContract
    {
        [Header("Biome Identity")]
        [Tooltip("Unique biome family identifier (e.g., 'AbyssalPlain', 'VolcanicRidge')")]
        public string biomeFamilyId;

        [Tooltip("Human-readable display name")]
        public string displayName;

        [Tooltip("Biome depth range in meters (min, max)")]
        public Vector2 depthRange;

        [Header("Geology Role")]
        [Tooltip("Primary geological features this biome contributes")]
        public GeologyRole geologyRole;

        [Tooltip("Terrain deformation patterns (cracks, mounds, trenches)")]
        public List<string> terrainPatterns;

        [Tooltip("Cave generation density (0-1, where 1 = maximum cave coverage)")]
        [Range(0f, 1f)]
        public float caveDensity;

        [Header("Flora Role")]
        [Tooltip("Primary vegetation types and their roles")]
        public List<FloraRole> floraRoles;

        [Tooltip("Vegetation density multiplier (0-1)")]
        [Range(0f, 1f)]
        public float vegetationDensity;

        [Header("Microfauna Flavor")]
        [Tooltip("Ambient microfauna behavior patterns")]
        public MicrofaunaFlavor microfaunaFlavor;

        [Tooltip("Microfauna spawn density (0-1)")]
        [Range(0f, 1f)]
        public float microfaunaDensity;

        [Header("Passive Fauna")]
        [Tooltip("Non-aggressive creature types and their behaviors")]
        public List<PassiveFaunaEntry> passiveFauna;

        [Tooltip("Passive fauna spawn density (0-1)")]
        [Range(0f, 1f)]
        public float passiveFaunaDensity;

        [Header("Predator Pressure")]
        [Tooltip("Aggressive creature types and their threat levels")]
        public List<PredatorEntry> predators;

        [Tooltip("Overall predator pressure (0-1, affects player caution)")]
        [Range(0f, 1f)]
        public float predatorPressure;

        [Header("Ruin Relation")]
        [Tooltip("Types of human/artificial ruins found here")]
        public List<RuinType> ruinTypes;

        [Tooltip("Ruin spawn density (0-1)")]
        [Range(0f, 1f)]
        public float ruinDensity;

        [Tooltip("Ruin condition (0 = pristine, 1 = completely overgrown/ruined)")]
        [Range(0f, 1f)]
        public float ruinCondition;

        [Header("Cave Relation")]
        [Tooltip("Cave types and features specific to this biome")]
        public List<CaveFeature> caveFeatures;

        [Tooltip("Cave exploration difficulty (0 = easy navigation, 1 = extreme danger)")]
        [Range(0f, 1f)]
        public float caveDifficulty;

        [Header("Resource Signature")]
        [Tooltip("Primary resources found in this biome")]
        public List<ResourceEntry> primaryResources;

        [Tooltip("Rare/valuable resources (lower spawn rate)")]
        public List<ResourceEntry> rareResources;

        [Tooltip("Resource node density (0-1)")]
        [Range(0f, 1f)]
        public float resourceDensity;

        [Header("Memory Motif")]
        [Tooltip("Emotional/psychological themes this biome evokes")]
        public List<string> memoryMotifs;

        [Tooltip("Visual silhouette patterns that define the biome's character")]
        public List<string> silhouettePatterns;

        [Header("Return Reason")]
        [Tooltip("Reasons why players would want to return to this biome")]
        public List<ReturnReason> returnReasons;

        [Tooltip("Biome's role in the overall game progression")]
        public ProgressionRole progressionRole;

        /// <summary>
        /// Validates that this contract is complete and consistent.
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(biomeFamilyId) &&
                   !string.IsNullOrEmpty(displayName) &&
                   depthRange.x < depthRange.y &&
                   geologyRole != null &&
                   floraRoles != null && floraRoles.Count > 0 &&
                   microfaunaFlavor != null &&
                   passiveFauna != null &&
                   predators != null &&
                   ruinTypes != null &&
                   caveFeatures != null &&
                   primaryResources != null &&
                   rareResources != null &&
                   memoryMotifs != null && memoryMotifs.Count > 0 &&
                   silhouettePatterns != null && silhouettePatterns.Count > 0 &&
                   returnReasons != null && returnReasons.Count > 0 &&
                   progressionRole != null;
        }

        /// <summary>
        /// Creates a default contract template for a new biome.
        /// </summary>
        public static BiomeContentPackContract CreateTemplate(string biomeId, string name, Vector2 depth)
        {
            return new BiomeContentPackContract
            {
                biomeFamilyId = biomeId,
                displayName = name,
                depthRange = depth,
                geologyRole = GeologyRole.CreateDefault(),
                terrainPatterns = new List<string>(4),
                caveDensity = 0.5f,
                floraRoles = new List<FloraRole>(4),
                vegetationDensity = 0.5f,
                microfaunaFlavor = MicrofaunaFlavor.CreateDefault(),
                microfaunaDensity = 0.5f,
                passiveFauna = new List<PassiveFaunaEntry>(4),
                passiveFaunaDensity = 0.5f,
                predators = new List<PredatorEntry>(2),
                predatorPressure = 0.5f,
                ruinTypes = new List<RuinType>(2),
                ruinDensity = 0.5f,
                ruinCondition = 0.5f,
                caveFeatures = new List<CaveFeature>(4),
                caveDifficulty = 0.5f,
                primaryResources = new List<ResourceEntry>(6),
                rareResources = new List<ResourceEntry>(3),
                resourceDensity = 0.5f,
                memoryMotifs = new List<string>(4),
                silhouettePatterns = new List<string>(4),
                returnReasons = new List<ReturnReason>(4),
                progressionRole = ProgressionRole.CreateDefault()
            };
        }
    }

    /// <summary>
    /// Defines the geological contribution of a biome.
    /// </summary>
    [Serializable]
    public class GeologyRole
    {
        [Tooltip("Primary terrain material (rock, sediment, volcanic)")]
        public string primaryMaterial;

        [Tooltip("Secondary terrain features")]
        public List<string> secondaryFeatures;

        [Tooltip("Terrain roughness (0 = smooth, 1 = extremely jagged)")]
        [Range(0f, 1f)]
        public float roughness;

        [Tooltip("Terrain scale (affects feature size)")]
        [Range(0.1f, 10f)]
        public float scale;

        public static GeologyRole CreateDefault()
        {
            return new GeologyRole
            {
                primaryMaterial = "Sediment",
                secondaryFeatures = new List<string>(4),
                roughness = 0.5f,
                scale = 1f
            };
        }
    }

    /// <summary>
    /// Defines a flora type and its ecological role.
    /// </summary>
    [Serializable]
    public class FloraRole
    {
        [Tooltip("Flora type name")]
        public string typeName;

        [Tooltip("Ecological role (oxygen, food, shelter, decoration)")]
        public string ecologicalRole;

        [Tooltip("Visual density (0-1)")]
        [Range(0f, 1f)]
        public float visualDensity;

        [Tooltip("Functional importance (0-1)")]
        [Range(0f, 1f)]
        public float functionalImportance;
    }

    /// <summary>
    /// Defines microfauna behavior patterns.
    /// </summary>
    [Serializable]
    public class MicrofaunaFlavor
    {
        [Tooltip("Behavior pattern (swarming, schooling, solitary, territorial)")]
        public string behaviorPattern;

        [Tooltip("Movement speed multiplier")]
        [Range(0.1f, 5f)]
        public float movementSpeed;

        [Tooltip("Activity level (0 = dormant, 1 = hyperactive)")]
        [Range(0f, 1f)]
        public float activityLevel;

        [Tooltip("Response to player presence")]
        public string playerResponse;

        public static MicrofaunaFlavor CreateDefault()
        {
            return new MicrofaunaFlavor
            {
                behaviorPattern = "Schooling",
                movementSpeed = 1f,
                activityLevel = 0.5f,
                playerResponse = "Neutral"
            };
        }
    }

    /// <summary>
    /// Defines a passive fauna entry.
    /// </summary>
    [Serializable]
    public class PassiveFaunaEntry
    {
        [Tooltip("Creature type identifier")]
        public string creatureType;

        [Tooltip("Behavior pattern")]
        public string behavior;

        [Tooltip("Size category")]
        public string sizeCategory;

        [Tooltip("Rarity (0 = common, 1 = extremely rare)")]
        [Range(0f, 1f)]
        public float rarity;
    }

    /// <summary>
    /// Defines a predator entry.
    /// </summary>
    [Serializable]
    public class PredatorEntry
    {
        [Tooltip("Predator type identifier")]
        public string predatorType;

        [Tooltip("Threat level (0 = minor nuisance, 1 = lethal)")]
        [Range(0f, 1f)]
        public float threatLevel;

        [Tooltip("Aggression trigger distance")]
        public float triggerDistance;

        [Tooltip("Hunt pattern")]
        public string huntPattern;
    }

    /// <summary>
    /// Defines a ruin type.
    /// </summary>
    [Serializable]
    public class RuinType
    {
        [Tooltip("Ruin category (research, habitation, industrial)")]
        public string category;

        [Tooltip("Architectural style")]
        public string style;

        [Tooltip("Story significance")]
        public string storySignificance;
    }

    /// <summary>
    /// Defines a cave feature.
    /// </summary>
    [Serializable]
    public class CaveFeature
    {
        [Tooltip("Feature type (chamber, tunnel, seam)")]
        public string featureType;

        [Tooltip("Geological material")]
        public string material;

        [Tooltip("Exploration value")]
        public string explorationValue;
    }

    /// <summary>
    /// Defines a resource entry.
    /// </summary>
    [Serializable]
    public class ResourceEntry
    {
        [Tooltip("Resource type identifier")]
        public string resourceType;

        [Tooltip("Node size (small, medium, large)")]
        public string nodeSize;

        [Tooltip("Harvest difficulty")]
        public string harvestDifficulty;

        [Tooltip("Economic value")]
        public float economicValue;
    }

    /// <summary>
    /// Defines a return reason.
    /// </summary>
    [Serializable]
    public class ReturnReason
    {
        [Tooltip("Reason category (resources, exploration, story)")]
        public string category;

        [Tooltip("Specific motivation")]
        public string motivation;

        [Tooltip("Reward type")]
        public string rewardType;
    }

    /// <summary>
    /// Defines the biome's role in game progression.
    /// </summary>
    [Serializable]
    public class ProgressionRole
    {
        [Tooltip("Progression stage (early, mid, late)")]
        public string stage;

        [Tooltip("Unlock requirements")]
        public List<string> unlockRequirements;

        [Tooltip("Rewards for completion")]
        public List<string> completionRewards;

        public static ProgressionRole CreateDefault()
        {
            return new ProgressionRole
            {
                stage = "Early",
                unlockRequirements = new List<string>(4),
                completionRewards = new List<string>(4)
            };
        }
    }
}
