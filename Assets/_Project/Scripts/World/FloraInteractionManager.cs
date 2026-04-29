using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Systems.AI;
using Hecton.Localization;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.World
{
    /// <summary>
    /// Publishes global vegetation interaction and environment shader inputs.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]
    public sealed class FloraInteractionManager : MonoBehaviour, ITickable, IOriginShiftListener
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct FloraInteractionPointGpuData
        {
            public Vector4 PositionRadius;
            public Vector4 VelocitySpeed;
        }

        private struct WakeTrailStampCommand
        {
            public Vector4 UvEllipse;
            public Vector4 DirectionStrengthVertical;
        }

        private struct ModuleParasiteState
        {
            public float PowerDrainWatts;
            public float InfectionLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FloraCascadeEventPayload
        {
            public float3 Center;
            public float StartTimeSeconds;
            public float RadiusMeters;
            public float Padding;
        }

        private struct DefensiveSporeBurstState
        {
            public Vector3 PositionWS;
            public float Radius;
            public float Intensity;
            public float ExpireTimeSeconds;
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Low)]
        private struct PopulateCascadePhaseSeedsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Matrix4x4> Matrices;
            [ReadOnly] public NativeArray<HectonVegetationInstanceData> Metadata;
            [ReadOnly] public NativeArray<byte> ReactiveTemplateMask;
            [ReadOnly] public NativeArray<FloraCascadeEventPayload> Events;
            public int EventCount;
            public float PropagationSpeedMetersPerSecond;
            public float InactiveSeed;

            [WriteOnly] public NativeArray<float> PhaseSeeds;

            public void Execute(int index)
            {
                if (!Metadata.IsCreated ||
                    !ReactiveTemplateMask.IsCreated ||
                    index < 0 ||
                    index >= Metadata.Length ||
                    index >= Matrices.Length ||
                    EventCount <= 0)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                HectonVegetationInstanceData instanceData = Metadata[index];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(instanceData.HeightScale) <= 0.0001f)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                int templateIndex = (int)math.round(instanceData.TemplateIndex);
                if (templateIndex < 0 || templateIndex >= ReactiveTemplateMask.Length || ReactiveTemplateMask[templateIndex] == 0)
                {
                    PhaseSeeds[index] = InactiveSeed;
                    return;
                }

                float3 position = new float3(Matrices[index].m03, Matrices[index].m13, Matrices[index].m23);
                float bestSeed = InactiveSeed;
                bool found = false;
                float safeSpeed = math.max(0.01f, PropagationSpeedMetersPerSecond);
                int safeEventCount = math.min(EventCount, Events.Length);
                for (int eventIndex = 0; eventIndex < safeEventCount; eventIndex++)
                {
                    FloraCascadeEventPayload cascadeEvent = Events[eventIndex];
                    float distance = math.distance(position, cascadeEvent.Center);
                    if (distance > cascadeEvent.RadiusMeters)
                        continue;

                    float activationTime = cascadeEvent.StartTimeSeconds + (distance / safeSpeed);
                    if (!found || activationTime < bestSeed)
                    {
                        bestSeed = activationTime;
                        found = true;
                    }
                }

                PhaseSeeds[index] = found ? bestSeed : InactiveSeed;
            }
        }

        private const int MaxPublishedInteractionPoints = 12;
        private const int MaxExternalInteractionPoints = 4;
        private const int MaxQueryColliders = 32;
        private const int MaxModuleQueryHits = 32;
        private const int MaxParasiteAnchors = 16;
        private const int MaxCascadeEvents = 4;
        private const int MaxDefensiveSporeBursts = 6;
        private const int InteractionPointStride = 32;
        private const int FlowFieldStride = sizeof(float) * 2;
        private const float DefaultVegetationWaterLevel = 4900f;
        private const float FlowFieldUploadIntervalSeconds = 0.1f;
        private const float FlowFieldRecenterThresholdCells = 0.5f;
        private const int WakeTrailStampCommandCapacity = 4;
        private const int WakeTrailThreadGroupSize = 8;
        private const int ReactiveFloraKindMask = 1;
        private const float InactiveCascadeSeed = -100000f;
        private const int ToxicSporeHazardSourceId = unchecked((int)0x6B13A7F1);
        private const int DefensiveSporeHazardSourceId = unchecked((int)0x52F1063A);
#if UNITY_EDITOR
        private const string WakeTrailSimulationComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_VegetationWakeTrailSim.compute";
#endif

        private static readonly int _PropWashPosId = Shader.PropertyToID("_HectonPropWashPosition");
        private static readonly int _PropWashForceId = Shader.PropertyToID("_HectonPropWashForce");
        private static readonly int _InteractionBufferId = Shader.PropertyToID("_HectonFloraInteractionPoints");
        private static readonly int _InteractionCountId = Shader.PropertyToID("_HectonFloraInteractionCount");
        private static readonly int _MarineSnowFlowFieldId = Shader.PropertyToID("_MarineSnowFlowField");
        private static readonly int _MarineSnowFlowFieldCenterCellSizeId = Shader.PropertyToID("_MarineSnowFlowFieldCenterCellSize");
        private static readonly int _FloraFlowFieldResolutionId = Shader.PropertyToID("_HectonFloraFlowFieldResolution");
        private static readonly int _PlayerRuntimePositionId = Shader.PropertyToID("_HectonPlayerRuntimePosition");
        private static readonly int _PlayerFloraInteractionParamsId = Shader.PropertyToID("_HectonPlayerFloraInteractionParams");
        private static readonly int _VegetationFogColorId = Shader.PropertyToID("_HectonVegetationFogColor");
        private static readonly int _VegetationAmbientColorId = Shader.PropertyToID("_HectonVegetationAmbientColor");
        private static readonly int _VegetationDepthId = Shader.PropertyToID("_HectonVegetationDepth");
        private static readonly int _VegetationLightFactorId = Shader.PropertyToID("_HectonVegetationLightFactor");
        private static readonly int _VegetationTurbidityId = Shader.PropertyToID("_HectonVegetationTurbidity");
        private static readonly int _VegetationWaterLevelId = Shader.PropertyToID("_HectonVegetationWaterLevel");
        private static readonly int _VegetationCurrentVectorId = Shader.PropertyToID("_HectonVegetationCurrentVector");
        private static readonly int _GlobalOceanFlowId = Shader.PropertyToID("_GlobalOceanFlow");
        private static readonly int _VegetationCurrentStrengthId = Shader.PropertyToID("_HectonVegetationCurrentStrength");
        private static readonly int _VegetationCurrentNoiseScaleId = Shader.PropertyToID("_HectonVegetationCurrentNoiseScale");
        private static readonly int _VegetationCurrentTimeScaleId = Shader.PropertyToID("_HectonVegetationCurrentTimeScale");
        private static readonly int _VegetationCurrentVerticalFactorId = Shader.PropertyToID("_HectonVegetationCurrentVerticalFactor");
        private static readonly int _FloraPredatorThreatParamsId = Shader.PropertyToID("_HectonFloraPredatorThreatParams");
        private static readonly int _FloraLifecycleParamsId = Shader.PropertyToID("_HectonFloraLifecycleParams");
        private static readonly int _FloraCascadeParamsId = Shader.PropertyToID("_HectonFloraCascadeParams");
        private static readonly int _WakeTrailTextureId = Shader.PropertyToID("_HectonVegetationWakeTrailRT");
        private static readonly int _WakeTrailWorldRectId = Shader.PropertyToID("_HectonVegetationWakeTrailWorldRect");
        private static readonly int _WakeTrailActiveId = Shader.PropertyToID("_HectonVegetationWakeTrailActive");
        private static readonly int _ShallowWaterFieldTextureId = Shader.PropertyToID("_HectonShallowWaterFieldRT");
        private static readonly int _ShallowWaterFieldWorldRectId = Shader.PropertyToID("_HectonShallowWaterFieldWorldRect");
        private static readonly int _ShallowWaterFieldActiveId = Shader.PropertyToID("_HectonShallowWaterFieldActive");
        private static readonly int _ShallowWaterFieldTexelSizeId = Shader.PropertyToID("_HectonShallowWaterFieldTexelSize");
        private static readonly int _WakeTrailSourceId = Shader.PropertyToID("_HectonWakeTrailSource");
        private static readonly int _WakeTrailResultId = Shader.PropertyToID("_HectonWakeTrailResult");
        private static readonly int _WakeTrailFadeDeltaId = Shader.PropertyToID("_HectonWakeTrailFadeDelta");
        private static readonly int _WakeTrailDiffusionId = Shader.PropertyToID("_HectonWakeTrailDiffusion");
        private static readonly int _WakeTrailWaveStrengthId = Shader.PropertyToID("_HectonWakeTrailWaveStrength");
        private static readonly int _WakeTrailDampingId = Shader.PropertyToID("_HectonWakeTrailDamping");
        private static readonly int _WakeTrailCurlStrengthId = Shader.PropertyToID("_HectonWakeTrailCurlStrength");
        private static readonly int _WakeTrailSimulationTimeId = Shader.PropertyToID("_HectonWakeTrailSimulationTime");
        private static readonly int _WakeTrailTexelSizeId = Shader.PropertyToID("_HectonWakeTrailTexelSize");
        private static readonly int _WakeTrailStampCommandsId = Shader.PropertyToID("_HectonWakeTrailStampCommands");
        private static readonly int _WakeTrailStampCountId = Shader.PropertyToID("_HectonWakeTrailStampCount");
        private static readonly int _WakeTrailScrollUvOffsetId = Shader.PropertyToID("_HectonWakeTrailScrollUvOffset");
        private static readonly int _ParasiteAnchorDataId = Shader.PropertyToID("_HectonParasiteAnchorData");
        private static readonly int _ParasiteAnchorParamsId = Shader.PropertyToID("_HectonParasiteAnchorParams");
        private static readonly int _ParasiteGlobalsId = Shader.PropertyToID("_HectonParasiteGlobals");

        [Header("Runtime Wiring")]
        [SerializeField]
        [Tooltip("Optional direct player override for direct scene play mode when BootstrapState has not published a runtime player yet.")]
        private Transform _playerTransformOverride;

        [SerializeField]
        [Tooltip("Optional direct scooter transform override for isolated prefab or broken-scene validation.")]
        private Transform _scooterTransformOverride;

        [SerializeField]
        [Tooltip("Optional vegetation bridge override used for dense-grass heuristics and sediment interaction bursts.")]
        private HectonMapMagicVegetationBridge _vegetationBridgeOverride;

        [Header("Interaction")]
        [SerializeField, Range(1f, 10f)]
        [Tooltip("Base radius around the player influence point for legacy prop-wash style vegetation response.")]
        private float _baseRadius = 3.5f;

        [SerializeField, Range(0f, 5f)]
        [Tooltip("How much player speed increases the published legacy interaction radius.")]
        private float _velocityRadiusMultiplier = 0.45f;

        [SerializeField, Range(0.1f, 10f)]
        [Tooltip("Maximum player interaction force pushed into legacy vegetation shader parameters.")]
        private float _maxInteractionForce = 4.2f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Position smoothing speed for the player interaction point.")]
        private float _positionSmoothSpeed = 12f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Radius and force smoothing speed for the player interaction point.")]
        private float _intensitySmoothSpeed = 8f;

        [Header("Velocity Bend")]
        [SerializeField, Range(1, MaxPublishedInteractionPoints)]
        [Tooltip("Maximum number of interaction points published to the global vegetation buffer, including the player.")]
        private int _maxInteractionPoints = 12;

        [SerializeField, Range(4f, 20f)]
        [Tooltip("Attention radius for collecting dynamic object interaction points around the player.")]
        private float _dynamicInteractionRadius = 15f;

        [SerializeField, Range(1.5f, 3f)]
        [Tooltip("Base true-bend radius used for the player interaction point.")]
        private float _playerBendRadius = 2.4f;

        [SerializeField, Range(1.5f, 3f)]
        [Tooltip("Base true-bend radius used for non-player dynamic objects.")]
        private float _dynamicObjectBaseRadius = 2.2f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Extra true-bend radius per meter per second of velocity.")]
        private float _dynamicVelocityRadiusMultiplier = 0.08f;

        [SerializeField, Range(2f, 3f)]
        [Tooltip("Maximum true-bend radius applied to interaction points.")]
        private float _maxBendRadius = 2.9f;

        [SerializeField, Range(1.5f, 4f)]
        [Tooltip("Base bend radius published for the active Manta scooter wake point.")]
        private float _scooterBendRadius = 2.8f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Velocity multiplier used for the active Manta scooter wake point.")]
        private float _scooterVelocityMultiplier = 1.35f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Forward offset used to move the scooter wake point ahead of the held tool transform.")]
        private float _scooterForwardOffset = 0.4f;

        [SerializeField, Range(0.05f, 0.5f)]
        [Tooltip("Spring rise time for new or fast-changing vegetation interaction vectors.")]
        private float _interactionRiseSmoothTime = 0.12f;

        [SerializeField, Range(0.5f, 2f)]
        [Tooltip("Spring recovery time used when interaction sources stop pushing the vegetation field.")]
        private float _interactionRecoverySmoothTime = 1.25f;

        [SerializeField, Range(0.01f, 0.25f)]
        [Tooltip("Velocity threshold below which a recovered interaction point is dropped from publication.")]
        private float _interactionReleaseSpeed = 0.08f;

        [SerializeField]
        [Tooltip("Physics layers considered for dynamic vegetation interaction queries.")]
        private LayerMask _dynamicInteractionMask = ~0;

        [Header("Biolum Stealth")]
        [SerializeField, Range(2f, 48f)]
        [Tooltip("Search radius used to detect aggressive bioforms that should dim nearby flora emission around the player.")]
        private float _predatorThreatQueryRadius = 18f;

        [SerializeField, Range(1f, 32f)]
        [Tooltip("World radius around the player over which nearby flora emission is dimmed when predators are close.")]
        private float _predatorBiolumDimRadius = 10f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Global scalar applied to the predator-driven flora bioluminescence dimming effect.")]
        private float _predatorBiolumDimStrength = 0.75f;

        [SerializeField]
        [Tooltip("Physics layers considered when querying the nearest aggressive bioform for flora stealth dimming.")]
        private LayerMask _predatorThreatMask = ~0;

        [Header("Seasonal Lifecycle")]
        [SerializeField, Range(120f, 3600f)]
        [Tooltip("Simulation-time length in seconds of one flora bloom-to-decay lifecycle.")]
        private float _seasonCycleSeconds = 720f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized cycle position where the bloom phase peaks.")]
        private float _bloomPhaseCenterNormalized = 0.22f;

        [SerializeField, Range(0.05f, 0.45f)]
        [Tooltip("Normalized bloom phase half-width used to shape the seasonal bioluminescence window.")]
        private float _bloomPhaseWidthNormalized = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Normalized cycle position where the decay phase peaks.")]
        private float _decayPhaseCenterNormalized = 0.76f;

        [SerializeField, Range(0.05f, 0.45f)]
        [Tooltip("Normalized decay phase half-width used to shape the seasonal wilt window.")]
        private float _decayPhaseWidthNormalized = 0.22f;

        [SerializeField, Range(1f, 4f)]
        [Tooltip("Maximum global bioluminescence multiplier applied during bloom windows.")]
        private float _bloomEmissionBoost = 2.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Global wilt deformation strength applied during decay windows.")]
        private float _decayWiltStrength = 0.82f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Extra bloom weight injected while the encounter pacing director ramps or peaks.")]
        private float _encounterBloomBias = 0.16f;

        [SerializeField, Range(0f, 0.75f)]
        [Tooltip("Extra decay weight injected while the encounter pacing director enters Decay.")]
        private float _encounterDecayBias = 0.38f;

        [Header("Bioluminescent Cascades")]
        [SerializeField]
        [Tooltip("Stable flora template ids that participate in the reactive nerve-vine bioluminescent cascade network.")]
        private string[] _cascadeReactiveStableIds = { "flora.nerve_vine" };

        [SerializeField, Range(0.5f, 8f)]
        [Tooltip("World-space contact radius used to detect a player brushing into one reactive flora source.")]
        private float _cascadeContactRadius = 2.2f;

        [SerializeField, Range(8f, 320f)]
        [Tooltip("Maximum propagation radius used when a cascade wave spreads through the reactive flora network.")]
        private float _cascadePropagationRadius = 160f;

        [SerializeField, Range(1f, 40f)]
        [Tooltip("Wavefront speed in meters per second written into the reactive flora phase-seed buffer.")]
        private float _cascadePropagationSpeed = 15f;

        [SerializeField, Range(0.15f, 8f)]
        [Tooltip("Duration of the bright cascade crest once a reactive plant receives the propagated phase seed.")]
        private float _cascadePulseDurationSeconds = 2.4f;

        [SerializeField, Range(0.5f, 16f)]
        [Tooltip("Time window in seconds during which one cascade seed remains valid before fading back to idle.")]
        private float _cascadeReleaseDurationSeconds = 5.5f;

        [SerializeField, Range(0f, 4f)]
        [Tooltip("Extra emission multiplier applied by the reactive flora shader while a cascade crest passes through an instance.")]
        private float _cascadeEmissionBoost = 2.2f;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Minimum time between retriggering the same cascade source while the player remains in contact.")]
        private float _cascadeRetriggerCooldownSeconds = 1.15f;

        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("Cadence used to rebuild the reactive flora spatial hashes from the active streamed vegetation payloads.")]
        private float _cascadeSpatialRefreshIntervalSeconds = 0.5f;

        [Header("Toxic Spores")]
        [SerializeField]
        [Tooltip("Stable flora template id treated as a toxic-spore emitter when the player enters its near-field volume.")]
        private string _toxicSporeStableId = "flora.ghost_weed";

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("How often the flora runtime scans active instance payloads for toxic-spore emitters near the player.")]
        private float _toxicSporeScanIntervalSeconds = 0.2f;

        [SerializeField, Range(1f, 8f)]
        [Tooltip("Maximum world-space distance from the player to a toxic spore emitter before exposure begins.")]
        private float _toxicSporeDetectionRadius = 4.5f;

        [SerializeField, Range(1f, 8f)]
        [Tooltip("Hazard volume radius registered around the nearest toxic spore emitter.")]
        private float _toxicSporeHazardRadius = 3f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Peak toxicity intensity registered into the trauma hazard pipeline while the player remains inside spores.")]
        private float _toxicSporeHazardIntensity = 0.78f;

        [SerializeField, Range(1f, 3f)]
        [Tooltip("External environmental drag multiplier applied to the player while spores remain active.")]
        private float _toxicSporeDragMultiplier = 1.3f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Extra visor glitch bias forwarded into the hazard runtime for toxic-spore interference.")]
        private float _toxicSporeVisorGlitchBias = 1.25f;

        [Header("Base Parasitism")]
        [SerializeField, Range(0.1f, 5f)]
        [Tooltip("How often the flora runtime rescans active module parasites and thermophilic growth state.")]
        private float _moduleParasiteScanIntervalSeconds = 0.5f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Maximum distance from a parasitic flora instance to a module contact before the latch is discarded.")]
        private float _moduleParasiteAttachmentRadius = 4f;

        [SerializeField, Range(0.5f, 12f)]
        [Tooltip("Maximum distance from a module to the nearest active BioReactor used for thermophilic host validation.")]
        private float _thermophileReactorValidationRadius = 5f;

        [SerializeField]
        [Tooltip("Stable thermophilic flora id resolved for overheated module growth when the authored template is present.")]
        private string _thermalTubewormStableId = "flora.thermal_tubeworm";

        [Header("Defensive Spore Bursts")]
        [SerializeField]
        [Tooltip("Stable flora template ids that detonate into a defensive toxicity cloud when cut or drilled.")]
        private string[] _defensiveSporeBurstStableIds = { "flora.fungal_stalk", "flora.spore_cannon", "flora.acid_shroom" };

        [SerializeField, Range(1f, 20f)]
        [Tooltip("World-space radius of the defensive spore cloud injected after a reactive fungal stalk bursts.")]
        private float _defensiveSporeBurstRadius = 7f;

        [SerializeField, Range(0.25f, 16f)]
        [Tooltip("Chemical-grid toxicity dose injected at the burst origin when a reactive fungal stalk detonates.")]
        private float _defensiveSporeBurstDose = 8f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Lifetime in seconds of the localized blind/toxic zone created by a defensive spore burst.")]
        private float _defensiveSporeBurstLifetimeSeconds = 10f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Hazard intensity registered against the player while they remain inside a defensive spore cloud.")]
        private float _defensiveSporeHazardIntensity = 1.15f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Extra visor-trauma bias applied while the player remains inside a defensive spore cloud.")]
        private float _defensiveSporeVisorGlitchBias = 1.75f;

        [Header("Wake Trail")]
        [SerializeField, Range(64f, 192f)]
        [Tooltip("World-space coverage of the shallow-water field centered around the player.")]
        private float _wakeTrailWorldSize = 128f;

        [SerializeField, Range(8f, 16f)]
        [Tooltip("Seconds required for the persistent wake trail to fade out back to calm water.")]
        private float _wakeTrailFadeSeconds = 12f;

        [SerializeField, Range(0.1f, 2f)]
        [Tooltip("Persistent wake intensity written by the player body when moving through vegetation.")]
        private float _wakeTrailPlayerStrength = 0.28f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Persistent wake intensity written by the active Manta scooter.")]
        private float _wakeTrailScooterStrength = 0.95f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base half-width of each wake trail stamp in world meters.")]
        private float _wakeTrailBaseRadius = 1.35f;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum world-space trail length written per wake stamp.")]
        private float _wakeTrailMinLength = 2.4f;

        [SerializeField, Range(4f, 30f)]
        [Tooltip("Maximum world-space trail length written per wake stamp.")]
        private float _wakeTrailMaxLength = 15f;

        [SerializeField, Range(0.05f, 0.75f)]
        [Tooltip("Extra trail length written per meter per second of source velocity.")]
        private float _wakeTrailVelocityToLength = 0.28f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum player speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailPlayerMinSpeed = 0.75f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum scooter speed required before persistent wake stamps start accumulating.")]
        private float _wakeTrailScooterMinSpeed = 0.45f;

        [SerializeField, Range(0.25f, 2f)]
        [Tooltip("Persistent wake intensity written by the active submarine while moving through macro-flora.")]
        private float _wakeTrailSubmarineStrength = 0.82f;

        [SerializeField, Range(0.25f, 4f)]
        [Tooltip("Minimum submarine speed required before persistent flora wake stamps start accumulating.")]
        private float _wakeTrailSubmarineMinSpeed = 2.5f;

        [SerializeField, Range(8f, 24f)]
        [Tooltip("Submarine speed threshold that upgrades the wake field into a kelp-whip stamp.")]
        private float _wakeTrailSubmarineWhipSpeed = 15f;

        [SerializeField, Range(1f, 6f)]
        [Tooltip("Base world radius of the submarine flora wake stamp.")]
        private float _wakeTrailSubmarineRadius = 3.2f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Pixel stride used to quantize treadmill recentering and avoid sub-pixel wake shimmer.")]
        private float _wakeTrailCenterSnapPixelStride = 1f;

        [SerializeField]
        [Tooltip("Optional compute shader used to evolve the persistent wake trail into reactive spreading ripples.")]
        private ComputeShader _wakeTrailSimulationCompute;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Neighbor blending factor used by the wake ripple simulation.")]
        private float _wakeTrailDiffusion = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Wave propagation strength used when the wake trail expands into surrounding water.")]
        private float _wakeTrailWaveStrength = 0.36f;

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Per-step damping used by the wake ripple simulation.")]
        private float _wakeTrailWaveDamping = 0.94f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Curl-noise advection strength used to form micro-vortices inside the reactive wake field.")]
        private float _wakeTrailCurlStrength = 0.42f;

        [Header("Sediment Interaction")]
        [SerializeField]
        [Tooltip("Optional scene particle system used for sediment bursts kicked out of dense grass. If null, a hidden local system is created once.")]
        private ParticleSystem _sedimentBurstParticleSystem;

        [SerializeField, Range(1024, 65535)]
        [Tooltip("Minimum active surface instance count required before the manager considers the current area dense enough for grass sediment bursts.")]
        private int _denseGrassInstanceThreshold = 8192;

        [SerializeField, Range(1f, 20f)]
        [Tooltip("Minimum speed required before player movement starts emitting sediment bursts in dense grass.")]
        private float _playerSedimentMinSpeed = 4.5f;

        [SerializeField, Range(1f, 30f)]
        [Tooltip("Minimum speed required before scooter wake starts emitting sediment bursts in dense grass.")]
        private float _scooterSedimentMinSpeed = 7.5f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between player sediment burst emissions.")]
        private float _playerSedimentCooldown = 0.16f;

        [SerializeField, Range(0.05f, 1f)]
        [Tooltip("Minimum time between scooter sediment burst emissions.")]
        private float _scooterSedimentCooldown = 0.09f;

        [SerializeField, Range(0.5f, 4f)]
        [Tooltip("Base world radius of one sediment burst stamp.")]
        private float _sedimentBurstRadius = 1.3f;

        [SerializeField, Range(2, 32)]
        [Tooltip("Maximum particle count emitted by one dense-grass sediment burst.")]
        private int _sedimentMaxBurstCount = 18;

        private Vector3 _smoothPosition;
        private float _smoothRadius;
        private float _smoothForce;
        private Transform _playerTransform;
        private Rigidbody _playerRb;
        private HectonPlayerMovement _playerMovement;
        private PlayerToolManager _playerToolManager;
        private Transform _activeScooterTransform;
        private Vector3 _lastPlayerPosition;
        private Vector3 _lastPublishedPlayerVelocity;
        private Vector3 _lastPublishedScooterWakePosition;
        private Vector3 _lastPublishedSubmarineWakePosition;
        private Vector3 _smoothedPlayerVelocity;
        private Vector3 _smoothedPlayerVelocityDamp;
        private Vector3 _smoothedScooterVelocity;
        private Vector3 _smoothedScooterVelocityDamp;
        private Vector3 _smoothedScooterPosition;
        private Vector3 _smoothedScooterPositionDamp;
        private bool _hasLastPlayerPosition;
        private bool _hasSmoothedScooterPosition;
        private bool _hasActiveScooterWake;
        private bool _hasActiveSubmarineWake;
        private bool _isRegistered;
        private int _lastPublishedInteractionCount;
        private int _externalInteractionCount;

        private FloraInteractionPointGpuData[] _interactionPoints;
        private FloraInteractionPointGpuData[] _externalInteractionPoints;
        private Collider[] _interactionColliders;
        private Rigidbody[] _interactionBodies;
        private GraphicsBuffer _interactionBuffer;
        private GraphicsBuffer _flowFieldBuffer;
        private GraphicsBuffer _wakeTrailStampCommandBuffer;
        private RenderTexture _wakeTrailRead;
        private RenderTexture _wakeTrailWrite;
        private Vector4 _wakeTrailWorldRect;
        private Vector2 _wakeTrailCenterXZ;
        private Vector2 _pendingWakeTrailScrollUv;
        private NativeArray<WakeTrailStampCommand> _queuedWakeTrailStampCommands;
        private float _wakeTrailRuntimeWorldSize;
        private float _wakeTrailEnergy;
        private float _playerSedimentCooldownRemaining;
        private float _scooterSedimentCooldownRemaining;
        private bool _wakeTrailDisabled;
        private int _queuedWakeTrailStampCount;
        private int _lastWakeTrailDispatchFrame = -1;
        private int _wakeTrailRuntimeResolution;
        private int _wakeTrailQualityLevel = -1;
        private int _wakeTrailSimulationKernel = -1;
        private int _flowFieldResolution;
        private float _flowFieldCellSize;
        private float _flowFieldUploadTimer;
        private HectonMapMagicVegetationBridge _vegetationBridge;
        private IHectonOceanKinematics _oceanKinematicsProvider;
        private Vector3 _flowFieldCenterWS;
        private Vector3 _lastUploadedFlowFieldCenterWS;
        private NativeArray<Vector3> _oceanFlowSamplePositions;
        private NativeArray<Vector3> _oceanFlowSampleResults;
        private ParticleSystem.EmitParams _sedimentEmitParams;
        private bool[] _toxicSporeTemplateMask = Array.Empty<bool>();
        private int _cachedToxicSporeTemplateCount = -1;
        private int _toxicSporeStableHashId;
        private int _thermalTubewormStableHashId;
        private float _toxicSporeScanTimer;
        private float _lastToxicSporeExposure01;
        private float _moduleParasiteScanTimer;
        private SpatialQueryHit[] _moduleQueryHits;
        private readonly Vector4[] _parasiteAnchorData = new Vector4[MaxParasiteAnchors];
        private readonly Vector4[] _parasiteAnchorParams = new Vector4[MaxParasiteAnchors];
        private int _publishedParasiteAnchorCount;
        private Dictionary<BaseModule, ModuleParasiteState> _moduleParasiteStateFront = new Dictionary<BaseModule, ModuleParasiteState>(16);
        private Dictionary<BaseModule, ModuleParasiteState> _moduleParasiteStateBack = new Dictionary<BaseModule, ModuleParasiteState>(16);
        private readonly Dictionary<BaseModule, float> _thermophileDwellSeconds = new Dictionary<BaseModule, float>(8);
        private readonly List<BaseModule> _staleParasiticModules = new List<BaseModule>(16);
        private int[] _cascadeReactiveStableHashIds = Array.Empty<int>();
        private int[] _defensiveSporeBurstStableHashIds = Array.Empty<int>();
        private int _cachedCascadeReactiveTemplateCount = -1;
        private int _cachedDefensiveSporeBurstTemplateCount = -1;
        private float _cascadeSpatialRefreshTimer;
        private float _lastSurfaceCascadeTriggerTime = float.MinValue;
        private float _lastUnderwaterCascadeTriggerTime = float.MinValue;
        private int _lastSurfaceCascadeSourcePayloadIndex = -1;
        private int _lastUnderwaterCascadeSourcePayloadIndex = -1;
        private int _lastSurfacePlayerContactPayloadIndex = -1;
        private int _lastUnderwaterPlayerContactPayloadIndex = -1;
        private NativeArray<byte> _cascadeReactiveTemplateMask;
        private NativeArray<byte> _defensiveSporeBurstTemplateMask;
        private NativeArray<float> _surfaceCascadePhaseSeeds;
        private NativeArray<float> _underwaterCascadePhaseSeeds;
        private NativeArray<FloraCascadeEventPayload> _surfaceCascadeEvents;
        private NativeArray<FloraCascadeEventPayload> _underwaterCascadeEvents;
        private NativeList<int> _surfaceReactiveFloraHandles;
        private NativeList<int> _underwaterReactiveFloraHandles;
        private NativeList<int> _reactiveFloraQueryHandles;
        private HectonSpatialHash _surfaceReactiveFloraHash;
        private HectonSpatialHash _underwaterReactiveFloraHash;
        private GraphicsBuffer _surfaceCascadePhaseSeedBuffer;
        private GraphicsBuffer _underwaterCascadePhaseSeedBuffer;
        private DefensiveSporeBurstState[] _defensiveSporeBursts;
        private int _defensiveSporeBurstCount;
        private int _surfaceCascadeEventCount;
        private int _underwaterCascadeEventCount;

        /// <summary>Last interaction point count pushed into the global flora buffer.</summary>
        public int PublishedInteractionCount => _lastPublishedInteractionCount;

        /// <summary>True when the active Manta scooter wake point is currently being published.</summary>
        public bool HasActiveScooterWake => _hasActiveScooterWake;

        /// <summary>Last published player velocity vector.</summary>
        public Vector3 LastPublishedPlayerVelocity => _lastPublishedPlayerVelocity;

        /// <summary>Last published scooter wake anchor position.</summary>
        public Vector3 LastPublishedScooterWakePosition => _lastPublishedScooterWakePosition;

        /// <summary>Approximate VRAM footprint in bytes for the wake-trail ping-pong textures and interaction buffer.</summary>
        public long GetVRAMEstimation()
        {
            long totalBytes = 0L;
            totalBytes += EstimateGraphicsBufferBytes(_interactionBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_flowFieldBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_surfaceCascadePhaseSeedBuffer);
            totalBytes += EstimateGraphicsBufferBytes(_underwaterCascadePhaseSeedBuffer);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailRead);
            totalBytes += EstimateRenderTextureBytes(_wakeTrailWrite);
            return totalBytes;
        }

        private void Awake()
        {
            _maxInteractionPoints = Mathf.Clamp(_maxInteractionPoints, 1, MaxPublishedInteractionPoints);
            _wakeTrailWorldSize = Mathf.Max(32f, _wakeTrailWorldSize);
            _wakeTrailFadeSeconds = Mathf.Max(0.1f, _wakeTrailFadeSeconds);
            _wakeTrailDiffusion = Mathf.Clamp01(_wakeTrailDiffusion);
            _wakeTrailWaveStrength = Mathf.Clamp01(_wakeTrailWaveStrength);
            _wakeTrailWaveDamping = Mathf.Clamp(_wakeTrailWaveDamping, 0.5f, 1f);
            _denseGrassInstanceThreshold = Mathf.Max(1024, _denseGrassInstanceThreshold);
            _sedimentMaxBurstCount = Mathf.Clamp(_sedimentMaxBurstCount, 2, 32);
            _predatorThreatQueryRadius = Mathf.Max(2f, _predatorThreatQueryRadius);
            _predatorBiolumDimRadius = Mathf.Max(1f, _predatorBiolumDimRadius);
            _predatorBiolumDimStrength = Mathf.Clamp01(_predatorBiolumDimStrength);
            _seasonCycleSeconds = Mathf.Max(120f, _seasonCycleSeconds);
            _bloomPhaseWidthNormalized = Mathf.Clamp(_bloomPhaseWidthNormalized, 0.05f, 0.45f);
            _decayPhaseWidthNormalized = Mathf.Clamp(_decayPhaseWidthNormalized, 0.05f, 0.45f);
            _bloomEmissionBoost = Mathf.Max(1f, _bloomEmissionBoost);
            _decayWiltStrength = Mathf.Clamp01(_decayWiltStrength);
            _encounterBloomBias = Mathf.Clamp(_encounterBloomBias, 0f, 0.5f);
            _encounterDecayBias = Mathf.Clamp(_encounterDecayBias, 0f, 0.75f);
            _cascadeContactRadius = Mathf.Max(0.5f, _cascadeContactRadius);
            _cascadePropagationRadius = Mathf.Max(8f, _cascadePropagationRadius);
            _cascadePropagationSpeed = Mathf.Max(1f, _cascadePropagationSpeed);
            _cascadePulseDurationSeconds = Mathf.Max(0.15f, _cascadePulseDurationSeconds);
            _cascadeReleaseDurationSeconds = Mathf.Max(_cascadePulseDurationSeconds, _cascadeReleaseDurationSeconds);
            _cascadeEmissionBoost = Mathf.Max(0f, _cascadeEmissionBoost);
            _cascadeRetriggerCooldownSeconds = Mathf.Max(0.1f, _cascadeRetriggerCooldownSeconds);
            _cascadeSpatialRefreshIntervalSeconds = Mathf.Max(0.1f, _cascadeSpatialRefreshIntervalSeconds);
            _toxicSporeScanIntervalSeconds = Mathf.Max(0.05f, _toxicSporeScanIntervalSeconds);
            _toxicSporeDetectionRadius = Mathf.Max(1f, _toxicSporeDetectionRadius);
            _toxicSporeHazardRadius = Mathf.Max(1f, _toxicSporeHazardRadius);
            _toxicSporeHazardIntensity = Mathf.Clamp01(_toxicSporeHazardIntensity);
            _toxicSporeDragMultiplier = Mathf.Max(1f, _toxicSporeDragMultiplier);
            _toxicSporeVisorGlitchBias = Mathf.Max(0f, _toxicSporeVisorGlitchBias);
            _moduleParasiteScanIntervalSeconds = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds);
            _moduleParasiteAttachmentRadius = Mathf.Max(0.5f, _moduleParasiteAttachmentRadius);
            _thermophileReactorValidationRadius = Mathf.Max(0.5f, _thermophileReactorValidationRadius);
            _defensiveSporeBurstRadius = Mathf.Max(1f, _defensiveSporeBurstRadius);
            _defensiveSporeBurstDose = Mathf.Max(0.25f, _defensiveSporeBurstDose);
            _defensiveSporeBurstLifetimeSeconds = Mathf.Max(1f, _defensiveSporeBurstLifetimeSeconds);
            _defensiveSporeHazardIntensity = Mathf.Max(0f, _defensiveSporeHazardIntensity);
            _defensiveSporeVisorGlitchBias = Mathf.Max(0f, _defensiveSporeVisorGlitchBias);
            _wakeTrailQualityLevel = QualitySettings.GetQualityLevel();
            _wakeTrailRuntimeResolution = ResolveWakeTrailResolutionForQuality(_wakeTrailQualityLevel);
            _vegetationBridge = ResolveVegetationBridge();
            _toxicSporeStableHashId = string.IsNullOrWhiteSpace(_toxicSporeStableId) ? 0 : LocHash.Compute(_toxicSporeStableId);
            _thermalTubewormStableHashId = string.IsNullOrWhiteSpace(_thermalTubewormStableId) ? 0 : LocHash.Compute(_thermalTubewormStableId);
            CacheStableHashIds(_cascadeReactiveStableIds, ref _cascadeReactiveStableHashIds);
            CacheStableHashIds(_defensiveSporeBurstStableIds, ref _defensiveSporeBurstStableHashIds);
            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute != null)
                _wakeTrailSimulationKernel = _wakeTrailSimulationCompute.FindKernel("SimulateWakeTrail");

            // COLD ALLOC: FloraInteractionPointGpuData[_maxInteractionPoints] - global vegetation interaction payload - owner: FloraInteractionManager
            _interactionPoints = new FloraInteractionPointGpuData[_maxInteractionPoints];
            // COLD ALLOC: FloraInteractionPointGpuData[4] - external tool-impact vegetation interaction payloads - owner: FloraInteractionManager
            _externalInteractionPoints = new FloraInteractionPointGpuData[MaxExternalInteractionPoints];
            // COLD ALLOC: Collider[32] - NonAlloc interaction query results - owner: FloraInteractionManager
            _interactionColliders = new Collider[MaxQueryColliders];
            // COLD ALLOC: Rigidbody[32] - duplicate suppression for interaction query results - owner: FloraInteractionManager
            _interactionBodies = new Rigidbody[MaxQueryColliders];
            // COLD ALLOC: SpatialQueryHit[32] - module-contact query results for parasitic flora host resolution - owner: FloraInteractionManager
            _moduleQueryHits = new SpatialQueryHit[MaxModuleQueryHits];
            _interactionBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<FloraInteractionPointGpuData>(_maxInteractionPoints); // COLD ALLOC: GraphicsBuffer[_maxInteractionPoints] - global vegetation interaction StructuredBuffer - owner: FloraInteractionManager
            // COLD ALLOC: NativeArray<Vector3>[1] - caller-owned ocean provider sample positions for vegetation flow publishing - owner: FloraInteractionManager
            _oceanFlowSamplePositions = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<Vector3>[1] - caller-owned ocean provider sample results for vegetation flow publishing - owner: FloraInteractionManager
            _oceanFlowSampleResults = new NativeArray<Vector3>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            _reactiveFloraQueryHandles = new NativeList<int>(64, Allocator.Persistent); // COLD ALLOC: NativeList<int>[64] - shared reactive flora spatial-query handle staging - owner: FloraInteractionManager
            _surfaceReactiveFloraHandles = new NativeList<int>(64, Allocator.Persistent); // COLD ALLOC: NativeList<int>[64] - registered surface reactive-flora spatial handles - owner: FloraInteractionManager
            _underwaterReactiveFloraHandles = new NativeList<int>(64, Allocator.Persistent); // COLD ALLOC: NativeList<int>[64] - registered underwater reactive-flora spatial handles - owner: FloraInteractionManager
            _surfaceCascadeEvents = new NativeArray<FloraCascadeEventPayload>(MaxCascadeEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FloraCascadeEventPayload>[4] - bounded active surface cascade wavefront descriptors - owner: FloraInteractionManager
            _underwaterCascadeEvents = new NativeArray<FloraCascadeEventPayload>(MaxCascadeEvents, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<FloraCascadeEventPayload>[4] - bounded active underwater cascade wavefront descriptors - owner: FloraInteractionManager
            _defensiveSporeBursts = new DefensiveSporeBurstState[MaxDefensiveSporeBursts]; // COLD ALLOC: DefensiveSporeBurstState[6] - bounded active toxicity cloud descriptors - owner: FloraInteractionManager

            Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
            PublishFlowFieldGlobals();
            CreateWakeTrailResources();
            EnsureSedimentParticleSystem();
            RefreshToxicSporeTemplateMask(force: true);
            RefreshCascadeTemplateMask(force: true);
            RefreshDefensiveSporeBurstTemplateMask(force: true);
            ResetInteractionGlobals();
            PublishEnvironmentGlobals(Vector3.zero);
            PublishParasiteInfectionGlobals();
        }

        private void OnEnable()
        {
            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);

            HectonFloatingOrigin.RegisterListener(this);
            PublishFlowFieldGlobals();
            PublishWakeTrailGlobals();
            TryRegister();
            PublishEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : Vector3.zero);
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ClearToxicSporeHazard();
            ClearDefensiveSporeHazard();
            ClearModuleParasiteState();
            ResetInteractionGlobals();
            ClearReactiveFloraSpatialState();
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            ClearToxicSporeHazard();
            ClearDefensiveSporeHazard();
            ClearModuleParasiteState();
            ResetInteractionGlobals();
            ClearReactiveFloraSpatialState();

            if (_oceanFlowSamplePositions.IsCreated)
                _oceanFlowSamplePositions.Dispose();

            if (_oceanFlowSampleResults.IsCreated)
                _oceanFlowSampleResults.Dispose();

            DisposeNativeArray(ref _cascadeReactiveTemplateMask);
            DisposeNativeArray(ref _defensiveSporeBurstTemplateMask);
            DisposeNativeArray(ref _surfaceCascadePhaseSeeds);
            DisposeNativeArray(ref _underwaterCascadePhaseSeeds);
            DisposeNativeArray(ref _surfaceCascadeEvents);
            DisposeNativeArray(ref _underwaterCascadeEvents);
            DisposeNativeList(ref _reactiveFloraQueryHandles);
            DisposeNativeList(ref _surfaceReactiveFloraHandles);
            DisposeNativeList(ref _underwaterReactiveFloraHandles);
            ReleaseGraphicsBuffer(ref _surfaceCascadePhaseSeedBuffer);
            ReleaseGraphicsBuffer(ref _underwaterCascadePhaseSeedBuffer);

            _surfaceReactiveFloraHash?.Dispose();
            _surfaceReactiveFloraHash = null;
            _underwaterReactiveFloraHash?.Dispose();
            _underwaterReactiveFloraHash = null;

            if (_interactionBuffer != null)
            {
                _interactionBuffer.Release();
                _interactionBuffer = null;
            }

            ReleaseFlowFieldBuffer();
            ReleaseWakeTrailResources();
        }

        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!isActiveAndEnabled || shiftData.ShiftOffset.sqrMagnitude <= 0.0001f)
                return;

            ApplyRuntimeOffsetToCachedState(-shiftData.ShiftOffset);
        }

        /// <summary>
        /// Updates published vegetation interaction and environment globals.
        /// </summary>
        /// <param name="deltaTime">Current frame delta.</param>
        public void Tick(float deltaTime)
        {
            RefreshQualityDependentResourcesIfNeeded();
            UpdateSedimentCooldowns(deltaTime);

            Transform runtimePlayerTransform = ResolveRuntimePlayerTransform();
            PublishEnvironmentGlobals(runtimePlayerTransform != null ? runtimePlayerTransform.position : Vector3.zero);
            RefreshFlowFieldGlobals(deltaTime);
            RefreshModuleParasiteState(deltaTime);
            if (runtimePlayerTransform == null)
            {
                ClearToxicSporeHazard();
                ClearDefensiveSporeHazard();
                ResetInteractionGlobals();
                ResetExternalInteractions();
                return;
            }

            ResolvePlayerState(runtimePlayerTransform);
            UpdateToxicSporeExposure(runtimePlayerTransform.position, deltaTime);
            UpdateDefensiveSporeBursts(runtimePlayerTransform.position);
            UpdateBioluminescentCascades(runtimePlayerTransform.position, deltaTime);

            Vector3 targetPosition = runtimePlayerTransform.position;
            Vector3 playerVelocity = UpdatePlayerSpringVelocity(ResolvePlayerVelocity(targetPosition, deltaTime), deltaTime);
            float velocityMagnitude = playerVelocity.magnitude;
            _lastPublishedPlayerVelocity = playerVelocity;
            _hasActiveScooterWake = false;
            _hasActiveSubmarineWake = false;

            float targetRadius = _baseRadius + velocityMagnitude * _velocityRadiusMultiplier;
            float targetForce = Mathf.Clamp(velocityMagnitude * 0.85f, 0f, _maxInteractionForce);

            _smoothPosition = Vector3.Lerp(_smoothPosition, targetPosition, deltaTime * _positionSmoothSpeed);
            _smoothRadius = Mathf.Lerp(_smoothRadius, targetRadius, deltaTime * _intensitySmoothSpeed);
            _smoothForce = Mathf.Lerp(_smoothForce, targetForce, deltaTime * _intensitySmoothSpeed);

            int interactionCount = 0;
            float playerBendRadius = Mathf.Clamp(
                _playerBendRadius + velocityMagnitude * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            PublishPlayerRuntimePosition(targetPosition, playerBendRadius, velocityMagnitude, targetForce);
            interactionCount = AppendInteractionPoint(_smoothPosition, playerVelocity, playerBendRadius, interactionCount);
            interactionCount = AppendScooterInteractionPoint(playerVelocity, interactionCount, deltaTime);
            interactionCount = CollectDynamicInteractionPoints(targetPosition, interactionCount);
            interactionCount = AppendExternalInteractions(interactionCount);
            UpdateWakeTrail(runtimePlayerTransform.position, playerVelocity, deltaTime);
            TryEmitSedimentBursts(targetPosition, playerVelocity);

            Shader.SetGlobalVector(
                _PropWashPosId,
                new Vector4(_smoothPosition.x, _smoothPosition.y, _smoothPosition.z, _smoothRadius));
            Shader.SetGlobalFloat(_PropWashForceId, _smoothForce);

            if (_interactionBuffer != null && interactionCount > 0)
            {
                GraphicsBufferUploadUtility.UploadArray(_interactionBuffer, _interactionPoints, interactionCount);
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);
                Shader.SetGlobalInt(_InteractionCountId, interactionCount);
                _lastPublishedInteractionCount = interactionCount;
                ResetExternalInteractions();
                return;
            }

            Shader.SetGlobalInt(_InteractionCountId, 0);
            _lastPublishedInteractionCount = 0;
            ResetExternalInteractions();
        }

        /// <summary>
        /// Queues one external vegetation interaction burst for publication during the next Tick.
        /// </summary>
        public void RegisterExternalInteraction(Vector3 positionWS, Vector3 velocityWS, float radius)
        {
            if (_externalInteractionPoints == null || _externalInteractionCount >= MaxExternalInteractionPoints)
                return;

            _externalInteractionPoints[_externalInteractionCount++] = new FloraInteractionPointGpuData
            {
                PositionRadius = new Vector4(
                    positionWS.x,
                    positionWS.y,
                    positionWS.z,
                    Mathf.Max(0.05f, radius)),
                VelocitySpeed = new Vector4(
                    velocityWS.x,
                    velocityWS.y,
                    velocityWS.z,
                    velocityWS.magnitude)
            };
        }

        private Transform ResolveRuntimePlayerTransform()
        {
            Transform runtimePlayerTransform = BootstrapState.CurrentPlayerTransform;
            if (runtimePlayerTransform != null)
                return runtimePlayerTransform;

            return _playerTransformOverride;
        }

        private void ResolvePlayerState(Transform runtimePlayerTransform)
        {
            if (_playerTransform == runtimePlayerTransform)
            {
                ResolveScooterState();
                return;
            }

            _playerTransform = runtimePlayerTransform;
            _playerRb = runtimePlayerTransform.GetComponent<Rigidbody>();
            _playerMovement = runtimePlayerTransform.GetComponent<HectonPlayerMovement>();
            _playerToolManager = ResolvePlayerToolManager(runtimePlayerTransform);
            _activeScooterTransform = _scooterTransformOverride;
            _smoothPosition = runtimePlayerTransform.position;
            _lastPlayerPosition = runtimePlayerTransform.position;
            _hasLastPlayerPosition = true;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPosition = _activeScooterTransform != null ? _activeScooterTransform.position : runtimePlayerTransform.position;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = _activeScooterTransform != null;
            ResolveScooterState();
        }

        private void RefreshQualityDependentResourcesIfNeeded()
        {
            int qualityLevel = QualitySettings.GetQualityLevel();
            if (_wakeTrailQualityLevel == qualityLevel)
                return;

            _wakeTrailQualityLevel = qualityLevel;
            int desiredResolution = ResolveWakeTrailResolutionForQuality(qualityLevel);
            if (_wakeTrailRuntimeResolution == desiredResolution)
                return;

            _wakeTrailRuntimeResolution = desiredResolution;
            ReleaseWakeTrailResources();
            CreateWakeTrailResources();
        }

        private int ResolveWakeTrailResolutionForQuality(int qualityLevel)
        {
            string[] qualityNames = QualitySettings.names;
            string qualityName = qualityLevel >= 0 && qualityLevel < qualityNames.Length ? qualityNames[qualityLevel] : string.Empty;
            return 256;
        }

        private Vector3 ResolvePlayerVelocity(Vector3 targetPosition, float deltaTime)
        {
            if (_playerRb != null)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return _playerRb.linearVelocity;
            }

            if (!_hasLastPlayerPosition || deltaTime <= 0.0001f)
            {
                _lastPlayerPosition = targetPosition;
                _hasLastPlayerPosition = true;
                return Vector3.zero;
            }

            if (!TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
            {
                _lastPlayerPosition = targetPosition;
                return Vector3.zero;
            }

            Vector3 velocity = HectonPlayerMotor.SafeVelocity((targetPosition - _lastPlayerPosition) * inverseDeltaTime);
            _lastPlayerPosition = targetPosition;
            return velocity;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || math.abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private int CollectDynamicInteractionPoints(Vector3 targetPosition, int interactionCount)
        {
            int hitCount = global::UnityEngine.Physics.OverlapSphereNonAlloc(
                targetPosition,
                _dynamicInteractionRadius,
                _interactionColliders,
                _dynamicInteractionMask,
                QueryTriggerInteraction.Ignore);

            int uniqueBodyCount = 0;
            for (int i = 0; i < hitCount && interactionCount < _maxInteractionPoints; i++)
            {
                Collider hitCollider = _interactionColliders[i];
                if (hitCollider == null)
                    continue;

                Rigidbody hitBody = hitCollider.attachedRigidbody;
                if (hitBody == null || hitBody == _playerRb || hitBody.transform == _playerTransform)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < uniqueBodyCount; j++)
                {
                    if (_interactionBodies[j] == hitBody)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                    continue;

                if (uniqueBodyCount < _interactionBodies.Length)
                    _interactionBodies[uniqueBodyCount++] = hitBody;

                Vector3 velocity = hitBody.linearVelocity;
                float radius = Mathf.Clamp(
                    _dynamicObjectBaseRadius + velocity.magnitude * _dynamicVelocityRadiusMultiplier,
                    0.5f,
                    _maxBendRadius);
                interactionCount = AppendInteractionPoint(hitBody.worldCenterOfMass, velocity, radius, interactionCount);
            }

            return interactionCount;
        }

        private int AppendScooterInteractionPoint(Vector3 playerVelocity, int interactionCount, float deltaTime)
        {
            ResolveScooterState();

            if (interactionCount >= _maxInteractionPoints)
                return interactionCount;

            bool hasScooterSource = _activeScooterTransform != null;
            Vector3 targetVelocity = hasScooterSource ? playerVelocity * _scooterVelocityMultiplier : Vector3.zero;
            Vector3 smoothedVelocity = SmoothInteractionVector(
                _smoothedScooterVelocity,
                targetVelocity,
                ref _smoothedScooterVelocityDamp,
                deltaTime);
            float speed = smoothedVelocity.magnitude;
            if (speed <= _interactionReleaseSpeed)
                return interactionCount;

            Vector3 targetScooterPosition = _smoothedScooterPosition;
            if (hasScooterSource)
            {
                targetScooterPosition = _activeScooterTransform.position;
                if (_scooterForwardOffset > 0.0001f)
                    targetScooterPosition += _activeScooterTransform.forward * _scooterForwardOffset;
            }

            if (!_hasSmoothedScooterPosition)
            {
                _smoothedScooterPosition = targetScooterPosition;
                _hasSmoothedScooterPosition = true;
            }

            _smoothedScooterPosition = Vector3.SmoothDamp(
                _smoothedScooterPosition,
                targetScooterPosition,
                ref _smoothedScooterPositionDamp,
                hasScooterSource ? _interactionRiseSmoothTime : _interactionRecoverySmoothTime,
                Mathf.Infinity,
                deltaTime);

            float radius = Mathf.Clamp(
                _scooterBendRadius + speed * _dynamicVelocityRadiusMultiplier,
                0.5f,
                _maxBendRadius);
            _hasActiveScooterWake = true;
            _lastPublishedScooterWakePosition = _smoothedScooterPosition;

            return AppendInteractionPoint(_smoothedScooterPosition, smoothedVelocity, radius, interactionCount);
        }

        private Vector3 UpdatePlayerSpringVelocity(Vector3 targetVelocity, float deltaTime)
        {
            _smoothedPlayerVelocity = SmoothInteractionVector(
                _smoothedPlayerVelocity,
                targetVelocity,
                ref _smoothedPlayerVelocityDamp,
                deltaTime);
            return _smoothedPlayerVelocity;
        }

        private Vector3 SmoothInteractionVector(
            Vector3 currentVelocity,
            Vector3 targetVelocity,
            ref Vector3 smoothVelocity,
            float deltaTime)
        {
            if (deltaTime <= 0.0001f)
                return currentVelocity;

            float smoothTime = targetVelocity.sqrMagnitude > 0.0001f
                ? _interactionRiseSmoothTime
                : _interactionRecoverySmoothTime;

            return Vector3.SmoothDamp(
                currentVelocity,
                targetVelocity,
                ref smoothVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        private PlayerToolManager ResolvePlayerToolManager(Transform runtimePlayerTransform)
        {
            if (runtimePlayerTransform == null)
                return null;

            if (runtimePlayerTransform.TryGetComponent(out PlayerToolManager directToolManager))
                return directToolManager;

            return ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.ToolManager != null) ? Hecton8.Core.GlobalRegistry.Player.ToolManager : runtimePlayerTransform.GetComponent<PlayerToolManager>());
        }

        private void ResolveScooterState()
        {
            _activeScooterTransform = _scooterTransformOverride;

            if (_playerTransform == null)
                return;

            if (_playerToolManager == null)
                _playerToolManager = ResolvePlayerToolManager(_playerTransform);

            if (_playerToolManager == null || _playerToolManager.IsSwapping)
                return;

            if (!(_playerToolManager.CurrentTool is MantaScooter scooter) || !scooter.IsTransportActive)
                return;

            if (_activeScooterTransform == null)
                _activeScooterTransform = scooter.transform;
        }

        private HectonMapMagicVegetationBridge ResolveVegetationBridge()
        {
            if (_vegetationBridgeOverride != null)
                return _vegetationBridgeOverride;

            HectonMapMagicVegetationBridge directBridge = GetComponent<HectonMapMagicVegetationBridge>();
            if (directBridge != null)
                return directBridge;

            HectonMapMagicVegetationBridge childBridge = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<HectonMapMagicVegetationBridge>(transform);
            if (childBridge != null)
                return childBridge;

            return GetComponentInParent<HectonMapMagicVegetationBridge>();
        }

        private void EnsureSedimentParticleSystem()
        {
            if (_sedimentBurstParticleSystem != null)
                return;

            GameObject sedimentObject = new GameObject("__VegetationSedimentBursts");
            sedimentObject.hideFlags = HideFlags.HideAndDontSave;
            sedimentObject.transform.SetParent(transform, false);
            _sedimentBurstParticleSystem = sedimentObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = _sedimentBurstParticleSystem.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 256;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 1.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.58f, 0.64f, 0.56f, 0.34f));
            main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.02f);

            ParticleSystem.EmissionModule emission = _sedimentBurstParticleSystem.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _sedimentBurstParticleSystem.shape;
            shape.enabled = false;

            ParticleSystem.NoiseModule noise = _sedimentBurstParticleSystem.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
            noise.frequency = 0.28f;

            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = _sedimentBurstParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);

            ParticleSystemRenderer renderer = _sedimentBurstParticleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
        }

        private void UpdateSedimentCooldowns(float deltaTime)
        {
            if (_playerSedimentCooldownRemaining > 0f)
            {
                _playerSedimentCooldownRemaining -= deltaTime;
                if (_playerSedimentCooldownRemaining < 0f)
                    _playerSedimentCooldownRemaining = 0f;
            }

            if (_scooterSedimentCooldownRemaining > 0f)
            {
                _scooterSedimentCooldownRemaining -= deltaTime;
                if (_scooterSedimentCooldownRemaining < 0f)
                    _scooterSedimentCooldownRemaining = 0f;
            }
        }

        private void TryEmitSedimentBursts(Vector3 playerPosition, Vector3 playerVelocity)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            float playerSpeed = playerVelocity.magnitude;
            if (_playerSedimentCooldownRemaining <= 0f && playerSpeed >= _playerSedimentMinSpeed && IsInsideDenseGrassZone(playerPosition))
            {
                EmitSedimentBurst(playerPosition, playerVelocity, false);
                _playerSedimentCooldownRemaining = _playerSedimentCooldown;
            }

            float scooterSpeed = _smoothedScooterVelocity.magnitude;
            if (_hasActiveScooterWake &&
                _scooterSedimentCooldownRemaining <= 0f &&
                scooterSpeed >= _scooterSedimentMinSpeed &&
                IsInsideDenseGrassZone(_lastPublishedScooterWakePosition))
            {
                EmitSedimentBurst(_lastPublishedScooterWakePosition, _smoothedScooterVelocity, true);
                _scooterSedimentCooldownRemaining = _scooterSedimentCooldown;
            }
        }

        private bool IsInsideDenseGrassZone(Vector3 positionWS)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            if (_vegetationBridge == null || _vegetationBridge.ActiveSurfaceInstanceCount < _denseGrassInstanceThreshold)
                return false;

            Bounds surfaceBounds = _vegetationBridge.ActiveSurfaceDrawBounds;
            if (surfaceBounds.size.sqrMagnitude <= 0.0001f || !surfaceBounds.Contains(positionWS))
                return false;

            float waterLevel = HectonFluidEngine.Instance != null ? HectonFluidEngine.Instance.WaterLevel : DefaultVegetationWaterLevel;
            return positionWS.y <= waterLevel - 0.25f;
        }

        private void EmitSedimentBurst(Vector3 positionWS, Vector3 velocityWS, bool scooterBurst)
        {
            if (_sedimentBurstParticleSystem == null)
                return;

            if (!_sedimentBurstParticleSystem.isPlaying)
                _sedimentBurstParticleSystem.Play(true);

            float speed = velocityWS.magnitude;
            float burstRadiusScale = Mathf.Clamp(_sedimentBurstRadius * 0.5f, 0.4f, 2f);
            int burstCount = Mathf.Clamp(Mathf.RoundToInt(speed * (scooterBurst ? 0.9f : 0.55f)), 2, _sedimentMaxBurstCount);
            Vector3 planarVelocity = new Vector3(velocityWS.x, 0f, velocityWS.z);
            if (planarVelocity.sqrMagnitude <= 0.0001f)
                planarVelocity = Vector3.forward;
            planarVelocity.Normalize();

            _sedimentEmitParams.position = positionWS + Vector3.down * 0.18f;
            _sedimentEmitParams.velocity = planarVelocity * Mathf.Min(speed * (0.16f + _sedimentBurstRadius * 0.015f), 3.2f) + Vector3.up * (scooterBurst ? 0.38f : 0.22f);
            _sedimentEmitParams.startSize = Mathf.Lerp(0.08f, 0.24f, Mathf.InverseLerp(_playerSedimentMinSpeed, _scooterSedimentMinSpeed * 2f, speed)) * burstRadiusScale;
            _sedimentEmitParams.startLifetime = Mathf.Lerp(1.0f, 2.0f, Mathf.InverseLerp(_playerSedimentMinSpeed, _scooterSedimentMinSpeed * 2f, speed));
            _sedimentEmitParams.startColor = scooterBurst
                ? new Color(0.62f, 0.7f, 0.62f, 0.36f)
                : new Color(0.55f, 0.6f, 0.54f, 0.28f);
            _sedimentBurstParticleSystem.Emit(_sedimentEmitParams, burstCount);
        }

        private int AppendInteractionPoint(Vector3 position, Vector3 velocity, float radius, int interactionCount)
        {
            if (_interactionPoints == null || _maxInteractionPoints <= 0)
                return 0;

            if (interactionCount < 0)
                interactionCount = 0;

            int interactionCapacity = Mathf.Min(_maxInteractionPoints, _interactionPoints.Length);
            if (interactionCount >= interactionCapacity)
                return interactionCount;

            _interactionPoints[interactionCount] = new FloraInteractionPointGpuData
            {
                PositionRadius = new Vector4(
                    position.x,
                    position.y,
                    position.z,
                    Mathf.Max(0.05f, radius)),
                VelocitySpeed = new Vector4(
                    velocity.x,
                    velocity.y,
                    velocity.z,
                    velocity.magnitude)
            };
            return interactionCount + 1;
        }

        private int AppendExternalInteractions(int interactionCount)
        {
            if (_externalInteractionPoints == null || _externalInteractionCount <= 0)
                return interactionCount;

            int copyCount = Mathf.Min(_externalInteractionCount, _externalInteractionPoints.Length);
            for (int i = 0; i < copyCount && interactionCount < _maxInteractionPoints; i++)
            {
                _interactionPoints[interactionCount] = _externalInteractionPoints[i];
                interactionCount++;
            }

            return interactionCount;
        }

        private void ResetExternalInteractions()
        {
            _externalInteractionCount = 0;
        }

        private void PublishEnvironmentGlobals(Vector3 samplePositionWS)
        {
            RefreshToxicSporeTemplateMask(force: false);
            RefreshCascadeTemplateMask(force: false);
            RefreshDefensiveSporeBurstTemplateMask(force: false);
            HectonUnderwaterVisuals underwaterVisuals = HectonUnderwaterVisuals.ActiveRuntimeInstance;
            float depth = underwaterVisuals != null ? underwaterVisuals.CurrentDepth : 0f;
            float lightFactor = underwaterVisuals != null ? underwaterVisuals.CurrentLightFactor : 1f;
            float turbidity = underwaterVisuals != null ? underwaterVisuals.CurrentTurbidity : 0f;

            HectonFluidEngine fluidEngine = HectonFluidEngine.Instance;
            float waterLevel = fluidEngine != null ? fluidEngine.WaterLevel : DefaultVegetationWaterLevel;
            Vector3 currentVector = ResolveGlobalOceanFlow(samplePositionWS, fluidEngine);
            float currentStrength = currentVector.magnitude;
            float currentNoiseScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentNoiseScale : 0f;
            float currentTimeScale = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentTimeScale : 0f;
            float currentVerticalFactor = fluidEngine != null && fluidEngine.EnablePhantomCurrent ? fluidEngine.CurrentVerticalFactor : 0f;

            Shader.SetGlobalColor(_VegetationFogColorId, RenderSettings.fogColor);
            Shader.SetGlobalColor(_VegetationAmbientColorId, ResolveAmbientColor());
            Shader.SetGlobalFloat(_VegetationDepthId, depth);
            Shader.SetGlobalFloat(_VegetationLightFactorId, lightFactor);
            Shader.SetGlobalFloat(_VegetationTurbidityId, turbidity);
            Shader.SetGlobalFloat(_VegetationWaterLevelId, waterLevel);
            Shader.SetGlobalVector(_GlobalOceanFlowId, new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalVector(
                _VegetationCurrentVectorId,
                new Vector4(currentVector.x, currentVector.y, currentVector.z, 0f));
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, currentStrength);
            Shader.SetGlobalFloat(_VegetationCurrentNoiseScaleId, currentNoiseScale);
            Shader.SetGlobalFloat(_VegetationCurrentTimeScaleId, currentTimeScale);
            Shader.SetGlobalFloat(_VegetationCurrentVerticalFactorId, currentVerticalFactor);
            PublishLifecycleGlobals();
            PublishCascadeGlobals();
            PublishPredatorThreatGlobals(samplePositionWS);
        }

        private void PublishLifecycleGlobals()
        {
            float cycleSeconds = Mathf.Max(120f, _seasonCycleSeconds);
            float simulationTime = GetCurrentSimulationTimeSeconds();
            float cyclePhase = Mathf.Repeat(simulationTime / cycleSeconds, 1f);
            int encounterPhaseIndex = ResolveEncounterPhaseIndex();

            float bloomWeight = ResolveLifecycleWindowWeight(cyclePhase, _bloomPhaseCenterNormalized, _bloomPhaseWidthNormalized);
            float decayWeight = ResolveLifecycleWindowWeight(cyclePhase, _decayPhaseCenterNormalized, _decayPhaseWidthNormalized);

            if (encounterPhaseIndex == 1)
                bloomWeight = Mathf.Clamp01(bloomWeight + (_encounterBloomBias * 0.55f));
            else if (encounterPhaseIndex == 2)
                bloomWeight = Mathf.Clamp01(bloomWeight + _encounterBloomBias);

            if (encounterPhaseIndex == 3)
            {
                decayWeight = Mathf.Clamp01(decayWeight + _encounterDecayBias);
                bloomWeight = Mathf.Clamp01(bloomWeight * 0.7f);
            }

            Shader.SetGlobalVector(
                _FloraLifecycleParamsId,
                new Vector4(
                    bloomWeight,
                    decayWeight,
                    Mathf.Lerp(1f, _bloomEmissionBoost, bloomWeight),
                    _decayWiltStrength));
        }

        private void PublishCascadeGlobals()
        {
            Shader.SetGlobalVector(
                _FloraCascadeParamsId,
                new Vector4(
                    GetCurrentSimulationTimeSeconds(),
                    _cascadePulseDurationSeconds,
                    _cascadeEmissionBoost,
                    _cascadeReleaseDurationSeconds));
        }

        private static float GetCurrentSimulationTimeSeconds()
        {
            return GlobalRegistry.Save != null
                ? Mathf.Max(0f, GlobalRegistry.Save.CurrentPlayTimeSeconds)
                : Time.realtimeSinceStartup;
        }

        private static int ResolveEncounterPhaseIndex()
        {
            HectonDirectorAI director = HectonDirectorAI.ActiveRuntimeInstance;
            return director != null ? director.CurrentPhaseIndex : -1;
        }

        private static float ResolveLifecycleWindowWeight(float cyclePhase, float centerNormalized, float halfWidthNormalized)
        {
            float wrappedDelta = Mathf.Abs(Mathf.DeltaAngle(cyclePhase * 360f, Mathf.Repeat(centerNormalized, 1f) * 360f)) / 360f;
            float normalized = Mathf.Clamp01(1f - (wrappedDelta / Mathf.Max(0.001f, halfWidthNormalized)));
            return normalized * normalized * (3f - (2f * normalized));
        }

        private void UpdateToxicSporeExposure(Vector3 playerPositionWS, float deltaTime)
        {
            if (_playerMovement == null || _toxicSporeStableHashId == 0)
            {
                ClearToxicSporeHazard();
                return;
            }

            if (_lastToxicSporeExposure01 > 0f)
                _playerMovement.ApplyEnvironmentalDrag(Mathf.Lerp(1f, _toxicSporeDragMultiplier, _lastToxicSporeExposure01));

            _toxicSporeScanTimer -= deltaTime;
            if (_toxicSporeScanTimer > 0f)
                return;

            _toxicSporeScanTimer = _toxicSporeScanIntervalSeconds;

            float exposure01 = 0f;
            Vector3 hazardPosition = Vector3.zero;
            TryResolveNearestToxicSporeEmitter(
                playerPositionWS,
                Mathf.Max(1f, _toxicSporeDetectionRadius),
                ref exposure01,
                ref hazardPosition,
                underwater: true);

            if (exposure01 <= 0f)
            {
                TryResolveNearestToxicSporeEmitter(
                    playerPositionWS,
                    Mathf.Max(1f, _toxicSporeDetectionRadius),
                    ref exposure01,
                    ref hazardPosition,
                    underwater: false);
            }

            if (exposure01 <= 0f)
            {
                ClearToxicSporeHazard();
                return;
            }

            _lastToxicSporeExposure01 = exposure01;
            HectonHazardManager.Register(
                ToxicSporeHazardSourceId,
                hazardPosition,
                _toxicSporeHazardIntensity * exposure01,
                _toxicSporeHazardRadius,
                HazardType.Toxicity,
                _toxicSporeVisorGlitchBias);
        }

        private void TryResolveNearestToxicSporeEmitter(
            Vector3 playerPositionWS,
            float detectionRadius,
            ref float bestExposure01,
            ref Vector3 bestPositionWS,
            bool underwater)
        {
            if (_vegetationBridge == null || _toxicSporeTemplateMask == null || _toxicSporeTemplateMask.Length == 0)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            float detectionRadiusSq = detectionRadius * detectionRadius;
            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    instanceData.HeightScale <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
                if (templateIndex < 0 || templateIndex >= _toxicSporeTemplateMask.Length || !_toxicSporeTemplateMask[templateIndex])
                    continue;

                Vector3 instancePositionWS = ExtractTranslation(matrices[i]);
                float distanceSq = (instancePositionWS - playerPositionWS).sqrMagnitude;
                if (distanceSq > detectionRadiusSq)
                    continue;

                float exposure01 = 1f - Mathf.Clamp01(Mathf.Sqrt(distanceSq) / detectionRadius);
                if (exposure01 <= bestExposure01)
                    continue;

                bestExposure01 = exposure01;
                bestPositionWS = instancePositionWS;
            }
        }

        private void RefreshToxicSporeTemplateMask(bool force)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (!force &&
                _cachedToxicSporeTemplateCount == templateCount &&
                ((_vegetationBridge == null && templateCount == 0) || templateCount == _toxicSporeTemplateMask.Length))
            {
                return;
            }

            _cachedToxicSporeTemplateCount = templateCount;
            if (templateCount <= 0 || _toxicSporeStableHashId == 0)
            {
                _toxicSporeTemplateMask = Array.Empty<bool>();
                return;
            }

            if (_toxicSporeTemplateMask == null || _toxicSporeTemplateMask.Length != templateCount)
            {
                _toxicSporeTemplateMask = new bool[templateCount]; // COLD ALLOC: bool[floraTemplates.Length] - toxic spore template membership cache - owner: FloraInteractionManager
            }
            else
            {
                Array.Clear(_toxicSporeTemplateMask, 0, _toxicSporeTemplateMask.Length);
            }

            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                    continue;

                _toxicSporeTemplateMask[i] = LocHash.Compute(template.StableId) == _toxicSporeStableHashId;
            }
        }

        private void ClearToxicSporeHazard()
        {
            _lastToxicSporeExposure01 = 0f;
            HectonHazardManager.Unregister(ToxicSporeHazardSourceId);
        }

        private void RefreshModuleParasiteState(float deltaTime)
        {
            _moduleParasiteScanTimer -= deltaTime;
            if (_moduleParasiteScanTimer > 0f)
                return;

            float scanDeltaTime = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds - _moduleParasiteScanTimer);
            _moduleParasiteScanTimer = Mathf.Max(0.1f, _moduleParasiteScanIntervalSeconds);
            _moduleParasiteStateBack.Clear();
            _publishedParasiteAnchorCount = 0;

            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            ScanActiveModuleParasites(underwater: true);
            ScanActiveModuleParasites(underwater: false);
            EvaluateThermophilicModuleGrowth(scanDeltaTime);
            ApplyModuleParasiteStateDiff();
            PublishParasiteInfectionGlobals();
        }

        private void ScanActiveModuleParasites(bool underwater)
        {
            if (_vegetationBridge == null)
                return;

            FloraDataTemplate[] floraTemplates = _vegetationBridge.FloraTemplates;
            if (floraTemplates == null || floraTemplates.Length == 0)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            for (int i = 0; i < safeCount; i++)
            {
                HectonVegetationInstanceData instanceData = metadata[i];
                if (instanceData.RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    instanceData.HeightScale <= 0.0001f)
                {
                    continue;
                }

                int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
                if (templateIndex < 0 || templateIndex >= floraTemplates.Length)
                    continue;

                FloraDataTemplate template = floraTemplates[templateIndex];
                if (template == null ||
                    !template.IsParasiticToModules ||
                    template.AttachmentSurfaceType != FloraDataTemplate.AttachmentSurface.Metal)
                {
                    continue;
                }

                Vector3 instancePositionWS = ExtractTranslation(matrices[i]);
                if (!TryResolveNearestBaseModule(instancePositionWS, _moduleParasiteAttachmentRadius, out BaseModule module))
                    continue;

                AccumulateModuleParasiteState(module, template.ModulePowerDrainWatts, template.ModuleInfectionStrength);
                AppendParasiteAnchor(
                    instancePositionWS,
                    template.ModuleInfectionRadiusMeters,
                    template.ModuleInfectionStrength,
                    template.ModuleInfectionPulseFrequency,
                    0f);
            }
        }

        private void EvaluateThermophilicModuleGrowth(float scanDeltaTime)
        {
            IReadOnlyList<BaseModule> activeModules = BaseModule.ActiveModules;
            if (activeModules == null || activeModules.Count == 0)
            {
                _thermophileDwellSeconds.Clear();
                return;
            }

            FloraDataTemplate template = ResolveThermalTubewormTemplate();
            if (template == null || !template.IsThermophilicModuleGrowth)
            {
                _thermophileDwellSeconds.Clear();
                return;
            }

            _staleParasiticModules.Clear();
            Dictionary<BaseModule, float>.Enumerator dwellEnumerator = _thermophileDwellSeconds.GetEnumerator();
            while (dwellEnumerator.MoveNext())
            {
                BaseModule module = dwellEnumerator.Current.Key;
                if (module == null || !module.isActiveAndEnabled)
                    _staleParasiticModules.Add(module);
            }

            for (int i = 0; i < _staleParasiticModules.Count; i++)
                _thermophileDwellSeconds.Remove(_staleParasiticModules[i]);

            float validationRadiusSq = _thermophileReactorValidationRadius * _thermophileReactorValidationRadius;
            float thresholdTemperature = template.ThermalActivationTemperatureCelsius;
            float dwellThresholdSeconds = template.ThermalActivationDwellSeconds;
            for (int i = 0; i < activeModules.Count; i++)
            {
                BaseModule module = activeModules[i];
                if (module == null || !module.isActiveAndEnabled || !module.TryGetHostedBioReactor(out BioReactor reactor) || reactor == null)
                {
                    _thermophileDwellSeconds.Remove(module);
                    continue;
                }

                if (reactor.PowerRating <= 0.0001f ||
                    (reactor.transform.position - module.transform.position).sqrMagnitude > validationRadiusSq)
                {
                    _thermophileDwellSeconds.Remove(module);
                    continue;
                }

                float roomTemperature = module.ResolveHostRoomTemperatureCelsius();
                if (roomTemperature < thresholdTemperature)
                {
                    _thermophileDwellSeconds.Remove(module);
                    continue;
                }

                float nextDwellSeconds = scanDeltaTime;
                if (_thermophileDwellSeconds.TryGetValue(module, out float accumulatedDwellSeconds))
                    nextDwellSeconds += accumulatedDwellSeconds;

                _thermophileDwellSeconds[module] = nextDwellSeconds;
                if (nextDwellSeconds < dwellThresholdSeconds)
                    continue;

                AccumulateModuleParasiteState(module, template.ModulePowerDrainWatts, template.ModuleInfectionStrength);
                AppendParasiteAnchor(
                    module.ResolveBotanyAnchorWorldPosition(),
                    template.ModuleInfectionRadiusMeters,
                    template.ModuleInfectionStrength,
                    template.ModuleInfectionPulseFrequency,
                    1f);
            }
        }

        private FloraDataTemplate ResolveThermalTubewormTemplate()
        {
            if (_vegetationBridge == null)
                return null;

            FloraDataTemplate[] floraTemplates = _vegetationBridge.FloraTemplates;
            if (floraTemplates == null)
                return null;

            for (int i = 0; i < floraTemplates.Length; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || !template.IsThermophilicModuleGrowth)
                    continue;

                if (_thermalTubewormStableHashId == 0 || LocHash.Compute(template.StableId) == _thermalTubewormStableHashId)
                    return template;
            }

            return null;
        }

        private void AccumulateModuleParasiteState(BaseModule module, float drainWatts, float infectionLevel)
        {
            if (module == null)
                return;

            if (_moduleParasiteStateBack.TryGetValue(module, out ModuleParasiteState state))
            {
                state.PowerDrainWatts += Mathf.Max(0f, drainWatts);
                state.InfectionLevel = Mathf.Clamp01(Mathf.Max(state.InfectionLevel, infectionLevel));
                _moduleParasiteStateBack[module] = state;
                return;
            }

            _moduleParasiteStateBack[module] = new ModuleParasiteState
            {
                PowerDrainWatts = Mathf.Max(0f, drainWatts),
                InfectionLevel = Mathf.Clamp01(infectionLevel)
            };
        }

        private void AppendParasiteAnchor(Vector3 positionWS, float radiusMeters, float infectionLevel, float pulseFrequency, float thermalGrowthFlag)
        {
            if (_publishedParasiteAnchorCount >= MaxParasiteAnchors)
                return;

            _parasiteAnchorData[_publishedParasiteAnchorCount] = new Vector4(
                positionWS.x,
                positionWS.y,
                positionWS.z,
                Mathf.Max(0.25f, radiusMeters));
            _parasiteAnchorParams[_publishedParasiteAnchorCount] = new Vector4(
                Mathf.Clamp01(infectionLevel),
                Mathf.Max(0.05f, pulseFrequency),
                thermalGrowthFlag,
                0f);
            _publishedParasiteAnchorCount++;
        }

        private bool TryResolveNearestBaseModule(Vector3 origin, float radius, out BaseModule module)
        {
            module = null;
            if (_moduleQueryHits == null || radius <= 0.0001f)
                return false;

            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(origin, radius, SpatialTargetKind.Module, _moduleQueryHits);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                Transform hitTransform = _moduleQueryHits[i].Transform;
                if (hitTransform == null)
                    continue;

                BaseModule candidate = hitTransform.GetComponentInParent<BaseModule>();
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                float distanceSq = (candidate.transform.position - origin).sqrMagnitude;
                if (distanceSq >= bestDistanceSq)
                    continue;

                bestDistanceSq = distanceSq;
                module = candidate;
            }

            return module != null;
        }

        private void ApplyModuleParasiteStateDiff()
        {
            _staleParasiticModules.Clear();
            Dictionary<BaseModule, ModuleParasiteState>.Enumerator previousEnumerator = _moduleParasiteStateFront.GetEnumerator();
            while (previousEnumerator.MoveNext())
            {
                BaseModule module = previousEnumerator.Current.Key;
                if (module == null || !_moduleParasiteStateBack.ContainsKey(module))
                    _staleParasiticModules.Add(module);
            }

            for (int i = 0; i < _staleParasiticModules.Count; i++)
            {
                BaseModule staleModule = _staleParasiticModules[i];
                if (staleModule != null)
                    staleModule.SetParasiteInfestation(0f, 0f);
            }

            Dictionary<BaseModule, ModuleParasiteState>.Enumerator nextEnumerator = _moduleParasiteStateBack.GetEnumerator();
            while (nextEnumerator.MoveNext())
            {
                BaseModule module = nextEnumerator.Current.Key;
                if (module == null)
                    continue;

                ModuleParasiteState nextState = nextEnumerator.Current.Value;
                if (!_moduleParasiteStateFront.TryGetValue(module, out ModuleParasiteState previousState) ||
                    !AreEquivalentParasiteStates(in previousState, in nextState))
                {
                    module.SetParasiteInfestation(nextState.PowerDrainWatts, nextState.InfectionLevel);
                }
            }

            Dictionary<BaseModule, ModuleParasiteState> swap = _moduleParasiteStateFront;
            _moduleParasiteStateFront = _moduleParasiteStateBack;
            _moduleParasiteStateBack = swap;
            _moduleParasiteStateBack.Clear();
        }

        private static bool AreEquivalentParasiteStates(in ModuleParasiteState lhs, in ModuleParasiteState rhs)
        {
            return Mathf.Abs(lhs.PowerDrainWatts - rhs.PowerDrainWatts) <= 0.01f &&
                   Mathf.Abs(lhs.InfectionLevel - rhs.InfectionLevel) <= 0.001f;
        }

        private void ClearModuleParasiteState()
        {
            Dictionary<BaseModule, ModuleParasiteState>.Enumerator frontEnumerator = _moduleParasiteStateFront.GetEnumerator();
            while (frontEnumerator.MoveNext())
            {
                BaseModule module = frontEnumerator.Current.Key;
                if (module != null)
                    module.SetParasiteInfestation(0f, 0f);
            }

            _moduleParasiteStateFront.Clear();
            _moduleParasiteStateBack.Clear();
            _thermophileDwellSeconds.Clear();
            _publishedParasiteAnchorCount = 0;
            PublishParasiteInfectionGlobals();
        }

        private void PublishParasiteInfectionGlobals()
        {
            Shader.SetGlobalVectorArray(_ParasiteAnchorDataId, _parasiteAnchorData);
            Shader.SetGlobalVectorArray(_ParasiteAnchorParamsId, _parasiteAnchorParams);
            Shader.SetGlobalVector(
                _ParasiteGlobalsId,
                new Vector4(
                    _publishedParasiteAnchorCount,
                    Time.time,
                    0.35f,
                    1f));
        }

        public void RegisterDefensiveSporeBurst(Vector3 positionWS, float intensity01)
        {
            float simulationTime = GetCurrentSimulationTimeSeconds();
            float clampedIntensity = Mathf.Max(0.1f, intensity01);
            ChemicalInfluenceGrid.QueueToxicityBurst(positionWS, _defensiveSporeBurstDose * clampedIntensity);
            ChemicalInfluenceGrid.QueueFearPheromone(positionWS, Mathf.Clamp01(clampedIntensity));

            if (_defensiveSporeBursts == null || _defensiveSporeBursts.Length == 0)
                return;

            int writeIndex = _defensiveSporeBurstCount < _defensiveSporeBursts.Length
                ? _defensiveSporeBurstCount
                : FindWeakestDefensiveSporeBurstIndex(simulationTime);

            _defensiveSporeBursts[writeIndex] = new DefensiveSporeBurstState
            {
                PositionWS = positionWS,
                Radius = _defensiveSporeBurstRadius,
                Intensity = clampedIntensity,
                ExpireTimeSeconds = simulationTime + _defensiveSporeBurstLifetimeSeconds
            };
            _defensiveSporeBurstCount = Mathf.Min(_defensiveSporeBursts.Length, Mathf.Max(_defensiveSporeBurstCount, writeIndex + 1));
        }

        private void UpdateDefensiveSporeBursts(Vector3 playerPositionWS)
        {
            if (_defensiveSporeBursts == null || _defensiveSporeBurstCount <= 0)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            float simulationTime = GetCurrentSimulationTimeSeconds();
            CompactDefensiveSporeBursts(simulationTime);
            if (_defensiveSporeBurstCount <= 0)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            float strongestExposure = 0f;
            Vector3 strongestPosition = Vector3.zero;
            for (int i = 0; i < _defensiveSporeBurstCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                float distance = Vector3.Distance(playerPositionWS, burst.PositionWS);
                if (distance > burst.Radius)
                    continue;

                float exposure01 = (1f - Mathf.Clamp01(distance / Mathf.Max(0.001f, burst.Radius))) * burst.Intensity;
                if (exposure01 <= strongestExposure)
                    continue;

                strongestExposure = exposure01;
                strongestPosition = burst.PositionWS;
            }

            if (strongestExposure <= 0f)
            {
                ClearDefensiveSporeHazard();
                return;
            }

            HectonHazardManager.Register(
                DefensiveSporeHazardSourceId,
                strongestPosition,
                _defensiveSporeHazardIntensity * strongestExposure,
                _defensiveSporeBurstRadius,
                HazardType.Toxicity,
                _defensiveSporeVisorGlitchBias * strongestExposure);
        }

        private void ClearDefensiveSporeHazard()
        {
            HectonHazardManager.Unregister(DefensiveSporeHazardSourceId);
        }

        private void CompactDefensiveSporeBursts(float simulationTime)
        {
            if (_defensiveSporeBursts == null)
            {
                _defensiveSporeBurstCount = 0;
                return;
            }

            int writeIndex = 0;
            int safeCount = Mathf.Min(_defensiveSporeBurstCount, _defensiveSporeBursts.Length);
            for (int i = 0; i < safeCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                if (simulationTime >= burst.ExpireTimeSeconds)
                    continue;

                _defensiveSporeBursts[writeIndex] = burst;
                writeIndex++;
            }

            _defensiveSporeBurstCount = writeIndex;
        }

        private int FindWeakestDefensiveSporeBurstIndex(float simulationTime)
        {
            if (_defensiveSporeBursts == null || _defensiveSporeBursts.Length == 0)
                return 0;

            int bestIndex = 0;
            float weakestScore = float.MaxValue;
            int safeCount = Mathf.Min(_defensiveSporeBurstCount, _defensiveSporeBursts.Length);
            for (int i = 0; i < safeCount; i++)
            {
                DefensiveSporeBurstState burst = _defensiveSporeBursts[i];
                float remainingLifetime = Mathf.Max(0f, burst.ExpireTimeSeconds - simulationTime);
                float score = remainingLifetime * Mathf.Max(0.01f, burst.Intensity);
                if (score >= weakestScore)
                    continue;

                weakestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private void UpdateBioluminescentCascades(Vector3 playerPositionWS, float deltaTime)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            if (_vegetationBridge == null || !_cascadeReactiveTemplateMask.IsCreated || _cascadeReactiveTemplateMask.Length == 0)
                return;

            _cascadeSpatialRefreshTimer -= deltaTime;
            if (_cascadeSpatialRefreshTimer <= 0f)
            {
                RefreshReactiveFloraSpatialHashes();
                _cascadeSpatialRefreshTimer = _cascadeSpatialRefreshIntervalSeconds;
            }

            TryTriggerCascadeInLane(playerPositionWS, underwater: true);
            TryTriggerCascadeInLane(playerPositionWS, underwater: false);
        }

        private void RefreshReactiveFloraSpatialHashes()
        {
            RebuildReactiveFloraSpatialHash(underwater: false);
            RebuildReactiveFloraSpatialHash(underwater: true);
        }

        private void RebuildReactiveFloraSpatialHash(bool underwater)
        {
            if (_vegetationBridge == null)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);

            HectonSpatialHash spatialHash = underwater ? _underwaterReactiveFloraHash : _surfaceReactiveFloraHash;
            NativeList<int> registeredHandles = underwater ? _underwaterReactiveFloraHandles : _surfaceReactiveFloraHandles;
            int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;

            EnsureReactiveFloraHashCapacity(ref spatialHash, count);
            ClearRegisteredReactiveFloraHandles(spatialHash, registeredHandles);

            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
            {
                ReleaseCascadePhaseSeedChannel(underwater);
                if (underwater)
                    _underwaterReactiveFloraHash = spatialHash;
                else
                    _surfaceReactiveFloraHash = spatialHash;
                return;
            }

            int safeCount = math.min(count, math.min(matrices.Length, metadata.Length));
            for (int i = 0; i < safeCount; i++)
            {
                if (!IsReactiveCascadeTemplate(metadata[i]))
                    continue;

                if (metadata[i].RuntimeState >= HectonVegetationInstanceData.RuntimeStateDying - 0.01f ||
                    math.abs(metadata[i].HeightScale) <= 0.0001f)
                {
                    continue;
                }

                Vector3 positionWS = ExtractTranslation(matrices[i]);
                int handle = spatialHash.Register(
                    AbsoluteUniversePosition.FromRuntimePosition(positionWS),
                    ResolveReactiveFloraHalfExtents(metadata[i], types.IsCreated && i < types.Length ? types[i] : 0),
                    ReactiveFloraKindMask,
                    i);
                registeredHandles.Add(handle);
            }

            if (underwater)
                _underwaterReactiveFloraHash = spatialHash;
            else
                _surfaceReactiveFloraHash = spatialHash;

            EnsureCascadePhaseSeedResources(underwater, safeCount);
            if (eventCount > 0)
            {
                RecomputeCascadePhaseSeeds(underwater, matrices, metadata, safeCount);
            }
            else
            {
                ClearCascadePhaseSeeds(underwater, safeCount);
                UploadCascadePhaseSeedBuffer(underwater, safeCount);
            }
        }

        private void TryTriggerCascadeInLane(Vector3 playerPositionWS, bool underwater)
        {
            if (_reactiveFloraQueryHandles.IsCreated)
                _reactiveFloraQueryHandles.Clear();

            HectonSpatialHash spatialHash = underwater ? _underwaterReactiveFloraHash : _surfaceReactiveFloraHash;
            if (spatialHash == null || !_reactiveFloraQueryHandles.IsCreated)
                return;

            NativeArray<Matrix4x4> matrices;
            NativeArray<HectonVegetationInstanceData> metadata;
            NativeArray<int> types;
            int count;
            bool hasPayload = underwater
                ? _vegetationBridge.TryGetActiveUnderwaterNativePayload(out matrices, out metadata, out types, out count)
                : _vegetationBridge.TryGetActiveSurfaceNativePayload(out matrices, out metadata, out types, out count);
            if (!hasPayload || !matrices.IsCreated || !metadata.IsCreated || count <= 0)
                return;

            int queryCount = spatialHash.CollectSphere(
                AbsoluteUniversePosition.FromRuntimePosition(playerPositionWS),
                _cascadeContactRadius,
                ReactiveFloraKindMask,
                _reactiveFloraQueryHandles);

            int lastContactPayloadIndex = underwater ? _lastUnderwaterPlayerContactPayloadIndex : _lastSurfacePlayerContactPayloadIndex;
            int nearestPayloadIndex = -1;
            float nearestDistanceSq = float.MaxValue;
            for (int i = 0; i < queryCount; i++)
            {
                if (!spatialHash.TryGetEntry(_reactiveFloraQueryHandles[i], out HectonSpatialHash.SpatialEntry entry))
                    continue;

                int payloadIndex = entry.PayloadId;
                if (payloadIndex < 0 || payloadIndex >= count || payloadIndex >= matrices.Length || payloadIndex >= metadata.Length)
                    continue;

                Vector3 positionWS = ExtractTranslation(matrices[payloadIndex]);
                float distanceSq = (positionWS - playerPositionWS).sqrMagnitude;
                if (distanceSq >= nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearestPayloadIndex = payloadIndex;
            }

            if (nearestPayloadIndex < 0)
            {
                if (lastContactPayloadIndex >= 0 && lastContactPayloadIndex < metadata.Length)
                    SetPlayerContactRuntimeFlag(metadata, lastContactPayloadIndex, false);

                if (underwater)
                    _lastUnderwaterPlayerContactPayloadIndex = -1;
                else
                    _lastSurfacePlayerContactPayloadIndex = -1;
                return;
            }

            if (lastContactPayloadIndex >= 0 &&
                lastContactPayloadIndex != nearestPayloadIndex &&
                lastContactPayloadIndex < metadata.Length)
            {
                SetPlayerContactRuntimeFlag(metadata, lastContactPayloadIndex, false);
            }

            SetPlayerContactRuntimeFlag(metadata, nearestPayloadIndex, true);
            if (underwater)
                _lastUnderwaterPlayerContactPayloadIndex = nearestPayloadIndex;
            else
                _lastSurfacePlayerContactPayloadIndex = nearestPayloadIndex;

            float simulationTime = GetCurrentSimulationTimeSeconds();
            float lastTriggerTime = underwater ? _lastUnderwaterCascadeTriggerTime : _lastSurfaceCascadeTriggerTime;
            int lastSourcePayloadIndex = underwater ? _lastUnderwaterCascadeSourcePayloadIndex : _lastSurfaceCascadeSourcePayloadIndex;
            if (nearestPayloadIndex == lastSourcePayloadIndex &&
                simulationTime < lastTriggerTime + _cascadeRetriggerCooldownSeconds)
            {
                return;
            }

            Vector3 sourcePositionWS = ExtractTranslation(matrices[nearestPayloadIndex]);
            spatialHash.CollectSphere(
                AbsoluteUniversePosition.FromRuntimePosition(sourcePositionWS),
                _cascadePropagationRadius,
                ReactiveFloraKindMask,
                _reactiveFloraQueryHandles);
            RegisterCascadeEvent(underwater, sourcePositionWS, simulationTime);
            RecomputeCascadePhaseSeeds(underwater, matrices, metadata, count);

            if (underwater)
            {
                _lastUnderwaterCascadeTriggerTime = simulationTime;
                _lastUnderwaterCascadeSourcePayloadIndex = nearestPayloadIndex;
            }
            else
            {
                _lastSurfaceCascadeTriggerTime = simulationTime;
                _lastSurfaceCascadeSourcePayloadIndex = nearestPayloadIndex;
            }
        }

        private void RegisterCascadeEvent(bool underwater, Vector3 centerWS, float simulationTime)
        {
            NativeArray<FloraCascadeEventPayload> cascadeEvents = underwater ? _underwaterCascadeEvents : _surfaceCascadeEvents;
            int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;
            CompactCascadeEvents(cascadeEvents, ref eventCount, simulationTime, _cascadeReleaseDurationSeconds);

            int writeIndex = eventCount < cascadeEvents.Length
                ? eventCount
                : FindOldestCascadeEventIndex(cascadeEvents, eventCount);

            cascadeEvents[writeIndex] = new FloraCascadeEventPayload
            {
                Center = new float3(centerWS.x, centerWS.y, centerWS.z),
                StartTimeSeconds = simulationTime,
                RadiusMeters = _cascadePropagationRadius,
                Padding = 0f
            };

            eventCount = Mathf.Min(cascadeEvents.Length, Mathf.Max(eventCount, writeIndex + 1));
            if (underwater)
                _underwaterCascadeEventCount = eventCount;
            else
                _surfaceCascadeEventCount = eventCount;
        }

        private void RecomputeCascadePhaseSeeds(
            bool underwater,
            NativeArray<Matrix4x4> matrices,
            NativeArray<HectonVegetationInstanceData> metadata,
            int count)
        {
            NativeArray<float> phaseSeeds = underwater ? _underwaterCascadePhaseSeeds : _surfaceCascadePhaseSeeds;
            NativeArray<FloraCascadeEventPayload> cascadeEvents = underwater ? _underwaterCascadeEvents : _surfaceCascadeEvents;
            int eventCount = underwater ? _underwaterCascadeEventCount : _surfaceCascadeEventCount;
            if (!phaseSeeds.IsCreated || count <= 0)
                return;

            CompactCascadeEvents(cascadeEvents, ref eventCount, GetCurrentSimulationTimeSeconds(), _cascadeReleaseDurationSeconds);
            if (underwater)
                _underwaterCascadeEventCount = eventCount;
            else
                _surfaceCascadeEventCount = eventCount;

            if (eventCount <= 0)
            {
                ClearCascadePhaseSeeds(underwater, count);
                UploadCascadePhaseSeedBuffer(underwater, count);
                return;
            }

            PopulateCascadePhaseSeedsJob job = new PopulateCascadePhaseSeedsJob
            {
                Matrices = matrices,
                Metadata = metadata,
                ReactiveTemplateMask = _cascadeReactiveTemplateMask,
                Events = cascadeEvents,
                EventCount = eventCount,
                PropagationSpeedMetersPerSecond = _cascadePropagationSpeed,
                InactiveSeed = InactiveCascadeSeed,
                PhaseSeeds = phaseSeeds
            };
            job.Run(count);
            UploadCascadePhaseSeedBuffer(underwater, count);
        }

        private void ClearCascadePhaseSeeds(bool underwater, int count)
        {
            NativeArray<float> phaseSeeds = underwater ? _underwaterCascadePhaseSeeds : _surfaceCascadePhaseSeeds;
            if (!phaseSeeds.IsCreated)
                return;

            int safeCount = math.min(count, phaseSeeds.Length);
            for (int i = 0; i < safeCount; i++)
                phaseSeeds[i] = InactiveCascadeSeed;
        }

        private void UploadCascadePhaseSeedBuffer(bool underwater, int count)
        {
            NativeArray<float> phaseSeeds = underwater ? _underwaterCascadePhaseSeeds : _surfaceCascadePhaseSeeds;
            GraphicsBuffer phaseSeedBuffer = underwater ? _underwaterCascadePhaseSeedBuffer : _surfaceCascadePhaseSeedBuffer;
            if (!phaseSeeds.IsCreated || phaseSeedBuffer == null || count <= 0)
            {
                ReleaseCascadePhaseSeedChannel(underwater);
                return;
            }

            int safeCount = math.min(count, phaseSeeds.Length);
            GraphicsBufferUploadUtility.UploadNativeArray(phaseSeedBuffer, phaseSeeds, safeCount);
            _vegetationBridge.BindReactivePhaseSeedBuffer(underwater, phaseSeedBuffer);
        }

        private void EnsureCascadePhaseSeedResources(bool underwater, int count)
        {
            if (count <= 0)
            {
                ReleaseCascadePhaseSeedChannel(underwater);
                return;
            }

            if (underwater)
            {
                EnsureFloatNativeArray(ref _underwaterCascadePhaseSeeds, count);
                EnsureStructuredFloatBuffer(ref _underwaterCascadePhaseSeedBuffer, count);
                return;
            }

            EnsureFloatNativeArray(ref _surfaceCascadePhaseSeeds, count);
            EnsureStructuredFloatBuffer(ref _surfaceCascadePhaseSeedBuffer, count);
        }

        private void ReleaseCascadePhaseSeedChannel(bool underwater)
        {
            if (_vegetationBridge != null)
                _vegetationBridge.BindReactivePhaseSeedBuffer(underwater, null);

            if (underwater)
            {
                DisposeNativeArray(ref _underwaterCascadePhaseSeeds);
                ReleaseGraphicsBuffer(ref _underwaterCascadePhaseSeedBuffer);
                return;
            }

            DisposeNativeArray(ref _surfaceCascadePhaseSeeds);
            ReleaseGraphicsBuffer(ref _surfaceCascadePhaseSeedBuffer);
        }

        private void EnsureReactiveFloraHashCapacity(ref HectonSpatialHash spatialHash, int count)
        {
            int safeCapacity = Mathf.NextPowerOfTwo(Mathf.Max(16, count));
            if (spatialHash != null)
                return;

            spatialHash = new HectonSpatialHash(safeCapacity, safeCapacity * 4);
        }

        private static void ClearRegisteredReactiveFloraHandles(HectonSpatialHash spatialHash, NativeList<int> registeredHandles)
        {
            if (spatialHash == null || !registeredHandles.IsCreated)
                return;

            for (int i = 0; i < registeredHandles.Length; i++)
                spatialHash.Unregister(registeredHandles[i]);

            registeredHandles.Clear();
        }

        private static float3 ResolveReactiveFloraHalfExtents(HectonVegetationInstanceData instanceData, int instanceType)
        {
            float width = Mathf.Lerp(0.45f, 2.2f, Mathf.Clamp01(Mathf.Abs(instanceData.WidthScale)));
            float height = instanceType == (int)HectonVegetationInstanceType.GiantKelp
                ? Mathf.Lerp(2.4f, 10f, Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale)))
                : Mathf.Lerp(0.45f, 2.4f, Mathf.Clamp01(Mathf.Abs(instanceData.HeightScale)));
            return new float3(width, Mathf.Max(0.35f, height * 0.5f), width);
        }

        private bool IsReactiveCascadeTemplate(HectonVegetationInstanceData instanceData)
        {
            if (!_cascadeReactiveTemplateMask.IsCreated)
                return false;

            int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
            return templateIndex >= 0 &&
                   templateIndex < _cascadeReactiveTemplateMask.Length &&
                   _cascadeReactiveTemplateMask[templateIndex] != 0;
        }

        private bool IsDefensiveSporeBurstTemplate(HectonVegetationInstanceData instanceData)
        {
            if (!_defensiveSporeBurstTemplateMask.IsCreated)
                return false;

            int templateIndex = Mathf.RoundToInt(instanceData.TemplateIndex);
            return templateIndex >= 0 &&
                   templateIndex < _defensiveSporeBurstTemplateMask.Length &&
                   _defensiveSporeBurstTemplateMask[templateIndex] != 0;
        }

        internal bool IsDefensiveSporeBurstTemplateIndex(int templateIndex)
        {
            return _defensiveSporeBurstTemplateMask.IsCreated &&
                   templateIndex >= 0 &&
                   templateIndex < _defensiveSporeBurstTemplateMask.Length &&
                   _defensiveSporeBurstTemplateMask[templateIndex] != 0;
        }

        private void RefreshCascadeTemplateMask(bool force)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            CacheStableHashIds(_cascadeReactiveStableIds, ref _cascadeReactiveStableHashIds);
            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (!force && _cachedCascadeReactiveTemplateCount == templateCount && _cascadeReactiveTemplateMask.IsCreated && _cascadeReactiveTemplateMask.Length == templateCount)
                return;

            _cachedCascadeReactiveTemplateCount = templateCount;
            if (templateCount <= 0 || _cascadeReactiveStableHashIds.Length == 0)
            {
                DisposeNativeArray(ref _cascadeReactiveTemplateMask);
                return;
            }

            EnsureByteNativeArray(ref _cascadeReactiveTemplateMask, templateCount);
            for (int i = 0; i < _cascadeReactiveTemplateMask.Length; i++)
                _cascadeReactiveTemplateMask[i] = 0;

            for (int i = 0; i < templateCount; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                    continue;

                _cascadeReactiveTemplateMask[i] = MatchesStableHash(LocHash.Compute(template.StableId), _cascadeReactiveStableHashIds) ? (byte)1 : (byte)0;
            }
        }

        private void RefreshDefensiveSporeBurstTemplateMask(bool force)
        {
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            CacheStableHashIds(_defensiveSporeBurstStableIds, ref _defensiveSporeBurstStableHashIds);
            FloraDataTemplate[] floraTemplates = _vegetationBridge != null ? _vegetationBridge.FloraTemplates : null;
            int templateCount = floraTemplates != null ? floraTemplates.Length : 0;
            if (!force && _cachedDefensiveSporeBurstTemplateCount == templateCount && _defensiveSporeBurstTemplateMask.IsCreated && _defensiveSporeBurstTemplateMask.Length == templateCount)
                return;

            _cachedDefensiveSporeBurstTemplateCount = templateCount;
            if (templateCount <= 0 || _defensiveSporeBurstStableHashIds.Length == 0)
            {
                DisposeNativeArray(ref _defensiveSporeBurstTemplateMask);
                return;
            }

            EnsureByteNativeArray(ref _defensiveSporeBurstTemplateMask, templateCount);
            for (int i = 0; i < _defensiveSporeBurstTemplateMask.Length; i++)
                _defensiveSporeBurstTemplateMask[i] = 0;

            for (int i = 0; i < templateCount; i++)
            {
                FloraDataTemplate template = floraTemplates[i];
                if (template == null || string.IsNullOrWhiteSpace(template.StableId))
                    continue;

                _defensiveSporeBurstTemplateMask[i] = MatchesStableHash(LocHash.Compute(template.StableId), _defensiveSporeBurstStableHashIds) ? (byte)1 : (byte)0;
            }
        }

        private static void CompactCascadeEvents(
            NativeArray<FloraCascadeEventPayload> cascadeEvents,
            ref int eventCount,
            float simulationTime,
            float activeLifetimeSeconds)
        {
            if (!cascadeEvents.IsCreated)
            {
                eventCount = 0;
                return;
            }

            int writeIndex = 0;
            int safeCount = math.min(eventCount, cascadeEvents.Length);
            for (int i = 0; i < safeCount; i++)
            {
                FloraCascadeEventPayload cascadeEvent = cascadeEvents[i];
                if (simulationTime >= cascadeEvent.StartTimeSeconds + activeLifetimeSeconds)
                    continue;

                cascadeEvents[writeIndex] = cascadeEvent;
                writeIndex++;
            }

            eventCount = writeIndex;
        }

        private static int FindOldestCascadeEventIndex(NativeArray<FloraCascadeEventPayload> cascadeEvents, int eventCount)
        {
            if (!cascadeEvents.IsCreated || eventCount <= 0)
                return 0;

            int bestIndex = 0;
            float oldestTime = float.MaxValue;
            int safeCount = math.min(eventCount, cascadeEvents.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (cascadeEvents[i].StartTimeSeconds >= oldestTime)
                    continue;

                oldestTime = cascadeEvents[i].StartTimeSeconds;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static void CacheStableHashIds(string[] stableIds, ref int[] hashIds)
        {
            int count = stableIds != null ? stableIds.Length : 0;
            if (hashIds == null || hashIds.Length != count)
                hashIds = new int[count];

            for (int i = 0; i < count; i++)
                hashIds[i] = string.IsNullOrWhiteSpace(stableIds[i]) ? 0 : LocHash.Compute(stableIds[i]);
        }

        private static bool MatchesStableHash(int stableHashId, int[] resolvedHashes)
        {
            if (stableHashId == 0 || resolvedHashes == null)
                return false;

            for (int i = 0; i < resolvedHashes.Length; i++)
            {
                if (resolvedHashes[i] == stableHashId)
                    return true;
            }

            return false;
        }

        private static void SetPlayerContactRuntimeFlag(NativeArray<HectonVegetationInstanceData> metadata, int index, bool enabled)
        {
            if (!metadata.IsCreated || index < 0 || index >= metadata.Length)
                return;

            HectonVegetationInstanceData instanceData = metadata[index];
            byte packedFlags = HectonVegetationRuntimeFlagEncoding.ExtractPackedFlags(instanceData.RuntimeFlags);
            if (enabled)
                packedFlags |= (byte)HectonVegetationRuntimeFlags.PlayerContact;
            else
                packedFlags &= unchecked((byte)~(byte)HectonVegetationRuntimeFlags.PlayerContact);

            instanceData.RuntimeFlags = packedFlags;
            metadata[index] = instanceData;
        }

        private void ClearReactiveFloraSpatialState()
        {
            if (_surfaceReactiveFloraHandles.IsCreated && _surfaceReactiveFloraHash != null)
                ClearRegisteredReactiveFloraHandles(_surfaceReactiveFloraHash, _surfaceReactiveFloraHandles);
            if (_underwaterReactiveFloraHandles.IsCreated && _underwaterReactiveFloraHash != null)
                ClearRegisteredReactiveFloraHandles(_underwaterReactiveFloraHash, _underwaterReactiveFloraHandles);

            ReleaseCascadePhaseSeedChannel(underwater: false);
            ReleaseCascadePhaseSeedChannel(underwater: true);
            _surfaceCascadeEventCount = 0;
            _underwaterCascadeEventCount = 0;
            _lastSurfaceCascadeSourcePayloadIndex = -1;
            _lastUnderwaterCascadeSourcePayloadIndex = -1;
            _lastSurfacePlayerContactPayloadIndex = -1;
            _lastUnderwaterPlayerContactPayloadIndex = -1;
            _defensiveSporeBurstCount = 0;
        }

        private static void EnsureByteNativeArray(ref NativeArray<byte> array, int requiredCount)
        {
            if (requiredCount <= 0)
            {
                DisposeNativeArray(ref array);
                return;
            }

            if (array.IsCreated && array.Length == requiredCount)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<byte>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[templateCount] - reactive flora template membership mask - owner: FloraInteractionManager
        }

        private static void EnsureFloatNativeArray(ref NativeArray<float> array, int requiredCount)
        {
            if (requiredCount <= 0)
            {
                DisposeNativeArray(ref array);
                return;
            }

            if (array.IsCreated && array.Length == requiredCount)
                return;

            DisposeNativeArray(ref array);
            array = new NativeArray<float>(requiredCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[instanceCount] - per-instance cascade phase seed staging - owner: FloraInteractionManager
        }

        private static void EnsureStructuredFloatBuffer(ref GraphicsBuffer buffer, int requiredCount)
        {
            if (requiredCount <= 0)
            {
                ReleaseGraphicsBuffer(ref buffer);
                return;
            }

            if (buffer != null && buffer.count >= requiredCount)
                return;

            ReleaseGraphicsBuffer(ref buffer);
            buffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float>(requiredCount); // COLD ALLOC: GraphicsBuffer[instanceCount] - per-instance cascade phase seed GPU buffer - owner: FloraInteractionManager
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            array.Dispose();
            array = default;
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            list.Dispose();
            list = default;
        }

        private static void ReleaseGraphicsBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static Vector3 ExtractTranslation(Matrix4x4 matrix)
        {
            return new Vector3(matrix.m03, matrix.m13, matrix.m23);
        }

        private void PublishPredatorThreatGlobals(Vector3 samplePositionWS)
        {
            float aggressiveBioformThreat = 0f;
            if (WorldSpatialHashGrid.TryGetNearestAggressiveBioform(
                samplePositionWS,
                _predatorThreatQueryRadius,
                _predatorThreatMask.value,
                _playerTransform,
                out SpatialQueryHit hit))
            {
                float distance = Vector3.Distance(hit.Position, samplePositionWS);
                aggressiveBioformThreat = 1f - Mathf.Clamp01(distance / Mathf.Max(_predatorThreatQueryRadius, 0.001f));
            }

            float bridgeThreat = _vegetationBridge != null ? Mathf.Clamp01(_vegetationBridge.GetThreatLevel(samplePositionWS)) : 0f;
            float threatExposure = Mathf.Max(aggressiveBioformThreat, bridgeThreat);
            Shader.SetGlobalVector(
                _FloraPredatorThreatParamsId,
                new Vector4(
                    threatExposure,
                    Mathf.Max(0.25f, _predatorBiolumDimRadius),
                    _predatorBiolumDimStrength,
                    aggressiveBioformThreat));
        }

        private void RefreshFlowFieldGlobals(float deltaTime)
        {
            _flowFieldUploadTimer -= deltaTime;
            if (_vegetationBridge == null)
                _vegetationBridge = ResolveVegetationBridge();

            if (_vegetationBridge == null)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            bool hasPayload = _vegetationBridge.TryGetEcosystemFlowFieldPayload(
                out NativeArray<float2> flowVectors,
                out int gridResolution,
                out Vector3 gridCenter,
                out float cellSize);
            if (!hasPayload)
            {
                _flowFieldResolution = 0;
                _flowFieldCellSize = 0f;
                _flowFieldCenterWS = Vector3.zero;
                PublishFlowFieldGlobals();
                return;
            }

            _flowFieldResolution = gridResolution;
            _flowFieldCellSize = cellSize;
            _flowFieldCenterWS = gridCenter;

            float recenterThreshold = math.max(0.01f, cellSize * FlowFieldRecenterThresholdCells);
            bool forceUpload =
                _flowFieldBuffer == null ||
                _flowFieldUploadTimer <= 0f ||
                _lastUploadedFlowFieldCenterWS == Vector3.zero ||
                (gridCenter - _lastUploadedFlowFieldCenterWS).sqrMagnitude >= recenterThreshold * recenterThreshold;

            if (forceUpload)
            {
                int requiredCount = math.max(1, flowVectors.Length);
                if (_flowFieldBuffer == null || _flowFieldBuffer.count != requiredCount)
                {
                    ReleaseFlowFieldBuffer();
                    _flowFieldBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float2>(requiredCount); // COLD ALLOC: GraphicsBuffer[flowVectors.Length] - authoritative ecosystem flow-field GPU staging for flora shading - owner: FloraInteractionManager
                }

                GraphicsBufferUploadUtility.UploadNativeArray(_flowFieldBuffer, flowVectors, requiredCount);
                _lastUploadedFlowFieldCenterWS = gridCenter;
                _flowFieldUploadTimer = FlowFieldUploadIntervalSeconds;
            }

            PublishFlowFieldGlobals();
        }

        private void PublishFlowFieldGlobals()
        {
            if (_flowFieldBuffer != null)
                Shader.SetGlobalBuffer(_MarineSnowFlowFieldId, _flowFieldBuffer);

            Shader.SetGlobalVector(
                _MarineSnowFlowFieldCenterCellSizeId,
                new Vector4(_flowFieldCenterWS.x, _flowFieldCenterWS.y, _flowFieldCenterWS.z, _flowFieldCellSize));
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, _flowFieldResolution);
        }

        private void PublishPlayerRuntimePosition(
            Vector3 playerRuntimePosition,
            float playerBendRadius,
            float playerSpeed,
            float targetForce)
        {
            float normalizedForce = _maxInteractionForce > 0.0001f
                ? Mathf.Clamp01(targetForce / _maxInteractionForce)
                : 0f;

            Shader.SetGlobalVector(
                _PlayerRuntimePositionId,
                new Vector4(
                    playerRuntimePosition.x,
                    playerRuntimePosition.y,
                    playerRuntimePosition.z,
                    Mathf.Max(0.05f, playerBendRadius)));
            Shader.SetGlobalVector(
                _PlayerFloraInteractionParamsId,
                new Vector4(
                    playerSpeed,
                    normalizedForce,
                    _hasActiveScooterWake ? 1f : 0f,
                    1f));
        }

        private Vector3 ResolveGlobalOceanFlow(Vector3 samplePositionWS, HectonFluidEngine fluidEngine)
        {
            IHectonOceanKinematics provider = HectonOceanRegistry.ActiveProvider;
            _oceanKinematicsProvider = provider;
            if (provider != null &&
                provider.IsAvailable &&
                _oceanFlowSamplePositions.IsCreated &&
                _oceanFlowSampleResults.IsCreated &&
                _oceanFlowSamplePositions.Length > 0 &&
                _oceanFlowSampleResults.Length > 0)
            {
                _oceanFlowSamplePositions[0] = samplePositionWS;
                if (provider.GetSurfaceFlow(_oceanFlowSamplePositions, 1, 1f, _oceanFlowSampleResults))
                    return _oceanFlowSampleResults[0];
            }

            return fluidEngine != null ? fluidEngine.CurrentVector : Vector3.zero;
        }

        private void CreateWakeTrailResources()
        {
            if (_wakeTrailDisabled)
                return;

            if (_wakeTrailRead == null)
                _wakeTrailRead = CreateWakeTrailTexture("__VegetationWakeTrail_A");

            if (_wakeTrailWrite == null)
                _wakeTrailWrite = CreateWakeTrailTexture("__VegetationWakeTrail_B");

            if (!_queuedWakeTrailStampCommands.IsCreated)
                _queuedWakeTrailStampCommands = new NativeArray<WakeTrailStampCommand>(WakeTrailStampCommandCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<WakeTrailStampCommand>[4] - queued vegetation wake-trail stamps for single compute dispatch - owner: FloraInteractionManager

            if (_wakeTrailStampCommandBuffer == null)
                _wakeTrailStampCommandBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<WakeTrailStampCommand>(WakeTrailStampCommandCapacity); // COLD ALLOC: GraphicsBuffer[4] - queued vegetation wake-trail stamp buffer for single compute dispatch - owner: FloraInteractionManager

            TryAutoAssignWakeTrailSimulationCompute();
            if (_wakeTrailSimulationCompute == null)
            {
                Debug.LogError("[FloraInteractionManager] Missing wake trail compute shader. Expected Hecton_VegetationWakeTrailSim.compute.", this);
                _wakeTrailDisabled = true;
                PublishWakeTrailGlobals();
                return;
            }

            if (_wakeTrailSimulationKernel < 0)
                _wakeTrailSimulationKernel = _wakeTrailSimulationCompute.FindKernel("SimulateWakeTrail");

            RefreshWakeTrailWorldRect(Vector3.zero, forceClear: true);
            PublishWakeTrailGlobals();
        }

        private void ReleaseWakeTrailResources()
        {
            ReleaseWakeTrailTexture(ref _wakeTrailRead);
            ReleaseWakeTrailTexture(ref _wakeTrailWrite);

            if (_wakeTrailStampCommandBuffer != null)
            {
                _wakeTrailStampCommandBuffer.Release();
                _wakeTrailStampCommandBuffer = null;
            }

            if (_queuedWakeTrailStampCommands.IsCreated)
                _queuedWakeTrailStampCommands.Dispose();

            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
            _lastWakeTrailDispatchFrame = -1;

            Shader.SetGlobalFloat(_WakeTrailActiveId, 0f);
        }

        private void UpdateWakeTrail(Vector3 playerPosition, Vector3 playerVelocity, float deltaTime)
        {
            if (_wakeTrailDisabled)
                return;

            _hasActiveSubmarineWake = false;
            CreateWakeTrailResources();
            if (_wakeTrailRead == null || _wakeTrailWrite == null || _wakeTrailSimulationCompute == null || _wakeTrailStampCommandBuffer == null)
            {
                PublishWakeTrailGlobals();
                return;
            }

            RefreshWakeTrailWorldRect(playerPosition, forceClear: false);

            bool wrotePass = false;
            float fade = Mathf.Max(0f, deltaTime / _wakeTrailFadeSeconds);
            float strongestStamp = 0f;

            float playerSpeed = playerVelocity.magnitude;
            if (playerSpeed >= _wakeTrailPlayerMinSpeed)
            {
                QueueWakeTrailStamp(
                    playerPosition,
                    playerVelocity,
                    _wakeTrailBaseRadius,
                    Mathf.Clamp(_wakeTrailMinLength + playerSpeed * _wakeTrailVelocityToLength, _wakeTrailMinLength, _wakeTrailMaxLength),
                    Mathf.Clamp01(_wakeTrailPlayerStrength));
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, _wakeTrailPlayerStrength);
            }

            float scooterSpeed = _smoothedScooterVelocity.magnitude;
            if (_hasActiveScooterWake && scooterSpeed >= _wakeTrailScooterMinSpeed)
            {
                QueueWakeTrailStamp(
                    _lastPublishedScooterWakePosition,
                    _smoothedScooterVelocity,
                    _wakeTrailBaseRadius * 1.15f,
                    Mathf.Clamp(_wakeTrailMinLength + scooterSpeed * (_wakeTrailVelocityToLength * 1.7f), _wakeTrailMinLength * 1.25f, _wakeTrailMaxLength),
                    Mathf.Clamp01(_wakeTrailScooterStrength));
                wrotePass = true;
                strongestStamp = Mathf.Max(strongestStamp, _wakeTrailScooterStrength);
            }

            ISubmarineRuntimeContext submarine = GlobalRegistry.Submarine;
            Rigidbody submarineHull = submarine != null ? submarine.HullRigidbody : null;
            if (submarineHull != null)
            {
                Vector3 submarineVelocity = submarineHull.linearVelocity;
                float submarineSpeed = submarineVelocity.magnitude;
                if (submarineSpeed >= _wakeTrailSubmarineMinSpeed)
                {
                    _hasActiveSubmarineWake = true;
                    _lastPublishedSubmarineWakePosition = submarineHull.worldCenterOfMass;
                    bool kelpWhipActive = submarineSpeed >= _wakeTrailSubmarineWhipSpeed;
                    float submarineStrength = kelpWhipActive
                        ? Mathf.Clamp01(_wakeTrailSubmarineStrength * 1.35f)
                        : Mathf.Clamp01(_wakeTrailSubmarineStrength);
                    float lengthScale = kelpWhipActive ? 2.35f : 1.55f;
                    float radiusScale = kelpWhipActive ? 1.55f : 1f;
                    QueueWakeTrailStamp(
                        _lastPublishedSubmarineWakePosition,
                        submarineVelocity,
                        _wakeTrailSubmarineRadius * radiusScale,
                        Mathf.Clamp(_wakeTrailMinLength + submarineSpeed * (_wakeTrailVelocityToLength * lengthScale), _wakeTrailMinLength * 1.5f, _wakeTrailMaxLength * 1.6f),
                        submarineStrength);
                    wrotePass = true;
                    strongestStamp = Mathf.Max(strongestStamp, submarineStrength);
                }
            }

            if (wrotePass || _wakeTrailEnergy > 0.0001f || _pendingWakeTrailScrollUv.sqrMagnitude > 0.0000001f)
                ExecuteWakeTrailSimulation(fade);

            _wakeTrailEnergy = Mathf.Max(0f, wrotePass ? Mathf.Max(_wakeTrailEnergy - fade, strongestStamp) : (_wakeTrailEnergy - fade));
            PublishWakeTrailGlobals();
        }

        private void RefreshWakeTrailWorldRect(Vector3 anchorPosition, bool forceClear)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            float desiredWorldSize = Mathf.Max(64f, _wakeTrailWorldSize);
            float snapStride = ResolveWakeTrailSnapStride(desiredWorldSize);
            Vector2 desiredCenterXZ = QuantizeWakeTrailCenter(new Vector2(anchorPosition.x, anchorPosition.z), snapStride);

            bool mustClear = forceClear || _wakeTrailRuntimeWorldSize <= 0f || Mathf.Abs(desiredWorldSize - _wakeTrailRuntimeWorldSize) > 0.001f;
            Vector2 centerDelta = desiredCenterXZ - _wakeTrailCenterXZ;
            if (!mustClear && centerDelta.sqrMagnitude <= 0.000001f)
                return;

            _wakeTrailCenterXZ = desiredCenterXZ;
            _wakeTrailRuntimeWorldSize = desiredWorldSize;
            float halfSize = desiredWorldSize * 0.5f;
            _wakeTrailWorldRect = new Vector4(
                desiredCenterXZ.x - halfSize,
                desiredCenterXZ.y - halfSize,
                1f / Mathf.Max(desiredWorldSize, 0.001f),
                1f / Mathf.Max(desiredWorldSize, 0.001f));

            if (mustClear)
            {
                ClearWakeTrailTextures();
                return;
            }

            QueueWakeTrailScroll(centerDelta);
        }

        private void QueueWakeTrailStamp(
            Vector3 positionWS,
            Vector3 directionWS,
            float radiusWS,
            float lengthWS,
            float strength)
        {
            if (!_queuedWakeTrailStampCommands.IsCreated || _queuedWakeTrailStampCount >= WakeTrailStampCommandCapacity)
                return;

            Vector2 uvCenter = new Vector2(
                (positionWS.x - _wakeTrailWorldRect.x) * _wakeTrailWorldRect.z,
                (positionWS.z - _wakeTrailWorldRect.y) * _wakeTrailWorldRect.w);
            Vector2 directionXZ = new Vector2(directionWS.x, directionWS.z);
            float directionMagnitude = directionWS.magnitude;
            float verticalImpulse = directionMagnitude > 0.0001f
                ? Mathf.Clamp01(Mathf.Abs(directionWS.y) / directionMagnitude) * Mathf.Clamp01(directionMagnitude * 0.12f)
                : 0f;
            if (directionXZ.sqrMagnitude <= 0.0001f)
                directionXZ = Vector2.up;
            directionXZ.Normalize();

            float uvRadius = radiusWS * _wakeTrailWorldRect.z;
            float uvLength = lengthWS * _wakeTrailWorldRect.z;

            _queuedWakeTrailStampCommands[_queuedWakeTrailStampCount] = new WakeTrailStampCommand
            {
                UvEllipse = new Vector4(uvCenter.x, uvCenter.y, uvRadius, uvLength),
                DirectionStrengthVertical = new Vector4(directionXZ.x, directionXZ.y, Mathf.Clamp01(strength), verticalImpulse)
            };
            _queuedWakeTrailStampCount++;
        }

        private void ExecuteWakeTrailSimulation(float fade)
        {
            if (_wakeTrailSimulationCompute == null ||
                _wakeTrailSimulationKernel < 0 ||
                _wakeTrailRead == null ||
                _wakeTrailWrite == null ||
                _wakeTrailStampCommandBuffer == null ||
                _lastWakeTrailDispatchFrame == Time.frameCount)
            {
                return;
            }

            if (_queuedWakeTrailStampCount > 0 && _queuedWakeTrailStampCommands.IsCreated)
                GraphicsBufferUploadUtility.UploadNativeArray(_wakeTrailStampCommandBuffer, _queuedWakeTrailStampCommands, _queuedWakeTrailStampCount);

            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailSourceId, _wakeTrailRead);
            _wakeTrailSimulationCompute.SetTexture(_wakeTrailSimulationKernel, _WakeTrailResultId, _wakeTrailWrite);
            _wakeTrailSimulationCompute.SetBuffer(_wakeTrailSimulationKernel, _WakeTrailStampCommandsId, _wakeTrailStampCommandBuffer);
            _wakeTrailSimulationCompute.SetInt(_WakeTrailStampCountId, _queuedWakeTrailStampCount);
            _wakeTrailSimulationCompute.SetVector(_WakeTrailScrollUvOffsetId, new Vector4(_pendingWakeTrailScrollUv.x, _pendingWakeTrailScrollUv.y, 0f, 0f));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailFadeDeltaId, Mathf.Max(0f, fade));
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDiffusionId, _wakeTrailDiffusion);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailWaveStrengthId, _wakeTrailWaveStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailDampingId, _wakeTrailWaveDamping);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailCurlStrengthId, _wakeTrailCurlStrength);
            _wakeTrailSimulationCompute.SetFloat(_WakeTrailSimulationTimeId, Time.unscaledTime);
            _wakeTrailSimulationCompute.SetVector(
                _WakeTrailTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));

            int groupCount = Mathf.CeilToInt(_wakeTrailRuntimeResolution / (float)WakeTrailThreadGroupSize);
            _wakeTrailSimulationCompute.Dispatch(_wakeTrailSimulationKernel, Mathf.Max(1, groupCount), Mathf.Max(1, groupCount), 1);

            RenderTexture temp = _wakeTrailRead;
            _wakeTrailRead = _wakeTrailWrite;
            _wakeTrailWrite = temp;
            _lastWakeTrailDispatchFrame = Time.frameCount;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void QueueWakeTrailScroll(Vector2 centerDelta)
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            float uvOffsetX = centerDelta.x / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            float uvOffsetY = centerDelta.y / Mathf.Max(_wakeTrailRuntimeWorldSize, 0.001f);
            if (Mathf.Abs(uvOffsetX) >= 1f || Mathf.Abs(uvOffsetY) >= 1f)
            {
                ClearWakeTrailTextures();
                return;
            }

            _pendingWakeTrailScrollUv.x += uvOffsetX;
            _pendingWakeTrailScrollUv.y += uvOffsetY;
        }

        private void ClearWakeTrailTextures()
        {
            if (_wakeTrailRead == null || _wakeTrailWrite == null)
                return;

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = _wakeTrailRead;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = _wakeTrailWrite;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
            _wakeTrailEnergy = 0f;
            _pendingWakeTrailScrollUv = Vector2.zero;
            _queuedWakeTrailStampCount = 0;
        }

        private void PublishWakeTrailGlobals()
        {
            if (_wakeTrailDisabled || _wakeTrailRead == null)
            {
                Shader.SetGlobalFloat(_WakeTrailActiveId, 0f);
                Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, 0f);
                return;
            }

            Shader.SetGlobalTexture(_WakeTrailTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_WakeTrailWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_WakeTrailActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            Shader.SetGlobalTexture(_ShallowWaterFieldTextureId, _wakeTrailRead);
            Shader.SetGlobalVector(_ShallowWaterFieldWorldRectId, _wakeTrailWorldRect);
            Shader.SetGlobalFloat(_ShallowWaterFieldActiveId, _wakeTrailRuntimeWorldSize > 0f ? 1f : 0f);
            Shader.SetGlobalVector(
                _ShallowWaterFieldTexelSizeId,
                new Vector4(
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    1f / Mathf.Max(_wakeTrailRuntimeResolution, 1),
                    _wakeTrailRuntimeResolution,
                    _wakeTrailRuntimeResolution));
        }

        private RenderTexture CreateWakeTrailTexture(string textureName)
        {
            RenderTexture texture = new RenderTexture(_wakeTrailRuntimeResolution, _wakeTrailRuntimeResolution, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] - persistent vegetation wake trail ping-pong target - owner: FloraInteractionManager
            texture.Create();
            return texture;
        }

        private static void ReleaseWakeTrailTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

        private float ResolveWakeTrailSnapStride(float worldSize)
        {
            float pixelWorldSize = worldSize / Mathf.Max(_wakeTrailRuntimeResolution, 1);
            return pixelWorldSize * Mathf.Max(0.1f, _wakeTrailCenterSnapPixelStride);
        }

        private static Vector2 QuantizeWakeTrailCenter(Vector2 centerXZ, float stride)
        {
            if (stride <= 0.0001f)
                return centerXZ;

            return new Vector2(
                Mathf.Round(centerXZ.x / stride) * stride,
                Mathf.Round(centerXZ.y / stride) * stride);
        }

        private static Color ResolveAmbientColor()
        {
            switch (RenderSettings.ambientMode)
            {
                case AmbientMode.Flat:
                    return RenderSettings.ambientLight;
                case AmbientMode.Trilight:
                    return RenderSettings.ambientEquatorColor;
                default:
                    return RenderSettings.ambientSkyColor;
            }
        }

        private void ApplyRuntimeOffsetToCachedState(Vector3 runtimeOffset)
        {
            _smoothPosition += runtimeOffset;
            if (_hasLastPlayerPosition)
                _lastPlayerPosition += runtimeOffset;

            if (_hasSmoothedScooterPosition)
                _smoothedScooterPosition += runtimeOffset;

            if (_hasActiveScooterWake)
                _lastPublishedScooterWakePosition += runtimeOffset;

            if (_hasActiveSubmarineWake)
                _lastPublishedSubmarineWakePosition += runtimeOffset;

            if (_interactionPoints != null &&
                _interactionBuffer != null &&
                _lastPublishedInteractionCount > 0)
            {
                int interactionCount = Mathf.Min(_lastPublishedInteractionCount, _interactionPoints.Length);
                for (int i = 0; i < interactionCount; i++)
                {
                    Vector4 positionRadius = _interactionPoints[i].PositionRadius;
                    positionRadius.x += runtimeOffset.x;
                    positionRadius.y += runtimeOffset.y;
                    positionRadius.z += runtimeOffset.z;
                    _interactionPoints[i].PositionRadius = positionRadius;
                }

                GraphicsBufferUploadUtility.UploadArray(_interactionBuffer, _interactionPoints, interactionCount);
            }

            if (_flowFieldResolution > 0 || _flowFieldCellSize > 0f)
            {
                _flowFieldCenterWS += runtimeOffset;
                _lastUploadedFlowFieldCenterWS += runtimeOffset;
                PublishFlowFieldGlobals();
            }

            if (_wakeTrailWorldRect.z > 0f && _wakeTrailWorldRect.w > 0f)
            {
                _wakeTrailCenterXZ += new Vector2(runtimeOffset.x, runtimeOffset.z);
                _wakeTrailWorldRect.x += runtimeOffset.x;
                _wakeTrailWorldRect.y += runtimeOffset.z;
                PublishWakeTrailGlobals();
            }

            if (_publishedParasiteAnchorCount > 0)
            {
                for (int i = 0; i < _publishedParasiteAnchorCount; i++)
                {
                    Vector4 anchor = _parasiteAnchorData[i];
                    anchor.x += runtimeOffset.x;
                    anchor.y += runtimeOffset.y;
                    anchor.z += runtimeOffset.z;
                    _parasiteAnchorData[i] = anchor;
                }

                PublishParasiteInfectionGlobals();
            }

            PublishEnvironmentGlobals(_playerTransform != null ? _playerTransform.position : _smoothPosition);
        }

        private void ResetInteractionGlobals()
        {
            Shader.SetGlobalVector(_PropWashPosId, Vector4.zero);
            Shader.SetGlobalFloat(_PropWashForceId, 0f);
            Shader.SetGlobalInt(_InteractionCountId, 0);
            Shader.SetGlobalVector(_PlayerRuntimePositionId, Vector4.zero);
            Shader.SetGlobalVector(_PlayerFloraInteractionParamsId, Vector4.zero);
            Shader.SetGlobalVector(_GlobalOceanFlowId, Vector4.zero);
            Shader.SetGlobalVector(_VegetationCurrentVectorId, Vector4.zero);
            Shader.SetGlobalFloat(_VegetationCurrentStrengthId, 0f);
            Shader.SetGlobalVector(_FloraPredatorThreatParamsId, Vector4.zero);
            Shader.SetGlobalVector(_FloraLifecycleParamsId, new Vector4(0f, 0f, 1f, 0f));
            Shader.SetGlobalVector(_MarineSnowFlowFieldCenterCellSizeId, Vector4.zero);
            Shader.SetGlobalInt(_FloraFlowFieldResolutionId, 0);
            Shader.SetGlobalVector(_ParasiteGlobalsId, Vector4.zero);
            _lastPublishedInteractionCount = 0;
            _lastPublishedPlayerVelocity = Vector3.zero;
            _lastPublishedScooterWakePosition = Vector3.zero;
            _lastPublishedSubmarineWakePosition = Vector3.zero;
            _smoothedPlayerVelocity = Vector3.zero;
            _smoothedPlayerVelocityDamp = Vector3.zero;
            _smoothedScooterVelocity = Vector3.zero;
            _smoothedScooterVelocityDamp = Vector3.zero;
            _smoothedScooterPositionDamp = Vector3.zero;
            _hasSmoothedScooterPosition = false;
            _hasActiveScooterWake = false;
            _hasActiveSubmarineWake = false;
            _wakeTrailEnergy = 0f;
            _playerSedimentCooldownRemaining = 0f;
            _scooterSedimentCooldownRemaining = 0f;
            _toxicSporeScanTimer = 0f;
            _lastToxicSporeExposure01 = 0f;
            _moduleParasiteScanTimer = 0f;
            _publishedParasiteAnchorCount = 0;

            if (_interactionBuffer != null)
                Shader.SetGlobalBuffer(_InteractionBufferId, _interactionBuffer);

            ClearWakeTrailTextures();
            PublishWakeTrailGlobals();
        }

        private void ReleaseFlowFieldBuffer()
        {
            if (_flowFieldBuffer == null)
                return;

            _flowFieldBuffer.Release();
            _flowFieldBuffer = null;
        }

        private void TryRegister()
        {
            if (_isRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _isRegistered = true;
        }

        private void TryUnregister()
        {
            if (!_isRegistered)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _isRegistered = false;
        }

        private static long EstimateGraphicsBufferBytes(GraphicsBuffer buffer)
        {
            return buffer != null ? (long)buffer.count * buffer.stride : 0L;
        }

        private static long EstimateRenderTextureBytes(RenderTexture texture)
        {
            if (texture == null)
                return 0L;

            int bytesPerPixel = 4;
            return (long)texture.width * texture.height * bytesPerPixel;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            TryAutoAssignWakeTrailSimulationCompute();
        }
#endif

        private void TryAutoAssignWakeTrailSimulationCompute()
        {
#if UNITY_EDITOR
            if (_wakeTrailSimulationCompute == null)
                _wakeTrailSimulationCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>(WakeTrailSimulationComputeAssetPath);
#endif
        }
    }
}
