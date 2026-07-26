using System;
using System.Collections.Generic;
using System.Globalization;
using Hecton8.Core;
using Hecton8.Input;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Supported UI setting kinds exposed by mods.
    /// </summary>
    internal enum ModSettingKind
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
    internal struct ModSettingView
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
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint ModSettingCallbackExceptionWarningHash = 0x4D534346u; // MSCF
        private const string ModSettingCallbackExceptionDisableReason = "Mod setting callback threw.";

        // COLD ALLOC: List<SettingEntry>[32] — registered mod setting entries — owner: ModSettingsRegistry
        private static readonly List<SettingEntry> _entries = new List<SettingEntry>(32);
        // COLD ALLOC: Dictionary<string,int>[32] — compound key to setting index lookup — owner: ModSettingsRegistry
        private static readonly Dictionary<uint, int> _entryIndexByHash = new Dictionary<uint, int>(32);
        private static UserOptionsPersistence s_userOptions;
        private static bool s_pendingFullStage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _entries.Clear();
            _entryIndexByHash.Clear();
            s_userOptions = null;
            s_pendingFullStage = false;
        }

        internal static void BindRegistryServicesCold()
        {
            CacheUserOptions(GlobalRegistry.UserOptions);
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.UserOptionsRuntime)
                CacheUserOptions(currentService as UserOptionsPersistence);
        }

        internal static void RegisterToggle(string modId, string settingName, bool defaultValue, Action<bool> onValueChanged)
        {
            if (!TryGetCompoundHash(modId, settingName, out uint compoundHash))
                return;

            string storageKey = BuildStorageKey(compoundHash);
            UserOptionsPersistence options = ResolveUserOptions();
            bool value = options != null ? options.GetBool(storageKey, defaultValue) : defaultValue;

            SettingEntry entry = new SettingEntry
            {
                ModId = modId,
                ModHash = ModCommandDispatcher.ComputeModHash(modId),
                SettingName = settingName,
                DisplayName = settingName,
                StorageKey = storageKey,
                KeyHash = compoundHash,
                Kind = ModSettingKind.Toggle,
                BoolValue = value,
                DefaultBoolValue = defaultValue,
                BoolChanged = onValueChanged
            };

            AddOrUpdateEntry(compoundHash, entry);
            InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, entry.BoolValue);
        }

        internal static void RegisterSlider(string modId, string settingName, float defaultValue, float minValue, float maxValue, Action<float> onValueChanged)
        {
            if (!TryGetCompoundHash(modId, settingName, out uint compoundHash))
                return;

            float safeMin = Mathf.Min(minValue, maxValue);
            float safeMax = Mathf.Max(minValue, maxValue);
            float safeDefault = Mathf.Clamp(defaultValue, safeMin, safeMax);

            string storageKey = BuildStorageKey(compoundHash);
            UserOptionsPersistence options = ResolveUserOptions();
            float value = options != null
                ? Mathf.Clamp(options.GetFloat(storageKey, safeDefault), safeMin, safeMax)
                : safeDefault;

            SettingEntry entry = new SettingEntry
            {
                ModId = modId,
                ModHash = ModCommandDispatcher.ComputeModHash(modId),
                SettingName = settingName,
                DisplayName = settingName,
                StorageKey = storageKey,
                KeyHash = compoundHash,
                Kind = ModSettingKind.Slider,
                FloatValue = value,
                MinValue = safeMin,
                MaxValue = safeMax,
                DefaultFloatValue = safeDefault,
                FloatChanged = onValueChanged
            };

            AddOrUpdateEntry(compoundHash, entry);
            InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, entry.FloatValue);
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

        internal static void UnregisterModSettings(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId) || _entries.Count == 0)
                return;

            uint modHash = ModCommandDispatcher.ComputeModHash(modId);
            bool removed = false;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(_entries[i].ModId, modId, StringComparison.Ordinal))
                    continue;

                RemoveEntryAt(i);
                removed = true;
            }

            if (removed)
                ModRegistryEvents.NotifySettingsRegistryChanged(modHash, 0u);
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

            UserOptionsPersistence options = ResolveUserOptions();
            if (options != null)
            {
                options.SetBool(entry.StorageKey, value);
                TrySaveUserOptions(options, entry.StorageKey);
            }
            else
            {
                s_pendingFullStage = true;
            }

            InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, value);
            ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
            return true;
        }

        internal static bool TryApplySlider(string modId, string settingName, float value, bool persist = true)
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

            UserOptionsPersistence options = ResolveUserOptions();
            if (options != null)
            {
                options.SetFloat(entry.StorageKey, clamped);
                if (persist)
                    TrySaveUserOptions(options, entry.StorageKey);
            }
            else if (persist)
            {
                s_pendingFullStage = true;
            }

            InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, clamped);
            if (persist)
                ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
            return true;
        }

        internal static bool TryPersistSetting(string modId, string settingName)
        {
            if (!TryGetEntry(modId, settingName, out int index))
                return false;

            UserOptionsPersistence options = ResolveUserOptions();
            if (options == null)
            {
                s_pendingFullStage = true;
                return false;
            }

            SettingEntry entry = _entries[index];
            StageEntry(options, entry);
            if (!TrySaveUserOptions(options, entry.StorageKey))
                return false;

            ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
            return true;
        }

        private static UserOptionsPersistence ResolveUserOptions()
        {
            UserOptionsPersistence cachedOptions = s_userOptions;
            if (IsUserOptionsRuntimeUsable(cachedOptions))
                return cachedOptions;

            CacheUserOptions(GlobalRegistry.UserOptions);
            return s_userOptions;
        }

        private static void CacheUserOptions(UserOptionsPersistence options)
        {
            s_userOptions = IsUserOptionsRuntimeUsable(options) ? options : null;
            if (s_userOptions == null)
                return;

            if (s_pendingFullStage)
            {
                TrySaveUserOptions(s_userOptions, "pending");
                return;
            }

            HydrateEntriesFromUserOptions(s_userOptions);
        }

        private static void HydrateEntriesFromUserOptions(UserOptionsPersistence options)
        {
            if (options == null || _entries.Count == 0)
                return;

            int index = 0;
            while (index < _entries.Count)
            {
                SettingEntry entry = _entries[index];
                if (!TryHydrateEntryFromUserOptions(options, ref entry))
                {
                    index++;
                    continue;
                }

                _entries[index] = entry;
                if (entry.Kind == ModSettingKind.Toggle)
                    InvokeToggleCallback(entry.ModId, entry.ModHash, entry.BoolChanged, entry.BoolValue);
                else if (entry.Kind == ModSettingKind.Slider)
                    InvokeSliderCallback(entry.ModId, entry.ModHash, entry.FloatChanged, entry.FloatValue);

                ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
                if (index < _entries.Count && _entries[index].KeyHash == entry.KeyHash)
                    index++;
            }
        }

        private static bool TryHydrateEntryFromUserOptions(UserOptionsPersistence options, ref SettingEntry entry)
        {
            if (options == null)
                return false;

            if (entry.Kind == ModSettingKind.Toggle)
            {
                bool storedValue = options.GetBool(entry.StorageKey, entry.DefaultBoolValue);
                if (entry.BoolValue == storedValue)
                    return false;

                entry.BoolValue = storedValue;
                return true;
            }

            if (entry.Kind == ModSettingKind.Slider)
            {
                float storedValue = Mathf.Clamp(
                    options.GetFloat(entry.StorageKey, entry.DefaultFloatValue),
                    entry.MinValue,
                    entry.MaxValue);
                if (Mathf.Approximately(entry.FloatValue, storedValue))
                    return false;

                entry.FloatValue = storedValue;
                return true;
            }

            return false;
        }

        private static bool TrySaveUserOptions(UserOptionsPersistence options, string storageKey)
        {
            if (options == null)
                return false;

            if (s_pendingFullStage)
                StageAllEntries(options);

            if (options.TrySave())
            {
                s_pendingFullStage = false;
                return true;
            }

            s_pendingFullStage = true;
            Hecton8.Core.H8Debug.LogWarning("[ModSettingsRegistry] Failed to persist option '" + storageKey + "'.");
            return false;
        }

        private static void StageAllEntries(UserOptionsPersistence options)
        {
            if (options == null)
                return;

            for (int i = 0; i < _entries.Count; i++)
                StageEntry(options, _entries[i]);
        }

        private static void StageEntry(UserOptionsPersistence options, SettingEntry entry)
        {
            if (options == null)
                return;

            if (entry.Kind == ModSettingKind.Toggle)
            {
                options.SetBool(entry.StorageKey, entry.BoolValue);
                return;
            }

            if (entry.Kind == ModSettingKind.Slider)
                options.SetFloat(entry.StorageKey, entry.FloatValue);
        }

        private static bool IsUserOptionsRuntimeUsable(UserOptionsPersistence options)
        {
            return options != null &&
                   options.IsServiceReady &&
                   options.isActiveAndEnabled;
        }

        private const int MaxSettingEntries = 4096;

        private static void AddOrUpdateEntry(uint compoundHash, SettingEntry entry)
        {
            if (_entryIndexByHash.TryGetValue(compoundHash, out int index))
            {
                _entries[index] = entry;
                ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
                return;
            }

            if (_entries.Count >= MaxSettingEntries)
            {
                H8Debug.LogWarning("[ModSettingsRegistry] Setting registration cap reached; ignoring additional mod setting.");
                return;
            }

            _entryIndexByHash.Add(compoundHash, _entries.Count);
            _entries.Add(entry);
            ModRegistryEvents.NotifySettingsRegistryChanged(entry.ModHash, entry.KeyHash);
        }

        private static void RemoveEntryAt(int index)
        {
            if ((uint)index >= (uint)_entries.Count)
                return;

            SettingEntry removed = _entries[index];
            _entryIndexByHash.Remove(removed.KeyHash);

            int lastIndex = _entries.Count - 1;
            if (index != lastIndex)
            {
                SettingEntry moved = _entries[lastIndex];
                _entries[index] = moved;
                _entryIndexByHash[moved.KeyHash] = index;
            }

            _entries.RemoveAt(lastIndex);
        }

        private static bool TryGetEntry(string modId, string settingName, out int index)
        {
            index = -1;
            return TryGetCompoundHash(modId, settingName, out uint compoundHash) &&
                   _entryIndexByHash.TryGetValue(compoundHash, out index);
        }

        private static bool TryGetCompoundHash(string modId, string settingName, out uint compoundHash)
        {
            compoundHash = 0u;
            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(settingName))
            {
                Hecton8.Core.H8Debug.LogWarning("[ModSettingsRegistry] Refused to register a setting with an empty modId or settingName.");
                return false;
            }

            compoundHash = ComputeCompoundHash(modId, settingName);
            return true;
        }

        private static uint ComputeCompoundHash(string modId, string settingName)
        {
            uint hash = FnvOffsetBasis;
            hash = AccumulateFnv(hash, modId);
            hash = AccumulateFnv(hash, '|');
            return AccumulateFnv(hash, settingName);
        }

        private static uint AccumulateFnv(uint hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
                hash = AccumulateFnv(hash, value[i]);

            return hash;
        }

        private static uint AccumulateFnv(uint hash, char value)
        {
            unchecked
            {
                hash ^= (byte)(value & 0xFF);
                hash *= FnvPrime;
                hash ^= (byte)(value >> 8);
                hash *= FnvPrime;
                return hash;
            }
        }

        private static string BuildStorageKey(uint compoundHash)
        {
            return "Hecton_ModSetting_" + compoundHash.ToString("X8", CultureInfo.InvariantCulture);
        }

        private static void InvokeToggleCallback(string modId, uint modHash, Action<bool> callback, bool value)
        {
            if (callback == null)
                return;

            try
            {
                using (ModExecutionScope.Enter(modId, modHash))
                {
                    callback(value);
                }
            }
            catch (Exception exception)
            {
                ReportSettingCallbackException(modId, modHash, exception);
            }
        }

        private static void InvokeSliderCallback(string modId, uint modHash, Action<float> callback, float value)
        {
            if (callback == null)
                return;

            try
            {
                using (ModExecutionScope.Enter(modId, modHash))
                {
                    callback(value);
                }
            }
            catch (Exception exception)
            {
                ReportSettingCallbackException(modId, modHash, exception);
            }
        }

        private static void ReportSettingCallbackException(string modId, uint modHash, Exception exception)
        {
            PublishPerformanceWarningBestEffort(ModSettingCallbackExceptionWarningHash, modHash, 1f);
            Hecton8.Core.H8Debug.LogWarning($"[ModSettingsRegistry] Callback failed for mod '{modId}': {exception}");
            ModLoader.DisableMod(modId, ModSettingCallbackExceptionDisableReason);
        }

        private static void PublishPerformanceWarningBestEffort(uint warningHash, uint contextHash, float value)
        {
            try
            {
                GlobalTelemetryBus.PublishPerformanceWarning(warningHash, contextHash, value);
            }
            catch (Exception telemetryException)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[ModSettingsRegistry] telemetry failed: " + telemetryException.Message);
#endif
            }
        }

        private struct SettingEntry
        {
            public string ModId;
            public uint ModHash;
            public string SettingName;
            public string DisplayName;
            public string StorageKey;
            public uint KeyHash;
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
