using Hecton8.Interaction;
using Hecton8.Items;
using Hecton8.Scavenging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    public static class ResourceWorldBootstrapAuthoring
    {
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Resources";
        private const string PickupPrefabFolder = "Assets/_Project/Prefabs/Resources/Pickups";
        private const string RootPath = "--- WORLD ---/Resource_FieldSources";
        private const float DefaultSurfaceWaterLevelY = 14.02f;

        [MenuItem("Hecton8/Authoring/Rebuild Starter Resource Sources", priority = 172)]
        public static void RebuildStarterResourceSources()
        {
            if (!WorldProceduralFinalPrefabQualityGate.AllowLegacyPrimitiveProductionAuthoring(
                    nameof(ResourceWorldBootstrapAuthoring),
                    PickupPrefabFolder))
                return;

            EnsureFolder("Assets/_Project/Art");
            EnsureFolder("Assets/_Project/Art/Materials");
            EnsureFolder(MaterialFolder);
            EnsureFolder("Assets/_Project/Prefabs");
            EnsureFolder("Assets/_Project/Prefabs/Resources");
            EnsureFolder(PickupPrefabFolder);

            ItemData titanium = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_TitaniumScrap.asset");
            ItemData copper = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_Copper.asset");
            ItemData silica = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_SilicaShards.asset");
            ItemData fiber = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_FiberKelp.asset");
            ItemData membrane = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_MembraneTissue.asset");
            ItemData silver = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_SilverOre.asset");
            ItemData sulfur = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_SulfurClumps.asset");
            ItemData resin = LoadItem("Assets/_Project/Data/Items/Resources/Raw/Data_HydrocarbonResin.asset");

            if (titanium == null || copper == null || silica == null || fiber == null ||
                membrane == null || silver == null || sulfur == null || resin == null)
            {
                Debug.LogError("[ResourceWorldBootstrap] Missing ItemData assets. Rebuild core resource kit first.");
                return;
            }

            Material scrapMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Scrap.mat", new Color(0.55f, 0.63f, 0.69f, 1f));
            Material copperMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Copper.mat", new Color(0.78f, 0.47f, 0.22f, 1f));
            Material silicaMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Silica.mat", new Color(0.72f, 0.82f, 0.92f, 1f));
            Material fiberMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Fiber.mat", new Color(0.28f, 0.66f, 0.38f, 1f));
            Material membraneMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Membrane.mat", new Color(0.42f, 0.78f, 0.62f, 1f));
            Material silverMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Silver.mat", new Color(0.75f, 0.78f, 0.84f, 1f));
            Material sulfurMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Sulfur.mat", new Color(0.88f, 0.82f, 0.24f, 1f));
            Material resinMat = CreateOrUpdateMaterial(MaterialFolder + "/Mat_Resource_Resin.mat", new Color(0.58f, 0.36f, 0.14f, 1f));

            if (ResourcePickupGeminiMaterialApplier.AreSourceMaterialsAvailable())
                ResourcePickupGeminiMaterialApplier.Apply(false);

            GameObject titaniumPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_TitaniumScrap.prefab", titanium, scrapMat, PrimitiveType.Cube, new Vector3(0.34f, 0.24f, 0.28f));
            GameObject copperPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_CopperOre.prefab", copper, copperMat, PrimitiveType.Cube, new Vector3(0.28f, 0.28f, 0.28f));
            GameObject silicaPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_SilicaShards.prefab", silica, silicaMat, PrimitiveType.Sphere, new Vector3(0.24f, 0.24f, 0.24f));
            GameObject fiberPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_FiberKelp.prefab", fiber, fiberMat, PrimitiveType.Capsule, new Vector3(0.22f, 0.55f, 0.22f));
            GameObject membranePickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_MembraneTissue.prefab", membrane, membraneMat, PrimitiveType.Sphere, new Vector3(0.30f, 0.20f, 0.30f));
            GameObject silverPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_SilverOre.prefab", silver, silverMat, PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 0.25f));
            GameObject sulfurPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_SulfurClumps.prefab", sulfur, sulfurMat, PrimitiveType.Sphere, new Vector3(0.26f, 0.26f, 0.26f));
            GameObject resinPickup = CreatePickupPrefab(PickupPrefabFolder + "/PFB_Resource_HydrocarbonResin.prefab", resin, resinMat, PrimitiveType.Capsule, new Vector3(0.28f, 0.38f, 0.28f));

            GameObject root = EnsureWorldRoot(RootPath);
            ClearChildren(root.transform);
            root.transform.position = new Vector3(96f, DefaultSurfaceWaterLevelY, 1678f);

            GameObject scrapField = CreateSceneRoot(root.transform, "Scrap_Field", Vector3.zero);
            PlacePickup(scrapField.transform, titaniumPickup, "Scrap_A", new Vector3(-1.8f, 0.25f, -0.8f));
            PlacePickup(scrapField.transform, titaniumPickup, "Scrap_B", new Vector3(-0.9f, 0.25f, 0.5f));
            PlacePickup(scrapField.transform, titaniumPickup, "Scrap_C", new Vector3(0.3f, 0.25f, -0.3f));
            PlacePickup(scrapField.transform, titaniumPickup, "Scrap_D", new Vector3(1.4f, 0.25f, 0.7f));

            GameObject mineralPocket = CreateSceneRoot(root.transform, "Mineral_Pocket", new Vector3(8f, 0f, -1f));
            CreateResourceNode(mineralPocket.transform, "Node_Copper_A", copperPickup, copperMat, new Vector3(-0.8f, 0.6f, 0.2f), 95f, 3);
            CreateResourceNode(mineralPocket.transform, "Node_Copper_B", copperPickup, copperMat, new Vector3(1.6f, 0.6f, -0.4f), 105f, 3);
            CreateResourceNode(mineralPocket.transform, "Node_Silica_A", silicaPickup, silicaMat, new Vector3(4.0f, 0.55f, 0.4f), 85f, 2);
            CreateResourceNode(mineralPocket.transform, "Node_Silver_A", silverPickup, silverMat, new Vector3(6.7f, 0.7f, -0.1f), 125f, 2);

            GameObject organicGarden = CreateSceneRoot(root.transform, "Organic_Garden", new Vector3(-7f, 0f, 7f));
            PlacePickup(organicGarden.transform, fiberPickup, "Fiber_A", new Vector3(-1.0f, 0.35f, -0.4f));
            PlacePickup(organicGarden.transform, fiberPickup, "Fiber_B", new Vector3(0.4f, 0.35f, 0.8f));
            PlacePickup(organicGarden.transform, fiberPickup, "Fiber_C", new Vector3(1.7f, 0.35f, -0.2f));
            PlacePickup(organicGarden.transform, membranePickup, "Membrane_A", new Vector3(3.2f, 0.25f, 0.4f));
            PlacePickup(organicGarden.transform, membranePickup, "Membrane_B", new Vector3(4.4f, 0.25f, -0.5f));

            GameObject chemicalSeep = CreateSceneRoot(root.transform, "Chemical_Seep", new Vector3(9f, 0f, 9f));
            CreateResourceNode(chemicalSeep.transform, "Node_Sulfur_A", sulfurPickup, sulfurMat, new Vector3(-0.6f, 0.45f, 0f), 90f, 2);
            PlacePickup(chemicalSeep.transform, resinPickup, "Resin_A", new Vector3(2.0f, 0.25f, -0.3f));
            PlacePickup(chemicalSeep.transform, resinPickup, "Resin_B", new Vector3(3.0f, 0.25f, 0.5f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded)
            {
                EditorSceneManager.MarkSceneDirty(activeScene);
            }

            Debug.Log("[ResourceWorldBootstrap] Starter resource sources rebuilt.");
        }

        [MenuItem("Hecton8/Validation/Validate Starter Resource Sources", priority = 173)]
        public static void ValidateStarterResourceSources()
        {
            int errors = 0;
            // Inactive-inclusive, because this is a VALIDATOR: with GameObject.Find it reported the
            // resource field sources as missing whenever the authored world root was disabled, which is
            // a false absence rather than a finding.
            GameObject root = FindByPathIncludingInactive(RootPath);
            if (root == null)
            {
                Debug.LogError("[ResourceWorldBootstrap] Missing world root '--- WORLD ---/Resource_FieldSources'.");
                return;
            }

            ValidatePickup(root.transform, "Scrap_Field/Scrap_A", ref errors);
            ValidatePickup(root.transform, "Scrap_Field/Scrap_B", ref errors);
            ValidatePickup(root.transform, "Organic_Garden/Fiber_A", ref errors);
            ValidatePickup(root.transform, "Organic_Garden/Membrane_A", ref errors);
            ValidatePickup(root.transform, "Chemical_Seep/Resin_A", ref errors);

            ValidateNode(root.transform, "Mineral_Pocket/Node_Copper_A", ref errors);
            ValidateNode(root.transform, "Mineral_Pocket/Node_Silica_A", ref errors);
            ValidateNode(root.transform, "Mineral_Pocket/Node_Silver_A", ref errors);
            ValidateNode(root.transform, "Chemical_Seep/Node_Sulfur_A", ref errors);

            if (errors == 0)
            {
                Debug.Log("[ResourceWorldBootstrap] PASS no issues found.");
            }
            else
            {
                Debug.LogError("[ResourceWorldBootstrap] FAIL " + errors + " issue(s) found.");
            }
        }

        private static ItemData LoadItem(string path)
        {
            return AssetDatabase.LoadAssetAtPath<ItemData>(path);
        }

        private static GameObject CreatePickupPrefab(string path, ItemData item, Material material, PrimitiveType primitiveType, Vector3 scale)
        {
            GameObject root = GameObject.CreatePrimitive(primitiveType);
            root.name = System.IO.Path.GetFileNameWithoutExtension(path);
            root.transform.localScale = scale;

            if (root.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.25f;
            rb.linearDamping = 1.2f;
            rb.angularDamping = 1.6f;

            PickupItem pickup = root.AddComponent<PickupItem>();
            pickup.Configure(item, 1);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Finds a named object at ANY DEPTH across every loaded scene, inactive included. Faithful
        /// replacement for <see cref="GameObject.Find"/>, which searched all loaded scenes at any depth
        /// but skipped inactive objects.
        ///
        /// The first version of this searched <c>GetRootGameObjects()</c> only, which was wrong twice
        /// over. Too narrow, because GameObject.Find was never root-only. And it missed the very state it
        /// was written for: <c>Assets/_Project/Editor/H8_SceneCleaner.cs</c> REPARENTED
        /// <c>--- WORLD ---</c> under <c>DEPRECATED_STUFF</c> and disabled it (:41-42, saved at :47), and a
        /// reparented object sits at depth 1 where a depth-0 scan cannot reach it. Caught by
        /// <c>H8_AuthoringRootReachabilityGate</c>.
        /// </summary>
        private static GameObject FindInLoadedScenesIncludingInactive(string targetName)
        {
            if (string.IsNullOrEmpty(targetName))
                return null;

            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    GameObject match = FindInHierarchyIncludingInactive(roots[i].transform, targetName);
                    if (match != null)
                        return match;
                }
            }

            return null;
        }

        /// <summary>
        /// Depth-first search for a named object under <paramref name="branch"/>, inactive children
        /// included. Transform child enumeration rather than GetComponentsInChildren, so nothing is
        /// allocated per node and inactive children are seen unconditionally.
        /// </summary>
        private static GameObject FindInHierarchyIncludingInactive(Transform branch, string targetName)
        {
            if (branch == null)
                return null;

            if (string.Equals(branch.name, targetName, System.StringComparison.Ordinal))
                return branch.gameObject;

            for (int i = 0; i < branch.childCount; i++)
            {
                GameObject match = FindInHierarchyIncludingInactive(branch.GetChild(i), targetName);
                if (match != null)
                    return match;
            }

            return null;
        }

        /// <summary>
        /// Resolves a "/"-separated hierarchy path, INCLUDING inactive objects. Only the first segment
        /// needs the scan - <see cref="Transform.Find"/> already accepts a path and already sees
        /// inactive children.
        /// </summary>
        private static GameObject FindByPathIncludingInactive(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            int firstSeparator = path.IndexOf('/');
            if (firstSeparator < 0)
                return FindInLoadedScenesIncludingInactive(path);

            GameObject root = FindInLoadedScenesIncludingInactive(path.Substring(0, firstSeparator));
            if (root == null)
                return null;

            Transform child = root.transform.Find(path.Substring(firstSeparator + 1));
            return child != null ? child.gameObject : null;
        }

        private static GameObject EnsureWorldRoot(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string[] parts = path.Split('/');
            if (parts.Length == 0) return null;

            // Inactive-inclusive, and note the asymmetry it repairs: the child lookup below already uses
            // Transform.Find, which DOES see inactive children, while GameObject.Find here saw only
            // ACTIVE objects. So once Assets/_Project/Editor/H8_SceneCleaner.cs reparented
            // '--- WORLD ---' under DEPRECATED_STUFF and disabled it (:41-42, then SaveScene at :47),
            // this reuse check went blind and every run created a SECOND, active world root beside the
            // buried one - in a binary scene with no diff to reveal it.
            GameObject current = FindInLoadedScenesIncludingInactive(parts[0]);
            if (current == null)
            {
                current = new GameObject(parts[0]);
            }

            for (int i = 1; i < parts.Length; i++)
            {
                Transform childTransform = current.transform.Find(parts[i]);
                GameObject found = childTransform != null ? childTransform.gameObject : null;

                if (found == null)
                {
                    found = new GameObject(parts[i]);
                    found.transform.SetParent(current.transform, false);
                }

                current = found;
            }

            return current;
        }

        private static GameObject CreateSceneRoot(Transform parent, string name, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static void PlacePickup(Transform parent, GameObject pickupPrefab, string name, Vector3 localPosition)
        {
            GameObject go = PrefabUtility.InstantiatePrefab(pickupPrefab, parent) as GameObject;
            if (go == null)
            {
                go = Object.Instantiate(pickupPrefab, parent);
            }

            go.name = name;
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
        }

        private static void CreateResourceNode(Transform parent, string name, GameObject lootPrefab, Material material, Vector3 localPosition, float health, int lootCount)
        {
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = name;
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);

            if (root.TryGetComponent(out Renderer renderer))
            {
                renderer.sharedMaterial = material;
            }

            ResourceNode node = root.AddComponent<ResourceNode>();
            SerializedObject so = new SerializedObject(node);
            so.FindProperty("lootPrefab").objectReferenceValue = lootPrefab;
            so.FindProperty("lootCount").intValue = lootCount;
            so.FindProperty("lootLifetime").floatValue = 90f;
            so.FindProperty("maxHealth").floatValue = health;
            so.FindProperty("scatterRadius").floatValue = 0.22f;
            so.FindProperty("scatterForce").floatValue = 1.4f;
            so.FindProperty("upwardBias").floatValue = 0.7f;
            so.FindProperty("autoGenerateId").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ValidatePickup(Transform root, string relativePath, ref int errors)
        {
            Transform target = root.Find(relativePath);
            if (target == null)
            {
                Debug.LogError("[ResourceWorldBootstrap] Missing pickup: " + relativePath);
                errors++;
                return;
            }

            if (!target.TryGetComponent(out PickupItem pickup) || pickup.ItemData == null)
            {
                Debug.LogError("[ResourceWorldBootstrap] Pickup has no item data: " + relativePath, target.gameObject);
                errors++;
            }
        }

        private static void ValidateNode(Transform root, string relativePath, ref int errors)
        {
            Transform target = root.Find(relativePath);
            if (target == null)
            {
                Debug.LogError("[ResourceWorldBootstrap] Missing resource node: " + relativePath);
                errors++;
                return;
            }

            if (!target.TryGetComponent(out ResourceNode node))
            {
                Debug.LogError("[ResourceWorldBootstrap] Resource node missing ResourceNode component: " + relativePath, target.gameObject);
                errors++;
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string[] split = folderPath.Split('/');
            string current = split[0];
            for (int i = 1; i < split.Length; i++)
            {
                string next = current + "/" + split[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, split[i]);
                }

                current = next;
            }
        }
    }
}
