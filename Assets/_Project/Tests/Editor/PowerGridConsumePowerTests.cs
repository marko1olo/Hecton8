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
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 0f, balance: 100f, supplyRatio: 0f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(20f);
        AssertState(_grid, totalGeneration: 80f, balance: 80f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: true);
    }

    [Test]
    public void ConsumePower_DeficitTolerance_NoDeficitIfWithinTolerance()
    {
        // totalGeneration + 0.0001f >= totalConsumption
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 100.00005f, balance: 0f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(0.00002f);
        // Gen will be 99.99998f, + 0.0001f = 100.00008f > 100.00005f => No deficit
        AssertState(_grid, totalGeneration: 99.99998f, balance: -0.00007000001f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: true);
    }

    [Test]
    public void ConsumePower_SupplyRatioBelow10Percent_SetsEmergencyOnly()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 100f, balance: 0f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        // supplyRatio < 0.10f
        _grid.ConsumePower(95f);
        AssertState(_grid, totalGeneration: 5f, balance: -95f, supplyRatio: 0.05f, hasPowerDeficit: true, brownoutTier: LogisticsBrownoutTier.EmergencyOnly, isDirty: true);
    }

    [Test]
    public void ConsumePower_SupplyRatioBelow40Percent_SetsEssentialOnly()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 100f, balance: 0f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        // 0.10f <= supplyRatio < 0.40f
        _grid.ConsumePower(80f);
        AssertState(_grid, totalGeneration: 20f, balance: -80f, supplyRatio: 0.20f, hasPowerDeficit: true, brownoutTier: LogisticsBrownoutTier.EssentialOnly, isDirty: true);
    }

    [Test]
    public void ConsumePower_SupplyRatioAt85Percent_SetsNone()
    {
        SetGridState(_grid, totalGeneration: 100f, totalConsumption: 100f, balance: 0f, supplyRatio: 1f, hasPowerDeficit: false, brownoutTier: LogisticsBrownoutTier.None, isDirty: false);

        _grid.ConsumePower(15f);
        AssertState(_grid, totalGeneration: 85f, balance: -15f, supplyRatio: 0.85f, hasPowerDeficit: true, brownoutTier: LogisticsBrownoutTier.None, isDirty: true);
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
        Assert.That(grid.TotalGeneration, Is.EqualTo(totalGeneration).Within(0.0001f));
        Assert.That(grid.Balance, Is.EqualTo(balance).Within(0.0001f));
        Assert.That(grid.SupplyRatio, Is.EqualTo(supplyRatio).Within(0.0001f));
        Assert.That(grid.HasPowerDeficit, Is.EqualTo(hasPowerDeficit));
        Assert.That(grid.BrownoutTier, Is.EqualTo(brownoutTier));
        Assert.That(grid.IsDirty, Is.EqualTo(isDirty));
    }
}
