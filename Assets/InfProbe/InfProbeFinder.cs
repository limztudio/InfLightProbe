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
        var vCurPos = transform.position;
        var vP = vCurPos - probeGen.vTetBaryMatrices[iLastProbe]._3;

        float[] fWeight = new float[4];
        fWeight[0] = Vector3.Dot(vP, probeGen.vTetBaryMatrices[iLastProbe]._0);
        fWeight[1] = Vector3.Dot(vP, probeGen.vTetBaryMatrices[iLastProbe]._1);
        fWeight[2] = Vector3.Dot(vP, probeGen.vTetBaryMatrices[iLastProbe]._2);
        fWeight[3] = 1.0f - fWeight[0] - fWeight[1] - fWeight[2];

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

        if (fMin < 0.0f)
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
            //shColor = new SHColor();
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
