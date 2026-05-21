using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.Graphics.Culling
{
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct VertexBudgetDTO
    {
        [FieldOffset(0)]
        public uint MaxVisibleVertices;     // offset 0, size 4
        [FieldOffset(4)]
        public uint CurrentVisibleVertices; // offset 4, size 4
        [FieldOffset(8)]
        public float TilePressure;          // offset 8, size 4
        [FieldOffset(12)]
        public uint _pad0;                  // offset 12, size 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct TBDRVertexBudgetCounter64
    {
        [FieldOffset(0)]
        public VertexBudgetDTO Budget;      // offset 0, size 16
        [FieldOffset(16)]
        public ulong _pad0;                 // offset 16, size 8
        [FieldOffset(24)]
        public ulong _pad1;                 // offset 24, size 8
        [FieldOffset(32)]
        public ulong _pad2;                 // offset 32, size 8
        [FieldOffset(40)]
        public ulong _pad3;                 // offset 40, size 8
        [FieldOffset(48)]
        public ulong _pad4;                 // offset 48, size 8
        [FieldOffset(56)]
        public ulong _pad5;                 // offset 56, size 8
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TileSpillWarningDTO
    {
        [FieldOffset(0)]
        public float EstimatedOverdraw;     // offset 0, size 4
        [FieldOffset(4)]
        public uint CulledInstanceCount;    // offset 4, size 4
        [FieldOffset(8)]
        public ulong _pad0;                 // offset 8, size 8
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public partial struct MockQualityWeightSignal
    {
        [FieldOffset(0)]
        public float GlobalQualityWeight;   // offset 0, size 4
        [FieldOffset(4)]
        public uint Frame;                  // offset 4, size 4
        [FieldOffset(8)]
        public uint Seed;                   // offset 8, size 4
        [FieldOffset(12)]
        public uint _pad0;                  // offset 12, size 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PoiTransformDTO
    {
        [FieldOffset(0)]
        public float4x4 LocalToWorld;                 // offset 0, size 64
        [FieldOffset(64)]
        public float4 CameraRelativePositionRadius;   // offset 64, size 16
        [FieldOffset(80)]
        public uint MeshId;                           // offset 80, size 4
        [FieldOffset(84)]
        public uint InstanceId;                       // offset 84, size 4
        [FieldOffset(88)]
        public uint VertexCount;                      // offset 88, size 4
        [FieldOffset(92)]
        public float DistanceSq;                      // offset 92, size 4
        [FieldOffset(96)]
        public uint SortKey;                          // offset 96, size 4
        [FieldOffset(100)]
        public uint Flags;                            // offset 100, size 4
        [FieldOffset(104)]
        public ulong _pad0;                           // offset 104, size 8
        [FieldOffset(112)]
        public ulong _pad1;                           // offset 112, size 8
        [FieldOffset(120)]
        public ulong _pad2;                           // offset 120, size 8
    }

    public static class TBDRVisibilityFlags
    {
        public const uint FrustumRejected = 1u << 0;
        public const uint HzbRejected = 1u << 1;
        public const uint RejectedMask = FrustumRejected | HzbRejected;
    }

    public static class TBDRHardwareBudgetMath
    {
        public const uint MaxSafeVisibleVertices = 20000000u;

        public static uint ClampVisibleVertexCap(uint value)
        {
            if (value == 0u)
                return 1u;

            return value < MaxSafeVisibleVertices ? value : MaxSafeVisibleVertices;
        }
    }

    public struct MockScatterBuffer
    {
        public NativeArray<PoiTransformDTO> VisibleInstances;
        public NativeArray<PoiTransformDTO> SortScratch;
        public NativeArray<uint> MeshVertexCounts;
        public NativeArray<int> RadixHistogram;
        public NativeArray<int> VisibleCountOut;
        public int InstanceCount;
        public int MeshCount;
        public uint Frame;
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct MockCameraMatrix
    {
        [FieldOffset(0)]
        public float4x4 ViewProjection;
        [FieldOffset(64)]
        public float4 PositionRadius;
        [FieldOffset(80)]
        public float4 ForwardFov;
        [FieldOffset(96)]
        public uint Frame;
        [FieldOffset(100)]
        public uint Flags;
        [FieldOffset(104)]
        public ulong _pad0;
        [FieldOffset(112)]
        public ulong _pad1;
        [FieldOffset(120)]
        public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 48)]
    public struct AupGpuLocalizationInput
    {
        [FieldOffset(0)]
        public long CellX;          // offset 0, size 8
        [FieldOffset(8)]
        public long CellY;          // offset 8, size 8
        [FieldOffset(16)]
        public long CellZ;          // offset 16, size 8
        [FieldOffset(24)]
        public float3 Local;        // offset 24, size 12
        [FieldOffset(36)]
        public float BoundsRadius;  // offset 36, size 4
        [FieldOffset(40)]
        public uint MeshId;         // offset 40, size 4
        [FieldOffset(44)]
        public uint InstanceId;     // offset 44, size 4
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TextureStreamingSliceDTO
    {
        [FieldOffset(0)]
        public uint BiomeHash;
        [FieldOffset(4)]
        public uint SliceId;
        [FieldOffset(8)]
        public uint LastTouchedFrame;
        [FieldOffset(12)]
        public uint ResidentFlags;
        [FieldOffset(16)]
        public uint SourceWidth;
        [FieldOffset(20)]
        public uint SourceHeight;
        [FieldOffset(24)]
        public uint ApproxBytes;
        [FieldOffset(28)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TBDRPipelineTelemetryEntry
    {
        [FieldOffset(0)]
        public uint Frame;
        [FieldOffset(4)]
        public uint TotalSubmittedVertices;
        [FieldOffset(8)]
        public uint MaxVisibleVertices;
        [FieldOffset(12)]
        public uint TileSpillWarnings;
        [FieldOffset(16)]
        public float SortComputeTimeMs;
        [FieldOffset(20)]
        public float TilePressure;
        [FieldOffset(24)]
        public uint Flags;
        [FieldOffset(28)]
        public uint StateHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TBDRHardwareBudgetLimits
    {
        [FieldOffset(0)]
        public uint Quest3MaxVisibleVertices;
        [FieldOffset(4)]
        public uint MobileLowMaxVisibleVertices;
        [FieldOffset(8)]
        public uint SteamDeckMaxVisibleVertices;
        [FieldOffset(12)]
        public uint DesktopMaxVisibleVertices;
        [FieldOffset(16)]
        public uint TextureArrayBudgetMb;
        [FieldOffset(20)]
        public uint TransparentQuadLimit;
        [FieldOffset(24)]
        public float FrustumSqueezeDegrees;
        [FieldOffset(28)]
        public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TBDRTunerSnapshot
    {
        [FieldOffset(0)]
        public uint HardVertexCap;
        [FieldOffset(4)]
        public uint CurrentVisibleVertices;
        [FieldOffset(8)]
        public uint TransparentQuadLimit;
        [FieldOffset(12)]
        public uint TotalSubmittedVertices;
        [FieldOffset(16)]
        public float TilePressure;
        [FieldOffset(20)]
        public float FrustumSqueezeDegrees;
        [FieldOffset(24)]
        public float EstimatedVramMb;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TBDRShaderBudgetGlobalsDTO
    {
        [FieldOffset(0)]
        public float GlobalQualityWeight;
        [FieldOffset(4)]
        public float FrustumSqueezeDegrees;
        [FieldOffset(8)]
        public float TilePressure;
        [FieldOffset(12)]
        public float EstimatedVramMb;
        [FieldOffset(16)]
        public uint HardVertexCap;
        [FieldOffset(20)]
        public uint CurrentVisibleVertices;
        [FieldOffset(24)]
        public uint TransparentQuadLimit;
        [FieldOffset(28)]
        public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct TBDRIndirectDrawArgsDTO
    {
        [FieldOffset(0)]
        public uint VertexCountPerInstance;
        [FieldOffset(4)]
        public uint InstanceCount;
        [FieldOffset(8)]
        public uint StartVertex;
        [FieldOffset(12)]
        public uint StartInstance;
        [FieldOffset(16)]
        public uint StartIndex;
        [FieldOffset(20)]
        public uint _pad0;
        [FieldOffset(24)]
        public uint _pad1;
        [FieldOffset(28)]
        public uint _pad2;
    }

    public static class TBDRBufferIds
    {
        public const BufferID VertexBudgetCounters = (BufferID)70820;
        public const BufferID TileWarnings = (BufferID)70821;
        public const BufferID TransparentQuadCounters = (BufferID)70822;
        public const BufferID TelemetryRing = (BufferID)70823;
        public const BufferID MockVisibleInstances = (BufferID)70824;
        public const BufferID SortScratch = (BufferID)70825;
        public const BufferID MeshVertexCounts = (BufferID)70826;
        public const BufferID RadixHistogram = (BufferID)70827;
        public const BufferID VisibleCountOut = (BufferID)70828;
        public const BufferID MockQualitySignal = (BufferID)70829;
        public const BufferID MockCamera = (BufferID)70830;
        public const BufferID SourceFrustumPlanes = (BufferID)70831;
        public const BufferID SqueezedFrustumPlanes = (BufferID)70832;
        public const BufferID HzbVisibilityMask = (BufferID)70833;
        public const BufferID IndirectDrawArgs = (BufferID)70834;
    }

    internal static class TBDRByteFlags
    {
        public const byte False = 0;
        public const byte True = 1;

        public static byte FromBool(bool value)
        {
            return value ? True : False;
        }
    }

    internal static class TBDRVaultDescriptorRoutes
    {
        public static bool OpenOrAcquire<T>(
            IDataVault dataVault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpen(dataVault, ref handle, bufferId, requiredLength, out buffer))
                return true;

            if (dataVault == null || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            handle = dataVault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.GraphicsScalability,
                options);
            return TryOpen(dataVault, ref handle, bufferId, requiredLength, out buffer);
        }

        public static bool TryOpen<T>(
            IDataVault dataVault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (dataVault == null ||
                requiredLength <= 0 ||
                !IsMatching(in handle, bufferId) ||
                !dataVault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        public static bool IsMatching<T>(in VaultGenerationHandle<T> handle, BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)SystemID.GraphicsScalability &&
                   handle.Generation != 0u;
        }
    }

    public unsafe struct TBDRVertexBudgetVault : IDisposable
    {
        private const string NativeOwner = "SHINOBU_45_TBDR_PIPELINE";

        public NativeArray<TBDRVertexBudgetCounter64> VertexBudgetCounters;
        public NativeArray<TileSpillWarningDTO> TileWarnings;
        public NativeArray<int> TransparentQuadCount;
        public NativeArray<TBDRPipelineTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<TBDRVertexBudgetCounter64> VertexBudgetCountersHandle;
        public VaultGenerationHandle<TileSpillWarningDTO> TileWarningsHandle;
        public VaultGenerationHandle<int> TransparentQuadCountHandle;
        public VaultGenerationHandle<TBDRPipelineTelemetryEntry> TelemetryRingHandle;
        public int BudgetCount;
        public int WarningCount;
        public int TransparentCounterCount;
        public int TelemetryCapacity;
        public uint Generation;
        public byte UsesGlobalDataVaultFlag;

        public TBDRVertexBudgetVault(int budgetCount, int warningCount, int transparentCounterCount, int telemetryCapacity)
        {
            BudgetCount = math.max(1, budgetCount);
            WarningCount = math.max(1, warningCount);
            TransparentCounterCount = math.max(1, transparentCounterCount);
            TelemetryCapacity = math.max(1, telemetryCapacity);
            Generation = 1u;
            VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: NativeArray<TBDRVertexBudgetCounter64>[BudgetCount] - 64B budget caps - owner: SHINOBU_45_TBDR_PIPELINE
            TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TileSpillWarningDTO>[WarningCount] - tile-spill warning lane - owner: SHINOBU_45_TBDR_PIPELINE
            TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<int>[TransparentCounterCount] - transparent overdraw counters - owner: SHINOBU_45_TBDR_PIPELINE
            TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[TelemetryCapacity] - 300-frame black-box ring - owner: SHINOBU_45_TBDR_PIPELINE
            VertexBudgetCountersHandle = default;
            TileWarningsHandle = default;
            TransparentQuadCountHandle = default;
            TelemetryRingHandle = default;
            UsesGlobalDataVaultFlag = TBDRByteFlags.False;
            RegisterNativeArrays();
            Clear();
        }

        public TBDRVertexBudgetVault(IDataVault dataVault, int budgetCount, int warningCount, int transparentCounterCount, int telemetryCapacity)
        {
            BudgetCount = math.max(1, budgetCount);
            WarningCount = math.max(1, warningCount);
            TransparentCounterCount = math.max(1, transparentCounterCount);
            TelemetryCapacity = math.max(1, telemetryCapacity);
            Generation = 1u;
            VertexBudgetCounters = default;
            TileWarnings = default;
            TransparentQuadCount = default;
            TelemetryRing = default;
            VertexBudgetCountersHandle = default;
            TileWarningsHandle = default;
            TransparentQuadCountHandle = default;
            TelemetryRingHandle = default;
            UsesGlobalDataVaultFlag = TBDRByteFlags.FromBool(dataVault != null);

            if (UsesGlobalDataVaultFlag != 0)
            {
                bool acquired = TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref VertexBudgetCountersHandle, TBDRBufferIds.VertexBudgetCounters, BudgetCount, NativeArrayOptions.UninitializedMemory, out VertexBudgetCounters) &&
                                TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref TileWarningsHandle, TBDRBufferIds.TileWarnings, WarningCount, NativeArrayOptions.UninitializedMemory, out TileWarnings) &&
                                TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref TransparentQuadCountHandle, TBDRBufferIds.TransparentQuadCounters, TransparentCounterCount, NativeArrayOptions.UninitializedMemory, out TransparentQuadCount) &&
                                TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref TelemetryRingHandle, TBDRBufferIds.TelemetryRing, TelemetryCapacity, NativeArrayOptions.UninitializedMemory, out TelemetryRing);
                UsesGlobalDataVaultFlag = TBDRByteFlags.FromBool(acquired);
            }

            if (UsesGlobalDataVaultFlag == 0)
            {
                VertexBudgetCounters = new NativeArray<TBDRVertexBudgetCounter64>(BudgetCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                TileWarnings = new NativeArray<TileSpillWarningDTO>(WarningCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                TransparentQuadCount = new NativeArray<int>(TransparentCounterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                TelemetryRing = new NativeArray<TBDRPipelineTelemetryEntry>(TelemetryCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: CI/mock path only; production path uses GlobalDataVault
                RegisterNativeArrays();
            }

            Clear();
        }

        public bool IsCreated()
        {
            return VertexBudgetCounters.IsCreated &&
                   TileWarnings.IsCreated &&
                   TransparentQuadCount.IsCreated &&
                   TelemetryRing.IsCreated;
        }

        public ref VertexBudgetDTO BudgetRef(int index)
        {
            return ref TBDRVertexBudgetAccess.GetBudgetRef(VertexBudgetCounters, math.clamp(index, 0, BudgetCount - 1));
        }

        public ref TileSpillWarningDTO WarningRef(int index)
        {
            int safeIndex = math.clamp(index, 0, WarningCount - 1);
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(TileWarnings);
            byte* elementPtr = (byte*)basePtr + safeIndex * UnsafeUtility.SizeOf<TileSpillWarningDTO>();
            return ref UnsafeUtility.AsRef<TileSpillWarningDTO>(elementPtr);
        }

        public VertexBudgetDTO* BudgetPtr(int index)
        {
            return TBDRVertexBudgetAccess.GetBudgetPtr(VertexBudgetCounters, math.clamp(index, 0, BudgetCount - 1));
        }

        public TileSpillWarningDTO* WarningPtr(int index)
        {
            int safeIndex = math.clamp(index, 0, WarningCount - 1);
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(TileWarnings);
            return (TileSpillWarningDTO*)((byte*)basePtr + safeIndex * UnsafeUtility.SizeOf<TileSpillWarningDTO>());
        }

        public void ApplyHardLimits(in TBDRHardwareBudgetLimits limits)
        {
            if (!VertexBudgetCounters.IsCreated || VertexBudgetCounters.Length == 0)
                return;

            ref VertexBudgetDTO budget = ref BudgetRef(0);
            budget.MaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(limits.Quest3MaxVisibleVertices);
            budget.CurrentVisibleVertices = 0u;
            budget.TilePressure = 0f;
            budget._pad0 = 0u;

            if (TransparentQuadCount.IsCreated && TransparentQuadCount.Length > 0)
                TransparentQuadCount[0] = (int)math.max(1u, limits.TransparentQuadLimit);

            Generation++;
        }

        public void Clear()
        {
            if (VertexBudgetCounters.IsCreated)
            {
                for (int i = 0; i < VertexBudgetCounters.Length; i++)
                {
                    VertexBudgetCounters[i] = new TBDRVertexBudgetCounter64
                    {
                        Budget = new VertexBudgetDTO
                        {
                            MaxVisibleVertices = 800000u,
                            CurrentVisibleVertices = 0u,
                            TilePressure = 0f,
                            _pad0 = 0u
                        }
                    };
                }
            }

            if (TileWarnings.IsCreated)
            {
                for (int i = 0; i < TileWarnings.Length; i++)
                    TileWarnings[i] = default;
            }

            if (TransparentQuadCount.IsCreated)
            {
                for (int i = 0; i < TransparentQuadCount.Length; i++)
                    TransparentQuadCount[i] = 5000;
            }

            if (TelemetryRing.IsCreated)
            {
                for (int i = 0; i < TelemetryRing.Length; i++)
                    TelemetryRing[i] = default;
            }
        }

        public JobHandle Dispose(JobHandle dependency)
        {
            UnregisterNativeArrays();
            JobHandle handle = dependency;
            if (UsesGlobalDataVaultFlag == 0 && VertexBudgetCounters.IsCreated)
            {
                handle = VertexBudgetCounters.Dispose(handle);
                VertexBudgetCounters = default;
            }

            if (UsesGlobalDataVaultFlag == 0 && TileWarnings.IsCreated)
            {
                handle = TileWarnings.Dispose(handle);
                TileWarnings = default;
            }

            if (UsesGlobalDataVaultFlag == 0 && TransparentQuadCount.IsCreated)
            {
                handle = TransparentQuadCount.Dispose(handle);
                TransparentQuadCount = default;
            }

            if (UsesGlobalDataVaultFlag == 0 && TelemetryRing.IsCreated)
            {
                handle = TelemetryRing.Dispose(handle);
                TelemetryRing = default;
            }

            VertexBudgetCounters = default;
            TileWarnings = default;
            TransparentQuadCount = default;
            TelemetryRing = default;
            VertexBudgetCountersHandle = default;
            TileWarningsHandle = default;
            TransparentQuadCountHandle = default;
            TelemetryRingHandle = default;
            BudgetCount = 0;
            WarningCount = 0;
            TransparentCounterCount = 0;
            TelemetryCapacity = 0;
            UsesGlobalDataVaultFlag = TBDRByteFlags.False;
            Generation++;
            return handle;
        }

        public void Dispose()
        {
            UnregisterNativeArrays();
            if (UsesGlobalDataVaultFlag == 0 && VertexBudgetCounters.IsCreated)
                VertexBudgetCounters.Dispose();
            if (UsesGlobalDataVaultFlag == 0 && TileWarnings.IsCreated)
                TileWarnings.Dispose();
            if (UsesGlobalDataVaultFlag == 0 && TransparentQuadCount.IsCreated)
                TransparentQuadCount.Dispose();
            if (UsesGlobalDataVaultFlag == 0 && TelemetryRing.IsCreated)
                TelemetryRing.Dispose();

            VertexBudgetCounters = default;
            TileWarnings = default;
            TransparentQuadCount = default;
            TelemetryRing = default;
            VertexBudgetCountersHandle = default;
            TileWarningsHandle = default;
            TransparentQuadCountHandle = default;
            TelemetryRingHandle = default;
            BudgetCount = 0;
            WarningCount = 0;
            TransparentCounterCount = 0;
            TelemetryCapacity = 0;
            UsesGlobalDataVaultFlag = TBDRByteFlags.False;
            Generation++;
        }

        private void RegisterNativeArrays()
        {
            if (UsesGlobalDataVaultFlag != 0)
                return;

            NativeMemoryTrackingBridge.RegisterNativeArray(VertexBudgetCounters, NativeOwner, nameof(VertexBudgetCounters), NativeMemoryBridgeLifetime.Session);
            NativeMemoryTrackingBridge.RegisterNativeArray(TileWarnings, NativeOwner, nameof(TileWarnings), NativeMemoryBridgeLifetime.Session);
            NativeMemoryTrackingBridge.RegisterNativeArray(TransparentQuadCount, NativeOwner, nameof(TransparentQuadCount), NativeMemoryBridgeLifetime.Session);
            NativeMemoryTrackingBridge.RegisterNativeArray(TelemetryRing, NativeOwner, nameof(TelemetryRing), NativeMemoryBridgeLifetime.Session);
        }

        private void UnregisterNativeArrays()
        {
            if (UsesGlobalDataVaultFlag != 0)
                return;

            NativeMemoryTrackingBridge.UnregisterNativeArray(VertexBudgetCounters, NativeOwner, nameof(VertexBudgetCounters));
            NativeMemoryTrackingBridge.UnregisterNativeArray(TileWarnings, NativeOwner, nameof(TileWarnings));
            NativeMemoryTrackingBridge.UnregisterNativeArray(TransparentQuadCount, NativeOwner, nameof(TransparentQuadCount));
            NativeMemoryTrackingBridge.UnregisterNativeArray(TelemetryRing, NativeOwner, nameof(TelemetryRing));
        }
    }

    public static unsafe class TBDRVertexBudgetAccess
    {
        public static ref VertexBudgetDTO GetBudgetRef(NativeArray<TBDRVertexBudgetCounter64> budgets, int index)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(budgets);
            byte* elementPtr = (byte*)basePtr + index * UnsafeUtility.SizeOf<TBDRVertexBudgetCounter64>();
            ref TBDRVertexBudgetCounter64 counter = ref UnsafeUtility.AsRef<TBDRVertexBudgetCounter64>(elementPtr);
            return ref counter.Budget;
        }

        public static VertexBudgetDTO* GetBudgetPtr(NativeArray<TBDRVertexBudgetCounter64> budgets, int index)
        {
            void* basePtr = NativeArrayUnsafeUtility.GetUnsafePtr(budgets);
            byte* elementPtr = (byte*)basePtr + index * UnsafeUtility.SizeOf<TBDRVertexBudgetCounter64>();
            ref TBDRVertexBudgetCounter64 counter = ref UnsafeUtility.AsRef<TBDRVertexBudgetCounter64>(elementPtr);
            return (VertexBudgetDTO*)UnsafeUtility.AddressOf(ref counter.Budget);
        }

        public static uint AddVisibleVerticesAtomic(VertexBudgetDTO* budget, uint delta)
        {
            if (budget == null || delta == 0u)
                return budget != null ? budget->CurrentVisibleVertices : 0u;

            int updated = Interlocked.Add(
                ref UnsafeUtility.AsRef<int>(&budget->CurrentVisibleVertices),
                unchecked((int)delta));
            return unchecked((uint)updated);
        }
    }

    public static class TBDRLegacyBudgetArchaeology
    {
        private const string MobileVertexLimitsName = "mobile_vertex_limits.h8bin";
        private const string TextureStreamingBudgetsName = "texture_streaming_budgets.bin";

        public static TBDRHardwareBudgetLimits GenerateEmergencyMockLimits()
        {
            return new TBDRHardwareBudgetLimits
            {
                Quest3MaxVisibleVertices = 800000u,
                MobileLowMaxVisibleVertices = 600000u,
                SteamDeckMaxVisibleVertices = 1100000u,
                DesktopMaxVisibleVertices = 2500000u,
                TextureArrayBudgetMb = 512u,
                TransparentQuadLimit = 5000u,
                FrustumSqueezeDegrees = 12f,
                _pad0 = 0u
            };
        }

        public static bool TryLoadLegacyLimits(string docsArchiveRoot, string streamingAssetsRoot, out TBDRHardwareBudgetLimits limits)
        {
            limits = GenerateEmergencyMockLimits();

            try
            {
                if (TryLoadBinaryLimitFile(Path.Combine(docsArchiveRoot, MobileVertexLimitsName), ref limits) ||
                    TryLoadBinaryLimitFile(Path.Combine(streamingAssetsRoot, MobileVertexLimitsName), ref limits))
                {
                    TryLoadTextureBudgetFile(Path.Combine(docsArchiveRoot, TextureStreamingBudgetsName), ref limits);
                    TryLoadTextureBudgetFile(Path.Combine(streamingAssetsRoot, TextureStreamingBudgetsName), ref limits);
                    return true;
                }
            }
            catch (IOException)
            {
                limits = GenerateEmergencyMockLimits();
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                limits = GenerateEmergencyMockLimits();
                return false;
            }
            catch (ArgumentException)
            {
                limits = GenerateEmergencyMockLimits();
                return false;
            }

            return false;
        }

        private static bool TryLoadBinaryLimitFile(string path, ref TBDRHardwareBudgetLimits limits)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < 16L)
                    return false;

                if (!TryReadUInt32AutoEndian(stream, limits.Quest3MaxVisibleVertices, TBDRHardwareBudgetMath.MaxSafeVisibleVertices, out uint quest3) ||
                    !TryReadUInt32AutoEndian(stream, limits.MobileLowMaxVisibleVertices, TBDRHardwareBudgetMath.MaxSafeVisibleVertices, out uint mobileLow) ||
                    !TryReadUInt32AutoEndian(stream, limits.SteamDeckMaxVisibleVertices, TBDRHardwareBudgetMath.MaxSafeVisibleVertices, out uint steamDeck) ||
                    !TryReadUInt32AutoEndian(stream, limits.DesktopMaxVisibleVertices, TBDRHardwareBudgetMath.MaxSafeVisibleVertices, out uint desktop))
                {
                    return false;
                }

                limits.Quest3MaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(quest3);
                limits.MobileLowMaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(mobileLow);
                limits.SteamDeckMaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(steamDeck);
                limits.DesktopMaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(desktop);
                return true;
            }
        }

        private static bool TryLoadTextureBudgetFile(string path, ref TBDRHardwareBudgetLimits limits)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length < 8L)
                    return false;

                if (!TryReadUInt32AutoEndian(stream, limits.TextureArrayBudgetMb, 16384u, out uint textureMb) ||
                    !TryReadUInt32AutoEndian(stream, limits.TransparentQuadLimit, 500000u, out uint transparentQuads))
                {
                    return false;
                }

                limits.TextureArrayBudgetMb = math.max(1u, textureMb);
                limits.TransparentQuadLimit = math.max(1u, transparentQuads);
                return true;
            }
        }

        private static bool TryReadUInt32AutoEndian(FileStream stream, uint fallback, uint maxPlausible, out uint value)
        {
            Span<byte> bytes = stackalloc byte[4];
            int offset = 0;
            while (offset < 4)
            {
                int read = stream.Read(bytes.Slice(offset, 4 - offset));
                if (read <= 0)
                    break;
                offset += read;
            }

            if (offset != 4)
            {
                value = fallback;
                return false;
            }

            uint little = (uint)(bytes[0] |
                                 (bytes[1] << 8) |
                                 (bytes[2] << 16) |
                                 (bytes[3] << 24));
            uint swapped = ReverseByteOrder(little);
            bool littleOk = IsPlausibleBudget(little, maxPlausible);
            bool swappedOk = IsPlausibleBudget(swapped, maxPlausible);
            value = littleOk ? little : (swappedOk ? swapped : fallback);
            return littleOk || swappedOk;
        }

        private static bool IsPlausibleBudget(uint value, uint maxPlausible)
        {
            return value > 0u && value <= maxPlausible;
        }

        private static uint ReverseByteOrder(uint value)
        {
            return ((value & 0x000000FFu) << 24) |
                   ((value & 0x0000FF00u) << 8) |
                   ((value & 0x00FF0000u) >> 8) |
                   ((value & 0xFF000000u) >> 24);
        }
    }

    public static class TBDRHardwarePipelineSwitch
    {
        public static bool IsMobileTBDR()
        {
            RuntimePlatform platform = Application.platform;
            if (platform == RuntimePlatform.Android || platform == RuntimePlatform.IPhonePlayer || platform == RuntimePlatform.tvOS)
                return true;

            GraphicsDeviceType graphicsType = SystemInfo.graphicsDeviceType;
            if (SystemInfo.deviceType == DeviceType.Handheld)
                return true;

            if (graphicsType == GraphicsDeviceType.OpenGLES3)
                return true;

            string gpuName = SystemInfo.graphicsDeviceName;
            string model = SystemInfo.deviceModel;
            return ContainsOrdinal(gpuName, "Adreno") ||
                   ContainsOrdinal(gpuName, "Mali") ||
                   ContainsOrdinal(gpuName, "Apple") ||
                   ContainsOrdinal(model, "Quest") ||
                   ContainsOrdinal(model, "Android");
        }

        public static bool ShouldRunEarlyZRadixSort()
        {
            if (IsMobileTBDR())
                return true;

            string gpuName = SystemInfo.graphicsDeviceName;
            if (ContainsOrdinal(gpuName, "RTX") ||
                ContainsOrdinal(gpuName, "GeForce RTX") ||
                ContainsOrdinal(gpuName, "Radeon RX"))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsOrdinal(string source, string value)
        {
            return !string.IsNullOrEmpty(source) &&
                   source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public static class TBDRGlobalShaderBudgetBinder
    {
        private static readonly int BudgetVector0Id = Shader.PropertyToID("_H8_TBDR_Budget0");
        private static readonly int BudgetVector1Id = Shader.PropertyToID("_H8_TBDR_Budget1");
        private static readonly int QualityWeightId = Shader.PropertyToID("_H8_TBDR_GlobalQualityWeight");
        private static readonly int FrustumSqueezeId = Shader.PropertyToID("_H8_TBDR_FrustumSqueezeDegrees");
        private static readonly int TilePressureId = Shader.PropertyToID("_H8_TBDR_TilePressure");
        private static readonly int HardVertexCapId = Shader.PropertyToID("_H8_TBDR_HardVertexCap");
        private static readonly int CurrentVisibleVerticesId = Shader.PropertyToID("_H8_TBDR_CurrentVisibleVertices");
        private static readonly int TransparentQuadLimitId = Shader.PropertyToID("_H8_TBDR_TransparentQuadLimit");
        private static readonly int FlagsId = Shader.PropertyToID("_H8_TBDR_Flags");

        public static TBDRShaderBudgetGlobalsDTO BuildFromSnapshot(in TBDRTunerSnapshot snapshot, float qualityWeight)
        {
            return new TBDRShaderBudgetGlobalsDTO
            {
                GlobalQualityWeight = math.clamp(qualityWeight, 0f, 1f),
                FrustumSqueezeDegrees = math.clamp(snapshot.FrustumSqueezeDegrees, 0f, 15f),
                TilePressure = math.saturate(snapshot.TilePressure),
                EstimatedVramMb = math.max(0f, snapshot.EstimatedVramMb),
                HardVertexCap = snapshot.HardVertexCap,
                CurrentVisibleVertices = snapshot.CurrentVisibleVertices,
                TransparentQuadLimit = snapshot.TransparentQuadLimit,
                Flags = snapshot.Flags
            };
        }

        public static void Push(in TBDRShaderBudgetGlobalsDTO globals)
        {
            float qualityWeight = math.clamp(globals.GlobalQualityWeight, 0f, 1f);
            float tilePressure = math.saturate(globals.TilePressure);
            float frustumSqueeze = math.clamp(globals.FrustumSqueezeDegrees, 0f, 15f);
            float estimatedVramMb = math.max(0f, globals.EstimatedVramMb);

            Shader.SetGlobalVector(BudgetVector0Id, new Vector4(qualityWeight, tilePressure, frustumSqueeze, estimatedVramMb));
            Shader.SetGlobalVector(BudgetVector1Id, new Vector4(
                (float)math.min(globals.HardVertexCap, (uint)int.MaxValue),
                (float)math.min(globals.CurrentVisibleVertices, (uint)int.MaxValue),
                (float)math.min(globals.TransparentQuadLimit, (uint)int.MaxValue),
                (float)globals.Flags));
            Shader.SetGlobalFloat(QualityWeightId, qualityWeight);
            Shader.SetGlobalFloat(FrustumSqueezeId, frustumSqueeze);
            Shader.SetGlobalFloat(TilePressureId, tilePressure);
            Shader.SetGlobalInt(HardVertexCapId, (int)math.min(globals.HardVertexCap, (uint)int.MaxValue));
            Shader.SetGlobalInt(CurrentVisibleVerticesId, (int)math.min(globals.CurrentVisibleVertices, (uint)int.MaxValue));
            Shader.SetGlobalInt(TransparentQuadLimitId, (int)math.min(globals.TransparentQuadLimit, (uint)int.MaxValue));
            Shader.SetGlobalInt(FlagsId, (int)math.min(globals.Flags, (uint)int.MaxValue));
        }
    }

    public static class TBDRComputeDispatchLimiter
    {
        public static int HardwareMaxThreadsPerGroup;
        public static int ActiveMaxThreadsPerGroup;
        public static int LastKernelThreadsPerGroup;
        public static int LastDispatchGroupsX;
        public static int LastDispatchGroupsY;
        public static int LastDispatchGroupsZ;
        public static uint LastRejectCode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HardwareMaxThreadsPerGroup = 0;
            ActiveMaxThreadsPerGroup = 0;
            LastKernelThreadsPerGroup = 0;
            LastDispatchGroupsX = 0;
            LastDispatchGroupsY = 0;
            LastDispatchGroupsZ = 0;
            LastRejectCode = 0u;
        }

        public static void Boot()
        {
            HardwareMaxThreadsPerGroup = math.max(1, SystemInfo.maxComputeWorkGroupSize);
            ActiveMaxThreadsPerGroup = TBDRHardwarePipelineSwitch.IsMobileTBDR()
                ? math.min(256, HardwareMaxThreadsPerGroup)
                : math.min(1024, HardwareMaxThreadsPerGroup);
        }

        public static bool TryDispatch1D(ComputeShader shader, int kernel, int workItemCount)
        {
            return TryDispatch(shader, kernel, math.max(0, workItemCount), 1, 1);
        }

        public static bool TryDispatch(ComputeShader shader, int kernel, int workItemsX, int workItemsY, int workItemsZ)
        {
            if (shader == null || kernel < 0)
            {
                LastRejectCode = 1u;
                return false;
            }

            if (ActiveMaxThreadsPerGroup <= 0)
                Boot();

            shader.GetKernelThreadGroupSizes(kernel, out uint groupXRaw, out uint groupYRaw, out uint groupZRaw);
            int groupX = ToPositiveGroupSize(groupXRaw);
            int groupY = ToPositiveGroupSize(groupYRaw);
            int groupZ = ToPositiveGroupSize(groupZRaw);
            long threadsPerGroup = (long)groupX * groupY * groupZ;
            LastKernelThreadsPerGroup = threadsPerGroup > int.MaxValue ? int.MaxValue : (int)threadsPerGroup;
            if (threadsPerGroup <= 0L || threadsPerGroup > ActiveMaxThreadsPerGroup)
            {
                LastRejectCode = 2u;
                return false;
            }

            if (workItemsX <= 0 || workItemsY <= 0 || workItemsZ <= 0)
            {
                LastDispatchGroupsX = 0;
                LastDispatchGroupsY = 0;
                LastDispatchGroupsZ = 0;
                LastRejectCode = 3u;
                return false;
            }

            LastDispatchGroupsX = DivCeil(workItemsX, groupX);
            LastDispatchGroupsY = DivCeil(workItemsY, groupY);
            LastDispatchGroupsZ = DivCeil(workItemsZ, groupZ);
            shader.Dispatch(kernel, LastDispatchGroupsX, LastDispatchGroupsY, LastDispatchGroupsZ);
            LastRejectCode = 0u;
            return true;
        }

        private static int ToPositiveGroupSize(uint value)
        {
            if (value == 0u)
                return 1;

            return value > (uint)int.MaxValue ? int.MaxValue : (int)value;
        }

        private static int DivCeil(int value, int divisor)
        {
            if (value <= 0)
                return 0;

            int safeDivisor = math.max(1, divisor);
            return 1 + (value - 1) / safeDivisor;
        }
    }

    public sealed class TBDRTextureStreamingTracker : IDisposable
    {
        private const string NativeOwner = "SHINOBU_45_TEXTURE_STREAMING_TRACKER";

        public Texture2DArray TargetArray;
        public NativeArray<TextureStreamingSliceDTO> SliceTable;
        public VaultGenerationHandle<TextureStreamingSliceDTO> SliceTableHandle;
        public int SliceCapacity;
        public int MaxResidentMb;
        public uint ActiveBiomeHash;
        public uint Generation;
        public byte UsesGlobalDataVaultFlag;
        private int _nextSlice;

        public bool Configure(Texture2DArray targetArray, int sliceCapacity, int maxResidentMb)
        {
            return Configure(targetArray, sliceCapacity, maxResidentMb, null);
        }

        public bool Configure(Texture2DArray targetArray, int sliceCapacity, int maxResidentMb, IDataVault dataVault)
        {
            if (targetArray == null || sliceCapacity <= 0)
                return false;

            Dispose();
            TargetArray = targetArray;
            SliceCapacity = math.min(sliceCapacity, targetArray.depth);
            MaxResidentMb = math.max(1, maxResidentMb);
            UsesGlobalDataVaultFlag = TBDRByteFlags.FromBool(dataVault != null);
            if (UsesGlobalDataVaultFlag != 0)
            {
                UsesGlobalDataVaultFlag = TBDRByteFlags.FromBool(
                    TBDRVaultDescriptorRoutes.OpenOrAcquire(dataVault, ref SliceTableHandle, (BufferID)70835, SliceCapacity, NativeArrayOptions.UninitializedMemory, out SliceTable));
            }

            if (UsesGlobalDataVaultFlag == 0)
            {
                SliceTable = new NativeArray<TextureStreamingSliceDTO>(SliceCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC FALLBACK: fixed texture array residency table; production should pass GlobalDataVault
                NativeMemoryTrackingBridge.RegisterNativeArray(SliceTable, NativeOwner, nameof(SliceTable), NativeMemoryBridgeLifetime.Session);
            }

            for (int i = 0; i < SliceTable.Length; i++)
                SliceTable[i] = default;
            _nextSlice = 0;
            Generation++;
            return true;
        }

        public bool TryStageBiomeSlice(uint biomeHash, Texture sourceTexture, uint sourceSliceId, uint approxBytes, uint frame)
        {
            if (TargetArray == null || sourceTexture == null || !SliceTable.IsCreated || SliceCapacity <= 0)
                return false;

            ulong capBytes = (ulong)math.max(1, MaxResidentMb) * 1024UL * 1024UL;
            ulong incomingBytes = math.max(1u, approxBytes);
            if (incomingBytes > capBytes)
                return false;

            int slice = _nextSlice;
            _nextSlice++;
            if (_nextSlice >= SliceCapacity)
                _nextSlice = 0;

            TextureStreamingSliceDTO previous = SliceTable[slice];
            ulong previousBytes = (previous.ResidentFlags & 1u) != 0u ? previous.ApproxBytes : 0UL;
            ulong projectedBytes = EstimateResidentBytesUnclamped();
            projectedBytes = projectedBytes > previousBytes ? projectedBytes - previousBytes : 0UL;
            projectedBytes += incomingBytes;
            EvictResidentSlicesUntilUnderBudget(slice, capBytes, ref projectedBytes);
            if (projectedBytes > capBytes)
                return false;

            UnityEngine.Graphics.CopyTexture(sourceTexture, 0, 0, TargetArray, slice, 0);
            SliceTable[slice] = new TextureStreamingSliceDTO
            {
                BiomeHash = biomeHash,
                SliceId = sourceSliceId,
                LastTouchedFrame = frame,
                ResidentFlags = 1u,
                SourceWidth = (uint)math.max(1, sourceTexture.width),
                SourceHeight = (uint)math.max(1, sourceTexture.height),
                ApproxBytes = incomingBytes < (ulong)uint.MaxValue ? (uint)incomingBytes : uint.MaxValue,
                _pad0 = 0u
            };
            ActiveBiomeHash = biomeHash;
            Generation++;
            return true;
        }

        public uint EstimateResidentBytes()
        {
            if (!SliceTable.IsCreated)
                return 0u;

            ulong total = EstimateResidentBytesUnclamped();
            ulong capBytes = (ulong)math.max(1, MaxResidentMb) * 1024UL * 1024UL;
            ulong capped = total < capBytes ? total : capBytes;
            capped = capped < (ulong)uint.MaxValue ? capped : (ulong)uint.MaxValue;
            return (uint)capped;
        }

        private ulong EstimateResidentBytesUnclamped()
        {
            ulong total = 0UL;
            if (!SliceTable.IsCreated)
                return total;

            for (int i = 0; i < SliceTable.Length; i++)
            {
                TextureStreamingSliceDTO slice = SliceTable[i];
                if ((slice.ResidentFlags & 1u) != 0u)
                    total += slice.ApproxBytes;
            }

            return total;
        }

        private void EvictResidentSlicesUntilUnderBudget(int reservedSlice, ulong capBytes, ref ulong projectedBytes)
        {
            for (int eviction = 0; eviction < SliceTable.Length && projectedBytes > capBytes; eviction++)
            {
                int oldestIndex = -1;
                uint oldestFrame = uint.MaxValue;
                for (int i = 0; i < SliceTable.Length; i++)
                {
                    if (i == reservedSlice)
                        continue;

                    TextureStreamingSliceDTO candidate = SliceTable[i];
                    if ((candidate.ResidentFlags & 1u) == 0u)
                        continue;

                    if (oldestIndex < 0 || candidate.LastTouchedFrame < oldestFrame)
                    {
                        oldestIndex = i;
                        oldestFrame = candidate.LastTouchedFrame;
                    }
                }

                if (oldestIndex < 0)
                    return;

                TextureStreamingSliceDTO evicted = SliceTable[oldestIndex];
                evicted.ResidentFlags = 0u;
                SliceTable[oldestIndex] = evicted;
                projectedBytes = projectedBytes > evicted.ApproxBytes ? projectedBytes - evicted.ApproxBytes : 0UL;
            }
        }

        public void Dispose()
        {
            if (UsesGlobalDataVaultFlag == 0)
                NativeMemoryTrackingBridge.UnregisterNativeArray(SliceTable, NativeOwner, nameof(SliceTable));
            if (UsesGlobalDataVaultFlag == 0 && SliceTable.IsCreated)
                SliceTable.Dispose();
            SliceTable = default;
            SliceTableHandle = default;
            TargetArray = null;
            SliceCapacity = 0;
            MaxResidentMb = 0;
            ActiveBiomeHash = 0u;
            UsesGlobalDataVaultFlag = TBDRByteFlags.False;
            _nextSlice = 0;
            Generation++;
        }
    }

    public sealed class TBDRGpuBudgetCsvIngestor
    {
        private const int BufferCapacity = 4096;
        private readonly byte[] _fileBuffer = new byte[BufferCapacity]; // COLD ALLOC: byte[4096] - CSV override staging buffer - owner: TBDRGpuBudgetCsvIngestor
        private DateTime _lastWriteUtc;

        public uint LastParsedVertexCap;
        public int LastParsedTransparentQuadLimit;
        public float LastParsedFrustumSqueezeDegrees;
        public uint LastParseGeneration;
        public uint LastErrorCode;

        public bool Poll(string path, ref TBDRVertexBudgetVault vault)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !vault.IsCreated())
                return false;

            DateTime writeUtc = File.GetLastWriteTimeUtc(path);
            if (writeUtc <= _lastWriteUtc)
                return false;

            _lastWriteUtc = writeUtc;
            return TryRead(path, ref vault);
        }

        public bool TryRead(string path, ref TBDRVertexBudgetVault vault)
        {
            try
            {
                int length;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    length = stream.Read(_fileBuffer, 0, _fileBuffer.Length);
                }

                if (length <= 0)
                {
                    LastErrorCode = 1u;
                    return false;
                }

                if (!TryParseFirstDataLine(new ReadOnlySpan<byte>(_fileBuffer, 0, length), out uint vertexCap, out int transparentLimit, out float squeezeDegrees))
                {
                    LastErrorCode = 2u;
                    return false;
                }

                ref VertexBudgetDTO budget = ref vault.BudgetRef(0);
                budget.MaxVisibleVertices = TBDRHardwareBudgetMath.ClampVisibleVertexCap(vertexCap);
                budget.CurrentVisibleVertices = 0u;
                budget.TilePressure = 0f;

                if (vault.TransparentQuadCount.IsCreated && vault.TransparentQuadCount.Length > 0)
                    vault.TransparentQuadCount[0] = math.max(1, transparentLimit);

                LastParsedVertexCap = budget.MaxVisibleVertices;
                LastParsedTransparentQuadLimit = transparentLimit;
                LastParsedFrustumSqueezeDegrees = math.clamp(squeezeDegrees, 0f, 15f);
                LastParseGeneration++;
                LastErrorCode = 0u;
                return true;
            }
            catch (IOException)
            {
                LastErrorCode = 3u;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                LastErrorCode = 4u;
                return false;
            }
        }

        private static bool TryParseFirstDataLine(ReadOnlySpan<byte> bytes, out uint vertexCap, out int transparentLimit, out float squeezeDegrees)
        {
            vertexCap = 0u;
            transparentLimit = 0;
            squeezeDegrees = 0f;
            int lineStart = 0;
            while (lineStart < bytes.Length)
            {
                int lineEnd = lineStart;
                while (lineEnd < bytes.Length && bytes[lineEnd] != (byte)'\n' && bytes[lineEnd] != (byte)'\r')
                    lineEnd++;

                ReadOnlySpan<byte> line = bytes.Slice(lineStart, lineEnd - lineStart);
                if (IsDataLine(line) && TryParseBudgetLine(line, out vertexCap, out transparentLimit, out squeezeDegrees))
                    return true;

                lineStart = lineEnd + 1;
                while (lineStart < bytes.Length && (bytes[lineStart] == (byte)'\n' || bytes[lineStart] == (byte)'\r'))
                    lineStart++;
            }

            return false;
        }

        private static bool IsDataLine(ReadOnlySpan<byte> line)
        {
            for (int i = 0; i < line.Length; i++)
            {
                byte c = line[i];
                if (c == (byte)' ' || c == (byte)'\t')
                    continue;
                return c >= (byte)'0' && c <= (byte)'9';
            }

            return false;
        }

        private static bool TryParseBudgetLine(ReadOnlySpan<byte> line, out uint vertexCap, out int transparentLimit, out float squeezeDegrees)
        {
            vertexCap = 0u;
            transparentLimit = 0;
            squeezeDegrees = 0f;
            int cursor = 0;
            if (!TryParseUInt(line, ref cursor, out vertexCap))
                return false;
            if (!TryParseInt(line, ref cursor, out transparentLimit))
                return false;
            if (!TryParseFloat(line, ref cursor, out squeezeDegrees))
                return false;

            vertexCap = TBDRHardwareBudgetMath.ClampVisibleVertexCap(vertexCap);
            transparentLimit = math.max(1, transparentLimit);
            squeezeDegrees = math.clamp(squeezeDegrees, 0f, 15f);
            return true;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> line, ref int cursor, out uint value)
        {
            value = 0u;
            SkipSeparators(line, ref cursor);
            bool found = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                found = true;
                uint digit = (uint)(c - (byte)'0');
                value = value > (uint.MaxValue - digit) / 10u
                    ? uint.MaxValue
                    : value * 10u + digit;
                cursor++;
            }

            return found;
        }

        private static bool TryParseInt(ReadOnlySpan<byte> line, ref int cursor, out int value)
        {
            bool parsed = TryParseUInt(line, ref cursor, out uint raw);
            value = (int)math.min(raw, (uint)int.MaxValue);
            return parsed;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            SkipSeparators(line, ref cursor);
            bool found = false;
            float scale = 1f;
            bool decimalMode = false;
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c == (byte)'.')
                {
                    if (decimalMode)
                        break;
                    decimalMode = true;
                    cursor++;
                    continue;
                }

                if (c < (byte)'0' || c > (byte)'9')
                    break;

                found = true;
                int digit = c - (byte)'0';
                if (decimalMode)
                {
                    scale *= 0.1f;
                    value += digit * scale;
                }
                else
                {
                    value = value * 10f + digit;
                }

                cursor++;
            }

            return found && math.isfinite(value);
        }

        private static void SkipSeparators(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length)
            {
                byte c = line[cursor];
                if (c == (byte)',' || c == (byte)';' || c == (byte)' ' || c == (byte)'\t')
                {
                    cursor++;
                    continue;
                }

                break;
            }
        }
    }

    public sealed class TBDRPipelineTelemetryRecorder : IDisposable
    {
        private const int RingCapacity = 300;
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_TBDR_PIPELINE.bin";
        private const string NativeOwner = "SHINOBU_45_TBDR_TELEMETRY";

        public NativeArray<TBDRPipelineTelemetryEntry> Ring;
        public int WriteIndex;
        public byte DumpedFlag;
        public byte UsesExternalRingFlag;

        public void BindExternalRing(NativeArray<TBDRPipelineTelemetryEntry> ring)
        {
            if (!ring.IsCreated || ring.Length < RingCapacity)
                return;

            if (Ring.IsCreated && UsesExternalRingFlag == 0)
            {
                NativeMemoryTrackingBridge.UnregisterNativeArray(Ring, NativeOwner, nameof(Ring));
                Ring.Dispose();
            }

            Ring = ring;
            UsesExternalRingFlag = TBDRByteFlags.True;
            WriteIndex = math.clamp(WriteIndex, 0, RingCapacity - 1);
            DumpedFlag = TBDRByteFlags.False;
            for (int i = 0; i < RingCapacity; i++)
                Ring[i] = default;
        }

        public void EnsureCreated()
        {
            if (Ring.IsCreated)
                return;

            Ring = new NativeArray<TBDRPipelineTelemetryEntry>(RingCapacity, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<TBDRPipelineTelemetryEntry>[300] - TBDR black-box ring - owner: SHINOBU_45_TBDR_TELEMETRY
            NativeMemoryTrackingBridge.RegisterNativeArray(Ring, NativeOwner, nameof(Ring), NativeMemoryBridgeLifetime.Session);
            UsesExternalRingFlag = TBDRByteFlags.False;
            for (int i = 0; i < Ring.Length; i++)
                Ring[i] = default;
        }

        public void Record(uint frame, uint submittedVertices, uint maxVertices, uint warnings, float sortMs, float tilePressure, uint flags)
        {
            EnsureCreated();
            uint hash = 2166136261u;
            hash = Mix(hash, frame);
            hash = Mix(hash, submittedVertices);
            hash = Mix(hash, maxVertices);
            hash = Mix(hash, warnings);
            Ring[WriteIndex] = new TBDRPipelineTelemetryEntry
            {
                Frame = frame,
                TotalSubmittedVertices = submittedVertices,
                MaxVisibleVertices = maxVertices,
                TileSpillWarnings = warnings,
                SortComputeTimeMs = math.isfinite(sortMs) ? sortMs : 0f,
                TilePressure = math.isfinite(tilePressure) ? tilePressure : 0f,
                Flags = flags,
                StateHash = hash
            };

            WriteIndex++;
            if (WriteIndex >= RingCapacity)
                WriteIndex = 0;

            if (submittedVertices > maxVertices && DumpedFlag == 0)
                Dump();
        }

        public void Dump()
        {
            if (!Ring.IsCreated || DumpedFlag != 0)
                return;

            DumpedFlag = TBDRByteFlags.True;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpRelativePath));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(RingCapacity);
                writer.Write(WriteIndex);
                for (int i = 0; i < Ring.Length; i++)
                {
                    TBDRPipelineTelemetryEntry entry = Ring[i];
                    writer.Write(entry.Frame);
                    writer.Write(entry.TotalSubmittedVertices);
                    writer.Write(entry.MaxVisibleVertices);
                    writer.Write(entry.TileSpillWarnings);
                    writer.Write(entry.SortComputeTimeMs);
                    writer.Write(entry.TilePressure);
                    writer.Write(entry.Flags);
                    writer.Write(entry.StateHash);
                }
            }
        }

        public void Dispose()
        {
            if (UsesExternalRingFlag == 0)
                NativeMemoryTrackingBridge.UnregisterNativeArray(Ring, NativeOwner, nameof(Ring));
            if (UsesExternalRingFlag == 0 && Ring.IsCreated)
                Ring.Dispose();
            Ring = default;
            WriteIndex = 0;
            DumpedFlag = TBDRByteFlags.False;
            UsesExternalRingFlag = TBDRByteFlags.False;
        }

        private static uint Mix(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }
    }

    public static class TBDRUmaRawBufferWriter
    {
        public static GraphicsBuffer CreateRawMatrixBuffer(int matrixCapacity)
        {
            int capacity = math.max(1, matrixCapacity);
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Raw,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                capacity,
                UnsafeUtility.SizeOf<float4x4>());
        }

        public static JobHandle SchedulePopulateLockedMatrices(
            GraphicsBuffer buffer,
            NativeArray<PoiTransformDTO> source,
            int count,
            JobHandle dependency,
            out NativeArray<float4x4> lockedMatrices)
        {
            int safeCount = buffer != null && source.IsCreated
                ? math.min(math.min(count, source.Length), buffer.count)
                : 0;

            if (safeCount <= 0)
            {
                lockedMatrices = default;
                return dependency;
            }

            lockedMatrices = buffer.LockBufferForWrite<float4x4>(0, safeCount);
            return new PopulateLockedMatrixBufferJob
            {
                Source = source,
                Destination = lockedMatrices
            }.Schedule(safeCount, 64, dependency);
        }

        public static void UnlockAfterWrite(GraphicsBuffer buffer, int count)
        {
            if (buffer == null || count <= 0)
                return;

            buffer.UnlockBufferAfterWrite<float4x4>(math.min(count, buffer.count));
        }
    }
}
