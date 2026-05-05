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
    public sealed class CelestialTimeLapseDebugger : MonoBehaviour, ISlowTickable
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

        public bool IsApplied => _debugApplied;
        public float DebugTimeScale => debugTimeScale;
        public float PhysicsFixedDeltaClampSeconds => Time.fixedDeltaTime;

        private void OnEnable()
        {
            TryRegister();
            ApplyResolvedTimeSettings();
        }

        private void OnDisable()
        {
            TryUnregister();
            ApplyResolvedTimeSettings();
        }

        private void OnDestroy()
        {
            TryUnregister();
            ApplyResolvedTimeSettings();
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

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Core);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Core);
            _registered = false;
        }

        public void SlowTick()
        {
            ApplyResolvedTimeSettings();
        }

        private void ApplyResolvedTimeSettings()
        {
            if (!enableTimeLapse)
            {
                ApplyCelestialTimeScale(1f);
                _debugApplied = false;
                _debugCelestialTimeScale = 1f;
                return;
            }

            _debugApplied = true;
            _debugCelestialTimeScale = Mathf.Max(1f, debugTimeScale);
            ApplyCelestialTimeScale(_debugCelestialTimeScale);
        }

        private static void ApplyCelestialTimeScale(float scale)
        {
            HectonCelestialEngine celestialEngine = HectonCelestialEngine.ActiveRuntimeInstance;
            if (celestialEngine != null)
                celestialEngine.SetDebugCelestialTimeScale(Mathf.Max(1f, scale));
        }
    }
}
