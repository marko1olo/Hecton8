using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Physics
{
    internal sealed class CablePhysics132Service : ICablePhysics132Service
    {
        internal static readonly CablePhysics132Service Instance = new CablePhysics132Service();

        public int TickCount => global::System.Environment.TickCount;

        public bool ValidateLayout()
        {
            return CablePhysicsSolver132.ValidateLayout();
        }

        public bool TryHasMockBuffers(IDataVault vault)
        {
            return CablePhysicsSolver132.TryHasMockBuffers(vault);
        }

        public void EnsureMockBuffers(IDataVault vault, float globalQualityWeight, uint frameIndex)
        {
            CablePhysicsSolver132.EnsureMockBuffers(vault, globalQualityWeight, frameIndex);
        }

        public bool TryScheduleMockFromVault(
            IDataVault vault,
            uint frameIndex,
            float fixedDeltaTime,
            float3 gravity,
            float3 abyssalFlow,
            double3 cameraAup,
            float globalQualityWeight,
            float lastElapsedMicroseconds,
            JobHandle dependency,
            out JobHandle handle)
        {
            return CablePhysicsSolver132.TryScheduleMockFromVault(
                vault,
                frameIndex,
                fixedDeltaTime,
                gravity,
                abyssalFlow,
                cameraAup,
                globalQualityWeight,
                lastElapsedMicroseconds,
                dependency,
                out handle);
        }

        public void ReleaseMockScheduleBufferPins(IDataVault vault)
        {
            CablePhysicsSolver132.ReleaseMockScheduleBufferPins(vault);
        }

        public bool TryDumpLatestFault(IDataVault vault)
        {
            if (!CablePhysicsSolver132.TrySampleLatestTelemetry(vault, out TetherTelemetryEntry telemetry))
                return false;

            uint faultFlags = CableNodeFlags132.NonFiniteRecovered | CableNodeFlags132.ConstraintFault;
            return (telemetry.Flags & faultFlags) != 0u &&
                   CablePhysicsSolver132.TryDumpCableSurgeon(vault, telemetry.Flags);
        }
    }

    internal static class CablePhysics132RegistryBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterService()
        {
            GlobalRegistry.RegisterCablePhysics132Runtime(CablePhysics132Service.Instance);
        }
    }
}
