using NUnit.Framework;
using Hecton8.Power;

public sealed class PowerNodeVoltageTests
{
    private PowerNode _powerNode;

    [SetUp]
    public void SetUp()
    {
        _powerNode = new UnityEngine.GameObject("PowerNodeTest").AddComponent<PowerNode>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_powerNode != null && _powerNode.gameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(_powerNode.gameObject);
        }
    }

    [Test]
    public void OnVoltageChanged_NormalRange_SetsVoltageAndPowerStatus()
    {
        _powerNode.OnVoltageChanged(0.5f);
        Assert.AreEqual(0.5f, _powerNode.Voltage01, 0.0001f);
        Assert.IsTrue(_powerNode.HasPower);

        _powerNode.OnVoltageChanged(0.1f);
        Assert.AreEqual(0.1f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);
    }

    [Test]
    public void OnVoltageChanged_AboveOne_ClampsToOne()
    {
        _powerNode.OnVoltageChanged(1.5f);
        Assert.AreEqual(1f, _powerNode.Voltage01, 0.0001f);
        Assert.IsTrue(_powerNode.HasPower);
    }

    [Test]
    public void OnVoltageChanged_BelowZero_ClampsToZero()
    {
        _powerNode.OnVoltageChanged(-0.5f);
        Assert.AreEqual(0f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);
    }

    [Test]
    public void OnVoltageChanged_NaN_DefaultsToZero()
    {
        _powerNode.OnVoltageChanged(float.NaN);
        Assert.AreEqual(0f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);
    }

    [Test]
    public void OnVoltageChanged_Infinity_DefaultsToZero()
    {
        _powerNode.OnVoltageChanged(float.PositiveInfinity);
        Assert.AreEqual(0f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);

        _powerNode.OnVoltageChanged(float.NegativeInfinity);
        Assert.AreEqual(0f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);
    }

    [Test]
    public void OnVoltageChanged_Threshold_SetsHasPowerCorrectly()
    {
        _powerNode.OnVoltageChanged(0.2f);
        Assert.AreEqual(0.2f, _powerNode.Voltage01, 0.0001f);
        Assert.IsTrue(_powerNode.HasPower);

        _powerNode.OnVoltageChanged(0.199f);
        Assert.AreEqual(0.199f, _powerNode.Voltage01, 0.0001f);
        Assert.IsFalse(_powerNode.HasPower);
    }
}
