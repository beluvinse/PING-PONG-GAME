// Circular wipe for scene transitions: paints everything outside a centred
// circle, so animating _Radius from 1.2 (clear) down to 0 closes the screen in
// from the edges. Lives in Resources so Shader.Find works in builds too.
Shader "Custom/IrisWipe"
{
    Properties
    {
        // The canvas assigns the graphic's texture here; a UI material without
        // it logs an error every frame.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (0, 0, 0, 1)
        _Radius ("Radius (0 = closed, 1.2 = open)", Range(0, 1.5)) = 1.2
        // 0 = crisp edge (still antialiased, one pixel wide). Raise it to blur.
        _Softness ("Edge Softness", Range(0, 0.3)) = 0
        _Aspect ("Aspect (width / height)", Float) = 1.7777
    }

    SubShader
    {
        Tags { "Queue" = "Overlay" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0        // fwidth (screen-space derivatives)
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Color;
            float _Radius;
            float _Softness;
            float _Aspect;

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
                // Aspect keeps the wipe circular instead of oval; normalised so
                // 0 is the centre of the screen and 1 its corners.
                float2 offset = (i.uv - 0.5) * float2(_Aspect, 1.0);
                float dist = length(offset) / length(float2(_Aspect, 1.0) * 0.5);

                // fwidth is how much dist changes across one pixel, so the edge
                // is a hard circle that still antialiases instead of stepping.
                float edge = max(fwidth(dist), _Softness);
                float alpha = smoothstep(_Radius - edge, _Radius + edge, dist);

                fixed4 tex = tex2D(_MainTex, i.uv);   // white when the graphic has no texture
                return fixed4(_Color.rgb * tex.rgb, _Color.a * tex.a * alpha);
            }
            ENDCG
        }
    }
}
