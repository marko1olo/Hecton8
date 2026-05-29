using System.Collections.Generic;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [CreateAssetMenu(fileName = "BarterOfferCatalog", menuName = "Hecton/Barter Offer Catalog", order = 131)]
    public sealed class BarterOfferCatalog : ScriptableObject
    {
        [SerializeField] private List<BarterOfferData> offers = new List<BarterOfferData>(8);
        [SerializeField, HideInInspector] private int validationNullOfferCount;
        [SerializeField, HideInInspector] private int validationDuplicateOfferHashCount;
        [SerializeField, HideInInspector] private int validationFirstNullOfferIndex = -1;
        [SerializeField, HideInInspector] private int validationFirstDuplicateOfferHashIndex = -1;
        [SerializeField, HideInInspector] private int validationRuntimeOfferCount;

        public int Count => offers != null ? offers.Count : 0;
        public IReadOnlyList<BarterOfferData> Offers => offers;
        public int ValidationNullOfferCount => validationNullOfferCount;
        public int ValidationDuplicateOfferHashCount => validationDuplicateOfferHashCount;
        public int ValidationFirstNullOfferIndex => validationFirstNullOfferIndex;
        public int ValidationFirstDuplicateOfferHashIndex => validationFirstDuplicateOfferHashIndex;
        public int ValidationRuntimeOfferCount => validationRuntimeOfferCount;
        public bool HasValidationErrors => validationNullOfferCount > 0 || validationDuplicateOfferHashCount > 0;

        public BarterOfferData GetAt(int index)
        {
            if (offers == null || index < 0 || index >= offers.Count)
                return null;

            return offers[index];
        }

        public void RefreshValidationState()
        {
            validationNullOfferCount = 0;
            validationDuplicateOfferHashCount = 0;
            validationFirstNullOfferIndex = -1;
            validationFirstDuplicateOfferHashIndex = -1;
            validationRuntimeOfferCount = 0;

            if (offers == null)
                return;

            for (int i = 0; i < offers.Count; i++)
            {
                BarterOfferData offer = offers[i];
                if (offer == null)
                {
                    validationNullOfferCount++;
                    if (validationFirstNullOfferIndex < 0)
                        validationFirstNullOfferIndex = i;

                    continue;
                }

                offer.RefreshValidationState();
                int offerHash = ResolveRuntimeOfferHash(offer);
                if (offerHash == 0)
                    continue;

                if (HasDuplicateOfferHashBefore(offerHash, i))
                {
                    validationDuplicateOfferHashCount++;
                    if (validationFirstDuplicateOfferHashIndex < 0)
                        validationFirstDuplicateOfferHashIndex = i;

                    continue;
                }

                validationRuntimeOfferCount++;
            }
        }

        public bool IsRuntimeOfferSlotValid(int index)
        {
            BarterOfferData offer = GetAt(index);
            if (offer == null)
                return false;

            int offerHash = ResolveRuntimeOfferHash(offer);
            return offerHash != 0 && !HasDuplicateOfferHashBefore(offerHash, index);
        }

        private bool HasDuplicateOfferHashBefore(int offerHash, int index)
        {
            if (offers == null || offerHash == 0)
                return false;

            for (int i = 0; i < index; i++)
            {
                if (ResolveRuntimeOfferHash(offers[i]) == offerHash)
                    return true;
            }

            return false;
        }

        private static int ResolveRuntimeOfferHash(BarterOfferData offer)
        {
            if (offer == null)
                return 0;

            if ((offer.ValidationFlags & BarterOfferValidationFlags.MissingOfferId) != 0)
                return 0;

            string offerId = offer.RuntimeOfferId;
            return string.IsNullOrWhiteSpace(offerId) ? 0 : LocHash.Compute(offerId);
        }

        private void OnEnable()
        {
            RefreshValidationState();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (offers == null)
                offers = new List<BarterOfferData>(8);

            RefreshValidationState();
        }
#endif
    }
}
