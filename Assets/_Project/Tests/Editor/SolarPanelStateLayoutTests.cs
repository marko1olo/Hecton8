#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using Hecton8.Power;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public sealed class SolarPanelStateLayoutTests
    {
        [Test]
        public void SolarPanelStateDTO_HasExactUnsafeLayout()
        {
            Assert.That(UnsafeUtility.SizeOf<SolarPanelStateDTO>(), Is.EqualTo(32));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SolarPanelStateDTO), nameof(SolarPanelStateDTO.PanelAUP)), Is.EqualTo(0));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SolarPanelStateDTO), nameof(SolarPanelStateDTO.BaseEfficiencyScalar)), Is.EqualTo(24));
            Assert.That(UnsafeUtility.GetFieldOffset(typeof(SolarPanelStateDTO), nameof(SolarPanelStateDTO.PowerNodeHashID)), Is.EqualTo(28));
        }
    }
}
#endif
