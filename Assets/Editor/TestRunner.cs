using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class TestRunner
{
    public static void Run()
    {
        Debug.Log("Starting TestRunner!");
        Hecton8.BlackboxDiagnostics.H8Runner.RunBootstrapScenePlayMode(new Hecton8.BlackboxDiagnostics.H8DiagnosticOptions { 
            playModeWaitSeconds = 5f,
            includeInactiveObjects = true,
            includeConsoleLog = true
        });
    }
}
