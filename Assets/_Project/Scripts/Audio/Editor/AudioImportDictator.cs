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
        internal struct AudioImportPolicy
        {
            public AudioResidencyDomain Domain;
            public bool Dialogue;
            public bool Spatial3D;
            public AudioClipLoadType LoadType;
            public AudioCompressionFormat CompressionFormat;
            public int SampleRate;
            public float Quality;
            public bool Preload;
            public bool BackgroundLoad;
        }

        internal const string ProjectAudioRoot = "Assets/_Project/Audio";
        internal const long PreloadRamBudgetBytes = 50L * 1024L * 1024L;

        private const float ShortClipSeconds = 2.0f;
        private const float StreamingClipSeconds = 5.0f;
        private const int MusicSampleRate = 44100;
        private const int RuntimeSampleRate = 22050;
        private const int DialogueSampleRate = 16000;
        private const float MusicVorbisQuality = 0.70f;
        private const float AmbientVorbisQuality = 0.45f;
        private const float DialogueVorbisQuality = 0.22f;
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

            AudioImportPolicy policy = ResolvePolicy(assetPath, clipLengthSeconds);

            bool changed = false;
            if (importer.forceToMono != policy.Spatial3D)
            {
                importer.forceToMono = policy.Spatial3D;
                changed = true;
            }

            if (importer.preloadAudioData != policy.Preload)
            {
                importer.preloadAudioData = policy.Preload;
                changed = true;
            }

            if (importer.loadInBackground != policy.BackgroundLoad)
            {
                importer.loadInBackground = policy.BackgroundLoad;
                changed = true;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType != policy.LoadType)
            {
                settings.loadType = policy.LoadType;
                changed = true;
            }

            if (settings.compressionFormat != policy.CompressionFormat)
            {
                settings.compressionFormat = policy.CompressionFormat;
                changed = true;
            }

            if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                changed = true;
            }

            if (settings.sampleRateOverride != policy.SampleRate)
            {
                settings.sampleRateOverride = policy.SampleRate;
                changed = true;
            }

            if (policy.CompressionFormat == AudioCompressionFormat.Vorbis &&
                Math.Abs(settings.quality - policy.Quality) > 0.001f)
            {
                settings.quality = policy.Quality;
                changed = true;
            }

            if (changed)
                importer.defaultSampleSettings = settings;

            return changed;
        }

        internal static AudioImportPolicy ResolvePolicy(string assetPath, float clipLengthSeconds)
        {
            string normalizedPath = NormalizePath(assetPath);
            AudioResidencyDomain domain = ResolveDomain(normalizedPath);
            bool dialogue = IsDialoguePath(normalizedPath);
            AudioClipLoadType loadType = ResolveLoadType(domain, clipLengthSeconds);

            return new AudioImportPolicy
            {
                Domain = domain,
                Dialogue = dialogue,
                Spatial3D = IsSpatialized3DPath(normalizedPath, domain, dialogue),
                LoadType = loadType,
                CompressionFormat = ResolveCompressionFormat(domain, dialogue, clipLengthSeconds, loadType),
                SampleRate = ResolveSampleRate(domain, dialogue),
                Quality = ResolveVorbisQuality(domain, dialogue),
                Preload = ShouldPreloadAudio(domain, loadType),
                BackgroundLoad = loadType != AudioClipLoadType.DecompressOnLoad
            };
        }

        internal static bool IsPolicyCompliant(
            AudioImporter importer,
            string assetPath,
            float clipLengthSeconds,
            StringBuilder builder)
        {
            if (importer == null || !IsProjectAudioAsset(assetPath))
                return true;

            AudioImportPolicy policy = ResolvePolicy(assetPath, clipLengthSeconds);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            bool compliant = true;

            if (importer.forceToMono != policy.Spatial3D)
            {
                AppendPolicyIssue(builder, assetPath, "forceToMono", policy.Spatial3D, importer.forceToMono);
                compliant = false;
            }

            if (importer.preloadAudioData != policy.Preload)
            {
                AppendPolicyIssue(builder, assetPath, "preloadAudioData", policy.Preload, importer.preloadAudioData);
                compliant = false;
            }

            if (importer.loadInBackground != policy.BackgroundLoad)
            {
                AppendPolicyIssue(builder, assetPath, "loadInBackground", policy.BackgroundLoad, importer.loadInBackground);
                compliant = false;
            }

            if (settings.loadType != policy.LoadType)
            {
                AppendPolicyIssue(builder, assetPath, "loadType", policy.LoadType.ToString(), settings.loadType.ToString());
                compliant = false;
            }

            if (settings.compressionFormat != policy.CompressionFormat)
            {
                AppendPolicyIssue(builder, assetPath, "compressionFormat", policy.CompressionFormat.ToString(), settings.compressionFormat.ToString());
                compliant = false;
            }

            if (settings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                AppendPolicyIssue(builder, assetPath, "sampleRateSetting", AudioSampleRateSetting.OverrideSampleRate.ToString(), settings.sampleRateSetting.ToString());
                compliant = false;
            }

            if (settings.sampleRateOverride != policy.SampleRate)
            {
                AppendPolicyIssue(builder, assetPath, "sampleRateOverride", policy.SampleRate, settings.sampleRateOverride);
                compliant = false;
            }

            if (policy.CompressionFormat == AudioCompressionFormat.Vorbis &&
                Math.Abs(settings.quality - policy.Quality) > 0.001f)
            {
                AppendPolicyIssue(builder, assetPath, "quality", policy.Quality, settings.quality);
                compliant = false;
            }

            return compliant;
        }

        private static void AppendPolicyIssue(StringBuilder builder, string assetPath, string field, bool expected, bool actual)
        {
            AppendPolicyIssue(builder, assetPath, field, expected ? "true" : "false", actual ? "true" : "false");
        }

        private static void AppendPolicyIssue(StringBuilder builder, string assetPath, string field, int expected, int actual)
        {
            builder.Append(" - ");
            builder.Append(assetPath);
            builder.Append(" | ");
            builder.Append(field);
            builder.Append(" expected=");
            builder.Append(expected);
            builder.Append(" actual=");
            builder.Append(actual);
            builder.AppendLine();
        }

        private static void AppendPolicyIssue(StringBuilder builder, string assetPath, string field, float expected, float actual)
        {
            builder.Append(" - ");
            builder.Append(assetPath);
            builder.Append(" | ");
            builder.Append(field);
            builder.Append(" expected=");
            builder.Append(expected);
            builder.Append(" actual=");
            builder.Append(actual);
            builder.AppendLine();
        }

        private static void AppendPolicyIssue(StringBuilder builder, string assetPath, string field, string expected, string actual)
        {
            builder.Append(" - ");
            builder.Append(assetPath);
            builder.Append(" | ");
            builder.Append(field);
            builder.Append(" expected=");
            builder.Append(expected);
            builder.Append(" actual=");
            builder.Append(actual);
            builder.AppendLine();
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

        [MenuItem("Hecton/Audio/Apply Import Policy To All Audio Assets", priority = 409)]
        internal static void ApplyPolicyToAllAudioAssetsMenu()
        {
            int changedCount = ApplyPolicyToAllAudioAssets();
            Debug.Log("[AudioImportDictator:0xA1D10005] Applied import policy to " +
                      changedCount +
                      " changed audio assets under " +
                      ProjectAudioRoot +
                      ".");
        }

        internal static int ApplyPolicyToAllAudioAssets()
        {
            List<string> paths = CollectProjectAudioClipPaths();
            int changedCount = 0;

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = paths[i];
                    AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (importer == null || clip == null)
                        continue;

                    if (!ApplyPolicy(importer, path, clip.length))
                        continue;

                    importer.SaveAndReimport();
                    changedCount++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            return changedCount;
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
            // Task 2 is absolute: every sub-2s one-shot stays ADPCM, including VO stubs.
            if (clipLengthSeconds >= 0f && clipLengthSeconds < ShortClipSeconds)
                return AudioCompressionFormat.ADPCM;

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
                return DialogueSampleRate;

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
            ValidateImportPolicyDrift(true);
            ValidatePreloadedAudioBudget(true);
            EnvironmentAudioSourcePurgeGate.ValidateEnvironmentPrefabsNoAudioSources(true);
        }

        [MenuItem("Hecton/Validation/Audio/Validate Import Policy Drift", priority = 409)]
        internal static void ValidateImportPolicyDriftMenu()
        {
            ValidateImportPolicyDrift(false);
        }

        internal static void ValidateImportPolicyDrift(bool failBuild)
        {
            List<string> paths = AudioImportDictator.CollectProjectAudioClipPaths();
            StringBuilder builder = new StringBuilder(8192);
            int offenderCount = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (importer == null || clip == null)
                    continue;

                int beforeLength = builder.Length;
                if (AudioImportDictator.IsPolicyCompliant(importer, path, clip.length, builder))
                    continue;

                if (builder.Length > beforeLength)
                    offenderCount++;
            }

            if (offenderCount <= 0)
                return;

            string reportText = BuildPolicyDriftReport(offenderCount, builder);
            Debug.LogError(reportText);
            if (failBuild)
                throw new BuildFailedException(reportText);
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

        private static string BuildPolicyDriftReport(int offenderCount, StringBuilder issues)
        {
            StringBuilder builder = new StringBuilder(issues.Length + 256);
            builder.AppendLine("[AudioImportPolicyDriftGate:0xA1D10006] First-party audio import settings drifted from AudioImportDictator. Build aborted.");
            builder.Append("Offending audio assets: ");
            builder.Append(offenderCount);
            builder.AppendLine();
            builder.Append(issues);
            return builder.ToString();
        }
    }

    /// <summary>
    /// Strips ambient prefab AudioSources so environment audio enters through SignalBus-backed systems only.
    /// </summary>
    internal sealed class EnvironmentAudioSourcePurgeGate
    {
        private const string ProjectPrefabRoot = "Assets/_Project/Prefabs";

        private struct PrefabAudioIssue
        {
            public string Path;
            public int Count;
        }

        [MenuItem("Hecton/Audio/Purge Environment Prefab AudioSources", priority = 411)]
        internal static void PurgeEnvironmentAudioSourcesMenu()
        {
            PurgeEnvironmentAudioSources();
        }

        [MenuItem("Hecton/Validation/Audio/Validate Environment Prefabs No AudioSources", priority = 412)]
        internal static void ValidateEnvironmentPrefabsNoAudioSourcesMenu()
        {
            ValidateEnvironmentPrefabsNoAudioSources(false);
        }

        internal static void PurgeEnvironmentAudioSources()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectPrefabRoot });
            int changedPrefabCount = 0;
            int removedSourceCount = 0;

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsEnvironmentPrefabPath(path))
                    continue;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    AudioSource[] sources = root.GetComponentsInChildren<AudioSource>(true);
                    if (sources == null || sources.Length <= 0)
                        continue;

                    for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
                    {
                        UnityEngine.Object.DestroyImmediate(sources[sourceIndex], true);
                        removedSourceCount++;
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changedPrefabCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[EnvironmentAudioSourcePurgeGate:0xA1D10003] Removed " +
                      removedSourceCount +
                      " AudioSource components from " +
                      changedPrefabCount +
                      " environment prefabs.");
        }

        internal static void ValidateEnvironmentPrefabsNoAudioSources(bool failBuild)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { ProjectPrefabRoot });
            List<PrefabAudioIssue> issues = new List<PrefabAudioIssue>(16);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsEnvironmentPrefabPath(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                AudioSource[] sources = prefab.GetComponentsInChildren<AudioSource>(true);
                if (sources == null || sources.Length <= 0)
                    continue;

                issues.Add(new PrefabAudioIssue
                {
                    Path = path,
                    Count = sources.Length
                });
            }

            if (issues.Count <= 0)
                return;

            string report = BuildFailureReport(issues);
            Debug.LogError(report);
            if (failBuild)
                throw new BuildFailedException(report);
        }

        private static string BuildFailureReport(List<PrefabAudioIssue> issues)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("[EnvironmentAudioSourcePurgeGate:0xA1D10004] Environment prefab AudioSource components are banned. Build aborted.");
            for (int i = 0; i < issues.Count; i++)
            {
                builder.Append(" - ");
                builder.Append(issues[i].Count);
                builder.Append(" AudioSource | ");
                builder.AppendLine(issues[i].Path);
            }

            return builder.ToString();
        }

        private static bool IsEnvironmentPrefabPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = AudioImportDictator.NormalizePath(path);
            if (!normalizedPath.StartsWith(ProjectPrefabRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            if (ContainsToken(normalizedPath, "/Audio/") ||
                ContainsToken(normalizedPath, "/Player") ||
                ContainsToken(normalizedPath, "/Tools/") ||
                ContainsToken(normalizedPath, "/UI/") ||
                ContainsToken(normalizedPath, "/Interface/"))
            {
                return false;
            }

            return ContainsToken(normalizedPath, "/Environment/") ||
                   ContainsToken(normalizedPath, "/World/") ||
                   ContainsToken(normalizedPath, "/Biome") ||
                   ContainsToken(normalizedPath, "/Cave") ||
                   ContainsToken(normalizedPath, "/Flora") ||
                   ContainsToken(normalizedPath, "/Terrain") ||
                   ContainsToken(normalizedPath, "/Ambient") ||
                   ContainsToken(normalizedPath, "/Outpost");
        }

        private static bool ContainsToken(string path, string token)
        {
            return path.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
