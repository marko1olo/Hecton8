using System;
using System.Globalization;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
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
        private const int SampleCount = 12;
        private const int JsonBufferLength = 768;
        private const double SlopeProbeMeters = 64.0;
        private const float HighWorldY = 2000f;
        private const float LowWorldY = -5000f;
        private const float RequiredMinMeters = -4900f;
        private const float RequiredMaxMeters = 1900f;
        private const string NativeMemoryOwner = nameof(HectonSandboxAbyssalShelfSmokeTester);
        private static readonly uint _smokeFailureWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeFailure"));
        private static readonly uint _smokeCompletionWarningHash =
            unchecked((uint)LocHash.Compute("HectonSandboxAbyssalShelf.SmokeCompletionMs"));
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
        [SerializeField] private float _debugAupDeterminismDeltaMeters;
        [SerializeField] private float _debugCompletionMilliseconds;
        [SerializeField] private string _debugJson = "NotRun";

        // COLD ALLOC: char[768] - inspector JSON staging for sandbox smoke result - owner: HectonSandboxAbyssalShelfSmokeTester
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
            NativeArray<double2> positions = default;
            NativeArray<HectonSandboxAbyssalShelfAuditSample> samples = default;
            JobHandle sampleHandle = default;
            bool sampleHandleScheduled = false;
            HectonSandboxAbyssalShelfParams parameters = CreateDefaultParameters();

            try
            {
                positions = new NativeArray<double2>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                samples = new NativeArray<HectonSandboxAbyssalShelfAuditSample>(SampleCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                RegisterTempJobArray(positions, nameof(positions));
                RegisterTempJobArray(samples, nameof(samples));
                FillSamplePositions(positions);

                var sampleJob = new HectonSandboxAbyssalShelfSmokeSampleJob
                {
                    PositionsAupXZ = positions,
                    OutputSamples = samples,
                    Parameters = parameters,
                    SlopeProbeMeters = SlopeProbeMeters
                };

                long completeStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                sampleHandle = sampleJob.Schedule(SampleCount, 4);
                sampleHandleScheduled = true;
                DispatcherJobSwap.TryComplete(ref sampleHandle, forceComplete: true);
                sampleHandleScheduled = false;
                _debugCompletionMilliseconds =
                    (float)((System.Diagnostics.Stopwatch.GetTimestamp() - completeStartTimestamp) *
                    1000.0 /
                    System.Diagnostics.Stopwatch.Frequency);

                EvaluateSamples(samples, in parameters);
                WriteDebugJson();

                if (!_debugPassed)
                {
                    GlobalTelemetryBus.PublishPerformanceWarning(
                        _smokeFailureWarningHash,
                        _smokeContextHash,
                        _debugInvalidSampleCount);
                }
                else if (_debugCompletionMilliseconds > 4f)
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

                if (positions.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(positions);
                    positions.Dispose();
                }

                if (samples.IsCreated)
                {
                    NativeMemorySentinel.UnregisterNativeArray(samples);
                    samples.Dispose();
                }
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
                _debugAupDeterminismDeltaMeters,
                _debugCompletionMilliseconds,
                out charsWritten);
        }

        private static HectonSandboxAbyssalShelfParams CreateDefaultParameters()
        {
            return new HectonSandboxAbyssalShelfParams
            {
                AupCellSizeMeters = AbsoluteUniversePosition.CellSizeMeters,
                DescentRadiusMeters = 16500.0,
                PlateCellSizeMeters = 2200.0,
                HighWorldY = HighWorldY,
                LowWorldY = LowWorldY,
                RidgeHeightMeters = 1750f,
                RidgeMultiplier = 0.22f,
                RidgeWidthMeters = 190f,
                JunctionWidthMeters = 360f,
                PlateUniformity = 0.86f,
                DomainWarpMeters = 480f,
                DomainWarpFrequency = 0.00018f,
                Seed = 880031u
            };
        }

        private static void FillSamplePositions(NativeArray<double2> positions)
        {
            positions[0] = new double2(0.0, 0.0);
            positions[1] = new double2(15000.0, 0.0);
            positions[2] = new double2(16500.0, 0.0);
            positions[3] = new double2(-16500.0, 0.0);
            positions[4] = new double2(0.0, 16500.0);
            positions[5] = new double2(5000.0, 5000.0);
            positions[6] = new double2(100000.0, 0.0);
            positions[7] = new double2(100125.0, -99625.0);
            positions[8] = new double2(-100125.0, 99625.0);
            positions[9] = new double2(15000.0, 15000.0);
            positions[10] = new double2(-15000.0, 15000.0);
            positions[11] = new double2(7500.0, -12500.0);
        }

        private void EvaluateSamples(
            NativeArray<HectonSandboxAbyssalShelfAuditSample> samples,
            in HectonSandboxAbyssalShelfParams parameters)
        {
            _debugSampleCount = samples.Length;
            _debugInvalidSampleCount = 0;
            _debugCliffSampleCount = 0;
            _debugPlateauSampleCount = 0;
            _debugMinHeightMeters = float.MaxValue;
            _debugMaxHeightMeters = float.MinValue;
            _debugMaxSlopeDegrees = 0f;

            for (int i = 0; i < samples.Length; i++)
            {
                HectonSandboxAbyssalShelfAuditSample sample = samples[i];
                if ((sample.Flags & 0x03) != 0)
                    _debugInvalidSampleCount++;

                if ((sample.Flags & 0x04) != 0)
                    _debugCliffSampleCount++;

                if ((sample.Flags & 0x08) != 0)
                    _debugPlateauSampleCount++;

                _debugMinHeightMeters = math.min(_debugMinHeightMeters, sample.HeightMeters);
                _debugMaxHeightMeters = math.max(_debugMaxHeightMeters, sample.HeightMeters);
                _debugMaxSlopeDegrees = math.max(_debugMaxSlopeDegrees, sample.SlopeAngleDegrees);
            }

            float shiftedA = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(100125.0, -99625.0, in parameters);
            float shiftedB = HectonSandboxAbyssalShelfMath.EvaluateHeightMeters(
                20.0 * AbsoluteUniversePosition.CellSizeMeters + 125.0,
                -20.0 * AbsoluteUniversePosition.CellSizeMeters + 375.0,
                in parameters);
            _debugAupDeterminismDeltaMeters = math.abs(shiftedA - shiftedB);
            _debugPassed =
                _debugSampleCount == SampleCount &&
                _debugInvalidSampleCount == 0 &&
                _debugMinHeightMeters <= RequiredMinMeters &&
                _debugMaxHeightMeters >= RequiredMaxMeters &&
                _debugAupDeterminismDeltaMeters <= 0.0001f;
        }

        private void WriteDebugJson()
        {
            if (TryWriteLastResultJson(_jsonBuffer.AsSpan(), out int charsWritten))
                _debugJson = new string(_jsonBuffer, 0, charsWritten);
            else
                _debugJson = "FAIL:JsonBuffer";
        }

        private static void RegisterTempJobArray<T>(NativeArray<T> array, string label) where T : struct
        {
            NativeMemorySentinel.RegisterNativeArray(
                array,
                NativeMemoryOwner,
                label,
                NativeAllocationLifetime.TempJob);
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
            float aupDeterminismDeltaMeters,
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
                AppendLiteral(destination, ref cursor, ",\"aupDeltaMeters\":") &&
                AppendFloat(destination, ref cursor, aupDeterminismDeltaMeters) &&
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
            if (!value.TryFormat(destination.Slice(cursor), out int written, provider: CultureInfo.InvariantCulture))
                return false;

            cursor += written;
            return true;
        }

        private static bool AppendFloat(Span<char> destination, ref int cursor, float value)
        {
            if (!value.TryFormat(destination.Slice(cursor), out int written, "0.###", CultureInfo.InvariantCulture))
                return false;

            cursor += written;
            return true;
        }
    }
}
