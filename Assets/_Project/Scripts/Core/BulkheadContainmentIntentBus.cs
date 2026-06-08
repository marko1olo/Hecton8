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

        private struct IntentWriteViews
        {
            public NativeArray<BulkheadContainmentIntentDTO> Intents;
            public NativeArray<BulkheadContainmentIntentControlDTO> ControlRows;
            public uint Capacity;
        }

        public static void BindDataVault(IDataVault vault)
        {
            TryBindDataVault(vault);
        }

        public static void UnbindDataVault(IDataVault vault)
        {
            if (vault == null || ReferenceEquals(s_cachedVault, vault))
                ClearDataVaultBinding();
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

            if (!TryAcquireIntentWriteViews(vault, out IntentWriteViews views) &&
                (!TryBindDataVault(vault) || !TryAcquireIntentWriteViews(vault, out views)))
            {
                return false;
            }

            try
            {
                if (!TryReadIntentControlGuarded(views, out BulkheadContainmentIntentControlDTO control, out uint write))
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
                return TryWriteIntentGuarded(views, write, in intent) &&
                       TryPublishIntentControlGuarded(views, in control);
            }
            finally
            {
                ReleaseIntentWriteViews(vault);
            }
        }

        private static bool TryAcquireIntentWriteViews(IDataVault vault, out IntentWriteViews views)
        {
            views = default;
            if (!TryEnsureBound(vault) ||
                !vault.TryAcquireMutationGuard(IntentMutationGuardMask))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (!HasBoundHandles() ||
                    !vault.TryResolveHandle(in s_intentsHandle, out NativeArray<BulkheadContainmentIntentDTO> intents) ||
                    !vault.TryResolveHandle(in s_controlHandle, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows) ||
                    !intents.IsCreated ||
                    !controlRows.IsCreated ||
                    intents.Length == 0 ||
                    controlRows.Length == 0)
                {
                    return false;
                }

                uint capacity = (uint)math.min(IntentCapacity, intents.Length);
                if (capacity == 0u)
                    return false;

                views.Intents = intents;
                views.ControlRows = controlRows;
                views.Capacity = capacity;
                releaseOnFailure = false;
                return true;
            }
            finally
            {
                if (releaseOnFailure)
                    ReleaseIntentWriteViews(vault);
            }
        }

        private static void ReleaseIntentWriteViews(IDataVault vault)
        {
            vault?.ReleaseMutationGuard(IntentMutationGuardMask);
        }

        private static bool TryBindDataVault(IDataVault vault)
        {
            ClearDataVaultBinding();

            if (vault == null)
                return false;

            s_cachedVault = vault;

            bool bound =
                vault.TryGetGenerationHandle<BulkheadContainmentIntentDTO>(
                    BufferID.Shinobu220BulkheadIntentRing,
                    out s_intentsHandle) &&
                vault.TryGetGenerationHandle<BulkheadContainmentIntentControlDTO>(
                    BufferID.Shinobu220BulkheadIntentControl,
                    out s_controlHandle);

            if (bound)
                return true;

            ClearDataVaultBinding();
            return false;
        }

        private static void ClearDataVaultBinding()
        {
            s_cachedVault = null;
            s_intentsHandle = default;
            s_controlHandle = default;
        }

        private static bool TryEnsureBound(IDataVault vault)
        {
            return vault != null &&
                   (ReferenceEquals(vault, s_cachedVault) && HasBoundHandles() ||
                    TryBindDataVault(vault));
        }

        private static bool TryReadIntentControlGuarded(
            IntentWriteViews views,
            out BulkheadContainmentIntentControlDTO control,
            out uint write)
        {
            control = default;
            write = 0u;
            if (views.Capacity == 0u || !views.ControlRows.IsCreated || views.ControlRows.Length == 0)
                return false;

            control = views.ControlRows[0];
            write = control.WriteCursor;
            uint read = control.ReadCursor;
            uint pending = write - read;
            if (pending >= views.Capacity)
            {
                uint dropped = pending - views.Capacity + 1u;
                read = write - views.Capacity + 1u;
                control.ReadCursor = read;
                control.Dropped = unchecked(control.Dropped + dropped);
                control.Flags |= BulkheadContainmentIntentFlags.OverflowCompensated;
            }

            control.WriteCursor = unchecked(write + 1u);
            control.Capacity = views.Capacity;
            return true;
        }

        private static bool TryWriteIntentGuarded(
            IntentWriteViews views,
            uint write,
            in BulkheadContainmentIntentDTO intent)
        {
            if (views.Capacity == 0u || !views.Intents.IsCreated || views.Intents.Length == 0)
                return false;

            uint safeCapacity = (uint)math.min(views.Capacity, views.Intents.Length);
            if (safeCapacity == 0u)
                return false;

            views.Intents[(int)(write % safeCapacity)] = intent;
            return true;
        }

        private static bool TryPublishIntentControlGuarded(
            IntentWriteViews views,
            in BulkheadContainmentIntentControlDTO control)
        {
            if (!views.ControlRows.IsCreated || views.ControlRows.Length == 0)
                return false;

            views.ControlRows[0] = control;
            return true;
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
