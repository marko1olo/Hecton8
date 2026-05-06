using System;
using System.Globalization;
using System.IO;
using System.Text;

internal static class Program
{
    private const int SampleCount = 16;
    private const double SlopeProbeMeters = 64.0;
    private const double AupCellSizeMeters = 5000.0;
    private const float HighWorldY = 2000f;
    private const float LowWorldY = -5000f;
    private const float ShelfRunMeters = 15000f;
    private const float ShelfTargetSlopeDegrees = 30f;
    private const float RequiredMinMeters = -4900f;
    private const float RequiredMaxMeters = 1900f;
    private const float MaxAllowedSlopeDegrees = 62f;
    private const float AupDeterminismToleranceMeters = 0.0001f;
    private const float AupBoundaryContinuityToleranceMeters = 2f;
    private const double AupBoundaryProbeMeters = 0.25;
    private const int ChunkAuditResolution = 17;
    private const double ChunkAuditSizeMeters = 1024.0;
    private const double FarChunkOriginMeters = 50000.0;

    private static int Main(string[] args)
    {
        string outputPath = ResolveOutputPath(args);
        ShelfParams parameters = CreateDefaultParameters();
        AuditSample[] samples = new AuditSample[SampleCount];
        SampleReduction[] reductions = new SampleReduction[SampleCount];
        AupPosition[] positions = CreateSamplePositions();

        long start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (int i = 0; i < SampleCount; i++)
            samples[i] = Sample(positions[i], parameters);

        for (int i = 0; i < SampleCount; i++)
            reductions[i] = Reduce(samples[i]);

        SmokeSummary summary = Summarize(reductions, parameters);
        double elapsedMs = (System.Diagnostics.Stopwatch.GetTimestamp() - start) *
            1000.0 /
            System.Diagnostics.Stopwatch.Frequency;

        string json = WriteJson(summary, elapsedMs);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, json);
        Console.WriteLine(json);
        return summary.Passed ? 0 : 1;
    }

    private static string ResolveOutputPath(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--output", StringComparison.Ordinal))
                return Path.GetFullPath(args[i + 1]);
        }

        return Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "HectonSandboxAbyssalShelfStandaloneSmoke.json"));
    }

    private static ShelfParams CreateDefaultParameters()
    {
        return new ShelfParams
        {
            AupCellSizeMeters = AupCellSizeMeters,
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
            MacroExponentialFalloff = 3.1f,
            ShelfRunMeters = ShelfRunMeters,
            ShelfTargetSlopeDegrees = ShelfTargetSlopeDegrees,
            TrenchDepthMeters = 5000f,
            TrenchWidthMeters = 780f,
            TrenchSharpness = 2.4f,
            IslandCenterRadiusMeters = 2600f,
            IslandJunctionThreshold = 0.58f,
            Seed = CombineWorldSeed(880031u, 0)
        };
    }

    private static AupPosition[] CreateSamplePositions()
    {
        return new[]
        {
            BuildAupXZ(0.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(9000.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(12500.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(-15000.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(0.0, 16500.0, AupCellSizeMeters),
            BuildAupXZ(5000.0, 5000.0, AupCellSizeMeters),
            BuildAupXZ(50000.0, 50000.0, AupCellSizeMeters),
            BuildAupXZ(50125.0, 50375.0, AupCellSizeMeters),
            BuildAupXZ(-50000.0, 50000.0, AupCellSizeMeters),
            BuildAupXZ(15000.0, 15000.0, AupCellSizeMeters),
            BuildAupXZ(-15000.0, 15000.0, AupCellSizeMeters),
            BuildAupXZ(7500.0, -12500.0, AupCellSizeMeters),
            BuildAupXZ(1800.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(2200.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(2450.0, 0.0, AupCellSizeMeters),
            BuildAupXZ(2700.0, 0.0, AupCellSizeMeters)
        };
    }

    private static AuditSample Sample(AupPosition positionAup, ShelfParams parameters)
    {
        Double2 position = ResolveSampleAupXZ(positionAup, 0.0, 0.0, Math.Max(1.0, parameters.AupCellSizeMeters));
        double probe = Math.Max(0.001, SlopeProbeMeters);
        AupPosition neighborXAup = BuildAupXZ(position.X + probe, position.Y, Math.Max(1.0, parameters.AupCellSizeMeters));
        AupPosition neighborZAup = BuildAupXZ(position.X, position.Y + probe, Math.Max(1.0, parameters.AupCellSizeMeters));
        float center = EvaluateHeightMeters(positionAup, parameters);
        float neighborX = EvaluateHeightMeters(neighborXAup, parameters);
        float neighborZ = EvaluateHeightMeters(neighborZAup, parameters);
        float dx = (neighborX - center) / (float)probe;
        float dz = (neighborZ - center) / (float)probe;
        float gradient = MathF.Sqrt(dx * dx + dz * dz);
        float slopeAngle = Degrees(MathF.Atan(gradient));
        byte flags = 0;

        if (!float.IsFinite(center) || !float.IsFinite(neighborX) || !float.IsFinite(neighborZ))
            flags |= 1;

        if (center < parameters.LowWorldY - 0.5f || center > parameters.HighWorldY + 0.5f)
            flags |= 2;

        if (slopeAngle >= 45f)
            flags |= 4;

        if (slopeAngle <= 15f)
            flags |= 8;

        if (slopeAngle >= 24f && slopeAngle <= 36f)
            flags |= 16;

        return new AuditSample
        {
            Position = position,
            HeightMeters = center,
            NeighborHeightXMeters = neighborX,
            NeighborHeightZMeters = neighborZ,
            SlopeAngleDegrees = slopeAngle,
            Flags = flags
        };
    }

    private static SampleReduction Reduce(AuditSample sample)
    {
        return new SampleReduction
        {
            InvalidSampleCount = (sample.Flags & 0x03) != 0 ? 1 : 0,
            CliffSampleCount = (sample.Flags & 0x04) != 0 ? 1 : 0,
            PlateauSampleCount = (sample.Flags & 0x08) != 0 ? 1 : 0,
            MinHeightMeters = sample.HeightMeters,
            MaxHeightMeters = sample.HeightMeters,
            MaxSlopeDegrees = sample.SlopeAngleDegrees,
            SlopeAngleSumDegrees = sample.SlopeAngleDegrees,
            ActiveSlopeAngleSumDegrees = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 45f ? sample.SlopeAngleDegrees : 0f,
            ActiveSlopeMinDegrees = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 45f ? sample.SlopeAngleDegrees : float.MaxValue,
            ActiveSlopeMaxDegrees = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 45f ? sample.SlopeAngleDegrees : float.MinValue,
            Slope30SampleCount = (sample.Flags & 0x10) != 0 ? 1 : 0,
            ActiveSlopeSampleCount = sample.SlopeAngleDegrees > 15f && sample.SlopeAngleDegrees < 45f ? 1 : 0
        };
    }

    private static SmokeSummary Summarize(SampleReduction[] reductions, ShelfParams parameters)
    {
        int invalidCount = 0;
        int cliffCount = 0;
        int plateauCount = 0;
        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        float maxSlope = 0f;
        float slopeSum = 0f;
        float activeSlopeSum = 0f;
        float activeSlopeMin = float.MaxValue;
        float activeSlopeMax = float.MinValue;
        int slope30Count = 0;
        int activeSlopeCount = 0;

        for (int i = 0; i < reductions.Length; i++)
        {
            SampleReduction reduction = reductions[i];
            invalidCount += reduction.InvalidSampleCount;
            cliffCount += reduction.CliffSampleCount;
            plateauCount += reduction.PlateauSampleCount;
            minHeight = MathF.Min(minHeight, reduction.MinHeightMeters);
            maxHeight = MathF.Max(maxHeight, reduction.MaxHeightMeters);
            maxSlope = MathF.Max(maxSlope, reduction.MaxSlopeDegrees);
            slopeSum += reduction.SlopeAngleSumDegrees;
            activeSlopeSum += reduction.ActiveSlopeAngleSumDegrees;
            activeSlopeMin = MathF.Min(activeSlopeMin, reduction.ActiveSlopeMinDegrees);
            activeSlopeMax = MathF.Max(activeSlopeMax, reduction.ActiveSlopeMaxDegrees);
            slope30Count += reduction.Slope30SampleCount;
            activeSlopeCount += reduction.ActiveSlopeSampleCount;
        }

        double cellSize = Math.Max(1.0, parameters.AupCellSizeMeters);
        AupPosition shiftedAup = BuildAupXZ(100125.0, -99625.0, cellSize);
        float shiftedA = EvaluateHeightMeters(100125.0, -99625.0, parameters);
        float shiftedB = EvaluateHeightMeters(shiftedAup, parameters);
        float aupDelta = MathF.Abs(shiftedA - shiftedB);
        double boundaryProbe = Math.Max(0.001, AupBoundaryProbeMeters);
        AupPosition boundaryLeftAup = BuildAupXZ(cellSize - boundaryProbe, 375.125, cellSize);
        AupPosition boundaryRightAup = BuildAupXZ(cellSize + boundaryProbe, 375.125, cellSize);
        float boundaryLeft = EvaluateHeightMeters(boundaryLeftAup, parameters);
        float boundaryRight = EvaluateHeightMeters(boundaryRightAup, parameters);
        float boundaryDelta = MathF.Abs(boundaryLeft - boundaryRight);
        AupPosition highChunkAup = BuildAupXZ(FarChunkOriginMeters + 125.0, FarChunkOriginMeters + 375.0, cellSize);
        float highChunkDirect = EvaluateHeightMeters(FarChunkOriginMeters + 125.0, FarChunkOriginMeters + 375.0, parameters);
        float highChunkAupHeight = EvaluateHeightMeters(highChunkAup, parameters);
        float highChunkDelta = MathF.Abs(highChunkDirect - highChunkAupHeight);
        int originChunkInvalid = CountInvalidChunk(0.0, 0.0, ChunkAuditResolution, ChunkAuditSizeMeters, parameters);
        int farChunkInvalid = CountInvalidChunk(FarChunkOriginMeters, FarChunkOriginMeters, ChunkAuditResolution, ChunkAuditSizeMeters, parameters);
        float averageSlope = slopeSum / Math.Max(1, reductions.Length);
        float averageActiveSlope = activeSlopeSum / Math.Max(1, activeSlopeCount);
        float resolvedActiveSlopeMin = activeSlopeCount > 0 ? activeSlopeMin : 0f;
        float resolvedActiveSlopeMax = activeSlopeCount > 0 ? activeSlopeMax : 0f;
        bool passed =
            reductions.Length == SampleCount &&
            invalidCount == 0 &&
            plateauCount > 0 &&
            slope30Count > 0 &&
            activeSlopeCount > 0 &&
            minHeight <= RequiredMinMeters &&
            maxHeight >= RequiredMaxMeters &&
            maxSlope <= MaxAllowedSlopeDegrees &&
            resolvedActiveSlopeMin >= 15f &&
            resolvedActiveSlopeMax <= 45f &&
            averageActiveSlope >= 24f &&
            averageActiveSlope <= 42f &&
            aupDelta <= AupDeterminismToleranceMeters &&
            boundaryDelta <= AupBoundaryContinuityToleranceMeters &&
            highChunkDelta <= AupDeterminismToleranceMeters &&
            originChunkInvalid == 0 &&
            farChunkInvalid == 0;

        return new SmokeSummary
        {
            SampleCount = reductions.Length,
            InvalidSampleCount = invalidCount,
            CliffSampleCount = cliffCount,
            PlateauSampleCount = plateauCount,
            MinHeightMeters = minHeight,
            MaxHeightMeters = maxHeight,
            MaxSlopeDegrees = maxSlope,
            AverageSlopeDegrees = averageSlope,
            AverageActiveSlopeDegrees = averageActiveSlope,
            ActiveSlopeMinDegrees = resolvedActiveSlopeMin,
            ActiveSlopeMaxDegrees = resolvedActiveSlopeMax,
            ActiveSlopeSampleCount = activeSlopeCount,
            Slope30SampleCount = slope30Count,
            AupDeterminismDeltaMeters = aupDelta,
            AupBoundaryDeltaMeters = boundaryDelta,
            OriginChunkInvalidSampleCount = originChunkInvalid,
            FarChunkInvalidSampleCount = farChunkInvalid,
            HighChunkAupDeltaMeters = highChunkDelta,
            Passed = passed
        };
    }

    private static float EvaluateHeightMeters(double absoluteX, double absoluteZ, ShelfParams parameters)
    {
        AupPosition position = BuildAupXZ(absoluteX, absoluteZ, Math.Max(1.0, parameters.AupCellSizeMeters));
        return EvaluateHeightMeters(position, parameters);
    }

    private static float EvaluateSeededHeightMeters(Double2 aupXZ, ShelfParams parameters)
    {
        float heightRange = MathF.Max(0.001f, parameters.HighWorldY - parameters.LowWorldY);
        float macro01 = EvaluateGreatDescent01(aupXZ, parameters.DescentRadiusMeters, parameters.MacroExponentialFalloff);
        float baseY = Lerp(parameters.HighWorldY, parameters.LowWorldY, macro01);
        float base01 = Saturate((baseY - parameters.LowWorldY) / heightRange);

        RidgeData ridge = EvaluateVoronoiRidgeData(aupXZ, parameters);
        float ridgeMask = ridge.RidgeMask;
        float ridgeAttenuation = SmoothStep(0.04f, 0.42f, base01);
        float ridgeLift01 = Saturate(parameters.RidgeHeightMeters / heightRange) * ridgeMask * ridgeAttenuation;
        float multiplied01 = base01 * (1f + MathF.Max(0f, parameters.RidgeMultiplier) * ridgeMask * ridgeAttenuation);
        float ridged01 = Saturate(multiplied01 + ridgeLift01);
        float heightMeters = parameters.LowWorldY + ridged01 * heightRange;
        float trenchMask = MathF.Pow(Saturate(ridge.TrenchMask), MathF.Max(0.35f, parameters.TrenchSharpness));
        float trenchDepth = MathF.Max(0f, parameters.TrenchDepthMeters);
        float trenchDescentBias = SmoothStep(0.18f, 0.96f, macro01);
        heightMeters -= trenchDepth * trenchMask * trenchDescentBias;

        if (heightMeters > 0f)
            heightMeters *= ridge.IslandMask;

        return MathF.Min(parameters.HighWorldY, MathF.Max(parameters.LowWorldY, heightMeters));
    }

    private static float EvaluateHeightMeters(AupPosition position, ShelfParams parameters)
    {
        Double2 aupXZ = ResolveSampleAupXZ(position, 0.0, 0.0, Math.Max(1.0, parameters.AupCellSizeMeters));
        parameters.Seed = DeriveAupGridSeed(parameters.Seed, position.GridX, position.GridZ);
        return EvaluateSeededHeightMeters(aupXZ, parameters);
    }

    private static int CountInvalidChunk(
        double originX,
        double originZ,
        int resolution,
        double chunkSizeMeters,
        ShelfParams parameters)
    {
        int invalidCount = 0;
        int safeResolution = Math.Max(2, resolution);
        double cellSize = Math.Max(1.0, parameters.AupCellSizeMeters);
        double step = Math.Max(1.0, chunkSizeMeters) / Math.Max(1, safeResolution - 1);

        for (int z = 0; z < safeResolution; z++)
        {
            for (int x = 0; x < safeResolution; x++)
            {
                AupPosition sampleAup = BuildAupXZ(originX + x * step, originZ + z * step, cellSize);
                float h = EvaluateHeightMeters(sampleAup, parameters);
                if (!float.IsFinite(h) || h < parameters.LowWorldY - 0.5f || h > parameters.HighWorldY + 0.5f)
                    invalidCount++;
            }
        }

        return invalidCount;
    }

    private static Double2 ResolveAupAlignedXZ(Double2 absoluteXZ, double cellSizeMeters)
    {
        long gridX = (long)Math.Floor(absoluteXZ.X / cellSizeMeters);
        long gridZ = (long)Math.Floor(absoluteXZ.Y / cellSizeMeters);
        double localX = absoluteXZ.X - gridX * cellSizeMeters;
        double localZ = absoluteXZ.Y - gridZ * cellSizeMeters;
        return new Double2(gridX * cellSizeMeters + localX, gridZ * cellSizeMeters + localZ);
    }

    private static AupPosition BuildAupXZ(double absoluteX, double absoluteZ, double cellSizeMeters)
    {
        double safeCellSize = Math.Max(1.0, cellSizeMeters);
        long gridX = (long)Math.Floor(absoluteX / safeCellSize);
        long gridZ = (long)Math.Floor(absoluteZ / safeCellSize);
        return new AupPosition(
            gridX,
            gridZ,
            (float)(absoluteX - gridX * safeCellSize),
            (float)(absoluteZ - gridZ * safeCellSize));
    }

    private static Double2 ResolveSampleAupXZ(AupPosition origin, double localOffsetX, double localOffsetZ, double cellSizeMeters)
    {
        double safeCellSize = Math.Max(1.0, cellSizeMeters);
        return new Double2(
            origin.GridX * safeCellSize + origin.LocalX + localOffsetX,
            origin.GridZ * safeCellSize + origin.LocalZ + localOffsetZ);
    }

    private static float EvaluateGreatDescent01(Double2 aupXZ, double descentRadiusMeters, float macroExponentialFalloff)
    {
        double radius = Math.Sqrt(aupXZ.X * aupXZ.X + aupXZ.Y * aupXZ.Y);
        double t = Saturate(radius / Math.Max(1.0, descentRadiusMeters));
        double falloff = Math.Max(0.1, macroExponentialFalloff);
        double curved = 1.0 - Math.Exp(-falloff * t * t);
        double normalization = 1.0 - Math.Exp(-falloff);
        return (float)(curved / Math.Max(0.000001, normalization));
    }

    private static RidgeData EvaluateVoronoiRidgeData(Double2 aupXZ, ShelfParams parameters)
    {
        Double2 warpedXZ = aupXZ + EvaluateDomainWarp(aupXZ, parameters);
        double safePlateSize = Math.Max(1.0, parameters.PlateCellSizeMeters);
        Double2 platePosition = warpedXZ / safePlateSize;
        Int2 baseCell = FloorToInt2(platePosition);

        double first = double.MaxValue;
        double second = double.MaxValue;
        double third = double.MaxValue;
        uint nearestHash = 0u;

        for (int dz = -2; dz <= 2; dz++)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                Int2 cell = baseCell + new Int2(dx, dz);
                Double2 feature = new Double2(cell.X, cell.Y) +
                    ResolveFeatureOffset(cell, parameters.Seed, parameters.PlateUniformity);
                Double2 delta = platePosition - feature;
                double distSq = delta.X * delta.X + delta.Y * delta.Y;

                if (distSq < first)
                {
                    third = second;
                    second = first;
                    first = distSq;
                    nearestHash = Hash(cell.X, cell.Y, parameters.Seed);
                }
                else if (distSq < second)
                {
                    third = second;
                    second = distSq;
                }
                else if (distSq < third)
                {
                    third = distSq;
                }
            }
        }

        double firstDistance = Math.Sqrt(first);
        double secondDistance = Math.Sqrt(second);
        double thirdDistance = Math.Sqrt(third);
        float edgeDeltaMeters = (float)((secondDistance - firstDistance) * safePlateSize);
        float junctionDeltaMeters = (float)((thirdDistance - secondDistance) * safePlateSize);

        float edgeWidth = MathF.Max(0.001f, parameters.RidgeWidthMeters);
        float junctionWidth = MathF.Max(0.001f, parameters.JunctionWidthMeters);
        float branchNoise = FractalPerlinNoise(
            new Float2((float)(warpedXZ.X * 0.00037), (float)(warpedXZ.Y * 0.00037)),
            parameters.Seed ^ 0x31D9A7B5u);
        float branchWidthScale = Lerp(0.58f, 1.42f, branchNoise);
        float edgeMask = 1f - SmoothStep(edgeWidth * 0.10f, edgeWidth * branchWidthScale, edgeDeltaMeters);
        float junctionWidthScale = Lerp(0.72f, 1.32f, branchNoise);
        float junctionMask = 1f - SmoothStep(junctionWidth * 0.14f, junctionWidth * junctionWidthScale, junctionDeltaMeters);
        float forkNoise = FractalPerlinNoise(
            new Float2((float)(warpedXZ.X * 0.00021), (float)(warpedXZ.Y * 0.00021)),
            parameters.Seed ^ 0x51633E2Du);
        float irregularity = Lerp(0.86f, 1.14f, HashToUnitFloat(nearestHash ^ 0xA24BAED5u));
        float forkLift = SmoothStep(0.38f, 0.92f, junctionMask + forkNoise * 0.28f);
        float branched = Saturate(edgeMask * 0.76f + junctionMask * 0.82f + forkLift * 0.18f);
        float ridgeMask = Saturate(branched * irregularity);
        float centerDistanceMeters = (float)(firstDistance * safePlateSize);
        float trenchWidth = parameters.TrenchWidthMeters > 0.001f
            ? parameters.TrenchWidthMeters
            : edgeWidth * 0.58f;
        trenchWidth = MathF.Max(1f, trenchWidth);
        float centerMask = 1f - SmoothStep(trenchWidth * 0.12f, trenchWidth, centerDistanceMeters);
        float lowCenterToken = MathF.Pow(1f - HashToUnitFloat(nearestHash ^ 0x6C8E9CF5u), 2.35f);
        float trenchCandidate = SmoothStep(0.46f, 0.92f, lowCenterToken);
        float trenchNoise = FractalPerlinNoise(
            new Float2((float)(warpedXZ.X * 0.00043), (float)(warpedXZ.Y * 0.00043)),
            parameters.Seed ^ 0x91E83B37u);
        float trenchMask = Saturate(centerMask * trenchCandidate * Lerp(0.74f, 1.18f, trenchNoise));
        float islandNoise = FractalPerlinNoise(
            new Float2((float)(warpedXZ.X * 0.000083), (float)(warpedXZ.Y * 0.000083)),
            parameters.Seed ^ 0xDB4F0B91u);
        float junctionThreshold = Saturate(parameters.IslandJunctionThreshold);
        float junctionIsland = junctionMask *
            SmoothStep(junctionThreshold, MathF.Min(0.999f, junctionThreshold + 0.22f), islandNoise);
        double radius = Math.Sqrt(aupXZ.X * aupXZ.X + aupXZ.Y * aupXZ.Y);
        float centerRadius = MathF.Max(1f, parameters.IslandCenterRadiusMeters);
        float centerIsland = 1f - SmoothStep(centerRadius * 0.35f, centerRadius, (float)radius);

        return new RidgeData
        {
            RidgeMask = ridgeMask,
            EdgeMask = edgeMask,
            JunctionMask = junctionMask,
            IslandMask = Saturate(MathF.Max(centerIsland, junctionIsland)),
            TrenchMask = trenchMask
        };
    }

    private static Double2 EvaluateDomainWarp(Double2 aupXZ, ShelfParams parameters)
    {
        float amplitude = MathF.Max(0f, parameters.DomainWarpMeters);
        if (amplitude <= 0.0001f)
            return new Double2(0.0, 0.0);

        Float2 sample = new Float2((float)aupXZ.X, (float)aupXZ.Y) *
            MathF.Max(0.000001f, parameters.DomainWarpFrequency);
        float lowX = FractalPerlinNoise(sample, parameters.Seed ^ 0x5F356495u) * 2f - 1f;
        float lowZ = FractalPerlinNoise(sample + new Float2(17.317f, -41.113f), parameters.Seed ^ 0xC2B2AE35u) * 2f - 1f;
        float highX = FractalPerlinNoise(sample * 2.37f + new Float2(-61.7f, 8.31f), parameters.Seed ^ 0xB5297A4Du) * 2f - 1f;
        float highZ = FractalPerlinNoise(sample * 2.11f + new Float2(4.89f, 73.2f), parameters.Seed ^ 0x68E31DA4u) * 2f - 1f;
        float twist = FractalPerlinNoise(sample * 0.73f + new Float2(31.19f, -22.7f), parameters.Seed ^ 0x1B56C4E9u) * 2f - 1f;
        float angle = twist * 1.0471976f;
        float s = MathF.Sin(angle);
        float c = MathF.Cos(angle);
        Float2 warp = new Float2(lowX, lowZ) * 0.72f + new Float2(highX, highZ) * 0.28f;
        Float2 twisted = new Float2(warp.X * c - warp.Y * s, warp.X * s + warp.Y * c);
        return new Double2(twisted.X * amplitude, twisted.Y * amplitude);
    }

    private static Double2 ResolveFeatureOffset(Int2 cell, uint seed, float uniformity)
    {
        float u = Saturate(uniformity);
        Float2 hash = new Float2(
            Hash01(cell.X, cell.Y, seed),
            Hash01(cell.X, cell.Y, seed ^ 0x9E3779B9u));
        Float2 offset = Lerp(new Float2(0.5f, 0.5f), hash, u);
        return new Double2(offset.X, offset.Y);
    }

    private static float FractalValueNoise(Float2 sample, uint seed)
    {
        float amplitude = 0.5f;
        float frequency = 1f;
        float total = 0f;
        float normalization = 0f;

        for (int octave = 0; octave < 4; octave++)
        {
            total += ValueNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.07f;
        }

        return total / MathF.Max(0.0001f, normalization);
    }

    private static float FractalPerlinNoise(Float2 sample, uint seed)
    {
        float amplitude = 0.5f;
        float frequency = 1f;
        float total = 0f;
        float normalization = 0f;

        for (int octave = 0; octave < 4; octave++)
        {
            total += PerlinNoise(sample * frequency, seed + (uint)octave * 0x85EBCA6Bu) * amplitude;
            normalization += amplitude;
            amplitude *= 0.5f;
            frequency *= 2.07f;
        }

        return total / MathF.Max(0.0001f, normalization);
    }

    private static float PerlinNoise(Float2 sample, uint seed)
    {
        Float2 floorSample = Floor(sample);
        Int2 cell = new Int2((int)floorSample.X, (int)floorSample.Y);
        Float2 local = sample - floorSample;
        Float2 smooth = local * local * local * (local * (local * 6f - 15f) + new Float2(10f, 10f));

        float a = GradientDot(cell.X, cell.Y, local, seed);
        float b = GradientDot(cell.X + 1, cell.Y, local - new Float2(1f, 0f), seed);
        float c = GradientDot(cell.X, cell.Y + 1, local - new Float2(0f, 1f), seed);
        float d = GradientDot(cell.X + 1, cell.Y + 1, local - new Float2(1f, 1f), seed);
        float value = Lerp(Lerp(a, b, smooth.X), Lerp(c, d, smooth.X), smooth.Y);
        return Saturate(value * 0.70710678f + 0.5f);
    }

    private static float GradientDot(int x, int y, Float2 delta, uint seed)
    {
        uint direction = Hash(x, y, seed) & 7u;
        Float2 gradient =
            direction == 0u ? new Float2(1f, 0f) :
            direction == 1u ? new Float2(-1f, 0f) :
            direction == 2u ? new Float2(0f, 1f) :
            direction == 3u ? new Float2(0f, -1f) :
            direction == 4u ? new Float2(0.70710678f, 0.70710678f) :
            direction == 5u ? new Float2(-0.70710678f, 0.70710678f) :
            direction == 6u ? new Float2(0.70710678f, -0.70710678f) :
            new Float2(-0.70710678f, -0.70710678f);

        return gradient.X * delta.X + gradient.Y * delta.Y;
    }

    private static float ValueNoise(Float2 sample, uint seed)
    {
        Float2 floorSample = Floor(sample);
        Int2 cell = new Int2((int)floorSample.X, (int)floorSample.Y);
        Float2 local = sample - floorSample;
        Float2 smooth = local * local * (new Float2(3f, 3f) - 2f * local);

        float a = Hash01(cell.X, cell.Y, seed);
        float b = Hash01(cell.X + 1, cell.Y, seed);
        float c = Hash01(cell.X, cell.Y + 1, seed);
        float d = Hash01(cell.X + 1, cell.Y + 1, seed);

        return Lerp(Lerp(a, b, smooth.X), Lerp(c, d, smooth.X), smooth.Y);
    }

    private static float Hash01(int x, int y, uint seed)
    {
        return HashToUnitFloat(Hash(x, y, seed));
    }

    private static uint Hash(int x, int y, uint seed)
    {
        uint hash = unchecked((uint)x * 0x8DA6B343u);
        hash ^= unchecked((uint)y * 0xD8163841u);
        hash ^= seed + 0x9E3779B9u + (hash << 6) + (hash >> 2);
        hash ^= hash >> 16;
        hash *= 0x7FEB352Du;
        hash ^= hash >> 15;
        hash *= 0x846CA68Bu;
        hash ^= hash >> 16;
        return hash;
    }

    private static uint CombineWorldSeed(uint authoringSeed, int runtimeWorldSeed)
    {
        return Hash((int)authoringSeed, runtimeWorldSeed, 0x4D3C2B1Au);
    }

    private static uint DeriveAupGridSeed(uint worldSeed, long gridX, long gridZ)
    {
        const long macroChunkGridCells = 20L;
        long chunkX = FloorDiv(gridX, macroChunkGridCells);
        long chunkZ = FloorDiv(gridZ, macroChunkGridCells);
        return Hash((int)chunkX, (int)chunkZ, worldSeed ^ 0x73C6A91Fu);
    }

    private static long FloorDiv(long value, long divisor)
    {
        long quotient = value / divisor;
        long remainder = value % divisor;
        return remainder != 0L && ((remainder < 0L) != (divisor < 0L))
            ? quotient - 1L
            : quotient;
    }

    private static float HashToUnitFloat(uint hash)
    {
        return (hash & 0x00FFFFFFu) * (1f / 16777215f);
    }

    private static string WriteJson(SmokeSummary summary, double elapsedMs)
    {
        var builder = new StringBuilder(512);
        builder.Append('{');
        AppendJson(builder, "status", summary.Passed ? "MACRO SHELF VERIFIED" : "FAIL");
        builder.Append(',');
        AppendJson(builder, "tester", "HectonSandboxAbyssalShelfStandaloneSmoke");
        builder.Append(',');
        AppendJson(builder, "samples", summary.SampleCount);
        builder.Append(',');
        AppendJson(builder, "invalid", summary.InvalidSampleCount);
        builder.Append(',');
        AppendJson(builder, "cliffSamples", summary.CliffSampleCount);
        builder.Append(',');
        AppendJson(builder, "plateauSamples", summary.PlateauSampleCount);
        builder.Append(',');
        AppendJson(builder, "minHeightMeters", summary.MinHeightMeters);
        builder.Append(',');
        AppendJson(builder, "maxHeightMeters", summary.MaxHeightMeters);
        builder.Append(',');
        AppendJson(builder, "maxSlopeDegrees", summary.MaxSlopeDegrees);
        builder.Append(',');
        AppendJson(builder, "averageSlopeDegrees", summary.AverageSlopeDegrees);
        builder.Append(',');
        AppendJson(builder, "averageActiveSlopeDegrees", summary.AverageActiveSlopeDegrees);
        builder.Append(',');
        AppendJson(builder, "activeSlopeMinDegrees", summary.ActiveSlopeMinDegrees);
        builder.Append(',');
        AppendJson(builder, "activeSlopeMaxDegrees", summary.ActiveSlopeMaxDegrees);
        builder.Append(',');
        AppendJson(builder, "activeSlopeSamples", summary.ActiveSlopeSampleCount);
        builder.Append(',');
        AppendJson(builder, "slope30Samples", summary.Slope30SampleCount);
        builder.Append(',');
        AppendJson(builder, "aupDeltaMeters", summary.AupDeterminismDeltaMeters);
        builder.Append(',');
        AppendJson(builder, "aupBoundaryDeltaMeters", summary.AupBoundaryDeltaMeters);
        builder.Append(',');
        AppendJson(builder, "originChunkInvalid", summary.OriginChunkInvalidSampleCount);
        builder.Append(',');
        AppendJson(builder, "farChunkInvalid", summary.FarChunkInvalidSampleCount);
        builder.Append(',');
        AppendJson(builder, "highChunkAupDeltaMeters", summary.HighChunkAupDeltaMeters);
        builder.Append(',');
        AppendJson(builder, "elapsedMs", elapsedMs);
        builder.Append('}');
        return builder.ToString();
    }

    private static void AppendJson(StringBuilder builder, string name, string value)
    {
        builder.Append('"').Append(name).Append("\":\"").Append(value).Append('"');
    }

    private static void AppendJson(StringBuilder builder, string name, int value)
    {
        builder.Append('"').Append(name).Append("\":").Append(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AppendJson(StringBuilder builder, string name, float value)
    {
        builder.Append('"').Append(name).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static void AppendJson(StringBuilder builder, string name, double value)
    {
        builder.Append('"').Append(name).Append("\":").Append(value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    private static Float2 Floor(Float2 value)
    {
        return new Float2(MathF.Floor(value.X), MathF.Floor(value.Y));
    }

    private static Int2 FloorToInt2(Double2 value)
    {
        return new Int2((int)Math.Floor(value.X), (int)Math.Floor(value.Y));
    }

    private static float Saturate(float value)
    {
        return MathF.Min(1f, MathF.Max(0f, value));
    }

    private static double Saturate(double value)
    {
        return Math.Min(1.0, Math.Max(0.0, value));
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Saturate((value - edge0) / MathF.Max(0.000001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }

    private static Float2 Lerp(Float2 a, Float2 b, float t)
    {
        return a + (b - a) * t;
    }

    private static float Degrees(float radians)
    {
        return radians * (180f / MathF.PI);
    }

    private struct ShelfParams
    {
        public double AupCellSizeMeters;
        public double DescentRadiusMeters;
        public double PlateCellSizeMeters;
        public float HighWorldY;
        public float LowWorldY;
        public float RidgeHeightMeters;
        public float RidgeMultiplier;
        public float RidgeWidthMeters;
        public float JunctionWidthMeters;
        public float PlateUniformity;
        public float DomainWarpMeters;
        public float DomainWarpFrequency;
        public float MacroExponentialFalloff;
        public float ShelfRunMeters;
        public float ShelfTargetSlopeDegrees;
        public float TrenchDepthMeters;
        public float TrenchWidthMeters;
        public float TrenchSharpness;
        public float IslandCenterRadiusMeters;
        public float IslandJunctionThreshold;
        public uint Seed;
    }

    private struct RidgeData
    {
        public float RidgeMask;
        public float EdgeMask;
        public float JunctionMask;
        public float IslandMask;
        public float TrenchMask;
    }

    private struct AuditSample
    {
        public Double2 Position;
        public float HeightMeters;
        public float NeighborHeightXMeters;
        public float NeighborHeightZMeters;
        public float SlopeAngleDegrees;
        public byte Flags;
    }

    private struct SampleReduction
    {
        public int InvalidSampleCount;
        public int CliffSampleCount;
        public int PlateauSampleCount;
        public float MinHeightMeters;
        public float MaxHeightMeters;
        public float MaxSlopeDegrees;
        public float SlopeAngleSumDegrees;
        public float ActiveSlopeAngleSumDegrees;
        public float ActiveSlopeMinDegrees;
        public float ActiveSlopeMaxDegrees;
        public int Slope30SampleCount;
        public int ActiveSlopeSampleCount;
    }

    private struct SmokeSummary
    {
        public int SampleCount;
        public int InvalidSampleCount;
        public int CliffSampleCount;
        public int PlateauSampleCount;
        public float MinHeightMeters;
        public float MaxHeightMeters;
        public float MaxSlopeDegrees;
        public float AverageSlopeDegrees;
        public float AverageActiveSlopeDegrees;
        public float ActiveSlopeMinDegrees;
        public float ActiveSlopeMaxDegrees;
        public int ActiveSlopeSampleCount;
        public int Slope30SampleCount;
        public float AupDeterminismDeltaMeters;
        public float AupBoundaryDeltaMeters;
        public int OriginChunkInvalidSampleCount;
        public int FarChunkInvalidSampleCount;
        public float HighChunkAupDeltaMeters;
        public bool Passed;
    }

    private readonly struct Int2
    {
        public readonly int X;
        public readonly int Y;

        public Int2(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
    }

    private readonly struct Float2
    {
        public readonly float X;
        public readonly float Y;

        public Float2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static Float2 operator +(Float2 a, Float2 b) => new Float2(a.X + b.X, a.Y + b.Y);
        public static Float2 operator +(Float2 a, float b) => new Float2(a.X + b, a.Y + b);
        public static Float2 operator -(Float2 a, Float2 b) => new Float2(a.X - b.X, a.Y - b.Y);
        public static Float2 operator -(Float2 a, float b) => new Float2(a.X - b, a.Y - b);
        public static Float2 operator *(Float2 a, Float2 b) => new Float2(a.X * b.X, a.Y * b.Y);
        public static Float2 operator *(Float2 a, float b) => new Float2(a.X * b, a.Y * b);
        public static Float2 operator *(float a, Float2 b) => new Float2(a * b.X, a * b.Y);
    }

    private readonly struct AupPosition
    {
        public readonly long GridX;
        public readonly long GridZ;
        public readonly float LocalX;
        public readonly float LocalZ;

        public AupPosition(long gridX, long gridZ, float localX, float localZ)
        {
            GridX = gridX;
            GridZ = gridZ;
            LocalX = localX;
            LocalZ = localZ;
        }
    }

    private readonly struct Double2
    {
        public readonly double X;
        public readonly double Y;

        public Double2(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static Double2 operator +(Double2 a, Double2 b) => new Double2(a.X + b.X, a.Y + b.Y);
        public static Double2 operator -(Double2 a, Double2 b) => new Double2(a.X - b.X, a.Y - b.Y);
        public static Double2 operator /(Double2 a, double b) => new Double2(a.X / b, a.Y / b);
    }
}
