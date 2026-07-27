using System;
using Hecton8.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Idempotent authoring for the one piece of the music system that nothing can install itself.
    ///
    /// HectonMusicDirector self-installs at [RuntimeInitializeOnLoadMethod(AfterSceneLoad)], but it can
    /// only build itself from a HectonMusicDirectorConfig, and the only runtime route to that config is
    /// a hand-placed HectonMusicDirectorAnchor in the scene. Resources.Load is forbidden project-wide
    /// (see ApexIntegratorSourceGuard), so there is deliberately no code path that finds the config on
    /// its own. That makes the anchor a gateway: place it and the subsystem works, omit it and the
    /// subsystem is silently absent - silently, because EnsureRuntimeInstance passes
    /// reportMissingConfig:false so that render sandboxes are not spammed with errors.
    ///
    /// Silence is the right default for a sandbox and the wrong default for a shipping scene, and
    /// nothing in the project distinguished the two. This does: the scene list below is the declared
    /// set of scenes that present audio to the player, and running this makes them all match it.
    ///
    /// Re-runnable by design. It adds only what is missing and reports every scene it left alone,
    /// so it can be run again after any new scene is created rather than being a one-time repair.
    /// </summary>
    public static class MusicDirectorAnchorAuthoring
    {
        private const string Marker = "[H8_MUSIC_ANCHOR]";
        private const string GlobalConfigPath =
            "Assets/_Project/Data/Audio/Music/Configs/MusicDirectorConfig_Global.asset";
        private const string AnchorObjectName = "[H8_MUSIC_DIRECTOR_ANCHOR]";

        /// <summary>
        /// Scenes that present music to the player. 00_BOOTSTRAP is deliberately excluded: it is a
        /// transition scene that hands off immediately, and 020_RENDER_SANDBOX* are renderer test beds
        /// where the current silence is intended, not a defect.
        /// </summary>
        private static readonly string[] MusicBearingScenes =
        {
            "Assets/_Project/Scenes/01_MAIN_MENU.unity",
            "Assets/_Project/Scenes/01_ORBIT.unity",
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
        };

        /// <summary>
        /// Reports what would change and writes nothing. Run this first: 02_HECTON_WORLD is a ~6 MB
        /// BINARY scene, and a save there produces a diff nobody can review and integrity nobody can
        /// verify in batchmode. Knowing it is already wired is what makes applying safe.
        /// </summary>
        [MenuItem("Hecton8/Audio/Report Music Director Anchors")]
        public static void ReportAnchors()
        {
            Run(false);
        }

        [MenuItem("Hecton8/Audio/Ensure Music Director Anchors")]
        public static void EnsureAnchors()
        {
            Run(true);
        }

        private static void Run(bool apply)
        {
            var config = AssetDatabase.LoadAssetAtPath<HectonMusicDirectorConfig>(GlobalConfigPath);
            if (config == null)
            {
                Debug.LogError($"{Marker} ABORT - config missing at {GlobalConfigPath}");
                return;
            }

            int added = 0;
            int rewired = 0;
            int untouched = 0;

            foreach (string scenePath in MusicBearingScenes)
            {
                try
                {
                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.IsValid())
                    {
                        Debug.LogError($"{Marker} INVALID {scenePath}");
                        continue;
                    }

                    HectonMusicDirectorAnchor anchor = FindAnchorInScene(scene);
                    bool needsAnchor = anchor == null;
                    bool needsConfig = !needsAnchor && ReadConfig(anchor) == null;

                    if (!apply)
                    {
                        if (needsAnchor)
                            Debug.Log($"{Marker} WOULD ADD anchor + config -> {scenePath}");
                        else if (needsConfig)
                            Debug.Log($"{Marker} WOULD REWIRE null config on existing anchor -> {scenePath}");
                        else
                            Debug.Log($"{Marker} OK already wired -> {scenePath}");

                        if (needsAnchor)
                            added++;
                        else if (needsConfig)
                            rewired++;
                        else
                            untouched++;

                        continue;
                    }

                    bool createdAnchor = false;
                    if (needsAnchor)
                    {
                        var host = new GameObject(AnchorObjectName);
                        SceneManager.MoveGameObjectToScene(host, scene);
                        anchor = host.AddComponent<HectonMusicDirectorAnchor>();
                        createdAnchor = true;
                    }

                    // _config is private; assign through SerializedObject rather than widening the API.
                    var serialized = new SerializedObject(anchor);
                    SerializedProperty configProperty = serialized.FindProperty("_config");
                    bool assignedConfig = false;
                    if (configProperty != null && configProperty.objectReferenceValue == null)
                    {
                        configProperty.objectReferenceValue = config;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        assignedConfig = true;
                    }

                    if (createdAnchor || assignedConfig)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);

                        if (createdAnchor)
                        {
                            added++;
                            Debug.Log($"{Marker} ADDED anchor + config -> {scenePath}");
                        }
                        else
                        {
                            rewired++;
                            Debug.Log($"{Marker} REWIRED existing anchor config -> {scenePath}");
                        }
                    }
                    else
                    {
                        untouched++;
                        Debug.Log($"{Marker} OK already wired -> {scenePath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Marker} FAILED {scenePath}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            string mode = apply ? "APPLIED" : "DRY-RUN";
            Debug.Log($"{Marker} DONE [{mode}] added={added} rewired={rewired} alreadyWired={untouched}");
        }

        private static HectonMusicDirectorConfig ReadConfig(HectonMusicDirectorAnchor anchor)
        {
            var serialized = new SerializedObject(anchor);
            SerializedProperty configProperty = serialized.FindProperty("_config");
            return configProperty?.objectReferenceValue as HectonMusicDirectorConfig;
        }

        private static HectonMusicDirectorAnchor FindAnchorInScene(Scene scene)
        {
            // Root traversal instead of a Find* API: those tokens are on the project's forbidden list.
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var anchor = root.GetComponentInChildren<HectonMusicDirectorAnchor>(true);
                if (anchor != null)
                    return anchor;
            }

            return null;
        }
    }
}
