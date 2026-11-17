Shader "CandyCruisers/YellowAttachment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _HostColor ("Host", Color) = (1,1,1,1)
        _YellowColor ("Yellow", Color) = (1,1,0,1)
        _HostSides ("Left right up down", Vector) = (0,0,0,0)
        _SpriteCenter ("Sprite center", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 _HostColor, _YellowColor;
            float4 _HostSides, _SpriteCenter;
            struct input { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 local : TEXCOORD1; fixed4 color : COLOR; };
            output vert(input v)
            {
                output o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; o.local = v.vertex.xy; o.color = v.color;
                return o;
            }
            fixed4 frag(output i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float2 p = i.local - _SpriteCenter.xy;
                float covered = saturate(_HostSides.x * step(p.x, 0) + _HostSides.y * step(0, p.x) +
                    _HostSides.z * step(0, p.y) + _HostSides.w * step(p.y, 0));
                fixed4 tint = lerp(_YellowColor, _HostColor, covered);
                return fixed4(tex.rgb * tint.rgb, tex.a * i.color.a);
            }
            ENDCG
        }
    }
}
