// ============================================================================
// HECTON-8 — BuilderTool.cs
// Vizualnyy most mezhdu PlayerToolManager i PlayerBuilder.
//
// OTVETSTVENNOSTI:
//   1. Visual Bridge: delegiruet OnEquip/OnUnequip/UsePrimary/UseSecondary/
//      ToolTick v PlayerBuilder (logicheskiy kontroller stroitelstva).
//   2. Auto-Binding: pri spavne iz pula nahodit Player root po tegu,
//      izvlekaet i keshiruet PlayerInventory, PlayerBuilder, Camera.
//   3. NASA-Punk Sway: model instrumenta plavno otstaet ot povorota
//      kamery, sozdavaya oschuschenie vesa i inertsii.
//   4. LCD Screen: otobrazhaet imya aktivnogo BuildableData na MeshRenderer
//      cherez MaterialPropertyBlock (zero GC per-frame).
//
// NE SODERZhIT stroitelnoy logiki — tolko vizual i delegatsiya.
//
// ZERO GC V RANTAYME:
//   • Nikakih strokovyh allokatsiy v ToolTick.
//   • MaterialPropertyBlock — pre-allocated, reused.
//   • Unity.Mathematics quaternion.slerp — struct math, zero boxing.
//   • Player lookup — GameBootstrapper cached player transform, no scene search.
//
// LIFECYCLE:
//   ObjectPoolManager.Spawn() → OnSpawn() → [PlayerToolManager] → OnEquip()
//   → ToolTick()/UsePrimary()/UseSecondary() → OnUnequip() → OnDespawn()
// ============================================================================

namespace Hecton8.Gameplay
{
    using Hecton8.Bootstrap;
    using Hecton8.Building;
    using Hecton8.Core;
    using Hecton8.Inventory;
    using Unity.Mathematics;
    using UnityEngine;

    [DisallowMultipleComponent]
    public sealed class BuilderTool : PlayerTool, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUAL
        // ══════════════════════════════════════════════════════════

        [Header("── Sway Settings (NASA-Punk) ─────────────────")]
        [Tooltip("Skorost, s kotoroy model dogonyaet kameru. Menshe = bolshe inertsii, tyazhelee oschuschenie.")]
        [SerializeField] private float swaySpeed = 8f;

        [Tooltip("Maksimalnoe otklonenie sway ot kamery (gradusy). Ogranichivaet vizualnyy lag pri bystryh povorotah.")]
        [SerializeField] private float swayMaxAngle = 12f;

        [Header("── LCD Screen ────────────────────────────────")]
        [Tooltip("MeshRenderer malenkogo LCD-ekrana na modeli instrumenta. Esli null - ekran ne obnovlyaetsya (net allokatsiy, net oshibok).")]
        public MeshRenderer screenRenderer;

        [Tooltip("Indeks materiala na screenRenderer dlya LCD-ekrana. Obychno 0, esli ekran - otdelnyy submesh.")]
        [SerializeField] private int screenMaterialIndex;

        // ══════════════════════════════════════════════════════════
        //  CACHED SCENE REFERENCES (auto-bound in OnSpawn)
        // ══════════════════════════════════════════════════════════

        /// <summary>Logicheskiy kontroller stroitelstva na Player root.</summary>
        private PlayerBuilder  _playerBuilder;

        /// <summary>Inventar igroka (dlya buduschih rasshireniy — proverka resursov v UI).</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Keshirovannyy Transform osnovnoy kamery.</summary>
        private Transform _cameraTransform;

        // ══════════════════════════════════════════════════════════
        //  SWAY STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Tekuschiy povorot sway modeli.
        /// Unity.Mathematics quaternion — struct, zero GC.
        /// Initsializiruetsya pri OnEquip iz tekuschego povorota kamery.
        /// </summary>
        private quaternion _swayRotation;
        private float _cachedSwayLimitAngle = -1f;
        private float _cachedSwayLimitSinSq;

        /// <summary>
        /// Transform kornya modeli instrumenta (this.transform).
        /// Keshiruetsya dlya izbezhaniya povtornyh vyzovov get_transform().
        /// </summary>
        private Transform _selfTransform;

        // ══════════════════════════════════════════════════════════
        //  LCD SCREEN STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated MaterialPropertyBlock. Reused kazhdyy kadr.
        /// Zero GC pri SetTexture/SetColor/SetFloat.
        /// </summary>
        private MaterialPropertyBlock _screenPropBlock;

        /// <summary>
        /// Shader property ID dlya teksta na ekrane.
        /// Keshiruetsya cherez Shader.PropertyToID — vyzyvaetsya odin raz.
        /// Ispolzuetsya s _ScreenText (Vector4, kodiruyuschiy ASCII/indeks).
        /// Alternativa: _MainTex dlya teksturnogo atlasa shriftov.
        /// </summary>
        private static readonly int PropScreenColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color ScreenOfflineColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        private static readonly Color ScreenMissingCostColor = new Color(0.9f, 0.55f, 0.18f, 1f);
        private static readonly Color ScreenReadyColor = new Color(0.2f, 0.85f, 1f, 1f);
        private static readonly Color ScreenSnapReadyColor = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color ScreenBlockedColor = new Color(1f, 0.28f, 0.22f, 1f);

        /// <summary>
        /// Posledniy otobrazhennyy buildable. Dlya skip-proverki —
        /// ne obnovlyaem ekran, esli modul ne izmenilsya.
        /// </summary>
        private BuildableData _lastDisplayedBuildable;
        private PlayerBuilder.BuildReadiness _lastReadinessState;
        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder tool legacy string bridge - owner: BuilderTool
        private bool _screenVisualDirty;
        private bool _lateFrameRegistered;

        /// <summary>Flag uspeshnoy privyazki k stsene.</summary>
        private bool _bound;

        // ══════════════════════════════════════════════════════════
        //  IPoolable — POOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya ObjectPoolManager pri izvlechenii iz pula.
        ///
        /// KRITIChESKAYa TOChKA AUTO-BINDING:
        /// Instrument spavnitsya v HandAnchor iz pula — u nego net
        /// Inspector-ssylok na obekty stseny. Nahodim Player root
        /// po tegu i izvlekaem nuzhnye komponenty.
        ///
        /// Allokatsii: GameBootstrapper cached lookup; no scene search in OnSpawn.
        /// </summary>
        private void Awake()
        {
            EnsureScreenPropertyBlock();
        }

        public override void OnSpawn()
        {
            base.OnSpawn();

            EnsureScreenPropertyBlock();

            _selfTransform = transform;
            _bound         = false;

            // ── Auto-Binding: nayti Player root ──
            Transform playerTransform = null;
            if (!TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext) ||
                playerContext.PlayerTransform == null)
            {
                GameBootstrapper.TryGetCurrentPlayerTransform(out playerTransform);
            }
            else
            {
                playerTransform = playerContext.PlayerTransform;
            }

            if (playerTransform == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError("[BuilderTool] OnSpawn: Player transform could not be resolved via GameBootstrapper. Builder tool will not function.");
#endif
                return;
            }

            // ── Izvlechenie komponentov s Player root ──
            // GetComponent na konkretnom GameObject — zero GC (TryGetComponent).

            if (!TryBindPlayerReferencesCold(playerTransform))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
                    "[BuilderTool] OnSpawn: PlayerBuilder not found on Player root!");
#endif
                return;
            }

            if (_playerInventory == null && !playerTransform.TryGetComponent(out _playerInventory))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[BuilderTool] OnSpawn: PlayerInventory not found on Player root. Resource display will be unavailable.");
#endif
                // Ne kritichno — prodolzhaem bez inventarya
            }

            // ── Kesh Main Camera Transform ──
            Camera playerCamera = null;
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext runtimeContext))
                playerCamera = runtimeContext.PlayerCamera;
            if (playerCamera == null)
                playerTransform.TryGetComponent(out playerCamera);
            if (playerCamera != null)
            {
                _cameraTransform = playerCamera.transform;
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning(
                    "[BuilderTool] OnSpawn: Player camera not found in player hierarchy. Sway effect disabled.");
#endif
            }

            _lastDisplayedBuildable = null;
            _lastReadinessState = PlayerBuilder.BuildReadiness.Offline;
            _bound = true;
        }

        /// <summary>
        /// Vyzyvaetsya ObjectPoolManager pri vozvrate v pul.
        /// Ochischaet vse keshirovannye ssylki na stsenu.
        /// </summary>
        public override void OnDespawn()
        {
            _playerBuilder   = null;
            _playerInventory = null;
            _cameraTransform = null;
            _selfTransform   = null;
            _bound           = false;

            _lastDisplayedBuildable = null;
            _lastReadinessState = PlayerBuilder.BuildReadiness.Offline;
            _screenVisualDirty = false;
            TryUnregisterLateFrameTick();

            base.OnDespawn();
        }

        private bool TryBindPlayerReferencesCold(Transform playerTransform)
        {
            if (TryGetPlayerRuntimeContext(out IPlayerRuntimeContext playerContext))
            {
                _playerBuilder = playerContext.PlayerBuilder;
                _playerInventory = playerContext.Inventory;
                Camera playerCamera = playerContext.PlayerCamera;
                if (playerCamera != null)
                    _cameraTransform = playerCamera.transform;
            }

            if (playerTransform != null)
            {
                if (_playerBuilder == null)
                    playerTransform.TryGetComponent(out _playerBuilder);
                if (_playerInventory == null)
                    playerTransform.TryGetComponent(out _playerInventory);
                if (_cameraTransform == null && playerTransform.TryGetComponent(out Camera playerCamera))
                    _cameraTransform = playerCamera.transform;
            }

            return _playerBuilder != null;
        }

        protected override void OnToolRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            base.OnToolRegistryServiceReplaced(serviceSlot, previousService, currentService);
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                if (currentService is IPlayerRuntimeContext playerContext && playerContext.IsInitialized)
                {
                    _bound = TryBindPlayerReferencesCold(playerContext.PlayerTransform);
                    if (_bound && isActiveAndEnabled)
                        QueueScreenRefresh();
                }
                else
                {
                    _playerBuilder = null;
                    _playerInventory = null;
                    _cameraTransform = null;
                    _bound = false;
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                return;

            bool needsLateFrameTick = _lateFrameRegistered || _screenVisualDirty;
            TryUnregisterLateFrameTick();
            if (needsLateFrameTick && currentService != null && isActiveAndEnabled)
                TryRegisterLateFrameTick();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE — delegatsiya v PlayerBuilder
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vhod v rezhim stroitelstva.
        /// Aktiviruet ghost cherez PlayerBuilder i initsializiruet sway.
        /// </summary>
        public override void OnEquip()
        {
            base.OnEquip();

            if (!_bound) return;

            // ── Delegatsiya: aktivirovat prizrak postroyki ──
            _playerBuilder.OnEquip();

            // ── Initsializatsiya sway iz tekuschego povorota kamery ──
            if (_cameraTransform != null)
            {
                _swayRotation = _cameraTransform.rotation;
            }
            else
            {
                _swayRotation = quaternion.identity;
            }

            // ── Obnovit LCD ekran s tekuschim modulem ──
            QueueScreenRefresh();
        }

        /// <summary>
        /// Vyhod iz rezhima stroitelstva.
        /// Deaktiviruet ghost cherez PlayerBuilder.
        /// </summary>
        public override void OnUnequip()
        {
            if (_bound && _playerBuilder != null)
            {
                _playerBuilder.OnUnequip();
            }

            _lastDisplayedBuildable = null;
            _lastReadinessState = PlayerBuilder.BuildReadiness.Offline;

            base.OnUnequip();
        }

        /// <summary>
        /// Osnovnoe deystvie (LKM): razmeschenie modulya.
        /// Delegiruet v PlayerBuilder.UsePrimary().
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            if (!_bound) return;

            _playerBuilder.UsePrimary(deltaTime);
        }

        /// <summary>
        /// Alternativnoe deystvie (PKM): vraschenie prizraka.
        /// Delegiruet v PlayerBuilder.UseSecondary().
        /// </summary>
        public override void UseSecondary(float deltaTime)
        {
            if (!_bound) return;

            _playerBuilder.UseSecondary(deltaTime);
        }

        /// <summary>
        /// Vyzyvaetsya kazhdyy kadr cherez PlayerToolManager.
        ///
        /// Vypolnyaet:
        ///   1. Delegatsiyu ToolTick v PlayerBuilder (obnovlenie ghost pozitsii).
        ///   2. Sway-effekt modeli instrumenta (NASA-punk inertia).
        ///   3. Obnovlenie LCD-ekrana (tolko pri smene modulya).
        ///
        /// ZERO GC: Unity.Mathematics struct math, no string ops,
        /// MaterialPropertyBlock reuse.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            if (!_bound) return;

            // ── 1. Delegatsiya logiki stroitelstva ──
            _playerBuilder.ToolTick(deltaTime);

            // ── 2. Sway-effekt ──
            ApplySway(deltaTime);

            // ── 3. LCD-ekran (skip esli modul ne izmenilsya) ──
            BuildableData current = _playerBuilder.ActiveBuildable;
            PlayerBuilder.BuildReadiness readiness = _playerBuilder.ActiveBuildReadiness;
            bool brownoutActive = TryGetToolBrownoutFlicker(out _);
            if (brownoutActive || !ReferenceEquals(current, _lastDisplayedBuildable) || readiness != _lastReadinessState)
            {
                QueueScreenRefresh();
            }
        }

        public void LateFrameTick()
        {
            if (_screenVisualDirty)
            {
                _screenVisualDirty = false;
                UpdateScreen();
            }

            if (!IsEquipped && !_screenVisualDirty)
                TryUnregisterLateFrameTick();
        }

        // ══════════════════════════════════════════════════════════
        //  SWAY — NASA-Punk Inertia Effect
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Model instrumenta plavno otstaet ot povorota kamery.
        ///
        /// Algoritm:
        ///   1. Tselevoy povorot = kamera (quaternion).
        ///   2. Sway-povorot interpoliruetsya k tseli cherez slerp
        ///      s eksponentsialnym sglazhivaniem (frame-rate independent).
        ///   3. Delta mezhdu sway i kameroy ogranichivaetsya swayMaxAngle.
        ///   4. Model povorachivaetsya na sway-povorot.
        ///
        /// Unity.Mathematics quaternion — struct, zero GC, SIMD-friendly.
        ///
        /// Vizualnyy rezultat: pri bystrom povorote myshi instrument
        /// «zapazdyvaet», sozdavaya oschuschenie massy (NASA-punk aesthetic).
        /// </summary>
        public override string BuildLegacyOperationalSummaryString()
        {
            return "BUILDER";
        }

        public override void WriteOperationalSummary(ref FixedCharBuffer buffer)
        {
            if (!_bound || _playerBuilder == null)
            {
                AppendText(ref buffer, "BUILDER // OFFLINE");
                return;
            }

            AppendText(ref buffer, "BUILDER // ");
            _playerBuilder.WriteActiveBuildOperationalSummary(ref buffer);
            AppendText(ref buffer, " // ");
            _playerBuilder.WriteActiveBuildStatusLabel(ref buffer);
        }

        public override string BuildLegacyOperationalDirectiveString()
        {
            return "Restore builder link before field deployment.";
        }

        public override void WriteOperationalDirective(ref FixedCharBuffer buffer)
        {
            if (!_bound || _playerBuilder == null)
            {
                AppendText(ref buffer, "Restore builder link before field deployment.");
                return;
            }

            _playerBuilder.WriteActiveBuildAdvice(ref buffer);
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private void ApplySway(float dt)
        {
            if (_cameraTransform == null || _selfTransform == null) return;

            // ── Tselevoy povorot kamery ──
            quaternion cameraRot = _cameraTransform.rotation;

            // ── Frame-rate independent exponential slerp ──
            // t = 1 - exp(-speed * dt) obespechivaet odinakovuyu
            // vizualnuyu skorost pri 30, 60 i 144 fps.
            float t = ResolveDecayBlend(swaySpeed, dt);

            _swayRotation = math.slerp(_swayRotation, cameraRot, t);

            // Cheap visual clamp: squared quaternion-vector gate, no per-frame acos/degrees.
            quaternion delta = math.mul(math.inverse(cameraRot), _swayRotation);
            float4 deltaValue = delta.value;
            float vectorSinSq = math.lengthsq(deltaValue.xyz);
            float limitSinSq = ResolveSwayLimitSinSq();

            if (vectorSinSq > limitSinSq)
            {
                float clampT = math.saturate((vectorSinSq - limitSinSq) / math.max(vectorSinSq, 0.0001f));
                _swayRotation = math.nlerp(_swayRotation, cameraRot, clampT);
            }

            // ── Primenyaem k modeli ──
            _selfTransform.rotation = _swayRotation;
        }

        // ══════════════════════════════════════════════════════════
        //  LCD SCREEN — Visual Feedback
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obnovlyaet LCD-ekran na modeli instrumenta.
        ///
        /// Tekuschaya realizatsiya: menyaet emission color na osnove
        /// nalichiya/otsutstviya aktivnogo modulya.
        ///
        /// Buduschee rasshirenie: teksturnyy atlas shriftov dlya
        /// otobrazheniya imeni modulya (RenderTexture → material).
        ///
        /// ZERO GC: MaterialPropertyBlock — pre-allocated, reused.
        /// SetPropertyBlock ne allotsiruet.
        ///
        /// Vyzyvaetsya TOLKO pri smene aktivnogo modulya (ne per-frame).
        /// </summary>
        public void UpdateScreen()
        {
            if (screenRenderer == null) return;
            EnsureScreenPropertyBlock();

            BuildableData buildable = null;

            if (_playerBuilder != null)
            {
                buildable = _playerBuilder.ActiveBuildable;
            }

            _lastDisplayedBuildable = buildable;

            // ── Poluchaem tekuschiy property block (merge s suschestvuyuschimi) ──
            screenRenderer.GetPropertyBlock(_screenPropBlock, screenMaterialIndex);

            Color screenColor = ScreenOfflineColor;

            if (buildable != null && _playerBuilder != null)
            {
                PlayerBuilder.BuildReadiness readiness = _playerBuilder.ActiveBuildReadiness;
                _lastReadinessState = readiness;

                switch (readiness)
                {
                    case PlayerBuilder.BuildReadiness.MissingCost:
                        screenColor = ScreenMissingCostColor;
                        break;
                    case PlayerBuilder.BuildReadiness.PlacementBlocked:
                        screenColor = ScreenBlockedColor;
                        break;
                    case PlayerBuilder.BuildReadiness.SnappedReady:
                        screenColor = ScreenSnapReadyColor;
                        break;
                    case PlayerBuilder.BuildReadiness.Ready:
                        screenColor = ScreenReadyColor;
                        break;
                    default:
                        screenColor = ScreenOfflineColor;
                        break;
                }
            }
            else
            {
                _lastReadinessState = PlayerBuilder.BuildReadiness.Offline;
            }

            if (TryGetToolBrownoutFlicker(out float brownoutFlicker))
            {
                float alpha = screenColor.a;
                screenColor *= math.saturate(brownoutFlicker);
                screenColor.a = alpha;
            }

            _screenPropBlock.SetColor(PropScreenColor, screenColor);

            screenRenderer.SetPropertyBlock(_screenPropBlock, screenMaterialIndex);
        }

        private void QueueScreenRefresh()
        {
            _screenVisualDirty = true;
            TryRegisterLateFrameTick();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
            _lateFrameRegistered = false;
        }

        private void EnsureScreenPropertyBlock()
        {
            if (_screenPropBlock != null)
                return;

            // COLD ALLOC: MaterialPropertyBlock[1] — builder LCD state bridge — owner: BuilderTool
            _screenPropBlock = new MaterialPropertyBlock();
        }

        private float ResolveSwayLimitSinSq()
        {
            float limitAngle = math.max(0f, swayMaxAngle);
            if (limitAngle != _cachedSwayLimitAngle)
            {
                float halfLimit = math.radians(limitAngle) * 0.5f;
                float sinLimit = MathLodApproximation.ApproxSinBhaskara(halfLimit);
                _cachedSwayLimitSinSq = sinLimit * sinLimit;
                _cachedSwayLimitAngle = limitAngle;
            }

            return _cachedSwayLimitSinSq;
        }

        private static float ResolveDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            return math.saturate(x / (1f + x));
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (swaySpeed    < 0.1f) swaySpeed    = 0.1f;
            if (swayMaxAngle < 1f)   swayMaxAngle = 1f;
            if (swayMaxAngle > 45f)  swayMaxAngle = 45f;

            if (screenMaterialIndex < 0) screenMaterialIndex = 0;
        }
#endif
    }
}
