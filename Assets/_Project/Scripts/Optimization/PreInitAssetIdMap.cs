using Hecton8.Core;
using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.Optimization
{
    internal static class PreInitAssetIdMapLayout
    {
        public const int AssetGuidIdRecordStrideBytes = 16;
    }

    [StructLayout(LayoutKind.Explicit, Size = PreInitAssetIdMapLayout.AssetGuidIdRecordStrideBytes)]
    internal readonly struct AssetGuidIdRecord
    {
        [FieldOffset(0)]
        public readonly uint GuidHash;
        [FieldOffset(4)]
        public readonly uint AssetId;
        [FieldOffset(8)]
        private readonly ulong _pad0;

        public AssetGuidIdRecord(uint guidHash, uint assetId)
        {
            GuidHash = guidHash;
            AssetId = assetId;
            _pad0 = 0UL;
        }
    }

    internal static class PreInitAssetIdMap
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        private static NativeArray<AssetGuidIdRecord> _guidRecords;
        private static bool _initialized;
        private static bool _hasRecords;

        [System.ThreadStatic] private static uint _lastResolvedGuidHash;
        [System.ThreadStatic] private static uint _lastResolvedAssetId;
        [System.ThreadStatic] private static uint _lastMissingGuidHash;

        internal static int GeneratedRecordCount => GeneratedAssetGuidIdTable.RecordCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Shutdown();
        }

        internal static void Initialize()
        {
            if (_initialized)
                return;

            ClearThreadLocalResolveCache();
            int recordCount = GeneratedAssetGuidIdTable.RecordCount;
            if (recordCount == 0)
            {
                _hasRecords = false;
                _initialized = true;
                return;
            }

            try
            {
                _guidRecords = new NativeArray<AssetGuidIdRecord>(
                    recordCount,
                    Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory); // COLD ALLOC: NativeArray<AssetGuidIdRecord>[generated asset count] - sorted GUID hash to uint asset ids - owner: PreInitAssetIdMap
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(
                    _guidRecords,
                    nameof(PreInitAssetIdMap),
                    nameof(_guidRecords),
                    NativeAllocationLifetime.Session);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("Native memory sentinel registration failed for pre-init asset id records.");

                GeneratedAssetGuidIdTable.CopyTo(_guidRecords);
            }
            catch
            {
                Shutdown();
                throw;
            }

            _hasRecords = true;
            _initialized = true;
        }

        internal static void Shutdown()
        {
            if (_guidRecords.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_guidRecords);
                _guidRecords.Dispose();
                _guidRecords = default;
            }

            _initialized = false;
            _hasRecords = false;
            ClearThreadLocalResolveCache();
        }

        internal static bool TryResolve(System.ReadOnlySpan<char> guid, out uint assetId)
        {
            assetId = 0u;
            if (!_initialized)
                Initialize();

            if (!_hasRecords)
                return false;

            uint hash = ComputeGuidHash(guid);
            if (hash == 0u)
                return false;

            if (hash == _lastResolvedGuidHash && _lastResolvedAssetId != 0u)
            {
                assetId = _lastResolvedAssetId;
                return true;
            }

            if (hash == _lastMissingGuidHash)
                return false;

            if (_guidRecords.IsCreated && TryResolveSorted(hash, out assetId))
            {
                _lastResolvedGuidHash = hash;
                _lastResolvedAssetId = assetId;
                _lastMissingGuidHash = 0u;
                return true;
            }

            _lastMissingGuidHash = hash;
            return false;
        }

        private static void ClearThreadLocalResolveCache()
        {
            _lastResolvedGuidHash = 0u;
            _lastResolvedAssetId = 0u;
            _lastMissingGuidHash = 0u;
        }

        internal static uint MixAssetVariant(uint assetId, byte biomeId, byte lodLevel)
        {
            unchecked
            {
                uint hash = FnvOffset;
                hash ^= assetId;
                hash *= FnvPrime;
                hash ^= biomeId;
                hash *= FnvPrime;
                hash ^= lodLevel;
                hash *= FnvPrime;
                return hash != 0u ? hash : 1u;
            }
        }

        internal static uint ComputeGuidHash(System.ReadOnlySpan<char> guid)
        {
            if (guid.Length == 0)
                return 0u;

            unchecked
            {
                uint hash = FnvOffset;
                for (int i = 0; i < guid.Length; i++)
                {
                    char value = guid[i];
                    if (value == '-')
                        continue;

                    if ((uint)(value - 'A') <= 5u)
                        value = (char)(value + 32);

                    hash ^= value;
                    hash *= FnvPrime;
                }

                return hash != 0u ? hash : 1u;
            }
        }

        private static bool TryResolveSorted(uint guidHash, out uint assetId)
        {
            int low = 0;
            int high = _guidRecords.Length - 1;

            while (low <= high)
            {
                int index = low + ((high - low) >> 1);
                AssetGuidIdRecord record = _guidRecords[index];

                if (record.GuidHash == guidHash)
                {
                    assetId = record.AssetId;
                    return assetId != 0u;
                }

                if (record.GuidHash < guidHash)
                    low = index + 1;
                else
                    high = index - 1;
            }

            assetId = 0u;
            return false;
        }
    }
}
