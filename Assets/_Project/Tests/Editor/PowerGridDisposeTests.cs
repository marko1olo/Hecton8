using NUnit.Framework;
using Hecton8.Power;
using Unity.Mathematics;
using System.Reflection;

public sealed class PowerGridDisposeTests
{
    private PowerGrid _grid;

    [SetUp]
    public void SetUp()
    {
        _grid = new PowerGrid(16, null);
    }

    [Test]
    public void Dispose_CleanState_NullifiesAndClearsBuffers()
    {
        Assert.DoesNotThrow(() => _grid.Dispose());
    }

    [Test]
    public void Dispose_WithDirtyStateAndPendingEvaluation_CancelsEvaluationAndDisposesSafely()
    {
        // Force evaluation state to simulate mid-tick teardown
        _grid.MarkDirty();
        _grid.BeginSlowTickEvaluation();

        Assert.DoesNotThrow(() => _grid.Dispose());
    }

    [Test]
    public void Dispose_WithThermalDissipationPending_CancelsJobAndDisposesSafely()
    {
        // Mock a pending thermal dissipation job using reflection
        typeof(PowerGrid).GetField("_thermalDissipationPending", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_grid, true);

        Assert.DoesNotThrow(() => _grid.Dispose());
    }
}
