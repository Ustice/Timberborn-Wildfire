// GPU-driven smoke / steam billboard shader.
// Smoke: _PuffsPerCell staggered puffs per cell with deterministic jitter and noisy breakup.
// Steam: _PuffsPerCell staggered rising puffs per cell with lighter vertical vapor.
//        instanceID = cellIndex * PuffsPerCell + puffSlot.
// Intensity is read from _SmoothedFields (temporally smoothed, no popping).
// Toxic smoke uses burgundy (not green): contamination lerps base color toward (0.35, 0.05, 0.10).
// Blend: SrcAlpha OneMinusSrcAlpha (standard alpha).
// Requires #pragma target 4.5 for StructuredBuffer in vertex stage.
Shader "Wildfire/Cloud"
{
    Properties
    {
        _BaseColor      ("Base Color",                Color)       = (0.27, 0.28, 0.27, 1)
        _ContamColor    ("Contamination Color",       Color)       = (0.35, 0.05, 0.10, 1)
        _Radius         ("Billboard Radius (world)",  Float)       = 1.1
        _HeightOffset   ("Center Height Offset",      Float)       = 1.2
        _MaxSteamHeight ("Steam Rise Height (world)", Float)       = 2.2
        _MaxOpacity     ("Max Opacity",               Range(0, 1)) = 0.62
        _IsSteam        ("Is Steam (0=smoke 1=steam)",Float)       = 0
        _PuffsPerCell   ("Puffs Per Cell",            Float)       = 1
        _Wind           ("Wind X,Y,Strength",         Vector)      = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+2"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            // _SmoothedFields per cell: float4(fire, smoke, smokeContam, steam).
            StructuredBuffer<float4> _SmoothedFields;
            StructuredBuffer<float3> _CellWorldPositions;

            float4 _BaseColor;
            float4 _ContamColor;
            float  _Radius;
            float  _HeightOffset;
            float  _MaxSteamHeight;
            float  _MaxOpacity;
            float  _IsSteam;
            float  _PuffsPerCell;
            float4 _Wind;

            // Six corners: two CCW triangles forming a screen-facing quad.
            static const float2 BillboardCorners[6] =
            {
                float2(-0.5, -0.5), float2( 0.5, -0.5), float2( 0.5,  0.5),
                float2(-0.5, -0.5), float2( 0.5,  0.5), float2(-0.5,  0.5),
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float  intensity : TEXCOORD1;
                float  contam    : TEXCOORD2;
                float  seed      : TEXCOORD3;
                float  isSteam   : TEXCOORD4;
                float3 worldPos  : TEXCOORD5;
            };

            // Wang hash: maps uint → uniform [0,1).
            float Hash(uint n)
            {
                n = (n ^ 61u) ^ (n >> 16u);
                n *= 9u;
                n ^= n >> 4u;
                n *= 0x27d4eb2du;
                n ^= n >> 15u;
                return (float)(n & 0x7FFFFFFFu) / 2147483648.0;
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float2 shifted = abs(i) + 4096.0;
                uint ax = (uint)shifted.x;
                uint ay = (uint)shifted.y;
                uint h00 = ax * 1973u + ay * 9277u;
                uint h10 = (ax + 1u) * 1973u + ay * 9277u;
                uint h01 = ax * 1973u + (ay + 1u) * 9277u;
                uint h11 = (ax + 1u) * 1973u + (ay + 1u) * 9277u;

                float x0 = lerp(Hash(h00), Hash(h10), u.x);
                float x1 = lerp(Hash(h01), Hash(h11), u.x);
                return lerp(x0, x1, u.y);
            }

            float CloudNoise(float2 p)
            {
                return ValueNoise(p) * 0.62 +
                    ValueNoise(p * 2.13 + 17.0) * 0.28 +
                    ValueNoise(p * 4.07 + 43.0) * 0.10;
            }

            v2f vert(uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                v2f o;

                uint puffsPerCell = max(1u, (uint)_PuffsPerCell);
                uint cellIndex    = instanceID / puffsPerCell;
                uint puffSlot     = instanceID % puffsPerCell;

                float4 smoothed   = _SmoothedFields[cellIndex];
                float  smoke      = smoothed.y;
                float  smokeContam = smoothed.z;
                float  steam      = smoothed.w;

                float intensity = _IsSteam > 0.5 ? steam : smoke;
                float contam    = _IsSteam > 0.5 ? 0.0  : smokeContam;
                float renderIntensity = _IsSteam > 0.5
                    ? intensity
                    : saturate(intensity * 1.35);

                if (renderIntensity < 0.015)
                {
                    o.pos       = float4(2.0, 2.0, 2.0, 1.0);
                    o.uv        = float2(0.5, 0.5);
                    o.intensity = 0.0;
                    o.contam    = 0.0;
                    o.seed      = 0.0;
                    o.isSteam   = _IsSteam > 0.5 ? 1.0 : 0.0;
                    o.worldPos  = float3(0.0, 0.0, 0.0);
                    return o;
                }

                float3 worldPos = _CellWorldPositions[cellIndex];

                float isSteam = _IsSteam > 0.5 ? 1.0 : 0.0;
                float seedA = Hash(cellIndex * 73u + puffSlot * 17u + 11u);
                float seedB = Hash(cellIndex * 89u + puffSlot * 31u + 23u);
                float seedC = Hash(cellIndex * 107u + puffSlot * 47u + 37u);
                float seedD = Hash(cellIndex * 131u + puffSlot * 59u + 41u);

                float sizeMult  = 1.0;
                float alphaMult = 1.0;
                float heightY   = _HeightOffset;
                float windStrength = saturate(_Wind.z);
                float2 windDir = length(_Wind.xy) > 0.001 ? normalize(_Wind.xy) : float2(0.0, 0.0);
                float2 jitter = (float2(seedA, seedB) - 0.5) * (isSteam > 0.5 ? 0.44 : 0.72);
                float widthMult = isSteam > 0.5
                    ? lerp(0.72, 1.08, seedC)
                    : lerp(0.92, 1.42, seedC);
                float heightMult = isSteam > 0.5
                    ? lerp(1.08, 1.58, seedD)
                    : lerp(0.78, 1.18, seedD);

                if (isSteam > 0.5 && puffsPerCell > 1u)
                {
                    float stagger   = (float)puffSlot / (float)puffsPerCell;
                    float puffPhase = frac(_Time.y * 0.68 + stagger + seedA * 0.23);

                    heightY = puffPhase * _MaxSteamHeight;

                    float envelope = sin(puffPhase * 3.14159265);
                    sizeMult  = 0.34 + envelope * 0.82;
                    alphaMult = envelope * lerp(0.72, 1.0, seedB);
                    jitter *= lerp(0.38, 0.88, envelope);
                    jitter += windDir * windStrength * puffPhase * 0.52;
                }
                else if (puffsPerCell > 1u)
                {
                    float stagger = (float)puffSlot / (float)puffsPerCell;
                    float drift = _Time.y * lerp(0.018, 0.038, seedD) + stagger * 6.2831853;
                    float smokePhase = frac(_Time.y * lerp(0.045, 0.075, seedD) + stagger + seedA * 0.37);
                    float smokeEnvelope = smoothstep(0.02, 0.22, smokePhase) * (1.0 - smoothstep(0.72, 1.0, smokePhase));
                    float orderedSlot = (float)puffSlot / max(1.0, (float)(puffsPerCell - 1u));
                    float slotThreshold = saturate(lerp(0.0, 0.46, orderedSlot) * lerp(0.76, 1.12, seedC));
                    float slotActivation = smoothstep(slotThreshold, min(1.0, slotThreshold + 0.18), renderIntensity);
                    if (slotActivation * smokeEnvelope < 0.002)
                    {
                        o.pos       = float4(2.0, 2.0, 2.0, 1.0);
                        o.uv        = float2(0.5, 0.5);
                        o.intensity = 0.0;
                        o.contam    = 0.0;
                        o.seed      = seedA;
                        o.isSteam   = isSteam;
                        o.worldPos  = worldPos;
                        return o;
                    }

                    jitter += float2(sin(drift), cos(drift * 0.73 + seedA * 6.2831853)) * 0.055;
                    jitter += windDir * windStrength * (smokePhase * 0.82 + seedA * 0.16);
                    jitter += float2(-windDir.y, windDir.x) * windStrength * sin(_Time.y * 0.21 + seedB * 6.2831853) * 0.08;
                    heightY += (seedD - 0.5) * 0.48 + smokePhase * lerp(0.22, 0.50, windStrength);
                    sizeMult = lerp(0.78, 1.56, saturate(renderIntensity * 0.84 + seedA * 0.42));
                    alphaMult = slotActivation * smokeEnvelope * lerp(0.84, 1.18, seedB) * lerp(0.78, 1.56, renderIntensity);
                }

                worldPos.xz += jitter;
                worldPos.y += heightY;

                // Camera-facing billboard using view-matrix world-space axes.
                float3 camRight = float3(UNITY_MATRIX_V[0][0], UNITY_MATRIX_V[1][0], UNITY_MATRIX_V[2][0]);
                float3 camUp    = float3(UNITY_MATRIX_V[0][1], UNITY_MATRIX_V[1][1], UNITY_MATRIX_V[2][1]);

                float2 corner = BillboardCorners[vertexID];
                float  r      = _Radius * sizeMult;
                float3 vPos   = worldPos +
                    camRight * (corner.x * r * widthMult) +
                    camUp * (corner.y * r * heightMult);

                o.pos       = UnityWorldToClipPos(vPos);
                o.uv        = corner + 0.5;
                o.intensity = renderIntensity * alphaMult;
                o.contam    = contam;
                o.seed      = seedA;
                o.isSteam   = isSteam;
                o.worldPos  = worldPos;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 centered = i.uv * 2.0 - 1.0;
                float isSteam = i.isSteam > 0.5 ? 1.0 : 0.0;
                float radius = length(centered * float2(isSteam > 0.5 ? 0.78 : 0.66, 1.0));
                float body = 1.0 - smoothstep(isSteam > 0.5 ? 0.20 : 0.16, 1.08, radius);
                float softEdge = 1.0 - smoothstep(isSteam > 0.5 ? 0.54 : 0.60, 1.04, radius);
                float lowerFade = smoothstep(isSteam > 0.5 ? -0.28 : -0.18, isSteam > 0.5 ? 0.08 : 0.14, centered.y);
                float upperFade = 1.0 - smoothstep(isSteam > 0.5 ? 0.42 : 0.72, 1.08, centered.y);
                float noise = CloudNoise(i.uv * (isSteam > 0.5 ? 2.15 : 3.35) +
                    i.worldPos.xz * (isSteam > 0.5 ? 0.10 : 0.16) +
                    i.seed * 19.0 +
                    _Time.y * (isSteam > 0.5 ? 0.18 : 0.055));
                float breakup = lerp(smoothstep(0.26, 0.84, noise), 1.0, isSteam);
                float alpha = body * softEdge * lowerFade * upperFade * lerp(0.32, 1.0, breakup);
                alpha *= i.intensity * _MaxOpacity;

                float3 smokeBase = lerp(_BaseColor.rgb * 0.72, _BaseColor.rgb * 1.18, noise);
                float3 dirtyBase = lerp(smokeBase, _ContamColor.rgb, saturate(i.contam) * 0.82);
                float3 steamBase = lerp(_BaseColor.rgb * 0.86, float3(1.0, 1.0, 1.0), saturate(i.uv.y + noise * 0.34));
                float3 col = lerp(dirtyBase, steamBase, isSteam);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
