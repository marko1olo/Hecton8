// ============================================================================
// HECTON-8 — SoundscapeSystem.cs
// Система звуковых слоёв по глубине.
//
// ЛОР (лор1 — Звуковой дизайн, детальная карта):
//   ПОВЕРХНОСТЬ:    волны, ветер, гравитационный гул Аэгира
//   0-150м:         "пение" воды, рыбы, металлические стоны модулей
//   150-500м:       тишина нарастает, скрип скафандра
//   500-1000м:      только скафандр и дыхание, биолюм щелчки
//   1000-2000м:     механический скрип, постоянный гул давления
//   2000-4000м:     субзвук давления, вибрация контроллера
//   4000-5000м:     термальные потоки, трескотня минеральных башен
//
// АРХИТЕКТУРА:
//   • Публикует _SoundscapeDepthTier в шейдеры.
//   • Публикует события для AudioManager (смена эмбиента).
//   • ISlowTickable — обновление тира раз в 0.5с.
//   • Интегрируется с DepthZoneDirector.
// ============================================================================

using System;
using Conditional = System.Diagnostics.ConditionalAttribute;
using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.World
{
    public enum SoundscapeTier
    {
        Surface     = 0,   // Поверхность
        Shallow     = 1,   // 0-150м
        Twilight    = 2,   // 150-500м
        Darkness    = 3,   // 500-1000м
        Abyss       = 4,   // 1000-2000м
        DeepAbyss   = 5,   // 2000-4000м
        Thermal     = 6    // 4000-5000м
    }

    public static class SoundscapeEvents
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => OnTierChanged = null;

        /// <summary>Звуковой тир изменился. (oldTier, newTier)</summary>
        public static event Action<SoundscapeTier, SoundscapeTier> OnTierChanged;

        public static void RaiseTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
            => OnTierChanged?.Invoke(oldTier, newTier);
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-60)]
    public sealed class SoundscapeSystem : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Depth Thresholds (meters) ───────────────")]
        [SerializeField] private float shallowDepth   = 0f;
        [SerializeField] private float twilightDepth  = 150f;
        [SerializeField] private float darknessDepth  = 500f;
        [SerializeField] private float abyssDepth     = 1000f;
        [SerializeField] private float deepAbyssDepth = 2000f;
        [SerializeField] private float thermalDepth   = 4000f;
        [SerializeField] private float tierDepthHysteresis = 18f;

        [Header("── References ──────────────────────────────")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;

        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        public static SoundscapeSystem Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        private SoundscapeTier _currentTier = SoundscapeTier.Surface;
        private bool _registered;

        private static readonly int _ShaderSoundscapeTier =
            Shader.PropertyToID("_SoundscapeDepthTier");

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        public SoundscapeTier CurrentTier => _currentTier;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            TryRegister();

            ResolveSurvivalSystem();

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)_currentTier);
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void TryRegister()
        {
            if (_registered)
                return;


            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registered = false;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable
        // ══════════════════════════════════════════════════════════

        public void SlowTick()
        {
            if (survivalSystem == null && !ResolveSurvivalSystem())
                return;

            float depth = survivalSystem != null ? survivalSystem.Depth : 0f;
            SoundscapeTier newTier = CalculateTier(depth, _currentTier);

            if (newTier == _currentTier) return;

            SoundscapeTier oldTier = _currentTier;
            _currentTier = newTier;

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)newTier);
            SoundscapeEvents.RaiseTierChanged(oldTier, newTier);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[Soundscape] Tier: {oldTier} → {newTier} (depth: {depth:F0}m)");
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE
        // ══════════════════════════════════════════════════════════

        private SoundscapeTier CalculateTier(float depth, SoundscapeTier currentTier)
        {
            float hysteresis = Mathf.Max(0f, tierDepthHysteresis);

            switch (currentTier)
            {
                case SoundscapeTier.Surface:
                    return depth >= shallowDepth + hysteresis
                        ? SoundscapeTier.Shallow
                        : SoundscapeTier.Surface;

                case SoundscapeTier.Shallow:
                    if (depth < shallowDepth - hysteresis)
                        return SoundscapeTier.Surface;
                    if (depth >= twilightDepth + hysteresis)
                        return SoundscapeTier.Twilight;
                    return SoundscapeTier.Shallow;

                case SoundscapeTier.Twilight:
                    if (depth < twilightDepth - hysteresis)
                        return SoundscapeTier.Shallow;
                    if (depth >= darknessDepth + hysteresis)
                        return SoundscapeTier.Darkness;
                    return SoundscapeTier.Twilight;

                case SoundscapeTier.Darkness:
                    if (depth < darknessDepth - hysteresis)
                        return SoundscapeTier.Twilight;
                    if (depth >= abyssDepth + hysteresis)
                        return SoundscapeTier.Abyss;
                    return SoundscapeTier.Darkness;

                case SoundscapeTier.Abyss:
                    if (depth < abyssDepth - hysteresis)
                        return SoundscapeTier.Darkness;
                    if (depth >= deepAbyssDepth + hysteresis)
                        return SoundscapeTier.DeepAbyss;
                    return SoundscapeTier.Abyss;

                case SoundscapeTier.DeepAbyss:
                    if (depth < deepAbyssDepth - hysteresis)
                        return SoundscapeTier.Abyss;
                    if (depth >= thermalDepth + hysteresis)
                        return SoundscapeTier.Thermal;
                    return SoundscapeTier.DeepAbyss;

                case SoundscapeTier.Thermal:
                    return depth < thermalDepth - hysteresis
                        ? SoundscapeTier.DeepAbyss
                        : SoundscapeTier.Thermal;

                default:
                    if (depth < shallowDepth)
                        return SoundscapeTier.Surface;
                    if (depth < twilightDepth)
                        return SoundscapeTier.Shallow;
                    if (depth < darknessDepth)
                        return SoundscapeTier.Twilight;
                    if (depth < abyssDepth)
                        return SoundscapeTier.Darkness;
                    if (depth < deepAbyssDepth)
                        return SoundscapeTier.Abyss;
                    if (depth < thermalDepth)
                        return SoundscapeTier.DeepAbyss;
                    return SoundscapeTier.Thermal;
            }
        }

        private bool ResolveSurvivalSystem()
        {
            if (survivalSystem != null)
                return true;

            if (!BootstrapState.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            return playerTransform.TryGetComponent(out survivalSystem);
        }
    }
}
