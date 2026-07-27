using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Editor
{
    /// <summary>
    /// Finds and persists ComputeShader references that exist only in memory.
    ///
    /// Several systems resolve their kernels in OnValidate and call EditorUtility.SetDirty.
    /// OnValidate fires when Unity loads the scene, so the reference is present the moment the scene
    /// is open - and evaporates unless somebody saves. An assignment that is never written is the
    /// classic "works in the editor, null in the player build" defect.
    ///
    /// Two traps make this hard to measure, and both were hit while writing this:
    ///
    /// 1. scene.isDirty is worthless here. EditorUtility.SetDirty on a scene object does NOT set it
    ///    (only EditorSceneManager.MarkSceneDirty does), so it reads False whether or not OnValidate
    ///    just invented a reference in memory.
    /// 2. Counting nulls after OpenScene measures MEMORY, not the file. OnValidate has already run by
    ///    the time OpenScene returns, so a fully-resolved graph looks identical either way.
    ///
    /// The one honest signal is AssetDatabase.GetDependencies, which reads the serialized file. A
    /// reference held in memory but absent from the scene's dependencies is, precisely, unpersisted.
    /// No hardcoded kernel list: whatever the loaded graph points at is compared against what the
    /// file admits to, so this keeps working as systems are added.
    /// </summary>
    public static class GpuKernelReferencePersist
    {
        private const string Marker = "[H8_GPU_PERSIST]";

        private static readonly string[] TargetScenes =
        {
            "Assets/_Project/Scenes/02_HECTON_WORLD.unity",
            "Assets/_Project/Scenes/010_TEST.unity",
        };

        [MenuItem("Hecton8/GPU/Report Unpersisted Kernel References")]
        public static void ReportPersist()
        {
            Run(false);
        }

        [MenuItem("Hecton8/GPU/Persist Kernel References")]
        public static void ApplyPersist()
        {
            Run(true);
        }

        private static void Run(bool apply)
        {
            int saved = 0;
            int clean = 0;

            foreach (string scenePath in TargetScenes)
            {
                try
                {
                    // Disk truth, captured before the loaded graph can muddy it.
                    var onDisk = new HashSet<string>(
                        AssetDatabase.GetDependencies(scenePath, true), StringComparer.Ordinal);

                    Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    if (!scene.IsValid())
                    {
                        Debug.LogError($"{Marker} INVALID {scenePath}");
                        continue;
                    }

                    int objects = 0;
                    var inMemory = new Dictionary<string, string>(StringComparer.Ordinal);
                    var stillNull = new List<string>();

                    GameObject[] roots = scene.GetRootGameObjects();
                    foreach (GameObject root in roots)
                        InspectObject(root, ref objects, inMemory, stillNull);

                    var unpersisted = new List<string>();
                    foreach (KeyValuePair<string, string> pair in inMemory)
                    {
                        if (!onDisk.Contains(pair.Key))
                            unpersisted.Add($"{System.IO.Path.GetFileNameWithoutExtension(pair.Key)} <- {pair.Value}");
                    }

                    string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
                    Debug.Log($"{Marker} {sceneName}: roots={roots.Length} objects={objects} " +
                              $"computeRefsInMemory={inMemory.Count} unpersisted={unpersisted.Count} null={stillNull.Count}");

                    if (stillNull.Count > 0)
                        Debug.Log($"{Marker}   null -> {string.Join(", ", stillNull)}");

                    if (unpersisted.Count == 0)
                    {
                        clean++;
                        Debug.Log($"{Marker}   every in-memory kernel is already in the scene's dependencies, not writing");
                        continue;
                    }

                    Debug.Log($"{Marker}   UNPERSISTED -> {string.Join(" | ", unpersisted)}");

                    if (!apply)
                    {
                        Debug.Log($"{Marker}   WOULD SAVE");
                        continue;
                    }

                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    saved++;
                    Debug.Log($"{Marker}   SAVED");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{Marker} FAILED {scenePath}: {ex.GetType().Name}: {ex.Message}");
                }
            }

            Debug.Log($"{Marker} DONE [{(apply ? "APPLIED" : "DRY-RUN")}] saved={saved} alreadyClean={clean}");
        }

        private static void InspectObject(
            GameObject target,
            ref int objects,
            Dictionary<string, string> inMemory,
            List<string> stillNull)
        {
            objects++;

            foreach (Component component in target.GetComponents<Component>())
            {
                if (component == null)
                    continue;

                var serialized = new SerializedObject(component);
                SerializedProperty iterator = serialized.GetIterator();
                while (iterator.NextVisible(true))
                {
                    // "PPtr<$ComputeShader>" stays correct while the reference is null, which is the point.
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference ||
                        iterator.type != "PPtr<$ComputeShader>")
                        continue;

                    string owner = $"{component.GetType().Name}.{iterator.propertyPath}";
                    if (iterator.objectReferenceValue == null)
                    {
                        stillNull.Add(owner);
                        continue;
                    }

                    string assetPath = AssetDatabase.GetAssetPath(iterator.objectReferenceValue);
                    if (!string.IsNullOrEmpty(assetPath))
                        inMemory[assetPath] = owner;
                }
            }

            foreach (Transform child in target.transform)
                InspectObject(child.gameObject, ref objects, inMemory, stillNull);
        }
    }
}
