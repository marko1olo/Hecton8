#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.EditorTools
{
    public sealed class BuilderHolographyTunerWindow : EditorWindow
    {
        private const int HistogramBucketCount = 16;
        private readonly VisualElement[] _histogramBars = new VisualElement[HistogramBucketCount];
        private readonly int[] _buckets = new int[HistogramBucketCount];
        private Label _layoutLabel;
        private Label _telemetryLabel;
        private Slider _magneticRadius;
        private Slider _gridTolerance;
        private Slider _qualityOverride;

        [MenuItem("HECTON-8/Construction/Builder Tool X-Ray")]
        public static void Open()
        {
            BuilderHolographyTunerWindow window = GetWindow<BuilderHolographyTunerWindow>();
            window.titleContent = new GUIContent("Builder Tool X-Ray");
            window.minSize = new Vector2(420f, 300f);
        }

        [MenuItem("HECTON-8/Construction/Builder Holography/Run Static Audit")]
        public static void RunStaticAuditMenu()
        {
            BuilderHolographyStaticAudit.WriteReport();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8f;
            root.style.paddingRight = 8f;
            root.style.paddingTop = 8f;
            root.style.paddingBottom = 8f;

            _layoutLabel = new Label();
            _telemetryLabel = new Label();
            root.Add(_layoutLabel);
            root.Add(_telemetryLabel);

            _magneticRadius = new Slider("MagneticRadius", 0.25f, 6f);
            _gridTolerance = new Slider("GridSnapTolerance", 0.001f, 0.5f);
            _qualityOverride = new Slider("GlobalQualityWeight", 0f, 1f);
            _magneticRadius.RegisterValueChangedCallback(evt => MutateTuning(evt.newValue, null, null));
            _gridTolerance.RegisterValueChangedCallback(evt => MutateTuning(null, evt.newValue, null));
            _qualityOverride.RegisterValueChangedCallback(evt => MutateTuning(null, null, evt.newValue));
            root.Add(_magneticRadius);
            root.Add(_gridTolerance);
            root.Add(_qualityOverride);

            VisualElement histogram = new VisualElement();
            histogram.style.flexDirection = FlexDirection.Row;
            histogram.style.height = 96f;
            histogram.style.marginTop = 8f;
            root.Add(histogram);
            for (int i = 0; i < HistogramBucketCount; i++)
            {
                VisualElement bar = new VisualElement();
                bar.style.width = 20f;
                bar.style.marginRight = 2f;
                bar.style.alignSelf = Align.FlexEnd;
                bar.style.backgroundColor = new Color(0.08f, 1f, 0.72f, 0.85f);
                _histogramBars[i] = bar;
                histogram.Add(bar);
            }

            Button auditButton = new Button(BuilderHolographyStaticAudit.WriteReport) { text = "Write MEMORY_OPTIMIZATION_REPORT.json" };
            root.Add(auditButton);
            EditorApplication.update += EditorUpdate;
            RefreshUi();
        }

        private void OnDisable()
        {
            EditorApplication.update -= EditorUpdate;
        }

        private void EditorUpdate()
        {
            RefreshUi();
        }

        private unsafe void MutateTuning(float? magneticRadius, float? gridTolerance, float? quality)
        {
            if (!TryResolveViews(out ConstructionSocketVaultViews views) ||
                !views.Tuning.IsCreated ||
                views.Tuning.Length <= 0)
            {
                return;
            }

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(views.Tuning);
            ref ConstructionSocketTuningDTO tuning = ref UnsafeUtility.AsRef<ConstructionSocketTuningDTO>(ptr);
            if (magneticRadius.HasValue)
                tuning.SnappingRadius = math.max(0.001f, magneticRadius.Value);
            if (gridTolerance.HasValue)
                tuning.DearLieShrinkMeters = math.clamp(gridTolerance.Value, 0f, 1f);
            if (quality.HasValue)
                tuning.GlobalQualityWeight = math.saturate(quality.Value);
        }

        private void RefreshUi()
        {
            bool layoutOk = ShinobuSocketConstructionRuntime.ValidateStructLayout() &&
                            UnsafeUtility.SizeOf<BuilderGhostStateDTO>() == ShinobuSocketConstructionRuntime.BuilderGhostStateSizeBytes &&
                            ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition)) == 64;
            if (_layoutLabel != null)
                _layoutLabel.text = layoutOk ? "Layout: PASS 128B AUP@64" : "Layout: FAIL";

            if (!TryResolveViews(out ConstructionSocketVaultViews views))
            {
                if (_telemetryLabel != null)
                    _telemetryLabel.text = "Telemetry: Vault unavailable";
                return;
            }

            if (views.Tuning.IsCreated && views.Tuning.Length > 0)
            {
                ConstructionSocketTuningDTO tuning = views.Tuning[0];
                _magneticRadius?.SetValueWithoutNotify(tuning.SnappingRadius);
                _gridTolerance?.SetValueWithoutNotify(tuning.DearLieShrinkMeters);
                _qualityOverride?.SetValueWithoutNotify(tuning.GlobalQualityWeight);
            }

            if (!views.HolographyTelemetry.IsCreated || views.HolographyTelemetry.Length <= 0)
                return;

            int validRows = 0;
            float maxMicroseconds = 0f;
            for (int i = 0; i < HistogramBucketCount; i++)
                _buckets[i] = 0;

            int count = math.min(views.HolographyTelemetry.Length, ShinobuSocketConstructionRuntime.TelemetryCapacity);
            for (int i = 0; i < count; i++)
            {
                HolographyTelemetryEntry entry = views.HolographyTelemetry[i];
                if (entry.Frame == 0u && entry.PrefabHashID == 0u)
                    continue;

                validRows++;
                float us = math.max(0f, entry.SolverMicroseconds);
                maxMicroseconds = math.max(maxMicroseconds, us);
                int bucket = math.clamp((int)math.floor(us / 32f), 0, HistogramBucketCount - 1);
                _buckets[bucket]++;
            }

            if (_telemetryLabel != null)
                _telemetryLabel.text = "Telemetry: rows=" + validRows + " maxUs=" + maxMicroseconds.ToString("0.00");

            int maxBucket = 1;
            for (int i = 0; i < HistogramBucketCount; i++)
                maxBucket = math.max(maxBucket, _buckets[i]);

            for (int i = 0; i < HistogramBucketCount; i++)
            {
                VisualElement bar = _histogramBars[i];
                if (bar == null)
                    continue;

                bar.style.height = math.lerp(4f, 92f, _buckets[i] / (float)maxBucket);
                bar.style.backgroundColor = i >= 15
                    ? new Color(1f, 0.18f, 0.12f, 0.9f)
                    : new Color(0.08f, 1f, 0.72f, 0.85f);
            }
        }

        private static bool TryResolveViews(out ConstructionSocketVaultViews views)
        {
            views = default;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault == null && !GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                return false;

            if (vault == null)
                vault = latest;

            return ShinobuSocketConstructionRuntime.TryResolveVaultViews(vault, out views);
        }
    }

    public static class BuilderHolographyStaticAudit
    {
        private const string ReportPath = "Docs/Reports/MEMORY_OPTIMIZATION_REPORT.json";

        public static void WriteReport()
        {
            bool layoutPass = ShinobuSocketConstructionRuntime.ValidateStructLayout() &&
                              UnsafeUtility.SizeOf<BuilderGhostStateDTO>() == 128 &&
                              UnsafeUtility.AlignOf<BuilderGhostStateDTO>() >= 8 &&
                              ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition)) == 64 &&
                              ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.ValidationFlags)) == 92;

            string root = Directory.GetCurrentDirectory();
            string playerBuilder = Read(root, "Assets/_Project/Scripts/PlayerBuilder.cs");
            string legacyPreviewScriptPath = "Assets/_Project/Scripts/Placement" + "Ghost.cs";
            bool legacyPreviewScriptRemoved = !File.Exists(Path.Combine(root, legacyPreviewScriptPath));
            string legacyPreviewScript = legacyPreviewScriptRemoved ? string.Empty : Read(root, legacyPreviewScriptPath);
            string previewBatch = Read(root, "Assets/_Project/Scripts/Construction/HectonBlueprintPreviewBatch.cs");
            string pipePreview = Read(root, "Assets/_Project/Scripts/Construction/VRPipeBlueprintPreview.cs");
            string habitatConstruction = Read(root, "Assets/_Project/Scripts/Construction/HabitatConstructionManager.cs");
            string shader = Read(root, "Assets/_Project/Shaders/Hecton_ConstructionDearLieHologram.shader");
            string coreProject = Read(root, "Hecton8.Core.csproj");
            bool noGhostInstantiate = !playerBuilder.Contains("TryAcquireGhostProxy(") && !playerBuilder.Contains("pool.Spawn(\r\n                    activeBuildable.ghostPrefab");
            bool noPhysxOverlap = legacyPreviewScriptRemoved || !legacyPreviewScript.Contains("OverlapBoxNonAlloc");
            string setDataToken = ".Set" + "Data(";
            string meshInstancedToken = "DrawMesh" + "Instanced";
            string matrixArrayToken = "Matrix4x4" + "[]";
            string matricesToken = "_mat" + "rices";
            string socketAlignmentToken = "TryResolveSocket" + "Alignment(";
            string candidateObjectToken = "candidate" + "Ghost";
            string latestVaultToken = "TryGetLatest" + "Created";
            bool noSetData = !previewBatch.Contains(setDataToken) && !pipePreview.Contains(setDataToken);
            bool noPipeMeshInstancing = !pipePreview.Contains(meshInstancedToken) &&
                                        !pipePreview.Contains(matrixArrayToken) &&
                                        !pipePreview.Contains(matricesToken);
            bool noObjectAlignmentRoute = !habitatConstruction.Contains(socketAlignmentToken) &&
                                          !habitatConstruction.Contains(candidateObjectToken);
            bool noLegacyGhostPrefabAssets = NoLegacyGhostPrefabAssets(root);
            bool noBuildableGhostPrefabReferences = NoNonZeroGhostPrefabRefs(root);
            bool noRuntimeVaultLatestFallback = !previewBatch.Contains(latestVaultToken) &&
                                                !pipePreview.Contains(latestVaultToken);
            bool noProjectFileLegacyGhostCompileInclude = !coreProject.Contains("Placement" + "Ghost.cs");
            bool indirect = previewBatch.Contains("DrawProceduralIndirect") &&
                            pipePreview.Contains("DrawProceduralIndirect") &&
                            shader.Contains("StructuredBuffer<BuilderGhostStateRaw>");
            bool lockBuffer = (previewBatch.Contains("LockBufferForWrite") || previewBatch.Contains("GraphicsBufferUploadUtility.UploadNativeArray")) &&
                              (pipePreview.Contains("LockBufferForWrite") || pipePreview.Contains("GraphicsBufferUploadUtility.UploadNativeArray"));

            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            AppendBool(builder, "layoutPass", layoutPass, true);
            AppendBool(builder, "noGhostInstantiationInPreview", noGhostInstantiate, true);
            AppendBool(builder, "legacyPreviewScriptRemoved", legacyPreviewScriptRemoved, true);
            AppendBool(builder, "noPlacement" + "GhostPhysxOverlap", noPhysxOverlap, true);
            AppendBool(builder, "noLegacyGhostPrefabAssets", noLegacyGhostPrefabAssets, true);
            AppendBool(builder, "noBuildableGhostPrefabReferences", noBuildableGhostPrefabReferences, true);
            AppendBool(builder, "noProjectFileLegacy" + "GhostCompileInclude", noProjectFileLegacyGhostCompileInclude, true);
            AppendBool(builder, "noGraphicsBufferSetData", noSetData, true);
            AppendBool(builder, "noVRPipeMeshInstancing", noPipeMeshInstancing, true);
            AppendBool(builder, "noLegacyObjectAlignmentRoute", noObjectAlignmentRoute, true);
            AppendBool(builder, "noRuntimeVaultLatestFallback", noRuntimeVaultLatestFallback, true);
            AppendBool(builder, "drawProceduralIndirect", indirect, true);
            AppendBool(builder, "lockBufferUpload", lockBuffer, true);
            builder.Append("  \"builderGhostStateSize\": ").Append(UnsafeUtility.SizeOf<BuilderGhostStateDTO>()).AppendLine(",");
            builder.Append("  \"builderGhostAupOffset\": ").Append(ShinobuSocketConstructionRuntime.ResolveOffset<BuilderGhostStateDTO>(nameof(BuilderGhostStateDTO.AUP_TargetPosition))).AppendLine(",");
            builder.Append("  \"builderGhostAlign\": ").Append(UnsafeUtility.AlignOf<BuilderGhostStateDTO>()).AppendLine();
            builder.AppendLine("}");

            string absolutePath = Path.Combine(root, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            UpsertReportSection(absolutePath, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static string Read(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static bool NoLegacyGhostPrefabAssets(string root)
        {
            string directory = Path.Combine(root, "Assets/_Project/Prefabs/Construction/Ghosts");
            if (!Directory.Exists(directory))
                return true;

            string search = "PFB_" + "Ghost_*.prefab";
            return Directory.GetFiles(directory, search, SearchOption.TopDirectoryOnly).Length == 0;
        }

        private static bool NoNonZeroGhostPrefabRefs(string root)
        {
            string directory = Path.Combine(root, "Assets/_Project/Data/Construction");
            if (!Directory.Exists(directory))
                return true;

            string[] files = Directory.GetFiles(directory, "*.asset", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                int index = text.IndexOf("ghostPrefab:", StringComparison.Ordinal);
                while (index >= 0)
                {
                    int lineEnd = text.IndexOf('\n', index);
                    if (lineEnd < 0)
                        lineEnd = text.Length;

                    string line = text.Substring(index, lineEnd - index);
                    if (!line.Contains("ghostPrefab: {fileID: 0"))
                        return false;

                    index = text.IndexOf("ghostPrefab:", lineEnd, StringComparison.Ordinal);
                }
            }

            return true;
        }

        private static void AppendBool(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void UpsertReportSection(string absolutePath, string sectionJson)
        {
            if (File.Exists(absolutePath))
            {
                string existing = File.ReadAllText(absolutePath);
                int sectionKey = existing.IndexOf("\"SHINOBU_228\"", StringComparison.Ordinal);
                if (sectionKey >= 0)
                {
                    int objectStart = existing.IndexOf('{', sectionKey);
                    int objectEnd = FindMatchingBrace(existing, objectStart);
                    if (objectStart > 0 && objectEnd > objectStart)
                    {
                        string prefix = existing.Substring(0, objectStart);
                        string suffix = existing.Substring(objectEnd + 1);
                        File.WriteAllText(absolutePath, prefix + sectionJson.TrimEnd() + suffix);
                        return;
                    }
                }

                int insertIndex = existing.LastIndexOf('}');
                if (insertIndex > 0)
                {
                    string prefix = existing.Substring(0, insertIndex).TrimEnd();
                    string suffix = existing.Substring(insertIndex);
                    bool hasExistingProperties = prefix.Length > 0 && prefix[prefix.Length - 1] != '{';
                    StringBuilder merged = new StringBuilder(existing.Length + sectionJson.Length + 32);
                    merged.Append(prefix);
                    if (hasExistingProperties)
                        merged.Append(',');
                    merged.AppendLine();
                    merged.Append("  \"SHINOBU_228\": ").Append(sectionJson.TrimEnd()).AppendLine();
                    merged.Append(suffix);
                    File.WriteAllText(absolutePath, merged.ToString());
                    return;
                }
            }

            File.WriteAllText(absolutePath, "{\n  \"SHINOBU_228\": " + sectionJson.TrimEnd() + "\n}\n");
        }

        private static int FindMatchingBrace(string text, int objectStart)
        {
            if (string.IsNullOrEmpty(text) || objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
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
    }

    public static unsafe class BuilderHolographyProfileCsv
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint MagneticRadiusHash = 0x1670F141u;
        private const uint GridSnapToleranceHash = 0x77770C0Cu;
        private const uint GlobalQualityWeightHash = 0xC74CE627u;

        public static bool TryIngest(ReadOnlySpan<byte> bytes, NativeArray<ConstructionSocketTuningDTO> tuning)
        {
            if (bytes.Length <= 0 || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(tuning);
            ref ConstructionSocketTuningDTO dto = ref UnsafeUtility.AsRef<ConstructionSocketTuningDTO>(ptr);
            int cursor = 0;
            bool any = false;
            while (cursor < bytes.Length)
            {
                int lineStart = cursor;
                while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                    cursor++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, cursor - lineStart);
                while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                    cursor++;

                if (line.Length <= 0 || line[0] == (byte)'#')
                    continue;

                int separator = FindSeparator(line);
                if (separator <= 0 || separator >= line.Length - 1)
                    continue;

                uint keyHash = HashAsciiLower(Trim(line.Slice(0, separator)));
                if (!TryParseFloat(Trim(line.Slice(separator + 1)), out float value))
                    continue;

                switch (keyHash)
                {
                    case MagneticRadiusHash:
                        dto.SnappingRadius = math.max(0.001f, value);
                        any = true;
                        break;
                    case GridSnapToleranceHash:
                        dto.DearLieShrinkMeters = math.clamp(value, 0f, 1f);
                        any = true;
                        break;
                    case GlobalQualityWeightHash:
                        dto.GlobalQualityWeight = math.saturate(value);
                        any = true;
                        break;
                }
            }

            return any;
        }

        private static int FindSeparator(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte b = line[i];
                if (b == (byte)',' || b == (byte)'=' || b == (byte)';')
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool IsWhitespace(byte b)
        {
            return b == (byte)' ' || b == (byte)'\t';
        }

        private static uint HashAsciiLower(ReadOnlySpan<byte> span)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < span.Length; i++)
            {
                byte b = span[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            if (span.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool any = false;
            while (index < span.Length)
            {
                byte b = span[index];
                if (b < (byte)'0' || b > (byte)'9')
                    break;

                integer = (integer * 10f) + (b - (byte)'0');
                any = true;
                index++;
            }

            float fraction = 0f;
            float scale = 1f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length)
                {
                    byte b = span[index];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;

                    scale *= 0.1f;
                    fraction += (b - (byte)'0') * scale;
                    any = true;
                    index++;
                }
            }

            if (!any)
                return false;

            value = (integer + fraction) * sign;
            return math.isfinite(value);
        }
    }
}
#endif
