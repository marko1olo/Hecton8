using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Crafting;
using Hecton8.Items;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Modding
{
    /// <summary>
    /// Public facade for supported mod interactions with the live game.
    /// Mods should prefer this API over direct access to internal gameplay classes.
    /// </summary>
    public static class HectonAPI
    {
        /// <summary>
        /// Event-facing mod API.
        /// Use this surface for safe subscriptions instead of binding directly to first-party runtime owners.
        /// </summary>
        public static class Events
        {
            /// <summary>
            /// Subscribes to a typed modding event.
            /// Handlers are isolated behind try/catch so one broken mod does not break the entire dispatch chain.
            /// </summary>
            /// <typeparam name="TEvent">Concrete event payload type to receive.</typeparam>
            /// <param name="handler">Method invoked on each dispatch.</param>
            /// <param name="subscriberId">
            /// Optional stable diagnostic ID for exception logs.
            /// When omitted, the current mod loader scope ID is used if available.
            /// </param>
            /// <returns>
            /// A disposable subscription token. Dispose it from <c>IHectonMod.OnUnload()</c> to stop receiving events.
            /// </returns>
            public static HectonEventSubscription Subscribe<TEvent>(Action<TEvent> handler, string subscriberId = null)
                where TEvent : HectonEvent
            {
                return HectonEventBus.Subscribe(handler, subscriberId);
            }

            /// <summary>
            /// Removes a previously created event subscription.
            /// This is a convenience wrapper around <see cref="IDisposable.Dispose"/>.
            /// </summary>
            /// <param name="subscription">Subscription token returned by <see cref="Subscribe{TEvent}"/>.</param>
            public static void Unsubscribe(HectonEventSubscription subscription)
            {
                subscription?.Dispose();
            }

            /// <summary>
            /// Publishes a typed event into the global modding event bus.
            /// Mods may use this for custom coordination, while first-party owners use the same pipeline for supported gameplay hooks.
            /// </summary>
            /// <typeparam name="TEvent">Concrete event payload type being published.</typeparam>
            /// <param name="evt">Payload instance shared with every subscriber.</param>
            /// <returns>
            /// The same payload instance after all handlers ran.
            /// This lets caller code inspect mutations or cancellation state.
            /// </returns>
            public static TEvent Publish<TEvent>(TEvent evt)
                where TEvent : HectonEvent
            {
                return HectonEventBus.Publish(evt);
            }
        }

        /// <summary>
        /// Item-facing mod API for registering and resolving supported runtime content.
        /// </summary>
        public static class Items
        {
            /// <summary>
            /// Registers a custom <see cref="ItemData"/> into the live item catalog without mutating the authored ScriptableObject asset.
            /// If the player-facing catalog is not available yet, the item is queued and injected on the first bootstrap-ready frame.
            /// </summary>
            /// <param name="data">Authored item asset to expose to runtime systems and save/load lookups.</param>
            /// <returns>
            /// True when the item was accepted for runtime registration or deferred injection.
            /// False when the item is invalid or collides with an existing stable identifier.
            /// </returns>
            public static bool RegisterCustomItem(ItemData data)
            {
                bool success = ModItemRegistry.TryRegister(data, out string error);
                if (!success)
                {
                    Debug.LogWarning(
                        $"[HectonAPI.Items] Failed to register custom item '{(data != null ? data.name : "null")}': {error}");
                }

                return success;
            }

            /// <summary>
            /// Resolves an item by stable ID through the active runtime item catalog.
            /// This includes authored items plus mod-injected runtime registrations.
            /// </summary>
            /// <param name="persistentId">Stable item identifier used by saves and catalogs.</param>
            /// <param name="itemData">Resolved item asset when found.</param>
            /// <returns>True when the active runtime catalog exists and the ID resolves successfully.</returns>
            public static bool TryFindItem(string persistentId, out ItemData itemData)
            {
                itemData = null;

                ItemCatalog catalog = ModItemRegistry.ResolveActiveCatalog();
                if (catalog == null || string.IsNullOrWhiteSpace(persistentId))
                    return false;

                itemData = catalog.FindById(persistentId);
                return itemData != null;
            }
        }

        /// <summary>
        /// Crafting-facing mod API for runtime recipe injection.
        /// </summary>
        public static class Crafting
        {
            /// <summary>
            /// Registers a runtime recipe overlay without mutating any authored fabricator recipe list.
            /// Registered recipes are appended to live fabricator views through the supported overlay registry.
            /// Alternative recipes that output an existing first-party item are supported as long as the recipe asset itself is valid.
            /// </summary>
            /// <param name="recipe">Authored recipe asset to expose through the live crafting registry.</param>
            /// <returns>
            /// True when the recipe was accepted by the runtime registry.
            /// False when the recipe payload is invalid.
            /// </returns>
            public static bool RegisterRecipe(RecipeData recipe)
            {
                bool success = ModRecipeRegistry.TryRegister(recipe, out string error);
                if (!success)
                {
                    Debug.LogWarning(
                        $"[HectonAPI.Crafting] Failed to register custom recipe '{(recipe != null ? recipe.name : "null")}': {error}");
                }

                return success;
            }
        }

        /// <summary>
        /// Construction-facing mod API for runtime buildable injection.
        /// </summary>
        public static class Construction
        {
            /// <summary>
            /// Registers a runtime buildable overlay into the live module catalog without mutating the authored ScriptableObject asset list.
            /// The injected buildable becomes visible to supported build UIs, preview cycling, and save-facing module lookup through the active catalog owner.
            /// </summary>
            /// <param name="data">Buildable module definition to expose at runtime.</param>
            /// <param name="customCategory">
            /// Optional runtime-only category label for future Mods-facing build browsers.
            /// This metadata does not overwrite the authored <see cref="BuildableData.family"/> field.
            /// </param>
            /// <returns>
            /// True when the buildable was accepted for runtime registration or deferred injection.
            /// False when the payload is invalid or collides with an existing module identity alias.
            /// </returns>
            public static bool RegisterBuildable(BuildableData data, string customCategory = "Mods")
            {
                bool success = ModBuildableRegistry.TryRegister(data, customCategory, out string error);
                if (!success)
                {
                    Debug.LogWarning(
                        $"[HectonAPI.Construction] Failed to register custom buildable '{(data != null ? data.name : "null")}': {error}");
                }

                return success;
            }

            /// <summary>
            /// Resolves a buildable module definition through the active runtime module catalog.
            /// This includes authored modules plus mod-injected runtime registrations.
            /// </summary>
            /// <param name="persistentId">Stable buildable identifier used by saves and catalogs.</param>
            /// <param name="buildableData">Resolved buildable asset when found.</param>
            /// <returns>True when the active runtime catalog exists and the identifier resolves successfully.</returns>
            public static bool TryFindBuildable(string persistentId, out BuildableData buildableData)
            {
                buildableData = null;

                ModuleCatalog catalog = ModBuildableRegistry.ResolveActiveCatalog();
                if (catalog == null || string.IsNullOrWhiteSpace(persistentId))
                    return false;

                buildableData = catalog.FindDataById(persistentId);
                return buildableData != null;
            }
        }

        /// <summary>
        /// Asset-facing mod API for bundle-backed content loaded from a mod package directory.
        /// </summary>
        public static class Assets
        {
            /// <summary>
            /// Loads a prefab from the specified mod package.
            /// The mod must provide a supported AssetBundle registered by the loader.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name or mod-relative prefab asset path.</param>
            /// <returns>The loaded prefab asset, or null when no matching asset exists.</returns>
            public static GameObject LoadPrefab(string modId, string assetName)
            {
                return ModAssetManager.LoadPrefab(modId, assetName);
            }

            /// <summary>
            /// Loads an audio clip from the specified mod package.
            /// The mod must provide a supported AssetBundle registered by the loader.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name for the target clip.</param>
            /// <returns>The loaded audio clip, or null when no matching clip exists.</returns>
            public static AudioClip LoadAudioClip(string modId, string assetName)
            {
                return ModAssetManager.LoadAudioClip(modId, assetName);
            }

            /// <summary>
            /// Loads a texture from the specified mod package.
            /// The loader first resolves the mod AssetBundle.
            /// If no texture is found there, a raw PNG fallback is attempted when <paramref name="assetName"/> points to a mod-relative file path.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name or mod-relative PNG path.</param>
            /// <returns>The loaded texture, or null when no supported source resolves successfully.</returns>
            public static Texture2D LoadTexture(string modId, string assetName)
            {
                return ModAssetManager.LoadTexture(modId, assetName);
            }
        }

        /// <summary>
        /// Localization-facing mod API for runtime translation injection.
        /// </summary>
        public static class Localization
        {
            /// <summary>
            /// Injects a flat translation table into the live localization owner.
            /// This is the code-driven equivalent of shipping a <c>lang_xx.json</c> file in the mod directory.
            /// </summary>
            /// <param name="language">Target language table to extend.</param>
            /// <param name="entries">Flat key/value pairs to merge into the live localization table.</param>
            /// <param name="overwriteExisting">
            /// True to replace an existing key with the injected value.
            /// False to preserve the current key owner and only add missing translations.
            /// </param>
            public static void InjectTable(
                GameLanguage language,
                Dictionary<string, string> entries,
                bool overwriteExisting = true)
            {
                if (entries == null || entries.Count == 0)
                    return;

                Hecton.Localization.LocalizationManager manager = Hecton.Localization.LocalizationManager.Instance;
                if (manager == null)
                {
                    Debug.LogWarning("[HectonAPI.Localization] LocalizationManager is unavailable. Injection was skipped.");
                    return;
                }

                manager.InjectEntries(language, entries, ModExecutionScope.CurrentModId, overwriteExisting);
            }
        }

        /// <summary>
        /// UI-facing mod API for non-invasive player messaging and supported settings surfaces.
        /// </summary>
        public static class UI
        {
            /// <summary>
            /// Shows an informational HUD message through the live notification owner.
            /// If the HUD instance is not active yet, the message is routed through the notification event bus.
            /// </summary>
            /// <param name="message">User-facing message body.</param>
            public static void ShowInfo(string message)
            {
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowInfo(message ?? string.Empty);
                    return;
                }

                NotificationEvents.PushInfo(message ?? string.Empty);
            }

            /// <summary>
            /// Shows a warning HUD message through the live notification owner.
            /// </summary>
            /// <param name="message">User-facing warning body.</param>
            public static void ShowWarning(string message)
            {
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowWarning(message ?? string.Empty);
                    return;
                }

                NotificationEvents.PushWarning(message ?? string.Empty);
            }

            /// <summary>
            /// Shows a critical HUD message through the live notification owner.
            /// </summary>
            /// <param name="message">User-facing critical body.</param>
            public static void ShowCritical(string message)
            {
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowCritical(message ?? string.Empty);
                    return;
                }

                NotificationEvents.PushCritical(message ?? string.Empty);
            }

            /// <summary>
            /// Registers a mod-owned boolean setting that will appear in the supported Mods settings surface.
            /// The current persisted value is applied to <paramref name="onValueChanged"/> immediately after registration.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="settingName">Stable mod-local setting key.</param>
            /// <param name="defaultValue">Default toggle value used when no persisted preference exists yet.</param>
            /// <param name="onValueChanged">Callback invoked immediately with the current value and again whenever the player changes it.</param>
            public static void RegisterSetting(string modId, string settingName, bool defaultValue, Action<bool> onValueChanged)
            {
                ModSettingsRegistry.RegisterToggle(modId, settingName, defaultValue, onValueChanged);
            }

            /// <summary>
            /// Registers a mod-owned slider setting that will appear in the supported Mods settings surface.
            /// The current persisted value is applied to <paramref name="onValueChanged"/> immediately after registration.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="settingName">Stable mod-local setting key.</param>
            /// <param name="defaultValue">Default slider value used when no persisted preference exists yet.</param>
            /// <param name="onValueChanged">Callback invoked immediately with the current value and again whenever the player changes it.</param>
            /// <param name="minValue">Inclusive lower slider bound. Defaults to 0.</param>
            /// <param name="maxValue">Inclusive upper slider bound. Defaults to 1.</param>
            public static void RegisterSetting(
                string modId,
                string settingName,
                float defaultValue,
                Action<float> onValueChanged,
                float minValue = 0f,
                float maxValue = 1f)
            {
                ModSettingsRegistry.RegisterSlider(modId, settingName, defaultValue, minValue, maxValue, onValueChanged);
            }
        }

        /// <summary>
        /// World-facing mod API for bootstrap-safe access to runtime state that is explicitly supported for mods.
        /// </summary>
        public static class World
        {
            /// <summary>
            /// True after the active gameplay scene finished bootstrap and published a live player object.
            /// </summary>
            public static bool IsGameReady => SceneBootstrap.IsGameReady;

            /// <summary>
            /// Resolves the live player GameObject published by bootstrap.
            /// </summary>
            /// <param name="playerObject">Current bootstrap player object when available.</param>
            /// <returns>True when a player object is currently published by the official bootstrap pipeline.</returns>
            public static bool TryGetPlayerObject(out GameObject playerObject)
            {
                playerObject = SceneBootstrap.CurrentPlayerObject;
                return playerObject != null;
            }

            /// <summary>
            /// Resolves the live player transform published by bootstrap.
            /// </summary>
            /// <param name="playerTransform">Current bootstrap player transform when available.</param>
            /// <returns>True when a player transform is currently published by the official bootstrap pipeline.</returns>
            public static bool TryGetPlayerTransform(out Transform playerTransform)
            {
                playerTransform = SceneBootstrap.CurrentPlayerTransform;
                return playerTransform != null;
            }

            /// <summary>
            /// Spawns a persistent prefab instance from a mod AssetBundle and registers it with the mod world save owner.
            /// Use this API when the spawned object must survive save/load round-trips.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">Prefab asset name inside the mod AssetBundle.</param>
            /// <param name="position">World position for the spawned instance.</param>
            /// <param name="rotation">World rotation for the spawned instance.</param>
            /// <returns>The spawned instance, or null when the prefab or world owners are unavailable.</returns>
            public static GameObject SpawnPersistentPrefab(string modId, string assetName, Vector3 position, Quaternion rotation)
            {
                return ModWorldPersistenceManager.EnsureRuntimeInstance()
                    .SpawnPersistentPrefab(modId, assetName, position, rotation);
            }

            /// <summary>
            /// Removes a previously spawned persistent mod instance from the save registry and returns it to the supported pool owner.
            /// </summary>
            /// <param name="instance">Live instance created by <see cref="SpawnPersistentPrefab"/>.</param>
            /// <returns>True when the instance was recognized and removed successfully.</returns>
            public static bool DespawnPersistentInstance(GameObject instance)
            {
                return ModWorldPersistenceManager.EnsureRuntimeInstance().DespawnPersistentInstance(instance);
            }
        }

        /// <summary>
        /// Save-facing mod API for storing small serialized payloads inside the official game save without changing first-party save owners.
        /// </summary>
        public static class SaveState
        {
            /// <summary>
            /// Writes a string payload into the official mod save dictionary.
            /// Use a namespaced key such as <c>com.hecton.examplemod.player_state</c> to avoid collisions with other mods.
            /// Values may contain JSON or any other text serialization chosen by the mod.
            /// </summary>
            /// <param name="key">Stable fully-qualified mod save key.</param>
            /// <param name="value">Serialized payload text. Null is normalized to an empty string.</param>
            public static void SetModString(string key, string value)
            {
                ModSaveStateStore.SetModString(key, value);
            }

            /// <summary>
            /// Reads a string payload from the official mod save dictionary.
            /// </summary>
            /// <param name="key">Stable fully-qualified mod save key.</param>
            /// <param name="defaultValue">Fallback value returned when the key is absent.</param>
            /// <returns>The stored payload text or <paramref name="defaultValue"/> when no payload exists.</returns>
            public static string GetModString(string key, string defaultValue = "")
            {
                return ModSaveStateStore.GetModString(key, defaultValue);
            }
        }

        /// <summary>
        /// Loader-facing diagnostics API for supported menus and tooling.
        /// </summary>
        public static class Mods
        {
            /// <summary>
            /// Copies the current discovered mod descriptors into the provided list.
            /// </summary>
            /// <param name="destination">Destination list that will be cleared and filled with current runtime info records.</param>
            public static void GetLoadedMods(List<ModRuntimeInfo> destination)
            {
                ModLoader.CollectRuntimeInfo(destination);
            }
        }
    }
}
