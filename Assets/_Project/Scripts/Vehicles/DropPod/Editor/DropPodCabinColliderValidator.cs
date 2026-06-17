#if UNITY_EDITOR
namespace Hecton8.Vehicles.DropPod.Editor
{
    using System.Collections.Generic;
    using Hecton8.Vehicles.DropPod;
    using UnityEditor;
    using UnityEngine;

    public static class DropPodCabinColliderValidator
    {
        // COLD ALLOC: List<MeshCollider>[16] - editor-only cabin collider validation scratch - owner: DropPodCabinColliderValidator
        private static readonly List<MeshCollider> s_meshColliderScratch = new List<MeshCollider>(16);

        [MenuItem("Hecton8/Drop Pod/Validate Cabin Colliders")]
        public static void ValidateSelection()
        {
            GameObject root = Selection.activeGameObject;
            if (root == null)
            {
                Debug.LogWarning("[DropPodCabinColliderValidator] Select a drop pod root.");
                return;
            }

            int meshColliderCount = CountMeshColliders(root);
            if (meshColliderCount > 0)
            {
                Debug.LogError("[DropPodCabinColliderValidator] MeshCollider is forbidden inside drop pod cabin. Count=" + meshColliderCount);
                return;
            }

            Debug.Log("[DropPodCabinColliderValidator] PASS primitive/compound collider cabin route.");
        }

        public static int CountMeshColliders(GameObject root)
        {
            if (root == null)
                return 0;

            s_meshColliderScratch.Clear();
            try
            {
                root.GetComponentsInChildren(true, s_meshColliderScratch);
                return s_meshColliderScratch.Count;
            }
            finally
            {
                s_meshColliderScratch.Clear();
            }
        }
    }
}
#endif
