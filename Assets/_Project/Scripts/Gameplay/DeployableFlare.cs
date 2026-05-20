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

        private FlareState _state = FlareState.Inactive;
        private float _fuelTimer;

        public FlareState State => _state;
        public float RemainingFuel => _fuelTimer;
        public bool IsBurning => _state == FlareState.Burning;

        public void Deploy()
        {
            if (_state == FlareState.Burning)
                return;

            _state = FlareState.Burning;
            _fuelTimer = math.max(0.01f, fuelDuration);
            AuxiliaryEquipmentRouterRuntime.TryDeployFlare(transform.position, _fuelTimer);
        }

        public void ForceExtinguish()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelFlare(transform.position);
            _state = FlareState.Extinguished;
            _fuelTimer = 0f;
        }

        public void ResetFlare()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelFlare(transform.position);
            _state = FlareState.Inactive;
            _fuelTimer = math.max(0.01f, fuelDuration);
        }
    }
}
