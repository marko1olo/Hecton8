using System;
using Hecton8.AI;
using Hecton8.Modding;
using Hecton8.Meta;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Generates deterministic per-instance fauna traits from spawn position, creature identity, biome, and persisted world seed.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6235)]
    [AddComponentMenu("Hecton8/Ecosystem/Fauna Genetics Manager")]
    public sealed class FaunaGeneticsManager : MonoBehaviour, ISaveable
    {
        private const int FallbackWorldSeed = unchecked((int)0x51ED270B);

        private static FaunaGeneticsManager _instance;

        [SerializeField] private int _worldSeed;

        /// <summary>Active runtime owner while the gameplay scene is loaded.</summary>
        public static FaunaGeneticsManager Instance => _instance;

        /// <summary>Persisted deterministic world seed used by ecosystem systems.</summary>
        public int WorldSeed => _worldSeed;

        /// <inheritdoc />
        public int SavePriority => 40;

        /// <inheritdoc />
        public int LoadPriority => 40;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (_worldSeed == 0)
                _worldSeed = GenerateInitialSeed();
        }

        private void OnEnable()
        {
            SaveManager.Instance?.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Instance?.Unregister(this);
        }

        private void OnDestroy()
        {
            SaveManager.Instance?.Unregister(this);
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Applies deterministic runtime traits to the specified fauna instance.
        /// </summary>
        public void ApplyTraits(FaunaBrain faunaBrain, CreatureArchetypeData archetype, int biomeIndex, Vector3 spawnPosition)
        {
            if (faunaBrain == null)
                return;

            faunaBrain.ApplyGeneticTraits(GenerateTraits(archetype, biomeIndex, spawnPosition));
        }

        /// <summary>
        /// Generates deterministic runtime traits for one fauna spawn request.
        /// </summary>
        public FaunaGeneticTraits GenerateTraits(CreatureArchetypeData archetype, int biomeIndex, Vector3 spawnPosition)
        {
            uint variationHash = BuildVariationHash(archetype, biomeIndex, spawnPosition);
            float scale = Mathf.Lerp(0.85f, 1.15f, Hash01(variationHash ^ 0x68BC21EBu));
            float sizeDelta = scale - 1f;
            float speed = Mathf.Clamp(1f - sizeDelta * 0.9f + HashSigned(variationHash ^ 0x02E5BE93u) * 0.04f, 0.82f, 1.22f);
            float health = Mathf.Clamp(1f + sizeDelta * 1.4f + HashSigned(variationHash ^ 0x7F4A7C15u) * 0.05f, 0.78f, 1.35f);

            ApplyMutationOverlays(archetype, biomeIndex, variationHash, ref scale, ref speed, ref health);

            FaunaGeneticTraits traits = default;
            traits.ScaleMultiplier = Mathf.Clamp(scale, 0.8f, 1.25f);
            traits.SpeedMultiplier = Mathf.Clamp(speed, 0.75f, 1.35f);
            traits.HealthMultiplier = Mathf.Clamp(health, 0.75f, 1.5f);
            traits.VariationHash = variationHash;
            return traits;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.ecosystemState.worldSeed = _worldSeed != 0 ? _worldSeed : GenerateInitialSeed();
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
            {
                if (_worldSeed == 0)
                    _worldSeed = GenerateInitialSeed();

                return;
            }

            int persistedSeed = data.ecosystemState.worldSeed;
            _worldSeed = persistedSeed != 0 ? persistedSeed : GenerateInitialSeed();
        }

        private void ApplyMutationOverlays(
            CreatureArchetypeData archetype,
            int biomeIndex,
            uint variationHash,
            ref float scale,
            ref float speed,
            ref float health)
        {
            string creatureId = archetype != null ? archetype.creatureId ?? string.Empty : string.Empty;
            for (int i = 0; i < ModEcosystemRegistry.Count; i++)
            {
                FaunaBiomeMutationDefinition definition = ModEcosystemRegistry.GetAt(i);
                if (definition == null || definition.BiomeId != biomeIndex)
                    continue;

                if (!string.IsNullOrEmpty(definition.SpeciesId) &&
                    !string.Equals(definition.SpeciesId, creatureId, StringComparison.Ordinal))
                {
                    continue;
                }

                float overlayScale = Mathf.Lerp(
                    definition.MinScaleMultiplier,
                    definition.MaxScaleMultiplier,
                    Hash01(variationHash ^ (uint)(i + 1) * 0x9E3779B9u));

                scale *= overlayScale;
                speed *= definition.SpeedMultiplier;
                health *= definition.HealthMultiplier;
            }
        }

        private uint BuildVariationHash(CreatureArchetypeData archetype, int biomeIndex, Vector3 spawnPosition)
        {
            unchecked
            {
                uint hash = Mix((uint)_worldSeed);
                hash = Mix(hash ^ (uint)biomeIndex * 0x85EBCA6Bu);
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(spawnPosition.x * 4f));
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(spawnPosition.y * 4f));
                hash = Mix(hash ^ (uint)Mathf.RoundToInt(spawnPosition.z * 4f));
                hash = Mix(hash ^ HashString(archetype != null ? archetype.creatureId : string.Empty));
                return hash;
            }
        }

        private int GenerateInitialSeed()
        {
            RunModifierController runModifierController = RunModifierController.Instance;
            if (runModifierController != null)
            {
                RunModifiersDTO modifiers = runModifierController.CurrentModifiers;
                if (modifiers.isDailySeed && !string.IsNullOrWhiteSpace(modifiers.dailySeedId))
                {
                    int dailySeed = unchecked((int)HashString(modifiers.dailySeedId));
                    return dailySeed != 0 ? dailySeed : FallbackWorldSeed;
                }
            }

            long ticks = DateTime.UtcNow.Ticks;
            int seed = unchecked((int)(ticks ^ (ticks >> 32)));
            return seed != 0 ? seed : FallbackWorldSeed;
        }

        private static uint HashString(string value)
        {
            unchecked
            {
                if (string.IsNullOrEmpty(value))
                    return 0x811C9DC5u;

                uint hash = 0x811C9DC5u;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint Mix(uint value)
        {
            unchecked
            {
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return value;
            }
        }

        private static float Hash01(uint value)
        {
            return (Mix(value) & 0x00FFFFFFu) * (1f / 16777215f);
        }

        private static float HashSigned(uint value)
        {
            return Hash01(value) * 2f - 1f;
        }
    }
}
