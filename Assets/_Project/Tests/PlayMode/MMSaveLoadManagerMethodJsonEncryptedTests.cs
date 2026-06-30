using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Text.RegularExpressions;
using MoreMountains.Tools;

namespace Hecton8.Tests.PlayMode
{
    public class MMSaveLoadManagerMethodJsonEncryptedTests
    {
        [System.Serializable]
        private class TestData
        {
            public string Value;
        }

        private string _tempFilePath;

        [SetUp]
        public void SetUp()
        {
            _tempFilePath = Path.Combine(Application.persistentDataPath, "TestEncryptJson_WrongKey.json");
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }

        [Test]
        public void Load_WithWrongEncryptionKey_CatchesCryptographicExceptionAndReturnsNull()
        {
            // Arrange: Save with one key
            var saveMethod = new MMSaveLoadManagerMethodJsonEncrypted();
            saveMethod.Key = "GoodKey123!";

            var testData = new TestData { Value = "SecretValue" };

            using (var saveFile = new FileStream(_tempFilePath, FileMode.Create))
            {
                saveMethod.Save(testData, saveFile);
            }

            // Act: Load with a wrong key
            var loadMethod = new MMSaveLoadManagerMethodJsonEncrypted();
            loadMethod.Key = "BadKey456!";

            object loadedData = null;
            using (var loadFile = new FileStream(_tempFilePath, FileMode.Open))
            {
                // Expect the error log from the catch block
                LogAssert.Expect(LogType.Error, new Regex(@"^\[MMSaveLoadManager\] Encryption key error:.*"));
                loadedData = loadMethod.Load(typeof(TestData), loadFile);
            }

            // Assert: Should return null as per the catch block
            Assert.IsNull(loadedData, "Loaded object should be null when a CryptographicException occurs.");
        }
    }
}
