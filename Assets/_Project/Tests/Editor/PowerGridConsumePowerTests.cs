using NUnit.Framework;
using Hecton8.Power;
using Unity.Mathematics;
using System.Reflection;

public sealed class PowerGridConsumePowerTests
{
    private PowerGrid _grid;

    [SetUp]
    public void SetUp()
    {
        _grid = new PowerGrid(16, null);
    }

    [TearDown]
    public void TearDown()
    {
        _grid.Dispose();
    }

    [Test]
    public void ConsumePower_AmountZeroOrNegative_DoesNotChangeState()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 50f, balance: 50f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(0f);
        AssertState(_grid, totalGeneration: 100f, balance: 50f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(-10f);
        AssertState(_grid, totalGeneration: 100f, balance: 50f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);
    }

    [Test]
    public void ConsumePower_AmountPositive_UpdatesStateCorrectly()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 50f, balance: 50f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(20f);
        AssertState(_grid, totalGeneration: 80f, balance: 30f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: true);
    }

    [Test]
    public void ConsumePower_AmountExceedsGeneration_ClampsGenerationToZero()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 50f, balance: 50f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(120f);
        AssertState(_grid, totalGeneration: 0f, balance: -50f, supplyRatio: 0f, hasPowerDeficit: true, brownoutTier: LogisticsBrownoutTier.EmergencyOnly, isDirty: true);
    }

    [Test]
    public void ConsumePower_CausesBrownout_UpdatesSupplyRatioAndTier()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 100f, balance: 0f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        // Consume enough to drop supply ratio below 0.85
        _grid.ConsumePower(20f);
        // 80 / 100 = 0.8
        AssertState(_grid, totalGeneration: 80f, balance: -20f, supplyRatio: 0.8f, hasPowerDeficit: true, brownoutTier: LogisticsBrownoutTier.AmbientLightsOnly, isDirty: true);
    }

    [Test]
    public void ConsumePower_TotalConsumptionZero_SetsSupplyRatioToOne()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 0f, balance: 100f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(20f);
        AssertState(_grid, totalGeneration: 80f, balance: 80f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: true);
    }

    private void SetGridState(PowerGrid grid, float totalGeneration, float totalConsumption, float balance, float supplyRatio, bool hasPowerDeficit, LogisticsBrownoutTier brownoutTier, bool isDirty)
    {
        typeof(PowerGrid).GetField("_totalGeneration", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, totalGeneration);
        typeof(PowerGrid).GetField("_totalConsumption", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, totalConsumption);
        typeof(PowerGrid).GetField("_balance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, balance);
        typeof(PowerGrid).GetField("_supplyRatio", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, supplyRatio);
        typeof(PowerGrid).GetField("_hasPowerDeficit", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, hasPowerDeficit);
        typeof(PowerGrid).GetField("_brownoutTier", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, brownoutTier);
        typeof(PowerGrid).GetField("_isDirty", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(grid, isDirty);
    }

    private void AssertState(PowerGrid grid, float totalGeneration, float balance, float supplyRatio, bool hasPowerDeficit, LogisticsBrownoutTier brownoutTier, bool isDirty)
    {
        Assert.AreEqual(totalGeneration, grid.TotalGeneration, 0.0001f);
        Assert.AreEqual(balance, grid.Balance, 0.0001f);
        Assert.AreEqual(supplyRatio, grid.SupplyRatio, 0.0001f);
        Assert.AreEqual(hasPowerDeficit, grid.HasPowerDeficit);
        Assert.AreEqual(brownoutTier, grid.BrownoutTier);
        Assert.AreEqual(isDirty, grid.IsDirty);
    }
}
