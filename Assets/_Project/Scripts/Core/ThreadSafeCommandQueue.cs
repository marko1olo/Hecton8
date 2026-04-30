using System.Collections.Generic;
using Unity.Collections;
using Hecton8.Caves;
using Hecton8.Gameplay;
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
    }

    /// <summary>
    /// Blittable structural command payload authored by jobs and drained in the dispatcher late-frame window.
    /// </summary>
    public struct EntityCommand
    {
        public EntityCommandType CommandType;
        public int TargetToken;
        public int SecondaryToken;
        public int IntValue;
        public float FloatValue;
        public Vector3 VectorValue;

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
            return new EntityCommand
            {
                CommandType = EntityCommandType.CommitStorageReservation,
                TargetToken = crateToken,
                SecondaryToken = 0,
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
    }

    /// <summary>
    /// Dispatcher-owned lock-free structural command queue.
    /// Jobs publish blittable intent only; the main thread resolves targets and applies mutations in LateUpdate.
    /// </summary>
    public static class ThreadSafeCommandQueue
    {
        private const int MaxMainThreadCommandsPerDrain = 256;

        // COLD ALLOC: Dictionary<int, GameObject>[256] - structural command target registry keyed by queue token - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<int, GameObject> _targetsByToken = new Dictionary<int, GameObject>(256);
        // COLD ALLOC: Dictionary<ulong, int>[256] - GameObject entity-id to structural command token map - owner: ThreadSafeCommandQueue
        private static readonly Dictionary<ulong, int> _tokensByInstanceId = new Dictionary<ulong, int>(256);
        // COLD ALLOC: List<int>[64] - recycled structural command target tokens - owner: ThreadSafeCommandQueue
        private static readonly List<int> _freeTokens = new List<int>(64);

        private static NativeQueue<EntityCommand> _pendingCommands;
        private static int _nextToken = 1;

        /// <summary>
        /// True once the structural command queue has allocated its persistent native storage.
        /// </summary>
        public static bool IsReady => _pendingCommands.IsCreated;

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
        }

        public static NativeQueue<EntityCommand>.ParallelWriter AsParallelWriter()
        {
            Initialize();
            return _pendingCommands.AsParallelWriter();
        }

        /// <summary>
        /// Returns a producer writer for Burst/job authored structural commands.
        /// The caller must capture this on the main thread while scheduling work.
        /// </summary>
        /// <param name="writer">Queue writer safe for concurrent job producers.</param>
        /// <returns>True when the queue is ready.</returns>
        public static bool TryGetParallelWriter(out NativeQueue<EntityCommand>.ParallelWriter writer)
        {
            Initialize();
            if (!_pendingCommands.IsCreated)
            {
                writer = default;
                return false;
            }

            writer = _pendingCommands.AsParallelWriter();
            return true;
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

            _targetsByToken.Clear();
            _tokensByInstanceId.Clear();
            _freeTokens.Clear();
            _nextToken = 1;
        }

        public static bool DrainMainThread()
        {
            if (!_pendingCommands.IsCreated)
                return true;

            int remainingBudget = MaxMainThreadCommandsPerDrain;
            while (remainingBudget > 0 && _pendingCommands.TryDequeue(out EntityCommand command))
            {
                ExecuteCommand(in command);
                remainingBudget--;
            }

            return _pendingCommands.IsEmpty();
        }

        public static void Shutdown()
        {
            if (_pendingCommands.IsCreated)
            {
                _pendingCommands.Dispose();
                _pendingCommands = default;
            }

            _targetsByToken.Clear();
            _tokensByInstanceId.Clear();
            _freeTokens.Clear();
            _nextToken = 1;
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
            }

            if (!TryResolveTarget(command.TargetToken, out GameObject instance))
                return;

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
                    if (command.IntValue <= 0 || !instance.TryGetComponent(out StorageCrate crate))
                        break;

                    crate.CommitReservation(command.IntValue);
                    break;
                }
            }
        }

        private static bool RequiresGameObjectTarget(EntityCommandType commandType)
        {
            switch (commandType)
            {
                case EntityCommandType.OpenPDATab:
                case EntityCommandType.ClosePDA:
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
