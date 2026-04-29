using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Central owner for reusable MaterialPropertyBlock instances on approved legacy draw paths.
    /// </summary>
    /// <remarks>
    /// Use only for procedural <see cref="Graphics.DrawMesh"/> style passes, particles, and UI.
    /// Do not use on standard MeshRenderer or SkinnedMeshRenderer geometry because that breaks SRP Batcher residency.
    /// </remarks>
    public static class MaterialPropertyBlockRegistry
    {
        // COLD ALLOC: Dictionary<ulong, MaterialPropertyBlock>[16] - reusable legacy draw-property blocks keyed by owner entity id - owner: MaterialPropertyBlockRegistry
        private static readonly Dictionary<ulong, MaterialPropertyBlock> s_LegacyBlocks = new Dictionary<ulong, MaterialPropertyBlock>(16);

        /// <summary>
        /// Returns the reusable MaterialPropertyBlock assigned to the given owner entity.
        /// </summary>
        /// <param name="ownerEntityId">Stable entity identifier for the procedural draw owner.</param>
        /// <returns>Reusable property block for the owner entity.</returns>
        public static MaterialPropertyBlock GetLegacyBlock(ulong ownerEntityId)
        {
            if (s_LegacyBlocks.TryGetValue(ownerEntityId, out MaterialPropertyBlock block))
                return block;

            block = new MaterialPropertyBlock(); // COLD ALLOC: MaterialPropertyBlock[1] - reusable procedural draw-property block for one owner - owner: MaterialPropertyBlockRegistry
            s_LegacyBlocks.Add(ownerEntityId, block);
            return block;
        }

        /// <summary>
        /// Returns the reusable MaterialPropertyBlock assigned to the given owner.
        /// </summary>
        /// <param name="owner">Owner requesting a legacy draw property block.</param>
        /// <returns>Reusable property block for the owner, or null when no owner was supplied.</returns>
        public static MaterialPropertyBlock GetLegacyBlock(Object owner)
        {
            if (owner == null)
                return null;

            return GetLegacyBlock(EntityId.ToULong(owner.GetEntityId()));
        }

        /// <summary>
        /// Legacy compatibility alias for callers that already use the previous registry method name.
        /// </summary>
        /// <param name="owner">Owner requesting a legacy draw property block.</param>
        /// <returns>Reusable property block for the owner, or null when no owner was supplied.</returns>
        public static MaterialPropertyBlock GetOrCreateLegacyBlock(Object owner)
        {
            return GetLegacyBlock(owner);
        }

        /// <summary>
        /// Attempts to resolve an existing cached block without creating a new one.
        /// </summary>
        /// <param name="ownerEntityId">Stable entity identifier for the procedural draw owner.</param>
        /// <param name="block">Resolved block when present.</param>
        /// <returns>True when a block already exists for the owner entity.</returns>
        public static bool TryGetLegacyBlock(ulong ownerEntityId, out MaterialPropertyBlock block)
        {
            return s_LegacyBlocks.TryGetValue(ownerEntityId, out block);
        }

        /// <summary>
        /// Releases the cached MaterialPropertyBlock owned by the supplied object.
        /// </summary>
        /// <param name="owner">Owner whose cached property block should be released.</param>
        public static void ReleaseLegacyBlock(ulong ownerEntityId)
        {
            s_LegacyBlocks.Remove(ownerEntityId);
        }

        /// <summary>
        /// Releases the cached MaterialPropertyBlock owned by the supplied object.
        /// </summary>
        /// <param name="owner">Owner whose cached property block should be released.</param>
        public static void ReleaseLegacyBlock(Object owner)
        {
            if (owner == null)
                return;

            ReleaseLegacyBlock(EntityId.ToULong(owner.GetEntityId()));
        }
    }
}
