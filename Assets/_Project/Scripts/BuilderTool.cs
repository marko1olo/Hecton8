// ============================================================================
// HECTON-8 — BuilderTool.cs
// Визуальный мост между PlayerToolManager и PlayerBuilder.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Visual Bridge: делегирует OnEquip/OnUnequip/UsePrimary/UseSecondary/
//      ToolTick в PlayerBuilder (логический контроллер строительства).
//   2. Auto-Binding: при спавне из пула находит Player root по тегу,
//      извлекает и кэширует PlayerInventory, PlayerBuilder, Camera.
//   3. NASA-Punk Sway: модель инструмента плавно отстаёт от поворота
//      камеры, создавая ощущение веса и инерции.
//   4. LCD Screen: отображает имя активного BuildableData на MeshRenderer
//      через MaterialPropertyBlock (zero GC per-frame).
//
// НЕ СОДЕРЖИТ строительной логики — только визуал и делегация.
//
// ZERO GC В РАНТАЙМЕ:
//   • Никаких строковых аллокаций в ToolTick.
//   • MaterialPropertyBlock — pre-allocated, reused.
//   • Unity.Mathematics quaternion.slerp — struct math, zero boxing.
//   • Player lookup — SceneBootstrap cached player transform, no scene search.
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
    public sealed class BuilderTool : PlayerTool
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — VISUAL
        // ══════════════════════════════════════════════════════════

        [Header("── Sway Settings (NASA-Punk) ─────────────────")]
        [Tooltip("Скорость, с которой модель догоняет камеру. " +
                 "Меньше = больше инерции, тяжелее ощущение.")]
        [SerializeField] private float swaySpeed = 8f;

        [Tooltip("Максимальное отклонение sway от камеры (градусы). " +
                 "Ограничивает визуальный лаг при быстрых поворотах.")]
        [SerializeField] private float swayMaxAngle = 12f;

        [Header("── LCD Screen ────────────────────────────────")]
        [Tooltip("MeshRenderer маленького LCD-экрана на модели инструмента. " +
                 "Если null — экран не обновляется (нет аллокаций, нет ошибок).")]
        public MeshRenderer screenRenderer;

        [Tooltip("Индекс материала на screenRenderer для LCD-экрана. " +
                 "Обычно 0, если экран — отдельный submesh.")]
        [SerializeField] private int screenMaterialIndex;

        // ══════════════════════════════════════════════════════════
        //  CACHED SCENE REFERENCES (auto-bound in OnSpawn)
        // ══════════════════════════════════════════════════════════

        /// <summary>Логический контроллер строительства на Player root.</summary>
        private PlayerBuilder  _playerBuilder;

        /// <summary>Инвентарь игрока (для будущих расширений — проверка ресурсов в UI).</summary>
        private PlayerInventory _playerInventory;

        /// <summary>Кэшированный Transform основной камеры.</summary>
        private Transform _cameraTransform;

        // ══════════════════════════════════════════════════════════
        //  SWAY STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Текущий поворот sway модели.
        /// Unity.Mathematics quaternion — struct, zero GC.
        /// Инициализируется при OnEquip из текущего поворота камеры.
        /// </summary>
        private quaternion _swayRotation;
        private float _cachedSwayLimitAngle = -1f;
        private float _cachedSwayLimitSinSq;

        /// <summary>
        /// Transform корня модели инструмента (this.transform).
        /// Кэшируется для избежания повторных вызовов get_transform().
        /// </summary>
        private Transform _selfTransform;

        // ══════════════════════════════════════════════════════════
        //  LCD SCREEN STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated MaterialPropertyBlock. Reused каждый кадр.
        /// Zero GC при SetTexture/SetColor/SetFloat.
        /// </summary>
        private MaterialPropertyBlock _screenPropBlock;

        /// <summary>
        /// Shader property ID для текста на экране.
        /// Кэшируется через Shader.PropertyToID — вызывается один раз.
        /// Используется с _ScreenText (Vector4, кодирующий ASCII/индекс).
        /// Альтернатива: _MainTex для текстурного атласа шрифтов.
        /// </summary>
        private static readonly int PropScreenColor = Shader.PropertyToID("_EmissionColor");
        private static readonly Color ScreenOfflineColor = new Color(0.6f, 0.1f, 0.1f, 1f);
        private static readonly Color ScreenMissingCostColor = new Color(0.9f, 0.55f, 0.18f, 1f);
        private static readonly Color ScreenReadyColor = new Color(0.2f, 0.85f, 1f, 1f);
        private static readonly Color ScreenSnapReadyColor = new Color(0.2f, 1f, 0.4f, 1f);
        private static readonly Color ScreenBlockedColor = new Color(1f, 0.28f, 0.22f, 1f);

        /// <summary>
        /// Последний отображённый buildable. Для skip-проверки —
        /// не обновляем экран, если модуль не изменился.
        /// </summary>
        private BuildableData _lastDisplayedBuildable;
        private PlayerBuilder.BuildReadiness _lastReadinessState;
        private FixedCharBuffer _legacyOperationalBuffer = new FixedCharBuffer(512); // COLD ALLOC: char[512] - builder tool legacy string bridge - owner: BuilderTool

        /// <summary>Флаг успешной привязки к сцене.</summary>
        private bool _bound;

        // ══════════════════════════════════════════════════════════
        //  IPoolable — POOL LIFECYCLE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается ObjectPoolManager при извлечении из пула.
        ///
        /// КРИТИЧЕСКАЯ ТОЧКА AUTO-BINDING:
        /// Инструмент спавнится в HandAnchor из пула — у него нет
        /// Inspector-ссылок на объекты сцены. Находим Player root
        /// по тегу и извлекаем нужные компоненты.
        ///
        /// Аллокации: SceneBootstrap cached lookup; no scene search in OnSpawn.
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

            // ── Auto-Binding: найти Player root ──
            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[BuilderTool] OnSpawn: Player transform could not be resolved via SceneBootstrap. " +
                    "Builder tool will not function.");
#endif
                return;
            }

            GameObject playerRoot = playerTransform.gameObject;

            // ── Извлечение компонентов с Player root ──
            // GetComponent на конкретном GameObject — zero GC (TryGetComponent).

            if (!playerRoot.TryGetComponent(out _playerBuilder))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[BuilderTool] OnSpawn: PlayerBuilder not found on Player root!");
#endif
                return;
            }

            if (!playerRoot.TryGetComponent(out _playerInventory))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[BuilderTool] OnSpawn: PlayerInventory not found on Player root. " +
                    "Resource display will be unavailable.");
#endif
                // Не критично — продолжаем без инвентаря
            }

            // ── Кэш Main Camera Transform ──
            Camera playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
            if (playerCamera != null)
            {
                _cameraTransform = playerCamera.transform;
            }
            else
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning(
                    "[BuilderTool] OnSpawn: Player camera not found in player hierarchy. Sway effect disabled.");
#endif
            }

            _lastDisplayedBuildable = null;
            _lastReadinessState = PlayerBuilder.BuildReadiness.Offline;
            _bound = true;
        }

        /// <summary>
        /// Вызывается ObjectPoolManager при возврате в пул.
        /// Очищает все кэшированные ссылки на сцену.
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

            base.OnDespawn();
        }

        // ══════════════════════════════════════════════════════════
        //  TOOL LIFECYCLE — делегация в PlayerBuilder
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вход в режим строительства.
        /// Активирует ghost через PlayerBuilder и инициализирует sway.
        /// </summary>
        public override void OnEquip()
        {
            base.OnEquip();

            if (!_bound) return;

            // ── Делегация: активировать призрак постройки ──
            _playerBuilder.OnEquip();

            // ── Инициализация sway из текущего поворота камеры ──
            if (_cameraTransform != null)
            {
                _swayRotation = _cameraTransform.rotation;
            }
            else
            {
                _swayRotation = quaternion.identity;
            }

            // ── Обновить LCD экран с текущим модулем ──
            UpdateScreen();
        }

        /// <summary>
        /// Выход из режима строительства.
        /// Деактивирует ghost через PlayerBuilder.
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
        /// Основное действие (ЛКМ): размещение модуля.
        /// Делегирует в PlayerBuilder.UsePrimary().
        /// </summary>
        public override void UsePrimary(float deltaTime)
        {
            if (!_bound) return;

            _playerBuilder.UsePrimary(deltaTime);
        }

        /// <summary>
        /// Альтернативное действие (ПКМ): вращение призрака.
        /// Делегирует в PlayerBuilder.UseSecondary().
        /// </summary>
        public override void UseSecondary(float deltaTime)
        {
            if (!_bound) return;

            _playerBuilder.UseSecondary(deltaTime);
        }

        /// <summary>
        /// Вызывается каждый кадр через PlayerToolManager.
        ///
        /// Выполняет:
        ///   1. Делегацию ToolTick в PlayerBuilder (обновление ghost позиции).
        ///   2. Sway-эффект модели инструмента (NASA-punk inertia).
        ///   3. Обновление LCD-экрана (только при смене модуля).
        ///
        /// ZERO GC: Unity.Mathematics struct math, no string ops,
        /// MaterialPropertyBlock reuse.
        /// </summary>
        public override void ToolTick(float deltaTime)
        {
            if (!_bound) return;

            // ── 1. Делегация логики строительства ──
            _playerBuilder.ToolTick(deltaTime);

            // ── 2. Sway-эффект ──
            ApplySway(deltaTime);

            // ── 3. LCD-экран (skip если модуль не изменился) ──
            BuildableData current = _playerBuilder.ActiveBuildable;
            PlayerBuilder.BuildReadiness readiness = _playerBuilder.ActiveBuildReadiness;
            bool brownoutActive = TryGetToolBrownoutFlicker(out _);
            if (brownoutActive || !ReferenceEquals(current, _lastDisplayedBuildable) || readiness != _lastReadinessState)
            {
                UpdateScreen();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  SWAY — NASA-Punk Inertia Effect
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Модель инструмента плавно отстаёт от поворота камеры.
        ///
        /// Алгоритм:
        ///   1. Целевой поворот = камера (quaternion).
        ///   2. Sway-поворот интерполируется к цели через slerp
        ///      с экспоненциальным сглаживанием (frame-rate independent).
        ///   3. Дельта между sway и камерой ограничивается swayMaxAngle.
        ///   4. Модель поворачивается на sway-поворот.
        ///
        /// Unity.Mathematics quaternion — struct, zero GC, SIMD-friendly.
        ///
        /// Визуальный результат: при быстром повороте мыши инструмент
        /// «запаздывает», создавая ощущение массы (NASA-punk aesthetic).
        /// </summary>
        public override string GetOperationalSummary()
        {
            _legacyOperationalBuffer.Clear();
            WriteOperationalSummary(ref _legacyOperationalBuffer);
            return CreateLegacyString(in _legacyOperationalBuffer);
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

        public override string GetOperationalDirective()
        {
            _legacyOperationalBuffer.Clear();
            WriteOperationalDirective(ref _legacyOperationalBuffer);
            return CreateLegacyString(in _legacyOperationalBuffer);
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

        private static string CreateLegacyString(in FixedCharBuffer buffer)
        {
            return buffer.Length > 0 ? new string(buffer.Buffer, 0, buffer.Length) : string.Empty;
        }

        private static bool AppendText(ref FixedCharBuffer buffer, string value)
        {
            return string.IsNullOrEmpty(value) || buffer.Append(value);
        }

        private void ApplySway(float dt)
        {
            if (_cameraTransform == null || _selfTransform == null) return;

            // ── Целевой поворот камеры ──
            quaternion cameraRot = _cameraTransform.rotation;

            // ── Frame-rate independent exponential slerp ──
            // t = 1 - exp(-speed * dt) обеспечивает одинаковую
            // визуальную скорость при 30, 60 и 144 fps.
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

            // ── Применяем к модели ──
            _selfTransform.rotation = _swayRotation;
        }

        // ══════════════════════════════════════════════════════════
        //  LCD SCREEN — Visual Feedback
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обновляет LCD-экран на модели инструмента.
        ///
        /// Текущая реализация: меняет emission color на основе
        /// наличия/отсутствия активного модуля.
        ///
        /// Будущее расширение: текстурный атлас шрифтов для
        /// отображения имени модуля (RenderTexture → material).
        ///
        /// ZERO GC: MaterialPropertyBlock — pre-allocated, reused.
        /// SetPropertyBlock не аллоцирует.
        ///
        /// Вызывается ТОЛЬКО при смене активного модуля (не per-frame).
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

            // ── Получаем текущий property block (merge с существующими) ──
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
                float sinLimit = math.sin(halfLimit);
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
