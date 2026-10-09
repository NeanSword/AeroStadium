// Native integer texel addressing; geometry UV scale/shift already applied.
// Inputs are tile-relative. Tile origin must be subtracted before this helper.
// Implements Angrylion clamp -> mirror/mask ordering, not complete RDP filtering.
int NativeN64AddressAxis(int texel, int mode, int maskBits, int clampLastTexel)
{
    if (((mode & 2) != 0) || maskBits == 0)
        texel = clamp(texel, 0, clampLastTexel);
    if (maskBits > 0)
    {
        if ((mode & 1) != 0)
        {
            int mirrorBit = (texel >> min(maskBits, 10)) & 1;
            texel = texel ^ (-mirrorBit);
        }
        texel = texel & ((1 << maskBits) - 1);
    }
    return texel;
}

// clampTexels means inclusive tile extent + 1, not PNG size or mask period.
// For F2 bounds, extent = (high >> 2) - (low >> 2) + 1 in texels.
float2 NativeN64NearestUV(float2 meshUV, int2 mode, int2 maskBits,
                          int2 clampTexels, float2 textureSize,
                          float2 tileOriginTexels)
{
    float2 nativeUV = float2(meshUV.x, 1.0 - meshUV.y);
    int2 baseTexel = int2(floor(nativeUV * textureSize - tileOriginTexels));
    int s = NativeN64AddressAxis(baseTexel.x, mode.x, maskBits.x, clampTexels.x - 1);
    int t = NativeN64AddressAxis(baseTexel.y, mode.y, maskBits.y, clampTexels.y - 1);
    float2 addressedUV = (float2(s,t) + 0.5) / textureSize;
    return float2(addressedUV.x, 1.0 - addressedUV.y);
}
