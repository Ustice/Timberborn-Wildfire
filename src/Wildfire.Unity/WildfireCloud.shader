// GPU-driven smoke / steam billboard shader.
// Smoke: 1 billboard per cell, sphere-shaded circular falloff, gray → burgundy by contamination.
// Steam: _PuffsPerCell billboards per cell on a continuous rising lifecycle.
//        instanceID = cellIndex * PuffsPerCell + puffSlot.
// Intensity is read from _SmoothedFields (temporally smoothed, no popping).
// Toxic smoke uses burgundy (not green): contamination lerps base color toward (0.35, 0.05, 0.10).
// Blend: SrcAlpha OneMinusSrcAlpha (standard alpha).
// Requires #pragma target 4.5 for StructuredBuffer in vertex stage.
Shader "Wildfire/Cloud"
{
    Properties
    {
        _BaseColor      ("Base Color",                Color)       = (0.45, 0.45, 0.45, 1)
        _ContamColor    ("Contamination Color",       Color)       = (0.35, 0.05, 0.10, 1)
        _Radius         ("Billboard Radius (world)",  Float)       = 0.65
        _HeightOffset   ("Center Height Offset",      Float)       = 1.2
        _MaxSteamHeight ("Steam Rise Height (world)", Float)       = 1.8
        _MaxOpacity     ("Max Opacity",               Range(0, 1)) = 0.45
        _IsSteam        ("Is Steam (0=smoke 1=steam)",Float)       = 0
        _PuffsPerCell   ("Puffs Per Cell",            Float)       = 1
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

                if (intensity < 0.015)
                {
                    o.pos       = float4(2.0, 2.0, 2.0, 1.0);
                    o.uv        = float2(0.5, 0.5);
                    o.intensity = 0.0;
                    o.contam    = 0.0;
                    return o;
                }

                float3 worldPos = _CellWorldPositions[cellIndex];

                // --- Steam rising-puff lifecycle ---
                // Each puff cycles continuously: rises from ground, fades in/out.
                // Phase offset is staggered so puffs don't all start at the same time.
                float sizeMult  = 1.0;
                float alphaMult = 1.0;
                float heightY   = _HeightOffset;

                if (_IsSteam > 0.5 && puffsPerCell > 1u)
                {
                    // Stagger puff start phases evenly across [0,1).
                    float stagger   = (float)puffSlot / (float)puffsPerCell;
                    float randOfs   = Hash(cellIndex * 3u + puffSlot) * 0.25; // small random jitter
                    float puffPhase = frac(_Time.y * 0.45 + stagger + randOfs);

                    // Height: rises linearly from 0 to _MaxSteamHeight.
                    heightY = puffPhase * _MaxSteamHeight;

                    // Envelope: sin curve → small at birth/death, full size in the middle.
                    float envelope = sin(puffPhase * 3.14159265);
                    sizeMult  = 0.25 + envelope * 0.75;
                    alphaMult = envelope;
                }

                worldPos.y += heightY;

                // Camera-facing billboard using view-matrix world-space axes.
                float3 camRight = float3(UNITY_MATRIX_V[0][0], UNITY_MATRIX_V[1][0], UNITY_MATRIX_V[2][0]);
                float3 camUp    = float3(UNITY_MATRIX_V[0][1], UNITY_MATRIX_V[1][1], UNITY_MATRIX_V[2][1]);

                float2 corner = BillboardCorners[vertexID];
                float  r      = _Radius * sizeMult;
                float3 vPos   = worldPos + camRight * (corner.x * r) + camUp * (corner.y * r);

                o.pos       = UnityWorldToClipPos(vPos);
                o.uv        = corner + 0.5;
                o.intensity = intensity * alphaMult;
                o.contam    = contam;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;

                // Smooth sphere-like falloff: sharp circular edge, soft interior glow.
                float circleMask = 1.0 - smoothstep(0.75, 1.0, d);
                // Subtle sphere shading: brighten the centre slightly.
                float sphereShade = 0.7 + 0.3 * (1.0 - d * d);
                float alpha = circleMask * (1.0 - d * 0.5) * i.intensity * _MaxOpacity;

                // Smoke: gray base lerped toward burgundy by contamination level.
                // Toxic smoke is burgundy (0.35, 0.05, 0.10), never green.
                float3 col = lerp(_BaseColor.rgb, _ContamColor.rgb, saturate(i.contam)) * sphereShade;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
