using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class InfProbeFinder : MonoBehaviour
{
    public GameObject objProbeGen;
    public InfProbeGen probeGen = null;

    private Vector3 vOldPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);

    public int iLastProbe = 0;
    public SHColor shColor;

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

    private static Vector3 ProjectPointOntoFace(ref Vector3 v0, ref Vector3 v1, ref Vector3 v2, ref Vector3 vP, ref float fNormalLength)
    {
        var vFaceNormal = Vector3.Cross((v1 - v0), (v2 - v0));
        fNormalLength = vFaceNormal.magnitude;

        if(fNormalLength > 0.0001f)
            vFaceNormal /= fNormalLength;
        var fDist = Vector3.Dot(v0, vFaceNormal);

        var vProjected = vP - (vFaceNormal * fDist);
        return vProjected;
    }
    private static Vector2 MakeBaryCoord(ref Vector3 v0, ref Vector3 v1, ref Vector3 v2, ref Vector3 vP, float fWholeAreaRatioInv)
    {
        var v0P = v0 - vP;
        var v1P = v1 - vP;
        var v2P = v2 - vP;

        var fW2 = Vector3.Cross(v0P, v1P).magnitude * fWholeAreaRatioInv;
        var fW1 = Vector3.Cross(v0P, v2P).magnitude * fWholeAreaRatioInv;
        
        // UV0 is (0, 0), so it is okay to be skipped
        var vUV1 = new Vector2(1.0f, 0.0f);
        var vUV2 = new Vector2(0.0f, 1.0f);

        var vUVP = (vUV1 * fW1) + (vUV2 * fW2);
        return vUVP;
    }
    private static float GetFaceDepthOnPoint(ref TetDepth vDepth, ref Vector2 vBary)
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

    private void FindClosestProbe()
    {
        ref var vTetBaryMatrix = ref probeGen.vTetBaryMatrices[iLastProbe];

        var vCurPos = transform.position;

        float[] fWeight = new float[4];
        {
            var vP = vCurPos - vTetBaryMatrix._3;

            fWeight[0] = Vector3.Dot(vP, vTetBaryMatrix._0);
            fWeight[1] = Vector3.Dot(vP, vTetBaryMatrix._1);
            fWeight[2] = Vector3.Dot(vP, vTetBaryMatrix._2);
            fWeight[3] = 1.0f - fWeight[0] - fWeight[1] - fWeight[2];
        }

        int iMin = 0;
        float fMin = fWeight[0];
        for (int i = 1; i < 4; ++i)
        {
            if (fMin > fWeight[i])
            {
                iMin = i;
                fMin = fWeight[i];
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

            { // 0 -> 1, 2, 3
                ref var v0 = ref vTetVertex._1;
                ref var v1 = ref vTetVertex._2;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);
                var fDepth = GetFaceDepthOnPoint(ref vTetDepthMap._0, ref vBary) < 0.9998f ? 0.0f : 1.0f;

            }

            { // 1 -> 0, 2, 3
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._2;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);
                var fDepth = GetFaceDepthOnPoint(ref vTetDepthMap._1, ref vBary) < 0.9998f ? 0.0f : 1.0f;
            }

            { // 2 -> 0, 1, 3
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._1;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);
                var fDepth = GetFaceDepthOnPoint(ref vTetDepthMap._2, ref vBary) < 0.9998f ? 0.0f : 1.0f;
            }

            { // 3 -> 0, 1, 2
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._1;
                ref var v2 = ref vTetVertex._2;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);
                var fDepth = GetFaceDepthOnPoint(ref vTetDepthMap._3, ref vBary) < 0.9998f ? 0.0f : 1.0f;
            }

            shColor = new SHColor();


            //var shTmpColor = new SHColor();

            //if (probeGen.vSHColors.TryGetValue(probeGen.vTetVertices[iLastProbe]._0, out shTmpColor))
            //{
            //    for (int i = 0; i < 9; ++i)
            //        shColor.SH[i] = shTmpColor.SH[i] * fWeight[0];
            //}
            //if (probeGen.vSHColors.TryGetValue(probeGen.vTetVertices[iLastProbe]._1, out shTmpColor))
            //{
            //    for (int i = 0; i < 9; ++i)
            //        shColor.SH[i] += shTmpColor.SH[i] * fWeight[1];
            //}
            //if (probeGen.vSHColors.TryGetValue(probeGen.vTetVertices[iLastProbe]._2, out shTmpColor))
            //{
            //    for (int i = 0; i < 9; ++i)
            //        shColor.SH[i] += shTmpColor.SH[i] * fWeight[2];
            //}
            //if (probeGen.vSHColors.TryGetValue(probeGen.vTetVertices[iLastProbe]._3, out shTmpColor))
            //{
            //    for (int i = 0; i < 9; ++i)
            //        shColor.SH[i] += shTmpColor.SH[i] * fWeight[3];
            //}
        }
    }
    public void InitProbeFinder()
    {
        probeGen = objProbeGen.GetComponent<InfProbeGen>();
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
}
