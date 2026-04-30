namespace Hecton8.Inventory
{
    using UnityEngine;

    /// <summary>
    /// Module bridge that marks an inventory as protected from depth pressure crush while active.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PressurizedContainer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Inventory protected by this pressure-rated container. Falls back to a PlayerInventory on the same GameObject.")]
        [SerializeField] private PlayerInventory protectedInventory;

        private bool _registered;

        private void Awake()
        {
            if (protectedInventory == null)
                TryGetComponent(out protectedInventory);
        }

        private void OnEnable()
        {
            RegisterProtection();
        }

        private void OnDisable()
        {
            UnregisterProtection();
        }

        /// <summary>
        /// Rebinds this module to a different inventory owner.
        /// </summary>
        /// <param name="inventory">Inventory to protect while this component is enabled.</param>
        public void Bind(PlayerInventory inventory)
        {
            if (_registered)
                UnregisterProtection();

            protectedInventory = inventory;

            if (isActiveAndEnabled)
                RegisterProtection();
        }

        private void RegisterProtection()
        {
            if (_registered || protectedInventory == null)
                return;

            protectedInventory.AddPressurizedContainerProtection();
            _registered = true;
        }

        private void UnregisterProtection()
        {
            if (!_registered || protectedInventory == null)
                return;

            protectedInventory.RemovePressurizedContainerProtection();
            _registered = false;
        }
    }
}
