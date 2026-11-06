// Shared Dongba glyph scatter for the water shaders. Drop-in usage:
//   float g = SampleGlyph(scatterUV, _GlyphCoverage, _GlyphScale, _GlyphSeed);
// scatterUV is a grid space in roughly meter units, already offset for drift.
// Each cell randomly decides whether it holds a glyph and where/how rotated it sits,
// so the pattern never reads as a neat grid. Only whole glyphs are sampled — the
// 水 character is never cut apart, cells either show it fully or not at all.
#ifndef DIORAMA_GLYPH_SCATTER_INCLUDED
#define DIORAMA_GLYPH_SCATTER_INCLUDED

TEXTURE2D(_GlyphTex);
// Inline sampler state: linear filtering, clamped at the glyph quad border
SamplerState sampler_linear_clamp;

float GlyphHash(float2 p, float salt)
{
    p = frac(p * float2(234.34, 435.345));
    p += dot(p, p + 34.23 + salt);
    return frac(p.x * p.y);
}

float SampleGlyph(float2 scatterUV, float coverage, float scale, float seed)
{
    float2 cell = floor(scatterUV);
    float2 f = frac(scatterUV);

    // Most cells stay empty so the characters feel scattered, not tiled
    if (GlyphHash(cell, seed) > coverage)
    {
        return 0.0;
    }

    // Random center kept away from cell edges, small rotation so the character
    // stays readable, and mild size variation around the material's scale
    float2 center = float2(GlyphHash(cell, seed + 1.7), GlyphHash(cell, seed + 3.1)) * 0.4 + 0.3;
    float angle = (GlyphHash(cell, seed + 4.3) - 0.5) * 1.0;
    float s = scale * (0.7 + 0.6 * GlyphHash(cell, seed + 5.9));

    float2 d = (f - center) / max(s, 1e-3);
    float cs = cos(angle);
    float sn = sin(angle);
    float2 g = float2(d.x * cs - d.y * sn, d.x * sn + d.y * cs) + 0.5;

    if (any(g < 0.0) || any(g > 1.0))
    {
        return 0.0;
    }

    return _GlyphTex.Sample(sampler_linear_clamp, g).a;
}

#endif
