// ============================================================================
// HECTON-8 — ProximityColliderSystem.cs
// Podstavlyaet fizicheskie kollaydery iz pula k blizhayshim tochkam (kamni/musor),
// kotorye renderyatsya cherez GPU Instancer bez sobstvennoy fiziki.
//
// ARHITEKTURA:
//   • ITickable — tikaetsya cherez GameTickManager (edinyy Update).
//   • Unity.Jobs + Burst — vychislenie distantsiy na worker threads.
//   • ObjectPoolManager — pul pustyh GameObject s BoxCollider.
//   • Gisterezis 40/45m — predotvraschaet mertsanie na granitse radiusa.
//
// ZERO GC V TICK:
//   • NativeArray (persistent) — nikakih new v goryachih putyah.
//   • Keshirovannyy massiv GameObject[] dlya aktivnyh kollayderov.
//   • Keshirovannyy massiv byte[] dlya predyduschego sostoyaniya.
//   • Nikakih LINQ, foreach, List, lyambd, zamykaniy.
//
// POTOKOBEZOPASNOST:
//   Job planiruetsya v Tick, zavershenie proveryaetsya v sleduyuschem Tick.
//   Vse mutatsii (Spawn/Despawn) — strogo Main Thread.
//
// PAMYaT:
//   Pri 10,000 tochek:
//     NativeArray<float3>  = 10,000 × 12 bytes = ~120 KB
//     NativeArray<byte>    = 10,000 ×  1 byte  = ~10 KB
//     GameObject[]         = 10,000 ×  8 bytes  = ~80 KB (references)
//     byte[] prevStatus    = 10,000 ×  1 byte  = ~10 KB
//     ITOGO: ~220 KB — nichtozhno dlya lyubogo zheleza.
// ============================================================================

using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    public sealed class ProximityColliderSystem : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        internal static ProximityColliderSystem ActiveRuntimeInstance { get; private set; }
        internal static event System.Action<ProximityColliderSystem> ActiveRuntimeInstanceChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveRuntimeForSubsystemRegistration()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged = null;
        }
#if UNITY_EDITOR
        private static bool _assemblyReloadHookRegistered;
#endif
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform igroka. Esli ne naznachen — ischetsya po tegu Player.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Prefab pustogo GameObject s BoxCollider dlya pula. " +
                 "Dolzhen byt progret v ObjectPoolManager.warmupPresets.")]
        [SerializeField] private GameObject colliderPrefab;

        [Header("── Proximity Settings ────────────────────────")]
        [Tooltip("Radius aktivatsii kollayderov (metry).")]
        [SerializeField] private float activateRadius = 40f;

        [Tooltip("Radius deaktivatsii kollayderov (metry). " +
                 "Dolzhen byt > activateRadius dlya gisterezisa.")]
        [SerializeField] private float deactivateRadius = 45f;

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Maksimalnoe kolichestvo Spawn/Despawn operatsiy za odin Tick. " +
                 "Predotvraschaet lag-spayki pri teleportatsii igroka.")]
        [SerializeField] private int maxOperationsPerTick = 64;

        [Header("── Diagnostics (Read Only) ───────────────────")]
        [SerializeField] private int _debugTotalPoints;
        [SerializeField] private int _debugActiveColliders;
        [SerializeField] private int _debugJobFrameDelay;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        private const SystemID VaultOwnerSystemId = SystemID.Physics;
        private const BufferID PositionsBufferId = BufferID.ProximityColliderPositions;
        private const BufferID JobResultsBufferId = BufferID.ProximityColliderJobResults;
        private const BufferID PrevStatusBufferId = BufferID.ProximityColliderPrevStatus;
        private static readonly ulong _jobMutationGuardMask =
            ProximityMutationGuardBit(PositionsBufferId) |
            ProximityMutationGuardBit(JobResultsBufferId) |
            ProximityMutationGuardBit(PrevStatusBufferId);

        // ── Job I/O (DataVault descriptors only; native views stay phase-local) ──
        private VaultGenerationHandle<float3> _positionsHandle;
        private VaultGenerationHandle<byte> _jobResultsHandle;
        private VaultGenerationHandle<byte> _prevStatusHandle;
        private IDataVault _dataVault;
        private IDataVault _positionWriteVault;
        private IDataVault _jobBufferGuardVault;
        private IObjectPoolService _objectPool;

        // ── Main-thread cached arrays (zero GC) ──
        private GameObject[] _activeColliders;       // null = net kollaydera
        private byte[]       _prevStatus;            // predyduschee sostoyanie (0/1)

        // ── Job management ──
        private JobHandle _jobHandle;
        private bool      _jobScheduled;
        private bool      _initialized;
        private bool      _registeredToDispatcher;
        private bool      _registeredLateFrame;
        private bool      _hotSwapRegistered;
        private bool      _jobBuffersLocked;
        private int       _jobPendingFrameCount;
        private IPlayerRuntimeContext _playerRuntimeContext;

        // ── Cached squared radii (avoid sqrt in Job) ──
        private float _activateRadiusSq;
        private float _deactivateRadiusSq;
        private float _nextPlayerResolveWarningTime;

        // ── Point count ──
        private int _pointCount;

        // ══════════════════════════════════════════════════════════
        //  BURST JOB — vychislenie distantsiy na worker threads
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Burst-compiled Job. Vychislyaet kvadrat distantsii ot igroka
        /// do kazhdoy tochki. Zapisyvaet 1 (near) ili 0 (far) v rezultat.
        ///
        /// Gisterezis realizovan cherez dva radiusa:
        ///   • Esli tochka UZhE aktivna (prevStatus=1) — ispolzuem deactivateRadiusSq
        ///   • Esli tochka NE aktivna (prevStatus=0) — ispolzuem activateRadiusSq
        ///
        /// Eto pozvolyaet izbezhat mertsaniya kollayderov na granitse radiusa.
        /// </summary>
        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct DistanceCalcJob : IJobParallelFor
        {
            [ReadOnly] public float3 playerPos;
            [ReadOnly] public float  activateRadiusSq;
            [ReadOnly] public float  deactivateRadiusSq;

            [ReadOnly]  public NativeArray<float3> positions;
            [ReadOnly]  public NativeArray<byte>   prevStatus;
            [WriteOnly] public NativeArray<byte>   results;

            public void Execute(int index)
            {
                float3 diff = positions[index] - playerPos;
                float distSq = math.lengthsq(diff);

                // Branchless hysteresis keeps Burst on a compare/select path instead of a divergent branch.
                bool wasActive = prevStatus[index] != 0;
                float radiusSq = math.select(activateRadiusSq, deactivateRadiusSq, wasActive);
                results[index] = (byte)math.select(0, 1, distSq <= radiusSq);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — INITsIALIZATsIYa TOChEK
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Initsializiruet sistemu massivom pozitsiy kamney/musora.
        /// Vyzyvaetsya odin raz posle generatsii mira ili zagruzki stseny.
        ///
        /// VAZhNO: peredaetsya kopiya dannyh. Originalnyy massiv mozhno
        /// osvobodit posle vyzova. NativeArray allotsiruetsya s Persistent.
        ///
        /// Primer ispolzovaniya:
        ///   var positions = new Vector3[10000]; // zapolnit pozitsiyami
        ///   proximitySystem.Initialize(positions);
        /// </summary>
        /// <param name="worldPositions">Mirovye koordinaty vseh tochek.</param>
        public void Initialize(Vector3[] worldPositions)
        {
            Initialize(worldPositions, worldPositions != null ? worldPositions.Length : 0);
        }

        /// <summary>
        /// Peregruzka dlya chastichnogo ispolzovaniya predvaritelno vydelennogo massiva.
        /// </summary>
        /// <param name="worldPositions">Bufer mirovyh koordinat.</param>
        /// <param name="count">Kolichestvo validnyh elementov v nachale bufera.</param>
        public void Initialize(Vector3[] worldPositions, int count)
        {
            if (worldPositions == null)
            {
                Hecton8.Core.H8Debug.LogError("[ProximityColliderSystem] Initialize: worldPositions is null!");
                return;
            }

            if (count <= 0)
            {
                ClearRuntimeData();
                return;
            }

            if (worldPositions.Length < count)
            {
                Hecton8.Core.H8Debug.LogError("[ProximityColliderSystem] Initialize: count exceeds buffer length!");
                return;
            }

            PrepareForReinitialize();

            _pointCount = count;

            if (!EnsureProximityVaultBuffers(_pointCount))
            {
                ClearRuntimeData();
                return;
            }

            // ── Kopiruem pozitsii v NativeArray<float3> ──
            if (!TryAcquirePositionWriteBuffer(out NativeArray<float3> positions))
            {
                ClearRuntimeData();
                return;
            }

            try
            {
                for (int i = 0; i < _pointCount; i++)
                {
                    positions[i] = new float3(
                        worldPositions[i].x,
                        worldPositions[i].y,
                        worldPositions[i].z
                    );
                }
            }
            finally
            {
                ReleasePositionWriteBuffer();
            }

            // ── Managed arrays (one-time allocation) ──
            _activeColliders = new GameObject[_pointCount];
            _prevStatus      = new byte[_pointCount];

            // ── Cache squared radii ──
            _activateRadiusSq   = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;

            _initialized = true;

#if UNITY_EDITOR
            _debugTotalPoints = _pointCount;
#endif
        }

        /// <summary>
        /// Peregruzka dlya NativeArray (zero-copy, esli vyzyvayuschiy
        /// garantiruet lifetime).
        /// VAZhNO: dannye KOPIRUYuTSYa — original mozhno osvobozhdat.
        /// </summary>
        public void Initialize(NativeArray<float3> worldPositions)
        {
            if (!worldPositions.IsCreated)
            {
                Hecton8.Core.H8Debug.LogError("[ProximityColliderSystem] Initialize: invalid NativeArray!");
                return;
            }

            if (worldPositions.Length == 0)
            {
                ClearRuntimeData();
                return;
            }

            PrepareForReinitialize();

            _pointCount = worldPositions.Length;

            if (!EnsureProximityVaultBuffers(_pointCount))
            {
                ClearRuntimeData();
                return;
            }

            // ── NativeArray.CopyFrom — bulk memcpy, zero GC ──
            if (!TryAcquirePositionWriteBuffer(out NativeArray<float3> positions))
            {
                ClearRuntimeData();
                return;
            }

            try
            {
                positions.CopyFrom(worldPositions);
            }
            finally
            {
                ReleasePositionWriteBuffer();
            }

            _activeColliders = new GameObject[_pointCount];
            _prevStatus      = new byte[_pointCount];

            _activateRadiusSq   = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;

            _initialized = true;

#if UNITY_EDITOR
            _debugTotalPoints = _pointCount;
#endif
        }

        public float ActivateRadius => activateRadius;
        public float DeactivateRadius => deactivateRadius;
        public int MaxOperationsPerFrame => maxOperationsPerTick;

        /// <summary>
        /// Polnostyu ochischaet runtime-sostoyanie sistemy.
        /// </summary>
        /// <remarks>
        /// Bezopasno zavershaet aktivnuyu Job, vozvraschaet vse vydannye collider proxy
        /// obratno v pul i osvobozhdaet vnutrennie bufery. Ispolzuetsya, kogda
        /// v mire bolshe ne ostalos tochek dlya blizhney fiziki ili trebuetsya
        /// pereinitsializirovat sistemu novym naborom pozitsiy.
        /// </remarks>
        public void ClearRuntimeData()
        {
            PrepareForReinitialize();
        }

        public void SetRuntimeBudget(float newActivateRadius, float newDeactivateRadius, int newMaxOperations)
        {
            activateRadius = Mathf.Max(4f, newActivateRadius);
            deactivateRadius = Mathf.Max(activateRadius + 2f, newDeactivateRadius);
            maxOperationsPerTick = Mathf.Max(4, newMaxOperations);
            _activateRadiusSq = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE — registratsiya v GameTickManager
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            PublishActiveRuntimeInstance();
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
            // ── Avto-resolve igroka cherez bootstrap, esli ssylka ne zadana ──
            CachePlayerRuntimeContextCold();
            CacheDataVaultCold();
            CacheObjectPool(null);
            TryRegisterHotSwapListener();

            // ── Validatsiya ──
            if (colliderPrefab == null)
            {
                Hecton8.Core.H8Debug.LogError("[ProximityColliderSystem] colliderPrefab is not assigned! " +
                               "System will not function.");
                enabled = false;
                return;
            }

            if (playerTransform == null)
            {
                Hecton8.Core.H8Debug.LogWarning("[ProximityColliderSystem] playerTransform is not ready during OnEnable. Runtime retry will continue.");
            }

            // ── Validatsiya radiusov ──
            if (deactivateRadius <= activateRadius)
            {
                Hecton8.Core.H8Debug.LogWarning("[ProximityColliderSystem] deactivateRadius should be > " +
                                 "activateRadius for proper hysteresis. Auto-correcting.");
                deactivateRadius = activateRadius + 5f;
            }

            TryRegisterDispatcherRoutes();
        }

        private void OnDisable()
        {
            // ── Zavershaem tekuschuyu Job, esli ona v polete ──
            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            CancelScheduledJobForTeardown();
            TryUnregisterHotSwapListener();
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPool(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    RebindDataVault(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherRoutes();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegisterDispatcherRoutes();
                    break;
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext previousContext = previousService as IPlayerRuntimeContext;
                    if (previousContext != null && ReferenceEquals(playerTransform, previousContext.PlayerTransform))
                        playerTransform = null;

                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    RefreshPlayerTransformFromCachedContext();
                    break;
            }
        }

        private void CacheObjectPool(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) ||
                ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                _objectPool = pool;
                return;
            }

            _objectPool = null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private static void DespawnColliderProxyOrDestroy(IObjectPoolService pool, GameObject colliderObject)
        {
            if (colliderObject == null)
                return;

            if (pool != null && pool.CanDespawnWithoutDestroy(colliderObject))
            {
                pool.Despawn(colliderObject);
                return;
            }

            Destroy(colliderObject);
        }

        private void TryRegisterDispatcherRoutes()
        {
            if (!Application.isPlaying)
                return;

            if (!_registeredToDispatcher)
                _registeredToDispatcher = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterDispatcherRoutes()
        {
            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
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

        private void OnDestroy()
        {
            // ── Zavershaem Job i vozvraschaem vse kollaydery v pul ──
            CancelScheduledJobForTeardown();
            DespawnAllColliders();
            Cleanup();
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ClearActiveRuntimeInstance();
        }

        private void PublishActiveRuntimeInstance()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                return;

            ActiveRuntimeInstance = this;
            ActiveRuntimeInstanceChanged?.Invoke(this);
        }

        private void ClearActiveRuntimeInstance()
        {
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged?.Invoke(null);
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — GLAVNYY GORYaChIY PUT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya kazhdyy kadr cherez GameTickManager.
        ///
        /// Pattern: "Schedule → Wait → Process → Schedule"
        ///
        /// Kadr N:   Schedule Job (vychislenie distantsiy)
        /// Kadr N+1: Complete Job, obrabotka rezultatov, Schedule novyy Job
        ///
        /// Eto daet Job tselyy kadr na vypolnenie — worker threads
        /// rabotayut parallelno s ostalnoy igrovoy logikoy.
        ///
        /// ZERO GC: nikakih allokatsiy. Vse massivy keshirovany.
        /// </summary>
#if UNITY_EDITOR
        private static void EnsureAssemblyReloadHook()
        {
            if (_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            EditorApplication.quitting += HandleEditorQuitting;
            _assemblyReloadHookRegistered = true;
        }

        private static void ReleaseAssemblyReloadHook()
        {
            if (!_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _assemblyReloadHookRegistered = false;
        }

        private static void HandleBeforeAssemblyReload()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void HandleEditorQuitting()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void TeardownActiveRuntimeInstanceForEditorReload()
        {
            ProximityColliderSystem activeRuntime = ActiveRuntimeInstance;
            if (activeRuntime == null)
                return;

            activeRuntime.PrepareForReinitialize();
            ActiveRuntimeInstance = null;
            ActiveRuntimeInstanceChanged?.Invoke(null);
        }
#endif

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (playerTransform == null)
            {
                RefreshPlayerTransformFromCachedContext();
                if (playerTransform == null)
                {
                    float now = (float)Hecton8.Core.SystemDispatcher.CurrentUnscaledTimeSeconds;
                    if (now >= _nextPlayerResolveWarningTime)
                    {
                        _nextPlayerResolveWarningTime = now + 5f;
                        LogPlayerResolveRetryFailed();
                    }

                    return;
                }
            }

            // ═══════════════════════════════════════════════════
            //  STEP 1: Obrabotka rezultatov predyduschey Job
            // ═══════════════════════════════════════════════════

            if (_jobScheduled)
            {
                _jobPendingFrameCount++;
                return;
            }

            // ═══════════════════════════════════════════════════
            //  STEP 2: Planiruem novuyu Job na sleduyuschiy kadr
            // ═══════════════════════════════════════════════════

            ScheduleDistanceJob();
        }

        public void LateFrameTick()
        {
            if (!_initialized || !_jobScheduled)
                return;

            if (!DispatcherJobSwap.TryComplete(ref _jobHandle, false))
                return;

            _jobScheduled = false;
            _jobPendingFrameCount = 0;

            try
            {
                ProcessJobResults();
            }
            finally
            {
                ReleaseJobBufferLocks();
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerResolveRetryFailed()
        {
            Hecton8.Core.H8Debug.LogWarning("[ProximityColliderSystem] playerTransform still unresolved after runtime retry.");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — JOB SCHEDULING
        // ══════════════════════════════════════════════════════════

        private void ScheduleDistanceJob()
        {
            if (!TryAcquireJobBuffers(
                    out NativeArray<float3> positions,
                    out NativeArray<byte> prevStatus,
                    out NativeArray<byte> results))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[ProximityColliderSystem] Proximity DataVault buffers are invalid. Clearing runtime data.");
#endif
                ClearRuntimeData();
                return;
            }

            // ── Sozdaem i planiruem Job ──
            bool scheduled = false;
            try
            {
                var job = new DistanceCalcJob
                {
                    playerPos          = new float3(
                        playerTransform.position.x,
                        playerTransform.position.y,
                        playerTransform.position.z),
                    activateRadiusSq   = _activateRadiusSq,
                    deactivateRadiusSq = _deactivateRadiusSq,
                    positions          = positions,
                    prevStatus         = prevStatus,
                    results            = results
                };

            // ── innerloopBatchCount = 256 ──
            // Kazhdyy worker thread obrabatyvaet pachku po 256 tochek.
            // Dlya 10,000 tochek = ~39 batchey. Na 4-yadernom CPU =
            // ~10 batchey na yadro. Otlichnyy balans overhead/parallelism.
                _jobHandle  = job.Schedule(_pointCount, 256);
                _jobScheduled = true;
                _jobPendingFrameCount = 0;
                scheduled = true;
            }
            finally
            {
                if (!scheduled)
                    ReleaseJobBufferLocks();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — OBRABOTKA REZULTATOV
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Chitaet rezultaty Job i vypolnyaet Spawn/Despawn.
        ///
        /// Ogranichenie maxOperationsPerTick predotvraschaet lag-spayk
        /// pri teleportatsii igroka (kogda razom nuzhno spavnit/despavnit
        /// sotni kollayderov). Ostavshiesya obrabotayutsya v sleduyuschih kadrah.
        ///
        /// ZERO GC: for-tsikl po NativeArray + managed array.
        /// </summary>
        private void ProcessJobResults()
        {
            if (!TryResolveCachedObjectPool(out IObjectPoolService pool)) return;
            if (!TryReadJobResults(out NativeArray<byte>.ReadOnly jobResults) ||
                !TryReadPositions(out NativeArray<float3>.ReadOnly positions))
            {
                return;
            }

            int operationsThisTick = 0;

#if UNITY_EDITOR
            int activeCount = 0;
#endif

            for (int i = 0; i < _pointCount; i++)
            {
                byte newStatus = jobResults[i];
                byte oldStatus = _prevStatus[i];

#if UNITY_EDITOR
                if (newStatus == 1) activeCount++;
#endif

                // ── Bez izmeneniy — skip ──
                if (newStatus == oldStatus) continue;

                // ── Limit operatsiy za kadr ──
                if (operationsThisTick >= maxOperationsPerTick) break;

                if (newStatus == 1 && oldStatus == 0)
                {
                    // ═══════════════════════════════════
                    //  ACTIVATE: tochka voshla v radius
                    // ═══════════════════════════════════

                    // Dvoynaya proverka: kollayder mozhet uzhe byt (race condition
                    // pri pereinitsializatsii). Propuskaem bez allokatsii.
                    if (_activeColliders[i] != null)
                    {
                        _prevStatus[i] = 1;
                        continue;
                    }

                    float3 pos = positions[i];
                    GameObject colliderObj = pool.Spawn(
                        colliderPrefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        Quaternion.identity
                    );

                    if (colliderObj != null)
                    {
                        _activeColliders[i] = colliderObj;
                        _prevStatus[i] = 1;
                        operationsThisTick++;
                    }
                }
                else if (newStatus == 0 && oldStatus == 1)
                {
                    // ═══════════════════════════════════
                    //  DEACTIVATE: tochka vyshla iz radiusa
                    // ═══════════════════════════════════

                    GameObject colliderObj = _activeColliders[i];

                    if (colliderObj != null)
                    {
                        DespawnColliderProxyOrDestroy(pool, colliderObj);
                        _activeColliders[i] = null;
                        operationsThisTick++;
                    }

                    _prevStatus[i] = 0;
                }
            }

#if UNITY_EDITOR
            _debugActiveColliders = activeCount;
            _debugJobFrameDelay = operationsThisTick;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CLEANUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Completes the active distance job in explicit teardown scope and releases DataVault writer fences.
        /// </summary>
        private void CancelScheduledJobForTeardown()
        {
            if (_jobScheduled)
            {
                TryCompleteProximityJobForTeardown(ref _jobHandle);
                _jobHandle = default;
                _jobScheduled = false;
                _jobPendingFrameCount = 0;
            }

            ReleaseJobBufferLocks();
        }

        private static bool TryCompleteProximityJobForTeardown(ref JobHandle handle)
        {
            bool completed;
            DispatcherJobSwap.BeginLateFrameSwapWindow();
            try
            {
                completed = DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            }
            finally
            {
                DispatcherJobSwap.EndLateFrameSwapWindow();
            }

            return completed;
        }

        /// <summary>
        /// Gotovit sistemu k bezopasnoy pereinitsializatsii.
        /// </summary>
        /// <remarks>
        /// Vazhno vyzyvat etot put pered osvobozhdeniem massivov. Inache mozhno
        /// dispose-nut dannye, poka Job esche rabotaet, ili ostavit aktivnye
        /// collider proxy viset posle smeny world-dannyh.
        /// </remarks>
        private void PrepareForReinitialize()
        {
            CancelScheduledJobForTeardown();
            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            DespawnAllColliders();
            Cleanup();
#if UNITY_EDITOR
            _debugTotalPoints = 0;
            _debugActiveColliders = 0;
            _debugJobFrameDelay = 0;
#endif
        }

        /// <summary>
        /// Vozvraschaet vse aktivnye kollaydery v pul.
        /// Vyzyvaetsya pri unichtozhenii ili pereinitsializatsii.
        /// </summary>
        private void DespawnAllColliders()
        {
            if (_activeColliders == null) return;

            TryResolveCachedObjectPool(out IObjectPoolService pool);

            for (int i = 0; i < _activeColliders.Length; i++)
            {
                GameObject obj = _activeColliders[i];
                if (obj != null)
                {
                    DespawnColliderProxyOrDestroy(pool, obj);

                    _activeColliders[i] = null;
                }
            }
        }

        /// <summary>
        /// Releases DataVault buffers and clears managed ownership.
        /// </summary>
        private void Cleanup()
        {
            ReleaseProximityBuffers();

            _activeColliders = null;
            _prevStatus      = null;
            _initialized     = false;
            _pointCount      = 0;
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;
            return _dataVault;
        }

        private void RebindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            CancelScheduledJobForTeardown();
            DespawnAllColliders();
            ReleaseProximityBuffers(_dataVault);
            _dataVault = dataVault;
            _activeColliders = null;
            _prevStatus = null;
            _initialized = false;
            _pointCount = 0;
#if UNITY_EDITOR
            _debugTotalPoints = 0;
            _debugActiveColliders = 0;
            _debugJobFrameDelay = 0;
#endif
        }

        private bool EnsureProximityVaultBuffers(int requiredCount)
        {
            if (requiredCount <= 0)
                return false;

            return EnsureProximityVaultBuffer(ref _positionsHandle, PositionsBufferId, requiredCount, NativeArrayOptions.UninitializedMemory) &&
                   EnsureProximityVaultBuffer(ref _jobResultsHandle, JobResultsBufferId, requiredCount, NativeArrayOptions.ClearMemory) &&
                   EnsureProximityVaultBuffer(ref _prevStatusHandle, PrevStatusBufferId, requiredCount, NativeArrayOptions.UninitializedMemory);
        }

        private bool EnsureProximityVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCount,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = CacheDataVaultCold();
            if (vault == null || requiredCount <= 0)
                return false;

            if (IsExactVaultHandle(in handle, bufferId) &&
                vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly existing) &&
                existing.IsCreated &&
                existing.Length >= requiredCount)
            {
                return true;
            }

            ReleaseProximityVaultHandle(vault, ref handle, bufferId);
            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredCount, VaultOwnerSystemId, options);
            return IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredCount;
        }

        private bool TryAcquirePositionWriteBuffer(out NativeArray<float3> positions)
        {
            positions = default;
            if (_jobBuffersLocked || _positionWriteVault != null || !EnsureProximityVaultBuffers(_pointCount))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in _positionsHandle, PositionsBufferId) ||
                !vault.TryAcquireWriteLock(in _positionsHandle, VaultOwnerSystemId, out positions))
            {
                return false;
            }

            bool keepLock = false;
            try
            {
                if (positions.IsCreated && positions.Length >= _pointCount)
                {
                    _positionWriteVault = vault;
                    keepLock = true;
                    return true;
                }

                return false;
            }
            finally
            {
                if (!keepLock)
                {
                    vault.ReleaseWriteLock(in _positionsHandle, VaultOwnerSystemId);
                    positions = default;
                }
            }
        }

        private void ReleasePositionWriteBuffer()
        {
            IDataVault vault = _positionWriteVault;
            _positionWriteVault = null;
            if (vault != null && IsExactVaultHandle(in _positionsHandle, PositionsBufferId))
                vault.ReleaseWriteLock(in _positionsHandle, VaultOwnerSystemId);
        }

        private bool TryAcquireJobBuffers(
            out NativeArray<float3> positions,
            out NativeArray<byte> prevStatus,
            out NativeArray<byte> results)
        {
            positions = default;
            prevStatus = default;
            results = default;
            if (_jobBuffersLocked || !EnsureProximityVaultBuffers(_pointCount))
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;
            if (!IsExactVaultHandle(in _positionsHandle, PositionsBufferId) ||
                !IsExactVaultHandle(in _prevStatusHandle, PrevStatusBufferId) ||
                !IsExactVaultHandle(in _jobResultsHandle, JobResultsBufferId))
            {
                return false;
            }

            bool prevWriteLocked = false;
            bool guardAcquired = false;
            bool success = false;
            try
            {
                if (!vault.TryAcquireWriteLock(in _prevStatusHandle, VaultOwnerSystemId, out NativeArray<byte> prevStatusWrite))
                {
                    return false;
                }
                prevWriteLocked = true;

                if (!prevStatusWrite.IsCreated || prevStatusWrite.Length < _pointCount)
                {
                    return false;
                }

                // Copy the active logical range while exactly one writer fence is held.
                NativeArray<byte>.Copy(_prevStatus, 0, prevStatusWrite, 0, _pointCount);
                vault.ReleaseWriteLock(in _prevStatusHandle, VaultOwnerSystemId);
                prevWriteLocked = false;

                if (!vault.TryAcquireMutationGuard(_jobMutationGuardMask))
                    return false;
                guardAcquired = true;

                if (!vault.TryResolveHandle(in _positionsHandle, out positions) ||
                    !vault.TryResolveHandle(in _prevStatusHandle, out prevStatus) ||
                    !vault.TryResolveHandle(in _jobResultsHandle, out results))
                {
                    return false;
                }

                if (!positions.IsCreated ||
                    positions.Length < _pointCount ||
                    !prevStatus.IsCreated ||
                    prevStatus.Length < _pointCount ||
                    !results.IsCreated || results.Length < _pointCount)
                {
                    return false;
                }

                _jobBufferGuardVault = vault;
                _jobBuffersLocked = true;
                success = true;
                return true;
            }
            finally
            {
                if (!success)
                {
                    if (guardAcquired)
                        vault.ReleaseMutationGuard(_jobMutationGuardMask);
                    if (prevWriteLocked)
                        vault.ReleaseWriteLock(in _prevStatusHandle, VaultOwnerSystemId);
                    positions = default;
                    prevStatus = default;
                    results = default;
                }
            }
        }

        private bool TryReadPositions(out NativeArray<float3>.ReadOnly positions)
        {
            return TryReadProximityVaultBuffer(in _positionsHandle, PositionsBufferId, _pointCount, out positions);
        }

        private bool TryReadJobResults(out NativeArray<byte>.ReadOnly results)
        {
            return TryReadProximityVaultBuffer(in _jobResultsHandle, JobResultsBufferId, _pointCount, out results);
        }

        private bool TryReadProximityVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredCount,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredCount > 0 &&
                   IsExactVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredCount;
        }

        private void ReleaseJobBufferLocks()
        {
            if (!_jobBuffersLocked)
                return;

            IDataVault vault = _jobBufferGuardVault;
            vault?.ReleaseMutationGuard(_jobMutationGuardMask);
            _jobBufferGuardVault = null;
            _jobBuffersLocked = false;
        }

        private void ReleaseProximityBuffers()
        {
            ReleaseProximityBuffers(_dataVault);
        }

        private void ReleaseProximityBuffers(IDataVault vault)
        {
            ReleasePositionWriteBuffer();
            ReleaseJobBufferLocks();
            ReleaseProximityVaultHandle(vault, ref _positionsHandle, PositionsBufferId);
            ReleaseProximityVaultHandle(vault, ref _jobResultsHandle, JobResultsBufferId);
            ReleaseProximityVaultHandle(vault, ref _prevStatusHandle, PrevStatusBufferId);
        }

        private static void ReleaseProximityVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsExactVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private static bool IsExactVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)VaultOwnerSystemId &&
                   handle.Generation != 0u;
        }

        private static ulong ProximityMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — RUNTIME UPDATES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obnovlyaet pozitsiyu odnoy tochki (naprimer, kamen sdvinulsya).
        /// ZERO GC. O(1).
        /// </summary>
        public void UpdatePointPosition(int index, Vector3 newPosition)
        {
            if (!_initialized) return;
            if (index < 0 || index >= _pointCount) return;
            if (_jobScheduled) return;

            // Job completion is owned by LateFrameTick; writes are skipped while a job reads this buffer.
            if (!TryAcquirePositionWriteBuffer(out NativeArray<float3> positions))
                return;

            try
            {
                positions[index] = new float3(newPosition.x, newPosition.y, newPosition.z);
            }
            finally
            {
                ReleasePositionWriteBuffer();
            }
        }

        /// <summary>
        /// Menyaet Transform igroka v rantayme (naprimer, smena kontrollera).
        /// </summary>
        public void SetPlayerTransform(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        private void CachePlayerRuntimeContextCold()
        {
            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            RefreshPlayerTransformFromCachedContext();
        }

        private void RefreshPlayerTransformFromCachedContext()
        {
            if (playerTransform != null)
                return;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            if (playerRuntimeContext != null && playerRuntimeContext.PlayerTransform != null)
                playerTransform = playerRuntimeContext.PlayerTransform;
        }

        /// <summary>
        /// Obnovlyaet radiusy aktivatsii/deaktivatsii.
        /// Keshiruet kvadraty dlya Job.
        /// </summary>
        public void SetRadii(float activate, float deactivate)
        {
            activateRadius      = activate;
            deactivateRadius    = deactivate;
            _activateRadiusSq   = activate * activate;
            _deactivateRadiusSq = deactivate * deactivate;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR VALIDATION
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (deactivateRadius <= activateRadius)
                deactivateRadius = activateRadius + 5f;

            if (maxOperationsPerTick < 1)
                maxOperationsPerTick = 1;

            // ── Obnovlyaem kesh, esli izmenili v Inspector vo vremya Play ──
            if (Application.isPlaying && _initialized)
            {
                _activateRadiusSq   = activateRadius * activateRadius;
                _deactivateRadiusSq = deactivateRadius * deactivateRadius;
            }
        }

        /// <summary>
        /// Vizualizatsiya radiusov v Scene View.
        /// Risuem dva kruga: zelenyy (activate) i krasnyy (deactivate).
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Vector3 pos = playerTransform.position;

            // ── Radius aktivatsii (zelenyy) ──
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(pos, activateRadius);

            // ── Radius deaktivatsii (krasnyy) ──
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(pos, deactivateRadius);

            // ── Zona gisterezisa (zheltaya, zapolnennaya) ──
            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Gizmos.DrawSphere(pos, deactivateRadius);
        }
#endif
    }
}
