using System;

namespace Hecton8.Modding
{
    /// <summary>
    /// Public metadata contract exposed for a discovered mod package.
    /// The loader resolves dependency ordering from these fields before any mod code executes.
    /// </summary>
    [Serializable]
    public struct ModMetadata
    {
        /// <summary>
        /// Stable unique mod identifier used in dependency resolution and diagnostics.
        /// Example: <c>com.hecton.examplemod</c>.
        /// </summary>
        public string Id;

        /// <summary>
        /// Human-readable display name used in logs and tooling surfaces.
        /// </summary>
        public string Name;

        /// <summary>
        /// Semantic or project-specific version string supplied by the mod package.
        /// </summary>
        public string Version;

        /// <summary>
        /// Human-readable author string used by tooling and diagnostics surfaces.
        /// </summary>
        public string Author;

        /// <summary>
        /// Stable mod IDs that must load before this package is allowed to execute.
        /// </summary>
        public string[] Dependencies;
    }
}
