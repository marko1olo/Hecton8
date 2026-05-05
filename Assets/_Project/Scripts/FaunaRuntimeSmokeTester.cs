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
        private const double PredatorPreyDistanceToleranceMeters = 0.0001d;
        private const double DistanceToleranceSqr = 0.05;

        [SerializeField] private bool runOnStart;
        [SerializeField] private bool _lastAupShiftMathPassed;
        [SerializeField] private bool _lastCorpseAttractorShiftMathPassed;
        [SerializeField] private float _lastMaxRuntimeDeltaError;
        [SerializeField] private double _lastMaxDistanceErrorSqr;
        [SerializeField] private float _lastCorpseRuntimeDeltaError;
        [SerializeField] private double _lastCorpseDistanceErrorSqr;
        [SerializeField] private bool _lastPredatorPreyAupShiftMathPassed;
        [SerializeField] private double _lastPredatorPreyDistanceErrorMeters;

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
            AbsoluteUniversePosition corpseAttractor = AbsoluteUniversePosition.FromAbsolutePosition(new double3(10512.5, -76.25, -10064.125));
            AbsoluteUniversePosition scavengerQuery = AbsoluteUniversePosition.FromAbsolutePosition(new double3(10528.75, -76.0, -10071.5));

            float3 previousOriginOffset = new float3(0f, 0f, 0f);
            float3 shiftOffset = new float3(10000f, 0f, -10000f);
            float3 committedOriginOffset = previousOriginOffset + shiftOffset;

            float3 runtimeA0 = AUPMath.ToRuntimeFloat3(in waypointA, previousOriginOffset);
            float3 runtimeB0 = AUPMath.ToRuntimeFloat3(in waypointB, previousOriginOffset);
            float3 runtimeC0 = AUPMath.ToRuntimeFloat3(in waypointC, previousOriginOffset);
            float3 corpseRuntime0 = AUPMath.ToRuntimeFloat3(in corpseAttractor, previousOriginOffset);
            float3 scavengerRuntime0 = AUPMath.ToRuntimeFloat3(in scavengerQuery, previousOriginOffset);
            float3 runtimeA1 = AUPMath.ToRuntimeFloat3(in waypointA, committedOriginOffset);
            float3 runtimeB1 = AUPMath.ToRuntimeFloat3(in waypointB, committedOriginOffset);
            float3 runtimeC1 = AUPMath.ToRuntimeFloat3(in waypointC, committedOriginOffset);
            float3 corpseRuntime1 = AUPMath.ToRuntimeFloat3(in corpseAttractor, committedOriginOffset);
            float3 scavengerRuntime1 = AUPMath.ToRuntimeFloat3(in scavengerQuery, committedOriginOffset);

            float3 expectedRuntimeDelta = -shiftOffset;
            float maxRuntimeDeltaError = 0f;
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeA1 - runtimeA0) - expectedRuntimeDelta));
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeB1 - runtimeB0) - expectedRuntimeDelta));
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, math.length((runtimeC1 - runtimeC0) - expectedRuntimeDelta));
            float corpseRuntimeDeltaError = math.max(
                math.length((corpseRuntime1 - corpseRuntime0) - expectedRuntimeDelta),
                math.length((scavengerRuntime1 - scavengerRuntime0) - expectedRuntimeDelta));
            maxRuntimeDeltaError = math.max(maxRuntimeDeltaError, corpseRuntimeDeltaError);

            double aupDistanceAB = AUPMath.AUPDistanceSq(in waypointA, in waypointB);
            double aupDistanceAC = AUPMath.AUPDistanceSq(in waypointA, in waypointC);
            double corpseAupDistanceSq = AUPMath.AUPDistanceSq(in scavengerQuery, in corpseAttractor);
            double runtimeDistanceAB0 = RuntimeDistanceSq(runtimeA0, runtimeB0);
            double runtimeDistanceAB1 = RuntimeDistanceSq(runtimeA1, runtimeB1);
            double runtimeDistanceAC0 = RuntimeDistanceSq(runtimeA0, runtimeC0);
            double runtimeDistanceAC1 = RuntimeDistanceSq(runtimeA1, runtimeC1);
            double corpseRuntimeDistance0 = RuntimeDistanceSq(scavengerRuntime0, corpseRuntime0);
            double corpseRuntimeDistance1 = RuntimeDistanceSq(scavengerRuntime1, corpseRuntime1);

            double maxDistanceErrorSqr = 0.0;
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAB - runtimeDistanceAB0));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAB - runtimeDistanceAB1));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAC - runtimeDistanceAC0));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, math.abs(aupDistanceAC - runtimeDistanceAC1));
            double corpseDistanceErrorSqr = math.max(
                math.abs(corpseAupDistanceSq - corpseRuntimeDistance0),
                math.abs(corpseAupDistanceSq - corpseRuntimeDistance1));
            maxDistanceErrorSqr = math.max(maxDistanceErrorSqr, corpseDistanceErrorSqr);
            bool predatorPreyPassed = RunHeadlessAupDriftAssertion(out double predatorPreyDistanceErrorMeters);

            _lastMaxRuntimeDeltaError = maxRuntimeDeltaError;
            _lastMaxDistanceErrorSqr = maxDistanceErrorSqr;
            _lastCorpseRuntimeDeltaError = corpseRuntimeDeltaError;
            _lastCorpseDistanceErrorSqr = corpseDistanceErrorSqr;
            _lastPredatorPreyAupShiftMathPassed = predatorPreyPassed;
            _lastPredatorPreyDistanceErrorMeters = predatorPreyDistanceErrorMeters;
            _lastCorpseAttractorShiftMathPassed = corpseRuntimeDeltaError <= RuntimeToleranceMeters &&
                                                  corpseDistanceErrorSqr <= DistanceToleranceSqr;
            _lastAupShiftMathPassed = maxRuntimeDeltaError <= RuntimeToleranceMeters &&
                                      maxDistanceErrorSqr <= DistanceToleranceSqr &&
                                      _lastCorpseAttractorShiftMathPassed &&
                                      _lastPredatorPreyAupShiftMathPassed;

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

        public static bool RunHeadlessAupDriftAssertion(out double distanceErrorMeters)
        {
            AbsoluteUniversePosition predatorAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(50000.0, -120.0, -50000.0));
            AbsoluteUniversePosition preyAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(50012.5, -116.75, -49993.5));
            EntityDataRecord predatorRecord = PersistentWorldRegistry.CreateFaunaHibernationState(
                0xA11CE001u,
                101,
                100f,
                in predatorAup,
                true,
                true,
                0f,
                0.5f);
            EntityDataRecord preyRecord = PersistentWorldRegistry.CreateFaunaHibernationState(
                0xA11CE002u,
                201,
                20f,
                in preyAup,
                false,
                false,
                0f,
                0.1f);

            float3 origin0 = float3.zero;
            float3 origin1 = new float3(50000f, 0f, -50000f);
            AbsoluteUniversePosition predatorRecordAup = AbsoluteUniversePosition.FromAlignedBlit(in predatorRecord.Position);
            AbsoluteUniversePosition preyRecordAup = AbsoluteUniversePosition.FromAlignedBlit(in preyRecord.Position);
            double aupDistance0 = math.sqrt(AUPMath.AUPDistanceSq(in predatorRecordAup, in preyRecordAup));
            float3 predatorRuntime0 = AUPMath.ToRuntimeFloat3(in predatorRecordAup, origin0);
            float3 preyRuntime0 = AUPMath.ToRuntimeFloat3(in preyRecordAup, origin0);
            float3 predatorRuntime1 = AUPMath.ToRuntimeFloat3(in predatorRecordAup, origin1);
            float3 preyRuntime1 = AUPMath.ToRuntimeFloat3(in preyRecordAup, origin1);
            double runtimeDistance0 = math.sqrt(RuntimeDistanceSq(predatorRuntime0, preyRuntime0));
            double runtimeDistance1 = math.sqrt(RuntimeDistanceSq(predatorRuntime1, preyRuntime1));
            distanceErrorMeters = math.max(math.abs(aupDistance0 - runtimeDistance0), math.abs(aupDistance0 - runtimeDistance1));
            distanceErrorMeters = math.max(distanceErrorMeters, math.abs(runtimeDistance0 - runtimeDistance1));
            return distanceErrorMeters <= PredatorPreyDistanceToleranceMeters;
        }
    }
}
