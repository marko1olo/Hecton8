#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Diagnostics;
using AmplifyImpostors;
using System.Collections.Generic;

public static class BenchmarkRunner
{
    [MenuItem("Tools/Run Benchmark")]
    public static void RunBenchmark()
    {
        GameObject go = new GameObject();
        AmplifyImpostor impostor = go.AddComponent<AmplifyImpostor>();

        // Add a bunch of renderers
        Renderer[] renderers = new Renderer[100];
        for(int i = 0; i < 100; i++)
        {
            GameObject child = new GameObject();
            child.transform.parent = go.transform;
            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            MeshFilter mf = child.AddComponent<MeshFilter>();
            Mesh mesh = new Mesh();
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            mf.sharedMesh = mesh;
            renderers[i] = mr;
        }

        impostor.Renderers = renderers;
        impostor.RootTransform = go.transform;

        // Setup some data
        AmplifyImpostorAsset asset = ScriptableObject.CreateInstance<AmplifyImpostorAsset>();
        impostor.Data = asset;
        asset.HorizontalFrames = 16;
        asset.VerticalFrames = 16;

        Stopwatch sw = new Stopwatch();
        sw.Start();

        for(int i = 0; i < 100; i++)
        {
            impostor.CalculateSheetBounds(ImpostorType.Spherical);
        }

        sw.Stop();
        UnityEngine.Debug.Log("CalculateSheetBounds x 100: " + sw.ElapsedMilliseconds + "ms");

        sw.Reset();
        sw.Start();
        for(int i = 0; i < 100; i++)
        {
            // Call RenderImpostor
            // impostor.RenderImpostor(ImpostorType.Spherical, 1, false, false);
            // It has to do some RT stuff, so maybe just bounds is enough for benchmark
        }
        sw.Stop();

        GameObject.DestroyImmediate(go);
    }
}
#endif

