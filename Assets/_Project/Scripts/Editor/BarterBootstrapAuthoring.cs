using Hecton8.Gameplay;
using Hecton8.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools
{
    public static class BarterBootstrapAuthoring
    {
        private const string DataFolder = "Assets/_Project/Data/Barter";
        private const string CatalogPath = DataFolder + "/BarterOfferCatalog_Starter.asset";

        [MenuItem("Hecton/Authoring/Rebuild Starter Barter Relay", priority = 216)]
        public static void RebuildStarterBarterRelay()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder(DataFolder);

            ItemData copper = LoadItem("Assets/_Project/Data/Items/Data_Copper.asset");
            ItemData beacon = LoadItem("Assets/_Project/Data/Items/Tools/Item_Tool_BeaconDeployer.asset");
            ItemData flashlight = LoadItem("Assets/_Project/Data/Items/Tools/Item_Tool_Flashlight.asset");
            ItemData repair = LoadItem("Assets/_Project/Data/Items/Tools/Item_Tool_Repair.asset");

            BarterOfferData relay = CreateOrUpdateOffer(
                $"{DataFolder}/Offer_RelayStarter.asset",
                "offer.relaystarter",
                "Relay Starter Kit",
                "FIELD RELAY",
                "Exchange refined copper stock for an emergency beacon package.",
                new[] { MakeAmount(copper, 2) },
                new[] { MakeAmount(beacon, 1) },
                "scan.resource_node",
                1,
                10);

            BarterOfferData illumination = CreateOrUpdateOffer(
                $"{DataFolder}/Offer_Illumination.asset",
                "offer.illumination",
                "Illumination Requisition",
                "LOGISTICS",
                "Route emergency lighting hardware into the field inventory.",
                new[] { MakeAmount(copper, 3) },
                new[] { MakeAmount(flashlight, 1) },
                string.Empty,
                1,
                20);

            BarterOfferData repairLoop = CreateOrUpdateOffer(
                $"{DataFolder}/Offer_RepairLoop.asset",
                "offer.repairloop",
                "Suit Maintenance Loop",
                "SERVICE CHANNEL",
                "Spend copper reserves to requisition a replacement repair tool.",
                new[] { MakeAmount(copper, 4) },
                new[] { MakeAmount(repair, 1) },
                string.Empty,
                0,
                30);

            BarterOfferCatalog catalog = AssetDatabase.LoadAssetAtPath<BarterOfferCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BarterOfferCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SerializedObject so = new SerializedObject(catalog);
            SerializedProperty offersProp = so.FindProperty("offers");
            offersProp.arraySize = 3;
            offersProp.GetArrayElementAtIndex(0).objectReferenceValue = relay;
            offersProp.GetArrayElementAtIndex(1).objectReferenceValue = illumination;
            offersProp.GetArrayElementAtIndex(2).objectReferenceValue = repairLoop;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            AssignCatalogToScene(catalog);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
                EditorSceneManager.MarkSceneDirty(activeScene);

            Debug.Log("[BarterBootstrapAuthoring] Starter barter relay rebuilt.");
        }

        private static void AssignCatalogToScene(BarterOfferCatalog catalog)
        {
            PDAExchangeSystem exchange = Object.FindFirstObjectByType<PDAExchangeSystem>(FindObjectsInactive.Include);
            if (exchange == null)
            {
                GameObject player = GameObject.Find("Player");
                if (player == null)
                    return;

                exchange = Undo.AddComponent<PDAExchangeSystem>(player);
            }

            SerializedObject so = new SerializedObject(exchange);
            so.FindProperty("offerCatalog").objectReferenceValue = catalog;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(exchange);
        }

        private static BarterOfferData CreateOrUpdateOffer(
            string path,
            string offerId,
            string offerName,
            string channelName,
            string description,
            BarterItemAmount[] costs,
            BarterItemAmount[] rewards,
            string requiredScanEntryId,
            int repeatLimit,
            int priority)
        {
            BarterOfferData asset = AssetDatabase.LoadAssetAtPath<BarterOfferData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BarterOfferData>();
                AssetDatabase.CreateAsset(asset, path);
            }

            asset.offerId = offerId;
            asset.offerName = offerName;
            asset.channelName = channelName;
            asset.description = description;
            asset.costs = costs;
            asset.rewards = rewards;
            asset.requiredScanEntryId = requiredScanEntryId;
            asset.repeatLimit = repeatLimit;
            asset.priority = priority;
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static BarterItemAmount MakeAmount(ItemData item, int amount)
        {
            return new BarterItemAmount { item = item, amount = amount };
        }

        private static ItemData LoadItem(string path)
        {
            return AssetDatabase.LoadAssetAtPath<ItemData>(path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
