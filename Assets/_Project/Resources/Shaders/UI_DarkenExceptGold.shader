Shader "UI/DarkenExceptGold"
{
    // Zorluk etiketi (ana menü seviye butonu): butonun gövdesini koyulaştırır, altın/sarı çerçeveyi korur.
    // Buton sprite'ı bölüm temasıyla değiştiği için renk bağımsız: yalnız sarı-turuncu, doygun, parlak pikseller korunur.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Darken ("Darken amount (0..1)", Range(0,1)) = 0
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
            half4 _Color, _TextureSampleAdd;
            float _Darken;
            float4 _ClipRect;

            float3 ToHSV(float3 rgb)
            {
                float4 k = float4(0, -1.0 / 3.0, 2.0 / 3.0, -1);
                float4 p = lerp(float4(rgb.bg, k.wz), float4(rgb.gb, k.xy), step(rgb.b, rgb.g));
                float4 q = lerp(float4(p.xyw, rgb.r), float4(rgb.r, p.yzx), step(p.x, rgb.r));
                float d = q.x - min(q.w, q.y);
                return float3(abs(q.z + (q.w - q.y) / (6 * d + 1e-6)), d / (q.x + 1e-6), q.x);
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
                half4 c = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float3 rgb = c.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToGammaSpace(rgb);
                #endif
                float3 hsv = ToHSV(rgb);
                // Altın: ton ~25°–65°, doygun ve parlak. Kırmızı/bordo, yeşil, mavi gövdeler bu aralıkta değil.
                float gold = smoothstep(0.055, 0.085, hsv.x) * (1 - smoothstep(0.17, 0.20, hsv.x))
                    * smoothstep(0.28, 0.42, hsv.y) * smoothstep(0.32, 0.48, hsv.z);
                // Krem/beyaz süsler (köşe yıldızları, parlama) da korunur: açık ve az doygun.
                float cream = smoothstep(0.72, 0.85, hsv.z) * (1 - smoothstep(0.30, 0.45, hsv.y));
                c.rgb *= lerp(1 - _Darken, 1, max(gold, cream));
                half4 color = c * i.color;
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
