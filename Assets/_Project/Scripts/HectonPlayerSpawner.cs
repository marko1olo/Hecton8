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
//   - Zero-GC ground-probe fields: _rayOrigin and _hitInfo.
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
        Debug.LogWarning(message);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawnerError(string message)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError(message);
#endif
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    private static void LogSpawnerError(string message, UnityEngine.Object context)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogError(message, context);
#endif
    }

    // ══════════════════════════════════════════════════════════════
    // Inspector: player
    // ══════════════════════════════════════════════════════════════

    [Header("=== Player ===")]
    [Tooltip("Player Rigidbody reference assigned through the Inspector.")]
    [SerializeField] private Rigidbody playerRigidbody;

    // ══════════════════════════════════════════════════════════════
    // Inspector: water settings
    // ══════════════════════════════════════════════════════════════

    [Header("=== Water Settings ===")]
    [Tooltip("Sea level in world-space Y.")]
    [SerializeField] private float waterLevel = 4900f;

    [Tooltip("Minimum valid sea floor height under water.")]
    [SerializeField] private float minSeaFloorHeight = 4800f;

    // ══════════════════════════════════════════════════════════════
    // Inspector: raycast settings
    // ══════════════════════════════════════════════════════════════

    [Header("=== Raycast Settings ===")]
    [Tooltip("World-space height used for downward terrain raycasts.")]
    [SerializeField] private float raycastOriginHeight = 10000f;

    [Tooltip("Terrain layers used by spawn raycasts.")]
    [SerializeField] private LayerMask terrainLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

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

    [Tooltip("Delay between raycast retries while waiting for MapMagic terrain, in seconds.")]
    [SerializeField] private float retryDelay = 0.5f;

    [Tooltip("Maximum terrain generation wait in phase 1, in seconds.")]
    [SerializeField] private float maxWaitTime = 60f;

    // ══════════════════════════════════════════════════════════════
    // Inspector: deadlock protection
    // ══════════════════════════════════════════════════════════════

    [Header("=== Deadlock Protection (v3.1) ===")]
    [Tooltip("Maximum raycast retries for one spiral point.\n" +
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
    /// Predallotsirovannaya tochka nachala legacy-probe lucha.
    /// Pereispolzuetsya vo vseh poverhnostnyh probah bez sozdaniya novyh Vector3.
    /// </summary>
    private Vector3 _rayOrigin;

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
        if (!s_spawnTrigLutInitialized)
        {
            InitializeSpawnTrigLut();
        }

        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        if (playerRigidbody == null && playerContext != null)
            playerRigidbody = playerContext.PlayerRigidbody;
        if (_playerMovement == null && playerContext != null)
            _playerMovement = playerContext.PlayerMovement;
        // ── Popytka avtomaticheskogo poiska, esli Inspector-ssylka ne zadana ──
        if (playerRigidbody == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSpawner(
                "[HectonPlayerSpawner] Rigidbody is not assigned in the Inspector. " +
                "Trying to resolve the current player through GameBootstrapper.");
#endif

            if (GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransformRoot) &&
                playerTransformRoot != null)
            {
                playerTransformRoot.TryGetComponent(out playerRigidbody);
                if (_playerMovement == null)
                {
                    playerTransformRoot.TryGetComponent(out _playerMovement);
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
            playerRigidbody.TryGetComponent(out _playerMovement);
        }

        // ── Finalnaya proverka posle vseh popytok poiska ──
        if (playerRigidbody == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            LogSpawnerError(
                "[HectonPlayerSpawner] Player Rigidbody not found. " +
                "Inspector reference and bootstrap lookup both failed. " +
                "Spawn is impossible.",
                this);
#endif
            enabled = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Asinhronnyy poisk bezopasnoy tochki spavna i teleportatsiya igroka.
    ///
    /// v3.1: Zaschita ot dedloka:
    ///   • Per-point retry limit (maxRetriesPerPoint) — kazhdaya tochka spirali
    ///     mozhet byt proverena ne bolee N raz. Pri ischerpanii — propuskaetsya.
    ///   • Globalnyy taymaut (globalTimeoutSec) — esli vsya operatsiya
    ///     prevyshaet limit — nemedlennyy fallback spavn.
    ///   • Beskonechnyy tsikl na odnoy tochke MATEMATIChESKI NEVOZMOZhEN:
    ///     retryCount inkrementiruetsya pri kazhdoy neudache, spiralIndex++
    ///     garantirovanno vyzyvaetsya pri retryCount >= maxRetriesPerPoint.
    /// </summary>
    public async Awaitable SpawnPlayerAsync(CancellationToken ct)
    {
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

        _rayOrigin.Set(searchOrigin.x, raycastOriginHeight, searchOrigin.y);

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

            if (TryResolveGroundHit(out _hitInfo))
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
        //
        //  v3.1 FIX: Per-point retry counter.
        //
        //  BYLO (v3.0, DEDLOK):
        //    if (!Raycast) { await delay; continue; }
        //    // continue propuskaet spiralIndex++ → vechnyy tsikl.
        //
        //  STALO (v3.1, BEZOPASNO):
        //    if (!Raycast) {
        //        retryCount++;
        //        if (retryCount >= maxRetriesPerPoint) {
        //            retryCount = 0;
        //            → AdvanceSpiral() (spiralIndex++ guaranteed)
        //        } else {
        //            await delay;
        //        }
        //        continue;
        //    }
        //    retryCount = 0;  // reset on successful raycast
        //    → evaluate point
        //    → AdvanceSpiral()
        //
        //  Maksimalnoe vremya na odnu tochku:
        //    maxRetriesPerPoint × retryDelay = 10 × 0.5 = 5 sekund.
        //  Posle etogo — garantirovannyy perehod k sleduyuschey tochke.
        // ══════════════════════════════════════════════════════════

        // Snachala proveryaem tsentralnuyu tochku
        SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);

        if (centerResult == SpawnSearchResult.ValidShallowWater)
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

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (TryResolveGroundHit(out _hitInfo))
            {
                // ── Raycast uspeshen — sbrasyvaem schetchik popytok ──
                retryCount = 0;

                SpawnSearchResult result = EvaluatePointFromHit(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
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

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (TryResolveGroundHit(out _hitInfo))
            {
                float groundY = _hitInfo.point.y;

                // Nashli sushu — ischem blizhayshuyu tochku melkovodya
                if (groundY > waterLevel)
                {
                    bool foundNearshore = TryFindNearshorePoint(testX, testZ);
                    if (foundNearshore)
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
        if (!TryResolveCachedTerrainHeight(_rayOrigin.x, _rayOrigin.z, out float groundY))
            return false;

        hit.point = new Vector3(_rayOrigin.x, groundY, _rayOrigin.z);
        hit.normal = Vector3.up;
        hit.distance = Mathf.Max(0f, _rayOrigin.y - groundY);
        return true;
    }

    private static bool TryResolveCachedTerrainHeight(float x, float z, out float groundY)
    {
        groundY = 0f;
        HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
        return vegetationBridge != null &&
               vegetationBridge.TryGetCachedTerrainHeight(x, z, out groundY) &&
               float.IsFinite(groundY);
    }

    private SpawnSearchResult EvaluatePoint(float x, float z)
    {
        _rayOrigin.Set(x, raycastOriginHeight, z);

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
        if (!Hecton8.Physics.PhysicsDeterminismSignals.TryGetLatestKccVelocity(out KccVelocitySignal signal) ||
            signal.Sequence == 0u)
        {
            return Vector3.zero;
        }

        uint currentFrame = unchecked((uint)SystemDispatcher.CurrentFrameIndex);
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
            Hecton8.Physics.PhysicsForceRouter.QueuePoseSet(playerRigidbody, position, ResolvePlayerRuntimeRotationForTeleport());

        if (playerMotor != null)
            playerMotor.SetLinearVelocity(targetLinearVelocity);
        if (playerMotor == null || !playerMotor.HydrodynamicKccOwnsCollisionAuthority)
            Hecton8.Physics.PhysicsForceRouter.QueueAngularVelocitySet(playerRigidbody, targetAngularVelocity);
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
    /// minimalnaya glubina i luch Raycast.
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

        // Luch Raycast
        Gizmos.color = Color.yellow;
        Vector3 rayStart = new Vector3(
            searchOrigin.x, raycastOriginHeight, searchOrigin.y);
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
