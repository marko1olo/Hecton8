using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Caves;
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
    }

    /// <summary>
    /// Dispatcher-owned lock-free structural command queue.
    /// Jobs publish blittable intent only; the main thread resolves targets and applies mutations in LateUpdate.
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

        private static readonly uint _storageCommitOverflowWarningHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommitOverflow"));
        private static readonly uint _storageCommitQueueHash = unchecked((uint)LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommit"));

        // COLD ALLOC: Dictionary<int, GameObject>[256] - structural command target registry keyed by queue token - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<int, GameObject> _targetsByToken = new Dictionary<int, GameObject>(256);
        // COLD ALLOC: Dictionary<ulong, int>[256] - GameObject entity-id to structural command token map - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<ulong, int> _tokensByInstanceId = new Dictionary<ulong, int>(256);
        // COLD ALLOC: List<int>[64] - recycled structural command target tokens - owner: ThreadSafeCommandQueue
        private static readonly List<int> _freeTokens = new List<int>(64);
        // COLD ALLOC: object[8] - storage reservation acknowledgement listeners drained after command queue, object-backed to avoid interface arrays - owner: ThreadSafeCommandQueue
        private static readonly object[] _storageReservationCommitListeners = new object[StorageReservationCommitListenerCapacity];

        private static NativeQueue<EntityCommand> _pendingCommands;
        private static NativeQueue<StorageReservationCommitResolvedPayload> _pendingStorageReservationCommitResolved;
        private static int _nextToken = 1;
        private static int _pendingStorageReservationCommitResolvedCount;
        private static int _storageReservationCommitListenerCount;
        private static int _lastStorageReservationCommitOverflowWarningFrame = -1;

        /// <summary>
        /// True once the structural command queue has allocated its persistent native storage.
        /// </summary>
        public static bool IsReady => _pendingCommands.IsCreated;

        public static int PendingCount => _pendingCommands.IsCreated ? _pendingCommands.Count : 0;

        public static int PendingStorageReservationCommitResolvedCount => _pendingStorageReservationCommitResolvedCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        public static void Initialize()
        {
            if (_pendingCommands.IsCreated)
                return;

            _pendingCommands = new NativeQueue<EntityCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<EntityCommand>(Persistent) - structural command ingress drained by SystemDispatcher LateUpdate - owner: ThreadSafeCommandQueue
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingCommands,
                MaxMainThreadCommandsPerDrain,
                nameof(ThreadSafeCommandQueue),
                nameof(_pendingCommands),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _pendingCommands, MaxMainThreadCommandsPerDrain);
        }

        public static void Register(IStorageReservationCommitResolvedListener listener)
        {
            if (listener == null)
                return;

            if (IndexOfStorageReservationCommitListener(listener) >= 0)
                return;

            if (_storageReservationCommitListenerCount >= StorageReservationCommitListenerCapacity)
            {
                LogStorageReservationCommitListenerCapacityExceeded();
                return;
            }

            _storageReservationCommitListeners[_storageReservationCommitListenerCount] = listener;
            _storageReservationCommitListenerCount++;
        }

        public static void Unregister(IStorageReservationCommitResolvedListener listener)
        {
            if (listener == null)
                return;

            int index = IndexOfStorageReservationCommitListener(listener);
            if (index < 0)
                return;

            _storageReservationCommitListenerCount--;
            if (index < _storageReservationCommitListenerCount)
                _storageReservationCommitListeners[index] = _storageReservationCommitListeners[_storageReservationCommitListenerCount];

            _storageReservationCommitListeners[_storageReservationCommitListenerCount] = null;
        }

        public static void Enqueue(in EntityCommand command)
        {
            if (command.CommandType == EntityCommandType.None)
                return;

            if (RequiresGameObjectTarget(command.CommandType) && command.TargetToken <= 0)
                return;

            Initialize();
            _pendingCommands.Enqueue(command);
        }

        public static int RegisterGameObjectTarget(GameObject instance)
        {
            if (instance == null)
                return 0;

            ulong instanceId = EntityId.ToULong(instance.GetEntityId());
            if (_tokensByInstanceId.TryGetValue(instanceId, out int existingToken))
                return existingToken;

            int token = AllocateToken();
            _targetsByToken[token] = instance;
            _tokensByInstanceId[instanceId] = token;
            return token;
        }

        public static bool TryGetGameObjectTargetToken(GameObject instance, out int token)
        {
            token = 0;
            if (instance == null)
                return false;

            return _tokensByInstanceId.TryGetValue(EntityId.ToULong(instance.GetEntityId()), out token);
        }

        public static void UnregisterGameObjectTarget(GameObject instance)
        {
            if (instance == null)
                return;

            if (_tokensByInstanceId.TryGetValue(EntityId.ToULong(instance.GetEntityId()), out int token))
                UnregisterToken(token, instance);
        }

        public static void Clear()
        {
            if (_pendingCommands.IsCreated)
            {
                while (_pendingCommands.TryDequeue(out _))
                {
                }
            }

            if (_pendingStorageReservationCommitResolved.IsCreated)
            {
                while (_pendingStorageReservationCommitResolved.TryDequeue(out _))
                {
                }
            }

            _targetsByToken.Clear();
            _tokensByInstanceId.Clear();
            _freeTokens.Clear();
            _nextToken = 1;
            _pendingStorageReservationCommitResolvedCount = 0;
        }

        public static bool DrainMainThread()
        {
            if (!_pendingCommands.IsCreated)
                return true;

            int remainingBudget = MaxMainThreadCommandsPerDrain;
            while (remainingBudget > 0 && !_pendingCommands.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingCommands.TryDequeue(out EntityCommand command))
                    break;

                ExecuteCommand(in command);
                remainingBudget--;
            }

            return _pendingCommands.IsEmpty();
        }

        public static bool FlushStorageReservationCommitResolvedEvents()
        {
            if (!_pendingStorageReservationCommitResolved.IsCreated)
                return true;

            int scanBudget = _pendingStorageReservationCommitResolvedCount > 0
                ? _pendingStorageReservationCommitResolvedCount
                : StorageReservationCommitEventCapacity;
            while (scanBudget-- > 0 && !_pendingStorageReservationCommitResolved.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!_pendingStorageReservationCommitResolved.TryDequeue(out StorageReservationCommitResolvedPayload payload))
                    break;

                if (_pendingStorageReservationCommitResolvedCount > 0)
                    _pendingStorageReservationCommitResolvedCount--;

                int count = _storageReservationCommitListenerCount;
                for (int i = count - 1; i >= 0; i--)
                {
                    IStorageReservationCommitResolvedListener listener = _storageReservationCommitListeners[i] as IStorageReservationCommitResolvedListener;
                    if (listener != null)
                        listener.OnStorageReservationCommitResolved(in payload);
                }
            }

            if (_pendingStorageReservationCommitResolved.IsEmpty())
                _pendingStorageReservationCommitResolvedCount = 0;

            return _pendingStorageReservationCommitResolved.IsEmpty();
        }

        public static void Shutdown()
        {
            if (_pendingCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ThreadSafeCommandQueue), nameof(_pendingCommands));
                _pendingCommands.Dispose();
                _pendingCommands = default;
            }

            if (_pendingStorageReservationCommitResolved.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ThreadSafeCommandQueue), nameof(_pendingStorageReservationCommitResolved));
                _pendingStorageReservationCommitResolved.Dispose();
                _pendingStorageReservationCommitResolved = default;
            }

            _targetsByToken.Clear();
            _tokensByInstanceId.Clear();
            _freeTokens.Clear();
            System.Array.Clear(_storageReservationCommitListeners, 0, _storageReservationCommitListenerCount);
            _storageReservationCommitListenerCount = 0;
            _nextToken = 1;
            _pendingStorageReservationCommitResolvedCount = 0;
            _lastStorageReservationCommitOverflowWarningFrame = -1;
        }

        private static int AllocateToken()
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
                    ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                    if (pool != null)
                        pool.Despawn(instance, Mathf.Max(0f, command.FloatValue));
                    else
                        Object.Destroy(instance);

                    UnregisterToken(command.TargetToken, instance);
                    break;
                }

                case EntityCommandType.DestroyGameObject:
                    Object.Destroy(instance);
                    UnregisterToken(command.TargetToken, instance);
                    break;

                case EntityCommandType.SetGameObjectActive:
                    instance.SetActive(command.IntValue != 0);
                    break;

                case EntityCommandType.SpawnGameObject:
                {
                    ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                    if (pool != null)
                        pool.Spawn(instance, command.VectorValue, Quaternion.identity);
                    break;
                }

                case EntityCommandType.ModifyVoxel:
                {
                    if (!instance.TryGetComponent(out HectonVoxelVolume volume))
                        break;

                    HectonVoxelEngine engine = HectonVoxelEngine.ActiveRuntimeInstance;
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
                    if (command.IntValue <= 0 || !instance.TryGetComponent(out IStorageReservationCommitTarget target))
                    {
                        RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, false);
                        break;
                    }

                    bool committed = target.TryCommitReservation(command.IntValue);
                    RaiseStorageReservationCommitResolved(command.SecondaryToken, command.IntValue, committed);
                    break;
                }
            }
        }

        private static void RaiseStorageReservationCommitResolved(int requesterId, int reservationId, bool committed)
        {
            if (requesterId <= 0)
                return;

            EnqueueStorageReservationCommitResolved(new StorageReservationCommitResolvedPayload
            {
                RequesterId = requesterId,
                ReservationId = reservationId,
                Committed = committed ? (byte)1 : (byte)0
            });
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

        private static void EnsureStorageReservationCommitResolvedQueue()
        {
            if (_pendingStorageReservationCommitResolved.IsCreated)
                return;

            _pendingStorageReservationCommitResolved = new NativeQueue<StorageReservationCommitResolvedPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<StorageReservationCommitResolvedPayload>[64] - deferred storage reservation acknowledgements - owner: ThreadSafeCommandQueue
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingStorageReservationCommitResolved,
                StorageReservationCommitEventCapacity,
                nameof(ThreadSafeCommandQueue),
                nameof(_pendingStorageReservationCommitResolved),
                NativeAllocationLifetime.Session);
            PrewarmQueue(ref _pendingStorageReservationCommitResolved, StorageReservationCommitEventCapacity);
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
            if (_pendingStorageReservationCommitResolvedCount >= StorageReservationCommitEventCapacity)
            {
                ReportStorageReservationCommitOverflowOncePerFrame();
                return false;
            }

            EnsureStorageReservationCommitResolvedQueue();
            _pendingStorageReservationCommitResolved.Enqueue(payload);
            _pendingStorageReservationCommitResolvedCount++;
            return true;
        }

        private static void ReportStorageReservationCommitOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastStorageReservationCommitOverflowWarningFrame == frame)
                return;

            _lastStorageReservationCommitOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _storageCommitOverflowWarningHash,
                _storageCommitQueueHash,
                StorageReservationCommitEventCapacity);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogStorageReservationCommitListenerCapacityExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[ThreadSafeCommandQueue] Storage reservation commit listener capacity exceeded. capacity=" +
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
            if (_targetsByToken.TryGetValue(token, out instance) && instance != null)
                return true;

            instance = null;
            if (_targetsByToken.ContainsKey(token))
                _targetsByToken.Remove(token);
            return false;
        }

        private static void UnregisterToken(int token, GameObject instance)
        {
            _targetsByToken.Remove(token);
            if (instance != null)
                _tokensByInstanceId.Remove(EntityId.ToULong(instance.GetEntityId()));
            _freeTokens.Add(token);
        }
    }
}
