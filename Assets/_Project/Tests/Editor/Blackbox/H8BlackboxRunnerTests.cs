#if UNITY_EDITOR
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.BlackboxDiagnostics;

namespace Hecton8.BlackboxDiagnostics.Tests
{
    public class H8BlackboxRunnerTests
    {
        [Test]
        public void RunSelfCheck_HandlesExceptionAndLogsError()
        {
            // Arrange
            bool didThrow = false;
            H8Runner.s_RunSelfCheckLogic = (opts) =>
            {
                didThrow = true;
                throw new Exception("Mock SelfCheck Exception");
            };

            // We expect the Debug.LogError to catch and output this string
            LogAssert.Expect(LogType.Error, new Regex(@"\[H8Blackbox\] SelfCheck failed: Mock SelfCheck Exception"));

            // Act
            H8Runner.RunSelfCheck(new H8DiagnosticOptions());

            // Assert
            Assert.IsTrue(didThrow, "The mock delegate was not invoked.");

            // Cleanup
            H8Runner.s_RunSelfCheckLogic = null;
        }

        [Test]
        public void RunEditMode_HandlesExceptionAndLogsError()
        {
            // Arrange
            bool didThrow = false;
            H8Runner.s_RunEditModeLogic = (opts) =>
            {
                didThrow = true;
                throw new Exception("Mock EditMode Exception");
            };

            // We expect the Debug.LogError to catch and output this string
            LogAssert.Expect(LogType.Error, new Regex(@"\[H8Blackbox\] Edit Mode Run failed: Mock EditMode Exception"));

            // Act
            H8Runner.RunEditMode(new H8DiagnosticOptions());

            // Assert
            Assert.IsTrue(didThrow, "The mock delegate was not invoked.");

            // Cleanup
            H8Runner.s_RunEditModeLogic = null;
        }
    }
}
#endif
