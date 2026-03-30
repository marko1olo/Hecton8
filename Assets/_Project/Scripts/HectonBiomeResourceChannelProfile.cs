using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Environment
{
    [CreateAssetMenu(fileName = "BiomeResourceChannelProfile", menuName = "Hecton/Environment/Biome Resource Channel Profile", order = 110)]
    public sealed class HectonBiomeResourceChannelProfile : ScriptableObject
    {
        [Header("Identity")]
        public string profileId = "biome.resource_channel.generic";
        public string profileLabel = "Generic Resource Channel";

        [Header("World Roles")]
        public ItemData resourcePocketItem;
        public ItemData nodeClusterItem;
        public ItemData safePocketItem;
        public ItemData buildSocketItem;
        public ItemData powerSpineItem;
        public ItemData serviceChokeItem;
        public ItemData routeAnchorHintItem;
        public ItemData hazardGateRewardItem;
        public ItemData rareObjectiveRewardItem;

        [Header("Reading")]
        [TextArea(2, 4)] public string pocketRead = "The nearby pocket pays out routine value.";
        [TextArea(2, 4)] public string nodeRead = "The node cluster pays out the biome's heavier extraction value.";
        [TextArea(2, 4)] public string safePocketRead = "Safe pockets give a small but useful sustain reward.";
        [TextArea(2, 4)] public string routeRead = "Route anchors hint what kind of material line this biome belongs to.";
        [TextArea(2, 4)] public string rareRead = "Rare objectives pay out the biome's expensive reason to return.";
    }
}
