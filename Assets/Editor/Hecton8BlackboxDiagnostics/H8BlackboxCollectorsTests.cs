#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.BlackboxDiagnostics;

namespace Hecton8.BlackboxDiagnostics.Tests
{
    public class H8BlackboxCollectorsTests
    {
        private Action<H8ProjectMetadata> _originalPopulateProjectMetadata;

        [SetUp]
        public void SetUp()
        {
            _originalPopulateProjectMetadata = H8Collectors.PopulateProjectMetadata;
        }

        [TearDown]
        public void TearDown()
        {
            H8Collectors.PopulateProjectMetadata = _originalPopulateProjectMetadata;
        }

        [Test]
        public void CollectProjectMetadata_CatchesException_ReturnsNonNull()
        {
            // Arrange
            string testErrorMessage = "Test exception message";
            H8Collectors.PopulateProjectMetadata = (meta) =>
            {
                throw new Exception(testErrorMessage);
            };

            var opts = new H8DiagnosticOptions();

            // Act & Assert
            LogAssert.Expect(LogType.Warning, $"[H8Blackbox/Collectors] CollectProjectMetadata failed: {testErrorMessage}");

            var result = H8Collectors.CollectProjectMetadata(opts);

            Assert.IsNotNull(result);
            Assert.AreEqual("Unknown", result.qualityLevelName, "Expected a default or partially initialized metadata object.");
        }
    }
}
#endif
