Shader "Crest/Inputs/Albedo/Sargassum Oil Film"
{
    Properties
    {
        _MainTex("Density Texture", 2D) = "black" {}
        _OilTint("Oil Tint", Color) = (0.12, 0.19, 0.14, 0.78)
        _DensityPower("Density Power", Range(0.5, 4.0)) = 1.45
        _AlphaScale("Alpha Scale", Range(0.0, 1.0)) = 0.92
        _CutRelief("Cut Relief", Range(0.0, 1.0)) = 1.0
        _IridescenceStrength("Iridescence Strength", Range(0.0, 1.0)) = 0.34
        _IridescenceScale("Iridescence Scale", Range(0.01, 1.0)) = 0.12
        _IridescenceSpeed("Iridescence Speed", Range(0.0, 2.0)) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _SargassumCutMaskRT;

            float4 _OilTint;
            float _DensityPower;
            float _AlphaScale;
            float _CutRelief;
            float _IridescenceStrength;
            float _IridescenceScale;
            float _IridescenceSpeed;
            float4 _DensityWorldRect;
            float4 _SargassumGlobalDriftOffset;
            float4 _SargassumCutMaskWorldRect;
            float _SargassumCutMaskActive;

            struct Attributes
            {
                float4 vertex : POSITION;
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
                float3 positionWS = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.positionCS = UnityWorldToClipPos(positionWS);
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
                float alpha = saturate(effectiveDensity * _AlphaScale * _OilTint.a);
                float spectralPhase =
                    (densitySampleXZ.x + densitySampleXZ.y) * _IridescenceScale +
                    _Time.y * _IridescenceSpeed +
                    effectiveDensity * 3.7;
                float3 spectralShift = 0.5 + 0.5 * cos(float3(0.0, 2.0943951, 4.1887902) + spectralPhase);
                float spectralMask = effectiveDensity * effectiveDensity * _IridescenceStrength;
                float3 oilyColor = lerp(_OilTint.rgb, saturate(_OilTint.rgb + spectralShift * 0.65), spectralMask);
                return half4(oilyColor, alpha);
            }
            ENDCG
        }
    }
}
