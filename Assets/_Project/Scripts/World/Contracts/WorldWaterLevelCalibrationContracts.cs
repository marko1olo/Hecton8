using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.World
{
    [System.Flags]
    public enum WorldWaterLevelCalibrationFlags : uint
    {
        None = 0u,
        Valid = 1u << 0,
        AppliedToCrestRoot = 1u << 1,
        UsedFallback = 1u << 2,
        MissingTargetRoot = 1u << 3,
        DuplicateOwner = 1u << 4
    }

    [StructLayout(LayoutKind.Explicit, Size = WorldWaterLevelCalibrationMath.DtoBytes)]
    public struct WorldWaterLevelCalibrationDTO
    {
        [FieldOffset(0)] public float RequestedWaterLevelY;
        [FieldOffset(4)] public float ResolvedWaterLevelY;
        [FieldOffset(8)] public float FallbackWaterLevelY;
        [FieldOffset(12)] public float CalibrationTravelMeters;
        [FieldOffset(16)] public uint AuthoringSeed;
        [FieldOffset(20)] public uint RuntimeSeed;
        [FieldOffset(24)] public uint SourceHash;
        [FieldOffset(28)] public uint Flags;
    }

    public interface IWorldWaterLevelCalibrationReadModel
    {
        bool TryGetWaterLevelCalibrationSnapshot(out WorldWaterLevelCalibrationDTO snapshot);
    }

    public interface IWorldWaterLevelCalibrationWriteModel : IWorldWaterLevelCalibrationReadModel
    {
        bool TryApplyWaterLevelCalibration(float waterLevelY, float calibrationTravelMeters, uint sourceHash);
    }

    public static class WorldWaterLevelCalibrationRuntimeRegistry
    {
        private const int MaxTrackedReadModels = 8;
        private static readonly IWorldWaterLevelCalibrationReadModel[] s_readModels =
            new IWorldWaterLevelCalibrationReadModel[MaxTrackedReadModels];
        private static int s_readModelCount;
        private static uint s_untrackedDuplicateOwnerCount;

        public static uint DuplicateOwnerCount
        {
            get
            {
                uint trackedDuplicates = s_readModelCount > 1 ? (uint)(s_readModelCount - 1) : 0u;
                return trackedDuplicates + s_untrackedDuplicateOwnerCount;
            }
        }

        public static void Register(IWorldWaterLevelCalibrationReadModel readModel)
        {
            if (readModel == null)
                return;

            for (int i = 0; i < s_readModelCount; i++)
            {
                if (object.ReferenceEquals(s_readModels[i], readModel))
                    return;
            }

            if (s_readModelCount < MaxTrackedReadModels)
            {
                s_readModels[s_readModelCount] = readModel;
                s_readModelCount++;
                return;
            }

            if (s_untrackedDuplicateOwnerCount < uint.MaxValue)
                s_untrackedDuplicateOwnerCount++;
        }

        public static void Unregister(IWorldWaterLevelCalibrationReadModel readModel)
        {
            if (readModel == null)
                return;

            for (int i = 0; i < s_readModelCount; i++)
            {
                if (!object.ReferenceEquals(s_readModels[i], readModel))
                    continue;

                for (int j = i + 1; j < s_readModelCount; j++)
                    s_readModels[j - 1] = s_readModels[j];

                s_readModelCount--;
                s_readModels[s_readModelCount] = null;
                return;
            }

            if (s_untrackedDuplicateOwnerCount != 0u)
                s_untrackedDuplicateOwnerCount--;
        }

        public static void Reset()
        {
            for (int i = 0; i < s_readModelCount; i++)
                s_readModels[i] = null;

            s_readModelCount = 0;
            s_untrackedDuplicateOwnerCount = 0u;
        }

        public static bool TryGetActiveSnapshot(out WorldWaterLevelCalibrationDTO snapshot)
        {
            for (int i = 0; i < s_readModelCount; i++)
            {
                IWorldWaterLevelCalibrationReadModel readModel = s_readModels[i];
                if (readModel == null)
                    continue;

                if (!readModel.TryGetWaterLevelCalibrationSnapshot(out snapshot) ||
                    (snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.Valid) == 0u)
                {
                    continue;
                }

                if (DuplicateOwnerCount != 0u)
                    snapshot.Flags |= (uint)WorldWaterLevelCalibrationFlags.DuplicateOwner;

                return true;
            }

            snapshot = default;
            return false;
        }

        public static bool TryApplySavedCalibration(
            float waterLevelY,
            float calibrationTravelMeters,
            uint sourceHash)
        {
            if (!WorldWaterLevelCalibrationMath.TryResolveWaterLevelY(
                    waterLevelY,
                    WorldWaterLevelCalibrationMath.DefaultWaterLevelY,
                    calibrationTravelMeters,
                    out float resolvedWaterLevelY))
            {
                return false;
            }

            float resolvedTravelMeters =
                WorldWaterLevelCalibrationMath.ResolveCalibrationTravelMeters(calibrationTravelMeters);
            for (int i = 0; i < s_readModelCount; i++)
            {
                if (s_readModels[i] is IWorldWaterLevelCalibrationWriteModel writeModel &&
                    writeModel.TryApplyWaterLevelCalibration(resolvedWaterLevelY, resolvedTravelMeters, sourceHash))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public static class WorldWaterLevelCalibrationMath
    {
        public const int DtoBytes = 32;
        public const float DefaultWaterLevelY = 14.02f;
        public const int DefaultAuthoringSeed = 880031;
        public const float DefaultCalibrationTravelMeters = 1000f;
        public const float MinimumCalibrationTravelMeters = 100f;
        public const float MaximumAbsoluteWaterLevelY = 1000f;

        public static WorldWaterLevelCalibrationDTO BuildSnapshot(
            float requestedWaterLevelY,
            float fallbackWaterLevelY,
            float calibrationTravelMeters,
            uint authoringSeed,
            uint runtimeSeed,
            uint sourceHash)
        {
            WorldWaterLevelCalibrationDTO snapshot = default;
            snapshot.RequestedWaterLevelY = requestedWaterLevelY;
            snapshot.FallbackWaterLevelY = ResolveFallbackWaterLevelY(fallbackWaterLevelY);
            snapshot.CalibrationTravelMeters = ResolveCalibrationTravelMeters(calibrationTravelMeters);
            snapshot.AuthoringSeed = authoringSeed;
            snapshot.RuntimeSeed = runtimeSeed;
            snapshot.SourceHash = sourceHash;

            if (TryResolveWaterLevelY(
                    requestedWaterLevelY,
                    snapshot.FallbackWaterLevelY,
                    snapshot.CalibrationTravelMeters,
                    out float resolvedWaterLevelY))
            {
                snapshot.ResolvedWaterLevelY = resolvedWaterLevelY;
                snapshot.Flags = (uint)WorldWaterLevelCalibrationFlags.Valid;
            }
            else
            {
                snapshot.ResolvedWaterLevelY = snapshot.FallbackWaterLevelY;
                snapshot.Flags = (uint)(WorldWaterLevelCalibrationFlags.Valid | WorldWaterLevelCalibrationFlags.UsedFallback);
            }

            return snapshot;
        }

        public static bool TryResolveWaterLevelY(
            float requestedWaterLevelY,
            float fallbackWaterLevelY,
            float calibrationTravelMeters,
            out float resolvedWaterLevelY)
        {
            float fallback = ResolveFallbackWaterLevelY(fallbackWaterLevelY);
            float travel = ResolveCalibrationTravelMeters(calibrationTravelMeters);
            if (math.isfinite(requestedWaterLevelY) &&
                math.abs(requestedWaterLevelY) <= MaximumAbsoluteWaterLevelY &&
                math.abs(requestedWaterLevelY - fallback) <= travel)
            {
                resolvedWaterLevelY = requestedWaterLevelY;
                return true;
            }

            resolvedWaterLevelY = fallback;
            return false;
        }

        public static float ResolveFallbackWaterLevelY(float fallbackWaterLevelY)
        {
            return math.isfinite(fallbackWaterLevelY) &&
                   math.abs(fallbackWaterLevelY) > 0.0001f &&
                   math.abs(fallbackWaterLevelY) <= MaximumAbsoluteWaterLevelY
                ? fallbackWaterLevelY
                : DefaultWaterLevelY;
        }

        public static float ResolveCalibrationTravelMeters(float calibrationTravelMeters)
        {
            if (!math.isfinite(calibrationTravelMeters))
                return DefaultCalibrationTravelMeters;

            return math.clamp(
                math.abs(calibrationTravelMeters),
                MinimumCalibrationTravelMeters,
                MaximumAbsoluteWaterLevelY);
        }

        public static uint ComputeSourceHash(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 0u;

            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }

            return math.select(1u, hash, hash != 0u);
        }
    }
}
