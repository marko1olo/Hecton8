using NUnit.Framework;
using UnityEditor.ShaderGraph.ProviderSystem;

namespace UnityEditor.ShaderGraph.Editor.Tests
{
    class TryParseTypeInfoTests
    {
        [Test]
        public void TestTryParseTypeInfo_FloatArray()
        {
            Assert.IsTrue(HeaderUtils.TryParseTypeInfo("float[4]", out string prim, out bool isScalar, out bool isVector, out bool isMatrix, out int rows, out int cols, out int length));
            Assert.AreEqual("float", prim);
            Assert.IsTrue(isScalar);
            Assert.IsFalse(isVector);
            Assert.IsFalse(isMatrix);
            Assert.AreEqual(-1, rows);
            Assert.AreEqual(-1, cols);
            Assert.AreEqual(4, length);
        }

        [Test]
        public void TestTryParseTypeInfo_Float4Array()
        {
            Assert.IsTrue(HeaderUtils.TryParseTypeInfo("float4[12]", out string prim, out bool isScalar, out bool isVector, out bool isMatrix, out int rows, out int cols, out int length));
            Assert.AreEqual("float", prim);
            Assert.IsFalse(isScalar);
            Assert.IsTrue(isVector);
            Assert.IsFalse(isMatrix);
            Assert.AreEqual(4, rows);
            Assert.AreEqual(-1, cols);
            Assert.AreEqual(12, length);
        }

        [Test]
        public void TestTryParseTypeInfo_MultidimensionalArray_Fails()
        {
            Assert.IsFalse(HeaderUtils.TryParseTypeInfo("float[4,4]", out _, out _, out _, out _, out _, out _, out _));
            Assert.IsFalse(HeaderUtils.TryParseTypeInfo("float[4][4]", out _, out _, out _, out _, out _, out _, out _));
        }

        [Test]
        public void TestTryParseTypeInfo_InvalidArray_Fails()
        {
            Assert.IsFalse(HeaderUtils.TryParseTypeInfo("float[abc]", out _, out _, out _, out _, out _, out _, out _));
            Assert.IsFalse(HeaderUtils.TryParseTypeInfo("float[4", out _, out _, out _, out _, out _, out _, out _));
        }
    }
}
