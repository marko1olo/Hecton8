using System.Runtime.InteropServices;

namespace Hecton8.Core.Persistence
{
    internal static class PersistenceAssemblyLayout
    {
        public const int AssemblyMarkerStrideBytes = 1;
    }

    /// <summary>
    /// Marker for the isolated persistence assembly boundary.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = PersistenceAssemblyLayout.AssemblyMarkerStrideBytes)]
    public readonly struct PersistenceAssemblyMarker
    {
    }
}
