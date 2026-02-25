Shader "Hidden/AlphaBackground"
{
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // Write only to the Alpha channel
        ColorMask A

        // Source blend: One, Dest blend: Zero => dest_alpha = src_alpha * 1 + dest_alpha * 0 = 0
        // Since frag outputs alpha=0, this forces alpha=0 into every pixel
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata { float4 vertex : POSITION; };
            struct v2f    { float4 pos : SV_POSITION; };

            // Pass clip-space coords directly — do NOT apply MVP transform
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = v.vertex;  // vertices already in clip space [-1,1]
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0, 0, 0, 0);  // alpha = 0
            }
            ENDCG
        }
    }
}
