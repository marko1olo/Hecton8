#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace Hecton8.Tests.Editor.AI.Ambient
{
    public sealed class AmbientBiotaDirectorBlackBoxTests
    {
        private Type _directorType;
        private FieldInfo _inFlightField;
        private FieldInfo _countField;

        [SetUp]
        public void SetUp()
        {
            _directorType = typeof(Hecton8.AI.Ambient.AmbientBiotaDirector);
            _inFlightField = _directorType.GetField("s_blackBoxDumpInFlight", BindingFlags.NonPublic | BindingFlags.Static);
            _countField = _directorType.GetField("s_blackBoxDumpCount", BindingFlags.NonPublic | BindingFlags.Static);
        }

        [TearDown]
        public void TearDown()
        {
            if (_inFlightField != null)
                _inFlightField.SetValue(null, 0);

            if (_countField != null)
                _countField.SetValue(null, 0);
        }

        [Test]
        public void QueueStagedBlackBoxDump_ExceptionInTry_ResetsInFlightFlag()
        {
            Assert.That(_inFlightField, Is.Not.Null, "s_blackBoxDumpInFlight field not found");
            Assert.That(_countField, Is.Not.Null, "s_blackBoxDumpCount field not found");

            _inFlightField.SetValue(null, 1);
            _countField.SetValue(null, 1);

            var method = _directorType.GetMethod("QueueStagedBlackBoxDump", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, "QueueStagedBlackBoxDump method not found");

            InvalidOperationException caughtException = null;
            Task.Run(() =>
            {
                try
                {
                    method.Invoke(null, null);
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException is InvalidOperationException ioe)
                    {
                        caughtException = ioe;
                    }
                }
            }).Wait();

            Assert.That(caughtException, Is.Not.Null, "Expected InvalidOperationException due to background thread Temp allocation");
            Assert.That((int)_inFlightField.GetValue(null), Is.EqualTo(0), "s_blackBoxDumpInFlight should be reset to 0 in finally block");
        }
    }
}
#endif
