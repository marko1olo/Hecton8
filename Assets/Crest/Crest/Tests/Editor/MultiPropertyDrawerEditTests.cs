#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;
using Crest;
using Crest.EditorHelpers;
using UnityEngine.TestTools;

namespace Crest.Tests.Editor
{
    public class MockDecoratedPropertyAttribute : DecoratedPropertyAttribute
    {
        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            throw new ArgumentException("Mock ArgumentException");
        }
    }

    public class MockObject : ScriptableObject
    {
        [MockDecoratedProperty]
        public float mockField;
    }

    public class MultiPropertyDrawerEditTests
    {
        [Test]
        public void OnGUI_WhenDecoratedPropertyThrowsArgumentException_LogsError()
        {
            var mockObject = ScriptableObject.CreateInstance<MockObject>();
            mockObject.name = "TestMockObject";
            var serializedObject = new SerializedObject(mockObject);
            var property = serializedObject.FindProperty("mockField");

            var drawer = (DecoratedDrawer)Activator.CreateInstance(typeof(DecoratedDrawer), nonPublic: true);

            // Set attribute using reflection
            var baseType = typeof(PropertyDrawer);

            var attributeFieldInfo = baseType.GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);
            var attribute = new MockDecoratedPropertyAttribute();
            attributeFieldInfo.SetValue(drawer, attribute);

            var fieldInfoProperty = baseType.GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);
            fieldInfoProperty.SetValue(drawer, typeof(MockObject).GetField("mockField"));

            LogAssert.Expect(LogType.Error, $"Crest: Property <i>{property.displayName}</i> on <i>{property.serializedObject.targetObject.name}</i> has a multi-property attribute which requires a custom editor.");

            drawer.OnGUI(new Rect(), property, new GUIContent("Mock Field"));

            UnityEngine.Object.DestroyImmediate(mockObject);
        }
    }
}
#endif
