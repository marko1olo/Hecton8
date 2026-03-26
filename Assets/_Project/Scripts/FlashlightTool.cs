// ============================================================================
// HECTON-8 — FlashlightTool.cs
// Hand-tool adapter over the existing PlayerFlashlight system.
// Does not create a second flashlight pipeline.
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Input;
    using Hecton8.UI;
    using UnityEngine;

    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Gameplay/Tools/Flashlight Tool")]
    public sealed class FlashlightTool : PlayerTool
    {
        [Header("── Adapter ─────────────────────────────────")]
        [SerializeField] private bool autoTurnOffOnUnequip = true;
        [SerializeField] private bool secondaryShowsStatus = true;

        private PlayerFlashlight _flashlight;
        private HUDNotification _hudNotification;
        private bool _stateBeforeEquip;
        private bool _primaryLatched;
        private bool _secondaryLatched;
        private bool _missingFlashlightWarned;

        public override void OnEquip()
        {
            base.OnEquip();

            ResolveRuntimeReferences();
            _stateBeforeEquip = _flashlight != null && _flashlight.IsOn;
            _primaryLatched = false;
            _secondaryLatched = false;
        }

        public override void OnUnequip()
        {
            if (autoTurnOffOnUnequip &&
                _flashlight != null &&
                !_stateBeforeEquip &&
                _flashlight.IsOn)
            {
                _flashlight.TurnOff();
            }

            _primaryLatched = false;
            _secondaryLatched = false;
            base.OnUnequip();
        }

        public override void UsePrimary(float deltaTime)
        {
            if (_primaryLatched)
                return;

            _primaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            _flashlight.Toggle();
            ShowInfo(_flashlight.IsOn ? "DIVE LAMP — ON" : "DIVE LAMP — OFF");
        }

        public override void UseSecondary(float deltaTime)
        {
            if (!secondaryShowsStatus || _secondaryLatched)
                return;

            _secondaryLatched = true;

            if (!TryResolveFlashlight())
                return;

            ShowInfo(_flashlight.IsOn ? "DIVE LAMP — READY" : "DIVE LAMP — STANDBY");
        }

        public override void ToolTick(float deltaTime)
        {
            InputManager input = InputManager.Instance;
            if (input == null)
                return;

            if (!input.IsPrimaryActionHeld)
                _primaryLatched = false;

            if (!input.IsSecondaryActionHeld)
                _secondaryLatched = false;
        }

        private void ResolveRuntimeReferences()
        {
            if (_flashlight == null)
                _flashlight = GetComponentInParent<PlayerFlashlight>();

            if (_flashlight == null)
                _flashlight = FindFirstObjectByType<PlayerFlashlight>();

            if (_hudNotification == null)
                _hudNotification = FindFirstObjectByType<HUDNotification>();
        }

        private bool TryResolveFlashlight()
        {
            ResolveRuntimeReferences();

            if (_flashlight != null)
                return true;

            if (!_missingFlashlightWarned)
            {
                Debug.LogWarning("[FlashlightTool] No PlayerFlashlight found in scene.");
                _missingFlashlightWarned = true;
            }

            return false;
        }

        private void ShowInfo(string message)
        {
            if (_hudNotification != null)
                _hudNotification.ShowInfo(message);
            else
                Debug.Log(message);
        }
    }
}
