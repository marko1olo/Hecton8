// ============================================================================
// HECTON-8 — HectonPlayerSpawner.cs (v3.1)
//
// Bezopasnyy asinhronnyy spavner igroka dlya protsedurno generiruemogo mira
// (MapMagic 2). Unity 6 Awaitable API — Zero-GC v asinhronnyh tsiklah.
//
// KRITIChESKIY FIKS v3.1:
//   • [BUG] V v3.0 Faza 2: esli Raycast ne nahodit terreyn v tochke spirali,
//     tsikl delaet `continue` bez inkrementa spiralIndex. Tochka proveryaetsya
//     beskonechno, esli chank MapMagic nikogda ne sgeneriruetsya (za predelami
//     karty, oshibka generatsii, OOM). Rezultat: vechnyy ekran zagruzki.
//
//   • [FIX] Dobavlen per-point retry counter (maxRetriesPerPoint).
//     Kazhdaya tochka spirali poluchaet ogranichennoe kolichestvo popytok.
//     Pri ischerpanii — tochka propuskaetsya (spiralIndex++).
//     Dedlok matematicheski nevozmozhen.
//
//   • [FIX] Dobavlen globalnyy taymaut operatsii (globalTimeoutSec).
//     Time.realtimeSinceStartup proveryaetsya na kazhdoy iteratsii.
//     Pri prevyshenii — nemedlennyy fallback spavn.
//     Zaschita ot VSEH vozmozhnyh zavisaniy, vklyuchaya edge cases.
//
//   • [FIX] Faza 3 poluchila tot zhe globalnyy taymaut.
//     Hotya v v3.0 Faza 3 ne imela `continue`-baga, globalnyy
//     taymaut zaschischaet ot medlennyh nearshore-poiskov na ogromnyh kartah.
//
// SOHRANENO BEZ IZMENENIY:
//   • Arhimedova spiral, fallback, nearshore search
//   • Zero-GC Raycast: predallotsirovannye _rayOrigin, _hitInfo
//   • Rigidbody teleport: isKinematic, interpolation, velocity reset
//   • Unity 6 Awaitable API (zero-GC awaiter)
//
// ALGORITM:
//   Faza 1 — Ozhidanie generatsii terreyna v tsentre karty (taymaut: maxWaitTime).
//   Faza 2 — Poisk melkovodya po Arhimedovoy spirali (per-point retries + global timeout).
//   Faza 3 — Fallback: poisk sushi → nearshore melkovode (global timeout).
//   Faza 4 — Avariynyy spavn na urovne vody v tsentre (garantirovanno dostigaetsya).
//
// TsELEVOE ZhELEZO: NVIDIA GeForce MX350.
// ============================================================================

using System;
using System.Threading;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

/// <summary>
/// Bezopasnyy asinhronnyy spavner igroka dlya protsedurno generiruemogo mira.
/// <para>
/// Ischet tochku na poverhnosti vody vblizi sushi, gde glubina ne slishkom bolshaya.
/// Igrok poyavlyaetsya NA vode (Y = WaterLevel) ryadom s beregom.
/// </para>
/// <para>
/// <b>Ispolzovanie iz GameBootstrapper:</b>
/// <code>
/// var spawner = sceneBootstrap.PlayerSpawner;
/// await spawner.SpawnPlayerAsync(ct);
/// </code>
/// </para>
/// </summary>
public class HectonPlayerSpawner : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — PLAYER
    // ══════════════════════════════════════════════════════════════

    [Header("=== Player ===")]
    [Tooltip("Ssylka na Rigidbody igroka (zadaetsya cherez Inspector)")]
    [SerializeField] private Rigidbody playerRigidbody;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — WATER SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Water Settings ===")]
    [Tooltip("Uroven morya (Water Level) — vysota Y poverhnosti vody")]
    [SerializeField] private float waterLevel = 4900f;

    [Tooltip("Minimalnaya dopustimaya vysota dna pod vodoy (zaschita ot slishkom glubokih mest)")]
    [SerializeField] private float minSeaFloorHeight = 4800f;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — RAYCAST SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Raycast Settings ===")]
    [Tooltip("Vysota, s kotoroy puskaetsya luch vniz dlya poiska zemli")]
    [SerializeField] private float raycastOriginHeight = 10000f;

    [Tooltip("Sloy(i) terreyna dlya Raycast")]
    [SerializeField] private LayerMask terrainLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — SPAWN SEARCH SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Spawn Search Settings ===")]
    [Tooltip("Nachalnaya tochka poiska po X,Z")]
    [SerializeField] private Vector2 searchOrigin = Vector2.zero;

    [Tooltip("Shag spirali v metrah (rasstoyanie mezhdu vitkami)")]
    [SerializeField] private float spiralStep = 75f;

    [Tooltip("Maksimalnoe kolichestvo tochek spirali dlya proverki")]
    [SerializeField] private int maxSpiralPoints = 500;

    [Tooltip("Smeschenie igroka nad poverhnostyu vody / zemley")]
    [SerializeField] private float spawnHeightOffset = 2f;

    [Tooltip("Zaderzhka mezhdu popytkami Raycast (ozhidanie generatsii MapMagic), sekundy")]
    [SerializeField] private float retryDelay = 0.5f;

    [Tooltip("Maksimalnoe vremya ozhidaniya generatsii terreyna v Faze 1 (sekundy)")]
    [SerializeField] private float maxWaitTime = 60f;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — DEADLOCK PROTECTION (v3.1)
    // ══════════════════════════════════════════════════════════════

    [Header("=== Deadlock Protection (v3.1) ===")]
    [Tooltip("Maksimalnoe kolichestvo povtornyh popytok Raycast dlya odnoy tochki spirali.\n" +
             "Esli terreyn ne poyavilsya za maxRetriesPerPoint × retryDelay sekund — " +
             "tochka propuskaetsya.\n" +
             "10 popytok × 0.5s = 5 sekund maksimum na tochku.")]
    [SerializeField] private int maxRetriesPerPoint = 10;

    [Tooltip("Globalnyy taymaut vsey operatsii SpawnPlayerAsync (sekundy).\n" +
             "Vklyuchaet vse fazy. Pri prevyshenii — avariynyy spavn.\n" +
             "Zaschita ot VSEH vozmozhnyh zavisaniy.")]
    [SerializeField] private float globalTimeoutSec = 120f;

    // ══════════════════════════════════════════════════════════════
    //  CACHED FIELDS — Zero-GC
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Predallotsirovannaya tochka nachala lucha Raycast.
    /// Pereispolzuetsya vo vseh vyzovah Physics.Raycast
    /// bez sozdaniya novyh Vector3.
    /// </summary>
    private Vector3 _rayOrigin;

    /// <summary>
    /// Predallotsirovannaya pozitsiya spavna.
    /// Zapolnyaetsya pri nahozhdenii validnoy tochki.
    /// </summary>
    private Vector3 _spawnPosition;

    /// <summary>
    /// Predallotsirovannaya struktura rezultata Raycast.
    /// Unity perezapisyvaet polya pri kazhdom vyzove Physics.Raycast.
    /// </summary>
    private RaycastHit _hitInfo;
    private readonly RaycastHit[] _groundHits = new RaycastHit[1]; // COLD ALLOC: spawner needs only the nearest terrain hit per probe.

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

    // ══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Validatsiya ssylok s avtomaticheskim poiskom igroka v stsene.
    /// </summary>
    private void Awake()
    {
        IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
        if (playerRigidbody == null && playerContext != null)
            playerRigidbody = playerContext.PlayerRigidbody;
        if (_playerMovement == null && playerContext != null)
            _playerMovement = playerContext.PlayerMovement;
        // ── Popytka avtomaticheskogo poiska, esli Inspector-ssylka ne zadana ──
        if (playerRigidbody == null)
        {
            Debug.Log(
                "[HectonPlayerSpawner] Rigidbody ne naznachen v Inspector. " +
                "Pytayus poluchit current player cherez GameBootstrapper...");

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
                    Debug.Log(
                        $"[HectonPlayerSpawner] Rigidbody nayden avtomaticheski " +
                        $"cherez GameBootstrapper na obekte \"{playerRigidbody.gameObject.name}\".");
                }
                else
                {
                    Debug.LogWarning(
                        $"[HectonPlayerSpawner] Current player \"{playerTransformRoot.name}\" " +
                        "nayden cherez GameBootstrapper, no Rigidbody ne obnaruzhen ni na root, ni v dochernih obektah.");
                }
            }
            else
            {
                Debug.LogWarning(
                    "[HectonPlayerSpawner] GameBootstrapper ne predostavil current player.");
            }
        }
        else if (_playerMovement == null)
        {
            playerRigidbody.TryGetComponent(out _playerMovement);
        }

        // ── Finalnaya proverka posle vseh popytok poiska ──
        if (playerRigidbody == null)
        {
            Debug.LogError(
                "[HectonPlayerSpawner] Rigidbody igroka ne nayden! " +
                "Ni Inspector-ssylka, ni poisk po tegu \"Player\" ne dali rezultata. " +
                "Spavn nevozmozhen.",
                this);
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
        Debug.Log("[HectonPlayerSpawner] Nachinayu poisk bezopasnoy tochki spavna...");

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
                Debug.LogError(
                    $"[HectonPlayerSpawner] Globalnyy taymaut ({globalTimeoutSec}s) " +
                    "v Faze 1. Avariynyy spavn.");
                ForceFallbackSpawn();
                return;
            }

            if (TryRaycastGround(out _hitInfo))
            {
                terrainReady = true;
                Debug.Log(
                    $"[HectonPlayerSpawner] Terreyn obnaruzhen v tsentre karty. " +
                    $"Vysota: {_hitInfo.point.y:F1}");
            }
            else
            {
                waitTimer += retryDelay;

                if (waitTimer >= maxWaitTime)
                {
                    Debug.LogError(
                        $"[HectonPlayerSpawner] Taymaut ({maxWaitTime}s): " +
                        "terreyn ne sgenerirovan. Spavnyu na urovne vody v tsentre.");
                    ForceFallbackSpawn();
                    return;
                }

                Debug.Log(
                    $"[HectonPlayerSpawner] Terreyn esche ne gotov, zhdu... " +
                    $"({waitTimer:F1}s)");
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
        int spiralIndex = 0;
        float angleStep = 45f;
        float currentAngle = 0f;
        float currentRadius = spiralStep;
        int pointsPerRing = 8;
        int pointInRing = 0;
        int retryCount = 0;  // v3.1: per-point retry counter

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // ── Globalnyy taymaut (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
                Debug.LogError(
                    $"[HectonPlayerSpawner] Globalnyy taymaut ({globalTimeoutSec}s) " +
                    $"v Faze 2 na tochke {spiralIndex}/{maxSpiralPoints}. Avariynyy spavn.");
                ForceFallbackSpawn();
                return;
            }

            // Vychislyaem X, Z po spirali
            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (TryRaycastGround(out _hitInfo))
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

                Debug.Log(
                    $"[HectonPlayerSpawner] Tochka spirali #{spiralIndex} " +
                    $"({testX:F0}, {testZ:F0}): terreyn ne sgenerirovan " +
                    $"posle {maxRetriesPerPoint} popytok. Propuskayu.");
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
                currentRadius += spiralStep;

                pointsPerRing = Mathf.Max(
                    8,
                    Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                angleStep = 360f / pointsPerRing;
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

        Debug.LogWarning(
            "[HectonPlayerSpawner] Melkaya voda vblizi sushi ne naydena " +
            "posle polnogo obhoda spirali. Ischu lyubuyu sushu...");

        // Sbros parametrov spirali dlya vtorogo prohoda
        currentAngle = 0f;
        currentRadius = spiralStep;
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
                Debug.LogError(
                    $"[HectonPlayerSpawner] Globalnyy taymaut ({globalTimeoutSec}s) " +
                    $"v Faze 3 na tochke {spiralIndex}/{maxSpiralPoints}. Avariynyy spavn.");
                ForceFallbackSpawn();
                return;
            }

            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (TryRaycastGround(out _hitInfo))
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
                currentRadius += spiralStep;
                pointsPerRing = Mathf.Max(
                    8,
                    Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                angleStep = 360f / pointsPerRing;
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

        Debug.LogWarning(
            "[HectonPlayerSpawner] Ni susha, ni melkovode ne naydeny. " +
            "Spavnyu na urovne vody v tsentre.");
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

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — EVALUATION
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Otsenivaet tochku karty, puskaya Raycast vniz.
    /// Pri nahozhdenii validnogo melkovodya zapolnyaet _spawnPosition.
    /// Zero-GC: predallotsirovannye _rayOrigin i _hitInfo.
    /// </summary>
    private bool TryRaycastGround(out RaycastHit hit)
    {
        int hitCount = Physics.RaycastNonAlloc(
            _rayOrigin,
            Vector3.down,
            _groundHits,
            raycastOriginHeight * 2f,
            terrainLayerMask);

        if (hitCount > 0)
        {
            hit = _groundHits[0];
            return true;
        }

        hit = default;
        return false;
    }

    private SpawnSearchResult EvaluatePoint(float x, float z)
    {
        _rayOrigin.Set(x, raycastOriginHeight, z);

        if (!TryRaycastGround(out _hitInfo))
        {
            return SpawnSearchResult.NoTerrain;
        }

        return EvaluatePointFromHit(x, z);
    }

    /// <summary>
    /// Otsenivaet tochku na osnove uzhe zapolnennogo _hitInfo.
    /// Vyzyvaetsya posle uspeshnogo Raycast.
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
            float angle = dir * 45f * Mathf.Deg2Rad;
            float dirX = Mathf.Cos(angle);
            float dirZ = Mathf.Sin(angle);

            for (int s = 1; s <= maxSteps; s++)
            {
                float testX = landX + dirX * step * s;
                float testZ = landZ + dirZ * step * s;

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
        _teleportPreservedAngularVelocity = HectonPlayerMotor.SafeVelocity(playerRigidbody.angularVelocity);
        if (!TryResolveTeleportVelocityFrame(playerRigidbody.position, playerRigidbody.linearVelocity, out _teleportPreservedLocalVelocity, out _teleportPreservedPlatformVelocity))
        {
            _teleportPreservedLocalVelocity = HectonPlayerMotor.SafeVelocity(playerRigidbody.linearVelocity);
            _teleportPreservedPlatformVelocity = Vector3.zero;
        }

        playerRigidbody.isKinematic = true;
        playerRigidbody.interpolation = RigidbodyInterpolation.None;
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

        Vector3 targetLinearVelocity = HectonPlayerMotor.SafeVelocity(
            platformVelocityAtTarget + _teleportPreservedLocalVelocity,
            playerRigidbody.linearVelocity);
        Vector3 targetAngularVelocity = HectonPlayerMotor.SafeVelocity(
            _teleportPreservedAngularVelocity,
            playerRigidbody.angularVelocity);

        playerRigidbody.MovePosition(position);
        playerRigidbody.isKinematic = false;
        playerRigidbody.linearVelocity = targetLinearVelocity;
        playerRigidbody.angularVelocity = targetAngularVelocity;
        playerRigidbody.WakeUp();
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        float elapsed = Time.realtimeSinceStartup - _operationStartTime;

        Debug.Log(
            $"[HectonPlayerSpawner] ✅ Igrok uspeshno zaspavnen!\n" +
            $"   Koordinaty: ({position.x:F1}, {position.y:F1}, {position.z:F1})\n" +
            $"   Uroven morya: {waterLevel:F1}\n" +
            $"   Vysota dna pod igrokom: {_hitInfo.point.y:F1}\n" +
            $"   Glubina vody: {waterLevel - _hitInfo.point.y:F1}m\n" +
            $"   Vremya poiska: {elapsed:F1}s");
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

        float elapsed = Time.realtimeSinceStartup - _operationStartTime;

        Debug.LogWarning(
            $"[HectonPlayerSpawner] ⚠️ Avariynyy spavn na " +
            $"({_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1})\n" +
            $"   Vremya do fallback: {elapsed:F1}s");
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
