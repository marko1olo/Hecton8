using Hecton8.Core.Contracts;
using Hecton8.Core.Signals;

namespace Hecton8.Core
{
    /// <summary>
    /// Contract-facing bridge that lets the isolated macro database assembly emit only typed native signals.
    /// </summary>
    public readonly struct MacroDatabaseSignalBridge : IMacroDatabaseSignalSink
    {
        public void PublishSectorHydrated(in Hecton8.Core.Contracts.SectorHydratedSignal signal)
        {
            Hecton8.Core.Signals.SectorHydratedSignal payload = new Hecton8.Core.Signals.SectorHydratedSignal
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
