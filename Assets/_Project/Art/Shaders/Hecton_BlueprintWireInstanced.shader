Shader "Hecton8/Fabrication/BlueprintWireInstanced"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.08, 1.0, 0.72, 0.72)
        _GridScale("Grid Scale", Float) = 7
        _LineThickness("Line Thickness", Range(0.001, 0.25)) = 0.055
        _FlickerSpeed("Flicker Speed", Float) = 26
        _FlickerCutoff("Flicker Cutoff", Range(0, 1)) = 0.18
        _H8SnapDampen("Snap Dampen", Float) = 0
        _H8SnapWiggleSpeed("Snap Wiggle Speed", Float) = 18
        _H8GlobalQualityWeight("Global Quality Weight", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
        }

        Pass
        {
            Name "BlueprintWire"
            Blend Off
            ZWrite On
            AlphaToMask On
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
                float _H8SnapDampen;
                float _H8SnapWiggleSpeed;
                float _H8GlobalQualityWeight;
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
                float q = saturate(_H8GlobalQualityWeight);
                float smoothQ = q * q * (3.0 - 2.0 * q);
                float phase = dot(input.positionOS.xyz, float3(19.0, 31.0, 43.0)) + (_Time.y * _H8SnapWiggleSpeed);
                float wave = sin(phase);
                float3 normalOS = HectonCoreLitSafeNormalize(input.normalOS);
                float amplitude = max(0.0, _H8SnapDampen) * lerp(0.25, 1.0, smoothQ);
                float3 positionOS = input.positionOS.xyz - normalOS * amplitude + normalOS * wave * amplitude * 0.35;
                output.positionWS = TransformObjectToWorld(positionOS);
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
                half dither = (half)HectonCoreLitTaaAccumulatedInterleavedGradientNoise(floor(input.positionCS.xy));
                clip(alpha - dither);
                return half4(_BaseColor.rgb * (half)(1.0 + fresnel * 2.4), 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
