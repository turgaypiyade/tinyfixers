Shader "UI/WonderReveal"
{
    // Tek bir imajı alttan yukarı "kaynak/inşa" ile açar.
    // Açılmamış kısım = mavi hologram (tel-taslak hissi), açılmış kısım = gerçek renk.
    // Sınırda parlayan bir kaynak şeridi (weld edge) gezer. _Reveal 0..1 ile sürülür.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Reveal        ("Reveal (0..1)", Range(0,1)) = 1

        _EdgeWidth     ("Weld Edge Width", Range(0.001, 0.2)) = 0.05
        [HDR] _EdgeColor ("Weld Edge Color", Color) = (1.9, 1.45, 0.7, 1)
        _EdgeNoise     ("Edge Wobble", Range(0, 0.15)) = 0.04
        _NoiseScale    ("Edge Wobble Scale", Range(1, 80)) = 26

        _HoloColor     ("Hologram Tint", Color) = (0.35, 0.75, 1.0, 1)
        _HoloAlpha     ("Hologram Alpha", Range(0, 1)) = 0.55
        _HoloDesat     ("Hologram Desaturate", Range(0, 1)) = 0.85
        _ScanStrength  ("Hologram Scanline", Range(0, 1)) = 0.25
        _ScanFreq      ("Hologram Scanline Freq", Range(50, 900)) = 340

        // UI mask / stencil desteği (UGUI ile uyum)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        // RectMask2D / UI kırpma (scroll viewport dışına taşmasın)
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

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
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

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
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            float4 _ClipRect;

            sampler2D _MainTex;
            fixed4 _Color;

            float  _Reveal;
            float  _EdgeWidth;
            fixed4 _EdgeColor;
            float  _EdgeNoise;
            float  _NoiseScale;

            fixed4 _HoloColor;
            float  _HoloAlpha;
            float  _HoloDesat;
            float  _ScanStrength;
            float  _ScanFreq;

            // Ucuz 1B değer-gürültüsü (kaynak sınırını düz çizgi olmaktan çıkarır)
            float hash1(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float vnoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(hash1(i), hash1(i + 1.0), u);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 src = tex2D(_MainTex, i.texcoord) * i.color;

                // --- Kaynak sınırı (alttan yukarı) ------------------------
                // _Reveal 0 -> sınır ekranın altında (her yer hologram)
                // _Reveal 1 -> sınır ekranın üstünde (her yer gerçek)
                float pad = _EdgeNoise + _EdgeWidth;
                float edge = lerp(-pad, 1.0 + pad, _Reveal);
                float wobble = (vnoise(i.texcoord.x * _NoiseScale) - 0.5) * _EdgeNoise;
                float threshold = edge + wobble;

                // 0 = tam hologram, 1 = tam gerçek
                float revealMask = smoothstep(threshold - _EdgeWidth, threshold + _EdgeWidth, i.texcoord.y);
                revealMask = 1.0 - revealMask; // altı gerçek olsun

                // --- Hologram görünüm (aynı imajdan türetilir) ------------
                float lum = dot(src.rgb, float3(0.299, 0.587, 0.114));
                float3 holoRgb = lerp(src.rgb, float3(lum, lum, lum), _HoloDesat);
                holoRgb *= _HoloColor.rgb * 1.4;                 // mavi taslak tonu
                float scan = 1.0 - _ScanStrength * (0.5 + 0.5 * sin(i.texcoord.y * _ScanFreq));
                holoRgb *= scan;
                float holoA = src.a * _HoloAlpha;

                // --- Gerçek <-> hologram harmanı --------------------------
                float3 rgb = lerp(holoRgb, src.rgb, revealMask);
                float  a   = lerp(holoA,  src.a,   revealMask);

                // --- Kaynak parıltı şeridi (sınır bandı) ------------------
                float band = 1.0 - saturate(abs(i.texcoord.y - threshold) / _EdgeWidth);
                band = band * band;                              // keskin çekirdek
                // sadece _Reveal aralık içindeyken parlasın (0/1 uçlarında değil)
                float active = smoothstep(0.0, 0.03, _Reveal) * smoothstep(1.0, 0.97, _Reveal);
                rgb += _EdgeColor.rgb * band * active;
                a = max(a, band * active * src.a);

                // --- UI kırpma (RectMask2D / scroll viewport) -------------
                #ifdef UNITY_UI_CLIP_RECT
                a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(a - 0.001);
                #endif

                return fixed4(rgb, a);
            }
            ENDCG
        }
    }
}
