using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class BuildTestTests
    {
        [Test]
        public void BuildTest_DoBuild_ExecutesWithoutExceptions()
        {
            // Act
            TestDelegate action = () => BuildTest.DoBuild();

            // Assert
            Assert.DoesNotThrow(action, "BuildTest.DoBuild should execute without throwing exceptions.");
        }
    }
}
