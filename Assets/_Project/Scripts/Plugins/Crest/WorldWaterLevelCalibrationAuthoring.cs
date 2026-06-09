using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Ocean/Crest World Water Level Calibration")]
    public sealed class WorldWaterLevelCalibrationAuthoring : MonoBehaviour, IWorldWaterLevelCalibrationWriteModel
    {
        private const string DefaultCalibrationArtifact =
            "Docs/GeneratedAssets/Terrain/MacroGeology/WorldWaterLevelCalibration_Extent30000m_Res192.json";

        [Header("Source")]
        [SerializeField] private int authoringSeed = WorldWaterLevelCalibrationMath.DefaultAuthoringSeed;
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

        public bool TryGetWaterLevelCalibrationSnapshot(out WorldWaterLevelCalibrationDTO snapshot)
        {
            snapshot = BuildSnapshot();
            return (snapshot.Flags & (uint)WorldWaterLevelCalibrationFlags.Valid) != 0u;
        }

        public void SetCalibratedWaterLevelY(float waterLevelY)
        {
            calibratedWaterLevelY = SanitizeSerializedWaterLevel(waterLevelY);
            TryApplyWaterLevel();
        }

        public bool TryApplyWaterLevelCalibration(float waterLevelY, float calibrationTravelMeters, uint sourceHash)
        {
            uint localSourceHash = WorldWaterLevelCalibrationMath.ComputeSourceHash(calibrationArtifactRelativePath);
            if (sourceHash != 0u && localSourceHash != 0u && sourceHash != localSourceHash)
                return false;

            if (!WorldWaterLevelCalibrationMath.TryResolveWaterLevelY(
                    waterLevelY,
                    fallbackWaterLevelY,
                    calibrationTravelMeters,
                    out float resolvedWaterLevelY))
            {
                return false;
            }

            calibratedWaterLevelY = resolvedWaterLevelY;
            this.calibrationTravelMeters =
                WorldWaterLevelCalibrationMath.ResolveCalibrationTravelMeters(calibrationTravelMeters);
            return TryApplyWaterLevel();
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
            WorldWaterLevelCalibrationRuntimeRegistry.Register(this);
            if (applyOnEnable)
                TryApplyWaterLevel();
        }

        private void OnDisable()
        {
            WorldWaterLevelCalibrationRuntimeRegistry.Unregister(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntimeRegistry()
        {
            WorldWaterLevelCalibrationRuntimeRegistry.Reset();
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
