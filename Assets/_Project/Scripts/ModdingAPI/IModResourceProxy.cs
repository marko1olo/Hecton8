using System;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Sandboxed resource resolver exposed to mods.
    /// Returns hash identifiers only; engine owners resolve actual Unity assets.
    /// </summary>
    public interface IModResourceProxy
    {
        /// <summary>
        /// Resolves a prefab asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolvePrefab(string assetName, out uint hashId);

        /// <summary>
        /// Resolves an audio clip asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolveAudioClip(string assetName, out uint hashId);

        /// <summary>
        /// Resolves a texture asset name to an engine-owned resource hash.
        /// </summary>
        /// <param name="assetName">Mod-local asset name.</param>
        /// <param name="hashId">Resolved resource hash.</param>
        /// <returns>True when the resource hash was registered.</returns>
        bool TryResolveTexture(string assetName, out uint hashId);
    }

    internal enum ModResourceKind : byte
    {
        Prefab = 1,
        AudioClip = 2,
        Texture = 3
    }

    internal sealed class ModResourceProxy : IModResourceProxy
    {
        internal static readonly ModResourceProxy Instance = new ModResourceProxy();

        private ModResourceProxy()
        {
        }

        private static void ThrowIfNoActiveMod()
        {
            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Resource proxy calls must originate from an active mod execution scope.");
        }

        public bool TryResolvePrefab(string assetName, out uint hashId)
        {
            ThrowIfNoActiveMod();
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
            {
                hashId = 0u;
                return false;
            }

            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.Prefab, out hashId);
        }

        public bool TryResolveAudioClip(string assetName, out uint hashId)
        {
            ThrowIfNoActiveMod();
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
            {
                hashId = 0u;
                return false;
            }

            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.AudioClip, out hashId);
        }

        public bool TryResolveTexture(string assetName, out uint hashId)
        {
            ThrowIfNoActiveMod();
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
            {
                hashId = 0u;
                return false;
            }

            return ModResourceRegistry.TryRegister(ModExecutionScope.CurrentModId, assetName, ModResourceKind.Texture, out hashId);
        }
    }

    internal static class ModResourceRegistry
    {
        private const int ResourceCapacity = 256;
        private const uint ResourceRegistrationOverflowWarningHash = 0x4D525246u; // MRRF
        private const uint ResourceRegistrationOverflowContextHash = 0x4D525251u; // MRRQ

        private struct ResourceRecord
        {
            public string ModId;
            public string AssetName;
            public ModResourceKind Kind;
        }

        // COLD ALLOC: ResourceRecord[256] - managed sidecar for engine-only resource resolution - owner: ModResourceRegistry
        private static readonly ResourceRecord[] _records = new ResourceRecord[ResourceCapacity];
        private static NativeHashMap<uint, int> _resourceIndexByHash;
        private static int _resourceIndexByHashSentinelId;
        private static int _recordCount;
        private static int _droppedResourceRegistrationCount;
        private static int _lastResourceRegistrationOverflowTelemetryFrame = -1;

        internal static int DroppedResourceRegistrationCount => _droppedResourceRegistrationCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Initialize()
        {
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return;

            if (!_resourceIndexByHash.IsCreated)
            {
                _resourceIndexByHash = new NativeHashMap<uint, int>(ResourceCapacity, Allocator.Persistent); // COLD ALLOC: NativeHashMap<uint,int>[256] - O(1) resource hash to sidecar index - owner: ModResourceRegistry
                try
                {
                    _resourceIndexByHashSentinelId = NativeMemorySentinel.RegisterNativeHashMapInstance(
                        _resourceIndexByHash,
                        nameof(ModResourceRegistry),
                        nameof(_resourceIndexByHash),
                        NativeAllocationLifetime.Session);
                    if (_resourceIndexByHashSentinelId <= 0)
                        throw new System.InvalidOperationException("NativeMemorySentinel rejected ModResourceRegistry hash map registration.");
                }
                catch (System.Exception exception)
                {
                    try
                    {
                        DisposeResourceIndexByHash();
                    }
                    catch (System.Exception cleanupException)
                    {
                        throw new System.AggregateException(
                            "ModResourceRegistry native hash map initialization failed and cleanup also failed.",
                            exception,
                            cleanupException);
                    }

                    throw;
                }
            }
        }

        internal static void Shutdown()
        {
            DisposeResourceIndexByHash();

            for (int i = 0; i < _recordCount; i++)
                _records[i] = default;

            _recordCount = 0;
            _droppedResourceRegistrationCount = 0;
            _lastResourceRegistrationOverflowTelemetryFrame = -1;
        }

        private static void DisposeResourceIndexByHash()
        {
            Exception firstException = null;

            if (_resourceIndexByHashSentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(_resourceIndexByHashSentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    _resourceIndexByHashSentinelId = 0;
                }
            }

            if (_resourceIndexByHash.IsCreated)
            {
                try
                {
                    _resourceIndexByHash.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    _resourceIndexByHash = default;
                }
            }
            else
            {
                _resourceIndexByHash = default;
            }

            if (firstException != null)
                throw firstException;
        }


        internal static bool TryRegister(
            string modId,
            string assetName,
            ModResourceKind kind,
            out uint hashId)
        {
            hashId = 0u;

            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return false;

            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException("Resource proxy calls must originate from an active mod execution scope.");

            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName) || kind == 0)
                return false;

            if (!string.Equals(modId, ModExecutionScope.CurrentModId, System.StringComparison.Ordinal))
                throw new IllegalContractException("Resource registration owner must match the active mod execution scope.");

            Initialize();
            hashId = ComputeResourceHash(modId, assetName, kind);
            if (hashId == 0u)
                return false;

            if (_resourceIndexByHash.ContainsKey(hashId))
                return true;

            if (_recordCount >= ResourceCapacity)
            {
                ReportResourceRegistrationOverflow(kind);
                return false;
            }

            _records[_recordCount] = new ResourceRecord
            {
                ModId = modId,
                AssetName = assetName,
                Kind = kind
            };
            _resourceIndexByHash.Add(hashId, _recordCount);
            _recordCount++;
            return true;
        }

        internal static void UnregisterModResources(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId) || _recordCount <= 0)
                return;

            for (int i = _recordCount - 1; i >= 0; i--)
            {
                if (!string.Equals(_records[i].ModId, modId, StringComparison.Ordinal))
                    continue;

                RemoveRecordAt(i);
            }
        }

        internal static bool TryResolvePrefab(uint hashId, out GameObject prefab)
        {
            prefab = null;
            if (!TryResolve(hashId, ModResourceKind.Prefab, out ResourceRecord record))
                return false;

            prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);
            return prefab != null;
        }

        internal static bool TryResolveAudioClip(uint hashId, out AudioClip clip)
        {
            clip = null;
            if (!TryResolve(hashId, ModResourceKind.AudioClip, out ResourceRecord record))
                return false;

            clip = ModAssetManager.LoadAudioClip(record.ModId, record.AssetName);
            return clip != null;
        }

        internal static bool TryResolveTexture(uint hashId, out Texture2D texture)
        {
            texture = null;
            if (!TryResolve(hashId, ModResourceKind.Texture, out ResourceRecord record))
                return false;

            texture = ModAssetManager.LoadTexture(record.ModId, record.AssetName);
            return texture != null;
        }

        private static bool TryResolve(uint hashId, ModResourceKind expectedKind, out ResourceRecord record)
        {
            record = default;
            if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                return false;

            if (hashId == 0u || !_resourceIndexByHash.IsCreated)
                return false;

            if (!_resourceIndexByHash.TryGetValue(hashId, out int index) ||
                (uint)index >= (uint)_recordCount)
            {
                return false;
            }

            record = _records[index];
            return record.Kind == expectedKind;
        }

        private static void RemoveRecordAt(int index)
        {
            if ((uint)index >= (uint)_recordCount)
                return;

            ResourceRecord removed = _records[index];
            RemoveResourceIndex(removed);

            int lastIndex = _recordCount - 1;
            if (index != lastIndex)
            {
                ResourceRecord moved = _records[lastIndex];
                _records[index] = moved;
                RemoveResourceIndex(moved);
                AddResourceIndex(moved, index);
            }

            _records[lastIndex] = default;
            _recordCount--;
        }

        private static void RemoveResourceIndex(in ResourceRecord record)
        {
            if (!_resourceIndexByHash.IsCreated ||
                string.IsNullOrWhiteSpace(record.ModId) ||
                string.IsNullOrWhiteSpace(record.AssetName) ||
                record.Kind == 0)
            {
                return;
            }

            uint hash = ComputeResourceHash(record.ModId, record.AssetName, record.Kind);
            if (hash != 0u)
                _resourceIndexByHash.Remove(hash);
        }

        private static void AddResourceIndex(in ResourceRecord record, int index)
        {
            if (!_resourceIndexByHash.IsCreated ||
                string.IsNullOrWhiteSpace(record.ModId) ||
                string.IsNullOrWhiteSpace(record.AssetName) ||
                record.Kind == 0)
            {
                return;
            }

            uint hash = ComputeResourceHash(record.ModId, record.AssetName, record.Kind);
            if (hash == 0u)
                return;

            _resourceIndexByHash.Remove(hash);
            _resourceIndexByHash.Add(hash, index);
        }

        private static uint ComputeResourceHash(string modId, string assetName, ModResourceKind kind)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = MixHash(hash, (uint)LocHash.Compute(modId));
                hash = MixHash(hash, (uint)LocHash.Compute(assetName));
                hash = MixHash(hash, (uint)kind);
                return hash == 0u ? 1u : hash;
            }
        }

        private static uint MixHash(uint hash, uint value)
        {
            hash ^= value;
            hash *= 16777619u;
            return hash;
        }

        private static void ReportResourceRegistrationOverflow(ModResourceKind kind)
        {
            _droppedResourceRegistrationCount++;
            int frame = ResolveCurrentFrameIndexSafe();
            if (_lastResourceRegistrationOverflowTelemetryFrame == frame)
                return;

            _lastResourceRegistrationOverflowTelemetryFrame = frame;
            PublishPerformanceWarningBestEffort(
                ResourceRegistrationOverflowWarningHash,
                ResourceRegistrationOverflowContextHash ^ ((uint)kind << 24),
                _droppedResourceRegistrationCount);
        }

        private static int ResolveCurrentFrameIndexSafe()
        {
            try
            {
                return SystemDispatcher.CurrentFrameIndex;
            }
            catch
            {
                return -1;
            }
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                H8Debug.LogWarning("[ModResourceRegistry] telemetry failed: " + exception.Message);
#endif
            }
        }
    }
}
