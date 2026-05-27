using System.Reflection;
using Hecton8.Environment;
using NUnit.Framework;
using Unity.Collections;

public sealed class WeatherEventsEditTests
{
    private const BindingFlags StaticPrivateFlags = BindingFlags.Static | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        ResetWeatherEvents();
    }

    [TearDown]
    public void TearDown()
    {
        ResetWeatherEvents();
    }

    [Test]
    public void PrepareColdCreatesNativeQueuesBeforeFirstPublish()
    {
        WeatherEvents.PrepareCold();

        AssertQueueCreated("_pendingEvents");
        AssertQueueCreated("_nextFrameEvents");
    }

    private static void AssertQueueCreated(string fieldName)
    {
        FieldInfo field = typeof(WeatherEvents).GetField(fieldName, StaticPrivateFlags);
        Assert.IsNotNull(field, $"Expected WeatherEvents.{fieldName} to exist.");
        NativeQueue<WeatherEventPayload> queue = (NativeQueue<WeatherEventPayload>)field.GetValue(null);
        Assert.IsTrue(queue.IsCreated, $"Expected WeatherEvents.{fieldName} to be prewarmed.");
    }

    private static void ResetWeatherEvents()
    {
        MethodInfo reset = typeof(WeatherEvents).GetMethod("ResetStaticState", StaticPrivateFlags);
        Assert.IsNotNull(reset, "Expected WeatherEvents.ResetStaticState to exist.");
        reset.Invoke(null, null);
    }
}
