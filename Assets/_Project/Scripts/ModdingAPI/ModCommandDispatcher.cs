using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#pragma warning disable CS0618

namespace Hecton8.Modding
{
    /// <summary>
    /// Engine-owned command opcodes accepted from sandboxed mods.
    /// </summary>
    public enum ModCommandOpcode : ushort
    {
        /// <summary>No command.</summary>
        None = 0,

        /// <summary>Requests engine-owned debris spawn logic.</summary>
        SpawnDebris = 1,

        /// <summary>Requests engine-owned thermal application logic.</summary>
        ApplyHeat = 2,

        /// <summary>Requests an engine-scheduled raycast query.</summary>
        RaycastQuery = 3,

        /// <summary>Requests frame-space effect placement.</summary>
        SpawnEffect = 4,

        /// <summary>Requests frame-space entity movement.</summary>
        MoveEntity = 5,

        /// <summary>Requests a protected voxel SDF add/subtract operation.</summary>
        VoxelModify = 6,

        /// <summary>Requests an asynchronous abyssal flow vector sample.</summary>
        FlowQuery = 7,

        /// <summary>Requests an engine-owned acoustic ping emission.</summary>
        AcousticPing = 8
    }

    /// <summary>
    /// Engine subsystem targets used by the mod command security gate.
    /// </summary>
    public enum ModCommandTargetSystem : ushort
    {
        /// <summary>No target.</summary>
        None = 0,

        /// <summary>World simulation target.</summary>
        World = 1,

        /// <summary>Thermal simulation target.</summary>
        Thermal = 2,

        /// <summary>Voxel simulation target.</summary>
        Voxel = 3,

        /// <summary>Physics query target.</summary>
        Physics = 4,

        /// <summary>VFX/effects target.</summary>
        Effects = 5,

        /// <summary>Audio/sensory target.</summary>
        Audio = 6,

        /// <summary>Environmental simulation target.</summary>
        Environment = 7
    }

    /// <summary>
    /// Bit flags stored in <see cref="ModCommand.Flags"/>.
    /// </summary>
    [System.Flags]
    public enum ModCommandFlags : ushort
    {
        /// <summary>No flags.</summary>
        None = 0,

        /// <summary>Command may execute after the request frame.</summary>
        Deferred = 1 << 0,

        /// <summary>Security gate accepted the command.</summary>
        Validated = 1 << 1,

        /// <summary>Command originated from the sandboxed mod facade.</summary>
        Sandboxed = 1 << 2,

        /// <summary>Command was rebased from AUP into frame-space by the engine.</summary>
        AupRebased = 1 << 3
    }

    /// <summary>
    /// Fixed-size mod command packet.
    /// Header: 8 bytes. Payload: seven 64-bit words = 56 bytes. Total: 64 bytes.
    /// Payload0 packs ModHash in bits 0..31 and RequestId in bits 32..63.
    /// </summary>
    [System.Obsolete("Legacy ModCommand lane is quarantined. Use FutureCommandEnvelope through HectonAPI.Commands.RequestFuture.", false)]
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct ModCommand
    {
        /// <summary>16-bit command opcode.</summary>
        [FieldOffset(0)]
        public ushort Opcode;

        /// <summary>16-bit target system identifier.</summary>
        [FieldOffset(2)]
        public ushort TargetSystem;

        /// <summary>16-bit command flags.</summary>
        [FieldOffset(4)]
        public ushort Flags;

        /// <summary>16-bit mod API version captured at enqueue time.</summary>
        [FieldOffset(6)]
        public ushort ApiVersion;

        /// <summary>Payload word 0. Low 32 bits = mod hash. High 32 bits = request id.</summary>
        [FieldOffset(8)]
        public ulong Payload0;

        /// <summary>Stable hash of the mod that requested this command.</summary>
        [FieldOffset(8)]
        public uint ModHash;

        /// <summary>Mod-local request identifier.</summary>
        [FieldOffset(12)]
        public uint RequestId;

        /// <summary>Payload word 1.</summary>
        [FieldOffset(16)]
        public ulong Payload1;

        /// <summary>Payload word 2.</summary>
        [FieldOffset(24)]
        public ulong Payload2;

        /// <summary>Payload word 3.</summary>
        [FieldOffset(32)]
        public ulong Payload3;

        /// <summary>Payload word 4.</summary>
        [FieldOffset(40)]
        public ulong Payload4;

        /// <summary>Payload word 5.</summary>
        [FieldOffset(48)]
        public ulong Payload5;

        /// <summary>Payload word 6.</summary>
        [FieldOffset(56)]
        public ulong Payload6;
    }

    /// <summary>
    /// Engine-side executor for a validated mod command.
    /// </summary>
    [System.Obsolete("Legacy managed mod command kernels are quarantined. Future command execution uses Burst jobs and SignalBus lanes.", false)]
    internal interface IModCommandKernel
    {
        /// <summary>
        /// Executes a security-validated mod command.
        /// </summary>
        /// <param name="command">Validated command payload.</param>
        /// <returns>True when the command was accepted by the owning subsystem.</returns>
        bool Execute(in ModCommand command);
    }

    internal enum ModCommandRejectReason : uint
    {
        None = 0,
        QueueFull = 1,
        UnknownMod = 2,
        QuarantinedMod = 3,
        InvalidOpcode = 4,
        InvalidTarget = 5,
        MissingKernel = 6,
        AupRequired = 7,
        OriginShiftActive = 8,
        RaycastLaneFull = 9,
        CommandFlood = 10,
        SpawnConflict = 11,
        RenderCapacityExceeded = 12,
        HeapQuotaExceeded = 13,
        ProtectedCoreSector = 14,
        VoxelUnavailable = 15,
        FlowUnavailable = 16,
        AcousticUnavailable = 17,
        InvalidPayload = 18
    }

    internal static class ModCommandDispatcher
    {
        private const int CommandCapacity = 4096;
        private const int MaxDrainPerLateFrame = 256;
        private const int KernelCapacity = 32;
        private const int ModCapacity = 32;
        private const int MaxCommandsPerModPerTick = 128;
        private const int MaxModRaycasts = 128;
        private const int MaxModRenderInstancesPerFrame = 1024;
        private const int MaxRejectEventsPerLateFrame = MaxDrainPerLateFrame;
        private const int MaxAupResponsesPerLateFrame = MaxDrainPerLateFrame;
        private const int MaxMemoryEvictionEventsPerLateFrame = ModCapacity;
        private const int CurrentApiVersion = ModLoader.CurrentAPIVersion;
        private const ushort FutureKernelReservedOpcodeMin = 0x7800;
        private const ushort FutureKernelReservedOpcodeMax = 0x78FF;
        private const ushort FutureKernelReservedTargetMin = 0x7800;
        private const ushort FutureKernelReservedTargetMax = 0x78FF;
        private const uint MemorySentinelModMaskLaneHash = 0x4D4D534Bu; // MMSK
        private const uint MemorySentinelModMaskSourceHash = 0x53483738u; // SH78
        private static readonly bool LegacyCommandSurfaceEnabled = false;
        private const int AupCellSizeMeters = HectonPhysicsContract.AupSectorSizeMetersInt;
        private const double SpawnConflictEpsilonSq = 0.25d;
        private const long ModHeapQuotaBytes = 16L * 1024L * 1024L;
        private const long ModHeapFrameQuotaBytes = 1L * 1024L * 1024L;
        private const float MaxModVoxelModifyRadiusMeters = 8f;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;

        private const byte ModStateActive = 1;
        private const byte ModStateQuarantined = 2;

        private struct ModCommandModState
        {
            public int ApiVersion;
            public int LastCommandFrame;
            public int CommandsThisFrame;
            public int Priority;
            public long TrackedHeapBytes;
            public long FrameHeapBytes;
            public int LastHeapFrame;
            public byte State;
            public byte Reserved0;
            public ushort Reserved1;
        }

#pragma warning disable 0649
        private struct ModRaycastRequestRecord
        {
            public uint ModHash;
            public uint RequestId;
            public byte IsActive;
            public byte Reserved0;
            public ushort Reserved1;
        }
#pragma warning restore 0649

        private struct AupExecutionCandidate
        {
            public ModCommand Command;
            public long3 Grid;
            public float3 Local;
            public int Priority;
            public byte Accepted;
        }

        // COLD ALLOC: IModCommandKernel[32] - engine command executors indexed by NativeHashMap<uint,int> - owner: ModCommandDispatcher
        private static readonly IModCommandKernel[] _kernels = new IModCommandKernel[KernelCapacity];
        // COLD ALLOC: string[32] - reverse lookup for numeric mod eviction and diagnostics - owner: ModCommandDispatcher
        private static readonly string[] _modIdsByIndex = new string[ModCapacity];
        // COLD ALLOC: uint[32] - stable mod hashes aligned to _modIdsByIndex - owner: ModCommandDispatcher
        private static readonly uint[] _modHashesByIndex = new uint[ModCapacity];
        // COLD ALLOC: ModRaycastRequestRecord[128] - pending dispatcher raycast owner records - owner: ModCommandDispatcher
        private static readonly ModRaycastRequestRecord[] _raycastRequestRecords = new ModRaycastRequestRecord[MaxModRaycasts];
        // COLD ALLOC: AupExecutionCandidate[256] - current late-frame AUP arbitration buffer - owner: ModCommandDispatcher
        private static readonly AupExecutionCandidate[] _aupCandidates = new AupExecutionCandidate[MaxDrainPerLateFrame];
        // COLD ALLOC: ModRaycastReceiver[1] - dispatcher raycast callback bridge - owner: ModCommandDispatcher
        private static readonly ModRaycastReceiver _raycastReceiver = new ModRaycastReceiver();

        private static NativeQueue<ModCommand> _pendingCommands;
        private static NativeQueue<ModAupCommand> _pendingAupCommands;
        private static NativeQueue<ModRenderInstanceCommand> _pendingRenderCommands;
        private static NativeQueue<ModRaycastResultPayload> _pendingRaycastResults;
        private static NativeQueue<ModInteractionRejectedPayload> _pendingRejectEvents;
        private static NativeQueue<ModCriticalMemoryEvictionPayload> _pendingMemoryEvictionEvents;
        private static NativeQueue<ModAupResponse> _pendingAupResponses;
        private static NativeHashMap<uint, ModCommandModState> _modStatesByHash;
        private static NativeHashMap<uint, int> _modIndexByHash;
        private static NativeHashMap<uint, int> _kernelIndexByCommandKey;
        private static int _queuedCommandCount;
        private static int _queuedAupCommandCount;
        private static int _queuedRenderCommandCount;
        private static int _queuedRaycastResultCount;
        private static int _queuedRejectEventCount;
        private static int _queuedMemoryEvictionEventCount;
        private static int _queuedAupResponseCount;
        private static int _kernelCount;
        private static int _modCount;
        private static bool _modMaskSignalConfigured;
        private static IAbyssalFlowGpuReadModel _abyssalFlowGpu;
        private static IAudioService _audioService;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Initialize()
        {
            FutureCommandSandboxValidator.Initialize();
            if (!LegacyCommandSurfaceEnabled)
                return;

            if (!_pendingCommands.IsCreated)
            {
                _pendingCommands = new NativeQueue<ModCommand>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModCommand>[4096] - sandboxed mod command ring buffer - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingCommands, CommandCapacity, nameof(_pendingCommands));
            }

            if (!_pendingAupCommands.IsCreated)
            {
                _pendingAupCommands = new NativeQueue<ModAupCommand>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModAupCommand>[4096] - AUP-stable mod command ring buffer - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingAupCommands, CommandCapacity, nameof(_pendingAupCommands));
            }

            if (!_pendingRenderCommands.IsCreated)
            {
                _pendingRenderCommands = new NativeQueue<ModRenderInstanceCommand>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRenderInstanceCommand>[1024] - mod instancing request lane - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingRenderCommands, MaxModRenderInstancesPerFrame, nameof(_pendingRenderCommands));
            }

            if (!_pendingRaycastResults.IsCreated)
            {
                _pendingRaycastResults = new NativeQueue<ModRaycastResultPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModRaycastResultPayload>[128] - next-frame mod raycast callback lane - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingRaycastResults, MaxModRaycasts, nameof(_pendingRaycastResults));
            }

            if (!_pendingRejectEvents.IsCreated)
            {
                _pendingRejectEvents = new NativeQueue<ModInteractionRejectedPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModInteractionRejectedPayload>[256] - unmanaged mod rejection event lane - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingRejectEvents, MaxDrainPerLateFrame, nameof(_pendingRejectEvents));
            }

            if (!_pendingMemoryEvictionEvents.IsCreated)
            {
                _pendingMemoryEvictionEvents = new NativeQueue<ModCriticalMemoryEvictionPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModCriticalMemoryEvictionPayload>[32] - unmanaged mod memory eviction event lane - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingMemoryEvictionEvents, ModCapacity, nameof(_pendingMemoryEvictionEvents));
            }

            if (!_pendingAupResponses.IsCreated)
            {
                _pendingAupResponses = new NativeQueue<ModAupResponse>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ModAupResponse>[256] - unmanaged mod AUP response event lane - owner: ModCommandDispatcher
                RegisterQueue(ref _pendingAupResponses, MaxDrainPerLateFrame, nameof(_pendingAupResponses));
            }

            if (!_modStatesByHash.IsCreated)
            {
                _modStatesByHash = new NativeHashMap<uint, ModCommandModState>(ModCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeHashMap<uint,ModCommandModState>[32] - O(1) mod command security lookup - owner: ModCommandDispatcher
                NativeMemorySentinel.RegisterNativeHashMap(_modStatesByHash, nameof(ModCommandDispatcher), nameof(_modStatesByHash), NativeAllocationLifetime.Session);
            }

            if (!_modIndexByHash.IsCreated)
            {
                _modIndexByHash = new NativeHashMap<uint, int>(ModCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeHashMap<uint,int>[32] - O(1) mod hash reverse-index lookup - owner: ModCommandDispatcher
                NativeMemorySentinel.RegisterNativeHashMap(_modIndexByHash, nameof(ModCommandDispatcher), nameof(_modIndexByHash), NativeAllocationLifetime.Session);
            }

            if (!_kernelIndexByCommandKey.IsCreated)
            {
                _kernelIndexByCommandKey = new NativeHashMap<uint, int>(KernelCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeHashMap<uint,int>[32] - O(1) command kernel lookup - owner: ModCommandDispatcher
                NativeMemorySentinel.RegisterNativeHashMap(_kernelIndexByCommandKey, nameof(ModCommandDispatcher), nameof(_kernelIndexByCommandKey), NativeAllocationLifetime.Session);
            }

        }

        internal static void Shutdown()
        {
            bool notifyShutdown = _modCount != 0 || _modMaskSignalConfigured;

            FutureCommandSandboxValidator.Shutdown();

            DisposeQueue(ref _pendingCommands, nameof(_pendingCommands));
            DisposeQueue(ref _pendingAupCommands, nameof(_pendingAupCommands));
            DisposeQueue(ref _pendingRenderCommands, nameof(_pendingRenderCommands));
            DisposeQueue(ref _pendingRaycastResults, nameof(_pendingRaycastResults));
            DisposeQueue(ref _pendingRejectEvents, nameof(_pendingRejectEvents));
            DisposeQueue(ref _pendingMemoryEvictionEvents, nameof(_pendingMemoryEvictionEvents));
            DisposeQueue(ref _pendingAupResponses, nameof(_pendingAupResponses));

            if (_modStatesByHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher), nameof(_modStatesByHash));
                _modStatesByHash.Dispose();
                _modStatesByHash = default;
            }

            if (_modIndexByHash.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher), nameof(_modIndexByHash));
                _modIndexByHash.Dispose();
                _modIndexByHash = default;
            }

            if (_kernelIndexByCommandKey.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeHashMap(nameof(ModCommandDispatcher), nameof(_kernelIndexByCommandKey));
                _kernelIndexByCommandKey.Dispose();
                _kernelIndexByCommandKey = default;
            }

            for (int i = 0; i < _kernels.Length; i++)
                _kernels[i] = null;

            for (int i = 0; i < _modIdsByIndex.Length; i++)
            {
                _modIdsByIndex[i] = null;
                _modHashesByIndex[i] = 0u;
            }

            for (int i = 0; i < _raycastRequestRecords.Length; i++)
                _raycastRequestRecords[i] = default;

            _queuedCommandCount = 0;
            _queuedAupCommandCount = 0;
            _queuedRenderCommandCount = 0;
            _queuedRaycastResultCount = 0;
            _queuedRejectEventCount = 0;
            _queuedMemoryEvictionEventCount = 0;
            _queuedAupResponseCount = 0;
            _kernelCount = 0;
            _modCount = 0;
            if (notifyShutdown)
                NotifyMemorySentinelModMask(ModdedGameMaskSignal.FlagLifecycleShutdown);
            _modMaskSignalConfigured = false;
            _abyssalFlowGpu = null;
            _audioService = null;
        }

        internal static void BindRegistryServicesCold()
        {
            _abyssalFlowGpu = GlobalRegistry.AbyssalFlowGpu;
            _audioService = GlobalRegistry.Audio;
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
            {
                _abyssalFlowGpu = currentService as IAbyssalFlowGpuReadModel;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Audio)
                _audioService = currentService as IAudioService;
        }

        internal static uint ComputeModHash(string modId)
        {
            return string.IsNullOrWhiteSpace(modId)
                ? 0u
                : unchecked((uint)LocHash.Compute(modId));
        }

        internal static void RegisterMod(string modId, int apiVersion)
        {
            RegisterMod(modId, apiVersion, 0);
        }

        internal static void RegisterMod(string modId, int apiVersion, int priority)
        {
            if (!LegacyCommandSurfaceEnabled)
                return;

            uint modHash = ComputeModHash(modId);
            if (modHash == 0u)
                return;

            Initialize();
            if (!_modIndexByHash.TryGetValue(modHash, out int index))
            {
                if (_modCount >= ModCapacity)
                    return;

                index = _modCount++;
                _modIndexByHash.Add(modHash, index);
            }

            _modIdsByIndex[index] = modId;
            _modHashesByIndex[index] = modHash;
            _modStatesByHash[modHash] = new ModCommandModState
            {
                ApiVersion = apiVersion,
                LastCommandFrame = -1,
                CommandsThisFrame = 0,
                Priority = priority,
                TrackedHeapBytes = 0L,
                FrameHeapBytes = 0L,
                LastHeapFrame = -1,
                State = ModStateActive,
                Reserved0 = 0,
                Reserved1 = 0
            };

            NotifyMemorySentinelModMask(0u);
        }

        internal static void UnregisterMod(string modId)
        {
            uint modHash = ComputeModHash(modId);
            if (modHash == 0u || !_modStatesByHash.IsCreated)
                return;

            _modStatesByHash.Remove(modHash);

            if (!_modIndexByHash.IsCreated || !_modIndexByHash.TryGetValue(modHash, out int index))
                return;

            int lastIndex = _modCount - 1;
            if ((uint)index < (uint)_modCount && index != lastIndex)
            {
                string movedId = _modIdsByIndex[lastIndex];
                uint movedHash = _modHashesByIndex[lastIndex];
                _modIdsByIndex[index] = movedId;
                _modHashesByIndex[index] = movedHash;
                _modIndexByHash[movedHash] = index;
            }

            _modIdsByIndex[lastIndex] = null;
            _modHashesByIndex[lastIndex] = 0u;
            _modIndexByHash.Remove(modHash);
            _modCount = math.max(0, _modCount - 1);
            NotifyMemorySentinelModMask(0u);
        }

        private static void NotifyMemorySentinelModMask(uint flags)
        {
            EnsureMemorySentinelModMaskLane();

            ModdedGameMaskSignal signal = default;
            signal.ModdedGameMask = _modCount > 0 ? 1u : 0u;
            signal.ActiveModCount = unchecked((uint)math.max(0, _modCount));
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.SourceHash = MemorySentinelModMaskSourceHash;
            signal.Flags = flags;
            SignalBus<ModdedGameMaskSignal>.TryPush(in signal);
        }

        private static void EnsureMemorySentinelModMaskLane()
        {
            if (_modMaskSignalConfigured)
                return;

            SignalBus<ModdedGameMaskSignal>.Configure(
                8,
                maxFrameSignals: 8,
                lowTierFrameSignals: 2,
                laneHash: MemorySentinelModMaskLaneHash);
            SignalBus<ModdedGameMaskSignal>.EnsureInitialized();
            _modMaskSignalConfigured = true;
        }

        internal static bool IsRegisteredMod(string modId)
        {
            uint modHash = ComputeModHash(modId);
            return IsRegisteredMod(modHash);
        }

        internal static bool IsRegisteredMod(uint modHash)
        {
            return modHash != 0u &&
                   _modStatesByHash.IsCreated &&
                   _modStatesByHash.ContainsKey(modHash);
        }

        internal static void QuarantineMod(string modId)
        {
            uint modHash = ComputeModHash(modId);
            QuarantineMod(modHash);
        }

        internal static void ReportModManagedAllocation(string modId, long allocatedBytes)
        {
            uint modHash = ComputeModHash(modId);
            ReportModManagedAllocation(modHash, allocatedBytes);
        }

        internal static void ReportModManagedAllocation(uint modHash, long allocatedBytes)
        {
            if (!LegacyCommandSurfaceEnabled)
                return;

            if (modHash == 0u || allocatedBytes <= 0L || !_modStatesByHash.IsCreated)
                return;

            if (!_modStatesByHash.TryGetValue(modHash, out ModCommandModState state))
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (state.LastHeapFrame != frame)
            {
                state.LastHeapFrame = frame;
                state.FrameHeapBytes = 0L;
            }

            state.FrameHeapBytes = long.MaxValue - state.FrameHeapBytes < allocatedBytes
                ? long.MaxValue
                : state.FrameHeapBytes + allocatedBytes;
            state.TrackedHeapBytes = long.MaxValue - state.TrackedHeapBytes < allocatedBytes
                ? long.MaxValue
                : state.TrackedHeapBytes + allocatedBytes;
            _modStatesByHash[modHash] = state;
            if (state.FrameHeapBytes > ModHeapFrameQuotaBytes)
            {
                GlobalTelemetryBus.PublishModCriticalMemoryEviction(modHash, state.FrameHeapBytes, ModHeapFrameQuotaBytes);
                EnqueueMemoryEvictionEvent(modHash, state.FrameHeapBytes, ModHeapFrameQuotaBytes);
                QuarantineMod(modHash);
                if (TryGetModId(modHash, out string modIdForFrameQuota))
                    ModLoader.DisableManagedMod(modIdForFrameQuota, "CRITICAL_MEMORY_EVICTION: managed allocation frame quota exceeded.");
                return;
            }

            if (state.TrackedHeapBytes <= ModHeapQuotaBytes)
                return;

            GlobalTelemetryBus.PublishModCriticalMemoryEviction(modHash, state.TrackedHeapBytes, ModHeapQuotaBytes);
            EnqueueMemoryEvictionEvent(modHash, state.TrackedHeapBytes, ModHeapQuotaBytes);
            QuarantineMod(modHash);
            if (TryGetModId(modHash, out string modId))
                ModLoader.DisableManagedMod(modId, "CRITICAL_MEMORY_EVICTION: tracked managed allocation quota exceeded.");
        }

        [System.Obsolete("Legacy managed command kernel registration is quarantined. Use FutureCommandEnvelope kernel opcodes.", false)]
        internal static bool RegisterKernel(
            ModCommandOpcode opcode,
            ModCommandTargetSystem targetSystem,
            IModCommandKernel kernel)
        {
            if (!LegacyCommandSurfaceEnabled)
                return false;

            if (kernel == null ||
                opcode == ModCommandOpcode.None ||
                targetSystem == ModCommandTargetSystem.None ||
                IsFutureKernelReservedOpcode((ushort)opcode) ||
                IsFutureKernelReservedTarget((ushort)targetSystem))
            {
                return false;
            }

            Initialize();
            uint key = BuildCommandKey((ushort)opcode, (ushort)targetSystem);
            if (_kernelIndexByCommandKey.ContainsKey(key))
                return false;

            if (_kernelCount >= _kernels.Length)
                return false;

            _kernels[_kernelCount] = kernel;
            _kernelIndexByCommandKey.Add(key, _kernelCount);
            _kernelCount++;
            return true;
        }

        /// <summary>
        /// Enqueues a sandboxed command request for late-frame engine validation.
        /// </summary>
        /// <param name="command">Command payload. Mod identity is overwritten from the active execution scope.</param>
        /// <returns>True when the command entered the queue.</returns>
        [System.Obsolete("Legacy ModCommand request lane is quarantined and returns false. Use HectonAPI.Commands.RequestFuture.", false)]
        internal static bool Request(in ModCommand command)
        {
            if (!LegacyCommandSurfaceEnabled)
                return false;

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Mod commands must be requested from an active mod execution scope.");

            Initialize();

            uint modHash = ModExecutionScope.CurrentModHash;
            if (!TryResolveRequestState(modHash, out ModCommandModState state, out ModCommandRejectReason rejectReason))
            {
                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)rejectReason, 1f);
                return false;
            }

            if (RequiresAup(command.Opcode))
            {
                RejectCommand(modHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.AupRequired);
                return false;
            }

            if (!TryAccountCommandForTick(modHash, ref state))
                return false;

            ModCommand queuedCommand = command;
            queuedCommand.ModHash = modHash;
            queuedCommand.ApiVersion = (ushort)state.ApiVersion;
            queuedCommand.Flags = (ushort)(queuedCommand.Flags | (ushort)ModCommandFlags.Sandboxed);

            if (_queuedCommandCount >= CommandCapacity)
            {
                if (_pendingCommands.TryDequeue(out _))
                    _queuedCommandCount--;

                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)ModCommandRejectReason.QueueFull, 1f);
            }

            _pendingCommands.Enqueue(queuedCommand);
            _queuedCommandCount++;
            return true;
        }

        /// <summary>
        /// Enqueues an AUP-backed command. The engine rebases to frame-space during late-frame drain.
        /// </summary>
        /// <param name="command">AUP command wrapper.</param>
        /// <returns>True when queued.</returns>
        [System.Obsolete("Legacy AUP command request lane is quarantined and returns false. Use HectonAPI.Commands.RequestFuture.", false)]
        internal static bool RequestAup(in ModAupCommand command)
        {
            if (!LegacyCommandSurfaceEnabled)
                return false;

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("AUP mod commands must be requested from an active mod execution scope.");

            Initialize();

            uint modHash = ModExecutionScope.CurrentModHash;
            if (!TryResolveRequestState(modHash, out ModCommandModState state, out ModCommandRejectReason rejectReason))
            {
                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)rejectReason, 1f);
                return false;
            }

            if (!TryAccountCommandForTick(modHash, ref state))
                return false;

            ModAupCommand queuedCommand = command;
            queuedCommand.Command.ModHash = modHash;
            queuedCommand.Command.ApiVersion = (ushort)state.ApiVersion;
            queuedCommand.Command.Flags = (ushort)(queuedCommand.Command.Flags | (ushort)ModCommandFlags.Sandboxed);

            if (_queuedAupCommandCount >= CommandCapacity)
            {
                if (_pendingAupCommands.TryDequeue(out _))
                    _queuedAupCommandCount--;

                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)ModCommandRejectReason.QueueFull, 1f);
            }

            _pendingAupCommands.Enqueue(queuedCommand);
            _queuedAupCommandCount++;
            return true;
        }

        /// <summary>
        /// Enqueues a matrix for the reserved mod instancing graphics layer.
        /// </summary>
        /// <param name="command">Render instance packet. Mod identity is overwritten.</param>
        /// <returns>True when queued.</returns>
        [System.Obsolete("Legacy render-instance request lane is quarantined and returns false. Use HectonAPI.Commands.RequestFuture.", false)]
        internal static bool RequestRenderInstance(in ModRenderInstanceCommand command)
        {
            if (!LegacyCommandSurfaceEnabled)
                return false;

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Mod render commands must be requested from an active mod execution scope.");

            Initialize();

            uint modHash = ModExecutionScope.CurrentModHash;
            if (!TryResolveRequestState(modHash, out ModCommandModState state, out ModCommandRejectReason rejectReason))
            {
                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)rejectReason, 1f);
                return false;
            }

            if (!TryAccountCommandForTick(modHash, ref state))
                return false;

            if (_queuedRenderCommandCount >= MaxModRenderInstancesPerFrame)
            {
                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)ModCommandRejectReason.RenderCapacityExceeded, 1f);
                EnqueueRejectEvent(modHash, command.RequestId, 0, (ushort)ModCommandTargetSystem.Effects, ModCommandRejectReason.RenderCapacityExceeded);
                return false;
            }

            ModRenderInstanceCommand queuedCommand = command;
            queuedCommand.ModHash = modHash;
            _pendingRenderCommands.Enqueue(queuedCommand);
            _queuedRenderCommandCount++;
            return true;
        }

        /// <summary>
        /// Late-frame command drain. Called only by <see cref="SystemDispatcher"/>.
        /// </summary>
        internal static void DrainLateFrame()
        {
            if (!LegacyCommandSurfaceEnabled)
            {
                FutureCommandSandboxValidator.DrainLateFrame();
                return;
            }

            FlushDeferredEventQueues();
            DrainRenderCommands();
            FutureCommandSandboxValidator.DrainLateFrame();
        }

        /// <summary>
        /// Pre-simulation command drain. Called only after GlobalSignals pre-sim flush and before gameplay ticks.
        /// </summary>
        internal static void DrainPreSimulation()
        {
            FutureCommandSandboxValidator.DrainPreSimulation();
            if (!LegacyCommandSurfaceEnabled)
                return;

            DrainAupCommands();
            DrainStandardCommands();
        }

        private static void DrainAupCommands()
        {
            if (!_pendingAupCommands.IsCreated || _queuedAupCommandCount <= 0)
                return;

            int candidateCount = 0;
            int drained = 0;
            while (_queuedAupCommandCount > 0 && drained < MaxDrainPerLateFrame)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingAupCommands.TryDequeue(out ModAupCommand aupCommand))
                    return;

                _queuedAupCommandCount--;
                drained++;

                if (!TryRebaseAupCommand(in aupCommand, out ModCommand command, out ModCommandRejectReason rebaseRejectReason))
                {
                    RejectCommand(
                        aupCommand.Command.ModHash,
                        aupCommand.Command.RequestId,
                        aupCommand.Command.Opcode,
                        aupCommand.Command.TargetSystem,
                        rebaseRejectReason);
                    continue;
                }

                if (!TryPassSecurityGate(ref command, out ModCommandRejectReason rejectReason))
                {
                    RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, rejectReason);
                    continue;
                }

                int priority = ResolveModPriority(command.ModHash);
                if (TryExecuteValidatedAupIntrinsic(in command, in aupCommand.Position))
                    continue;

                if (command.Opcode == (ushort)ModCommandOpcode.SpawnDebris &&
                    !TryAcceptSpawnCandidate(
                        in command,
                        aupCommand.Position.Grid,
                        aupCommand.Position.Local,
                        priority,
                        _aupCandidates,
                        ref candidateCount))
                {
                    continue;
                }

                if (candidateCount < _aupCandidates.Length)
                {
                    _aupCandidates[candidateCount] = new AupExecutionCandidate
                    {
                        Command = command,
                        Grid = aupCommand.Position.Grid,
                        Local = aupCommand.Position.Local,
                        Priority = priority,
                        Accepted = 1
                    };
                    candidateCount++;
                }
            }

            for (int i = 0; i < candidateCount; i++)
            {
                if (_aupCandidates[i].Accepted != 0)
                    ExecuteValidatedCommand(in _aupCandidates[i].Command);

                _aupCandidates[i] = default;
            }
        }

        private static void DrainStandardCommands()
        {
            if (!_pendingCommands.IsCreated || _queuedCommandCount <= 0)
                return;

            int drained = 0;
            while (_queuedCommandCount > 0 && drained < MaxDrainPerLateFrame)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingCommands.TryDequeue(out ModCommand command))
                    return;

                _queuedCommandCount--;
                drained++;

                if (!TryPassSecurityGate(ref command, out ModCommandRejectReason rejectReason))
                {
                    RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, rejectReason);
                    continue;
                }

                ExecuteValidatedCommand(in command);
            }
        }

        private static void DrainRenderCommands()
        {
            if (!_pendingRenderCommands.IsCreated || _queuedRenderCommandCount <= 0)
                return;

            int drained = 0;
            while (_queuedRenderCommandCount > 0 && drained < MaxModRenderInstancesPerFrame)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingRenderCommands.TryDequeue(out ModRenderInstanceCommand command))
                    return;

                _queuedRenderCommandCount--;
                drained++;

                if (!GPUScatterDirector.SubmitModInstanceMatrix(command.ModHash, command.ResourceHash, in command.Matrix))
                {
                    RejectCommand(command.ModHash, command.RequestId, 0, (ushort)ModCommandTargetSystem.Effects, ModCommandRejectReason.RenderCapacityExceeded);
                }
            }
        }

        private static bool TryPassSecurityGate(ref ModCommand command, out ModCommandRejectReason rejectReason)
        {
            rejectReason = ModCommandRejectReason.None;

            if (command.ModHash == 0u || !_modStatesByHash.TryGetValue(command.ModHash, out ModCommandModState state))
            {
                rejectReason = ModCommandRejectReason.UnknownMod;
                return false;
            }

            if (state.State == ModStateQuarantined)
            {
                rejectReason = ModCommandRejectReason.QuarantinedMod;
                return false;
            }

            ApplyCompatShim(ref command, state.ApiVersion);

            if (command.Opcode == (ushort)ModCommandOpcode.None)
            {
                rejectReason = ModCommandRejectReason.InvalidOpcode;
                return false;
            }

            if (IsFutureKernelReservedOpcode(command.Opcode))
            {
                rejectReason = ModCommandRejectReason.InvalidOpcode;
                return false;
            }

            if (IsFutureKernelReservedTarget(command.TargetSystem))
            {
                rejectReason = ModCommandRejectReason.InvalidTarget;
                return false;
            }

            if (RequiresAup(command.Opcode) &&
                (command.Flags & (ushort)ModCommandFlags.AupRebased) == 0)
            {
                rejectReason = ModCommandRejectReason.AupRequired;
                return false;
            }

            if (!IsTargetValid(command.Opcode, command.TargetSystem))
            {
                rejectReason = ModCommandRejectReason.InvalidTarget;
                return false;
            }

            command.Flags = (ushort)(command.Flags | (ushort)ModCommandFlags.Validated);
            return true;
        }

        private static void ExecuteValidatedCommand(in ModCommand command)
        {
            if (command.Opcode == (ushort)ModCommandOpcode.RaycastQuery)
            {
                if (!QueueModRaycast(in command))
                    RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.RaycastLaneFull);
                return;
            }

            if (command.Opcode == (ushort)ModCommandOpcode.VoxelModify ||
                command.Opcode == (ushort)ModCommandOpcode.FlowQuery ||
                command.Opcode == (ushort)ModCommandOpcode.AcousticPing)
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.AupRequired);
                return;
            }

            uint key = BuildCommandKey(command.Opcode, command.TargetSystem);
            if (!_kernelIndexByCommandKey.TryGetValue(key, out int kernelIndex) ||
                (uint)kernelIndex >= (uint)_kernelCount)
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.MissingKernel);
                return;
            }

            IModCommandKernel kernel = _kernels[kernelIndex];
            if (kernel == null || !kernel.Execute(in command))
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.MissingKernel);
        }

        private static bool TryExecuteValidatedAupIntrinsic(in ModCommand command, in ModAup position)
        {
            switch ((ModCommandOpcode)command.Opcode)
            {
                case ModCommandOpcode.VoxelModify:
                    ExecuteModVoxelModify(in command, in position);
                    return true;

                case ModCommandOpcode.FlowQuery:
                    ExecuteModFlowQuery(in command, in position);
                    return true;

                case ModCommandOpcode.AcousticPing:
                    ExecuteModAcousticPing(in command, in position);
                    return true;

                default:
                    return false;
            }
        }

        private static void ExecuteModVoxelModify(in ModCommand command, in ModAup position)
        {
            UnpackFloat2(command.Payload1, out float centerX, out float centerY);
            UnpackFloat2(command.Payload2, out float centerZ, out float radius);
            Vector3 runtimeCenter = new Vector3(centerX, centerY, centerZ);
            if (!IsFinite(runtimeCenter) ||
                !float.IsFinite(radius) ||
                radius <= 0f ||
                radius > MaxModVoxelModifyRadiusMeters)
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.InvalidPayload);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.VoxelModify, ModAupResponseStatus.Rejected, in position);
                return;
            }

            if (IsProtectedCoreAup(in position))
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.ProtectedCoreSector);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.VoxelModify, ModAupResponseStatus.Rejected, in position);
                return;
            }

            HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
            VoxelDeltaProcessor deltaProcessor = engine != null ? engine.DeltaProcessor : null;
            if (deltaProcessor == null)
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.VoxelUnavailable);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.VoxelModify, ModAupResponseStatus.Unavailable, in position);
                return;
            }

            ushort mode = unchecked((ushort)(command.Payload5 & 0xFFFFUL));
            bool additive = mode == (ushort)ModSdfMode.Add;
            if (!deltaProcessor.TryApplyModSdfModify(runtimeCenter, radius, additive))
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.VoxelUnavailable);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.VoxelModify, ModAupResponseStatus.Unavailable, in position);
                return;
            }

            EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.VoxelModify, ModAupResponseStatus.Accepted, in position);
        }

        private static void ExecuteModFlowQuery(in ModCommand command, in ModAup position)
        {
            UnpackFloat2(command.Payload1, out float centerX, out float centerY);
            UnpackFloat2(command.Payload2, out float centerZ, out _);
            Vector3 runtimePosition = new Vector3(centerX, centerY, centerZ);
            if (!IsFinite(runtimePosition))
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.InvalidPayload);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.FlowVector, ModAupResponseStatus.Rejected, in position);
                return;
            }

            IAbyssalFlowGpuReadModel fluidFlow = _abyssalFlowGpu;
            if (fluidFlow == null ||
                !fluidFlow.TrySampleModAbyssalFlow(runtimePosition, out float3 flowVector))
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.FlowUnavailable);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.FlowVector, ModAupResponseStatus.Unavailable, in position);
                return;
            }

            uint3 payload = PackSequentialFloat3(flowVector.x, flowVector.y, flowVector.z);
            EnqueueAupResponse(
                command.ModHash,
                command.RequestId,
                ModAupResponseKind.FlowVector,
                ModAupResponseStatus.Accepted,
                in position,
                payload);
        }

        private static void ExecuteModAcousticPing(in ModCommand command, in ModAup position)
        {
            UnpackFloat2(command.Payload1, out float centerX, out float centerY);
            UnpackFloat2(command.Payload2, out float centerZ, out float intensity01);
            Vector3 runtimePosition = new Vector3(centerX, centerY, centerZ);
            if (!IsFinite(runtimePosition) || !float.IsFinite(intensity01) || intensity01 <= 0f)
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.InvalidPayload);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.AcousticPing, ModAupResponseStatus.Rejected, in position);
                return;
            }

            float normalizedIntensity = math.saturate(intensity01);
            IAudioService audioManager = _audioService;
            if (audioManager == null || !audioManager.TryEmitModAcousticPing(runtimePosition, normalizedIntensity))
            {
                RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.AcousticUnavailable);
                EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.AcousticPing, ModAupResponseStatus.Unavailable, in position);
                return;
            }

            EnqueueAupResponse(command.ModHash, command.RequestId, ModAupResponseKind.AcousticPing, ModAupResponseStatus.Accepted, in position);
        }

        private static bool TryResolveRequestState(uint modHash, out ModCommandModState state, out ModCommandRejectReason rejectReason)
        {
            state = default;
            rejectReason = ModCommandRejectReason.None;

            if (modHash == 0u || !_modStatesByHash.TryGetValue(modHash, out state))
            {
                rejectReason = ModCommandRejectReason.UnknownMod;
                return false;
            }

            if (state.State == ModStateQuarantined)
            {
                rejectReason = ModCommandRejectReason.QuarantinedMod;
                return false;
            }

            return true;
        }

        private static bool TryAccountCommandForTick(uint modHash, ref ModCommandModState state)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (state.LastCommandFrame != frame)
            {
                state.LastCommandFrame = frame;
                state.CommandsThisFrame = 0;
            }

            if (state.CommandsThisFrame >= MaxCommandsPerModPerTick)
            {
                _modStatesByHash[modHash] = state;
                GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)ModCommandRejectReason.CommandFlood, 1f);
                EnqueueRejectEvent(modHash, 0u, 0, 0, ModCommandRejectReason.CommandFlood);
                return false;
            }

            state.CommandsThisFrame++;
            _modStatesByHash[modHash] = state;
            return true;
        }

        private static bool TryRebaseAupCommand(
            in ModAupCommand aupCommand,
            out ModCommand rebasedCommand,
            out ModCommandRejectReason rejectReason)
        {
            rebasedCommand = aupCommand.Command;
            rejectReason = ModCommandRejectReason.None;

            if (HectonFloatingOrigin.IsShiftInProgress || HectonFloatingOrigin.IsPhysicsPausedForShift)
            {
                rejectReason = ModCommandRejectReason.OriginShiftActive;
                return false;
            }

            if (!IsValidAupLocal(aupCommand.Position.Local) || !float.IsFinite(aupCommand.Scalar))
            {
                rejectReason = ModCommandRejectReason.InvalidPayload;
                return false;
            }

            float3 framePosition = RebaseAupToFrameSpace(aupCommand.Position.Grid, aupCommand.Position.Local);
            if (!IsFinite(framePosition))
            {
                rejectReason = ModCommandRejectReason.InvalidPayload;
                return false;
            }

            float3 direction = aupCommand.Direction;
            float directionLengthSq = math.lengthsq(direction);
            if (rebasedCommand.Opcode == (ushort)ModCommandOpcode.RaycastQuery)
            {
                if (!IsFinite(direction) ||
                    directionLengthSq <= 0.0001f ||
                    !float.IsFinite(directionLengthSq) ||
                    aupCommand.Scalar <= 0f)
                {
                    rejectReason = ModCommandRejectReason.InvalidTarget;
                    return false;
                }

                direction *= math.rsqrt(directionLengthSq);
            }

            rebasedCommand.Payload1 = PackFloat2(framePosition.x, framePosition.y);
            rebasedCommand.Payload2 = PackFloat2(framePosition.z, aupCommand.Scalar);
            rebasedCommand.Payload3 = PackFloat2(direction.x, direction.y);
            rebasedCommand.Payload4 = PackFloat2(direction.z, 0f);
            rebasedCommand.Flags = (ushort)(rebasedCommand.Flags | (ushort)ModCommandFlags.AupRebased);
            return true;
        }

        private static bool QueueModRaycast(in ModCommand command)
        {
            return false;
        }

        private static int FindFreeRaycastSlot()
        {
            for (int i = 0; i < _raycastRequestRecords.Length; i++)
            {
                if (_raycastRequestRecords[i].IsActive == 0)
                    return i;
            }

            return -1;
        }

        private static void ConsumeDispatcherSurfaceHit(int slot, in KinematicSurfaceHit hit)
        {
            if ((uint)slot >= (uint)_raycastRequestRecords.Length)
                return;

            ModRaycastRequestRecord record = _raycastRequestRecords[slot];
            _raycastRequestRecords[slot] = default;
            if (record.IsActive == 0)
                return;

            bool hasHit = hit.hasHit && hit.distance > 0f;
            ModRaycastResultPayload payload = new ModRaycastResultPayload
            {
                ModHash = record.ModHash,
                RequestId = record.RequestId,
                Status = hasHit ? (uint)ModRaycastResultStatus.Hit : (uint)ModRaycastResultStatus.Miss,
                ColliderInstanceId = 0,
                Layer = hasHit ? hit.Layer : -1,
                Distance = hasHit ? hit.distance : 0f,
                Point = hasHit ? new float3(hit.point.x, hit.point.y, hit.point.z) : default,
                Normal = hasHit ? new float3(hit.normal.x, hit.normal.y, hit.normal.z) : default
            };

            if (_queuedRaycastResultCount < MaxModRaycasts)
            {
                _pendingRaycastResults.Enqueue(payload);
                _queuedRaycastResultCount++;
            }
        }

        private static bool TryAcceptSpawnCandidate(
            in ModCommand command,
            long3 grid,
            float3 local,
            int priority,
            AupExecutionCandidate[] candidates,
            ref int candidateCount)
        {
            for (int i = 0; i < candidateCount; i++)
            {
                if (candidates[i].Accepted == 0 ||
                    candidates[i].Command.Opcode != (ushort)ModCommandOpcode.SpawnDebris)
                {
                    continue;
                }

                if (DistanceSqAup(grid, local, candidates[i].Grid, candidates[i].Local) > SpawnConflictEpsilonSq)
                    continue;

                if (priority <= candidates[i].Priority)
                {
                    RejectCommand(command.ModHash, command.RequestId, command.Opcode, command.TargetSystem, ModCommandRejectReason.SpawnConflict);
                    return false;
                }

                RejectCommand(
                    candidates[i].Command.ModHash,
                    candidates[i].Command.RequestId,
                    candidates[i].Command.Opcode,
                    candidates[i].Command.TargetSystem,
                    ModCommandRejectReason.SpawnConflict);
                candidates[i].Accepted = 0;
                return true;
            }

            return true;
        }

        private static void RejectCommand(uint modHash, uint requestId, ushort opcode, ushort targetSystem, ModCommandRejectReason reason)
        {
            GlobalTelemetryBus.PublishModCommandRejected(modHash, (uint)reason, 1f);
            EnqueueRejectEvent(modHash, requestId, opcode, targetSystem, reason);
        }

        private static void EnqueueRejectEvent(uint modHash, uint requestId, ushort opcode, ushort targetSystem, ModCommandRejectReason reason)
        {
            if (!_pendingRejectEvents.IsCreated || _queuedRejectEventCount >= MaxRejectEventsPerLateFrame)
                return;

            _pendingRejectEvents.Enqueue(new ModInteractionRejectedPayload
            {
                ModHash = modHash,
                RequestId = requestId,
                Opcode = opcode,
                TargetSystem = targetSystem,
                Reason = (uint)reason
            });
            _queuedRejectEventCount++;
        }

        private static void EnqueueMemoryEvictionEvent(uint modHash, long trackedHeapBytes, long limitBytes)
        {
            if (!_pendingMemoryEvictionEvents.IsCreated ||
                _queuedMemoryEvictionEventCount >= MaxMemoryEvictionEventsPerLateFrame)
                return;

            long clampedLimitBytes = limitBytes < 0L
                ? 0L
                : (limitBytes > uint.MaxValue ? uint.MaxValue : limitBytes);
            _pendingMemoryEvictionEvents.Enqueue(new ModCriticalMemoryEvictionPayload
            {
                ModHash = modHash,
                TrackedHeapBytes = unchecked((ulong)(trackedHeapBytes > 0L ? trackedHeapBytes : 0L)),
                LimitBytes = unchecked((uint)clampedLimitBytes),
                Reason = (uint)ModCommandRejectReason.HeapQuotaExceeded
            });
            _queuedMemoryEvictionEventCount++;
        }

        private static void EnqueueAupResponse(
            uint modHash,
            uint requestId,
            ModAupResponseKind responseKind,
            ModAupResponseStatus status,
            in ModAup position,
            uint3 payload = default)
        {
            if (!_pendingAupResponses.IsCreated || _queuedAupResponseCount >= MaxAupResponsesPerLateFrame)
                return;

            _pendingAupResponses.Enqueue(new ModAupResponse
            {
                ModHash = modHash,
                RequestId = requestId,
                ResponseKind = (uint)responseKind,
                Status = (uint)status,
                Grid = position.Grid,
                Local = position.Local,
                Payload = payload
            });
            _queuedAupResponseCount++;
        }

        private static void FlushDeferredEventQueues()
        {
            while (_queuedRaycastResultCount > 0 && !_pendingRaycastResults.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingRaycastResults.TryDequeue(out ModRaycastResultPayload raycastPayload))
                    break;

                _queuedRaycastResultCount--;
                HectonEventBus.Publish(in raycastPayload);
            }

            while (_queuedRejectEventCount > 0 && !_pendingRejectEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingRejectEvents.TryDequeue(out ModInteractionRejectedPayload rejectPayload))
                    break;

                _queuedRejectEventCount--;
                HectonEventBus.Publish(in rejectPayload);
            }

            while (_queuedMemoryEvictionEventCount > 0 && !_pendingMemoryEvictionEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingMemoryEvictionEvents.TryDequeue(out ModCriticalMemoryEvictionPayload evictionPayload))
                    break;

                _queuedMemoryEvictionEventCount--;
                HectonEventBus.Publish(in evictionPayload);
            }

            while (_queuedAupResponseCount > 0 && !_pendingAupResponses.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingAupResponses.TryDequeue(out ModAupResponse aupResponse))
                    break;

                _queuedAupResponseCount--;
                HectonEventBus.Publish(in aupResponse);
            }
        }

        private static void QuarantineMod(uint modHash)
        {
            if (modHash == 0u || !_modStatesByHash.IsCreated)
                return;

            if (!_modStatesByHash.TryGetValue(modHash, out ModCommandModState state))
                return;

            state.State = ModStateQuarantined;
            _modStatesByHash[modHash] = state;
        }

        private static bool TryGetModId(uint modHash, out string modId)
        {
            modId = null;
            if (!_modIndexByHash.IsCreated || !_modIndexByHash.TryGetValue(modHash, out int index))
                return false;

            if ((uint)index >= (uint)_modCount)
                return false;

            modId = _modIdsByIndex[index];
            return !string.IsNullOrWhiteSpace(modId);
        }

        private static int ResolveModPriority(uint modHash)
        {
            if (modHash == 0u || !_modStatesByHash.TryGetValue(modHash, out ModCommandModState state))
                return 0;

            return state.Priority;
        }

        private static void ApplyCompatShim(ref ModCommand command, int modApiVersion)
        {
            if (modApiVersion >= CurrentApiVersion)
                return;

            if (modApiVersion <= 1 && command.Opcode == 1001)
            {
                command.Opcode = (ushort)ModCommandOpcode.SpawnDebris;
                command.TargetSystem = (ushort)ModCommandTargetSystem.World;
            }
        }

        private static bool IsTargetValid(ushort opcode, ushort targetSystem)
        {
            switch ((ModCommandOpcode)opcode)
            {
                case ModCommandOpcode.SpawnDebris:
                    return targetSystem == (ushort)ModCommandTargetSystem.World;
                case ModCommandOpcode.ApplyHeat:
                    return targetSystem == (ushort)ModCommandTargetSystem.Thermal ||
                           targetSystem == (ushort)ModCommandTargetSystem.Voxel;
                case ModCommandOpcode.RaycastQuery:
                    return targetSystem == (ushort)ModCommandTargetSystem.Physics;
                case ModCommandOpcode.SpawnEffect:
                    return targetSystem == (ushort)ModCommandTargetSystem.Effects;
                case ModCommandOpcode.MoveEntity:
                    return targetSystem == (ushort)ModCommandTargetSystem.World;
                case ModCommandOpcode.VoxelModify:
                    return targetSystem == (ushort)ModCommandTargetSystem.Voxel;
                case ModCommandOpcode.FlowQuery:
                    return targetSystem == (ushort)ModCommandTargetSystem.Environment;
                case ModCommandOpcode.AcousticPing:
                    return targetSystem == (ushort)ModCommandTargetSystem.Audio;
                default:
                    return false;
            }
        }

        private static bool IsFutureKernelReservedOpcode(ushort opcode)
        {
            return opcode >= FutureKernelReservedOpcodeMin && opcode <= FutureKernelReservedOpcodeMax;
        }

        private static bool IsFutureKernelReservedTarget(ushort targetSystem)
        {
            return targetSystem >= FutureKernelReservedTargetMin && targetSystem <= FutureKernelReservedTargetMax;
        }

        private static bool RequiresAup(ushort opcode)
        {
            switch ((ModCommandOpcode)opcode)
            {
                case ModCommandOpcode.SpawnDebris:
                case ModCommandOpcode.ApplyHeat:
                case ModCommandOpcode.RaycastQuery:
                case ModCommandOpcode.SpawnEffect:
                case ModCommandOpcode.MoveEntity:
                case ModCommandOpcode.VoxelModify:
                case ModCommandOpcode.FlowQuery:
                case ModCommandOpcode.AcousticPing:
                    return true;
                default:
                    return false;
            }
        }

        private static uint BuildCommandKey(ushort opcode, ushort targetSystem)
        {
            return ((uint)targetSystem << 16) | opcode;
        }

        private static float3 RebaseAupToFrameSpace(long3 grid, float3 local)
        {
            double3 offset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            double cellSize = AupCellSizeMeters;
            double runtimeX = (grid.x * cellSize) + local.x - offset.x;
            double runtimeY = (grid.y * cellSize) + local.y - offset.y;
            double runtimeZ = (grid.z * cellSize) + local.z - offset.z;
            return new float3((float)runtimeX, (float)runtimeY, (float)runtimeZ);
        }

        private static bool IsProtectedCoreAup(in ModAup position)
        {
            AbsoluteUniversePosition absolutePosition = new AbsoluteUniversePosition
            {
                GridX = position.Grid.x,
                GridY = position.Grid.y,
                GridZ = position.Grid.z,
                LocalX = position.Local.x,
                LocalY = position.Local.y,
                LocalZ = position.Local.z
            };

            return PersistentWorldRegistry.IsModProtectedCoreAup(in absolutePosition);
        }

        private static double DistanceSqAup(long3 gridA, float3 localA, long3 gridB, float3 localB)
        {
            const double cellSize = AupCellSizeMeters;
            double deltaX = ((gridA.x - gridB.x) * cellSize) + ((double)localA.x - localB.x);
            double deltaY = ((gridA.y - gridB.y) * cellSize) + ((double)localA.y - localB.y);
            double deltaZ = ((gridA.z - gridB.z) * cellSize) + ((double)localA.z - localB.z);
            return (deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ);
        }

        private static ulong PackFloat2(float a, float b)
        {
            return ((ulong)math.asuint(b) << 32) | math.asuint(a);
        }

        /// <summary>
        /// Packs two sequential floats into a uint2 without managed conversion.
        /// </summary>
        internal static uint2 PackSequentialFloat2(float a, float b)
        {
            return new uint2(math.asuint(a), math.asuint(b));
        }

        /// <summary>
        /// Packs three sequential floats into a uint3 without managed conversion.
        /// </summary>
        internal static uint3 PackSequentialFloat3(float a, float b, float c)
        {
            return new uint3(math.asuint(a), math.asuint(b), math.asuint(c));
        }

        private static void UnpackFloat2(ulong packed, out float a, out float b)
        {
            a = math.asfloat(unchecked((uint)(packed & 0xFFFFFFFFUL)));
            b = math.asfloat(unchecked((uint)(packed >> 32)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private static bool IsValidAupLocal(float3 local)
        {
            return math.all(math.isfinite(local)) &&
                   math.abs(local.x) <= AupCellSizeMeters &&
                   math.abs(local.y) <= AupCellSizeMeters &&
                   math.abs(local.z) <= AupCellSizeMeters;
        }

        private static void RegisterQueue<TPayload>(ref NativeQueue<TPayload> queue, int expectedCapacity, string label)
            where TPayload : unmanaged
        {
            NativeMemorySentinel.RegisterNativeQueue(
                queue,
                expectedCapacity,
                nameof(ModCommandDispatcher),
                label,
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref queue, expectedCapacity);
        }

        private static void PrewarmQueue<TPayload>(ref NativeQueue<TPayload> queue, int capacity)
            where TPayload : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static void DisposeQueue<TPayload>(ref NativeQueue<TPayload> queue, string label)
            where TPayload : unmanaged
        {
            if (!queue.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeQueue(nameof(ModCommandDispatcher), label);
            queue.Dispose();
            queue = default;
        }

        private sealed class ModRaycastReceiver : IDispatcherSurfaceProbeReceiver
        {
            public void ConsumeDispatcherSurfaceHit(int requestId, in KinematicSurfaceHit hit)
            {
                ModCommandDispatcher.ConsumeDispatcherSurfaceHit(requestId, in hit);
            }
        }
    }
}
