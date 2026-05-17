// GPU-driven flame shader — bent, rippling pyramids with masking noise.
//
// Geometry: 9 verts per face (bottom quad + top triangle), 4 faces = 36 verts per tongue.
// The flame bends progressively from base (anchored) through a mid-ring to the tip,
// so it leans and ripples rather than rocking rigidly like a traffic cone.
// Masking noise in the fragment shader breaks up the silhouette into irregular tongues.
//
// instanceID = cellIndex * 5 + tongueSlot.
// Intensity read from _SmoothedFields.x (temporally smoothed, no popping).
// Blend: SrcAlpha One (additive).
// Requires #pragma target 4.5 for StructuredBuffer in vertex stage.
Shader "Wildfire/Flame"
{
    Properties
    {
        _MaxTongueHeight ("Max Tongue Height", Float) = 2.2
        _BaseWidth       ("Base Width",        Float) = 0.42
    }
    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent+1"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }
        Blend SrcAlpha One
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

            StructuredBuffer<float4> _SmoothedFields;
            StructuredBuffer<float3> _CellWorldPositions;

            float _MaxTongueHeight;
            float _BaseWidth;

            // Within-cell base offsets (XZ, in BaseWidth units) and per-tongue scale.
            static const float2 TongueBaseOffsets[5] =
            {
                float2( 0.00,  0.00),
                float2( 0.28,  0.18),
                float2(-0.28,  0.18),
                float2( 0.18, -0.22),
                float2(-0.18, -0.22),
            };
            static const float TongueHeightScale[5] = { 1.0, 0.82, 0.88, 0.78, 0.84 };
            static const float TongueWidthScale[5]  = { 1.0, 0.75, 0.80, 0.70, 0.72 };

            // Pyramid face base corners (XZ sign ±1), two per face.
            static const float2 FaceVerts[8] =
            {
                float2(-1,-1), float2(+1,-1),  // face 0: front  (-Z)
                float2(+1,-1), float2(+1,+1),  // face 1: right  (+X)
                float2(+1,+1), float2(-1,+1),  // face 2: back   (+Z)
                float2(-1,+1), float2(-1,-1),  // face 3: left   (-X)
            };

            // 9 verts per face: 3 tris.
            // Position key: 0=baseA 1=baseB 2=midA 3=midB 4=tip
            static const uint VertToPos[9] = { 0,1,2,  2,1,3,  2,3,4 };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float2 faceUV   : TEXCOORD0;  // face-local UV for noise (x: 0-1 across, y: 0-1 height)
                float  tHeight  : TEXCOORD1;  // normalised height 0=base 1=tip
                float  fire     : TEXCOORD2;
                float  tongueId : TEXCOORD3;  // per-tongue hash seed for noise variation
            };

            // Wang hash: maps uint → [0,1).
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
                uint cellIndex = instanceID / 5u;
                uint slot      = instanceID % 5u;
                float fire = _SmoothedFields[cellIndex].x;

                uint activeTongues = min(5u, max(1u, (uint)ceil(fire * 5.0)));
                if (fire < 0.015 || slot >= activeTongues)
                {
                    o.pos      = float4(2.0, 2.0, 2.0, 1.0);
                    o.faceUV   = 0;
                    o.tHeight  = 0.0;
                    o.fire     = 0.0;
                    o.tongueId = 0.0;
                    return o;
                }

                // Per-tongue geometry parameters
                float hw     = _BaseWidth * TongueWidthScale[slot] * 0.5;
                float height = fire * _MaxTongueHeight * TongueHeightScale[slot];
                float midFrac = 0.52;  // mid-ring at ~52% of height

                // Per-tongue hashes for independent, non-synchronised movement
                float h1 = Hash(cellIndex * 31u + slot * 7u);
                float h2 = Hash(cellIndex * 31u + slot * 7u + 1u);
                float h3 = Hash(cellIndex * 31u + slot * 7u + 2u);
                float h4 = Hash(cellIndex * 31u + slot * 7u + 3u);
                float h5 = Hash(cellIndex * 31u + slot * 7u + 4u);

                // Unique per-tongue frequency (1.5–6.5 Hz). Incommensurate multipliers
                // (golden ratio, ln2, √(4/3)) prevent any two tongues ever syncing.
                float freq      = 1.5 + h1 * 5.0;
                float wobbleMag = fire * hw * 0.85;

                // Full tip displacement — multiple harmonics, chaotic
                float wx_full = (sin(_Time.y * freq          + h2 * 6.283) * 0.50
                               + sin(_Time.y * freq * 1.6180  + h3 * 6.283) * 0.32
                               + sin(_Time.y * freq * 0.3820  + h4 * 6.283) * 0.18) * wobbleMag;
                float wz_full = (cos(_Time.y * freq * 1.1547  + h2 * 6.283) * 0.50
                               + cos(_Time.y * freq * 0.6931  + h3 * 6.283) * 0.32
                               + cos(_Time.y * freq * 1.9021  + h4 * 6.283) * 0.18) * wobbleMag;

                // Mid-ring displacement: same direction but scaled by height fraction → progressive bend.
                // A secondary fast ripple is added to give the mid-ring its own motion.
                float rippleFreq = freq * 2.3 + h5 * 3.0;
                float wx_mid = wx_full * midFrac
                    + sin(_Time.y * rippleFreq + h5 * 6.283) * wobbleMag * 0.25;
                float wz_mid = wz_full * midFrac
                    + cos(_Time.y * rippleFreq * 0.8 + h5 * 6.283) * wobbleMag * 0.25;

                float3 base = _CellWorldPositions[cellIndex];
                float2 ofs  = TongueBaseOffsets[slot] * _BaseWidth;
                base.x += ofs.x;
                base.z += ofs.y;

                float midWidth = hw * 0.65;  // mid-ring is narrower than base

                // Decode face and position from vertexID
                uint faceIdx    = vertexID / 9u;
                uint vertInFace = vertexID % 9u;
                uint posKey     = VertToPos[vertInFace];

                // 0=baseA 1=baseB 2=midA 3=midB 4=tip
                float2 fv0 = FaceVerts[faceIdx * 2u + 0u];
                float2 fv1 = FaceVerts[faceIdx * 2u + 1u];

                float3 vPos;
                float  tHeight;
                float2 faceUV;

                if (posKey == 0u)
                {
                    vPos    = base + float3(fv0.x * hw,      0.0,          fv0.y * hw);
                    tHeight = 0.0;
                    faceUV  = float2(0.0, 0.0);
                }
                else if (posKey == 1u)
                {
                    vPos    = base + float3(fv1.x * hw,      0.0,          fv1.y * hw);
                    tHeight = 0.0;
                    faceUV  = float2(1.0, 0.0);
                }
                else if (posKey == 2u)
                {
                    vPos    = base + float3(fv0.x * midWidth + wx_mid, height * midFrac, fv0.y * midWidth + wz_mid);
                    tHeight = midFrac;
                    faceUV  = float2(0.0, midFrac);
                }
                else if (posKey == 3u)
                {
                    vPos    = base + float3(fv1.x * midWidth + wx_mid, height * midFrac, fv1.y * midWidth + wz_mid);
                    tHeight = midFrac;
                    faceUV  = float2(1.0, midFrac);
                }
                else  // posKey == 4 → tip
                {
                    vPos    = base + float3(wx_full, height, wz_full);
                    tHeight = 1.0;
                    faceUV  = float2(0.5, 1.0);
                }

                o.pos      = UnityWorldToClipPos(vPos);
                o.faceUV   = faceUV;
                o.tHeight  = tHeight;
                o.fire     = fire;
                o.tongueId = h1 * 100.0;  // float seed for noise variation in fragment
                return o;
            }

            // Wave-based masking noise: product of three cosines at incommensurate
            // spatial frequencies and time phases creates natural-looking turbulence.
            float FlameNoise(float2 uv, float seed, float t)
            {
                float w1 = cos(uv.x * 7.3  + uv.y * 3.1  + seed * 11.3 + t * 1.3) * 0.5 + 0.5;
                float w2 = cos(uv.x * 4.7  - uv.y * 8.1  + seed * 23.7 + t * 0.8) * 0.5 + 0.5;
                float w3 = cos(uv.x * 2.1  + uv.y * 12.7 + seed *  5.9 - t * 0.6) * 0.5 + 0.5;
                return w1 * w2 * w3;  // product: sparse bright patches, mostly dark
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = i.tHeight;

                // Masking noise — sampled at face-local UV, animated, unique per tongue.
                // Drives both silhouette irregularity (edge fade) and interior bright patches.
                float noise  = FlameNoise(i.faceUV, i.tongueId, _Time.y);
                // Boost noise toward bright patches; threshold removes the darkest areas
                float mask   = smoothstep(0.05, 0.6, noise);

                // Secondary high-frequency noise for fine-grained edge turbulence
                float noise2 = FlameNoise(i.faceUV * float2(2.1, 1.4) + 0.3, i.tongueId + 5.0, _Time.y * 1.7);
                float edgeMask = lerp(mask, saturate(mask + noise2 * 0.5), saturate(t * 2.0));

                // Color: yellow-white at base → orange mid → dark red at tip
                float3 col = lerp(
                    lerp(float3(1.0, 0.92, 0.18), float3(1.0, 0.32, 0.0), saturate(t * 1.5)),
                    float3(0.40, 0.02, 0.0),
                    saturate((t - 0.40) * 1.7)
                );
                // Noise brightens core patches (hot spots)
                col += float3(0.8, 0.4, 0.0) * saturate(noise2 - 0.5) * (1.0 - t);

                // Alpha: height fade × fire intensity × masking noise
                float alpha = saturate(1.0 - t * 0.78) * i.fire * edgeMask;
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
}
