#if UNITY_EDITOR

using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only repair commands for the bounded screen-space light shaft runtime.
    /// </summary>
    internal static class ScreenSpaceLightShaftPrefabRepair
    {
        private const string RuntimeObjectName = "H8_ScreenSpaceLightShaftRuntime";
        private const string RuntimeTypeName = "Hecton8.Lighting.Shafts.ScreenSpaceLightShaftRuntime";
        private const string SourceTypeName = "Hecton8.Lighting.Shafts.ScreenSpaceLightShaftSource";

        [MenuItem("Tools/Hecton/Lighting/Ensure Screen Space Light Shaft Runtime")]
        private static void EnsureRuntime()
        {
            Type runtimeType = ResolveLightShaftType(RuntimeTypeName);
            if (runtimeType == null || HasComponentInLoadedScenes(runtimeType))
                return;

            GameObject runtimeObject = new GameObject(RuntimeObjectName);
            Undo.RegisterCreatedObjectUndo(runtimeObject, "Create screen-space light shaft runtime");
            runtimeObject.AddComponent(runtimeType);
            EditorSceneManager.MarkSceneDirty(runtimeObject.scene);
        }

        [MenuItem("Tools/Hecton/Lighting/Add Light Shaft Source To Selected Lights")]
        private static void AddSourceToSelectedLights()
        {
            Type sourceType = ResolveLightShaftType(SourceTypeName);
            if (sourceType == null)
                return;

            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject target = selected[i];
                if (target == null ||
                    !target.TryGetComponent(out Light _) ||
                    target.GetComponent(sourceType) != null)
                {
                    continue;
                }

                Undo.AddComponent(target, sourceType);
                EditorSceneManager.MarkSceneDirty(target.scene);
            }
        }

        [MenuItem("Tools/Hecton/Lighting/Add Light Shaft Source To Selected Lights", true)]
        private static bool CanAddSourceToSelectedLights()
        {
            Type sourceType = ResolveLightShaftType(SourceTypeName);
            if (sourceType == null)
                return false;

            GameObject[] selected = Selection.gameObjects;
            for (int i = 0; i < selected.Length; i++)
            {
                GameObject target = selected[i];
                if (target != null &&
                    target.TryGetComponent(out Light _) &&
                    target.GetComponent(sourceType) == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static Type ResolveLightShaftType(string fullName)
        {
            foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            {
                if (type != null && string.Equals(type.FullName, fullName, StringComparison.Ordinal))
                    return type;
            }

            return null;
        }

        private static bool HasComponentInLoadedScenes(Type componentType)
        {
            MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType() == componentType)
                    return true;
            }

            return false;
        }
    }
}

#endif
