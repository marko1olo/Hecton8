using NUnit.Framework;
using UnityEngine;
using Hecton8.SaveSystem;
using Hecton8.Core.Contracts;
using Hecton8.Core.Persistence.Paging;
using System.Reflection;
using System.Runtime.Serialization;

namespace Hecton8.Tests.SaveSystem
{
    [TestFixture]
    public class SaveManagerGetWorldPagerTelemetryTests
    {
        private GameObject _go;
        private SaveManager _saveManager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject();
            _saveManager = _go.AddComponent<SaveManager>();
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
            var field = typeof(SaveManager).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(_saveManager, value);
        }

        [Test]
        public void GetWorldPagerTelemetry_ReturnsDefault_WhenRuntimeOwnerAborted()
        {
            SetField("_runtimeOwnerAborted", true);
            SetField("_serviceRegistered", true);
            SetField("_worldPager", FormatterServices.GetUninitializedObject(typeof(H8BinaryWorldPager)));

            var result = _saveManager.GetWorldPagerTelemetry();

            Assert.AreEqual(default(H8WorldPagerTelemetrySnapshot), result);
        }

        [Test]
        public void GetWorldPagerTelemetry_ReturnsDefault_WhenNotServiceRegistered()
        {
            SetField("_runtimeOwnerAborted", false);
            SetField("_serviceRegistered", false);
            SetField("_worldPager", FormatterServices.GetUninitializedObject(typeof(H8BinaryWorldPager)));

            var result = _saveManager.GetWorldPagerTelemetry();

            Assert.AreEqual(default(H8WorldPagerTelemetrySnapshot), result);
        }

        [Test]
        public void GetWorldPagerTelemetry_ReturnsDefault_WhenWorldPagerIsNull()
        {
            SetField("_runtimeOwnerAborted", false);
            SetField("_serviceRegistered", true);
            SetField("_worldPager", null);

            var result = _saveManager.GetWorldPagerTelemetry();

            Assert.AreEqual(default(H8WorldPagerTelemetrySnapshot), result);
        }

        [Test]
        public void GetWorldPagerTelemetry_ReturnsTelemetryFromPager_WhenValid()
        {
            SetField("_runtimeOwnerAborted", false);
            SetField("_serviceRegistered", true);
            var pager = (H8BinaryWorldPager)FormatterServices.GetUninitializedObject(typeof(H8BinaryWorldPager));

            SetField("_worldPager", pager);

            var result = _saveManager.GetWorldPagerTelemetry();

            Assert.IsInstanceOf<H8WorldPagerTelemetrySnapshot>(result);
            Assert.AreEqual(0, result.PendingDiskWrites);
            Assert.AreEqual(0, result.PendingDiskReads);
        }
    }
}
