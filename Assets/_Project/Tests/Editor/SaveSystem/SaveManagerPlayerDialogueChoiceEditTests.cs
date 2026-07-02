using NUnit.Framework;
using UnityEngine;

namespace Project.Tests.SaveSystem
{
    public class SaveManagerPlayerDialogueChoiceEditTests
    {
        private GameObject _saveManagerGameObject;
        private SaveManager _saveManager;

        [SetUp]
        public void Setup()
        {
            _saveManagerGameObject = new GameObject("SaveManager");
            _saveManager = _saveManagerGameObject.AddComponent<SaveManager>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_saveManagerGameObject);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_UpdatesFlags_WithValidInput()
        {
            ushort expectedMask = SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask;

            _saveManager.RecordPlayerDialogueChoiceFlag(expectedMask);

            Assert.AreEqual(expectedMask, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_IgnoresZeroMask()
        {
            _saveManager.RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask);

            _saveManager.RecordPlayerDialogueChoiceFlag(0);

            Assert.AreEqual(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_SanitizesInvalidFlags()
        {
            ushort invalidFlag = 0b1000_0000_0000_0000;
            ushort combinedMask = (ushort)(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask | invalidFlag);

            _saveManager.RecordPlayerDialogueChoiceFlag(combinedMask);

            Assert.AreEqual(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask, _saveManager.PlayerDialogueChoiceFlags);
        }

        [Test]
        public void RecordPlayerDialogueChoiceFlag_IsIdempotent()
        {
            _saveManager.RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask);
            _saveManager.RecordPlayerDialogueChoiceFlag(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask);

            Assert.AreEqual(SaveBinaryStorage.PlayerDialogueChoiceSaveFacilityMask, _saveManager.PlayerDialogueChoiceFlags);
        }
    }
}
