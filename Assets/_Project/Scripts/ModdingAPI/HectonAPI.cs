using System;
using System.Collections.Generic;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Building;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Crafting;
using Hecton8.Economy;
using Hecton8.Ecosystem;
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
            private const string EnvelopeOnlyEventApiDisabledMessage =
                "HectonAPI.Events is disabled in FutureCommandEnvelope-only mode. Submit 64-byte FutureCommandEnvelope packets through HectonAPI.Commands.RequestFuture.";

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
            internal static HectonEventSubscription Subscribe<TEvent>(Action<TEvent> handler, string subscriberId = null)
                where TEvent : HectonEvent
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, subscriberId);
            }

            /// <summary>
            /// Subscribes to a mod-facing unmanaged payload stream.
            /// </summary>
            /// <typeparam name="TPayload">Unmanaged payload type.</typeparam>
            /// <param name="handler">Payload handler.</param>
            /// <param name="subscriberId">Optional stable diagnostic ID.</param>
            /// <returns>A disposable subscription token.</returns>
            public static HectonEventSubscription Subscribe<TPayload>(
                HectonUnmanagedEventHandler<TPayload> handler,
                string subscriberId = null)
                where TPayload : unmanaged
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, subscriberId);
            }

            /// <summary>
            /// Subscribes to immutable native queue payload bytes.
            /// </summary>
            /// <param name="handler">Native byte payload handler.</param>
            /// <param name="subscriberId">Optional stable diagnostic ID.</param>
            /// <returns>A disposable subscription token.</returns>
            public static HectonEventSubscription SubscribeNative(HectonNativeEventHandler handler, string subscriberId = null)
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.SubscribeNative(handler, subscriberId);
            }

            /// <summary>
            /// Subscribes to sampled public native signals projected as player-relative mod DTOs.
            /// </summary>
            /// <param name="handler">Projected event callback.</param>
            /// <param name="subscriberId">Optional stable diagnostic ID.</param>
            /// <returns>A disposable subscription token.</returns>
            public static HectonEventSubscription SubscribeProjected(Action<ModEventDto> handler, string subscriberId = null)
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.SubscribeProjected(handler, subscriberId);
            }

            public static HectonEventSubscription OnPlayerSpawned(
                HectonUnmanagedEventHandler<ModPlayerSpawnedEvent> handler,
                string subscriberId = null)
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, subscriberId);
            }

            public static HectonEventSubscription OnBiomeChanged(
                HectonUnmanagedEventHandler<ModBiomeChangedEvent> handler,
                string subscriberId = null)
            {
                ThrowIfEnvelopeOnly();
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
            internal static TEvent Publish<TEvent>(TEvent evt)
                where TEvent : HectonEvent
            {
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Publish(evt);
            }

            /// <summary>
            /// Publishes a mod-facing unmanaged payload event.
            /// </summary>
            /// <typeparam name="TPayload">Unmanaged payload type.</typeparam>
            /// <param name="payload">Blittable payload.</param>
            public static void Publish<TPayload>(in TPayload payload)
                where TPayload : unmanaged
            {
                ThrowIfEnvelopeOnly();
                HectonEventBus.Publish(in payload);
            }

            private static void ThrowIfEnvelopeOnly()
            {
                if (ModLoader.GetIsFutureCommandEnvelopeOnly())
                    throw new IllegalContractException(EnvelopeOnlyEventApiDisabledMessage);
            }
        }

        /// <summary>
        /// Input-facing mod API. Mods receive button masks only, never Input System assets or action references.
        /// </summary>
        public static class Input
        {
            /// <summary>
            /// Returns the current frame-cached gameplay button mask.
            /// </summary>
            public static uint GetButtonMask()
            {
                IInputService input = GlobalRegistry.Input;
                if (input == null)
                    return 0u;

                return input.GetState().ActionsBitmask;
            }

            /// <summary>
            /// Returns true when all requested button bits are present in the current frame mask.
            /// </summary>
            /// <param name="requiredMask">Bit mask built from <see cref="PlayerInputAction"/> values.</param>
            public static bool HasButtonMask(uint requiredMask)
            {
                if (requiredMask == 0u)
                    return false;

                uint currentMask = GetButtonMask();
                return (currentMask & requiredMask) == requiredMask;
            }
        }

        /// <summary>
        /// Command-facing mod API. UGC commands must be fixed 64-byte future envelopes.
        /// </summary>
        public static class Commands
        {
            /// <summary>
            /// Enqueues one binary-only Future Command envelope for Burst validation.
            /// </summary>
            /// <param name="envelope">Fixed 64-byte command envelope.</param>
            /// <returns>True when the envelope entered the quarantine queue.</returns>
            public static bool RequestFuture(in FutureCommandEnvelope envelope)
            {
                return FutureCommandSandboxValidator.Request(in envelope);
            }

#pragma warning disable CS0618
            /// <summary>
            /// Legacy command lane is quarantined. Use <see cref="RequestFuture"/> with a 64-byte envelope.
            /// </summary>
            /// <param name="command">Command packet. Mod identity is assigned by the engine.</param>
            /// <returns>Always false while envelope-only UGC is enforced.</returns>
            [System.Obsolete("Legacy ModCommand lane is quarantined and returns false. Use RequestFuture with a 64-byte FutureCommandEnvelope.", false)]
            public static bool Request(in ModCommand command)
            {
                return false;
            }

            /// <summary>
            /// Legacy AUP command lane is quarantined. Use <see cref="RequestFuture"/> with a 64-byte envelope.
            /// </summary>
            /// <param name="command">AUP-backed command packet.</param>
            /// <returns>Always false while envelope-only UGC is enforced.</returns>
            [System.Obsolete("Legacy AUP command lane is quarantined and returns false. Use RequestFuture with a 64-byte FutureCommandEnvelope.", false)]
            public static bool RequestAup(in ModAupCommand command)
            {
                return false;
            }

            /// <summary>
            /// Legacy render-instance lane is quarantined. Use <see cref="RequestFuture"/> with a 64-byte envelope.
            /// </summary>
            /// <param name="command">Render instance packet.</param>
            /// <returns>Always false while envelope-only UGC is enforced.</returns>
            [System.Obsolete("Legacy render-instance lane is quarantined and returns false. Use RequestFuture with a 64-byte FutureCommandEnvelope.", false)]
            public static bool RequestRenderInstance(in ModRenderInstanceCommand command)
            {
                return false;
            }
#pragma warning restore CS0618
        }

        /// <summary>
        /// Resource-facing mod API. Mods receive hash identifiers only.
        /// </summary>
        public static class Resources
        {
            /// <summary>
            /// Current mod resource proxy.
            /// </summary>
            public static IModResourceProxy Proxy => ModResourceProxy.Instance;

            /// <summary>
            /// Resolves a prefab asset name to a hash identifier.
            /// </summary>
            public static bool TryResolvePrefab(string assetName, out uint hashId)
            {
                return ModResourceProxy.Instance.TryResolvePrefab(assetName, out hashId);
            }

            /// <summary>
            /// Resolves an audio clip asset name to a hash identifier.
            /// </summary>
            public static bool TryResolveAudioClip(string assetName, out uint hashId)
            {
                return ModResourceProxy.Instance.TryResolveAudioClip(assetName, out hashId);
            }

            /// <summary>
            /// Resolves a texture asset name to a hash identifier.
            /// </summary>
            public static bool TryResolveTexture(string assetName, out uint hashId)
            {
                return ModResourceProxy.Instance.TryResolveTexture(assetName, out hashId);
            }
        }

        /// <summary>
        /// Telemetry-facing mod API. Payloads are pre-hashed and written to the global ring buffer.
        /// </summary>
        public static class Telemetry
        {
            /// <summary>
            /// Writes a mod telemetry marker.
            /// </summary>
            /// <param name="markerHash">Stable marker hash.</param>
            /// <param name="scalarValue">Optional scalar payload.</param>
            public static void Publish(uint markerHash, float scalarValue)
            {
                if (!ModExecutionScope.HasActiveMod)
                    throw new IllegalContractException("Mod telemetry writes must originate from an active mod execution scope.");

                GlobalTelemetryBus.PublishModTelemetry(ModExecutionScope.CurrentModHash, markerHash, scalarValue);
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
                    Hecton8.Core.H8Debug.LogWarning(
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
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[HectonAPI.Crafting] Failed to register custom recipe '{(recipe != null ? recipe.name : "null")}': {error}");
                }

                return success;
            }

            /// <summary>
            /// Registers a runtime recycle-yield overlay for the specified source item without mutating authored item or recipe assets.
            /// Explicit recycle yields override the built-in auto-derivation path used by the official recycling owner.
            /// </summary>
            /// <param name="itemId">Stable source item identifier used by the runtime item catalog.</param>
            /// <param name="yield">Resource stacks granted when one unit of the source item is recycled.</param>
            /// <returns>
            /// True when the recycle-yield overlay was accepted by the runtime registry.
            /// False when the source ID or yield payload is invalid.
            /// </returns>
            public static bool RegisterRecycleYield(string itemId, List<ResourceStack> yield)
            {
                bool success = ModRecycleRegistry.TryRegister(itemId, yield, out string error);
                if (!success)
                {
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[HectonAPI.Crafting] Failed to register recycle yield for '{itemId ?? "null"}': {error}");
                }

                return success;
            }
        }

        /// <summary>
        /// Recycling-facing API for the official dismantling owner.
        /// </summary>
        public static class Recycling
        {
            /// <summary>
            /// Attempts to recycle one inventory unit of the specified item through the official recycling owner.
            /// Recycle yields come from registered runtime overlays first and fall back to auto-derived dismantle results.
            /// </summary>
            /// <param name="itemId">Stable item identifier stored in the active runtime item catalog.</param>
            /// <returns>True when one unit was removed from inventory and the recycle outputs were granted successfully.</returns>
            public static bool ProcessRecycle(string itemId)
            {
                ScrapManager manager = GlobalRegistry.Scrap;
                return manager != null && manager.ProcessRecycle(itemId);
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
                    Hecton8.Core.H8Debug.LogWarning(
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
        /// Ecosystem-facing mod API for deterministic fauna mutation overlays.
        /// </summary>
        public static class Ecosystem
        {
            /// <summary>
            /// Registers a biome-scoped mutation overlay that biases runtime fauna genetics without mutating authored creature assets.
            /// Matching is deterministic and evaluated by the live ecosystem genetics owner during spawn.
            /// </summary>
            /// <param name="definition">Biome mutation definition to merge into the live runtime overlay registry.</param>
            /// <returns>
            /// True when the mutation definition was accepted by the runtime registry.
            /// False when the payload is invalid.
            /// </returns>
            public static bool RegisterBiomeMutation(FaunaBiomeMutationDefinition definition)
            {
                bool success = ModEcosystemRegistry.TryRegister(definition, out string error);
                if (!success)
                {
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[HectonAPI.Ecosystem] Failed to register biome mutation for biome '{(definition != null ? definition.BiomeId : 0)}': {error}");
                }

                return success;
            }
        }

        /// <summary>
        /// Asset-facing legacy API. Direct object loading is quarantined in envelope-only UGC mode.
        /// </summary>
        public static class Assets
        {
            /// <summary>
            /// Direct prefab references are forbidden; use a CRC-approved asset hash in a FutureCommandEnvelope.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name or mod-relative prefab asset path.</param>
            /// <returns>The loaded prefab asset, or null when no matching asset exists.</returns>
            internal static GameObject LoadPrefab(string modId, string assetName)
            {
                throw new IllegalContractException("Mods cannot receive Unity prefab references. Resolve a resource hash and submit a FutureCommandEnvelope through HectonAPI.Commands.RequestFuture.");
            }

            /// <summary>
            /// Direct audio clip references are forbidden; use a CRC-approved asset hash in a FutureCommandEnvelope.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name for the target clip.</param>
            /// <returns>The loaded audio clip, or null when no matching clip exists.</returns>
            internal static AudioClip LoadAudioClip(string modId, string assetName)
            {
                throw new IllegalContractException("Mods cannot receive Unity audio clip references. Use HectonAPI.Resources.TryResolveAudioClip.");
            }

            /// <summary>
            /// Direct texture references are forbidden; use a CRC-approved asset hash in a FutureCommandEnvelope.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">AssetBundle asset name or mod-relative PNG path.</param>
            /// <returns>The loaded texture, or null when no supported source resolves successfully.</returns>
            internal static Texture2D LoadTexture(string modId, string assetName)
            {
                throw new IllegalContractException("Mods cannot receive Unity texture references. Use HectonAPI.Resources.TryResolveTexture.");
            }
        }

        /// <summary>
        /// Localization-facing mod API for binary translation envelopes.
        /// </summary>
        public static class Localization
        {
            /// <summary>
            /// Rejects runtime localization injection until modded Babel binary envelopes are supported.
            /// </summary>
            /// <param name="language">Target language table to extend.</param>
            /// <param name="babelEnvelope">Future binary/hash localization envelope bytes.</param>
            public static void InjectBabelEnvelope(
                GameLanguage language,
                ReadOnlySpan<byte> babelEnvelope)
            {
                _ = language;
                _ = babelEnvelope;
                throw new IllegalContractException("Mod localization injection is disabled until Babel binary/hash envelopes are supported.");
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

                NotificationEvents.TryPushInfo(message ?? string.Empty);
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

                NotificationEvents.TryPushWarning(message ?? string.Empty);
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

                NotificationEvents.TryPushCritical(message ?? string.Empty);
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
            public static bool IsGameReady => GameBootstrapper.IsGameReady;

            /// <summary>
            /// Resolves the live player GameObject published by bootstrap.
            /// </summary>
            /// <param name="playerObject">Current bootstrap player object when available.</param>
            /// <returns>True when a player object is currently published by the official bootstrap pipeline.</returns>
            internal static bool TryGetPlayerObject(out GameObject playerObject)
            {
                playerObject = null;
                throw new IllegalContractException("Mods cannot receive Unity GameObject references. Use TryGetPlayerEntityHash.");
            }

            /// <summary>
            /// Resolves the live player transform published by bootstrap.
            /// </summary>
            /// <param name="playerTransform">Current bootstrap player transform when available.</param>
            /// <returns>True when a player transform is currently published by the official bootstrap pipeline.</returns>
            internal static bool TryGetPlayerTransform(out Transform playerTransform)
            {
                playerTransform = null;
                throw new IllegalContractException("Mods cannot receive Unity Transform references. Use TryGetPlayerEntityHash.");
            }

            /// <summary>
            /// Resolves the live player entity hash without exposing Unity object references.
            /// </summary>
            /// <param name="playerHash">Current player entity hash.</param>
            /// <returns>True when a player object is currently published.</returns>
            public static bool TryGetPlayerEntityHash(out uint playerHash)
            {
                GameObject playerObject = GameBootstrapper.CurrentPlayerObject;
                playerHash = playerObject != null
                    ? unchecked((uint)EntityId.ToULong(playerObject.GetEntityId()))
                    : 0u;
                return playerHash != 0u;
            }

            /// <summary>
            /// Direct persistent prefab spawning is forbidden; use a validated FutureCommandEnvelope request.
            /// </summary>
            /// <param name="modId">Stable owner mod identifier.</param>
            /// <param name="assetName">Legacy prefab asset name.</param>
            /// <param name="position">World position for the spawned instance.</param>
            /// <param name="rotation">World rotation for the spawned instance.</param>
            /// <returns>The spawned instance, or null when the prefab or world owners are unavailable.</returns>
            internal static GameObject SpawnPersistentPrefab(string modId, string assetName, Vector3 position, Quaternion rotation)
            {
                throw new IllegalContractException("Mods cannot spawn Unity prefabs directly. Resolve a resource hash and submit a FutureCommandEnvelope through HectonAPI.Commands.RequestFuture.");
            }

            /// <summary>
            /// Removes a previously spawned persistent mod instance from the save registry and returns it to the supported pool owner.
            /// </summary>
            /// <param name="instance">Live instance created by <see cref="SpawnPersistentPrefab"/>.</param>
            /// <returns>True when the instance was recognized and removed successfully.</returns>
            internal static bool DespawnPersistentInstance(GameObject instance)
            {
                throw new IllegalContractException("Mods cannot despawn Unity instances directly. Submit a validated FutureCommandEnvelope through HectonAPI.Commands.RequestFuture.");
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
                if (!ModExecutionScope.HasActiveMod)
                    throw new IllegalContractException("Mod save writes must originate from an active mod execution scope.");

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
                if (!ModExecutionScope.HasActiveMod)
                    throw new IllegalContractException("Mod save reads must originate from an active mod execution scope.");

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
