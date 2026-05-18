using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Mathematics;

namespace Hecton8.Global.Contracts
{
    /// <summary>
    /// Compile-wall layer classification used by the assembly guard.
    /// </summary>
    public enum AssemblyLayer : byte
    {
        Unknown = 0,
        Contracts = 1,
        Runtime = 2,
        Authoring = 3,
        Editor = 4,
        Tests = 5
    }

    /// <summary>
    /// Fixed bootstrap phase contract. Implementations are sorted by this coarse lane before dependency ids.
    /// </summary>
    public enum BootstrapPhase : byte
    {
        Contracts = 0,
        Core = 16,
        Simulation = 32,
        Presentation = 48,
        Authoring = 64
    }

    /// <summary>
    /// Compile-time quality route. Do not branch to high-end work from low-tier assemblies.
    /// </summary>
    public enum HardwareQualityRoute : byte
    {
        Low = 0,
        Middle = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Fixed-size payload for cross-assembly SignalBus traffic. Payload body is 112 bytes; total size is 128 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128, Pack = 8)]
    public struct GlobalSignalPayload
    {
        [FieldOffset(0)] public uint TypeHash;
        [FieldOffset(4)] public ushort Version;
        [FieldOffset(6)] public ushort PayloadBytes;
        [FieldOffset(8)] public uint FrameIndex;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public ulong Payload00;
        [FieldOffset(24)] public ulong Payload01;
        [FieldOffset(32)] public ulong Payload02;
        [FieldOffset(40)] public ulong Payload03;
        [FieldOffset(48)] public ulong Payload04;
        [FieldOffset(56)] public ulong Payload05;
        [FieldOffset(64)] public ulong Payload06;
        [FieldOffset(72)] public ulong Payload07;
        [FieldOffset(80)] public ulong Payload08;
        [FieldOffset(88)] public ulong Payload09;
        [FieldOffset(96)] public ulong Payload10;
        [FieldOffset(104)] public ulong Payload11;
        [FieldOffset(112)] public ulong Payload12;
        [FieldOffset(120)] public ulong Payload13;
    }

    /// <summary>
    /// Raw DataVault slice handle passed across assembly boundaries. Ownership stays with the allocating assembly.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
    public struct GlobalNativeBufferHandle
    {
        [FieldOffset(0)] public IntPtr Pointer;
        [FieldOffset(8)] public int Length;
        [FieldOffset(12)] public int StrideBytes;
        [FieldOffset(16)] public uint OwnerHash;
        [FieldOffset(20)] public uint Generation;
        [FieldOffset(24)] public uint AccessFlags;
        [FieldOffset(28)] public uint Reserved;
    }

    /// <summary>
    /// Raw Burst function pointer slot for non-generic registries and CSV-driven mock swaps.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
    public struct GlobalFunctionPointerHandle
    {
        [FieldOffset(0)] public IntPtr Pointer;
        [FieldOffset(8)] public uint FunctionHash;
        [FieldOffset(12)] public uint ContractHash;
        [FieldOffset(16)] public uint Version;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved;
    }

    /// <summary>
    /// Writable alias contract. A non-zero WriterSystemHash grants exclusive write ownership for the generation.
    /// </summary>
    [NoAlias]
    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 8)]
    public struct NativeMemoryAliasContract
    {
        [FieldOffset(0)] public GlobalNativeBufferHandle ReadOnlyFront;
        [FieldOffset(32)] public uint WriterSystemHash;
        [FieldOffset(36)] public uint ReaderMaskLow;
        [FieldOffset(40)] public uint ReaderMaskHigh;
        [FieldOffset(44)] public uint Generation;
        [FieldOffset(48)] public ulong ByteRangeStart;
        [FieldOffset(56)] public ulong ByteRangeLength;
    }

    /// <summary>
    /// Compile-time offset record emitted by the vault offset generator.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 8)]
    public struct VaultOffsetRecord
    {
        [FieldOffset(0)] public uint TypeHash;
        [FieldOffset(4)] public uint FieldHash;
        [FieldOffset(8)] public int OffsetBytes;
        [FieldOffset(12)] public int SizeBytes;
        [FieldOffset(16)] public int StrideBytes;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public ulong Reserved;
    }

    /// <summary>
    /// CSV-controlled implementation route. Used for mock injection without C# source edits.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 8)]
    public struct AssemblyRoutingOverride
    {
        [FieldOffset(0)] public uint ContractHash;
        [FieldOffset(4)] public uint ImplementationHash;
        [FieldOffset(8)] public uint MockImplementationHash;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public HardwareQualityRoute MinQuality;
        [FieldOffset(17)] public HardwareQualityRoute MaxQuality;
        [FieldOffset(18)] public ushort Priority;
        [FieldOffset(20)] public float MinQualityWeight;
        [FieldOffset(24)] public float MaxQualityWeight;
        [FieldOffset(28)] public uint QualityCurveHash;
        [FieldOffset(32)] public ulong Reserved2;
        [FieldOffset(40)] public ulong Reserved3;
        [FieldOffset(48)] public ulong Reserved4;
        [FieldOffset(56)] public ulong Reserved5;
    }

    /// <summary>
    /// Bootstrap registration context passed by the deterministic bootstrapper.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80, Pack = 8)]
    public struct BootstrapRegistryContext
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint NodeCount;
        [FieldOffset(8)] public GlobalNativeBufferHandle BufferTable;
        [FieldOffset(40)] public GlobalFunctionPointerHandle RegistryTable;
        [FieldOffset(72)] public ulong Reserved;
    }

    /// <summary>
    /// Dependency injection snapshot passed after registration. Runtime nodes cache data from this struct.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 80, Pack = 8)]
    public struct BootstrapDependencySnapshot
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public uint Generation;
        [FieldOffset(8)] public GlobalNativeBufferHandle ServiceTable;
        [FieldOffset(40)] public GlobalNativeBufferHandle SignalTable;
        [FieldOffset(72)] public ulong Reserved;
    }

    /// <summary>
    /// Deterministic bootstrap node. Leaf assemblies implement this; Core sorts and invokes it.
    /// </summary>
    public interface IBootstrapNode
    {
        uint GetNodeId();
        BootstrapPhase GetPhase();
        int GetDependencyCount();
        bool TryGetDependencyId(int index, out uint dependencyId);
        void OnRegister(ref BootstrapRegistryContext context);
        void OnDependencyInject(ref BootstrapDependencySnapshot snapshot);
        void ResetStaticState();
    }

    /// <summary>
    /// Static reset node for no-domain-reload Play Mode. Implementations must clear static fields and native handles.
    /// </summary>
    public interface IStaticResetNode
    {
        uint GetResetNodeId();
        void ResetStaticState();
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void PhysicsApplyForceDelegate(GlobalNativeBufferHandle bodyBuffer, int bodyIndex, float3 force, float deltaTime);

    /// <summary>
    /// Contract facade for synchronous physics calls without a concrete physics assembly reference.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public readonly struct PhysicsFacade
    {
        public readonly FunctionPointer<PhysicsApplyForceDelegate> ApplyForceFunction;
        public readonly GlobalNativeBufferHandle BodyBuffer;

        public PhysicsFacade(FunctionPointer<PhysicsApplyForceDelegate> applyForceFunction, GlobalNativeBufferHandle bodyBuffer)
        {
            ApplyForceFunction = applyForceFunction;
            BodyBuffer = bodyBuffer;
        }

        public void ApplyForce(int bodyIndex, float3 force, float deltaTime)
        {
            if (ApplyForceFunction.IsCreated)
                ApplyForceFunction.Invoke(BodyBuffer, bodyIndex, force, deltaTime);
        }
    }
}
