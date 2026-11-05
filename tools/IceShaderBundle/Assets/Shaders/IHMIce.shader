// Ice that encases a frozen monster (IncreaseDifficultyMod, Ice Pistol). Built-in render pipeline.
// Drawn over duplicates of the monster's own renderers in up to three layers (inner refracting ice, then
// frostier outer shells). The vertex stage puffs the surface outward and chips it into crystals, with
// occasional crystal growths; the fragment stage uses screen-space facet normals (cut faces), refracts what
// is behind through a shared grab texture, adds triplanar cracks/frost, suspended air bubbles, a white rime
// crust over the lower body, reflections from the nearest probe, a fresnel rim and glints.
Shader "IHM/Ice"
{
    Properties
    {
        _Color ("Ice tint", Color) = (0.72, 0.9, 1, 1)
        _CrackTex ("Cracks (A = cracks+frost)", 2D) = "white" {}
        _Tiling ("Crack tiling (per metre)", Float) = 2.2
        _Inflate ("Thickness (m)", Float) = 0.06
        _Lumps ("Chip size (m)", Float) = 0.035
        _ChipScale ("Chip frequency", Float) = 7
        _Growth ("Crystal growth (m)", Float) = 0
        _Refraction ("Refraction", Float) = 0.045
        _Frost ("Frost / crack whiteness", Range(0, 1)) = 0.55
        _Bubbles ("Air bubbles", Range(0, 1)) = 0.6
        _BubbleScale ("Bubble frequency", Float) = 14
        _RimColor ("Rim colour", Color) = (0.82, 0.9, 1, 1)
        _RimPower ("Rim power", Float) = 2.6
        _RimStrength ("Rim strength", Float) = 1.1
        _Reflect ("Reflection", Float) = 0.6
        _Base ("Rime base (world Y)", Float) = 0
        _Height ("Body height (m)", Float) = 1
        _Rime ("Rime strength", Range(0, 1)) = 0.8
        _Opacity ("Layer opacity", Range(0, 1)) = 1
        _UseGrab ("Refract the scene (inner layer only)", Range(0, 1)) = 1
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+20" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        // One shared grab per frame for every frozen monster.
        GrabPass { "_IHMIceGrab" }
        Pass
        {
            ZWrite Off
            Cull Back
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _IHMIceGrab;
            sampler2D _CrackTex;
            float4 _Color, _RimColor;
            float _Tiling, _Inflate, _Lumps, _ChipScale, _Growth, _Refraction, _Frost, _Bubbles, _BubbleScale;
            float _RimPower, _RimStrength, _Reflect, _Base, _Height, _Rime, _Opacity, _UseGrab, _Fade;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 grab : TEXCOORD0;
                float3 world : TEXCOORD1;
            };

            float hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float3 hash3(float3 p)
            {
                return float3(hash(p), hash(p + 19.19), hash(p + 47.47));
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float4 w = mul(unity_ObjectToWorld, v.vertex);
                // Chips: coarse + fine per-cell pushes, so the surface breaks into separate crystals.
                float coarse = hash(floor(w.xyz * _ChipScale));
                float fine = hash(floor(w.xyz * _ChipScale * 2.7) + 3.1);
                float push = _Inflate + (coarse * 0.7 + fine * 0.3) * _Lumps;
                // Crystal growths: a few cells shoot out much further, favouring up and outward.
                float grow = saturate((coarse - 0.82) / 0.18);
                push += grow * _Growth * (0.6 + 0.4 * saturate(n.y + 0.5));
                w.xyz += n * push;
                o.pos = mul(UNITY_MATRIX_VP, w);
                o.grab = ComputeGrabScreenPos(o.pos);
                o.world = w.xyz;
                return o;
            }

            float4 Triplanar(float3 p, float3 n)
            {
                float3 w = pow(abs(n), 4);
                w /= (w.x + w.y + w.z);
                return tex2D(_CrackTex, p.zy * _Tiling) * w.x +
                       tex2D(_CrackTex, p.xz * _Tiling) * w.y +
                       tex2D(_CrackTex, p.xy * _Tiling) * w.z;
            }

            // Air bubbles: one random sphere per cell; returns a bright ring at the bubble's edge.
            float Bubble(float3 p)
            {
                float3 cell = floor(p * _BubbleScale);
                float best = 0;
                for (int x = 0; x <= 1; x++)
                for (int y = 0; y <= 1; y++)
                for (int z = 0; z <= 1; z++)
                {
                    float3 c = cell + float3(x, y, z) - 0.5;
                    float3 r = hash3(c);
                    if (r.x > 0.55) continue; // most cells are empty
                    float3 centre = (c + r) / _BubbleScale;
                    float radius = (0.12 + r.y * 0.22) / _BubbleScale;
                    float d = length(p - centre) / radius;
                    best = max(best, smoothstep(1.0, 0.82, d) * (0.35 + 0.65 * smoothstep(0.55, 0.95, d)));
                }
                return best;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.world);
                // Flat facet normal from screen derivatives: every triangle becomes a cut face.
                float3 N = normalize(cross(ddy(i.world), ddx(i.world)));
                if (dot(N, V) < 0) N = -N;

                float4 crack = Triplanar(i.world, N);
                float frost = crack.a;
                float rime = saturate(1 - (i.world.y - _Base) / max(0.05, _Height * 0.45)) * _Rime;
                rime *= 0.55 + 0.45 * frost; // grainy crust, not a flat band

                float fresnel = pow(1 - saturate(dot(N, V)), _RimPower);
                float3 viewN = mul((float3x3)UNITY_MATRIX_V, N);
                float2 offset = viewN.xy * _Refraction * (1 + frost);
                float3 behind = tex2Dproj(_IHMIceGrab, i.grab + float4(offset * i.grab.w, 0, 0)).rgb;

                // Clear blue ice over whatever is inside, clouded white by cracks, frost and bubbles.
                // Only the inner layer shows the (refracted) scene; outer shells must not, or each one
                // draws its own shifted copy of the monster and the image doubles.
                float3 ice = lerp(_Color.rgb * 0.3, behind * _Color.rgb, _UseGrab);
                ice = lerp(ice, float3(0.86, 0.94, 1.0), saturate(frost * _Frost));
                ice += Bubble(i.world) * _Bubbles * float3(0.75, 0.88, 1.0);
                ice = lerp(ice, float3(0.93, 0.97, 1.0), rime);

                float3 R = reflect(-V, N);
                float3 env = DecodeHDR(UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, R), unity_SpecCube0_HDR);
                ice += env * _Reflect * (0.12 + fresnel * 0.88) * (1 - rime * 0.6);
                ice += _RimColor.rgb * fresnel * _RimStrength;
                // Sharp glints where a facet catches the light from above.
                float glint = pow(saturate(dot(R, normalize(float3(0.35, 0.9, 0.25)))), 90);
                ice += glint * 1.6 * (1 - rime * 0.7);

                float alpha = saturate(_Opacity + fresnel * 0.5 + rime * 0.6 + frost * 0.25 * (1 - _Opacity));
                return float4(ice, alpha * _Fade);
            }
            ENDCG
        }
    }
    Fallback Off
}
