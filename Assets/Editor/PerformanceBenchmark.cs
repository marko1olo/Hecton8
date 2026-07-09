#if false
using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using CandiceAIforGames.AI;

public static class PerformanceBenchmark
{
    public static void Run()
    {
        var go = new GameObject("TestGO");
        go.AddComponent<CandiceAIController>();

        int iterations = 100000;

        Stopwatch sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            var agent = go.GetComponent<CandiceAIController>();
            var player = go.GetComponent<CandiceAIPlayerController>();
        }
        sw.Stop();
        UnityEngine.Debug.Log($"[Benchmark] Baseline (2x GetComponent): {sw.ElapsedMilliseconds} ms");

        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            if (go.TryGetComponent<CandiceAIController>(out var agent))
            {
                // defer second GetComponent
            }
        }
        sw.Stop();
        UnityEngine.Debug.Log($"[Benchmark] Optimized (1x TryGetComponent): {sw.ElapsedMilliseconds} ms");

        EditorApplication.Exit(0);
    }
}
#endif
