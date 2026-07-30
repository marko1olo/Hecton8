using Hecton8.Editor;
using NUnit.Framework;

public sealed class AnomalyBasinDetectorEditTests
{
    [Test]
    public void PerfectBowlHarness_FindsExactLipAndDeepestPointAboveFiftyMeters()
    {
        AnomalyTestHarness.RunPerfectBowlAssertion();
    }
}
