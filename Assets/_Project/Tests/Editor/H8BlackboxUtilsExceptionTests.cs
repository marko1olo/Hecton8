#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Hecton8.BlackboxDiagnostics;

namespace Hecton8.Tests.Editor
{
    public class H8BlackboxUtilsExceptionTests
    {
        [Test]
        public void ReadEditorLogTail_CatchBlock_ReturnsErrorMessage()
        {
            // Arrange
            // Use reflection to access internal fields because [assembly: InternalsVisibleTo] is not available
            var type = typeof(H8Utils);

            var fileExistsField = type.GetField("s_FileExists", BindingFlags.Static | BindingFlags.NonPublic);
            var fileStreamFactoryField = type.GetField("s_FileStreamFactory", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.IsNotNull(fileExistsField, "Could not find internal field s_FileExists");
            Assert.IsNotNull(fileStreamFactoryField, "Could not find internal field s_FileStreamFactory");

            var originalFileExists = (Func<string, bool>)fileExistsField.GetValue(null);
            var originalFactory = (Func<string, FileMode, FileAccess, FileShare, Stream>)fileStreamFactoryField.GetValue(null);

            // Force File.Exists to return true so we don't return early
            Func<string, bool> mockFileExists = path => true;
            fileExistsField.SetValue(null, mockFileExists);

            // Force FileStream to throw
            Func<string, FileMode, FileAccess, FileShare, Stream> mockFactory = (path, mode, access, share) =>
            {
                throw new IOException("Simulated file lock exception");
            };
            fileStreamFactoryField.SetValue(null, mockFactory);

            string result = "";
            try
            {
                // Act
                result = H8Utils.ReadEditorLogTail(10);
            }
            finally
            {
                // Restore original state
                fileExistsField.SetValue(null, originalFileExists);
                fileStreamFactoryField.SetValue(null, originalFactory);
            }

            // Assert
            Assert.IsTrue(result.StartsWith("Failed to read Editor.log:"), $"Expected result to start with failure message, but got: {result}");
            Assert.IsTrue(result.Contains("Simulated file lock exception"), $"Expected result to contain the exception message, but got: {result}");
        }
    }
}
#endif
