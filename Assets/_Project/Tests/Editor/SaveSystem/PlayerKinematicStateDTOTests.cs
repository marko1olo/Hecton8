using Hecton8.SaveSystem;
using NUnit.Framework;

namespace Hecton8.Tests.Editor.SaveSystem
{
    public sealed class PlayerKinematicStateDTOTests
    {
        [Test]
        public void ApplyTo_AssignsAllFieldsToPlayerStatsDTO()
        {
            // Arrange
            PlayerKinematicStateDTO kinematicState = new PlayerKinematicStateDTO
            {
                posX = 1.1f,
                posY = 2.2f,
                posZ = 3.3f,
                rotX = 4.4f,
                rotY = 5.5f,
                rotZ = 6.6f,
                rotW = 7.7f,
                velX = 8.8f,
                velY = 9.9f,
                velZ = 10.1f,
                flags = 1
            };

            PlayerStatsDTO stats = new PlayerStatsDTO();

            // Act
            kinematicState.ApplyTo(ref stats);

            // Assert
            Assert.AreEqual(1.1f, stats.posX);
            Assert.AreEqual(2.2f, stats.posY);
            Assert.AreEqual(3.3f, stats.posZ);
            Assert.AreEqual(4.4f, stats.rotX);
            Assert.AreEqual(5.5f, stats.rotY);
            Assert.AreEqual(6.6f, stats.rotZ);
            Assert.AreEqual(7.7f, stats.rotW);
            Assert.AreEqual(8.8f, stats.velX);
            Assert.AreEqual(9.9f, stats.velY);
            Assert.AreEqual(10.1f, stats.velZ);
        }
    }
}
