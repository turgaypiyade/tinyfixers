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
        [HideInInspector] _TaskRegions ("Task regions active", Float) = 0
        [HideInInspector] _TaskRectCount ("Task rect count", Float) = 0
        [HideInInspector] _ContextReveal ("Gradual surrounding scene restoration", Range(0,1)) = 1
        [HideInInspector] _TaskBounds ("Animating task bounds", Vector) = (0, 0, 1, 1)
        [HideInInspector] _TaskAnim ("Task, progress, action, active", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PieceTask ("Falling sheet task", Float) = -1
        [HideInInspector] _RhodesTorch ("RodosDevi background flame", Float) = 0

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
            float _RhodesTorch;
            // Görev bölgeleri (WonderTaskRegions ile aynı kural): en küçük içeren dikdörtgen kazanır.
            float4 _TaskRects[24];
            float _TaskRectOwners[24];
            float _TaskRegions, _TaskRectCount, _ContextReveal, _PieceTask;
            float4 _TaskBounds, _TaskAnim;
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

            // Curved patch layout retained only for legacy, un-authored wonders.
            float contextRegion(float2 uv, out float4 bounds)
            {
                float count = max(1.0, _RegionCount);
                float rows = ceil(count * 0.5);
                float row = clamp(floor(uv.y * rows), 0.0, rows - 1.0);
                float lower = regionBoundary(row, rows, uv.x);
                float upper = regionBoundary(row + 1.0, rows, uv.x);
                row = clamp(row - step(uv.y, lower) + step(upper, uv.y), 0.0, rows - 1.0);
                lower = regionBoundary(row, rows, uv.x);
                upper = regionBoundary(row + 1.0, rows, uv.x);
                float split = regionSplit(uv.y);
                float fullWidth = step(count - 1.0, row * 2.0);
                float column = step(split, uv.x) * (1.0 - fullWidth);
                float left = column * split;
                float right = fullWidth > 0.5 ? 1.0 : lerp(split, 1.0, column);
                bounds = float4(left, lower, right, upper);
                return fmod((row * 2.0 + column) * _RegionStride, count);
            }

            // Authored subject parts take priority over their surrounding scene.
            float taskOwner(float2 uv, out float rectangle)
            {
                float2 w = uv + (float2(patinaNoise(uv * float2(9.0, 13.0)), patinaNoise(uv * float2(13.0, 9.0) + 7.0)) - 0.5) * 0.014;
                float best = 1e5;
                float owner = -1.0;
                rectangle = -1.0;
                [loop] for (int k = 0; k < 24; k++)
                {
                    if ((float)k >= _TaskRectCount) break;
                    float4 r = _TaskRects[k];
                    if (w.x >= r.x && w.y >= r.y && w.x <= r.z && w.y <= r.w)
                    {
                        float area = (r.z - r.x) * (r.w - r.y);
                        if (area < best) { best = area; owner = _TaskRectOwners[k]; rectangle = (float)k; }
                    }
                }
                return owner;
            }

            float taskOwner(float2 uv)
            {
                float rectangle;
                return taskOwner(uv, rectangle);
            }

            // Onarma dışındaki eylemler: bölge içinde ilerleyen bir cephe paslıyı temize çevirir.
            // action 1 Parlat: çapraz parlama bandı · 2 Boya: yukarıdan akan boya · 3 Işık: merkezden ışıma · 4 Temizle: yandan süpürme
            float3 sweepTask(float2 uv, float3 photo, float3 weathered, float action, float p)
            {
                float2 local = saturate((uv - _TaskBounds.xy) / max(_TaskBounds.zw - _TaskBounds.xy, float2(0.001, 0.001)));
                float n = patinaNoise(uv * float2(23.0, 31.0)) - 0.5;
                float s;
                float soft = 0.05;
                if (action < 1.5)      s = local.x * 0.55 + local.y * 0.45 + n * 0.03;
                else if (action < 2.5) { s = 1.0 - local.y + (vnoise(local.x * 16.0) - 0.5) * 0.14; soft = 0.025; }
                else if (action < 3.5) s = length((local - 0.5) * float2(1.0, 1.2)) * 1.3 + n * 0.05;
                else                   { s = local.x + n * 0.12; soft = 0.09; }
                float front = lerp(-0.15, 1.25, p);
                float clean = 1.0 - smoothstep(front - soft, front + soft, s);
                float3 c = lerp(weathered, photo, clean);
                float bd = (s - front) / 0.045;   // pow() negatif tabanda NaN verir
                float band = exp(-bd * bd);
                if (action < 1.5)
                {
                    c += float3(1.0, 0.96, 0.82) * band * 0.85;                     // parlama bandı
                    c += photo * clean * 0.18 * (1.0 - p);                         // taze cila parıltısı
                }
                else if (action < 2.5)
                {
                    c = lerp(c, c * 0.72, band * 0.8);                             // ıslak boya kenarı
                    c += photo * clean * 0.10 * (1.0 - p);
                }
                else if (action < 3.5)
                {
                    float3 warm = float3(1.0, 0.78, 0.38);
                    c += warm * band * 0.75;
                    c += photo * warm * clean * 0.45 * sin(3.14159265 * p);         // yanan ışığın parlaması
                }
                else
                {
                    c = lerp(c, float3(0.86, 0.76, 0.58), band * 0.45);            // savrulan toz
                }
                return c;
            }

            // Parlama (Shine): bölge sahiplenmez; açık yüzeyin üstünden çapraz cila bandı geçer, parlak yüzeyde daha güçlü.
            float3 shineOverlay(float2 uv, float3 c, float3 photo, float p)
            {
                float2 size = max(_TaskBounds.zw - _TaskBounds.xy, float2(0.001, 0.001));
                float2 local = (uv - _TaskBounds.xy) / size;
                float2 edge = min(local, 1.0 - local);
                float inside = smoothstep(0.0, 0.06, min(edge.x, edge.y));
                if (inside <= 0.0) return c;
                float s = local.x * 0.55 + local.y * 0.45;
                float front = lerp(-0.2, 1.2, p);
                float bd = (s - front) / 0.05;
                float band = exp(-bd * bd);
                float lum = dot(photo, float3(0.299, 0.587, 0.114));
                float glint = band * inside * (0.35 + lum * 0.9);
                float twinkle = step(0.985, hash2(floor(uv * float2(160.0, 280.0)))) * exp(-bd * bd * 0.25) * inside;
                return c + float3(1.0, 0.95, 0.80) * glint * 0.9 + float3(1.0, 1.0, 0.9) * twinkle * 0.8;
            }

            // Onarma: torç bölge çevresini keser, paslı tabaka (ayrı mesh) kıvrılıp düşer.
            float3 repairCut(float2 uv, float3 photo, float3 weathered, float rank, float left, float right, float lower, float upper, float rectangle)
            {
                float count = max(1.0, _RegionCount);
                float clean = saturate(_Reveal * count - rank);
                float finishing = _RegionFinish.z * (1.0 - step(0.5, abs(rank - _RegionFinish.x)));
                float2 local = saturate(float2((uv.x - left) / max(0.001, right - left),
                    (uv.y - lower) / max(0.001, upper - lower)));
                // A real textured mesh covers this patch while curling away.
                // The revealed image stays fixed under the moving sheet.
                if (finishing > 0.5) clean = 1.0;
                float3 c = lerp(weathered, photo, clean);
                float cutting = _RegionCut.z * (1.0 - step(0.5, abs(rank - _RegionCut.x)));
                if (_TaskRegions > 0.5 && _RegionCut.w > 0.5)
                    cutting *= 1.0 - step(0.5, abs(rectangle - (_RegionCut.w - 1.0)));
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

            float3 restoredImage(float2 uv, float3 photo)
            {
                float count = max(1.0, _RegionCount);
                if (_TaskRegions > 0.5)
                {
                    if (_Reveal >= 0.999 && _TaskAnim.w < 0.5) return photo;
                    float rectangle;
                    float task = taskOwner(uv, rectangle);
                    if (rectangle < 0.0)
                    {
                        // Background quietly regains colour alongside the actual repair.
                        // Paint/welding effects remain confined to the subject part.
                        float contextClean = saturate(_ContextReveal);
                        if (contextClean >= 0.999) return photo;
                        float3 context = lerp(weatheredImage(uv, photo), photo, contextClean);
                        if (_TaskAnim.w > 1.5 && _TaskAnim.z > 2.5 && _TaskAnim.z < 3.5)
                            context += photo * float3(1.0, 0.78, 0.38) * 0.12 * sin(3.14159265 * _TaskAnim.y);
                        return context;
                    }
                    float clean = saturate(_Reveal * count - task);
                    float3 ct = photo;
                    if (clean < 0.999)
                    {
                        float3 weatheredT = weatheredImage(uv, photo);
                        if (_TaskAnim.w > 0.5 && abs(task - _TaskAnim.x) < 0.5 && _TaskAnim.z > 0.5 && _TaskAnim.z < 4.5)
                            ct = sweepTask(uv, photo, weatheredT, _TaskAnim.z, _TaskAnim.y);
                        else
                        {
                            float4 bounds = rectangle >= 0.0 ? _TaskRects[(int)max(0.0, rectangle)] : _TaskBounds;
                            ct = repairCut(uv, photo, weatheredT, task, bounds.x, bounds.z, bounds.y, bounds.w, rectangle);
                        }
                    }
                    // Polishing acts on completed surfaces, never future crowns, roofs or bells.
                    if (_TaskAnim.w > 0.5 && _TaskAnim.z > 4.5 && clean >= 0.999)
                        ct = shineOverlay(uv, ct, photo, _TaskAnim.y);
                    return ct;
                }
                float4 bounds;
                float rank = contextRegion(uv, bounds);
                float clean = saturate(_Reveal * count - rank);
                // The complete reward is exactly the original image and skips all patina work.
                if (clean >= 0.999) return photo;

                float3 weathered = weatheredImage(uv, photo);
                return repairCut(uv, photo, weathered, rank, bounds.x, bounds.z, bounds.y, bounds.w, -1.0);
            }

            // RodosDevi.png's painted flame: localized motion, warm flicker and two drifting embers.
            // Coordinates are sprite-relative, so aspect fitting and atlas packing preserve the anchor.
            float3 rhodesFlame(float2 uv, float3 photo)
            {
                if (uv.x < 0.175 || uv.x > 0.355 || uv.y < 0.925) return photo;
                float t = _Time.y;
                float height = saturate((uv.y - 0.944) / 0.056);
                float widthMask = 1.0 - smoothstep(0.045, 0.068, abs(uv.x - 0.258));
                float warm = saturate((photo.r - photo.b - 0.18) * 4.0);
                float flameMask = smoothstep(0.0, 0.25, height) * widthMask * warm;
                float2 bend = float2(
                    sin(height * 9.0 - t * 7.2) * 0.0060 + sin(height * 17.0 - t * 11.1) * 0.0024,
                    sin(t * 6.4 + uv.x * 51.0) * 0.0030) * height;
                float2 sampleUV = lerp(_SpriteUVRect.xy, _SpriteUVRect.zw, saturate(uv + bend));
                float3 moving = tex2D(_MainTex, sampleUV).rgb;
                float flicker = 1.0 + sin(t * 9.3) * 0.080 + sin(t * 16.7 + 0.8) * 0.050;
                float3 c = lerp(photo, moving * flicker, flameMask);
                float2 glow = (uv - float2(0.258, 0.963)) / float2(0.066, 0.039);
                c += float3(1.0, 0.40, 0.055) * exp(-dot(glow, glow) * 2.0) * (0.055 + 0.018 * sin(t * 6.1));
                [unroll] for (int ember = 0; ember < 2; ember++)
                {
                    float seed = (float)ember;
                    float age = frac(t * 0.52 + seed * 0.53);
                    float2 p = float2(0.258 + (seed - 0.5) * 0.095 + 0.007 * sin(age * 5.0 + seed),
                        0.946 + age * 0.067);
                    float2 d = (uv - p) / float2(0.0020, 0.0020);
                    float spark = exp(-dot(d, d) * 2.0) * sin(age * 3.14159265);
                    c += float3(1.0, 0.62, 0.15) * spark * 1.0;
                }
                return c;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 textureColor = tex2D(_MainTex, i.texcoord);
                if (_RhodesTorch > 0.5 && _RestorationPiece < 0.5 && _Reveal > 0.999)
                {
                    float2 photoUV = saturate((i.texcoord - _SpriteUVRect.xy) /
                        max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.0001, 0.0001)));
                    textureColor.rgb = rhodesFlame(photoUV, textureColor.rgb);
                }
                fixed4 src = textureColor * i.color;
                float3 rgb;
                float a;
                if (_RestorationPiece > 0.5)
                {
                    float2 uv = saturate((i.texcoord - _SpriteUVRect.xy) / max(_SpriteUVRect.zw - _SpriteUVRect.xy, float2(0.0001, 0.0001)));
                    rgb = weatheredImage(uv, textureColor.rgb) * i.color.rgb;
                    a = i.color.a;
                    // Görev bölgesinde tabaka yalnız o görevin piksellerini taşır (iç içe bölgeler açık kalır).
                    if (_TaskRegions > 0.5)
                    {
                        float rectangle;
                        float owner = taskOwner(uv, rectangle);
                        if (rectangle < 0.0 || abs(owner - _PieceTask) > 0.5) a = 0.0;
                    }
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
