using NUnit.Framework;
using CandiceAIforGames.AI;
using System.Collections.Generic;

namespace Tests.CandiceAI
{
    [TestFixture]
    public class CandiceGOAPStateHasStateTests
    {
        private CandiceGOAPState _state;

        [SetUp]
        public void SetUp()
        {
            _state = new CandiceGOAPState(new Dictionary<string, int>());
        }

        [Test]
        public void HasState_KeyExists_ReturnsTrue()
        {
            // Arrange
            _state.AddState("HasWeapon", 1);

            // Act
            bool result = _state.HasState("HasWeapon");

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void HasState_KeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            _state.AddState("HasWeapon", 1);

            // Act
            bool result = _state.HasState("HasTarget");

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void HasState_EmptyState_ReturnsFalse()
        {
            // Act
            bool result = _state.HasState("HasWeapon");

            // Assert
            Assert.That(result, Is.False);
        }
    }
}
