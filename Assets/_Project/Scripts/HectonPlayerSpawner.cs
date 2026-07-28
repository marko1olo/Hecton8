// ============================================================================
// HECTON-8 - HectonPlayerSpawner.cs v3.1
//
// Safe async player spawner for a procedurally generated MapMagic 2 world.
// Unity 6 Awaitable API. Zero-GC in async search loops.
//
// v3.1 critical fix:
//   - Phase 2 no longer retries the same missing-terrain spiral point forever.
//   - Each spiral point has a bounded retry count before the search advances.
//   - A global realtime timeout forces fallback spawn on slow or failed terrain.
//   - Phase 3 uses the same timeout guard for slow nearshore searches.
//
// Retained:
//   - Archimedean spiral search, fallback, nearshore search.
//   - Zero-GC ground-probe fields: _groundProbeOrigin and _hitInfo.
//   - Rigidbody teleport with kinematic/interpolation/velocity reset.
//   - Unity 6 Awaitable API.
//
// Algorithm:
//   Phase 1 - wait for center terrain generation.
//   Phase 2 - search shallow water by spiral with bounded retries.
//   Phase 3 - search land, then nearest shallow nearshore water.
//   Phase 4 - emergency water-level fallback at world center.
//
// Target hardware: NVIDIA GeForce MX350.
// ============================================================================

using System;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Gameplay;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Safe async player spawner for the procedurally generated world.
/// <para>
/// Finds a shallow water spawn point near land and places the player at water level.
/// </para>
/// <para>
/// <b>Usage from GameBootstrapper:</b>
/// <code>
/// var spawner = sceneBootstrap.PlayerSpawner;
/// await spawner.SpawnPlayerAsync(ct);
/// </code>
/// </para>
/// </summary>
public class HectonPlayerSpawner : MonoBehaviour
{
    private const int SpawnAngleLutSize = 1024;
    private const int SpawnAngleLutMask = SpawnAngleLutSize - 1;
    private const float SpawnAngleLutScale = SpawnAngleLutSize / 360f;
    private const float SpawnAngleLutSinDelta = 0.00613588465f;
    private const float SpawnAngleLutCosDelta = 0.99998117528f;
    private const float SpawnSearchTwoPi = 6.2831853071795864769f;
    private const float NearshoreDiagonal = 0.70710678118f;
    private const uint KccVelocitySpawnerMaxAgeFrames = 12u;
    private const float DefaultWaterLevel = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
    private const float DefaultMaxSpawnDepthMeters = 100f;
    private const string ProductionPlayerPrefabGuid = "1c4db7a430141e5408e01b6ce4ed19d7";

    // COLD ALLOC: float[1024] — spawn spiral trigonometry lookup — owner: HectonPlayerSpawner
    private static readonly float[] s_spawnSinLut = new float[SpawnAngleLutSize];
    // COLD ALLOC: float[1024] — spawn spiral trigonometry lookup — owner: HectonPlayerSpawner
    private static readonly float[] s_spawnCosLut = new float[SpawnAngleLutSize];
    private static bool s_spawnTrigLutInitialized;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawner(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Hecton8.Core.H8Debug.Log(message);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawnerWarning(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Hecton8.Core.H8Debug.LogWarning(message);
#endif
    }

    /// <summary>Stable telemetry hash for "the player spawn was refused". FNV-style literal, 'PSPN'.</summary>
    private const uint SpawnRejectedWarningHash = 0x5053504Eu;

    /// <summary>Stable telemetry hash for "more than one player spawner is alive". Literal 'PSDU'.</summary>
    private const uint DuplicateSpawnerWarningHash = 0x50534455u;

    /// <summary>
    /// How many spawners have run Awake this session. Not an ownership latch - nothing is aborted or
    /// destroyed on the strength of it - only a count, so that a second spawner cannot arrive unnoticed.
    /// </summary>
    private static int s_liveSpawnerCount;

    /// <summary>
    /// Cleared per play session. This project runs with domain reload DISABLED
    /// (ProjectSettings/EditorSettings.asset, m_EnterPlayModeOptions: 1), so a plain static would carry
    /// the previous session's count into the next one and report a duplicate that does not exist - or,
    /// worse, stay quiet about one that does.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnerInstanceCount()
    {
        s_liveSpawnerCount = 0;
    }

    /// <summary>
    /// Reports a SECOND live spawner. Measured, not hypothetical: a headless route run logged
    /// "Production Player.prefab cold-instantiated" TWICE against a single
    /// "Step 0: Loading 02_HECTON_WORLD", so two spawners each cold-instantiated their own Player into
    /// the world. One of those players is then destroyed, and the spawner the bootstrap goes on to call
    /// is the one holding the destroyed reference - which is where the scene gate's PLAYER_NULL comes
    /// from. Two owners of one runtime truth is a MASTER_PLAN.md:19 violation on its own terms.
    ///
    /// Deliberately does NOT abort, disable or destroy the duplicate, unlike
    /// GameBootstrapper.AbortDuplicateRuntimeOwner. Silently picking a winner would trade a loud defect
    /// for a quiet one. Naming it is the fix that is provable today.
    ///
    /// DO NOT go looking for a second spawner in the scene - there isn't one, and I checked before
    /// writing this. A format-agnostic object-model census with a PASSING instrument self-test reports
    /// exactly ONE authored instance: HectonPlayerSpawner 00_BOOTSTRAP=absent 01_MAIN_MENU=absent
    /// 02_HECTON_WORLD=1(enabled 1, active 1) at GameObject 'PlayerSpawner'. No code path creates one
    /// either - there is no AddComponent&lt;HectonPlayerSpawner&gt; and no Instantiate of a spawner prefab
    /// anywhere under Assets/.
    /// One authored spawner plus two live Awakes in a single run therefore means the WORLD SCENE IS
    /// BEING LOADED TWICE - a second load builds a second instance generation, the second spawner
    /// cold-instantiates a second Player, and the FIRST player dies with the first scene. That is where
    /// "the object Awake accepted was DESTROYED before SpawnPlayerAsync ran" comes from, and the real
    /// defect is the duplicate scene load, not duplicate authoring.
    /// </summary>
    private void ReportIfDuplicateSpawner()
    {
        s_liveSpawnerCount++;
        if (s_liveSpawnerCount <= 1)
            return;

        GlobalTelemetryBus.PublishPerformanceWarning(
            DuplicateSpawnerWarningHash,
            0u,
            s_liveSpawnerCount);

        LogSpawnerError(
            "[HectonPlayerSpawner] DUPLICATE SPAWNER: this is live spawner #" +
            s_liveSpawnerCount.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " this session, on GameObject '" + gameObject.name + "' in scene '" + gameObject.scene.name +
            "'. Each spawner cold-instantiates its own Player when it cannot resolve one, so the world " +
            "now holds more than one player and only one of them can survive.",
            this);
    }

    /// <summary>
    /// Reports a refused player spawn on a route that SURVIVES A RELEASE PLAYER BUILD.
    ///
    /// Every other reporting helper on this class carries [Conditional("UNITY_EDITOR")] plus
    /// [Conditional("DEVELOPMENT_BUILD")], so in a shipped game the player simply never appeared and
    /// nothing anywhere said why. GlobalTelemetryBus.PublishPerformanceWarning has no [Conditional]
    /// attribute, so it is the one surface that still speaks in a release build. The scalar carries the
    /// case, because a telemetry consumer cannot read a string:
    ///   0 = the reference was never set,
    ///   1 = it was set and the object was destroyed before the spawn call,
    ///   2 = the object is alive but fails an authority condition.
    /// Cases 1 and 2 are different defects - a lifetime bug and a prefab bug - and they were previously
    /// reported with the same sentence.
    /// </summary>
    private static void LogSpawnerAuthorityRejection(
        bool referenceWasSet,
        bool destroyedSinceAwake,
        string authorityReason)
    {
        float caseScalar = !referenceWasSet ? 0f : (destroyedSinceAwake ? 1f : 2f);
        Hecton8.Core.GlobalTelemetryBus.PublishPerformanceWarning(
            SpawnRejectedWarningHash,
            0u,
            caseScalar);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string detail = !referenceWasSet
            ? "the Rigidbody reference was never set"
            : (destroyedSinceAwake
                ? "the player object Awake accepted was DESTROYED before SpawnPlayerAsync ran - a lifetime defect, not a prefab defect"
                : "the player object is alive but fails an authority condition");
        Hecton8.Core.H8Debug.LogError(
            "[HectonPlayerSpawner] Spawn rejected: " + detail + ". reason=" + authorityReason);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawnerError(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Hecton8.Core.H8Debug.LogError(message);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawnerError(string message, UnityEngine.Object context)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Hecton8.Core.H8Debug.LogError(message, context);
#endif
    }

    // ══════════════════════════════════════════════════════════════
    // Inspector: player
    // ══════════════════════════════════════════════════════════════

    [Header("=== Player ===")]
    [Tooltip("Production Player.prefab source. Must resolve to GUID 1c4db7a430141e5408e01b6ce4ed19d7 when assigned through Unity.")]
    [SerializeField] private GameObject productionPlayerPrefab;

    // RUNTIME-RESOLVED CACHE, NOT A TUNING VALUE, and normally NOT authored. The old tooltip named the
    // Inspector as THE source of this reference; on every route that can be measured statically it is
    // not a source at all. This spawner's GUID (560e83b763132d2418e071332d17b172) appears in 0 of the
    // 27 text scenes and 0 of the 962 prefabs, and Awake writes this field from three runtime routes.
    //
    // It stays [SerializeField] deliberately rather than becoming a plain private field: the production
    // world scene is serialized ForceBinary, so a hex-text GUID search cannot prove absence there, and
    // Awake DOES honour an authored seed when it passes production authority validation. Dropping the
    // attribute on unprovable absence would silently discard such a seed.
    //
    // Awake resolution order: (1) this serialized seed, kept only if TryAcceptProductionPlayerRigidbody
    // accepts it; (2) IPlayerRuntimeContext.PlayerRigidbody; (3) GameBootstrapper current player
    // transform; (4) cold Instantiate of productionPlayerPrefab.
    [Tooltip("Optional production player Rigidbody seed. Normally resolved at runtime, not authored.")]
    [SerializeField] private Rigidbody playerRigidbody;

    // ══════════════════════════════════════════════════════════════
    // Inspector: water settings
    // ══════════════════════════════════════════════════════════════

    [Header("=== Water Settings ===")]
    [Tooltip("Sea level in world-space Y.")]
    [SerializeField] private float waterLevel = DefaultWaterLevel;

    [Tooltip("Minimum valid sea floor height under water.")]
    [SerializeField] private float minSeaFloorHeight = DefaultWaterLevel - DefaultMaxSpawnDepthMeters;

    // ══════════════════════════════════════════════════════════════
    // Inspector: ground probe settings
    // ══════════════════════════════════════════════════════════════

    [Header("=== Ground Probe Settings ===")]
    [Tooltip("World-space height used for downward cached terrain probes.")]
    [SerializeField] private float groundProbeOriginHeight = 10000f;

    // ══════════════════════════════════════════════════════════════
    // Inspector: spawn search settings
    // ══════════════════════════════════════════════════════════════

    [Header("=== Spawn Search Settings ===")]
    [Tooltip("Initial XZ search origin.")]
    [SerializeField] private Vector2 searchOrigin = Vector2.zero;

    [Tooltip("Spiral step in meters.")]
    [SerializeField] private float spiralStep = 75f;

    [Tooltip("Maximum number of spiral points to test.")]
    [SerializeField] private int maxSpiralPoints = 500;

    [Tooltip("Player height offset above water or ground.")]
    [SerializeField] private float spawnHeightOffset = 2f;

    [Tooltip("Delay between ground-probe retries while waiting for MapMagic terrain, in seconds.")]
    [SerializeField] private float retryDelay = 0.5f;

    [Tooltip("Maximum terrain generation wait in phase 1, in seconds.")]
    [SerializeField] private float maxWaitTime = 60f;

    // ══════════════════════════════════════════════════════════════
    // Inspector: deadlock protection
    // ══════════════════════════════════════════════════════════════

    [Header("=== Deadlock Protection (v3.1) ===")]
    [Tooltip("Maximum ground-probe retries for one spiral point.\n" +
             "If terrain does not appear within maxRetriesPerPoint * retryDelay seconds, the point is skipped.\n" +
             "10 retries * 0.5s = 5 seconds maximum per point.")]
    [SerializeField] private int maxRetriesPerPoint = 10;

    [Tooltip("Global timeout for the whole SpawnPlayerAsync operation, in seconds.\n" +
             "Covers all phases. On timeout, emergency fallback spawn is used.\n" +
             "Prevents all terrain-generation wait locks.")]
    [SerializeField] private float globalTimeoutSec = 120f;

    // ══════════════════════════════════════════════════════════════
    // Cached fields: zero-GC
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Predallotsirovannaya tochka nachala cached terrain probe.
    /// Pereispolzuetsya vo vseh poverhnostnyh probah bez sozdaniya novyh Vector3.
    /// </summary>
    private Vector3 _groundProbeOrigin;

    /// <summary>
    /// Predallotsirovannaya pozitsiya spavna.
    /// Zapolnyaetsya pri nahozhdenii validnoy tochki.
    /// </summary>
    private Vector3 _spawnPosition;

    /// <summary>
    /// Predallotsirovannaya struktura rezultata cached terrain-probe.
    /// Zapolnyaetsya bez PhysX i bez Unity hit DTO.
    /// </summary>
    private SpawnGroundHit _hitInfo;

    /// <summary>
    /// Vremya nachala operatsii SpawnPlayerAsync (realtimeSinceStartup).
    /// Ispolzuetsya dlya proverki globalnogo taymauta.
    /// </summary>
    private float _operationStartTime;
    private HectonPlayerMovement _playerMovement;
    private Vector3 _teleportPreservedLocalVelocity;
    private Vector3 _teleportPreservedAngularVelocity;
    private Vector3 _teleportPreservedPlatformVelocity;

    // ══════════════════════════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Rezultat proverki konkretnoy tochki karty.
    /// </summary>
    private enum SpawnSearchResult
    {
        /// <summary>Terreyn ne obnaruzhen (chank esche ne sgenerirovan).</summary>
        NoTerrain,

        /// <summary>Slishkom gluboko (dno nizhe <see cref="minSeaFloorHeight"/>).</summary>
        DeepWater,

        /// <summary>Susha (zemlya vyshe urovnya morya).</summary>
        AboveWater,

        /// <summary>Melkaya voda vblizi sushi — idealno dlya spavna.</summary>
        ValidShallowWater
    }

    private struct SpawnGroundHit
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
    }

    // ══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Validatsiya ssylok s avtomaticheskim poiskom igroka v stsene.
    /// </summary>
    private void Awake()
    {
        ReportIfDuplicateSpawner();

        if (!s_spawnTrigLutInitialized)
        {
            InitializeSpawnTrigLut();
        }

        IPlayerRuntimeContext playerContext = ResolveProductionPlayerContext();

        // Whether a reference EXISTED and this method threw it away, versus never having one at all.
        // The two states are indistinguishable from `playerRigidbody == null` alone, and the fallback
        // log below used to report the second one unconditionally - including when the first was true.
        bool serializedRigidbodyRejected = false;

        if (playerRigidbody != null && !TryAcceptProductionPlayerRigidbody(playerRigidbody, out _playerMovement))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSpawnerError(
                "[HectonPlayerSpawner] Assigned Rigidbody is not the production player authority root: " +
                ProductionPlayerAuthorityUtility.DescribeProductionPlayerAuthorityFailure(
                    playerRigidbody.gameObject),
                this);
#endif
            serializedRigidbodyRejected = true;
            playerRigidbody = null;
            _playerMovement = null;
        }

        if (playerRigidbody == null && playerContext != null)
            playerRigidbody = playerContext.PlayerRigidbody;
        if (_playerMovement == null && playerContext != null)
            _playerMovement = playerContext.PlayerMovement;
        // Fall back to the bootstrap current-player route. Two routes have already been tried above.
        if (playerRigidbody == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // This branch is reached from three distinct states and the old message named only one of
            // them - "Rigidbody is not assigned in the Inspector" - as though it were the cause. That is
            // actively wrong in the case the code produces itself: when an assigned reference FAILED
            // authority validation and was cleared above, the log told the reader to go populate an
            // inspector field that had already been populated. All three branches are compile-time
            // string constants, so naming the true state allocates nothing.
            LogSpawner(
                serializedRigidbodyRejected
                    ? "[HectonPlayerSpawner] Serialized Rigidbody failed production authority validation and was cleared. Trying to resolve the current player through GameBootstrapper."
                    : playerContext == null
                        ? "[HectonPlayerSpawner] No serialized Rigidbody and no player runtime context was available. Trying to resolve the current player through GameBootstrapper."
                        : "[HectonPlayerSpawner] No serialized Rigidbody and the player runtime context supplied none. Trying to resolve the current player through GameBootstrapper.");
#endif

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransformRoot) &&
                playerTransformRoot != null)
            {
                if (!TryAcceptProductionPlayerTransform(
                        playerTransformRoot,
                        out playerRigidbody,
                        out _playerMovement))
                {
                    playerRigidbody = null;
                    _playerMovement = null;
                }

                if (playerRigidbody != null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogSpawner(
                        $"[HectonPlayerSpawner] Rigidbody resolved through GameBootstrapper on \"{playerRigidbody.gameObject.name}\".");
#endif
                }
                else
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogSpawnerWarning(
                        $"[HectonPlayerSpawner] Current player \"{playerTransformRoot.name}\" has no Rigidbody on root or children.");
#endif
                }
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawnerWarning(
                    "[HectonPlayerSpawner] GameBootstrapper did not provide the current player.");
#endif
            }
        }
        else if (_playerMovement == null)
        {
            if (!TryAcceptProductionPlayerRigidbody(playerRigidbody, out _playerMovement))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawnerError(
                    "[HectonPlayerSpawner] Player Rigidbody reference failed production authority validation.",
                    this);
#endif
                playerRigidbody = null;
                _playerMovement = null;
            }
        }

        // ── Finalnaya proverka posle vseh popytok poiska ──
        if (playerRigidbody == null &&
            TryInstantiateProductionPlayerPrefab(out playerRigidbody, out _playerMovement))
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSpawner(
                "[HectonPlayerSpawner] Production Player.prefab cold-instantiated through spawner source route.");
#endif
        }

        if (playerRigidbody == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Named two routes when four were actually attempted, so a reader chasing this error was
            // told the prefab-instantiate route had not been tried.
            LogSpawnerError(
                "[HectonPlayerSpawner] Player Rigidbody not found. All four routes failed: serialized " +
                "seed, IPlayerRuntimeContext, GameBootstrapper current player, and cold Instantiate of " +
                "productionPlayerPrefab. Spawn is impossible.",
                this);
#endif
            enabled = false;
        }
    }

    public async Awaitable SpawnPlayerAsync(System.Threading.CancellationToken ct)
    {
        if (!TryAcceptProductionPlayerRigidbody(playerRigidbody, out _playerMovement))
        {
            // Awake already accepted a player through this exact predicate - either the Inspector
            // reference, the bootstrap lookup, or a cold Instantiate of Player.prefab, and the
            // "rejected after instantiate" path would have logged and destroyed a bad one. So a
            // rejection HERE means the reference stopped being valid between Awake and the spawn call,
            // and the old message - which only ever said "authority is missing" - could not tell the
            // difference between "never had it" and "had it and lost it".
            //
            // ReferenceEquals sees the real managed reference, while Unity's == overload reports a
            // destroyed object as null. The two disagreeing IS the destroyed case, and naming it is the
            // whole point: a missing component is a prefab defect, a destroyed object is a lifetime
            // defect, and they have nothing to do with each other.
            bool referenceWasSet = !ReferenceEquals(playerRigidbody, null);
            bool destroyedSinceAwake = referenceWasSet && playerRigidbody == null;
            string authorityReason = destroyedSinceAwake || !referenceWasSet
                ? "PLAYER_RIGIDBODY_NULL"
                : ProductionPlayerAuthorityUtility.DescribeProductionPlayerAuthorityFailure(
                    playerRigidbody.gameObject);

            LogSpawnerAuthorityRejection(referenceWasSet, destroyedSinceAwake, authorityReason);
            return;
        }

        RefreshWaterSurfaceFromRuntimeWaterline();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogSpawner("[HectonPlayerSpawner] Starting safe spawn point search.");
#endif

        // ── Zasekaem globalnyy taymer (v3.1) ──
        _operationStartTime = Time.realtimeSinceStartup;

        // ── Podgotovka Rigidbody k teleportu ──
        PrepareRigidbodyForTeleport();

        // ══════════════════════════════════════════════════════════
        //  FAZA 1: Ozhidanie generatsii terreyna v tsentre karty
        // ══════════════════════════════════════════════════════════

        bool terrainReady = false;
        float waitTimer = 0f;

        _groundProbeOrigin.Set(searchOrigin.x, groundProbeOriginHeight, searchOrigin.y);

        while (!terrainReady)
        {
            ct.ThrowIfCancellationRequested();

            // ── Globalnyy taymaut (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawnerError(
                    $"[HectonPlayerSpawner] Global timeout ({globalTimeoutSec}s) in phase 1. Using fallback spawn.");
#endif
                ForceFallbackSpawn();
                return;
            }

            // R99: a ground hit alone is not physics readiness — the chunk must have published an active
            // TerrainCollider. See IsSpawnPointPhysicsReady.
            if (TryResolveGroundHit(out _hitInfo) && IsSpawnPointPhysicsReady(searchOrigin.x, searchOrigin.y))
            {
                terrainReady = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawner(
                    $"[HectonPlayerSpawner] Terrain found at map center. Height: {_hitInfo.point.y:F1}");
#endif
            }
            else
            {
                waitTimer += retryDelay;

                if (waitTimer >= maxWaitTime)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    LogSpawnerError(
                        $"[HectonPlayerSpawner] Timeout ({maxWaitTime}s): terrain not generated. Spawning at center water level.");
#endif
                    ForceFallbackSpawn();
                    return;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawner(
                    $"[HectonPlayerSpawner] Terrain not ready; waiting ({waitTimer:F1}s).");
#endif
                await Awaitable.WaitForSecondsAsync(retryDelay, cancellationToken: ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  FAZA 2: Poisk melkovodya po Arhimedovoy spirali
        // ══════════════════════════════════════════════════════════

        // Snachala proveryaem tsentralnuyu tochku
        SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);

        if (centerResult == SpawnSearchResult.ValidShallowWater &&
            IsSpawnPointPhysicsReady(_spawnPosition.x, _spawnPosition.z))
        {
            TeleportPlayer(_spawnPosition);
            return;
        }

        // ── Initsializatsiya spirali ──
        float safeSpiralStep = Mathf.Max(spiralStep, 0.0001f);
        float inverseSpiralStep = 1f / safeSpiralStep;
        int spiralIndex = 0;
        float angleStep = 45f;
        float currentAngle = 0f;
        float currentRadius = safeSpiralStep;
        int pointsPerRing = 8;
        int pointInRing = 0;
        int retryCount = 0;  // v3.1: per-point retry counter

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // ── Globalnyy taymaut (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawnerError(
                    $"[HectonPlayerSpawner] Global timeout ({globalTimeoutSec}s) in phase 2 at point {spiralIndex}/{maxSpiralPoints}. Using fallback spawn.");
#endif
                ForceFallbackSpawn();
                return;
            }

            // Vychislyaem X, Z po spirali
            ResolveSpawnDegreesSinCosFast(currentAngle, out float sinAngle, out float cosAngle);
            float testX = searchOrigin.x + cosAngle * currentRadius;
            float testZ = searchOrigin.y + sinAngle * currentRadius;

            _groundProbeOrigin.Set(testX, groundProbeOriginHeight, testZ);

            if (TryResolveGroundHit(out _hitInfo))
            {
                // Ground probe succeeded - reset the per-point retry counter.
                retryCount = 0;

                SpawnSearchResult result = EvaluatePointFromHit(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater &&
                    IsSpawnPointPhysicsReady(_spawnPosition.x, _spawnPosition.z))
                {
                    TeleportPlayer(_spawnPosition);
                    return;
                }

                // Tochka ne podhodit (susha/gluboko) — perehodim k sleduyuschey
            }
            else
            {
                // ── Terreyn ne nayden — per-point retry (v3.1) ──
                retryCount++;

                if (retryCount < maxRetriesPerPoint)
                {
                    // Esche est popytki — zhdem i povtoryaem TU ZhE tochku
                    await Awaitable.WaitForSecondsAsync(retryDelay, cancellationToken: ct);
                    continue; // NE inkrementiruem spiralIndex — povtoryaem tochku
                }

                // ── Popytki ischerpany — propuskaem tochku (v3.1) ──
                // spiralIndex++ proizoydet nizhe cherez AdvanceSpiral.
                retryCount = 0;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawner(
                    $"[HectonPlayerSpawner] Spiral point #{spiralIndex} ({testX:F0}, {testZ:F0}) has no terrain after {maxRetriesPerPoint} retries. Skipping.");
#endif
            }

            // ── Perehodim k sleduyuschey tochke spirali ──
            // Etot kod GARANTIROVANNO vypolnyaetsya pri kazhdom prohode,
            // krome sluchaya continue vyshe (kotoryy imeet limit retryCount).
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                pointInRing = 0;
                currentRadius += safeSpiralStep;

                pointsPerRing = ResolveSpiralPointsPerRing(currentRadius, inverseSpiralStep);
                angleStep = 360f * (1f / pointsPerRing);
                currentAngle = 0f;
            }

            // Kazhdye 16 tochek — otdaem kadr Unity (plavnyy loading screen)
            if ((spiralIndex & 15) == 0)
            {
                await AwaitableDebtMonitor.NextFrameAsync(ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  FAZA 3: Fallback — poisk sushi → nearshore melkovode
        //
        //  v3.1: Dobavlen globalnyy taymaut. Faza 3 v v3.0
        //  ne imela continue-baga, no nearshore search (8 napravleniy
        //  × 20 shagov = 160 reykastov na tochku) mozhet byt medlennym
        //  na ogromnyh kartah. Globalnyy taymaut zaschischaet ot etogo.
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogSpawnerWarning(
            "[HectonPlayerSpawner] No shallow nearshore water found after full spiral scan. Searching for land.");
#endif

        // Sbros parametrov spirali dlya vtorogo prohoda
        currentAngle = 0f;
        currentRadius = safeSpiralStep;
        pointsPerRing = 8;
        pointInRing = 0;
        angleStep = 45f;
        spiralIndex = 0;

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // ── Globalnyy taymaut (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                LogSpawnerError(
                    $"[HectonPlayerSpawner] Global timeout ({globalTimeoutSec}s) in phase 3 at point {spiralIndex}/{maxSpiralPoints}. Using fallback spawn.");
#endif
                ForceFallbackSpawn();
                return;
            }

            ResolveSpawnDegreesSinCosFast(currentAngle, out float sinAngle, out float cosAngle);
            float testX = searchOrigin.x + cosAngle * currentRadius;
            float testZ = searchOrigin.y + sinAngle * currentRadius;

            _groundProbeOrigin.Set(testX, groundProbeOriginHeight, testZ);

            if (TryResolveGroundHit(out _hitInfo))
            {
                float groundY = _hitInfo.point.y;

                // Nashli sushu — ischem blizhayshuyu tochku melkovodya
                if (groundY > waterLevel)
                {
                    bool foundNearshore = TryFindNearshorePoint(testX, testZ);
                    if (foundNearshore &&
                        IsSpawnPointPhysicsReady(_spawnPosition.x, _spawnPosition.z))
                    {
                        TeleportPlayer(_spawnPosition);
                        return;
                    }
                }
            }
            // v3.1: Faza 3 NE zhdet generatsii terreyna — prosto propuskaet.
            // Esli terreyna net — perehodim k sleduyuschey tochke nemedlenno.

            // ── Vsegda prodvigaem spiral (net continue → net dedloka) ──
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                pointInRing = 0;
                currentRadius += safeSpiralStep;
                pointsPerRing = ResolveSpiralPointsPerRing(currentRadius, inverseSpiralStep);
                angleStep = 360f * (1f / pointsPerRing);
                currentAngle = 0f;
            }

            // Kazhdye 16 tochek — yield dlya plavnosti
            if ((spiralIndex & 15) == 0)
            {
                await AwaitableDebtMonitor.NextFrameAsync(ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  FAZA 4: Absolyutnyy fallback
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogSpawnerWarning(
            "[HectonPlayerSpawner] No land or shallow water found. Spawning at center water level.");
#endif
        ForceFallbackSpawn();
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — GLOBAL TIMEOUT CHECK (v3.1)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Proveryaet, prevyshen li globalnyy taymaut operatsii.
    /// Ispolzuet Time.realtimeSinceStartup — ne zavisit ot Time.timeScale.
    /// Zero GC: float comparison.
    /// </summary>
    /// <returns>true esli operatsiya vypolnyaetsya dolshe globalTimeoutSec.</returns>
    private bool IsGlobalTimeoutExceeded()
    {
        return (Time.realtimeSinceStartup - _operationStartTime) >= globalTimeoutSec;
    }

    private static int ResolveSpiralPointsPerRing(float currentRadius, float inverseSpiralStep)
    {
        int resolved = (int)((SpawnSearchTwoPi * currentRadius * inverseSpiralStep) + 0.5f);
        return resolved > 8 ? resolved : 8;
    }

    private static void ResolveSpawnDegreesSinCosFast(float degrees, out float sin, out float cos)
    {
        if (!s_spawnTrigLutInitialized)
        {
            InitializeSpawnTrigLut();
        }

        if (!float.IsFinite(degrees))
        {
            sin = 0f;
            cos = 1f;
            return;
        }

        float scaled = degrees * SpawnAngleLutScale;
        int rounded = (int)(scaled >= 0f ? scaled + 0.5f : scaled - 0.5f);
        int index = rounded & SpawnAngleLutMask;
        sin = s_spawnSinLut[index];
        cos = s_spawnCosLut[index];
    }

    private static void InitializeSpawnTrigLut()
    {
        float sin = 0f;
        float cos = 1f;

        for (int i = 0; i < SpawnAngleLutSize; i++)
        {
            s_spawnSinLut[i] = sin;
            s_spawnCosLut[i] = cos;

            float nextSin = sin * SpawnAngleLutCosDelta + cos * SpawnAngleLutSinDelta;
            float nextCos = cos * SpawnAngleLutCosDelta - sin * SpawnAngleLutSinDelta;
            sin = nextSin;
            cos = nextCos;
        }

        s_spawnTrigLutInitialized = true;
    }

    private static void ResolveNearshoreDirection(int directionIndex, out float dirX, out float dirZ)
    {
        switch (directionIndex & 7)
        {
            case 0:
                dirX = 1f;
                dirZ = 0f;
                return;
            case 1:
                dirX = NearshoreDiagonal;
                dirZ = NearshoreDiagonal;
                return;
            case 2:
                dirX = 0f;
                dirZ = 1f;
                return;
            case 3:
                dirX = -NearshoreDiagonal;
                dirZ = NearshoreDiagonal;
                return;
            case 4:
                dirX = -1f;
                dirZ = 0f;
                return;
            case 5:
                dirX = -NearshoreDiagonal;
                dirZ = -NearshoreDiagonal;
                return;
            case 6:
                dirX = 0f;
                dirZ = -1f;
                return;
            default:
                dirX = NearshoreDiagonal;
                dirZ = -NearshoreDiagonal;
                return;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — EVALUATION
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Otsenivaet tochku karty cherez cached terrain height.
    /// Pri nahozhdenii validnogo melkovodya zapolnyaet _spawnPosition.
    /// Zero-GC: no PhysX query, no managed allocation.
    /// </summary>
    private bool TryResolveGroundHit(out SpawnGroundHit hit)
    {
        hit = default;
        if (!TryResolveCachedTerrainHeight(_groundProbeOrigin.x, _groundProbeOrigin.z, out float groundY))
            return false;

        hit.point = new Vector3(_groundProbeOrigin.x, groundY, _groundProbeOrigin.z);
        hit.normal = Vector3.up;
        hit.distance = Mathf.Max(0f, _groundProbeOrigin.y - groundY);
        return true;
    }

        private static bool TryResolveCachedTerrainHeight(float x, float z, out float groundY)
        {
            groundY = 0f;
            HectonMapMagicVegetationBridge vegetationBridge = null;
            return WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref vegetationBridge) &&
                   vegetationBridge.TryGetCachedTerrainHeight(x, z, out groundY) &&
                   float.IsFinite(groundY);
        }

    private void RefreshWaterSurfaceFromRuntimeWaterline()
    {
        if (!TryResolveOceanWaterLevel(out float resolvedWaterLevel) &&
            !TryResolveTerrainBridgeWaterLevel(out resolvedWaterLevel))
        {
            return;
        }

        float previousWaterLevel = TryResolveWaterLevel(waterLevel, out float resolvedPreviousWaterLevel)
            ? resolvedPreviousWaterLevel
            : DefaultWaterLevel;
        float spawnDepthWindow = previousWaterLevel - minSeaFloorHeight;
        if (!math.isfinite(spawnDepthWindow) || spawnDepthWindow <= 0f || spawnDepthWindow > 1000f)
            spawnDepthWindow = DefaultMaxSpawnDepthMeters;

        waterLevel = resolvedWaterLevel;
        minSeaFloorHeight = waterLevel - spawnDepthWindow;
    }

    private static bool TryResolveOceanWaterLevel(out float waterLevel)
    {
        IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;
        IHectonOceanKinematics oceanKinematics = oceanKinematicsService != null && oceanKinematicsService.IsInitialized
            ? oceanKinematicsService.ActiveProvider
            : null;

        if (oceanKinematics != null &&
            oceanKinematics.IsAvailable &&
            TryResolveOceanWaterLevel(oceanKinematics.SeaLevel, out waterLevel))
        {
            return true;
        }

        waterLevel = DefaultWaterLevel;
        return false;
    }

    private static bool TryResolveOceanWaterLevel(float candidateWaterLevel, out float waterLevel)
    {
        if (math.isfinite(candidateWaterLevel) &&
            math.abs(candidateWaterLevel) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
        {
            waterLevel = candidateWaterLevel;
            return true;
        }

        waterLevel = DefaultWaterLevel;
        return false;
    }

    private static bool TryResolveTerrainBridgeWaterLevel(out float waterLevel)
    {
        MapMagicBridge terrainBridge = null;
        if (WorldRuntimeReferenceUtility.TryResolveMapMagicBridge(ref terrainBridge) &&
            TryResolveWaterLevel(terrainBridge.WaterSurfaceLevel, out waterLevel))
        {
            return true;
        }

        waterLevel = DefaultWaterLevel;
        return false;
    }

    private static bool TryResolveWaterLevel(float candidateWaterLevel, out float waterLevel)
    {
        if (math.isfinite(candidateWaterLevel) &&
            math.abs(candidateWaterLevel) > 0.0001f &&
            math.abs(candidateWaterLevel) <= 1000f)
        {
            waterLevel = candidateWaterLevel;
            return true;
        }

        waterLevel = DefaultWaterLevel;
        return false;
    }

    private static IPlayerRuntimeContext ResolveProductionPlayerContext()
    {
        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        return playerContext != null &&
               ProductionPlayerAuthorityUtility.IsProductionPlayerAuthorityObject(playerContext.PlayerObject)
            ? playerContext
            : null;
    }

    private static bool TryAcceptProductionPlayerTransform(
        Transform playerTransform,
        out Rigidbody body,
        out HectonPlayerMovement movement)
    {
        body = null;
        movement = null;
        if (playerTransform == null)
            return false;

        if (!ProductionPlayerAuthorityUtility.IsProductionPlayerAuthorityObject(playerTransform.gameObject))
            return false;

        return playerTransform.TryGetComponent(out body) &&
               body != null &&
               playerTransform.TryGetComponent(out movement) &&
               movement != null;
    }

    private static bool TryAcceptProductionPlayerRigidbody(
        Rigidbody body,
        out HectonPlayerMovement movement)
    {
        movement = null;
        if (body == null ||
            !ProductionPlayerAuthorityUtility.IsProductionPlayerAuthorityObject(body.gameObject))
        {
            return false;
        }

        return body.TryGetComponent(out movement) && movement != null;
    }

    private bool TryInstantiateProductionPlayerPrefab(
        out Rigidbody body,
        out HectonPlayerMovement movement)
    {
        body = null;
        movement = null;
        if (productionPlayerPrefab == null)
            return false;

        GameObject instance = Instantiate(productionPlayerPrefab, transform.position, transform.rotation);
        if (instance != null)
        {
            instance.name = productionPlayerPrefab.name;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, this.gameObject.scene);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSpawner($"[HectonPlayerSpawner-DEBUG] Instantiated player into scene: {instance.scene.name}");
#endif
        }

        if (instance != null &&
            TryAcceptProductionPlayerTransform(instance.transform, out body, out movement))
        {
            return true;
        }

        DestroyInvalidProductionPrefabInstance(instance);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LogSpawnerError(
            "[HectonPlayerSpawner] Production Player.prefab rejected after instantiate: " +
            "movement, interaction, and Rigidbody authority are required.",
            this);
#endif
        body = null;
        movement = null;
        return false;
    }

    private static void DestroyInvalidProductionPrefabInstance(GameObject instance)
    {
        if (instance == null)
            return;

        if (Application.isPlaying)
            UnityEngine.Object.Destroy(instance);
        else
            UnityEngine.Object.DestroyImmediate(instance);
    }

    private SpawnSearchResult EvaluatePoint(float x, float z)
    {
        _groundProbeOrigin.Set(x, groundProbeOriginHeight, z);

        if (!TryResolveGroundHit(out _hitInfo))
        {
            return SpawnSearchResult.NoTerrain;
        }

        return EvaluatePointFromHit(x, z);
    }

    /// <summary>
    /// Otsenivaet tochku na osnove uzhe zapolnennogo _hitInfo.
    /// Vyzyvaetsya posle uspeshnogo cached terrain probe.
    /// </summary>
    private SpawnSearchResult EvaluatePointFromHit(float x, float z)
    {
        float groundY = _hitInfo.point.y;

        // Susha — zemlya vyshe urovnya vody
        if (groundY >= waterLevel)
        {
            return SpawnSearchResult.AboveWater;
        }

        // Slishkom gluboko — dno nizhe poroga
        if (groundY < minSeaFloorHeight)
        {
            return SpawnSearchResult.DeepWater;
        }

        // Melkaya voda — idealnaya tochka, igrok poyavlyaetsya NA poverhnosti
        _spawnPosition.Set(x, waterLevel + spawnHeightOffset, z);
        return SpawnSearchResult.ValidShallowWater;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — NEARSHORE SEARCH
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Iz tochki sushi pytaetsya nayti blizhayshuyu tochku melkovodya,
    /// dvigayas ot sushi v 8 napravleniyah s shagom 10 metrov (do 200m).
    /// Pri uspehe — zapolnyaet _spawnPosition.
    /// </summary>
    private bool TryFindNearshorePoint(float landX, float landZ)
    {
        const float step = 10f;
        const int maxSteps = 20;

        for (int dir = 0; dir < 8; dir++)
        {
            ResolveNearshoreDirection(dir, out float dirX, out float dirZ);
            float stepX = dirX * step;
            float stepZ = dirZ * step;
            float testX = landX;
            float testZ = landZ;

            for (int s = 1; s <= maxSteps; s++)
            {
                testX += stepX;
                testZ += stepZ;

                SpawnSearchResult result = EvaluatePoint(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — RIGIDBODY TELEPORT
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Podgotavlivaet Rigidbody k bezopasnomu teleportu.
    /// </summary>
    private void PrepareRigidbodyForTeleport()
    {
        _teleportPreservedAngularVelocity = Vector3.zero;
        Vector3 currentVelocity = ResolveKccVelocityForTeleport();
        Vector3 currentPosition = ResolvePlayerRuntimePositionForTeleport();
        if (!TryResolveTeleportVelocityFrame(currentPosition, currentVelocity, out _teleportPreservedLocalVelocity, out _teleportPreservedPlatformVelocity))
        {
            _teleportPreservedLocalVelocity = HectonPlayerMotor.SafeVelocity(currentVelocity);
            _teleportPreservedPlatformVelocity = Vector3.zero;
        }

        if (TryResolveHydroPlayerMotor(out _))
            return;

        PrepareLegacyRigidbodyForTeleport(playerRigidbody);
    }

    private static Vector3 ResolveKccVelocityForTeleport()
    {
        if (!CoreDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
            signal.Sequence == 0u)
        {
            return Vector3.zero;
        }

        uint currentFrame = SystemDispatcher.CurrentFrameId;
        uint signalFrame = signal.Frame != 0u ? signal.Frame : signal.Sequence;
        if (currentFrame != 0u &&
            signalFrame != 0u &&
            (signalFrame > currentFrame || currentFrame - signalFrame > KccVelocitySpawnerMaxAgeFrames))
        {
            return Vector3.zero;
        }

        float3 velocity = signal.Velocity;
        return math.all(math.isfinite(velocity))
            ? HectonPlayerMotor.SafeVelocity(new Vector3(velocity.x, velocity.y, velocity.z))
            : Vector3.zero;
    }

    private static Vector3 ResolvePlayerRuntimePositionForTeleport()
    {
        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        if (playerContext != null &&
            playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
            math.all(math.isfinite(snapshot.RuntimePosition)))
        {
            float3 position = snapshot.RuntimePosition;
            return HectonPlayerMotor.SafeVelocity(new Vector3(position.x, position.y, position.z));
        }

        return Vector3.zero;
    }

    private static Quaternion ResolvePlayerRuntimeRotationForTeleport()
    {
        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        if (playerContext != null &&
            playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
            math.all(math.isfinite(snapshot.Forward)))
        {
            float3 forward3 = snapshot.Forward;
            float forwardLengthSq = math.lengthsq(forward3);
            if (math.isfinite(forwardLengthSq) && forwardLengthSq > 0.0001f)
            {
                float invLength = math.rsqrt(math.max(forwardLengthSq, 0.0001f));
                Vector3 forward = new Vector3(
                    forward3.x * invLength,
                    forward3.y * invLength,
                    forward3.z * invLength);
                return Quaternion.LookRotation(forward, Vector3.up);
            }
        }

        return Quaternion.identity;
    }

    /// <summary>
    /// Teleportiruet igroka v ukazannuyu pozitsiyu.
    /// Bezopasnyy poryadok operatsiy dlya Rigidbody.
    /// </summary>
    /// <summary>
    /// R99 KINEMATIC ARREST GATE (partial).
    ///
    /// `AGENTS.md` requires the player to stay suspended until the spawn chunk's terrain physics is proven
    /// baked, and bans time-based loading waits. Before R99 the signal named by that law
    /// (<see cref="WorldChunkPhysicsBakedSignal"/>) did not exist at all, so this spawner released the player
    /// purely on a raycast hit plus realtime timers — a raycast can hit a collider whose heightmap has since
    /// been rewritten, and it cannot distinguish "collider live" from "collider disabled".
    ///
    /// This gate refuses a spawn point until the chunk covering it has published an ACTIVE collider.
    /// A point whose chunk reported terminal bake failure is not accepted either: it is simply not a valid
    /// spawn point, and the existing spiral search moves on. The pre-existing global timeout remains the
    /// last-resort degradation path.
    ///
    /// STILL MISSING (not implemented here): player suspension itself — `IsSuspended`, gravity/velocity zero,
    /// input lock and screen blackout live in the movement/UI route, which this change does not touch.
    /// </summary>
    private static bool IsSpawnPointPhysicsReady(float worldX, float worldZ)
    {
        // No terrain-bake route published in this scene (isolated sandbox / render test scenes):
        // there is nothing to wait for, so do not deadlock the spawner on a signal that never comes.
        if (!WorldChunkPhysicsBakedEvents.IsLaneActive)
            return true;

        return WorldChunkPhysicsBakedEvents.IsWorldPointPhysicsBaked(worldX, worldZ);
    }

    private void TeleportPlayer(Vector3 position)
    {
        Vector3 platformVelocityAtTarget = _teleportPreservedPlatformVelocity;
        if (_playerMovement != null &&
            _playerMovement.TryGetActiveTransportPlatform(out ITransportPlatform transportPlatform) &&
            transportPlatform != null &&
            transportPlatform.IsTransportPlatformActive)
        {
            platformVelocityAtTarget = transportPlatform.GetPlatformPointVelocity(position);
        }

        Vector3 targetLinearVelocity = HectonPlayerMotor.SafeVelocity(platformVelocityAtTarget + _teleportPreservedLocalVelocity);
        Vector3 targetAngularVelocity = HectonPlayerMotor.SafeVelocity(_teleportPreservedAngularVelocity);

        playerRigidbody.TryGetComponent(out HectonPlayerMotor playerMotor);
        if (playerMotor != null && playerMotor.HydrodynamicKccOwnsCollisionAuthority)
        {
            playerMotor.MovePosition(position);
            playerMotor.SetLinearVelocity(targetLinearVelocity);
            Transform playerTransform = ResolvePlayerTransformForTeleport();
            if (playerTransform != null)
                playerTransform.SetPositionAndRotation(position, ResolvePlayerRuntimeRotationForTeleport());
            return;
        }

        if (playerMotor != null)
            playerMotor.MovePosition(position);
        else
            GlobalRegistry.Physics?.QueuePoseSet(playerRigidbody, position, ResolvePlayerRuntimeRotationForTeleport());

        if (playerMotor != null)
            playerMotor.SetLinearVelocity(targetLinearVelocity);
        if (playerMotor != null)
            playerMotor.SetAngularVelocity(targetAngularVelocity);
        RestoreLegacyRigidbodyAfterTeleport(playerRigidbody);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float elapsed = Time.realtimeSinceStartup - _operationStartTime;
        LogSpawner(
            $"[HectonPlayerSpawner] Player spawned.\n" +
            $"   Position: ({position.x:F1}, {position.y:F1}, {position.z:F1})\n" +
            $"   Water level: {waterLevel:F1}\n" +
            $"   Ground height under player: {_hitInfo.point.y:F1}\n" +
            $"   Water depth: {waterLevel - _hitInfo.point.y:F1}m\n" +
            $"   Search time: {elapsed:F1}s");
#endif
    }

    private bool TryResolveHydroPlayerMotor(out HectonPlayerMotor playerMotor)
    {
        playerMotor = null;
        return playerRigidbody != null &&
               playerRigidbody.TryGetComponent(out playerMotor) &&
               playerMotor.HydrodynamicKccOwnsCollisionAuthority;
    }

    private Transform ResolvePlayerTransformForTeleport()
    {
        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        if (playerContext != null && playerContext.PlayerTransform != null)
            return playerContext.PlayerTransform;

        if (_playerMovement != null)
            return _playerMovement.transform;

        return playerRigidbody != null ? playerRigidbody.transform : null;
    }

    private static void PrepareLegacyRigidbodyForTeleport(Rigidbody body)
    {
        if (body == null)
            return;

        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
    }

    private static void RestoreLegacyRigidbodyAfterTeleport(Rigidbody body)
    {
        if (body == null)
            return;

        body.isKinematic = false;
        body.WakeUp();
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — FALLBACK
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Avariynyy spavn na urovne vody v tsentre karty.
    /// Garantirovanno zavershaet operatsiyu bez dedloka.
    /// </summary>
    private bool TryResolveTeleportVelocityFrame(
        Vector3 currentPosition,
        Vector3 currentVelocity,
        out Vector3 localVelocity,
        out Vector3 platformVelocity)
    {
        localVelocity = HectonPlayerMotor.SafeVelocity(currentVelocity);
        platformVelocity = Vector3.zero;
        if (_playerMovement == null ||
            !_playerMovement.TryGetActiveTransportPlatform(out ITransportPlatform transportPlatform) ||
            transportPlatform == null ||
            !transportPlatform.IsTransportPlatformActive)
        {
            return false;
        }

        Vector3 activePlatformVelocity = transportPlatform.GetPlatformPointVelocity(currentPosition);
        platformVelocity = HectonPlayerMotor.SafeVelocity(activePlatformVelocity);
        localVelocity = HectonPlayerMotor.SafeVelocity(currentVelocity - platformVelocity, currentVelocity);
        return IsFiniteVector(localVelocity) && IsFiniteVector(platformVelocity);
    }

    private static bool IsFiniteVector(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }

    private void ForceFallbackSpawn()
    {
        _spawnPosition.Set(
            searchOrigin.x,
            waterLevel + spawnHeightOffset,
            searchOrigin.y);

        TeleportPlayer(_spawnPosition);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        float elapsed = Time.realtimeSinceStartup - _operationStartTime;
        LogSpawnerWarning(
            $"[HectonPlayerSpawner] Fallback spawn at " +
            $"({_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1})\n" +
            $"   Time before fallback: {elapsed:F1}s");
#endif
    }

    // ══════════════════════════════════════════════════════════════
    //  EDITOR — GIZMOS
    // ══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    /// <summary>
    /// Vizualizatsiya v redaktore: tochka poiska, uroven morya,
    /// minimalnaya glubina i liniya cached terrain probe.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Tochka nachala poiska
        Gizmos.color = Color.cyan;
        Vector3 origin = new Vector3(searchOrigin.x, waterLevel, searchOrigin.y);
        Gizmos.DrawWireSphere(origin, 5f);

        // Ploskost urovnya morya
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawCube(origin, new Vector3(500f, 0.1f, 500f));

        // Uroven minimalnoy glubiny
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Vector3 minDepthOrigin = new Vector3(
            searchOrigin.x, minSeaFloorHeight, searchOrigin.y);
        Gizmos.DrawCube(minDepthOrigin, new Vector3(500f, 0.1f, 500f));

        // Cached terrain probe line
        Gizmos.color = Color.yellow;
        Vector3 rayStart = new Vector3(
            searchOrigin.x, groundProbeOriginHeight, searchOrigin.y);
        Gizmos.DrawLine(rayStart, origin);
    }

    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isCompiling ||
            UnityEditor.EditorApplication.isUpdating ||
            UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (maxRetriesPerPoint < 1) maxRetriesPerPoint = 1;
        if (globalTimeoutSec < 10f) globalTimeoutSec = 10f;
        if (retryDelay < 0.1f) retryDelay = 0.1f;
        if (maxWaitTime < 5f) maxWaitTime = 5f;
    }
#endif
}
