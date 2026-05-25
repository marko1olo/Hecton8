namespace Hecton8.Core
{
    using System.Threading;
    using Hecton.Localization;
    using Hecton8.Core.Contracts.Signals;
    using Hecton8.Items;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Owner-neutral first-party route for item lifecycle traffic.
    /// Managed mod events stay outside this hot typed lane.
    /// </summary>
    public static class ItemLifecycleSignalRoute
    {
        private static readonly uint DataHydrocarbonResinHash = unchecked((uint)LocHash.Compute("Data_HydrocarbonResin"));
        private static readonly uint HydrocarbonResinHash = unchecked((uint)LocHash.Compute("HydrocarbonResin"));
        private static readonly uint CompFiberMeshHash = unchecked((uint)LocHash.Compute("Comp_FiberMesh"));
        private static readonly uint FiberMeshHash = unchecked((uint)LocHash.Compute("FiberMesh"));
        private static readonly uint CompSealantPackHash = unchecked((uint)LocHash.Compute("Comp_SealantPack"));
        private static readonly uint SealantPackHash = unchecked((uint)LocHash.Compute("SealantPack"));

        private static int s_sequence;

        [System.Obsolete("Use TryPublishCollected(...) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool PublishCollected(ItemData item, int quantity, ulong interactorEntityId, Vector3 runtimePosition, bool hasRuntimePosition)
        {
            return TryPublishCollected(item, quantity, interactorEntityId, runtimePosition, hasRuntimePosition);
        }

        public static bool TryPublishCollected(ItemData item, int quantity, ulong interactorEntityId, Vector3 runtimePosition, bool hasRuntimePosition)
        {
            return TryPublish(item, ItemLifecycleSignal.ActionCollected, quantity, 0, interactorEntityId, runtimePosition, hasRuntimePosition);
        }

        [System.Obsolete("Use TryPublishRecycled(ItemData,int,int) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool PublishRecycled(ItemData item, int quantity, int yieldUnitCount)
        {
            return TryPublishRecycled(item, quantity, yieldUnitCount);
        }

        public static bool TryPublishRecycled(ItemData item, int quantity, int yieldUnitCount)
        {
            return TryPublish(item, ItemLifecycleSignal.ActionRecycled, quantity, yieldUnitCount, 0ul, Vector3.zero, false);
        }

        [System.Obsolete("Use TryPublishDiscarded(...) so overflow/drop semantics stay visible at the producer.", true)]
        public static bool PublishDiscarded(ItemData item, int quantity, ulong interactorEntityId, Vector3 runtimePosition, bool hasRuntimePosition)
        {
            return TryPublishDiscarded(item, quantity, interactorEntityId, runtimePosition, hasRuntimePosition);
        }

        public static bool TryPublishDiscarded(ItemData item, int quantity, ulong interactorEntityId, Vector3 runtimePosition, bool hasRuntimePosition)
        {
            return TryPublish(item, ItemLifecycleSignal.ActionDiscarded, quantity, 0, interactorEntityId, runtimePosition, hasRuntimePosition);
        }

        private static bool TryPublish(
            ItemData item,
            byte action,
            int quantity,
            int yieldUnitCount,
            ulong interactorEntityId,
            Vector3 runtimePosition,
            bool hasRuntimePosition)
        {
            if (item == null || quantity <= 0)
                return false;

            uint itemHash = unchecked((uint)item.PersistentHashId);
            float unitWeightKg = math.max(0f, item.weight);
            byte category = ClampByte((int)item.category);
            byte resourceFamily = ClampByte((int)item.resourceFamily);
            byte flags = BuildFlags(item, hasRuntimePosition, itemHash);

            ItemLifecycleSignal signal = new ItemLifecycleSignal
            {
                RuntimePosition = hasRuntimePosition ? new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z) : float3.zero,
                UnitWeightKg = unitWeightKg,
                ItemHash = itemHash,
                InteractorHash = FoldEntityId(interactorEntityId),
                Quantity = quantity,
                YieldUnitCount = math.max(0, yieldUnitCount),
                Frame = unchecked((uint)Time.frameCount),
                Sequence = unchecked((uint)Interlocked.Increment(ref s_sequence)),
                PollutionMilli = ResolveDiscardPollutionMilli(category, unitWeightKg),
                Action = action,
                Category = category,
                ResourceFamily = resourceFamily,
                Flags = flags
            };

            return SignalBus<ItemLifecycleSignal>.TryPush(in signal);
        }

        private static byte BuildFlags(ItemData item, bool hasRuntimePosition, uint itemHash)
        {
            byte flags = 0;
            if (hasRuntimePosition)
                flags |= ItemLifecycleSignal.FlagHasRuntimePosition;
            if (item.isRawResource)
                flags |= ItemLifecycleSignal.FlagRawResource;
            if (item.category == ItemCategory.Material)
                flags |= ItemLifecycleSignal.FlagMaterialCategory;
            if (IsPlasticLike(item, itemHash))
                flags |= ItemLifecycleSignal.FlagPlasticLike;
            return flags;
        }

        private static bool IsPlasticLike(ItemData item, uint itemHash)
        {
            if (item == null)
                return false;

            if (item.resourceFamily == ResourceFamily.Chemical)
                return true;

            return itemHash == DataHydrocarbonResinHash ||
                   itemHash == HydrocarbonResinHash ||
                   itemHash == CompFiberMeshHash ||
                   itemHash == FiberMeshHash ||
                   itemHash == CompSealantPackHash ||
                   itemHash == SealantPackHash;
        }

        private static uint ResolveDiscardPollutionMilli(byte category, float unitWeightKg)
        {
            float categoryWeight = ResolveDiscardCategoryWeight(category);
            float pollution = 1f + math.clamp(unitWeightKg * 0.35f, 0f, 1.5f) + (categoryWeight - 1f);
            return (uint)math.max(0, (int)math.round(pollution * 1000f));
        }

        private static float ResolveDiscardCategoryWeight(byte category)
        {
            switch ((ItemCategory)category)
            {
                case ItemCategory.Material:
                    return 0.8f;
                case ItemCategory.Component:
                    return 1.1f;
                case ItemCategory.Tool:
                case ItemCategory.Equipment:
                    return 1.4f;
                default:
                    return 1f;
            }
        }

        private static uint FoldEntityId(ulong entityId)
        {
            return unchecked((uint)entityId ^ (uint)(entityId >> 32));
        }

        private static byte ClampByte(int value)
        {
            if (value <= 0)
                return 0;
            return value > byte.MaxValue ? byte.MaxValue : (byte)value;
        }
    }
}
