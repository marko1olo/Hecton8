// HECTON-8 — Agent Work Cementer (Editor-only safety net).
//
// WHY THIS EXISTS
// Multiple agents edit this working tree concurrently (a local Unity-attached agent and a
// remote agent that writes files over a device bridge). Remote edits land as UNCOMMITTED
// working-tree changes. A single `git reset --hard`, `git checkout .`, or `git stash` by any
// participant silently destroys them, and there is no recovery path for uncommitted work.
//
// This component removes the human step: whenever Unity imports changed assets, it commits the
// working tree after a quiet period. Every edit therefore has a git recovery point within
// seconds of landing, so a later reset can only ever cost work that was never imported.
//
// SAFETY CONTRACT (deliberately narrow)
//   - Runs `git add -A` and `git commit` ONLY.
//   - NEVER runs reset, checkout, clean, stash, rebase, merge, branch, push, or pull.
//   - Editor-only assembly; contributes nothing to a player build and no runtime cost.
//   - Never touches assets: no SetDirty, no SaveScene, no PrefabUtility (Sandbox Firewall Rule).
//   - Skips while compiling, importing, or updating so it cannot fight the asset pipeline.
//   - Runs git asynchronously with a hard timeout; the Editor main thread never blocks on it.
//   - Self-disables after repeated failures instead of spamming the console.
//
// CONTROL
//   Tools > HECTON-8 > Agent Work Cementer  (toggle, commit now, status)
//   Preference key: Hecton8.AgentWorkCementer.Enabled (per-machine, default ON).

#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.EditorTools
{
    [InitializeOnLoad]
    internal static class HectonAgentWorkCementer
    {
        private const string EnabledPrefKey = "Hecton8.AgentWorkCementer.Enabled";
        private const string MenuRoot = "Tools/HECTON-8/Agent Work Cementer/";
        private const string EnabledMenuPath = MenuRoot + "Enabled";
        private const string CommitNowMenuPath = MenuRoot + "Cement Now";
        private const string StatusMenuPath = MenuRoot + "Log Status";

        /// <summary>Seconds of import silence before a commit is attempted. Avoids committing mid-batch.</summary>
        private const double QuietPeriodSeconds = 20.0;

        /// <summary>Hard ceiling for a git invocation. A hung git must never wedge the Editor loop.</summary>
        private const int GitTimeoutMilliseconds = 45000;

        /// <summary>Consecutive failures tolerated before the cementer parks itself for the session.</summary>
        private const int MaxConsecutiveFailures = 3;

        private static double s_pendingSinceEditorTime;
        private static bool s_hasPendingChanges;
        private static bool s_gitRunning;
        private static int s_consecutiveFailures;
        private static bool s_disabledForSession;
        private static string s_repositoryRoot;

        static HectonAgentWorkCementer()
        {
            EditorApplication.update += OnEditorUpdate;
            // A domain reload is itself evidence that files changed on disk; arm the timer once.
            RequestCement("domain reload");
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledPrefKey, true);
            set => EditorPrefs.SetBool(EnabledPrefKey, value);
        }

        [MenuItem(EnabledMenuPath)]
        private static void ToggleEnabled()
        {
            bool next = !Enabled;
            Enabled = next;
            s_disabledForSession = false;
            s_consecutiveFailures = 0;
            Debug.Log($"[AgentWorkCementer] {(next ? "ENABLED" : "DISABLED")}. Automatic `git add -A` + `git commit` after {QuietPeriodSeconds:0}s of import silence.");
        }

        [MenuItem(EnabledMenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            Menu.SetChecked(EnabledMenuPath, Enabled);
            return true;
        }

        [MenuItem(CommitNowMenuPath)]
        private static void CommitNow()
        {
            s_disabledForSession = false;
            s_consecutiveFailures = 0;
            RequestCement("manual");
            s_pendingSinceEditorTime = 0.0; // fire on the next update tick
        }

        [MenuItem(StatusMenuPath)]
        private static void LogStatus()
        {
            string root = ResolveRepositoryRoot();
            Debug.Log(
                "[AgentWorkCementer] status\n" +
                $"  enabled            : {Enabled}\n" +
                $"  disabled (session) : {s_disabledForSession}\n" +
                $"  pending changes    : {s_hasPendingChanges}\n" +
                $"  git running        : {s_gitRunning}\n" +
                $"  consecutive fails  : {s_consecutiveFailures}\n" +
                $"  repository root    : {(string.IsNullOrEmpty(root) ? "<not found>" : root)}");
        }

        internal static void RequestCement(string reason)
        {
            if (!Enabled || s_disabledForSession)
                return;

            s_hasPendingChanges = true;
            s_pendingSinceEditorTime = EditorApplication.timeSinceStartup;
            _ = reason; // kept for future telemetry; intentionally unused to avoid console noise
        }

        private static void OnEditorUpdate()
        {
            if (!s_hasPendingChanges || s_gitRunning || s_disabledForSession || !Enabled)
                return;

            // Never fight the asset pipeline: a commit mid-import can capture a half-written tree.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                s_pendingSinceEditorTime = EditorApplication.timeSinceStartup;
                return;
            }

            if (EditorApplication.timeSinceStartup - s_pendingSinceEditorTime < QuietPeriodSeconds)
                return;

            s_hasPendingChanges = false;
            TryCement();
        }

        private static void TryCement()
        {
            string root = ResolveRepositoryRoot();
            if (string.IsNullOrEmpty(root))
            {
                s_disabledForSession = true;
                Debug.LogWarning("[AgentWorkCementer] No git repository found above the project folder. Cementer parked for this session.");
                return;
            }

            s_gitRunning = true;
            RunGitAsync(root, new[] { "add", "-A" }, addExit =>
            {
                if (addExit.ExitCode != 0)
                {
                    FinishFailure("git add", addExit);
                    return;
                }

                // `git commit` exits 1 when there is nothing staged. That is the common, healthy
                // case (Unity reimported without any real change) and must not count as a failure.
                string message = BuildCommitMessage();
                RunGitAsync(root, new[] { "commit", "-m", message }, commitExit =>
                {
                    s_gitRunning = false;

                    if (commitExit.ExitCode == 0)
                    {
                        s_consecutiveFailures = 0;
                        string firstLine = FirstLine(commitExit.StandardOutput);
                        Debug.Log($"[AgentWorkCementer] Cemented working tree. {firstLine}");
                        return;
                    }

                    string combined = commitExit.StandardOutput + commitExit.StandardError;
                    if (combined.IndexOf("nothing to commit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        combined.IndexOf("nothing added to commit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        combined.IndexOf("no changes added", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        s_consecutiveFailures = 0;
                        return; // clean tree — silent, this happens constantly
                    }

                    FinishFailure("git commit", commitExit);
                });
            });
        }

        private static void FinishFailure(string stage, GitResult result)
        {
            s_gitRunning = false;
            s_consecutiveFailures++;
            string detail = FirstLine(string.IsNullOrEmpty(result.StandardError) ? result.StandardOutput : result.StandardError);
            Debug.LogWarning($"[AgentWorkCementer] {stage} failed (exit {result.ExitCode}): {detail}");

            if (s_consecutiveFailures >= MaxConsecutiveFailures)
            {
                s_disabledForSession = true;
                Debug.LogWarning($"[AgentWorkCementer] Parked for this session after {MaxConsecutiveFailures} consecutive failures. Re-arm via {StatusMenuPath.Replace("Log Status", "Cement Now")}.");
            }
        }

        private static string BuildCommitMessage()
        {
            // Quotes/newlines would break the -m argument; the message is fully agent-controlled
            // and kept to a single safe ASCII line.
            return $"chore(auto): cement working tree {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        private static string ResolveRepositoryRoot()
        {
            if (!string.IsNullOrEmpty(s_repositoryRoot))
                return s_repositoryRoot;

            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(Application.dataPath) ?? string.Empty);
            while (directory != null)
            {
                string gitPath = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                {
                    s_repositoryRoot = directory.FullName;
                    return s_repositoryRoot;
                }

                directory = directory.Parent;
            }

            return null;
        }

        private readonly struct GitResult
        {
            public GitResult(int exitCode, string standardOutput, string standardError)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
        }

        /// <summary>
        /// Runs git off the main thread and marshals the completion callback back onto the Editor
        /// update loop. Nothing here touches the Unity API from the worker thread (COMMON_SENSE #10).
        /// </summary>
        private static void RunGitAsync(string workingDirectory, string[] arguments, Action<GitResult> onCompleted)
        {
            GitResult captured = default;
            bool completed = false;

            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                GitResult local;
                try
                {
                    local = RunGitBlocking(workingDirectory, arguments);
                }
                catch (Exception exception)
                {
                    local = new GitResult(-1, string.Empty, exception.Message);
                }

                captured = local;
                System.Threading.Volatile.Write(ref completed, true);
            });

            void Pump()
            {
                if (!System.Threading.Volatile.Read(ref completed))
                    return;

                EditorApplication.update -= Pump;
                onCompleted(captured);
            }

            EditorApplication.update += Pump;
        }

        private static GitResult RunGitBlocking(string workingDirectory, string[] arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            foreach (var arg in arguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using (Process process = Process.Start(startInfo))
            {
                if (process == null)
                    return new GitResult(-1, string.Empty, "git process could not be started (is git on PATH?)");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    try { process.Kill(); } catch { /* already gone */ }
                    return new GitResult(-1, output, $"git timed out after {GitTimeoutMilliseconds} ms");
                }

                return new GitResult(process.ExitCode, output, error);
            }
        }

        private static string FirstLine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            int index = value.IndexOfAny(new[] { '\r', '\n' });
            return index < 0 ? value.Trim() : value.Substring(0, index).Trim();
        }
    }

    /// <summary>
    /// Arms the cementer whenever Unity finishes importing changed assets — this is the signal that
    /// files (from any agent, editor action, or external write) actually landed on disk.
    /// </summary>
    internal sealed class HectonAgentWorkCementerPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (importedAssets.Length == 0 && deletedAssets.Length == 0 && movedAssets.Length == 0)
                return;

            HectonAgentWorkCementer.RequestCement("asset import");
        }
    }
}
#endif
