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
using Unity.Collections;
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

    /// <summary>
    /// Listener contract for queue-backed soundscape tier notifications.
    /// </summary>
    public interface ISoundscapeEventListener
    {
        /// <summary>Called when the active soundscape tier changes.</summary>
        /// <param name="oldTier">Previous tier.</param>
        /// <param name="newTier">New tier.</param>
        void OnSoundscapeTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier);
    }

    public static class SoundscapeEvents
    {
        private const int PendingEventCapacity = 16;

        private struct SoundscapeEventPayload
        {
            public SoundscapeTier OldTier;
            public SoundscapeTier NewTier;
        }

        private static readonly RegistryBucket<ISoundscapeEventListener> _listeners = new RegistryBucket<ISoundscapeEventListener>(16);
        private static NativeQueue<SoundscapeEventPayload> _pendingEvents;
        private static int _pendingEventCount;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEventCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SoundscapeEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _pendingEventCount = 0;
            _listeners.Clear();
        }

        /// <summary>Звуковой тир изменился. (oldTier, newTier)</summary>
        public static void Register(ISoundscapeEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        public static void Unregister(ISoundscapeEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        public static void RaiseTierChanged(SoundscapeTier oldTier, SoundscapeTier newTier)
        {
            if (_listeners.Count <= 0 || _pendingEventCount >= PendingEventCapacity)
                return;

            EnsureInitialized();
            _pendingEvents.Enqueue(new SoundscapeEventPayload
            {
                OldTier = oldTier,
                NewTier = newTier
            });
            _pendingEventCount++;
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out SoundscapeEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ISoundscapeEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnSoundscapeTierChanged(payload.OldTier, payload.NewTier);
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<SoundscapeEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SoundscapeEventPayload>[16] - soundscape tier event lane flushed by SystemDispatcher - owner: SoundscapeEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(SoundscapeEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }
        }
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
        private bool _serviceRegistered;

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
            TryRegisterService();
            TryRegister();

            ResolveSurvivalSystem();

            Shader.SetGlobalInt(_ShaderSoundscapeTier, (int)_currentTier);
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterService();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
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

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterSoundscapeRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Soundscape, this);
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterSoundscapeRuntime(this);
            _serviceRegistered = false;
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

            LogTierChanged();
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

        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        private static void LogTierChanged()
        {
            Debug.Log("[Soundscape] Tier changed.");
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
