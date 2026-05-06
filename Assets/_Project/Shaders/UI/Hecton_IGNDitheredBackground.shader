Shader "Hecton8/UI/IGNDitheredBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OpacityScale ("Opacity Scale", Range(0, 1)) = 1
        _DitherBias ("Dither Bias", Range(-0.25, 0.25)) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Overlay"
            "IgnoreProjector"="True"
            "RenderType"="TransparentCutout"
            "RenderPipeline"="UniversalPipeline"
            "CanUseSpriteAtlas"="True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One Zero
        ColorMask RGB

        Pass
        {
            Name "IGNDitherClip"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _OpacityScale;
                float _DitherBias;
            CBUFFER_END

            v2f vert(appdata_t input)
            {
                v2f output;
                output.vertex = TransformObjectToHClip(input.vertex.xyz);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                output.screenPos = ComputeScreenPos(output.vertex);
                return output;
            }

            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

            half4 frag(v2f input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.texcoord);
                half4 color = texel * input.color;
                float2 screenUv = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float threshold = InterleavedGradientNoise(floor(screenUv * _ScreenParams.xy));
                float coverage = saturate(color.a * _OpacityScale + _DitherBias);
                clip(coverage - threshold);
                return half4(color.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
