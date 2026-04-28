using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace Hecton8.Editor
{
    /// <summary>
    /// Removes broken or unsupported custom mixer effects from project audio mixers.
    /// Manual-only to avoid full-project asset scans during domain reload.
    /// </summary>
    internal static class AudioMixerSanitizer
    {
        private const string TargetEffectName = "Hecton Sensory Kernel";

        [MenuItem("Hecton8/Audio/Sanitize Missing Mixer Effects")]
        private static void SanitizeFromMenu()
        {
            SanitizeAllMixers(logSummary: true);
        }

        private static void SanitizeAllMixers(bool logSummary)
        {
            string[] mixerGuids = AssetDatabase.FindAssets("t:AudioMixer");
            int sanitizedMixerCount = 0;
            int removedEffectCount = 0;

            for (int i = 0; i < mixerGuids.Length; i++)
            {
                string mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuids[i]);
                if (string.IsNullOrEmpty(mixerPath))
                    continue;

                if (!SanitizeMixerAtPath(mixerPath, out int removedForMixer))
                    continue;

                sanitizedMixerCount++;
                removedEffectCount += removedForMixer;
            }

            if (sanitizedMixerCount <= 0)
            {
                if (logSummary)
                    Debug.Log("[AudioMixerSanitizer] No mixer changes were required.");

                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            Debug.Log(
                $"[AudioMixerSanitizer] Sanitized {sanitizedMixerCount} mixer asset(s). Removed {removedEffectCount} broken/custom effect reference(s).");
        }

        private static bool SanitizeMixerAtPath(string mixerPath, out int removedEffectCount)
        {
            removedEffectCount = 0;

            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            if (mixer == null)
                return false;

            Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(mixerPath);
            if (subAssets == null || subAssets.Length == 0)
                return false;

            HashSet<Object> effectsToRemove = new HashSet<Object>();
            for (int i = 0; i < subAssets.Length; i++)
            {
                Object subAsset = subAssets[i];
                if (subAsset == null)
                    continue;

                if (!IsMixerEffectController(subAsset))
                    continue;

                SerializedObject effectSerializedObject = new SerializedObject(subAsset);
                SerializedProperty effectNameProperty = effectSerializedObject.FindProperty("m_EffectName");
                SerializedProperty effectIdProperty = effectSerializedObject.FindProperty("m_EffectID");
                string effectName = effectNameProperty != null ? effectNameProperty.stringValue : string.Empty;
                string effectId = effectIdProperty != null ? effectIdProperty.stringValue : string.Empty;

                if (ShouldRemoveEffect(effectName, effectId))
                    effectsToRemove.Add(subAsset);
            }

            bool changed = false;
            for (int i = 0; i < subAssets.Length; i++)
            {
                Object subAsset = subAssets[i];
                if (subAsset == null || !IsMixerGroupController(subAsset))
                    continue;

                SerializedObject groupSerializedObject = new SerializedObject(subAsset);
                SerializedProperty effectsProperty = groupSerializedObject.FindProperty("m_Effects");
                if (effectsProperty == null || !effectsProperty.isArray)
                    continue;

                bool groupChanged = false;
                for (int effectIndex = effectsProperty.arraySize - 1; effectIndex >= 0; effectIndex--)
                {
                    SerializedProperty effectReference = effectsProperty.GetArrayElementAtIndex(effectIndex);
                    Object referencedEffect = effectReference.objectReferenceValue;
                    if (referencedEffect != null && !effectsToRemove.Contains(referencedEffect))
                        continue;

                    int sizeBeforeDelete = effectsProperty.arraySize;
                    effectsProperty.DeleteArrayElementAtIndex(effectIndex);
                    if (effectsProperty.arraySize == sizeBeforeDelete)
                    {
                        effectsProperty.DeleteArrayElementAtIndex(effectIndex);
                    }

                    removedEffectCount++;
                    groupChanged = true;
                    changed = true;
                }

                if (groupChanged)
                {
                    groupSerializedObject.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(subAsset);
                }
            }

            if (!changed && effectsToRemove.Count == 0)
                return false;

            foreach (Object effect in effectsToRemove)
            {
                if (effect == null)
                    continue;

                AssetDatabase.RemoveObjectFromAsset(effect);
                removedEffectCount++;
                changed = true;
            }

            if (!changed)
                return false;

            EditorUtility.SetDirty(mixer);
            AssetDatabase.ImportAsset(mixerPath, ImportAssetOptions.ForceUpdate);
            return true;
        }

        private static bool ShouldRemoveEffect(string effectName, string effectId)
        {
            if (string.IsNullOrEmpty(effectId))
                return true;

            return !string.IsNullOrEmpty(effectName) &&
                   effectName.IndexOf(TargetEffectName, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsMixerEffectController(Object subAsset)
        {
            System.Type subAssetType = subAsset.GetType();
            return subAssetType != null && subAssetType.Name == "AudioMixerEffectController";
        }

        private static bool IsMixerGroupController(Object subAsset)
        {
            System.Type subAssetType = subAsset.GetType();
            return subAssetType != null && subAssetType.Name == "AudioMixerGroupController";
        }
    }
}
