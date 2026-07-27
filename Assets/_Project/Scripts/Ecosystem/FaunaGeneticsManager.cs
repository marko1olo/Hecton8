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
    public sealed class FaunaGeneticsManager : MonoBehaviour, ISaveable, IFaunaWorldSeedReadModel, IWorldSeedProvider, IGlobalRegistryHotSwapListener
    {
        private const int FallbackWorldSeed = unchecked((int)0x51ED270B);

        // This class does not generate the world, so it cannot honestly claim a world-generation
        // algorithm version. SaveManager.ValidateRuntimeWorldSeed only compares versions when the
        // SAVED one is > 0, so 0 means "unversioned" and stays quiet - while a save written by a
        // real HectonWorldGenerator still warns, which is the correct thing for it to do.
        private const int UnversionedWorldGeneration = 0;

        [SerializeField] private int _worldSeed;
        private bool _serviceRegistered;
        private bool _worldSeedProviderRegistered;
        private bool _hotSwapRegistered;
        private bool _duplicateServiceSuppressed;
        private bool _saveRegistered;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private RunModifierController _runModifiers;

        /// <summary>Persisted deterministic world seed used by ecosystem systems.</summary>
        public int WorldSeed => _worldSeed;

        // IWorldSeedProvider. This class was already the de-facto owner of the world seed - it
        // generates it, persists it to ecosystemState.worldSeed and restores it on load - but it
        // only ever published it as IFaunaWorldSeedReadModel, so fauna genetics was the single
        // system in the project that could see it. Nothing implemented IWorldSeedProvider, which is
        // what the procedural-generation side asks for, so the world seed reaching MapMagic and
        // every other generator was 0. Publishing the same field under the interface that the rest
        // of the project actually queries is what connects the two halves.
        //
        // Yields to a real HectonWorldGenerator if one is ever present: see
        // TryRegisterWorldSeedProvider.

        /// <inheritdoc />
        public bool IsInitialized => _worldSeedProviderRegistered && _worldSeed != 0;

        /// <inheritdoc />
        public int RuntimeWorldSeed => _worldSeed;

        /// <inheritdoc />
        public int RuntimeWorldGenerationVersionId => UnversionedWorldGeneration;

        /// <inheritdoc />
        public int SavePriority => 40;

        /// <inheritdoc />
        public int LoadPriority => 40;

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            CacheRunModifiersCold();
            if (_worldSeed == 0)
                _worldSeed = GenerateInitialSeed();
        }

        private void OnEnable()
        {
            if (_duplicateServiceSuppressed)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            TryRegisterService();
            if (_duplicateServiceSuppressed)
                return;

            CacheRunModifiersCold();
            CacheSaveServiceCold();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterWorldSeedProvider();
        }

        private void OnDisable()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            _saveService = null;
            _runModifiers = null;
            TryUnregisterWorldSeedProvider();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterSaveParticipant();
            TryUnregisterHotSwapListener();
            _saveService = null;
            _runModifiers = null;
            TryUnregisterWorldSeedProvider();
            TryUnregisterService();
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            GlobalRegistry.RegisterFaunaGeneticsRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.FaunaGenetics, this);
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            FaunaGeneticsManager registered = GlobalRegistry.FaunaGenetics;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsFaunaGeneticsRuntimeUsable(registered))
            {
                SuppressDuplicateService();
                return true;
            }

            GlobalRegistry.UnregisterFaunaGeneticsRuntime(registered);
            return false;
        }

        private static bool IsFaunaGeneticsRuntimeUsable(FaunaGeneticsManager manager)
        {
            return manager != null &&
                   manager._serviceRegistered &&
                   !manager._duplicateServiceSuppressed &&
                   manager.isActiveAndEnabled;
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

        private void TryRegisterWorldSeedProvider()
        {
            if (_worldSeedProviderRegistered || !Application.isPlaying || _duplicateServiceSuppressed)
                return;

            // Awake generates the seed, so a zero here means this instance was suppressed as a
            // duplicate before it ever ran. Registering a zero seed would make IsInitialized lie.
            if (_worldSeed == 0)
                return;

            // HectonWorldGenerator remains the preferred owner: it derives the seed from the
            // authored noise assets that actually shape its terrain, which is strictly more
            // informative than this persisted integer. Never displace a live one.
            IWorldSeedProvider existing = GlobalRegistry.WorldSeedProvider;
            if (!ReferenceEquals(existing, null) && !ReferenceEquals(existing, this) && existing.IsInitialized)
                return;

            GlobalRegistry.RegisterWorldSeedProvider(this);
            _worldSeedProviderRegistered = ReferenceEquals(GlobalRegistry.WorldSeedProvider, this);
        }

        private void TryUnregisterWorldSeedProvider()
        {
            if (!_worldSeedProviderRegistered)
                return;

            GlobalRegistry.UnregisterWorldSeedProvider(this);
            _worldSeedProviderRegistered = false;
        }

        private void CacheSaveServiceCold()
        {
            _saveService = GlobalRegistry.Save;
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered || !Application.isPlaying || !isActiveAndEnabled || _duplicateServiceSuppressed)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void CacheRunModifiersCold()
        {
            _runModifiers = GlobalRegistry.RunModifiers;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.RunModifierRuntime)
            {
                _runModifiers = currentService as RunModifierController;
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.WorldSeedProvider)
            {
                // Either a HectonWorldGenerator took the slot - in which case stand down, it is the
                // better owner - or the slot went empty and the world seed would otherwise fall back
                // to 0 for every procedural generator in the project.
                if (!ReferenceEquals(currentService, this))
                    _worldSeedProviderRegistered = false;

                TryRegisterWorldSeedProvider();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            TryUnregisterSaveParticipant();
            _saveService = currentService as ISaveService;
            TryRegisterSaveParticipant();
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
            ReadOnlySpan<char> creatureId = AsSpanOrEmpty(archetype != null ? archetype.creatureId : null);
            for (int i = 0; i < ModEcosystemRegistry.Count; i++)
            {
                FaunaBiomeMutationDefinition definition = ModEcosystemRegistry.GetAt(i);
                if (definition == null || definition.BiomeId != biomeIndex)
                    continue;

                if (!MatchesSpeciesFilter(definition, creatureId))
                    continue;

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
                uint speciesHash = HashString(AsSpanOrEmpty(archetype != null ? archetype.creatureId : null));
                if (hasAupProof)
                {
                    return FaunaGenome64.BuildAupSeed(
                        in spawnAup,
                        (uint)_worldSeed,
                        speciesHash,
                        (uint)biomeIndex);
                }

                uint hash = Mix((uint)_worldSeed);
                hash = Mix(hash ^ (uint)biomeIndex * 0x85EBCA6Bu);
                hash = Mix(hash ^ speciesHash);
                return hash;
            }
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out Hecton8.World.AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            Hecton8.World.AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = Hecton8.World.AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(localRuntime.x, localRuntime.y, localRuntime.z));
            return positionAup.IsFinite();
        }

        private int GenerateInitialSeed()
        {
            RunModifierController runModifierController = _runModifiers;
            if (runModifierController != null)
            {
                RunModifiersDTO modifiers = runModifierController.CurrentModifiers;
                ReadOnlySpan<char> dailySeedId = AsSpanOrEmpty(modifiers.dailySeedId);
                if (modifiers.isDailySeed && HasNonWhiteSpace(dailySeedId))
                {
                    int dailySeed = unchecked((int)HashString(dailySeedId));
                    return dailySeed != 0 ? dailySeed : FallbackWorldSeed;
                }
            }

            return FallbackWorldSeed;
        }

        private static bool MatchesSpeciesFilter(FaunaBiomeMutationDefinition definition, ReadOnlySpan<char> creatureId)
        {
            ReadOnlySpan<char> speciesId = AsSpanOrEmpty(definition.SpeciesId);
            return speciesId.Length == 0 || speciesId.SequenceEqual(creatureId);
        }

        private static ReadOnlySpan<char> AsSpanOrEmpty(string value)
        {
            return value != null ? value.AsSpan() : ReadOnlySpan<char>.Empty;
        }

        private static bool HasNonWhiteSpace(ReadOnlySpan<char> value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!IsAsciiWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static bool IsAsciiWhiteSpace(char value)
        {
            return value == ' ' || (uint)(value - '\t') <= 4u;
        }

        private static uint HashString(ReadOnlySpan<char> value)
        {
            unchecked
            {
                if (value.Length == 0)
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
