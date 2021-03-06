using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct SHShaderColor
{
    public Vector4 vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R;
    public Vector4 vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G;
    public Vector4 vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B;
    public Vector4 vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R;
    public Vector4 vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G;
    public Vector4 vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B;
    public Vector3 vBand2Base4R_vBand2Base4G_vBand2Base4B;
};


public class InfProbeFinder : MonoBehaviour
{
    public GameObject objProbeGen;
    public InfProbeGen probeGen = null;

    private new Renderer renderer = null;
    private MaterialPropertyBlock propBlock = null;

    private Vector3 vOldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

    public bool bProbeLost = true;
    public int iLastProbe = 0;
    public SHShaderColor shColor;
    public TetInt4 vProbeSelected;
    public TetBool4 vProbeVisibility;

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

    private static Vector3 IntersectPointWithFace(Vector3 vTri0, Vector3 vTri1, Vector3 vTri2, Vector3 vLine0, Vector3 vLine1, out float fDepth, out float fFaceNormalLength)
    {
        var vFaceNormal = Vector3.Cross((vTri1 - vTri0), (vTri2 - vTri0));
        var fFaceNormalLengthSq = vFaceNormal.sqrMagnitude;
        if (fFaceNormalLengthSq > 0.0001f)
        {
            fFaceNormalLength = Mathf.Sqrt(fFaceNormalLengthSq);
            vFaceNormal /= fFaceNormalLength;
        }
        else
            fFaceNormalLength = 0.0f;

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
    private static Vector2 MakeBaryCoord(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 vP, float fFaceNormalLengthInv)
    {
        var v0P = v0 - vP;
        var v1P = v1 - vP;
        var v2P = v2 - vP;

        var vA2 = Vector3.Cross(v0P, v1P);
        var vA1 = Vector3.Cross(v0P, v2P);

        var fW2 = vA2.magnitude;
        var fW1 = vA1.magnitude;

        fW2 *= fFaceNormalLengthInv;
        fW1 *= fFaceNormalLengthInv;

        // UV0 is (0, 0), so it is okay to be skipped
        var vUV1 = new Vector2(1.0f, 0.0f);
        var vUV2 = new Vector2(0.0f, 1.0f);

        var vUVP = (vUV1 * fW1) + (vUV2 * fW2);
        return vUVP;
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

                bProbeLost = true;
            }
            else
            {
                iLastProbe = iNextProbe;
                FindClosestProbe();
            }
        }
        else
        {
            ref var vTetVertexIndices = ref probeGen.vTetIndices[iLastProbe];
            ref var vTetDepthMap = ref probeGen.vTetDepthMap[iLastProbe];

            var vTetVertex0 = probeGen.vTetPositions[vTetVertexIndices._0];
            var vTetVertex1 = probeGen.vTetPositions[vTetVertexIndices._1];
            var vTetVertex2 = probeGen.vTetPositions[vTetVertexIndices._2];
            var vTetVertex3 = probeGen.vTetPositions[vTetVertexIndices._3];

            var fWeightsWithDepth = new float[4];
            var fTotalWeightWithDepth = 0.0f;

            float fClosestDist;
            int iClosestProbe;

            vProbeVisibility = new TetBool4();

            { // 0 -> 1, 2, 3
                float fLineDepth;
                float fFaceNormalLength;
                var vP = IntersectPointWithFace(vTetVertex1, vTetVertex2, vTetVertex3, vTetVertex0, vCurPos, out fLineDepth, out fFaceNormalLength);
                var vBary = MakeBaryCoord(vTetVertex1, vTetVertex2, vTetVertex3, vP, 1.0f / fFaceNormalLength);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._0, vBary);
                var fDist = (vTetVertex0 - vCurPos).magnitude;

                fClosestDist = fDist;
                iClosestProbe = 0;

                if (fLineDepth > fCachedDepth)
                {
                    vProbeVisibility._0 = false;
                    fWeightsWithDepth[0] = 0.0f;
                }
                else
                {
                    vProbeVisibility._0 = true;
                    fWeightsWithDepth[0] = fDist;
                    fTotalWeightWithDepth += fWeightsWithDepth[0];
                }
            }

            { // 1 -> 0, 2, 3
                float fLineDepth;
                float fFaceNormalLength;
                var vP = IntersectPointWithFace(vTetVertex0, vTetVertex2, vTetVertex3, vTetVertex1, vCurPos, out fLineDepth, out fFaceNormalLength);
                var vBary = MakeBaryCoord(vTetVertex0, vTetVertex2, vTetVertex3, vP, 1.0f / fFaceNormalLength);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._1, vBary);
                var fDist = (vTetVertex1 - vCurPos).magnitude;

                if(fClosestDist > fDist)
                {
                    fClosestDist = fDist;
                    iClosestProbe = 1;
                }

                if (fLineDepth > fCachedDepth)
                {
                    vProbeVisibility._1 = false;
                    fWeightsWithDepth[1] = 0.0f;
                }
                else
                {
                    vProbeVisibility._1 = true;
                    fWeightsWithDepth[1] = fDist;
                    fTotalWeightWithDepth += fWeightsWithDepth[1];
                }
            }

            { // 2 -> 0, 1, 3
                float fLineDepth;
                float fFaceNormalLength;
                var vP = IntersectPointWithFace(vTetVertex0, vTetVertex1, vTetVertex3, vTetVertex2, vCurPos, out fLineDepth, out fFaceNormalLength);
                var vBary = MakeBaryCoord(vTetVertex0, vTetVertex1, vTetVertex3, vP, 1.0f / fFaceNormalLength);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._2, vBary);
                var fDist = (vTetVertex2 - vCurPos).magnitude;

                if (fClosestDist > fDist)
                {
                    fClosestDist = fDist;
                    iClosestProbe = 2;
                }

                if (fLineDepth > fCachedDepth)
                {
                    vProbeVisibility._2 = false;
                    fWeightsWithDepth[2] = 0.0f;
                }
                else
                {
                    vProbeVisibility._2 = true;
                    fWeightsWithDepth[2] = fDist;
                    fTotalWeightWithDepth += fWeightsWithDepth[2];
                }
            }

            { // 3 -> 0, 1, 2
                float fLineDepth;
                float fFaceNormalLength;
                var vP = IntersectPointWithFace(vTetVertex0, vTetVertex1, vTetVertex2, vTetVertex3, vCurPos, out fLineDepth, out fFaceNormalLength);
                var vBary = MakeBaryCoord(vTetVertex0, vTetVertex1, vTetVertex2, vP, 1.0f / fFaceNormalLength);
                var fCachedDepth = GetFaceDepthOnPoint(vTetDepthMap._3, vBary);
                var fDist = (vTetVertex3 - vCurPos).magnitude;

                if (fClosestDist > fDist)
                {
                    fClosestDist = fDist;
                    iClosestProbe = 3;
                }

                if (fLineDepth > fCachedDepth)
                {
                    vProbeVisibility._3 = false;
                    fWeightsWithDepth[3] = 0.0f;
                }
                else
                {
                    vProbeVisibility._3 = true;
                    fWeightsWithDepth[3] = fDist;
                    fTotalWeightWithDepth += fWeightsWithDepth[3];
                }
            }

            vProbeSelected = new TetInt4();
            {
                vProbeSelected._0 = probeGen.vTetIndices[iLastProbe]._0;
                vProbeSelected._1 = probeGen.vTetIndices[iLastProbe]._1;
                vProbeSelected._2 = probeGen.vTetIndices[iLastProbe]._2;
                vProbeSelected._3 = probeGen.vTetIndices[iLastProbe]._3;
            }

            if (fTotalWeightWithDepth > 0.0f)
            {
                var shTmpColorAcc = new SHColor();
                shTmpColorAcc.SH = new Vector3[] {
                    Vector3.zero, Vector3.zero, Vector3.zero,
                    Vector3.zero, Vector3.zero, Vector3.zero,
                    Vector3.zero, Vector3.zero, Vector3.zero
                };

                do
                {
                    {
                        fWeightsWithDepth[0] = (fWeightsWithDepth[0] > 0.0001f) ? (fTotalWeightWithDepth / fWeightsWithDepth[0]) : 0.0f;
                        fWeightsWithDepth[1] = (fWeightsWithDepth[1] > 0.0001f) ? (fTotalWeightWithDepth / fWeightsWithDepth[1]) : 0.0f;
                        fWeightsWithDepth[2] = (fWeightsWithDepth[2] > 0.0001f) ? (fTotalWeightWithDepth / fWeightsWithDepth[2]) : 0.0f;
                        fWeightsWithDepth[3] = (fWeightsWithDepth[3] > 0.0001f) ? (fTotalWeightWithDepth / fWeightsWithDepth[3]) : 0.0f;

                        fTotalWeightWithDepth = fWeightsWithDepth[0] + fWeightsWithDepth[1] + fWeightsWithDepth[2] + fWeightsWithDepth[3];
                        fTotalWeightWithDepth = 1.0f / fTotalWeightWithDepth;

                        fWeightsWithDepth[0] *= fTotalWeightWithDepth;
                        fWeightsWithDepth[1] *= fTotalWeightWithDepth;
                        fWeightsWithDepth[2] *= fTotalWeightWithDepth;
                        fWeightsWithDepth[3] *= fTotalWeightWithDepth;
                    }

                    if (fWeightsWithDepth[0] > 0.9998f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._0].SH[i];
                        break;
                    }
                    else if (fWeightsWithDepth[0] > 0.0001f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._0].SH[i] * fWeightsWithDepth[0];
                    }

                    if (fWeightsWithDepth[1] > 0.9998f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._1].SH[i];
                        break;
                    }
                    else if (fWeightsWithDepth[1] > 0.0001f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._1].SH[i] * fWeightsWithDepth[1];
                    }

                    if (fWeightsWithDepth[2] > 0.9998f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._2].SH[i];
                        break;
                    }
                    else if (fWeightsWithDepth[2] > 0.0001f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._2].SH[i] * fWeightsWithDepth[2];
                    }

                    if (fWeightsWithDepth[3] > 0.9998f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._3].SH[i];
                        break;
                    }
                    else if (fWeightsWithDepth[3] > 0.0001f)
                    {
                        for (int i = 0; i < 9; ++i)
                            shTmpColorAcc.SH[i] += probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._3].SH[i] * fWeightsWithDepth[3];
                    }
                } while (false);

                shColor = new SHShaderColor();
                {
                    shColor.vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R = new Vector4(
                        shTmpColorAcc.SH[3].x * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[1].x * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[2].x * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[0].x * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColorAcc.SH[6].x * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G = new Vector4(
                        shTmpColorAcc.SH[3].y * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[1].y * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[2].y * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[0].y * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColorAcc.SH[6].y * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B = new Vector4(
                        shTmpColorAcc.SH[3].z * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[1].z * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[2].z * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[0].z * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColorAcc.SH[6].z * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R = new Vector4(
                        shTmpColorAcc.SH[4].x * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[5].x * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[6].x * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[7].x * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G = new Vector4(
                        shTmpColorAcc.SH[4].y * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[5].y * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[6].y * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[7].y * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B = new Vector4(
                        shTmpColorAcc.SH[4].z * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[5].z * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[6].z * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColorAcc.SH[7].z * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base4R_vBand2Base4G_vBand2Base4B =
                        shTmpColorAcc.SH[8] * ((0.785398163397448309f) * (0.546274215296039590f) * (0.318309886183790672f))
                        ;
                }

                bProbeLost = false;
            }
            else if(bProbeLost)
            {
                shColor = new SHShaderColor();
                {
                    SHColor shTmpColor;
                    switch (iClosestProbe)
                    {
                        case 1:
                            shTmpColor = probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._1];
                            break;
                        case 2:
                            shTmpColor = probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._2];
                            break;
                        case 3:
                            shTmpColor = probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._3];
                            break;
                        default:
                            shTmpColor = probeGen.vSHColors[probeGen.vTetIndices[iLastProbe]._0];
                            break;
                    }

                    shColor.vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R = new Vector4(
                        shTmpColor.SH[3].x * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[1].x * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[2].x * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[0].x * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColor.SH[6].x * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G = new Vector4(
                        shTmpColor.SH[3].y * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[1].y * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[2].y * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[0].y * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColor.SH[6].y * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B = new Vector4(
                        shTmpColor.SH[3].z * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[1].z * ((2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[2].z * ((2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f)),
                        shTmpColor.SH[0].z * ((3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f)) + shTmpColor.SH[6].z * ((0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R = new Vector4(
                        shTmpColor.SH[4].x * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColor.SH[5].x * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColor.SH[6].x * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColor.SH[7].x * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G = new Vector4(
                        shTmpColor.SH[4].y * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColor.SH[5].y * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColor.SH[6].y * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColor.SH[7].y * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B = new Vector4(
                        shTmpColor.SH[4].z * ((0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f)),
                        shTmpColor.SH[5].z * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)),
                        shTmpColor.SH[6].z * ((0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f)),
                        shTmpColor.SH[7].z * ((0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f))
                        );
                    shColor.vBand2Base4R_vBand2Base4G_vBand2Base4B =
                        shTmpColor.SH[8] * ((0.785398163397448309f) * (0.546274215296039590f) * (0.318309886183790672f))
                        ;
                }

                bProbeLost = false;
            }
        }
    }
    public void InitProbeFinder()
    {
        probeGen = objProbeGen.GetComponent<InfProbeGen>();
        renderer = transform.GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        iLastProbe = 0;
        bProbeLost = true;
        vOldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

        UpdateProbe();
        UpdateProbeShader();
    }
    public void UpdateProbe()
    {
        var vCurPos = transform.position;
        if(vOldPos != vCurPos)
        {
            FindClosestProbe();

            vOldPos = vCurPos;
        }
    }
    public void UpdateProbeShader()
    {
        if (renderer != null && propBlock != null)
        {
            renderer.GetPropertyBlock(propBlock);

            propBlock.SetVector("unity_SHAr", shColor.vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R);
            propBlock.SetVector("unity_SHAg", shColor.vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G);
            propBlock.SetVector("unity_SHAb", shColor.vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B);
            propBlock.SetVector("unity_SHBr", shColor.vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R);
            propBlock.SetVector("unity_SHBg", shColor.vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G);
            propBlock.SetVector("unity_SHBb", shColor.vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B);
            propBlock.SetVector("unity_SHC", shColor.vBand2Base4R_vBand2Base4G_vBand2Base4B);

            renderer.SetPropertyBlock(propBlock);
        }
    }
}

