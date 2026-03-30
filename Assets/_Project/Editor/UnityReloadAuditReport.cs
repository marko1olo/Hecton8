using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class UnityReloadAuditReport
    {
        private const string OutputFileName = "UNITY_RELOAD_AUDIT_REPORT.md";

        private static readonly string[] ProtectedClassNames =
        {
            "HectonCelestialEngine",
            "HectonAtmosphereManager",
            "HectonUnderwaterVisuals",
            "SkySystemFollowCamera",
            "VisorHUDController",
            "SuitHUDScreenCompositor",
            "SuitHUDPresentationController",
            "SuitHUDV4CanvasOverlay",
            "HectonSuitHUD",
            "HectonSuitHUD_v4",
            "HectonSuitHUDExtensions",
        };

        private static readonly HookPattern[] Patterns =
        {
            new HookPattern("ExecuteAlways", "[ExecuteAlways]"),
            new HookPattern("ExecuteInEditMode", "[ExecuteInEditMode]"),
            new HookPattern("InitializeOnLoad", "[InitializeOnLoad]"),
            new HookPattern("InitializeOnLoadMethod", "[InitializeOnLoadMethod]"),
            new HookPattern("DidReloadScripts", "[DidReloadScripts]"),
            new HookPattern("RuntimeInitializeOnLoadMethod", "[RuntimeInitializeOnLoadMethod"),
            new HookPattern("AssemblyReloadEvents", "AssemblyReloadEvents."),
            new HookPattern("EditorApplication.delayCall", "EditorApplication.delayCall"),
            new HookPattern("EditorApplication.update", "EditorApplication.update"),
            new HookPattern("EditorApplication.playModeStateChanged", "EditorApplication.playModeStateChanged"),
            new HookPattern("AssetPostprocessor", "AssetPostprocessor"),
            new HookPattern("SceneView.duringSceneGui", "SceneView.duringSceneGui"),
        };

        [MenuItem("Hecton/Validation/Generate Unity Reload Audit Report")]
        public static void GenerateReport()
        {
            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/_Project" });
            List<HookEntry> entries = new List<HookEntry>();

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                    continue;

                if (assetPath.EndsWith("/UnityReloadAuditReport.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string source = File.ReadAllText(assetPath);
                List<string> hooks = Patterns
                    .Where(pattern => source.Contains(pattern.Token, StringComparison.Ordinal))
                    .Select(pattern => pattern.Name)
                    .ToList();

                if (hooks.Count == 0)
                    continue;

                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
                string className = script != null && script.GetClass() != null
                    ? script.GetClass().Name
                    : Path.GetFileNameWithoutExtension(assetPath);

                entries.Add(new HookEntry(
                    className,
                    assetPath,
                    hooks,
                    Classify(className, assetPath, hooks)));
            }

            entries = entries
                .OrderBy(entry => entry.Classification)
                .ThenBy(entry => entry.ClassName)
                .ToList();

            string outputPath = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, OutputFileName);
            File.WriteAllText(outputPath, BuildMarkdown(entries), Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"[UnityReloadAudit] Report generated: {outputPath}");
        }

        private static string Classify(string className, string assetPath, IReadOnlyCollection<string> hooks)
        {
            if (IsProtected(className, assetPath))
                return "Protected";

            if (hooks.Contains("ExecuteAlways") || hooks.Contains("ExecuteInEditMode"))
                return "Safe To Disable In Editor";

            if (hooks.Contains("InitializeOnLoad") ||
                hooks.Contains("InitializeOnLoadMethod") ||
                hooks.Contains("AssemblyReloadEvents") ||
                hooks.Contains("EditorApplication.update") ||
                hooks.Contains("EditorApplication.playModeStateChanged"))
            {
                if (assetPath.Contains("/Scripts/Editor/", StringComparison.OrdinalIgnoreCase) ||
                    assetPath.Contains("/Editor/", StringComparison.OrdinalIgnoreCase))
                {
                    return "Safe To Defer";
                }

                return "Risky";
            }

            if (hooks.Contains("RuntimeInitializeOnLoadMethod"))
                return "Risky";

            return "Safe To Defer";
        }

        private static bool IsProtected(string className, string assetPath)
        {
            if (ProtectedClassNames.Contains(className, StringComparer.Ordinal))
                return true;

            return assetPath.Contains("/Scripts/Visor/", StringComparison.OrdinalIgnoreCase) ||
                   assetPath.Contains("/Scripts/UI/SuitHUD", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildMarkdown(IReadOnlyList<HookEntry> entries)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Unity Reload Audit Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();
            builder.AppendLine("## Summary");
            builder.AppendLine();

            foreach (IGrouping<string, HookEntry> group in entries.GroupBy(entry => entry.Classification))
            {
                builder.AppendLine($"- `{group.Key}`: {group.Count()}");
            }

            builder.AppendLine();

            foreach (IGrouping<string, HookEntry> group in entries.GroupBy(entry => entry.Classification))
            {
                builder.AppendLine($"## {group.Key}");
                builder.AppendLine();

                foreach (HookEntry entry in group)
                {
                    builder.AppendLine($"- `{entry.ClassName}`");
                    builder.AppendLine($"  path: `{entry.AssetPath}`");
                    builder.AppendLine($"  hooks: {string.Join(", ", entry.Hooks)}");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private readonly struct HookPattern
        {
            public HookPattern(string name, string token)
            {
                Name = name;
                Token = token;
            }

            public string Name { get; }
            public string Token { get; }
        }

        private readonly struct HookEntry
        {
            public HookEntry(string className, string assetPath, IReadOnlyList<string> hooks, string classification)
            {
                ClassName = className;
                AssetPath = assetPath;
                Hooks = hooks;
                Classification = classification;
            }

            public string ClassName { get; }
            public string AssetPath { get; }
            public IReadOnlyList<string> Hooks { get; }
            public string Classification { get; }
        }
    }
}
