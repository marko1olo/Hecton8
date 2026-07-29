using System;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Cold-path smoke tester for the sandbox abyssal shelf height function.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonSandboxAbyssalShelfSmokeTester : MonoBehaviour
    {
        private const int SampleCount = 16;
        private const int JsonBufferLength = 1536;
        private const double SlopeProbeMeters = 64.0;
        // Vertical extent has ONE owner: WorldVerticalExtentMath
        // (Scripts/World/WorldVerticalExtentContracts.cs). These were hand-copied duplicates of the
        // HectonSandboxAbyssalShelfMapMagicNode field initialisers; identical values, identical smoke result.
        private const float HighWorldY = WorldVerticalExtentMath.DefaultHighWorldY;
        private const float LowWorldY = WorldVerticalExtentMath.DefaultLowWorldY;
        private const float ShelfRunMeters = 15000f;
        private const float ShelfTargetSlopeDegrees = 30f;

        // THIS SMOKE TEST CANNOT PASS, AND THESE TWO CONSTANTS ARE WHY. Deliberately left as literals
        // rather than re-derived from WorldVerticalExtentMath: they are the (LowWorldY + 100) /
        // (HighWorldY - 100) margin band, and re-deriving them from the canonical window would make a
        // provably wrong derivation look sanctioned.
        //
        // HectonSandboxAbyssalShelfJobs.cs:1174-1175 requires minHeight <= RequiredMinMeters AND
        // maxHeight >= RequiredMaxMeters. The generator's containment interval is Y in
        // [-4655.98, +704.02] (derivation and citations: <remarks> on
        // WorldVerticalExtentMath.DefaultVerticalSpanMeters), so:
        //   min side: -4900 is 244.02 m BELOW the deepest Y the generator can emit -> unsatisfiable.
        //   max side: +1900 is 1195.98 m ABOVE the highest Y the generator can emit -> unsatisfiable.
        // Both conditions are false for every seed, at every position, so Passed is always 0 and this
        // component's only outputs are the failure/coverage telemetry warnings below. It is asserting
        // against a normalisation WINDOW (7000 m) instead of the geology ENVELOPE (5360 m).
        //
        // NOT FIXED HERE ON PURPOSE: choosing the band that should be asserted is a vertical-extent
        // decision (either the window shrinks toward the envelope, or HadalDepthMeters grows toward the
        // window), and that call belongs to the owner. See the backlog note in the consolidation report.
        private const float RequiredMinMeters = -4900f;
        private const float RequiredMaxMeters = 1900f;
        private const float MaxAllowedSlopeDegrees = 85f;
        private const float AupDeterminismToleranceMeters = 0.0001f;
        private const float AupBoundaryContinuityToleranceMeters = 2f;
        private const double AupBoundaryProbeMeters = 0.25;
        private const int ChunkAuditResolution = 17;
        private const double ChunkAuditSizeMeters = 1024.0;
        private const double FarChunkOriginMeters = 50000.0;
        private const string NativeMemoryOwner = nameof(HectonSandboxAbyssalShelfSmokeTester);
        private static readonly uint _smokeFailureWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeFailure"));
        private static readonly uint _smokeCompletionWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeCompletionMs"));
        private static readonly uint _smokeAupDriftWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeAupDrift"));
        private static readonly uint _smokeCoverageWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeCoverage"));
        private static readonly uint _smokeAupBoundaryWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeAupBoundary"));
        private static readonly uint _smokeContextHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelfSmokeTester"));

        [Header("Execution")]
        [SerializeField] private bool runOnStart;

        [Header("Result")]
        [SerializeField] private bool _debugPassed;
        [SerializeField] private int _debugSampleCount;
        [SerializeField] private int _debugInvalidSampleCount;
        [SerializeField] private int _debugCliffSampleCount;
        [SerializeField] private int _debugPlateauSampleCount;
        [SerializeField] private float _debugMinHeightMeters;
        [SerializeField] private float _debugMaxHeightMeters;
        [SerializeField] private float _debugMaxSlopeDegrees;
        [SerializeField] private float _debugAverageSlopeDegrees;
        [SerializeField] private float _debugAverageActiveSlopeDegrees;
        [SerializeField] private float _debugActiveSlopeMinDegrees;
        [SerializeField] private float _debugActiveSlopeMaxDegrees;
        [SerializeField] private int _debugActiveSlopeSampleCount;
        [SerializeField] private int _debugSlope30SampleCount;
        [SerializeField] private float _debugAupDeterminismDeltaMeters;
        [SerializeField] private float _debugAupBoundaryDeltaMeters;
        [SerializeField] private int _debugOriginChunkInvalidSampleCount;
        [SerializeField] private int _debugFarChunkInvalidSampleCount;
        [SerializeField] private float _debugHighChunkAupDeltaMeters;
        [SerializeField] private float _debugCompletionMilliseconds;
        [SerializeField] private string _debugJson = "NotRun";

        // COLD ALLOC: char[1536] - inspector JSON staging for sandbox smoke result - owner: HectonSandboxAbyssalShelfSmokeTester
        private readonly char[] _jsonBuffer = new char[JsonBufferLength];

        public bool LastRunPassed => _debugPassed;
        public string LastRunJson => _debugJson;

        private void Start()
        {
            if (runOnStart)
                RunSmokeTest();
        }

        [ContextMenu("Run Sandbox Abyssal Shelf Smoke Test")]
        private void RunSmokeTestFromContextMenu()
        {
            RunSmokeTest();
        }

        /// <summary>
        /// Executes the Burst-backed smoke pass and writes a JSON result for editor/test harnesses.
        /// </summary>
        public bool RunSmokeTest()
        {
            NativeArray<AbsoluteUniversePosition> positions = default;
            NativeArray<HectonSandboxAbyssalShelfAuditSample> samples = default;
            NativeArray<HectonSandboxAbyssalShelfSampleReduction> reductions = default;
            NativeArray<HectonSandboxAbyssalShelfSmokeSummary> summary = default;
            JobHandle sampleHandle = default;
            bool sampleHandleScheduled = false;
            HectonSandboxAbyssalShelfParams parameters = CreateDefaultParameters();

            try
            {
                positions = AllocateTrackedTempJobArray<AbsoluteUniversePosition>(SampleCount, nameof(positions), NativeArrayOptions.UninitializedMemory);
                samples = AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, nameof(samples), NativeArrayOptions.UninitializedMemory);
                reductions = AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfSampleReduction>(SampleCount, nameof(reductions), NativeArrayOptions.UninitializedMemory);
                summary = AllocateTrackedTempJobArray<HectonSandboxAbyssalShelfSmokeSummary>(1, nameof(summary), NativeArrayOptions.ClearMemory);
                FillSamplePositions(positions);

                var sampleJob = new HectonSandboxAbyssalShelfSmokeSampleJob
                {
                    PositionsAup = positions,
                    OutputSamples = samples,
                    Parameters = parameters,
                    SlopeProbeMeters = SlopeProbeMeters
                };

                long completeStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                sampleHandle = sampleJob.Schedule(SampleCount, 4);
                sampleHandleScheduled = true;
                sampleHandle = new HectonSandboxAbyssalShelfSmokeReductionJob
                {
                    Samples = samples,
                    Reductions = reductions
                }.Schedule(SampleCount, 4, sampleHandle);
                sampleHandle = new HectonSandboxAbyssalShelfSmokeSummaryJob
                {
                    Reductions = reductions,
                    Summary = summary,
                    Parameters = parameters,
                    RequiredSampleCount = SampleCount,
                    RequiredMinHeightMeters = RequiredMinMeters,
                    RequiredMaxHeightMeters = RequiredMaxMeters,
                    MaxAllowedSlopeDegrees = MaxAllowedSlopeDegrees,
                    AupDeterminismToleranceMeters = AupDeterminismToleranceMeters,
                    AupBoundaryContinuityToleranceMeters = AupBoundaryContinuityToleranceMeters,
                    AupBoundaryProbeMeters = AupBoundaryProbeMeters,
                    ChunkAuditResolution = ChunkAuditResolution,
                    ChunkAuditSizeMeters = ChunkAuditSizeMeters,
                    FarChunkOriginMeters = FarChunkOriginMeters
                }.Schedule(sampleHandle);
                DispatcherJobSwap.TryComplete(ref sampleHandle, forceComplete: true);
                sampleHandleScheduled = false;
                _debugCompletionMilliseconds =
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - completeStartTimestamp) *
                    1000.0 /
                    System.Diagnostics.Stopwatch.Frequency);

                ApplySummary(summary[0]);
                WriteDebugJson();

                if (!_debugPassed)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeFailureWarningHash,
                        _smokeContextHash,
                        _debugInvalidSampleCount);
                }

                if (_debugAupDeterminismDeltaMeters > AupDeterminismToleranceMeters)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeAupDriftWarningHash,
                        _smokeContextHash,
                        _debugAupDeterminismDeltaMeters);
                }

                if (_debugAupBoundaryDeltaMeters > AupBoundaryContinuityToleranceMeters)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeAupBoundaryWarningHash,
                        _smokeContextHash,
                        _debugAupBoundaryDeltaMeters);
                }

                if (_debugMaxSlopeDegrees > MaxAllowedSlopeDegrees || _debugPlateauSampleCount == 0 || _debugSlope30SampleCount == 0)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeCoverageWarningHash,
                        _smokeContextHash,
                        _debugCliffSampleCount + _debugPlateauSampleCount + _debugSlope30SampleCount);
                }

                if (_debugPassed && _debugCompletionMilliseconds > 0.2f)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeCompletionWarningHash,
                        _smokeContextHash,
                        _debugCompletionMilliseconds);
                }

                return _debugPassed;
            }
            finally
            {
                if (sampleHandleScheduled)
                    DispatcherJobSwap.TryComplete(ref sampleHandle, forceComplete: true);

                DisposeTrackedTempJobArray(ref positions);
                DisposeTrackedTempJobArray(ref samples);
                DisposeTrackedTempJobArray(ref reductions);
                DisposeTrackedTempJobArray(ref summary);
            }
        }

        public bool TryWriteLastResultJson(Span<char> destination, out int charsWritten)
        {
            return TryWriteJson(
                destination,
                _debugPassed,
                _debugSampleCount,
                _debugInvalidSampleCount,
                _debugCliffSampleCount,
                _debugPlateauSampleCount,
                _debugMinHeightMeters,
                _debugMaxHeightMeters,
                _debugMaxSlopeDegrees,
                _debugAverageSlopeDegrees,
                _debugAverageActiveSlopeDegrees,
                _debugActiveSlopeMinDegrees,
                _debugActiveSlopeMaxDegrees,
                _debugActiveSlopeSampleCount,
                _debugSlope30SampleCount,
                _debugAupDeterminismDeltaMeters,
                _debugAupBoundaryDeltaMeters,
                _debugOriginChunkInvalidSampleCount,
                _debugFarChunkInvalidSampleCount,
                _debugHighChunkAupDeltaMeters,
                _debugCompletionMilliseconds,
                out charsWritten);
        }

        private static HectonSandboxAbyssalShelfParams CreateDefaultParameters()
        {
            return new HectonSandboxAbyssalShelfParams
            {
                AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                DescentRadiusMeters = ShelfRunMeters,
                PlateCellSizeMeters = 4200.0,
                HighWorldY = HighWorldY,
                LowWorldY = LowWorldY,
                RidgeHeightMeters = 700f,
                RidgeMultiplier = 0.08f,
                RidgeWidthMeters = 1450f,
                JunctionWidthMeters = 2800f,
                PlateUniformity = 0.78f,
                DomainWarpMeters = 1450f,
                DomainWarpFrequency = 0.00011f,
                SlopeNoiseFrequency = 0.00003125f,
                MacroExponentialFalloff = 3.1f,
                ShelfRunMeters = ShelfRunMeters,
                ShelfTargetSlopeDegrees = ShelfTargetSlopeDegrees,
                TrenchDepthMeters = 5000f,
                TrenchWidthMeters = 780f,
                TrenchSharpness = 2.4f,
                IslandCenterRadiusMeters = 2600f,
                IslandJunctionThreshold = 0.58f,
                Seed = HectonSandboxAbyssalShelfMath.CombineWorldSeed(880031u, 0),
                MacroGeologyArtifactVersion = WorldMacroGeologyFields.ArtifactVersion
            };
        }

        private static void FillSamplePositions(NativeArray<AbsoluteUniversePosition> positions)
        {
            double cellSize = AbsoluteUniversePosition.CellSizeMeters;
            positions[0] = HectonSandboxAbyssalShelfMath.BuildAupXZ(0.0, 0.0, cellSize);
            positions[1] = HectonSandboxAbyssalShelfMath.BuildAupXZ(9000.0, 0.0, cellSize);
            positions[2] = HectonSandboxAbyssalShelfMath.BuildAupXZ(12500.0, 0.0, cellSize);
            positions[3] = HectonSandboxAbyssalShelfMath.BuildAupXZ(-15000.0, 0.0, cellSize);
            positions[4] = HectonSandboxAbyssalShelfMath.BuildAupXZ(0.0, 16500.0, cellSize);
            positions[5] = HectonSandboxAbyssalShelfMath.BuildAupXZ(cellSize, cellSize, cellSize);
            positions[6] = HectonSandboxAbyssalShelfMath.BuildAupXZ(50000.0, 50000.0, cellSize);
            positions[7] = HectonSandboxAbyssalShelfMath.BuildAupXZ(50125.0, 50375.0, cellSize);
            positions[8] = HectonSandboxAbyssalShelfMath.BuildAupXZ(-50000.0, 50000.0, cellSize);
            positions[9] = HectonSandboxAbyssalShelfMath.BuildAupXZ(15000.0, 15000.0, cellSize);
            positions[10] = HectonSandboxAbyssalShelfMath.BuildAupXZ(-15000.0, 15000.0, cellSize);
            positions[11] = HectonSandboxAbyssalShelfMath.BuildAupXZ(7500.0, -12500.0, cellSize);
            positions[12] = HectonSandboxAbyssalShelfMath.BuildAupXZ(1800.0, 0.0, cellSize);
            positions[13] = HectonSandboxAbyssalShelfMath.BuildAupXZ(2200.0, 0.0, cellSize);
            positions[14] = HectonSandboxAbyssalShelfMath.BuildAupXZ(2450.0, 0.0, cellSize);
            positions[15] = HectonSandboxAbyssalShelfMath.BuildAupXZ(2700.0, 0.0, cellSize);
        }

        private void ApplySummary(HectonSandboxAbyssalShelfSmokeSummary summary)
        {
            _debugSampleCount = summary.SampleCount;
            _debugInvalidSampleCount = summary.InvalidSampleCount;
            _debugCliffSampleCount = summary.CliffSampleCount;
            _debugPlateauSampleCount = summary.PlateauSampleCount;
            _debugMinHeightMeters = summary.MinHeightMeters;
            _debugMaxHeightMeters = summary.MaxHeightMeters;
            _debugMaxSlopeDegrees = summary.MaxSlopeDegrees;
            _debugAverageSlopeDegrees = summary.AverageSlopeDegrees;
            _debugAverageActiveSlopeDegrees = summary.AverageActiveSlopeDegrees;
            _debugActiveSlopeMinDegrees = summary.ActiveSlopeMinDegrees;
            _debugActiveSlopeMaxDegrees = summary.ActiveSlopeMaxDegrees;
            _debugActiveSlopeSampleCount = summary.ActiveSlopeSampleCount;
            _debugSlope30SampleCount = summary.Slope30SampleCount;
            _debugAupDeterminismDeltaMeters = summary.AupDeterminismDeltaMeters;
            _debugAupBoundaryDeltaMeters = summary.AupBoundaryDeltaMeters;
            _debugOriginChunkInvalidSampleCount = summary.OriginChunkInvalidSampleCount;
            _debugFarChunkInvalidSampleCount = summary.FarChunkInvalidSampleCount;
            _debugHighChunkAupDeltaMeters = summary.HighChunkAupDeltaMeters;
            _debugPassed = summary.Passed != 0;
        }

        private void WriteDebugJson()
        {
            if (TryWriteLastResultJson(_jsonBuffer.AsSpan(), out int charsWritten))
                _debugJson = new string(_jsonBuffer, 0, charsWritten);
            else
                _debugJson = "FAIL:JsonBuffer";
        }

        private static NativeArray<T> AllocateTrackedTempJobArray<T>(int length, string label, NativeArrayOptions options) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.TempJob, options);
            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(
                    array,
                    NativeMemoryOwner,
                    label,
                    NativeAllocationLifetime.TempJob);
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
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static unsafe void DisposeTrackedTempJobArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static bool TryWriteJson(
            Span<char> destination,
            bool passed,
            int sampleCount,
            int invalidSampleCount,
            int cliffSampleCount,
            int plateauSampleCount,
            float minHeightMeters,
            float maxHeightMeters,
            float maxSlopeDegrees,
            float averageSlopeDegrees,
            float averageActiveSlopeDegrees,
            float activeSlopeMinDegrees,
            float activeSlopeMaxDegrees,
            int activeSlopeSampleCount,
            int slope30SampleCount,
            float aupDeterminismDeltaMeters,
            float aupBoundaryDeltaMeters,
            int originChunkInvalidSampleCount,
            int farChunkInvalidSampleCount,
            float highChunkAupDeltaMeters,
            float completionMilliseconds,
            out int charsWritten)
        {
            int cursor = 0;
            bool ok =
                AppendLiteral(destination, ref cursor, "{\"tester\":\"HectonSandboxAbyssalShelf\",\"passed\":") &&
                AppendBool(destination, ref cursor, passed) &&
                AppendLiteral(destination, ref cursor, ",\"samples\":") &&
                AppendInt(destination, ref cursor, sampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"invalid\":") &&
                AppendInt(destination, ref cursor, invalidSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"cliffSamples\":") &&
                AppendInt(destination, ref cursor, cliffSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"plateauSamples\":") &&
                AppendInt(destination, ref cursor, plateauSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"minHeightMeters\":") &&
                AppendFloat(destination, ref cursor, minHeightMeters) &&
                AppendLiteral(destination, ref cursor, ",\"maxHeightMeters\":") &&
                AppendFloat(destination, ref cursor, maxHeightMeters) &&
                AppendLiteral(destination, ref cursor, ",\"maxSlopeDegrees\":") &&
                AppendFloat(destination, ref cursor, maxSlopeDegrees) &&
                AppendLiteral(destination, ref cursor, ",\"averageSlopeDegrees\":") &&
                AppendFloat(destination, ref cursor, averageSlopeDegrees) &&
                AppendLiteral(destination, ref cursor, ",\"averageActiveSlopeDegrees\":") &&
                AppendFloat(destination, ref cursor, averageActiveSlopeDegrees) &&
                AppendLiteral(destination, ref cursor, ",\"activeSlopeMinDegrees\":") &&
                AppendFloat(destination, ref cursor, activeSlopeMinDegrees) &&
                AppendLiteral(destination, ref cursor, ",\"activeSlopeMaxDegrees\":") &&
                AppendFloat(destination, ref cursor, activeSlopeMaxDegrees) &&
                AppendLiteral(destination, ref cursor, ",\"activeSlopeSamples\":") &&
                AppendInt(destination, ref cursor, activeSlopeSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"slope30Samples\":") &&
                AppendInt(destination, ref cursor, slope30SampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"aupDeltaMeters\":") &&
                AppendFloat(destination, ref cursor, aupDeterminismDeltaMeters) &&
                AppendLiteral(destination, ref cursor, ",\"aupBoundaryDeltaMeters\":") &&
                AppendFloat(destination, ref cursor, aupBoundaryDeltaMeters) &&
                AppendLiteral(destination, ref cursor, ",\"originChunkInvalid\":") &&
                AppendInt(destination, ref cursor, originChunkInvalidSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"farChunkInvalid\":") &&
                AppendInt(destination, ref cursor, farChunkInvalidSampleCount) &&
                AppendLiteral(destination, ref cursor, ",\"highChunkAupDeltaMeters\":") &&
                AppendFloat(destination, ref cursor, highChunkAupDeltaMeters) &&
                AppendLiteral(destination, ref cursor, ",\"completionMs\":") &&
                AppendFloat(destination, ref cursor, completionMilliseconds) &&
                AppendLiteral(destination, ref cursor, "}");

            charsWritten = ok ? cursor : 0;
            return ok;
        }

        private static bool AppendLiteral(Span<char> destination, ref int cursor, string literal)
        {
            ReadOnlySpan<char> source = literal.AsSpan();
            if (cursor + source.Length > destination.Length)
                return false;

            source.CopyTo(destination.Slice(cursor));
            cursor += source.Length;
            return true;
        }

        private static bool AppendBool(Span<char> destination, ref int cursor, bool value)
        {
            return AppendLiteral(destination, ref cursor, value ? "true" : "false");
        }

        private static bool AppendInt(Span<char> destination, ref int cursor, int value)
        {
            if (value == 0)
                return AppendChar(destination, ref cursor, '0');

            long working = value;
            if (working < 0L)
            {
                if (!AppendChar(destination, ref cursor, '-'))
                    return false;

                working = -working;
            }

            Span<char> scratch = stackalloc char[16];
            int count = 0;
            while (working > 0L)
            {
                scratch[count++] = (char)('0' + (working % 10L));
                working /= 10L;
            }

            if (cursor + count > destination.Length)
                return false;

            for (int i = count - 1; i >= 0; i--)
                destination[cursor++] = scratch[i];

            return true;
        }

        private static bool AppendFloat(Span<char> destination, ref int cursor, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return AppendChar(destination, ref cursor, '0');

            int scaled = (int)math.round(value * 1000f);
            if (scaled < 0)
            {
                if (!AppendChar(destination, ref cursor, '-'))
                    return false;

                scaled = -scaled;
            }

            int whole = scaled / 1000;
            int fraction = scaled - (whole * 1000);
            if (!AppendInt(destination, ref cursor, whole))
                return false;

            if (fraction == 0)
                return true;

            int digits = 3;
            while (digits > 0 && fraction % 10 == 0)
            {
                fraction /= 10;
                digits--;
            }

            if (!AppendChar(destination, ref cursor, '.'))
                return false;

            int divisor = digits == 3 ? 100 : digits == 2 ? 10 : 1;
            for (int i = 0; i < digits; i++)
            {
                int digit = fraction / divisor;
                if (!AppendChar(destination, ref cursor, (char)('0' + digit)))
                    return false;

                fraction -= digit * divisor;
                divisor /= 10;
            }

            return true;
        }

        private static bool AppendChar(Span<char> destination, ref int cursor, char value)
        {
            if (cursor >= destination.Length)
                return false;

            destination[cursor++] = value;
            return true;
        }
    }
}
