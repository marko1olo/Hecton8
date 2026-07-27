using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Fluids;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core;
using Hecton8.Optimization;
using Hecton8.VFX;
using Hecton8.VFX.Wakes;
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Environment
{
    /// <summary>
    /// GPU-resident camera-local marine snow renderer driven by the authoritative ecosystem flow field.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonMarineSnowRenderer : MonoBehaviour,
        ILateFrameTickable,
        ISlowTickable,
        IColdTickable,
        IOriginShiftListener,
        IVehicleCommandSignalListener,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private static int s_x001HectonMarineSnowRendererSignalPushDropCount;
        private const float BiolumeSurgeDurationSeconds = 4f;
        private const int DefaultClearKernelTileSize = 8;
        private const int MaxParticleDispatchGroupsPerCall = 512;
        private const int PortableMaxComputeThreadsPerGroup = 256;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const int MinimumMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount;
        private const int OverkillMarineSnowParticleCapacity = VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount;
        private const int MaxMarineSnowParticleCapacity = OverkillMarineSnowParticleCapacity;
#if UNITY_EDITOR
        private const string DefaultEmptyCaveSdfTexturePath1728 = "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyCaveSdf_1x1x1.asset";
        private const string DefaultEmptyAbyssalFlowTexturePath1728 = "Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset";
#endif
        private const int ParticleDataStride = 32;
        private const int ParticleRenderMetaStride = 32;
        private const int ProceduralIndirectArgsStride = 16;
        private const int DynamicWakeDtoStride = 32;
        private const int PropwashEventStride = PropwashGpuContracts.EventStrideBytes;
        private const int PropwashRingCursorStride = PropwashGpuContracts.RingCursorStrideBytes;
        private const int PropwashTelemetryEntryStride = PropwashGpuContracts.TelemetryEntryStrideBytes;
        private const int PropwashTuningStride = PropwashGpuContracts.TuningStrideBytes;
        private const int PropwashWakeProfileStride = PropwashGpuContracts.WakeProfileStrideBytes;
        private const int PropwashEventMinSampleCapacity = 4;
        private const int SiltConfigurationStride = 32;
        private const int FrameConstantsStride = 128;
        private const int VehicleWakeJobResultStride = 48;
        private const int TelemetryCapacity = 300;
        private const int TelemetryEntrySizeBytes = 64;
        private const int MockWakeCapacity = 4;
        private const int DynamicWakeDtoCapacity = 16;
        private const int ProceduralWakeSourceBridgeCapacity = 16;
        private const int ProceduralWakeSourceBridgeMinWrites = 4;
        private const int DefaultEcosystemFlowFieldResolution = 201;
        private const int DefaultEcosystemFlowFieldBufferCapacity = DefaultEcosystemFlowFieldResolution * DefaultEcosystemFlowFieldResolution;
        private const int MaxEcosystemFlowFieldBufferCapacity = 262144;
        private const int PropwashEventRingCapacity = PropwashGpuContracts.EventRingCapacity;
        private const int PropwashMockEventCount = PropwashGpuContracts.MockEventCount;
        private const int PropwashWakeProfileCapacity = PropwashGpuContracts.WakeProfileCapacity;
        private const int CsvProfileReadBufferBytes = 4096;
        private const int TelemetryPublishFrameCadence = 30;
        private const float VehicleWakeThrottleDeadZone = 0.05f;
        private const float VehicleWakePublishCooldownSeconds = 0.08f;
        private const float MockWakeUploadIntervalSeconds = 0.125f;
        private const float MockAcousticPulseIntervalSeconds = 8f;
        private const float MockAcousticPulseRadius = 18f;
        private const float MockAcousticPulseMagnitude = 1.35f;
        private const float MockAcousticPulseDuration = 1.45f;
        private const float MockAcousticPulseSpeed = 24f;
        private const float CsvProfilePollIntervalSeconds = 0.5f;
        private const int CsvProfilePollSliceMilliseconds = 50;
        private const int CsvProfileThreadJoinTimeoutMilliseconds = CsvProfilePollSliceMilliseconds + 10;
        private const float InvTau = 0.15915494f;
        private const float ActiveDensityEpsilon = 0.0001f;
        private const float ShaderVectorPublishEpsilon = 0.0001f;
        private const float ExternalGpuBindingColdTickSeconds = 0.1f;
        private const float FogDensityEncodedScale = 65535f;
        private const float FogDensityParticleSizeGain = 128f;
        private const uint MarineSnowTelemetryContextHash = 0x4D534E57u;
        private const uint DispatchedParticleCountTelemetryHash = 0x44504354u;
        private const uint VehicleWakeSourceHash = 0x5653574Bu;
        private const SystemID VaultOwnerSystem = SystemID.Vfx;
        private const string SiltProfileCsvFileName = "vfx_silt_profiles.csv";
        private const string WakeProfileCsvFileName = "vehicle_wake_profiles.csv";
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SILT_VFX.h8dump";
        private const string LegacyBlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_SILT_VFX.bin";
        private static readonly Vector4 DepthCollisionParams = new Vector4(15f, 0.25f, 0.5f, 0f);
        private static readonly Vector4 DefaultFlowSynchronyParams = new Vector4(1f, 0.26f, 0f, 0f);
        private static readonly Vector4 DisabledTerrainHeightScale = new Vector4(0f, 0f, 0f, 0f);
        private static readonly Vector4 DefaultPropwashParams = new Vector4(2f, 0.08f, 0.025f, 1f);
        private static readonly Vector4 MinimumScalabilityParams = new Vector4(0f, 15f, 0f, 0f);
        private static readonly Vector4 InvalidVector = new Vector4(float.NaN, float.NaN, float.NaN, float.NaN);
        private static readonly Matrix4x4 IdentityMatrix = Matrix4x4.identity;
        private static readonly Matrix4x4 InvalidMatrix = new Matrix4x4(InvalidVector, InvalidVector, InvalidVector, InvalidVector);
        [StructLayout(LayoutKind.Explicit, Size = FrameConstantsStride)]
        private struct FrameConstantsData
        {
            [FieldOffset(0)]
            public Vector4 CameraPositionTime;

            [FieldOffset(16)]
            public Vector4 CameraRightDeltaTime;

            [FieldOffset(32)]
            public Vector4 CameraUpDensity;

            [FieldOffset(48)]
            public Vector4 FlowFieldCenterCellSize;

            [FieldOffset(64)]
            public Vector4 ShellParams;

            [FieldOffset(80)]
            public Vector4 MetaParams;

            [FieldOffset(96)]
            public Vector4 CameraVelocityStretch;

            [FieldOffset(112)]
            public Vector4 Pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = VehicleWakeJobResultStride)]
        private struct VehicleWakeJobResult
        {
            [FieldOffset(0)]
            public float3 PositionWS;

            [FieldOffset(12)]
            public float Radius;

            [FieldOffset(16)]
            public float3 VectorWS;

            [FieldOffset(28)]
            public float Lifetime;

            [FieldOffset(32)]
            public float Intensity;

            [FieldOffset(36)]
            public uint Flags;

            [FieldOffset(40)]
            public uint Pad0;

            [FieldOffset(44)]
            public uint Pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = TelemetryEntrySizeBytes)]
        private struct MarineSnowTelemetryEntry
        {
            [FieldOffset(0)]
            public int Frame;

            [FieldOffset(4)]
            public int DispatchedParticleCount;

            [FieldOffset(8)]
            public int Capacity;

            [FieldOffset(12)]
            public int DynamicWakeCount;

            [FieldOffset(16)]
            public float Throttle;

            [FieldOffset(20)]
            public float SystemStress01;

            [FieldOffset(24)]
            public float MaxSiltSpeed;

            [FieldOffset(28)]
            public float AupShiftSq;

            [FieldOffset(32)]
            public Vector3 CameraPositionWS;

            [FieldOffset(44)]
            public float HeadlightBoost;

            [FieldOffset(48)]
            public uint Flags;

            [FieldOffset(52)]
            public uint StateHash;

            [FieldOffset(56)]
            public int MockGpuMicroseconds;

            [FieldOffset(60)]
            public uint CommandSequence;
        }

        private static class ShaderIds
        {
            internal static readonly int ParticlesReadId = Shader.PropertyToID("_MarineSnowParticlesRead");
            internal static readonly int ParticlesWriteId = Shader.PropertyToID("_MarineSnowParticlesWrite");
            internal static readonly int ParticlesRenderId = Shader.PropertyToID("_MarineSnowParticles");
            internal static readonly int ParticleMetaReadId = Shader.PropertyToID("_MarineSnowParticleMetaRead");
            internal static readonly int ParticleMetaWriteId = Shader.PropertyToID("_MarineSnowParticleMetaWrite");
            internal static readonly int ParticleMetaRenderId = Shader.PropertyToID("_MarineSnowParticleMeta");
            internal static readonly int VisibleParticleIndicesId = Shader.PropertyToID("_MarineSnowVisibleParticleIndices");
            internal static readonly int IndirectArgsId = Shader.PropertyToID("_MarineSnowIndirectArgs");
            internal static readonly int FlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
            internal static readonly int AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
            internal static readonly int AbyssalFlowFieldTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
            internal static readonly int AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
            internal static readonly int AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
            internal static readonly int AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
            internal static readonly int AbyssalFlowTextureParamsId = Shader.PropertyToID("_AbyssalFlowTextureParams");
            internal static readonly int AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
            internal static readonly int DynamicWakesId = Shader.PropertyToID("_DynamicWakes");
            internal static readonly int DynamicWakeVectorsId = Shader.PropertyToID("_DynamicWakeVectors");
            internal static readonly int DynamicWakeDtosId = Shader.PropertyToID("_DynamicWakeDTOs");
            internal static readonly int DynamicWakeParamsId = Shader.PropertyToID("_DynamicWakeParams");
            internal static readonly int DynamicWakeDtoParamsId = Shader.PropertyToID("_DynamicWakeDtoParams");
            internal static readonly int MockAcousticPulseId = Shader.PropertyToID("_MarineSnowMockAcousticPulse");
            internal static readonly int MockAcousticParamsId = Shader.PropertyToID("_MarineSnowMockAcousticParams");
            internal static readonly int MaelstromsId = Shader.PropertyToID("_MarineSnowMaelstroms");
            internal static readonly int MaelstromParamsId = Shader.PropertyToID("_MarineSnowMaelstromParams");
            internal static readonly int FrameConstantsId = Shader.PropertyToID("_HectonMarineSnowFrame");
            internal static readonly int DriftParamsId = Shader.PropertyToID("_MarineSnowDriftParams");
            internal static readonly int FlowParamsId = Shader.PropertyToID("_MarineSnowFlowParams");
            internal static readonly int MockFlowFieldId = Shader.PropertyToID("_MarineSnowMockFlowField");
            internal static readonly int VelocityParamsId = Shader.PropertyToID("_MarineSnowVelocityParams");
            internal static readonly int InitializationParamsId = Shader.PropertyToID("_MarineSnowInitializationParams");
            internal static readonly int DispatchOffsetId = Shader.PropertyToID("_MarineSnowDispatchOffset");
            internal static readonly int DispatchTileOffsetId = Shader.PropertyToID("_MarineSnowDispatchTileOffset");
            internal static readonly int BubbleParamsId = Shader.PropertyToID("_MarineSnowBubbleParams");
            internal static readonly int TerrainHeightTextureId = Shader.PropertyToID("_MarineSnowTerrainHeightTexture");
            internal static readonly int TerrainHeightRectId = Shader.PropertyToID("_MarineSnowTerrainHeightRect");
            internal static readonly int TerrainHeightScaleId = Shader.PropertyToID("_MarineSnowTerrainHeightScale");
            internal static readonly int PropwashParamsId = Shader.PropertyToID("_MarineSnowPropwashParams");
            internal static readonly int PropwashEventsId = Shader.PropertyToID("_PropwashEvents");
            internal static readonly int PropwashEventParamsId = Shader.PropertyToID("_PropwashEventParams");
            internal static readonly int PropwashBiomeTintId = Shader.PropertyToID("_PropwashBiomeTint");
            internal static readonly int ScalabilityParamsId = Shader.PropertyToID("_MarineSnowScalabilityParams");
            internal static readonly int FlowSynchronyParamsId = Shader.PropertyToID("_HectonFlowSynchronyParams");
            internal static readonly int RenderParamsId = Shader.PropertyToID("_MarineSnowRenderParams");
            internal static readonly int TintId = Shader.PropertyToID("_MarineSnowTint");
            internal static readonly int MaskAtlasId = Shader.PropertyToID("_MarineSnowMaskAtlas");
            internal static readonly int NormalAtlasId = Shader.PropertyToID("_MarineSnowNormalAtlas");
            internal static readonly int AtlasParamsId = Shader.PropertyToID("_MarineSnowAtlasParams");
            internal static readonly int FlipbookParamsId = Shader.PropertyToID("_MarineSnowFlipbookParams");
            internal static readonly int EmissionParamsId = Shader.PropertyToID("_MarineSnowEmissionParams");
            internal static readonly int ViewProjectionId = Shader.PropertyToID("_MarineSnowViewProjection");
            internal static readonly int ViewMatrixId = Shader.PropertyToID("_MarineSnowViewMatrix");
            internal static readonly int CaveVoxelSdfTexId = Shader.PropertyToID("_HectonCaveVoxelSdfTex");
            internal static readonly int CaveVoxelActiveId = Shader.PropertyToID("_HectonCaveVoxelActive");
            internal static readonly int CaveVoxelWorldToLocalId = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
            internal static readonly int CaveVoxelHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelHalfExtents");
            internal static readonly int CaveVoxelInvDoubleHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelInvDoubleHalfExtents");
            internal static readonly int SubmarineWashSphereId = Shader.PropertyToID("_HectonSubmarineWashSphere");
            internal static readonly int SubmarineWashVelocityId = Shader.PropertyToID("_HectonSubmarineWashVelocity");
            internal static readonly int FloatingOriginOffsetId = Shader.PropertyToID("_HectonFloatingOriginOffset");
            internal static readonly int AupShiftOffsetId = Shader.PropertyToID("_AupShiftOffset");
            internal static readonly int FlashlightPositionWSId = Shader.PropertyToID("_HectonFlashlightPositionWS");
            internal static readonly int FlashlightDirectionWSId = Shader.PropertyToID("_HectonFlashlightDirectionWS");
            internal static readonly int FlashlightColorId = Shader.PropertyToID("_HectonFlashlightColor");
            internal static readonly int FlashlightConeDataId = Shader.PropertyToID("_HectonFlashlightConeData");
            internal static readonly int FlashlightActiveId = Shader.PropertyToID("_HectonFlashlightActive");
            internal static readonly int ZBufferParamsId = Shader.PropertyToID("_MarineSnowZBufferParams");
            internal static readonly int DepthTextureTexelSizeId = Shader.PropertyToID("_MarineSnowDepthTextureTexelSize");
            internal static readonly int DepthCollisionParamsId = Shader.PropertyToID("_MarineSnowDepthCollisionParams");
            internal static readonly int CameraDepthTextureId = Shader.PropertyToID("_CameraDepthTexture");
            internal static readonly int GlobalZBufferParamsId = Shader.PropertyToID("_ZBufferParams");
            internal static readonly int SonarGlowTextureId = Shader.PropertyToID("_HectonMarineSnowSonarGlowTex");
            internal static readonly int SonarGlowResultId = Shader.PropertyToID("_HectonMarineSnowSonarGlowResult");
            internal static readonly int SonarGlowTexelSizeId = Shader.PropertyToID("_HectonMarineSnowSonarGlowTexelSize");
            internal static readonly int SonarGlowParamsId = Shader.PropertyToID("_HectonMarineSnowSonarGlowParams");
            internal static readonly int FogDensityTextureId = Shader.PropertyToID("_HectonMarineSnowFogDensityTex");
            internal static readonly int FogDensityResultId = Shader.PropertyToID("_HectonMarineSnowFogDensityResult");
            internal static readonly int FogDensityTexelSizeId = Shader.PropertyToID("_HectonMarineSnowFogDensityTexelSize");
            internal static readonly int FogDensityParamsId = Shader.PropertyToID("_HectonMarineSnowFogDensityParams");
            internal static readonly int SonarRevealExpireTimeId = Shader.PropertyToID("_SonarRevealExpireTime");
        }

        [Header("References")]
        [Tooltip("Camera transform that owns the marine snow shell. Bind this to the runtime main camera.")]
        [SerializeField] private Transform targetCamera;
        [Tooltip("Compute shader responsible for marine-snow simulation.")]
        [SerializeField] private ComputeShader marineSnowCompute;
        [Tooltip("Dedicated material used by the direct marine-snow billboard draw.")]
        [SerializeField] private Material marineSnowMaterial;

        [Tooltip("Optional fluid emission profile that overrides drag, buoyancy, and turbulence coefficients per particle class.")]
        [SerializeField] private VFXEmissionProfile emissionProfile;

        [Tooltip("Fluid class emitted by this GPU particle owner.")]
        [SerializeField] private VFXEmissionProfile.FluidType fluidType = VFXEmissionProfile.FluidType.Snow;

        [Header("Population")]
        [Tooltip("Empty safety radius around the camera to avoid particles clipping through the visor.")]
        [SerializeField, Range(0.1f, 4f)] private float innerRadius = 0.8f;
        [Tooltip("Outer shell radius. Particles respawn to the ring when they drift beyond this distance.")]
        [SerializeField, Range(4f, 32f)] private float outerRadius = 18f;
        [Tooltip("Vertical span of the marine-snow shell relative to the target camera.")]
        [SerializeField] private Vector2 verticalSpan = new Vector2(-10f, 8f);

        [Header("Drift")]
        [Tooltip("Minimum base descent speed for marine snow.")]
        [SerializeField, Range(0.005f, 0.08f)] private float descentMinSpeed = 0.015f;
        [Tooltip("Maximum base descent speed for marine snow.")]
        [SerializeField, Range(0.005f, 0.08f)] private float descentMaxSpeed = 0.04f;
        [Tooltip("Horizontal wander amplitude used before anisotropic drag clamps it back into the current.")]
        [SerializeField, Range(0f, 0.02f)] private float wanderStrength = 0.008f;
        [Tooltip("Base drag coefficient for the mandated anisotropic-drag attenuation.")]
        [SerializeField, Range(0.01f, 0.5f)] private float baseDragCoefficient = 0.15f;

        [Header("Flow Coupling")]
        [Tooltip("How strongly particles chase the authoritative ecosystem current before anisotropic drag is applied.")]
        [SerializeField, Range(0f, 1f)] private float flowBlend = 0.18f;
        [Tooltip("Extra flow-coupling gain injected by denser water states.")]
        [SerializeField, Range(0f, 1f)] private float densityBiasFlowGain = 0.08f;
        [Tooltip("How often the CPU is allowed to upload the current flow-field snapshot to the GPU.")]
        [SerializeField, Range(0.05f, 2f)] private float flowFieldUploadInterval = 0.25f;
        [Tooltip("If the flow-field center shifts by more than this many cells, force an upload immediately.")]
        [SerializeField, Range(0.1f, 4f)] private float flowFieldRecenterThresholdCells = 0.5f;
        [Tooltip("Cold GPU upload capacity for the ecosystem flow-field snapshot. Raise in editor if the vegetation grid is larger; never resized in play.")]
        [SerializeField, Min(1f)] private int flowFieldUploadCapacity = DefaultEcosystemFlowFieldBufferCapacity;
        [Tooltip("Authored 1x1x1 neutral SDF volume used when cave SDF is inactive. Runtime Texture3D synthesis is forbidden.")]
        [SerializeField] private Texture3D emptyCaveSdfTexture3D;
        [Tooltip("Authored 1x1x1 clear flow volume used when abyssal flow is inactive. Runtime Texture3D synthesis is forbidden.")]
        [SerializeField] private Texture3D emptyAbyssalFlowTexture3D;

        [Header("Wake Advection")]
        [Tooltip("Maximum particle speed after all wake, curl, and light-cone responses are applied.")]
        [SerializeField, Range(0.1f, 4f)] private float maxSiltSpeed = 1.65f;
        [Tooltip("Runtime radius of throttle-authored wake impulses published into the fluid wake ring.")]
        [SerializeField, Range(0.5f, 24f)] private float vehicleWakeRadius = 9.5f;
        [Tooltip("Lifetime in seconds for throttle-authored wake impulses.")]
        [SerializeField, Range(0.1f, 8f)] private float vehicleWakeLifetime = 2.4f;
        [Tooltip("Meters-per-second multiplier for throttle-authored wake impulse vectors.")]
        [SerializeField, Range(0.01f, 2f)] private float vehicleWakeStrength = 0.18f;
        [Tooltip("Editor/development-only GPU wake proof path used while submarine wake producers are absent or disconnected.")]
        [SerializeField] private bool enableMockWakeSignals;
        [Tooltip("Editor/development-only GPU acoustic pulse proof path used while sonar/acoustic signal producers are absent or disconnected.")]
        [SerializeField] private bool enableMockAcousticSignals;
        [Tooltip("Emission response multiplier for particles inside the active flashlight cone.")]
        [SerializeField, Range(0f, 4f)] private float headlightEmissionMultiplier = 1.65f;

        [Header("Rendering")]
        [Tooltip("Minimum world-space snow billboard size.")]
        [SerializeField, Range(0.0005f, 0.02f)] private float particleSizeMin = 0.0035f;
        [Tooltip("Maximum world-space snow billboard size.")]
        [SerializeField, Range(0.0005f, 0.03f)] private float particleSizeMax = 0.009f;
        [Tooltip("Base tint for the marine-snow quads.")]
        [SerializeField] private Color particleTint = new Color(0.54f, 0.61f, 0.58f, 0.55f);
        [Tooltip("Maximum resolved particle alpha before density scaling.")]
        [SerializeField, Range(0f, 1f)] private float maxAlpha = 0.55f;
        [Tooltip("Softness of the particle radial falloff.")]
        [SerializeField, Range(0.5f, 8f)] private float softness = 3.2f;
        [Tooltip("Distance fade for the camera-local shell.")]
        [SerializeField, Range(4f, 48f)] private float maxViewDistance = 18f;
        [Tooltip("Shadow-casting mode for the marine-snow particle draw.")]
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        [Header("Offline Flipbook Atlas")]
        [Tooltip("Optional packed atlas baked by ProceduralTextureBaker 1718/1728. R=density, G=biolum, B=flow hint, A=AO.")]
        [SerializeField] private Texture2D marineSnowMaskAtlas;
        [Tooltip("Optional normal atlas baked beside the packed mask atlas.")]
        [SerializeField] private Texture2D marineSnowNormalAtlas;
        [Tooltip("Flipbook atlas columns. The 1718/1728 high-quality baker emits an 8x8 atlas.")]
        [SerializeField, Range(1, 16)] private int marineSnowAtlasColumns = 8;
        [Tooltip("Flipbook atlas rows. The 1718/1728 high-quality baker emits an 8x8 atlas.")]
        [SerializeField, Range(1, 16)] private int marineSnowAtlasRows = 8;
        [Tooltip("Per-pixel headlight response from the baked normal atlas.")]
        [SerializeField, Range(0f, 2f)] private float marineSnowNormalAtlasWeight = 0.55f;
        [Tooltip("Blend from radial sprite coverage to baked density coverage.")]
        [SerializeField, Range(0f, 1f)] private float marineSnowMaskAtlasWeight = 1f;
        [Tooltip("Frame cycling rate for the 64-frame flipbook atlas.")]
        [SerializeField, Range(0f, 2f)] private float marineSnowFlipbookTimeScale = 0.18f;
        [Tooltip("Additional phase offset from particle lifetime to avoid lockstep shimmer.")]
        [SerializeField, Range(0f, 1f)] private float marineSnowFlipbookLifePhase = 0.15f;
        [Tooltip("How strongly the baked AO channel dims particulate pixels.")]
        [SerializeField, Range(0f, 1f)] private float marineSnowAtlasAoGain = 0.22f;
        [Tooltip("How strongly the baked biolum channel reacts to the flashlight boost.")]
        [SerializeField, Range(0f, 2f)] private float marineSnowAtlasBiolumGain = 0.35f;

        [Header("Sprint Speed Lines")]
        [Tooltip("Camera/player velocity where marine snow stretches into full sprint speed lines.")]
        [SerializeField, Range(1f, 18f)] private float speedLineFullVelocity = 8f;
        [Tooltip("Maximum billboard elongation applied to plankton at full sprint velocity.")]
        [SerializeField, Range(1f, 18f)] private float speedLineMaxStretch = 7.5f;
        [Tooltip("Blend sharpness for speed-line stretch so brief frame spikes do not flash the whole shell.")]
        [SerializeField, Range(0.1f, 16f)] private float speedLineResponseSharpness = 7f;

        [Header("Biolume Surge")]
        [Tooltip("Temporary particle-population multiplier applied while the global biolume surge bit remains active.")]
        [SerializeField, Range(1f, 3f)] private float biolumeSurgeParticleMultiplier = 1.75f;
        [Tooltip("Temporary turbulence multiplier applied while the global biolume surge bit remains active.")]
        [SerializeField, Range(1f, 4f)] private float biolumeSurgeTurbulenceMultiplier = 2f;

        [Header("Sonar Glow")]
        [Tooltip("Screen-space render-scale used by the low-resolution sonar-reactive plankton glow splatmap.")]
        [SerializeField, Range(0.1f, 0.75f)] private float sonarGlowRenderScale = 0.35f;
        [Tooltip("Simulation-side intensity scale used when particles intersect the active sonar pulse.")]
        [SerializeField, Range(0f, 8f)] private float sonarGlowIntensity = 2.2f;
        [Tooltip("Final underwater composite strength for sonar-reactive plankton glow.")]
        [SerializeField, Range(0f, 4f)] private float sonarGlowCompositeStrength = 1.15f;

        [Header("Fog Injection")]
        [Tooltip("Low-resolution noir fog density contributed by visible marine-snow particles.")]
        [SerializeField, Range(0f, 0.5f)] private float fogDensityInjectionStrength = 0.10f;
        [Tooltip("Render scale for the marine-snow fog density buffer.")]
        [SerializeField, Range(0.1f, 0.5f)] private float fogDensityRenderScale = 0.25f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<MarineSnowTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<VfxConfigurationDTO> _siltTuningHandle;
        private VaultGenerationHandle<DynamicWakeDTO> _dynamicWakeDtoHandle;
        private VaultGenerationHandle<MockFlowField> _mockFlowFieldHandle;
        private VaultGenerationHandle<WakeSource> _proceduralWakeSourcesHandle;
        private VaultGenerationHandle<PropwashEventDTO> _propwashEventHandle;
        private VaultGenerationHandle<PropwashRingCursorDTO> _propwashCursorHandle;
        private VaultGenerationHandle<PropwashTelemetryEntry> _propwashTelemetryHandle;
        private VaultGenerationHandle<PropwashGpuTuningDTO> _propwashTuningHandle;
        private VaultGenerationHandle<PropwashWakeProfileDTO> _propwashWakeProfileHandle;
        private GraphicsBuffer _particleBufferA;
        private GraphicsBuffer _particleBufferB;
        private GraphicsBuffer _particleMetaBufferA;
        private GraphicsBuffer _particleMetaBufferB;
        private GraphicsBuffer _flowFieldBuffer;
        private GraphicsBuffer _emptyFlowFieldBuffer;
        private GraphicsBuffer _frameConstantsBufferA;
        private GraphicsBuffer _frameConstantsBufferB;
        private GraphicsBuffer _activeFrameConstantsBuffer;
        private int _frameConstantsUploadBufferIndex;
        private GraphicsBuffer _visibleParticleIndexBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _maelstromBufferA;
        private GraphicsBuffer _maelstromBufferB;
        private GraphicsBuffer _emptyAbyssalFlowBuffer;
        private GraphicsBuffer _mockWakeDtoBuffer;
        private GraphicsBuffer _mockWakeBuffer;
        private GraphicsBuffer _mockWakeVectorBuffer;
        private GraphicsBuffer _propwashEventBufferA;
        private GraphicsBuffer _propwashEventBufferB;
        private GraphicsBuffer _propwashEventBuffer;
        private GraphicsBuffer _boundAbyssalFlowBuffer;
        private Camera _targetCameraComponent;
        private Bounds _drawBounds;
        private int _kernelIndex = -1;
        private int _initializeKernel = -1;
        private int _clearVisibleKernel = -1;
        private int _sonarGlowClearKernel = -1;
        private int _sonarGlowAccumulateKernel = -1;
        private int _fogDensityClearKernel = -1;
        private int _wakeProximityKernel = -1;
        private int _rebaseKernel = -1;
        private int _simulationThreadGroupSize;
        private int _initializeThreadGroupSize;
        private int _clearVisibleThreadGroupSize;
        private int _sonarGlowAccumulateThreadGroupSize;
        private int _wakeProximityThreadGroupSize;
        private int _rebaseThreadGroupSize;
        private int _sonarGlowClearTileSizeX = DefaultClearKernelTileSize;
        private int _sonarGlowClearTileSizeY = DefaultClearKernelTileSize;
        private int _fogDensityClearTileSizeX = DefaultClearKernelTileSize;
        private int _fogDensityClearTileSizeY = DefaultClearKernelTileSize;
        private int _frameParity;
        private int _flowFieldResolution;
        private int _flowFieldBufferCapacity;
        private int _propwashEventUploadWriteIndex;
        private float _flowFieldCellSize;
        private float _flowFieldUploadTimer;
        private float _simulationTime;
        private int _activeParticleCount;
        private int _allocatedParticleCapacity;
        private int _resolvedParticleCapacity = MinimumMarineSnowParticleCapacity;
        private VfxComputeParticleBudget _resolvedPressureBudget = VfxComputeParticleBudget.MinimumQuality;
        private VFXEmissionProfile.FluidType _resolvedFluidType = (VFXEmissionProfile.FluidType)255;
        private byte _resolvedPressureLevel = byte.MaxValue;
        private float _resolvedGlobalQualityWeight = -1f;
        private ulong _resolvedKillSwitchMask = ulong.MaxValue;
        private bool _registeredLateFrame;
        private bool _registeredSlowTick;
        private bool _registeredColdTick;
        private bool _pendingVisualTickDirty;
        private float _pendingVisualTickDeltaTime;
        private bool _buffersReady;
        private bool _staticBindingsDirty = true;
        private bool _underwaterActive;
        private float _biolumeSurgeTimer;
        private float _visualDensityScale;
        private float _lastDepth;
        private float _lastLightFactor = 1f;
        private float _lastSubmergeImpulse;
        private float _bubbleTrailMovement01;
        private float _bubbleTrailExhale01;
        private float _speedLineIntensity;
        private float _speedLineStartVelocitySq;
        private float _speedLineInvVelocityBandSq = 1f;
        private float _speedLineStretchDelta;
        private float _speedLineResponseSpeed = 0.1f;
        private bool _hasLastCameraPositionWS;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        private Vector3 _lastCameraPositionWS;
        private RenderTexture _sonarGlowTexture;
        private int _sonarGlowWidth;
        private int _sonarGlowHeight;
        private RenderTexture _fogDensityTexture;
        private int _fogDensityWidth;
        private int _fogDensityHeight;
        private int _fogDensityClearGroupsX;
        private int _fogDensityClearGroupsY;
        private IAbyssalFlowGpuReadModel _abyssalFlowGpuReadModel;
        private IWeatherService _weatherService;
        private DynamicResolutionScaler _dynamicResolutionScaler;
        private IVramBudgetReadModel _vramMonitor;
        private ITickDispatcher _tickDispatcher;
        private int _nextFluidRebindFrame;
        private int _nextDataVaultRebindFrame;
        private int _nextProceduralWakeSourceProbeFrame;
        private Vector4 _fogDensityTexelSize;
        private Vector4 _lastPublishedSonarGlowTexelSize;
        private Vector4 _lastPublishedSonarGlowParams;
        private Texture _lastPublishedSonarGlowTexture;
        private Vector4 _lastPublishedFogDensityTexelSize;
        private Vector4 _lastPublishedFogDensityParams;
        private Texture _lastPublishedFogDensityTexture;
        private Texture _boundCameraDepthTexture;
        private Texture _cameraDepthTextureSnapshot;
        private Texture _boundTerrainHeightTexture;
        private Texture _boundCaveSdfTexture;
        private Texture _boundAbyssalFlowTexture;
        private Texture3D _emptyCaveSdfTexture;
        private Texture3D _emptyAbyssalFlowTexture;
        private Vector4 _boundAbyssalGridResolution;
        private Vector4 _boundAbyssalFlowCenter;
        private Vector4 _boundAbyssalFlowSpacing;
        private Vector4 _boundAbyssalFlowTextureParams;
        private GraphicsBuffer _boundDynamicWakeBuffer;
        private GraphicsBuffer _boundDynamicWakeVectorBuffer;
        private GraphicsBuffer _boundDynamicWakeDtoBuffer;
        private GraphicsBuffer _boundPropwashEventBuffer;
        private Vector4 _boundDynamicWakeParams = InvalidVector;
        private Vector4 _boundDynamicWakeDtoParams = InvalidVector;
        private Vector4 _boundMaelstromParams = InvalidVector;
        private float _boundAbyssalFlowTextureActive = float.NaN;
        private float _boundFlashlightActive = float.NaN;
        private Vector4 _boundCaveVoxelHalfExtents;
        private Vector4 _boundCaveVoxelInvDoubleHalfExtents;
        private Vector4 _boundTerrainHeightRect;
        private Vector4 _boundTerrainHeightScale;
        private Vector4 _boundSubmarineWashSphere;
        private Vector4 _boundSubmarineWashVelocity;
        private Vector4 _cachedSubmarineWashSphere;
        private Vector4 _cachedSubmarineWashVelocity;
        private Vector4 _cachedFlashlightPositionWS;
        private Vector4 _cachedFlashlightDirectionWS;
        private Vector4 _cachedFlashlightColor;
        private Vector4 _cachedFlashlightConeData;
        private Vector4 _cachedFlowSynchronyParams = DefaultFlowSynchronyParams;
        private Vector4 _cachedZBufferParams;
        private float _cachedFlashlightActive;
        private float _cachedSonarRevealExpireTime;
        private Vector4 _boundFloatingOriginOffset = InvalidVector;
        private Vector4 _boundAupShiftOffset = InvalidVector;
        private Vector4 _boundFlashlightPositionWS = InvalidVector;
        private Vector4 _boundFlashlightDirectionWS = InvalidVector;
        private Vector4 _boundFlashlightColor = InvalidVector;
        private Vector4 _boundFlashlightConeData = InvalidVector;
        private Vector4 _boundPropwashParams;
        private Vector4 _boundPropwashEventParams = InvalidVector;
        private Vector4 _boundPropwashBiomeTint = InvalidVector;
        private Vector4 _boundVelocityParams = InvalidVector;
        private Vector4 _resolvedScalabilityParams = MinimumScalabilityParams;
        private GraphicsBuffer _boundSimulationReadBuffer;
        private GraphicsBuffer _boundSimulationWriteBuffer;
        private GraphicsBuffer _boundSimulationMetaReadBuffer;
        private GraphicsBuffer _boundSimulationMetaWriteBuffer;
        private GraphicsBuffer _boundSimulationFlowFieldBuffer;
        private GraphicsBuffer _boundSimulationVisibleParticleIndexBuffer;
        private GraphicsBuffer _boundSimulationIndirectArgsBuffer;
        private GraphicsBuffer _boundSimulationMaelstromBuffer;
        private uint _boundMaelstromUploadHash;
        private int _boundMaelstromUploadCount = -1;
        private int _maelstromWriteBufferIndex;
        private GraphicsBuffer _boundMaterialParticlesBuffer;
        private GraphicsBuffer _boundMaterialParticleMetaBuffer;
        private GraphicsBuffer _boundMaterialVisibleParticleIndexBuffer;
        private Texture _boundMaterialMaskAtlas;
        private Texture _boundMaterialNormalAtlas;
        private Material _materialAtlasFallbackSource;
        private Vector4 _boundMaterialAtlasParams = InvalidVector;
        private Vector4 _boundMaterialFlipbookParams = InvalidVector;
        private Vector4 _boundMaterialPropwashBiomeTint = InvalidVector;
        private GraphicsBuffer _boundSonarGlowParticlesWriteBuffer;
        private GraphicsBuffer _boundSonarGlowParticleMetaWriteBuffer;
        private Texture _boundSonarGlowClearTexture;
        private Texture _boundSonarGlowAccumulateTexture;
        private Texture _boundFogDensityClearTexture;
        private Texture _boundFogDensitySimulationTexture;
        private Vector4 _boundEmissionParams = InvalidVector;
        private Vector4 _boundBubbleParams = InvalidVector;
        private Vector4 _boundDriftParams = InvalidVector;
        private Vector4 _boundFlowParams = InvalidVector;
        private Vector4 _boundMockFlowField = InvalidVector;
        private Vector4 _boundMockAcousticPulse = InvalidVector;
        private Vector4 _boundMockAcousticParams = InvalidVector;
        private Vector4 _boundFlowSynchronyParams = InvalidVector;
        private Vector4 _boundZBufferParams = InvalidVector;
        private int _boundDispatchOffset = int.MinValue;
        private Vector4 _boundDepthTextureTexelSize = InvalidVector;
        private Vector4 _boundDepthCollisionParams = InvalidVector;
        private Vector4 _boundScalabilityParams = InvalidVector;
        private Vector4 _boundSonarGlowTexelSize = InvalidVector;
        private Vector4 _boundSonarGlowParams = InvalidVector;
        private Vector4 _boundFogDensityTexelSize = InvalidVector;
        private Vector4 _boundFogDensityParams = InvalidVector;
        private Vector4 _boundDispatchTileOffset = InvalidVector;
        private Matrix4x4 _boundViewProjection = InvalidMatrix;
        private Matrix4x4 _boundViewMatrix = InvalidMatrix;
        private Matrix4x4 _boundCaveVoxelWorldToLocal = IdentityMatrix;
        private float _boundCaveVoxelActive = -1f;
        private float _externalGpuBindingColdTickTimer;
        private float _vehicleWakePublishCooldown;
        private float _mockWakeUploadTimer;
        private float _mockAcousticPulseTimer;
        private float _lastVehicleThrottle;
        private float _lastHeadlightBoost;
        private int _lastVehicleTargetInstanceId;
        private uint _lastVehicleCommandSequence;
        private int _lastTelemetryPublishFrame = -TelemetryPublishFrameCadence;
        private int _telemetryWriteIndex;
        private int _telemetryWrittenCount;
        private int _debugDynamicWakeCount;
        private int _debugMockWakeCount;
        private int _debugPropwashEventCount;
        private float _debugPropwashMaxIntensity;
        private float3 _debugPropwashStrongestLocalPosition;
        private int _propwashTelemetryWriteIndex;
        private int _propwashTelemetryWrittenCount;
#if UNITY_EDITOR
        private long _csvProfileAppliedTicks;
        private long _csvProfileStagedTicks;
        private long _wakeProfileAppliedTicks;
        private long _wakeProfileStagedTicks;
        private int _csvProfileStagedLength;
        private int _wakeProfileStagedLength;
        private string _csvProfilePath;
        private string _wakeProfilePath;
        private bool _csvProfilePathsResolved;
        private Thread _csvProfileThread;
        private volatile bool _csvProfileThreadStopRequested;
        private volatile bool _csvProfileStagedDirty;
        private volatile bool _wakeProfileStagedDirty;
#endif
        private VfxConfigurationDTO _cachedSiltTuning;
        private PropwashGpuTuningDTO _cachedPropwashTuning;
        private MockFlowField _cachedMockFlowField;
        private MockAcousticSignal _mockAcousticSignal;
        private Vector3 _pendingAupShiftOffset;
        private bool _vehicleCommandListenerRegistered;
        private bool _nativeStateReady;
        private bool _blackBoxDumped;
        private bool _hotSwapRegistered;
        private bool _dispatcherReady;
        private bool _mockWakeBuffersCleared = true;
        private bool _particleBuffersNeedGpuBootstrap;
        private bool _externalGpuBindingsDirty = true;
        private bool _materialAtlasFallbackResolved;
        private bool _sonarGlowGlobalsDirty = true;
        private bool _fogDensityGlobalsDirty = true;
        private bool _coldSupportsComputeShaders;
        [SerializeField] private int _debugActiveParticleCount;
        [SerializeField] private int _debugAllocatedParticleCapacity;
        [SerializeField] private int _debugScalabilityParticleCapacity = MinimumMarineSnowParticleCapacity;
        [SerializeField] private float _debugGlobalQualityWeight01;
        [SerializeField] private float _debugQualityPressure01 = 1f;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetScale = 1f;
        [SerializeField] private byte _debugAdaptiveVramPressureState;
        [SerializeField] private float _debugBiolumeSurgeBlend;
        [SerializeField] private int _debugHomeostasisPressureLevel;
        [SerializeField] private uint _debugHomeostasisKillSwitchMaskLow32;
        [SerializeField] private float _debugBudgetedStepDistanceMeters;
        [SerializeField] private int _debugBudgetedShadowTaps;
        [SerializeField] private int _debugPropwashGpuEventCount;

#if UNITY_EDITOR
        private readonly byte[] _csvProfileReadBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - reusable vfx_silt_profiles.csv parser buffer - owner: HectonMarineSnowRenderer
        private readonly byte[] _csvProfileBackgroundBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - background CSV file-read staging buffer - owner: HectonMarineSnowRenderer
        private readonly byte[] _csvProfileStagedBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - main-thread CSV parse staging buffer - owner: HectonMarineSnowRenderer
        private readonly object _csvProfileSync = new object(); // COLD ALLOC: object[1] - CSV staging lock shared with background reader - owner: HectonMarineSnowRenderer
        private readonly byte[] _wakeProfileReadBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - reusable vehicle_wake_profiles.csv parser buffer - owner: HectonMarineSnowRenderer
        private readonly byte[] _wakeProfileBackgroundBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - wake profile background staging buffer - owner: HectonMarineSnowRenderer
        private readonly byte[] _wakeProfileStagedBuffer = new byte[CsvProfileReadBufferBytes]; // COLD ALLOC: byte[4096] - wake profile main-thread parse staging buffer - owner: HectonMarineSnowRenderer
        private readonly object _wakeProfileSync = new object(); // COLD ALLOC: object[1] - wake profile staging lock shared with background reader - owner: HectonMarineSnowRenderer
        private NativeArray<PropwashWakeProfileDTO> _wakeProfileParseScratch;
#endif
        /// <summary>
        /// True when the compute path has all required resources and can replace the fallback particle system.
        /// </summary>
        public bool IsOperational => _buffersReady && marineSnowCompute != null && marineSnowMaterial != null && _kernelIndex >= 0;

        private void CacheGraphicsCapabilitySnapshotCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
        }

        private void RefreshExternalShaderGlobalsCold()
        {
            _cachedSubmarineWashSphere = Shader.GetGlobalVector(ShaderIds.SubmarineWashSphereId);
            _cachedSubmarineWashVelocity = Shader.GetGlobalVector(ShaderIds.SubmarineWashVelocityId);
            _cachedFlashlightPositionWS = Shader.GetGlobalVector(ShaderIds.FlashlightPositionWSId);
            _cachedFlashlightDirectionWS = Shader.GetGlobalVector(ShaderIds.FlashlightDirectionWSId);
            _cachedFlashlightColor = Shader.GetGlobalVector(ShaderIds.FlashlightColorId);
            _cachedFlashlightConeData = Shader.GetGlobalVector(ShaderIds.FlashlightConeDataId);
            _cachedFlashlightActive = Shader.GetGlobalFloat(ShaderIds.FlashlightActiveId);
            _cachedSonarRevealExpireTime = Shader.GetGlobalFloat(ShaderIds.SonarRevealExpireTimeId);
            _cameraDepthTextureSnapshot = Shader.GetGlobalTexture(ShaderIds.CameraDepthTextureId);
            _cachedZBufferParams = Shader.GetGlobalVector(ShaderIds.GlobalZBufferParamsId);

            Vector4 synchronyParams = Shader.GetGlobalVector(ShaderIds.FlowSynchronyParamsId);
            _cachedFlowSynchronyParams = synchronyParams.x > 0f ? synchronyParams : DefaultFlowSynchronyParams;
        }

        private void OnEnable()
        {
            RefreshSpeedLineCache();
            CacheGraphicsCapabilitySnapshotCold();
            ResolveTargetCameraCold();
            TryRegisterHotSwapListener();
            RefreshExternalShaderGlobalsCold();
            RefreshAuthoredNeutralVolumeFallbacksColdEditor();
            RefreshMaterialFlipbookAtlasFallbackCold();
            RefreshFluidBinding(force: true);
            RefreshDataVaultBinding(force: true);
            EnsureNativeState();
            RegisterVehicleCommandListener();
            HectonFloatingOrigin.RegisterListener(this);
#if UNITY_EDITOR
            EnsureCsvProfileBackgroundReader();
#endif
            TryRegisterLateFrame();
            TryRegisterSlowTick();
            TryRegisterColdTick();
        }

        private void OnValidate()
        {
            RefreshSpeedLineCache();
            RefreshAuthoredNeutralVolumeFallbacksColdEditor();
            RefreshMaterialFlipbookAtlasFallbackCold();
            _staticBindingsDirty = true;
            _externalGpuBindingsDirty = true;
#if UNITY_EDITOR
            ResolveMarineSnowComputeAtAuthorTime();
#endif
        }

#if UNITY_EDITOR
        private const string MarineSnowComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_MarineSnow.compute";

        /// <summary>
        /// Resolves the marine snow kernel at AUTHOR time so the reference is serialized and therefore ships.
        ///
        /// marineSnowCompute arrived only through [SerializeField], and a GUID census found this compute asset
        /// referenced by no scene, prefab or asset - so the field was null everywhere. IsOperational gates on
        /// it, as do every dispatch guard, which means all marine snow paths early-returned and the densest
        /// depth cue in the abyss never rendered at all. Commit 32c3c8a1a connected the two missing pieces
        /// around this system but not this reference, so it stayed inert regardless.
        ///
        /// The assignment is proven by kernel name rather than name similarity: CS_IntegrateSiltParticles and
        /// CS_RebaseParticles are declared in exactly one compute asset in the project.
        ///
        /// SetDirty is what separates this from a lazy runtime resolve. Assigning in memory only would repair
        /// the field on every editor load and still ship null - the exact failure mode 30deb1fe9 documents,
        /// where the editor always looks correct and the player build is dead. Marking dirty serializes it, so
        /// any new scene or prefab carrying this component wires itself instead of depending on somebody
        /// remembering an Inspector drag. Runtime cost is zero; none of this exists in a build.
        ///
        /// Writing it to disk is a separate, deliberate step owned by Hecton8/GPU/Persist Kernel References
        /// (GpuKernelReferencePersist). That tool only persists references already resolved in memory, which is
        /// precisely what this supplies - it previously saw this field as stillNull and could not act on it.
        /// </summary>
        private void ResolveMarineSnowComputeAtAuthorTime()
        {
            if (Application.isPlaying || marineSnowCompute != null)
                return;

            ComputeShader resolved = AssetDatabase.LoadAssetAtPath<ComputeShader>(MarineSnowComputeAssetPath);
            if (resolved == null)
                return;

            marineSnowCompute = resolved;
            EditorUtility.SetDirty(this);
        }
#endif

        private void OnDisable()
        {
            ReleaseRuntimeState();
        }

        private void OnDestroy()
        {
            ReleaseRuntimeState();
        }

        private void ReleaseRuntimeState()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            UnregisterVehicleCommandListener();
            TryUnregisterHotSwapListener();
#if UNITY_EDITOR
            StopCsvProfileBackgroundReader();
            DisposeWakeProfileParseScratch();
#endif
            SetUnderwaterState(false, 0f, 0f, 1f, 0f);
            SetBubbleTrailState(0f, 0f);
            UnregisterRuntimeTickables();
            ReleaseBuffers();
            ClearNativeStateLease();
            _abyssalFlowGpuReadModel = null;
            _weatherService = null;
            _dynamicResolutionScaler = null;
            _vramMonitor = null;
            _tickDispatcher = null;
            _dispatcherReady = false;
            _nextFluidRebindFrame = 0;
            _nextDataVaultRebindFrame = 0;
            _nextProceduralWakeSourceProbeFrame = 0;
        }

        private void UnregisterRuntimeTickables()
        {
            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }
        }

        /// <summary>
        /// Binds the camera transform that owns the marine-snow shell.
        /// </summary>
        /// <param name="cameraTransform">Runtime main-camera transform.</param>
        public void BindTargetCamera(Transform cameraTransform)
        {
            targetCamera = cameraTransform;
            _targetCameraComponent = ResolveComponentOnTransform<Camera>(cameraTransform);
            ResetSpeedLineHistory();
        }

        /// <summary>
        /// Binds the camera that owns the marine-snow shell without a component lookup.
        /// </summary>
        /// <param name="cameraComponent">Runtime main-camera component.</param>
        public void BindTargetCamera(Camera cameraComponent)
        {
            targetCamera = cameraComponent != null ? cameraComponent.transform : null;
            _targetCameraComponent = cameraComponent;
            ResetSpeedLineHistory();
        }

        /// <summary>
        /// Updates the underwater state pushed by <see cref="HectonUnderwaterVisuals"/>.
        /// </summary>
        /// <param name="active">True when underwater visuals are active.</param>
        /// <param name="densityScale">Normalized density scale derived from the underwater owner.</param>
        /// <param name="depth">Current camera depth.</param>
        /// <param name="lightFactor">Current underwater light factor.</param>
        /// <param name="submergeImpulse">Current submerge impulse amount.</param>
        public void SetUnderwaterState(bool active, float densityScale, float depth, float lightFactor, float submergeImpulse)
        {
            _underwaterActive = active;
            _visualDensityScale = math.saturate(densityScale);
            _lastDepth = math.max(0f, depth);
            _lastLightFactor = math.saturate(lightFactor);
            _lastSubmergeImpulse = math.saturate(submergeImpulse);
            if (!active)
                ResetSpeedLineHistory();
        }

        public void SetBubbleTrailState(float movement01, float exhale01)
        {
            _bubbleTrailMovement01 = math.saturate(movement01);
            _bubbleTrailExhale01 = math.saturate(exhale01);
        }

        public void OnVehicleCommandSignal(in VehicleCommandSignal signal)
        {
            if ((signal.Flags & (byte)VehicleCommandSignalFlags.ManualThrottle) == 0 &&
                math.abs(signal.Throttle) <= VehicleWakeThrottleDeadZone)
            {
                return;
            }

            if (!math.isfinite(signal.Throttle))
                return;

            _lastVehicleThrottle = math.clamp(signal.Throttle, -1f, 1f);
            _lastVehicleTargetInstanceId = signal.TargetInstanceId;
            _lastVehicleCommandSequence = signal.Sequence;
        }

        private void RegisterVehicleCommandListener()
        {
            if (_vehicleCommandListenerRegistered || !Application.isPlaying)
                return;

            VehicleCommandSignalBus.Register(this);
            _vehicleCommandListenerRegistered = true;
        }

        private void UnregisterVehicleCommandListener()
        {
            if (!_vehicleCommandListenerRegistered)
                return;

            VehicleCommandSignalBus.Unregister(this);
            _vehicleCommandListenerRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                BindDataVault(currentService as IDataVault, previousService as IDataVault);
                return;
            }

            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Dispatcher, GlobalRegistry.TickDispatcher);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.FluidRuntime, GlobalRegistry.AbyssalFlowGpu);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.Weather, GlobalRegistry.Weather);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DynamicResolutionRuntime, GlobalRegistry.DynamicResolution);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.VRAMMonitorRuntime, GlobalRegistry.VRAMBudgetReadModel);
            _resolvedGlobalQualityWeight = -1f;
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Dispatcher:
                    ITickDispatcher tickDispatcher = currentService as ITickDispatcher;
                    if (!ReferenceEquals(_tickDispatcher, tickDispatcher))
                    {
                        if (_registeredLateFrame)
                        {
                            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                            _registeredLateFrame = false;
                        }

                        if (_registeredSlowTick)
                        {
                            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                            _registeredSlowTick = false;
                        }

                        if (_registeredColdTick)
                        {
                            GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                            _registeredColdTick = false;
                        }

                        _tickDispatcher = tickDispatcher;
                    }

                    _dispatcherReady = tickDispatcher != null;
                    if (_dispatcherReady)
                    {
                        TryRegisterLateFrame();
                        TryRegisterSlowTick();
                        TryRegisterColdTick();
                    }
                    break;
                case GlobalRegistryServiceSlot.FluidRuntime:
                    _abyssalFlowGpuReadModel = currentService as IAbyssalFlowGpuReadModel;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    BindDataVault(currentService as IDataVault, null);
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    _weatherService = currentService as IWeatherService;
                    break;
                case GlobalRegistryServiceSlot.DynamicResolutionRuntime:
                    _dynamicResolutionScaler = currentService as DynamicResolutionScaler;
                    break;
                case GlobalRegistryServiceSlot.VRAMMonitorRuntime:
                    _vramMonitor = currentService as IVramBudgetReadModel;
                    break;
            }
        }

        private void BindDataVault(IDataVault vault, IDataVault previousVault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseOwnedVaultHandles(previousVault ?? _dataVault);
            _dataVault = vault;
            ClearVaultHandleCache();
        }

        private void ClearVaultHandleCache()
        {
            _telemetryRingHandle = default;
            _siltTuningHandle = default;
            _dynamicWakeDtoHandle = default;
            _mockFlowFieldHandle = default;
            _proceduralWakeSourcesHandle = default;
            _propwashEventHandle = default;
            _propwashCursorHandle = default;
            _propwashTelemetryHandle = default;
            _propwashTuningHandle = default;
            _propwashWakeProfileHandle = default;
            _nextProceduralWakeSourceProbeFrame = 0;
            _nativeStateReady = false;
        }

        private void ResetSpeedLineHistory()
        {
            _speedLineIntensity = 0f;
            _hasLastCameraPositionWS = false;
            _lastCameraPositionWS = Vector3.zero;
        }

        private void RefreshSpeedLineCache()
        {
            float fullVelocity = math.max(1f, speedLineFullVelocity);
            float startVelocity = fullVelocity * 0.72f;
            float fullVelocitySq = fullVelocity * fullVelocity;
            float startVelocitySq = startVelocity * startVelocity;
            _speedLineStartVelocitySq = startVelocitySq;
            _speedLineInvVelocityBandSq = math.rcp(math.max(0.01f, fullVelocitySq - startVelocitySq));
            _speedLineStretchDelta = math.max(1f, speedLineMaxStretch) - 1f;
            _speedLineResponseSpeed = math.max(0.1f, speedLineResponseSharpness);
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector3 runtimeOffset = -shiftOffset;
            _flowFieldCenterWS += runtimeOffset;
            if (_lastUploadedFlowFieldCenterWS != Vector3.zero)
                _lastUploadedFlowFieldCenterWS += runtimeOffset;

            _pendingAupShiftOffset += runtimeOffset;
            _flowFieldUploadTimer = 0f;
            ResetSpeedLineHistory();
        }

        private void AdvanceMarineSnowVisualState(float dt)
        {
            _pendingVisualTickDeltaTime += math.max(0f, dt);
            _pendingVisualTickDirty = true;
        }

        public void LateFrameTick()
        {
            AdvanceMarineSnowVisualState(SystemDispatcher.CurrentFrameDeltaTime);

            if (!_pendingVisualTickDirty)
                return;

            float dt = _pendingVisualTickDeltaTime;
            _pendingVisualTickDeltaTime = 0f;
            _pendingVisualTickDirty = false;
            RunMarineSnowVisualTick(dt);
        }

        public void SlowTick()
        {
            if (!enabled || marineSnowCompute == null || marineSnowMaterial == null || !HasCachedTargetCamera())
                return;

            if (!_buffersReady)
                return;

            _externalGpuBindingsDirty = true;
        }

        public void ColdTick()
        {
            CacheGraphicsCapabilitySnapshotCold();
            ResolveTargetCameraCold();
            RefreshExternalShaderGlobalsCold();

            if (!enabled || marineSnowCompute == null || marineSnowMaterial == null || !HasCachedTargetCamera())
                return;

            EnsureNativeState();
            EnsureBuffers();
            if (!_buffersReady)
                return;

#if UNITY_EDITOR
            RefreshSiltProfileCsv();
            RefreshPropwashWakeProfileCsv();
#endif
            EnsureSonarGlowTexture();
            EnsureFogDensityTexture();
            RefreshColdGpuBindings(ExternalGpuBindingColdTickSeconds);
        }

        private void RunMarineSnowVisualTick(float dt)
        {
            if (!enabled || marineSnowCompute == null || marineSnowMaterial == null)
                return;

            if (!HasCachedTargetCamera())
                return;

            float effectiveDensityScale = ResolveEffectiveDensityScale();
            if (effectiveDensityScale <= ActiveDensityEpsilon)
            {
                _activeParticleCount = 0;
                _debugActiveParticleCount = 0;
                ClearInactiveVisualWakeState();
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                PublishFogDensityGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            if (!AreMarineSnowRuntimeResourcesReady())
                return;

            RefreshMockWakeSignals(math.max(0f, dt));
            HarvestProceduralWakeSourcesIntoPropwash();
            RefreshMockAcousticSignal(math.max(0f, dt));
            UpdateBiolumeSurgeState(dt);
            EnsureParticleBudget();
            _activeParticleCount = ResolveActiveParticleCount(effectiveDensityScale);
            if (_activeParticleCount <= 0)
            {
                ClearInactiveVisualWakeState();
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                PublishFogDensityGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            RefreshFlowFieldUpload(dt);
            ApplyStaticBindingsIfNeeded();
            UpdateFrameConstants(math.max(0f, dt), effectiveDensityScale);
            RefreshHotGpuBindings();
            PublishVehicleWakeImpulse(math.max(0f, dt));
            DispatchParticleInitializationIfNeeded();
            DispatchVisibleClear();
            DispatchFogDensityClear();
            DispatchSimulation();
            DispatchSonarGlow();
            RenderMarineSnow();
            RecordTelemetry();
            _frameParity ^= 1;
        }

        private void ClearInactiveVisualWakeState()
        {
            _debugMockWakeCount = 0;
            _cachedMockFlowField = default;
            ResetPropwashDebugState();

            if (_mockWakeBuffersCleared)
                return;

            MockFlowField emptyFlowField = default;
            TryWriteMockFlowFieldToVault(in emptyFlowField);
            ClearMockWakeGpuBuffers();
            ClearMockOnlyPropwashCursor();
            _mockWakeBuffersCleared = true;
        }

        private float ResolveEffectiveDensityScale()
        {
            if (!_underwaterActive)
                return 0f;

            VfxConfigurationDTO tuning = CaptureSiltTuningSnapshot();
            float densityMultiplier = tuning.Version != 0u ? math.max(0f, tuning.DensityScale) : 1f;
            return math.saturate(
                _visualDensityScale +
                (_lastSubmergeImpulse * 0.35f) +
                (_bubbleTrailMovement01 * 0.08f) +
                (_bubbleTrailExhale01 * 0.12f)) * densityMultiplier;
        }

        private void RefreshFluidBinding(bool force)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (!force && frame < _nextFluidRebindFrame)
                return;

            _nextFluidRebindFrame = frame + 30;
        }

        private bool RefreshDataVaultBinding(bool force)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_dataVault != null && _dataVault.IsCompactionFenceActive)
            {
                _nativeStateReady = false;
                _proceduralWakeSourcesHandle = default;
                if (!force && frame < _nextDataVaultRebindFrame)
                    return false;
            }

            if (!force && _dataVault != null && !_dataVault.IsCompactionFenceActive)
                return true;
            if (!force && frame < _nextDataVaultRebindFrame)
                return _dataVault != null && !_dataVault.IsCompactionFenceActive;

            _nextDataVaultRebindFrame = frame + 30;
            return _dataVault != null && !_dataVault.IsCompactionFenceActive;
        }

        private bool EnsureNativeState()
        {
            if (_nativeStateReady)
            {
                IDataVault cachedVault = _dataVault;
                if (cachedVault != null &&
                    !cachedVault.IsCompactionFenceActive &&
                    AreOwnedVaultBuffersReady(cachedVault))
                {
                    RefreshProceduralWakeSourcesHandle(cachedVault, force: false);
                    return true;
                }

                _nativeStateReady = false;
            }

            if (!ValidateNativeStructLayouts() || !RefreshDataVaultBinding(force: false))
            {
                _nativeStateReady = false;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                _nativeStateReady = false;
                return false;
            }

            _nativeStateReady =
                EnsureOwnedVaultBuffer(
                    ref _telemetryRingHandle,
                    BufferID.MarineSnowTelemetryRing,
                    TelemetryCapacity,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _siltTuningHandle,
                    BufferID.MarineSnowTuningConstants,
                    1,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _dynamicWakeDtoHandle,
                    BufferID.MarineSnowDynamicWakes,
                    DynamicWakeDtoCapacity,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _mockFlowFieldHandle,
                    BufferID.MarineSnowMockFlowField,
                    1,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _propwashEventHandle,
                    BufferID.PropwashGpuEventRing,
                    PropwashEventRingCapacity,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _propwashCursorHandle,
                    BufferID.PropwashGpuRingCursor,
                    1,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _propwashTelemetryHandle,
                    BufferID.PropwashGpuTelemetryRing,
                    PropwashGpuContracts.TelemetryCapacity,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _propwashTuningHandle,
                    BufferID.PropwashGpuTuning,
                    1,
                    NativeArrayOptions.ClearMemory) &&
                EnsureOwnedVaultBuffer(
                    ref _propwashWakeProfileHandle,
                    BufferID.PropwashGpuWakeProfiles,
                    PropwashWakeProfileCapacity,
                    NativeArrayOptions.ClearMemory);

            if (!_nativeStateReady)
                return false;

            RefreshProceduralWakeSourcesHandle(vault, force: true);
            InitializeDefaultSiltTuning(vault);
            InitializeDefaultPropwashTuning(vault);
            InitializeDefaultPropwashWakeProfiles(vault);
            return true;
        }

        private bool AreMarineSnowRuntimeResourcesReady()
        {
            if (!_buffersReady || !_nativeStateReady)
                return false;

            IDataVault vault = _dataVault;
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   AreOwnedVaultBuffersReady(vault);
        }

        private void RefreshProceduralWakeSourcesHandle(IDataVault vault, bool force)
        {
            if (HasVaultBuffer(vault, in _proceduralWakeSourcesHandle, BufferID.WakeSources, 1) ||
                vault == null ||
                vault.IsCompactionFenceActive)
            {
                return;
            }

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (!force && frame < _nextProceduralWakeSourceProbeFrame)
                return;

            _nextProceduralWakeSourceProbeFrame = frame + 30;
            if (vault.TryGetGenerationHandle<WakeSource>(BufferID.WakeSources, out VaultGenerationHandle<WakeSource> proceduralWakeSourcesHandle) &&
                HasVaultBuffer(vault, in proceduralWakeSourcesHandle, BufferID.WakeSources, 1))
            {
                _proceduralWakeSourcesHandle = proceduralWakeSourcesHandle;
            }
            else
            {
                _proceduralWakeSourcesHandle = default;
            }
        }

        private bool TryReadTelemetryRing(out NativeArray<MarineSnowTelemetryEntry>.ReadOnly telemetryRing)
        {
            telemetryRing = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _telemetryRingHandle, BufferID.MarineSnowTelemetryRing, TelemetryCapacity, out telemetryRing);
        }

        private bool TryReadSiltTuning(out NativeArray<VfxConfigurationDTO>.ReadOnly tuning)
        {
            tuning = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _siltTuningHandle, BufferID.MarineSnowTuningConstants, 1, out tuning);
        }

        private bool TryReadReadyProceduralWakeSources(out NativeArray<WakeSource>.ReadOnly wakeSources)
        {
            wakeSources = default;
            if (!_nativeStateReady)
                return false;

            RefreshProceduralWakeSourcesHandle(_dataVault, force: false);
            return TryReadOnlyVaultBuffer(ref _proceduralWakeSourcesHandle, BufferID.WakeSources, 1, out wakeSources);
        }

        private bool TryReadReadyPropwashEvents(out NativeArray<PropwashEventDTO>.ReadOnly events)
        {
            events = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _propwashEventHandle, BufferID.PropwashGpuEventRing, PropwashEventRingCapacity, out events);
        }

        private bool TryReadReadyDynamicWakes(out NativeArray<DynamicWakeDTO>.ReadOnly wakes)
        {
            wakes = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _dynamicWakeDtoHandle, BufferID.MarineSnowDynamicWakes, DynamicWakeDtoCapacity, out wakes);
        }

        private bool TryReadReadyPropwashCursor(out NativeArray<PropwashRingCursorDTO>.ReadOnly cursor)
        {
            cursor = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _propwashCursorHandle, BufferID.PropwashGpuRingCursor, 1, out cursor);
        }

        private bool TryReadReadyPropwashTelemetry(out NativeArray<PropwashTelemetryEntry>.ReadOnly telemetry)
        {
            telemetry = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _propwashTelemetryHandle, BufferID.PropwashGpuTelemetryRing, PropwashGpuContracts.TelemetryCapacity, out telemetry);
        }

        private bool TryReadReadyPropwashTuning(out NativeArray<PropwashGpuTuningDTO>.ReadOnly tuning)
        {
            tuning = default;
            return _nativeStateReady &&
                   TryReadOnlyVaultBuffer(ref _propwashTuningHandle, BufferID.PropwashGpuTuning, 1, out tuning);
        }

        private void InitializeDefaultSiltTuning(IDataVault vault)
        {
            VfxConfigurationDTO fallback = CreateDefaultSiltTuning();
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _siltTuningHandle, BufferID.MarineSnowTuningConstants, 1, out NativeArray<VfxConfigurationDTO> tuning))
            {
                return;
            }

            try
            {
                VfxConfigurationDTO current = tuning[0];
                if (current.Version == 0u)
                {
                    current = fallback;
                    tuning[0] = current;
                }

                _cachedSiltTuning = current;
            }
            finally
            {
                vault.ReleaseWriteLock(in _siltTuningHandle, VaultOwnerSystem);
            }
        }

        private VfxConfigurationDTO CreateDefaultSiltTuning()
        {
            return VolumetricSiltConfigurationAccess.CreateDefault(ResolveDefaultSiltParticleCapacity());
        }

        private int ResolveDefaultSiltParticleCapacity()
        {
            RefreshScalabilityProfile();
            return math.clamp(_resolvedParticleCapacity, 64, MaxMarineSnowParticleCapacity);
        }

        private VfxConfigurationDTO CaptureSiltTuningSnapshot()
        {
            if (TryReadSiltTuning(out NativeArray<VfxConfigurationDTO>.ReadOnly tuning))
            {
                VfxConfigurationDTO current = tuning[0];
                if (current.Version == 0u)
                {
                    current = CreateDefaultSiltTuning();
                }

                _cachedSiltTuning = current;
            }
            else if (_cachedSiltTuning.Version == 0u)
            {
                _cachedSiltTuning = CreateDefaultSiltTuning();
            }

            return _cachedSiltTuning;
        }

        private void InitializeDefaultPropwashTuning(IDataVault vault)
        {
            PropwashGpuTuningDTO fallback = PropwashGpuContracts.CreateDefaultTuning();
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _propwashTuningHandle, BufferID.PropwashGpuTuning, 1, out NativeArray<PropwashGpuTuningDTO> tuning))
            {
                return;
            }

            try
            {
                PropwashGpuTuningDTO current = tuning[0];
                if (current.Version == 0u)
                {
                    current = fallback;
                    tuning[0] = current;
                }

                _cachedPropwashTuning = current;
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashTuningHandle, VaultOwnerSystem);
            }
        }

        private void InitializeDefaultPropwashWakeProfiles(IDataVault vault)
        {
            PropwashWakeProfileDTO fallback = PropwashGpuContracts.CreateDefaultWakeProfile();
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _propwashWakeProfileHandle, BufferID.PropwashGpuWakeProfiles, PropwashWakeProfileCapacity, out NativeArray<PropwashWakeProfileDTO> profiles))
            {
                return;
            }

            try
            {
                if (profiles[0].Version == 0u)
                    profiles[0] = fallback;
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashWakeProfileHandle, VaultOwnerSystem);
            }
        }

        private PropwashGpuTuningDTO CapturePropwashTuningSnapshot()
        {
            if (TryReadReadyPropwashTuning(out NativeArray<PropwashGpuTuningDTO>.ReadOnly tuning))
            {
                PropwashGpuTuningDTO current = tuning[0];
                if (current.Version != 0u)
                    _cachedPropwashTuning = current;
            }

            if (_cachedPropwashTuning.Version == 0u)
            {
                _cachedPropwashTuning = PropwashGpuContracts.CreateDefaultTuning();
            }

            return _cachedPropwashTuning;
        }

        private float ResolvePropwashQualityWeight()
        {
            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            return tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
        }

        private bool EnsureOwnedVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (HasVaultBuffer(vault, in handle, bufferId, requiredLength))
                return true;

            if (vault.IsAllocationLocked)
                return false;

            ReleaseOwnedVaultHandle(vault, ref handle, bufferId);
            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            if (HasVaultBuffer(vault, in handle, bufferId, requiredLength))
                return true;

            ReleaseOwnedVaultHandle(vault, ref handle, bufferId);
            return false;
        }

        private bool AreOwnedVaultBuffersReady(IDataVault vault)
        {
            return HasVaultBuffer(vault, in _telemetryRingHandle, BufferID.MarineSnowTelemetryRing, TelemetryCapacity) &&
                   HasVaultBuffer(vault, in _siltTuningHandle, BufferID.MarineSnowTuningConstants, 1) &&
                   HasVaultBuffer(vault, in _dynamicWakeDtoHandle, BufferID.MarineSnowDynamicWakes, DynamicWakeDtoCapacity) &&
                   HasVaultBuffer(vault, in _mockFlowFieldHandle, BufferID.MarineSnowMockFlowField, 1) &&
                   HasVaultBuffer(vault, in _propwashEventHandle, BufferID.PropwashGpuEventRing, PropwashEventRingCapacity) &&
                   HasVaultBuffer(vault, in _propwashCursorHandle, BufferID.PropwashGpuRingCursor, 1) &&
                   HasVaultBuffer(vault, in _propwashTelemetryHandle, BufferID.PropwashGpuTelemetryRing, PropwashGpuContracts.TelemetryCapacity) &&
                   HasVaultBuffer(vault, in _propwashTuningHandle, BufferID.PropwashGpuTuning, 1) &&
                   HasVaultBuffer(vault, in _propwashWakeProfileHandle, BufferID.PropwashGpuWakeProfiles, PropwashWakeProfileCapacity);
        }

        private static bool HasVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsVfxVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadOnlyVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            return TryReadOnlyVaultBuffer(_dataVault, ref handle, bufferId, requiredLength, out buffer);
        }

        private static bool TryReadOnlyVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            return IsVfxVaultHandle(in handle, bufferId) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryAcquireOwnedVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsVfxVaultHandle(in handle, bufferId) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystem, out buffer))
            {
                return false;
            }

            bool handoffToCaller = false;
            try
            {
                handoffToCaller = buffer.IsCreated && buffer.Length >= requiredLength;
                if (handoffToCaller)
                    return true;
            }
            finally
            {
                if (!handoffToCaller)
                    vault.ReleaseWriteLock(in handle, VaultOwnerSystem);
            }

            buffer = default;
            return false;
        }

        private static bool IsVfxVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private void ReleaseOwnedVaultHandles(IDataVault vault)
        {
            ReleaseOwnedVaultHandle(vault, ref _telemetryRingHandle, BufferID.MarineSnowTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _siltTuningHandle, BufferID.MarineSnowTuningConstants);
            ReleaseOwnedVaultHandle(vault, ref _dynamicWakeDtoHandle, BufferID.MarineSnowDynamicWakes);
            ReleaseOwnedVaultHandle(vault, ref _mockFlowFieldHandle, BufferID.MarineSnowMockFlowField);
            ReleaseOwnedVaultHandle(vault, ref _propwashEventHandle, BufferID.PropwashGpuEventRing);
            ReleaseOwnedVaultHandle(vault, ref _propwashCursorHandle, BufferID.PropwashGpuRingCursor);
            ReleaseOwnedVaultHandle(vault, ref _propwashTelemetryHandle, BufferID.PropwashGpuTelemetryRing);
            ReleaseOwnedVaultHandle(vault, ref _propwashTuningHandle, BufferID.PropwashGpuTuning);
            ReleaseOwnedVaultHandle(vault, ref _propwashWakeProfileHandle, BufferID.PropwashGpuWakeProfiles);
            _proceduralWakeSourcesHandle = default;
        }

        private static void ReleaseOwnedVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsVfxVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ClearNativeStateLease()
        {
            ReleaseOwnedVaultHandles(_dataVault);
            _dataVault = null;
            ClearVaultHandleCache();
            _telemetryWriteIndex = 0;
            _telemetryWrittenCount = 0;
            _propwashTelemetryWriteIndex = 0;
            _propwashTelemetryWrittenCount = 0;
            _blackBoxDumped = false;
        }

        private static bool ValidateNativeStructLayouts()
        {
            return HasExpectedNativeStride<ParticleDataDTO>(ParticleDataStride) &&
                HasExpectedNativeStride<ParticleRenderMetaDTO>(ParticleRenderMetaStride) &&
                HasExpectedNativeStride<DynamicWakeDTO>(DynamicWakeDtoStride) &&
                HasExpectedNativeStride<VfxConfigurationDTO>(SiltConfigurationStride) &&
                HasExpectedNativeStride<MockFlowField>(SiltConfigurationStride) &&
                HasExpectedNativeStride<MockAcousticSignal>(SiltConfigurationStride) &&
                HasExpectedNativeStride<FrameConstantsData>(FrameConstantsStride) &&
                HasExpectedNativeStride<VehicleWakeJobResult>(VehicleWakeJobResultStride) &&
                HasExpectedNativeStride<MarineSnowTelemetryEntry>(TelemetryEntrySizeBytes) &&
                HasExpectedNativeStride<PropwashEventDTO>(PropwashEventStride) &&
                HasExpectedNativeStride<PropwashRingCursorDTO>(PropwashRingCursorStride) &&
                HasExpectedNativeStride<PropwashTelemetryEntry>(PropwashTelemetryEntryStride) &&
                HasExpectedNativeStride<PropwashGpuTuningDTO>(PropwashTuningStride) &&
                HasExpectedNativeStride<PropwashWakeProfileDTO>(PropwashWakeProfileStride) &&
                PropwashGpuContracts.ValidateRuntimeLayouts();
        }

        private static bool HasExpectedNativeStride<T>(int expectedStride) where T : struct
        {
            int actualStride = UnsafeUtility.SizeOf<T>();
            return actualStride == expectedStride && (actualStride & 7) == 0;
        }

        private void PublishVehicleWakeImpulse(float dt)
        {
            _vehicleWakePublishCooldown = math.max(0f, _vehicleWakePublishCooldown - dt);
            if (_vehicleWakePublishCooldown > 0f ||
                math.abs(_lastVehicleThrottle) <= VehicleWakeThrottleDeadZone)
            {
                return;
            }

            Vector4 washSphere = _cachedSubmarineWashSphere;
            Vector4 washVelocity = _cachedSubmarineWashVelocity;
            VehicleWakeJobResult result = BuildVehicleWakeSignal(
                new float4(washSphere.x, washSphere.y, washSphere.z, washSphere.w),
                new float4(washVelocity.x, washVelocity.y, washVelocity.z, washVelocity.w),
                _lastVehicleThrottle,
                math.max(0.5f, vehicleWakeRadius),
                math.max(0.1f, vehicleWakeLifetime),
                math.max(0.01f, vehicleWakeStrength));
            if ((result.Flags & 1u) == 0u)
                return;

            CommitVehicleWakePropwashEvent(in result);

            Vector3 position = new Vector3(result.PositionWS.x, result.PositionWS.y, result.PositionWS.z);
            if (!TryResolveRuntimeAup(position, out AbsoluteUniversePosition positionAup))
                return;

            FluidImpulseSignal signal = new FluidImpulseSignal
            {
                PositionAup = positionAup,
                Vector = result.VectorWS,
                Radius = result.Radius,
                Lifetime = result.Lifetime,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                SourceHash = VehicleWakeSourceHash,
                Flags = result.Flags
            };
            SignalBus<FluidImpulseSignal>.TryPushTracked(in signal, ref s_x001HectonMarineSnowRendererSignalPushDropCount);
            _vehicleWakePublishCooldown = VehicleWakePublishCooldownSeconds;
        }

        private static VehicleWakeJobResult BuildVehicleWakeSignal(
            float4 washSphere,
            float4 washVelocity,
            float throttle,
            float wakeRadius,
            float wakeLifetime,
            float wakeStrength)
        {
            VehicleWakeJobResult output = default;
            float safeThrottle = math.clamp(throttle, -1f, 1f);
            float throttleAbs = math.abs(safeThrottle);
            float radius = math.max(0.5f, wakeRadius);
            float lifetime = math.max(0.1f, wakeLifetime);
            float3 position = washSphere.xyz;
            float3 velocity = washVelocity.xyz;
            float speedSq = math.lengthsq(velocity);
            bool valid =
                throttleAbs > VehicleWakeThrottleDeadZone &&
                math.all(math.isfinite(position)) &&
                math.all(math.isfinite(velocity)) &&
                math.isfinite(radius) &&
                math.isfinite(lifetime) &&
                speedSq > 0.0001f;

            if (!valid)
                return output;

            float invSpeed = math.rsqrt(math.max(speedSq, 0.000001f));
            float speed = speedSq * invSpeed;
            float3 wakeAxis = velocity * invSpeed;
            output.PositionWS = position - wakeAxis * math.min(radius * 0.35f, 4f);
            output.VectorWS = wakeAxis * (math.max(0.01f, wakeStrength) * throttleAbs * math.min(speed, 16f));
            output.Radius = radius;
            output.Lifetime = lifetime;
            output.Intensity = throttleAbs;
            output.Flags = 1u;
            return output;
        }

        private void CommitVehicleWakePropwashEvent(in VehicleWakeJobResult result)
        {
            if (!TryReadPropwashCursorSnapshot(out PropwashRingCursorDTO cursor) ||
                !TryBuildPropwashLocalPosition(result.PositionWS, out float3 localPosition))
            {
                return;
            }

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(
                    vault,
                    in _propwashEventHandle,
                    BufferID.PropwashGpuEventRing,
                    PropwashEventRingCapacity,
                    out NativeArray<PropwashEventDTO> events))
            {
                return;
            }

            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            float quality = tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
            float radiusLimit = tuning.Version != 0u ? math.max(0.25f, tuning.MaxEventRadius) : 32f;
            bool publishEvents = false;
            try
            {
                if (TryAppendVehicleWakePropwashEvent(
                        events,
                        localPosition,
                        result.VectorWS,
                        result.Intensity,
                        math.min(result.Radius, radiusLimit),
                        unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                        quality,
                        PropwashGpuContracts.DefaultWakeProfileHash,
                        ref cursor))
                {
                    publishEvents = true;
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashEventHandle, VaultOwnerSystem);
            }

            if (publishEvents)
                TryPublishPropwashEvents(in cursor);
        }

        private bool TryBuildPropwashLocalPosition(float3 runtimePosition, out float3 localPosition)
        {
            localPosition = default;
            if (targetCamera == null || !math.all(math.isfinite(runtimePosition)))
                return false;

            Vector3 cameraRuntime = targetCamera.position;
            if (!math.isfinite(cameraRuntime.x) ||
                !math.isfinite(cameraRuntime.y) ||
                !math.isfinite(cameraRuntime.z))
            {
                return false;
            }

            if (!TryResolveRuntimeAup(
                    new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                    out AbsoluteUniversePosition eventAup) ||
                !TryResolveRuntimeAup(cameraRuntime, out AbsoluteUniversePosition cameraAup))
                return false;

            double3 delta = eventAup.ToAbsoluteDouble3() - cameraAup.ToAbsoluteDouble3();
            localPosition = AupPrecisionMath.DowncastLocalDelta(delta, float3.zero);
            return math.all(math.isfinite(localPosition));
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            double3 origin = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(origin)))
                return false;

            positionAup = AbsoluteUniversePosition.FromAbsolutePosition(
                origin + new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
        }

#if UNITY_EDITOR
        private void EnsureCsvProfileBackgroundReader()
        {
            if (_csvProfileThread != null && _csvProfileThread.IsAlive)
                return;

            if (!_csvProfilePathsResolved)
            {
                _csvProfilePath = ResolveVfxSourceCsvPath(SiltProfileCsvFileName);
                _wakeProfilePath = ResolveVfxSourceCsvPath(WakeProfileCsvFileName);
                _csvProfilePathsResolved = true;
            }

            if (string.IsNullOrEmpty(_csvProfilePath) && string.IsNullOrEmpty(_wakeProfilePath))
                return;

            try
            {
                _csvProfileThreadStopRequested = false;
                Thread reader = new Thread(CsvProfileBackgroundReadLoop)
                {
                    IsBackground = true,
                    Name = "H8_VfxCsvReader"
                };
                _csvProfileThread = reader;
                reader.Start();
            }
            catch (Exception)
            {
                _csvProfileThreadStopRequested = true;
                _csvProfileThread = null;
            }
        }

        private static string ResolveVfxSourceCsvPath(string fileName)
        {
            return Path.Combine(Application.dataPath, "_SourceData", "VFX", "Propwash", fileName);
        }

        private void StopCsvProfileBackgroundReader()
        {
            _csvProfileThreadStopRequested = true;
            Thread reader = _csvProfileThread;
            if (reader == null)
                return;

            if (TryJoinCsvProfileThreadNoThrow(reader))
                _csvProfileThread = null;
        }

        private static bool TryJoinCsvProfileThreadNoThrow(Thread reader)
        {
            if (reader == null || !reader.IsAlive)
                return true;
            if (ReferenceEquals(Thread.CurrentThread, reader))
                return false;

            try
            {
                reader.Join(CsvProfileThreadJoinTimeoutMilliseconds);
                return !reader.IsAlive;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool EnsureWakeProfileParseScratch()
        {
            if (_wakeProfileParseScratch.IsCreated &&
                _wakeProfileParseScratch.Length >= PropwashWakeProfileCapacity)
            {
                return true;
            }

            DisposeWakeProfileParseScratch();

            _wakeProfileParseScratch = H8Memory.Allocate<PropwashWakeProfileDTO>(
                PropwashWakeProfileCapacity,
                VaultOwnerSystem,
                Allocator.Persistent,
                NativeArrayOptions.ClearMemory);
            return _wakeProfileParseScratch.IsCreated &&
                   _wakeProfileParseScratch.Length >= PropwashWakeProfileCapacity;
        }

        private void DisposeWakeProfileParseScratch()
        {
            if (!_wakeProfileParseScratch.IsCreated)
                return;

            H8Memory.Release(ref _wakeProfileParseScratch, VaultOwnerSystem);
        }

        private void CsvProfileBackgroundReadLoop()
        {
            long lastReadTicks = 0L;
            long lastWakeReadTicks = 0L;
            while (!_csvProfileThreadStopRequested)
            {
                string path = _csvProfilePath;
                if (!string.IsNullOrEmpty(path))
                    lastReadTicks = TryStageCsvProfileFromDisk(path, lastReadTicks);

                string wakePath = _wakeProfilePath;
                if (!string.IsNullOrEmpty(wakePath))
                    lastWakeReadTicks = TryStageWakeProfileFromDisk(wakePath, lastWakeReadTicks);

                int waitMilliseconds = (int)(CsvProfilePollIntervalSeconds * 1000f);
                while (waitMilliseconds > 0 && !_csvProfileThreadStopRequested)
                {
                    int slice = math.min(CsvProfilePollSliceMilliseconds, waitMilliseconds);
                    Thread.Sleep(slice);
                    waitMilliseconds -= slice;
                }
            }
        }

        private long TryStageCsvProfileFromDisk(string path, long lastReadTicks)
        {
            try
            {
                if (!File.Exists(path))
                    return lastReadTicks;

                long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks == lastReadTicks)
                    return lastReadTicks;

                int bytesRead = ReadSiltProfileCsvBytes(path, _csvProfileBackgroundBuffer);
                if (bytesRead <= 0)
                    return ticks;

                lock (_csvProfileSync)
                {
                    System.Buffer.BlockCopy(_csvProfileBackgroundBuffer, 0, _csvProfileStagedBuffer, 0, bytesRead);
                    _csvProfileStagedLength = bytesRead;
                    _csvProfileStagedTicks = ticks;
                    _csvProfileStagedDirty = true;
                }

                return ticks;
            }
            catch (IOException)
            {
                return lastReadTicks;
            }
            catch (UnauthorizedAccessException)
            {
                return lastReadTicks;
            }
        }

        private long TryStageWakeProfileFromDisk(string path, long lastReadTicks)
        {
            try
            {
                if (!File.Exists(path))
                    return lastReadTicks;

                long ticks = File.GetLastWriteTimeUtc(path).Ticks;
                if (ticks == lastReadTicks)
                    return lastReadTicks;

                int bytesRead = ReadSiltProfileCsvBytes(path, _wakeProfileBackgroundBuffer);
                if (bytesRead <= 0)
                    return ticks;

                lock (_wakeProfileSync)
                {
                    System.Buffer.BlockCopy(_wakeProfileBackgroundBuffer, 0, _wakeProfileStagedBuffer, 0, bytesRead);
                    _wakeProfileStagedLength = bytesRead;
                    _wakeProfileStagedTicks = ticks;
                    _wakeProfileStagedDirty = true;
                }

                return ticks;
            }
            catch (IOException)
            {
                return lastReadTicks;
            }
            catch (UnauthorizedAccessException)
            {
                return lastReadTicks;
            }
        }

        private void RefreshSiltProfileCsv()
        {
            EnsureCsvProfileBackgroundReader();
            if (!_csvProfileStagedDirty)
                return;

            int bytesRead;
            long ticks;
            if (!Monitor.TryEnter(_csvProfileSync))
                return;

            try
            {
                if (!_csvProfileStagedDirty)
                    return;

                bytesRead = _csvProfileStagedLength;
                ticks = _csvProfileStagedTicks;
                System.Buffer.BlockCopy(_csvProfileStagedBuffer, 0, _csvProfileReadBuffer, 0, bytesRead);
                _csvProfileStagedDirty = false;
            }
            finally
            {
                Monitor.Exit(_csvProfileSync);
            }

            if (ticks == _csvProfileAppliedTicks)
                return;
            if (bytesRead <= 0)
                return;

            VfxConfigurationDTO current = _cachedSiltTuning.Version != 0u
                ? _cachedSiltTuning
                : CreateDefaultSiltTuning();
            if (TryReadSiltTuning(out NativeArray<VfxConfigurationDTO>.ReadOnly readOnlyTuning))
            {
                VfxConfigurationDTO published = readOnlyTuning[0];
                if (published.Version != 0u)
                    current = published;
            }

            IDataVault vault = _dataVault;
            if (!VolumetricSiltCsvParser.TryParse(_csvProfileReadBuffer, bytesRead, ref current, out _) ||
                !TryAcquireOwnedVaultWriteBuffer(vault, in _siltTuningHandle, BufferID.MarineSnowTuningConstants, 1, out NativeArray<VfxConfigurationDTO> tuning))
            {
                return;
            }

            try
            {
                tuning[0] = current;
                _cachedSiltTuning = current;
                _csvProfileAppliedTicks = ticks;
                _staticBindingsDirty = _buffersReady;
            }
            finally
            {
                vault.ReleaseWriteLock(in _siltTuningHandle, VaultOwnerSystem);
            }
        }

#if UNITY_EDITOR
        private void RefreshPropwashWakeProfileCsv()
        {
            if (!_wakeProfileStagedDirty)
                return;

            int bytesRead;
            long ticks;
            if (!Monitor.TryEnter(_wakeProfileSync))
                return;

            try
            {
                if (!_wakeProfileStagedDirty)
                    return;

                bytesRead = _wakeProfileStagedLength;
                ticks = _wakeProfileStagedTicks;
                System.Buffer.BlockCopy(_wakeProfileStagedBuffer, 0, _wakeProfileReadBuffer, 0, bytesRead);
                _wakeProfileStagedDirty = false;
            }
            finally
            {
                Monitor.Exit(_wakeProfileSync);
            }

            if (ticks == _wakeProfileAppliedTicks)
                return;
            if (bytesRead <= 0 || !EnsureWakeProfileParseScratch())
                return;

            NativeArray<PropwashWakeProfileDTO> scratch = _wakeProfileParseScratch;
            if (!PropwashGpuProfileCsvParser.TryParseWakeProfiles(
                new ReadOnlySpan<byte>(_wakeProfileReadBuffer, 0, bytesRead),
                scratch,
                out int profileCount,
                out uint fileHash))
            {
                return;
            }

            int safeProfileCount = math.clamp(profileCount, 0, scratch.Length);
            for (int i = safeProfileCount; i < scratch.Length; i++)
                scratch[i] = default;

            PropwashWakeProfileDTO first = scratch[0];
            first.Reserved0 = fileHash;
            scratch[0] = first;

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _propwashWakeProfileHandle, BufferID.PropwashGpuWakeProfiles, PropwashWakeProfileCapacity, out NativeArray<PropwashWakeProfileDTO> profiles))
                return;

            try
            {
                int copyCount = math.min(profiles.Length, scratch.Length);
                for (int i = 0; i < copyCount; i++)
                    profiles[i] = scratch[i];
                _wakeProfileAppliedTicks = ticks;
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashWakeProfileHandle, VaultOwnerSystem);
            }
        }
#endif

        private int ReadSiltProfileCsvBytes(string path, byte[] target)
        {
            try
            {
                using (FileStream stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite,
                    target.Length,
                    FileOptions.SequentialScan))
                {
                    long streamLength = stream.Length;
                    int cappedLength = streamLength <= 0L
                        ? 0
                        : streamLength > target.Length
                            ? target.Length
                            : (int)streamLength;
                    int offset = 0;
                    while (offset < cappedLength)
                    {
                        int read = stream.Read(target, offset, cappedLength - offset);
                        if (read <= 0)
                            break;
                        offset += read;
                    }

                    return offset;
                }
            }
            catch (IOException)
            {
                return 0;
            }
        }
#endif

        private static int ResolveSafeGpuWriteCount<T>(GraphicsBuffer buffer, int requestedCount) where T : struct
        {
            if (buffer == null || requestedCount <= 0 || buffer.count <= 0)
                return 0;
            if (buffer.stride != UnsafeUtility.SizeOf<T>())
                return 0;

            return math.min(requestedCount, buffer.count);
        }

        private static void UploadSingleGraphicsBuffer<T>(GraphicsBuffer buffer, T value) where T : struct
        {
            GraphicsBufferUploadUtility.TryUploadSingle(buffer, value);
        }

        private static void ClearGraphicsBuffer<T>(GraphicsBuffer buffer, int requestedCount) where T : struct
        {
            GraphicsBufferUploadUtility.TryClear<T>(buffer, requestedCount);
        }

        private static DynamicWakeDTO SanitizeDynamicWake(DynamicWakeDTO wake)
        {
            if (!math.all(math.isfinite(wake.Position)) ||
                !math.all(math.isfinite(wake.Force)) ||
                !math.isfinite(wake.Radius) ||
                !math.isfinite(wake.Falloff))
                return default;

            wake.Radius = math.max(0f, wake.Radius);
            wake.Falloff = math.max(0f, wake.Falloff);
            return wake;
        }

        private void ClearMockWakeGpuBuffers()
        {
            ClearGraphicsBuffer<DynamicWakeDTO>(_mockWakeDtoBuffer, MockWakeCapacity);
            ClearGraphicsBuffer<Vector4>(_mockWakeBuffer, MockWakeCapacity);
            ClearGraphicsBuffer<Vector4>(_mockWakeVectorBuffer, MockWakeCapacity);
        }

        private void UploadMockWakeGpuBuffers(NativeArray<DynamicWakeDTO>.ReadOnly wakes, int activeCount)
        {
            int dtoCount = ResolveSafeGpuWriteCount<DynamicWakeDTO>(_mockWakeDtoBuffer, MockWakeCapacity);
            int wakeCount = ResolveSafeGpuWriteCount<Vector4>(_mockWakeBuffer, MockWakeCapacity);
            int vectorCount = ResolveSafeGpuWriteCount<Vector4>(_mockWakeVectorBuffer, MockWakeCapacity);
            int safeCount = math.min(dtoCount, math.min(wakeCount, vectorCount));
            if (safeCount <= 0)
                return;

            long uploadBytes =
                GraphicsBufferUploadUtility.EstimateUploadBytes<DynamicWakeDTO>(safeCount) +
                GraphicsBufferUploadUtility.EstimateUploadBytes<Vector4>(safeCount) +
                GraphicsBufferUploadUtility.EstimateUploadBytes<Vector4>(safeCount);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return;

            bool uploadAccepted = false;
            try
            {
                int sourceCount = wakes.IsCreated ? wakes.Length : 0;
                int enabledCount = math.min(math.max(0, activeCount), sourceCount);
                if (!TryWriteMockWakeDtoGpuBuffer(wakes, safeCount, enabledCount))
                    return;
                if (!TryWriteMockWakePackedGpuBuffer(_mockWakeBuffer, wakes, safeCount, enabledCount, false))
                    return;
                if (!TryWriteMockWakePackedGpuBuffer(_mockWakeVectorBuffer, wakes, safeCount, enabledCount, true))
                    return;
                uploadAccepted = true;
            }
            finally
            {
                if (uploadAccepted)
                    GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                else
                    GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
            }
        }

        private bool TryWriteMockWakeDtoGpuBuffer(NativeArray<DynamicWakeDTO>.ReadOnly wakes, int safeCount, int enabledCount)
        {
            bool locked = false;
            NativeArray<DynamicWakeDTO> map = default;
            try
            {
                map = _mockWakeDtoBuffer.LockBufferForWrite<DynamicWakeDTO>(0, safeCount);
                locked = true;
                for (int i = 0; i < safeCount; i++)
                    map[i] = i < enabledCount ? SanitizeDynamicWake(wakes[i]) : default;
                return true;
            }
            finally
            {
                if (locked)
                    _mockWakeDtoBuffer.UnlockBufferAfterWrite<DynamicWakeDTO>(safeCount);
            }
        }

        private bool TryWriteMockWakePackedGpuBuffer(
            GraphicsBuffer buffer,
            NativeArray<DynamicWakeDTO>.ReadOnly wakes,
            int safeCount,
            int enabledCount,
            bool writeForceVector)
        {
            bool locked = false;
            NativeArray<Vector4> map = default;
            try
            {
                map = buffer.LockBufferForWrite<Vector4>(0, safeCount);
                locked = true;
                for (int i = 0; i < safeCount; i++)
                {
                    if (i >= enabledCount)
                    {
                        map[i] = Vector4.zero;
                        continue;
                    }

                    DynamicWakeDTO wake = SanitizeDynamicWake(wakes[i]);
                    map[i] = writeForceVector
                        ? new Vector4(wake.Force.x, wake.Force.y, wake.Force.z, math.max(0.001f, wake.Radius))
                        : new Vector4(wake.Position.x, wake.Position.y, wake.Position.z, math.length(wake.Force) * wake.Falloff);
                }
                return true;
            }
            finally
            {
                if (locked)
                    buffer.UnlockBufferAfterWrite<Vector4>(safeCount);
            }
        }

        private void UploadPropwashEventGpuBuffer(NativeArray<PropwashEventDTO>.ReadOnly events, PropwashRingCursorDTO cursor)
        {
            GraphicsBuffer uploadBuffer = ResolvePropwashEventUploadBufferCandidate();
            int safeCount = ResolveSafeGpuWriteCount<PropwashEventDTO>(uploadBuffer, PropwashEventRingCapacity);
            int sourceCount = events.IsCreated ? events.Length : 0;
            int enabledCount = math.min(math.max(0, cursor.EventCount), math.min(sourceCount, safeCount));
            int sourceStart = ComputePropwashUploadStart(cursor.WriteCursor, enabledCount, sourceCount);
            if (safeCount <= 0 || enabledCount <= 0)
            {
                _debugPropwashEventCount = 0;
                _debugPropwashGpuEventCount = 0;
                _debugPropwashMaxIntensity = 0f;
                _debugPropwashStrongestLocalPosition = default;
                return;
            }

            long uploadBytes = GraphicsBufferUploadUtility.EstimateUploadBytes<PropwashEventDTO>(safeCount);
            if (!GraphicsBufferUploadUtility.TryBeginManualUpload(uploadBytes))
                return;

            float maxIntensity = 0f;
            float3 strongestLocalPosition = default;
            bool bufferLocked = false;
            bool uploadAccepted = false;
            bool unlockSucceeded = false;
            NativeArray<PropwashEventDTO> mapped = default;
            try
            {
                mapped = uploadBuffer.LockBufferForWrite<PropwashEventDTO>(0, safeCount);
                bufferLocked = true;
                for (int i = 0; i < safeCount; i++)
                {
                    if (i < enabledCount)
                    {
                        int sourceIndex = WrapPropwashUploadIndex(sourceStart + i, sourceCount);
                        PropwashEventDTO evt = SanitizePropwashEvent(events[sourceIndex]);
                        mapped[i] = evt;
                        if (evt.Intensity > maxIntensity)
                        {
                            maxIntensity = evt.Intensity;
                            strongestLocalPosition = evt.LocalPosition;
                        }
                    }
                    else
                    {
                        mapped[i] = default;
                    }
                }
                uploadAccepted = true;
            }
            finally
            {
                try
                {
                    if (bufferLocked)
                    {
                        uploadBuffer.UnlockBufferAfterWrite<PropwashEventDTO>(safeCount);
                        unlockSucceeded = true;
                    }
                }
                finally
                {
                    if (uploadAccepted && unlockSucceeded)
                        GraphicsBufferUploadUtility.CompleteManualUpload(uploadBytes);
                    else
                        GraphicsBufferUploadUtility.CancelManualUpload(uploadBytes);
                }
            }

            CommitPropwashEventUploadBuffer(uploadBuffer);
            _debugPropwashEventCount = enabledCount;
            _debugPropwashGpuEventCount = enabledCount;
            _debugPropwashMaxIntensity = maxIntensity;
            _debugPropwashStrongestLocalPosition = strongestLocalPosition;
            _boundPropwashEventBuffer = null;
        }

        private static int ComputePropwashUploadStart(int writeCursor, int eventCount, int capacity)
        {
            if (capacity <= 0 || eventCount <= 0)
                return 0;

            return WrapPropwashUploadIndex(writeCursor - eventCount, capacity);
        }

        private static int WrapPropwashUploadIndex(int value, int capacity)
        {
            int safeCapacity = math.max(1, capacity);
            int wrapped = value % safeCapacity;
            return wrapped < 0 ? wrapped + safeCapacity : wrapped;
        }

        private GraphicsBuffer ResolvePropwashEventUploadBufferCandidate()
        {
            GraphicsBuffer candidate = _propwashEventUploadWriteIndex == 0
                ? _propwashEventBufferA
                : _propwashEventBufferB;
            if (candidate == null)
                candidate = _propwashEventBufferA != null ? _propwashEventBufferA : _propwashEventBufferB;

            return candidate;
        }

        private void CommitPropwashEventUploadBuffer(GraphicsBuffer uploadBuffer)
        {
            _propwashEventBuffer = uploadBuffer;
            _propwashEventUploadWriteIndex = uploadBuffer == _propwashEventBufferA ? 1 : 0;
        }

        private static PropwashEventDTO SanitizePropwashEvent(PropwashEventDTO evt)
        {
            if (!math.all(math.isfinite(evt.LocalPosition)) ||
                !math.all(math.isfinite(evt.ThrustVector)) ||
                !math.isfinite(evt.Intensity) ||
                !math.isfinite(evt.Radius))
                return default;

            evt.Intensity = math.max(0f, evt.Intensity);
            evt.Radius = math.clamp(evt.Radius, 0f, 32f);
            return evt;
        }

        private static float TriangleSignedTurns(float phaseTurns)
        {
            float t = math.frac(phaseTurns);
            return (math.abs(t * 2f - 1f) * 2f) - 1f;
        }

        private static MockFlowField BuildMockFlowFieldSnapshot(Vector3 cameraPosition, float timeSeconds, float curlStrength)
        {
            float phaseA = timeSeconds * 0.17f;
            float phaseB = timeSeconds * 0.113f + 1.37f;
            return new MockFlowField
            {
                GlobalFlow = new float3(
                    TriangleSignedTurns(phaseA * InvTau) * 0.035f,
                    0f,
                    TriangleSignedTurns((phaseB * InvTau) + 0.25f) * 0.035f),
                CurlStrength = math.max(0f, curlStrength),
                NoiseAnchor = cameraPosition,
                DensityScale = 1f
            };
        }

        private bool TryWriteMockFlowFieldToVault(in MockFlowField flowField)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _mockFlowFieldHandle, BufferID.MarineSnowMockFlowField, 1, out NativeArray<MockFlowField> buffer))
                return false;

            try
            {
                buffer[0] = flowField;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _mockFlowFieldHandle, VaultOwnerSystem);
            }
        }

        private int WriteMockWakeBuffer(NativeArray<DynamicWakeDTO> wakes, int activeCount)
        {
            if (!wakes.IsCreated)
                return 0;

            Vector3 cameraPosition = targetCamera != null ? targetCamera.position : Vector3.zero;
            Vector3 cameraRightVector = targetCamera != null ? targetCamera.right : Vector3.right;
            Vector3 cameraUpVector = targetCamera != null ? targetCamera.up : Vector3.up;
            float3 cameraRight = new float3(cameraRightVector.x, cameraRightVector.y, cameraRightVector.z);
            float3 cameraUp = new float3(cameraUpVector.x, cameraUpVector.y, cameraUpVector.z);
            float3 cameraForward = math.normalizesafe(math.cross(cameraRight, cameraUp), new float3(0f, 0f, 1f));
            int length = wakes.Length;
            int active = math.clamp(activeCount, 0, math.min(length, MockWakeCapacity));
            for (int i = 0; i < length; i++)
            {
                if (i >= active)
                {
                    wakes[i] = default;
                    continue;
                }

                float lane = i + 1f;
                float phase = _simulationTime * (0.73f + lane * 0.19f) + lane * 1.6180339f;
                float lateral = TriangleSignedTurns(phase * InvTau) * (1.2f + lane * 0.35f);
                float vertical = TriangleSignedTurns((phase * 0.71f * InvTau) + 0.25f) * 0.42f;
                float radius = 4.75f + lane * 1.15f;
                float3 force =
                    cameraForward * (0.42f + lane * 0.08f) +
                    cameraRight * (TriangleSignedTurns((phase * 1.37f * InvTau) + 0.25f) * 0.22f) +
                    cameraUp * (TriangleSignedTurns(phase * 0.53f * InvTau) * 0.08f);
                wakes[i] = new DynamicWakeDTO
                {
                    Position = new float3(cameraPosition.x, cameraPosition.y, cameraPosition.z) - cameraForward * (3.5f + lane * 2.0f) + cameraRight * lateral + cameraUp * vertical,
                    Radius = radius,
                    Force = force,
                    Falloff = math.saturate(1.0f - i * 0.18f)
                };
            }

            return active;
        }

        private bool TryWriteMockWakeVaultAndGpu(int requestedActiveCount, out int writtenCount)
        {
            writtenCount = 0;
            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(
                    vault,
                    in _dynamicWakeDtoHandle,
                    BufferID.MarineSnowDynamicWakes,
                    DynamicWakeDtoCapacity,
                    out NativeArray<DynamicWakeDTO> wakes))
            {
                return false;
            }

            try
            {
                writtenCount = WriteMockWakeBuffer(wakes, requestedActiveCount);
                if (writtenCount <= 0)
                {
                    ClearMockWakeGpuBuffers();
                    return false;
                }

            }
            finally
            {
                vault.ReleaseWriteLock(in _dynamicWakeDtoHandle, VaultOwnerSystem);
            }

            if (!TryReadReadyDynamicWakes(out NativeArray<DynamicWakeDTO>.ReadOnly readyWakes))
            {
                ClearMockWakeGpuBuffers();
                return false;
            }

            UploadMockWakeGpuBuffers(readyWakes, writtenCount);
            return true;
        }

        private bool TryReadPropwashCursorSnapshot(out PropwashRingCursorDTO cursor)
        {
            cursor = default;
            if (!TryReadReadyPropwashCursor(out NativeArray<PropwashRingCursorDTO>.ReadOnly cursorRing) ||
                cursorRing.Length <= 0)
            {
                return false;
            }

            cursor = cursorRing[0];
            return true;
        }

        private bool TryWritePropwashCursorToVault(in PropwashRingCursorDTO cursor)
        {
            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(
                    vault,
                    in _propwashCursorHandle,
                    BufferID.PropwashGpuRingCursor,
                    1,
                    out NativeArray<PropwashRingCursorDTO> cursorRing))
            {
                return false;
            }

            try
            {
                cursorRing[0] = cursor;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashCursorHandle, VaultOwnerSystem);
            }
        }

        private bool TryPublishPropwashEvents(in PropwashRingCursorDTO cursor)
        {
            if (!TryReadReadyPropwashEvents(out NativeArray<PropwashEventDTO>.ReadOnly events))
                return false;

            // Cursor is published after the event write-lock is released so readers never observe a new count before payload visibility.
            if (!TryWritePropwashCursorToVault(in cursor))
                return false;

            UploadPropwashEventGpuBuffer(events, cursor);
            return true;
        }

        private bool TryPublishPropwashCursorOnly(in PropwashRingCursorDTO cursor)
        {
            if (!TryWritePropwashCursorToVault(in cursor))
                return false;

            ResetPropwashDebugState();
            return true;
        }

        private void ResetPropwashDebugState()
        {
            _debugPropwashEventCount = 0;
            _debugPropwashGpuEventCount = 0;
            _debugPropwashMaxIntensity = 0f;
            _debugPropwashStrongestLocalPosition = default;
        }

        private bool TryAppendVehicleWakePropwashEvent(
            NativeArray<PropwashEventDTO> events,
            float3 localPosition,
            float3 thrustVector,
            float intensity,
            float radius,
            int frame,
            float globalQualityWeight,
            uint profileHash,
            ref PropwashRingCursorDTO cursor)
        {
            int capacity = events.IsCreated ? events.Length : 0;
            if (capacity <= 0)
                return false;

            float safeIntensity = math.max(0f, intensity);
            float safeRadius = math.clamp(radius, 0.25f, 32f);
            if (safeIntensity <= 0.0001f ||
                !math.all(math.isfinite(localPosition)) ||
                !math.all(math.isfinite(thrustVector)) ||
                !math.isfinite(safeRadius))
            {
                return false;
            }

            int previousCount = math.clamp(cursor.EventCount, 0, capacity);
            int write = WrapPropwashUploadIndex(cursor.WriteCursor, capacity);
            events[write] = new PropwashEventDTO
            {
                LocalPosition = localPosition,
                ThrustVector = thrustVector,
                Intensity = safeIntensity,
                Radius = safeRadius
            };

            int nextCount = math.min(capacity, previousCount + 1);
            cursor.WriteCursor = WrapPropwashUploadIndex(write + 1, capacity);
            cursor.EventCount = nextCount;
            if (previousCount >= capacity && cursor.DroppedCount < int.MaxValue)
                cursor.DroppedCount++;
            cursor.LastFrame = frame;
            cursor.GlobalQualityWeight = math.saturate(globalQualityWeight);
            cursor.StateHash = PropwashGpuContracts.HashState(frame, nextCount, cursor.GlobalQualityWeight, profileHash);
            cursor.Flags = nextCount > 0 ? (cursor.Flags | PropwashGpuContracts.VehicleWakeSourceFlag) : 0u;
            return true;
        }

        private bool TryBuildMockPropwashEvents(
            NativeArray<PropwashEventDTO> events,
            int requestedCount,
            float timeSeconds,
            float globalQualityWeight,
            int frame,
            ref PropwashRingCursorDTO cursor)
        {
            int capacity = events.IsCreated ? events.Length : 0;
            if (capacity <= 0)
                return false;

            int eventCount = math.clamp(requestedCount, 0, math.min(capacity, PropwashGpuContracts.MockEventCount));
            int baseCursor = WrapPropwashUploadIndex(cursor.WriteCursor, capacity);
            float quality = math.saturate(globalQualityWeight);
            float radiusScale = math.lerp(0.62f, 1.35f, quality);
            float forceScale = math.lerp(0.45f, 1.85f, quality);

            for (int i = 0; i < eventCount; i++)
            {
                float lane = i + 1f;
                float lane01 = lane * math.rcp(math.max(1f, eventCount));
                float phase = timeSeconds * (0.23f + lane01 * 0.41f) + lane * 0.013671875f;
                float side = TriangleSignedTurns(phase) * (0.35f + 7.5f * lane01);
                float lift = TriangleSignedTurns(phase * 0.37f + 0.25f) * (0.18f + 0.75f * lane01);
                float range = 1.5f + lane01 * 18f;
                float swirl = TriangleSignedTurns(phase * 0.71f + 0.5f);
                float safeIntensity = math.saturate(0.18f + lane01 * 0.82f) * forceScale;
                float safeRadius = (1.15f + 5.25f * lane01) * radiusScale;
                int slot = WrapPropwashUploadIndex(baseCursor + i, capacity);

                events[slot] = new PropwashEventDTO
                {
                    LocalPosition = new float3(side, lift - 0.35f, -range),
                    ThrustVector = new float3(swirl * 0.28f, math.max(0.02f, safeIntensity * 0.11f), -safeIntensity),
                    Intensity = safeIntensity,
                    Radius = safeRadius
                };
            }

            cursor.WriteCursor = WrapPropwashUploadIndex(baseCursor + eventCount, capacity);
            cursor.EventCount = eventCount;
            cursor.DroppedCount = math.max(0, requestedCount - eventCount);
            cursor.LastFrame = frame;
            cursor.GlobalQualityWeight = quality;
            cursor.StateHash = PropwashGpuContracts.HashState(frame, eventCount, quality, 0u);
            cursor.Flags = eventCount > 0 ? PropwashGpuContracts.MockSourceFlag : 0u;
            return true;
        }

        private bool TryBuildAndPublishMockPropwashEvents(
            int requestedCount,
            float timeSeconds,
            float globalQualityWeight,
            int frame,
            out PropwashRingCursorDTO cursor)
        {
            cursor = default;
            if (!TryReadPropwashCursorSnapshot(out cursor))
                return false;

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(
                    vault,
                    in _propwashEventHandle,
                    BufferID.PropwashGpuEventRing,
                    PropwashEventRingCapacity,
                    out NativeArray<PropwashEventDTO> events))
            {
                return false;
            }

            bool publishEvents = false;
            try
            {
                publishEvents = TryBuildMockPropwashEvents(
                    events,
                    requestedCount,
                    timeSeconds,
                    globalQualityWeight,
                    frame,
                    ref cursor);
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashEventHandle, VaultOwnerSystem);
            }

            if (!publishEvents)
                return false;

            return cursor.EventCount > 0
                ? TryPublishPropwashEvents(in cursor)
                : TryPublishPropwashCursorOnly(in cursor);
        }

        private bool TryAppendWakeSourcePropwash(
            NativeArray<PropwashEventDTO> events,
            NativeArray<WakeSource>.ReadOnly wakeSources,
            double3 cameraAup,
            int sourceScanLimit,
            int writeLimit,
            int frame,
            float globalQualityWeight,
            uint profileHash,
            ref PropwashRingCursorDTO cursor)
        {
            int capacity = events.IsCreated ? events.Length : 0;
            if (capacity <= 0 || !wakeSources.IsCreated || wakeSources.Length <= 0)
                return false;

            int scanLimit = math.clamp(sourceScanLimit, 0, wakeSources.Length);
            int safeWriteLimit = math.clamp(writeLimit, 0, math.min(capacity, scanLimit));
            if (scanLimit <= 0 || safeWriteLimit <= 0)
                return false;

            int previousCount = math.clamp(cursor.EventCount, 0, capacity);
            int writeCursor = WrapPropwashUploadIndex(cursor.WriteCursor, capacity);
            int dropped = math.max(0, cursor.DroppedCount);
            int written = 0;
            float quality = math.saturate(globalQualityWeight);
            float forceScale = math.lerp(0.55f, 1.45f, quality);
            float radiusScale = math.lerp(0.75f, 1.65f, quality);

            for (int i = 0; i < scanLimit && written < safeWriteLimit; i++)
            {
                WakeSource source = wakeSources[i];
                byte sourceKind = source.SourceKind != 0
                    ? source.SourceKind
                    : (byte)(source.SourceFlags & 0xFFu);
                if (source.Active == 0 ||
                    (sourceKind != PropwashGpuContracts.WakeSourceVehicle &&
                     sourceKind != PropwashGpuContracts.WakeSourceApexPredator))
                {
                    continue;
                }

                float3 velocity = source.VelocityWS;
                float sourceIntensity = math.max(0f, source.Intensity);
                float sourceRadius = math.max(0.05f, source.Radius);
                double3 localDouble = source.PositionAup.ToAbsoluteDouble3() - cameraAup;
                float3 localPosition = new float3((float)localDouble.x, (float)localDouble.y, (float)localDouble.z);
                float speedSq = math.lengthsq(velocity);
                if (sourceIntensity <= 0.0001f ||
                    sourceRadius <= 0.05f ||
                    speedSq <= 0.0001f ||
                    !math.all(math.isfinite(localPosition)) ||
                    !math.all(math.isfinite(velocity)))
                {
                    continue;
                }

                float invSpeed = math.rsqrt(math.max(speedSq, 0.0001f));
                float3 direction = velocity * invSpeed;
                float faunaWeight = sourceKind == PropwashGpuContracts.WakeSourceApexPredator ? 0.72f : 1f;
                int slot = WrapPropwashUploadIndex(writeCursor + written, capacity);
                events[slot] = new PropwashEventDTO
                {
                    LocalPosition = localPosition,
                    ThrustVector = direction * (sourceIntensity * forceScale * faunaWeight),
                    Intensity = math.saturate(sourceIntensity * faunaWeight),
                    Radius = math.clamp(sourceRadius * radiusScale, 0.25f, 32f)
                };

                if (previousCount >= capacity)
                {
                    if (dropped < int.MaxValue)
                        dropped++;
                }
                else
                {
                    previousCount = math.min(capacity, previousCount + 1);
                }

                written++;
            }

            if (written <= 0)
                return false;

            cursor.WriteCursor = WrapPropwashUploadIndex(writeCursor + written, capacity);
            cursor.EventCount = previousCount;
            cursor.DroppedCount = dropped;
            cursor.LastFrame = frame;
            cursor.GlobalQualityWeight = quality;
            cursor.StateHash = PropwashGpuContracts.HashState(frame, previousCount, quality, profileHash);
            cursor.Flags = cursor.EventCount > 0
                ? (cursor.Flags | PropwashGpuContracts.WakeSourceBridgeFlag)
                : cursor.Flags;
            return true;
        }

        private void RefreshMockWakeSignals(float dt)
        {
            _mockWakeUploadTimer = math.max(0f, _mockWakeUploadTimer - dt);
            if (_mockWakeUploadTimer > 0f)
                return;

            _mockWakeUploadTimer = MockWakeUploadIntervalSeconds;
            if (_mockWakeDtoBuffer == null || _mockWakeBuffer == null || _mockWakeVectorBuffer == null)
            {
                _debugMockWakeCount = 0;
                return;
            }

            bool mockWakeActive = AreMockWakeSignalsAllowed() && _underwaterActive && targetCamera != null;
            if (!mockWakeActive)
            {
                ClearInactiveVisualWakeState();
                return;
            }

            _mockWakeBuffersCleared = false;
            int activeCount = MockWakeCapacity;
            MockFlowField flowField = BuildMockFlowFieldSnapshot(
                targetCamera != null ? targetCamera.position : Vector3.zero,
                _simulationTime,
                ResolveFlowParams().z);
            if (TryWriteMockFlowFieldToVault(in flowField))
                _cachedMockFlowField = flowField;

            float propwashQuality = ResolvePropwashQualityWeight();
            int activePropwashCount = mockWakeActive ? ResolveMockPropwashEventCount(propwashQuality) : 0;
            if (TryBuildAndPublishMockPropwashEvents(
                    activePropwashCount,
                    _simulationTime,
                    propwashQuality,
                    unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                    out PropwashRingCursorDTO propwashCursor))
            {
                if (propwashCursor.EventCount <= 0)
                    ResetPropwashDebugState();
            }
            else
            {
                activePropwashCount = 0;
                ResetPropwashDebugState();
            }

            if (TryWriteMockWakeVaultAndGpu(activeCount, out int writtenWakeCount))
            {
                activeCount = writtenWakeCount;
            }
            else
            {
                activeCount = 0;
                ClearMockWakeGpuBuffers();
            }

            _debugMockWakeCount = activeCount;
        }

        private void ClearMockOnlyPropwashCursor()
        {
            if (!TryReadPropwashCursorSnapshot(out PropwashRingCursorDTO cursor) ||
                (cursor.Flags & PropwashGpuContracts.MockSourceFlag) == 0u ||
                (cursor.Flags & ~PropwashGpuContracts.MockSourceFlag) != 0u)
            {
                return;
            }

            cursor.WriteCursor = 0;
            cursor.EventCount = 0;
            cursor.DroppedCount = 0;
            cursor.LastFrame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId);
            cursor.StateHash = PropwashGpuContracts.HashState(cursor.LastFrame, 0, 0f, 0u);
            cursor.GlobalQualityWeight = 0f;
            cursor.Flags = 0u;
            TryPublishPropwashCursorOnly(in cursor);
        }

        private void HarvestProceduralWakeSourcesIntoPropwash()
        {
            if (targetCamera == null ||
                !TryReadReadyProceduralWakeSources(out NativeArray<WakeSource>.ReadOnly wakeSources) ||
                !TryReadPropwashCursorSnapshot(out PropwashRingCursorDTO propwashCursor) ||
                !TryResolveRuntimeAup(targetCamera.position, out AbsoluteUniversePosition cameraAup))
            {
                return;
            }

            int scanLimit = math.min(wakeSources.Length, ProceduralWakeSourceBridgeCapacity);
            if (scanLimit <= 0)
                return;

            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            float quality = tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
            int writeLimit = ResolveProceduralWakeSourceBridgeWriteLimit(quality, scanLimit);
            if (writeLimit <= 0)
                return;

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(
                    vault,
                    in _propwashEventHandle,
                    BufferID.PropwashGpuEventRing,
                    PropwashEventRingCapacity,
                    out NativeArray<PropwashEventDTO> events))
            {
                return;
            }

            bool publishEvents = false;
            try
            {
                PropwashRingCursorDTO before = propwashCursor;
                if (TryAppendWakeSourcePropwash(
                        events,
                        wakeSources,
                        cameraAup.ToAbsoluteDouble3(),
                        scanLimit,
                        writeLimit,
                        unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                        quality,
                        PropwashGpuContracts.DefaultWakeProfileHash,
                        ref propwashCursor) &&
                    (propwashCursor.WriteCursor != before.WriteCursor ||
                     propwashCursor.EventCount != before.EventCount ||
                     (propwashCursor.LastFrame == SystemDispatcher.CurrentFrameIndex &&
                      (propwashCursor.Flags & PropwashGpuContracts.WakeSourceBridgeFlag) != 0u)))
                {
                    publishEvents = true;
                }
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashEventHandle, VaultOwnerSystem);
            }

            if (publishEvents)
                TryPublishPropwashEvents(in propwashCursor);
        }

        private static int ResolveProceduralWakeSourceBridgeWriteLimit(float globalQualityWeight, int scanLimit)
        {
            int safeScanLimit = math.max(0, scanLimit);
            if (safeScanLimit <= 0)
                return 0;

            float q = math.saturate(globalQualityWeight);
            float curved = q * q * (3f - 2f * q);
            int writes = (int)math.round(math.lerp(
                ProceduralWakeSourceBridgeMinWrites,
                ProceduralWakeSourceBridgeCapacity,
                curved));
            return math.clamp(writes, 1, safeScanLimit);
        }

        private void RefreshMockAcousticSignal(float dt)
        {
            if (!AreMockAcousticSignalsAllowed() || !_underwaterActive || targetCamera == null)
            {
                _mockAcousticPulseTimer = 0f;
                _mockAcousticSignal = default;
                return;
            }

            _mockAcousticPulseTimer = math.max(0f, _mockAcousticPulseTimer - dt);
            if (_mockAcousticPulseTimer > 0f)
            {
                float age = _simulationTime - _mockAcousticSignal.StartTime;
                if (age > _mockAcousticSignal.Duration)
                    _mockAcousticSignal.Magnitude = 0f;
                return;
            }

            _mockAcousticPulseTimer = MockAcousticPulseIntervalSeconds;
            Vector3 origin = targetCamera.position +
                targetCamera.forward * 10f +
                targetCamera.right * ((math.frac(_simulationTime * 0.137f) - 0.5f) * 8f) +
                targetCamera.up * 1.5f;

            _mockAcousticSignal = new MockAcousticSignal
            {
                Position = origin,
                Radius = MockAcousticPulseRadius,
                Magnitude = MockAcousticPulseMagnitude,
                StartTime = _simulationTime,
                Duration = MockAcousticPulseDuration,
                WaveSpeed = MockAcousticPulseSpeed
            };
        }

        private bool AreMockWakeSignalsAllowed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return enableMockWakeSignals;
#else
            return false;
#endif
        }

        private bool AreMockAcousticSignalsAllowed()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return enableMockAcousticSignals;
#else
            return false;
#endif
        }

        private static int ResolveMockPropwashEventCount(float globalQualityWeight)
        {
            int safeMax = math.min(PropwashMockEventCount, PropwashEventRingCapacity);
            if (safeMax <= 0)
                return 0;

            float q = math.saturate(globalQualityWeight);
            float curved = q * q * (3f - 2f * q);
            int resolved = (int)math.round(math.lerp(PropwashEventMinSampleCapacity, safeMax, curved));
            return math.clamp(resolved, 0, safeMax);
        }

        private void UpdateBiolumeSurgeState(float dt)
        {
            IWeatherService weatherService = _weatherService;
            if (weatherService != null &&
                weatherService.IsInitialized &&
                (weatherService.CurrentWeatherState & WeatherState.BiolumeSurge) != 0)
            {
                _biolumeSurgeTimer = math.max(_biolumeSurgeTimer, BiolumeSurgeDurationSeconds);
            }
            else
            {
                _biolumeSurgeTimer = math.max(0f, _biolumeSurgeTimer - math.max(0f, dt));
            }

            _debugBiolumeSurgeBlend = ResolveBiolumeSurgeBlend();
        }

        private void ResolveTargetCameraCold()
        {
            if (targetCamera == null)
            {
                _targetCameraComponent = ResolveComponentInParents<Camera>(transform);
                targetCamera = _targetCameraComponent != null ? _targetCameraComponent.transform : null;
            }
            else if (_targetCameraComponent == null || _targetCameraComponent.transform != targetCamera)
            {
                _targetCameraComponent = ResolveComponentOnTransform<Camera>(targetCamera);
            }
        }

        private bool HasCachedTargetCamera()
        {
            Camera cameraComponent = _targetCameraComponent;
            Transform cameraTransform = targetCamera;
            if (cameraComponent == null || cameraTransform == null)
                return false;

            if (cameraComponent.transform == cameraTransform)
                return true;

            _targetCameraComponent = null;
            return false;
        }

        private void TryRegisterLateFrame()
        {
            if (_registeredLateFrame)
                return;
            if (!Application.isPlaying || !_dispatcherReady)
                return;

            _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterSlowTick()
        {
            if (_registeredSlowTick)
                return;
            if (!Application.isPlaying || !_dispatcherReady)
                return;

            _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterColdTick()
        {
            if (_registeredColdTick)
                return;
            if (!Application.isPlaying || !_dispatcherReady)
                return;

            _registeredColdTick = GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment);
        }

        private void EnsureBuffers()
        {
            if (_buffersReady)
                return;

            int clampedParticleCount = RefreshAndResolveConfiguredCapacity();
            if (marineSnowCompute == null ||
                marineSnowMaterial == null ||
                !_coldSupportsComputeShaders)
                return;

            RefreshAuthoredNeutralVolumeFallbacksColdEditor();
            RefreshMaterialFlipbookAtlasFallbackCold();

            if (emptyCaveSdfTexture3D == null || emptyAbyssalFlowTexture3D == null)
            {
                UnityEngine.Assertions.Assert.IsNotNull(emptyCaveSdfTexture3D, "Fatal: Missing authored neutral MarineSnow cave SDF Texture3D.");
                UnityEngine.Assertions.Assert.IsNotNull(emptyAbyssalFlowTexture3D, "Fatal: Missing authored neutral MarineSnow abyssal flow Texture3D.");
                enabled = false;
                return;
            }

            if (!TryResolveKernel("CSMain", out _kernelIndex))
            {
                LogMissingMainKernel();
                enabled = false;
                return;
            }

            if (!TryResolveKernel("InitializeParticles", out _initializeKernel))
            {
                LogMissingInitializeKernel();
                enabled = false;
                return;
            }

            if (!TryResolveKernel("ClearVisibleParticles", out _clearVisibleKernel))
            {
                LogMissingVisibleKernel();
                enabled = false;
                return;
            }

            if (!TryResolveKernel("ClearSonarGlow", out _sonarGlowClearKernel) ||
                !TryResolveKernel("AccumulateSonarGlow", out _sonarGlowAccumulateKernel) ||
                !TryResolveKernel("ClearFogDensity", out _fogDensityClearKernel))
            {
                LogMissingAuxiliaryKernels();
                enabled = false;
                return;
            }

            if (!TryResolveKernel("CS_EvaluateWakeProximity", out _wakeProximityKernel) ||
                !TryResolveKernel("CS_RebaseParticles", out _rebaseKernel))
            {
                LogMissingPropwashKernels();
                enabled = false;
                return;
            }

            if (!CacheKernelThreadGroupSizes())
            {
                LogInvalidKernelThreadGroups();
                enabled = false;
                return;
            }

            // COLD ALLOC: GraphicsBuffer[clampedParticleCount] - catalog-capped 32B persistent 16-byte aligned silt state ping-pong buffer A - owner: HectonMarineSnowRenderer
            _particleBufferA = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleDataDTO>(clampedParticleCount);
            // COLD ALLOC: GraphicsBuffer[clampedParticleCount] - catalog-capped 32B persistent 16-byte aligned silt state ping-pong buffer B - owner: HectonMarineSnowRenderer
            _particleBufferB = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleDataDTO>(clampedParticleCount);
            _particleMetaBufferA = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleRenderMetaDTO>(clampedParticleCount); // COLD ALLOC: GraphicsBuffer[clampedParticleCount] - 32B render metadata ping-pong A - owner: HectonMarineSnowRenderer
            _particleMetaBufferB = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleRenderMetaDTO>(clampedParticleCount); // COLD ALLOC: GraphicsBuffer[clampedParticleCount] - 32B render metadata ping-pong B - owner: HectonMarineSnowRenderer
            _frameConstantsBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FrameConstantsData>(1); // COLD ALLOC: GraphicsBuffer[1] - per-frame marine-snow constant buffer A - owner: HectonMarineSnowRenderer
            _frameConstantsBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FrameConstantsData>(1); // COLD ALLOC: GraphicsBuffer[1] - per-frame marine-snow constant buffer B - owner: HectonMarineSnowRenderer
            _activeFrameConstantsBuffer = _frameConstantsBufferA;
            _frameConstantsUploadBufferIndex = 1;
            _emptyFlowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(1); // COLD ALLOC: GraphicsBuffer[1] - zero fallback ecosystem flow-vector buffer - owner: HectonMarineSnowRenderer
            ClearGraphicsBuffer<float2>(_emptyFlowFieldBuffer, 1);
            int flowFieldCapacity = SanitizeFlowFieldUploadCapacity();
            _flowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(flowFieldCapacity); // COLD ALLOC: GraphicsBuffer[configured float2] - fixed ecosystem flow-field GPU snapshot staging, no runtime resize - owner: HectonMarineSnowRenderer
            _flowFieldBufferCapacity = flowFieldCapacity;
            ClearGraphicsBuffer<float2>(_flowFieldBuffer, _flowFieldBufferCapacity);
            _visibleParticleIndexBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<uint>(clampedParticleCount); // COLD ALLOC: GraphicsBuffer[clampedParticleCount] - GPU-written visible-particle index list - owner: HectonMarineSnowRenderer
            _indirectArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, ProceduralIndirectArgsStride); // COLD ALLOC: GraphicsBuffer[1] - GPU-written non-indexed procedural indirect args: vertexCount, instanceCount, startVertex, startInstance - owner: HectonMarineSnowRenderer
            _emptyAbyssalFlowBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(1); // COLD ALLOC: GraphicsBuffer[1] - zero fallback abyssal-flow vector buffer - owner: HectonMarineSnowRenderer
            ClearGraphicsBuffer<Vector4>(_emptyAbyssalFlowBuffer, 1);
            _mockWakeDtoBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<DynamicWakeDTO>(MockWakeCapacity); // COLD ALLOC: GraphicsBuffer[4] - local mock DynamicWakeDTO proof buffer - owner: HectonMarineSnowRenderer
            _mockWakeBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MockWakeCapacity); // COLD ALLOC: GraphicsBuffer[4] - legacy packed mock wake positions for existing fluid ABI - owner: HectonMarineSnowRenderer
            _mockWakeVectorBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<Vector4>(MockWakeCapacity); // COLD ALLOC: GraphicsBuffer[4] - legacy packed mock wake vectors for existing fluid ABI - owner: HectonMarineSnowRenderer
            _propwashEventBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<PropwashEventDTO>(PropwashEventRingCapacity); // COLD ALLOC: GraphicsBuffer[512] - PropwashEventDTO upload buffer A, avoids CPU/GPU write-read contention - owner: HectonMarineSnowRenderer
            _propwashEventBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<PropwashEventDTO>(PropwashEventRingCapacity); // COLD ALLOC: GraphicsBuffer[512] - PropwashEventDTO upload buffer B, inactive buffer receives next frame - owner: HectonMarineSnowRenderer
            _propwashEventBuffer = _propwashEventBufferA;
            _propwashEventUploadWriteIndex = 1;
            ClearMockWakeGpuBuffers();
            _maelstromBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FluidAnalyticalContractConstants.MaxActiveMaelstromCount); // COLD ALLOC: GraphicsBuffer[2] - compact maelstrom particle swirl buffer A - owner: HectonMarineSnowRenderer
            _maelstromBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(FluidAnalyticalContractConstants.MaxActiveMaelstromCount); // COLD ALLOC: GraphicsBuffer[2] - compact maelstrom particle swirl buffer B for CPU/GPU flip - owner: HectonMarineSnowRenderer
            _allocatedParticleCapacity = clampedParticleCount;
            _debugAllocatedParticleCapacity = clampedParticleCount;
            _frameParity = 0;
            _particleBuffersNeedGpuBootstrap = true;
            EnsureEmptyCaveSdfTexture();
            EnsureEmptyAbyssalFlowTexture();
            EnsureSonarGlowTexture();
            EnsureFogDensityTexture();
            _buffersReady = true;
            ResetGpuBindingCaches();
            _staticBindingsDirty = true;
            _externalGpuBindingsDirty = true;
        }

        private void EnsureParticleBudget()
        {
            RefreshScalabilityProfile();
#if UNITY_EDITOR
            if (Application.isPlaying)
                return;

            int configuredCapacity = ComputeConfiguredAllocationCapacity();
            if (configuredCapacity != _allocatedParticleCapacity)
                ResizeParticleBuffers(configuredCapacity);
#endif
        }

        private void ResizeParticleBuffers(int particleCount)
        {
            if (particleCount <= 0)
                return;

            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _particleMetaBufferA);
            ReleaseBuffer(ref _particleMetaBufferB);
            ReleaseBuffer(ref _visibleParticleIndexBuffer);

            // COLD ALLOC: GraphicsBuffer[particleCount] - resized 32B silt state ping-pong buffer A - owner: HectonMarineSnowRenderer
            _particleBufferA = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleDataDTO>(particleCount);
            // COLD ALLOC: GraphicsBuffer[particleCount] - resized 32B silt state ping-pong buffer B - owner: HectonMarineSnowRenderer
            _particleBufferB = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleDataDTO>(particleCount);
            _particleMetaBufferA = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleRenderMetaDTO>(particleCount); // COLD ALLOC: GraphicsBuffer[particleCount] - resized 32B render metadata ping-pong A - owner: HectonMarineSnowRenderer
            _particleMetaBufferB = GraphicsBufferUploadUtility.CreateStructuredBuffer<ParticleRenderMetaDTO>(particleCount); // COLD ALLOC: GraphicsBuffer[particleCount] - resized 32B render metadata ping-pong B - owner: HectonMarineSnowRenderer
            _visibleParticleIndexBuffer = GraphicsBufferUploadUtility.CreateStructuredBuffer<uint>(particleCount); // COLD ALLOC: GraphicsBuffer[particleCount] - resized GPU-written visible-particle index list - owner: HectonMarineSnowRenderer

            _allocatedParticleCapacity = particleCount;
            _debugAllocatedParticleCapacity = particleCount;
            _frameParity = 0;
            _particleBuffersNeedGpuBootstrap = true;
            ResetGpuBindingCaches();
            _staticBindingsDirty = true;
            _externalGpuBindingsDirty = true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingMainKernel()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel CSMain not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingInitializeKernel()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel InitializeParticles not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingVisibleKernel()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel ClearVisibleParticles not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingAuxiliaryKernels()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: auxiliary compute kernels not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingPropwashKernels()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: propwash compute kernels not found. Disabling compute marine snow.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogInvalidKernelThreadGroups()
        {
#if UNITY_EDITOR
            Hecton8.Core.H8Debug.LogError("HectonMarineSnowRenderer: compute kernel thread-group contract is invalid. Disabling compute marine snow.");
#endif
        }

        private bool TryResolveKernel(string kernelName, out int kernelIndex)
        {
            kernelIndex = -1;
            if (marineSnowCompute == null || !_coldSupportsComputeShaders)
                return false;

            try
            {
                if (!marineSnowCompute.HasKernel(kernelName))
                    return false;

                kernelIndex = marineSnowCompute.FindKernel(kernelName);
                return kernelIndex >= 0;
            }
            catch (System.ObjectDisposedException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (System.InvalidOperationException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (System.ArgumentException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (MissingReferenceException)
            {
                kernelIndex = -1;
                return false;
            }
            catch (UnityException)
            {
                kernelIndex = -1;
                return false;
            }
        }

        private bool CacheKernelThreadGroupSizes()
        {
            if (!TryResolveKernelThreadGroupSizeX(_kernelIndex, out _simulationThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_initializeKernel, out _initializeThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_sonarGlowAccumulateKernel, out _sonarGlowAccumulateThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_wakeProximityKernel, out _wakeProximityThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_rebaseKernel, out _rebaseThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_clearVisibleKernel, out _clearVisibleThreadGroupSize) ||
                !TryResolveKernelThreadGroupTile(
                    _sonarGlowClearKernel,
                    out _sonarGlowClearTileSizeX,
                    out _sonarGlowClearTileSizeY) ||
                !TryResolveKernelThreadGroupTile(
                    _fogDensityClearKernel,
                    out _fogDensityClearTileSizeX,
                    out _fogDensityClearTileSizeY))
            {
                ResetKernelThreadGroupSizes();
                return false;
            }

            return true;
        }

        private void ResetKernelThreadGroupSizes()
        {
            _simulationThreadGroupSize = 0;
            _initializeThreadGroupSize = 0;
            _clearVisibleThreadGroupSize = 0;
            _sonarGlowAccumulateThreadGroupSize = 0;
            _wakeProximityThreadGroupSize = 0;
            _rebaseThreadGroupSize = 0;
            _sonarGlowClearTileSizeX = 0;
            _sonarGlowClearTileSizeY = 0;
            _fogDensityClearTileSizeX = 0;
            _fogDensityClearTileSizeY = 0;
        }

        private bool TryResolveKernelThreadGroupSizeX(int kernelIndex, out int groupSizeX)
        {
            groupSizeX = 0;
            if (!TryQueryKernelThreadGroups(kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ))
                return false;
            if (sizeY != 1u || sizeZ != 1u)
                return false;

            groupSizeX = (int)sizeX;
            return true;
        }

        private bool TryResolveKernelThreadGroupTile(
            int kernelIndex,
            out int tileSizeX,
            out int tileSizeY)
        {
            tileSizeX = 0;
            tileSizeY = 0;
            if (!TryQueryKernelThreadGroups(kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ))
                return false;
            if (sizeY == 0u || sizeZ != 1u)
                return false;

            tileSizeX = (int)sizeX;
            tileSizeY = (int)sizeY;
            return true;
        }

        private bool TryValidateKernelThreadProduct(int kernelIndex)
        {
            return TryQueryKernelThreadGroups(kernelIndex, out _, out _, out _);
        }

        private bool TryQueryKernelThreadGroups(int kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ)
        {
            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            if (marineSnowCompute == null ||
                kernelIndex < 0 ||
                !_coldSupportsComputeShaders)
                return false;

            try
            {
                if (!marineSnowCompute.IsSupported(kernelIndex))
                    return false;

                marineSnowCompute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
            if (sizeX == 0u || sizeY == 0u || sizeZ == 0u)
                return false;

            ulong maxThreads = (ulong)PortableMaxComputeThreadsPerGroup;
            ulong threadCount = (ulong)sizeX * sizeY;
            if (threadCount == 0UL ||
                threadCount > maxThreads ||
                sizeZ > maxThreads / threadCount)
            {
                return false;
            }

            threadCount *= sizeZ;
            return threadCount <= maxThreads;
        }

        private void RefreshFlowFieldUpload(float dt)
        {
            _flowFieldUploadTimer -= dt;
            HectonMapMagicVegetationBridge bridge = null;
            if (!WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref bridge))
            {
                ResetFlowFieldSamplingState();
                return;
            }

            bool hasPayload = bridge.TryGetEcosystemFlowFieldPayload(
                out NativeArray<float2>.ReadOnly flowVectors,
                out int gridResolution,
                out Vector3 gridCenter,
                out float cellSize);
            if (!hasPayload)
            {
                ResetFlowFieldSamplingState();
                return;
            }

            int availableCount = math.max(0, flowVectors.Length);
            long expectedCount = (long)math.max(0, gridResolution) * math.max(0, gridResolution);
            if (gridResolution <= 1 ||
                expectedCount <= 0L ||
                expectedCount > int.MaxValue ||
                availableCount < (int)expectedCount)
            {
                ResetFlowFieldSamplingState();
                return;
            }

            int uploadCount = (int)expectedCount;
            if (!TryEnsureFlowFieldUploadCapacity(uploadCount))
            {
                ResetFlowFieldSamplingState();
                return;
            }

            _flowFieldCenterWS = gridCenter;
            _flowFieldResolution = gridResolution;
            _flowFieldCellSize = cellSize;

            float recenterThreshold = math.max(0.01f, cellSize * flowFieldRecenterThresholdCells);
            bool forceUpload =
                _flowFieldBuffer == null ||
                _flowFieldUploadTimer <= 0f ||
                _lastUploadedFlowFieldCenterWS == Vector3.zero ||
                (gridCenter - _lastUploadedFlowFieldCenterWS).sqrMagnitude >= recenterThreshold * recenterThreshold;

            if (!forceUpload)
                return;

            if (!bridge.TryUploadEcosystemFlowFieldPayload(_flowFieldBuffer, uploadCount))
            {
                ResetFlowFieldSamplingState();
                return;
            }

            _lastUploadedFlowFieldCenterWS = gridCenter;
            _flowFieldUploadTimer = math.max(0.05f, flowFieldUploadInterval);
        }

        private void ResetFlowFieldSamplingState()
        {
            _flowFieldResolution = 0;
            _flowFieldCellSize = 0f;
            _flowFieldCenterWS = Vector3.zero;
            _lastUploadedFlowFieldCenterWS = Vector3.zero;
            _flowFieldUploadTimer = 0f;
        }

        private bool TryEnsureFlowFieldUploadCapacity(int requiredCount)
        {
            if (_flowFieldBuffer != null && _flowFieldBuffer.count >= requiredCount)
                return true;

#if UNITY_EDITOR
            if (!Application.isPlaying && requiredCount > 0)
            {
                int boundedCount = math.clamp(requiredCount, DefaultEcosystemFlowFieldBufferCapacity, MaxEcosystemFlowFieldBufferCapacity);
                if (boundedCount < requiredCount)
                    return false;

                ReleaseBuffer(ref _flowFieldBuffer);
                _flowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(boundedCount); // COLD ALLOC: GraphicsBuffer[editor flowVectors.Length] - editor-only ecosystem flow-field staging resize - owner: HectonMarineSnowRenderer
                _flowFieldBufferCapacity = boundedCount;
                _boundSimulationFlowFieldBuffer = null;
                _staticBindingsDirty = true;
                return true;
            }
#endif

            return false;
        }

        private int SanitizeFlowFieldUploadCapacity()
        {
            return math.clamp(flowFieldUploadCapacity, DefaultEcosystemFlowFieldBufferCapacity, MaxEcosystemFlowFieldBufferCapacity);
        }

        private void ApplyStaticBindingsIfNeeded()
        {
            if (!_staticBindingsDirty)
                return;

            if (_particleBufferA == null ||
                _particleBufferB == null ||
                _particleMetaBufferA == null ||
                _particleMetaBufferB == null ||
                _activeFrameConstantsBuffer == null ||
                _emptyFlowFieldBuffer == null ||
                _visibleParticleIndexBuffer == null ||
                _indirectArgsBuffer == null ||
                _emptyAbyssalFlowBuffer == null ||
                _mockWakeDtoBuffer == null ||
                _mockWakeBuffer == null ||
                _mockWakeVectorBuffer == null ||
                _propwashEventBufferA == null ||
                _propwashEventBufferB == null ||
                _emptyAbyssalFlowTexture == null)
                return;

            if (_propwashEventBuffer == null)
                _propwashEventBuffer = _propwashEventBufferA;

            GraphicsBuffer flowFieldBuffer = _flowFieldBuffer != null ? _flowFieldBuffer : _emptyFlowFieldBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesReadId, _particleBufferA);
            _boundSimulationReadBuffer = _particleBufferA;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticlesWriteId, _particleBufferB);
            _boundSimulationWriteBuffer = _particleBufferB;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticleMetaReadId, _particleMetaBufferA);
            _boundSimulationMetaReadBuffer = _particleMetaBufferA;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.ParticleMetaWriteId, _particleMetaBufferB);
            _boundSimulationMetaWriteBuffer = _particleMetaBufferB;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FlowFieldId, flowFieldBuffer);
            _boundSimulationFlowFieldBuffer = flowFieldBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer);
            _boundSimulationVisibleParticleIndexBuffer = _visibleParticleIndexBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.IndirectArgsId, _indirectArgsBuffer);
            _boundSimulationIndirectArgsBuffer = _indirectArgsBuffer;
            marineSnowCompute.SetBuffer(_initializeKernel, ShaderIds.FrameConstantsId, _activeFrameConstantsBuffer);
            marineSnowCompute.SetBuffer(_wakeProximityKernel, ShaderIds.FrameConstantsId, _activeFrameConstantsBuffer);
            marineSnowCompute.SetBuffer(_clearVisibleKernel, ShaderIds.IndirectArgsId, _indirectArgsBuffer);
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.AbyssalFlowFieldResultId, _emptyAbyssalFlowBuffer);
            _boundAbyssalFlowBuffer = _emptyAbyssalFlowBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.DynamicWakesId, _emptyAbyssalFlowBuffer);
            _boundDynamicWakeBuffer = _emptyAbyssalFlowBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.DynamicWakeVectorsId, _emptyAbyssalFlowBuffer);
            _boundDynamicWakeVectorBuffer = _emptyAbyssalFlowBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.DynamicWakeDtosId, _mockWakeDtoBuffer);
            _boundDynamicWakeDtoBuffer = _mockWakeDtoBuffer;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.PropwashEventsId, _propwashEventBuffer);
            _boundPropwashEventBuffer = _propwashEventBuffer;
            marineSnowCompute.SetVector(ShaderIds.DynamicWakeParamsId, Vector4.zero);
            _boundDynamicWakeParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.DynamicWakeDtoParamsId, Vector4.zero);
            _boundDynamicWakeDtoParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.PropwashEventParamsId, Vector4.zero);
            _boundPropwashEventParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.PropwashBiomeTintId, Vector4.zero);
            _boundPropwashBiomeTint = Vector4.zero;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.MaelstromsId, _emptyAbyssalFlowBuffer);
            _boundSimulationMaelstromBuffer = _emptyAbyssalFlowBuffer;
            marineSnowCompute.SetVector(ShaderIds.MaelstromParamsId, Vector4.zero);
            _boundMaelstromParams = Vector4.zero;
            marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.AbyssalFlowFieldTextureId, _emptyAbyssalFlowTexture);
            _boundAbyssalFlowTexture = _emptyAbyssalFlowTexture;
            marineSnowCompute.SetFloat(ShaderIds.AbyssalFlowTextureActiveId, 0f);
            _boundAbyssalFlowTextureActive = 0f;
            marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.FrameConstantsId, _activeFrameConstantsBuffer);
            VFXEmissionProfile.FluidSettings emissionSettings = ResolveEmissionSettings();
            Vector4 driftParams = ResolveDriftParams(emissionSettings);
            marineSnowCompute.SetVector(ShaderIds.DriftParamsId, driftParams);
            _boundDriftParams = driftParams;
            Vector4 flowParams = ResolveFlowParams();
            marineSnowCompute.SetVector(ShaderIds.FlowParamsId, flowParams);
            _boundFlowParams = flowParams;
            marineSnowCompute.SetVector(ShaderIds.MockFlowFieldId, Vector4.zero);
            _boundMockFlowField = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.MockAcousticPulseId, Vector4.zero);
            _boundMockAcousticPulse = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.MockAcousticParamsId, Vector4.zero);
            _boundMockAcousticParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.BubbleParamsId, Vector4.zero);
            _boundBubbleParams = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.DepthCollisionParamsId, DepthCollisionParams);
            _boundDepthCollisionParams = DepthCollisionParams;
            marineSnowCompute.SetVector(ShaderIds.ScalabilityParamsId, _resolvedScalabilityParams);
            _boundScalabilityParams = _resolvedScalabilityParams;
            marineSnowCompute.SetVector(ShaderIds.AupShiftOffsetId, Vector4.zero);
            _boundAupShiftOffset = Vector4.zero;
            marineSnowCompute.SetVector(ShaderIds.VelocityParamsId, new Vector4(math.max(0.1f, maxSiltSpeed), math.max(0f, headlightEmissionMultiplier), 0f, 0f));
            _boundVelocityParams = new Vector4(math.max(0.1f, maxSiltSpeed), math.max(0f, headlightEmissionMultiplier), 0f, 0f);
            marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, Vector4.zero);

            marineSnowMaterial.SetBuffer(ShaderIds.FrameConstantsId, _activeFrameConstantsBuffer);
            marineSnowMaterial.SetBuffer(ShaderIds.ParticleMetaRenderId, _particleMetaBufferB);
            _boundMaterialParticleMetaBuffer = _particleMetaBufferB;
            marineSnowMaterial.SetBuffer(ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer);
            _boundMaterialVisibleParticleIndexBuffer = _visibleParticleIndexBuffer;
            marineSnowMaterial.SetVector(
                ShaderIds.RenderParamsId,
                new Vector4(
                    maxAlpha,
                    softness,
                    math.max(0.25f, maxViewDistance),
                    0f));
            marineSnowMaterial.SetColor(ShaderIds.TintId, particleTint);
            marineSnowMaterial.SetVector(ShaderIds.PropwashBiomeTintId, Vector4.zero);
            _boundMaterialPropwashBiomeTint = Vector4.zero;
            BindMaterialFlipbookAtlasIfNeeded();

            _staticBindingsDirty = false;
        }

        private VFXEmissionProfile.FluidSettings ResolveEmissionSettings()
        {
            if (emissionProfile != null)
                return emissionProfile.GetSettings(fluidType);

            return new VFXEmissionProfile.FluidSettings
            {
                baseDragCoeff = baseDragCoefficient,
                buoyancyModifier = fluidType == VFXEmissionProfile.FluidType.Bubble ? 1f : -0.02f,
                turbulenceScale = 1f,
                wobbleScale = fluidType == VFXEmissionProfile.FluidType.Bubble ? 1f : 0f
            };
        }

        private Vector4 ResolveDriftParams(VFXEmissionProfile.FluidSettings emissionSettings)
        {
            VfxConfigurationDTO tuning = CaptureSiltTuningSnapshot();
            float sinkScale = tuning.Version != 0u ? math.max(0.05f, tuning.GravitySinkingSpeed) : 1f;
            return new Vector4(
                math.min(descentMinSpeed, descentMaxSpeed) * sinkScale,
                math.max(descentMinSpeed, descentMaxSpeed) * sinkScale,
                wanderStrength,
                emissionSettings.baseDragCoeff > 0f ? emissionSettings.baseDragCoeff : baseDragCoefficient);
        }

        private Vector4 ResolveFlowParams()
        {
            VfxConfigurationDTO tuning = CaptureSiltTuningSnapshot();
            float curlStrength = tuning.Version != 0u ? tuning.CurlNoiseStrength : 0.15f;
            float wakeInfluence = tuning.Version != 0u ? tuning.WakeInfluence : 1f;
            return new Vector4(
                flowBlend,
                densityBiasFlowGain,
                math.max(0f, curlStrength),
                math.max(0f, wakeInfluence));
        }

        private Vector4 ResolveMockFlowVector()
        {
            MockFlowField mock = _cachedMockFlowField;
            float density = mock.DensityScale > 0f ? mock.DensityScale : 0f;
            return new Vector4(mock.GlobalFlow.x, mock.GlobalFlow.y, mock.GlobalFlow.z, density);
        }

        private Vector4 BuildPropwashEventParams()
        {
            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            float quality = tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
            return new Vector4(
                PropwashEventRingCapacity,
                math.max(0, _debugPropwashEventCount),
                math.max(0.05f, tuning.SiltProximityMeters),
                math.saturate(quality));
        }

        private static int ComputePropwashEventSampleBudget(int activeCount, float quality)
        {
            int safeActiveCount = math.clamp(activeCount, 0, PropwashEventRingCapacity);
            if (safeActiveCount <= 0)
                return 0;

            float q = math.saturate(quality);
            float curved = q * q * (3f - 2f * q);
            float sampleBudget = math.min(
                safeActiveCount,
                math.max(
                    PropwashEventMinSampleCapacity,
                    math.lerp(PropwashEventMinSampleCapacity, safeActiveCount, curved)));
            return math.clamp((int)sampleBudget, 0, safeActiveCount);
        }

        private Vector4 BuildPropwashBiomeTint()
        {
            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            return new Vector4(
                math.saturate(tuning.BiomeTintR),
                math.saturate(tuning.BiomeTintG),
                math.saturate(tuning.BiomeTintB),
                math.max(0.01f, tuning.CurlNoiseFrequency));
        }

        private void ResolveMockAcousticVectors(out Vector4 pulse, out Vector4 parameters)
        {
            MockAcousticSignal signal = _mockAcousticSignal;
            float age = _simulationTime - signal.StartTime;
            if (signal.Magnitude <= 0f ||
                signal.Radius <= 0f ||
                signal.Duration <= 0f ||
                age < 0f ||
                age > signal.Duration)
            {
                pulse = Vector4.zero;
                parameters = Vector4.zero;
                return;
            }

            pulse = new Vector4(signal.Position.x, signal.Position.y, signal.Position.z, signal.Radius);
            parameters = new Vector4(
                math.max(0f, signal.Magnitude),
                signal.StartTime,
                math.max(0.01f, signal.Duration),
                math.max(0.01f, signal.WaveSpeed));
        }

        private void UpdateFrameConstants(float dt, float effectiveDensityScale)
        {
            _simulationTime += dt;
            if (_simulationTime >= 60f)
                _simulationTime -= 60f;

            Vector3 cameraPosition = targetCamera.position;
            Vector3 cameraRight = targetCamera.right;
            Vector3 cameraUp = targetCamera.up;
            Vector3 cameraVelocity = ResolveCameraVelocity(cameraPosition, dt);
            float speedLineStretch = ResolveSpeedLineStretch(cameraVelocity, dt);
            float densityScale = math.saturate(effectiveDensityScale);
            float activeFlag = densityScale > ActiveDensityEpsilon ? 1f : 0f;

            FrameConstantsData frameConstants = new FrameConstantsData
            {
                CameraPositionTime = new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, _simulationTime),
                CameraRightDeltaTime = new Vector4(cameraRight.x, cameraRight.y, cameraRight.z, dt),
                CameraUpDensity = new Vector4(cameraUp.x, cameraUp.y, cameraUp.z, densityScale),
                FlowFieldCenterCellSize = new Vector4(_flowFieldCenterWS.x, _flowFieldCenterWS.y, _flowFieldCenterWS.z, _flowFieldCellSize),
                ShellParams = new Vector4(
                    math.max(0.05f, innerRadius),
                    math.max(innerRadius + 0.1f, outerRadius),
                    math.min(verticalSpan.x, verticalSpan.y),
                    math.max(verticalSpan.x, verticalSpan.y)),
                MetaParams = new Vector4(
                    _activeParticleCount,
                    _flowFieldResolution,
                    SystemDispatcher.CurrentFrameIndex & 1023,
                    activeFlag),
                CameraVelocityStretch = new Vector4(
                    cameraVelocity.x,
                    cameraVelocity.y,
                    cameraVelocity.z,
                    speedLineStretch),
                Pad0 = Vector4.zero
            };

            GraphicsBuffer frameConstantsWriteBuffer = (_frameConstantsUploadBufferIndex & 1) == 0
                ? _frameConstantsBufferA
                : _frameConstantsBufferB;
            if (frameConstantsWriteBuffer == null || !frameConstantsWriteBuffer.IsValid())
                return;

            UploadSingleGraphicsBuffer(frameConstantsWriteBuffer, frameConstants);
            _activeFrameConstantsBuffer = frameConstantsWriteBuffer;
            _frameConstantsUploadBufferIndex ^= 1;
            VFXEmissionProfile.FluidSettings emissionSettings = ResolveEmissionSettings();
            float biolumeSurgeBlend = ResolveBiolumeSurgeBlend();
            float surgeTurbulenceScale = 1f + (biolumeSurgeTurbulenceMultiplier - 1f) * biolumeSurgeBlend;
            Vector4 emissionParams = new Vector4(
                emissionSettings.buoyancyModifier,
                emissionSettings.turbulenceScale * surgeTurbulenceScale,
                emissionSettings.wobbleScale * surgeTurbulenceScale,
                (float)fluidType);
            Vector4 bubbleParams = new Vector4(
                _underwaterActive ? _bubbleTrailMovement01 : 0f,
                _underwaterActive ? _bubbleTrailExhale01 : 0f,
                _lastDepth,
                activeFlag);
            SetComputeVectorHotIfChanged(ShaderIds.EmissionParamsId, emissionParams, ref _boundEmissionParams);
            SetComputeVectorHotIfChanged(ShaderIds.BubbleParamsId, bubbleParams, ref _boundBubbleParams);
            if (_targetCameraComponent != null)
            {
                Texture depthTexture = _cameraDepthTextureSnapshot;
                if (depthTexture != null)
                    SetKernelTextureIfChanged(_kernelIndex, ShaderIds.CameraDepthTextureId, depthTexture, ref _boundCameraDepthTexture);

                int pixelWidth = _targetCameraComponent.pixelWidth;
                int pixelHeight = _targetCameraComponent.pixelHeight;
                Matrix4x4 worldToCameraMatrix = _targetCameraComponent.worldToCameraMatrix;
                Matrix4x4 viewProjection = GL.GetGPUProjectionMatrix(_targetCameraComponent.projectionMatrix, false) * worldToCameraMatrix;
                Vector4 depthTextureTexelSize = new Vector4(
                    pixelWidth > 0 ? math.rcp((float)math.max(1, pixelWidth)) : 0f,
                    pixelHeight > 0 ? math.rcp((float)math.max(1, pixelHeight)) : 0f,
                    pixelWidth,
                    pixelHeight);
                SetComputeMatrixHotIfChanged(ShaderIds.ViewProjectionId, viewProjection, ref _boundViewProjection);
                SetComputeMatrixHotIfChanged(ShaderIds.ViewMatrixId, worldToCameraMatrix, ref _boundViewMatrix);
                SetComputeVectorHotIfChanged(ShaderIds.ZBufferParamsId, _cachedZBufferParams, ref _boundZBufferParams);
                SetComputeVectorHotIfChanged(ShaderIds.DepthTextureTexelSizeId, depthTextureTexelSize, ref _boundDepthTextureTexelSize);
            }
        }

        private void RefreshHotGpuBindings()
        {
            double3 floatingOriginOffset = HectonFloatingOrigin.CurrentTotalOffsetDouble;
            if (!math.all(math.isfinite(floatingOriginOffset)))
                floatingOriginOffset = double3.zero;
            VFXEmissionProfile.FluidSettings emissionSettings = ResolveEmissionSettings();
            SetComputeVectorHotIfChanged(ShaderIds.DriftParamsId, ResolveDriftParams(emissionSettings), ref _boundDriftParams);
            SetComputeVectorHotIfChanged(ShaderIds.FlowParamsId, ResolveFlowParams(), ref _boundFlowParams);
            SetComputeVectorHotIfChanged(ShaderIds.MockFlowFieldId, ResolveMockFlowVector(), ref _boundMockFlowField);
            ResolveMockAcousticVectors(out Vector4 mockAcousticPulse, out Vector4 mockAcousticParams);
            SetComputeVectorHotIfChanged(ShaderIds.MockAcousticPulseId, mockAcousticPulse, ref _boundMockAcousticPulse);
            SetComputeVectorHotIfChanged(ShaderIds.MockAcousticParamsId, mockAcousticParams, ref _boundMockAcousticParams);
            SetComputeVectorHotIfChanged(
                ShaderIds.SubmarineWashSphereId,
                _cachedSubmarineWashSphere,
                ref _boundSubmarineWashSphere);
            SetComputeVectorHotIfChanged(
                ShaderIds.SubmarineWashVelocityId,
                _cachedSubmarineWashVelocity,
                ref _boundSubmarineWashVelocity);
            SetComputeVectorHotIfChanged(ShaderIds.PropwashParamsId, DefaultPropwashParams, ref _boundPropwashParams);
            if (_propwashEventBuffer != null)
                SetKernelBufferIfChanged(_kernelIndex, ShaderIds.PropwashEventsId, _propwashEventBuffer, ref _boundPropwashEventBuffer);
            Vector4 propwashBiomeTint = BuildPropwashBiomeTint();
            SetComputeVectorHotIfChanged(ShaderIds.PropwashEventParamsId, BuildPropwashEventParams(), ref _boundPropwashEventParams);
            SetComputeVectorHotIfChanged(ShaderIds.PropwashBiomeTintId, propwashBiomeTint, ref _boundPropwashBiomeTint);
            SetMaterialVectorHotIfChanged(ShaderIds.PropwashBiomeTintId, propwashBiomeTint, ref _boundMaterialPropwashBiomeTint);
            SetComputeVectorHotIfChanged(
                ShaderIds.FloatingOriginOffsetId,
                new Vector4((float)floatingOriginOffset.x, (float)floatingOriginOffset.y, (float)floatingOriginOffset.z, 0f),
                ref _boundFloatingOriginOffset);
            SetComputeVectorHotIfChanged(
                ShaderIds.AupShiftOffsetId,
                new Vector4(_pendingAupShiftOffset.x, _pendingAupShiftOffset.y, _pendingAupShiftOffset.z, 0f),
                ref _boundAupShiftOffset);
            SetComputeVectorHotIfChanged(
                ShaderIds.VelocityParamsId,
                new Vector4(math.max(0.1f, maxSiltSpeed), math.max(0f, headlightEmissionMultiplier), 0f, 0f),
                ref _boundVelocityParams);
            SetComputeVectorHotIfChanged(
                ShaderIds.FlashlightPositionWSId,
                _cachedFlashlightPositionWS,
                ref _boundFlashlightPositionWS);
            SetComputeVectorHotIfChanged(
                ShaderIds.FlashlightDirectionWSId,
                _cachedFlashlightDirectionWS,
                ref _boundFlashlightDirectionWS);
            Vector4 flashlightColor = _cachedFlashlightColor;
            SetComputeVectorHotIfChanged(
                ShaderIds.FlashlightColorId,
                flashlightColor,
                ref _boundFlashlightColor);
            SetComputeVectorHotIfChanged(
                ShaderIds.FlashlightConeDataId,
                _cachedFlashlightConeData,
                ref _boundFlashlightConeData);
            float flashlightActive = _cachedFlashlightActive;
            _lastHeadlightBoost = math.saturate((flashlightActive >= 0.5f ? 1f : 0f) * flashlightColor.w * math.max(0f, headlightEmissionMultiplier));
            SetComputeBinaryFloatHotIfChanged(
                ShaderIds.FlashlightActiveId,
                flashlightActive,
                ref _boundFlashlightActive);
            RefreshDynamicWakeBinding();
        }

        private void RefreshColdGpuBindings(float dt)
        {
            _externalGpuBindingColdTickTimer -= math.max(0f, dt);
            if (!_externalGpuBindingsDirty && _externalGpuBindingColdTickTimer > 0f)
                return;

            RefreshFluidBinding(force: false);
            RefreshAbyssalFlowBinding();
            RefreshMaelstromBinding();
            RefreshCaveSdfBinding();
            RefreshTerrainHeightBinding();
            SetComputeVectorIfChanged(ShaderIds.FlowSynchronyParamsId, ResolveFlowSynchronyParams(), ref _boundFlowSynchronyParams);
            _externalGpuBindingColdTickTimer = ExternalGpuBindingColdTickSeconds;
            _externalGpuBindingsDirty = false;
        }

        private void RefreshAbyssalFlowBinding()
        {
            GraphicsBuffer flowFieldBuffer = _emptyAbyssalFlowBuffer;
            Texture flowFieldTexture = _emptyAbyssalFlowTexture;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;
            Vector4 textureParams = Vector4.zero;
            float textureActive = 0f;

            IAbyssalFlowGpuReadModel abyssalFlow = _abyssalFlowGpuReadModel;
            if (abyssalFlow != null &&
                abyssalFlow.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer publishedFlowFieldBuffer,
                    out Vector4 publishedGridResolution,
                    out Vector4 publishedFlowCenter,
                    out Vector4 publishedFlowSpacing))
            {
                flowFieldBuffer = publishedFlowFieldBuffer;
                gridResolution = publishedGridResolution;
                flowCenter = publishedFlowCenter;
                flowSpacing = publishedFlowSpacing;
            }

            if (abyssalFlow != null &&
                abyssalFlow.TryGetGpuAbyssalFlowFieldTexture(
                    out Texture publishedFlowFieldTexture,
                    out Vector4 publishedTextureResolution,
                    out Vector4 publishedTextureCenter,
                    out Vector4 publishedTextureSpacing))
            {
                flowFieldTexture = publishedFlowFieldTexture;
                flowCenter = publishedTextureCenter;
                textureParams = new Vector4(
                    publishedTextureResolution.x,
                    publishedTextureSpacing.z,
                    0f,
                    1f);
                textureActive = 1f;
            }

            if (flowFieldBuffer != null && flowFieldBuffer != _boundAbyssalFlowBuffer)
            {
                marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.AbyssalFlowFieldResultId, flowFieldBuffer);
                _boundAbyssalFlowBuffer = flowFieldBuffer;
            }

            if (flowFieldTexture != null && flowFieldTexture != _boundAbyssalFlowTexture)
            {
                marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.AbyssalFlowFieldTextureId, flowFieldTexture);
                _boundAbyssalFlowTexture = flowFieldTexture;
            }

            SetComputeVectorIfChanged(ShaderIds.AbyssalGridResolutionId, gridResolution, ref _boundAbyssalGridResolution);
            SetComputeVectorIfChanged(ShaderIds.AbyssalFlowCenterId, flowCenter, ref _boundAbyssalFlowCenter);
            SetComputeVectorIfChanged(ShaderIds.AbyssalFlowSpacingId, flowSpacing, ref _boundAbyssalFlowSpacing);
            SetComputeVectorIfChanged(ShaderIds.AbyssalFlowTextureParamsId, textureParams, ref _boundAbyssalFlowTextureParams);
            SetComputeBinaryFloatIfChanged(ShaderIds.AbyssalFlowTextureActiveId, textureActive, ref _boundAbyssalFlowTextureActive);
        }

        private void RefreshDynamicWakeBinding()
        {
            GraphicsBuffer wakeBuffer = _emptyAbyssalFlowBuffer;
            GraphicsBuffer wakeVectorBuffer = _emptyAbyssalFlowBuffer;
            GraphicsBuffer wakeDtoBuffer = _mockWakeDtoBuffer != null ? _mockWakeDtoBuffer : _emptyAbyssalFlowBuffer;
            Vector4 wakeParams = Vector4.zero;
            float wakeQualityPressure01 = ResolveDynamicWakeQualityPressure01(_resolvedScalabilityParams.x);
            Vector4 wakeDtoParams = _debugMockWakeCount > 0 ? new Vector4(MockWakeCapacity, wakeQualityPressure01, _debugMockWakeCount, math.max(0f, ResolveFlowParams().w)) : Vector4.zero;

            IAbyssalFlowGpuReadModel abyssalFlow = _abyssalFlowGpuReadModel;
            if (abyssalFlow != null &&
                abyssalFlow.TryGetDynamicWakeGpuPayload(
                    out GraphicsBuffer publishedWakeBuffer,
                    out GraphicsBuffer publishedWakeVectorBuffer,
                    out Vector4 publishedWakeParams))
            {
                if (publishedWakeBuffer != null && publishedWakeBuffer.IsValid())
                    wakeBuffer = publishedWakeBuffer;
                if (publishedWakeVectorBuffer != null && publishedWakeVectorBuffer.IsValid())
                    wakeVectorBuffer = publishedWakeVectorBuffer;
                wakeParams = SanitizeDynamicWakeParams(publishedWakeParams);
            }
            else if (_debugMockWakeCount > 0 && _mockWakeBuffer != null && _mockWakeVectorBuffer != null)
            {
                wakeBuffer = _mockWakeBuffer;
                wakeVectorBuffer = _mockWakeVectorBuffer;
                wakeParams = new Vector4(MockWakeCapacity, wakeQualityPressure01, _debugMockWakeCount, math.max(0f, ResolveFlowParams().w));
            }

            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.DynamicWakesId, wakeBuffer, ref _boundDynamicWakeBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.DynamicWakeVectorsId, wakeVectorBuffer, ref _boundDynamicWakeVectorBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.DynamicWakeDtosId, wakeDtoBuffer, ref _boundDynamicWakeDtoBuffer);
            SetComputeVectorHotIfChanged(ShaderIds.DynamicWakeParamsId, wakeParams, ref _boundDynamicWakeParams);
            SetComputeVectorHotIfChanged(ShaderIds.DynamicWakeDtoParamsId, wakeDtoParams, ref _boundDynamicWakeDtoParams);
            _debugDynamicWakeCount = math.max(math.max(0, (int)wakeParams.z), math.max(0, (int)wakeDtoParams.z));
        }

        private static Vector4 SanitizeDynamicWakeParams(Vector4 wakeParams)
        {
            if (!IsFiniteVector(wakeParams))
                return Vector4.zero;

            float qualityPressure01 = math.saturate(wakeParams.y);
            float continuousCapacity = math.lerp(16f, 4f, qualityPressure01);
            float slotLimit = math.clamp(wakeParams.x, 0f, continuousCapacity);
            float activeCount = math.clamp(wakeParams.z, 0f, slotLimit);
            return new Vector4(slotLimit, qualityPressure01, activeCount, math.max(0f, wakeParams.w));
        }

        private static float ResolveDynamicWakeQualityPressure01(float flowQualityWeight)
        {
            return math.saturate(1f - math.saturate(flowQualityWeight * 0.5f));
        }

        private void RefreshMaelstromBinding()
        {
            GraphicsBuffer maelstromBuffer = _emptyAbyssalFlowBuffer;
            Vector4 maelstromParams = Vector4.zero;

            IAbyssalFlowGpuReadModel abyssalFlow = _abyssalFlowGpuReadModel;
            if (abyssalFlow != null &&
                _maelstromBufferA != null &&
                _maelstromBufferB != null &&
                abyssalFlow.TryGetActiveMaelstroms(
                    out NativeArray<float4>.ReadOnly maelstroms,
                    out int maelstromCount,
                    out Vector4 publishedMeta))
            {
                int uploadCount = math.clamp(maelstromCount, 0, abyssalFlow.MaxActiveMaelstromCapacity);
                if (uploadCount > 0)
                {
                    bool hasBoundMaelstromBuffer = false;
                    uint uploadHash = BuildMaelstromUploadHash(maelstroms, uploadCount, publishedMeta);
                    if (uploadHash != _boundMaelstromUploadHash || uploadCount != _boundMaelstromUploadCount)
                    {
                        GraphicsBuffer writeBuffer = ResolveMaelstromWriteBuffer();
                        if (abyssalFlow.TryUploadActiveMaelstroms(writeBuffer, uploadCount))
                        {
                            _boundMaelstromUploadHash = uploadHash;
                            _boundMaelstromUploadCount = uploadCount;
                            _maelstromWriteBufferIndex ^= 1;
                            maelstromBuffer = writeBuffer;
                            hasBoundMaelstromBuffer = true;
                        }
                    }
                    else if (_boundSimulationMaelstromBuffer != null &&
                             _boundSimulationMaelstromBuffer != _emptyAbyssalFlowBuffer)
                    {
                        maelstromBuffer = _boundSimulationMaelstromBuffer;
                        hasBoundMaelstromBuffer = true;
                    }
                    else
                    {
                        maelstromBuffer = ResolveMaelstromReadFallbackBuffer();
                        hasBoundMaelstromBuffer = maelstromBuffer != null && maelstromBuffer != _emptyAbyssalFlowBuffer;
                    }

                    if (hasBoundMaelstromBuffer)
                    {
                        maelstromParams = new Vector4(
                            uploadCount,
                            math.max(0f, publishedMeta.y),
                            math.max(0f, publishedMeta.z),
                            publishedMeta.w);
                    }
                }
            }

            if (maelstromParams.x <= 0f)
            {
                _boundMaelstromUploadHash = 0u;
                _boundMaelstromUploadCount = 0;
            }

            if (maelstromBuffer != null && maelstromBuffer != _boundSimulationMaelstromBuffer)
            {
                marineSnowCompute.SetBuffer(_kernelIndex, ShaderIds.MaelstromsId, maelstromBuffer);
                _boundSimulationMaelstromBuffer = maelstromBuffer;
            }

            SetComputeVectorIfChanged(ShaderIds.MaelstromParamsId, maelstromParams, ref _boundMaelstromParams);
        }

        private GraphicsBuffer ResolveMaelstromWriteBuffer()
        {
            return (_maelstromWriteBufferIndex & 1) == 0 ? _maelstromBufferA : _maelstromBufferB;
        }

        private GraphicsBuffer ResolveMaelstromReadFallbackBuffer()
        {
            return (_maelstromWriteBufferIndex & 1) == 0 ? _maelstromBufferB : _maelstromBufferA;
        }

        private static uint BuildMaelstromUploadHash(NativeArray<float4>.ReadOnly maelstroms, int count, Vector4 meta)
        {
            uint hash = 2166136261u;
            hash = MixMaelstromUploadHash(hash, unchecked((uint)count));
            hash = MixMaelstromUploadHash(hash, math.asuint(meta.x));
            hash = MixMaelstromUploadHash(hash, math.asuint(meta.y));
            hash = MixMaelstromUploadHash(hash, math.asuint(meta.z));
            hash = MixMaelstromUploadHash(hash, math.asuint(meta.w));
            int safeCount = math.min(math.max(0, count), maelstroms.IsCreated ? maelstroms.Length : 0);
            for (int i = 0; i < safeCount; i++)
            {
                float4 maelstrom = maelstroms[i];
                hash = MixMaelstromUploadHash(hash, math.asuint(maelstrom.x));
                hash = MixMaelstromUploadHash(hash, math.asuint(maelstrom.y));
                hash = MixMaelstromUploadHash(hash, math.asuint(maelstrom.z));
                hash = MixMaelstromUploadHash(hash, math.asuint(maelstrom.w));
            }

            return hash;
        }

        private static uint MixMaelstromUploadHash(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private void RefreshCaveSdfBinding()
        {
            Texture sdfTexture = _emptyCaveSdfTexture;
            Matrix4x4 worldToLocal = IdentityMatrix;
            Vector4 halfExtentsAndRange = Vector4.zero;
            Vector4 invDoubleHalfExtents = Vector4.zero;
            float active = 0f;

            HectonCaveVoxelLightingVolume caveVolume = HectonCaveVoxelLightingVolume.ActiveRuntimeInstance;
            if (caveVolume != null &&
                caveVolume.TryGetPublishedGpuSdfPayload(
                    out Texture3D publishedSdfTexture,
                    out Matrix4x4 publishedWorldToLocal,
                    out Vector4 publishedHalfExtentsAndRange,
                    out Vector4 publishedInvDoubleHalfExtents))
            {
                sdfTexture = publishedSdfTexture;
                worldToLocal = publishedWorldToLocal;
                halfExtentsAndRange = publishedHalfExtentsAndRange;
                invDoubleHalfExtents = publishedInvDoubleHalfExtents;
                active = 1f;
            }

            if (sdfTexture != null && sdfTexture != _boundCaveSdfTexture)
            {
                marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.CaveVoxelSdfTexId, sdfTexture);
                _boundCaveSdfTexture = sdfTexture;
            }

            SetComputeBinaryFloatIfChanged(ShaderIds.CaveVoxelActiveId, active, ref _boundCaveVoxelActive);
            SetComputeMatrixIfChanged(ShaderIds.CaveVoxelWorldToLocalId, worldToLocal, ref _boundCaveVoxelWorldToLocal);
            SetComputeVectorIfChanged(ShaderIds.CaveVoxelHalfExtentsId, halfExtentsAndRange, ref _boundCaveVoxelHalfExtents);
            SetComputeVectorIfChanged(ShaderIds.CaveVoxelInvDoubleHalfExtentsId, invDoubleHalfExtents, ref _boundCaveVoxelInvDoubleHalfExtents);
        }

        private void RefreshTerrainHeightBinding()
        {
            Texture heightTexture = Texture2D.blackTexture;
            Vector4 heightRect = Vector4.zero;
            Vector4 heightScale = DisabledTerrainHeightScale;

            HectonMapMagicVegetationBridge bridge = null;
            if (WorldRuntimeReferenceUtility.TryResolveHectonMapMagicVegetationBridge(ref bridge) &&
                bridge.TryGetActiveHeightTexturePayload(out HectonMapMagicVegetationBridge.TerrainHeightTexturePayload heightPayload) &&
                heightPayload.HeightTexture != null &&
                heightPayload.TerrainSize.x > 0f &&
                heightPayload.TerrainSize.z > 0f)
            {
                heightTexture = heightPayload.HeightTexture;
                heightRect = new Vector4(
                    heightPayload.TerrainPosition.x,
                    heightPayload.TerrainPosition.z,
                    math.rcp(math.max(heightPayload.TerrainSize.x, 0.0001f)),
                    math.rcp(math.max(heightPayload.TerrainSize.z, 0.0001f)));
                heightScale = new Vector4(
                    heightPayload.TerrainPosition.y,
                    heightPayload.TerrainSize.y,
                    1f,
                    heightPayload.HeightmapResolution);
            }

            if (heightTexture != _boundTerrainHeightTexture)
            {
                marineSnowCompute.SetTexture(_kernelIndex, ShaderIds.TerrainHeightTextureId, heightTexture);
                _boundTerrainHeightTexture = heightTexture;
            }

            SetComputeVectorIfChanged(ShaderIds.TerrainHeightRectId, heightRect, ref _boundTerrainHeightRect);
            SetComputeVectorIfChanged(ShaderIds.TerrainHeightScaleId, heightScale, ref _boundTerrainHeightScale);
        }

        private void DispatchVisibleClear()
        {
            if (_clearVisibleKernel < 0 || _indirectArgsBuffer == null)
                return;

            int clearGroups = CeilDivide(1, _clearVisibleThreadGroupSize);
            if (clearGroups <= 0 || clearGroups > MaxDispatchGroupsPerDimension)
                return;

            marineSnowCompute.Dispatch(_clearVisibleKernel, clearGroups, 1, 1);
        }

        private void DispatchFogDensityClear()
        {
            if (!IsFogDensityInjectionActive() || _fogDensityClearKernel < 0)
            {
                SetComputeVectorHotIfChanged(ShaderIds.FogDensityParamsId, Vector4.zero, ref _boundFogDensityParams);
                PublishFogDensityGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            if (_fogDensityTexture == null)
            {
                SetComputeVectorHotIfChanged(ShaderIds.FogDensityParamsId, Vector4.zero, ref _boundFogDensityParams);
                PublishFogDensityGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            Vector4 fogDensityTexelSize = ResolveFogDensityTexelSize();
            Vector4 fogDensityParams = ResolveFogDensityParams();
            SetComputeVectorHotIfChanged(ShaderIds.FogDensityTexelSizeId, fogDensityTexelSize, ref _boundFogDensityTexelSize);
            SetComputeVectorHotIfChanged(ShaderIds.FogDensityParamsId, fogDensityParams, ref _boundFogDensityParams);
            SetKernelTextureIfChanged(_fogDensityClearKernel, ShaderIds.FogDensityResultId, _fogDensityTexture, ref _boundFogDensityClearTexture);
            SetKernelTextureIfChanged(_kernelIndex, ShaderIds.FogDensityResultId, _fogDensityTexture, ref _boundFogDensitySimulationTexture);
            PublishFogDensityGlobals(fogDensityTexelSize, fogDensityParams, _fogDensityTexture);

            DispatchClearKernelChunked(
                _fogDensityClearKernel,
                _fogDensityClearGroupsX,
                _fogDensityClearGroupsY,
                _fogDensityClearTileSizeX,
                _fogDensityClearTileSizeY);
        }

        private Vector3 ResolveCameraVelocity(Vector3 cameraPosition, float dt)
        {
            Vector3 velocity = Vector3.zero;
            if (_hasLastCameraPositionWS && dt > 0.0001f)
                velocity = (cameraPosition - _lastCameraPositionWS) * math.rcp(math.max(dt, 0.0001f));

            _lastCameraPositionWS = cameraPosition;
            _hasLastCameraPositionWS = true;
            return velocity;
        }

        private float ResolveSpeedLineStretch(Vector3 cameraVelocity, float dt)
        {
            float targetIntensity = 0f;
            if (_underwaterActive && dt > 0f)
            {
                float speedSq = math.lengthsq((float3)cameraVelocity);
                float speed01 = math.saturate((speedSq - _speedLineStartVelocitySq) * _speedLineInvVelocityBandSq);
                targetIntensity = speed01 * speed01 * (3f - 2f * speed01);
            }

            float blendT = FastDecayBlend(_speedLineResponseSpeed, math.max(0f, dt));
            _speedLineIntensity += (targetIntensity - _speedLineIntensity) * blendT;
            return 1f + _speedLineStretchDelta * _speedLineIntensity;
        }

        private static float FastDecayBlend(float speed, float deltaTime)
        {
            float x = math.max(0f, speed) * math.max(0f, deltaTime);
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) * math.rcp(math.max(12f + (6f * x) + (x * x), 0.0001f)));
        }

        private Vector4 ResolveFlowSynchronyParams()
        {
            return _cachedFlowSynchronyParams.x > 0f ? _cachedFlowSynchronyParams : DefaultFlowSynchronyParams;
        }

        private void DispatchSimulation()
        {
            GraphicsBuffer readBuffer = _frameParity == 0 ? _particleBufferA : _particleBufferB;
            GraphicsBuffer writeBuffer = _frameParity == 0 ? _particleBufferB : _particleBufferA;
            GraphicsBuffer readMetaBuffer = _frameParity == 0 ? _particleMetaBufferA : _particleMetaBufferB;
            GraphicsBuffer writeMetaBuffer = _frameParity == 0 ? _particleMetaBufferB : _particleMetaBufferA;
            GraphicsBuffer flowFieldBuffer = _flowFieldBuffer != null ? _flowFieldBuffer : _emptyFlowFieldBuffer;
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticlesReadId, readBuffer, ref _boundSimulationReadBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticlesWriteId, writeBuffer, ref _boundSimulationWriteBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticleMetaReadId, readMetaBuffer, ref _boundSimulationMetaReadBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.ParticleMetaWriteId, writeMetaBuffer, ref _boundSimulationMetaWriteBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.FlowFieldId, flowFieldBuffer, ref _boundSimulationFlowFieldBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer, ref _boundSimulationVisibleParticleIndexBuffer);
            SetKernelBufferIfChanged(_kernelIndex, ShaderIds.IndirectArgsId, _indirectArgsBuffer, ref _boundSimulationIndirectArgsBuffer);

            DispatchAupRebaseIfNeeded(readBuffer, readMetaBuffer);
            DispatchWakeProximityInjection(readBuffer, readMetaBuffer);
            DispatchParticleKernelChunked(_kernelIndex, _activeParticleCount, _simulationThreadGroupSize);
            _pendingAupShiftOffset = Vector3.zero;

            SetMaterialBufferIfChanged(ShaderIds.ParticlesRenderId, writeBuffer, ref _boundMaterialParticlesBuffer);
            SetMaterialBufferIfChanged(ShaderIds.ParticleMetaRenderId, writeMetaBuffer, ref _boundMaterialParticleMetaBuffer);
            SetMaterialBufferIfChanged(ShaderIds.VisibleParticleIndicesId, _visibleParticleIndexBuffer, ref _boundMaterialVisibleParticleIndexBuffer);
            BindMaterialFlipbookAtlasIfNeeded();
        }

        private void DispatchAupRebaseIfNeeded(GraphicsBuffer particleBuffer, GraphicsBuffer particleMetaBuffer)
        {
            if (_rebaseKernel < 0 ||
                _activeParticleCount <= 0 ||
                particleBuffer == null ||
                particleMetaBuffer == null ||
                _pendingAupShiftOffset.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            marineSnowCompute.SetBuffer(_rebaseKernel, ShaderIds.ParticlesWriteId, particleBuffer);
            marineSnowCompute.SetBuffer(_rebaseKernel, ShaderIds.ParticleMetaWriteId, particleMetaBuffer);
            DispatchParticleKernelChunked(_rebaseKernel, _activeParticleCount, _rebaseThreadGroupSize);
            SetComputeVectorHotIfChanged(ShaderIds.AupShiftOffsetId, Vector4.zero, ref _boundAupShiftOffset);
            _pendingAupShiftOffset = Vector3.zero;
        }

        private void DispatchWakeProximityInjection(GraphicsBuffer particleWriteBuffer, GraphicsBuffer particleMetaWriteBuffer)
        {
            if (_wakeProximityKernel < 0 ||
                _debugPropwashEventCount <= 0 ||
                particleWriteBuffer == null ||
                particleMetaWriteBuffer == null ||
                _propwashEventBuffer == null ||
                _activeFrameConstantsBuffer == null)
            {
                return;
            }

            Texture sdfTexture = _boundCaveSdfTexture != null ? _boundCaveSdfTexture : _emptyCaveSdfTexture;
            Texture heightTexture = _boundTerrainHeightTexture != null ? _boundTerrainHeightTexture : Texture2D.blackTexture;
            marineSnowCompute.SetBuffer(_wakeProximityKernel, ShaderIds.FrameConstantsId, _activeFrameConstantsBuffer);
            marineSnowCompute.SetBuffer(_wakeProximityKernel, ShaderIds.ParticlesWriteId, particleWriteBuffer);
            marineSnowCompute.SetBuffer(_wakeProximityKernel, ShaderIds.ParticleMetaWriteId, particleMetaWriteBuffer);
            marineSnowCompute.SetBuffer(_wakeProximityKernel, ShaderIds.PropwashEventsId, _propwashEventBuffer);
            if (sdfTexture != null)
                marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.CaveVoxelSdfTexId, sdfTexture);
            if (heightTexture != null)
                marineSnowCompute.SetTexture(_wakeProximityKernel, ShaderIds.TerrainHeightTextureId, heightTexture);

            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            float quality = tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
            int proximityEventBudget = math.min(
                _activeParticleCount,
                ComputePropwashEventSampleBudget(_debugPropwashEventCount, quality));
            if (proximityEventBudget <= 0)
                return;

            DispatchParticleKernelChunked(
                _wakeProximityKernel,
                proximityEventBudget,
                _wakeProximityThreadGroupSize);
        }

        private void DispatchParticleInitializationIfNeeded()
        {
            if (!_particleBuffersNeedGpuBootstrap ||
                _initializeKernel < 0 ||
                _particleBufferA == null ||
                _particleBufferB == null ||
                _particleMetaBufferA == null ||
                _particleMetaBufferB == null ||
                _allocatedParticleCapacity <= 0)
            {
                return;
            }

            marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, new Vector4(_allocatedParticleCapacity, 0f, 0f, 0f));
            marineSnowCompute.SetBuffer(_initializeKernel, ShaderIds.ParticlesWriteId, _particleBufferA);
            marineSnowCompute.SetBuffer(_initializeKernel, ShaderIds.ParticleMetaWriteId, _particleMetaBufferA);
            DispatchParticleKernelChunked(_initializeKernel, _allocatedParticleCapacity, _initializeThreadGroupSize);
            marineSnowCompute.SetBuffer(_initializeKernel, ShaderIds.ParticlesWriteId, _particleBufferB);
            marineSnowCompute.SetBuffer(_initializeKernel, ShaderIds.ParticleMetaWriteId, _particleMetaBufferB);
            DispatchParticleKernelChunked(_initializeKernel, _allocatedParticleCapacity, _initializeThreadGroupSize);
            marineSnowCompute.SetVector(ShaderIds.InitializationParamsId, Vector4.zero);

            _frameParity = 0;
            _particleBuffersNeedGpuBootstrap = false;
            _boundMaterialParticlesBuffer = null;
            _boundMaterialParticleMetaBuffer = null;
            _boundSimulationReadBuffer = null;
            _boundSimulationWriteBuffer = null;
            _boundSimulationMetaReadBuffer = null;
            _boundSimulationMetaWriteBuffer = null;
        }

        private void DispatchParticleKernelChunked(int kernelIndex, int particleCount, int threadGroupSize)
        {
            if (kernelIndex < 0 || particleCount <= 0 || threadGroupSize <= 0)
                return;

            int remainingGroups = CeilDivide(particleCount, threadGroupSize);
            if (remainingGroups <= 0)
                return;

            int groupOffset = 0;
            int maxGroupsPerDispatch = math.min(MaxParticleDispatchGroupsPerCall, MaxDispatchGroupsPerDimension);
            while (remainingGroups > 0)
            {
                int groupsThisDispatch = math.min(remainingGroups, maxGroupsPerDispatch);
                if (groupsThisDispatch <= 0)
                    return;

                long particleOffsetLong = (long)groupOffset * threadGroupSize;
                if (particleOffsetLong > int.MaxValue)
                    return;

                int particleOffset = (int)particleOffsetLong;
                SetComputeIntHotIfChanged(ShaderIds.DispatchOffsetId, particleOffset, ref _boundDispatchOffset);
                marineSnowCompute.Dispatch(kernelIndex, groupsThisDispatch, 1, 1);
                groupOffset += groupsThisDispatch;
                remainingGroups -= groupsThisDispatch;
            }
        }

        private void DispatchClearKernelChunked(int kernelIndex, int groupCountX, int groupCountY, int tileSizeX, int tileSizeY)
        {
            if (kernelIndex < 0 ||
                groupCountX <= 0 ||
                groupCountY <= 0 ||
                tileSizeX <= 0 ||
                tileSizeY <= 0)
                return;

            int xGroupOffset = 0;
            int maxGroupsPerDispatch = math.min(MaxParticleDispatchGroupsPerCall, MaxDispatchGroupsPerDimension);
            while (xGroupOffset < groupCountX)
            {
                int groupsXThisDispatch = math.min(groupCountX - xGroupOffset, maxGroupsPerDispatch);
                if (groupsXThisDispatch <= 0)
                    return;

                int maxYGroupsForX = maxGroupsPerDispatch / groupsXThisDispatch;
                if (maxYGroupsForX <= 0)
                    return;

                int yGroupOffset = 0;
                while (yGroupOffset < groupCountY)
                {
                    int groupsYThisDispatch = math.min(groupCountY - yGroupOffset, maxYGroupsForX);
                    if (groupsYThisDispatch <= 0)
                        return;

                    long xPixelOffset = (long)xGroupOffset * tileSizeX;
                    long yPixelOffset = (long)yGroupOffset * tileSizeY;
                    if (xPixelOffset > int.MaxValue || yPixelOffset > int.MaxValue)
                        return;

                    Vector4 tileOffset = new Vector4(
                        (int)xPixelOffset,
                        (int)yPixelOffset,
                        0f,
                        0f);
                    SetComputeVectorHotIfChanged(ShaderIds.DispatchTileOffsetId, tileOffset, ref _boundDispatchTileOffset);
                    marineSnowCompute.Dispatch(kernelIndex, groupsXThisDispatch, groupsYThisDispatch, 1);
                    yGroupOffset += groupsYThisDispatch;
                }

                xGroupOffset += groupsXThisDispatch;
            }
        }

        private void DispatchSonarGlow()
        {
            if (!IsSonarGlowActive())
            {
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            if (_sonarGlowTexture == null)
            {
                PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
                return;
            }

            Vector4 sonarGlowTexelSize = ResolveSonarGlowTexelSize();
            Vector4 sonarGlowParams = ResolveSonarGlowParams();
            SetComputeVectorHotIfChanged(ShaderIds.SonarGlowTexelSizeId, sonarGlowTexelSize, ref _boundSonarGlowTexelSize);
            SetComputeVectorHotIfChanged(ShaderIds.SonarGlowParamsId, sonarGlowParams, ref _boundSonarGlowParams);
            PublishSonarGlowGlobals(sonarGlowTexelSize, sonarGlowParams, _sonarGlowTexture);

            SetKernelTextureIfChanged(_sonarGlowClearKernel, ShaderIds.SonarGlowResultId, _sonarGlowTexture, ref _boundSonarGlowClearTexture);
            SetKernelTextureIfChanged(_sonarGlowAccumulateKernel, ShaderIds.SonarGlowResultId, _sonarGlowTexture, ref _boundSonarGlowAccumulateTexture);
            SetKernelBufferIfChanged(
                _sonarGlowAccumulateKernel,
                ShaderIds.ParticlesWriteId,
                _frameParity == 0 ? _particleBufferB : _particleBufferA,
                ref _boundSonarGlowParticlesWriteBuffer);
            SetKernelBufferIfChanged(
                _sonarGlowAccumulateKernel,
                ShaderIds.ParticleMetaWriteId,
                _frameParity == 0 ? _particleMetaBufferB : _particleMetaBufferA,
                ref _boundSonarGlowParticleMetaWriteBuffer);

            int clearGroupsX = CeilDivide(_sonarGlowWidth, _sonarGlowClearTileSizeX);
            int clearGroupsY = CeilDivide(_sonarGlowHeight, _sonarGlowClearTileSizeY);
            DispatchClearKernelChunked(
                _sonarGlowClearKernel,
                clearGroupsX,
                clearGroupsY,
                _sonarGlowClearTileSizeX,
                _sonarGlowClearTileSizeY);

            DispatchParticleKernelChunked(_sonarGlowAccumulateKernel, _activeParticleCount, _sonarGlowAccumulateThreadGroupSize);
        }

        private bool IsSonarGlowActive()
        {
            if (_activeParticleCount <= 0 ||
                sonarGlowIntensity <= 0f ||
                sonarGlowCompositeStrength <= 0f)
            {
                return false;
            }

            return (float)SystemDispatcher.CurrentUnscaledTimeSeconds <= _cachedSonarRevealExpireTime;
        }

        private bool IsFogDensityInjectionActive()
        {
            return _activeParticleCount > 0 &&
                   _underwaterActive &&
                   fogDensityInjectionStrength > 0f;
        }

        private Vector4 ResolveSonarGlowTexelSize()
        {
            Vector4 texelSize;
            texelSize.x = math.rcp((float)math.max(1, _sonarGlowWidth));
            texelSize.y = math.rcp((float)math.max(1, _sonarGlowHeight));
            texelSize.z = _sonarGlowWidth;
            texelSize.w = _sonarGlowHeight;
            return texelSize;
        }

        private Vector4 ResolveSonarGlowParams()
        {
            Vector4 parameters;
            parameters.x = math.max(0f, sonarGlowIntensity);
            parameters.y = math.max(0f, sonarGlowCompositeStrength);
            parameters.z = 65535f;
            parameters.w = 1f;
            return parameters;
        }

        private Vector4 ResolveFogDensityTexelSize()
        {
            return _fogDensityTexelSize;
        }

        private Vector4 ResolveFogDensityParams()
        {
            Vector4 parameters;
            parameters.x = math.max(0f, fogDensityInjectionStrength);
            parameters.y = FogDensityEncodedScale;
            parameters.z = FogDensityParticleSizeGain;
            parameters.w = 1f;
            return parameters;
        }

        private void PublishSonarGlowGlobals(Vector4 texelSize, Vector4 parameters, Texture texture)
        {
            if (_sonarGlowGlobalsDirty ||
                !NearlyEqual(_lastPublishedSonarGlowTexelSize, texelSize, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.SonarGlowTexelSizeId, texelSize);
                _lastPublishedSonarGlowTexelSize = texelSize;
            }

            if (_sonarGlowGlobalsDirty ||
                !NearlyEqual(_lastPublishedSonarGlowParams, parameters, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.SonarGlowParamsId, parameters);
                _lastPublishedSonarGlowParams = parameters;
            }

            if (texture != null && (_sonarGlowGlobalsDirty || _lastPublishedSonarGlowTexture != texture))
            {
                Shader.SetGlobalTexture(ShaderIds.SonarGlowTextureId, texture);
                _lastPublishedSonarGlowTexture = texture;
            }
            else if (texture == null)
            {
                _lastPublishedSonarGlowTexture = null;
            }

            _sonarGlowGlobalsDirty = false;
        }

        private void PublishFogDensityGlobals(Vector4 texelSize, Vector4 parameters, Texture texture)
        {
            if (_fogDensityGlobalsDirty ||
                !NearlyEqual(_lastPublishedFogDensityTexelSize, texelSize, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.FogDensityTexelSizeId, texelSize);
                _lastPublishedFogDensityTexelSize = texelSize;
            }

            if (_fogDensityGlobalsDirty ||
                !NearlyEqual(_lastPublishedFogDensityParams, parameters, ShaderVectorPublishEpsilon))
            {
                Shader.SetGlobalVector(ShaderIds.FogDensityParamsId, parameters);
                _lastPublishedFogDensityParams = parameters;
            }

            if (texture != null && (_fogDensityGlobalsDirty || _lastPublishedFogDensityTexture != texture))
            {
                Shader.SetGlobalTexture(ShaderIds.FogDensityTextureId, texture);
                _lastPublishedFogDensityTexture = texture;
            }
            else if (texture == null)
            {
                _lastPublishedFogDensityTexture = null;
            }

            _fogDensityGlobalsDirty = false;
        }

        private void EnsureEmptyCaveSdfTexture()
        {
            if (ReferenceEquals(_emptyCaveSdfTexture, emptyCaveSdfTexture3D))
                return;

            _emptyCaveSdfTexture = emptyCaveSdfTexture3D;
        }

        private void EnsureEmptyAbyssalFlowTexture()
        {
            if (ReferenceEquals(_emptyAbyssalFlowTexture, emptyAbyssalFlowTexture3D))
                return;

            _emptyAbyssalFlowTexture = emptyAbyssalFlowTexture3D;
        }

        private static bool NearlyEqual(Vector4 left, Vector4 right, float epsilon)
        {
            return math.abs(left.x - right.x) <= epsilon &&
                   math.abs(left.y - right.y) <= epsilon &&
                   math.abs(left.z - right.z) <= epsilon &&
                   math.abs(left.w - right.w) <= epsilon;
        }

        private static bool IsFiniteVector(Vector4 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static bool NearlyEqual(Matrix4x4 left, Matrix4x4 right, float epsilon)
        {
            return math.abs(left.m00 - right.m00) <= epsilon &&
                   math.abs(left.m01 - right.m01) <= epsilon &&
                   math.abs(left.m02 - right.m02) <= epsilon &&
                   math.abs(left.m03 - right.m03) <= epsilon &&
                   math.abs(left.m10 - right.m10) <= epsilon &&
                   math.abs(left.m11 - right.m11) <= epsilon &&
                   math.abs(left.m12 - right.m12) <= epsilon &&
                   math.abs(left.m13 - right.m13) <= epsilon &&
                   math.abs(left.m20 - right.m20) <= epsilon &&
                   math.abs(left.m21 - right.m21) <= epsilon &&
                   math.abs(left.m22 - right.m22) <= epsilon &&
                   math.abs(left.m23 - right.m23) <= epsilon &&
                   math.abs(left.m30 - right.m30) <= epsilon &&
                   math.abs(left.m31 - right.m31) <= epsilon &&
                   math.abs(left.m32 - right.m32) <= epsilon &&
                   math.abs(left.m33 - right.m33) <= epsilon;
        }

        private void SetKernelBufferIfChanged(int kernelIndex, int shaderId, GraphicsBuffer buffer, ref GraphicsBuffer cachedBuffer)
        {
            if (buffer == null || buffer == cachedBuffer)
                return;

            marineSnowCompute.SetBuffer(kernelIndex, shaderId, buffer);
            cachedBuffer = buffer;
        }

        private void SetMaterialBufferIfChanged(int shaderId, GraphicsBuffer buffer, ref GraphicsBuffer cachedBuffer)
        {
            if (buffer == null || buffer == cachedBuffer)
                return;

            marineSnowMaterial.SetBuffer(shaderId, buffer);
            cachedBuffer = buffer;
        }

        private void SetMaterialVectorHotIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (marineSnowMaterial == null || NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowMaterial.SetVector(shaderId, value);
            cachedValue = value;
        }

        private void SetMaterialTextureHotIfChanged(int shaderId, Texture texture, ref Texture cachedTexture)
        {
            if (marineSnowMaterial == null || texture == cachedTexture)
                return;

            marineSnowMaterial.SetTexture(shaderId, texture);
            cachedTexture = texture;
        }

        private void BindMaterialFlipbookAtlasIfNeeded()
        {
            if (marineSnowMaterial == null)
                return;

            SetMaterialTextureHotIfChanged(ShaderIds.MaskAtlasId, marineSnowMaskAtlas, ref _boundMaterialMaskAtlas);
            SetMaterialTextureHotIfChanged(ShaderIds.NormalAtlasId, marineSnowNormalAtlas, ref _boundMaterialNormalAtlas);
            SetMaterialVectorHotIfChanged(ShaderIds.AtlasParamsId, ResolveMaterialAtlasParams(), ref _boundMaterialAtlasParams);
            SetMaterialVectorHotIfChanged(ShaderIds.FlipbookParamsId, ResolveMaterialFlipbookParams(), ref _boundMaterialFlipbookParams);
        }

        private void RefreshMaterialFlipbookAtlasFallbackCold()
        {
            if (marineSnowMaterial == null)
            {
                _materialAtlasFallbackSource = null;
                _materialAtlasFallbackResolved = false;
                return;
            }

            if (_materialAtlasFallbackResolved &&
                _materialAtlasFallbackSource == marineSnowMaterial &&
                marineSnowMaskAtlas != null &&
                marineSnowNormalAtlas != null)
            {
                return;
            }

            _materialAtlasFallbackSource = marineSnowMaterial;
            _materialAtlasFallbackResolved = true;

            if (marineSnowMaskAtlas == null && marineSnowMaterial.HasProperty(ShaderIds.MaskAtlasId))
                marineSnowMaskAtlas = marineSnowMaterial.GetTexture(ShaderIds.MaskAtlasId) as Texture2D;

            if (marineSnowNormalAtlas == null && marineSnowMaterial.HasProperty(ShaderIds.NormalAtlasId))
                marineSnowNormalAtlas = marineSnowMaterial.GetTexture(ShaderIds.NormalAtlasId) as Texture2D;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void RefreshAuthoredNeutralVolumeFallbacksColdEditor()
        {
#if UNITY_EDITOR
            if (emptyCaveSdfTexture3D == null)
                emptyCaveSdfTexture3D = AssetDatabase.LoadAssetAtPath<Texture3D>(DefaultEmptyCaveSdfTexturePath1728);

            if (emptyAbyssalFlowTexture3D == null)
                emptyAbyssalFlowTexture3D = AssetDatabase.LoadAssetAtPath<Texture3D>(DefaultEmptyAbyssalFlowTexturePath1728);
#endif
        }

        private Vector4 ResolveMaterialAtlasParams()
        {
            float maskWeight = marineSnowMaskAtlas != null ? math.saturate(marineSnowMaskAtlasWeight) : 0f;
            float normalWeight = marineSnowNormalAtlas != null && marineSnowMaskAtlas != null ? math.max(0f, marineSnowNormalAtlasWeight) : 0f;
            return new Vector4(
                math.clamp(marineSnowAtlasColumns, 1, 16),
                math.clamp(marineSnowAtlasRows, 1, 16),
                normalWeight,
                maskWeight);
        }

        private Vector4 ResolveMaterialFlipbookParams()
        {
            return new Vector4(
                math.max(0f, marineSnowFlipbookTimeScale),
                math.saturate(marineSnowFlipbookLifePhase),
                math.saturate(marineSnowAtlasAoGain),
                math.max(0f, marineSnowAtlasBiolumGain));
        }

        private void SetKernelTextureIfChanged(int kernelIndex, int shaderId, Texture texture, ref Texture cachedTexture)
        {
            if (texture == null || texture == cachedTexture)
                return;

            marineSnowCompute.SetTexture(kernelIndex, shaderId, texture);
            cachedTexture = texture;
        }

        private void SetComputeVectorHotIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetVector(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeMatrixHotIfChanged(int shaderId, Matrix4x4 value, ref Matrix4x4 cachedValue)
        {
            if (NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetMatrix(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeVectorIfChanged(int shaderId, Vector4 value, ref Vector4 cachedValue)
        {
            if (!_externalGpuBindingsDirty && NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetVector(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeBinaryFloatIfChanged(int shaderId, float value, ref float cachedValue)
        {
            float binaryValue = value >= 0.5f ? 1f : 0f;
            if (!_externalGpuBindingsDirty && cachedValue == binaryValue)
                return;

            marineSnowCompute.SetFloat(shaderId, binaryValue);
            cachedValue = binaryValue;
        }

        private void SetComputeBinaryFloatHotIfChanged(int shaderId, float value, ref float cachedValue)
        {
            float binaryValue = value >= 0.5f ? 1f : 0f;
            if (cachedValue == binaryValue)
                return;

            marineSnowCompute.SetFloat(shaderId, binaryValue);
            cachedValue = binaryValue;
        }

        private void SetComputeIntHotIfChanged(int shaderId, int value, ref int cachedValue)
        {
            if (cachedValue == value)
                return;

            marineSnowCompute.SetInt(shaderId, value);
            cachedValue = value;
        }

        private void SetComputeMatrixIfChanged(int shaderId, Matrix4x4 value, ref Matrix4x4 cachedValue)
        {
            if (!_externalGpuBindingsDirty && NearlyEqual(value, cachedValue, ShaderVectorPublishEpsilon))
                return;

            marineSnowCompute.SetMatrix(shaderId, value);
            cachedValue = value;
        }

        private void EnsureSonarGlowTexture()
        {
            if (_targetCameraComponent == null)
                return;

            float renderScale = math.clamp(sonarGlowRenderScale, 0.1f, 1f);
            int targetWidth = math.max(8, (int)(_targetCameraComponent.pixelWidth * renderScale + 0.999f));
            int targetHeight = math.max(8, (int)(_targetCameraComponent.pixelHeight * renderScale + 0.999f));
            if (_sonarGlowTexture != null)
            {
                if (_sonarGlowWidth == targetWidth && _sonarGlowHeight == targetHeight)
                    return;

                if (Application.isPlaying)
                    return;
            }

            ReleaseSonarGlowTexture();

            // COLD ALLOC: RenderTexture[sonarGlowWidth*sonarGlowHeight] - persistent sonar-reactive plankton splatmap - owner: HectonMarineSnowRenderer
            _sonarGlowTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear)
            {
                name = "HectonMarineSnowSonarGlow",
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _sonarGlowTexture.Create();
            _sonarGlowWidth = targetWidth;
            _sonarGlowHeight = targetHeight;
            _sonarGlowGlobalsDirty = true;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundSonarGlowTexelSize = InvalidVector;
        }

        private void ReleaseSonarGlowTexture()
        {
            if (_sonarGlowTexture == null)
                return;

            _sonarGlowTexture.Release();
            Destroy(_sonarGlowTexture);
            _sonarGlowTexture = null;
            _sonarGlowWidth = 0;
            _sonarGlowHeight = 0;
            _sonarGlowGlobalsDirty = true;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundSonarGlowTexelSize = InvalidVector;
        }

        private void EnsureFogDensityTexture()
        {
            if (_targetCameraComponent == null)
                return;

            float renderScale = math.clamp(fogDensityRenderScale, 0.1f, 0.5f);
            int targetWidth = math.max(8, (int)(_targetCameraComponent.pixelWidth * renderScale + 0.999f));
            int targetHeight = math.max(8, (int)(_targetCameraComponent.pixelHeight * renderScale + 0.999f));
            if (_fogDensityTexture != null)
            {
                if (_fogDensityWidth == targetWidth && _fogDensityHeight == targetHeight)
                    return;

                if (Application.isPlaying)
                    return;
            }

            ReleaseFogDensityTexture();

            // COLD ALLOC: RenderTexture[fogDensityWidth*fogDensityHeight] - persistent low-resolution marine-snow fog-density buffer - owner: HectonMarineSnowRenderer
            _fogDensityTexture = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.RInt, RenderTextureReadWrite.Linear)
            {
                name = "HectonMarineSnowFogDensity",
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            _fogDensityTexture.Create();
            _fogDensityWidth = targetWidth;
            _fogDensityHeight = targetHeight;
            _fogDensityClearGroupsX = CeilDivide(targetWidth, _fogDensityClearTileSizeX);
            _fogDensityClearGroupsY = CeilDivide(targetHeight, _fogDensityClearTileSizeY);
            _fogDensityTexelSize = new Vector4(
                math.rcp((float)math.max(1, targetWidth)),
                math.rcp((float)math.max(1, targetHeight)),
                targetWidth,
                targetHeight);
            _fogDensityGlobalsDirty = true;
            _boundFogDensityClearTexture = null;
            _boundFogDensitySimulationTexture = null;
            _boundFogDensityTexelSize = InvalidVector;
            _boundFogDensityParams = InvalidVector;
        }

        private void ReleaseFogDensityTexture()
        {
            if (_fogDensityTexture == null)
                return;

            _fogDensityTexture.Release();
            Destroy(_fogDensityTexture);
            _fogDensityTexture = null;
            _fogDensityWidth = 0;
            _fogDensityHeight = 0;
            _fogDensityClearGroupsX = 0;
            _fogDensityClearGroupsY = 0;
            _fogDensityTexelSize = Vector4.zero;
            _fogDensityGlobalsDirty = true;
            _boundFogDensityClearTexture = null;
            _boundFogDensitySimulationTexture = null;
            _boundFogDensityTexelSize = InvalidVector;
            _boundFogDensityParams = InvalidVector;
        }

        private static int CeilDivide(int value, int divisor)
        {
            if (value <= 0 || divisor <= 0)
                return 0;

            long groups = ((long)value + divisor - 1L) / divisor;
            if (groups <= 0L || groups > int.MaxValue)
                return 0;

            return (int)groups;
        }

        private void RenderMarineSnow()
        {
            if (_targetCameraComponent == null ||
                marineSnowMaterial == null ||
                _indirectArgsBuffer == null)
                return;

            Vector3 cameraPosition = targetCamera.position;
            float verticalSize = math.max(1f, math.abs(verticalSpan.y - verticalSpan.x));
            _drawBounds = new Bounds(
                cameraPosition + new Vector3(0f, (verticalSpan.x + verticalSpan.y) * 0.5f, 0f),
                new Vector3(outerRadius * 2f, verticalSize, outerRadius * 2f));

            UnityEngine.Graphics.DrawProceduralIndirect(
                marineSnowMaterial,
                _drawBounds,
                MeshTopology.Triangles,
                _indirectArgsBuffer,
                0,
                _targetCameraComponent,
                null,
                shadowCastingMode,
                false,
                gameObject.layer);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (targetCamera == null ||
                !TryReadReadyPropwashEvents(out NativeArray<PropwashEventDTO>.ReadOnly events) ||
                !TryReadReadyPropwashCursor(out NativeArray<PropwashRingCursorDTO>.ReadOnly cursorRing))
            {
                return;
            }

            PropwashRingCursorDTO cursor = cursorRing[0];
            int eventCount = math.min(math.max(0, cursor.EventCount), events.Length);
            int count = math.min(math.min(math.min(_debugPropwashEventCount, eventCount), 32), events.Length);
            int sourceStart = ComputePropwashUploadStart(cursor.WriteCursor, eventCount, events.Length);
            Vector3 cameraPosition = targetCamera.position;
            Gizmos.color = new Color(0.46f, 0.42f, 0.35f, 0.55f);
            for (int i = 0; i < count; i++)
            {
                int sourceIndex = WrapPropwashUploadIndex(sourceStart + i, events.Length);
                PropwashEventDTO evt = events[sourceIndex];
                if (evt.Radius <= 0f || !math.all(math.isfinite(evt.LocalPosition)))
                    continue;

                Vector3 position = cameraPosition + new Vector3(evt.LocalPosition.x, evt.LocalPosition.y, evt.LocalPosition.z);
                Vector3 thrust = new Vector3(evt.ThrustVector.x, evt.ThrustVector.y, evt.ThrustVector.z);
                Gizmos.DrawWireSphere(position, math.min(evt.Radius, 4f));
                Gizmos.DrawLine(position, position + thrust);
            }
        }
#endif

        private void ReleaseBuffers()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _particleMetaBufferA);
            ReleaseBuffer(ref _particleMetaBufferB);
            ReleaseBuffer(ref _flowFieldBuffer);
            _flowFieldBufferCapacity = 0;
            ReleaseBuffer(ref _emptyFlowFieldBuffer);
            ReleaseBuffer(ref _frameConstantsBufferA);
            ReleaseBuffer(ref _frameConstantsBufferB);
            _activeFrameConstantsBuffer = null;
            _frameConstantsUploadBufferIndex = 0;
            ReleaseBuffer(ref _visibleParticleIndexBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);
            ReleaseBuffer(ref _emptyAbyssalFlowBuffer);
            ReleaseBuffer(ref _mockWakeDtoBuffer);
            ReleaseBuffer(ref _mockWakeBuffer);
            ReleaseBuffer(ref _mockWakeVectorBuffer);
            ReleaseBuffer(ref _propwashEventBufferA);
            ReleaseBuffer(ref _propwashEventBufferB);
            _propwashEventBuffer = null;
            ReleaseBuffer(ref _maelstromBufferA);
            ReleaseBuffer(ref _maelstromBufferB);
            ReleaseEmptyCaveSdfTexture();
            ReleaseEmptyAbyssalFlowTexture();
            ReleaseSonarGlowTexture();
            ReleaseFogDensityTexture();
            PublishSonarGlowGlobals(Vector4.zero, Vector4.zero, null);
            PublishFogDensityGlobals(Vector4.zero, Vector4.zero, null);
            _buffersReady = false;
            _kernelIndex = -1;
            _initializeKernel = -1;
            _clearVisibleKernel = -1;
            _sonarGlowClearKernel = -1;
            _sonarGlowAccumulateKernel = -1;
            _fogDensityClearKernel = -1;
            ResetGpuBindingCaches();
            _externalGpuBindingsDirty = true;
            _particleBuffersNeedGpuBootstrap = false;
            _allocatedParticleCapacity = 0;
            _debugAllocatedParticleCapacity = 0;
            ResetSpeedLineHistory();
        }

        private void ResetGpuBindingCaches()
        {
            _boundCameraDepthTexture = null;
            _boundTerrainHeightTexture = null;
            _boundCaveSdfTexture = null;
            _boundAbyssalFlowTexture = null;
            _boundAbyssalFlowBuffer = null;
            _boundSimulationReadBuffer = null;
            _boundSimulationWriteBuffer = null;
            _boundSimulationMetaReadBuffer = null;
            _boundSimulationMetaWriteBuffer = null;
            _boundSimulationFlowFieldBuffer = null;
            _boundSimulationVisibleParticleIndexBuffer = null;
            _boundSimulationIndirectArgsBuffer = null;
            _boundSimulationMaelstromBuffer = null;
            _boundMaelstromUploadHash = 0u;
            _boundMaelstromUploadCount = -1;
            _maelstromWriteBufferIndex = 0;
            _boundMaterialParticlesBuffer = null;
            _boundMaterialParticleMetaBuffer = null;
            _boundMaterialVisibleParticleIndexBuffer = null;
            _boundMaterialMaskAtlas = null;
            _boundMaterialNormalAtlas = null;
            _boundMaterialAtlasParams = InvalidVector;
            _boundMaterialFlipbookParams = InvalidVector;
            _boundMaterialPropwashBiomeTint = InvalidVector;
            _boundSonarGlowParticlesWriteBuffer = null;
            _boundSonarGlowParticleMetaWriteBuffer = null;
            _boundSonarGlowClearTexture = null;
            _boundSonarGlowAccumulateTexture = null;
            _boundFogDensityClearTexture = null;
            _boundFogDensitySimulationTexture = null;
            _boundAbyssalGridResolution = Vector4.zero;
            _boundAbyssalFlowCenter = Vector4.zero;
            _boundAbyssalFlowSpacing = Vector4.zero;
            _boundAbyssalFlowTextureParams = Vector4.zero;
            _boundDynamicWakeBuffer = null;
            _boundDynamicWakeVectorBuffer = null;
            _boundDynamicWakeDtoBuffer = null;
            _boundPropwashEventBuffer = null;
            _boundDynamicWakeParams = InvalidVector;
            _boundDynamicWakeDtoParams = InvalidVector;
            _boundMaelstromParams = Vector4.zero;
            _boundCaveVoxelHalfExtents = Vector4.zero;
            _boundCaveVoxelInvDoubleHalfExtents = Vector4.zero;
            _boundTerrainHeightRect = Vector4.zero;
            _boundTerrainHeightScale = Vector4.zero;
            _boundSubmarineWashSphere = Vector4.zero;
            _boundSubmarineWashVelocity = Vector4.zero;
            _boundFloatingOriginOffset = InvalidVector;
            _boundAupShiftOffset = InvalidVector;
            _boundFlashlightPositionWS = InvalidVector;
            _boundFlashlightDirectionWS = InvalidVector;
            _boundFlashlightColor = InvalidVector;
            _boundFlashlightConeData = InvalidVector;
            _boundPropwashParams = Vector4.zero;
            _boundPropwashEventParams = InvalidVector;
            _boundPropwashBiomeTint = InvalidVector;
            _boundVelocityParams = InvalidVector;
            _boundEmissionParams = InvalidVector;
            _boundBubbleParams = InvalidVector;
            _boundDriftParams = InvalidVector;
            _boundFlowParams = InvalidVector;
            _boundMockFlowField = InvalidVector;
            _boundMockAcousticPulse = InvalidVector;
            _boundMockAcousticParams = InvalidVector;
            _boundFlowSynchronyParams = InvalidVector;
            _boundScalabilityParams = InvalidVector;
            _boundZBufferParams = InvalidVector;
            _boundDepthTextureTexelSize = InvalidVector;
            _boundDepthCollisionParams = InvalidVector;
            _boundSonarGlowTexelSize = InvalidVector;
            _boundSonarGlowParams = InvalidVector;
            _boundFogDensityTexelSize = InvalidVector;
            _boundFogDensityParams = InvalidVector;
            _boundDispatchOffset = int.MinValue;
            _boundDispatchTileOffset = InvalidVector;
            _boundViewProjection = InvalidMatrix;
            _boundViewMatrix = InvalidMatrix;
            _boundCaveVoxelWorldToLocal = IdentityMatrix;
            _boundCaveVoxelActive = -1f;
            _boundAbyssalFlowTextureActive = -1f;
            _boundFlashlightActive = -1f;
        }

        private void ReleaseEmptyCaveSdfTexture()
        {
            if (_emptyCaveSdfTexture == null)
                return;

            _emptyCaveSdfTexture = null;
        }

        private void ReleaseEmptyAbyssalFlowTexture()
        {
            if (_emptyAbyssalFlowTexture == null)
                return;

            _emptyAbyssalFlowTexture = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private int ResolveActiveParticleCount(float effectiveDensityScale)
        {
            int capacity = _allocatedParticleCapacity > 0
                ? _allocatedParticleCapacity
                : math.clamp(_resolvedParticleCapacity, 64, MaxMarineSnowParticleCapacity);
            capacity = math.min(capacity, math.clamp(_resolvedParticleCapacity, 64, MaxMarineSnowParticleCapacity));
            float densityScale = math.saturate(effectiveDensityScale);
            if (densityScale <= ActiveDensityEpsilon)
            {
                _debugActiveParticleCount = 0;
                return 0;
            }

            float budgetScale = 1f;
            byte pressureLevel = HomeostasisBrain.PressureLevel;
            ulong killSwitchMask = VfxComputeParticleBudgetCatalog.ResolvePolicyKillSwitchMask(
                pressureLevel,
                HomeostasisBrain.CurrentKillSwitchMask);
            VfxComputeParticleBudget pressureBudget = _resolvedPressureBudget;
            capacity = math.min(capacity, pressureBudget.ResolvePoolCapacity(fluidType));
            float systemStress01 = ResolveSystemStress01();
            float stressCapacityBlend = math.smoothstep(0.65f, 0.95f, systemStress01);
            int stressedCapacity = math.min(
                capacity,
                ResolvePoolCapacityForRow(
                    fluidType,
                    VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
                    VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount,
                    VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount));
            capacity = math.clamp((int)(math.lerp(capacity, stressedCapacity, stressCapacityBlend) + 0.5f), 64, capacity);
            _debugHomeostasisPressureLevel = pressureLevel;
            _debugHomeostasisKillSwitchMaskLow32 = unchecked((uint)killSwitchMask);
            _debugBudgetedStepDistanceMeters = pressureBudget.StepDistanceMeters;
            _debugBudgetedShadowTaps = ResolveEffectiveShadowTaps(pressureBudget, killSwitchMask, pressureLevel);

            DynamicResolutionScaler scaler = _dynamicResolutionScaler;
            float renderScale = scaler != null ? math.saturate(scaler.CurrentRenderScale) : 1f;
            budgetScale *= math.clamp(renderScale, 0.45f, 1f);
            _debugAdaptiveRenderScale = renderScale;

            IVramBudgetReadModel vramMonitor = _vramMonitor;
            byte pressureState = vramMonitor != null
                ? vramMonitor.PressureStateCode
                : VramPressureStateCodes.Stable;

            switch (pressureState)
            {
                case VramPressureStateCodes.Critical:
                    budgetScale *= 0.45f;
                    break;
                case VramPressureStateCodes.Warning:
                    budgetScale *= 0.7f;
                    break;
            }

            _debugAdaptiveVramPressureState = pressureState;
            budgetScale *= 0.35f + 0.65f * densityScale;
            budgetScale *= 1f + (biolumeSurgeParticleMultiplier - 1f) * ResolveBiolumeSurgeBlend();
            budgetScale *= math.min(1.12f, 1f + _bubbleTrailMovement01 * 0.08f + _bubbleTrailExhale01 * 0.06f);
            _debugAdaptiveBudgetScale = budgetScale;

            int resolvedCount = math.clamp((int)(capacity * budgetScale + 0.5f), 64, capacity);
            resolvedCount = VfxComputeParticleBudgetCatalog.ApplyKillSwitchCount(
                resolvedCount,
                fluidType,
                killSwitchMask,
                pressureLevel);
            _debugActiveParticleCount = resolvedCount;
            return resolvedCount;
        }

        private void RecordTelemetry()
        {
            Vector3 cameraPosition = targetCamera != null ? targetCamera.position : Vector3.zero;
            float systemStress01 = ResolveSystemStress01();
            float maxSpeed = math.max(0.1f, maxSiltSpeed);
            float aupShiftSq = _pendingAupShiftOffset.sqrMagnitude;
            bool finite =
                math.isfinite(cameraPosition.x) &&
                math.isfinite(cameraPosition.y) &&
                math.isfinite(cameraPosition.z) &&
                math.isfinite(systemStress01) &&
                math.isfinite(maxSpeed) &&
                math.isfinite(aupShiftSq);

            uint flags = finite ? 0u : 1u;
            uint hash = 2166136261u;
            hash = MixTelemetryHash(hash, Hecton8.Core.SystemDispatcher.CurrentFrameId);
            hash = MixTelemetryHash(hash, unchecked((uint)math.max(0, _activeParticleCount)));
            hash = MixTelemetryHash(hash, unchecked((uint)math.max(0, _allocatedParticleCapacity)));
            hash = MixTelemetryHash(hash, math.asuint(_lastVehicleThrottle));
            hash = MixTelemetryHash(hash, math.asuint(systemStress01));

            int mockGpuMicroseconds = EstimateGpuExecutionMicroseconds(
                _activeParticleCount,
                _debugDynamicWakeCount,
                math.saturate(_resolvedScalabilityParams.x * 0.5f));
            if (mockGpuMicroseconds > 1500)
                flags |= 2u;

            MarineSnowTelemetryEntry telemetryEntry = new MarineSnowTelemetryEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                DispatchedParticleCount = math.max(0, _activeParticleCount),
                Capacity = math.max(0, _allocatedParticleCapacity),
                DynamicWakeCount = math.max(0, _debugDynamicWakeCount),
                Throttle = _lastVehicleThrottle,
                SystemStress01 = systemStress01,
                MaxSiltSpeed = maxSpeed,
                AupShiftSq = aupShiftSq,
                CameraPositionWS = cameraPosition,
                HeadlightBoost = _lastHeadlightBoost,
                Flags = flags,
                StateHash = hash,
                MockGpuMicroseconds = mockGpuMicroseconds,
                CommandSequence = _lastVehicleCommandSequence
            };

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _telemetryRingHandle, BufferID.MarineSnowTelemetryRing, TelemetryCapacity, out NativeArray<MarineSnowTelemetryEntry> telemetryRing))
                return;

            try
            {
                telemetryRing[_telemetryWriteIndex] = telemetryEntry;
                _telemetryWriteIndex++;
                if (_telemetryWriteIndex >= TelemetryCapacity)
                    _telemetryWriteIndex = 0;
                _telemetryWrittenCount = math.min(_telemetryWrittenCount + 1, TelemetryCapacity);
            }
            finally
            {
                vault.ReleaseWriteLock(in _telemetryRingHandle, VaultOwnerSystem);
            }

            RecordPropwashTelemetry(mockGpuMicroseconds, flags);

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (frame - _lastTelemetryPublishFrame >= TelemetryPublishFrameCadence)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    DispatchedParticleCountTelemetryHash,
                    MarineSnowTelemetryContextHash,
                    _activeParticleCount);
                _lastTelemetryPublishFrame = frame;
            }

            if (!finite || mockGpuMicroseconds > 1500)
                DumpBlackBoxOnce();
        }

        private void RecordPropwashTelemetry(int estimatedGpuMicroseconds, uint flags)
        {
            PropwashGpuTuningDTO tuning = CapturePropwashTuningSnapshot();
            float quality = tuning.GlobalQualityWeightOverride >= 0f
                ? tuning.GlobalQualityWeightOverride
                : HomeostasisBrain.GlobalQualityWeight;
            int eventCount = math.max(0, _debugPropwashEventCount);
            int overflowCount = 0;
            int cursorValue = eventCount;
            if (TryReadReadyPropwashCursor(out NativeArray<PropwashRingCursorDTO>.ReadOnly cursorRing) &&
                cursorRing.Length > 0)
            {
                PropwashRingCursorDTO cursor = cursorRing[0];
                overflowCount = math.max(0, cursor.DroppedCount);
                cursorValue = cursor.WriteCursor;
            }

            uint stateHash = PropwashGpuContracts.HashState(unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId), eventCount, quality, tuning.Version);

            PropwashTelemetryEntry telemetryEntry = new PropwashTelemetryEntry
            {
                Frame = unchecked((int)Hecton8.Core.SystemDispatcher.CurrentFrameId),
                EventCount = eventCount,
                ParticleBudgetLimit = PropwashGpuContracts.ResolveParticleBudget(quality),
                OverflowCount = overflowCount,
                GlobalQualityWeight = math.saturate(quality),
                MaxIntensity = eventCount > 0 ? _debugPropwashMaxIntensity : 0f,
                EstimatedGpuMicroseconds = estimatedGpuMicroseconds,
                SdfProximityMeters = math.max(0.05f, tuning.SiltProximityMeters),
                StrongestLocalPosition = _debugPropwashStrongestLocalPosition,
                StateHash = stateHash,
                Flags = flags,
                Cursor = unchecked((uint)math.max(0, cursorValue)),
                ProfileHash = tuning.Version
            };

            IDataVault vault = _dataVault;
            if (!TryAcquireOwnedVaultWriteBuffer(vault, in _propwashTelemetryHandle, BufferID.PropwashGpuTelemetryRing, PropwashGpuContracts.TelemetryCapacity, out NativeArray<PropwashTelemetryEntry> telemetryRing))
                return;

            try
            {
                telemetryRing[_propwashTelemetryWriteIndex] = telemetryEntry;
                _propwashTelemetryWriteIndex++;
                if (_propwashTelemetryWriteIndex >= PropwashGpuContracts.TelemetryCapacity)
                    _propwashTelemetryWriteIndex = 0;
                _propwashTelemetryWrittenCount = math.min(_propwashTelemetryWrittenCount + 1, PropwashGpuContracts.TelemetryCapacity);
            }
            finally
            {
                vault.ReleaseWriteLock(in _propwashTelemetryHandle, VaultOwnerSystem);
            }

            if (overflowCount > 0 || estimatedGpuMicroseconds > 1500)
                DumpBlackBoxOnce();
        }

        private void DumpBlackBoxOnce()
        {
            if (_blackBoxDumped || !TryReadTelemetryRing(out var telemetryRing))
                return;

            string path = ResolveBlackBoxDumpPath();
            bool wrotePrimary = TryWriteBlackBoxDump(path, telemetryRing);
            bool wroteLegacy = TryWriteBlackBoxDump(ResolveLegacyBlackBoxDumpPath(), telemetryRing);
            _blackBoxDumped = wrotePrimary || wroteLegacy;
            if (TryReadReadyPropwashTelemetry(out NativeArray<PropwashTelemetryEntry>.ReadOnly propwashTelemetry))
            {
                string root = Application.isPlaying || !string.IsNullOrEmpty(Application.dataPath)
                    ? Path.GetFullPath(Path.Combine(Application.dataPath, ".."))
                    : Directory.GetCurrentDirectory();
                PropwashTelemetryDump.TryWrite(root, propwashTelemetry, _propwashTelemetryWriteIndex, _propwashTelemetryWrittenCount);
            }

            GlobalTelemetryBus.PublishPerformanceWarning(0x44554D50u, MarineSnowTelemetryContextHash, _telemetryWrittenCount);
        }

        private unsafe bool TryWriteBlackBoxDump(string path, NativeArray<MarineSnowTelemetryEntry>.ReadOnly telemetryRing)
        {
            NativeArray<byte> payload = default;
            try
            {
                int count = math.clamp(_telemetryWrittenCount, 0, math.min(telemetryRing.Length, TelemetryCapacity));
                int byteCount = 16 + count * TelemetryEntrySizeBytes;
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(HectonMarineSnowRenderer),
                    "MarineSnowTelemetryDumpPayload");
                byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(payloadPtr, 0, MarineSnowTelemetryContextHash);
                WriteUInt32LittleEndian(payloadPtr, 4, unchecked((uint)TelemetryCapacity));
                WriteUInt32LittleEndian(payloadPtr, 8, unchecked((uint)TelemetryEntrySizeBytes));
                WriteUInt32LittleEndian(payloadPtr, 12, unchecked((uint)math.max(0, _telemetryWrittenCount)));
                if (count > 0)
                {
                    int readIndex = count >= TelemetryCapacity ? WrapTelemetryIndex(_telemetryWriteIndex) : 0;
                    byte* basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
                    int firstCount = math.min(count, TelemetryCapacity - readIndex);
                    UnsafeUtility.MemCpy(payloadPtr + 16, basePtr + readIndex * TelemetryEntrySizeBytes, firstCount * TelemetryEntrySizeBytes);
                    int secondCount = count - firstCount;
                    if (secondCount > 0)
                        UnsafeUtility.MemCpy(payloadPtr + 16 + firstCount * TelemetryEntrySizeBytes, basePtr, secondCount * TelemetryEntrySizeBytes);
                }

                return NativeFaultDumpWriter.TryWriteAll(path, payload, byteCount);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(HectonMarineSnowRenderer),
                    "MarineSnowTelemetryDumpPayload");
            }
        }

        private static int WrapTelemetryIndex(int value)
        {
            int wrapped = value % TelemetryCapacity;
            return wrapped < 0 ? wrapped + TelemetryCapacity : wrapped;
        }

        private static unsafe void WriteUInt32LittleEndian(byte* target, int offset, uint value)
        {
            target[offset] = (byte)value;
            target[offset + 1] = (byte)(value >> 8);
            target[offset + 2] = (byte)(value >> 16);
            target[offset + 3] = (byte)(value >> 24);
        }

        private static int EstimateGpuExecutionMicroseconds(int dispatchedParticleCount, int dynamicWakeCount, float qualityWeight)
        {
            float quality = math.saturate(qualityWeight);
            float particleDivisor = math.lerp(700f, 420f, quality);
            float wakeCostPerItem = math.lerp(4f, 12f, quality);
            float qualityBaseCost = math.lerp(72f, 150f, quality);
            float particleCost = math.max(0, dispatchedParticleCount) / math.max(particleDivisor, 1f);
            float wakeCost = math.max(0, dynamicWakeCount) * wakeCostPerItem;
            return math.clamp((int)(qualityBaseCost + particleCost + wakeCost + 0.5f), 0, 5000);
        }

        private static string ResolveBlackBoxDumpPath()
        {
            return BlackBoxDumpRelativePath;
        }

        private static string ResolveLegacyBlackBoxDumpPath()
        {
            return LegacyBlackBoxDumpRelativePath;
        }

        private static float ResolveSystemStress01()
        {
            return math.saturate(HomeostasisBrain.SystemHealthIndex01);
        }

        private static uint MixTelemetryHash(uint hash, uint value)
        {
            unchecked
            {
                hash ^= value;
                return hash * 16777619u;
            }
        }

        private float ResolveBiolumeSurgeBlend()
        {
            return math.saturate(_biolumeSurgeTimer * math.rcp(math.max(BiolumeSurgeDurationSeconds, 0.0001f)));
        }

        private int RefreshAndResolveConfiguredCapacity()
        {
            RefreshScalabilityProfile();
            return ComputeConfiguredAllocationCapacity();
        }

        private int ComputeConfiguredAllocationCapacity()
        {
            VfxConfigurationDTO tuning = CaptureSiltTuningSnapshot();
            int qualityCap = math.clamp(_resolvedParticleCapacity, 64, MaxMarineSnowParticleCapacity);
            if (tuning.Version == 0u ||
                tuning.ParticleCount <= 0 ||
                tuning.CsvProfileHash == 0u)
            {
                return qualityCap;
            }

            int authoredCap = math.clamp(tuning.ParticleCount, 64, MaxMarineSnowParticleCapacity);
            return math.min(authoredCap, qualityCap);
        }

        private void RefreshScalabilityProfile()
        {
            byte pressureLevel = HomeostasisBrain.PressureLevel;
            float globalQualityWeight = math.saturate(HomeostasisBrain.GlobalQualityWeight);
            ulong killSwitchMask = VfxComputeParticleBudgetCatalog.ResolvePolicyKillSwitchMask(
                pressureLevel,
                HomeostasisBrain.CurrentKillSwitchMask);
            if (pressureLevel == _resolvedPressureLevel &&
                killSwitchMask == _resolvedKillSwitchMask &&
                math.abs(globalQualityWeight - _resolvedGlobalQualityWeight) <= ShaderVectorPublishEpsilon &&
                fluidType == _resolvedFluidType)
                return;

            VfxComputeParticleBudget pressureBudget = BuildContinuousPressureBudget(
                globalQualityWeight,
                pressureLevel,
                killSwitchMask);
            int particleCapacity = math.min(
                ResolveContinuousPoolCapacity(fluidType, globalQualityWeight),
                pressureBudget.ResolvePoolCapacity(fluidType));
            Vector4 scalabilityParams = BuildContinuousScalabilityParams(
                globalQualityWeight,
                pressureLevel,
                killSwitchMask);

            _resolvedPressureLevel = pressureLevel;
            _resolvedGlobalQualityWeight = globalQualityWeight;
            _resolvedKillSwitchMask = killSwitchMask;
            _resolvedFluidType = fluidType;
            _resolvedPressureBudget = pressureBudget;
            _resolvedParticleCapacity = math.clamp(particleCapacity, 64, MaxMarineSnowParticleCapacity);
            _resolvedScalabilityParams = scalabilityParams;
            _debugScalabilityParticleCapacity = _resolvedParticleCapacity;
            _debugGlobalQualityWeight01 = globalQualityWeight;
            _debugQualityPressure01 = ResolveDynamicWakeQualityPressure01(scalabilityParams.x);
            _debugHomeostasisPressureLevel = pressureLevel;
            _debugHomeostasisKillSwitchMaskLow32 = unchecked((uint)killSwitchMask);
            _debugBudgetedStepDistanceMeters = pressureBudget.StepDistanceMeters;
            _debugBudgetedShadowTaps = ResolveEffectiveShadowTaps(pressureBudget, killSwitchMask, pressureLevel);
            _staticBindingsDirty = _buffersReady;
        }

        private static Vector4 BuildContinuousScalabilityParams(
            float globalQualityWeight,
            byte pressureLevel,
            ulong killSwitchMask)
        {
            float q = math.saturate(globalQualityWeight);
            float pressure01 = math.saturate(pressureLevel * 0.33333334f);
            float stress01 = math.smoothstep(0.65f, 0.95f, ResolveSystemStress01());
            float policyFlowWeight = VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight(
                killSwitchMask,
                VfxComputeParticleBudgetCatalog.ParticleAdvectionMask,
                pressure01,
                VfxComputeParticleBudgetCatalog.MaskedParticleAdvectionWeightFloor);
            float policyCollisionWeight = VfxComputeParticleBudgetCatalog.ResolvePolicyQualityWeight(
                killSwitchMask,
                VfxComputeParticleBudgetCatalog.VolumetricFogHighResMask,
                pressure01,
                VfxComputeParticleBudgetCatalog.MaskedVolumetricQualityWeightFloor);
            float thermalQuality = q * math.lerp(1f, 0.18f, pressure01) * math.lerp(1f, 0.32f, stress01);
            float flowQuality = math.smoothstep(0.04f, 1f, thermalQuality) * policyFlowWeight;
            float collisionQuality = math.smoothstep(0.18f, 0.78f, thermalQuality) * policyCollisionWeight;
            float depthQuality = math.smoothstep(0.28f, 0.92f, thermalQuality) * policyCollisionWeight;
            return new Vector4(flowQuality * 2f, flowQuality, collisionQuality, depthQuality);
        }

        private static VfxComputeParticleBudget BuildContinuousPressureBudget(
            float globalQualityWeight,
            byte pressureLevel,
            ulong killSwitchMask)
        {
            float q = math.saturate(globalQualityWeight);
            float pressure01 = math.saturate(pressureLevel * 0.33333334f);
            float minimumToMiddle = math.smoothstep(0f, 0.45f, q);
            float middleToMaximum = math.smoothstep(0.35f, 0.85f, q);
            float maximumToOverkill = math.smoothstep(0.72f, 1f, q);
            float midPressure01 = math.smoothstep(0.18f, 0.45f, pressure01);
            float emergencyPressure01 = math.smoothstep(0.48f, 0.90f, pressure01);

            int marineSnowCount = ResolveContinuousBudgetCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount,
                VfxComputeParticleBudgetCatalog.MiddleQualityMarineSnowCount,
                VfxComputeParticleBudgetCatalog.MaximumQualityMarineSnowCount,
                VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill,
                midPressure01,
                emergencyPressure01);
            int bubbleCount = ResolveContinuousBudgetCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount,
                VfxComputeParticleBudgetCatalog.MiddleQualityBubbleCount,
                VfxComputeParticleBudgetCatalog.MaximumQualityBubbleCount,
                VfxComputeParticleBudgetCatalog.OverkillQualityBubbleCount,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill,
                midPressure01,
                emergencyPressure01);
            int debrisCount = ResolveContinuousBudgetCount(
                VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount,
                VfxComputeParticleBudgetCatalog.MiddleQualityDebrisCount,
                VfxComputeParticleBudgetCatalog.MaximumQualityDebrisCount,
                VfxComputeParticleBudgetCatalog.OverkillQualityDebrisCount,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill,
                midPressure01,
                emergencyPressure01);
            float stepDistanceMeters = ResolveContinuousBudgetFloat(
                VfxComputeParticleBudgetCatalog.MinimumQualityStepDistanceMeters,
                VfxComputeParticleBudgetCatalog.MiddleQualityStepDistanceMeters,
                VfxComputeParticleBudgetCatalog.MaximumQualityStepDistanceMeters,
                VfxComputeParticleBudgetCatalog.OverkillQualityStepDistanceMeters,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill);
            stepDistanceMeters = math.lerp(
                stepDistanceMeters,
                math.max(stepDistanceMeters, VfxComputeParticleBudgetCatalog.MiddleQualityStepDistanceMeters),
                midPressure01);
            stepDistanceMeters = math.lerp(
                stepDistanceMeters,
                VfxComputeParticleBudgetCatalog.MinimumQualityStepDistanceMeters,
                emergencyPressure01);

            float shadowTapFloat = ResolveContinuousBudgetFloat(
                VfxComputeParticleBudgetCatalog.MinimumQualityShadowTaps,
                VfxComputeParticleBudgetCatalog.MiddleQualityShadowTaps,
                VfxComputeParticleBudgetCatalog.MaximumQualityShadowTaps,
                VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill);
            shadowTapFloat = math.lerp(
                shadowTapFloat,
                math.min(shadowTapFloat, VfxComputeParticleBudgetCatalog.MiddleQualityShadowTaps),
                midPressure01);
            shadowTapFloat = math.lerp(
                shadowTapFloat,
                VfxComputeParticleBudgetCatalog.MinimumQualityShadowTaps,
                emergencyPressure01);
            int shadowTaps = math.clamp((int)(shadowTapFloat + 0.5f), 0, VfxComputeParticleBudgetCatalog.OverkillQualityShadowTaps);
            shadowTaps = VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps(
                shadowTaps,
                killSwitchMask,
                pressureLevel);

            float flowFramesFloat = ResolveContinuousBudgetFloat(
                VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames,
                VfxComputeParticleBudgetCatalog.MiddleQualityFlowResampleFrames,
                VfxComputeParticleBudgetCatalog.MaximumQualityFlowResampleFrames,
                VfxComputeParticleBudgetCatalog.OverkillQualityFlowResampleFrames,
                minimumToMiddle,
                middleToMaximum,
                maximumToOverkill);
            flowFramesFloat = math.lerp(flowFramesFloat, VfxComputeParticleBudgetCatalog.MinimumQualityFlowResampleFrames, emergencyPressure01);
            int flowResampleFrames = math.clamp((int)(flowFramesFloat + 0.5f), 0, VfxComputeParticleBudgetCatalog.MiddleQualityFlowResampleFrames);
            flowResampleFrames = VfxComputeParticleBudgetCatalog.ResolvePolicyFlowResampleFrames(
                flowResampleFrames,
                killSwitchMask,
                pressureLevel);

            return new VfxComputeParticleBudget(
                marineSnowCount + bubbleCount + debrisCount,
                marineSnowCount,
                bubbleCount,
                debrisCount,
                math.max(0.05f, stepDistanceMeters),
                shadowTaps,
                flowResampleFrames);
        }

        private static int ResolveContinuousBudgetCount(
            int minimum,
            int middle,
            int maximum,
            int overkill,
            float minimumToMiddle,
            float middleToMaximum,
            float maximumToOverkill,
            float midPressure01,
            float emergencyPressure01)
        {
            float value = ResolveContinuousBudgetFloat(minimum, middle, maximum, overkill, minimumToMiddle, middleToMaximum, maximumToOverkill);
            value = math.lerp(value, math.min(value, middle), midPressure01);
            value = math.lerp(value, minimum, emergencyPressure01);
            return math.max(0, (int)(value + 0.5f));
        }

        private static float ResolveContinuousBudgetFloat(
            float minimum,
            float middle,
            float maximum,
            float overkill,
            float minimumToMiddle,
            float middleToMaximum,
            float maximumToOverkill)
        {
            float value = math.lerp(minimum, middle, minimumToMiddle);
            value = math.lerp(value, maximum, middleToMaximum);
            return math.lerp(value, overkill, maximumToOverkill);
        }

        private static int ResolveContinuousPoolCapacity(
            VFXEmissionProfile.FluidType fluidType,
            float globalQualityWeight)
        {
            float q = math.saturate(globalQualityWeight);
            int minimum = ResolvePoolCapacityForRow(fluidType, VfxComputeParticleBudgetCatalog.MinimumQualityMarineSnowCount, VfxComputeParticleBudgetCatalog.MinimumQualityBubbleCount, VfxComputeParticleBudgetCatalog.MinimumQualityDebrisCount);
            int middle = ResolvePoolCapacityForRow(fluidType, VfxComputeParticleBudgetCatalog.MiddleQualityMarineSnowCount, VfxComputeParticleBudgetCatalog.MiddleQualityBubbleCount, VfxComputeParticleBudgetCatalog.MiddleQualityDebrisCount);
            int maximum = ResolvePoolCapacityForRow(fluidType, VfxComputeParticleBudgetCatalog.MaximumQualityMarineSnowCount, VfxComputeParticleBudgetCatalog.MaximumQualityBubbleCount, VfxComputeParticleBudgetCatalog.MaximumQualityDebrisCount);
            int overkill = ResolvePoolCapacityForRow(fluidType, VfxComputeParticleBudgetCatalog.OverkillQualityMarineSnowCount, VfxComputeParticleBudgetCatalog.OverkillQualityBubbleCount, VfxComputeParticleBudgetCatalog.OverkillQualityDebrisCount);
            float minimumToMiddle = math.smoothstep(0f, 0.45f, q);
            float middleToMaximum = math.smoothstep(0.35f, 0.85f, q);
            float maximumToOverkill = math.smoothstep(0.72f, 1f, q);
            float capacity = math.lerp(minimum, middle, minimumToMiddle);
            capacity = math.lerp(capacity, maximum, middleToMaximum);
            capacity = math.lerp(capacity, overkill, maximumToOverkill);
            return math.clamp((int)(capacity + 0.5f), 64, MaxMarineSnowParticleCapacity);
        }

        private static int ResolvePoolCapacityForRow(
            VFXEmissionProfile.FluidType fluidType,
            int marineSnowCount,
            int bubbleCount,
            int debrisCount)
        {
            switch (fluidType)
            {
                case VFXEmissionProfile.FluidType.Bubble:
                    return bubbleCount;
                case VFXEmissionProfile.FluidType.Debris:
                    return debrisCount;
                default:
                    return marineSnowCount;
            }
        }

        private static int ResolveEffectiveShadowTaps(
            VfxComputeParticleBudget budget,
            ulong killSwitchMask,
            byte pressureLevel)
        {
            return VfxComputeParticleBudgetCatalog.ResolvePolicyShadowTaps(
                budget.ShadowTaps,
                killSwitchMask,
                pressureLevel);
        }

        private static T ResolveComponentOnTransform<T>(Transform source) where T : Component
        {
            if (source == null)
                return null;

            return source.TryGetComponent(out T component) ? component : null;
        }

        private static T ResolveComponentInParents<T>(Transform start) where T : Component
        {
            Transform current = start;
            while (current != null)
            {
                if (current.TryGetComponent(out T component))
                    return component;

                current = current.parent;
            }

            return null;
        }
    }
}
