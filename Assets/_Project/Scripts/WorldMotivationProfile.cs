using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldMotivationProfile", menuName = "Hecton8/World/Motivation Profile")]
    public sealed class WorldMotivationProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "zone.motivation.generic";
        public string profileLabel = "Generic Motivation";

        [Header("Primary Pulls")]
        [TextArea(1, 3)] public string survivalNeed = "This area helps the player stay supplied and recover from mistakes.";
        [Range(0.1f, 2f)] public float survivalNeedWeight = 1f;
        [TextArea(1, 3)] public string resourceNeed = "This area promises practical materials worth repeated short visits.";
        [Range(0.1f, 2f)] public float resourceNeedWeight = 1f;
        [TextArea(1, 3)] public string engineeringNeed = "This area supports building, repair, or power problem-solving.";
        [Range(0.1f, 2f)] public float engineeringNeedWeight = 1f;
        [TextArea(1, 3)] public string curiosityPull = "This area invites checking one more landmark, pocket, or silhouette.";
        [Range(0.1f, 2f)] public float curiosityPullWeight = 1f;
        [TextArea(1, 3)] public string storyPull = "This area hints that something larger or stranger lies nearby.";
        [Range(0.1f, 2f)] public float storyPullWeight = 1f;
        [TextArea(1, 3)] public string rareValuePull = "This area suggests there may be expensive late-game value for a riskier visit.";
        [Range(0.1f, 2f)] public float rareValuePullWeight = 1f;

        [Header("Sandbox Rules")]
        [TextArea(2, 4)] public string optionalityRule = "Any of these pulls may be ignored; the player chooses when a need matters enough.";
        [TextArea(2, 4)] public string returnRule = "The area should remain worth revisiting later with different gear or different goals.";
        [TextArea(2, 4)] public string ownershipRule = "The player should feel they are building their own mental map and reasons to come back.";
    }
}
