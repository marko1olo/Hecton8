// ============================================================================
// HECTON-8 - CelestialTimeLapseDebugger.cs
// Developer-only orbital cycle accelerator. Physics time is not mutated.
// ============================================================================

using Hecton8.Core;
using Hecton8.Celestial;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Celestial Time-Lapse Debugger")]
    public sealed class CelestialTimeLapseDebugger : MonoBehaviour, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        [Header("Time-Lapse")]
        [SerializeField] private bool enableTimeLapse;
        [SerializeField, Min(1f)] private float debugTimeScale = 1000f;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugApplied;
        [SerializeField] private float _debugCelestialTimeScale;
#pragma warning restore CS0414

        private bool _registered;
        private bool _hotSwapRegistered;
        private HectonCelestialEngine _celestialEngine;

        public bool IsApplied => _debugApplied;
        public float DebugTimeScale => debugTimeScale;
        public float PhysicsFixedDeltaClampSeconds => Time.fixedDeltaTime;

        private void OnEnable()
        {
            CacheCelestialEngineCold();
            TryRegisterHotSwapListener();
            TryRegister();
            ApplyResolvedTimeSettings();
        }

        private void OnDisable()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearResolvedTimeSettings();
            _celestialEngine = null;
        }

        private void OnDestroy()
        {
            TryUnregister();
            TryUnregisterHotSwapListener();
            ClearResolvedTimeSettings();
            _celestialEngine = null;
        }

        [ContextMenu("Enable 1000x Celestial Time-Lapse")]
        public void EnableTimeLapse()
        {
            enableTimeLapse = true;
            ApplyResolvedTimeSettings();
        }

        [ContextMenu("Disable Celestial Time-Lapse")]
        public void DisableTimeLapse()
        {
            enableTimeLapse = false;
            ApplyResolvedTimeSettings();
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Core);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.CelestialEngineRuntime || !isActiveAndEnabled)
                return;

            HectonCelestialEngine previousEngine = previousService as HectonCelestialEngine;
            if (previousEngine != null && !ReferenceEquals(previousEngine, currentService))
                previousEngine.SetDebugCelestialTimeScale(1f);

            _celestialEngine = currentService as HectonCelestialEngine;
            ApplyResolvedTimeSettings();
        }

        public void SlowTick()
        {
            ApplyResolvedTimeSettings();
        }

        private void ApplyResolvedTimeSettings()
        {
            if (!enableTimeLapse)
            {
                ClearResolvedTimeSettings();
                return;
            }

            _debugApplied = true;
            _debugCelestialTimeScale = Mathf.Max(1f, debugTimeScale);
            ApplyCelestialTimeScale(_debugCelestialTimeScale);
        }

        private void ClearResolvedTimeSettings()
        {
            ApplyCelestialTimeScale(1f);
            _debugApplied = false;
            _debugCelestialTimeScale = 1f;
        }

        private void ApplyCelestialTimeScale(float scale)
        {
            HectonCelestialEngine celestialEngine = _celestialEngine;
            if (celestialEngine != null)
                celestialEngine.SetDebugCelestialTimeScale(Mathf.Max(1f, scale));
        }

        private void CacheCelestialEngineCold()
        {
            _celestialEngine = GlobalRegistry.CelestialEngine;
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
    }
}
