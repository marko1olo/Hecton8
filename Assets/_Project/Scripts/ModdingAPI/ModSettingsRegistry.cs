using System;
using System.Collections.Generic;
using Hecton8.Input;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Supported UI setting kinds exposed by mods.
    /// </summary>
    public enum ModSettingKind
    {
        /// <summary>
        /// Boolean toggle.
        /// </summary>
        Toggle = 0,

        /// <summary>
        /// Floating-point slider.
        /// </summary>
        Slider = 1
    }

    /// <summary>
    /// UI-facing immutable snapshot of a registered mod setting.
    /// </summary>
    [Serializable]
    public struct ModSettingView
    {
        /// <summary>
        /// Stable mod identifier that owns this setting.
        /// </summary>
        public string ModId;

        /// <summary>
        /// Stable setting key inside the owning mod namespace.
        /// </summary>
        public string SettingName;

        /// <summary>
        /// Human-readable label shown in UI.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Setting kind to render.
        /// </summary>
        public ModSettingKind Kind;

        /// <summary>
        /// Current boolean value for toggle settings.
        /// </summary>
        public bool BoolValue;

        /// <summary>
        /// Current float value for slider settings.
        /// </summary>
        public float FloatValue;

        /// <summary>
        /// Minimum slider value.
        /// </summary>
        public float MinValue;

        /// <summary>
        /// Maximum slider value.
        /// </summary>
        public float MaxValue;

        /// <summary>
        /// Default boolean value declared by the mod.
        /// </summary>
        public bool DefaultBoolValue;

        /// <summary>
        /// Default float value declared by the mod.
        /// </summary>
        public float DefaultFloatValue;
    }

    /// <summary>
    /// Runtime registry for mod-owned player settings backed by the first-party user options owner.
    /// </summary>
    internal static class ModSettingsRegistry
    {
        // COLD ALLOC: List<SettingEntry>[32] — registered mod setting entries — owner: ModSettingsRegistry
        private static readonly List<SettingEntry> _entries = new List<SettingEntry>(32);
        // COLD ALLOC: Dictionary<string,int>[32] — compound key to setting index lookup — owner: ModSettingsRegistry
        private static readonly Dictionary<string, int> _entryIndexByKey = new Dictionary<string, int>(32);

        internal static event Action RegistryChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            _entryIndexByKey.Clear();
            RegistryChanged = null;
        }

        internal static void RegisterToggle(string modId, string settingName, bool defaultValue, Action<bool> onValueChanged)
        {
            if (!TryGetCompoundKey(modId, settingName, out string compoundKey))
                return;

            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            bool value = options != null ? options.GetBool(BuildStorageKey(compoundKey), defaultValue) : defaultValue;

            SettingEntry entry = new SettingEntry
            {
                ModId = modId,
                SettingName = settingName,
                DisplayName = settingName,
                Kind = ModSettingKind.Toggle,
                BoolValue = value,
                DefaultBoolValue = defaultValue,
                BoolChanged = onValueChanged
            };

            AddOrUpdateEntry(compoundKey, entry);
            InvokeToggleCallback(entry.ModId, entry.BoolChanged, entry.BoolValue);
        }

        internal static void RegisterSlider(string modId, string settingName, float defaultValue, float minValue, float maxValue, Action<float> onValueChanged)
        {
            if (!TryGetCompoundKey(modId, settingName, out string compoundKey))
                return;

            float safeMin = Mathf.Min(minValue, maxValue);
            float safeMax = Mathf.Max(minValue, maxValue);
            float safeDefault = Mathf.Clamp(defaultValue, safeMin, safeMax);

            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            float value = options != null
                ? Mathf.Clamp(options.GetFloat(BuildStorageKey(compoundKey), safeDefault), safeMin, safeMax)
                : safeDefault;

            SettingEntry entry = new SettingEntry
            {
                ModId = modId,
                SettingName = settingName,
                DisplayName = settingName,
                Kind = ModSettingKind.Slider,
                FloatValue = value,
                MinValue = safeMin,
                MaxValue = safeMax,
                DefaultFloatValue = safeDefault,
                FloatChanged = onValueChanged
            };

            AddOrUpdateEntry(compoundKey, entry);
            InvokeSliderCallback(entry.ModId, entry.FloatChanged, entry.FloatValue);
        }

        internal static void CollectSettings(List<ModSettingView> destination)
        {
            if (destination == null)
                return;

            destination.Clear();
            for (int i = 0; i < _entries.Count; i++)
            {
                SettingEntry entry = _entries[i];
                destination.Add(new ModSettingView
                {
                    ModId = entry.ModId,
                    SettingName = entry.SettingName,
                    DisplayName = entry.DisplayName,
                    Kind = entry.Kind,
                    BoolValue = entry.BoolValue,
                    FloatValue = entry.FloatValue,
                    MinValue = entry.MinValue,
                    MaxValue = entry.MaxValue,
                    DefaultBoolValue = entry.DefaultBoolValue,
                    DefaultFloatValue = entry.DefaultFloatValue
                });
            }
        }

        internal static bool TryApplyToggle(string modId, string settingName, bool value)
        {
            if (!TryGetEntry(modId, settingName, out int index))
                return false;

            SettingEntry entry = _entries[index];
            if (entry.Kind != ModSettingKind.Toggle)
                return false;

            if (entry.BoolValue == value)
                return true;

            entry.BoolValue = value;
            _entries[index] = entry;

            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (options != null)
            {
                options.SetBool(BuildStorageKey(BuildCompoundKey(modId, settingName)), value);
                options.Save();
            }

            InvokeToggleCallback(entry.ModId, entry.BoolChanged, value);
            RegistryChanged?.Invoke();
            return true;
        }

        internal static bool TryApplySlider(string modId, string settingName, float value)
        {
            if (!TryGetEntry(modId, settingName, out int index))
                return false;

            SettingEntry entry = _entries[index];
            if (entry.Kind != ModSettingKind.Slider)
                return false;

            float clamped = Mathf.Clamp(value, entry.MinValue, entry.MaxValue);
            if (Mathf.Approximately(entry.FloatValue, clamped))
                return true;

            entry.FloatValue = clamped;
            _entries[index] = entry;

            UserOptionsPersistence options = UserOptionsPersistence.Instance;
            if (options != null)
            {
                options.SetFloat(BuildStorageKey(BuildCompoundKey(modId, settingName)), clamped);
                options.Save();
            }

            InvokeSliderCallback(entry.ModId, entry.FloatChanged, clamped);
            RegistryChanged?.Invoke();
            return true;
        }

        private static void AddOrUpdateEntry(string compoundKey, SettingEntry entry)
        {
            if (_entryIndexByKey.TryGetValue(compoundKey, out int index))
            {
                _entries[index] = entry;
                RegistryChanged?.Invoke();
                return;
            }

            _entryIndexByKey.Add(compoundKey, _entries.Count);
            _entries.Add(entry);
            RegistryChanged?.Invoke();
        }

        private static bool TryGetEntry(string modId, string settingName, out int index)
        {
            return _entryIndexByKey.TryGetValue(BuildCompoundKey(modId, settingName), out index);
        }

        private static bool TryGetCompoundKey(string modId, string settingName, out string compoundKey)
        {
            compoundKey = string.Empty;
            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(settingName))
            {
                Debug.LogWarning("[ModSettingsRegistry] Refused to register a setting with an empty modId or settingName.");
                return false;
            }

            compoundKey = BuildCompoundKey(modId, settingName);
            return true;
        }

        private static string BuildCompoundKey(string modId, string settingName)
        {
            return modId + "|" + settingName;
        }

        private static string BuildStorageKey(string compoundKey)
        {
            return "Hecton_ModSetting_" + compoundKey;
        }

        private static void InvokeToggleCallback(string modId, Action<bool> callback, bool value)
        {
            if (callback == null)
                return;

            try
            {
                using (ModExecutionScope.Enter(modId))
                {
                    callback(value);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ModSettingsRegistry] Toggle callback failed for mod '{modId}': {exception}");
            }
        }

        private static void InvokeSliderCallback(string modId, Action<float> callback, float value)
        {
            if (callback == null)
                return;

            try
            {
                using (ModExecutionScope.Enter(modId))
                {
                    callback(value);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[ModSettingsRegistry] Slider callback failed for mod '{modId}': {exception}");
            }
        }

        private struct SettingEntry
        {
            public string ModId;
            public string SettingName;
            public string DisplayName;
            public ModSettingKind Kind;
            public bool BoolValue;
            public float FloatValue;
            public float MinValue;
            public float MaxValue;
            public bool DefaultBoolValue;
            public float DefaultFloatValue;
            public Action<bool> BoolChanged;
            public Action<float> FloatChanged;
        }
    }
}
