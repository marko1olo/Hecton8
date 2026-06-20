using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Gameplay;

namespace Hecton8.Tests.PlayMode
{
    public class HectonScanMarkerSystemTest
    {
        [UnityTest]
        public IEnumerator Initialize_WithValidResources_SetsUpWithoutExceptions()
        {
            // Create a valid dummy Mesh (needs submesh and indices)
            Mesh dummyMesh = new Mesh();
            dummyMesh.vertices = new Vector3[] { Vector3.zero, Vector3.up, Vector3.right };
            dummyMesh.triangles = new int[] { 0, 1, 2 };

            // Create a valid dummy Material (needs shader and enableInstancing)
            Shader standardShader = Shader.Find("Standard");
            // Fallback in case standard is stripped/missing in test context, we'll try something basic
            if (standardShader == null) standardShader = Shader.Find("Hidden/InternalErrorShader");

            Material dummyMaterial = new Material(standardShader);
            dummyMaterial.enableInstancing = true;

            // Create GameObject and add HectonScanMarkerSystem
            GameObject go = new GameObject("TestMarkerSystem");
            HectonScanMarkerSystem system = go.AddComponent<HectonScanMarkerSystem>();

            // Call Initialize
            // Should not throw exceptions if assertions pass
            Assert.DoesNotThrow(() => system.Initialize(dummyMesh, dummyMaterial));

            // Wait a frame
            yield return null;

            // Clean up
            GameObject.Destroy(go);
            Object.Destroy(dummyMaterial);
            Object.Destroy(dummyMesh);
        }
    }
}
