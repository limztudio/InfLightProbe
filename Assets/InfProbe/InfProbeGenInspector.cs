using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InfProbeGen))]
public class InfProbeGenInspector : Editor
{
    private InfProbeGen probeGen;

    private Vector3 vOldAABBOrigin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    private Vector3 vOldAABBExtents = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
    private Vector3 vOldPorbeSpacing = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);


    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TGBuildTets(float[] vertices, uint numVert);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint TGGetTetIndexCount();
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetIndices(uint[] vOut);


    private void _generateProbes()
    {
        if (vOldAABBOrigin != probeGen.transform.position || vOldAABBExtents != probeGen.vAABBExtents || vOldPorbeSpacing != probeGen.vProbeSpacing)
        {
            var vAABBMin = probeGen.transform.position - probeGen.vAABBExtents;
            var vAABBMax = probeGen.transform.position + probeGen.vAABBExtents;

            int iLen = 0;
            for (float fZ = vAABBMin.z + probeGen.vProbeSpacing.z * 0.5f; fZ < vAABBMax.z; fZ += probeGen.vProbeSpacing.z)
                for (float fY = vAABBMin.y + probeGen.vProbeSpacing.y * 0.5f; fY < vAABBMax.y; fY += probeGen.vProbeSpacing.y)
                    for (float fX = vAABBMin.x + probeGen.vProbeSpacing.x * 0.5f; fX < vAABBMax.x; fX += probeGen.vProbeSpacing.x)
                        ++iLen;
            probeGen.vProbes = new Vector3[iLen];
            float[] fPassProbes = new float[iLen * 3];

            iLen = 0;
            for (float fZ = vAABBMin.z + probeGen.vProbeSpacing.z * 0.5f; fZ < vAABBMax.z; fZ += probeGen.vProbeSpacing.z)
            {
                for (float fY = vAABBMin.y + probeGen.vProbeSpacing.y * 0.5f; fY < vAABBMax.y; fY += probeGen.vProbeSpacing.y)
                {
                    for (float fX = vAABBMin.x + probeGen.vProbeSpacing.x * 0.5f; fX < vAABBMax.x; fX += probeGen.vProbeSpacing.x)
                    {
                        fPassProbes[iLen * 3 + 0] = fX;
                        fPassProbes[iLen * 3 + 1] = fY;
                        fPassProbes[iLen * 3 + 2] = fZ;
                        probeGen.vProbes[iLen++] = new Vector3(fX, fY, fZ);
                    }
                }
            }

            {
                TGBuildTets(fPassProbes, (uint)(fPassProbes.Length));

                iLen = (int)TGGetTetIndexCount();
                uint[] uRawTetIndices = new uint[iLen];
                TGGetTetIndices(uRawTetIndices);

                iLen >>= 2;
                probeGen.vTetIndices = new TetIndex[iLen];

                for (int i = 0; i < iLen; ++i)
                {
                    probeGen.vTetIndices[i]._0 = (int)uRawTetIndices[i * 4 + 0];
                    probeGen.vTetIndices[i]._1 = (int)uRawTetIndices[i * 4 + 1];
                    probeGen.vTetIndices[i]._2 = (int)uRawTetIndices[i * 4 + 2];
                    probeGen.vTetIndices[i]._3 = (int)uRawTetIndices[i * 4 + 3];
                }
            }

            vOldAABBOrigin = probeGen.transform.position;
            vOldAABBExtents = probeGen.vAABBExtents;
            vOldPorbeSpacing = probeGen.vProbeSpacing;
        }
    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        { // render AABB
            var vDisplayExtents = Handles.DoPositionHandle(probeGen.vAABBExtents + probeGen.transform.position, Quaternion.identity);
            probeGen.vAABBExtents = vDisplayExtents - probeGen.transform.position;
            probeGen.vAABBExtents.x = Mathf.Max(0, probeGen.vAABBExtents.x);
            probeGen.vAABBExtents.y = Mathf.Max(0, probeGen.vAABBExtents.y);
            probeGen.vAABBExtents.z = Mathf.Max(0, probeGen.vAABBExtents.z);

            Handles.color = Color.yellow;
            Handles.DrawWireCube(probeGen.transform.position, probeGen.vAABBExtents * 2);
        }

        _generateProbes();

        {
            var vSize = new Vector3(2, 2, 2);

            Handles.color = Color.magenta;
            foreach (var vProbe in probeGen.vProbes)
                Handles.DrawWireCube(vProbe, vSize);
        }
    }
}