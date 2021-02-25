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

    private static int ToIndex(TetInt4 v, int i)
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
    private static byte ToIndex(TetDepth v, int i)
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
        var fW0 = 1.0f - fW1 - fW2;

        var vUV0 = new Vector2(0, 0);
        var vUV1 = new Vector2(1, 0);
        var vUV2 = new Vector2(0, 1);

        var vUVP = (vUV0 * fW0) + (vUV1 * fW1) + (vUV2 * fW2);
        return vUVP;
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
            int iNextProbe = ToIndex(probeGen.vTetAdjIndices[iLastProbe], iMin);
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

            { // 0 -> 1, 2, 3
                ref var v0 = ref vTetVertex._1;
                ref var v1 = ref vTetVertex._2;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);

            }

            { // 1 -> 0, 2, 3
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._2;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);

            }

            { // 2 -> 0, 1, 3
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._1;
                ref var v2 = ref vTetVertex._3;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);

            }

            { // 3 -> 0, 1, 2
                ref var v0 = ref vTetVertex._0;
                ref var v1 = ref vTetVertex._1;
                ref var v2 = ref vTetVertex._2;

                var fWholeAreaRatio = new float();
                var vP = ProjectPointOntoFace(ref v0, ref v1, ref v2, ref vCurPos, ref fWholeAreaRatio);
                var fWholeAreaRatioInv = 1.0f / fWholeAreaRatio;
                var vBary = MakeBaryCoord(ref v0, ref v1, ref v2, ref vCurPos, fWholeAreaRatioInv);

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
