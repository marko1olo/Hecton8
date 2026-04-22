Shader "Hidden/Hecton8/SargassumDampingFacadeCopy"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "WaveDampingMask"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragWave

            #include "UnityCG.cginc"

            sampler2D _DensityTex;
            sampler2D _CutMaskTex;
            float4 _DensityWorldRect;
            float4 _CutMaskWorldRect;
            float4 _GlobalDriftOffset;
            float _CutMaskActive;
            float _DensityPower;
            float _CutRelief;
            float _AlphaScale;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXZ : TEXCOORD1;
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
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;

                float worldSizeX = _DensityWorldRect.z > 0.0 ? 1.0 / _DensityWorldRect.z : 0.0;
                float worldSizeZ = _DensityWorldRect.w > 0.0 ? 1.0 / _DensityWorldRect.w : 0.0;
                output.worldXZ = float2(
                    _DensityWorldRect.x + input.uv.x * worldSizeX + _GlobalDriftOffset.x,
                    _DensityWorldRect.y + input.uv.y * worldSizeZ + _GlobalDriftOffset.z);
                return output;
            }

            half4 BuildMask(Varyings input, float alphaScale) : SV_Target
            {
                float density = tex2D(_DensityTex, input.uv).r;
                float cutMask = _CutMaskActive > 0.5
                    ? SampleRectTexture(_CutMaskTex, input.worldXZ, _CutMaskWorldRect)
                    : 0.0;
                float effectiveDensity = saturate(density * (1.0 - saturate(cutMask * _CutRelief)));
                float mask = pow(saturate(effectiveDensity), _DensityPower) * alphaScale;
                return half4(mask, mask, mask, mask);
            }

            half4 FragWave(Varyings input) : SV_Target
            {
                return BuildMask(input, 1.0);
            }

            ENDHLSL
        }

        Pass
        {
            Name "OilFilmMask"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragOil

            #include "UnityCG.cginc"

            sampler2D _DensityTex;
            sampler2D _CutMaskTex;
            float4 _DensityWorldRect;
            float4 _CutMaskWorldRect;
            float4 _GlobalDriftOffset;
            float _CutMaskActive;
            float _DensityPower;
            float _CutRelief;
            float _AlphaScale;

            struct Attributes
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 worldXZ : TEXCOORD1;
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
                output.positionCS = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;

                float worldSizeX = _DensityWorldRect.z > 0.0 ? 1.0 / _DensityWorldRect.z : 0.0;
                float worldSizeZ = _DensityWorldRect.w > 0.0 ? 1.0 / _DensityWorldRect.w : 0.0;
                output.worldXZ = float2(
                    _DensityWorldRect.x + input.uv.x * worldSizeX + _GlobalDriftOffset.x,
                    _DensityWorldRect.y + input.uv.y * worldSizeZ + _GlobalDriftOffset.z);
                return output;
            }

            half4 FragOil(Varyings input) : SV_Target
            {
                float density = tex2D(_DensityTex, input.uv).r;
                float cutMask = _CutMaskActive > 0.5
                    ? SampleRectTexture(_CutMaskTex, input.worldXZ, _CutMaskWorldRect)
                    : 0.0;
                float effectiveDensity = saturate(density * (1.0 - saturate(cutMask * _CutRelief)));
                float mask = pow(saturate(effectiveDensity), _DensityPower) * _AlphaScale;
                return half4(mask, mask, mask, mask);
            }

            ENDHLSL
        }
    }

    FallBack Off
}
