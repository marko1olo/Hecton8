Shader "Hecton8/UI/RetinaStressPulse"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.02, 0.82, 0.74, 1)
        _PulseStrength ("Pulse Strength", Range(0, 1)) = 0.42
        _PulseRate ("Pulse Rate", Range(0.1, 8)) = 1.35
        _GlitchStrength ("Glitch Strength", Range(0, 1)) = 0.18
        _LineDensity ("Line Density", Range(8, 240)) = 116
        _EdgeCrush ("Edge Crush", Range(0, 1)) = 0.72
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "False"
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
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            fixed4 _TintColor;
            float _PulseStrength;
            float _PulseRate;
            float _GlitchStrength;
            float _LineDensity;
            float _EdgeCrush;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.texcoord;
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(0.1031, 0.11369));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 screenUv = i.screenPos.xy / max(i.screenPos.w, 0.0001);
                float2 centered = (screenUv * 2.0) - 1.0;
                float radial = dot(centered, centered);
                float edgeMask = smoothstep(0.28, 1.08, radial);

                float beatPhase = frac(_Time.y * _PulseRate);
                float triangleBeat = 1.0 - abs((beatPhase * 2.0) - 1.0);
                float beat = triangleBeat * triangleBeat * (3.0 - (2.0 * triangleBeat));

                float scanPhase = frac((screenUv.y * _LineDensity) + (_Time.y * 0.37));
                float scan = smoothstep(0.48, 0.0, abs(scanPhase - 0.5));

                float2 glitchCell = floor(screenUv * float2(96.0, 54.0));
                float glitchNoise = Hash21(glitchCell + floor(_Time.y * 24.0));
                float glitchGate = step(0.986, glitchNoise);
                float glitchBand = smoothstep(0.78, 1.0, frac((screenUv.y * 13.0) - (_Time.y * 3.1)));

                float crush = edgeMask * _EdgeCrush;
                float pulseAlpha = crush * beat * _PulseStrength;
                float scanAlpha = scan * 0.055 * saturate(_PulseStrength + 0.2);
                float glitchAlpha = glitchGate * glitchBand * _GlitchStrength;

                fixed3 tint = _TintColor.rgb;
                fixed3 pressureTint = lerp(tint, fixed3(0.08, 0.34, 0.30), crush);
                float alpha = saturate(pulseAlpha + scanAlpha + glitchAlpha) * _TintColor.a * i.color.a;

                return fixed4(pressureTint * (1.0 + glitchAlpha * 0.65), alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
