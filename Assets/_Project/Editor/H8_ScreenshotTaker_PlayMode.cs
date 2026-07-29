using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Hecton8.Tools;

namespace Hecton8.EditorTools
{
    public static class H8_ScreenshotTaker_PlayMode
    {
        /// <summary>
        /// Inactive-inclusive single-component lookup. <see cref="Object.FindAnyObjectByType{T}()"/> takes
        /// <see cref="FindObjectsInactive.Exclude"/> by default, which is the wrong default in this scene.
        /// </summary>
        private static T FirstOrDefaultIncludingInactive<T>() where T : Component
        {
            T[] found = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return found != null && found.Length > 0 ? found[0] : null;
        }

        public static void TakeScreenshotAndExit()
        {
            string scenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            
            Debug.Log($"[H8ScreenshotPlayMode] Opening scene: {scenePath}");
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            FixCelestialAndLight();

            EnsureSinglePlayModeScreenshotter();

            Debug.Log("[H8ScreenshotPlayMode] Entering Play Mode...");
            EditorApplication.isPlaying = true;
        }

        /// <summary>
        /// Adds the play-mode screenshotter ONLY if the scene does not already carry one.
        ///
        /// This method exists because the unconditional two-line injection it replaces authored NINE copies
        /// into 02_HECTON_WORLD. Logs/omega_rootaudit3.log lists H8_PlayModeScreenshotter as a scene root
        /// nine times, each with its component. Every field of that class is per-instance, so nine copies
        /// meant nine player searches, nine 20-second settle timers, nine captures and nine capture
        /// directories - 16.0 MiB on disk in the last run to hold two distinct frames, verified by md5.
        ///
        /// HOW NINE ACCUMULATED: there was no existence check of any kind here, and the object is a plain
        /// serializable scene root in a scene this method opens Single and dirties. The old comment claimed
        /// the injection lived "in the unsaved editor scene state only" - true only while nothing saves, and
        /// six unconditional EditorSceneManager.SaveScene callers open this scene. Each inject-then-save
        /// cycle cemented one more.
        ///
        /// FindObjectsInactive.Include, not the default Exclude: DEPRECATED_STUFF holds 1457 objects with
        /// activeSelf=0, so an inactive copy must count as present or this check would keep adding roots that
        /// already exist - which is the exact defect class already fixed in three authoring tools.
        ///
        /// DELIBERATELY NOT HideFlags.HideAndDontSave, despite that being the obvious-looking fix and what a
        /// sibling tool (H8_RouteCaptureStation) does for its own temporary objects. Entering play mode
        /// reloads the scene from its serialized state, and a DontSave object is excluded from that state -
        /// so flagging it would destroy the screenshotter during the very transition it is injected for. The
        /// existence check alone stops the duplication, and it cannot break the capture.
        /// </summary>
        private static void EnsureSinglePlayModeScreenshotter()
        {
            H8_PlayModeScreenshotter[] existing = Object.FindObjectsByType<H8_PlayModeScreenshotter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            if (existing != null && existing.Length > 0)
            {
                Debug.Log(
                    "[H8ScreenshotPlayMode] Reusing the existing H8_PlayModeScreenshotter (" +
                    existing.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    " already in the scene). Not adding another.");
                return;
            }

            var go = new GameObject("H8_PlayModeScreenshotter");
            go.AddComponent<H8_PlayModeScreenshotter>();
            Debug.Log("[H8ScreenshotPlayMode] Injected one H8_PlayModeScreenshotter.");
        }

        private static void FixCelestialAndLight()
        {
            // FindObjectsInactive.Include on both lookups below, matching the Light search further down which
            // already got this right. With the default Exclude, a copy buried inside the disabled
            // DEPRECATED_STUFF root (1457 objects, activeSelf=0) reads as ABSENT and this method adds a
            // duplicate beside it. Currently latent - the root audit shows exactly one active root for each -
            // but it is the same defect class already fixed in three authoring tools tonight, and it sits
            // twelve lines above a call that does it correctly.
            var celestial = FirstOrDefaultIncludingInactive<Hecton8.Celestial.HectonCelestialEngine>();
            if (celestial == null)
            {
                var go = new GameObject("HectonCelestialEngine");
                celestial = go.AddComponent<Hecton8.Celestial.HectonCelestialEngine>();
                Debug.Log("[H8ScreenshotPlayMode] Added missing CelestialEngine.");
            }

            var orch = FirstOrDefaultIncludingInactive<Hecton8.Graphics.HectonVisualsOrchestrator>();
            if (orch == null)
            {
                var go = new GameObject("HectonVisualsOrchestrator");
                orch = go.AddComponent<Hecton8.Graphics.HectonVisualsOrchestrator>();
            }

            var orchSO = new SerializedObject(orch);
            orchSO.FindProperty("_celestialEngine").objectReferenceValue = celestial;
            orchSO.FindProperty("_oceanMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Crest/Crest/Materials/Ocean.mat");
            orchSO.ApplyModifiedProperties();

            Light sunLight = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (l.type == LightType.Directional)
                {
                    sunLight = l;
                    l.shadows = LightShadows.Soft;
                    break;
                }
            }

            if (sunLight == null)
            {
                var sunGO = new GameObject("Directional Light (Sun)");
                sunLight = sunGO.AddComponent<Light>();
                sunLight.type = LightType.Directional;
            }
            sunLight.shadows = LightShadows.Soft;
            sunLight.intensity = 1.5f;
            sunLight.color = Color.white;
            sunLight.transform.rotation = Quaternion.Euler(45, -30, 0);

            var celSO = new SerializedObject(celestial);
            celSO.FindProperty("sunLight").objectReferenceValue = sunLight;
            celSO.FindProperty("aegirFallbackMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Sky/Hecton_AegirSky_Mat.mat");
            celSO.ApplyModifiedProperties();
            
        }
    }
}
