using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class BulkheadContainmentIntentBus
    {
        public const int IntentCapacity = 256;
        public const ulong IntentMutationGuardMask = 1UL << 62;

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

            if (!TryEnsureBound(vault) ||
                !TryAcquireIntentWriteViews(vault, out NativeArray<BulkheadContainmentIntentDTO> intents, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows))
            {
                if (!TryBindDataVault(vault) ||
                    !TryAcquireIntentWriteViews(vault, out intents, out controlRows))
                {
                    return false;
                }
            }

            try
            {
                BulkheadContainmentIntentControlDTO control = controlRows[0];
                uint capacity = (uint)math.min(IntentCapacity, intents.Length);
                if (capacity == 0u)
                    return false;

                uint write = control.WriteCursor;
                uint read = control.ReadCursor;
                uint pending = write - read;
                if (pending >= capacity)
                {
                    read = write - capacity + 1u;
                    control.ReadCursor = read;
                    control.Dropped = unchecked(control.Dropped + 1u);
                    control.Flags |= BulkheadContainmentIntentFlags.OverflowCompensated;
                }

                uint flags = BulkheadContainmentIntentFlags.Valid;
                flags |= locked ? BulkheadContainmentIntentFlags.Locked : BulkheadContainmentIntentFlags.None;

                intents[(int)(write % capacity)] = new BulkheadContainmentIntentDTO
                {
                    CenterAup = centerAup,
                    Normal = normal,
                    WidthMeters = widthMeters,
                    HeightMeters = heightMeters,
                    ParentIntegrity01 = parentIntegrity01,
                    EdgeHashID = edgeHash,
                    SiblingNodeHash = siblingNodeHash,
                    Flags = flags,
                    Frame = frame
                };
                control.WriteCursor = unchecked(write + 1u);
                control.Capacity = capacity;
                control.LastEdgeHashID = edgeHash;
                controlRows[0] = control;
                return true;
            }
            finally
            {
                ReleaseIntentWriteViews(vault);
            }
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

        private static bool TryAcquireIntentWriteViews(
            IDataVault vault,
            out NativeArray<BulkheadContainmentIntentDTO> intents,
            out NativeArray<BulkheadContainmentIntentControlDTO> controlRows)
        {
            intents = default;
            controlRows = default;
            if (vault == null ||
                !HasBoundHandles() ||
                !vault.TryAcquireMutationGuard(IntentMutationGuardMask))
            {
                return false;
            }

            try
            {
                vault.TryResolveHandle(in s_intentsHandle, out intents);
                vault.TryResolveHandle(in s_controlHandle, out controlRows);
                if (intents.IsCreated &&
                    controlRows.IsCreated &&
                    intents.Length > 0 &&
                    controlRows.Length > 0)
                {
                    return true;
                }

                return false;
            }
            finally
            {
                if (!intents.IsCreated || !controlRows.IsCreated || intents.Length == 0 || controlRows.Length == 0)
                {
                    vault.ReleaseMutationGuard(IntentMutationGuardMask);
                }
            }
        }

        private static void ReleaseIntentWriteViews(IDataVault vault)
        {
            if (vault == null || !HasBoundHandles())
                return;

            vault.ReleaseMutationGuard(IntentMutationGuardMask);
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
