using System;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Modding;
using Hecton8.Meta;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
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
        private const double TraitPositionBucketsPerMeter = 4d;

        [SerializeField] private int _worldSeed;
        private bool _serviceRegistered;
        private bool _duplicateServiceSuppressed;

        /// <summary>Persisted deterministic world seed used by ecosystem systems.</summary>
        public int WorldSeed => _worldSeed;

        /// <inheritdoc />
        public int SavePriority => 40;

        /// <inheritdoc />
        public int LoadPriority => 40;

        private void Awake()
        {
            FaunaGeneticsManager registered = GlobalRegistry.FaunaGenetics;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
                return;
            }

            if (_worldSeed == 0)
                _worldSeed = GenerateInitialSeed();
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            TryRegisterService();
            if (_duplicateServiceSuppressed)
                return;

            Hecton8.Core.GlobalRegistry.SaveRuntime?.Register(this);
        }

        private void OnDisable()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            Hecton8.Core.GlobalRegistry.SaveRuntime?.Unregister(this);
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            FaunaGeneticsManager registered = GlobalRegistry.FaunaGenetics;
            if (registered != null && registered != this)
            {
                SuppressDuplicateService();
                return;
            }

            GlobalRegistry.RegisterFaunaGeneticsRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.FaunaGenetics, this);
        }

        private void SuppressDuplicateService()
        {
            _duplicateServiceSuppressed = true;
            _serviceRegistered = false;
            enabled = false;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterFaunaGeneticsRuntime(this);
            _serviceRegistered = false;
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
            float scale = 0.85f + Hash01(variationHash ^ 0x68BC21EBu) * 0.30f;
            float sizeDelta = scale - 1f;
            float speed = Mathf.Clamp(1f - sizeDelta * 0.9f + HashSigned(variationHash ^ 0x02E5BE93u) * 0.04f, 0.82f, 1.22f);
            float health = Mathf.Clamp(1f + sizeDelta * 1.4f + HashSigned(variationHash ^ 0x7F4A7C15u) * 0.05f, 0.78f, 1.35f);

            ApplyMutationOverlays(archetype, biomeIndex, variationHash, ref scale, ref speed, ref health);

            FaunaGeneticTraits traits = default;
            traits.BaseScaleMultiplier = Mathf.Clamp(scale, 0.8f, 1.25f);
            traits.BaseSpeedMultiplier = Mathf.Clamp(speed, 0.75f, 1.35f);
            traits.BaseHealthMultiplier = Mathf.Clamp(health, 0.75f, 1.5f);
            traits.VariationHash = variationHash;
            traits.BaseGenome = FaunaGenome64.BuildGenome(variationHash, traits.BaseScaleMultiplier, traits.BaseSpeedMultiplier);
            return FaunaGenome64.ResolveRuntimeTraitsFromGenome(traits, traits.BaseGenome);
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

                float overlayT = Hash01(variationHash ^ (uint)(i + 1) * 0x9E3779B9u);
                float overlayScale = definition.MinScaleMultiplier +
                    (definition.MaxScaleMultiplier - definition.MinScaleMultiplier) * overlayT;

                scale *= overlayScale;
                speed *= definition.SpeedMultiplier;
                health *= definition.HealthMultiplier;
            }
        }

        private uint BuildVariationHash(CreatureArchetypeData archetype, int biomeIndex, Vector3 spawnPosition)
        {
            unchecked
            {
                bool hasAupProof = TryResolveAupFromRuntimeOrigin(spawnPosition, out Hecton8.World.AbsoluteUniversePosition spawnAup);
                uint hash = Mix((uint)_worldSeed);
                hash = Mix(hash ^ (uint)biomeIndex * 0x85EBCA6Bu);
                if (hasAupProof)
                {
                    hash = Mix(hash ^ FoldInt64(spawnAup.GridX));
                    hash = Mix(hash ^ FoldInt64(spawnAup.GridY));
                    hash = Mix(hash ^ FoldInt64(spawnAup.GridZ));
                    hash = Mix(hash ^ QuantizeAupLocal(spawnAup.LocalX));
                    hash = Mix(hash ^ QuantizeAupLocal(spawnAup.LocalY));
                    hash = Mix(hash ^ QuantizeAupLocal(spawnAup.LocalZ));
                }
                else
                {
                    float3 safeSpawn = new float3(spawnPosition.x, spawnPosition.y, spawnPosition.z);
                    hash = Mix(hash ^ math.asuint(math.select(0f, safeSpawn.x, math.isfinite(safeSpawn.x))));
                    hash = Mix(hash ^ math.asuint(math.select(0f, safeSpawn.y, math.isfinite(safeSpawn.y))));
                    hash = Mix(hash ^ math.asuint(math.select(0f, safeSpawn.z, math.isfinite(safeSpawn.z))));
                }

                hash = Mix(hash ^ HashString(archetype != null ? archetype.creatureId : string.Empty));
                return hash;
            }
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out Hecton8.World.AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            Hecton8.World.AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = Hecton8.World.AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return positionAup.IsFinite();
        }

        private static uint FoldInt64(long value)
        {
            unchecked
            {
                ulong bits = (ulong)value;
                return (uint)(bits ^ (bits >> 32));
            }
        }

        private static uint QuantizeAupLocal(double value)
        {
            double scaled = value * TraitPositionBucketsPerMeter;
            int rounded = scaled >= 0d ? (int)(scaled + 0.5d) : (int)(scaled - 0.5d);
            return unchecked((uint)rounded);
        }

        private int GenerateInitialSeed()
        {
            RunModifierController runModifierController = GlobalRegistry.RunModifiers;
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
