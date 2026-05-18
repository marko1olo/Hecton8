Shader "Hecton8/Rendering/UberNoir"
{
    Properties
    {
        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        _MaskMap("Packed ORM Emission", 2D) = "white" {}
        [Normal] _BumpMap("Normal", 2D) = "bump" {}
        _RustDetailMap("Rust Height Normal", 2D) = "gray" {}
        _BlueNoiseTex("Blue Noise", 2D) = "gray" {}
        _HectonCausticsMap("Caustics Atlas", 2D) = "gray" {}
        [NoScaleOffset] _H8UberNoirAlbedoArray("UberNoir Albedo Array", 2DArray) = "" {}
        [NoScaleOffset] _H8UberNoirNormalArray("UberNoir Normal Array", 2DArray) = "" {}
        [NoScaleOffset] _H8UberNoirMaskArray("UberNoir Mask Array", 2DArray) = "" {}

        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 0)
        _RustTint("Rust Tint", Color) = (0.45, 0.24, 0.10, 1)
        _RustPitTint("Rust Pit Tint", Color) = (0.12, 0.055, 0.032, 1)
        [HDR] _BiolumLowColor("Biolum Low", Color) = (0.0, 0.18, 0.34, 1)
        [HDR] _BiolumHighColor("Biolum High", Color) = (0.24, 0.95, 1.35, 1)
        _NoirAbyssFloorColor("Abyss Floor", Color) = (0.005, 0.008, 0.012, 1)
        _NoirFogColor("Noir Fog", Color) = (0.015, 0.025, 0.035, 1)
        [HDR] _UberNoirCausticColor("Caustic Color", Color) = (0.18, 0.62, 0.72, 1)

        _UberNoirFeatureFlags("Feature Flags", Vector) = (1, 1, 1, 1)
        _UberNoirInstanceParams("Instance Params", Vector) = (0, 0, 0, 0)
        _UberNoirParallaxParams("Parallax Params", Vector) = (0.035, 0.16, 0, 0)
        _UberNoirRustParams("Rust Params", Vector) = (1, 0.3, 0.65, 0.9)
        _UberNoirBendParams("Bend Params", Vector) = (1, 0.22, 1, 0)
        _UberNoirCausticParams("Caustic Params", Vector) = (0.35, 30, 1, 0.025)
        _UberNoirBiolumParams("Biolum Params", Vector) = (1, 0.35, 4, 1)
        _UberNoirDitherParams("Dither Params", Vector) = (0.5, 0, 1, 1)
        _UberNoirLightingParams("Lighting Params", Vector) = (0.35, 0.08, 0.35, 1)
        _UberNoirRefractionParams("Refraction Params", Vector) = (0, 0.5, 0, 0)
        _UberNoirIorLut("IOR LUT Air Water Dense Glass", Vector) = (1.0003, 1.333, 1.38, 1.46)

        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.72
        _OcclusionStrength("Occlusion", Range(0, 1)) = 1
        _BumpScale("Normal Scale", Range(0, 2)) = 1
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.5
        _NoirFogAlpha("Noir Fog Alpha", Range(0, 1)) = 0.62

        [HideInInspector] _UberNoirPadding0("Padding 0", Float) = 0
        [HideInInspector] _UberNoirPadding1("Padding 1", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "UniversalMaterialType" = "Lit"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex H8UberNoirVertex
            #pragma fragment H8UberNoirFragment

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ _MATH_LOD_LOW
            #pragma multi_compile _ H8_UBERNOIR_USE_INSTANCE_BUFFER
            #pragma shader_feature_local _ H8_UBERNOIR_CAUSTICS_TEXTURED
            #pragma shader_feature_local _ H8_UBERNOIR_SCREEN_REFRACTION
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #pragma skip_variants SHADOWS_SHADOWMASK DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _SCREEN_SPACE_OCCLUSION

            #include "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "MotionVectors"
            Tags { "LightMode" = "MotionVectors" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask RG

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex H8UberNoirMotionVertex
            #pragma fragment H8UberNoirMotionFragment

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MATH_LOD_LOW
            #pragma multi_compile _ H8_UBERNOIR_USE_INSTANCE_BUFFER
            #define H8_UBERNOIR_MOTION_VECTOR_PASS 1

            #include "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            Cull Back
            ZWrite On
            ZTest LEqual
            ColorMask 0
            AlphaToMask On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex H8UberNoirShadowVertex
            #pragma fragment H8UberNoirShadowFragment

            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling renderinglayer
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma multi_compile _ _MATH_LOD_LOW
            #pragma multi_compile _ H8_UBERNOIR_USE_INSTANCE_BUFFER
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #define H8_UBERNOIR_SHADOW_CASTER_PASS 1

            #include "Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl"
            ENDHLSL
        }
    }

    FallBack Off
}
