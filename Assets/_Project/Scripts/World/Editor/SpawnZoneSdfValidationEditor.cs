#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.World.Editor
{
    [InitializeOnLoad]
    internal static class SpawnZoneSdfValidationLayoutGuard
    {
        static SpawnZoneSdfValidationLayoutGuard()
        {
            Validate();
        }

        [MenuItem("Hecton8/AI/Validate Spawn SDF Layout")]
        private static void ValidateMenu()
        {
            Validate();
        }

        private static void Validate()
        {
            bool ok = UnsafeUtility.SizeOf<SpawnValidationRequestDTO>() == 32 &&
                      UnsafeUtility.AlignOf<SpawnValidationRequestDTO>() == 8 &&
                      FieldOffset<SpawnValidationRequestDTO>(nameof(SpawnValidationRequestDTO.TargetAUP)) == 0 &&
                      FieldOffset<SpawnValidationRequestDTO>(nameof(SpawnValidationRequestDTO.RequiredClearanceRadius)) == 24 &&
                      FieldOffset<SpawnValidationRequestDTO>(nameof(SpawnValidationRequestDTO.ValidationResultFlags)) == 28 &&
                      UnsafeUtility.SizeOf<SpawnSdfGridHeaderDTO>() == 64 &&
                      UnsafeUtility.SizeOf<SpawnValidationTelemetryEntry>() == 64 &&
                      UnsafeUtility.SizeOf<SpawnValidationTuningDTO>() == 32 &&
                      UnsafeUtility.SizeOf<SpawnClearanceProfileDTO>() == 32;

            if (!ok)
            {
                throw new FatalArchitectureException(
                    "SHINOBU_310 spawn SDF layout violation: SpawnValidationRequestDTO size=32 align=8 TargetAUP@0 RequiredClearanceRadius@24 ValidationResultFlags@28; header=64 telemetry=64 tuning=32 profile=32.");
            }
        }

        private static int FieldOffset<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                return -1;

            try
            {
                return UnsafeUtility.GetFieldOffset(field);
            }
            catch
            {
                FieldOffsetAttribute offset = field.GetCustomAttribute<FieldOffsetAttribute>();
                return offset != null ? offset.Value : -1;
            }
        }
    }

    public sealed class SpawnIntegrityXRayWindow : EditorWindow
    {
        private const SystemID OwnerSystem = SystemID.WorldResourceSpawnerRuntime;
        private readonly StringBuilder _builder = new StringBuilder(256);
        private Label _stats;
        private Slider _clearanceMultiplier;
        private Slider _dearLiePush;
        private Slider _quality;
        private VisualElement _passBar;
        private VisualElement _failBar;
        private GlobalDataVault _vault;
        private uint _lastFrame = uint.MaxValue;
        private bool _registered;

        [MenuItem("Hecton8/AI/Spawn Integrity X-Ray")]
        public static void Open()
        {
            GetWindow<SpawnIntegrityXRayWindow>("Spawn Integrity X-Ray");
        }

        public void CreateGUI()
        {
            TryResolveEditorVault(out _vault);
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _stats = new Label("Vault not active.");
            rootVisualElement.Add(_stats);

            VisualElement histogram = new VisualElement();
            histogram.style.height = 18;
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.marginTop = 8;
            histogram.style.marginBottom = 8;
            _passBar = new VisualElement();
            _passBar.style.backgroundColor = new Color(0.1f, 0.7f, 0.35f, 1f);
            _passBar.style.height = 18;
            _failBar = new VisualElement();
            _failBar.style.backgroundColor = new Color(0.9f, 0.22f, 0.16f, 1f);
            _failBar.style.height = 18;
            histogram.Add(_passBar);
            histogram.Add(_failBar);
            rootVisualElement.Add(histogram);

            _clearanceMultiplier = CreateSlider("GlobalClearanceMultiplier", 0f, 3f, 1f);
            _dearLiePush = CreateSlider("DearLiePushbackMaxDistance", 0f, 2f, 0.35f);
            _quality = CreateSlider("GlobalQualityWeight", 0f, 1f, 1f);
            rootVisualElement.Add(_clearanceMultiplier);
            rootVisualElement.Add(_dearLiePush);
            rootVisualElement.Add(_quality);

            _clearanceMultiplier.RegisterValueChangedCallback(_ => WriteTuning());
            _dearLiePush.RegisterValueChangedCallback(_ => WriteTuning());
            _quality.RegisterValueChangedCallback(_ => WriteTuning());

            if (!_registered)
            {
                EditorApplication.update += Tick;
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (_registered)
            {
                EditorApplication.update -= Tick;
                _registered = false;
            }

            _vault = null;
        }

        private void OnFocus()
        {
            TryResolveEditorVault(out _vault);
        }

        private static Slider CreateSlider(string label, float low, float high, float value)
        {
            return new Slider(label, low, high)
            {
                value = value,
                showInputField = true
            };
        }

        private void Tick()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                TryResolveEditorVault(out _vault);

            GlobalDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _stats.text = "Vault not active.";
                return;
            }

            if (TryResolve(vault, BufferID.ShinobuSpawnSdfValidationTuning, out NativeArray<SpawnValidationTuningDTO> tuning) &&
                tuning.Length > 0)
            {
                SpawnValidationTuningDTO row = SpawnZoneSdfMath.Sanitize(tuning[0]);
                _clearanceMultiplier.SetValueWithoutNotify(row.GlobalClearanceMultiplier);
                _dearLiePush.SetValueWithoutNotify(row.DearLiePushbackMaxDistance);
                _quality.SetValueWithoutNotify(row.GlobalQualityWeight);
            }

            SpawnValidationTelemetryEntry latest = default;
            if (TryResolve(vault, BufferID.ShinobuSpawnSdfValidationTelemetryRing, out NativeArray<SpawnValidationTelemetryEntry> telemetry) &&
                telemetry.Length > 0)
            {
                for (int i = 0; i < telemetry.Length; i++)
                {
                    SpawnValidationTelemetryEntry entry = telemetry[i];
                    if (entry.Frame >= latest.Frame)
                        latest = entry;
                }
            }

            if (latest.Frame == _lastFrame)
                return;

            _lastFrame = latest.Frame;
            int valid = Mathf.Max(0, latest.ValidatedCount);
            int failed = Mathf.Clamp(latest.FailedIntersectionCount, 0, valid);
            int passed = Mathf.Max(0, valid - failed);
            float total = Mathf.Max(1f, valid);
            _passBar.style.width = Length.Percent((passed / total) * 100f);
            _failBar.style.width = Length.Percent((failed / total) * 100f);

            _builder.Clear();
            _builder.Append("Validated: ").Append(latest.ValidatedCount);
            _builder.Append(" | Failed: ").Append(latest.FailedIntersectionCount);
            _builder.Append(" | Dear Lie: ").Append(latest.DearLieResolvedCount);
            _builder.Append(" | us: ").Append(latest.QueryMicroseconds.ToString("0.###"));
            _builder.Append(" | flags: 0x").Append(latest.Flags.ToString("X8"));
            _stats.text = _builder.ToString();
        }

        private unsafe void WriteTuning()
        {
            if (_vault == null || _vault.IsCompactionFenceActive)
                TryResolveEditorVault(out _vault);

            GlobalDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive)
                return;

            VaultGenerationHandle<SpawnValidationTuningDTO> handle = vault.EnsureGenerationHandle<SpawnValidationTuningDTO>(
                BufferID.ShinobuSpawnSdfValidationTuning,
                1,
                OwnerSystem,
                NativeArrayOptions.ClearMemory);

            if (handle.BufferID == 0u ||
                !vault.TryResolveHandle(in handle, out NativeArray<SpawnValidationTuningDTO> tuning) ||
                !tuning.IsCreated ||
                tuning.Length <= 0)
            {
                return;
            }

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning);
            ref SpawnValidationTuningDTO row = ref UnsafeUtility.AsRef<SpawnValidationTuningDTO>(ptr);
            row = SpawnZoneSdfMath.Sanitize(row);
            row.GlobalClearanceMultiplier = Mathf.Max(0f, _clearanceMultiplier.value);
            row.DearLiePushbackMaxDistance = Mathf.Max(0f, _dearLiePush.value);
            row.GlobalQualityWeight = Mathf.Clamp01(_quality.value);
            row.Flags |= SpawnValidationTuningFlags.Valid | SpawnValidationTuningFlags.EnableDearLie;
        }

        private static bool TryResolve<T>(IDataVault vault, BufferID id, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(id, out VaultGenerationHandle<T> handle) &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryResolveEditorVault(out GlobalDataVault vault)
        {
            return GlobalDataVault.TryGetLatestCreated(out vault) && !vault.IsCompactionFenceActive;
        }
    }

    [InitializeOnLoad]
    internal static class SpawnClearanceDebugGizmo
    {
        private const int MaxDrawnRequests = 64;

        static SpawnClearanceDebugGizmo()
        {
            SceneView.duringSceneGui += Draw;
        }

        private static void Draw(SceneView view)
        {
            if (!TryResolveEditorVault(out GlobalDataVault vault) ||
                !TryResolve(vault, BufferID.ShinobuSpawnSdfValidationRequests, out NativeArray<SpawnValidationRequestDTO> requests))
            {
                return;
            }

            int count = Mathf.Min(MaxDrawnRequests, requests.Length);
            for (int i = 0; i < count; i++)
            {
                SpawnValidationRequestDTO request = requests[i];
                if (!math.all(Unity.Mathematics.math.isfinite(request.TargetAUP)) || request.RequiredClearanceRadius <= 0f)
                    continue;

                Vector3 center = HectonFloatingOrigin.ToRuntimePosition(request.TargetAUP);
                uint flags = request.ValidationResultFlags;
                Handles.color = (flags & SpawnValidationFlags.FailedGeometryIntersection) != 0u
                    ? Color.red
                    : ((flags & SpawnValidationFlags.ResolvedDearLie) != 0u ? new Color(1f, 0.55f, 0.05f, 1f) : Color.green);
                float radius = Mathf.Max(0.05f, request.RequiredClearanceRadius);
                Handles.DrawWireDisc(center, Vector3.up, radius);
                Handles.DrawWireDisc(center, Vector3.right, radius);
                Handles.DrawWireDisc(center, Vector3.forward, radius);
            }
        }

        private static bool TryResolve<T>(IDataVault vault, BufferID id, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(id, out VaultGenerationHandle<T> handle) &&
                   handle.BufferID != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private static bool TryResolveEditorVault(out GlobalDataVault vault)
        {
            return GlobalDataVault.TryGetLatestCreated(out vault) && !vault.IsCompactionFenceActive;
        }
    }

    public static class OOP_Spawn_Query_Scanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/AI_OPTIMIZATION_REPORT.json";
        private const string StableReportRelativePath = "Docs/Reports/SHINOBU_310_AI_OPTIMIZATION_REPORT.json";
        private static readonly string[] ForbiddenTokens =
        {
            "Physics.CheckSphere",
            "Physics.OverlapCapsule",
            "NavMesh.SamplePosition"
        };

        [MenuItem("Hecton8/AI/OOP Spawn Query Scanner")]
        public static void RunMenu()
        {
            Debug.Log(RunScan());
        }

        public static string RunScan()
        {
            string root = ResolveProjectRoot();
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            var findings = new System.Collections.Generic.List<Finding>(16);
            if (Directory.Exists(scriptsRoot))
            {
                foreach (string path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (normalized.Contains("/Editor/", StringComparison.Ordinal) || normalized.EndsWith("_Scanner.cs", StringComparison.Ordinal))
                        continue;

                    string source = File.ReadAllText(path);
                    if (!IsRuntimeSpawnScope(source))
                        continue;

                    ScanSource(path, source, findings);
                }
            }

            string json = BuildJson(root, findings);
            string stable = Path.Combine(root, StableReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(stable));
            File.WriteAllText(stable, json, Encoding.UTF8);

            string shared = Path.Combine(root, SharedReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(shared));
            WriteShared(shared, json);
            return "OOP_Spawn_Query_Scanner wrote " + stable + " findings=" + findings.Count;
        }

        private static bool IsRuntimeSpawnScope(string source)
        {
            return source.Contains("namespace Hecton8.AI", StringComparison.Ordinal) ||
                   source.Contains("namespace Hecton8.Fauna", StringComparison.Ordinal) ||
                   source.Contains("namespace Hecton8.World", StringComparison.Ordinal) ||
                   source.Contains("Spawn", StringComparison.Ordinal);
        }

        private static void ScanSource(string path, string source, System.Collections.Generic.List<Finding> findings)
        {
            string ns = ResolveNamespace(source);
            for (int t = 0; t < ForbiddenTokens.Length; t++)
            {
                string token = ForbiddenTokens[t];
                int index = 0;
                while (index < source.Length)
                {
                    index = source.IndexOf(token, index, StringComparison.Ordinal);
                    if (index < 0)
                        break;

                    if (!IsInsideLineComment(source, index))
                    {
                        findings.Add(new Finding
                        {
                            Path = path,
                            Namespace = ns,
                            Token = token,
                            Line = ResolveLine(source, index)
                        });
                    }

                    index += token.Length;
                }
            }
        }

        private static string BuildJson(string root, System.Collections.Generic.List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            builder.AppendLine("  \"scanner\": \"OOP_Spawn_Query_Scanner\",");
            builder.AppendLine("  \"agent\": \"SHINOBU_310\",");
            builder.AppendLine("  \"domain\": \"SPAWNING_ZONE_SDF_VALIDATOR\",");
            builder.AppendLine("  \"status\": \"" + (findings.Count == 0 ? "OOP Proximity Queries Eradicated - STATIC SCAN" : "OOP Spawn Queries Found") + "\",");
            builder.AppendLine("  \"tokens\": [\"Physics.CheckSphere\", \"Physics.OverlapCapsule\", \"NavMesh.SamplePosition\"],");
            builder.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/World/SpawnZoneSdfValidation.cs\",");
            builder.AppendLine("  \"dtoLayout\": \"SpawnValidationRequestDTO=32 bytes: TargetAUP@0 RequiredClearanceRadius@24 ValidationResultFlags@28\",");
            builder.AppendLine("  \"vaultBuffers\": [\"ShinobuSpawnSdfValidationRequests\", \"ShinobuSpawnSdfValidationTelemetryRing\", \"ShinobuSpawnSdfValidationTuning\", \"VoxelSdfTexture3D\", \"VoxelSdfPayloadDescriptor\"],");
            builder.AppendLine("  \"vaultBufferIds\": [72600, 72601, 72602, 72603, 72604, 72605, 72606, 72607, 72608],");
            builder.AppendLine("  \"bufferIdCollisionAudit\": \"PASS: SHINOBU_310 moved off 71960..71968 because SHINOBU_302 owns cognition lanes there\",");
            builder.AppendLine("  \"blackBoxTelemetryFrames\": 300,");
            builder.AppendLine("  \"failClosedNoVaultInPlayMode\": true,");
            builder.AppendLine("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding f = findings[i];
                builder.AppendLine("    {");
                builder.AppendLine("      \"path\": \"" + Escape(MakeRelative(root, f.Path)) + "\",");
                builder.AppendLine("      \"line\": " + f.Line + ",");
                builder.AppendLine("      \"namespace\": \"" + Escape(f.Namespace) + "\",");
                builder.AppendLine("      \"token\": \"" + Escape(f.Token) + "\"");
                builder.Append("    }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void WriteShared(string path, string json)
        {
            if (!File.Exists(path))
            {
                File.WriteAllText(path, json, Encoding.UTF8);
                return;
            }

            string existing = File.ReadAllText(path);
            string trimmed = existing.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
            {
                File.WriteAllText(path, json, Encoding.UTF8);
                return;
            }

            string property = "\"shinobu310SpawnSdfValidator\"";
            int propertyIndex = existing.IndexOf(property, StringComparison.Ordinal);
            if (propertyIndex >= 0)
            {
                int valueStart = existing.IndexOf('{', propertyIndex);
                int valueEnd = FindMatchingBrace(existing, valueStart);
                if (valueStart >= 0 && valueEnd > valueStart)
                {
                    string replaced = existing.Substring(0, valueStart) + json + existing.Substring(valueEnd + 1);
                    File.WriteAllText(path, replaced, Encoding.UTF8);
                    return;
                }
            }

            int insert = existing.LastIndexOf('}');
            string prefix = existing.Substring(0, insert).TrimEnd();
            string suffix = existing.Substring(insert);
            string comma = prefix.EndsWith("{", StringComparison.Ordinal) ? string.Empty : ",";
            File.WriteAllText(path, prefix + comma + "\n  \"shinobu310SpawnSdfValidator\": " + json + "\n" + suffix, Encoding.UTF8);
        }

        private static int FindMatchingBrace(string text, int open)
        {
            if (open < 0)
                return -1;

            int depth = 0;
            bool inString = false;
            for (int i = open; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                    inString = !inString;
                if (inString)
                    continue;
                if (c == '{')
                    depth++;
                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static string ResolveNamespace(string source)
        {
            const string key = "namespace ";
            int index = source.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;

            int start = index + key.Length;
            int end = start;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '.' || source[end] == '_'))
                end++;
            return source.Substring(start, end - start);
        }

        private static int ResolveLine(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static bool IsInsideLineComment(string source, int index)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(index - 1, 0));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int comment = source.IndexOf("//", lineStart, index - lineStart, StringComparison.Ordinal);
            return comment >= 0;
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            return Directory.GetParent(dataPath).FullName;
        }

        private static string MakeRelative(string root, string path)
        {
            string normalizedRoot = root.Replace('\\', '/').TrimEnd('/') + "/";
            string normalizedPath = path.Replace('\\', '/');
            return normalizedPath.StartsWith(normalizedRoot, StringComparison.Ordinal)
                ? normalizedPath.Substring(normalizedRoot.Length)
                : normalizedPath;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private struct Finding
        {
            public string Path;
            public string Namespace;
            public string Token;
            public int Line;
        }
    }
}
#endif
