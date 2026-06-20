#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Hecton8.Core;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HarpoonAssessmentTests
    {
        private Type _assessmentType;
        private MethodInfo _tryWriteHeadlineMethod;

        [SetUp]
        public void Setup()
        {
            var toolType = typeof(HarpoonLauncherTool);
            _assessmentType = toolType.GetNestedType("HarpoonAssessment", BindingFlags.NonPublic);
            Assert.IsNotNull(_assessmentType, "Could not find HarpoonAssessment type.");

            _tryWriteHeadlineMethod = _assessmentType.GetMethod("TryWriteHeadline", BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(_tryWriteHeadlineMethod, "Could not find TryWriteHeadline method.");
        }

        private object CreateAssessment(string headline, string summary, string recommendation, string severity)
        {
            return Activator.CreateInstance(_assessmentType, headline, summary, recommendation, severity);
        }

        [Test]
        public void TryWriteHeadline_WithValidHeadline_WritesToBuffer()
        {
            object assessment = CreateAssessment("CRITICAL BREACH", "Hull compromised", "Repair immediately", "CRITICAL");
            var buffer = new FixedCharBuffer(64);
            object[] args = new object[] { buffer };

            bool result = (bool)_tryWriteHeadlineMethod.Invoke(assessment, args);
            buffer = (FixedCharBuffer)args[0];

            Assert.IsTrue(result);
            Assert.AreEqual("CRITICAL BREACH", buffer.AsSpan().ToString());
        }

        [Test]
        public void TryWriteHeadline_EmptyHeadline_ReturnsTrueWithNoChange()
        {
            object assessment = CreateAssessment("", "Summary", "Recommendation", "INFO");
            var buffer = new FixedCharBuffer(64);
            buffer.Append("PREFIX: ");
            object[] args = new object[] { buffer };

            bool result = (bool)_tryWriteHeadlineMethod.Invoke(assessment, args);
            buffer = (FixedCharBuffer)args[0];

            Assert.IsTrue(result);
            Assert.AreEqual("PREFIX: ", buffer.AsSpan().ToString());
        }
    }
}
#endif
