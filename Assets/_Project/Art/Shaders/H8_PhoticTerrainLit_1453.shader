Shader "Hecton8/World/PhoticTerrainLit1453"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _Tint ("Tint", Color) = (0.86, 0.90, 0.91, 1.0)
        _ShadowTint ("Water Shadow Tint", Color) = (0.11, 0.17, 0.21, 1.0)
        _RidgeTint ("Ridge Mineral Tint", Color) = (0.70, 0.76, 0.78, 1.0)
        _CausticColor ("Caustic Color", Color) = (0.58, 0.82, 1.0, 1.0)
        _CausticStrength ("Caustic Strength", Range(0.0, 1.5)) = 0.34
        _TextureScale ("Texture Scale", Range(0.02, 4.0)) = 0.22
        _FillLight ("Fill Light", Range(0.0, 2.0)) = 0.88
        _WetSpec ("Wet Spec Fake", Range(0.0, 1.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                half4 _ShadowTint;
                half4 _RidgeTint;
                half4 _CausticColor;
                half _CausticStrength;
                half _TextureScale;
                half _FillLight;
                half _WetSpec;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half CausticLine(float2 p, half scale, half phase)
            {
                half a = 0.5h + 0.5h * sin(p.x * scale + p.y * (scale * 0.31h) + phase);
                half b = 0.5h + 0.5h * sin(p.y * (scale * 0.83h) - p.x * (scale * 0.27h) + phase * 1.37h);
                return pow(saturate(a * b), 7.0h);
            }

            half3 SampleBaseTriplanar(float3 positionWS, half3 normalWS)
            {
                half3 weights = pow(abs(normalWS), 4.0h);
                weights *= rcp(max(weights.x + weights.y + weights.z, 0.0001h));

                float scale = max(_TextureScale, 0.02h);
                half3 xProj = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.zy * scale).rgb;
                half3 yProj = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xz * scale).rgb;
                half3 zProj = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, positionWS.xy * scale).rgb;

                return xProj * weights.x + yProj * weights.y + zProj * weights.z;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 n = normalize(input.normalWS);
                half top = saturate(n.y * 0.5h + 0.5h);
                half3 tex = SampleBaseTriplanar(input.positionWS, n);

                half ridge = 0.5h + 0.5h * sin(input.positionWS.y * 2.2h + input.positionWS.x * 0.13h + input.positionWS.z * 0.17h);
                half mineral = saturate(ridge * 0.52h + input.color.r * 0.48h);
                half3 rock = tex * _Tint.rgb;
                rock = lerp(rock, rock * _RidgeTint.rgb * 1.12h, mineral * 0.42h);

                half waterFill = _FillLight * (0.54h + top * 0.46h);
                half3 shaded = lerp(_ShadowTint.rgb, rock, waterFill);

                half caustic = CausticLine(input.positionWS.xz, 0.42h, 1.1h) + CausticLine(input.positionWS.xz + 19.3h, 0.71h, 3.4h) * 0.55h;
                half wet = pow(saturate(1.0h - abs(n.y)), 3.0h) * _WetSpec;
                half3 col = shaded + _CausticColor.rgb * caustic * _CausticStrength * saturate(input.color.a);
                col += half3(0.17h, 0.22h, 0.24h) * wet;

                return half4(max(col, half3(0.11h, 0.16h, 0.18h)), 1.0h);
            }
            ENDHLSL
        }
    }
}
