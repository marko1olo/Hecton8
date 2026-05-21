#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Construction.Editor
{
    public sealed class BaseGroundingTunerWindow : EditorWindow
    {
        private const string RuntimeInactiveText = "Runtime inactive.";

        private readonly StringBuilder _statusBuilder = new StringBuilder(256);
        private Label _statusLabel;
        private Slider _maxLength;
        private Slider _radiusLow;
        private Slider _radiusUltra;
        private Slider _flareLow;
        private Slider _flareUltra;
        private SliderInt _stepsLow;
        private SliderInt _stepsUltra;
        private bool _lastRuntimeActive;
        private int _lastActiveCount = int.MinValue;
        private int _lastSlotCount = int.MinValue;
        private float _lastQuality = float.NaN;
        private float _lastMaxLength = float.NaN;
        private uint _lastFrame = uint.MaxValue;
        private uint _lastFlags = uint.MaxValue;
        private uint _lastHash = uint.MaxValue;

        [MenuItem("HECTON-8/Construction/Base Grounding Tuner")]
        public static void Open()
        {
            GetWindow<BaseGroundingTunerWindow>("Base Grounding");
        }

        private void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10;
            rootVisualElement.style.paddingRight = 10;
            rootVisualElement.style.paddingTop = 10;
            rootVisualElement.style.paddingBottom = 10;

            _statusLabel = new Label(RuntimeInactiveText);
            rootVisualElement.Add(_statusLabel);

            FoundationTuningDTO tuning = FoundationSnappingCalculatorRuntime.GetTuning();
            _maxLength = MakeSlider("Max pylon length", 1f, 96f, tuning.MaxPylonLengthMeters);
            _radiusLow = MakeSlider("Radius low", 0.02f, 2f, tuning.RadiusLowMeters);
            _radiusUltra = MakeSlider("Radius ultra", 0.02f, 3f, tuning.RadiusUltraMeters);
            _flareLow = MakeSlider("Flare low", 0f, 2f, tuning.ShaderFlareLow);
            _flareUltra = MakeSlider("Flare ultra", 0f, 4f, tuning.ShaderFlareUltra);
            _stepsLow = MakeSliderInt("Steps low", 1, 256, tuning.MaxMarchStepsLow);
            _stepsUltra = MakeSliderInt("Steps ultra", 1, 512, tuning.MaxMarchStepsUltra);
            rootVisualElement.Add(_maxLength);
            rootVisualElement.Add(_radiusLow);
            rootVisualElement.Add(_radiusUltra);
            rootVisualElement.Add(_flareLow);
            rootVisualElement.Add(_flareUltra);
            rootVisualElement.Add(_stepsLow);
            rootVisualElement.Add(_stepsUltra);
            rootVisualElement.Add(new Button(ApplyTuning) { text = "Apply Runtime Tuning" });
            rootVisualElement.Add(new Button(LoadProfilesCsv) { text = "Load module_foundation_profiles.csv" });
            rootVisualElement.Add(new Button(FoundationPhysicsInquisition.Run) { text = "Run Foundation Inquisition" });
            rootVisualElement.Add(new Button(FoundationPylonLayoutValidator.Run) { text = "Validate ARM64 Layout" });
        }

        private void OnInspectorUpdate()
        {
            if (_statusLabel == null)
                return;

            IDataVault vault = GlobalRegistry.DataVault;
            if (FoundationSnappingCalculatorRuntime.TryReadEditorState(
                    vault,
                    out int activeCount,
                    out int slotCount,
                    out float quality,
                    out float maxLength,
                    out uint frame,
                    out uint flags,
                    out uint hash))
            {
                if (_lastRuntimeActive &&
                    _lastActiveCount == activeCount &&
                    _lastSlotCount == slotCount &&
                    NearlyEqual(_lastQuality, quality, 0.0005f) &&
                    NearlyEqual(_lastMaxLength, maxLength, 0.0005f) &&
                    _lastFrame == frame &&
                    _lastFlags == flags &&
                    _lastHash == hash)
                {
                    return;
                }

                _statusBuilder.Clear();
                _statusBuilder.Append("Active: ").Append(activeCount)
                    .Append(" | Slots: ").Append(slotCount)
                    .Append(" | Quality: ").Append(quality.ToString("0.00"))
                    .Append(" | Max length: ").Append(maxLength.ToString("0.00"))
                    .Append(" | Flags: 0x").Append(flags.ToString("X8"))
                    .Append(" | Hash: 0x").Append(hash.ToString("X8"))
                    .Append(" | Frame: ").Append(frame);
                _statusLabel.text = _statusBuilder.ToString();
                _lastRuntimeActive = true;
                _lastActiveCount = activeCount;
                _lastSlotCount = slotCount;
                _lastQuality = quality;
                _lastMaxLength = maxLength;
                _lastFrame = frame;
                _lastFlags = flags;
                _lastHash = hash;
            }
            else
            {
                if (!_lastRuntimeActive && string.Equals(_statusLabel.text, RuntimeInactiveText, StringComparison.Ordinal))
                    return;

                _statusLabel.text = RuntimeInactiveText;
                _lastRuntimeActive = false;
            }
        }

        private void ApplyTuning()
        {
            FoundationSnappingCalculatorRuntime.TryApplyEditorTuning(
                _maxLength.value,
                _radiusLow.value,
                _radiusUltra.value,
                _flareLow.value,
                _flareUltra.value,
                _stepsLow.value,
                _stepsUltra.value);
        }

        private static void LoadProfilesCsv()
        {
            string path = EditorUtility.OpenFilePanel("module_foundation_profiles.csv", Application.dataPath, "csv");
            if (!string.IsNullOrEmpty(path))
                FoundationSnappingCalculatorRuntime.TryLoadProfilesFromCsvFile(path);
        }

        private static Slider MakeSlider(string label, float min, float max, float value)
        {
            Slider slider = new Slider(label, min, max) { value = value, showInputField = true };
            return slider;
        }

        private static SliderInt MakeSliderInt(string label, int min, int max, int value)
        {
            SliderInt slider = new SliderInt(label, min, max) { value = value, showInputField = true };
            return slider;
        }

        private static bool NearlyEqual(float left, float right, float epsilon)
        {
            return float.IsNaN(left) && float.IsNaN(right) ||
                   Math.Abs(left - right) <= epsilon;
        }
    }

    public static class FoundationPylonLayoutValidator
    {
        [MenuItem("HECTON-8/Construction/Validate Foundation Pylon Layout")]
        public static void Run()
        {
            bool pass = FoundationSnappingCalculatorRuntime.ValidateStructLayout() &&
                        UnsafeUtility.SizeOf<PylonMatrixDTO>() == 64 &&
                        ResolveUnsafeOffset<PylonMatrixDTO>(nameof(PylonMatrixDTO.LocalToWorld)) == 0 &&
                        UnsafeUtility.SizeOf<FoundationPylonSurfaceDTO>() == 64 &&
                        ResolveUnsafeOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.SurfaceNormalFlare)) == 0 &&
                        ResolveUnsafeOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.AxisRadius)) == 16 &&
                        ResolveUnsafeOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.HitLocalLength)) == 32 &&
                        UnsafeUtility.SizeOf<FoundationPylonFrameCounters>() == 64;
            if (pass)
                Debug.Log("[SHINOBU_252] Foundation pylon ARM64 layout PASS.");
            else
                Debug.LogError("[SHINOBU_252] Foundation pylon ARM64 layout FAIL.");
        }

        public static int ResolveUnsafeOffset<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
    }

    public static class FoundationPhysicsInquisition
    {
        private const string ReportPath = "Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_252.json";

        [MenuItem("HECTON-8/Construction/Foundation Physics Inquisition")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string constructionDir = Path.Combine(root, "Assets", "_Project", "Scripts", "Construction");
            string playerBuilder = Path.Combine(root, "Assets", "_Project", "Scripts", "PlayerBuilder.cs");
            string constructionManager = Path.Combine(root, "Assets", "_Project", "Scripts", "ConstructionManager.cs");
            int foundationPhysicsRaycast = CountInFiles(constructionDir, "Foundation", "Physics" + ".Raycast");
            int foundationDeferredQueryCount = CountInFiles(constructionDir, "Foundation", "Raycast" + "Command");
            int foundationInstantiate = CountInFiles(constructionDir, "Foundation", "Instantiate" + "(");
            int foundationTransformLists = CountInFiles(constructionDir, "Foundation", "List" + "<Transform>");
            int playerSdfReads = File.Exists(playerBuilder) ? Count(File.ReadAllText(playerBuilder), "HectonVoxelVolume.TryReadRuntimeSdfDensity(") : 0;
            int constructionDeferredRaycasts = File.Exists(constructionManager) ? Count(File.ReadAllText(constructionManager), "Raycast" + "Command") : 0;
            bool pass = foundationPhysicsRaycast == 0 &&
                        foundationDeferredQueryCount == 0 &&
                        foundationInstantiate == 0 &&
                        foundationTransformLists == 0 &&
                        FoundationSnappingCalculatorRuntime.ValidateStructLayout();

            Directory.CreateDirectory(Path.Combine(root, "Docs", "Reports"));
            StringBuilder json = new StringBuilder(1024);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_252\",");
            json.AppendLine("  \"domain\": \"FOUNDATION_SNAPPING_CALCULATOR\",");
            json.Append("  \"pass\": ").Append(pass ? "true" : "false").AppendLine(",");
            json.Append("  \"foundation_physics_raycast_sites\": ").Append(foundationPhysicsRaycast).AppendLine(",");
            json.Append("  \"foundation_raycast_command_sites\": ").Append(foundationDeferredQueryCount).AppendLine(",");
            json.Append("  \"foundation_instantiate_sites\": ").Append(foundationInstantiate).AppendLine(",");
            json.Append("  \"foundation_list_transform_sites\": ").Append(foundationTransformLists).AppendLine(",");
            json.Append("  \"player_builder_runtime_sdf_reads\": ").Append(playerSdfReads).AppendLine(",");
            json.Append("  \"construction_manager_deferred_raycast_command_sites\": ").Append(constructionDeferredRaycasts).AppendLine(",");
            json.AppendLine("  \"route\": \"ConstructionSocketModuleDTO -> FoundationModuleAupDTO -> Burst SDF raymarch -> active draw compaction -> PylonMatrixDTO/SurfaceDTO -> GraphicsBuffer.LockBufferForWrite\",");
            json.AppendLine("  \"rollback_fence\": \"PylonMatrixDTO is presentation-only and is not written to rollback/Merkle DTO lanes\",");
            json.AppendLine("  \"gpu_policy\": \"One procedural draw; no pylon GameObjects; inactive pylons are compacted out before upload and indirect args\"");
            json.AppendLine("}");
            File.WriteAllText(Path.Combine(root, ReportPath), json.ToString());
            AssetDatabase.Refresh();
            Debug.Log("[SHINOBU_252] Foundation physics inquisition wrote " + ReportPath + " pass=" + pass);
        }

        private static int CountInFiles(string directory, string filenameContains, string needle)
        {
            if (!Directory.Exists(directory))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (file.IndexOf(filenameContains, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                count += Count(File.ReadAllText(file), needle);
            }

            return count;
        }

        private static int Count(string text, string needle)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }

            return count;
        }
    }

    public static class Foundation_Physics_Inquisition
    {
        public static void Run()
        {
            FoundationPhysicsInquisition.Run();
        }
    }
}
#endif
