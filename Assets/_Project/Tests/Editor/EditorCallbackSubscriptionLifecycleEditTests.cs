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
                    AuditEvent(path, line, body, "EditorApplication.update", failures);
                    AuditEvent(path, line, body, "SceneView.duringSceneGui", failures);
                }
            }

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

        private static string ToProjectRelative(string path)
        {
            string root = ProjectRoot;
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }
    }
}
