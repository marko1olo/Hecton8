// ============================================================================
// HECTON-8 â€” FaunaDirector.cs
// Ð”Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€ Ñ„Ð°ÑƒÐ½Ñ‹ â€” ÑƒÐ¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ ÑÐ¿Ð°Ð²Ð½Ð¾Ð¼ Ð¸ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¾Ð¼ Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ñ‹Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð².
//
// ÐžÐ¢Ð’Ð•Ð¢Ð¡Ð¢Ð’Ð•ÐÐÐžÐ¡Ð¢Ð˜:
//   1. ÐŸÐµÑ€Ð¸Ð¾Ð´Ð¸Ñ‡ÐµÑÐºÐ°Ñ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð¸Ð³Ñ€Ð¾ÐºÐ° (ISlowTickable).
//   2. ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ Ñ‚ÐµÐºÑƒÑ‰ÐµÐ³Ð¾ Ð±Ð¸Ð¾Ð¼Ð° Ñ‡ÐµÑ€ÐµÐ· MapMagicBridge (Ñ Ñ‚Ñ€Ð¾Ñ‚Ñ‚Ð»Ð¸Ð½Ð³Ð¾Ð¼).
//   3. Ð¡Ð¿Ð°Ð²Ð½ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² ÐºÐ¾Ð»ÑŒÑ†Ðµ Ð²Ð¾ÐºÑ€ÑƒÐ³ Ð¸Ð³Ñ€Ð¾ÐºÐ° Ñ‡ÐµÑ€ÐµÐ· ObjectPoolManager.
//   4. Culling: Ð´ÐµÑÐ¿Ð°Ð²Ð½ ÑÑƒÑ‰ÐµÑÑ‚Ð², ÑƒÐ¿Ð»Ñ‹Ð²ÑˆÐ¸Ñ… Ð·Ð° Ð¿Ñ€ÐµÐ´ÐµÐ»Ñ‹ killDistance.
//   5. Ð£Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ð»Ð¸Ð¼Ð¸Ñ‚Ð°Ð¼Ð¸ (Ð³Ð»Ð¾Ð±Ð°Ð»ÑŒÐ½Ñ‹Ð¹ max + per-type max).
//   6. Ð’Ð½ÐµÑˆÐ½ÐµÐµ ÑƒÐ¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ: ForceSpawnHorde, SetPredatorPressure
//      (Ð¾Ñ€ÐºÐµÑÑ‚Ñ€Ð¾Ð²ÐºÐ° Ð¾Ñ‚ HectonDirectorAI).
//
// ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð:
//   â€¢ ISlowTickable â€” Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ GameTickManager ÐºÐ°Ð¶Ð´Ñ‹Ðµ ~0.5-1 ÑÐµÐº.
//   â€¢ Pre-allocated List<ActiveCreature> â€” zero GC Ð¿Ñ€Ð¸ Ð¸Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ð¸.
//   â€¢ Swap-remove Ð¿Ñ€Ð¸ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ðµ â€” O(1) Ð±ÐµÐ· ÑÐ´Ð²Ð¸Ð³Ð° Ð¼Ð°ÑÑÐ¸Ð²Ð°.
//   â€¢ Ð’ÑÐµ distance-Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ¸ Ñ‡ÐµÑ€ÐµÐ· sqrMagnitude â€” Ð±ÐµÐ· sqrt.
//   â€¢ Stateful counters â€” Ð¸Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð¿Ð¾Ð´ÑÑ‡Ñ‘Ñ‚ O(1) Ð²Ð¼ÐµÑÑ‚Ð¾ O(n).
//   â€¢ Biome throttling â€” TryGetBiomeIndex Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ñ€Ð°Ð· Ð² 2 ÑÐµÐº.
//
// Ð¡ÐŸÐÐ’Ð ÐšÐžÐ›Ð¬Ð¦Ðž:
//   â€¢ Ð’Ð½ÑƒÑ‚Ñ€ÐµÐ½Ð½Ð¸Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ: 50Ð¼ (Ð½Ðµ ÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ÑŒ ÑÐ»Ð¸ÑˆÐºÐ¾Ð¼ Ð±Ð»Ð¸Ð·ÐºÐ¾).
//   â€¢ Ð’Ð½ÐµÑˆÐ½Ð¸Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ: 150Ð¼ (Ð½Ðµ ÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ÑŒ ÑÐ»Ð¸ÑˆÐºÐ¾Ð¼ Ð´Ð°Ð»ÐµÐºÐ¾).
//   â€¢ Ð’Ñ‹ÑÐ¾Ñ‚Ð°: Ð¼ÐµÐ¶Ð´Ñƒ Ð´Ð½Ð¾Ð¼ + offset Ð¸ Ð¿Ð¾Ð²ÐµÑ€Ñ…Ð½Ð¾ÑÑ‚ÑŒÑŽ Ð²Ð¾Ð´Ñ‹.
//
// HORDE SPAWN (ForceSpawnHorde):
//   â€¢ Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ HectonDirectorAI Ð¿Ñ€Ð¸ Peak-ÑÐ¾Ð±Ñ‹Ñ‚Ð¸Ð¸.
//   â€¢ Ð¡Ð¿Ð°Ð²Ð½Ð¸Ñ‚ 3-5 Ð°Ð³Ñ€ÐµÑÑÐ¸Ð²Ð½Ñ‹Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ñ€Ð°Ð´Ð¸ÑƒÑÐµ 10-15Ð¼ Ð¾Ñ‚ worldCenter.
//   â€¢ Ð˜Ð³Ð½Ð¾Ñ€Ð¸Ñ€ÑƒÐµÑ‚ Ð²Ð½ÑƒÑ‚Ñ€ÐµÐ½Ð½Ð¸Ðµ ÐºÑƒÐ»Ð´Ð°ÑƒÐ½Ñ‹ â€” ÑÑ‚Ð¾ Ð¿Ñ€Ð¸ÐºÐ°Ð· Ð”Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€Ð°.
//   â€¢ ÐÐµÐ¼ÐµÐ´Ð»ÐµÐ½Ð½Ð¾ ÑƒÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ ForceState(Aggressive) Ð½Ð° Ð²ÑÐµÑ… ÑÐ¿Ð°Ð²Ð½Ð¾Ð².
//   â€¢ Ð£Ð²Ð°Ð¶Ð°ÐµÑ‚ _pressureEnabled Ñ„Ð»Ð°Ð³ (Relax-Ñ„Ð°Ð·Ð° Ð±Ð»Ð¾ÐºÐ¸Ñ€ÑƒÐµÑ‚ Ð¾Ñ€Ð´Ñ‹).
//
// PREDATOR PRESSURE (SetPredatorPressure):
//   â€¢ false: Ð²ÑÐµ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¿ÐµÑ€ÐµÐ²Ð¾Ð´ÑÑ‚ÑÑ Ð² Wander (Ð¾Ñ‚ÑÑ‚ÑƒÐ¿Ð»ÐµÐ½Ð¸Ðµ).
//   â€¢ true: Ð²Ð¾ÑÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ðµ ÑˆÑ‚Ð°Ñ‚Ð½Ð¾Ð³Ð¾ AI behaviour.
//   â€¢ Ð£Ð¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ÑÑ HectonDirectorAI Ð¿Ñ€Ð¸ ÑÐ¼ÐµÐ½Ðµ Ñ„Ð°Ð·.
//
// ZERO GC:
//   â€¢ ActiveCreature â€” struct (44 Ð±Ð°Ð¹Ñ‚Ð° Ð½Ð° ÑÑ‚ÐµÐºÐµ).
//   â€¢ List<ActiveCreature> â€” pre-allocated, Ð±ÐµÐ· boxing.
//   â€¢ Spawn directions use a 64-step LUT; runtime spawn loops do not call Sin/Cos.
//   â€¢ Random.Range â€” returns float/int (struct).
//   â€¢ ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… foreach, Ð½Ð¸ÐºÐ°ÐºÐ¸Ñ… LINQ.
//   â€¢ Biome check throttled â€” GetAlphamaps Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ñ Ñ€Ð°Ð· Ð² 2 ÑÐµÐº.
// ============================================================================

using System.Collections.Generic;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Ecosystem;
using Hecton8.Environment;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using float3 = Unity.Mathematics.float3;
using int3 = Unity.Mathematics.int3;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class FaunaDirector : MonoBehaviour, IUpdatable, ISlowTickable, ILateFrameTickable, ISaveable, RuntimeWatchdog.IEmergencyResetTarget, RuntimeWatchdog.IEmergencyColdTickCullTarget, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int CreaturePoolMinimumReserve = 8;
        private const int CreaturePoolBurstReserveMultiplier = 2;
        private const int SmallPassiveProxyMinimumReserve = 24;
        private const int SmallPassiveProxyBurstReserveMultiplier = 3;
        private const string SmallPassiveProxyPrefabName = "SmallPassiveProxy";
        private const string DebugBiomeFallbackLabel = "None";
        private const float PlayerResolveRetryInterval = 1f;
        private const float DirectorSlowTickIntervalSeconds = 0.5f;
        private const float ResidentDataOnlyLodTickIntervalSeconds = DirectorSlowTickIntervalSeconds;
        private const float ResidentDataOnlyLodMaxAccumulatedDeltaSeconds = ResidentDataOnlyLodTickIntervalSeconds * 4f;
        private const float DehydrationDistanceMeters = 40f;
        private const double DehydrationDistanceSq = DehydrationDistanceMeters * DehydrationDistanceMeters;
        private const float HibernationDistanceMeters = 150f;
        private const double HibernationDistanceSq = HibernationDistanceMeters * HibernationDistanceMeters;
        private const float HibernationStarvationHuntThreshold01 = 0.9f;
        private const float HibernationHungerCatchUpRatePerSecond = 0.045f;
        private const float ThermalApexMigrationIntervalSeconds = 2f;
        private const float ThermalApexMigrationRadiusMeters = 1000f;
        private const float ThermalApexMigrationStepMeters = 250f;
        private const int GlobalFaunaHardCap = 200;
        private const int PredatorHardCapPerKilometerSector = 5;
        private const uint StandardFaunaInstanceTypeId = 0xF9u;
        private const byte ResidentSimulationFlag = 1 << 0;
        private const byte DehydratedSimulationFlag = 1 << 1;
        private const uint ApexFaunaInstanceTypeId = 0xFAu;
        private const int InvalidDehydrationSlotIndex = -1;
        private const int MaxFaunaResidencySlots = 512;
        private const float SpawnVisibilityDotThreshold = 0.5f;
        private const float SpawnVisibilityDotThresholdSqr = SpawnVisibilityDotThreshold * SpawnVisibilityDotThreshold;
        private const float MinimumSpawnViewDirectionMagnitudeSqr = 0.0001f;
        private const int SpawnDirectionLutSize = 64;
        private const int SpawnDirectionLutMask = SpawnDirectionLutSize - 1;
        private const float RuntimeSettingsRefreshInterval = 5f;
        private const int AcousticPanicCommandCapacity = 8;
        private const int AcousticPanicCommandIndexMask = AcousticPanicCommandCapacity - 1;
        private const float AcousticPingBoidPanicRadiusMeters = 100f;
        private const float AcousticPingBoidPanicDurationSeconds = 3f;
        private const uint ActiveCreatureFlagPredator = 1u << 0;
        private const uint ActiveCreatureFlagHasBrain = 1u << 1;
        private const string MaxHibernatedFaunaStatesWarning = "[FaunaDirector] Max hibernated fauna states reached. Extra residents were not saved.";
        private static readonly string[] ThermalHabitatTokens = { "thermal", "brine", "heat", "furnace", "volcanic", "chemical" };
        private static readonly string[] CaveHabitatTokens = { "cave", "nest", "ambush", "rift", "pocket", "burrow", "crevice" };
        private static readonly Vector2[] _spawnDirectionLut = BuildSpawnDirectionLut(); // COLD ALLOC: Vector2[64] - spawn ring direction lookup table - owner: FaunaDirector
        private static readonly Quaternion[] _spawnRotationLut = BuildSpawnRotationLut(); // COLD ALLOC: Quaternion[64] - spawn yaw lookup table - owner: FaunaDirector
        private Unity.Mathematics.Random _biomeSpawnRandom;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ACTIVE CREATURE â€” struct tracker
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð—Ð°Ð¿Ð¸ÑÑŒ Ð¾Ð± Ð°ÐºÑ‚Ð¸Ð²Ð½Ð¾Ð¼ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ðµ. Struct â€” zero GC Ð¿Ñ€Ð¸ Ñ…Ñ€Ð°Ð½ÐµÐ½Ð¸Ð¸ Ð² List.
        /// Ð¥Ñ€Ð°Ð½Ð¸Ñ‚ Ð¼Ð¸Ð½Ð¸Ð¼ÑƒÐ¼ Ð´Ð°Ð½Ð½Ñ‹Ñ… Ð´Ð»Ñ culling Ð¸ accounting.
        /// </summary>
        private struct ActiveCreature
        {
            /// <summary>Ð¡ÑÑ‹Ð»ÐºÐ° Ð½Ð° GameObject (Ð¸Ð· Ð¿ÑƒÐ»Ð°).</summary>
            public GameObject gameObject;

            /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Transform (avoid GetComponent per frame).</summary>
            public Transform transform;
            public FaunaBrain brain;

            /// <summary>Ð˜Ð½Ð´ÐµÐºÑ Ð² FaunaBiomeData.possibleCreatures (Ð´Ð»Ñ counting).</summary>
            public int creatureTypeIndex;

            /// <summary>Ð˜Ð½Ð´ÐµÐºÑ Ð±Ð¸Ð¾Ð¼Ð°, Ð² ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¼ Ð±Ñ‹Ð» Ð·Ð°ÑÐ¿Ð°Ð²Ð½ÐµÐ½.</summary>
            public int biomeIndex;

            /// <summary>ÐŸÑ€ÐµÑ„Ð°Ð±-Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸Ðº (Ð´Ð»Ñ Ð¿ÑƒÐ»Ð°, ÐµÑÐ»Ð¸ Ð¿Ð¾Ð½Ð°Ð´Ð¾Ð±Ð¸Ñ‚ÑÑ Ð¸Ð´ÐµÐ½Ñ‚Ð¸Ñ„Ð¸ÐºÐ°Ñ†Ð¸Ñ).</summary>
            public GameObject prefabSource;

            /// <summary>ÐÑ€Ñ…ÐµÑ‚Ð¸Ð¿ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð´Ð»Ñ Ñ€ÐµÐ³Ð¸Ð´Ñ€Ð°Ñ‚Ð°Ñ†Ð¸Ð¸ Ð¿Ð¾ÑÐ»Ðµ off-screen Ð´ÐµÐ°ÐºÑ‚Ð¸Ð²Ð°Ñ†Ð¸Ð¸.</summary>
            public CreatureArchetypeData archetype;

            /// <summary>Ð§Ð°Ð½Ðº, Ð² ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¼ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð¾ Ð±Ñ‹Ð»Ð¾ Ð·Ð°ÑÐ¿Ð°Ð²Ð½ÐµÐ½Ð¾.</summary>
            public WorldChunkCoordinate chunkCoord;

            /// <summary>Ð‘Ð¾Ð»ÑŒÑˆÐ¾Ð¹ ÑƒÑ‡Ð°ÑÑ‚Ð¾Ðº Ð²Ð¾Ð´Ñ‹, Ðº ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¼Ñƒ Ð¿Ñ€Ð¸Ð²ÑÐ·Ð°Ð½Ð° ÐºÑ€ÑƒÐ¿Ð½Ð°Ñ ÑƒÐ³Ñ€Ð¾Ð·Ð°.</summary>
            public WorldMacroZoneCoordinate macroZoneCoord;

            /// <summary>Ð¯Ð²Ð»ÑÐµÑ‚ÑÑ Ð»Ð¸ ÑÑ‚Ð¾ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð¾ ÐºÑ€ÑƒÐ¿Ð½Ð¾Ð¹ ÑƒÐ³Ñ€Ð¾Ð·Ð¾Ð¹ Ð±Ð¾Ð»ÑŒÑˆÐ¾Ð³Ð¾ ÑƒÑ‡Ð°ÑÑ‚ÐºÐ° Ð²Ð¾Ð´Ñ‹.</summary>
            public bool isLargeThreat;
            public bool isPredator;
            public uint watchdogFlags;
            public AbsoluteUniversePosition lastKnownAup;
            public bool hasLastKnownAup;

            /// <summary>Ð˜Ð½Ð´ÐµÐºÑ Ñ€ÐµÐ·Ð¸Ð´ÐµÐ½Ñ‚Ð½Ð¾Ð³Ð¾ ÑÐ»Ð¾Ñ‚Ð° Ð´ÐµÐ³Ð¸Ð´Ñ€Ð°Ñ‚Ð°Ñ†Ð¸Ð¸.</summary>
            public uint uniqueInstanceUid;
            public int dehydrationSlotIndex;
        }

        private struct ResolvedFaunaEntry
        {
            public GameObject prefab;
            public CreatureArchetypeData archetype;
            public int speciesId;
            public float spawnWeight;
            public int maxAlive;
            public int creatureTypeIndex;
            public bool isLargeThreat;
            public bool isPredator;
            public bool blockedWhenPressureDisabled;
            public bool prefersClaustrophobicZone;
            public bool prefersThermalZone;
            public bool prefersHighPressureZone;
        }

        private struct FaunaResidencyState
        {
            public GameObject prefabSource;
            public CreatureArchetypeData archetype;
            public Quaternion rotation;
            public Vector3 linearVelocity;
            public Vector3 angularVelocity;
            public Vector3 pendingHibernationHuntTarget;
            public float health;
            public float hunger01;
            public float hibernationStartTimeSeconds;
            public int speciesId;
            public int creatureTypeIndex;
            public int biomeIndex;
            public WorldChunkCoordinate chunkCoord;
            public WorldMacroZoneCoordinate macroZoneCoord;
            public bool isLargeThreat;
            public bool isPredator;
            public uint uniqueInstanceUid;
            public bool isResident;
            public bool isDehydrated;
            public bool hasPendingHibernationHuntTarget;
        }

        private struct FaunaHibernationRestoreResult
        {
            public float Health;
            public float Hunger01;
        }

        private struct AcousticPanicCommand
        {
            public Vector3 RuntimePosition;
            public float RadiusMeters;
            public float DurationSeconds;
            public float Intensity01;
            public uint Seed;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” DATASETS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Biome Datasets â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð”Ð°Ð½Ð½Ñ‹Ðµ Ñ„Ð°ÑƒÐ½Ñ‹ Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð±Ð¸Ð¾Ð¼Ð°. " +
                 "Ð˜Ð½Ð´ÐµÐºÑÑ‹ biomeIndex Ð´Ð¾Ð»Ð¶Ð½Ñ‹ ÑÐ¾Ð¾Ñ‚Ð²ÐµÑ‚ÑÑ‚Ð²Ð¾Ð²Ð°Ñ‚ÑŒ MapMagic Biomes Set.")]
        [SerializeField] private FaunaBiomeData[] biomeDatasets;

        [Header("â”€â”€ Chunk Streaming â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐžÐ±Ñ‰Ð¸Ð¹ Ð¿Ñ€Ð¾Ñ„Ð¸Ð»ÑŒ Ñ‡Ð°Ð½ÐºÐ¾Ð²Ð¾Ð³Ð¾ Ð¼Ð¸Ñ€Ð°. Ð•ÑÐ»Ð¸ Ð·Ð°Ð´Ð°Ð½, Ñ„Ð°ÑƒÐ½Ð° Ð±ÐµÑ€Ñ‘Ñ‚ Ð¸Ð· Ð½ÐµÐ³Ð¾ Ñ€Ð°Ð·Ð¼ÐµÑ€Ñ‹ Ñ‡Ð°Ð½ÐºÐ°, Ñ€Ð°Ð´Ð¸ÑƒÑÑ‹ Ð¶Ð¸Ð·Ð½Ð¸ Ð¸ Ð²Ð¼ÐµÑÑ‚Ð¸Ð¼Ð¾ÑÑ‚ÑŒ.")]
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;
        [SerializeField] private WorldFaunaSpawnRegistry spawnRegistry;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;
        [SerializeField] private float ordinaryAnchorReuseCooldownSeconds = 90f;
        [SerializeField] private float largeThreatZoneReuseCooldownSeconds = 300f;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [SerializeField] private WorldZoneDirector worldZoneDirector;
        [SerializeField] private DepthZoneDirector depthZoneDirector;

        [Header("â”€â”€ Ecology Cadence â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Global fauna budget scale when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmFaunaBudgetScale = 0.9f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyFaunaBudgetScale = 1.2f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedFaunaBudgetScale = 1f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileFaunaBudgetScale = 0.78f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmBiomeCapScale = 0.92f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyBiomeCapScale = 1.25f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedBiomeCapScale = 1f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileBiomeCapScale = 0.82f;

        [Header("â”€â”€ Ecology Composition â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.25f, 2f)] private float calmPassiveSelectionScale = 1.25f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.1f, 2f)] private float calmAggressiveSelectionScale = 0.58f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.25f, 2f)] private float livelyPassiveSelectionScale = 1.15f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.1f, 2f)] private float livelyAggressiveSelectionScale = 0.82f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.25f, 2f)] private float mixedPassiveSelectionScale = 1f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.1f, 2f)] private float mixedAggressiveSelectionScale = 1f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.25f, 2f)] private float hostilePassiveSelectionScale = 0.72f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileAggressiveSelectionScale = 1.35f;
        [Tooltip("Passive fauna weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0.25f, 2f)] private float safePocketPassiveSelectionScale = 1.4f;
        [Tooltip("Aggressive fauna weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0.05f, 2f)] private float safePocketAggressiveSelectionScale = 0.4f;
        [Tooltip("Large-threat weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0f, 2f)] private float safePocketLargeThreatSelectionScale = 0.08f;
        [Tooltip("Passive fauna weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileZonePassiveSelectionScale = 0.68f;
        [Tooltip("Aggressive fauna weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileZoneAggressiveSelectionScale = 1.4f;
        [Tooltip("Large-threat weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0f, 2f)] private float hostileZoneLargeThreatSelectionScale = 1.45f;
        [Tooltip("Large-threat weight multiplier while the player is on a route-critical lane. Keeps navigation legible.")]
        [SerializeField, Range(0f, 2f)] private float routeCriticalLargeThreatSelectionScale = 0.72f;

        [Header("â”€â”€ Depth-Zone Ecology â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Overall fauna-budget scale inside cave-tagged depth zones. Reduces live density so cramped water reads clearer.")]
        [SerializeField, Range(0.25f, 1.5f)] private float caveDepthZoneBudgetScale = 0.84f;
        [Tooltip("Overall fauna-budget scale inside thermal depth zones. Keeps hot water dense enough to feel alive without flooding weak hardware.")]
        [SerializeField, Range(0.25f, 1.5f)] private float thermalDepthZoneBudgetScale = 0.9f;
        [Tooltip("Overall fauna-budget scale inside high-danger or high-hull-pressure depth zones.")]
        [SerializeField, Range(0.25f, 1.5f)] private float highPressureDepthZoneBudgetScale = 0.82f;
        [Tooltip("Passive fauna weight multiplier inside cave-tagged depth zones.")]
        [SerializeField, Range(0.1f, 2f)] private float caveDepthZonePassiveSelectionScale = 0.74f;
        [Tooltip("Aggressive fauna weight multiplier inside cave-tagged depth zones.")]
        [SerializeField, Range(0.1f, 2f)] private float caveDepthZoneAggressiveSelectionScale = 1.28f;
        [Tooltip("Large-threat weight multiplier inside cave-tagged depth zones.")]
        [SerializeField, Range(0f, 2f)] private float caveDepthZoneLargeThreatSelectionScale = 0.86f;
        [Tooltip("Passive fauna weight multiplier inside thermal depth zones.")]
        [SerializeField, Range(0.1f, 2f)] private float thermalDepthZonePassiveSelectionScale = 0.82f;
        [Tooltip("Aggressive fauna weight multiplier inside thermal depth zones.")]
        [SerializeField, Range(0.1f, 2f)] private float thermalDepthZoneAggressiveSelectionScale = 1.18f;
        [Tooltip("Large-threat weight multiplier inside thermal depth zones.")]
        [SerializeField, Range(0f, 2f)] private float thermalDepthZoneLargeThreatSelectionScale = 1.08f;
        [Tooltip("Bonus weight for entries whose authored traits read as cave / ambush ecology when the current depth zone has caves.")]
        [SerializeField, Range(0.5f, 2f)] private float caveSpecialistSelectionScale = 1.35f;
        [Tooltip("Bonus weight for entries whose authored traits read as thermal / brine ecology when the current depth zone is thermal.")]
        [SerializeField, Range(0.5f, 2f)] private float thermalSpecialistSelectionScale = 1.4f;
        [Tooltip("Bonus weight for entries whose authored traits read as high-pressure hunters when the current depth zone is dangerous.")]
        [SerializeField, Range(0.5f, 2f)] private float highPressureHunterSelectionScale = 1.22f;

        [Header("â”€â”€ Adaptive Perf Budget â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Scales fauna density from DynamicResolutionScaler render scale so weak devices shed live-world pressure before frame time collapses.")]
        [SerializeField] private bool enableAdaptivePerfBudget = true;
        [Tooltip("Render scale floor where fauna density reaches its lowest adaptive budget.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveBudgetFloorRenderScale = 0.72f;
        [Tooltip("Minimum global fauna budget scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveGlobalFaunaBudgetFloor = 0.58f;
        [Tooltip("Minimum per-biome fauna cap scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveBiomeCapBudgetFloor = 0.62f;
        [Tooltip("Minimum spawn-burst scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveSpawnBurstBudgetFloor = 0.4f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” LIMITS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Global Limits â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð¾Ðµ Ð¾Ð±Ñ‰ÐµÐµ ÐºÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ð¼Ð¸Ñ€Ðµ.")]
        [SerializeField] private int globalMaxCount = 30;

        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð¾Ðµ ÐºÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ ÑÐ¿Ð°Ð²Ð½Ð¾Ð² Ð·Ð° Ð¾Ð´Ð¸Ð½ SlowTick. " +
                 "ÐŸÑ€ÐµÐ´Ð¾Ñ‚Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ spike Ð¿Ñ€Ð¸ Ð²Ñ…Ð¾Ð´Ðµ Ð² Ð½Ð¾Ð²Ñ‹Ð¹ Ð±Ð¸Ð¾Ð¼.")]
        [SerializeField] private int maxSpawnsPerTick = 3;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” SPAWN RING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Spawn Ring â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ñ ÑÐ¿Ð°Ð²Ð½Ð° Ð¾Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° (Ð¼ÐµÑ‚Ñ€Ñ‹).")]
        [SerializeField] private float spawnRingInner = 50f;

        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ñ ÑÐ¿Ð°Ð²Ð½Ð° Ð¾Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ° (Ð¼ÐµÑ‚Ñ€Ñ‹).")]
        [SerializeField] private float spawnRingOuter = 150f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” CULLING
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Culling â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Ð”Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ñ Ð¾Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ°, Ð¿Ð¾ÑÐ»Ðµ ÐºÐ¾Ñ‚Ð¾Ñ€Ð¾Ð¹ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð¾ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ÑÑ.")]
        [SerializeField] private float killDistance = 200f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” HORDE SETTINGS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Horde Spawn (Director Command) â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð¾Ðµ ÐºÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ð¾Ñ€Ð´Ðµ.")]
        [SerializeField] private int hordeCountMin = 3;

        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð¾Ðµ ÐºÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ð¾Ñ€Ð´Ðµ.")]
        [SerializeField] private int hordeCountMax = 5;

        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ ÑÐ¿Ð°Ð²Ð½Ð° Ð¾Ñ€Ð´Ñ‹ Ð¾Ñ‚ Ñ†ÐµÐ½Ñ‚Ñ€Ð° (Ð¼ÐµÑ‚Ñ€Ñ‹).")]
        [SerializeField] private float hordeRadiusInner = 10f;

        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ñ€Ð°Ð´Ð¸ÑƒÑ ÑÐ¿Ð°Ð²Ð½Ð° Ð¾Ñ€Ð´Ñ‹ Ð¾Ñ‚ Ñ†ÐµÐ½Ñ‚Ñ€Ð° (Ð¼ÐµÑ‚Ñ€Ñ‹).")]
        [SerializeField] private float hordeRadiusOuter = 15f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  INSPECTOR â€” DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private int _debugActiveCount;
        [SerializeField] private int _debugCurrentBiome = -1;
        [SerializeField] private int _debugSpawnAttempts;
        [SerializeField] private int _debugCullCount;
        [SerializeField] private bool _debugPressureEnabled = true;
        [SerializeField] private int _debugLastHordeSpawned;
        [SerializeField] private int _debugActiveChunks;
        [SerializeField] private int _debugActiveMacroZones;
        [SerializeField] private int _debugRegistryFaunaAnchors;
        [SerializeField] private int _debugRegistryLargeThreatZones;
        [SerializeField] private int _debugCurrentChunkX;
        [SerializeField] private int _debugCurrentChunkZ;
        [SerializeField] private int _debugCurrentMacroZoneX;
        [SerializeField] private int _debugCurrentMacroZoneZ;
        [SerializeField] private float _debugRuntimeChunkSize = 192f;
        [SerializeField] private float _debugRuntimeMacroZoneSize = 768f;
        [SerializeField] private int _debugRuntimeGlobalMaxCount = 30;
        [SerializeField] private int _debugRuntimePerChunkMaxCount = 6;
        [SerializeField] private float _debugRuntimeSpawnOuter = 150f;
        [SerializeField] private float _debugRuntimeLargeThreatSpawnOuter = 420f;
        [SerializeField] private float _debugRuntimeCullDistance = 200f;
        [SerializeField] private float _debugRuntimeLargeThreatCullDistance = 900f;
        [SerializeField] private int _debugSpawnValidationAttempts;
        [SerializeField] private int _debugSpawnValidationSuccesses;
        [SerializeField] private int _debugAnchorBasedSpawns;
        [SerializeField] private int _debugFallbackRingSpawns;
        [SerializeField] private WorldProceduralFaunaMood _debugMatrixFaunaMood = WorldProceduralFaunaMood.None;
        [SerializeField] private int _debugEffectiveGlobalMaxCount = 30;
        [SerializeField] private int _debugEffectiveSpawnsPerTick = 3;
        [SerializeField] private int _debugEffectiveBiomeMaxCount = 10;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveGlobalBudgetScale = 1f;
        [SerializeField] private float _debugAdaptiveBiomeBudgetScale = 1f;
        [SerializeField] private float _debugAdaptiveSpawnBudgetScale = 1f;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private bool _debugCurrentZoneRouteCritical;
        [SerializeField] private bool _debugCurrentZoneSafePocket;
        [SerializeField] private float _debugPassiveSelectionScale = 1f;
        [SerializeField] private float _debugAggressiveSelectionScale = 1f;
        [SerializeField] private float _debugLargeThreatSelectionScale = 1f;
        [SerializeField] private string _debugCurrentDepthZone = "None";
        [SerializeField] private bool _debugCurrentDepthZoneThermal;
        [SerializeField] private bool _debugCurrentDepthZoneCaves;
        [SerializeField] private float _debugCurrentDepthZoneDanger;
        [SerializeField] private float _debugDepthZoneBudgetScale = 1f;
        [SerializeField] private float _debugDepthZoneSpecialistScale = 1f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CACHED STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Pre-allocated ÑÐ¿Ð¸ÑÐ¾Ðº Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð².
        /// Capacity = globalMaxCount. ÐÐ¸ÐºÐ¾Ð³Ð´Ð° Ð½Ðµ Ð¿Ñ€ÐµÐ²Ñ‹ÑˆÐ°ÐµÑ‚.
        /// Swap-remove Ð¿Ñ€Ð¸ Ð´ÐµÑÐ¿Ð°Ð²Ð½Ðµ â€” Ð¿Ð¾Ñ€ÑÐ´Ð¾Ðº Ð½Ðµ Ð²Ð°Ð¶ÐµÐ½.
        /// </summary>
        private List<ActiveCreature> _activeCreatures;

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Transform Ð¸Ð³Ñ€Ð¾ÐºÐ°.</summary>
        private Transform _playerTransform;
        private Transform _playerViewTransform;
        private Vector3 _playerLookViewPosition;
        private Vector3 _playerLookViewForward;
        private bool _hasPlayerLookView;
        private int _playerRuntimeContextCacheFrame = -1;
        private bool _playerRuntimeContextCacheValid;
        private PlayerRuntimeContext _playerRuntimeContextCache;
        private IPhysicsService _physicsService;
        private IVegetationThreatReadModel _vegetationThreatBridge;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _hotSwapRegistered;
        private bool _saveRegistered;
        private ISaveService _saveService;
        private float _slowTickAccumulator;
        private bool _pendingResidentCreatureHydration;
        private AbsoluteUniversePosition _pendingResidentCreatureHydrationAup;
        private bool _pendingCreatureSpawnFlush;
        private FaunaBiomeData _pendingCreatureSpawnBiome;
        private ITerrainProvider _pendingCreatureSpawnBridge;
        private Vector3 _pendingCreatureSpawnPlayerPos;
        private AbsoluteUniversePosition _pendingCreatureSpawnPlayerAup;
        // COLD ALLOC: AcousticPanicCommand[8] - active sonar panic bridge to GPU boids - owner: FaunaDirector
        private readonly AcousticPanicCommand[] _acousticPanicCommands = new AcousticPanicCommand[AcousticPanicCommandCapacity];
        private int _acousticPanicReadIndex;
        private int _acousticPanicWriteIndex;
        private int _acousticPanicCount;
        private uint _acousticPanicSequence;
        private int _lastAcousticPingSnapshotGeneration;
        private bool _acousticPingSubscribed;

        /// <summary>ÐšÐ²Ð°Ð´Ñ€Ð°Ñ‚ killDistance Ð´Ð»Ñ sqrMagnitude.</summary>
        private float _killDistanceSqr;

        /// <summary>
        /// Lookup: biomeIndex â†’ FaunaBiomeData.
        /// Pre-built Ð² Awake. Dictionary&lt;int, FaunaBiomeData&gt;.
        /// ÐžÐ´Ð½Ð° Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ñ Ð¿Ñ€Ð¸ ÑÑ‚Ð°Ñ€Ñ‚Ðµ.
        /// </summary>
        private Dictionary<int, FaunaBiomeData> _biomeLookup;
        private Dictionary<long, int> _countsPerChunk;
        private Dictionary<long, int> _largeThreatCountsPerMacroZone;
        private float _runtimeSpawnRingInner;
        private float _runtimeSpawnRingOuter;
        private float _runtimeLargeThreatSpawnInner;
        private float _runtimeLargeThreatSpawnOuter;
        private float _runtimeKillDistance;
        private float _runtimeKillDistanceSqr;
        private float _runtimeChunkSize = 192f;
        private float _runtimeMacroZoneSize = 768f;
        private float _runtimeLargeThreatKillDistance;
        private float _runtimeLargeThreatKillDistanceSqr;
        private int _runtimeGlobalMaxCount;
        private int _runtimeMaxSpawnsPerTick;
        private int _runtimePerChunkMaxCount;
        private int _runtimeMaxNearbyLargeThreats = 1;
        private int _runtimeFaunaAnchorChunkDistance = 2;
        private int _runtimeLargeThreatMacroZoneDistance = 1;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  BIOME THROTTLING â€” ÑÐ½Ð¸Ð¶ÐµÐ½Ð¸Ðµ Ñ‡Ð°ÑÑ‚Ð¾Ñ‚Ñ‹ GetAlphamaps
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Absolute time gate for the next biome probe.</summary>
        private float _nextBiomeCheckTime = float.NegativeInfinity;

        /// <summary>Ð˜Ð½Ñ‚ÐµÑ€Ð²Ð°Ð» Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ¸ Ð±Ð¸Ð¾Ð¼Ð° (ÑÐµÐºÑƒÐ½Ð´Ñ‹). Ð¡Ð½Ð¸Ð¶Ð°ÐµÑ‚ GC Ð¾Ñ‚ GetAlphamaps.</summary>
        private const float BiomeCheckInterval = 2.0f;

        /// <summary>ÐšÑÑˆÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ð¹ Ñ€ÐµÐ·ÑƒÐ»ÑŒÑ‚Ð°Ñ‚ Ð¿Ð¾ÑÐ»ÐµÐ´Ð½ÐµÐ¹ Ð¿Ñ€Ð¾Ð²ÐµÑ€ÐºÐ¸ Ð±Ð¸Ð¾Ð¼Ð°. -1 = Ð½Ðµ Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»Ñ‘Ð½.</summary>
        private int _cachedBiomeIndex = -1;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STATEFUL COUNTERS â€” Ð¸Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð¿Ð¾Ð´ÑÑ‡Ñ‘Ñ‚ O(1)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð¡Ñ‡Ñ‘Ñ‚Ñ‡Ð¸Ðº ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð¿Ð¾ Ð¸Ð½Ð´ÐµÐºÑÑƒ Ð±Ð¸Ð¾Ð¼Ð°.
        /// Ð Ð°Ð·Ð¼ÐµÑ€ = maxBiomeIndex + 1. Ð˜Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚/Ð´ÐµÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚ Ð¿Ñ€Ð¸ ÑÐ¿Ð°Ð²Ð½Ðµ/Ð´ÐµÑÐ¿Ð°Ð²Ð½Ðµ.
        /// Ð—Ð°Ð¼ÐµÐ½ÑÐµÑ‚ O(n) CountBiomeCreatures.
        /// </summary>
        private int[] _countsPerBiome;

        /// <summary>
        /// Ð¡Ñ‡Ñ‘Ñ‚Ñ‡Ð¸Ðº ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð¿Ð¾ Ñ‚Ð¸Ð¿Ð°Ð¼ Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð±Ð¸Ð¾Ð¼Ð°.
        /// ÐšÐ»ÑŽÑ‡ = FaunaBiomeData, Ð—Ð½Ð°Ñ‡ÐµÐ½Ð¸Ðµ = int[possibleCreatures.Count].
        /// Ð—Ð°Ð¼ÐµÐ½ÑÐµÑ‚ O(n) CountCreatureTypes.
        /// </summary>
        private Dictionary<FaunaBiomeData, int[]> _countsPerTypePerBiome;
        private Dictionary<FaunaBiomeData, ResolvedFaunaEntry[]> _resolvedEntriesPerBiome;
        private Dictionary<FaunaBiomeData, int[]> _availablePoolCountsPerBiome;
        private Dictionary<FaunaBiomeData, Dictionary<GameObject, int>> _prefabTypeIndexLookup;
        private Dictionary<long, int> _predatorCountsPerSector;
        private FaunaSimulationMemory _faunaSimulationMemory;
        private FaunaSimulationEngine _faunaSimulationEngine;
        private FaunaPresentationService _faunaPresentationService;
        private bool _faunaSimulationRegistered;
        private ITerrainProvider _mapMagicRuntime;
        private IMicroFaunaPresentationPulseSink _sargassumMicroFauna;
        private IObjectPoolService _objectPool;
        private IEcosystemDirectorService _ecosystemDirector;
        private IFaunaPersistentWorldStateService _persistentWorldRegistry;
        private IThermodynamicsService _thermalRuntime;
        private IDynamicResolutionRuntime _dynamicResolutionRuntime;
        private IDepthZoneReadModel _depthZoneReadModel;
        // COLD ALLOC: FaunaResidencyState[512] - resident fauna state cache for dehydration and restore - owner: FaunaDirector
        private FaunaResidencyState[] _dehydratedCreatureStates;
        // COLD ALLOC: int[512] - active dehydrated slot index list for rehydration scans - owner: FaunaDirector
        private int[] _activeDehydrationSlots;
        private int _activeDehydrationSlotCount;
        // COLD ALLOC: GameObject[200] - LateFrame fauna presentation disable queue for poolless fallbacks - owner: FaunaDirector
        private readonly GameObject[] _pendingPresentationDeactivations = new GameObject[GlobalFaunaHardCap];
        private int _pendingPresentationDeactivationCount;
        // COLD ALLOC: List<EntityDataRecord>[64] - nearby tier-2 fauna restore scratch loaded from persistent sector cache - owner: FaunaDirector
        private List<EntityDataRecord> _persistedFaunaRestoreScratch;
        private int _persistedTier2FaunaCount;
        private JobHandle _residentDataOnlyLodHandle;
        private bool _residentDataOnlyLodScheduled;
        private float _residentDataOnlyLodDeltaAccumulator;
        private float _nextPlayerResolveTime = float.NegativeInfinity;
        private float _nextRuntimeSettingsRefreshTime = float.NegativeInfinity;
        private float _nextThermalApexMigrationTime = float.NegativeInfinity;
        private bool _runtimeSettingsDirty = true;
        private WorldProceduralFaunaMood _currentMatrixFaunaMood;
        private int _currentEffectiveGlobalMaxCount = 30;
        private int _currentEffectiveSpawnsPerTick = 3;
        private int _currentEffectiveBiomeMaxCount = 10;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveGlobalBudgetScale = 1f;
        private float _adaptiveBiomeBudgetScale = 1f;
        private float _adaptiveSpawnBudgetScale = 1f;
        private float _currentPassiveSelectionScale = 1f;
        private float _currentAggressiveSelectionScale = 1f;
        private float _currentLargeThreatSelectionScale = 1f;
        private WorldZoneAnchor _currentZone;
        private DepthZoneProfile _currentDepthZone;
        private bool _currentZoneIsSafePocket;
        private float _currentDepthZoneBudgetScale = 1f;
        private float _currentDepthZoneSpecialistScale = 1f;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PREDATOR PRESSURE STATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð¤Ð»Ð°Ð³ Ñ€Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ð¸Ñ Ñ…Ð¸Ñ‰Ð½Ð¾Ð³Ð¾ Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ñ.
        /// false = Relax-Ñ„Ð°Ð·Ð°: ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¿ÐµÑ€ÐµÐ²ÐµÐ´ÐµÐ½Ñ‹ Ð² Wander, Ð¾Ñ€Ð´Ñ‹ Ð·Ð°Ð¿Ñ€ÐµÑ‰ÐµÐ½Ñ‹.
        /// true  = ÑˆÑ‚Ð°Ñ‚Ð½Ñ‹Ð¹ Ñ€ÐµÐ¶Ð¸Ð¼: Ð½Ð¾Ñ€Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ AI behaviour, Ð¾Ñ€Ð´Ñ‹ Ñ€Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ñ‹.
        /// Ð£Ð¿Ñ€Ð°Ð²Ð»ÑÐµÑ‚ÑÑ Ñ‡ÐµÑ€ÐµÐ· SetPredatorPressure() Ð¸Ð· HectonDirectorAI.
        /// Default = true (Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ€Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ð¾ Ð¿Ñ€Ð¸ ÑÑ‚Ð°Ñ€Ñ‚Ðµ).
        /// </summary>
        private bool _pressureEnabled = true;
        private bool _creaturePoolsWarmed;
        private int _defaultCreaturePoolWarmupReserve;
        private int _smallPassiveCreaturePoolWarmupReserve;
        private readonly List<GameObject> _warmupPrefabs = new List<GameObject>(128); // COLD ALLOC: flat fauna pool warmup prefab dedupe scratch - owner: FaunaDirector
        internal static FaunaDirector ActiveRuntimeInstance { get; private set; }
        internal int DebugEffectiveSpawnsPerTick { get { return _debugEffectiveSpawnsPerTick; } }
        internal int DebugEffectiveBiomeMaxCount { get { return _debugEffectiveBiomeMaxCount; } }
        internal int DebugEffectiveGlobalMaxCount { get { return _debugEffectiveGlobalMaxCount; } }
        internal string DebugBiomeLabel
        {
            get
            {
                return biomeMatrixDirector != null && biomeMatrixDirector.CurrentProfile != null
                    ? biomeMatrixDirector.CurrentProfile.name
                    : DebugBiomeFallbackLabel;
            }
        }
        internal string DebugEcologyBiasLabel { get { return ResolveDebugEcologyBiasLabel(); } }
        public int SavePriority { get { return 56; } }
        public int LoadPriority { get { return 56; } }
        public ServiceHeartbeatState HeartbeatState => IsServiceReady ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;
        public bool IsServiceReady => _faunaSimulationRegistered && _faunaSimulationEngine != null && _faunaSimulationEngine.IsReady;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            // COLD ALLOC: FaunaSimulationEngine[1] - data-only fauna LOD simulation service registered through GlobalRegistry - owner: FaunaDirector
            _faunaSimulationEngine ??= new FaunaSimulationEngine();
            _faunaSimulationEngine.BindOwner(this);
            // COLD ALLOC: FaunaPresentationService[1] - spawn presentation service isolated from native simulation - owner: FaunaDirector
            _faunaPresentationService ??= new FaunaPresentationService();
            RefreshColdRegistryDependencies();
            EnsureRuntimeStateInitialized();
            InitializeDehydrationResidencyState();
            _biomeSpawnRandom = CreateBiomeSpawnRandom();
            ResolveSpawnRegistry();
            ResolveBiomeMatrixDirector();
            ResolveWorldZoneDirector();
            ResolveDepthZoneDirector();
            ResolveVegetationThreatBridge();
            RefreshRuntimeStreamingSettings();
            TryWarmupCreaturePools();
            _runtimeSettingsDirty = false;
            _nextRuntimeSettingsRefreshTime = ReadDispatcherTimeSeconds() + RuntimeSettingsRefreshInterval;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            TryRegisterSaveParticipant();
            TryRegisterFaunaSimulationService();
            RuntimeWatchdog.RegisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.FaunaDirector, this);
            SubscribeAcousticPingEvents();
            TryRegisterDispatcherTicks();

            InvalidatePlayerRuntimeContextCache();
            if (_playerTransform == null)
                FindPlayer(true);
            else
                ResolvePlayerViewTransform();

            if (spawnRegistry == null)
                ResolveSpawnRegistry();

            if (biomeMatrixDirector == null)
                ResolveBiomeMatrixDirector();

            if (worldZoneDirector == null)
                ResolveWorldZoneDirector();

            if (depthZoneDirector == null)
                ResolveDepthZoneDirector();
            if (_vegetationThreatBridge == null)
                ResolveVegetationThreatBridge();

            if (!_creaturePoolsWarmed)
                TryWarmupCreaturePools();

            _slowTickAccumulator = 0f;
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            ShutdownServiceState(releaseNativeState: false);
        }

        private void OnDestroy()
        {
            ShutdownServiceState(releaseNativeState: true);
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState(releaseNativeState: true);
        }

        private void ShutdownServiceState(bool releaseNativeState)
        {
            TryUnregisterSaveParticipant();

            InvalidatePlayerRuntimeContextCache();
            RuntimeWatchdog.UnregisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.FaunaDirector, this);
            TryUnregisterFaunaSimulationService();
            TryUnregisterHotSwapListener();
            UnsubscribeAcousticPingEvents();
            CompleteResidentDataOnlySimulation(forceComplete: false);

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _lateFrameRegistered = false;
            }

            if (releaseNativeState)
                DisposeDehydrationResidencyState();

            _slowTickAccumulator = 0f;
            if (releaseNativeState && ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        /// <summary>
        /// Explicit bootstrap registration pass for the data-only fauna simulation service.
        /// </summary>
        internal void InitializeService()
        {
            ActiveRuntimeInstance = this;
            _faunaSimulationEngine ??= new FaunaSimulationEngine();
            _faunaSimulationEngine.BindOwner(this);
            RefreshColdRegistryDependencies();
            TryRegisterHotSwapListener();
            EnsureRuntimeStateInitialized();
            InitializeDehydrationResidencyState();
            TryRegisterFaunaSimulationService();
            RuntimeWatchdog.RegisterEmergencyResetTarget(RuntimeWatchdog.RuntimeWatchdogLane.FaunaDirector, this);
            SubscribeAcousticPingEvents();
            TryRegisterDispatcherTicks();
        }

        private void TryRegisterDispatcherTicks()
        {
            if (!Application.isPlaying)
                return;

            if (!_dispatcherRegistered)
                _dispatcherRegistered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
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

        private void RefreshColdRegistryDependencies()
        {
            _mapMagicRuntime = GlobalRegistry.Terrain;
            _physicsService = GlobalRegistry.Physics;
            _sargassumMicroFauna = GlobalRegistry.MicroFaunaPresentationPulses;
            _objectPool = GlobalRegistry.ObjectPoolService;
            _ecosystemDirector = GlobalRegistry.EcosystemDirector;
            _persistentWorldRegistry = GlobalRegistry.PersistentWorldRegistry;
            _thermalRuntime = GlobalRegistry.ThermodynamicsService;
            _dynamicResolutionRuntime = GlobalRegistry.DynamicResolutionRuntime;
            _depthZoneReadModel = GlobalRegistry.DepthZoneReadModel;
            if (depthZoneDirector == null)
                depthZoneDirector = GlobalRegistry.DepthZone;
            if (_depthZoneReadModel == null && depthZoneDirector != null)
                _depthZoneReadModel = depthZoneDirector;
            if (_vegetationThreatBridge == null)
                _vegetationThreatBridge = GlobalRegistry.VegetationThreat;
            if (_saveService == null)
                _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.MapMagicRuntime:
                case GlobalRegistryServiceSlot.TerrainProviderRuntime:
                    _mapMagicRuntime = currentService as ITerrainProvider;
                    _cachedBiomeIndex = -1;
                    _nextBiomeCheckTime = float.NegativeInfinity;
                    break;
                case GlobalRegistryServiceSlot.Physics:
                    _physicsService = currentService as IPhysicsService;
                    break;
                case GlobalRegistryServiceSlot.SargassumMicroFaunaRuntime:
                    _sargassumMicroFauna = currentService as IMicroFaunaPresentationPulseSink;
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    _objectPool = currentService as IObjectPoolService;
                    _creaturePoolsWarmed = false;
                    break;
                case GlobalRegistryServiceSlot.EcosystemDirector:
                    _ecosystemDirector = currentService as IEcosystemDirectorService;
                    break;
                case GlobalRegistryServiceSlot.PersistentWorldRegistry:
                    _persistentWorldRegistry = currentService as IFaunaPersistentWorldStateService;
                    break;
                case GlobalRegistryServiceSlot.ThermodynamicsRuntime:
                case GlobalRegistryServiceSlot.ThermodynamicsService:
                    _thermalRuntime = currentService as IThermodynamicsService;
                    break;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionRuntime = currentService as IDynamicResolutionRuntime;
                    _runtimeSettingsDirty = true;
                    break;
                case GlobalRegistryServiceSlot.DepthZoneRuntime:
                    _depthZoneReadModel = currentService as IDepthZoneReadModel;
                    _runtimeSettingsDirty = true;
                    break;
                case GlobalRegistryServiceSlot.MapMagicVegetationRuntime:
                    _vegetationThreatBridge = currentService as IVegetationThreatReadModel;
                    break;
                case GlobalRegistryServiceSlot.Save:
                    TryUnregisterSaveParticipant();
                    _saveService = currentService as ISaveService;
                    if (_saveService != null)
                    {
                        _saveService.Register(this);
                        _saveRegistered = true;
                    }
                    break;
            }
        }

        private void TryRegisterSaveParticipant()
        {
            if (_saveRegistered)
                return;

            if (_saveService == null)
                _saveService = Hecton8.SaveSystem.SaveManager.ActiveRuntimeInstance;
            if (_saveService == null)
                return;

            _saveService.Register(this);
            _saveRegistered = true;
        }

        private void TryUnregisterSaveParticipant()
        {
            if (!_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _saveService = null;
            _saveRegistered = false;
        }

        public void ServiceEmergencyReset()
        {
            DespawnAll();
            ResetDehydrationResidencyState();
            TryRegisterFaunaSimulationService();
            _slowTickAccumulator = 0f;
            _runtimeSettingsDirty = true;
            _nextRuntimeSettingsRefreshTime = 0f;
        }

        private void TryRegisterFaunaSimulationService()
        {
            if (_faunaSimulationRegistered || _faunaSimulationEngine == null)
                return;

            GlobalRegistry.RegisterFaunaSimulationService(_faunaSimulationEngine);
            _faunaSimulationRegistered = true;
        }

        private void TryUnregisterFaunaSimulationService()
        {
            if (!_faunaSimulationRegistered || _faunaSimulationEngine == null)
                return;

            GlobalRegistry.UnregisterFaunaSimulationService(_faunaSimulationEngine);
            _faunaSimulationRegistered = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  ISlowTickable â€” MAIN LOOP (~Ñ€Ð°Ð· Ð² 0.5-1 ÑÐµÐºÑƒÐ½Ð´Ñƒ)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð“Ð»Ð°Ð²Ð½Ñ‹Ð¹ Ñ†Ð¸ÐºÐ» Ð”Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€Ð°. ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº:
        ///   1. ÐŸÑ€Ð¾Ð²ÐµÑ€ÐºÐ° Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ñ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
        ///   2. Culling (Ð´ÐµÑÐ¿Ð°Ð²Ð½ Ð´Ð°Ð»Ñ‘ÐºÐ¸Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð²).
        ///   3. ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÐµÐ½Ð¸Ðµ Ð±Ð¸Ð¾Ð¼Ð° (Ñ Ñ‚Ñ€Ð¾Ñ‚Ñ‚Ð»Ð¸Ð½Ð³Ð¾Ð¼).
        ///   4. Ð¡Ð¿Ð°Ð²Ð½ Ð½Ð¾Ð²Ñ‹Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð² (ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ ÑÐ»Ð¾Ñ‚Ñ‹).
        ///
        /// ZERO GC: struct math, pre-allocated collections, no LINQ.
        /// Biome check throttled â€” GetAlphamaps Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ñ€Ð°Ð· Ð² BiomeCheckInterval.
        /// </summary>
        /// <summary>
        /// Advances the director cadence on the registry dispatcher lane.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by <see cref="SystemDispatcher"/>.</param>
        public void Tick(float deltaTime)
        {
            long watchdogSampleTimestamp = RuntimeWatchdog.BeginFaunaArterySample();
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.FaunaDirector);
            if (deltaTime <= 0f)
            {
                RuntimeWatchdog.EndFaunaArterySample(watchdogSampleTimestamp);
                return;
            }

            ConsumeAcousticPingSignals();
            DrainAcousticPanicCommands();
            AccumulateResidentDataOnlyLodDelta(deltaTime);

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < DirectorSlowTickIntervalSeconds)
            {
                RuntimeWatchdog.EndFaunaArterySample(watchdogSampleTimestamp);
                return;
            }

            _slowTickAccumulator -= DirectorSlowTickIntervalSeconds;
            if (_slowTickAccumulator > DirectorSlowTickIntervalSeconds)
                _slowTickAccumulator = DirectorSlowTickIntervalSeconds;

            SlowTick();

            if (TryResolvePlayerLogicPose(out _, out AbsoluteUniversePosition postSlowTickPlayerAup))
                TryScheduleResidentDataOnlySimulation(in postSlowTickPlayerAup);
            RuntimeWatchdog.EndFaunaArterySample(watchdogSampleTimestamp);
        }

        public void LateFrameTick()
        {
            CompleteResidentDataOnlySimulation(forceComplete: false);
            FlushPendingPresentationDeactivations();
            FlushPendingResidentCreatureHydration();
            FlushPendingCreatureSpawns();
        }

        private void SubscribeAcousticPingEvents()
        {
            if (_acousticPingSubscribed || !Application.isPlaying)
                return;

            SignalBus<AcousticPingSignal>.EnsureInitialized();
            _lastAcousticPingSnapshotGeneration = SignalBus<AcousticPingSignal>.SnapshotGeneration;
            _acousticPingSubscribed = true;
        }

        private void UnsubscribeAcousticPingEvents()
        {
            if (!_acousticPingSubscribed)
                return;

            _acousticPingSubscribed = false;
            _lastAcousticPingSnapshotGeneration = 0;
            _acousticPanicReadIndex = 0;
            _acousticPanicWriteIndex = 0;
            _acousticPanicCount = 0;
        }

        private void ConsumeAcousticPingSignals()
        {
            if (!_acousticPingSubscribed)
                return;

            int snapshotGeneration = SignalBus<AcousticPingSignal>.SnapshotGeneration;
            if (snapshotGeneration == 0 || snapshotGeneration == _lastAcousticPingSnapshotGeneration)
                return;

            _lastAcousticPingSnapshotGeneration = snapshotGeneration;
            System.ReadOnlySpan<AcousticPingSignal> signals = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                ref readonly AcousticPingSignal signal = ref signals[i];
                float intensity01 = math.saturate(signal.Intensity01);
                if (intensity01 <= 0.0001f)
                    continue;

                float3 runtimePosition = signal.PositionAup.ToRuntimeFloat3();
                if (!math.all(math.isfinite(runtimePosition)))
                    continue;

                EnqueueAcousticPanicCommand(
                    new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    AcousticPingBoidPanicRadiusMeters,
                    AcousticPingBoidPanicDurationSeconds,
                    intensity01);
            }
        }

        private void EnqueueAcousticPanicCommand(
            Vector3 runtimePosition,
            float radiusMeters,
            float durationSeconds,
            float intensity01)
        {
            if (radiusMeters <= 0.001f || durationSeconds <= 0.001f || intensity01 <= 0.0001f)
                return;

            uint seed = math.hash(new int4(
                math.asint(runtimePosition.x),
                math.asint(runtimePosition.y),
                math.asint(runtimePosition.z),
                unchecked((int)++_acousticPanicSequence)));
            if (seed == 0u)
                seed = 0x9E3779B9u;

            if (_acousticPanicCount >= AcousticPanicCommandCapacity)
            {
                _acousticPanicReadIndex = (_acousticPanicReadIndex + 1) & AcousticPanicCommandIndexMask;
                _acousticPanicCount--;
            }

            _acousticPanicCommands[_acousticPanicWriteIndex] = new AcousticPanicCommand
            {
                RuntimePosition = runtimePosition,
                RadiusMeters = radiusMeters,
                DurationSeconds = durationSeconds,
                Intensity01 = math.saturate(intensity01),
                Seed = seed
            };
            _acousticPanicWriteIndex = (_acousticPanicWriteIndex + 1) & AcousticPanicCommandIndexMask;
            _acousticPanicCount++;
        }

        private void DrainAcousticPanicCommands()
        {
            if (_acousticPanicCount <= 0)
                return;

            IMicroFaunaPresentationPulseSink boids = _sargassumMicroFauna;
            while (_acousticPanicCount > 0)
            {
                AcousticPanicCommand command = _acousticPanicCommands[_acousticPanicReadIndex];
                _acousticPanicCommands[_acousticPanicReadIndex] = default;
                _acousticPanicReadIndex = (_acousticPanicReadIndex + 1) & AcousticPanicCommandIndexMask;
                _acousticPanicCount--;

                if (boids == null)
                    continue;

                boids.RegisterAcousticPanicBurst(
                    command.RuntimePosition,
                    command.RadiusMeters,
                    command.DurationSeconds,
                    command.Intensity01,
                    command.Seed);
            }
        }

        /// <summary>
        /// Main ecology evaluation pass.
        /// Handles player resolution, culling, biome probing, and spawn issuance without allocations.
        /// </summary>
        public void SlowTick()
        {
            EnsureRuntimeStateInitialized();
            ResolveBiomeMatrixDirector();
            ResolveWorldZoneDirector();
            ResolveDepthZoneDirector();
            ResolveVegetationThreatBridge();
            if (!_creaturePoolsWarmed)
                TryWarmupCreaturePools();
            float nowSeconds = ReadDispatcherTimeSeconds();
            if (_runtimeSettingsDirty || nowSeconds >= _nextRuntimeSettingsRefreshTime)
            {
                RefreshRuntimeStreamingSettings();
                _runtimeSettingsDirty = false;
                _nextRuntimeSettingsRefreshTime = nowSeconds + RuntimeSettingsRefreshInterval;
            }
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  1. PLAYER CHECK
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null)
                    return;
            }

            ResolvePlayerViewTransform();

            // Unity null check (player could be destroyed)
            if (_playerTransform == null)
            {
                _playerTransform = null;
                _playerViewTransform = null;
                _hasPlayerLookView = false;
                _nextPlayerResolveTime = 0f;
                return;
            }

            if (!TryResolvePlayerLogicPose(out Vector3 playerPos, out AbsoluteUniversePosition playerAup))
            {
                _playerTransform = null;
                _playerViewTransform = null;
                _hasPlayerLookView = false;
                _nextPlayerResolveTime = 0f;
                return;
            }

            RestorePersistedTier2Fauna(in playerAup);
            int spawnValidationAttempts = 0;
            int spawnValidationSuccesses = 0;
            int anchorBasedSpawns = 0;
            int fallbackRingSpawns = 0;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  2. CULLING â€” Ð´ÐµÑÐ¿Ð°Ð²Ð½ Ð´Ð°Ð»Ñ‘ÐºÐ¸Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð²
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            int cullCount = CullOrDehydrateDistantCreatures(in playerAup);
            QueueResidentCreatureHydration(in playerAup);
            OffloadPersistedTier2Fauna(in playerAup);
            ApplyThermalApexMigrationToPersistedTier2Fauna();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  3. BIOME DETECTION (THROTTLED)
            //     GetAlphamaps Ð²Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ GC-Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸ÑŽ, Ð¿Ð¾ÑÑ‚Ð¾Ð¼Ñƒ
            //     Ð¿Ñ€Ð¾Ð²ÐµÑ€ÑÐµÐ¼ Ð±Ð¸Ð¾Ð¼ Ñ€Ð°Ð· Ð² BiomeCheckInterval ÑÐµÐºÑƒÐ½Ð´.
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            ITerrainProvider bridge = _mapMagicRuntime;
            if (bridge == null)
            {
                UpdateDiagnostics(cullCount, 0, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
                return;
            }

            if (nowSeconds >= _nextBiomeCheckTime)
            {
                _nextBiomeCheckTime = nowSeconds + BiomeCheckInterval;
                if (bridge.TryGetBiomeIndex(playerPos.x, playerPos.z, out int biome))
                {
                    _cachedBiomeIndex = biome;
                }
            }

            int currentBiome = _cachedBiomeIndex;
            if (currentBiome == -1)
            {
                // Ð‘Ð¸Ð¾Ð¼ ÐµÑ‰Ñ‘ Ð½Ðµ Ð¾Ð¿Ñ€ÐµÐ´ÐµÐ»Ñ‘Ð½ â€” Ð¿Ñ€Ð¾Ð¿ÑƒÑÐºÐ°ÐµÐ¼ ÑÐ¿Ð°Ð²Ð½
                UpdateDiagnostics(cullCount, 0, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
                return;
            }

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  4. SPAWN â€” ÐµÑÐ»Ð¸ ÐµÑÑ‚ÑŒ ÑÐ²Ð¾Ð±Ð¾Ð´Ð½Ñ‹Ðµ ÑÐ»Ð¾Ñ‚Ñ‹
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            int spawnAttempts = 0;
            _currentMatrixFaunaMood = biomeMatrixDirector != null && biomeMatrixDirector.CurrentProfile != null
                ? biomeMatrixDirector.CurrentProfile.faunaMood
                : WorldProceduralFaunaMood.None;
            _currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            _currentDepthZone = _depthZoneReadModel != null ? _depthZoneReadModel.CurrentZone : null;
            _currentZoneIsSafePocket = IsSafePocketZone(_currentZone);

            RefreshAdaptiveBudgetResponse();
            RefreshEcologyCompositionResponse(_currentMatrixFaunaMood, _currentZone, _currentDepthZone);
            _currentEffectiveGlobalMaxCount = ResolveEffectiveGlobalMaxCount(_currentMatrixFaunaMood, _currentDepthZone);
            _currentEffectiveSpawnsPerTick = ResolveEffectiveSpawnsPerTick(_currentMatrixFaunaMood, _currentDepthZone);
            _currentEffectiveBiomeMaxCount = 0;

            if (GetTrackedCreaturePopulationCount() < _currentEffectiveGlobalMaxCount)
            {
                // Ð˜Ñ‰ÐµÐ¼ Ð´Ð°Ð½Ð½Ñ‹Ðµ Ð±Ð¸Ð¾Ð¼Ð°
                if (_biomeLookup.TryGetValue(currentBiome, out FaunaBiomeData biomeData))
                {
                    _currentEffectiveBiomeMaxCount = ResolveEffectiveBiomeMaxCount(biomeData, _currentMatrixFaunaMood, _currentDepthZone);
                    QueueCreatureSpawns(biomeData, playerPos, in playerAup, bridge);
                }
            }

            UpdateDiagnostics(cullCount, spawnAttempts, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  CULLING
        /// <summary>
        /// Dehydrates distant active fauna into resident data-only slots instead of hard-despawning them.
        /// </summary>
        private int CullOrDehydrateDistantCreatures(in AbsoluteUniversePosition playerAup)
        {
            if (_activeCreatures == null || _activeCreatures.Count == 0)
                return 0;

            int dehydrated = 0;
            IObjectPoolService pool = _objectPool;

            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];
                if (creature.gameObject == null || creature.transform == null)
                {
                    DecrementCreatureCounters(in creature);
                    ReleaseDehydrationSlot(creature.dehydrationSlotIndex);
                    SwapRemoveAt(i);
                    dehydrated++;
                    continue;
                }

                if (!creature.gameObject.activeInHierarchy)
                {
                    DecrementCreatureCounters(in creature);
                    ReleaseDehydrationSlot(creature.dehydrationSlotIndex);
                    SwapRemoveAt(i);
                    dehydrated++;
                    continue;
                }

                if (!TryResolveActiveCreatureLogicAup(ref creature, out AbsoluteUniversePosition creatureAup))
                {
                    DecrementCreatureCounters(in creature);
                    ReleaseDehydrationSlot(creature.dehydrationSlotIndex);
                    SwapRemoveAt(i);
                    dehydrated++;
                    continue;
                }

                _activeCreatures[i] = creature;
                if (AbsoluteUniversePosition.DistanceSq(in creatureAup, in playerAup) < DehydrationDistanceSq)
                    continue;

                UpdateResidencyStateFromActiveCreature(in creature, in creatureAup, markDehydrated: true);
                QueuePresentationDeactivation(creature.gameObject);

                AddActiveDehydrationSlot(creature.dehydrationSlotIndex);
                SwapRemoveAt(i);
                dehydrated++;
            }

            return dehydrated;
        }

        private void QueuePresentationDeactivation(GameObject target)
        {
            if (target == null || _pendingPresentationDeactivationCount >= _pendingPresentationDeactivations.Length)
                return;

            _pendingPresentationDeactivations[_pendingPresentationDeactivationCount++] = target;
        }

        private void FlushPendingPresentationDeactivations()
        {
            IObjectPoolService pool = _objectPool;
            for (int i = 0; i < _pendingPresentationDeactivationCount; i++)
            {
                GameObject target = _pendingPresentationDeactivations[i];
                _pendingPresentationDeactivations[i] = null;
                if (target == null)
                    continue;

                if (pool != null)
                    pool.Despawn(target);
                else if (target.activeSelf)
                    target.SetActive(false);
            }

            _pendingPresentationDeactivationCount = 0;
        }

        //  SPAWN
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ‹Ñ‚Ð°ÐµÑ‚ÑÑ Ð·Ð°ÑÐ¿Ð°Ð²Ð½Ð¸Ñ‚ÑŒ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ñ‚ÐµÐºÑƒÑ‰ÐµÐ¼ Ð±Ð¸Ð¾Ð¼Ðµ.
        ///
        /// ÐÐ»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼:
        ///   1. ÐŸÐ¾Ð»ÑƒÑ‡Ð¸Ñ‚ÑŒ Ñ‚ÐµÐºÑƒÑ‰Ð¸Ðµ ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¸ Ð¸Ð· stateful-ÑÑ‚Ñ€ÑƒÐºÑ‚ÑƒÑ€ (O(1)).
        ///   2. Ð¦Ð¸ÐºÐ» Ð´Ð¾ maxSpawnsPerTick (Ð¸Ð»Ð¸ Ð¿Ð¾ÐºÐ° Ð½Ðµ Ð·Ð°Ð¿Ð¾Ð»Ð½ÐµÐ½ globalMaxCount).
        ///   3. Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ ÑÐ»ÑƒÑ‡Ð°Ð¹Ð½ÑƒÑŽ Ñ‚Ð¾Ñ‡ÐºÑƒ Ð² ÐºÐ¾Ð»ÑŒÑ†Ðµ Ð²Ð¾ÐºÑ€ÑƒÐ³ Ð¸Ð³Ñ€Ð¾ÐºÐ°.
        ///   4. ÐŸÑ€Ð¾Ð²ÐµÑ€Ð¸Ñ‚ÑŒ Ð²Ñ‹ÑÐ¾Ñ‚Ñƒ Ð´Ð½Ð° Ñ‡ÐµÑ€ÐµÐ· MapMagicBridge.
        ///   5. ÐŸÑ€Ð¾Ð²ÐµÑ€Ð¸Ñ‚ÑŒ Ñ‡Ñ‚Ð¾ Ñ‚Ð¾Ñ‡ÐºÐ° Ð¿Ð¾Ð´ Ð²Ð¾Ð´Ð¾Ð¹.
        ///   6. Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ Ñ‚Ð¸Ð¿ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ñ‡ÐµÑ€ÐµÐ· weighted random.
        ///   7. Ð¡Ð¿Ð°Ð²Ð½ Ñ‡ÐµÑ€ÐµÐ· ObjectPoolManager.
        ///   8. Ð˜Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¸.
        ///
        /// ZERO GC: spawn direction LUT, Random.Range â†’ float,
        /// stateful counters (no per-tick O(n) scan), struct ActiveCreature.
        /// </summary>
        private int TrySpawnCreatures(
            FaunaBiomeData biomeData,
            Vector3 playerPos,
            in AbsoluteUniversePosition playerAup,
            ITerrainProvider bridge,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            IObjectPoolService pool = _objectPool;
            if (pool == null) return 0;

            int biomeIdx = biomeData.biomeIndex;
            IEcosystemDirectorService ecosystemDirector = ResolveEcosystemDirector();

            // â”€â”€ ÐŸÐ¾Ð»ÑƒÑ‡ÐµÐ½Ð¸Ðµ ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¾Ð² Ð¸Ð· stateful-ÑÑ‚Ñ€ÑƒÐºÑ‚ÑƒÑ€ (O(1)) â”€â”€
            int biomeAlive = (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                ? _countsPerBiome[biomeIdx]
                : 0;

            if (biomeAlive >= _currentEffectiveBiomeMaxCount)
                return 0;

            // ÐœÐ°ÑÑÐ¸Ð² per-type counts Ð´Ð»Ñ ÑÑ‚Ð¾Ð³Ð¾ Ð±Ð¸Ð¾Ð¼Ð° (ÑÑÑ‹Ð»ÐºÐ°, Ð½Ðµ ÐºÐ¾Ð¿Ð¸Ñ)
            if (!_countsPerTypePerBiome.TryGetValue(biomeData, out int[] creatureTypeCounts))
                return 0;
            if (_resolvedEntriesPerBiome == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out ResolvedFaunaEntry[] resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0)
            {
                return 0;
            }
            if (_availablePoolCountsPerBiome == null ||
                !_availablePoolCountsPerBiome.TryGetValue(biomeData, out int[] availablePoolCounts) ||
                availablePoolCounts == null ||
                availablePoolCounts.Length < resolvedEntries.Length)
            {
                return 0;
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                GameObject prefab = resolvedEntries[i].prefab;
                availablePoolCounts[i] = prefab != null ? pool.GetAvailableCount(prefab) : 0;
            }

            int spawned = 0;

            for (int attempt = 0; attempt < _currentEffectiveSpawnsPerTick; attempt++)
            {
                // Global limit
                if (GetTrackedCreaturePopulationCount() >= _currentEffectiveGlobalMaxCount)
                    break;

                // Biome limit
                if (biomeAlive >= _currentEffectiveBiomeMaxCount)
                    break;

                if (!TrySelectResolvedEntry(
                    resolvedEntries,
                    creatureTypeCounts,
                    availablePoolCounts,
                    biomeData.biomeIndex,
                    _pressureEnabled,
                    _currentPassiveSelectionScale,
                    _currentAggressiveSelectionScale,
                    _currentLargeThreatSelectionScale,
                    _currentDepthZone,
                    _currentDepthZoneSpecialistScale,
                    out ResolvedFaunaEntry selectedEntry))
                {
                    // Ð’ÑÐµ Ñ‚Ð¸Ð¿Ñ‹ Ð½Ð° Ð»Ð¸Ð¼Ð¸Ñ‚Ðµ â€” Ð¿Ñ€ÐµÐºÑ€Ð°Ñ‰Ð°ÐµÐ¼
                    break;
                }

                bool isLargeThreat = selectedEntry.isLargeThreat;
                Vector3 spawnPos;
                WorldMacroZoneCoordinate spawnMacroZone = default;
                WorldFaunaSpawnRegistry.Anchor sourceAnchor = default;
                bool usedRegistryAnchor = false;
                bool hasSpawnPoint = isLargeThreat
                    ? TryResolveLargeThreatSpawnLocation(
                        playerPos,
                        in playerAup,
                        _playerViewTransform,
                        bridge,
                        biomeData,
                        out spawnPos,
                        out spawnMacroZone,
                        out sourceAnchor,
                        out usedRegistryAnchor,
                        ref spawnValidationAttempts,
                        ref spawnValidationSuccesses,
                        ref anchorBasedSpawns,
                        ref fallbackRingSpawns)
                    : TryResolveOrdinarySpawnLocation(
                        playerPos,
                        in playerAup,
                        _playerViewTransform,
                        bridge,
                        biomeData,
                        out spawnPos,
                        out sourceAnchor,
                        out usedRegistryAnchor,
                        ref spawnValidationAttempts,
                        ref spawnValidationSuccesses,
                        ref anchorBasedSpawns,
                        ref fallbackRingSpawns);
                if (!hasSpawnPoint)
                    continue;

                if (!TrySelectResolvedEntryForSpawnPoint(
                        resolvedEntries,
                        creatureTypeCounts,
                        availablePoolCounts,
                        biomeData.biomeIndex,
                        _pressureEnabled,
                        _currentPassiveSelectionScale,
                        _currentAggressiveSelectionScale,
                        _currentLargeThreatSelectionScale,
                        _currentDepthZone,
                        _currentDepthZoneSpecialistScale,
                        isLargeThreat,
                        spawnPos,
                        _vegetationThreatBridge,
                        ecosystemDirector,
                        out selectedEntry))
                {
                    continue;
                }

                Quaternion spawnRot = NextSpawnRotation(ref _biomeSpawnRandom);
                WorldChunkCoordinate spawnChunk = WorldChunkCoordinate.FromWorldPosition(spawnPos, _runtimeChunkSize);
                if (selectedEntry.isPredator && GetPredatorSectorCount(spawnChunk) >= PredatorHardCapPerKilometerSector)
                    continue;

                if (GetChunkCreatureCount(spawnChunk) >= _runtimePerChunkMaxCount)
                    continue;

                if (selectedEntry.isPredator && GetPredatorSectorCount(spawnChunk) >= PredatorHardCapPerKilometerSector)
                    continue;

                if (isLargeThreat)
                {
                    if (!CanSpawnLargeThreatNearPlayer(spawnMacroZone, playerPos))
                        continue;
                }

                GameObject resolvedPrefab = selectedEntry.prefab;
                if (resolvedPrefab == null)
                    continue;

                if (!TryResolveRuntimePositionAup(spawnPos, out AbsoluteUniversePosition spawnAup))
                    continue;

                uint uniqueInstanceUid = IsApexPredatorArchetype(selectedEntry.archetype, isLargeThreat)
                    ? BuildApexFaunaInstanceUid(selectedEntry.archetype, in spawnMacroZone)
                    : BuildStandardFaunaInstanceUid(selectedEntry.speciesId, biomeIdx, spawnChunk, in spawnAup);
                if (uniqueInstanceUid != 0u &&
                    ecosystemDirector != null &&
                    ecosystemDirector.IsApexTombstoned(uniqueInstanceUid))
                {
                    continue;
                }

                if (ecosystemDirector != null &&
                    !ecosystemDirector.TryConsumeSpawnCredit(selectedEntry.archetype, isLargeThreat, selectedEntry.isPredator))
                {
                    continue;
                }

                GameObject instance = pool.Spawn(resolvedPrefab, spawnPos, spawnRot, false);

                if (instance == null)
                {
                    ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, isLargeThreat, selectedEntry.isPredator);
                    continue;
                }
                if (usedRegistryAnchor && sourceAnchor.runtimeKey != 0L)
                {
                    ResolveProceduralStateRegistry();
                    proceduralStateRegistry?.MarkFaunaAnchorUsed(
                        sourceAnchor.runtimeKey,
                        sourceAnchor.isLargeThreatZone,
                        isLargeThreat
                            ? Mathf.Max(0f, largeThreatZoneReuseCooldownSeconds)
                            : Mathf.Max(0f, ordinaryAnchorReuseCooldownSeconds));
                }


                // â”€â”€ ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° ÑÐ¿Ð°Ð²Ð½-Ð¿Ð¾Ð¸Ð½Ñ‚Ð° Ð´Ð»Ñ AI â”€â”€
                if (instance.TryGetComponent(out FaunaBrain ai))
                {
                    ai.ApplyArchetype(selectedEntry.archetype);
                    ai.SetSpawnPoint(spawnPos);
                    ai.SetLogicalIdentity(uniqueInstanceUid);
                    ai.SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                    _faunaPresentationService?.ConfigureSpawnedCreature(ai, selectedEntry.archetype, biomeIdx, spawnPos, in spawnChunk);
                }

                int typeIndex = selectedEntry.creatureTypeIndex;

                // â”€â”€ Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð°Ñ†Ð¸Ñ Ð² Ñ‚Ñ€ÐµÐºÐµÑ€Ðµ â”€â”€
                if (!TryBuildActiveCreatureRecord(
                        instance,
                        resolvedPrefab,
                        selectedEntry.archetype,
                        typeIndex,
                        biomeIdx,
                        spawnChunk,
                        spawnMacroZone,
                        isLargeThreat,
                        selectedEntry.isPredator,
                        ai,
                        uniqueInstanceUid,
                        in spawnAup,
                        out ActiveCreature record))
                {
                    ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, isLargeThreat, selectedEntry.isPredator);
                    if (pool != null)
                        pool.Despawn(instance);
                    else
                        instance.SetActive(false);

                    continue;
                }

                _activeCreatures.Add(record);

                // â”€â”€ Ð˜Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¾Ð² â”€â”€
                if (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                    _countsPerBiome[biomeIdx]++;

                if (typeIndex >= 0 && typeIndex < creatureTypeCounts.Length)
                    creatureTypeCounts[typeIndex]++;
                if (typeIndex >= 0 && typeIndex < availablePoolCounts.Length && availablePoolCounts[typeIndex] > 0)
                    availablePoolCounts[typeIndex]--;

                IncrementChunkCount(spawnChunk);
                if (selectedEntry.isPredator)
                    IncrementPredatorSectorCount(spawnChunk);
                if (isLargeThreat)
                    IncrementMacroZoneCount(spawnMacroZone);

                biomeAlive++;
                spawned++;
            }

            return spawned;
        }

        private void QueueCreatureSpawns(
            FaunaBiomeData biomeData,
            Vector3 playerPos,
            in AbsoluteUniversePosition playerAup,
            ITerrainProvider bridge)
        {
            _pendingCreatureSpawnBiome = biomeData;
            _pendingCreatureSpawnPlayerPos = playerPos;
            _pendingCreatureSpawnPlayerAup = playerAup;
            _pendingCreatureSpawnBridge = bridge;
            _pendingCreatureSpawnFlush = true;
        }

        private void FlushPendingCreatureSpawns()
        {
            if (!_pendingCreatureSpawnFlush)
                return;

            FaunaBiomeData biomeData = _pendingCreatureSpawnBiome;
            ITerrainProvider bridge = _pendingCreatureSpawnBridge;
            Vector3 playerPos = _pendingCreatureSpawnPlayerPos;
            AbsoluteUniversePosition playerAup = _pendingCreatureSpawnPlayerAup;
            _pendingCreatureSpawnFlush = false;
            _pendingCreatureSpawnBiome = null;
            _pendingCreatureSpawnBridge = null;

            if (biomeData == null || bridge == null)
                return;

            int spawnValidationAttempts = 0;
            int spawnValidationSuccesses = 0;
            int anchorBasedSpawns = 0;
            int fallbackRingSpawns = 0;
            int spawnAttempts = TrySpawnCreatures(
                biomeData,
                playerPos,
                in playerAup,
                bridge,
                ref spawnValidationAttempts,
                ref spawnValidationSuccesses,
                ref anchorBasedSpawns,
                ref fallbackRingSpawns);
            UpdateDiagnostics(0, spawnAttempts, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  STATEFUL COUNTER HELPERS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð”ÐµÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚Ð¸Ñ€ÑƒÐµÑ‚ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¸ Ð¿Ñ€Ð¸ ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ð¸ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°.
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¸Ð· CullDistantCreatures Ð¿ÐµÑ€ÐµÐ´ SwapRemoveAt.
        /// O(1). Zero GC.
        /// </summary>
        private bool TryResolveOrdinarySpawnLocation(
            Vector3 playerPos,
            in AbsoluteUniversePosition playerAup,
            Transform playerViewTransform,
            ITerrainProvider bridge,
            FaunaBiomeData biomeData,
            out Vector3 spawnPos,
            out WorldFaunaSpawnRegistry.Anchor registryAnchor,
            out bool usedRegistryAnchor,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            ResolveSpawnRegistry();

            if (spawnRegistry != null)
            {
                WorldChunkCoordinate playerChunk = WorldChunkCoordinate.FromWorldPosition(playerPos, _runtimeChunkSize);
                if (spawnRegistry.TryGetOrdinaryAnchor(playerPos, in playerAup, playerChunk, _runtimeFaunaAnchorChunkDistance, out WorldFaunaSpawnRegistry.Anchor anchor) &&
                    TryBuildSpawnPointAroundAnchor(anchor.position, anchor.radius, playerViewTransform, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                {
                    registryAnchor = anchor;
                    usedRegistryAnchor = true;
                    anchorBasedSpawns++;
                    return true;
                }
            }

            registryAnchor = default;
            usedRegistryAnchor = false;
            bool builtInRing = TryBuildSpawnPointInRing(
                playerPos,
                _runtimeSpawnRingInner,
                _runtimeSpawnRingOuter,
                playerViewTransform,
                biomeData,
                bridge,
                out spawnPos,
                ref spawnValidationAttempts,
                ref spawnValidationSuccesses);
            if (builtInRing)
                fallbackRingSpawns++;

            return builtInRing;
        }

        private bool TryResolveLargeThreatSpawnLocation(
            Vector3 playerPos,
            in AbsoluteUniversePosition playerAup,
            Transform playerViewTransform,
            ITerrainProvider bridge,
            FaunaBiomeData biomeData,
            out Vector3 spawnPos,
            out WorldMacroZoneCoordinate macroZoneCoord,
            out WorldFaunaSpawnRegistry.Anchor registryAnchor,
            out bool usedRegistryAnchor,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            ResolveSpawnRegistry();

            if (spawnRegistry != null)
            {
                WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(playerPos, _runtimeMacroZoneSize);
                if (spawnRegistry.TryGetLargeThreatZone(playerPos, in playerAup, playerMacroZone, _runtimeLargeThreatMacroZoneDistance, out WorldFaunaSpawnRegistry.Anchor zoneAnchor) &&
                    CanSpawnLargeThreatNearPlayer(zoneAnchor.macroZoneCoord, playerPos) &&
                    TryBuildSpawnPointAroundAnchor(zoneAnchor.position, zoneAnchor.radius, playerViewTransform, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                {
                    macroZoneCoord = zoneAnchor.macroZoneCoord;
                    registryAnchor = zoneAnchor;
                    usedRegistryAnchor = true;
                    anchorBasedSpawns++;
                    return true;
                }
            }

            if (TryBuildSpawnPointInRing(
                    playerPos,
                    _runtimeLargeThreatSpawnInner,
                    _runtimeLargeThreatSpawnOuter,
                    playerViewTransform,
                    biomeData,
                    bridge,
                    out spawnPos,
                    ref spawnValidationAttempts,
                    ref spawnValidationSuccesses))
            {
                macroZoneCoord = WorldMacroZoneCoordinate.FromWorldPosition(spawnPos, _runtimeMacroZoneSize);
                registryAnchor = default;
                usedRegistryAnchor = false;
                fallbackRingSpawns++;
                return CanSpawnLargeThreatNearPlayer(macroZoneCoord, playerPos);
            }

            spawnPos = default;
            macroZoneCoord = default;
            registryAnchor = default;
            usedRegistryAnchor = false;
            return false;
        }

        private bool TryBuildSpawnPointInRing(
            Vector3 center,
            float innerRadius,
            float outerRadius,
            Transform playerViewTransform,
            FaunaBiomeData biomeData,
            ITerrainProvider bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 direction = NextSpawnDirection(ref _biomeSpawnRandom);
                float distance = _biomeSpawnRandom.NextFloat(innerRadius, outerRadius);
                Vector3 candidateCenter = new Vector3(
                    center.x + direction.x * distance,
                    center.y,
                    center.z + direction.y * distance);

                if (TryBuildValidatedSpawnPoint(candidateCenter, playerViewTransform, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                    return true;
            }

            spawnPos = default;
            return false;
        }

        private bool TryBuildSpawnPointAroundAnchor(
            Vector3 anchorPosition,
            float anchorRadius,
            Transform playerViewTransform,
            FaunaBiomeData biomeData,
            ITerrainProvider bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            float safeRadius = Mathf.Max(6f, anchorRadius);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                Vector2 direction = NextSpawnDirection(ref _biomeSpawnRandom);
                float distance = _biomeSpawnRandom.NextFloat(0f, safeRadius);
                Vector3 candidateCenter = new Vector3(
                    anchorPosition.x + direction.x * distance,
                    anchorPosition.y,
                    anchorPosition.z + direction.y * distance);

                if (TryBuildValidatedSpawnPoint(candidateCenter, playerViewTransform, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                    return true;
            }

            spawnPos = default;
            return false;
        }

        private bool TryBuildValidatedSpawnPoint(
            Vector3 candidateCenter,
            Transform playerViewTransform,
            FaunaBiomeData biomeData,
            ITerrainProvider bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            spawnValidationAttempts++;
            if (!bridge.TryGetHeight(candidateCenter.x, candidateCenter.z, out float bottomHeight))
            {
                spawnPos = default;
                return false;
            }

            float spawnY = biomeData.GetRandomSpawnHeight(ref _biomeSpawnRandom, bottomHeight);
            if (spawnY >= bridge.WaterSurfaceLevel || spawnY <= bottomHeight)
            {
                spawnPos = default;
                return false;
            }

            spawnPos = new Vector3(candidateCenter.x, spawnY, candidateCenter.z);
            if (!IsSpawnPointValid(playerViewTransform, spawnPos))
            {
                spawnPos = default;
                return false;
            }

            spawnValidationSuccesses++;
            return true;
        }

        private Unity.Mathematics.Random CreateBiomeSpawnRandom()
        {
            uint datasetCount = unchecked((uint)(biomeDatasets != null ? biomeDatasets.Length : 0));
            uint ownerId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            uint seed = Unity.Mathematics.math.hash(new Unity.Mathematics.uint4(ownerId, datasetCount, 0x51A3C4D7u, 0x2C9277B5u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }

        private static Vector2[] BuildSpawnDirectionLut()
        {
            Vector2[] directions = new Vector2[SpawnDirectionLutSize];
            for (int i = 0; i < SpawnDirectionLutSize; i++)
            {
                float angle = (Mathf.PI * 2f * i) / SpawnDirectionLutSize;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                directions[i].x = cos;
                directions[i].y = sin;
            }

            return directions;
        }

        private static Quaternion[] BuildSpawnRotationLut()
        {
            Quaternion[] rotations = new Quaternion[SpawnDirectionLutSize];
            float stepDegrees = 360f / SpawnDirectionLutSize;
            for (int i = 0; i < SpawnDirectionLutSize; i++)
                rotations[i] = Quaternion.Euler(0f, stepDegrees * i, 0f);

            return rotations;
        }

        private static Vector2 NextSpawnDirection(ref Unity.Mathematics.Random random)
        {
            return _spawnDirectionLut[(int)(random.NextUInt() & SpawnDirectionLutMask)];
        }

        private static Quaternion NextSpawnRotation(ref Unity.Mathematics.Random random)
        {
            return _spawnRotationLut[(int)(random.NextUInt() & SpawnDirectionLutMask)];
        }

        private bool IsSpawnPointValid(Transform viewTransform, Vector3 spawnPoint)
        {
            if (!_hasPlayerLookView && viewTransform == null)
                return true;

            Vector3 viewPosition = _hasPlayerLookView ? _playerLookViewPosition : viewTransform.position;
            Vector3 viewForward = _hasPlayerLookView ? _playerLookViewForward : viewTransform.forward;
            Vector3 toSpawn = spawnPoint - viewPosition;
            float sqrMagnitude = toSpawn.sqrMagnitude;
            if (sqrMagnitude <= MinimumSpawnViewDirectionMagnitudeSqr)
                return false;

            float forwardProjection = Vector3.Dot(viewForward, toSpawn);
            if (forwardProjection <= 0f)
                return true;

            return forwardProjection * forwardProjection <= SpawnVisibilityDotThresholdSqr * sqrMagnitude;
        }

        private bool TrySelectResolvedEntry(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            int biomeIndex,
            bool pressureEnabled,
            float passiveSelectionScale,
            float aggressiveSelectionScale,
            float largeThreatSelectionScale,
            DepthZoneProfile currentDepthZone,
            float depthZoneSpecialistScale,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0 || availablePoolCounts == null || availablePoolCounts.Length < resolvedEntries.Length)
                return false;

            float availableWeight = 0f;
            int fallbackIndex = -1;
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    passiveSelectionScale,
                    aggressiveSelectionScale,
                    largeThreatSelectionScale,
                    biomeIndex,
                    currentDepthZone,
                    depthZoneSpecialistScale);
                if (selectionWeight <= 0f)
                    continue;

                availableWeight += selectionWeight;
                fallbackIndex = i;
            }

            if (availableWeight <= 0f)
                return false;

            float roll = _biomeSpawnRandom.NextFloat(0f, availableWeight);
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    passiveSelectionScale,
                    aggressiveSelectionScale,
                    largeThreatSelectionScale,
                    biomeIndex,
                    currentDepthZone,
                    depthZoneSpecialistScale);
                if (selectionWeight <= 0f)
                    continue;

                roll -= selectionWeight;
                if (roll <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            if (fallbackIndex >= 0)
            {
                selectedEntry = resolvedEntries[fallbackIndex];
                return true;
            }

            return false;
        }

        private bool TrySelectResolvedEntryForSpawnPoint(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            int biomeIndex,
            bool pressureEnabled,
            float passiveSelectionScale,
            float aggressiveSelectionScale,
            float largeThreatSelectionScale,
            DepthZoneProfile currentDepthZone,
            float depthZoneSpecialistScale,
            bool requireLargeThreatClass,
            Vector3 spawnPosition,
            IVegetationThreatReadModel vegetationBridge,
            IEcosystemDirectorService ecosystemDirector,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0 || availablePoolCounts == null || availablePoolCounts.Length < resolvedEntries.Length)
                return false;

            float availableWeight = 0f;
            int fallbackIndex = -1;
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.isLargeThreat != requireLargeThreatClass || entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    passiveSelectionScale,
                    aggressiveSelectionScale,
                    largeThreatSelectionScale,
                    biomeIndex,
                    currentDepthZone,
                    depthZoneSpecialistScale);
                if (entry.isPredator && vegetationBridge != null)
                    selectionWeight *= vegetationBridge.GetSpawnWeightModifier(spawnPosition);
                if (ecosystemDirector != null)
                {
                    if (!ecosystemDirector.TryResolveSpawnWeightMultiplier(entry.archetype, spawnPosition, out float ecosystemSelectionMultiplier))
                        continue;

                    selectionWeight *= ecosystemSelectionMultiplier;
                }

                if (selectionWeight <= 0f)
                    continue;

                availableWeight += selectionWeight;
                fallbackIndex = i;
            }

            if (availableWeight <= 0f)
                return false;

            float roll = _biomeSpawnRandom.NextFloat(0f, availableWeight);
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.isLargeThreat != requireLargeThreatClass || entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    passiveSelectionScale,
                    aggressiveSelectionScale,
                    largeThreatSelectionScale,
                    biomeIndex,
                    currentDepthZone,
                    depthZoneSpecialistScale);
                if (entry.isPredator && vegetationBridge != null)
                    selectionWeight *= vegetationBridge.GetSpawnWeightModifier(spawnPosition);
                if (ecosystemDirector != null)
                {
                    if (!ecosystemDirector.TryResolveSpawnWeightMultiplier(entry.archetype, spawnPosition, out float ecosystemSelectionMultiplier))
                        continue;

                    selectionWeight *= ecosystemSelectionMultiplier;
                }

                if (selectionWeight <= 0f)
                    continue;

                roll -= selectionWeight;
                if (roll <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            if (fallbackIndex >= 0)
            {
                selectedEntry = resolvedEntries[fallbackIndex];
                return true;
            }

            return false;
        }

        private bool TrySelectResolvedHordeEntry(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0 || availablePoolCounts == null || availablePoolCounts.Length < resolvedEntries.Length)
                return false;

            int startIndex = _biomeSpawnRandom.NextInt(0, resolvedEntries.Length);
            for (int search = 0; search < resolvedEntries.Length; search++)
            {
                int index = startIndex + search;
                if (index >= resolvedEntries.Length)
                    index -= resolvedEntries.Length;

                ResolvedFaunaEntry entry = resolvedEntries[index];
                if (entry.prefab == null || entry.isLargeThreat)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[index] <= 0)
                    continue;

                selectedEntry = entry;
                return true;
            }

            return false;
        }

        private void DecrementCreatureCounters(in ActiveCreature creature)
        {
            if (_countsPerBiome == null || _biomeLookup == null || _countsPerTypePerBiome == null)
                return;

            int bi = creature.biomeIndex;
            if (bi >= 0 && bi < _countsPerBiome.Length)
                _countsPerBiome[bi]--;

            if (_biomeLookup.TryGetValue(bi, out FaunaBiomeData biomeData) &&
                _countsPerTypePerBiome.TryGetValue(biomeData, out int[] typeCounts))
            {
                int ti = creature.creatureTypeIndex;
                if (ti >= 0 && ti < typeCounts.Length)
                    typeCounts[ti]--;
            }

            DecrementChunkCount(creature.chunkCoord);
            if (creature.isPredator)
                DecrementPredatorSectorCount(creature.chunkCoord);
            if (creature.isLargeThreat)
                DecrementMacroZoneCount(creature.macroZoneCoord);
        }

        private void EnsureRuntimeStateInitialized()
        {
            if (_biomeLookup != null &&
                _countsPerTypePerBiome != null &&
                _resolvedEntriesPerBiome != null &&
                _availablePoolCountsPerBiome != null &&
                _prefabTypeIndexLookup != null &&
                _countsPerChunk != null &&
                _predatorCountsPerSector != null &&
                _largeThreatCountsPerMacroZone != null &&
                _activeCreatures != null &&
                _countsPerBiome != null &&
                _persistedFaunaRestoreScratch != null)
            {
                return;
            }

            int capacity = biomeDatasets != null ? biomeDatasets.Length : 4;
            _biomeLookup ??= new Dictionary<int, FaunaBiomeData>(capacity);
            _countsPerTypePerBiome ??= new Dictionary<FaunaBiomeData, int[]>(capacity);
            _resolvedEntriesPerBiome ??= new Dictionary<FaunaBiomeData, ResolvedFaunaEntry[]>(capacity);
            _availablePoolCountsPerBiome ??= new Dictionary<FaunaBiomeData, int[]>(capacity);
            _prefabTypeIndexLookup ??= new Dictionary<FaunaBiomeData, Dictionary<GameObject, int>>(capacity);
            _countsPerChunk ??= new Dictionary<long, int>(32);
            // COLD ALLOC: Dictionary<long,int>[32] — predator budget counts keyed by 1 km fauna sector — owner: FaunaDirector
            _predatorCountsPerSector ??= new Dictionary<long, int>(32);
            _largeThreatCountsPerMacroZone ??= new Dictionary<long, int>(16);
            // COLD ALLOC: List<EntityDataRecord>[64] — MMF-backed Tier 2 fauna restore scratch buffer — owner: FaunaDirector
            _persistedFaunaRestoreScratch ??= new List<EntityDataRecord>(64);

            _biomeLookup.Clear();
            _countsPerTypePerBiome.Clear();
            _resolvedEntriesPerBiome.Clear();
            _availablePoolCountsPerBiome.Clear();
            _prefabTypeIndexLookup.Clear();

            int maxBiomeIndex = 0;
            if (biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    FaunaBiomeData data = biomeDatasets[i];
                    if (data == null)
                        continue;

                    _biomeLookup[data.biomeIndex] = data;
                    if (data.biomeIndex > maxBiomeIndex)
                        maxBiomeIndex = data.biomeIndex;

                    int creatureCount = data.possibleCreatures != null ? data.possibleCreatures.Count : 0;
                    _countsPerTypePerBiome[data] = new int[creatureCount];
                    ResolvedFaunaEntry[] resolvedEntries = new ResolvedFaunaEntry[creatureCount];
                    _availablePoolCountsPerBiome[data] = new int[creatureCount];

                    Dictionary<GameObject, int> prefabLookup = new Dictionary<GameObject, int>(Mathf.Max(1, creatureCount));
                    if (data.possibleCreatures != null)
                    {
                        for (int creatureIndex = 0; creatureIndex < creatureCount; creatureIndex++)
                        {
                            FaunaEntry faunaEntry = data.possibleCreatures[creatureIndex];
                            GameObject resolvedPrefab = faunaEntry.GetResolvedPrefab();
                            resolvedEntries[creatureIndex] = new ResolvedFaunaEntry
                            {
                                prefab = resolvedPrefab,
                                archetype = faunaEntry.archetype,
                                speciesId = ResolveStableSpeciesId(faunaEntry.archetype, resolvedPrefab),
                                spawnWeight = faunaEntry.GetResolvedSpawnWeight(),
                                maxAlive = Mathf.Max(1, faunaEntry.GetResolvedMaxAlive()),
                                creatureTypeIndex = creatureIndex,
                                isLargeThreat = IsLargeThreatEntry(data, faunaEntry),
                                isPredator = faunaEntry.archetype != null &&
                                             (faunaEntry.archetype.isAggressive ||
                                              faunaEntry.archetype.roleType == CreatureRoleType.Hunter ||
                                              faunaEntry.archetype.roleType == CreatureRoleType.Leviathan),
                                blockedWhenPressureDisabled = ShouldBlockEntryWhenPressureDisabled(faunaEntry.archetype),
                                prefersClaustrophobicZone = DoesEntryPreferClaustrophobicZone(faunaEntry.archetype),
                                prefersThermalZone = DoesEntryPreferThermalZone(faunaEntry.archetype),
                                prefersHighPressureZone = DoesEntryPreferHighPressureZone(data, faunaEntry)
                            };

                            if (resolvedPrefab == null || prefabLookup.ContainsKey(resolvedPrefab))
                                continue;

                            prefabLookup.Add(resolvedPrefab, creatureIndex);
                        }
                    }

                    _resolvedEntriesPerBiome[data] = resolvedEntries;
                    _prefabTypeIndexLookup[data] = prefabLookup;
                }
            }

            _activeCreatures ??= new List<ActiveCreature>(Mathf.Max(4, globalMaxCount));
            if (_countsPerBiome == null || _countsPerBiome.Length < maxBiomeIndex + 1)
                _countsPerBiome = new int[maxBiomeIndex + 1];
        }

        private void InitializeDehydrationResidencyState()
        {
            if (_faunaSimulationMemory.HasResidentBuffers &&
                _faunaSimulationMemory.FreeSlots.IsCreated &&
                _dehydratedCreatureStates != null &&
                _activeDehydrationSlots != null)
            {
                return;
            }

            int slotCapacity = MaxFaunaResidencySlots;

            _faunaSimulationMemory.Allocate(slotCapacity);
            _faunaSimulationEngine?.Initialize(slotCapacity);
            if (_dehydratedCreatureStates == null || _dehydratedCreatureStates.Length != slotCapacity)
                _dehydratedCreatureStates = new FaunaResidencyState[slotCapacity];
            if (_activeDehydrationSlots == null || _activeDehydrationSlots.Length != slotCapacity)
                _activeDehydrationSlots = new int[slotCapacity];

            _activeDehydrationSlotCount = 0;
        }

        private void DisposeDehydrationResidencyState()
        {
            JobHandle disposeDependency = CancelResidentDataOnlySimulationForDispose();

            _faunaSimulationEngine?.Shutdown();
            _faunaSimulationMemory.Dispose(disposeDependency);
            JobHandle.ScheduleBatchedJobs();

            _dehydratedCreatureStates = null;
            _activeDehydrationSlots = null;
            _activeDehydrationSlotCount = 0;
            _residentDataOnlyLodScheduled = false;
            _residentDataOnlyLodDeltaAccumulator = 0f;
        }

        private JobHandle CancelResidentDataOnlySimulationForDispose()
        {
            if (!_residentDataOnlyLodScheduled)
                return default;

            JobHandle disposeDependency = _residentDataOnlyLodHandle;
            _residentDataOnlyLodHandle = default;
            _residentDataOnlyLodScheduled = false;
            return disposeDependency;
        }

        private void ResetDehydrationResidencyState()
        {
            CompleteResidentDataOnlySimulation(forceComplete: true);

            _faunaSimulationMemory.Reset();

            if (_dehydratedCreatureStates != null)
                System.Array.Clear(_dehydratedCreatureStates, 0, _dehydratedCreatureStates.Length);

            if (_activeDehydrationSlots != null)
                System.Array.Clear(_activeDehydrationSlots, 0, _activeDehydrationSlots.Length);

            _activeDehydrationSlotCount = 0;
            _residentDataOnlyLodScheduled = false;
            _residentDataOnlyLodDeltaAccumulator = 0f;
        }

        /// <summary>
        /// Ð˜Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚Ð¸Ñ€ÑƒÐµÑ‚ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¸ Ð¿Ñ€Ð¸ Ð´Ð¾Ð±Ð°Ð²Ð»ÐµÐ½Ð¸Ð¸ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ ForceSpawnHorde Ð´Ð»Ñ ÐºÐ¾Ñ€Ñ€ÐµÐºÑ‚Ð½Ð¾Ð³Ð¾ accounting.
        /// O(1). Zero GC.
        /// </summary>
        private void IncrementCreatureCounters(int biomeIdx, int typeIndex,
                                                FaunaBiomeData biomeData)
        {
            if (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                _countsPerBiome[biomeIdx]++;

            if (biomeData != null &&
                _countsPerTypePerBiome.TryGetValue(biomeData, out int[] typeCounts))
            {
                if (typeIndex >= 0 && typeIndex < typeCounts.Length)
                    typeCounts[typeIndex]++;
            }
        }

        /// <summary>
        /// ÐÐ°Ñ…Ð¾Ð´Ð¸Ñ‚ Ð¸Ð½Ð´ÐµÐºÑ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð² possibleCreatures Ð¿Ð¾ Ð¿Ñ€ÐµÑ„Ð°Ð±Ñƒ.
        /// ReferenceEquals â€” zero GC. O(n) Ð¿Ð¾ Ñ‚Ð¸Ð¿Ð°Ð¼ (Ð¾Ð±Ñ‹Ñ‡Ð½Ð¾ 3-5).
        /// </summary>
        private int FindCreatureTypeIndex(FaunaBiomeData biomeData, GameObject prefab)
        {
            if (biomeData == null || prefab == null)
                return -1;

            if (_prefabTypeIndexLookup != null &&
                _prefabTypeIndexLookup.TryGetValue(biomeData, out Dictionary<GameObject, int> prefabLookup) &&
                prefabLookup != null &&
                prefabLookup.TryGetValue(prefab, out int cachedIndex))
            {
                return cachedIndex;
            }

            return -1;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SWAP REMOVE â€” O(1) ÑƒÐ´Ð°Ð»ÐµÐ½Ð¸Ðµ Ð¸Ð· List
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Swap-Remove: Ð¼ÐµÐ½ÑÐµÑ‚ ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚ Ñ Ð¿Ð¾ÑÐ»ÐµÐ´Ð½Ð¸Ð¼, ÑƒÐ´Ð°Ð»ÑÐµÑ‚ Ð¿Ð¾ÑÐ»ÐµÐ´Ð½Ð¸Ð¹.
        /// O(1) Ð²Ð¼ÐµÑÑ‚Ð¾ O(n). ÐŸÐ¾Ñ€ÑÐ´Ð¾Ðº Ð½Ðµ ÑÐ¾Ñ…Ñ€Ð°Ð½ÑÐµÑ‚ÑÑ (Ð½Ðµ Ð²Ð°Ð¶Ð½Ð¾ Ð´Ð»Ñ Ð½Ð°Ñ).
        /// Zero GC: List.RemoveAt(last) Ð½Ðµ ÑÐ´Ð²Ð¸Ð³Ð°ÐµÑ‚ Ð¼Ð°ÑÑÐ¸Ð².
        /// </summary>
        private void SwapRemoveAt(int index)
        {
            int lastIndex = _activeCreatures.Count - 1;

            if (index < lastIndex)
            {
                _activeCreatures[index] = _activeCreatures[lastIndex];
            }

            _activeCreatures.RemoveAt(lastIndex);
        }

        private int GetTrackedCreaturePopulationCount()
        {
            int activeCount = _activeCreatures != null ? _activeCreatures.Count : 0;
            return activeCount + _activeDehydrationSlotCount + _persistedTier2FaunaCount;
        }

        private int AllocateDehydrationSlot()
        {
            if (!_faunaSimulationMemory.FreeSlots.IsCreated || !_faunaSimulationMemory.FreeSlots.TryDequeue(out int slotIndex))
                return InvalidDehydrationSlotIndex;

            return slotIndex;
        }

        private void ReleaseDehydrationSlot(int slotIndex)
        {
            if (slotIndex < 0 ||
                !_faunaSimulationMemory.HasPoolSlot(slotIndex) ||
                !_faunaSimulationMemory.FreeSlots.IsCreated ||
                _dehydratedCreatureStates == null ||
                slotIndex >= _dehydratedCreatureStates.Length)
            {
                return;
            }

            if (!_dehydratedCreatureStates[slotIndex].isResident)
                return;

            RemoveActiveDehydrationSlot(slotIndex);
            _faunaSimulationMemory.TryClearSlot(slotIndex);
            _dehydratedCreatureStates[slotIndex] = default;
            _faunaSimulationMemory.FreeSlots.Enqueue(slotIndex);
        }

        private void AddActiveDehydrationSlot(int slotIndex)
        {
            if (slotIndex < 0 || _activeDehydrationSlots == null)
                return;

            for (int i = 0; i < _activeDehydrationSlotCount; i++)
            {
                if (_activeDehydrationSlots[i] == slotIndex)
                    return;
            }

            if (_activeDehydrationSlotCount >= _activeDehydrationSlots.Length)
                return;

            _activeDehydrationSlots[_activeDehydrationSlotCount] = slotIndex;
            _activeDehydrationSlotCount++;
        }

        private void RemoveActiveDehydrationSlotAt(int index)
        {
            if (_activeDehydrationSlots == null || index < 0 || index >= _activeDehydrationSlotCount)
                return;

            int lastIndex = _activeDehydrationSlotCount - 1;
            _activeDehydrationSlots[index] = _activeDehydrationSlots[lastIndex];
            _activeDehydrationSlots[lastIndex] = 0;
            _activeDehydrationSlotCount = lastIndex;
        }

        private void RemoveActiveDehydrationSlot(int slotIndex)
        {
            if (_activeDehydrationSlots == null || slotIndex < 0)
                return;

            for (int i = 0; i < _activeDehydrationSlotCount; i++)
            {
                if (_activeDehydrationSlots[i] != slotIndex)
                    continue;

                RemoveActiveDehydrationSlotAt(i);
                return;
            }
        }

        private void CompleteResidentDataOnlySimulation(bool forceComplete)
        {
            if (!_residentDataOnlyLodScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _residentDataOnlyLodHandle, forceComplete))
                return;

            _residentDataOnlyLodScheduled = false;

            if (_dehydratedCreatureStates == null)
                return;

            for (int i = 0; i < _activeDehydrationSlotCount; i++)
            {
                int slotIndex = _activeDehydrationSlots[i];
                if (slotIndex < 0 || slotIndex >= _dehydratedCreatureStates.Length)
                    continue;

                FaunaResidencyState state = _dehydratedCreatureStates[slotIndex];
                if (!state.isResident || !state.isDehydrated)
                    continue;

                if (!_faunaSimulationMemory.TryReadLinearVelocity(slotIndex, out float3 velocity))
                    continue;

                state.linearVelocity = new Vector3(velocity.x, velocity.y, velocity.z);
                _dehydratedCreatureStates[slotIndex] = state;
            }
        }

        private void AccumulateResidentDataOnlyLodDelta(float deltaTime)
        {
            _residentDataOnlyLodDeltaAccumulator = math.min(
                _residentDataOnlyLodDeltaAccumulator + math.max(0f, deltaTime),
                ResidentDataOnlyLodMaxAccumulatedDeltaSeconds);
        }

        private void TryScheduleResidentDataOnlySimulation(in AbsoluteUniversePosition playerAup)
        {
            if (_residentDataOnlyLodDeltaAccumulator < ResidentDataOnlyLodTickIntervalSeconds)
                return;

            float lodDeltaTime = _residentDataOnlyLodDeltaAccumulator;
            if (ScheduleResidentDataOnlySimulation(in playerAup, lodDeltaTime))
                _residentDataOnlyLodDeltaAccumulator = 0f;
        }

        private bool ScheduleResidentDataOnlySimulation(in AbsoluteUniversePosition playerAup, float deltaTime)
        {
            if (_residentDataOnlyLodScheduled ||
                _activeDehydrationSlotCount <= 0 ||
                !_faunaSimulationMemory.HasResidentBuffers)
            {
                return false;
            }

            if (!_faunaSimulationMemory.TryScheduleResidentDataOnlyLod(
                    _faunaSimulationEngine,
                    in playerAup,
                    deltaTime,
                    DehydrationDistanceSq,
                    HibernationDistanceSq,
                    ResidentSimulationFlag,
                    DehydratedSimulationFlag,
                    out _residentDataOnlyLodHandle))
            {
                return false;
            }

            _residentDataOnlyLodScheduled = true;
            return true;
        }

        private static bool IsApexPredatorArchetype(CreatureArchetypeData archetype, bool isLargeThreat)
        {
            return isLargeThreat &&
                   archetype != null &&
                   archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static int ResolveStableSpeciesId(CreatureArchetypeData archetype, GameObject prefabSource)
        {
            if (archetype != null)
            {
                if (archetype.faunaDataTemplate != null && archetype.faunaDataTemplate.SpeciesId != 0)
                    return archetype.faunaDataTemplate.SpeciesId;

                if (!string.IsNullOrWhiteSpace(archetype.creatureId))
                    return unchecked((int)Hecton.Localization.LocHash.Compute(archetype.creatureId)) & int.MaxValue;
            }

            if (prefabSource != null && prefabSource.TryGetComponent(out FaunaBrain faunaBrain))
            {
                int prefabSpeciesId = faunaBrain.SpeciesId;
                if (prefabSpeciesId != 0)
                    return prefabSpeciesId;
            }

            return 0;
        }

        private static uint BuildApexFaunaInstanceUid(CreatureArchetypeData archetype, in WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (!IsApexPredatorArchetype(archetype, isLargeThreat: true))
                return 0u;

            ulong persistentHash = PersistentWorldRegistry.ComputePersistentIdHash(archetype.creatureId);
            uint sectorHash = math.hash(new int4(
                macroZoneCoord.x,
                macroZoneCoord.z,
                unchecked((int)(persistentHash & 0xFFFFFFFFUL)),
                unchecked((int)(persistentHash >> 32))));
            uint sequence = sectorHash & 0x00FFFFFFu;
            if (sequence == 0u)
                sequence = 1u;

            return (ApexFaunaInstanceTypeId << 24) | sequence;
        }

        private static uint BuildStandardFaunaInstanceUid(int speciesId, int biomeIndex, WorldChunkCoordinate chunkCoord, Vector3 runtimePosition)
        {
            if (!TryResolveRuntimePositionAup(runtimePosition, out AbsoluteUniversePosition runtimeAup))
                return 0u;

            return BuildStandardFaunaInstanceUid(speciesId, biomeIndex, chunkCoord, in runtimeAup);
        }

        private static uint BuildStandardFaunaInstanceUid(
            int speciesId,
            int biomeIndex,
            WorldChunkCoordinate chunkCoord,
            in AbsoluteUniversePosition absolutePosition)
        {
            double3 absoluteMeters = absolutePosition.ToAbsoluteDouble3();
            uint positionHash = math.hash(new int4(
                RoundDoubleToIntSaturated(absoluteMeters.x * 10d),
                RoundDoubleToIntSaturated(absoluteMeters.y * 10d),
                RoundDoubleToIntSaturated(absoluteMeters.z * 10d),
                biomeIndex));
            uint sectorHash = math.hash(new int4(
                chunkCoord.x,
                chunkCoord.z,
                speciesId,
                unchecked((int)positionHash)));
            uint sequence = sectorHash & 0x00FFFFFFu;
            if (sequence == 0u)
                sequence = 1u;

            return (StandardFaunaInstanceTypeId << 24) | sequence;
        }

        private static int RoundDoubleToIntSaturated(double value)
        {
            if (double.IsNaN(value))
                return 0;

            if (value >= int.MaxValue)
                return int.MaxValue;

            if (value <= int.MinValue)
                return int.MinValue;

            return value >= 0d ? (int)(value + 0.5d) : (int)(value - 0.5d);
        }

        private IEcosystemDirectorService ResolveEcosystemDirector()
        {
            return _ecosystemDirector;
        }

        private static uint BuildActiveCreatureWatchdogFlags(bool isPredator, FaunaBrain brain)
        {
            uint flags = brain != null ? ActiveCreatureFlagHasBrain : 0u;
            if (isPredator)
                flags |= ActiveCreatureFlagPredator;

            return flags;
        }

        private bool TryBuildActiveCreatureRecord(
            GameObject instance,
            GameObject prefabSource,
            CreatureArchetypeData archetype,
            int creatureTypeIndex,
            int biomeIndex,
            WorldChunkCoordinate chunkCoord,
            WorldMacroZoneCoordinate macroZoneCoord,
            bool isLargeThreat,
            bool isPredator,
            FaunaBrain brain,
            uint uniqueInstanceUid,
            in AbsoluteUniversePosition positionAup,
            out ActiveCreature record)
        {
            record = default;
            if (instance == null || prefabSource == null)
                return false;

            int slotIndex = AllocateDehydrationSlot();
            if (slotIndex == InvalidDehydrationSlotIndex)
                return false;

            UpdateResidencySlot(slotIndex, instance, prefabSource, archetype, creatureTypeIndex, biomeIndex, chunkCoord, macroZoneCoord, isLargeThreat, isPredator, uniqueInstanceUid, in positionAup, markDehydrated: false);

            record = new ActiveCreature
            {
                gameObject = instance,
                transform = instance.transform,
                brain = brain,
                creatureTypeIndex = creatureTypeIndex,
                biomeIndex = biomeIndex,
                prefabSource = prefabSource,
                archetype = archetype,
                chunkCoord = chunkCoord,
                macroZoneCoord = macroZoneCoord,
                isLargeThreat = isLargeThreat,
                isPredator = isPredator,
                watchdogFlags = BuildActiveCreatureWatchdogFlags(isPredator, brain),
                lastKnownAup = positionAup,
                hasLastKnownAup = true,
                uniqueInstanceUid = uniqueInstanceUid,
                dehydrationSlotIndex = slotIndex
            };

            return true;
        }

        private static bool TryResolveActiveCreatureLogicAup(ref ActiveCreature creature, out AbsoluteUniversePosition positionAup)
        {
            if (creature.brain != null && creature.brain.TryResolveLogicAup(out positionAup))
            {
                creature.lastKnownAup = positionAup;
                creature.hasLastKnownAup = true;
                return true;
            }

            if (creature.hasLastKnownAup)
            {
                positionAup = creature.lastKnownAup;
                return true;
            }

            positionAup = default;
            return false;
        }

        private void UpdateResidencyStateFromActiveCreature(in ActiveCreature creature, in AbsoluteUniversePosition positionAup, bool markDehydrated)
        {
            if (creature.dehydrationSlotIndex < 0 || !_faunaSimulationMemory.HasPoolSlot(creature.dehydrationSlotIndex))
                return;

            UpdateResidencySlot(
                creature.dehydrationSlotIndex,
                creature.gameObject,
                creature.prefabSource,
                creature.archetype,
                creature.creatureTypeIndex,
                creature.biomeIndex,
                creature.chunkCoord,
                creature.macroZoneCoord,
                creature.isLargeThreat,
                creature.isPredator,
                creature.uniqueInstanceUid,
                in positionAup,
                markDehydrated);
        }

        private void UpdateResidencySlot(
            int slotIndex,
            GameObject instance,
            GameObject prefabSource,
            CreatureArchetypeData archetype,
            int creatureTypeIndex,
            int biomeIndex,
            WorldChunkCoordinate chunkCoord,
            WorldMacroZoneCoordinate macroZoneCoord,
            bool isLargeThreat,
            bool isPredator,
            uint uniqueInstanceUid,
            in AbsoluteUniversePosition positionAup,
            bool markDehydrated)
        {
            if (slotIndex < 0 ||
                _dehydratedCreatureStates == null ||
                !_faunaSimulationMemory.HasPoolSlot(slotIndex))
            {
                return;
            }

            if (!_faunaSimulationMemory.TryReadPoolSlot(slotIndex, out PoolSlotData slotData))
                return;

            slotData.BoundGuid = unchecked((ulong)(slotIndex + 1));
            WritePoolSlotPosition(ref slotData, in positionAup);
            slotData.HydrationFrame = ReadDispatcherFrame16();
            slotData.RefCount = 1;
            slotData.StateFlags = markDehydrated ? (byte)1 : (byte)0;
            slotData.LastVisibleFrame = ReadDispatcherFrame16();
            _faunaSimulationMemory.TryWritePoolSlot(slotIndex, in slotData);

            Vector3 linearVelocity = Vector3.zero;
            Vector3 angularVelocity = Vector3.zero;
            Quaternion rotation = instance != null ? instance.transform.rotation : Quaternion.identity;
            float health = archetype != null ? Mathf.Max(1f, archetype.maxHealth) : 1f;
            float hunger01 = 0f;

            if (instance != null)
            {
                if (instance.TryGetComponent(out FaunaBrain ai))
                {
                    health = ai.CurrentHealth;
                    hunger01 = ai.CurrentHunger01;
                }

                if (instance.TryGetComponent(out Rigidbody rigidbody))
                {
                    linearVelocity = rigidbody.linearVelocity;
                    angularVelocity = rigidbody.angularVelocity;
                }
            }

            _faunaSimulationMemory.TryWriteLinearVelocity(
                slotIndex,
                new float3(linearVelocity.x, linearVelocity.y, linearVelocity.z));
            byte flags = ResidentSimulationFlag;
            if (markDehydrated)
                flags |= DehydratedSimulationFlag;
            _faunaSimulationMemory.TryWriteSimulationFlag(slotIndex, flags);

            _dehydratedCreatureStates[slotIndex] = new FaunaResidencyState
            {
                prefabSource = prefabSource,
                archetype = archetype,
                rotation = rotation,
                linearVelocity = linearVelocity,
                angularVelocity = angularVelocity,
                pendingHibernationHuntTarget = default,
                health = health,
                hunger01 = hunger01,
                hibernationStartTimeSeconds = -1f,
                speciesId = ResolveStableSpeciesId(archetype, prefabSource),
                creatureTypeIndex = creatureTypeIndex,
                biomeIndex = biomeIndex,
                chunkCoord = chunkCoord,
                macroZoneCoord = macroZoneCoord,
                isLargeThreat = isLargeThreat,
                isPredator = isPredator,
                uniqueInstanceUid = uniqueInstanceUid,
                isResident = true,
                isDehydrated = markDehydrated,
                hasPendingHibernationHuntTarget = false
            };
        }

        private int HydrateResidentCreatures(in AbsoluteUniversePosition playerAup)
        {
            if (_activeDehydrationSlotCount <= 0 || _activeCreatures == null)
                return 0;

            IObjectPoolService pool = _objectPool;
            if (pool == null)
                return 0;

            int hydrated = 0;

            for (int i = _activeDehydrationSlotCount - 1; i >= 0; i--)
            {
                if (_activeCreatures.Count >= _currentEffectiveGlobalMaxCount)
                    break;

                int slotIndex = _activeDehydrationSlots[i];
                if (slotIndex < 0 || slotIndex >= _dehydratedCreatureStates.Length)
                {
                    RemoveActiveDehydrationSlotAt(i);
                    continue;
                }

                FaunaResidencyState state = _dehydratedCreatureStates[slotIndex];
                if (!state.isResident || !state.isDehydrated)
                {
                    RemoveActiveDehydrationSlotAt(i);
                    continue;
                }
                if (!_faunaSimulationMemory.TryReadPoolSlot(slotIndex, out PoolSlotData slotData))
                {
                    RemoveActiveDehydrationSlotAt(i);
                    continue;
                }

                AbsoluteUniversePosition creatureAup = ReadPoolSlotPosition(in slotData);
                if (AbsoluteUniversePosition.DistanceSq(in creatureAup, in playerAup) >= DehydrationDistanceSq)
                    continue;

                Vector3 runtimePosition = creatureAup.ToRuntimeFloat3();
                GameObject instance = pool.Spawn(state.prefabSource, runtimePosition, state.rotation, false);
                if (instance == null)
                    continue;

                if (instance.TryGetComponent(out FaunaBrain ai))
                {
                    ai.ApplyArchetype(state.archetype);
                    ai.SetSpawnPoint(runtimePosition);
                    ai.SetLogicalIdentity(state.uniqueInstanceUid);
                    ai.SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                    ai.SetHibernationHunger01(state.hunger01);
                    _faunaPresentationService?.ConfigureSpawnedCreature(ai, state.archetype, state.biomeIndex, runtimePosition, in state.chunkCoord);

                    ai.ApplyHibernationHealthSnapshot(state.health);

                    if (state.hasPendingHibernationHuntTarget)
                    {
                        ai.ForceHighPriorityHibernationHunt(state.pendingHibernationHuntTarget, state.hunger01);
                    }
                    else if (state.isPredator &&
                             state.hunger01 > HibernationStarvationHuntThreshold01 &&
                             TryResolveHibernationStarvationHuntTarget(runtimePosition, out Vector3 lateHuntTarget))
                    {
                        ai.ForceHighPriorityHibernationHunt(lateHuntTarget, state.hunger01);
                    }
                }

                if (instance.TryGetComponent(out Rigidbody rigidbody))
                {
                    IPhysicsService physicsService = _physicsService;
                    if (physicsService != null)
                    {
                        physicsService.QueueLinearVelocitySet(rigidbody, state.linearVelocity);
                        physicsService.QueueAngularVelocitySet(rigidbody, state.angularVelocity);
                    }
                }

                ActiveCreature creature = new ActiveCreature
                {
                    gameObject = instance,
                    transform = instance.transform,
                    brain = ai,
                    creatureTypeIndex = state.creatureTypeIndex,
                    biomeIndex = state.biomeIndex,
                    prefabSource = state.prefabSource,
                    archetype = state.archetype,
                    chunkCoord = state.chunkCoord,
                    macroZoneCoord = state.macroZoneCoord,
                    isLargeThreat = state.isLargeThreat,
                    isPredator = state.isPredator,
                    watchdogFlags = BuildActiveCreatureWatchdogFlags(state.isPredator, ai),
                    lastKnownAup = creatureAup,
                    hasLastKnownAup = true,
                    uniqueInstanceUid = state.uniqueInstanceUid,
                    dehydrationSlotIndex = slotIndex
                };

                _activeCreatures.Add(creature);
                state.isDehydrated = false;
                state.hibernationStartTimeSeconds = -1f;
                state.pendingHibernationHuntTarget = default;
                state.hasPendingHibernationHuntTarget = false;
                _dehydratedCreatureStates[slotIndex] = state;

                if (!_faunaSimulationMemory.TryReadPoolSlot(slotIndex, out slotData))
                {
                    RemoveActiveDehydrationSlotAt(i);
                    continue;
                }

                slotData.StateFlags = 0;
                slotData.HydrationFrame = ReadDispatcherFrame16();
                slotData.LastVisibleFrame = ReadDispatcherFrame16();
                _faunaSimulationMemory.TryWritePoolSlot(slotIndex, in slotData);
                _faunaSimulationMemory.TryWriteSimulationFlag(slotIndex, ResidentSimulationFlag);

                RemoveActiveDehydrationSlotAt(i);
                hydrated++;
            }

            return hydrated;
        }

        private void QueueResidentCreatureHydration(in AbsoluteUniversePosition playerAup)
        {
            _pendingResidentCreatureHydrationAup = playerAup;
            _pendingResidentCreatureHydration = true;
        }

        private void FlushPendingResidentCreatureHydration()
        {
            if (!_pendingResidentCreatureHydration)
                return;

            _pendingResidentCreatureHydration = false;
            HydrateResidentCreatures(in _pendingResidentCreatureHydrationAup);
        }

        private void OffloadPersistedTier2Fauna(in AbsoluteUniversePosition playerAup)
        {
            if (_activeDehydrationSlotCount <= 0)
                return;

            IFaunaPersistentWorldStateService registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            for (int i = _activeDehydrationSlotCount - 1; i >= 0; i--)
            {
                int slotIndex = _activeDehydrationSlots[i];
                if (slotIndex < 0 ||
                    slotIndex >= _dehydratedCreatureStates.Length ||
                    !_faunaSimulationMemory.HasPoolSlot(slotIndex))
                {
                    continue;
                }

                FaunaResidencyState state = _dehydratedCreatureStates[slotIndex];
                if (!state.isResident || !state.isDehydrated)
                    continue;

                if (!_faunaSimulationMemory.TryReadPoolSlot(slotIndex, out PoolSlotData slotData))
                    continue;

                AbsoluteUniversePosition creatureAup = ReadPoolSlotPosition(in slotData);
                if (AbsoluteUniversePosition.DistanceSq(in creatureAup, in playerAup) <= HibernationDistanceSq)
                    continue;

                uint instanceUid = state.uniqueInstanceUid != 0u
                    ? state.uniqueInstanceUid
                    : BuildStandardFaunaInstanceUid(state.speciesId, state.biomeIndex, state.chunkCoord, in creatureAup);
                EntityDataRecord cachedState = PersistentWorldRegistry.CreateFaunaHibernationState(
                    instanceUid,
                    state.speciesId,
                    state.health,
                    in creatureAup,
                    state.isLargeThreat,
                    state.isPredator,
                    ReadDispatcherTimeSeconds(),
                    state.hunger01);
                if (!registry.TryCacheFaunaHibernationState(in cachedState))
                    continue;

                ReleaseDehydrationSlot(slotIndex);
                _persistedTier2FaunaCount++;
            }
        }

        private void ApplyThermalApexMigrationToPersistedTier2Fauna()
        {
            float nowSeconds = ReadDispatcherTimeSeconds();
            if (nowSeconds < _nextThermalApexMigrationTime)
                return;

            _nextThermalApexMigrationTime = nowSeconds + ThermalApexMigrationIntervalSeconds;
            IThermodynamicsService thermalManager = _thermalRuntime;
            if (thermalManager == null ||
                !thermalManager.TryResolveApexMigrationThermalAttractor(out Vector3 attractorPosition, out float strength01))
            {
                return;
            }

            IFaunaPersistentWorldStateService registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            float stepMeters = ThermalApexMigrationStepMeters * Mathf.Max(0.25f, Mathf.Clamp01(strength01));
            if (!TryResolveRuntimePositionAup(attractorPosition, out AbsoluteUniversePosition attractorAup))
                return;

            registry.MigrateApexFaunaHibernationStatesToward(in attractorAup, ThermalApexMigrationRadiusMeters, stepMeters);
        }

        private void RestorePersistedTier2Fauna(in AbsoluteUniversePosition playerAup)
        {
            IFaunaPersistentWorldStateService registry = _persistentWorldRegistry;
            if (registry == null)
                return;

            if (_persistedFaunaRestoreScratch == null)
                return;

            _persistedFaunaRestoreScratch.Clear();
            int restoredCount = registry.ConsumeCachedFaunaHibernationStates(in playerAup, HibernationDistanceMeters, _persistedFaunaRestoreScratch);
            if (restoredCount <= 0)
                return;

            for (int i = 0; i < _persistedFaunaRestoreScratch.Count; i++)
            {
                EntityDataRecord cachedState = _persistedFaunaRestoreScratch[i];
                FaunaHibernationRestoreResult restoreResult = BuildPurgeHibernationRestoreResult(in cachedState, ReadDispatcherTimeSeconds());
                if (!TryRestorePersistedTier2FaunaState(in cachedState, in restoreResult))
                {
                    registry.TryCacheFaunaHibernationState(in cachedState);
                    continue;
                }

                if (_persistedTier2FaunaCount > 0)
                    _persistedTier2FaunaCount--;
            }
        }

        private static FaunaHibernationRestoreResult BuildPurgeHibernationRestoreResult(in EntityDataRecord cachedState, float currentTimeSeconds)
        {
            float savedHunger01 = PersistentWorldRegistry.GetFaunaHibernationHunger01(in cachedState);
            float sleepStartTimeSeconds = PersistentWorldRegistry.GetFaunaHibernationSleepStartTimeSeconds(in cachedState);
            float timeAsleepSeconds = math.max(0f, currentTimeSeconds - sleepStartTimeSeconds);
            float restoredHunger01 = math.saturate(savedHunger01 + (HibernationHungerCatchUpRatePerSecond * timeAsleepSeconds));
            return new FaunaHibernationRestoreResult
            {
                Health = PersistentWorldRegistry.GetFaunaHibernationHealth(in cachedState),
                Hunger01 = restoredHunger01
            };
        }

        private bool TryRestorePersistedTier2FaunaState(in EntityDataRecord cachedState, in FaunaHibernationRestoreResult restoreResult)
        {
            if (!PersistentWorldRegistry.IsFaunaHibernationState(in cachedState))
                return false;

            int speciesId = PersistentWorldRegistry.GetFaunaHibernationSpeciesId(in cachedState);
            if (speciesId == 0)
                return false;

            if (!TryResolvePersistedTier2Entry(speciesId, out FaunaBiomeData biomeData, out ResolvedFaunaEntry entry))
                return false;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in cachedState.Position);
            Vector3 runtimePosition = position.ToRuntimeFloat3();
            WorldChunkCoordinate chunkCoord = WorldChunkCoordinate.FromWorldPosition(runtimePosition, _runtimeChunkSize);
            WorldMacroZoneCoordinate macroZoneCoord = WorldMacroZoneCoordinate.FromWorldPosition(runtimePosition, _runtimeMacroZoneSize);
            bool isLargeThreat = PersistentWorldRegistry.GetFaunaHibernationLargeThreatFlag(in cachedState) || entry.isLargeThreat;
            bool isPredator = PersistentWorldRegistry.GetFaunaHibernationPredatorFlag(in cachedState) || entry.isPredator;

            int slotIndex = AllocateDehydrationSlot();
            if (slotIndex == InvalidDehydrationSlotIndex)
                return false;

            UpdateResidencySlot(
                slotIndex,
                null,
                entry.prefab,
                entry.archetype,
                entry.creatureTypeIndex,
                biomeData.biomeIndex,
                chunkCoord,
                macroZoneCoord,
                isLargeThreat,
                isPredator,
                cachedState.InstanceUid,
                in position,
                markDehydrated: true);

            FaunaResidencyState restoredState = _dehydratedCreatureStates[slotIndex];
            restoredState.health = restoreResult.Health;
            restoredState.hunger01 = restoreResult.Hunger01;
            restoredState.hibernationStartTimeSeconds = -1f;
            restoredState.speciesId = speciesId;
            restoredState.isLargeThreat = isLargeThreat;
            restoredState.isPredator = isPredator;
            if (isPredator &&
                restoreResult.Hunger01 > HibernationStarvationHuntThreshold01 &&
                TryResolveHibernationStarvationHuntTarget(runtimePosition, out Vector3 huntTarget))
            {
                restoredState.pendingHibernationHuntTarget = huntTarget;
                restoredState.hasPendingHibernationHuntTarget = true;
            }
            else
            {
                restoredState.pendingHibernationHuntTarget = default;
                restoredState.hasPendingHibernationHuntTarget = false;
            }

            _dehydratedCreatureStates[slotIndex] = restoredState;
            AddActiveDehydrationSlot(slotIndex);
            return true;
        }

        private bool TryResolveHibernationStarvationHuntTarget(Vector3 runtimePosition, out Vector3 huntTarget)
        {
            huntTarget = default;
            IEcosystemDirectorService ecosystemDirector = ResolveEcosystemDirector();
            return ecosystemDirector != null && ecosystemDirector.TryResolveNearestOrganicMass(runtimePosition, out huntTarget);
        }

        private bool TryResolvePersistedTier2Entry(int speciesId, out FaunaBiomeData biomeData, out ResolvedFaunaEntry entry)
        {
            biomeData = null;
            entry = default;
            if (_resolvedEntriesPerBiome == null || biomeDatasets == null)
                return false;

            for (int i = 0; i < biomeDatasets.Length; i++)
            {
                FaunaBiomeData candidateBiome = biomeDatasets[i];
                if (candidateBiome == null ||
                    !_resolvedEntriesPerBiome.TryGetValue(candidateBiome, out ResolvedFaunaEntry[] resolvedEntries) ||
                    resolvedEntries == null)
                {
                    continue;
                }

                for (int entryIndex = 0; entryIndex < resolvedEntries.Length; entryIndex++)
                {
                    ResolvedFaunaEntry candidateEntry = resolvedEntries[entryIndex];
                    if (candidateEntry.prefab == null || candidateEntry.speciesId != speciesId)
                        continue;

                    biomeData = candidateBiome;
                    entry = candidateEntry;
                    return true;
                }
            }

            return false;
        }

        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            CompleteResidentDataOnlySimulation(forceComplete: true);
            ref ProceduralWorldStateDTO dto = ref data.proceduralWorldState;
            dto.EnsureCapacity();

            int savedCount = 0;
            for (int i = 0; i < _activeDehydrationSlotCount; i++)
            {
                if (savedCount >= ProceduralWorldStateDTO.MaxHibernatedFaunaStates)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(MaxHibernatedFaunaStatesWarning, this);
#endif
                    break;
                }

                int slotIndex = _activeDehydrationSlots[i];
                if (!TryBuildHibernatedFaunaState(slotIndex, out HibernatedFaunaStateDTO savedState))
                    continue;

                dto.hibernatedFaunaStates[savedCount] = savedState;
                savedCount++;
            }

            dto.hibernatedFaunaCount = savedCount;
        }

        public void LoadFromSaveData(SaveData data)
        {
            EnsureRuntimeStateInitialized();
            InitializeDehydrationResidencyState();
            DespawnAll();

            if (data == null)
                return;

            ProceduralWorldStateDTO dto = data.proceduralWorldState;
            if (dto.hibernatedFaunaStates == null)
                return;

            int restoreCount = Mathf.Min(dto.hibernatedFaunaCount, dto.hibernatedFaunaStates.Length);
            for (int i = 0; i < restoreCount; i++)
                TryRestoreHibernatedFaunaState(in dto.hibernatedFaunaStates[i]);
        }

        private bool TryBuildHibernatedFaunaState(int slotIndex, out HibernatedFaunaStateDTO savedState)
        {
            savedState = default;
            if (slotIndex < 0 ||
                _dehydratedCreatureStates == null ||
                slotIndex >= _dehydratedCreatureStates.Length ||
                !_faunaSimulationMemory.HasPoolSlot(slotIndex))
            {
                return false;
            }

            FaunaResidencyState state = _dehydratedCreatureStates[slotIndex];
            if (!state.isResident || !state.isDehydrated || state.prefabSource == null || state.archetype == null)
                return false;

            if (IsApexPredatorArchetype(state.archetype, state.isLargeThreat))
                return false;

            int speciesId = state.speciesId != 0 ? state.speciesId : ResolveStableSpeciesId(state.archetype, state.prefabSource);
            if (speciesId == 0)
                return false;

            if (!_faunaSimulationMemory.TryReadPoolSlot(slotIndex, out PoolSlotData slotData))
                return false;

            AbsoluteUniversePosition position = ReadPoolSlotPosition(in slotData);
            savedState = new HibernatedFaunaStateDTO
            {
                speciesId = speciesId,
                biomeIndex = state.biomeIndex,
                creatureTypeIndex = state.creatureTypeIndex,
                health = state.health,
                position = position.ToAlignedBlit(),
                rotationX = state.rotation.x,
                rotationY = state.rotation.y,
                rotationZ = state.rotation.z,
                rotationW = state.rotation.w,
                linearVelocityX = state.linearVelocity.x,
                linearVelocityY = state.linearVelocity.y,
                linearVelocityZ = state.linearVelocity.z,
                angularVelocityX = state.angularVelocity.x,
                angularVelocityY = state.angularVelocity.y,
                angularVelocityZ = state.angularVelocity.z,
                uniqueInstanceUid = state.uniqueInstanceUid,
                flags = state.isLargeThreat ? HibernatedFaunaStateDTO.FlagLargeThreat : (byte)0
            };
            return true;
        }

        private bool TryRestoreHibernatedFaunaState(in HibernatedFaunaStateDTO savedState)
        {
            if (savedState.speciesId == 0)
                return false;

            if (!TryResolveHibernatedEntry(savedState.biomeIndex, savedState.creatureTypeIndex, savedState.speciesId, out FaunaBiomeData biomeData, out ResolvedFaunaEntry entry))
                return false;

            if (entry.prefab == null || entry.archetype == null)
                return false;

            int slotIndex = AllocateDehydrationSlot();
            if (slotIndex == InvalidDehydrationSlotIndex)
                return false;

            AbsoluteUniversePosition position = AbsoluteUniversePosition.FromAlignedBlit(in savedState.position);
            Vector3 runtimePosition = position.ToRuntimeFloat3();
            WorldChunkCoordinate chunkCoord = WorldChunkCoordinate.FromWorldPosition(runtimePosition, _runtimeChunkSize);
            WorldMacroZoneCoordinate macroZoneCoord = WorldMacroZoneCoordinate.FromWorldPosition(runtimePosition, _runtimeMacroZoneSize);
            bool isLargeThreat = (savedState.flags & HibernatedFaunaStateDTO.FlagLargeThreat) != 0 || entry.isLargeThreat;
            uint uniqueInstanceUid = savedState.uniqueInstanceUid != 0u
                ? savedState.uniqueInstanceUid
                : BuildStandardFaunaInstanceUid(savedState.speciesId, biomeData.biomeIndex, chunkCoord, in position);

            UpdateResidencySlot(
                slotIndex,
                null,
                entry.prefab,
                entry.archetype,
                entry.creatureTypeIndex,
                biomeData.biomeIndex,
                chunkCoord,
                macroZoneCoord,
                isLargeThreat,
                entry.isPredator,
                uniqueInstanceUid,
                in position,
                markDehydrated: true);

            FaunaResidencyState restoredState = _dehydratedCreatureStates[slotIndex];
            restoredState.rotation = new Quaternion(savedState.rotationX, savedState.rotationY, savedState.rotationZ, savedState.rotationW);
            restoredState.linearVelocity = new Vector3(savedState.linearVelocityX, savedState.linearVelocityY, savedState.linearVelocityZ);
            restoredState.angularVelocity = new Vector3(savedState.angularVelocityX, savedState.angularVelocityY, savedState.angularVelocityZ);
            restoredState.health = savedState.health;
            restoredState.hibernationStartTimeSeconds = -1f;
            restoredState.speciesId = savedState.speciesId;
            restoredState.isLargeThreat = isLargeThreat;
            restoredState.isPredator = entry.isPredator;
            _dehydratedCreatureStates[slotIndex] = restoredState;

            _faunaSimulationMemory.TryWriteLinearVelocity(
                slotIndex,
                new float3(
                    savedState.linearVelocityX,
                    savedState.linearVelocityY,
                    savedState.linearVelocityZ));

            AddActiveDehydrationSlot(slotIndex);
            IncrementCreatureCounters(biomeData.biomeIndex, entry.creatureTypeIndex, biomeData);
            IncrementChunkCount(chunkCoord);
            if (entry.isPredator)
                IncrementPredatorSectorCount(chunkCoord);
            if (isLargeThreat)
                IncrementMacroZoneCount(macroZoneCoord);
            return true;
        }

        private bool TryResolveHibernatedEntry(
            int biomeIndex,
            int creatureTypeIndex,
            int speciesId,
            out FaunaBiomeData biomeData,
            out ResolvedFaunaEntry entry)
        {
            biomeData = null;
            entry = default;
            if (_biomeLookup == null ||
                _resolvedEntriesPerBiome == null ||
                !_biomeLookup.TryGetValue(biomeIndex, out biomeData) ||
                biomeData == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out ResolvedFaunaEntry[] resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0)
            {
                return false;
            }

            if (creatureTypeIndex >= 0 && creatureTypeIndex < resolvedEntries.Length)
            {
                ResolvedFaunaEntry directEntry = resolvedEntries[creatureTypeIndex];
                if (directEntry.prefab != null && directEntry.speciesId == speciesId)
                {
                    entry = directEntry;
                    return true;
                }
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry candidate = resolvedEntries[i];
                if (candidate.prefab == null || candidate.speciesId != speciesId)
                    continue;

                entry = candidate;
                return true;
            }

            return false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PLAYER LOOKUP
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð›ÐµÐ½Ð¸Ð²Ñ‹Ð¹ Ð¿Ð¾Ð¸ÑÐº Ð¸Ð³Ñ€Ð¾ÐºÐ° Ð¿Ð¾ Ñ‚ÐµÐ³Ñƒ "Player".
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð· Ð¿Ñ€Ð¸ OnEnable Ð¸Ð»Ð¸ ÐµÑÐ»Ð¸ ÑÑÑ‹Ð»ÐºÐ° Ð¿Ð¾Ñ‚ÐµÑ€ÑÐ½Ð°.
        /// </summary>
        private void FindPlayer(bool force = false)
        {
            if (_playerTransform != null)
                return;

            float nowSeconds = ReadDispatcherTimeSeconds();
            if (!force && nowSeconds < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = nowSeconds + PlayerResolveRetryInterval;
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_playerTransform != null)
            {
                _nextPlayerResolveTime = float.NegativeInfinity;
                ResolvePlayerViewTransform();
            }
        }

        private void ResolvePlayerViewTransform()
        {
            if (_playerTransform == null)
            {
                _playerViewTransform = null;
                _hasPlayerLookView = false;
                return;
            }

            _hasPlayerLookView = false;
            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null)
            {
                PlayerLookState lookState = runtimeContext.LookState;
                float aimForwardLengthSq = math.lengthsq(lookState.AimForward);
                if ((lookState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                    aimForwardLengthSq > MinimumSpawnViewDirectionMagnitudeSqr)
                {
                    _playerLookViewPosition = (Vector3)lookState.EyePosition;
                    _playerLookViewForward = (Vector3)(lookState.AimForward * math.rsqrt(math.max(aimForwardLengthSq, MinimumSpawnViewDirectionMagnitudeSqr)));
                    _hasPlayerLookView = true;
                }
            }

            _playerViewTransform = _playerTransform;
        }

        private bool TryResolvePlayerLogicPose(out Vector3 playerPosition, out AbsoluteUniversePosition playerAup)
        {
            if (TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                playerAup = runtimeContext.MovementState.PredictedAup;
                float3 runtimePosition = playerAup.ToRuntimeFloat3();
                playerPosition = new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
                return true;
            }

            playerPosition = default;
            playerAup = default;
            return false;
        }

        private static bool TryResolveRuntimePositionAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 runtime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(runtime)))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            AbsoluteUniversePosition originAup = AbsoluteUniversePosition.FromAbsolutePosition(origin);
            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtime.x, runtime.y, runtime.z));
            return positionAup.IsFinite();
        }

        private static float ReadDispatcherTimeSeconds()
        {
            SystemDispatcher dispatcher = SystemDispatcher.ActiveRuntimeInstance;
            double seconds = dispatcher != null ? dispatcher.DilatedTimeSeconds : 0d;
            if (!math.isfinite(seconds) || seconds <= 0d)
                return 0f;

            return seconds > float.MaxValue ? float.MaxValue : (float)seconds;
        }

        private static ushort ReadDispatcherFrame16()
        {
            return unchecked((ushort)ReadDispatcherFrameId());
        }

        private static int ReadDispatcherFrameInt()
        {
            return unchecked((int)ReadDispatcherFrameId());
        }

        private static uint ReadDispatcherFrameId()
        {
            uint frame = TimeSliceScheduler.CurrentFrameId;
            return frame != 0u ? frame : 1u;
        }

        private bool TryResolveCachedPlayerRuntimeContext(out PlayerRuntimeContext runtimeContext)
        {
            int frame = ReadDispatcherFrameInt();
            if (_playerRuntimeContextCacheFrame != frame)
            {
                _playerRuntimeContextCacheFrame = frame;
                _playerRuntimeContextCacheValid =
                    PlayerRuntimeContextService.TryGetActiveRuntimeContext(out _playerRuntimeContextCache) &&
                    _playerRuntimeContextCache != null;
                if (!_playerRuntimeContextCacheValid)
                    _playerRuntimeContextCache = null;
            }

            runtimeContext = _playerRuntimeContextCache;
            return _playerRuntimeContextCacheValid;
        }

        private void InvalidatePlayerRuntimeContextCache()
        {
            _playerRuntimeContextCacheFrame = -1;
            _playerRuntimeContextCacheValid = false;
            _playerRuntimeContextCache = null;
        }

        private void ResolveSpawnRegistry()
        {
            if (spawnRegistry == null)
                WorldRuntimeReferenceUtility.TryResolveWorldFaunaSpawnRegistry(ref spawnRegistry);

            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void ResolveProceduralStateRegistry()
        {
            if (proceduralStateRegistry == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralStateRegistry(ref proceduralStateRegistry);

            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void ResolveBiomeMatrixDirector()
        {
            if (biomeMatrixDirector == null)
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
        }

        private void ResolveWorldZoneDirector()
        {
            if (worldZoneDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
        }

        private void ResolveDepthZoneDirector()
        {
            if (_depthZoneReadModel == null)
                _depthZoneReadModel = GlobalRegistry.DepthZoneReadModel;

            if (_depthZoneReadModel == null && depthZoneDirector != null)
                _depthZoneReadModel = depthZoneDirector;
        }

        private void ResolveVegetationThreatBridge()
        {
            if (_vegetationThreatBridge == null)
                _vegetationThreatBridge = GlobalRegistry.VegetationThreat;
        }

        private int ResolveEffectiveGlobalMaxCount(WorldProceduralFaunaMood faunaMood, DepthZoneProfile depthZone)
        {
            return Mathf.Max(1, Mathf.RoundToInt(
                _runtimeGlobalMaxCount *
                ResolveGlobalBudgetScale(faunaMood) *
                ResolveDepthZoneBudgetScale(depthZone) *
                _adaptiveGlobalBudgetScale));
        }

        private int ResolveEffectiveSpawnsPerTick(WorldProceduralFaunaMood faunaMood, DepthZoneProfile depthZone)
        {
            return Mathf.Max(1, Mathf.RoundToInt(
                _runtimeMaxSpawnsPerTick *
                ResolveGlobalBudgetScale(faunaMood) *
                ResolveDepthZoneBudgetScale(depthZone) *
                _adaptiveSpawnBudgetScale));
        }

        private int ResolveEffectiveBiomeMaxCount(FaunaBiomeData biomeData, WorldProceduralFaunaMood faunaMood, DepthZoneProfile depthZone)
        {
            if (biomeData == null)
                return 0;

            return Mathf.Max(1, Mathf.RoundToInt(
                biomeData.biomeMaxCreatures *
                ResolveBiomeCapScale(faunaMood) *
                ResolveDepthZoneBudgetScale(depthZone) *
                _adaptiveBiomeBudgetScale));
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!enableAdaptivePerfBudget)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            IDynamicResolutionRuntime scaler = _dynamicResolutionRuntime;
            if (scaler == null)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            float renderScale = Mathf.Clamp01(scaler.CurrentRenderScale01);
            float normalized = Mathf.Clamp01(
                (renderScale - adaptiveBudgetFloorRenderScale) /
                Mathf.Max(0.0001f, 1f - adaptiveBudgetFloorRenderScale));

            ApplyAdaptiveBudgetResponse(renderScale, normalized);
        }

        private void ApplyAdaptiveBudgetResponse(float renderScale, float normalized)
        {
            _adaptiveBudgetNormalized = normalized;
            _adaptiveGlobalBudgetScale = math.lerp(adaptiveGlobalFaunaBudgetFloor, 1f, normalized);
            _adaptiveBiomeBudgetScale = math.lerp(adaptiveBiomeCapBudgetFloor, 1f, normalized);
            _adaptiveSpawnBudgetScale = math.lerp(adaptiveSpawnBurstBudgetFloor, 1f, normalized);

#if UNITY_EDITOR
            _debugAdaptiveRenderScale = renderScale;
            _debugAdaptiveBudgetNormalized = normalized;
            _debugAdaptiveGlobalBudgetScale = _adaptiveGlobalBudgetScale;
            _debugAdaptiveBiomeBudgetScale = _adaptiveBiomeBudgetScale;
            _debugAdaptiveSpawnBudgetScale = _adaptiveSpawnBudgetScale;
#endif
        }

        private void RefreshEcologyCompositionResponse(
            WorldProceduralFaunaMood faunaMood,
            WorldZoneAnchor currentZone,
            DepthZoneProfile currentDepthZone)
        {
            _currentDepthZoneBudgetScale = ResolveDepthZoneBudgetScale(currentDepthZone);
            _currentPassiveSelectionScale = ResolvePassiveSelectionScale(faunaMood, currentZone, currentDepthZone);
            _currentAggressiveSelectionScale = ResolveAggressiveSelectionScale(faunaMood, currentZone, currentDepthZone);
            _currentLargeThreatSelectionScale = ResolveLargeThreatSelectionScale(currentZone, currentDepthZone);
            _currentDepthZoneSpecialistScale = ResolveDepthZoneSpecialistScale(currentDepthZone);
        }

        private float ResolvePassiveSelectionScale(
            WorldProceduralFaunaMood faunaMood,
            WorldZoneAnchor currentZone,
            DepthZoneProfile currentDepthZone)
        {
            float scale;
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    scale = calmPassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Lively:
                    scale = livelyPassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Hostile:
                    scale = hostilePassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Mixed:
                default:
                    scale = mixedPassiveSelectionScale;
                    break;
            }

            if (IsSafePocketZone(currentZone))
                scale *= safePocketPassiveSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZonePassiveSelectionScale;

            if (currentDepthZone != null)
            {
                if (currentDepthZone.hasCaves)
                    scale *= caveDepthZonePassiveSelectionScale;

                if (currentDepthZone.isThermal)
                    scale *= thermalDepthZonePassiveSelectionScale;
            }

            return Mathf.Max(0f, scale);
        }

        private float ResolveAggressiveSelectionScale(
            WorldProceduralFaunaMood faunaMood,
            WorldZoneAnchor currentZone,
            DepthZoneProfile currentDepthZone)
        {
            float scale;
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    scale = calmAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Lively:
                    scale = livelyAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Hostile:
                    scale = hostileAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Mixed:
                default:
                    scale = mixedAggressiveSelectionScale;
                    break;
            }

            if (IsSafePocketZone(currentZone))
                scale *= safePocketAggressiveSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZoneAggressiveSelectionScale;

            if (currentDepthZone != null)
            {
                if (currentDepthZone.hasCaves)
                    scale *= caveDepthZoneAggressiveSelectionScale;

                if (currentDepthZone.isThermal)
                    scale *= thermalDepthZoneAggressiveSelectionScale;

                if (currentDepthZone.requiredHullTier >= 2 || currentDepthZone.dangerLevel >= 0.72f)
                    scale *= highPressureHunterSelectionScale;
            }

            return Mathf.Max(0f, scale);
        }

        private float ResolveLargeThreatSelectionScale(WorldZoneAnchor currentZone, DepthZoneProfile currentDepthZone)
        {
            float scale = 1f;

            if (IsSafePocketZone(currentZone))
                scale *= safePocketLargeThreatSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZoneLargeThreatSelectionScale;

            if (currentZone != null && currentZone.RouteCritical)
                scale *= routeCriticalLargeThreatSelectionScale;

            if (currentDepthZone != null)
            {
                if (currentDepthZone.hasCaves)
                    scale *= caveDepthZoneLargeThreatSelectionScale;

                if (currentDepthZone.isThermal)
                    scale *= thermalDepthZoneLargeThreatSelectionScale;

                if (currentDepthZone.requiredHullTier >= 2 || currentDepthZone.dangerLevel >= 0.72f)
                    scale *= 1.08f;
            }

            scale *= _adaptiveSpawnBudgetScale;
            return Mathf.Max(0f, scale);
        }

        private static float ResolveSelectionWeight(
            in ResolvedFaunaEntry entry,
            float passiveSelectionScale,
            float aggressiveSelectionScale,
            float largeThreatSelectionScale,
            int biomeIndex,
            DepthZoneProfile currentDepthZone,
            float depthZoneSpecialistScale)
        {
            float weight = entry.spawnWeight;
            if (weight <= 0f)
                return 0f;

            CreatureArchetypeData archetype = entry.archetype;
            bool aggressive = (archetype != null && archetype.isAggressive) ||
                              (archetype != null && (archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan));

            weight *= aggressive ? aggressiveSelectionScale : passiveSelectionScale;
            if (entry.isLargeThreat)
                weight *= largeThreatSelectionScale;

            if (currentDepthZone != null)
            {
                if (currentDepthZone.hasCaves && entry.prefersClaustrophobicZone)
                    weight *= depthZoneSpecialistScale;

                if (currentDepthZone.isThermal && entry.prefersThermalZone)
                    weight *= depthZoneSpecialistScale;

                if ((currentDepthZone.requiredHullTier >= 2 || currentDepthZone.dangerLevel >= 0.72f) &&
                    entry.prefersHighPressureZone)
                {
                    weight *= depthZoneSpecialistScale;
                }
            }

            weight *= MigrationDirector.ResolveSelectionMultiplier(biomeIndex, archetype);

            return Mathf.Max(0f, weight);
        }

        private static bool IsPredatorArchetype(CreatureArchetypeData archetype)
        {
            return archetype != null &&
                   (archetype.isAggressive ||
                    archetype.roleType == CreatureRoleType.Hunter ||
                    archetype.roleType == CreatureRoleType.Leviathan);
        }

        private float ResolveDepthZoneBudgetScale(DepthZoneProfile currentDepthZone)
        {
            float scale = 1f;

            if (currentDepthZone != null)
            {
                if (currentDepthZone.hasCaves)
                    scale *= caveDepthZoneBudgetScale;

                if (currentDepthZone.isThermal)
                    scale *= thermalDepthZoneBudgetScale;

                if (currentDepthZone.requiredHullTier >= 2 || currentDepthZone.dangerLevel >= 0.72f)
                    scale *= highPressureDepthZoneBudgetScale;
            }

#if UNITY_EDITOR
            _debugDepthZoneBudgetScale = scale;
#endif
            return Mathf.Max(0.25f, scale);
        }

        private float ResolveDepthZoneSpecialistScale(DepthZoneProfile currentDepthZone)
        {
            if (currentDepthZone == null)
                return 1f;

            float scale = 1f;
            if (currentDepthZone.hasCaves)
                scale = Mathf.Max(scale, caveSpecialistSelectionScale);
            if (currentDepthZone.isThermal)
                scale = Mathf.Max(scale, thermalSpecialistSelectionScale);
            if (currentDepthZone.requiredHullTier >= 2 || currentDepthZone.dangerLevel >= 0.72f)
                scale = Mathf.Max(scale, highPressureHunterSelectionScale);

#if UNITY_EDITOR
            _debugDepthZoneSpecialistScale = scale;
#endif
            return Mathf.Max(1f, scale);
        }

        private static bool DoesEntryPreferClaustrophobicZone(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            if (archetype.defendNest || archetype.useHomeTerritory || archetype.useFeintRush)
                return true;

            switch (archetype.roleType)
            {
                case CreatureRoleType.Territorial:
                case CreatureRoleType.Hunter:
                    return true;
            }

            return ContainsAnyToken(archetype.creatureId, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.displayName, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.gameplayPurpose, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.biomeNotes, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.behaviorTreeHint, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.recommendedFaunaFamilyIds, CaveHabitatTokens) ||
                   ContainsAnyToken(archetype.recommendedBiomeFamilyIds, CaveHabitatTokens);
        }

        private static bool DoesEntryPreferThermalZone(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            return ContainsAnyToken(archetype.creatureId, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.displayName, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.gameplayPurpose, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.biomeNotes, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.behaviorTreeHint, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.recommendedFaunaFamilyIds, ThermalHabitatTokens) ||
                   ContainsAnyToken(archetype.recommendedBiomeFamilyIds, ThermalHabitatTokens);
        }

        private static bool DoesEntryPreferHighPressureZone(FaunaBiomeData biomeData, FaunaEntry faunaEntry)
        {
            CreatureArchetypeData archetype = faunaEntry.archetype;
            if (archetype == null)
                return biomeData != null && biomeData.useLargeThreatMacroZone;

            if (archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan)
                return true;

            if (archetype.useLeviathanPresence || archetype.isAggressive && archetype.maxHealth >= 90f)
                return true;

            if (biomeData != null &&
                (biomeData.largeThreatEncounterType == LeviathanEncounterType.AmbushBurst ||
                 biomeData.largeThreatEncounterType == LeviathanEncounterType.SentinelPressure))
            {
                return true;
            }

            if (biomeData != null && biomeData.useLargeThreatMacroZone)
                return true;

            return false;
        }

        private static bool ContainsAnyToken(string value, string[] tokens)
        {
            if (string.IsNullOrEmpty(value) || tokens == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                if (!string.IsNullOrEmpty(token) &&
                    value.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsAnyToken(string[] values, string[] tokens)
        {
            if (values == null || tokens == null)
                return false;

            for (int i = 0; i < values.Length; i++)
            {
                if (ContainsAnyToken(values[i], tokens))
                    return true;
            }

            return false;
        }

        private static bool IsSafePocketZone(WorldZoneAnchor currentZone)
        {
            if (currentZone == null)
                return false;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Construction:
                case WorldZoneAnchor.ZoneKind.Power:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsHostileZone(WorldZoneAnchor currentZone)
        {
            if (currentZone == null)
                return false;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Combat:
                case WorldZoneAnchor.ZoneKind.Trial:
                    return true;
                default:
                    return false;
            }
        }

        private string ResolveDebugEcologyBiasLabel()
        {
            if (_currentZoneIsSafePocket)
                return "SAFE";

            if (IsHostileZone(_currentZone))
                return "COMBAT";

            if (_currentDepthZone != null &&
                (_currentDepthZone.hasCaves ||
                 _currentDepthZone.isThermal ||
                 _currentDepthZone.requiredHullTier >= 2 ||
                 _currentDepthZone.dangerLevel >= 0.72f))
            {
                return "DEEP";
            }

            switch (_currentMatrixFaunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return "CALM";
                case WorldProceduralFaunaMood.Lively:
                    return "LIVELY";
                case WorldProceduralFaunaMood.Hostile:
                    return "HOSTILE";
                case WorldProceduralFaunaMood.Mixed:
                default:
                    return "BALANCED";
            }
        }

        private float ResolveGlobalBudgetScale(WorldProceduralFaunaMood faunaMood)
        {
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return calmFaunaBudgetScale;
                case WorldProceduralFaunaMood.Lively:
                    return livelyFaunaBudgetScale;
                case WorldProceduralFaunaMood.Mixed:
                    return mixedFaunaBudgetScale;
                case WorldProceduralFaunaMood.Hostile:
                    return hostileFaunaBudgetScale;
                default:
                    return 1f;
            }
        }

        private float ResolveBiomeCapScale(WorldProceduralFaunaMood faunaMood)
        {
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return calmBiomeCapScale;
                case WorldProceduralFaunaMood.Lively:
                    return livelyBiomeCapScale;
                case WorldProceduralFaunaMood.Mixed:
                    return mixedBiomeCapScale;
                case WorldProceduralFaunaMood.Hostile:
                    return hostileBiomeCapScale;
                default:
                    return 1f;
            }
        }

        private void TryWarmupCreaturePools()
        {
            if (_creaturePoolsWarmed || biomeDatasets == null || biomeDatasets.Length == 0)
                return;

            IObjectPoolService pool = _objectPool;
            if (pool == null)
                return;

            _warmupPrefabs.Clear();

            for (int biomeIndex = 0; biomeIndex < biomeDatasets.Length; biomeIndex++)
            {
                FaunaBiomeData biomeData = biomeDatasets[biomeIndex];
                if (biomeData == null || biomeData.possibleCreatures == null)
                    continue;

                List<FaunaEntry> possibleCreatures = biomeData.possibleCreatures;
                int creatureCount = possibleCreatures.Count;

                for (int creatureIndex = 0; creatureIndex < creatureCount; creatureIndex++)
                {
                    GameObject resolvedPrefab = possibleCreatures[creatureIndex].GetResolvedPrefab();
                    if (resolvedPrefab == null)
                        continue;
                    AddWarmupPrefabIfMissing(resolvedPrefab);
                }
            }

            for (int i = 0; i < _warmupPrefabs.Count; i++)
            {
                GameObject warmupPrefab = _warmupPrefabs[i];
                int requiredReserve = GetRequiredCreaturePoolWarmupReserve(warmupPrefab);
                int availableCount = pool.GetAvailableCount(warmupPrefab);
                if (availableCount >= requiredReserve)
                    continue;

                pool.Warmup(warmupPrefab, requiredReserve - availableCount);
            }

            _creaturePoolsWarmed = true;
        }

        private void AddWarmupPrefabIfMissing(GameObject prefab)
        {
            if (prefab == null)
                return;

            for (int i = 0; i < _warmupPrefabs.Count; i++)
            {
                if (ReferenceEquals(_warmupPrefabs[i], prefab))
                    return;
            }

            _warmupPrefabs.Add(prefab);
        }

        private int GetRequiredCreaturePoolWarmupReserve(GameObject prefab)
        {
            return IsSmallPassiveProxyPrefab(prefab)
                ? _smallPassiveCreaturePoolWarmupReserve
                : _defaultCreaturePoolWarmupReserve;
        }

        private static bool IsSmallPassiveProxyPrefab(GameObject prefab)
        {
            return prefab != null && prefab.name == SmallPassiveProxyPrefabName;
        }

        private void RefreshCreaturePoolWarmupTargets()
        {
            int maxAllowedReserve = Mathf.Max(CreaturePoolMinimumReserve, _runtimeGlobalMaxCount);
            int defaultReserve = Mathf.Clamp(
                Mathf.Max(CreaturePoolMinimumReserve, _runtimeMaxSpawnsPerTick * CreaturePoolBurstReserveMultiplier),
                CreaturePoolMinimumReserve,
                maxAllowedReserve);
            int smallPassiveReserve = Mathf.Clamp(
                Mathf.Max(defaultReserve, Mathf.Max(SmallPassiveProxyMinimumReserve, _runtimeMaxSpawnsPerTick * SmallPassiveProxyBurstReserveMultiplier)),
                CreaturePoolMinimumReserve,
                maxAllowedReserve);

            if (defaultReserve == _defaultCreaturePoolWarmupReserve &&
                smallPassiveReserve == _smallPassiveCreaturePoolWarmupReserve)
            {
                return;
            }

            _defaultCreaturePoolWarmupReserve = defaultReserve;
            _smallPassiveCreaturePoolWarmupReserve = smallPassiveReserve;
            _creaturePoolsWarmed = false;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ñ… ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ð¼Ð¸Ñ€Ðµ.</summary>
        public int ActiveCreatureCount => _activeCreatures != null ? _activeCreatures.Count : 0;

        internal int ApplyEmergencyColdTickCull()
        {
            if (_activeCreatures == null || _activeCreatures.Count <= 0)
                return 0;

            int culledCount = 0;
            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];
                if ((creature.watchdogFlags & ActiveCreatureFlagPredator) != 0u ||
                    (creature.watchdogFlags & ActiveCreatureFlagHasBrain) == 0u)
                {
                    continue;
                }

                FaunaBrain ai = creature.brain;
                if (ai == null)
                {
                    creature.watchdogFlags &= ~ActiveCreatureFlagHasBrain;
                    _activeCreatures[i] = creature;
                    continue;
                }

                if (ai.ApplyDirectorColdTickCull(true))
                    culledCount++;
            }

            return culledCount;
        }

        int RuntimeWatchdog.IEmergencyColdTickCullTarget.ApplyEmergencyColdTickCull()
        {
            return ApplyEmergencyColdTickCull();
        }

        internal bool TrySpawnEncounterThreat(
            EncounterThreatClass threatClass,
            Vector3 spawnPosition,
            uint deterministicSeed,
            uint squadStateBits,
            int squadOrdinal,
            out GameObject spawnedInstance)
        {
            spawnedInstance = null;

            EnsureRuntimeStateInitialized();
            ResolvePlayerViewTransform();

            IObjectPoolService pool = _objectPool;
            if (pool == null)
                return false;

            if (!TryResolveEncounterBiomeData(
                    pool,
                    out FaunaBiomeData biomeData,
                    out ResolvedFaunaEntry[] resolvedEntries,
                    out int[] creatureTypeCounts,
                    out int[] availablePoolCounts))
            {
                return false;
            }

            if (!TrySelectEncounterEntry(
                    resolvedEntries,
                    creatureTypeCounts,
                    availablePoolCounts,
                    biomeData,
                    threatClass,
                    spawnPosition,
                    deterministicSeed,
                    out ResolvedFaunaEntry selectedEntry))
            {
                return false;
            }

            WorldChunkCoordinate spawnChunk = WorldChunkCoordinate.FromWorldPosition(spawnPosition, _runtimeChunkSize);
            if (GetChunkCreatureCount(spawnChunk) >= _runtimePerChunkMaxCount)
                return false;

            if (selectedEntry.isPredator && GetPredatorSectorCount(spawnChunk) >= PredatorHardCapPerKilometerSector)
                return false;

            WorldMacroZoneCoordinate spawnMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(spawnPosition, _runtimeMacroZoneSize);
            if (selectedEntry.isLargeThreat &&
                (!TryResolvePlayerLogicPose(out Vector3 encounterPlayerPosition, out _) ||
                 !CanSpawnLargeThreatNearPlayer(spawnMacroZone, encounterPlayerPosition)))
            {
                return false;
            }

            IEcosystemDirectorService ecosystemDirector = ResolveEcosystemDirector();
            if (ecosystemDirector != null &&
                !ecosystemDirector.TryResolveSpawnWeightMultiplier(selectedEntry.archetype, spawnPosition, out _))
            {
                return false;
            }

            GameObject resolvedPrefab = selectedEntry.prefab;
            if (resolvedPrefab == null)
                return false;

            int biomeIndex = biomeData.biomeIndex;
            if (!TryResolveRuntimePositionAup(spawnPosition, out AbsoluteUniversePosition spawnAup))
                return false;

            uint uniqueInstanceUid = IsApexPredatorArchetype(selectedEntry.archetype, selectedEntry.isLargeThreat)
                ? BuildApexFaunaInstanceUid(selectedEntry.archetype, in spawnMacroZone)
                : BuildStandardFaunaInstanceUid(selectedEntry.speciesId, biomeIndex, spawnChunk, in spawnAup);
            if (uniqueInstanceUid != 0u &&
                ecosystemDirector != null &&
                ecosystemDirector.IsApexTombstoned(uniqueInstanceUid))
            {
                return false;
            }

            if (ecosystemDirector != null &&
                !ecosystemDirector.TryConsumeSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator))
            {
                return false;
            }

            Quaternion spawnRotation = ResolveDeterministicEncounterRotation(deterministicSeed);
            GameObject instance = pool.Spawn(resolvedPrefab, spawnPosition, spawnRotation, false);
            if (instance == null)
            {
                ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator);
                return false;
            }

            int typeIndex = selectedEntry.creatureTypeIndex;

            if (instance.TryGetComponent(out FaunaBrain ai))
            {
                ai.ApplyArchetype(selectedEntry.archetype);
                ai.SetSpawnPoint(spawnPosition);
                ai.SetLogicalIdentity(uniqueInstanceUid);
                ai.SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                _faunaPresentationService?.ConfigureSpawnedCreature(ai, selectedEntry.archetype, biomeIndex, spawnPosition, in spawnChunk);

                if (selectedEntry.isPredator || threatClass != EncounterThreatClass.Drone)
                    ai.ForceState(FaunaBrain.AIState.Aggressive);

                if (threatClass == EncounterThreatClass.Stalker &&
                    TryResolvePlayerLogicPose(out Vector3 squadPlayerPosition, out _))
                {
                    uint resolvedSquadStateBits = squadStateBits != 0u ? squadStateBits : FaunaBrain.PredatorHuntingStateBits;
                    ai.ApplyHunterSquadDirective(squadPlayerPosition, resolvedSquadStateBits, squadOrdinal, 18f);
                }
            }

            if (!TryBuildActiveCreatureRecord(
                    instance,
                    resolvedPrefab,
                    selectedEntry.archetype,
                    typeIndex,
                    biomeIndex,
                    spawnChunk,
                    spawnMacroZone,
                    selectedEntry.isLargeThreat,
                    selectedEntry.isPredator,
                    ai,
                    uniqueInstanceUid,
                    in spawnAup,
                    out ActiveCreature record))
            {
                ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator);
                if (pool != null)
                    pool.Despawn(instance);
                else
                    instance.SetActive(false);

                return false;
            }

            _activeCreatures.Add(record);
            IncrementCreatureCounters(biomeIndex, typeIndex, biomeData);
            IncrementChunkCount(spawnChunk);
            if (selectedEntry.isPredator)
                IncrementPredatorSectorCount(spawnChunk);
            if (selectedEntry.isLargeThreat)
                IncrementMacroZoneCount(spawnMacroZone);

            if (typeIndex >= 0 && typeIndex < availablePoolCounts.Length && availablePoolCounts[typeIndex] > 0)
                availablePoolCounts[typeIndex]--;

            spawnedInstance = instance;
            return true;
        }

        internal bool TryRecallEncounterThreat(int instanceId)
        {
            if (instanceId == 0 || _activeCreatures == null || _activeCreatures.Count <= 0)
                return false;

            IObjectPoolService pool = _objectPool;
            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];
                if (creature.gameObject == null)
                {
                    DecrementCreatureCounters(in creature);
                    ReleaseDehydrationSlot(creature.dehydrationSlotIndex);
                    SwapRemoveAt(i);
                    continue;
                }

                if (unchecked((int)EntityId.ToULong(creature.gameObject.GetEntityId())) != instanceId)
                    continue;

                if (pool != null)
                    pool.Despawn(creature.gameObject);
                else
                    creature.gameObject.SetActive(false);

                DecrementCreatureCounters(in creature);
                ReleaseDehydrationSlot(creature.dehydrationSlotIndex);
                SwapRemoveAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// ÐŸÑ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¹ Ð´ÐµÑÐ¿Ð°Ð²Ð½ Ð’Ð¡Ð•Ð¥ ÑÑƒÑ‰ÐµÑÑ‚Ð².
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ Ð¿Ñ€Ð¸ ÑÐ¼ÐµÐ½Ðµ Ð·Ð¾Ð½Ñ‹, Ð·Ð°Ð³Ñ€ÑƒÐ·ÐºÐµ ÑÐµÐ¹Ð²Ð°, Ñ‚ÐµÐ»ÐµÐ¿Ð¾Ñ€Ñ‚Ðµ.
        /// ÐžÑ‡Ð¸Ñ‰Ð°ÐµÑ‚ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¸.
        /// </summary>
        public void DespawnAll()
        {
            CompleteResidentDataOnlySimulation(forceComplete: true);
            IObjectPoolService pool = _objectPool;

            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];

                if (creature.gameObject != null && pool != null)
                {
                    pool.Despawn(creature.gameObject);
                }
            }

            _activeCreatures.Clear();
            ResetDehydrationResidencyState();
            _countsPerChunk.Clear();
            _predatorCountsPerSector.Clear();
            _largeThreatCountsPerMacroZone.Clear();
            _persistedTier2FaunaCount = 0;

            // â”€â”€ ÐžÑ‡Ð¸ÑÑ‚ÐºÐ° stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¾Ð² â”€â”€
            System.Array.Clear(_countsPerBiome, 0, _countsPerBiome.Length);

            // ÐžÑ‡Ð¸ÑÑ‚ÐºÐ° per-type counts Ð±ÐµÐ· foreach (Ð¸Ð·Ð±ÐµÐ³Ð°ÐµÐ¼ GC Ð¾Ñ‚ Dictionary enumerator)
            if (biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    FaunaBiomeData data = biomeDatasets[i];
                    if (data != null &&
                        _countsPerTypePerBiome.TryGetValue(data, out int[] typeCounts))
                    {
                        System.Array.Clear(typeCounts, 0, typeCounts.Length);
                    }
                }
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” DIRECTOR ORCHESTRATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð£Ð¿Ñ€Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ…Ð¸Ñ‰Ð½Ñ‹Ð¼ Ð´Ð°Ð²Ð»ÐµÐ½Ð¸ÐµÐ¼. Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ HectonDirectorAI
        /// Ð¿Ñ€Ð¸ ÑÐ¼ÐµÐ½Ðµ Ñ„Ð°Ð· (BuildUp/Peak â†’ true, Relax â†’ false).
        ///
        /// ÐŸÑ€Ð¸ enabled == false:
        ///   â€¢ Ð’ÑÐµ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ñ FaunaBrain Ð¿ÐµÑ€ÐµÐ²Ð¾Ð´ÑÑ‚ÑÑ
        ///     Ð² ÑÐ¾ÑÑ‚Ð¾ÑÐ½Ð¸Ðµ Wander (Ð¾Ñ‚ÑÑ‚ÑƒÐ¿Ð»ÐµÐ½Ð¸Ðµ Ð¾Ñ‚ Ð¸Ð³Ñ€Ð¾ÐºÐ°).
        ///   â€¢ ForceSpawnHorde Ð±Ð»Ð¾ÐºÐ¸Ñ€ÑƒÐµÑ‚ÑÑ Ð´Ð¾ Ð¿Ð¾Ð²Ñ‚Ð¾Ñ€Ð½Ð¾Ð³Ð¾ Ð²ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ñ.
        ///
        /// ÐŸÑ€Ð¸ enabled == true:
        ///   â€¢ Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ÑÑ ÑˆÑ‚Ð°Ñ‚Ð½Ñ‹Ð¹ AI behaviour.
        ///   â€¢ ForceSpawnHorde Ñ€Ð°Ð·Ñ€ÐµÑˆÐ°ÐµÑ‚ÑÑ.
        ///
        /// ZERO GC: for-Ñ†Ð¸ÐºÐ» Ð¿Ð¾ pre-allocated List, TryGetComponent
        /// Ð½Ðµ Ð°Ð»Ð»Ð¾Ñ†Ð¸Ñ€ÑƒÐµÑ‚ (generic constrained). ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… LINQ/foreach.
        /// </summary>
        /// <param name="enabled">true = Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ Ñ€Ð°Ð·Ñ€ÐµÑˆÐµÐ½Ð¾, false = Ð¾Ñ‚ÑÑ‚ÑƒÐ¿Ð»ÐµÐ½Ð¸Ðµ.</param>
        private bool TryResolveEncounterBiomeData(
            IObjectPoolService pool,
            out FaunaBiomeData biomeData,
            out ResolvedFaunaEntry[] resolvedEntries,
            out int[] creatureTypeCounts,
            out int[] availablePoolCounts)
        {
            biomeData = null;
            resolvedEntries = null;
            creatureTypeCounts = null;
            availablePoolCounts = null;

            if (_cachedBiomeIndex >= 0)
                _biomeLookup.TryGetValue(_cachedBiomeIndex, out biomeData);

            if (biomeData == null && biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    if (biomeDatasets[i] != null)
                    {
                        biomeData = biomeDatasets[i];
                        break;
                    }
                }
            }

            if (biomeData == null ||
                _resolvedEntriesPerBiome == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0 ||
                !_countsPerTypePerBiome.TryGetValue(biomeData, out creatureTypeCounts) ||
                _availablePoolCountsPerBiome == null ||
                !_availablePoolCountsPerBiome.TryGetValue(biomeData, out availablePoolCounts) ||
                availablePoolCounts == null ||
                availablePoolCounts.Length < resolvedEntries.Length)
            {
                return false;
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                GameObject prefab = resolvedEntries[i].prefab;
                availablePoolCounts[i] = prefab != null ? pool.GetAvailableCount(prefab) : 0;
            }

            return true;
        }

        private bool TrySelectEncounterEntry(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            FaunaBiomeData biomeData,
            EncounterThreatClass threatClass,
            Vector3 spawnPosition,
            uint deterministicSeed,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0)
                return false;

            float totalWeight = 0f;
            int fallbackIndex = -1;
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (!MatchesEncounterThreatClass(in entry, threatClass))
                    continue;
                if (!_pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    _currentPassiveSelectionScale,
                    _currentAggressiveSelectionScale,
                    _currentLargeThreatSelectionScale,
                    biomeData.biomeIndex,
                    _currentDepthZone,
                    _currentDepthZoneSpecialistScale);
                selectionWeight *= ResolveEncounterThreatBias(in entry, threatClass);
                if (selectionWeight <= 0f)
                    continue;

                totalWeight += selectionWeight;
                fallbackIndex = i;
            }

            if (totalWeight <= 0f)
                return false;

            uint seed = EncounterDirector.BuildDeterministicSeed(spawnPosition, unchecked((int)deterministicSeed), biomeData.biomeIndex, _activeCreatures.Count);
            float roll = EncounterDirector.HashToUnit01(seed) * totalWeight;

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (!MatchesEncounterThreatClass(in entry, threatClass))
                    continue;
                if (!_pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(
                    in entry,
                    _currentPassiveSelectionScale,
                    _currentAggressiveSelectionScale,
                    _currentLargeThreatSelectionScale,
                    biomeData.biomeIndex,
                    _currentDepthZone,
                    _currentDepthZoneSpecialistScale);
                selectionWeight *= ResolveEncounterThreatBias(in entry, threatClass);
                if (selectionWeight <= 0f)
                    continue;

                roll -= selectionWeight;
                if (roll <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            if (fallbackIndex >= 0)
            {
                selectedEntry = resolvedEntries[fallbackIndex];
                return true;
            }

            return false;
        }

        private static bool MatchesEncounterThreatClass(in ResolvedFaunaEntry entry, EncounterThreatClass threatClass)
        {
            CreatureArchetypeData archetype = entry.archetype;
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return entry.isLargeThreat || (archetype != null && archetype.roleType == CreatureRoleType.Leviathan);

                case EncounterThreatClass.Stalker:
                    return !entry.isLargeThreat && entry.isPredator;

                case EncounterThreatClass.Swarm:
                    if (entry.isLargeThreat || archetype == null)
                        return false;

                    return archetype.usePackHunt ||
                           archetype.callNearbyAllies ||
                           archetype.roleType == CreatureRoleType.Territorial ||
                           archetype.roleType == CreatureRoleType.Hunter;

                default:
                    return !entry.isLargeThreat;
            }
        }

        private static float ResolveEncounterThreatBias(in ResolvedFaunaEntry entry, EncounterThreatClass threatClass)
        {
            CreatureArchetypeData archetype = entry.archetype;
            switch (threatClass)
            {
                case EncounterThreatClass.Leviathan:
                    return entry.isLargeThreat ? 2f : 0.25f;

                case EncounterThreatClass.Stalker:
                    return entry.isPredator ? 1.5f : 0.35f;

                case EncounterThreatClass.Swarm:
                    if (archetype == null)
                        return 0.5f;

                    if (archetype.usePackHunt || archetype.callNearbyAllies)
                        return 1.8f;

                    if (archetype.roleType == CreatureRoleType.Territorial || archetype.roleType == CreatureRoleType.Hunter)
                        return 1.25f;

                    return 0.45f;

                default:
                    if (archetype != null && archetype.roleType == CreatureRoleType.DroneTrader)
                        return 2f;

                    return entry.isPredator ? 0.35f : 1f;
            }
        }

        private static Quaternion ResolveDeterministicEncounterRotation(uint seed)
        {
            return _spawnRotationLut[(int)((seed ^ 0xC13FA9A9u) & SpawnDirectionLutMask)];
        }

        public void SetPredatorPressure(bool enabled)
        {
            _pressureEnabled = enabled;

            // â”€â”€ ÐŸÑ€Ð¸ Ð¾Ñ‚ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¸Ð¸ Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ñ â€” Ð·Ð°ÑÑ‚Ð°Ð²Ð»ÑÐµÐ¼ Ð²ÑÐµÑ… Ð¾Ñ‚ÑÑ‚ÑƒÐ¿Ð¸Ñ‚ÑŒ â”€â”€
            if (!enabled)
            {
                int count = _activeCreatures.Count;
                for (int i = 0; i < count; i++)
                {
                    ActiveCreature creature = _activeCreatures[i];

                    // ÐŸÑ€Ð¾Ð¿ÑƒÑÐºÐ°ÐµÐ¼ ÑƒÐ½Ð¸Ñ‡Ñ‚Ð¾Ð¶ÐµÐ½Ð½Ñ‹Ðµ/Ð´ÐµÐ°ÐºÑ‚Ð¸Ð²Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ñ‹Ðµ Ð¾Ð±ÑŠÐµÐºÑ‚Ñ‹
                    if ((creature.watchdogFlags & ActiveCreatureFlagHasBrain) == 0u)
                        continue;

                    FaunaBrain ai = creature.brain;
                    if (ai == null)
                    {
                        creature.watchdogFlags &= ~ActiveCreatureFlagHasBrain;
                        _activeCreatures[i] = creature;
                        continue;
                    }

                    ai.ForceState(FaunaBrain.AIState.Wander);
                }
            }

#if UNITY_EDITOR
            _debugPressureEnabled = _pressureEnabled;
#endif
        }

        /// <summary>
        /// ÐŸÑ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¹ ÑÐ¿Ð°Ð²Ð½ Ð¾Ñ€Ð´Ñ‹ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð¿Ð¾ ÐºÐ¾Ð¼Ð°Ð½Ð´Ðµ Ð”Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€Ð°.
        /// Ð˜Ð³Ð½Ð¾Ñ€Ð¸Ñ€ÑƒÐµÑ‚ Ð²Ð½ÑƒÑ‚Ñ€ÐµÐ½Ð½Ð¸Ðµ ÐºÑƒÐ»Ð´Ð°ÑƒÐ½Ñ‹ FaunaDirector â€” ÑÑ‚Ð¾ Ð¿Ñ€Ð¸ÐºÐ°Ð·.
        ///
        /// ÐÐ»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼:
        ///   1. Ð•ÑÐ»Ð¸ _pressureEnabled == false â€” Ð²Ñ‹Ñ…Ð¾Ð´ (Relax Ð±Ð»Ð¾ÐºÐ¸Ñ€ÑƒÐµÑ‚ Ð¾Ñ€Ð´Ñ‹).
        ///   2. Ð’Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ FaunaBiomeData: Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÐ¼ _cachedBiomeIndex
        ///      (Ñ‚ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð±Ð¸Ð¾Ð¼ Ð¸Ð³Ñ€Ð¾ÐºÐ°), fallback Ð½Ð° Ð¿ÐµÑ€Ð²Ñ‹Ð¹ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ñ‹Ð¹ dataset.
        ///   3. Ð’ Ñ†Ð¸ÐºÐ»Ðµ (hordeCountMin..hordeCountMax Ð¸Ñ‚ÐµÑ€Ð°Ñ†Ð¸Ð¹):
        ///      â€¢ Ð“ÐµÐ½ÐµÑ€Ð°Ñ†Ð¸Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð² Ñ€Ð°Ð´Ð¸ÑƒÑÐµ hordeRadiusInner..hordeRadiusOuter
        ///        Ð¾Ñ‚ worldCenter.
        ///      â€¢ Ð¡Ð¿Ð°Ð²Ð½ Ñ‡ÐµÑ€ÐµÐ· ObjectPoolManager.Spawn.
        ///      â€¢ Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð°Ñ†Ð¸Ñ Ð² _activeCreatures + Ð¸Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚ ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¾Ð².
        ///      â€¢ ÐÐµÐ¼ÐµÐ´Ð»ÐµÐ½Ð½Ñ‹Ð¹ ForceState(Aggressive) Ð´Ð»Ñ Ð°Ñ‚Ð°ÐºÐ¸.
        ///   4. Global limit ÑƒÐ²Ð°Ð¶Ð°ÐµÑ‚ÑÑ â€” ÐµÑÐ»Ð¸ ÑÐ»Ð¾Ñ‚Ñ‹ ÐºÐ¾Ð½Ñ‡Ð¸Ð»Ð¸ÑÑŒ, ÑÐ¿Ð°Ð²Ð½ Ð¿Ñ€ÐµÑ€Ñ‹Ð²Ð°ÐµÑ‚ÑÑ.
        ///
        /// ZERO GC: struct math, pre-allocated List, TryGetComponent.
        /// </summary>
        /// <param name="worldCenter">Ð¦ÐµÐ½Ñ‚Ñ€ ÑÐ¿Ð°Ð²Ð½Ð° Ð¾Ñ€Ð´Ñ‹ (Ð¼Ð¸Ñ€Ð¾Ð²Ñ‹Ðµ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ñ‹).</param>
        public void ForceSpawnHorde(Vector3 worldCenter)
        {
            // â”€â”€ Relax-Ñ„Ð°Ð·Ð° Ð±Ð»Ð¾ÐºÐ¸Ñ€ÑƒÐµÑ‚ Ð¾Ñ€Ð´Ñ‹ â”€â”€
            if (!_pressureEnabled)
                return;

            ResolvePlayerViewTransform();

            IObjectPoolService pool = _objectPool;
            if (pool == null)
                return;

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  Ð’Ñ‹Ð±Ð¾Ñ€ FaunaBiomeData
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  ÐŸÑ€Ð¸Ð¾Ñ€Ð¸Ñ‚ÐµÑ‚: Ñ‚ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð±Ð¸Ð¾Ð¼ Ð¸Ð³Ñ€Ð¾ÐºÐ° (_cachedBiomeIndex).
            //  Fallback: Ð¿ÐµÑ€Ð²Ñ‹Ð¹ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ñ‹Ð¹ dataset Ð¸Ð· biomeDatasets.

            FaunaBiomeData biomeData = null;

            // ÐŸÐ¾Ð¿Ñ€Ð¾Ð±Ð¾Ð²Ð°Ñ‚ÑŒ Ñ‚ÐµÐºÑƒÑ‰Ð¸Ð¹ Ð±Ð¸Ð¾Ð¼ Ð¸Ð³Ñ€Ð¾ÐºÐ°
            if (_cachedBiomeIndex >= 0)
            {
                _biomeLookup.TryGetValue(_cachedBiomeIndex, out biomeData);
            }

            // Fallback: Ð¿ÐµÑ€Ð²Ñ‹Ð¹ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ñ‹Ð¹ dataset
            if (biomeData == null && biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    if (biomeDatasets[i] != null)
                    {
                        biomeData = biomeDatasets[i];
                        break;
                    }
                }
            }

            if (biomeData == null)
                return;

            // ÐŸÑ€Ð¾Ð²ÐµÑ€ÑÐµÐ¼ Ð½Ð°Ð»Ð¸Ñ‡Ð¸Ðµ ÑÑƒÑ‰ÐµÑÑ‚Ð² Ð² Ð±Ð¸Ð¾Ð¼Ðµ
            if (_resolvedEntriesPerBiome == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out ResolvedFaunaEntry[] resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0)
            {
                return;
            }

            if (!_countsPerTypePerBiome.TryGetValue(biomeData, out int[] creatureTypeCounts))
                return;

            if (_availablePoolCountsPerBiome == null ||
                !_availablePoolCountsPerBiome.TryGetValue(biomeData, out int[] availablePoolCounts) ||
                availablePoolCounts == null ||
                availablePoolCounts.Length < resolvedEntries.Length)
            {
                return;
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                GameObject prefab = resolvedEntries[i].prefab;
                availablePoolCounts[i] = prefab != null ? pool.GetAvailableCount(prefab) : 0;
            }

            int biomeIdx = biomeData.biomeIndex;
            IEcosystemDirectorService ecosystemDirector = ResolveEcosystemDirector();

            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
            //  Ð¡Ð¿Ð°Ð²Ð½ Ð¾Ñ€Ð´Ñ‹
            // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

            int hordeSize = _biomeSpawnRandom.NextInt(hordeCountMin, hordeCountMax + 1);
            int spawned = 0;

            for (int h = 0; h < hordeSize; h++)
            {
                // â”€â”€ Global limit check â”€â”€
                if (GetTrackedCreaturePopulationCount() >= _runtimeGlobalMaxCount)
                    break;

                // â”€â”€ Ð’Ñ‹Ð±Ð¾Ñ€ ÑÐ»ÑƒÑ‡Ð°Ð¹Ð½Ð¾Ð³Ð¾ Ñ‚Ð¸Ð¿Ð° ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð° Ð¸Ð· Ð±Ð¸Ð¾Ð¼Ð° â”€â”€
                // Ð”Ð»Ñ Ð¾Ñ€Ð´Ñ‹ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÐ¼ Ñ€Ð°Ð²Ð½Ð¾Ð¼ÐµÑ€Ð½Ñ‹Ð¹ Ð²Ñ‹Ð±Ð¾Ñ€ Ð¸Ð· possibleCreatures
                // (weighted random Ñ‡ÐµÑ€ÐµÐ· TrySelectCreature Ð½Ðµ Ð¾Ð±ÑÐ·Ð°Ñ‚ÐµÐ»ÐµÐ½ â€”
                //  Ð”Ð¸Ñ€ÐµÐºÑ‚Ð¾Ñ€ Ñ…Ð¾Ñ‡ÐµÑ‚ Ð»ÑŽÐ±ÑƒÑŽ ÑƒÐ³Ñ€Ð¾Ð·Ñƒ, Ð° Ð½Ðµ balanced population).
                if (!TrySelectResolvedHordeEntry(resolvedEntries, creatureTypeCounts, availablePoolCounts, out ResolvedFaunaEntry selectedEntry))
                    break;

                GameObject resolvedPrefab = selectedEntry.prefab;
                if (resolvedPrefab == null)
                    continue;

                // â”€â”€ ÐŸÐ¾Ð·Ð¸Ñ†Ð¸Ñ Ð² ÐºÐ¾Ð»ÑŒÑ†Ðµ Ð²Ð¾ÐºÑ€ÑƒÐ³ worldCenter â”€â”€
                Vector2 direction = NextSpawnDirection(ref _biomeSpawnRandom);
                float distance = _biomeSpawnRandom.NextFloat(hordeRadiusInner, hordeRadiusOuter);

                float spawnX = worldCenter.x + direction.x * distance;
                float spawnZ = worldCenter.z + direction.y * distance;
                float spawnY = worldCenter.y; // Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÐ¼ Ð²Ñ‹ÑÐ¾Ñ‚Ñƒ Ñ†ÐµÐ½Ñ‚Ñ€Ð° ÑÐ¾Ð±Ñ‹Ñ‚Ð¸Ñ

                Vector3    spawnPos = new Vector3(spawnX, spawnY, spawnZ);
                Quaternion spawnRot = NextSpawnRotation(ref _biomeSpawnRandom);
                WorldChunkCoordinate spawnChunk = WorldChunkCoordinate.FromWorldPosition(spawnPos, _runtimeChunkSize);

                if (!IsSpawnPointValid(_playerViewTransform, spawnPos))
                    continue;

                if (ecosystemDirector != null &&
                    !ecosystemDirector.TryResolveSpawnWeightMultiplier(selectedEntry.archetype, spawnPos, out _))
                {
                    continue;
                }

                if (GetChunkCreatureCount(spawnChunk) >= _runtimePerChunkMaxCount)
                    continue;

                if (ecosystemDirector != null &&
                    !ecosystemDirector.TryConsumeSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator))
                {
                    continue;
                }

                // â”€â”€ Ð¡Ð¿Ð°Ð²Ð½ Ñ‡ÐµÑ€ÐµÐ· Ð¿ÑƒÐ» â”€â”€
                GameObject instance = pool.Spawn(resolvedPrefab, spawnPos, spawnRot, false);
                if (instance == null)
                {
                    ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator);
                    continue;
                }

                // â”€â”€ ÐžÐ¿Ñ€ÐµÐ´ÐµÐ»ÑÐµÐ¼ typeIndex â”€â”€
                int typeIndex = selectedEntry.creatureTypeIndex;

                // â”€â”€ Ð ÐµÐ³Ð¸ÑÑ‚Ñ€Ð°Ñ†Ð¸Ñ Ð² Ñ‚Ñ€ÐµÐºÐµÑ€Ðµ â”€â”€
                ActiveCreature record = default;

                // â”€â”€ Ð˜Ð½ÐºÑ€ÐµÐ¼ÐµÐ½Ñ‚ stateful-ÑÑ‡Ñ‘Ñ‚Ñ‡Ð¸ÐºÐ¾Ð² â”€â”€

                // â”€â”€ ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° AI: ÑÐ¿Ð°Ð²Ð½-Ð¿Ð¾Ð¸Ð½Ñ‚ + Ð¿Ñ€Ð¸Ð½ÑƒÐ´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð¾Ðµ Aggressive â”€â”€
                if (!TryResolveRuntimePositionAup(spawnPos, out AbsoluteUniversePosition spawnAup))
                    continue;

                uint uniqueInstanceUid = BuildStandardFaunaInstanceUid(selectedEntry.speciesId, biomeIdx, spawnChunk, in spawnAup);

                if (instance.TryGetComponent(out FaunaBrain ai))
                {
                    ai.ApplyArchetype(selectedEntry.archetype);
                    ai.SetSpawnPoint(spawnPos);
                    ai.SetLogicalIdentity(uniqueInstanceUid);
                    ai.SetLogicalLodTier(FaunaLogicalLodTier.FullSim);
                    _faunaPresentationService?.ConfigureSpawnedCreature(ai, selectedEntry.archetype, biomeIdx, spawnPos, in spawnChunk);
                    ai.ForceState(FaunaBrain.AIState.Aggressive);
                }

                if (!TryBuildActiveCreatureRecord(
                        instance,
                        resolvedPrefab,
                        selectedEntry.archetype,
                        typeIndex,
                        biomeIdx,
                        spawnChunk,
                        default,
                        false,
                        selectedEntry.isPredator,
                        ai,
                        uniqueInstanceUid,
                        in spawnAup,
                        out record))
                {
                    ecosystemDirector?.RefundSpawnCredit(selectedEntry.archetype, selectedEntry.isLargeThreat, selectedEntry.isPredator);
                    if (pool != null)
                        pool.Despawn(instance);
                    else
                        instance.SetActive(false);

                    continue;
                }

                _activeCreatures.Add(record);
                IncrementCreatureCounters(biomeIdx, typeIndex, biomeData);
                IncrementChunkCount(spawnChunk);
                if (selectedEntry.isPredator)
                    IncrementPredatorSectorCount(spawnChunk);

                if (typeIndex >= 0 && typeIndex < availablePoolCounts.Length && availablePoolCounts[typeIndex] > 0)
                    availablePoolCounts[typeIndex]--;

                spawned++;
            }

#if UNITY_EDITOR
            _debugLastHordeSpawned = spawned;
#endif
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  DIAGNOSTICS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(
            int cullCount,
            int spawnAttempts,
            int spawnValidationAttempts,
            int spawnValidationSuccesses,
            int anchorBasedSpawns,
            int fallbackRingSpawns)
        {
            _debugActiveCount    = _activeCreatures.Count;
            _debugCurrentBiome   = _cachedBiomeIndex;
            _debugCullCount      = cullCount;
            _debugSpawnAttempts  = spawnAttempts;
            _debugPressureEnabled = _pressureEnabled;
            _debugActiveChunks = _countsPerChunk != null ? _countsPerChunk.Count : 0;
            _debugActiveMacroZones = _largeThreatCountsPerMacroZone != null ? _largeThreatCountsPerMacroZone.Count : 0;
            _debugRegistryFaunaAnchors = spawnRegistry != null ? spawnRegistry.OrdinaryAnchorCount : 0;
            _debugRegistryLargeThreatZones = spawnRegistry != null ? spawnRegistry.LargeThreatZoneCount : 0;
            _debugRuntimeChunkSize = _runtimeChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeMacroZoneSize;
            _debugRuntimeGlobalMaxCount = _runtimeGlobalMaxCount;
            _debugRuntimePerChunkMaxCount = _runtimePerChunkMaxCount;
            _debugRuntimeSpawnOuter = _runtimeSpawnRingOuter;
            _debugRuntimeLargeThreatSpawnOuter = _runtimeLargeThreatSpawnOuter;
            _debugRuntimeCullDistance = _runtimeKillDistance;
            _debugRuntimeLargeThreatCullDistance = _runtimeLargeThreatKillDistance;
            _debugSpawnValidationAttempts = spawnValidationAttempts;
            _debugSpawnValidationSuccesses = spawnValidationSuccesses;
            _debugAnchorBasedSpawns = anchorBasedSpawns;
            _debugFallbackRingSpawns = fallbackRingSpawns;
            _debugMatrixFaunaMood = _currentMatrixFaunaMood;
            _debugEffectiveGlobalMaxCount = _currentEffectiveGlobalMaxCount;
            _debugEffectiveSpawnsPerTick = _currentEffectiveSpawnsPerTick;
            _debugEffectiveBiomeMaxCount = _cachedBiomeIndex >= 0 ? _currentEffectiveBiomeMaxCount : 0;
            IDynamicResolutionRuntime scaler = _dynamicResolutionRuntime;
            _debugAdaptiveRenderScale = scaler != null ? scaler.CurrentRenderScale01 : 1f;
            _debugAdaptiveBudgetNormalized = _adaptiveBudgetNormalized;
            _debugAdaptiveGlobalBudgetScale = _adaptiveGlobalBudgetScale;
            _debugAdaptiveBiomeBudgetScale = _adaptiveBiomeBudgetScale;
            _debugAdaptiveSpawnBudgetScale = _adaptiveSpawnBudgetScale;
            _debugCurrentZone = _currentZone != null ? _currentZone.ZoneLabel : "None";
            _debugCurrentZoneRouteCritical = _currentZone != null && _currentZone.RouteCritical;
            _debugCurrentZoneSafePocket = _currentZoneIsSafePocket;
            _debugCurrentDepthZone = _currentDepthZone != null ? _currentDepthZone.displayName : "None";
            _debugCurrentDepthZoneThermal = _currentDepthZone != null && _currentDepthZone.isThermal;
            _debugCurrentDepthZoneCaves = _currentDepthZone != null && _currentDepthZone.hasCaves;
            _debugCurrentDepthZoneDanger = _currentDepthZone != null ? _currentDepthZone.dangerLevel : 0f;
            _debugPassiveSelectionScale = _currentPassiveSelectionScale;
            _debugAggressiveSelectionScale = _currentAggressiveSelectionScale;
            _debugLargeThreatSelectionScale = _currentLargeThreatSelectionScale;

            if (TryResolvePlayerLogicPose(out Vector3 debugPlayerPosition, out _))
            {
                WorldChunkCoordinate playerChunk = WorldChunkCoordinate.FromWorldPosition(debugPlayerPosition, _runtimeChunkSize);
                _debugCurrentChunkX = playerChunk.x;
                _debugCurrentChunkZ = playerChunk.z;
                WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(debugPlayerPosition, _runtimeMacroZoneSize);
                _debugCurrentMacroZoneX = playerMacroZone.x;
                _debugCurrentMacroZoneZ = playerMacroZone.z;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR â€” GIZMOS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying && _playerTransform != null
                ? _playerTransform.position
                : transform.position;
            float innerRadius = Application.isPlaying ? _runtimeSpawnRingInner : spawnRingInner;
            float outerRadius = Application.isPlaying ? _runtimeSpawnRingOuter : spawnRingOuter;
            float cullRadius = Application.isPlaying ? _runtimeKillDistance : killDistance;
            float largeThreatOuter = Application.isPlaying ? _runtimeLargeThreatSpawnOuter : spawnRingOuter;
            float largeThreatCull = Application.isPlaying ? _runtimeLargeThreatKillDistance : killDistance;

            // Spawn ring â€” inner
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
            DrawWireCircle(center, innerRadius, 32);

            // Spawn ring â€” outer
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
            DrawWireCircle(center, outerRadius, 48);

            // Kill distance
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.08f);
            DrawWireCircle(center, cullRadius, 64);

            // Large threats
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.14f);
            DrawWireCircle(center, largeThreatOuter, 72);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
            DrawWireCircle(center, largeThreatCull, 80);

            // Active creatures (Ð² Play Mode)
            if (Application.isPlaying && _activeCreatures != null)
            {
                Gizmos.color = Color.cyan;
                int count = _activeCreatures.Count;
                for (int i = 0; i < count; i++)
                {
                    ActiveCreature c = _activeCreatures[i];
                    if (c.transform != null)
                    {
                        Gizmos.DrawWireSphere(c.transform.position, 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Ð Ð¸ÑÑƒÐµÑ‚ Ð³Ð¾Ñ€Ð¸Ð·Ð¾Ð½Ñ‚Ð°Ð»ÑŒÐ½Ñ‹Ð¹ wireframe-ÐºÑ€ÑƒÐ³ (XZ Ð¿Ð»Ð¾ÑÐºÐ¾ÑÑ‚ÑŒ).
        /// </summary>
        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = Mathf.PI * 2f / segments;

            Vector3 prev = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                Vector3 next = center + new Vector3(
                    cos * radius, 0f,
                    sin * radius);

                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (globalMaxCount    < 1)   globalMaxCount    = 1;
            if (maxSpawnsPerTick  < 1)   maxSpawnsPerTick  = 1;
            if (spawnRingInner    < 10f) spawnRingInner    = 10f;
            if (spawnRingOuter    < spawnRingInner + 10f)
                spawnRingOuter = spawnRingInner + 10f;
            if (killDistance      < spawnRingOuter)
                killDistance = spawnRingOuter + 50f;

            RefreshRuntimeStreamingSettings();

            if (hordeCountMin < 1) hordeCountMin = 1;
            if (hordeCountMax < hordeCountMin) hordeCountMax = hordeCountMin;
            if (hordeRadiusInner < 1f) hordeRadiusInner = 1f;
            if (hordeRadiusOuter < hordeRadiusInner + 1f)
                hordeRadiusOuter = hordeRadiusInner + 1f;
        }
#endif

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            _runtimeSettingsDirty = true;
            RefreshRuntimeStreamingSettings();
            _runtimeSettingsDirty = false;
            _nextRuntimeSettingsRefreshTime = ReadDispatcherTimeSeconds() + RuntimeSettingsRefreshInterval;
        }

        public void SetSpawnRegistry(WorldFaunaSpawnRegistry registry)
        {
            spawnRegistry = registry;
            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        public void SetProceduralStateRegistry(WorldProceduralStateRegistry registry)
        {
            proceduralStateRegistry = registry;
            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void RefreshRuntimeStreamingSettings()
        {
            _runtimeSpawnRingInner = spawnRingInner;
            _runtimeSpawnRingOuter = spawnRingOuter;
            _runtimeKillDistance = killDistance;
            _runtimeChunkSize = 192f;
            _runtimeMacroZoneSize = 768f;
            _runtimeLargeThreatSpawnInner = Mathf.Max(_runtimeSpawnRingOuter + 60f, _runtimeSpawnRingInner + 120f);
            _runtimeLargeThreatSpawnOuter = Mathf.Max(_runtimeLargeThreatSpawnInner + 120f, _runtimeKillDistance);
            _runtimeLargeThreatKillDistance = Mathf.Max(_runtimeLargeThreatSpawnOuter + 120f, _runtimeKillDistance * 1.5f);
            _runtimeGlobalMaxCount = Mathf.Clamp(globalMaxCount, 1, GlobalFaunaHardCap);
            _runtimeMaxSpawnsPerTick = Mathf.Max(1, maxSpawnsPerTick);
            _runtimePerChunkMaxCount = Mathf.Max(4, Mathf.CeilToInt(_runtimeGlobalMaxCount / 5f));
            _runtimeMaxNearbyLargeThreats = 1;
            _runtimeFaunaAnchorChunkDistance = Mathf.Max(1, Mathf.CeilToInt(_runtimeSpawnRingOuter / Mathf.Max(1f, _runtimeChunkSize)));
            _runtimeLargeThreatMacroZoneDistance = 1;

            if (chunkStreamingProfile != null)
            {
                WorldChunkStreamingProfile.LayerProfile faunaLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Fauna);
                WorldChunkStreamingProfile.LayerProfile largeThreatLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.LargeThreats);

                _runtimeChunkSize = Mathf.Max(32f, chunkStreamingProfile.chunkSizeMeters);
                _runtimeMacroZoneSize = Mathf.Max(_runtimeChunkSize, chunkStreamingProfile.macroZoneSizeMeters);

                float fullRadius = Mathf.Max(60f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.5f, faunaLayer.nearRadiusScale));
                float midRadius = Mathf.Max(fullRadius + 30f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.5f, faunaLayer.midRadiusScale));
                float largeThreatNear = Mathf.Max(fullRadius + 60f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.75f, largeThreatLayer.nearRadiusScale) + 40f);
                float largeThreatMid = Mathf.Max(largeThreatNear + 120f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.85f, largeThreatLayer.midRadiusScale));
                float largeThreatFar = Mathf.Max(largeThreatMid + 120f, chunkStreamingProfile.visualResidencyRadius * Mathf.Max(0.9f, largeThreatLayer.farRadiusScale));

                _runtimeSpawnRingInner = Mathf.Clamp(fullRadius * 0.35f, 24f, fullRadius - 10f);
                _runtimeSpawnRingOuter = fullRadius;
                _runtimeKillDistance = midRadius;
                _runtimeLargeThreatSpawnInner = largeThreatNear;
                _runtimeLargeThreatSpawnOuter = largeThreatMid;
                _runtimeLargeThreatKillDistance = largeThreatFar;

                int estimatedLoadedChunks = EstimateChunkCoverage(midRadius, _runtimeChunkSize);
                _runtimeGlobalMaxCount = Mathf.Clamp(Mathf.Max(globalMaxCount, estimatedLoadedChunks * 6), 1, GlobalFaunaHardCap);
                _runtimeMaxSpawnsPerTick = Mathf.Max(maxSpawnsPerTick, Mathf.Clamp(faunaLayer.maxActivationsPerTick / 2, 4, 16));
                _runtimePerChunkMaxCount = Mathf.Clamp(Mathf.CeilToInt(_runtimeGlobalMaxCount / (float)Mathf.Max(1, estimatedLoadedChunks)), 4, 12);
                _runtimeMaxNearbyLargeThreats = Mathf.Clamp(Mathf.Max(1, largeThreatLayer.maxActivationsPerTick / 2), 1, 2);
                _runtimeFaunaAnchorChunkDistance = Mathf.Max(1, Mathf.CeilToInt(midRadius / Mathf.Max(1f, _runtimeChunkSize)));
                _runtimeLargeThreatMacroZoneDistance = Mathf.Clamp(Mathf.CeilToInt(largeThreatMid / Mathf.Max(1f, _runtimeMacroZoneSize)), 1, 2);
            }

            if (_runtimeSpawnRingOuter < _runtimeSpawnRingInner + 10f)
                _runtimeSpawnRingOuter = _runtimeSpawnRingInner + 10f;

            if (_runtimeKillDistance < _runtimeSpawnRingOuter + 10f)
                _runtimeKillDistance = _runtimeSpawnRingOuter + 10f;
            if (_runtimeLargeThreatSpawnOuter < _runtimeLargeThreatSpawnInner + 60f)
                _runtimeLargeThreatSpawnOuter = _runtimeLargeThreatSpawnInner + 60f;
            if (_runtimeLargeThreatKillDistance < _runtimeLargeThreatSpawnOuter + 120f)
                _runtimeLargeThreatKillDistance = _runtimeLargeThreatSpawnOuter + 120f;

            _runtimeKillDistanceSqr = _runtimeKillDistance * _runtimeKillDistance;
            _runtimeLargeThreatKillDistanceSqr = _runtimeLargeThreatKillDistance * _runtimeLargeThreatKillDistance;
            _killDistanceSqr = _runtimeKillDistanceSqr;
            RefreshCreaturePoolWarmupTargets();
        }

        private static int EstimateChunkCoverage(float radius, float chunkSize)
        {
            float safeRadius = Mathf.Max(1f, radius);
            float safeChunkSize = Mathf.Max(1f, chunkSize);
            float coverage = (Mathf.PI * safeRadius * safeRadius) / (safeChunkSize * safeChunkSize);
            return Mathf.Max(1, Mathf.CeilToInt(coverage));
        }

        private bool IsLargeThreatEntry(FaunaBiomeData biomeData, in FaunaEntry entry)
        {
            CreatureArchetypeData archetype = entry.archetype;
            if (archetype == null)
                return false;

            if (biomeData != null && biomeData.CountsAsLargeThreat(archetype))
                return true;

            return archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static bool ShouldBlockEntryWhenPressureDisabled(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            if (archetype.isAggressive)
                return true;

            return archetype.roleType == CreatureRoleType.Hunter ||
                   archetype.roleType == CreatureRoleType.Leviathan;
        }

        private bool CanSpawnLargeThreatNearPlayer(WorldMacroZoneCoordinate spawnMacroZone, Vector3 playerPos)
        {
            if (_currentLargeThreatSelectionScale <= 0.01f || _currentZoneIsSafePocket)
                return false;

            if (GetMacroZoneLargeThreatCount(spawnMacroZone) > 0)
                return false;

            WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(playerPos, _runtimeMacroZoneSize);
            if (spawnMacroZone.ChebyshevDistanceTo(playerMacroZone) > 1)
                return false;

            return CountNearbyLargeThreats(playerMacroZone) < _runtimeMaxNearbyLargeThreats;
        }

        private int CountNearbyLargeThreats(WorldMacroZoneCoordinate playerMacroZone)
        {
            if (_largeThreatCountsPerMacroZone == null || _largeThreatCountsPerMacroZone.Count == 0)
                return 0;

            int total = 0;
            // ZERO GC: Dictionary<long,int>.Enumerator is a struct â€” GetEnumerator() returns by value, no heap alloc.
            // foreach on Dictionary<K,V> is FORBIDDEN (boxes enumerator state). Explicit struct enumerator is ALLOWED.
            Dictionary<long, int>.Enumerator enumerator = _largeThreatCountsPerMacroZone.GetEnumerator();
            while (enumerator.MoveNext())
            {
                WorldMacroZoneCoordinate zone = DecomposeMacroZoneKey(enumerator.Current.Key);
                if (zone.ChebyshevDistanceTo(playerMacroZone) <= 1)
                    total += enumerator.Current.Value;
            }
            enumerator.Dispose();

            return total;
        }

        private int GetChunkCreatureCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return 0;

            long key = ComposeChunkKey(chunkCoord);
            return _countsPerChunk.TryGetValue(key, out int count) ? count : 0;
        }

        private int GetMacroZoneLargeThreatCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return 0;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            return _largeThreatCountsPerMacroZone.TryGetValue(key, out int count) ? count : 0;
        }

        private int GetPredatorSectorCount(WorldChunkCoordinate chunkCoord)
        {
            if (_predatorCountsPerSector == null)
                return 0;

            long key = ComposePredatorSectorKey(chunkCoord);
            return _predatorCountsPerSector.TryGetValue(key, out int count) ? count : 0;
        }

        private void IncrementChunkCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return;

            long key = ComposeChunkKey(chunkCoord);
            if (_countsPerChunk.TryGetValue(key, out int count))
                _countsPerChunk[key] = count + 1;
            else
                _countsPerChunk.Add(key, 1);
        }

        private void IncrementPredatorSectorCount(WorldChunkCoordinate chunkCoord)
        {
            if (_predatorCountsPerSector == null)
                return;

            long key = ComposePredatorSectorKey(chunkCoord);
            if (_predatorCountsPerSector.TryGetValue(key, out int count))
                _predatorCountsPerSector[key] = count + 1;
            else
                _predatorCountsPerSector.Add(key, 1);
        }

        private void DecrementChunkCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return;

            long key = ComposeChunkKey(chunkCoord);
            if (!_countsPerChunk.TryGetValue(key, out int count))
                return;

            count--;
            if (count <= 0)
                _countsPerChunk.Remove(key);
            else
                _countsPerChunk[key] = count;
        }

        private void DecrementPredatorSectorCount(WorldChunkCoordinate chunkCoord)
        {
            if (_predatorCountsPerSector == null)
                return;

            long key = ComposePredatorSectorKey(chunkCoord);
            if (!_predatorCountsPerSector.TryGetValue(key, out int count))
                return;

            count--;
            if (count <= 0)
                _predatorCountsPerSector.Remove(key);
            else
                _predatorCountsPerSector[key] = count;
        }

        private void IncrementMacroZoneCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            if (_largeThreatCountsPerMacroZone.TryGetValue(key, out int count))
                _largeThreatCountsPerMacroZone[key] = count + 1;
            else
                _largeThreatCountsPerMacroZone.Add(key, 1);
        }

        private void DecrementMacroZoneCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            if (!_largeThreatCountsPerMacroZone.TryGetValue(key, out int count))
                return;

            count--;
            if (count <= 0)
                _largeThreatCountsPerMacroZone.Remove(key);
            else
                _largeThreatCountsPerMacroZone[key] = count;
        }

        private static long ComposeChunkKey(WorldChunkCoordinate chunkCoord)
        {
            return ((long)chunkCoord.x << 32) ^ (uint)chunkCoord.z;
        }

        private static long ComposeMacroZoneKey(WorldMacroZoneCoordinate macroZoneCoord)
        {
            return ((long)macroZoneCoord.x << 32) ^ (uint)macroZoneCoord.z;
        }

        private long ComposePredatorSectorKey(WorldChunkCoordinate chunkCoord)
        {
            float sectorScale = 1000f / Mathf.Max(1f, _runtimeChunkSize);
            int sectorX = Mathf.FloorToInt(chunkCoord.x / sectorScale);
            int sectorZ = Mathf.FloorToInt(chunkCoord.z / sectorScale);
            return ((long)sectorX << 32) ^ (uint)sectorZ;
        }

        private static void WritePoolSlotPosition(ref PoolSlotData slotData, in AbsoluteUniversePosition position)
        {
            slotData.AupCell = new int3((int)position.GridX, (int)position.GridY, (int)position.GridZ);
            slotData.LocalOffset = new float3(position.LocalX, position.LocalY, position.LocalZ);
        }

        private static AbsoluteUniversePosition ReadPoolSlotPosition(in PoolSlotData slotData)
        {
            return new AbsoluteUniversePosition
            {
                GridX = slotData.AupCell.x,
                GridY = slotData.AupCell.y,
                GridZ = slotData.AupCell.z,
                LocalX = slotData.LocalOffset.x,
                LocalY = slotData.LocalOffset.y,
                LocalZ = slotData.LocalOffset.z
            };
        }

        private static WorldMacroZoneCoordinate DecomposeMacroZoneKey(long key)
        {
            int x = (int)(key >> 32);
            int z = (int)key;
            return new WorldMacroZoneCoordinate(x, z);
        }
    }
}

