// ============================================================================
// HECTON-8 - ToolStagingSpawner.cs
// Editor-side authoring helper that lays out tool world prefabs in a rack.
// Does not affect player runtime loadout.
// ============================================================================

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Tool Staging Spawner")]
    public sealed class ToolStagingSpawner : MonoBehaviour
    {
        internal static ToolStagingSpawner ActiveAuthoringInstance { get; private set; }
        [SerializeField] private Vector3 startLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 cellSpacing = new Vector3(1.35f, 0f, 1.35f);
        [SerializeField] private int columns = 4;
        [SerializeField] private bool rebuildOnReset = true;

#if UNITY_EDITOR
        private static readonly string[] ToolWorldPrefabPaths =
        {
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Scanner_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Repair_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Builder_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_LaserCutter_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Flashlight_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Propulsion_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_SalvageSampler_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_BeaconDeployer_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_EnvAnalyzer_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_Knife_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_StunPistol_World.prefab",
            "Assets/_Project/Prefabs/Items/Tools/Item_Tool_HarpoonLauncher_World.prefab",
        };

        [System.NonSerialized] private bool _rebuildQueued;

        private void Reset()
        {
            ActiveAuthoringInstance = this;
            if (!rebuildOnReset)
                return;

            QueueRebuildAfterReset();
        }

        private void OnValidate()
        {
            ActiveAuthoringInstance = this;
            if (!rebuildOnReset || !IsEditorRebuildSafe())
                return;

            if (transform.childCount == 0)
                QueueRebuildAfterReset();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveAuthoringInstance, this))
                ActiveAuthoringInstance = null;
            EditorApplication.delayCall -= TryRebuildAfterReset;
            _rebuildQueued = false;
        }

        [ContextMenu("Rebuild Tool Staging")]
        private void RebuildToolStaging()
        {
            if (Application.isPlaying)
                return;

            RebuildInternal();
        }

        [MenuItem("Hecton8/Dev/Rebuild Tool Staging")]
        private static void RebuildToolStagingFromMenu()
        {
            ToolStagingSpawner spawner = ActiveAuthoringInstance;
            if (spawner == null || Application.isPlaying)
                return;

            spawner.RebuildInternal();
        }

        private void TryRebuildAfterReset()
        {
            _rebuildQueued = false;

            if (this == null || gameObject == null || !IsEditorRebuildSafe())
                return;

            RebuildInternal();
        }

        private void QueueRebuildAfterReset()
        {
            if (_rebuildQueued || !IsEditorRebuildSafe())
                return;

            _rebuildQueued = true;
            EditorApplication.delayCall -= TryRebuildAfterReset;
            EditorApplication.delayCall += TryRebuildAfterReset;
        }

        private static bool IsEditorRebuildSafe()
        {
            return !Application.isPlaying &&
                   !EditorApplication.isCompiling &&
                   !EditorApplication.isUpdating &&
                   !EditorApplication.isPlayingOrWillChangePlaymode;
        }

        private void RebuildInternal()
        {
            Undo.RegisterFullObjectHierarchyUndo(gameObject, "Rebuild Tool Staging");
            ClearChildren();

            int safeColumns = Mathf.Max(1, columns);

            for (int i = 0; i < ToolWorldPrefabPaths.Length; i++)
            {
                string path = ToolWorldPrefabPaths[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[ToolStagingSpawner] Missing world prefab at '{path}'.", this);
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, gameObject.scene);
                if (instance == null)
                    continue;

                Undo.RegisterCreatedObjectUndo(instance, "Spawn Tool Staging Item");

                Transform child = instance.transform;
                child.SetParent(transform, false);

                int row = i / safeColumns;
                int col = i % safeColumns;
                child.localPosition = startLocalOffset + new Vector3(col * cellSpacing.x, -row * Mathf.Abs(cellSpacing.y), row * cellSpacing.z);
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }

        private void ClearChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child != null)
                    Undo.DestroyObjectImmediate(child.gameObject);
            }
        }
#endif
    }
}
