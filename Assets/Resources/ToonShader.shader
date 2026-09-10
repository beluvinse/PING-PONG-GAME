// Soft cartoon shader for the built-in render pipeline.
// Look: bright and warm, one soft shadow band with a warm tint (never goes dark),
// slight saturation boost, subtle rim - inspired by casual mobile sports games.
// Uses the same property names as Standard (_Color, _MainTex) so materials
// keep their colors and textures when their shader is swapped to this one.
Shader "Custom/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}

        [Header(Soft Shading)]
        _ShadowThreshold ("Shadow Threshold", Range(0, 1)) = 0.5
        _ShadowSoftness ("Shadow Softness", Range(0.01, 0.5)) = 0.16
        _ShadowColor ("Shadow Tint (warm)", Color) = (0.82, 0.66, 0.62, 1)

        [Header(Color Grading)]
        _Saturation ("Saturation Boost", Range(0.5, 2)) = 1.15
        _WarmTint ("Warm Tint", Color) = (1.04, 1.0, 0.94, 1)

        [Header(Rim)]
        _RimColor ("Rim Color", Color) = (1, 0.95, 0.85, 0.18)
        _RimPower ("Rim Power", Range(0.5, 8)) = 4

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.45, 0.3, 0.25, 1)
        _OutlineWidth ("Outline Width (world units)", Range(0, 0.01)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        // ---- Optional outline (off by default: width 0). Back faces pushed out along normals ----
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);
                worldPos += worldNormal * _OutlineWidth;
                o.pos = UnityWorldToClipPos(float4(worldPos, 1.0));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(_OutlineColor.rgb, _OutlineColor.a * _Color.a);
            }
            ENDCG
        }

        // ---- Main soft-shaded pass ----
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ShadowThreshold;
            float _ShadowSoftness;
            fixed4 _ShadowColor;
            float _Saturation;
            fixed4 _WarmTint;
            fixed4 _RimColor;
            float _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);

                // Half-Lambert keeps everything bright, then one soft shadow band
                float ndl = dot(n, l) * 0.5 + 0.5;
                float shade = smoothstep(_ShadowThreshold - _ShadowSoftness,
                                         _ShadowThreshold + _ShadowSoftness, ndl);

                // Shadows are a warm tint of the base color, never plain dark
                float3 litCol = albedo.rgb * _LightColor0.rgb;
                float3 shadowCol = albedo.rgb * _ShadowColor.rgb * _LightColor0.rgb;
                float3 col = lerp(shadowCol, litCol, shade);

                // Ambient keeps the whole scene airy
                col += albedo.rgb * ShadeSH9(float4(n, 1.0));

                // Soft warm rim on lit edges
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float rim = pow(1.0 - saturate(dot(n, viewDir)), _RimPower) * _RimColor.a * shade;
                col += _RimColor.rgb * rim;

                // Gentle grading: saturation boost + warm tint
                float grey = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(grey.xxx, col, _Saturation) * _WarmTint.rgb;

                return fixed4(col, albedo.a);
            }
            ENDCG
        }
    }

    Fallback "Diffuse"
}
