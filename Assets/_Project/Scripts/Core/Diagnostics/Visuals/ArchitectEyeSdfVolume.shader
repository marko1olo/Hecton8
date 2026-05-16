Shader "Hidden/Hecton8/Diagnostics/ArchitectEyeSdfVolume"
{
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.9, 1.0, 0.25)
        _Density ("Density", Range(0, 1)) = 0.2
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
                float3 p = abs(i.local);
                float edge = 1.0 - saturate(min(min(p.x, p.y), p.z));
                float pulse = saturate(edge * 2.5 + _Density * 0.5);
                return half4(_Color.rgb, _Color.a * pulse);
            }
            ENDHLSL
        }
    }
}
