Shader "Dongba/XR Forest Paper" {
Properties { _MainTex("Paper fibers",2D)="white" {} _Decline("Decline",Range(0,1))=0 }
SubShader { Tags {"Queue"="Transparent-100" "RenderType"="Transparent"} Cull Off ZWrite Off
Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile_instancing
#include "UnityCG.cginc"
sampler2D _MainTex;float _Decline,_DongbaMRNight;
struct app {float4 vertex:POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float2 ground:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO};
v2f vert(app v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.ground=mul(unity_ObjectToWorld,v.vertex).xz;return o;}
fixed4 frag(v2f i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
fixed3 c=tex2D(_MainTex,i.uv).rgb+_Decline*(fixed3(.865,.867,.843)-fixed3(.945,.904,.811));
c*=lerp(fixed3(1,1,1),fixed3(.55,.62,.76),_DongbaMRNight);
// No grove/clearing alpha variation: the room cannot darken patches of paper.
float edge=min(46-abs(i.ground.x),21-abs(i.ground.y));
return fixed4(saturate(c),smoothstep(0,.85,edge));
} ENDCG }
} }
