using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Editor
{
    public static class CreatureProxyPrefabAuthoring
    {
        private const string RootFolder = "Assets/_Project/Data/AI/GeneratedProxies";
        private const string PrefabFolder = RootFolder + "/Prefabs";
        private const string MaterialFolder = RootFolder + "/Materials";

        private const string SmallPassiveProxyPath = PrefabFolder + "/SmallPassiveProxy.prefab";
        private const string TerritorialProxyPath = PrefabFolder + "/TerritorialProxy.prefab";
        private const string HunterProxyPath = PrefabFolder + "/HunterProxy.prefab";
        private const string HeavyHunterProxyPath = PrefabFolder + "/HeavyHunterProxy.prefab";
        private const string LeviathanProxyPath = PrefabFolder + "/LeviathanProxy.prefab";
        private const string DroneProxyPath = PrefabFolder + "/DroneProxy.prefab";
        private static readonly int s_baseColorPropertyId = Shader.PropertyToID("_BaseColor");
        private static readonly int s_colorPropertyId = Shader.PropertyToID("_Color");
        private static readonly int s_emissionColorPropertyId = Shader.PropertyToID("_EmissionColor");

        [MenuItem("Hecton8/Authoring/Build AI Creature Proxies", priority = 181)]
        public static void BuildCreatureProxies()
        {
            EnsureProxyAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CreatureProxyPrefabAuthoring] AI proxy prefabs rebuilt.");
        }

        public static void EnsureProxyAssets()
        {
            EnsureFolder("Assets/_Project/Data");
            EnsureFolder("Assets/_Project/Data/AI");
            EnsureFolder(RootFolder);
            EnsureFolder(PrefabFolder);
            EnsureFolder(MaterialFolder);

            Material passiveMaterial = EnsureMaterial("Mat_SmallPassiveProxy.mat", new Color(0.42f, 0.82f, 0.98f));
            Material territorialMaterial = EnsureMaterial("Mat_TerritorialProxy.mat", new Color(0.68f, 0.95f, 0.62f));
            Material hunterMaterial = EnsureMaterial("Mat_HunterProxy.mat", new Color(0.97f, 0.70f, 0.30f));
            Material heavyHunterMaterial = EnsureMaterial("Mat_HeavyHunterProxy.mat", new Color(0.95f, 0.42f, 0.26f));
            Material leviathanMaterial = EnsureMaterial("Mat_LeviathanProxy.mat", new Color(0.75f, 0.22f, 0.22f));
            Material droneMaterial = EnsureMaterial("Mat_DroneProxy.mat", new Color(0.70f, 0.86f, 1.00f));

            EnsureProxyPrefab(SmallPassiveProxyPath, BuildRoot("SmallPassiveProxy", PrimitiveType.Sphere, passiveMaterial, new Vector3(0.8f, 0.45f, 1.2f), AddSmallPassiveCollider, kinematicOnly: true));
            EnsureProxyPrefab(TerritorialProxyPath, BuildRoot("TerritorialProxy", PrimitiveType.Capsule, territorialMaterial, new Vector3(1.2f, 0.9f, 2.0f), AddTerritorialCollider));
            EnsureProxyPrefab(HunterProxyPath, BuildRoot("HunterProxy", PrimitiveType.Capsule, hunterMaterial, new Vector3(1.4f, 1.0f, 2.6f), AddHunterCollider));
            EnsureProxyPrefab(HeavyHunterProxyPath, BuildRoot("HeavyHunterProxy", PrimitiveType.Capsule, heavyHunterMaterial, new Vector3(2.0f, 1.4f, 3.8f), AddHeavyHunterCollider));
            EnsureProxyPrefab(LeviathanProxyPath, BuildRoot("LeviathanProxy", PrimitiveType.Cylinder, leviathanMaterial, new Vector3(3.4f, 1.0f, 12f), AddLeviathanCollider));
            EnsureProxyPrefab(DroneProxyPath, BuildRoot("DroneProxy", PrimitiveType.Cube, droneMaterial, new Vector3(1.2f, 0.8f, 1.4f), AddDroneCollider));
        }

        public static GameObject ResolveDefaultProxyPrefab(CreatureRoleType roleType, float maxHealth, float attackDamage)
        {
            EnsureProxyAssets();

            switch (roleType)
            {
                case CreatureRoleType.Ambient:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(SmallPassiveProxyPath);
                case CreatureRoleType.Territorial:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(TerritorialProxyPath);
                case CreatureRoleType.Hunter:
                    if (maxHealth >= 100f || attackDamage >= 25f)
                        return AssetDatabase.LoadAssetAtPath<GameObject>(HeavyHunterProxyPath);
                    return AssetDatabase.LoadAssetAtPath<GameObject>(HunterProxyPath);
                case CreatureRoleType.Leviathan:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(LeviathanProxyPath);
                case CreatureRoleType.DroneTrader:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(DroneProxyPath);
                default:
                    return AssetDatabase.LoadAssetAtPath<GameObject>(SmallPassiveProxyPath);
            }
        }

        public static bool IsGeneratedProxy(GameObject prefab)
        {
            if (prefab == null)
                return false;

            string path = AssetDatabase.GetAssetPath(prefab);
            return !string.IsNullOrWhiteSpace(path) && path.StartsWith(PrefabFolder, System.StringComparison.OrdinalIgnoreCase);
        }

        private static GameObject BuildRoot(
            string name,
            PrimitiveType visualType,
            Material material,
            Vector3 visualScale,
            System.Action<GameObject> addCollider,
            bool kinematicOnly = false)
        {
            GameObject root = new GameObject(name);
            if (!kinematicOnly)
            {
                Rigidbody rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = false;
                rigidbody.detectCollisions = true;
                rigidbody.linearDamping = 1.2f;
                rigidbody.angularDamping = 4f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            }

            if (!kinematicOnly)
            {
                root.AddComponent<FaunaBrain>();
                root.AddComponent<Hecton8.Gameplay.ScannableTarget>();
            }

            addCollider(root);
            if (kinematicOnly)
            {
                if (root.TryGetComponent(out Collider proxyCollider))
                    proxyCollider.isTrigger = true;
            }

            GameObject visual = GameObject.CreatePrimitive(visualType);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualScale;

            if (visual.TryGetComponent(out Collider visualCollider))
                Object.DestroyImmediate(visualCollider);

            if (visual.TryGetComponent(out MeshRenderer renderer) && material != null)
                renderer.sharedMaterial = material;

            return root;
        }

        private static void AddSmallPassiveCollider(GameObject root)
        {
            SphereCollider collider = root.AddComponent<SphereCollider>();
            collider.radius = 0.55f;
            collider.center = new Vector3(0f, 0f, 0.15f);
        }

        private static void AddTerritorialCollider(GameObject root)
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.65f;
            collider.height = 2.2f;
            collider.center = new Vector3(0f, 0f, 0.2f);
        }

        private static void AddHunterCollider(GameObject root)
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.7f;
            collider.height = 2.8f;
            collider.center = new Vector3(0f, 0f, 0.25f);
        }

        private static void AddHeavyHunterCollider(GameObject root)
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 0.95f;
            collider.height = 4.1f;
            collider.center = new Vector3(0f, 0f, 0.35f);
        }

        private static void AddLeviathanCollider(GameObject root)
        {
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.direction = 2;
            collider.radius = 2.2f;
            collider.height = 13.5f;
            collider.center = new Vector3(0f, 0f, 0.2f);
        }

        private static void AddDroneCollider(GameObject root)
        {
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.4f, 0.8f, 1.6f);
        }

        private static void EnsureProxyPrefab(string assetPath, GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            Object.DestroyImmediate(root);
        }

        private static Material EnsureMaterial(string fileName, Color color)
        {
            string assetPath = $"{MaterialFolder}/{fileName}";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, assetPath);
            }

            if (material.HasProperty(s_baseColorPropertyId))
                material.SetColor(s_baseColorPropertyId, color);
            if (material.HasProperty(s_colorPropertyId))
                material.SetColor(s_colorPropertyId, color);
            if (material.HasProperty(s_emissionColorPropertyId))
                material.SetColor(s_emissionColorPropertyId, color * 0.08f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;

            int slashIndex = path.LastIndexOf('/');
            if (slashIndex <= 0)
                return;

            string parent = path.Substring(0, slashIndex);
            string folderName = path.Substring(slashIndex + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

