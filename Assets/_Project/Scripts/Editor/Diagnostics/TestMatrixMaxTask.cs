using UnityEditor;
using UnityEngine;
using MapMagic.Nodes;
using MapMagic.Core;
using System.IO;

namespace MapMagic.Editor.Diagnostics
{
    public static class TestMatrixMaxTask
    {
        public static void Test()
        {
            try
            {
                Matrix m1 = new Matrix(new CoordRect(0, 0, 10, 10));
                Matrix m2 = new Matrix(new CoordRect(0, 0, 10, 10));
                
                for (int i=0; i<m1.arr.Length; i++) m1.arr[i] = 0.5f; // Terrain = 0.5
                for (int i=0; i<m2.arr.Length; i++) m2.arr[i] = 0.1f; // Floor = 0.1
                
                m1.Max(m2); // Should be Max(0.5, 0.1) = 0.5!
                
                float result = m1.arr[0];
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/matrix_test.txt", $"Max(0.5, 0.1) = {result}");
            }
            catch (System.Exception ex)
            {
                File.WriteAllText("C:/Users/Admin/.gemini/antigravity/brain/7b5d06d2-b333-42a8-ad13-119572c28fd0/matrix_test.txt", ex.ToString());
            }
            finally
            {
                EditorApplication.Exit(0);
            }
        }
    }
}
