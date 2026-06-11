using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class AutoRunDiagnostic
{
    static AutoRunDiagnostic()
    {
        // To prevent an infinite loop, we delete the script right after running!
        EditorApplication.delayCall += RunAndSelfDestruct;
    }

    private static void RunAndSelfDestruct()
    {
        Debug.Log("[AutoRunDiagnostic] Triggering diagnostic run...");
        
        var opts = new Hecton8.BlackboxDiagnostics.H8DiagnosticOptions
        {
            includeInactiveObjects = true,
            includeReflectionDump = true,
            includeConsoleLog = true,
            includeEditorLogTail = true,
            includeGitDiff = true,
            includePlayModeDiff = true,
            playModeWaitSeconds = 15f
        };
        
        // Use Full Comparison as the user wants
        Hecton8.BlackboxDiagnostics.H8Runner.RunFullComparison(opts);

        Debug.Log("[AutoRunDiagnostic] Run finished. Deleting auto-runner script.");
        
        // Self-destruct
        string path = "Assets/Editor/AutoRunDiagnostic.cs";
        if (System.IO.File.Exists(path))
        {
            AssetDatabase.DeleteAsset(path);
        }
    }
}
