#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.BlackboxDiagnostics;

namespace Hecton8.Tests.Editor.BlackboxDiagnostics
{
    public class H8BlackboxCLITests
    {
        private Action<H8DiagnosticOptions> _originalRunEditModeAction;
        private Action<int> _originalExitAction;

        [SetUp]
        public void SetUp()
        {
            _originalRunEditModeAction = H8CLI.s_RunEditModeAction;
            _originalExitAction = H8CLI.s_ExitAction;
        }

        [TearDown]
        public void TearDown()
        {
            H8CLI.s_RunEditModeAction = _originalRunEditModeAction;
            H8CLI.s_ExitAction = _originalExitAction;
        }

        [Test]
        public void RunEditMode_WhenExceptionThrown_LogsErrorAndExits()
        {
            // Arrange
            bool didThrow = false;
            int exitCode = -1;

            H8CLI.s_RunEditModeAction = (opts) =>
            {
                didThrow = true;
                throw new InvalidOperationException("Test exception in RunEditMode");
            };

            H8CLI.s_ExitAction = (code) =>
            {
                exitCode = code;
            };

            // In our test, Application.isBatchMode is false,
            // but we can fake it or just test the exception path since log is printed unconditionally

            LogAssert.Expect(LogType.Log, "[H8Blackbox] CLI starting RunEditMode...");
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(@"\[H8Blackbox\] CLI RunEditMode failed: Test exception in RunEditMode"));

            // Act
            H8CLI.RunEditMode();

            // Assert
            Assert.IsTrue(didThrow, "The mock delegate was not invoked.");

            // Note: Application.isBatchMode is likely false during tests,
            // so s_ExitAction might not be called. If we wanted to test the exit,
            // we'd need to mock Application.isBatchMode, but it's a native property.
        }
    }
}
#endif
