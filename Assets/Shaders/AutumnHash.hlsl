#ifndef AUTUMN_HASH_INCLUDED
#define AUTUMN_HASH_INCLUDED

void AutumnHash_float(float3 ObjectWorldPosition, float SeedScale, float PaletteOffset, out float Hash)
{
    float2 p = ObjectWorldPosition.xz * max(SeedScale, 0.0001);
    float n = sin(dot(p, float2(12.9898, 78.233))) * 43758.5453;
    Hash = frac(n + PaletteOffset);
}

#endif