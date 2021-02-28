using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public struct SHColor
{
    public Vector3[] SH;
};
[System.Serializable]
public struct TetFloat43
{
    public Vector3 _0, _1, _2, _3;
};
[System.Serializable]
public struct TetInt4
{
    public int _0, _1, _2, _3;
};
[System.Serializable]
public struct TetDepth
{
    public byte _00, _01, _02, _03, _04;
    public byte _05, _06, _07, _08;
    public byte _09, _10, _11;
    public byte _12, _13;
    public byte _14;
};
[System.Serializable]
public struct TetDepthMap
{
    public TetDepth _0, _1, _2, _3;
};


public class InfProbeGen : MonoBehaviour
{
    public ComputeShader shdSHIntegrator;
    public ComputeShader shdSHReductor1;
    public ComputeShader shdSHReductor2;
    public Vector3 vAABBExtents = new Vector3(50, 50, 50);

    public float fMinVolume = 10.0f;
    public float fSHDiff = 0.1f;

    public TetFloat43[] vTetVertices;
    public TetInt4[] vTetAdjIndices;
    public TetFloat43[] vTetBaryMatrices;
    public TetDepthMap[] vTetDepthMap;
    public TetInt4[] vTetSHIndices;
    public SHColor[] vSHColors;

    private void Awake()
    {
#if UNITY_EDITOR
#endif      
    }

    private void Update()
    {
#if UNITY_EDITOR
#endif
    }
}
