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

        private bool _isActive;

        public bool IsActive => _isActive;
        public float PullRadius => pullRadius;

        private void OnEnable()
        {
            Activate();
        }

        private void OnDisable()
        {
            _isActive = false;
        }

        public void Activate()
        {
            if (_isActive)
                return;

            _isActive = true;
            float lifetime = math.max(0.01f, lifetimeSeconds);
            Vector3 position = transform.position;
            AuxiliaryEquipmentRouterRuntime.TryDeployGravityTether(position, position, lifetime);
        }

        public void Deactivate()
        {
            AuxiliaryEquipmentRouterRuntime.TryCancelGravityTether(transform.position);
            _isActive = false;
        }
    }
}
