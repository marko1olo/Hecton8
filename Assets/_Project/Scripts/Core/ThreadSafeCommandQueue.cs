using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton.Localization;
using Hecton8.Caves;
using Hecton8.World;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Structural command opcode consumed on the main thread after jobs finish.
    /// </summary>
    public enum EntityCommandType : byte
    {
        None = 0,
        DespawnGameObject = 1,
        DestroyGameObject = 2,
        SetGameObjectActive = 3,
        SpawnGameObject = 4,
        ModifyVoxel = 5,
        CommitStorageReservation = 6,
        OpenPDATab = 7,
        ClosePDA = 8,
        UndoPDAState = 9,
    }

    /// <summary>
    /// Blittable structural command payload authored by jobs and drained in the dispatcher late-frame window.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct EntityCommand
    {
        [FieldOffset(0)] public EntityCommandType CommandType;
        [FieldOffset(1)] private byte _pad0;
        [FieldOffset(2)] private ushort _pad1;
        [FieldOffset(4)] public int TargetToken;
        [FieldOffset(8)] public int SecondaryToken;
        [FieldOffset(12)] public int IntValue;
        [FieldOffset(16)] public float FloatValue;
        [FieldOffset(20)] public Vector3 VectorValue;

        public static EntityCommand CreateDespawn(int targetToken, float delaySeconds = 0f)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.DespawnGameObject,
                TargetToken = targetToken,
                SecondaryToken = 0,
                IntValue = 0,
                FloatValue = delaySeconds,
                VectorValue = default
            };
        }

        public static EntityCommand CreateDestroy(int targetToken)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.DestroyGameObject,
                TargetToken = targetToken,
                SecondaryToken = 0,
                IntValue = 0,
                FloatValue = 0f,
                VectorValue = default
            };
        }

        public static EntityCommand CreateSetActive(int targetToken, bool value)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.SetGameObjectActive,
                TargetToken = targetToken,
                SecondaryToken = 0,
                IntValue = value ? 1 : 0,
                FloatValue = 0f,
                VectorValue = default
            };
        }

        public static EntityCommand CreateSpawn(int prefabToken, Vector3 runtimePosition)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.SpawnGameObject,
                TargetToken = prefabToken,
                SecondaryToken = 0,
                IntValue = 0,
                FloatValue = 0f,
                VectorValue = runtimePosition
            };
        }

        public static EntityCommand CreateModifyVoxelCarve(int volumeToken, Vector3 absolutePosition, float radius, byte materialId = 0)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.ModifyVoxel,
                TargetToken = volumeToken,
                SecondaryToken = 0,
                IntValue = materialId,
                FloatValue = radius,
                VectorValue = absolutePosition
            };
        }

        public static EntityCommand CreateCommitStorageReservation(int crateToken, int reservationId)
        {
            return CreateCommitStorageReservation(crateToken, reservationId, 0);
        }

        /// <summary>
        /// Creates a storage reservation commit command with an optional requester id for transaction acknowledgement.
        /// </summary>
        /// <param name="crateToken">Registered storage crate target token.</param>
        /// <param name="reservationId">Prepared storage reservation id.</param>
        /// <param name="requesterId">Optional producer id receiving commit success/failure notification.</param>
        /// <returns>Blittable command payload for the structural command queue.</returns>
        public static EntityCommand CreateCommitStorageReservation(int crateToken, int reservationId, int requesterId)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.CommitStorageReservation,
                TargetToken = crateToken,
                SecondaryToken = requesterId,
                IntValue = reservationId,
                FloatValue = 0f,
                VectorValue = default
            };
        }

        public static EntityCommand CreateOpenPDATab(int tabIndex)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.OpenPDATab,
                TargetToken = 0,
                SecondaryToken = 0,
                IntValue = tabIndex,
                FloatValue = 0f,
                VectorValue = default
            };
        }

        public static EntityCommand CreateClosePDA()
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.ClosePDA,
                TargetToken = 0,
                SecondaryToken = 0,
                IntValue = 0,
                FloatValue = 0f,
                VectorValue = default
            };
        }

        public static EntityCommand CreateUndoPDAState(int framesBack = 1)
        {
            return new EntityCommand
            {
                CommandType = EntityCommandType.UndoPDAState,
                TargetToken = 0,
                SecondaryToken = 0,
                IntValue = Mathf.Max(1, framesBack),
                FloatValue = 0f,
                VectorValue = default
            };
        }
    }

    public interface IStorageReservationCommitTarget
    {
        bool TryCommitReservation(int reservationId);

        void ReleaseReservation(int reservationId);
    }

    /// <summary>
    /// Dispatcher-owned structural command queue.
    /// Producers publish blittable intent only; the main thread resolves targets and applies mutations in LateUpdate.
    /// </summary>
    public static class ThreadSafeCommandQueue
    {
        private const int MaxMainThreadCommandsPerDrain = 256;
        private const int StorageReservationCommitListenerCapacity = 8;
        private const int StorageReservationCommitEventCapacity = 64;

        /// <summary>
        /// Storage reservation commit acknowledgement payload emitted after the command has been applied or rejected.
        /// </summary>
        [StructLayout(LayoutKind.Explicit, Size = 16)]
        public struct StorageReservationCommitResolvedPayload
        {
            [FieldOffset(0)] public int RequesterId;
            [FieldOffset(4)] public int ReservationId;
            [FieldOffset(8)] public byte Committed;
            [FieldOffset(9)] private byte _padding0;
            [FieldOffset(10)] private ushort _padding1;
            [FieldOffset(12)] private uint _padding2;
        }

        /// <summary>
        /// Listener for deferred storage reservation commit acknowledgements.
        /// </summary>
        public interface IStorageReservationCommitResolvedListener
        {
            void OnStorageReservationCommitResolved(in StorageReservationCommitResolvedPayload payload);
        }

        private static readonly uint _commandOverflowWarningHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.CommandOverflow"));
        private static readonly uint _commandQueueHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.Commands"));
        private static readonly uint _storageCommitOverflowWarningHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommitOverflow"));
        private static readonly uint _storageCommitQueueHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommit"));
        private static readonly uint _storageCommitListenerExceptionWarningHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommitListenerException"));
        private static readonly uint _storageCommitListenerCapacityWarningHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommitListenerCapacity"));

        // COLD ALLOC: Dictionary<int, GameObject>[256] - structural command target registry keyed by queue token - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<int, GameObject> _targetsByToken = new Dictionary<int, GameObject>(256);
        // COLD ALLOC: Dictionary<ulong, int>[256] - GameObject entity-id to structural command token map - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<ulong, int> _tokensByInstanceId = new Dictionary<ulong, int>(256);
        // COLD ALLOC: Dictionary<int, HectonVoxelVolume>[64] - optional voxel command target cache keyed by queue token - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<int, HectonVoxelVolume> _voxelVolumesByToken = new Dictionary<int, HectonVoxelVolume>(64);
        // COLD ALLOC: Dictionary<int, IStorageReservationCommitTarget>[64] - optional storage command target cache keyed by queue token - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<int, IStorageReservationCommitTarget> _storageCommitTargetsByToken = new Dictionary<int, IStorageReservationCommitTarget>(64);
        // COLD ALLOC: HashSet<int>[64] - one-shot structural command target tokens that must not clear persistent reverse maps - owner: ThreadSafeCommandQueue
        private static readonly HashSet<int> _oneShotTargetTokens = new HashSet<int>(64);
        // COLD ALLOC: List<int>[64] - recycled structural command target tokens - owner: ThreadSafeCommandQueue
        private static readonly List<int> _freeTokens = new List<int>(64);
        // COLD ALLOC: object[8] - storage reservation acknowledgement listeners drained after command queue, object-backed to avoid interface arrays - owner: ThreadSafeCommandQueue
        private static readonly object[] _storageReservationCommitListeners = new object[StorageReservationCommitListenerCapacity];
        // COLD ALLOC: object[8] - fixed dispatch copy to call listeners outside the listener gate - owner: ThreadSafeCommandQueue
        private static readonly object[] _storageReservationCommitDispatchBuffer = new object[StorageReservationCommitListenerCapacity];
        // COLD ALLOC: EntityCommand[256] - save-snapshot command sieve preserving non-storage commands while abandoning transient storage commits - owner: ThreadSafeCommandQueue
        private static readonly EntityCommand[] _persistenceSnapshotCommandBuffer = new EntityCommand[MaxMainThreadCommandsPerDrain];

        private static NativeQueue<EntityCommand> _pendingCommands;
        private static NativeQueue<StorageReservationCommitResolvedPayload> _pendingStorageReservationCommitResolved;
        private static int _nextToken = 1;
        private static int _pendingCommandCount;
        private static int _droppedCommandCount;
        private static int _pendingStorageReservationCommitResolvedCount;
        private static int _storageReservationCommitOverflowCount;
        private static int _storageReservationCommitListenerExceptionCount;
        private static int _storageReservationCommitListenerCapacityExceededCount;
        private static int _storageReservationCommitListenerCount;
        private static int _storageReservationCommitListenerGeneration;
        private static int _lastCommandOverflowWarningFrame = -1;
        private static int _lastStorageReservationCommitOverflowWarningFrame = -1;
        private static int _lastStorageReservationCommitListenerExceptionWarningFrame = -1;
        private static int _pendingCommandsReady;
        private static int _pendingStorageReservationCommitResolvedReady;
        private static int _pendingCommandsSentinelId;
        private static int _pendingStorageReservationCommitResolvedSentinelId;
        private static int _commandQueueGate;
        private static int _storageReservationCommitResolvedGate;
        private static int _targetRegistryGate;
        private static int _storageReservationCommitListenerGate;
        private static int _persistenceSnapshotGate;
        private static IObjectPoolService _objectPool;

        /// <summary>
        /// True once the structural command queue has allocated its persistent native storage.
        /// </summary>
        public static bool IsReady => Volatile.Read(ref _pendingCommandsReady) != 0;

        public static int PendingCount => Volatile.Read(ref _pendingCommandCount);

        public static int DroppedCommandCount => Volatile.Read(ref _droppedCommandCount);

        public static int PendingStorageReservationCommitResolvedCount => Volatile.Read(ref _pendingStorageReservationCommitResolvedCount);

        public static int StorageReservationCommitOverflowCount => Volatile.Read(ref _storageReservationCommitOverflowCount);

        public static int StorageReservationCommitListenerExceptionCount => Volatile.Read(ref _storageReservationCommitListenerExceptionCount);

        public static int StorageReservationCommitListenerCapacityExceededCount => Volatile.Read(ref _storageReservationCommitListenerCapacityExceededCount);

        public static int StorageReservationCommitListenerGeneration => Volatile.Read(ref _storageReservationCommitListenerGeneration);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void Initialize()
        {
            if (Volatile.Read(ref _pendingCommandsReady) != 0)
                return;

            EnterGate(ref _commandQueueGate);
            try
            {
                if (Volatile.Read(ref _pendingCommandsReady) == 0)
                {
                    _pendingCommands = CreateTrackedPersistentQueue<EntityCommand>(
                        MaxMainThreadCommandsPerDrain,
                        nameof(_pendingCommands),
                        out _pendingCommandsSentinelId); // COLD ALLOC: NativeQueue<EntityCommand>(Persistent) - structural command ingress drained by SystemDispatcher LateUpdate - owner: ThreadSafeCommandQueue
                    Volatile.Write(ref _pendingCommandsReady, 1);
                }
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }

            CacheRegistryServicesCold();
        }

        public static bool Register(IStorageReservationCommitResolvedListener listener)
        {
            if (listener == null)
                return false;

            bool capacityExceeded = false;
            bool registered = false;
            EnterGate(ref _storageReservationCommitListenerGate);
            try
            {
                if (IndexOfStorageReservationCommitListener(listener) >= 0)
                {
                    registered = true;
                }
                else
                {
                    if (_storageReservationCommitListenerCount >= StorageReservationCommitListenerCapacity)
                    {
                        capacityExceeded = true;
                    }
                    else
                    {
                        _storageReservationCommitListeners[_storageReservationCommitListenerCount] = listener;
                        _storageReservationCommitListenerCount++;
                        AdvanceStorageReservationCommitListenerGeneration();
                        registered = true;
                    }
                }
            }
            finally
            {
                ExitGate(ref _storageReservationCommitListenerGate);
            }

            if (capacityExceeded)
                ReportStorageReservationCommitListenerCapacityExceeded();

            return registered;
        }

        public static void Unregister(IStorageReservationCommitResolvedListener listener)
        {
            if (listener == null)
                return;

            EnterGate(ref _storageReservationCommitListenerGate);
            try
            {
                int index = IndexOfStorageReservationCommitListener(listener);
                if (index < 0)
                    return;

                _storageReservationCommitListenerCount--;
                if (index < _storageReservationCommitListenerCount)
                    _storageReservationCommitListeners[index] = _storageReservationCommitListeners[_storageReservationCommitListenerCount];

                _storageReservationCommitListeners[_storageReservationCommitListenerCount] = null;
                AdvanceStorageReservationCommitListenerGeneration();
            }
            finally
            {
                ExitGate(ref _storageReservationCommitListenerGate);
            }
        }

        public static void Enqueue(in EntityCommand command)
        {
            TryEnqueue(in command);
        }

        public static bool TryEnqueue(in EntityCommand command)
        {
            if (command.CommandType == EntityCommandType.None)
                return false;

            if (RequiresGameObjectTarget(command.CommandType) && command.TargetToken <= 0)
                return false;

            try
            {
                Initialize();
                bool reportOverflow = false;
                bool enqueued = false;
                EnterGate(ref _commandQueueGate);
                try
                {
                    if (Volatile.Read(ref _pendingCommandsReady) != 0)
                    {
                        if (_pendingCommandCount >= MaxMainThreadCommandsPerDrain)
                        {
                            _droppedCommandCount++;
                            reportOverflow = true;
                        }
                        else
                        {
                            _pendingCommands.Enqueue(command);
                            _pendingCommandCount++;
                            enqueued = true;
                        }
                    }
                }
                finally
                {
                    ExitGate(ref _commandQueueGate);
                }

                if (enqueued)
                    return true;

                if (command.CommandType == EntityCommandType.CommitStorageReservation && command.TargetToken > 0)
                    TryUnregisterOneShotTarget(command.TargetToken);

                if (reportOverflow)
                    ReportCommandOverflowOncePerFrame();

                return false;
            }
            catch (System.Exception exception) when (!(exception is FatalArchitectureException))
            {
                if (command.CommandType == EntityCommandType.CommitStorageReservation && command.TargetToken > 0)
                    TryUnregisterOneShotTarget(command.TargetToken);

                MarkCommandQueueUnavailableIfNativeStorageMissing();
                LogQueueEnqueueException(exception);
                return false;
            }
        }

        public static int RegisterGameObjectTarget(GameObject instance)
        {
            if (instance == null)
                return 0;

            ulong instanceId = EntityId.ToULong(instance.GetEntityId());
            HectonVoxelVolume volume = null;
            IStorageReservationCommitTarget storageTarget = null;
            if (instance.TryGetComponent(out HectonVoxelVolume resolvedVolume))
                volume = resolvedVolume;

            if (instance.TryGetComponent(out IStorageReservationCommitTarget resolvedStorageTarget))
                storageTarget = resolvedStorageTarget;

            EnterGate(ref _targetRegistryGate);
            try
            {
                if (_tokensByInstanceId.TryGetValue(instanceId, out int existingToken))
                    return existingToken;

                int token = AllocateTokenLocked();
                _targetsByToken[token] = instance;
                _tokensByInstanceId[instanceId] = token;
                if (volume != null)
                    _voxelVolumesByToken[token] = volume;

                if (storageTarget != null)
                    _storageCommitTargetsByToken[token] = storageTarget;

                return token;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        public static int RegisterOneShotGameObjectTarget(GameObject instance)
        {
            if (instance == null)
                return 0;

            HectonVoxelVolume volume = null;
            IStorageReservationCommitTarget storageTarget = null;
            if (instance.TryGetComponent(out HectonVoxelVolume resolvedVolume))
                volume = resolvedVolume;

            if (instance.TryGetComponent(out IStorageReservationCommitTarget resolvedStorageTarget))
                storageTarget = resolvedStorageTarget;

            EnterGate(ref _targetRegistryGate);
            try
            {
                int token = AllocateTokenLocked();
                _targetsByToken[token] = instance;
                _oneShotTargetTokens.Add(token);
                if (volume != null)
                    _voxelVolumesByToken[token] = volume;

                if (storageTarget != null)
                    _storageCommitTargetsByToken[token] = storageTarget;

                return token;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        public static bool TryGetGameObjectTargetToken(GameObject instance, out int token)
        {
            token = 0;
            if (instance == null)
                return false;

            EnterGate(ref _targetRegistryGate);
            try
            {
                return _tokensByInstanceId.TryGetValue(EntityId.ToULong(instance.GetEntityId()), out token);
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        public static void UnregisterGameObjectTarget(GameObject instance)
        {
            if (instance == null)
                return;

            EnterGate(ref _targetRegistryGate);
            try
            {
                if (_tokensByInstanceId.TryGetValue(EntityId.ToULong(instance.GetEntityId()), out int token))
                    UnregisterTokenLocked(token, instance);
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        public static void Clear()
        {
            DrainAbandonedPendingCommands(dispatchStorageReservationFailures: false);
            DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: false);

            EnterGate(ref _commandQueueGate);
            try
            {
                _pendingCommandCount = 0;
                _droppedCommandCount = 0;
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }

            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                _pendingStorageReservationCommitResolvedCount = 0;
                _storageReservationCommitOverflowCount = 0;
                _storageReservationCommitListenerExceptionCount = 0;
                _storageReservationCommitListenerCapacityExceededCount = 0;
                _lastStorageReservationCommitListenerExceptionWarningFrame = -1;
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }

            EnterGate(ref _targetRegistryGate);
            try
            {
                _targetsByToken.Clear();
                _tokensByInstanceId.Clear();
                _voxelVolumesByToken.Clear();
                _storageCommitTargetsByToken.Clear();
                _oneShotTargetTokens.Clear();
                _freeTokens.Clear();
                _nextToken = 1;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }

            Volatile.Write(ref _objectPool, null);
        }

        public static bool DrainMainThread()
        {
            if (Volatile.Read(ref _pendingCommandsReady) == 0)
                return true;

            int remainingBudget = MaxMainThreadCommandsPerDrain;
            while (remainingBudget > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                bool hasCommand = false;
                EntityCommand command = default;
                EnterGate(ref _commandQueueGate);
                try
                {
                    if (Volatile.Read(ref _pendingCommandsReady) == 0)
                        return true;

                    if (_pendingCommands.IsEmpty())
                    {
                        _pendingCommandCount = 0;
                        return true;
                    }

                    hasCommand = _pendingCommands.TryDequeue(out command);
                    if (!hasCommand)
                    {
                        _pendingCommandCount = 0;
                        return true;
                    }

                    if (_pendingCommandCount > 0)
                        _pendingCommandCount--;

                    if (_pendingCommands.IsEmpty())
                        _pendingCommandCount = 0;
                }
                finally
                {
                    ExitGate(ref _commandQueueGate);
                }

                if (hasCommand)
                    ExecuteCommand(in command);

                remainingBudget--;
            }

            return !HasPendingCommands();
        }

        public static bool FlushStorageReservationCommitResolvedEvents()
        {
            if (Volatile.Read(ref _pendingStorageReservationCommitResolvedReady) == 0)
                return true;

            int pendingCount = Volatile.Read(ref _pendingStorageReservationCommitResolvedCount);
            int scanBudget = pendingCount > 0
                ? pendingCount
                : StorageReservationCommitEventCapacity;
            while (scanBudget-- > 0)
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                bool hasPayload = false;
                StorageReservationCommitResolvedPayload payload = default;
                EnterGate(ref _storageReservationCommitResolvedGate);
                try
                {
                    if (Volatile.Read(ref _pendingStorageReservationCommitResolvedReady) == 0)
                        return true;

                    if (_pendingStorageReservationCommitResolved.IsEmpty())
                    {
                        _pendingStorageReservationCommitResolvedCount = 0;
                        return true;
                    }

                    hasPayload = _pendingStorageReservationCommitResolved.TryDequeue(out payload);
                    if (!hasPayload)
                    {
                        _pendingStorageReservationCommitResolvedCount = 0;
                        return true;
                    }

                    if (_pendingStorageReservationCommitResolvedCount > 0)
                        _pendingStorageReservationCommitResolvedCount--;

                    if (_pendingStorageReservationCommitResolved.IsEmpty())
                        _pendingStorageReservationCommitResolvedCount = 0;
                }
                finally
                {
                    ExitGate(ref _storageReservationCommitResolvedGate);
                }

                if (hasPayload)
                    DispatchStorageReservationCommitResolved(in payload);
            }

            return !HasPendingStorageReservationCommitResolved();
        }

        /// <summary>
        /// Closes transient storage commit edges before save serialization without executing unrelated structural commands.
        /// </summary>
        public static void PrepareStorageReservationCommitBridgeForPersistenceSnapshot()
        {
            EnterGate(ref _persistenceSnapshotGate);
            try
            {
                DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);
                DrainPendingStorageReservationCommitsForPersistenceSnapshot();
            }
            finally
            {
                ExitGate(ref _persistenceSnapshotGate);
            }
        }

        public static void Shutdown()
        {
            DrainAbandonedStorageReservationCommitResolvedEvents(dispatchPendingEvents: true);
            DrainAbandonedPendingCommands(dispatchStorageReservationFailures: true);

            EnterGate(ref _commandQueueGate);
            try
            {
                if (Volatile.Read(ref _pendingCommandsReady) != 0 ||
                    _pendingCommandsSentinelId > 0 ||
                    _pendingCommands.IsCreated)
                    DisposeTrackedPersistentQueue(ref _pendingCommands, ref _pendingCommandsSentinelId, ref _pendingCommandsReady);

                _pendingCommandCount = 0;
                _droppedCommandCount = 0;
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }

            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                if (Volatile.Read(ref _pendingStorageReservationCommitResolvedReady) != 0 ||
                    _pendingStorageReservationCommitResolvedSentinelId > 0 ||
                    _pendingStorageReservationCommitResolved.IsCreated)
                    DisposeTrackedPersistentQueue(
                        ref _pendingStorageReservationCommitResolved,
                        ref _pendingStorageReservationCommitResolvedSentinelId,
                        ref _pendingStorageReservationCommitResolvedReady);

                _pendingStorageReservationCommitResolvedCount = 0;
                _storageReservationCommitOverflowCount = 0;
                _storageReservationCommitListenerExceptionCount = 0;
                _lastStorageReservationCommitListenerExceptionWarningFrame = -1;
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }

            EnterGate(ref _targetRegistryGate);
            try
            {
                _targetsByToken.Clear();
                _tokensByInstanceId.Clear();
                _voxelVolumesByToken.Clear();
                _storageCommitTargetsByToken.Clear();
                _oneShotTargetTokens.Clear();
                _freeTokens.Clear();
                _nextToken = 1;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }

            EnterGate(ref _storageReservationCommitListenerGate);
            try
            {
                bool hadListeners = _storageReservationCommitListenerCount > 0;
                System.Array.Clear(_storageReservationCommitListeners, 0, _storageReservationCommitListenerCount);
                System.Array.Clear(_storageReservationCommitDispatchBuffer, 0, StorageReservationCommitListenerCapacity);
                _storageReservationCommitListenerCount = 0;
                _storageReservationCommitListenerCapacityExceededCount = 0;
                if (hadListeners)
                    AdvanceStorageReservationCommitListenerGeneration();
            }
            finally
            {
                ExitGate(ref _storageReservationCommitListenerGate);
            }

            _lastCommandOverflowWarningFrame = -1;
            _lastStorageReservationCommitOverflowWarningFrame = -1;
            Volatile.Write(ref _objectPool, null);
        }

        private static int AllocateTokenLocked()
        {
            int freeCount = _freeTokens.Count;
            if (freeCount > 0)
            {
                int index = freeCount - 1;
                int token = _freeTokens[index];
                _freeTokens.RemoveAt(index);
                return token;
            }

            return _nextToken++;
        }

        private static void ExecuteCommand(in EntityCommand command)
        {
            switch (command.CommandType)
            {
                case EntityCommandType.OpenPDATab:
                    UIStateStore.SetPDAOpenState(true, command.IntValue, 0f);
                    return;

                case EntityCommandType.ClosePDA:
                    UIStateStore.SetPDAOpenState(false, 0, 0f);
                    return;

                case EntityCommandType.UndoPDAState:
                    UIStateStore.TryRollbackPDAState(command.IntValue <= 0 ? 1 : command.IntValue);
                    return;
            }

            if (!TryResolveTarget(command.TargetToken, out GameObject instance))
            {
                if (command.CommandType == EntityCommandType.CommitStorageReservation)
                    RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, false);

                return;
            }

            switch (command.CommandType)
            {
                case EntityCommandType.DespawnGameObject:
                {
                    IObjectPoolService pool = ResolveObjectPoolCold();
                    if (ObjectPoolManager.TryResolvePoolForInstance(instance, pool, out IObjectPoolService ownerPool))
                        ownerPool.Despawn(instance, Mathf.Max(0f, command.FloatValue));
                    else
                        UnityEngine.Object.Destroy(instance);

                    UnregisterToken(command.TargetToken, instance);
                    break;
                }

                case EntityCommandType.DestroyGameObject:
                    UnityEngine.Object.Destroy(instance);
                    UnregisterToken(command.TargetToken, instance);
                    break;

                case EntityCommandType.SetGameObjectActive:
                    instance.SetActive(command.IntValue != 0);
                    break;

                case EntityCommandType.SpawnGameObject:
                {
                    IObjectPoolService pool = ResolveObjectPoolCold();
                    if (pool != null)
                        pool.Spawn(instance, command.VectorValue, Quaternion.identity);
                    break;
                }

                case EntityCommandType.ModifyVoxel:
                {
                    if (!TryResolveVoxelVolume(command.TargetToken, out HectonVoxelVolume volume))
                        break;

                    HectonVoxelEngine engine = null;
                    WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref engine);
                    VoxelDeltaProcessor deltaProcessor = engine != null ? engine.DeltaProcessor : null;
                    if (deltaProcessor == null)
                        break;

                    deltaProcessor.ApplyImmediateAbsoluteCrater(
                        volume,
                        command.VectorValue,
                        Mathf.Max(0f, command.FloatValue),
                        (byte)Mathf.Clamp(command.IntValue, 0, byte.MaxValue));
                    break;
                }

                case EntityCommandType.CommitStorageReservation:
                {
                    bool committed = false;
                    try
                    {
                        if (command.IntValue > 0 &&
                            TryResolveStorageCommitTarget(command.TargetToken, out IStorageReservationCommitTarget target))
                        {
                            committed = target.TryCommitReservation(command.IntValue);
                        }
                    }
                    catch (System.Exception exception) when (!(exception is FatalArchitectureException))
                    {
                        H8Debug.LogException(exception);
                    }
                    finally
                    {
                        TryUnregisterOneShotTarget(command.TargetToken);
                    }

                    RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, committed);
                    break;
                }
            }
        }

        private static void CacheRegistryServicesCold()
        {
            BindObjectPoolServiceCold(null);
        }

        internal static void BindObjectPoolServiceCold(IObjectPoolService objectPool)
        {
            ObjectPoolManager pool = objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                Volatile.Write(ref _objectPool, pool);
                return;
            }

            Volatile.Write(ref _objectPool, null);
        }

        private static IObjectPoolService ResolveObjectPoolCold()
        {
            ObjectPoolManager cached = Volatile.Read(ref _objectPool) as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
                return cached;

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                Volatile.Write(ref _objectPool, resolved);
                return resolved;
            }

            Volatile.Write(ref _objectPool, null);
            return null;
        }

        private static void RaiseStorageReservationCommitResolved(int requesterId, int reservationId, bool committed)
        {
            if (requesterId <= 0)
                return;

            StorageReservationCommitResolvedPayload payload = new StorageReservationCommitResolvedPayload
            {
                RequesterId = requesterId,
                ReservationId = reservationId,
                Committed = committed ? (byte)1 : (byte)0
            };

            bool enqueued = false;
            try
            {
                enqueued = EnqueueStorageReservationCommitResolved(in payload);
            }
            catch (System.Exception exception) when (!(exception is FatalArchitectureException))
            {
                MarkStorageReservationCommitResolvedQueueUnavailableIfNativeStorageMissing();
                LogQueueEnqueueException(exception);
            }

            if (!enqueued)
                DispatchStorageReservationCommitResolved(in payload);
        }

        private static void DispatchStorageReservationCommitResolvedFailure(int requesterId, int reservationId)
        {
            if (requesterId <= 0)
                return;

            StorageReservationCommitResolvedPayload payload = new StorageReservationCommitResolvedPayload
            {
                RequesterId = requesterId,
                ReservationId = reservationId,
                Committed = 0
            };
            DispatchStorageReservationCommitResolved(in payload);
        }

        private static int IndexOfStorageReservationCommitListener(IStorageReservationCommitResolvedListener listener)
        {
            for (int i = 0; i < _storageReservationCommitListenerCount; i++)
            {
                if (ReferenceEquals(_storageReservationCommitListeners[i], listener))
                    return i;
            }

            return -1;
        }

        private static void AdvanceStorageReservationCommitListenerGeneration()
        {
            unchecked
            {
                _storageReservationCommitListenerGeneration++;
            }
        }

        private static void DispatchStorageReservationCommitResolved(in StorageReservationCommitResolvedPayload payload)
        {
            int count;
            EnterGate(ref _storageReservationCommitListenerGate);
            try
            {
                count = _storageReservationCommitListenerCount;
                for (int i = 0; i < count; i++)
                    _storageReservationCommitDispatchBuffer[i] = _storageReservationCommitListeners[i];
            }
            finally
            {
                ExitGate(ref _storageReservationCommitListenerGate);
            }

            int dispatchIndex = count - 1;
            try
            {
                for (; dispatchIndex >= 0; dispatchIndex--)
                {
                    IStorageReservationCommitResolvedListener listener = _storageReservationCommitDispatchBuffer[dispatchIndex] as IStorageReservationCommitResolvedListener;
                    _storageReservationCommitDispatchBuffer[dispatchIndex] = null;
                    if (listener != null)
                        DispatchStorageReservationCommitResolvedToListener(listener, in payload);
                }
            }
            finally
            {
                for (; dispatchIndex >= 0; dispatchIndex--)
                    _storageReservationCommitDispatchBuffer[dispatchIndex] = null;
            }
        }

        private static void DispatchStorageReservationCommitResolvedToListener(
            IStorageReservationCommitResolvedListener listener,
            in StorageReservationCommitResolvedPayload payload)
        {
            try
            {
                listener.OnStorageReservationCommitResolved(in payload);
            }
            catch (System.Exception exception) when (!(exception is FatalArchitectureException))
            {
                ReportStorageReservationCommitListenerException(exception);
            }
        }

        private static void DisposeTrackedPersistentQueue<T>(ref NativeQueue<T> queue, ref int sentinelId, ref int readyFlag)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void DrainAbandonedPendingCommands(bool dispatchStorageReservationFailures)
        {
            while (TryDequeuePendingCommandForAbandon(out EntityCommand command))
                ResolveAbandonedPendingCommand(in command, dispatchStorageReservationFailures);
        }

        private static bool TryDequeuePendingCommandForAbandon(out EntityCommand command)
        {
            command = default;
            EnterGate(ref _commandQueueGate);
            try
            {
                if (!_pendingCommands.IsCreated || _pendingCommands.IsEmpty())
                {
                    _pendingCommandCount = 0;
                    return false;
                }

                if (!_pendingCommands.TryDequeue(out command))
                {
                    _pendingCommandCount = 0;
                    return false;
                }

                if (_pendingCommandCount > 0)
                    _pendingCommandCount--;

                if (_pendingCommands.IsEmpty())
                    _pendingCommandCount = 0;

                return true;
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }
        }

        private static void ResolveAbandonedPendingCommand(
            in EntityCommand command,
            bool dispatchStorageReservationFailures)
        {
            if (command.CommandType != EntityCommandType.CommitStorageReservation)
                return;

            ReleaseAbandonedStorageReservationCommit(in command);
            if (dispatchStorageReservationFailures)
                DispatchStorageReservationCommitResolvedFailure(command.SecondaryToken, command.IntValue);
        }

        private static void DrainPendingStorageReservationCommitsForPersistenceSnapshot()
        {
            int drainedCommandCount = 0;
            EnterGate(ref _commandQueueGate);
            try
            {
                if (!_pendingCommands.IsCreated || _pendingCommands.IsEmpty())
                {
                    _pendingCommandCount = 0;
                    return;
                }

                while (drainedCommandCount < _persistenceSnapshotCommandBuffer.Length &&
                       _pendingCommands.TryDequeue(out EntityCommand command))
                {
                    _persistenceSnapshotCommandBuffer[drainedCommandCount++] = command;
                }

                int requeuedCommandCount = 0;
                for (int i = 0; i < drainedCommandCount; i++)
                {
                    EntityCommand command = _persistenceSnapshotCommandBuffer[i];
                    if (command.CommandType == EntityCommandType.CommitStorageReservation)
                        continue;

                    _pendingCommands.Enqueue(command);
                    requeuedCommandCount++;
                }

                _pendingCommandCount = requeuedCommandCount;
                if (_pendingCommands.IsEmpty())
                    _pendingCommandCount = 0;
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }

            for (int i = 0; i < drainedCommandCount; i++)
            {
                EntityCommand command = _persistenceSnapshotCommandBuffer[i];
                _persistenceSnapshotCommandBuffer[i] = default;
                if (command.CommandType == EntityCommandType.CommitStorageReservation)
                    ResolveAbandonedPendingCommand(in command, dispatchStorageReservationFailures: true);
            }
        }

        private static void ReleaseAbandonedStorageReservationCommit(in EntityCommand command)
        {
            try
            {
                if (command.IntValue > 0 &&
                    TryResolveStorageCommitTarget(command.TargetToken, out IStorageReservationCommitTarget target))
                {
                    target.ReleaseReservation(command.IntValue);
                }
            }
            catch (System.Exception exception) when (!(exception is FatalArchitectureException))
            {
                H8Debug.LogException(exception);
            }
            finally
            {
                TryUnregisterOneShotTarget(command.TargetToken);
            }
        }

        private static void DrainAbandonedStorageReservationCommitResolvedEvents(bool dispatchPendingEvents)
        {
            while (TryDequeuePendingStorageReservationCommitResolvedForAbandon(out StorageReservationCommitResolvedPayload payload))
            {
                if (dispatchPendingEvents)
                    DispatchStorageReservationCommitResolved(in payload);
            }
        }

        private static bool TryDequeuePendingStorageReservationCommitResolvedForAbandon(
            out StorageReservationCommitResolvedPayload payload)
        {
            payload = default;
            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                if (!_pendingStorageReservationCommitResolved.IsCreated ||
                    _pendingStorageReservationCommitResolved.IsEmpty())
                {
                    _pendingStorageReservationCommitResolvedCount = 0;
                    return false;
                }

                if (!_pendingStorageReservationCommitResolved.TryDequeue(out payload))
                {
                    _pendingStorageReservationCommitResolvedCount = 0;
                    return false;
                }

                if (_pendingStorageReservationCommitResolvedCount > 0)
                    _pendingStorageReservationCommitResolvedCount--;

                if (_pendingStorageReservationCommitResolved.IsEmpty())
                    _pendingStorageReservationCommitResolvedCount = 0;

                return true;
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }
        }

        private static void MarkCommandQueueUnavailableIfNativeStorageMissing()
        {
            EnterGate(ref _commandQueueGate);
            try
            {
                if (!_pendingCommands.IsCreated)
                {
                    _pendingCommandCount = 0;
                    Volatile.Write(ref _pendingCommandsReady, 0);
                }
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }
        }

        private static void MarkStorageReservationCommitResolvedQueueUnavailableIfNativeStorageMissing()
        {
            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                if (!_pendingStorageReservationCommitResolved.IsCreated)
                {
                    _pendingStorageReservationCommitResolvedCount = 0;
                    Volatile.Write(ref _pendingStorageReservationCommitResolvedReady, 0);
                }
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }
        }

        private static void EnsureStorageReservationCommitResolvedQueue()
        {
            if (Volatile.Read(ref _pendingStorageReservationCommitResolvedReady) != 0)
                return;

            _pendingStorageReservationCommitResolved = CreateTrackedPersistentQueue<StorageReservationCommitResolvedPayload>(
                StorageReservationCommitEventCapacity,
                nameof(_pendingStorageReservationCommitResolved),
                out _pendingStorageReservationCommitResolvedSentinelId); // COLD ALLOC: NativeQueue<StorageReservationCommitResolvedPayload>[64] - deferred storage reservation acknowledgements - owner: ThreadSafeCommandQueue
            Volatile.Write(ref _pendingStorageReservationCommitResolvedReady, 1);
        }

        private static NativeQueue<T> CreateTrackedPersistentQueue<T>(int capacity, string label, out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            NativeQueue<T> queue = new NativeQueue<T>(Allocator.Persistent);
            try
            {
                sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                    queue,
                    capacity,
                    nameof(ThreadSafeCommandQueue),
                    label,
                    NativeAllocationLifetime.Session);
                if (sentinelId <= 0)
                    throw new System.InvalidOperationException("NativeMemorySentinel rejected ThreadSafeCommandQueue queue registration.");

                PrewarmQueue(ref queue, capacity);
                return queue;
            }
            catch (Exception exception)
            {
                Exception cleanupException = null;

                try
                {
                    int cleanupReadyFlag = queue.IsCreated ? 1 : 0;
                    DisposeTrackedPersistentQueue(ref queue, ref sentinelId, ref cleanupReadyFlag);
                }
                catch (Exception releaseException)
                {
                    cleanupException = releaseException;
                }

                if (cleanupException != null)
                    throw new System.AggregateException(
                        "ThreadSafeCommandQueue native queue creation failed and cleanup also failed.",
                        exception,
                        cleanupException);

                throw;
            }
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
        }

        private static bool EnqueueStorageReservationCommitResolved(in StorageReservationCommitResolvedPayload payload)
        {
            bool reportOverflow = false;
            bool enqueued = false;
            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                if (_pendingStorageReservationCommitResolvedCount >= StorageReservationCommitEventCapacity)
                {
                    if (_storageReservationCommitOverflowCount < int.MaxValue)
                        _storageReservationCommitOverflowCount++;

                    reportOverflow = true;
                }
                else
                {
                    EnsureStorageReservationCommitResolvedQueue();
                    _pendingStorageReservationCommitResolved.Enqueue(payload);
                    _pendingStorageReservationCommitResolvedCount++;
                    enqueued = true;
                }
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }

            if (reportOverflow)
                ReportStorageReservationCommitOverflowOncePerFrame();

            return enqueued;
        }

        private static void ReportCommandOverflowOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastCommandOverflowWarningFrame == frame)
                return;

            _lastCommandOverflowWarningFrame = frame;
            PublishQueuePerformanceWarningBestEffort(
                _commandOverflowWarningHash,
                _commandQueueHash,
                MaxMainThreadCommandsPerDrain);
        }

        private static void ReportStorageReservationCommitOverflowOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastStorageReservationCommitOverflowWarningFrame == frame)
                return;

            _lastStorageReservationCommitOverflowWarningFrame = frame;
            PublishQueuePerformanceWarningBestEffort(
                _storageCommitOverflowWarningHash,
                _storageCommitQueueHash,
                Mathf.Max(1, _storageReservationCommitOverflowCount));
        }

        private static void ReportStorageReservationCommitListenerException(System.Exception exception)
        {
            if (_storageReservationCommitListenerExceptionCount < int.MaxValue)
                _storageReservationCommitListenerExceptionCount++;

            ReportStorageReservationCommitListenerExceptionOncePerFrame();
            LogStorageReservationCommitListenerException(exception);
        }

        private static void ReportStorageReservationCommitListenerCapacityExceeded()
        {
            IncrementStorageReservationCommitListenerCapacityExceededCount();
            PublishQueuePerformanceWarningBestEffort(
                _storageCommitListenerCapacityWarningHash,
                _storageCommitQueueHash,
                StorageReservationCommitListenerCapacity);
            LogStorageReservationCommitListenerCapacityExceeded();
        }

        private static void IncrementStorageReservationCommitListenerCapacityExceededCount()
        {
            int currentCount;
            do
            {
                currentCount = Volatile.Read(ref _storageReservationCommitListenerCapacityExceededCount);
                if (currentCount >= int.MaxValue)
                    return;
            }
            while (Interlocked.CompareExchange(
                       ref _storageReservationCommitListenerCapacityExceededCount,
                       currentCount + 1,
                       currentCount) != currentCount);
        }

        private static void ReportStorageReservationCommitListenerExceptionOncePerFrame()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastStorageReservationCommitListenerExceptionWarningFrame == frame)
                return;

            _lastStorageReservationCommitListenerExceptionWarningFrame = frame;
            PublishQueuePerformanceWarningBestEffort(
                _storageCommitListenerExceptionWarningHash,
                _storageCommitQueueHash,
                Mathf.Max(1, _storageReservationCommitListenerExceptionCount));
        }

        private static void PublishQueuePerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (System.Exception telemetryException) when (!(telemetryException is FatalArchitectureException))
            {
                LogQueueTelemetryException(telemetryException);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStorageReservationCommitListenerException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogQueueTelemetryException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogQueueEnqueueException(System.Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            H8Debug.LogException(exception);
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStorageReservationCommitListenerCapacityExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[ThreadSafeCommandQueue] Storage reservation commit listener capacity exceeded. capacity=" +
                           StorageReservationCommitListenerCapacity);
#endif
        }

        private static bool RequiresGameObjectTarget(EntityCommandType commandType)
        {
            switch (commandType)
            {
                case EntityCommandType.OpenPDATab:
                case EntityCommandType.ClosePDA:
                case EntityCommandType.UndoPDAState:
                    return false;

                default:
                    return true;
            }
        }

        private static bool TryResolveTarget(int token, out GameObject instance)
        {
            EnterGate(ref _targetRegistryGate);
            try
            {
                if (_targetsByToken.TryGetValue(token, out instance))
                {
                    if (instance != null)
                        return true;

                    UnregisterTokenLocked(token, null);
                }

                instance = null;
                return false;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        private static bool TryResolveVoxelVolume(int token, out HectonVoxelVolume volume)
        {
            EnterGate(ref _targetRegistryGate);
            try
            {
                return _voxelVolumesByToken.TryGetValue(token, out volume) && volume != null;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        private static bool TryResolveStorageCommitTarget(int token, out IStorageReservationCommitTarget target)
        {
            EnterGate(ref _targetRegistryGate);
            try
            {
                return _storageCommitTargetsByToken.TryGetValue(token, out target) && target != null;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        private static void UnregisterToken(int token, GameObject instance)
        {
            EnterGate(ref _targetRegistryGate);
            try
            {
                UnregisterTokenLocked(token, instance);
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        private static bool TryUnregisterOneShotTarget(int token)
        {
            if (token <= 0)
                return false;

            EnterGate(ref _targetRegistryGate);
            try
            {
                if (!_oneShotTargetTokens.Contains(token))
                    return false;

                UnregisterTokenLocked(token, null);
                return true;
            }
            finally
            {
                ExitGate(ref _targetRegistryGate);
            }
        }

        private static void UnregisterTokenLocked(int token, GameObject instance)
        {
            bool removed = _targetsByToken.Remove(token);
            removed |= _oneShotTargetTokens.Remove(token);
            removed |= _voxelVolumesByToken.Remove(token);
            removed |= _storageCommitTargetsByToken.Remove(token);
            if (instance != null)
                removed |= _tokensByInstanceId.Remove(EntityId.ToULong(instance.GetEntityId()));
            else
                removed |= RemoveInstanceTokenValueLocked(token);

            if (removed)
                _freeTokens.Add(token);
        }

        private static bool RemoveInstanceTokenValueLocked(int token)
        {
            ulong staleInstanceId = 0UL;
            bool found = false;
            foreach (KeyValuePair<ulong, int> entry in _tokensByInstanceId)
            {
                if (entry.Value != token)
                    continue;

                staleInstanceId = entry.Key;
                found = true;
                break;
            }

            if (found)
            {
                _tokensByInstanceId.Remove(staleInstanceId);
                return true;
            }

            return false;
        }

        private static bool HasPendingCommands()
        {
            EnterGate(ref _commandQueueGate);
            try
            {
                if (Volatile.Read(ref _pendingCommandsReady) == 0)
                    return false;

                bool hasPending = !_pendingCommands.IsEmpty();
                if (!hasPending)
                    _pendingCommandCount = 0;

                return hasPending;
            }
            finally
            {
                ExitGate(ref _commandQueueGate);
            }
        }

        private static bool HasPendingStorageReservationCommitResolved()
        {
            EnterGate(ref _storageReservationCommitResolvedGate);
            try
            {
                if (Volatile.Read(ref _pendingStorageReservationCommitResolvedReady) == 0)
                    return false;

                bool hasPending = !_pendingStorageReservationCommitResolved.IsEmpty();
                if (!hasPending)
                    _pendingStorageReservationCommitResolvedCount = 0;

                return hasPending;
            }
            finally
            {
                ExitGate(ref _storageReservationCommitResolvedGate);
            }
        }

        private static void EnterGate(ref int gate)
        {
            SpinWait spinWait = default;
            while (Interlocked.CompareExchange(ref gate, 1, 0) != 0)
                spinWait.SpinOnce();
        }

        private static void ExitGate(ref int gate)
        {
            Volatile.Write(ref gate, 0);
        }
    }
}
