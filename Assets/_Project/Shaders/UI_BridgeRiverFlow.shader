Shader "TinyFixers/UI/BridgeRiverFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WaterMask ("River mask (linear data)", 2D) = "black" {}
        _FlowStrength ("Flow strength", Range(0,2)) = 1
        _FlowClock ("Unscaled flow clock", Float) = 0
        _FlowTint ("Highlight tint", Color) = (0.82,0.98,1,1)
        _StreakIntensity ("Flow streaks", Range(0,1)) = 0.32
        _SparkleIntensity ("Sparkles", Range(0,2)) = 0.8
        _Horizon ("Horizon (sprite uv.y)", Range(0,1)) = 0.8
        _SpriteUV ("Sprite UV bounds", Vector) = (0,0,1,1)
        _SpritePixelSize ("Sprite pixel size", Vector) = (941,1671,0,0)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                half4 mask : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex, _WaterMask;
            fixed4 _Color, _TextureSampleAdd, _FlowTint;
            float4 _SpriteUV, _SpritePixelSize, _ClipRect;
            float _FlowClock, _FlowStrength, _StreakIntensity, _SparkleIntensity, _Horizon;

            // Hücre y ekseninde 64'lük periyot: kaydırma 600 sn'lik saatle dikişsiz döner.
            float Hash(float2 c)
            {
                c.y = c.y - 64.0 * floor(c.y / 64.0);
                float3 p = frac(float3(c.xyx) * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float ValueNoise(float2 p)
            {
                float2 c = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(c), b = Hash(c + float2(1, 0));
                float d = Hash(c + float2(0, 1)), e = Hash(c + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(d, e, f.x), f.y);
            }
            float _UIMaskSoftnessX, _UIMaskSoftnessY;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                float2 pixelSize = o.vertex.w / abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
                float4 rect = clamp(_ClipRect, -2e10, 2e10);
                o.mask = half4(v.vertex.xy * 2 - rect.xy - rect.zw,
                    0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize)));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 extent = max(_SpriteUV.zw - _SpriteUV.xy, float2(1e-5, 1e-5));
                float2 uv = (i.uv - _SpriteUV.xy) / extent;
                float2 texel = 1.0 / max(_SpritePixelSize.xy, float2(1, 1));
                half river = tex2D(_WaterMask, uv).r;
                fixed4 original = tex2D(_MainTex, i.uv) + _TextureSampleAdd;

                // Two staggered scrolling samples: each phase resets while invisible.
                // Positive sample Y makes the painted water move down toward the viewer.
                float phase = frac(_FlowClock / 4.0);
                float phaseB = frac(phase + 0.5);
                float blend = abs(phase * 2.0 - 1.0);
                float depth = lerp(0.3, 1.0, saturate((0.74 - uv.y) / 0.5));
                float wave = sin(uv.y * 210.0 + uv.x * 28.0 + _FlowClock * 2.0943951);
                float waveB = sin(uv.y * 340.0 - uv.x * 45.0 + _FlowClock * 3.14159265);
                float2 ripple = float2(wave * 3.0 + waveB * 1.0, waveB * 1.2);
                float2 travel = float2(wave * 1.5, 34.0);
                float2 amount = texel * river * depth * _FlowStrength;
                float2 uvA = clamp(uv + (ripple + travel * (phase - 0.5)) * amount,
                    texel * 0.5, 1.0 - texel * 0.5);
                float2 uvB = clamp(uv + (ripple + travel * (phaseB - 0.5)) * amount,
                    texel * 0.5, 1.0 - texel * 0.5);

                // Destination and source must both be water: shore/boat colors cannot smear.
                half safeA = tex2D(_WaterMask, uvA).r;
                half safeB = tex2D(_WaterMask, uvB).r;
                fixed3 a = tex2D(_MainTex, _SpriteUV.xy + uvA * extent).rgb + _TextureSampleAdd.rgb;
                fixed3 b = tex2D(_MainTex, _SpriteUV.xy + uvB * extent).rgb + _TextureSampleAdd.rgb;
                a = lerp(original.rgb, a, safeA);
                b = lerp(original.rgb, b, safeB);
                fixed3 moving = lerp(a, b, blend);
                // Perspektif: ufka (ufuk çizgisi) yaklaştıkça desen küçülür ve yavaşlar; akış izleyiciye doğru.
                float h = max(_Horizon - uv.y, 0.05);
                float2 world = float2((uv.x - 0.5) * (_SpritePixelSize.x / _SpritePixelSize.y) / h, 1.0 / h);
                // Scroll hızları 64/600'ün tam katı → saat 600'de sarınca desen atlamaz.
                float scrollA = _FlowClock * (64.0 * 4.0 / 600.0);
                float scrollB = _FlowClock * (64.0 * 7.0 / 600.0);
                float nA = ValueNoise(float2(world.x * 20.0, world.y * 2.2 + scrollA));
                float nB = ValueNoise(float2(world.x * 26.0 + 17.0, world.y * 4.0 + scrollB));
                float nC = ValueNoise(float2(world.x * 7.0 - 5.0, world.y * 1.1 + scrollA * 0.5));
                // Akış çizgileri: akış yönünde uzamış açık şeritler, geniş bir dalga ile kümelenir.
                float streak = smoothstep(0.55, 0.9, nA) * smoothstep(0.35, 0.75, nC);
                // Pırıltı: iki katmanın tepe noktalarının kesişimi, kısa ve parlak.
                float sparkle = smoothstep(0.78, 0.95, nB) * smoothstep(0.5, 0.8, nA);
                // Ufka yakın desen piksel altına iner (kıpırtı) → orada söndür.
                float fadeFar = smoothstep(0.05, 0.16, h) * lerp(0.45, 1.0, depth);
                float flowLight = (streak * _StreakIntensity + sparkle * _SparkleIntensity) * fadeFar;
                moving *= 1.0 + (nC - 0.5) * 0.10 * saturate(_FlowStrength);
                moving += _FlowTint.rgb * flowLight * saturate(_FlowStrength);
                fixed4 color = fixed4(lerp(original.rgb, moving, river), original.a) * i.color;

                #ifdef UNITY_UI_CLIP_RECT
                half2 clipMask = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                color.a *= clipMask.x * clipMask.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
