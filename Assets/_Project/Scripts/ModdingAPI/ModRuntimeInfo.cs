using System;

namespace Hecton8.Modding
{
    /// <summary>
    /// Runtime status of a discovered mod package.
    /// </summary>
    internal enum ModLoadStatus
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
    /// Internal runtime descriptor for a discovered mod package.
    /// Engine UI and diagnostics surfaces should use this contract instead of reading loader internals.
    /// </summary>
    [Serializable]
    internal struct ModRuntimeInfo
    {
        /// <summary>
        /// Static metadata declared by the mod package.
        /// </summary>
        internal ModMetadata Metadata;

        /// <summary>
        /// Current runtime status assigned by the loader.
        /// </summary>
        internal ModLoadStatus Status;

        /// <summary>
        /// Absolute directory path of the mod package root.
        /// </summary>
        internal string DirectoryPath;

        /// <summary>
        /// Human-readable loader message explaining the current status.
        /// Empty for healthy packages.
        /// </summary>
        internal string StatusMessage;

        /// <summary>
        /// Absolute path to the Addressables catalog.json discovered for the mod package.
        /// Empty when the package has no supported Addressables catalog.
        /// </summary>
        internal string CatalogPath;

        /// <summary>
        /// True when the mod package exposed a managed entry assembly and loader entry point.
        /// False for content-only packages.
        /// </summary>
        internal bool HasManagedEntry;

        /// <summary>
        /// True when the loader discovered at least one supported localization file for this package.
        /// </summary>
        internal bool HasLocalizationFiles;
    }
}
