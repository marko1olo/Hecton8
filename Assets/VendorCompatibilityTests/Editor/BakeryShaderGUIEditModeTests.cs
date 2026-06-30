using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VendorCompatibility.Tests.Editor
{
    public class BakeryShaderGUIEditModeTests
    {
        [Test]
        public void BakeryShaderGUI_FindProperties_CatchesMissingVolumeProperties()
        {
            string shaderSource = @"
Shader ""Hidden/TestBakeryShader"" {
    Properties {
        _Mode (""Mode"", Float) = 0
        _MainTex (""MainTex"", 2D) = ""white"" {}
        _Color (""Color"", Color) = (1,1,1,1)
        _Cutoff (""Cutoff"", Float) = 0.5
        _SpecGlossMap (""SpecGlossMap"", 2D) = ""white"" {}
        _SpecColor (""SpecColor"", Color) = (1,1,1,1)
        _MetallicGlossMap (""MetallicGlossMap"", 2D) = ""white"" {}
        _Metallic (""Metallic"", Float) = 0
        _Glossiness (""Glossiness"", Float) = 0.5
        _GlossMapScale (""GlossMapScale"", Float) = 1
        _SmoothnessTextureChannel (""SmoothnessTextureChannel"", Float) = 0
        _SpecularHighlights (""SpecularHighlights"", Float) = 1
        _GlossyReflections (""GlossyReflections"", Float) = 1
        _BumpScale (""BumpScale"", Float) = 1
        _BumpMap (""BumpMap"", 2D) = ""bump"" {}
        _Parallax (""Parallax"", Float) = 0.02
        _ParallaxMap (""ParallaxMap"", 2D) = ""black"" {}
        _OcclusionStrength (""OcclusionStrength"", Float) = 1
        _OcclusionMap (""OcclusionMap"", 2D) = ""white"" {}
        _EmissionColor (""EmissionColor"", Color) = (0,0,0,0)
        _EmissionMap (""EmissionMap"", 2D) = ""white"" {}
        _DetailMask (""DetailMask"", 2D) = ""white"" {}
        _DetailAlbedoMap (""DetailAlbedoMap"", 2D) = ""grey"" {}
        _DetailNormalMapScale (""DetailNormalMapScale"", Float) = 1
        _DetailNormalMap (""DetailNormalMap"", 2D) = ""bump"" {}
        _UVSec (""UVSec"", Float) = 0
        _BAKERY_2SIDED (""BAKERY_2SIDED"", Float) = 0
        _BAKERY_2SIDEDON (""BAKERY_2SIDEDON"", Float) = 0
        _BAKERY_VERTEXLM (""BAKERY_VERTEXLM"", Float) = 0
        _BAKERY_VERTEXLMDIR (""BAKERY_VERTEXLMDIR"", Float) = 0
        _BAKERY_VERTEXLMSH (""BAKERY_VERTEXLMSH"", Float) = 0
        _BAKERY_VERTEXLMMASK (""BAKERY_VERTEXLMMASK"", Float) = 0
        _BAKERY_SH (""BAKERY_SH"", Float) = 0
        _BAKERY_MONOSH (""BAKERY_MONOSH"", Float) = 0
        _BAKERY_SHNONLINEAR (""BAKERY_SHNONLINEAR"", Float) = 0
        _BAKERY_RNM (""BAKERY_RNM"", Float) = 0
        _BAKERY_LMSPEC (""BAKERY_LMSPEC"", Float) = 0
        _BAKERY_BICUBIC (""BAKERY_BICUBIC"", Float) = 0
        _BAKERY_PROBESHNONLINEAR (""BAKERY_PROBESHNONLINEAR"", Float) = 0
        // Purposely missing _BAKERY_VOLUME and others here to trigger the catch block
    }
    SubShader { Pass { } }
}";

            Material mat = null;
            string assetPath = "Assets/VendorCompatibilityTests/Editor/TempTestBakeryShader.shader";
            try
            {
                System.IO.File.WriteAllText(assetPath, shaderSource);
                AssetDatabase.ImportAsset(assetPath);

                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, "Failed to load generated shader");

                mat = new Material(shader);
                MaterialProperty[] props = MaterialEditor.GetMaterialProperties(new UnityEngine.Object[] { mat });

                BakeryShaderGUI gui = new BakeryShaderGUI();

                Assert.DoesNotThrow(() => gui.FindProperties(props));

                FieldInfo enableVolumesField = typeof(BakeryShaderGUI).GetField("enableVolumes", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                if (enableVolumesField != null)
                {
                    Assert.IsNull(enableVolumesField.GetValue(gui));
                }
            }
            finally
            {
                if (mat != null)
                {
                    UnityEngine.Object.DestroyImmediate(mat);
                }

                if (System.IO.File.Exists(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }
    }
}
