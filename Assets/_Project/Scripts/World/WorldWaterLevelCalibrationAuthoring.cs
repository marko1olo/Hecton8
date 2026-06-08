using System.Runtime.InteropServices;
using Hecton8.Physics;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [System.Flags]
    public enum WorldWaterLevelCalibrationFlags : uint
    {
        None = 0u,
        Valid = 1u << 0,
        AppliedToCrestRoot = 1u << 1,
        UsedFallback = 1u << 2,
        MissingTargetRoot = 1u << 3
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

    public static class WorldWaterLevelCalibrationMath
    {
        public const int DtoBytes = 32;
        public const float DefaultWaterLevelY = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;
        public const float DefaultCalibrationTravelMeters = 512f;
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

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/World/World Water Level Calibration")]
    public sealed class WorldWaterLevelCalibrationAuthoring : MonoBehaviour
    {
        private const string DefaultCalibrationArtifact =
            "Docs/GeneratedAssets/Terrain/MacroGeology/WorldWaterLevelCalibration_Extent30000m_Res192.json";

        [Header("Source")]
        [SerializeField] private int authoringSeed = 880031;
        [SerializeField] private int runtimeSeed;
        [SerializeField] private string calibrationArtifactRelativePath = DefaultCalibrationArtifact;

        [Header("Water Level")]
        [SerializeField] private float calibratedWaterLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [SerializeField] private float fallbackWaterLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [SerializeField, Min(WorldWaterLevelCalibrationMath.MinimumCalibrationTravelMeters)]
        private float calibrationTravelMeters = WorldWaterLevelCalibrationMath.DefaultCalibrationTravelMeters;

        [Header("Crest Target")]
        [SerializeField] private global::Crest.OceanRenderer oceanRenderer;
        [SerializeField] private Transform crestRootOverride;
        [SerializeField] private bool applyOnEnable = true;
        [SerializeField] private bool applyInEditMode = true;

        [Header("Debug")]
        [SerializeField] private float _debugResolvedWaterLevelY = WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        [SerializeField] private uint _debugCalibrationFlags;

        public float ResolvedWaterLevelY
        {
            get
            {
                WorldWaterLevelCalibrationDTO snapshot = BuildSnapshot();
                return snapshot.ResolvedWaterLevelY;
            }
        }

        public WorldWaterLevelCalibrationDTO LastSnapshot => BuildSnapshot();

        public void SetCalibratedWaterLevelY(float waterLevelY)
        {
            calibratedWaterLevelY = waterLevelY;
            TryApplyWaterLevel();
        }

        public bool TryApplyWaterLevel()
        {
            if (!Application.isPlaying && !applyInEditMode)
                return false;

            WorldWaterLevelCalibrationDTO snapshot = BuildSnapshot();
            Transform targetRoot = ResolveTargetRoot();
            if (targetRoot == null)
            {
                snapshot.Flags |= (uint)WorldWaterLevelCalibrationFlags.MissingTargetRoot;
                PublishDebugSnapshot(in snapshot);
                return false;
            }

            Vector3 rootPosition = targetRoot.position;
            float resolvedWaterLevelY = snapshot.ResolvedWaterLevelY;
            if (!Mathf.Approximately(rootPosition.y, resolvedWaterLevelY))
                targetRoot.position = new Vector3(rootPosition.x, resolvedWaterLevelY, rootPosition.z);

            snapshot.Flags |= (uint)WorldWaterLevelCalibrationFlags.AppliedToCrestRoot;
            PublishDebugSnapshot(in snapshot);
            return (snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.UsedFallback) == 0u;
        }

        private void Awake()
        {
            BindLocalCrestIfMissing();
        }

        private void OnEnable()
        {
            BindLocalCrestIfMissing();
            if (applyOnEnable)
                TryApplyWaterLevel();
        }

        private void OnValidate()
        {
            calibratedWaterLevelY = SanitizeSerializedWaterLevel(calibratedWaterLevelY);
            fallbackWaterLevelY = WorldWaterLevelCalibrationMath.ResolveFallbackWaterLevelY(fallbackWaterLevelY);
            calibrationTravelMeters = WorldWaterLevelCalibrationMath.ResolveCalibrationTravelMeters(calibrationTravelMeters);
            BindLocalCrestIfMissing();
            if (applyInEditMode)
                TryApplyWaterLevel();
        }

        private WorldWaterLevelCalibrationDTO BuildSnapshot()
        {
            return WorldWaterLevelCalibrationMath.BuildSnapshot(
                calibratedWaterLevelY,
                fallbackWaterLevelY,
                calibrationTravelMeters,
                unchecked((uint)authoringSeed),
                unchecked((uint)runtimeSeed),
                WorldWaterLevelCalibrationMath.ComputeSourceHash(calibrationArtifactRelativePath));
        }

        private Transform ResolveTargetRoot()
        {
            if (oceanRenderer != null && oceanRenderer.Root != null)
                return oceanRenderer.Root;

            if (crestRootOverride != null)
                return crestRootOverride;

            return transform;
        }

        private void BindLocalCrestIfMissing()
        {
            if (oceanRenderer == null)
                TryGetComponent(out oceanRenderer);
        }

        private void PublishDebugSnapshot(in WorldWaterLevelCalibrationDTO snapshot)
        {
            _debugResolvedWaterLevelY = snapshot.ResolvedWaterLevelY;
            _debugCalibrationFlags = snapshot.Flags;
        }

        private static float SanitizeSerializedWaterLevel(float waterLevelY)
        {
            return math.isfinite(waterLevelY) &&
                   math.abs(waterLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY
                ? waterLevelY
                : WorldWaterLevelCalibrationMath.DefaultWaterLevelY;
        }
    }
}
