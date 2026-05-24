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
//   Core audio registry PlayAtPoint(clip, position, volume, pitch)
//   Core audio registry PlayAtPoint(clip, position, volume, pitch, mixerGroup)
//   Core audio registry PlayStatic2D(clip, volume)
//   Core audio registry PlayStatic2D(clip, volume, mixerGroup)
//   Core audio registry StopAll()
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
using Hecton8.AI.Sensory;
using Hecton8.Atmosphere;
using Hecton8.Audio.Propagation;
using Hecton8.Audio.Virtualization;
using Hecton8.Caves;
using Hecton8.Construction;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay;
using Hecton8.Networking;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using CoreAudioEvent = Hecton8.Core.AudioEvent;
using AcousticAup = Hecton8.Core.Contracts.AcousticAup;
using AcousticOcclusionTelemetryEntry = Hecton8.Audio.Virtualization.AcousticTelemetryEntry;
using AcousticPortalTelemetryEntry = Hecton8.Audio.Propagation.AcousticTelemetryEntry;
using VirtualVoiceSdfSampler = Hecton8.Audio.Virtualization.MockSDFSampler;

namespace Hecton8.Audio
{
    public enum AudioResidencyDomain : byte
    {
        Music = 0,
        Player = 1,
        Creatures = 2,
        Environment = 3,
        Interface = 4
    }

    public static class AudioResidencyDomainUtility
    {
        public const int DomainCount = 5;

        public static bool IsValid(AudioResidencyDomain domain)
        {
            return domain >= AudioResidencyDomain.Music && domain <= AudioResidencyDomain.Interface;
        }

        public static ReadOnlySpan<char> GetLabel(AudioResidencyDomain domain)
        {
            switch (domain)
            {
                case AudioResidencyDomain.Music:
                    return "Music".AsSpan();
                case AudioResidencyDomain.Player:
                    return "Player".AsSpan();
                case AudioResidencyDomain.Creatures:
                    return "Creatures".AsSpan();
                case AudioResidencyDomain.Environment:
                    return "Environment".AsSpan();
                case AudioResidencyDomain.Interface:
                    return "Interface".AsSpan();
                default:
                    return "Unknown".AsSpan();
            }
        }
    }

    public static class AudioResidencyCache
    {
        private const int MaxEntries = 64;
        private const long RuntimeDecodedBudgetBytes = 16L * 1024L * 1024L;

        private struct Entry
        {
            public AudioClip Clip;
            public int ClipId;
            public AudioResidencyDomain Domain;
            public int LastUseFrame;
            public long EstimatedBytes;
            public bool Resident;
        }

        private static Entry[] s_entries;
        private static long s_residentBytes;
        private static int s_residentCount;

        public static long CurrentResidentBytes => s_residentBytes;
        public static int ResidentClipCount => s_residentCount;

        public static void TouchClip(AudioClip clip, AudioResidencyDomain domain, bool decodeNow)
        {
            if (clip == null || !AudioResidencyDomainUtility.IsValid(domain))
                return;

            EnsureInitialized();
            int slot = FindOrAllocateSlot(clip, domain);
            if (slot < 0)
                return;

            Entry entry = s_entries[slot];
            entry.LastUseFrame = Time.frameCount;
            entry.Domain = domain;

            if (decodeNow && ShouldLoadClip(clip))
            {
                long bytes = EstimateDecodedBytes(clip);
                EnsureBudgetFor(bytes, slot);
                AudioDataLoadState previousState = clip.loadState;
                if (previousState == AudioDataLoadState.Loaded || clip.LoadAudioData())
                {
                    if (!entry.Resident)
                    {
                        entry.Resident = true;
                        s_residentCount++;
                    }

                    if (entry.EstimatedBytes <= 0L)
                    {
                        entry.EstimatedBytes = bytes;
                        s_residentBytes += bytes;
                    }
                }
            }

            s_entries[slot] = entry;
        }

        public static void PrewarmAudioSource(AudioSource source, AudioResidencyDomain domain)
        {
            if (source == null)
                return;

            TouchClip(source.clip, domain, true);
        }

        public static void ReleaseAudioSource(AudioSource source)
        {
            if (source == null)
                return;

            ReleaseClip(source.clip);
        }

        public static void ReleaseClip(AudioClip clip)
        {
            if (clip == null || s_entries == null)
                return;

            int slot = FindSlot(clip);
            if (slot < 0)
            {
                UnloadClipData(clip);
                return;
            }

            ReleaseSlot(slot);
        }

        public static void EvictDomain(AudioResidencyDomain domain)
        {
            if (s_entries == null || !AudioResidencyDomainUtility.IsValid(domain))
                return;

            for (int i = 0; i < s_entries.Length; i++)
            {
                if (s_entries[i].Clip != null && s_entries[i].Domain == domain)
                    ReleaseSlot(i);
            }
        }

        private static void EnsureInitialized()
        {
            if (s_entries != null)
                return;

            s_entries = new Entry[MaxEntries]; // COLD ALLOC: fixed audio residency LRU cache - owner: AudioResidencyCache
        }

        private static bool ShouldLoadClip(AudioClip clip)
        {
            if (clip == null || clip.loadType == AudioClipLoadType.Streaming)
                return false;

            return clip.loadState != AudioDataLoadState.Failed;
        }

        private static int FindOrAllocateSlot(AudioClip clip, AudioResidencyDomain domain)
        {
            int existing = FindSlot(clip);
            if (existing >= 0)
                return existing;

            int free = -1;
            int oldest = -1;
            int oldestFrame = int.MaxValue;
            for (int i = 0; i < s_entries.Length; i++)
            {
                if (s_entries[i].Clip == null)
                {
                    free = i;
                    break;
                }

                if (s_entries[i].LastUseFrame < oldestFrame)
                {
                    oldestFrame = s_entries[i].LastUseFrame;
                    oldest = i;
                }
            }

            int slot = free >= 0 ? free : oldest;
            if (slot < 0)
                return -1;

            if (s_entries[slot].Clip != null)
                ReleaseSlot(slot);

            s_entries[slot] = new Entry
            {
                Clip = clip,
                ClipId = clip.GetInstanceID(),
                Domain = domain,
                LastUseFrame = Time.frameCount,
                EstimatedBytes = 0L,
                Resident = false
            };
            return slot;
        }

        private static int FindSlot(AudioClip clip)
        {
            if (clip == null || s_entries == null)
                return -1;

            int clipId = clip.GetInstanceID();
            for (int i = 0; i < s_entries.Length; i++)
            {
                if (s_entries[i].ClipId == clipId && s_entries[i].Clip == clip)
                    return i;
            }

            return -1;
        }

        private static void EnsureBudgetFor(long requestedBytes, int protectedSlot)
        {
            if (requestedBytes <= 0L)
                return;

            while (s_residentBytes + requestedBytes > RuntimeDecodedBudgetBytes)
            {
                int victim = FindOldestResidentSlot(protectedSlot);
                if (victim < 0)
                    return;

                ReleaseSlot(victim);
            }
        }

        private static int FindOldestResidentSlot(int protectedSlot)
        {
            int victim = -1;
            int oldestFrame = int.MaxValue;
            for (int i = 0; i < s_entries.Length; i++)
            {
                Entry entry = s_entries[i];
                if (i == protectedSlot || entry.Clip == null || !entry.Resident)
                    continue;

                if (entry.LastUseFrame < oldestFrame)
                {
                    oldestFrame = entry.LastUseFrame;
                    victim = i;
                }
            }

            return victim;
        }

        private static void ReleaseSlot(int slot)
        {
            if (s_entries == null || slot < 0 || slot >= s_entries.Length)
                return;

            Entry entry = s_entries[slot];
            if (entry.Clip != null)
            {
                UnloadClipData(entry.Clip);

                if (entry.Resident)
                {
                    s_residentBytes -= entry.EstimatedBytes;
                    if (s_residentBytes < 0L)
                        s_residentBytes = 0L;

                    s_residentCount = math.max(0, s_residentCount - 1);
                }
            }

            s_entries[slot] = default;
        }

        private static void UnloadClipData(AudioClip clip)
        {
            if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                clip.UnloadAudioData();
        }

        private static long EstimateDecodedBytes(AudioClip clip)
        {
            if (clip == null)
                return 0L;

            return Math.Max(0L, (long)clip.samples * Math.Max(1, clip.channels) * 2L);
        }
    }

    /// <summary>
    /// Ð¦ÐµÐ½Ñ‚Ñ€Ð°Ð»ÑŒÐ½Ñ‹Ð¹ Ð¼ÐµÐ½ÐµÐ´Ð¶ÐµÑ€ Ð¿Ñ€Ð¾ÑÑ‚Ñ€Ð°Ð½ÑÑ‚Ð²ÐµÐ½Ð½Ð¾Ð³Ð¾ Ð·Ð²ÑƒÐºÐ° Ñ Ð¿ÑƒÐ»Ð¸Ð½Ð³Ð¾Ð¼.
    /// Runtime audio service accessed through the core audio registry.
    /// Zero-GC Ð² hot path. Ð–Ñ‘ÑÑ‚ÐºÐ¸Ð¹ Ð»Ð¸Ð¼Ð¸Ñ‚ Ð¾Ð´Ð½Ð¾Ð²Ñ€ÐµÐ¼ÐµÐ½Ð½Ñ‹Ñ… Ð¸ÑÑ‚Ð¾Ñ‡Ð½Ð¸ÐºÐ¾Ð².
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour, IAudioService, IAudioResidencyService, ISceneTransitionAudioBridge, IAudioVirtualizationService, IUpdatable, IFastTickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IPhysicsImpactEventListener, IRepairDroneTorchAcousticListener, IFatalPressureImplosionEventListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IServiceHeartbeat, IServiceShutdown
    {
        private const float SoundSpeedWaterMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        private const float MassiveDistanceFixedAudioDelayMeters = 740f;
        private const float MassiveDistanceFixedAudioDelaySeconds = 0.5f;
        private const float ThermalShimmerMaximumPitchRatio = 0.018f;
        private const float TimeDilationAudioMinimumPitchRatio = 0.72f;
        private const int LowTierAmbientOutputSampleRate = 22050;
        private const float BrownoutAudioPitchMinimumRatio = 0.58f;
        private const float BrownoutAudioPitchSharpness = 7f;
        private const float BrownoutAudioReleasePerSecond = 0.45f;
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
        private const byte AudioClipRouteFlagLeviathanRoar = 1 << 2;
        private const byte AudioClipRouteFlagBubble = 1 << 3;
        private const byte AudioVoiceCategoryNone = 0;
        private const byte AudioVoiceCategoryLeviathanRoar = 1;
        private const byte AudioVoiceCategoryBubble = 2;
        private const int MaxLeviathanRoarVoices = 3;
        private const int MaxBubbleVoices = 10;
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
        private const int SpatialAudioPolicyUninitializedFrame = -4096;
        private const int SpatialAudioRegistryRetryFrames = 30;

        internal static SpatialAudioManager ActiveRuntimeInstance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }
        private const int MaxDelayedAudioEvents = 16;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
        private const Allocator DataVaultExemptOwnerIndexAllocator = Allocator.Persistent;
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
        private const int MaxVirtualVoiceCapacity = VirtualVoiceUtility.MaxVirtualVoiceCount;
        private const int MaxVirtualPhysicalVoices = VirtualVoiceUtility.MaxPhysicalVoiceCount;
        private const int LowTierVirtualPhysicalVoices = VirtualVoiceUtility.LowTierPhysicalVoiceCount;
        private const int VirtualVoiceTierHysteresisSlowTicks = 25;
        private const int VirtualVoiceBlackBoxFrameCount = 300;
        private const float VirtualVoiceStealFadeSeconds = 0.01f;
        private const string VirtualVoiceDumpRelativePath = "Docs/AgentLogs/Dump_ACOUSTIC_SURGEON.bin";
        private const BufferID SpatialAudioVirtualVoiceTuningBufferId = (BufferID)70015;
        private const BufferID SpatialAudioVirtualVoiceWritePoolBufferId = (BufferID)70016;
        private const BufferID SpatialAudioVirtualVoiceSortPoolBufferId = (BufferID)70017;
        private const BufferID SpatialAudioVirtualVoiceDtoPoolBufferId = (BufferID)70018;
        private const BufferID SpatialAudioVirtualVoiceSortKeyPoolBufferId = (BufferID)70019;
        private const BufferID SpatialAudioAcousticSourceWritePoolBufferId = (BufferID)70020;
        private const BufferID SpatialAudioAcousticSourceSortPoolBufferId = (BufferID)70021;
        private const BufferID SpatialAudioAcousticPreviousAupWritePoolBufferId = (BufferID)70022;
        private const BufferID SpatialAudioAcousticPreviousAupSortPoolBufferId = (BufferID)70023;
        private const BufferID SpatialAudioAcousticDspOutputPoolBufferId = (BufferID)70024;
        private const BufferID SpatialAudioAcousticMaterialRowsBufferId = (BufferID)70025;
        private const BufferID SpatialAudioAcousticSelectedSourcePoolBufferId = (BufferID)70026;
        private const BufferID SpatialAudioAcousticSelectedPreviousAupPoolBufferId = (BufferID)70027;
        private const int AcousticSdfDefaultWidth = 64;
        private const int AcousticSdfDefaultHeight = 40;
        private const int AcousticSdfDefaultDepth = 64;
        private const int AcousticSdfDefaultVoxelCount = AcousticSdfDefaultWidth * AcousticSdfDefaultHeight * AcousticSdfDefaultDepth;
        private const float AcousticSdfDefaultCellMeters = 2f;
        private const float AcousticSdfDefaultRangeMeters = 24f;
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
        private const int AcousticLutVolumeCount = 256;
        private const int AcousticLutAbsorptionCount = 256;
        private const int AcousticLutRecordBytes = 8;
        private const int AcousticLutExpectedBytes = AcousticLutVolumeCount * AcousticLutAbsorptionCount * AcousticLutRecordBytes;
        private const string AcousticLutRelativePath = "Data/Audio/Acoustic_LUT.bin";
#if DEVELOPMENT_BUILD
        private const int AudioRamDebugTextCapacity = 48;
        private const int AudioRamDebugCanvasSortingOrder = 32760;
#endif
        private static readonly uint _virtualVoiceTelemetryHash = unchecked((uint)LocHash.Compute("Audio.VirtualVoiceTelemetry"));
        private static readonly uint _virtualVoiceActiveHash = unchecked((uint)LocHash.Compute("Audio.VirtualVoice.Active"));
        private static readonly uint _virtualVoiceCulledHash = unchecked((uint)LocHash.Compute("Audio.VirtualVoice.Culled"));

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

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct ActiveEmitterSample
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)]
            public Vector3 Position;
            [FieldOffset(60)]
            public float Amplitude;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct ActiveImpactEmitterSample
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)]
            public float Amplitude;
            [FieldOffset(52)]
            private uint _pad0;
            [FieldOffset(56)]
            private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        internal struct BinauralEmitterTelemetry
        {
            [FieldOffset(0)]
            public Vector3 Position;
            [FieldOffset(12)]
            public float DistanceMeters;
            [FieldOffset(16)]
            public float AzimuthRadians;
            [FieldOffset(20)]
            public float RightDot;
            [FieldOffset(24)]
            public float ItdSeconds;
            [FieldOffset(28)]
            public float ShadowAmount01;
            [FieldOffset(32)]
            public float ShadowCutoffHertz;
            [FieldOffset(36)]
            public float Energy;
            [FieldOffset(40)]
            public float WaterDensityMul;
            [FieldOffset(44)]
            public int Valid;
            [FieldOffset(48)]
            private ulong _pad0;
            [FieldOffset(56)]
            private ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct DelayedAudioEvent
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition Aup;
            [FieldOffset(48)]
            public float EventTimeSeconds;
            [FieldOffset(52)]
            public float DelaySeconds;
            [FieldOffset(56)]
            public float Volume;
            [FieldOffset(60)]
            public float Pitch;
            [FieldOffset(64)]
            public float AcousticTransmission01;
            [FieldOffset(68)]
            public float LowPassCutoffHz;
            [FieldOffset(72)]
            public float ThermalShimmer01;
            [FieldOffset(76)]
            public float TraumaRangeMeters;
            [FieldOffset(80)]
            public float TraumaImpulse;
            [FieldOffset(84)]
            public float TraumaWeight;
            [FieldOffset(88)]
            public DelayedAudioEventKind Kind;
            [FieldOffset(89)]
            private byte _pad0;
            [FieldOffset(90)]
            private ushort _pad1;
            [FieldOffset(92)]
            private uint _pad2;
            [FieldOffset(96)]
            private ulong _pad3;
            [FieldOffset(104)]
            private ulong _pad4;
            [FieldOffset(112)]
            private ulong _pad5;
            [FieldOffset(120)]
            private ulong _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 256)]
        private struct AcousticPortalCacheEntry
        {
            [FieldOffset(0)]
            public AcousticAup SourceAup;
            [FieldOffset(40)]
            public AcousticAup ListenerAup;
            [FieldOffset(80)]
            public AcousticPathResult Result;
            [FieldOffset(184)]
            public int Key;
            [FieldOffset(188)]
            public int Frame;
            [FieldOffset(192)]
            public byte Valid;
            [FieldOffset(193)]
            private byte _reserved0;
            [FieldOffset(194)]
            private ushort _reserved1;
            [FieldOffset(196)]
            private uint _reserved2;
            [FieldOffset(200)]
            private ulong _pad0;
            [FieldOffset(208)]
            private ulong _pad1;
            [FieldOffset(216)]
            private ulong _pad2;
            [FieldOffset(224)]
            private ulong _pad3;
            [FieldOffset(232)]
            private ulong _pad4;
            [FieldOffset(240)]
            private ulong _pad5;
            [FieldOffset(248)]
            private ulong _pad6;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct ImpactEmitterSample
        {
            [FieldOffset(0)]
            public AbsoluteUniversePosition PositionAup;
            [FieldOffset(48)]
            public Vector3 Position;
            [FieldOffset(60)]
            public float Amplitude;
            [FieldOffset(64)]
            public float SpawnAt;
            [FieldOffset(68)]
            public float ExpireAt;
            [FieldOffset(72)]
            private ulong _pad0;
            [FieldOffset(80)]
            private ulong _pad1;
            [FieldOffset(88)]
            private ulong _pad2;
            [FieldOffset(96)]
            private ulong _pad3;
            [FieldOffset(104)]
            private ulong _pad4;
            [FieldOffset(112)]
            private ulong _pad5;
            [FieldOffset(120)]
            private ulong _pad6;
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

        [Tooltip("Exposed mixer parameter for brownout-driven global pitch multiplier.")]
        [SerializeField] private string _brownoutPitchMultiplierParameter = "BrownoutPitchMultiplier";

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
        private byte[] _worldSourceVoiceCategories;
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
        private bool _registeredFastTickable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredOriginShiftListener;
        private bool _acousticOcclusionRuntimeAcquired;
        private IFoveatedSimulationDirector _foveatedSimulationDirector;
        private IDataVault _dataVault;
        private Transform _listenerTransform;
        private Vector3 _previousListenerAbsolutePosition;
        private bool _hasPreviousListenerAbsolutePosition;
        private BinauralEmitterTelemetry _dominantBinauralEmitter;
        private VaultGenerationHandle<float> _acousticRadarIntensityBinsHandle;
        private VaultGenerationHandle<float> _acousticRadarGridHandle;
        private NativeArray<float> _acousticRadarIntensityBins; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<float> _acousticRadarGrid; // Vault alias; GlobalDataVault owns backing memory.
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
        private float _acousticLutFallbackRt60Seconds = VirtualVoiceUtility.SabineMinimumRt60Seconds;
        private float _acousticLutFallbackDamping01 = 1f;
        private bool _acousticLutFallbackLoaded;
#if DEVELOPMENT_BUILD
        private TMPro.TextMeshProUGUI _audioRamDebugLabel;
        private char[] _audioRamDebugTextBuffer;
        private int _audioRamDebugLastResidentKilobytes = -1;
        private int _audioRamDebugLastClipCount = -1;
#endif
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
        private bool _hasBrownoutPitchMultiplierParameter;
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
        private float _brownoutAudioPitchRatio = 1f;
        private float _brownoutTarget01;
        private float _lastAppliedBrownoutPitchRatio = 1f;
        private int _lastCreatureFrozenBankEvictFrame = -4096;
        private int _lastAcousticImpulseSignalFrame = -4096;
        private int _lastAudioOutputSampleRate = -1;
        private float _listenerWaterDensityMul;
        private float _radarDecayAccumulator;
        private HectonPlayerMovement _listenerPlayerMovement;
        private int _delayedAudioIngressCount;
        private NativeQueue<DelayedAudioEvent> _delayedAudioIngress;
        private NativeList<DelayedAudioEvent> _pendingDelayedAudioEvents;
        private int _audioEventQueueCount;
        private int _audioEventQueueDroppedCount;
        private NativeQueue<CoreAudioEvent> _audioEventQueue;
        private NativeParallelHashMap<uint, int> _audioClipHashToTableIndex;
        private VaultGenerationHandle<VirtualVoice> _virtualVoiceWritePoolHandle;
        private VaultGenerationHandle<VirtualVoice> _virtualVoiceSortPoolHandle;
        private VaultGenerationHandle<VirtualVoiceDTO> _virtualVoiceDtoPoolHandle;
        private VaultGenerationHandle<VirtualVoiceSortKey> _virtualVoiceSortKeyPoolHandle;
        private VaultGenerationHandle<VirtualVoiceSelection> _virtualVoiceSelectionsHandle;
        private VaultGenerationHandle<VirtualVoiceStatistics> _virtualVoiceStatisticsHandle;
        private VaultGenerationHandle<AcousticOcclusionTelemetryEntry> _virtualVoiceBlackBoxHandle;
        private VaultGenerationHandle<VirtualVoiceTuningSnapshot> _virtualVoiceTuningHandle;
        private VaultGenerationHandle<AcousticSourceDTO> _acousticSourceWritePoolHandle;
        private VaultGenerationHandle<AcousticSourceDTO> _acousticSourceSortPoolHandle;
        private VaultGenerationHandle<double3> _acousticPreviousAupWritePoolHandle;
        private VaultGenerationHandle<double3> _acousticPreviousAupSortPoolHandle;
        private VaultGenerationHandle<AcousticDspOutputDTO> _acousticDspOutputPoolHandle;
        private VaultGenerationHandle<AcousticMaterialCoefficientDTO> _acousticMaterialRowsHandle;
        private VaultGenerationHandle<AcousticSourceDTO> _acousticSelectedSourcePoolHandle;
        private VaultGenerationHandle<double3> _acousticSelectedPreviousAupPoolHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _virtualVoiceScalabilityStateHandle;
        private VaultGenerationHandle<RollbackAudioSuppressionDTO> _virtualVoiceRollbackAudioSuppressionHandle;
        private NativeArray<VirtualVoice> _virtualVoiceWritePool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoice> _virtualVoiceSortPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoiceDTO> _virtualVoiceDtoPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoiceSortKey> _virtualVoiceSortKeyPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoiceSelection> _virtualVoiceSelections; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoiceStatistics> _virtualVoiceStatistics; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticOcclusionTelemetryEntry> _virtualVoiceBlackBox; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<VirtualVoiceTuningSnapshot> _virtualVoiceTuningVault; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticSourceDTO> _acousticSourceWritePool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticSourceDTO> _acousticSourceSortPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<double3> _acousticPreviousAupWritePool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<double3> _acousticPreviousAupSortPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticDspOutputDTO> _acousticDspOutputPool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticMaterialCoefficientDTO> _acousticMaterialRows; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticSourceDTO> _acousticSelectedSourcePool; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<double3> _acousticSelectedPreviousAupPool; // Vault alias; GlobalDataVault owns backing memory.
        private VaultGenerationHandle<byte> _acousticVoxelSdfTexture3DHandle;
        private NativeArray<byte> _acousticVoxelSdfTexture3D; // External read-only Vault alias; voxel owner owns backing memory.
        private NativeArray<ScalabilityStateDTO> _virtualVoiceScalabilityState; // Vault alias; Homeostasis owns backing memory.
        private NativeArray<RollbackAudioSuppressionDTO> _virtualVoiceRollbackAudioSuppression; // Vault alias; rollback runtime owns backing memory.
        private JobHandle _virtualVoiceSortHandle;
        private JobHandle _acousticOcclusionHandle;
        private VirtualVoiceStatistics _lastVirtualVoiceStatistics;
        private VirtualVoiceTuningSnapshot _virtualVoiceTuning = VirtualVoiceTuningSnapshot.CreateDefault();
        private int _virtualVoiceWriteCount;
        private int _virtualVoiceSortCount;
        private int _virtualVoiceDtoCount;
        private AcousticAup _virtualListenerAup;
        private Vector3 _virtualPreviousListenerAbsolutePosition;
        private float3 _virtualListenerVelocityMetersPerSecond;
        private VirtualVoiceSdfSampler _virtualVoiceSdfSampler;
        private float3 _virtualListenerRight = new float3(1f, 0f, 0f);
        private float3 _virtualListenerSdfProbePosition;
        private float _virtualListenerDepthMeters;
        private float _virtualSimulationTickDeltaSeconds = 1f / 60f;
        private float _virtualVoiceSortStartRealtimeSeconds;
        private long _acousticOcclusionStartTicks;
        private float _lastAcousticOcclusionTimeMs;
        private int _virtualVoiceBlackBoxCursor;
        private int _virtualVoiceDroppedCount;
        private int _virtualPhysicalVoiceLimit = MaxVirtualPhysicalVoices;
        private int _virtualVoiceTierPendingSlowTicks;
        private float _virtualVoiceQualityWeight = 1f;
        private int _acousticOcclusionOutputCount;
        private bool _virtualVoiceSortScheduled;
        private bool _acousticOcclusionScheduled;
        private bool _hasVirtualListenerAup;
        private bool _hasVirtualPreviousListenerAbsolutePosition;
        private bool _virtualVoiceBlackBoxDumped;
        private bool _virtualVoiceLowTierTarget;
        private bool _virtualVoiceLowTierApplied;
        private uint[] _virtualChannelStableKeys;
        private int[] _virtualChannelSourceIndices;
        private VirtualVoiceSelection[] _virtualChannelPendingSelections;
        private float[] _virtualChannelFadeRemaining;
        private float[] _virtualChannelFadeStartVolumes;
        private byte[] _virtualChannelPendingFlags;
        private VaultGenerationHandle<AcousticPortalNode> _acousticPortalNodesHandle;
        private VaultGenerationHandle<AcousticPortalEdge> _acousticPortalEdgesHandle;
        private VaultGenerationHandle<AcousticPathResult> _acousticPortalResultHandle;
        private VaultGenerationHandle<float> _acousticPortalCostsHandle;
        private VaultGenerationHandle<int> _acousticPortalCameFromHandle;
        private VaultGenerationHandle<byte> _acousticPortalStatesHandle;
        private NativeArray<AcousticPortalNode> _acousticPortalNodes; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticPortalEdge> _acousticPortalEdges; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<AcousticPathResult> _acousticPortalResult; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<float> _acousticPortalCosts; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<int> _acousticPortalCameFrom; // Vault alias; GlobalDataVault owns backing memory.
        private NativeArray<byte> _acousticPortalStates; // Vault alias; GlobalDataVault owns backing memory.
        private NativeList<int> _acousticPortalOpenSet;
        private NativeList<int> _acousticPortalClosedSet;
        private VaultGenerationHandle<AcousticPortalTelemetryEntry> _acousticPortalBlackBoxHandle;
        private NativeArray<AcousticPortalTelemetryEntry> _acousticPortalBlackBox; // Vault alias; GlobalDataVault owns backing memory.
        private int _acousticPortalBlackBoxCursor;
        private Vector3[] _acousticPortalWaypointScratch;
        private int[] _acousticHabitatNodeMap;
        private int[] _acousticHabitatQueue;
        private AcousticPortalCacheEntry[] _acousticPortalCache;
        private bool _isInitialized;
        private bool _runtimeResourcesInitialized;
        private bool _eventsSubscribed;
        private bool _hotSwapRegistered;
        private IPlayerRuntimeContext _cachedPlayerRuntimeContext;
        private IWeatherService _cachedWeatherService;
        private AcousticZoneController _cachedAcousticZone;
        private HectonSurfaceWeatherDirector _cachedSurfaceWeatherDirector;
        private PlayerCriticalProceduralAudioRenderer _cachedPlayerCriticalAudio;
        private ConstructionManager _cachedConstructionManager;
        private float _cachedSpatialAudioQualityWeight01 = 1f;
        private int _spatialAudioPolicyRefreshFrame = SpatialAudioPolicyUninitializedFrame;
        private int _playerRuntimeContextResolveFrame = -4096;
        private int _weatherServiceResolveFrame = -4096;
        private int _acousticZoneResolveFrame = -4096;
        private int _surfaceWeatherResolveFrame = -4096;
        private int _foveatedDirectorResolveFrame = -4096;
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

            if (Application.isPlaying)
            {
                RefreshCachedAudioRuntimeServicesCold();
                TryRegisterHotSwapListener();
            }

            if (_isInitialized)
            {
                RefreshSpatialAudioPolicyCold();
                TrySubscribeAudioEvents();
            }
            TryRegisterOriginShiftListener();
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

            TryUnregisterHotSwapListener();
            TryUnsubscribeAudioEvents();
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterAudioVirtualizationService(this);
                GlobalRegistry.UnregisterAudioService(this);
                _isInitialized = false;
            }

            if (_registeredUpdatable)
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);

            _registeredUpdatable = false;
            if (_registeredFastTickable)
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Environment);

            _registeredFastTickable = false;
            if (_registeredSlowTickable)
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);

            _registeredSlowTickable = false;
            if (_registeredLateFrameTickable)
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);

            _registeredLateFrameTickable = false;
            if (_registeredOriginShiftListener)
                HectonFloatingOrigin.UnregisterListener(this);

            _registeredOriginShiftListener = false;
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
            ClearVirtualVoiceQueues();
            _listenerPlayerMovement = null;
            _foveatedSimulationDirector = null;
            _cachedPlayerCriticalAudio = null;
            _cachedConstructionManager = null;
            _cachedPlayerRuntimeContext = null;
            _cachedWeatherService = null;
            _cachedAcousticZone = null;
            _cachedSurfaceWeatherDirector = null;
            _cachedSpatialAudioQualityWeight01 = 1f;
            _spatialAudioPolicyRefreshFrame = SpatialAudioPolicyUninitializedFrame;
            _playerRuntimeContextResolveFrame = -4096;
            _weatherServiceResolveFrame = -4096;
            _acousticZoneResolveFrame = -4096;
            _surfaceWeatherResolveFrame = -4096;
            _foveatedDirectorResolveFrame = -4096;
            _listenerWaterDensityMul = 0f;
            SetParasiteRoomAcousticLoad(0);
            SetEclipseAcousticPitchShiftCents(0f);
            _brownoutTarget01 = 0f;
            _brownoutAudioPitchRatio = 1f;
            ApplyBrownoutPitchToMixerAndSources();
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

        /// <inheritdoc />
        public bool IsVirtualizationReady =>
            _runtimeResourcesInitialized &&
            _virtualVoiceWritePool.IsCreated &&
            _virtualVoiceSortPool.IsCreated &&
            _virtualVoiceDtoPool.IsCreated &&
            _virtualVoiceSortKeyPool.IsCreated &&
            _virtualVoiceSelections.IsCreated &&
            _virtualVoiceStatistics.IsCreated;

        /// <inheritdoc />
        public int PhysicalVoiceLimit => math.min(
            math.min(_virtualPhysicalVoiceLimit, VirtualVoiceUtility.ResolveContinuousVoiceBudget(_virtualVoiceQualityWeight)),
            math.clamp(_virtualVoiceTuning.MaxHydratedVoices, 1, MaxVirtualPhysicalVoices));

        /// <inheritdoc />
        public int VirtualVoiceCount => _virtualVoiceWritePool.IsCreated ? _virtualVoiceWriteCount : 0;

        /// <inheritdoc />
        public int ActivePhysicalVoiceCount => _lastVirtualVoiceStatistics.ActivePhysicalVoices;

        /// <inheritdoc />
        public int CulledVoiceCount => _lastVirtualVoiceStatistics.CulledVoices;

        /// <inheritdoc />
        public int StolenVoiceCount => _lastVirtualVoiceStatistics.StolenVoices;

        /// <inheritdoc />
        public int DroppedVoiceCount => math.max(0, _lastVirtualVoiceStatistics.DroppedVoices) + _virtualVoiceDroppedCount;

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
            RefreshCachedAudioRuntimeServicesCold();
            TryRegisterHotSwapListener();
            RefreshSpatialAudioPolicyCold();
            ApplyAmbientOutputSampleRatePolicy();
            TryRegisterOriginShiftListener();
            RefreshVirtualPhysicalVoiceLimit(true);
            RefreshFoveatedDirector();

            if (_isInitialized)
            {
                TryRegisterUpdatable();
                TryRegisterFastTickable();
                TryRegisterSlowTickable();
                TryRegisterLateFrameTickable();
                return;
            }

            GlobalRegistry.RegisterAudioService(this);
            GlobalRegistry.RegisterAudioVirtualizationService(this);
            TryRegisterUpdatable();
            TryRegisterFastTickable();
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
            TryLoadAcousticLutFallbackCold();
            PrepareGlobalWindHowlSource();
#if DEVELOPMENT_BUILD
            EnsureAudioRamDebugOverlay();
#endif
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
            EnsureSpatialAudioPolicyCached();
            AdvanceVirtualVoiceStealFades(safeDeltaTime);
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
            UpdateBrownoutAudioPitch(safeDeltaTime);
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

        public void OnGlobalRegistryServiceRebound(
            GlobalRegistryServiceSlot serviceSlot,
            ref object currentService)
        {
            CacheReboundAudioRuntimeService(serviceSlot, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault && previousService is IDataVault previousVault)
                previousVault.ReleaseOwnerBuffers(SystemID.Audio, out _);

            CacheReboundAudioRuntimeService(serviceSlot, currentService);
        }

        /// <summary>
        /// Ranks virtual acoustic emitters after simulation and before late-frame DSP injection.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time.</param>
        public void FastTick(float deltaTime)
        {
            if (!TryFinalizeAcousticOcclusionNoWait())
            {
                _virtualVoiceDroppedCount += math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
                _virtualVoiceWriteCount = 0;
                _virtualVoiceDtoCount = 0;
                return;
            }

            if (!TryFinalizeVirtualVoiceSortJobNoWait())
            {
                _virtualVoiceDroppedCount += math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
                _virtualVoiceWriteCount = 0;
                _virtualVoiceDtoCount = 0;
                return;
            }

            if (!_virtualVoiceWritePool.IsCreated ||
                !_virtualVoiceSortPool.IsCreated ||
                !_virtualVoiceDtoPool.IsCreated ||
                !_virtualVoiceSortKeyPool.IsCreated ||
                !_virtualVoiceSelections.IsCreated ||
                !_virtualVoiceStatistics.IsCreated ||
                !_acousticSourceWritePool.IsCreated ||
                !_acousticSourceSortPool.IsCreated ||
                !_acousticPreviousAupWritePool.IsCreated ||
                !_acousticPreviousAupSortPool.IsCreated ||
                !_acousticSelectedSourcePool.IsCreated ||
                !_acousticSelectedPreviousAupPool.IsCreated ||
                !_acousticDspOutputPool.IsCreated)
            {
                return;
            }

            if (!TryResolveListenerFrame(
                    out Transform listener,
                    out _,
                    out Vector3 listenerAbsolutePosition,
                    out AbsoluteUniversePosition listenerAup))
            {
                _hasVirtualListenerAup = false;
                _hasVirtualPreviousListenerAbsolutePosition = false;
                _virtualListenerVelocityMetersPerSecond = float3.zero;
                _virtualListenerSdfProbePosition = float3.zero;
                _virtualVoiceWriteCount = 0;
                _virtualVoiceSortCount = 0;
                _virtualVoiceDtoCount = 0;
                _acousticOcclusionOutputCount = 0;
                ResetVirtualVoiceSelections();
                _lastVirtualVoiceStatistics = default;
                return;
            }

            AcousticAup acousticListener = ToAcousticAup(in listenerAup);
            SetVirtualListener(in acousticListener);
            _virtualListenerVelocityMetersPerSecond = ResolveVirtualListenerVelocity(listenerAbsolutePosition, deltaTime);
            float globalQualityWeight = ResolveVirtualVoiceQualityWeight();
            _virtualVoiceQualityWeight = globalQualityWeight;
            float listenerDepthMeters = math.max(0f, -listenerAbsolutePosition.y);
            _virtualListenerDepthMeters = listenerDepthMeters;
            _virtualSimulationTickDeltaSeconds = math.max(0.0001f, SanitizeFinite(deltaTime, 1f / 60f));
            _virtualListenerSdfProbePosition = new float3(listenerAbsolutePosition.x, listenerAbsolutePosition.y, listenerAbsolutePosition.z);
            Vector3 listenerRightVector = listener != null && listener.right.sqrMagnitude > 0.000001f
                ? listener.right.normalized
                : Vector3.right;
            _virtualListenerRight = new float3(listenerRightVector.x, listenerRightVector.y, listenerRightVector.z);
            float depthLowPassHertz = VirtualVoiceUtility.ResolveDepthLowPassHertz(listenerDepthMeters, globalQualityWeight);
            _virtualVoiceSdfSampler = ResolveVirtualVoiceSdfSampler();
            if (_virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0)
            {
                VirtualVoiceTuningSnapshot tuning = _virtualVoiceTuningVault[0];
                _virtualVoiceTuning = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            }

            NativeArray<VirtualVoice> previousSortPool = _virtualVoiceSortPool;
            _virtualVoiceSortPool = _virtualVoiceWritePool;
            _virtualVoiceWritePool = previousSortPool;
            VaultGenerationHandle<VirtualVoice> previousSortHandle = _virtualVoiceSortPoolHandle;
            _virtualVoiceSortPoolHandle = _virtualVoiceWritePoolHandle;
            _virtualVoiceWritePoolHandle = previousSortHandle;
            NativeArray<AcousticSourceDTO> previousAcousticSourceSortPool = _acousticSourceSortPool;
            _acousticSourceSortPool = _acousticSourceWritePool;
            _acousticSourceWritePool = previousAcousticSourceSortPool;
            VaultGenerationHandle<AcousticSourceDTO> previousAcousticSourceSortHandle = _acousticSourceSortPoolHandle;
            _acousticSourceSortPoolHandle = _acousticSourceWritePoolHandle;
            _acousticSourceWritePoolHandle = previousAcousticSourceSortHandle;
            NativeArray<double3> previousAcousticAupSortPool = _acousticPreviousAupSortPool;
            _acousticPreviousAupSortPool = _acousticPreviousAupWritePool;
            _acousticPreviousAupWritePool = previousAcousticAupSortPool;
            VaultGenerationHandle<double3> previousAcousticAupSortHandle = _acousticPreviousAupSortPoolHandle;
            _acousticPreviousAupSortPoolHandle = _acousticPreviousAupWritePoolHandle;
            _acousticPreviousAupWritePoolHandle = previousAcousticAupSortHandle;
            _virtualVoiceSortCount = math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
            _virtualVoiceDtoCount = _virtualVoiceSortCount;
            _virtualVoiceWriteCount = 0;
            int tunedPhysicalLimit = math.min(
                math.min(_virtualPhysicalVoiceLimit, VirtualVoiceUtility.ResolveContinuousVoiceBudget(globalQualityWeight)),
                math.clamp(_virtualVoiceTuning.MaxHydratedVoices, 1, MaxVirtualPhysicalVoices));

            var sortJob = new VirtualVoiceSortJob
            {
                Voices = _virtualVoiceSortPool,
                SortKeys = _virtualVoiceSortKeyPool,
                Selections = _virtualVoiceSelections,
                Statistics = _virtualVoiceStatistics,
                ListenerAup = _virtualListenerAup,
                ListenerVelocityMetersPerSecond = _virtualListenerVelocityMetersPerSecond,
                SdfSampler = _virtualVoiceSdfSampler,
                DefaultSabineRt60Seconds = _listenerSabineRt60Seconds > 0f ? _listenerSabineRt60Seconds : _acousticLutFallbackRt60Seconds,
                DefaultSabineRoomVolumeCubicMeters = _listenerSabineVolumeCubicMeters,
                SoundSpeedMetersPerSecond = _virtualVoiceTuning.SoundSpeedMetersPerSecond,
                GlobalOcclusionPenalty = _virtualVoiceTuning.GlobalOcclusionPenalty,
                OccludedLowPassHertz = _virtualVoiceTuning.OccludedLowPassHertz,
                SabineDecayScale = _virtualVoiceTuning.SabineDecayScale,
                PhysicalVoiceLimit = tunedPhysicalLimit,
                VoiceCount = _virtualVoiceSortCount,
                DroppedVoiceCount = _virtualVoiceDroppedCount,
                Frame = Time.frameCount,
                DisableSdfOcclusion = _virtualVoiceTuning.DisableSdfOcclusion != 0 ? 1 : 0,
                MinimumAudibleEnergy = VirtualVoiceUtility.MinimumAudibleEnergy,
                GlobalQualityWeight = globalQualityWeight,
                DepthLowPassHertz = depthLowPassHertz,
                RollbackActive = ResolveRollbackAudioSuppressionActive() ? 1 : 0
            };

            _virtualVoiceDroppedCount = 0;
            _virtualVoiceSortStartRealtimeSeconds = Time.realtimeSinceStartup;
            _virtualVoiceSortHandle = sortJob.Schedule();
            _virtualVoiceSortScheduled = true;
        }

        /// <summary>
        /// Refreshes listener cave/reverb telemetry on the slow lane.
        /// </summary>
        public void SlowTick()
        {
            RefreshVirtualPhysicalVoiceLimit(false);
            RefreshFoveatedDirector();

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
        /// Locks virtual voice job ownership during floating-origin shifts.
        /// </summary>
        /// <param name="shiftData">Committed shift payload.</param>
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            if (!IsFinite(shiftData.ShiftOffset))
            {
                DumpVirtualVoiceBlackBox();
                return;
            }

            CompleteVirtualVoiceSort();
        }

        /// <summary>
        /// Drains queued gameplay audio events after frame simulation.
        /// </summary>
        public void LateFrameTick()
        {
            AcousticOcclusionUtility.LateFrameTick();
            ConsumeAcousticImpulseSignals();
            TryFinalizeVirtualVoiceSortNoWait();
            InjectVirtualVoiceSelections();
            DrainAudioEventQueue();
#if DEVELOPMENT_BUILD
            UpdateAudioRamDebugOverlay();
#endif
        }

#if DEVELOPMENT_BUILD
        private void EnsureAudioRamDebugOverlay()
        {
            if (_audioRamDebugLabel != null)
                return;

            if (_audioRamDebugTextBuffer == null || _audioRamDebugTextBuffer.Length < AudioRamDebugTextCapacity)
                _audioRamDebugTextBuffer = new char[AudioRamDebugTextCapacity]; // COLD ALLOC: char[48] - development audio RAM overlay text staging - owner: SpatialAudioManager

            GameObject overlayRoot = new GameObject("AudioRamDebugOverlay", typeof(RectTransform), typeof(Canvas)); // COLD ALLOC: GameObject[1] - development audio RAM overlay canvas - owner: SpatialAudioManager
            overlayRoot.transform.SetParent(transform, false);

            Canvas canvas = overlayRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = AudioRamDebugCanvasSortingOrder;

            GameObject labelObject = new GameObject("AudioRamDebugLabel", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI)); // COLD ALLOC: GameObject[1] - development audio RAM overlay label - owner: SpatialAudioManager
            labelObject.transform.SetParent(overlayRoot.transform, false);

            _audioRamDebugLabel = labelObject.GetComponent<TMPro.TextMeshProUGUI>();
            TMPro.TMP_FontAsset defaultFont = TMPro.TMP_Settings.defaultFontAsset;
            if (defaultFont != null)
                _audioRamDebugLabel.font = defaultFont;

            _audioRamDebugLabel.fontSize = 12f;
            _audioRamDebugLabel.alignment = TMPro.TextAlignmentOptions.Left;
            _audioRamDebugLabel.color = new Color32(126, 226, 255, 230);
            _audioRamDebugLabel.raycastTarget = false;

            RectTransform rect = (RectTransform)labelObject.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(320f, 24f);

            _audioRamDebugLastResidentKilobytes = -1;
            _audioRamDebugLastClipCount = -1;
            UpdateAudioRamDebugOverlay();
        }

        private void UpdateAudioRamDebugOverlay()
        {
            if (_audioRamDebugLabel == null)
                return;

            long residentBytes = AudioResidencyCache.CurrentResidentBytes;
            if (residentBytes < 0L)
                residentBytes = 0L;

            int residentKilobytes = residentBytes >= int.MaxValue ? int.MaxValue : (int)(residentBytes >> 10);
            int clipCount = math.max(0, AudioResidencyCache.ResidentClipCount);
            if (residentKilobytes == _audioRamDebugLastResidentKilobytes &&
                clipCount == _audioRamDebugLastClipCount)
                return;

            _audioRamDebugLastResidentKilobytes = residentKilobytes;
            _audioRamDebugLastClipCount = clipCount;

            int residentTenthsMb = residentKilobytes >= int.MaxValue / 10
                ? int.MaxValue
                : residentKilobytes * 10 / 1024;
            int cursor = 0;
            cursor = WriteAscii("Audio RAM: ".AsSpan(), _audioRamDebugTextBuffer, cursor);
            cursor = WritePositiveInt(residentTenthsMb / 10, _audioRamDebugTextBuffer, cursor);
            cursor = WriteAscii(".".AsSpan(), _audioRamDebugTextBuffer, cursor);
            cursor = WritePositiveInt(residentTenthsMb % 10, _audioRamDebugTextBuffer, cursor);
            cursor = WriteAscii(" MB | Clips: ".AsSpan(), _audioRamDebugTextBuffer, cursor);
            cursor = WritePositiveInt(clipCount, _audioRamDebugTextBuffer, cursor);

            _audioRamDebugLabel.SetCharArray(_audioRamDebugTextBuffer, 0, cursor);
        }

        private static int WriteAscii(ReadOnlySpan<char> source, char[] destination, int cursor)
        {
            int limit = math.min(source.Length, destination.Length - cursor);
            for (int i = 0; i < limit; i++)
                destination[cursor++] = source[i];

            return cursor;
        }

        private static int WritePositiveInt(int value, char[] destination, int cursor)
        {
            value = math.max(0, value);
            if (value == 0)
            {
                if (cursor < destination.Length)
                    destination[cursor++] = '0';

                return cursor;
            }

            int divisor = 1;
            while (value / divisor >= 10)
                divisor *= 10;

            while (divisor > 0 && cursor < destination.Length)
            {
                int digit = value / divisor;
                destination[cursor++] = (char)('0' + digit);
                value -= digit * divisor;
                divisor /= 10;
            }

            return cursor;
        }
#endif

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
            _worldSourceVoiceCategories = new byte[_poolSize]; // COLD ALLOC: byte[_poolSize] - hard-capped SFX category occupancy - owner: SpatialAudioManager
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
            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition))
                return;

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

        private int PlayAtPointResolved(
            AudioClip clip,
            Vector3 position,
            in AbsoluteUniversePosition sourceAup,
            Vector3 sourceAbsolutePosition,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            int stationaryCacheKey,
            float dopplerRatio = 1f)
        {
            if (clip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayAtPointNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Debug.LogWarning("[SpatialAudioManager] PlayAtPoint called with null clip.");
#endif
                return -1;
            }

            if (_pool == null || _poolSize <= 0)
                return -1;

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
                PublishAcousticEchoPortalTap(
                    in sourceAup,
                    in acousticPortalResult,
                    volume,
                    stationaryCacheKey,
                    0u);
            }

            if (hasListener && IsBeyondMaxHearingRange(in audibleAup, in listenerAup))
                return -1;

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in audibleAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return -1;

            byte voiceCategory = ResolveAudioVoiceCategory(clip);
            if (!TryReserveAudioVoiceCategory(voiceCategory, -1))
                return -1;

            int index = AcquireSourceIndex();
            if (index < 0)
                return -1;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            AssignAudioVoiceCategory(index, voiceCategory);
            source.enabled = true;

            // â”€â”€ ÐŸÐ¾Ð·Ð¸Ñ†Ð¸Ð¾Ð½Ð¸Ñ€Ð¾Ð²Ð°Ð½Ð¸Ðµ â”€â”€
            source.transform.position = audiblePosition;
            AudioResidencyCache.TouchClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup), true);

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
            float clampedDopplerRatio = math.clamp(
                1f,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && index < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[index] = clampedDopplerRatio;
            source.pitch = ResolveSourcePitch(index, clampedDopplerRatio);
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
            return index;
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

        public bool QueuePrologueAudioTransition(in AudioTransitionState state)
        {
            PlayerCriticalProceduralAudioRenderer playerCriticalAudio = _cachedPlayerCriticalAudio;
            return playerCriticalAudio != null && playerCriticalAudio.QueuePrologueAudioTransition(in state);
        }

        public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            if (!_virtualVoiceWritePool.IsCreated ||
                _virtualVoiceWriteCount >= MaxVirtualVoiceCapacity ||
                !TryResolveAudioEventClip(signal.EventID, out AudioClip clip) ||
                !AcousticAup.IsFinite(in signal.SourceAup))
            {
                if (_virtualVoiceWritePool.IsCreated)
                    _virtualVoiceDroppedCount++;
                return false;
            }

            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in signal.SourceAup);
            Vector3 runtimePosition = ToRuntimeVector3(in sourceAup);
            byte foveatedTier = ResolveVirtualVoiceFoveatedTier(runtimePosition);
            float priority = ResolveVirtualVoicePriority(signal.Volume, signal.Flags, foveatedTier);
            uint clipHash = unchecked((uint)EntityId.ToULong(clip.GetEntityId()));
            uint sourceEntityId = signal.StationaryCacheKey != 0
                ? unchecked((uint)signal.StationaryCacheKey)
                : (clipHash ^ signal.EventID);
            byte acousticEnvironment = _listenerInsideBaseInteriorMuffle
                ? (byte)1
                : _listenerCaveInterior01 >= 0.35f ? (byte)2 : (byte)4;
            VirtualVoiceDspFlags dspFlags = _listenerInsideBaseInteriorMuffle
                ? VirtualVoiceDspFlags.InsideSubmarineHull
                : VirtualVoiceDspFlags.None;
            var request = new VirtualVoiceRequest(
                signal.EventID,
                clipHash,
                sourceEntityId,
                in signal.SourceAup,
                float3.zero,
                signal.Volume,
                priority,
                signal.Pitch,
                1f,
                _listenerSabineRt60Seconds,
                _listenerSabineVolumeCubicMeters,
                VirtualVoiceUtility.OpenLowPassHertz,
                0f,
                signal.StationaryCacheKey,
                ToVirtualVoicePortalFlags(signal.Flags),
                foveatedTier,
                acousticEnvironment,
                dspFlags);
            AppendVirtualVoice(in request);
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

            HullStressSignal routedSignal = signal;
            AbsoluteUniversePosition sourceAup = signal.SourceAup;
            float3 sourceRuntime = sourceAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(sourceRuntime)))
                return false;

            Vector3 sourceRuntimePosition = new Vector3(sourceRuntime.x, sourceRuntime.y, sourceRuntime.z);
            if (TryResolveListenerFrame(
                    out Transform listener,
                    out Vector3 listenerRuntimePosition,
                    out _,
                    out AbsoluteUniversePosition listenerAup))
            {
                ResolveListenerBasis(listener, out float3 listenerRight, out _, out _);
                if (TryResolveAcousticPortalPath(
                        sourceRuntimePosition,
                        listenerRuntimePosition,
                        listenerRight,
                        in sourceAup,
                        in listenerAup,
                        0,
                        out AcousticPathResult acousticPathResult))
                {
                    float routedTransmission = math.saturate(
                        signal.AcousticTransmission01 * acousticPathResult.Transmission01);
                    float routedLowPassCutoffHz = math.min(
                        signal.LowPassCutoffHz,
                        acousticPathResult.LowPassCutoffHz);
                    float routedDelaySeconds = math.max(0f, signal.AcousticDelaySeconds) +
                        math.max(0f, acousticPathResult.DelaySeconds);
                    routedSignal = new HullStressSignal(
                        in sourceAup,
                        sourceRuntimePosition,
                        signal.Stress01,
                        signal.PressureDelta,
                        signal.DepthMeters,
                        signal.PitchScale,
                        routedTransmission,
                        routedLowPassCutoffHz,
                        routedDelaySeconds);
                    PublishAcousticEchoPortalTap(
                        in sourceAup,
                        in acousticPathResult,
                        math.max(signal.Stress01, math.abs(signal.PressureDelta)),
                        0,
                        0u);
                }
            }

            ProceduralAudioEvents.RaiseHullStressSignal(in routedSignal);
            return true;
        }

        public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            if (!IsInitialized ||
                !math.isfinite(signal.LostKineticEnergy) ||
                !math.isfinite(signal.ImpactSpeed) ||
                math.max(signal.LostKineticEnergy, signal.ImpactSpeed) <= 0f)
            {
                return false;
            }

            float3 runtime = signal.PointAup.ToRuntimeFloat3();
            if (!math.all(math.isfinite(runtime)))
                return false;

            Vector3 position = new Vector3(runtime.x, runtime.y, runtime.z);
            float amplitude = math.saturate(signal.ImpactSpeed * 0.04f + signal.LostKineticEnergy * 0.000025f);
            bool radarQueued = TryQueueImpactRadarEmitter(
                position,
                in signal.PointAup,
                amplitude * ImpactEmitterAmplitudeScale,
                amplitude);

            PlayerCriticalProceduralAudioRenderer renderer = _cachedPlayerCriticalAudio;
            bool proceduralQueued = renderer != null && renderer.QueueHighSpeedImpactSignal(in signal);
            return radarQueued || proceduralQueued;
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

        private bool TryResolveAudioEventClip(uint eventID, uint clipHash, out AudioClip clip)
        {
            if (TryResolveAudioEventClip(eventID, out clip))
                return true;

            return TryResolveAudioClipHash(clipHash, out clip);
        }

        private bool TryResolveAudioClipHash(uint clipHash, out AudioClip clip)
        {
            clip = null;
            if (clipHash == 0u ||
                !_audioClipHashToTableIndex.IsCreated ||
                !_audioClipHashToTableIndex.TryGetValue(clipHash, out int tableIndex))
            {
                return false;
            }

            AudioClip[] table = _audioEventClipTable;
            if (table == null || tableIndex < 0 || tableIndex >= table.Length)
                return false;

            clip = table[tableIndex];
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

        /// <inheritdoc />
        public bool EnqueueVirtualVoice(in VirtualVoiceRequest request)
        {
            if (!_virtualVoiceWritePool.IsCreated ||
                !_virtualVoiceDtoPool.IsCreated ||
                _virtualVoiceWriteCount >= MaxVirtualVoiceCapacity ||
                !AcousticAup.IsFinite(in request.SourceAup) ||
                !TryResolveAudioEventClip(request.EventID, request.ClipHash, out _))
            {
                if (_virtualVoiceWritePool.IsCreated)
                    _virtualVoiceDroppedCount++;
                return false;
            }

            AppendVirtualVoice(in request);
            return true;
        }

        private void AppendVirtualVoice(in VirtualVoiceRequest request)
        {
            if (request.FoveatedTier >= VirtualVoiceUtility.FoveatedTierFrozen)
                EvictCreatureAudioBanksForFrozenTier();

            float priority = request.FoveatedTier >= VirtualVoiceUtility.FoveatedTierFrozen
                ? 0f
                : math.max(0f, SanitizeFinite(request.Priority, 0f));
            uint stableKey = VirtualVoiceUtility.ComputeStableKey(
                request.EventID,
                request.ClipHash,
                request.SourceEntityID,
                request.StationaryCacheKey,
                in request.SourceAup);
            float3 sourceVelocity = math.all(math.isfinite(request.SourceVelocityMetersPerSecond))
                ? request.SourceVelocityMetersPerSecond
                : float3.zero;
            int writeIndex = _virtualVoiceWriteCount;
            double3 acousticMeters = ToAbsoluteAcousticMeters(in request.SourceAup);
            _virtualVoiceWritePool[writeIndex] = new VirtualVoice
            {
                EventID = request.EventID,
                ClipHash = request.ClipHash,
                StableKey = stableKey,
                SourceEntityID = request.SourceEntityID,
                SourceAup = request.SourceAup,
                SourceVelocityMetersPerSecond = sourceVelocity,
                Volume = math.saturate(SanitizeFinite(request.Volume, 0f)),
                Priority = priority,
                Pitch = math.clamp(SanitizeFinite(request.Pitch, 1f), 0.1f, 3f),
                DopplerRatio = math.clamp(
                    SanitizeFinite(request.DopplerRatio, 1f),
                    VirtualVoiceUtility.MinimumDopplerRatio,
                    VirtualVoiceUtility.MaximumDopplerRatio),
                SabineRt60Seconds = math.max(0f, SanitizeFinite(request.SabineRt60Seconds, 0f)),
                SabineRoomVolumeCubicMeters = math.max(0f, SanitizeFinite(request.SabineRoomVolumeCubicMeters, 0f)),
                LowPassCutoffHz = math.clamp(
                    SanitizeFinite(request.LowPassCutoffHz, VirtualVoiceUtility.OpenLowPassHertz),
                    80f,
                    VirtualVoiceUtility.OpenLowPassHertz),
                DelaySeconds = math.max(0f, SanitizeFinite(request.DelaySeconds, 0f)),
                StationaryCacheKey = request.StationaryCacheKey,
                PortalFlags = request.PortalFlags,
                FoveatedTier = request.FoveatedTier,
                AcousticEnvironment = request.AcousticEnvironment,
                DspFlags = request.DspFlags
            };
            _virtualVoiceDtoPool[writeIndex] = new VirtualVoiceDTO
            {
                AupMeters = acousticMeters,
                Volume = math.saturate(SanitizeFinite(request.Volume, 0f)),
                Pitch = math.clamp(SanitizeFinite(request.Pitch, 1f), 0.1f, 3f),
                ClipHash = request.ClipHash,
                SourceEntityID = request.SourceEntityID,
                Importance = priority,
                Padding = 0u
            };
            if (_acousticSourceWritePool.IsCreated && writeIndex < _acousticSourceWritePool.Length)
            {
                _acousticSourceWritePool[writeIndex] = new AcousticSourceDTO
                {
                    SourceHash = stableKey,
                    BaseVolume = math.saturate(SanitizeFinite(request.Volume, 0f)),
                    BasePitch = math.clamp(SanitizeFinite(request.Pitch, 1f), 0.1f, 3f),
                    Flags = (uint)request.DspFlags | ((uint)request.PortalFlags << 8),
                    AUP_Position = acousticMeters,
                    ComputedOcclusion = 0f,
                    ComputedReverb = 0f
                };
            }

            if (_acousticPreviousAupWritePool.IsCreated && writeIndex < _acousticPreviousAupWritePool.Length)
            {
                double3 velocity = new double3(sourceVelocity.x, sourceVelocity.y, sourceVelocity.z);
                _acousticPreviousAupWritePool[writeIndex] = acousticMeters - velocity * (1.0 / 60.0);
            }

            _virtualVoiceWriteCount = writeIndex + 1;
            _virtualVoiceDtoCount = math.max(_virtualVoiceDtoCount, _virtualVoiceWriteCount);
        }

        /// <inheritdoc />
        public void SetVirtualListener(in AcousticAup listenerAup)
        {
            if (!AcousticAup.IsFinite(in listenerAup))
            {
                _hasVirtualListenerAup = false;
                return;
            }

            _virtualListenerAup = listenerAup;
            _hasVirtualListenerAup = true;
        }

        /// <inheritdoc />
        public void SetLowTierVirtualization(bool lowTier)
        {
            ApplyVirtualVoiceQualityWeight(lowTier ? 0f : 1f);
        }

        private float3 ResolveVirtualListenerVelocity(Vector3 listenerAbsolutePosition, float deltaTime)
        {
            if (!_hasVirtualPreviousListenerAbsolutePosition || deltaTime <= 0.0001f)
            {
                _virtualPreviousListenerAbsolutePosition = listenerAbsolutePosition;
                _hasVirtualPreviousListenerAbsolutePosition = true;
                return float3.zero;
            }

            float deltaTimeInv = math.rcp(math.max(deltaTime, 0.0001f));
            Vector3 velocity = (listenerAbsolutePosition - _virtualPreviousListenerAbsolutePosition) * deltaTimeInv;
            _virtualPreviousListenerAbsolutePosition = listenerAbsolutePosition;
            return new float3(velocity.x, velocity.y, velocity.z);
        }

        private VirtualVoiceSdfSampler ResolveVirtualVoiceSdfSampler()
        {
            float qualityWeight = ResolveVirtualVoiceQualityWeight();
            bool enabled = qualityWeight > 0.02f &&
                (_listenerInsideBaseInteriorMuffle || _listenerCaveInterior01 >= 0.35f);
            return new VirtualVoiceSdfSampler
            {
                Center = float3.zero,
                HalfExtents = new float3(0.001f),
                WallPlaneY = 0f,
                WallThickness = _listenerInsideBaseInteriorMuffle
                    ? math.lerp(0.04f, 0.08f, qualityWeight)
                    : math.lerp(0.015f, 0.06f, math.saturate(_listenerCaveInterior01) * math.max(0.25f, qualityWeight)),
                Enabled = enabled ? (byte)1 : (byte)0,
                UseBox = 0
            };
        }

        /// <inheritdoc />
        public void ApplyVirtualVoiceAupShift(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            CompleteVirtualVoiceSort();
            RebaseVirtualVoicePool(_virtualVoiceWritePool, _virtualVoiceWriteCount, gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseVirtualVoicePool(_virtualVoiceSortPool, _virtualVoiceSortCount, gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseAcousticSourcePool(_acousticSourceWritePool, _acousticPreviousAupWritePool, _virtualVoiceWriteCount, gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseAcousticSourcePool(_acousticSourceSortPool, _acousticPreviousAupSortPool, _virtualVoiceSortCount, gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseVirtualVoiceDtoPool(gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseVirtualVoiceSelections(gridDeltaX, gridDeltaY, gridDeltaZ);
            RebaseVirtualChannelPendingSelections(gridDeltaX, gridDeltaY, gridDeltaZ);
            if (_hasVirtualListenerAup)
            {
                _virtualListenerAup.GridX += gridDeltaX;
                _virtualListenerAup.GridY += gridDeltaY;
                _virtualListenerAup.GridZ += gridDeltaZ;
            }
        }

        /// <inheritdoc />
        public bool TryGetVirtualizationStats(out VirtualVoiceStatistics statistics)
        {
            statistics = _lastVirtualVoiceStatistics;
            return IsVirtualizationReady;
        }

        public bool TryGetVirtualVoiceRuntimeTuning(out VirtualVoiceTuningSnapshot tuning)
        {
            if (_virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0)
            {
                VirtualVoiceTuningSnapshot stored = _virtualVoiceTuningVault[0];
                _virtualVoiceTuning = VirtualVoiceTuningSnapshot.Sanitize(in stored);
            }

            tuning = _virtualVoiceTuning;
            return _virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0;
        }

        public void ApplyVirtualVoiceRuntimeTuning(in VirtualVoiceTuningSnapshot tuning)
        {
            VirtualVoiceTuningSnapshot sanitized = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            _virtualVoiceTuning = sanitized;
            if (_virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0)
                _virtualVoiceTuningVault[0] = sanitized;
        }

        public int ReloadAcousticMaterialRowsFromCsvCold(ReadOnlySpan<byte> csvBytes)
        {
            if (!_acousticMaterialRows.IsCreated || _acousticMaterialRows.Length <= 0)
                return 0;

            int parsed = VirtualVoiceProfileCsvParser.ParseMaterialRows(csvBytes, _acousticMaterialRows);
            return parsed > 0
                ? parsed
                : VirtualVoiceProfileCsvParser.GenerateEmergencyMockAcoustics(_acousticMaterialRows);
        }

        private void CompleteVirtualVoiceSort()
        {
            CompleteVirtualVoiceSortJobForBarrier();
            CompleteAcousticOcclusionForBarrier();
        }

        private void TryFinalizeVirtualVoiceSortNoWait()
        {
            TryFinalizeVirtualVoiceSortJobNoWait();
            TryFinalizeAcousticOcclusionNoWait();
        }

        private void ScheduleAcousticOcclusionJob(
            int sourceCount,
            in AcousticAup listenerAup,
            float3 listenerRight,
            float listenerDepthMeters,
            float globalQualityWeight,
            float deltaTime)
        {
            int count = PopulateSelectedAcousticSources(sourceCount);
            if (count <= 0)
            {
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }
            _acousticOcclusionOutputCount = count;

            double3 listenerMeters = ToAbsoluteAcousticMeters(in listenerAup);
            float safeDelta = math.max(0.0001f, SanitizeFinite(deltaTime, 1f / 60f));
            bool hasVoxelSdf = TrySnapshotAcousticSdfPayload(
                new Vector3(_virtualListenerSdfProbePosition.x, _virtualListenerSdfProbePosition.y, _virtualListenerSdfProbePosition.z),
                out NativeArray<byte>.ReadOnly sdfVoxels,
                out int3 sdfDimensions,
                out float3 sdfOrigin,
                out float3 sdfCellSize,
                out float sdfRange);
            double3 listenerVelocity = new double3(
                _virtualListenerVelocityMetersPerSecond.x,
                _virtualListenerVelocityMetersPerSecond.y,
                _virtualListenerVelocityMetersPerSecond.z);
            double3 previousListenerMeters = listenerMeters - listenerVelocity * safeDelta;
            float rightSq = math.lengthsq(listenerRight);
            float3 right = rightSq > 0.000001f
                ? listenerRight * math.rsqrt(math.max(rightSq, 0.000001f))
                : new float3(1f, 0f, 0f);
            var occlusionJob = new AcousticOcclusionJob
            {
                Sources = _acousticSelectedSourcePool,
                Outputs = _acousticDspOutputPool,
                PreviousSourceAup = _acousticSelectedPreviousAupPool,
                SdfVoxels = hasVoxelSdf ? sdfVoxels : default,
                Materials = _acousticMaterialRows,
                FallbackSdf = _virtualVoiceSdfSampler,
                ListenerAup = listenerMeters,
                PreviousListenerAup = previousListenerMeters,
                ListenerRight = right,
                SdfOriginMeters = hasVoxelSdf ? sdfOrigin - _virtualListenerSdfProbePosition : float3.zero,
                SdfCellSizeMeters = hasVoxelSdf ? sdfCellSize : new float3(1f),
                SdfDimensions = hasVoxelSdf ? sdfDimensions : new int3(0, 0, 0),
                SdfDistanceScaleMeters = hasVoxelSdf ? sdfRange : 1f,
                SimulationTickDeltaSeconds = safeDelta,
                SoundSpeedMetersPerSecond = _virtualVoiceTuning.SoundSpeedMetersPerSecond,
                ListenerDepthMeters = listenerDepthMeters,
                GlobalQualityWeight = globalQualityWeight,
                RollbackActive = ResolveRollbackAudioSuppressionActive() ? 1 : 0,
                SourceCount = count
            };

            _acousticOcclusionStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            _acousticOcclusionHandle = occlusionJob.Schedule(count, 16);
            _acousticOcclusionScheduled = true;
        }

        private bool TrySnapshotAcousticSdfPayload(
            Vector3 targetRuntimePosition,
            out NativeArray<byte>.ReadOnly sdfVoxels,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange)
        {
            sdfVoxels = default;
            sdfDimensions = default;
            sdfOrigin = float3.zero;
            sdfCellSize = float3.zero;
            sdfRange = 0f;

            if (!HectonVoxelVolume.TryGetClosestPublishedSonarSdfPayload(
                    targetRuntimePosition,
                    out NativeArray<byte>.ReadOnly publishedSdf,
                    out _,
                    out Vector3Int publishedDimensions,
                    out Vector3 publishedOrigin,
                    out Vector3 publishedCellSize,
                    out float publishedRange,
                    out _))
            {
                return false;
            }

            int3 resolvedDimensions = new int3(publishedDimensions.x, publishedDimensions.y, publishedDimensions.z);
            if (!TryResolveAcousticSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                !publishedSdf.IsCreated ||
                publishedSdf.Length < expectedLength)
            {
                return false;
            }

            NativeArray<byte>.ReadOnly resolvedSdf = publishedSdf;
            IDataVault vault = _dataVault;
            if (TryOpenBorrowedAudioVaultBuffer(
                    vault,
                    ref _acousticVoxelSdfTexture3DHandle,
                    BufferID.VoxelSdfTexture3D,
                    SystemID.WorldStreaming,
                    expectedLength,
                    out NativeArray<byte> vaultSdfTexture3D))
            {
                resolvedSdf = vaultSdfTexture3D.AsReadOnly();
            }

            float3 resolvedOrigin = new float3(publishedOrigin.x, publishedOrigin.y, publishedOrigin.z);
            float3 resolvedCellSize = new float3(publishedCellSize.x, publishedCellSize.y, publishedCellSize.z);
            float resolvedRange = math.max(0f, SanitizeFinite(publishedRange, 0f));
            if (!math.all(math.isfinite(resolvedOrigin)) ||
                !math.all(math.isfinite(resolvedCellSize)) ||
                math.any(math.abs(resolvedCellSize) <= new float3(0.0001f)) ||
                resolvedRange <= 0.0001f)
            {
                return false;
            }

            _acousticVoxelSdfTexture3D = resolvedSdf;
            sdfVoxels = resolvedSdf;
            sdfDimensions = resolvedDimensions;
            sdfOrigin = resolvedOrigin;
            sdfCellSize = resolvedCellSize;
            sdfRange = resolvedRange;
            return true;
        }

        private static bool TryResolveAcousticSdfVoxelCount(int3 dimensions, out int voxelCount)
        {
            voxelCount = 0;
            if (dimensions.x <= 1 || dimensions.y <= 1 || dimensions.z <= 1)
                return false;

            long count = (long)dimensions.x * dimensions.y * dimensions.z;
            if (count <= 0L || count > int.MaxValue)
                return false;

            voxelCount = (int)count;
            return true;
        }

        private int PopulateSelectedAcousticSources(int selectedCount)
        {
            if (!_virtualVoiceSelections.IsCreated ||
                !_acousticSelectedSourcePool.IsCreated ||
                !_acousticSelectedPreviousAupPool.IsCreated)
            {
                return 0;
            }

            int selectionLimit = math.clamp(
                selectedCount,
                0,
                math.min(
                    _virtualVoiceSelections.Length,
                    math.min(_acousticSelectedSourcePool.Length, _acousticSelectedPreviousAupPool.Length)));
            int written = 0;
            double safeDelta = math.max((double)_virtualSimulationTickDeltaSeconds, 0.0001);
            for (int i = 0; i < selectionLimit; i++)
            {
                VirtualVoiceSelection selection = _virtualVoiceSelections[i];
                if (selection.StableKey == 0u)
                    continue;

                double3 acousticMeters = ToAbsoluteAcousticMeters(in selection.SourceAup);
                float3 velocity = math.all(math.isfinite(selection.SourceVelocityMetersPerSecond))
                    ? selection.SourceVelocityMetersPerSecond
                    : float3.zero;
                _acousticSelectedSourcePool[written] = new AcousticSourceDTO
                {
                    SourceHash = selection.StableKey,
                    BaseVolume = math.saturate(SanitizeFinite(selection.Volume, 0f)),
                    BasePitch = math.clamp(SanitizeFinite(selection.Pitch, 1f), 0.1f, 3f),
                    Flags = (uint)selection.DspFlags | ((uint)selection.PortalFlags << 8),
                    AUP_Position = acousticMeters,
                    ComputedOcclusion = 0f,
                    ComputedReverb = 0f
                };
                _acousticSelectedPreviousAupPool[written] = acousticMeters - new double3(velocity.x, velocity.y, velocity.z) * safeDelta;

                written++;
            }

            return written;
        }

        private bool TryFinalizeAcousticOcclusionNoWait()
        {
            if (!_acousticOcclusionScheduled)
                return true;

            if (!_acousticOcclusionHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _acousticOcclusionHandle))
                return false;

            FinishAcousticOcclusionCompletion();
            return true;
        }

        private bool CompleteAcousticOcclusionForBarrier()
        {
            if (!_acousticOcclusionScheduled)
                return true;

            if (!DispatcherJobFence.TryComplete(ref _acousticOcclusionHandle, forceComplete: true))
                return false;

            FinishAcousticOcclusionCompletion();
            return true;
        }

        private void FinishAcousticOcclusionCompletion()
        {
            _acousticOcclusionScheduled = false;
            if (_acousticOcclusionStartTicks > 0L)
            {
                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _acousticOcclusionStartTicks;
                _lastAcousticOcclusionTimeMs = math.max(
                    0f,
                    (float)(elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                if (!math.isfinite(_lastAcousticOcclusionTimeMs))
                    _lastAcousticOcclusionTimeMs = 0f;
            }
            else
            {
                _lastAcousticOcclusionTimeMs = 0f;
            }

            _acousticOcclusionStartTicks = 0L;
            _lastVirtualVoiceStatistics.AcousticOcclusionTimeMs = _lastAcousticOcclusionTimeMs;
            if (_virtualVoiceStatistics.IsCreated && _virtualVoiceStatistics.Length > 0)
                _virtualVoiceStatistics[0] = _lastVirtualVoiceStatistics;
            PushVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
            PublishVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
        }

        private bool TryFinalizeVirtualVoiceSortJobNoWait()
        {
            if (!_virtualVoiceSortScheduled)
                return true;

            if (!_virtualVoiceSortHandle.IsCompleted)
            {
                VirtualVoiceStatistics overrunStatistics = _lastVirtualVoiceStatistics;
                overrunStatistics.Frame = Time.frameCount;
                overrunStatistics.TotalVoices = math.clamp(_virtualVoiceSortCount, 0, MaxVirtualVoiceCapacity);
                overrunStatistics.DroppedVoices += math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
                overrunStatistics.SortTimeMs = math.max(
                    0.5f,
                    (Time.realtimeSinceStartup - _virtualVoiceSortStartRealtimeSeconds) * 1000f);
                overrunStatistics.AcousticOcclusionTimeMs = _lastAcousticOcclusionTimeMs;
                PushVirtualVoiceTelemetry(in overrunStatistics);
                PublishVirtualVoiceTelemetry(in overrunStatistics);
                return false;
            }

            long sortWaitStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryFinalizeCompleted(ref _virtualVoiceSortHandle))
                return false;

            FinishVirtualVoiceSortCompletion(sortWaitStartTicks);
            return true;
        }

        private bool CompleteVirtualVoiceSortJobForBarrier()
        {
            if (!_virtualVoiceSortScheduled)
                return true;

            long sortWaitStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            if (!DispatcherJobFence.TryComplete(ref _virtualVoiceSortHandle, forceComplete: true))
                return false;

            FinishVirtualVoiceSortCompletion(sortWaitStartTicks);
            return true;
        }

        private void FinishVirtualVoiceSortCompletion(long sortWaitStartTicks)
        {
            long sortWaitTicks = System.Diagnostics.Stopwatch.GetTimestamp() - sortWaitStartTicks;
            _virtualVoiceSortScheduled = false;
            if (_virtualVoiceStatistics.IsCreated && _virtualVoiceStatistics.Length > 0)
            {
                _lastVirtualVoiceStatistics = _virtualVoiceStatistics[0];
                float elapsedMs = math.max(0f, (float)(sortWaitTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
                _lastVirtualVoiceStatistics.SortTimeMs = elapsedMs;
                _lastVirtualVoiceStatistics.AcousticOcclusionTimeMs = _lastAcousticOcclusionTimeMs;
                _virtualVoiceStatistics[0] = _lastVirtualVoiceStatistics;
                _virtualVoiceSortCount = math.clamp(_lastVirtualVoiceStatistics.AudibleVoices, 0, MaxVirtualVoiceCapacity);
            }

            ScheduleAcousticOcclusionJob(
                _lastVirtualVoiceStatistics.ActivePhysicalVoices,
                in _virtualListenerAup,
                _virtualListenerRight,
                _virtualListenerDepthMeters,
                _virtualVoiceQualityWeight,
                _virtualSimulationTickDeltaSeconds);
            if (!_acousticOcclusionScheduled)
            {
                _lastVirtualVoiceStatistics.AcousticOcclusionTimeMs = _lastAcousticOcclusionTimeMs;
                if (_virtualVoiceStatistics.IsCreated && _virtualVoiceStatistics.Length > 0)
                    _virtualVoiceStatistics[0] = _lastVirtualVoiceStatistics;
                PushVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
                PublishVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
            }
        }

        private void InjectVirtualVoiceSelections()
        {
            if (!_virtualVoiceSelections.IsCreated ||
                _virtualChannelStableKeys == null ||
                _virtualChannelSourceIndices == null)
            {
                return;
            }

            int outputCapacity = _pool != null ? math.min(_pool.Length, MaxVirtualPhysicalVoices) : MaxVirtualPhysicalVoices;
            int safeLimit = math.clamp(_lastVirtualVoiceStatistics.PhysicalVoiceLimit, 0, outputCapacity);
            int selectedCount = math.clamp(_lastVirtualVoiceStatistics.ActivePhysicalVoices, 0, safeLimit);
            for (int channel = safeLimit; channel < MaxVirtualPhysicalVoices; channel++)
                BeginVirtualChannelFadeToSilence(channel);

            for (int i = 0; i < selectedCount; i++)
            {
                VirtualVoiceSelection selection = _virtualVoiceSelections[i];
                if (selection.StableKey == 0u)
                    continue;

                ApplyAcousticDspOutputToSelection(ref selection);
                int channel = FindVirtualChannelByStableKey(selection.StableKey, safeLimit);
                if (channel < 0)
                    channel = FindVirtualPendingChannelByStableKey(selection.StableKey, safeLimit);
                if (channel < 0)
                    channel = FindFreeVirtualChannel(safeLimit);
                if (channel < 0)
                    channel = math.min(i, safeLimit - 1);
                if (channel < 0)
                    continue;

                uint currentKey = _virtualChannelStableKeys[channel];
                if (currentKey != 0u && currentKey != selection.StableKey)
                    BeginVirtualChannelSteal(channel, in selection);
                else
                    StartOrUpdateVirtualPhysicalVoice(channel, in selection);
            }

            for (int channel = 0; channel < safeLimit; channel++)
            {
                if (_virtualChannelPendingFlags != null && _virtualChannelPendingFlags[channel] != 0)
                    continue;

                uint key = _virtualChannelStableKeys[channel];
                if (key != 0u && !IsVirtualStableKeySelected(key, selectedCount))
                    BeginVirtualChannelFadeToSilence(channel);
            }
        }

        private void ApplyAcousticDspOutputToSelection(ref VirtualVoiceSelection selection)
        {
            if (!_acousticDspOutputPool.IsCreated || selection.StableKey == 0u)
                return;

            int limit = math.clamp(_acousticOcclusionOutputCount, 0, _acousticDspOutputPool.Length);
            for (int i = 0; i < limit; i++)
            {
                AcousticDspOutputDTO output = _acousticDspOutputPool[i];
                if (output.SourceHash != selection.StableKey)
                    continue;

                selection.Volume = math.saturate(SanitizeFinite(output.Volume, selection.Volume));
                selection.EffectiveVolume = selection.Volume;
                selection.Pitch = math.clamp(SanitizeFinite(output.Pitch, selection.Pitch), 0.1f, 3f);
                selection.DopplerRatio = math.clamp(
                    SanitizeFinite(output.DopplerRatio, selection.DopplerRatio),
                    VirtualVoiceUtility.MinimumDopplerRatio,
                    VirtualVoiceUtility.MaximumDopplerRatio);
                selection.SabineRt60Seconds = math.clamp(
                    SanitizeFinite(output.ReverbRt60Seconds, selection.SabineRt60Seconds),
                    VirtualVoiceUtility.SabineMinimumRt60Seconds,
                    VirtualVoiceUtility.SabineMaximumRt60Seconds);
                selection.LowPassCutoffHz = math.clamp(
                    SanitizeFinite(output.LowPassHertz, selection.LowPassCutoffHz),
                    80f,
                    VirtualVoiceUtility.OpenLowPassHertz);
                selection.DelaySeconds = math.max(0f, SanitizeFinite(output.DelaySeconds, selection.DelaySeconds));
                if (output.Occlusion01 > 0.5f)
                    selection.DspFlags |= VirtualVoiceDspFlags.SdfOccluded;
                return;
            }
        }

        private void StartOrUpdateVirtualPhysicalVoice(int channel, in VirtualVoiceSelection selection)
        {
            int sourceIndex = ResolveVirtualChannelSourceIndex(channel);
            if (sourceIndex >= 0 &&
                _pool != null &&
                sourceIndex < _pool.Length &&
                _pool[sourceIndex] != null &&
                _pool[sourceIndex].clip != null &&
                _pool[sourceIndex].isPlaying &&
                _virtualChannelStableKeys[channel] == selection.StableKey)
            {
                UpdateVirtualPhysicalVoice(channel, sourceIndex, in selection);
                return;
            }

            StartVirtualPhysicalVoice(channel, in selection, sourceIndex);
        }

        private void StartVirtualPhysicalVoice(int channel, in VirtualVoiceSelection selection, int preferredSourceIndex)
        {
            bool played = preferredSourceIndex >= 0 &&
                TryPlayVirtualSelectionOnSource(channel, preferredSourceIndex, in selection);
            if (!played)
            {
                AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in selection.SourceAup);
                Vector3 runtimePosition = ToRuntimeVector3(in sourceAup);
                Vector3 absolutePosition = ToAbsoluteVector3(in sourceAup);
                int sourceIndex = PlayAtPointResolved(
                    TryResolveAudioEventClip(selection.EventID, selection.ClipHash, out AudioClip clip) ? clip : null,
                    runtimePosition,
                    in sourceAup,
                    absolutePosition,
                    ResolveVirtualSelectionVolume(in selection),
                    selection.Pitch,
                    ResolvedDefaultWorldMixerGroup,
                    selection.StationaryCacheKey,
                    selection.DopplerRatio);
                played = sourceIndex >= 0;
                preferredSourceIndex = sourceIndex;
            }

            if (!played)
            {
                ClearVirtualChannel(channel);
                return;
            }

            ClearVirtualChannelOwningSource(preferredSourceIndex, channel);
            _virtualChannelStableKeys[channel] = selection.StableKey;
            _virtualChannelSourceIndices[channel] = preferredSourceIndex;
            _virtualChannelPendingFlags[channel] = 0;
            _virtualChannelFadeRemaining[channel] = 0f;
        }

        private bool TryPlayVirtualSelectionOnSource(int channel, int sourceIndex, in VirtualVoiceSelection selection)
        {
            if (!TryResolveAudioEventClip(selection.EventID, selection.ClipHash, out AudioClip clip) ||
                _pool == null ||
                sourceIndex < 0 ||
                sourceIndex >= _pool.Length ||
                _pool[sourceIndex] == null)
            {
                return false;
            }

            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in selection.SourceAup);
            Vector3 runtimePosition = ToRuntimeVector3(in sourceAup);
            Vector3 sourceAbsolutePosition = ToAbsoluteVector3(in sourceAup);
            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAbsolutePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            if (hasListener)
                runtimePosition = ToListenerRelativeRuntimeVector3(in sourceAup, true, in listenerAup, listenerRuntimePosition);
            Vector3 audiblePosition = runtimePosition;
            Vector3 audibleAbsolutePosition = sourceAbsolutePosition;
            AbsoluteUniversePosition audibleAup = sourceAup;
            AcousticPathResult acousticPortalResult = default;
            bool hasAcousticPortalPath = hasListener &&
                TryResolveAcousticPortalPath(
                    runtimePosition,
                    listenerRuntimePosition,
                    listenerRight,
                    in sourceAup,
                    in listenerAup,
                    selection.StationaryCacheKey,
                    out acousticPortalResult);
            if (hasAcousticPortalPath)
            {
                audibleAup = ToAbsoluteUniversePosition(in acousticPortalResult.LastPortalAup);
                audiblePosition = ToListenerRelativeRuntimeVector3(in audibleAup, true, in listenerAup, listenerRuntimePosition);
                audibleAbsolutePosition = ToAbsoluteVector3(in audibleAup);
                PublishAcousticEchoPortalTap(
                    in sourceAup,
                    in acousticPortalResult,
                    selection.Volume,
                    selection.StationaryCacheKey,
                    selection.EventID);
            }

            if (hasListener && IsBeyondMaxHearingRange(in audibleAup, in listenerAup))
                return false;

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in audibleAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return false;

            byte voiceCategory = ResolveAudioVoiceCategory(clip);
            if (!TryReserveAudioVoiceCategory(voiceCategory, sourceIndex))
                return false;

            AudioSource source = _pool[sourceIndex];
            ResetWorldSourceState(sourceIndex, true);
            AssignAudioVoiceCategory(sourceIndex, voiceCategory);
            source.enabled = true;
            source.transform.position = audiblePosition;
            AudioResidencyCache.TouchClip(clip, ResolveWorldResidencyDomain(clip, ResolvedDefaultWorldMixerGroup), true);
            source.clip = clip;
            float clampedVolume = ResolveVirtualSelectionVolume(in selection);
            if (hasAcousticPortalPath)
                clampedVolume *= acousticPortalResult.Transmission01;
            source.volume = clampedVolume;
            float clampedPitch = math.clamp(selection.Pitch, 0.1f, 3f);
            _baseVolumes[sourceIndex] = clampedVolume;
            _basePitches[sourceIndex] = clampedPitch;
            source.outputAudioMixerGroup = ResolveWorldMixerGroup(clip, ResolvedDefaultWorldMixerGroup);
            CacheWorldSourceBusFlags(sourceIndex, source.outputAudioMixerGroup);
            float dopplerRatio = math.clamp(
                selection.DopplerRatio,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && sourceIndex < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[sourceIndex] = dopplerRatio;
            source.pitch = ResolveSourcePitch(sourceIndex, dopplerRatio);
            _audioLodTiers[sourceIndex] = lodTier;
            float now = Time.unscaledTime;
            int currentFrame = Time.frameCount;
            UpdateWorldSourceAudioLod(
                sourceIndex,
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
                ApplyAcousticPortalPresentation(sourceIndex, source, in acousticPortalResult);
            ApplyVirtualSelectionDspPresentation(sourceIndex, source, in selection);
            ApplyHaasMask(sourceIndex, in audibleAup, hasListener, in listenerAup, now);
            source.spatialBlend = ResolveTargetSpatialBlend(sourceIndex, now);
            PlayAcousticSource(
                source,
                hasAcousticPortalPath
                    ? math.max(acousticPortalResult.DelaySeconds, selection.DelaySeconds)
                    : selection.DelaySeconds);
            PublishVirtualSelectionAcousticPing(in audibleAup, in selection, clampedVolume);
            _startTimes[sourceIndex] = now;
            CacheActiveWorldRuntimePosition(sourceIndex, audiblePosition, currentFrame);
            CacheActiveWorldAup(sourceIndex, in audibleAup, currentFrame);
            MarkWorldSourceActive(sourceIndex);
            return true;
        }

        private static void PublishVirtualSelectionAcousticPing(
            in AbsoluteUniversePosition audibleAup,
            in VirtualVoiceSelection selection,
            float volume01)
        {
            float intensity = math.saturate(volume01);
            if (intensity < 0.35f)
                return;

            var ping = new AcousticPingSignal
            {
                PositionAup = audibleAup,
                RadiusMeters = math.clamp(32f + intensity * 256f, 32f, 512f),
                Intensity01 = intensity,
                SourceId = selection.SourceEntityID != 0u ? selection.SourceEntityID : selection.EventID,
                Channel = AcousticPingSignal.ChannelMetalStress,
                Flags = 0
            };
            GlobalSignals.Publish(in ping);
        }

        private void UpdateVirtualPhysicalVoice(int channel, int sourceIndex, in VirtualVoiceSelection selection)
        {
            if (!TryResolveAudioEventClip(selection.EventID, selection.ClipHash, out AudioClip clip) ||
                _pool == null ||
                sourceIndex < 0 ||
                sourceIndex >= _pool.Length)
            {
                ClearVirtualChannel(channel);
                return;
            }

            AudioSource source = _pool[sourceIndex];
            if (source == null)
            {
                ClearVirtualChannel(channel);
                return;
            }

            AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in selection.SourceAup);
            Vector3 runtimePosition = ToRuntimeVector3(in sourceAup);
            if (TryResolveListenerFrame(
                    out _,
                    out Vector3 listenerRuntimePosition,
                    out _,
                    out AbsoluteUniversePosition listenerAup))
            {
                runtimePosition = ToListenerRelativeRuntimeVector3(in sourceAup, true, in listenerAup, listenerRuntimePosition);
            }
            source.transform.position = runtimePosition;
            if (source.clip != clip)
            {
                StartVirtualPhysicalVoice(channel, in selection, sourceIndex);
                return;
            }

            float clampedVolume = ResolveVirtualSelectionVolume(in selection);
            source.volume = clampedVolume;
            if (_baseVolumes != null && sourceIndex < _baseVolumes.Length)
                _baseVolumes[sourceIndex] = clampedVolume;

            float clampedPitch = math.clamp(selection.Pitch, 0.1f, 3f);
            if (_basePitches != null && sourceIndex < _basePitches.Length)
                _basePitches[sourceIndex] = clampedPitch;

            float dopplerRatio = math.clamp(
                selection.DopplerRatio,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && sourceIndex < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[sourceIndex] = dopplerRatio;
            source.pitch = ResolveSourcePitch(sourceIndex, dopplerRatio);
            ApplyVirtualSelectionDspPresentation(sourceIndex, source, in selection);
            int currentFrame = Time.frameCount;
            CacheActiveWorldRuntimePosition(sourceIndex, runtimePosition, currentFrame);
            CacheActiveWorldAup(sourceIndex, in sourceAup, currentFrame);
            _virtualChannelSourceIndices[channel] = sourceIndex;
            _virtualChannelStableKeys[channel] = selection.StableKey;
        }

        private static float ResolveVirtualSelectionVolume(in VirtualVoiceSelection selection)
        {
            float effective = SanitizeFinite(selection.EffectiveVolume, 0f);
            if (effective > 0f)
                return math.saturate(effective);

            return math.saturate(SanitizeFinite(selection.Volume, 0f));
        }

        private void ApplyVirtualSelectionDspPresentation(
            int sourceIndex,
            AudioSource source,
            in VirtualVoiceSelection selection)
        {
            if (source == null)
                return;

            float cutoff = math.clamp(
                SanitizeFinite(selection.LowPassCutoffHz, VirtualVoiceUtility.OpenLowPassHertz),
                AcousticOcclusionUtility.MinimumLowPassCutoffHertz,
                VirtualVoiceUtility.OpenLowPassHertz);
            if (_lowPassFilters != null && sourceIndex >= 0 && sourceIndex < _lowPassFilters.Length)
            {
                AudioLowPassFilter lowPass = _lowPassFilters[sourceIndex];
                if (lowPass != null && lowPass.enabled)
                    cutoff = math.min(cutoff, lowPass.cutoffFrequency);
            }

            if (cutoff < VirtualVoiceUtility.OpenLowPassHertz - 1f)
                ApplyLowPassFilter(sourceIndex, true, cutoff);

            float rt60 = SanitizeFinite(selection.SabineRt60Seconds, 0f);
            if (rt60 > 0f)
            {
                float reverbMix = math.clamp(0.08f + rt60 * 0.065f, 0f, math.lerp(0.45f, 1f, _virtualVoiceQualityWeight));
                source.reverbZoneMix = math.max(source.reverbZoneMix, reverbMix);
            }
        }

        private void BeginVirtualChannelSteal(int channel, in VirtualVoiceSelection nextSelection)
        {
            if (_virtualChannelPendingFlags == null ||
                channel < 0 ||
                channel >= _virtualChannelPendingFlags.Length)
            {
                return;
            }

            if (_virtualChannelPendingFlags[channel] != 0 &&
                _virtualChannelPendingSelections[channel].StableKey == nextSelection.StableKey)
            {
                return;
            }

            _virtualChannelPendingSelections[channel] = nextSelection;
            _virtualChannelPendingFlags[channel] = 1;
            _virtualChannelFadeRemaining[channel] = VirtualVoiceStealFadeSeconds;
            int sourceIndex = ResolveVirtualChannelSourceIndex(channel);
            AudioSource source = sourceIndex >= 0 && _pool != null && sourceIndex < _pool.Length
                ? _pool[sourceIndex]
                : null;
            _virtualChannelFadeStartVolumes[channel] = source != null ? math.max(0f, source.volume) : 0f;
        }

        private void BeginVirtualChannelFadeToSilence(int channel)
        {
            if (_virtualChannelPendingSelections == null ||
                channel < 0 ||
                channel >= _virtualChannelPendingSelections.Length ||
                _virtualChannelStableKeys[channel] == 0u)
            {
                return;
            }

            _virtualChannelPendingSelections[channel] = default;
            _virtualChannelPendingFlags[channel] = 1;
            _virtualChannelFadeRemaining[channel] = VirtualVoiceStealFadeSeconds;
            int sourceIndex = ResolveVirtualChannelSourceIndex(channel);
            AudioSource source = sourceIndex >= 0 && _pool != null && sourceIndex < _pool.Length
                ? _pool[sourceIndex]
                : null;
            _virtualChannelFadeStartVolumes[channel] = source != null ? math.max(0f, source.volume) : 0f;
        }

        private void AdvanceVirtualVoiceStealFades(float deltaTime)
        {
            if (_virtualChannelPendingFlags == null)
                return;

            float safeDelta = math.max(0f, deltaTime);
            for (int channel = 0; channel < _virtualChannelPendingFlags.Length; channel++)
            {
                if (_virtualChannelPendingFlags[channel] == 0)
                    continue;

                int sourceIndex = ResolveVirtualChannelSourceIndex(channel);
                AudioSource source = sourceIndex >= 0 && _pool != null && sourceIndex < _pool.Length
                    ? _pool[sourceIndex]
                    : null;
                float remaining = math.max(0f, _virtualChannelFadeRemaining[channel] - safeDelta);
                _virtualChannelFadeRemaining[channel] = remaining;
                if (source != null && source.isPlaying)
                {
                    float fade01 = VirtualVoiceStealFadeSeconds > 0f
                        ? math.saturate(remaining * math.rcp(VirtualVoiceStealFadeSeconds))
                        : 0f;
                    source.volume = _virtualChannelFadeStartVolumes[channel] * fade01;
                    if (remaining > 0f)
                        continue;

                    source.Stop();
                    ResetWorldSourceState(sourceIndex, true);
                }

                VirtualVoiceSelection pendingSelection = _virtualChannelPendingSelections[channel];
                if (pendingSelection.StableKey != 0u)
                    StartVirtualPhysicalVoice(channel, in pendingSelection, sourceIndex);
                else
                    ClearVirtualChannel(channel);
            }
        }

        private int ResolveVirtualChannelSourceIndex(int channel)
        {
            if (_virtualChannelSourceIndices == null ||
                channel < 0 ||
                channel >= _virtualChannelSourceIndices.Length)
            {
                return -1;
            }

            return _virtualChannelSourceIndices[channel];
        }

        private int FindVirtualChannelByStableKey(uint stableKey, int limit)
        {
            if (stableKey == 0u || _virtualChannelStableKeys == null)
                return -1;

            int safeLimit = math.min(limit, _virtualChannelStableKeys.Length);
            for (int channel = 0; channel < safeLimit; channel++)
            {
                if (_virtualChannelStableKeys[channel] == stableKey)
                    return channel;
            }

            return -1;
        }

        private int FindVirtualPendingChannelByStableKey(uint stableKey, int limit)
        {
            if (stableKey == 0u ||
                _virtualChannelPendingSelections == null ||
                _virtualChannelPendingFlags == null)
            {
                return -1;
            }

            int safeLimit = math.min(limit, _virtualChannelPendingSelections.Length);
            safeLimit = math.min(safeLimit, _virtualChannelPendingFlags.Length);
            for (int channel = 0; channel < safeLimit; channel++)
            {
                if (_virtualChannelPendingFlags[channel] != 0 &&
                    _virtualChannelPendingSelections[channel].StableKey == stableKey)
                {
                    return channel;
                }
            }

            return -1;
        }

        private int FindFreeVirtualChannel(int limit)
        {
            if (_virtualChannelStableKeys == null)
                return -1;

            int safeLimit = math.min(limit, _virtualChannelStableKeys.Length);
            for (int channel = 0; channel < safeLimit; channel++)
            {
                if (_virtualChannelStableKeys[channel] == 0u &&
                    (_virtualChannelPendingFlags == null || _virtualChannelPendingFlags[channel] == 0))
                {
                    return channel;
                }
            }

            return -1;
        }

        private bool IsVirtualStableKeySelected(uint stableKey, int selectedCount)
        {
            if (stableKey == 0u || !_virtualVoiceSelections.IsCreated)
                return false;

            int safeCount = math.min(selectedCount, _virtualVoiceSelections.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (_virtualVoiceSelections[i].StableKey == stableKey)
                    return true;
            }

            return false;
        }

        private void ClearVirtualChannelOwningSource(int sourceIndex, int exceptChannel)
        {
            if (_virtualChannelSourceIndices == null)
                return;

            for (int channel = 0; channel < _virtualChannelSourceIndices.Length; channel++)
            {
                if (channel == exceptChannel)
                    continue;

                if (_virtualChannelSourceIndices[channel] == sourceIndex)
                    ClearVirtualChannel(channel);
            }
        }

        private void ClearVirtualChannel(int channel)
        {
            if (_virtualChannelStableKeys == null ||
                channel < 0 ||
                channel >= _virtualChannelStableKeys.Length)
            {
                return;
            }

            _virtualChannelStableKeys[channel] = 0u;
            if (_virtualChannelSourceIndices != null && channel < _virtualChannelSourceIndices.Length)
                _virtualChannelSourceIndices[channel] = -1;
            if (_virtualChannelPendingSelections != null && channel < _virtualChannelPendingSelections.Length)
                _virtualChannelPendingSelections[channel] = default;
            if (_virtualChannelFadeRemaining != null && channel < _virtualChannelFadeRemaining.Length)
                _virtualChannelFadeRemaining[channel] = 0f;
            if (_virtualChannelFadeStartVolumes != null && channel < _virtualChannelFadeStartVolumes.Length)
                _virtualChannelFadeStartVolumes[channel] = 0f;
            if (_virtualChannelPendingFlags != null && channel < _virtualChannelPendingFlags.Length)
                _virtualChannelPendingFlags[channel] = 0;
        }

        private void ClearVirtualVoiceQueues()
        {
            CompleteVirtualVoiceSort();
            _virtualVoiceWriteCount = 0;
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _acousticOcclusionOutputCount = 0;
            ResetVirtualVoiceSelections();
            _virtualVoiceDroppedCount = 0;
            _lastVirtualVoiceStatistics = default;
            if (_virtualChannelStableKeys == null)
                return;

            for (int channel = 0; channel < _virtualChannelStableKeys.Length; channel++)
                ClearVirtualChannel(channel);
        }

        private void ResetVirtualVoiceSelections()
        {
            if (!_virtualVoiceSelections.IsCreated)
                return;

            for (int i = 0; i < _virtualVoiceSelections.Length; i++)
                _virtualVoiceSelections[i] = default;
        }

        private void EnsureVirtualChannelArrays()
        {
            bool createdSourceIndices = _virtualChannelSourceIndices == null ||
                _virtualChannelSourceIndices.Length != MaxVirtualPhysicalVoices;
            if (_virtualChannelStableKeys == null || _virtualChannelStableKeys.Length != MaxVirtualPhysicalVoices)
                _virtualChannelStableKeys = new uint[MaxVirtualPhysicalVoices]; // COLD ALLOC: uint[64] - stable virtual voice key per physical channel - owner: SpatialAudioManager
            if (createdSourceIndices)
                _virtualChannelSourceIndices = new int[MaxVirtualPhysicalVoices]; // COLD ALLOC: int[64] - AudioSource pool index per virtual channel - owner: SpatialAudioManager
            if (_virtualChannelPendingSelections == null || _virtualChannelPendingSelections.Length != MaxVirtualPhysicalVoices)
                _virtualChannelPendingSelections = new VirtualVoiceSelection[MaxVirtualPhysicalVoices]; // COLD ALLOC: VirtualVoiceSelection[64] - deferred post-fade PCM injection payloads - owner: SpatialAudioManager
            if (_virtualChannelFadeRemaining == null || _virtualChannelFadeRemaining.Length != MaxVirtualPhysicalVoices)
                _virtualChannelFadeRemaining = new float[MaxVirtualPhysicalVoices]; // COLD ALLOC: float[64] - 10ms steal fade countdowns - owner: SpatialAudioManager
            if (_virtualChannelFadeStartVolumes == null || _virtualChannelFadeStartVolumes.Length != MaxVirtualPhysicalVoices)
                _virtualChannelFadeStartVolumes = new float[MaxVirtualPhysicalVoices]; // COLD ALLOC: float[64] - source volume captured at steal fade start - owner: SpatialAudioManager
            if (_virtualChannelPendingFlags == null || _virtualChannelPendingFlags.Length != MaxVirtualPhysicalVoices)
                _virtualChannelPendingFlags = new byte[MaxVirtualPhysicalVoices]; // COLD ALLOC: byte[64] - pending virtual channel fade/injection flags - owner: SpatialAudioManager

            if (createdSourceIndices)
            {
                for (int i = 0; i < _virtualChannelSourceIndices.Length; i++)
                    _virtualChannelSourceIndices[i] = -1;
            }
        }

        private void RebaseVirtualVoicePool(
            NativeArray<VirtualVoice> voices,
            int count,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ)
        {
            if (!voices.IsCreated)
                return;

            int safeCount = math.clamp(count, 0, voices.Length);
            for (int i = 0; i < safeCount; i++)
            {
                VirtualVoice voice = voices[i];
                voice.SourceAup.GridX += gridDeltaX;
                voice.SourceAup.GridY += gridDeltaY;
                voice.SourceAup.GridZ += gridDeltaZ;
                voices[i] = voice;
            }
        }

        private void RebaseAcousticSourcePool(
            NativeArray<AcousticSourceDTO> sources,
            NativeArray<double3> previousAup,
            int count,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ)
        {
            if (!sources.IsCreated)
                return;

            double cellSize = AcousticAup.CellSizeMeters;
            double3 deltaMeters = new double3(gridDeltaX * cellSize, gridDeltaY * cellSize, gridDeltaZ * cellSize);
            int safeCount = math.clamp(count, 0, sources.Length);
            for (int i = 0; i < safeCount; i++)
            {
                AcousticSourceDTO source = sources[i];
                source.AUP_Position += deltaMeters;
                sources[i] = source;
            }

            if (!previousAup.IsCreated)
                return;

            safeCount = math.min(safeCount, previousAup.Length);
            for (int i = 0; i < safeCount; i++)
                previousAup[i] += deltaMeters;
        }

        private void RebaseVirtualVoiceDtoPool(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            if (!_virtualVoiceDtoPool.IsCreated)
                return;

            double cellSize = AcousticAup.CellSizeMeters;
            double3 deltaMeters = new double3(gridDeltaX * cellSize, gridDeltaY * cellSize, gridDeltaZ * cellSize);
            int safeCount = math.clamp(_virtualVoiceDtoCount, 0, _virtualVoiceDtoPool.Length);
            for (int i = 0; i < safeCount; i++)
            {
                VirtualVoiceDTO dto = _virtualVoiceDtoPool[i];
                dto.AupMeters += deltaMeters;
                _virtualVoiceDtoPool[i] = dto;
            }
        }

        private void RebaseVirtualVoiceSelections(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            if (!_virtualVoiceSelections.IsCreated)
                return;

            for (int i = 0; i < _virtualVoiceSelections.Length; i++)
            {
                VirtualVoiceSelection selection = _virtualVoiceSelections[i];
                if (selection.StableKey == 0u)
                    continue;

                selection.SourceAup.GridX += gridDeltaX;
                selection.SourceAup.GridY += gridDeltaY;
                selection.SourceAup.GridZ += gridDeltaZ;
                _virtualVoiceSelections[i] = selection;
            }
        }

        private void RebaseVirtualChannelPendingSelections(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            if (_virtualChannelPendingSelections == null)
                return;

            for (int channel = 0; channel < _virtualChannelPendingSelections.Length; channel++)
            {
                if (_virtualChannelPendingFlags != null &&
                    channel < _virtualChannelPendingFlags.Length &&
                    _virtualChannelPendingFlags[channel] == 0)
                {
                    continue;
                }

                VirtualVoiceSelection selection = _virtualChannelPendingSelections[channel];
                if (selection.StableKey == 0u)
                    continue;

                selection.SourceAup.GridX += gridDeltaX;
                selection.SourceAup.GridY += gridDeltaY;
                selection.SourceAup.GridZ += gridDeltaZ;
                _virtualChannelPendingSelections[channel] = selection;
            }
        }

        private byte ResolveVirtualVoiceFoveatedTier(Vector3 runtimePosition)
        {
            IFoveatedSimulationDirector director = _foveatedSimulationDirector;
            byte tier = director != null
                ? (byte)director.ResolveTierForPosition(runtimePosition)
                : (byte)FoveatedSimulationTier.Active;
            if (tier >= VirtualVoiceUtility.FoveatedTierFrozen)
                EvictCreatureAudioBanksForFrozenTier();

            return tier;
        }

        private float ResolveVirtualVoicePriority(float volume, AcousticPortalFlags flags, byte foveatedTier)
        {
            if (foveatedTier >= VirtualVoiceUtility.FoveatedTierFrozen)
                return 0f;

            float priority = math.max(0.001f, math.saturate(SanitizeFinite(volume, 0f)));
            if ((flags & AcousticPortalFlags.SealedBulkhead) != 0)
                priority *= 0.75f;
            if ((flags & AcousticPortalFlags.Solid) != 0)
                priority *= 0.5f;
            return priority;
        }

        private static VirtualVoicePortalFlags ToVirtualVoicePortalFlags(AcousticPortalFlags flags)
        {
            return (VirtualVoicePortalFlags)(byte)flags;
        }

        private void RefreshFoveatedDirector()
        {
            ResolveFoveatedSimulationDirector();
        }

        private IFoveatedSimulationDirector ResolveFoveatedSimulationDirector()
        {
            int frame = Time.frameCount;
            IFoveatedSimulationDirector director = _foveatedSimulationDirector;
            if (director != null && frame < _foveatedDirectorResolveFrame)
                return director;

            if (frame < _foveatedDirectorResolveFrame)
                return director;

            _foveatedDirectorResolveFrame = frame + SpatialAudioRegistryRetryFrames;
            IFoveatedSimulationDirector resolvedDirector = GlobalRegistry.FoveatedSimulationDirector;
            if (resolvedDirector != null || director == null)
                _foveatedSimulationDirector = resolvedDirector;

            return _foveatedSimulationDirector;
        }

        private void EnsureSpatialAudioPolicyCached()
        {
            int frame = Time.frameCount;
            if (_spatialAudioPolicyRefreshFrame == frame)
                return;

            CacheSpatialAudioPolicy(ResolveGlobalSpatialAudioQualityWeight01(), frame);
        }

        private void RefreshSpatialAudioPolicyCold()
        {
            CacheSpatialAudioPolicy(ResolveGlobalSpatialAudioQualityWeight01(), Time.frameCount);
        }

        private void RefreshCachedAudioRuntimeServicesCold()
        {
            int nextResolveFrame = Time.frameCount + SpatialAudioRegistryRetryFrames;
            IPlayerRuntimeContext playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            IWeatherService weatherService = GlobalRegistry.Weather;

            _cachedPlayerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _cachedWeatherService = weatherService != null && weatherService.IsInitialized ? weatherService : null;
            _cachedAcousticZone = GlobalRegistry.AcousticZone;
            _cachedSurfaceWeatherDirector = GlobalRegistry.SurfaceWeather;
            _cachedPlayerCriticalAudio = GlobalRegistry.PlayerCriticalAudio;
            _cachedConstructionManager = GlobalRegistry.ConstructionRuntime;
            _foveatedSimulationDirector = GlobalRegistry.FoveatedSimulationDirector;
            _dataVault = GlobalRegistry.DataVault;
            _playerRuntimeContextResolveFrame = nextResolveFrame;
            _weatherServiceResolveFrame = nextResolveFrame;
            _acousticZoneResolveFrame = nextResolveFrame;
            _surfaceWeatherResolveFrame = nextResolveFrame;
            _foveatedDirectorResolveFrame = nextResolveFrame;
        }

        private void CacheReboundAudioRuntimeService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            int nextResolveFrame = Time.frameCount + SpatialAudioRegistryRetryFrames;
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                    _cachedPlayerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
                    _playerRuntimeContextResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    IWeatherService weatherService = currentService as IWeatherService;
                    _cachedWeatherService = weatherService != null && weatherService.IsInitialized ? weatherService : null;
                    _weatherServiceResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.AcousticZoneRuntime:
                    _cachedAcousticZone = currentService as AcousticZoneController;
                    _acousticZoneResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.SurfaceWeatherRuntime:
                    _cachedSurfaceWeatherDirector = currentService as HectonSurfaceWeatherDirector;
                    _surfaceWeatherResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    _cachedPlayerCriticalAudio = currentService as PlayerCriticalProceduralAudioRenderer;
                    break;
                case GlobalRegistryServiceSlot.Logistics:
                    _cachedConstructionManager = currentService as ConstructionManager;
                    break;
                case GlobalRegistryServiceSlot.FoveatedSimulationDirector:
                    _foveatedSimulationDirector = currentService as IFoveatedSimulationDirector;
                    _foveatedDirectorResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    IDataVault dataVault = currentService as IDataVault;
                    if (!ReferenceEquals(_dataVault, dataVault))
                    {
                        _dataVault = dataVault;
                        ClearVaultBackedTelemetryAliases();
                        if (_runtimeResourcesInitialized)
                            InitializeTelemetryCaches();
                    }
                    break;
            }
        }

        private void CacheSpatialAudioPolicy(float qualityWeight01, int frame)
        {
            _cachedSpatialAudioQualityWeight01 = SanitizeQuality01(qualityWeight01);
            _spatialAudioPolicyRefreshFrame = frame;
        }

        private void ApplyAmbientOutputSampleRatePolicy()
        {
            if (!Application.isPlaying)
                return;

            EnsureSpatialAudioPolicyCached();
            bool reducedSampleRate =
                _cachedSpatialAudioQualityWeight01 <= 0.28f ||
                HardwareTierDetector.IsQuest3Like ||
                QuestVulkanRuntimePolicy.IsQuestRuntimeActive;
            if (!reducedSampleRate)
                return;

            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            if (configuration.sampleRate > 0 && configuration.sampleRate <= LowTierAmbientOutputSampleRate)
            {
                _lastAudioOutputSampleRate = configuration.sampleRate;
                return;
            }

            configuration.sampleRate = LowTierAmbientOutputSampleRate;
            if (AudioSettings.Reset(configuration))
                _lastAudioOutputSampleRate = LowTierAmbientOutputSampleRate;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private static float ResolveGlobalSpatialAudioQualityWeight01()
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return SanitizeQuality01(quality);
        }

        private static float SanitizeQuality01(float quality)
        {
            return math.saturate(math.select(1f, quality, math.isfinite(quality)));
        }

        private static float SmoothQuality01(float quality)
        {
            float q = SanitizeQuality01(quality);
            return q * q * (3f - 2f * q);
        }

        private IPlayerRuntimeContext ResolvePlayerRuntimeContext()
        {
            int frame = Time.frameCount;
            IPlayerRuntimeContext playerContext = _cachedPlayerRuntimeContext;
            if (playerContext != null && playerContext.IsInitialized && frame < _playerRuntimeContextResolveFrame)
                return playerContext;

            if (frame < _playerRuntimeContextResolveFrame)
                return null;

            _playerRuntimeContextResolveFrame = frame + SpatialAudioRegistryRetryFrames;
            playerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            _cachedPlayerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            return _cachedPlayerRuntimeContext;
        }

        private IWeatherService ResolveWeatherService()
        {
            int frame = Time.frameCount;
            IWeatherService weatherService = _cachedWeatherService;
            if (weatherService != null && weatherService.IsInitialized && frame < _weatherServiceResolveFrame)
                return weatherService;

            if (frame < _weatherServiceResolveFrame)
                return null;

            _weatherServiceResolveFrame = frame + SpatialAudioRegistryRetryFrames;
            weatherService = GlobalRegistry.Weather;
            _cachedWeatherService = weatherService != null && weatherService.IsInitialized ? weatherService : null;
            return _cachedWeatherService;
        }

        private AcousticZoneController ResolveAcousticZone()
        {
            int frame = Time.frameCount;
            AcousticZoneController acousticZone = _cachedAcousticZone;
            if (acousticZone != null && frame < _acousticZoneResolveFrame)
                return acousticZone;

            if (frame < _acousticZoneResolveFrame)
                return null;

            _acousticZoneResolveFrame = frame + SpatialAudioRegistryRetryFrames;
            _cachedAcousticZone = GlobalRegistry.AcousticZone;
            return _cachedAcousticZone;
        }

        private HectonSurfaceWeatherDirector ResolveSurfaceWeatherDirector()
        {
            int frame = Time.frameCount;
            HectonSurfaceWeatherDirector surfaceWeather = _cachedSurfaceWeatherDirector;
            if (surfaceWeather != null && frame < _surfaceWeatherResolveFrame)
                return surfaceWeather;

            if (frame < _surfaceWeatherResolveFrame)
                return null;

            _surfaceWeatherResolveFrame = frame + SpatialAudioRegistryRetryFrames;
            _cachedSurfaceWeatherDirector = GlobalRegistry.SurfaceWeather;
            return _cachedSurfaceWeatherDirector;
        }

        private float ResolveVirtualVoiceQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            if (_virtualVoiceScalabilityState.IsCreated && _virtualVoiceScalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = _virtualVoiceScalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                    weight = state.GlobalQualityWeight;
            }

            EnsureSpatialAudioPolicyCached();
            weight = math.min(weight, _cachedSpatialAudioQualityWeight01);
            return SmoothQuality01(weight);
        }

        private bool ResolveRollbackAudioSuppressionActive()
        {
            if (!_virtualVoiceRollbackAudioSuppression.IsCreated ||
                _virtualVoiceRollbackAudioSuppression.Length <= 0)
            {
                return false;
            }

            RollbackAudioSuppressionDTO audio = _virtualVoiceRollbackAudioSuppression[0];
            uint frame = (uint)Time.frameCount;
            return audio.IsResimulating != 0u || frame <= audio.UntilFrame;
        }

        private void RefreshVirtualPhysicalVoiceLimit(bool immediate)
        {
            RefreshVirtualExternalStateAliases();
            ApplyVirtualPhysicalVoiceLimitTarget(ResolveVirtualVoiceQualityWeight(), immediate);
        }

        private void ApplyVirtualPhysicalVoiceLimitTarget(float qualityWeight, bool immediate)
        {
            float targetWeight = math.saturate(SanitizeFinite(qualityWeight, 1f));
            if (immediate)
            {
                ApplyVirtualVoiceQualityWeight(targetWeight);
                return;
            }

            int targetBudget = VirtualVoiceUtility.ResolveContinuousVoiceBudget(targetWeight);
            if (math.abs(targetWeight - _virtualVoiceQualityWeight) <= 0.015f &&
                targetBudget == _virtualPhysicalVoiceLimit)
            {
                _virtualVoiceTierPendingSlowTicks = 0;
                return;
            }

            bool targetSurvival = targetWeight <= 0.18f;
            if (targetSurvival != _virtualVoiceLowTierTarget)
            {
                _virtualVoiceLowTierTarget = targetSurvival;
                _virtualVoiceTierPendingSlowTicks = 0;
                return;
            }

            _virtualVoiceTierPendingSlowTicks++;
            if (_virtualVoiceTierPendingSlowTicks >= VirtualVoiceTierHysteresisSlowTicks)
            {
                ApplyVirtualVoiceQualityWeight(targetWeight);
                return;
            }

            float blend = math.saturate(_virtualVoiceTierPendingSlowTicks * math.rcp(math.max(1f, VirtualVoiceTierHysteresisSlowTicks)));
            float smoothed = math.lerp(_virtualVoiceQualityWeight, targetWeight, blend);
            _virtualVoiceQualityWeight = smoothed;
            _virtualPhysicalVoiceLimit = VirtualVoiceUtility.ResolveContinuousVoiceBudget(smoothed);
            _virtualVoiceLowTierApplied = smoothed <= 0.18f;
        }

        private void ApplyVirtualVoiceQualityWeight(float qualityWeight)
        {
            float sanitized = math.saturate(SanitizeFinite(qualityWeight, 1f));
            _virtualVoiceQualityWeight = sanitized;
            _virtualPhysicalVoiceLimit = VirtualVoiceUtility.ResolveContinuousVoiceBudget(sanitized);
            bool survival = sanitized <= 0.18f;
            _virtualVoiceLowTierTarget = survival;
            _virtualVoiceLowTierApplied = survival;
            _virtualVoiceTierPendingSlowTicks = 0;
        }

        private void PushVirtualVoiceTelemetry(in VirtualVoiceStatistics statistics)
        {
            if (!_virtualVoiceBlackBox.IsCreated || _virtualVoiceBlackBox.Length == 0)
                return;

            float loudestWeight = 0f;
            if (_virtualVoiceSelections.IsCreated && statistics.ActivePhysicalVoices > 0)
                loudestWeight = _virtualVoiceSelections[0].Weight;
            if (statistics.LoudestWeight > loudestWeight)
                loudestWeight = statistics.LoudestWeight;
            uint stateHash = ComputeVirtualVoiceStateHash(in statistics, loudestWeight);
            int index = _virtualVoiceBlackBoxCursor % _virtualVoiceBlackBox.Length;
            _virtualVoiceBlackBox[index] = new AcousticOcclusionTelemetryEntry
            {
                Frame = statistics.Frame,
                TotalVoices = (ushort)math.clamp(statistics.TotalVoices, 0, ushort.MaxValue),
                AudibleVoices = (ushort)math.clamp(statistics.AudibleVoices, 0, ushort.MaxValue),
                CulledVoices = (ushort)math.clamp(statistics.CulledVoices, 0, ushort.MaxValue),
                ActiveVoices = (ushort)math.clamp(statistics.ActivePhysicalVoices, 0, ushort.MaxValue),
                PhysicalVoiceLimit = (ushort)math.clamp(statistics.PhysicalVoiceLimit, 0, ushort.MaxValue),
                StolenVoices = (ushort)math.clamp(statistics.StolenVoices, 0, ushort.MaxValue),
                DroppedVoices = (ushort)math.clamp(statistics.DroppedVoices, 0, ushort.MaxValue),
                Flags = (ushort)((_hasVirtualListenerAup ? 1 : 0) | (_virtualVoiceLowTierApplied ? 2 : 0)),
                OccludedVoices = (ushort)math.clamp(statistics.OccludedVoices, 0, ushort.MaxValue),
                DelayedVoices = (ushort)math.clamp(statistics.DelayedVoices, 0, ushort.MaxValue),
                StateHash = stateHash,
                LoudestWeight = loudestWeight,
                SortTimeMs = statistics.SortTimeMs,
                AverageRt60Seconds = statistics.AverageRt60Seconds,
                AverageLowPassHertz = statistics.AverageLowPassHertz,
                MaximumDelaySeconds = statistics.MaximumDelaySeconds,
                AcousticOcclusionTimeMs = statistics.AcousticOcclusionTimeMs
            };
            _virtualVoiceBlackBoxCursor = (_virtualVoiceBlackBoxCursor + 1) % _virtualVoiceBlackBox.Length;
            if (!math.isfinite(loudestWeight) ||
                !math.isfinite(statistics.SortTimeMs) ||
                !math.isfinite(statistics.AcousticOcclusionTimeMs) ||
                statistics.SortTimeMs > 0.5f ||
                statistics.AcousticOcclusionTimeMs > 1.0f)
            {
                DumpVirtualVoiceBlackBox();
            }
        }

        private void PublishVirtualVoiceTelemetry(in VirtualVoiceStatistics statistics)
        {
            GlobalTelemetryBus.PublishModTelemetry(
                _virtualVoiceTelemetryHash,
                _virtualVoiceActiveHash,
                math.max(0, statistics.ActivePhysicalVoices));
            GlobalTelemetryBus.PublishModTelemetry(
                _virtualVoiceTelemetryHash,
                _virtualVoiceCulledHash,
                math.max(0, statistics.CulledVoices));
        }

        private static uint ComputeVirtualVoiceStateHash(in VirtualVoiceStatistics statistics, float loudestWeight)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)statistics.Frame) * 16777619u;
                hash = (hash ^ (uint)statistics.TotalVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.AudibleVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.CulledVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.ActivePhysicalVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.StolenVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.OccludedVoices) * 16777619u;
                hash = (hash ^ (uint)statistics.DelayedVoices) * 16777619u;
                hash = (hash ^ math.asuint(loudestWeight)) * 16777619u;
                hash = (hash ^ math.asuint(statistics.SortTimeMs)) * 16777619u;
                hash = (hash ^ math.asuint(statistics.AcousticOcclusionTimeMs)) * 16777619u;
                return hash;
            }
        }

        private void DumpVirtualVoiceBlackBox()
        {
            if (_virtualVoiceBlackBoxDumped || !_virtualVoiceBlackBox.IsCreated)
                return;

            _virtualVoiceBlackBoxDumped = true;
            try
            {
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", VirtualVoiceDumpRelativePath));
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(_virtualVoiceBlackBoxCursor);
                    writer.Write(_virtualVoiceBlackBox.Length);
                    for (int i = 0; i < _virtualVoiceBlackBox.Length; i++)
                    {
                        AcousticOcclusionTelemetryEntry entry = _virtualVoiceBlackBox[i];
                        writer.Write(entry.Frame);
                        writer.Write(entry.TotalVoices);
                        writer.Write(entry.AudibleVoices);
                        writer.Write(entry.CulledVoices);
                        writer.Write(entry.ActiveVoices);
                        writer.Write(entry.PhysicalVoiceLimit);
                        writer.Write(entry.StolenVoices);
                        writer.Write(entry.DroppedVoices);
                        writer.Write(entry.Flags);
                        writer.Write(entry.OccludedVoices);
                        writer.Write(entry.DelayedVoices);
                        writer.Write(entry.StateHash);
                        writer.Write(entry.LoudestWeight);
                        writer.Write(entry.SortTimeMs);
                        writer.Write(entry.AverageRt60Seconds);
                        writer.Write(entry.AverageLowPassHertz);
                        writer.Write(entry.MaximumDelaySeconds);
                        writer.Write(entry.AcousticOcclusionTimeMs);
                    }
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception, this);
#endif
            }
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private void TryLoadAcousticLutFallbackCold()
        {
            if (_acousticLutFallbackLoaded)
                return;

            _acousticLutFallbackLoaded = true;
            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string path = Path.Combine(projectRoot, AcousticLutRelativePath);
                if (!File.Exists(path))
                    return;

                byte[] bytes = File.ReadAllBytes(path); // COLD ALLOC: byte[524288] - one-shot Sabine RT60+damping fallback read - owner: SpatialAudioManager
                if (bytes.Length != AcousticLutExpectedBytes)
                    return;

                int volumeIndex = 128;
                int absorptionIndex = 128;
                int byteOffset = ((volumeIndex * AcousticLutAbsorptionCount) + absorptionIndex) * AcousticLutRecordBytes;
                float rt60Seconds = BitConverter.ToSingle(bytes, byteOffset);
                float damping01 = BitConverter.ToSingle(bytes, byteOffset + 4);
                if (math.isfinite(rt60Seconds) && rt60Seconds > 0f)
                    _acousticLutFallbackRt60Seconds = math.clamp(rt60Seconds, VirtualVoiceUtility.SabineMinimumRt60Seconds, VirtualVoiceUtility.SabineMaximumRt60Seconds);
                if (math.isfinite(damping01) && damping01 > 0f)
                    _acousticLutFallbackDamping01 = math.saturate(damping01);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(exception, this);
#endif
            }
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
            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition))
                return;

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
                PublishAcousticEchoPortalTap(
                    in sourceAup,
                    in acousticPortalResult,
                    volume,
                    0,
                    0u);
            }

            if (hasListener && IsBeyondMaxHearingRange(in audibleAup, in listenerAup))
                return;

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in audibleAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return;

            byte voiceCategory = ResolveAudioVoiceCategory(clip);
            if (!TryReserveAudioVoiceCategory(voiceCategory, -1))
                return;

            int index = AcquireSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            AssignAudioVoiceCategory(index, voiceCategory);
            source.enabled = true;
            source.transform.position = audiblePosition;
            AudioResidencyCache.TouchClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup), true);
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
            float clampedDopplerRatio = math.clamp(
                1f,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && index < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[index] = clampedDopplerRatio;
            source.pitch = ResolveSourcePitch(index, clampedDopplerRatio);
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
            return;
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

            AudioResidencyCache.TouchClip(clip, AudioResidencyDomain.Interface, true);
            source.clip = clip;
            source.volume = math.saturate(volume);
            source.pitch = _brownoutAudioPitchRatio;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = ResolveUiMixerGroup(clip, mixerGroup);

            source.Play();
            _startTimes2D[index] = Time.unscaledTime;
        }

        public void TouchClip(AudioClip clip, byte residencyDomain, bool decodeNow)
        {
            if (!TryResolveAudioResidencyDomain(residencyDomain, out AudioResidencyDomain domain))
                return;

            AudioResidencyCache.TouchClip(clip, domain, decodeNow);
        }

        public void PrewarmAudioSource(AudioSource source, byte residencyDomain)
        {
            if (!TryResolveAudioResidencyDomain(residencyDomain, out AudioResidencyDomain domain))
                return;

            AudioResidencyCache.PrewarmAudioSource(source, domain);
        }

        public void ReleaseAudioSource(AudioSource source)
        {
            AudioResidencyCache.ReleaseAudioSource(source);
        }

        public void ReleaseClip(AudioClip clip)
        {
            AudioResidencyCache.ReleaseClip(clip);
        }

        private static bool TryResolveAudioResidencyDomain(byte residencyDomain, out AudioResidencyDomain domain)
        {
            domain = (AudioResidencyDomain)residencyDomain;
            return AudioResidencyDomainUtility.IsValid(domain);
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
        public bool TryGetAcousticRadarPayload(out NativeArray<float>.ReadOnly radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = default;
            radialResolution = AcousticRadarBinCount;
            if (!_acousticRadarIntensityBins.IsCreated || radialResolution <= 0)
                return false;

            radialIntensityBins = _acousticRadarIntensityBins.AsReadOnly();
            return true;
        }

        /// <summary>Uploads the persistent 360-degree acoustic radar ring into a caller-owned texture.</summary>
        public bool TryUploadAcousticRadarPayload(Texture2D destination, out int uploadedSampleCount, out float peakIntensity)
        {
            uploadedSampleCount = 0;
            peakIntensity = 0f;

            if (destination == null ||
                !_acousticRadarIntensityBins.IsCreated ||
                AcousticRadarBinCount <= 0)
            {
                return false;
            }

            int sampleCount = math.min(_acousticRadarIntensityBins.Length, AcousticRadarBinCount);
            if (sampleCount <= 0)
                return false;

            destination.SetPixelData(_acousticRadarIntensityBins.GetSubArray(0, sampleCount), 0);

            float peak = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = _acousticRadarIntensityBins[i];
                if (sample > peak)
                    peak = sample;
            }

            uploadedSampleCount = sampleCount;
            peakIntensity = math.saturate(peak);
            return true;
        }

        /// <summary>Returns the persistent 8x4 acoustic radar grid and its GPU upload buffer.</summary>
        public bool TryGetAcousticRadarGridPayload(
            out NativeArray<float>.ReadOnly gridEnergy,
            out int azimuthBins,
            out int elevationBins,
            out ComputeBuffer gridBuffer)
        {
            gridEnergy = _acousticRadarGrid.IsCreated ? _acousticRadarGrid.AsReadOnly() : default;
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
            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup, out Vector3 sourceAbsolutePosition))
                return false;

            if (hasListener && IsBeyondMaxHearingRange(in sourceAup, in listenerAup))
                return false;

            AudioLodTier lodTier = hasListener
                ? ResolveAudioLodTier(in sourceAup, in listenerAup)
                : AudioLodTier.Tier0Full;
            if (lodTier == AudioLodTier.Tier2Culled)
                return false;

            byte voiceCategory = ResolveAudioVoiceCategory(clip);
            if (!TryReserveAudioVoiceCategory(voiceCategory, -1))
                return false;

            int index = AcquireSourceIndexNoEvict();
            if (index < 0)
                return false;

            AudioSource source = _pool[index];
            ResetWorldSourceState(index, true);
            AssignAudioVoiceCategory(index, voiceCategory);
            source.enabled = true;
            source.transform.position = position;
            AudioResidencyCache.TouchClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup), true);
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

        private void TryRegisterFastTickable()
        {
            if (_registeredFastTickable || !Application.isPlaying)
                return;

            _registeredFastTickable = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);
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

        private void TryRegisterOriginShiftListener()
        {
            if (_registeredOriginShiftListener || !Application.isPlaying)
                return;

            HectonFloatingOrigin.RegisterListener(this);
            _registeredOriginShiftListener = HectonFloatingOrigin.IsListenerRegistered(this);
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
            if (PhysicsImpactSignal.IsHeavy(in impactSignal))
                amplitude = math.max(amplitude, 0.45f);

            AbsoluteUniversePosition impactAup = impactSignal.ResolvePointAup();
            TryQueueImpactRadarEmitter(
                impactSignal.Point,
                in impactAup,
                amplitude,
                math.saturate(impactSignal.Intensity));
        }

        private void ConsumeAcousticImpulseSignals()
        {
            int frame = Time.frameCount;
            if (_lastAcousticImpulseSignalFrame == frame)
                return;

            _lastAcousticImpulseSignalFrame = frame;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType != (ushort)PhysicsEventType.AcousticImpulse)
                    continue;

                AcousticImpulseEvent impulseEvent = new AcousticImpulseEvent(
                    payload.RuntimePosition,
                    payload.Direction,
                    payload.Scalar0,
                    payload.Scalar1,
                    payload.Scalar2,
                    payload.RadiusMeters,
                    payload.PrimaryId,
                    unchecked((byte)payload.DataHash),
                    (AcousticImpulseFlags)payload.StatusBits);
                HandleAcousticImpulse(in impulseEvent);
            }

            ReadOnlySpan<AcousticPingSignal> acousticPings = SignalBus<AcousticPingSignal>.GetFrameSnapshot();
            for (int i = 0; i < acousticPings.Length; i++)
            {
                AcousticPingSignal ping = acousticPings[i];
                float amplitude = math.saturate(ping.Intensity01);
                if (amplitude <= 0.001f)
                    continue;

                Vector3 runtimePosition = ToRuntimeVector3(in ping.PositionAup);
                TryQueueImpactRadarEmitter(
                    runtimePosition,
                    in ping.PositionAup,
                    amplitude,
                    math.saturate(ping.RadiusMeters * 0.001953125f));
            }
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
            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition positionAup))
                return false;

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
            if (!TryResolveAupFromRuntimeOrigin(implosionRuntimePosition, out AbsoluteUniversePosition implosionAup))
                return;

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
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition eventAup))
                return;

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

            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
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

            if (!TryResolveAupFromRuntimeOrigin(listenerRuntimePosition, out listenerAup))
            {
                listenerAbsolutePosition = default;
                return false;
            }

            listenerAbsolutePosition = ToAbsoluteVector3(in listenerAup);
            return true;
        }

        private static bool TryResolveSourceAupFrame(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition sourceAup,
            out Vector3 absolutePosition)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out sourceAup))
            {
                absolutePosition = default;
                return false;
            }

            absolutePosition = ToAbsoluteVector3(in sourceAup);
            return true;
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

        private static Vector3 ToListenerRelativeRuntimeVector3(
            in AbsoluteUniversePosition sourceAup,
            bool hasListener,
            in AbsoluteUniversePosition listenerAup,
            Vector3 listenerRuntimePosition)
        {
            if (!hasListener)
                return ToRuntimeVector3(in sourceAup);

            float3 relative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in listenerAup, in sourceAup);
            return listenerRuntimePosition + new Vector3(relative.x, relative.y, relative.z);
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

        private static double3 ToAbsoluteAcousticMeters(in AcousticAup aup)
        {
            double cellSize = AcousticAup.CellSizeMeters;
            return new double3(
                (aup.GridX * cellSize) + aup.Local.x,
                (aup.GridY * cellSize) + aup.Local.y,
                (aup.GridZ * cellSize) + aup.Local.z);
        }

        private bool TryResolvePlayerListenerAup(
            Transform listener,
            Vector3 listenerRuntimePosition,
            out AbsoluteUniversePosition listenerAup)
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            if (playerContext != null &&
                IsPlayerOwnedListener(listener, playerContext.PlayerTransform, playerContext.PlayerObject, playerContext.PlayerCamera))
            {
                if (TryResolveXrCachedHeadAup(listenerRuntimePosition, out listenerAup))
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
                if (TryResolveXrCachedHeadAup(listenerRuntimePosition, out listenerAup))
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

        private static bool TryResolveXrCachedHeadAup(Vector3 runtimePosition, out AbsoluteUniversePosition headAup)
        {
            if (HectonXRRuntimeState.TryResolveCachedHeadAupFields(
                    runtimePosition,
                    out long gridX,
                    out long gridY,
                    out long gridZ,
                    out float localX,
                    out float localY,
                    out float localZ))
            {
                headAup = new AbsoluteUniversePosition
                {
                    GridX = gridX,
                    GridY = gridY,
                    GridZ = gridZ,
                    LocalX = localX,
                    LocalY = localY,
                    LocalZ = localZ
                };
                return true;
            }

            headAup = default;
            return false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private static bool IsFiniteAup(in AbsoluteUniversePosition value)
        {
            return math.all(math.isfinite(new double3(value.LocalX, value.LocalY, value.LocalZ)));
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!IsFiniteAup(in originAup))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return IsFiniteAup(in positionAup);
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

            if (_worldSourceVoiceCategories != null && sourceIndex < _worldSourceVoiceCategories.Length)
                _worldSourceVoiceCategories[sourceIndex] = AudioVoiceCategoryNone;

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

        private bool IsListenerInteriorZoneActive()
        {
            AcousticZoneController acousticZone = ResolveAcousticZone();
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

                if (!TryResolveAupFromRuntimeOrigin(worldCenter, out AbsoluteUniversePosition worldCenterAup))
                    continue;

                _baseInteriorMuffleAups[writeIndex] = worldCenterAup;
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
                if (IsThreatWorldSource(sourceIndex))
                    EvictCreatureAudioBanksForFrozenTier();

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

        private static void PublishAcousticEchoPortalTap(
            in AbsoluteUniversePosition sourceAup,
            in AcousticPathResult result,
            float volume01,
            int stationaryCacheKey,
            uint sourceId)
        {
            if (!math.isfinite(volume01) || volume01 <= 0.001f)
                return;

            byte flags = stationaryCacheKey != 0
                ? AcousticEchoLocationRuntime.FlagNoisemakerCandidate
                : (byte)0;
            uint resolvedSourceId = sourceId != 0u
                ? sourceId
                : ResolveAcousticEchoSourceId(stationaryCacheKey, in sourceAup);
            AcousticEchoLocationRuntime.TryEnqueuePortalEcho(
                in sourceAup,
                in result.LastPortalAup,
                result.Status == AcousticPathStatus.PathFound ? (byte)1 : (byte)0,
                result.UsedPortalPath,
                result.Transmission01,
                result.DelaySeconds,
                volume01,
                resolvedSourceId,
                Time.frameCount,
                Time.unscaledTime,
                AcousticEchoLocationRuntime.EncodeQualityWeightByte(HomeostasisBrain.GlobalQualityWeight),
                flags);
        }

        private static uint ResolveAcousticEchoSourceId(int stationaryCacheKey, in AbsoluteUniversePosition sourceAup)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)stationaryCacheKey) * 16777619u;
            hash = (hash ^ (uint)sourceAup.GridX) * 16777619u;
            hash = (hash ^ (uint)(sourceAup.GridX >> 32)) * 16777619u;
            hash = (hash ^ (uint)sourceAup.GridY) * 16777619u;
            hash = (hash ^ (uint)(sourceAup.GridY >> 32)) * 16777619u;
            hash = (hash ^ (uint)sourceAup.GridZ) * 16777619u;
            hash = (hash ^ (uint)(sourceAup.GridZ >> 32)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static float ResolveAcousticPortalReverbMix(float roomVolumeCubicMeters)
        {
            float volume01 = math.saturate(roomVolumeCubicMeters * math.rcp(SabineReverbModuleVolumeReferenceCubicMeters));
            float volumeCurve = volume01 * math.rsqrt(math.max(volume01, 0.000001f));
            return math.clamp(0.12f + volumeCurve * 0.68f, 0f, 1.1f);
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
                GlobalQualityWeight = ResolveVirtualVoiceQualityWeight(),
                DisablePortalPath = 0
            };

            double start = Time.realtimeSinceStartupAsDouble;
            AcousticPathJob pathJob = new AcousticPathJob
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
            };
            pathJob.Execute();

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

        private bool ShouldUseAcousticPortalPath()
        {
            float qualityWeight = ResolveVirtualVoiceQualityWeight();
            float portalBudget01 = math.smoothstep(0.12f, 0.92f, math.saturate(SanitizeFinite(qualityWeight, 0f)));
            return portalBudget01 > 0.0001f;
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
                if (!TryResolveAupFromRuntimeOrigin(_acousticPortalWaypointScratch[i], out AbsoluteUniversePosition aup))
                    return false;

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
                        DistanceMeters = ResolveRuntimeDistanceMeters(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i - 1]),
                        Flags = AcousticPortalFlags.Voxel
                    };
                }

                if (i + 1 < nodeCount && edgeCount < AcousticPortalMaxEdges)
                {
                    _acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = i + 1,
                        DistanceMeters = ResolveRuntimeDistanceMeters(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i + 1]),
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

        private static float ResolveRuntimeDistanceMeters(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            float distanceSq = dx * dx + dy * dy + dz * dz;
            if (!math.isfinite(distanceSq) || distanceSq <= 0f)
                return 0f;

            return distanceSq * math.rsqrt(math.max(distanceSq, 0.0001f));
        }

        private bool TryBuildHabitatAcousticPortalGraph(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            ConstructionManager constructionManager = _cachedConstructionManager;
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

            NativeArray<int>.ReadOnly edgeOffsets = graph.EdgeOffsets;
            NativeArray<int>.ReadOnly edgeDestinations = graph.EdgeDestinations;
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

                if (!TryResolveAupFromRuntimeOrigin(new Vector3(nodePosition.x, nodePosition.y, nodePosition.z), out AbsoluteUniversePosition aup))
                    return false;

                float roomVolume = 0f;
                NativeArray<float>.ReadOnly roomVolumes = graph.RoomVolumes;
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

            NativeArray<byte>.ReadOnly edgeFlags = graph.EdgeFlags;
            NativeArray<float>.ReadOnly edgeResistance = graph.EdgeResistance;
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
            _acousticPortalBlackBox[index] = new AcousticPortalTelemetryEntry
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
                        AcousticPortalTelemetryEntry entry = _acousticPortalBlackBox[i];
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

        private bool IsBeyondMaxHearingRange(in AbsoluteUniversePosition sourceAup, in AbsoluteUniversePosition listenerAup)
        {
            float maxRange = math.max(1f, _maxDistance);
            float maxRangeSq = maxRange * maxRange;
            float distanceSq = ClampAupDistanceSqToFloat(AbsoluteUniversePosition.DistanceSq(in listenerAup, in sourceAup));
            return distanceSq > maxRangeSq;
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

        private bool EnsureVaultBackedArray<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            ref NativeArray<T> alias)
            where T : struct
        {
            if (requiredLength <= 0)
            {
                alias = default;
                handle = default;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null)
            {
                alias = default;
                handle = default;
                return false;
            }

            if (TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out alias))
                return true;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle) ||
                    !TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out alias))
                {
                    alias = default;
                    handle = default;
                    return false;
                }

                return true;
            }

            handle = vault.GetGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Audio,
                options);
            if (!TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out alias))
            {
                alias = default;
                handle = default;
                return false;
            }

            return true;
        }

        private void RefreshVirtualExternalStateAliases()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _virtualVoiceScalabilityState = default;
                _virtualVoiceRollbackAudioSuppression = default;
                _acousticVoxelSdfTexture3D = default;
                return;
            }

            if (TryOpenBorrowedAudioVaultBuffer(
                    vault,
                    ref _virtualVoiceScalabilityStateHandle,
                    BufferID.ShinobuScalabilityState,
                    SystemID.GraphicsScalability,
                    1,
                    out NativeArray<ScalabilityStateDTO> scalabilityState))
            {
                _virtualVoiceScalabilityState = scalabilityState;
            }
            else
            {
                _virtualVoiceScalabilityState = default;
            }

            if (TryOpenBorrowedAudioVaultBuffer(
                    vault,
                    ref _virtualVoiceRollbackAudioSuppressionHandle,
                    RollbackNetcodeVault.AudioSuppression,
                    RollbackNetcodeVault.OwnerSystem,
                    1,
                    out NativeArray<RollbackAudioSuppressionDTO> rollbackSuppression))
            {
                _virtualVoiceRollbackAudioSuppression = rollbackSuppression;
            }
            else
            {
                _virtualVoiceRollbackAudioSuppression = default;
            }

            if (TryOpenBorrowedAudioVaultBuffer(
                    vault,
                    ref _acousticVoxelSdfTexture3DHandle,
                    BufferID.VoxelSdfTexture3D,
                    SystemID.WorldStreaming,
                    AcousticSdfDefaultVoxelCount,
                    out NativeArray<byte> voxelSdfTexture3D))
            {
                _acousticVoxelSdfTexture3D = voxelSdfTexture3D;
            }
            else
            {
                _acousticVoxelSdfTexture3D = default;
            }
        }

        private static bool TryOpenBorrowedAudioVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID ownerSystem,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryOpenAudioVaultBuffer(vault, ref handle, bufferId, ownerSystem, requiredLength, out buffer))
                return true;

            if (vault == null ||
                requiredLength <= 0 ||
                !vault.TryGetGenerationHandle(bufferId, out handle) ||
                !TryOpenAudioVaultBuffer(vault, ref handle, bufferId, ownerSystem, requiredLength, out buffer))
            {
                buffer = default;
                handle = default;
                return false;
            }

            return true;
        }

        private static bool TryOpenAudioVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID ownerSystem,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (vault == null ||
                requiredLength <= 0 ||
                !IsAudioVaultHandle(in handle, bufferId, ownerSystem) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool IsAudioVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID ownerSystem) where T : struct
        {
            return handle.BufferID == (uint)bufferId &&
                   handle.SystemID == (uint)ownerSystem &&
                   handle.Generation != 0u;
        }

        private void ClearVaultBackedTelemetryAliases()
        {
            _acousticRadarIntensityBinsHandle = default;
            _acousticRadarGridHandle = default;
            _virtualVoiceWritePoolHandle = default;
            _virtualVoiceSortPoolHandle = default;
            _virtualVoiceDtoPoolHandle = default;
            _virtualVoiceSortKeyPoolHandle = default;
            _virtualVoiceSelectionsHandle = default;
            _virtualVoiceStatisticsHandle = default;
            _virtualVoiceBlackBoxHandle = default;
            _virtualVoiceTuningHandle = default;
            _acousticSourceWritePoolHandle = default;
            _acousticSourceSortPoolHandle = default;
            _acousticPreviousAupWritePoolHandle = default;
            _acousticPreviousAupSortPoolHandle = default;
            _acousticDspOutputPoolHandle = default;
            _acousticMaterialRowsHandle = default;
            _acousticSelectedSourcePoolHandle = default;
            _acousticSelectedPreviousAupPoolHandle = default;
            _virtualVoiceScalabilityStateHandle = default;
            _virtualVoiceRollbackAudioSuppressionHandle = default;
            _acousticVoxelSdfTexture3DHandle = default;
            _acousticPortalNodesHandle = default;
            _acousticPortalEdgesHandle = default;
            _acousticPortalResultHandle = default;
            _acousticPortalCostsHandle = default;
            _acousticPortalCameFromHandle = default;
            _acousticPortalStatesHandle = default;
            _acousticPortalBlackBoxHandle = default;
            _acousticRadarIntensityBins = default;
            _acousticRadarGrid = default;
            _virtualVoiceWritePool = default;
            _virtualVoiceSortPool = default;
            _virtualVoiceDtoPool = default;
            _virtualVoiceSortKeyPool = default;
            _virtualVoiceSelections = default;
            _virtualVoiceStatistics = default;
            _virtualVoiceBlackBox = default;
            _virtualVoiceTuningVault = default;
            _acousticSourceWritePool = default;
            _acousticSourceSortPool = default;
            _acousticPreviousAupWritePool = default;
            _acousticPreviousAupSortPool = default;
            _acousticDspOutputPool = default;
            _acousticMaterialRows = default;
            _acousticSelectedSourcePool = default;
            _acousticSelectedPreviousAupPool = default;
            _acousticVoxelSdfTexture3D = default;
            _virtualVoiceScalabilityState = default;
            _virtualVoiceRollbackAudioSuppression = default;
            _acousticPortalNodes = default;
            _acousticPortalEdges = default;
            _acousticPortalResult = default;
            _acousticPortalCosts = default;
            _acousticPortalCameFrom = default;
            _acousticPortalStates = default;
            _acousticPortalBlackBox = default;
            _virtualVoiceWriteCount = 0;
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _acousticOcclusionOutputCount = 0;
            _acousticOcclusionScheduled = false;
        }

        private void InitializeTelemetryCaches()
        {
            EnsureVaultBackedArray(
                ref _acousticRadarIntensityBinsHandle,
                BufferID.SpatialAudioRadarIntensityBins,
                AcousticRadarBinCount,
                NativeArrayOptions.ClearMemory,
                ref _acousticRadarIntensityBins);
            EnsureVaultBackedArray(
                ref _acousticRadarGridHandle,
                BufferID.SpatialAudioRadarGrid,
                AcousticRadarGridCellCount,
                NativeArrayOptions.ClearMemory,
                ref _acousticRadarGrid);

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
                _delayedAudioIngress = new NativeQueue<DelayedAudioEvent>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<DelayedAudioEvent>[16] - underwater propagation ingress queue for delayed world events - owner: SpatialAudioManager
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
                _audioEventQueue = new NativeQueue<CoreAudioEvent>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<CoreAudioEvent>[32] - zero-GC gameplay audio ingress drained by SpatialAudioManager LateFrameTick - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeQueue(
                    _audioEventQueue,
                    MaxQueuedAudioEvents,
                    nameof(SpatialAudioManager),
                    nameof(_audioEventQueue),
                    NativeAllocationLifetime.Session);
                PrewarmAudioEventQueue();
            }
            EnsureAudioClipHashMapCold();

            if (!_pendingDelayedAudioEvents.IsCreated)
            {
                _pendingDelayedAudioEvents = new NativeList<DelayedAudioEvent>(MaxDelayedAudioEvents, DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeList<DelayedAudioEvent>[16] - active delayed world-event schedule - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(
                    _pendingDelayedAudioEvents,
                    nameof(SpatialAudioManager),
                    nameof(_pendingDelayedAudioEvents),
                NativeAllocationLifetime.Session);
            }

            EnsureVaultBackedArray(
                ref _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _virtualVoiceWritePool);
            EnsureVaultBackedArray(
                ref _virtualVoiceSortPoolHandle,
                SpatialAudioVirtualVoiceSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _virtualVoiceSortPool);
            EnsureVaultBackedArray(
                ref _virtualVoiceDtoPoolHandle,
                SpatialAudioVirtualVoiceDtoPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _virtualVoiceDtoPool);
            EnsureVaultBackedArray(
                ref _virtualVoiceSortKeyPoolHandle,
                SpatialAudioVirtualVoiceSortKeyPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _virtualVoiceSortKeyPool);
            EnsureVaultBackedArray(
                ref _acousticSourceWritePoolHandle,
                SpatialAudioAcousticSourceWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticSourceWritePool);
            EnsureVaultBackedArray(
                ref _acousticSourceSortPoolHandle,
                SpatialAudioAcousticSourceSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticSourceSortPool);
            EnsureVaultBackedArray(
                ref _acousticPreviousAupWritePoolHandle,
                SpatialAudioAcousticPreviousAupWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticPreviousAupWritePool);
            EnsureVaultBackedArray(
                ref _acousticPreviousAupSortPoolHandle,
                SpatialAudioAcousticPreviousAupSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticPreviousAupSortPool);
            EnsureVaultBackedArray(
                ref _acousticDspOutputPoolHandle,
                SpatialAudioAcousticDspOutputPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticDspOutputPool);
            EnsureVaultBackedArray(
                ref _acousticSelectedSourcePoolHandle,
                SpatialAudioAcousticSelectedSourcePoolBufferId,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticSelectedSourcePool);
            EnsureVaultBackedArray(
                ref _acousticSelectedPreviousAupPoolHandle,
                SpatialAudioAcousticSelectedPreviousAupPoolBufferId,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.UninitializedMemory,
                ref _acousticSelectedPreviousAupPool);
            if (EnsureVaultBackedArray(
                    ref _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    3,
                    NativeArrayOptions.UninitializedMemory,
                    ref _acousticMaterialRows))
            {
                VirtualVoiceProfileCsvParser.GenerateEmergencyMockAcoustics(_acousticMaterialRows);
            }

            EnsureVaultBackedArray(
                ref _virtualVoiceSelectionsHandle,
                BufferID.SpatialAudioVirtualVoiceSelections,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.ClearMemory,
                ref _virtualVoiceSelections);
            EnsureVaultBackedArray(
                ref _virtualVoiceStatisticsHandle,
                BufferID.SpatialAudioVirtualVoiceStatistics,
                1,
                NativeArrayOptions.ClearMemory,
                ref _virtualVoiceStatistics);
            EnsureVaultBackedArray(
                ref _virtualVoiceBlackBoxHandle,
                BufferID.SpatialAudioVirtualVoiceBlackBox,
                VirtualVoiceBlackBoxFrameCount,
                NativeArrayOptions.ClearMemory,
                ref _virtualVoiceBlackBox);
            EnsureVaultBackedArray(
                ref _virtualVoiceTuningHandle,
                SpatialAudioVirtualVoiceTuningBufferId,
                1,
                NativeArrayOptions.ClearMemory,
                ref _virtualVoiceTuningVault);
            EnsureVirtualVoiceTuningState();
            RefreshVirtualExternalStateAliases();

            EnsureVirtualChannelArrays();

            EnsureVaultBackedArray(
                ref _acousticPortalNodesHandle,
                BufferID.SpatialAudioPortalNodes,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalNodes);
            EnsureVaultBackedArray(
                ref _acousticPortalEdgesHandle,
                BufferID.SpatialAudioPortalEdges,
                AcousticPortalMaxEdges,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalEdges);
            EnsureVaultBackedArray(
                ref _acousticPortalResultHandle,
                BufferID.SpatialAudioPortalResult,
                1,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalResult);
            EnsureVaultBackedArray(
                ref _acousticPortalCostsHandle,
                BufferID.SpatialAudioPortalCosts,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalCosts);
            EnsureVaultBackedArray(
                ref _acousticPortalCameFromHandle,
                BufferID.SpatialAudioPortalCameFrom,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalCameFrom);
            EnsureVaultBackedArray(
                ref _acousticPortalStatesHandle,
                BufferID.SpatialAudioPortalStates,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalStates);

            if (!_acousticPortalOpenSet.IsCreated)
            {
                _acousticPortalOpenSet = new NativeList<int>(AcousticPortalMaxNodes, DataVaultExemptSceneScratchAllocator); // COLD ALLOC: NativeList<int>[30] - acoustic path open set - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(_acousticPortalOpenSet, nameof(SpatialAudioManager), nameof(_acousticPortalOpenSet), NativeAllocationLifetime.Session);
            }

            if (!_acousticPortalClosedSet.IsCreated)
            {
                _acousticPortalClosedSet = new NativeList<int>(AcousticPortalMaxNodes, DataVaultExemptSceneScratchAllocator); // COLD ALLOC: NativeList<int>[30] - acoustic path closed set - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeList(_acousticPortalClosedSet, nameof(SpatialAudioManager), nameof(_acousticPortalClosedSet), NativeAllocationLifetime.Session);
            }

            EnsureVaultBackedArray(
                ref _acousticPortalBlackBoxHandle,
                BufferID.SpatialAudioPortalBlackBox,
                AcousticPortalConstants.TelemetryFrameCount,
                NativeArrayOptions.ClearMemory,
                ref _acousticPortalBlackBox);

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

        private void EnsureAudioClipHashMapCold()
        {
            AudioClip[] table = _audioEventClipTable;
            int requiredCapacity = math.max(1, table != null ? table.Length : 0);
            if (!_audioClipHashToTableIndex.IsCreated)
            {
                _audioClipHashToTableIndex = new NativeParallelHashMap<uint, int>(requiredCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeParallelHashMap<uint,int>[audioEventClipTable] - clip hash to preloaded clip index - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeParallelHashMap(
                    _audioClipHashToTableIndex,
                    nameof(SpatialAudioManager),
                    nameof(_audioClipHashToTableIndex),
                    NativeAllocationLifetime.Session);
            }
            else if (_audioClipHashToTableIndex.Capacity < requiredCapacity)
            {
                ReleaseAudioClipHashMap();
                _audioClipHashToTableIndex = new NativeParallelHashMap<uint, int>(requiredCapacity, DataVaultExemptOwnerIndexAllocator); // COLD ALLOC: NativeParallelHashMap<uint,int>[audioEventClipTable] - resized clip hash lookup - owner: SpatialAudioManager
                NativeMemorySentinel.RegisterNativeParallelHashMap(
                    _audioClipHashToTableIndex,
                    nameof(SpatialAudioManager),
                    nameof(_audioClipHashToTableIndex),
                    NativeAllocationLifetime.Session);
            }

            _audioClipHashToTableIndex.Clear();
            if (table == null)
                return;

            for (int i = 0; i < table.Length; i++)
            {
                AudioClip clip = table[i];
                if (clip == null)
                    continue;

                uint clipHash = unchecked((uint)EntityId.ToULong(clip.GetEntityId()));
                if (clipHash != 0u)
                    _audioClipHashToTableIndex.TryAdd(clipHash, i);
            }
        }

        private void ReleaseAudioClipHashMap()
        {
            if (!_audioClipHashToTableIndex.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeParallelHashMap(nameof(SpatialAudioManager), nameof(_audioClipHashToTableIndex));
            _audioClipHashToTableIndex.Dispose();
            _audioClipHashToTableIndex = default;
        }

        private void EnsureVirtualVoiceTuningState()
        {
            VirtualVoiceTuningSnapshot tuning = _virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0
                ? _virtualVoiceTuningVault[0]
                : VirtualVoiceTuningSnapshot.CreateDefault();

            if (tuning.SoundSpeedMetersPerSecond <= 0f || !math.isfinite(tuning.SoundSpeedMetersPerSecond))
                tuning = VirtualVoiceTuningSnapshot.CreateDefault();

            tuning = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            _virtualVoiceTuning = tuning;
            if (_virtualVoiceTuningVault.IsCreated && _virtualVoiceTuningVault.Length > 0)
                _virtualVoiceTuningVault[0] = tuning;
        }

        private void ReleaseTelemetryCaches()
        {
            CompleteVirtualVoiceSort();

            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseOwnerBuffers(SystemID.Audio, out _);

            ClearVaultBackedTelemetryAliases();

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
            ReleaseAudioClipHashMap();

            if (_pendingDelayedAudioEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SpatialAudioManager), nameof(_pendingDelayedAudioEvents));
                _pendingDelayedAudioEvents.Dispose();
                _pendingDelayedAudioEvents = default;
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

            _delayedAudioIngressCount = 0;
            _audioEventQueueCount = 0;
            _audioEventQueueDroppedCount = 0;
            _virtualVoiceDroppedCount = 0;
            _virtualVoiceWriteCount = 0;
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _virtualVoiceBlackBoxCursor = 0;
            _lastVirtualVoiceStatistics = default;
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

        private AudioResidencyDomain ResolveWorldResidencyDomain(AudioClip clip, AudioMixerGroup requestedGroup)
        {
            byte routeFlags = ResolveClipRouteFlags(clip);
            if (requestedGroup == _threatGroup || (routeFlags & AudioClipRouteFlagThreat) != 0)
                return AudioResidencyDomain.Creatures;

            if (requestedGroup == _interfaceGroup)
                return AudioResidencyDomain.Interface;

            return AudioResidencyDomain.Environment;
        }

        private bool IsThreatWorldSource(int sourceIndex)
        {
            return _worldSourceBusFlags != null &&
                   sourceIndex >= 0 &&
                   sourceIndex < _worldSourceBusFlags.Length &&
                   (_worldSourceBusFlags[sourceIndex] & WorldSourceBusFlagThreat) != 0;
        }

        private void EvictCreatureAudioBanksForFrozenTier()
        {
            int frame = Time.frameCount;
            if (_lastCreatureFrozenBankEvictFrame == frame)
                return;

            _lastCreatureFrozenBankEvictFrame = frame;
            AudioResidencyCache.EvictDomain(AudioResidencyDomain.Creatures);
        }

        private float ResolveSourcePitch(int sourceIndex, float dopplerRatio)
        {
            if (_basePitches == null || sourceIndex < 0 || sourceIndex >= _basePitches.Length)
                return 1f;

            float eclipseRatio = ResolveEclipseAcousticPitchRatio(sourceIndex);
            return math.clamp(
                _basePitches[sourceIndex] * dopplerRatio * eclipseRatio * _timeDilationWorldPitchRatio * _brownoutAudioPitchRatio,
                0.1f,
                3f);
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

        private void UpdateBrownoutAudioPitch(float deltaTime)
        {
            float frameBrownout01 = 0f;
            ReadOnlySpan<BrownoutSignal> signals = SignalBus<BrownoutSignal>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                BrownoutSignal signal = signals[i];
                float severity01 = math.saturate(signal.Severity01);
                float supplyDrop01 = 1f - math.saturate(signal.SupplyRatio);
                frameBrownout01 = math.max(frameBrownout01, math.max(severity01, supplyDrop01));
            }

            if (signals.Length > 0)
            {
                _brownoutTarget01 = frameBrownout01;
            }
            else if (deltaTime > 0f)
            {
                _brownoutTarget01 = math.max(0f, _brownoutTarget01 - deltaTime * BrownoutAudioReleasePerSecond);
            }

            float targetRatio = math.lerp(1f, BrownoutAudioPitchMinimumRatio, math.saturate(_brownoutTarget01));
            float blend = deltaTime > 0f ? FastDecayBlend(BrownoutAudioPitchSharpness, deltaTime) : 1f;
            float nextRatio = math.lerp(_brownoutAudioPitchRatio, targetRatio, blend);
            if (math.abs(nextRatio - _brownoutAudioPitchRatio) <= 0.001f)
                return;

            _brownoutAudioPitchRatio = math.clamp(nextRatio, BrownoutAudioPitchMinimumRatio, 1f);
            ApplyBrownoutPitchToMixerAndSources();
        }

        private void ApplyBrownoutPitchToMixerAndSources()
        {
            float ratio = _brownoutAudioPitchRatio;
            if (math.abs(ratio - _lastAppliedBrownoutPitchRatio) <= 0.001f)
                return;

            _lastAppliedBrownoutPitchRatio = ratio;
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer != null && _hasBrownoutPitchMultiplierParameter)
                mixer.SetFloat(_brownoutPitchMultiplierParameter, ratio);

            ApplyEclipsePitchShiftToActiveWorldSources();
            ApplyBrownoutPitchToActive2DSources();
        }

        private void ApplyBrownoutPitchToActive2DSources()
        {
            if (_pool2D == null)
                return;

            float ratio = _brownoutAudioPitchRatio;
            int count = math.min(_pool2DSize, _pool2D.Length);
            for (int i = 0; i < count; i++)
            {
                AudioSource source = _pool2D[i];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                    continue;

                source.pitch = ratio;
            }
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

            IWeatherService weatherService = ResolveWeatherService();
            if (weatherService != null && weatherService.IsInitialized)
            {
                Vector3 wind = weatherService.GlobalWindVector;
                float windSpeedSq = wind.x * wind.x + wind.y * wind.y + wind.z * wind.z;
                target01 = math.max(target01, math.saturate(windSpeedSq * math.rcp(GlobalWindHowlReferenceSpeedSq)));
                if ((weatherService.CurrentWeatherState & WeatherState.Storm) != 0)
                    target01 = math.max(target01, math.saturate(_globalWindHowlStormFloor));
            }

            HectonSurfaceWeatherDirector surfaceWeather = ResolveSurfaceWeatherDirector();
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

        private bool ResolveGlobalWindHowlOccluded()
        {
            HectonSurfaceWeatherDirector surfaceWeather = ResolveSurfaceWeatherDirector();
            if (surfaceWeather != null && surfaceWeather.IsLocallySheltered)
                return true;

            AcousticZoneController acousticZone = ResolveAcousticZone();
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
            _hasBrownoutPitchMultiplierParameter = !string.IsNullOrWhiteSpace(_brownoutPitchMultiplierParameter);
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
            bool leviathan = ContainsTokenInsensitive(clipName, "leviathan");
            bool roar = ContainsTokenInsensitive(clipName, "roar");
            bool bubble = ContainsTokenInsensitive(clipName, "bubble");
            if (leviathan ||
                roar ||
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

            if (leviathan || roar)
                routeFlags |= AudioClipRouteFlagLeviathanRoar;

            if (bubble)
                routeFlags |= AudioClipRouteFlagBubble;

            return routeFlags;
        }

        private byte ResolveAudioVoiceCategory(AudioClip clip)
        {
            byte routeFlags = ResolveClipRouteFlags(clip);
            if ((routeFlags & AudioClipRouteFlagLeviathanRoar) != 0)
                return AudioVoiceCategoryLeviathanRoar;

            if ((routeFlags & AudioClipRouteFlagBubble) != 0)
                return AudioVoiceCategoryBubble;

            return AudioVoiceCategoryNone;
        }

        private bool TryReserveAudioVoiceCategory(byte voiceCategory, int replacingSourceIndex)
        {
            int limit = ResolveAudioVoiceCategoryLimit(voiceCategory);
            if (limit <= 0 || _worldSourceVoiceCategories == null || _pool == null)
                return true;

            int count = 0;
            int scanCount = math.min(_poolSize, math.min(_pool.Length, _worldSourceVoiceCategories.Length));
            for (int i = 0; i < scanCount; i++)
            {
                if (i == replacingSourceIndex || _worldSourceVoiceCategories[i] != voiceCategory)
                    continue;

                AudioSource source = _pool[i];
                if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                    continue;

                count++;
                if (count >= limit)
                    return false;
            }

            return true;
        }

        private static int ResolveAudioVoiceCategoryLimit(byte voiceCategory)
        {
            switch (voiceCategory)
            {
                case AudioVoiceCategoryLeviathanRoar:
                    return MaxLeviathanRoarVoices;
                case AudioVoiceCategoryBubble:
                    return MaxBubbleVoices;
                default:
                    return 0;
            }
        }

        private void AssignAudioVoiceCategory(int sourceIndex, byte voiceCategory)
        {
            if (_worldSourceVoiceCategories == null || sourceIndex < 0 || sourceIndex >= _worldSourceVoiceCategories.Length)
                return;

            _worldSourceVoiceCategories[sourceIndex] = voiceCategory;
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

            if (TryResolveAupFromRuntimeOrigin(sourcePosition, out sourceAup))
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
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
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
            if (_pool != null)
            {
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

            DrawVirtualVoiceEditorGizmos();
        }

        public void DrawVirtualVoiceEditorGizmos()
        {
            if (!Application.isPlaying)
                return;

            Vector3 listenerPosition = _listenerTransform != null ? _listenerTransform.position : transform.position;
            int selectedCount = math.clamp(
                _lastVirtualVoiceStatistics.ActivePhysicalVoices,
                0,
                _virtualVoiceSelections.IsCreated ? _virtualVoiceSelections.Length : 0);
            bool drewAcousticDtoLane = DrawSelectedAcousticSourceDtoGizmos(listenerPosition);
            for (int i = 0; !drewAcousticDtoLane && i < selectedCount; i++)
            {
                VirtualVoiceSelection selection = _virtualVoiceSelections[i];
                if (selection.StableKey == 0u)
                    continue;

                AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in selection.SourceAup);
                Vector3 position = ToRuntimeVector3(in sourceAup);
                float radius = math.clamp(0.18f + ResolveVirtualSelectionVolume(in selection) * 1.8f, 0.18f, 2.2f);
                Gizmos.color = ResolveVirtualVoiceGizmoColor(selection.DspFlags, 0.8f);
                Gizmos.DrawWireSphere(position, radius);
                Gizmos.color = new Color(1f, 0.08f, 0.02f, 0.35f);
                Gizmos.DrawLine(position, listenerPosition);
            }

            if (!_virtualVoiceSortPool.IsCreated)
                return;

            int virtualCount = math.min(_virtualVoiceSortCount, MaxVirtualVoiceCapacity);
            for (int i = selectedCount; i < virtualCount; i++)
            {
                VirtualVoice voice = _virtualVoiceSortPool[i];
                if (voice.StableKey == 0u)
                    continue;

                AbsoluteUniversePosition sourceAup = ToAbsoluteUniversePosition(in voice.SourceAup);
                Vector3 position = ToRuntimeVector3(in sourceAup);
                float radius = math.clamp(0.08f + voice.EffectiveVolume * 3f, 0.08f, 0.8f);
                Gizmos.color = ResolveVirtualVoiceGizmoColor(voice.DspFlags, 0.5f);
                Gizmos.DrawWireSphere(position, radius);
                Gizmos.color = new Color(1f, 0.08f, 0.02f, 0.18f);
                Gizmos.DrawLine(position, listenerPosition);
            }
        }

        private bool DrawSelectedAcousticSourceDtoGizmos(Vector3 listenerPosition)
        {
            if (!_acousticSelectedSourcePool.IsCreated || _acousticOcclusionOutputCount <= 0)
                return false;

            int count = math.min(_acousticOcclusionOutputCount, _acousticSelectedSourcePool.Length);
            for (int i = 0; i < count; i++)
            {
                AcousticSourceDTO source = _acousticSelectedSourcePool[i];
                if (source.SourceHash == 0u)
                    continue;

                AbsoluteUniversePosition sourceAup = AbsoluteUniversePosition.FromAbsolutePosition(source.AUP_Position);
                Vector3 position = ToRuntimeVector3(in sourceAup);
                float occlusion = math.saturate(source.ComputedOcclusion);
                float effectiveVolume = math.saturate(source.BaseVolume) * (1f - occlusion);
                float radius = math.clamp(0.18f + effectiveVolume * 1.8f, 0.18f, 2.2f);
                Gizmos.color = ResolveAcousticDtoGizmoColor(occlusion, 0.85f);
                Gizmos.DrawWireSphere(position, radius);
                Gizmos.color = new Color(1f, 0.08f, 0.02f, 0.35f);
                Gizmos.DrawLine(position, listenerPosition);
            }

            return true;
        }

        private static Color ResolveVirtualVoiceGizmoColor(VirtualVoiceDspFlags flags, float alpha)
        {
            float occluded = (flags & VirtualVoiceDspFlags.SdfOccluded) != 0 ? 1f : 0f;
            Color open = new Color(0f, 1f, 0.25f, alpha);
            Color blocked = new Color(1f, 0.08f, 0.02f, alpha);
            return Color.Lerp(open, blocked, occluded);
        }

        private static Color ResolveAcousticDtoGizmoColor(float occlusion01, float alpha)
        {
            Color open = new Color(0f, 1f, 0.25f, alpha);
            Color blocked = new Color(1f, 0.08f, 0.02f, alpha);
            return Color.Lerp(open, blocked, math.saturate(occlusion01));
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
            bool hasWorldAup = TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition worldAup);
            WorldAup = worldAup;
            _hasWorldAup = hasWorldAup ? (byte)1 : (byte)0;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
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
            if (HasWorldAup)
                return WorldAup;

            return TryResolveAupFromRuntimeOrigin(WorldPosition, out AbsoluteUniversePosition worldAup)
                ? worldAup
                : default;
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = GlobalSignals.CurrentRuntimeOriginAup();
            if (!math.all(math.isfinite(new double3(originAup.LocalX, originAup.LocalY, originAup.LocalZ))))
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return math.all(math.isfinite(new double3(positionAup.LocalX, positionAup.LocalY, positionAup.LocalZ)));
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>
    /// Unmanaged caption payload carried by the deferred audio-caption lane.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct AudioCaptionPayload
    {
        [FieldOffset(0)]
        public AbsoluteUniversePosition WorldAup;
        [FieldOffset(48)]
        public Vector3 WorldPosition;
        [FieldOffset(60)]
        public float DurationSeconds;
        [FieldOffset(64)]
        public float Intensity;
        [FieldOffset(68)]
        public uint CaptionHashId;
        [FieldOffset(72)]
        public int ReferenceSlot;
        [FieldOffset(76)]
        public ushort EventType;
        [FieldOffset(78)]
        public ushort Reserved;
        [FieldOffset(80)]
        public byte HasWorldAup;
        [FieldOffset(81)]
        public byte ReservedByte0;
        [FieldOffset(82)]
        public ushort ReservedShort0;
        [FieldOffset(84)]
        private uint _pad0;
        [FieldOffset(88)]
        private ulong _pad1;
        [FieldOffset(96)]
        private ulong _pad2;
        [FieldOffset(104)]
        private ulong _pad3;
        [FieldOffset(112)]
        private ulong _pad4;
        [FieldOffset(120)]
        private ulong _pad5;
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
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents"));

        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity]; // COLD ALLOC: ListenerSlot[8] - audio caption listeners drained by SystemDispatcher LateUpdate - owner: AudioCaptionEvents
        private static int _listenerCount;
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

            for (int i = 0; i < _listenerCount; i++)
                _listeners[i].Clear();
            _listenerCount = 0;
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
            RegisterImmediate(listener);
        }

        /// <summary>Unregisters one audio caption listener.</summary>
        public static void Unregister(IAudioCaptionEventListener listener)
        {
            if (listener == null)
                return;

            TryUnregisterImmediate(listener);
            if (_listenerCount <= 0)
                DropQueuedCaptionPayloads();
        }

        /// <summary>Flushes queued audio captions to registered UI listeners.</summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listenerCount <= 0)
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
            if (!Application.isPlaying || _listenerCount <= 0)
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
                _pendingEvents = new NativeQueue<AudioCaptionPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AudioCaptionPayload>[32] - deferred spatial audio caption lane flushed by SystemDispatcher LateUpdate - owner: AudioCaptionEvents
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
                _nextFrameEvents = new NativeQueue<AudioCaptionPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AudioCaptionPayload>[32] - next-frame spatial audio captions raised by caption listeners - owner: AudioCaptionEvents
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

            int count = _listenerCount;
            for (int i = count - 1; i >= 0; i--)
            {
                IAudioCaptionEventListener listener = _listeners[i].Listener;
                if (listener != null)
                    listener.OnAudioCaptionRequested(request);
            }
        }

        private static void RegisterImmediate(IAudioCaptionEventListener listener)
        {
            if (ContainsImmediate(listener) || _listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount].Listener = listener;
            _listenerCount++;
        }

        private static bool TryUnregisterImmediate(IAudioCaptionEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = _listenerCount - 1;
                _listeners[i] = _listeners[lastIndex];
                _listeners[lastIndex].Clear();
                _listenerCount = lastIndex;
                return true;
            }

            return false;
        }

        private static bool ContainsImmediate(IAudioCaptionEventListener listener)
        {
            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private struct ListenerSlot
        {
            public IAudioCaptionEventListener Listener;

            public void Clear()
            {
                Listener = null;
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
