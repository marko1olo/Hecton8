// ============================================================================
// HECTON-8 Гўв‚¬вЂќ SpatialAudioManager.cs
// ГђвЂ™Г‘вЂ№Г‘ВЃГђВѕГђВєГђВѕГђВїГ‘в‚¬ГђВѕГђВёГђВ·ГђВІГђВѕГђВґГђВёГ‘вЂљГђВµГђВ»Г‘Е’ГђВЅГђВ°Г‘ВЏ Г‘ВЃГђВёГ‘ВЃГ‘вЂљГђВµГђВјГђВ° ГђВїГ‘в‚¬ГђВѕГ‘ВЃГ‘вЂљГ‘в‚¬ГђВ°ГђВЅГ‘ВЃГ‘вЂљГђВІГђВµГђВЅГђВЅГђВѕГђВіГђВѕ ГђВ·ГђВІГ‘Ж’ГђВєГђВ° Г‘ВЃ ГђВїГ‘Ж’ГђВ»ГђВёГђВЅГђВіГђВѕГђВј.
//
// ГђВђГђВ ГђВҐГђЛњГђВўГђвЂўГђЕЎГђВўГђВЈГђВ ГђВђ:
//   Гўв‚¬Вў ГђВЎГђВёГђВЅГђВіГђВ»Г‘вЂљГђВѕГђВЅ: ГђВїГ‘Ж’ГђВ» 3D AudioSource + ГђВѕГ‘вЂљГђВґГђВµГђВ»Г‘Е’ГђВЅГ‘вЂ№ГђВ№ ГђВїГ‘Ж’ГђВ» 2D (Г‘Л†ГђВ»ГђВµГђВј/UI).
//   Гўв‚¬Вў Zero-GC ГђВІ hot path: ГђВјГђВ°Г‘ВЃГ‘ВЃГђВёГђВІГ‘вЂ№ Г‘вЂћГђВёГђВєГ‘ВЃГђВёГ‘в‚¬ГђВѕГђВІГђВ°ГђВЅГђВЅГђВѕГђВіГђВѕ Г‘в‚¬ГђВ°ГђВ·ГђВјГђВµГ‘в‚¬ГђВ°, no LINQ, no allocations.
//   Гўв‚¬Вў ГђЕёГђВѕГђВґГђВґГђВµГ‘в‚¬ГђВ¶ГђВєГђВ° 3D-ГђВїГ‘Ж’ГђВ»ГђВ° (PlayAtPoint) ГђВё 2D-ГђВїГ‘Ж’ГђВ»ГђВ° (PlayStatic2D).
//   Гўв‚¬Вў ГђвЂ™Г‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГђВµ Г‘ВЃГђВ°ГђВјГђВѕГђВіГђВѕ Г‘ВЃГ‘вЂљГђВ°Г‘в‚¬ГђВѕГђВіГђВѕ ГђВ·ГђВІГ‘Ж’ГђВєГђВ° ГђВїГ‘в‚¬ГђВё ГђВёГ‘ВЃГ‘вЂЎГђВµГ‘в‚¬ГђВїГђВ°ГђВЅГђВёГђВё ГђВїГ‘Ж’ГђВ»ГђВ°.
//   Гўв‚¬Вў AudioMixerGroup ГђВјГђВ°Г‘в‚¬Г‘Л†Г‘в‚¬Г‘Ж’Г‘вЂљГђВёГђВ·ГђВ°Г‘вЂ ГђВёГ‘ВЏ (SFX, Interface, Ambient).
//
// ГђЕѕГђЕёГђВўГђЛњГђЕ“ГђЛњГђвЂ”ГђВђГђВ¦ГђЛњГђВЇ (MX350 / CPU):
//   Гўв‚¬Вў ГђвЂ“Г‘вЂГ‘ВЃГ‘вЂљГђВєГђВёГђВ№ ГђВ»ГђВёГђВјГђВёГ‘вЂљ ГђВѕГђВґГђВЅГђВѕГђВІГ‘в‚¬ГђВµГђВјГђВµГђВЅГђВЅГ‘вЂ№Г‘вЂ¦ AudioSource (default 16, max 32).
//   Гўв‚¬Вў Linear Rolloff ГђВґГђВ»Г‘ВЏ ГђВїГ‘в‚¬ГђВµГђВґГ‘ВЃГђВєГђВ°ГђВ·Г‘Ж’ГђВµГђВјГђВѕГђВіГђВѕ ГђВ·ГђВ°Г‘вЂљГ‘Ж’Г‘вЂ¦ГђВ°ГђВЅГђВёГ‘ВЏ ГђВ±ГђВµГђВ· ГђВ»ГђВёГ‘Л†ГђВЅГђВёГ‘вЂ¦ ГђВІГ‘вЂ№Г‘вЂЎГђВёГ‘ВЃГђВ»ГђВµГђВЅГђВёГђВ№.
//   Гўв‚¬Вў ГђВќГђВµГ‘вЂљ per-frame loop Гўв‚¬вЂќ ГђВІГ‘ВЃГ‘ВЏ ГђВ»ГђВѕГђВіГђВёГђВєГђВ° ГђВІ ГђВјГђВѕГђВјГђВµГђВЅГ‘вЂљГђВµ ГђВІГ‘вЂ№ГђВ·ГђВѕГђВІГђВ° Play.
//   Гўв‚¬Вў ГђЕёГ‘Ж’ГђВ» Г‘ВЃГђВѕГђВ·ГђВґГђВ°Г‘вЂГ‘вЂљГ‘ВЃГ‘ВЏ ГђВѕГђВґГђВёГђВЅ Г‘в‚¬ГђВ°ГђВ· ГђВІ Awake, ГђВґГђВ°ГђВ»Г‘Е’Г‘Л†ГђВµ Гўв‚¬вЂќ Г‘вЂљГђВѕГђВ»Г‘Е’ГђВєГђВѕ ГђВїГђВµГ‘в‚¬ГђВµГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГђВµ.
//
// API:
//   Core audio registry PlayAtPoint(clip, position, volume, pitch)
//   Core audio registry PlayAtPoint(clip, position, volume, pitch, mixerGroup)
//   Core audio registry PlayStatic2D(clip, volume)
//   Core audio registry PlayStatic2D(clip, volume, mixerGroup)
//   Core audio registry StopAll()
//
// MIXER GROUPS:
//   ГђВќГђВ°ГђВ·ГђВЅГђВ°Г‘вЂЎГђВ°Г‘ЕЅГ‘вЂљГ‘ВЃГ‘ВЏ ГђВІ ГђВёГђВЅГ‘ВЃГђВїГђВµГђВєГ‘вЂљГђВѕГ‘в‚¬ГђВµ: SfxGroup, InterfaceGroup, AmbientGroup.
//   ГђЕёГђВѕГђВ·ГђВІГђВѕГђВ»Г‘ВЏГ‘ЕЅГ‘вЂљ Г‘вЂ ГђВµГђВЅГ‘вЂљГ‘в‚¬ГђВ°ГђВ»ГђВёГђВ·ГђВѕГђВІГђВ°ГђВЅГђВЅГђВѕ ГђВїГ‘в‚¬ГђВёГђВјГђВµГђВЅГ‘ВЏГ‘вЂљГ‘Е’ Г‘вЂћГђВёГђВ»Г‘Е’Г‘вЂљГ‘в‚¬Г‘вЂ№ (LPF ГђВґГђВ»Г‘ВЏ ГђВїГђВѕГђВґГђВІГђВѕГђВґГђВЅГђВѕГ‘ВЃГ‘вЂљГђВё,
//   distortion ГђВґГђВ»Г‘ВЏ ГђВїГђВѕГђВІГ‘в‚¬ГђВµГђВ¶ГђВґГђВµГђВЅГђВёГђВ№ Г‘Л†ГђВ»ГђВµГђВјГђВ°, etc.)
//
// NASA-PUNK ГђЕЎГђЕѕГђВќГђВўГђвЂўГђЕЎГђВЎГђВў:
//   PlayStatic2D Гўв‚¬вЂќ ГђВґГђВ»Г‘ВЏ ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ ГђВІГђВЅГ‘Ж’Г‘вЂљГ‘в‚¬ГђВё Г‘Л†ГђВ»ГђВµГђВјГђВ° ГђВєГђВѕГ‘ВЃГђВјГђВѕГђВЅГђВ°ГђВІГ‘вЂљГђВ°:
//     Гўв‚¬Вў HUD beeps, suit warnings, radio static, breath sounds.
//     Гўв‚¬Вў Spatial Blend = 0.0 (ГђВїГђВѕГђВ»ГђВЅГђВѕГ‘ВЃГ‘вЂљГ‘Е’Г‘ЕЅ 2D, "ГђВІ ГђВіГђВѕГђВ»ГђВѕГђВІГђВµ").
//   PlayAtPoint Гўв‚¬вЂќ ГђВґГђВ»Г‘ВЏ ГђВІГђВЅГђВµГ‘Л†ГђВЅГђВёГ‘вЂ¦ ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ Г‘ВЃГ‘в‚¬ГђВµГђВґГ‘вЂ№:
//     Гўв‚¬Вў Bioluminescent creature clicks, hull groans, pressure vents.
//     Гўв‚¬Вў Spatial Blend = 1.0 (ГђВїГђВѕГђВ»ГђВЅГђВѕГ‘ВЃГ‘вЂљГ‘Е’Г‘ЕЅ 3D).
//
// ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
//  ГђЕ“ГђВђГђВ ГђВЁГђВ ГђВЈГђВўГђЛњГђвЂ”ГђВђГђВ¦ГђЛњГђВЇ (ГђВєГђВ°Г‘ВЃГ‘вЂљГђВѕГђВјГђВЅГ‘вЂ№ГђВ№ ГђВєГђВѕГђВґ ГђВІ Assets/_Project):
//    Гўв‚¬Вў ГђЕ“ГђВёГ‘в‚¬ / ГђВѕГђВ±Г‘Е ГђВµГђВєГ‘вЂљГ‘вЂ№ Г‘Ж’ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВё ГўвЂ вЂ™ PlayAtPoint
//    Гўв‚¬Вў ГђВЁГђВ»ГђВµГђВј / HUD ГўвЂ вЂ™ PlayStatic2D (ГђВїГ‘Ж’ГђВ» 2D, ГђВЅГђВµ Г‘в‚¬ГђВ°ГђВ·ГђВ±Г‘в‚¬ГђВ°Г‘ВЃГ‘вЂ№ГђВІГђВ°Г‘вЂљГ‘Е’ PlayOneShot ГђВїГђВѕ MonoBehaviour)
//  ГђЕёГђВ»ГђВ°ГђВіГђВёГђВЅГ‘вЂ№ Г‘вЂљГ‘в‚¬ГђВѕГђВіГђВ°ГђВµГђВј Г‘вЂљГђВѕГђВ»Г‘Е’ГђВєГђВѕ ГђВїГ‘в‚¬ГђВё ГђВЅГђВµГђВѕГђВ±Г‘вЂ¦ГђВѕГђВґГђВёГђВјГђВѕГ‘ВЃГ‘вЂљГђВё.
// ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
//
// ESTIMATED COST:
//   Memory: ~16 + pool2D AudioSource + manager overhead
//   CPU per Play call: ~0.01ms (array scan + AudioSource setup)
//   CPU idle: 0ms (no Update)
// ============================================================================

using System;
using System.Buffers.Binary;
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
using Hecton8.World;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Audio;
using CoreAudioEvent = Hecton8.Core.AudioEvent;
using AcousticAup = Hecton8.Core.Contracts.AcousticAup;
using AcousticOcclusionJob = Hecton8.Audio.Virtualization.AcousticOcclusionJob;
using AcousticOcclusionTelemetryEntry = Hecton8.Audio.Virtualization.AcousticOcclusionTelemetryEntry;
using AcousticPortalTelemetryEntry = Hecton8.Audio.Propagation.AcousticTelemetryEntry;
using VirtualVoiceSortJob = Hecton8.Audio.Virtualization.VirtualVoiceSortJob;
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

        public static void EnsureReady()
        {
            EnsureInitialized();
        }

        public static void TouchClip(AudioClip clip, AudioResidencyDomain domain, bool decodeNow)
        {
            if (clip == null || !AudioResidencyDomainUtility.IsValid(domain))
                return;

            if (!decodeNow)
            {
                TouchClipMetadata(clip, domain);
                return;
            }

            EnsureInitialized();
            int slot = FindOrAllocateSlot(clip, domain);
            if (slot < 0)
                return;

            Entry entry = s_entries[slot];
            entry.LastUseFrame = SystemDispatcher.CurrentFrameIndex;
            entry.Domain = domain;

            if (ShouldLoadClip(clip))
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

        private static void TouchClipMetadata(AudioClip clip, AudioResidencyDomain domain)
        {
            EnsureInitialized();
            int slot = FindSlot(clip);
            if (slot < 0)
            {
                slot = FindFreeSlot();
                if (slot < 0)
                    return;

                s_entries[slot] = new Entry
                {
                    Clip = clip,
                    ClipId = clip.GetEntityId().GetHashCode(),
                    Domain = domain,
                    LastUseFrame = SystemDispatcher.CurrentFrameIndex,
                    EstimatedBytes = 0L,
                    Resident = false
                };
                return;
            }

            Entry entry = s_entries[slot];
            entry.LastUseFrame = SystemDispatcher.CurrentFrameIndex;
            entry.Domain = domain;
            s_entries[slot] = entry;
        }

        private static int FindFreeSlot()
        {
            if (s_entries == null)
                return -1;

            for (int i = 0; i < s_entries.Length; i++)
            {
                if (s_entries[i].Clip == null)
                    return i;
            }

            return -1;
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
                ClipId = clip.GetEntityId().GetHashCode(),
                Domain = domain,
                LastUseFrame = SystemDispatcher.CurrentFrameIndex,
                EstimatedBytes = 0L,
                Resident = false
            };
            return slot;
        }

        private static int FindSlot(AudioClip clip)
        {
            if (clip == null || s_entries == null)
                return -1;

            int clipId = clip.GetEntityId().GetHashCode();
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
    /// ГђВ¦ГђВµГђВЅГ‘вЂљГ‘в‚¬ГђВ°ГђВ»Г‘Е’ГђВЅГ‘вЂ№ГђВ№ ГђВјГђВµГђВЅГђВµГђВґГђВ¶ГђВµГ‘в‚¬ ГђВїГ‘в‚¬ГђВѕГ‘ВЃГ‘вЂљГ‘в‚¬ГђВ°ГђВЅГ‘ВЃГ‘вЂљГђВІГђВµГђВЅГђВЅГђВѕГђВіГђВѕ ГђВ·ГђВІГ‘Ж’ГђВєГђВ° Г‘ВЃ ГђВїГ‘Ж’ГђВ»ГђВёГђВЅГђВіГђВѕГђВј.
    /// Runtime audio service accessed through the core audio registry.
    /// Zero-GC ГђВІ hot path. ГђвЂ“Г‘вЂГ‘ВЃГ‘вЂљГђВєГђВёГђВ№ ГђВ»ГђВёГђВјГђВёГ‘вЂљ ГђВѕГђВґГђВЅГђВѕГђВІГ‘в‚¬ГђВµГђВјГђВµГђВЅГђВЅГ‘вЂ№Г‘вЂ¦ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВѕГђВІ.
    /// </summary>
    public sealed class SpatialAudioManager : MonoBehaviour, IAudioService, IAudioResidencyService, ISpatialAudioImpactEmitterReadModel, ISpatialAudioWorldEmitterReadModel, ISpatialAudioListenerCaveReadModel, ISpatialAudioBinauralEmitterReadModel, IMeteorShowerAudioSink, ISpatialAudioLowPassPlayback, ISpatialAudioEnvironmentModulationSink, ISpatialAudioSfxMixerRouteReadModel, ISpatialAudioNarrativeRadioSink, ISpatialAudioInventoryRunawaySink, ISpatialAudioHarvestPlaybackSink, ISpatialAudioWeatherPlaybackSink, ISceneTransitionAudioBridge, IAudioVirtualizationService, IUpdatable, IFastTickable, ISlowTickable, ILateFrameTickable, IOriginShiftListener, IPhysicsImpactEventListener, IRepairDroneTorchAcousticListener, IFatalPressureImplosionEventListener, IGlobalRegistryHotSwapListener, IGlobalRegistryHotSwapRefListener, IServiceHeartbeat, IServiceShutdown
    {
        private static int s_x001SpatialAudioManagerSignalPushDropCount;
        private const float DefaultSeaLevelY = OceanSurfaceAtmosphereConstants.DefaultSeaLevel;
        private const float SoundSpeedWaterMetersPerSecond = HectonPhysicsContract.SoundSpeedWaterMetersPerSecondConst;
        private const float MassiveDistanceFixedAudioDelayMeters = 740f;
        private const float MassiveDistanceFixedAudioDelaySeconds = 0.5f;
        private const float ThermalShimmerMaximumPitchRatio = 0.018f;
        private const float TimeDilationAudioMinimumPitchRatio = 0.72f;
        private const int SurvivalAmbientOutputSampleRate = 22050;
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
        private const float MasterDepthPressureOpenY = -50f;
        private const float MasterDepthPressureFullY = -300f;
        private const float MasterDepthPressureYRangeInv = 1f / (MasterDepthPressureFullY - MasterDepthPressureOpenY);
        private const float MasterDepthPressureCutoffOpenHertz = 22000f;
        private const float MasterDepthPressureCutoffFullHertz = 400f;
        private const float MasterDepthPressureCutoffOpenLog = 9.998798f;
        private const float MasterDepthPressureCutoffFullLog = 5.991465f;
        private const float MasterDepthPressureLowPassCompactSharpness = 3f;
        private const float MasterDepthPressureLowPassSharpness = 6f;
        private const float StereoPanDistanceNormalizationMeters = 15f;
        private const int MaxImpactRadarEmitters = 16;
        private const float ImpactEmitterLifetimeMinSeconds = 0.18f;
        private const float ImpactEmitterLifetimeMaxSeconds = 0.42f;
        private const float ImpactEmitterAmplitudeScale = 0.75f;
        private const float ImpactEmitterMinimumAmplitude = 0.02f;
        private const uint AcousticImpulseFlagLeviathan = 1u << 1;
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
        private const int AudioClipRouteCacheCapacity = 512;
        private const byte AudioClipRouteFlagThreat = 1 << 0;
        private const byte AudioClipRouteFlagBed = 1 << 1;
        private const byte AudioClipRouteFlagLeviathanRoar = 1 << 2;
        private const byte AudioClipRouteFlagBubble = 1 << 3;
        private const byte AudioVoiceCategoryNone = 0;
        private const byte AudioVoiceCategoryLeviathanRoar = 1;
        private const byte AudioVoiceCategoryBubble = 2;
        private const int MaxLeviathanRoarVoices = 3;
        private const int MaxBubbleVoices = 10;
        private const int UnityAudioPriorityCritical = 0;
        private const int UnityAudioPriorityThreat = 24;
        private const int UnityAudioPriorityDefaultWorld = 128;
        private const int UnityAudioPriorityAmbientBed = 224;
        private const int UnityAudioPriorityLowAudibilityPenalty = 24;
        private const int UnityAudioPriorityProxyTierPenalty = 24;
        private const int UnityAudioPriorityHighAudibilityBonus = 16;
        private const int MaxListenerContainingCaveVolumes = 8;
        private const int MaxActiveCaveVolumesForAudio = 32;
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

        /// <summary>
        /// Resolve-or-create the sole GlobalRegistry.Audio / AudioVirtualization owner.
        /// Bootstrap previously only GetComponentInChildren'd an authored child; prefab exists
        /// (PFB_SpatialAudioManagerRoot) but is not parented under GameBootstrapper in player
        /// builds, so the Audio node stayed EXEMPT and NoOpAudio filled the slot.
        /// </summary>
        public static SpatialAudioManager EnsureRuntimeInstance()
        {
            SpatialAudioManager active = ActiveRuntimeInstance;
            if (IsSpatialAudioRuntimeUsable(active))
                return active;

            IAudioService registered = GlobalRegistry.Audio;
            SpatialAudioManager asManager = registered as SpatialAudioManager;
            if (IsSpatialAudioRuntimeUsable(asManager))
                return asManager;

            if (!Application.isPlaying)
                return null;

            // Player-build construction path: no authored/bootstrap instance reachable.
            // Must construct in player builds when bootstrap reorders or skips registration.
            GameObject runtimeRoot = new GameObject("[SpatialAudioManager]"); // COLD ALLOC
            SpatialAudioManager created = runtimeRoot.AddComponent<SpatialAudioManager>();
            created.InitializeService();
            return created;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]

        private static void ResetStaticState()
        {

            SpatialAudioManager activeRuntime = ActiveRuntimeInstance;
            if (activeRuntime != null)
                activeRuntime.ShutdownServiceState(releaseRuntimeResources: true);

            ActiveRuntimeInstance = null;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorReloadHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= DisposeActiveRuntimeForEditorReload;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += DisposeActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting -= DisposeActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.quitting += DisposeActiveRuntimeForEditorReload;
            UnityEditor.EditorApplication.playModeStateChanged -= HandleEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += HandleEditorPlayModeStateChanged;
        }

        private static void HandleEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode ||
                state == UnityEditor.PlayModeStateChange.EnteredEditMode)
            {
                DisposeActiveRuntimeForEditorReload();
            }
        }

        private static void DisposeActiveRuntimeForEditorReload()
        {
            SpatialAudioManager activeRuntime = ActiveRuntimeInstance;
            if (activeRuntime != null)
                activeRuntime.ShutdownServiceState(releaseRuntimeResources: true);
        }
#endif
        private const int MaxDelayedAudioEvents = 16;
        private const Allocator DataVaultExemptSceneScratchAllocator = Allocator.Persistent;
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
        private const int SurvivalVirtualPhysicalVoices = VirtualVoiceUtility.SurvivalPhysicalVoiceCount;
        private const int VirtualVoiceQualityHysteresisSlowTicks = 25;
        private const int VirtualVoiceBlackBoxFrameCount = 300;
        private const float VirtualVoiceStealFadeSeconds = 0.01f;
        private const string VirtualVoiceDumpRelativePath = "Docs/AgentLogs/Dump_ACOUSTIC_SURGEON.bin";
        private const BufferID SpatialAudioVirtualVoiceTuningBufferId = BufferID.SpatialAudioVirtualVoiceTuning;
        private const BufferID SpatialAudioVirtualVoiceWritePoolBufferId = BufferID.SpatialAudioVirtualVoiceWritePool;
        private const BufferID SpatialAudioVirtualVoiceSortPoolBufferId = BufferID.SpatialAudioVirtualVoiceSortPool;
        private const BufferID SpatialAudioVirtualVoiceDtoPoolBufferId = BufferID.SpatialAudioVirtualVoiceDtoPool;
        private const BufferID SpatialAudioVirtualVoiceSortKeyPoolBufferId = BufferID.SpatialAudioVirtualVoiceSortKeyPool;
        private const BufferID SpatialAudioAcousticSourceWritePoolBufferId = BufferID.SpatialAudioAcousticSourceWritePool;
        private const BufferID SpatialAudioAcousticSourceSortPoolBufferId = BufferID.SpatialAudioAcousticSourceSortPool;
        private const BufferID SpatialAudioAcousticPreviousAupWritePoolBufferId = BufferID.SpatialAudioAcousticPreviousAupWritePool;
        private const BufferID SpatialAudioAcousticPreviousAupSortPoolBufferId = BufferID.SpatialAudioAcousticPreviousAupSortPool;
        private const BufferID SpatialAudioAcousticDspOutputPoolBufferId = BufferID.SpatialAudioAcousticDspOutputPool;
        private static readonly ulong VirtualVoiceAppendMutationGuardMask =
            AudioVaultMutationGuardBit(SpatialAudioVirtualVoiceWritePoolBufferId) |
            AudioVaultMutationGuardBit(SpatialAudioVirtualVoiceDtoPoolBufferId) |
            AudioVaultMutationGuardBit(SpatialAudioAcousticSourceWritePoolBufferId) |
            AudioVaultMutationGuardBit(SpatialAudioAcousticPreviousAupWritePoolBufferId);
        private const BufferID SpatialAudioAcousticMaterialRowsBufferId = BufferID.SpatialAudioAcousticMaterialRows;
        private const BufferID SpatialAudioAcousticSelectedSourcePoolBufferId = BufferID.SpatialAudioAcousticSelectedSourcePool;
        private const BufferID SpatialAudioAcousticSelectedPreviousAupPoolBufferId = BufferID.SpatialAudioAcousticSelectedPreviousAupPool;
        private const BufferID SpatialAudioPortalOpenSetBufferId = BufferID.SpatialAudioPortalOpenSet;
        private const BufferID SpatialAudioPortalClosedSetBufferId = BufferID.SpatialAudioPortalClosedSet;
        private static readonly ulong AcousticPortalWorkMutationGuardMask =
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalNodes) |
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalEdges) |
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalResult) |
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalCosts) |
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalCameFrom) |
            AudioVaultMutationGuardBit(BufferID.SpatialAudioPortalStates);
        private static readonly ulong AcousticPortalScratchMutationGuardMask =
            AudioVaultMutationGuardBit(SpatialAudioPortalOpenSetBufferId) |
            AudioVaultMutationGuardBit(SpatialAudioPortalClosedSetBufferId);
        private static readonly ulong AcousticPortalPathMutationGuardMask =
            AcousticPortalWorkMutationGuardMask |
            AcousticPortalScratchMutationGuardMask;
        private const BufferID SpatialAudioPreviousVelocityAupsBufferId = BufferID.SpatialAudioPreviousVelocityAups;
        private const BufferID SpatialAudioPreviousVelocityAupFramesBufferId = BufferID.SpatialAudioPreviousVelocityAupFrames;
        private static readonly ulong PreviousVelocityAupMutationGuardMask =
            AudioVaultMutationGuardBit(SpatialAudioPreviousVelocityAupsBufferId) |
            AudioVaultMutationGuardBit(SpatialAudioPreviousVelocityAupFramesBufferId);
        private const BufferID SpatialAudioAcousticVoxelSdfTexture3DBufferId = BufferID.SpatialAudioManager_SpatialAudioAcousticVoxelSdfTexture3DBufferId;
        private static readonly ulong AcousticOcclusionSdfSnapshotMutationGuardMask =
            AudioVaultMutationGuardBit(SpatialAudioAcousticVoxelSdfTexture3DBufferId);
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
        private const string AcousticPortalDumpRelativePath = "Docs/AgentLogs/Dump_1307_Acoustics.bin";
        private const uint AcousticPortalDumpMagic = 0x41313330u;
        private const uint AcousticPortalDumpVersion = 2u;
        private const int AcousticPortalDumpHeaderBytes = 20;
        private const int AcousticPortalTelemetryEntryBytes = 64;
        private const uint AcousticPortalFailureNone = 0u;
        private const uint AcousticPortalFailureNonFiniteResult = 1u;
        private const uint AcousticPortalFailureHandleInvalid = 2u;
        private const uint AcousticPortalFailureLockOrCapacity = 3u;
        private const uint AcousticPortalFailureDumpIo = 4u;
        private const uint SpatialAudioFailureVirtualVoiceDumpIo = 0xA1307005u;
        private const uint SpatialAudioFailureVirtualVoiceSortSchedule = 0xA1307006u;
        private const uint SpatialAudioFailureAcousticOcclusionSchedule = 0xA1307007u;
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
#endif
        private static readonly uint _audioEventQueueDropWarningHash = unchecked((uint)LocHash.Compute("Audio.EventQueue.Drop"));
        private static readonly uint _audioEventQueueOverflowContextHash = unchecked((uint)LocHash.Compute("Audio.EventQueue.Overflow"));
        private static readonly uint _audioEventBadDataContextHash = unchecked((uint)LocHash.Compute("Audio.EventQueue.BadData"));
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
            private byte _pad0;
            [FieldOffset(49)]
            private byte _pad1;
            [FieldOffset(50)]
            private byte _pad2;
            [FieldOffset(51)]
            private byte _pad3;
            [FieldOffset(52)]
            private byte _pad4;
            [FieldOffset(53)]
            private byte _pad5;
            [FieldOffset(54)]
            private byte _pad6;
            [FieldOffset(55)]
            private byte _pad7;
            [FieldOffset(56)]
            private byte _pad8;
            [FieldOffset(57)]
            private byte _pad9;
            [FieldOffset(58)]
            private byte _pad10;
            [FieldOffset(59)]
            private byte _pad11;
            [FieldOffset(60)]
            private byte _pad12;
            [FieldOffset(61)]
            private byte _pad13;
            [FieldOffset(62)]
            private byte _pad14;
            [FieldOffset(63)]
            private byte _pad15;
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
            private byte _pad1;
            [FieldOffset(91)]
            private byte _pad2;
            [FieldOffset(92)]
            private byte _pad3;
            [FieldOffset(93)]
            private byte _pad4;
            [FieldOffset(94)]
            private byte _pad5;
            [FieldOffset(95)]
            private byte _pad6;
            [FieldOffset(96)]
            private byte _pad7;
            [FieldOffset(97)]
            private byte _pad8;
            [FieldOffset(98)]
            private byte _pad9;
            [FieldOffset(99)]
            private byte _pad10;
            [FieldOffset(100)]
            private byte _pad11;
            [FieldOffset(101)]
            private byte _pad12;
            [FieldOffset(102)]
            private byte _pad13;
            [FieldOffset(103)]
            private byte _pad14;
            [FieldOffset(104)]
            private byte _pad15;
            [FieldOffset(105)]
            private byte _pad16;
            [FieldOffset(106)]
            private byte _pad17;
            [FieldOffset(107)]
            private byte _pad18;
            [FieldOffset(108)]
            private byte _pad19;
            [FieldOffset(109)]
            private byte _pad20;
            [FieldOffset(110)]
            private byte _pad21;
            [FieldOffset(111)]
            private byte _pad22;
            [FieldOffset(112)]
            private byte _pad23;
            [FieldOffset(113)]
            private byte _pad24;
            [FieldOffset(114)]
            private byte _pad25;
            [FieldOffset(115)]
            private byte _pad26;
            [FieldOffset(116)]
            private byte _pad27;
            [FieldOffset(117)]
            private byte _pad28;
            [FieldOffset(118)]
            private byte _pad29;
            [FieldOffset(119)]
            private byte _pad30;
            [FieldOffset(120)]
            private byte _pad31;
            [FieldOffset(121)]
            private byte _pad32;
            [FieldOffset(122)]
            private byte _pad33;
            [FieldOffset(123)]
            private byte _pad34;
            [FieldOffset(124)]
            private byte _pad35;
            [FieldOffset(125)]
            private byte _pad36;
            [FieldOffset(126)]
            private byte _pad37;
            [FieldOffset(127)]
            private byte _pad38;
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
            private byte _padByte0;
            [FieldOffset(194)]
            private byte _padByte1;
            [FieldOffset(195)]
            private byte _padByte2;
            [FieldOffset(196)]
            private byte _padByte3;
            [FieldOffset(197)]
            private byte _padByte4;
            [FieldOffset(198)]
            private byte _padByte5;
            [FieldOffset(199)]
            private byte _padByte6;
            [FieldOffset(200)]
            private byte _padByte7;
            [FieldOffset(201)]
            private byte _padByte8;
            [FieldOffset(202)]
            private byte _padByte9;
            [FieldOffset(203)]
            private byte _padByte10;
            [FieldOffset(204)]
            private byte _padByte11;
            [FieldOffset(205)]
            private byte _padByte12;
            [FieldOffset(206)]
            private byte _padByte13;
            [FieldOffset(207)]
            private byte _padByte14;
            [FieldOffset(208)]
            private byte _padByte15;
            [FieldOffset(209)]
            private byte _padByte16;
            [FieldOffset(210)]
            private byte _padByte17;
            [FieldOffset(211)]
            private byte _padByte18;
            [FieldOffset(212)]
            private byte _padByte19;
            [FieldOffset(213)]
            private byte _padByte20;
            [FieldOffset(214)]
            private byte _padByte21;
            [FieldOffset(215)]
            private byte _padByte22;
            [FieldOffset(216)]
            private byte _padByte23;
            [FieldOffset(217)]
            private byte _padByte24;
            [FieldOffset(218)]
            private byte _padByte25;
            [FieldOffset(219)]
            private byte _padByte26;
            [FieldOffset(220)]
            private byte _padByte27;
            [FieldOffset(221)]
            private byte _padByte28;
            [FieldOffset(222)]
            private byte _padByte29;
            [FieldOffset(223)]
            private byte _padByte30;
            [FieldOffset(224)]
            private byte _padByte31;
            [FieldOffset(225)]
            private byte _padByte32;
            [FieldOffset(226)]
            private byte _padByte33;
            [FieldOffset(227)]
            private byte _padByte34;
            [FieldOffset(228)]
            private byte _padByte35;
            [FieldOffset(229)]
            private byte _padByte36;
            [FieldOffset(230)]
            private byte _padByte37;
            [FieldOffset(231)]
            private byte _padByte38;
            [FieldOffset(232)]
            private byte _padByte39;
            [FieldOffset(233)]
            private byte _padByte40;
            [FieldOffset(234)]
            private byte _padByte41;
            [FieldOffset(235)]
            private byte _padByte42;
            [FieldOffset(236)]
            private byte _padByte43;
            [FieldOffset(237)]
            private byte _padByte44;
            [FieldOffset(238)]
            private byte _padByte45;
            [FieldOffset(239)]
            private byte _padByte46;
            [FieldOffset(240)]
            private byte _padByte47;
            [FieldOffset(241)]
            private byte _padByte48;
            [FieldOffset(242)]
            private byte _padByte49;
            [FieldOffset(243)]
            private byte _padByte50;
            [FieldOffset(244)]
            private byte _padByte51;
            [FieldOffset(245)]
            private byte _padByte52;
            [FieldOffset(246)]
            private byte _padByte53;
            [FieldOffset(247)]
            private byte _padByte54;
            [FieldOffset(248)]
            private byte _padByte55;
            [FieldOffset(249)]
            private byte _padByte56;
            [FieldOffset(250)]
            private byte _padByte57;
            [FieldOffset(251)]
            private byte _padByte58;
            [FieldOffset(252)]
            private byte _padByte59;
            [FieldOffset(253)]
            private byte _padByte60;
            [FieldOffset(254)]
            private byte _padByte61;
            [FieldOffset(255)]
            private byte _padByte62;
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
            private byte _pad0;
            [FieldOffset(73)]
            private byte _pad1;
            [FieldOffset(74)]
            private byte _pad2;
            [FieldOffset(75)]
            private byte _pad3;
            [FieldOffset(76)]
            private byte _pad4;
            [FieldOffset(77)]
            private byte _pad5;
            [FieldOffset(78)]
            private byte _pad6;
            [FieldOffset(79)]
            private byte _pad7;
            [FieldOffset(80)]
            private byte _pad8;
            [FieldOffset(81)]
            private byte _pad9;
            [FieldOffset(82)]
            private byte _pad10;
            [FieldOffset(83)]
            private byte _pad11;
            [FieldOffset(84)]
            private byte _pad12;
            [FieldOffset(85)]
            private byte _pad13;
            [FieldOffset(86)]
            private byte _pad14;
            [FieldOffset(87)]
            private byte _pad15;
            [FieldOffset(88)]
            private byte _pad16;
            [FieldOffset(89)]
            private byte _pad17;
            [FieldOffset(90)]
            private byte _pad18;
            [FieldOffset(91)]
            private byte _pad19;
            [FieldOffset(92)]
            private byte _pad20;
            [FieldOffset(93)]
            private byte _pad21;
            [FieldOffset(94)]
            private byte _pad22;
            [FieldOffset(95)]
            private byte _pad23;
            [FieldOffset(96)]
            private byte _pad24;
            [FieldOffset(97)]
            private byte _pad25;
            [FieldOffset(98)]
            private byte _pad26;
            [FieldOffset(99)]
            private byte _pad27;
            [FieldOffset(100)]
            private byte _pad28;
            [FieldOffset(101)]
            private byte _pad29;
            [FieldOffset(102)]
            private byte _pad30;
            [FieldOffset(103)]
            private byte _pad31;
            [FieldOffset(104)]
            private byte _pad32;
            [FieldOffset(105)]
            private byte _pad33;
            [FieldOffset(106)]
            private byte _pad34;
            [FieldOffset(107)]
            private byte _pad35;
            [FieldOffset(108)]
            private byte _pad36;
            [FieldOffset(109)]
            private byte _pad37;
            [FieldOffset(110)]
            private byte _pad38;
            [FieldOffset(111)]
            private byte _pad39;
            [FieldOffset(112)]
            private byte _pad40;
            [FieldOffset(113)]
            private byte _pad41;
            [FieldOffset(114)]
            private byte _pad42;
            [FieldOffset(115)]
            private byte _pad43;
            [FieldOffset(116)]
            private byte _pad44;
            [FieldOffset(117)]
            private byte _pad45;
            [FieldOffset(118)]
            private byte _pad46;
            [FieldOffset(119)]
            private byte _pad47;
            [FieldOffset(120)]
            private byte _pad48;
            [FieldOffset(121)]
            private byte _pad49;
            [FieldOffset(122)]
            private byte _pad50;
            [FieldOffset(123)]
            private byte _pad51;
            [FieldOffset(124)]
            private byte _pad52;
            [FieldOffset(125)]
            private byte _pad53;
            [FieldOffset(126)]
            private byte _pad54;
            [FieldOffset(127)]
            private byte _pad55;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  SERVICE REGISTRY
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђвЂњГђВ»ГђВѕГђВ±ГђВ°ГђВ»Г‘Е’ГђВЅГ‘вЂ№ГђВ№ ГђВґГђВѕГ‘ВЃГ‘вЂљГ‘Ж’ГђВї ГђВє ГђВјГђВµГђВЅГђВµГђВґГђВ¶ГђВµГ‘в‚¬Г‘Ж’. ГђВќГђВµ Г‘ВЃГђВѕГђВ·ГђВґГђВ°Г‘вЂГ‘вЂљ ГђВѕГђВ±Г‘Е ГђВµГђВєГ‘вЂљ ГђВ°ГђВІГ‘вЂљГђВѕГђВјГђВ°Г‘вЂљГђВёГ‘вЂЎГђВµГ‘ВЃГђВєГђВё Гўв‚¬вЂќ
        /// ГђВјГђВµГђВЅГђВµГђВґГђВ¶ГђВµГ‘в‚¬ ГђВґГђВѕГђВ»ГђВ¶ГђВµГђВЅ ГђВ±Г‘вЂ№Г‘вЂљГ‘Е’ Г‘в‚¬ГђВ°ГђВ·ГђВјГђВµГ‘вЂ°Г‘вЂГђВЅ ГђВЅГђВ° Г‘ВЃГ‘вЂ ГђВµГђВЅГђВµ ГђВІГ‘в‚¬Г‘Ж’Г‘вЂЎГђВЅГ‘Ж’Г‘ЕЅ ГђВёГђВ»ГђВё Г‘вЂЎГђВµГ‘в‚¬ГђВµГђВ· bootstrap.
        /// </summary>
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        //  INSPECTOR CONFIGURATION
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        [Header("Pool Configuration Гўв‚¬вЂќ 3D World")]
        [Tooltip("ГђЕЎГђВѕГђВ»ГђВёГ‘вЂЎГђВµГ‘ВЃГ‘вЂљГђВІГђВѕ AudioSource ГђВІ ГђВїГ‘Ж’ГђВ»ГђВµ. 16 ГђВѕГђВїГ‘вЂљГђВёГђВјГђВ°ГђВ»Г‘Е’ГђВЅГђВѕ ГђВґГђВ»Г‘ВЏ MX350. Max 32.")]
        [Range(4, 32)]
        [SerializeField] private int _poolSize = 32;

        [Header("Pool Configuration Гўв‚¬вЂќ 2D Helmet / UI")]
        [Tooltip("ГђвЂњГђВѕГђВ»ГђВѕГ‘ВЃГђВ° ГђВґГђВ»Г‘ВЏ ГђВєГђВѕГ‘в‚¬ГђВѕГ‘вЂљГђВєГђВёГ‘вЂ¦ UI/Г‘Л†ГђВ»ГђВµГђВјГђВЅГ‘вЂ№Г‘вЂ¦ ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ; ГђВїГђВµГ‘в‚¬ГђВµГђВєГ‘в‚¬Г‘вЂ№Г‘вЂљГђВёГђВµ Г‘вЂЎГђВµГ‘в‚¬ГђВµГђВ· ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГђВµ.")]
        [Range(2, 16)]
        [SerializeField] private int _pool2DSize = 8;

        [Header("3D Audio Defaults")]
        [Tooltip("ГђЕ“ГђВёГђВЅГђВёГђВјГђВ°ГђВ»Г‘Е’ГђВЅГђВ°Г‘ВЏ ГђВґГђВёГ‘ВЃГ‘вЂљГђВ°ГђВЅГ‘вЂ ГђВёГ‘ВЏ 3D ГђВ·ГђВІГ‘Ж’ГђВєГђВ° (ГђВјГђВµГ‘вЂљГ‘в‚¬Г‘вЂ№).")]
        [SerializeField] private float _minDistance = 1f;

        [Tooltip("ГђЕ“ГђВ°ГђВєГ‘ВЃГђВёГђВјГђВ°ГђВ»Г‘Е’ГђВЅГђВ°Г‘ВЏ ГђВґГђВёГ‘ВЃГ‘вЂљГђВ°ГђВЅГ‘вЂ ГђВёГ‘ВЏ 3D ГђВ·ГђВІГ‘Ж’ГђВєГђВ° (ГђВјГђВµГ‘вЂљГ‘в‚¬Г‘вЂ№). ГђвЂ”ГђВ° ГђВЅГђВµГђВ№ ГђВ·ГђВІГ‘Ж’ГђВє ГђВЅГђВµ Г‘ВЃГђВ»Г‘вЂ№Г‘Л†ГђВµГђВЅ.")]
        [SerializeField] private float _maxDistance = 50f;

        [Header("Mixer Groups (ГђВЅГђВ°ГђВ·ГђВЅГђВ°Г‘вЂЎГђВёГ‘вЂљГ‘Е’ ГђВёГђВ· AudioMixer)")]
        [Tooltip("ГђвЂњГ‘в‚¬Г‘Ж’ГђВїГђВїГђВ° ГђВґГђВ»Г‘ВЏ SFX (Г‘ВЃГ‘Ж’Г‘вЂ°ГђВµГ‘ВЃГ‘вЂљГђВІГђВ°, ГђВјГђВµГ‘вЂ¦ГђВ°ГђВЅГђВёГђВ·ГђВјГ‘вЂ№, ГђВѕГђВєГ‘в‚¬Г‘Ж’ГђВ¶ГђВµГђВЅГђВёГђВµ).")]
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Tooltip("ГђвЂњГ‘в‚¬Г‘Ж’ГђВїГђВїГђВ° ГђВґГђВ»Г‘ВЏ ГђВёГђВЅГ‘вЂљГђВµГ‘в‚¬Г‘вЂћГђВµГђВ№Г‘ВЃГђВ° ГђВё ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ ГђВІГђВЅГ‘Ж’Г‘вЂљГ‘в‚¬ГђВё Г‘Л†ГђВ»ГђВµГђВјГђВ°.")]
        [SerializeField] private AudioMixerGroup _interfaceGroup;

        [Tooltip("Optional pre-authored DSP route for encrypted PDA voiceover bit-crush. Falls back to Interface.")]
        [SerializeField] private AudioMixerGroup _encryptedVoiceGroup;

        [Tooltip("ГђвЂњГ‘в‚¬Г‘Ж’ГђВїГђВїГђВ° ГђВґГђВ»Г‘ВЏ Г‘ВЌГђВјГђВ±ГђВёГђВµГђВЅГ‘вЂљГђВ° (ГђВїГђВѕГђВґГђВІГђВѕГђВґГђВЅГ‘вЂ№ГђВ№ ГђВіГ‘Ж’ГђВ», ГђВґГђВ°ГђВІГђВ»ГђВµГђВЅГђВёГђВµ, etc).")]
        [SerializeField] private AudioMixerGroup _ambientGroup;

        [Tooltip("Threat bus for dominant hostile cues such as leviathan roars. Falls back to SFX when unassigned.")]
        [SerializeField] private AudioMixerGroup _threatGroup;

        [Tooltip("Bed bus for ambient world layers that should duck under threat activity. Falls back to Ambient when unassigned.")]
        [SerializeField] private AudioMixerGroup _bedGroup;

        [Tooltip("Optional mixer override for threat-driven bed ducking. If null, the bed or ambient mixer is used.")]
        [SerializeField] private AudioMixer _routingMixer;

        [Header("Clip Route Overrides")]
        [Tooltip("Optional clips that must resolve through the Threat route even when called through a generic SFX path.")]
        [SerializeField] private AudioClip[] _threatRouteClips;

        [Tooltip("Optional clips that must resolve through the Ambient Bed route even when called through a generic SFX path.")]
        [SerializeField] private AudioClip[] _bedRouteClips;

        [Tooltip("Optional clips that are capped as leviathan roars regardless of caller path.")]
        [SerializeField] private AudioClip[] _leviathanRoarRouteClips;

        [Tooltip("Optional clips that are capped as bubble-boil events regardless of caller path.")]
        [SerializeField] private AudioClip[] _bubbleRouteClips;

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

        [Tooltip("Exposed mixer parameter for narrative black-box bitcrusher mix. 0 means clean, 1 means fully degraded.")]
        [SerializeField] private string _narrativeRadioBitCrushMixParameter = "NarrativeRadioBitCrushMix";

        [Tooltip("Exposed mixer parameter for narrative black-box pitch shift in cents.")]
        [SerializeField] private string _narrativeRadioPitchShiftCentsParameter = "NarrativeRadioPitchShiftCents";

        [Tooltip("Exposed mixer parameter for brownout-driven global pitch multiplier.")]
        [SerializeField] private string _brownoutPitchMultiplierParameter = "BrownoutPitchMultiplier";

        [Tooltip("Exposed mixer parameter for depth-pressure master low-pass cutoff in hertz.")]
        [SerializeField] private string _masterDepthPressureLowPassCutoffParameter = "MasterDepthPressureLowPassCutoffHz";

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
        [Tooltip("One-based EventID table drained from the fixed AudioEvent ingress ring. Slot 0 resolves EventID 1.")]
        [SerializeField] private AudioClip[] _audioEventClipTable;

        [Header("Authored Pool Roots")]
        [Tooltip("Pre-authored root containing world-space AudioSource + AudioLowPassFilter pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _worldPoolRoot;

        [Tooltip("Pre-authored root containing 2D helmet/UI AudioSource pool nodes. Runtime AddComponent is forbidden.")]
        [SerializeField] private Transform _helmetPoolRoot;

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  POOL DATA Гўв‚¬вЂќ Fixed arrays, zero allocation
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>ГђЕёГ‘Ж’ГђВ» AudioSource ГђВєГђВѕГђВјГђВїГђВѕГђВЅГђВµГђВЅГ‘вЂљГђВѕГђВІ. ГђВ ГђВ°ГђВ·ГђВјГђВµГ‘в‚¬ Г‘вЂћГђВёГђВєГ‘ВЃГђВёГ‘в‚¬ГђВѕГђВІГђВ°ГђВЅ ГђВїГђВѕГ‘ВЃГђВ»ГђВµ Awake.</summary>
        private AudioSource[] _pool;

        /// <summary>ГђвЂ™Г‘в‚¬ГђВµГђВјГ‘ВЏ ГђВЅГђВ°Г‘вЂЎГђВ°ГђВ»ГђВ° ГђВІГђВѕГ‘ВЃГђВїГ‘в‚¬ГђВѕГђВёГђВ·ГђВІГђВµГђВґГђВµГђВЅГђВёГ‘ВЏ ГђВєГђВ°ГђВ¶ГђВґГђВѕГђВіГђВѕ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВ° ((float)SystemDispatcher.CurrentUnscaledTimeSeconds).
        /// ГђЛњГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·Г‘Ж’ГђВµГ‘вЂљГ‘ВЃГ‘ВЏ ГђВґГђВ»Г‘ВЏ ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГ‘ВЏ Г‘ВЃГђВ°ГђВјГђВѕГђВіГђВѕ Г‘ВЃГ‘вЂљГђВ°Г‘в‚¬ГђВѕГђВіГђВѕ ГђВ·ГђВІГ‘Ж’ГђВєГђВ°.</summary>
        private float[] _startTimes;

        /// <summary>ГђЕёГ‘Ж’ГђВ» 2D AudioSource (spatialBlend = 0).</summary>
        private AudioSource[] _pool2D;

        /// <summary>ГђвЂ™Г‘в‚¬ГђВµГђВјГ‘ВЏ Г‘ВЃГ‘вЂљГђВ°Г‘в‚¬Г‘вЂљГђВ° ГђВґГђВ»Г‘ВЏ ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГ‘ВЏ ГђВІ 2D-ГђВїГ‘Ж’ГђВ»ГђВµ.</summary>
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
        private Vector3[] _activeWorldRuntimePositions;
        private int[] _activeWorldRuntimePositionFrames;
        private AbsoluteUniversePosition[] _activeWorldAups;
        private int[] _activeWorldAupFrames;
        private int[] _activeWorldIndices;
        private int[] _activeWorldSlots;
        private int _activeWorldCount;
        private float _pendingSpatialAudioTickDeltaTime;
        private bool _hasPendingSpatialAudioTick;
        private bool _registeredUpdatable;
        private bool _registeredFastTickable;
        private bool _registeredSlowTickable;
        private bool _registeredLateFrameTickable;
        private bool _registeredOriginShiftListener;
        private bool _acousticOcclusionRuntimeAcquired;
        private IFoveatedSimulationDirector _foveatedSimulationDirector;
        private IDataVault _dataVault;
        private IPhysicsStateEventService _physicsStateEvents;
        private Transform _listenerTransform;
        private AbsoluteUniversePosition _previousListenerVelocityAup;
        private bool _hasPreviousListenerVelocityAup;
        private BinauralEmitterTelemetry _dominantBinauralEmitter;
        private VaultGenerationHandle<float> _acousticRadarIntensityBinsHandle;
        private VaultGenerationHandle<float> _acousticRadarGridHandle;
        private WorldCaveDirector _worldCaveDirector;
        private GraphicsBuffer _acousticRadarGridBufferA;
        private GraphicsBuffer _acousticRadarGridBufferB;
        private GraphicsBuffer _activeAcousticRadarGridBuffer;
        private int _acousticRadarGridUploadIndex;
        private bool _acousticRadarGridDirty;
        // COLD ALLOC: float[360] - CPU mirror for acoustic radar texture uploads - owner: SpatialAudioManager
        private float[] _acousticRadarIntensityUploadScratch;
        // COLD ALLOC: float[32] - CPU mirror for acoustic radar grid GraphicsBuffer uploads - owner: SpatialAudioManager
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
        private readonly HectonVoxelVolume[] _caveVolumeBuffer = new HectonVoxelVolume[MaxActiveCaveVolumesForAudio]; // COLD ALLOC: HectonVoxelVolume[32] - cave AABB query scratch buffer - owner: SpatialAudioManager
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
        [SerializeField, Tooltip("Optional pre-authored TMP label for development audio residency. Runtime overlay object creation is forbidden.")]
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
        private float _lastNarrativeRadioBitCrushMix01 = float.PositiveInfinity;
        private float _lastNarrativeRadioPitchShiftCents = float.PositiveInfinity;
        private float _lastThreatBusDuckDb = float.PositiveInfinity;
        private float _masterDepthPressureLowPassCutoffHz = MasterDepthPressureCutoffOpenHertz;
        private float _lastMasterDepthPressureLowPassCutoffHz = -1f;
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
        private bool _hasNarrativeRadioBitCrushMixParameter;
        private bool _hasNarrativeRadioPitchShiftCentsParameter;
        private bool _hasBrownoutPitchMultiplierParameter;
        private bool _hasMasterDepthPressureLowPassCutoffParameter;
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
        private IPlayerMovementTraumaSink _listenerPlayerMovementTrauma;
        private IHectonOceanKinematicsService _cachedOceanKinematicsService;
        private int _delayedAudioIngressCount;
        private int _delayedAudioIngressHead;
        private DelayedAudioEvent[] _delayedAudioIngress;
        private DelayedAudioEvent[] _pendingDelayedAudioEvents;
        private int _pendingDelayedAudioEventCount;
        private int _audioEventQueueCount;
        private int _audioEventQueueDroppedCount;
        private int _lastAudioEventQueueOverflowTelemetryFrame = -1;
        private int _lastAudioEventBadDataTelemetryFrame = -1;
        private int _audioEventQueueHead;
        private CoreAudioEvent[] _audioEventQueue;
        private uint[] _audioClipHashKeys;
        private int[] _audioClipHashTableIndices;
        private int _audioClipHashMask;
        private int _audioClipHashCount;
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
        private VaultGenerationHandle<AbsoluteUniversePosition> _previousVelocityAupsHandle;
        private VaultGenerationHandle<int> _previousVelocityAupFramesHandle;
        private VaultGenerationHandle<ScalabilityStateDTO> _virtualVoiceScalabilityStateHandle;
        private VaultGenerationHandle<RollbackAudioSuppressionDTO> _virtualVoiceRollbackAudioSuppressionHandle;
        private VaultGenerationHandle<byte> _acousticVoxelSdfTexture3DHandle;
        private JobHandle _virtualVoiceSortHandle;
        private JobHandle _acousticOcclusionHandle;
        private VirtualVoiceStatistics _lastVirtualVoiceStatistics;
        private VirtualVoiceTuningSnapshot _virtualVoiceTuning = VirtualVoiceTuningSnapshot.CreateDefault();
        private int _virtualVoiceWriteCount;
        private int _virtualVoiceSortCount;
        private int _virtualVoiceDtoCount;
        private AcousticAup _virtualListenerAup;
        private AbsoluteUniversePosition _virtualPreviousListenerVelocityAup;
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
        private IDataVault _acousticMaterialRowsLockVault;
        private IDataVault _virtualVoiceSortBuffersLockVault;
        private IDataVault _acousticOcclusionBuffersLockVault;
        private IDataVault _acousticOcclusionSdfSnapshotGuardVault;
        private IDataVault _previousVelocityAupGuardVault;
        private IDataVault _acousticPortalWorkGuardVault;
        private IDataVault _acousticPortalScratchGuardVault;
        private bool _virtualVoiceSortScheduled;
        private bool _acousticOcclusionScheduled;
        private bool _acousticMaterialRowsLockedForOcclusion;
        private bool _acousticOcclusionSdfSnapshotGuardHeld;
        private bool _virtualVoiceStatisticsLockedForSort;
        private bool _virtualVoiceSortPoolLockedForSort;
        private bool _virtualVoiceSortKeyPoolLockedForSort;
        private bool _virtualVoiceSelectionsLockedForSort;
        private bool _acousticSelectedSourcePoolLockedForOcclusion;
        private bool _acousticSelectedPreviousAupPoolLockedForOcclusion;
        private bool _acousticDspOutputPoolLockedForOcclusion;
        private bool _hasVirtualListenerAup;
        private bool _hasVirtualPreviousListenerVelocityAup;
        private bool _virtualVoiceBlackBoxDumped;
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
        private VaultGenerationHandle<int> _acousticPortalOpenSetHandle;
        private VaultGenerationHandle<int> _acousticPortalClosedSetHandle;
        private VaultGenerationHandle<AcousticPortalTelemetryEntry> _acousticPortalBlackBoxHandle;
        private int _acousticPortalBlackBoxCursor;
        private bool _acousticPortalBlackBoxDumpPending;
        private Vector3[] _acousticPortalWaypointScratch;
        private int[] _acousticHabitatNodeMap;
        private int[] _acousticHabitatQueue;
        private AcousticPortalCacheEntry[] _acousticPortalCache;
        private bool _isInitialized;
        private bool _runtimeResourcesInitialized;
        private bool _eventsSubscribed;
        private bool _runtimeOwnerAborted;
        private bool _hotSwapRegistered;
        private bool _worldCaveDirectorListenerRegistered;
        private bool _physicsImpactRegistered;
        private IPlayerRuntimeContext _cachedPlayerRuntimeContext;
        private IWeatherService _cachedWeatherService;
        private IAcousticZoneReadModel _cachedAcousticZone;
        private ISurfaceWeatherReadModel _cachedSurfaceWeatherDirector;
        private IPlayerCriticalAudioSignalSink _cachedPlayerCriticalAudio;
        private IHabitatGraphService _cachedHabitatGraph;
        private float _cachedSpatialAudioQualityWeight01 = 1f;
        private int _spatialAudioPolicyRefreshFrame = SpatialAudioPolicyUninitializedFrame;
        private int _playerRuntimeContextResolveFrame = -4096;
        private int _weatherServiceResolveFrame = -4096;
        private int _acousticZoneResolveFrame = -4096;
        private int _surfaceWeatherResolveFrame = -4096;
        private int _foveatedDirectorResolveFrame = -4096;
        private readonly ImpactEmitterSample[] _impactEmitters = new ImpactEmitterSample[MaxImpactRadarEmitters]; // COLD ALLOC: ImpactEmitterSample[16] - passive radar impact impulse cache - owner: SpatialAudioManager

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  LIFECYCLE
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        private void Awake()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            // Self-state only. Runtime resources are allocated by explicit bootstrap registration.
            _resolvedAcousticOcclusionLayerMask = AcousticOcclusionUtility.BuildSensoryMask();
            RefreshMixerParameterAvailability();
        }

        private void OnEnable()
        {
            if (TryAbortForUsableExistingRuntime())
                return;

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
                TryRegisterWorldCaveDirectorListener();
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
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState(releaseRuntimeResources: false);
        }

        private void OnDestroy()
        {
            if (_runtimeOwnerAborted)
            {
                if (ReferenceEquals(ActiveRuntimeInstance, this))
                    ActiveRuntimeInstance = null;

                return;
            }

            ShutdownServiceState(releaseRuntimeResources: true);
        }

        public void OnServiceShutdown()
        {
            if (_runtimeOwnerAborted)
                return;

            ShutdownServiceState(releaseRuntimeResources: true);
        }

        private void ShutdownServiceState(bool releaseRuntimeResources)
        {
            if (_runtimeOwnerAborted)
                return;

            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregisterHotSwapListener();
            TryUnregisterWorldCaveDirectorListener();
            TryUnsubscribeAudioEvents();
            if (_isInitialized)
            {
                GlobalRegistry.UnregisterAudioVirtualizationService(this);
                GlobalRegistry.UnregisterAudioService(this);
                _isInitialized = false;
            }

            TryUnregisterDispatcherLanes();
            if (_registeredOriginShiftListener)
                HectonFloatingOrigin.UnregisterListener(this);

            _registeredOriginShiftListener = false;
            _hasPreviousListenerVelocityAup = false;
            _previousListenerVelocityAup = default;
            ResetAllWorldSourceState();
            ResetImpactEmitters();
            ResetAcousticRadarBins();
            ResetAcousticRadarGrid();
            ResetListenerCaveState();
            ResetBaseInteriorMuffleCache();
            ClearDelayedAudioEvents();
            ClearAudioEventQueue();
            ClearVirtualVoiceQueues();
            _listenerPlayerMovementTrauma = null;
            _foveatedSimulationDirector = null;
            _cachedPlayerCriticalAudio = null;
            _cachedHabitatGraph = null;
            _cachedPlayerRuntimeContext = null;
            _cachedWeatherService = null;
            _cachedOceanKinematicsService = null;
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
            SetEclipseAcousticPitchShiftCentsCore(0f);
            _brownoutTarget01 = 0f;
            _brownoutAudioPitchRatio = 1f;
            ApplyBrownoutPitchToMixerAndSources();
            ResetGlobalWindHowlState();
            _worldDroneCrossfadeActive = false;
            ApplyThreatBusDucking(0f, 0f);
            ResetMasterDepthPressureLowPass();
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
        public bool IsInitialized => !_runtimeOwnerAborted && _isInitialized;

        /// <summary>
        /// True while this component is the registered audio and virtualization owner and can accept live runtime mutations.
        /// </summary>
        public bool IsSpatialAudioRuntimeReady =>
            IsAudioRuntimeReady;

        /// <inheritdoc />
        public bool IsAudioRuntimeReady =>
            IsSpatialAudioRuntimeUsable(this) &&
            ReferenceEquals(GlobalRegistry.Audio, this) &&
            ReferenceEquals(GlobalRegistry.AudioVirtualization, this) &&
            IsInitialized &&
            IsVirtualizationReady;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => IsInitialized ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => IsInitialized;

        /// <inheritdoc />
        public bool IsVirtualizationReady =>
            !_runtimeOwnerAborted &&
            _runtimeResourcesInitialized &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                MaxVirtualVoiceCapacity) &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceSortPoolHandle,
                SpatialAudioVirtualVoiceSortPoolBufferId,
                MaxVirtualVoiceCapacity) &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceDtoPoolHandle,
                SpatialAudioVirtualVoiceDtoPoolBufferId,
                MaxVirtualVoiceCapacity) &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceSortKeyPoolHandle,
                SpatialAudioVirtualVoiceSortKeyPoolBufferId,
                MaxVirtualVoiceCapacity) &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceSelectionsHandle,
                BufferID.SpatialAudioVirtualVoiceSelections,
                MaxVirtualPhysicalVoices) &&
            HasAudioVaultReadBuffer(
                in _virtualVoiceStatisticsHandle,
                BufferID.SpatialAudioVirtualVoiceStatistics,
                1);

        /// <inheritdoc />
        public int PhysicalVoiceLimit => _runtimeOwnerAborted
            ? 0
            : math.min(
                math.min(_virtualPhysicalVoiceLimit, VirtualVoiceUtility.ResolveContinuousVoiceBudget(_virtualVoiceQualityWeight)),
                math.clamp(_virtualVoiceTuning.MaxHydratedVoices, 1, MaxVirtualPhysicalVoices));

        /// <inheritdoc />
        public int VirtualVoiceCount => !_runtimeOwnerAborted && HasAudioVaultReadBuffer(
            in _virtualVoiceWritePoolHandle,
            SpatialAudioVirtualVoiceWritePoolBufferId,
            MaxVirtualVoiceCapacity)
                ? _virtualVoiceWriteCount
                : 0;

        /// <inheritdoc />
        public int ActivePhysicalVoiceCount => _runtimeOwnerAborted ? 0 : _lastVirtualVoiceStatistics.ActivePhysicalVoices;

        /// <inheritdoc />
        public int CulledVoiceCount => _runtimeOwnerAborted ? 0 : _lastVirtualVoiceStatistics.CulledVoices;

        /// <inheritdoc />
        public int StolenVoiceCount => _runtimeOwnerAborted ? 0 : _lastVirtualVoiceStatistics.StolenVoices;

        /// <inheritdoc />
        public int DroppedVoiceCount => _runtimeOwnerAborted
            ? 0
            : math.max(0, _lastVirtualVoiceStatistics.DroppedVoices) + _virtualVoiceDroppedCount;

        /// <inheritdoc />
        public int DroppedAudioEventCount => _runtimeOwnerAborted ? 0 : math.max(0, _audioEventQueueDroppedCount);

        /// <summary>
        /// Current eclipse-driven pitch shift applied to ambient bed/drone world sources.
        /// </summary>
        public float EclipseAcousticPitchShiftCents => _runtimeOwnerAborted ? 0f : _eclipseAcousticPitchShiftCents;

        /// <summary>
        /// Current pitch ratio derived from <see cref="EclipseAcousticPitchShiftCents"/>.
        /// </summary>
        public float EclipseAcousticPitchRatio => _runtimeOwnerAborted ? 1f : _eclipseAcousticPitchRatio;

        /// <summary>
        /// Sets the eclipse pitch scalar in cents. Negative values lower ambient bed/drone sources.
        /// </summary>
        public void SetEclipseAcousticPitchShiftCents(float shiftCents)
        {
            if (!IsInitialized)
                return;

            SetEclipseAcousticPitchShiftCentsCore(shiftCents);
        }

        private void SetEclipseAcousticPitchShiftCentsCore(float shiftCents)
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
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

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
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

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
            if (_runtimeOwnerAborted)
                return;

            if (!TryRegisterAudioRuntimeServices())
                return;

            EnsureRuntimeResourcesInitialized();
            TrySubscribeAudioEvents();
            RefreshCachedAudioRuntimeServicesCold();
            TryRegisterHotSwapListener();
            TryRegisterWorldCaveDirectorListener();
            RefreshSpatialAudioPolicyCold();
            ApplyAmbientOutputSampleRatePolicy();
            TryRegisterOriginShiftListener();
            RefreshVirtualPhysicalVoiceLimit(true);
            RefreshFoveatedDirector();

            if (_isInitialized)
            {
                TryRegisterDispatcherLanes();
                return;
            }

            TryRegisterDispatcherLanes();
            _isInitialized = true;
        }

        private bool TryRegisterAudioRuntimeServices()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (TryAbortForUsableExistingRuntime())
                return false;

            IAudioService registeredAudioService = GlobalRegistry.Audio;
            IAudioVirtualizationService registeredVirtualization = GlobalRegistry.AudioVirtualization;

            if (!ReferenceEquals(registeredAudioService, null) && !ReferenceEquals(registeredAudioService, this))
            {
                if (IsAudioServiceOwnerUsable(registeredAudioService))
                {
                    RestoreActiveRuntimeInstanceFromOwner(registeredAudioService);
                    AbortDuplicateRuntimeOwner();
                    return false;
                }
            }

            if (!ReferenceEquals(registeredVirtualization, null) && !ReferenceEquals(registeredVirtualization, this))
            {
                if (IsAudioVirtualizationOwnerUsable(registeredVirtualization))
                {
                    RestoreActiveRuntimeInstanceFromOwner(registeredVirtualization);
                    AbortDuplicateRuntimeOwner();
                    return false;
                }
            }

            if (!ReferenceEquals(registeredAudioService, null) && !ReferenceEquals(registeredAudioService, this))
                GlobalRegistry.UnregisterAudioService(registeredAudioService);

            if (!ReferenceEquals(registeredVirtualization, null) && !ReferenceEquals(registeredVirtualization, this))
                GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);

            GlobalRegistry.RegisterAudioService(this);
            GlobalRegistry.RegisterAudioVirtualizationService(this);
            bool ownsServices =
                ReferenceEquals(GlobalRegistry.Audio, this) &&
                ReferenceEquals(GlobalRegistry.AudioVirtualization, this);
            if (!ownsServices)
                AbortDuplicateRuntimeOwner();
            return ownsServices;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            if (_runtimeOwnerAborted)
                return true;

            if (!Application.isPlaying)
                return false;

            SpatialAudioManager activeRuntime = ActiveRuntimeInstance;
            if (!ReferenceEquals(activeRuntime, null) && !ReferenceEquals(activeRuntime, this))
            {
                if (IsSpatialAudioRuntimeUsable(activeRuntime))
                {
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                if (ReferenceEquals(ActiveRuntimeInstance, activeRuntime))
                    ActiveRuntimeInstance = null;
            }

            IAudioService registeredAudioService = GlobalRegistry.Audio;
            if (!ReferenceEquals(registeredAudioService, null) && !ReferenceEquals(registeredAudioService, this))
            {
                if (IsAudioServiceOwnerUsable(registeredAudioService))
                {
                    RestoreActiveRuntimeInstanceFromOwner(registeredAudioService);
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                GlobalRegistry.UnregisterAudioService(registeredAudioService);
            }

            IAudioVirtualizationService registeredVirtualization = GlobalRegistry.AudioVirtualization;
            if (!ReferenceEquals(registeredVirtualization, null) && !ReferenceEquals(registeredVirtualization, this))
            {
                if (IsAudioVirtualizationOwnerUsable(registeredVirtualization))
                {
                    RestoreActiveRuntimeInstanceFromOwner(registeredVirtualization);
                    AbortDuplicateRuntimeOwner();
                    return true;
                }

                GlobalRegistry.UnregisterAudioVirtualizationService(registeredVirtualization);
            }

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterHotSwapListener();
            TryUnregisterWorldCaveDirectorListener();
            TryUnsubscribeAudioEvents();
            FatalPressureImplosionEvents.Unregister(this);
            RepairDroneTorchAcousticEvents.Unregister(this);
            if (ReferenceEquals(GlobalRegistry.AudioVirtualization, this))
                GlobalRegistry.UnregisterAudioVirtualizationService(this);

            if (ReferenceEquals(GlobalRegistry.Audio, this))
                GlobalRegistry.UnregisterAudioService(this);

            TryUnregisterDispatcherLanes();

            if (_registeredOriginShiftListener)
                HectonFloatingOrigin.UnregisterListener(this);

            _runtimeOwnerAborted = true;
            _isInitialized = false;
            _registeredOriginShiftListener = false;
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
            enabled = false;
            Destroy(this);
        }

        private static bool IsSpatialAudioRuntimeUsable(SpatialAudioManager manager)
        {
            return manager != null &&
                   manager.isActiveAndEnabled &&
                   !manager._runtimeOwnerAborted;
        }

        private static bool IsAudioServiceOwnerUsable(IAudioService audioService)
        {
            if (ReferenceEquals(audioService, null))
                return false;

            if (audioService is SpatialAudioManager manager && manager._runtimeOwnerAborted)
                return false;

            if (audioService is Behaviour behaviour && (behaviour == null || !behaviour.isActiveAndEnabled))
                return false;

            return audioService.IsAudioRuntimeReady;
        }

        private static bool IsAudioVirtualizationOwnerUsable(IAudioVirtualizationService virtualization)
        {
            if (ReferenceEquals(virtualization, null))
                return false;

            if (virtualization is SpatialAudioManager manager && manager._runtimeOwnerAborted)
                return false;

            if (virtualization is Behaviour behaviour && (behaviour == null || !behaviour.isActiveAndEnabled))
                return false;

            return virtualization.IsVirtualizationReady;
        }

        private static void RestoreActiveRuntimeInstanceFromOwner(object owner)
        {
            if (owner is SpatialAudioManager manager && manager != null)
                ActiveRuntimeInstance = manager;
        }

        private void EnsureRuntimeResourcesInitialized()
        {
            if (_runtimeResourcesInitialized)
                return;

            RefreshMixerParameterAvailability();
            InitializePool();
            InitializePool2D();
            InitializeTelemetryCaches();
#if UNITY_EDITOR
            TryLoadAcousticLutFallbackCold();
#endif
            PrepareGlobalWindHowlSource();
#if DEVELOPMENT_BUILD
            EnsureAudioRamDebugOverlay();
#endif
            _runtimeResourcesInitialized = true;
        }

        private void TrySubscribeAudioEvents()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_eventsSubscribed)
                return;

            TryRegisterPhysicsImpactListener();
            FatalPressureImplosionEvents.Unregister(this);
            FatalPressureImplosionEvents.Register(this);
            RepairDroneTorchAcousticEvents.Unregister(this);
            RepairDroneTorchAcousticEvents.Register(this);
            _eventsSubscribed = true;
        }

        private void TryUnsubscribeAudioEvents()
        {
            if (!_eventsSubscribed)
                return;

            TryUnregisterPhysicsImpactListener();
            FatalPressureImplosionEvents.Unregister(this);
            RepairDroneTorchAcousticEvents.Unregister(this);
            _eventsSubscribed = false;
        }

        private void TryRegisterWorldCaveDirectorListener()
        {
            if (_runtimeOwnerAborted || _worldCaveDirectorListenerRegistered || !Application.isPlaying)
                return;

            _worldCaveDirector = null;
            WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(ref _worldCaveDirector);
            WorldCaveDirector.ActiveRuntimeInstanceChanged += HandleWorldCaveDirectorChanged;
            _worldCaveDirectorListenerRegistered = true;
        }

        private void TryUnregisterWorldCaveDirectorListener()
        {
            if (!_worldCaveDirectorListenerRegistered)
                return;

            WorldCaveDirector.ActiveRuntimeInstanceChanged -= HandleWorldCaveDirectorChanged;
            _worldCaveDirectorListenerRegistered = false;
            _worldCaveDirector = null;
        }

        private void HandleWorldCaveDirectorChanged(WorldCaveDirector director)
        {
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

            _worldCaveDirector = director;
            WorldRuntimeReferenceUtility.TryResolveWorldCaveDirector(ref _worldCaveDirector);
            ResetListenerCaveState();
        }

        /// <summary>
        /// Restores temporary Haas masking on clustered arrivals.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta time.</param>
        public void Tick(float deltaTime)
        {
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

            if (_pool == null || _arrivalTimes == null || _haasReleaseTimes == null)
                return;

            _pendingSpatialAudioTickDeltaTime += math.max(0f, deltaTime);
            _hasPendingSpatialAudioTick = true;
        }

        private void RunSpatialAudioTickCore(float deltaTime)
        {
            if (_pool == null || _arrivalTimes == null || _haasReleaseTimes == null)
                return;

            float safeDeltaTime = math.max(0f, deltaTime);
            EnsureSpatialAudioPolicyCached();
            AdvanceVirtualVoiceStealFades(safeDeltaTime);
            float blendT = FastDecayBlend(HaasBlendSharpness, safeDeltaTime);
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAupRuntimePosition,
                out AbsoluteUniversePosition listenerAup);
            Vector3 listenerVelocity = Vector3.zero;
            if (hasListener)
            {
                listenerVelocity = ResolveListenerAupVelocity(in listenerAup, safeDeltaTime);
            }
            else
            {
                _hasPreviousListenerVelocityAup = false;
                _previousListenerVelocityAup = default;
            }
            ResolveListenerBasis(listener, out float3 listenerRight, out float3 listenerUp, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            NativeArray<AbsoluteUniversePosition> previousVelocityAups = default;
            NativeArray<int> previousVelocityAupFrames = default;
            bool previousVelocityLocked = hasListener &&
                _activeWorldCount > 0 &&
                TryAcquirePreviousVelocityAupBuffers(out previousVelocityAups, out previousVelocityAupFrames);

            UpdateListenerWaterDensityMul(safeDeltaTime);
            UpdateStormRoarShedder(safeDeltaTime);
            UpdateGlobalWindHowl(safeDeltaTime);
            ApplyMasterDepthPressureLowPass(hasListener ? listenerAupRuntimePosition.y : 0f, safeDeltaTime);
            UpdateTimeDilationPitchScalar();
            UpdateBrownoutAudioPitch(safeDeltaTime);
            float threatActivity = 0f;
            DecayImpactEmitters(now);
            AdvanceAcousticRadarDecayCadence(safeDeltaTime);
            ResetNearestRadarEmitterScratch();
            DrainDelayedAudioIngress();
            ProcessDelayedAudioEvents(hasListener, in listenerAup);
            int activeSlot = 0;
            try
            {
                while (activeSlot < _activeWorldCount)
                {
                    int sourceIndex = _activeWorldIndices[activeSlot];
                    AudioSource source = _pool[sourceIndex];
                    if (source == null || !source.isActiveAndEnabled || source.clip == null || !source.isPlaying)
                    {
                        if (previousVelocityLocked)
                            ResetPreviousVelocityAupSlotLocal(sourceIndex, previousVelocityAups, previousVelocityAupFrames);
                        ResetWorldSourceState(sourceIndex, false);
                        continue;
                    }

                    if (!TryGetCachedActiveWorldRuntimePosition(sourceIndex, out Vector3 sourcePosition))
                    {
                        if (previousVelocityLocked)
                            ResetPreviousVelocityAupSlotLocal(sourceIndex, previousVelocityAups, previousVelocityAupFrames);
                        ResetWorldSourceState(sourceIndex, false);
                        continue;
                    }

                    AbsoluteUniversePosition sourceAup = ResolveActiveWorldAup(sourceIndex, sourcePosition, currentFrame);
                    CacheActiveWorldRuntimePosition(sourceIndex, sourcePosition, currentFrame);
                    CacheActiveWorldAup(sourceIndex, in sourceAup, currentFrame);
                    UpdateWorldSourceAudioLod(
                        sourceIndex,
                        source,
                        sourcePosition,
                        in sourceAup,
                        listener,
                        in listenerAup,
                        listenerRuntimePosition,
                        listenerRight,
                        listenerAcousticForward,
                        now,
                        false);
                    if (!source.isPlaying)
                    {
                        if (previousVelocityLocked)
                            ResetPreviousVelocityAupSlotLocal(sourceIndex, previousVelocityAups, previousVelocityAupFrames);
                        ResetWorldSourceState(sourceIndex, false);
                        continue;
                    }

                    if (listener != null)
                    {
                        if (previousVelocityLocked)
                            UpdateManualDopplerPitch(
                                sourceIndex,
                                source,
                                in sourceAup,
                                in listenerAup,
                                listenerVelocity,
                                safeDeltaTime,
                                currentFrame,
                                previousVelocityAups,
                                previousVelocityAupFrames);
                        else
                            ResetManualDopplerPitch(sourceIndex, source);
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
            }
            finally
            {
                if (previousVelocityLocked)
                    ReleasePreviousVelocityAupBuffers();
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
            if (_runtimeOwnerAborted)
                return;

            CacheReboundAudioRuntimeService(serviceSlot, currentService);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

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
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

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

            if (!HasAudioVaultReadBuffer(
                    in _virtualVoiceWritePoolHandle,
                    SpatialAudioVirtualVoiceWritePoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceSortPoolHandle,
                    SpatialAudioVirtualVoiceSortPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceDtoPoolHandle,
                    SpatialAudioVirtualVoiceDtoPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceSortKeyPoolHandle,
                    SpatialAudioVirtualVoiceSortKeyPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices) ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceStatisticsHandle,
                    BufferID.SpatialAudioVirtualVoiceStatistics,
                    1) ||
                !HasAudioVaultReadBuffer(
                    in _acousticSourceWritePoolHandle,
                    SpatialAudioAcousticSourceWritePoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _acousticSourceSortPoolHandle,
                    SpatialAudioAcousticSourceSortPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _acousticPreviousAupWritePoolHandle,
                    SpatialAudioAcousticPreviousAupWritePoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _acousticPreviousAupSortPoolHandle,
                    SpatialAudioAcousticPreviousAupSortPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                !HasAudioVaultReadBuffer(
                    in _acousticSelectedSourcePoolHandle,
                    SpatialAudioAcousticSelectedSourcePoolBufferId,
                    MaxVirtualPhysicalVoices) ||
                !HasAudioVaultReadBuffer(
                    in _acousticSelectedPreviousAupPoolHandle,
                    SpatialAudioAcousticSelectedPreviousAupPoolBufferId,
                    MaxVirtualPhysicalVoices) ||
                !HasAudioVaultReadBuffer(
                    in _acousticDspOutputPoolHandle,
                    SpatialAudioAcousticDspOutputPoolBufferId,
                    MaxVirtualVoiceCapacity))
            {
                return;
            }

            if (!TryResolveListenerFrame(
                    out Transform listener,
                    out _,
                    out Vector3 listenerAupRuntimePosition,
                    out AbsoluteUniversePosition listenerAup))
            {
                _hasVirtualListenerAup = false;
                _hasVirtualPreviousListenerVelocityAup = false;
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
            _virtualListenerVelocityMetersPerSecond = ResolveVirtualListenerVelocity(in listenerAup, deltaTime);
            float globalQualityWeight = ResolveVirtualVoiceQualityWeight();
            _virtualVoiceQualityWeight = globalQualityWeight;
            float listenerDepthMeters = ResolveVirtualListenerDepthMeters(listenerAupRuntimePosition);
            _virtualListenerDepthMeters = listenerDepthMeters;
            _virtualSimulationTickDeltaSeconds = math.max(0.0001f, SanitizeFinite(deltaTime, 1f / 60f));
            _virtualListenerSdfProbePosition = new float3(listenerAupRuntimePosition.x, listenerAupRuntimePosition.y, listenerAupRuntimePosition.z);
            Vector3 listenerRightVector = listener != null && listener.right.sqrMagnitude > 0.000001f
                ? listener.right.normalized
                : Vector3.right;
            _virtualListenerRight = new float3(listenerRightVector.x, listenerRightVector.y, listenerRightVector.z);
            float depthLowPassHertz = VirtualVoiceUtility.ResolveDepthLowPassHertz(listenerDepthMeters, globalQualityWeight);
            _virtualVoiceSdfSampler = ResolveVirtualVoiceSdfSampler();
            RefreshVirtualVoiceTuningFromVault();

            VaultGenerationHandle<VirtualVoice> previousSortHandle = _virtualVoiceSortPoolHandle;
            _virtualVoiceSortPoolHandle = _virtualVoiceWritePoolHandle;
            _virtualVoiceWritePoolHandle = previousSortHandle;
            VaultGenerationHandle<AcousticSourceDTO> previousAcousticSourceSortHandle = _acousticSourceSortPoolHandle;
            _acousticSourceSortPoolHandle = _acousticSourceWritePoolHandle;
            _acousticSourceWritePoolHandle = previousAcousticSourceSortHandle;
            VaultGenerationHandle<double3> previousAcousticAupSortHandle = _acousticPreviousAupSortPoolHandle;
            _acousticPreviousAupSortPoolHandle = _acousticPreviousAupWritePoolHandle;
            _acousticPreviousAupWritePoolHandle = previousAcousticAupSortHandle;
            _virtualVoiceSortCount = math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
            _virtualVoiceDtoCount = _virtualVoiceSortCount;
            _virtualVoiceWriteCount = 0;
            int tunedPhysicalLimit = math.min(
                math.min(_virtualPhysicalVoiceLimit, VirtualVoiceUtility.ResolveContinuousVoiceBudget(globalQualityWeight)),
                math.clamp(_virtualVoiceTuning.MaxHydratedVoices, 1, MaxVirtualPhysicalVoices));
            IDataVault sortVault = _dataVault;
            if (sortVault == null || !TryLockVirtualVoiceSortBuffers(sortVault))
            {
                ReleaseVirtualVoiceSortBufferLocks();
                DropVirtualVoiceSortBatchForVaultLockFailure();
                return;
            }

            bool sortPinsTransferred = false;
            NativeArray<VirtualVoice> virtualVoiceSortPool;
            NativeArray<VirtualVoiceSortKey> virtualVoiceSortKeys;
            NativeArray<VirtualVoiceSelection> virtualVoiceSelections;
            NativeArray<VirtualVoiceStatistics> virtualVoiceStatistics;
            try
            {
                if (!TryOpenAudioVaultBuffer(
                        sortVault,
                        ref _virtualVoiceSortPoolHandle,
                        SpatialAudioVirtualVoiceSortPoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out virtualVoiceSortPool))
                {
                    DropVirtualVoiceSortBatchForVaultLockFailure();
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        sortVault,
                        ref _virtualVoiceSortKeyPoolHandle,
                        SpatialAudioVirtualVoiceSortKeyPoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out virtualVoiceSortKeys))
                {
                    DropVirtualVoiceSortBatchForVaultLockFailure();
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        sortVault,
                        ref _virtualVoiceSelectionsHandle,
                        BufferID.SpatialAudioVirtualVoiceSelections,
                        SystemID.Audio,
                        MaxVirtualPhysicalVoices,
                        out virtualVoiceSelections))
                {
                    DropVirtualVoiceSortBatchForVaultLockFailure();
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        sortVault,
                        ref _virtualVoiceStatisticsHandle,
                        BufferID.SpatialAudioVirtualVoiceStatistics,
                        SystemID.Audio,
                        1,
                        out virtualVoiceStatistics))
                {
                    DropVirtualVoiceSortBatchForVaultLockFailure();
                    return;
                }

                sortPinsTransferred = true;
            }
            finally
            {
                if (!sortPinsTransferred)
                    ReleaseVirtualVoiceSortBufferLocks();
            }

            var sortJob = new VirtualVoiceSortJob
            {
                Voices = virtualVoiceSortPool,
                SortKeys = virtualVoiceSortKeys,
                Selections = virtualVoiceSelections,
                Statistics = virtualVoiceStatistics,
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
                Frame = SystemDispatcher.CurrentFrameIndex,
                DisableSdfOcclusion = _virtualVoiceTuning.DisableSdfOcclusion != 0 ? 1 : 0,
                MinimumAudibleEnergy = VirtualVoiceUtility.MinimumAudibleEnergy,
                GlobalQualityWeight = globalQualityWeight,
                DepthLowPassHertz = depthLowPassHertz,
                RollbackActive = ResolveRollbackAudioSuppressionActive() ? 1 : 0
            };

            _virtualVoiceDroppedCount = 0;
            _virtualVoiceSortStartRealtimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                _virtualVoiceSortHandle = sortJob.Schedule();
                _virtualVoiceSortScheduled = true;
            }
            catch
            {
                ReleaseVirtualVoiceSortBufferLocks();
                _virtualVoiceSortScheduled = false;
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SpatialAudioFailureVirtualVoiceSortSchedule));
            }
#else
            _virtualVoiceSortHandle = sortJob.Schedule();
            _virtualVoiceSortScheduled = true;
#endif
        }

        private float ResolveVirtualListenerDepthMeters(Vector3 listenerAupRuntimePosition)
        {
            float seaLevelY = ResolveAudioWaterLevelY();
            return math.isfinite(listenerAupRuntimePosition.y)
                ? math.max(0f, seaLevelY - listenerAupRuntimePosition.y)
                : 0f;
        }

        private float ResolveAudioWaterLevelY()
        {
            IHectonOceanKinematicsService service = _cachedOceanKinematicsService;
            IHectonOceanKinematics provider = service != null && service.IsInitialized
                ? service.ActiveProvider
                : null;

            if (provider != null &&
                provider.IsAvailable &&
                TryResolveAudioWaterLevel(provider.SeaLevel, out float providerSeaLevelY))
                return providerSeaLevelY;

            return DefaultSeaLevelY;
        }

        private static bool TryResolveAudioWaterLevel(float candidateSeaLevelY, out float seaLevelY)
        {
            if (math.isfinite(candidateSeaLevelY) &&
                math.abs(candidateSeaLevelY) <= WorldWaterLevelCalibrationMath.MaximumAbsoluteWaterLevelY)
            {
                seaLevelY = candidateSeaLevelY;
                return true;
            }

            seaLevelY = DefaultSeaLevelY;
            return false;
        }

        /// <summary>
        /// Refreshes listener cave/reverb telemetry on the slow lane.
        /// </summary>
        public void SlowTick()
        {
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

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
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!IsFinite(shiftOffset) || !math.isfinite(shiftSqrMagnitude))
            {
                DumpVirtualVoiceBlackBox();
                return;
            }

            if (shiftSqrMagnitude <= 0.000001f)
                return;

            CompleteVirtualVoiceSort();
        }

        /// <summary>
        /// Drains queued gameplay audio events after frame simulation.
        /// </summary>
        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted || !_isInitialized)
                return;

            if (_hasPendingSpatialAudioTick)
            {
                float audioDeltaTime = _pendingSpatialAudioTickDeltaTime;
                _pendingSpatialAudioTickDeltaTime = 0f;
                _hasPendingSpatialAudioTick = false;
                RunSpatialAudioTickCore(audioDeltaTime);
            }

            ConsumeAcousticImpulseSignals();
            TryFinalizeVirtualVoiceSortNoWait();
            InjectVirtualVoiceSelections();
            DrainAudioEventQueue();
            TryFlushPendingAcousticPortalBlackBoxDump();
#if DEVELOPMENT_BUILD
            UpdateAudioRamDebugOverlay();
#endif
        }

#if DEVELOPMENT_BUILD
        private void EnsureAudioRamDebugOverlay()
        {
            if (_audioRamDebugLabel == null)
                return;

            if (_audioRamDebugTextBuffer == null || _audioRamDebugTextBuffer.Length < AudioRamDebugTextCapacity)
                _audioRamDebugTextBuffer = new char[AudioRamDebugTextCapacity]; // COLD ALLOC: char[48] - development audio RAM overlay text staging - owner: SpatialAudioManager

            _audioRamDebugLabel.raycastTarget = false;

            _audioRamDebugLastResidentKilobytes = -1;
            _audioRamDebugLastClipCount = -1;
            UpdateAudioRamDebugOverlay();
        }

        private void UpdateAudioRamDebugOverlay()
        {
            if (_audioRamDebugLabel == null ||
                _audioRamDebugTextBuffer == null ||
                _audioRamDebugTextBuffer.Length < AudioRamDebugTextCapacity)
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
        public bool IsListenerInsideCaveVolume => IsInitialized && _listenerContainingCaveCount > 0;

        /// <summary>Normalized cave-interior depth from the current listener-containing volume cache.</summary>
        public float ListenerCaveInterior01 => IsInitialized ? math.saturate(_listenerCaveInterior01) : 0f;

        /// <summary>Current listener cave RT60 calculated with RT60 = 0.161 * (Volume / SurfaceArea).</summary>
        public float ListenerSabineRt60Seconds => IsInitialized ? _listenerSabineRt60Seconds : 0f;

        /// <summary>Current listener cave open-cell volume estimate in cubic meters.</summary>
        public float ListenerSabineVolumeCubicMeters => IsInitialized ? _listenerSabineVolumeCubicMeters : 0f;

        /// <summary>Current listener cave exposed surface estimate in square meters.</summary>
        public float ListenerSabineSurfaceAreaSquareMeters => IsInitialized ? _listenerSabineSurfaceAreaSquareMeters : 0f;

        /// <summary>
        /// Publishes the current parasite load of the occupied module into mixer-level room filtering.
        /// </summary>
        /// <param name="parasiteCount">Attached parasite count for the player-occupied module.</param>
        public void SetParasiteRoomAcousticLoad(int parasiteCount)
        {
            if (_runtimeOwnerAborted)
                return;

            int sanitizedCount = math.max(0, parasiteCount);
            if (_parasiteRoomAcousticCount == sanitizedCount)
                return;

            _parasiteRoomAcousticCount = sanitizedCount;
            _parasiteRoomTarget01 = math.saturate(sanitizedCount * math.rcp((float)math.max(1, _parasiteRoomCountForFullInfection)));
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  POOL INITIALIZATION
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђВЎГђВѕГђВ·ГђВґГђВ°Г‘вЂГ‘вЂљ ГђВїГ‘Ж’ГђВ» AudioSource ГђВєГђВ°ГђВє ГђВґГђВѕГ‘вЂЎГђВµГ‘в‚¬ГђВЅГђВёГђВµ ГђВѕГђВ±Г‘Е ГђВµГђВєГ‘вЂљГ‘вЂ№.
        /// ГђвЂ™Г‘вЂ№ГђВ·Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљГ‘ВЃГ‘ВЏ ГђВѕГђВґГђВёГђВЅ Г‘в‚¬ГђВ°ГђВ· ГђВІ Awake. ГђВќГђВёГђВєГђВ°ГђВєГђВёГ‘вЂ¦ ГђВ°ГђВ»ГђВ»ГђВѕГђВєГђВ°Г‘вЂ ГђВёГђВ№ ГђВїГђВѕГ‘ВЃГђВ»ГђВµ Г‘ВЌГ‘вЂљГђВѕГђВіГђВѕ.
        /// </summary>
        private void InitializePool()
        {
            AudioResidencyCache.EnsureReady();

            int effectivePoolSize = math.min(_poolSize, CountAuthoredWorldPoolNodes(ResolveWorldPoolRoot()));
            if (effectivePoolSize < _poolSize)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
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
            _clipRouteCacheIds = new int[AudioClipRouteCacheCapacity]; // COLD ALLOC: int[512] - AudioClip instance-id route cache keys - owner: SpatialAudioManager
            _clipRouteCacheFlags = new byte[AudioClipRouteCacheCapacity]; // COLD ALLOC: byte[512] - AudioClip route flags keyed by stable clip entity id - owner: SpatialAudioManager
            _activeWorldRuntimePositions = new Vector3[_poolSize]; // COLD ALLOC: Vector3[_poolSize] - per-frame active world-source runtime position cache - owner: SpatialAudioManager
            _activeWorldRuntimePositionFrames = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - validity frame for active world-source runtime position cache - owner: SpatialAudioManager
            _activeWorldAups = new AbsoluteUniversePosition[_poolSize]; // COLD ALLOC: AbsoluteUniversePosition[_poolSize] - per-source AUP cache shared by radar and binaural telemetry - owner: SpatialAudioManager
            _activeWorldAupFrames = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - validity frame for active world-source AUP cache - owner: SpatialAudioManager
            _activeWorldIndices = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse active world-source set - owner: SpatialAudioManager
            _activeWorldSlots = new int[_poolSize]; // COLD ALLOC: int[_poolSize] - sparse world-source slot lookup - owner: SpatialAudioManager
            for (int i = 0; i < AudioClipRouteCacheCapacity; i++)
                _clipRouteCacheIds[i] = int.MinValue;

            CacheAuthoredClipRouteOverridesCold();

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

        /// <summary>ГђВЎГђВѕГђВ·ГђВґГђВ°Г‘вЂГ‘вЂљ ГђВїГ‘Ж’ГђВ» 2D ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВѕГђВІ (ГђВ°ГђВЅГђВ°ГђВ»ГђВѕГђВіГђВёГ‘вЂЎГђВЅГђВѕ 3D, ГђВ±ГђВµГђВ· PlayOneShot).</summary>
        private void InitializePool2D()
        {
            int effectivePool2DSize = math.min(_pool2DSize, CountAuthoredHelmetPoolNodes(ResolveHelmetPoolRoot()));
            if (effectivePool2DSize < _pool2DSize)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogError(
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
        /// ГђВќГђВ°Г‘ВЃГ‘вЂљГ‘в‚¬ГђВ°ГђВёГђВІГђВ°ГђВµГ‘вЂљ AudioSource ГђВєГђВ°ГђВє 3D ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВє Г‘ВЃ Linear Rolloff.
        /// Linear Rolloff ГђВґГђВµГ‘Л†ГђВµГђВІГђВ»ГђВµ Logarithmic ГђВё ГђВїГ‘в‚¬ГђВµГђВґГ‘ВЃГђВєГђВ°ГђВ·Г‘Ж’ГђВµГђВјГђВµГђВµ ГђВґГђВ»Г‘ВЏ ГђВіГђВµГђВ№ГђВјГђВґГђВёГђВ·ГђВ°ГђВ№ГђВЅГђВ°.
        /// </summary>
        private void ConfigureAs3D(AudioSource source)
        {
            source.spatialBlend = 1f;          // ГђЕёГђВѕГђВ»ГђВЅГђВѕГ‘ВЃГ‘вЂљГ‘Е’Г‘ЕЅ 3D
            source.spread = 0f;                // ГђВўГђВѕГ‘вЂЎГђВµГ‘вЂЎГђВЅГ‘вЂ№ГђВ№ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВє
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = _minDistance;
            source.maxDistance = _maxDistance;
            source.dopplerLevel = 0f;          // ГђЕѕГ‘вЂљГђВєГђВ»Г‘ЕЅГ‘вЂЎГђВ°ГђВµГђВј Doppler Гўв‚¬вЂќ ГђВґГђВµГ‘Л†ГђВµГђВІГђВ»ГђВµ ГђВё ГђВЅГђВµГ‘вЂљ ГђВ°Г‘в‚¬Г‘вЂљГђВµГ‘вЂћГђВ°ГђВєГ‘вЂљГђВѕГђВІ

            source.outputAudioMixerGroup = ResolvedDefaultWorldMixerGroup;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PUBLIC API Гўв‚¬вЂќ 3D SPATIAL AUDIO
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВѕГђВёГђВіГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљ 3D ГђВ·ГђВІГ‘Ж’ГђВє ГђВІ Г‘Ж’ГђВєГђВ°ГђВ·ГђВ°ГђВЅГђВЅГђВѕГђВ№ ГђВјГђВёГ‘в‚¬ГђВѕГђВІГђВѕГђВ№ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВё.
        /// ГђЛњГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·Г‘Ж’ГђВµГ‘вЂљ SFX mixer group ГђВїГђВѕ Г‘Ж’ГђВјГђВѕГђВ»Г‘вЂЎГђВ°ГђВЅГђВёГ‘ЕЅ.
        ///
        /// ГђвЂєГђВѕГђВіГђВёГђВєГђВ° ГђВїГ‘Ж’ГђВ»ГђВ°:
        ///   1. ГђЛњГ‘вЂ°ГђВµГ‘вЂљ ГђВїГђВµГ‘в‚¬ГђВІГ‘вЂ№ГђВ№ Г‘ВЃГђВІГђВѕГђВ±ГђВѕГђВґГђВЅГ‘вЂ№ГђВ№ (!isPlaying) ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВє Гўв‚¬вЂќ O(n), n ГўвЂ°В¤ 32.
        ///   2. ГђвЂўГ‘ВЃГђВ»ГђВё ГђВІГ‘ВЃГђВµ ГђВ·ГђВ°ГђВЅГ‘ВЏГ‘вЂљГ‘вЂ№ Гўв‚¬вЂќ ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГ‘ВЏГђВµГ‘вЂљ Г‘ВЃГђВ°ГђВјГ‘вЂ№ГђВ№ Г‘ВЃГ‘вЂљГђВ°Г‘в‚¬Г‘вЂ№ГђВ№ (lowest startTime).
        ///   3. Zero-GC: Г‘вЂљГђВѕГђВ»Г‘Е’ГђВєГђВѕ array traversal, ГђВЅГђВёГђВєГђВ°ГђВєГђВёГ‘вЂ¦ ГђВ°ГђВ»ГђВ»ГђВѕГђВєГђВ°Г‘вЂ ГђВёГђВ№.
        ///
        /// ГђвЂ™Г‘вЂ№ГђВ·ГђВѕГђВІ: Hecton8.Core.GlobalRegistry.Audio.PlayAtPoint(clip, transform.position);
        /// </summary>
        /// <param name="clip">AudioClip ГђВґГђВ»Г‘ВЏ ГђВІГђВѕГ‘ВЃГђВїГ‘в‚¬ГђВѕГђВёГђВ·ГђВІГђВµГђВґГђВµГђВЅГђВёГ‘ВЏ. Null-safe.</param>
        /// <param name="position">ГђЕ“ГђВёГ‘в‚¬ГђВѕГђВІГђВ°Г‘ВЏ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГ‘ВЏ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВ° ГђВ·ГђВІГ‘Ж’ГђВєГђВ°.</param>
        /// <param name="volume">ГђвЂњГ‘в‚¬ГђВѕГђВјГђВєГђВѕГ‘ВЃГ‘вЂљГ‘Е’ [0..1]. Default = 1.</param>
        /// <param name="pitch">Pitch [0.1..3]. Default = 1. ГђВ ГђВ°ГђВЅГђВґГђВѕГђВјГђВёГђВ·ГђВёГ‘в‚¬ГђВѕГђВІГђВ°Г‘вЂљГ‘Е’ ГђВґГђВ»Г‘ВЏ ГђВІГђВ°Г‘в‚¬ГђВёГђВ°Г‘вЂљГђВёГђВІГђВЅГђВѕГ‘ВЃГ‘вЂљГђВё.</param>
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
            if (!IsInitialized || clip == null)
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
            if (!IsInitialized || clip == null)
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
            if (!IsInitialized)
                return;

            float clampedIntensity = math.saturate(intensity01);
            if (clampedIntensity <= 0.001f)
                return;

            ProceduralAudioEvents.TryRaiseAudioPingTriggered(
                position,
                clampedIntensity,
                1.15f,
                1f,
                math.clamp(lowPassCutoffHz, 80f, 800f),
                ProceduralAudioPingKind.MeteorBoom);
        }

        public void PlayHarvestAtAup(in AbsoluteUniversePosition positionAup, AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!IsInitialized || clip == null)
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

        public void PlaySporeEmissionAtAup(
            in AbsoluteUniversePosition positionAup,
            AudioClip clip,
            float pulseFrequencyHz,
            float simulationTimeSeconds,
            float phaseOffset01,
            float volume = 1f)
        {
            if (!IsInitialized || clip == null)
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
        /// ГђЕёГ‘в‚¬ГђВѕГђВёГђВіГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљ 3D ГђВ·ГђВІГ‘Ж’ГђВє Г‘ВЃ Г‘ВЏГђВІГђВЅГ‘вЂ№ГђВј Г‘Ж’ГђВєГђВ°ГђВ·ГђВ°ГђВЅГђВёГђВµГђВј AudioMixerGroup.
        /// ГђЛњГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·Г‘Ж’ГђВ№Г‘вЂљГђВµ ГђВґГђВ»Г‘ВЏ ambient ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ: PlayAtPoint(clip, pos, 1f, 1f, ambientGroup).
        /// </summary>
        public void PlayAtPoint(
            AudioClip clip, Vector3 position, float volume, float pitch, AudioMixerGroup mixerGroup)
        {
            if (!IsInitialized)
                return;

            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup))
                return;

            PlayAtPointResolved(
                clip,
                position,
                in sourceAup,
                volume,
                pitch,
                mixerGroup,
                0);
        }

        private int PlayAtPointResolved(
            AudioClip clip,
            Vector3 position,
            in AbsoluteUniversePosition sourceAup,
            float volume,
            float pitch,
            AudioMixerGroup mixerGroup,
            int stationaryCacheKey,
            float dopplerRatio = 1f)
        {
            if (!IsInitialized)
                return -1;

            if (clip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayAtPointNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Hecton8.Core.H8Debug.LogWarning("[SpatialAudioManager] PlayAtPoint called with null clip.");
#endif
                return -1;
            }

            if (_pool == null || _poolSize <= 0)
                return -1;

            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAupRuntimePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            Vector3 audiblePosition = position;
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

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђЕёГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВѕГђВЅГђВёГ‘в‚¬ГђВѕГђВІГђВ°ГђВЅГђВёГђВµ ГўвЂќв‚¬ГўвЂќв‚¬
            source.transform.position = audiblePosition;
            TouchPlaybackClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup));

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђВќГђВ°Г‘ВЃГ‘вЂљГ‘в‚¬ГђВѕГђВ№ГђВєГђВ° ГўвЂќв‚¬ГўвЂќв‚¬
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
            source.priority = ResolveUnityAudioPriority(clip, source.outputAudioMixerGroup, clampedVolume, lodTier);
            float clampedDopplerRatio = math.clamp(
                dopplerRatio,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && index < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[index] = clampedDopplerRatio;
            source.pitch = ResolveSourcePitch(index, clampedDopplerRatio);
            _audioLodTiers[index] = lodTier;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            UpdateWorldSourceAudioLod(
                index,
                source,
                audiblePosition,
                in audibleAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
                now,
                true);
            if (hasAcousticPortalPath)
                ApplyAcousticPortalPresentation(index, source, in acousticPortalResult);
            ApplyHaasMask(index, in audibleAup, hasListener, in listenerAup, now);
            source.spatialBlend = ResolveTargetSpatialBlend(index, now);

            // ГўвЂќв‚¬ГўвЂќв‚¬ ГђвЂ”ГђВ°ГђВїГ‘Ж’Г‘ВЃГђВє ГўвЂќв‚¬ГўвЂќв‚¬
            PlayAcousticSource(source, hasAcousticPortalPath ? acousticPortalResult.DelaySeconds : 0f);
            _startTimes[index] = now;
            CacheActiveWorldRuntimePosition(index, audiblePosition, currentFrame);
            CacheActiveWorldAup(index, in audibleAup, currentFrame);
            MarkWorldSourceActive(index);
            return index;
        }

        public bool QueueAudioEvent(in CoreAudioEvent audioEvent)
        {
            if (!IsAudioRuntimeReady || _audioEventQueue == null)
                return false;

            if (_audioEventQueueCount >= MaxQueuedAudioEvents)
            {
                _audioEventQueueDroppedCount++;
                PublishAudioEventQueueDropTelemetry(_audioEventQueueOverflowContextHash, ref _lastAudioEventQueueOverflowTelemetryFrame);
                return false;
            }

            if (!TryResolveAudioEventClip(audioEvent.EventID, audioEvent.ClipHash, out _))
            {
                _audioEventQueueDroppedCount++;
                PublishAudioEventQueueDropTelemetry(_audioEventBadDataContextHash, ref _lastAudioEventBadDataTelemetryFrame);
                return false;
            }

            int writeIndex = (_audioEventQueueHead + _audioEventQueueCount) % _audioEventQueue.Length;
            _audioEventQueue[writeIndex] = audioEvent;
            _audioEventQueueCount++;
            return true;
        }

        private void PublishAudioEventQueueDropTelemetry(uint contextHash, ref int lastTelemetryFrame)
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (lastTelemetryFrame == frame)
                return;

            lastTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _audioEventQueueDropWarningHash,
                contextHash,
                math.max(1, _audioEventQueueDroppedCount));
        }

        public bool QueuePrologueAudioTransition(in AudioTransitionState state)
        {
            if (!IsAudioRuntimeReady)
                return false;

            IPlayerCriticalAudioSignalSink playerCriticalAudio = ResolvePlayerCriticalAudioSignalSink();
            return playerCriticalAudio != null && playerCriticalAudio.QueuePrologueAudioTransition(in state);
        }

        public bool QueueSoundEmissionSignal(in SoundEmissionSignal signal)
        {
            if (!IsAudioRuntimeReady)
                return false;

            bool writePoolReady = HasAudioVaultReadBuffer(
                in _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                MaxVirtualVoiceCapacity);
            if (!writePoolReady ||
                _virtualVoiceWriteCount >= MaxVirtualVoiceCapacity ||
                !TryResolveAudioEventClip(signal.EventID, out AudioClip clip) ||
                !AcousticAup.IsFinite(in signal.SourceAup))
            {
                if (writePoolReady)
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
            if (!IsAudioRuntimeReady ||
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

            ProceduralAudioEvents.TryRaiseHullStressSignal(in routedSignal);
            return true;
        }

        public bool QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)
        {
            if (!IsAudioRuntimeReady ||
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

            IPlayerCriticalAudioSignalSink renderer = ResolvePlayerCriticalAudioSignalSink();
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
            {
                if (clipHash == 0u)
                    return true;

                uint resolvedHash = unchecked((uint)EntityId.ToULong(clip.GetEntityId()));
                if (resolvedHash == clipHash)
                    return true;
            }

            return TryResolveAudioClipHash(clipHash, out clip);
        }

        private bool TryResolveAudioClipHash(uint clipHash, out AudioClip clip)
        {
            clip = null;
            if (clipHash == 0u ||
                _audioClipHashKeys == null ||
                _audioClipHashTableIndices == null)
            {
                return false;
            }

            if (_audioClipHashMask <= 0)
                return false;

            int slot = (int)(clipHash & (uint)_audioClipHashMask);
            int probeLimit = _audioClipHashKeys.Length;
            for (int probe = 0; probe < probeLimit; probe++)
            {
                uint candidateHash = _audioClipHashKeys[slot];
                if (candidateHash == 0u)
                    return false;

                if (candidateHash == clipHash)
                {
                    int encodedIndex = _audioClipHashTableIndices[slot];
                    if (encodedIndex <= 0)
                        return false;

                    int tableIndex = encodedIndex - 1;
                    AudioClip[] table = _audioEventClipTable;
                    if (table == null || tableIndex < 0 || tableIndex >= table.Length)
                        return false;

                    clip = table[tableIndex];
                    return clip != null;
                }

                slot = (slot + 1) & _audioClipHashMask;
            }

            return false;
        }

        private void DrainAudioEventQueue()
        {
            if (_audioEventQueue == null || _audioEventQueueCount <= 0)
                return;

            while (_audioEventQueueCount > 0)
            {
                int readIndex = _audioEventQueueHead;
                CoreAudioEvent audioEvent = _audioEventQueue[readIndex];
                _audioEventQueue[readIndex] = default;
                _audioEventQueueHead = (_audioEventQueueHead + 1) % _audioEventQueue.Length;
                _audioEventQueueCount--;
                DispatchQueuedAudioEvent(in audioEvent);
            }

            if (_audioEventQueueCount < 0)
                _audioEventQueueCount = 0;
        }

        /// <inheritdoc />
        public bool EnqueueVirtualVoice(in VirtualVoiceRequest request)
        {
            if (!IsAudioRuntimeReady)
                return false;

            bool writePoolReady = HasAudioVaultReadBuffer(
                in _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                MaxVirtualVoiceCapacity);
            if (!writePoolReady ||
                !HasAudioVaultReadBuffer(
                    in _virtualVoiceDtoPoolHandle,
                    SpatialAudioVirtualVoiceDtoPoolBufferId,
                    MaxVirtualVoiceCapacity) ||
                _virtualVoiceWriteCount >= MaxVirtualVoiceCapacity ||
                !AcousticAup.IsFinite(in request.SourceAup) ||
                !TryResolveAudioEventClip(request.EventID, request.ClipHash, out _))
            {
                if (writePoolReady)
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
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(VirtualVoiceAppendMutationGuardMask))
            {
                _virtualVoiceDroppedCount++;
                return;
            }

            try
            {
                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _virtualVoiceWritePoolHandle,
                        SpatialAudioVirtualVoiceWritePoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<VirtualVoice> virtualVoiceWritePool))
                {
                    _virtualVoiceDroppedCount++;
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _virtualVoiceDtoPoolHandle,
                        SpatialAudioVirtualVoiceDtoPoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<VirtualVoiceDTO> virtualVoiceDtoPool))
                {
                    _virtualVoiceDroppedCount++;
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticSourceWritePoolHandle,
                        SpatialAudioAcousticSourceWritePoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<AcousticSourceDTO> acousticSourceWritePool))
                {
                    _virtualVoiceDroppedCount++;
                    return;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPreviousAupWritePoolHandle,
                        SpatialAudioAcousticPreviousAupWritePoolBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<double3> acousticPreviousAupWritePool))
                {
                    _virtualVoiceDroppedCount++;
                    return;
                }

                virtualVoiceWritePool[writeIndex] = new VirtualVoice
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
                virtualVoiceDtoPool[writeIndex] = new VirtualVoiceDTO
                {
                    AupMeters = acousticMeters,
                    Volume = math.saturate(SanitizeFinite(request.Volume, 0f)),
                    Pitch = math.clamp(SanitizeFinite(request.Pitch, 1f), 0.1f, 3f),
                    ClipHash = request.ClipHash,
                    SourceEntityID = request.SourceEntityID,
                    Importance = priority,
                    Padding = 0u
                };
                if (writeIndex < acousticSourceWritePool.Length)
                {
                    acousticSourceWritePool[writeIndex] = new AcousticSourceDTO
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

                if (writeIndex < acousticPreviousAupWritePool.Length)
                {
                    double3 velocity = new double3(sourceVelocity.x, sourceVelocity.y, sourceVelocity.z);
                    acousticPreviousAupWritePool[writeIndex] = acousticMeters - velocity * (1.0 / 60.0);
                }

                _virtualVoiceWriteCount = writeIndex + 1;
                _virtualVoiceDtoCount = math.max(_virtualVoiceDtoCount, _virtualVoiceWriteCount);
            }
            finally
            {
                vault.ReleaseMutationGuard(VirtualVoiceAppendMutationGuardMask);
            }
        }

        /// <inheritdoc />
        public void SetVirtualListener(in AcousticAup listenerAup)
        {
            if (!IsInitialized)
                return;

            if (!AcousticAup.IsFinite(in listenerAup))
            {
                _hasVirtualListenerAup = false;
                return;
            }

            _virtualListenerAup = listenerAup;
            _hasVirtualListenerAup = true;
        }

        /// <inheritdoc />
        public void SetVirtualizationQualityWeight(float qualityWeight01)
        {
            if (!IsInitialized)
                return;

            ApplyVirtualVoiceQualityWeight(qualityWeight01);
        }

        private float3 ResolveVirtualListenerVelocity(in AbsoluteUniversePosition listenerAup, float deltaTime)
        {
            if (!_hasVirtualPreviousListenerVelocityAup || deltaTime <= 0.0001f)
            {
                _virtualPreviousListenerVelocityAup = listenerAup;
                _hasVirtualPreviousListenerVelocityAup = true;
                return float3.zero;
            }

            float deltaTimeInv = math.rcp(math.max(deltaTime, 0.0001f));
            float3 delta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in listenerAup, in _virtualPreviousListenerVelocityAup);
            _virtualPreviousListenerVelocityAup = listenerAup;
            return delta * deltaTimeInv;
        }

        private VirtualVoiceSdfSampler ResolveVirtualVoiceSdfSampler()
        {
            float qualityWeight = ResolveVirtualVoiceQualityWeight();
            float occlusionWeight = math.lerp(0.25f, 1f, SmoothQuality01(qualityWeight));
            bool enabled = _listenerInsideBaseInteriorMuffle || _listenerCaveInterior01 >= 0.35f;
            return new VirtualVoiceSdfSampler
            {
                Center = float3.zero,
                HalfExtents = new float3(0.001f),
                WallPlaneY = 0f,
                WallThickness = _listenerInsideBaseInteriorMuffle
                    ? math.lerp(0.04f, 0.08f, occlusionWeight)
                    : math.lerp(0.015f, 0.06f, math.saturate(_listenerCaveInterior01) * occlusionWeight),
                Enabled = enabled ? (byte)1 : (byte)0,
                UseBox = 0
            };
        }

        /// <inheritdoc />
        public void ApplyVirtualVoiceAupShift(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            if (!IsInitialized)
                return;

            CompleteVirtualVoiceSort();
            RebaseVirtualVoicePool(
                in _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                _virtualVoiceWriteCount,
                gridDeltaX,
                gridDeltaY,
                gridDeltaZ);
            RebaseVirtualVoicePool(
                in _virtualVoiceSortPoolHandle,
                SpatialAudioVirtualVoiceSortPoolBufferId,
                _virtualVoiceSortCount,
                gridDeltaX,
                gridDeltaY,
                gridDeltaZ);
            RebaseAcousticSourcePool(
                in _acousticSourceWritePoolHandle,
                SpatialAudioAcousticSourceWritePoolBufferId,
                in _acousticPreviousAupWritePoolHandle,
                SpatialAudioAcousticPreviousAupWritePoolBufferId,
                _virtualVoiceWriteCount,
                gridDeltaX,
                gridDeltaY,
                gridDeltaZ);
            RebaseAcousticSourcePool(
                in _acousticSourceSortPoolHandle,
                SpatialAudioAcousticSourceSortPoolBufferId,
                in _acousticPreviousAupSortPoolHandle,
                SpatialAudioAcousticPreviousAupSortPoolBufferId,
                _virtualVoiceSortCount,
                gridDeltaX,
                gridDeltaY,
                gridDeltaZ);
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
            if (!IsVirtualizationReady)
            {
                statistics = default;
                return false;
            }

            statistics = _lastVirtualVoiceStatistics;
            return true;
        }

        public bool TryGetVirtualVoiceRuntimeTuning(out VirtualVoiceTuningSnapshot tuning)
        {
            if (!IsInitialized)
            {
                tuning = default;
                return false;
            }

            if (TryReadVirtualVoiceTuningFromVault(out tuning))
                return true;

            tuning = _virtualVoiceTuning;
            return false;
        }

        public void ApplyVirtualVoiceRuntimeTuning(in VirtualVoiceTuningSnapshot tuning)
        {
            if (!IsInitialized)
                return;

            VirtualVoiceTuningSnapshot sanitized = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            _virtualVoiceTuning = sanitized;
            WriteVirtualVoiceTuningToVault(in sanitized);
        }

#if UNITY_EDITOR
        public int ReloadAcousticMaterialRowsFromCsvCold(ReadOnlySpan<byte> csvBytes)
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    3,
                    out NativeArray<AcousticMaterialCoefficientDTO> rows))
            {
                return 0;
            }

            try
            {
                int parsed = VirtualVoiceProfileCsvParser.ParseMaterialRows(csvBytes, rows);
                return parsed > 0
                    ? parsed
                    : VirtualVoiceProfileCsvParser.GenerateEmergencyMockAcoustics(rows);
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(in _acousticMaterialRowsHandle, SpatialAudioAcousticMaterialRowsBufferId);
            }
        }
#endif

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
            if (!TryReadAudioVaultBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<VirtualVoiceSelection>.ReadOnly selections))
            {
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }

            IDataVault vault = _dataVault;
            if (vault == null || !TryLockAcousticOcclusionBuffers(vault))
            {
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }

            if (!TryOpenAudioVaultBuffer(
                    vault,
                    ref _acousticSelectedSourcePoolHandle,
                    SpatialAudioAcousticSelectedSourcePoolBufferId,
                    SystemID.Audio,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<AcousticSourceDTO> selectedSourcePool))
            {
                ReleaseAcousticOcclusionBufferLocks();
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }

            if (!TryOpenAudioVaultBuffer(
                    vault,
                    ref _acousticSelectedPreviousAupPoolHandle,
                    SpatialAudioAcousticSelectedPreviousAupPoolBufferId,
                    SystemID.Audio,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<double3> selectedPreviousAupPool))
            {
                ReleaseAcousticOcclusionBufferLocks();
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }

            if (!TryOpenAudioVaultBuffer(
                    vault,
                    ref _acousticDspOutputPoolHandle,
                    SpatialAudioAcousticDspOutputPoolBufferId,
                    SystemID.Audio,
                    MaxVirtualVoiceCapacity,
                    out NativeArray<AcousticDspOutputDTO> dspOutputPool))
            {
                ReleaseAcousticOcclusionBufferLocks();
                _acousticOcclusionOutputCount = 0;
                _acousticOcclusionStartTicks = 0L;
                _lastAcousticOcclusionTimeMs = 0f;
                return;
            }

            int count = PopulateSelectedAcousticSources(sourceCount, selections, selectedSourcePool, selectedPreviousAupPool);
            if (count <= 0)
            {
                ReleaseAcousticOcclusionBufferLocks();
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
                out float sdfRange,
                out bool acousticSdfSnapshotLocked);
            double3 listenerVelocity = new double3(
                _virtualListenerVelocityMetersPerSecond.x,
                _virtualListenerVelocityMetersPerSecond.y,
                _virtualListenerVelocityMetersPerSecond.z);
            double3 previousListenerMeters = listenerMeters - listenerVelocity * safeDelta;
            float rightSq = math.lengthsq(listenerRight);
            float3 right = rightSq > 0.000001f
                ? listenerRight * math.rsqrt(math.max(rightSq, 0.000001f))
                : new float3(1f, 0f, 0f);
            bool materialRowsLocked = TryLockAcousticMaterialRowsForOcclusion(
                out NativeArray<AcousticMaterialCoefficientDTO>.ReadOnly materialRowsView);
            var occlusionJob = new AcousticOcclusionJob
            {
                Sources = selectedSourcePool,
                Outputs = dspOutputPool,
                PreviousSourceAup = selectedPreviousAupPool,
                SdfVoxels = hasVoxelSdf ? sdfVoxels : default,
                Materials = materialRowsView,
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
            if (hasVoxelSdf)
                _acousticOcclusionSdfSnapshotGuardHeld = acousticSdfSnapshotLocked;
            bool scheduleAccepted = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            try
            {
                _acousticOcclusionHandle = occlusionJob.Schedule(count, 16);
                _acousticOcclusionScheduled = true;
                scheduleAccepted = true;
            }
            catch
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SpatialAudioFailureAcousticOcclusionSchedule));
            }
            finally
            {
                if (!scheduleAccepted)
                {
                    if (materialRowsLocked)
                        ReleaseAcousticMaterialRowsOcclusionLock();
                    ReleaseAcousticOcclusionBufferLocks();
                    _acousticMaterialRowsLockedForOcclusion = false;
                    _acousticMaterialRowsLockVault = null;
                    _acousticOcclusionScheduled = false;
                }
            }
#else
            try
            {
                _acousticOcclusionHandle = occlusionJob.Schedule(count, 16);
                _acousticOcclusionScheduled = true;
                scheduleAccepted = true;
            }
            finally
            {
                if (!scheduleAccepted)
                {
                    if (materialRowsLocked)
                        ReleaseAcousticMaterialRowsOcclusionLock();
                    ReleaseAcousticOcclusionBufferLocks();
                    _acousticMaterialRowsLockedForOcclusion = false;
                    _acousticMaterialRowsLockVault = null;
                    _acousticOcclusionScheduled = false;
                }
            }
#endif
        }

        private bool TrySnapshotAcousticSdfPayload(
            Vector3 targetRuntimePosition,
            out NativeArray<byte>.ReadOnly sdfVoxels,
            out int3 sdfDimensions,
            out float3 sdfOrigin,
            out float3 sdfCellSize,
            out float sdfRange,
            out bool snapshotLocked)
        {
            sdfVoxels = default;
            sdfDimensions = default;
            sdfOrigin = float3.zero;
            sdfCellSize = float3.zero;
            sdfRange = 0f;
            snapshotLocked = false;

            if (!HectonVoxelVolume.TryAcquireClosestPublishedSonarSdfPayloadReadLease(
                    targetRuntimePosition,
                    out HectonVoxelVolume publishedVolume,
                    out NativeArray<byte>.ReadOnly publishedSdf,
                    out _,
                    out Vector3Int publishedDimensions,
                    out Vector3 publishedOrigin,
                    out Vector3 publishedCellSize,
                    out float publishedRange,
                    out _,
                    out HectonVoxelVolume.PublishedSonarSdfReadLease publishedLease))
            {
                return false;
            }

            int3 resolvedDimensions = new int3(publishedDimensions.x, publishedDimensions.y, publishedDimensions.z);
            bool accepted = false;
            try
            {
                if (!TryResolveAcousticSdfVoxelCount(resolvedDimensions, out int expectedLength) ||
                    !publishedSdf.IsCreated ||
                    publishedSdf.Length < expectedLength)
                {
                    return false;
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

                if (!TryCopyAcousticSdfLeaseToSnapshot(publishedSdf, expectedLength, out NativeArray<byte>.ReadOnly snapshotSdf, out snapshotLocked))
                    return false;

                sdfVoxels = snapshotSdf;
                sdfDimensions = resolvedDimensions;
                sdfOrigin = resolvedOrigin;
                sdfCellSize = resolvedCellSize;
                sdfRange = resolvedRange;
                accepted = true;
                return true;
            }
            finally
            {
                if (publishedVolume != null)
                    publishedVolume.ReleasePublishedSonarSdfPayloadReadLease(in publishedLease);
                if (!accepted)
                    UnlockAcousticOcclusionSdfSnapshot(ref snapshotLocked);
            }
        }

        private bool TryCopyAcousticSdfLeaseToSnapshot(
            NativeArray<byte>.ReadOnly sourceSdf,
            int requiredLength,
            out NativeArray<byte>.ReadOnly snapshotSdf,
            out bool snapshotLocked)
        {
            snapshotSdf = default;
            snapshotLocked = false;
            if (!sourceSdf.IsCreated || requiredLength <= 0 || sourceSdf.Length < requiredLength)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            if (!EnsureAudioVaultHandle(
                    ref _acousticVoxelSdfTexture3DHandle,
                    SpatialAudioAcousticVoxelSdfTexture3DBufferId,
                    requiredLength,
                    NativeArrayOptions.UninitializedMemory))
            {
                return false;
            }

            if (vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(AcousticOcclusionSdfSnapshotMutationGuardMask))
            {
                return false;
            }

            NativeArray<byte> snapshot = default;
            bool snapshotReady = false;
            bool mutationGuardHeld = true;
            try
            {
                if (vault.IsCompactionFenceActive)
                    return false;

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticVoxelSdfTexture3DHandle,
                        SpatialAudioAcousticVoxelSdfTexture3DBufferId,
                        SystemID.Audio,
                        requiredLength,
                        out snapshot))
                {
                    return false;
                }

                for (int i = 0; i < requiredLength; i++)
                    snapshot[i] = sourceSdf[i];

                snapshotReady = true;
            }
            finally
            {
                if (mutationGuardHeld)
                    vault.ReleaseMutationGuard(AcousticOcclusionSdfSnapshotMutationGuardMask);
                if (!snapshotLocked)
                    _acousticOcclusionSdfSnapshotGuardVault = null;
            }

            if (!snapshotReady || !vault.TryLockBuffer(SpatialAudioAcousticVoxelSdfTexture3DBufferId, SystemID.Audio))
                return false;

            snapshotLocked = true;
            _acousticOcclusionSdfSnapshotGuardVault = vault;
            snapshotSdf = snapshot.AsReadOnly();
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

        private int PopulateSelectedAcousticSources(
            int selectedCount,
            NativeArray<VirtualVoiceSelection>.ReadOnly selections,
            NativeArray<AcousticSourceDTO> selectedSourcePool,
            NativeArray<double3> selectedPreviousAupPool)
        {
            if (!selections.IsCreated ||
                !selectedSourcePool.IsCreated ||
                !selectedPreviousAupPool.IsCreated)
            {
                return 0;
            }

            int selectionLimit = math.clamp(
                selectedCount,
                0,
                math.min(
                    selections.Length,
                    math.min(selectedSourcePool.Length, selectedPreviousAupPool.Length)));
            int written = 0;
            double safeDelta = math.max((double)_virtualSimulationTickDeltaSeconds, 0.0001);
            for (int i = 0; i < selectionLimit; i++)
            {
                VirtualVoiceSelection selection = selections[i];
                if (selection.StableKey == 0u)
                    continue;

                double3 acousticMeters = ToAbsoluteAcousticMeters(in selection.SourceAup);
                float3 velocity = math.all(math.isfinite(selection.SourceVelocityMetersPerSecond))
                    ? selection.SourceVelocityMetersPerSecond
                    : float3.zero;
                selectedSourcePool[written] = new AcousticSourceDTO
                {
                    SourceHash = selection.StableKey,
                    BaseVolume = math.saturate(SanitizeFinite(selection.Volume, 0f)),
                    BasePitch = math.clamp(SanitizeFinite(selection.Pitch, 1f), 0.1f, 3f),
                    Flags = (uint)selection.DspFlags | ((uint)selection.PortalFlags << 8),
                    AUP_Position = acousticMeters,
                    ComputedOcclusion = 0f,
                    ComputedReverb = 0f
                };
                selectedPreviousAupPool[written] = acousticMeters - new double3(velocity.x, velocity.y, velocity.z) * safeDelta;

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

            bool completed;
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _acousticOcclusionHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            if (!completed)
                return false;

            FinishAcousticOcclusionCompletion();
            return true;
        }

        private void FinishAcousticOcclusionCompletion()
        {
            _acousticOcclusionScheduled = false;
            if (_acousticMaterialRowsLockedForOcclusion)
                ReleaseAcousticMaterialRowsOcclusionLock();
            ReleaseAcousticOcclusionBufferLocks();

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
            WriteVirtualVoiceStatisticsSnapshot(in _lastVirtualVoiceStatistics);
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
                overrunStatistics.Frame = SystemDispatcher.CurrentFrameIndex;
                overrunStatistics.TotalVoices = math.clamp(_virtualVoiceSortCount, 0, MaxVirtualVoiceCapacity);
                overrunStatistics.DroppedVoices += math.clamp(_virtualVoiceWriteCount, 0, MaxVirtualVoiceCapacity);
                overrunStatistics.SortTimeMs = math.max(
                    0.5f,
                    ((float)SystemDispatcher.CurrentUnscaledTimeSeconds - _virtualVoiceSortStartRealtimeSeconds) * 1000f);
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
            bool completed;
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                completed = DispatcherJobFence.TryComplete(ref _virtualVoiceSortHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            if (!completed)
                return false;

            FinishVirtualVoiceSortCompletion(sortWaitStartTicks);
            return true;
        }

        private void FinishVirtualVoiceSortCompletion(long sortWaitStartTicks)
        {
            long sortWaitTicks = System.Diagnostics.Stopwatch.GetTimestamp() - sortWaitStartTicks;
            _virtualVoiceSortScheduled = false;
            ReleaseVirtualVoiceSortBufferLocks();
            if (!TryReadVirtualVoiceStatisticsSnapshot(out VirtualVoiceStatistics completedStatistics))
            {
                _lastVirtualVoiceStatistics = default;
                _virtualVoiceSortCount = 0;
                _acousticOcclusionOutputCount = 0;
                return;
            }

            _lastVirtualVoiceStatistics = completedStatistics;
            float elapsedMs = math.max(0f, (float)(sortWaitTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency));
            _lastVirtualVoiceStatistics.SortTimeMs = elapsedMs;
            _lastVirtualVoiceStatistics.AcousticOcclusionTimeMs = _lastAcousticOcclusionTimeMs;
            WriteVirtualVoiceStatisticsSnapshot(in _lastVirtualVoiceStatistics);
            _virtualVoiceSortCount = math.clamp(_lastVirtualVoiceStatistics.AudibleVoices, 0, MaxVirtualVoiceCapacity);

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
                WriteVirtualVoiceStatisticsSnapshot(in _lastVirtualVoiceStatistics);
                PushVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
                PublishVirtualVoiceTelemetry(in _lastVirtualVoiceStatistics);
            }
        }

        private void InjectVirtualVoiceSelections()
        {
            if (!TryReadAudioVaultBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<VirtualVoiceSelection>.ReadOnly selections) ||
                _virtualChannelStableKeys == null ||
                _virtualChannelSourceIndices == null)
            {
                return;
            }

            NativeArray<AcousticDspOutputDTO>.ReadOnly dspOutputs = default;
            if (_acousticOcclusionOutputCount > 0)
            {
                TryReadAudioVaultBuffer(
                    in _acousticDspOutputPoolHandle,
                    SpatialAudioAcousticDspOutputPoolBufferId,
                    MaxVirtualVoiceCapacity,
                    out dspOutputs);
            }

            int outputCapacity = _pool != null ? math.min(_pool.Length, MaxVirtualPhysicalVoices) : MaxVirtualPhysicalVoices;
            int safeLimit = math.clamp(_lastVirtualVoiceStatistics.PhysicalVoiceLimit, 0, outputCapacity);
            int selectedCount = math.clamp(_lastVirtualVoiceStatistics.ActivePhysicalVoices, 0, math.min(safeLimit, selections.Length));
            for (int channel = safeLimit; channel < MaxVirtualPhysicalVoices; channel++)
                BeginVirtualChannelFadeToSilence(channel);

            for (int i = 0; i < selectedCount; i++)
            {
                VirtualVoiceSelection selection = selections[i];
                if (selection.StableKey == 0u)
                    continue;

                ApplyAcousticDspOutputToSelection(ref selection, dspOutputs);
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
                if (key != 0u && !IsVirtualStableKeySelected(key, selectedCount, selections))
                    BeginVirtualChannelFadeToSilence(channel);
            }
        }

        private void ApplyAcousticDspOutputToSelection(
            ref VirtualVoiceSelection selection,
            NativeArray<AcousticDspOutputDTO>.ReadOnly dspOutputs)
        {
            if (!dspOutputs.IsCreated || selection.StableKey == 0u)
                return;

            int limit = math.clamp(_acousticOcclusionOutputCount, 0, dspOutputs.Length);
            for (int i = 0; i < limit; i++)
            {
                AcousticDspOutputDTO output = dspOutputs[i];
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
                int sourceIndex = PlayAtPointResolved(
                    TryResolveAudioEventClip(selection.EventID, selection.ClipHash, out AudioClip clip) ? clip : null,
                    runtimePosition,
                    in sourceAup,
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
            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAupRuntimePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            if (hasListener)
                runtimePosition = ToListenerRelativeRuntimeVector3(in sourceAup, true, in listenerAup, listenerRuntimePosition);
            Vector3 audiblePosition = runtimePosition;
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
            TouchPlaybackClip(clip, ResolveWorldResidencyDomain(clip, ResolvedDefaultWorldMixerGroup));
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
            source.priority = ResolveUnityAudioPriority(clip, source.outputAudioMixerGroup, clampedVolume, lodTier);
            float dopplerRatio = math.clamp(
                selection.DopplerRatio,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && sourceIndex < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[sourceIndex] = dopplerRatio;
            source.pitch = ResolveSourcePitch(sourceIndex, dopplerRatio);
            _audioLodTiers[sourceIndex] = lodTier;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            UpdateWorldSourceAudioLod(
                sourceIndex,
                source,
                audiblePosition,
                in audibleAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
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
            SignalBus<AcousticPingSignal>.TryPushTracked(in ping, ref s_x001SpatialAudioManagerSignalPushDropCount);
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
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
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

        private static bool IsVirtualStableKeySelected(
            uint stableKey,
            int selectedCount,
            NativeArray<VirtualVoiceSelection>.ReadOnly selections)
        {
            if (stableKey == 0u || !selections.IsCreated)
                return false;

            int safeCount = math.min(selectedCount, selections.Length);
            for (int i = 0; i < safeCount; i++)
            {
                if (selections[i].StableKey == stableKey)
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
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<VirtualVoiceSelection> selections))
            {
                return;
            }

            try
            {
                for (int i = 0; i < selections.Length; i++)
                    selections[i] = default;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections);
            }
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
            in VaultGenerationHandle<VirtualVoice> handle,
            BufferID bufferId,
            int count,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ)
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in handle,
                    bufferId,
                    MaxVirtualVoiceCapacity,
                    out NativeArray<VirtualVoice> voices))
            {
                return;
            }

            try
            {
                RebaseVirtualVoicePool(voices, count, gridDeltaX, gridDeltaY, gridDeltaZ);
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(in handle, bufferId);
            }
        }

        private static void RebaseVirtualVoicePool(
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
            in VaultGenerationHandle<AcousticSourceDTO> sourceHandle,
            BufferID sourceBufferId,
            in VaultGenerationHandle<double3> previousAupHandle,
            BufferID previousAupBufferId,
            int count,
            long gridDeltaX,
            long gridDeltaY,
            long gridDeltaZ)
        {
            IDataVault vault = _dataVault;
            ulong guardMask = AudioVaultMutationGuardBit(sourceBufferId) | AudioVaultMutationGuardBit(previousAupBufferId);
            if (vault == null || !vault.TryAcquireMutationGuard(guardMask))
                return;

            try
            {
                VaultGenerationHandle<AcousticSourceDTO> sourceResolveHandle = sourceHandle;
                VaultGenerationHandle<double3> previousAupResolveHandle = previousAupHandle;
                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref sourceResolveHandle,
                        sourceBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<AcousticSourceDTO> sources) ||
                    !TryOpenAudioVaultBuffer(
                        vault,
                        ref previousAupResolveHandle,
                        previousAupBufferId,
                        SystemID.Audio,
                        MaxVirtualVoiceCapacity,
                        out NativeArray<double3> previousAup))
                {
                    return;
                }

                RebaseAcousticSourcePool(sources, previousAup, count, gridDeltaX, gridDeltaY, gridDeltaZ);
            }
            finally
            {
                vault.ReleaseMutationGuard(guardMask);
            }
        }

        private static void RebaseAcousticSourcePool(
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
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _virtualVoiceDtoPoolHandle,
                    SpatialAudioVirtualVoiceDtoPoolBufferId,
                    MaxVirtualVoiceCapacity,
                    out NativeArray<VirtualVoiceDTO> virtualVoiceDtoPool))
            {
                return;
            }

            try
            {
                double cellSize = AcousticAup.CellSizeMeters;
                double3 deltaMeters = new double3(gridDeltaX * cellSize, gridDeltaY * cellSize, gridDeltaZ * cellSize);
                int safeCount = math.clamp(_virtualVoiceDtoCount, 0, virtualVoiceDtoPool.Length);
                for (int i = 0; i < safeCount; i++)
                {
                    VirtualVoiceDTO dto = virtualVoiceDtoPool[i];
                    dto.AupMeters += deltaMeters;
                    virtualVoiceDtoPool[i] = dto;
                }
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _virtualVoiceDtoPoolHandle,
                    SpatialAudioVirtualVoiceDtoPoolBufferId);
            }
        }

        private void RebaseVirtualVoiceSelections(long gridDeltaX, long gridDeltaY, long gridDeltaZ)
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<VirtualVoiceSelection> selections))
            {
                return;
            }

            try
            {
                for (int i = 0; i < selections.Length; i++)
                {
                    VirtualVoiceSelection selection = selections[i];
                    if (selection.StableKey == 0u)
                        continue;

                    selection.SourceAup.GridX += gridDeltaX;
                    selection.SourceAup.GridY += gridDeltaY;
                    selection.SourceAup.GridZ += gridDeltaZ;
                    selections[i] = selection;
                }
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections);
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
            return _foveatedSimulationDirector;
        }

        private void EnsureSpatialAudioPolicyCached()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_spatialAudioPolicyRefreshFrame == frame)
                return;

            CacheSpatialAudioPolicy(ResolveGlobalSpatialAudioQualityWeight01(), frame);
        }

        private void RefreshSpatialAudioPolicyCold()
        {
            CacheSpatialAudioPolicy(ResolveGlobalSpatialAudioQualityWeight01(), SystemDispatcher.CurrentFrameIndex);
        }

        private void RefreshCachedAudioRuntimeServicesCold()
        {
            int nextResolveFrame = SystemDispatcher.CurrentFrameIndex + SpatialAudioRegistryRetryFrames;
            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            IWeatherService weatherService = GlobalRegistry.Weather;
            IHectonOceanKinematicsService oceanKinematicsService = GlobalRegistry.OceanKinematics;

            _cachedPlayerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
            _cachedWeatherService = weatherService != null && weatherService.IsInitialized ? weatherService : null;
            _cachedOceanKinematicsService = oceanKinematicsService != null && oceanKinematicsService.IsInitialized ? oceanKinematicsService : null;
            _cachedAcousticZone = GlobalRegistry.AcousticZoneReadModel;
            _cachedSurfaceWeatherDirector = GlobalRegistry.SurfaceWeatherReadModel;
            CachePlayerCriticalAudio(GlobalRegistry.PlayerCriticalAudioSignals);
            _cachedHabitatGraph = GlobalRegistry.HabitatGraph;
            _foveatedSimulationDirector = GlobalRegistry.FoveatedSimulationDirector;
            _dataVault = GlobalRegistry.DataVault;
            _listenerPlayerMovementTrauma = GlobalRegistry.PlayerMovementContracts;
            _physicsStateEvents = GlobalRegistry.PhysicsStateEvents;
            _playerRuntimeContextResolveFrame = nextResolveFrame;
            _weatherServiceResolveFrame = nextResolveFrame;
            _acousticZoneResolveFrame = nextResolveFrame;
            _surfaceWeatherResolveFrame = nextResolveFrame;
            _foveatedDirectorResolveFrame = nextResolveFrame;
            ResolveListenerTransformCold();
        }

        private void CacheReboundAudioRuntimeService(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            int nextResolveFrame = SystemDispatcher.CurrentFrameIndex + SpatialAudioRegistryRetryFrames;
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Player:
                    IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;
                    _cachedPlayerRuntimeContext = playerContext != null && playerContext.IsInitialized ? playerContext : null;
                    _playerRuntimeContextResolveFrame = nextResolveFrame;
                    ResolveListenerTransformCold();
                    break;
                case GlobalRegistryServiceSlot.PlayerMovementContracts:
                    _listenerPlayerMovementTrauma = currentService as IPlayerMovementTraumaSink;
                    break;
                case GlobalRegistryServiceSlot.Weather:
                    IWeatherService weatherService = currentService as IWeatherService;
                    _cachedWeatherService = weatherService != null && weatherService.IsInitialized ? weatherService : null;
                    _weatherServiceResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.OceanKinematics:
                    IHectonOceanKinematicsService oceanKinematicsService = currentService as IHectonOceanKinematicsService;
                    _cachedOceanKinematicsService = oceanKinematicsService != null && oceanKinematicsService.IsInitialized ? oceanKinematicsService : null;
                    break;
                case GlobalRegistryServiceSlot.AcousticZoneRuntime:
                    _cachedAcousticZone = currentService as IAcousticZoneReadModel;
                    _acousticZoneResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.SurfaceWeatherRuntime:
                    _cachedSurfaceWeatherDirector = currentService as ISurfaceWeatherReadModel;
                    _surfaceWeatherResolveFrame = nextResolveFrame;
                    break;
                case GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime:
                    CachePlayerCriticalAudio(currentService as IPlayerCriticalAudioSignalSink);
                    break;
                case GlobalRegistryServiceSlot.Logistics:
                    _cachedHabitatGraph = currentService as IHabitatGraphService;
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
                case GlobalRegistryServiceSlot.PhysicsStateManager:
                    RebindPhysicsStateEventService(currentService as IPhysicsStateEventService);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterDispatcherLanes();
                    if (currentService != null && _isInitialized && isActiveAndEnabled)
                        TryRegisterDispatcherLanes();
                    break;
            }
        }

        private void CachePlayerCriticalAudio(IPlayerCriticalAudioSignalSink playerCriticalAudio)
        {
            _cachedPlayerCriticalAudio = IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio)
                ? playerCriticalAudio
                : null;
        }

        private IPlayerCriticalAudioSignalSink ResolvePlayerCriticalAudioSignalSink()
        {
            IPlayerCriticalAudioSignalSink playerCriticalAudio = _cachedPlayerCriticalAudio;
            if (IsPlayerCriticalAudioSignalSinkUsable(playerCriticalAudio))
                return playerCriticalAudio;

            _cachedPlayerCriticalAudio = null;
            return null;
        }

        private static bool IsPlayerCriticalAudioSignalSinkUsable(IPlayerCriticalAudioSignalSink playerCriticalAudio)
        {
            if (playerCriticalAudio == null)
                return false;

            if (playerCriticalAudio is Behaviour behaviour)
                return behaviour != null && behaviour.isActiveAndEnabled;

            return true;
        }

        private void TryRegisterPhysicsImpactListener()
        {
            if (_physicsImpactRegistered)
                return;

            RebindPhysicsStateEventService(_physicsStateEvents ?? GlobalRegistry.PhysicsStateEvents);
        }

        private void TryUnregisterPhysicsImpactListener()
        {
            if (!_physicsImpactRegistered)
            {
                _physicsStateEvents = null;
                return;
            }

            _physicsStateEvents?.UnregisterImpactListener(this);
            _physicsStateEvents = null;
            _physicsImpactRegistered = false;
        }

        private void RebindPhysicsStateEventService(IPhysicsStateEventService physicsStateEvents)
        {
            if (ReferenceEquals(_physicsStateEvents, physicsStateEvents) && _physicsImpactRegistered)
                return;

            if (_physicsImpactRegistered)
                _physicsStateEvents?.UnregisterImpactListener(this);

            _physicsStateEvents = physicsStateEvents;
            _physicsImpactRegistered = false;

            if (_physicsStateEvents == null ||
                !isActiveAndEnabled ||
                !IsPhysicsStateEventServiceUsable(_physicsStateEvents))
                return;

            _physicsStateEvents.RegisterImpactListener(this);
            _physicsImpactRegistered = true;
        }

        private static bool IsPhysicsStateEventServiceUsable(IPhysicsStateEventService physicsStateEvents)
        {
            return physicsStateEvents != null && physicsStateEvents.IsInitialized;
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
            bool platformSampleRateConstrained =
                HardwareTierDetector.IsQuest3Like ||
                QuestVulkanRuntimePolicy.IsQuestRuntimeActive;
            if (!platformSampleRateConstrained)
                return;

            AudioConfiguration configuration = AudioSettings.GetConfiguration();
            if (configuration.sampleRate > 0 && configuration.sampleRate <= SurvivalAmbientOutputSampleRate)
            {
                _lastAudioOutputSampleRate = configuration.sampleRate;
                return;
            }

            configuration.sampleRate = SurvivalAmbientOutputSampleRate;
            if (AudioSettings.Reset(configuration))
                _lastAudioOutputSampleRate = SurvivalAmbientOutputSampleRate;
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
            IPlayerRuntimeContext playerContext = _cachedPlayerRuntimeContext;
            return playerContext != null && playerContext.IsInitialized ? playerContext : null;
        }

        private IWeatherService ResolveWeatherService()
        {
            IWeatherService weatherService = _cachedWeatherService;
            return weatherService != null && weatherService.IsInitialized ? weatherService : null;
        }

        private IAcousticZoneReadModel ResolveAcousticZone()
        {
            return _cachedAcousticZone;
        }

        private ISurfaceWeatherReadModel ResolveSurfaceWeatherDirector()
        {
            return _cachedSurfaceWeatherDirector;
        }

        private float ResolveVirtualVoiceQualityWeight()
        {
            float weight = HomeostasisBrain.GlobalQualityWeight;
            IDataVault vault = _dataVault;
            if (vault != null &&
                IsAudioVaultHandle(
                    in _virtualVoiceScalabilityStateHandle,
                    BufferID.ShinobuScalabilityState,
                    SystemID.GraphicsScalability) &&
                vault.TryReadOnlyHandle(
                    in _virtualVoiceScalabilityStateHandle,
                    out NativeArray<ScalabilityStateDTO>.ReadOnly scalabilityState) &&
                scalabilityState.Length > 0)
            {
                ScalabilityStateDTO state = scalabilityState[0];
                if (math.isfinite(state.GlobalQualityWeight))
                    weight = state.GlobalQualityWeight;
            }

            EnsureSpatialAudioPolicyCached();
            weight = math.min(weight, _cachedSpatialAudioQualityWeight01);
            return SmoothQuality01(weight);
        }

        private bool ResolveRollbackAudioSuppressionActive()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsAudioVaultHandle(
                    in _virtualVoiceRollbackAudioSuppressionHandle,
                    RollbackNetcodeVault.AudioSuppression,
                    RollbackNetcodeVault.OwnerSystem) ||
                !vault.TryReadOnlyHandle(
                    in _virtualVoiceRollbackAudioSuppressionHandle,
                    out NativeArray<RollbackAudioSuppressionDTO>.ReadOnly rollbackSuppression) ||
                rollbackSuppression.Length <= 0)
            {
                return false;
            }

            RollbackAudioSuppressionDTO audio = rollbackSuppression[0];
            uint frame = (uint)SystemDispatcher.CurrentFrameIndex;
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

            _virtualVoiceTierPendingSlowTicks++;
            if (_virtualVoiceTierPendingSlowTicks >= VirtualVoiceQualityHysteresisSlowTicks)
            {
                ApplyVirtualVoiceQualityWeight(targetWeight);
                return;
            }

            float blend = math.saturate(_virtualVoiceTierPendingSlowTicks * math.rcp(math.max(1f, VirtualVoiceQualityHysteresisSlowTicks)));
            float smoothed = math.lerp(_virtualVoiceQualityWeight, targetWeight, blend);
            _virtualVoiceQualityWeight = smoothed;
            _virtualPhysicalVoiceLimit = VirtualVoiceUtility.ResolveContinuousVoiceBudget(smoothed);
        }

        private void ApplyVirtualVoiceQualityWeight(float qualityWeight)
        {
            float sanitized = math.saturate(SanitizeFinite(qualityWeight, 1f));
            _virtualVoiceQualityWeight = sanitized;
            _virtualPhysicalVoiceLimit = VirtualVoiceUtility.ResolveContinuousVoiceBudget(sanitized);
            _virtualVoiceTierPendingSlowTicks = 0;
        }

        private void PushVirtualVoiceTelemetry(in VirtualVoiceStatistics statistics)
        {
            float loudestWeight = 0f;
            if (statistics.ActivePhysicalVoices > 0 &&
                TryReadAudioVaultBuffer(
                    in _virtualVoiceSelectionsHandle,
                    BufferID.SpatialAudioVirtualVoiceSelections,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<VirtualVoiceSelection>.ReadOnly selections) &&
                selections.Length > 0)
            {
                loudestWeight = selections[0].Weight;
            }

            if (statistics.LoudestWeight > loudestWeight)
                loudestWeight = statistics.LoudestWeight;

            uint stateHash = ComputeVirtualVoiceStateHash(in statistics, loudestWeight);
            ushort qualityWeightQ8 = EncodeVirtualVoiceQualityQ8(_virtualVoiceQualityWeight);
            bool dumpRequired =
                !math.isfinite(loudestWeight) ||
                !math.isfinite(statistics.SortTimeMs) ||
                !math.isfinite(statistics.AcousticOcclusionTimeMs) ||
                statistics.SortTimeMs > 0.5f ||
                statistics.AcousticOcclusionTimeMs > 1.0f;

            IDataVault vault = _dataVault;
            NativeArray<AcousticOcclusionTelemetryEntry> blackBox = default;
            bool lockAcquired =
                vault != null &&
                IsAudioVaultHandle(
                    in _virtualVoiceBlackBoxHandle,
                    BufferID.SpatialAudioVirtualVoiceBlackBox,
                    SystemID.Audio) &&
                vault.TryAcquireWriteLock(
                    in _virtualVoiceBlackBoxHandle,
                    SystemID.Audio,
                    out blackBox);

            if (!lockAcquired ||
                !blackBox.IsCreated ||
                blackBox.Length == 0)
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _virtualVoiceBlackBoxHandle, SystemID.Audio);
                return;
            }

            bool wroteBlackBox = false;
            try
            {
                int index = _virtualVoiceBlackBoxCursor % blackBox.Length;
                blackBox[index] = new AcousticOcclusionTelemetryEntry
                {
                    Frame = statistics.Frame,
                    TotalVoices = (ushort)math.clamp(statistics.TotalVoices, 0, ushort.MaxValue),
                    AudibleVoices = (ushort)math.clamp(statistics.AudibleVoices, 0, ushort.MaxValue),
                    CulledVoices = (ushort)math.clamp(statistics.CulledVoices, 0, ushort.MaxValue),
                    ActiveVoices = (ushort)math.clamp(statistics.ActivePhysicalVoices, 0, ushort.MaxValue),
                    PhysicalVoiceLimit = (ushort)math.clamp(statistics.PhysicalVoiceLimit, 0, ushort.MaxValue),
                    StolenVoices = (ushort)math.clamp(statistics.StolenVoices, 0, ushort.MaxValue),
                    DroppedVoices = (ushort)math.clamp(statistics.DroppedVoices, 0, ushort.MaxValue),
                    Flags = (ushort)(_hasVirtualListenerAup ? 1 : 0),
                    OccludedVoices = (ushort)math.clamp(statistics.OccludedVoices, 0, ushort.MaxValue),
                    DelayedVoices = (ushort)math.clamp(statistics.DelayedVoices, 0, ushort.MaxValue),
                    QualityWeightQ8 = qualityWeightQ8,
                    StateHash = stateHash,
                    LoudestWeight = loudestWeight,
                    SortTimeMs = statistics.SortTimeMs,
                    AverageRt60Seconds = statistics.AverageRt60Seconds,
                    AverageLowPassHertz = statistics.AverageLowPassHertz,
                    MaximumDelaySeconds = statistics.MaximumDelaySeconds,
                    AcousticOcclusionTimeMs = statistics.AcousticOcclusionTimeMs
                };
                _virtualVoiceBlackBoxCursor = (_virtualVoiceBlackBoxCursor + 1) % blackBox.Length;
                wroteBlackBox = true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _virtualVoiceBlackBoxHandle, SystemID.Audio);
            }

            if (wroteBlackBox && dumpRequired)
                DumpVirtualVoiceBlackBox();
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

        private static ushort EncodeVirtualVoiceQualityQ8(float qualityWeight01)
        {
            float quality = math.saturate(SanitizeFinite(qualityWeight01, 0f));
            return (ushort)math.clamp((int)math.round(quality * 255f), 0, 255);
        }

        private void DumpVirtualVoiceBlackBox()
        {
#if !UNITY_EDITOR
            if (_virtualVoiceBlackBoxDumped)
                return;

            _virtualVoiceBlackBoxDumped = true;
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)SpatialAudioFailureVirtualVoiceDumpIo));
            return;
#else
            IDataVault vault = _dataVault;
            if (_virtualVoiceBlackBoxDumped ||
                vault == null ||
                !IsAudioVaultHandle(
                    in _virtualVoiceBlackBoxHandle,
                    BufferID.SpatialAudioVirtualVoiceBlackBox,
                    SystemID.Audio) ||
                !vault.TryReadOnlyHandle(
                    in _virtualVoiceBlackBoxHandle,
                    out NativeArray<AcousticOcclusionTelemetryEntry>.ReadOnly blackBox) ||
                !blackBox.IsCreated)
            {
                return;
            }

            try
            {
                const int headerBytes = sizeof(int) * 2;
                const int entryBytes = 52;
                int totalBytes = headerBytes + (blackBox.Length * entryBytes);
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(SpatialAudioManager),
                    "virtualVoiceBlackBoxPayload");
                try
                {
                    unsafe
                    {
                        byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                        Span<byte> payloadSpan = new Span<byte>(payloadPtr, totalBytes);
                        BinaryPrimitives.WriteInt32LittleEndian(payloadSpan.Slice(0, 4), _virtualVoiceBlackBoxCursor);
                        BinaryPrimitives.WriteInt32LittleEndian(payloadSpan.Slice(4, 4), blackBox.Length);

                        int offset = headerBytes;
                        for (int i = 0; i < blackBox.Length; i++)
                        {
                            AcousticOcclusionTelemetryEntry entry = blackBox[i];
                            WriteVirtualVoiceTelemetryEntry(payloadSpan.Slice(offset, entryBytes), in entry);
                            offset += entryBytes;
                        }
                    }

                    _virtualVoiceBlackBoxDumped = NativeFaultDumpWriter.TryWriteAll(VirtualVoiceDumpRelativePath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(SpatialAudioManager),
                        "virtualVoiceBlackBoxPayload");
                }
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogException(exception, this);
#endif
            }
#endif
        }

        private static void WriteVirtualVoiceTelemetryEntry(Span<byte> destination, in AcousticOcclusionTelemetryEntry entry)
        {
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(0, 4), entry.Frame);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(4, 2), entry.TotalVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, 2), entry.AudibleVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(8, 2), entry.CulledVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(10, 2), entry.ActiveVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(12, 2), entry.PhysicalVoiceLimit);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(14, 2), entry.StolenVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(16, 2), entry.DroppedVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(18, 2), entry.Flags);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(20, 2), entry.OccludedVoices);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(22, 2), entry.DelayedVoices);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(24, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(28, 4), math.asuint(entry.LoudestWeight));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(32, 4), math.asuint(entry.SortTimeMs));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(36, 4), math.asuint(entry.AverageRt60Seconds));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(40, 4), math.asuint(entry.AverageLowPassHertz));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), math.asuint(entry.MaximumDelaySeconds));
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(48, 4), math.asuint(entry.AcousticOcclusionTimeMs));
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

#if UNITY_EDITOR
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
                Hecton8.Core.H8Debug.LogException(exception, this);
#endif
            }
        }
#endif

        private void DispatchQueuedAudioEvent(in CoreAudioEvent audioEvent)
        {
            if (!TryResolveAudioEventClip(audioEvent.EventID, audioEvent.ClipHash, out AudioClip clip))
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
            if (!IsInitialized)
                return;

            if (clip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayAtPointLowPassNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Hecton8.Core.H8Debug.LogWarning("[SpatialAudioManager] PlayAtPointWithLowPass called with null clip.");
#endif
                return;
            }

            if (_pool == null || _poolSize <= 0)
                return;

            bool hasListener = TryResolveListenerFrame(
                out Transform listener,
                out Vector3 listenerRuntimePosition,
                out Vector3 listenerAupRuntimePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup))
                return;

            Vector3 audiblePosition = position;
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
            TouchPlaybackClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup));
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
            source.priority = ResolveUnityAudioPriority(clip, source.outputAudioMixerGroup, clampedVolume, lodTier);
            float clampedDopplerRatio = math.clamp(
                1f,
                VirtualVoiceUtility.MinimumDopplerRatio,
                VirtualVoiceUtility.MaximumDopplerRatio);
            if (_smoothedDopplerRatios != null && index < _smoothedDopplerRatios.Length)
                _smoothedDopplerRatios[index] = clampedDopplerRatio;
            source.pitch = ResolveSourcePitch(index, clampedDopplerRatio);
            _audioLodTiers[index] = lodTier;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            UpdateWorldSourceAudioLod(
                index,
                source,
                audiblePosition,
                in audibleAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
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
            if (!IsInitialized)
                return false;

            float amplitude = math.saturate(intensity01);
            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return false;

            if (!TryQueueImpactRadarEmitter(runtimePosition, amplitude, amplitude))
                return false;

            NoiseSystem.ReportActiveSonarPing(runtimePosition, amplitude);
            return true;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PUBLIC API Гўв‚¬вЂќ 2D STATIC AUDIO (SUIT / HELMET / HUD)
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВѕГђВёГђВіГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљ 2D ГђВ·ГђВІГ‘Ж’ГђВє ГђВ±ГђВµГђВ· ГђВїГ‘в‚¬ГђВѕГ‘ВЃГ‘вЂљГ‘в‚¬ГђВ°ГђВЅГ‘ВЃГ‘вЂљГђВІГђВµГђВЅГђВЅГђВѕГђВіГђВѕ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВѕГђВЅГђВёГ‘в‚¬ГђВѕГђВІГђВ°ГђВЅГђВёГ‘ВЏ.
        /// ГђвЂќГђВ»Г‘ВЏ ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ ГђВІГђВЅГ‘Ж’Г‘вЂљГ‘в‚¬ГђВё Г‘Л†ГђВ»ГђВµГђВјГђВ°: HUD beeps, suit warnings, radio static,
        /// breath sounds, system alerts.
        ///
        /// ГђЛњГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·Г‘Ж’ГђВµГ‘вЂљ ГђВїГ‘Ж’ГђВ» 2D-ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВѕГђВІ Гўв‚¬вЂќ ГђВЅГђВµГ‘ВЃГђВєГђВѕГђВ»Г‘Е’ГђВєГђВѕ ГђВєГђВѕГ‘в‚¬ГђВѕГ‘вЂљГђВєГђВёГ‘вЂ¦ Г‘ВЃГђВёГђВіГђВЅГђВ°ГђВ»ГђВѕГђВІ ГђВјГђВѕГђВіГ‘Ж’Г‘вЂљ ГђВёГђВіГ‘в‚¬ГђВ°Г‘вЂљГ‘Е’
        /// ГђВїГђВ°Г‘в‚¬ГђВ°ГђВ»ГђВ»ГђВµГђВ»Г‘Е’ГђВЅГђВѕ ГђВґГђВѕ ГђВёГ‘ВЃГ‘вЂЎГђВµГ‘в‚¬ГђВїГђВ°ГђВЅГђВёГ‘ВЏ ГђВїГ‘Ж’ГђВ»ГђВ°; ГђВґГђВ°ГђВ»Г‘Е’Г‘Л†ГђВµ Гўв‚¬вЂќ ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГђВµ ГђВїГђВѕ ГђВІГ‘в‚¬ГђВµГђВјГђВµГђВЅГђВё.
        ///
        /// ГђвЂ™Г‘вЂ№ГђВ·ГђВѕГђВІ: Hecton8.Core.GlobalRegistry.Audio.PlayStatic2D(beepClip, 0.5f);
        /// </summary>
        /// <param name="clip">AudioClip. Null-safe.</param>
        /// <param name="volume">ГђвЂњГ‘в‚¬ГђВѕГђВјГђВєГђВѕГ‘ВЃГ‘вЂљГ‘Е’ [0..1]. Default = 1.</param>
        public void PlayStatic2D(AudioClip clip, float volume = 1f)
        {
            PlayStatic2D(clip, volume, _interfaceGroup);
        }

        /// <summary>
        /// ГђЕёГ‘в‚¬ГђВѕГђВёГђВіГ‘в‚¬Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљ 2D ГђВ·ГђВІГ‘Ж’ГђВє Г‘ВЃ Г‘ВЏГђВІГђВЅГђВѕГђВ№ AudioMixerGroup.
        /// </summary>
        public void PlayStatic2D(AudioClip clip, float volume, AudioMixerGroup mixerGroup)
        {
            if (!IsInitialized)
                return;

            if (clip == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (ShouldEmitEditorThrottledLog(ref _nextPlayStatic2DNullClipLogTime, NullClipEditorLogIntervalSeconds))
                    Hecton8.Core.H8Debug.LogWarning("[SpatialAudioManager] PlayStatic2D called with null clip.");
#endif
                return;
            }

            if (_pool2D == null || _pool2DSize <= 0)
                return;

            int index = Acquire2DSourceIndex();
            if (index < 0)
                return;

            AudioSource source = _pool2D[index];

            TouchPlaybackClip(clip, AudioResidencyDomain.Interface);
            source.clip = clip;
            source.volume = math.saturate(volume);
            source.pitch = _brownoutAudioPitchRatio;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = ResolveUiMixerGroup(clip, mixerGroup);

            source.Play();
            _startTimes2D[index] = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
        }

        public void TouchClip(AudioClip clip, byte residencyDomain, bool decodeNow)
        {
            if (!IsInitialized)
                return;

            if (!TryResolveAudioResidencyDomain(residencyDomain, out AudioResidencyDomain domain))
                return;

            AudioResidencyCache.TouchClip(clip, domain, decodeNow);
        }

        private static void TouchPlaybackClip(AudioClip clip, AudioResidencyDomain domain)
        {
            AudioResidencyCache.TouchClip(clip, domain, false);
        }

        public void PrewarmAudioSource(AudioSource source, byte residencyDomain)
        {
            if (!IsInitialized)
                return;

            if (!TryResolveAudioResidencyDomain(residencyDomain, out AudioResidencyDomain domain))
                return;

            AudioResidencyCache.PrewarmAudioSource(source, domain);
        }

        public void ReleaseAudioSource(AudioSource source)
        {
            if (!IsInitialized)
                return;

            AudioResidencyCache.ReleaseAudioSource(source);
        }

        public void ReleaseClip(AudioClip clip)
        {
            if (!IsInitialized)
                return;

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
            if (!IsInitialized)
                return false;

            bool hasEncryptedVoiceRoute = _encryptedVoiceGroup != null;
            PlayStatic2D(clip, volume, hasEncryptedVoiceRoute ? _encryptedVoiceGroup : _interfaceGroup);
            return clip != null && hasEncryptedVoiceRoute;
        }

        public void SetNarrativeRadioInterference(float interference01)
        {
            if (!IsInitialized)
                return;

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

        public void SetNarrativeRadioGlitch(float corruption01, float bitCrushMix01, float pitchShiftCents, float qualityWeight01)
        {
            if (!IsInitialized)
                return;

            if (!_hasNarrativeRadioBitCrushMixParameter && !_hasNarrativeRadioPitchShiftCentsParameter)
                return;

            AudioMixer mixer = ResolveNarrativeRadioMixer();
            if (mixer == null)
                return;

            float quality = SmoothQuality01(SanitizeQuality01(qualityWeight01));
            float corruption = math.saturate(SanitizeFinite(corruption01, 0f));
            float presentationWeight = math.saturate(quality * math.smoothstep(0.18f, 1f, quality));
            float bitCrushMix = math.saturate(SanitizeFinite(bitCrushMix01, 0f)) * presentationWeight;
            float pitchCents = math.clamp(
                SanitizeFinite(pitchShiftCents, 0f) * presentationWeight,
                -1200f,
                1200f);

            if (corruption <= 0.0001f || quality <= 0.0001f)
            {
                bitCrushMix = 0f;
                pitchCents = 0f;
            }

            if (_hasNarrativeRadioBitCrushMixParameter &&
                math.abs(bitCrushMix - _lastNarrativeRadioBitCrushMix01) > 0.005f)
            {
                if (!mixer.SetFloat(_narrativeRadioBitCrushMixParameter, bitCrushMix))
                    _hasNarrativeRadioBitCrushMixParameter = false;
                else
                    _lastNarrativeRadioBitCrushMix01 = bitCrushMix;
            }

            if (_hasNarrativeRadioPitchShiftCentsParameter &&
                math.abs(pitchCents - _lastNarrativeRadioPitchShiftCents) > 0.5f)
            {
                if (!mixer.SetFloat(_narrativeRadioPitchShiftCentsParameter, pitchCents))
                    _hasNarrativeRadioPitchShiftCentsParameter = false;
                else
                    _lastNarrativeRadioPitchShiftCents = pitchCents;
            }
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PUBLIC API Гўв‚¬вЂќ MIXER GROUP ACCESSORS
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>Mixer group ГђВґГђВ»Г‘ВЏ SFX (Г‘ВЃГ‘Ж’Г‘вЂ°ГђВµГ‘ВЃГ‘вЂљГђВІГђВ°, ГђВјГђВµГ‘вЂ¦ГђВ°ГђВЅГђВёГђВ·ГђВјГ‘вЂ№, ГђВѕГђВєГ‘в‚¬Г‘Ж’ГђВ¶ГђВµГђВЅГђВёГђВµ).</summary>
        public AudioMixerGroup SfxGroup => IsInitialized ? _sfxGroup : null;

        /// <summary>Mixer group ГђВґГђВ»Г‘ВЏ ГђВёГђВЅГ‘вЂљГђВµГ‘в‚¬Г‘вЂћГђВµГђВ№Г‘ВЃГђВ° ГђВё ГђВ·ГђВІГ‘Ж’ГђВєГђВѕГђВІ Г‘Л†ГђВ»ГђВµГђВјГђВ°.</summary>
        public AudioMixerGroup InterfaceGroup => IsInitialized ? _interfaceGroup : null;

        public bool HasEncryptedVoiceBitCrushRoute => IsInitialized && _encryptedVoiceGroup != null;

        public AudioMixerGroup EncryptedVoiceGroup => IsInitialized
            ? _encryptedVoiceGroup != null ? _encryptedVoiceGroup : _interfaceGroup
            : null;

        /// <summary>Mixer group for resolved ambient-bed playback.</summary>
        public AudioMixerGroup AmbientGroup => IsInitialized ? ResolvedBedBusGroup : null;

        /// <summary>Mixer group for dominant hostile cues.</summary>
        public AudioMixerGroup ThreatGroup => IsInitialized ? ResolvedThreatBusGroup : null;

        /// <summary>Mixer group for ambient bed layers.</summary>
        public AudioMixerGroup BedGroup => IsInitialized ? ResolvedBedBusGroup : null;

        /// <summary>Current 360-bin acoustic radar intensity ring for HUD consumers. Treat as read-only and reacquire each tick.</summary>
        public NativeArray<float>.ReadOnly AcousticRadarIntensityBins =>
            IsInitialized && TryReadAcousticRadarIntensityBins(out NativeArray<float>.ReadOnly radialIntensityBins) ? radialIntensityBins : default;

        /// <summary>Current acoustic radar angular resolution in bins.</summary>
        public int AcousticRadarResolution => IsInitialized ? AcousticRadarBinCount : 0;

        /// <summary>Vault-backed 8x4 acoustic radar energy grid view for HUD sonar distortion overlays.</summary>
        public NativeArray<float>.ReadOnly AcousticRadarEnergyGrid =>
            IsInitialized && TryReadAcousticRadarGrid(out NativeArray<float>.ReadOnly gridEnergy) ? gridEnergy : default;

        /// <summary>GPU upload buffer for the 8x4 acoustic radar energy grid.</summary>
        public GraphicsBuffer AcousticRadarEnergyGridBuffer => IsInitialized ? _activeAcousticRadarGridBuffer : null;

        /// <summary>Returns the persistent 360-degree acoustic radar ring for HUD/visor consumers.</summary>
        public bool TryGetAcousticRadarPayload(out NativeArray<float>.ReadOnly radialIntensityBins, out int radialResolution)
        {
            radialIntensityBins = default;
            radialResolution = AcousticRadarBinCount;
            if (!IsInitialized ||
                radialResolution <= 0 ||
                !TryReadAcousticRadarIntensityBins(out radialIntensityBins))
                return false;

            return true;
        }

        /// <summary>Uploads the persistent 360-degree acoustic radar ring into a caller-owned texture.</summary>
        public bool TryUploadAcousticRadarPayload(Texture2D destination, out int uploadedSampleCount, out float peakIntensity)
        {
            uploadedSampleCount = 0;
            peakIntensity = 0f;

            if (!IsInitialized ||
                destination == null ||
                _acousticRadarIntensityUploadScratch == null ||
                !TryReadAcousticRadarIntensityBins(out NativeArray<float>.ReadOnly radialIntensityBins))
                return false;

            int sampleCount = math.min(radialIntensityBins.Length, _acousticRadarIntensityUploadScratch.Length);
            sampleCount = math.min(sampleCount, AcousticRadarBinCount);
            if (sampleCount <= 0)
                return false;

            float peak = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float sample = radialIntensityBins[i];
                _acousticRadarIntensityUploadScratch[i] = sample;
                if (sample > peak)
                    peak = sample;
            }

            destination.SetPixelData(_acousticRadarIntensityUploadScratch, 0);

            uploadedSampleCount = sampleCount;
            peakIntensity = math.saturate(peak);
            return true;
        }

        /// <summary>Returns the vault-backed 8x4 acoustic radar grid view and its GPU upload buffer.</summary>
        public bool TryGetAcousticRadarGridPayload(
            out NativeArray<float>.ReadOnly gridEnergy,
            out int azimuthBins,
            out int elevationBins,
            out GraphicsBuffer gridBuffer)
        {
            azimuthBins = AcousticRadarGridAzimuthBins;
            elevationBins = AcousticRadarGridElevationBins;
            if (!IsInitialized)
            {
                gridEnergy = default;
                gridBuffer = null;
                return false;
            }

            gridEnergy = TryReadAcousticRadarGrid(out NativeArray<float>.ReadOnly grid) ? grid : default;
            gridBuffer = _activeAcousticRadarGridBuffer;
            return gridEnergy.IsCreated && gridBuffer != null;
        }

        internal bool TryGetDominantBinauralEmitter(out BinauralEmitterTelemetry telemetry)
        {
            if (!IsInitialized)
            {
                telemetry = default;
                return false;
            }

            telemetry = _dominantBinauralEmitter;
            return telemetry.Valid != 0;
        }

        bool ISpatialAudioBinauralEmitterReadModel.TryGetDominantBinauralEmitter(out SpatialAudioBinauralEmitterTelemetry telemetry)
        {
            if (!IsInitialized)
            {
                telemetry = default;
                return false;
            }

            BinauralEmitterTelemetry source = _dominantBinauralEmitter;
            telemetry = new SpatialAudioBinauralEmitterTelemetry
            {
                Position = source.Position,
                DistanceMeters = source.DistanceMeters,
                AzimuthRadians = source.AzimuthRadians,
                RightDot = source.RightDot,
                ItdSeconds = source.ItdSeconds,
                ShadowAmount01 = source.ShadowAmount01,
                ShadowCutoffHertz = source.ShadowCutoffHertz,
                Energy = source.Energy,
                WaterDensityMul = source.WaterDensityMul,
                Valid = source.Valid
            };
            return source.Valid != 0;
        }

        public int CopyActiveWorldEmitterSamples(SpatialAudioActiveEmitterSample[] destination)
        {
            if (!IsInitialized || destination == null || destination.Length == 0 || _pool == null)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            for (int activeSlot = 0; activeSlot < _activeWorldCount && count < limit; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                if (source == null || !source.isPlaying || source.clip == null)
                    continue;

                if (!TryGetCachedActiveWorldRuntimePosition(sourceIndex, out Vector3 sourcePosition))
                    continue;

                destination[count] = new SpatialAudioActiveEmitterSample
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

                destination[count] = new SpatialAudioActiveEmitterSample
                {
                    PositionAup = emitter.PositionAup,
                    Position = emitter.Position,
                    Amplitude = amplitude
                };
                count++;
            }

            return count;
        }

        public int CopyActiveImpactEmitterSamples(SpatialAudioImpactEmitterSample[] destination)
        {
            if (!IsInitialized || destination == null || destination.Length == 0)
                return 0;

            int count = 0;
            int limit = destination.Length;
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            for (int i = 0; i < _impactEmitters.Length && count < limit; i++)
            {
                ImpactEmitterSample emitter = _impactEmitters[i];
                float amplitude = ResolveImpactEmitterAmplitude(emitter, now);
                if (!(amplitude > ImpactEmitterMinimumAmplitude))
                    continue;

                destination[count] = new SpatialAudioImpactEmitterSample
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

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  PUBLIC API Гўв‚¬вЂќ UTILITY
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђЕѕГ‘ВЃГ‘вЂљГђВ°ГђВЅГђВ°ГђВІГђВ»ГђВёГђВІГђВ°ГђВµГ‘вЂљ ГђВІГ‘ВЃГђВµ ГђВ·ГђВІГ‘Ж’ГђВєГђВё ГђВІ ГђВїГ‘Ж’ГђВ»ГђВµ. ГђВђГђВІГђВ°Г‘в‚¬ГђВёГђВ№ГђВЅГ‘вЂ№ГђВ№ ГђВјГђВµГ‘вЂљГђВѕГђВґ.
        /// ГђЕёГђВѕГђВ»ГђВµГђВ·ГђВµГђВЅ ГђВїГ‘в‚¬ГђВё Г‘ВЃГђВјГђВµГђВЅГђВµ Г‘ВЃГ‘вЂ ГђВµГђВЅГ‘вЂ№, ГђВїГђВ°Г‘Ж’ГђВ·ГђВµ, ГђВёГђВ»ГђВё Г‘вЂћГђВ°Г‘вЂљГђВ°ГђВ»Г‘Е’ГђВЅГђВѕГђВј Г‘ВЃГђВѕГђВ±Г‘вЂ№Г‘вЂљГђВёГђВё.
        /// </summary>
        public void StopAll()
        {
            if (!IsInitialized)
                return;

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
        /// ГђвЂ™ГђВѕГђВ·ГђВІГ‘в‚¬ГђВ°Г‘вЂ°ГђВ°ГђВµГ‘вЂљ ГђВєГђВѕГђВ»ГђВёГ‘вЂЎГђВµГ‘ВЃГ‘вЂљГђВІГђВѕ ГђВ°ГђВєГ‘вЂљГђВёГђВІГђВЅГђВѕ ГђВёГђВіГ‘в‚¬ГђВ°Г‘ЕЅГ‘вЂ°ГђВёГ‘вЂ¦ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВѕГђВІ ГђВІ ГђВїГ‘Ж’ГђВ»ГђВµ.
        /// ГђВўГђВѕГђВ»Г‘Е’ГђВєГђВѕ ГђВґГђВ»Г‘ВЏ debug / profiling. ГђВќГђВµ ГђВІГ‘вЂ№ГђВ·Г‘вЂ№ГђВІГђВ°Г‘вЂљГ‘Е’ ГђВІ hot path.
        /// </summary>
        public int ActiveSourceCount
        {
            get
            {
                return IsInitialized ? _activeWorldCount : 0;
            }
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  POOL MANAGEMENT Гўв‚¬вЂќ PRIVATE
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

        /// <summary>
        /// ГђВќГђВ°Г‘вЂ¦ГђВѕГђВґГђВёГ‘вЂљ ГђВёГђВЅГђВґГђВµГђВєГ‘ВЃ Г‘ВЃГђВІГђВѕГђВ±ГђВѕГђВґГђВЅГђВѕГђВіГђВѕ AudioSource ГђВІ ГђВїГ‘Ж’ГђВ»ГђВµ.
        /// ГђвЂўГ‘ВЃГђВ»ГђВё ГђВІГ‘ВЃГђВµ ГђВ·ГђВ°ГђВЅГ‘ВЏГ‘вЂљГ‘вЂ№ Гўв‚¬вЂќ ГђВІГђВѕГђВ·ГђВІГ‘в‚¬ГђВ°Г‘вЂ°ГђВ°ГђВµГ‘вЂљ ГђВёГђВЅГђВґГђВµГђВєГ‘ВЃ Г‘ВЃГђВ°ГђВјГђВѕГђВіГђВѕ Г‘ВЃГ‘вЂљГђВ°Г‘в‚¬ГђВѕГђВіГђВѕ (ГђВІГ‘вЂ№Г‘вЂљГђВµГ‘ВЃГђВЅГђВµГђВЅГђВёГђВµ).
        ///
        /// ГђВђГђВ»ГђВіГђВѕГ‘в‚¬ГђВёГ‘вЂљГђВј:
        ///   1. ГђвЂєГђВёГђВЅГђВµГђВ№ГђВЅГ‘вЂ№ГђВ№ ГђВїГ‘в‚¬ГђВѕГ‘вЂ¦ГђВѕГђВґ ГђВїГђВѕ ГђВјГђВ°Г‘ВЃГ‘ВЃГђВёГђВІГ‘Ж’ Гўв‚¬вЂќ ГђВёГ‘вЂ°ГђВµГђВј ГђВїГђВµГ‘в‚¬ГђВІГ‘вЂ№ГђВ№ !isPlaying.
        ///   2. Track quietest active source; startTime is only a tie-breaker.
        ///   3. ГђЕѕГђВґГђВёГђВЅ ГђВїГ‘в‚¬ГђВѕГ‘вЂ¦ГђВѕГђВґ Гўв‚¬вЂќ O(n), n ГўвЂ°В¤ 32. Zero-GC.
        ///
        /// Cost: ~0.001ms ГђВґГђВ»Г‘ВЏ ГђВїГ‘Ж’ГђВ»ГђВ° ГђВёГђВ· 16 Г‘ВЌГђВ»ГђВµГђВјГђВµГђВЅГ‘вЂљГђВѕГђВІ.
        /// </summary>
        /// <returns>ГђЛњГђВЅГђВґГђВµГђВєГ‘ВЃ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВ° ГђВґГђВ»Г‘ВЏ ГђВёГ‘ВЃГђВїГђВѕГђВ»Г‘Е’ГђВ·ГђВѕГђВІГђВ°ГђВЅГђВёГ‘ВЏ.</returns>
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
            int lowestImportancePriority = int.MinValue;
            float quietestVolume = float.MaxValue;
            float oldestTime = float.MaxValue;
            for (int activeSlot = 0; activeSlot < _activeWorldCount; activeSlot++)
            {
                int sourceIndex = _activeWorldIndices[activeSlot];
                AudioSource source = _pool[sourceIndex];
                int candidatePriority = source != null ? source.priority : 256;
                float candidateVolume = source != null ? math.max(0f, source.volume) : 0f;
                float candidateStartTime = _startTimes[sourceIndex];
                if (candidatePriority > lowestImportancePriority ||
                    (candidatePriority == lowestImportancePriority && candidateVolume < quietestVolume) ||
                    (candidatePriority == lowestImportancePriority && candidateVolume <= quietestVolume && candidateStartTime < oldestTime))
                {
                    lowestImportancePriority = candidatePriority;
                    quietestVolume = candidateVolume;
                    oldestTime = candidateStartTime;
                    quietestIndex = sourceIndex;
                }
            }

            AudioSource evictedSource = _pool[quietestIndex];
            if (evictedSource != null)
                evictedSource.Stop();
            ResetWorldSourceState(quietestIndex, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ShouldEmitEditorThrottledLog(ref _nextWorldPoolFullEditorLogTime, PoolFullEditorLogIntervalSeconds))
            {
                Hecton8.Core.H8Debug.Log("[SpatialAudioManager] World pool full. Evicting lowest-priority source.", this);
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
                out Vector3 listenerAupRuntimePosition,
                out AbsoluteUniversePosition listenerAup);
            ResolveListenerBasis(listener, out float3 listenerRight, out _, out float3 listenerForward);
            float3 listenerAcousticForward = listenerForward;
            if (!TryResolveSourceAupFrame(position, out AbsoluteUniversePosition sourceAup))
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
            TouchPlaybackClip(clip, ResolveWorldResidencyDomain(clip, mixerGroup));
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
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int currentFrame = SystemDispatcher.CurrentFrameIndex;
            UpdateWorldSourceAudioLod(
                index,
                source,
                position,
                in sourceAup,
                listener,
                in listenerAup,
                listenerRuntimePosition,
                listenerRight,
                listenerAcousticForward,
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
            if (_runtimeOwnerAborted || _registeredUpdatable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredUpdatable = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterFastTickable()
        {
            if (_runtimeOwnerAborted || _registeredFastTickable || !Application.isPlaying)
                return;

            _registeredFastTickable = GlobalRegistry.TryRegisterFastTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterSlowTickable()
        {
            if (_runtimeOwnerAborted || _registeredSlowTickable || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registeredSlowTickable = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterLateFrameTickable()
        {
            if (_runtimeOwnerAborted || _registeredLateFrameTickable || !Application.isPlaying)
                return;

            _registeredLateFrameTickable = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryRegisterDispatcherLanes()
        {
            TryRegisterUpdatable();
            TryRegisterFastTickable();
            TryRegisterSlowTickable();
            TryRegisterLateFrameTickable();
        }

        private void TryUnregisterDispatcherLanes()
        {
            if (_registeredUpdatable)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdatable = false;
            }

            if (_registeredFastTickable)
            {
                GlobalRegistry.UnregisterFastTickable(this, PriorityLayer.Environment);
                _registeredFastTickable = false;
            }

            if (_registeredSlowTickable)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTickable = false;
            }

            if (_registeredLateFrameTickable)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTickable = false;
            }
        }

        private void TryRegisterOriginShiftListener()
        {
            if (_runtimeOwnerAborted || _registeredOriginShiftListener || !Application.isPlaying)
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
            if (!IsInitialized)
                return;

            // Mirrors impact positions for passive radar/UI consumers only.
            // Audible impact energy is synthesized through PlayerCriticalProceduralAudioRenderer.
            float amplitude = math.saturate(impactSignal.Intensity * ImpactEmitterAmplitudeScale);
            if (PhysicsImpactSignal.IsHeavy(in impactSignal))
                amplitude = math.max(amplitude, 0.45f);

            AbsoluteUniversePosition impactAup = AbsoluteUniversePosition.FromAbsolutePosition(impactSignal.ResolvePointAupMeters());
            TryQueueImpactRadarEmitter(
                impactSignal.Point,
                in impactAup,
                amplitude,
                math.saturate(impactSignal.Intensity));
        }

        private void ConsumeAcousticImpulseSignals()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastAcousticImpulseSignalFrame == frame)
                return;

            _lastAcousticImpulseSignalFrame = frame;
            ReadOnlySpan<PhysicsEventPayload> signals = SignalBus<PhysicsEventPayload>.GetFrameSnapshot();
            for (int i = 0; i < signals.Length; i++)
            {
                PhysicsEventPayload payload = signals[i];
                if (payload.EventType != (ushort)PhysicsEventType.AcousticImpulse)
                    continue;

                HandleAcousticImpulse(in payload);
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

        private void HandleAcousticImpulse(in PhysicsEventPayload impulseEvent)
        {
            float volume01 = math.saturate(impulseEvent.Scalar1);
            float amplitude = math.saturate(volume01 * ImpactEmitterAmplitudeScale);
            if ((impulseEvent.StatusBits & AcousticImpulseFlagLeviathan) != 0u)
                amplitude = math.max(amplitude, 0.5f);

            TryQueueImpactRadarEmitter(impulseEvent.RuntimePosition, amplitude, volume01);
        }

        private bool TryQueueImpactRadarEmitter(Vector3 position, float amplitude, float lifetime01)
        {
            if (!IsInitialized)
                return false;

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
            if (!IsInitialized)
                return false;

            if (!(amplitude > ImpactEmitterMinimumAmplitude))
                return false;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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
            if (!IsInitialized)
                return;

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
                EventTimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
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
            if (!IsInitialized)
                return;

            HandleFatalPressureImplosion(in implosionEvent);
        }

        /// <summary>
        /// Queues the muffled backpack chemistry explosion through the same delayed underwater event bus.
        /// </summary>
        public void QueueInventoryRunawayExplosion(Vector3 runtimePosition, float volume01)
        {
            if (!IsInitialized)
                return;

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
                EventTimeSeconds = (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
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
            if (!IsAudioRuntimeReady || _delayedAudioIngress == null || _pendingDelayedAudioEvents == null)
                return;

            if (_pendingDelayedAudioEventCount + _delayedAudioIngressCount >= MaxDelayedAudioEvents)
                return;

            int writeIndex = (_delayedAudioIngressHead + _delayedAudioIngressCount) % _delayedAudioIngress.Length;
            _delayedAudioIngress[writeIndex] = delayedEvent;
            _delayedAudioIngressCount++;
        }

        private void DrainDelayedAudioIngress()
        {
            if (_delayedAudioIngress == null ||
                _pendingDelayedAudioEvents == null ||
                _delayedAudioIngressCount <= 0)
                return;

            while (_delayedAudioIngressCount > 0 && _pendingDelayedAudioEventCount < _pendingDelayedAudioEvents.Length)
            {
                int readIndex = _delayedAudioIngressHead;
                DelayedAudioEvent delayedEvent = _delayedAudioIngress[readIndex];
                _delayedAudioIngress[readIndex] = default;
                _delayedAudioIngressHead = (_delayedAudioIngressHead + 1) % _delayedAudioIngress.Length;
                _delayedAudioIngressCount--;
                _pendingDelayedAudioEvents[_pendingDelayedAudioEventCount++] = delayedEvent;
            }
        }

        private void ProcessDelayedAudioEvents(bool hasListener, in AbsoluteUniversePosition listenerAup)
        {
            if (_pendingDelayedAudioEvents == null || _pendingDelayedAudioEventCount == 0)
                return;

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            int writeIndex = 0;
            for (int i = 0; i < _pendingDelayedAudioEventCount; i++)
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

            for (int i = writeIndex; i < _pendingDelayedAudioEventCount; i++)
                _pendingDelayedAudioEvents[i] = default;
            _pendingDelayedAudioEventCount = writeIndex;
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

            uint aupHash = ResolveAcousticEchoSourceId(0, in delayedEvent.Aup);
            float phase = ((float)SystemDispatcher.CurrentUnscaledTimeSeconds * 47.3f) +
                          ((aupHash & 1023u) * 0.006135923f);
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
            if (!IsInitialized)
                return;

            if (acousticEvent.Clip == null ||
                !IsFinite(acousticEvent.Position))
                return;

            float volume = math.saturate(SanitizeFinite(acousticEvent.Volume, 0f));
            if (volume <= 0f)
                return;

            float pitch = math.clamp(SanitizeFinite(acousticEvent.Pitch, 1f), 0.1f, 3f);
            PlayAtPoint(
                acousticEvent.Clip,
                acousticEvent.Position,
                volume,
                pitch,
                ResolvedDefaultWorldMixerGroup);
        }

        /// <summary>
        /// Receives deferred repair-drone torch acoustic pulses from the construction event lane.
        /// </summary>
        public void OnRepairDroneTorchAcoustic(in RepairDroneTorchAcousticEvent acousticEvent)
        {
            if (!IsInitialized)
                return;

            HandleRepairDroneTorchAcoustic(in acousticEvent);
        }

        private void ApplyDelayedTrauma(in DelayedAudioEvent delayedEvent, in AbsoluteUniversePosition listenerAup)
        {
            IPlayerMovementTraumaSink traumaSink = _listenerPlayerMovementTrauma;
            if (traumaSink == null)
                return;

            float3 listenerOffsetAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in listenerAup, in delayedEvent.Aup);
            Vector3 listenerOffset = new Vector3(listenerOffsetAup.x, listenerOffsetAup.y, listenerOffsetAup.z);
            float distanceSq = math.lengthsq(listenerOffsetAup);
            float traumaRange = math.max(delayedEvent.TraumaRangeMeters, 0.0001f);
            float traumaRangeSq = traumaRange * traumaRange;
            if (distanceSq > traumaRangeSq)
                return;

            // distanceSq is already the exact squared length from math.lengthsq above, so rsqrt
            // yields the exact inverse length in one instruction - cheaper than the octagonal
            // approximation it replaces. That approximation undershoots by 13.4% on a body diagonal,
            // leaving this "normalised" direction at length 1.155, which made the delivered impulse
            // depend on the blast's bearing rather than on its distance.
            float invDistance = math.rsqrt(math.max(distanceSq, 1e-12f));
            Vector3 traumaDirection = distanceSq > 0.000001f
                ? listenerOffset * invDistance
                : Vector3.up;
            float distance01 = math.saturate(distanceSq * math.rcp(traumaRangeSq));
            float trauma01 = 1f - distance01 * distance01;
            traumaSink.ApplyPhysicalTrauma(
                traumaDirection * (delayedEvent.TraumaImpulse * trauma01),
                delayedEvent.TraumaWeight * trauma01);
        }

        private void ClearDelayedAudioEvents()
        {
            if (_delayedAudioIngress != null)
                Array.Clear(_delayedAudioIngress, 0, _delayedAudioIngress.Length);
            _delayedAudioIngressCount = 0;
            _delayedAudioIngressHead = 0;

            if (_pendingDelayedAudioEvents != null)
                Array.Clear(_pendingDelayedAudioEvents, 0, _pendingDelayedAudioEvents.Length);
            _pendingDelayedAudioEventCount = 0;
        }

        private void ClearAudioEventQueue()
        {
            if (_audioEventQueue != null)
                Array.Clear(_audioEventQueue, 0, _audioEventQueue.Length);
            _audioEventQueueCount = 0;
            _audioEventQueueHead = 0;
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
            if (playerContext == null)
            {
                _listenerTransform = null;
                return null;
            }

            Camera playerCamera = playerContext.PlayerCamera;
            if (playerCamera != null)
            {
                _listenerTransform = playerCamera.transform;
                return _listenerTransform;
            }

            _listenerTransform = null;
            return null;
        }

        private void ResolveListenerTransformCold()
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerRuntimeContext;
            if (playerContext == null || !playerContext.IsInitialized)
            {
                _listenerTransform = null;
                return;
            }

            Camera playerCamera = playerContext.PlayerCamera;
            if (playerCamera != null)
            {
                _listenerTransform = playerCamera.transform;
                return;
            }

            GameObject playerObject = playerContext.PlayerObject;
            if (playerObject == null)
            {
                _listenerTransform = null;
                return;
            }

            if (playerObject.TryGetComponent(out AudioListener playerListener))
            {
                _listenerTransform = playerListener.transform;
                return;
            }

            AudioListener ownedPlayerListener =
                ComponentReferenceUtility.ResolveOwnedComponent<AudioListener>(playerObject.transform);
            _listenerTransform = ownedPlayerListener != null ? ownedPlayerListener.transform : null;
        }

        private bool TryResolveListenerFrame(
            out Transform listener,
            out Vector3 listenerRuntimePosition,
            out Vector3 listenerAupRuntimePosition,
            out AbsoluteUniversePosition listenerAup)
        {
            listener = ResolveListenerTransform();
            if (listener == null)
            {
                listenerRuntimePosition = default;
                listenerAupRuntimePosition = default;
                listenerAup = default;
                return false;
            }

            listenerRuntimePosition = listener.position;
            if (!IsFinite(listenerRuntimePosition))
            {
                listenerAupRuntimePosition = default;
                listenerAup = default;
                return false;
            }

            if (TryResolvePlayerListenerAup(listener, listenerRuntimePosition, out listenerAup))
            {
                listenerAupRuntimePosition = ToRuntimeVector3(in listenerAup);
                return true;
            }

            if (!TryResolveAupFromRuntimeOrigin(listenerRuntimePosition, out listenerAup))
            {
                listenerAupRuntimePosition = default;
                return false;
            }

            listenerAupRuntimePosition = ToRuntimeVector3(in listenerAup);
            return true;
        }

        private static bool TryResolveSourceAupFrame(
            Vector3 runtimePosition,
            out AbsoluteUniversePosition sourceAup)
        {
            if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out sourceAup))
            {
                return false;
            }

            return true;
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

            float3 relative = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup);
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

                if (playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                    (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u)
                {
                    AbsoluteUniversePosition currentAup = movementState.PredictedAup;
                    Vector3 rootRuntimePosition = currentAup.ToRuntimeFloat3();
                    if (IsFinite(rootRuntimePosition))
                    {
                        listenerAup = OffsetAupLocal(in currentAup, listenerRuntimePosition - rootRuntimePosition);
                        return true;
                    }
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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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

        private void ResolveListenerBasis(
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

            IPlayerRuntimeContext runtimeContext = _cachedPlayerRuntimeContext;
            if (runtimeContext != null &&
                runtimeContext.IsInitialized &&
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

        private static bool TryResolveRuntimeContextForward(IPlayerRuntimeContext runtimeContext, out float3 listenerForward)
        {
            listenerForward = default;
            if (runtimeContext.TryGetLookRuntimeState(out PlayerLookState lookState))
            {
                float3 lookForward = lookState.AimForward;
                if (math.lengthsq(lookForward) > 0.0001f)
                {
                    listenerForward = ResolveDominantAxisDirection(lookForward);
                    return true;
                }
            }

            if (!runtimeContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState))
                return false;

            float3 cameraForward = movementState.CameraForward;
            if (math.lengthsq(cameraForward) > 0.0001f)
            {
                listenerForward = ResolveDominantAxisDirection(cameraForward);
                return true;
            }

            float3 movementForward = movementState.Forward;
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

            ResetPreviousVelocityAupSlot(sourceIndex);

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
            IAcousticZoneReadModel acousticZone = ResolveAcousticZone();
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
            in AbsoluteUniversePosition sourceAup,
            Transform listener,
            in AbsoluteUniversePosition listenerAup,
            Vector3 listenerRuntimePosition,
            float3 listenerRight,
            float3 listenerForward,
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
                SystemDispatcher.CurrentFrameIndex,
                (float)SystemDispatcher.CurrentUnscaledTimeSeconds,
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
            if (!ShouldUseAcousticPortalPath())
            {
                return false;
            }

            AcousticAup acousticSource = ToAcousticAup(in sourceAup);
            AcousticAup acousticListener = ToAcousticAup(in listenerAup);
            int cacheKey = ComputeAcousticPortalCacheKey(in acousticSource, in acousticListener, stationaryCacheKey);
            if (TryReadAcousticPortalCache(cacheKey, in acousticSource, in acousticListener, out result))
            {
                WriteAcousticPortalBlackBox(in result, SystemDispatcher.CurrentFrameIndex);
                return result.Status == AcousticPathStatus.PathFound && result.UsedPortalPath != 0;
            }

            if (!TryAcquireAcousticPortalWorkBuffers(
                    out NativeArray<AcousticPortalNode> acousticPortalNodes,
                    out NativeArray<AcousticPortalEdge> acousticPortalEdges,
                    out NativeArray<AcousticPathResult> acousticPortalResult,
                    out NativeArray<float> acousticPortalCosts,
                    out NativeArray<int> acousticPortalCameFrom,
                    out NativeArray<byte> acousticPortalStates))
            {
                return false;
            }

            try
            {
                if (!TryBuildAcousticPortalGraph(
                        sourceRuntimePosition,
                        listenerRuntimePosition,
                        in sourceAup,
                        in listenerAup,
                        acousticPortalNodes,
                        acousticPortalEdges,
                        out int nodeCount,
                        out int edgeCount))
                {
                    return false;
                }

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

                if (!TryAcquireAcousticPortalScratchSets(
                        out NativeArray<int> acousticPortalOpenSet,
                        out NativeArray<int> acousticPortalClosedSet))
                {
                    return false;
                }

                long start = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    AcousticPathJob pathJob = new AcousticPathJob
                    {
                        Nodes = acousticPortalNodes,
                        Edges = acousticPortalEdges,
                        OpenSet = acousticPortalOpenSet,
                        ClosedSet = acousticPortalClosedSet,
                        Costs = acousticPortalCosts,
                        CameFrom = acousticPortalCameFrom,
                        States = acousticPortalStates,
                        Result = acousticPortalResult,
                        Query = query
                    };
                    pathJob.Execute();
                }
                finally
                {
                    ReleaseAcousticPortalScratchSets();
                }

                result = acousticPortalResult[0];
                result.PathfindingMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                WriteAcousticPortalBlackBox(in result, SystemDispatcher.CurrentFrameIndex);
                if (result.Status == AcousticPathStatus.PathFound && result.UsedPortalPath != 0)
                {
                    WriteAcousticPortalCache(cacheKey, in acousticSource, in acousticListener, in result);
                    return true;
                }

                return false;
            }
            finally
            {
                ReleaseAcousticPortalWorkBuffers();
            }
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
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            NativeArray<AcousticPortalNode> acousticPortalNodes,
            NativeArray<AcousticPortalEdge> acousticPortalEdges,
            out int nodeCount,
            out int edgeCount)
        {
            if (TryBuildHabitatAcousticPortalGraph(
                    in sourceAup,
                    in listenerAup,
                    acousticPortalNodes,
                    acousticPortalEdges,
                    out nodeCount,
                    out edgeCount))
            {
                return true;
            }

            return TryBuildVoxelAcousticPortalGraph(
                sourceRuntimePosition,
                listenerRuntimePosition,
                acousticPortalNodes,
                acousticPortalEdges,
                out nodeCount,
                out edgeCount);
        }

        private bool TryBuildVoxelAcousticPortalGraph(
            Vector3 sourceRuntimePosition,
            Vector3 listenerRuntimePosition,
            NativeArray<AcousticPortalNode> acousticPortalNodes,
            NativeArray<AcousticPortalEdge> acousticPortalEdges,
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

                acousticPortalNodes[i] = new AcousticPortalNode
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
                    acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = i - 1,
                        DistanceMeters = ResolveRuntimeDistanceMeters(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i - 1]),
                        Flags = AcousticPortalFlags.Voxel
                    };
                }

                if (i + 1 < nodeCount && edgeCount < AcousticPortalMaxEdges)
                {
                    acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = i + 1,
                        DistanceMeters = ResolveRuntimeDistanceMeters(_acousticPortalWaypointScratch[i], _acousticPortalWaypointScratch[i + 1]),
                        Flags = AcousticPortalFlags.Voxel
                    };
                }

                AcousticPortalNode node = acousticPortalNodes[i];
                node.FirstEdge = startEdge;
                node.EdgeCount = edgeCount - startEdge;
                acousticPortalNodes[i] = node;
            }

            return nodeCount >= 2 && edgeCount > 0;
        }

        private static float ResolveRuntimeDistanceMeters(Vector3 a, Vector3 b)
        {
            if (!TryResolveAupFromRuntimeOrigin(a, out AbsoluteUniversePosition aupA) ||
                !TryResolveAupFromRuntimeOrigin(b, out AbsoluteUniversePosition aupB))
            {
                return 0f;
            }

            AcousticAup acousticA = ToAcousticAup(in aupA);
            AcousticAup acousticB = ToAcousticAup(in aupB);
            float distance = AcousticAup.DistanceMeters(in acousticA, in acousticB);
            return math.isfinite(distance) ? distance : 0f;
        }

        private bool TryBuildHabitatAcousticPortalGraph(
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            NativeArray<AcousticPortalNode> acousticPortalNodes,
            NativeArray<AcousticPortalEdge> acousticPortalEdges,
            out int nodeCount,
            out int edgeCount)
        {
            nodeCount = 0;
            edgeCount = 0;
            IHabitatGraphService habitatGraph = _cachedHabitatGraph;
            if (habitatGraph == null ||
                !habitatGraph.TryGetHabitatAcousticGraph(out HabitatGraphManager graph) ||
                _acousticHabitatNodeMap == null ||
                _acousticHabitatQueue == null ||
                graph.NodeCount < 2)
            {
                return false;
            }

            if (!TryFindNearestHabitatNode(graph, in sourceAup, out int sourceNode, out float sourceDistanceSq) ||
                !TryFindNearestHabitatNode(graph, in listenerAup, out int listenerNode, out float listenerDistanceSq))
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

                acousticPortalNodes[localIndex] = new AcousticPortalNode
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

                    AcousticPortalNode localNode = acousticPortalNodes[localIndex];
                    AcousticPortalNode destinationNode = acousticPortalNodes[destinationLocal];
                    float distance = AcousticAup.DistanceMeters(
                        in localNode.Position,
                        in destinationNode.Position);
                    if ((!math.isfinite(distance) || distance <= 0.001f) &&
                        edgeResistance.IsCreated &&
                        (uint)graphEdgeIndex < (uint)edgeResistance.Length)
                    {
                        distance = math.max(1f, edgeResistance[graphEdgeIndex] * 20f);
                    }

                    acousticPortalEdges[edgeCount++] = new AcousticPortalEdge
                    {
                        ToNode = destinationLocal,
                        DistanceMeters = math.max(0.001f, distance),
                        Flags = flags
                    };
                }

                AcousticPortalNode node = acousticPortalNodes[localIndex];
                node.FirstEdge = startEdge;
                node.EdgeCount = edgeCount - startEdge;
                acousticPortalNodes[localIndex] = node;
            }

            return nodeCount >= 2 && edgeCount > 0;
        }

        private bool TryFindNearestHabitatNode(
            HabitatGraphManager graph,
            in AbsoluteUniversePosition targetAup,
            out int nodeIndex,
            out float distanceSq)
        {
            nodeIndex = -1;
            distanceSq = float.PositiveInfinity;
            AcousticAup targetAcousticAup = ToAcousticAup(in targetAup);
            int count = graph != null ? graph.NodeCount : 0;
            for (int i = 0; i < count; i++)
            {
                if (!graph.TryGetAcousticNodePosition(i, out float3 nodePosition))
                    continue;

                if (!TryResolveAupFromRuntimeOrigin(new Vector3(nodePosition.x, nodePosition.y, nodePosition.z), out AbsoluteUniversePosition nodeAup))
                    continue;

                AcousticAup nodeAcousticAup = ToAcousticAup(in nodeAup);
                float candidateDistance = AcousticAup.DistanceMeters(in nodeAcousticAup, in targetAcousticAup);
                if (!math.isfinite(candidateDistance))
                    continue;

                float candidateDistanceSq = candidateDistance * candidateDistance;
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

            int frame = SystemDispatcher.CurrentFrameIndex;
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

        private void WriteAcousticPortalBlackBox(
            in AcousticPathResult result,
            int frame,
            BufferID sourceBufferId = BufferID.SpatialAudioPortalResult,
            uint sourceGeneration = 0u,
            uint failureCode = AcousticPortalFailureNone)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsAudioVaultHandle(
                    in _acousticPortalBlackBoxHandle,
                    BufferID.SpatialAudioPortalBlackBox,
                    SystemID.Audio))
            {
                return;
            }

            bool blackBoxLocked = false;
            bool shouldDump = false;
            bool resultFinite = IsAcousticPathResultFinite(in result);
            uint resolvedFailureCode = failureCode;
            if (!resultFinite && resolvedFailureCode == AcousticPortalFailureNone)
                resolvedFailureCode = AcousticPortalFailureNonFiniteResult;
            if (sourceGeneration == 0u && resolvedFailureCode == AcousticPortalFailureNone)
                sourceGeneration = _acousticPortalResultHandle.Generation;

            NativeArray<AcousticPortalTelemetryEntry> blackBox = default;
            try
            {
                if (!vault.TryAcquireWriteLock(in _acousticPortalBlackBoxHandle, SystemID.Audio, out blackBox))
                    return;

                blackBoxLocked = true;
                if (!blackBox.IsCreated || blackBox.Length <= 0)
                    return;

                uint flags = 0u;
                if (result.UsedPortalPath != 0)
                    flags |= 1u;
                if (result.UsedSealedBulkhead != 0)
                    flags |= 2u;
                if (result.UsedReprojectionCache != 0)
                    flags |= 4u;

                int index = _acousticPortalBlackBoxCursor % blackBox.Length;
                blackBox[index] = new AcousticPortalTelemetryEntry
                {
                    StopwatchTicks = System.Diagnostics.Stopwatch.GetTimestamp(),
                    Frame = frame,
                    NodeCount = result.NodeCount,
                    CornerCount = result.CornerCount,
                    ExpandedNodeCount = result.ExpandedNodeCount,
                    PathfindingMs = result.PathfindingMs,
                    TrueDistanceMeters = result.TrueDistanceMeters,
                    DelaySeconds = result.DelaySeconds,
                    LowPassCutoffHz = result.LowPassCutoffHz,
                    Flags = flags,
                    StateHash = result.StateHash,
                    BufferId = (uint)sourceBufferId,
                    Generation = sourceGeneration,
                    FailureCode = resolvedFailureCode
                };
                _acousticPortalBlackBoxCursor = (index + 1) % blackBox.Length;
                shouldDump = !resultFinite;
            }
            finally
            {
                if (blackBoxLocked)
                    vault.ReleaseWriteLock(in _acousticPortalBlackBoxHandle, SystemID.Audio);
            }

            if (shouldDump)
            {
                _acousticPortalBlackBoxDumpPending = true;
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)AcousticPortalFailureNonFiniteResult));
            }
        }

        private void WriteAcousticPortalFailureBlackBox(
            BufferID sourceBufferId,
            uint sourceGeneration,
            uint failureCode)
        {
            AcousticPathQuery query = default;
            AcousticPathResult result = AcousticPathResult.Fallback(AcousticPathStatus.InvalidInput, in query);
            uint hash = 2166136261u;
            hash = (hash ^ (uint)sourceBufferId) * 16777619u;
            hash = (hash ^ sourceGeneration) * 16777619u;
            hash = (hash ^ failureCode) * 16777619u;
            result.StateHash = hash;
            WriteAcousticPortalBlackBox(
                in result,
                SystemDispatcher.CurrentFrameIndex,
                sourceBufferId,
                sourceGeneration,
                failureCode);
        }

        private static bool IsAcousticPathResultFinite(in AcousticPathResult result)
        {
            return math.isfinite(result.PathfindingMs) &&
                   math.isfinite(result.TrueDistanceMeters) &&
                   math.isfinite(result.DelaySeconds) &&
                   math.isfinite(result.LowPassCutoffHz) &&
                   math.isfinite(result.Transmission01) &&
                   math.isfinite(result.ItdSeconds) &&
                   AcousticAup.IsFinite(in result.LastPortalAup);
        }

        private void TryFlushPendingAcousticPortalBlackBoxDump()
        {
            if (!_acousticPortalBlackBoxDumpPending)
                return;

            _acousticPortalBlackBoxDumpPending = false;
            DumpAcousticPortalBlackBox();
        }

        private void DumpAcousticPortalBlackBox()
        {
#if !UNITY_EDITOR
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)AcousticPortalFailureDumpIo));
            return;
#else
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsAudioVaultHandle(
                    in _acousticPortalBlackBoxHandle,
                    BufferID.SpatialAudioPortalBlackBox,
                    SystemID.Audio) ||
                !vault.TryReadOnlyHandle(
                    in _acousticPortalBlackBoxHandle,
                    out NativeArray<AcousticPortalTelemetryEntry>.ReadOnly blackBox) ||
                blackBox.Length <= 0)
            {
                return;
            }

            try
            {
                int totalBytes = AcousticPortalDumpHeaderBytes + (blackBox.Length * AcousticPortalTelemetryEntryBytes);
                NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(SpatialAudioManager),
                    "acousticPortalBlackBoxPayload");
                try
                {
                    unsafe
                    {
                        byte* payloadPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                        Span<byte> payloadSpan = new Span<byte>(payloadPtr, totalBytes);
                        BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(0, 4), AcousticPortalDumpMagic);
                        BinaryPrimitives.WriteUInt32LittleEndian(payloadSpan.Slice(4, 4), AcousticPortalDumpVersion);
                        BinaryPrimitives.WriteInt32LittleEndian(payloadSpan.Slice(8, 4), blackBox.Length);
                        BinaryPrimitives.WriteInt32LittleEndian(payloadSpan.Slice(12, 4), _acousticPortalBlackBoxCursor);
                        BinaryPrimitives.WriteInt32LittleEndian(payloadSpan.Slice(16, 4), AcousticPortalTelemetryEntryBytes);

                        int offset = AcousticPortalDumpHeaderBytes;
                        for (int i = 0; i < blackBox.Length; i++)
                        {
                            AcousticPortalTelemetryEntry entry = blackBox[i];
                            WriteAcousticPortalTelemetryEntry(payloadSpan.Slice(offset, AcousticPortalTelemetryEntryBytes), in entry);
                            offset += AcousticPortalTelemetryEntryBytes;
                        }
                    }

                    NativeFaultDumpWriter.TryWriteAll(AcousticPortalDumpRelativePath, payload, totalBytes);
                }
                finally
                {
                    NativeFaultDumpWriter.DisposeTransientPayload(
                        ref payload,
                        nameof(SpatialAudioManager),
                        "acousticPortalBlackBoxPayload");
                }
            }
            catch (Exception)
            {
                GlobalTelemetryBus.PublishMathGuardInvalidNumber(unchecked((int)AcousticPortalFailureDumpIo));
            }
#endif
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

        private bool EnsureAudioVaultHandle<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options)
            where T : struct
        {
            if (requiredLength <= 0)
            {
                handle = default;
                return false;
            }

            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive)
            {
                handle = default;
                return false;
            }

            if (TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out NativeArray<T> existing))
                return true;

            if (vault.IsAllocationLocked)
            {
                if (!vault.TryGetGenerationHandle(bufferId, out handle) ||
                    !TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out existing))
                {
                    handle = default;
                    return false;
                }

                return true;
            }

            if (vault.IsCompactionFenceActive || vault.IsAllocationLocked)
                return false;

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                SystemID.Audio,
                options);
            if (!TryOpenAudioVaultBuffer(vault, ref handle, bufferId, SystemID.Audio, requiredLength, out existing))
            {
                handle = default;
                return false;
            }

            return true;
        }

        private bool TryAcquireAudioVaultWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsAudioVaultHandle(in handle, bufferId, SystemID.Audio) ||
                !vault.TryAcquireWriteLock(in handle, SystemID.Audio, out buffer))
            {
                return false;
            }

            bool releaseOnFailure = true;
            try
            {
                if (vault.IsCompactionFenceActive ||
                    !buffer.IsCreated ||
                    buffer.Length < requiredLength)
                {
                    return false;
                }

                releaseOnFailure = false;
                return true;
            }
            finally
            {
                if (releaseOnFailure)
                {
                    vault.ReleaseWriteLock(in handle, SystemID.Audio);
                    buffer = default;
                }
            }
        }

        private void ReleaseAudioVaultWriteBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            IDataVault vault = _dataVault;
            ReleaseAudioVaultWriteBuffer(vault, in handle, bufferId);
        }

        private void ReleaseAudioVaultWriteBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            if (vault != null && IsAudioVaultHandle(in handle, bufferId, SystemID.Audio))
                vault.ReleaseWriteLock(in handle, SystemID.Audio);
        }

        private bool TryAcquirePreviousVelocityAupBuffers(
            out NativeArray<AbsoluteUniversePosition> previousVelocityAups,
            out NativeArray<int> previousVelocityAupFrames)
        {
            previousVelocityAups = default;
            previousVelocityAupFrames = default;
            if (_poolSize <= 0)
                return false;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(PreviousVelocityAupMutationGuardMask))
            {
                return false;
            }

            bool guardHeld = true;
            try
            {
                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _previousVelocityAupsHandle,
                        SpatialAudioPreviousVelocityAupsBufferId,
                        SystemID.Audio,
                        _poolSize,
                        out previousVelocityAups))
                {
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _previousVelocityAupFramesHandle,
                        SpatialAudioPreviousVelocityAupFramesBufferId,
                        SystemID.Audio,
                        _poolSize,
                        out previousVelocityAupFrames))
                {
                    previousVelocityAups = default;
                    return false;
                }

                guardHeld = false;
                _previousVelocityAupGuardVault = vault;
                return true;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(PreviousVelocityAupMutationGuardMask);
            }
        }

        private void ReleasePreviousVelocityAupBuffers()
        {
            IDataVault vault = _previousVelocityAupGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(PreviousVelocityAupMutationGuardMask);
            _previousVelocityAupGuardVault = null;
        }

        private void ResetPreviousVelocityAupState()
        {
            if (!TryAcquirePreviousVelocityAupBuffers(
                    out NativeArray<AbsoluteUniversePosition> previousVelocityAups,
                    out NativeArray<int> previousVelocityAupFrames))
            {
                return;
            }

            try
            {
                int limit = math.min(_poolSize, math.min(previousVelocityAups.Length, previousVelocityAupFrames.Length));
                for (int i = 0; i < limit; i++)
                {
                    previousVelocityAups[i] = default;
                    previousVelocityAupFrames[i] = -1;
                }
            }
            finally
            {
                ReleasePreviousVelocityAupBuffers();
            }
        }

        private void ResetPreviousVelocityAupSlot(int sourceIndex)
        {
            if ((uint)sourceIndex >= (uint)_poolSize)
                return;

            if (!TryAcquirePreviousVelocityAupBuffers(
                    out NativeArray<AbsoluteUniversePosition> previousVelocityAups,
                    out NativeArray<int> previousVelocityAupFrames))
            {
                return;
            }

            try
            {
                if ((uint)sourceIndex < (uint)previousVelocityAups.Length)
                    previousVelocityAups[sourceIndex] = default;
                if ((uint)sourceIndex < (uint)previousVelocityAupFrames.Length)
                    previousVelocityAupFrames[sourceIndex] = -1;
            }
            finally
            {
                ReleasePreviousVelocityAupBuffers();
            }
        }

        private void ResetPreviousVelocityAupSlotLocal(
            int sourceIndex,
            NativeArray<AbsoluteUniversePosition> previousVelocityAups,
            NativeArray<int> previousVelocityAupFrames)
        {
            if (sourceIndex < 0)
                return;

            if (previousVelocityAups.IsCreated && (uint)sourceIndex < (uint)previousVelocityAups.Length)
                previousVelocityAups[sourceIndex] = default;
            if (previousVelocityAupFrames.IsCreated && (uint)sourceIndex < (uint)previousVelocityAupFrames.Length)
                previousVelocityAupFrames[sourceIndex] = -1;
        }

        private void DropVirtualVoiceSortBatchForVaultLockFailure()
        {
            _virtualVoiceDroppedCount += math.clamp(_virtualVoiceSortCount, 0, MaxVirtualVoiceCapacity);
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _acousticOcclusionOutputCount = 0;
            _lastVirtualVoiceStatistics = default;
        }

        private void ReleaseVirtualVoiceSortBufferLocks()
        {
            IDataVault vault = _virtualVoiceSortBuffersLockVault;
            if (_virtualVoiceSelectionsLockedForSort)
            {
                _virtualVoiceSelectionsLockedForSort = false;
                vault?.TryUnlockBuffer(BufferID.SpatialAudioVirtualVoiceSelections, SystemID.Audio);
            }

            if (_virtualVoiceSortKeyPoolLockedForSort)
            {
                _virtualVoiceSortKeyPoolLockedForSort = false;
                vault?.TryUnlockBuffer(SpatialAudioVirtualVoiceSortKeyPoolBufferId, SystemID.Audio);
            }

            if (_virtualVoiceSortPoolLockedForSort)
            {
                _virtualVoiceSortPoolLockedForSort = false;
                vault?.TryUnlockBuffer(SpatialAudioVirtualVoiceSortPoolBufferId, SystemID.Audio);
            }

            if (_virtualVoiceStatisticsLockedForSort)
            {
                _virtualVoiceStatisticsLockedForSort = false;
                vault?.TryUnlockBuffer(BufferID.SpatialAudioVirtualVoiceStatistics, SystemID.Audio);
            }

            _virtualVoiceSortBuffersLockVault = null;
        }

        private bool TryLockVirtualVoiceSortBuffers(IDataVault vault)
        {
            if (vault == null ||
                _virtualVoiceSortBuffersLockVault != null ||
                _virtualVoiceSelectionsLockedForSort ||
                _virtualVoiceSortKeyPoolLockedForSort ||
                _virtualVoiceSortPoolLockedForSort ||
                _virtualVoiceStatisticsLockedForSort ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            _virtualVoiceSortBuffersLockVault = vault;
            bool locked = false;
            try
            {
                if (!TryLockVirtualVoiceSortBuffer(vault, SpatialAudioVirtualVoiceSortPoolBufferId, ref _virtualVoiceSortPoolLockedForSort) ||
                    !TryLockVirtualVoiceSortBuffer(vault, SpatialAudioVirtualVoiceSortKeyPoolBufferId, ref _virtualVoiceSortKeyPoolLockedForSort) ||
                    !TryLockVirtualVoiceSortBuffer(vault, BufferID.SpatialAudioVirtualVoiceSelections, ref _virtualVoiceSelectionsLockedForSort) ||
                    !TryLockVirtualVoiceSortBuffer(vault, BufferID.SpatialAudioVirtualVoiceStatistics, ref _virtualVoiceStatisticsLockedForSort))
                {
                    return false;
                }

                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    ReleaseVirtualVoiceSortBufferLocks();
            }
        }

        private static bool TryLockVirtualVoiceSortBuffer(IDataVault vault, BufferID bufferId, ref bool locked)
        {
            if (locked)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.Audio))
                return false;

            locked = true;
            return true;
        }

        private void ReleaseAcousticOcclusionBufferLocks()
        {
            ReleaseAcousticOcclusionSdfSnapshotLock();

            IDataVault vault = _acousticOcclusionBuffersLockVault;
            if (_acousticDspOutputPoolLockedForOcclusion)
            {
                _acousticDspOutputPoolLockedForOcclusion = false;
                vault?.TryUnlockBuffer(SpatialAudioAcousticDspOutputPoolBufferId, SystemID.Audio);
            }

            if (_acousticSelectedPreviousAupPoolLockedForOcclusion)
            {
                _acousticSelectedPreviousAupPoolLockedForOcclusion = false;
                vault?.TryUnlockBuffer(SpatialAudioAcousticSelectedPreviousAupPoolBufferId, SystemID.Audio);
            }

            if (_acousticSelectedSourcePoolLockedForOcclusion)
            {
                _acousticSelectedSourcePoolLockedForOcclusion = false;
                vault?.TryUnlockBuffer(SpatialAudioAcousticSelectedSourcePoolBufferId, SystemID.Audio);
            }

            _acousticOcclusionBuffersLockVault = null;
        }

        private bool TryLockAcousticOcclusionBuffers(IDataVault vault)
        {
            if (vault == null ||
                _acousticOcclusionBuffersLockVault != null ||
                _acousticDspOutputPoolLockedForOcclusion ||
                _acousticSelectedPreviousAupPoolLockedForOcclusion ||
                _acousticSelectedSourcePoolLockedForOcclusion ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            _acousticOcclusionBuffersLockVault = vault;
            bool locked = false;
            try
            {
                if (!TryLockAcousticOcclusionBuffer(vault, SpatialAudioAcousticSelectedSourcePoolBufferId, ref _acousticSelectedSourcePoolLockedForOcclusion) ||
                    !TryLockAcousticOcclusionBuffer(vault, SpatialAudioAcousticSelectedPreviousAupPoolBufferId, ref _acousticSelectedPreviousAupPoolLockedForOcclusion) ||
                    !TryLockAcousticOcclusionBuffer(vault, SpatialAudioAcousticDspOutputPoolBufferId, ref _acousticDspOutputPoolLockedForOcclusion))
                {
                    return false;
                }

                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    ReleaseAcousticOcclusionBufferLocks();
            }
        }

        private static bool TryLockAcousticOcclusionBuffer(IDataVault vault, BufferID bufferId, ref bool locked)
        {
            if (locked)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.Audio))
                return false;

            locked = true;
            return true;
        }

        private void ReleaseAcousticOcclusionSdfSnapshotLock()
        {
            if (!_acousticOcclusionSdfSnapshotGuardHeld)
                return;

            IDataVault vault = _acousticOcclusionSdfSnapshotGuardVault;
            if (vault != null)
                vault.TryUnlockBuffer(SpatialAudioAcousticVoxelSdfTexture3DBufferId, SystemID.Audio);

            _acousticOcclusionSdfSnapshotGuardHeld = false;
            _acousticOcclusionSdfSnapshotGuardVault = null;
        }

        private void UnlockAcousticOcclusionSdfSnapshot(ref bool locked)
        {
            if (!locked)
                return;

            IDataVault vault = _acousticOcclusionSdfSnapshotGuardVault;
            if (vault != null)
                vault.TryUnlockBuffer(SpatialAudioAcousticVoxelSdfTexture3DBufferId, SystemID.Audio);

            locked = false;
            if (!_acousticOcclusionSdfSnapshotGuardHeld)
                _acousticOcclusionSdfSnapshotGuardVault = null;
        }

        private void ReleaseAcousticMaterialRowsOcclusionLock()
        {
            if (!_acousticMaterialRowsLockedForOcclusion)
                return;

            IDataVault vault = _acousticMaterialRowsLockVault;
            if (vault != null &&
                IsAudioVaultHandle(
                    in _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    SystemID.Audio))
            {
                vault.TryUnlockBuffer(SpatialAudioAcousticMaterialRowsBufferId, SystemID.Audio);
            }

            _acousticMaterialRowsLockedForOcclusion = false;
            _acousticMaterialRowsLockVault = null;
        }

        private bool TryLockAcousticMaterialRowsForOcclusion(
            out NativeArray<AcousticMaterialCoefficientDTO>.ReadOnly materialRows)
        {
            materialRows = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                _acousticMaterialRowsLockedForOcclusion ||
                vault.IsCompactionFenceActive ||
                !IsAudioVaultHandle(
                    in _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    SystemID.Audio))
            {
                return false;
            }

            if (!vault.TryLockBuffer(SpatialAudioAcousticMaterialRowsBufferId, SystemID.Audio))
                return false;

            _acousticMaterialRowsLockedForOcclusion = true;
            _acousticMaterialRowsLockVault = vault;
            bool locked = false;
            try
            {
                if (!TryReadAudioVaultBuffer(
                        in _acousticMaterialRowsHandle,
                        SpatialAudioAcousticMaterialRowsBufferId,
                        3,
                        out materialRows))
                {
                    return false;
                }

                locked = true;
                return true;
            }
            finally
            {
                if (!locked)
                    ReleaseAcousticMaterialRowsOcclusionLock();
            }
        }

        private void ReleaseVirtualVoiceStatisticsSortLock()
        {
            if (!_virtualVoiceStatisticsLockedForSort)
                return;

            ReleaseVirtualVoiceSortBufferLocks();
        }

        private bool HasAudioVaultReadBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsAudioVaultHandle(in handle, bufferId, SystemID.Audio) &&
                   vault.TryReadOnlyHandle(in handle, out NativeArray<T>.ReadOnly buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadAudioVaultBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   requiredLength > 0 &&
                   IsAudioVaultHandle(in handle, bufferId, SystemID.Audio) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryReadAcousticRadarIntensityBins(out NativeArray<float>.ReadOnly radialIntensityBins)
        {
            return TryReadAudioVaultBuffer(
                in _acousticRadarIntensityBinsHandle,
                BufferID.SpatialAudioRadarIntensityBins,
                AcousticRadarBinCount,
                out radialIntensityBins);
        }

        private bool TryReadAcousticRadarGrid(out NativeArray<float>.ReadOnly gridEnergy)
        {
            return TryReadAudioVaultBuffer(
                in _acousticRadarGridHandle,
                BufferID.SpatialAudioRadarGrid,
                AcousticRadarGridCellCount,
                out gridEnergy);
        }

        private bool TryReadVirtualVoiceStatisticsSnapshot(out VirtualVoiceStatistics statistics)
        {
            statistics = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsAudioVaultHandle(
                    in _virtualVoiceStatisticsHandle,
                    BufferID.SpatialAudioVirtualVoiceStatistics,
                    SystemID.Audio) ||
                !vault.TryReadOnlyHandle(
                    in _virtualVoiceStatisticsHandle,
                    out NativeArray<VirtualVoiceStatistics>.ReadOnly statisticsView) ||
                !statisticsView.IsCreated ||
                statisticsView.Length <= 0)
            {
                return false;
            }

            statistics = statisticsView[0];
            return true;
        }

        private bool WriteVirtualVoiceStatisticsSnapshot(in VirtualVoiceStatistics statistics)
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _virtualVoiceStatisticsHandle,
                    BufferID.SpatialAudioVirtualVoiceStatistics,
                    1,
                    out NativeArray<VirtualVoiceStatistics> statisticsView))
            {
                return false;
            }

            try
            {
                statisticsView[0] = statistics;
                return true;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _virtualVoiceStatisticsHandle,
                    BufferID.SpatialAudioVirtualVoiceStatistics);
            }
        }

        private static void WriteAcousticPortalTelemetryEntry(Span<byte> destination, in AcousticPortalTelemetryEntry entry)
        {
            destination.Clear();
            BinaryPrimitives.WriteInt64LittleEndian(destination.Slice(0, 8), entry.StopwatchTicks);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(8, 4), entry.Frame);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(12, 4), entry.NodeCount);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(16, 4), entry.CornerCount);
            BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(20, 4), entry.ExpandedNodeCount);
            WriteFloatLittleEndian(destination.Slice(24, 4), entry.PathfindingMs);
            WriteFloatLittleEndian(destination.Slice(28, 4), entry.TrueDistanceMeters);
            WriteFloatLittleEndian(destination.Slice(32, 4), entry.DelaySeconds);
            WriteFloatLittleEndian(destination.Slice(36, 4), entry.LowPassCutoffHz);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(40, 4), entry.Flags);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(44, 4), entry.StateHash);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(48, 4), entry.BufferId);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(52, 4), entry.Generation);
            BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(56, 4), entry.FailureCode);
        }

        private static void WriteFloatLittleEndian(Span<byte> destination, float value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(destination, math.asuint(value));
        }

        private bool TryAcquireAcousticPortalWorkBuffers(
            out NativeArray<AcousticPortalNode> nodes,
            out NativeArray<AcousticPortalEdge> edges,
            out NativeArray<AcousticPathResult> result,
            out NativeArray<float> costs,
            out NativeArray<int> cameFrom,
            out NativeArray<byte> states)
        {
            nodes = default;
            edges = default;
            result = default;
            costs = default;
            cameFrom = default;
            states = default;

            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(AcousticPortalPathMutationGuardMask))
            {
                WriteAcousticPortalFailureBlackBox(
                    BufferID.SpatialAudioPortalNodes,
                    _acousticPortalNodesHandle.Generation,
                    AcousticPortalFailureLockOrCapacity);
                return false;
            }

            bool guardHeld = true;
            try
            {
                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalNodesHandle,
                        BufferID.SpatialAudioPortalNodes,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out nodes))
                {
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalNodes,
                        _acousticPortalNodesHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalEdgesHandle,
                        BufferID.SpatialAudioPortalEdges,
                        SystemID.Audio,
                        AcousticPortalMaxEdges,
                        out edges))
                {
                    nodes = default;
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalEdges,
                        _acousticPortalEdgesHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalResultHandle,
                        BufferID.SpatialAudioPortalResult,
                        SystemID.Audio,
                        1,
                        out result))
                {
                    nodes = default;
                    edges = default;
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalResult,
                        _acousticPortalResultHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalCostsHandle,
                        BufferID.SpatialAudioPortalCosts,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out costs))
                {
                    nodes = default;
                    edges = default;
                    result = default;
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalCosts,
                        _acousticPortalCostsHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalCameFromHandle,
                        BufferID.SpatialAudioPortalCameFrom,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out cameFrom))
                {
                    nodes = default;
                    edges = default;
                    result = default;
                    costs = default;
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalCameFrom,
                        _acousticPortalCameFromHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalStatesHandle,
                        BufferID.SpatialAudioPortalStates,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out states))
                {
                    nodes = default;
                    edges = default;
                    result = default;
                    costs = default;
                    cameFrom = default;
                    WriteAcousticPortalFailureBlackBox(
                        BufferID.SpatialAudioPortalStates,
                        _acousticPortalStatesHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                guardHeld = false;
                _acousticPortalWorkGuardVault = vault;
                return true;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(AcousticPortalPathMutationGuardMask);
            }
        }

        private void ReleaseAcousticPortalWorkBuffers()
        {
            IDataVault vault = _acousticPortalWorkGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(AcousticPortalPathMutationGuardMask);
            _acousticPortalWorkGuardVault = null;
        }

        private bool TryAcquireAcousticPortalScratchSets(
            out NativeArray<int> openSet,
            out NativeArray<int> closedSet)
        {
            openSet = default;
            closedSet = default;
            IDataVault pathGuardVault = _acousticPortalWorkGuardVault;
            bool usingPathGuard = pathGuardVault != null;
            IDataVault vault = usingPathGuard ? pathGuardVault : _dataVault;
            if (vault == null)
            {
                WriteAcousticPortalFailureBlackBox(SpatialAudioPortalOpenSetBufferId, 0u, AcousticPortalFailureHandleInvalid);
                return false;
            }

            if (!IsAudioVaultHandle(in _acousticPortalOpenSetHandle, SpatialAudioPortalOpenSetBufferId, SystemID.Audio))
            {
                WriteAcousticPortalFailureBlackBox(
                    SpatialAudioPortalOpenSetBufferId,
                    _acousticPortalOpenSetHandle.Generation,
                    AcousticPortalFailureHandleInvalid);
                return false;
            }

            if (!IsAudioVaultHandle(in _acousticPortalClosedSetHandle, SpatialAudioPortalClosedSetBufferId, SystemID.Audio))
            {
                WriteAcousticPortalFailureBlackBox(
                    SpatialAudioPortalClosedSetBufferId,
                    _acousticPortalClosedSetHandle.Generation,
                    AcousticPortalFailureHandleInvalid);
                return false;
            }

            if (!usingPathGuard && !vault.TryAcquireMutationGuard(AcousticPortalScratchMutationGuardMask))
            {
                WriteAcousticPortalFailureBlackBox(
                    SpatialAudioPortalOpenSetBufferId,
                    _acousticPortalOpenSetHandle.Generation,
                    AcousticPortalFailureLockOrCapacity);
                return false;
            }

            bool guardHeld = !usingPathGuard;
            try
            {
                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalOpenSetHandle,
                        SpatialAudioPortalOpenSetBufferId,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out openSet))
                {
                    WriteAcousticPortalFailureBlackBox(
                        SpatialAudioPortalOpenSetBufferId,
                        _acousticPortalOpenSetHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                if (!TryOpenAudioVaultBuffer(
                        vault,
                        ref _acousticPortalClosedSetHandle,
                        SpatialAudioPortalClosedSetBufferId,
                        SystemID.Audio,
                        AcousticPortalMaxNodes,
                        out closedSet))
                {
                    openSet = default;
                    WriteAcousticPortalFailureBlackBox(
                        SpatialAudioPortalClosedSetBufferId,
                        _acousticPortalClosedSetHandle.Generation,
                        AcousticPortalFailureLockOrCapacity);
                    return false;
                }

                guardHeld = false;
                if (!usingPathGuard)
                    _acousticPortalScratchGuardVault = vault;
                return true;
            }
            finally
            {
                if (guardHeld)
                    vault.ReleaseMutationGuard(AcousticPortalScratchMutationGuardMask);
            }
        }

        private void ReleaseAcousticPortalScratchSets()
        {
            IDataVault vault = _acousticPortalScratchGuardVault;
            if (vault != null)
                vault.ReleaseMutationGuard(AcousticPortalScratchMutationGuardMask);
            _acousticPortalScratchGuardVault = null;
        }

        private static int GenerateEmergencyMockAcoustics(NativeArray<AcousticMaterialCoefficientDTO> rows)
        {
            if (!rows.IsCreated || rows.Length <= 0)
                return 0;

            int count = math.min(rows.Length, 3);
            if (count > 0)
            {
                rows[0] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0x3A1B4AB4u,
                    Absorption01 = 0.32f,
                    Scatter01 = 0.55f,
                    Density01 = 0.85f,
                    LowPassHertz = 2100f,
                    Flags = 1u
                };
            }

            if (count > 1)
            {
                rows[1] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0xD756AEDCu,
                    Absorption01 = 0.18f,
                    Scatter01 = 0.28f,
                    Density01 = 1f,
                    LowPassHertz = 3400f,
                    Flags = 1u
                };
            }

            if (count > 2)
            {
                rows[2] = new AcousticMaterialCoefficientDTO
                {
                    MaterialHash = 0x02FC484Du,
                    Absorption01 = 0.62f,
                    Scatter01 = 0.75f,
                    Density01 = 0.45f,
                    LowPassHertz = 1200f,
                    Flags = 1u
                };
            }

            return count;
        }

        private void RefreshVirtualExternalStateAliases()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
            {
                _virtualVoiceScalabilityStateHandle = default;
                _virtualVoiceRollbackAudioSuppressionHandle = default;
                return;
            }

            RefreshBorrowedAudioVaultHandle(
                vault,
                ref _virtualVoiceScalabilityStateHandle,
                BufferID.ShinobuScalabilityState,
                SystemID.GraphicsScalability,
                1);

            RefreshBorrowedAudioVaultHandle(
                vault,
                ref _virtualVoiceRollbackAudioSuppressionHandle,
                RollbackNetcodeVault.AudioSuppression,
                RollbackNetcodeVault.OwnerSystem,
                1);
        }

        private static bool RefreshBorrowedAudioVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            SystemID ownerSystem,
            int requiredLength) where T : struct
        {
            if (TryOpenBorrowedAudioVaultBuffer(vault, ref handle, bufferId, ownerSystem, requiredLength, out NativeArray<T> buffer))
                return true;

            handle = default;
            return false;
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
                vault.IsCompactionFenceActive ||
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
                vault.IsCompactionFenceActive ||
                requiredLength <= 0 ||
                !IsAudioVaultHandle(in handle, bufferId, ownerSystem) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                vault.IsCompactionFenceActive ||
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

        private static ulong AudioVaultMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ClearVaultBackedTelemetryAliases()
        {
            if (_virtualVoiceSortScheduled)
                CompleteVirtualVoiceSortJobForBarrier();
            else
            {
                ReleaseVirtualVoiceSortBufferLocks();
                ReleaseVirtualVoiceStatisticsSortLock();
            }

            if (_acousticOcclusionScheduled)
                CompleteAcousticOcclusionForBarrier();
            else
            {
                ReleaseAcousticMaterialRowsOcclusionLock();
                ReleaseAcousticOcclusionBufferLocks();
            }

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
            _previousVelocityAupsHandle = default;
            _previousVelocityAupFramesHandle = default;
            _virtualVoiceScalabilityStateHandle = default;
            _virtualVoiceRollbackAudioSuppressionHandle = default;
            _acousticVoxelSdfTexture3DHandle = default;
            _acousticPortalNodesHandle = default;
            _acousticPortalEdgesHandle = default;
            _acousticPortalResultHandle = default;
            _acousticPortalCostsHandle = default;
            _acousticPortalCameFromHandle = default;
            _acousticPortalStatesHandle = default;
            _acousticPortalOpenSetHandle = default;
            _acousticPortalClosedSetHandle = default;
            _acousticPortalBlackBoxHandle = default;
            _virtualVoiceWriteCount = 0;
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _acousticOcclusionOutputCount = 0;
            _acousticOcclusionScheduled = false;
            _acousticMaterialRowsLockedForOcclusion = false;
            _acousticMaterialRowsLockVault = null;
            _virtualVoiceStatisticsLockedForSort = false;
            _virtualVoiceSortPoolLockedForSort = false;
            _virtualVoiceSortKeyPoolLockedForSort = false;
            _virtualVoiceSelectionsLockedForSort = false;
            _virtualVoiceSortBuffersLockVault = null;
            _acousticSelectedSourcePoolLockedForOcclusion = false;
            _acousticSelectedPreviousAupPoolLockedForOcclusion = false;
            _acousticDspOutputPoolLockedForOcclusion = false;
            _acousticOcclusionBuffersLockVault = null;
            _previousVelocityAupGuardVault = null;
            _acousticPortalWorkGuardVault = null;
            _acousticPortalScratchGuardVault = null;
        }

        private void InitializeTelemetryCaches()
        {
            EnsureAudioVaultHandle(
                ref _acousticRadarIntensityBinsHandle,
                BufferID.SpatialAudioRadarIntensityBins,
                AcousticRadarBinCount,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticRadarGridHandle,
                BufferID.SpatialAudioRadarGrid,
                AcousticRadarGridCellCount,
                NativeArrayOptions.ClearMemory);

            if (_acousticRadarIntensityUploadScratch == null || _acousticRadarIntensityUploadScratch.Length != AcousticRadarBinCount)
                _acousticRadarIntensityUploadScratch = new float[AcousticRadarBinCount]; // COLD ALLOC: float[360] - CPU mirror for acoustic radar texture uploads - owner: SpatialAudioManager

            if (_acousticRadarGridUploadScratch == null || _acousticRadarGridUploadScratch.Length != AcousticRadarGridCellCount)
                _acousticRadarGridUploadScratch = new float[AcousticRadarGridCellCount]; // COLD ALLOC: float[32] - CPU mirror for acoustic radar grid GraphicsBuffer uploads - owner: SpatialAudioManager

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

            if (_acousticRadarGridBufferA == null)
                _acousticRadarGridBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, AcousticRadarGridCellCount, sizeof(float));
            if (_acousticRadarGridBufferB == null)
                _acousticRadarGridBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, AcousticRadarGridCellCount, sizeof(float));
            if (_activeAcousticRadarGridBuffer == null)
                _activeAcousticRadarGridBuffer = _acousticRadarGridBufferA;

            if (_delayedAudioIngress == null || _delayedAudioIngress.Length != MaxDelayedAudioEvents)
            {
                _delayedAudioIngress = new DelayedAudioEvent[MaxDelayedAudioEvents]; // COLD ALLOC: DelayedAudioEvent[16] - underwater propagation ingress ring - owner: SpatialAudioManager
                _delayedAudioIngressHead = 0;
                _delayedAudioIngressCount = 0;
            }

            if (_audioEventQueue == null || _audioEventQueue.Length != MaxQueuedAudioEvents)
            {
                _audioEventQueue = new CoreAudioEvent[MaxQueuedAudioEvents]; // COLD ALLOC: AudioEvent[32] - zero-GC gameplay audio ingress ring - owner: SpatialAudioManager
                _audioEventQueueHead = 0;
                _audioEventQueueCount = 0;
                _audioEventQueueDroppedCount = 0;
                _lastAudioEventQueueOverflowTelemetryFrame = -1;
                _lastAudioEventBadDataTelemetryFrame = -1;
            }
            EnsureAudioClipHashMapCold();

            if (_pendingDelayedAudioEvents == null || _pendingDelayedAudioEvents.Length != MaxDelayedAudioEvents)
            {
                _pendingDelayedAudioEvents = new DelayedAudioEvent[MaxDelayedAudioEvents]; // COLD ALLOC: DelayedAudioEvent[16] - active delayed world-event schedule - owner: SpatialAudioManager
                _pendingDelayedAudioEventCount = 0;
            }

            EnsureAudioVaultHandle(
                ref _virtualVoiceWritePoolHandle,
                SpatialAudioVirtualVoiceWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceSortPoolHandle,
                SpatialAudioVirtualVoiceSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceDtoPoolHandle,
                SpatialAudioVirtualVoiceDtoPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceSortKeyPoolHandle,
                SpatialAudioVirtualVoiceSortKeyPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticSourceWritePoolHandle,
                SpatialAudioAcousticSourceWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticSourceSortPoolHandle,
                SpatialAudioAcousticSourceSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticPreviousAupWritePoolHandle,
                SpatialAudioAcousticPreviousAupWritePoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticPreviousAupSortPoolHandle,
                SpatialAudioAcousticPreviousAupSortPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticDspOutputPoolHandle,
                SpatialAudioAcousticDspOutputPoolBufferId,
                MaxVirtualVoiceCapacity,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticSelectedSourcePoolHandle,
                SpatialAudioAcousticSelectedSourcePoolBufferId,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _acousticSelectedPreviousAupPoolHandle,
                SpatialAudioAcousticSelectedPreviousAupPoolBufferId,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.UninitializedMemory);
            EnsureAudioVaultHandle(
                ref _previousVelocityAupsHandle,
                SpatialAudioPreviousVelocityAupsBufferId,
                _poolSize,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _previousVelocityAupFramesHandle,
                SpatialAudioPreviousVelocityAupFramesBufferId,
                _poolSize,
                NativeArrayOptions.UninitializedMemory);
            ResetPreviousVelocityAupState();
            if (EnsureAudioVaultHandle(
                    ref _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    3,
                    NativeArrayOptions.UninitializedMemory) &&
                TryAcquireAudioVaultWriteBuffer(
                    in _acousticMaterialRowsHandle,
                    SpatialAudioAcousticMaterialRowsBufferId,
                    3,
                    out NativeArray<AcousticMaterialCoefficientDTO> materialRows))
            {
                try
                {
                    GenerateEmergencyMockAcoustics(materialRows);
                }
                finally
                {
                    ReleaseAudioVaultWriteBuffer(in _acousticMaterialRowsHandle, SpatialAudioAcousticMaterialRowsBufferId);
                }
            }

            EnsureAudioVaultHandle(
                ref _virtualVoiceSelectionsHandle,
                BufferID.SpatialAudioVirtualVoiceSelections,
                MaxVirtualPhysicalVoices,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceStatisticsHandle,
                BufferID.SpatialAudioVirtualVoiceStatistics,
                1,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceBlackBoxHandle,
                BufferID.SpatialAudioVirtualVoiceBlackBox,
                VirtualVoiceBlackBoxFrameCount,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _virtualVoiceTuningHandle,
                SpatialAudioVirtualVoiceTuningBufferId,
                1,
                NativeArrayOptions.ClearMemory);
            EnsureVirtualVoiceTuningState();
            RefreshVirtualExternalStateAliases();

            EnsureVirtualChannelArrays();

            EnsureAudioVaultHandle(
                ref _acousticPortalNodesHandle,
                BufferID.SpatialAudioPortalNodes,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalEdgesHandle,
                BufferID.SpatialAudioPortalEdges,
                AcousticPortalMaxEdges,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalResultHandle,
                BufferID.SpatialAudioPortalResult,
                1,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalCostsHandle,
                BufferID.SpatialAudioPortalCosts,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalCameFromHandle,
                BufferID.SpatialAudioPortalCameFrom,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalStatesHandle,
                BufferID.SpatialAudioPortalStates,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalOpenSetHandle,
                SpatialAudioPortalOpenSetBufferId,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);
            EnsureAudioVaultHandle(
                ref _acousticPortalClosedSetHandle,
                SpatialAudioPortalClosedSetBufferId,
                AcousticPortalMaxNodes,
                NativeArrayOptions.ClearMemory);

            EnsureAudioVaultHandle(
                ref _acousticPortalBlackBoxHandle,
                BufferID.SpatialAudioPortalBlackBox,
                AcousticPortalConstants.TelemetryFrameCount,
                NativeArrayOptions.ClearMemory);

            if (_acousticPortalWaypointScratch == null || _acousticPortalWaypointScratch.Length != AcousticPortalMaxNodes)
                _acousticPortalWaypointScratch = new Vector3[AcousticPortalMaxNodes]; // COLD ALLOC: Vector3[30] - voxel macro portal waypoint scratch - owner: SpatialAudioManager

            if (_acousticHabitatNodeMap == null || _acousticHabitatNodeMap.Length != AcousticPortalMaxNodes)
                _acousticHabitatNodeMap = new int[AcousticPortalMaxNodes]; // COLD ALLOC: int[30] - habitat acoustic global-to-local node map - owner: SpatialAudioManager

            if (_acousticHabitatQueue == null || _acousticHabitatQueue.Length != AcousticPortalMaxNodes)
                _acousticHabitatQueue = new int[AcousticPortalMaxNodes]; // COLD ALLOC: int[30] - habitat acoustic BFS queue - owner: SpatialAudioManager

            if (_acousticPortalCache == null || _acousticPortalCache.Length != AcousticPortalCacheCapacity)
                _acousticPortalCache = new AcousticPortalCacheEntry[AcousticPortalCacheCapacity]; // COLD ALLOC: AcousticPortalCacheEntry[16] - stationary emitter acoustic reprojection cache - owner: SpatialAudioManager
        }

        private void EnsureAudioClipHashMapCold()
        {
            AudioClip[] table = _audioEventClipTable;
            int requiredCapacity = ResolveAudioClipHashCapacity(table != null ? table.Length : 0);
            if (_audioClipHashKeys == null ||
                _audioClipHashTableIndices == null ||
                _audioClipHashKeys.Length != requiredCapacity ||
                _audioClipHashTableIndices.Length != requiredCapacity)
            {
                _audioClipHashKeys = new uint[requiredCapacity]; // COLD ALLOC: uint[clipHashCapacity] - open-addressed clip hash keys - owner: SpatialAudioManager
                _audioClipHashTableIndices = new int[requiredCapacity]; // COLD ALLOC: int[clipHashCapacity] - one-based clip table indices - owner: SpatialAudioManager
            }

            Array.Clear(_audioClipHashKeys, 0, _audioClipHashKeys.Length);
            Array.Clear(_audioClipHashTableIndices, 0, _audioClipHashTableIndices.Length);
            _audioClipHashMask = requiredCapacity - 1;
            _audioClipHashCount = 0;
            if (table == null)
                return;

            for (int i = 0; i < table.Length; i++)
            {
                AudioClip clip = table[i];
                if (clip == null)
                    continue;

                uint clipHash = unchecked((uint)EntityId.ToULong(clip.GetEntityId()));
                if (clipHash != 0u && TryInsertAudioClipHashCold(clipHash, i))
                {
                    _audioClipHashCount++;
                }
            }
        }

        private static int ResolveAudioClipHashCapacity(int tableLength)
        {
            int target = tableLength > 0x3FFFFFFF ? int.MaxValue : math.max(2, tableLength * 2);
            int capacity = 2;
            while (capacity > 0 && capacity < target)
                capacity <<= 1;

            return capacity > 0 ? capacity : 1 << 30;
        }

        private bool TryInsertAudioClipHashCold(uint clipHash, int tableIndex)
        {
            if (_audioClipHashKeys == null ||
                _audioClipHashTableIndices == null ||
                _audioClipHashMask <= 0)
            {
                return false;
            }

            int slot = (int)(clipHash & (uint)_audioClipHashMask);
            int probeLimit = _audioClipHashKeys.Length;
            for (int probe = 0; probe < probeLimit; probe++)
            {
                uint candidateHash = _audioClipHashKeys[slot];
                if (candidateHash == 0u)
                {
                    _audioClipHashKeys[slot] = clipHash;
                    _audioClipHashTableIndices[slot] = tableIndex + 1;
                    return true;
                }

                if (candidateHash == clipHash)
                    return false;

                slot = (slot + 1) & _audioClipHashMask;
            }

            return false;
        }

        private void ReleaseAudioClipHashMap()
        {
            _audioClipHashKeys = null;
            _audioClipHashTableIndices = null;
            _audioClipHashMask = 0;
            _audioClipHashCount = 0;
        }

        private void EnsureVirtualVoiceTuningState()
        {
            VirtualVoiceTuningSnapshot tuning = RefreshVirtualVoiceTuningFromVault()
                ? _virtualVoiceTuning
                : VirtualVoiceTuningSnapshot.CreateDefault();

            if (tuning.SoundSpeedMetersPerSecond <= 0f || !math.isfinite(tuning.SoundSpeedMetersPerSecond))
                tuning = VirtualVoiceTuningSnapshot.CreateDefault();

            tuning = VirtualVoiceTuningSnapshot.Sanitize(in tuning);
            _virtualVoiceTuning = tuning;
            WriteVirtualVoiceTuningToVault(in tuning);
        }

        private bool RefreshVirtualVoiceTuningFromVault()
        {
            if (!TryReadVirtualVoiceTuningFromVault(out VirtualVoiceTuningSnapshot tuning))
                return false;

            _virtualVoiceTuning = tuning;
            return true;
        }

        private bool TryReadVirtualVoiceTuningFromVault(out VirtualVoiceTuningSnapshot tuning)
        {
            IDataVault vault = _dataVault;
            tuning = default;
            if (vault == null ||
                !IsAudioVaultHandle(
                    in _virtualVoiceTuningHandle,
                    SpatialAudioVirtualVoiceTuningBufferId,
                    SystemID.Audio) ||
                !vault.TryReadOnlyHandle(
                    in _virtualVoiceTuningHandle,
                    out NativeArray<VirtualVoiceTuningSnapshot>.ReadOnly tuningRows) ||
                tuningRows.Length <= 0)
            {
                return false;
            }

            VirtualVoiceTuningSnapshot stored = tuningRows[0];
            tuning = VirtualVoiceTuningSnapshot.Sanitize(in stored);
            return true;
        }

        private bool WriteVirtualVoiceTuningToVault(in VirtualVoiceTuningSnapshot tuning)
        {
            IDataVault vault = _dataVault;
            NativeArray<VirtualVoiceTuningSnapshot> tuningRows = default;
            bool lockAcquired =
                vault != null &&
                IsAudioVaultHandle(
                    in _virtualVoiceTuningHandle,
                    SpatialAudioVirtualVoiceTuningBufferId,
                    SystemID.Audio) &&
                vault.TryAcquireWriteLock(
                    in _virtualVoiceTuningHandle,
                    SystemID.Audio,
                    out tuningRows);

            if (!lockAcquired ||
                !tuningRows.IsCreated ||
                tuningRows.Length <= 0)
            {
                if (lockAcquired)
                    vault.ReleaseWriteLock(in _virtualVoiceTuningHandle, SystemID.Audio);
                return false;
            }

            try
            {
                tuningRows[0] = tuning;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _virtualVoiceTuningHandle, SystemID.Audio);
            }
        }

        private void ReleaseTelemetryCaches()
        {
            CompleteVirtualVoiceSort();

            IDataVault vault = _dataVault;
            if (vault != null)
                vault.ReleaseOwnerBuffers(SystemID.Audio, out _);

            ClearVaultBackedTelemetryAliases();

            ReleaseAcousticRadarGridBuffer(ref _acousticRadarGridBufferA);
            ReleaseAcousticRadarGridBuffer(ref _acousticRadarGridBufferB);
            _activeAcousticRadarGridBuffer = null;
            _acousticRadarGridUploadIndex = 0;

            _delayedAudioIngress = null;
            _pendingDelayedAudioEvents = null;
            _audioEventQueue = null;
            ReleaseAudioClipHashMap();

            _delayedAudioIngressCount = 0;
            _delayedAudioIngressHead = 0;
            _pendingDelayedAudioEventCount = 0;
            _audioEventQueueCount = 0;
            _audioEventQueueDroppedCount = 0;
            _lastAudioEventQueueOverflowTelemetryFrame = -1;
            _lastAudioEventBadDataTelemetryFrame = -1;
            _audioEventQueueHead = 0;
            _virtualVoiceDroppedCount = 0;
            _virtualVoiceWriteCount = 0;
            _virtualVoiceSortCount = 0;
            _virtualVoiceDtoCount = 0;
            _virtualVoiceBlackBoxCursor = 0;
            _lastVirtualVoiceStatistics = default;
            _acousticPortalBlackBoxCursor = 0;
            _acousticPortalBlackBoxDumpPending = false;
        }

        private static void ReleaseAcousticRadarGridBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
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

        private int ResolveUnityAudioPriority(
            AudioClip clip,
            AudioMixerGroup resolvedGroup,
            float volume01,
            AudioLodTier lodTier)
        {
            byte routeFlags = ResolveClipRouteFlags(clip);
            int priority = UnityAudioPriorityDefaultWorld;

            if ((routeFlags & AudioClipRouteFlagLeviathanRoar) != 0)
            {
                priority = UnityAudioPriorityCritical;
            }
            else if ((routeFlags & AudioClipRouteFlagThreat) != 0 ||
                     (_threatGroup != null && resolvedGroup == _threatGroup))
            {
                priority = UnityAudioPriorityThreat;
            }
            else if ((routeFlags & AudioClipRouteFlagBed) != 0 ||
                     resolvedGroup == _bedGroup ||
                     resolvedGroup == _ambientGroup)
            {
                priority = UnityAudioPriorityAmbientBed;
            }

            if (lodTier >= AudioLodTier.Tier1Reduced)
                priority += UnityAudioPriorityProxyTierPenalty;

            if (volume01 <= 0.08f)
                priority += UnityAudioPriorityLowAudibilityPenalty;
            else if (volume01 >= 0.75f)
                priority -= UnityAudioPriorityHighAudibilityBonus;

            return math.clamp(priority, 0, 256);
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
            int frame = SystemDispatcher.CurrentFrameIndex;
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
            float scalar = SimulationSignalRoute.TimeDilationScalar;
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

            ISurfaceWeatherReadModel surfaceWeather = ResolveSurfaceWeatherDirector();
            if (surfaceWeather != null && !surfaceWeather.IsSurfaceSuppressed)
            {
                float weatherPressure = math.saturate(
                    surfaceWeather.CurrentPrecipitationIntensity * 0.72f +
                    surfaceWeather.CurrentElectricalActivity * 0.48f);
                target01 = math.max(target01, weatherPressure);
                switch (surfaceWeather.CurrentWeatherKindCode)
                {
                    case SurfaceWeatherKindCodes.HeavyRain:
                    case SurfaceWeatherKindCodes.ElectricalStorm:
                        target01 = math.max(target01, math.saturate(_globalWindHowlStormFloor));
                        break;
                }
            }

            return math.saturate(target01);
        }

        private bool ResolveGlobalWindHowlOccluded()
        {
            ISurfaceWeatherReadModel surfaceWeather = ResolveSurfaceWeatherDirector();
            if (surfaceWeather != null && surfaceWeather.IsLocallySheltered)
                return true;

            IAcousticZoneReadModel acousticZone = ResolveAcousticZone();
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
                _lastThreatBusDuckDb = float.PositiveInfinity;
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

            float duckDb = math.lerp(0f, ThreatBusDuckMaximumDb, _threatBusDuck01);
            if (math.abs(duckDb - _lastThreatBusDuckDb) <= 0.05f)
                return;

            mixer.SetFloat(_bedDuckDbParameter, duckDb);
            _lastThreatBusDuckDb = duckDb;
        }

        private void ApplyMasterDepthPressureLowPass(float listenerRuntimeY, float deltaTime)
        {
            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer == null || !_hasMasterDepthPressureLowPassCutoffParameter)
                return;

            float safeY = math.select(0f, listenerRuntimeY, math.isfinite(listenerRuntimeY));
            float pressure01 = math.saturate((safeY - MasterDepthPressureOpenY) * MasterDepthPressureYRangeInv);
            float quality01 = SmoothQuality01(math.min(
                SanitizeQuality01(_virtualVoiceQualityWeight),
                SanitizeQuality01(_cachedSpatialAudioQualityWeight01)));
            float pressureCurve01 = pressure01 * pressure01 * (3f - (2f * pressure01));
            float targetCutoffHz = math.exp(math.lerp(
                MasterDepthPressureCutoffOpenLog,
                MasterDepthPressureCutoffFullLog,
                pressureCurve01));

            if (!math.isfinite(_masterDepthPressureLowPassCutoffHz) ||
                _masterDepthPressureLowPassCutoffHz < MasterDepthPressureCutoffFullHertz ||
                _masterDepthPressureLowPassCutoffHz > MasterDepthPressureCutoffOpenHertz)
            {
                _masterDepthPressureLowPassCutoffHz = MasterDepthPressureCutoffOpenHertz;
            }

            float sharpness = math.lerp(
                MasterDepthPressureLowPassCompactSharpness,
                MasterDepthPressureLowPassSharpness,
                quality01);
            float blend = deltaTime > 0f
                ? FastDecayBlend(sharpness, deltaTime)
                : 1f;
            _masterDepthPressureLowPassCutoffHz = math.lerp(_masterDepthPressureLowPassCutoffHz, targetCutoffHz, blend);
            float cutoffHz = math.clamp(
                _masterDepthPressureLowPassCutoffHz,
                MasterDepthPressureCutoffFullHertz,
                MasterDepthPressureCutoffOpenHertz);

            if (math.abs(cutoffHz - _lastMasterDepthPressureLowPassCutoffHz) <= 1f)
                return;

            if (!mixer.SetFloat(_masterDepthPressureLowPassCutoffParameter, cutoffHz))
            {
                _hasMasterDepthPressureLowPassCutoffParameter = false;
                return;
            }

            _lastMasterDepthPressureLowPassCutoffHz = cutoffHz;
        }

        private void ResetMasterDepthPressureLowPass()
        {
            _masterDepthPressureLowPassCutoffHz = MasterDepthPressureCutoffOpenHertz;
            _lastMasterDepthPressureLowPassCutoffHz = -1f;

            if (string.IsNullOrWhiteSpace(_masterDepthPressureLowPassCutoffParameter))
                return;

            AudioMixer mixer = ResolveThreatDuckingMixer();
            if (mixer == null)
                return;

            _hasMasterDepthPressureLowPassCutoffParameter = mixer.SetFloat(
                _masterDepthPressureLowPassCutoffParameter,
                MasterDepthPressureCutoffOpenHertz);
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
            _hasNarrativeRadioBitCrushMixParameter = !string.IsNullOrWhiteSpace(_narrativeRadioBitCrushMixParameter);
            _hasNarrativeRadioPitchShiftCentsParameter = !string.IsNullOrWhiteSpace(_narrativeRadioPitchShiftCentsParameter);
            _hasBrownoutPitchMultiplierParameter = !string.IsNullOrWhiteSpace(_brownoutPitchMultiplierParameter);
            _hasMasterDepthPressureLowPassCutoffParameter = !string.IsNullOrWhiteSpace(_masterDepthPressureLowPassCutoffParameter);
        }

        private byte ResolveClipRouteFlags(AudioClip clip)
        {
            if (clip == null)
                return 0;

            int clipId = unchecked((int)EntityId.ToULong(clip.GetEntityId()));
            if (TryGetCachedClipRouteFlags(clipId, out byte routeFlags))
                return routeFlags;

            return ResolveAuthoredClipRouteFlags(clip);
        }

        private byte ResolveAuthoredClipRouteFlags(AudioClip clip)
        {
            byte routeFlags = 0;
            if (ContainsAuthoredClip(_threatRouteClips, clip))
                routeFlags |= AudioClipRouteFlagThreat;

            if (ContainsAuthoredClip(_bedRouteClips, clip))
                routeFlags |= AudioClipRouteFlagBed;

            if (ContainsAuthoredClip(_leviathanRoarRouteClips, clip))
                routeFlags |= AudioClipRouteFlagThreat | AudioClipRouteFlagLeviathanRoar;

            if (ContainsAuthoredClip(_bubbleRouteClips, clip))
                routeFlags |= AudioClipRouteFlagBubble;

            return routeFlags;
        }

        private void CacheAuthoredClipRouteOverridesCold()
        {
            CacheAuthoredClipRouteArrayCold(_threatRouteClips, AudioClipRouteFlagThreat);
            CacheAuthoredClipRouteArrayCold(_bedRouteClips, AudioClipRouteFlagBed);
            CacheAuthoredClipRouteArrayCold(_leviathanRoarRouteClips, AudioClipRouteFlagThreat | AudioClipRouteFlagLeviathanRoar);
            CacheAuthoredClipRouteArrayCold(_bubbleRouteClips, AudioClipRouteFlagBubble);
        }

        private void CacheAuthoredClipRouteArrayCold(AudioClip[] clips, byte routeFlags)
        {
            if (clips == null || routeFlags == 0)
                return;

            for (int i = 0; i < clips.Length; i++)
                CacheAuthoredClipRouteCold(clips[i], routeFlags);
        }

        private void CacheAuthoredClipRouteCold(AudioClip clip, byte routeFlags)
        {
            if (clip == null || routeFlags == 0)
                return;

            int clipId = unchecked((int)EntityId.ToULong(clip.GetEntityId()));
            if (TryGetCachedClipRouteFlags(clipId, out byte existingFlags))
                routeFlags |= existingFlags;

            CacheClipRouteFlags(clipId, routeFlags);
        }

        private static bool ContainsAuthoredClip(AudioClip[] clips, AudioClip clip)
        {
            if (clips == null || clip == null)
                return false;

            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == clip)
                    return true;
            }

            return false;
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
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins,
                    AcousticRadarBinCount,
                    out NativeArray<float> radialIntensityBins))
                return;

            try
            {
                for (int i = 0; i < radialIntensityBins.Length; i++)
                {
                    float energy = radialIntensityBins[i];
                    if (energy <= 0f)
                        continue;

                    float decayed = energy * AcousticRadarDecayFactorPerSlowTick;
                    radialIntensityBins[i] = decayed > AcousticRadarEnergyEpsilon ? decayed : 0f;
                }
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins);
            }
        }

        private void DecayAcousticRadarGrid()
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid,
                    AcousticRadarGridCellCount,
                    out NativeArray<float> gridEnergy))
                return;

            try
            {
                bool dirty = false;
                for (int i = 0; i < gridEnergy.Length; i++)
                {
                    float energy = gridEnergy[i];
                    if (energy <= 0f)
                        continue;

                    float decayed = energy * AcousticRadarDecayFactorPerSlowTick;
                    gridEnergy[i] = decayed > AcousticRadarEnergyEpsilon ? decayed : 0f;
                    dirty = true;
                }

                if (dirty)
                    _acousticRadarGridDirty = true;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid);
            }
        }

        private void ResetAcousticRadarBins()
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins,
                    AcousticRadarBinCount,
                    out NativeArray<float> radialIntensityBins))
                return;

            try
            {
                for (int i = 0; i < radialIntensityBins.Length; i++)
                    radialIntensityBins[i] = 0f;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins);
            }
        }

        private void ResetAcousticRadarGrid()
        {
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid,
                    AcousticRadarGridCellCount,
                    out NativeArray<float> gridEnergy))
                return;

            try
            {
                for (int i = 0; i < gridEnergy.Length; i++)
                    gridEnergy[i] = 0f;

                _acousticRadarGridDirty = true;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid);
            }
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
            if (listener == null || !(amplitude > 0f))
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
            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins,
                    AcousticRadarBinCount,
                    out NativeArray<float> radialIntensityBins))
            {
                return;
            }

            try
            {
                if ((uint)radialIndex < (uint)radialIntensityBins.Length)
                    radialIntensityBins[radialIndex] = math.max(radialIntensityBins[radialIndex], intensity);
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarIntensityBinsHandle,
                    BufferID.SpatialAudioRadarIntensityBins);
            }
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
                _radarNearestEmitterDistanceSq == null ||
                _radarNearestEmitterPositions == null ||
                _radarNearestEmitterAups == null ||
                _radarNearestEmitterAmplitudes == null ||
                _radarNearestEmitterRoots == null)
            {
                return;
            }

            if (!TryAcquireAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid,
                    AcousticRadarGridCellCount,
                    out NativeArray<float> gridEnergy))
            {
                return;
            }

            try
            {
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
                    if ((uint)cellIndex < (uint)gridEnergy.Length)
                    {
                        gridEnergy[cellIndex] += energy;
                        dirty = true;
                    }
                }

                if (dirty)
                    _acousticRadarGridDirty = true;
            }
            finally
            {
                ReleaseAudioVaultWriteBuffer(
                    in _acousticRadarGridHandle,
                    BufferID.SpatialAudioRadarGrid);
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
            if (!TryReadAcousticRadarGrid(out NativeArray<float>.ReadOnly gridEnergy) ||
                _acousticRadarGridUploadScratch == null)
                return;

            if (!_acousticRadarGridDirty)
                return;

            GraphicsBuffer writeBuffer = _acousticRadarGridUploadIndex == 0
                ? _acousticRadarGridBufferA
                : _acousticRadarGridBufferB;
            if (writeBuffer == null)
                return;

            int count = math.min(gridEnergy.Length, _acousticRadarGridUploadScratch.Length);
            count = math.min(count, writeBuffer.count);
            if (count <= 0)
                return;

            for (int i = 0; i < count; i++)
                _acousticRadarGridUploadScratch[i] = gridEnergy[i];

            GraphicsBufferUploadUtility.UploadArray(writeBuffer, _acousticRadarGridUploadScratch, count);
            _activeAcousticRadarGridBuffer = writeBuffer;
            _acousticRadarGridUploadIndex ^= 1;
            _acousticRadarGridDirty = false;
        }

        private void RefreshListenerCaveState(Transform listener, Vector3 listenerPosition)
        {
            ResetListenerCaveState();
            if (listener == null)
                return;

            if (_worldCaveDirector == null)
                return;

            int volumeCount = _worldCaveDirector.CopyActiveVolumesTo(_caveVolumeBuffer);
            HectonVoxelVolume sabineCandidateVolume = null;
            Bounds sabineCandidateLocalBounds = default;
            float sabineCandidateInterior01 = -1f;
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

        private Vector3 ResolveListenerAupVelocity(in AbsoluteUniversePosition listenerAup, float deltaTime)
        {
            if (!_hasPreviousListenerVelocityAup || deltaTime <= 0.0001f)
            {
                _previousListenerVelocityAup = listenerAup;
                _hasPreviousListenerVelocityAup = true;
                return Vector3.zero;
            }

            float deltaTimeInv = math.rcp(deltaTime);
            float3 delta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in listenerAup, in _previousListenerVelocityAup);
            _previousListenerVelocityAup = listenerAup;
            return new Vector3(delta.x, delta.y, delta.z) * deltaTimeInv;
        }

        private void UpdateListenerWaterDensityMul(float deltaTime)
        {
            IPlayerRuntimeContext playerContext = ResolvePlayerRuntimeContext();
            bool underwater = playerContext != null &&
                              playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                              (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.Underwater) != 0u;
            float target = underwater ? 1f : 0f;
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
            in AbsoluteUniversePosition sourceAup,
            in AbsoluteUniversePosition listenerAup,
            Vector3 listenerVelocity,
            float deltaTime,
            int currentFrame,
            NativeArray<AbsoluteUniversePosition> previousVelocityAups,
            NativeArray<int> previousVelocityAupFrames)
        {
            if (source == null ||
                _basePitches == null ||
                _smoothedDopplerRatios == null ||
                !previousVelocityAups.IsCreated ||
                !previousVelocityAupFrames.IsCreated ||
                sourceIndex < 0 ||
                sourceIndex >= _basePitches.Length ||
                sourceIndex >= previousVelocityAups.Length ||
                sourceIndex >= previousVelocityAupFrames.Length)
            {
                ResetManualDopplerPitch(sourceIndex, source);
                return;
            }

            Vector3 sourceVelocity = Vector3.zero;
            if (deltaTime > 0.0001f &&
                previousVelocityAupFrames[sourceIndex] >= 0)
            {
                float deltaTimeInv = math.rcp(deltaTime);
                AbsoluteUniversePosition previousSourceAup = previousVelocityAups[sourceIndex];
                float3 sourceDelta = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in previousSourceAup);
                sourceVelocity = new Vector3(sourceDelta.x, sourceDelta.y, sourceDelta.z) * deltaTimeInv;
            }

            previousVelocityAups[sourceIndex] = sourceAup;
            previousVelocityAupFrames[sourceIndex] = currentFrame;

            float3 listenerToSourceAup = AbsoluteUniversePosition.ToCameraRelativeFloat3(in sourceAup, in listenerAup);
            float distanceSq = math.lengthsq(listenerToSourceAup);
            float targetRatio = 1f;

            if (distanceSq > 0.0001f)
            {
                targetRatio = Hecton8.PureLogic.Systems.SubseaVehicleDopplerReverbShiftCalculator.Compute(
                    1f,
                    new System.Numerics.Vector3(listenerToSourceAup.x, listenerToSourceAup.y, listenerToSourceAup.z),
                    new System.Numerics.Vector3(sourceVelocity.x, sourceVelocity.y, sourceVelocity.z),
                    System.Numerics.Vector3.Zero,
                    new System.Numerics.Vector3(listenerVelocity.x, listenerVelocity.y, listenerVelocity.z),
                    SoundSpeedWaterMetersPerSecond
                );

                float3 direction = ResolveDominantAxisDirection(listenerToSourceAup);
                float relativeVelocity = math.dot((float3)(listenerVelocity - sourceVelocity), direction);
                float clampedRelativeVelocity = math.clamp(
                    relativeVelocity,
                    -SoundSpeedWaterMetersPerSecond * 0.9f,
                    SoundSpeedWaterMetersPerSecond * 0.9f);

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

            ResetManualDopplerPitch(sourceIndex, source);
        }

        private void ResetManualDopplerPitch(int sourceIndex, AudioSource source)
        {
            if (source == null ||
                _basePitches == null ||
                _smoothedDopplerRatios == null ||
                sourceIndex < 0 ||
                sourceIndex >= _basePitches.Length ||
                sourceIndex >= _smoothedDopplerRatios.Length)
            {
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
                Hecton8.Core.H8Debug.Log("[SpatialAudioManager] Helmet/UI pool full. Evicting quietest source.", this);
            }
#endif

            return quietestIndex;
        }

        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ
        //  EDITOR VALIDATION
        // ГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђГўвЂўВђ

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static bool ShouldEmitEditorThrottledLog(ref float nextLogTime, float intervalSeconds)
        {
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
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
        /// ГђвЂ™ГђВёГђВ·Г‘Ж’ГђВ°ГђВ»ГђВёГђВ·ГђВ°Г‘вЂ ГђВёГ‘ВЏ ГђВїГ‘Ж’ГђВ»ГђВ° ГђВІ Scene View ГђВґГђВ»Г‘ВЏ ГђВѕГ‘вЂљГђВ»ГђВ°ГђВґГђВєГђВё.
        /// ГђЕёГђВѕГђВєГђВ°ГђВ·Г‘вЂ№ГђВІГђВ°ГђВµГ‘вЂљ ГђВїГђВѕГђВ·ГђВёГ‘вЂ ГђВёГђВё ГђВ°ГђВєГ‘вЂљГђВёГђВІГђВЅГ‘вЂ№Г‘вЂ¦ ГђВёГ‘ВЃГ‘вЂљГђВѕГ‘вЂЎГђВЅГђВёГђВєГђВѕГђВІ.
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
            if (!Application.isPlaying || !IsInitialized)
                return;

            Vector3 listenerPosition = _listenerTransform != null ? _listenerTransform.position : transform.position;
            bool hasSelections = TryReadAudioVaultBuffer(
                in _virtualVoiceSelectionsHandle,
                BufferID.SpatialAudioVirtualVoiceSelections,
                MaxVirtualPhysicalVoices,
                out NativeArray<VirtualVoiceSelection>.ReadOnly selections);
            int selectedCount = hasSelections
                ? math.clamp(_lastVirtualVoiceStatistics.ActivePhysicalVoices, 0, selections.Length)
                : 0;
            bool drewAcousticDtoLane = DrawSelectedAcousticSourceDtoGizmos(listenerPosition);
            for (int i = 0; hasSelections && !drewAcousticDtoLane && i < selectedCount; i++)
            {
                VirtualVoiceSelection selection = selections[i];
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

            if (!TryReadAudioVaultBuffer(
                    in _virtualVoiceSortPoolHandle,
                    SpatialAudioVirtualVoiceSortPoolBufferId,
                    MaxVirtualVoiceCapacity,
                    out NativeArray<VirtualVoice>.ReadOnly virtualVoiceSortPool))
            {
                return;
            }

            int virtualCount = math.min(_virtualVoiceSortCount, MaxVirtualVoiceCapacity);
            for (int i = selectedCount; i < virtualCount; i++)
            {
                VirtualVoice voice = virtualVoiceSortPool[i];
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
            if (_acousticOcclusionOutputCount <= 0 ||
                !TryReadAudioVaultBuffer(
                    in _acousticSelectedSourcePoolHandle,
                    SpatialAudioAcousticSelectedSourcePoolBufferId,
                    MaxVirtualPhysicalVoices,
                    out NativeArray<AcousticSourceDTO>.ReadOnly selectedSourcePool))
            {
                return false;
            }

            int count = math.min(_acousticOcclusionOutputCount, selectedSourcePool.Length);
            for (int i = 0; i < count; i++)
            {
                AcousticSourceDTO source = selectedSourcePool[i];
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
    
        #region JulesLink_AudioDistanceAttenuationCurveCalculator
        private static void JulesLink_AudioDistanceAttenuationCurveCalculator() { _ = typeof(Hecton8.PureLogic.Systems.AudioDistanceAttenuationCurveCalculator); }
        #endregion
}

    /// <summary>
    /// Caption request wrapper for contextual spatial-audio captions.
    /// Producers pass stable caption hashes; UI resolves the display string at the presentation edge.
    /// </summary>
    public readonly struct AudioCaptionRequest
    {
        public AudioCaptionRequest(uint captionHashId, Vector3 worldPosition, float durationSeconds, float intensity)
        {
            CaptionHashId = captionHashId;
            WorldPosition = worldPosition;
            bool hasWorldAup = TryResolveAupFromRuntimeOrigin(worldPosition, out AbsoluteUniversePosition worldAup);
            WorldAup = worldAup;
            _hasWorldAup = hasWorldAup ? (byte)1 : (byte)0;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
        }

        public AudioCaptionRequest(uint captionHashId, Vector3 worldPosition, in AbsoluteUniversePosition worldAup, float durationSeconds, float intensity)
        {
            CaptionHashId = captionHashId;
            WorldPosition = worldPosition;
            WorldAup = worldAup;
            _hasWorldAup = 1;
            DurationSeconds = durationSeconds;
            Intensity = intensity;
        }

        /// <summary>Stable caption text/hash id. Zero means invalid/legacy-only.</summary>
        public uint CaptionHashId { get; }

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

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
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
        private byte _pad0;
        [FieldOffset(85)]
        private byte _pad1;
        [FieldOffset(86)]
        private byte _pad2;
        [FieldOffset(87)]
        private byte _pad3;
        [FieldOffset(88)]
        private byte _pad4;
        [FieldOffset(89)]
        private byte _pad5;
        [FieldOffset(90)]
        private byte _pad6;
        [FieldOffset(91)]
        private byte _pad7;
        [FieldOffset(92)]
        private byte _pad8;
        [FieldOffset(93)]
        private byte _pad9;
        [FieldOffset(94)]
        private byte _pad10;
        [FieldOffset(95)]
        private byte _pad11;
        [FieldOffset(96)]
        private byte _pad12;
        [FieldOffset(97)]
        private byte _pad13;
        [FieldOffset(98)]
        private byte _pad14;
        [FieldOffset(99)]
        private byte _pad15;
        [FieldOffset(100)]
        private byte _pad16;
        [FieldOffset(101)]
        private byte _pad17;
        [FieldOffset(102)]
        private byte _pad18;
        [FieldOffset(103)]
        private byte _pad19;
        [FieldOffset(104)]
        private byte _pad20;
        [FieldOffset(105)]
        private byte _pad21;
        [FieldOffset(106)]
        private byte _pad22;
        [FieldOffset(107)]
        private byte _pad23;
        [FieldOffset(108)]
        private byte _pad24;
        [FieldOffset(109)]
        private byte _pad25;
        [FieldOffset(110)]
        private byte _pad26;
        [FieldOffset(111)]
        private byte _pad27;
        [FieldOffset(112)]
        private byte _pad28;
        [FieldOffset(113)]
        private byte _pad29;
        [FieldOffset(114)]
        private byte _pad30;
        [FieldOffset(115)]
        private byte _pad31;
        [FieldOffset(116)]
        private byte _pad32;
        [FieldOffset(117)]
        private byte _pad33;
        [FieldOffset(118)]
        private byte _pad34;
        [FieldOffset(119)]
        private byte _pad35;
        [FieldOffset(120)]
        private byte _pad36;
        [FieldOffset(121)]
        private byte _pad37;
        [FieldOffset(122)]
        private byte _pad38;
        [FieldOffset(123)]
        private byte _pad39;
        [FieldOffset(124)]
        private byte _pad40;
        [FieldOffset(125)]
        private byte _pad41;
        [FieldOffset(126)]
        private byte _pad42;
        [FieldOffset(127)]
        private byte _pad43;
    }

    /// <summary>
    /// Fixed-ring main-thread event bus for spatial-audio captions.
    /// Audio systems publish semantic cue text here; HUD overlays render it.
    /// </summary>
    public static class AudioCaptionEvents
    {
        private const int PendingEventCapacity = 32;
        private const ushort CaptionRequestedEventType = 1;
        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("AudioCaptionEvents"));
        public const uint LowPowerCaptionHash = VwsCaptionFallbackCatalog.LowPowerCaptionHash;
        public const uint LifeSupportCaptionHash = VwsCaptionFallbackCatalog.LifeSupportCaptionHash;
        public const uint MultiFailureCaptionHash = VwsCaptionFallbackCatalog.MultiFailureCaptionHash;
        public const uint EmergencyDangerCaptionHash = VwsCaptionFallbackCatalog.EmergencyDangerCaptionHash;
        public const uint AbandonShipCaptionHash = VwsCaptionFallbackCatalog.AbandonShipCaptionHash;
        public const uint HostileDroneCaptionHash = VwsCaptionFallbackCatalog.HostileDroneCaptionHash;
        public const uint OxygenLowCaptionHash = VwsCaptionFallbackCatalog.OxygenLowCaptionHash;
        public const uint OxygenCriticalCaptionHash = VwsCaptionFallbackCatalog.OxygenCriticalCaptionHash;
        public const uint HullBreachCaptionHash = VwsCaptionFallbackCatalog.HullBreachCaptionHash;
        public const uint PressureHighCaptionHash = VwsCaptionFallbackCatalog.PressureHighCaptionHash;
        public const uint ThermalStressCaptionHash = VwsCaptionFallbackCatalog.ThermalStressCaptionHash;

        private static readonly AudioCaptionPayload[] _pendingEvents = new AudioCaptionPayload[PendingEventCapacity]; // COLD ALLOC: AudioCaptionPayload[32] - deferred spatial audio caption lane - owner: AudioCaptionEvents
        private static readonly AudioCaptionPayload[] _nextFrameEvents = new AudioCaptionPayload[PendingEventCapacity]; // COLD ALLOC: AudioCaptionPayload[32] - next-frame spatial audio caption lane - owner: AudioCaptionEvents
        private static int _activeConsumerCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _pendingEventHead;
        private static int _nextFrameEventHead;
        private static int _lastOverflowWarningFrame = -1;
        private static bool _isDispatching;

        /// <summary>Number of caption payloads waiting for late-frame dispatch.</summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Array.Clear(_pendingEvents, 0, _pendingEvents.Length);
            Array.Clear(_nextFrameEvents, 0, _nextFrameEvents.Length);
            _activeConsumerCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _pendingEventHead = 0;
            _nextFrameEventHead = 0;
            _lastOverflowWarningFrame = -1;
            _isDispatching = false;
        }

        /// <summary>Registers one UI-side pull consumer for audio captions.</summary>
        public static void RegisterConsumer()
        {
            if (!Application.isPlaying)
                return;

            if (_activeConsumerCount < int.MaxValue)
                _activeConsumerCount++;
        }

        /// <summary>Unregisters one UI-side pull consumer for audio captions.</summary>
        public static void UnregisterConsumer()
        {
            if (_activeConsumerCount > 0)
                _activeConsumerCount--;

            if (_activeConsumerCount <= 0)
                DropQueuedCaptionPayloads();
        }

        /// <summary>Dispatcher compatibility hook; active UI overlays drain captions from their LateFrameTick.</summary>
        public static void FlushPending()
        {
            if (_activeConsumerCount <= 0)
                DropQueuedCaptionPayloads();
        }

        /// <summary>Consumes one queued caption payload into a stack/local request DTO.</summary>
        public static bool ConsumeNextPendingCaption(out AudioCaptionRequest request)
        {
            request = default;
            if (_activeConsumerCount <= 0)
            {
                DropQueuedCaptionPayloads();
                return false;
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventHead = 0;
                PromoteNextFrameEvents();
                if (_pendingEventCount <= 0)
                    return false;
            }

            int scanBudget = _pendingEventCount;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && _pendingEventCount > 0)
                {
                    int readIndex = _pendingEventHead;
                    AudioCaptionPayload payload = _pendingEvents[readIndex];
                    _pendingEvents[readIndex] = default;
                    _pendingEventHead = (_pendingEventHead + 1) % _pendingEvents.Length;
                    _pendingEventCount--;
                    bool hasRequest = TryBuildCaptionRequest(in payload, out request);
                    if (_pendingEventCount <= 0)
                    {
                        _pendingEventHead = 0;
                        PromoteNextFrameEvents();
                    }

                    if (hasRequest)
                        return true;
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (_pendingEventCount <= 0)
            {
                _pendingEventHead = 0;
                PromoteNextFrameEvents();
            }

            return false;
        }

        /// <summary>
        /// Queues a caption request using a stable caption hash.
        /// </summary>
        public static bool TryRaise(in AudioCaptionRequest request)
        {
            if (!CanQueueCaptionHash(request.CaptionHashId))
                return false;

            return EnqueueCaptionRequest(in request);
        }

        public static bool TryRaiseHash(uint captionHashId, Vector3 worldPosition, float durationSeconds, float intensity)
        {
            if (!CanQueueCaptionHash(captionHashId))
                return false;

            AudioCaptionRequest request = new AudioCaptionRequest(captionHashId, worldPosition, durationSeconds, intensity);
            return EnqueueCaptionRequest(in request);
        }

        public static bool TryRaiseHash(uint captionHashId, Vector3 worldPosition, in AbsoluteUniversePosition worldAup, float durationSeconds, float intensity)
        {
            if (!CanQueueCaptionHash(captionHashId))
                return false;

            AudioCaptionRequest request = new AudioCaptionRequest(captionHashId, worldPosition, in worldAup, durationSeconds, intensity);
            return EnqueueCaptionRequest(in request);
        }


        private static bool CanQueueCaptionHash(uint captionHashId)
        {
            return Application.isPlaying &&
                   _activeConsumerCount > 0 &&
                   captionHashId != 0u &&
                   HasCaptionText(captionHashId);
        }

        private static bool HasCaptionText(uint captionHashId)
        {
            if (captionHashId == 0u)
                return false;

            if (LocRegistry.TryGetLocalizedSpan(captionHashId, out ReadOnlySpan<byte> localizedUtf8) &&
                localizedUtf8.Length > 0)
                return true;

            return TryResolveCaptionTextSpan(captionHashId, out ReadOnlySpan<char> captionText) &&
                   captionText.Length > 0;
        }

        private static bool EnqueueCaptionRequest(in AudioCaptionRequest request)
        {
            return Enqueue(new AudioCaptionPayload
            {
                WorldPosition = request.WorldPosition,
                WorldAup = request.WorldAup,
                DurationSeconds = request.DurationSeconds,
                Intensity = request.Intensity,
                CaptionHashId = request.CaptionHashId,
                ReferenceSlot = -1,
                EventType = CaptionRequestedEventType,
                Reserved = 0,
                HasWorldAup = request.HasWorldAup ? (byte)1 : (byte)0,
                ReservedByte0 = 0,
                ReservedShort0 = 0
            });
        }

        private static bool Enqueue(in AudioCaptionPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            if (_isDispatching)
            {
                int writeIndex = (_nextFrameEventHead + _nextFrameEventCount) % _nextFrameEvents.Length;
                _nextFrameEvents[writeIndex] = payload;
                _nextFrameEventCount++;
            }
            else
            {
                int writeIndex = (_pendingEventHead + _pendingEventCount) % _pendingEvents.Length;
                _pendingEvents[writeIndex] = payload;
                _pendingEventCount++;
            }

            return true;
        }

        private static void PromoteNextFrameEvents()
        {
            if (_nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _pendingEventCount < PendingEventCapacity)
            {
                int readIndex = _nextFrameEventHead;
                AudioCaptionPayload payload = _nextFrameEvents[readIndex];
                _nextFrameEvents[readIndex] = default;
                _nextFrameEventHead = (_nextFrameEventHead + 1) % _nextFrameEvents.Length;
                _nextFrameEventCount--;
                int writeIndex = (_pendingEventHead + _pendingEventCount) % _pendingEvents.Length;
                _pendingEvents[writeIndex] = payload;
                _pendingEventCount++;
            }

            if (_nextFrameEventCount == 0)
                _nextFrameEventHead = 0;
        }

        private static void DropQueuedCaptionPayloads()
        {
            if (_pendingEventCount <= 0 && _nextFrameEventCount <= 0)
                return;

            Array.Clear(_pendingEvents, 0, _pendingEvents.Length);
            Array.Clear(_nextFrameEvents, 0, _nextFrameEvents.Length);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _pendingEventHead = 0;
            _nextFrameEventHead = 0;
        }

        private static bool TryBuildCaptionRequest(in AudioCaptionPayload payload, out AudioCaptionRequest request)
        {
            request = default;
            if (payload.EventType != CaptionRequestedEventType ||
                payload.CaptionHashId == 0u)
            {
                return false;
            }

            AbsoluteUniversePosition worldAup = payload.WorldAup;
            request = payload.HasWorldAup != 0
                ? new AudioCaptionRequest(
                    payload.CaptionHashId,
                    payload.WorldPosition,
                    in worldAup,
                    payload.DurationSeconds,
                    payload.Intensity)
                : new AudioCaptionRequest(
                    payload.CaptionHashId,
                    payload.WorldPosition,
                    payload.DurationSeconds,
                    payload.Intensity);
            return true;
        }

        public static bool TryResolveCaptionTextSpan(uint captionHashId, out ReadOnlySpan<char> captionText)
        {
            return VwsCaptionFallbackCatalog.TryResolveCaptionTextSpan(captionHashId, out captionText);
        }

        public static bool TryWriteCaptionText(
            uint captionHashId,
            Span<char> destination,
            out int displayLength,
            out int sourceLength,
            out bool localized)
        {
            displayLength = 0;
            sourceLength = 0;
            localized = false;

            if (captionHashId == 0u || destination.Length <= 0)
                return false;

            if (LocRegistry.TryGetLocalizedSpan(captionHashId, out ReadOnlySpan<byte> localizedUtf8) &&
                localizedUtf8.Length > 0)
            {
                if (LocRegistry.TryWriteKnownLocalizedSpanFromUtf8(captionHashId, localizedUtf8, destination, out displayLength) &&
                    displayLength > 0)
                {
                    localized = true;
                    sourceLength = math.max(displayLength, localizedUtf8.Length);
                    return true;
                }

                displayLength = 0;
                sourceLength = 0;
            }

            if (!TryResolveCaptionTextSpan(captionHashId, out ReadOnlySpan<char> fallbackText) ||
                fallbackText.Length == 0)
                return false;

            sourceLength = fallbackText.Length;
            displayLength = math.min(fallbackText.Length, destination.Length);
            bool truncated = fallbackText.Length > destination.Length && displayLength >= 3;
            for (int i = 0; i < displayLength; i++)
                destination[i] = truncated && i >= displayLength - 3 ? '.' : fallbackText[i];

            return displayLength > 0;
        }

        private static void ReportOverflowOncePerFrame()
        {
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
        }
    }
}
