using System;
using Hecton8.Items;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct BarterItemAmount
    {
        public ItemData item;
        [Min(1)] public int amount;
    }

    [CreateAssetMenu(fileName = "BarterOffer", menuName = "Hecton/Barter Offer", order = 130)]
    public sealed class BarterOfferData : ScriptableObject
    {
        [Header("Identity")]
        public string offerId = "offer.new";
        public string offerName = "Untitled Exchange";
        [TextArea(2, 5)] public string description = "";
        public string channelName = "FIELD RELAY";

        [Header("Gates")]
        [Tooltip("Optional scan-log entry required before this offer becomes available.")]
        public string requiredScanEntryId = "";
        [Tooltip("0 = unlimited executions.")]
        [Min(0)] public int repeatLimit = 1;

        [Header("Payload")]
        public BarterItemAmount[] costs = Array.Empty<BarterItemAmount>();
        public BarterItemAmount[] rewards = Array.Empty<BarterItemAmount>();

        [Header("Presentation")]
        public int priority = 0;
        public Sprite icon;

        public bool IsRepeatable => repeatLimit == 0 || repeatLimit > 1;
    }
}
