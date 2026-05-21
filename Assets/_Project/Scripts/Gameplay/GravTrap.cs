using Hecton8.Equipment.Auxiliary;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GravTrap : MonoBehaviour
    {
        [Header("Router Payload")]
        [SerializeField, Min(0.1f)] private float pullRadius = 8f;
        [SerializeField, Min(0.01f)] private float lifetimeSeconds = 12f;

        public bool IsActive => ReadActiveState();
        public float PullRadius => pullRadius;

        private void OnEnable()
        {
            Activate();
        }

        private void OnDisable()
        {
            Deactivate();
        }

        public void Activate()
        {
            if (ReadActiveState())
                return;

            float lifetime = math.max(0.01f, lifetimeSeconds);
            float radius = math.max(0.1f, pullRadius);
            Vector3 position = transform.position;
            Vector3 shellPosition = position + (transform.forward * radius);
            AuxiliaryEquipmentRouterRuntime.TryDeployGravityTether(shellPosition, position, lifetime);
        }

        public void Deactivate()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelGravityTether(transform.position, math.max(0.1f, pullRadius) + 0.5f);
        }

        private bool ReadActiveState()
        {
            return AuxiliaryEquipmentRouterRuntime.TryReadNearestRemainingLifetime(
                AuxiliaryEquipmentConstants.GravityTetherPrefabHash,
                transform.position,
                math.max(0.1f, pullRadius) + 0.5f,
                out _);
        }
    }
}
