using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Core.Contracts
{
    public static class BulkheadContainmentIntentBus
    {
        public const int IntentCapacity = 256;

        private static IDataVault s_cachedVault;

        public static void BindDataVault(IDataVault vault)
        {
            s_cachedVault = vault;
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
            if (vault == null ||
                edgeHash == 0u ||
                !math.isfinite(centerAup.x) ||
                !math.isfinite(centerAup.y) ||
                !math.isfinite(centerAup.z))
            {
                return false;
            }

            if (!vault.TryGetGenerationHandle<BulkheadContainmentIntentDTO>(
                    BufferID.Shinobu220BulkheadIntentRing,
                    out VaultGenerationHandle<BulkheadContainmentIntentDTO> intentsHandle) ||
                !vault.TryGetGenerationHandle<BulkheadContainmentIntentControlDTO>(
                    BufferID.Shinobu220BulkheadIntentControl,
                    out VaultGenerationHandle<BulkheadContainmentIntentControlDTO> controlHandle) ||
                !vault.TryResolveHandle(in intentsHandle, out NativeArray<BulkheadContainmentIntentDTO> intents) ||
                !vault.TryResolveHandle(in controlHandle, out NativeArray<BulkheadContainmentIntentControlDTO> controlRows) ||
                !intents.IsCreated ||
                !controlRows.IsCreated ||
                intents.Length == 0 ||
                controlRows.Length == 0)
            {
                return false;
            }

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
            if (!math.isfinite(normal.x) ||
                !math.isfinite(normal.y) ||
                !math.isfinite(normal.z) ||
                !math.isfinite(widthMeters) ||
                !math.isfinite(heightMeters) ||
                !math.isfinite(parentIntegrity01))
            {
                flags |= BulkheadContainmentIntentFlags.NonFinite;
            }

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
    }
}
