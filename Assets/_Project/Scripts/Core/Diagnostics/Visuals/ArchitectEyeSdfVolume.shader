Shader "Hidden/Hecton8/Diagnostics/ArchitectEyeSdfVolume"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.9, 1.0, 0.25)
        _Density ("Density", Range(0, 1)) = 0.2
        _VisualTier ("Visual Tier", Range(0, 3)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Pass
        {
            ZWrite Off
            ZTest LEqual
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            float4 _Color;
            float _Density;
            float _VisualTier;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 local : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.local = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half3 p = abs((half3)i.local);
                half edge = 1.0 - saturate(min(min(p.x, p.y), p.z));
                half march = 0.0;
                int steps = _VisualTier > 2.5 ? 16 : (_VisualTier > 1.5 ? 8 : 3);

                [loop]
                for (int step = 0; step < 16; step++)
                {
                    if (step >= steps)
                        break;

                    half t = ((half)step + 0.5) / (half)steps;
                    half shell = saturate(1.0 - abs(edge - t) * (3.0 + (half)_VisualTier * 4.0));
                    half silt = frac((i.local.x * 12.9898 + i.local.y * 78.233 + i.local.z * 37.719 + t * 19.19) * 0.0243902);
                    march += shell * (0.65 + silt * 0.35);
                }

                march *= rcp((half)steps);
                half pulse = saturate(edge * 2.5 + (half)_Density * 0.5 + march * saturate((half)_VisualTier * 0.35));
                half3 glow = (half3)_Color.rgb + march * half3(0.05, 0.22, 0.35);
                return half4(glow, (half)_Color.a * pulse);
            }
            ENDHLSL
        }
    }
}
