using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Narrative;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class AudioLogSystemSlowTickTests
    {
        private AudioLogSystem _system;

        [SetUp]
        public void SetUp()
        {
            GameObject go = new GameObject("AudioLogSystem");
            _system = go.AddComponent<AudioLogSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_system != null && _system.gameObject != null)
            {
                Object.DestroyImmediate(_system.gameObject);
            }
        }

        [Test]
        public void SlowTick_Aborts_WhenRuntimeOwnerAborted()
        {
            SetField(_system, "_runtimeOwnerAborted", true);
            SetField(_system, "_isPlaying", true);
            SetField(_system, "_playbackTimer", 1.0f);

            _system.SlowTick();

            Assert.That((float)GetField(_system, "_playbackTimer"), Is.EqualTo(1.0f));
        }

        [Test]
        public void SlowTick_ReducesPlaybackTimer_AndStopsPlayback_WhenTimerReachesZero()
        {
            AudioLogData dummyLog = ScriptableObject.CreateInstance<AudioLogData>();
            SetField(_system, "_isPlaying", true);
            SetField(_system, "_currentLog", dummyLog);
            SetField(_system, "_playbackTimer", 0.4f); // Needs to be <= 0 after subtracting 0.5f

            _system.SlowTick();

            Assert.That(_system.IsPlaying, Is.False);
            Assert.That(GetField(_system, "_currentLog"), Is.Null);
            Assert.That((float)GetField(_system, "_playbackTimer"), Is.EqualTo(0f));
            Assert.That((bool)GetField(_system, "_currentPlaybackBitCrushed"), Is.False);

            Object.DestroyImmediate(dummyLog);
        }

        [Test]
        public void SlowTick_ReducesPlaybackTimer_ButKeepsPlaying_WhenTimerAboveZero()
        {
            AudioLogData dummyLog = ScriptableObject.CreateInstance<AudioLogData>();
            SetField(_system, "_isPlaying", true);
            SetField(_system, "_currentLog", dummyLog);
            SetField(_system, "_playbackTimer", 1.0f);

            _system.SlowTick();

            Assert.That(_system.IsPlaying, Is.True);
            Assert.That(GetField(_system, "_currentLog"), Is.EqualTo(dummyLog));
            Assert.That((float)GetField(_system, "_playbackTimer"), Is.EqualTo(0.5f));

            Object.DestroyImmediate(dummyLog);
        }

        [Test]
        public void SlowTick_TickAtmosphericWarningBlocker_StopsIfTrue()
        {
            SetField(_system, "_atmosphericWarningActive", true);
            SetField(_system, "_atmosphericWarningTimer", 0.4f);
            SetField(_system, "_isPlaying", false);
            SetField(_system, "_playbackTimer", 1.0f);

            _system.SlowTick();

            Assert.That((bool)GetField(_system, "_atmosphericWarningActive"), Is.False);
            // Verify playbackTimer wasn't touched because queuedPlaybackStarted returned
            Assert.That((float)GetField(_system, "_playbackTimer"), Is.EqualTo(1.0f));
        }

        private void SetField(object obj, string fieldName, object value)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
        }

        private object GetField(object obj, string fieldName)
        {
            FieldInfo field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            return field?.GetValue(obj);
        }
    }
}
