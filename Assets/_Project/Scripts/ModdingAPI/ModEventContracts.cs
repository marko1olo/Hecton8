using System;
using Unity.Mathematics;

namespace Hecton8.Modding
{
    /// <summary>
    /// Native first-party event streams exposed to mods as immutable byte payloads.
    /// </summary>
    public enum HectonNativeEventKind : byte
    {
        Interaction = 0,
        Crafting = 1
    }

    /// <summary>
    /// Read-only player spawn snapshot for mod event hooks.
    /// No mutable engine arrays or Unity object references are exposed.
    /// </summary>
    public readonly struct ModPlayerSpawnedEvent
    {
        public ModPlayerSpawnedEvent(ulong playerId, float3 absoluteUniversePosition, int biomeId)
        {
            PlayerId = playerId;
            AbsoluteUniversePosition = absoluteUniversePosition;
            BiomeId = biomeId;
        }

        public ulong PlayerId { get; }
        public float3 AbsoluteUniversePosition { get; }
        public int BiomeId { get; }
    }

    /// <summary>
    /// Read-only biome transition snapshot for mod event hooks.
    /// </summary>
    public readonly struct ModBiomeChangedEvent
    {
        public ModBiomeChangedEvent(int previousBiomeId, int currentBiomeId, float3 absoluteUniversePosition)
        {
            PreviousBiomeId = previousBiomeId;
            CurrentBiomeId = currentBiomeId;
            AbsoluteUniversePosition = absoluteUniversePosition;
        }

        public int PreviousBiomeId { get; }
        public int CurrentBiomeId { get; }
        public float3 AbsoluteUniversePosition { get; }
    }

    /// <summary>
    /// First-party bridge for safe mod event publication.
    /// </summary>
    public static class HectonModHooks
    {
        public static void PublishPlayerSpawned(in ModPlayerSpawnedEvent payload)
        {
            HectonEventBus.Publish(in payload);
        }

        public static void PublishBiomeChanged(in ModBiomeChangedEvent payload)
        {
            HectonEventBus.Publish(in payload);
        }
    }

    /// <summary>
    /// Managed mod callback for read-only native event payload copies.
    /// The span is valid only for the callback duration and cannot expose native container ownership.
    /// </summary>
    /// <param name="eventKind">Native event lane that produced the payload.</param>
    /// <param name="payload">Blittable payload bytes copied from the internal native queue.</param>
    public delegate void HectonNativeEventHandler(HectonNativeEventKind eventKind, ReadOnlySpan<byte> payload);

    /// <summary>
    /// Managed callback for mod-facing unmanaged payload events.
    /// Payloads are passed by readonly reference and cannot contain managed references.
    /// </summary>
    /// <typeparam name="TPayload">Unmanaged event payload type.</typeparam>
    /// <param name="payload">Blittable event payload.</param>
    public delegate void HectonUnmanagedEventHandler<TPayload>(in TPayload payload)
        where TPayload : unmanaged;
}
