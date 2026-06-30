#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using System.Collections.Generic;
using GPUInstancer;

public static class GPUInstancerUtilityBenchmark
{
    [MenuItem("Tools/Benchmark Prefab Instances")]
    public static void RunBenchmark()
    {
        // We will just extract the logic and benchmark the baseline vs optimized

        GameObject prefab = new GameObject("TestPrefab");
        var prefabScript = prefab.AddComponent<GPUInstancerPrefab>();
        prefabScript.prefabPrototype = ScriptableObject.CreateInstance<GPUInstancerPrefabPrototype>();

        string path = "Assets/TestPrefab.prefab";
        GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(prefab, path);

        List<GameObject> instances = new List<GameObject>();
        for (int i = 0; i < 10000; i++)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(savedPrefab);
            instances.Add(inst);
        }

        UnityEngine.Debug.Log("Created 10000 instances");

        GPUInstancerPrefab[] prefabInstances = GameObject.FindObjectsByType<GPUInstancerPrefab>(FindObjectsInactive.Include);

        // Baseline
        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < prefabInstances.Length; i++)
        {
            UnityEngine.Object prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstances[i].gameObject);
            if (prefabRoot != null && ((GameObject)prefabRoot).GetComponent<GPUInstancerPrefab>() != null && prefabInstances[i].prefabPrototype != ((GameObject)prefabRoot).GetComponent<GPUInstancerPrefab>().prefabPrototype)
            {
                // prefabInstances[i].prefabPrototype = ((GameObject)prefabRoot).GetComponent<GPUInstancerPrefab>().prefabPrototype;
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log("Baseline time: " + sw.ElapsedMilliseconds + "ms");

        // Optimized
        sw.Restart();
        Dictionary<UnityEngine.Object, GPUInstancerPrefab> prefabRootCache = new Dictionary<UnityEngine.Object, GPUInstancerPrefab>();
        for (int i = 0; i < prefabInstances.Length; i++)
        {
            UnityEngine.Object prefabRoot = PrefabUtility.GetCorrespondingObjectFromSource(prefabInstances[i].gameObject);
            if (prefabRoot != null)
            {
                if (!prefabRootCache.TryGetValue(prefabRoot, out GPUInstancerPrefab rootPrefabScript))
                {
                    rootPrefabScript = ((GameObject)prefabRoot).GetComponent<GPUInstancerPrefab>();
                    prefabRootCache[prefabRoot] = rootPrefabScript;
                }

                if (rootPrefabScript != null && prefabInstances[i].prefabPrototype != rootPrefabScript.prefabPrototype)
                {
                    // prefabInstances[i].prefabPrototype = rootPrefabScript.prefabPrototype;
                }
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log("Optimized time: " + sw.ElapsedMilliseconds + "ms");

        foreach (var inst in instances)
        {
            GameObject.DestroyImmediate(inst);
        }
        AssetDatabase.DeleteAsset(path);
    }
}
#endif
