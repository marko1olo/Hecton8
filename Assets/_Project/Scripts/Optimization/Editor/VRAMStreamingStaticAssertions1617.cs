#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Optimization.Editor
{
    public static class VRAMStreamingStaticAssertions1617
    {
        [MenuItem("HECTON-8/Validation/Agent 1617/Run VRAM Streaming Assertions")]
        public static void RunAssertions()
        {
            AssertMipBiasCurve();
            AssertUploadBudgetCurve();
            AssertGraphicsBufferUploadBudgetCurve();
            AssertCoreUploadUtilityUsesBudgetGate();
            AssertProceduralGpuUploadsUseBudgetGate();
            Debug.Log("[VRAMStreamingStaticAssertions1617] Static VRAM streaming assertions passed.");
        }

        private static void AssertMipBiasCurve()
        {
            string source = ReadProjectSource("Assets/_Project/Scripts/Optimization/VRAMPressureMonitor.cs");
            AssertSourceText(source, "ResolveMipLimitDeltaForAudit");
            AssertSourceText(source, "VRAMEnforcer.RuntimeTextureMipLimitFloor");
            AssertSourceText(source, "Mathf.Max(_baselineMipLimit, VRAMEnforcer.RuntimeTextureMipLimitFloor)");
            AssertSourceText(source, "return ResolveMipLimitDelta(response, redZonePressure)");
        }

        private static void AssertUploadBudgetCurve()
        {
            string source = ReadProjectSource("Assets/_Project/Scripts/Optimization/AssetLoadDispatcher.cs");
            AssertSourceText(source, "ResolveUploadBudgetBytesForAudit");
            AssertSourceText(source, "MinimumFrameUploadBudgetBytes");
            AssertSourceText(source, "UltraFrameUploadBudgetBytes");
            AssertSourceText(source, "math.smoothstep(0.55f, 0.98f, pressure)");
            AssertSourceText(source, "return (long)math.max((float)MinimumFrameUploadBudgetBytes, pressureBudget)");
        }

        private static void AssertGraphicsBufferUploadBudgetCurve()
        {
            string source = ReadProjectSource("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            AssertSourceText(source, "MinimumFrameUploadBudgetBytes = 256L * 1024L");
            AssertSourceText(source, "UltraFrameUploadBudgetBytes = 4L * 1024L * 1024L");
            AssertSourceText(source, "ResolveFrameUploadBudgetBytes");
            AssertSourceText(source, "math.smoothstep(0.55f, 0.98f, pressure)");
            AssertSourceText(source, "return (long)math.round(math.lerp(qualityBudgetBytes, MinimumFrameUploadBudgetBytes, pressureCollapse))");
        }

        private static void AssertProceduralGpuUploadsUseBudgetGate()
        {
            AssertSourceContains(
                "Assets/_Project/Scripts/World/VoxelSurfaceNets/VoxelSurfaceNetsGpuUploadDispatcher.cs",
                "GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/World/ProceduralCoral/ProceduralCoralGpuUploadDispatcher.cs",
                "GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/World/ProceduralWreckage/ProceduralWreckageGpuUploadDispatcher.cs",
                "GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/World/ScatterGPUIBackend.cs",
                "GraphicsBufferUploadUtility.CanUploadBytesThisFrame(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs",
                "GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/World/HectonOctahedralImpostorRenderer.cs",
                "GraphicsBufferUploadUtility.TryUploadSingle(_argsBuffer, args)");
            AssertSourceContains(
                "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
                "GraphicsBufferUploadUtility.TryUploadSingle(buffer, value)");
            AssertSourceContains(
                "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
                "GraphicsBufferUploadUtility.TryClear<T>(buffer, requestedCount)");
            AssertSourceContains(
                "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
                "GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes)");
            AssertSourceContains(
                "Assets/_Project/Scripts/VFX/HectonMarineSnowRenderer.cs",
                "CommitPropwashEventUploadBuffer(uploadBuffer)");
            AssertSourceContains(
                "Assets/_Project/Scripts/Graphics/Materials/VisualPressureAgingRuntime.cs",
                "GraphicsBufferUploadUtility.TryUploadNativeArrayRange(destination, source, 0, 0, safeCount)");
            AssertSourceContains(
                "Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs",
                "GraphicsBufferUploadUtility.TryUploadNativeArrayRange(destination, source, 0, 0, safeCount)");
            AssertSourceContains(
                "Assets/_Project/Scripts/Graphics/Materials/ShinobuMaterialResponseRuntime.cs",
                "GraphicsBufferUploadUtility.TryUploadSingle(destination, constants)");
        }

        private static void AssertCoreUploadUtilityUsesBudgetGate()
        {
            string source = ReadProjectSource("Assets/_Project/Scripts/Core/SystemDispatcher.cs");
            if (CountOccurrences(source, "TryBeginManualUpload(uploadedBytes)") < 8)
                throw new InvalidOperationException("GraphicsBufferUploadUtility direct upload paths must reserve bytes before GPU upload.");
            if (CountOccurrences(source, "CompleteManualUpload(uploadedBytes)") < 8)
                throw new InvalidOperationException("GraphicsBufferUploadUtility direct upload paths must complete byte reservations after upload.");
            if (CountOccurrences(source, "CancelManualUpload(uploadedBytes)") < 4)
                throw new InvalidOperationException("GraphicsBufferUploadUtility lock paths must cancel reservations after rejected copies.");
            if (source.IndexOf("TryUploadSingle", StringComparison.Ordinal) < 0 ||
                source.IndexOf("TryClear", StringComparison.Ordinal) < 0 ||
                source.IndexOf("TryUploadNativeArrayRange", StringComparison.Ordinal) < 0 ||
                source.IndexOf("TryUploadArrayRange", StringComparison.Ordinal) < 0)
            {
                throw new InvalidOperationException("GraphicsBufferUploadUtility must expose budgeted single, clear, and range upload helpers.");
            }
        }

        private static void AssertSourceContains(string projectRelativePath, string requiredText)
        {
            string source = ReadProjectSource(projectRelativePath);
            AssertSourceText(source, requiredText, projectRelativePath);
        }

        private static void AssertSourceText(string source, string requiredText)
        {
            AssertSourceText(source, requiredText, "<source>");
        }

        private static void AssertSourceText(string source, string requiredText, string sourceName)
        {
            if (source.IndexOf(requiredText, StringComparison.Ordinal) < 0)
                throw new InvalidOperationException(sourceName + " missing required text: " + requiredText);
        }

        private static string ReadProjectSource(string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return File.ReadAllText(Path.Combine(projectRoot, projectRelativePath));
        }

        private static int CountOccurrences(string source, string needle)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int next = source.IndexOf(needle, index, StringComparison.Ordinal);
                if (next < 0)
                    return count;

                count++;
                index = next + needle.Length;
            }

            return count;
        }
    }
}
#endif
