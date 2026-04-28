#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Enforces the HECTON-8 first-party SFX import contract and exposes a bulk reimport entry point.
    /// </summary>
    internal sealed class HectonAudioPostprocessor : AssetPostprocessor
    {
        internal const string ProjectSfxRoot = "Assets/_Project/Audio/SFX";

        private const string ReimportGuardPrefix = "HectonAudioPostprocessor.ReimportGuard.";
        private const float ShortSfxThresholdSeconds = 0.5f;
        private const int TargetSampleRateHertz = 22050;

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

            Debug.Log($"[HectonAudioPostprocessor] Reimported {clipPaths.Count} managed SFX clips.");
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
                    Debug.LogError($"[HectonAudioPostprocessor] Managed SFX importer policy drift: '{clipPaths[i]}'.");
                }
            }

            if (mismatchCount <= 0)
            {
                Debug.Log($"[HectonAudioPostprocessor] Validated {clipPaths.Count} managed SFX clips. No importer drift detected.");
                return;
            }

            Debug.LogError($"[HectonAudioPostprocessor] Found {mismatchCount} managed SFX clips with importer drift.");
        }

        internal static List<string> CollectManagedSfxPaths()
        {
            string[] clipGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { ProjectSfxRoot });
            List<string> paths = new List<string>(clipGuids.Length);
            HashSet<string> uniquePaths = new HashSet<string>(StringComparer.Ordinal);

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

        private void OnPreprocessAudio()
        {
            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null || !IsManagedSfxAsset(assetPath))
                return;

            ApplyImporterPolicy(importer, -1f);
        }

        private void OnPostprocessAudio(AudioClip audioClip)
        {
            if (audioClip == null || !IsManagedSfxAsset(assetPath))
                return;

            AudioImporter importer = assetImporter as AudioImporter;
            if (importer == null)
                return;

            string guardKey = ReimportGuardPrefix + assetPath;
            bool guardArmed = SessionState.GetBool(guardKey, false);
            bool changed = ApplyImporterPolicy(importer, audioClip.length);

            if (!changed)
            {
                SessionState.SetBool(guardKey, false);
                return;
            }

            if (guardArmed)
            {
                SessionState.SetBool(guardKey, false);
                Debug.LogError(
                    $"[HectonAudioPostprocessor] Import settings remained unstable after reimport: '{assetPath}'.");
                return;
            }

            SessionState.SetBool(guardKey, true);
            importer.SaveAndReimport();
        }

        internal static bool ApplyImporterPolicy(AudioImporter importer, float clipLengthSeconds)
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

            if (clipLengthSeconds >= 0f)
            {
                AudioClipLoadType desiredLoadType = clipLengthSeconds < ShortSfxThresholdSeconds
                    ? AudioClipLoadType.DecompressOnLoad
                    : AudioClipLoadType.CompressedInMemory;
                if (sampleSettings.loadType != desiredLoadType)
                {
                    sampleSettings.loadType = desiredLoadType;
                    changed = true;
                }
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
            return importer.forceToMono &&
                   sampleSettings.compressionFormat == AudioCompressionFormat.ADPCM &&
                   sampleSettings.sampleRateSetting == AudioSampleRateSetting.OverrideSampleRate &&
                   sampleSettings.sampleRateOverride == TargetSampleRateHertz;
        }

        private static bool IsManagedSfxAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.Contains("/Plugins/"))
                return false;

            return normalizedPath.StartsWith(ProjectSfxRoot, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
