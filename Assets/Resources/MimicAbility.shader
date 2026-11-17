Shader "CandyCruisers/MimicAbility"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _HostColor ("Host", Color) = (0,1,0,1)
        _YellowColor ("Yellow", Color) = (1,1,0,1)
        _HostSides ("Left right up down", Vector) = (0,0,0,0)
        _EffectCenter ("World center", Vector) = (0,0,0,0)
        _Split ("Split coverage", Float) = 0
        _Gradient ("Arrow shading", Float) = 0
        [HideInInspector] _RendererColor ("Renderer color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable external alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
        Cull Off ZWrite Off Lighting Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            fixed4 _HostColor, _YellowColor;
            float4 _HostSides, _EffectCenter;
            float _Split, _Gradient;
            struct output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; float local : TEXCOORD2; fixed alpha : COLOR; };
            output vert(appdata_t v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                output o;
                float4 position = UnityFlipSprite(v.vertex, _Flip);
                o.vertex = UnityObjectToClipPos(position);
                o.uv = v.texcoord;
                o.world = mul(unity_ObjectToWorld, position).xy;
                o.local = v.vertex.x + .5;
                o.alpha = v.color.a * _RendererColor.a;
                return o;
            }
            fixed4 frag(output i) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(i.uv);
                float2 p = i.world - _EffectCenter.xy;
                float covered = saturate(_HostSides.x * step(p.x, 0) + _HostSides.y * step(0, p.x) +
                    _HostSides.z * step(0, p.y) + _HostSides.w * step(p.y, 0));
                fixed4 tint = lerp(_HostColor, lerp(_YellowColor, _HostColor, covered), _Split);
                float shade = lerp(1, lerp(1.15, .55, saturate(i.local)), _Gradient);
                return fixed4(tex.rgb * tint.rgb * shade, tex.a * i.alpha * _Color.a);
            }
            ENDCG
        }
    }
}
