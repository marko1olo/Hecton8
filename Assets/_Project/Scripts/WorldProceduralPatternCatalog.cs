using System.Collections.Generic;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldProceduralPatternCatalog", menuName = "Hecton8/World/Procedural Pattern Catalog")]
    public sealed class WorldProceduralPatternCatalog : ScriptableObject
    {
        [SerializeField] private WorldProceduralPatternProfile fallbackProfile;
        [SerializeField] private WorldProceduralPatternProfile[] profiles = new WorldProceduralPatternProfile[0];

        private Dictionary<WorldProceduralPattern, WorldProceduralPatternProfile> _lookup;

        public WorldProceduralPatternProfile FallbackProfile => fallbackProfile;
        public IReadOnlyList<WorldProceduralPatternProfile> Profiles => profiles;

        public bool HasConfiguredProfile(WorldProceduralPattern pattern)
        {
            EnsureLookup();
            return _lookup != null &&
                   _lookup.TryGetValue(pattern, out WorldProceduralPatternProfile profile) &&
                   profile != null;
        }

        public WorldProceduralPatternProfile GetProfile(WorldProceduralPattern pattern, out bool usedFallback)
        {
            EnsureLookup();
            if (_lookup != null && _lookup.TryGetValue(pattern, out WorldProceduralPatternProfile profile) && profile != null)
            {
                usedFallback = false;
                return profile;
            }

            usedFallback = true;
            return fallbackProfile;
        }

        private void OnEnable()
        {
            _lookup = null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
                return;

            int profileCount = profiles != null ? profiles.Length : 0;
            _lookup = new Dictionary<WorldProceduralPattern, WorldProceduralPatternProfile>(profileCount);
            if (profileCount <= 0)
                return;

            for (int i = 0; i < profileCount; i++)
            {
                WorldProceduralPatternProfile profile = profiles[i];
                if (profile == null)
                    continue;

                if (!_lookup.ContainsKey(profile.pattern))
                    _lookup.Add(profile.pattern, profile);
            }
        }
    }
}
