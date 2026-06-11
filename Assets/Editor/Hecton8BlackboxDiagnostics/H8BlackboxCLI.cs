// H8BlackboxCLI.cs — Command-line interface for Hecton8 Blackbox Diagnostics
using System;
using UnityEditor;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    /// <summary>
    /// Headless entry points for CI/CD or Agent-driven diagnostics.
    /// Usage: Unity.exe -quit -batchmode -projectPath "C:\path" -executeMethod Hecton8.BlackboxDiagnostics.H8CLI.RunEditMode
    /// </summary>
    public static class H8CLI
    {
        private static H8DiagnosticOptions GetDefaultOpts()
        {
            return new H8DiagnosticOptions
            {
                includeInactiveObjects = true,
                includeReflectionDump = true,
                includeConsoleLog = true,
                includeEditorLogTail = true,
                includeGitDiff = true,
                includePlayModeDiff = true,
                playModeWaitSeconds = 60f
            };
        }

        public static void RunSelfCheck()
        {
            Debug.Log("[H8Blackbox] CLI starting RunSelfCheck...");
            H8Runner.RunSelfCheck(GetDefaultOpts());
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void RunEditMode()
        {
            Debug.Log("[H8Blackbox] CLI starting RunEditMode...");
            try
            {
                H8Runner.RunEditMode(GetDefaultOpts());
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[H8Blackbox] CLI RunEditMode failed: {e.Message}\n{e.StackTrace}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static void RunCurrentScenePlayMode()
        {
            Debug.Log("[H8Blackbox] CLI starting RunCurrentScenePlayMode...");
            H8Runner.RunCurrentScenePlayMode(GetDefaultOpts());
            // PlayMode runs async, so batch mode exit might need to be handled by the runner or event,
            // but for now we just start it.
        }

        public static void RunBootstrapScenePlayMode()
        {
            Debug.Log("[H8Blackbox] CLI starting RunBootstrapScenePlayMode...");
            H8Runner.RunBootstrapScenePlayMode(GetDefaultOpts());
            // Need to let it run, so we can't exit here. We rely on the callback.
        }

        public static void RunFullComparison()
        {
            Debug.Log("[H8Blackbox] CLI starting RunFullComparison...");
            if (Application.isBatchMode)
            {
                Debug.LogWarning("[H8Blackbox] FullComparison PlayMode requires interactive editor. Run from menu.");
                string outDir = System.IO.Path.Combine(H8Utils.GetProjectRoot(), H8Utils.OutputRootName, $"Hecton8_Blackbox_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}_FullComparison_Aborted");
                System.IO.Directory.CreateDirectory(outDir);
                
                var pSummary = new H8RunSummary 
                { 
                    mode = "FullComparison", 
                    success = false, 
                    partialSuccess = true, 
                    abortReason = "Batchmode not supported for FullComparison", 
                    outputPath = outDir 
                };
                pSummary.warnings.Add("Batchmode not supported for FullComparison. Exiting gracefully.");
                H8Writers.WriteRunSummary(outDir, pSummary);
                H8Utils.WriteFile(System.IO.Path.Combine(outDir, "full_comparison_report.md"), "# Full Comparison Aborted\nBatchmode not supported for FullComparison PlayMode state machine.");
                
                EditorApplication.Exit(0);
                return;
            }
            H8Runner.RunFullComparison(GetDefaultOpts());
        }
    }
}
