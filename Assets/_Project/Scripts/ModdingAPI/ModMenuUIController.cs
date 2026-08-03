using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Optional settings-panel controller that renders discovered mods and their registered UI settings.
    /// This view is inactive until a scene prefab wires the template references.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModMenuUIController : MonoBehaviour
    {
        [Header("Mod List")]
        [SerializeField] private Transform modListContainer;
        [SerializeField] private ModMenuModEntryView modEntryTemplate;

        [Header("Settings")]
        [SerializeField] private Transform modSettingsContainer;
        [SerializeField] private ModMenuSettingToggleView toggleSettingTemplate;
        [SerializeField] private ModMenuSettingSliderView sliderSettingTemplate;

        [Header("Empty State")]
        [SerializeField] private TMP_Text emptyStateLabel;

        // COLD ALLOC: List<ModRuntimeInfo>[16] - view-model cache for loaded mods UI - owner: ModMenuUIController
        private readonly List<ModRuntimeInfo> _mods = new List<ModRuntimeInfo>(16);
        // COLD ALLOC: List<ModSettingView>[32] - view-model cache for mod settings UI - owner: ModMenuUIController
        private readonly List<ModSettingView> _settings = new List<ModSettingView>(32);
        // COLD ALLOC: List<ModMenuModEntryView>[16] - pooled row views for mod list UI - owner: ModMenuUIController
        private readonly List<ModMenuModEntryView> _modViews = new List<ModMenuModEntryView>(16);
        // COLD ALLOC: List<ModMenuSettingToggleView>[16] - pooled toggle row views - owner: ModMenuUIController
        private readonly List<ModMenuSettingToggleView> _toggleViews = new List<ModMenuSettingToggleView>(16);
        // COLD ALLOC: List<ModMenuSettingSliderView>[16] - pooled slider row views - owner: ModMenuUIController
        private readonly List<ModMenuSettingSliderView> _sliderViews = new List<ModMenuSettingSliderView>(16);
        private ModRegistryEventAdapter _modRegistryEventAdapter;
        private bool _modRegistryEventRegistered;

        private void OnEnable()
        {
            TryRegisterModRegistryListener();
            RefreshView();
        }

        private void OnDisable()
        {
            if (_modRegistryEventRegistered && _modRegistryEventAdapter != null)
                ModRegistryEvents.Unregister(_modRegistryEventAdapter);

            _modRegistryEventRegistered = false;
        }

        /// <summary>
        /// Rebuilds the UI from the current runtime registries.
        /// Cold settings-panel path only: synchronous rebuild, no coroutine/iterator state machine.
        /// </summary>
        public void RefreshView()
        {
            TryRegisterModRegistryListener();
            ModLoader.CollectRuntimeInfo(_mods);
            ModSettingsRegistry.CollectSettings(_settings);
            RebuildUI();
        }

        private void RebuildUI()
        {
            if (emptyStateLabel != null)
            {
                bool hasContent = _mods.Count > 0 || _settings.Count > 0;
                emptyStateLabel.gameObject.SetActive(!hasContent);
                if (!hasContent)
                    Hecton8.UI.TmpTextNoAlloc.Set(emptyStateLabel, "No mods loaded.");
            }

            int visibleCount = _mods.Count;
            for (int i = 0; i < visibleCount; i++)
            {
                ModMenuModEntryView view = GetOrCreateModView(i, out _);
                if (view == null)
                    break;

                view.Bind(_mods[i]);
                view.gameObject.SetActive(true);
            }

            for (int i = visibleCount; i < _modViews.Count; i++)
                _modViews[i].gameObject.SetActive(false);

            int toggleCount = 0;
            int sliderCount = 0;

            for (int i = 0; i < _settings.Count; i++)
            {
                ModSettingView view = _settings[i];

                if (view.Kind == ModSettingKind.Toggle)
                {
                    ModMenuSettingToggleView toggleView = GetOrCreateToggleView(toggleCount++, out _);
                    if (toggleView == null)
                        break;

                    toggleView.Bind(view);
                    toggleView.gameObject.SetActive(true);
                }
                else
                {
                    ModMenuSettingSliderView sliderView = GetOrCreateSliderView(sliderCount++, out _);
                    if (sliderView == null)
                        break;

                    sliderView.Bind(view);
                    sliderView.gameObject.SetActive(true);
                }
            }

            for (int i = toggleCount; i < _toggleViews.Count; i++)
                _toggleViews[i].gameObject.SetActive(false);

            for (int i = sliderCount; i < _sliderViews.Count; i++)
                _sliderViews[i].gameObject.SetActive(false);
        }

        /// <summary>
        /// Handles deferred mod registry invalidation events.
        /// </summary>
        /// <param name="payload">Unmanaged mod registry payload.</param>
        private void HandleModRegistryEvent(in ModRegistryEventPayload payload)
        {
            ModRegistryEventType eventType = (ModRegistryEventType)payload.EventType;
            if (eventType != ModRegistryEventType.RuntimeRegistryChanged &&
                eventType != ModRegistryEventType.SettingsRegistryChanged)
            {
                return;
            }

            RefreshView();
        }

        private ModRegistryEventAdapter GetModRegistryEventAdapter()
        {
            if (_modRegistryEventAdapter == null)
                _modRegistryEventAdapter = new ModRegistryEventAdapter(this); // COLD ALLOC: ModRegistryEventAdapter[1] - internal mod registry invalidation listener bridge - owner: ModMenuUIController

            return _modRegistryEventAdapter;
        }

        private void TryRegisterModRegistryListener()
        {
            if (_modRegistryEventRegistered || !isActiveAndEnabled)
                return;

            _modRegistryEventRegistered = ModRegistryEvents.Register(GetModRegistryEventAdapter());
        }

        private sealed class ModRegistryEventAdapter : IModRegistryEventListener
        {
            private readonly ModMenuUIController _owner;

            public ModRegistryEventAdapter(ModMenuUIController owner)
            {
                _owner = owner;
            }

            void IModRegistryEventListener.OnModRegistryEvent(in ModRegistryEventPayload payload)
            {
                _owner.HandleModRegistryEvent(in payload);
            }
        }

        private ModMenuModEntryView GetOrCreateModView(int index, out bool instantiated)
        {
            instantiated = false;
            while (_modViews.Count <= index)
            {
                if (modEntryTemplate == null || modListContainer == null)
                    return null;

                ModMenuModEntryView instance = Instantiate(modEntryTemplate, modListContainer); // COLD ALLOC: UI row clone for mods panel - owner: ModMenuUIController
                instance.gameObject.SetActive(false);
                _modViews.Add(instance);
                instantiated = true;
            }

            return _modViews[index];
        }

        private ModMenuSettingToggleView GetOrCreateToggleView(int index, out bool instantiated)
        {
            instantiated = false;
            while (_toggleViews.Count <= index)
            {
                if (toggleSettingTemplate == null || modSettingsContainer == null)
                    return null;

                ModMenuSettingToggleView instance = Instantiate(toggleSettingTemplate, modSettingsContainer); // COLD ALLOC: UI row clone for mods panel - owner: ModMenuUIController
                instance.gameObject.SetActive(false);
                _toggleViews.Add(instance);
                instantiated = true;
            }

            return _toggleViews[index];
        }

        private ModMenuSettingSliderView GetOrCreateSliderView(int index, out bool instantiated)
        {
            instantiated = false;
            while (_sliderViews.Count <= index)
            {
                if (sliderSettingTemplate == null || modSettingsContainer == null)
                    return null;

                ModMenuSettingSliderView instance = Instantiate(sliderSettingTemplate, modSettingsContainer); // COLD ALLOC: UI row clone for mods panel - owner: ModMenuUIController
                instance.gameObject.SetActive(false);
                _sliderViews.Add(instance);
                instantiated = true;
            }

            return _sliderViews[index];
        }
    }
}
