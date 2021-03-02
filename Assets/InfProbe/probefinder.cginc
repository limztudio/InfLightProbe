#ifndef PROBEFINDER_INCLUDED
#define PROBEFINDER_INCLUDED


uniform float4 PRB_BAND1BASE0RGB_BAND2BASE0R;
uniform float4 PRB_BAND1BASE1RGB_BAND2BASE0G;
uniform float4 PRB_BAND1BASE2RGB_BAND2BASE0B;
uniform float4 PRB_BAND2BASE1RGB_BAND2BASE4R;
uniform float4 PRB_BAND2BASE2RGB_BAND2BASE4G;
uniform float4 PRB_BAND2BASE3RGB_BAND2BASE4B;
uniform float3 PRB_BAND0BASE0RGB;


inline half3 getIrradiance(float3 vNormal){
    float3 vSH0 = PRB_BAND0BASE0RGB;
    float3 vSH1 = PRB_BAND1BASE0RGB_BAND2BASE0R.xyz;
    float3 vSH2 = PRB_BAND1BASE1RGB_BAND2BASE0G.xyz;
    float3 vSH3 = PRB_BAND1BASE2RGB_BAND2BASE0B.xyz;
    float3 vSH4 = float3(PRB_BAND1BASE0RGB_BAND2BASE0R.w, PRB_BAND1BASE1RGB_BAND2BASE0G.w, PRB_BAND1BASE2RGB_BAND2BASE0B.w);
    float3 vSH5 = PRB_BAND2BASE1RGB_BAND2BASE4R.xyz;
    float3 vSH6 = PRB_BAND2BASE2RGB_BAND2BASE4G.xyz;
    float3 vSH7 = PRB_BAND2BASE3RGB_BAND2BASE4B.xyz;
    float3 vSH8 = float3(PRB_BAND2BASE1RGB_BAND2BASE4R.w, PRB_BAND2BASE2RGB_BAND2BASE4G.w, PRB_BAND2BASE3RGB_BAND2BASE4B.w);

    float3 vBase0 = float3(
        0.282095f,
        0.488603f * vNormal.y,
        0.488603f * vNormal.z
        );
    float3 vBase1 = float3(
        0.488603f * vNormal.x,
        1.092548f * (vNormal.x * vNormal.y),
        1.092548f * (vNormal.y * vNormal.z)
        );
    float3 vBase2 = float3(
        0.315392f * ((3.f * vNormal.z * vNormal.z) - 1.f),
        1.092548f * (vNormal.x * vNormal.z),
        0.546274f * ((vNormal.x * vNormal.x) - (vNormal.y * vNormal.y))
        );

    float fBase0Acc = max(0.f, dot(vBase0, vSH0) + dot(vBase0, vSH3) + dot(vBase0, vSH6));
    float fBase1Acc = max(0.f, dot(vBase1, vSH1) + dot(vBase1, vSH4) + dot(vBase1, vSH7));
    float fBase2Acc = max(0.f, dot(vBase2, vSH2) + dot(vBase2, vSH5) + dot(vBase2, vSH8));

    float3 vReturn = float3(fBase0Acc, fBase1Acc, fBase2Acc);
    vReturn *= 0.31830988f; // 1.f / PI

    vReturn = pow(vReturn / (float3(1.f, 1.f, 1.f) + vReturn), float3(1.f / 1.8f, 1.f / 1.8f, 1.f / 1.8f));

    return vReturn;
}


#endif //PROBEFINDER_INCLUDED

