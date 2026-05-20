using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Lighting;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DynamicPointLightCullingEditTests
    {
        [Test]
        public void LightCullStateLayoutMatchesAssignedArm64Contract()
        {
            Assert.AreEqual(32, UnsafeUtility.SizeOf<LightCullStateDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO.LightHash)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO.DistanceSq)));
            Assert.AreEqual(8, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO.BaseIntensity)));
            Assert.AreEqual(12, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO.ComputedIntensity)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO.Flags)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO._pad0)));
            Assert.AreEqual(31, (int)Marshal.OffsetOf<LightCullStateDTO>(nameof(LightCullStateDTO._pad11)));

            Assert.AreEqual(96, UnsafeUtility.SizeOf<DynamicPointLightSourceDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DynamicPointLightGpuDTO>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DynamicPointLightCullingTelemetryEntry>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<DynamicPointLightSourceManifestDTO>());
            Assert.AreEqual(0, (int)Marshal.OffsetOf<DynamicPointLightSourceManifestDTO>(nameof(DynamicPointLightSourceManifestDTO.ActiveSourceCount)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<DynamicPointLightSourceManifestDTO>(nameof(DynamicPointLightSourceManifestDTO.Flags)));
            Assert.AreEqual(56, (int)Marshal.OffsetOf<DynamicPointLightSourceManifestDTO>(nameof(DynamicPointLightSourceManifestDTO._pad3)));
        }

        [Test]
        public void CullingJobSubtractsCameraAupBeforeFloatDistance()
        {
            NativeArray<DynamicPointLightSourceDTO> sources = new NativeArray<DynamicPointLightSourceDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<LightCullStateDTO> states = new NativeArray<LightCullStateDTO>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<float4> planes = new NativeArray<float4>(6, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<float> sdf = new NativeArray<float>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<DynamicPointLightProfileRuleDTO> rules = new NativeArray<DynamicPointLightProfileRuleDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<uint> keys = new NativeArray<uint>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> indices = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                DynamicPointLightCullingSettingsDTO settings = default;
                settings.CameraAup = new double3(1000000000.0d, 0.0d, -1000000000.0d);
                settings.BaseFadeDistanceSq = 1024f;
                settings.ImportanceWeight = 1f;
                settings.GlobalQualityWeight = 1f;
                settings.MaxRangeMeters = 64f;
                settings.SubmitIntensityEpsilon = 0.0001f;
                settings.ActiveSourceCount = 1;
                settings.MaxActiveLights = 8;
                settings.FrustumPlaneCount = 0;

                DynamicPointLightSourceDTO source = default;
                source.AUP = settings.CameraAup + new double3(5.0d, 0.0d, 0.0d);
                source.RangeMeters = 8f;
                source.BaseIntensity = 1f;
                source.Priority = 1f;
                source.FadeDistanceSq = 1024f;
                source.Color = new float3(1f, 1f, 1f);
                source.Direction = new float3(0f, 0f, 1f);
                source.LightHash = 17u;
                sources[0] = source;

                new EvaluateLightCullingJob
                {
                    Sources = sources,
                    FrustumPlanes = planes,
                    SdfSamples = sdf,
                    ProfileRules = rules,
                    States = states,
                    ImportanceKeys = keys,
                    ImportanceIndices = indices,
                    Settings = settings,
                    ProfileRuleCount = 0
                }.Run(1);

                Assert.AreEqual(25f, states[0].DistanceSq, 0.0001f);
                Assert.Greater(states[0].ComputedIntensity, 0.1f);
            }
            finally
            {
                sources.Dispose();
                states.Dispose();
                planes.Dispose();
                sdf.Dispose();
                rules.Dispose();
                keys.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void MaxActiveLightBudgetScalesContinuously()
        {
            Assert.AreEqual(8, DynamicPointLightCullingMath.ResolveMaxActiveLights(0f));
            Assert.AreEqual(64, DynamicPointLightCullingMath.ResolveMaxActiveLights(1f));
            int middle = DynamicPointLightCullingMath.ResolveMaxActiveLights(0.5f);
            Assert.Greater(middle, 8);
            Assert.Less(middle, 64);
            Assert.Less(DynamicPointLightCullingMath.ResolveMaxActiveLights(1f, 1f), 64);
        }

        [Test]
        public void RadixSortOrdersImportanceKeysAscending()
        {
            NativeArray<uint> keys = new NativeArray<uint>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> indices = new NativeArray<int>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<uint> scratchKeys = new NativeArray<uint>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            NativeArray<int> scratchIndices = new NativeArray<int>(4, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            try
            {
                keys[0] = 10u; indices[0] = 0;
                keys[1] = 3u; indices[1] = 1;
                keys[2] = 9u; indices[2] = 2;
                keys[3] = 1u; indices[3] = 3;

                new SortLightImportanceJob
                {
                    Keys = keys,
                    Indices = indices,
                    ScratchKeys = scratchKeys,
                    ScratchIndices = scratchIndices,
                    Count = 4
                }.Run();

                Assert.AreEqual(1u, keys[0]);
                Assert.AreEqual(3u, keys[1]);
                Assert.AreEqual(9u, keys[2]);
                Assert.AreEqual(10u, keys[3]);
                Assert.AreEqual(3, indices[0]);
            }
            finally
            {
                keys.Dispose();
                indices.Dispose();
                scratchKeys.Dispose();
                scratchIndices.Dispose();
            }
        }

        [Test]
        public void CsvProfileParserWritesUnmanagedRulesWithoutStringsInParser()
        {
            NativeArray<byte> bytes = new NativeArray<byte>(128, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            NativeArray<DynamicPointLightProfileRuleDTO> rules = new NativeArray<DynamicPointLightProfileRuleDTO>(4, Allocator.Temp, NativeArrayOptions.ClearMemory);
            try
            {
                byte[] source = System.Text.Encoding.ASCII.GetBytes("flare,1.5,0.75,2.0,-0.1,3\n");
                for (int i = 0; i < source.Length; i++)
                    bytes[i] = source[i];

                int parsed = DynamicPointLightProfileCsvParser.Parse(bytes, source.Length, rules, rules.Length, out int rejected);
                Assert.AreEqual(1, parsed);
                Assert.AreEqual(0, rejected);
                Assert.AreEqual(BuildFnv("flare"), rules[0].ProfileHash);
                Assert.AreEqual(1.5f, rules[0].PriorityMultiplier, 0.0001f);
                Assert.AreEqual(0.75f, rules[0].FadeDistanceMultiplier, 0.0001f);
                Assert.AreEqual(2.0f, rules[0].IntensityMultiplier, 0.0001f);
                Assert.AreEqual(3u, rules[0].Flags);
            }
            finally
            {
                bytes.Dispose();
                rules.Dispose();
            }
        }

        [Test]
        public void RuntimeSourceAvoidsUnityLightObjectSubmission()
        {
            string root = Path.Combine(Application.dataPath, "_Project/Scripts/Lighting/DynamicPointLightCulling");
            string director = File.ReadAllText(Path.Combine(root, "DynamicPointLightCullingDirector.cs"));
            string jobs = File.ReadAllText(Path.Combine(root, "DynamicPointLightCullingJobs.cs"));
            Assert.That(director, Does.Not.Contain("Light.enabled"));
            Assert.That(director, Does.Not.Contain("new Light"));
            Assert.That(jobs, Does.Not.Contain("math.sqrt"));
            Assert.That(jobs, Does.Not.Contain("math.length("));
            Assert.That(jobs, Does.Not.Contain("Vector3.Distance"));
            Assert.That(jobs, Does.Contain("UnsafeUtility.AsRef"));
            Assert.That(jobs, Does.Contain("NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr"));
            Assert.That(jobs, Does.Contain("NativeArrayUnsafeUtility.GetUnsafePtr"));
            Assert.That(director, Does.Not.Contain("GeometryUtility.CalculateFrustumPlanes"));
            Assert.That(director, Does.Not.Contain("Plane[]"));
            Assert.That(director, Does.Not.Contain("InjectDynamicLightJob"));
            Assert.That(director, Does.Contain("GraphicsBuffer.UsageFlags.LockBufferForWrite"));
            Assert.That(director, Does.Contain("NativeArrayOptions.UninitializedMemory"));
            Assert.That(director, Does.Contain("TryGetProbeBounceReadback"));
            Assert.That(director, Does.Contain("SdfSampleCount = _mockSdfSeeded"));
            Assert.That(director, Does.Contain("NativeArrayOptions.ClearMemory"));
            Assert.That(director, Does.Contain("EnsureGpuBuffers(gpuCapacity)"));
            Assert.That(director, Does.Contain("TryCommitExternalSourceCount"));
            Assert.That(director, Does.Contain("SourceManifest"));
            Assert.That(File.ReadAllText(Path.Combine(root, "DynamicPointLightCullingContracts.cs")), Does.Contain("math.step"));
        }

        [Test]
        public void RollbackMerkleSourceDoesNotHashDynamicLightPresentationBuffers()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Scripts/Networking/RollbackNetcodeContracts.cs");
            string source = File.ReadAllText(path);
            Assert.That(source, Does.Not.Contain("DynamicPointLight"));
            Assert.That(source, Does.Not.Contain("LightCullStateDTO"));
            Assert.That(source, Does.Not.Contain("GpuPayloadFront"));
        }

        private static uint BuildFnv(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
                hash = DynamicPointLightCullingMath.FnvaByte(hash, (byte)text[i]);
            return hash;
        }
    }
}
