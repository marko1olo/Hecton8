/*! \cond PRIVATE */
namespace DarkTonic.MasterAudio
{
    public class MasterAudioSettings : SingletonScriptable<MasterAudioSettings>
    {
        public const string AssetName = "MasterAudioSettings.asset";
        public const string AssetFolder = "Assets/Resources/MasterAudio";
        public const string ResourcePath = "MasterAudio/MasterAudioSettings";

        public bool UseDbScale;
        public bool RemoveUnplayedDueToProbabilityVariation = true;
        public bool UseCentsPitch;
        public bool HideLogoNav;
        public bool EditMAFolder;
        public string InstallationFolderPath = MasterAudio.MasterAudioDefaultFolder;
        public MasterAudio.MixerWidthMode MixerWidthSetting = MasterAudio.MixerWidthMode.Narrow;
        public bool BusesShownInNarrow = true;
        public bool ShowWelcomeWindowOnStart = true;

#if UNITY_EDITOR
        static MasterAudioSettings()
        {
            AssetNameToLoad = AssetFolder + "/" + AssetName;
            ResourceNameToLoad = ResourcePath;
            // COLD ALLOC: List<string>[2] - editor-only singleton asset folder list - owner: MasterAudioSettings
            FoldersToCreate = new System.Collections.Generic.List<string>(2) {
                "Assets/Resources",
                "Assets/Resources/MasterAudio"
            };
        }
#endif
    }
}
/*! \endcond */
