using Hecton8.AI;
using Hecton8.Core;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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
        private const int AupStressPairCount = 128;
        private const int ParasiteSmokeCount = 4;
        private const string NativeMemoryOwner = nameof(FaunaRuntimeSmokeTester);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.TempJob;

        [SerializeField] private bool runOnStart;
        [SerializeField] private bool _lastAupShiftMathPassed;
        [SerializeField] private bool _lastCorpseAttractorShiftMathPassed;
        [SerializeField] private float _lastMaxRuntimeDeltaError;
        [SerializeField] private double _lastMaxDistanceErrorSqr;
        [SerializeField] private float _lastCorpseRuntimeDeltaError;
        [SerializeField] private double _lastCorpseDistanceErrorSqr;
        [SerializeField] private bool _lastPredatorPreyAupShiftMathPassed;
        [SerializeField] private double _lastPredatorPreyDistanceErrorMeters;

        public struct OmegaSmokeResult
        {
            public byte Passed;
            public byte AupDriftPassed;
            public byte AupStressPassed;
            public byte ParasiteAttachPassed;
            public byte EggPersistencePassed;
            public byte NativeSentinelBalanced;
            public double AupDriftDistanceErrorMeters;
            public double AupStressMaxDistanceErrorMeters;
            public float ParasiteHostHealth;
            public float ParasiteHunger01;
            public double ParasiteMaxDistanceErrorMeters;
            public float EggHatchTimeSeconds;
            public int NativeSentinelDelta;
        }

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
                Hecton8.Core.H8Debug.Log("FaunaRuntimeSmokeTester AUP shift math passed.");
            else
                Hecton8.Core.H8Debug.LogError("FaunaRuntimeSmokeTester AUP shift math failed.");
#endif
        }

        private static double RuntimeDistanceSq(float3 a, float3 b)
        {
            double dx = (double)a.x - b.x;
            double dy = (double)a.y - b.y;
            double dz = (double)a.z - b.z;
            return dx * dx + dy * dy + dz * dz;
        }

        public static bool RunOmegaHeadlessSmoke(out OmegaSmokeResult result)
        {
            result = default;
            int sentinelBefore = NativeMemorySentinel.ActiveAllocationCount;
            bool aupPassed = RunHeadlessAupDriftAssertion(out double distanceErrorMeters);
            bool stressPassed = RunAupDriftStressJob(out double stressMaxDistanceErrorMeters);
            bool parasitePassed = RunParasiteAttachSmoke(
                out float parasiteHostHealth,
                out float parasiteHunger01,
                out double parasiteMaxDistanceErrorMeters);
            bool eggPassed = RunEggPersistenceSmoke(out float eggHatchTimeSeconds);
            int sentinelDelta = NativeMemorySentinel.ActiveAllocationCount - sentinelBefore;

            result.AupDriftPassed = aupPassed ? (byte)1 : (byte)0;
            result.AupStressPassed = stressPassed ? (byte)1 : (byte)0;
            result.ParasiteAttachPassed = parasitePassed ? (byte)1 : (byte)0;
            result.EggPersistencePassed = eggPassed ? (byte)1 : (byte)0;
            result.NativeSentinelBalanced = sentinelDelta == 0 ? (byte)1 : (byte)0;
            result.AupDriftDistanceErrorMeters = distanceErrorMeters;
            result.AupStressMaxDistanceErrorMeters = stressMaxDistanceErrorMeters;
            result.ParasiteHostHealth = parasiteHostHealth;
            result.ParasiteHunger01 = parasiteHunger01;
            result.ParasiteMaxDistanceErrorMeters = parasiteMaxDistanceErrorMeters;
            result.EggHatchTimeSeconds = eggHatchTimeSeconds;
            result.NativeSentinelDelta = sentinelDelta;
            result.Passed = aupPassed &&
                            stressPassed &&
                            parasitePassed &&
                            eggPassed &&
                            sentinelDelta == 0
                ? (byte)1
                : (byte)0;
            return result.Passed != 0;
        }

        private static bool RunAupDriftStressJob(out double maxDistanceErrorMeters)
        {
            maxDistanceErrorMeters = double.PositiveInfinity;
            NativeArray<AbsoluteUniversePositionBlit128> predatorAups = default;
            NativeArray<AbsoluteUniversePositionBlit128> preyAups = default;
            NativeArray<double> distanceErrors = default;

            try
            {
                predatorAups = AllocateTrackedNativeArray<AbsoluteUniversePositionBlit128>(AupStressPairCount, nameof(predatorAups), NativeArrayOptions.UninitializedMemory);
                preyAups = AllocateTrackedNativeArray<AbsoluteUniversePositionBlit128>(AupStressPairCount, nameof(preyAups), NativeArrayOptions.UninitializedMemory);
                distanceErrors = AllocateTrackedNativeArray<double>(AupStressPairCount, nameof(distanceErrors), NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < AupStressPairCount; i++)
                {
                    double3 predatorPosition = new double3(
                        50000.0 + (i * 8.125),
                        -120.0 - ((i & 7) * 0.25),
                        -50000.0 + (i * 4.5));
                    double3 preyPosition = predatorPosition + new double3(12.5, 3.25, 6.5);
                    AbsoluteUniversePosition predatorAup = AbsoluteUniversePosition.FromAbsolutePosition(predatorPosition);
                    AbsoluteUniversePosition preyAup = AbsoluteUniversePosition.FromAbsolutePosition(preyPosition);
                    predatorAups[i] = predatorAup.ToAlignedBlit();
                    preyAups[i] = preyAup.ToAlignedBlit();
                }

                JobHandle handle = new AupDriftStressJob
                {
                    PredatorAups = predatorAups,
                    PreyAups = preyAups,
                    DistanceErrors = distanceErrors,
                    OriginBefore = float3.zero,
                    OriginAfter = new float3(50000f, 0f, -50000f)
                }.Schedule(AupStressPairCount, 32);

                if (!DispatcherJobSwap.TryComplete(ref handle, forceComplete: true))
                    return false;

                double maxError = 0.0;
                for (int i = 0; i < AupStressPairCount; i++)
                    maxError = math.max(maxError, distanceErrors[i]);

                maxDistanceErrorMeters = maxError;
                return maxError <= PredatorPreyDistanceToleranceMeters;
            }
            finally
            {
                DisposeTrackedNativeArray(ref predatorAups);
                DisposeTrackedNativeArray(ref preyAups);
                DisposeTrackedNativeArray(ref distanceErrors);
            }
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(int length, string label, NativeArrayOptions options)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
                if (sentinelId > 0)
                    return array;
            }
            catch
            {
                if (array.IsCreated)
                    array.Dispose();

                throw;
            }

            array.Dispose();
            throw new System.InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static bool RunParasiteAttachSmoke(
            out float lastHostHealth,
            out float lastParasiteHunger01,
            out double maxDistanceErrorMeters)
        {
            lastHostHealth = 0f;
            lastParasiteHunger01 = 0f;
            maxDistanceErrorMeters = double.PositiveInfinity;
            NativeArray<FaunaParasiteAttachInput> inputs = default;
            NativeArray<FaunaParasiteAttachResult> results = default;

            try
            {
                inputs = AllocateTrackedNativeArray<FaunaParasiteAttachInput>(ParasiteSmokeCount, nameof(inputs), NativeArrayOptions.UninitializedMemory);
                results = AllocateTrackedNativeArray<FaunaParasiteAttachResult>(ParasiteSmokeCount, nameof(results), NativeArrayOptions.UninitializedMemory);

                for (int i = 0; i < ParasiteSmokeCount; i++)
                {
                    AbsoluteUniversePosition hostAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(
                        12345.25 + (i * 3.5),
                        -210.5,
                        -54321.75 - (i * 2.25)));
                    inputs[i] = new FaunaParasiteAttachInput
                    {
                        HostAup = hostAup.ToAlignedBlit(),
                        HostLocalAttachOffset = new float3(1.25f + i, -0.5f, 2.75f),
                        HostHealth = 0.8f,
                        ParasiteHunger01 = 0.25f,
                        DrainPerSecond = 0.1f,
                        DeltaTimeSeconds = 2f,
                        Attached = 1
                    };
                }

                FaunaSimulationEngine simulationEngine = new FaunaSimulationEngine();
                JobHandle handle = simulationEngine.ScheduleParasiteAttach(inputs, results, ParasiteSmokeCount);
                if (!DispatcherJobSwap.TryComplete(ref handle, forceComplete: true))
                    return false;

                double maxError = 0.0;
                bool passed = true;
                for (int i = 0; i < ParasiteSmokeCount; i++)
                {
                    FaunaParasiteAttachInput input = inputs[i];
                    FaunaParasiteAttachResult result = results[i];
                    double3 expected = ToAbsolute(in input.HostAup) + (double3)input.HostLocalAttachOffset;
                    double3 actual = ToAbsolute(in result.ParasiteAup);
                    double distanceError = math.sqrt(math.lengthsq(expected - actual));
                    maxError = math.max(maxError, distanceError);
                    passed &= math.abs(result.HostHealth - 0.6f) <= 0.0001f;
                    passed &= math.abs(result.ParasiteHunger01 - 0.05f) <= 0.0001f;
                    passed &= distanceError <= PredatorPreyDistanceToleranceMeters;
                    lastHostHealth = result.HostHealth;
                    lastParasiteHunger01 = result.ParasiteHunger01;
                }

                maxDistanceErrorMeters = maxError;
                return passed;
            }
            finally
            {
                DisposeTrackedNativeArray(ref inputs);
                DisposeTrackedNativeArray(ref results);
            }
        }

        private static bool RunEggPersistenceSmoke(out float hatchTimeSeconds)
        {
            AbsoluteUniversePosition eggAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(4096.25, -88.5, -8192.75));
            EntityDataRecord eggState = PersistentWorldRegistry.CreateFaunaEggState(0xE6600001u, 77, in eggAup, 12.5f, 90f);
            hatchTimeSeconds = PersistentWorldRegistry.GetFaunaEggHatchTimeSeconds(in eggState);
            AbsoluteUniversePosition unpackedAup = AbsoluteUniversePosition.FromAlignedBlit(in eggState.Position);
            return PersistentWorldRegistry.IsFaunaEggState(in eggState) &&
                   PersistentWorldRegistry.GetFaunaEggSpeciesId(in eggState) == 77 &&
                   math.abs(hatchTimeSeconds - 102.5f) <= 0.0001f &&
                   AUPMath.AUPDistanceSq(in eggAup, in unpackedAup) <= 0.000001d;
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

        private static double3 ToAbsolute(in AbsoluteUniversePositionBlit128 position)
        {
            const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            return new double3(
                (position.GridX * cellSize) + position.Local.x,
                (position.GridY * cellSize) + position.Local.y,
                (position.GridZ * cellSize) + position.Local.z);
        }

        private static float3 ToRuntime(in AbsoluteUniversePositionBlit128 position, float3 origin)
        {
            double3 absolute = ToAbsolute(in position);
            return new float3(
                (float)(absolute.x - origin.x),
                (float)(absolute.y - origin.y),
                (float)(absolute.z - origin.z));
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AupDriftStressJob : IJobParallelFor
        {
            [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> PredatorAups;
            [ReadOnly, NoAlias] public NativeArray<AbsoluteUniversePositionBlit128> PreyAups;
            [NoAlias] public NativeArray<double> DistanceErrors;
            public float3 OriginBefore;
            public float3 OriginAfter;

            public void Execute(int index)
            {
                AbsoluteUniversePositionBlit128 predator = PredatorAups[index];
                AbsoluteUniversePositionBlit128 prey = PreyAups[index];
                double aupDistance = math.sqrt(math.lengthsq(ToAbsolute(in predator) - ToAbsolute(in prey)));
                double runtimeDistanceBefore = math.sqrt(RuntimeDistanceSq(
                    ToRuntime(in predator, OriginBefore),
                    ToRuntime(in prey, OriginBefore)));
                double runtimeDistanceAfter = math.sqrt(RuntimeDistanceSq(
                    ToRuntime(in predator, OriginAfter),
                    ToRuntime(in prey, OriginAfter)));
                double error = math.max(math.abs(aupDistance - runtimeDistanceBefore), math.abs(aupDistance - runtimeDistanceAfter));
                DistanceErrors[index] = math.max(error, math.abs(runtimeDistanceBefore - runtimeDistanceAfter));
            }

            private static double3 ToAbsolute(in AbsoluteUniversePositionBlit128 position)
            {
                const double cellSize = AbsoluteUniversePosition.CellSizeMeters;
                return new double3(
                    (position.GridX * cellSize) + position.Local.x,
                    (position.GridY * cellSize) + position.Local.y,
                    (position.GridZ * cellSize) + position.Local.z);
            }

            private static float3 ToRuntime(in AbsoluteUniversePositionBlit128 position, float3 origin)
            {
                double3 absolute = ToAbsolute(in position);
                return new float3(
                    (float)(absolute.x - origin.x),
                    (float)(absolute.y - origin.y),
                    (float)(absolute.z - origin.z));
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
}
