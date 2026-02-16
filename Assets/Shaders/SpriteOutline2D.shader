Shader "Custom/SpriteOutline2D"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineThickness ("Outline Thickness (px)", Range(0, 32)) = 2
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
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
            };

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineThickness;
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;

                #ifdef PIXELSNAP_ON
                    OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, IN.uv) * IN.color;
                float baseAlpha = baseCol.a;

                float maxNeighborAlpha = 0.0;
                float minNeighborAlpha = 1.0;

                // Sample multiple rings around the pixel so thickness scales visually as expected.
                int thickness = (int)round(_OutlineThickness);
                [unroll(32)]
                for (int i = 1; i <= 32; i++)
                {
                    if (i > thickness) break;

                    float2 texel = _MainTex_TexelSize.xy * i;
                    float a1 = tex2D(_MainTex, IN.uv + float2( texel.x, 0.0)).a;
                    float a2 = tex2D(_MainTex, IN.uv + float2(-texel.x, 0.0)).a;
                    float a3 = tex2D(_MainTex, IN.uv + float2(0.0,  texel.y)).a;
                    float a4 = tex2D(_MainTex, IN.uv + float2(0.0, -texel.y)).a;
                    float a5 = tex2D(_MainTex, IN.uv + float2( texel.x,  texel.y)).a;
                    float a6 = tex2D(_MainTex, IN.uv + float2(-texel.x,  texel.y)).a;
                    float a7 = tex2D(_MainTex, IN.uv + float2( texel.x, -texel.y)).a;
                    float a8 = tex2D(_MainTex, IN.uv + float2(-texel.x, -texel.y)).a;

                    maxNeighborAlpha = max(maxNeighborAlpha, max(max(max(a1, a2), max(a3, a4)), max(max(a5, a6), max(a7, a8))));
                    minNeighborAlpha = min(minNeighborAlpha, min(min(min(a1, a2), min(a3, a4)), min(min(a5, a6), min(a7, a8))));
                }

                float outsideMask = saturate(maxNeighborAlpha - baseAlpha);
                float insideMask = saturate(baseAlpha - minNeighborAlpha);
                float outlineMask = max(outsideMask, insideMask);
                fixed4 outlineCol = _OutlineColor;
                outlineCol.a *= outlineMask;

                fixed4 result = baseCol;
                result.rgb = lerp(result.rgb, outlineCol.rgb, outlineCol.a);
                result.a = saturate(baseAlpha + outsideMask * _OutlineColor.a * (1.0 - baseAlpha));
                return result;
            }
            ENDCG
        }
    }
}
