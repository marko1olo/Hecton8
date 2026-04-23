Shader "Hecton8/Environment/Hecton_VoxelBakeGhost"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.045, 0.068, 0.082, 1)
        [HDR] _EdgeColor ("Edge Color", Color) = (0.16, 0.38, 0.46, 1)
        [HDR] _EmissionColor ("Emission Color", Color) = (0.0, 0.16, 0.22, 1)
        _Opacity ("Opacity", Range(0.05, 1.0)) = 0.42
        _InstabilityScale ("Instability Scale", Range(0.1, 8.0)) = 1.4
        _InstabilitySpeed ("Instability Speed", Range(0.0, 6.0)) = 1.25
        _InstabilityStrength ("Instability Strength", Range(0.0, 1.0)) = 0.28
        _DitherBias ("Dither Bias", Range(-0.5, 0.5)) = 0.0
        _FresnelPower ("Fresnel Power", Range(0.5, 8.0)) = 2.3
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "UniversalMaterialType" = "Unlit"
            "ForceNoShadowCasting" = "True"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GhostForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                half4 _EmissionColor;
                half _Opacity;
                half _InstabilityScale;
                half _InstabilitySpeed;
                half _InstabilityStrength;
                half _DitherBias;
                half _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 viewDirWS : TEXCOORD2;
            };

            float Hash31(float3 value)
            {
                value = frac(value * 0.1031);
                value += dot(value, value.yzx + 33.33);
                return frac((value.x + value.y) * value.z);
            }

            half ResolveInterleavedGradientNoise(float2 positionCS)
            {
                float2 pixel = floor(positionCS);
                return (half)frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(positionInputs.positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = SafeNormalize(input.normalWS);
                half3 viewDirWS = SafeNormalize(input.viewDirWS);
                half fresnel = pow(saturate(1.0h - dot(normalWS, viewDirWS)), _FresnelPower);

                float instabilitySeed = Hash31(
                    input.positionWS * max((float)_InstabilityScale, 0.001) +
                    _Time.y * max((float)_InstabilitySpeed, 0.0));
                half instability = (half)instabilitySeed;
                half threshold = ResolveInterleavedGradientNoise(input.positionCS.xy);

                half alpha =
                    saturate(
                        _Opacity +
                        (instability - 0.5h) * _InstabilityStrength +
                        fresnel * 0.18h -
                        _DitherBias);

                clip(alpha - threshold);

                half edgeBlend = saturate(fresnel * 0.82h + instability * 0.28h);
                half3 color = lerp(_BaseColor.rgb, _EdgeColor.rgb, edgeBlend);
                color += _EmissionColor.rgb * (0.35h + fresnel * 0.65h);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
