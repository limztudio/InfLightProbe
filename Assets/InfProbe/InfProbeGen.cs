using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TetIndex
{
    public int _0, _1, _2, _3;
};
[System.Serializable]
public struct TetDepth
{
    public byte _00;
    public byte _01, _02;
    public byte _03, _04, _05;
    public byte _06, _07, _08, _09;
    public byte _10, _11, _12, _13, _14;
};
[System.Serializable]
public struct TetDepthMap
{
    public TetDepth _0, _1, _2, _3;
};

public class InfProbeGen : MonoBehaviour
{
    public Vector3 vAABBExtents = new Vector3(50, 50, 50);
    public Vector3 vProbeSpacing = new Vector3(5, 5, 5);

    public Vector3[] vProbes;
    public TetIndex[] vTetIndices;
    public TetDepthMap[] vTetDepthMap;

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
