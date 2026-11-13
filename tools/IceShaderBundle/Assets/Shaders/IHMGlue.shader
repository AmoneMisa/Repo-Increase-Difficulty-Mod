// Sticky glue (IncreaseDifficultyMod: Sticky Pistol, Glue Grenade). Built-in render pipeline, original code.
// One shader for three uses, picked by _Coat:
//   _Coat = 0: the puddle and the stretched strands (procedural meshes, drawn whole);
//   _Coat = 1: a gel coat over a stuck monster, drawn on twins of its own renderers. Only the lower part of the body
//              is covered, under a wobbling edge with drips hanging below it (_Base/_Height/_Coverage/_Drip).
// Look: thick, wet, slightly translucent pink gel. Lumpy slow-flowing surface normals, darker where the gel is deep,
// light scattering through thin edges, suspended bubbles, a fresnel sheen, reflections from the nearest probe and
// sharp wet highlights from real lights (ForwardAdd: flashlights and lamps are what players see by).
Shader "IHM/Glue"
{
    Properties
    {
        _Color ("Gel colour", Color) = (0.95, 0.36, 0.68, 1)
        _DeepColor ("Deep colour", Color) = (0.45, 0.05, 0.28, 1)
        _Inflate ("Coat thickness (m)", Float) = 0.025
        _Wobble ("Wobble (m)", Float) = 0.006
        _Scale ("Lump frequency", Float) = 9
        _Flow ("Flow speed", Float) = 0.12
        _Bubbles ("Bubbles", Range(0, 1)) = 0.5
        _Gloss ("Highlight sharpness", Float) = 180
        _Reflect ("Reflection", Float) = 0.55
        _Opacity ("Opacity", Range(0, 1)) = 0.88
        _Coat ("Coat mode", Range(0, 1)) = 0
        _Base ("Body base (world Y)", Float) = 0
        _Height ("Body height (m)", Float) = 1
        _Coverage ("Covered share of the body", Range(0, 1)) = 0.35
        _Drip ("Drip length (share of height)", Float) = 0.12
        _Fade ("Fade", Range(0, 1)) = 1
    }
    CGINCLUDE
    #include "UnityCG.cginc"
    #include "Lighting.cginc"
    #include "AutoLight.cginc"

    float4 _Color, _DeepColor;
    float _Inflate, _Wobble, _Scale, _Flow, _Bubbles, _Gloss, _Reflect, _Opacity, _Coat, _Base, _Height, _Coverage, _Drip, _Fade;

    float hash(float3 p)
    {
        p = frac(p * 0.3183099 + 0.1);
        p *= 17.0;
        return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
    }

    float noise(float3 x)
    {
        float3 i = floor(x); float3 f = frac(x); f = f * f * (3 - 2 * f);
        return lerp(lerp(lerp(hash(i), hash(i + float3(1, 0, 0)), f.x), lerp(hash(i + float3(0, 1, 0)), hash(i + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(hash(i + float3(0, 0, 1)), hash(i + float3(1, 0, 1)), f.x), lerp(hash(i + float3(0, 1, 1)), hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
    }

    float fbm(float3 p) { return noise(p) * 0.55 + noise(p * 2.1 + 7.3) * 0.3 + noise(p * 4.3 + 1.7) * 0.15; }

    // Slowly sliding lumps: the gel creeps downward over time.
    float3 FlowPos(float3 w) { return w * _Scale + float3(0, _Time.y * _Flow * _Scale, 0); }

    // Bumped normal from the lump field (finite differences in world space).
    float3 GelNormal(float3 w, float3 n)
    {
        float e = 0.02 / max(_Scale, 0.1) * 9;
        float3 p = FlowPos(w);
        float h = fbm(p);
        float3 g = float3(fbm(p + float3(e * _Scale, 0, 0)) - h, fbm(p + float3(0, e * _Scale, 0)) - h, fbm(p + float3(0, 0, e * _Scale)) - h) / e;
        return normalize(n - (g - n * dot(g, n)) * 0.03);
    }

    // The coat's lower edge: covered below a wobbling line, with drips hanging from it.
    float CoatMask(float3 w)
    {
        if (_Coat < 0.5) return 1;
        float h = (w.y - _Base) / max(0.05, _Height);
        float angle = atan2(w.z, w.x);
        // A soft, lumpy creeping edge: rounded tongues of gel climbing the body, never spikes.
        float edge = _Coverage + (noise(float3(angle * 2.3, 0, _Time.y * 0.04)) - 0.5) * 0.07;
        float lobes = smoothstep(0.35, 0.95, noise(float3(angle * 3.7, 4.2, 0)));
        float drip = lobes * lobes * (3 - 2 * lobes) * _Drip * (0.9 + 0.1 * sin(_Time.y * 1.1 + angle * 3));
        float cover = (edge + drip) * _Fade;
        return saturate((cover - h) * 14);
    }

    struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
    struct v2f
    {
        float4 pos : SV_POSITION;
        float3 world : TEXCOORD0;
        float3 normal : TEXCOORD1;
        float depth : TEXCOORD2; // 0 at the surface edge, 1 where the gel is thick
        UNITY_FOG_COORDS(3)
    };

    v2f vert(appdata v)
    {
        v2f o;
        float3 n = normalize(UnityObjectToWorldNormal(v.normal));
        float4 w = mul(unity_ObjectToWorld, v.vertex);
        float lump = fbm(FlowPos(w.xyz) * 0.5);
        float wobble = sin(_Time.y * 2.6 + dot(w.xyz, float3(7.1, 3.3, 5.7))) * _Wobble;
        w.xyz += n * (_Inflate * (0.6 + 0.8 * lump) + wobble) * _Fade;
        o.pos = mul(UNITY_MATRIX_VP, w);
        o.world = w.xyz;
        o.normal = n;
        o.depth = lump;
        UNITY_TRANSFER_FOG(o, o.pos);
        return o;
    }

    float Bubble(float3 p)
    {
        float3 q = p * 22;
        float3 cell = floor(q);
        float3 r = float3(hash(cell), hash(cell + 11.1), hash(cell + 23.7));
        if (r.x > 0.35) return 0;
        float d = length(frac(q) - (0.25 + r * 0.5)) / (0.12 + r.y * 0.16);
        return smoothstep(1.0, 0.7, d) * (0.3 + 0.7 * smoothstep(0.45, 0.95, d));
    }
    ENDCG

    SubShader
    {
        Tags { "Queue" = "Transparent+15" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            float4 frag(v2f i) : SV_Target
            {
                float mask = CoatMask(i.world);
                clip(mask - 0.01);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 N = GelNormal(i.world, normalize(i.normal));
                if (dot(N, V) < 0) N = -N;
                float ndv = saturate(dot(N, V));
                float fresnel = pow(1 - ndv, 3);

                // Thick gel is darker and more saturated; thin edges let light through and glow pink.
                float thick = saturate(i.depth * 0.8 + ndv * 0.5);
                float3 gel = lerp(_Color.rgb * 1.15, _DeepColor.rgb, thick);
                float3 ambient = max(ShadeSH9(float4(N, 1)), 0.08);
                float3 col = gel * (ambient + 0.25);
                col += _Color.rgb * (1 - ndv) * 0.35;                       // light scattered through thin gel
                col += Bubble(i.world + float3(0, _Time.y * 0.02, 0)) * _Bubbles * float3(1, 0.8, 0.92) * 0.6;

                float3 R = reflect(-V, N);
                float3 env = DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, R), unity_SpecCube0_HDR);
                col += env * _Reflect * (0.15 + fresnel * 0.85);
                // A fixed soft overhead sheen so the gel reads wet even in dark rooms.
                col += pow(saturate(dot(R, normalize(float3(0.3, 1, 0.2)))), _Gloss * 0.35) * 0.35;
                col += fresnel * float3(1, 0.75, 0.9) * 0.45;

                float alpha = saturate(_Opacity + fresnel * 0.25) * mask * _Fade;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return float4(col, alpha);
            }
            ENDCG
        }
        Pass
        {
            // Point and spot lights (flashlights, lamps): diffuse through the gel plus a sharp wet highlight.
            Tags { "LightMode" = "ForwardAdd" }
            ZWrite Off
            Cull Back
            Blend SrcAlpha One
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAdd
            #pragma multi_compile_fwdadd
            #pragma target 3.0

            float4 fragAdd(v2f i) : SV_Target
            {
                float mask = CoatMask(i.world);
                clip(mask - 0.01);
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                float3 N = GelNormal(i.world, normalize(i.normal));
                if (dot(N, V) < 0) N = -N;
                float3 L = normalize(UnityWorldSpaceLightDir(i.world));
                UNITY_LIGHT_ATTENUATION(atten, 0, i.world);
                float3 H = normalize(L + V);
                float diffuse = saturate(dot(N, L) * 0.6 + 0.4);           // wrapped: light soaks into the gel
                float spec = pow(saturate(dot(N, H)), _Gloss) * 2.2 + pow(saturate(dot(N, H)), _Gloss * 0.2) * 0.25;
                float3 col = (_Color.rgb * diffuse * 0.8 + spec) * _LightColor0.rgb * atten;
                return float4(col, saturate(_Opacity) * mask * _Fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
