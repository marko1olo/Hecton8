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
        private static IInputService s_inputService;

        internal static void ResetRegistryCacheCold()
        {
            s_inputService = null;
            UI.ResetNotificationDiagnostics();
        }

        internal static void BindRegistryServicesCold()
        {
            s_inputService = GlobalRegistry.Input;
        }

        internal static void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Input)
                s_inputService = currentService as IInputService;
        }

        private static void ThrowIfNoActiveMod(string surface)
        {
            if (!ModExecutionScope.HasActiveMod)
                throw new IllegalContractException($"HectonAPI.{surface} calls must originate from an active mod execution scope.");
        }

        private static void ThrowIfScopeMismatch(string surface, string modId)
        {
            ThrowIfNoActiveMod(surface);
            if (!string.Equals(ModExecutionScope.CurrentModId, modId, StringComparison.Ordinal))
                throw new IllegalContractException($"HectonAPI.{surface} modId must match the active mod execution scope.");
        }

        private static void ThrowIfSignatureMismatch(string surface, uint modHash)
        {
            ThrowIfNoActiveMod(surface);
            if (ModExecutionScope.CurrentModHash != modHash)
                throw new IllegalContractException($"HectonAPI.{surface} ModderSignature must match the active mod execution scope.");
        }

        private static string RequireSubscriberScope(string surface, string subscriberId)
        {
            ThrowIfNoActiveMod(surface);
            if (!string.IsNullOrWhiteSpace(subscriberId) &&
                !string.Equals(ModExecutionScope.CurrentModId, subscriberId, StringComparison.Ordinal))
            {
                throw new IllegalContractException($"HectonAPI.{surface} subscriberId must match the active mod execution scope.");
            }

            return ModExecutionScope.CurrentModId;
        }

        private static void ThrowIfSubscriptionScopeMismatch(string surface, HectonEventSubscription subscription)
        {
            ThrowIfNoActiveMod(surface);
            if (subscription == null || string.IsNullOrWhiteSpace(subscription.SubscriberId))
                return;

            if (!string.Equals(ModExecutionScope.CurrentModId, subscription.SubscriberId, StringComparison.Ordinal))
                throw new IllegalContractException($"HectonAPI.{surface} subscription owner must match the active mod execution scope.");
        }

        private static void ThrowIfEngineOwnedPublishPayload<TPayload>(string surface)
            where TPayload : unmanaged
        {
            Type payloadType = typeof(TPayload);
            if (payloadType == typeof(ModEventDto) ||
                payloadType == typeof(ModPlayerSpawnedEvent) ||
                payloadType == typeof(ModBiomeChangedEvent) ||
                payloadType == typeof(ModRaycastResultPayload) ||
                payloadType == typeof(ModInteractionRejectedPayload) ||
                payloadType == typeof(ModCriticalMemoryEvictionPayload) ||
                payloadType == typeof(ModAupResponse) ||
                payloadType == typeof(FutureCommandEnvelope) ||
#pragma warning disable CS0618
                payloadType == typeof(ModCommand) ||
                payloadType == typeof(ModAupCommand) ||
                payloadType == typeof(ModRenderInstanceCommand)
#pragma warning restore CS0618
                )
            {
                throw new IllegalContractException($"HectonAPI.{surface} cannot publish engine-owned mod payload type {payloadType.Name}.");
            }
        }

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
                string resolvedSubscriberId = RequireSubscriberScope("Events.Subscribe", subscriberId);
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, resolvedSubscriberId);
            }

            /// <summary>
            /// Subscribes to immutable native queue payload bytes.
            /// </summary>
            /// <param name="handler">Native byte payload handler.</param>
            /// <param name="subscriberId">Optional stable diagnostic ID.</param>
            /// <returns>A disposable subscription token.</returns>
            public static HectonEventSubscription SubscribeNative(HectonNativeEventHandler handler, string subscriberId = null)
            {
                string resolvedSubscriberId = RequireSubscriberScope("Events.SubscribeNative", subscriberId);
                ThrowIfEnvelopeOnly();
                return HectonEventBus.SubscribeNative(handler, resolvedSubscriberId);
            }

            /// <summary>
            /// Subscribes to sampled public native signals projected as player-relative mod DTOs.
            /// </summary>
            /// <param name="handler">Projected event callback.</param>
            /// <param name="subscriberId">Optional stable diagnostic ID.</param>
            /// <returns>A disposable subscription token.</returns>
            public static HectonEventSubscription SubscribeProjected(Action<ModEventDto> handler, string subscriberId = null)
            {
                string resolvedSubscriberId = RequireSubscriberScope("Events.SubscribeProjected", subscriberId);
                ThrowIfEnvelopeOnly();
                return HectonEventBus.SubscribeProjected(handler, resolvedSubscriberId);
            }

            public static HectonEventSubscription OnPlayerSpawned(
                HectonUnmanagedEventHandler<ModPlayerSpawnedEvent> handler,
                string subscriberId = null)
            {
                string resolvedSubscriberId = RequireSubscriberScope("Events.OnPlayerSpawned", subscriberId);
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, resolvedSubscriberId);
            }

            public static HectonEventSubscription OnBiomeChanged(
                HectonUnmanagedEventHandler<ModBiomeChangedEvent> handler,
                string subscriberId = null)
            {
                string resolvedSubscriberId = RequireSubscriberScope("Events.OnBiomeChanged", subscriberId);
                ThrowIfEnvelopeOnly();
                return HectonEventBus.Subscribe(handler, resolvedSubscriberId);
            }

            /// <summary>
            /// Removes a previously created event subscription.
            /// This is a convenience wrapper around <see cref="IDisposable.Dispose"/>.
            /// </summary>
            /// <param name="subscription">Subscription token returned by <see cref="Subscribe{TEvent}"/>.</param>
            public static void Unsubscribe(HectonEventSubscription subscription)
            {
                ThrowIfSubscriptionScopeMismatch("Events.Unsubscribe", subscription);
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
                ThrowIfNoActiveMod("Events.Publish");
                ThrowIfEnvelopeOnly();
                ThrowIfEngineOwnedPublishPayload<TPayload>("Events.Publish");
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
                ThrowIfNoActiveMod("Input.GetButtonMask");
                IInputService input = s_inputService;
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
                ThrowIfSignatureMismatch("Commands.RequestFuture", envelope.ModderSignature);
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
                ThrowIfNoActiveMod("Commands.Request");
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
                ThrowIfNoActiveMod("Commands.RequestAup");
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
                ThrowIfNoActiveMod("Commands.RequestRenderInstance");
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
            public static IModResourceProxy Proxy => GetProxy();

            private static IModResourceProxy GetProxy()
            {
                ThrowIfNoActiveMod("Resources.Proxy");
                return ModResourceProxy.Instance;
            }

            /// <summary>
            /// Resolves a prefab asset name to a hash identifier.
            /// </summary>
            public static bool TryResolvePrefab(string assetName, out uint hashId)
            {
                ThrowIfNoActiveMod("Resources.TryResolvePrefab");
                return ModResourceProxy.Instance.TryResolvePrefab(assetName, out hashId);
            }

            /// <summary>
            /// Resolves an audio clip asset name to a hash identifier.
            /// </summary>
            public static bool TryResolveAudioClip(string assetName, out uint hashId)
            {
                ThrowIfNoActiveMod("Resources.TryResolveAudioClip");
                return ModResourceProxy.Instance.TryResolveAudioClip(assetName, out hashId);
            }

            /// <summary>
            /// Resolves a texture asset name to a hash identifier.
            /// </summary>
            public static bool TryResolveTexture(string assetName, out uint hashId)
            {
                ThrowIfNoActiveMod("Resources.TryResolveTexture");
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
                ThrowIfNoActiveMod("Telemetry.Publish");
                GlobalTelemetryBus.PublishModTelemetry(ModExecutionScope.CurrentModHash, markerHash, scalarValue);
            }
        }

        /// <summary>
        /// Item-facing mod API. ScriptableObject item handles are not public mod API.
        /// </summary>
        public static class Items
        {
            /// <summary>
            /// Internal guard for the forbidden ScriptableObject item path.
            /// </summary>
            /// <param name="data">Authored item asset to expose to runtime systems and save/load lookups.</param>
            /// <returns>
            /// Always throws for mod callers.
            /// </returns>
            internal static bool RegisterCustomItem(ItemData data)
            {
                _ = data;
                throw new IllegalContractException("Mods cannot pass ScriptableObject ItemData handles through HectonAPI. Use approved hash/CRC content envelopes.");
            }

            /// <summary>
            /// Internal guard for the forbidden ScriptableObject item lookup path.
            /// </summary>
            /// <param name="persistentId">Stable item identifier used by saves and catalogs.</param>
            /// <param name="itemData">Resolved item asset when found.</param>
            /// <returns>Never returns for mod callers.</returns>
            internal static bool TryFindItem(string persistentId, out ItemData itemData)
            {
                _ = persistentId;
                itemData = null;
                throw new IllegalContractException("Mods cannot receive ScriptableObject ItemData handles from HectonAPI. Use hash-only resource/content identifiers.");
            }
        }

        /// <summary>
        /// Crafting-facing mod API. Direct recipe/recycle owner overrides are not public mod API.
        /// </summary>
        public static class Crafting
        {
            /// <summary>
            /// Internal guard for the forbidden ScriptableObject recipe path.
            /// </summary>
            /// <param name="recipe">Authored recipe asset to expose through the live crafting registry.</param>
            /// <returns>
            /// Always throws for mod callers.
            /// </returns>
            internal static bool RegisterRecipe(RecipeData recipe)
            {
                _ = recipe;
                throw new IllegalContractException("Mods cannot pass ScriptableObject RecipeData handles through HectonAPI. Use approved hash/CRC content envelopes.");
            }

            /// <summary>
            /// Internal guard for the forbidden recycle-yield owner override path.
            /// </summary>
            /// <param name="itemId">Stable source item identifier used by the runtime item catalog.</param>
            /// <param name="yield">Resource stacks granted when one unit of the source item is recycled.</param>
            /// <returns>
            /// Always throws for mod callers.
            /// </returns>
            internal static bool RegisterRecycleYield(string itemId, List<ResourceStack> yield)
            {
                _ = itemId;
                _ = yield;
                throw new IllegalContractException("Mods cannot override recycle yields through HectonAPI. Use an approved content manifest or FutureCommandEnvelope owner route.");
            }
        }

        /// <summary>
        /// Recycling-facing API. Direct inventory mutation is not public mod API.
        /// </summary>
        public static class Recycling
        {
            /// <summary>
            /// Internal guard for the forbidden direct recycling owner path.
            /// </summary>
            /// <param name="itemId">Stable item identifier stored in the active runtime item catalog.</param>
            /// <returns>Never returns for mod callers.</returns>
            internal static bool ProcessRecycle(string itemId)
            {
                _ = itemId;
                throw new IllegalContractException("Mods cannot mutate inventory through HectonAPI.Recycling. Submit a validated FutureCommandEnvelope through HectonAPI.Commands.RequestFuture.");
            }
        }

        /// <summary>
        /// Construction-facing mod API. ScriptableObject buildable handles are not public mod API.
        /// </summary>
        public static class Construction
        {
            /// <summary>
            /// Internal guard for the forbidden ScriptableObject buildable path.
            /// </summary>
            /// <param name="data">Buildable module definition to expose at runtime.</param>
            /// <param name="customCategory">
            /// Optional runtime-only category label for future Mods-facing build browsers.
            /// This metadata does not overwrite the authored <see cref="BuildableData.family"/> field.
            /// </param>
            /// <returns>
            /// Always throws for mod callers.
            /// </returns>
            internal static bool RegisterBuildable(BuildableData data, string customCategory = "Mods")
            {
                _ = data;
                _ = customCategory;
                throw new IllegalContractException("Mods cannot pass ScriptableObject BuildableData handles through HectonAPI. Use approved hash/CRC content envelopes.");
            }

            /// <summary>
            /// Internal guard for the forbidden ScriptableObject buildable lookup path.
            /// </summary>
            /// <param name="persistentId">Stable buildable identifier used by saves and catalogs.</param>
            /// <param name="buildableData">Resolved buildable asset when found.</param>
            /// <returns>Never returns for mod callers.</returns>
            internal static bool TryFindBuildable(string persistentId, out BuildableData buildableData)
            {
                _ = persistentId;
                buildableData = null;
                throw new IllegalContractException("Mods cannot receive ScriptableObject BuildableData handles from HectonAPI. Use hash-only resource/content identifiers.");
            }
        }

        /// <summary>
        /// Ecosystem-facing mod API for deterministic fauna mutation overlays.
        /// </summary>
        public static class Ecosystem
        {
            /// <summary>
            /// Internal guard for the forbidden direct ecosystem mutation overlay path.
            /// </summary>
            /// <param name="definition">Biome mutation definition to merge into the live runtime overlay registry.</param>
            /// <returns>
            /// Always throws for mod callers.
            /// </returns>
            internal static bool RegisterBiomeMutation(FaunaBiomeMutationDefinition definition)
            {
                _ = definition;
                throw new IllegalContractException("Mods cannot register ecosystem mutation overlays through HectonAPI without owner revocation and runtime proof.");
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
                ThrowIfNoActiveMod("Localization.InjectBabelEnvelope");
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
            private static readonly uint NotificationMissWarningHash = unchecked((uint)LocHash.Compute("HectonAPI.UI.NotificationMiss"));
            private static readonly uint NotificationContextHash = unchecked((uint)LocHash.Compute("HectonAPI.UI.Notification"));
            private static int s_notificationMissCount;

            public static int NotificationMissCount => s_notificationMissCount;

            /// <summary>
            /// Shows an informational HUD message through the live notification owner.
            /// If the HUD instance is not active yet, the message is routed through the notification event bus.
            /// </summary>
            /// <param name="message">User-facing message body.</param>
            public static void ShowInfo(string message)
            {
                ThrowIfNoActiveMod("UI.ShowInfo");
                ReadOnlySpan<char> messageSpan = string.IsNullOrEmpty(message) ? ReadOnlySpan<char>.Empty : message.AsSpan();
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowInfo(messageSpan);
                    return;
                }

                TryPushNotification(messageSpan, severity: 0);
            }

            /// <summary>
            /// Shows a warning HUD message through the live notification owner.
            /// </summary>
            /// <param name="message">User-facing warning body.</param>
            public static void ShowWarning(string message)
            {
                ThrowIfNoActiveMod("UI.ShowWarning");
                ReadOnlySpan<char> messageSpan = string.IsNullOrEmpty(message) ? ReadOnlySpan<char>.Empty : message.AsSpan();
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowWarning(messageSpan);
                    return;
                }

                TryPushNotification(messageSpan, severity: 1);
            }

            /// <summary>
            /// Shows a critical HUD message through the live notification owner.
            /// </summary>
            /// <param name="message">User-facing critical body.</param>
            public static void ShowCritical(string message)
            {
                ThrowIfNoActiveMod("UI.ShowCritical");
                ReadOnlySpan<char> messageSpan = string.IsNullOrEmpty(message) ? ReadOnlySpan<char>.Empty : message.AsSpan();
                if (HUDNotification.TryGetActive(out HUDNotification notification))
                {
                    notification.ShowCritical(messageSpan);
                    return;
                }

                TryPushNotification(messageSpan, severity: 2);
            }

            private static void TryPushNotification(ReadOnlySpan<char> message, byte severity)
            {
                bool pushed = severity switch
                {
                    2 => NotificationEvents.TryPushCritical(message),
                    1 => NotificationEvents.TryPushWarning(message),
                    _ => NotificationEvents.TryPushInfo(message)
                };

                if (pushed)
                    return;

                ReportNotificationMiss(severity);
            }

            private static void ReportNotificationMiss(byte severity)
            {
                s_notificationMissCount++;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    NotificationMissWarningHash,
                    NotificationContextHash ^ ModExecutionScope.CurrentModHash ^ unchecked((uint)severity),
                    Mathf.Max(1, s_notificationMissCount));
            }

            internal static void ResetNotificationDiagnostics()
            {
                s_notificationMissCount = 0;
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
                ThrowIfScopeMismatch("UI.RegisterSetting", modId);
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
                ThrowIfScopeMismatch("UI.RegisterSetting", modId);
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
            public static bool IsGameReady => GetIsGameReady();

            private static bool GetIsGameReady()
            {
                ThrowIfNoActiveMod("World.IsGameReady");
                return GameBootstrapper.IsGameReady;
            }

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
                ThrowIfNoActiveMod("World.TryGetPlayerEntityHash");
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
                ThrowIfNoActiveMod("SaveState.SetModString");
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
                ThrowIfNoActiveMod("SaveState.GetModString");
                return ModSaveStateStore.GetModString(key, defaultValue);
            }
        }

        /// <summary>
        /// Loader-facing diagnostics API for supported menus and tooling.
        /// </summary>
        internal static class Mods
        {
            /// <summary>
            /// Copies the current discovered mod descriptors into the provided list.
            /// </summary>
            /// <param name="destination">Destination list that will be cleared and filled with current runtime info records.</param>
            internal static void GetLoadedMods(List<ModRuntimeInfo> destination)
            {
                ModLoader.CollectRuntimeInfo(destination);
            }
        }
    }
}
