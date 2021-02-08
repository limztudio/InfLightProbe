using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InfProbeGen : MonoBehaviour
{
    public Vector3 vAABBExtents = new Vector3(50, 50, 50);
    public Vector3 vProbeSpacing = new Vector3(5, 5, 5);

    public Vector3[] vProbes;

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
