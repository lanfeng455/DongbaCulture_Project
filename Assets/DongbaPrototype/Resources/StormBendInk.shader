Shader "Dongba/StormBendInk" {
Properties {
 [PerRendererData] _MainTex("Original glyph",2D)="white" {}
 _Growth("Root to crown",Range(0,1))=0
 _Green("Living ink",Color)=(.30,.40,.13,1)
 _Bend("Crown deflection",Float)=0
 _Height("Original height",Float)=1
 _PartScale("Layer scale",Float)=1
 _PartOffset("Layer anchor",Float)=0
}
SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="False" "DisableBatching"="True"}
 Cull Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.0
 #pragma multi_compile_instancing
 #include "UnityCG.cginc"
 CBUFFER_START(UnityPerMaterial)
 float _Growth,_Bend,_Height,_PartScale,_PartOffset;
 fixed4 _Green;
 CBUFFER_END
 sampler2D _MainTex;
 struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;fixed4 color:COLOR;UNITY_VERTEX_OUTPUT_STEREO};
 v2f vert(app v){
   v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
   float height=saturate((v.vertex.y*_PartScale+_PartOffset)/max(.01,_Height));
   v.vertex.x+=_Bend*height*height/max(.01,_PartScale);
   o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;return o;
 }
 fixed4 frag(v2f i):SV_Target {
   UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
   fixed4 ink=tex2D(_MainTex,i.uv);
   float edge=lerp(-.035,1.035,_Growth);
   float cover=1-smoothstep(edge-.035,edge+.035,i.uv.y);
   fixed3 living=_Green.rgb*(.85+.35*max(ink.r,max(ink.g,ink.b)));
   ink.rgb=lerp(ink.rgb,living,cover);return ink*i.color;
 }
 ENDCG }
} }
