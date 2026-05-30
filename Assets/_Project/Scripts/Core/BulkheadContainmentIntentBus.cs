using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class BulkheadContainmentIntentBus
    {
        public const int IntentCapacity = 256;
        public const ulong IntentMutationGuardMask = 1UL << 62;
        private const SystemID OwnerSystemId = SystemID.Construction;

        private static IDataVault s_cachedVault;
        private static VaultGenerationHandle<BulkheadContainmentIntentDTO> s_intentsHandle;
        private static VaultGenerationHandle<BulkheadContainmentIntentControlDTO> s_controlHandle;

        public static void BindDataVault(IDataVault vault)
        {
            TryBindDataVault(vault);
        }

        public static bool TryWriteAirlockBulkheadIntent(
            uint edgeHash,
            bool locked,
            double3 centerAup,
            float3 normal,
            float widthMeters,
            float heightMeters,
            float parentIntegrity01,
            uint siblingNodeHash,
            uint frame)
        {
            return TryWriteAirlockBulkheadIntent(
                s_cachedVault,
                edgeHash,
                locked,
                centerAup,
                normal,
                widthMeters,
                heightMeters,
                parentIntegrity01,
                siblingNodeHash,
                frame);
        }

        public static bool TryWriteAirlockBulkheadIntent(
            IDataVault vault,
            uint edgeHash,
            bool locked,
            double3 centerAup,
            float3 normal,
            float widthMeters,
            float heightMeters,
            float parentIntegrity01,
            uint siblingNodeHash,
            uint frame)
        {
            bool finitePacket =
                math.isfinite(centerAup.x) &&
                math.isfinite(centerAup.y) &&
                math.isfinite(centerAup.z) &&
                math.isfinite(normal.x) &&
                math.isfinite(normal.y) &&
                math.isfinite(normal.z) &&
                math.isfinite(widthMeters) &&
                math.isfinite(heightMeters) &&
                math.isfinite(parentIntegrity01);

            if (vault == null ||
                edgeHash == 0u ||
                !finitePacket)
            {
                return false;
            }

            if (!TryAcquireBoundIntentGuard(vault) &&
                !TryAcquireReboundIntentGuard(vault))
            {
                return false;
            }

            try
            {
                if (!TryResolveIntentCapacityGuarded(vault, out uint capacity) ||
                    !TryReadIntentControlGuarded(vault, capacity, out BulkheadContainmentIntentControlDTO control, out uint write))
                {
                    return false;
                }

                uint flags = BulkheadContainmentIntentFlags.Valid;
                flags |= locked ? BulkheadContainmentIntentFlags.Locked : BulkheadContainmentIntentFlags.None;

                BulkheadContainmentIntentDTO intent = default;
                intent.CenterAup = centerAup;
                intent.Normal = normal;
                intent.WidthMeters = widthMeters;
                intent.HeightMeters = heightMeters;
                intent.ParentIntegrity01 = parentIntegrity01;
                intent.EdgeHashID = edgeHash;
                intent.SiblingNodeHash = siblingNodeHash;
                intent.Flags = flags;
                intent.Frame = frame;

                control.LastEdgeHashID = edgeHash;
                return TryWriteIntentGuarded(vault, write, capacity, in intent) &&
                       TryPublishIntentControlGuarded(vault, in control);
            }
            finally
            {
                vault.ReleaseMutationGuard(IntentMutationGuardMask);
            }
        }

        private static bool TryAcquireBoundIntentGuard(IDataVault vault)
        {
            return TryEnsureBound(vault) &&
                   vault.TryAcquireMutationGuard(IntentMutationGuardMask);
        }

        private static bool TryAcquireReboundIntentGuard(IDataVault vault)
        {
            return TryBindDataVault(vault) &&
                   vault.TryAcquireMutationGuard(IntentMutationGuardMask);
        }

        private static bool TryBindDataVault(IDataVault vault)
        {
            s_cachedVault = vault;
            s_intentsHandle = default;
            s_controlHandle = default;

            if (vault == null)
                return false;

            bool bound =
                vault.TryGetGenerationHandle<BulkheadContainmentIntentDTO>(
                    BufferID.Shinobu220BulkheadIntentRing,
                    out s_intentsHandle) &&
                vault.TryGetGenerationHandle<BulkheadContainmentIntentControlDTO>(
                    BufferID.Shinobu220BulkheadIntentControl,
                    out s_controlHandle);

            if (bound)
                return true;

            s_cachedVault = null;
            s_intentsHandle = default;
            s_controlHandle = default;
            return false;
        }

        private static bool TryEnsureBound(IDataVault vault)
        {
            return vault != null &&
                   (ReferenceEquals(vault, s_cachedVault) && HasBoundHandles() ||
                    TryBindDataVault(vault));
        }

        private static bool TryResolveIntentCapacityGuarded(IDataVault vault, out uint capacity)
        {
            capacity = 0u;
            if (vault == null ||
                !HasBoundHandles() ||
                !vault.TryReadOnlyHandle(in s_intentsHandle, out NativeArray<BulkheadContainmentIntentDTO>.ReadOnly intents))
            {
                return false;
            }

            capacity = intents.IsCreated
                ? (uint)math.min(IntentCapacity, intents.Length)
                : 0u;
            return capacity > 0u;
        }

        private static bool TryReadIntentControlGuarded(
            IDataVault vault,
            uint capacity,
            out BulkheadContainmentIntentControlDTO control,
            out uint write)
        {
            control = default;
            write = 0u;
            if (vault == null ||
                capacity == 0u ||
                !HasBoundHandles() ||
                !vault.TryReadOnlyHandle(in s_controlHandle, out NativeArray<BulkheadContainmentIntentControlDTO>.ReadOnly controlRows))
            {
                return false;
            }

            if (!controlRows.IsCreated || controlRows.Length == 0)
                return false;

            control = controlRows[0];
            write = control.WriteCursor;
            uint read = control.ReadCursor;
            uint pending = write - read;
            if (pending >= capacity)
            {
                read = write - capacity + 1u;
                control.ReadCursor = read;
                control.Dropped = unchecked(control.Dropped + 1u);
                control.Flags |= BulkheadContainmentIntentFlags.OverflowCompensated;
            }

            control.WriteCursor = unchecked(write + 1u);
            control.Capacity = capacity;
            return true;
        }

        private static bool TryWriteIntentGuarded(
            IDataVault vault,
            uint write,
            uint capacity,
            in BulkheadContainmentIntentDTO intent)
        {
            if (vault == null ||
                capacity == 0u ||
                !HasBoundHandles() ||
                !vault.TryAcquireWriteLock(in s_intentsHandle, OwnerSystemId, out NativeArray<BulkheadContainmentIntentDTO> intents))
            {
                return false;
            }

            try
            {
                if (!intents.IsCreated || intents.Length == 0)
                    return false;

                uint safeCapacity = (uint)math.min(capacity, intents.Length);
                if (safeCapacity == 0u)
                    return false;

                intents[(int)(write % safeCapacity)] = intent;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_intentsHandle, OwnerSystemId);
            }
        }

        private static bool TryPublishIntentControlGuarded(
            IDataVault vault,
            in BulkheadContainmentIntentControlDTO control)
        {
            if (vault == null ||
                !HasBoundHandles() ||
                !vault.TryAcquireWriteLock(in s_controlHandle, OwnerSystemId, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows))
            {
                return false;
            }

            try
            {
                if (!controlRows.IsCreated || controlRows.Length == 0)
                    return false;

                controlRows[0] = control;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in s_controlHandle, OwnerSystemId);
            }
        }

        private static bool HasBoundHandles()
        {
            return s_intentsHandle.BufferID != 0u &&
                   s_intentsHandle.Generation != 0u &&
                   s_controlHandle.BufferID != 0u &&
                   s_controlHandle.Generation != 0u;
        }
    }
}
