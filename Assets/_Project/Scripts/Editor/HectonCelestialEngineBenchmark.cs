using UnityEngine;
using UnityEditor;
using System.Diagnostics;
using Hecton8.Celestial;
using Hecton8.Atmosphere;

public class HectonCelestialEngineBenchmark
{
    [MenuItem("Tools/Benchmark CacheMoonRenderers")]
    public static void RunBenchmark()
    {
        GameObject root = new GameObject("TestRoot");
        var engine = root.AddComponent<HectonCelestialEngine>();

        // Add 10,000 bodies
        for (int i = 0; i < 10000; i++)
        {
            var go = new GameObject("Body" + i);
            go.transform.SetParent(root.transform);
            var body = go.AddComponent<ObserverRelativeCelestialBody>();
            go.AddComponent<MeshRenderer>();
        }

        // We need to call CacheMoonRenderers. Since it's private, we'll use reflection.
        var method = typeof(HectonCelestialEngine).GetMethod("CacheMoonRenderers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (method == null)
        {
            UnityEngine.Debug.LogError("Could not find CacheMoonRenderers method");
            return;
        }

        // Warmup
        method.Invoke(engine, null);

        Stopwatch sw = new Stopwatch();
        sw.Start();
        for (int i = 0; i < 100; i++)
        {
            method.Invoke(engine, null);
        }
        sw.Stop();

        UnityEngine.Debug.Log($"CacheMoonRenderers x100 took: {sw.ElapsedMilliseconds} ms");

        GameObject.DestroyImmediate(root);
    }
}
