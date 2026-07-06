using NUnit.Framework;
using UnityEngine;
using MoreMountains.Tools;

namespace MoreMountains.Tools.Tests
{
    public class MMSaveLoadKeyEmptyTests
    {
        [Test]
        public void MMSaveLoadManagerMethod_SetSaveLoadMethod_ThrowsIfBinaryEncryptedAndKeyEmpty()
        {
            var go = new GameObject();
            var method = go.AddComponent<MMSaveLoadManagerMethod>();
            method.SaveLoadMethod = MMSaveLoadManagerMethods.BinaryEncrypted;
            method.EncryptionKey = "";
            Assert.Throws<System.ArgumentNullException>(() => method.SetSaveLoadMethod());
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void MMSaveLoadManagerMethod_SetSaveLoadMethod_ThrowsIfJsonEncryptedAndKeyEmpty()
        {
            var go = new GameObject();
            var method = go.AddComponent<MMSaveLoadManagerMethod>();
            method.SaveLoadMethod = MMSaveLoadManagerMethods.JsonEncrypted;
            method.EncryptionKey = "";
            Assert.Throws<System.ArgumentNullException>(() => method.SetSaveLoadMethod());
            GameObject.DestroyImmediate(go);
        }

        [Test]
        public void MMSaveLoadTester_Save_ThrowsIfJsonEncryptedAndKeyEmpty()
        {
            var go = new GameObject();
            var tester = go.AddComponent<MMSaveLoadTester>();
            tester.SaveLoadMethod = MMSaveLoadManagerMethods.JsonEncrypted;
            tester.EncryptionKey = "";
            Assert.Throws<System.ArgumentNullException>(() => tester.Save());
            GameObject.DestroyImmediate(go);
        }
    }
}
