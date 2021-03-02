#ifndef PROBEFINDER_INCLUDED
#define PROBEFINDER_INCLUDED


uniform float4 PRB_N1E0R_N1E1R_N1E2R_N2E0R;
uniform float4 PRB_N1E0G_N1E1G_N1E2G_N2E0G;
uniform float4 PRB_N1E0B_N1E1B_N1E2B_N2E0B;
uniform float4 PRB_N2E1R_N2E2R_N2E3R_N2E4R;
uniform float4 PRB_N2E1G_N2E2G_N2E3G_N2E4G;
uniform float4 PRB_N2E1B_N2E2B_N2E3B_N2E4B;
uniform float3 PRB_N0E0RGB;


inline half3 getIrradiance(float3 vNormal){
    float3 vSH0RGB = PRB_N0E0RGB;
    float4 vSH1R = PRB_N1E0R_N1E1R_N1E2R_N2E0R;
    float4 vSH1G = PRB_N1E0G_N1E1G_N1E2G_N2E0G;
    float4 vSH1B = PRB_N1E0B_N1E1B_N1E2B_N2E0B;
    float4 vSH2R = PRB_N2E1R_N2E2R_N2E3R_N2E4R;
    float4 vSH2G = PRB_N2E1G_N2E2G_N2E3G_N2E4G;
    float4 vSH2B = PRB_N2E1B_N2E2B_N2E3B_N2E4B;

    float4 vBase1 = float4(
        vNormal.y,
        vNormal.z,
        vNormal.x,
        (vNormal.x * vNormal.y)
        );
    float4 vBase2 = float4(
        (vNormal.y * vNormal.z),
        ((3.f * vNormal.z * vNormal.z) - 1.f),
        (vNormal.x * vNormal.z),
        ((vNormal.x * vNormal.x) - (vNormal.y * vNormal.y))
        );

    float fBase0Acc = max(0.f, (vSH0RGB.x) + dot(vSH1R, vBase1) + dot(vSH2R, vBase2));
    float fBase1Acc = max(0.f, (vSH0RGB.y) + dot(vSH1G, vBase1) + dot(vSH2G, vBase2));
    float fBase2Acc = max(0.f, (vSH0RGB.z) + dot(vSH1B, vBase1) + dot(vSH2B, vBase2));

    float3 vReturn = float3(fBase0Acc, fBase1Acc, fBase2Acc);
    vReturn *= 0.31830988f; // 1.f / PI

#ifdef UNITY_COLORSPACE_GAMMA
    vReturn = LinearToGammaSpace(vReturn);
#endif

    return vReturn;
}


#endif //PROBEFINDER_INCLUDED

