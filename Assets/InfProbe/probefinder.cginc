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

    float fBase0;
    float4 vBase1;
    float4 vBase2;
    {
        const float z2 = vNormal.z * vNormal.z;

        // l=0
        const float p_0_0 = (0.282094791773878140f);
        fBase0 = p_0_0; // l=0,m=0
        // l=1
        const float p_1_0 = (0.488602511902919920f) * vNormal.z;
        vBase1.y = p_1_0; // l=1,m=0
        // l=2
        const float p_2_0 = (0.946174695757560080f) * z2 + (-0.315391565252520050f);
        vBase2.y = p_2_0; // l=2,m=0


        const float s1 = vNormal.y;
        const float c1 = vNormal.x;

        // l=1
        const float p_1_1 = (-0.488602511902919920f);
        vBase1.x = p_1_1 * s1; // l=1,m=-1
        vBase1.z = p_1_1 * c1; // l=1,m=+1

        // l=2
        const float p_2_1 = (-1.092548430592079200f) * vNormal.z;
        vBase2.x = p_2_1 * s1; // l=2,m=-1
        vBase2.z = p_2_1 * c1; // l=2,m=+1


        const float s2 = vNormal.x * s1 + vNormal.y * c1;
        const float c2 = vNormal.x * c1 - vNormal.y * s1;

        // l=2
        const float p_2_2 = (0.546274215296039590f);
        vBase1.w = p_2_2 * s2; // l=2,m=-2
        vBase2.w = p_2_2 * c2; // l=2,m=+2
    }

    float fBase0Acc = max(0.f, (vSH0RGB.x * fBase0) + dot(vSH1R, vBase1) + dot(vSH2R, vBase2));
    float fBase1Acc = max(0.f, (vSH0RGB.y * fBase0) + dot(vSH1G, vBase1) + dot(vSH2G, vBase2));
    float fBase2Acc = max(0.f, (vSH0RGB.z * fBase0) + dot(vSH1B, vBase1) + dot(vSH2B, vBase2));

    float3 vReturn = float3(fBase0Acc, fBase1Acc, fBase2Acc);
    vReturn *= 0.31830988f; // 1.f / PI

#ifdef UNITY_COLORSPACE_GAMMA
    vReturn = LinearToGammaSpace(vReturn);
#endif

    return vReturn;
}


#endif //PROBEFINDER_INCLUDED

