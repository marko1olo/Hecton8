// ============================================================================
// HECTON-8 - CelestialTimeLapseDebugger.cs
// Developer-only orbital cycle accelerator with bounded physics stepping.
// ============================================================================

using Hecton8.Core;
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
        [SerializeField, Range(0.001f, 0.05f)] private float physicsFixedDeltaClampSeconds = 0.02f;
        [SerializeField, Range(0.01f, 0.25f)] private float maximumPhysicsDeltaSeconds = 0.05f;

#pragma warning disable CS0414
        [Header("Debug")]
        [SerializeField] private bool _debugApplied;
        [SerializeField] private float _debugActiveTimeScale;
        [SerializeField] private float _debugActiveFixedDelta;
        [SerializeField] private float _debugActiveMaximumDelta;
#pragma warning restore CS0414

        private bool _registered;
        private bool _capturedBaseline;
        private float _baselineTimeScale = 1f;
        private float _baselineFixedDeltaTime = 0.02f;
        private float _baselineMaximumDeltaTime = 0.3333333f;

        public bool IsApplied => _debugApplied;
        public float DebugTimeScale => debugTimeScale;
        public float PhysicsFixedDeltaClampSeconds => physicsFixedDeltaClampSeconds;

        private void OnEnable()
        {
            CaptureBaseline();
            TryRegister();
            ApplyResolvedTimeSettings();
        }

        private void OnDisable()
        {
            TryUnregister();
            RestoreBaseline();
        }

        private void OnDestroy()
        {
            TryUnregister();
            RestoreBaseline();
        }

        public void SlowTick()
        {
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

        private void CaptureBaseline()
        {
            if (_capturedBaseline)
                return;

            _baselineTimeScale = Time.timeScale;
            _baselineFixedDeltaTime = Time.fixedDeltaTime;
            _baselineMaximumDeltaTime = Time.maximumDeltaTime;
            _capturedBaseline = true;
        }

        private void ApplyResolvedTimeSettings()
        {
            CaptureBaseline();
            if (!enableTimeLapse)
            {
                RestoreBaseline();
                return;
            }

            float resolvedTimeScale = Mathf.Max(1f, debugTimeScale);
            float resolvedFixedDelta = Mathf.Min(
                Mathf.Max(0.001f, _baselineFixedDeltaTime * resolvedTimeScale),
                Mathf.Max(0.001f, physicsFixedDeltaClampSeconds));
            float resolvedMaximumDelta = Mathf.Min(
                Mathf.Max(0.01f, maximumPhysicsDeltaSeconds),
                Mathf.Max(0.01f, resolvedFixedDelta * 3f));

            Time.timeScale = resolvedTimeScale;
            Time.fixedDeltaTime = resolvedFixedDelta;
            Time.maximumDeltaTime = resolvedMaximumDelta;

            _debugApplied = true;
            _debugActiveTimeScale = Time.timeScale;
            _debugActiveFixedDelta = Time.fixedDeltaTime;
            _debugActiveMaximumDelta = Time.maximumDeltaTime;
        }

        private void RestoreBaseline()
        {
            if (!_capturedBaseline || !_debugApplied)
                return;

            Time.timeScale = Mathf.Max(0f, _baselineTimeScale);
            Time.fixedDeltaTime = Mathf.Max(0.001f, _baselineFixedDeltaTime);
            Time.maximumDeltaTime = Mathf.Max(0.01f, _baselineMaximumDeltaTime);

            _debugApplied = false;
            _debugActiveTimeScale = Time.timeScale;
            _debugActiveFixedDelta = Time.fixedDeltaTime;
            _debugActiveMaximumDelta = Time.maximumDeltaTime;
        }
    }
}
