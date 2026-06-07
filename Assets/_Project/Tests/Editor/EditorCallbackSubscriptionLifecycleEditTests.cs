using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EditorCallbackSubscriptionLifecycleEditTests
    {
        private static readonly string[] PairedEditorEvents =
        {
            "EditorApplication.update",
            "EditorApplication.playModeStateChanged",
            "SceneView.duringSceneGui"
        };

        private static readonly Regex OnEnableSignatureRegex = new Regex(
            @"void\s+OnEnable\s*\([^)]*\)\s*\{",
            RegexOptions.Compiled);

        [Test]
        public void OnEnableEditorCallbacks_DefensivelyDeduplicateBeforeSubscribe()
        {
            string scriptsRoot = Path.Combine(ProjectRoot, "Assets", "_Project", "Scripts");
            List<string> failures = new List<string>();

            foreach (string path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string source = File.ReadAllText(path);
                foreach (Match match in OnEnableSignatureRegex.Matches(source))
                {
                    string body = ExtractMethodBody(source, match.Index);
                    int line = CountLinesBefore(source, match.Index);
                    foreach (string eventName in PairedEditorEvents)
                        AuditEvent(path, line, body, eventName, failures);
                }
            }

            Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void PlayModeStateChangedSubscriptions_DefensivelyDeduplicateNearSubscribe()
        {
            string[] sourceFiles = EnumerateScriptFiles();
            List<string> failures = new List<string>();

            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEditor\.)?EditorApplication\.playModeStateChanged",
                "playModeStateChanged",
                failures);

            Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void ReloadAndCompilationSubscriptions_DefensivelyDeduplicateNearSubscribe()
        {
            string[] sourceFiles = EnumerateScriptFiles();
            List<string> failures = new List<string>();

            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEditor\.)?AssemblyReloadEvents\.beforeAssemblyReload",
                "beforeAssemblyReload",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEditor\.)?AssemblyReloadEvents\.afterAssemblyReload",
                "afterAssemblyReload",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"CompilationPipeline\.compilationStarted",
                "compilationStarted",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"CompilationPipeline\.compilationFinished",
                "compilationFinished",
                failures);

            Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
        }

        [Test]
        public void GlobalRuntimeSubscriptions_DefensivelyDeduplicateNearSubscribe()
        {
            string[] sourceFiles = EnumerateScriptFiles();
            List<string> failures = new List<string>();

            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEngine\.SceneManagement\.)?SceneManager\.sceneLoaded",
                "sceneLoaded",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEngine\.SceneManagement\.)?SceneManager\.sceneUnloaded",
                "sceneUnloaded",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEngine\.SceneManagement\.)?SceneManager\.activeSceneChanged",
                "activeSceneChanged",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"(?<!Editor)(?:UnityEngine\.)?Application\.quitting",
                "Application.quitting",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"(?:UnityEditor\.)?EditorApplication\.quitting",
                "EditorApplication.quitting",
                failures);
            AuditNearbySubscriptions(
                sourceFiles,
                @"AudioSettings\.OnAudioConfigurationChanged",
                "OnAudioConfigurationChanged",
                failures);

            Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures));
        }

        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ExtractMethodBody(string source, int signatureIndex)
        {
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing OnEnable open brace.");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing OnEnable close brace.");
            return string.Empty;
        }

        private static void AuditEvent(
            string path,
            int line,
            string body,
            string eventName,
            List<string> failures)
        {
            Regex addRegex = new Regex(Regex.Escape(eventName) + @"\s*\+=\s*(?<handler>[^;]+);");
            foreach (Match match in addRegex.Matches(body))
            {
                string handler = match.Groups["handler"].Value.Trim();
                Regex removeRegex = new Regex(Regex.Escape(eventName) + @"\s*-=\s*" + Regex.Escape(handler) + @"\s*;");
                if (!removeRegex.IsMatch(body))
                    failures.Add(ToProjectRelative(path) + ":" + line + " missing " + eventName + " -= " + handler + "; before subscribe");
            }
        }

        private static int CountLinesBefore(string source, int index)
        {
            int lines = 1;
            for (int i = 0; i < index; i++)
            {
                if (source[i] == '\n')
                    lines++;
            }

            return lines;
        }

        private static string[] EnumerateScriptFiles()
        {
            string scriptsRoot = Path.Combine(ProjectRoot, "Assets", "_Project", "Scripts");
            List<string> paths = new List<string>(Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories));
            return paths.ToArray();
        }

        private static void AuditNearbySubscriptions(
            string[] sourceFiles,
            string eventPattern,
            string eventName,
            List<string> failures)
        {
            Regex addRegex = new Regex(eventPattern + @"\s*\+=\s*(?<handler>[^;]+);");

            foreach (string path in sourceFiles)
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    Match match = addRegex.Match(lines[i]);
                    if (!match.Success)
                        continue;

                    string handler = match.Groups["handler"].Value.Trim();
                    if (!HasNearbyRemove(lines, i, eventPattern, handler))
                        failures.Add(ToProjectRelative(path) + ":" + (i + 1) + " missing " + eventName + " -= " + handler + "; before subscribe");
                }
            }
        }

        private static bool HasNearbyRemove(string[] lines, int addLineIndex, string eventPattern, string handler)
        {
            Regex removeRegex = new Regex(eventPattern + @"\s*-=\s*" + Regex.Escape(handler) + @"\s*;");
            int firstLine = Math.Max(0, addLineIndex - 5);
            for (int i = firstLine; i <= addLineIndex; i++)
            {
                if (removeRegex.IsMatch(lines[i]))
                    return true;
            }

            return false;
        }

        private static string ToProjectRelative(string path)
        {
            string root = ProjectRoot;
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }
    }
}
