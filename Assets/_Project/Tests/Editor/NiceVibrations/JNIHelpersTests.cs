using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System;
using System.Reflection;
using System.Text.RegularExpressions;

#if UNITY_EDITOR
namespace Lofelt.NiceVibrations.Tests
{
    public class JNIHelpersTests
    {
        [Test]
        public void CallVoid_WithNullObject_LogsException()
        {
            Type jniHelpersType = typeof(Lofelt.NiceVibrations.JNIHelpers);
            MethodInfo callMethod = jniHelpersType.GetMethod("Call", BindingFlags.Public | BindingFlags.Static, null, new Type[] { typeof(AndroidJavaObject), typeof(IntPtr), typeof(jvalue[]) }, null);

            // By passing null for AndroidJavaObject, obj.GetRawObject() will throw NullReferenceException inside the try block
            // We verify that the catch block catches this and logs the exception.
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

            callMethod.Invoke(null, new object[] { null, new IntPtr(1), new jvalue[] { } });
        }
    }
}
#endif
