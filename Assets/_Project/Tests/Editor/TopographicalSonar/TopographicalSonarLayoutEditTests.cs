using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.UI;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class TopographicalSonarLayoutEditTests
    {
        [Test]
        public void SonarPointDtoLayoutIsGpuContractExact()
        {
            Assert.AreEqual(16, UnsafeUtility.SizeOf<SonarPointDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<SonarPointDTO>(nameof(SonarPointDTO.LocalPosition)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<SonarPointDTO>(nameof(SonarPointDTO.ColorPacked)));
        }

        [Test]
        public void SonarTelemetryAndArgsLayoutsStayArm64Aligned()
        {
            Assert.AreEqual(128, UnsafeUtility.SizeOf<TopographicalSonarTelemetryEntry>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<TopographicalSonarTelemetryEntry>(nameof(TopographicalSonarTelemetryEntry.TimeSeconds)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<TopographicalSonarTelemetryEntry>(nameof(TopographicalSonarTelemetryEntry.PingAupX)));
            Assert.AreEqual(56, (int)Marshal.OffsetOf<TopographicalSonarTelemetryEntry>(nameof(TopographicalSonarTelemetryEntry.Frame)));
            Assert.AreEqual(124, (int)Marshal.OffsetOf<TopographicalSonarTelemetryEntry>(nameof(TopographicalSonarTelemetryEntry.ComputeTimeMicroseconds)));

            Assert.AreEqual(16, UnsafeUtility.SizeOf<SonarProceduralArgsDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<SonarProceduralArgsDTO>(nameof(SonarProceduralArgsDTO.VertexCountPerInstance)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<SonarProceduralArgsDTO>(nameof(SonarProceduralArgsDTO.InstanceCount)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<SonarProceduralArgsDTO>(nameof(SonarProceduralArgsDTO.StartInstance)));

            Assert.AreEqual(64, UnsafeUtility.SizeOf<TopographicalSonarShaderGlobalsDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<TopographicalSonarShaderGlobalsDTO>(nameof(TopographicalSonarShaderGlobalsDTO.CameraRuntimeAndPointSize)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<TopographicalSonarShaderGlobalsDTO>(nameof(TopographicalSonarShaderGlobalsDTO.RenderParams1)));
            Assert.IsTrue(TopographicalSonarSynthesizer.TryRunStaticSelfAudit(out uint failureMask), "failureMask=" + failureMask);
        }

        [Test]
        public void CsvParserAcceptsNamesAndHexWithoutManagedStringsInRuntimePath()
        {
            NativeArray<byte> bytes = new NativeArray<byte>(64, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<uint> lut = new NativeArray<uint>(256, Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                byte[] source = System.Text.Encoding.ASCII.GetBytes("basalt,#11223344\n2,255,190,68,230\n");
                for (int i = 0; i < source.Length; i++)
                    bytes[i] = source[i];

                int rows = TopographicalSonarSynthesizer.ParseMaterialColorCsv(bytes, source.Length, lut);
                Assert.AreEqual(2, rows);
                Assert.AreEqual(0xE644BEFFu, lut[2]);
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
                if (lut.IsCreated)
                    lut.Dispose();
            }
        }

        [Test]
        public void RuntimeSourceContainsNoPhysxOrGameObjectPointCloudRoute()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/UI/TopographicalSonar/TopographicalSonarSynthesizer.cs");
            AssertFileDoesNotContain(path, "Physics.Raycast");
            AssertFileDoesNotContain(path, "Physics.SphereCast");
            AssertFileDoesNotContain(path, "Collider.Raycast");
            AssertFileDoesNotContain(path, "Instantiate(");
            AssertFileDoesNotContain(path, "File.ReadAllBytes");
            AssertFileDoesNotContain(path, "Shader.Find");
            AssertFileDoesNotContain(path, "new Material");
            AssertFileDoesNotContain(path, "new byte[byteCount]");
            AssertFileDoesNotContain(path, "File.WriteAllBytes");
            AssertFileDoesNotContain(path, "Schedule(_activePointCount, 128).Complete");
            AssertFileContains(path, "Graphics.DrawProceduralIndirect");
            AssertFileContains(path, "NativeArrayOptions.UninitializedMemory");
            AssertFileContains(path, "SonarCompactHitsJob");
            AssertFileContains(path, "Points[writeIndex] = Points[i]");
            AssertFileDoesNotContain(path, "Counters[0] = safeRayCount");
            AssertFileDoesNotContain(path, "Points[index] = default");
            AssertFileContains(path, "i < _activePointCount && i < points.Length");
            AssertFileContains(path, "_pointBufferA");
            AssertFileContains(path, "_pointBufferB");
            AssertFileContains(path, "ResolveMinimumPingIntervalSeconds");
            AssertFileContains(path, "ResolveWorkCurve");
            AssertFileContains(path, "ExecuteSingleLookup(index, direction, step)");
            AssertFileContains(path, "ResolveSingleLookupDistance01");
            AssertFileDoesNotContain(path, "public double3 PingAup");
            AssertFileDoesNotContain(path, "public double3 CameraAup");
        }

        [Test]
        public void RollbackMerkleSourceDoesNotHashTopographicalSonarPresentationBuffers()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Networking/RollbackNetcodeContracts.cs");
            AssertFileDoesNotContain(path, "TopographicalSonar");
            AssertFileDoesNotContain(path, "SonarPointDTO");
        }

        [Test]
        public void ComputeShaderKeepsSamePingLocalDtoContractAsBurstPath()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarRaymarch.compute");
            AssertFileContains(path, "ResolveWorkCurve");
            AssertFileContains(path, "SingleLookupDistance01");
            AssertFileContains(path, "if (maxSteps <= 1u)");
            AssertFileContains(path, "Load(int4");
            AssertFileContains(path, "_IndirectArgs.InterlockedAdd(4, 1u, writeIndex)");
            AssertFileContains(path, "_SonarPointBuffer[writeIndex].LocalPosition = direction * resolvedDistance");
            AssertFileDoesNotContain(path, "_PingCameraLocal + direction * resolvedDistance");
            AssertFileDoesNotContain(path, "_IndirectArgs.Store(16");
            AssertFileDoesNotContain(path, "_IndirectArgs.Store(4, (uint)_RayCount)");
            AssertFileDoesNotContain(path, "_SonarPointBuffer[rayIndex].ColorPacked = 0u");
        }

        [Test]
        public void UnityMetasExistForTopographicalSonarAssets()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarPoint.shader.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarRaymarch.compute.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Data/UI/sonar_material_colors.csv.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Scripts/Editor/TopographicalSonarTunerWindow.cs.meta")));
        }

        private static void AssertFileContains(string path, string token)
        {
            Assert.IsTrue(FileContains(path, token), path + " missing token: " + token);
        }

        private static void AssertFileDoesNotContain(string path, string token)
        {
            Assert.IsFalse(FileContains(path, token), path + " contains forbidden token: " + token);
        }

        private static bool FileContains(string path, string token)
        {
            foreach (string line in File.ReadLines(path))
            {
                if (line.IndexOf(token, StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
        }
    }
}
