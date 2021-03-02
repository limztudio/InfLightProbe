using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct SHShaderColor
{
    public Vector4 vBand1Base0R_Band1Base1R_Band1Base2R_Band2Base0R;
    public Vector4 vBand1Base0G_Band1Base1G_Band1Base2G_Band2Base0G;
    public Vector4 vBand1Base0B_Band1Base1B_Band1Base2B_Band2Base0B;
    public Vector4 vBand2Base1R_Band2Base2R_Band2Base3R_Band2Base4R;
    public Vector4 vBand2Base1G_Band2Base2G_Band2Base3G_Band2Base4G;
    public Vector4 vBand2Base1B_Band2Base2B_Band2Base3B_Band2Base4B;
    public Vector3 vBand0Base0RGB;
};


public class InfProbeFinder : MonoBehaviour
{
    public GameObject objProbeGen;
    public InfProbeGen probeGen = null;

    private Vector3 vOldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

    public int iLastProbe = 0;
    public SHShaderColor shColor;

    private static int ToIndex(ref TetInt4 v, int i)
    {
        switch (i)
        {
            case 0:
                return v._0;
            case 1:
                return v._1;
            case 2:
                return v._2;
            case 3:
                return v._3;
        }
        return v._3;
    }
    private static byte ToIndex(ref TetDepth v, int i)
    {
        switch (i)
        {
            case 0:
                return v._00;
            case 1:
                return v._01;
            case 2:
                return v._02;
            case 3:
                return v._03;
            case 4:
                return v._04;
            case 5:
                return v._05;
            case 6:
                return v._06;
            case 7:
                return v._07;
            case 8:
                return v._08;
            case 9:
                return v._09;
            case 10:
                return v._10;
            case 11:
                return v._11;
            case 12:
                return v._12;
            case 13:
                return v._13;
            case 14:
                return v._14;
        }
        return v._14;
    }

    private static Vector3 IntersectPointWithFace(Vector3 vTri0, Vector3 vTri1, Vector3 vTri2, Vector3 vLine0, Vector3 vLine1, out float fDepth)
    {
        var vFaceNormal = Vector3.Cross((vTri1 - vTri0), (vTri2 - vTri0));
        var fFaceNormalLengthSq = vFaceNormal.sqrMagnitude;
        if (fFaceNormalLengthSq > 0.0001f)
        {
            var fFaceNormalLength = Mathf.Sqrt(fFaceNormalLengthSq);
            vFaceNormal /= fFaceNormalLength;
        }

        var vLineNormal = vLine1 - vLine0;
        var fLineNormalLengthSq = vLineNormal.sqrMagnitude;
        var fLineNormalLength = 0.0f;
        if (fLineNormalLengthSq > 0.0001f)
        {
            fLineNormalLength = Mathf.Sqrt(fLineNormalLengthSq);
            vLineNormal /= fLineNormalLength;
        }

        var fP = Vector3.Dot(vFaceNormal, vTri0) - Vector3.Dot(vFaceNormal, vLine1);
        var fQ = Vector3.Dot(vFaceNormal, vLineNormal);
        var fT = fP / fQ;

        var vIntersected = vLine1 + (vLineNormal * fT);
        fDepth = fLineNormalLength / (fLineNormalLength + fT);

        return vIntersected;
    }
    private static Vector2 MakeBaryCoord(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 vP)
    {
        var v0P = v0 - vP;
        var v1P = v1 - vP;
        var v2P = v2 - vP;

        var vA2 = Vector3.Cross(v0P, v1P);
        var vA1 = Vector3.Cross(v0P, v2P);
        var vA0 = Vector3.Cross(v1P, v2P);

        var fW2 = vA2.magnitude;
        var fW1 = vA1.magnitude;
        var fW0 = vA0.magnitude;

        var fWT = fW0 + fW1 + fW2;
        fWT = 1.0f / fWT;

        fW2 *= fWT;
        fW1 *= fWT;

        // UV0 is (0, 0), so it is okay to be skipped
        var vUV1 = new Vector2(1.0f, 0.0f);
        var vUV2 = new Vector2(0.0f, 1.0f);

        var vUVP = (vUV1 * fW1) + (vUV2 * fW2);
        return vUVP;


        // follwing code is pre-calculate total area ratio of triangle. so it doesn't need to calculate additional Cross, Dot and Sqrt computation.
        // so it supposed to working correctly, but seems it's not.
        // still looking for where is the error come from.

        //var v0P = v0 - vP;
        //var v1P = v1 - vP;
        //var v2P = v2 - vP;

        //var vA2 = Vector3.Cross(v0P, v1P);
        //var vA1 = Vector3.Cross(v0P, v2P);

        //var fW2 = vA2.magnitude;
        //var fW1 = vA1.magnitude;

        //fW2 *= fWholeAreaRatioInv;
        //fW1 *= fWholeAreaRatioInv;

        //// UV0 is (0, 0), so it is okay to be skipped
        //var vUV1 = new Vector2(1.0f, 0.0f);
        //var vUV2 = new Vector2(0.0f, 1.0f);

        //var vUVP = (vUV1 * fW1) + (vUV2 * fW2);
        //return vUVP;
    }
    private static float GetFaceDepthOnPoint(TetDepth vDepth, Vector2 vBary)
    {
        var fSampleY = Mathf.Floor(vBary.y * 4.0f);
        var fSampleX = Mathf.Floor(vBary.x * 4.0f);

        var iBaseIndex = (int)(((11.0f - fSampleY) * fSampleY) * 0.5f + fSampleX + 0.5f);
        var iLineLength = 5 - (int)fSampleY;

        var vBaseBary = new Vector2(fSampleX * 0.25f, fSampleY * 0.25f);

        var iIndices = new int[] { iBaseIndex + 1, iBaseIndex + iLineLength, iBaseIndex };
        var fSampleBaries = new float[3];
        fSampleBaries[0] = (vBary.x - vBaseBary.x) * 4.0f;
        fSampleBaries[1] = (vBary.y - vBaseBary.y) * 4.0f;

        if ((fSampleBaries[0] + fSampleBaries[1]) > 1.0f)
        { // above the diagonal
            iIndices[0] = iBaseIndex + iLineLength;
            iIndices[1] = iBaseIndex + 1;
            iIndices[2] = iBaseIndex + iLineLength + 1;

            var fTmpSampleBaryX = -(fSampleBaries[1] - 1.0f);
            var fTmpSampleBaryY = -(fSampleBaries[0] - 1.0f);

            fSampleBaries[0] = fTmpSampleBaryX;
            fSampleBaries[1] = fTmpSampleBaryY;
        }

        fSampleBaries[2] = 1.0f - fSampleBaries[0] - fSampleBaries[1];

        var fResult = 0.0f;
        for (int i = 0; i < 3; ++i)
        {
            var iIndex = iIndices[i] % 15;
            var fDepth = ToIndex(ref vDepth, iIndex) / 255.0f;

            fDepth *= fSampleBaries[i];
            fResult += fDepth;
        }

        return fResult;
    }

    private void Awake()
    {
        InitProbeFinder();
    }

    private void Start()
    {
        InitProbeFinder();
    }

    private void Update()
    {
        UpdateProbe();
    }

    private void OnPreRender()
    {
        UpdateProbeShader();
    }

    private void FindClosestProbe()
    {
        ref var vTetBaryMatrix = ref probeGen.vTetBaryMatrices[iLastProbe];

        var vCurPos = transform.position;

        float[] fTetWeights = new float[4];
        {
            var vP = vCurPos - vTetBaryMatrix._3;

            fTetWeights[0] = Vector3.Dot(vP, vTetBaryMatrix._0);
            fTetWeights[1] = Vector3.Dot(vP, vTetBaryMatrix._1);
            fTetWeights[2] = Vector3.Dot(vP, vTetBaryMatrix._2);
            fTetWeights[3] = 1.0f - fTetWeights[0] - fTetWeights[1] - fTetWeights[2];
        }

        int iMin = 0;
        float fMin = fTetWeights[0];
        for (int i = 1; i < 4; ++i)
        {
            if (fMin > fTetWeights[i])
            {
                iMin = i;
                fMin = fTetWeights[i];
            }
        }

        if (fMin < -0.0001f)
        {
            int iNextProbe = ToIndex(ref probeGen.vTetAdjIndices[iLastProbe], iMin);
            if (iNextProbe == -1)
            { // do extrapolation
                // seems on Unity:
                // find closest face from tetrahedron
                // straight down a line from object position which is perpendicular with the face
                // find intersection point with the line and the face
                // use barycentric coordinate of the point on the face
                // depending on the weight of 3, calculate SH colours
            }
            else
            {
                iLastProbe = iNextProbe;
                FindClosestProbe();
            }
        }
        else
        {
            ref var vTetVertex = ref probeGen.vTetVertices[iLastProbe];
            ref var vTetDepthMap = ref probeGen.vTetDepthMap[iLastProbe];

            var fWeights = new float[4];
            var fWeightsWithDepth = new float[4];

            var fTotalWeight = 0.0f;
            var fTotalWeightWithDepth = 0.0f;

            { // 0 -> 1, 2, 3
                float fLineDepth;
                var vP = IntersectPointWithFace(vTetVertex._1, vTetVertex._2, vTetVertex._3, vTetVertex._0, vCurPos, out fLineDepth);
                var vBary = MakeBaryCoord(vTetVertex._1, vTetVertex._2, vTetVertex._3, vCurPos);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._0, vBary);
                var fVisible = (fLineDepth > fCachedDepth) ? 0.0f : 1.0f;

                fWeights[0] = (vTetVertex._0 - vP).magnitude;
                fWeightsWithDepth[0] = fWeights[0] * fVisible;

                fTotalWeight += fWeights[0];
                fTotalWeightWithDepth += fWeightsWithDepth[0];
            }

            { // 1 -> 0, 2, 3
                float fLineDepth;
                var vP = IntersectPointWithFace(vTetVertex._0, vTetVertex._2, vTetVertex._3, vTetVertex._1, vCurPos, out fLineDepth);
                var vBary = MakeBaryCoord(vTetVertex._0, vTetVertex._2, vTetVertex._3, vCurPos);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._1, vBary);
                var fVisible = (fLineDepth > fCachedDepth) ? 0.0f : 1.0f;

                fWeights[1] = (vTetVertex._1 - vP).magnitude;
                fWeightsWithDepth[1] = fWeights[1] * fVisible;

                fTotalWeight += fWeights[1];
                fTotalWeightWithDepth += fWeightsWithDepth[1];
            }

            { // 2 -> 0, 1, 3
                float fLineDepth;
                var vP = IntersectPointWithFace(vTetVertex._0, vTetVertex._1, vTetVertex._3, vTetVertex._2, vCurPos, out fLineDepth);
                var vBary = MakeBaryCoord(vTetVertex._0, vTetVertex._1, vTetVertex._3, vCurPos);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._2, vBary);
                var fVisible = (fLineDepth > fCachedDepth) ? 0.0f : 1.0f;

                fWeights[2] = (vTetVertex._2 - vP).magnitude;
                fWeightsWithDepth[2] = fWeights[2] * fVisible;

                fTotalWeight += fWeights[2];
                fTotalWeightWithDepth += fWeightsWithDepth[2];
            }

            { // 3 -> 0, 1, 2
                float fLineDepth;
                var vP = IntersectPointWithFace(vTetVertex._0, vTetVertex._1, vTetVertex._2, vTetVertex._3, vCurPos, out fLineDepth);
                var vBary = MakeBaryCoord(vTetVertex._0, vTetVertex._1, vTetVertex._2, vCurPos);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._3, vBary);
                var fVisible = (fLineDepth > fCachedDepth) ? 0.0f : 1.0f;

                fWeights[3] = (vTetVertex._3 - vP).magnitude;
                fWeightsWithDepth[3] = fWeights[3] * fVisible;

                fTotalWeight += fWeights[3];
                fTotalWeightWithDepth += fWeightsWithDepth[3];
            }

            var shTmpColorAcc = new SHColor();
            shTmpColorAcc.SH = new Vector3[] {
                Vector3.zero, Vector3.zero, Vector3.zero,
                Vector3.zero, Vector3.zero, Vector3.zero,
                Vector3.zero, Vector3.zero, Vector3.zero
            };

            float fSHWeight;

            if (fTotalWeightWithDepth > 0.0f)
            {
                fTotalWeightWithDepth = 1.0f / fTotalWeightWithDepth;

                fSHWeight = 1.0f - (fWeightsWithDepth[0] * fTotalWeightWithDepth);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._0];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeightsWithDepth[1] * fTotalWeightWithDepth);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._1];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeightsWithDepth[2] * fTotalWeightWithDepth);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._2];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeightsWithDepth[3] * fTotalWeightWithDepth);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._3];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }
            }
            else
            {
                fTotalWeight = 1.0f / fTotalWeight;

                fSHWeight = 1.0f - (fWeights[0] * fTotalWeight);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._0];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeights[1] * fTotalWeight);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._1];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeights[2] * fTotalWeight);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._2];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }

                fSHWeight = 1.0f - (fWeights[3] * fTotalWeight);
                if (fSHWeight > 0.0001f)
                {
                    ref var shTmpColor = ref probeGen.vSHColors[probeGen.vTetSHIndices[iLastProbe]._3];
                    for (int i = 0; i < 9; ++i)
                    {
                        shTmpColor.SH[i] = shTmpColor.SH[i] * fSHWeight;
                        shTmpColorAcc.SH[i] += shTmpColor.SH[i];
                    }
                }
            }

            shColor = new SHShaderColor();
            {
                shColor.vBand0Base0RGB = shTmpColorAcc.SH[0];
                shColor.vBand1Base0R_Band1Base1R_Band1Base2R_Band2Base0R = new Vector4(
                    shTmpColorAcc.SH[1].x,
                    shTmpColorAcc.SH[2].x,
                    shTmpColorAcc.SH[3].x,
                    shTmpColorAcc.SH[4].x
                    );
                shColor.vBand1Base0G_Band1Base1G_Band1Base2G_Band2Base0G = new Vector4(
                    shTmpColorAcc.SH[1].y,
                    shTmpColorAcc.SH[2].y,
                    shTmpColorAcc.SH[3].y,
                    shTmpColorAcc.SH[4].y
                    );
                shColor.vBand1Base0B_Band1Base1B_Band1Base2B_Band2Base0B = new Vector4(
                    shTmpColorAcc.SH[1].z,
                    shTmpColorAcc.SH[2].z,
                    shTmpColorAcc.SH[3].z,
                    shTmpColorAcc.SH[4].z
                    );
                shColor.vBand2Base1R_Band2Base2R_Band2Base3R_Band2Base4R = new Vector4(
                    shTmpColorAcc.SH[5].x,
                    shTmpColorAcc.SH[6].x,
                    shTmpColorAcc.SH[7].x,
                    shTmpColorAcc.SH[8].x
                    );
                shColor.vBand2Base1G_Band2Base2G_Band2Base3G_Band2Base4G = new Vector4(
                    shTmpColorAcc.SH[5].y,
                    shTmpColorAcc.SH[6].y,
                    shTmpColorAcc.SH[7].y,
                    shTmpColorAcc.SH[8].y
                    );
                shColor.vBand2Base1B_Band2Base2B_Band2Base3B_Band2Base4B = new Vector4(
                    shTmpColorAcc.SH[5].z,
                    shTmpColorAcc.SH[6].z,
                    shTmpColorAcc.SH[7].z,
                    shTmpColorAcc.SH[8].z
                    );
            }
        }
    }
    public void InitProbeFinder()
    {
        probeGen = objProbeGen.GetComponent<InfProbeGen>();
        iLastProbe = 0;
        vOldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        UpdateProbe();
    }
    public void UpdateProbe()
    {
        var vCurPos = transform.position;
        if(vOldPos != vCurPos)
        {
            FindClosestProbe();

            vOldPos = vCurPos;
        }

        { // test
            UpdateProbeShader();
        }
    }
    public void UpdateProbeShader()
    {
        Shader.SetGlobalVector("PRB_N1E0R_N1E1R_N1E2R_N2E0R", shColor.vBand1Base0R_Band1Base1R_Band1Base2R_Band2Base0R);
        Shader.SetGlobalVector("PRB_N1E0G_N1E1G_N1E2G_N2E0G", shColor.vBand1Base0G_Band1Base1G_Band1Base2G_Band2Base0G);
        Shader.SetGlobalVector("PRB_N1E0B_N1E1B_N1E2B_N2E0B", shColor.vBand1Base0B_Band1Base1B_Band1Base2B_Band2Base0B);
        Shader.SetGlobalVector("PRB_N2E1R_N2E2R_N2E3R_N2E4R", shColor.vBand2Base1R_Band2Base2R_Band2Base3R_Band2Base4R);
        Shader.SetGlobalVector("PRB_N2E1G_N2E2G_N2E3G_N2E4G", shColor.vBand2Base1G_Band2Base2G_Band2Base3G_Band2Base4G);
        Shader.SetGlobalVector("PRB_N2E1B_N2E2B_N2E3B_N2E4B", shColor.vBand2Base1B_Band2Base2B_Band2Base3B_Band2Base4B);
        Shader.SetGlobalVector("PRB_N0E0RGB", shColor.vBand0Base0RGB);

        
    }
}
