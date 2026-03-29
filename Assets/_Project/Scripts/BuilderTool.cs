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
//   • FindWithTag — только в OnSpawn (одноразово).
//
// LIFECYCLE:
//   ObjectPoolManager.Spawn() → OnSpawn() → [PlayerToolManager] → OnEquip()
//   → ToolTick()/UsePrimary()/UseSecondary() → OnUnequip() → OnDespawn()
// ============================================================================

namespace Hecton8.Gameplay
{
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
        /// Аллокации: FindWithTag (одна строковая проверка, допустимо в OnSpawn).
        /// </summary>
        public override void OnSpawn()
        {
            base.OnSpawn();

            _selfTransform = transform;
            _bound         = false;

            // ── Auto-Binding: найти Player root ──
            GameObject playerRoot = GameObject.FindWithTag("Player");

            if (playerRoot == null)
            {
                Debug.LogError(
                    "[BuilderTool] OnSpawn: No GameObject with tag 'Player' found! " +
                    "Builder tool will not function.");
                return;
            }

            // ── Извлечение компонентов с Player root ──
            // GetComponent на конкретном GameObject — zero GC (TryGetComponent).

            if (!playerRoot.TryGetComponent(out _playerBuilder))
            {
                Debug.LogError(
                    "[BuilderTool] OnSpawn: PlayerBuilder not found on Player root!");
                return;
            }

            if (!playerRoot.TryGetComponent(out _playerInventory))
            {
                Debug.LogWarning(
                    "[BuilderTool] OnSpawn: PlayerInventory not found on Player root. " +
                    "Resource display will be unavailable.");
                // Не критично — продолжаем без инвентаря
            }

            // ── Кэш Main Camera Transform ──
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraTransform = mainCam.transform;
            }
            else
            {
                Debug.LogWarning(
                    "[BuilderTool] OnSpawn: Camera.main is null! Sway effect disabled.");
            }

            // ── LCD Screen: инициализация MaterialPropertyBlock ──
            if (_screenPropBlock == null)
            {
                _screenPropBlock = new MaterialPropertyBlock();
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
            if (!ReferenceEquals(current, _lastDisplayedBuildable) || readiness != _lastReadinessState)
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
            if (!_bound || _playerBuilder == null)
                return "BUILDER // OFFLINE";

            return "BUILDER // " + _playerBuilder.GetActiveBuildOperationalSummary() +
                   " // " + _playerBuilder.GetActiveBuildStatusLabel();
        }

        public override string GetOperationalDirective()
        {
            if (!_bound || _playerBuilder == null)
                return "Restore builder link before field deployment.";

            return _playerBuilder.GetActiveBuildAdvice();
        }

        private void ApplySway(float dt)
        {
            if (_cameraTransform == null || _selfTransform == null) return;

            // ── Целевой поворот камеры ──
            quaternion cameraRot = _cameraTransform.rotation;

            // ── Frame-rate independent exponential slerp ──
            // t = 1 - exp(-speed * dt) обеспечивает одинаковую
            // визуальную скорость при 30, 60 и 144 fps.
            float t = 1f - math.exp(-swaySpeed * dt);

            _swayRotation = math.slerp(_swayRotation, cameraRot, t);

            // ── Ограничение максимального отклонения ──
            // Вычисляем угол между текущим sway и целью.
            // Если превышает лимит — подтягиваем sway ближе.
            quaternion delta = math.mul(math.inverse(cameraRot), _swayRotation);

            // Angle from quaternion: 2 * acos(|w|), в радианах
            float halfAngle = math.acos(math.clamp(math.abs(delta.value.w), 0f, 1f));
            float angleDeg  = math.degrees(halfAngle * 2f);

            if (angleDeg > swayMaxAngle)
            {
                // Пропорционально подтягиваем sway к камере
                float clampT = 1f - (swayMaxAngle / angleDeg);
                _swayRotation = math.slerp(_swayRotation, cameraRot, clampT);
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
            if (_screenPropBlock == null) return;

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

            _screenPropBlock.SetColor(PropScreenColor, screenColor);

            screenRenderer.SetPropertyBlock(_screenPropBlock, screenMaterialIndex);
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (swaySpeed    < 0.1f) swaySpeed    = 0.1f;
            if (swayMaxAngle < 1f)   swayMaxAngle = 1f;
            if (swayMaxAngle > 45f)  swayMaxAngle = 45f;

            if (screenMaterialIndex < 0) screenMaterialIndex = 0;
        }
#endif
    }
}
