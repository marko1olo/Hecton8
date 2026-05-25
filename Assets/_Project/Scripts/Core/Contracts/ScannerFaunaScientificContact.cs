using System.Runtime.InteropServices;

namespace Hecton8.Core.Contracts
{
    internal static class ScannerFaunaScientificContactLayout
    {
        internal const int ContactStrideBytes = 8;
    }

    public interface IScannerFaunaScientificContact
    {
        bool TryReadScannerFaunaScientificContact(out ScannerFaunaScientificContact contact);
    }

    [StructLayout(LayoutKind.Explicit, Size = ScannerFaunaScientificContactLayout.ContactStrideBytes)]
    public struct ScannerFaunaScientificContact
    {
        public const byte FlagContact = 1 << 0;
        public const byte FlagFlankingManeuver = 1 << 1;

        [FieldOffset(0)] public uint ThreatPredictionLoreHash;
        [FieldOffset(4)] public byte Flags;
        [FieldOffset(5)] private byte _pad0;
        [FieldOffset(6)] private ushort _pad1;
    }
}
