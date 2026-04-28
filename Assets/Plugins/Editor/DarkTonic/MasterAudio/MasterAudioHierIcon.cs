using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DarkTonic.MasterAudio.EditorScripts
{
    // ReSharper disable once CheckNamespace
    public class MasterAudioHierIcon : MonoBehaviour
    {
        static Texture2D MAicon;
        static Texture2D PCicon;

        static MasterAudioHierIcon()
        {
            EditorApplication.delayCall -= RegisterHierarchyIcons;
            EditorApplication.delayCall += RegisterHierarchyIcons;
        }

        static void RegisterHierarchyIcons()
        {
            EditorApplication.delayCall -= RegisterHierarchyIcons;

            if (InternalEditorUtility.inBatchMode)
            {
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RegisterHierarchyIcons;
                return;
            }

            MAicon = AssetDatabase.LoadAssetAtPath("Assets/Gizmos/MasterAudio/MasterAudio Icon.png", typeof(Texture2D)) as Texture2D;
            PCicon = AssetDatabase.LoadAssetAtPath("Assets/Gizmos/MasterAudio/PlaylistController Icon.png", typeof(Texture2D)) as Texture2D;

            if (MAicon == null)
            {
                return;
            }

            EditorApplication.hierarchyWindowItemByEntityIdOnGUI -= HierarchyItemCB;
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += HierarchyItemCB;
            EditorApplication.RepaintHierarchyWindow();
        }

        // ReSharper disable once InconsistentNaming
        static void HierarchyItemCB(EntityId entityId, Rect selectionRect)
        {
            var masterAudioGameObject = EditorUtility.EntityIdToObject(entityId) as GameObject;

            if (masterAudioGameObject == null)
            {
                return;
            }

            if (MAicon != null && masterAudioGameObject.GetComponent<MasterAudio>() != null)
            {
                var iconRect = new Rect(selectionRect);
                // Always position the hierarchy icon on the right no matter how deep the GameObject is within the hierarchy
                iconRect.x = iconRect.width + (selectionRect.x - 16);
                iconRect.width = 16;
                iconRect.height = 16;
                GUI.DrawTexture(iconRect, MAicon);
            }
            else if (PCicon != null && masterAudioGameObject.GetComponent<PlaylistController>() != null)
            {
                var iconRect = new Rect(selectionRect);
                // Always position the hierarchy icon on the right no matter how deep the GameObject is within the hierarchy
                iconRect.x = iconRect.width + (selectionRect.x - 16);
                iconRect.width = 16;
                iconRect.height = 16;
                GUI.DrawTexture(iconRect, PCicon);
            }
        }
    }
}
