Shader "Hidden/AlphaBackground"
{
    SubShader
    {
        // No culling, no depth write, no depth test
        Cull Off
        ZWrite Off
        ZTest Always

        // Only write to the Alpha channel — leave RGB intact
        ColorMask A

        // Blend: dest_alpha = 0 (replace with zero regardless of source)
        Blend Zero Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f    { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // Output alpha=0: fully transparent
            fixed4 frag(v2f i) : SV_Target
            {
                return fixed4(0, 0, 0, 0);
            }
            ENDCG
        }
    }
}
