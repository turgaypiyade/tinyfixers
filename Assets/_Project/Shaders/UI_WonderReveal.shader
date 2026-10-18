Shader "UI/WonderReveal"
{
    // Tek bir imajı alttan yukarı "kaynak/inşa" ile açar.
    // Görev overlay: paslı resim üzerinde bölgesel restorasyon. Diğer UI: eski hologram.
    // Sınırda parlayan bir kaynak şeridi (weld edge) gezer. _Reveal 0..1 ile sürülür.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _Reveal        ("Reveal (0..1)", Range(0,1)) = 1

        [HideInInspector] _RestorationPiece ("Falling restored sheet", Float) = 0
        _CutThickness ("Restoration cut thickness", Range(1, 12)) = 5
        [Toggle] _Restoration ("Rust restoration patches", Float) = 0
        _RestorationRect ("Image local rect", Vector) = (0, 0, 1080, 1920)
        _SpriteUVRect ("Sprite atlas UV rect", Vector) = (0, 0, 1, 1)
        _RegionCount ("Restoration regions", Float) = 8
        _RegionStride ("Deterministic region order", Float) = 3
        _RegionCut ("Stage, contour progress, active", Vector) = (0, 0, 0, 0)
        _RegionFinish ("Stage, restoration progress, active", Vector) = (0, 0, 0, 0)
        _Torch ("Torch UV, active", Vector) = (0, 0, 0, 0)

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
            float _Restoration, _RestorationPiece, _CutThickness, _RegionCount, _RegionStride;
            float4 _RestorationRect, _SpriteUVRect, _RegionCut, _RegionFinish, _Torch;
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

            float hash2(float2 p)
            {
                p = frac(p * float2(0.1031, 0.1030));
                p += dot(p, p.yx + 33.33);
                return frac((p.x + p.y) * p.x);
            }

            float patinaNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash2(cell), hash2(cell + float2(1, 0)), f.x),
                    lerp(hash2(cell + float2(0, 1)), hash2(cell + 1.0), f.x), f.y);
            }

            // Keep these equations in sync with WonderRestorationLayout.
            float regionBoundary(float row, float rows, float x)
            {
                float f = saturate(row / rows);
                return f + sin(3.14159265 * f) * min(1.0, 6.0 / rows) *
                    (0.024 * sin(x * 10.0 + f * 8.0) + 0.008 * sin(x * 25.0 - f * 11.0));
            }

            float regionSplit(float y)
            {
                return 0.49 + 0.1 * sin(y * 8.0 + 0.4) + 0.035 * sin(y * 21.0 + 1.0);
            }

            float3 weatheredImage(float2 uv, float3 photo)
            {
                float broad = patinaNoise(uv * float2(5.0, 8.0));
                float fine = patinaNoise(uv * float2(32.0, 53.0));
                float patina = broad * 0.7 + fine * 0.3;
                float luminance = dot(photo, float3(0.299, 0.587, 0.114));
                float3 faded = lerp(float3(0.16, 0.145, 0.12), float3(0.59, 0.54, 0.43),
                    luminance * 0.72 + 0.1);
                float rust = smoothstep(0.40, 0.76, patina);
                float3 weathered = lerp(faded, faded * float3(1.08, 0.61, 0.35), rust * 0.8);
                weathered *= 0.85 + fine * 0.22;
                // Fine vertical wear adds age without a visible grid or panel outlines.
                float streak = patinaNoise(uv * float2(75.0, 4.0));
                weathered *= 0.93 + streak * 0.1;
                return weathered;
            }

            float3 restoredImage(float2 uv, float3 photo)
            {
                float count = max(1.0, _RegionCount);
                float rows = ceil(count * 0.5);
                float row = clamp(floor(uv.y * rows), 0.0, rows - 1.0);
                float lower = regionBoundary(row, rows, uv.x);
                float upper = regionBoundary(row + 1.0, rows, uv.x);
                // Boundary displacement is less than half a row; only one neighbor is possible.
                row = clamp(row - step(uv.y, lower) + step(upper, uv.y), 0.0, rows - 1.0);
                lower = regionBoundary(row, rows, uv.x);
                upper = regionBoundary(row + 1.0, rows, uv.x);
                float split = regionSplit(uv.y);
                float fullWidth = step(count - 1.0, row * 2.0);
                float column = step(split, uv.x) * (1.0 - fullWidth);
                float left = column * split;
                float right = fullWidth > 0.5 ? 1.0 : lerp(split, 1.0, column);
                float cell = row * 2.0 + column;
                float rank = fmod(cell * _RegionStride, count);
                float clean = saturate(_Reveal * count - rank);
                // The complete reward is exactly the original image and skips all patina work.
                if (clean >= 0.999) return photo;

                float3 weathered = weatheredImage(uv, photo);

                float finishing = _RegionFinish.z * (1.0 - step(0.5, abs(rank - _RegionFinish.x)));
                float2 local = saturate(float2((uv.x - left) / max(0.001, right - left),
                    (uv.y - lower) / max(0.001, upper - lower)));
                // A real textured mesh covers this patch while curling away.
                // The revealed image stays fixed under the moving sheet.
                if (finishing > 0.5) clean = 1.0;
                float3 c = lerp(weathered, photo, clean);
                float cutting = _RegionCut.z * (1.0 - step(0.5, abs(rank - _RegionCut.x)));
                if (cutting > 0.5)
                {
                    float2 size = max(_RestorationRect.zw, float2(1, 1));
                    float nearest = (uv.y - lower) * size.y;
                    float phase = local.x * 0.25;
                    float distanceRight = (right - uv.x) * size.x;
                    if (distanceRight < nearest) { nearest = distanceRight; phase = 0.25 + local.y * 0.25; }
                    float distanceTop = (upper - uv.y) * size.y;
                    if (distanceTop < nearest) { nearest = distanceTop; phase = 0.5 + (1.0 - local.x) * 0.25; }
                    float distanceLeft = (uv.x - left) * size.x;
                    if (distanceLeft < nearest) { nearest = distanceLeft; phase = 0.75 + (1.0 - local.y) * 0.25; }
                    float traced = step(phase, _RegionCut.y);
                    float cooling = exp(-max(0.0, _RegionCut.y - phase) * 9.0);
                    float fade = 1.0 - _RegionFinish.y * finishing;
                    float cutEdge = (1.0 - smoothstep(_CutThickness * 0.35, _CutThickness, nearest)) * traced * fade;
                    float warmRim = (1.0 - smoothstep(_CutThickness, _CutThickness * 1.8, nearest)) * traced * fade;
                    c = lerp(c, float3(0.14, 0.065, 0.025), cutEdge * 0.55);
                    c += float3(1.0, 0.46, 0.1) * cutEdge * cooling;
                    c += float3(0.32, 0.10, 0.018) * warmRim * cooling;
                    float2 torch = (uv - _Torch.xy) * size;
                    c += float3(1.3, 1.15, 0.75) * exp(-dot(torch, torch) / 95.0) * _Torch.z;
                }
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, i.texcoord);
                fixed4 src = textureColor * i.color;
                float3 rgb;
                float a;
                if (_RestorationPiece > 0.5)
                {
                    float2 uv = saturate((i.texcoord - _SpriteUVRect.xy) / max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.0001, 0.0001)));
                    rgb = weatheredImage(uv, textureColor.rgb) * i.color.rgb;
                    a = i.color.a;
                }
                else if (_Restoration > 0.5)
                {
                    float2 uv = saturate((i.texcoord - _SpriteUVRect.xy) / max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.0001, 0.0001)));
                    rgb = restoredImage(uv, textureColor.rgb) * i.color.rgb;
                    // Weathering is opaque, rather than revealing the menu behind this image.
                    a = i.color.a;
                }
                else
                {

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
                    rgb = lerp(holoRgb, src.rgb, revealMask);
                    a = lerp(holoA, src.a, revealMask);

                    // --- Kaynak parıltı şeridi (sınır bandı) ------------------
                    float band = 1.0 - saturate(abs(i.texcoord.y - threshold) / _EdgeWidth);
                    band = band * band;                              // keskin çekirdek
                    // sadece _Reveal aralık içindeyken parlasın (0/1 uçlarında değil)
                    float active = smoothstep(0.0, 0.03, _Reveal) * smoothstep(1.0, 0.97, _Reveal);
                    rgb += _EdgeColor.rgb * band * active;
                    a = max(a, band * active * src.a);

                }

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
