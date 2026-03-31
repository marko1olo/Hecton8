using System.Collections.Generic;
using Hecton8.Environment;
using UnityEngine;

namespace Hecton8.World
{
    [CreateAssetMenu(fileName = "WorldProceduralBiomeFamilyContextCatalog", menuName = "Hecton8/World/Procedural Biome Family Context Catalog")]
    public sealed class WorldProceduralBiomeFamilyContextCatalog : ScriptableObject
    {
        [SerializeField] private WorldProceduralBiomeFamilyContextProfile fallbackProfile;
        [SerializeField] private WorldProceduralBiomeFamilyContextProfile[] profiles = new WorldProceduralBiomeFamilyContextProfile[0];

        private Dictionary<string, WorldProceduralBiomeFamilyContextProfile> _lookup;

        public WorldProceduralBiomeFamilyContextProfile FallbackProfile => fallbackProfile;
        public IReadOnlyList<WorldProceduralBiomeFamilyContextProfile> Profiles => profiles;

        public bool HasConfiguredProfile(HectonBiomeFamilyProfile family)
        {
            if (family == null || string.IsNullOrWhiteSpace(family.familyId))
                return false;

            EnsureLookup();
            return _lookup != null && _lookup.ContainsKey(family.familyId) && _lookup[family.familyId] != null;
        }

        public WorldProceduralBiomeFamilyContextProfile GetProfile(HectonBiomeFamilyProfile family, out bool usedFallback)
        {
            EnsureLookup();
            if (family != null &&
                !string.IsNullOrWhiteSpace(family.familyId) &&
                _lookup != null &&
                _lookup.TryGetValue(family.familyId, out WorldProceduralBiomeFamilyContextProfile profile) &&
                profile != null)
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

            _lookup = new Dictionary<string, WorldProceduralBiomeFamilyContextProfile>();
            if (profiles == null)
                return;

            for (int i = 0; i < profiles.Length; i++)
            {
                WorldProceduralBiomeFamilyContextProfile profile = profiles[i];
                if (profile == null || profile.biomeFamily == null || string.IsNullOrWhiteSpace(profile.biomeFamily.familyId))
                    continue;

                if (!_lookup.ContainsKey(profile.biomeFamily.familyId))
                    _lookup.Add(profile.biomeFamily.familyId, profile);
            }
        }
    }
}
