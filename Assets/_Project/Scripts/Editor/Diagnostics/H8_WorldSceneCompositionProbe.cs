using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Read-only batchmode probe: reports what a scene actually CONTAINS.
    ///
    /// Exists because several project scenes (02_HECTON_WORLD, 010_TEST, 020_RENDER_SANDBOX*) are
    /// stored in Unity's BINARY serialization format even though EditorSettings says Force Text.
    /// Text tools - grep for `m_Name:`, grep for a script GUID - silently find nothing in those
    /// files and read as "the scene is empty", which is a false negative, not a result.
    /// Anything asking "is X wired into the world scene?" must go through the object model.
    /// </summary>
    public static class H8_WorldSceneCompositionProbe
    {
        private const string Marker = "[H8_SCENE_PROBE]";

        private static readonly string[] ProbedScenes =
        {
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
            "Assets/_Project/Scenes/00_BOOTSTRAP.unity",
        };

        public static void Run()
        {
            foreach (string scenePath in ProbedScenes)
            {
                try
                {
                    ProbeScene(scenePath);
                }
                catch (Exception ex)
                {
                    Debug.Log($"{Marker} FAILED {scenePath}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Debug.Log($"{Marker} DONE");
        }

        private static void ProbeScene(string scenePath)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.Log($"{Marker} {scenePath} -> INVALID");
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            int totalObjects = 0;
            var componentCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var rendererShaders = new Dictionary<string, int>(StringComparer.Ordinal);
            var missingScripts = 0;

            foreach (GameObject root in roots)
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    totalObjects++;
                    foreach (Component c in t.GetComponents<Component>())
                    {
                        if (c == null)
                        {
                            missingScripts++;
                            continue;
                        }

                        string typeName = c.GetType().Name;
                        componentCounts.TryGetValue(typeName, out int n);
                        componentCounts[typeName] = n + 1;

                        if (c is Renderer renderer)
                        {
                            foreach (Material m in renderer.sharedMaterials)
                            {
                                string key = m == null
                                    ? "<null material>"
                                    : (m.shader == null ? $"{m.name} -> <null shader>" : m.shader.name);
                                rendererShaders.TryGetValue(key, out int rn);
                                rendererShaders[key] = rn + 1;
                            }
                        }
                    }
                }
            }

            Debug.Log($"{Marker} SCENE {scenePath} roots={roots.Length} objects={totalObjects} missingScripts={missingScripts}");

            ReportNamedComponent(scene, "HectonVoxelEngine");
            ReportNamedComponent(scene, "HectonVoxelVolume");
            ReportNamedComponent(scene, "GameBootstrapper");

            Debug.Log($"{Marker}   distinct component types = {componentCounts.Count}");
            Debug.Log($"{Marker}   shaders in use: {Summarize(rendererShaders, 14)}");
            Debug.Log($"{Marker}   top components: {Summarize(componentCounts, 18)}");

            ReportNullMaterialRenderers(scene);
            ReportTerrains(scene);
            ReportProceduralTerrainDrivers(scene);
        }

        /// <summary>
        /// Reports whatever is driving the terrain procedurally (MapMagic and friends) WITHOUT taking a
        /// compile-time dependency on its API: matched by type name, walked with SerializedObject.
        /// The question this answers is why every Terrain tile reports layers=0 / alphamaps=0.
        /// </summary>
        private static void ReportProceduralTerrainDrivers(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null)
                        continue;

                    string typeName = c.GetType().FullName ?? string.Empty;
                    if (typeName.IndexOf("MapMagic", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    // Only the driver objects, not every per-tile helper.
                    if (typeName.IndexOf("Tile", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    Debug.Log($"{Marker}   MAPMAGIC {typeName} @ {GetHierarchyPath(c.transform)} enabled={(c is Behaviour b ? b.enabled.ToString() : "n/a")}");

                    var so = new SerializedObject(c);
                    SerializedProperty it = so.GetIterator();
                    while (it.NextVisible(true))
                    {
                        if (it.depth > 1)
                            continue;

                        if (it.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            UnityEngine.Object reference = it.objectReferenceValue;
                            Debug.Log($"{Marker}     {it.propertyPath} = " +
                                      (reference == null
                                          ? "<NULL>"
                                          : $"{reference.name} ({reference.GetType().Name}) @ {AssetDatabase.GetAssetPath(reference)}"));

                            if (reference != null && it.propertyPath.IndexOf("graph", StringComparison.OrdinalIgnoreCase) >= 0)
                                ReportGraphOutputs(reference);
                        }
                        else if (it.isArray && it.propertyType != SerializedPropertyType.String)
                        {
                            Debug.Log($"{Marker}     {it.propertyPath}[] length={it.arraySize}");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// A MapMagic graph only produces splat/control data if it contains texture Output generators.
        /// Reports the generator type names by reflection so a missing output node is visible.
        /// </summary>
        private static void ReportGraphOutputs(UnityEngine.Object graphAsset)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var outputs = new Dictionary<string, int>(StringComparer.Ordinal);

            var so = new SerializedObject(graphAsset);
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ManagedReference)
                    continue;

                string full = it.managedReferenceFullTypename;
                if (string.IsNullOrEmpty(full))
                    continue;

                int lastDot = full.LastIndexOf('.');
                string shortName = lastDot >= 0 && lastDot < full.Length - 1 ? full.Substring(lastDot + 1) : full;

                counts.TryGetValue(shortName, out int n);
                counts[shortName] = n + 1;

                if (shortName.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    outputs.TryGetValue(shortName, out int on);
                    outputs[shortName] = on + 1;
                }
            }

            Debug.Log($"{Marker}       GRAPH {graphAsset.name}: generators={counts.Count} distinct");
            Debug.Log($"{Marker}       GRAPH OUTPUT nodes: {Summarize(outputs, 12)}");
            Debug.Log($"{Marker}       GRAPH all nodes: {Summarize(counts, 20)}");
        }

        private static void ReportNullMaterialRenderers(Scene scene)
        {
            int nullSlots = 0;
            int activeInHierarchy = 0;
            int rendererEnabled = 0;
            var byName = new Dictionary<string, int>(StringComparer.Ordinal);
            var byRootName = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = r.sharedMaterials;
                    bool hasNull = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] != null)
                            continue;

                        nullSlots++;
                        hasNull = true;
                    }

                    if (!hasNull)
                        continue;

                    if (r.gameObject.activeInHierarchy)
                        activeInHierarchy++;
                    if (r.enabled)
                        rendererEnabled++;

                    string name = r.gameObject.name;
                    // Collapse "Rock_014 (3)" style clones into one bucket.
                    int trim = name.IndexOf(" (", StringComparison.Ordinal);
                    if (trim > 0)
                        name = name.Substring(0, trim);
                    name = name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_', '-', ' ');

                    byName.TryGetValue(name, out int n);
                    byName[name] = n + 1;

                    byRootName.TryGetValue(root.name, out int rn);
                    byRootName[root.name] = rn + 1;
                }
            }

            Debug.Log($"{Marker}   NULL-MATERIAL slots={nullSlots} activeInHierarchy={activeInHierarchy} rendererEnabled={rendererEnabled}");
            Debug.Log($"{Marker}     by object name: {Summarize(byName, 12)}");
            Debug.Log($"{Marker}     by scene root : {Summarize(byRootName, 12)}");
        }

        private static void ReportTerrains(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData data = terrain.terrainData;
                    Material template = terrain.materialTemplate;
                    string layers = "<no data>";
                    if (data != null)
                    {
                        var sb = new StringBuilder();
                        TerrainLayer[] terrainLayers = data.terrainLayers;
                        for (int i = 0; i < terrainLayers.Length; i++)
                        {
                            if (sb.Length > 0)
                                sb.Append('/');
                            sb.Append(terrainLayers[i] != null ? terrainLayers[i].name : "<null>");
                        }

                        layers = $"layers={terrainLayers.Length}[{sb}] alphamaps={data.alphamapTextureCount} res={data.alphamapResolution}";
                    }

                    Debug.Log($"{Marker}   TERRAIN {GetHierarchyPath(terrain.transform)} " +
                              $"material={(template != null ? template.name : "<NULL>")} " +
                              $"shader={(template != null && template.shader != null ? template.shader.name : "<none>")} {layers}");
                }
            }
        }

        private static void ReportNamedComponent(Scene scene, string typeName)
        {
            int found = 0;
            var details = new StringBuilder();

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || !string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal))
                        continue;

                    found++;
                    if (details.Length > 0)
                        details.Append(" | ");
                    details.Append(GetHierarchyPath(c.transform));

                    SerializedObject so = new SerializedObject(c);
                    SerializedProperty mat = so.FindProperty("voxelMaterial");
                    if (mat != null)
                    {
                        Material assigned = mat.objectReferenceValue as Material;
                        details.Append(assigned == null
                            ? " voxelMaterial=<NULL>"
                            : $" voxelMaterial={assigned.name} shader={(assigned.shader != null ? assigned.shader.name : "<null>")}");
                    }
                }
            }

            Debug.Log($"{Marker}   {typeName}: count={found}{(found > 0 ? " @ " + details : string.Empty)}");
        }

        /// <summary>
        /// Reverse-dependency audit for .compute assets, run through AssetDatabase rather than text search.
        ///
        /// A raw GUID grep CANNOT answer this: four project scenes are binary-serialized, so a
        /// ComputeShader assigned to a serialized field in one of them stores its GUID as 16 raw bytes and
        /// a text scan reports the asset as unreferenced. AssetDatabase.GetDependencies reads the real
        /// dependency graph and sees through both serialization formats.
        /// </summary>
        public static void RunOrphanComputeAudit()
        {
            string[] computeGuids = AssetDatabase.FindAssets("t:ComputeShader", new[] { "Assets/_Project" });
            var computePaths = new List<string>();
            foreach (string guid in computeGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    computePaths.Add(path);
            }

            // Everything that could hold a reference: scenes, prefabs, ScriptableObjects, materials.
            var referrerPaths = new List<string>();
            foreach (string filter in new[] { "t:Scene", "t:Prefab", "t:ScriptableObject", "t:Material" })
            {
                foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        referrerPaths.Add(path);
                }
            }

            var referenced = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string referrer in referrerPaths)
            {
                foreach (string dependency in AssetDatabase.GetDependencies(referrer, true))
                {
                    if (!dependency.EndsWith(".compute", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!referenced.TryGetValue(dependency, out List<string> list))
                    {
                        list = new List<string>();
                        referenced[dependency] = list;
                    }

                    if (list.Count < 4)
                        list.Add(referrer);
                }
            }

            Debug.Log($"{Marker} COMPUTE AUDIT: {computePaths.Count} .compute under Assets/_Project, " +
                      $"{referenced.Count} reachable from a Scene/Prefab/ScriptableObject/Material dependency graph");

            computePaths.Sort(StringComparer.Ordinal);
            foreach (string path in computePaths)
            {
                if (referenced.TryGetValue(path, out List<string> referrers))
                    Debug.Log($"{Marker}   REFERENCED {System.IO.Path.GetFileName(path)} <- {string.Join(", ", referrers)}");
                else
                    Debug.Log($"{Marker}   ORPHAN     {path}");
            }

            Debug.Log($"{Marker} DONE");
        }

        private static string GetHierarchyPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null)
            {
                t = t.parent;
                path = t.name + "/" + path;
            }

            return path;
        }

        private static string Summarize(Dictionary<string, int> counts, int take)
        {
            var list = new List<KeyValuePair<string, int>>(counts);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));

            var sb = new StringBuilder();
            for (int i = 0; i < list.Count && i < take; i++)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(list[i].Key).Append('=').Append(list[i].Value);
            }

            if (list.Count > take)
                sb.Append(", ...+").Append(list.Count - take).Append(" more");

            return sb.Length == 0 ? "<none>" : sb.ToString();
        }
    }
}
