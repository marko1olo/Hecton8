#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Hecton8.EditorTools
{
    /// <summary>
    /// Editor-only smoke test for the HECTON-8 tech-art hardening pass.
    /// </summary>
    public static class TechArtPipelineSmokeTester
    {
        private const string MenuPath = "Hecton/Validation/Asset Pipeline/Run Tech-Art Smoke Tester";
        private const string OutputRelativePath = "Library/TechArtPipelineSmokeTester.json";
        private const string BlueNoisePath = "Assets/_Project/Art/TEXTURES/Utility/TX_BlueNoise_256_R8.png";
        private const string BlueNoiseMetaPath = "Assets/_Project/Art/TEXTURES/Utility/TX_BlueNoise_256_R8.png.meta";
        private const string VoxelShaderPath = "Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader";
        private const string VisorShaderPath = "Assets/_Project/Art/Shaders/Hecton_VisorFluidDistortion.shader";
        private const string BridgePath = "Assets/_Project/Scripts/World/HectonVoxelStreamingBridge.cs";
        private const string VramDictatorPath = "Assets/_Project/Scripts/Editor/VRAMDictator.cs";
        private const string CoreLitPath = "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl";
        private const string DryZoneShaderPath = "Assets/_Project/Art/Shaders/Hecton_DryZoneLit.shader";
        private const string HudProjectionShaderPath = "Assets/_Project/Art/Shaders/Hecton_HUD_DiegeticProjectionUnlit.shader";
        private const string DiegeticPanelShaderPath = "Assets/_Project/Art/Shaders/Hecton_DiegeticPanelUnlit.shader";

        [MenuItem(MenuPath, priority = 198)]
        private static void RunFromMenu()
        {
            bool passed = Run(out string json);
            WriteOutput(json);
            Debug.Log(json);
            if (!passed)
                throw new BuildFailedException(json);
        }

        public static void RunBatchMode()
        {
            bool passed = Run(out string json);
            WriteOutput(json);
            Debug.Log(json);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string json)
        {
            int checks = 0;
            int failures = 0;
            StringBuilder failureBuilder = new StringBuilder(1024); // COLD ALLOC: StringBuilder[1024] - editor smoke failure staging - owner: TechArtPipelineSmokeTester

            bool ignDither = CheckIgnDither(ref checks, ref failures, failureBuilder);
            bool voxelShader = CheckVoxelShader(ref checks, ref failures, failureBuilder);
            bool visorShader = CheckVisorShader(ref checks, ref failures, failureBuilder);
            bool bridge = CheckBridge(ref checks, ref failures, failureBuilder);
            bool vramDictator = CheckVramDictator(ref checks, ref failures, failureBuilder);
            bool coreLit = CheckCoreLit(ref checks, ref failures, failureBuilder);
            bool dryZone = CheckDryZone(ref checks, ref failures, failureBuilder);
            bool bios = CheckBiosShaders(ref checks, ref failures, failureBuilder);
            bool forensic = CheckForensicStaticRules(ref checks, ref failures, failureBuilder);
            bool passed = failures == 0;

            StringBuilder jsonBuilder = new StringBuilder(2048); // COLD ALLOC: StringBuilder[2048] - editor smoke JSON report - owner: TechArtPipelineSmokeTester
            jsonBuilder.Append('{')
                .Append("\"tester\":\"TechArtPipelineSmokeTester\",")
                .Append("\"status\":\"").Append(passed ? "PASS" : "FAIL").Append("\",")
                .Append("\"checks\":").Append(checks).Append(',')
                .Append("\"failures\":").Append(failures).Append(',')
                .Append("\"ignDither\":").Append(JsonBool(ignDither)).Append(',')
                .Append("\"voxelShader\":").Append(JsonBool(voxelShader)).Append(',')
                .Append("\"visorShader\":").Append(JsonBool(visorShader)).Append(',')
                .Append("\"streamingBridge\":").Append(JsonBool(bridge)).Append(',')
                .Append("\"vramDictator\":").Append(JsonBool(vramDictator)).Append(',')
                .Append("\"coreLitRustSilt\":").Append(JsonBool(coreLit)).Append(',')
                .Append("\"dryZoneCondensation\":").Append(JsonBool(dryZone)).Append(',')
                .Append("\"biosPhosphor\":").Append(JsonBool(bios)).Append(',')
                .Append("\"forensicRules\":").Append(JsonBool(forensic)).Append(',')
                .Append("\"failureList\":\"").Append(EscapeJson(failureBuilder.ToString())).Append("\"")
                .Append('}');
            json = jsonBuilder.ToString();
            return passed;
        }

        private static bool CheckIgnDither(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            string absolutePath = ResolveProjectPath(BlueNoisePath);
            string absoluteMetaPath = ResolveProjectPath(BlueNoiseMetaPath);
            bool pass = !File.Exists(absolutePath) &&
                        !File.Exists(absoluteMetaPath) &&
                        FileContains(VisorShaderPath, "ResolveInterleavedGradientNoise") &&
                        !FileContains(VisorShaderPath, "ResolveBlueNoise") &&
                        !FileContains(VisorShaderPath, "_HectonVisorFluidBlueNoiseTex");
            return Record(pass, "ignDitherNoBlueNoiseAsset", ref failures, failuresOut);
        }

        private static bool CheckVoxelShader(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(VoxelShaderPath, "_TriplanarBlendSharpness") &&
                        FileContains(VoxelShaderPath, "HectonCoreLitApplyProceduralRustSilt") &&
                        FileContains(VoxelShaderPath, "LODFadeCrossFade") &&
                        FileContains(VoxelShaderPath, "_ChunkDissolveFade") &&
                        FileContains(VoxelShaderPath, "ApplyChunkDissolveMalfunction");
            return Record(pass, "voxelShaderContract", ref failures, failuresOut);
        }

        private static bool CheckVisorShader(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(VisorShaderPath, "ComputeDustMask") &&
                        FileContains(VisorShaderPath, "ResolveInterleavedGradientNoise") &&
                        !FileContains(VisorShaderPath, "SAMPLE_TEXTURE2D_LOD(_HectonVisorFluidBlueNoiseTex");
            return Record(pass, "visorShaderIgnDust", ref failures, failuresOut);
        }

        private static bool CheckBridge(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(BridgePath, "new Material(material)") &&
                        FileContains(BridgePath, "Destroy(state.RuntimeMaterial)") &&
                        FileContains(BridgePath, "PublishPerformanceWarning") &&
                        !FileContains(BridgePath, "MaterialPropertyBlock");
            return Record(pass, "streamingBridgeNoMpbFadeTelemetry", ref failures, failuresOut);
        }

        private static bool CheckVramDictator(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(VramDictatorPath, "BuildFailedException") &&
                        FileContains(VramDictatorPath, "MaxNonAtlasDimension = 1024") &&
                        FileContains(VramDictatorPath, "IsUncompressedRgba") &&
                        FileContains(VramDictatorPath, "nonBC7(non-normal audit)") &&
                        FileContains(VramDictatorPath, "normalNotBC5(audit)");
            return Record(pass, "vramDictatorBlockingGate", ref failures, failuresOut);
        }

        private static bool CheckCoreLit(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(CoreLitPath, "HectonCoreLitValueNoise2") &&
                        FileContains(CoreLitPath, "HectonCoreLitApplyProceduralRustSilt") &&
                        FileContains(CoreLitPath, "normalMicroCavity");
            return Record(pass, "coreLitRustSiltAlu", ref failures, failuresOut);
        }

        private static bool CheckDryZone(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(DryZoneShaderPath, "ApplyInteriorCondensation") &&
                        FileContains(DryZoneShaderPath, "_InteriorCondensationStrength") &&
                        FileContains(DryZoneShaderPath, "HectonCoreLitValueNoise2");
            return Record(pass, "dryZoneCondensationAlu", ref failures, failuresOut);
        }

        private static bool CheckBiosShaders(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool pass = FileContains(HudProjectionShaderPath, "Bayer4x4") &&
                        FileContains(HudProjectionShaderPath, "_PanelPowerLevel < 0.1") &&
                        FileContains(DiegeticPanelShaderPath, "powerLevel < 0.1");
            return Record(pass, "biosPhosphorDither", ref failures, failuresOut);
        }

        private static bool CheckForensicStaticRules(ref int checks, ref int failures, StringBuilder failuresOut)
        {
            checks++;
            bool noBridgeNative = !FileContains(BridgePath, "NativeArray") &&
                                  !FileContains(BridgePath, "NativeList") &&
                                  !FileContains(BridgePath, "NativeQueue") &&
                                  !FileContains(BridgePath, "NativeHash") &&
                                  !FileContains(BridgePath, "NativeParallel");
            bool noJobBarrier = !FileContains(BridgePath, ".Complete()") &&
                                !FileContains(BridgePath, "JobHandle.Run") &&
                                !FileContains(BridgePath, ".Run()");
            bool noStaticInstance = !FileContains(BridgePath, "DontDestroyOnLoad") &&
                                    !FileContains(BridgePath, "_instance");
            bool noHotString = !MethodBlockContains(BridgePath, "public void Tick", "$\"") &&
                               !MethodBlockContains(BridgePath, "public void Tick", ".ToString(") &&
                               !MethodBlockContains(BridgePath, "private void TickChunkFade", "$\"") &&
                               !MethodBlockContains(BridgePath, "private void TickChunkFade", ".ToString(");
            return Record(noBridgeNative && noJobBarrier && noStaticInstance && noHotString, "forensicStaticRules", ref failures, failuresOut);
        }

        private static bool FileContains(string assetPath, string token)
        {
            string path = ResolveProjectPath(assetPath);
            if (!File.Exists(path))
                return false;

            foreach (string line in File.ReadLines(path))
            {
                if (line.Contains(token))
                    return true;
            }

            return false;
        }

        private static bool MethodBlockContains(string assetPath, string methodStart, string token)
        {
            string path = ResolveProjectPath(assetPath);
            if (!File.Exists(path))
                return false;

            string text = File.ReadAllText(path);
            int start = text.IndexOf(methodStart, System.StringComparison.Ordinal);
            if (start < 0)
                return false;

            int end = text.IndexOf("\n        private ", start + methodStart.Length, System.StringComparison.Ordinal);
            if (end < 0)
                end = text.Length;

            return text.IndexOf(token, start, end - start, System.StringComparison.Ordinal) >= 0;
        }

        private static bool Record(bool pass, string label, ref int failures, StringBuilder failuresOut)
        {
            if (pass)
                return true;

            failures++;
            if (failuresOut.Length > 0)
                failuresOut.Append('|');
            failuresOut.Append(label);
            return false;
        }

        private static string ResolveProjectPath(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, assetPath);
        }

        private static void WriteOutput(string json)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, OutputRelativePath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            File.WriteAllText(outputPath, json);
        }

        private static string JsonBool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
#endif
