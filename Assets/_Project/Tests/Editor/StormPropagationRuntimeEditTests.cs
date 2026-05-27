using System.Reflection;
using Hecton8.Atmosphere;
using NUnit.Framework;
using UnityEngine;

public sealed class StormPropagationRuntimeEditTests
{
    [Test]
    public void StormPropagationRuntimeDoesNotClaimRuntimeInEditMode()
    {
        FieldInfo claimField = typeof(ShinobuStormPropagationRuntime).GetField(
            "s_runtimeClaimed",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(claimField);
        claimField.SetValue(null, 0);

        GameObject host = new GameObject("StormPropagationEditModeHost");
        try
        {
            host.AddComponent<ShinobuStormPropagationRuntime>();

            Assert.That((int)claimField.GetValue(null), Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(host);
            claimField.SetValue(null, 0);
        }
    }
}
