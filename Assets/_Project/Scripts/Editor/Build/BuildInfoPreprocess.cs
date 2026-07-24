using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Hecton8.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Hecton8.Editor.Build
{
    public sealed class BuildInfoPreprocess : IPreprocessBuildWithReport
    {
        private const string AssetPath = "Assets/_Project/Data/BuildInfo.asset";
        private const int GitMetadataTimeoutMilliseconds = 2000;
        private const int GitMetadataOutputDrainMilliseconds = 500;

        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildInfo buildInfo = LoadOrCreateAsset();
            string branch = RunGit("rev-parse --abbrev-ref HEAD");
            string fullCommit = RunGit("rev-parse HEAD");
            string commit = RunGit("rev-parse --short=12 HEAD");
            string dirty = RunGit("status --porcelain");
            bool isDirty = !string.IsNullOrWhiteSpace(dirty) && dirty != "unknown";
            if (isDirty)
                commit += "-dirty";

            buildInfo.Apply(
                branch,
                commit,
                BuildInfo.ParseCommitHash32(fullCommit),
                isDirty,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                Application.unityVersion,
                report.summary.platform.ToString());

            EditorUtility.SetDirty(buildInfo);
            AssetDatabase.SaveAssets();
        }

        private static BuildInfo LoadOrCreateAsset()
        {
            BuildInfo buildInfo = AssetDatabase.LoadAssetAtPath<BuildInfo>(AssetPath);
            if (buildInfo != null)
                return buildInfo;

            string directory = Path.GetDirectoryName(AssetPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            buildInfo = ScriptableObject.CreateInstance<BuildInfo>();
            AssetDatabase.CreateAsset(buildInfo, AssetPath);
            return buildInfo;
        }

        private static string RunGit(string arguments)
        {
            try
            {
                ProcessStartInfo info = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = Directory.GetCurrentDirectory(),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = TryStartGitMetadataProcessNoThrow(info))
                {
                    if (process == null)
                        return "unknown";

                    Task<string> outputTask;
                    Task<string> errorTask;
                    try
                    {
                        outputTask = process.StandardOutput.ReadToEndAsync();
                        errorTask = process.StandardError.ReadToEndAsync();
                    }
                    catch (Exception)
                    {
                        KillGitMetadataProcessNoThrow(process);
                        return "unknown";
                    }

                    if (!TryWaitForGitMetadataProcess(process))
                    {
                        KillGitMetadataProcessNoThrow(process);
                        return "unknown";
                    }

                    WaitForGitMetadataOutputDrain(outputTask, errorTask);
                    string output = ReadProcessOutputTaskNoThrow(outputTask);
                    return ReadProcessExitCodeNoThrow(process) == 0 ? output.Trim() : "unknown";
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[BuildInfoPreprocess] Git metadata unavailable: " + exception.Message);
                return "unknown";
            }
        }

        private static Process TryStartGitMetadataProcessNoThrow(ProcessStartInfo info)
        {
            try
            {
                return Process.Start(info);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool TryWaitForGitMetadataProcess(Process process)
        {
            try
            {
                return process.WaitForExit(GitMetadataTimeoutMilliseconds);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void WaitForGitMetadataOutputDrain(Task<string> outputTask, Task<string> errorTask)
        {
            try
            {
                Task.WaitAll(new Task[] { outputTask, errorTask }, GitMetadataOutputDrainMilliseconds);
            }
            catch (Exception)
            {
            }
        }

        private static string ReadProcessOutputTaskNoThrow(Task<string> task)
        {
            if (task == null || !task.IsCompleted || task.IsCanceled || task.IsFaulted)
                return string.Empty;

            try
            {
                return task.Result ?? string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static int ReadProcessExitCodeNoThrow(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        private static void KillGitMetadataProcessNoThrow(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill();
            }
            catch (Exception)
            {
            }
        }
    }
}
