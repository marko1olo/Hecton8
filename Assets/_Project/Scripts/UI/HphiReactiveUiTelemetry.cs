using Hecton8.Core;
using UnityEngine;

namespace Hecton8.UI
{
    internal static class HphiReactiveUiTelemetry
    {
        private static readonly uint ActiveUiUpdatesPerFrameHash = unchecked((uint)LocHash.Compute("ActiveUiUpdatesPerFrame"));
        private static readonly uint HphiUiContextHash = unchecked((uint)LocHash.Compute("H-PhiReactiveUI"));

        private static int s_frame = -1;
        private static int s_activeUpdates;

        internal static void RecordActiveUiUpdate()
        {
            int frame = Time.frameCount;
            if (s_frame != frame)
            {
                PublishPreviousFrame();
                s_frame = frame;
                s_activeUpdates = 0;
            }

            s_activeUpdates++;
        }

        private static void PublishPreviousFrame()
        {
            if (s_frame < 0)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                ActiveUiUpdatesPerFrameHash,
                HphiUiContextHash,
                s_activeUpdates);
        }
    }
}
