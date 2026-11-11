Shader "CandyCruisers/StarColors"
{
    Properties { _MainTex ("Stars", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            struct input { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; float2 world : TEXCOORD1; };
            output vert(input v)
            {
                output o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; o.world = mul(unity_ObjectToWorld, v.vertex).xy; return o;
            }
            fixed4 frag(output i) : SV_Target
            {
                clip(2.955 - abs(i.world.x));
                clip(5.575 - abs(i.world.y));
                fixed4 tex = tex2D(_MainTex, i.uv);
                return tex * i.color;
            }
            ENDCG
        }
    }
}
