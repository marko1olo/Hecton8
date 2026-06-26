#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using Hecton8.AI.Ambient;
using Hecton8.Core.Memory;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

public sealed class AmbientBiotaDirectorExceptionEditTests
{
    private GameObject _host;
    private AmbientBiotaDirector _director;

    [SetUp]
    public void SetUp()
    {
        _host = new GameObject("AmbientBiotaDirectorExceptionTest");
        _director = _host.AddComponent<AmbientBiotaDirector>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_host != null)
            UnityEngine.Object.DestroyImmediate(_host);
    }

    [Test]
    public void TryPinBiotaJobBuffersReleasesPinsWhenExceptionThrown()
    {
        // 1. Arrange a fake vault
        IDataVault vault = Substitute.For<IDataVault>();

        // Mock IsCompactionFenceActive = false to get past the early exit
        vault.IsCompactionFenceActive.Returns(false);

        // When locking BiotaAUPs (first call), succeed.
        vault.TryLockBuffer(Arg.Is<BufferID>(BufferID.BiotaAUPs), Arg.Any<SystemID>()).Returns(true);

        // When locking BiotaVelocities (second call), throw an exception.
        vault.When(v => v.TryLockBuffer(Arg.Is<BufferID>(BufferID.BiotaVelocities), Arg.Any<SystemID>()))
             .Do(callInfo => { throw new InvalidOperationException("Test exception in try block"); });

        // Set the private _vault field
        SetPrivateField(_director, "_vault", vault);

        // 2. Act
        Exception caughtException = null;
        try
        {
            InvokePrivateMethod(_director, "TryPinBiotaJobBuffers");
        }
        catch (TargetInvocationException ex)
        {
            caughtException = ex.InnerException;
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // 3. Assert
        Assert.IsNotNull(caughtException, "Expected InvalidOperationException to be thrown (unwrapped from TargetInvocationException)");
        Assert.IsInstanceOf<InvalidOperationException>(caughtException, "Expected caught exception to be InvalidOperationException");

        // Ensure TryUnlockBuffer was called for the successful lock (BiotaAUPs) in the finally block
        vault.Received(1).TryUnlockBuffer(Arg.Is<BufferID>(BufferID.BiotaAUPs), Arg.Any<SystemID>());
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
        field.SetValue(target, value);
    }

    private static object InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Method {methodName} not found on {target.GetType()}");
        return method.Invoke(target, null);
    }
}
#endif
