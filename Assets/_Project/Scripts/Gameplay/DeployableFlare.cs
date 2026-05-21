using Hecton8.Equipment.Auxiliary;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public enum FlareState
    {
        Inactive,
        Burning,
        Fading,
        Extinguished
    }

    [DisallowMultipleComponent]
    public sealed class DeployableFlare : MonoBehaviour
    {
        [Header("Router Payload")]
        [SerializeField, Min(0.01f)] private float fuelDuration = 60f;

        public FlareState State => ResolveState();
        public float RemainingFuel => ResolveRemainingFuel();
        public bool IsBurning => ResolveState() == FlareState.Burning;

        private void OnDisable()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelFlare(transform.position);
        }

        public void Deploy()
        {
            if (ResolveState() == FlareState.Burning)
                return;

            float lifetime = math.max(0.01f, fuelDuration);
            AuxiliaryEquipmentRouterRuntime.TryDeployFlare(transform.position, lifetime);
        }

        public void ForceExtinguish()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelFlare(transform.position);
        }

        public void ResetFlare()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelFlare(transform.position);
        }

        private FlareState ResolveState()
        {
            if (AuxiliaryEquipmentRouterRuntime.TryReadNearestRemainingLifetime(
                AuxiliaryEquipmentConstants.FlarePrefabHash,
                transform.position,
                2f,
                out _))
            {
                return FlareState.Burning;
            }

            return FlareState.Extinguished;
        }

        private float ResolveRemainingFuel()
        {
            return AuxiliaryEquipmentRouterRuntime.TryReadNearestRemainingLifetime(
                AuxiliaryEquipmentConstants.FlarePrefabHash,
                transform.position,
                2f,
                out float remaining) ? remaining : 0f;
        }
    }
}
