using Unity.Collections;

namespace Hecton8.UI.Localization
{
    /// <summary>
    /// Isolated Babel localization assembly marker and native slice contract.
    /// </summary>
    public readonly struct BabelLocalizationAssemblyMarker
    {
        public readonly FixedString64Bytes Marker;

        public BabelLocalizationAssemblyMarker(FixedString64Bytes marker)
        {
            Marker = marker;
        }
    }
}
