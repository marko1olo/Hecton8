using System;
using Hecton8.Gameplay;
using Hecton8.Items;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class BarterCatalogValidator
    {
        private const string CatalogRoot = "Assets/_Project/Data/Barter";

        [MenuItem("Hecton8/Validation/Validate Barter Catalog", priority = 242)]
        public static void ValidateBarterCatalog()
        {
            int errors = 0;
            int warnings = 0;
            string[] guids = AssetDatabase.FindAssets("t:BarterOfferCatalog", new[] { CatalogRoot });
            Array.Sort(guids, StringComparer.Ordinal);
            if (guids.Length <= 0)
            {
                Hecton8.Core.H8Debug.LogWarning("[BarterValidation] No BarterOfferCatalog assets found.");
                return;
            }

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BarterOfferCatalog catalog = AssetDatabase.LoadAssetAtPath<BarterOfferCatalog>(path);
                if (catalog == null)
                    continue;

                catalog.RefreshValidationState();
                if (catalog.ValidationNullOfferCount > 0)
                {
                    Hecton8.Core.H8Debug.LogError($"[BarterValidation] Catalog has null offer slots: count={catalog.ValidationNullOfferCount} first={catalog.ValidationFirstNullOfferIndex} path={path}", catalog);
                }

                if (catalog.ValidationDuplicateOfferHashCount > 0)
                {
                    Hecton8.Core.H8Debug.LogError($"[BarterValidation] Catalog has duplicate runtime offer hashes: count={catalog.ValidationDuplicateOfferHashCount} first={catalog.ValidationFirstDuplicateOfferHashIndex} path={path}", catalog);
                    errors += catalog.ValidationDuplicateOfferHashCount;
                }

                for (int j = 0; j < catalog.Count; j++)
                {
                    BarterOfferData offer = catalog.GetAt(j);
                    if (offer == null)
                    {
                        Hecton8.Core.H8Debug.LogError($"[BarterValidation] Null offer in catalog: {path}", catalog);
                        errors++;
                        continue;
                    }

                    offer.RefreshValidationState();
                    if ((offer.ValidationFlags & BarterOfferValidationFlags.MissingOfferId) != 0)
                    {
                        Hecton8.Core.H8Debug.LogError($"[BarterValidation] Offer missing offerId: {offer.name}", offer);
                        errors++;
                    }

                    if (offer.costs == null || offer.costs.Length == 0)
                    {
                        Hecton8.Core.H8Debug.LogError($"[BarterValidation] Offer missing costs: {offer.name}", offer);
                        errors++;
                    }

                    if (offer.rewards == null || offer.rewards.Length == 0)
                    {
                        Hecton8.Core.H8Debug.LogError($"[BarterValidation] Offer missing rewards: {offer.name}", offer);
                        errors++;
                    }

                    ValidateBundle("cost", offer.costs, offer, ref errors);
                    ValidateBundle("reward", offer.rewards, offer, ref errors);
                }
            }

            if (errors <= 0 && warnings <= 0)
            {
                Hecton8.Core.H8Debug.Log("[BarterValidation] PASS no issues found.");
                return;
            }

            Hecton8.Core.H8Debug.LogWarning($"[BarterValidation] COMPLETE errors={errors} warnings={warnings}");
        }

        private static void ValidateBundle(string label, BarterItemAmount[] bundle, UnityEngine.Object context, ref int errors)
        {
            if (bundle == null)
                return;

            for (int i = 0; i < bundle.Length; i++)
            {
                ItemData item = bundle[i].item;
                if (item == null)
                {
                    Hecton8.Core.H8Debug.LogError($"[BarterValidation] Offer has null {label} item at index {i}.", context);
                    errors++;
                }

                if (bundle[i].amount <= 0)
                {
                    Hecton8.Core.H8Debug.LogError($"[BarterValidation] Offer has non-positive {label} amount at index {i}.", context);
                    errors++;
                }
            }
        }
    }
}
