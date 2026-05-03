// ============================================================================
// HECTON-8 — ZeroGCComplianceScanner.cs
// Editor-only automated scanner for Zero-GC policy violations in hot paths.
//
// Menu: Hecton8 → Audit → Zero-GC Compliance Scan
//
// Scans all first-party C# scripts under Assets/_Project/Scripts for:
//   - LINQ usage in hot paths (Tick/FixedTick/SlowTick/Update/LateUpdate)
//   - StartCoroutine calls in gameplay code
//   - renderer.material access (leaked copies)
//   - Uncached GetComponent in hot paths
//   - String operations in hot paths
//   - Debug.Log without #if guard
//   - Camera.main in hot paths
//   - Physics.*Cast without NonAlloc
//   - SendMessage/BroadcastMessage usage
//   - new Action/Func/lambda in Tick
//   - foreach on Dictionary
//   - tag == "string" instead of CompareTag
//   - Animator.Set* with string literal
//   - Unauthorized DontDestroyOnLoad calls outside bootstrap/crash telemetry owners
//
// OWNERSHIP: Editor tooling only. No runtime code.
// ============================================================================

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Scans project scripts for Zero-GC policy violations.
    /// Reports findings with file, line number, and violation type.
    /// </summary>
    public static class ZeroGCComplianceScanner
    {
        // ══════════════════════════════════════════════════════════
        //  VIOLATION PATTERNS
        // ══════════════════════════════════════════════════════════

        private struct ViolationPattern
        {
            public string Name;
            public string Regex;
            public string Severity; // "ERROR" or "WARN"
            public string Fix;
        }

        private static readonly ViolationPattern[] _patterns = new ViolationPattern[]
        {
            // ── LINQ in hot paths ────────────────────────────────
            new ViolationPattern
            {
                Name = "LINQ in hot path",
                Regex = @"\.(Where|Select|Any|FirstOrDefault|ToList|ToArray|OrderBy|GroupBy|Distinct|Aggregate|Count\(|Sum\(|Min\(|Max\()\s*\(",
                Severity = "ERROR",
                Fix = "Replace with for(int i) loop over List<T> or T[]"
            },
            // ── StartCoroutine ───────────────────────────────────
            new ViolationPattern
            {
                Name = "StartCoroutine",
                Regex = @"StartCoroutine\s*\(",
                Severity = "ERROR",
                Fix = "Use ITickable state machine with enum State + _timer"
            },
            // ── renderer.material (leak) ─────────────────────────
            new ViolationPattern
            {
                Name = "renderer.material access",
                Regex = @"\.material\s*[=;.\[]",
                Severity = "ERROR",
                Fix = "Use MaterialPropertyBlock + renderer.Get/SetPropertyBlock"
            },
            // ── Uncached GetComponent ────────────────────────────
            new ViolationPattern
            {
                Name = "GetComponent in potential hot path",
                Regex = @"GetComponent\s*<",
                Severity = "WARN",
                Fix = "Cache in Awake/Start. Use TryGetComponent for null-safe access"
            },
            // ── Camera.main ──────────────────────────────────────
            new ViolationPattern
            {
                Name = "Camera.main access",
                Regex = @"Camera\.main",
                Severity = "WARN",
                Fix = "Cache as private Camera _mainCam in Awake"
            },
            // ── Physics cast without NonAlloc ────────────────────
            new ViolationPattern
            {
                Name = "Physics cast without NonAlloc",
                Regex = @"Physics\.(Raycast|SphereCast|BoxCast|CapsuleCast|OverlapSphere|OverlapBox)\s*\(",
                Severity = "ERROR",
                Fix = "Use Physics.*NonAlloc with pre-allocated buffer"
            },
            // ── SendMessage ──────────────────────────────────────
            new ViolationPattern
            {
                Name = "SendMessage/BroadcastMessage",
                Regex = @"(SendMessage|BroadcastMessage|SendMessageUpwards)\s*\(",
                Severity = "ERROR",
                Fix = "Use interfaces, direct calls, or NativeQueue-backed event lanes"
            },
            // ── String concat in potential hot path ──────────────
            new ViolationPattern
            {
                Name = "String interpolation/concat",
                Regex = @"\$""[^""]*\{",
                Severity = "WARN",
                Fix = "Pre-cache strings or use StringBuilder in cold paths only"
            },
            // ── Debug.Log without guard ──────────────────────────
            new ViolationPattern
            {
                Name = "Debug.Log without #if guard",
                Regex = @"Debug\.(Log|LogWarning|LogError)\s*\(",
                Severity = "WARN",
                Fix = "Guard with #if UNITY_EDITOR || DEVELOPMENT_BUILD"
            },
            // ── foreach on Dictionary ────────────────────────────
            new ViolationPattern
            {
                Name = "foreach on Dictionary",
                Regex = @"foreach\s*\([^)]*KeyValuePair",
                Severity = "ERROR",
                Fix = "Use for(int i) with separate key/value arrays or List<T>"
            },
            // ── tag == "string" ──────────────────────────────────
            new ViolationPattern
            {
                Name = "tag == string comparison",
                Regex = @"\.tag\s*==\s*""",
                Severity = "ERROR",
                Fix = "Use gameObject.CompareTag(\"...\") instead"
            },
            // ── Animator.Set with string ─────────────────────────
            new ViolationPattern
            {
                Name = "Animator.Set* with string literal",
                Regex = @"\.(SetBool|SetFloat|SetInteger|SetTrigger)\s*\(\s*""",
                Severity = "ERROR",
                Fix = "Use Animator.StringToHash cached as static readonly int"
            },
            // ── new Action/Func/lambda ───────────────────────────
            new ViolationPattern
            {
                Name = "Delegate allocation",
                Regex = @"new\s+(Action|Func|EventHandler)\s*[<(]",
                Severity = "WARN",
                Fix = "Cache delegate as field. Subscribe once in OnEnable"
            },
            // ── Object.Instantiate in gameplay ───────────────────
            new ViolationPattern
            {
                Name = "Object.Instantiate (use pooling)",
                Regex = @"(?<!//.*)(Instantiate|Object\.Instantiate)\s*\(",
                Severity = "WARN",
                Fix = "Use ObjectPoolManager.Instance.Spawn() for frequent objects"
            },
            // ── FindObjectOfType ─────────────────────────────────
            new ViolationPattern
            {
                Name = "Scene-wide object lookup at runtime",
                Regex = @"\b(?:FindFirstObjectByType|FindAnyObjectByType|FindObjectOfType|FindObjectsOfType|FindObjectsByType|FindWithTag|GameObject\.FindWithTag)\s*[<(]",
                Severity = "ERROR",
                Fix = "Use GlobalRegistry, cached refs, or serialized references"
            },
            // ── GameObject.Find ──────────────────────────────────
            new ViolationPattern
            {
                Name = "GameObject.Find at runtime",
                Regex = @"GameObject\.Find\s*\(",
                Severity = "ERROR",
                Fix = "Use GlobalRegistry, cached refs, or serialized references"
            },
            new ViolationPattern
            {
                Name = "Unauthorized DontDestroyOnLoad",
                Regex = @"(?:Object\.)?DontDestroyOnLoad\s*\(",
                Severity = "ERROR",
                Fix = "Move lifecycle ownership to GameBootstrapper/GlobalRegistry, or document an explicit exception"
            },
        };

        // ══════════════════════════════════════════════════════════
        //  SCAN CONFIG
        // ══════════════════════════════════════════════════════════

        private const string ScanRoot = "Assets/_Project/Scripts";
        private static readonly string[] _excludeFolders = { "Editor", "Tests", "ThirdParty" };

        // Lines near #if UNITY_EDITOR are exempted for Debug.Log checks.
        private const int EditorGuardLookback = 5;

        // ══════════════════════════════════════════════════════════
        //  MENU COMMANDS
        // ══════════════════════════════════════════════════════════

        [MenuItem("Hecton8/Audit/Zero-GC Compliance Scan")]
        public static void RunFullScan()
        {
            var results = new List<string>(256); // COLD ALLOC: editor-only.
            int totalViolations = 0;
            int errorCount = 0;
            int warnCount = 0;
            int filesScanned = 0;

            string fullPath = Path.Combine(Application.dataPath, "_Project", "Scripts");
            if (!Directory.Exists(fullPath))
            {
                Debug.LogError($"[ZeroGC Scanner] Scan root not found: {fullPath}");
                return;
            }

            string[] files = Directory.GetFiles(fullPath, "*.cs", SearchOption.AllDirectories);

            // Compile regex patterns once.
            var compiledPatterns = new Regex[_patterns.Length]; // COLD ALLOC: editor-only.
            for (int p = 0; p < _patterns.Length; p++)
            {
                compiledPatterns[p] = new Regex(_patterns[p].Regex, RegexOptions.Compiled);
            }

            for (int f = 0; f < files.Length; f++)
            {
                string filePath = files[f];
                string relativePath = filePath.Replace(Application.dataPath, "Assets");

                // Skip excluded folders.
                bool excluded = false;
                for (int e = 0; e < _excludeFolders.Length; e++)
                {
                    if (relativePath.Contains($"/{_excludeFolders[e]}/") ||
                        relativePath.Contains($"\\{_excludeFolders[e]}\\"))
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded) continue;

                filesScanned++;
                string[] lines = File.ReadAllLines(filePath); // COLD ALLOC: editor-only.

                for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
                {
                    string line = lines[lineIdx];
                    string trimmed = line.TrimStart();

                    // Skip comments.
                    if (trimmed.StartsWith("//")) continue;
                    if (trimmed.StartsWith("///")) continue;
                    if (trimmed.StartsWith("/*")) continue;
                    if (trimmed.StartsWith("*")) continue;

                    for (int p = 0; p < _patterns.Length; p++)
                    {
                        if (!compiledPatterns[p].IsMatch(line)) continue;

                        // Special: Debug.Log check — exempt if within #if UNITY_EDITOR block.
                        if (_patterns[p].Name.StartsWith("Debug.Log"))
                        {
                            if (IsWithinEditorGuard(lines, lineIdx))
                                continue;
                        }

                        // Special: GetComponent — only flag if inside a method that looks like a hot path.
                        if (_patterns[p].Name.StartsWith("GetComponent"))
                        {
                            if (!IsInHotPathMethod(lines, lineIdx))
                                continue;
                        }

                        // Special: Instantiate — skip if preceded by COLD ALLOC comment.
                        if (_patterns[p].Name.StartsWith("Object.Instantiate"))
                        {
                            if (HasColdAllocComment(lines, lineIdx))
                                continue;
                        }

                        // Special: String interpolation — only flag if clearly in a hot path method.
                        if (_patterns[p].Name.StartsWith("String interpolation"))
                        {
                            if (!IsInHotPathMethod(lines, lineIdx))
                                continue;
                        }

                        // Special: DDOL is allowed only in the explicit bootstrap/crash owners.
                        if (_patterns[p].Name.StartsWith("Unauthorized DontDestroyOnLoad"))
                        {
                            if (IsAllowedDontDestroyOnLoadOwner(relativePath))
                                continue;
                        }

                        string entry = $"  [{_patterns[p].Severity}] {relativePath}:{lineIdx + 1} — {_patterns[p].Name}";
                        results.Add(entry);
                        totalViolations++;
                        if (_patterns[p].Severity == "ERROR") errorCount++;
                        else warnCount++;
                    }
                }
            }

            // ── Output Report ────────────────────────────────────
            var sb = new StringBuilder(4096); // COLD ALLOC: editor-only.

            sb.AppendLine("╔══════════════════════════════════════════════════════════╗");
            sb.AppendLine("║       HECTON-8 — ZERO-GC COMPLIANCE SCAN REPORT        ║");
            sb.AppendLine("╚══════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine($"  Files scanned: {filesScanned}");
            sb.AppendLine($"  Violations:    {totalViolations} ({errorCount} ERROR, {warnCount} WARN)");
            sb.AppendLine();

            if (results.Count == 0)
            {
                sb.AppendLine("  ✅ No violations detected. Zero-GC policy compliant.");
            }
            else
            {
                sb.AppendLine("  Findings:");
                for (int i = 0; i < results.Count; i++)
                    sb.AppendLine(results[i]);
            }

            sb.AppendLine();
            sb.AppendLine("══════════════════════════════════════════════════════════");

            if (errorCount > 0)
                Debug.LogError(sb.ToString());
            else if (warnCount > 0)
                Debug.LogWarning(sb.ToString());
            else
                Debug.Log(sb.ToString());
        }

        // ══════════════════════════════════════════════════════════
        //  CONTEXT ANALYSIS HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Allows the explicit persistent runtime owners.
        /// </summary>
        private static bool IsAllowedDontDestroyOnLoadOwner(string relativePath)
        {
            return relativePath.EndsWith("/Bootstrap/GameBootstrapper.cs", StringComparison.Ordinal) ||
                   relativePath.EndsWith("\\Bootstrap\\GameBootstrapper.cs", StringComparison.Ordinal) ||
                   relativePath.EndsWith("/CrashTelemetryBuffer.cs", StringComparison.Ordinal) ||
                   relativePath.EndsWith("\\CrashTelemetryBuffer.cs", StringComparison.Ordinal);
        }

        /// <summary>
        /// Checks if the given line index is within a #if UNITY_EDITOR block.
        /// </summary>
        private static bool IsWithinEditorGuard(string[] lines, int lineIdx)
        {
            int start = Math.Max(0, lineIdx - EditorGuardLookback);
            for (int i = lineIdx; i >= start; i--)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith("#if UNITY_EDITOR") ||
                    trimmed.StartsWith("#if DEVELOPMENT_BUILD") ||
                    trimmed.StartsWith("[System.Diagnostics.Conditional"))
                    return true;

                if (trimmed.StartsWith("#endif") || trimmed.StartsWith("#else"))
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Heuristic: checks if the current line is inside a hot-path method
        /// (Tick, FixedTick, SlowTick, Update, LateUpdate, FixedUpdate).
        /// </summary>
        private static bool IsInHotPathMethod(string[] lines, int lineIdx)
        {
            for (int i = lineIdx; i >= Math.Max(0, lineIdx - 50); i--)
            {
                string trimmed = lines[i].TrimStart();
                if (trimmed.Contains("void Tick(") ||
                    trimmed.Contains("void FixedTick(") ||
                    trimmed.Contains("void SlowTick(") ||
                    trimmed.Contains("void " + "Update(") ||
                    trimmed.Contains("void LateUpdate(") ||
                    trimmed.Contains("void " + "FixedUpdate("))
                    return true;

                // Stop at class/struct/namespace boundaries.
                if (trimmed.StartsWith("class ") ||
                    trimmed.StartsWith("struct ") ||
                    trimmed.StartsWith("namespace "))
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Checks if the line or preceding line has a "// COLD ALLOC" comment.
        /// </summary>
        private static bool HasColdAllocComment(string[] lines, int lineIdx)
        {
            if (lines[lineIdx].Contains("COLD ALLOC")) return true;
            if (lineIdx > 0 && lines[lineIdx - 1].Contains("COLD ALLOC")) return true;
            return false;
        }
    }
}
#endif
