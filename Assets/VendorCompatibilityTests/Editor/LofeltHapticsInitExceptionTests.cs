using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Lofelt.NiceVibrations;

namespace VendorCompatibility.Tests.Editor
{
    [TestFixture]
    public sealed class LofeltHapticsInitExceptionTests
    {
        [Test]
        public void Initialize_WhenExceptionIsThrown_CatchesExceptionAndLogs()
        {
            // Arrange
            System.Action originalAction = LofeltHaptics.AndroidInitAction;

            try
            {
                // Inject a mock action that throws an exception
                LofeltHaptics.AndroidInitAction = () =>
                {
                    throw new InvalidOperationException("Simulated JNI Initialization Failure");
                };

                // We expect the Debug.LogException to log this exact exception
                LogAssert.Expect(LogType.Exception, new Regex("Simulated JNI Initialization Failure"));

                // Act
                // Calling Initialize() will trigger the injected mock action.
                // If the try-catch block does not swallow the exception as intended,
                // the test will fail here due to an unhandled exception.
                LofeltHaptics.Initialize();

                // Assert
                // Implicit assertion via LogAssert that the exception was logged successfully
                // and execution continued without bubbling up the exception to the caller.
            }
            finally
            {
                // Cleanup
                LofeltHaptics.AndroidInitAction = originalAction;
            }
        }
    }
}
