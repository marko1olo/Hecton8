using System;
using Hecton.Localization;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.World
{
    [System.Serializable]
    public struct DepthZoneAmbience
    {
        [Tooltip("Water fog tint.")]
        public Color waterColor;

        [Tooltip("Fog density multiplier.")]
        [Range(0f, 1f)] public float fogDensity;

        [Tooltip("Biolum intensity multiplier.")]
        [Range(0f, 1f)] public float biolumIntensity;

        [Tooltip("Ambient audio multiplier.")]
        [Range(0f, 2f)] public float ambientVolumeMultiplier;

        [Tooltip("Nominal water temperature in Celsius.")]
        public float waterTemperature;
    }

    [CreateAssetMenu(fileName = "DepthZone_", menuName = "Hecton8/World/Depth Zone Profile", order = 40)]
    public sealed class DepthZoneProfile : ScriptableObject
    {
        [Header("── Identity ─────────────────────────────")]
        [SerializeField] public string zoneId;
        [SerializeField] public string displayName = "UNKNOWN ZONE";
        [SerializeField] private LocalizedTextReference localizedDisplayName;
        [SerializeField, TextArea(2, 4)] public string description;
        [SerializeField] private LocalizedTextReference localizedDescription;

        [Header("── Depth Range ─────────────────────────")]
        [SerializeField] public float minDepth;
        [SerializeField] public float maxDepth;

        [Header("── Ambience ────────────────────────────")]
        [SerializeField] public DepthZoneAmbience ambience = new DepthZoneAmbience
        {
            waterColor = new Color(0.05f, 0.15f, 0.25f, 1f),
            fogDensity = 0.3f,
            biolumIntensity = 0.1f,
            ambientVolumeMultiplier = 1f,
            waterTemperature = 15f,
        };

        [Header("── Gameplay ────────────────────────────")]
        [SerializeField, Range(0, 4)] public int requiredHullTier;
        [SerializeField, Range(0f, 1f)] public float dangerLevel;
        [SerializeField] public bool hasCaves;
        [SerializeField] public bool isThermal;

        [Header("── Discovery ───────────────────────────")]
        [SerializeField] public string discoveryId;

        [System.NonSerialized] private uint _cachedZoneHash;
        [System.NonSerialized] private uint _cachedDiscoveryHash;

        public string DisplayNameOrFallback => ResolveDisplayName(GlobalRegistry.LocalizationText);
        public string DescriptionOrFallback => ResolveDescription(GlobalRegistry.LocalizationText);
        public uint ZoneHash => _cachedZoneHash;
        public uint DiscoveryHash => _cachedDiscoveryHash;

        public string ResolveDisplayName(LocalizationManager manager)
        {
            return localizedDisplayName.ResolveOrFallback(manager, FallbackOrDefault(displayName, "UNKNOWN ZONE"));
        }

        public string ResolveDisplayName(ILocalizationTextReadModel manager)
        {
            return localizedDisplayName.ResolveOrFallback(manager, FallbackOrDefault(displayName, "UNKNOWN ZONE"));
        }

        public ReadOnlySpan<char> ResolveDisplayNameSpan(LocalizationManager manager)
        {
            return localizedDisplayName.ResolveSpanOrFallback(manager, FallbackOrDefault(displayName, "UNKNOWN ZONE"));
        }

        public ReadOnlySpan<char> ResolveDisplayNameSpan(ILocalizationTextReadModel manager)
        {
            return localizedDisplayName.ResolveSpanOrFallback(manager, FallbackOrDefault(displayName, "UNKNOWN ZONE"));
        }

        public string ResolveDescription(LocalizationManager manager)
        {
            return localizedDescription.ResolveOrFallback(manager, description);
        }

        public string ResolveDescription(ILocalizationTextReadModel manager)
        {
            return localizedDescription.ResolveOrFallback(manager, description);
        }

        public ReadOnlySpan<char> ResolveDescriptionSpan(LocalizationManager manager)
        {
            return localizedDescription.ResolveSpanOrFallback(manager, description);
        }

        public ReadOnlySpan<char> ResolveDescriptionSpan(ILocalizationTextReadModel manager)
        {
            return localizedDescription.ResolveSpanOrFallback(manager, description);
        }

        private void OnEnable()
        {
            RebuildCache();
        }

        public void RebuildCache()
        {
            _cachedZoneHash = string.IsNullOrWhiteSpace(zoneId)
                ? 0u
                : unchecked((uint)LocHash.Compute(zoneId));

            _cachedDiscoveryHash = string.IsNullOrWhiteSpace(discoveryId)
                ? 0u
                : NarrativeEvents.ComputeDiscoveryHash(discoveryId);
        }

        public bool ContainsDepth(float depth) => depth >= minDepth && depth < maxDepth;

        private static string FallbackOrDefault(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(zoneId))
                zoneId = name.ToLower().Replace(" ", "_");
            else
                zoneId = zoneId.Trim();

            if (string.IsNullOrWhiteSpace(discoveryId))
                discoveryId = zoneId;
            else
                discoveryId = discoveryId.Trim();

            if (maxDepth <= minDepth)
                maxDepth = minDepth + 100f;

            RebuildCache();
        }
#endif
    }
}
