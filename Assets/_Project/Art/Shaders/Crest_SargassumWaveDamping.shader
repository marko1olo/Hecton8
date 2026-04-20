Shader "Crest/Inputs/Animated Waves/Sargassum Damping"
{
    Properties
    {
        _MainTex("Density Texture", 2D) = "black" {}
        _SuppressionFloor("Suppression Floor", Range(0.0, 1.0)) = 0.22
        _DensityPower("Density Power", Range(0.5, 4.0)) = 1.35
        _CutRelief("Cut Relief", Range(0.0, 1.0)) = 1.0
    }

    SubShader
    {
        Pass
        {
            Blend Zero SrcColor

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "../../../Crest/Crest/Shaders/OceanGlobals.hlsl"
            #include "../../../Crest/Crest/Shaders/OceanInputsDriven.hlsl"
            #include "../../../Crest/Crest/Shaders/OceanHelpersNew.hlsl"

            sampler2D _MainTex;
            sampler2D _SargassumCutMaskRT;

            CBUFFER_START(CrestPerOceanInput)
            float _Weight;
            float3 _DisplacementAtInputPosition;
            float _SuppressionFloor;
            float _DensityPower;
            float _CutRelief;
            CBUFFER_END

            float4 _DensityWorldRect;
            float4 _SargassumGlobalDriftOffset;
            float4 _SargassumCutMaskWorldRect;
            float _SargassumCutMaskActive;

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldXZ : TEXCOORD0;
            };

            float SampleRectTexture(sampler2D textureSampler, float2 worldXZ, float4 worldRect)
            {
                float2 uv = (worldXZ - worldRect.xy) * worldRect.zw;
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return 0.0;

                return tex2D(textureSampler, uv).r;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                positionWS.xz -= _DisplacementAtInputPosition.xz;
                output.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                output.worldXZ = positionWS.xz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 densitySampleXZ = input.worldXZ - _SargassumGlobalDriftOffset.xz;
                float density = SampleRectTexture(_MainTex, densitySampleXZ, _DensityWorldRect);
                float cutMask = _SargassumCutMaskActive > 0.5
                    ? SampleRectTexture(_SargassumCutMaskRT, input.worldXZ, _SargassumCutMaskWorldRect)
                    : 0.0;

                float effectiveDensity = saturate(density * (1.0 - saturate(cutMask * _CutRelief)));
                effectiveDensity = pow(effectiveDensity, _DensityPower);
                float scale = lerp(1.0, _SuppressionFloor, effectiveDensity);
                return half4(scale, scale, scale, scale) * _Weight;
            }
            ENDCG
        }
    }
}
