using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

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
        MoveEntity = 5
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
        Effects = 5
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
    [StructLayout(LayoutKind.Sequential, Size = 64)]
    public struct ModCommand
    {
        /// <summary>16-bit command opcode.</summary>
        public ushort Opcode;

        /// <summary>16-bit target system identifier.</summary>
        public ushort TargetSystem;

        /// <summary>16-bit command flags.</summary>
        public ushort Flags;

        /// <summary>16-bit mod API version captured at enqueue time.</summary>
        public ushort ApiVersion;

        /// <summary>Payload word 0. Low 32 bits = mod hash. High 32 bits = request id.</summary>
        public ulong Payload0;

        /// <summary>Payload word 1.</summary>
        public ulong Payload1;

        /// <summary>Payload word 2.</summary>
        public ulong Payload2;

        /// <summary>Payload word 3.</summary>
        public ulong Payload3;

        /// <summary>Payload word 4.</summary>
        public ulong Payload4;

        /// <summary>Payload word 5.</summary>
        public ulong Payload5;

        /// <summary>Payload word 6.</summary>
        public ulong Payload6;

        /// <summary>Stable hash of the mod that requested this command.</summary>
        public uint ModHash
        {
            readonly get => unchecked((uint)(Payload0 & 0xFFFFFFFFUL));
            set => Payload0 = (Payload0 & 0xFFFFFFFF00000000UL) | value;
        }

        /// <summary>Mod-local request identifier.</summary>
        public uint RequestId
        {
            readonly get => unchecked((uint)(Payload0 >> 32));
            set => Payload0 = (Payload0 & 0x00000000FFFFFFFFUL) | ((ulong)value << 32);
        }
    }

    /// <summary>
    /// Engine-side executor for a validated mod command.
    /// </summary>
    public interface IModCommandKernel
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
        HeapQuotaExceeded = 13
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
        private const int CurrentApiVersion = ModLoader.CurrentAPIVersion;
        private const int AupCellSizeMeters = 5000;
        private const double SpawnConflictEpsilonSq = 0.25d;
        private const long ModHeapQuotaBytes = 16L * 1024L * 1024L;

        private const byte ModStateActive = 1;
        private const byte ModStateQuarantined = 2;

        private struct ModCommandModState
        {
            public int ApiVersion;
            public int LastCommandFrame;
            public int CommandsThisFrame;
            public int Priority;
            public long TrackedHeapBytes;
            public byte State;
            public byte Reserved0;
            public ushort Reserved1;
        }

        private struct ModRaycastRequestRecord
        {
            public uint ModHash;
            public uint RequestId;
            public byte IsActive;
            public byte Reserved0;
            public ushort Reserved1;
        }

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
        private static NativeHashMap<uint, ModCommandModState> _modStatesByHash;
        private static NativeHashMap<uint, int> _modIndexByHash;
        private static NativeHashMap<uint, int> _kernelIndexByCommandKey;
        private static int _queuedCommandCount;
        private static int _queuedAupCommandCount;
        private static int _queuedRenderCommandCount;
        private static int _queuedRaycastResultCount;
        private static int _queuedRejectEventCount;
        private static int _queuedMemoryEvictionEventCount;
        private static int _kernelCount;
        private static int _modCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Initialize()
        {
            if (!_pendingCommands.IsCreated)
                _pendingCommands = new NativeQueue<ModCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModCommand>[4096] - sandboxed mod command ring buffer - owner: ModCommandDispatcher

            if (!_pendingAupCommands.IsCreated)
                _pendingAupCommands = new NativeQueue<ModAupCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModAupCommand>[4096] - AUP-stable mod command ring buffer - owner: ModCommandDispatcher

            if (!_pendingRenderCommands.IsCreated)
                _pendingRenderCommands = new NativeQueue<ModRenderInstanceCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModRenderInstanceCommand>[1024] - mod instancing request lane - owner: ModCommandDispatcher

            if (!_pendingRaycastResults.IsCreated)
                _pendingRaycastResults = new NativeQueue<ModRaycastResultPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModRaycastResultPayload>[128] - next-frame mod raycast callback lane - owner: ModCommandDispatcher

            if (!_pendingRejectEvents.IsCreated)
                _pendingRejectEvents = new NativeQueue<ModInteractionRejectedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModInteractionRejectedPayload>[256] - unmanaged mod rejection event lane - owner: ModCommandDispatcher

            if (!_pendingMemoryEvictionEvents.IsCreated)
                _pendingMemoryEvictionEvents = new NativeQueue<ModCriticalMemoryEvictionPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ModCriticalMemoryEvictionPayload>[32] - unmanaged mod memory eviction event lane - owner: ModCommandDispatcher

            if (!_modStatesByHash.IsCreated)
                _modStatesByHash = new NativeHashMap<uint, ModCommandModState>(ModCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,ModCommandModState>[32] - O(1) mod command security lookup - owner: ModCommandDispatcher

            if (!_modIndexByHash.IsCreated)
                _modIndexByHash = new NativeHashMap<uint, int>(ModCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[32] - O(1) mod hash reverse-index lookup - owner: ModCommandDispatcher

            if (!_kernelIndexByCommandKey.IsCreated)
                _kernelIndexByCommandKey = new NativeHashMap<uint, int>(KernelCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[32] - O(1) command kernel lookup - owner: ModCommandDispatcher
        }

        internal static void Shutdown()
        {
            DisposeQueue(ref _pendingCommands);
            DisposeQueue(ref _pendingAupCommands);
            DisposeQueue(ref _pendingRenderCommands);
            DisposeQueue(ref _pendingRaycastResults);
            DisposeQueue(ref _pendingRejectEvents);
            DisposeQueue(ref _pendingMemoryEvictionEvents);

            if (_modStatesByHash.IsCreated)
            {
                _modStatesByHash.Dispose();
                _modStatesByHash = default;
            }

            if (_modIndexByHash.IsCreated)
            {
                _modIndexByHash.Dispose();
                _modIndexByHash = default;
            }

            if (_kernelIndexByCommandKey.IsCreated)
            {
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
            _kernelCount = 0;
            _modCount = 0;
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
                State = ModStateActive,
                Reserved0 = 0,
                Reserved1 = 0
            };
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
        }

        internal static bool IsRegisteredMod(string modId)
        {
            uint modHash = ComputeModHash(modId);
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
            if (modHash == 0u || allocatedBytes <= 0L || !_modStatesByHash.IsCreated)
                return;

            if (!_modStatesByHash.TryGetValue(modHash, out ModCommandModState state))
                return;

            state.TrackedHeapBytes = long.MaxValue - state.TrackedHeapBytes < allocatedBytes
                ? long.MaxValue
                : state.TrackedHeapBytes + allocatedBytes;
            _modStatesByHash[modHash] = state;
            if (state.TrackedHeapBytes <= ModHeapQuotaBytes)
                return;

            GlobalTelemetryBus.PublishModCriticalMemoryEviction(modHash, state.TrackedHeapBytes, ModHeapQuotaBytes);
            EnqueueMemoryEvictionEvent(modHash, state.TrackedHeapBytes);
            QuarantineMod(modHash);
            if (TryGetModId(modHash, out string modId))
                ModLoader.DisableManagedMod(modId, "CRITICAL_MEMORY_EVICTION: tracked managed allocation quota exceeded.");
        }

        internal static bool RegisterKernel(
            ModCommandOpcode opcode,
            ModCommandTargetSystem targetSystem,
            IModCommandKernel kernel)
        {
            if (kernel == null || opcode == ModCommandOpcode.None || targetSystem == ModCommandTargetSystem.None)
                return false;

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
        public static bool Request(in ModCommand command)
        {
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
        public static bool RequestAup(in ModAupCommand command)
        {
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
        public static bool RequestRenderInstance(in ModRenderInstanceCommand command)
        {
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
            FlushDeferredEventQueues();
            DrainAupCommands();
            DrainStandardCommands();
            DrainRenderCommands();
        }

        private static void DrainAupCommands()
        {
            if (!_pendingAupCommands.IsCreated || _queuedAupCommandCount <= 0)
                return;

            int candidateCount = 0;
            int drained = 0;
            while (_queuedAupCommandCount > 0 && drained < MaxDrainPerLateFrame)
            {
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
            int frame = Time.frameCount;
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

            float3 framePosition = RebaseAupToFrameSpace(aupCommand.Position.Grid, aupCommand.Position.Local);
            float3 direction = aupCommand.Direction;
            float directionLengthSq = math.lengthsq(direction);
            if (rebasedCommand.Opcode == (ushort)ModCommandOpcode.RaycastQuery)
            {
                if (directionLengthSq <= 0.0001f || aupCommand.Scalar <= 0f)
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
            int slot = FindFreeRaycastSlot();
            if (slot < 0)
                return false;

            UnpackFloat2(command.Payload1, out float originX, out float originY);
            UnpackFloat2(command.Payload2, out float originZ, out float range);
            UnpackFloat2(command.Payload3, out float directionX, out float directionY);
            UnpackFloat2(command.Payload4, out float directionZ, out _);

            float3 direction = new float3(directionX, directionY, directionZ);
            float directionLengthSq = math.lengthsq(direction);
            if (directionLengthSq <= 0.0001f || range <= 0f)
                return false;

            direction *= math.rsqrt(directionLengthSq);
            int layerMask = unchecked((int)(command.Payload5 & 0xFFFFFFFFUL));
            if (layerMask == 0)
                layerMask = HectonLayerMasks.DefaultRaycastLayerMask;

            RaycastCommand raycastCommand = new RaycastCommand
            {
                from = new Vector3(originX, originY, originZ),
                direction = new Vector3(direction.x, direction.y, direction.z),
                distance = range,
                queryParameters = new QueryParameters
                {
                    layerMask = layerMask,
                    hitTriggers = QueryTriggerInteraction.Ignore,
                    hitBackfaces = false,
                    hitMultipleFaces = false
                }
            };

            _raycastRequestRecords[slot] = new ModRaycastRequestRecord
            {
                ModHash = command.ModHash,
                RequestId = command.RequestId,
                IsActive = 1,
                Reserved0 = 0,
                Reserved1 = 0
            };

            if (SystemDispatcher.QueueDispatcherRaycast(_raycastReceiver, slot, in raycastCommand))
                return true;

            _raycastRequestRecords[slot] = default;
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

        private static void ConsumeDispatcherRaycastHit(int slot, in RaycastHit hit)
        {
            if ((uint)slot >= (uint)_raycastRequestRecords.Length)
                return;

            ModRaycastRequestRecord record = _raycastRequestRecords[slot];
            _raycastRequestRecords[slot] = default;
            if (record.IsActive == 0)
                return;

            bool hasHit = hit.collider != null && hit.distance > 0f;
            ModRaycastResultPayload payload = new ModRaycastResultPayload
            {
                ModHash = record.ModHash,
                RequestId = record.RequestId,
                Status = hasHit ? (uint)ModRaycastResultStatus.Hit : (uint)ModRaycastResultStatus.Miss,
                ColliderInstanceId = hasHit ? unchecked((int)EntityId.ToULong(hit.collider.GetEntityId())) : 0,
                Layer = hasHit ? hit.collider.gameObject.layer : -1,
                Distance = hasHit ? hit.distance : 0f,
                Point = hasHit ? new float3(hit.point.x, hit.point.y, hit.point.z) : default,
                Normal = hasHit ? new float3(hit.normal.x, hit.normal.y, hit.normal.z) : default
            };

            _pendingRaycastResults.Enqueue(payload);
            _queuedRaycastResultCount++;
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
            if (!_pendingRejectEvents.IsCreated)
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

        private static void EnqueueMemoryEvictionEvent(uint modHash, long trackedHeapBytes)
        {
            if (!_pendingMemoryEvictionEvents.IsCreated)
                return;

            _pendingMemoryEvictionEvents.Enqueue(new ModCriticalMemoryEvictionPayload
            {
                ModHash = modHash,
                TrackedHeapBytes = unchecked((ulong)(trackedHeapBytes > 0L ? trackedHeapBytes : 0L)),
                LimitBytes = unchecked((uint)ModHeapQuotaBytes),
                Reason = (uint)ModCommandRejectReason.HeapQuotaExceeded
            });
            _queuedMemoryEvictionEventCount++;
        }

        private static void FlushDeferredEventQueues()
        {
            while (_queuedRaycastResultCount > 0 && _pendingRaycastResults.TryDequeue(out ModRaycastResultPayload raycastPayload))
            {
                _queuedRaycastResultCount--;
                HectonEventBus.Publish(in raycastPayload);
            }

            while (_queuedRejectEventCount > 0 && _pendingRejectEvents.TryDequeue(out ModInteractionRejectedPayload rejectPayload))
            {
                _queuedRejectEventCount--;
                HectonEventBus.Publish(in rejectPayload);
            }

            while (_queuedMemoryEvictionEventCount > 0 && _pendingMemoryEvictionEvents.TryDequeue(out ModCriticalMemoryEvictionPayload evictionPayload))
            {
                _queuedMemoryEvictionEventCount--;
                HectonEventBus.Publish(in evictionPayload);
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
                default:
                    return false;
            }
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
            Vector3 offset = HectonFloatingOrigin.CurrentTotalOffset;
            double cellSize = AupCellSizeMeters;
            double runtimeX = (grid.x * cellSize) + local.x - offset.x;
            double runtimeY = (grid.y * cellSize) + local.y - offset.y;
            double runtimeZ = (grid.z * cellSize) + local.z - offset.z;
            return new float3((float)runtimeX, (float)runtimeY, (float)runtimeZ);
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

        private static void UnpackFloat2(ulong packed, out float a, out float b)
        {
            a = math.asfloat(unchecked((uint)(packed & 0xFFFFFFFFUL)));
            b = math.asfloat(unchecked((uint)(packed >> 32)));
        }

        private static void DisposeQueue<TPayload>(ref NativeQueue<TPayload> queue)
            where TPayload : unmanaged
        {
            if (!queue.IsCreated)
                return;

            queue.Dispose();
            queue = default;
        }

        private sealed class ModRaycastReceiver : IDispatcherRaycastReceiver
        {
            public void ConsumeDispatcherRaycastHit(int requestId, in RaycastHit hit)
            {
                ModCommandDispatcher.ConsumeDispatcherRaycastHit(requestId, in hit);
            }
        }
    }
}
