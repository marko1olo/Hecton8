using NUnit.Framework;
using System.Collections.Generic;
using CandiceAIforGames.AI;

[TestFixture]
public class CandiceGOAPStateHasStatesTests
{
    private CandiceGOAPState _goapState;

    [SetUp]
    public void SetUp()
    {
        _goapState = new CandiceGOAPState(new Dictionary<string, int>());
    }

    [Test]
    public void HasStates_WhenAllConditionsMetExactly_ReturnsTrue()
    {
        // Arrange
        _goapState.AddState("hasWeapon", 1);
        _goapState.AddState("enemyVisible", 1);
        _goapState.AddState("ammoCount", 10);

        var conditions = new Dictionary<string, int>
        {
            { "hasWeapon", 1 },
            { "enemyVisible", 1 }
        };

        // Act
        bool result = _goapState.HasStates(conditions);

        // Assert
        Assert.That(result, Is.True);
    }

    [Test]
    public void HasStates_WhenOneConditionIsMissing_ReturnsFalse()
    {
        // Arrange
        _goapState.AddState("hasWeapon", 1);

        var conditions = new Dictionary<string, int>
        {
            { "hasWeapon", 1 },
            { "enemyVisible", 1 }
        };

        // Act
        bool result = _goapState.HasStates(conditions);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasStates_WhenOneConditionHasDifferentValue_ReturnsFalse()
    {
        // Arrange
        _goapState.AddState("hasWeapon", 1);
        _goapState.AddState("enemyVisible", 0);

        var conditions = new Dictionary<string, int>
        {
            { "hasWeapon", 1 },
            { "enemyVisible", 1 }
        };

        // Act
        bool result = _goapState.HasStates(conditions);

        // Assert
        Assert.That(result, Is.False);
    }

    [Test]
    public void HasStates_WhenConditionsEmpty_ReturnsTrue()
    {
        // Arrange
        _goapState.AddState("hasWeapon", 1);

        var conditions = new Dictionary<string, int>();

        // Act
        bool result = _goapState.HasStates(conditions);

        // Assert
        Assert.That(result, Is.True);
    }
}
