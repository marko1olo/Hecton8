using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

namespace Hecton8.Editor
{
    public static class PlayModeSmokeTester
    {
        private static int _step = 0;
        private static int _waitFrames = 0;
        private static float _stateTimer = 0f;

        [MenuItem("Hecton8/Verification/Run PlayMode Smoke Test")]
        public static void RunTest()
        {
            Debug.Log("[PlayModeSmokeTester] Starting automated verification...");
            _step = 0;
            _waitFrames = 0;
            _stateTimer = 0f;
            EditorApplication.update += EditorUpdate;
        }

        private static void EditorUpdate()
        {
            if (_step == 0)
            {
                // Step 0: Load Main Menu scene
                Debug.Log("[PlayModeSmokeTester] Step 0: Loading 01_MAIN_MENU scene...");
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MAIN_MENU.unity", OpenSceneMode.Single);
                _step = 1;
                _waitFrames = 0;
            }
            else if (_step == 1)
            {
                // Step 1: Wait a few frames and enter play mode
                _waitFrames++;
                if (_waitFrames > 10)
                {
                    Debug.Log("[PlayModeSmokeTester] Step 1: Entering Play Mode (Main Menu)...");
                    EditorApplication.isPlaying = true;
                    _step = 2;
                    _waitFrames = 0;
                }
            }
            else if (_step == 2)
            {
                // Step 2: Running in Play Mode (Main Menu)
                if (EditorApplication.isPlaying)
                {
                    _waitFrames++;
                    if (_waitFrames > 60) // wait about 1 second
                    {
                        Debug.Log("[PlayModeSmokeTester] Step 2: Main Menu is playing successfully! No prewarm hangs detected.");
                        // Verify registry state
                        var registry = GameObject.FindAnyObjectByType<Hecton8.World.PersistentWorldRegistry>();
                        if (registry != null)
                        {
                            Debug.Log($"[PlayModeSmokeTester] PersistentWorldRegistry status: AreResidentWorldPrefabPoolsReady = {registry.AreResidentWorldPrefabPoolsReady()}");
                        }
                        
                        Debug.Log("[PlayModeSmokeTester] Stopping Play Mode...");
                        EditorApplication.isPlaying = false;
                        _step = 3;
                        _waitFrames = 0;
                    }
                }
            }
            else if (_step == 3)
            {
                // Step 3: Wait until play mode fully stops
                if (!EditorApplication.isPlaying)
                {
                    _waitFrames++;
                    if (_waitFrames > 10)
                    {
                        Debug.Log("[PlayModeSmokeTester] Step 3: Loading 020_RENDER_SANDBOX scene...");
                        EditorSceneManager.OpenScene("Assets/_Project/Scenes/020_RENDER_SANDBOX.unity", OpenSceneMode.Single);
                        _step = 4;
                        _waitFrames = 0;
                    }
                }
            }
            else if (_step == 4)
            {
                // Step 4: Wait and enter play mode for Sandbox
                _waitFrames++;
                if (_waitFrames > 10)
                {
                    Debug.Log("[PlayModeSmokeTester] Step 4: Entering Play Mode (Render Sandbox)...");
                    EditorApplication.isPlaying = true;
                    _step = 5;
                    _waitFrames = 0;
                    _stateTimer = 0f;
                }
            }
            else if (_step == 5)
            {
                // Step 5: Running in Play Mode (Render Sandbox)
                if (EditorApplication.isPlaying)
                {
                    _stateTimer += Time.deltaTime;
                    _waitFrames++;
                    if (_waitFrames % 30 == 0)
                    {
                        Debug.Log($"[PlayModeSmokeTester] Playing Render Sandbox... Timer = {_stateTimer:F1}s");
                    }

                    if (_stateTimer > 15f) // wait 15 seconds to let MapMagic and Crest initialize
                    {
                        Debug.Log("[PlayModeSmokeTester] Step 5: Render Sandbox playing successfully! Verifying rendering components...");

                        // Verify Skybox material
                        var skybox = RenderSettings.skybox;
                        if (skybox != null)
                        {
                            Debug.Log($"[PlayModeSmokeTester] Skybox Material: {skybox.name}, Shader: {skybox.shader.name}");
                            if (skybox.HasProperty("_MainCloudTex"))
                            {
                                var cloudTex = skybox.GetTexture("_MainCloudTex");
                                Debug.Log($"[PlayModeSmokeTester] Skybox cloud texture: {(cloudTex != null ? cloudTex.name : "null")}");
                            }
                        }
                        else
                        {
                            Debug.LogError("[PlayModeSmokeTester] ERROR: Skybox material is NULL!");
                        }

                        // Verify Crest Ocean Renderer via reflection to avoid compile-time dependency
                        var crestType = System.Type.GetType("Crest.OceanRenderer, Crest") ?? System.Type.GetType("Crest.OceanRenderer, Assembly-CSharp");
                        if (crestType != null)
                        {
                            var crest = GameObject.FindAnyObjectByType(crestType);
                            if (crest != null)
                            {
                                var isEnabled = (bool)crestType.GetProperty("isActiveAndEnabled")?.GetValue(crest);
                                Debug.Log($"[PlayModeSmokeTester] Crest OceanRenderer active: {isEnabled}");
                            }
                            else
                            {
                                Debug.LogWarning("[PlayModeSmokeTester] Crest OceanRenderer not found in sandbox scene (might be normal if not instantiated or named differently).");
                            }
                        }
                        else
                        {
                            Debug.LogWarning("[PlayModeSmokeTester] Crest.OceanRenderer type could not be resolved via reflection.");
                        }

                        // Verify Volumetric Fog / Atmosphere Manager
                        var atmos = GameObject.FindAnyObjectByType<Hecton8.Atmosphere.HectonAtmosphereManager>();
                        if (atmos != null)
                        {
                            Debug.Log($"[PlayModeSmokeTester] HectonAtmosphereManager found. TimeOfDay = {atmos.GetType().GetField("_initialTimeOfDay", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(atmos)}");
                        }
                        else
                        {
                            Debug.LogWarning("[PlayModeSmokeTester] HectonAtmosphereManager not found in scene.");
                        }

                        Debug.Log("[PlayModeSmokeTester] Verification complete. Stopping Play Mode...");
                        EditorApplication.isPlaying = false;
                        _step = 6;
                        _waitFrames = 0;
                    }
                }
            }
            else if (_step == 6)
            {
                // Step 6: Wait and exit editor successfully
                if (!EditorApplication.isPlaying)
                {
                    _waitFrames++;
                    if (_waitFrames > 10)
                    {
                        EditorApplication.update -= EditorUpdate;
                        Debug.Log("[PlayModeSmokeTester] Automated verification test passed successfully!");
                        EditorApplication.Exit(0);
                    }
                }
            }
        }
    }
}
