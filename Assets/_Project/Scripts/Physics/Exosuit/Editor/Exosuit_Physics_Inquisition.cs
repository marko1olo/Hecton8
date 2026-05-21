#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Exosuit.Editor
{
    public static class Exosuit_Physics_Inquisition
    {
        [MenuItem("Hecton8/Physics/Run Exosuit Physics Inquisition")]
        public static void Run()
        {
            string projectRoot = ResolveProjectRoot();
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string aggregateReportPath = Path.Combine(projectRoot, "Docs", "Reports", "PHYSICS_OPTIMIZATION_REPORT.json");
            string reportPath = Path.Combine(projectRoot, "Docs", "Reports", "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_276.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            int fileCount = 0;
            int exosuitFiles = 0;
            int rigidbodyHits = 0;
            int jointHits = 0;
            int legacyMovementHits = 0;
            int guardedLegacyRigidbodyHits = 0;
            int unguardedLegacyMovementHits = 0;
            uint sourceHash = 2166136261u;

            StringBuilder violations = new StringBuilder(4096);
            string[] files = Directory.Exists(scriptsRoot)
                ? Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                : System.Array.Empty<string>();

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (Path.GetFileName(path) == "Exosuit_Physics_Inquisition.cs")
                    continue;

                fileCount++;
                string text = File.ReadAllText(path);
                sourceHash = MixHash(sourceHash, path);
                sourceHash = MixHash(sourceHash, text);
                if (text.IndexOf("Exosuit", System.StringComparison.OrdinalIgnoreCase) < 0 &&
                    text.IndexOf("mech", System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                exosuitFiles++;
                string[] lines = text.Split('\n');
                bool inLegacyExosuitMethod = false;
                bool legacyBraceSeen = false;
                int legacyBraceDepth = 0;
                bool legacyMethodGuardSeen = false;
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    bool legacyPath = IsLegacyExosuitPath(line);
                    if (legacyPath)
                    {
                        legacyMovementHits++;
                        AppendViolation(violations, projectRoot, path, lineIndex + 1, "legacy_exosuit_path", line);
                    }

                    if (IsLegacyExosuitMethodDefinition(line))
                    {
                        inLegacyExosuitMethod = true;
                        legacyBraceSeen = false;
                        legacyBraceDepth = 0;
                        legacyMethodGuardSeen = false;
                    }

                    if (inLegacyExosuitMethod &&
                        line.IndexOf("ExosuitKinematicAuthority.HasActiveAuthority", System.StringComparison.Ordinal) >= 0)
                    {
                        legacyMethodGuardSeen = true;
                    }

                    bool exosuitRelevantLine = inLegacyExosuitMethod || ContainsExosuitToken(line);
                    if (line.IndexOf("ConfigurableJoint", System.StringComparison.Ordinal) >= 0 ||
                        line.IndexOf("FixedJoint", System.StringComparison.Ordinal) >= 0)
                    {
                        if (exosuitRelevantLine)
                        {
                            jointHits++;
                            AppendViolation(violations, projectRoot, path, lineIndex + 1, "joint", line);
                        }
                    }

                    bool rigidbody = IsForbiddenPhysicsRoute(line);
                    if (rigidbody && exosuitRelevantLine)
                    {
                        if (inLegacyExosuitMethod && legacyMethodGuardSeen)
                        {
                            guardedLegacyRigidbodyHits++;
                            AppendViolation(violations, projectRoot, path, lineIndex + 1, "guarded_legacy_rigidbody_route", line);
                        }
                        else
                        {
                            rigidbodyHits++;
                            AppendViolation(violations, projectRoot, path, lineIndex + 1, "rigidbody_movement", line);
                        }
                    }

                    UpdateLegacyScope(line, ref inLegacyExosuitMethod, ref legacyBraceSeen, ref legacyBraceDepth);
                    if (!inLegacyExosuitMethod)
                        legacyMethodGuardSeen = false;
                }
            }

            bool layoutOk = ExosuitLayoutVerifier.ValidateRuntimeLayouts();
            bool forbiddenHits = rigidbodyHits != 0 || jointHits != 0 || unguardedLegacyMovementHits != 0;
            string summary = layoutOk && !forbiddenHits
                ? "No Unguarded Physics-Based Mech Movement Found"
                : "Forbidden Physics-Based Mech Movement Still Present";
            string verdict = layoutOk && !forbiddenHits
                ? "PASS_STATIC_NO_UNGUARDED_RIGIDBODY_MECH_ROUTE"
                : "FAIL_STATIC_FORBIDDEN_PHYSICS_ROUTE";
            StringBuilder json = new StringBuilder(8192);
            json.Append("{\n");
            AppendJson(json, "agent", "SHINOBU_276", 1).Append(",\n");
            AppendJson(json, "scope", "exosuit_6d_kinematic_integrator", 1).Append(",\n");
            AppendJson(json, "summary", summary, 1).Append(",\n");
            AppendJson(json, "verdict", verdict, 1).Append(",\n");
            AppendJson(json, "legacy_movement_hits_policy", "legacy ApplyExosuit* and indirect motor-force routes are warnings only when the same method scope has already passed ExosuitKinematicAuthority.HasActiveAuthority", 1).Append(",\n");
            json.Append("  \"scan_utc_ticks\": ").Append(System.DateTime.UtcNow.Ticks).Append(",\n");
            json.Append("  \"source_hash\": ").Append(sourceHash).Append(",\n");
            json.Append("  \"layout_ok\": ").Append(layoutOk ? "true" : "false").Append(",\n");
            json.Append("  \"files_scanned\": ").Append(fileCount).Append(",\n");
            json.Append("  \"exosuit_files\": ").Append(exosuitFiles).Append(",\n");
            json.Append("  \"rigidbody_hits\": ").Append(rigidbodyHits).Append(",\n");
            json.Append("  \"joint_hits\": ").Append(jointHits).Append(",\n");
            json.Append("  \"legacy_movement_hits\": ").Append(legacyMovementHits).Append(",\n");
            json.Append("  \"guarded_legacy_rigidbody_hits\": ").Append(guardedLegacyRigidbodyHits).Append(",\n");
            json.Append("  \"unguarded_legacy_movement_hits\": ").Append(unguardedLegacyMovementHits).Append(",\n");
            json.Append("  \"violations\": [\n");
            json.Append(violations);
            json.Append("\n  ]\n");
            json.Append("}\n");
            string reportJson = json.ToString();
            File.WriteAllText(reportPath, reportJson);
            TryUpsertAggregateReport(aggregateReportPath, reportJson);
            AssetDatabase.Refresh();
            if (forbiddenHits || !layoutOk)
                Debug.LogError("Exosuit physics inquisition failed. Report: " + reportPath);
            else
                Debug.Log("Exosuit physics inquisition wrote " + reportPath);
        }

        private static void TryUpsertAggregateReport(string aggregateReportPath, string reportJson)
        {
            const string NodeKey = "\"shinobu276ExosuitKinematicsScanner\"";
            string indentedReport = reportJson.TrimEnd().Replace("\n", "\n  ");
            if (!File.Exists(aggregateReportPath))
            {
                File.WriteAllText(aggregateReportPath, "{\n  " + NodeKey + ": " + indentedReport + "\n}\n");
                return;
            }

            string aggregate = File.ReadAllText(aggregateReportPath);
            int keyIndex = aggregate.IndexOf(NodeKey, System.StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int colon = aggregate.IndexOf(':', keyIndex);
                int objectStart = colon >= 0 ? aggregate.IndexOf('{', colon) : -1;
                int objectEnd = FindJsonObjectEnd(aggregate, objectStart);
                if (objectStart >= 0 && objectEnd >= objectStart)
                    File.WriteAllText(aggregateReportPath, aggregate.Substring(0, objectStart) + indentedReport + aggregate.Substring(objectEnd + 1));
                return;
            }

            int insert = aggregate.LastIndexOf('}');
            if (insert < 0)
                return;

            string prefix = aggregate.Substring(0, insert).TrimEnd();
            bool needsComma = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            string node = (needsComma ? ",\n" : "\n") + "  " + NodeKey + ": " + indentedReport + "\n";
            File.WriteAllText(aggregateReportPath, prefix + node + aggregate.Substring(insert));
        }

        private static int FindJsonObjectEnd(string text, int objectStart)
        {
            if (string.IsNullOrEmpty(text) || objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static void AppendViolation(StringBuilder builder, string projectRoot, string path, int line, string type, string source)
        {
            if (builder.Length > 0)
                builder.Append(",\n");

            builder.Append("    {");
            AppendJson(builder, "type", type, 0).Append(", ");
            AppendJson(builder, "path", MakeProjectRelative(projectRoot, path), 0).Append(", ");
            builder.Append("\"line\": ").Append(line).Append(", ");
            AppendJson(builder, "source", source.Trim(), 0);
            builder.Append("}");
        }

        private static bool ContainsExosuitToken(string line)
        {
            return line.IndexOf("Exosuit", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("mech", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsLegacyExosuitPath(string line)
        {
            return line.IndexOf("ApplyExosuitGrapplePhysics", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("ApplyExosuitJumpJets", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("AdvanceExosuitGrappleRequest", System.StringComparison.Ordinal) >= 0;
        }

        private static bool IsLegacyExosuitMethodDefinition(string line)
        {
            return line.IndexOf("void ApplyExosuitGrapplePhysics", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("void ApplyExosuitJumpJets", System.StringComparison.Ordinal) >= 0;
        }

        private static bool IsForbiddenPhysicsRoute(string line)
        {
            return line.IndexOf("AddForce", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("AddForceAtPosition", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("MovePosition", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("linearVelocity", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("ApplyMotorForce", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("ApplyMotorVelocityChange", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("ApplyClampedAccelerationForce", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("ApplyMotorAcceleration", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("QueueEnvironmentalVelocityChange", System.StringComparison.Ordinal) >= 0 ||
                   line.IndexOf("QueueSubsystemExternalAcceleration", System.StringComparison.Ordinal) >= 0;
        }

        private static void UpdateLegacyScope(string line, ref bool inLegacyExosuitMethod, ref bool braceSeen, ref int braceDepth)
        {
            if (!inLegacyExosuitMethod)
                return;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '{')
                {
                    braceDepth++;
                    braceSeen = true;
                }
                else if (c == '}')
                {
                    braceDepth--;
                }
            }

            if (braceSeen && braceDepth <= 0)
                inLegacyExosuitMethod = false;
        }

        private static uint MixHash(uint hash, string value)
        {
            if (string.IsNullOrEmpty(value))
                return hash;

            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return hash;
        }

        private static StringBuilder AppendJson(StringBuilder builder, string key, string value, int indent)
        {
            for (int i = 0; i < indent; i++)
                builder.Append("  ");
            builder.Append('"').Append(Escape(key)).Append("\": \"").Append(Escape(value)).Append('"');
            return builder;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string MakeProjectRelative(string projectRoot, string path)
        {
            string relative = path.StartsWith(projectRoot)
                ? path.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace('\\', '/');
        }

        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
#endif
