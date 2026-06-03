#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Enforces the HECTON-8 first-party audio import contract and exposes bulk reimport entry points.
    /// </summary>
    internal sealed class HectonAudioPostprocessor : AssetPostprocessor
    {
        internal const string ProjectAudioRoot = "Assets/_Project/Audio";
        internal const string ProjectSfxRoot = "Assets/_Project/Audio/SFX";
        internal const string ProjectAmbientRoot = "Assets/_Project/Audio/Ambient";
        internal const string ProjectMusicRoot = "Assets/_Project/Audio/Music for Game";

        private static readonly string[] ProjectSfxRoots =
        {
            ProjectSfxRoot,
            "Assets/_Project/Audio/Footsteps",
            "Assets/_Project/Audio/Hit (Damage)",
            "Assets/_Project/Audio/Impact",
            "Assets/_Project/Audio/Movement",
            "Assets/_Project/Audio/Creatures",
            "Assets/_Project/Audio/Thruster",
            "Assets/_Project/Audio/Breathing"
        };

        private static readonly string[] ProjectAmbientRoots =
        {
            ProjectAmbientRoot,
            ProjectMusicRoot
        };

        private const string ReimportGuardPrefix = "HectonAudioPostprocessor.ReimportGuard.";
        private const int TargetSampleRateHertz = 22050;
        private const int TargetMusicSampleRateHertz = 44100;
        private const float TargetVorbisQuality = 0.7f;
        private const string LogReimportedManagedSfx = "[HectonAudioPostprocessor:0xA11D5001] Reimported managed SFX clips.";
        private const string LogReimportedManagedAudio = "[HectonAudioPostprocessor:0xA11D5002] Reimported managed audio clips.";
        private const string LogManagedSfxPolicyDrift = "[HectonAudioPostprocessor:0xA11D5003] Managed SFX importer policy drift.";
        private const string LogManagedSfxValidated = "[HectonAudioPostprocessor:0xA11D5004] Managed SFX clips validated. No importer drift detected.";
        private const string LogManagedSfxDriftFound = "[HectonAudioPostprocessor:0xA11D5005] Managed SFX clips have importer drift.";
        private const string LogManagedAudioPolicyDrift = "[HectonAudioPostprocessor:0xA11D5006] Managed audio importer policy drift.";
        private const string LogManagedAudioValidated = "[HectonAudioPostprocessor:0xA11D5007] Managed audio clips validated. No importer drift detected.";
        private const string LogManagedAudioDriftFound = "[HectonAudioPostprocessor:0xA11D5008] Managed audio clips have importer drift.";
        private const string LogImportSettingsUnstable = "[HectonAudioPostprocessor:0xA11D5009] Import settings remained unstable after reimport.";
        private static readonly List<string> s_deferredReimportPaths = new List<string>(128);
        private static bool s_deferredReimportScheduled;

        [MenuItem("Hecton/Validation/Asset Pipeline/Reimport Managed SFX", priority = 183)]
        private static void ReimportManagedSfx()
        {
            List<string> clipPaths = CollectManagedSfxPaths();

            try
            {
                for (int i = 0; i < clipPaths.Count; i++)
                {
                    string assetPath = clipPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "HECTON-8 Audio Reimport",
                        assetPath,
                        clipPaths.Count > 0 ? (i + 1f) / clipPaths.Count : 1f);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(LogReimportedManagedSfx);
        }

        [MenuItem("Hecton/Validation/Asset Pipeline/Reimport Managed Audio", priority = 185)]
        private static void ReimportManagedAudio()
        {
            List<string> clipPaths = CollectManagedAudioPaths();

            try
            {
                for (int i = 0; i < clipPaths.Count; i++)
                {
                    string assetPath = clipPaths[i];
                    EditorUtility.DisplayProgressBar(
                        "HECTON-8 Audio Reimport",
                        assetPath,
                        clipPaths.Count > 0 ? (i + 1f) / clipPaths.Count : 1f);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(LogReimportedManagedAudio);
        }

        [MenuItem("Hecton/Validation/Asset Pipeline/Validate Managed SFX", priority = 184)]
        private static void ValidateManagedSfx()
        {
            List<string> clipPaths = CollectManagedSfxPaths();
            int mismatchCount = 0;

            for (int i = 0; i < clipPaths.Count; i++)
            {
                AudioImporter importer = AssetImporter.GetAtPath(clipPaths[i]) as AudioImporter;
                if (!ImporterMatchesManagedSfxPolicy(importer))
                {
                    mismatchCount++;
                    Debug.LogError(LogManagedSfxPolicyDrift);
                }
            }

            if (mismatchCount <= 0)
            {
                Debug.Log(LogManagedSfxValidated);
                return;
            }

            Debug.LogError(LogManagedSfxDriftFound);
        }

        [MenuItem("Hecton/Validation/Asset Pipeline/Validate Managed Audio", priority = 186)]
        private static void ValidateManagedAudio()
        {
            List<string> clipPaths = CollectManagedAudioPaths();
            int mismatchCount = 0;

            for (int i = 0; i < clipPaths.Count; i++)
            {
                string clipPath = clipPaths[i];
                AudioImporter importer = AssetImporter.GetAtPath(clipPath) as AudioImporter;
                bool matchesPolicy = IsManagedAmbientAsset(clipPath)
                    ? ImporterMatchesManagedAmbientPolicy(importer)
                    : ImporterMatchesManagedSfxPolicy(importer);
                if (matchesPolicy)
                    continue;

                mismatchCount++;
                Debug.LogError(LogManagedAudioPolicyDrift);
            }

            if (mismatchCount <= 0)
            {
                Debug.Log(LogManagedAudioValidated);
                return;
            }

            Debug.LogError(LogManagedAudioDriftFound);
        }

        internal static List<string> CollectManagedSfxPaths()
        {
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectSfxRoot });
            List<string> paths = new List<string>(clipGuids.Length);
            HashSet<string> uniquePaths = new HashSet<string>(clipGuids.Length, StringComparer.Ordinal);

            for (int i = 0; i < clipGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if (!IsManagedSfxAsset(assetPath) || !uniquePaths.Add(assetPath))
                    continue;

                paths.Add(assetPath);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        internal static List<string> CollectManagedAudioPaths()
        {
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectAudioRoot });
            List<string> paths = new List<string>(clipGuids.Length);
            HashSet<string> uniquePaths = new HashSet<string>(clipGuids.Length, StringComparer.Ordinal);

            for (int i = 0; i < clipGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(clipGuids[i]);
                if ((!IsManagedSfxAsset(assetPath) && !IsManagedAmbientAsset(assetPath)) ||
                    !uniquePaths.Add(assetPath))
                {
                    continue;
                }

                paths.Add(assetPath);
            }

            paths.Sort(StringComparer.Ordinal);
            return paths;
        }

        private void OnPreprocessAudio()
        {
            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null)
                return;

            if (IsAudioImportDictatorOwnedAsset(assetPath))
                return;

            if (IsManagedAmbientAsset(assetPath))
            {
                ApplyAmbientImporterPolicy(importer);
                return;
            }

            if (IsManagedSfxAsset(assetPath))
                ApplyImporterPolicy(importer, -1f);
        }

        private void OnPostprocessAudio(AudioClip audioClip)
        {
            if (audioClip == null)
                return;

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null)
                return;

            if (IsAudioImportDictatorOwnedAsset(assetPath))
                return;

            bool isManagedAmbient = IsManagedAmbientAsset(assetPath);
            bool isManagedSfx = !isManagedAmbient && IsManagedSfxAsset(assetPath);
            if (!isManagedAmbient && !isManagedSfx)
                return;

            string guardKey = ReimportGuardPrefix + assetPath;
            bool guardArmed = SessionState.GetBool(guardKey, false);
            bool changed = isManagedAmbient
                ? ApplyAmbientImporterPolicy(importer)
                : ApplyImporterPolicy(importer, audioClip.length);

            if (!changed)
            {
                SessionState.SetBool(guardKey, false);
                return;
            }

            if (guardArmed)
            {
                SessionState.SetBool(guardKey, false);
                Debug.LogError(LogImportSettingsUnstable);
                return;
            }

            SessionState.SetBool(guardKey, true);
            QueueDeferredReimport(assetPath);
        }

        private static void QueueDeferredReimport(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            if (!s_deferredReimportPaths.Contains(path))
                s_deferredReimportPaths.Add(path);

            if (s_deferredReimportScheduled)
                return;

            s_deferredReimportScheduled = true;
            EditorApplication.delayCall += FlushDeferredReimports;
        }

        private static void FlushDeferredReimports()
        {
            s_deferredReimportScheduled = false;
            if (EditorApplication.isUpdating || EditorApplication.isCompiling)
            {
                QueueDeferredReimportTick();
                return;
            }

            int count = s_deferredReimportPaths.Count;
            for (int i = 0; i < count; i++)
            {
                string path = s_deferredReimportPaths[i];
                string guardKey = ReimportGuardPrefix + path;
                AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    SessionState.SetBool(guardKey, false);
                    continue;
                }

                importer.SaveAndReimport();
            }

            if (count > 0)
                s_deferredReimportPaths.RemoveRange(0, count);

            if (s_deferredReimportPaths.Count > 0)
                QueueDeferredReimportTick();
        }

        private static void QueueDeferredReimportTick()
        {
            if (s_deferredReimportScheduled)
                return;

            s_deferredReimportScheduled = true;
            EditorApplication.delayCall += FlushDeferredReimports;
        }

        internal static bool ApplyImporterPolicy(AudioImporter importer, float clipLengthSeconds)
        {
            return ApplySfxImporterPolicy(importer, clipLengthSeconds);
        }

        internal static bool ApplySfxImporterPolicy(AudioImporter importer, float clipLengthSeconds)
        {
            if (importer == null)
                return false;

            bool changed = false;

            if (!importer.forceToMono)
            {
                importer.forceToMono = true;
                changed = true;
            }

            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;

            if (sampleSettings.compressionFormat != AudioCompressionFormat.ADPCM)
            {
                sampleSettings.compressionFormat = AudioCompressionFormat.ADPCM;
                changed = true;
            }

            if (sampleSettings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                sampleSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                changed = true;
            }

            if (sampleSettings.sampleRateOverride != TargetSampleRateHertz)
            {
                sampleSettings.sampleRateOverride = TargetSampleRateHertz;
                changed = true;
            }

            AudioClipLoadType targetLoadType = ResolveSfxLoadType(clipLengthSeconds);
            if (sampleSettings.loadType != targetLoadType)
            {
                sampleSettings.loadType = targetLoadType;
                changed = true;
            }

            if (changed)
                importer.defaultSampleSettings = sampleSettings;

            return changed;
        }

        internal static bool ApplyAmbientImporterPolicy(AudioImporter importer)
        {
            if (importer == null)
                return false;

            bool changed = false;
            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;

            if (sampleSettings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                changed = true;
            }

            if (sampleSettings.loadType != AudioClipLoadType.CompressedInMemory)
            {
                sampleSettings.loadType = AudioClipLoadType.CompressedInMemory;
                changed = true;
            }

            if (sampleSettings.sampleRateSetting != AudioSampleRateSetting.OverrideSampleRate)
            {
                sampleSettings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
                changed = true;
            }

            if (sampleSettings.sampleRateOverride != TargetMusicSampleRateHertz)
            {
                sampleSettings.sampleRateOverride = TargetMusicSampleRateHertz;
                changed = true;
            }

            if (Mathf.Abs(sampleSettings.quality - TargetVorbisQuality) > 0.001f)
            {
                sampleSettings.quality = TargetVorbisQuality;
                changed = true;
            }

            if (changed)
                importer.defaultSampleSettings = sampleSettings;

            return changed;
        }

        private static bool ImporterMatchesManagedSfxPolicy(AudioImporter importer)
        {
            if (importer == null || !IsManagedSfxAsset(importer.assetPath))
                return false;

            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
            AudioClipLoadType targetLoadType = ResolveSfxLoadType(ResolveClipLengthSeconds(importer));
            return importer.forceToMono &&
                   sampleSettings.compressionFormat == AudioCompressionFormat.ADPCM &&
                   sampleSettings.loadType == targetLoadType &&
                   sampleSettings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate &&
                   sampleSettings.sampleRateOverride == TargetSampleRateHertz;
        }

        private static AudioClipLoadType ResolveSfxLoadType(float clipLengthSeconds)
        {
            _ = clipLengthSeconds;
            return AudioClipLoadType.DecompressOnLoad;
        }

        private static float ResolveClipLengthSeconds(AudioImporter importer)
        {
            AudioClip clip = importer != null ? AssetDatabase.LoadAssetAtPath<AudioClip>(importer.assetPath) : null;
            return clip != null ? clip.length : -1f;
        }

        private static bool ImporterMatchesManagedAmbientPolicy(AudioImporter importer)
        {
            if (importer == null || !IsManagedAmbientAsset(importer.assetPath))
                return false;

            AudioImporterSampleSettings sampleSettings = importer.defaultSampleSettings;
            return sampleSettings.compressionFormat == AudioCompressionFormat.Vorbis &&
                   sampleSettings.loadType == AudioClipLoadType.CompressedInMemory &&
                   sampleSettings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate &&
                   sampleSettings.sampleRateOverride == TargetMusicSampleRateHertz &&
                   Mathf.Abs(sampleSettings.quality - TargetVorbisQuality) <= 0.001f;
        }

        private static bool IsManagedSfxAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.Contains("/Plugins/"))
                return false;

            return PathStartsWithAnyRoot(normalizedPath, ProjectSfxRoots);
        }

        private static bool IsAudioImportDictatorOwnedAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            return IsPathUnderRoot(normalizedPath, ProjectAudioRoot);
        }

        private static bool IsManagedAmbientAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.Contains("/Plugins/"))
                return false;

            if (PathStartsWithAnyRoot(normalizedPath, ProjectAmbientRoots))
                return true;

            return IsPathUnderRoot(normalizedPath, ProjectAudioRoot) &&
                   normalizedPath.IndexOf("ambient", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool PathStartsWithAnyRoot(string normalizedPath, string[] roots)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (IsPathUnderRoot(normalizedPath, roots[i]))
                    return true;
            }

            return false;
        }

        private static bool IsPathUnderRoot(string normalizedPath, string root)
        {
            return normalizedPath.Equals(root, StringComparison.OrdinalIgnoreCase) ||
                   normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
