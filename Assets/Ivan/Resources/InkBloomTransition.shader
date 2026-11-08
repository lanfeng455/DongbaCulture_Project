// Title → 森林的墨晕转场：墨点从触发位置向外晕开盖满全屏，加载完成后再退墨显形。
// 纯程序化噪声（hash + fbm）蚀刻墨缘，不依赖任何贴图；挂在全屏 RawImage 上使用。
Shader "Ivan/InkBloomTransition"
{
    Properties
    {
        _Progress ("Ink spread", Range(0, 1)) = 0
        _Origin ("Bloom origin (viewport)", Vector) = (0.5, 0.5, 0, 0)
        _InkColor ("Ink color", Color) = (0.13, 0.10, 0.08, 1)
        _HaloColor ("Wet paper halo", Color) = (0.42, 0.35, 0.27, 0.55)
        // 供 RawImage 使用的默认贴图槽位，墨晕本身不采样它。
        _MainTex ("Unused", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            // 材质参数集中放进 UnityPerMaterial，与项目内其他 shader 保持一致（SRP Batcher 友好）。
            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float4 _Origin;
                float4 _InkColor;
                float4 _HaloColor;
            CBUFFER_END

            struct app { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };

            v2f vert(app v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // 简易 hash / value noise：让墨缘呈现纸张晕染的不规则形状。
            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                float a = Hash(i), b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1)), d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = .5;
                [unroll]
                for (int n = 0; n < 4; n++) { v += a * Noise(p); p *= 2.03; a *= .5; }
                return v;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                if (_Progress <= 0) return 0;

                // 以晕点为原点做纵横比校正；d 以“当前墨半径”为单位，墨缘约在 d=1 处。
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 q = float2((i.uv.x - _Origin.x) * aspect, i.uv.y - _Origin.y);
                float d = length(q) / max(_Progress, 1e-4);

                // 噪声蚀刻墨缘，并让纹理随扩散缓慢流动，像墨在纸里渗开。
                float w = Fbm(q * 3.0 + _Progress * .5);
                float ragged = d * (0.78 + 0.44 * w);
                float edge = smoothstep(1.04, 0.92, ragged);

                // 湿晕圈：主墨缘外侧的一圈浅色，先于墨到达，营造纸面被浸湿的感觉。
                float halo = smoothstep(1.45, 1.02, ragged) * (1 - edge);

                // 进度收尾时整屏强制铺满，杜绝噪声在高宽比下于角落留洞。
                edge = max(edge, smoothstep(0.75, 1.0, _Progress));

                // 墨面内部保留浓淡颗粒，避免变成一块死平的色块。
                float grain = 0.91 + 0.18 * Fbm(q * 6.5);
                float3 col = lerp(_HaloColor.rgb, _InkColor.rgb * grain, edge);
                float alpha = max(edge * _InkColor.a, halo * _HaloColor.a);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
