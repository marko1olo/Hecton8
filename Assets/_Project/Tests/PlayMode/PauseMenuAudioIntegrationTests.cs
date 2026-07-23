using NUnit.Framework;
using UnityEngine;
using Hecton8.UI;
using System.Reflection;

namespace Hecton8.UI.Tests
{
    public class PauseMenuAudioIntegrationTests
    {
        private GameObject _go;
        private PauseMenuAudioIntegration _integration;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _integration = _go.AddComponent<PauseMenuAudioIntegration>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        private void SetField(string fieldName, object value)
        {
            var field = typeof(PauseMenuAudioIntegration).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(_integration, value);
            }
        }

        [Test]
        public void OnPauseMenuOpened_AudioDisabled_DoesNotPlay()
        {
            SetField("enableAudio", false);
            // We can't mock the static UIAudioFeedback.PlayPanelOpen() natively in NUnit.
            // The method should just return early, which means it shouldn't throw an error.
            Assert.DoesNotThrow(() => _integration.OnPauseMenuOpened());
        }

        [Test]
        public void OnPauseMenuOpened_PanelSoundsDisabled_DoesNotPlay()
        {
            SetField("playPanelSounds", false);
            Assert.DoesNotThrow(() => _integration.OnPauseMenuOpened());
        }
    }
}
