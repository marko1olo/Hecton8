using System.Runtime.InteropServices;

namespace Hecton8.Core.Persistence
{
    /// <summary>
    /// Marker for the isolated persistence assembly boundary.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct PersistenceAssemblyMarker
    {
    }
}
