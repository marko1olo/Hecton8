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
using Hecton8.Bootstrap;
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

        // ── Job I/O (persistent allocations) ──
        private NativeArray<float3> _positions;      // pozitsii vseh tochek
        private NativeArray<byte>   _jobResults;     // rezultat Job: 0=far, 1=near
        private ObjectPoolManager _objectPool;

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
        private int       _jobPendingFrameCount;

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
                Debug.LogError("[ProximityColliderSystem] Initialize: worldPositions is null!");
                return;
            }

            if (count <= 0)
            {
                ClearRuntimeData();
                return;
            }

            if (worldPositions.Length < count)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: count exceeds buffer length!");
                return;
            }

            PrepareForReinitialize();

            _pointCount = count;

            // ── Allokatsiya NativeArrays (Persistent — zhivut do Dispose) ──
            _positions  = new NativeArray<float3>(_pointCount, Allocator.Persistent,
                                                   NativeArrayOptions.UninitializedMemory);
            _jobResults = new NativeArray<byte>(_pointCount, Allocator.Persistent,
                                                 NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[pointCount] - persistent previous proximity state mirror for async distance jobs - owner: ProximityColliderSystem
            _prevStatusNative = new NativeArray<byte>(_pointCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterNativeBuffers();

            // ── Kopiruem pozitsii v NativeArray<float3> ──
            for (int i = 0; i < _pointCount; i++)
            {
                _positions[i] = new float3(
                    worldPositions[i].x,
                    worldPositions[i].y,
                    worldPositions[i].z
                );
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

            Debug.Log($"[ProximityColliderSystem] Initialized with {_pointCount} points. " +
                      $"Activate: {activateRadius}m, Deactivate: {deactivateRadius}m");
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
                Debug.LogError("[ProximityColliderSystem] Initialize: invalid NativeArray!");
                return;
            }

            if (worldPositions.Length == 0)
            {
                ClearRuntimeData();
                return;
            }

            PrepareForReinitialize();

            _pointCount = worldPositions.Length;

            _positions  = new NativeArray<float3>(_pointCount, Allocator.Persistent,
                                                   NativeArrayOptions.UninitializedMemory);
            _jobResults = new NativeArray<byte>(_pointCount, Allocator.Persistent,
                                                 NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[pointCount] - persistent previous proximity state mirror for async distance jobs - owner: ProximityColliderSystem
            _prevStatusNative = new NativeArray<byte>(_pointCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            RegisterNativeBuffers();

            // ── NativeArray.CopyFrom — bulk memcpy, zero GC ──
            _positions.CopyFrom(worldPositions);

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
            ActiveRuntimeInstance = this;
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
            // ── Avto-resolve igroka cherez bootstrap, esli ssylka ne zadana ──
            TryResolvePlayerTransform();
            CacheObjectPool(GlobalRegistry.ObjectPool);
            TryRegisterHotSwapListener();

            // ── Validatsiya ──
            if (colliderPrefab == null)
            {
                Debug.LogError("[ProximityColliderSystem] colliderPrefab is not assigned! " +
                               "System will not function.");
                enabled = false;
                return;
            }

            if (playerTransform == null)
            {
                Debug.LogWarning("[ProximityColliderSystem] playerTransform is not ready during OnEnable. Runtime retry will continue.");
            }

            // ── Validatsiya radiusov ──
            if (deactivateRadius <= activateRadius)
            {
                Debug.LogWarning("[ProximityColliderSystem] deactivateRadius should be > " +
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
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryRegisterDispatcherRoutes();
                    break;
            }
        }

        private void CacheObjectPool(ObjectPoolManager objectPool)
        {
            _objectPool = objectPool;
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
            JobHandle teardownDependency = CancelScheduledJobForTeardown();
            DespawnAllColliders();
            Cleanup(teardownDependency);
            TryUnregisterHotSwapListener();
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
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

            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
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
            if (ActiveRuntimeInstance == null)
                return;

            ActiveRuntimeInstance.PrepareForReinitialize();
            ActiveRuntimeInstance = null;
        }
#endif

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (playerTransform == null)
            {
                TryResolvePlayerTransform();
                if (playerTransform == null)
                {
                    if (Time.unscaledTime >= _nextPlayerResolveWarningTime)
                    {
                        _nextPlayerResolveWarningTime = Time.unscaledTime + 5f;
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

            ProcessJobResults();
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPlayerResolveRetryFailed()
        {
            Debug.LogWarning("[ProximityColliderSystem] playerTransform still unresolved after runtime retry.");
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — JOB SCHEDULING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Kopiruet prevStatus v NativeArray i planiruet Burst Job.
        /// Persistent buffer avoids TempJob lifetime warnings when a distance job spans multiple frames.
        /// </summary>
        private NativeArray<byte> _prevStatusNative;

        private void ScheduleDistanceJob()
        {
            if (!_prevStatusNative.IsCreated || _prevStatusNative.Length != _pointCount)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError("[ProximityColliderSystem] Native previous-status buffer is invalid. Clearing runtime data.");
#endif
                ClearRuntimeData();
                return;
            }

            // ── Kopiruem managed → native (memcpy, zero GC) ──
            // NativeArray<byte>.CopyFrom(byte[]) — spetsializirovannyy fast path.
            _prevStatusNative.CopyFrom(_prevStatus);

            // ── Sozdaem i planiruem Job ──
            var job = new DistanceCalcJob
            {
                playerPos          = new float3(
                    playerTransform.position.x,
                    playerTransform.position.y,
                    playerTransform.position.z),
                activateRadiusSq   = _activateRadiusSq,
                deactivateRadiusSq = _deactivateRadiusSq,
                positions          = _positions,
                prevStatus         = _prevStatusNative,
                results            = _jobResults
            };

            // ── innerloopBatchCount = 256 ──
            // Kazhdyy worker thread obrabatyvaet pachku po 256 tochek.
            // Dlya 10,000 tochek = ~39 batchey. Na 4-yadernom CPU =
            // ~10 batchey na yadro. Otlichnyy balans overhead/parallelism.
            _jobHandle  = job.Schedule(_pointCount, 256);
            _jobScheduled = true;
            _jobPendingFrameCount = 0;
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
            ObjectPoolManager pool = _objectPool;
            if (pool == null) return;

            int operationsThisTick = 0;

#if UNITY_EDITOR
            int activeCount = 0;
#endif

            for (int i = 0; i < _pointCount; i++)
            {
                byte newStatus = _jobResults[i];
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

                    float3 pos = _positions[i];
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
                        pool.Despawn(colliderObj);
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
        /// Detaches the active job handle for teardown without blocking the main thread.
        /// </summary>
        private JobHandle CancelScheduledJobForTeardown()
        {
            if (!_jobScheduled)
                return default;

            JobHandle dependency = _jobHandle;
            _jobHandle = default;
            _jobScheduled = false;
            _jobPendingFrameCount = 0;
            return dependency;
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
            JobHandle teardownDependency = CancelScheduledJobForTeardown();
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
            Cleanup(teardownDependency);
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

            ObjectPoolManager pool = _objectPool;

            for (int i = 0; i < _activeColliders.Length; i++)
            {
                GameObject obj = _activeColliders[i];
                if (obj != null)
                {
                    if (pool != null)
                        pool.Despawn(obj);
                    else
                        Destroy(obj); // fallback esli pul unichtozhen

                    _activeColliders[i] = null;
                }
            }
        }

        /// <summary>
        /// Releases NativeArrays with deferred disposal and clears managed ownership.
        /// </summary>
        private void Cleanup(JobHandle dependency)
        {
            JobHandle disposeDependency = dependency;

            if (_positions.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_positions);
                disposeDependency = _positions.Dispose(disposeDependency);
                _positions = default;
            }

            if (_jobResults.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_jobResults);
                disposeDependency = _jobResults.Dispose(disposeDependency);
                _jobResults = default;
            }

            if (_prevStatusNative.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_prevStatusNative);
                disposeDependency = _prevStatusNative.Dispose(disposeDependency);
                _prevStatusNative = default;
            }

            _activeColliders = null;
            _prevStatus      = null;
            _initialized     = false;
            _pointCount      = 0;
        }

        private void RegisterNativeBuffers()
        {
            NativeMemorySentinel.RegisterNativeArray(_positions, nameof(ProximityColliderSystem), nameof(_positions), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_jobResults, nameof(ProximityColliderSystem), nameof(_jobResults), NativeAllocationLifetime.Scene);
            NativeMemorySentinel.RegisterNativeArray(_prevStatusNative, nameof(ProximityColliderSystem), nameof(_prevStatusNative), NativeAllocationLifetime.Scene);
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

            // ── Bezopasno: NativeArray write mezhdu Jobs ──
            // Job completion is owned by LateFrameTick; writes are skipped while a job reads this buffer.
            _positions[index] = new float3(newPosition.x, newPosition.y, newPosition.z);
        }

        /// <summary>
        /// Menyaet Transform igroka v rantayme (naprimer, smena kontrollera).
        /// </summary>
        public void SetPlayerTransform(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        private void TryResolvePlayerTransform()
        {
            if (playerTransform != null)
                return;

            GameBootstrapper.TryGetCurrentPlayerTransform(out playerTransform);
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
