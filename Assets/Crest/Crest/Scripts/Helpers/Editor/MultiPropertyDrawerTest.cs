#if UNITY_EDITOR

using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEngine.TestTools;
using Crest.EditorHelpers;
using Object = UnityEngine.Object;

namespace Crest.Tests
{
    public class MultiPropertyDrawerTest
    {
        class DummyScriptableObject : ScriptableObject
        {
            [DummyDecoratedProperty]
            public float dummyValue;
        }

        class DummyDecoratedPropertyAttribute : DecoratedPropertyAttribute
        {
            internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
            {
                throw new ArgumentException("Simulated exception to trigger catch block");
            }
        }

        [Test]
        public void TestArgumentExceptionCatchBlock()
        {
            // Create target object and serialized property
            var obj = ScriptableObject.CreateInstance<DummyScriptableObject>();
            var serializedObject = new SerializedObject(obj);
            var serializedProperty = serializedObject.FindProperty("dummyValue");

            // Mock the property drawer
            var drawer = new DecoratedDrawer();

            // Set up the drawer's fieldInfo and attribute to our property so it gets the correct attribute
            var fieldInfo = typeof(DummyScriptableObject).GetField("dummyValue");
            typeof(PropertyDrawer).GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(drawer, fieldInfo);
            typeof(PropertyDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(drawer, new DummyDecoratedPropertyAttribute());

            // We expect an error log due to the catch block
            LogAssert.Expect(LogType.Error, "Crest: Property <i>Dummy Value</i> on <i>" + obj.name + "</i> has a multi-property attribute which requires a custom editor.");

            // Call OnGUI to trigger the try-catch block
            drawer.OnGUI(new Rect(), serializedProperty, new GUIContent("Dummy Value"));

            Object.DestroyImmediate(obj);
        }
    }
}

#endif
