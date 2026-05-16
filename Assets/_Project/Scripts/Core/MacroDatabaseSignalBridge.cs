using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using MacroDatabaseHydratedSignal = Hecton8.Core.Contracts.SectorHydratedSignal;

namespace Hecton8.Core
{
    /// <summary>
    /// Contract-facing bridge that lets the isolated macro database assembly emit only typed native signals.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct MacroDatabaseSignalBridge : IMacroDatabaseSignalSink
    {
        public void PublishSectorHydrated(in MacroDatabaseHydratedSignal signal)
        {
            Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal payload = new Hecton8.Core.Contracts.Signals.MacroDatabaseSectorHydrationSignal
            {
                SectorHash = signal.SectorHash,
                FileOffset = signal.FileOffset,
                PayloadBytes = signal.PayloadBytes,
                Frame = signal.FrameIndex,
                SourceTier = signal.SourceTier,
                Flags = signal.Flags
            };
            GlobalSignals.Publish(in payload);
        }
    }
}
