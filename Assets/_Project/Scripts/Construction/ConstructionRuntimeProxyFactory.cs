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
        private const int MaxRuntimeGhostSockets = 6;
        private static Material s_validGhostMaterial;
        private static Material s_invalidGhostMaterial;
        private static Material s_finalProxyMaterial;
        private static Mesh s_wireBoxMesh;
        private static int s_socketLayer = int.MinValue;
        private static GameObject s_ghostProxyRoot;
        private static Transform s_ghostProxyTransform;
        private static Transform s_ghostProxyVisual;
        private static ModuleMarker s_ghostProxyMarker;
        private static BoxCollider s_ghostProxyCollider;
        private static PlacementGhost s_ghostPlacementGhost;
        private static MeshFilter s_ghostProxyMeshFilter;
        private static MeshRenderer s_ghostProxyRenderer;
        // COLD ALLOC: ModuleSocket[6] - fixed reusable socket slots for generated ghost proxy - owner: ConstructionRuntimeProxyFactory
        private static readonly ModuleSocket[] s_ghostSockets = new ModuleSocket[MaxRuntimeGhostSockets];
        // COLD ALLOC: Transform[6] - fixed reusable socket transforms for generated ghost proxy - owner: ConstructionRuntimeProxyFactory
        private static readonly Transform[] s_ghostSocketTransforms = new Transform[MaxRuntimeGhostSockets];
        // COLD ALLOC: SphereCollider[6] - fixed reusable socket trigger colliders for generated ghost proxy - owner: ConstructionRuntimeProxyFactory
        private static readonly SphereCollider[] s_ghostSocketColliders = new SphereCollider[MaxRuntimeGhostSockets];
        // COLD ALLOC: Vector3[8] - shared unit wire box vertices for generated module proxies - owner: ConstructionRuntimeProxyFactory
        private static readonly Vector3[] s_wireBoxVertices =
        {
            new Vector3(-0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, 0.5f),
            new Vector3(-0.5f, 0.5f, 0.5f)
        };

        // COLD ALLOC: int[24] - shared unit wire box edge indices for generated module proxies - owner: ConstructionRuntimeProxyFactory
        private static readonly int[] s_wireBoxIndices =
        {
            0, 1, 1, 2, 2, 3, 3, 0,
            4, 5, 5, 6, 6, 7, 7, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        internal static bool TryCreateGhostProxy(BuildableData data, Vector3 position, Quaternion rotation, LayerMask blockingMask, out GameObject proxyRoot)
        {
            return TryAcquireGhostProxy(data, position, rotation, blockingMask, out proxyRoot);
        }

        internal static bool TryAcquireGhostProxy(BuildableData data, Vector3 position, Quaternion rotation, LayerMask blockingMask, out GameObject proxyRoot)
        {
            proxyRoot = null;
            if (data == null || data.ModuleTemplate == null)
                return false;

            EnsureSharedMaterials();
            EnsureReusableGhostProxy();
            if (s_ghostProxyRoot == null)
                return false;

            BaseModuleTemplate template = data.ModuleTemplate;
            s_ghostProxyTransform.SetPositionAndRotation(position, rotation);
            s_ghostProxyMarker.Initialize(data);
            s_ghostProxyCollider.center = template.ProxyBoundsCenter;
            s_ghostProxyCollider.size = template.ProxyBoundsSize;
            s_ghostProxyVisual.localPosition = template.ProxyBoundsCenter;
            s_ghostProxyVisual.localRotation = Quaternion.identity;
            s_ghostProxyVisual.localScale = template.ProxyBoundsSize;
            s_ghostProxyMeshFilter.sharedMesh = EnsureWireBoxMesh();
            s_ghostProxyRenderer.sharedMaterial = s_validGhostMaterial;

            ConfigureReusableGhostSockets(template.SocketDefinitions);
            s_ghostPlacementGhost.ConfigureRuntimeProxy(
                s_validGhostMaterial,
                s_invalidGhostMaterial,
                template.ProxyBoundsSize * 0.5f,
                template.ProxyBoundsCenter,
                blockingMask);
            s_ghostPlacementGhost.OnSpawn();
            s_ghostProxyRoot.SetActive(true);
            proxyRoot = s_ghostProxyRoot;
            return true;
        }

        internal static bool ReleaseGhostProxy(GameObject proxyRoot)
        {
            if (s_ghostProxyRoot == null || s_ghostPlacementGhost == null || !ReferenceEquals(proxyRoot, s_ghostProxyRoot))
                return false;

            s_ghostPlacementGhost.OnDespawn();
            for (int i = 0; i < MaxRuntimeGhostSockets; i++)
            {
                ModuleSocket socket = s_ghostSockets[i];
                if (socket != null)
                    socket.SetOccupied(false);
            }

            s_ghostProxyRoot.SetActive(false);
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

        internal static bool TryGetGhostProjectionResources(
            out Mesh projectionMesh,
            out Material validMaterial,
            out Material blockedMaterial)
        {
            EnsureSharedMaterials();
            projectionMesh = EnsureWireBoxMesh();
            validMaterial = s_validGhostMaterial;
            blockedMaterial = s_invalidGhostMaterial;
            return projectionMesh != null && validMaterial != null && blockedMaterial != null;
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

        private static void EnsureReusableGhostProxy()
        {
            if (s_ghostProxyRoot != null)
                return;

            EnsureSocketLayer();
            GameObject root = new GameObject("H8_RuntimeGhostProxy");
            root.SetActive(false);
            s_ghostProxyRoot = root;
            s_ghostProxyTransform = root.transform;

            ConstructionRuntimeProxyTag proxyTag = root.AddComponent<ConstructionRuntimeProxyTag>();
            proxyTag.Configure(true);

            s_ghostProxyMarker = root.AddComponent<ModuleMarker>();
            s_ghostProxyCollider = root.AddComponent<BoxCollider>();
            s_ghostProxyCollider.isTrigger = true;

            GameObject visual = new GameObject("ProxyVisual");
            s_ghostProxyVisual = visual.transform;
            s_ghostProxyVisual.SetParent(s_ghostProxyTransform, false);
            s_ghostProxyVisual.localPosition = Vector3.zero;
            s_ghostProxyVisual.localRotation = Quaternion.identity;
            s_ghostProxyVisual.localScale = Vector3.one;
            s_ghostProxyMeshFilter = visual.AddComponent<MeshFilter>();
            s_ghostProxyMeshFilter.sharedMesh = EnsureWireBoxMesh();
            s_ghostProxyRenderer = visual.AddComponent<MeshRenderer>();
            s_ghostProxyRenderer.sharedMaterial = s_validGhostMaterial;

            for (int i = 0; i < MaxRuntimeGhostSockets; i++)
            {
                GameObject socketObject = new GameObject(ResolveSocketSlotName(i));
                socketObject.SetActive(false);
                Transform socketTransform = socketObject.transform;
                socketTransform.SetParent(s_ghostProxyTransform, false);
                socketTransform.localPosition = Vector3.zero;
                socketTransform.localRotation = Quaternion.identity;
                if (s_socketLayer >= 0)
                    socketObject.layer = s_socketLayer;

                ModuleSocket socket = socketObject.AddComponent<ModuleSocket>();
                socket.ConfigureRuntime(string.Empty, ModuleSocketDirection.North);
                SphereCollider socketCollider = socketObject.AddComponent<SphereCollider>();
                socketCollider.radius = DefaultSocketTriggerRadius;
                socketCollider.isTrigger = true;

                s_ghostSockets[i] = socket;
                s_ghostSocketTransforms[i] = socketTransform;
                s_ghostSocketColliders[i] = socketCollider;
            }

            s_ghostPlacementGhost = root.AddComponent<PlacementGhost>();
        }

        private static void ConfigureReusableGhostSockets(BaseModuleTemplate.SocketDefinition[] socketDefinitions)
        {
            int definitionCount = socketDefinitions != null ? socketDefinitions.Length : 0;
            int activeCount = Mathf.Min(definitionCount, MaxRuntimeGhostSockets);
            for (int i = 0; i < MaxRuntimeGhostSockets; i++)
            {
                GameObject socketObject = s_ghostSocketTransforms[i].gameObject;
                if (i >= activeCount)
                {
                    s_ghostSockets[i].SetOccupied(false);
                    socketObject.SetActive(false);
                    continue;
                }

                BaseModuleTemplate.SocketDefinition definition = socketDefinitions[i];
                Transform socketTransform = s_ghostSocketTransforms[i];
                socketTransform.localPosition = definition.LocalPosition;
                socketTransform.localRotation = ModuleSocketTopology.RotationFromDirection(definition.Direction);
                s_ghostSockets[i].ConfigureRuntime(definition.CompatibleType, definition.Direction);
                s_ghostSocketColliders[i].radius = DefaultSocketTriggerRadius;
                socketObject.SetActive(true);
            }
        }

        private static string ResolveSocketSlotName(int index)
        {
            switch (index)
            {
                case 0: return "Socket_0";
                case 1: return "Socket_1";
                case 2: return "Socket_2";
                case 3: return "Socket_3";
                case 4: return "Socket_4";
                default: return "Socket_5";
            }
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

            s_wireBoxMesh.SetVertices(s_wireBoxVertices);
            s_wireBoxMesh.SetIndices(
                s_wireBoxIndices,
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
