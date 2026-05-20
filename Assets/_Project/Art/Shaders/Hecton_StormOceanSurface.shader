Shader "Hecton/Environment/Storm Ocean Surface"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.018, 0.084, 0.112, 0.78)
        _FoamColor ("Foam Color", Color) = (0.78, 0.9, 0.94, 1.0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.82
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
            Name "StormOceanForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Hecton_OceanSurfaceAtmosphere.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FoamColor;
                half _Smoothness;
                half3 _StormOceanPad0;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float foam : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS);
                float2 cameraLocalXZ = positionWS.xz - _WorldSpaceCameraPos.xz;

                float3 displacement;
                float3 normalWS;
                float foamScalar;
                H8EvaluateOceanSurface(cameraLocalXZ, displacement, normalWS, foamScalar);

                positionWS += displacement;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.normalWS = H8OceanNormalize3(normalWS, float3(0.0, 1.0, 0.0));
                output.positionWS = positionWS;
                output.foam = saturate(foamScalar);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half fresnel = (half)pow(saturate(1.0 - input.normalWS.y), 2.0);
                half foam = (half)input.foam;
                half3 baseColor = lerp(_BaseColor.rgb, _FoamColor.rgb, foam);
                baseColor += fresnel * _Smoothness * half3(0.05, 0.11, 0.14);
                return half4(baseColor, saturate(_BaseColor.a + foam * 0.18h));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
