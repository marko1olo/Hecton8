using System;
using System.IO;
using System.Linq;
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

        [MenuItem("Hecton/Validation/Log Unity Reload Summary")]
        public static void LogSummary()
        {
            string logPath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "Unity",
                "Editor",
                "Editor.log");

            if (!File.Exists(logPath))
            {
                Debug.LogWarning($"[UnityReloadLogSummary] Editor.log not found: {logPath}");
                return;
            }

            string[] lines = File.ReadAllLines(logPath);
            string[] tail = lines.Skip(Math.Max(0, lines.Length - 4000)).ToArray();

            Match[] reloadMatches = tail
                .Select(line => DomainReloadRegex.Match(line))
                .Where(match => match.Success)
                .ToArray();

            Match[] refreshMatches = tail
                .Select(line => AssetRefreshRegex.Match(line))
                .Where(match => match.Success)
                .ToArray();

            var stepMatches = tail
                .Select(line => StepRegex.Match(line))
                .Where(match => match.Success)
                .Select(match => new
                {
                    Name = match.Groups["name"].Value.Trim(),
                    Ms = int.Parse(match.Groups["ms"].Value)
                })
                .Where(entry => entry.Ms >= 1000)
                .GroupBy(entry => entry.Name)
                .Select(group => new
                {
                    Name = group.Key,
                    MaxMs = group.Max(entry => entry.Ms),
                    Count = group.Count()
                })
                .OrderByDescending(entry => entry.MaxMs)
                .Take(12)
                .ToArray();

            StringBuilder sb = new StringBuilder(1024);
            sb.AppendLine("=== UNITY RELOAD SUMMARY ===");
            sb.AppendLine($"Log: {logPath}");

            if (reloadMatches.Length > 0)
            {
                int[] reloadMs = reloadMatches
                    .Select(match => int.Parse(match.Groups["ms"].Value))
                    .ToArray();
                sb.AppendLine($"Domain reload samples: {reloadMs.Length}");
                sb.AppendLine($"Domain reload max: {reloadMs.Max()} ms");
                sb.AppendLine($"Domain reload avg: {(int)reloadMs.Average()} ms");
            }
            else
            {
                sb.AppendLine("Domain reload samples: none found");
            }

            if (refreshMatches.Length > 0)
            {
                float[] refreshSeconds = refreshMatches
                    .Select(match => float.Parse(match.Groups["seconds"].Value, System.Globalization.CultureInfo.InvariantCulture))
                    .ToArray();
                sb.AppendLine($"Asset refresh samples: {refreshSeconds.Length}");
                sb.AppendLine($"Asset refresh max: {refreshSeconds.Max():F3} s");
                sb.AppendLine($"Asset refresh avg: {refreshSeconds.Average():F3} s");
            }
            else
            {
                sb.AppendLine("Asset refresh samples: none found");
            }

            sb.AppendLine("Top expensive reload steps (>= 1000 ms):");
            if (stepMatches.Length == 0)
            {
                sb.AppendLine("  none found");
            }
            else
            {
                foreach (var step in stepMatches)
                {
                    sb.AppendLine($"  - {step.Name}: max {step.MaxMs} ms, seen {step.Count}x");
                }
            }

            Debug.Log(sb.ToString());
        }
    }
}
