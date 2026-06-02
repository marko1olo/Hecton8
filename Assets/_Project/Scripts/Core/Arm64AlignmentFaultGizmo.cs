using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core.Memory
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class Arm64AlignmentFaultGizmo : MonoBehaviour, IGlobalRegistryHotSwapListener
    {
        [SerializeField] private Vector3 boxSize = new Vector3(1.5f, 1.5f, 1.5f);
        [SerializeField] private float pulseSpeed = 5f;
        [SerializeField] private float maxSceneOffsetMeters = 100000f;

        private static readonly double StopwatchTicksToSeconds = 1d / System.Diagnostics.Stopwatch.Frequency;

        private IDataVault _vault;
        private bool _hotSwapRegistered;

        private void OnEnable()
        {
            BindVaultCold(GlobalRegistry.DataVault);
            TryRegisterHotSwapListener();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            BindVaultCold(null);
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            BindVaultCold(null);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
                BindVaultCold(currentService as IDataVault);
        }

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            IDataVault vault = _vault;
            if (vault == null)
                return;

            if (!Arm64AlignmentTelemetry.TryGetNewestFault(vault, out AlignmentTelemetryEntry entry))
                return;

            if ((entry.Flags & (AlignmentTelemetryFlags.MisalignedEightByteField | AlignmentTelemetryFlags.InvalidStride | AlignmentTelemetryFlags.DynamicCastFault)) == 0u)
                return;

            double3 p = entry.AupOrRuntimePosition;
            if (!math.all(math.isfinite(p)))
                return;

            float pulseTime = (float)(System.Diagnostics.Stopwatch.GetTimestamp() * StopwatchTicksToSeconds);
            float pulse = 0.55f + (0.45f * math.abs(MathLodApproximation.ApproxSinBhaskara(pulseTime * math.max(0.01f, pulseSpeed))));
            Gizmos.color = new Color(1f, 0.05f, 0.02f, pulse);
            Gizmos.DrawWireCube(
                ToRuntimePosition(p, HectonFloatingOrigin.CurrentTotalOffsetDouble, math.max(1f, maxSceneOffsetMeters)),
                boxSize);
#endif
        }

        private void BindVaultCold(IDataVault vault)
        {
            _vault = vault;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.IsHotSwapListenerRegistered(this) ||
                                 GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static Vector3 ToRuntimePosition(double3 aup, double3 originAup, float clampMeters)
        {
            double limit = math.max(1.0, clampMeters);
            double3 local = math.clamp(aup - originAup, new double3(-limit), new double3(limit));
            return new Vector3((float)local.x, (float)local.y, (float)local.z);
        }
    }
}
