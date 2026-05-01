using Hecton8.Building;
using Hecton8.Gameplay;
using Hecton8.Power;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Construction
{
    [DisallowMultipleComponent]
    internal sealed class ConstructionRuntimeProxyTag : MonoBehaviour
    {
        [SerializeField] private bool isGhostProxy;

        internal bool IsGhostProxy => isGhostProxy;

        internal void Configure(bool ghostProxy)
        {
            isGhostProxy = ghostProxy;
        }
    }

    internal static class ConstructionRuntimeProxyFactory
    {
        private const float DefaultSocketTriggerRadius = 0.15f;
        private static Material s_validGhostMaterial;
        private static Material s_invalidGhostMaterial;
        private static Material s_finalProxyMaterial;
        private static Mesh s_wireBoxMesh;
        private static int s_socketLayer = int.MinValue;

        internal static bool TryCreateGhostProxy(BuildableData data, Vector3 position, Quaternion rotation, LayerMask blockingMask, out GameObject proxyRoot)
        {
            proxyRoot = null;
            if (data == null || data.ModuleTemplate == null)
                return false;

            EnsureSharedMaterials();
            proxyRoot = CreateProxyRoot(data, position, rotation, true);
            if (proxyRoot == null)
                return false;

            PlacementGhost ghost = proxyRoot.AddComponent<PlacementGhost>();
            ghost.ConfigureRuntimeProxy(
                s_validGhostMaterial,
                s_invalidGhostMaterial,
                data.ModuleTemplate.ProxyBoundsSize * 0.5f,
                data.ModuleTemplate.ProxyBoundsCenter,
                blockingMask);
            return true;
        }

        internal static bool TryCreatePlacedProxy(BuildableData data, Vector3 position, Quaternion rotation, out GameObject proxyRoot)
        {
            proxyRoot = null;
            if (data == null || data.ModuleTemplate == null)
                return false;

            EnsureSharedMaterials();
            proxyRoot = CreateProxyRoot(data, position, rotation, false);
            if (proxyRoot == null)
                return false;

            proxyRoot.AddComponent<PowerNode>();
            BaseModule baseModule = proxyRoot.AddComponent<BaseModule>();
            BoxCollider interiorTrigger = CreateInteriorTrigger(proxyRoot.transform, data.ModuleTemplate);
            baseModule.ApplyBuildableTemplate(data, interiorTrigger);
            return true;
        }

        private static GameObject CreateProxyRoot(BuildableData data, Vector3 position, Quaternion rotation, bool ghostProxy)
        {
            BaseModuleTemplate template = data.ModuleTemplate;
            if (template == null)
                return null;

            GameObject root = new GameObject(data.moduleName + (ghostProxy ? "_GhostProxy" : "_Proxy"));
            root.transform.SetPositionAndRotation(position, rotation);

            ConstructionRuntimeProxyTag proxyTag = root.AddComponent<ConstructionRuntimeProxyTag>();
            proxyTag.Configure(ghostProxy);

            ModuleMarker marker = root.AddComponent<ModuleMarker>();
            marker.Initialize(data);

            BoxCollider structuralCollider = root.AddComponent<BoxCollider>();
            structuralCollider.center = template.ProxyBoundsCenter;
            structuralCollider.size = template.ProxyBoundsSize;
            structuralCollider.isTrigger = ghostProxy;

            GameObject visual = new GameObject("ProxyVisual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = template.ProxyBoundsCenter;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = template.ProxyBoundsSize;

            MeshFilter meshFilter = visual.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = EnsureWireBoxMesh();

            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ghostProxy ? s_validGhostMaterial : s_finalProxyMaterial;

            CreateSockets(root.transform, template.SocketDefinitions);
            return root;
        }

        private static BoxCollider CreateInteriorTrigger(Transform root, BaseModuleTemplate template)
        {
            GameObject triggerObject = new GameObject("InteriorTrigger");
            triggerObject.transform.SetParent(root, false);
            triggerObject.transform.localPosition = template.ProxyBoundsCenter;
            triggerObject.transform.localRotation = Quaternion.identity;

            BoxCollider trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.center = Vector3.zero;
            trigger.size = Vector3.Max(template.ProxyBoundsSize - Vector3.one * 0.2f, Vector3.one * 0.5f);
            trigger.isTrigger = true;
            return trigger;
        }

        private static void CreateSockets(Transform root, BaseModuleTemplate.SocketDefinition[] socketDefinitions)
        {
            if (socketDefinitions == null || socketDefinitions.Length == 0)
                return;

            EnsureSocketLayer();
            for (int i = 0; i < socketDefinitions.Length; i++)
            {
                BaseModuleTemplate.SocketDefinition definition = socketDefinitions[i];
                GameObject socketObject = new GameObject("Socket_" + definition.Direction);
                socketObject.transform.SetParent(root, false);
                socketObject.transform.localPosition = definition.LocalPosition;
                socketObject.transform.localRotation = ModuleSocketTopology.RotationFromDirection(definition.Direction);

                if (s_socketLayer >= 0)
                    socketObject.layer = s_socketLayer;

                ModuleSocket socket = socketObject.AddComponent<ModuleSocket>();
                socket.ConfigureRuntime(definition.CompatibleType, definition.Direction);

                SphereCollider socketCollider = socketObject.AddComponent<SphereCollider>();
                socketCollider.radius = DefaultSocketTriggerRadius;
                socketCollider.isTrigger = true;
            }
        }

        private static void EnsureSocketLayer()
        {
            if (s_socketLayer != int.MinValue)
                return;

            s_socketLayer = Hecton8.Core.HectonLayerMasks.Sockets;
        }

        private static void EnsureSharedMaterials()
        {
            if (s_validGhostMaterial == null)
                s_validGhostMaterial = CreateUnlitColorMaterial(Color.white);

            if (s_invalidGhostMaterial == null)
                s_invalidGhostMaterial = CreateUnlitColorMaterial(new Color(1f, 0.18f, 0.12f, 1f));

            if (s_finalProxyMaterial == null)
                s_finalProxyMaterial = CreateUnlitColorMaterial(Color.white);
        }

        private static Mesh EnsureWireBoxMesh()
        {
            if (s_wireBoxMesh != null)
                return s_wireBoxMesh;

            // COLD ALLOC: Mesh[1] — shared unit wire box for generated module proxies — owner: ConstructionRuntimeProxyFactory
            s_wireBoxMesh = new Mesh
            {
                name = "H8_RuntimeModuleWireBox"
            };

            // COLD ALLOC: Vector3[8] — shared unit wire box vertices for generated module proxies — owner: ConstructionRuntimeProxyFactory
            s_wireBoxMesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f, -0.5f),
                new Vector3( 0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f, -0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f, -0.5f),
                new Vector3( 0.5f,  0.5f,  0.5f),
                new Vector3(-0.5f,  0.5f,  0.5f)
            };
            s_wireBoxMesh.SetIndices(
                // COLD ALLOC: int[24] — shared unit wire box edge indices for generated module proxies — owner: ConstructionRuntimeProxyFactory
                new[]
                {
                    0, 1, 1, 2, 2, 3, 3, 0,
                    4, 5, 5, 6, 6, 7, 7, 4,
                    0, 4, 1, 5, 2, 6, 3, 7
                },
                MeshTopology.Lines,
                0);
            s_wireBoxMesh.RecalculateBounds();
            return s_wireBoxMesh;
        }

        private static Material CreateUnlitColorMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            Material material = new Material(shader);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);

            material.enableInstancing = true;
            return material;
        }
    }
}
