using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Hecton8.EditorTools.Diagnostics
{
    /// <summary>
    /// Finds subsystem initialisers that nothing calls.
    ///
    /// Written after HectonIndirectVegetationRenderer.EnsureBatchRendererGroupResources turned out to
    /// be the only place a BatchRendererGroup is ever constructed, with zero callers anywhere in the
    /// project - an entire rendering path silently inert. That is a class of defect no compiler
    /// warns about and no runtime probe can see, because the code simply never runs.
    ///
    /// The signal is deliberately narrow: an init-shaped PRIVATE method, with no Unity attribute that
    /// would make the engine call it, that nothing invokes anywhere, AND which is the only
    /// constructive assigner of at least one field in its file. That last clause is what separates
    /// "dead duplicate of a live path" from "this field is never set at all". Across 2074 runtime
    /// source files it currently matches exactly one method - the one above.
    ///
    /// Two counting rules earned the hard way, both of which produced wrong answers first:
    ///   - CALLS must be counted PROJECT-WIDE. This codebase leans on partial classes
    ///     (Foo_Bar.cs, Foo.Baz.cs), so a method declared in one shard is routinely called from
    ///     another. Counting per file reported eight false positives, every one a partial shard.
    ///   - FIELD assignments must be counted PER FILE. Private field names collide across classes -
    ///     several renderers have a _batchRendererGroup - so a project-wide count hid the one real
    ///     hit behind other classes' identically named fields.
    ///
    /// Text analysis, so it cannot see reflection, UnityEvent wiring or SendMessage. A hit is a
    /// question, not a verdict: read the method before concluding anything.
    /// </summary>
    public static class H8_UncalledInitialiserGate
    {
        private const string Marker = "[H8_DEAD_INIT]";
        private const string RuntimeRoot = "Assets/_Project/Scripts";

        /// <summary>
        /// Known and deliberate. EnsureBatchRendererGroupResources is documented in place: the BRG
        /// path cannot be enabled by simply calling it, because BatchRendererGroup does not consume
        /// MaterialPropertyBlock and that renderer publishes every binding through one. Raise this
        /// only when a new hit has been read and judged intentional, and say why here.
        /// </summary>
        private const int ExpectedHitCount = 1;

        private static readonly Regex InitialiserDeclaration = new Regex(
            @"private\s+(?:static\s+)?(?:void|bool)\s+((?:Ensure|Initialize|Install|Setup|Bootstrap|Create)[A-Za-z0-9_]*)\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex EngineInvokedAttribute = new Regex(
            @"\[\s*(RuntimeInitializeOnLoadMethod|InitializeOnLoadMethod|MenuItem|ContextMenu|Preserve)",
            RegexOptions.Compiled);

        private static readonly Regex CallToken = new Regex(
            @"([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);

        // A constructive assignment, i.e. not a teardown reset. Without this exclusion the one real
        // hit is masked by its own `_batchRendererGroup = null` in the release path.
        private static readonly Regex ConstructiveFieldAssignment = new Regex(
            @"(_[A-Za-z0-9_]+)\s*=\s*(?!=)(?!\s*(?:null|default|false|0|-1|Vector2\.zero|Vector3\.zero|Vector4\.zero)\s*;)",
            RegexOptions.Compiled);

        public static void Run()
        {
            Dictionary<string, string> sources = LoadRuntimeSources();
            Debug.Log($"{Marker} runtime sources indexed = {sources.Count}");

            var callCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string source in sources.Values)
            {
                foreach (Match match in CallToken.Matches(source))
                {
                    string token = match.Groups[1].Value;
                    callCounts.TryGetValue(token, out int seen);
                    callCounts[token] = seen + 1;
                }
            }

            var hits = new List<string>();
            foreach (KeyValuePair<string, string> file in sources)
            {
                Dictionary<string, int> fileAssignments = CountAssignments(file.Value);

                foreach (Match declaration in InitialiserDeclaration.Matches(file.Value))
                {
                    string name = declaration.Groups[1].Value;

                    int windowStart = Math.Max(0, declaration.Index - 260);
                    if (EngineInvokedAttribute.IsMatch(file.Value.Substring(windowStart, declaration.Index - windowStart)))
                        continue;

                    // The declaration itself is one token, so anything above one is a real call.
                    callCounts.TryGetValue(name, out int calls);
                    if (calls > 1)
                        continue;

                    if (!TryReadBody(file.Value, declaration.Index + declaration.Length, out string body))
                        continue;

                    string[] soleOwned = CountAssignments(body)
                        .Where(pair => fileAssignments.TryGetValue(pair.Key, out int total) && total == pair.Value)
                        .Select(pair => pair.Key)
                        .OrderBy(field => field, StringComparer.Ordinal)
                        .ToArray();

                    if (soleOwned.Length == 0)
                        continue;

                    hits.Add($"{file.Key} -> {name}");
                    Debug.Log(
                        $"{Marker} UNCALLED {name} in {file.Key} - sole constructive owner of: " +
                        string.Join(", ", soleOwned));
                }
            }

            Debug.Log($"{Marker} hits={hits.Count} expected={ExpectedHitCount}");
            Debug.Log($"{Marker} RESULT failures={(hits.Count > ExpectedHitCount ? 1 : 0)}");
            Debug.Log($"{Marker} DONE");
        }

        private static Dictionary<string, string> LoadRuntimeSources()
        {
            var sources = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!Directory.Exists(RuntimeRoot))
                return sources;

            foreach (string path in Directory.EnumerateFiles(RuntimeRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains("/Editor") || normalized.Contains("/Tests"))
                    continue;

                try
                {
                    sources[normalized] = File.ReadAllText(path);
                }
                catch (IOException)
                {
                    // A file another agent is mid-write on. Skipping it can only hide a hit, never
                    // invent one.
                }
            }

            return sources;
        }

        private static Dictionary<string, int> CountAssignments(string text)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Match match in ConstructiveFieldAssignment.Matches(text))
            {
                string field = match.Groups[1].Value;
                counts.TryGetValue(field, out int seen);
                counts[field] = seen + 1;
            }

            return counts;
        }

        private static bool TryReadBody(string source, int searchFrom, out string body)
        {
            body = null;
            int open = source.IndexOf('{', searchFrom);
            if (open < 0)
                return false;

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        body = source.Substring(open, i - open + 1);
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
