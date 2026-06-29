using System;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using AmplifyImpostors;

public class AmplifyImpostorBenchmark : MonoBehaviour
{
    public void Start()
    {
        // Setup mock data for testing
        GameObject root = new GameObject("Root");
        AmplifyImpostor impostor = root.AddComponent<AmplifyImpostor>();

        List<Renderer> renderers = new List<Renderer>();
        for (int i = 0; i < 100; i++)
        {
            GameObject child = new GameObject("Child" + i);
            child.transform.parent = root.transform;
            MeshFilter mf = child.AddComponent<MeshFilter>();
            mf.sharedMesh = new Mesh(); // Dummy mesh
            MeshRenderer mr = child.AddComponent<MeshRenderer>();
            renderers.Add(mr);
        }
        impostor.Renderers = renderers.ToArray();
        impostor.Data = ScriptableObject.CreateInstance<AmplifyImpostorAsset>();
        impostor.Data.HorizontalFrames = 16; // Gives hframes = 16, vframes = 16

        Stopwatch sw = new Stopwatch();
        sw.Start();
        for (int i = 0; i < 100; i++)
        {
            impostor.CalculateSheetBounds(ImpostorType.Octahedron);
        }
        sw.Stop();

        UnityEngine.Debug.Log("Benchmark CalculateSheetBounds: " + sw.ElapsedMilliseconds + " ms");
    }
}
