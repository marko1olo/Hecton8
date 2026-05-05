using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Hecton.UI.MainMenu
{
    /// <summary>
    /// Stateless Main Menu EventSystem repair path for authored scenes with stale or missing UI action references.
    /// </summary>
    internal static class MainMenuInputRoutingGuard
    {
        private static readonly uint _UiInputRepairWarningHash = unchecked((uint)LocHash.Compute("MainMenu.UIInput.Repair"));
        private static readonly uint _UiInputRoutingContextHash = unchecked((uint)LocHash.Compute("InputSystemUIInputModule"));
        private static bool _repairTelemetryPublished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _repairTelemetryPublished = false;
        }

        public static void EnsureInputSystemEventRouting()
        {
            bool createdEventSystem = false;
            bool removedLegacyModule = false;
            bool addedInputModule = false;
            bool assignedDefaultActions = false;

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                GameObject eventSystemRoot = new GameObject("EventSystem", typeof(EventSystem)); // COLD ALLOC: GameObject[1] - menu fallback event system root - owner: MainMenuInputRoutingGuard
                eventSystemRoot.hideFlags = HideFlags.DontSave;
                eventSystem = eventSystemRoot.GetComponent<EventSystem>();
                createdEventSystem = true;
            }

            if (eventSystem == null)
                return;

            eventSystem.enabled = true;
            eventSystem.sendNavigationEvents = true;

            StandaloneInputModule legacyInputModule = eventSystem.GetComponent<StandaloneInputModule>();
            if (legacyInputModule != null)
            {
                removedLegacyModule = true;
                legacyInputModule.enabled = false;
                if (Application.isPlaying)
                    Object.Destroy(legacyInputModule);
                else
                    Object.DestroyImmediate(legacyInputModule);
            }

            if (!eventSystem.TryGetComponent(out InputSystemUIInputModule inputSystemModule))
            {
                inputSystemModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                addedInputModule = true;
            }

            if (inputSystemModule == null)
                return;

            inputSystemModule.enabled = true;

            InputManager inputManager = GlobalRegistry.NativeInputManager;
            bool configuredProjectActions = false;
            if (inputManager != null)
                configuredProjectActions = inputManager.TryConfigureUiInputModule(inputSystemModule) &&
                                           HasUsableUiModuleActions(inputSystemModule);

            if (!configuredProjectActions && !HasUsableUiModuleActions(inputSystemModule))
            {
                inputSystemModule.AssignDefaultActions();
                assignedDefaultActions = true;
            }

            if (createdEventSystem ||
                removedLegacyModule ||
                addedInputModule ||
                assignedDefaultActions ||
                !configuredProjectActions)
            {
                PublishRepairTelemetry(
                    createdEventSystem,
                    removedLegacyModule,
                    addedInputModule,
                    assignedDefaultActions,
                    HasUsableUiModuleActions(inputSystemModule));
            }
        }

        internal static bool HasUsableUiModuleActions(InputSystemUIInputModule inputSystemModule)
        {
            return inputSystemModule != null &&
                   inputSystemModule.actionsAsset != null &&
                   HasUsableActionReference(inputSystemModule.point) &&
                   HasUsableActionReference(inputSystemModule.leftClick) &&
                   HasUsableActionReference(inputSystemModule.move) &&
                   HasUsableActionReference(inputSystemModule.submit) &&
                   HasUsableActionReference(inputSystemModule.cancel);
        }

        private static bool HasUsableActionReference(InputActionReference reference)
        {
            return reference != null &&
                   reference.action != null &&
                   reference.action.bindings.Count > 0;
        }

        private static void PublishRepairTelemetry(
            bool createdEventSystem,
            bool removedLegacyModule,
            bool addedInputModule,
            bool assignedDefaultActions,
            bool usableAfterRepair)
        {
            if (_repairTelemetryPublished)
                return;

            _repairTelemetryPublished = true;
            float repairCode = 0f;
            if (createdEventSystem)
                repairCode += 1f;
            if (removedLegacyModule)
                repairCode += 2f;
            if (addedInputModule)
                repairCode += 4f;
            if (assignedDefaultActions)
                repairCode += 8f;
            if (!usableAfterRepair)
                repairCode += 16f;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _UiInputRepairWarningHash,
                _UiInputRoutingContextHash,
                repairCode);
        }
    }
}
