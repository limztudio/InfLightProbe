#ifndef PROBEFINDER_INCLUDED
#define PROBEFINDER_INCLUDED


inline half3 getIrradiance(half4 vNormal){
    half4 vB = vNormal.xyzz * vNormal.yzzx;
    half vC = vNormal.x * vNormal.x - vNormal.y * vNormal.y;

    half3 vReturn;

    vReturn.r = dot(unity_SHAr, vNormal);
    vReturn.g = dot(unity_SHAg, vNormal);
    vReturn.b = dot(unity_SHAb, vNormal);

    vReturn.r += dot(unity_SHBr, vB);
    vReturn.g += dot(unity_SHBg, vB);
    vReturn.b += dot(unity_SHBb, vB);

    vReturn += unity_SHC.rgb * vC;

    vReturn = max(0.h, vReturn);

#ifdef UNITY_COLORSPACE_GAMMA
    vReturn = LinearToGammaSpace(vReturn);
#endif

    return vReturn;
}


#endif //PROBEFINDER_INCLUDED

