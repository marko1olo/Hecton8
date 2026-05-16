#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Audio;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Enforces HECTON-8 first-party audio import residency policy.
    /// </summary>
    internal sealed class AudioImportDictator : AssetPostprocessor
    {
        internal const string ProjectAudioRoot = "Assets/_Project/Audio";
        internal const long PreloadRamBudgetBytes = 50L * 1024L * 1024L;

        private const float ShortClipSeconds = 2.0f;
        private const float StreamingClipSeconds = 5.0f;
        private const int MusicSampleRate = 44100;
        private const int RuntimeSampleRate = 22050;
        private const float MusicVorbisQuality = 0.70f;
        private const float AmbientVorbisQuality = 0.45f;
        private const float DialogueVorbisQuality = 0.28f;
        private const string ReimportGuardPrefix = "AudioImportDictator.ReimportGuard.";
        private const string LogUnstable = "[AudioImportDictator:0xA1D10001] Import policy remained unstable after reimport.";

        /// <summary>
        /// Runs after older generic postprocessors so this dictator is the final first-party audio authority.
        /// </summary>
        public override int GetPostprocessOrder()
        {
            return 2000;
        }

        private void OnPreprocessAudio()
        {
            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null || !IsProjectAudioAsset(assetPath))
                return;

            ApplyPolicy(importer, assetPath, -1f);
        }

        private void OnPostprocessAudio(AudioClip clip)
        {
            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null || clip == null || !IsProjectAudioAsset(assetPath))
                return;

            string guardKey = ReimportGuardPrefix + assetPath;
            bool guardArmed = SessionState.GetBool(guardKey, false);
            bool changed = ApplyPolicy(importer, assetPath, clip.length);
            if (!changed)
            {
                SessionState.SetBool(guardKey, false);
                return;
            }

            if (guardArmed)
            {
                SessionState.SetBool(guardKey, false);
                Debug.LogError(LogUnstable);
                return;
            }

            SessionState.SetBool(guardKey, true);
            importer.SaveAndReimport();
        }

        internal static bool ApplyPolicy(AudioImporter importer, string assetPath, float clipLengthSeconds)
        {
            if (importer == null || !IsProjectAudioAsset(assetPath))
                return false;

            string normalizedPath = NormalizePath(assetPath);
            AudioResidencyDomain domain = ResolveDomain(normalizedPath);
            bool dialogue = IsDialoguePath(normalizedPath);
            bool spatial3D = IsSpatialized3DPath(normalizedPath, domain, dialogue);
            AudioClipLoadType loadType = ResolveLoadType(domain, clipLengthSeconds);
            AudioCompressionFormat compressionFormat = ResolveCompressionFormat(domain, dialogue, clipLengthSeconds, loadType);
            int sampleRate = ResolveSampleRate(domain, dialogue);
            float quality = ResolveVorbisQuality(domain, dialogue);
            bool preload = ShouldPreloadAudio(domain, loadType);
            bool backgroundLoad = loadType != AudioClipLoadType.DecompressOnLoad;

            bool changed = false;
            if (importer.forceToMono != spatial3D)
            {
                importer.forceToMono = spatial3D;
                changed = true;
            }

            if (importer.preloadAudioData != preload)
            {
                importer.preloadAudioData = preload;
                changed = true;
            }

            if (importer.loadInBackground != backgroundLoad)
            {
                importer.loadInBackground = backgroundLoad;
                changed = true;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType != loadType)
            {
                settings.loadType = loadType;
                changed = true;
            }

            if (settings.compressionFormat != compressionFormat)
            {
                settings.compressionFormat = compressionFormat;
                changed = true;
            }

            if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                changed = true;
            }

            if (settings.sampleRateOverride != sampleRate)
            {
                settings.sampleRateOverride = sampleRate;
                changed = true;
            }

            if (compressionFormat == AudioCompressionFormat.Vorbis &&
                Math.Abs(settings.quality - quality) > 0.001f)
            {
                settings.quality = quality;
                changed = true;
            }

            if (changed)
                importer.defaultSampleSettings = settings;

            return changed;
        }

        internal static List<string> CollectProjectAudioClipPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectAudioRoot });
            List<string> paths = new List<string>(guids.Length);
            HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsProjectAudioAsset(path) || !unique.Add(path))
                    continue;

                paths.Add(path);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        internal static AudioResidencyDomain ResolveDomain(string normalizedPath)
        {
            if (ContainsToken(normalizedPath, "/music") ||
                ContainsToken(normalizedPath, "music for game") ||
                ContainsToken(normalizedPath, "stinger") ||
                ContainsToken(normalizedPath, "main_menu"))
            {
                return AudioResidencyDomain.Music;
            }

            if (ContainsToken(normalizedPath, "/ui/") ||
                ContainsToken(normalizedPath, "interface") ||
                ContainsToken(normalizedPath, "visor") ||
                ContainsToken(normalizedPath, "pda") ||
                ContainsToken(normalizedPath, "menu"))
            {
                return AudioResidencyDomain.Interface;
            }

            if (ContainsToken(normalizedPath, "creature") ||
                ContainsToken(normalizedPath, "fauna") ||
                ContainsToken(normalizedPath, "leviathan") ||
                ContainsToken(normalizedPath, "predator") ||
                ContainsToken(normalizedPath, "roar"))
            {
                return AudioResidencyDomain.Creatures;
            }

            if (ContainsToken(normalizedPath, "footstep") ||
                ContainsToken(normalizedPath, "movement") ||
                ContainsToken(normalizedPath, "breathing") ||
                ContainsToken(normalizedPath, "thruster") ||
                ContainsToken(normalizedPath, "tool") ||
                ContainsToken(normalizedPath, "laser") ||
                ContainsToken(normalizedPath, "welder") ||
                ContainsToken(normalizedPath, "cutter"))
            {
                return AudioResidencyDomain.Player;
            }

            return AudioResidencyDomain.Environment;
        }

        internal static long EstimatePreloadBytes(string assetPath, AudioImporter importer, AudioClip clip)
        {
            if (importer == null || clip == null || !importer.preloadAudioData)
                return 0L;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming)
                return 0L;

            long decodedBytes = Math.Max(0L, (long)clip.samples * Math.Max(1, clip.channels) * 2L);
            if (settings.loadType == AudioClipLoadType.DecompressOnLoad)
                return decodedBytes;

            long fileBytes = GetFileSizeBytes(assetPath);
            return Math.Max(fileBytes, decodedBytes / 8L);
        }

        internal static bool IsProjectAudioAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = NormalizePath(path);
            return normalizedPath.StartsWith(ProjectAudioRoot, StringComparison.OrdinalIgnoreCase) &&
                   IsSupportedAudioExtension(normalizedPath);
        }

        internal static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
        }

        private static AudioClipLoadType ResolveLoadType(AudioResidencyDomain domain, float clipLengthSeconds)
        {
            if (clipLengthSeconds > StreamingClipSeconds)
                return AudioClipLoadType.Streaming;

            if (clipLengthSeconds >= 0f && clipLengthSeconds < ShortClipSeconds)
                return AudioClipLoadType.DecompressOnLoad;

            if (domain == AudioResidencyDomain.Music)
                return AudioClipLoadType.Streaming;

            return AudioClipLoadType.CompressedInMemory;
        }

        private static AudioCompressionFormat ResolveCompressionFormat(
            AudioResidencyDomain domain,
            bool dialogue,
            float clipLengthSeconds,
            AudioClipLoadType loadType)
        {
            if (dialogue || domain == AudioResidencyDomain.Music || domain == AudioResidencyDomain.Environment)
                return AudioCompressionFormat.Vorbis;

            if (loadType == AudioClipLoadType.Streaming || clipLengthSeconds >= ShortClipSeconds)
                return AudioCompressionFormat.Vorbis;

            return AudioCompressionFormat.ADPCM;
        }

        private static int ResolveSampleRate(AudioResidencyDomain domain, bool dialogue)
        {
            if (domain == AudioResidencyDomain.Music)
                return MusicSampleRate;

            if (dialogue)
                return RuntimeSampleRate;

            return RuntimeSampleRate;
        }

        private static float ResolveVorbisQuality(AudioResidencyDomain domain, bool dialogue)
        {
            if (dialogue)
                return DialogueVorbisQuality;

            if (domain == AudioResidencyDomain.Music)
                return MusicVorbisQuality;

            return AmbientVorbisQuality;
        }

        private static bool ShouldPreloadAudio(AudioResidencyDomain domain, AudioClipLoadType loadType)
        {
            if (loadType != AudioClipLoadType.DecompressOnLoad)
                return false;

            return domain == AudioResidencyDomain.Player ||
                   domain == AudioResidencyDomain.Creatures ||
                   domain == AudioResidencyDomain.Interface;
        }

        private static bool IsSpatialized3DPath(string normalizedPath, AudioResidencyDomain domain, bool dialogue)
        {
            if (dialogue || domain == AudioResidencyDomain.Music || domain == AudioResidencyDomain.Interface)
                return false;

            if (ContainsToken(normalizedPath, "2d") ||
                ContainsToken(normalizedPath, "helmet") ||
                ContainsToken(normalizedPath, "voice") ||
                ContainsToken(normalizedPath, "/vo/"))
            {
                return false;
            }

            return true;
        }

        private static bool IsDialoguePath(string normalizedPath)
        {
            return ContainsToken(normalizedPath, "/vo/") ||
                   ContainsToken(normalizedPath, "dialogue") ||
                   ContainsToken(normalizedPath, "dialog") ||
                   ContainsToken(normalizedPath, "oshino") ||
                   ContainsToken(normalizedPath, "voice");
        }

        private static bool ContainsToken(string path, string token)
        {
            return !string.IsNullOrEmpty(path) &&
                   path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSupportedAudioExtension(string normalizedPath)
        {
            return normalizedPath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(".flac", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(".aif", StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.EndsWith(".aiff", StringComparison.OrdinalIgnoreCase);
        }

        private static long GetFileSizeBytes(string assetPath)
        {
            string fullPath = Path.GetFullPath(assetPath);
            return File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0L;
        }
    }

    /// <summary>
    /// Build gate that rejects first-party audio preloads above the residency budget.
    /// </summary>
    internal sealed class AudioRamBudgetBuildGate : IPreprocessBuildWithReport
    {
        private struct AudioBudgetItem
        {
            public string Path;
            public long Bytes;
        }

        public int callbackOrder => -1200;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidatePreloadedAudioBudget(true);
        }

        [MenuItem("Hecton/Validation/Audio/Validate Preloaded Audio Budget", priority = 410)]
        internal static void ValidatePreloadedAudioBudgetMenu()
        {
            ValidatePreloadedAudioBudget(false);
        }

        internal static void ValidatePreloadedAudioBudget(bool failBuild)
        {
            List<string> paths = AudioImportDictator.CollectProjectAudioClipPaths();
            List<AudioBudgetItem> offenders = new List<AudioBudgetItem>(paths.Count);
            long totalBytes = 0L;

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                long bytes = AudioImportDictator.EstimatePreloadBytes(path, importer, clip);
                if (bytes <= 0L)
                    continue;

                totalBytes += bytes;
                offenders.Add(new AudioBudgetItem
                {
                    Path = path,
                    Bytes = bytes
                });
            }

            if (totalBytes <= AudioImportDictator.PreloadRamBudgetBytes)
                return;

            offenders.Sort(CompareBudgetItemsDescending);
            string reportText = BuildFailureReport(totalBytes, offenders);
            Debug.LogError(reportText);

            if (failBuild)
                throw new BuildFailedException(reportText);
        }

        private static int CompareBudgetItemsDescending(AudioBudgetItem left, AudioBudgetItem right)
        {
            int byteCompare = right.Bytes.CompareTo(left.Bytes);
            return byteCompare != 0 ? byteCompare : string.Compare(left.Path, right.Path, StringComparison.Ordinal);
        }

        private static string BuildFailureReport(long totalBytes, List<AudioBudgetItem> offenders)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[AudioRamBudgetBuildGate:0xA1D10002] Preloaded audio RAM exceeds 50 MB. Build aborted.");
            builder.Append("Total Preloaded Audio: ");
            builder.Append(totalBytes / (1024f * 1024f));
            builder.AppendLine(" MB");
            builder.AppendLine("Offending files:");

            int limit = Math.Min(offenders.Count, 64);
            for (int i = 0; i < limit; i++)
            {
                AudioBudgetItem item = offenders[i];
                builder.Append(" - ");
                builder.Append(item.Bytes / (1024f * 1024f));
                builder.Append(" MB | ");
                builder.AppendLine(item.Path);
            }

            return builder.ToString();
        }
    }
}
#endif
