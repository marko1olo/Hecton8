#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using Hecton8.Animation.IK;

namespace Hecton8.Animation.IK.Tests
{
    public sealed class LeviathanTerrainIkLayoutEditTests
    {
        [Test]
        public void Validate_ReturnsTrue_WhenMemoryLayoutMatchesConstants()
        {
            Assert.IsTrue(LeviathanTerrainIkLayout.Validate(), "LeviathanTerrainIkLayout.Validate() returned false. The struct sizes and field offsets do not match the expected constants in LeviathanTerrainIkConstants and LeviathanTerrainIkLayout.");
        }
    }
}
#endif
