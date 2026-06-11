using UnityEditor;
using UnityEngine;

public static class TestRunner2
{
    public static void Run()
    {
        Debug.Log("Starting TestRunner2 RunSelfCheck...");
        var opts = new Hecton8.BlackboxDiagnostics.H8DiagnosticOptions
        {
            includeInactiveObjects = true,
            includeReflectionDump = true,
            includeConsoleLog = true
        };
        Hecton8.BlackboxDiagnostics.H8Runner.RunSelfCheck(opts);
        Debug.Log("RunSelfCheck Complete.");
    }
}
