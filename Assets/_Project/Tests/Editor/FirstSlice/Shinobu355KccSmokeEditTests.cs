#if UNITY_EDITOR
using Hecton8.Physics.KCC.Editor;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class Shinobu355KccSmokeEditTests
    {
        [Test]
        public void Shinobu355_KccSmoke_100Phantoms_10000Frames_NoNanEscapeRollbackDesync()
        {
            bool passed = Shinobu355KccSmokeRunner.Run(out Shinobu355KccSmokeSummary summary);
            if (!passed)
            {
                Assert.Fail(
                    "SHINOBU_355 KCC smoke failed. flags=" + summary.ErrorFlags +
                    " failures=" + summary.FailureCount +
                    " dump=Docs/AgentLogs/Dump_SHINOBU_355.bin");
            }
        }
    }
}
#endif
