Shader "NpcLab/LabSurface"
{
    Properties { _Color ("Color", Color) = (0.3,0.6,0.8,1) }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; };
            struct v2f { float4 position:SV_POSITION; float shade:TEXCOORD0; };
            fixed4 _Color;
            v2f vert(appdata v)
            {
                v2f o; o.position=UnityObjectToClipPos(v.vertex);
                float3 normal=UnityObjectToWorldNormal(v.normal);
                o.shade=0.5+0.5*max(0,dot(normal,normalize(float3(-0.4,1,-0.3))));
                return o;
            }
            fixed4 frag(v2f i):SV_Target { return fixed4(_Color.rgb*i.shade,_Color.a); }
            ENDCG
        }
    }
}
