#ifndef PROBEFINDER_INCLUDED
#define PROBEFINDER_INCLUDED


uniform float4 PRB_N1E0R_N1E1R_N1E2R_N2E0R;
uniform float4 PRB_N1E0G_N1E1G_N1E2G_N2E0G;
uniform float4 PRB_N1E0B_N1E1B_N1E2B_N2E0B;
uniform float4 PRB_N2E1R_N2E2R_N2E3R_N2E4R;
uniform float4 PRB_N2E1G_N2E2G_N2E3G_N2E4G;
uniform float4 PRB_N2E1B_N2E2B_N2E3B_N2E4B;
uniform float3 PRB_N0E0RGB;


inline half3 getIrradiance(half3 vNormal){
    half3 vSH0RGB = PRB_N0E0RGB;
    half4 vSH1R = PRB_N1E0R_N1E1R_N1E2R_N2E0R;
    half4 vSH1G = PRB_N1E0G_N1E1G_N1E2G_N2E0G;
    half4 vSH1B = PRB_N1E0B_N1E1B_N1E2B_N2E0B;
    half4 vSH2R = PRB_N2E1R_N2E2R_N2E3R_N2E4R;
    half4 vSH2G = PRB_N2E1G_N2E2G_N2E3G_N2E4G;
    half4 vSH2B = PRB_N2E1B_N2E2B_N2E3B_N2E4B;

    half4 vBase1;
    half4 vBase2;
    {
        vBase1.x = vNormal.y;
        vBase1.y = vNormal.z;
        vBase1.z = vNormal.x;
        vBase1.w = vNormal.x * vNormal.y;

        vBase2.x = vNormal.z * vNormal.y;
        vBase2.y = vNormal.z * vNormal.z;
        vBase2.z = vNormal.z * vNormal.x;
        vBase2.w = vNormal.x * vNormal.x - vNormal.y * vNormal.y;
    }

    half fBase0Acc = vSH0RGB.x + dot(vSH1R, vBase1) + dot(vSH2R, vBase2);
    half fBase1Acc = vSH0RGB.y + dot(vSH1G, vBase1) + dot(vSH2G, vBase2);
    half fBase2Acc = vSH0RGB.z + dot(vSH1B, vBase1) + dot(vSH2B, vBase2);

    half3 vReturn = max(half3(0.h, 0.h, 0.h), half3(fBase0Acc, fBase1Acc, fBase2Acc));
    vReturn *= 0.31830988h; // 1.h / PI

#ifdef UNITY_COLORSPACE_GAMMA
    vReturn = LinearToGammaSpace(vReturn);
#endif

    return vReturn;
}


#endif //PROBEFINDER_INCLUDED

