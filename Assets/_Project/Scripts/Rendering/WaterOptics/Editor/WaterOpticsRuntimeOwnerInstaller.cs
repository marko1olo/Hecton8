#if UNITY_EDITOR
using System;
using Hecton8.Rendering.WaterOptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    /// <summary>
    /// Installs WaterOpticsRuntime on the bootstrap scene host.
    /// Batchmode-safe: no DisplayDialog; soft FAIL when busy; saveScene=false in batch
    /// so CI does not mutate 00_BOOTSTRAP.unity on disk.
    /// </summary>
    public static class WaterOpticsRuntimeOwnerInstaller
    {
        public const string MenuPath = "Hecton8/Rendering/Water Optics/Install Runtime Owner In Bootstrap Scene";
        public const string BootstrapScenePath = "Assets/_Project/Scenes/00_BOOTSTRAP.unity";
        private const string PreferredBootstrapRootName = "[BOOTSTRAPPER]";

        [MenuItem(MenuPath)]
        public static void InstallRuntimeOwnerInBootstrapScene()
        {
            bool batch = Application.isBatchMode;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating || Application.isPlaying)
            {
                Debug.LogError(
                    "[WaterOpticsRuntimeOwnerInstaller] RESULT: FAIL — Editor busy (compiling/updating/playing).");
                if (!batch)
                {
                    EditorUtility.DisplayDialog(
                        "Water Optics Runtime Owner",
                        "Editor is compiling, updating, or playing. Install the runtime owner after the editor is idle.",
                        "OK");
                }

                return;
            }

            // Batch: never save scene — prove open/find/attach path without committing scene dirt.
            bool saveScene = !batch;
            if (!TryInstallRuntimeOwnerInBootstrapScene(saveScene, out string failure, out bool changed))
            {
                Debug.LogError(
                    "[WaterOpticsRuntimeOwnerInstaller] RESULT: FAIL — " + failure);
                if (!batch)
                    EditorUtility.DisplayDialog("Water Optics Runtime Owner", failure, "OK");
                return;
            }

            string detail = changed
                ? (batch
                    ? "WaterOpticsRuntime would attach (batch: scene not saved)."
                    : "WaterOpticsRuntime owner was attached to the bootstrap scene.")
                : "WaterOpticsRuntime owner is already authored in the bootstrap scene.";

            Debug.Log("[WaterOpticsRuntimeOwnerInstaller] RESULT: PASS — " + detail);
            if (!batch)
                EditorUtility.DisplayDialog("Water Optics Runtime Owner", detail, "OK");
        }

        public static bool TryInstallRuntimeOwnerInBootstrapScene(
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

            WaterOpticsRuntime[] runtimes = UnityEngine.Object.FindObjectsByType<WaterOpticsRuntime>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < runtimes.Length; i++)
            {
                if (runtimes[i].gameObject.scene == scene)
                {
                    runtime = runtimes[i];
                    return true;
                }
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
