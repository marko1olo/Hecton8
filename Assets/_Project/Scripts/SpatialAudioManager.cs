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
using System.IO;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio.Propagation;
using Hecton8.Caves;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using CoreAudioEvent = Hecton8.Core.AudioEvent;

namespace Hecton8.Audio
{
    /// <summary>
    /// Ð¦ÐµÐ½Ñ‚Ñ€Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ° Ñ Ð¿ÑƒÐ»Ð¸Ð½Ð³Ð¾Ð¼.
    /// Runtime audio service accessed through Hecton8.Core.GlobalRegistry.Audio.
    /// Zero-GC Ð² hot path. Ð–Ñ‘ÑÑ‚ÐºÐ¸Ð¹ Ð»Ð¸Ð¼Ð¸Ñ‚ Ð¾Ð´Ð½Ð¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ñ… Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð².
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour, IAudioService, IUpdatable, ISlowTickable, ILateFrameTickable, IPhysicsImpactEventListener, IPhysicsAcousticImpulseEventListener, IRepairDroneTorchAcousticListener, IFatalPressureImplosionEventListener, IServiceHeartbeat, IServiceShutdown
    {
        private const float SoundSpeedWaterMetersPerSecond = 1480f;
        private const float MassiveDistanceFixedAudioDelayMeters = 740f;
        private const float MassiveDistanceFixedAudioDelaySeconds = 0.5f;
        private const float ThermalShimmerMaximumPitchRatio = 0.018f;
        private const float TimeDilationAudioMinimumPitchRatio = 0.72f;
        private const float InverseTwoPi = 0.15915494309f;
        private const float HaasArrivalWindowSeconds = 0.035f;
        private const float HaasReleaseThresholdSeconds = 0.04f;
        private const float HaasSecondarySpatialBlendFactor = 0.2f;
        private const float HaasBlendSharpness = 14f;
        private const float Tier0FullDspDistanceMeters = 15f;
        private const float Tier1ReducedDspDistanceMeters = 40f;
        private const float Tier1UpdateIntervalSeconds = 0.033333335f;
        private const float Tier1LowPassCutoffHertz = 1800f;
        private const float CinematicSourceMuffleUpdateIntervalSeconds = 0.2f;
        private const float CinematicSourceMuffleReferenceDistanceMeters = Tier0FullDspDistanceMeters;
        private const float CinematicSourceMuffleReferenceDistanceSq =
            CinematicSourceMuffleReferenceDistanceMeters * CinematicSourceMuffleReferenceDistanceMeters;
        private const float CinematicFarMuffleNearCutoffHertz = 3200f;
        private const float CinematicFarMuffleFarCutoffHertz = 900f;
        private const float CinematicFarMuffleNearTransmission = 0.96f;
        private const float CinematicFarMuffleFarTransmission = 0.72f;
        private const float CinematicZoneMuffleCutoffHertz = 800f;
        private const float CinematicZoneMuffleTransmission = 0.25118864f; // -12 dB.
        private const float StereoPanDistanceNormalizationMeters = 15f;
        private const int MaxImpactRadarEmitters = 16;
        private const float ImpactEmitterLifetimeMinSeconds = 0.18f;
        private const float ImpactEmitterLifetimeMaxSeconds = 0.42f;
        private const float ImpactEmitterAmplitudeScale = 0.75f;
        private const float ImpactEmitterMinimumAmplitude = 0.02f;
        private const int AcousticRadarBinCount = 360;
        private const float AcousticRadarBinCountInv = 0.0027777778f;
        private const float AcousticRadarDecayFactorPerSlowTick = 0.75f;
        private const float AcousticRadarDecayIntervalSeconds = 0.1f;
        private const float AcousticRadarDistanceRangeMeters = 180f;
        private const float AcousticRadarEnergyEpsilon = 0.000001f;
        private const int AcousticRadarGridAzimuthBins = 8;
        private const int AcousticRadarGridElevationBins = 4;
        private const int AcousticRadarGridCellCount = AcousticRadarGridAzimuthBins * AcousticRadarGridElevationBins;
        private const int AcousticRadarNearestEmitterLimit = 12;
        private const byte WorldSourceBusFlagThreat = 1 << 0;
        private const byte WorldSourceBusFlagBed = 1 << 1;
        private const int AudioClipRouteCacheCapacity = 128;
        private const byte AudioClipRouteFlagThreat = 1 << 0;
        private const byte AudioClipRouteFlagBed = 1 << 1;
        private const int MaxListenerContainingCaveVolumes = 8;
        private const int MaxCachedBaseInteriorMuffleZones = 32;
        private const float CaveExternalLowPassBoundaryCutoffHertz = 2600f;
        private const float CaveExternalLowPassDeepInteriorCutoffHertz = 1100f;
        private const float CaveInteriorReferenceDistanceMeters = 6f;
        private const float CaveInteriorReferenceDistanceMetersInv = 0.16666667f;
        private const float ManualDopplerFollowSharpness = 10f;
        private const float ManualDopplerMaximumRatio = 1.2f;
        private const float ManualDopplerMaximumRatioInv = 0.8333333f;
        private const float ManualDopplerMinimumDenominatorMetersPerSecond = 32f;
        private const float ManualDopplerVelocityJumpThresholdMetersPerSecond = 10f;
        private const float ManualDopplerSmoothingSamples = 128f;
        private const float ManualDopplerSampleRateHertz = 48000f;
        private const float ManualDopplerSampleRateHertzInv = 0.000020833333f;
        private const float RearHemisphereLowPassStartDot = -0.12f;
        private const float RearHemisphereLowPassFullDot = -0.92f;
        private const float RearHemisphereLowPassMaximumCutoffHertz = 18000f;
        private const float RearHemisphereLowPassMinimumCutoffHertz = 3200f;
        private const float RearHemisphereLowPassCutoffRangeInv = 0.00006756757f;
        private const float BinauralWaterBlendSharpness = 7f;
        private const float BinauralMinimumItdSeconds = 0.0001f;
        private const float BinauralMaximumItdSeconds = 0.0007f;
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
        private const int MaxWeatherAudioEventsPerFrame = 10;
        private const float MassiveDistanceFixedAudioDelayMetersSq =
            MassiveDistanceFixedAudioDelayMeters * MassiveDistanceFixedAudioDelayMeters;
        private const float StormRoarShedGainPerEvent = 0.08f;
        private const float StormRoarShedRiseSharpness = 18f;
        private const float StormRoarShedReleaseSharpness = 3.5f;
        private const float GlobalWindHowlReferenceSpeedSq = 784f;
        private const float FatalPressureImplosionEventVolume = 0.96f;
        private const float FatalPressureImplosionEventPitch = 0.84f;
        private const float FatalPressureImplosionTraumaRangeMeters = 220f;
        private const float FatalPressureImplosionTraumaImpulse = 18f;
        private const float FatalPressureImplosionTraumaWeight = 0.82f;
        private const float PoolFullEditorLogIntervalSeconds = 5f;
        private const float NullClipEditorLogIntervalSeconds = 5f;
        private const int MaxQueuedAudioEvents = 32;
        private const int MaxQueuedSoundEmissionSignals = 32;
        private const int AcousticPortalMaxNodes = AcousticPortalConstants.MaxPathNodes;
        private const int AcousticPortalMaxEdges = AcousticPortalConstants.MaxPathEdges;
        private const int AcousticPortalCacheCapacity = 16;
        private const float AcousticPortalCacheReuseDistanceMeters = 1f;
        private const float AcousticPortalHabitatAssociationMaxDistanceMeters = 45f;
        private const float AcousticPortalMaximumPlayDelaySeconds = 1.25f;
        private const string AcousticPortalDumpRelativePath = "Docs/AgentLogs/Dump_ACOUSTIC_PORTAL_PROPAGATION.bin";
        private const uint FirstAudioEventId = 1u;
        private const float SabineEquationConstant = 0.161f;
        private const float SabineMinimumRoomVolumeCubicMeters = 0.01f;
        private const float SabineMinimumSurfaceAreaSquareMeters = 0.5f;
        private const float SabineMinimumRt60Seconds = 0.12f;
        private const float SabineMaximumRt60Seconds = 10f;
        private const float SabineClosedVolumeScale = 0.18f;
        private const float SabineOpenVolumeScale = 0.75f;
        private const float SabineClosedSurfaceScale = 1.35f;
        private const float SabineOpenSurfaceScale = 0.85f;
        private const int SabineReverbDecayLutSize = 64;
        private const int SabineReverbDecayLutMaxIndex = SabineReverbDecayLutSize - 1;
        private const float SabineReverbDepthReferenceMeters = 6000f;
        private const float SabineReverbModuleVolumeReferenceCubicMeters = 6000f;

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

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal struct ActiveEmitterSample
        {
            public AbsoluteUniversePosition PositionAup;
            public Vector3 Position;
            public float Amplitude;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal struct ActiveImpactEmitterSample
        {
            public AbsoluteUniversePosition PositionAup;
            public float Amplitude;
        }

        [StructLayout(LayoutKind.Sequential, Size = 64)]
        internal struct BinauralEmitterTelemetry
        {
            public Vector3 Position;
            public float DistanceMeters;
            public float AzimuthRadians;
            public float RightDot;
            public float ItdSeconds;
            public float ShadowAmount01;
            public float ShadowCutoffHertz;
            public float Energy;
            public float WaterDensityMul;
            public int Valid;
        }

        [StructLayout(LayoutKind.Sequential, Size = 96)]
        private struct DelayedAudioEvent
        {
            public DelayedAudioEventKind Kind;
            public AbsoluteUniversePosition Aup;
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

        private struct AcousticPortalCacheEntry
        {
            public int Key;
            public int Frame;
            public byte Valid;
            public AcousticAup SourceAup;
            public AcousticAup ListenerAup;
            public AcousticPathResult Result;
        }

        [StructLayout(LayoutKind.Sequential, Size = 80)]
        private struct ImpactEmitterSample
        {
            public Vector3 Position;
            public float Amplitude;
            public AbsoluteUniversePosition PositionAup;
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
        [SerializeField] private int _poolSize = 32;

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

        [Tooltip("Optional pre-authored DSP route for encrypted PDA voiceover bit-crush. Falls back to Interface.")]
        [SerializeField] private AudioMixerGroup _encryptedVoiceGroup;

        [Tooltip("Ð“Ñ€ÑƒÐ¿Ð¿Ð° Ð´Ð»Ñ ÑÐ¼Ð±Ð¸ÐµÐ½Ñ‚Ð° (Ð¿Ð¾Ð´Ð²Ð¾Ð´Ð½Ñ‹Ð¹ Ð³ÑƒÐ», Ð´Ð°Ð²Ð»ÐµÐ½Ð¸Ðµ, etc).")]
        [SerializeField] private AudioMixerGroup _ambientGroup;

        [Tooltip("Threat bus for dominant hostile cues such as leviathan roars. Falls back to SFX when unassigned.")]
        [SerializeField] private AudioMixerGroup _threatGroup;

        [Tooltip("Bed bus for ambient world layers that should duck under threat activity. Falls back to Ambient when unassigned.")]
        [SerializeField] private AudioMixerGroup _bedGroup;

        [Tooltip("Optional mixer override for threat-driven bed ducking. If null, the bed or ambient mixer is used.")]
        [SerializeField] private AudioMixer _routingMixer;

        [Tooltip("Optional authored looping world-drone source used during menu-to-world transition. Runtime source creation is forbidden.")]
        [SerializeField] private AudioSource _worldDroneSource;

        [Tooltip("Optional authored world-drone clip assigned to the authored source if the source has no clip.")]
        [SerializeField] private AudioClip _worldDroneClip;

        [Header("Surface Weather Wind")]
        [Tooltip("Authored 2D looping wind source. Runtime source creation is forbidden.")]
        [SerializeField] private AudioSource _globalWindHowlSource;

        [Tooltip("Optional wind howl loop assigned to the authored source when it has no clip.")]
        [SerializeField] private AudioClip _globalWindHowlClip;

        [Tooltip("Optional authored low-pass filter on the wind source. Interior BaseModule/Submarine occlusion clamps it to 200 Hz.")]
        [SerializeField] private AudioLowPassFilter _globalWindHowlLowPass;

        [SerializeField, Range(0f, 1f)] private float _globalWindHowlMaxVolume = 0.42f;
        [SerializeField, Range(0f, 1f)] private float _globalWindHowlStormFloor = 0.22f;
        [SerializeField, Range(20f, 22000f)] private float _globalWindHowlInteriorLowPassCutoffHz = 200f;
        [SerializeField, Range(1000f, 22000f)] private float _globalWindHowlOpenLowPassCutoffHz = 22000f;
        [SerializeField, Range(0.1f, 24f)] private float _globalWindHowlFadeSharpness = 5f;
        [SerializeField, Range(0.1f, 32f)] private float _globalWindHowlOcclusionSharpness = 14f;

        [Tooltip("Exposed mixer parameter that attenuates the Bed bus in dB while Threat is active.")]
        [SerializeField] private string _bedDuckDbParameter = "BedDuckDb";

        [Tooltip("Exposed mixer parameter for the menu-to-world ambient drone gain in dB.")]
        [SerializeField] private string _worldDroneVolumeDbParameter = "WorldDroneDb";

        [Tooltip("Exposed mixer parameter for room low-pass cutoff while parasite growth is active.")]
        [SerializeField] private string _parasiteRoomLowPassCutoffParameter = "ParasiteRoomLowPassCutoffHz";

        [Tooltip("Exposed mixer parameter for the organic squelch ambient layer gain in dB.")]
        [SerializeField] private string _parasiteOrganicLayerGainParameter = "ParasiteOrganicLayerGainDb";

        [Tooltip("Exposed mixer parameter for narrative radio low-pass cutoff while deep or irradiated.")]
        [SerializeField] private string _narrativeRadioLowPassCutoffParameter = "NarrativeRadioLowPassCutoffHz";

        [SerializeField, Range(400f, 22000f)] private float _narrativeRadioOpenCutoffHz = 22000f;
        [SerializeField, Range(120f, 6000f)] private float _narrativeRadioMuffledCutoffHz = 900f;

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

        [Header("Queued Audio Events")]
        [Tooltip("One-based EventID table drained from the NativeQueue<AudioEvent>. Slot 0 resolves EventID 1.")]
        [SerializeField] private AudioClip[] _audioEventClipTable;

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
        private float[] _sourceCinematicMuffleLowPassCutoffs;
        private float[] _sourceCinematicMuffleTransmissions;
        private float[] _sourceCinematicMuffleNextUpdateTimes;
        private float[] _smoothedDopplerRatios;
        private float[] _previousRelativeVelocities;
        private float[] _arrivalTimes;
        private float[] _haasReleaseTimes;
        private float[] _nextTierUpdateTimes;
        private int _harvestAudioFrame = -1;
        private int _harvestAudioEventsThisFrame;
        private int _weatherAudioFrame = -1;
        private int _weatherAudioEventsThisFrame;
        private AudioLodTier[] _audioLodTiers;
        private AudioLowPassFilter[] _lowPassFilters;
        private Transform[] _worldSourceRoots;
        private byte[] _worldSourceBusFlags;
        private int[] _clipRouteCacheIds;
        private byte[] _clipRouteCacheFlags;
        private Vector3[] _previousAbsolutePositions;
        private Vector3[] _currentAbsoluteVelocities;
        private Vector3[] _activeWorldRuntimePositions;
        private int[] _activeWorldRuntimePositionFrames;
        private AbsoluteUniversePosition[] _activeWorldAups;
        private int[] _activeWorldAupFrames;
        private int[] _activeWorldIndices;
        private int[] _activeWorldSlots;
        private int _activeWorldCount;
        private bool _registeredUpdatable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _acousticOcclusionRuntimeAcquired;
        private Transform _listenerTransform;
        private Vector3 _previousListenerAbsolutePosition;
        private bool _hasPreviousListenerAbsolutePosition;
        private BinauralEmitterTelemetry _dominantBinauralEmitter;
        private NativeArray<float> _acousticRadarIntensityBins;
        private NativeArray<float> _acousticRadarGrid;
        private WorldCaveDirector _worldCaveDirector;
        private ComputeBuffer _acousticRadarGridBuffer;
        private bool _acousticRadarGridDirty;
        // COLD ALLOC: float[32] - CPU mirror for acoustic radar grid ComputeBuffer uploads - owner: SpatialAudioManager
        private float[] _acousticRadarGridUploadScratch;
        // COLD ALLOC: Vector3[12] - nearest-emitter radar accumulation positions - owner: SpatialAudioManager
        private Vector3[] _radarNearestEmitterPositions;
        // COLD ALLOC: AbsoluteUniversePosition[12] - nearest-emitter radar AUP cache avoiding repeated runtime conversions - owner: SpatialAudioManager
        private AbsoluteUniversePosition[] _radarNearestEmitterAups;
        // COLD ALLOC: float[12] - nearest-emitter radar accumulation amplitudes - owner: SpatialAudioManager
        private float[] _radarNearestEmitterAmplitudes;
        // COLD ALLOC: float[12] - nearest-emitter radar accumulation distance cache - owner: SpatialAudioManager
        private float[] _radarNearestEmitterDistanceSq;
        // COLD ALLOC: Transform[12] - nearest-emitter radar accumulation source roots for cached occlusion lookups - owner: SpatialAudioManager
        private Transform[] _radarNearestEmitterRoots;
        private int _resolvedAcousticOcclusionLayerMask;
        private readonly List<HectonVoxelVolume> _caveVolumeBuffer = new List<HectonVoxelVolume>(32); // COLD ALLOC: List<HectonVoxelVolume>[32] - cave AABB query scratch buffer - owner: SpatialAudioManager
        private readonly HectonVoxelVolume[] _listenerContainingCaveVolumes = new HectonVoxelVolume[MaxListenerContainingCaveVolumes]; // COLD ALLOC: HectonVoxelVolume[8] - listener cave containment cache - owner: SpatialAudioManager
        private readonly Bounds[] _listenerContainingCaveLocalBounds = new Bounds[MaxListenerContainingCaveVolumes]; // COLD ALLOC: Bounds[8] - listener cave local bounds cache - owner: SpatialAudioManager
        private readonly Matrix4x4[] _listenerContainingCaveWorldToLocal = new Matrix4x4[MaxListenerContainingCaveVolumes]; // COLD ALLOC: Matrix4x4[8] - listener cave transform cache - owner: SpatialAudioManager
        private readonly AbsoluteUniversePosition[] _baseInteriorMuffleAups = new AbsoluteUniversePosition[MaxCachedBaseInteriorMuffleZones]; // COLD ALLOC: AUP[32] - cached base-interior acoustic muffle centers - owner: SpatialAudioManager
        private readonly double[] _baseInteriorMuffleRadiusSq = new double[MaxCachedBaseInteriorMuffleZones]; // COLD ALLOC: double[32] - cached base-interior acoustic muffle radius squared - owner: SpatialAudioManager
        private int _listenerContainingCaveCount;
        private float _listenerCaveInterior01;
        private int _baseInteriorMuffleCount;
        private bool _listenerInsideBaseInteriorMuffle;
        private float _listenerSabineRt60Seconds;
        private float _listenerSabineVolumeCubicMeters;
        private float _listenerSabineSurfaceAreaSquareMeters;
        private float _threatBusDuck01;
        private float _parasiteRoomTarget01;
        private float _parasiteRoomSmoothed01;
        private float _lastParasiteRoomLowPassCutoffHz = -1f;
        private float _lastParasiteOrganicLayerGainDb = float.PositiveInfinity;
        private float _lastNarrativeRadioLowPassCutoffHz = -1f;
        private int _parasiteRoomAcousticCount;
        private float _worldDroneCrossfadeStartDb = -40f;
        private float _worldDroneCrossfadeTargetDb = -5f;
        private float _worldDroneCrossfadeDuration = 2.5f;
        private bool _worldDroneCrossfadeActive;
        private bool _hasBedDuckDbParameter;
        private bool _hasWorldDroneVolumeDbParameter;
        private bool _hasParasiteRoomLowPassCutoffParameter;
        private bool _hasParasiteOrganicLayerGainParameter;
        private bool _hasNarrativeRadioLowPassCutoffParameter;
        private float _nextWorldPoolFullEditorLogTime;
        private float _nextHelmetPoolFullEditorLogTime;
        private float _nextPlayAtPointNullClipLogTime;
        private float _nextPlayAtPointLowPassNullClipLogTime;
        private float _nextPlayStatic2DNullClipLogTime;
        private float _globalWindHowlVolume01;
        private float _globalWindHowlOcclusion01;
        private float _lastGlobalWindHowlCutoffHz = -1f;
        private float _stormRoarShedTarget01;
        private float _stormRoarShedCurrent01;
        private float _eclipseAcousticPitchShiftCents;
        private float _eclipseAcousticPitchRatio = 1f;
        private float _timeDilationWorldPitchRatio = 1f;
        private float _listenerWaterDensityMul;
        private float _radarDecayAccumulator;
        private HectonPlayerMovement _listenerPlayerMovement;
        private int _delayedAudioIngressCount;
        private NativeQueue<DelayedAudioEvent> _delayedAudioIngress;
        private NativeList<DelayedAudioEvent> _pendingDelayedAudioEvents;
        private int _audioEventQueueCount;
        private int _audioEventQueueDroppedCount;
        private NativeQueue<CoreAudioEvent> _audioEventQueue;
        private int _soundEmissionSignalQueueCount;
        private int _soundEmissionSignalDroppedCount;
        private NativeQueue<SoundEmissionSignal> _soundEmissionSignals;
        private NativeArray<AcousticPortalNode> _acousticPortalNodes;
        private NativeArray<AcousticPortalEdge> _acousticPortalEdges;
        private NativeArray<AcousticPathResult> _acousticPortalResult;
        private NativeArray<float> _acousticPortalCosts;
        private NativeArray<int> _acousticPortalCameFrom;
        private NativeArray<byte> _acousticPortalStates;
        private NativeList<int> _acousticPortalOpenSet;
        private NativeList<int> _acousticPortalClosedSet;
        private NativeArray<AcousticTelemetryEntry> _acousticPortalBlackBox;
        private int _acousticPortalBlackBoxCursor;
        private Vector3[] _acousticPortalWaypointScratch;
        private int[] _acousticHabitatNodeMap;
        private int[] _acousticHabitatQueue;
        private AcousticPortalCacheEntry[] _acousticPortalCache;
        private bool _isInitialized;
        private bool _runtimeResourcesInitialized;
        private bool _eventsSubscribed;
        private readonly ImpactEmitterSample[] _impactEmitters = new ImpactEmitterSample[MaxImpactRadarEmitters]; // COLD ALLOC: ImpactEmitterSample[16] - passive radar impact impulse cache - owner: SpatialAudioManager

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  LIFECYCLE
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            // Self-state only. Runtime resources are allocated by explicit bootstrap registration.
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask();
            RefreshMixerParameterAvailability();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            if (!_acousticOcclusionRuntimeAcquired)
            {
                AcousticOcclusionUtility.AcquireRuntime();
                _acousticOcclusionRuntimeAcquired = true;
            }

            if (_isInitialized)
                TrySubscribeAudioEvents();
        }

        private void OnDisable()
        {
            ShutdownServiceState(releaseRuntimeResources: false);
        }

        private void OnDestroy()
        {
            ShutdownServiceState(releaseRuntimeResources: true);
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState(releaseRuntimeResources: true);
        }

        private void ShutdownServiceState(bool releaseRuntimeResources)
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
            if (_registeredSlowTickable)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredSlowTickable = false;
            if (_registeredLateFrameTickable)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredLateFrameTickable = false;
            _hasPreviousListenerAbsolutePosition = false;
            _previousListenerAbsolutePosition = default;
            ResetAllWorldSourceState();
            ResetImpactEmitters();
            ResetAcousticRadarBins();
            ResetAcousticRadarGrid();
            ResetListenerCaveState();
            ResetBaseInteriorMuffleCache();
            ClearDelayedAudioEvents();
            ClearAudioEventQueue();
            _listenerPlayerMovement = null;
            _listenerWaterDensityMul = 0f;
            SetParasiteRoomAcousticLoad(0);
            SetEclipseAcousticPitchShiftCents(0f);
            ResetGlobalWindHowlState();
            _worldDroneCrossfadeActive = false;
            ApplyThreatBusDucking(0f, 0f);
            ApplyParasiteRoomAcousticState(0f);
            _radarDecayAccumulator = 0f;
            if (_acousticOcclusionRuntimeAcquired)
            {
                AcousticOcclusionUtility.ReleaseRuntime();
                _acousticOcclusionRuntimeAcquired = false;
            }

            if (releaseRuntimeResources)
            {
                ReleaseTelemetryCaches();
                _runtimeResourcesInitialized = false;
            }
        }

        /// <summary>
        /// True once the audio runtime has been registered into the global service locator.
        /// </summary>
        public bool IsInitialized => _isInitialized;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _isInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _isInitialized;

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
            _eclipseAcousticPitchRatio = ResolveCinematicPitchRatioFromCents(clampedCents);
            ApplyEclipsePitchShiftToActiveWorldSources();
        }

        /// <summary>
        /// Arms the authored ambient world-drone layer for a menu-to-world transition.
        /// </summary>
        public void BeginWorldDroneTransition(float startDb, float targetDb, float durationSeconds)
        {
            _worldDroneCrossfadeStartDb = math.clamp(startDb, -80f, 20f);
            _worldDroneCrossfadeTargetDb = math.clamp(targetDb, -80f, 20f);
            _worldDroneCrossfadeDuration = math.max(0.0001f, durationSeconds);
            _worldDroneCrossfadeActive = true;
            PrepareWorldDroneSource();
            ApplyWorldDroneGainDb(_worldDroneCrossfadeStartDb);
        }

        /// <summary>
        /// Locks ambient drone gain to the visual dither dissolve progress.
        /// </summary>
        public void SetWorldDroneTransitionProgress(float normalized)
        {
            if (!_worldDroneCrossfadeActive)
                return;

            float clamped = math.saturate(normalized);
            float eased = clamped * clamped * (3f - (2f * clamped));
            ApplyWorldDroneGainDb(math.lerp(_worldDroneCrossfadeStartDb, _worldDroneCrossfadeTargetDb, eased));
            if (clamped >= 1f)
                _worldDroneCrossfadeActive = false;
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
                TryRegisterSlowTickable();
                TryRegisterLateFrameTickable();
                return;
            }

            GlobalRegistry.RegisterAudioService(this);
            TryRegisterUpdatable();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
            _isInitialized = true;
        }

        private void EnsureRuntimeResourcesInitialized()
        {
            if (_runtimeResourcesInitialized)
                return;

            RefreshMixerParameterAvailability();
            InitializePool();
            InitializePool2D();
            InitializeTelemetryCaches();
            PrepareGlobalWindHowlSource();
            _runtimeResourcesInitialized = true;
        }

        private void TrySubscribeAudioEvents()
        {
            if (_eventsSubscribed)
                return;

            PhysicsEvents.Register(this);
            PhysicsEventBus.Register(this);
            FatalPressureImplosionEvents.Register(this);
            RepairDroneTorchAcousticEvents.Register(this);
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeAudioEvents()
        {
            if (!_eventsSubscribed)
                return;

            PhysicsEvents.Unregister(this);
            PhysicsEventBus.Unregister(this);
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
            float blendT = FastDecayBlend(HaasBlendSharpness, safeDeltaTime);
            float now = Time.unscaledTime;
            int currentFrame = Time.frameCount;
            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAbsolutePosition,
                out AbsoluteUniversePosition listenerAup);
            Vector3 listenerVelocity = Vector3.zero;
            if (hasListener)
            {
                listenerVelocity = ResolveListenerAbsoluteVelocity(listenerAbsolutePosition, safeDeltaTime);
            }
            else
            {
                _hasPreviousListenerAbsolutePosition = false;
                _previousListenerAbsolutePosition = default;
            }
            ResolveListenerBasis(listener, out float3 listenerRight, out float3 listenerUp, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;

            UpdateListenerWaterDensityMul(safeDeltaTime);
            UpdateStormRoarShedder(safeDeltaTime);
            UpdateGlobalWindHowl(safeDeltaTime);
            UpdateTimeDilationPitchScalar();
            float threatActivity = 0f;
            DecayImpactEmitters(now);
            AdvanceAcousticRadarDecayCadence(safeDeltaTime);
            ResetNearestRadarEmitterScratch();
            DrainDelayedAudioIngress();
            ProcessDelayedAudioEvents(hasListener, in listenerAup);
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

                if (!TryGetCachedActiveWorldRuntimePosition(sourceIndex, out Vector3 sourcePosition))
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                AbsoluteUniversePosition sourceAup = ResolveActiveWorldAup(sourceIndex, sourcePosition, currentFrame);
                Vector3 sourceAbsolutePosition = ToAbsoluteVector3(in sourceAup);
                CacheActiveWorldRuntimePosition(sourceIndex, sourcePosition, currentFrame);
                CacheActiveWorldAup(sourceIndex, in sourceAup, currentFrame);
                UpdateWorldSourceAudioLod(
                    sourceIndex,
                    source,
                    sourcePosition,
                    sourceAbsolutePosition,
                    in sourceAup,
                    listener,
                    in listenerAup,
                    listenerRuntimePosition,
                    listenerRight,
                    listenerAcousticForward,
                    listenerAbsolutePosition,
                    now,
                    false);
                if (listener != null)
                    UpdateManualDopplerPitch(sourceIndex, source, sourceAbsolutePosition, in sourceAup, in listenerAup, listenerVelocity, safeDeltaTime);
                if (!source.isPlaying)
                {
                    ResetWorldSourceState(sourceIndex, false);
                    continue;
                }

                float targetBlend = ResolveTargetSpatialBlend(sourceIndex, now);
                source.spatialBlend = math.lerp(source.spatialBlend, targetBlend, blendT);
                if (_haasReleaseTimes[sourceIndex] <= now && source.spatialBlend >= targetBlend - 0.001f)
                    _haasReleaseTimes[sourceIndex] = 0f;

                if (IsThreatWorldSource(sourceIndex))
                    threatActivity = math.max(threatActivity, math.saturate(source.volume));
                float sourceAmplitude = math.max(0f, source.volume);
                DepositAcousticRadarSample(
                    listener,
                    in listenerAup,
                    listenerRight,
                    listenerUp,
                    listenerForward,
                    sourcePosition,
                    in sourceAup,
                    sourceAmplitude);
                if (hasListener)
                    QueueNearestRadarEmitter(in listenerAup, listenerRuntimePosition, sourcePosition, in sourceAup, sourceAmplitude, ResolveWorldSourceRoot(sourceIndex));
                activeSlot++;
            }

            DepositImpactRadarSamples(listener, in listenerAup, listenerRight, listenerUp, listenerForward, now);
            if (hasListener)
            {
                QueueImpactRadarEmitters(in listenerAup, listenerRuntimePosition, now);
                AccumulateNearestRadarGrid(listener, listenerRuntimePosition, in listenerAup, listenerRight, listenerUp, listenerForward);
            }
            UploadAcousticRadarGridBuffer();
            UpdateDominantBinauralEmitterTelemetry(now, listener, in listenerAup, currentFrame);
            ApplyThreatBusDucking(threatActivity, safeDeltaTime);
            ApplyParasiteRoomAcousticState(safeDeltaTime);
        }

        /// <summary>
        /// Refreshes listener cave/reverb telemetry on the slow lane.
        /// </summary>
        public void SlowTick()
        {
            if (!TryResolveListenerFrame(
                    out Transform listener,
                    out Vector3 listenerRuntimePosition,
                    out _,
                    out AbsoluteUniversePosition listenerAup))
            {
                ResetListenerCaveState();
                ResetBaseInteriorMuffleCache();
                return;
            }

            RefreshListenerCaveState(listener, listenerRuntimePosition);
            RefreshBaseInteriorMuffleCache(in listenerAup);
        }

        /// <summary>
        /// Drains queued gameplay audio events after frame simulation.
        /// </summary>
        public void LateFrameTick()
        {
            AcousticOcclusionUtility.LateFrameTick();
            DrainSoundEmissionSignals();
            DrainAudioEventQueue();
        }

        /// <summary>True when the listener runtime position is inside a published cave/voxel volume bounding box.</summary>
        public bool IsListenerInsideCaveVolume => _listenerContainingCaveCount > 0;

        /// <summary>Normalized cave-interior depth from the current listener-containing volume cache.</summary>
        public float ListenerCaveInterior01 => math.saturate(_listenerCaveInterior01);

        /// <summary>Current listener cave RT60 calculated with RT60 = 0.161 * (Volume / SurfaceArea).</summary>
        public float ListenerSabineRt60Seconds => _listenerSabineRt60Seconds;

        /// <summary>Current listener cave open-cell volume estimate in cubic meters.</summary>
        public float ListenerSabineVolumeCubicMeters => _listenerSabineVolumeCubicMeters;

        /// <summary>Current listener cave exposed surface estimate in square meters.</summary>
        public float ListenerSabineSurfaceAreaSquareMeters => _listenerSabineSurfaceAreaSquareMeters;

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
            _parasiteRoomTarget01 = math.saturate(sanitizedCount * math.rcp((float)math.max(1, _parasiteRoomCountForFullInfection)));
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[SpatialAudioManager] World pool under-authored. Assign pre-authored AudioSource + AudioLowPassFilter children before play.",
                    this);
#endif
            }

            _poolSize = effectivePoolSize;
            _pool = new AudioSource[_poolSize]; // COLD ALLOC: AudioSource[_poolSize] - authored world-source pool references - owner: SpatialAudioManager
            _startTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - world-source playback start times - owner: SpatialAudioManager
            _baseVolumes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - world-source authored volume cache - owner: SpatialAudioManager
            _basePitches = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - world-source authored pitch cache - owner: SpatialAudioManager
            _sourceCinematicMuffleLowPassCutoffs = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source cinematic muffle LPF cache - owner: SpatialAudioManager
            _sourceCinematicMuffleTransmissions = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source cinematic muffle transmission cache - owner: SpatialAudioManager
            _sourceCinematicMuffleNextUpdateTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - throttled cinematic muffle refresh cadence - owner: SpatialAudioManager
            _smoothedDopplerRatios = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source Doppler smoothing state - owner: SpatialAudioManager
            _previousRelativeVelocities = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - per-source Doppler velocity cache - owner: SpatialAudioManager
            _arrivalTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - Haas arrival prediction cache - owner: SpatialAudioManager
            _haasReleaseTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - Haas masking release timestamps - owner: SpatialAudioManager
            _nextTierUpdateTimes = new float[_poolSize]; // COLD ALLOC: float[_poolSize] - audio LOD refresh cadence - owner: SpatialAudioManager
            _audioLodTiers = new AudioLodTier[_poolSize]; // COLD ALLOC: AudioLodTier[_poolSize] - world-source audio LOD cache - owner: SpatialAudioManager
            _lowPassFilters = new AudioLowPassFilter[_poolSize]; // COLD ALLOC: AudioLowPassFilter[_poolSize] - authored world-source LPF references - owner: SpatialAudioManager
            _worldSourceRoots = new Transform[_poolSize]; // COLD ALLOC: Transform[_poolSize] - authored world-source root cache for occlusion owner filtering - owner: SpatialAudioManager
            _worldSourceBusFlags = new byte[_poolSize]; // COLD ALLOC: byte[_poolSize] - per-source mixer role flags for hot-loop threat/bed decisions - owner: SpatialAudioManager
            _clipRouteCacheIds = new int[AudioClipRouteCacheCapacity]; // COLD ALLOC: int[128] - AudioClip instance-id route cache keys - owner: SpatialAudioManager
            _clipRouteCacheFlags = new byte[AudioClipRouteCacheCapacity]; // COLD ALLOC: byte[128] - AudioClip route flags avoiding repeated clip.name scans - owner: SpatialAudioManager
            _previousAbsolutePositions = new Vector3[_poolSize]; // COLD ALLOC: Vector3[_poolSize] - per-source absolute position history - owner: SpatialAudioManager
            _currentAbsoluteVelocities = new Vector3[_poolSize]; // COLD ALLOC: Vector3[_poolSize] - per-source absolute velocity cache - owner: SpatialAudioManager
            _activeWorldRuntimePositions = new Vector3[_poolSize]; // COLD ALLOC: Vector3[_poolSize] - per-frame active world-source runtime position cache - owner: SpatialAudioManager
            _activeWorldRuntimePositionFrames = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - validity frame for active world-source runtime position cache - owner: SpatialAudioManager
            _activeWorldAups = new AbsoluteUniversePosition[_poolSize]; // COLD ALLOC: AbsoluteUniversePosition[_poolSize] - per-source AUP cache shared by radar and binaural telemetry - owner: SpatialAudioManager
            _activeWorldAupFrames = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - validity frame for active world-source AUP cache - owner: SpatialAudioManager
            _activeWorldIndices = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse active world-source set - owner: SpatialAudioManager
            _activeWorldSlots = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse world-source slot lookup - owner: SpatialAudioManager
            for (int i = 0; i < AudioClipRouteCacheCapacity; i++)
                _clipRouteCacheIds[i] = int.MinValue;

            _activeWorldCount = 0;
            for (int i = 0; i < _poolSize; i++)
            {
                _activeWorldIndices[i] = -1;
                _activeWorldSlots[i] = -1;
                _basePitches[i] = 1f;
                _sourceCinematicMuffleLowPassCutoffs[i] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
                _sourceCinematicMuffleTransmissions[i] = 1f;
                _sourceCinematicMuffleNextUpdateTimes[i] = 0f;
                _smoothedDopplerRatios[i] = 1f;
                _previousRelativeVelocities[i] = 0f;
                _activeWorldRuntimePositionFrames[i] = -1;
                _activeWorldAupFrames[i] = -1;
            }

            if (_poolSize > 0)
            {
                int boundCount = 0;
                BindAuthoredWorldPoolRecursive(ResolveWorldPoolRoot(), ref boundCount);
            }

        }

        /// <summary>Ð¡Ð¾Ð·Ð´Ð°Ñ‘Ñ‚ Ð¿ÑƒÐ» 2D Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð² (Ð°Ð½Ð°Ð»Ð¾Ð³Ð¸Ñ‡Ð½Ð¾ 3D, Ð±ÐµÐ· PlayOneShot).</summary>
        private void InitializePool2D()
        {
            int effectivePool2DSize = math.min(_pool2DSize, CountAuthoredHelmetPoolNodes(ResolveHelmetPoolRoot()));
            if (effectivePool2DSize < _pool2DSize)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError(
                    "[SpatialAudioManager] Helmet/UI pool under-authored. Assign pre-authored 2D AudioSource children before play.",
                    this);
#endif
            }

            _pool2DSize = effectivePool2DSize;
            _pool2D = new AudioSource[_pool2DSize]; // COLD ALLOC: AudioSource[_pool2DSize] - authored helmet/UI source pool references - owner: SpatialAudioManager
            _startTimes2D = new float[_pool2DSize]; // COLD ALLOC: float[_pool2DSize] - helmet/UI source playback start times - owner: SpatialAudioManager

            if (_pool2DSize > 0)
            {
                int boundCount = 0;
                BindAuthoredHelmetPoolRecursive(ResolveHelmetPoolRoot(), ref boundCount);
            }

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
        /// Weather-only 3D cue path. Over-budget frames merge excess events into the 2D storm bed.
        /// </summary>
        public void PlayWeatherAtPoint(AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
            if (clip == null)
                return;

            if (!TryReserveWeatherAudioEvent(volume))
                return;

            PlayAtPoint(clip, position, volume, pitch, mixerGroup);
        }

        /// <summary>
        /// Weather-only low-pass cue path. Over-budget frames merge excess events into the 2D storm bed.
        /// </summary>
        public void PlayWeatherAtPointWithLowPass(
            AudioClip clip,
            Vector3 position,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            float lowPassCutoffHz)
        {
            if (clip == null)
                return;

            if (!TryReserveWeatherAudioEvent(volume))
                return;

            PlayAtPointWithLowPass(clip, position, volume, pitch, mixerGroup, lowPassCutoffHz);
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
            ResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition);
            PlayAtPointResolved(
                clip,
                position,
                in sourceAup,
                sourceAbsolutePosition,
                volume,
                pitch,
                mixerGroup,
                0);
        }

        private void PlayAtPointResolved(
            AudioClip clip,
            Vector3 position,
            in AbsoluteUniversePosition sourceAup,
            Vector3 sourceAbsolutePosition,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            int stationaryCacheKey)
        {
            if (clip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayAtPointNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Debug.LogWarning("[SpatialAudioManager] PlayAtPoint called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAbsolutePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            Vector3 audiblePosition = position;
            Vector3 audibleAbsolutePosition = sourceAbsolutePosition;
            AbsoluteUniversePosition audibleAup = sourceAup;
            AcousticPathResult acousticPortalResult = default;
            bool hasAcousticPortalPath = hasListener &&
                TryResolveAcousticPortalPath(
                    position,
                    listenerRuntimePosition,
                    listenerRight,
                    in sourceAup,
                    in listenerAup,
                    stationaryCacheKey,
                    out acousticPortalResult);
            if (hasAcousticPortalPath)
            {
                audibleAup = ToAbsoluteUniversePosition(in acousticPortalResult.LastPortalAup);
                audiblePosition = ToRuntimeVector3(in audibleAup);
                audibleAbsolutePosition = ToAbsoluteVector3(in audibleAup);
            }

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in audibleAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;

            // â”€â”€ ÐŸÐ¾Ð·Ð¸Ñ†Ð¸Ð¾Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ â”€â”€
            source.transform.position = audiblePosition;

            // â”€â”€ ÐÐ°ÑÑ‚Ñ€Ð¾Ð¹ÐºÐ° â”€â”€
            source.clip = clip;
            float clampedVolume = math.saturate(volume);
            if (hasAcousticPortalPath)
                clampedVolume *= acousticPortalResult.Transmission01;
            source.volume = clampedVolume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = clampedVolume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            CacheWorldSourceBusFlags(index, source.outputAudioMixerGroup);
            source.pitch = ResolveSourcePitch(index, 1f);
            _audioLodTiers[index] = lodTier;
            float now = Time.unscaledTime;
            int currentFrame = Time.frameCount;
            UpdateWorldSourceAudioLod(
                index,
                source,
                audiblePosition,
                audibleAbsolutePosition,
                in audibleAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
                listenerAbsolutePosition,
                now,
                true);
            if (hasAcousticPortalPath)
                ApplyAcousticPortalPresentation(index, source, in acousticPortalResult);
            ApplyHaasMask(index, in audibleAup, hasListener, in listenerAup, now);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);

            // â”€â”€ Ð—Ð°Ð¿ÑƒÑÐº â”€â”€
            PlayAcousticSource(source, hasAcousticPortalPath ? acousticPortalResult.DelaySeconds : 0f);
            _startTimes[index] = now;
            CacheActiveWorldRuntimePosition(index, audiblePosition, currentFrame);
            CacheActiveWorldAup(index, in audibleAup, currentFrame);
            MarkWorldSourceActive(index);
        }

        public bool QueueAudioEvent(in CoreAudioEvent audioEvent)
        {
            if (!_audioEventQueue.IsCreated ||
                _audioEventQueueCount >= MaxQueuedAudioEvents ||
                !TryResolveAudioEventClip(audioEvent.EventID, out _))
            {
                if (_audioEventQueue.IsCreated && _audioEventQueueCount >= MaxQueuedAudioEvents)
                    _audioEventQueueDroppedCount++;
                return false;
            }

            _audioEventQueue.Enqueue(audioEvent);
            _audioEventQueueCount++;
            return true;
        }

        public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            if (!_soundEmissionSignals.IsCreated ||
                _soundEmissionSignalQueueCount >= MaxQueuedSoundEmissionSignals ||
                !TryResolveAudioEventClip(signal.EventID, out _) ||
                !AcousticAup.IsFinite(in signal.SourceAup))
            {
                if (_soundEmissionSignals.IsCreated && _soundEmissionSignalQueueCount >= MaxQueuedSoundEmissionSignals)
                    _soundEmissionSignalDroppedCount++;
                return false;
            }

            _soundEmissionSignals.Enqueue(signal);
            _soundEmissionSignalQueueCount++;
            return true;
        }

        public bool QueueHullStressSignal(in HullStressSignal signal)
        {
            if (!IsInitialized ||
                !IsFinite(signal.WorldPosition) ||
                !math.isfinite(signal.Stress01) ||
                !math.isfinite(signal.PressureDelta) ||
                !math.isfinite(signal.DepthMeters) ||
                math.max(signal.Stress01, math.abs(signal.PressureDelta)) <= 0f)
            {
                return false;
            }

            ProceduralAudioEvents.RaiseHullStressSignal(in signal);
            return true;
        }

        private bool TryResolveAudioEventClip(uint eventID, out AudioClip clip)
        {
            clip = null;
            AudioClip[] table = _audioEventClipTable;
            if (eventID < FirstAudioEventId || table == null)
                return false;

            uint index = eventID - 1u;
            if (index >= (uint)table.Length)
                return false;

            clip = table[(int)index];
            return clip != null;
        }

        private void DrainAudioEventQueue()
        {
            if (!_audioEventQueue.IsCreated || _audioEventQueueCount <= 0)
                return;

            while (_audioEventQueueCount > 0 && _audioEventQueue.TryDequeue(out CoreAudioEvent audioEvent))
            {
                _audioEventQueueCount--;
                DispatchQueuedAudioEvent(in audioEvent);
            }

            if (_audioEventQueueCount < 0)
                _audioEventQueueCount = 0;
        }

        private void DrainSoundEmissionSignals()
        {
            if (!_soundEmissionSignals.IsCreated || _soundEmissionSignalQueueCount <= 0)
                return;

            while (_soundEmissionSignalQueueCount > 0 && _soundEmissionSignals.TryDequeue(out SoundEmissionSignal signal))
            {
                _soundEmissionSignalQueueCount--;
                DispatchSoundEmissionSignal(in signal);
            }

            if (_soundEmissionSignalQueueCount < 0)
                _soundEmissionSignalQueueCount = 0;
        }

        private void DispatchSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            if (!TryResolveAudioEventClip(signal.EventID, out AudioClip clip))
                return;

            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in signal.SourceAup);
            Vector3 runtimePosition = ToRuntimeVector3(in sourceAup);
            Vector3 absolutePosition = ToAbsoluteVector3(in sourceAup);
            int stationaryCacheKey = (signal.Flags & AcousticPortalFlags.StationaryEmitter) != 0
                ? signal.StationaryCacheKey
                : 0;
            PlayAtPointResolved(
                clip,
                runtimePosition,
                in sourceAup,
                absolutePosition,
                signal.Volume,
                signal.Pitch,
                ResolvedDefaultWorldMixerGroup,
                stationaryCacheKey);
        }

        private void DispatchQueuedAudioEvent(in CoreAudioEvent audioEvent)
        {
            if (!TryResolveAudioEventClip(audioEvent.EventID, out AudioClip clip))
                return;

            PlayAtPoint(
                clip,
                audioEvent.Position,
                audioEvent.Volume,
                audioEvent.Pitch,
                ResolvedDefaultWorldMixerGroup);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayAtPointLowPassNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Debug.LogWarning("[SpatialAudioManager] PlayAtPointWithLowPass called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAbsolutePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            ResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition);
            Vector3 audiblePosition = position;
            Vector3 audibleAbsolutePosition = sourceAbsolutePosition;
            AbsoluteUniversePosition audibleAup = sourceAup;
            AcousticPathResult acousticPortalResult = default;
            bool hasAcousticPortalPath = hasListener &&
                TryResolveAcousticPortalPath(
                    position,
                    listenerRuntimePosition,
                    listenerRight,
                    in sourceAup,
                    in listenerAup,
                    0,
                    out acousticPortalResult);
            if (hasAcousticPortalPath)
            {
                audibleAup = ToAbsoluteUniversePosition(in acousticPortalResult.LastPortalAup);
                audiblePosition = ToRuntimeVector3(in audibleAup);
                audibleAbsolutePosition = ToAbsoluteVector3(in audibleAup);
            }

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in audibleAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            source.enabled = true;
            source.transform.position = audiblePosition;
            source.clip = clip;
            float clampedVolume = math.saturate(volume);
            if (hasAcousticPortalPath)
                clampedVolume *= acousticPortalResult.Transmission01;
            source.volume = clampedVolume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = clampedVolume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            CacheWorldSourceBusFlags(index, source.outputAudioMixerGroup);
            source.pitch = ResolveSourcePitch(index, 1f);
            _audioLodTiers[index] = lodTier;
            float now = Time.unscaledTime;
            int currentFrame = Time.frameCount;
            UpdateWorldSourceAudioLod(
                index,
                source,
                audiblePosition,
                audibleAbsolutePosition,
                in audibleAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
                listenerAbsolutePosition,
                now,
                true);
            float cutoff = math.clamp(
                lowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (hasAcousticPortalPath)
                cutoff = math.min(cutoff, acousticPortalResult.LowPassCutoffHz);
            if (_sourceCinematicMuffleLowPassCutoffs != null && index >= 0 && index < _sourceCinematicMuffleLowPassCutoffs.Length)
                cutoff = math.min(cutoff, _sourceCinematicMuffleLowPassCutoffs[index]);
            if (cutoff < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f)
                ApplyLowPassFilter(index, true, cutoff);

            if (hasAcousticPortalPath)
                ApplyAcousticPortalPresentation(index, source, in acousticPortalResult);
            ApplyHaasMask(index, in audibleAup, hasListener, in listenerAup, now);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);
            PlayAcousticSource(source, hasAcousticPortalPath ? acousticPortalResult.DelaySeconds : 0f);
            _startTimes[index] = now;
            CacheActiveWorldRuntimePosition(index, audiblePosition, currentFrame);
            CacheActiveWorldAup(index, in audibleAup, currentFrame);
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayStatic2DNullClipLogTime, NullClipEditorLogIntervalSeconds))
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
            source.volume = math.saturate(volume);
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = ResolveUiMixerGroup(clip, mixerGroup);

            source.Play();
            _startTimes2D[index] = Time.unscaledTime;
        }

        public void PlayStatic2DBitCrushed(AudioClip clip, float volume)
        {
            TryPlayStatic2DBitCrushed(clip, volume);
        }

        public bool TryPlayStatic2DBitCrushed(AudioClip clip, float volume)
        {
            bool hasEncryptedVoiceRoute = _encryptedVoiceGroup != null;
            PlayStatic2D(clip, volume, hasEncryptedVoiceRoute ? _encryptedVoiceGroup : _interfaceGroup);
            return clip != null && hasEncryptedVoiceRoute;
        }

        public void SetNarrativeRadioInterference(float interference01)
        {
            if (!_hasNarrativeRadioLowPassCutoffParameter)
                return;

            AudioMixer mixer = ResolveNarrativeRadioMixer();
            if (mixer == null)
                return;

            float cutoffHz = math.lerp(
                math.max(20f, _narrativeRadioOpenCutoffHz),
                math.max(20f, _narrativeRadioMuffledCutoffHz),
                math.saturate(interference01));
            if (math.abs(cutoffHz - _lastNarrativeRadioLowPassCutoffHz) <= 1f)
                return;

            mixer.SetFloat(_narrativeRadioLowPassCutoffParameter, cutoffHz);
            _lastNarrativeRadioLowPassCutoffHz = cutoffHz;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  PUBLIC API â€” MIXER GROUP ACCESSORS
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

        /// <summary>Mixer group Ð´Ð»Ñ SFX (ÑÑƒÑ‰ÐµÑÑ‚Ð²Ð°, Ð¼ÐµÑ…Ð°Ð½Ð¸Ð·Ð¼Ñ‹, Ð¾ÐºÑ€ÑƒÐ¶ÐµÐ½Ð¸Ðµ).</summary>
        public AudioMixerGroup SfxGroup => _sfxGroup;

        /// <summary>Mixer group Ð´Ð»Ñ Ð¸Ð½Ñ‚ÐµÑ€Ñ„ÐµÐ¹ÑÐ° Ð¸ Ð·Ð²ÑƒÐºÐ¾Ð² ÑˆÐ»ÐµÐ¼Ð°.</summary>
        public AudioMixerGroup InterfaceGroup => _interfaceGroup;

        public bool HasEncryptedVoiceBitCrushRoute => _encryptedVoiceGroup != null;

        public AudioMixerGroup EncryptedVoiceGroup => _encryptedVoiceGroup != null ? _encryptedVoiceGroup : _interfaceGroup;

        /// <summary>Mixer group for resolved ambient-bed playback.</summary>
        public AudioMixerGroup AmbientGroup => ResolvedBedBusGroup;

        /// <summary>Mixer group for dominant hostile cues.</summary>
        public AudioMixerGroup ThreatGroup => ResolvedThreatBusGroup;

        /// <summary>Mixer group for ambient bed layers.</summary>
        public AudioMixerGroup BedGroup => ResolvedBedBusGroup;

        /// <summary>Current 360-bin acoustic radar intensity ring for HUD consumers. Treat as read-only and reacquire each tick.</summary>
        public NativeArray<float>.ReadOnly AcousticRadarIntensityBins =>
            _acousticRadarIntensityBins.IsCreated ? _acousticRadarIntensityBins.AsReadOnly() : default;

        /// <summary>Current acoustic radar angular resolution in bins.</summary>
        public int AcousticRadarResolution => AcousticRadarBinCount;

        /// <summary>Persistent 8x4 acoustic radar energy grid for HUD sonar distortion overlays.</summary>
        public NativeArray<float>.ReadOnly AcousticRadarEnergyGrid =>
            _acousticRadarGrid.IsCreated ? _acousticRadarGrid.AsReadOnly() : default;

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
            int currentFrame = Time.frameCount;
            for (int activeSlot = 0; activeSlot < _activeWorldCount && count < limit; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isPlaying || source.clip == null)
                    continue;

                if (!TryGetCachedActiveWorldRuntimePosition(sourceIndex, out Vector3 sourcePosition))
                    continue;

                destination[count] = new ActiveEmitterSample
                {
                    PositionAup = ResolveActiveWorldAup(sourceIndex, sourcePosition, currentFrame),
                    Position = sourcePosition,
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
                    PositionAup = emitter.PositionAup,
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        internal int CopyActiveImpactEmitterSamples(ActiveImpactEmitterSample[] destination)
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

                destination[count] = new ActiveImpactEmitterSample
                {
                    PositionAup = emitter.PositionAup,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        private void UpdateDominantBinauralEmitterTelemetry(
            float now,
            Transform listener,
            in AbsoluteUniversePosition listenerAup,
            int currentFrame)
        {
            _dominantBinauralEmitter = default;
            if (listener == null)
                return;

            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForwardBasis);
            float3 listenerForward = listenerForwardBasis;
            float bestScore = 0f;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isActiveAndEnabled || !source.isPlaying || source.clip == null)
                    continue;

                if (!TryGetCachedActiveWorldRuntimePosition(sourceIndex, out Vector3 sourcePosition))
                    continue;

                AbsoluteUniversePosition sourceAup = ResolveActiveWorldAup(sourceIndex, sourcePosition, currentFrame);
                TryPromoteBinauralEmitter(
                    in listenerAup,
                    listenerRight,
                    listenerForward,
                    sourcePosition,
                    in sourceAup,
                    math.max(0f, source.volume),
                    ref bestScore);
            }

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                TryPromoteBinauralEmitter(in listenerAup, listenerRight, listenerForward, emitter.Position, in emitter.PositionAup, amplitude, ref bestScore);
            }
        }

        private void TryPromoteBinauralEmitter(
            in AbsoluteUniversePosition listenerAup,
            float3 listenerRight,
            float3 listenerForward,
            Vector3 sourcePosition,
            in AbsoluteUniversePosition sourceAup,
            float amplitude,
            ref float bestScore)
        {
            if (!(amplitude > 0f))
                return;

            float3 runtimeDelta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup);
            float runtimeDistanceSq = math.lengthsq(runtimeDelta);
            if (runtimeDistanceSq <= 0.0001f)
                return;

            float distanceSqr = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            if (distanceSqr <= 0.0001f)
                return;

            float distanceSquaredGain = math.rcp(1f + distanceSqr);
            float energy = amplitude * distanceSquaredGain;
            if (!(energy > bestScore))
                return;

            float3 sourceDirection = ResolveDominantAxisDirection(runtimeDelta);
            float earAxisDot = math.clamp(math.dot(listenerRight, sourceDirection), -1f, 1f);
            float absSin = math.abs(earAxisDot);
            float itdSeconds = absSin > 0.001f
                ? math.lerp(BinauralMinimumItdSeconds, BinauralMaximumItdSeconds, absSin)
                : 0f;
            float waterDensityMul = math.saturate(_listenerWaterDensityMul);
            float airShadowCutoff = math.lerp(8000f, 1200f, absSin);
            float waterShadowCutoff = math.lerp(8000f, 3000f, absSin);
            float shadowCutoff = math.lerp(airShadowCutoff, waterShadowCutoff, waterDensityMul);
            float shadowAmount = math.lerp(absSin, absSin * 0.5f, waterDensityMul);
            if (TryResolveRearHemisphereLowPassCutoff(in sourceAup, listenerForward, in listenerAup, out float rearHemisphereCutoff))
            {
                shadowCutoff = math.min(shadowCutoff, rearHemisphereCutoff);
                float rearShadowAmount = math.saturate(
                    (RearHemisphereLowPassMaximumCutoffHertz - rearHemisphereCutoff) *
                    RearHemisphereLowPassCutoffRangeInv);
                shadowAmount = math.saturate(math.max(shadowAmount, rearShadowAmount));
            }

            _dominantBinauralEmitter = new BinauralEmitterTelemetry
            {
                Position = sourcePosition,
                DistanceMeters = runtimeDistanceSq * math.rsqrt(math.max(runtimeDistanceSq, 0.0001f)),
                AzimuthRadians = earAxisDot,
                RightDot = earAxisDot,
                ItdSeconds = itdSeconds,
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

            ClearAudioEventQueue();
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
        ///   2. Track quietest active source; startTime is only a tie-breaker.
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

            int quietestIndex = 0;
            float quietestVolume = float.MaxValue;
            float oldestTime = float.MaxValue;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                float candidateVolume = source != null ? math.max(0f, source.volume) : 0f;
                float candidateStartTime = _startTimes[sourceIndex];
                if (candidateVolume < quietestVolume ||
                    (candidateVolume <= quietestVolume && candidateStartTime < oldestTime))
                {
                    quietestVolume = candidateVolume;
                    oldestTime = candidateStartTime;
                    quietestIndex = sourceIndex;
                }
            }

            _pool[quietestIndex].Stop();
            ResetWorldSourceState(quietestIndex, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShouldEmitEditorThrottledLog(ref _nextWorldPoolFullEditorLogTime, PoolFullEditorLogIntervalSeconds))
            {
                Debug.Log("[SpatialAudioManager] World pool full. Evicting quietest source.", this);
            }
#endif

            return quietestIndex;
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

        private bool TryReserveWeatherAudioEvent(float volume01)
        {
            int frame = Time.frameCount;
            if (_weatherAudioFrame != frame)
            {
                _weatherAudioFrame = frame;
                _weatherAudioEventsThisFrame = 0;
            }

            if (_weatherAudioEventsThisFrame >= MaxWeatherAudioEventsPerFrame)
            {
                _stormRoarShedTarget01 = math.saturate(
                    _stormRoarShedTarget01 +
                    math.max(StormRoarShedGainPerEvent, math.saturate(volume01) * StormRoarShedGainPerEvent));
                return false;
            }

            _weatherAudioEventsThisFrame++;
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

            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAbsolutePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            ResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition);
            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in sourceAup, in listenerAup)
                : AudioLodTier.Tier0Full;
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
            float clampedVolume = math.saturate(volume);
            source.volume = clampedVolume;
            float clampedPitch = math.clamp(pitch, 0.1f, 3f);
            _baseVolumes[index] = clampedVolume;
            _basePitches[index] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, mixerGroup);
            CacheWorldSourceBusFlags(index, source.outputAudioMixerGroup);
            source.pitch = ResolveSourcePitch(index, 1f);
            _audioLodTiers[index] = lodTier;
            float now = Time.unscaledTime;
            int currentFrame = Time.frameCount;
            UpdateWorldSourceAudioLod(
                index,
                source,
                position,
                sourceAbsolutePosition,
                in sourceAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
                listenerAbsolutePosition,
                now,
                true);
            ApplyHaasMask(index, in sourceAup, hasListener, in listenerAup, now);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);
            source.Play();
            _startTimes[index] = now;
            CacheActiveWorldRuntimePosition(index, position, currentFrame);
            CacheActiveWorldAup(index, in sourceAup, currentFrame);
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

        private void TryRegisterSlowTickable()
        {
            if (_registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registeredSlowTickable = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_registeredLateFrameTickable || !Application.isPlaying)
                return;

            _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        void IPhysicsImpactEventListener.OnPhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            HandlePhysicsImpact(in impactSignal);
        }

        void IPhysicsAcousticImpulseEventListener.OnAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            HandleAcousticImpulse(in impulseEvent);
        }

        private void HandlePhysicsImpact(in PhysicsImpactSignal impactSignal)
        {
            // Mirrors impact positions for passive radar/UI consumers only.
            // Audible impact energy is synthesized through PlayerCriticalProceduralAudioRenderer.
            float amplitude = math.saturate(impactSignal.Intensity * ImpactEmitterAmplitudeScale);
            if (impactSignal.IsHeavy)
                amplitude = math.max(amplitude, 0.45f);

            AbsoluteUniversePosition impactAup = impactSignal.ResolvePointAup();
            TryQueueImpactRadarEmitter(
                impactSignal.Point,
                in impactAup,
                amplitude,
                math.saturate(impactSignal.Intensity));
        }

        private void HandleAcousticImpulse(in AcousticImpulseEvent impulseEvent)
        {
            float amplitude = math.saturate(impulseEvent.Volume01 * ImpactEmitterAmplitudeScale);
            if ((impulseEvent.Flags & AcousticImpulseFlags.Leviathan) != 0)
                amplitude = math.max(amplitude, 0.5f);

            TryQueueImpactRadarEmitter(impulseEvent.RuntimePosition, amplitude, math.saturate(impulseEvent.Volume01));
        }

        private bool TryQueueImpactRadarEmitter(Vector3 position, float amplitude, float lifetime01)
        {
            AbsoluteUniversePosition positionAup = AbsoluteUniversePosition.FromRuntimePosition(position);
            return TryQueueImpactRadarEmitter(position, in positionAup, amplitude, lifetime01);
        }

        private bool TryQueueImpactRadarEmitter(
            Vector3 position,
            in AbsoluteUniversePosition positionAup,
            float amplitude,
            float lifetime01)
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
                PositionAup = positionAup,
                SpawnAt = now,
                ExpireAt = now + lifetime
            };
            return true;
        }

        private void HandleFatalPressureImplosion(in FatalPressureImplosionEvent implosionEvent)
        {
            Vector3 implosionRuntimePosition = implosionEvent.RuntimePosition;
            AbsoluteUniversePosition implosionAup = AbsoluteUniversePosition.FromRuntimePosition(implosionRuntimePosition);
            bool hasListener = TryResolveListenerFrame(
                out _,
                out _,
                out _,
                out AbsoluteUniversePosition resolvedListenerAup);
            AbsoluteUniversePosition listenerAup = hasListener ? resolvedListenerAup : implosionAup;
            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in implosionAup));
            ResolveDelayedAcousticPath(
                implosionRuntimePosition,
                in implosionAup,
                in listenerAup,
                out float acousticTransmission01,
                out float lowPassCutoffHz);
            DelayedAudioEvent delayedEvent = new DelayedAudioEvent
            {
                Kind = DelayedAudioEventKind.FatalPressureImplosion,
                Aup = implosionAup,
                EventTimeSeconds = Time.unscaledTime,
                DelaySeconds = ResolveFixedUnderwaterArrivalDelaySecondsFromSq(distanceSq),
                Volume = FatalPressureImplosionEventVolume,
                Pitch = FatalPressureImplosionEventPitch,
                AcousticTransmission01 = acousticTransmission01,
                LowPassCutoffHz = lowPassCutoffHz,
                ThermalShimmer01 = 0f,
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
            AbsoluteUniversePosition eventAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            bool hasListener = TryResolveListenerFrame(
                out _,
                out _,
                out _,
                out AbsoluteUniversePosition resolvedListenerAup);
            AbsoluteUniversePosition listenerAup = hasListener ? resolvedListenerAup : eventAup;
            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in eventAup));
            ResolveDelayedAcousticPath(
                runtimePosition,
                in eventAup,
                in listenerAup,
                out float acousticTransmission01,
                out float lowPassCutoffHz);
            DelayedAudioEvent delayedEvent = new DelayedAudioEvent
            {
                Kind = DelayedAudioEventKind.InventoryRunawayExplosion,
                Aup = eventAup,
                EventTimeSeconds = Time.unscaledTime,
                DelaySeconds = ResolveFixedUnderwaterArrivalDelaySecondsFromSq(distanceSq),
                Volume = math.saturate(volume01),
                Pitch = 0.72f,
                AcousticTransmission01 = acousticTransmission01,
                LowPassCutoffHz = lowPassCutoffHz,
                ThermalShimmer01 = 0f,
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
                _pendingDelayedAudioEvents.AddNoResize(delayedEvent);
                _delayedAudioIngressCount--;
            }
        }

        private void ProcessDelayedAudioEvents(bool hasListener, in AbsoluteUniversePosition listenerAup)
        {
            if (!_pendingDelayedAudioEvents.IsCreated || _pendingDelayedAudioEvents.Length == 0)
                return;

            float now = Time.unscaledTime;
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

                DispatchDelayedAudioEvent(in delayedEvent, hasListener, in listenerAup);
            }

            if (writeIndex != _pendingDelayedAudioEvents.Length)
                _pendingDelayedAudioEvents.ResizeUninitialized(writeIndex);
        }

        private void DispatchDelayedAudioEvent(
            in DelayedAudioEvent delayedEvent,
            bool hasListener,
            in AbsoluteUniversePosition listenerAup)
        {
            switch (delayedEvent.Kind)
            {
                case DelayedAudioEventKind.FatalPressureImplosion:
                    if (_fatalPressureImplosionClip != null)
                    {
                        PlayAtPointWithLowPass(
                            _fatalPressureImplosionClip,
                            ToRuntimeVector3(in delayedEvent.Aup),
                            ResolveDelayedEventVolume(in delayedEvent),
                            ResolveDelayedEventPitch(in delayedEvent),
                            ResolvedThreatBusGroup,
                            ResolveDelayedEventLowPass(in delayedEvent));
                    }

                    if (hasListener)
                        ApplyDelayedTrauma(in delayedEvent, in listenerAup);
                    break;

                case DelayedAudioEventKind.InventoryRunawayExplosion:
                    if (_inventoryRunawayExplosionClip != null)
                    {
                        PlayAtPointWithLowPass(
                            _inventoryRunawayExplosionClip,
                            ToRuntimeVector3(in delayedEvent.Aup),
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

            double3 absolutePosition = delayedEvent.Aup.ToAbsoluteDouble3();
            float phase = (Time.unscaledTime * 47.3f) +
                          ((float)absolutePosition.x * 0.013f) +
                          ((float)absolutePosition.z * 0.017f);
            float shimmer = FastSineRadians(phase) * ThermalShimmerMaximumPitchRatio * shimmer01;
            return math.clamp(delayedEvent.Pitch * (1f + shimmer), 0.1f, 3f);
        }

        private static float FastSineRadians(float radians)
        {
            float phase = radians * InverseTwoPi;
            int whole = (int)phase;
            phase -= whole;
            if (phase < 0f)
                phase += 1f;
            else if (phase >= 1f)
                phase -= 1f;

            float centered = phase > 0.5f ? phase - 1f : phase;
            float wave = (4f * centered) - (8f * centered * math.abs(centered));
            return wave + 0.225f * ((wave * math.abs(wave)) - wave);
        }

        private static float ResolveFixedUnderwaterArrivalDelaySecondsFromSq(float distanceSq)
        {
            return distanceSq >= MassiveDistanceFixedAudioDelayMetersSq
                ? MassiveDistanceFixedAudioDelaySeconds
                : 0f;
        }

        private void ResolveDelayedAcousticPath(
            Vector3 sourceRuntimePosition,
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            out float acousticTransmission01,
            out float lowPassCutoffHz)
        {
            acousticTransmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            float sourceListenerDistanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            if (sourceListenerDistanceSq > CinematicSourceMuffleReferenceDistanceSq)
            {
                ResolveCinematicFarDistanceMuffle(sourceListenerDistanceSq, out acousticTransmission01, out lowPassCutoffHz);
            }

            if (TryResolveCinematicZoneMismatch(
                    sourceRuntimePosition,
                    in sourceAup,
                    in listenerAup))
            {
                acousticTransmission01 = math.min(acousticTransmission01, CinematicZoneMuffleTransmission);
                lowPassCutoffHz = math.min(lowPassCutoffHz, CinematicZoneMuffleCutoffHertz);
            }
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

        private void ApplyDelayedTrauma(in DelayedAudioEvent delayedEvent, in AbsoluteUniversePosition listenerAup)
        {
            if (_listenerPlayerMovement == null)
                return;

            float3 listenerOffsetAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in listenerAup, in delayedEvent.Aup);
            Vector3 listenerOffset = new Vector3(listenerOffsetAup.x, listenerOffsetAup.y, listenerOffsetAup.z);
            float distanceSq = math.lengthsq(listenerOffsetAup);
            float traumaRange = math.max(delayedEvent.TraumaRangeMeters, 0.0001f);
            float traumaRangeSq = traumaRange * traumaRange;
            if (distanceSq > traumaRangeSq)
                return;

            float invDistance = math.rcp(math.max(
                ApproximateMagnitude3D(listenerOffsetAup),
                0.000001f));
            Vector3 traumaDirection = distanceSq > 0.000001f
                ? listenerOffset * invDistance
                : Vector3.up;
            float distance01 = math.saturate(distanceSq * math.rcp(traumaRangeSq));
            float trauma01 = 1f - distance01 * distance01;
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

        private void ClearAudioEventQueue()
        {
            if (_audioEventQueue.IsCreated)
            {
                while (_audioEventQueue.TryDequeue(out _))
                {
                }
            }

            _audioEventQueueCount = 0;
            if (_soundEmissionSignals.IsCreated)
            {
                while (_soundEmissionSignals.TryDequeue(out _))
                {
                }
            }

            _soundEmissionSignalQueueCount = 0;
            _soundEmissionSignalDroppedCount = 0;
        }

        private void ApplyHaasMask(
            int sourceIndex,
            in AbsoluteUniversePosition sourceAup,
            bool hasListener,
            in AbsoluteUniversePosition listenerAup,
            float now)
        {
            float predictedArrivalTime = ResolvePredictedArrivalTime(in sourceAup, hasListener, in listenerAup, now);
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
                float releaseTime = now + HaasReleaseThresholdSeconds;
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

        private static float ResolvePredictedArrivalTime(
            in AbsoluteUniversePosition sourceAup,
            bool hasListener,
            in AbsoluteUniversePosition listenerAup,
            float now)
        {
            if (!hasListener)
                return now;

            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            return now + ResolveFixedUnderwaterArrivalDelaySecondsFromSq(distanceSq);
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

        private bool TryResolveListenerFrame(
            out Transform listener,
            out Vector3 listenerRuntimePosition,
            out Vector3 listenerAbsolutePosition,
            out AbsoluteUniversePosition listenerAup)
        {
            listener = ResolveListenerTransform();
            if (listener == null)
            {
                listenerRuntimePosition = default;
                listenerAbsolutePosition = default;
                listenerAup = default;
                return false;
            }

            listenerRuntimePosition = listener.position;
            if (!IsFinite(listenerRuntimePosition))
            {
                listenerAbsolutePosition = default;
                listenerAup = default;
                return false;
            }

            if (TryResolvePlayerListenerAup(listener, listenerRuntimePosition, out listenerAup))
            {
                double3 absolute = listenerAup.ToAbsoluteDouble3();
                listenerAbsolutePosition = new Vector3((float)absolute.x, (float)absolute.y, (float)absolute.z);
                return true;
            }

            listenerAbsolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(listenerRuntimePosition);
            listenerAup = AbsoluteUniversePosition.FromRuntimePosition(listenerRuntimePosition);
            return true;
        }

        private static void ResolveSourceAupFrame(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition sourceAup,
            out Vector3 absolutePosition)
        {
            sourceAup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
            absolutePosition = ToAbsoluteVector3(in sourceAup);
        }

        private static Vector3 ToAbsoluteVector3(in AbsoluteUniversePosition aup)
        {
            double3 absolute = aup.ToAbsoluteDouble3();
            return new Vector3((float)absolute.x, (float)absolute.y, (float)absolute.z);
        }

        private static Vector3 ToRuntimeVector3(in AbsoluteUniversePosition aup)
        {
            float3 runtime = aup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private static AcousticAup ToAcousticAup(in AbsoluteUniversePosition aup)
        {
            return new AcousticAup(
                aup.GridX,
                aup.GridY,
                aup.GridZ,
                new float3(aup.LocalX, aup.LocalY, aup.LocalZ));
        }

        private static AbsoluteUniversePosition ToAbsoluteUniversePosition(in AcousticAup aup)
        {
            return new AbsoluteUniversePosition
            {
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.Local.x,
                LocalY = aup.Local.y,
                LocalZ = aup.Local.z
            };
        }

        private static bool TryResolvePlayerListenerAup(
            Transform listener,
            Vector3 listenerRuntimePosition,
            out AbsoluteUniversePosition listenerAup)
        {
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            if (playerContext != null &&
                IsPlayerOwnedListener(listener, playerContext.PlayerTransform, playerContext.PlayerObject, playerContext.PlayerCamera))
            {
                if (HectonXRRuntimeState.TryResolveCachedHeadAup(listenerRuntimePosition, out listenerAup))
                    return true;

                HectonPlayerMovement movement = playerContext.PlayerMovement;
                if (movement != null)
                {
                    AbsoluteUniversePosition currentAup = movement.CurrentAup;
                    Vector3 rootRuntimePosition = currentAup.ToRuntimeFloat3();
                    if (IsFinite(rootRuntimePosition))
                    {
                        listenerAup = OffsetAupLocal(in currentAup, listenerRuntimePosition - rootRuntimePosition);
                        return true;
                    }
                }
            }

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                IsPlayerOwnedListener(listener, runtimeContext.PlayerTransform, runtimeContext.PlayerObject, runtimeContext.PlayerCamera) &&
                (runtimeContext.MovementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
            {
                if (HectonXRRuntimeState.TryResolveCachedHeadAup(listenerRuntimePosition, out listenerAup))
                return true;

                AbsoluteUniversePosition predictedAup = runtimeContext.MovementState.PredictedAup;
                Vector3 rootRuntimePosition = predictedAup.ToRuntimeFloat3();
                if (IsFinite(rootRuntimePosition))
                {
                    listenerAup = OffsetAupLocal(in predictedAup, listenerRuntimePosition - rootRuntimePosition);
                    return true;
                }
            }

            listenerAup = default;
            return false;
        }

        private static AbsoluteUniversePosition OffsetAupLocal(in AbsoluteUniversePosition anchorAup, Vector3 runtimeOffset)
        {
            AbsoluteUniversePosition result = anchorAup;
            result.LocalX += runtimeOffset.x;
            result.LocalY += runtimeOffset.y;
            result.LocalZ += runtimeOffset.z;
            NormalizeAupLocalAxis(ref result.GridX, ref result.LocalX);
            NormalizeAupLocalAxis(ref result.GridY, ref result.LocalY);
            NormalizeAupLocalAxis(ref result.GridZ, ref result.LocalZ);
            return result;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static void NormalizeAupLocalAxis(ref long grid, ref float local)
        {
            const float cellSize = AbsoluteUniversePosition.CellSizeMeters;
            if (local >= 0f && local < cellSize)
                return;

            long gridDelta = FastFloorToLong(local * math.rcp(cellSize));
            grid += gridDelta;
            local -= gridDelta * cellSize;
            if (local < 0f)
            {
                local += cellSize;
                grid--;
                return;
            }

            if (local >= cellSize)
            {
                local -= cellSize;
                grid++;
            }
        }

        private static bool IsPlayerOwnedListener(
            Transform listener,
            Transform playerTransform,
            GameObject playerObject,
            Camera playerCamera)
        {
            if (listener == null)
                return false;

            if (playerCamera != null && ReferenceEquals(listener, playerCamera.transform))
                return true;

            Transform listenerRoot = listener.root;
            Transform playerObjectTransform = playerObject != null ? playerObject.transform : null;
            return IsSameTransformOwner(listener, listenerRoot, playerTransform) ||
                   IsSameTransformOwner(listener, listenerRoot, playerObjectTransform);
        }

        private static bool IsSameTransformOwner(Transform listener, Transform listenerRoot, Transform owner)
        {
            if (owner == null)
                return false;

            return ReferenceEquals(listener, owner) ||
                   ReferenceEquals(listenerRoot, owner) ||
                   ReferenceEquals(listenerRoot, owner.root);
        }

        private static void ResolveListenerBasis(
            Transform listener,
            out float3 listenerRight,
            out float3 listenerUp,
            out float3 listenerForward)
        {
            listenerRight = new float3(1f, 0f, 0f);
            listenerUp = new float3(0f, 1f, 0f);
            listenerForward = new float3(0f, 0f, 1f);
            if (listener == null)
                return;

            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) &&
                runtimeContext != null &&
                IsPlayerOwnedListener(listener, runtimeContext.PlayerTransform, runtimeContext.PlayerObject, runtimeContext.PlayerCamera) &&
                TryResolveRuntimeContextForward(runtimeContext, out listenerForward))
            {
                ResolveDominantBasis(listenerForward, out listenerRight, out listenerUp);
                return;
            }

            listenerRight = (float3)listener.right;
            listenerUp = (float3)listener.up;
            listenerForward = (float3)listener.forward;
        }

        private static bool TryResolveRuntimeContextForward(PlayerRuntimeContext runtimeContext, out float3 listenerForward)
        {
            listenerForward = default;
            float3 lookForward = runtimeContext.LookState.AimForward;
            if (math.lengthsq(lookForward) > 0.0001f)
            {
                listenerForward = ResolveDominantAxisDirection(lookForward);
                return true;
            }

            float3 cameraForward = runtimeContext.MovementState.CameraForward;
            if (math.lengthsq(cameraForward) > 0.0001f)
            {
                listenerForward = ResolveDominantAxisDirection(cameraForward);
                return true;
            }

            float3 movementForward = runtimeContext.MovementState.Forward;
            if (math.lengthsq(movementForward) > 0.0001f)
            {
                listenerForward = ResolveDominantAxisDirection(movementForward);
                return true;
            }

            return false;
        }

        private static float3 ResolveDominantAxisDirection(float3 direction)
        {
            float3 absDirection = math.abs(direction);
            float maxAxis = math.max(absDirection.x, math.max(absDirection.y, absDirection.z));
            if (!(maxAxis > 0.0001f))
                return new float3(0f, 0f, 0f);

            if (absDirection.x >= absDirection.y && absDirection.x >= absDirection.z)
                return direction.x < 0f ? new float3(-1f, 0f, 0f) : new float3(1f, 0f, 0f);

            if (absDirection.y >= absDirection.z)
                return direction.y < 0f ? new float3(0f, -1f, 0f) : new float3(0f, 1f, 0f);

            return direction.z < 0f ? new float3(0f, 0f, -1f) : new float3(0f, 0f, 1f);
        }

        private static void ResolveDominantBasis(float3 listenerForward, out float3 listenerRight, out float3 listenerUp)
        {
            listenerForward = ResolveDominantAxisDirection(listenerForward);
            if (math.abs(listenerForward.y) > 0.5f)
            {
                listenerRight = new float3(1f, 0f, 0f);
                listenerUp = listenerForward.y > 0f ? new float3(0f, 0f, -1f) : new float3(0f, 0f, 1f);
                return;
            }

            listenerUp = new float3(0f, 1f, 0f);
            if (math.abs(listenerForward.x) > 0.5f)
            {
                listenerRight = listenerForward.x > 0f ? new float3(0f, 0f, -1f) : new float3(0f, 0f, 1f);
                return;
            }

            listenerRight = listenerForward.z > 0f ? new float3(1f, 0f, 0f) : new float3(-1f, 0f, 0f);
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

            if (_sourceCinematicMuffleLowPassCutoffs != null && sourceIndex < _sourceCinematicMuffleLowPassCutoffs.Length)
                _sourceCinematicMuffleLowPassCutoffs[sourceIndex] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            if (_sourceCinematicMuffleTransmissions != null && sourceIndex < _sourceCinematicMuffleTransmissions.Length)
                _sourceCinematicMuffleTransmissions[sourceIndex] = 1f;

            if (_sourceCinematicMuffleNextUpdateTimes != null && sourceIndex < _sourceCinematicMuffleNextUpdateTimes.Length)
                _sourceCinematicMuffleNextUpdateTimes[sourceIndex] = 0f;

            if (_smoothedDopplerRatios != null && sourceIndex < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[sourceIndex] = 1f;

            if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                _previousRelativeVelocities[sourceIndex] = 0f;

            if (_previousAbsolutePositions != null && sourceIndex < _previousAbsolutePositions.Length)
                _previousAbsolutePositions[sourceIndex] = default;

            if (_currentAbsoluteVelocities != null && sourceIndex < _currentAbsoluteVelocities.Length)
                _currentAbsoluteVelocities[sourceIndex] = default;

            if (_worldSourceBusFlags != null && sourceIndex < _worldSourceBusFlags.Length)
                _worldSourceBusFlags[sourceIndex] = 0;

            if (_activeWorldRuntimePositions != null && sourceIndex < _activeWorldRuntimePositions.Length)
                _activeWorldRuntimePositions[sourceIndex] = default;

            if (_activeWorldRuntimePositionFrames != null && sourceIndex < _activeWorldRuntimePositionFrames.Length)
                _activeWorldRuntimePositionFrames[sourceIndex] = -1;

            if (_activeWorldAups != null && sourceIndex < _activeWorldAups.Length)
                _activeWorldAups[sourceIndex] = default;

            if (_activeWorldAupFrames != null && sourceIndex < _activeWorldAupFrames.Length)
                _activeWorldAupFrames[sourceIndex] = -1;

            if (_nextTierUpdateTimes != null && sourceIndex < _nextTierUpdateTimes.Length)
                _nextTierUpdateTimes[sourceIndex] = 0f;

            if (_audioLodTiers != null && sourceIndex < _audioLodTiers.Length)
                _audioLodTiers[sourceIndex] = AudioLodTier.Tier0Full;

            if (_startTimes != null && sourceIndex < _startTimes.Length)
                _startTimes[sourceIndex] = -1f;

            ResetHaasState(sourceIndex);
        }

        private void CacheWorldSourceBusFlags(int sourceIndex, AudioMixerGroup mixerGroup)
        {
            if (_worldSourceBusFlags == null || sourceIndex < 0 || sourceIndex >= _worldSourceBusFlags.Length)
                return;

            byte flags = 0;
            if (mixerGroup != null)
            {
                if (mixerGroup == ResolvedThreatBusGroup)
                    flags |= WorldSourceBusFlagThreat;
                if (mixerGroup == ResolvedBedBusGroup)
                    flags |= WorldSourceBusFlagBed;
            }

            _worldSourceBusFlags[sourceIndex] = flags;
        }

        private bool TryRefreshSourceCinematicMuffle(
            int sourceIndex,
            Vector3 sourceRuntimePosition,
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            float now,
            bool forceImmediate,
            out float transmission01,
            out float lowPassCutoffHz)
        {
            transmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (_sourceCinematicMuffleLowPassCutoffs == null ||
                _sourceCinematicMuffleTransmissions == null ||
                _sourceCinematicMuffleNextUpdateTimes == null ||
                sourceIndex < 0 ||
                sourceIndex >= _sourceCinematicMuffleLowPassCutoffs.Length ||
                sourceIndex >= _sourceCinematicMuffleTransmissions.Length ||
                sourceIndex >= _sourceCinematicMuffleNextUpdateTimes.Length)
            {
                return false;
            }

            transmission01 = math.saturate(_sourceCinematicMuffleTransmissions[sourceIndex]);
            lowPassCutoffHz = math.clamp(
                _sourceCinematicMuffleLowPassCutoffs[sourceIndex],
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (!forceImmediate && now < _sourceCinematicMuffleNextUpdateTimes[sourceIndex])
                return lowPassCutoffHz < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f || transmission01 < 0.999f;

            _sourceCinematicMuffleNextUpdateTimes[sourceIndex] = now + CinematicSourceMuffleUpdateIntervalSeconds;
            _sourceCinematicMuffleTransmissions[sourceIndex] = 1f;
            _sourceCinematicMuffleLowPassCutoffs[sourceIndex] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            transmission01 = 1f;
            lowPassCutoffHz = AcousticOcclusionUtility.OpenLowPassCutoffHertz;

            float sourceListenerDistanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            if (sourceListenerDistanceSq > CinematicSourceMuffleReferenceDistanceSq)
            {
                ResolveCinematicFarDistanceMuffle(sourceListenerDistanceSq, out transmission01, out lowPassCutoffHz);
            }

            if (TryResolveCinematicZoneMismatch(
                    sourceRuntimePosition,
                    in sourceAup,
                    in listenerAup))
            {
                transmission01 = math.min(transmission01, CinematicZoneMuffleTransmission);
                lowPassCutoffHz = math.min(lowPassCutoffHz, CinematicZoneMuffleCutoffHertz);
            }

            lowPassCutoffHz = math.clamp(
                lowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            transmission01 = math.saturate(transmission01);
            _sourceCinematicMuffleTransmissions[sourceIndex] = transmission01;
            _sourceCinematicMuffleLowPassCutoffs[sourceIndex] = lowPassCutoffHz;
            return lowPassCutoffHz < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f || transmission01 < 0.999f;
        }

        private static void ResolveCinematicFarDistanceMuffle(
            float sourceListenerDistanceSq,
            out float transmission01,
            out float lowPassCutoffHz)
        {
            float farDistanceSq = Tier1ReducedDspDistanceMeters * Tier1ReducedDspDistanceMeters;
            float rangeSq = math.max(1f, farDistanceSq - CinematicSourceMuffleReferenceDistanceSq);
            float far01 = math.saturate(
                (math.max(0f, sourceListenerDistanceSq) - CinematicSourceMuffleReferenceDistanceSq) *
                math.rcp(rangeSq));
            transmission01 = math.lerp(
                CinematicFarMuffleNearTransmission,
                CinematicFarMuffleFarTransmission,
                far01);
            lowPassCutoffHz = math.lerp(
                CinematicFarMuffleNearCutoffHertz,
                CinematicFarMuffleFarCutoffHertz,
                far01);
        }

        private bool TryResolveCinematicZoneMismatch(
            Vector3 sourceRuntimePosition,
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup)
        {
            bool listenerInsideCave = _listenerContainingCaveCount > 0;
            bool sourceInsideListenerCave = listenerInsideCave && IsInsideListenerContainingCave(sourceRuntimePosition);
            if (listenerInsideCave != sourceInsideListenerCave)
                return true;

            bool listenerInsideBase = _listenerInsideBaseInteriorMuffle;
            if (!listenerInsideBase && _baseInteriorMuffleCount <= 0)
                return false;

            bool sourceInsideBase = IsInsideCachedBaseInteriorAup(in sourceAup);
            return listenerInsideBase != sourceInsideBase;
        }

        private static bool IsListenerInteriorZoneActive()
        {
            AcousticZoneController acousticZone = GlobalRegistry.AcousticZone;
            return acousticZone != null && acousticZone.IsInterior;
        }

        private void RefreshBaseInteriorMuffleCache(in AbsoluteUniversePosition listenerAup)
        {
            int activeModuleCount = BaseModule.ActiveModuleCount;
            int writeIndex = 0;
            for (int i = 0; i < activeModuleCount && writeIndex < MaxCachedBaseInteriorMuffleZones; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null || !module.isActiveAndEnabled)
                    continue;

                if (!module.TryGetInteriorHazardBounds(out Vector3 worldCenter, out float radius))
                    continue;

                if (!IsFinite(worldCenter) || radius <= 0.01f || !math.isfinite(radius))
                    continue;

                _baseInteriorMuffleAups[writeIndex] = AbsoluteUniversePosition.FromRuntimePosition(worldCenter);
                _baseInteriorMuffleRadiusSq[writeIndex] = (double)radius * radius;
                writeIndex++;
            }

            _baseInteriorMuffleCount = writeIndex;
            _listenerInsideBaseInteriorMuffle = IsListenerInteriorZoneActive() || IsInsideCachedBaseInteriorAup(in listenerAup);
        }

        private void ResetBaseInteriorMuffleCache()
        {
            _baseInteriorMuffleCount = 0;
            _listenerInsideBaseInteriorMuffle = false;
        }

        private bool IsInsideCachedBaseInteriorAup(in AbsoluteUniversePosition positionAup)
        {
            int count = _baseInteriorMuffleCount;
            for (int i = 0; i < count; i++)
            {
                AbsoluteUniversePosition centerAup = _baseInteriorMuffleAups[i];
                if (AbsoluteUniversePosition.DistanceSq(in positionAup, in centerAup) <= _baseInteriorMuffleRadiusSq[i])
                    return true;
            }

            return false;
        }

        private void UpdateWorldSourceAudioLod(
            int sourceIndex,
            AudioSource source,
            Vector3 sourcePosition,
            Vector3 sourceAbsolutePosition,
            in AbsoluteUniversePosition sourceAup,
            Transform listener,
            in AbsoluteUniversePosition listenerAup,
            Vector3 listenerRuntimePosition,
            float3 listenerRight,
            float3 listenerForward,
            Vector3 listenerAbsolutePosition,
            float now,
            bool forceImmediate)
        {
            if (source == null)
                return;

            AudioLodTier resolvedTier = listener != null
                ? ResolveAudioLodTier(in sourceAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (resolvedTier == AudioLodTier.Tier2Culled)
            {
                if (_audioLodTiers != null && sourceIndex >= 0 && sourceIndex < _audioLodTiers.Length)
                    _audioLodTiers[sourceIndex] = resolvedTier;
                source.Stop();
                source.enabled = false;
                ResetWorldSourceState(sourceIndex, true);
                return;
            }

            float rearHemisphereCutoff = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            bool rearHemisphereFilterEnabled = listener != null &&
                TryResolveRearHemisphereLowPassCutoff(in sourceAup, listenerForward, in listenerAup, out rearHemisphereCutoff);
            bool caveLowPassEnabled = TryResolveCaveExternalLowPassCutoff(source, sourcePosition, out float caveLowPassCutoff);
            bool cinematicMuffleEnabled = false;
            float cinematicTransmission01 = 1f;
            float cinematicLowPassCutoff = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            if (listener != null)
            {
                cinematicMuffleEnabled = TryRefreshSourceCinematicMuffle(
                    sourceIndex,
                    sourcePosition,
                    in sourceAup,
                    in listenerAup,
                    now,
                    forceImmediate,
                    out cinematicTransmission01,
                    out cinematicLowPassCutoff);
            }
            else if (_sourceCinematicMuffleTransmissions != null &&
                     _sourceCinematicMuffleLowPassCutoffs != null &&
                     sourceIndex >= 0 &&
                     sourceIndex < _sourceCinematicMuffleTransmissions.Length &&
                     sourceIndex < _sourceCinematicMuffleLowPassCutoffs.Length)
            {
                _sourceCinematicMuffleTransmissions[sourceIndex] = 1f;
                _sourceCinematicMuffleLowPassCutoffs[sourceIndex] = AcousticOcclusionUtility.OpenLowPassCutoffHertz;
            }
            if (_baseVolumes != null && sourceIndex >= 0 && sourceIndex < _baseVolumes.Length)
                source.volume = _baseVolumes[sourceIndex] * cinematicTransmission01;
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
                    if (cinematicMuffleEnabled)
                        tierZeroCutoff = math.min(tierZeroCutoff, cinematicLowPassCutoff);
                    ApplyLowPassFilter(
                        sourceIndex,
                        rearHemisphereFilterEnabled || caveLowPassEnabled || cinematicMuffleEnabled,
                        tierZeroCutoff);
                    _nextTierUpdateTimes[sourceIndex] = 0f;
                    return;

                case AudioLodTier.Tier1Reduced:
                    source.enabled = true;
                    source.panStereo = listener != null
                        ? ResolveStereoPan(in sourceAup, in listenerAup, listenerRight)
                        : 0f;
                    float tierOneCutoff = Tier1LowPassCutoffHertz;
                    if (rearHemisphereFilterEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, rearHemisphereCutoff);
                    if (caveLowPassEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, caveLowPassCutoff);
                    if (cinematicMuffleEnabled)
                        tierOneCutoff = math.min(tierOneCutoff, cinematicLowPassCutoff);
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

        private static void PlayAcousticSource(AudioSource source, float delaySeconds)
        {
            if (source == null)
                return;

            float delay = math.clamp(delaySeconds, 0f, AcousticPortalMaximumPlayDelaySeconds);
            if (delay > 0.001f)
                source.PlayDelayed(delay);
            else
                source.Play();
        }

        private void ApplyAcousticPortalPresentation(
            int sourceIndex,
            AudioSource source,
            in AcousticPathResult result)
        {
            if (source == null || result.UsedPortalPath == 0)
                return;

            float cutoff = math.clamp(
                result.LowPassCutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (_lowPassFilters != null && sourceIndex >= 0 && sourceIndex < _lowPassFilters.Length)
            {
                AudioLowPassFilter lowPass = _lowPassFilters[sourceIndex];
                if (lowPass != null && lowPass.enabled)
                    cutoff = math.min(cutoff, lowPass.cutoffFrequency);
            }

            if (cutoff < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f)
                ApplyLowPassFilter(sourceIndex, true, cutoff);

            if (math.abs(result.ItdSeconds) > 0.00001f)
            {
                float panOffset = result.ItdSeconds * math.rcp(AcousticPortalConstants.MaximumItdSeconds);
                if (math.isfinite(panOffset))
                    source.panStereo = math.clamp(source.panStereo + panOffset, -1f, 1f);
            }

            if (result.RoomVolumeCubicMeters > SabineMinimumRoomVolumeCubicMeters)
                source.reverbZoneMix = ResolveAcousticPortalReverbMix(result.RoomVolumeCubicMeters);
        }

        private static float ResolveAcousticPortalReverbMix(float roomVolumeCubicMeters)
        {
            float volume01 = math.saturate(roomVolumeCubicMeters * math.rcp(SabineReverbModuleVolumeReferenceCubicMeters));
            return math.clamp(0.12f + math.sqrt(volume01) * 0.68f, 0f, 1.1f);
        }

        private bool TryResolveAcousticPortalPath(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            float3 listenerRight,
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            int stationaryCacheKey,
            out AcousticPathResult result)
        {
            result = default;
            if (!ShouldUseAcousticPortalPath() ||
                !_acousticPortalNodes.IsCreated ||
                !_acousticPortalEdges.IsCreated ||
                !_acousticPortalResult.IsCreated ||
                !_acousticPortalOpenSet.IsCreated ||
                !_acousticPortalClosedSet.IsCreated)
            {
                return false;
            }

            AcousticAup acousticSource = ToAcousticAup(in sourceAup);
            AcousticAup acousticListener = ToAcousticAup(in listenerAup);
            int cacheKey = ComputeAcousticPortalCacheKey(in acousticSource, in acousticListener, stationaryCacheKey);
            if (TryReadAcousticPortalCache(cacheKey, in acousticSource, in acousticListener, out result))
            {
                WriteAcousticPortalBlackBox(in result, Time.frameCount);
                return result.Status == AcousticPathStatus.PathFound && result.UsedPortalPath != 0;
            }

            if (!TryBuildAcousticPortalGraph(sourceRuntimePosition, listenerRuntimePosition, out int nodeCount, out int edgeCount))
                return false;

            AcousticPathQuery query = new AcousticPathQuery
            {
                SourceAup = acousticSource,
                ListenerAup = acousticListener,
                ListenerRight = listenerRight,
                NodeCount = nodeCount,
                EdgeCount = edgeCount,
                MaxNodeExpansions = AcousticPortalMaxNodes,
                QualityTier = (byte)GlobalRegistry.ScalabilityTier,
                DisablePortalPath = 0
            };

            double start = Time.realtimeSinceStartupAsDouble;
            new AcousticPathJob
            {
                Nodes = _acousticPortalNodes,
                Edges = _acousticPortalEdges,
                OpenSet = _acousticPortalOpenSet,
                ClosedSet = _acousticPortalClosedSet,
                Costs = _acousticPortalCosts,
                CameFrom = _acousticPortalCameFrom,
                States = _acousticPortalStates,
                Result = _acousticPortalResult,
                Query = query
            }.Run();

            result = _acousticPortalResult[0];
            result.PathfindingMs = (float)((Time.realtimeSinceStartupAsDouble - start) * 1000.0);
            WriteAcousticPortalBlackBox(in result, Time.frameCount);
            if (result.Status == AcousticPathStatus.PathFound && result.UsedPortalPath != 0)
            {
                WriteAcousticPortalCache(cacheKey, in acousticSource, in acousticListener, in result);
                return true;
            }

            return false;
        }

        private static bool ShouldUseAcousticPortalPath()
        {
            HectonQualityTier tier = GlobalRegistry.ScalabilityTier;
            return tier != HectonQualityTier.Unknown &&
                   tier != HectonQualityTier.Low &&
                   tier != HectonQualityTier.Mx350;
        }

        private bool TryBuildAcousticPortalGraph(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            out int nodeCount,
            out int edgeCount)
        {
            if (TryBuildHabitatAcousticPortalGraph(sourceRuntimePosition, listenerRuntimePosition, out nodeCount, out edgeCount))
                return true;

            return TryBuildVoxelAcousticPortalGraph(sourceRuntimePosition, listenerRuntimePosition, out nodeCount, out edgeCount);
        }

        private bool TryBuildVoxelAcousticPortalGraph(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            if (_acousticPortalWaypointScratch == null ||
                !VoxelDynamicNavGridRuntime.TryBuildMacroPortalRouteNonAlloc(
                    (float3)sourceRuntimePosition,
                    (float3)listenerRuntimePosition,
                    _acousticPortalWaypointScratch,
                    out int waypointCount) ||
                waypointCount < 2)
            {
                return false;
            }

            nodeCount = math.min(waypointCount, AcousticPortalMaxNodes);
            for (int i = 0; i < nodeCount; i++)
            {
                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(_acousticPortalWaypointScratch[i]);
                _acousticPortalNodes[i] = new AcousticPortalNode
                {
                    Position = ToAcousticAup(in aup),
                    FirstEdge = edgeCount,
                    EdgeCount = 0,
                    RoomVolumeCubicMeters = 0f,
                    Flags = AcousticPortalFlags.Voxel
                };

                int startEdge = edgeCount;
                if (i > 0 && edgeCount < AcousticPortalMaxEdges)
                {
                    _acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = i - 1,
                        DistanceMeters = Vector3.Distance(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i - 1]),
                        Flags = AcousticPortalFlags.Voxel
                    };
                }

                if (i + 1 < nodeCount && edgeCount < AcousticPortalMaxEdges)
                {
                    _acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = i + 1,
                        DistanceMeters = Vector3.Distance(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i + 1]),
                        Flags = AcousticPortalFlags.Voxel
                    };
                }

                AcousticPortalNode node = _acousticPortalNodes[i];
                node.FirstEdge = startEdge;
                node.EdgeCount = edgeCount - startEdge;
                _acousticPortalNodes[i] = node;
            }

            return nodeCount >= 2 && edgeCount > 0;
        }

        private bool TryBuildHabitatAcousticPortalGraph(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            ConstructionManager constructionManager = GlobalRegistry.ConstructionRuntime;
            if (constructionManager == null ||
                !constructionManager.TryGetHabitatAcousticGraph(out HabitatGraphManager graph) ||
                _acousticHabitatNodeMap == null ||
                _acousticHabitatQueue == null ||
                graph.NodeCount < 2)
            {
                return false;
            }

            if (!TryFindNearestHabitatNode(graph, (float3)sourceRuntimePosition, out int sourceNode, out float sourceDistanceSq) ||
                !TryFindNearestHabitatNode(graph, (float3)listenerRuntimePosition, out int listenerNode, out float listenerDistanceSq))
            {
                return false;
            }

            float maxAssociationSq = AcousticPortalHabitatAssociationMaxDistanceMeters * AcousticPortalHabitatAssociationMaxDistanceMeters;
            if (sourceDistanceSq > maxAssociationSq || listenerDistanceSq > maxAssociationSq)
                return false;

            for (int i = 0; i < _acousticHabitatNodeMap.Length; i++)
                _acousticHabitatNodeMap[i] = -1;

            int queueRead = 0;
            int queueWrite = 0;
            MapHabitatNode(sourceNode, ref nodeCount, ref queueWrite);
            MapHabitatNode(listenerNode, ref nodeCount, ref queueWrite);

            NativeArray<int> edgeOffsets = graph.EdgeOffsets;
            NativeArray<int> edgeDestinations = graph.EdgeDestinations;
            while (queueRead < queueWrite && nodeCount < AcousticPortalMaxNodes)
            {
                int globalNode = _acousticHabitatQueue[queueRead++];
                if ((uint)globalNode >= (uint)graph.NodeCount || globalNode + 1 >= edgeOffsets.Length)
                    continue;

                int start = math.clamp(edgeOffsets[globalNode], 0, graph.EdgeCount);
                int end = math.clamp(edgeOffsets[globalNode + 1], start, graph.EdgeCount);
                for (int edgeIndex = start; edgeIndex < end && nodeCount < AcousticPortalMaxNodes; edgeIndex++)
                {
                    if ((uint)edgeIndex >= (uint)edgeDestinations.Length)
                        break;

                    int destination = edgeDestinations[edgeIndex];
                    if ((uint)destination < (uint)graph.NodeCount)
                        MapHabitatNode(destination, ref nodeCount, ref queueWrite);
                }
            }

            for (int localIndex = 0; localIndex < nodeCount; localIndex++)
            {
                int globalIndex = _acousticHabitatNodeMap[localIndex];
                if (!graph.TryGetAcousticNodePosition(globalIndex, out float3 nodePosition))
                    return false;

                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(new Vector3(nodePosition.x, nodePosition.y, nodePosition.z));
                float roomVolume = 0f;
                NativeArray<float> roomVolumes = graph.RoomVolumes;
                if (roomVolumes.IsCreated && (uint)globalIndex < (uint)roomVolumes.Length)
                    roomVolume = math.max(0f, roomVolumes[globalIndex]);

                _acousticPortalNodes[localIndex] = new AcousticPortalNode
                {
                    Position = ToAcousticAup(in aup),
                    FirstEdge = 0,
                    EdgeCount = 0,
                    RoomVolumeCubicMeters = roomVolume,
                    Flags = AcousticPortalFlags.Habitat
                };
            }

            NativeArray<byte> edgeFlags = graph.EdgeFlags;
            NativeArray<float> edgeResistance = graph.EdgeResistance;
            for (int localIndex = 0; localIndex < nodeCount && edgeCount < AcousticPortalMaxEdges; localIndex++)
            {
                int globalNode = _acousticHabitatNodeMap[localIndex];
                if ((uint)globalNode >= (uint)graph.NodeCount || globalNode + 1 >= edgeOffsets.Length)
                    continue;

                int startEdge = edgeCount;
                int start = math.clamp(edgeOffsets[globalNode], 0, graph.EdgeCount);
                int end = math.clamp(edgeOffsets[globalNode + 1], start, graph.EdgeCount);
                for (int graphEdgeIndex = start; graphEdgeIndex < end && edgeCount < AcousticPortalMaxEdges; graphEdgeIndex++)
                {
                    if ((uint)graphEdgeIndex >= (uint)edgeDestinations.Length)
                        break;

                    int destinationGlobal = edgeDestinations[graphEdgeIndex];
                    int destinationLocal = FindMappedHabitatNode(destinationGlobal, nodeCount);
                    if (destinationLocal < 0)
                        continue;

                    AcousticPortalFlags flags = AcousticPortalFlags.Habitat;
                    if (edgeFlags.IsCreated &&
                        (uint)graphEdgeIndex < (uint)edgeFlags.Length &&
                        (edgeFlags[graphEdgeIndex] & (byte)HabitatEdgeFloodFlags.Sealed) != 0)
                    {
                        flags |= AcousticPortalFlags.SealedBulkhead;
                    }

                    AcousticPortalNode localNode = _acousticPortalNodes[localIndex];
                    AcousticPortalNode destinationNode = _acousticPortalNodes[destinationLocal];
                    float distance = AcousticAup.DistanceMeters(
                        in localNode.Position,
                        in destinationNode.Position);
                    if ((!math.isfinite(distance) || distance <= 0.001f) &&
                        edgeResistance.IsCreated &&
                        (uint)graphEdgeIndex < (uint)edgeResistance.Length)
                    {
                        distance = math.max(1f, edgeResistance[graphEdgeIndex] * 20f);
                    }

                    _acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = destinationLocal,
                        DistanceMeters = math.max(0.001f, distance),
                        Flags = flags
                    };
                }

                AcousticPortalNode node = _acousticPortalNodes[localIndex];
                node.FirstEdge = startEdge;
                node.EdgeCount = edgeCount - startEdge;
                _acousticPortalNodes[localIndex] = node;
            }

            return nodeCount >= 2 && edgeCount > 0;
        }

        private bool TryFindNearestHabitatNode(
            HabitatGraphManager graph,
            float3 runtimePosition,
            out int nodeIndex,
            out float distanceSq)
        {
            nodeIndex = -1;
            distanceSq = float.PositiveInfinity;
            int count = graph != null ? graph.NodeCount : 0;
            for (int i = 0; i < count; i++)
            {
                if (!graph.TryGetAcousticNodePosition(i, out float3 nodePosition))
                    continue;

                float candidateDistanceSq = math.lengthsq(nodePosition - runtimePosition);
                if (candidateDistanceSq < distanceSq)
                {
                    distanceSq = candidateDistanceSq;
                    nodeIndex = i;
                }
            }

            return nodeIndex >= 0 && math.isfinite(distanceSq);
        }

        private int MapHabitatNode(int globalNode, ref int mappedCount, ref int queueWrite)
        {
            int existing = FindMappedHabitatNode(globalNode, mappedCount);
            if (existing >= 0)
                return existing;

            if (mappedCount >= AcousticPortalMaxNodes || queueWrite >= _acousticHabitatQueue.Length)
                return -1;

            int localIndex = mappedCount++;
            _acousticHabitatNodeMap[localIndex] = globalNode;
            _acousticHabitatQueue[queueWrite++] = globalNode;
            return localIndex;
        }

        private int FindMappedHabitatNode(int globalNode, int mappedCount)
        {
            if (_acousticHabitatNodeMap == null)
                return -1;

            int count = math.min(mappedCount, _acousticHabitatNodeMap.Length);
            for (int i = 0; i < count; i++)
            {
                if (_acousticHabitatNodeMap[i] == globalNode)
                    return i;
            }

            return -1;
        }

        private bool TryReadAcousticPortalCache(
            int key,
            in AcousticAup sourceAup,
            in AcousticAup listenerAup,
            out AcousticPathResult result)
        {
            result = default;
            if (_acousticPortalCache == null)
                return false;

            for (int i = 0; i < _acousticPortalCache.Length; i++)
            {
                AcousticPortalCacheEntry entry = _acousticPortalCache[i];
                if (entry.Valid == 0 || entry.Key != key)
                    continue;

                if (AcousticAup.DistanceMeters(in entry.SourceAup, in sourceAup) > AcousticPortalCacheReuseDistanceMeters ||
                    AcousticAup.DistanceMeters(in entry.ListenerAup, in listenerAup) > AcousticPortalCacheReuseDistanceMeters)
                {
                    continue;
                }

                result = entry.Result;
                result.UsedReprojectionCache = 1;
                result.PathfindingMs = 0f;
                return true;
            }

            return false;
        }

        private void WriteAcousticPortalCache(
            int key,
            in AcousticAup sourceAup,
            in AcousticAup listenerAup,
            in AcousticPathResult result)
        {
            if (_acousticPortalCache == null || _acousticPortalCache.Length == 0)
                return;

            int frame = Time.frameCount;
            int writeIndex = 0;
            int oldestFrame = int.MaxValue;
            for (int i = 0; i < _acousticPortalCache.Length; i++)
            {
                if (_acousticPortalCache[i].Valid == 0)
                {
                    writeIndex = i;
                    oldestFrame = int.MinValue;
                    break;
                }

                if (_acousticPortalCache[i].Frame < oldestFrame)
                {
                    oldestFrame = _acousticPortalCache[i].Frame;
                    writeIndex = i;
                }
            }

            _acousticPortalCache[writeIndex] = new AcousticPortalCacheEntry
            {
                Key = key,
                Frame = frame,
                Valid = 1,
                SourceAup = sourceAup,
                ListenerAup = listenerAup,
                Result = result
            };
        }

        private static int ComputeAcousticPortalCacheKey(
            in AcousticAup sourceAup,
            in AcousticAup listenerAup,
            int stationaryCacheKey)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashAcousticAup(hash, in sourceAup);
                hash = HashAcousticAup(hash, in listenerAup);
                hash = (hash ^ (uint)stationaryCacheKey) * 16777619u;
                return (int)hash;
            }
        }

        private static uint HashAcousticAup(uint hash, in AcousticAup aup)
        {
            unchecked
            {
                hash = (hash ^ (uint)aup.GridX) * 16777619u;
                hash = (hash ^ (uint)(aup.GridX >> 32)) * 16777619u;
                hash = (hash ^ (uint)aup.GridY) * 16777619u;
                hash = (hash ^ (uint)(aup.GridY >> 32)) * 16777619u;
                hash = (hash ^ (uint)aup.GridZ) * 16777619u;
                hash = (hash ^ (uint)(aup.GridZ >> 32)) * 16777619u;
                hash = (hash ^ (uint)math.round(aup.Local.x)) * 16777619u;
                hash = (hash ^ (uint)math.round(aup.Local.y)) * 16777619u;
                hash = (hash ^ (uint)math.round(aup.Local.z)) * 16777619u;
                return hash;
            }
        }

        private void WriteAcousticPortalBlackBox(in AcousticPathResult result, int frame)
        {
            if (!_acousticPortalBlackBox.IsCreated)
                return;

            uint flags = 0u;
            if (result.UsedPortalPath != 0)
                flags |= 1u;
            if (result.UsedSealedBulkhead != 0)
                flags |= 2u;
            if (result.UsedReprojectionCache != 0)
                flags |= 4u;

            int index = _acousticPortalBlackBoxCursor % _acousticPortalBlackBox.Length;
            _acousticPortalBlackBox[index] = new AcousticTelemetryEntry
            {
                Frame = frame,
                NodeCount = result.NodeCount,
                CornerCount = result.CornerCount,
                ExpandedNodeCount = result.ExpandedNodeCount,
                PathfindingMs = result.PathfindingMs,
                TrueDistanceMeters = result.TrueDistanceMeters,
                DelaySeconds = result.DelaySeconds,
                LowPassCutoffHz = result.LowPassCutoffHz,
                Flags = flags,
                StateHash = result.StateHash
            };
            _acousticPortalBlackBoxCursor = (index + 1) % _acousticPortalBlackBox.Length;

            if (!IsAcousticPathResultFinite(in result))
                DumpAcousticPortalBlackBox();
        }

        private static bool IsAcousticPathResultFinite(in AcousticPathResult result)
        {
            return math.isfinite(result.PathfindingMs) &&
                   math.isfinite(result.TrueDistanceMeters) &&
                   math.isfinite(result.DelaySeconds) &&
                   math.isfinite(result.LowPassCutoffHz) &&
                   math.isfinite(result.Transmission01) &&
                   math.isfinite(result.ItdSeconds);
        }

        private void DumpAcousticPortalBlackBox()
        {
            if (!_acousticPortalBlackBox.IsCreated)
                return;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string dumpPath = Path.GetFullPath(Path.Combine(projectRoot, AcousticPortalDumpRelativePath));
                string directory = Path.GetDirectoryName(dumpPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(_acousticPortalBlackBox.Length);
                    writer.Write(_acousticPortalBlackBoxCursor);
                    for (int i = 0; i < _acousticPortalBlackBox.Length; i++)
                    {
                        AcousticTelemetryEntry entry = _acousticPortalBlackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.NodeCount);
                        writer.Write(entry.CornerCount);
                        writer.Write(entry.ExpandedNodeCount);
                        writer.Write(entry.PathfindingMs);
                        writer.Write(entry.TrueDistanceMeters);
                        writer.Write(entry.DelaySeconds);
                        writer.Write(entry.LowPassCutoffHz);
                        writer.Write(entry.Flags);
                        writer.Write(entry.StateHash);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogError($"[SpatialAudioManager] Failed to dump acoustic portal blackbox: {exception.Message}", this);
#endif
            }
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

        private AudioLodTier ResolveAudioLodTier(in AbsoluteUniversePosition sourceAup, in AbsoluteUniversePosition listenerAup)
        {
            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            if (distanceSq > (Tier1ReducedDspDistanceMeters * Tier1ReducedDspDistanceMeters))
                return AudioLodTier.Tier2Culled;

            return distanceSq > (Tier0FullDspDistanceMeters * Tier0FullDspDistanceMeters)
                ? AudioLodTier.Tier1Reduced
                : AudioLodTier.Tier0Full;
        }

        private static float ResolveStereoPan(in AbsoluteUniversePosition sourceAup, in AbsoluteUniversePosition listenerAup, float3 listenerRight)
        {
            float3 listenerWorldDelta = ResolveAupDelta(in listenerAup, in sourceAup);
            float lateralPan = math.dot(listenerWorldDelta, listenerRight) * math.rcp(math.max(0.01f, StereoPanDistanceNormalizationMeters));
            return math.clamp(lateralPan, -1f, 1f);
        }

        private static bool TryResolveRearHemisphereLowPassCutoff(
            in AbsoluteUniversePosition sourceAup,
            float3 listenerForward,
            in AbsoluteUniversePosition listenerAup,
            out float cutoffFrequency)
        {
            cutoffFrequency = 22000f;

            float3 toSource = ResolveAupDelta(in listenerAup, in sourceAup);
            float distanceSq = math.lengthsq(toSource);
            if (distanceSq <= 0.0001f)
                return false;

            float3 sourceDirection = ResolveDominantAxisDirection(toSource);
            float forwardDot = math.dot(listenerForward, sourceDirection);
            if (forwardDot >= RearHemisphereLowPassStartDot)
                return false;

            float rear01 = math.saturate(
                (forwardDot - RearHemisphereLowPassStartDot) *
                math.rcp(math.max(RearHemisphereLowPassFullDot - RearHemisphereLowPassStartDot, 0.0001f)));
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

            if (_radarNearestEmitterAups == null || _radarNearestEmitterAups.Length != AcousticRadarNearestEmitterLimit)
                _radarNearestEmitterAups = new AbsoluteUniversePosition[AcousticRadarNearestEmitterLimit]; // COLD ALLOC: AbsoluteUniversePosition[12] - nearest-emitter radar AUP cache avoiding repeated runtime conversions - owner: SpatialAudioManager

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
                PrewarmDelayedAudioIngressQueue();
            }

            if (!_audioEventQueue.IsCreated)
            {
                _audioEventQueue = new NativeQueue<CoreAudioEvent>(Allocator.Persistent); // COLD ALLOC: NativeQueue<CoreAudioEvent>[32] - zero-GC gameplay audio ingress drained by SpatialAudioManager LateFrameTick - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _audioEventQueue,
                    MaxQueuedAudioEvents,
                    nameof(SpatialAudioManager),
                    nameof(_audioEventQueue),
                    NativeAllocationLifetime.Session);
                PrewarmAudioEventQueue();
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

            if (!_soundEmissionSignals.IsCreated)
            {
                _soundEmissionSignals = new NativeQueue<SoundEmissionSignal>(Allocator.Persistent); // COLD ALLOC: NativeQueue<SoundEmissionSignal>[32] - AUP acoustic emission ingress - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _soundEmissionSignals,
                    MaxQueuedSoundEmissionSignals,
                    nameof(SpatialAudioManager),
                    nameof(_soundEmissionSignals),
                    NativeAllocationLifetime.Session);
                PrewarmSoundEmissionSignalQueue();
            }

            if (!_acousticPortalNodes.IsCreated)
            {
                _acousticPortalNodes = new NativeArray<AcousticPortalNode>(AcousticPortalMaxNodes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticPortalNode>[30] - acoustic portal route nodes - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalNodes, nameof(SpatialAudioManager), nameof(_acousticPortalNodes), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalEdges.IsCreated)
            {
                _acousticPortalEdges = new NativeArray<AcousticPortalEdge>(AcousticPortalMaxEdges, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticPortalEdge>[60] - acoustic portal route edges - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalEdges, nameof(SpatialAudioManager), nameof(_acousticPortalEdges), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalResult.IsCreated)
            {
                _acousticPortalResult = new NativeArray<AcousticPathResult>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticPathResult>[1] - acoustic portal Burst result slot - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalResult, nameof(SpatialAudioManager), nameof(_acousticPortalResult), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalCosts.IsCreated)
            {
                _acousticPortalCosts = new NativeArray<float>(AcousticPortalMaxNodes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float>[30] - acoustic path Dijkstra cost scratch - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalCosts, nameof(SpatialAudioManager), nameof(_acousticPortalCosts), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalCameFrom.IsCreated)
            {
                _acousticPortalCameFrom = new NativeArray<int>(AcousticPortalMaxNodes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<int>[30] - acoustic path predecessor scratch - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalCameFrom, nameof(SpatialAudioManager), nameof(_acousticPortalCameFrom), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalStates.IsCreated)
            {
                _acousticPortalStates = new NativeArray<byte>(AcousticPortalMaxNodes, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<byte>[30] - acoustic path open/closed state scratch - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalStates, nameof(SpatialAudioManager), nameof(_acousticPortalStates), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalOpenSet.IsCreated)
            {
                _acousticPortalOpenSet = new NativeList<int>(AcousticPortalMaxNodes, Allocator.Persistent); // COLD ALLOC: NativeList<int>[30] - acoustic path open set - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(_acousticPortalOpenSet, nameof(SpatialAudioManager), nameof(_acousticPortalOpenSet), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalClosedSet.IsCreated)
            {
                _acousticPortalClosedSet = new NativeList<int>(AcousticPortalMaxNodes, Allocator.Persistent); // COLD ALLOC: NativeList<int>[30] - acoustic path closed set - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(_acousticPortalClosedSet, nameof(SpatialAudioManager), nameof(_acousticPortalClosedSet), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalBlackBox.IsCreated)
            {
                _acousticPortalBlackBox = new NativeArray<AcousticTelemetryEntry>(AcousticPortalConstants.TelemetryFrameCount, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<AcousticTelemetryEntry>[300] - acoustic portal blackbox - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeArray(_acousticPortalBlackBox, nameof(SpatialAudioManager), nameof(_acousticPortalBlackBox), NativeAllocationLifetime.Session);
            }

            if (_acousticPortalWaypointScratch == null || _acousticPortalWaypointScratch.Length != AcousticPortalMaxNodes)
                _acousticPortalWaypointScratch = new Vector3[AcousticPortalMaxNodes]; // COLD ALLOC: Vector3[30] - voxel macro portal waypoint scratch - owner: SpatialAudioManager

            if (_acousticHabitatNodeMap == null || _acousticHabitatNodeMap.Length != AcousticPortalMaxNodes)
                _acousticHabitatNodeMap = new int[AcousticPortalMaxNodes]; // COLD ALLOC: int[30] - habitat acoustic global-to-local node map - owner: SpatialAudioManager

            if (_acousticHabitatQueue == null || _acousticHabitatQueue.Length != AcousticPortalMaxNodes)
                _acousticHabitatQueue = new int[AcousticPortalMaxNodes]; // COLD ALLOC: int[30] - habitat acoustic BFS queue - owner: SpatialAudioManager

            if (_acousticPortalCache == null || _acousticPortalCache.Length != AcousticPortalCacheCapacity)
                _acousticPortalCache = new AcousticPortalCacheEntry[AcousticPortalCacheCapacity]; // COLD ALLOC: AcousticPortalCacheEntry[16] - stationary emitter acoustic reprojection cache - owner: SpatialAudioManager
        }

        private void PrewarmDelayedAudioIngressQueue()
        {
            if (!_delayedAudioIngress.IsCreated)
                return;

            for (int i = 0; i < MaxDelayedAudioEvents; i++)
                _delayedAudioIngress.Enqueue(default);

            while (_delayedAudioIngress.TryDequeue(out _))
            {
            }

            _delayedAudioIngressCount = 0;
        }

        private void PrewarmAudioEventQueue()
        {
            if (!_audioEventQueue.IsCreated)
                return;

            for (int i = 0; i < MaxQueuedAudioEvents; i++)
                _audioEventQueue.Enqueue(default);

            while (_audioEventQueue.TryDequeue(out _))
            {
            }

            _audioEventQueueCount = 0;
            _audioEventQueueDroppedCount = 0;
        }

        private void PrewarmSoundEmissionSignalQueue()
        {
            if (!_soundEmissionSignals.IsCreated)
                return;

            for (int i = 0; i < MaxQueuedSoundEmissionSignals; i++)
                _soundEmissionSignals.Enqueue(default);

            while (_soundEmissionSignals.TryDequeue(out _))
            {
            }

            _soundEmissionSignalQueueCount = 0;
            _soundEmissionSignalDroppedCount = 0;
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

            if (_audioEventQueue.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpatialAudioManager), nameof(_audioEventQueue));
                _audioEventQueue.Dispose();
                _audioEventQueue = default;
            }

            if (_pendingDelayedAudioEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SpatialAudioManager), nameof(_pendingDelayedAudioEvents));
                _pendingDelayedAudioEvents.Dispose();
                _pendingDelayedAudioEvents = default;
            }

            if (_soundEmissionSignals.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SpatialAudioManager), nameof(_soundEmissionSignals));
                _soundEmissionSignals.Dispose();
                _soundEmissionSignals = default;
            }

            if (_acousticPortalNodes.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalNodes);
                _acousticPortalNodes.Dispose();
                _acousticPortalNodes = default;
            }

            if (_acousticPortalEdges.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalEdges);
                _acousticPortalEdges.Dispose();
                _acousticPortalEdges = default;
            }

            if (_acousticPortalResult.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalResult);
                _acousticPortalResult.Dispose();
                _acousticPortalResult = default;
            }

            if (_acousticPortalCosts.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalCosts);
                _acousticPortalCosts.Dispose();
                _acousticPortalCosts = default;
            }

            if (_acousticPortalCameFrom.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalCameFrom);
                _acousticPortalCameFrom.Dispose();
                _acousticPortalCameFrom = default;
            }

            if (_acousticPortalStates.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalStates);
                _acousticPortalStates.Dispose();
                _acousticPortalStates = default;
            }

            if (_acousticPortalOpenSet.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SpatialAudioManager), nameof(_acousticPortalOpenSet));
                _acousticPortalOpenSet.Dispose();
                _acousticPortalOpenSet = default;
            }

            if (_acousticPortalClosedSet.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SpatialAudioManager), nameof(_acousticPortalClosedSet));
                _acousticPortalClosedSet.Dispose();
                _acousticPortalClosedSet = default;
            }

            if (_acousticPortalBlackBox.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_acousticPortalBlackBox);
                _acousticPortalBlackBox.Dispose();
                _acousticPortalBlackBox = default;
            }

            _delayedAudioIngressCount = 0;
            _audioEventQueueCount = 0;
            _audioEventQueueDroppedCount = 0;
            _soundEmissionSignalQueueCount = 0;
            _soundEmissionSignalDroppedCount = 0;
            _acousticPortalBlackBoxCursor = 0;
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
            byte routeFlags = ResolveClipRouteFlags(clip);

            if (requestedGroup == _ambientGroup ||
                requestedGroup == _bedGroup ||
                (routeFlags & AudioClipRouteFlagBed) != 0)
                return ResolvedBedBusGroup;

            if (requestedGroup == _threatGroup ||
                (routeFlags & AudioClipRouteFlagThreat) != 0)
                return ResolvedThreatBusGroup;

            return requestedGroup != null ? requestedGroup : _interfaceGroup;
        }

        private AudioMixerGroup ResolveWorldMixerGroup(AudioClip clip, AudioMixerGroup requestedGroup)
        {
            byte routeFlags = ResolveClipRouteFlags(clip);

            if (requestedGroup == _ambientGroup || requestedGroup == _bedGroup)
                return ResolvedBedBusGroup;

            if (requestedGroup == _threatGroup ||
                (routeFlags & AudioClipRouteFlagThreat) != 0)
                return ResolvedThreatBusGroup;

            if ((requestedGroup == null || requestedGroup == _sfxGroup) &&
                (routeFlags & AudioClipRouteFlagBed) != 0)
                return ResolvedBedBusGroup;

            return requestedGroup != null ? requestedGroup : ResolvedDefaultWorldMixerGroup;
        }

        private bool IsThreatWorldSource(int sourceIndex)
        {
            return _worldSourceBusFlags != null &&
                   sourceIndex >= 0 &&
                   sourceIndex < _worldSourceBusFlags.Length &&
                   (_worldSourceBusFlags[sourceIndex] & WorldSourceBusFlagThreat) != 0;
        }

        private float ResolveSourcePitch(int sourceIndex, float dopplerRatio)
        {
            if (_basePitches == null || sourceIndex < 0 || sourceIndex >= _basePitches.Length)
                return 1f;

            float eclipseRatio = ResolveEclipseAcousticPitchRatio(sourceIndex);
            return math.clamp(_basePitches[sourceIndex] * dopplerRatio * eclipseRatio * _timeDilationWorldPitchRatio, 0.1f, 3f);
        }

        private void UpdateTimeDilationPitchScalar()
        {
            float scalar = GlobalSignals.TimeDilationScalar;
            float saturatedScalar = math.saturate(scalar);
            float easedScalar = saturatedScalar * (2f - saturatedScalar);
            float targetRatio = math.lerp(TimeDilationAudioMinimumPitchRatio, 1f, easedScalar);
            if (math.abs(_timeDilationWorldPitchRatio - targetRatio) <= 0.001f)
                return;

            _timeDilationWorldPitchRatio = targetRatio;
            ApplyEclipsePitchShiftToActiveWorldSources();
        }

        private float ResolveEclipseAcousticPitchRatio(int sourceIndex)
        {
            if (math.abs(_eclipseAcousticPitchRatio - 1f) <= 0.0001f ||
                _worldSourceBusFlags == null ||
                sourceIndex < 0 ||
                sourceIndex >= _worldSourceBusFlags.Length ||
                (_worldSourceBusFlags[sourceIndex] & WorldSourceBusFlagBed) == 0)
            {
                return 1f;
            }

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

                source.pitch = ResolveSourcePitch(sourceIndex, _smoothedDopplerRatios[sourceIndex]);
            }
        }

        private void PrepareWorldDroneSource()
        {
            if (_worldDroneSource == null)
                return;

            if (_worldDroneSource.clip == null && _worldDroneClip != null)
                _worldDroneSource.clip = _worldDroneClip;

            _worldDroneSource.loop = true;
            if (_worldDroneSource.outputAudioMixerGroup == null)
                _worldDroneSource.outputAudioMixerGroup = ResolvedBedBusGroup;

            if (!_worldDroneSource.isPlaying && _worldDroneSource.clip != null)
                _worldDroneSource.Play();
        }

        private void ApplyWorldDroneGainDb(float gainDb)
        {
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer != null && _hasWorldDroneVolumeDbParameter)
                mixer.SetFloat(_worldDroneVolumeDbParameter, gainDb);

            if (_worldDroneSource != null)
                _worldDroneSource.volume = DbToLinearVolume(gainDb);
        }

        private void PrepareGlobalWindHowlSource()
        {
            if (_globalWindHowlSource == null)
                return;

            if (_globalWindHowlSource.clip == null && _globalWindHowlClip != null)
                _globalWindHowlSource.clip = _globalWindHowlClip;

            _globalWindHowlSource.loop = true;
            _globalWindHowlSource.playOnAwake = false;
            _globalWindHowlSource.spatialBlend = 0f;
            if (_globalWindHowlSource.outputAudioMixerGroup == null)
                _globalWindHowlSource.outputAudioMixerGroup = ResolvedBedBusGroup;

            if (_globalWindHowlLowPass == null)
                _globalWindHowlSource.TryGetComponent(out _globalWindHowlLowPass);

            _globalWindHowlSource.volume = 0f;
            ApplyGlobalWindHowlLowPass(_globalWindHowlOpenLowPassCutoffHz);
        }

        private void UpdateGlobalWindHowl(float deltaTime)
        {
            if (_globalWindHowlSource == null || _globalWindHowlSource.clip == null)
                return;

            float target01 = math.max(ResolveGlobalWindHowlTarget01(), _stormRoarShedCurrent01);
            float blendT = deltaTime > 0f
                ? FastDecayBlend(_globalWindHowlFadeSharpness, deltaTime)
                : 1f;
            _globalWindHowlVolume01 = math.lerp(_globalWindHowlVolume01, target01, blendT);
            float targetVolume = _globalWindHowlVolume01 * math.saturate(_globalWindHowlMaxVolume);
            _globalWindHowlSource.volume = targetVolume;

            if (targetVolume > 0.001f)
            {
                if (!_globalWindHowlSource.isPlaying)
                    _globalWindHowlSource.Play();
            }
            else if (_globalWindHowlSource.isPlaying)
            {
                _globalWindHowlSource.Stop();
            }

            bool windHowlOccluded = ResolveGlobalWindHowlOccluded();
            if (windHowlOccluded)
            {
                _globalWindHowlOcclusion01 = 1f;
            }
            else
            {
                float occlusionT = deltaTime > 0f
                    ? FastDecayBlend(_globalWindHowlOcclusionSharpness, deltaTime)
                    : 1f;
                _globalWindHowlOcclusion01 = math.lerp(_globalWindHowlOcclusion01, 0f, occlusionT);
            }

            float cutoff = math.lerp(
                math.max(20f, _globalWindHowlOpenLowPassCutoffHz),
                math.max(20f, _globalWindHowlInteriorLowPassCutoffHz),
                math.saturate(_globalWindHowlOcclusion01));
            ApplyGlobalWindHowlLowPass(cutoff);
        }

        private void UpdateStormRoarShedder(float deltaTime)
        {
            float target01 = math.saturate(_stormRoarShedTarget01);
            float sharpness = target01 > _stormRoarShedCurrent01
                ? StormRoarShedRiseSharpness
                : StormRoarShedReleaseSharpness;
            float blendT = deltaTime > 0f
                ? FastDecayBlend(sharpness, deltaTime)
                : 1f;
            _stormRoarShedCurrent01 = math.lerp(_stormRoarShedCurrent01, target01, blendT);

            float releaseT = deltaTime > 0f
                ? FastDecayBlend(StormRoarShedReleaseSharpness, deltaTime)
                : 1f;
            _stormRoarShedTarget01 = math.lerp(_stormRoarShedTarget01, 0f, releaseT);
        }

        private float ResolveGlobalWindHowlTarget01()
        {
            float target01 = 0f;

            IWeatherService weatherService = GlobalRegistry.Weather;
            if (weatherService != null && weatherService.IsInitialized)
            {
                Vector3 wind = weatherService.GlobalWindVector;
                float windSpeedSq = wind.x * wind.x + wind.y * wind.y + wind.z * wind.z;
                target01 = math.max(target01, math.saturate(windSpeedSq * math.rcp(GlobalWindHowlReferenceSpeedSq)));
                if ((weatherService.CurrentWeatherState & WeatherState.Storm) != 0)
                    target01 = math.max(target01, math.saturate(_globalWindHowlStormFloor));
            }

            HectonSurfaceWeatherDirector surfaceWeather = GlobalRegistry.SurfaceWeather;
            if (surfaceWeather != null && !surfaceWeather.IsSurfaceSuppressed)
            {
                float weatherPressure = math.saturate(
                    surfaceWeather.CurrentPrecipitationIntensity * 0.72f +
                    surfaceWeather.CurrentElectricalActivity * 0.48f);
                target01 = math.max(target01, weatherPressure);
                switch (surfaceWeather.CurrentWeatherKind)
                {
                    case SurfaceWeatherKind.HeavyRain:
                    case SurfaceWeatherKind.ElectricalStorm:
                        target01 = math.max(target01, math.saturate(_globalWindHowlStormFloor));
                        break;
                }
            }

            return math.saturate(target01);
        }

        private static bool ResolveGlobalWindHowlOccluded()
        {
            HectonSurfaceWeatherDirector surfaceWeather = GlobalRegistry.SurfaceWeather;
            if (surfaceWeather != null && surfaceWeather.IsLocallySheltered)
                return true;

            AcousticZoneController acousticZone = GlobalRegistry.AcousticZone;
            return acousticZone != null && acousticZone.IsInterior;
        }

        private void ApplyGlobalWindHowlLowPass(float cutoffHz)
        {
            if (_globalWindHowlLowPass == null)
                return;

            float resolvedCutoff = math.clamp(
                cutoffHz,
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                AcousticOcclusionUtility.OpenLowPassCutoffHertz);
            if (math.abs(resolvedCutoff - _lastGlobalWindHowlCutoffHz) <= 1f)
                return;

            _globalWindHowlLowPass.enabled = resolvedCutoff < AcousticOcclusionUtility.OpenLowPassCutoffHertz - 1f;
            _globalWindHowlLowPass.cutoffFrequency = resolvedCutoff;
            _lastGlobalWindHowlCutoffHz = resolvedCutoff;
        }

        private void ResetGlobalWindHowlState()
        {
            _globalWindHowlVolume01 = 0f;
            _globalWindHowlOcclusion01 = 0f;
            _lastGlobalWindHowlCutoffHz = -1f;
            _stormRoarShedTarget01 = 0f;
            _stormRoarShedCurrent01 = 0f;
            if (_globalWindHowlSource != null)
            {
                _globalWindHowlSource.volume = 0f;
                if (_globalWindHowlSource.isPlaying)
                    _globalWindHowlSource.Stop();
            }

            ApplyGlobalWindHowlLowPass(_globalWindHowlOpenLowPassCutoffHz);
        }

        private static float DbToLinearVolume(float gainDb)
        {
            float db = math.clamp(gainDb, -80f, 20f);
            if (db <= -60f)
                return 0.001f;

            if (db <= -40f)
                return math.lerp(0.001f, 0.01f, (db + 60f) * 0.05f);

            if (db <= -20f)
                return math.lerp(0.01f, 0.1f, (db + 40f) * 0.05f);

            if (db <= 0f)
                return math.lerp(0.1f, 1f, (db + 20f) * 0.05f);

            return math.lerp(1f, 3.2f, db * 0.05f);
        }

        private void ApplyThreatBusDucking(float threatActivity, float deltaTime)
        {
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer == null || !_hasBedDuckDbParameter)
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
                float duckBlend = FastDecayBlend(math.rcp(math.max(duckTimeSeconds, 0.0001f)), deltaTime);
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
                float blend = FastDecayBlend(sharpness, deltaTime);
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

            if (_hasParasiteRoomLowPassCutoffParameter &&
                math.abs(cutoffHz - _lastParasiteRoomLowPassCutoffHz) > 1f)
            {
                mixer.SetFloat(_parasiteRoomLowPassCutoffParameter, cutoffHz);
                _lastParasiteRoomLowPassCutoffHz = cutoffHz;
            }

            if (_hasParasiteOrganicLayerGainParameter &&
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

        private AudioMixer ResolveNarrativeRadioMixer()
        {
            if (_routingMixer != null)
                return _routingMixer;

            if (_encryptedVoiceGroup != null && _encryptedVoiceGroup.audioMixer != null)
                return _encryptedVoiceGroup.audioMixer;

            if (_interfaceGroup != null && _interfaceGroup.audioMixer != null)
                return _interfaceGroup.audioMixer;

            return _ambientGroup != null ? _ambientGroup.audioMixer : null;
        }

        private void RefreshMixerParameterAvailability()
        {
            _hasBedDuckDbParameter = !string.IsNullOrWhiteSpace(_bedDuckDbParameter);
            _hasWorldDroneVolumeDbParameter = !string.IsNullOrWhiteSpace(_worldDroneVolumeDbParameter);
            _hasParasiteRoomLowPassCutoffParameter = !string.IsNullOrWhiteSpace(_parasiteRoomLowPassCutoffParameter);
            _hasParasiteOrganicLayerGainParameter = !string.IsNullOrWhiteSpace(_parasiteOrganicLayerGainParameter);
            _hasNarrativeRadioLowPassCutoffParameter = !string.IsNullOrWhiteSpace(_narrativeRadioLowPassCutoffParameter);
        }

        private byte ResolveClipRouteFlags(AudioClip clip)
        {
            if (clip == null)
                return 0;

            int clipId = unchecked((int)EntityId.ToULong(clip.GetEntityId()));
            if (TryGetCachedClipRouteFlags(clipId, out byte routeFlags))
                return routeFlags;

            routeFlags = ClassifyClipRouteFlags(clip);
            CacheClipRouteFlags(clipId, routeFlags);
            return routeFlags;
        }

        private bool TryGetCachedClipRouteFlags(int clipId, out byte routeFlags)
        {
            if (_clipRouteCacheIds == null || _clipRouteCacheFlags == null)
            {
                routeFlags = 0;
                return false;
            }

            int cacheCount = math.min(_clipRouteCacheIds.Length, _clipRouteCacheFlags.Length);
            if (cacheCount <= 0)
            {
                routeFlags = 0;
                return false;
            }

            int slot = clipId & (cacheCount - 1);
            if (_clipRouteCacheIds[slot] == clipId)
            {
                routeFlags = _clipRouteCacheFlags[slot];
                return true;
            }

            routeFlags = 0;
            return false;
        }

        private void CacheClipRouteFlags(int clipId, byte routeFlags)
        {
            if (_clipRouteCacheIds == null || _clipRouteCacheFlags == null)
                return;

            int cacheCount = math.min(_clipRouteCacheIds.Length, _clipRouteCacheFlags.Length);
            if (cacheCount <= 0)
                return;

            int slot = clipId & (cacheCount - 1);
            _clipRouteCacheIds[slot] = clipId;
            _clipRouteCacheFlags[slot] = routeFlags;
        }

        private static byte ClassifyClipRouteFlags(AudioClip clip)
        {
            if (clip == null)
                return 0;

            string clipName = clip.name;
            byte routeFlags = 0;
            if (ContainsTokenInsensitive(clipName, "leviathan") ||
                ContainsTokenInsensitive(clipName, "roar") ||
                ContainsTokenInsensitive(clipName, "threat") ||
                ContainsTokenInsensitive(clipName, "predator") ||
                ContainsTokenInsensitive(clipName, "shriek"))
            {
                routeFlags |= AudioClipRouteFlagThreat;
            }

            if (ContainsTokenInsensitive(clipName, "ambient") ||
                ContainsTokenInsensitive(clipName, "ocean") ||
                ContainsTokenInsensitive(clipName, "water") ||
                ContainsTokenInsensitive(clipName, "current") ||
                ContainsTokenInsensitive(clipName, "bed") ||
                ContainsTokenInsensitive(clipName, "drone"))
            {
                routeFlags |= AudioClipRouteFlagBed;
            }

            return routeFlags;
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

        private void CacheActiveWorldRuntimePosition(int sourceIndex, Vector3 sourcePosition, int currentFrame)
        {
            if (_activeWorldRuntimePositions == null ||
                _activeWorldRuntimePositionFrames == null ||
                sourceIndex < 0 ||
                sourceIndex >= _activeWorldRuntimePositions.Length ||
                sourceIndex >= _activeWorldRuntimePositionFrames.Length)
            {
                return;
            }

            _activeWorldRuntimePositions[sourceIndex] = sourcePosition;
            _activeWorldRuntimePositionFrames[sourceIndex] = currentFrame;
        }

        private void CacheActiveWorldAup(int sourceIndex, in AbsoluteUniversePosition sourceAup, int currentFrame)
        {
            if (_activeWorldAups == null ||
                _activeWorldAupFrames == null ||
                sourceIndex < 0 ||
                sourceIndex >= _activeWorldAups.Length ||
                sourceIndex >= _activeWorldAupFrames.Length)
            {
                return;
            }

            _activeWorldAups[sourceIndex] = sourceAup;
            _activeWorldAupFrames[sourceIndex] = currentFrame;
        }

        private bool TryGetCachedActiveWorldRuntimePosition(int sourceIndex, out Vector3 sourcePosition)
        {
            sourcePosition = default;
            if (_activeWorldRuntimePositions == null ||
                _activeWorldRuntimePositionFrames == null ||
                sourceIndex < 0 ||
                sourceIndex >= _activeWorldRuntimePositions.Length ||
                sourceIndex >= _activeWorldRuntimePositionFrames.Length ||
                _activeWorldRuntimePositionFrames[sourceIndex] < 0)
            {
                return false;
            }

            sourcePosition = _activeWorldRuntimePositions[sourceIndex];
            return true;
        }

        private bool TryGetCachedActiveWorldAup(int sourceIndex, out AbsoluteUniversePosition sourceAup)
        {
            sourceAup = default;
            if (_activeWorldAups == null ||
                _activeWorldAupFrames == null ||
                sourceIndex < 0 ||
                sourceIndex >= _activeWorldAups.Length ||
                sourceIndex >= _activeWorldAupFrames.Length ||
                _activeWorldAupFrames[sourceIndex] < 0)
            {
                return false;
            }

            sourceAup = _activeWorldAups[sourceIndex];
            return true;
        }

        private AbsoluteUniversePosition ResolveActiveWorldAup(int sourceIndex, Vector3 sourcePosition, int currentFrame)
        {
            if (TryGetCachedActiveWorldAup(sourceIndex, out AbsoluteUniversePosition sourceAup))
                return sourceAup;

            sourceAup = AbsoluteUniversePosition.FromRuntimePosition(sourcePosition);
            CacheActiveWorldAup(sourceIndex, in sourceAup, currentFrame);
            return sourceAup;
        }

        private Transform ResolveWorldSourceRoot(int sourceIndex)
        {
            if (_worldSourceRoots == null || sourceIndex < 0 || sourceIndex >= _worldSourceRoots.Length)
                return null;

            return _worldSourceRoots[sourceIndex];
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
            {
                float energy = _acousticRadarIntensityBins[i];
                if (energy <= 0f)
                    continue;

                float decayed = energy * AcousticRadarDecayFactorPerSlowTick;
                _acousticRadarIntensityBins[i] = decayed > AcousticRadarEnergyEpsilon ? decayed : 0f;
            }
        }

        private void DecayAcousticRadarGrid()
        {
            if (!_acousticRadarGrid.IsCreated)
                return;

            bool dirty = false;
            for (int i = 0; i < _acousticRadarGrid.Length; i++)
            {
                float energy = _acousticRadarGrid[i];
                if (energy <= 0f)
                    continue;

                float decayed = energy * AcousticRadarDecayFactorPerSlowTick;
                _acousticRadarGrid[i] = decayed > AcousticRadarEnergyEpsilon ? decayed : 0f;
                dirty = true;
            }

            if (dirty)
                _acousticRadarGridDirty = true;
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

            _acousticRadarGridDirty = true;
        }

        private void ResetNearestRadarEmitterScratch()
        {
            if (_radarNearestEmitterDistanceSq == null ||
                _radarNearestEmitterPositions == null ||
                _radarNearestEmitterAups == null ||
                _radarNearestEmitterAmplitudes == null ||
                _radarNearestEmitterRoots == null)
            {
                return;
            }

            int limit = math.min(
                _radarNearestEmitterDistanceSq.Length,
                math.min(
                    _radarNearestEmitterPositions.Length,
                    math.min(
                        _radarNearestEmitterAups.Length,
                        math.min(_radarNearestEmitterAmplitudes.Length, _radarNearestEmitterRoots.Length))));
            for (int i = 0; i < limit; i++)
            {
                _radarNearestEmitterDistanceSq[i] = float.MaxValue;
                _radarNearestEmitterPositions[i] = default;
                _radarNearestEmitterAups[i] = default;
                _radarNearestEmitterAmplitudes[i] = 0f;
                _radarNearestEmitterRoots[i] = null;
            }
        }

        private void DepositImpactRadarSamples(
            Transform listener,
            in AbsoluteUniversePosition listenerAup,
            float3 listenerRight,
            float3 listenerUp,
            float3 listenerForward,
            float now)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                DepositAcousticRadarSample(
                    listener,
                    in listenerAup,
                    listenerRight,
                    listenerUp,
                    listenerForward,
                    emitter.Position,
                    in emitter.PositionAup,
                    amplitude);
            }
        }

        private void QueueImpactRadarEmitters(in AbsoluteUniversePosition listenerAup, Vector3 listenerWorldPosition, float now)
        {
            for (int i = 0; i < _impactEmitters.Length; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                QueueNearestRadarEmitter(in listenerAup, listenerWorldPosition, emitter.Position, in emitter.PositionAup, amplitude, null);
            }
        }

        private void DepositAcousticRadarSample(
            Transform listener,
            in AbsoluteUniversePosition listenerAup,
            float3 listenerRight,
            float3 listenerUp,
            float3 listenerForward,
            Vector3 sourcePosition,
            in AbsoluteUniversePosition sourceAup,
            float amplitude)
        {
            if (listener == null || !_acousticRadarIntensityBins.IsCreated || !(amplitude > 0f))
                return;

            float3 listenerLocalPosition = ResolveAupLocalDelta(
                in listenerAup,
                in sourceAup,
                listenerRight,
                listenerUp,
                listenerForward);
            int radialIndex = EncodeAcousticRadarDegreeBinFast(listenerLocalPosition);
            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            float rangeSq = math.max(1f, AcousticRadarDistanceRangeMeters * AcousticRadarDistanceRangeMeters);
            float falloff = 1f - math.saturate(distanceSq * math.rcp(rangeSq));
            float intensity = math.saturate(amplitude * falloff);
            _acousticRadarIntensityBins[radialIndex] = math.max(_acousticRadarIntensityBins[radialIndex], intensity);
        }

        private void QueueNearestRadarEmitter(
            in AbsoluteUniversePosition listenerAup,
            Vector3 listenerWorldPosition,
            Vector3 sourcePosition,
            in AbsoluteUniversePosition sourceAup,
            float amplitude,
            Transform sourceRoot)
        {
            if (_radarNearestEmitterDistanceSq == null ||
                _radarNearestEmitterPositions == null ||
                _radarNearestEmitterAups == null ||
                _radarNearestEmitterAmplitudes == null ||
                _radarNearestEmitterRoots == null ||
                !(amplitude > 0f))
            {
                return;
            }

            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            int replaceIndex = -1;
            float farthestDistanceSq = -1f;
            int limit = math.min(
                AcousticRadarNearestEmitterLimit,
                math.min(
                    _radarNearestEmitterDistanceSq.Length,
                    math.min(
                        _radarNearestEmitterPositions.Length,
                        math.min(
                            _radarNearestEmitterAups.Length,
                            math.min(_radarNearestEmitterAmplitudes.Length, _radarNearestEmitterRoots.Length)))));
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
            _radarNearestEmitterAups[replaceIndex] = sourceAup;
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

        private void AccumulateNearestRadarGrid(
            Transform listener,
            Vector3 listenerWorldPosition,
            in AbsoluteUniversePosition listenerAup,
            float3 listenerRight,
            float3 listenerUp,
            float3 listenerForward)
        {
            if (listener == null ||
                !_acousticRadarGrid.IsCreated ||
                _radarNearestEmitterDistanceSq == null ||
                _radarNearestEmitterPositions == null ||
                _radarNearestEmitterAups == null ||
                _radarNearestEmitterAmplitudes == null ||
                _radarNearestEmitterRoots == null)
            {
                return;
            }

            int limit = math.min(
                AcousticRadarNearestEmitterLimit,
                math.min(
                    _radarNearestEmitterDistanceSq.Length,
                    math.min(
                        _radarNearestEmitterPositions.Length,
                        math.min(
                            _radarNearestEmitterAups.Length,
                            math.min(_radarNearestEmitterAmplitudes.Length, _radarNearestEmitterRoots.Length)))));
            bool dirty = false;
            for (int i = 0; i < limit; i++)
            {
                float distanceSq = _radarNearestEmitterDistanceSq[i];
                if (distanceSq == float.MaxValue)
                    continue;

                Vector3 sourcePosition = _radarNearestEmitterPositions[i];
                AbsoluteUniversePosition sourceAup = _radarNearestEmitterAups[i];
                float amplitude = _radarNearestEmitterAmplitudes[i];
                if (!(amplitude > 0f))
                    continue;

                float3 listenerLocalPosition = ResolveAupLocalDelta(
                    in listenerAup,
                    in sourceAup,
                    listenerRight,
                    listenerUp,
                    listenerForward);
                int azimuthIndex = EncodeAcousticRadarGridAzimuthFast(listenerLocalPosition);
                float elevation01 = ResolveElevation01Fast(listenerLocalPosition);
                int elevationIndex = math.clamp(
                    FastFloorToInt(elevation01 * AcousticRadarGridElevationBins),
                    0,
                    AcousticRadarGridElevationBins - 1);
                float transmission = ResolveRadarTransmission(sourcePosition, listenerWorldPosition, _radarNearestEmitterRoots[i]);
                float energy = amplitude * transmission * math.rcp(math.max(distanceSq, 1f));
                if (!(energy > AcousticRadarEnergyEpsilon))
                    continue;

                int cellIndex = elevationIndex * AcousticRadarGridAzimuthBins + azimuthIndex;
                _acousticRadarGrid[cellIndex] += energy;
                dirty = true;
            }

            if (dirty)
                _acousticRadarGridDirty = true;
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

            if (!_acousticRadarGridDirty)
                return;

            int count = math.min(_acousticRadarGrid.Length, _acousticRadarGridUploadScratch.Length);
            for (int i = 0; i < count; i++)
                _acousticRadarGridUploadScratch[i] = _acousticRadarGrid[i];

            _acousticRadarGridBuffer.SetData(_acousticRadarGridUploadScratch, 0, 0, count);
            _acousticRadarGridDirty = false;
        }

        private void RefreshListenerCaveState(Transform listener, Vector3 listenerPosition)
        {
            ResetListenerCaveState();
            if (listener == null)
                return;

            if (_worldCaveDirector == null)
                _worldCaveDirector = WorldCaveDirector.ActiveRuntimeInstance;

            if (_worldCaveDirector == null)
                return;

            _worldCaveDirector.CollectActiveVolumes(_caveVolumeBuffer);
            HectonVoxelVolume sabineCandidateVolume = null;
            Bounds sabineCandidateLocalBounds = default;
            float sabineCandidateInterior01 = -1f;
            int volumeCount = _caveVolumeBuffer.Count;
            for (int volumeIndex = 0; volumeIndex < volumeCount; volumeIndex++)
            {
                HectonVoxelVolume volume = _caveVolumeBuffer[volumeIndex];
                if (volume == null || !volume.isActiveAndEnabled)
                    continue;

                if (!TryResolveCaveInteriorFactor(volume, listenerPosition, out Bounds localBounds, out Matrix4x4 worldToLocal, out float caveInterior01))
                    continue;

                if (_listenerContainingCaveCount < _listenerContainingCaveVolumes.Length)
                {
                    int caveIndex = _listenerContainingCaveCount++;
                    _listenerContainingCaveVolumes[caveIndex] = volume;
                    _listenerContainingCaveLocalBounds[caveIndex] = localBounds;
                    _listenerContainingCaveWorldToLocal[caveIndex] = worldToLocal;
                }
                _listenerCaveInterior01 = math.max(_listenerCaveInterior01, caveInterior01);
                if (caveInterior01 > sabineCandidateInterior01)
                {
                    sabineCandidateVolume = volume;
                    sabineCandidateLocalBounds = localBounds;
                    sabineCandidateInterior01 = caveInterior01;
                }
            }

            if (sabineCandidateVolume != null &&
                TryResolveCaveSabineAcoustics(
                    sabineCandidateVolume,
                    sabineCandidateLocalBounds,
                    sabineCandidateInterior01,
                    out float roomVolumeCubicMeters,
                    out float surfaceAreaSquareMeters,
                    out float rt60Seconds))
            {
                _listenerSabineVolumeCubicMeters = roomVolumeCubicMeters;
                _listenerSabineSurfaceAreaSquareMeters = surfaceAreaSquareMeters;
                _listenerSabineRt60Seconds = rt60Seconds;
            }
        }

        private void ResetListenerCaveState()
        {
            _listenerCaveInterior01 = 0f;
            _listenerSabineRt60Seconds = 0f;
            _listenerSabineVolumeCubicMeters = 0f;
            _listenerSabineSurfaceAreaSquareMeters = 0f;
            for (int i = 0; i < _listenerContainingCaveCount; i++)
            {
                _listenerContainingCaveVolumes[i] = null;
                _listenerContainingCaveLocalBounds[i] = default;
                _listenerContainingCaveWorldToLocal[i] = default;
            }
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

            float deltaTimeInv = math.rcp(deltaTime);
            Vector3 velocity = (listenerAbsolutePosition - _previousListenerAbsolutePosition) * deltaTimeInv;
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

            float blendT = FastDecayBlend(BinauralWaterBlendSharpness, deltaTime);
            _listenerWaterDensityMul = math.lerp(_listenerWaterDensityMul, target, blendT);
        }

        private void UpdateManualDopplerPitch(
            int sourceIndex,
            AudioSource source,
            Vector3 sourceAbsolutePosition,
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
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

            Vector3 sourceVelocity = Vector3.zero;
            if (deltaTime > 0.0001f)
            {
                float deltaTimeInv = math.rcp(deltaTime);
                sourceVelocity = (sourceAbsolutePosition - _previousAbsolutePositions[sourceIndex]) * deltaTimeInv;
            }

            _currentAbsoluteVelocities[sourceIndex] = sourceVelocity;
            _previousAbsolutePositions[sourceIndex] = sourceAbsolutePosition;

            float3 listenerToSourceAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup);
            float targetRatio = 1f;
            float distanceSq = math.lengthsq(listenerToSourceAup);
            if (distanceSq > 0.0001f)
            {
                float3 direction = ResolveDominantAxisDirection(listenerToSourceAup);
                float relativeVelocity = math.dot((float3)(listenerVelocity - sourceVelocity), direction);
                float clampedRelativeVelocity = math.clamp(
                    relativeVelocity,
                    -SoundSpeedWaterMetersPerSecond * 0.9f,
                    SoundSpeedWaterMetersPerSecond * 0.9f);
                targetRatio = math.clamp(
                    1f + (clampedRelativeVelocity * math.rcp(SoundSpeedWaterMetersPerSecond)),
                    ManualDopplerMaximumRatioInv,
                    ManualDopplerMaximumRatio);

                float previousRelativeVelocity = _previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length
                    ? _previousRelativeVelocities[sourceIndex]
                    : 0f;
                float velocityDelta = math.abs(clampedRelativeVelocity - previousRelativeVelocity);
                if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                    _previousRelativeVelocities[sourceIndex] = clampedRelativeVelocity;

                float smoothingDurationSeconds = ManualDopplerSmoothingSamples * ManualDopplerSampleRateHertzInv;
                float followT = velocityDelta > ManualDopplerVelocityJumpThresholdMetersPerSecond
                    ? math.saturate(math.max(deltaTime, 0f) * math.rcp(math.max(smoothingDurationSeconds, 0.0001f)))
                    : FastDecayBlend(ManualDopplerFollowSharpness, deltaTime);
                float smoothedRatio = math.lerp(_smoothedDopplerRatios[sourceIndex], targetRatio, followT);
                _smoothedDopplerRatios[sourceIndex] = smoothedRatio;
                source.pitch = ResolveSourcePitch(sourceIndex, smoothedRatio);
                return;
            }

            _smoothedDopplerRatios[sourceIndex] = 1f;
            if (_previousRelativeVelocities != null && sourceIndex < _previousRelativeVelocities.Length)
                _previousRelativeVelocities[sourceIndex] = 0f;
            source.pitch = ResolveSourcePitch(sourceIndex, 1f);
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

                Bounds localBounds = _listenerContainingCaveLocalBounds[i];
                Vector3 localPosition = _listenerContainingCaveWorldToLocal[i].MultiplyPoint3x4(worldPosition);
                if (localBounds.Contains(localPosition))
                    return true;
            }

            return false;
        }

        private static bool TryResolveCaveSabineAcoustics(
            HectonVoxelVolume volume,
            Bounds localBounds,
            float caveInterior01,
            out float roomVolumeCubicMeters,
            out float surfaceAreaSquareMeters,
            out float rt60Seconds)
        {
            roomVolumeCubicMeters = 0f;
            surfaceAreaSquareMeters = 0f;
            rt60Seconds = 0f;
            if (volume == null)
                return false;

            Vector3 localSize = localBounds.size;
            if (!IsFinite(localSize) || localSize.x <= 0.001f || localSize.y <= 0.001f || localSize.z <= 0.001f)
                return false;

            Transform volumeTransform = volume.transform;
            Vector3 scale = volumeTransform != null ? volumeTransform.lossyScale : Vector3.one;
            Vector3 worldSize = new Vector3(
                math.abs(localSize.x * scale.x),
                math.abs(localSize.y * scale.y),
                math.abs(localSize.z * scale.z));
            if (!IsFinite(worldSize) || worldSize.x <= 0.001f || worldSize.y <= 0.001f || worldSize.z <= 0.001f)
                return false;

            float xy = worldSize.x * worldSize.y;
            float xz = worldSize.x * worldSize.z;
            float yz = worldSize.y * worldSize.z;
            float interior01 = math.saturate(caveInterior01);
            float volumeScale = math.lerp(SabineClosedVolumeScale, SabineOpenVolumeScale, interior01);
            float surfaceScale = math.lerp(SabineClosedSurfaceScale, SabineOpenSurfaceScale, interior01);

            roomVolumeCubicMeters = math.max(
                SabineMinimumRoomVolumeCubicMeters,
                worldSize.x * worldSize.y * worldSize.z * volumeScale);
            surfaceAreaSquareMeters = math.max(
                SabineMinimumSurfaceAreaSquareMeters,
                2f * (xy + xz + yz) * surfaceScale);
            float sabineEquationRt60Seconds = math.clamp(
                SabineEquationConstant * (roomVolumeCubicMeters * math.rcp(surfaceAreaSquareMeters)),
                SabineMinimumRt60Seconds,
                SabineMaximumRt60Seconds);
            float lutRt60Seconds = SampleSabineReverbDecayLut(
                volumeTransform != null ? volumeTransform.position.y : 0f,
                roomVolumeCubicMeters,
                interior01);
            rt60Seconds = math.clamp(
                math.lerp(sabineEquationRt60Seconds, lutRt60Seconds, interior01 * 0.35f),
                SabineMinimumRt60Seconds,
                SabineMaximumRt60Seconds);
            return true;
        }

        private static float SampleSabineReverbDecayLut(
            float worldY,
            float roomVolumeCubicMeters,
            float caveInterior01)
        {
            float depth01 = math.saturate((-worldY) * math.rcp(SabineReverbDepthReferenceMeters));
            float volume01 = math.saturate(roomVolumeCubicMeters * math.rcp(SabineReverbModuleVolumeReferenceCubicMeters));
            float coordinate = math.saturate((depth01 * 0.45f) + (volume01 * 0.45f) + (math.saturate(caveInterior01) * 0.1f));
            float scaledIndex = coordinate * SabineReverbDecayLutMaxIndex;
            int lowIndex = math.clamp((int)scaledIndex, 0, SabineReverbDecayLutMaxIndex);
            int highIndex = math.min(lowIndex + 1, SabineReverbDecayLutMaxIndex);
            return math.lerp(
                ResolveSabineReverbDecayLutValue(lowIndex),
                ResolveSabineReverbDecayLutValue(highIndex),
                scaledIndex - lowIndex);
        }

        private static float ResolveSabineReverbDecayLutValue(int index)
        {
            float t = math.saturate(index * math.rcp((float)SabineReverbDecayLutMaxIndex));
            float depthBoost = 1f + t * t * 1.65f;
            float volumeBoost = 0.35f + t * 0.75f;
            return math.clamp(
                SabineMinimumRt60Seconds + depthBoost * volumeBoost,
                SabineMinimumRt60Seconds,
                SabineMaximumRt60Seconds);
        }

        private static bool TryResolveCaveInteriorFactor(
            HectonVoxelVolume volume,
            Vector3 viewerPositionWS,
            out Bounds localBounds,
            out Matrix4x4 worldToLocal,
            out float caveInterior01)
        {
            localBounds = default;
            worldToLocal = default;
            caveInterior01 = 0f;
            if (volume == null || !CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(volume, volume.preset, out localBounds))
                return false;

            worldToLocal = volume.transform.worldToLocalMatrix;
            Vector3 localViewerPosition = worldToLocal.MultiplyPoint3x4(viewerPositionWS);
            if (!localBounds.Contains(localViewerPosition))
                return false;

            Vector3 min = localBounds.min;
            Vector3 max = localBounds.max;
            float distanceToWall = math.min(
                math.min(localViewerPosition.x - min.x, max.x - localViewerPosition.x),
                math.min(
                    math.min(localViewerPosition.y - min.y, max.y - localViewerPosition.y),
                    math.min(localViewerPosition.z - min.z, max.z - localViewerPosition.z)));
            caveInterior01 = math.saturate(distanceToWall * CaveInteriorReferenceDistanceMetersInv);
            return true;
        }

        private static float ResolveImpactEmitterAmplitude(ImpactEmitterSample emitter, float now)
        {
            if (!(emitter.ExpireAt > now) || !(emitter.Amplitude > ImpactEmitterMinimumAmplitude))
                return 0f;

            float lifetime = math.max(0.001f, emitter.ExpireAt - emitter.SpawnAt);
            float fade = math.saturate((emitter.ExpireAt - now) * math.rcp(lifetime));
            return emitter.Amplitude * fade;
        }

        private static float3 ResolveAupDelta(in AbsoluteUniversePosition listenerAup, in AbsoluteUniversePosition sourceAup)
        {
            return AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup);
        }

        private static float3 ResolveAupLocalDelta(
            in AbsoluteUniversePosition listenerAup,
            in AbsoluteUniversePosition sourceAup,
            float3 listenerRight,
            float3 listenerUp,
            float3 listenerForward)
        {
            float3 worldDelta = ResolveAupDelta(in listenerAup, in sourceAup);
            return new float3(
                math.dot(worldDelta, listenerRight),
                math.dot(worldDelta, listenerUp),
                math.dot(worldDelta, listenerForward));
        }

        private static float ApproximateMagnitude3D(float3 value)
        {
            float3 absoluteValue = math.abs(value);
            float maxAxis = math.max(absoluteValue.x, math.max(absoluteValue.y, absoluteValue.z));
            float minAxis = math.min(absoluteValue.x, math.min(absoluteValue.y, absoluteValue.z));
            float midAxis = absoluteValue.x + absoluteValue.y + absoluteValue.z - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        private static int FastFloorToInt(float value)
        {
            int truncated = (int)value;
            return truncated > value ? truncated - 1 : truncated;
        }

        private static long FastFloorToLong(float value)
        {
            long truncated = (long)value;
            return truncated > value ? truncated - 1L : truncated;
        }

        private static int EncodeAcousticRadarDegreeBinFast(float3 listenerLocalPosition)
        {
            float x = listenerLocalPosition.x;
            float z = listenerLocalPosition.z;
            float absX = math.abs(x);
            float absZ = math.abs(z);
            float blend = absX * math.rcp(math.max(0.0001f, absX + absZ));
            float quarterDegrees = blend * 90f;
            float degrees;
            if (z >= 0f)
                degrees = x >= 0f ? quarterDegrees : 360f - quarterDegrees;
            else
                degrees = x >= 0f ? 180f - quarterDegrees : 180f + quarterDegrees;

            return math.clamp((int)degrees, 0, AcousticRadarBinCount - 1);
        }

        private static int EncodeAcousticRadarGridAzimuthFast(float3 listenerLocalPosition)
        {
            int degreeBin = EncodeAcousticRadarDegreeBinFast(listenerLocalPosition);
            int azimuthIndex = (int)((degreeBin * AcousticRadarGridAzimuthBins) * AcousticRadarBinCountInv);
            return math.clamp(azimuthIndex, 0, AcousticRadarGridAzimuthBins - 1);
        }

        private static float ResolveElevation01Fast(float3 listenerLocalPosition)
        {
            float distance = ApproximateMagnitude3D(listenerLocalPosition);
            if (distance <= 0.0001f)
                return 0.5f;

            return math.saturate((listenerLocalPosition.y * math.rcp(distance) + 1f) * 0.5f);
        }

        private static float ClampAupDistanceSqToFloat(double distanceSqr)
        {
            return distanceSqr >= float.MaxValue ? float.MaxValue : (float)math.max(0d, distanceSqr);
        }

        private static float FastDecayBlend(float sharpness, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;

            float x = math.max(0f, sharpness) * deltaTime;
            if (x >= 3.5f)
                return 1f;

            return math.saturate((12f * x) * math.rcp(12f + (6f * x) + (x * x)));
        }

        private static float ResolveCinematicPitchRatioFromCents(float cents)
        {
            return math.clamp(1f + (cents * 0.0005776f), 0.5f, 2f);
        }

        private int Acquire2DSourceIndex()
        {
            if (_pool2D == null || _pool2DSize <= 0)
                return -1;

            int quietestIndex = -1;
            float quietestVolume = float.MaxValue;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < _pool2DSize; i++)
            {
                AudioSource source = _pool2D[i];
                if (source == null)
                    continue;

                if (!source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                {
                    return i;
                }

                float candidateVolume = math.max(0f, source.volume);
                float candidateStartTime = _startTimes2D[i];
                if (candidateVolume < quietestVolume ||
                    (candidateVolume <= quietestVolume && candidateStartTime < oldestTime))
                {
                    quietestVolume = candidateVolume;
                    oldestTime = candidateStartTime;
                    quietestIndex = i;
                }
            }

            if (quietestIndex < 0)
                return -1;

            _pool2D[quietestIndex].Stop();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShouldEmitEditorThrottledLog(ref _nextHelmetPoolFullEditorLogTime, PoolFullEditorLogIntervalSeconds))
            {
                Debug.Log("[SpatialAudioManager] Helmet/UI pool full. Evicting quietest source.", this);
            }
#endif

            return quietestIndex;
        }

        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
        //  EDITOR VALIDATION
        // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool ShouldEmitEditorThrottledLog(ref float nextLogTime, float intervalSeconds)
        {
            float now = Time.unscaledTime;
            if (now < nextLogTime)
                return false;

            nextLogTime = now + math.max(0.1f, intervalSeconds);
            return true;
        }
#endif

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
                _worldSourceRoots[index] = current.root;
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

            RefreshMixerParameterAvailability();
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
            : this(
                captionText,
                worldPosition,
                AbsoluteUniversePosition.FromRuntimePosition(worldPosition),
                true,
                durationSeconds,
                intensity)
        {
        }

        public AudioCaptionRequest(string captionText, Vector3 worldPosition, in AbsoluteUniversePosition worldAup, float durationSeconds, float intensity)
            : this(captionText, worldPosition, worldAup, true, durationSeconds, intensity)
        {
        }

        private AudioCaptionRequest(
            string captionText,
            Vector3 worldPosition,
            AbsoluteUniversePosition worldAup,
            bool hasWorldAup,
            float durationSeconds,
            float intensity)
        {
            CaptionText = captionText;
            WorldPosition = worldPosition;
            WorldAup = worldAup;
            _hasWorldAup = hasWorldAup ? (byte)1 : (byte)0;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
        }

        /// <summary>Cached/prelocalized caption text shown by the HUD.</summary>
        public string CaptionText { get; }

        /// <summary>World-space origin used to position the caption around the reticle.</summary>
        public Vector3 WorldPosition { get; }

        /// <summary>Absolute caption origin, stable across floating-origin shifts.</summary>
        public AbsoluteUniversePosition WorldAup { get; }

        /// <summary>True when the caption request carries a stable absolute origin.</summary>
        public bool HasWorldAup => _hasWorldAup != 0;

        /// <summary>Visible duration in seconds.</summary>
        public float DurationSeconds { get; }

        /// <summary>Normalized caption strength in the 0..1 range.</summary>
        public float Intensity { get; }

        private readonly byte _hasWorldAup;

        /// <summary>Returns the stable absolute caption origin, falling back only for legacy/default payloads.</summary>
        public AbsoluteUniversePosition ResolveWorldAup()
        {
            return HasWorldAup
                ? WorldAup
                : AbsoluteUniversePosition.FromRuntimePosition(WorldPosition);
        }
    }

    /// <summary>
    /// Unmanaged caption payload carried by the deferred audio-caption lane.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AudioCaptionPayload
    {
        public Vector3 WorldPosition;
        public AbsoluteUniversePosition WorldAup;
        public float DurationSeconds;
        public float Intensity;
        public uint CaptionHashId;
        public int ReferenceSlot;
        public ushort EventType;
        public ushort Reserved;
        public byte HasWorldAup;
        public byte ReservedByte0;
        public ushort ReservedShort0;
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

            if (!Application.isPlaying)
                return;

            EnsureInitialized();
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>Unregisters one audio caption listener.</summary>
        public static void Unregister(IAudioCaptionEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
            if (_listeners.Count <= 0)
                DropQueuedCaptionPayloads();
        }

        /// <summary>Flushes queued audio captions to registered UI listeners.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DropQueuedCaptionPayloads();
                return;
            }

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
            if (!Application.isPlaying || _listeners.Count <= 0)
                return;

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
                WorldAup = request.WorldAup,
                DurationSeconds = request.DurationSeconds,
                Intensity = request.Intensity,
                CaptionHashId = 0u,
                ReferenceSlot = referenceSlot,
                EventType = CaptionRequestedEventType,
                Reserved = 0,
                HasWorldAup = request.HasWorldAup ? (byte)1 : (byte)0,
                ReservedByte0 = 0,
                ReservedShort0 = 0
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
                PrewarmCaptionQueue(ref _pendingEvents);
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
                PrewarmCaptionQueue(ref _nextFrameEvents);
            }
        }

        private static void PrewarmCaptionQueue(ref NativeQueue<AudioCaptionPayload> queue)
        {
            if (!queue.IsCreated)
                return;

            for (int i = 0; i < PendingEventCapacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
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

        private static void DropQueuedCaptionPayloads()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
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

            AbsoluteUniversePosition worldAup = payload.WorldAup;
            AudioCaptionRequest request = payload.HasWorldAup != 0
                ? new AudioCaptionRequest(
                    captionText,
                    payload.WorldPosition,
                    in worldAup,
                    payload.DurationSeconds,
                    payload.Intensity)
                : new AudioCaptionRequest(
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
