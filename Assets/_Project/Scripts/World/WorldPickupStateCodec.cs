using Hecton8.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.World
{
    /// <summary>
    /// Deterministic identity codec for authored world pickups.
    /// </summary>
    internal static class WorldPickupStateCodec
    {
        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;
        private const float ChunkSizeMeters = 64f;
        private const float PositionQuantizationScale = 100f;

        public static bool TryBuildIdentity(
            Transform targetTransform,
            Scene owningScene,
            ItemData itemData,
            string stableWorldStateId,
            Vector3 anchorPosition,
            out long persistenceKey,
            out long chunkKey)
        {
            persistenceKey = 0L;
            chunkKey = 0L;

            if (targetTransform == null || !owningScene.IsValid() || string.IsNullOrWhiteSpace(owningScene.path))
                return false;

            return TryBuildIdentity(
                owningScene.path,
                stableWorldStateId,
                itemData != null ? itemData.PersistentId : null,
                anchorPosition,
                out persistenceKey,
                out chunkKey);
        }

        public static bool TryBuildIdentity(
            Transform targetTransform,
            Scene owningScene,
            ItemData itemData,
            Vector3 anchorPosition,
            out long persistenceKey,
            out long chunkKey)
        {
            persistenceKey = 0L;
            chunkKey = 0L;
            // No new fallback primary key without stableWorldStateId; legacy migration uses TryBuildLegacyIdentity.
            return false;
        }

        internal static bool TryBuildIdentity(
            string scenePath,
            string stableWorldStateId,
            string itemPersistentId,
            Vector3 anchorPosition,
            out long persistenceKey,
            out long chunkKey)
        {
            persistenceKey = 0L;
            chunkKey = 0L;

            if (string.IsNullOrWhiteSpace(scenePath))
                return false;

            ulong sceneHash = HashString(FnvOffset, scenePath);
            string stableId = NormalizeStableWorldStateId(stableWorldStateId);
            if (string.IsNullOrWhiteSpace(stableId))
                return false;

            // Authored pickup slot identity deliberately ignores item content; validator owns item authoring checks.
            ulong keyHash = HashByte(sceneHash, (byte)'#');
            keyHash = HashDelimitedString(keyHash, stableId);

            ulong chunkHash = sceneHash;
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.x / ChunkSizeMeters));
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.y / ChunkSizeMeters));
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.z / ChunkSizeMeters));

            persistenceKey = EnsureNonZero(keyHash);
            chunkKey = EnsureNonZero(chunkHash);
            return true;
        }

        public static bool TryBuildLegacyIdentity(
            Transform targetTransform,
            Scene owningScene,
            ItemData itemData,
            Vector3 anchorPosition,
            out long persistenceKey,
            out long chunkKey)
        {
            persistenceKey = 0L;
            chunkKey = 0L;

            if (targetTransform == null ||
                itemData == null ||
                !owningScene.IsValid() ||
                string.IsNullOrWhiteSpace(owningScene.path) ||
                string.IsNullOrWhiteSpace(itemData.PersistentId))
            {
                return false;
            }

            ulong sceneHash = HashString(FnvOffset, owningScene.path);
            ulong keyHash = HashHierarchyPath(sceneHash, targetTransform);
            keyHash = HashString(keyHash, itemData.PersistentId);
            keyHash = HashInt(keyHash, QuantizePosition(anchorPosition.x));
            keyHash = HashInt(keyHash, QuantizePosition(anchorPosition.y));
            keyHash = HashInt(keyHash, QuantizePosition(anchorPosition.z));

            ulong chunkHash = sceneHash;
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.x / ChunkSizeMeters));
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.y / ChunkSizeMeters));
            chunkHash = HashInt(chunkHash, Mathf.FloorToInt(anchorPosition.z / ChunkSizeMeters));

            persistenceKey = EnsureNonZero(keyHash);
            chunkKey = EnsureNonZero(chunkHash);
            return true;
        }

        private static string NormalizeStableWorldStateId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int QuantizePosition(float value)
        {
            return Mathf.RoundToInt(value * PositionQuantizationScale);
        }

        private static ulong HashHierarchyPath(ulong hash, Transform targetTransform)
        {
            Transform parent = targetTransform.parent;
            if (parent != null)
                hash = HashHierarchyPath(hash, parent);

            hash = HashByte(hash, (byte)'/');
            return HashString(hash, targetTransform.name);
        }

        private static ulong HashString(ulong hash, string value)
        {
            if (string.IsNullOrEmpty(value))
                return HashByte(hash, 0);

            for (int i = 0; i < value.Length; i++)
            {
                hash = HashByte(hash, (byte)(value[i] & 0xFF));
                hash = HashByte(hash, (byte)(value[i] >> 8));
            }

            return hash;
        }

        private static ulong HashDelimitedString(ulong hash, string value)
        {
            hash = HashInt(hash, string.IsNullOrEmpty(value) ? 0 : value.Length);
            return HashString(hash, value);
        }

        private static ulong HashInt(ulong hash, int value)
        {
            uint unsignedValue = unchecked((uint)value);
            hash = HashByte(hash, (byte)unsignedValue);
            hash = HashByte(hash, (byte)(unsignedValue >> 8));
            hash = HashByte(hash, (byte)(unsignedValue >> 16));
            hash = HashByte(hash, (byte)(unsignedValue >> 24));
            return hash;
        }

        private static ulong HashByte(ulong hash, byte value)
        {
            return (hash ^ value) * FnvPrime;
        }

        private static long EnsureNonZero(ulong value)
        {
            ulong normalized = value == 0UL ? 1UL : value;
            long signed = unchecked((long)normalized);
            return signed == 0L ? 1L : signed;
        }
    }
}
