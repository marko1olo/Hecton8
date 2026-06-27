// H8BlackboxCollectors.cs — Data collection layer for Hecton8 Blackbox Diagnostics
// READ-ONLY diagnostic tool. Does not modify scenes, settings, or project assets.
// All project-specific types accessed via reflection only (H8Reflect).
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Hecton8.BlackboxDiagnostics
{
    public static class H8Collectors
    {
        private const string TAG = "[H8Blackbox/Collectors]";

        // ── 1. Project Metadata ──────────────────────────────────────────────

        public static H8ProjectMetadata CollectProjectMetadata(H8DiagnosticOptions opts)
        {
            var meta = new H8ProjectMetadata();
            try
            {
                meta.unityVersion = Application.unityVersion;
                meta.projectPath = H8Utils.GetProjectRoot();
                meta.platform = Application.platform.ToString();
                meta.buildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
                meta.qualityLevelIndex = QualitySettings.GetQualityLevel();
                meta.qualityLevelName = QualitySettings.names.Length > meta.qualityLevelIndex
                    ? QualitySettings.names[meta.qualityLevelIndex]
                    : "Unknown";

                // Render pipeline references (safe null checks)
                var currentRP = GraphicsSettings.currentRenderPipeline;
                meta.currentRenderPipeline = currentRP != null ? currentRP.name : "null";

                var defaultRP = GraphicsSettings.defaultRenderPipeline;
                meta.defaultRenderPipeline = defaultRP != null ? defaultRP.name : "null";

                var qualityRP = QualitySettings.renderPipeline;
                meta.qualityRenderPipeline = qualityRP != null ? qualityRP.name : "null";

                // Package versions from manifest.json
                meta.packageVersions = ReadPackageVersions();

                // Build scenes
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    string entry = scene.enabled
                        ? scene.path
                        : $"{scene.path} (disabled)";
                    meta.buildScenes.Add(entry);
                }

                // Active and loaded scenes
                var activeScene = SceneManager.GetActiveScene();
                meta.activeScene = activeScene.name;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.isLoaded)
                        meta.loadedScenes.Add(s.name);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectProjectMetadata failed: {e.Message}");
            }
            return meta;
        }

        /// <summary>
        /// Parse Packages/manifest.json for known package versions.
        /// Also probe for Crest and MapMagic folder existence.
        /// </summary>
        private static List<H8KV> ReadPackageVersions()
        {
            var versions = new List<H8KV>();
            string[] targetPackages = new[]
            {
                "com.unity.render-pipelines.universal",
                "com.unity.addressables",
                "com.unity.cinemachine",
                "com.unity.inputsystem"
            };

            try
            {
                string manifestPath = Path.Combine(H8Utils.GetProjectRoot(), "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string json = File.ReadAllText(manifestPath);
                    foreach (var pkg in targetPackages)
                    {
                        string version = ExtractPackageVersion(json, pkg);
                        versions.Add(new H8KV(pkg, version));
                    }
                }
                else
                {
                    versions.Add(new H8KV("_manifest", "manifest.json not found"));
                }
            }
            catch (Exception e)
            {
                versions.Add(new H8KV("_manifest_error", e.Message));
            }

            // Check Crest and MapMagic folder existence
            try
            {
                string assetsPath = Application.dataPath;
                bool crestExists = Directory.Exists(Path.Combine(assetsPath, "Crest"))
                    || Directory.Exists(Path.Combine(assetsPath, "_ThirdParty", "Crest"));
                versions.Add(new H8KV("Crest (folder)", crestExists ? "found" : "not found"));

                bool mapMagicExists = Directory.Exists(Path.Combine(assetsPath, "MapMagic"))
                    || Directory.Exists(Path.Combine(assetsPath, "_ThirdParty", "MapMagic"));
                versions.Add(new H8KV("MapMagic (folder)", mapMagicExists ? "found" : "not found"));
            }
            catch (Exception e)
            {
                versions.Add(new H8KV("_folder_check_error", e.Message));
            }

            return versions;
        }

        /// <summary>
        /// Simple string search for "packageName": "version" in manifest JSON.
        /// Avoids pulling in a JSON parser dependency.
        /// </summary>
        private static string ExtractPackageVersion(string json, string packageName)
        {
            // Look for: "com.unity.foo": "1.2.3"
            string search = $"\"{packageName}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return "not found";

            int colonIdx = json.IndexOf(':', idx + search.Length);
            if (colonIdx < 0) return "parse error";

            int quoteStart = json.IndexOf('"', colonIdx + 1);
            if (quoteStart < 0) return "parse error";

            int quoteEnd = json.IndexOf('"', quoteStart + 1);
            if (quoteEnd < 0) return "parse error";

            return json.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
        }

        // ── 2. Scene Info ────────────────────────────────────────────────────

        public static List<H8SceneInfo> CollectSceneInfo(H8DiagnosticOptions opts)
        {
            var scenes = new List<H8SceneInfo>();
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    var info = new H8SceneInfo();
                    info.name = scene.name;
                    info.path = scene.path;
                    info.buildIndex = scene.buildIndex;
                    info.isLoaded = scene.isLoaded;
                    info.isDirty = scene.isDirty;

                    if (scene.isLoaded)
                    {
                        info.rootCount = scene.rootCount;

                        H8Utils.CountSceneObjects(scene, out int total, out int active, out int inactive);
                        info.totalGameObjects = total;
                        info.activeGameObjects = active;
                        info.inactiveGameObjects = inactive;

                        info.cameraCount = H8Utils.CountComponentsInScene<Camera>(scene);
                        info.rendererCount = H8Utils.CountComponentsInScene<Renderer>(scene);
                        info.terrainCount = H8Utils.CountComponentsInScene<Terrain>(scene);
                        info.lightCount = H8Utils.CountComponentsInScene<Light>(scene);
                    }

                    scenes.Add(info);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectSceneInfo failed: {e.Message}");
            }
            return scenes;
        }

        // ── 3. Key Objects ───────────────────────────────────────────────────

        public static List<H8KeyObjectInfo> CollectKeyObjects(H8DiagnosticOptions opts)
        {
            var results = new List<H8KeyObjectInfo>();
            try
            {
                // Track seen instance IDs to deduplicate
                var seenIds = new HashSet<int>();

                // Names to search by GameObject name
                string[] nameSearches = new[]
                {
                    "GameBootstrapper", "GlobalRegistry", "SystemDispatcher",
                    "GameTickManager", "MapMagic", "MapMagicObject",
                    "Ocean_Crest", "H8_WORLD_CREST_OCEAN_RUNTIME_1428",
                    "Main Camera", "DEPRECATED_STUFF",
                    "EnvironmentRoot", "WorldRoot", "LightingRoot",
                    "WaterRoot", "OceanRoot", "TerrainRoot"
                };

                // Types to search by component type name
                string[] typeSearches = new[]
                {
                    "GameBootstrapper", "MapMagicRuntimeBridge", "MapMagicBridge",
                    "HectonAtmosphereManager", "HectonCelestialEngine", "WorldFidelityRoot"
                };

                // Search by name
                foreach (var name in nameSearches)
                {
                    try
                    {
                        // Soft match for specific difficult names
                        bool isSoftMatch = name.Contains("MapMagic") || name.Contains("Ocean") || name.Contains("Crest");
                
                        List<GameObject> foundGos;
                        if (isSoftMatch)
                        {
                            foundGos = new List<GameObject>();
                            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                            {
                                var transforms = root.GetComponentsInChildren<Transform>(opts.includeInactiveObjects);
                                foreach (var t in transforms)
                                {
                                    if (t.name.Contains(name))
                                        foundGos.Add(t.gameObject);
                                }
                            }
                        }
                        else
                        {
                            foundGos = H8Utils.FindGameObjectsByName(name, opts.includeInactiveObjects);
                        }

                        if (foundGos.Count == 0)
                        {
                            // Add a not-found entry
                            var notFound = new H8KeyObjectInfo();
                            notFound.exists = false;
                            notFound.searchKey = $"name:{name}";
                            results.Add(notFound);
                            continue;
                        }
                        foreach (var go in foundGos)
                        {
                            if (go == null) continue;
                            int id = go.GetHashCode();
                            if (seenIds.Contains(id)) continue;
                            seenIds.Add(id);
                            results.Add(H8Utils.BuildKeyObjectInfo($"name:{name}", go));
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{TAG} Key object name search '{name}' failed: {e.Message}");
                    }
                }

                // Search by type
                foreach (var typeName in typeSearches)
                {
                    try
                    {
                        var comps = H8Reflect.FindComponentsByTypeName(typeName);
                        if (comps.Count == 0)
                        {
                            // Only add not-found if we haven't already found something with this name
                            var notFound = new H8KeyObjectInfo();
                            notFound.exists = false;
                            notFound.searchKey = $"type:{typeName}";
                            results.Add(notFound);
                            continue;
                        }
                        foreach (var comp in comps)
                        {
                            if (comp == null) continue;
                            var go = comp.gameObject;
                            if (go == null) continue;
                            int id = go.GetHashCode();
                            if (seenIds.Contains(id)) continue;
                            seenIds.Add(id);
                            results.Add(H8Utils.BuildKeyObjectInfo($"type:{typeName}", go));
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{TAG} Key object type search '{typeName}' failed: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectKeyObjects failed: {e.Message}");
            }
            return results;
        }

        // ── 4. Bootstrap Info ────────────────────────────────────────────────

        public static H8BootstrapInfo CollectBootstrapInfo(H8DiagnosticOptions opts)
        {
            var info = new H8BootstrapInfo();
            try
            {
                var activeScene = SceneManager.GetActiveScene();
                info.activeSceneName = activeScene.name;
                info.activeSceneBuildIndex = activeScene.buildIndex;
                info.isBootstrapScene = activeScene.buildIndex == 0
                    || activeScene.name.IndexOf("BOOTSTRAP", StringComparison.OrdinalIgnoreCase) >= 0;

                // Find GameBootstrapper type
                Type bootstrapType = H8Reflect.FindType("GameBootstrapper");
                if (bootstrapType == null)
                {
                    info.bootstrapperFound = false;
                    info.inferredState = "TYPE_NOT_FOUND";
                    return info;
                }
                info.bootstrapperFound = true;

                // Find instances
                var instances = H8Reflect.FindComponentsByTypeName("GameBootstrapper");
                info.instanceCount = instances.Count;

                if (instances.Count == 0)
                {
                    info.inferredState = "NOT_INSTANTIATED";

                    // Still dump static fields if requested
                    if (opts.includeReflectionDump)
                        info.staticFields = H8Reflect.DumpStaticMembers(bootstrapType);

                    // Check registry phase even without bootstrap instances
                    info.inferredState = InferBootstrapState(info, null);
                    return info;
                }

                // Dump static fields
                if (opts.includeReflectionDump)
                    info.staticFields = H8Reflect.DumpStaticMembers(bootstrapType);

                // Dump instance fields on first found instance
                var firstInstance = instances[0];
                if (firstInstance != null)
                    info.instanceFields = H8Reflect.DumpInstanceMembers(firstInstance);

                // Infer state using registry phase
                info.inferredState = InferBootstrapState(info, firstInstance);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectBootstrapInfo failed: {e.Message}");
                info.inferredState = $"ERROR: {e.Message}";
            }
            return info;
        }

        /// <summary>
        /// Infer bootstrap state by reading _registryPhase from GlobalRegistry.
        /// </summary>
        private static string InferBootstrapState(H8BootstrapInfo info, Component bootstrapInstance)
        {
            try
            {
                Type registryType = H8Reflect.FindType("Hecton8.Core.GlobalRegistry")
                    ?? H8Reflect.FindType("GlobalRegistry");

                if (registryType != null)
                {
                    object phaseObj = H8Reflect.GetStatic(registryType, "_registryPhase");
                    if (phaseObj is int phase)
                    {
                        if (phase == 2) return "BOOTSTRAP_COMPLETE";
                        if (phase == 1) return "BOOTSTRAP_IN_PROGRESS";
                        if (phase == 0)
                        {
                            if (!info.isBootstrapScene)
                                return "DIRECT_WORLD_START_DETECTED";
                            return "BOOTSTRAP_NOT_STARTED";
                        }
                    }
                }

                // Fallback: check if instances exist
                if (info.instanceCount == 0) return "NOT_INSTANTIATED";

                // Check static fields for common completion flags
                foreach (var kv in info.staticFields)
                {
                    if (kv.key.IndexOf("Complete", StringComparison.OrdinalIgnoreCase) >= 0
                        || kv.key.IndexOf("Initialized", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (kv.value == "true" || kv.value == "True")
                            return "LIKELY_COMPLETE";
                    }
                }

                return "INSTANTIATED_PHASE_UNKNOWN";
            }
            catch (Exception e)
            {
                return $"INFERENCE_ERROR: {e.Message}";
            }
        }

        // ── 5. Registry Info ─────────────────────────────────────────────────

        public static H8RegistryInfo CollectRegistryInfo(H8DiagnosticOptions opts)
        {
            var info = new H8RegistryInfo();
            try
            {
                // Find GlobalRegistry type
                Type registryType = H8Reflect.FindType("Hecton8.Core.GlobalRegistry")
                    ?? H8Reflect.FindType("GlobalRegistry");

                if (registryType == null)
                {
                    info.typeFound = false;
                    info.typeName = "not found";
                    info.inferredState = "TYPE_NOT_FOUND";
                    return info;
                }
                info.typeFound = true;
                info.typeName = registryType.FullName ?? registryType.Name;

                // Read _registryPhase
                object phaseObj = H8Reflect.GetStatic(registryType, "_registryPhase");
                if (phaseObj is int phase)
                {
                    info.registryPhase = phase;
                    switch (phase)
                    {
                        case 0: info.registryPhaseName = "Uninitialized"; break;
                        case 1: info.registryPhaseName = "Registering"; break;
                        case 2: info.registryPhaseName = "Ready"; break;
                        default: info.registryPhaseName = $"Unknown({phase})"; break;
                    }
                }
                else
                {
                    info.registryPhaseName = "read_failed";
                }

                // Probe critical service slots
                var slotDefs = new (string fieldName, string slotName)[]
                {
                    ("_input", "Input"),
                    ("_physics", "Physics"),
                    ("_audio", "Audio"),
                    ("_scene", "Scene"),
                    ("_save", "Save"),
                    ("_ui", "UI"),
                    ("_player", "Player"),
                    ("_oceanKinematics", "OceanKinematics"),
                    ("_atmosphereRuntime", "AtmosphereRuntime"),
                    ("_celestialEngineRuntime", "CelestialEngineRuntime"),
                    ("_mapMagicRuntime", "MapMagicRuntime"),
                    ("_terrainProviderRuntime", "TerrainProviderRuntime"),
                    ("_tickManager", "TickManager"),
                    ("_dispatcher", "Dispatcher"),
                    ("_renderDispatcher", "RenderDispatcher"),
                    ("_objectPool", "ObjectPool"),
                    ("_environment", "Environment"),
                    ("_weather", "Weather")
                };

                int nullCount = 0;
                int totalSlots = slotDefs.Length;

                foreach (var (fieldName, slotName) in slotDefs)
                {
                    var slot = H8Reflect.ProbeRegistrySlot(registryType, fieldName, slotName);
                    info.slots.Add(slot);
                    if (slot.isNull && slot.memberFound) nullCount++;
                }

                // Fuzzy scan for candidate members
                string[] fuzzyNames = new[] { "Input", "Physics", "Audio", "Scene", "Save", "UI", "MapMagic", "Terrain", "Ocean", "Atmosphere", "Celestial", "Dispatcher", "Tick" };
                const System.Reflection.BindingFlags bf = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
                try
                {
                    foreach (var fi in registryType.GetFields(bf))
                    {
                        foreach (var fn in fuzzyNames)
                        {
                            if (fi.Name.IndexOf(fn, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                info.candidateStaticMembers.Add(new H8KV($"Field: {fi.Name}", fi.FieldType.Name));
                                break;
                            }
                        }
                    }
                    foreach (var pi in registryType.GetProperties(bf))
                    {
                        foreach (var fn in fuzzyNames)
                        {
                            if (pi.Name.IndexOf(fn, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                info.candidateStaticMembers.Add(new H8KV($"Prop: {pi.Name}", pi.PropertyType.Name));
                                break;
                            }
                        }
                    }
                }
                catch { /* ignore */ }

                // Dump static members if requested
                if (opts.includeReflectionDump)
                    info.staticFields = H8Reflect.DumpStaticMembers(registryType);

                // Infer state
                if (info.registryPhase == 2)
                    info.inferredState = "READY";
                else if (nullCount == totalSlots)
                    info.inferredState = "EMPTY";
                else if (nullCount > 0)
                    info.inferredState = $"PARTIAL ({totalSlots - nullCount}/{totalSlots} filled)";
                else
                    info.inferredState = "ALL_SLOTS_FILLED";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectRegistryInfo failed: {e.Message}");
                info.inferredState = $"ERROR: {e.Message}";
            }
            return info;
        }

        // ── 6. MapMagic Info ─────────────────────────────────────────────────

        public static H8MapMagicInfo CollectMapMagicInfo(H8DiagnosticOptions opts)
        {
            var info = new H8MapMagicInfo();
            try
            {
                // Find MapMagicObject instances
                var mmObjects = H8Reflect.FindComponentsByTypeName("MapMagicObject");
                if (mmObjects.Count > 0)
                {
                    var mm = mmObjects[0];
                    info.mapMagicObjectFound = true;
                    info.mapMagicObjectActive = mm.gameObject.activeSelf;
                    info.mapMagicObjectActiveInHierarchy = mm.gameObject.activeInHierarchy;

                    // Read graph field
                    try
                    {
                        object graph = H8Reflect.GetFieldFallback(mm, new[] { "graph", "Graph", "_graph" }, out string foundName);
                        if (!H8Reflect.IsUnityNull(graph))
                        {
                            info.graphAssigned = true;
                            if (graph is UnityEngine.Object graphObj)
                            {
                                info.graphAssetName = graphObj.name;
                                string assetPath = AssetDatabase.GetAssetPath(graphObj);
                                info.graphAssetPath = string.IsNullOrEmpty(assetPath) ? "runtime" : assetPath;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        info.graphAssetName = $"<read_error: {e.Message}>";
                    }

                    // Dump reflected fields if requested
                    if (opts.includeReflectionDump)
                        info.reflectedFields.AddRange(H8Reflect.DumpInstanceMembers(mm));
                }

                // Find MapMagicRuntimeBridge
                var bridges = H8Reflect.FindComponentsByTypeName("MapMagicRuntimeBridge");
                if (bridges.Count > 0)
                {
                    var bridge = bridges[0];
                    info.runtimeBridgeFound = true;
                    info.runtimeBridgeActive = bridge.gameObject.activeSelf;
                    info.runtimeBridgeEnabled = bridge is Behaviour beh ? beh.enabled : true;

                    if (opts.includeReflectionDump)
                        info.reflectedFields.AddRange(H8Reflect.DumpInstanceMembers(bridge));
                }

                // Terrain counts
                info.activeTerrainCount = Terrain.activeTerrains != null ? Terrain.activeTerrains.Length : 0;

                var allTerrains = UnityEngine.Object.FindObjectsByType<Terrain>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                info.allTerrainCount = allTerrains != null ? allTerrains.Length : 0;

                // Terrain details
                if (allTerrains != null)
                {
                    int maxTerrains = Math.Min(allTerrains.Length, opts.maxObjectsPerSection);
                    for (int i = 0; i < maxTerrains; i++)
                    {
                        var t = allTerrains[i];
                        if (t == null) continue;
                        string terrainName = t.name;
                        bool hasData = t.terrainData != null;
                        string size = hasData ? t.terrainData.size.ToString() : "no_data";
                        string pos = t.transform.position.ToString();
                        string mat = t.materialTemplate != null ? t.materialTemplate.name : "null";

                        info.terrainDetails.Add(new H8KV($"terrain[{i}]",
                            $"name={terrainName}, hasData={hasData}, size={size}, pos={pos}, material={mat}"));
                    }
                }

                // Check GlobalRegistry for MapMagic registration
                try
                {
                    Type registryType = H8Reflect.FindType("Hecton8.Core.GlobalRegistry")
                        ?? H8Reflect.FindType("GlobalRegistry");
                    if (registryType != null)
                    {
                        var slot = H8Reflect.ProbeRegistrySlot(registryType, "_mapMagicRuntime", "MapMagicRuntime");
                        info.registeredInGlobalRegistry = !slot.isNull;
                    }
                }
                catch { /* non-critical */ }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectMapMagicInfo failed: {e.Message}");
            }
            return info;
        }

        // ── 7. Crest Info ────────────────────────────────────────────────────

        public static H8CrestInfo CollectCrestInfo(H8DiagnosticOptions opts)
        {
            var info = new H8CrestInfo();
            try
            {
                // Find ocean GameObjects by name
                var oceanCrestObjects = H8Utils.FindGameObjectsByName("Ocean_Crest", true);
                var h8OceanObjects = H8Utils.FindGameObjectsByName("H8_WORLD_CREST_OCEAN_RUNTIME_1428", true);

                // Use whichever is found
                GameObject oceanGo = null;
                if (oceanCrestObjects.Count > 0) oceanGo = oceanCrestObjects[0];
                else if (h8OceanObjects.Count > 0) oceanGo = h8OceanObjects[0];

                if (oceanGo != null)
                {
                    info.oceanCrestObjectFound = true;
                    info.oceanCrestActive = oceanGo.activeSelf;
                    info.oceanCrestActiveInHierarchy = oceanGo.activeInHierarchy;
                    info.oceanCrestHierarchyPath = H8Utils.GetHierarchyPath(oceanGo.transform);
                }

                // Find OceanRenderer component (Crest namespace)
                var oceanRenderers = H8Reflect.FindComponentsByTypeName("OceanRenderer");
                if (oceanRenderers.Count > 0)
                {
                    var or = oceanRenderers[0];
                    info.oceanRendererFound = true;
                    info.oceanRendererActive = or.gameObject.activeSelf;
                    info.oceanRendererEnabled = or is Behaviour orBeh ? orBeh.enabled : true;

                    // Read key fields via reflection
                    try
                    {
                        object viewpoint = H8Reflect.GetFieldFallback(or, new[] { "_viewpoint", "Viewpoint" }, out string _);
                        info.viewpointAssigned = !H8Reflect.IsUnityNull(viewpoint);

                        object viewCam = H8Reflect.GetFieldFallback(or, new[] { "_camera", "_viewCamera", "ViewCamera", "Camera" }, out string _);
                        info.viewCameraAssigned = !H8Reflect.IsUnityNull(viewCam);

                        object primaryLight = H8Reflect.GetFieldFallback(or, new[] { "_primaryLight", "PrimaryLight" }, out string _);
                        info.primaryLightAssigned = !H8Reflect.IsUnityNull(primaryLight);
                    }
                    catch { /* non-critical reflection reads */ }

                    // Dump reflected fields if requested
                    if (opts.includeReflectionDump)
                        info.reflectedFields.AddRange(H8Reflect.DumpInstanceMembers(or));
                }

                // Find Crest4KinematicsAdapter
                var adapters = H8Reflect.FindComponentsByTypeName("Crest4KinematicsAdapter");
                if (adapters.Count > 0)
                {
                    var adapter = adapters[0];
                    info.adapterFound = true;
                    info.adapterActive = adapter.gameObject.activeSelf;
                    info.adapterEnabled = adapter is Behaviour adBeh ? adBeh.enabled : true;
                }

                // Find UnderwaterRenderer
                var underwaterRenderers = H8Reflect.FindComponentsByTypeName("UnderwaterRenderer");
                if (underwaterRenderers.Count > 0)
                {
                    var ur = underwaterRenderers[0];
                    info.underwaterRendererFound = true;
                    info.underwaterRendererEnabled = ur is Behaviour urBeh ? urBeh.enabled : true;

                    // Try to get the camera this underwater renderer targets
                    try
                    {
                        object urCam = H8Reflect.GetField(ur, "_camera");
                        if (!H8Reflect.IsUnityNull(urCam) && urCam is Camera cam)
                            info.underwaterRendererCameraName = cam.name;
                        else if (!H8Reflect.IsUnityNull(urCam) && urCam is Component camComp)
                            info.underwaterRendererCameraName = camComp.name;
                    }
                    catch { /* non-critical */ }
                }

                // Check GlobalRegistry for OceanKinematics registration
                try
                {
                    Type registryType = H8Reflect.FindType("Hecton8.Core.GlobalRegistry")
                        ?? H8Reflect.FindType("GlobalRegistry");
                    if (registryType != null)
                    {
                        var slot = H8Reflect.ProbeRegistrySlot(registryType, "_oceanKinematics", "OceanKinematics");
                        info.kinematicsRegistered = !slot.isNull;
                    }
                }
                catch { /* non-critical */ }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectCrestInfo failed: {e.Message}");
            }
            return info;
        }

        // ── 8. Atmosphere Info ───────────────────────────────────────────────

        public static H8AtmosphereInfo CollectAtmosphereInfo(H8DiagnosticOptions opts)
        {
            var info = new H8AtmosphereInfo();
            try
            {
                // Find HectonAtmosphereManager
                var atmosManagers = H8Reflect.FindComponentsByTypeName("HectonAtmosphereManager");
                if (atmosManagers.Count > 0)
                {
                    var am = atmosManagers[0];
                    info.atmosphereManagerFound = true;
                    info.atmosphereManagerActive = am.gameObject.activeSelf;
                    info.atmosphereManagerEnabled = am is Behaviour amBeh ? amBeh.enabled : true;

                    if (opts.includeReflectionDump)
                        info.reflectedFields.AddRange(H8Reflect.DumpInstanceMembers(am));
                }

                // Find HectonCelestialEngine
                var celestialEngines = H8Reflect.FindComponentsByTypeName("HectonCelestialEngine");
                if (celestialEngines.Count > 0)
                {
                    var ce = celestialEngines[0];
                    info.celestialEngineFound = true;
                    info.celestialEngineActive = ce.gameObject.activeSelf;
                    info.celestialEngineEnabled = ce is Behaviour ceBeh ? ceBeh.enabled : true;

                    if (opts.includeReflectionDump)
                        info.reflectedFields.AddRange(H8Reflect.DumpInstanceMembers(ce));
                }

                // RenderSettings
                var skyboxMat = RenderSettings.skybox;
                info.skyboxAssigned = skyboxMat != null;
                info.skyboxMaterialName = skyboxMat != null ? skyboxMat.name : "null";

                var sun = RenderSettings.sun;
                info.sunAssigned = sun != null;
                info.sunName = sun != null ? sun.name : "null";

                info.fogEnabled = RenderSettings.fog;
                info.fogColor = RenderSettings.fogColor.ToString();
                info.fogMode = RenderSettings.fogMode.ToString();

                // Render settings collection
                info.renderSettings.Add(new H8KV("ambientMode", RenderSettings.ambientMode.ToString()));
                info.renderSettings.Add(new H8KV("ambientLight", RenderSettings.ambientLight.ToString()));
                info.renderSettings.Add(new H8KV("ambientIntensity", RenderSettings.ambientIntensity.ToString("F3")));
                info.renderSettings.Add(new H8KV("ambientSkyColor", RenderSettings.ambientSkyColor.ToString()));
                info.renderSettings.Add(new H8KV("ambientEquatorColor", RenderSettings.ambientEquatorColor.ToString()));
                info.renderSettings.Add(new H8KV("ambientGroundColor", RenderSettings.ambientGroundColor.ToString()));
                info.renderSettings.Add(new H8KV("reflectionIntensity", RenderSettings.reflectionIntensity.ToString("F3")));
                info.renderSettings.Add(new H8KV("fogDensity", RenderSettings.fogDensity.ToString("F4")));
                info.renderSettings.Add(new H8KV("fogStartDistance", RenderSettings.fogStartDistance.ToString("F1")));
                info.renderSettings.Add(new H8KV("fogEndDistance", RenderSettings.fogEndDistance.ToString("F1")));
                info.renderSettings.Add(new H8KV("subtractiveShadowColor", RenderSettings.subtractiveShadowColor.ToString()));

                // Count directional lights
                var allLights = UnityEngine.Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
                int dirCount = 0;
                if (allLights != null)
                {
                    foreach (var light in allLights)
                    {
                        if (light != null && light.type == LightType.Directional)
                            dirCount++;
                    }
                }
                info.directionalLightCount = dirCount;

                // Check GlobalRegistry slots for atmosphere and celestial
                try
                {
                    Type registryType = H8Reflect.FindType("Hecton8.Core.GlobalRegistry")
                        ?? H8Reflect.FindType("GlobalRegistry");
                    if (registryType != null)
                    {
                        var atmosSlot = H8Reflect.ProbeRegistrySlot(registryType, "_atmosphereRuntime", "AtmosphereRuntime");
                        info.atmosphereRegistered = !atmosSlot.isNull;

                        var celestialSlot = H8Reflect.ProbeRegistrySlot(registryType, "_celestialEngineRuntime", "CelestialEngineRuntime");
                        info.celestialRegistered = !celestialSlot.isNull;
                    }
                }
                catch { /* non-critical */ }

                // Shader globals
                try
                {
                    info.shaderGlobals.Add(new H8KV("_SunDirection",
                        SafeReadShaderGlobalVector("_SunDirection")));
                    info.shaderGlobals.Add(new H8KV("_HectonTimeOfDay01",
                        SafeReadShaderGlobalFloat("_HectonTimeOfDay01")));
                }
                catch { /* non-critical */ }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectAtmosphereInfo failed: {e.Message}");
            }
            return info;
        }

        private static string SafeReadShaderGlobalVector(string name)
        {
            try { return Shader.GetGlobalVector(name).ToString(); }
            catch { return "<not_set>"; }
        }

        private static string SafeReadShaderGlobalFloat(string name)
        {
            try { return Shader.GetGlobalFloat(name).ToString("F4"); }
            catch { return "<not_set>"; }
        }

        // ── 9. URP Info ──────────────────────────────────────────────────────

        public static H8UrpInfo CollectUrpInfo(H8DiagnosticOptions opts)
        {
            var info = new H8UrpInfo();
            try
            {
                // Pipeline asset references
                var currentRP = GraphicsSettings.currentRenderPipeline;
                info.currentRenderPipelineAsset = currentRP != null ? currentRP.name : "null";

                var defaultRP = GraphicsSettings.defaultRenderPipeline;
                info.defaultRenderPipelineAsset = defaultRP != null ? defaultRP.name : "null";

                var qualityRP = QualitySettings.renderPipeline;
                info.qualityRenderPipelineAsset = qualityRP != null ? qualityRP.name : "null";

                // Determine active URP asset (prefer quality override, fall back to default)
                RenderPipelineAsset activeAsset = qualityRP != null ? qualityRP : defaultRP;
                if (activeAsset == null) activeAsset = currentRP;

                if (activeAsset == null)
                {
                    info.activeUrpAssetName = "null";
                    info.activeRendererDataName = "null";
                    return info;
                }

                info.activeUrpAssetName = activeAsset.name;

                // Check if it is a URP asset via type name (no direct reference)
                string assetTypeName = activeAsset.GetType().Name;
                info.urpSettings.Add(new H8KV("assetTypeName", assetTypeName));

                // Read renderer data list via reflection on the URP asset
                // Field name is "m_RendererDataList" in URP
                try
                {
                    object rendererDataList = H8Reflect.GetField(activeAsset, "m_RendererDataList");
                    if (rendererDataList == null)
                        rendererDataList = H8Reflect.GetField(activeAsset, "m_Renderers");

                    if (rendererDataList is Array rdArray && rdArray.Length > 0)
                    {
                        // Use first renderer data (the default)
                        object rendererData = rdArray.GetValue(0);
                        if (rendererData is UnityEngine.Object rdObj)
                        {
                            info.activeRendererDataName = rdObj.name;

                            // Read renderer features from the renderer data
                            // Field: "m_RendererFeatures"
                            object features = H8Reflect.GetField(rdObj, "m_RendererFeatures");
                            if (features is System.Collections.IList featureList)
                            {
                                foreach (object feature in featureList)
                                {
                                    if (H8Reflect.IsUnityNull(feature)) continue;
                                    if (feature is UnityEngine.Object featureObj)
                                    {
                                        string fName = featureObj.name;
                                        string fType = featureObj.GetType().Name;
                                        bool isActive = true;

                                        // Try reading "m_IsActive" or "isActive"
                                        try
                                        {
                                            object activeField = H8Reflect.GetField(featureObj, "m_IsActive");
                                            if (activeField == null)
                                                activeField = H8Reflect.GetField(featureObj, "isActive");
                                            if (activeField is bool ab) isActive = ab;
                                        }
                                        catch { /* default true */ }

                                        info.rendererFeatures.Add(new H8UrpFeatureInfo(fName, fType, isActive));
                                    }
                                }
                            }
                        }
                        else if (rendererData != null)
                        {
                            info.activeRendererDataName = rendererData.GetType().Name;
                        }

                        // Report count of renderer data entries
                        info.urpSettings.Add(new H8KV("rendererDataCount", rdArray.Length.ToString()));
                    }
                    else
                    {
                        info.activeRendererDataName = "<no_renderer_data>";
                    }
                }
                catch (Exception e)
                {
                    info.activeRendererDataName = $"<read_error: {e.Message}>";
                }

                // Read some common URP settings via reflection
                try
                {
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_ShadowDistance", "shadowDistance");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_MainLightShadowmapResolution", "mainLightShadowResolution");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_AdditionalLightsShadowmapResolution", "additionalLightShadowResolution");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_SupportsHDR", "supportsHDR");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_MSAA", "msaa");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_RenderScale", "renderScale");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_MainLightRenderingMode", "mainLightRenderingMode");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_AdditionalLightsRenderingMode", "additionalLightsRenderingMode");
                    ReadUrpSetting(activeAsset, info.urpSettings, "m_SoftShadowsSupported", "softShadows");
                }
                catch { /* non-critical */ }

                // Flag known Hecton8 custom features
                string[] hectonFeatures = new[]
                {
                    "HectonSinglePassOceanFeature",
                    "HectonDeferredCausticsFeature",
                    "HectonVolumetricParticulateFogFeature",
                    "HectonNoirDepthFogFeature",
                    "HectonFluidAdvectionRenderFeature"
                };

                foreach (var hfName in hectonFeatures)
                {
                    bool found = false;
                    foreach (var rf in info.rendererFeatures)
                    {
                        if (rf.typeName == hfName)
                        {
                            found = true;
                            break;
                        }
                    }
                    info.urpSettings.Add(new H8KV($"hectonFeature:{hfName}", found ? "present" : "not found"));
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectUrpInfo failed: {e.Message}");
            }
            return info;
        }

        private static void ReadUrpSetting(object asset, List<H8KV> target, string fieldName, string displayName)
        {
            try
            {
                object val = H8Reflect.GetField(asset, fieldName);
                target.Add(new H8KV(displayName, H8Reflect.SafeStr(val)));
            }
            catch { /* skip unreadable fields */ }
        }

        // ── 10. Cameras ──────────────────────────────────────────────────────

        public static List<H8CameraInfo> CollectCameras(H8DiagnosticOptions opts)
        {
            var cameras = new List<H8CameraInfo>();
            try
            {
                var allCameras = UnityEngine.Object.FindObjectsByType<Camera>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (allCameras == null) return cameras;

                int maxCams = Math.Min(allCameras.Length, opts.maxObjectsPerSection);
                for (int i = 0; i < maxCams; i++)
                {
                    var cam = allCameras[i];
                    if (cam == null) continue;

                    try
                    {
                        var ci = new H8CameraInfo();
                        ci.name = cam.name;
                        ci.hierarchyPath = H8Utils.GetHierarchyPath(cam.transform);
                        ci.activeSelf = cam.gameObject.activeSelf;
                        ci.activeInHierarchy = cam.gameObject.activeInHierarchy;
                        ci.enabled = cam.enabled;

                        try { ci.tag = cam.tag; }
                        catch { ci.tag = "<untagged>"; }

                        ci.isMainCamera = cam.CompareTag("MainCamera");
                        ci.clearFlags = cam.clearFlags.ToString();
                        ci.cullingMask = cam.cullingMask;
                        ci.culledLayerNames = H8Utils.LayerMaskToCulledNames(cam.cullingMask);
                        ci.visibleLayerNames = H8Utils.LayerMaskToVisibleNames(cam.cullingMask);
                        ci.nearClip = cam.nearClipPlane;
                        ci.farClip = cam.farClipPlane;
                        ci.fieldOfView = cam.fieldOfView;
                        ci.orthographic = cam.orthographic;
                        ci.depth = cam.depth;
                        ci.hasTargetTexture = cam.targetTexture != null;
                        ci.targetTextureName = cam.targetTexture != null ? cam.targetTexture.name : "";
                        ci.position = cam.transform.position.ToString("F2");
                        ci.rotation = cam.transform.rotation.eulerAngles.ToString("F1");

                        // Check for UniversalAdditionalCameraData (via reflection)
                        try
                        {
                            var urpCamData = FindComponentByTypeName(cam.gameObject, "UniversalAdditionalCameraData");
                            ci.hasUrpAdditionalData = urpCamData != null;
                            if (urpCamData != null)
                            {
                                ReadUrpCameraData(urpCamData, ci.urpData);
                            }
                        }
                        catch { /* non-critical */ }

                        // Check for CinemachineBrain
                        try
                        {
                            var cmBrain = FindComponentByTypeName(cam.gameObject, "CinemachineBrain");
                            ci.hasCinemachineBrain = cmBrain != null;
                        }
                        catch { /* non-critical */ }

                        // Check for UnderwaterRenderer
                        try
                        {
                            var uwRenderer = FindComponentByTypeName(cam.gameObject, "UnderwaterRenderer");
                            ci.hasUnderwaterRenderer = uwRenderer != null;
                        }
                        catch { /* non-critical */ }

                        cameras.Add(ci);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{TAG} Camera '{cam.name}' collection failed: {e.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectCameras failed: {e.Message}");
            }
            return cameras;
        }

        /// <summary>
        /// Find a component on a GameObject by type name without direct assembly reference.
        /// </summary>
        private static Component FindComponentByTypeName(GameObject go, string typeName)
        {
            if (go == null) return null;
            Type type = H8Reflect.FindType(typeName);
            if (type == null || !typeof(Component).IsAssignableFrom(type)) return null;
            return go.GetComponent(type);
        }

        /// <summary>
        /// Read common URP additional camera data fields via reflection.
        /// </summary>
        private static void ReadUrpCameraData(Component urpCamData, List<H8KV> target)
        {
            try
            {
                ReadReflectedField(urpCamData, target, "renderType", "renderType");
                ReadReflectedField(urpCamData, target, "m_RenderShadows", "renderShadows");
                ReadReflectedField(urpCamData, target, "m_RequiresDepthTextureOption", "requiresDepthTexture");
                ReadReflectedField(urpCamData, target, "m_RequiresOpaqueTextureOption", "requiresOpaqueTexture");
                ReadReflectedField(urpCamData, target, "m_RendererIndex", "rendererIndex");
                ReadReflectedField(urpCamData, target, "volumeLayerMask", "volumeLayerMask");
                ReadReflectedField(urpCamData, target, "antialiasing", "antialiasing");
                ReadReflectedField(urpCamData, target, "antialiasingQuality", "antialiasingQuality");
                ReadReflectedField(urpCamData, target, "m_StopNaN", "stopNaN");
                ReadReflectedField(urpCamData, target, "m_Dithering", "dithering");
            }
            catch { /* non-critical */ }
        }

        private static void ReadReflectedField(object instance, List<H8KV> target, string fieldName, string displayName)
        {
            try
            {
                object val = H8Reflect.GetField(instance, fieldName);
                if (val != null)
                    target.Add(new H8KV(displayName, H8Reflect.SafeStr(val)));
            }
            catch { /* skip */ }
        }

        // ── 11. Console Info ─────────────────────────────────────────────────

        public static H8ConsoleInfo CollectConsoleInfo(H8DiagnosticOptions opts)
        {
            var info = new H8ConsoleInfo();
            try
            {
                if (opts.includeConsoleLog)
                {
                    info.entries = H8Utils.GetConsoleLogs();

                    // Count by type
                    foreach (var entry in info.entries)
                    {
                        switch (entry.type)
                        {
                            case "Error": info.totalErrors++; break;
                            case "Warning": info.totalWarnings++; break;
                            case "Log": info.totalLogs++; break;
                        }
                    }
                }

                if (opts.includeEditorLogTail)
                {
                    info.editorLogTail = H8Utils.ReadEditorLogTail(200);
                    if (info.editorLogTail != null && info.editorLogTail.Length > opts.maxTextLengthPerValue)
                        info.editorLogTail = H8Utils.Truncate(info.editorLogTail, opts.maxTextLengthPerValue);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectConsoleInfo failed: {e.Message}");
            }
            return info;
        }

        // ── 12. Git Info ─────────────────────────────────────────────────────

        public static H8GitInfo CollectGitInfo(H8DiagnosticOptions opts)
        {
            var info = new H8GitInfo();
            try
            {
                if (!opts.includeGitDiff) return info;

                info.gitAvailable = H8Utils.IsGitAvailable();
                if (!info.gitAvailable) return info;

                // Branch
                string branchOutput = H8Utils.RunGit("branch --show-current");
                info.branch = branchOutput != null ? branchOutput.Trim() : "";

                // Modified files
                string statusOutput = H8Utils.RunGit("status --short");
                if (!string.IsNullOrEmpty(statusOutput) && !statusOutput.StartsWith("<git"))
                {
                    string[] lines = statusOutput.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed))
                            info.modifiedFiles.Add(H8Utils.Truncate(trimmed, opts.maxTextLengthPerValue));
                    }
                }

                // Targeted diffs (summary stats) for key files
                string[] targetFiles = new[]
                {
                    "GameBootstrapper.cs",
                    "BootstrapPlayModeEntryGuard.cs",
                    "02_HECTON_WORLD.unity",
                    "HECTON_SANDBOX_BIOMES_MAPMAGIC_GRAPH.asset",
                    "PC_Renderer.asset"
                };

                foreach (var targetFile in targetFiles)
                {
                    try
                    {
                        string diffStat = H8Utils.RunGit($"diff --stat -- \"*{targetFile}\"");
                        if (!string.IsNullOrEmpty(diffStat) && !diffStat.StartsWith("<git"))
                        {
                            string trimmed = diffStat.Trim();
                            if (!string.IsNullOrEmpty(trimmed))
                            {
                                info.targetedDiffs.Add(new H8KV(
                                    targetFile,
                                    H8Utils.Truncate(trimmed, opts.maxTextLengthPerValue)));
                            }
                        }
                        else
                        {
                            info.targetedDiffs.Add(new H8KV(targetFile, "no changes"));
                        }
                    }
                    catch
                    {
                        info.targetedDiffs.Add(new H8KV(targetFile, "<diff_error>"));
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectGitInfo failed: {e.Message}");
            }
            return info;
        }

        // ── 13. Full Snapshot ────────────────────────────────────────────────

        public static H8DiagnosticSnapshot CollectFullSnapshot(H8DiagnosticOptions opts, string mode)
        {
            var snapshot = new H8DiagnosticSnapshot();
            try
            {
                snapshot.timestamp = H8Utils.Timestamp();
                snapshot.mode = mode ?? "unknown";
                snapshot.sceneName = SceneManager.GetActiveScene().name;

                // Play mode elapsed time
                if (EditorApplication.isPlaying)
                    snapshot.playModeElapsedSeconds = Time.realtimeSinceStartup;

                // Collect all sections — each is individually wrapped
                snapshot.project = CollectProjectMetadata(opts);
                snapshot.scenes = CollectSceneInfo(opts);
                snapshot.keyObjects = CollectKeyObjects(opts);
                snapshot.bootstrap = CollectBootstrapInfo(opts);
                snapshot.registry = CollectRegistryInfo(opts);
                snapshot.mapMagic = CollectMapMagicInfo(opts);
                snapshot.crest = CollectCrestInfo(opts);
                snapshot.atmosphere = CollectAtmosphereInfo(opts);
                snapshot.urp = CollectUrpInfo(opts);
                snapshot.cameras = CollectCameras(opts);
                snapshot.console = CollectConsoleInfo(opts);
                snapshot.git = CollectGitInfo(opts);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{TAG} CollectFullSnapshot failed: {e.Message}");
            }
            return snapshot;
        }
    }
}
