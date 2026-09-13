// Lays down depth for a mesh that is about to draw transparent.
//
// A solid model whose own parts overlap - the paddle's handle runs up inside
// the head - blends those buried faces straight through the front face, because
// alpha blending has no way to tell which surface is "inside". Writing depth
// first means every fragment behind the front surface fails ZTest and never
// gets blended, so the model reads as a single translucent shell.
//
// Used as an extra material on the renderer: a renderer handed more materials
// than the mesh has submeshes draws the mesh again for each one, and this sits
// at queue 2999, one step ahead of the transparent material at 3000.
//
// Lives in Resources so Shader.Find works in builds too.
Shader "Custom/DepthPrepass"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Transparent-1"          // 2999: after opaques, before the paddle
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "DepthPrepass"

            ColorMask 0                        // depth only, never touches colour
            ZWrite On
            ZTest LEqual
            Cull Back                          // matches the paddle material's culling

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;                      // discarded by ColorMask 0
            }
            ENDHLSL
        }
    }
}
