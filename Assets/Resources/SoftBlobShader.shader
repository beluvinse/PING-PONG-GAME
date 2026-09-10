// Unlit soft circle / ring, drawn fully in shader (no texture needed).
// Used for the ball's blob shadow (_InnerRadius = 0) and the landing
// marker ring (_InnerRadius > 0). Lives in Resources so it is always
// included in builds for Shader.Find.
Shader "Custom/SoftBlob"
{
    Properties
    {
        _Color ("Color", Color) = (0, 0, 0, 0.35)
        _InnerRadius ("Inner Radius (0 = solid blob)", Range(0, 0.95)) = 0
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.18
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _InnerRadius;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 0 at quad center, 1 at the inscribed circle edge
                float d = distance(i.uv, float2(0.5, 0.5)) * 2.0;

                float outerMask = 1.0 - smoothstep(1.0 - _Softness, 1.0, d);
                float innerMask = _InnerRadius > 0.001
                    ? smoothstep(_InnerRadius - _Softness, _InnerRadius, d)
                    : 1.0;

                return fixed4(_Color.rgb, _Color.a * outerMask * innerMask);
            }
            ENDCG
        }
    }
}
