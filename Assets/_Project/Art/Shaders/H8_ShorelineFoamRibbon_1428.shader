Shader "HECTON/World/H8_ShorelineFoamRibbon_1428"
{
    Properties
    {
        _BaseMap ("Foam Texture", 2D) = "white" {}
        [HDR] _FoamColor ("Foam Color", Color) = (0.82, 0.96, 0.92, 0.48)
        _Alpha ("Alpha", Range(0, 1)) = 0.48
        _Threshold ("Threshold", Range(0, 1)) = 0.38
        _Softness ("Softness", Range(0.01, 0.6)) = 0.22
        _EdgeFade ("Edge Fade", Range(0.001, 0.5)) = 0.11
        _TilingA ("Tiling A", Vector) = (2.4, 0.72, 0.018, 0.004)
        _TilingB ("Tiling B", Vector) = (4.7, 1.35, -0.011, 0.006)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent+12"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "ForceNoShadowCasting" = "True"
        }

        Pass
        {
            Name "ShorelineFoamRibbon"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma skip_variants DIRLIGHTMAP_COMBINED LIGHTMAP_ON DYNAMICLIGHTMAP_ON
            #pragma skip_variants _ADDITIONAL_LIGHTS _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHT_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _FoamColor;
                float _Alpha;
                float _Threshold;
                float _Softness;
                float _EdgeFade;
                float4 _TilingA;
                float4 _TilingB;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.uv;
                float2 flowA = uv * _TilingA.xy + _Time.y * _TilingA.zw;
                float2 flowB = uv * _TilingB.xy + _Time.y * _TilingB.zw + float2(0.37, 0.11);
                float2 flowBreak = uv * float2(_TilingB.x * 1.73, max(0.17, _TilingA.y * 0.47)) + _Time.y * float2(-0.006, 0.002) + float2(0.19, 0.61);
                half4 foamSampleA = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowA);
                half4 foamSampleB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowB);
                half4 foamSampleBreak = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, flowBreak);
                half foamA = foamSampleA.r;
                half foamB = foamSampleB.g;
                half foamBreak = foamSampleBreak.b;
                half signal = saturate(foamA * 0.72h + foamB * 0.42h);
                half foamMask = smoothstep((half)_Threshold, (half)(_Threshold + _Softness), signal);
                half breakup = smoothstep(0.38h, 0.86h, saturate(foamBreak * 0.78h + foamB * 0.24h));
                half strand = smoothstep(0.12h, 0.88h, abs(frac(uv.x * 11.0 + foamBreak * 0.37h) - 0.5h) * 2.0h);
                half alphaGate = smoothstep(0.08h, 0.72h, saturate(foamSampleA.a * 0.52h + foamSampleB.a * 0.33h + foamSampleBreak.a * 0.28h));
                foamMask *= saturate(breakup * (0.46h + strand * 0.72h));
                foamMask *= alphaGate;

                half edgeX = min((half)uv.x, (half)(1.0 - uv.x));
                half edgeY = min((half)uv.y, (half)(1.0 - uv.y));
                half edge = smoothstep(0.0h, (half)_EdgeFade, min(edgeX, edgeY));
                half cameraAboveSurface = step(input.positionWS.y - 0.04h, _WorldSpaceCameraPos.y);
                half alpha = saturate(foamMask * edge * (half)_Alpha * _FoamColor.a) * cameraAboveSurface;
                half3 color = _FoamColor.rgb * (0.62h + signal * 0.48h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
