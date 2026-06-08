using System;
using System.IO;
using System.Text;
using Hecton8.Construction;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class FoundationSnappingCalculatorEditTests
    {
        [Test]
        public void PylonMatrixDTO_IsExactPromptLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<PylonMatrixDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<FoundationPylonSurfaceDTO>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<FoundationPylonFrameCounters>(), Is.EqualTo(64));
            Assert.That(UnsafeUtility.SizeOf<FoundationStructuralWarningSignal>(), Is.EqualTo(64));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<PylonMatrixDTO>(nameof(PylonMatrixDTO.LocalToWorld)), Is.EqualTo(0));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.Flags)), Is.EqualTo(48));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.ModuleHash)), Is.EqualTo(52));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.RayIndex)), Is.EqualTo(56));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationPylonSurfaceDTO>(nameof(FoundationPylonSurfaceDTO.ResultHash)), Is.EqualTo(60));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationStructuralWarningSignal>(nameof(FoundationStructuralWarningSignal.WarningFlags)), Is.EqualTo(28));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveOffset<FoundationStructuralWarningSignal>(nameof(FoundationStructuralWarningSignal.ResultHash)), Is.EqualTo(44));
            Assert.That(FoundationSnappingCalculatorRuntime.ValidateStructLayout(), Is.True);
        }

        [Test]
        public void QualityWeight_ResolvesContinuousPylonBudget()
        {
            FoundationTuningDTO tuning = FoundationSnappingCalculatorRuntime.CreateDefaultTuning(0f);
            tuning.MinRaysPerModule = 1;
            tuning.MaxRaysPerModule = 4;
            tuning.GlobalQualityWeight = 0f;
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveRaysPerModule(tuning), Is.EqualTo(1));
            tuning.GlobalQualityWeight = 0.5f;
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveRaysPerModule(tuning), Is.EqualTo(2));
            tuning.GlobalQualityWeight = 1f;
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveRaysPerModule(tuning), Is.EqualTo(4));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveSdfInterpolationWeight(tuning), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveRayBudget(tuning), Is.EqualTo(4f).Within(0.0001f));
            tuning.GlobalQualityWeight = 0f;
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveMarchSteps(tuning), Is.EqualTo(1));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveSdfInterpolationWeight(tuning), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(FoundationSnappingCalculatorRuntime.ResolveRayBudget(tuning), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void EncodedVoxelSdfRaymarch_BuildsGpuMatrixWithoutPhysx()
        {
            using NativeArray<FoundationModuleAupDTO> modules = new NativeArray<FoundationModuleAupDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            const int sizeX = 16;
            const int sizeY = 32;
            const int sizeZ = 16;
            const float sdfRange = 32f;
            using NativeArray<byte> sdf = new NativeArray<byte>(sizeX * sizeY * sizeZ, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using NativeArray<PylonMatrixDTO> matrices = new NativeArray<PylonMatrixDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonSurfaceDTO> surfaces = new NativeArray<FoundationPylonSurfaceDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonFrameCounters> perModule = new NativeArray<FoundationPylonFrameCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonFrameCounters> frame = new NativeArray<FoundationPylonFrameCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationDebugRayDTO> debug = new NativeArray<FoundationDebugRayDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonIndirectArgsDTO> args = new NativeArray<FoundationPylonIndirectArgsDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<FoundationModuleAupDTO> writableModules = modules;
            NativeArray<byte> writableSdf = sdf;

            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        int index = x + sizeX * (y + sizeY * z);
                        float signedDistance = y - 8f;
                        float normalized = math.clamp((signedDistance / sdfRange) * 0.5f + 0.5f, 0f, 1f);
                        writableSdf[index] = (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);
                    }
                }
            }

            FoundationSdfConfigDTO config;
            config.OriginAup = new double3(24d, 0d, 24d);
            config.VoxelSizeMeters = 1f;
            config.SizeX = sizeX;
            config.SizeY = sizeY;
            config.SizeZ = sizeZ;
            config.SdfRangeMeters = sdfRange;
            config.IsoSurface = 0f;
            config.Reserved0 = 0f;
            config.Reserved1 = 0f;
            config.Reserved2 = 0f;
            config.Flags = FoundationPylonFlags.RealVoxelSdf;
            writableModules[0] = new FoundationModuleAupDTO
            {
                CenterAup = new double3(32d, 16d, 32d),
                Rotation = quaternion.identity,
                BoundsExtents = new float3(2f, 1f, 2f),
                GroundClearanceMeters = 0.05f,
                ModuleHash = 0xF0252001u,
                Flags = FoundationPylonFlags.Active
            };

            FoundationTuningDTO tuning = FoundationSnappingCalculatorRuntime.CreateDefaultTuning(1f);
            JobHandle handle = new CalculateFoundationPylonsJob
            {
                Modules = modules,
                EncodedVoxelSdfTexture3D = sdf,
                PylonMatrices = matrices,
                PylonSurfaces = surfaces,
                PerModuleCounters = perModule,
                DebugRays = debug,
                SdfConfig = config,
                Tuning = tuning,
                CameraAup = double3.zero,
                ModuleCount = 1,
                ProfileCount = 0,
                RayOriginCount = 0
            }.Schedule(1, 1);

            handle = new ReduceFoundationPylonCountersJob
            {
                PerModuleCounters = perModule,
                FrameCounters = frame,
                ModuleCount = 1
            }.Schedule(handle);

            handle = new CompactFoundationPylonDrawListJob
            {
                PylonMatrices = matrices,
                PylonSurfaces = surfaces,
                FrameCounters = frame,
                SlotCount = FoundationSnappingCalculatorRuntime.MaxRaysPerModule
            }.Schedule(handle);

            handle = new BuildFoundationPylonIndirectArgsJob
            {
                FrameCounters = frame,
                Args = args,
                SlotCount = FoundationSnappingCalculatorRuntime.MaxRaysPerModule
            }.Schedule(handle);
            handle.Complete();

            Assert.That((surfaces[0].Flags & FoundationPylonFlags.Active) != 0u, Is.True);
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.RealVoxelSdf) != 0u, Is.True);
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) == 0u, Is.True);
            Assert.That(frame[0].HitCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(frame[0].SlotCount, Is.EqualTo(frame[0].ActivePylonCount));
            Assert.That(args[0].InstanceCount, Is.EqualTo((uint)frame[0].ActivePylonCount));
            Assert.That(matrices[0].LocalToWorld.c1.y, Is.GreaterThan(0f));
            Assert.That(math.abs(matrices[0].LocalToWorld.c0.x), Is.EqualTo(surfaces[0].AxisRadius.w * 2f).Within(0.0001f));
        }

        [Test]
        public void BoundaryVoxelSdfNormal_DoesNotUseOutOfBoundsSentinel()
        {
            using NativeArray<FoundationModuleAupDTO> modules = new NativeArray<FoundationModuleAupDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            const int sizeX = 8;
            const int sizeY = 16;
            const int sizeZ = 8;
            const float sdfRange = 16f;
            using NativeArray<byte> sdf = new NativeArray<byte>(sizeX * sizeY * sizeZ, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            using NativeArray<PylonMatrixDTO> matrices = new NativeArray<PylonMatrixDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonSurfaceDTO> surfaces = new NativeArray<FoundationPylonSurfaceDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonFrameCounters> perModule = new NativeArray<FoundationPylonFrameCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationDebugRayDTO> debug = new NativeArray<FoundationDebugRayDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<FoundationModuleAupDTO> writableModules = modules;
            NativeArray<byte> writableSdf = sdf;

            for (int z = 0; z < sizeZ; z++)
            {
                for (int y = 0; y < sizeY; y++)
                {
                    for (int x = 0; x < sizeX; x++)
                    {
                        int index = x + sizeX * (y + sizeY * z);
                        float signedDistance = y - 4f;
                        float normalized = math.clamp((signedDistance / sdfRange) * 0.5f + 0.5f, 0f, 1f);
                        writableSdf[index] = (byte)math.clamp((int)math.round(normalized * 255f), 0, 255);
                    }
                }
            }

            FoundationSdfConfigDTO config;
            config.OriginAup = double3.zero;
            config.VoxelSizeMeters = 1f;
            config.SizeX = sizeX;
            config.SizeY = sizeY;
            config.SizeZ = sizeZ;
            config.SdfRangeMeters = sdfRange;
            config.IsoSurface = 0f;
            config.Reserved0 = 0f;
            config.Reserved1 = 0f;
            config.Reserved2 = 0f;
            config.Flags = FoundationPylonFlags.RealVoxelSdf;
            writableModules[0] = new FoundationModuleAupDTO
            {
                CenterAup = new double3(0.05d, 6d, 4d),
                Rotation = quaternion.identity,
                BoundsExtents = new float3(0.1f),
                GroundClearanceMeters = 0.05f,
                ModuleHash = 0xF0252003u,
                Flags = FoundationPylonFlags.Active
            };

            FoundationTuningDTO tuning = FoundationSnappingCalculatorRuntime.CreateDefaultTuning(1f);
            tuning.SdfHitEpsilonMeters = 0.08f;
            JobHandle handle = new CalculateFoundationPylonsJob
            {
                Modules = modules,
                EncodedVoxelSdfTexture3D = sdf,
                PylonMatrices = matrices,
                PylonSurfaces = surfaces,
                PerModuleCounters = perModule,
                DebugRays = debug,
                SdfConfig = config,
                Tuning = tuning,
                CameraAup = double3.zero,
                ModuleCount = 1,
                ProfileCount = 0,
                RayOriginCount = 0
            }.Schedule(1, 1);
            handle.Complete();

            float3 normal = surfaces[0].SurfaceNormalFlare.xyz;
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.Active) != 0u, Is.True);
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.OutOfSdfBounds) == 0u, Is.True);
            Assert.That(math.abs(normal.x), Is.LessThan(0.15f));
            Assert.That(normal.y, Is.GreaterThan(0.95f));
            Assert.That(math.abs(normal.z), Is.LessThan(0.15f));
        }

        [Test]
        public void MissingVoxelSdf_FailsClosedWithNoSubstrateFlag()
        {
            using NativeArray<FoundationModuleAupDTO> modules = new NativeArray<FoundationModuleAupDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<PylonMatrixDTO> matrices = new NativeArray<PylonMatrixDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonSurfaceDTO> surfaces = new NativeArray<FoundationPylonSurfaceDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonFrameCounters> perModule = new NativeArray<FoundationPylonFrameCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationDebugRayDTO> debug = new NativeArray<FoundationDebugRayDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<FoundationModuleAupDTO> writableModules = modules;
            writableModules[0] = new FoundationModuleAupDTO
            {
                CenterAup = new double3(32d, 16d, 32d),
                Rotation = quaternion.identity,
                BoundsExtents = new float3(2f, 1f, 2f),
                GroundClearanceMeters = 0.05f,
                ModuleHash = 0xF0252001u,
                Flags = FoundationPylonFlags.Active
            };

            JobHandle handle = new CalculateFoundationPylonsJob
            {
                Modules = modules,
                EncodedVoxelSdfTexture3D = default,
                PylonMatrices = matrices,
                PylonSurfaces = surfaces,
                PerModuleCounters = perModule,
                DebugRays = debug,
                SdfConfig = FoundationSnappingCalculatorRuntime.CreateEmptySdfConfig(double3.zero),
                Tuning = FoundationSnappingCalculatorRuntime.CreateDefaultTuning(1f),
                CameraAup = double3.zero,
                ModuleCount = 1,
                ProfileCount = 0,
                RayOriginCount = 0
            }.Schedule(1, 1);
            handle.Complete();

            Assert.That((surfaces[0].Flags & FoundationPylonFlags.Active) == 0u, Is.True);
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That((perModule[0].Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That(perModule[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void CorruptVoxelSdfRange_FailsClosedWithNoSubstrateFlag()
        {
            using NativeArray<FoundationModuleAupDTO> modules = new NativeArray<FoundationModuleAupDTO>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<byte> sdf = new NativeArray<byte>(8, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<PylonMatrixDTO> matrices = new NativeArray<PylonMatrixDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonSurfaceDTO> surfaces = new NativeArray<FoundationPylonSurfaceDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationPylonFrameCounters> perModule = new NativeArray<FoundationPylonFrameCounters>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationDebugRayDTO> debug = new NativeArray<FoundationDebugRayDTO>(FoundationSnappingCalculatorRuntime.MaxRaysPerModule, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<FoundationModuleAupDTO> writableModules = modules;
            writableModules[0] = new FoundationModuleAupDTO
            {
                CenterAup = new double3(1d, 1d, 1d),
                Rotation = quaternion.identity,
                BoundsExtents = new float3(1f),
                GroundClearanceMeters = 0.05f,
                ModuleHash = 0xF0252002u,
                Flags = FoundationPylonFlags.Active
            };

            FoundationSdfConfigDTO config;
            config.OriginAup = double3.zero;
            config.VoxelSizeMeters = 1f;
            config.SizeX = 2;
            config.SizeY = 2;
            config.SizeZ = 2;
            config.SdfRangeMeters = 0f;
            config.IsoSurface = 0f;
            config.Reserved0 = 0f;
            config.Reserved1 = 0f;
            config.Reserved2 = 0f;
            config.Flags = FoundationPylonFlags.RealVoxelSdf;

            JobHandle handle = new CalculateFoundationPylonsJob
            {
                Modules = modules,
                EncodedVoxelSdfTexture3D = sdf,
                PylonMatrices = matrices,
                PylonSurfaces = surfaces,
                PerModuleCounters = perModule,
                DebugRays = debug,
                SdfConfig = config,
                Tuning = FoundationSnappingCalculatorRuntime.CreateDefaultTuning(1f),
                CameraAup = double3.zero,
                ModuleCount = 1,
                ProfileCount = 0,
                RayOriginCount = 0
            }.Schedule(1, 1);
            handle.Complete();

            Assert.That((surfaces[0].Flags & FoundationPylonFlags.Active) == 0u, Is.True);
            Assert.That((surfaces[0].Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That((perModule[0].Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That(perModule[0].HitCount, Is.EqualTo(0));
        }

        [Test]
        public void SanitizeSdfConfig_DoesNotPromoteCorruptRealPayload()
        {
            FoundationSdfConfigDTO config;
            config.OriginAup = new double3(double.NaN, double.PositiveInfinity, double.NegativeInfinity);
            config.VoxelSizeMeters = 0f;
            config.SizeX = 0;
            config.SizeY = 0;
            config.SizeZ = 0;
            config.SdfRangeMeters = 0f;
            config.IsoSurface = float.NaN;
            config.Reserved0 = float.NaN;
            config.Reserved1 = float.NaN;
            config.Reserved2 = float.NaN;
            config.Flags = FoundationPylonFlags.RealVoxelSdf;

            FoundationSdfConfigDTO sanitized = FoundationSnappingCalculatorRuntime.SanitizeSdfConfig(config);

            Assert.That((sanitized.Flags & FoundationPylonFlags.RealVoxelSdf) == 0u, Is.True);
            Assert.That((sanitized.Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That(sanitized.VoxelSizeMeters, Is.EqualTo(0f));
            Assert.That(sanitized.SizeX, Is.EqualTo(0));
            Assert.That(sanitized.SizeY, Is.EqualTo(0));
            Assert.That(sanitized.SizeZ, Is.EqualTo(0));
            Assert.That(sanitized.SdfRangeMeters, Is.EqualTo(0f));
            Assert.That(math.all(sanitized.OriginAup == double3.zero), Is.True);
            Assert.That(sanitized.IsoSurface, Is.EqualTo(0f));
        }

        [Test]
        public void SanitizeSdfConfig_PreservesNoSubstrateEmptyPayload()
        {
            FoundationSdfConfigDTO sanitized = FoundationSnappingCalculatorRuntime.SanitizeSdfConfig(
                FoundationSnappingCalculatorRuntime.CreateEmptySdfConfig(new double3(12d, -4d, 7d)));

            Assert.That((sanitized.Flags & FoundationPylonFlags.RealVoxelSdf) == 0u, Is.True);
            Assert.That((sanitized.Flags & FoundationPylonFlags.SnapFailed_NoSubstrate) != 0u, Is.True);
            Assert.That(sanitized.VoxelSizeMeters, Is.EqualTo(0f));
            Assert.That(sanitized.SizeX, Is.EqualTo(0));
            Assert.That(sanitized.SizeY, Is.EqualTo(0));
            Assert.That(sanitized.SizeZ, Is.EqualTo(0));
            Assert.That(sanitized.SdfRangeMeters, Is.EqualTo(0f));
            Assert.That(math.all(sanitized.OriginAup == new double3(12d, -4d, 7d)), Is.True);
        }

        [Test]
        public void CsvProfiles_UseFixedSlotsForRepeatedModuleRows()
        {
            using NativeArray<FoundationRayOriginDTO> rays = new NativeArray<FoundationRayOriginDTO>(FoundationSnappingCalculatorRuntime.RayProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationProfileRangeDTO> ranges = new NativeArray<FoundationProfileRangeDTO>(FoundationSnappingCalculatorRuntime.ProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            byte[] csv = Encoding.ASCII.GetBytes(
                "alpha,0,0,0,0,1\n" +
                "bravo,0,0,0,0,1\n" +
                "alpha,1,1,0,0,1\n");

            bool loaded = FoundationSnappingCalculatorRuntime.TryLoadProfilesFromCsvBytes(
                csv,
                rays,
                ranges,
                out int profileCount,
                out int rayCount);

            Assert.That(loaded, Is.True);
            Assert.That(profileCount, Is.EqualTo(2));
            Assert.That(rayCount, Is.EqualTo(8));
            Assert.That(ranges[0].StartIndex, Is.EqualTo(0));
            Assert.That(ranges[0].Count, Is.EqualTo(FoundationSnappingCalculatorRuntime.MaxRaysPerModule));
            Assert.That((rays[0].Flags & FoundationPylonFlags.Active) != 0u, Is.True);
            Assert.That((rays[1].Flags & FoundationPylonFlags.Active) != 0u, Is.True);
            Assert.That(rays[1].RayIndex, Is.EqualTo(1u));
            Assert.That((rays[2].Flags & FoundationPylonFlags.Active) != 0u, Is.False);
        }

        [Test]
        public void CsvProfiles_RejectEmptyModuleAndKeepModulePrefixedNames()
        {
            using NativeArray<FoundationRayOriginDTO> rays = new NativeArray<FoundationRayOriginDTO>(FoundationSnappingCalculatorRuntime.RayProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationProfileRangeDTO> ranges = new NativeArray<FoundationProfileRangeDTO>(FoundationSnappingCalculatorRuntime.ProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            byte[] csv = Encoding.ASCII.GetBytes(
                "module_hash,ray,x,y,z,radius\n" +
                ",0,0,0,0,1\n" +
                "module_alpha,0,0,0,0,1\n");

            bool loaded = FoundationSnappingCalculatorRuntime.TryLoadProfilesFromCsvBytes(
                csv,
                rays,
                ranges,
                out int profileCount,
                out int rayCount);

            Assert.That(loaded, Is.True);
            Assert.That(profileCount, Is.EqualTo(1));
            Assert.That(rayCount, Is.EqualTo(FoundationSnappingCalculatorRuntime.MaxRaysPerModule));
            Assert.That((rays[0].Flags & FoundationPylonFlags.Active) != 0u, Is.True);
            Assert.That(ranges[0].ModuleHash, Is.EqualTo(rays[0].ModuleHash));
        }

        [Test]
        public void TelemetryCursor_UsesUnsignedWrapForUninitializedValues()
        {
            using NativeArray<FoundationTelemetryEntry> telemetry = new NativeArray<FoundationTelemetryEntry>(4, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<int> cursor = new NativeArray<int>(1, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            NativeArray<int> writableCursor = cursor;

            writableCursor[0] = int.MinValue;
            FoundationPylonFrameCounters counters = default;
            counters.SlotCount = 1;
            counters.ResultHash = 0x252u;

            FoundationSnappingCalculatorRuntime.WriteTelemetry(
                telemetry,
                cursor,
                double3.zero,
                7u,
                in counters,
                1.25f,
                0.5f);

            Assert.That(cursor[0], Is.EqualTo(1));
            Assert.That(telemetry[0].Frame, Is.EqualTo(7u));
        }

        [Test]
        public void ProfileReadFence_ExposesPendingJobGuard()
        {
            Assert.That(FoundationSnappingCalculatorRuntime.HasActiveProfileReadFence(), Is.False);
            Assert.That(FoundationSnappingCalculatorRuntime.TryBeginProfileReadFence(), Is.True);
            Assert.That(FoundationSnappingCalculatorRuntime.HasActiveProfileReadFence(), Is.True);
            using NativeArray<FoundationRayOriginDTO> rays = new NativeArray<FoundationRayOriginDTO>(FoundationSnappingCalculatorRuntime.RayProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            using NativeArray<FoundationProfileRangeDTO> ranges = new NativeArray<FoundationProfileRangeDTO>(FoundationSnappingCalculatorRuntime.ProfileCapacity, Allocator.TempJob, NativeArrayOptions.ClearMemory);
            byte[] csv = Encoding.ASCII.GetBytes("alpha,0,0,0,0,1\n");
            Assert.That(FoundationSnappingCalculatorRuntime.TryLoadProfilesFromCsvBytes(csv, rays, ranges, out _, out _), Is.False);
            FoundationSnappingCalculatorRuntime.EndProfileReadFence();
            Assert.That(FoundationSnappingCalculatorRuntime.HasActiveProfileReadFence(), Is.False);
        }

        [Test]
        public void SocketModuleReadFence_ExposesFoundationConsumerGate()
        {
            ShinobuSocketConstructionRuntime.EndModuleWriteFence();
            for (int i = 0; i < 4 && ShinobuSocketConstructionRuntime.HasActiveModuleReadFence(); i++)
                ShinobuSocketConstructionRuntime.EndModuleReadFence();

            Assert.That(ShinobuSocketConstructionRuntime.HasActiveModuleReadFence(), Is.False);
            Assert.That(ShinobuSocketConstructionRuntime.TryBeginModuleReadFence(), Is.True);
            Assert.That(ShinobuSocketConstructionRuntime.TryBeginModuleReadFence(), Is.True);
            Assert.That(ShinobuSocketConstructionRuntime.HasActiveModuleReadFence(), Is.True);
            Assert.That(ShinobuSocketConstructionRuntime.TryBeginModuleWriteFence(), Is.False);
            ShinobuSocketConstructionRuntime.EndModuleReadFence();
            Assert.That(ShinobuSocketConstructionRuntime.HasActiveModuleReadFence(), Is.True);
            ShinobuSocketConstructionRuntime.EndModuleReadFence();
            Assert.That(ShinobuSocketConstructionRuntime.HasActiveModuleReadFence(), Is.False);
            Assert.That(ShinobuSocketConstructionRuntime.TryBeginModuleWriteFence(), Is.True);
            ShinobuSocketConstructionRuntime.EndModuleWriteFence();
        }

        [Test]
        public void FoundationPylonGpuBatch_NoSubstrateNotificationRefusalIsDiagnosticNotStructural()
        {
            string root = Directory.GetCurrentDirectory();
            string source = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scripts", "Construction", "FoundationPylonGpuBatch.cs"));

            Assert.That(source.Contains("public int NoSubstrateNotificationMissCount =>"), Is.True);
            Assert.That(source.Contains("s_noSubstrateNotificationMissWarningHash"), Is.True);
            Assert.That(source.Contains("s_noSubstrateNotificationContextHash"), Is.True);
            AssertTextBefore(
                source,
                "SignalBus<FoundationStructuralWarningSignal>.TryPushTracked(in signal, ref s_x001FoundationPylonGpuBatchSignalPushDropCount);",
                "TryPushNoSubstrateNotification();");
            Assert.That(source.Contains("NotificationEvents.TryPushWarning(NoSubstrateWarningMessage.AsSpan());"), Is.False);

            AssertTextAfter(source, "private void TryPushNoSubstrateNotification()", "NotificationEvents.TryPushWarning(NoSubstrateWarningMessage.AsSpan())");
            AssertTextAfter(source, "private void TryPushNoSubstrateNotification()", "ReportNoSubstrateNotificationMiss();");
            AssertTextAfter(source, "private void ReportNoSubstrateNotificationMiss()", "_noSubstrateNotificationMissCount++;");
            AssertTextAfter(source, "private void ReportNoSubstrateNotificationMiss()", "GlobalTelemetryBus.PublishPerformanceWarning(");
            AssertTextAfter(source, "private void ReportNoSubstrateNotificationMiss()", "s_noSubstrateNotificationMissWarningHash");
            AssertTextAfter(source, "private void ReportNoSubstrateNotificationMiss()", "s_foundationPylonGpuBatchContextHash ^ s_noSubstrateNotificationContextHash");
            AssertTextAfter(source, "private void ReportNoSubstrateNotificationMiss()", "math.max(1, _noSubstrateNotificationMissCount)");
            AssertTextAfter(source, "private void OnDisable()", "ClearNoSubstrateNotificationDiagnostics();");
            AssertTextAfter(source, "private void OnDestroy()", "ClearNoSubstrateNotificationDiagnostics();");
        }

        [Test]
        public void Shader_AvoidsTrigonometricHotAluAndIsPlayerIncluded()
        {
            string root = Directory.GetCurrentDirectory();
            string shaderPath = Path.Combine(root, "Assets", "_Project", "Shaders", "Hecton_FoundationPylon.shader");
            string shader = File.ReadAllText(shaderPath);
            Assert.That(shader.Contains("co" + "s("), Is.False);
            Assert.That(shader.Contains("si" + "n("), Is.False);
            Assert.That(shader.Contains("po" + "w("), Is.False);
            Assert.That(shader.Contains("\"RenderType\"=\"Opaque\""), Is.True);
            Assert.That(shader.Contains("\"Queue\"=\"Geometry\""), Is.True);
            Assert.That(shader.Contains("ZWrite On"), Is.True);
            Assert.That(shader.Contains("SafeNormalize"), Is.True);

            string graphicsSettings = File.ReadAllText(Path.Combine(root, "ProjectSettings", "GraphicsSettings.asset"));
            Assert.That(graphicsSettings.Contains("0e3d6c95b94344c7b864f17da3f25205"), Is.True);
            Assert.That(graphicsSettings.Contains("0e3d6c95b94344c7b864f17da3f25207"), Is.True);
            string bootstrapScene = File.ReadAllText(Path.Combine(root, "Assets", "_Project", "Scenes", "00_BOOTSTRAP.unity"));
            Assert.That(bootstrapScene.Contains("0e3d6c95b94344c7b864f17da3f25207"), Is.True);
        }

        [Test]
        public void FoundationFiles_DoNotIntroducePhysxOrPylonGameObjects()
        {
            string root = Directory.GetCurrentDirectory();
            string constructionDir = Path.Combine(root, "Assets", "_Project", "Scripts", "Construction");
            string[] files = Directory.GetFiles(constructionDir, "Foundation*.cs", SearchOption.AllDirectories);
            StringBuilder builder = new StringBuilder(4096);
            for (int i = 0; i < files.Length; i++)
                builder.AppendLine(File.ReadAllText(files[i]));
            string combined = builder.ToString();
            Assert.That(combined.Contains("Physics" + ".Raycast"), Is.False);
            Assert.That(combined.Contains("Raycast" + "Command"), Is.False);
            Assert.That(combined.Contains("Instantiate" + "("), Is.False);
            Assert.That(combined.Contains("List" + "<Transform>"), Is.False);
            Assert.That(combined.Contains("TryPopulatePreview" + "Fallback"), Is.False);
            Assert.That(combined.Contains("Construction" + "PreviewSignal"), Is.False);
        }

        [Test]
        public void PylonMatrixDTO_StaysOutOfRollbackAndMerklePaths()
        {
            string root = Directory.GetCurrentDirectory();
            string scripts = Path.Combine(root, "Assets", "_Project", "Scripts");
            string[] files = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i].Replace('\\', '/');
                if (file.Contains("/Construction/Foundation") ||
                    file.Contains("/Construction/Editor/Foundation"))
                {
                    continue;
                }

                string text = File.ReadAllText(files[i]);
                Assert.That(text.Contains("PylonMatrixDTO"), Is.False, file);
            }
        }

        private static void AssertTextBefore(string text, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = text.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = text.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.That(earlierIndex, Is.GreaterThanOrEqualTo(0), "Missing earlier text: " + expectedEarlier);
            Assert.That(laterIndex, Is.GreaterThanOrEqualTo(0), "Missing later text: " + expectedLater);
            Assert.That(earlierIndex, Is.LessThan(laterIndex), expectedEarlier + " should appear before " + expectedLater);
        }

        private static void AssertTextAfter(string text, string anchor, string expectedLater)
        {
            int anchorIndex = text.IndexOf(anchor, StringComparison.Ordinal);
            Assert.That(anchorIndex, Is.GreaterThanOrEqualTo(0), "Missing anchor text: " + anchor);
            int laterIndex = text.IndexOf(expectedLater, anchorIndex, StringComparison.Ordinal);
            Assert.That(laterIndex, Is.GreaterThanOrEqualTo(0), "Missing text after anchor: " + expectedLater);
        }
    }
}
