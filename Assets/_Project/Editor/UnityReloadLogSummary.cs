using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class UnityReloadLogSummary
    {
        private static readonly Regex DomainReloadRegex = new Regex(
            @"Domain Reload Profiling:\s+(?<ms>\d+)ms",
            RegexOptions.Compiled);

        private static readonly Regex AssetRefreshRegex = new Regex(
            @"Asset Pipeline Refresh .* Total:\s+(?<seconds>[\d\.]+)\s+seconds",
            RegexOptions.Compiled);

        private static readonly Regex StepRegex = new Regex(
            @"^\s+(?<name>[A-Za-z][A-Za-z0-9]+(?:[A-Za-z0-9 ]*[A-Za-z0-9])?)\s+\((?<ms>\d+)ms\)$",
            RegexOptions.Compiled);

        private struct StepSummary
        {
            public string Name;
            public int MaxMs;
            public int Count;

            public StepSummary(string name, int ms)
            {
                Name = name;
                MaxMs = ms;
                Count = 1;
            }

            public void Record(int ms)
            {
                if (ms > MaxMs)
                    MaxMs = ms;

                Count++;
            }
        }

        [MenuItem("Hecton8/Validation/Log Unity Reload Summary")]
        public static void LogSummary()
        {
            string logPath = Application.consoleLogPath;

            if (!File.Exists(logPath))
            {
                Debug.LogWarning($"[UnityReloadLogSummary] Editor.log not found: {logPath}");
                return;
            }

            string[] lines = File.ReadAllLines(logPath);
            int tailStart = Math.Max(0, lines.Length - 4000);
            List<int> reloadMs = new List<int>(16);
            List<float> refreshSeconds = new List<float>(16);
            Dictionary<string, StepSummary> stepSummaries = new Dictionary<string, StepSummary>(32, StringComparer.Ordinal);

            for (int lineIndex = tailStart; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];

                Match reloadMatch = DomainReloadRegex.Match(line);
                if (reloadMatch.Success)
                    reloadMs.Add(int.Parse(reloadMatch.Groups["ms"].Value));

                Match refreshMatch = AssetRefreshRegex.Match(line);
                if (refreshMatch.Success)
                    refreshSeconds.Add(float.Parse(refreshMatch.Groups["seconds"].Value, System.Globalization.CultureInfo.InvariantCulture));

                Match stepMatch = StepRegex.Match(line);
                if (!stepMatch.Success)
                    continue;

                int stepMs = int.Parse(stepMatch.Groups["ms"].Value);
                if (stepMs < 1000)
                    continue;

                string stepName = stepMatch.Groups["name"].Value.Trim();
                if (stepSummaries.TryGetValue(stepName, out StepSummary summary))
                {
                    summary.Record(stepMs);
                    stepSummaries[stepName] = summary;
                }
                else
                {
                    stepSummaries.Add(stepName, new StepSummary(stepName, stepMs));
                }
            }

            List<StepSummary> stepMatches = new List<StepSummary>(stepSummaries.Count);
            Dictionary<string, StepSummary>.Enumerator stepEnumerator = stepSummaries.GetEnumerator();
            while (stepEnumerator.MoveNext())
                stepMatches.Add(stepEnumerator.Current.Value);

            stepMatches.Sort((left, right) => right.MaxMs.CompareTo(left.MaxMs));

            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("=== UNITY RELOAD SUMMARY ===");
            sb.AppendLine($"Log: {logPath}");

            if (reloadMs.Count > 0)
            {
                int maxReloadMs = 0;
                long totalReloadMs = 0L;
                for (int i = 0; i < reloadMs.Count; i++)
                {
                    int sample = reloadMs[i];
                    if (sample > maxReloadMs)
                        maxReloadMs = sample;

                    totalReloadMs += sample;
                }

                sb.AppendLine($"Domain reload samples: {reloadMs.Count}");
                sb.AppendLine($"Domain reload max: {maxReloadMs} ms");
                sb.AppendLine($"Domain reload avg: {(int)(totalReloadMs / reloadMs.Count)} ms");
            }
            else
            {
                sb.AppendLine("Domain reload samples: none found");
            }

            if (refreshSeconds.Count > 0)
            {
                float maxRefreshSeconds = 0f;
                double totalRefreshSeconds = 0d;
                for (int i = 0; i < refreshSeconds.Count; i++)
                {
                    float sample = refreshSeconds[i];
                    if (sample > maxRefreshSeconds)
                        maxRefreshSeconds = sample;

                    totalRefreshSeconds += sample;
                }

                sb.AppendLine($"Asset refresh samples: {refreshSeconds.Count}");
                sb.AppendLine($"Asset refresh max: {maxRefreshSeconds:F3} s");
                sb.AppendLine($"Asset refresh avg: {totalRefreshSeconds / refreshSeconds.Count:F3} s");
            }
            else
            {
                sb.AppendLine("Asset refresh samples: none found");
            }

            sb.AppendLine("Top expensive reload steps (>= 1000 ms):");
            int stepCount = Math.Min(12, stepMatches.Count);
            if (stepCount == 0)
            {
                sb.AppendLine("  none found");
            }
            else
            {
                for (int i = 0; i < stepCount; i++)
                {
                    StepSummary step = stepMatches[i];
                    sb.AppendLine($"  - {step.Name}: max {step.MaxMs} ms, seen {step.Count}x");
                }
            }

            Debug.Log(sb.ToString());
        }
    }
}
