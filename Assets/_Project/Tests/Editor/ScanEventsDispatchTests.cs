#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Gameplay;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class ExceptionThrowingScanEventListener : IScanEventListener
    {
        public void OnScanEvent(in ScanEventPayload payload)
        {
            throw new InvalidOperationException("Test exception");
        }
    }

    [TestFixture]
    public class ScanEventsDispatchTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetScanEventsState();
            var field = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null) field.SetValue(null, true);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                var method = typeof(ScanEvents).GetMethod("ReleaseNativeQueues", BindingFlags.NonPublic | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, null);
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
            }
            finally
            {
                ResetScanEventsState();
                var field = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null) field.SetValue(null, false);
            }
        }

        private void ResetScanEventsState()
        {
            typeof(ScanEvents).GetField("_pendingEventCount", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 0);
            typeof(ScanEvents).GetField("_nextFrameEventCount", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 0);
            typeof(ScanEvents).GetField("_isDispatching", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);
            typeof(ScanEvents).GetField("_listenerExceptionCount", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 0);

            var listenersField = typeof(ScanEvents).GetField("_listeners", BindingFlags.NonPublic | BindingFlags.Static);
            if (listenersField != null)
            {
                var listeners = listenersField.GetValue(null);
                var clearMethod = listeners.GetType().GetMethod("Clear");
                if (clearMethod != null)
                {
                    clearMethod.Invoke(listeners, null);
                }
                listenersField.SetValue(null, listeners);
            }
        }

        [Test]
        public void DispatchToListener_WhenExceptionThrown_IncrementsListenerExceptionCount()
        {
            var listener = new ExceptionThrowingScanEventListener();
            ScanEvents.Register(listener);

            ScanEvents.TryRaiseNodeFound(float3.zero);

            // Act
            ScanEvents.FlushPending();

            // Assert
            var exceptionCountField = typeof(ScanEvents).GetField("_listenerExceptionCount", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(exceptionCountField, "_listenerExceptionCount field not found");

            int exceptionCount = (int)exceptionCountField.GetValue(null);
            Assert.That(exceptionCount, Is.EqualTo(1), "Listener exception count should be incremented when listener throws.");
        }
    }
}
#endif
