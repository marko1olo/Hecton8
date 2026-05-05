Shader "Hecton8/UI/BlueNoiseDitherDissolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BlueNoiseTex ("Blue Noise", 2D) = "white" {}
        _Color ("Color", Color) = (0.002, 0.004, 0.009, 1)
        _DitherProgress ("Dither Progress", Range(0, 1)) = 1
        _SignalPulseStrength ("Signal Pulse Strength", Range(0, 0.25)) = 0.08
        _SignalPulseRate ("Signal Pulse Rate", Range(0, 12)) = 4
        _SignalTearStrength ("Signal Tear Strength", Range(0, 0.2)) = 0.05
        _SignalTearRate ("Signal Tear Rate", Range(0, 12)) = 5
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            sampler2D _BlueNoiseTex;
            float4 _MainTex_ST;
            float4 _BlueNoiseTex_TexelSize;
            fixed4 _Color;
            float _DitherProgress;
            float _SignalPulseStrength;
            float _SignalPulseRate;
            float _SignalTearStrength;
            float _SignalTearRate;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUv = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                float2 temporalOffset = frac(_Time.y * float2(0.75487766, 0.56984029));
                float2 noiseUv = frac((screenUv * _ScreenParams.xy * _BlueNoiseTex_TexelSize.xy) + temporalOffset);
                float threshold = tex2D(_BlueNoiseTex, noiseUv).r;
                float progress = saturate(_DitherProgress);
                float ditherAlpha = progress <= 0.0001 ? 0.0 : step(threshold, progress);
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                fixed4 color = tex * i.color * _Color;
                float pulse = (sin((_Time.y * _SignalPulseRate) + (screenUv.y * 38.0)) * 0.5 + 0.5) * _SignalPulseStrength;
                float scanline = smoothstep(0.82, 1.0, frac((screenUv.y * 96.0) - (_Time.y * 0.65)));
                float signalShift = pulse * scanline * progress;
                color.rgb *= 1.0 + signalShift;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(0.65, 1.08, 1.18), signalShift);
                float tearBand = smoothstep(0.94, 1.0, frac((screenUv.y * 17.0) + (_Time.y * _SignalTearRate)));
                float tearGate = step(0.74, frac((screenUv.x * 23.0) - (_Time.y * 1.37)));
                float tearShift = tearBand * tearGate * _SignalTearStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(1.18, 0.94, 0.72), tearShift);
                color.a = saturate(color.a + (tearShift * 0.05));
                color.a *= ditherAlpha;
                return color;
            }
            ENDCG
        }
    }
}
