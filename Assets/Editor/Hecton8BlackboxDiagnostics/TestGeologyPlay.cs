using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

[InitializeOnLoad]
public static class TestGeologyPlay
{
    private const string HAS_RUN_KEY = "TestGeologyPlay_HasRun_v3";

    // No auto-trigger.
    [MenuItem("Hecton8/Debug/Run Geology Test")]
    public static void RunTest()
    {
        Hecton8.BlackboxDiagnostics.H8BlackboxWindow.RunFullComparisonFromMenu();
    }

    private static IEnumerator TestRoutine()
    {
        EditorSceneManager.SaveOpenScenes();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_BOOTSTRAP.unity", OpenSceneMode.Single);
        
        // Clear log
        var logEntries = System.Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
        logEntries.GetMethod("Clear").Invoke(null, null);

        EditorApplication.isPlaying = true;
        
        // Wait 15 seconds real time
        double startTime = EditorApplication.timeSinceStartup;
        while (EditorApplication.timeSinceStartup - startTime < 15.0)
        {
            yield return null;
        }

        var logs = Hecton8.BlackboxDiagnostics.H8Utils.GetConsoleLogs(200);
        List<string> lines = new List<string>();
        foreach(var l in logs) {
            lines.Add($"[{l.type}] {l.message}");
        }
        
        System.IO.File.WriteAllLines(@"C:\hades\Hecton8\geology_test_logs.txt", lines);
        Debug.Log("Wrote geology_test_logs.txt");

        EditorApplication.isPlaying = false;
        EditorApplication.Exit(0);
    }
}

// Simple coroutine for Editor
public class EditorCoroutine
{
    private IEnumerator routine;
    public static void Start(IEnumerator routine)
    {
        var runner = new EditorCoroutine();
        runner.routine = routine;
        EditorApplication.update += runner.Update;
    }
    void Update()
    {
        if (!routine.MoveNext())
            EditorApplication.update -= Update;
    }
}
