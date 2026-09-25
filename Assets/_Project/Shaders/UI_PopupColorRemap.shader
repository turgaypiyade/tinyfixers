Shader "TinyFixers/UI/PopupColorRemap"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _SourceColor ("Original panel color", Color) = (0.5058824,0.0745098,0.1843137,1)
        _TargetColor ("Panel color", Color) = (0.427451,0.227451,0.7960784,1)
        _SourceDarkColor ("Original panel shadow color", Color) = (0.3921569,0.0470588,0.1411765,1)
        _TargetDarkColor ("Panel shadow color", Color) = (0.1607843,0.1215686,0.3686275,1)
        _HueRange ("Source hue range", Range(0.001,0.25)) = 0.07
        _HueSoftness ("Selection softness", Range(0.001,0.10)) = 0.025
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
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
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

            struct appdata_t { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float4 local : TEXCOORD1; };
            sampler2D _MainTex;
            half4 _Color, _SourceColor, _TargetColor, _SourceDarkColor, _TargetDarkColor, _TextureSampleAdd;
            float _HueRange, _HueSoftness;
            float4 _ClipRect;

            float3 ToHSV(float3 rgb)
            {
                float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
                float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-6)), d / (q.x + 1e-6), q.x);
            }

            float3 ToRGB(float3 hsv)
            {
                float3 p = abs(frac(hsv.xxx + float3(0, 2.0 / 3.0, 1.0 / 3.0)) * 6 - 3);
                return hsv.z * lerp(float3(1,1,1), saturate(p - 1), hsv.y);
            }

            // Keep black and white endpoints; map the original base tone to the
            // chosen tone without clipping away the painted highlights/shadows.
            float RemapTone(float value, float source, float target)
            {
                return value <= source
                    ? value * target / max(source, 1e-5)
                    : target + (value - source) * (1 - target) / max(1 - source, 1e-5);
            }

            // Brightness curve through (0,0) → dark pair → base pair → (1,1).
            float RemapValue(float v, float darkFrom, float darkTo, float baseFrom, float baseTo)
            {
                if (v <= darkFrom) return v * darkTo / max(darkFrom, 1e-5);
                if (v <= baseFrom)
                    return lerp(darkTo, baseTo, (v - darkFrom) / max(baseFrom - darkFrom, 1e-5));
                return baseTo + (v - baseFrom) * (1 - baseTo) / max(1 - baseFrom, 1e-5);
            }

            float HueOffset(float from, float to)
            {
                float d = to - from;
                return d - round(d);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                o.local = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                half4 sampleColor = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float3 rgb = sampleColor.rgb;
                float3 source = _SourceColor.rgb;
                float3 target = _TargetColor.rgb;
                float3 sourceDark = _SourceDarkColor.rgb;
                float3 targetDark = _TargetDarkColor.rgb;
                // Select/remap in sRGB so the color picker's HEX value and the
                // source PNG agree in both Linear and Gamma projects.
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                source = LinearToGammaSpace(source);
                target = LinearToGammaSpace(target);
                sourceDark = LinearToGammaSpace(sourceDark);
                targetDark = LinearToGammaSpace(targetDark);
                #endif
                float3 hsv = ToHSV(rgb);
                float3 from = ToHSV(source);
                float3 to = ToHSV(target);
                float3 fromDark = ToHSV(sourceDark);
                float3 toDark = ToHSV(targetDark);
                float distance = abs(hsv.x - from.x);
                distance = min(distance, 1 - distance);
                float mask = (1 - smoothstep(_HueRange, _HueRange + _HueSoftness, distance))
                    * smoothstep(0.08, 0.20, hsv.y);
                // 0 at the shadow tone, 1 at the base tone: hue/saturation blend
                // between the two pairs; brightness follows the two-point curve.
                float k = saturate((hsv.z - fromDark.z) / max(from.z - fromDark.z, 1e-5));
                float hue = hsv.x + lerp(HueOffset(fromDark.x, toDark.x), HueOffset(from.x, to.x), k);
                float sat = lerp(RemapTone(hsv.y, fromDark.y, toDark.y), RemapTone(hsv.y, from.y, to.y), k);
                float3 recolored = float3(frac(hue), sat,
                    RemapValue(hsv.z, fromDark.z, toDark.z, from.z, to.z));
                float3 mapped = ToRGB(recolored);
                #ifndef UNITY_COLORSPACE_GAMMA
                mapped = GammaToLinearSpace(mapped);
                #endif
                // Unselected gold/cream pixels use the untouched texture sample.
                sampleColor.rgb = lerp(sampleColor.rgb, mapped, mask);
                half4 color = sampleColor * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.local.xy, _ClipRect);
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
