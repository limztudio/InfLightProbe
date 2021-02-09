using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TetIndex
{
    public int _0, _1, _2, _3;
};

public class InfProbeGen : MonoBehaviour
{
    public Vector3 vAABBExtents = new Vector3(50, 50, 50);
    public Vector3 vProbeSpacing = new Vector3(5, 5, 5);

    public Vector3[] vProbes;
    public TetIndex[] vTetIndices;

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
