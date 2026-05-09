Shader "Hecton8/Fabrication/BlueprintWireInstanced"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.08, 1.0, 0.72, 0.72)
        _GridScale("Grid Scale", Float) = 7
        _LineThickness("Line Thickness", Range(0.001, 0.25)) = 0.055
        _FlickerSpeed("Flicker Speed", Float) = 26
        _FlickerCutoff("Flicker Cutoff", Range(0, 1)) = 0.18
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "BlueprintWire"
            Blend SrcAlpha One
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON _ADDITIONAL_LIGHT_SHADOWS
            #pragma skip_variants POINT POINT_COOKIE _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH LIGHTMAP_SHADOW_MIXING SHADOWS_SHADOWMASK

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _GridScale;
                float _LineThickness;
                float _FlickerSpeed;
                float _FlickerCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                HECTON_CORE_LIT_DECLARE_VERTEX_INPUT_INSTANCE_ID
                HECTON_CORE_LIT_DECLARE_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_TRANSFER_INSTANCE_ID(input, output);
                HECTON_CORE_LIT_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                HECTON_CORE_LIT_SETUP_INSTANCE_ID(input);
                HECTON_CORE_LIT_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 absolutePosition = input.positionWS + _TotalUniverseOffset.xyz;
                float flickerGate = HectonCoreLitHologramFlickerGate(
                    input.positionCS,
                    absolutePosition,
                    _Time.y,
                    _FlickerSpeed,
                    _FlickerCutoff);
                clip(flickerGate);

                float3 grid = abs(frac(absolutePosition * max(0.001, _GridScale)) - 0.5);
                float nearestLine = min(grid.x, min(grid.y, grid.z));
                float wire = 1.0 - smoothstep(_LineThickness, _LineThickness + 0.035, nearestLine);
                float3 normalWS = HectonCoreLitSafeNormalize(input.normalWS);
                float3 viewDirWS = HectonCoreLitSafeNormalize(GetCameraPositionWS() - input.positionWS);
                float fresnelBase = 1.0 - saturate(abs(dot(normalWS, viewDirWS)));
                float fresnel = fresnelBase * fresnelBase * lerp(1.0, fresnelBase, 0.2);
                float crawl = HectonCoreLitTrianglePulse01(_Time.y * _FlickerSpeed + dot(floor(input.positionCS.xy), float2(0.017, 0.031)));
                half alpha = (half)saturate(_BaseColor.a * (wire + fresnel * 0.55) * lerp(0.62, 1.15, crawl));
                return half4(_BaseColor.rgb * (half)(1.0 + fresnel * 2.4), alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
