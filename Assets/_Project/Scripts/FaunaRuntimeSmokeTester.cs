using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Dev
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Dev/Fauna Runtime Smoke Tester")]
    public sealed class FaunaRuntimeSmokeTester : MonoBehaviour
    {
        private const float RuntimeToleranceMeters = 0.01f;
        private const double DistanceToleranceSqr = 0.05;

        [SerializeField] private bool runOnStart;
        [SerializeField] private bool _lastAupShiftMathPassed;
        [SerializeField] private float _lastMaxRuntimeDeltaError;
        [SerializeField] private double _lastMaxDistanceErrorSqr;

        private void Start()
        {
            if (runOnStart)
                RunAupShiftSmokeTest();
        }

        [ContextMenu("Run AUP Shift Smoke Test")]
        public void RunAupShiftSmokeTest()
        {
            AbsoluteUniversePosition waypointA = AbsoluteUniversePosition.FromAbsolutePosition(new double3(10025.125, -52.5, -9988.75));
            AbsoluteUniversePosition waypointB = AbsoluteUniversePosition.FromAbsolutePosition(new double3(10031.375, -52.25, -9991.5));
            AbsoluteUniversePosition waypointC = AbsoluteUniversePosition.FromAbsolutePosition(new double3(14998.875, 8.125, -15002.375));

            float3 previousOriginOffset = new float3(0f, 0f, 0f);
            float3 shiftOffset = new float3(10000f, 0f, -10000f);
            float3 committedOriginOffset = previousOriginOffset + shiftOffset;

            float3 runtimeA0 = AUPMath.ToRuntimeFloat3(in waypointA, previousOriginOffset);
            float3 runtimeB0 = AUPMath.ToRuntimeFloat3(in waypointB, previousOriginOffset);
            float3 runtimeC0 = AUPMath.ToRuntimeFloat3(in waypointC, previousOriginOffset);
            float3 runtimeA1 = AUPMath.ToRuntimeFloat3(in waypointA, committedOriginOffset);
            float3 runtimeB1 = AUPMath.ToRuntimeFloat3(in waypointB, committedOriginOffset);
            float3 runtimeC1 = AUPMath.ToRuntimeFloat3(in waypointC, committedOriginOffset);

            float3 expectedRuntimeDelta = -shiftOffset;
            float maxRuntimeDeltaError = 0f;
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeA1 - runtimeA0) - expectedRuntimeDelta));
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeB1 - runtimeB0) - expectedRuntimeDelta));
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeC1 - runtimeC0) - expectedRuntimeDelta));

            double aupDistanceAB = AUPMath.AUPDistanceSq(in waypointA, in waypointB);
            double aupDistanceAC = AUPMath.AUPDistanceSq(in waypointA, in waypointC);
            double runtimeDistanceAB0 = RuntimeDistanceSq(runtimeA0, runtimeB0);
            double runtimeDistanceAB1 = RuntimeDistanceSq(runtimeA1, runtimeB1);
            double runtimeDistanceAC0 = RuntimeDistanceSq(runtimeA0, runtimeC0);
            double runtimeDistanceAC1 = RuntimeDistanceSq(runtimeA1, runtimeC1);

            double maxDistanceErrorSqr = 0.0;
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAB - runtimeDistanceAB0));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAB - runtimeDistanceAB1));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAC - runtimeDistanceAC0));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAC - runtimeDistanceAC1));

            _lastMaxRuntimeDeltaError = maxRuntimeDeltaError;
            _lastMaxDistanceErrorSqr = maxDistanceErrorSqr;
            _lastAupShiftMathPassed = maxRuntimeDeltaError <= RuntimeToleranceMeters && maxDistanceErrorSqr <= DistanceToleranceSqr;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_lastAupShiftMathPassed)
                Debug.Log("FaunaRuntimeSmokeTester AUP shift math passed.");
            else
                Debug.LogError("FaunaRuntimeSmokeTester AUP shift math failed.");
#endif
        }

        private static double RuntimeDistanceSq(float3 a, float3 b)
        {
            double dx = (double)a.x - b.x;
            double dy = (double)a.y - b.y;
            double dz = (double)a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }
    }
}
