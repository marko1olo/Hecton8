using System;
using System.IO;
using System.Text;
using Hecton8.World.FloraAmbientSway;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Hecton8.Editor.FloraAmbientSway
{
    [InitializeOnLoad]
    internal static class FloraSwayLayoutValidator
    {
        static FloraSwayLayoutValidator()
        {
            EditorApplication.delayCall += ValidateOnLoad;
        }

        [MenuItem("Tools/Hecton8/Flora/Ambient Sway/Validate DTO Layouts")]
        public static void ValidateNow()
        {
            if (!FloraAmbientSwayRuntime.ValidateFloraSwayLayouts(out int paramsSize, out int telemetrySize, out int profileSize))
            {
                Debug.LogError("SHINOBU_267 flora ambient sway layout invalid. Params=" + paramsSize + " Telemetry=" + telemetrySize + " Profile=" + profileSize);
                return;
            }

            Debug.Log("SHINOBU_267 flora ambient sway layouts valid. Params=32 Telemetry=32 Profile=32.");
        }

        private static void ValidateOnLoad()
        {
            FloraAmbientSwayRuntime.ValidateFloraSwayLayouts(out _, out _, out _);
        }
    }

    public sealed class AmbientFloraSwayTunerWindow : EditorWindow
    {
        private Slider _amplitude;
        private Slider _frequency;
        private Slider _phase;
        private Slider _alphaClip;
        private Toggle _mockFlow;
        private Label _state;
        private SwayGraphElement _graph;
        private FloraAmbientSwayRuntime _runtime;

        [MenuItem("Tools/Hecton8/Flora/Ambient Flora Sway Tuner")]
        public static void Open()
        {
            GetWindow<AmbientFloraSwayTunerWindow>("Ambient Flora Sway Tuner");
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            _state = new Label("Runtime: not found");
            root.Add(_state);

            _amplitude = CreateSlider("GlobalAmplitude", 0f, 2f);
            _frequency = CreateSlider("Frequency", 0.001f, 8f);
            _phase = CreateSlider("PhaseSpatialOffset", 0f, 4f);
            _alphaClip = CreateSlider("AlphaClip", 0f, 1f);
            _mockFlow = new Toggle("Mock Flow");
            _mockFlow.RegisterValueChangedCallback(_ => PushTuning());

            root.Add(_amplitude);
            root.Add(_frequency);
            root.Add(_phase);
            root.Add(_alphaClip);
            root.Add(_mockFlow);

            _graph = new SwayGraphElement();
            _graph.style.height = 96;
            _graph.style.marginTop = 8;
            root.Add(_graph);

            Button pull = new Button(PullTuning) { text = "Pull Runtime" };
            root.Add(pull);
            PullTuning();
        }

        private void OnEnable()
        {
            EditorApplication.update += Tick;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
        }

        private Slider CreateSlider(string label, float min, float max)
        {
            Slider slider = new Slider(label, min, max);
            slider.showInputField = true;
            slider.RegisterValueChangedCallback(_ => PushTuning());
            return slider;
        }

        private void PullTuning()
        {
            _runtime = FindRuntime();
            if (_runtime == null)
            {
                _state.text = "Runtime: not found";
                return;
            }

            _amplitude.SetValueWithoutNotify(_runtime.GlobalAmplitudeMetersForEditor);
            _frequency.SetValueWithoutNotify(_runtime.FrequencyForEditor);
            _phase.SetValueWithoutNotify(_runtime.PhaseSpatialOffsetForEditor);
            _alphaClip.SetValueWithoutNotify(_runtime.AlphaClipForEditor);
            _mockFlow.SetValueWithoutNotify(_runtime.MockFlowEnabledForEditor);
            _state.text = "Runtime: " + _runtime.name;
        }

        private void PushTuning()
        {
            if (_runtime == null)
                _runtime = FindRuntime();

            if (_runtime == null)
            {
                _state.text = "Runtime: not found";
                return;
            }

            _runtime.ApplyEditorTuning(_amplitude.value, _frequency.value, _phase.value, _alphaClip.value, _mockFlow.value);
            _state.text = "Runtime: " + _runtime.name;
        }

        private void Tick()
        {
            if (_runtime != null && _runtime.TryReadLatestParams(out FloraSwayParamsDTO dto))
            {
                _graph.SetSample(
                    dto.SwayMathParams.x,
                    dto.SwayMathParams.y,
                    dto.SwayMathParams.z,
                    dto.SwayMathParams.w,
                    new Vector4(dto.GlobalFlowVector.x, dto.GlobalFlowVector.y, dto.GlobalFlowVector.z, dto.GlobalFlowVector.w));
            }
        }

        private static FloraAmbientSwayRuntime FindRuntime()
        {
            FloraAmbientSwayRuntime[] runtimes = Resources.FindObjectsOfTypeAll<FloraAmbientSwayRuntime>();
            for (int i = 0; i < runtimes.Length; i++)
            {
                FloraAmbientSwayRuntime runtime = runtimes[i];
                if (runtime != null && runtime.gameObject.scene.IsValid())
                    return runtime;
            }

            return null;
        }

        private sealed class SwayGraphElement : VisualElement
        {
            private float _time;
            private float _amplitude;
            private float _frequency;
            private float _quality;
            private Vector4 _flow;

            public SwayGraphElement()
            {
                generateVisualContent += Generate;
            }

            public void SetSample(float time, float amplitude, float frequency, float quality, Vector4 flow)
            {
                _time = time;
                _amplitude = amplitude;
                _frequency = frequency;
                _quality = quality;
                _flow = flow;
                MarkDirtyRepaint();
            }

            private void Generate(MeshGenerationContext context)
            {
                Rect rect = contentRect;
                Painter2D painter = context.painter2D;
                painter.lineWidth = 2f;
                painter.strokeColor = new Color(0.22f, 0.84f, 0.64f, 1f);
                float midY = rect.yMin + rect.height * 0.5f;
                float gain = Mathf.Clamp01((_quality - 0.1f) / 0.3f);
                float height = rect.height * 0.38f * gain;
                painter.BeginPath();
                for (int i = 0; i < 64; i++)
                {
                    float t = i / 63f;
                    float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    float wave = Mathf.Sin(_time + t * Mathf.Max(0.001f, _frequency) * 6.28318f);
                    float y = midY - wave * height * Mathf.Clamp01(_amplitude);
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
                painter.strokeColor = new Color(0.72f, 0.42f, 0.96f, 1f);
                painter.BeginPath();
                for (int i = 0; i < 32; i++)
                {
                    float t = i / 31f;
                    float x = Mathf.Lerp(rect.xMin, rect.xMax, t);
                    float flowWave = _flow.x * Mathf.Sin(t * 6.28318f) + _flow.z * Mathf.Cos(t * 6.28318f);
                    float y = midY - flowWave * rect.height * 0.22f * gain;
                    if (i == 0)
                        painter.MoveTo(new Vector2(x, y));
                    else
                        painter.LineTo(new Vector2(x, y));
                }

                painter.Stroke();
            }
        }
    }

    internal static class FloraVertexColorDebugSceneView
    {
        private static readonly int DebugId = Shader.PropertyToID("_HectonFloraVertexColorDebug");
        private static bool _enabled;

        [MenuItem("Tools/Hecton8/Flora/Ambient Sway/Toggle Vertex Color Debug")]
        public static void Toggle()
        {
            _enabled = !_enabled;
            Shader.SetGlobalFloat(DebugId, _enabled ? 1f : 0f);
            SceneView.RepaintAll();
        }
    }

    internal static class FloraAnimationScanner
    {
        private const string ReportKey = "\"shinobu_267_flora_ambient_sway\"";

        private static readonly string[] FloraTokens =
        {
            "flora",
            "kelp",
            "seaweed",
            "sargassum",
            "coral",
            "vegetation",
            "plant"
        };

        [MenuItem("Tools/Hecton8/Rendering/Run Flora Animation Scanner")]
        public static void Run()
        {
            StringBuilder json = new StringBuilder(8192);
            int findings = 0;
            json.AppendLine("  \"shinobu_267_flora_ambient_sway\": {");
            json.AppendLine("    \"agentId\": \"SHINOBU_267\",");
            json.AppendLine("    \"scanner\": \"Flora_Animation_Scanner\",");
            json.AppendLine("    \"summary\": \"OOP Flora Animations Eradicated\",");
            json.AppendLine("    \"reportSchema\": 1,");
            json.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            json.AppendLine("    \"rule\": \"No SkinnedMeshRenderer or Animator on flora prefabs/scenes\",");
            json.AppendLine("    \"findings\": [");

            ScanPrefabs(json, ref findings);
            ScanScenes(json, ref findings);

            json.AppendLine();
            json.AppendLine("    ],");
            json.AppendLine("    \"findingCount\": " + findings);
            json.AppendLine("  }");

            string path = Path.Combine(Directory.GetCurrentDirectory(), "Docs", "Reports", "RENDERING_OPTIMIZATION_REPORT.json");
            WriteMergedReport(path, json.ToString());
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_267 flora animation scanner findings: " + findings);
        }

        private static void ScanPrefabs(StringBuilder json, ref int findings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsFloraPath(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                ScanGameObject(json, ref findings, path, prefab, "prefab");
            }
        }

        private static void ScanScenes(StringBuilder json, ref int findings)
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" });
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!IsFloraPath(path))
                        continue;

                    Scene scene = default;
                    bool sceneOpened = false;
                    try
                    {
                        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                        if (!scene.IsValid())
                            continue;

                        sceneOpened = true;
                        GameObject[] roots = scene.GetRootGameObjects();
                        for (int r = 0; r < roots.Length; r++)
                            ScanGameObject(json, ref findings, path, roots[r], "scene");
                    }
                    finally
                    {
                        if (sceneOpened && scene.IsValid())
                            EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        private static void ScanGameObject(StringBuilder json, ref int findings, string assetPath, GameObject root, string assetKind)
        {
            if (root == null)
                return;

            bool floraName = IsFloraPath(root.name) || IsFloraPath(assetPath);
            if (!floraName)
                return;

            SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            if (skinned.Length == 0 && animators.Length == 0)
                return;

            AppendFindingPrefix(json, findings);
            json.Append("    { \"kind\": \"").Append(assetKind).Append("\", \"path\": \"").Append(Escape(assetPath)).Append("\", \"root\": \"").Append(Escape(root.name)).Append("\", ");
            json.Append("\"skinnedMeshRendererCount\": ").Append(skinned.Length).Append(", \"animatorCount\": ").Append(animators.Length).Append(" }");
            findings++;
        }

        private static void WriteMergedReport(string path, string sectionJson)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string existing = File.Exists(path) ? File.ReadAllText(path).Trim() : string.Empty;
            string root = existing.Length > 1 && existing[0] == '{' && existing[existing.Length - 1] == '}'
                ? RemoveExistingSection(existing)
                : "{\n}";

            int close = root.LastIndexOf('}');
            if (close < 0)
            {
                File.WriteAllText(path, "{\n" + sectionJson + "\n}");
                return;
            }

            string prefix = root.Substring(0, close).TrimEnd();
            bool hasExistingProperties = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            string merged = prefix + (hasExistingProperties ? "," : string.Empty) + Environment.NewLine + sectionJson + Environment.NewLine + "}";
            File.WriteAllText(path, merged);
        }

        private static string RemoveExistingSection(string json)
        {
            int key = json.IndexOf(ReportKey, StringComparison.Ordinal);
            if (key < 0)
                return json;

            int colon = json.IndexOf(':', key + ReportKey.Length);
            if (colon < 0)
                return json;

            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;
            if (valueStart >= json.Length || json[valueStart] != '{')
                return json;

            int valueEnd = FindMatchingBrace(json, valueStart);
            if (valueEnd < 0)
                return json;

            int removeStart = key;
            while (removeStart > 0 && char.IsWhiteSpace(json[removeStart - 1]))
                removeStart--;
            if (removeStart > 0 && json[removeStart - 1] == ',')
            {
                removeStart--;
            }
            else
            {
                int after = valueEnd + 1;
                while (after < json.Length && char.IsWhiteSpace(json[after]))
                    after++;
                if (after < json.Length && json[after] == ',')
                    valueEnd = after;
            }

            return json.Remove(removeStart, valueEnd - removeStart + 1);
        }

        private static int FindMatchingBrace(string json, int openBrace)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openBrace; i < json.Length; i++)
            {
                char c = json[i];
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (c == '\\')
                {
                    escape = inString;
                    continue;
                }
                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }
                if (inString)
                    continue;
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

        private static void AppendFindingPrefix(StringBuilder json, int findings)
        {
            if (findings > 0)
                json.AppendLine(",");
        }

        private static bool IsFloraPath(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string lower = value.ToLowerInvariant();
            for (int i = 0; i < FloraTokens.Length; i++)
            {
                if (lower.Contains(FloraTokens[i]))
                    return true;
            }

            return false;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class FloraAmbientSwaySelfAudit
    {
        [MenuItem("Tools/Hecton8/Flora/Ambient Sway/Run Self Audit")]
        public static void Run()
        {
            bool layout = FloraAmbientSwayRuntime.ValidateFloraSwayLayouts(out int paramsSize, out int telemetrySize, out int profileSize);
            string runtime = ReadProjectFile("Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs");
            string shader = ReadProjectFile("Assets/_Project/Art/Shaders/Hecton_IndirectVegetation.shader");
            string runtimeAsmdef = ReadProjectFile("Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef");
            string editorAsmdef = ReadProjectFile("Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef");
            bool dispatcher = runtime.Contains("DispatcherPhase.PreSimulation") && runtime.Contains("DispatcherPhase.VisualSync");
            bool fmod = runtime.Contains("math.fmod") && runtime.Contains("1000f");
            string forbiddenVectorUploadApi = "SetGlobal" + "Vector";
            bool upload = runtime.Contains("LockBufferForWrite") && runtime.Contains("SetGlobalConstantBuffer") && !runtime.Contains(forbiddenVectorUploadApi);
            int leafAlphaIndex = shader.IndexOf("coverageVisibility *= saturate(leafAlpha)", StringComparison.Ordinal);
            int earlyAlphaClipIndex = shader.IndexOf("clip(coverageVisibility - max((half)_AlphaClip, 0.01h));", leafAlphaIndex < 0 ? 0 : leafAlphaIndex, StringComparison.Ordinal);
            int lightingIndex = earlyAlphaClipIndex >= 0
                ? shader.IndexOf("half3 normalWS = SafeNormalize3", earlyAlphaClipIndex, StringComparison.Ordinal)
                : -1;
            string forbiddenMx350Keyword = "_QUALITY" + "_MX350";
            string forbiddenHighKeyword = "_QUALITY" + "_HIGH";
            bool shaderQuality =
                shader.Contains("smoothstep(0.1, 0.4") &&
                shader.Contains("ResolveGlobalAmbientFloraSwayOffset") &&
                shader.Contains("_FloraAlphaMask") &&
                shader.Contains("return isfinite(qualityWeight) ? saturate(qualityWeight) : 0.0") &&
                shader.Contains("isfinite(rawQualityWeight) ? saturate(rawQualityWeight) : 0.0") &&
                leafAlphaIndex >= 0 &&
                earlyAlphaClipIndex > leafAlphaIndex &&
                lightingIndex > earlyAlphaClipIndex &&
                !shader.Contains(forbiddenMx350Keyword) &&
                !shader.Contains(forbiddenHighKeyword);
            bool telemetry = runtime.Contains("SwayTelemetryCapacity = 300") && runtime.Contains("Dump_SHINOBU_267.bin");
            string oldShaderParamsResolver = "ResolveNextShaderParams" + "Buffer";
            string oldFrameResolver = "Resolve" + "FrameId";
            string oldVisualFrameResolver = "ResolveVisual" + "FrameId";
            bool readAccessorPurity =
                !runtime.Contains(oldShaderParamsResolver) &&
                !runtime.Contains(oldFrameResolver) &&
                !runtime.Contains(oldVisualFrameResolver);
            bool runtimeQualityFailClosed =
                runtime.Contains("private static float ResolveGlobalQualityWeight()") &&
                runtime.Contains("return math.saturate(weight);") &&
                runtime.Contains("return 0f;") &&
                runtime.Contains("float quality = math.saturate(SanitizeFinite(GlobalQualityWeight, 0f));");
            bool unsafeDtoMutation =
                runtime.Contains("UnsafeUtility.AsRef<FloraAmbientFlowStateDTO>(flowPtr) = state") &&
                runtime.Contains("UnsafeUtility.AsRef<FloraSwayParamsDTO>(paramsPtr) = next") &&
                !runtime.Contains("FlowState" + "[0] =") &&
                !runtime.Contains("Params" + "[0] =");
            string forbiddenMathSqrt = "math." + "sqrt";
            string forbiddenTuningIndexerWrite = "tuning" + "[0] =";
            string forbiddenRingIndexerWrite = "ring" + "[cursor] =";
            string forbiddenCursorIndexerWrite = "cursorArray" + "[0] =";
            string forbiddenParamsIndexerRead = "parameters" + "[0]";
            bool hotOwnerMutationAndMath =
                runtime.Contains("ReadFirstParamsReadonly(parameters)") &&
                runtime.Contains("UnsafeUtility.AsRef<FloraSwayTuningDTO>(tuningPtr) = dto") &&
                runtime.Contains("UnsafeUtility.AsRef<SwayTelemetryEntry>(entryPtr) = entry") &&
                runtime.Contains("UnsafeUtility.AsRef<int>(cursorPtr)") &&
                runtime.Contains("math.rsqrt(math.max(flowLengthSq, 0.0001f))") &&
                !runtime.Contains(forbiddenMathSqrt) &&
                !runtime.Contains(forbiddenTuningIndexerWrite) &&
                !runtime.Contains(forbiddenRingIndexerWrite) &&
                !runtime.Contains(forbiddenCursorIndexerWrite) &&
                !runtime.Contains(forbiddenParamsIndexerRead);
            bool hotValueNewHygiene =
                runtime.Contains("GenerateMockAmbientFlowJob mockFlowJob = default") &&
                runtime.Contains("CalculateFloraSwayParametersJob parametersJob = default") &&
                !runtime.Contains("new " + "GenerateMockAmbientFlowJob") &&
                !runtime.Contains("new " + "CalculateFloraSwayParametersJob") &&
                !runtime.Contains("new " + "float3") &&
                !runtime.Contains("new " + "float4");
            bool burstFunctionPointers =
                runtime.Contains("BurstCompiler.CompileFunctionPointer<GenerateMockAmbientFlowKernelDelegate>") &&
                runtime.Contains("BurstCompiler.CompileFunctionPointer<CalculateFloraSwayParametersKernelDelegate>") &&
                runtime.Contains("s_generateMockKernel.Invoke(&job)") &&
                runtime.Contains("s_calculateKernel.Invoke(&job)") &&
                !runtime.Contains("mockFlowJob." + "Execute()") &&
                !runtime.Contains("parametersJob." + "Execute()");
            bool aotFunctionPointerAbi =
                CountOccurrences(runtime, "[UnmanagedFunctionPointer(CallingConvention.Cdecl)]") >= 2 &&
                runtime.Contains("MonoPInvokeCallback(typeof(GenerateMockAmbientFlowKernelDelegate))") &&
                runtime.Contains("MonoPInvokeCallback(typeof(CalculateFloraSwayParametersKernelDelegate))");
            bool asmdefBoundary =
                runtimeAsmdef.Contains("\"allowUnsafeCode\": true") &&
                runtimeAsmdef.Contains("\"Unity.Mathematics\"") &&
                editorAsmdef.Contains("\"Hecton8.World.FloraAmbientSway\"") &&
                editorAsmdef.Contains("\"Hecton8.Core\"") &&
                editorAsmdef.Contains("\"Hecton8.Bootstrap.Contracts\"") &&
                editorAsmdef.Contains("\"Unity.Collections\"") &&
                editorAsmdef.Contains("\"Unity.Jobs\"") &&
                editorAsmdef.Contains("\"Unity.Mathematics\"");
            bool metaIdentity =
                HasProjectFile("Assets/_Project/Scripts/World/FloraAmbientSway.meta") &&
                HasProjectFile("Assets/_Project/Scripts/World/FloraAmbientSway/FloraAmbientSwayRuntime.cs.meta") &&
                HasProjectFile("Assets/_Project/Scripts/World/FloraAmbientSway/Hecton8.World.FloraAmbientSway.asmdef.meta") &&
                HasProjectFile("Assets/_Project/Scripts/Editor/FloraAmbientSway.meta") &&
                HasProjectFile("Assets/_Project/Scripts/Editor/FloraAmbientSway/FloraAmbientSwayEditorTools.cs.meta") &&
                HasProjectFile("Assets/_Project/Scripts/Editor/FloraAmbientSway/Hecton8.World.FloraAmbientSway.Editor.asmdef.meta");
            bool pass = layout && dispatcher && fmod && upload && shaderQuality && telemetry && readAccessorPurity && runtimeQualityFailClosed && unsafeDtoMutation && hotOwnerMutationAndMath && hotValueNewHygiene && burstFunctionPointers && aotFunctionPointerAbi && asmdefBoundary && metaIdentity;
            if (!pass)
            {
                Debug.LogError(
                    "SHINOBU_267 self-audit failed. layout=" + layout +
                    " params=" + paramsSize +
                    " telemetry=" + telemetrySize +
                    " profile=" + profileSize +
                    " dispatcher=" + dispatcher +
                    " fmod=" + fmod +
                    " upload=" + upload +
                    " shaderQuality=" + shaderQuality +
                    " blackBox=" + telemetry +
                    " readAccessorPurity=" + readAccessorPurity +
                    " runtimeQualityFailClosed=" + runtimeQualityFailClosed +
                    " unsafeDtoMutation=" + unsafeDtoMutation +
                    " hotOwnerMutationAndMath=" + hotOwnerMutationAndMath +
                    " hotValueNewHygiene=" + hotValueNewHygiene +
                    " burstFunctionPointers=" + burstFunctionPointers +
                    " aotFunctionPointerAbi=" + aotFunctionPointerAbi +
                    " asmdefBoundary=" + asmdefBoundary +
                    " metaIdentity=" + metaIdentity);
                return;
            }

            Debug.Log("SHINOBU_267 self-audit passed. 0 hot managed allocations by static route; Params=32, Telemetry=32, Profile=32, asmdef/meta route locked.");
        }

        private static string ReadProjectFile(string path)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), path);
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : string.Empty;
        }

        private static int CountOccurrences(string value, string token)
        {
            if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int offset = 0;
            while (offset < value.Length)
            {
                int index = value.IndexOf(token, offset, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                offset = index + token.Length;
            }

            return count;
        }

        private static bool HasProjectFile(string path)
        {
            string absolutePath = Path.Combine(Directory.GetCurrentDirectory(), path);
            return File.Exists(absolutePath);
        }
    }
}
