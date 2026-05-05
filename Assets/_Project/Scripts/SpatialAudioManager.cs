// ============================================================================
// HECTON-8 â€” SpatialAudioManager.cs
// Ð’Ñ‹ÑÐ¾ÐºÐ¾Ð¿Ñ€Ð¾Ð¸Ð·Ð²Ð¾Ð´Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ð°Ñ ÑÐ¸ÑÑ‚ÐµÐ¼Ð° Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ° Ñ Ð¿ÑƒÐ»Ð¸Ð½Ð³Ð¾Ð¼.
//
// ÐÐ Ð¥Ð˜Ð¢Ð•ÐšÐ¢Ð£Ð Ð:
//   â€¢ Ð¡Ð¸Ð½Ð³Ð»Ñ‚Ð¾Ð½: Ð¿ÑƒÐ» 3D AudioSource + Ð¾Ñ‚Ð´ÐµÐ»ÑŒÐ½Ñ‹Ð¹ Ð¿ÑƒÐ» 2D (ÑˆÐ»ÐµÐ¼/UI).
//   â€¢ Zero-GC Ð² hot path: Ð¼Ð°ÑÑÐ¸Ð²Ñ‹ Ñ„Ð¸ÐºÑÐ¸Ñ€Ð¾Ð²Ð°Ð½Ð½Ð¾Ð³Ð¾ Ñ€Ð°Ð·Ð¼ÐµÑ€Ð°, no LINQ, no allocations.
//   â€¢ ÐŸÐ¾Ð´Ð´ÐµÑ€Ð¶ÐºÐ° 3D-Ð¿ÑƒÐ»Ð° (PlayAtPoint) Ð¸ 2D-Ð¿ÑƒÐ»Ð° (PlayStatic2D).
//   â€¢ Ð’Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ðµ ÑÐ°Ð¼Ð¾Ð³Ð¾ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ° Ð¿Ñ€Ð¸ Ð¸ÑÑ‡ÐµÑ€Ð¿Ð°Ð½Ð¸Ð¸ Ð¿ÑƒÐ»Ð°.
//   â€¢ AudioMixerGroup Ð¼Ð°Ñ€ÑˆÑ€ÑƒÑ‚Ð¸Ð·Ð°Ñ†Ð¸Ñ (SFX, Interface, Ambient).
//
// ÐžÐŸÐ¢Ð˜ÐœÐ˜Ð—ÐÐ¦Ð˜Ð¯ (MX350 / CPU):
//   â€¢ Ð–Ñ‘ÑÑ‚ÐºÐ¸Ð¹ Ð»Ð¸Ð¼Ð¸Ñ‚ Ð¾Ð´Ð½Ð¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ñ… AudioSource (default 16, max 32).
//   â€¢ Linear Rolloff Ð´Ð»Ñ Ð¿Ñ€ÐµÐ´ÑÐºÐ°Ð·ÑƒÐµÐ¼Ð¾Ð³Ð¾ Ð·Ð°Ñ‚ÑƒÑ…Ð°Ð½Ð¸Ñ Ð±ÐµÐ· Ð»Ð¸ÑˆÐ½Ð¸Ñ… Ð²Ñ‹Ñ‡Ð¸ÑÐ»ÐµÐ½Ð¸Ð¹.
//   â€¢ ÐÐµÑ‚ per-frame loop â€” Ð²ÑÑ Ð»Ð¾Ð³Ð¸ÐºÐ° Ð² Ð¼Ð¾Ð¼ÐµÐ½Ñ‚Ðµ Ð²Ñ‹Ð·Ð¾Ð²Ð° Play.
//   â€¢ ÐŸÑƒÐ» ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð· Ð² Awake, Ð´Ð°Ð»ÑŒÑˆÐµ â€” Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿ÐµÑ€ÐµÐ¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ðµ.
//
// API:
//   Hecton8.Core.GlobalRegistry.Audio.PlayAtPoint(clip, position, volume, pitch)
//   Hecton8.Core.GlobalRegistry.Audio.PlayAtPoint(clip, position, volume, pitch, mixerGroup)
//   Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(clip, volume)
//   Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(clip, volume, mixerGroup)
//   Hecton8.Core.GlobalRegistry.Audio.StopAll()
//
// MIXER GROUPS:
//   ÐÐ°Ð·Ð½Ð°Ñ‡Ð°ÑŽÑ‚ÑÑ Ð² Ð¸Ð½ÑÐ¿ÐµÐºÑ‚Ð¾Ñ€Ðµ: SfxGroup, InterfaceGroup, AmbientGroup.
//   ÐŸÐ¾Ð·Ð²Ð¾Ð»ÑÑŽÑ‚ Ñ†ÐµÐ½Ñ‚Ñ€Ð°Ð»Ð¸Ð·Ð¾Ð²Ð°Ð½Ð½Ð¾ Ð¿Ñ€Ð¸Ð¼ÐµÐ½ÑÑ‚ÑŒ Ñ„Ð¸Ð»ÑŒÑ‚Ñ€Ñ‹ (LPF Ð´Ð»Ñ Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ð¾ÑÑ‚Ð¸,
//   distortion Ð´Ð»Ñ Ð¿Ð¾Ð²Ñ€ÐµÐ¶Ð´ÐµÐ½Ð¸Ð¹ ÑˆÐ»ÐµÐ¼Ð°, etc.)
//
// NASA-PUNK ÐšÐžÐÐ¢Ð•ÐšÐ¡Ð¢:
//   PlayStatic2D â€” Ð´Ð»Ñ Ð·Ð²ÑƒÐºÐ¾Ð² Ð²Ð½ÑƒÑ‚Ñ€Ð¸ ÑˆÐ»ÐµÐ¼Ð° ÐºÐ¾ÑÐ¼Ð¾Ð½Ð°Ð²Ñ‚Ð°:
//     â€¢ HUD beeps, suit warnings, radio static, breath sounds.
//     â€¢ Spatial Blend = 0.0 (Ð¿Ð¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ 2D, "Ð² Ð³Ð¾Ð»Ð¾Ð²Ðµ").
//   PlayAtPoint â€” Ð´Ð»Ñ Ð²Ð½ÐµÑˆÐ½Ð¸Ñ… Ð·Ð²ÑƒÐºÐ¾Ð² ÑÑ€ÐµÐ´Ñ‹:
//     â€¢ Bioluminescent creature clicks, hull groans, pressure vents.
//     â€¢ Spatial Blend = 1.0 (Ð¿Ð¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ 3D).
//
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//  ÐœÐÐ Ð¨Ð Ð£Ð¢Ð˜Ð—ÐÐ¦Ð˜Ð¯ (ÐºÐ°ÑÑ‚Ð¾Ð¼Ð½Ñ‹Ð¹ ÐºÐ¾Ð´ Ð² Assets/_Project):
//    â€¢ ÐœÐ¸Ñ€ / Ð¾Ð±ÑŠÐµÐºÑ‚Ñ‹ Ñƒ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ â†’ PlayAtPoint
//    â€¢ Ð¨Ð»ÐµÐ¼ / HUD â†’ PlayStatic2D (Ð¿ÑƒÐ» 2D, Ð½Ðµ Ñ€Ð°Ð·Ð±Ñ€Ð°ÑÑ‹Ð²Ð°Ñ‚ÑŒ PlayOneShot Ð¿Ð¾ MonoBehaviour)
//  ÐŸÐ»Ð°Ð³Ð¸Ð½Ñ‹ Ñ‚Ñ€Ð¾Ð³Ð°ÐµÐ¼ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ Ð¿Ñ€Ð¸ Ð½ÐµÐ¾Ð±Ñ…Ð¾Ð´Ð¸Ð¼Ð¾ÑÑ‚Ð¸.
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
//
// ESTIMATED COST:
//   Memory: ~16 + pool2D AudioSource + manager overhead
//   CPU per Play call: ~0.01ms (array scan + AudioSource setup)
//   CPU idle: 0ms (no Update)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Caves;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Audio
{
    /// <summary>
    /// Ð¦ÐµÐ½Ñ‚Ñ€Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ° Ñ Ð¿ÑƒÐ»Ð¸Ð½Ð³Ð¾Ð¼.
    /// Runtime audio service accessed through Hecton8.Core.GlobalRegistry.Audio.
    /// Zero-GC Ð² hot path. Ð–Ñ‘ÑÑ‚ÐºÐ¸Ð¹ Ð»Ð¸Ð¼Ð¸Ñ‚ Ð¾Ð´Ð½Ð¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ñ… Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð².
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour, IAudioService, IUpdatable, IPhysicsImpactEventListener, IRepairDroneTorchAcousticListener, IFatalPressureImplosionEventListener
    {
        private const float SoundSpeedWaterMetersPerSecond = 1480f;
        private const float SoundSpeedAirMetersPerSecond = 343f;
        private const float ThermalSoundSpeedMinimumMetersPerSecond = 1360f;
        private const float ThermalSoundSpeedMaximumMetersPerSecond = 1565f;
        private const float ThermalSoundSpeedSampleRadiusMeters = 12f;
        private const float ThermalFlowHeatReferenceCelsius = 60f;
        private const float ThermalFlowTemperatureBoostCelsius = 18f;
        private const float ThermalShimmerMaximumPitchRatio = 0.018f;
        private const float HaasArrivalWindowSeconds = 0.035f;
        private const float HaasReleaseThresholdSeconds = 0.04f;
        private const float HaasSecondarySpatialBlendFactor = 0.2f;
        private const float HaasBlendSharpness = 14f;
        private const float Tier0FullDspDistanceMeters = 15f;
        private const float Tier1ReducedDspDistanceMeters = 40f;
        private const float Tier1UpdateIntervalSeconds = 1f / 30f;
        private const float Tier1LowPassCutoffHertz = 1800f;
        private const float VoxelSourceOcclusionUpdateIntervalSeconds = 0.2f;
        private const float StereoPanDistanceNormalizationMeters = 15f;
        private const int MaxImpactRadarEmitters = 16;
        private const float ImpactEmitterLifetimeMinSeconds = 0.18f;
        private const float ImpactEmitterLifetimeMaxSeconds = 0.42f;
        private const float ImpactEmitterAmplitudeScale = 0.75f;
        private const float ImpactEmitterMinimumAmplitude = 0.02f;
        private const float BinauralHeadRadiusMeters = 0.0875f;
        private const int AcousticRadarBinCount = 360;
        private const float AcousticRadarDecayFactorPerSlowTick = 0.75f;
        private const float AcousticRadarDecayIntervalSeconds = 0.1f;
        private const float AcousticRadarDistanceRangeMeters = 180f;
        private const int AcousticRadarGridAzimuthBins = 8;
        private const int AcousticRadarGridElevationBins = 4;
        private const int AcousticRadarGridCellCount = AcousticRadarGridAzimuthBins * AcousticRadarGridElevationBins;
        private const int AcousticRadarNearestEmitterLimit = 12;
        private const float AcousticRadarElevationMinDegrees = -90f;
        private const float AcousticRadarElevationMaxDegrees = 90f;
        private const int MaxListenerContainingCaveVolumes = 8;
        private const float CaveExternalLowPassBoundaryCutoffHertz = 2600f;
        private const float CaveExternalLowPassDeepInteriorCutoffHertz = 1100f;
        private const float CaveInteriorReferenceDistanceMeters = 6f;
        private const float ManualDopplerFollowSharpness = 10f;
        private const float ManualDopplerMaximumRatio = 1.2f;
        private const float ManualDopplerMinimumDenominatorMetersPerSecond = 32f;
        private const float ManualDopplerVelocityJumpThresholdMetersPerSecond = 10f;
        private const float ManualDopplerSmoothingSamples = 128f;
        private const float ManualDopplerSampleRateHertz = 48000f;
        private const float RearHemisphereLowPassStartDot = -0.12f;
        private const float RearHemisphereLowPassFullDot = -0.92f;
        private const float RearHemisphereLowPassMaximumCutoffHertz = 18000f;
        private const float RearHemisphereLowPassMinimumCutoffHertz = 3200f;
        private const float BinauralWaterBlendSharpness = 7f;
        private const float ThreatBusDuckMaximumDb = -12f;
        private const float ThreatBusDuckAttackSeconds = 0.05f;
        private const float ThreatBusDuckReleaseSeconds = 0.3f;
        private const float ParasiteRoomAudioAttackSharpness = 8f;
        private const float ParasiteRoomAudioReleaseSharpness = 3f;
        private const float EclipseAcousticPitchShiftMinCents = -300f;
        private const float EclipseAcousticPitchShiftMaxCents = 0f;

        internal static SpatialAudioManager ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }
        private const int MaxDelayedAudioEvents = 16;
        private const int MaxHarvestAudioEventsPerFrame = 10;
        private const float FatalPressureImplosionEventVolume = 0.96f;
        private const float FatalPressureImplosionEventPitch = 0.84f;
        private const float FatalPressureImplosionTraumaRangeMeters = 220f;
        private const float FatalPressureImplosionTraumaImpulse = 18f;
        private const float FatalPressureImplosionTraumaWeight = 0.82f;

        private enum AudioLodTier : byte
        {
            Tier0Full = 0,
            Tier1Reduced = 1,
            Tier2Culled = 2
        }

        private enum DelayedAudioEventKind : byte
        {
            FatalPressureImplosion = 1,
            InventoryRunawayExplosion = 2
        }

        internal struct ActiveEmitterSample
        {
            public Vector3 Position;
            public float Amplitude;
        }

        internal struct BinauralEmitterTelemetry
        {
            public Vector3 Position;
            public float DistanceMeters;
            public float AzimuthRadians;
            public float ItdSeconds;
            public float ShadowAmount01;
            public float ShadowCutoffHertz;
            public float Energy;
            public float WaterDensityMul;
            public int Valid;
        }

        private struct DelayedAudioEvent
        {
            public DelayedAudioEventKind Kind;
            public Vector3 Position;
            public float EventTimeSeconds;
            public float DelaySeconds;
            public float Volume;
            public float Pitch;
            public float AcousticTransmission01;
            public float LowPassCutoffHz;
            public float ThermalShimmer01;
            public float TraumaRangeMeters;
            public float TraumaImpulse;
            public float TraumaWeight;
        }

        private struct ImpactEmitterSample
        {
            public Vector3 Position;
            public float Amplitude;
            public float SpawnAt;
            public float ExpireAt;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  SERVICE REGISTRY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð“Ð»Ð¾Ð±Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð´Ð¾ÑÑ‚ÑƒÐ¿ Ðº Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€Ñƒ. ÐÐµ ÑÐ¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð¾Ð±ÑŠÐµÐºÑ‚ Ð°Ð²Ñ‚Ð¾Ð¼Ð°Ñ‚Ð¸Ñ‡ÐµÑÐºÐ¸ â€”
        /// Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ñ€Ð°Ð·Ð¼ÐµÑ‰Ñ‘Ð½ Ð½Ð° ÑÑ†ÐµÐ½Ðµ Ð²Ñ€ÑƒÑ‡Ð½ÑƒÑŽ Ð¸Ð»Ð¸ Ñ‡ÐµÑ€ÐµÐ· bootstrap.
        /// </summary>
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        //  INSPECTOR CONFIGURATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        [Header("Pool Configuration â€” 3D World")]
        [Tooltip("ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ AudioSource Ð² Ð¿ÑƒÐ»Ðµ. 16 Ð¾Ð¿Ñ‚Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð¾ Ð´Ð»Ñ MX350. Max 32.")]
        [Range(4, 32)]
        [SerializeField] private int _poolSize = 16;

        [Header("Pool Configuration â€” 2D Helmet / UI")]
        [Tooltip("Ð“Ð¾Ð»Ð¾ÑÐ° Ð´Ð»Ñ ÐºÐ¾Ñ€Ð¾Ñ‚ÐºÐ¸Ñ… UI/ÑˆÐ»ÐµÐ¼Ð½Ñ‹Ñ… Ð·Ð²ÑƒÐºÐ¾Ð²; Ð¿ÐµÑ€ÐµÐºÑ€Ñ‹Ñ‚Ð¸Ðµ Ñ‡ÐµÑ€ÐµÐ· Ð²Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ðµ.")]
        [Range(2, 16)]
        [SerializeField] private int _pool2DSize = 8;

        [Header("3D Audio Defaults")]
        [Tooltip("ÐœÐ¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ñ 3D Ð·Ð²ÑƒÐºÐ° (Ð¼ÐµÑ‚Ñ€Ñ‹).")]
        [SerializeField] private float _minDistance = 1f;

        [Tooltip("ÐœÐ°ÐºÑÐ¸Ð¼Ð°Ð»ÑŒÐ½Ð°Ñ Ð´Ð¸ÑÑ‚Ð°Ð½Ñ†Ð¸Ñ 3D Ð·Ð²ÑƒÐºÐ° (Ð¼ÐµÑ‚Ñ€Ñ‹). Ð—Ð° Ð½ÐµÐ¹ Ð·Ð²ÑƒÐº Ð½Ðµ ÑÐ»Ñ‹ÑˆÐµÐ½.")]
        [SerializeField] private float _maxDistance = 50f;

        [Header("Mixer Groups (Ð½Ð°Ð·Ð½Ð°Ñ‡Ð¸Ñ‚ÑŒ Ð¸Ð· AudioMixer)")]
        [Tooltip("Ð“Ñ€ÑƒÐ¿Ð¿Ð° Ð´Ð»Ñ SFX (ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°, Ð¼ÐµÑ…Ð°Ð½Ð¸Ð·Ð¼Ñ‹, Ð¾ÐºÑ€ÑƒÐ¶ÐµÐ½Ð¸Ðµ).")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("Ð“Ñ€ÑƒÐ¿Ð¿Ð° Ð´Ð»Ñ Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹ÑÐ° Ð¸ Ð·Ð²ÑƒÐºÐ¾Ð² Ð²Ð½ÑƒÑ‚Ñ€Ð¸ ÑˆÐ»ÐµÐ¼Ð°.")]
        [SerializeField] private AudioMixerGroup _interfaceGroup;

        [Tooltip("Ð“Ñ€ÑƒÐ¿Ð¿Ð° Ð´Ð»Ñ ÑÐ¼Ð±Ð¸ÐµÐ½Ñ‚Ð° (Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ñ‹Ð¹ Ð³ÑƒÐ», Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ, etc).")]
        [SerializeField] private AudioMixerGroup _ambientGroup;

        [Tooltip("Threat bus for dominant hostile cues such as leviathan roars. Falls back to SFX when unassigned.")]
        [SerializeField] private AudioMixerGroup _threatGroup;

        [Tooltip("Bed bus for ambient world layers that should duck under threat activity. Falls back to Ambient when unassigned.")]
        [SerializeField] private AudioMixerGroup _bedGroup;

        [Tooltip("Optional mixer override for threat-driven bed ducking. If null, the bed or ambient mixer is used.")]
        [SerializeField] private AudioMixer _routingMixer;

        [Tooltip("Exposed mixer parameter that attenuates the Bed bus in dB while Threat is active.")]
        [SerializeField] private string _bedDuckDbParameter = "BedDuckDb";

        [Tooltip("Exposed mixer parameter for room low-pass cutoff while parasite growth is active.")]
        [SerializeField] private string _parasiteRoomLowPassCutoffParameter = "ParasiteRoomLowPassCutoffHz";

        [Tooltip("Exposed mixer parameter for the organic squelch ambient layer gain in dB.")]
        [SerializeField] private string _parasiteOrganicLayerGainParameter = "ParasiteOrganicLayerGainDb";

        [Tooltip("Low-pass cutoff for a clean powered room.")]
        [SerializeField, Range(1000f, 22000f)] private float _parasiteRoomHealthyCutoffHz = 18000f;

        [Tooltip("Low-pass cutoff for a fully overgrown room.")]
        [SerializeField, Range(250f, 8000f)] private float _parasiteRoomInfectedCutoffHz = 1400f;

        [Tooltip("Organic squelch gain while no module parasites are present.")]
        [SerializeField, Range(-80f, 0f)] private float _parasiteOrganicLayerSilentDb = -80f;

        [Tooltip("Organic squelch gain at maximum parasite room load.")]
        [SerializeField, Range(-40f, 0f)] private float _parasiteOrganicLayerMaxDb = -9f;

        [Tooltip("Parasite count that maps to full sick-room acoustic intensity.")]
        [SerializeField, Range(1, 16)] private int _parasiteRoomCountForFullInfection = 8;

        [Header("Delayed World Events")]
        [Tooltip("Threat-bus implosion clip fired after underwater propagation delay. Left null to keep the event trauma-only.")]
        [SerializeField] private AudioClip _fatalPressureImplosionClip;

        [Tooltip("Muffled inventory runaway blast clip fired through the delayed underwater event path. Left null to keep damage-only.")]
        [SerializeField] private AudioClip _inventoryRunawayExplosionClip;

        [Header("Authored Pool Roots")]
        [Tooltip("Pre-authored root containing world-space AudioSource + AudioLowPassFilter pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _worldPoolRoot;

        [Tooltip("Pre-authored root containing 2D helmet/UI AudioSource pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _helmetPoolRoot;

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  POOL DATA â€” Fixed arrays, zero allocation
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>ÐŸÑƒÐ» AudioSource ÐºÐ¾Ð¼Ð¿Ð¾Ð½ÐµÐ½Ñ‚Ð¾Ð². Ð Ð°Ð·Ð¼ÐµÑ€ Ñ„Ð¸ÐºÑÐ¸Ñ€Ð¾Ð²Ð°Ð½ Ð¿Ð¾ÑÐ»Ðµ Awake.</summary>
        private AudioSource[] _pool;

        /// <summary>Ð’Ñ€ÐµÐ¼Ñ Ð½Ð°Ñ‡Ð°Ð»Ð° Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ° (Time.unscaledTime).
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ÑÑ Ð´Ð»Ñ Ð²Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ñ ÑÐ°Ð¼Ð¾Ð³Ð¾ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ°.</summary>
        private float[] _startTimes;

        /// <summary>ÐŸÑƒÐ» 2D AudioSource (spatialBlend = 0).</summary>
        private AudioSource[] _pool2D;

        /// <summary>Ð’Ñ€ÐµÐ¼Ñ ÑÑ‚Ð°Ñ€Ñ‚Ð° Ð´Ð»Ñ Ð²Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ñ Ð² 2D-Ð¿ÑƒÐ»Ðµ.</summary>
        private float[] _startTimes2D;
        private float[] _baseVolumes;
        private float[] _basePitches;
        private float[] _sourceVoxelLowPassCutoffs;
        private float[] _sourceVoxelTransmissions;
        private float[] _sourceVoxelNextUpdateTimes;
        private float[] _smoothedDopplerRatios;
        private float[] _previousRelativeVelocities;
        private float[] _arrivalTimes;
        private float[] _haasReleaseTimes;
        private float[] _nextTierUpdateTimes;
        private int _harvestAudioFrame = -1;
        private int _harvestAudioEventsThisFrame;
        private AudioLodTier[] _audioLodTiers;
        private AudioLowPassFilter[] _lowPassFilters;
        private Vector3[] _previousAbsolutePositions;
        private Vector3[] _currentAbsoluteVelocities;
        private int[] _activeWorldIndices;
        private int[] _activeWorldSlots;
        private int _activeWorldCount;
        private bool _registeredUpdatable;
        private Transform _listenerTransform;
        private Vector3 _previousListenerAbsolutePosition;
        private bool _hasPreviousListenerAbsolutePosition;
        private BinauralEmitterTelemetry _dominantBinauralEmitter;
        private NativeArray<float> _acousticRadarIntensityBins;
        private NativeArray<float> _acousticRadarGrid;
        private WorldCaveDirector _worldCaveDirector;
        private ComputeBuffer _acousticRadarGridBuffer;
        // COLD ALLOC: float[32] - CPU mirror for acoustic radar grid ComputeBuffer uploads - owner: SpatialAudioManager
        private float[] _acousticRadarGridUploadScratch;
        // COLD ALLOC: Vector3[12] - nearest-emitter radar accumulation positions - owner: SpatialAudioManager
        private Vector3[] _radarNearestEmitterPositions;
        // COLD ALLOC: float[12] - nearest-emitter radar accumulation amplitudes - owner: SpatialAudioManager
        private float[] _radarNearestEmitterAmplitudes;
        // COLD ALLOC: float[12] - nearest-emitter radar accumulation distance cache - owner: SpatialAudioManager
        private float[] _radarNearestEmitterDistanceSq;
        // COLD ALLOC: Transform[12] - nearest-emitter radar accumulation source roots for cached occlusion lookups - owner: SpatialAudioManager
        private Transform[] _radarNearestEmitterRoots;
        private int _resolvedAcousticOcclusionLayerMask;
        // COLD ALLOC: List<HectonVoxelVolume>[32] - active cave-volume cache reused for cave-aware audio filtering - owner: SpatialAudioManager
        private readonly List<HectonVoxelVolume> _caveVolumeBuffer = new List<HectonVoxelVolume>(32);
        // COLD ALLOC: HectonVoxelVolume[8] - listener-containing cave volumes for external ambient filtering - owner: SpatialAudioManager
        private readonly HectonVoxelVolume[] _listenerContainingCaveVolumes = new HectonVoxelVolume[MaxListenerContainingCaveVolumes];
        private int _listenerContainingCaveCount;
        private float _listenerCaveInterior01;
        private float _threatBusDuck01;
        private float _parasiteRoomTarget01;
        private float _parasiteRoomSmoothed01;
        private float _lastParasiteRoomLowPassCutoffHz = -1f;
        private float _lastParasiteOrganicLayerGainDb = float.PositiveInfinity;
        private int _parasiteRoomAcousticCount;
        private float _eclipseAcousticPitchShiftCents;
        private float _eclipseAcousticPitchRatio = 1f;
        private float _listenerWaterDensityMul;
        private float _radarDecayAccumulator;
        private HectonPlayerMovement _listenerPlayerMovement;
        private int _delayedAudioIngressCount;
        private NativeQueue<DelayedAudioEvent> _delayedAudioIngress;
        private NativeList<DelayedAudioEvent> _pendingDelayedAudioEvents;
        private bool _isInitialized;
        private bool _runtimeResourcesInitialized;
        private bool _eventsSubscribed;
        // COLD ALLOC: ImpactEmitterSample[16] - deferred physics-impact telemetry for passive radar/UI only; audible impact stress is owned by PlayerCriticalProceduralAudioRenderer's SPSC queue - owner: SpatialAudioManager
        private readonly ImpactEmitterSample[] _impactEmitters = new ImpactEmitterSample[MaxImpactRadarEmitters];

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            // Self-state only. Runtime resources are allocated by explicit bootstrap registration.
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;

            if (_isInitialized)
                TrySubscribeAudioEvents();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnsubscribeAudioEvents();
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterAudioService(this);
                _isInitialized = false;
            }

            if (_registeredUpdatable)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registeredUpdatable = false;
            _hasPreviousListenerAbsolutePosition = false;
            _previousListenerAbsolutePosition = default;
            ResetAllWorldSourceState();
            ResetImpactEmitters();
            ResetAcousticRadarBins();
            ResetAcousticRadarGrid();
            ResetListenerCaveState();
            ClearDelayedAudioEvents();
            _listenerPlayerMovement = null;
            _listenerWaterDensityMul = 0f;
            SetParasiteRoomAcousticLoad(0);
            SetEclipseAcousticPitchShiftCents(0f);
            ApplyThreatBusDucking(0f, 0f);
            ApplyParasiteRoomAcousticState(0f);
            _radarDecayAccumulator = 0f;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            ReleaseTelemetryCaches();
        }

        /// <summary>
        /// True once the audio runtime has been registered into the global service locator.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <summary>
        /// Current eclipse-driven pitch shift applied to ambient bed/drone world sources.
        /// </summary>
        public float EclipseAcousticPitchShiftCents => _eclipseAcousticPitchShiftCents;

        /// <summary>
        /// Current pitch ratio derived from <see cref="EclipseAcousticPitchShiftCents"/>.
        /// </summary>
        public float EclipseAcousticPitchRatio => _eclipseAcousticPitchRatio;

        /// <summary>
        /// Sets the eclipse pitch scalar in cents. Negative values lower ambient bed/drone sources.
        /// </summary>
        public void SetEclipseAcousticPitchShiftCents(float shiftCents)
        {
            float clampedCents = math.clamp(
                shiftCents,
                EclipseAcousticPitchShiftMinCents,
                EclipseAcousticPitchShiftMaxCents);
            if (math.abs(clampedCents - _eclipseAcousticPitchShiftCents) <= 0.01f)
                return;

            _eclipseAcousticPitchShiftCents = clampedCents;
            _eclipseAcousticPitchRatio = math.pow(2f, clampedCents / 1200f);
            ApplyEclipsePitchShiftToActiveWorldSources();
        }

        /// <summary>
        /// Registers the audio runtime into <see cref="GlobalRegistry"/> and the environment update bucket.
        /// </summary>
        public void InitializeService()
        {
            EnsureRuntimeResourcesInitialized();
            TrySubscribeAudioEvents();

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                return;
            }

            GlobalRegistry.RegisterAudioService(this);
            TryRegisterUpdatable();
            _isInitialized = true;
        }

        private void EnsureRuntimeResourcesInitialized()
        {
            if (_runtimeResourcesInitialized)
                return;

            InitializePool();
            InitializePool2D();
            InitializeTelemetryCaches();
            _runtimeResourcesInitialized = true;
        }

        private void TrySubscribeAudioEvents()
        {
            if (_eventsSubscribed)
                return;

            PhysicsEvents.Register(this);
            FatalPressureImplosionEvents.Register(this);
            RepairDroneTorchAcousticEvents.Register(this);
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeAudioEvents()
        {
            if (!_eventsSubscribed)
                return;

            PhysicsEvents.Unregister(this);
            FatalPressureImplosionEvents.Unregister(this);
            RepairDroneTorchAcousticEvents.Unregister(this);
            _eventsSubscribed = false;
        }

        /// <summary>
        /// Restores temporary Haas masking on clustered arrivals.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time.</param>
        public void Tick(float deltaTime)
        {
            if (_pool == null || _arrivalTimes == null || _haasReleaseTimes == null)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            float blendT = 1f - math.exp(-math.max(HaasBlendSharpness, 0.01f) * safeDeltaTime);
            float now = Time.unscaledTime;
            Transform listener = ResolveListenerTransform();
            Vector3 listenerAbsolutePosition = listener != null
                ? HectonFloatingOrigin.ToAbsoluteUniversePosition(listener.position)
                : default;
            Vector3 listenerVelocity = ResolveListenerAbsoluteVelocity(listenerAbsolutePosition, safeDeltaTime);
            UpdateListenerWaterDensityMul(safeDeltaTime);
            float threatActivity = 0f;
            DecayImpactEmitters(now);
            AdvanceAcousticRadarDecayCadence(safeDeltaTime);
            RefreshListenerCaveState(listener);
            ResetNearestRadarEmitterScratch();
            DrainDelayedAudioIngress();
            ProcessDelayedAudioEvents(listenerAbsolutePosition);
            int activeSlot = 0;
            while (activeSlot < _activeWorldCount)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                UpdateWorldSourceAudioLod(sourceIndex, source, now, false);
                if (listener != null)
                    UpdateManualDopplerPitch(sourceIndex, source, listenerAbsolutePosition, listenerVelocity, safeDeltaTime);
                if (!source.isPlaying)
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                float targetBlend = ResolveTargetSpatialBlend(sourceIndex, now);
                source.spatialBlend = math.lerp(source.spatialBlend, targetBlend, blendT);
                if (_haasReleaseTimes[sourceIndex] <= now && source.spatialBlend >= targetBlend - 0.001f)
                    _haasReleaseTimes[sourceIndex] = 0f;

                if (IsThreatWorldSource(source))
                    threatActivity = math.max(threatActivity, math.saturate(source.volume));
                float sourceAmplitude = math.max(0f, source.volume);
                DepositAcousticRadarSample(listener, source.transform.position, sourceAmplitude);
                if (listener != null)
                    QueueNearestRadarEmitter(listenerAbsolutePosition, listener.position, source.transform.position, sourceAmplitude, source.transform.root);
                activeSlot++;
            }

            DepositImpactRadarSamples(listener, now);
            if (listener != null)
            {
                QueueImpactRadarEmitters(listenerAbsolutePosition, listener.position, now);
                AccumulateNearestRadarGrid(listener);
            }
            UploadAcousticRadarGridBuffer();
            UpdateDominantBinauralEmitterTelemetry(now, listener);
            ApplyThreatBusDucking(threatActivity, safeDeltaTime);
            ApplyParasiteRoomAcousticState(safeDeltaTime);
        }

        /// <summary>
        /// Publishes the current parasite load of the occupied module into mixer-level room filtering.
        /// </summary>
        /// <param name="parasiteCount">Attached parasite count for the player-occupied module.</param>
        public void SetParasiteRoomAcousticLoad(int parasiteCount)
        {
            int sanitizedCount = math.max(0, parasiteCount);
            if (_parasiteRoomAcousticCount == sanitizedCount)
                return;

            _parasiteRoomAcousticCount = sanitizedCount;
            _parasiteRoomTarget01 = math.saturate(sanitizedCount / (float)math.max(1, _parasiteRoomCountForFullInfection));
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  POOL INITIALIZATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð¿ÑƒÐ» AudioSource ÐºÐ°Ðº Ð´Ð¾Ñ‡ÐµÑ€Ð½Ð¸Ðµ Ð¾Ð±ÑŠÐµÐºÑ‚Ñ‹.
        /// Ð’Ñ‹Ð·Ñ‹Ð²Ð°ÐµÑ‚ÑÑ Ð¾Ð´Ð¸Ð½ Ñ€Ð°Ð· Ð² Awake. ÐÐ¸ÐºÐ°ÐºÐ¸Ñ… Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ð¹ Ð¿Ð¾ÑÐ»Ðµ ÑÑ‚Ð¾Ð³Ð¾.
        /// </summary>
        private void InitializePool()
        {
            int effectivePoolSize = math.min(_poolSize, CountAuthoredWorldPoolNodes(ResolveWorldPoolRoot()));
            if (effectivePoolSize < _poolSize)
            {
#if UNITY_EDITOR
                Debug.LogErrorFormat(
                    this,
                    "[SpatialAudioManager] World pool requested {0} authored nodes, found {1}. Assign pre-authored AudioSource + AudioLowPassFilter children before play.",
                    _poolSize,
                    effectivePoolSize);
#endif
            }

            _poolSize = effectivePoolSize;
            _pool = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];
            _baseVolumes = new float[_poolSize];
            _basePitches = new float[_poolSize];
            _sourceVoxelLowPassCutoffs = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source voxel occlusion LPF cache - owner: SpatialAudioManager
            _sourceVoxelTransmissions = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source voxel transmission cache - owner: SpatialAudioManager
            _sourceVoxelNextUpdateTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - throttled voxel occlusion refresh cadence - owner: SpatialAudioManager
            _smoothedDopplerRatios = new float[_poolSize];
            _previousRelativeVelocities = new float[_poolSize];
            _arrivalTimes = new float[_poolSize];
            _haasReleaseTimes = new float[_poolSize];
            _nextTierUpdateTimes = new float[_poolSize];
            _audioLodTiers = new AudioLodTier[_poolSize];
            _lowPassFilters = new AudioLowPassFilter[_poolSize];
            _previousAbsolutePositions = new Vector3[_poolSize];
            _currentAbsoluteVelocities = new Vector3[_poolSize];
            _activeWorldIndices = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse active world-source set - owner: SpatialAudioManager
            _activeWorldSlots = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse world-source slot lookup - owner: SpatialAudioManager
            _activeWorldCount = 0;
            for (int i = 0; i < _poolSize; i++)
            {
                _activeWorldIndices[i] = -1;
                _activeWorldSlots[i] = -1;
                _basePitches[i] = 1f;
                _sourceVoxelLowPassCutoffs[i] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
                _sourceVoxelTransmissions[i] = 1f;
                _sourceVoxelNextUpdateTimes[i] = 0f;
                _smoothedDopplerRatios[i] = 1f;
                _previousRelativeVelocities[i] = 0f;
            }

            if (_poolSize > 0)
            {
                int boundCount = 0;
                BindAuthoredWorldPoolRecursive(ResolveWorldPoolRoot(), ref boundCount);
            }

            return;
#if false

            _pool = new AudioSource[_poolSize];
            _startTimes = new float[_poolSize];
            _baseVolumes = new float[_poolSize];
            _arrivalTimes = new float[_poolSize];
            _haasReleaseTimes = new float[_poolSize];
            _nextTierUpdateTimes = new float[_poolSize];
            _audioLodTiers = new AudioLodTier[_poolSize];
            _lowPassFilters = new AudioLowPassFilter[_poolSize];

            for (int i = 0; i < _poolSize; i++)
            {
                // Ð”Ð¾Ñ‡ÐµÑ€Ð½Ð¸Ð¹ GameObject Ð´Ð»Ñ ÐºÐ°Ð¶Ð´Ð¾Ð³Ð¾ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ°
                GameObject child = null;
                child.transform.SetParent(transform, false);

                AudioSource source = null;
                AudioLowPassFilter lowPassFilter = null;
                ConfigureAs3D(source);
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;

                source.playOnAwake = false;
                source.loop = false;

                _pool[i] = source;
                _lowPassFilters[i] = lowPassFilter;
                _startTimes[i] = -1f; // Not playing
                _baseVolumes[i] = 0f;
                _arrivalTimes[i] = -1f;
                _haasReleaseTimes[i] = 0f;
                _nextTierUpdateTimes[i] = 0f;
                _audioLodTiers[i] = AudioLodTier.Tier0Full;
            }
#endif
        }

        /// <summary>Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð¿ÑƒÐ» 2D Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð² (Ð°Ð½Ð°Ð»Ð¾Ð³Ð¸Ñ‡Ð½Ð¾ 3D, Ð±ÐµÐ· PlayOneShot).</summary>
        private void InitializePool2D()
        {
            int effectivePool2DSize = math.min(_pool2DSize, CountAuthoredHelmetPoolNodes(ResolveHelmetPoolRoot()));
            if (effectivePool2DSize < _pool2DSize)
            {
#if UNITY_EDITOR
                Debug.LogErrorFormat(
                    this,
                    "[SpatialAudioManager] Helmet/UI pool requested {0} authored nodes, found {1}. Assign pre-authored 2D AudioSource children before play.",
                    _pool2DSize,
                    effectivePool2DSize);
#endif
            }

            _pool2DSize = effectivePool2DSize;
            _pool2D = new AudioSource[_pool2DSize];
            _startTimes2D = new float[_pool2DSize];

            if (_pool2DSize > 0)
            {
                int boundCount = 0;
                BindAuthoredHelmetPoolRecursive(ResolveHelmetPoolRoot(), ref boundCount);
            }

            return;
#if false

            _pool2D = new AudioSource[_pool2DSize];
            _startTimes2D = new float[_pool2DSize];

            for (int i = 0; i < _pool2DSize; i++)
            {
                GameObject child = null;
                child.transform.SetParent(transform, false);

                AudioSource source = null;
                ConfigureAs2D(source);

                source.playOnAwake = false;
                source.loop = false;

                _pool2D[i] = source;
                _startTimes2D[i] = -1f;
            }
#endif
        }

        private void ConfigureAs2D(AudioSource source)
        {
            source.spatialBlend = 0f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;

            if (_interfaceGroup != null)
            {
                source.outputAudioMixerGroup = _interfaceGroup;
            }
        }

        /// <summary>
        /// ÐÐ°ÑÑ‚Ñ€Ð°Ð¸Ð²Ð°ÐµÑ‚ AudioSource ÐºÐ°Ðº 3D Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸Ðº Ñ Linear Rolloff.
        /// Linear Rolloff Ð´ÐµÑˆÐµÐ²Ð»Ðµ Logarithmic Ð¸ Ð¿Ñ€ÐµÐ´ÑÐºÐ°Ð·ÑƒÐµÐ¼ÐµÐµ Ð´Ð»Ñ Ð³ÐµÐ¹Ð¼Ð´Ð¸Ð·Ð°Ð¹Ð½Ð°.
        /// </summary>
        private void ConfigureAs3D(AudioSource source)
        {
            source.spatialBlend = 1f;          // ÐŸÐ¾Ð»Ð½Ð¾ÑÑ‚ÑŒÑŽ 3D
            source.spread = 0f;                // Ð¢Ð¾Ñ‡ÐµÑ‡Ð½Ñ‹Ð¹ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸Ðº
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = _minDistance;
            source.maxDistance = _maxDistance;
            source.dopplerLevel = 0f;          // ÐžÑ‚ÐºÐ»ÑŽÑ‡Ð°ÐµÐ¼ Doppler â€” Ð´ÐµÑˆÐµÐ²Ð»Ðµ Ð¸ Ð½ÐµÑ‚ Ð°Ñ€Ñ‚ÐµÑ„Ð°ÐºÑ‚Ð¾Ð²

            source.outputAudioMixerGroup = ResolvedDefaultWorldMixerGroup;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” 3D SPATIAL AUDIO
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¾Ð¸Ð³Ñ€Ñ‹Ð²Ð°ÐµÑ‚ 3D Ð·Ð²ÑƒÐº Ð² ÑƒÐºÐ°Ð·Ð°Ð½Ð½Ð¾Ð¹ Ð¼Ð¸Ñ€Ð¾Ð²Ð¾Ð¹ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ SFX mixer group Ð¿Ð¾ ÑƒÐ¼Ð¾Ð»Ñ‡Ð°Ð½Ð¸ÑŽ.
        ///
        /// Ð›Ð¾Ð³Ð¸ÐºÐ° Ð¿ÑƒÐ»Ð°:
        ///   1. Ð˜Ñ‰ÐµÑ‚ Ð¿ÐµÑ€Ð²Ñ‹Ð¹ ÑÐ²Ð¾Ð±Ð¾Ð´Ð½Ñ‹Ð¹ (!isPlaying) Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸Ðº â€” O(n), n â‰¤ 32.
        ///   2. Ð•ÑÐ»Ð¸ Ð²ÑÐµ Ð·Ð°Ð½ÑÑ‚Ñ‹ â€” Ð²Ñ‹Ñ‚ÐµÑÐ½ÑÐµÑ‚ ÑÐ°Ð¼Ñ‹Ð¹ ÑÑ‚Ð°Ñ€Ñ‹Ð¹ (lowest startTime).
        ///   3. Zero-GC: Ñ‚Ð¾Ð»ÑŒÐºÐ¾ array traversal, Ð½Ð¸ÐºÐ°ÐºÐ¸Ñ… Ð°Ð»Ð»Ð¾ÐºÐ°Ñ†Ð¸Ð¹.
        ///
        /// Ð’Ñ‹Ð·Ð¾Ð²: Hecton8.Core.GlobalRegistry.Audio.PlayAtPoint(clip, transform.position);
        /// </summary>
        /// <param name="clip">AudioClip Ð´Ð»Ñ Ð²Ð¾ÑÐ¿Ñ€Ð¾Ð¸Ð·Ð²ÐµÐ´ÐµÐ½Ð¸Ñ. Null-safe.</param>
        /// <param name="position">ÐœÐ¸Ñ€Ð¾Ð²Ð°Ñ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ñ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ° Ð·Ð²ÑƒÐºÐ°.</param>
        /// <param name="volume">Ð“Ñ€Ð¾Ð¼ÐºÐ¾ÑÑ‚ÑŒ [0..1]. Default = 1.</param>
        /// <param name="pitch">Pitch [0.1..3]. Default = 1. Ð Ð°Ð½Ð´Ð¾Ð¼Ð¸Ð·Ð¸Ñ€Ð¾Ð²Ð°Ñ‚ÑŒ Ð´Ð»Ñ Ð²Ð°Ñ€Ð¸Ð°Ñ‚Ð¸Ð²Ð½Ð¾ÑÑ‚Ð¸.</param>
        public void PlayAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayAtPoint(clip, position, volume, pitch, ResolvedDefaultWorldMixerGroup);
        }

        /// <summary>
        /// Routes a dominant hostile cue through the threat bus so ambient bed content ducks under it.
        /// </summary>
        public void PlayThreatAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayAtPoint(clip, position, volume, pitch, ResolvedThreatBusGroup);
        }

        /// <summary>
        /// Routes ambient world-bed content through the bed bus.
        /// </summary>
        public void PlayBedAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            PlayAtPoint(clip, position, volume, pitch, ResolvedBedBusGroup);
        }

        /// <summary>
        /// Routes meteor-shower flash energy into the procedural low-frequency boom path.
        /// </summary>
        public void PlayMeteorShowerBoom(Vector3 position, float intensity01, float lowPassCutoffHz)
        {
            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0.001f)
                return;

            ProceduralAudioEvents.RaiseAudioPingTriggered(
                position,
                clampedIntensity,
                1.15f,
                1f,
                math.clamp(lowPassCutoffHz, 80f, 800f),
                ProceduralAudioPingKind.MeteorBoom);
        }

        internal void PlayHarvestAtAup(in AbsoluteUniversePosition positionAup, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null)
                return;

            if (!TryReserveHarvestAudioEvent())
                return;

            float3 runtimePosition = positionAup.ToRuntimeFloat3();
            TryPlayAtPointWithoutEviction(
                clip,
                new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                volume,
                pitch,
                ResolvedDefaultWorldMixerGroup);
        }

        internal void PlaySporeEmissionAtAup(
            in AbsoluteUniversePosition positionAup,
            AudioClip clip,
            float pulseFrequencyHz,
            float simulationTimeSeconds,
            float phaseOffset01,
            float volume = 1f)
        {
            if (clip == null)
                return;

            float3 runtimePosition = positionAup.ToRuntimeFloat3();
            float safePulseFrequency = math.max(0.01f, pulseFrequencyHz);
            float shaderPhase01 = math.frac(simulationTimeSeconds * safePulseFrequency + phaseOffset01);
            float peakSyncEnvelope = math.saturate(1f - math.abs(shaderPhase01 - 0.25f) * 4f);
            float pitch = math.clamp(safePulseFrequency, 0.1f, 3f);
            PlayAtPoint(
                clip,
                new Vector3(runtimePosition.x, runtimePosition.y, runtimePosition.z),
                volume * math.max(0.65f, peakSyncEnvelope),
                pitch,
                ResolvedThreatBusGroup);
        }

        /// <summary>
        /// ÐŸÑ€Ð¾Ð¸Ð³Ñ€Ñ‹Ð²Ð°ÐµÑ‚ 3D Ð·Ð²ÑƒÐº Ñ ÑÐ²Ð½Ñ‹Ð¼ ÑƒÐºÐ°Ð·Ð°Ð½Ð¸ÐµÐ¼ AudioMixerGroup.
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐ¹Ñ‚Ðµ Ð´Ð»Ñ ambient Ð·Ð²ÑƒÐºÐ¾Ð²: PlayAtPoint(clip, pos, 1f, 1f, ambientGroup).
        /// </summary>
        public void PlayAtPoint(
            AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SpatialAudioManager] PlayAtPoint called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            AudioLodTier lodTier = ResolveAudioLodTier(position);
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;

            // â”€â”€ ÐŸÐ¾Ð·Ð¸Ñ†Ð¸Ð¾Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ â”€â”€
            source.transform.position = position;

            // â”€â”€ ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° â”€â”€
            source.clip = clip;
            source.volume = volume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = volume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            source.pitch = ResolveSourcePitch(index, source, 1f);
            _audioLodTiers[index] = lodTier;
            UpdateWorldSourceAudioLod(index, source, Time.unscaledTime, true);
            ApplyHaasMask(index, position);
            source.spatialBlend = ResolveTargetSpatialBlend(index, Time.unscaledTime);

            // â”€â”€ Ð—Ð°Ð¿ÑƒÑÐº â”€â”€
            source.Play();
            _startTimes[index] = Time.unscaledTime;
            MarkWorldSourceActive(index);
        }

        /// <summary>
        /// Plays one world-space clip with an explicit acoustic low-pass cutoff resolved by the caller.
        /// </summary>
        public void PlayAtPointWithLowPass(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            float lowPassCutoffHz)
        {
            if (clip == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SpatialAudioManager] PlayAtPointWithLowPass called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            AudioLodTier lodTier = ResolveAudioLodTier(position);
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = volume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            source.pitch = ResolveSourcePitch(index, source, 1f);
            _audioLodTiers[index] = lodTier;
            float now = Time.unscaledTime;
            UpdateWorldSourceAudioLod(index, source, now, true);
            float cutoff = math.clamp(
                lowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (_sourceVoxelLowPassCutoffs != null && index >= 0 && index < _sourceVoxelLowPassCutoffs.Length)
                cutoff = math.min(cutoff, _sourceVoxelLowPassCutoffs[index]);
            if (cutoff < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f)
                ApplyLowPassFilter(index, true, cutoff);

            ApplyHaasMask(index, position);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);
            source.Play();
            _startTimes[index] = now;
            MarkWorldSourceActive(index);
        }

        /// <summary>
        /// Emits a sandboxed mod acoustic ping into passive radar and fauna hearing paths.
        /// The owning audio system converts it into engine-native sensory events only.
        /// </summary>
        /// <param name="runtimePosition">Frame-space ping origin.</param>
        /// <param name="intensity01">Normalized ping intensity.</param>
        /// <returns>True when the ping entered the sensory path.</returns>
        public bool TryEmitModAcousticPing(Vector3 runtimePosition, float intensity01)
        {
            float amplitude = math.saturate(intensity01);
            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return false;

            if (!TryQueueImpactRadarEmitter(runtimePosition, amplitude, amplitude))
                return false;

            NoiseSystem.ReportActiveSonarPing(runtimePosition, amplitude);
            return true;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” 2D STATIC AUDIO (SUIT / HELMET / HUD)
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐŸÑ€Ð¾Ð¸Ð³Ñ€Ñ‹Ð²Ð°ÐµÑ‚ 2D Ð·Ð²ÑƒÐº Ð±ÐµÐ· Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ð³Ð¾ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¾Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ñ.
        /// Ð”Ð»Ñ Ð·Ð²ÑƒÐºÐ¾Ð² Ð²Ð½ÑƒÑ‚Ñ€Ð¸ ÑˆÐ»ÐµÐ¼Ð°: HUD beeps, suit warnings, radio static,
        /// breath sounds, system alerts.
        ///
        /// Ð˜ÑÐ¿Ð¾Ð»ÑŒÐ·ÑƒÐµÑ‚ Ð¿ÑƒÐ» 2D-Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð² â€” Ð½ÐµÑÐºÐ¾Ð»ÑŒÐºÐ¾ ÐºÐ¾Ñ€Ð¾Ñ‚ÐºÐ¸Ñ… ÑÐ¸Ð³Ð½Ð°Ð»Ð¾Ð² Ð¼Ð¾Ð³ÑƒÑ‚ Ð¸Ð³Ñ€Ð°Ñ‚ÑŒ
        /// Ð¿Ð°Ñ€Ð°Ð»Ð»ÐµÐ»ÑŒÐ½Ð¾ Ð´Ð¾ Ð¸ÑÑ‡ÐµÑ€Ð¿Ð°Ð½Ð¸Ñ Ð¿ÑƒÐ»Ð°; Ð´Ð°Ð»ÑŒÑˆÐµ â€” Ð²Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ðµ Ð¿Ð¾ Ð²Ñ€ÐµÐ¼ÐµÐ½Ð¸.
        ///
        /// Ð’Ñ‹Ð·Ð¾Ð²: Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(beepClip, 0.5f);
        /// </summary>
        /// <param name="clip">AudioClip. Null-safe.</param>
        /// <param name="volume">Ð“Ñ€Ð¾Ð¼ÐºÐ¾ÑÑ‚ÑŒ [0..1]. Default = 1.</param>
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
            PlayStatic2D(clip, volume, _interfaceGroup);
        }

        /// <summary>
        /// ÐŸÑ€Ð¾Ð¸Ð³Ñ€Ñ‹Ð²Ð°ÐµÑ‚ 2D Ð·Ð²ÑƒÐº Ñ ÑÐ²Ð½Ð¾Ð¹ AudioMixerGroup.
        /// </summary>
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SpatialAudioManager] PlayStatic2D called with null clip.");
#endif
                return;
            }

            if (_pool2D == null || _pool2DSize <= 0)
                return;

            int index = Acquire2DSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool2D[index];

            source.clip = clip;
            source.volume = volume;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = ResolveUiMixerGroup(clip, mixerGroup);

            source.Play();
            _startTimes2D[index] = Time.unscaledTime;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” MIXER GROUP ACCESSORS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Mixer group Ð´Ð»Ñ SFX (ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°, Ð¼ÐµÑ…Ð°Ð½Ð¸Ð·Ð¼Ñ‹, Ð¾ÐºÑ€ÑƒÐ¶ÐµÐ½Ð¸Ðµ).</summary>
        public AudioMixerGroup SfxGroup => _sfxGroup;

        /// <summary>Mixer group Ð´Ð»Ñ Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹ÑÐ° Ð¸ Ð·Ð²ÑƒÐºÐ¾Ð² ÑˆÐ»ÐµÐ¼Ð°.</summary>
        public AudioMixerGroup InterfaceGroup => _interfaceGroup;

        /// <summary>Mixer group for resolved ambient-bed playback.</summary>
        public AudioMixerGroup AmbientGroup => ResolvedBedBusGroup;

        /// <summary>Mixer group for dominant hostile cues.</summary>
        public AudioMixerGroup ThreatGroup => ResolvedThreatBusGroup;

        /// <summary>Mixer group for ambient bed layers.</summary>
        public AudioMixerGroup BedGroup => ResolvedBedBusGroup;

        /// <summary>Current 360-bin acoustic radar intensity ring for HUD consumers. Treat as read-only and reacquire each tick.</summary>
        public NativeArray<float> AcousticRadarIntensityBins => _acousticRadarIntensityBins;

        /// <summary>Current acoustic radar angular resolution in bins.</summary>
        public int AcousticRadarResolution => AcousticRadarBinCount;

        /// <summary>Persistent 8x4 acoustic radar energy grid for HUD sonar distortion overlays.</summary>
        public NativeArray<float> AcousticRadarEnergyGrid => _acousticRadarGrid;

        /// <summary>GPU upload buffer for the 8x4 acoustic radar energy grid.</summary>
        public ComputeBuffer AcousticRadarEnergyGridBuffer => _acousticRadarGridBuffer;

        /// <summary>Returns the persistent 360-degree acoustic radar ring for HUD/visor consumers.</summary>
        public bool TryGetAcousticRadarPayload(out NativeArray<float> radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = _acousticRadarIntensityBins;
            radialResolution = AcousticRadarBinCount;
            return radialIntensityBins.IsCreated && radialResolution > 0;
        }

        /// <summary>Returns the persistent 8x4 acoustic radar grid and its GPU upload buffer.</summary>
        public bool TryGetAcousticRadarGridPayload(
            out NativeArray<float> gridEnergy,
            out int azimuthBins,
            out int elevationBins,
            out ComputeBuffer gridBuffer)
        {
            gridEnergy = _acousticRadarGrid;
            azimuthBins = AcousticRadarGridAzimuthBins;
            elevationBins = AcousticRadarGridElevationBins;
            gridBuffer = _acousticRadarGridBuffer;
            return gridEnergy.IsCreated && gridBuffer != null;
        }

        internal bool TryGetDominantBinauralEmitter(out BinauralEmitterTelemetry telemetry)
        {
            telemetry = _dominantBinauralEmitter;
            return telemetry.Valid != 0;
        }

        internal int CopyActiveWorldEmitterSamples(ActiveEmitterSample[] destination)
        {
            if (destination == null || destination.Length == 0 || _pool == null)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = Time.unscaledTime;
            for (int activeSlot = 0; activeSlot < _activeWorldCount && count < limit; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isPlaying || source.clip == null)
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = source.transform.position,
                    Amplitude = math.max(0f, source.volume)
                };
                count++;
            }

            for (int i = 0; i < _impactEmitters.Length && count < limit; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        internal int CopyActiveImpactEmitterSamples(ActiveEmitterSample[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = Time.unscaledTime;
            for (int i = 0; i < _impactEmitters.Length && count < limit; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        private void UpdateDominantBinauralEmitterTelemetry(float now, Transform listener)
        {
            _dominantBinauralEmitter = default;
            if (listener == null)
                return;

            float bestScore = 0f;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || !source.isPlaying || source.clip == null)
                    continue;

                TryPromoteBinauralEmitter(listener, source.transform.position, math.max(0f, source.volume), ref bestScore);
            }

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                TryPromoteBinauralEmitter(listener, emitter.Position, amplitude, ref bestScore);
            }
        }

        private void TryPromoteBinauralEmitter(Transform listener, Vector3 sourcePosition, float amplitude, ref float bestScore)
        {
            if (!(amplitude > 0f))
                return;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float distanceSqr = ResolveAbsoluteDistanceSqr(listener, sourcePosition);
            if (distanceSqr <= 0.0001f)
                return;

            float distance = math.sqrt(distanceSqr);
            float energy = amplitude * (1f - math.saturate(distance / math.max(_maxDistance, 0.01f)));
            if (!(energy > bestScore))
                return;

            float azimuth = math.atan2(listenerLocalPosition.x, listenerLocalPosition.z);
            float absAzimuth = math.abs(azimuth);
            float absSin = math.abs(math.sin(azimuth));
            float waterDensityMul = math.saturate(_listenerWaterDensityMul);
            float airItdSeconds =
                (BinauralHeadRadiusMeters / SoundSpeedAirMetersPerSecond) *
                (absAzimuth + math.sin(absAzimuth));
            float airShadowCutoff = math.lerp(8000f, 1200f, absSin);
            float waterShadowCutoff = math.lerp(8000f, 3000f, absSin);
            float shadowCutoff = math.lerp(airShadowCutoff, waterShadowCutoff, waterDensityMul);
            float shadowAmount = math.lerp(absSin, absSin * 0.5f, waterDensityMul);
            if (TryResolveRearHemisphereLowPassCutoff(sourcePosition, out float rearHemisphereCutoff))
            {
                shadowCutoff = math.min(shadowCutoff, rearHemisphereCutoff);
                float rearShadowAmount = math.saturate(
                    (RearHemisphereLowPassMaximumCutoffHertz - rearHemisphereCutoff) /
                    math.max(RearHemisphereLowPassMaximumCutoffHertz - RearHemisphereLowPassMinimumCutoffHertz, 1f));
                shadowAmount = math.saturate(math.max(shadowAmount, rearShadowAmount));
            }

            _dominantBinauralEmitter = new BinauralEmitterTelemetry
            {
                Position = sourcePosition,
                DistanceMeters = distance,
                AzimuthRadians = azimuth,
                ItdSeconds = airItdSeconds,
                ShadowAmount01 = shadowAmount,
                ShadowCutoffHertz = shadowCutoff,
                Energy = energy,
                WaterDensityMul = waterDensityMul,
                Valid = 1
            };
            bestScore = energy;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” UTILITY
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐžÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÑ‚ Ð²ÑÐµ Ð·Ð²ÑƒÐºÐ¸ Ð² Ð¿ÑƒÐ»Ðµ. ÐÐ²Ð°Ñ€Ð¸Ð¹Ð½Ñ‹Ð¹ Ð¼ÐµÑ‚Ð¾Ð´.
        /// ÐŸÐ¾Ð»ÐµÐ·ÐµÐ½ Ð¿Ñ€Ð¸ ÑÐ¼ÐµÐ½Ðµ ÑÑ†ÐµÐ½Ñ‹, Ð¿Ð°ÑƒÐ·Ðµ, Ð¸Ð»Ð¸ Ñ„Ð°Ñ‚Ð°Ð»ÑŒÐ½Ð¾Ð¼ ÑÐ¾Ð±Ñ‹Ñ‚Ð¸Ð¸.
        /// </summary>
        public void StopAll()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                _pool[i].Stop();
                ResetWorldSourceState(i, true);
            }

            for (int i = 0; i < _pool2DSize; i++)
            {
                _pool2D[i].Stop();
                _pool2D[i].clip = null;
                _startTimes2D[i] = -1f;
            }
        }

        /// <summary>
        /// Ð’Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ ÐºÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ Ð°ÐºÑ‚Ð¸Ð²Ð½Ð¾ Ð¸Ð³Ñ€Ð°ÑŽÑ‰Ð¸Ñ… Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð² Ð² Ð¿ÑƒÐ»Ðµ.
        /// Ð¢Ð¾Ð»ÑŒÐºÐ¾ Ð´Ð»Ñ debug / profiling. ÐÐµ Ð²Ñ‹Ð·Ñ‹Ð²Ð°Ñ‚ÑŒ Ð² hot path.
        /// </summary>
        public int ActiveSourceCount
        {
            get
            {
                return _activeWorldCount;
            }
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  POOL MANAGEMENT â€” PRIVATE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>
        /// ÐÐ°Ñ…Ð¾Ð´Ð¸Ñ‚ Ð¸Ð½Ð´ÐµÐºÑ ÑÐ²Ð¾Ð±Ð¾Ð´Ð½Ð¾Ð³Ð¾ AudioSource Ð² Ð¿ÑƒÐ»Ðµ.
        /// Ð•ÑÐ»Ð¸ Ð²ÑÐµ Ð·Ð°Ð½ÑÑ‚Ñ‹ â€” Ð²Ð¾Ð·Ð²Ñ€Ð°Ñ‰Ð°ÐµÑ‚ Ð¸Ð½Ð´ÐµÐºÑ ÑÐ°Ð¼Ð¾Ð³Ð¾ ÑÑ‚Ð°Ñ€Ð¾Ð³Ð¾ (Ð²Ñ‹Ñ‚ÐµÑÐ½ÐµÐ½Ð¸Ðµ).
        ///
        /// ÐÐ»Ð³Ð¾Ñ€Ð¸Ñ‚Ð¼:
        ///   1. Ð›Ð¸Ð½ÐµÐ¹Ð½Ñ‹Ð¹ Ð¿Ñ€Ð¾Ñ…Ð¾Ð´ Ð¿Ð¾ Ð¼Ð°ÑÑÐ¸Ð²Ñƒ â€” Ð¸Ñ‰ÐµÐ¼ Ð¿ÐµÑ€Ð²Ñ‹Ð¹ !isPlaying.
        ///   2. ÐŸÐ°Ñ€Ð°Ð»Ð»ÐµÐ»ÑŒÐ½Ð¾ Ð¾Ñ‚ÑÐ»ÐµÐ¶Ð¸Ð²Ð°ÐµÐ¼ oldest (Ð¼Ð¸Ð½Ð¸Ð¼Ð°Ð»ÑŒÐ½Ñ‹Ð¹ startTime ÑÑ€ÐµÐ´Ð¸ playing).
        ///   3. ÐžÐ´Ð¸Ð½ Ð¿Ñ€Ð¾Ñ…Ð¾Ð´ â€” O(n), n â‰¤ 32. Zero-GC.
        ///
        /// Cost: ~0.001ms Ð´Ð»Ñ Ð¿ÑƒÐ»Ð° Ð¸Ð· 16 ÑÐ»ÐµÐ¼ÐµÐ½Ñ‚Ð¾Ð².
        /// </summary>
        /// <returns>Ð˜Ð½Ð´ÐµÐºÑ Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ° Ð´Ð»Ñ Ð¸ÑÐ¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ð½Ð¸Ñ.</returns>
private int AcquireSourceIndex()
        {
            if (_pool == null || _poolSize <= 0)
                return -1;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_activeWorldSlots[i] < 0)
                    return i;

                AudioSource source = _pool[i];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    ResetWorldSourceState(i, true);
                    return i;
                }
            }

            int oldestIndex = 0;
            float oldestTime = float.MaxValue;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                if (_startTimes[sourceIndex] < oldestTime)
                {
                    oldestTime = _startTimes[sourceIndex];
                    oldestIndex = sourceIndex;
                }
            }

            _pool[oldestIndex].Stop();
            ResetWorldSourceState(oldestIndex, true);

#if UNITY_EDITOR
            Debug.LogFormat(
                this,
                "[SpatialAudioManager] Pool full ({0}/{0}). Evicting oldest source at index {1}.",
                _poolSize,
                oldestIndex);
#endif

            return oldestIndex;
        }

        private int AcquireSourceIndexNoEvict()
        {
            if (_pool == null || _activeWorldSlots == null || _poolSize <= 0)
                return -1;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_activeWorldSlots[i] < 0)
                    return i;

                AudioSource source = _pool[i];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    ResetWorldSourceState(i, true);
                    return i;
                }
            }

            return -1;
        }

        private bool TryReserveHarvestAudioEvent()
        {
            int frame = Time.frameCount;
            if (_harvestAudioFrame != frame)
            {
                _harvestAudioFrame = frame;
                _harvestAudioEventsThisFrame = 0;
            }

            if (_harvestAudioEventsThisFrame >= MaxHarvestAudioEventsPerFrame)
                return false;

            _harvestAudioEventsThisFrame++;
            return true;
        }

        private bool TryPlayAtPointWithoutEviction(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup)
        {
            if (clip == null || _pool == null || _poolSize <= 0)
                return false;

            AudioLodTier lodTier = ResolveAudioLodTier(position);
            if (lodTier == AudioLodTier.Tier2Culled)
                return false;

            int index = AcquireSourceIndexNoEvict();
            if (index < 0)
                return false;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;
            source.transform.position = position;
            source.clip = clip;
            source.volume = volume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = volume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            source.pitch = ResolveSourcePitch(index, source, 1f);
            _audioLodTiers[index] = lodTier;
            float now = Time.unscaledTime;
            UpdateWorldSourceAudioLod(index, source, now, true);
            ApplyHaasMask(index, position);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);
            source.Play();
            _startTimes[index] = now;
            MarkWorldSourceActive(index);
            return true;
        }

        private void TryRegisterUpdatable()
        {
            if (_registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registeredUpdatable = GlobalRegistry.Updatables.Contains(this);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            // Mirrors impact positions for passive radar/UI consumers only.
            // Audible impact energy is synthesized through PlayerCriticalProceduralAudioRenderer.
            float amplitude = math.saturate(impactSignal.Intensity * ImpactEmitterAmplitudeScale);
            if (impactSignal.IsHeavy)
                amplitude = math.max(amplitude, 0.45f);

            TryQueueImpactRadarEmitter(impactSignal.Point, amplitude, math.saturate(impactSignal.Intensity));
        }

        private bool TryQueueImpactRadarEmitter(Vector3 position, float amplitude, float lifetime01)
        {
            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return false;

            float now = Time.unscaledTime;
            float lifetime = math.lerp(
                ImpactEmitterLifetimeMinSeconds,
                ImpactEmitterLifetimeMaxSeconds,
                math.saturate(lifetime01));
            int selectedIndex = -1;
            float weakestAmplitude = float.MaxValue;
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                if (!(_impactEmitters[i].ExpireAt > now))
                {
                    selectedIndex = i;
                    break;
                }

                if (_impactEmitters[i].Amplitude < weakestAmplitude)
                {
                    weakestAmplitude = _impactEmitters[i].Amplitude;
                    selectedIndex = i;
                }
            }

            if (selectedIndex < 0)
                return false;

            _impactEmitters[selectedIndex] = new ImpactEmitterSample
            {
                Position = position,
                Amplitude = amplitude,
                SpawnAt = now,
                ExpireAt = now + lifetime
            };
            return true;
        }

        private void HandleFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent)
        {
            Vector3 listenerAbsolutePosition = _listenerTransform != null
                ? HectonFloatingOrigin.ToAbsoluteUniversePosition(_listenerTransform.position)
                : implosionEvent.RuntimePosition;
            float distanceMeters = math.length(implosionEvent.RuntimePosition - listenerAbsolutePosition);
            float soundSpeedMetersPerSecond = ResolveThermalSoundSpeedMetersPerSecond(
                implosionEvent.RuntimePosition,
                listenerAbsolutePosition,
                out float thermalShimmer01);
            ResolveDelayedAcousticPath(
                implosionEvent.RuntimePosition,
                listenerAbsolutePosition,
                out float acousticTransmission01,
                out float lowPassCutoffHz);
            DelayedAudioEvent delayedEvent = new DelayedAudioEvent
            {
                Kind = DelayedAudioEventKind.FatalPressureImplosion,
                Position = implosionEvent.RuntimePosition,
                EventTimeSeconds = Time.time,
                DelaySeconds = distanceMeters / soundSpeedMetersPerSecond,
                Volume = FatalPressureImplosionEventVolume,
                Pitch = FatalPressureImplosionEventPitch,
                AcousticTransmission01 = acousticTransmission01,
                LowPassCutoffHz = lowPassCutoffHz,
                ThermalShimmer01 = thermalShimmer01,
                TraumaRangeMeters = FatalPressureImplosionTraumaRangeMeters,
                TraumaImpulse = FatalPressureImplosionTraumaImpulse,
                TraumaWeight = FatalPressureImplosionTraumaWeight
            };
            TryEnqueueDelayedAudioEvent(in delayedEvent);
        }

        /// <summary>
        /// Receives deferred fatal pressure implosion notifications from the submarine atmosphere event lane.
        /// </summary>
        public void OnFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent)
        {
            HandleFatalPressureImplosion(in implosionEvent);
        }

        /// <summary>
        /// Queues the muffled backpack chemistry explosion through the same delayed underwater event bus.
        /// </summary>
        public void QueueInventoryRunawayExplosion(Vector3 runtimePosition, float volume01)
        {
            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(runtimePosition);
            Vector3 listenerAbsolutePosition = _listenerTransform != null
                ? HectonFloatingOrigin.ToAbsoluteUniversePosition(_listenerTransform.position)
                : absolutePosition;
            float distanceMeters = math.length(absolutePosition - listenerAbsolutePosition);
            float soundSpeedMetersPerSecond = ResolveThermalSoundSpeedMetersPerSecond(
                absolutePosition,
                listenerAbsolutePosition,
                out float thermalShimmer01);
            ResolveDelayedAcousticPath(
                absolutePosition,
                listenerAbsolutePosition,
                out float acousticTransmission01,
                out float lowPassCutoffHz);
            DelayedAudioEvent delayedEvent = new DelayedAudioEvent
            {
                Kind = DelayedAudioEventKind.InventoryRunawayExplosion,
                Position = absolutePosition,
                EventTimeSeconds = Time.time,
                DelaySeconds = distanceMeters / soundSpeedMetersPerSecond,
                Volume = math.saturate(volume01),
                Pitch = 0.72f,
                AcousticTransmission01 = acousticTransmission01,
                LowPassCutoffHz = lowPassCutoffHz,
                ThermalShimmer01 = thermalShimmer01,
                TraumaRangeMeters = 0f,
                TraumaImpulse = 0f,
                TraumaWeight = 0f
            };
            TryEnqueueDelayedAudioEvent(in delayedEvent);
        }

        private void TryEnqueueDelayedAudioEvent(in DelayedAudioEvent delayedEvent)
        {
            if (!_delayedAudioIngress.IsCreated || !_pendingDelayedAudioEvents.IsCreated)
                return;

            if (_pendingDelayedAudioEvents.Length + _delayedAudioIngressCount >= MaxDelayedAudioEvents)
                return;

            _delayedAudioIngress.Enqueue(delayedEvent);
            _delayedAudioIngressCount++;
        }

        private void DrainDelayedAudioIngress()
        {
            if (!_delayedAudioIngress.IsCreated || !_pendingDelayedAudioEvents.IsCreated || _delayedAudioIngressCount <= 0)
                return;

            while (_delayedAudioIngressCount > 0 && _delayedAudioIngress.TryDequeue(out DelayedAudioEvent delayedEvent))
            {
                _pendingDelayedAudioEvents.Add(delayedEvent);
                _delayedAudioIngressCount--;
            }
        }

        private void ProcessDelayedAudioEvents(Vector3 listenerAbsolutePosition)
        {
            if (!_pendingDelayedAudioEvents.IsCreated || _pendingDelayedAudioEvents.Length == 0)
                return;

            float now = Time.time;
            int writeIndex = 0;
            for (int i = 0; i < _pendingDelayedAudioEvents.Length; i++)
            {
                DelayedAudioEvent delayedEvent = _pendingDelayedAudioEvents[i];
                if (now < delayedEvent.EventTimeSeconds + delayedEvent.DelaySeconds)
                {
                    if (writeIndex != i)
                        _pendingDelayedAudioEvents[writeIndex] = delayedEvent;
                    writeIndex++;
                    continue;
                }

                DispatchDelayedAudioEvent(in delayedEvent, listenerAbsolutePosition);
            }

            if (writeIndex != _pendingDelayedAudioEvents.Length)
                _pendingDelayedAudioEvents.ResizeUninitialized(writeIndex);
        }

        private void DispatchDelayedAudioEvent(in DelayedAudioEvent delayedEvent, Vector3 listenerAbsolutePosition)
        {
            switch (delayedEvent.Kind)
            {
                case DelayedAudioEventKind.FatalPressureImplosion:
                    if (_fatalPressureImplosionClip != null)
                    {
                        PlayAtPointWithLowPass(
                            _fatalPressureImplosionClip,
                            delayedEvent.Position,
                            ResolveDelayedEventVolume(in delayedEvent),
                            ResolveDelayedEventPitch(in delayedEvent),
                            ResolvedThreatBusGroup,
                            ResolveDelayedEventLowPass(in delayedEvent));
                    }

                    ApplyDelayedTrauma(in delayedEvent, listenerAbsolutePosition);
                    break;

                case DelayedAudioEventKind.InventoryRunawayExplosion:
                    if (_inventoryRunawayExplosionClip != null)
                    {
                        PlayAtPointWithLowPass(
                            _inventoryRunawayExplosionClip,
                            delayedEvent.Position,
                            ResolveDelayedEventVolume(in delayedEvent),
                            ResolveDelayedEventPitch(in delayedEvent),
                            ResolvedThreatBusGroup,
                            ResolveDelayedEventLowPass(in delayedEvent));
                    }

                    break;
            }
        }

        private static float ResolveDelayedEventVolume(in DelayedAudioEvent delayedEvent)
        {
            float transmission01 = delayedEvent.AcousticTransmission01 > 0f
                ? math.saturate(delayedEvent.AcousticTransmission01)
                : 1f;
            return math.saturate(delayedEvent.Volume * transmission01);
        }

        private static float ResolveDelayedEventLowPass(in DelayedAudioEvent delayedEvent)
        {
            float cutoffHz = delayedEvent.LowPassCutoffHz > 0f
                ? delayedEvent.LowPassCutoffHz
                : AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            return math.clamp(
                cutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
        }

        private static float ResolveDelayedEventPitch(in DelayedAudioEvent delayedEvent)
        {
            float shimmer01 = math.saturate(delayedEvent.ThermalShimmer01);
            if (shimmer01 <= 0.0001f)
                return math.clamp(delayedEvent.Pitch, 0.1f, 3f);

            float phase = (Time.time * 47.3f) +
                          (delayedEvent.Position.x * 0.013f) +
                          (delayedEvent.Position.z * 0.017f);
            float shimmer = math.sin(phase) * ThermalShimmerMaximumPitchRatio * shimmer01;
            return math.clamp(delayedEvent.Pitch * (1f + shimmer), 0.1f, 3f);
        }

        private static float ResolveThermalSoundSpeedMetersPerSecond(
            Vector3 sourceAbsolutePosition,
            Vector3 listenerAbsolutePosition,
            out float thermalShimmer01)
        {
            Vector3 samplePosition = (sourceAbsolutePosition + listenerAbsolutePosition) * 0.5f;
            float temperatureCelsius = 2f;
            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                temperatureCelsius = vegetationBridge.GetWaterTemperature(samplePosition);

            thermalShimmer01 = 0f;
            IThermodynamicsService thermodynamicsService = GlobalRegistry.ThermodynamicsService;
            if (thermodynamicsService != null &&
                thermodynamicsService.IsInitialized &&
                thermodynamicsService.SampleThermalFlow(
                    samplePosition,
                    ThermalSoundSpeedSampleRadiusMeters,
                    out AbyssalThermalManager.ThermalFlowSample thermalFlowSample))
            {
                float heat01 = math.saturate(thermalFlowSample.Heat01 / math.max(ThermalFlowHeatReferenceCelsius, 0.001f));
                temperatureCelsius += heat01 * ThermalFlowTemperatureBoostCelsius;
                thermalShimmer01 = heat01;
            }

            float soundSpeed = 1440f + (4.6f * temperatureCelsius) - (0.05f * temperatureCelsius * temperatureCelsius);
            return math.clamp(
                soundSpeed,
                ThermalSoundSpeedMinimumMetersPerSecond,
                ThermalSoundSpeedMaximumMetersPerSecond);
        }

        private static void ResolveDelayedAcousticPath(
            Vector3 sourceAbsolutePosition,
            Vector3 listenerAbsolutePosition,
            out float acousticTransmission01,
            out float lowPassCutoffHz)
        {
            acousticTransmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (!AcousticOcclusionUtility.TryTraceVoxelDensityOcclusion(
                    sourceAbsolutePosition,
                    listenerAbsolutePosition,
                    out AcousticVoxelOcclusionResult voxelOcclusion))
            {
                return;
            }

            acousticTransmission01 = math.saturate(voxelOcclusion.Transmission01);
            lowPassCutoffHz = math.clamp(
                voxelOcclusion.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
        }

        private void HandleRepairDroneTorchAcoustic(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            if (acousticEvent.Clip == null)
                return;

            PlayAtPoint(
                acousticEvent.Clip,
                acousticEvent.Position,
                acousticEvent.Volume,
                acousticEvent.Pitch,
                ResolvedDefaultWorldMixerGroup);
        }

        /// <summary>
        /// Receives deferred repair-drone torch acoustic pulses from the construction event lane.
        /// </summary>
        public void OnRepairDroneTorchAcoustic(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            HandleRepairDroneTorchAcoustic(in acousticEvent);
        }

        private void ApplyDelayedTrauma(in DelayedAudioEvent delayedEvent, Vector3 listenerAbsolutePosition)
        {
            if (_listenerPlayerMovement == null)
                return;

            Vector3 listenerOffset = listenerAbsolutePosition - delayedEvent.Position;
            float distanceMeters = listenerOffset.magnitude;
            if (distanceMeters > delayedEvent.TraumaRangeMeters)
                return;

            Vector3 traumaDirection = distanceMeters > 0.0001f
                ? listenerOffset / distanceMeters
                : Vector3.up;
            float trauma01 = 1f - math.saturate(distanceMeters / math.max(delayedEvent.TraumaRangeMeters, 0.0001f));
            _listenerPlayerMovement.ApplyPhysicalTrauma(
                traumaDirection * (delayedEvent.TraumaImpulse * trauma01),
                delayedEvent.TraumaWeight * trauma01);
        }

        private void ClearDelayedAudioEvents()
        {
            if (_delayedAudioIngress.IsCreated)
            {
                while (_delayedAudioIngress.TryDequeue(out _))
                {
                }
            }

            _delayedAudioIngressCount = 0;
            if (_pendingDelayedAudioEvents.IsCreated)
                _pendingDelayedAudioEvents.Clear();
        }

        private void ApplyHaasMask(int sourceIndex, Vector3 sourcePosition)
        {
            float predictedArrivalTime = ResolvePredictedArrivalTime(sourcePosition);
            float closestDelta = float.MaxValue;
            int earliestCompetingIndex = -1;
            float earliestCompetingArrival = float.MaxValue;

            for (int i = 0; i < _poolSize; i++)
            {
                if (i == sourceIndex || _pool[i] == null || !_pool[i].isPlaying || _arrivalTimes[i] < 0f)
                    continue;

                float arrivalDelta = math.abs(predictedArrivalTime - _arrivalTimes[i]);
                if (arrivalDelta < closestDelta)
                {
                    closestDelta = arrivalDelta;
                    earliestCompetingIndex = i;
                    earliestCompetingArrival = _arrivalTimes[i];
                }
            }

            _arrivalTimes[sourceIndex] = predictedArrivalTime;
            if (closestDelta < HaasArrivalWindowSeconds && earliestCompetingIndex >= 0)
            {
                float releaseTime = Time.unscaledTime + HaasReleaseThresholdSeconds;
                if (predictedArrivalTime < earliestCompetingArrival)
                {
                    _haasReleaseTimes[earliestCompetingIndex] = releaseTime;
                    _haasReleaseTimes[sourceIndex] = 0f;
                }
                else
                {
                    _haasReleaseTimes[sourceIndex] = releaseTime;
                }

                return;
            }

            _haasReleaseTimes[sourceIndex] = 0f;
        }

        private float ResolvePredictedArrivalTime(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return Time.unscaledTime;

            return Time.unscaledTime +
                   (math.sqrt(ResolveAbsoluteDistanceSqr(listener, sourcePosition)) / SoundSpeedWaterMetersPerSecond);
        }

        private Transform ResolveListenerTransform()
        {
            if (_listenerTransform != null && _listenerTransform.gameObject.activeInHierarchy)
                return _listenerTransform;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null)
            {
                Camera playerCamera = playerContext.PlayerCamera;
                if (playerCamera != null)
                {
                    _listenerTransform = playerCamera.transform;
                    return _listenerTransform;
                }

                GameObject playerObject = playerContext.PlayerObject;
                if (playerObject != null)
                {
                    if (playerObject.TryGetComponent(out AudioListener playerListener))
                    {
                        _listenerTransform = playerListener.transform;
                        return _listenerTransform;
                    }

                    AudioListener ownedPlayerListener =
                        ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerObject.transform);
                    if (ownedPlayerListener != null)
                    {
                        _listenerTransform = ownedPlayerListener.transform;
                        return _listenerTransform;
                    }
                }
            }

            _listenerTransform = null;
            return _listenerTransform;
        }

        private void ResetAllHaasState()
        {
            if (_arrivalTimes == null || _haasReleaseTimes == null)
                return;

            int resetCount = math.min(_poolSize, math.min(_arrivalTimes.Length, _haasReleaseTimes.Length));
            for (int i = 0; i < resetCount; i++)
                ResetHaasState(i);
        }

        private void ResetAllWorldSourceState()
        {
            if (_pool == null)
                return;

            int resetCount = math.min(_poolSize, _pool.Length);
            for (int i = 0; i < resetCount; i++)
                ResetWorldSourceState(i, false);
        }

        private void ResetImpactEmitters()
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
                _impactEmitters[i] = default;
        }

        private void DecayImpactEmitters(float now)
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                if (_impactEmitters[i].ExpireAt > now)
                    continue;

                _impactEmitters[i] = default;
            }
        }

        private void ResetHaasState(int sourceIndex)
        {
            if (_arrivalTimes == null || _haasReleaseTimes == null || sourceIndex < 0)
                return;

            int resetCount = math.min(_poolSize, math.min(_arrivalTimes.Length, _haasReleaseTimes.Length));
            if (sourceIndex >= resetCount)
                return;

            _arrivalTimes[sourceIndex] = -1f;
            _haasReleaseTimes[sourceIndex] = 0f;
        }

        private void ResetWorldSourceState(int sourceIndex, bool clearClip)
        {
            if (_pool == null || sourceIndex < 0 || sourceIndex >= _pool.Length)
                return;

            RemoveWorldSourceActive(sourceIndex);

            AudioSource source = _pool[sourceIndex];
            if (source != null)
            {
                if (clearClip)
                    source.enabled = false;
                source.panStereo = 0f;
                source.spatialBlend = 1f;
                if (clearClip)
                    source.clip = null;
            }

            AudioLowPassFilter lowPassFilter = _lowPassFilters != null && sourceIndex < _lowPassFilters.Length
                ? _lowPassFilters[sourceIndex]
                : null;
            if (lowPassFilter != null)
            {
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;
            }

            if (_baseVolumes != null && sourceIndex < _baseVolumes.Length)
                _baseVolumes[sourceIndex] = 0f;

            if (_basePitches != null && sourceIndex < _basePitches.Length)
                _basePitches[sourceIndex] = 1f;

            if (_sourceVoxelLowPassCutoffs != null && sourceIndex < _sourceVoxelLowPassCutoffs.Length)
                _sourceVoxelLowPassCutoffs[sourceIndex] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if (_sourceVoxelTransmissions != null && sourceIndex < _sourceVoxelTransmissions.Length)
                _sourceVoxelTransmissions[sourceIndex] = 1f;

            if (_sourceVoxelNextUpdateTimes != null && sourceIndex < _sourceVoxelNextUpdateTimes.Length)
                _sourceVoxelNextUpdateTimes[sourceIndex] = 0f;

            if (_smoothedDopplerRatios != null && sourceIndex < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[sourceIndex] = 1f;

            if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                _previousRelativeVelocities[sourceIndex] = 0f;

            if (_previousAbsolutePositions != null && sourceIndex < _previousAbsolutePositions.Length)
                _previousAbsolutePositions[sourceIndex] = default;

            if (_currentAbsoluteVelocities != null && sourceIndex < _currentAbsoluteVelocities.Length)
                _currentAbsoluteVelocities[sourceIndex] = default;

            if (_nextTierUpdateTimes != null && sourceIndex < _nextTierUpdateTimes.Length)
                _nextTierUpdateTimes[sourceIndex] = 0f;

            if (_audioLodTiers != null && sourceIndex < _audioLodTiers.Length)
                _audioLodTiers[sourceIndex] = AudioLodTier.Tier0Full;

            if (_startTimes != null && sourceIndex < _startTimes.Length)
                _startTimes[sourceIndex] = -1f;

            ResetHaasState(sourceIndex);
        }

        private bool TryRefreshSourceVoxelOcclusion(
            int sourceIndex,
            Vector3 sourcePosition,
            float now,
            bool forceImmediate,
            out float transmission01,
            out float lowPassCutoffHz)
        {
            transmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (_sourceVoxelLowPassCutoffs == null ||
                _sourceVoxelTransmissions == null ||
                _sourceVoxelNextUpdateTimes == null ||
                sourceIndex < 0 ||
                sourceIndex >= _sourceVoxelLowPassCutoffs.Length ||
                sourceIndex >= _sourceVoxelTransmissions.Length ||
                sourceIndex >= _sourceVoxelNextUpdateTimes.Length)
            {
                return false;
            }

            transmission01 = math.saturate(_sourceVoxelTransmissions[sourceIndex]);
            lowPassCutoffHz = math.clamp(
                _sourceVoxelLowPassCutoffs[sourceIndex],
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (!forceImmediate && now < _sourceVoxelNextUpdateTimes[sourceIndex])
                return lowPassCutoffHz < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f || transmission01 < 0.999f;

            _sourceVoxelNextUpdateTimes[sourceIndex] = now + VoxelSourceOcclusionUpdateIntervalSeconds;
            _sourceVoxelTransmissions[sourceIndex] = 1f;
            _sourceVoxelLowPassCutoffs[sourceIndex] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            transmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return false;

            Vector3 sourceAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(sourcePosition);
            Vector3 listenerAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(listener.position);
            if (!AcousticOcclusionUtility.TryTraceVoxelDensityOcclusion(
                    sourceAbsolutePosition,
                    listenerAbsolutePosition,
                    out AcousticVoxelOcclusionResult voxelOcclusion))
            {
                return false;
            }

            transmission01 = math.saturate(voxelOcclusion.Transmission01);
            lowPassCutoffHz = math.clamp(
                voxelOcclusion.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            _sourceVoxelTransmissions[sourceIndex] = transmission01;
            _sourceVoxelLowPassCutoffs[sourceIndex] = lowPassCutoffHz;
            return lowPassCutoffHz < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f || transmission01 < 0.999f;
        }

        private void UpdateWorldSourceAudioLod(int sourceIndex, AudioSource source, float now, bool forceImmediate)
        {
            if (source == null)
                return;

            AudioLodTier resolvedTier = ResolveAudioLodTier(source.transform.position);
            bool rearHemisphereFilterEnabled = TryResolveRearHemisphereLowPassCutoff(source.transform.position, out float rearHemisphereCutoff);
            bool caveLowPassEnabled = TryResolveCaveExternalLowPassCutoff(source, source.transform.position, out float caveLowPassCutoff);
            bool voxelLowPassEnabled = TryRefreshSourceVoxelOcclusion(
                sourceIndex,
                source.transform.position,
                now,
                forceImmediate,
                out float voxelTransmission01,
                out float voxelLowPassCutoff);
            if (_baseVolumes != null && sourceIndex >= 0 && sourceIndex < _baseVolumes.Length)
                source.volume = _baseVolumes[sourceIndex] * voxelTransmission01;
            if (!forceImmediate &&
                resolvedTier == AudioLodTier.Tier1Reduced &&
                _audioLodTiers[sourceIndex] == AudioLodTier.Tier1Reduced &&
                now < _nextTierUpdateTimes[sourceIndex])
            {
                return;
            }

            _audioLodTiers[sourceIndex] = resolvedTier;
            switch (resolvedTier)
            {
                case AudioLodTier.Tier0Full:
                    source.enabled = true;
                    source.panStereo = 0f;
                    float tierZeroCutoff = 22000f;
                    if (rearHemisphereFilterEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, rearHemisphereCutoff);
                    if (caveLowPassEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, caveLowPassCutoff);
                    if (voxelLowPassEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, voxelLowPassCutoff);
                    ApplyLowPassFilter(
                        sourceIndex,
                        rearHemisphereFilterEnabled || caveLowPassEnabled || voxelLowPassEnabled,
                        tierZeroCutoff);
                    _nextTierUpdateTimes[sourceIndex] = 0f;
                    return;

                case AudioLodTier.Tier1Reduced:
                    source.enabled = true;
                    source.panStereo = ResolveStereoPan(source.transform.position);
                    float tierOneCutoff = Tier1LowPassCutoffHertz;
                    if (rearHemisphereFilterEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, rearHemisphereCutoff);
                    if (caveLowPassEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, caveLowPassCutoff);
                    if (voxelLowPassEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, voxelLowPassCutoff);
                    ApplyLowPassFilter(sourceIndex, true, tierOneCutoff);
                    _nextTierUpdateTimes[sourceIndex] = now + Tier1UpdateIntervalSeconds;
                    return;

                default:
                    source.Stop();
                    source.enabled = false;
                    ResetWorldSourceState(sourceIndex, true);
                    return;
            }
        }

        private void ApplyLowPassFilter(int sourceIndex, bool enabled, float cutoffFrequency)
        {
            if (_lowPassFilters == null || sourceIndex < 0 || sourceIndex >= _lowPassFilters.Length)
                return;

            AudioLowPassFilter lowPassFilter = _lowPassFilters[sourceIndex];
            if (lowPassFilter == null)
                return;

            lowPassFilter.enabled = enabled;
            lowPassFilter.cutoffFrequency = cutoffFrequency;
        }

        private float ResolveTargetSpatialBlend(int sourceIndex, float now)
        {
            float baseBlend = ResolveBaseSpatialBlend(_audioLodTiers[sourceIndex]);
            if (_haasReleaseTimes[sourceIndex] > now)
                return baseBlend * HaasSecondarySpatialBlendFactor;

            return baseBlend;
        }

        private static float ResolveBaseSpatialBlend(AudioLodTier tier)
        {
            switch (tier)
            {
                case AudioLodTier.Tier1Reduced:
                case AudioLodTier.Tier2Culled:
                    return 0f;
                default:
                    return 1f;
            }
        }

        private AudioLodTier ResolveAudioLodTier(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return AudioLodTier.Tier0Full;

            float distanceSq = ResolveAbsoluteDistanceSqr(listener, sourcePosition);
            if (distanceSq > (Tier1ReducedDspDistanceMeters * Tier1ReducedDspDistanceMeters))
                return AudioLodTier.Tier2Culled;

            return distanceSq > (Tier0FullDspDistanceMeters * Tier0FullDspDistanceMeters)
                ? AudioLodTier.Tier1Reduced
                : AudioLodTier.Tier0Full;
        }

        private float ResolveStereoPan(Vector3 sourcePosition)
        {
            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return 0f;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float lateralPan = listenerLocalPosition.x / math.max(0.01f, StereoPanDistanceNormalizationMeters);
            return math.clamp(lateralPan, -1f, 1f);
        }

        private bool TryResolveRearHemisphereLowPassCutoff(Vector3 sourcePosition, out float cutoffFrequency)
        {
            cutoffFrequency = 22000f;

            Transform listener = ResolveListenerTransform();
            if (listener == null)
                return false;

            Vector3 toSource = sourcePosition - listener.position;
            if (toSource.sqrMagnitude <= 0.0001f)
                return false;

            float forwardDot = math.dot((float3)listener.forward, math.normalize((float3)toSource));
            if (forwardDot >= RearHemisphereLowPassStartDot)
                return false;

            float rear01 = math.saturate(
                (forwardDot - RearHemisphereLowPassStartDot) /
                math.max(RearHemisphereLowPassFullDot - RearHemisphereLowPassStartDot, 0.0001f));
            cutoffFrequency = math.lerp(
                RearHemisphereLowPassMaximumCutoffHertz,
                RearHemisphereLowPassMinimumCutoffHertz,
                rear01);
            return true;
        }

        private void InitializeTelemetryCaches()
        {
            if (!_acousticRadarIntensityBins.IsCreated)
            {
                _acousticRadarIntensityBins = new NativeArray<float>(
                    AcousticRadarBinCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[360] - HUD acoustic radar ring - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(
                    _acousticRadarIntensityBins,
                    nameof(SpatialAudioManager),
                    nameof(_acousticRadarIntensityBins),
                    NativeAllocationLifetime.Session);
            }

            if (!_acousticRadarGrid.IsCreated)
            {
                _acousticRadarGrid = new NativeArray<float>(
                    AcousticRadarGridCellCount,
                    Allocator.Persistent,
                    NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[32] - 8x4 acoustic radar energy grid - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(
                    _acousticRadarGrid,
                    nameof(SpatialAudioManager),
                    nameof(_acousticRadarGrid),
                    NativeAllocationLifetime.Session);
            }

            if (_acousticRadarGridUploadScratch == null || _acousticRadarGridUploadScratch.Length != AcousticRadarGridCellCount)
                _acousticRadarGridUploadScratch = new float[AcousticRadarGridCellCount]; // COLD ALLOC: float[32] - CPU mirror for acoustic radar grid ComputeBuffer uploads - owner: SpatialAudioManager

            if (_radarNearestEmitterPositions == null || _radarNearestEmitterPositions.Length != AcousticRadarNearestEmitterLimit)
                _radarNearestEmitterPositions = new Vector3[AcousticRadarNearestEmitterLimit]; // COLD ALLOC: Vector3[12] - nearest-emitter radar accumulation positions - owner: SpatialAudioManager

            if (_radarNearestEmitterAmplitudes == null || _radarNearestEmitterAmplitudes.Length != AcousticRadarNearestEmitterLimit)
                _radarNearestEmitterAmplitudes = new float[AcousticRadarNearestEmitterLimit]; // COLD ALLOC: float[12] - nearest-emitter radar accumulation amplitudes - owner: SpatialAudioManager

            if (_radarNearestEmitterDistanceSq == null || _radarNearestEmitterDistanceSq.Length != AcousticRadarNearestEmitterLimit)
                _radarNearestEmitterDistanceSq = new float[AcousticRadarNearestEmitterLimit]; // COLD ALLOC: float[12] - nearest-emitter radar accumulation distance cache - owner: SpatialAudioManager

            if (_radarNearestEmitterRoots == null || _radarNearestEmitterRoots.Length != AcousticRadarNearestEmitterLimit)
                _radarNearestEmitterRoots = new Transform[AcousticRadarNearestEmitterLimit]; // COLD ALLOC: Transform[12] - nearest-emitter radar accumulation source roots for cached occlusion lookups - owner: SpatialAudioManager

            if (_acousticRadarGridBuffer == null)
                _acousticRadarGridBuffer = new ComputeBuffer(AcousticRadarGridCellCount, sizeof(float));

            if (!_delayedAudioIngress.IsCreated)
            {
                _delayedAudioIngress = new NativeQueue<DelayedAudioEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DelayedAudioEvent>[16] - underwater propagation ingress queue for delayed world events - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _delayedAudioIngress,
                    MaxDelayedAudioEvents,
                    nameof(SpatialAudioManager),
                    nameof(_delayedAudioIngress),
                    NativeAllocationLifetime.Session);
            }

            if (!_pendingDelayedAudioEvents.IsCreated)
            {
                _pendingDelayedAudioEvents = new NativeList<DelayedAudioEvent>(MaxDelayedAudioEvents, Allocator.Persistent); // COLD ALLOC: NativeList<DelayedAudioEvent>[16] - active delayed world-event schedule - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(
                    _pendingDelayedAudioEvents,
                    nameof(SpatialAudioManager),
                    nameof(_pendingDelayedAudioEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private void ReleaseTelemetryCaches()
        {
            if (_acousticRadarIntensityBins.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticRadarIntensityBins);
                _acousticRadarIntensityBins.Dispose();
                _acousticRadarIntensityBins = default;
            }

            if (_acousticRadarGrid.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticRadarGrid);
                _acousticRadarGrid.Dispose();
                _acousticRadarGrid = default;
            }

            if (_acousticRadarGridBuffer != null)
            {
                _acousticRadarGridBuffer.Release();
                _acousticRadarGridBuffer = null;
            }

            if (_delayedAudioIngress.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpatialAudioManager), nameof(_delayedAudioIngress));
                _delayedAudioIngress.Dispose();
                _delayedAudioIngress = default;
            }

            if (_pendingDelayedAudioEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SpatialAudioManager), nameof(_pendingDelayedAudioEvents));
                _pendingDelayedAudioEvents.Dispose();
                _pendingDelayedAudioEvents = default;
            }

            _delayedAudioIngressCount = 0;
        }

        private AudioMixerGroup ResolvedDefaultWorldMixerGroup => _sfxGroup != null ? _sfxGroup : ResolvedBedBusGroup;

        private AudioMixerGroup ResolvedThreatBusGroup
        {
            get
            {
                if (_threatGroup != null)
                    return _threatGroup;

                if (_sfxGroup != null)
                    return _sfxGroup;

                return ResolvedBedBusGroup;
            }
        }

        private AudioMixerGroup ResolvedBedBusGroup
        {
            get
            {
                if (_bedGroup != null)
                    return _bedGroup;

                if (_ambientGroup != null)
                    return _ambientGroup;

                return _sfxGroup;
            }
        }

        private AudioMixerGroup ResolveUiMixerGroup(AudioClip clip, AudioMixerGroup requestedGroup)
        {
            if (requestedGroup == _ambientGroup || requestedGroup == _bedGroup || IsBedClip(clip))
                return ResolvedBedBusGroup;

            if (requestedGroup == _threatGroup || IsThreatClip(clip))
                return ResolvedThreatBusGroup;

            return requestedGroup != null ? requestedGroup : _interfaceGroup;
        }

        private AudioMixerGroup ResolveWorldMixerGroup(AudioClip clip, AudioMixerGroup requestedGroup)
        {
            if (requestedGroup == _ambientGroup || requestedGroup == _bedGroup)
                return ResolvedBedBusGroup;

            if (requestedGroup == _threatGroup || IsThreatClip(clip))
                return ResolvedThreatBusGroup;

            if ((requestedGroup == null || requestedGroup == _sfxGroup) && IsBedClip(clip))
                return ResolvedBedBusGroup;

            return requestedGroup != null ? requestedGroup : ResolvedDefaultWorldMixerGroup;
        }

        private bool IsThreatWorldSource(AudioSource source)
        {
            AudioMixerGroup threatGroup = ResolvedThreatBusGroup;
            return source != null && threatGroup != null && source.outputAudioMixerGroup == threatGroup;
        }

        private float ResolveSourcePitch(int sourceIndex, AudioSource source, float dopplerRatio)
        {
            if (_basePitches == null || sourceIndex < 0 || sourceIndex >= _basePitches.Length)
                return 1f;

            float eclipseRatio = ResolveEclipseAcousticPitchRatio(source);
            return math.clamp(_basePitches[sourceIndex] * dopplerRatio * eclipseRatio, 0.1f, 3f);
        }

        private float ResolveEclipseAcousticPitchRatio(AudioSource source)
        {
            if (source == null || math.abs(_eclipseAcousticPitchRatio - 1f) <= 0.0001f)
                return 1f;

            AudioMixerGroup bedGroup = ResolvedBedBusGroup;
            if (bedGroup == null || source.outputAudioMixerGroup != bedGroup)
                return 1f;

            return _eclipseAcousticPitchRatio;
        }

        private void ApplyEclipsePitchShiftToActiveWorldSources()
        {
            if (_pool == null || _activeWorldIndices == null || _smoothedDopplerRatios == null)
                return;

            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                if (sourceIndex < 0 || sourceIndex >= _pool.Length || sourceIndex >= _smoothedDopplerRatios.Length)
                    continue;

                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                    continue;

                source.pitch = ResolveSourcePitch(sourceIndex, source, _smoothedDopplerRatios[sourceIndex]);
            }
        }

        private void ApplyThreatBusDucking(float threatActivity, float deltaTime)
        {
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer == null || string.IsNullOrWhiteSpace(_bedDuckDbParameter))
            {
                _threatBusDuck01 = 0f;
                return;
            }

            float targetDuck01 = math.saturate(threatActivity);
            if (deltaTime <= 0f)
            {
                _threatBusDuck01 = targetDuck01;
            }
            else
            {
                float duckTimeSeconds = targetDuck01 > _threatBusDuck01
                    ? ThreatBusDuckAttackSeconds
                    : ThreatBusDuckReleaseSeconds;
                float duckBlend = 1f - math.exp(-deltaTime / math.max(duckTimeSeconds, 0.0001f));
                _threatBusDuck01 = math.lerp(_threatBusDuck01, targetDuck01, duckBlend);
            }

            mixer.SetFloat(_bedDuckDbParameter, math.lerp(0f, ThreatBusDuckMaximumDb, _threatBusDuck01));
        }

        private void ApplyParasiteRoomAcousticState(float deltaTime)
        {
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer == null)
                return;

            float target01 = math.saturate(_parasiteRoomTarget01);
            if (deltaTime <= 0f)
            {
                _parasiteRoomSmoothed01 = target01;
            }
            else
            {
                float sharpness = target01 > _parasiteRoomSmoothed01
                    ? ParasiteRoomAudioAttackSharpness
                    : ParasiteRoomAudioReleaseSharpness;
                float blend = 1f - math.exp(-sharpness * deltaTime);
                _parasiteRoomSmoothed01 = math.lerp(_parasiteRoomSmoothed01, target01, blend);
            }

            float cutoffHz = math.lerp(
                math.max(20f, _parasiteRoomHealthyCutoffHz),
                math.max(20f, _parasiteRoomInfectedCutoffHz),
                _parasiteRoomSmoothed01);
            float organicGainDb = math.lerp(
                _parasiteOrganicLayerSilentDb,
                _parasiteOrganicLayerMaxDb,
                _parasiteRoomSmoothed01);

            if (!string.IsNullOrWhiteSpace(_parasiteRoomLowPassCutoffParameter) &&
                math.abs(cutoffHz - _lastParasiteRoomLowPassCutoffHz) > 1f)
            {
                mixer.SetFloat(_parasiteRoomLowPassCutoffParameter, cutoffHz);
                _lastParasiteRoomLowPassCutoffHz = cutoffHz;
            }

            if (!string.IsNullOrWhiteSpace(_parasiteOrganicLayerGainParameter) &&
                math.abs(organicGainDb - _lastParasiteOrganicLayerGainDb) > 0.05f)
            {
                mixer.SetFloat(_parasiteOrganicLayerGainParameter, organicGainDb);
                _lastParasiteOrganicLayerGainDb = organicGainDb;
            }
        }

        private AudioMixer ResolveThreatDuckingMixer()
        {
            if (_routingMixer != null)
                return _routingMixer;

            AudioMixerGroup bedGroup = ResolvedBedBusGroup;
            if (bedGroup != null && bedGroup.audioMixer != null)
                return bedGroup.audioMixer;

            return _ambientGroup != null ? _ambientGroup.audioMixer : null;
        }

        private static bool IsThreatClip(AudioClip clip)
        {
            if (clip == null)
                return false;

            string clipName = clip.name;
            return ContainsTokenInsensitive(clipName, "leviathan") ||
                   ContainsTokenInsensitive(clipName, "roar") ||
                   ContainsTokenInsensitive(clipName, "threat") ||
                   ContainsTokenInsensitive(clipName, "predator") ||
                   ContainsTokenInsensitive(clipName, "shriek");
        }

        private static bool IsBedClip(AudioClip clip)
        {
            if (clip == null)
                return false;

            string clipName = clip.name;
            return ContainsTokenInsensitive(clipName, "ambient") ||
                   ContainsTokenInsensitive(clipName, "ocean") ||
                   ContainsTokenInsensitive(clipName, "water") ||
                   ContainsTokenInsensitive(clipName, "current") ||
                   ContainsTokenInsensitive(clipName, "bed") ||
                   ContainsTokenInsensitive(clipName, "drone");
        }

        private static bool ContainsTokenInsensitive(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void MarkWorldSourceActive(int sourceIndex)
        {
            if (_activeWorldSlots == null || sourceIndex < 0 || sourceIndex >= _activeWorldSlots.Length)
                return;

            if (_activeWorldSlots[sourceIndex] >= 0)
                return;

            int insertIndex = _activeWorldCount;
            if (_activeWorldIndices == null || insertIndex >= _activeWorldIndices.Length)
                return;

            _activeWorldIndices[insertIndex] = sourceIndex;
            _activeWorldSlots[sourceIndex] = insertIndex;
            _activeWorldCount = insertIndex + 1;
        }

        private void RemoveWorldSourceActive(int sourceIndex)
        {
            if (_activeWorldSlots == null || sourceIndex < 0 || sourceIndex >= _activeWorldSlots.Length)
                return;

            int slot = _activeWorldSlots[sourceIndex];
            if (slot < 0 || slot >= _activeWorldCount)
                return;

            int lastSlot = _activeWorldCount - 1;
            int movedIndex = _activeWorldIndices[lastSlot];
            _activeWorldIndices[slot] = movedIndex;
            if (movedIndex >= 0 && movedIndex < _activeWorldSlots.Length)
                _activeWorldSlots[movedIndex] = slot;
            _activeWorldIndices[lastSlot] = -1;
            _activeWorldSlots[sourceIndex] = -1;
            _activeWorldCount = lastSlot;
        }

        private void AdvanceAcousticRadarDecayCadence(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            _radarDecayAccumulator += deltaTime;
            while (_radarDecayAccumulator >= AcousticRadarDecayIntervalSeconds)
            {
                _radarDecayAccumulator -= AcousticRadarDecayIntervalSeconds;
                DecayAcousticRadarBins();
                DecayAcousticRadarGrid();
            }
        }

        private void DecayAcousticRadarBins()
        {
            if (!_acousticRadarIntensityBins.IsCreated)
                return;

            for (int i = 0; i < _acousticRadarIntensityBins.Length; i++)
                _acousticRadarIntensityBins[i] *= AcousticRadarDecayFactorPerSlowTick;
        }

        private void DecayAcousticRadarGrid()
        {
            if (!_acousticRadarGrid.IsCreated)
                return;

            for (int i = 0; i < _acousticRadarGrid.Length; i++)
                _acousticRadarGrid[i] *= AcousticRadarDecayFactorPerSlowTick;
        }

        private void ResetAcousticRadarBins()
        {
            if (!_acousticRadarIntensityBins.IsCreated)
                return;

            for (int i = 0; i < _acousticRadarIntensityBins.Length; i++)
                _acousticRadarIntensityBins[i] = 0f;
        }

        private void ResetAcousticRadarGrid()
        {
            if (!_acousticRadarGrid.IsCreated)
                return;

            for (int i = 0; i < _acousticRadarGrid.Length; i++)
                _acousticRadarGrid[i] = 0f;
        }

        private void ResetNearestRadarEmitterScratch()
        {
            if (_radarNearestEmitterDistanceSq == null || _radarNearestEmitterAmplitudes == null || _radarNearestEmitterRoots == null)
                return;

            int limit = _radarNearestEmitterDistanceSq.Length;
            for (int i = 0; i < limit; i++)
            {
                _radarNearestEmitterDistanceSq[i] = float.MaxValue;
                _radarNearestEmitterAmplitudes[i] = 0f;
                _radarNearestEmitterRoots[i] = null;
            }
        }

        private void DepositImpactRadarSamples(Transform listener, float now)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                DepositAcousticRadarSample(listener, emitter.Position, amplitude);
            }
        }

        private void QueueImpactRadarEmitters(Vector3 listenerAbsolutePosition, Vector3 listenerWorldPosition, float now)
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                QueueNearestRadarEmitter(listenerAbsolutePosition, listenerWorldPosition, emitter.Position, amplitude, null);
            }
        }

        private void DepositAcousticRadarSample(Transform listener, Vector3 sourcePosition, float amplitude)
        {
            if (listener == null || !_acousticRadarIntensityBins.IsCreated || !(amplitude > 0f))
                return;

            Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
            float azimuthDegrees = math.degrees(math.atan2(listenerLocalPosition.x, listenerLocalPosition.z));
            if (azimuthDegrees < 0f)
                azimuthDegrees += AcousticRadarBinCount;

            int radialIndex = math.clamp((int)math.floor(azimuthDegrees), 0, AcousticRadarBinCount - 1);
            float distance = math.sqrt(ResolveAbsoluteDistanceSqr(listener, sourcePosition));
            float falloff = 1f - math.saturate(distance / AcousticRadarDistanceRangeMeters);
            float intensity = math.saturate(amplitude * falloff);
            _acousticRadarIntensityBins[radialIndex] = math.max(_acousticRadarIntensityBins[radialIndex], intensity);
        }

        private void QueueNearestRadarEmitter(
            Vector3 listenerAbsolutePosition,
            Vector3 listenerWorldPosition,
            Vector3 sourcePosition,
            float amplitude,
            Transform sourceRoot)
        {
            if (_radarNearestEmitterDistanceSq == null || _radarNearestEmitterPositions == null || _radarNearestEmitterRoots == null || !(amplitude > 0f))
                return;

            float distanceSq = ResolveAbsoluteDistanceSqr(listenerAbsolutePosition, sourcePosition);
            int replaceIndex = -1;
            float farthestDistanceSq = -1f;
            int limit = math.min(
                AcousticRadarNearestEmitterLimit,
                math.min(_radarNearestEmitterDistanceSq.Length, math.min(_radarNearestEmitterPositions.Length, _radarNearestEmitterAmplitudes.Length)));
            for (int i = 0; i < limit; i++)
            {
                if (_radarNearestEmitterDistanceSq[i] == float.MaxValue)
                {
                    replaceIndex = i;
                    break;
                }

                if (_radarNearestEmitterDistanceSq[i] > farthestDistanceSq)
                {
                    farthestDistanceSq = _radarNearestEmitterDistanceSq[i];
                    replaceIndex = i;
                }
            }

            if (replaceIndex < 0)
                return;

            if (_radarNearestEmitterDistanceSq[replaceIndex] != float.MaxValue && distanceSq >= _radarNearestEmitterDistanceSq[replaceIndex])
                return;

            _radarNearestEmitterPositions[replaceIndex] = sourcePosition;
            _radarNearestEmitterAmplitudes[replaceIndex] = amplitude;
            _radarNearestEmitterDistanceSq[replaceIndex] = distanceSq;
            _radarNearestEmitterRoots[replaceIndex] = sourceRoot;

            if (_resolvedAcousticOcclusionLayerMask != 0)
            {
                AcousticOcclusionUtility.PrimeOcclusionPath(
                    sourcePosition,
                    listenerWorldPosition,
                    _resolvedAcousticOcclusionLayerMask,
                    sourceRoot,
                    _listenerTransform != null ? _listenerTransform.root : null);
            }
        }

        private void AccumulateNearestRadarGrid(Transform listener)
        {
            if (listener == null || !_acousticRadarGrid.IsCreated || _radarNearestEmitterDistanceSq == null)
                return;

            Vector3 listenerWorldPosition = listener.position;
            int limit = math.min(
                AcousticRadarNearestEmitterLimit,
                math.min(_radarNearestEmitterDistanceSq.Length, math.min(_radarNearestEmitterPositions.Length, _radarNearestEmitterAmplitudes.Length)));
            for (int i = 0; i < limit; i++)
            {
                float distanceSq = _radarNearestEmitterDistanceSq[i];
                if (distanceSq == float.MaxValue)
                    continue;

                Vector3 sourcePosition = _radarNearestEmitterPositions[i];
                float amplitude = _radarNearestEmitterAmplitudes[i];
                if (!(amplitude > 0f))
                    continue;

                Vector3 listenerLocalPosition = listener.InverseTransformPoint(sourcePosition);
                float azimuthDegrees = math.degrees(math.atan2(listenerLocalPosition.x, listenerLocalPosition.z));
                if (azimuthDegrees < 0f)
                    azimuthDegrees += 360f;

                float3 listenerLocalDirection = new float3(listenerLocalPosition.x, listenerLocalPosition.y, listenerLocalPosition.z);
                float inverseDirectionLength = math.rsqrt(math.max(math.lengthsq(listenerLocalDirection), 0.000001f));
                float3 direction = listenerLocalDirection * inverseDirectionLength;
                float elevationDegrees = math.degrees(math.asin(math.clamp(direction.y, -1f, 1f)));
                int azimuthIndex = math.clamp(
                    (int)math.floor((azimuthDegrees / 360f) * AcousticRadarGridAzimuthBins),
                    0,
                    AcousticRadarGridAzimuthBins - 1);
                float elevation01 = math.saturate((elevationDegrees - AcousticRadarElevationMinDegrees) /
                                                  math.max(AcousticRadarElevationMaxDegrees - AcousticRadarElevationMinDegrees, 0.0001f));
                int elevationIndex = math.clamp(
                    (int)math.floor(elevation01 * AcousticRadarGridElevationBins),
                    0,
                    AcousticRadarGridElevationBins - 1);
                float transmission = ResolveRadarTransmission(sourcePosition, listenerWorldPosition, _radarNearestEmitterRoots[i]);
                float energy = amplitude * transmission * math.rcp(math.max(distanceSq, 1f));
                int cellIndex = elevationIndex * AcousticRadarGridAzimuthBins + azimuthIndex;
                _acousticRadarGrid[cellIndex] += energy;
            }
        }

        private float ResolveRadarTransmission(Vector3 sourcePosition, Vector3 listenerWorldPosition, Transform sourceRoot)
        {
            if (_resolvedAcousticOcclusionLayerMask == 0)
                return 1f;

            if (AcousticOcclusionUtility.TryGetCachedOcclusionPath(
                    sourcePosition,
                    listenerWorldPosition,
                    _resolvedAcousticOcclusionLayerMask,
                    sourceRoot,
                    _listenerTransform != null ? _listenerTransform.root : null,
                    out AcousticOcclusionResult result))
            {
                return math.saturate(result.Transmission01);
            }

            return 1f;
        }

        private void UploadAcousticRadarGridBuffer()
        {
            if (!_acousticRadarGrid.IsCreated || _acousticRadarGridBuffer == null || _acousticRadarGridUploadScratch == null)
                return;

            int count = math.min(_acousticRadarGrid.Length, _acousticRadarGridUploadScratch.Length);
            for (int i = 0; i < count; i++)
                _acousticRadarGridUploadScratch[i] = _acousticRadarGrid[i];

            _acousticRadarGridBuffer.SetData(_acousticRadarGridUploadScratch, 0, 0, count);
        }

        private void RefreshListenerCaveState(Transform listener)
        {
            ResetListenerCaveState();
            if (listener == null)
                return;

            if (_worldCaveDirector == null)
                _worldCaveDirector = WorldCaveDirector.ActiveRuntimeInstance;

            if (_worldCaveDirector == null)
                return;

            _worldCaveDirector.CollectActiveVolumes(_caveVolumeBuffer);
            int volumeCount = _caveVolumeBuffer.Count;
            for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
            {
                HectonVoxelVolume volume = _caveVolumeBuffer[volumeIndex];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!TryResolveCaveInteriorFactor(volume, listener.position, out float caveInterior01))
                    continue;

                if (_listenerContainingCaveCount < _listenerContainingCaveVolumes.Length)
                    _listenerContainingCaveVolumes[_listenerContainingCaveCount++] = volume;
                _listenerCaveInterior01 = math.max(_listenerCaveInterior01, caveInterior01);
            }
        }

        private void ResetListenerCaveState()
        {
            _listenerCaveInterior01 = 0f;
            for (int i = 0; i < _listenerContainingCaveCount; i++)
                _listenerContainingCaveVolumes[i] = null;
            _listenerContainingCaveCount = 0;
        }

        private Vector3 ResolveListenerAbsoluteVelocity(Vector3 listenerAbsolutePosition, float deltaTime)
        {
            if (!_hasPreviousListenerAbsolutePosition || deltaTime <= 0.0001f)
            {
                _previousListenerAbsolutePosition = listenerAbsolutePosition;
                _hasPreviousListenerAbsolutePosition = true;
                return Vector3.zero;
            }

            Vector3 velocity = (listenerAbsolutePosition - _previousListenerAbsolutePosition) / deltaTime;
            _previousListenerAbsolutePosition = listenerAbsolutePosition;
            return velocity;
        }

        private void UpdateListenerWaterDensityMul(float deltaTime)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            _listenerPlayerMovement = playerContext != null ? playerContext.PlayerMovement as HectonPlayerMovement : null;
            float target = _listenerPlayerMovement != null && _listenerPlayerMovement.IsPlayerSubmerged ? 1f : 0f;
            if (deltaTime <= 0f)
            {
                _listenerWaterDensityMul = target;
                return;
            }

            float blendT = 1f - math.exp(-math.max(BinauralWaterBlendSharpness, 0.01f) * deltaTime);
            _listenerWaterDensityMul = math.lerp(_listenerWaterDensityMul, target, blendT);
        }

        private void UpdateManualDopplerPitch(
            int sourceIndex,
            AudioSource source,
            Vector3 listenerAbsolutePosition,
            Vector3 listenerVelocity,
            float deltaTime)
        {
            if (source == null ||
                _basePitches == null ||
                _smoothedDopplerRatios == null ||
                _previousAbsolutePositions == null ||
                sourceIndex < 0 ||
                sourceIndex >= _basePitches.Length)
            {
                return;
            }

            Vector3 sourceAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(source.transform.position);
            Vector3 sourceVelocity = Vector3.zero;
            if (deltaTime > 0.0001f)
                sourceVelocity = (sourceAbsolutePosition - _previousAbsolutePositions[sourceIndex]) / deltaTime;

            _currentAbsoluteVelocities[sourceIndex] = sourceVelocity;
            _previousAbsolutePositions[sourceIndex] = sourceAbsolutePosition;

            Vector3 listenerToSource = sourceAbsolutePosition - listenerAbsolutePosition;
            float targetRatio = 1f;
            float distanceSq = listenerToSource.sqrMagnitude;
            if (distanceSq > 0.0001f)
            {
                Vector3 direction = listenerToSource / math.sqrt(distanceSq);
                float relativeVelocity = Vector3.Dot(listenerVelocity - sourceVelocity, direction);
                float clampedRelativeVelocity = math.clamp(
                    relativeVelocity,
                    -SoundSpeedWaterMetersPerSecond * 0.9f,
                    SoundSpeedWaterMetersPerSecond * 0.9f);
                float numerator = math.max(
                    SoundSpeedWaterMetersPerSecond + clampedRelativeVelocity,
                    ManualDopplerMinimumDenominatorMetersPerSecond);
                float denominator = math.max(
                    SoundSpeedWaterMetersPerSecond - clampedRelativeVelocity,
                    ManualDopplerMinimumDenominatorMetersPerSecond);
                targetRatio = math.clamp(
                    numerator / denominator,
                    1f / ManualDopplerMaximumRatio,
                    ManualDopplerMaximumRatio);

                float previousRelativeVelocity = _previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length
                    ? _previousRelativeVelocities[sourceIndex]
                    : 0f;
                float velocityDelta = math.abs(clampedRelativeVelocity - previousRelativeVelocity);
                if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                    _previousRelativeVelocities[sourceIndex] = clampedRelativeVelocity;

                float smoothingDurationSeconds = ManualDopplerSmoothingSamples / ManualDopplerSampleRateHertz;
                float followT = velocityDelta > ManualDopplerVelocityJumpThresholdMetersPerSecond
                    ? math.saturate(math.max(deltaTime, 0f) / math.max(smoothingDurationSeconds, 0.0001f))
                    : 1f - math.exp(-ManualDopplerFollowSharpness * math.max(deltaTime, 0f));
                float smoothedRatio = math.lerp(_smoothedDopplerRatios[sourceIndex], targetRatio, followT);
                _smoothedDopplerRatios[sourceIndex] = smoothedRatio;
                source.pitch = ResolveSourcePitch(sourceIndex, source, smoothedRatio);
                return;
            }

            _smoothedDopplerRatios[sourceIndex] = 1f;
            if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                _previousRelativeVelocities[sourceIndex] = 0f;
            source.pitch = ResolveSourcePitch(sourceIndex, source, 1f);
        }

        private bool TryResolveCaveExternalLowPassCutoff(AudioSource source, Vector3 sourcePosition, out float cutoffFrequency)
        {
            cutoffFrequency = 22000f;
            AudioMixerGroup bedGroup = ResolvedBedBusGroup;
            if (source == null || bedGroup == null || source.outputAudioMixerGroup != bedGroup || _listenerContainingCaveCount <= 0)
                return false;

            if (IsInsideListenerContainingCave(sourcePosition))
                return false;

            cutoffFrequency = math.lerp(
                CaveExternalLowPassBoundaryCutoffHertz,
                CaveExternalLowPassDeepInteriorCutoffHertz,
                _listenerCaveInterior01);
            return true;
        }

        private bool IsInsideListenerContainingCave(Vector3 worldPosition)
        {
            for (int i = 0; i < _listenerContainingCaveCount; i++)
            {
                HectonVoxelVolume volume = _listenerContainingCaveVolumes[i];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                    continue;

                Vector3 localPosition = volume.transform.InverseTransformPoint(worldPosition);
                if (localBounds.Contains(localPosition))
                    return true;
            }

            return false;
        }

        private static bool TryResolveCaveInteriorFactor(HectonVoxelVolume volume, Vector3 viewerPositionWS, out float caveInterior01)
        {
            caveInterior01 = 0f;
            if (volume == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out Bounds localBounds))
                return false;

            Vector3 localViewerPosition = volume.transform.InverseTransformPoint(viewerPositionWS);
            if (!localBounds.Contains(localViewerPosition))
                return false;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            float distanceToWall = math.min(
                math.min(localViewerPosition.x - min.x, max.x - localViewerPosition.x),
                math.min(
                    math.min(localViewerPosition.y - min.y, max.y - localViewerPosition.y),
                    math.min(localViewerPosition.z - min.z, max.z - localViewerPosition.z)));
            caveInterior01 = math.saturate(distanceToWall / CaveInteriorReferenceDistanceMeters);
            return true;
        }

        private static float ResolveImpactEmitterAmplitude(ImpactEmitterSample emitter, float now)
        {
            if (!(emitter.ExpireAt > now) || !(emitter.Amplitude > ImpactEmitterMinimumAmplitude))
                return 0f;

            float lifetime = math.max(0.001f, emitter.ExpireAt - emitter.SpawnAt);
            float fade = math.saturate((emitter.ExpireAt - now) / lifetime);
            return emitter.Amplitude * fade;
        }

        private static float ResolveAbsoluteDistanceSqr(Transform listener, Vector3 sourcePosition)
        {
            Vector3 listenerAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(listener.position);
            Vector3 sourceAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(sourcePosition);
            return (listenerAbsolutePosition - sourceAbsolutePosition).sqrMagnitude;
        }

        private static float ResolveAbsoluteDistanceSqr(Vector3 listenerAbsolutePosition, Vector3 sourcePosition)
        {
            Vector3 sourceAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(sourcePosition);
            return (listenerAbsolutePosition - sourceAbsolutePosition).sqrMagnitude;
        }

        private int Acquire2DSourceIndex()
        {
            if (_pool2D == null || _pool2DSize <= 0)
                return -1;

            int oldestIndex = 0;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < _pool2DSize; i++)
            {
                if (!_pool2D[i].isPlaying)
                {
                    return i;
                }

                if (_startTimes2D[i] < oldestTime)
                {
                    oldestTime = _startTimes2D[i];
                    oldestIndex = i;
                }
            }

            _pool2D[oldestIndex].Stop();

#if UNITY_EDITOR
            Debug.LogFormat(this, "[SpatialAudioManager] 2D pool full ({0}). Evicting index {1}.", _pool2DSize, oldestIndex);
#endif

            return oldestIndex;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR VALIDATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private Transform ResolveWorldPoolRoot()
        {
            return _worldPoolRoot != null ? _worldPoolRoot : transform;
        }

        private Transform ResolveHelmetPoolRoot()
        {
            return _helmetPoolRoot != null ? _helmetPoolRoot : transform;
        }

        private bool ShouldPartitionAuthoredPoolsBySpatialBlend()
        {
            return _worldPoolRoot == null || _helmetPoolRoot == null || _worldPoolRoot == _helmetPoolRoot;
        }

        private int CountAuthoredWorldPoolNodes(Transform root)
        {
            if (root == null)
                return 0;

            int count = 0;
            CountAuthoredWorldPoolNodesRecursive(root, ShouldPartitionAuthoredPoolsBySpatialBlend(), ref count);
            return count;
        }

        private int CountAuthoredHelmetPoolNodes(Transform root)
        {
            if (root == null)
                return 0;

            int count = 0;
            CountAuthoredHelmetPoolNodesRecursive(root, ShouldPartitionAuthoredPoolsBySpatialBlend(), ref count);
            return count;
        }

        private static void CountAuthoredWorldPoolNodesRecursive(Transform current, bool partitionBySpatialBlend, ref int count)
        {
            if (current == null)
                return;

            if (current.TryGetComponent(out AudioSource source) &&
                current.TryGetComponent(out AudioLowPassFilter _) &&
                (!partitionBySpatialBlend || source.spatialBlend > 0.5f))
            {
                count++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
                CountAuthoredWorldPoolNodesRecursive(current.GetChild(i), partitionBySpatialBlend, ref count);
        }

        private static void CountAuthoredHelmetPoolNodesRecursive(Transform current, bool partitionBySpatialBlend, ref int count)
        {
            if (current == null)
                return;

            if (current.TryGetComponent(out AudioSource source) &&
                (!partitionBySpatialBlend || source.spatialBlend <= 0.5f))
            {
                count++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount; i++)
                CountAuthoredHelmetPoolNodesRecursive(current.GetChild(i), partitionBySpatialBlend, ref count);
        }

        private void BindAuthoredWorldPoolRecursive(Transform current, ref int index)
        {
            if (current == null || index >= _poolSize)
                return;

            bool partitionBySpatialBlend = ShouldPartitionAuthoredPoolsBySpatialBlend();
            if (current.TryGetComponent(out AudioSource source) &&
                current.TryGetComponent(out AudioLowPassFilter lowPassFilter) &&
                (!partitionBySpatialBlend || source.spatialBlend > 0.5f))
            {
                ConfigureAs3D(source);
                lowPassFilter.enabled = false;
                lowPassFilter.cutoffFrequency = 22000f;
                source.playOnAwake = false;
                source.loop = false;

                _pool[index] = source;
                _lowPassFilters[index] = lowPassFilter;
                _startTimes[index] = -1f;
                _baseVolumes[index] = 0f;
                _arrivalTimes[index] = -1f;
                _haasReleaseTimes[index] = 0f;
                _nextTierUpdateTimes[index] = 0f;
                _audioLodTiers[index] = AudioLodTier.Tier0Full;
                index++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount && index < _poolSize; i++)
                BindAuthoredWorldPoolRecursive(current.GetChild(i), ref index);
        }

        private void BindAuthoredHelmetPoolRecursive(Transform current, ref int index)
        {
            if (current == null || index >= _pool2DSize)
                return;

            bool partitionBySpatialBlend = ShouldPartitionAuthoredPoolsBySpatialBlend();
            if (current.TryGetComponent(out AudioSource source) &&
                (!partitionBySpatialBlend || source.spatialBlend <= 0.5f))
            {
                ConfigureAs2D(source);
                source.playOnAwake = false;
                source.loop = false;

                _pool2D[index] = source;
                _startTimes2D[index] = -1f;
                index++;
            }

            int childCount = current.childCount;
            for (int i = 0; i < childCount && index < _pool2DSize; i++)
                BindAuthoredHelmetPoolRecursive(current.GetChild(i), ref index);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            _poolSize = math.clamp(_poolSize, 4, 32);
            _pool2DSize = math.clamp(_pool2DSize, 2, 16);

            if (_minDistance < 0.1f) _minDistance = 0.1f;
            if (_maxDistance < _minDistance) _maxDistance = _minDistance + 1f;

            if (_worldPoolRoot == null)
                _worldPoolRoot = transform;

            if (_helmetPoolRoot == null)
                _helmetPoolRoot = transform;
        }

        /// <summary>
        /// Ð’Ð¸Ð·ÑƒÐ°Ð»Ð¸Ð·Ð°Ñ†Ð¸Ñ Ð¿ÑƒÐ»Ð° Ð² Scene View Ð´Ð»Ñ Ð¾Ñ‚Ð»Ð°Ð´ÐºÐ¸.
        /// ÐŸÐ¾ÐºÐ°Ð·Ñ‹Ð²Ð°ÐµÑ‚ Ð¿Ð¾Ð·Ð¸Ñ†Ð¸Ð¸ Ð°ÐºÑ‚Ð¸Ð²Ð½Ñ‹Ñ… Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð².
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (_pool == null) return;

            for (int i = 0; i < _poolSize; i++)
            {
                if (_pool[i] == null) continue;

                if (_pool[i].isPlaying)
                {
                    Gizmos.color = new Color(0f, 1f, 0.6f, 0.7f); // Biolum green
                    Gizmos.DrawWireSphere(_pool[i].transform.position, 0.3f);
                    Gizmos.DrawLine(transform.position, _pool[i].transform.position);
                }
                else
                {
                    Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.2f);
                    Gizmos.DrawWireSphere(_pool[i].transform.position, 0.1f);
                }
            }
        }
#endif
    }

    /// <summary>
    /// Caption request wrapper for contextual spatial-audio captions.
    /// Producers are expected to pass a cached/prelocalized caption string.
    /// </summary>
    public readonly struct AudioCaptionRequest
    {
        public AudioCaptionRequest(string captionText, Vector3 worldPosition, float durationSeconds, float intensity)
        {
            CaptionText = captionText;
            WorldPosition = worldPosition;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
        }

        /// <summary>Cached/prelocalized caption text shown by the HUD.</summary>
        public string CaptionText { get; }

        /// <summary>World-space origin used to position the caption around the reticle.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Visible duration in seconds.</summary>
        public float DurationSeconds { get; }

        /// <summary>Normalized caption strength in the 0..1 range.</summary>
        public float Intensity { get; }
    }

    /// <summary>
    /// Unmanaged caption payload carried by the deferred audio-caption lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioCaptionPayload
    {
        public Vector3 WorldPosition;
        public float DurationSeconds;
        public float Intensity;
        public uint CaptionHashId;
        public int ReferenceSlot;
        public ushort EventType;
        public ushort Reserved;
    }

    /// <summary>
    /// Listener for deferred spatial-audio caption requests.
    /// </summary>
    public interface IAudioCaptionEventListener
    {
        void OnAudioCaptionRequested(AudioCaptionRequest request);
    }

    /// <summary>
    /// NativeQueue-backed main-thread event bus for spatial-audio captions.
    /// Audio systems publish semantic cue text here; HUD overlays render it.
    /// </summary>
    public static class AudioCaptionEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 32;
        private const int ReferenceSlotCapacity = 32;
        private const ushort CaptionRequestedEventType = 1;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents"));

        // COLD ALLOC: RegistryBucket<IAudioCaptionEventListener>[8] - audio caption listeners drained by SystemDispatcher LateUpdate - owner: AudioCaptionEvents
        private static readonly RegistryBucket<IAudioCaptionEventListener> _listeners = new RegistryBucket<IAudioCaptionEventListener>(ListenerCapacity);
        // COLD ALLOC: string[32] - managed caption text sidecar for unmanaged audio caption payloads - owner: AudioCaptionEvents
        private static readonly string[] _captionReferenceSlots = new string[ReferenceSlotCapacity];
        // COLD ALLOC: bool[32] - caption sidecar occupancy map prevents wrap overwrite before deferred flush - owner: AudioCaptionEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<AudioCaptionPayload> _pendingEvents;
        private static NativeQueue<AudioCaptionPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _lastOverflowWarningFrame = -1;
        private static bool _isDispatching;

        /// <summary>Number of caption payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioCaptionEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioCaptionEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _lastOverflowWarningFrame = -1;
            _isDispatching = false;
        }

        /// <summary>Registers one audio caption listener.</summary>
        public static void Register(IAudioCaptionEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>Unregisters one audio caption listener.</summary>
        public static void Unregister(IAudioCaptionEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>Flushes queued audio captions to registered UI listeners.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out AudioCaptionPayload payload))
                        break;

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    Dispatch(in payload);
                    ReleaseReferenceSlot(payload.ReferenceSlot);
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        /// <summary>
        /// Queues a caption request using a prelocalized text payload.
        /// </summary>
        public static void Raise(AudioCaptionRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.CaptionText))
                return;

            if (!TryReserveReferenceSlot(out int referenceSlot))
            {
                ReportOverflowOncePerFrame();
                return;
            }

            _captionReferenceSlots[referenceSlot] = request.CaptionText;
            Enqueue(new AudioCaptionPayload
            {
                WorldPosition = request.WorldPosition,
                DurationSeconds = request.DurationSeconds,
                Intensity = request.Intensity,
                CaptionHashId = unchecked((uint)LocHash.Compute(request.CaptionText)),
                ReferenceSlot = referenceSlot,
                EventType = CaptionRequestedEventType,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AudioCaptionPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioCaptionPayload>[32] - deferred spatial audio caption lane flushed by SystemDispatcher LateUpdate - owner: AudioCaptionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(AudioCaptionEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<AudioCaptionPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioCaptionPayload>[32] - next-frame spatial audio captions raised by caption listeners - owner: AudioCaptionEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(AudioCaptionEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool Enqueue(in AudioCaptionPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReleaseReferenceSlot(payload.ReferenceSlot);
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
            }
            else
            {
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }

            return true;
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out AudioCaptionPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }

        private static void Dispatch(in AudioCaptionPayload payload)
        {
            if (payload.EventType != CaptionRequestedEventType ||
                !IsValidReferenceSlot(payload.ReferenceSlot))
            {
                return;
            }

            string captionText = _captionReferenceSlots[payload.ReferenceSlot];
            if (string.IsNullOrWhiteSpace(captionText))
                return;

            AudioCaptionRequest request = new AudioCaptionRequest(
                captionText,
                payload.WorldPosition,
                payload.DurationSeconds,
                payload.Intensity);

            IAudioCaptionEventListener[] rawArray = _listeners.RawArray;
            int count = _listeners.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                IAudioCaptionEventListener listener = rawArray[i];
                if (listener != null)
                    listener.OnAudioCaptionRequested(request);
            }
        }

        private static bool TryReserveReferenceSlot(out int referenceSlot)
        {
            referenceSlot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            for (int probe = 0; probe < ReferenceSlotCapacity; probe++)
            {
                int candidateSlot = _referenceWriteIndex;
                _referenceWriteIndex++;
                if (_referenceWriteIndex >= ReferenceSlotCapacity)
                    _referenceWriteIndex = 0;

                if (_referenceSlotOccupied[candidateSlot])
                    continue;

                referenceSlot = candidateSlot;
                _referenceSlotOccupied[referenceSlot] = true;
                _referencePendingCount++;
                return true;
            }

            return false;
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            if (!IsValidReferenceSlot(referenceSlot) || !_referenceSlotOccupied[referenceSlot])
                return;

            _captionReferenceSlots[referenceSlot] = null;
            _referenceSlotOccupied[referenceSlot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static bool IsValidReferenceSlot(int referenceSlot)
        {
            return (uint)referenceSlot < ReferenceSlotCapacity;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _captionReferenceSlots[i] = null;
                _referenceSlotOccupied[i] = false;
            }
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }
    }
}
