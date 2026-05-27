#if UNITY_EDITOR
using System;
using Hecton8.Rendering.WaterOptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    internal static class WaterOpticsRuntimeOwnerInstaller
    {
        internal const string MenuPath = "Hecton8/Rendering/Water Optics/Install Runtime Owner In Bootstrap Scene";
        internal const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string PreferredBootstrapRootName = "[BOOTSTRAPPER]";

        [MenuItem(MenuPath)]
        internal static void InstallRuntimeOwnerInBootstrapScene()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Water Optics Runtime Owner",
                    "Editor is compiling, updating, or playing. Install the runtime owner after the editor is idle.",
                    "OK");
                return;
            }

            if (!TryInstallRuntimeOwnerInBootstrapScene(saveScene: true, out string failure, out bool changed))
            {
                EditorUtility.DisplayDialog("Water Optics Runtime Owner", failure, "OK");
                return;
            }

            string message = changed
                ? "WaterOpticsRuntime owner was attached to the bootstrap scene."
                : "WaterOpticsRuntime owner is already authored in the bootstrap scene.";

            EditorUtility.DisplayDialog("Water Optics Runtime Owner", message, "OK");
        }

        internal static bool TryInstallRuntimeOwnerInBootstrapScene(
            bool saveScene,
            out string failure,
            out bool changed)
        {
            failure = null;
            changed = false;

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.GetSceneByPath(BootstrapScenePath);
            bool closeAfterInstall = !scene.IsValid() || !scene.isLoaded;
            if (closeAfterInstall)
                scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Additive);

            if (!scene.IsValid() || !scene.isLoaded)
            {
                failure = string.Concat("[13KRA] Unable to open bootstrap scene: ", BootstrapScenePath);
                return false;
            }

            try
            {
                if (TryFindRuntimeOwnerInScene(scene, out _))
                    return true;

                if (!TryResolveOwnerHost(scene, out GameObject host))
                {
                    failure = string.Concat(
                        "[13KRA] Bootstrap scene has no root GameObject host for WaterOpticsRuntime: ",
                        BootstrapScenePath);
                    return false;
                }

                Undo.AddComponent<WaterOpticsRuntime>(host);
                EditorUtility.SetDirty(host);
                EditorSceneManager.MarkSceneDirty(scene);
                changed = true;

                if (saveScene && !EditorSceneManager.SaveScene(scene))
                {
                    failure = string.Concat("[13KRA] Failed to save bootstrap scene: ", BootstrapScenePath);
                    return false;
                }

                return true;
            }
            finally
            {
                if (closeAfterInstall && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);

                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
            }
        }

        private static bool TryFindRuntimeOwnerInScene(Scene scene, out WaterOpticsRuntime runtime)
        {
            runtime = null;
            if (!scene.IsValid() || !scene.isLoaded)
                return false;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (roots[rootIndex].TryGetComponent(out runtime))
                    return true;
            }

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                runtime = roots[rootIndex].GetComponentInChildren<WaterOpticsRuntime>(true);
                if (runtime != null)
                    return true;
            }

            return false;
        }

        private static bool TryResolveOwnerHost(Scene scene, out GameObject host)
        {
            host = null;
            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length == 0)
                return false;

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                if (string.Equals(roots[rootIndex].name, PreferredBootstrapRootName, StringComparison.Ordinal))
                {
                    host = roots[rootIndex];
                    return true;
                }
            }

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string rootName = roots[rootIndex].name;
                if (rootName.IndexOf("BOOTSTRAP", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    rootName.IndexOf("SYSTEM", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    host = roots[rootIndex];
                    return true;
                }
            }

            host = roots[0];
            return true;
        }
    }
}
#endif
