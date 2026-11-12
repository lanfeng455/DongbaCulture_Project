Shader "Dongba/KnifeBloodInk" {
Properties { [PerRendererData] _MainTex ("Original dictionary glyph",2D)="white" {} _Blood ("Dried brown blood",Color)=(0.38,0.145,0.075,1) }
SubShader { Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" } Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;};
sampler2D _MainTex;fixed4 _Blood;
v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;}
float stain(float2 uv,float2 center,float2 radius){return 1-smoothstep(.4,1,length((uv-center)/radius));}
fixed4 frag(v2f i):SV_Target {
    fixed4 source=tex2D(_MainTex,i.uv);
    float patch=max(stain(i.uv,float2(.70,.48),float2(.075,.23)),stain(i.uv,float2(.86,.55),float2(.043,.17)));
    patch=max(patch,stain(i.uv,float2(.56,.40),float2(.025,.10)));
    float grain=.80+.20*sin(i.uv.x*291+i.uv.y*57)*sin(i.uv.y*237);
    // Tint only existing ink. Original silhouette and transparent pixels remain untouched.
    return fixed4(lerp(source.rgb,_Blood.rgb,max(patch*grain,step(.68,i.uv.x)))*i.color.rgb,source.a*i.color.a);
}
ENDCG }
}}
