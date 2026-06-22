#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Hecton8.Core.Contracts.Physics;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class HarpoonLauncherToolTests
    {
        private GameObject _gameObject;
        private HarpoonLauncherTool _tool;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("HarpoonLauncherTool_Test");
            _tool = _gameObject.AddComponent<HarpoonLauncherTool>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void LateFrameTick_WhenTracerInactive_DoesNotThrow()
        {
            // By default, private fields are initialized to default values (false, 0, etc.)
            // so _tracerActive is false.
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_WhenTracerActiveButNotReady_DoesNotThrow()
        {
            SetPrivateField(_tool, "_tracerActive", true);
            SetPrivateField(_tool, "_tracerTimer", 1.0f);

            // The buffers and material are null, so HasTracerReady should be false.
            Assert.DoesNotThrow(() => _tool.LateFrameTick());
        }

        [Test]
        public void LateFrameTick_WhenTracerActiveAndMaterialNull_ReturnsEarlyWithoutThrowing()
        {
            SetPrivateField(_tool, "_tracerActive", true);
            SetPrivateField(_tool, "_tracerTimer", 1.0f);

            // Set fields to make HasTracerReady() return true, but tracerMaterial is null
            SetPrivateField(_tool, "_tracerPositionBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 2, UnsafeUtility.SizeOf<GpuCableSplinePointDTO>()));
            SetPrivateField(_tool, "_tracerTensionBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float)));
            SetPrivateField(_tool, "_tracerDrawParamsBuffer", new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, UnsafeUtility.SizeOf<GpuCableDrawParamsDTO>()));
            SetPrivateField(_tool, "_tracerPropertyBlock", new MaterialPropertyBlock());

            // Material is naturally null, HasTracerReady() returns true, should return at Material check
            try
            {
                Assert.DoesNotThrow(() => _tool.LateFrameTick());
            }
            finally
            {
                CleanupBuffers();
            }
        }

        private void CleanupBuffers()
        {
            var posBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerPositionBuffer");
            var tenBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerTensionBuffer");
            var drawBuffer = GetPrivateField<GraphicsBuffer>(_tool, "_tracerDrawParamsBuffer");

            if (posBuffer != null) posBuffer.Release();
            if (tenBuffer != null) tenBuffer.Release();
            if (drawBuffer != null) drawBuffer.Release();

            SetPrivateField(_tool, "_tracerPositionBuffer", null);
            SetPrivateField(_tool, "_tracerTensionBuffer", null);
            SetPrivateField(_tool, "_tracerDrawParamsBuffer", null);
        }

        private void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = target.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            field.SetValue(target, value);
        }

        private T GetPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
                field = target.GetType().BaseType?.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"Field {fieldName} not found on {target.GetType()}");
            return (T)field.GetValue(target);
        }
    }
}
#endif
