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

    [Test]
    public void SetRuntimeActivation01_ValidInput_SetsActivation()
    {
        bool result = _powerNode.SetRuntimeActivation01(0.5f);
        Assert.IsTrue(result);
        var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float actual = (float)field.GetValue(_powerNode);
        Assert.AreEqual(0.5f, actual, 0.0001f);
    }

    [Test]
    public void SetRuntimeActivation01_UnchangedInput_ReturnsFalse()
    {
        _powerNode.SetRuntimeActivation01(0.5f);
        bool result = _powerNode.SetRuntimeActivation01(0.5f);
        Assert.IsFalse(result);
    }

    [Test]
    public void SetRuntimeActivation01_AboveOne_ClampsToOne()
    {
        _powerNode.SetRuntimeActivation01(1.5f);
        var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float actual = (float)field.GetValue(_powerNode);
        Assert.AreEqual(1f, actual, 0.0001f);
    }

    [Test]
    public void SetRuntimeActivation01_BelowZero_ClampsToZero()
    {
        _powerNode.SetRuntimeActivation01(-0.5f);
        var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float actual = (float)field.GetValue(_powerNode);
        Assert.AreEqual(0f, actual, 0.0001f);
    }

    [Test]
    public void SetRuntimeActivation01_NaNOrInfinity_ClampsToOne()
    {
        _powerNode.SetRuntimeActivation01(float.NaN);
        var field = typeof(PowerNode).GetField("_runtimeActivation01", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float actualNaN = (float)field.GetValue(_powerNode);
        Assert.AreEqual(1f, actualNaN, 0.0001f);

        _powerNode.SetRuntimeActivation01(float.PositiveInfinity);
        float actualPosInf = (float)field.GetValue(_powerNode);
        Assert.AreEqual(1f, actualPosInf, 0.0001f);

        _powerNode.SetRuntimeActivation01(float.NegativeInfinity);
        float actualNegInf = (float)field.GetValue(_powerNode);
        Assert.AreEqual(1f, actualNegInf, 0.0001f);
    }

    [Test]
    public void SetRuntimeActivation01_ConductivityChange_UpdatesTopology()
    {
        // Ensure starting state is fully conductive
        _powerNode.SetRuntimeActivation01(1f);

        var revisionField = typeof(PowerNode).GetField("_topologyRevision", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int initialRevision = (int)revisionField.GetValue(_powerNode);

        // Drop conductivity to trigger topology update
        _powerNode.SetRuntimeActivation01(0f); // 0f is clearly <= 0.0001f, dropping conductivity

        int newRevision = (int)revisionField.GetValue(_powerNode);
        Assert.Greater(newRevision, initialRevision);
    }

    [Test]
    public void OnPowerStatusChanged_True_SetsHasPowerAndUpdatesVoltage()
    {
        _powerNode.OnVoltageChanged(0.1f); // Reset state first

        _powerNode.OnPowerStatusChanged(true);

        Assert.IsTrue(_powerNode.HasPower);
        Assert.GreaterOrEqual(_powerNode.Voltage01, 0.2f);
    }

    [Test]
    public void OnPowerStatusChanged_False_SetsHasPowerAndUpdatesVoltage()
    {
        _powerNode.OnVoltageChanged(0.5f); // Reset state first

        _powerNode.OnPowerStatusChanged(false);

        Assert.IsFalse(_powerNode.HasPower);
        Assert.LessOrEqual(_powerNode.Voltage01, 0.199f);
    }
}