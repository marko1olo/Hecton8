using System;
using System.Reflection;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.SaveSystem.Tests
{
    [TestFixture]
    public class SaveManagerPlayerDialogueChoiceEditTests
    {
        private GameObject _go;
        private SaveManager _saveManager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestSaveManager");
            _saveManager = _go.AddComponent<SaveManager>();

            var fieldInfo = typeof(SaveManager).GetField("_playerDialogueChoiceFlags", BindingFlags.NonPublic | BindingFlags.Instance);
            if (fieldInfo != null)
            {
                fieldInfo.SetValue(_saveManager, 0);
            }
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_ValidFlag_UpdatesMask()
        {
            _saveManager.RecordPlayerDialogueChoiceFlag(1);
            Assert.AreEqual(1, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_InvalidFlag_SanitizedOut()
        {
            // PlayerDialogueChoiceKnownFlagsMask is currently 1.
            // Any other bits (like 0xFFFF & ~1) should be sanitized to 0.
            _saveManager.RecordPlayerDialogueChoiceFlag(0xFFFF);
            // Result should only retain the known flag bit 1 if it was passed,
            // but since it passes through SanitizePlayerDialogueChoiceFlags it will become 1.
            // Wait, 0xFFFF & 1 = 1. So it should be 1.
            Assert.AreEqual(1, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_Idempotent_Accumulates()
        {
            _saveManager.RecordPlayerDialogueChoiceFlag(1);
            _saveManager.RecordPlayerDialogueChoiceFlag(1);
            Assert.AreEqual(1, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_ZeroMask_DoesNothing()
        {
            _saveManager.RecordPlayerDialogueChoiceFlag(1);
            _saveManager.RecordPlayerDialogueChoiceFlag(0);
            Assert.AreEqual(1, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_UpdatesViaInterlocked()
        {
            var threads = new Thread[10];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() => {
                    for (int j = 0; j < 1000; j++)
                    {
                        _saveManager.RecordPlayerDialogueChoiceFlag(1);
                    }
                });
            }

            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();

            Assert.AreEqual(1, _saveManager.PlayerDialogueChoiceFlags);
        }
    }
}
