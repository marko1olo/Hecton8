Shader "Hecton8/UI/IGNDitherDissolve"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0.002, 0.004, 0.009, 1)
        _DitherProgress ("Dither Progress", Range(0, 1)) = 1
        _SignalPulseStrength ("Signal Pulse Strength", Range(0, 0.25)) = 0.08
        _SignalPulseRate ("Signal Pulse Rate", Range(0, 12)) = 4
        _SignalTearStrength ("Signal Tear Strength", Range(0, 0.2)) = 0.05
        _SignalTearRate ("Signal Tear Rate", Range(0, 12)) = 5
        _SignalEdgeFlickerStrength ("Signal Edge Flicker Strength", Range(0, 0.18)) = 0.04
        _SignalEdgeFlickerRate ("Signal Edge Flicker Rate", Range(0, 16)) = 7
        _SignalChromaAliasStrength ("Signal Chroma Alias Strength", Range(0, 0.14)) = 0.025
        _SignalChromaAliasRate ("Signal Chroma Alias Rate", Range(0, 20)) = 9
        _SignalPhosphorStutterStrength ("Signal Phosphor Stutter Strength", Range(0, 0.16)) = 0.035
        _SignalPhosphorStutterRate ("Signal Phosphor Stutter Rate", Range(0, 18)) = 6
        _SignalWarningColor ("Signal Warning Color", Color) = (0.55, 0.28, 0.04, 1)
        _SignalWarningPulseStrength ("Signal Warning Pulse Strength", Range(0, 0.18)) = 0.045
        _SignalWarningPulseRate ("Signal Warning Pulse Rate", Range(0, 16)) = 3.5
        _SignalPressureRippleStrength ("Signal Pressure Ripple Strength", Range(0, 0.14)) = 0.032
        _SignalPressureRippleRate ("Signal Pressure Ripple Rate", Range(0, 18)) = 5.5
        _SignalNotchStrength ("Signal Notch Strength", Range(0, 0.12)) = 0.028
        _SignalNotchRate ("Signal Notch Rate", Range(0, 18)) = 4.8
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
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
            float4 _MainTex_ST;
            fixed4 _Color;
            float _DitherProgress;
            float _SignalPulseStrength;
            float _SignalPulseRate;
            float _SignalTearStrength;
            float _SignalTearRate;
            float _SignalEdgeFlickerStrength;
            float _SignalEdgeFlickerRate;
            float _SignalChromaAliasStrength;
            float _SignalChromaAliasRate;
            float _SignalPhosphorStutterStrength;
            float _SignalPhosphorStutterRate;
            fixed4 _SignalWarningColor;
            float _SignalWarningPulseStrength;
            float _SignalWarningPulseRate;
            float _SignalPressureRippleStrength;
            float _SignalPressureRippleRate;
            float _SignalNotchStrength;
            float _SignalNotchRate;
            float _HectonBrownoutPulse;

            float FastTrianglePulse01(float phase)
            {
                return 1.0 - abs(frac(phase * 0.15915494 + 0.25) * 2.0 - 1.0);
            }

            float Hash21(float2 value)
            {
                float3 hash = frac(float3(value.xyx) * float3(0.1031, 0.1030, 0.0973));
                hash += dot(hash, hash.yzx + 33.33);
                return frac((hash.x + hash.y) * hash.z);
            }

            float InterleavedGradientNoise(float2 pixel)
            {
                return frac(52.9829189 * frac(dot(pixel, float2(0.06711056, 0.00583715))));
            }

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
                float threshold = InterleavedGradientNoise(floor(screenUv * _ScreenParams.xy));
                float progress = saturate(_DitherProgress);
                float ditherAlpha = progress <= 0.0001 ? 0.0 : step(threshold, progress);
                fixed4 tex = tex2D(_MainTex, i.texcoord);
                fixed4 color = tex * i.color * _Color;
                float pulse = FastTrianglePulse01((_Time.y * _SignalPulseRate) + (screenUv.y * 38.0)) * _SignalPulseStrength;
                float scanline = smoothstep(0.82, 1.0, frac((screenUv.y * 96.0) - (_Time.y * 0.65)));
                float signalShift = pulse * scanline * progress;
                color.rgb *= 1.0 + signalShift;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(0.65, 1.08, 1.18), signalShift);
                float tearBand = smoothstep(0.94, 1.0, frac((screenUv.y * 17.0) + (_Time.y * _SignalTearRate)));
                float tearGate = step(0.74, frac((screenUv.x * 23.0) - (_Time.y * 1.37)));
                float tearShift = tearBand * tearGate * _SignalTearStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(1.18, 0.94, 0.72), tearShift);
                color.a = saturate(color.a + (tearShift * 0.05));
                float2 centeredUv = (screenUv * 2.0) - 1.0;
                float edgeMask = smoothstep(0.52, 1.08, dot(centeredUv, centeredUv));
                float edgeFlicker = FastTrianglePulse01((_Time.y * _SignalEdgeFlickerRate) + (screenUv.x * 41.0) + (screenUv.y * 29.0));
                float edgeShift = edgeMask * edgeFlicker * _SignalEdgeFlickerStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(0.78, 1.06, 1.22), edgeShift);
                float aliasHash = Hash21((screenUv * _ScreenParams.xy) + floor(_Time.y * _SignalChromaAliasRate));
                float aliasShift = step(0.88, aliasHash) * _SignalChromaAliasStrength * progress;
                color.rgb = lerp(color.rgb, color.gbr, aliasShift);
                float phosphorBand = smoothstep(0.91, 1.0, frac((screenUv.x * 7.0) + (screenUv.y * 3.0) + (_Time.y * _SignalPhosphorStutterRate)));
                float phosphorShift = phosphorBand * _SignalPhosphorStutterStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb * fixed3(0.92, 1.14, 0.96), phosphorShift);
                float warningSweep = smoothstep(0.86, 1.0, frac((_Time.y * _SignalWarningPulseRate) + (screenUv.y * 5.0)));
                float warningMask = warningSweep * edgeMask * _SignalWarningPulseStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb + (_SignalWarningColor.rgb * color.a), warningMask);
                float pressureWave = FastTrianglePulse01(((screenUv.x * 31.0) + (screenUv.y * 19.0)) + (_Time.y * _SignalPressureRippleRate));
                float pressureMask = pressureWave * edgeMask * _SignalPressureRippleStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb + (_SignalWarningColor.rgb * 0.12), pressureMask);
                float notchPhase = frac((screenUv.x * 3.0) + (screenUv.y * 11.0) - (_Time.y * _SignalNotchRate));
                float notchMask = smoothstep(0.965, 1.0, notchPhase) * _SignalNotchStrength * progress;
                color.rgb = lerp(color.rgb, color.rgb * (1.0 - (edgeMask * 0.42)), notchMask);
                float dropoutBand = smoothstep(0.982, 1.0, frac((screenUv.y * 37.0) + (_Time.y * 2.7)));
                float dropoutGate = step(0.71, frac((screenUv.x * 5.0) - (_Time.y * 0.23)));
                color.rgb = lerp(color.rgb, color.rgb * fixed3(0.72, 0.9, 1.08), dropoutBand * dropoutGate * 0.025 * progress);
                float brownoutPulse = saturate(_HectonBrownoutPulse);
                float brownoutScan = smoothstep(0.78, 1.0, frac((screenUv.y * 11.0) + (_Time.y * 0.31)));
                float brownoutMask = brownoutPulse * (0.35 + (0.65 * edgeMask)) * (0.65 + (0.35 * brownoutScan)) * progress;
                color.rgb = lerp(color.rgb, (color.rgb * fixed3(1.12, 0.82, 0.55)) + (_SignalWarningColor.rgb * 0.18), brownoutMask);
                color.a = saturate(color.a + (brownoutPulse * edgeMask * 0.035));
                color.a *= ditherAlpha;
                return color;
            }
            ENDCG
        }
    }
}
