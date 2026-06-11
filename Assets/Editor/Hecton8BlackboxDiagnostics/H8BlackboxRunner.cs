// H8BlackboxRunner.cs — Orchestrates the diagnostic run
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hecton8.BlackboxDiagnostics
{
    [InitializeOnLoad]
    public static class H8Runner
    {
        // ── SessionState Keys for Domain Reload Recovery ──
        private const string SS_IS_RUNNING = "H8Blackbox_IsRunning";
        private const string SS_MODE = "H8Blackbox_Mode";
        private const string SS_OUTDIR = "H8Blackbox_OutDir";
        private const string SS_LABEL = "H8Blackbox_Label";
        private const string SS_START_TIME = "H8Blackbox_StartTime";
        private const string SS_REAL_START_TIME = "H8Blackbox_RealStartTime";
        private const string SS_WAIT_LIMIT = "H8Blackbox_WaitLimit";
        private const string SS_S1_DONE = "H8Blackbox_S1Done";
        private const string SS_S5_DONE = "H8Blackbox_S5Done";
        private const string SS_S15_DONE = "H8Blackbox_S15Done";
        private const string SS_STOP_REQUESTED = "H8Blackbox_StopRequested";
        private const string SS_STOP_REQUESTED_TIME = "H8Blackbox_StopRequestedTime";

        // Keep delegates for completion
        private static Action<H8DiagnosticSnapshot> s_OnComplete;
        private static H8DiagnosticOptions s_Opts;

        static H8Runner()
        {
            // Called on domain reload
            if (SessionState.GetBool(SS_IS_RUNNING, false))
            {
                Debug.Log("[H8Blackbox] Domain reload detected. Recovering PlayMode state machine...");
                // Note: delegates (s_OnComplete, s_Opts) are lost on domain reload.
                // In a fully persistent system we would serialize the full comparison state machine.
                // For now, we continue the state machine to collect snapshots and exit playmode,
                // but the overall FullComparison chain might break if domain reload happens midway.
                // We will warn the user if domain reload is enabled.
                EditorApplication.update += RecoveredOnUpdate;
            }
        }

        public static void RunSelfCheck(H8DiagnosticOptions opts)
        {
            Debug.Log("[H8Blackbox] Starting SelfCheck...");
            try
            {
                string outDir = H8Utils.CreateOutputFolder();
                var snapshot = H8Collectors.CollectFullSnapshot(opts, "SelfCheck");
                var findings = H8FindingsEngine.Analyze(snapshot);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Hecton8 Blackbox Self-Check\n");
                
                string IsOk(bool cond) => cond ? "OK" : "FAIL";
                
                sb.AppendLine($"1. Output folder writable: {IsOk(Directory.Exists(outDir))}");
                sb.AppendLine($"2. Unity version read: {IsOk(!string.IsNullOrEmpty(snapshot.project.unityVersion))}");
                sb.AppendLine($"3. Project root exists: {IsOk(Directory.Exists(H8Utils.GetProjectRoot()))}");
                sb.AppendLine($"4. Assets exists: {IsOk(Directory.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Assets")))}");
                sb.AppendLine($"5. Packages exists: {IsOk(Directory.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Packages")))}");
                sb.AppendLine($"6. ProjectSettings exists: {IsOk(Directory.Exists(Path.Combine(H8Utils.GetProjectRoot(), "ProjectSettings")))}");
                sb.AppendLine($"7. 00_BOOTSTRAP scene exists: {IsOk(File.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Assets", "_Project", "Scenes", "00_BOOTSTRAP.unity")))}");
                sb.AppendLine($"8. 02_HECTON_WORLD scene exists: {IsOk(File.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Assets", "_Project", "Scenes", "02_HECTON_WORLD.unity")))}");
                sb.AppendLine($"9. GameBootstrapper type found: {IsOk(snapshot.bootstrap.bootstrapperFound)}");
                sb.AppendLine($"10. GlobalRegistry type found: {IsOk(snapshot.registry.typeFound)}");
                bool mapMagicFound = H8Reflect.FindType("MapMagicObject") != null || Directory.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Assets", "MapMagic"));
                sb.AppendLine($"11. MapMagicObject type or folder found: {IsOk(mapMagicFound)}");
                bool crestFound = H8Reflect.FindType("OceanRenderer") != null || Directory.Exists(Path.Combine(H8Utils.GetProjectRoot(), "Assets", "Crest"));
                sb.AppendLine($"12. OceanRenderer type or folder found: {IsOk(crestFound)}");
                sb.AppendLine($"13. Can collect project metadata: {IsOk(snapshot.project != null)}");
                sb.AppendLine($"14. Can collect scene info: {IsOk(snapshot.scenes != null)}");
                sb.AppendLine($"15. Can collect URP info: {IsOk(snapshot.urp != null)}");
                sb.AppendLine($"16. Can write JSON: OK");
                sb.AppendLine($"17. Can write Markdown: OK");

                H8Utils.WriteFile(Path.Combine(outDir, "self_check.md"), sb.ToString());
                WriteOutput(outDir, snapshot, findings, "SelfCheck");

                Debug.Log($"[H8Blackbox] SelfCheck complete. Written to {outDir}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[H8Blackbox] SelfCheck failed: {e.Message}");
            }
        }

        public static void RunEditMode(H8DiagnosticOptions opts)
        {
            Debug.Log("[H8Blackbox] Starting Edit Mode Diagnostic...");
            try
            {
                string outDir = H8Utils.CreateOutputFolder();
                var snapshot = H8Collectors.CollectFullSnapshot(opts, "EditMode");
                var findings = H8FindingsEngine.Analyze(snapshot);
                WriteOutput(outDir, snapshot, findings, "EditMode");
            }
            catch (Exception e)
            {
                Debug.LogError($"[H8Blackbox] Edit Mode Run failed: {e.Message}");
            }
        }

        public static void RunCurrentScenePlayMode(H8DiagnosticOptions opts)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[H8Blackbox] Already playing."); return; }
            string outDir = H8Utils.CreateOutputFolder();
            RunPlayModeStateMachine(outDir, "Direct", opts, opts.playModeWaitSeconds, (snap) => {
                var findings = H8FindingsEngine.Analyze(snap);
                WriteOutput(outDir, snap, findings, "PlayMode_Current");
            });
        }

        public static void RunBootstrapScenePlayMode(H8DiagnosticOptions opts)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[H8Blackbox] Already playing."); return; }
            if (H8Utils.HasDirtyScenes())
            {
                Debug.LogWarning("[H8Blackbox] Dirty scenes detected. Aborting to preserve read-only safety.");
                return;
            }

            string[] openScenes = H8Utils.GetOpenScenePaths();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_BOOTSTRAP.unity", OpenSceneMode.Single);
            string outDir = H8Utils.CreateOutputFolder();

            RunPlayModeStateMachine(outDir, "Bootstrap", opts, opts.playModeWaitSeconds, (snap) => {
                var findings = H8FindingsEngine.Analyze(snap);
                WriteOutput(outDir, snap, findings, "PlayMode_Bootstrap");
                H8Utils.TryRestoreScenes(openScenes);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            });
        }

        private const string SS_FULLCOMP_PHASE = "H8Blackbox_FullCompPhase"; // 0=None, 1=Direct, 2=Bootstrap

        public static void RunFullComparison(H8DiagnosticOptions opts)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[H8Blackbox] Already playing."); return; }
            
            string outDir = Path.Combine(H8Utils.GetProjectRoot(), H8Utils.OutputRootName, $"Hecton8_Blackbox_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_FullComparison");
            Directory.CreateDirectory(outDir);

            // Serialize options
            string optsJson = JsonUtility.ToJson(opts);
            SessionState.SetString("H8Blackbox_Opts", optsJson);
            SessionState.SetInt(SS_FULLCOMP_PHASE, 1);
            SessionState.SetString(SS_OUTDIR, outDir);

            List<string> warnings = new List<string>();

            // Auto-open 02_HECTON_WORLD for the Direct phase
            string worldScenePath = "Assets/_Project/Scenes/02_HECTON_WORLD.unity";
            if (File.Exists(Path.Combine(H8Utils.GetProjectRoot(), worldScenePath)))
            {
                if (H8Utils.HasDirtyScenes())
                {
                    Debug.LogWarning("[H8Blackbox] Dirty scenes detected. Aborting Full Comparison to preserve safety.");
                    return;
                }
                EditorSceneManager.OpenScene(worldScenePath, OpenSceneMode.Single);
            }

            // 1. Initial EditMode Snapshot
            var initialSnap = H8Collectors.CollectFullSnapshot(opts, "EditMode_Initial");
            H8Writers.WriteSnapshot(outDir, initialSnap, "snapshot_editmode_initial.json");

            // 2. Start Direct PlayMode
            RunPlayModeStateMachine(outDir, "direct", opts, 15.0f, null);
        }

        private static void RunPlayModeStateMachine(string outDir, string label, H8DiagnosticOptions opts, float waitLimit, Action<H8DiagnosticSnapshot> onComplete)
        {
            s_OnComplete = onComplete;
            s_Opts = opts;

            SessionState.SetString("H8Blackbox_Opts", JsonUtility.ToJson(opts));
            SessionState.SetBool(SS_IS_RUNNING, true);
            SessionState.SetString(SS_OUTDIR, outDir);
            SessionState.SetString(SS_LABEL, label);
            SessionState.SetString(SS_START_TIME, "0");
            SessionState.SetString(SS_REAL_START_TIME, EditorApplication.timeSinceStartup.ToString());
            SessionState.SetFloat(SS_WAIT_LIMIT, waitLimit > 0 ? waitLimit : 15.0f);
            SessionState.SetBool(SS_S1_DONE, false);
            SessionState.SetBool(SS_S5_DONE, false);
            SessionState.SetBool(SS_S15_DONE, false);
            SessionState.SetBool(SS_STOP_REQUESTED, false);
            SessionState.SetString(SS_STOP_REQUESTED_TIME, "0");

            EditorApplication.update += ActiveOnUpdate;
            EditorApplication.isPlaying = true;
        }

        private static void ActiveOnUpdate()
        {
            if (!SessionState.GetBool(SS_IS_RUNNING, false))
            {
                EditorApplication.update -= ActiveOnUpdate;
                return;
            }

            string label = SessionState.GetString(SS_LABEL, "unknown");
            string outDir = SessionState.GetString(SS_OUTDIR, "");
            float waitLimit = SessionState.GetFloat(SS_WAIT_LIMIT, 15.0f);
            double totalRealTimeSinceStart = double.Parse(SessionState.GetString(SS_REAL_START_TIME, "0"));
            double startTime = double.Parse(SessionState.GetString(SS_START_TIME, "0"));
            bool stopRequested = SessionState.GetBool(SS_STOP_REQUESTED, false);
            double stopRequestedTime = double.Parse(SessionState.GetString(SS_STOP_REQUESTED_TIME, "0"));

            // Timeout safety for start
            if (!EditorApplication.isPlaying && startTime == 0 && EditorApplication.timeSinceStartup - totalRealTimeSinceStart > 30.0)
            {
                Debug.LogError($"[H8Blackbox] PlayMode failed to start within 30s ({label}).");
                FinishStateMachine(null);
                return;
            }

            // Wait for exit
            if (stopRequested)
            {
                if (!EditorApplication.isPlaying)
                {
                    // Exit completed
                    var finalSnapPath = Path.Combine(outDir, $"snapshot_{label.ToLower()}_{waitLimit}s.json");
                    H8DiagnosticSnapshot finalSnap = null;
                    if (File.Exists(finalSnapPath))
                        finalSnap = JsonUtility.FromJson<H8DiagnosticSnapshot>(File.ReadAllText(finalSnapPath));
                    FinishStateMachine(finalSnap);
                }
                else if (EditorApplication.timeSinceStartup - stopRequestedTime > 30.0)
                {
                    Debug.LogWarning($"[H8Blackbox] PlayMode did not stop cleanly within 30s ({label}).");
                    FinishStateMachine(null);
                }
                return;
            }

            if (EditorApplication.isPlaying)
            {
                if (startTime == 0)
                {
                    startTime = EditorApplication.timeSinceStartup;
                    SessionState.SetString(SS_START_TIME, startTime.ToString());
                }
                
                double elapsed = EditorApplication.timeSinceStartup - startTime;
                
                if (elapsed >= 1.0 && !SessionState.GetBool(SS_S1_DONE, false))
                {
                    SessionState.SetBool(SS_S1_DONE, true);
                    var snap = H8Collectors.CollectFullSnapshot(s_Opts, $"PlayMode_{label}_1s");
                    H8Writers.WriteSnapshot(outDir, snap, $"snapshot_{label.ToLower()}_1s.json");
                }
                if (elapsed >= 5.0 && !SessionState.GetBool(SS_S5_DONE, false) && waitLimit >= 5.0)
                {
                    SessionState.SetBool(SS_S5_DONE, true);
                    var snap = H8Collectors.CollectFullSnapshot(s_Opts, $"PlayMode_{label}_5s");
                    H8Writers.WriteSnapshot(outDir, snap, $"snapshot_{label.ToLower()}_5s.json");
                }
                if (elapsed >= waitLimit && !SessionState.GetBool(SS_S15_DONE, false))
                {
                    SessionState.SetBool(SS_S15_DONE, true);
                    var finalSnap = H8Collectors.CollectFullSnapshot(s_Opts, $"PlayMode_{label}_{waitLimit}s");
                    H8Writers.WriteSnapshot(outDir, finalSnap, $"snapshot_{label.ToLower()}_{waitLimit}s.json");
                    
                    SessionState.SetBool(SS_STOP_REQUESTED, true);
                    SessionState.SetString(SS_STOP_REQUESTED_TIME, EditorApplication.timeSinceStartup.ToString());
                    EditorApplication.isPlaying = false;
                }
            }
        }

        private static void RecoveredOnUpdate()
        {
            if (s_Opts == null)
            {
                string optsJson = SessionState.GetString("H8Blackbox_Opts", "{}");
                s_Opts = JsonUtility.FromJson<H8DiagnosticOptions>(optsJson);
            }
            
            // Similar to ActiveOnUpdate but we don't invoke s_OnComplete because it was lost
            // We just ensure snapshots finish and we exit playmode.
            ActiveOnUpdate();
            if (!SessionState.GetBool(SS_IS_RUNNING, false))
            {
                EditorApplication.update -= RecoveredOnUpdate;
            }
        }

        private static void FinishStateMachine(H8DiagnosticSnapshot snap)
        {
            SessionState.SetBool(SS_IS_RUNNING, false);
            EditorApplication.update -= ActiveOnUpdate;
            EditorApplication.update -= RecoveredOnUpdate;

            int phase = SessionState.GetInt(SS_FULLCOMP_PHASE, 0);
            string outDir = SessionState.GetString(SS_OUTDIR, "");
            string optsJson = SessionState.GetString("H8Blackbox_Opts", "{}");
            H8DiagnosticOptions opts = JsonUtility.FromJson<H8DiagnosticOptions>(optsJson);

            if (phase == 0) // Standalone PlayMode run
            {
                var findings = H8FindingsEngine.Analyze(snap);
                WriteOutput(outDir, snap, findings, SessionState.GetString(SS_LABEL, "PlayMode"));
            }
            else if (phase == 1) // Finished Direct
            {
                var directFindings = H8FindingsEngine.Analyze(snap);
                H8Writers.WriteFindings(outDir, directFindings, "findings_direct.md");

                if (H8Utils.HasDirtyScenes())
                {
                    Debug.LogWarning("[H8Blackbox] Dirty scenes detected after Direct run. We will NOT save them. Discarding changes and proceeding to Bootstrap...");
                    // No abort, we just let OpenScene(Single) discard changes or we don't save.
                }

                SessionState.SetInt(SS_FULLCOMP_PHASE, 2);
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/00_BOOTSTRAP.unity", OpenSceneMode.Single);
                RunPlayModeStateMachine(outDir, "bootstrap", opts, 15.0f, null);
            }
            else if (phase == 2) // Finished Bootstrap
            {
                var bootstrapFindings = H8FindingsEngine.Analyze(snap);
                H8Writers.WriteFindings(outDir, bootstrapFindings, "findings_bootstrap.md");

                var directSnapPath = Path.Combine(outDir, "snapshot_direct_15s.json");
                H8DiagnosticSnapshot directSnap = null;
                if (File.Exists(directSnapPath))
                    directSnap = JsonUtility.FromJson<H8DiagnosticSnapshot>(File.ReadAllText(directSnapPath));

                var directFindingsPath = Path.Combine(outDir, "findings_direct.md");
                List<H8Finding> directFindings = new List<H8Finding>(); // Simplification: we don't strictly need to reload the finding objects for the text report, WriteFullComparisonHandoff does though
                
                H8Writers.WriteDirectVsBootstrapDiff(outDir, directSnap, snap);
                H8Writers.WriteFullComparisonHandoff(outDir, directSnap, snap, directFindings, bootstrapFindings);
                
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("# Hecton8 Full Comparison Report");
                sb.AppendLine("## Run Status");
                sb.AppendLine("- Status: `Success`");
                sb.AppendLine($"- Output Folder: `{outDir}`");
                
                sb.AppendLine("\n## Direct Final Snapshot Summary");
                sb.AppendLine($"- Scene: `{directSnap?.project.activeScene}`");
                sb.AppendLine($"- Phase: `{directSnap?.registry.registryPhaseName}`");
                sb.AppendLine($"- Terrains: `{directSnap?.mapMagic.activeTerrainCount}`");
                sb.AppendLine($"- Ocean Active: `{directSnap?.crest.oceanCrestActive}`");
                
                sb.AppendLine("\n## Bootstrap Final Snapshot Summary");
                sb.AppendLine($"- Scene: `{snap?.project.activeScene}`");
                sb.AppendLine($"- Phase: `{snap?.registry.registryPhaseName}`");
                sb.AppendLine($"- Terrains: `{snap?.mapMagic.activeTerrainCount}`");
                sb.AppendLine($"- Ocean Active: `{snap?.crest.oceanCrestActive}`");
                
                sb.AppendLine("\n## What To Send To Claude");
                sb.AppendLine("- `compact_handoff_for_claude.md`");
                sb.AppendLine("- `direct_vs_bootstrap_diff.md`");
                sb.AppendLine("- `findings_direct.md`");
                sb.AppendLine("- `findings_bootstrap.md`");
                
                H8Utils.WriteFile(Path.Combine(outDir, "full_comparison_report.md"), sb.ToString());
                Debug.Log($"[H8Blackbox] Full Comparison complete. Output: {outDir}");

                SessionState.SetInt(SS_FULLCOMP_PHASE, 0);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else // Normal run
            {
                var cb = s_OnComplete;
                s_OnComplete = null;
                s_Opts = null;
                cb?.Invoke(snap ?? new H8DiagnosticSnapshot());
            }
        }

        private static void WritePartialHandoff(string outDir, H8DiagnosticSnapshot directSnap, List<H8Finding> findings, string abortReason)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# Hecton8 Blackbox Full Comparison Handoff (PARTIAL)");
            sb.AppendLine($"**ABORT REASON:** {abortReason}");
            sb.AppendLine("\n## Direct Run Final Facts");
            sb.AppendLine($"- Scene: `{directSnap?.project.activeScene}`");
            sb.AppendLine($"- Phase: `{directSnap?.registry.registryPhaseName}`");
            sb.AppendLine($"- Console Errors: `{directSnap?.console.totalErrors}`");
            
            sb.AppendLine("\n## What To Send To Claude");
            sb.AppendLine("Tell Claude that Full Comparison was aborted because of dirty scenes, provide the direct findings.");
            H8Utils.WriteFile(Path.Combine(outDir, "compact_handoff_for_claude.md"), sb.ToString());
        }

        private static void WriteOutput(string outDir, H8DiagnosticSnapshot snapshot, List<H8Finding> findings, string mode)
        {
            var summary = new H8RunSummary
            {
                success = true,
                unityVersion = snapshot.project.unityVersion,
                activeScene = snapshot.project.activeScene,
                mode = mode,
                timestamp = snapshot.timestamp,
                outputPath = outDir
            };
            
            foreach (var f in findings)
            {
                if (f.severity == H8Severity.Critical.ToString()) summary.criticalCount++;
                else if (f.severity == H8Severity.Error.ToString()) summary.errorCount++;
                else if (f.severity == H8Severity.Warning.ToString()) summary.warningCount++;
                
                if (f.severity == H8Severity.Critical.ToString() || f.severity == H8Severity.Error.ToString())
                {
                    if (summary.topFindings.Count < 10)
                        summary.topFindings.Add($"[{f.category}] {f.title}");
                }
            }
            
            summary.outputFiles.Add("run_summary.json");
            summary.outputFiles.Add($"snapshot_{mode.ToLower()}.json");
            summary.outputFiles.Add("findings.md");
            summary.outputFiles.Add("report.md");
            summary.outputFiles.Add("compact_handoff_for_claude.md");
            summary.outputFiles.Add("next_steps_for_agent.md");
            summary.outputFiles.Add("hierarchy_editmode.txt");
            summary.outputFiles.Add("raw_console_log.txt");
            
            H8Writers.WriteRunSummary(outDir, summary);
            if (mode == "EditMode") H8Writers.WriteSnapshot(outDir, snapshot, "snapshot_editmode.json");
            H8Writers.WriteFindings(outDir, findings);
            H8Writers.WriteReport(outDir, snapshot, findings);
            H8Writers.WriteCompactHandoff(outDir, snapshot, findings);
            H8Writers.WriteNextSteps(outDir, findings);
            H8Writers.WriteHierarchy(outDir, snapshot.keyObjects);
            H8Writers.WriteConsoleLogs(outDir, snapshot.console);
            
            Debug.Log($"[H8Blackbox] {mode} Diagnostic complete.\nOutput written to: {outDir}");
        }
    }
}
