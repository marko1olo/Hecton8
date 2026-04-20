using System;

namespace Hecton8.Modding
{
    /// <summary>
    /// Runtime status of a discovered mod package.
    /// </summary>
    public enum ModLoadStatus
    {
        /// <summary>
        /// Mod package loaded successfully and remains available for runtime systems.
        /// </summary>
        Active = 0,

        /// <summary>
        /// Mod package was discovered but disabled because of dependency, manifest, or load failures.
        /// </summary>
        Disabled = 1
    }

    /// <summary>
    /// Public runtime descriptor for a discovered mod package.
    /// UI and diagnostics surfaces should use this contract instead of reading loader internals.
    /// </summary>
    [Serializable]
    public struct ModRuntimeInfo
    {
        /// <summary>
        /// Static metadata declared by the mod package.
        /// </summary>
        public ModMetadata Metadata;

        /// <summary>
        /// Current runtime status assigned by the loader.
        /// </summary>
        public ModLoadStatus Status;

        /// <summary>
        /// Absolute directory path of the mod package root.
        /// </summary>
        public string DirectoryPath;

        /// <summary>
        /// Human-readable loader message explaining the current status.
        /// Empty for healthy packages.
        /// </summary>
        public string StatusMessage;

        /// <summary>
        /// Absolute path to the primary AssetBundle discovered for the mod package.
        /// Empty when the package has no supported AssetBundle.
        /// </summary>
        public string AssetBundlePath;

        /// <summary>
        /// True when the mod package exposed a managed entry assembly and loader entry point.
        /// False for content-only packages.
        /// </summary>
        public bool HasManagedEntry;

        /// <summary>
        /// True when the loader discovered at least one supported localization file for this package.
        /// </summary>
        public bool HasLocalizationFiles;
    }
}
