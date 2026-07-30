using NUnit.Framework;
using Hecton8.Animation.IK;

namespace Hecton8.Tests.Editor.Animation
{
    [TestFixture]
    public class ProceduralIkMatrixLayoutTests
    {
        [Test]
        public void Validate_ReturnsTrue_WhenStructLayoutIsCorrect()
        {
            // Act
            bool isValid = ProceduralIkMatrixLayout.Validate();

            // Assert
            Assert.That(isValid, Is.True, "ProceduralIkMatrixLayout.Validate() should return true, indicating the struct layouts match the predefined constants.");
        }
    }
}
