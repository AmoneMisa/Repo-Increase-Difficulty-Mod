// World-space triplanar surface for authored rooms. Boxes of any size share one texel density,
// so no per-box UV meshes are needed. Albedo alpha scales smoothness (joints, pores and cracks stay matte).
Shader "LevelAuthoring/Triplanar"
{
    Properties
    {
        _Color ("Colour", Color) = (1,1,1,1)
        _MainTex ("Surface (rgb tone, a gloss mask)", 2D) = "white" {}
        _Tile ("Tiles per metre", Float) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.1
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _Grime ("Grime near the floor", Range(0,1)) = 0.3
        [Toggle] _UseUV ("Use mesh UVs (pictures, window views)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color, _EmissionColor;
        half _Tile, _Glossiness, _Metallic, _Grime, _UseUV;

        struct Input { float3 worldPos; float3 worldNormal; float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // The small bias keeps flat double-sided meshes (zero averaged normal) from dividing by zero.
            float3 weight = pow(abs(IN.worldNormal), 4) + 1e-4;
            weight /= (weight.x + weight.y + weight.z);
            float3 p = IN.worldPos * _Tile;
            fixed4 c = tex2D(_MainTex, p.zy) * weight.x + tex2D(_MainTex, p.xz) * weight.y + tex2D(_MainTex, p.xy) * weight.z;
            // A picture (window view) is mapped once across the mesh instead of tiling through the world.
            c = lerp(c, tex2D(_MainTex, IN.uv_MainTex), step(0.5, _UseUV));
            // Vertical surfaces darken toward the floor, where dirt collects.
            float wall = 1 - saturate(abs(IN.worldNormal.y) * 1.5);
            float low = saturate(1 - IN.worldPos.y / 1.6) * step(-0.3, IN.worldPos.y);
            c.rgb *= 1 - _Grime * wall * low * 0.55;
            o.Albedo = c.rgb * _Color.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness * c.a;
            o.Emission = _EmissionColor.rgb * c.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
