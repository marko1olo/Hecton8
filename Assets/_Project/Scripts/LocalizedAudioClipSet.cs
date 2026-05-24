using System;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// One localized audio clip override for a specific language.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct LocalizedAudioClipVariant
    {
        [Tooltip("Language for this audio override.")]
        [SerializeField] private GameLanguage language;

        [Tooltip("Localized clip for the selected language.")]
        [SerializeField] private AudioClip clip;

        /// <summary>
        /// Language for the override.
        /// </summary>
        public GameLanguage Language => language;

        /// <summary>
        /// Localized clip value.
        /// </summary>
        public AudioClip Clip => clip;
    }

    /// <summary>
    /// Serializable audio localization set with a default clip and optional per-language overrides.
    /// </summary>
    [Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct LocalizedAudioClipSet
    {
        [Tooltip("Default clip used when no per-language override exists.")]
        [SerializeField] private AudioClip defaultClip;

        [Tooltip("Optional per-language clip overrides.")]
        [SerializeField] private LocalizedAudioClipVariant[] variants;

        /// <summary>
        /// Returns the default clip.
        /// </summary>
        public AudioClip DefaultClip => defaultClip;

        /// <summary>
        /// Resolve the clip for the current language.
        /// </summary>
        public AudioClip Resolve()
        {
            LocalizationManager manager = LocalizationManager.ActiveRuntimeInstance;
            return Resolve(manager);
        }

        /// <summary>
        /// Resolve the clip through a cached localization owner.
        /// </summary>
        public AudioClip Resolve(LocalizationManager manager)
        {
            GameLanguage language = manager != null ? manager.CurrentLanguage : GameLanguage.English;
            return Resolve(language);
        }

        /// <summary>
        /// Resolve the clip for a specific language.
        /// </summary>
        public AudioClip Resolve(GameLanguage language)
        {
            if (variants != null)
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    if (variants[i].Language == language && variants[i].Clip != null)
                        return variants[i].Clip;
                }
            }

            return defaultClip;
        }
    }
}
