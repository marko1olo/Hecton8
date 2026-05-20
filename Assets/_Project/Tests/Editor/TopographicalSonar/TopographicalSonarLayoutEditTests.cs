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
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Not.Contain("Physics.Raycast"));
            Assert.That(source, Does.Not.Contain("Physics.SphereCast"));
            Assert.That(source, Does.Not.Contain("Collider.Raycast"));
            Assert.That(source, Does.Not.Contain("Instantiate("));
            Assert.That(source, Does.Not.Contain("File.ReadAllBytes"));
            Assert.That(source, Does.Not.Contain("Shader.Find"));
            Assert.That(source, Does.Not.Contain("new Material"));
            Assert.That(source, Does.Not.Contain("new byte[byteCount]"));
            Assert.That(source, Does.Not.Contain("File.WriteAllBytes"));
            Assert.That(source, Does.Not.Contain("Schedule(_activePointCount, 128).Complete"));
            Assert.That(source, Does.Contain("Graphics.DrawProceduralIndirect"));
            Assert.That(source, Does.Contain("NativeArrayOptions.UninitializedMemory"));
            Assert.That(source, Does.Contain("SonarCompactHitsJob"));
            Assert.That(source, Does.Contain("Points[writeIndex] = Points[i]"));
            Assert.That(source, Does.Not.Contain("Counters[0] = safeRayCount"));
            Assert.That(source, Does.Not.Contain("Points[index] = default"));
            Assert.That(source, Does.Contain("i < _activePointCount && i < points.Length"));
            Assert.That(source, Does.Contain("_pointBufferA"));
            Assert.That(source, Does.Contain("_pointBufferB"));
            Assert.That(source, Does.Contain("ResolveMinimumPingIntervalSeconds"));
            Assert.That(source, Does.Contain("ResolveWorkCurve"));
            Assert.That(source, Does.Contain("ExecuteSingleLookup(index, direction, step)"));
            Assert.That(source, Does.Contain("ResolveSingleLookupDistance01"));
            Assert.That(source, Does.Not.Contain("public double3 PingAup"));
            Assert.That(source, Does.Not.Contain("public double3 CameraAup"));
        }

        [Test]
        public void RollbackMerkleSourceDoesNotHashTopographicalSonarPresentationBuffers()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Networking/RollbackNetcodeContracts.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Not.Contain("TopographicalSonar"));
            Assert.That(source, Does.Not.Contain("SonarPointDTO"));
        }

        [Test]
        public void ComputeShaderKeepsSamePingLocalDtoContractAsBurstPath()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarRaymarch.compute");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Contain("ResolveWorkCurve"));
            Assert.That(source, Does.Contain("SingleLookupDistance01"));
            Assert.That(source, Does.Contain("if (maxSteps <= 1u)"));
            Assert.That(source, Does.Contain("Load(int4"));
            Assert.That(source, Does.Contain("_IndirectArgs.InterlockedAdd(4, 1u, writeIndex)"));
            Assert.That(source, Does.Contain("_SonarPointBuffer[writeIndex].LocalPosition = direction * resolvedDistance"));
            Assert.That(source, Does.Not.Contain("_PingCameraLocal + direction * resolvedDistance"));
            Assert.That(source, Does.Not.Contain("_IndirectArgs.Store(16"));
            Assert.That(source, Does.Not.Contain("_IndirectArgs.Store(4, (uint)_RayCount)"));
            Assert.That(source, Does.Not.Contain("_SonarPointBuffer[rayIndex].ColorPacked = 0u"));
        }

        [Test]
        public void UnityMetasExistForTopographicalSonarAssets()
        {
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarPoint.shader.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Art/Shaders/Hecton_SonarRaymarch.compute.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Data/UI/sonar_material_colors.csv.meta")));
            Assert.IsTrue(File.Exists(Path.Combine(Application.dataPath, "_Project/Scripts/Editor/TopographicalSonarTunerWindow.cs.meta")));
        }
    }
}
