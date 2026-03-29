using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(fileName = "BarterOfferCatalog", menuName = "Hecton/Barter Offer Catalog", order = 131)]
    public sealed class BarterOfferCatalog : ScriptableObject
    {
        [SerializeField] private List<BarterOfferData> offers = new List<BarterOfferData>();

        public int Count => offers != null ? offers.Count : 0;
        public IReadOnlyList<BarterOfferData> Offers => offers;

        public BarterOfferData GetAt(int index)
        {
            if (offers == null || index < 0 || index >= offers.Count)
                return null;

            return offers[index];
        }
    }
}
