// Su kanalı çıkışı: tahtanın alt kenarından sürekli dökülen şelale (RawImage, texture = dikişsiz su dokusu).
// Animasyon tamamen _Time ile — C# tarafında kare başına iş yok. Üstte kenardan taşan köpük, ortada aşağı akan doku +
// dikey akış çizgileri, altta sise dönüşüp sönen dip; yanlar yumuşak.
Shader "TinyFixers/UI/WaterfallFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Water Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Speed ("Flow speed (tiles/sec)", Float) = 1.6
        _Tiling ("Texture tiling (x, y)", Vector) = (1, 2.2, 0, 0)
        _FoamColor ("Foam color", Color) = (0.95, 1, 1, 1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };

            sampler2D _MainTex;
            fixed4 _Color, _FoamColor;
            float4 _Tiling, _ClipRect;
            float _Speed;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.world = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;                       // x: 0..1 genişlik, y: 0 alt, 1 üst (kenar)
                float t = _Time.y * _Speed;

                // Aşağı akan iki katman (farklı hız) → derinlik; doku dikişsiz, frac ile tekrar.
                float2 uvA = float2(uv.x * _Tiling.x, uv.y * _Tiling.y + t);
                float2 uvB = float2(uv.x * _Tiling.x * 1.3 + 0.37, uv.y * _Tiling.y * 0.8 + t * 1.45);
                fixed3 water = lerp(tex2D(_MainTex, frac(uvA)).rgb, tex2D(_MainTex, frac(uvB)).rgb, 0.45);

                // Dikey akış çizgileri: x'e bağlı ince parlak şeritler, aşağı kayar.
                float streak = sin(uv.x * 38.0 + sin(uv.x * 7.0) * 2.0) * 0.5 + 0.5;
                streak *= sin((uv.y + t * 0.9) * 9.0 + uv.x * 13.0) * 0.5 + 0.5;
                water += pow(streak, 6.0) * 0.35;

                // Üst kenar: taşan köpük (dalgalı alt sınır), alt: sis/köpük ve sönme.
                float lip = 0.86 + 0.04 * sin(uv.x * 22.0 + _Time.y * 6.0);
                float topFoam = smoothstep(lip - 0.10, lip, uv.y);
                float mist = 1.0 - smoothstep(0.0, 0.35, uv.y);
                float mistNoise = sin(uv.x * 31.0 + _Time.y * 4.0) * sin(uv.x * 17.0 - _Time.y * 3.0) * 0.5 + 0.5;
                fixed3 col = lerp(water, _FoamColor.rgb, saturate(topFoam * 0.85 + mist * (0.55 + 0.35 * mistNoise)));

                // Alfa: yanlar yumuşak, altta söner, gövde hafif şeffaf.
                float sides = smoothstep(0.0, 0.14, uv.x) * smoothstep(1.0, 0.86, uv.x);
                float fadeBottom = smoothstep(0.0, 0.30, uv.y);
                float alpha = sides * (0.88 * fadeBottom + mist * 0.25 * mistNoise * fadeBottom);

                fixed4 c = fixed4(col, saturate(alpha)) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
