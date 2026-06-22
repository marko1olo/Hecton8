using UnityEngine;
using UnityEditor;
using Hecton8.Interaction;
using System.Diagnostics;
using System.Reflection;

public class PickupItemOptimizationBenchmark
{
    [MenuItem("Tools/Benchmark PickupItem Optimization")]
    public static void RunBenchmark()
    {
        var sw = Stopwatch.StartNew();
        GameObject root = new GameObject("BenchmarkRoot");

        PickupItem[] items = new PickupItem[2000];
        for (int i = 0; i < 2000; i++)
        {
            GameObject go = new GameObject($"Pickup_{i}");
            go.transform.parent = root.transform;
            items[i] = go.AddComponent<PickupItem>();
            items[i].persistWorldState = true;
        }

        sw.Stop();
        UnityEngine.Debug.Log($"[Benchmark] Created 2000 PickupItems in {sw.ElapsedMilliseconds} ms.");

        sw.Restart();
        MethodInfo method = typeof(PickupItem).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (method != null)
        {
            for (int i = 0; i < 2000; i++)
            {
                method.Invoke(items[i], null);
            }
        }
        else
        {
            UnityEngine.Debug.LogError("[Benchmark] OnValidate method not found.");
        }
        sw.Stop();

        UnityEngine.Debug.Log($"[Benchmark] OnValidate 2000 times took {sw.ElapsedMilliseconds} ms.");

        GameObject.DestroyImmediate(root);
    }
}
