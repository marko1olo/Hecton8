#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using UnityEngine;
using Hecton8.Building;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HectonSocketHelperTests
    {
        private GameObject _go;
        private HectonSocketHelper _helper;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestSocket");
            _helper = _go.AddComponent<HectonSocketHelper>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
                _go = null;
            }
        }

        [Test]
        public void Initialization_DefaultValues_AreSetCorrectly()
        {
            var socketTypeField = typeof(HectonSocketHelper).GetField("socketType", BindingFlags.NonPublic | BindingFlags.Instance);
            var arrowLengthField = typeof(HectonSocketHelper).GetField("arrowLength", BindingFlags.NonPublic | BindingFlags.Instance);
            var tipRadiusField = typeof(HectonSocketHelper).GetField("tipRadius", BindingFlags.NonPublic | BindingFlags.Instance);
            var snapRayDistanceField = typeof(HectonSocketHelper).GetField("snapRayDistance", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(socketTypeField, "socketType field not found");
            Assert.IsNotNull(arrowLengthField, "arrowLength field not found");
            Assert.IsNotNull(tipRadiusField, "tipRadius field not found");
            Assert.IsNotNull(snapRayDistanceField, "snapRayDistance field not found");

            var socketType = (HectonSocketHelper.SocketType)socketTypeField.GetValue(_helper);
            var arrowLength = (float)arrowLengthField.GetValue(_helper);
            var tipRadius = (float)tipRadiusField.GetValue(_helper);
            var snapRayDistance = (float)snapRayDistanceField.GetValue(_helper);

            Assert.That(socketType, Is.EqualTo(HectonSocketHelper.SocketType.Side));
            Assert.That(arrowLength, Is.EqualTo(0.5f));
            Assert.That(tipRadius, Is.EqualTo(0.05f));
            Assert.That(snapRayDistance, Is.EqualTo(2f));
        }

        [Test]
        public void GetSocketColor_TopSocket_ReturnsGreen()
        {
            var method = typeof(HectonSocketHelper).GetMethod("GetSocketColor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetSocketColor method not found.");

            var color = (Color)method.Invoke(null, new object[] { HectonSocketHelper.SocketType.Top, true });
            Assert.That(color, Is.EqualTo(Color.green));
        }

        [Test]
        public void GetSocketColor_SideSocket_ReturnsYellow()
        {
            var method = typeof(HectonSocketHelper).GetMethod("GetSocketColor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetSocketColor method not found.");

            var color = (Color)method.Invoke(null, new object[] { HectonSocketHelper.SocketType.Side, true });
            Assert.That(color, Is.EqualTo(Color.yellow));
        }

        [Test]
        public void GetSocketColor_UnderSocket_ReturnsRed()
        {
            var method = typeof(HectonSocketHelper).GetMethod("GetSocketColor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetSocketColor method not found.");

            var color = (Color)method.Invoke(null, new object[] { HectonSocketHelper.SocketType.Under, true });
            Assert.That(color, Is.EqualTo(Color.red));
        }

        [Test]
        public void GetSocketColor_Unselected_AppliesAlphaTransparency()
        {
            var method = typeof(HectonSocketHelper).GetMethod("GetSocketColor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetSocketColor method not found.");

            var color = (Color)method.Invoke(null, new object[] { HectonSocketHelper.SocketType.Top, false });

            Assert.That(color.a, Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(color.r, Is.EqualTo(Color.green.r));
            Assert.That(color.g, Is.EqualTo(Color.green.g));
            Assert.That(color.b, Is.EqualTo(Color.green.b));
        }

        [Test]
        public void DrawSocketGizmo_DoesNotThrow_WhenCameraIsNull()
        {
            // Because DrawSocketGizmo checks "if (cam == null) return;",
            // calling it headlessly shouldn't throw.
            var method = typeof(HectonSocketHelper).GetMethod("DrawSocketGizmo", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "DrawSocketGizmo method not found.");

            Assert.DoesNotThrow(() =>
            {
                method.Invoke(_helper, new object[] { true });
            });
        }

        [Test]
        public void GetSocketColor_UnknownSocket_ReturnsCyan()
        {
            var method = typeof(HectonSocketHelper).GetMethod("GetSocketColor", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "GetSocketColor method not found.");

            var color = (Color)method.Invoke(null, new object[] { (HectonSocketHelper.SocketType)999, true });
            Assert.That(color, Is.EqualTo(Color.cyan));
        }

        [Test]
        public void ResetStaticState_ResetsFieldsToDefault()
        {
            var resetMethod = typeof(HectonSocketHelper).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            resetMethod?.Invoke(null, null);

            var labelStyleField = typeof(HectonSocketHelper).GetField("s_LabelStyle", BindingFlags.NonPublic | BindingFlags.Static);
            var lastLabelColorField = typeof(HectonSocketHelper).GetField("s_LastLabelColor", BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(labelStyleField, "s_LabelStyle field not found.");
            Assert.IsNotNull(lastLabelColorField, "s_LastLabelColor field not found.");

            Assert.IsNull(labelStyleField.GetValue(null));
            Assert.That((Color)lastLabelColorField.GetValue(null), Is.EqualTo(new Color(-1f, -1f, -1f, -1f)));
        }
    }
}
#endif
